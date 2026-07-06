using Neighbor.Main.Features.Interaction;
using Neighbor.Main.Features.Neighbor;
using Neighbor.Main.Features.Progression;
using UnityEngine;

namespace Neighbor.Main.Features.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerOnboardingDirector : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField, Min(5f)] private float onboardingDuration = 60f;
        [SerializeField, Min(0f)] private float initialPromptDelay = 2.5f;
        [SerializeField, Min(0.25f)] private float promptCooldown = 5.5f;
        [SerializeField, Min(0.25f)] private float targetRefreshInterval = 0.5f;

        [Header("Prompts")]
        [SerializeField] private string fallbackObjectivePrompt = "Find a way inside.";
        [SerializeField] private string movementPrompt = "Move quietly. Run only when you must.";
        [SerializeField] private string interactPrompt = "Look closely. Useful things respond.";
        [SerializeField] private string heldItemPrompt = "Place, throw, or inspect what you carry.";
        [SerializeField] private string hidingPrompt = "Stay still. Breathe slow.";
        [SerializeField] private string chasePrompt = "Break sight. Hide before he reaches you.";

        private PlayerController player;
        private PlayerInteractor interactor;
        private PlayerHidingState hidingState;
        private NeighborBrain neighbor;
        private CoreLoopObjectiveTracker objectiveTracker;
        private CoreLoopObjectiveTracker.ObjectiveStep lastObjectiveStep;
        private Vector3 startPosition;
        private float startTime;
        private float nextPromptTime;
        private float nextTargetRefreshTime;
        private bool initialPromptShown;
        private bool movementPromptShown;
        private bool interactPromptShown;
        private bool heldItemPromptShown;
        private bool hidingPromptShown;
        private bool chasePromptShown;

        public string LastPrompt { get; private set; }
        public bool IsOnboardingActive => Time.time - startTime <= onboardingDuration;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            interactor = GetComponentInChildren<PlayerInteractor>(true);
            hidingState = GetComponent<PlayerHidingState>() ?? GetComponentInChildren<PlayerHidingState>(true);
            startPosition = transform.position;
            startTime = Time.time;
            nextPromptTime = Time.time + initialPromptDelay;
            ResolveTargets(true);
        }

        private void OnEnable()
        {
            SubscribeObjective();
        }

        private void OnDisable()
        {
            UnsubscribeObjective();
        }

        private void Update()
        {
            if (!IsOnboardingActive || InteractionOverlayState.IsGameplayInputBlocked)
            {
                return;
            }

            ResolveTargets(false);
            TryShowInitialPrompt();
            TryShowMovementPrompt();
            TryShowContextPrompt();
        }

        private void TryShowInitialPrompt()
        {
            if (initialPromptShown || Time.time - startTime < initialPromptDelay)
            {
                return;
            }

            initialPromptShown = TryEmitPrompt(GetObjectivePrompt(), 0.35f, true);
        }

        private void TryShowMovementPrompt()
        {
            if (movementPromptShown || Time.time - startTime < 8f)
            {
                return;
            }

            Vector3 planarOffset = transform.position - startPosition;
            planarOffset.y = 0f;
            if (planarOffset.sqrMagnitude > 0.8f * 0.8f)
            {
                movementPromptShown = true;
                return;
            }

            movementPromptShown = TryEmitPrompt(movementPrompt, 0.25f);
        }

        private void TryShowContextPrompt()
        {
            if (neighbor != null && neighbor.CurrentState == NeighborBrain.BehaviorState.Chase)
            {
                chasePromptShown = chasePromptShown || TryEmitPrompt(chasePrompt, 0.85f);
                return;
            }

            if (hidingState != null && hidingState.IsHidden)
            {
                hidingPromptShown = hidingPromptShown || TryEmitPrompt(hidingPrompt, 0.55f);
                return;
            }

            if (interactor != null && interactor.IsHoldingPickup)
            {
                heldItemPromptShown = heldItemPromptShown || TryEmitPrompt(BuildHeldItemPrompt(), 0.35f);
                return;
            }

            if (interactor != null && interactor.HasFocusedInteractable)
            {
                interactPromptShown = interactPromptShown || TryEmitPrompt(BuildInteractPrompt(), 0.3f);
            }
        }

        private void HandleObjectiveProgressChanged(CoreLoopObjectiveTracker tracker)
        {
            if (tracker == null || tracker.CurrentStep == lastObjectiveStep)
            {
                return;
            }

            lastObjectiveStep = tracker.CurrentStep;
            TryEmitPrompt(tracker.CurrentHint, 0.45f, true);
        }

        private string GetObjectivePrompt()
        {
            return objectiveTracker != null && !string.IsNullOrWhiteSpace(objectiveTracker.CurrentHint)
                ? objectiveTracker.CurrentHint
                : fallbackObjectivePrompt;
        }

        private string BuildInteractPrompt()
        {
            return $"{PlayerInputBindings.GetControlLabel(PlayerInputBindingAction.Interact)}: {interactPrompt}";
        }

        private string BuildHeldItemPrompt()
        {
            string place = PlayerInputBindings.GetControlLabel(PlayerInputBindingAction.SecondaryUse);
            string inspect = PlayerInputBindings.GetControlLabel(PlayerInputBindingAction.InspectHeld);
            string prompt = string.IsNullOrWhiteSpace(heldItemPrompt)
                ? "Carry options."
                : heldItemPrompt.Trim();
            return $"{prompt} {place}: place/throw. Hold {inspect}: inspect.";
        }

        private bool TryEmitPrompt(string message, float intensity, bool ignoreCooldown = false)
        {
            if (string.IsNullOrWhiteSpace(message) || !ignoreCooldown && Time.time < nextPromptTime)
            {
                return false;
            }

            LastPrompt = message.Trim();
            PlayerFeedbackEvents.ReportOnboardingPrompt(LastPrompt, intensity);
            nextPromptTime = Time.time + promptCooldown;
            return true;
        }

        private void ResolveTargets(bool force)
        {
            if (!force && Time.time < nextTargetRefreshTime)
            {
                return;
            }

            if (player == null)
            {
                player = GetComponent<PlayerController>();
            }

            if (interactor == null)
            {
                interactor = GetComponentInChildren<PlayerInteractor>(true);
            }

            if (hidingState == null)
            {
                hidingState = GetComponent<PlayerHidingState>() ?? GetComponentInChildren<PlayerHidingState>(true);
            }

            if (neighbor == null)
            {
                neighbor = FindAnyObjectByType<NeighborBrain>();
            }

            CoreLoopObjectiveTracker foundObjective = objectiveTracker != null
                ? objectiveTracker
                : FindAnyObjectByType<CoreLoopObjectiveTracker>();
            if (foundObjective != objectiveTracker)
            {
                UnsubscribeObjective();
                objectiveTracker = foundObjective;
                SubscribeObjective();
            }

            nextTargetRefreshTime = Time.time + targetRefreshInterval;
        }

        private void SubscribeObjective()
        {
            if (objectiveTracker == null)
            {
                return;
            }

            objectiveTracker.ProgressChanged -= HandleObjectiveProgressChanged;
            objectiveTracker.ProgressChanged += HandleObjectiveProgressChanged;
            lastObjectiveStep = objectiveTracker.CurrentStep;
        }

        private void UnsubscribeObjective()
        {
            if (objectiveTracker != null)
            {
                objectiveTracker.ProgressChanged -= HandleObjectiveProgressChanged;
            }
        }
    }
}
