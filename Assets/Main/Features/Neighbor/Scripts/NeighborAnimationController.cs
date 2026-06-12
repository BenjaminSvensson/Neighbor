using Neighbor.Main.Features.Interaction;
using UnityEngine;

namespace Neighbor.Main.Features.Neighbor
{
    [DisallowMultipleComponent]
    public sealed class NeighborAnimationController : MonoBehaviour
    {
        private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
        private static readonly int TaskState = Animator.StringToHash("Base Layer.Task");
        private static readonly int WalkState = Animator.StringToHash("Base Layer.Walk");
        private static readonly int CautiousState = Animator.StringToHash("Base Layer.Cautious");
        private static readonly int RunState = Animator.StringToHash("Base Layer.Run");
        private static readonly int TraverseState = Animator.StringToHash("Base Layer.Traverse");
        private static readonly int HurtState = Animator.StringToHash("Base Layer.Hurt");
        private static readonly int DoorKickState = Animator.StringToHash("Base Layer.DoorKick");
        private const string DefaultTaskClipName = "Idle_Talking_Loop";
        private const string DefaultHurtClipName = "Hit_Chest";

        private enum ActionAnimation
        {
            None,
            Task,
            CatchPlayer,
            SearchCloset,
            WanderIdle,
            Traversal,
            InvestigationArrival,
            LockedDoor
        }

        [SerializeField] private Animator animator;
        [SerializeField] private NeighborBrain brain;
        [SerializeField] private NeighborMotor motor;
        [SerializeField] private NeighborDoorInteractor doorInteractor;
        [SerializeField] private NeighborImpactReceiver impactReceiver;
        [SerializeField, Min(0f)] private float movingThreshold = 0.08f;
        [SerializeField, Min(0f)] private float runningThreshold = 3.4f;
        [SerializeField, Min(0f)] private float transitionDuration = 0.16f;
        [SerializeField, Min(0.1f)] private float minimumLocomotionPlaybackSpeed = 0.7f;
        [SerializeField, Min(0.1f)] private float maximumLocomotionPlaybackSpeed = 1.4f;

        [Header("Hurt Reaction")]
        [SerializeField, Min(0f)] private float hurtTransitionDuration = 0.045f;
        [SerializeField] private AnimationClip[] hurtAnimations;

        [Header("Action Animations")]
        [SerializeField] private AnimationClip catchPlayerAnimation;
        [SerializeField, Min(0.05f)] private float catchPlayerAnimationSpeed = 1f;
        [SerializeField] private AnimationClip searchClosetAnimation;
        [SerializeField, Min(0.05f)] private float searchClosetAnimationSpeed = 1f;
        [SerializeField] private AnimationClip[] wanderIdleAnimations;
        [SerializeField, Min(0.05f)] private float wanderIdleAnimationSpeed = 1f;
        [SerializeField] private AnimationClip investigationArrivalAnimation;
        [SerializeField, Min(0.05f)] private float investigationArrivalAnimationSpeed = 1f;
        [SerializeField] private AnimationClip lockedDoorReactionAnimation;
        [SerializeField, Min(0.05f)] private float lockedDoorReactionAnimationSpeed = 1f;

        [Header("Traversal Animations")]
        [SerializeField] private AnimationClip traversalStartAnimation;
        [SerializeField] private AnimationClip traversalLoopAnimation;
        [SerializeField] private AnimationClip traversalLandingAnimation;
        [SerializeField] private AnimationClip climbAnimation;
        [SerializeField, Min(0.05f)] private float traversalAnimationSpeed = 1f;

        [Header("Leg IK")]
        [SerializeField] private NeighborFootIK.Settings legIKSettings = new NeighborFootIK.Settings();

        private int currentState;
        private NeighborFootIK footIK;
        private AnimatorOverrideController taskAnimationOverrides;
        private AnimationClip defaultTaskAnimation;
        private AnimationClip defaultHurtAnimation;
        private NeighborTaskLocation activeAnimatedTask;
        private NeighborTaskLocation.TaskAnimationPhase activeTaskAnimationPhase;
        private ActionAnimation activeActionAnimation;
        private AnimationClip activeOverrideAnimation;
        private float activeActionPlaybackSpeed = 1f;
        private bool restartActionAnimation;

        public float CatchAnimationDuration => catchPlayerAnimation != null
            ? catchPlayerAnimation.length / Mathf.Max(0.05f, catchPlayerAnimationSpeed)
            : 0f;

        private void Awake()
        {
            animator = animator != null ? animator : GetComponentInChildren<Animator>(true);
            brain = brain != null ? brain : GetComponent<NeighborBrain>();
            motor = motor != null ? motor : GetComponent<NeighborMotor>();
            doorInteractor = doorInteractor != null ? doorInteractor : GetComponent<NeighborDoorInteractor>();
            impactReceiver = impactReceiver != null ? impactReceiver : GetComponent<NeighborImpactReceiver>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                ConfigureAnimationOverrides();
                legIKSettings ??= new NeighborFootIK.Settings();
                footIK = animator.GetComponent<NeighborFootIK>();
                if (footIK == null)
                {
                    footIK = animator.gameObject.AddComponent<NeighborFootIK>();
                }

                footIK.Configure(animator, transform, motor, legIKSettings);
            }
        }

        private void OnEnable()
        {
            if (impactReceiver != null)
            {
                impactReceiver.ImpactReceived += PlayHurtReaction;
            }

            if (doorInteractor != null)
            {
                doorInteractor.LockedDoorFeedback += RestartActionAnimation;
            }
        }

        private void Update()
        {
            if (animator == null)
            {
                return;
            }

            bool actionAnimationChanged = UpdateActionAnimationOverride();
            int desiredState = ChooseState();
            bool restartTaskAnimation = desiredState == TaskState && (actionAnimationChanged || restartActionAnimation);
            if (desiredState != currentState || restartTaskAnimation)
            {
                animator.CrossFadeInFixedTime(
                    desiredState,
                    transitionDuration,
                    0,
                    restartTaskAnimation ? 0f : float.NegativeInfinity);
                currentState = desiredState;
            }

            restartActionAnimation = false;
            float speed = motor != null ? motor.CurrentSpeed : 0f;
            bool locomotion = desiredState == WalkState || desiredState == CautiousState || desiredState == RunState;
            animator.speed = locomotion
                ? Mathf.Clamp(speed / GetReferenceSpeed(desiredState), minimumLocomotionPlaybackSpeed, maximumLocomotionPlaybackSpeed)
                : desiredState == TaskState
                    ? activeActionPlaybackSpeed
                    : 1f;
        }

        private int ChooseState()
        {
            if (brain != null && brain.CurrentState == NeighborBrain.BehaviorState.Stunned)
            {
                return HurtState;
            }

            if (doorInteractor != null && doorInteractor.IsKickingBlockedDoor)
            {
                return DoorKickState;
            }

            if (motor != null && motor.IsTraversingSpecialMove && activeActionAnimation != ActionAnimation.Traversal)
            {
                return TraverseState;
            }

            if (activeActionAnimation != ActionAnimation.None
                && (activeActionAnimation == ActionAnimation.Task || activeOverrideAnimation != defaultTaskAnimation))
            {
                return TaskState;
            }

            float speed = motor != null ? motor.CurrentSpeed : 0f;
            if (speed >= runningThreshold || brain != null && brain.CurrentState == NeighborBrain.BehaviorState.Chase)
            {
                return RunState;
            }

            if (speed > movingThreshold)
            {
                return brain != null
                    && (brain.CurrentState == NeighborBrain.BehaviorState.Investigate
                        || brain.CurrentState == NeighborBrain.BehaviorState.HuntMode)
                    ? CautiousState
                    : WalkState;
            }

            if (brain != null && brain.CurrentState == NeighborBrain.BehaviorState.Task)
            {
                return TaskState;
            }

            return IdleState;
        }

        private static float GetReferenceSpeed(int state)
        {
            if (state == RunState)
            {
                return 5.8f;
            }

            return state == CautiousState ? 1.65f : 2.4f;
        }

        private void ConfigureAnimationOverrides()
        {
            RuntimeAnimatorController baseController = animator.runtimeAnimatorController;
            if (baseController == null)
            {
                return;
            }

            AnimationClip[] clips = baseController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null && clips[i].name == DefaultTaskClipName)
                {
                    defaultTaskAnimation = clips[i];
                }

                if (clips[i] != null && clips[i].name == DefaultHurtClipName)
                {
                    defaultHurtAnimation = clips[i];
                }
            }

            if (defaultTaskAnimation == null)
            {
                Debug.LogWarning(
                    $"Neighbor task animations could not be configured because '{DefaultTaskClipName}' was not found.",
                    this);
                return;
            }

            taskAnimationOverrides = new AnimatorOverrideController(baseController);
            taskAnimationOverrides.name = $"{baseController.name} (Task Overrides)";
            animator.runtimeAnimatorController = taskAnimationOverrides;
        }

        private bool UpdateActionAnimationOverride()
        {
            NeighborTaskLocation task = brain != null ? brain.ActiveTaskLocation : null;
            NeighborTaskLocation.TaskAnimationPhase phase = brain != null
                ? brain.ActiveTaskAnimationPhase
                : NeighborTaskLocation.TaskAnimationPhase.None;

            ActionAnimation desiredAction = ActionAnimation.None;
            AnimationClip desiredAnimation = defaultTaskAnimation;
            float desiredPlaybackSpeed = 1f;
            if (brain != null && brain.IsCatchingPlayer)
            {
                desiredAction = ActionAnimation.CatchPlayer;
                desiredAnimation = catchPlayerAnimation != null ? catchPlayerAnimation : defaultTaskAnimation;
                desiredPlaybackSpeed = catchPlayerAnimationSpeed;
            }
            else if (brain != null && brain.IsSearchingHideSpot)
            {
                desiredAction = ActionAnimation.SearchCloset;
                desiredAnimation = searchClosetAnimation != null ? searchClosetAnimation : defaultTaskAnimation;
                desiredPlaybackSpeed = searchClosetAnimationSpeed;
            }
            else if (brain != null && brain.IsWaitingDuringWander)
            {
                desiredAction = ActionAnimation.WanderIdle;
                desiredAnimation = activeActionAnimation == ActionAnimation.WanderIdle
                    ? activeOverrideAnimation
                    : GetRandomWanderIdleAnimation();
                desiredAnimation = desiredAnimation != null ? desiredAnimation : defaultTaskAnimation;
                desiredPlaybackSpeed = wanderIdleAnimationSpeed;
            }
            else if (motor != null && motor.IsTraversingSpecialMove)
            {
                AnimationClip traversalAnimation = GetTraversalAnimation(motor.CurrentTraversalAnimationPhase);
                if (traversalAnimation != null)
                {
                    desiredAction = ActionAnimation.Traversal;
                    desiredAnimation = traversalAnimation;
                    desiredPlaybackSpeed = traversalAnimationSpeed;
                }
            }
            else if (doorInteractor != null && doorInteractor.IsReactingToLockedDoor && lockedDoorReactionAnimation != null)
            {
                desiredAction = ActionAnimation.LockedDoor;
                desiredAnimation = lockedDoorReactionAnimation;
                desiredPlaybackSpeed = lockedDoorReactionAnimationSpeed;
            }
            else if (brain != null && brain.IsAtInvestigationGoal && investigationArrivalAnimation != null)
            {
                desiredAction = ActionAnimation.InvestigationArrival;
                desiredAnimation = investigationArrivalAnimation;
                desiredPlaybackSpeed = investigationArrivalAnimationSpeed;
            }
            else if (task != null)
            {
                desiredAction = ActionAnimation.Task;
                AnimationClip taskAnimation = task.GetAnimation(phase);
                desiredAnimation = taskAnimation != null ? taskAnimation : defaultTaskAnimation;
                desiredPlaybackSpeed = task.GetAnimationPlaybackSpeed(phase);
            }

            if (desiredAction == activeActionAnimation
                && desiredAnimation == activeOverrideAnimation
                && task == activeAnimatedTask
                && phase == activeTaskAnimationPhase)
            {
                return false;
            }

            activeActionAnimation = desiredAction;
            activeOverrideAnimation = desiredAnimation;
            activeActionPlaybackSpeed = Mathf.Max(0.05f, desiredPlaybackSpeed);
            activeAnimatedTask = task;
            activeTaskAnimationPhase = phase;
            if (taskAnimationOverrides == null || defaultTaskAnimation == null)
            {
                return false;
            }

            taskAnimationOverrides[defaultTaskAnimation] = desiredAnimation != null
                ? desiredAnimation
                : defaultTaskAnimation;
            return true;
        }

        private AnimationClip GetRandomWanderIdleAnimation()
        {
            if (wanderIdleAnimations == null || wanderIdleAnimations.Length == 0)
            {
                return null;
            }

            int startIndex = Random.Range(0, wanderIdleAnimations.Length);
            for (int offset = 0; offset < wanderIdleAnimations.Length; offset++)
            {
                AnimationClip clip = wanderIdleAnimations[(startIndex + offset) % wanderIdleAnimations.Length];
                if (clip != null)
                {
                    return clip;
                }
            }

            return null;
        }

        private AnimationClip GetTraversalAnimation(NeighborMotor.TraversalAnimationPhase phase)
        {
            return phase switch
            {
                NeighborMotor.TraversalAnimationPhase.Start => traversalStartAnimation,
                NeighborMotor.TraversalAnimationPhase.Loop => traversalLoopAnimation,
                NeighborMotor.TraversalAnimationPhase.Landing => traversalLandingAnimation,
                NeighborMotor.TraversalAnimationPhase.Climb => climbAnimation,
                _ => null
            };
        }

        private void PlayHurtReaction()
        {
            if (animator == null)
            {
                return;
            }

            AnimationClip hurtAnimation = GetRandomAnimation(hurtAnimations);
            if (taskAnimationOverrides != null && defaultHurtAnimation != null)
            {
                taskAnimationOverrides[defaultHurtAnimation] = hurtAnimation != null
                    ? hurtAnimation
                    : defaultHurtAnimation;
            }

            animator.CrossFadeInFixedTime(HurtState, hurtTransitionDuration, 0, 0f);
            currentState = HurtState;
        }

        private void RestartActionAnimation()
        {
            restartActionAnimation = true;
        }

        private static AnimationClip GetRandomAnimation(AnimationClip[] animations)
        {
            if (animations == null || animations.Length == 0)
            {
                return null;
            }

            int startIndex = Random.Range(0, animations.Length);
            for (int offset = 0; offset < animations.Length; offset++)
            {
                AnimationClip animation = animations[(startIndex + offset) % animations.Length];
                if (animation != null)
                {
                    return animation;
                }
            }

            return null;
        }

        private void OnDisable()
        {
            if (impactReceiver != null)
            {
                impactReceiver.ImpactReceived -= PlayHurtReaction;
            }

            if (doorInteractor != null)
            {
                doorInteractor.LockedDoorFeedback -= RestartActionAnimation;
            }

            if (animator != null)
            {
                animator.speed = 1f;
            }
        }

        private void OnDestroy()
        {
            if (taskAnimationOverrides != null)
            {
                Destroy(taskAnimationOverrides);
            }
        }
    }
}
