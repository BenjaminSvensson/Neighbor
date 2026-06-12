using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Neighbor
{
    public sealed class NeighborVision : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform eye;
        [SerializeField] private Transform target;

        [Header("Vision")]
        [SerializeField, Min(0f)] private float viewDistance = 13f;
        [SerializeField, Range(1f, 180f)] private float viewAngle = 95f;
        [SerializeField, Min(0f)] private float closeDetectionDistance = 2.2f;
        [SerializeField] private LayerMask lineOfSightMask = ~0;
        [SerializeField, Min(0f)] private float eyeHeight = 1.65f;

        public Transform VisibleTarget { get; private set; }
        public Vector3 LastSeenPosition { get; private set; }
        public Vector3 EyePosition => eye != null
            ? eye.position + Vector3.up * eyeHeight
            : transform.position + Vector3.up * eyeHeight;
        public float ViewDistance => viewDistance;
        public float ViewAngle => viewAngle;
        public float CloseDetectionDistance => closeDetectionDistance;

        private void Awake()
        {
            if (eye == null)
            {
                eye = transform;
            }

            ResolveTarget();
        }

        public bool TrySeeTarget(out Transform seenTarget, out Vector3 seenPosition)
        {
            ResolveTarget();
            VisibleTarget = null;
            seenTarget = null;
            seenPosition = LastSeenPosition;

            if (target == null)
            {
                return false;
            }

            PlayerHidingState hidingState = target.GetComponent<PlayerHidingState>() ?? target.GetComponentInChildren<PlayerHidingState>();
            if (hidingState != null && hidingState.IsHidden)
            {
                return false;
            }

            Vector3 origin = EyePosition;
            Vector3 targetPosition = GetTargetAimPoint(target);
            Vector3 toTarget = targetPosition - origin;
            float distance = toTarget.magnitude;
            if (distance > viewDistance || distance <= 0.01f)
            {
                return false;
            }

            float angle = Vector3.Angle(transform.forward, toTarget);
            bool insideViewCone = angle <= viewAngle * 0.5f;
            bool closeEnoughToNotice = distance <= closeDetectionDistance;
            if (!insideViewCone && !closeEnoughToNotice)
            {
                return false;
            }

            if (Physics.Raycast(origin, toTarget / distance, out RaycastHit hit, distance, lineOfSightMask, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(target)
                && hit.transform.root != target.root)
            {
                return false;
            }

            VisibleTarget = target;
            LastSeenPosition = target.position;
            seenTarget = target;
            seenPosition = LastSeenPosition;
            return true;
        }

        private static Vector3 GetTargetAimPoint(Transform targetTransform)
        {
            CharacterController controller = targetTransform.GetComponent<CharacterController>() ?? targetTransform.GetComponentInChildren<CharacterController>();
            if (controller == null)
            {
                return targetTransform.position + Vector3.up;
            }

            return controller.bounds.center;
        }

        private void ResolveTarget()
        {
            if (target != null)
            {
                return;
            }

            PlayerController playerController = FindAnyObjectByType<PlayerController>();
            if (playerController != null)
            {
                target = playerController.transform;
            }
        }
    }
}
