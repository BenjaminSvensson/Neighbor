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
        private static readonly int StunnedState = Animator.StringToHash("Base Layer.Stunned");
        private static readonly int DoorKickState = Animator.StringToHash("Base Layer.DoorKick");

        [SerializeField] private Animator animator;
        [SerializeField] private NeighborBrain brain;
        [SerializeField] private NeighborMotor motor;
        [SerializeField] private NeighborDoorInteractor doorInteractor;
        [SerializeField, Min(0f)] private float movingThreshold = 0.08f;
        [SerializeField, Min(0f)] private float runningThreshold = 3.4f;
        [SerializeField, Min(0f)] private float transitionDuration = 0.16f;
        [SerializeField, Min(0.1f)] private float minimumLocomotionPlaybackSpeed = 0.7f;
        [SerializeField, Min(0.1f)] private float maximumLocomotionPlaybackSpeed = 1.4f;

        private int currentState;

        private void Awake()
        {
            animator = animator != null ? animator : GetComponentInChildren<Animator>(true);
            brain = brain != null ? brain : GetComponent<NeighborBrain>();
            motor = motor != null ? motor : GetComponent<NeighborMotor>();
            doorInteractor = doorInteractor != null ? doorInteractor : GetComponent<NeighborDoorInteractor>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }
        }

        private void Update()
        {
            if (animator == null)
            {
                return;
            }

            int desiredState = ChooseState();
            if (desiredState != currentState)
            {
                animator.CrossFadeInFixedTime(desiredState, transitionDuration);
                currentState = desiredState;
            }

            float speed = motor != null ? motor.CurrentSpeed : 0f;
            bool locomotion = desiredState == WalkState || desiredState == CautiousState || desiredState == RunState;
            animator.speed = locomotion
                ? Mathf.Clamp(speed / GetReferenceSpeed(desiredState), minimumLocomotionPlaybackSpeed, maximumLocomotionPlaybackSpeed)
                : 1f;
        }

        private int ChooseState()
        {
            if (brain != null && brain.CurrentState == NeighborBrain.BehaviorState.Stunned)
            {
                return StunnedState;
            }

            if (doorInteractor != null && doorInteractor.IsKickingBlockedDoor)
            {
                return DoorKickState;
            }

            if (motor != null && motor.IsTraversingSpecialMove)
            {
                return TraverseState;
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

        private void OnDisable()
        {
            if (animator != null)
            {
                animator.speed = 1f;
            }
        }
    }
}
