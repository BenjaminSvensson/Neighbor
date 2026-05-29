using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Pickupable))]
    public sealed class DoorBlockerChair : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float doorOffset = 0.85f;
        [SerializeField, Min(0f)] private float placementSurfacePadding = 0.02f;
        [SerializeField, Min(0f)] private float groundProbeHeight = 1.5f;
        [SerializeField, Min(0f)] private float groundProbeDistance = 3f;
        [SerializeField, Min(0f)] private float removalDistance = 0.65f;
        [SerializeField] private LayerMask groundMask = ~0;

        private Pickupable pickupable;
        private Door blockedDoor;
        private Vector3 blockedPosition;

        private void Awake()
        {
            pickupable = GetComponent<Pickupable>();
        }

        private void OnDisable()
        {
            ClearBlockedDoor();
        }

        private void Update()
        {
            if (blockedDoor == null)
            {
                return;
            }

            if ((pickupable != null && pickupable.IsHeld) || Vector3.Distance(transform.position, blockedPosition) > removalDistance)
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

            Vector3 openingNormal = door.DefaultOpeningSideNormal.normalized;
            Quaternion rotation = Quaternion.LookRotation(-openingNormal, Vector3.up);
            Vector3 position = GetPlacementPosition(door.transform.position + openingNormal * doorOffset, rotation);

            pickupable.Place(position, rotation);
            blockedDoor = door;
            blockedPosition = position;
            return true;
        }

        private Vector3 GetPlacementPosition(Vector3 desiredCenter, Quaternion rotation)
        {
            Quaternion originalRotation = transform.rotation;
            transform.rotation = rotation;
            Bounds bounds = pickupable.GetPlacementBounds();
            transform.rotation = originalRotation;

            float bottomOffset = transform.position.y - bounds.min.y;
            Vector3 probeOrigin = desiredCenter + Vector3.up * groundProbeHeight;
            if (Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit hit, groundProbeHeight + groundProbeDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                desiredCenter.y = hit.point.y + bottomOffset + placementSurfacePadding;
            }

            return desiredCenter;
        }

        private void ClearBlockedDoor()
        {
            if (blockedDoor != null)
            {
                blockedDoor.RemoveBlocker(this);
                blockedDoor = null;
            }
        }
    }
}
