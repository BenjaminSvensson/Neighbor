using UnityEngine;

namespace Neighbor.Main.Features.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerAnimationController : MonoBehaviour
    {
        private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
        private static readonly int WalkState = Animator.StringToHash("Base Layer.Walk");
        private static readonly int RunState = Animator.StringToHash("Base Layer.Run");
        private static readonly int CrouchIdleState = Animator.StringToHash("Base Layer.CrouchIdle");
        private static readonly int CrouchWalkState = Animator.StringToHash("Base Layer.CrouchWalk");
        private static readonly int SlideState = Animator.StringToHash("Base Layer.Slide");
        private static readonly int JumpStartState = Animator.StringToHash("Base Layer.JumpStart");
        private static readonly int AirborneState = Animator.StringToHash("Base Layer.Airborne");
        private static readonly int LandState = Animator.StringToHash("Base Layer.Land");

        [SerializeField] private PlayerController playerController;
        [SerializeField] private Animator animator;
        [SerializeField, Min(0f)] private float movingThreshold = 0.08f;
        [SerializeField, Min(0f)] private float transitionDuration = 0.12f;
        [SerializeField, Min(0f)] private float jumpStartHoldDuration = 0.14f;
        [SerializeField, Min(0f)] private float landingHoldDuration = 0.18f;
        [SerializeField, Min(0.1f)] private float minimumLocomotionPlaybackSpeed = 0.75f;
        [SerializeField, Min(0.1f)] private float maximumLocomotionPlaybackSpeed = 1.35f;

        private int currentState;
        private int heldActionState;
        private float heldActionUntil;

        private void Awake()
        {
            playerController = playerController != null
                ? playerController
                : GetComponentInParent<PlayerController>();
            animator = animator != null ? animator : GetComponentInChildren<Animator>(true);

            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }

        private void LateUpdate()
        {
            if (animator == null || playerController == null)
            {
                return;
            }

            UpdateHeldAction();
            int desiredState = Time.time < heldActionUntil ? heldActionState : ChooseLocomotionState();
            if (desiredState != currentState)
            {
                animator.CrossFadeInFixedTime(desiredState, transitionDuration);
                currentState = desiredState;
            }

            animator.speed = GetPlaybackSpeed(desiredState);
        }

        private void UpdateHeldAction()
        {
            if (playerController.JumpStartedThisFrame)
            {
                heldActionState = JumpStartState;
                heldActionUntil = Time.time + jumpStartHoldDuration;
                return;
            }

            if (playerController.LandedThisFrame)
            {
                heldActionState = LandState;
                heldActionUntil = Time.time + landingHoldDuration;
            }
        }

        private int ChooseLocomotionState()
        {
            if (playerController.IsLedgeClimbing || !playerController.IsGrounded)
            {
                return AirborneState;
            }

            if (playerController.IsSliding)
            {
                return SlideState;
            }

            bool isMoving = playerController.MoveAmount > movingThreshold;
            if (playerController.IsCrouching)
            {
                return isMoving ? CrouchWalkState : CrouchIdleState;
            }

            if (playerController.IsRunning && isMoving)
            {
                return RunState;
            }

            return isMoving ? WalkState : IdleState;
        }

        private float GetPlaybackSpeed(int state)
        {
            float referenceSpeed01 = state == RunState
                ? 1f
                : state == CrouchWalkState || state == SlideState
                    ? 0.32f
                    : state == WalkState
                        ? 0.72f
                        : 0f;

            if (referenceSpeed01 <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp(
                playerController.Speed01 / referenceSpeed01,
                minimumLocomotionPlaybackSpeed,
                maximumLocomotionPlaybackSpeed);
        }

        private void OnDisable()
        {
            if (animator != null)
            {
                animator.speed = 1f;
            }
        }
    }
}
