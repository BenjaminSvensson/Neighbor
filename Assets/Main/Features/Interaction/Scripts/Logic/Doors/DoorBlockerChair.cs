using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Pickupable))]
    public sealed class DoorBlockerChair : MonoBehaviour, IPickupLifecycleReceiver
    {
        [SerializeField, Min(0f)] private float doorOffset = 0.85f;
        [SerializeField, Min(0f)] private float placementSurfacePadding = 0.02f;
        [SerializeField, Min(0f)] private float groundProbeHeight = 1.5f;
        [SerializeField, Min(0f)] private float groundProbeDistance = 3f;
        [SerializeField, Min(0f)] private float autoBlockRadius = 1.1f;
        [SerializeField, Range(0f, 45f)] private float leanAngle = 12f;
        [SerializeField] private bool attachToDoorSurface;
        [SerializeField, Min(0f)] private float doorSurfaceOffset = 0.03f;
        [SerializeField] private float doorSurfaceHeight = 1.15f;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private LayerMask doorMask = ~0;

        private Pickupable pickupable;
        private Rigidbody body;
        private Door blockedDoor;
        private RigidbodyConstraints originalConstraints;
        private bool hasFrozenBlockedBody;
        private bool blockDisabledUntilPlaced;
        private readonly Collider[] nearbyDoorHits = new Collider[8];
        private readonly RaycastHit[] groundHits = new RaycastHit[12];

        public bool IsBlockingDoor => blockedDoor != null;

        private void Awake()
        {
            pickupable = GetComponent<Pickupable>();
            body = GetComponent<Rigidbody>();
        }

        private void OnDisable()
        {
            ClearBlockedDoor();
        }

        private void Update()
        {
            if (blockedDoor == null)
            {
                TryAutoBlockNearbyDoor();
                return;
            }

            if (pickupable != null && pickupable.IsHeld)
            {
                ClearBlockedDoor();
            }
        }

        public bool TryBlockDoor(Door door, PlayerInteractor interactor)
        {
            if (door == null || interactor == null || pickupable == null)
            {
                return false;
            }

            if (!door.TryAddBlocker(this, interactor.transform.position))
            {
                return false;
            }

            ApplyBlockedPose(door);
            return true;
        }

        public void OnPickupStarted(Pickupable pickupable, PlayerInteractor interactor)
        {
            ClearBlockedDoor();
            blockDisabledUntilPlaced = false;
        }

        public void HandlePriedLoose()
        {
            ClearBlockedDoor();
            blockDisabledUntilPlaced = true;
        }

        public void HandleKickedLoose(Vector3 kickDirection, float impulse, float upwardImpulse)
        {
            ClearBlockedDoor();
            blockDisabledUntilPlaced = true;

            if (body == null || body.isKinematic || impulse <= 0f)
            {
                return;
            }

            Vector3 impulseDirection = kickDirection;
            impulseDirection.y = 0f;
            if (impulseDirection.sqrMagnitude <= 0.001f)
            {
                impulseDirection = transform.forward;
                impulseDirection.y = 0f;
            }

            body.WakeUp();
            body.AddForce(impulseDirection.normalized * impulse + Vector3.up * upwardImpulse, ForceMode.Impulse);
            body.AddTorque(Vector3.Cross(Vector3.up, impulseDirection.normalized) * impulse, ForceMode.Impulse);
        }

        public void OnPickupPlaced(Pickupable pickupable)
        {
            blockDisabledUntilPlaced = false;
        }

        private void TryAutoBlockNearbyDoor()
        {
            if (pickupable == null || pickupable.IsHeld || blockDisabledUntilPlaced || autoBlockRadius <= 0f)
            {
                return;
            }

            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, autoBlockRadius, nearbyDoorHits, doorMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Door door = nearbyDoorHits[i] != null ? nearbyDoorHits[i].GetComponentInParent<Door>() : null;
                if (door == null || !door.TryAddBlocker(this, transform.position, false))
                {
                    continue;
                }

                ApplyBlockedPose(door);
                return;
            }
        }

        private void ApplyBlockedPose(Door door)
        {
            Vector3 openingNormal = door.DefaultOpeningSideNormal.normalized;
            Quaternion rotation = GetBlockedRotation(openingNormal);
            Vector3 position = attachToDoorSurface
                ? GetDoorSurfacePlacementPosition(door, openingNormal, rotation)
                : GetGroundPlacementPosition(door, door.transform.position + openingNormal * doorOffset, rotation);

            pickupable.Place(position, rotation);
            blockedDoor = door;
            FreezeBlockedBody();
        }

        private Quaternion GetBlockedRotation(Vector3 openingNormal)
        {
            Quaternion facingDoor = Quaternion.LookRotation(-openingNormal, Vector3.up);
            return facingDoor * Quaternion.AngleAxis(leanAngle, Vector3.right);
        }

        private Vector3 GetDoorSurfacePlacementPosition(Door door, Vector3 openingNormal, Quaternion rotation)
        {
            Quaternion originalRotation = transform.rotation;
            transform.rotation = rotation;
            Bounds bounds = pickupable.GetPlacementBounds();
            transform.rotation = originalRotation;

            float normalExtent =
                Mathf.Abs(openingNormal.x) * bounds.extents.x +
                Mathf.Abs(openingNormal.y) * bounds.extents.y +
                Mathf.Abs(openingNormal.z) * bounds.extents.z;

            return door.transform.position
                + Vector3.up * doorSurfaceHeight
                + openingNormal * (normalExtent + doorSurfaceOffset);
        }

        private Vector3 GetGroundPlacementPosition(Door door, Vector3 desiredCenter, Quaternion rotation)
        {
            Quaternion originalRotation = transform.rotation;
            transform.rotation = rotation;
            Bounds bounds = pickupable.GetPlacementBounds();
            transform.rotation = originalRotation;

            float bottomOffset = transform.position.y - bounds.min.y;
            Vector3 probeOrigin = desiredCenter + Vector3.up * groundProbeHeight;
            if (TryGetGroundHit(probeOrigin, door, out RaycastHit hit))
            {
                desiredCenter.y = hit.point.y + bottomOffset + placementSurfacePadding;
            }

            return desiredCenter;
        }

        private bool TryGetGroundHit(Vector3 probeOrigin, Door door, out RaycastHit bestHit)
        {
            bestHit = default;
            int hitCount = Physics.RaycastNonAlloc(
                probeOrigin,
                Vector3.down,
                groundHits,
                groundProbeHeight + groundProbeDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);

            float bestDistance = float.PositiveInfinity;
            bool foundHit = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = groundHits[i];
                if (hit.collider == null
                    || hit.collider.GetComponentInParent<Door>() == door
                    || hit.collider.GetComponentInParent<Pickupable>() == pickupable)
                {
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    bestHit = hit;
                    foundHit = true;
                }
            }

            return foundHit;
        }

        private void FreezeBlockedBody()
        {
            if (body == null || hasFrozenBlockedBody)
            {
                return;
            }

            originalConstraints = body.constraints;
            RigidbodyVelocityUtility.ClearIfDynamic(body);
            body.constraints = RigidbodyConstraints.FreezeAll;
            body.Sleep();
            hasFrozenBlockedBody = true;
        }

        private void RestoreBlockedBody()
        {
            if (body == null || !hasFrozenBlockedBody)
            {
                return;
            }

            body.constraints = originalConstraints;
            body.WakeUp();
            hasFrozenBlockedBody = false;
        }

        private void ClearBlockedDoor()
        {
            RestoreBlockedBody();

            if (blockedDoor != null)
            {
                blockedDoor.RemoveBlocker(this);
                blockedDoor = null;
            }
        }
    }
}
