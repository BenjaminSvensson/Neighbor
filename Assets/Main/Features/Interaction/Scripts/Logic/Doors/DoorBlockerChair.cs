using Neighbor.Main.Features.Neighbor;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Pickupable))]
    public sealed class DoorBlockerChair : MonoBehaviour, IPickupLifecycleReceiver
    {
        public enum BlockerPlacementOrigin
        {
            Player,
            Reinforcement
        }

        [SerializeField, Min(0f)] private float doorOffset = 0.85f;
        [SerializeField, Min(0f)] private float placementSurfacePadding = 0.02f;
        [SerializeField, Min(0f)] private float groundProbeHeight = 1.5f;
        [SerializeField, Min(0f)] private float groundProbeDistance = 3f;
        [SerializeField, Min(0f)] private float autoBlockRadius = 1.1f;
        [SerializeField, Range(0f, 45f)] private float leanAngle = 12f;
        [SerializeField] private bool attachToDoorSurface;
        [SerializeField, Min(0f)] private float doorSurfaceOffset = 0.03f;
        [SerializeField] private float doorSurfaceHeight = 1.15f;
        [SerializeField, Min(0f)] private float reinforcementBoardVerticalSpacing = 0.42f;
        [SerializeField, Range(0f, 25f)] private float reinforcementBoardRollVariation = 7f;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private LayerMask doorMask = ~0;

        private Pickupable pickupable;
        private Rigidbody body;
        private bool isWoodBoard;
        private Door blockedDoor;
        private RigidbodyConstraints originalConstraints;
        private bool hasFrozenBlockedBody;
        private bool blockDisabledUntilPlaced;
        private bool isBlockingReinforcedOpening;
        [SerializeField, Tooltip("Who placed this blocker. Reinforcement spawns set this automatically.")]
        private BlockerPlacementOrigin placementOrigin = BlockerPlacementOrigin.Player;
        private readonly Collider[] nearbyDoorHits = new Collider[8];
        private readonly RaycastHit[] groundHits = new RaycastHit[12];
        private ItemAudioFeedback audioFeedback;

        public bool IsBlockingDoor => blockedDoor != null || isBlockingReinforcedOpening;
        public BlockerPlacementOrigin PlacementOrigin => placementOrigin;
        public bool IsReinforcementPlaced => placementOrigin == BlockerPlacementOrigin.Reinforcement;

        public void MarkAsReinforcement()
        {
            placementOrigin = BlockerPlacementOrigin.Reinforcement;
        }

        private void Awake()
        {
            ResolveDependencies();
        }

        private void ResolveDependencies()
        {
            pickupable = GetComponent<Pickupable>();
            body = GetComponent<Rigidbody>();
            isWoodBoard = GetComponent<WoodBoardPryTarget>() != null;
            audioFeedback = ItemAudioFeedback.Resolve(gameObject);
        }

        private void OnDisable()
        {
            ClearBlockedDoor();
        }

        private void Update()
        {
            if (blockedDoor == null && !isBlockingReinforcedOpening)
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
            ResolveDependencies();
            if (door == null || interactor == null || pickupable == null)
            {
                return false;
            }

            Vector3 blockerSidePosition = isWoodBoard ? transform.position : interactor.transform.position;
            if (!door.TryAddBlocker(this, blockerSidePosition))
            {
                return false;
            }

            placementOrigin = BlockerPlacementOrigin.Player;
            ApplyBlockedPose(door, 0, false);
            return true;
        }

        public bool TryBlockDoorAsReinforcement(Door door)
        {
            return TryBlockDoorAsReinforcement(door, 0);
        }

        public bool TryBlockDoorAsReinforcement(Door door, int placementIndex)
        {
            ResolveDependencies();
            if (door == null || pickupable == null)
            {
                return false;
            }

            blockDisabledUntilPlaced = false;
            Vector3 openingSidePosition = door.transform.position + door.DefaultOpeningSideNormal;
            if (!door.TryAddBlocker(this, openingSidePosition, false))
            {
                return false;
            }

            placementOrigin = BlockerPlacementOrigin.Reinforcement;
            ApplyBlockedPose(door, placementIndex, true);
            return true;
        }

        public bool TryBlockOpeningAsReinforcement(Vector3 position, Quaternion rotation)
        {
            ResolveDependencies();
            if (pickupable == null)
            {
                return false;
            }

            blockDisabledUntilPlaced = false;
            ClearBlockedDoor();
            placementOrigin = BlockerPlacementOrigin.Reinforcement;
            pickupable.Place(position, rotation);
            isBlockingReinforcedOpening = true;
            audioFeedback?.Play(ItemSoundProfile.Impact, 0.42f);
            FreezeBlockedBody();
            return true;
        }

        public void ReleaseForDoorReset()
        {
            ClearBlockedDoor();
            blockDisabledUntilPlaced = true;
        }

        public void OnPickupStarted(Pickupable pickupable, PlayerInteractor interactor)
        {
            if (blockedDoor != null)
            {
                NeighborEnvironmentalAwareness.Report(transform.position, 0.55f, gameObject);
            }

            ClearBlockedDoor();
            blockDisabledUntilPlaced = false;
            placementOrigin = BlockerPlacementOrigin.Player;
        }

        public void HandlePriedLoose()
        {
            ClearBlockedDoor();
            blockDisabledUntilPlaced = true;
        }

        public void HandleKickedLoose(Vector3 kickDirection, float impulse, float upwardImpulse)
        {
            audioFeedback?.Play(ItemSoundProfile.Impact, 0.75f);
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

                ApplyBlockedPose(door, 0, false);
                return;
            }
        }

        private void ApplyBlockedPose(Door door, int placementIndex, bool varyReinforcementPose)
        {
            Vector3 openingNormal = door.DefaultOpeningSideNormal.normalized;
            Quaternion rotation = GetBlockedRotation(openingNormal, placementIndex, varyReinforcementPose);
            bool placeOnDoorSurface = attachToDoorSurface || varyReinforcementPose && isWoodBoard;
            Vector3 position = placeOnDoorSurface
                ? GetDoorSurfacePlacementPosition(door, openingNormal, rotation, placementIndex, varyReinforcementPose)
                : GetGroundPlacementPosition(door, door.transform.position + openingNormal * doorOffset, rotation);

            pickupable.Place(position, rotation);
            blockedDoor = door;
            isBlockingReinforcedOpening = false;
            audioFeedback?.Play(ItemSoundProfile.Impact, 0.42f);
            FreezeBlockedBody();
        }

        private Quaternion GetBlockedRotation(Vector3 openingNormal, int placementIndex, bool varyReinforcementPose)
        {
            Quaternion facingDoor = Quaternion.LookRotation(-openingNormal, Vector3.up);
            float boardRoll = varyReinforcementPose ? GetReinforcementBoardRoll(placementIndex) : 0f;
            return facingDoor
                * Quaternion.AngleAxis(boardRoll, Vector3.forward)
                * Quaternion.AngleAxis(leanAngle, Vector3.right);
        }

        private Vector3 GetDoorSurfacePlacementPosition(
            Door door,
            Vector3 openingNormal,
            Quaternion rotation,
            int placementIndex,
            bool varyReinforcementPose)
        {
            Quaternion originalRotation = transform.rotation;
            transform.rotation = rotation;
            Bounds bounds = pickupable.GetPlacementBounds();
            transform.rotation = originalRotation;

            float normalExtent =
                Mathf.Abs(openingNormal.x) * bounds.extents.x +
                Mathf.Abs(openingNormal.y) * bounds.extents.y +
                Mathf.Abs(openingNormal.z) * bounds.extents.z;

            float surfaceHeight = varyReinforcementPose
                ? GetReinforcementBoardSurfaceHeight(placementIndex)
                : doorSurfaceHeight;

            return door.transform.position
                + Vector3.up * surfaceHeight
                + openingNormal * (normalExtent + doorSurfaceOffset);
        }

        private float GetReinforcementBoardSurfaceHeight(int placementIndex)
        {
            return Mathf.Max(0.1f, doorSurfaceHeight + GetAlternatingPlacementOffset(placementIndex) * reinforcementBoardVerticalSpacing);
        }

        private float GetReinforcementBoardRoll(int placementIndex)
        {
            int safeIndex = Mathf.Max(0, placementIndex);
            float direction = safeIndex % 2 == 0 ? 1f : -1f;
            float magnitude = safeIndex / 2 + 1f;
            return direction * magnitude * reinforcementBoardRollVariation;
        }

        private static int GetAlternatingPlacementOffset(int placementIndex)
        {
            int safeIndex = Mathf.Max(0, placementIndex);
            if (safeIndex == 0)
            {
                return 0;
            }

            int row = (safeIndex + 1) / 2;
            return safeIndex % 2 == 0 ? row : -row;
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
            isBlockingReinforcedOpening = false;

            if (blockedDoor != null)
            {
                blockedDoor.RemoveBlocker(this);
                blockedDoor = null;
            }
        }
    }
}
