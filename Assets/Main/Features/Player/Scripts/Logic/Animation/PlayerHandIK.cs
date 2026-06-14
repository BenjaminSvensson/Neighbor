using Neighbor.Main.Features.Interaction;
using UnityEngine;

namespace Neighbor.Main.Features.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerHandIK : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float blendSpeed = 8f;
        [SerializeField, Range(0f, 1f)] private float positionWeight = 0.92f;
        [SerializeField, Min(0f)] private float minimumGripSpacing = 0.1f;
        [SerializeField, Min(0f)] private float maximumGripSpacing = 0.34f;
        [SerializeField, Min(0f)] private float handDepthOffset = 0.08f;
        [SerializeField, Min(0f)] private float chargeHandDrop = 0.12f;

        private Animator animator;
        private PlayerInteractor interactor;
        private float currentWeight;
        private bool suppressed;

        public void Configure(Animator targetAnimator, PlayerInteractor targetInteractor)
        {
            animator = targetAnimator != null ? targetAnimator : GetComponent<Animator>();
            interactor = targetInteractor;
        }

        public void SetSuppressed(bool value)
        {
            suppressed = value;
        }

        private void Awake()
        {
            animator = animator != null ? animator : GetComponent<Animator>();
            interactor = interactor != null ? interactor : GetComponentInParent<PlayerInteractor>();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null)
            {
                return;
            }

            Pickupable heldPickup = interactor != null ? interactor.HeldPickup : null;
            float targetWeight = heldPickup != null && !suppressed ? positionWeight : 0f;
            currentWeight = Mathf.MoveTowards(currentWeight, targetWeight, blendSpeed * Time.deltaTime);

            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, currentWeight);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, currentWeight);
            if (heldPickup == null || currentWeight <= 0f)
            {
                return;
            }

            Transform view = interactor.ViewTransform;
            Bounds bounds = heldPickup.GetPlacementBounds();
            float gripSpacing = Mathf.Clamp(
                GetGripExtent(bounds, view.right) * 0.8f,
                minimumGripSpacing,
                maximumGripSpacing);
            Vector3 gripCenter = bounds.center - view.up * (interactor.ThrowCharge * chargeHandDrop);
            float probeDepth = GetGripExtent(bounds, view.forward) + Mathf.Max(handDepthOffset, 0.1f);
            Vector3 leftProbe = gripCenter - view.right * gripSpacing - view.forward * probeDepth;
            Vector3 rightProbe = gripCenter + view.right * gripSpacing - view.forward * probeDepth;

            animator.SetIKPosition(AvatarIKGoal.LeftHand, GetGripTarget(heldPickup, leftProbe, view.forward));
            animator.SetIKPosition(AvatarIKGoal.RightHand, GetGripTarget(heldPickup, rightProbe, view.forward));
        }

        private Vector3 GetGripTarget(Pickupable pickup, Vector3 probePosition, Vector3 viewForward)
        {
            Vector3 surfacePoint = pickup.GetClosestGripPoint(probePosition);
            Vector3 outward = probePosition - surfacePoint;
            if (outward.sqrMagnitude <= 0.0001f)
            {
                outward = -viewForward;
            }

            return surfacePoint + outward.normalized * handDepthOffset;
        }

        private static float GetGripExtent(Bounds bounds, Vector3 axis)
        {
            Vector3 extents = bounds.extents;
            Vector3 absoluteAxis = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
            return Vector3.Dot(extents, absoluteAxis);
        }
    }
}
