using System.Collections.Generic;
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
        private static readonly int InteractState = Animator.StringToHash("Base Layer.Interact");
        private static readonly int ClimbState = Animator.StringToHash("Base Layer.Climb");

        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerInteractor playerInteractor;
        [SerializeField] private Animator animator;

        [Header("Animation Clips")]
        [SerializeField] private AnimationClip idleAnimation;
        [SerializeField] private AnimationClip walkAnimation;
        [SerializeField] private AnimationClip runAnimation;
        [SerializeField] private AnimationClip crouchIdleAnimation;
        [SerializeField] private AnimationClip crouchWalkAnimation;
        [SerializeField] private AnimationClip slideAnimation;
        [SerializeField] private AnimationClip jumpStartAnimation;
        [SerializeField] private AnimationClip airborneAnimation;
        [SerializeField] private AnimationClip landAnimation;
        [SerializeField] private AnimationClip grabAnimation;
        [SerializeField] private AnimationClip dropAnimation;
        [SerializeField] private AnimationClip throwAnimation;
        [SerializeField] private AnimationClip interactAnimation;
        [SerializeField] private AnimationClip climbAnimation;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float movingThreshold = 0.08f;
        [SerializeField, Min(0f)] private float transitionDuration = 0.12f;
        [SerializeField, Min(0f)] private float jumpStartHoldDuration = 0.14f;
        [SerializeField, Min(0f)] private float landingHoldDuration = 0.18f;
        [SerializeField, Min(0f)] private float grabHoldDuration = 0.22f;
        [SerializeField, Min(0f)] private float dropHoldDuration = 0.22f;
        [SerializeField, Min(0f)] private float throwHoldDuration = 0.32f;
        [SerializeField, Min(0f)] private float interactHoldDuration = 0.22f;
        [SerializeField, Min(0.1f)] private float grabPlaybackSpeed = 2.5f;
        [SerializeField, Min(0.1f)] private float dropPlaybackSpeed = 2.5f;
        [SerializeField, Min(0.1f)] private float interactPlaybackSpeed = 3f;
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
                ApplyAnimationOverrides();

                handIK = animator.GetComponent<PlayerHandIK>();
                if (handIK == null)
                {
                    handIK = animator.gameObject.AddComponent<PlayerHandIK>();
                }

                handIK.Configure(animator, playerInteractor);
            }
        }

        private void ApplyAnimationOverrides()
        {
            RuntimeAnimatorController baseController = animator.runtimeAnimatorController;
            if (baseController == null)
            {
                return;
            }

            AnimatorOverrideController overrideController = new AnimatorOverrideController(baseController);
            List<KeyValuePair<AnimationClip, AnimationClip>> overrides = new();
            AddOverride(baseController, overrides, "Idle_Loop", idleAnimation);
            AddOverride(baseController, overrides, "Walk_Loop", walkAnimation);
            AddOverride(baseController, overrides, "Sprint_Loop", runAnimation);
            AddOverride(baseController, overrides, "Crouch_Idle_Loop", crouchIdleAnimation);
            AddOverride(baseController, overrides, "Crouch_Fwd_Loop", crouchWalkAnimation);
            AddOverride(
                baseController,
                overrides,
                "Dance_Loop",
                slideAnimation != null ? slideAnimation : FindClip(baseController, "Crouch_Fwd_Loop"));
            AddOverride(baseController, overrides, "Jump_Start", jumpStartAnimation);
            AddOverride(baseController, overrides, "Jump_Loop", airborneAnimation);
            AddOverride(baseController, overrides, "Jump_Land", landAnimation);
            AddOverride(baseController, overrides, "PickUp_Table", grabAnimation);
            AddOverride(
                baseController,
                overrides,
                "Hit_Chest",
                dropAnimation != null ? dropAnimation : FindClip(baseController, "PickUp_Table"));
            AddOverride(baseController, overrides, "Spell_Simple_Shoot", throwAnimation);
            AddOverride(baseController, overrides, "Interact", interactAnimation);
            AddOverride(baseController, overrides, "ClimbUp_1m_RM", climbAnimation);
            overrideController.ApplyOverrides(overrides);
            animator.runtimeAnimatorController = overrideController;
        }

        private static void AddOverride(
            RuntimeAnimatorController baseController,
            ICollection<KeyValuePair<AnimationClip, AnimationClip>> overrides,
            string sourceClipName,
            AnimationClip replacement)
        {
            if (replacement == null)
            {
                return;
            }

            AnimationClip sourceClip = FindClip(baseController, sourceClipName);
            if (sourceClip != null)
            {
                overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(sourceClip, replacement));
            }
        }

        private static AnimationClip FindClip(RuntimeAnimatorController controller, string clipName)
        {
            foreach (AnimationClip clip in controller.animationClips)
            {
                if (clip != null && clip.name == clipName)
                {
                    return clip;
                }
            }

            return null;
        }

        private void OnEnable()
        {
            if (playerInteractor != null)
            {
                playerInteractor.PickupStarted += PlayGrab;
                playerInteractor.DropStarted += PlayDrop;
                playerInteractor.ThrowStarted += PlayThrow;
                playerInteractor.InteractionStarted += PlayInteract;
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

            handIK?.SetSuppressed(desiredState == ClimbState || desiredState == DropState || desiredState == InteractState);
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

            if (state == InteractState)
            {
                return interactPlaybackSpeed;
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
                playerInteractor.InteractionStarted -= PlayInteract;
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

        private void PlayInteract()
        {
            HoldAction(InteractState, interactHoldDuration);
        }

        private void HoldAction(int state, float duration)
        {
            heldActionState = state;
            heldActionUntil = Time.time + duration;
        }
    }
}
