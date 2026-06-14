using Neighbor.Main.Features.Interaction;
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
        private static readonly int GrabState = Animator.StringToHash("Base Layer.Grab");
        private static readonly int DropState = Animator.StringToHash("Base Layer.Drop");
        private static readonly int ThrowState = Animator.StringToHash("Base Layer.Throw");
        private static readonly int OpenDoorState = Animator.StringToHash("Base Layer.OpenDoor");
        private static readonly int ClimbState = Animator.StringToHash("Base Layer.Climb");

        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerInteractor playerInteractor;
        [SerializeField] private Animator animator;
        [SerializeField, Min(0f)] private float movingThreshold = 0.08f;
        [SerializeField, Min(0f)] private float transitionDuration = 0.12f;
        [SerializeField, Min(0f)] private float jumpStartHoldDuration = 0.14f;
        [SerializeField, Min(0f)] private float landingHoldDuration = 0.18f;
        [SerializeField, Min(0f)] private float grabHoldDuration = 0.22f;
        [SerializeField, Min(0f)] private float dropHoldDuration = 0.22f;
        [SerializeField, Min(0f)] private float throwHoldDuration = 0.32f;
        [SerializeField, Min(0f)] private float openDoorHoldDuration = 0.7f;
        [SerializeField, Min(0.1f)] private float grabPlaybackSpeed = 2.5f;
        [SerializeField, Min(0.1f)] private float dropPlaybackSpeed = 2.5f;
        [SerializeField, Min(0.1f)] private float climbPlaybackSpeed = 3f;
        [SerializeField, Min(0.1f)] private float minimumLocomotionPlaybackSpeed = 0.75f;
        [SerializeField, Min(0.1f)] private float maximumLocomotionPlaybackSpeed = 1.35f;

        private int currentState;
        private int heldActionState;
        private float heldActionUntil;
        private PlayerHandIK handIK;

        private void Awake()
        {
            playerController = playerController != null
                ? playerController
                : GetComponentInParent<PlayerController>();
            playerInteractor = playerInteractor != null
                ? playerInteractor
                : GetComponentInParent<PlayerController>()?.GetComponentInChildren<PlayerInteractor>(true);
            animator = animator != null ? animator : GetComponentInChildren<Animator>(true);

            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                handIK = animator.GetComponent<PlayerHandIK>();
                if (handIK == null)
                {
                    handIK = animator.gameObject.AddComponent<PlayerHandIK>();
                }

                handIK.Configure(animator, playerInteractor);
            }
        }

        private void OnEnable()
        {
            if (playerInteractor != null)
            {
                playerInteractor.PickupStarted += PlayGrab;
                playerInteractor.DropStarted += PlayDrop;
                playerInteractor.ThrowStarted += PlayThrow;
                playerInteractor.DoorOpened += PlayOpenDoor;
            }
        }

        private void LateUpdate()
        {
            if (animator == null || playerController == null)
            {
                return;
            }

            UpdateHeldAction();
            int desiredState = playerController.IsLedgeClimbing
                ? ClimbState
                : Time.time < heldActionUntil
                    ? heldActionState
                    : ChooseLocomotionState();
            if (desiredState != currentState)
            {
                animator.CrossFadeInFixedTime(desiredState, transitionDuration);
                currentState = desiredState;
            }

            handIK?.SetSuppressed(desiredState == ClimbState || desiredState == DropState || desiredState == OpenDoorState);
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
            if (!playerController.IsGrounded)
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
            if (state == ClimbState)
            {
                return climbPlaybackSpeed;
            }

            if (state == GrabState)
            {
                return grabPlaybackSpeed;
            }

            if (state == DropState)
            {
                return dropPlaybackSpeed;
            }

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
            if (playerInteractor != null)
            {
                playerInteractor.PickupStarted -= PlayGrab;
                playerInteractor.DropStarted -= PlayDrop;
                playerInteractor.ThrowStarted -= PlayThrow;
                playerInteractor.DoorOpened -= PlayOpenDoor;
            }

            handIK?.SetSuppressed(false);
            if (animator != null)
            {
                animator.speed = 1f;
            }
        }

        private void PlayGrab()
        {
            HoldAction(GrabState, grabHoldDuration);
        }

        private void PlayThrow()
        {
            HoldAction(ThrowState, throwHoldDuration);
        }

        private void PlayDrop()
        {
            HoldAction(DropState, dropHoldDuration);
        }

        private void PlayOpenDoor()
        {
            HoldAction(OpenDoorState, openDoorHoldDuration);
        }

        private void HoldAction(int state, float duration)
        {
            heldActionState = state;
            heldActionUntil = Time.time + duration;
        }
    }
}
