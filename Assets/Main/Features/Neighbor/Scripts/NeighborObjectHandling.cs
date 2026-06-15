using System.Collections.Generic;
using Neighbor.Main.Features.Interaction;
using UnityEngine;

namespace Neighbor.Main.Features.Neighbor
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NeighborBrain))]
    [RequireComponent(typeof(NeighborMotor))]
    public sealed class NeighborObjectHandling : MonoBehaviour
    {
        public enum ActivityPhase
        {
            None,
            ApproachingPickup,
            CarryingToPlacement,
            Placing
        }

        [Header("Experimental Object Handling")]
        [SerializeField] private bool enableObjectHandling;
        [SerializeField, Range(0f, 1f)] private float routineChance = 0.18f;
        [SerializeField, Min(0f)] private float retryCooldown = 8f;

        [Header("Pickup Selection")]
        [SerializeField, Min(0.5f)] private float pickupSearchRadius = 5f;
        [SerializeField, Min(0.1f)] private float pickupApproachDistance = 1.15f;
        [SerializeField, Min(0f)] private float pickupDestinationSampleRadius = 1.5f;
        [SerializeField, Min(0f)] private float maximumPickupMass = 5f;
        [SerializeField, Min(0.1f)] private float maximumPickupSize = 1.25f;
        [SerializeField] private LayerMask pickupSearchMask = ~0;
        [SerializeField] private bool ignoreTaskObjects = true;
        [SerializeField] private bool ignoreDoorBlockers = true;
        [SerializeField] private bool ignoreNeighborPlacedCameras = true;

        [Header("Carry")]
        [SerializeField] private Transform carryAnchor;
        [SerializeField] private Vector3 handCarryOffset = new(0.08f, 0.05f, 0.12f);
        [SerializeField] private Vector3 fallbackCarryOffset = new(0.38f, 1.15f, 0.42f);
        [SerializeField] private Vector3 carryRotationEuler = new(0f, 0f, 0f);
        [SerializeField, Min(0f)] private float minimumCarryTime = 1.5f;
        [SerializeField, Min(0f)] private float maximumCarryTime = 4f;

        [Header("Placement")]
        [SerializeField, Min(0.5f)] private float placementRadius = 5f;
        [SerializeField, Min(0f)] private float minimumCarryDistance = 1.5f;
        [SerializeField, Min(1)] private int placementDestinationAttempts = 10;
        [SerializeField, Min(0f)] private float placementPause = 0.35f;
        [SerializeField, Min(0f)] private float placementProbeHeight = 1.5f;
        [SerializeField, Min(0.1f)] private float placementProbeDistance = 4f;
        [SerializeField, Range(0f, 1f)] private float placementMinimumUpDot = 0.65f;
        [SerializeField, Min(0f)] private float placementSurfacePadding = 0.025f;
        [SerializeField, Min(0f)] private float placementClearancePadding = 0.03f;
        [SerializeField] private LayerMask placementMask = ~0;

        private readonly Collider[] pickupSearchHits = new Collider[64];
        private readonly Collider[] placementBlockHits = new Collider[32];
        private readonly HashSet<Pickupable> consideredPickups = new();
        private NeighborMotor motor;
        private Animator animator;
        private Transform resolvedCarryAnchor;
        private Pickupable targetPickup;
        private Pickupable heldPickup;
        private ActivityPhase phase;
        private Vector3 pickupOrigin;
        private Vector3 placementDestination;
        private Vector3 heldLocalBoundsCenter;
        private Vector3 heldBoundsSize;
        private float heldPivotBottomOffset;
        private float placeAfterTime;
        private float nextAttemptTime;

        public bool EnableObjectHandling
        {
            get => enableObjectHandling;
            set
            {
                enableObjectHandling = value;
                if (!enableObjectHandling)
                {
                    CancelActivity();
                }
            }
        }

        public bool IsActive => phase != ActivityPhase.None;
        public bool IsHoldingObject => heldPickup != null && heldPickup.IsHeld;
        public ActivityPhase CurrentPhase => phase;
        public Pickupable TargetPickup => targetPickup;
        public Pickupable HeldPickup => heldPickup;
        public Vector3 PlacementDestination => placementDestination;

        private void Awake()
        {
            motor = GetComponent<NeighborMotor>();
            animator = GetComponentInChildren<Animator>(true);
            ResolveCarryAnchor();
        }

        private void OnDisable()
        {
            CancelActivity();
        }

        private void LateUpdate()
        {
            UpdateHeldPose();
        }

        public bool TryBeginRoutine(out Vector3 goal)
        {
            goal = transform.position;
            if (!enableObjectHandling
                || IsActive
                || motor == null
                || Time.time < nextAttemptTime
                || Random.value > routineChance)
            {
                return false;
            }

            nextAttemptTime = Time.time + retryCooldown;
            targetPickup = FindBestPickupCandidate();
            if (targetPickup == null
                || !motor.TrySetDestinationNear(
                    targetPickup.transform.position,
                    pickupDestinationSampleRadius,
                    out goal))
            {
                targetPickup = null;
                return false;
            }

            phase = ActivityPhase.ApproachingPickup;
            return true;
        }

        public bool UpdateActivity(out Vector3 goal)
        {
            goal = phase == ActivityPhase.ApproachingPickup
                ? targetPickup != null ? targetPickup.transform.position : transform.position
                : placementDestination;
            if (!enableObjectHandling || phase == ActivityPhase.None)
            {
                CancelActivity();
                return false;
            }

            switch (phase)
            {
                case ActivityPhase.ApproachingPickup:
                    return UpdateApproachingPickup(out goal);
                case ActivityPhase.CarryingToPlacement:
                    return UpdateCarryingToPlacement(out goal);
                case ActivityPhase.Placing:
                    return UpdatePlacing(out goal);
                default:
                    return false;
            }
        }

        public void CancelActivity()
        {
            if (heldPickup != null && heldPickup.IsHeld)
            {
                PlaceHeldObjectNear(transform.position);
            }

            targetPickup = null;
            heldPickup = null;
            phase = ActivityPhase.None;
            nextAttemptTime = Mathf.Max(nextAttemptTime, Time.time + retryCooldown);
        }

        private bool UpdateApproachingPickup(out Vector3 goal)
        {
            goal = targetPickup != null ? targetPickup.transform.position : transform.position;
            if (!IsPickupCandidateValid(targetPickup))
            {
                CancelActivity();
                return false;
            }

            Vector3 offset = targetPickup.transform.position - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > pickupApproachDistance * pickupApproachDistance && !motor.HasArrived)
            {
                return true;
            }

            motor.Stop();
            motor.FaceTowards(targetPickup.transform.position, 12f);
            Pickupable pickup = targetPickup;
            targetPickup = null;
            CacheHeldBounds(pickup);
            pickupOrigin = pickup.transform.position;
            pickup.Pickup(null);
            if (!pickup.IsHeld)
            {
                CancelActivity();
                return false;
            }

            heldPickup = pickup;
            UpdateHeldPose();
            if (!TryChoosePlacementDestination(out placementDestination)
                || !motor.SetDestination(placementDestination))
            {
                CancelActivity();
                return false;
            }

            placeAfterTime = Time.time + Random.Range(
                minimumCarryTime,
                Mathf.Max(minimumCarryTime, maximumCarryTime));
            goal = placementDestination;
            phase = ActivityPhase.CarryingToPlacement;
            return true;
        }

        private bool UpdateCarryingToPlacement(out Vector3 goal)
        {
            goal = placementDestination;
            if (heldPickup == null || !heldPickup.IsHeld)
            {
                heldPickup = null;
                phase = ActivityPhase.None;
                return false;
            }

            if (!motor.HasArrived || Time.time < placeAfterTime)
            {
                return true;
            }

            motor.Stop();
            placeAfterTime = Time.time + placementPause;
            phase = ActivityPhase.Placing;
            return true;
        }

        private bool UpdatePlacing(out Vector3 goal)
        {
            goal = placementDestination;
            if (heldPickup == null || !heldPickup.IsHeld)
            {
                heldPickup = null;
                phase = ActivityPhase.None;
                return false;
            }

            if (Time.time < placeAfterTime)
            {
                motor.FaceTowards(placementDestination, 8f);
                return true;
            }

            PlaceHeldObjectNear(placementDestination);
            targetPickup = null;
            heldPickup = null;
            phase = ActivityPhase.None;
            nextAttemptTime = Time.time + retryCooldown;
            return false;
        }

        private Pickupable FindBestPickupCandidate()
        {
            consideredPickups.Clear();
            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                pickupSearchRadius,
                pickupSearchHits,
                pickupSearchMask,
                QueryTriggerInteraction.Ignore);
            Pickupable bestPickup = null;
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                Pickupable pickup = pickupSearchHits[i] != null
                    ? pickupSearchHits[i].GetComponentInParent<Pickupable>()
                    : null;
                if (pickup == null || !consideredPickups.Add(pickup) || !IsPickupCandidateValid(pickup))
                {
                    continue;
                }

                float score = Vector3.Distance(transform.position, pickup.transform.position)
                    + Random.Range(-pickupSearchRadius * 0.25f, pickupSearchRadius * 0.25f);
                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestPickup = pickup;
            }

            return bestPickup;
        }

        private bool IsPickupCandidateValid(Pickupable pickup)
        {
            if (pickup == null
                || !pickup.isActiveAndEnabled
                || pickup.IsHeld
                || pickup.IsInventoryStored
                || !pickup.CanInteract(null)
                || ignoreTaskObjects && pickup.GetComponentInParent<NeighborTaskLocation>() != null
                || ignoreDoorBlockers && pickup.GetComponentInChildren<DoorBlockerChair>() != null)
            {
                return false;
            }

            SecurityCamera camera = pickup.GetComponentInChildren<SecurityCamera>();
            if (ignoreNeighborPlacedCameras && camera != null && camera.IsNeighborPlaced)
            {
                return false;
            }

            Rigidbody body = pickup.GetComponent<Rigidbody>();
            if (body == null || body.mass > maximumPickupMass)
            {
                return false;
            }

            Bounds bounds = pickup.GetPlacementBounds();
            return Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) <= maximumPickupSize;
        }

        private bool TryChoosePlacementDestination(out Vector3 destination)
        {
            destination = transform.position;
            float bestScore = float.NegativeInfinity;
            bool found = false;
            int attempts = Mathf.Max(1, placementDestinationAttempts);
            for (int i = 0; i < attempts; i++)
            {
                if (!motor.TryGetRandomReachablePoint(pickupOrigin, placementRadius, out Vector3 candidate))
                {
                    continue;
                }

                float carriedDistance = Vector3.Distance(pickupOrigin, candidate);
                if (carriedDistance < minimumCarryDistance)
                {
                    continue;
                }

                float score = carriedDistance + Random.Range(0f, placementRadius * 0.5f);
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                destination = candidate;
                found = true;
            }

            return found;
        }

        private void PlaceHeldObjectNear(Vector3 center)
        {
            if (heldPickup == null || !heldPickup.IsHeld)
            {
                return;
            }

            if (TryFindPlacementPose(center, out Vector3 position, out Quaternion rotation))
            {
                heldPickup.Place(position, rotation);
            }
            else
            {
                heldPickup.Drop();
            }
        }

        private bool TryFindPlacementPose(Vector3 center, out Vector3 position, out Quaternion rotation)
        {
            const int directions = 8;
            rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            for (int ring = 0; ring < 3; ring++)
            {
                float radius = ring * 0.45f;
                for (int i = 0; i < directions; i++)
                {
                    Vector3 direction = Quaternion.AngleAxis(i * (360f / directions), Vector3.up) * transform.forward;
                    Vector3 probePosition = center + direction * radius + Vector3.up * placementProbeHeight;
                    if (!Physics.Raycast(
                            probePosition,
                            Vector3.down,
                            out RaycastHit hit,
                            placementProbeDistance,
                            placementMask,
                            QueryTriggerInteraction.Ignore)
                        || hit.normal.y < placementMinimumUpDot)
                    {
                        continue;
                    }

                    Vector3 candidatePosition = hit.point
                        + Vector3.up * (heldPivotBottomOffset + placementSurfacePadding);
                    if (!IsPlacementClear(candidatePosition, rotation))
                    {
                        continue;
                    }

                    position = candidatePosition;
                    return true;
                }
            }

            position = center;
            return false;
        }

        private bool IsPlacementClear(Vector3 position, Quaternion rotation)
        {
            Vector3 halfExtents = Vector3.Max(
                heldBoundsSize * 0.5f - Vector3.one * placementClearancePadding,
                Vector3.one * 0.03f);
            Vector3 center = position + rotation * heldLocalBoundsCenter;
            int hitCount = Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                placementBlockHits,
                rotation,
                placementMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = placementBlockHits[i];
                if (hit != null
                    && !hit.transform.IsChildOf(transform)
                    && !transform.IsChildOf(hit.transform)
                    && (heldPickup == null || !hit.transform.IsChildOf(heldPickup.transform)))
                {
                    return false;
                }
            }

            return true;
        }

        private void CacheHeldBounds(Pickupable pickup)
        {
            Bounds bounds = pickup.GetPlacementBounds();
            heldLocalBoundsCenter = pickup.transform.InverseTransformVector(bounds.center - pickup.transform.position);
            heldBoundsSize = bounds.size;
            heldPivotBottomOffset = Mathf.Max(0f, pickup.transform.position.y - bounds.min.y);
        }

        private void UpdateHeldPose()
        {
            if (heldPickup == null || !heldPickup.IsHeld)
            {
                return;
            }

            ResolveCarryAnchor();
            Quaternion rotation = transform.rotation * Quaternion.Euler(carryRotationEuler);
            Vector3 anchorPosition;
            if (resolvedCarryAnchor != null)
            {
                anchorPosition = resolvedCarryAnchor.TransformPoint(handCarryOffset);
            }
            else
            {
                anchorPosition = transform.TransformPoint(fallbackCarryOffset);
            }

            Vector3 pickupPosition = anchorPosition - rotation * heldLocalBoundsCenter;
            heldPickup.SnapHeldPose(pickupPosition, rotation);
        }

        private void ResolveCarryAnchor()
        {
            if (carryAnchor != null)
            {
                resolvedCarryAnchor = carryAnchor;
                return;
            }

            if (resolvedCarryAnchor == null && animator != null && animator.isHuman)
            {
                resolvedCarryAnchor = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }
        }
    }
}
