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
            Vector3 gripCenter = bounds.center
                - view.forward * handDepthOffset
                - view.up * (interactor.ThrowCharge * chargeHandDrop);

            animator.SetIKPosition(AvatarIKGoal.LeftHand, gripCenter - view.right * gripSpacing);
            animator.SetIKPosition(AvatarIKGoal.RightHand, gripCenter + view.right * gripSpacing);
        }

        private static float GetGripExtent(Bounds bounds, Vector3 axis)
        {
            Vector3 extents = bounds.extents;
            Vector3 absoluteAxis = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
            return Vector3.Dot(extents, absoluteAxis);
        }
    }
}
