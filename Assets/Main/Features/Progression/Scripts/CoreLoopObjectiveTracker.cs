using System;
using Neighbor.Main.Features.Interaction;
using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Progression
{
    [DisallowMultipleComponent]
    public sealed class CoreLoopObjectiveTracker : MonoBehaviour
    {
        public const int TotalObjectiveSteps = 5;

        public enum ObjectiveStep
        {
            GetInside,
            FindKey,
            UnlockDoor,
            ReachRoom,
            Escape,
            Complete
        }

        [Header("Objective")]
        [SerializeField] private string requiredKeyId = "test_key";
        [SerializeField] private bool resetOnAwake = true;

        [Header("Readable Hints")]
        [SerializeField] private string getInsideHint = "Get inside the house.";
        [SerializeField] private string findKeyHint = "Find the key.";
        [SerializeField] private string unlockDoorHint = "Use the key on the locked door.";
        [SerializeField] private string reachRoomHint = "Reach the target room.";
        [SerializeField] private string escapeHint = "Escape the house.";
        [SerializeField] private string completeHint = "Escaped.";

        [Header("Progress Feedback")]
        [SerializeField] private string enteredHouseFeedback = "Inside. Find the key.";
        [SerializeField] private string keyCollectedFeedback = "Key found. Unlock the door.";
        [SerializeField] private string doorUnlockedFeedback = "Door unlocked. Reach the room.";
        [SerializeField] private string roomReachedFeedback = "Room reached. Get out.";
        [SerializeField] private string escapedFeedback = "Escaped.";

        public event Action<CoreLoopObjectiveTracker> ProgressChanged;

        public ObjectiveStep CurrentStep { get; private set; } = ObjectiveStep.GetInside;
        public string RequiredKeyId => requiredKeyId;
        public int CurrentStepIndex => GetStepIndex(CurrentStep);
        public bool HasEnteredHouse { get; private set; }
        public bool HasKey { get; private set; }
        public bool HasUnlockedDoor { get; private set; }
        public bool HasReachedRoom { get; private set; }
        public bool HasEscaped { get; private set; }
        public bool IsComplete => CurrentStep == ObjectiveStep.Complete;
        public string CurrentHint => CurrentStep switch
        {
            ObjectiveStep.GetInside => getInsideHint,
            ObjectiveStep.FindKey => findKeyHint,
            ObjectiveStep.UnlockDoor => unlockDoorHint,
            ObjectiveStep.ReachRoom => reachRoomHint,
            ObjectiveStep.Escape => escapeHint,
            ObjectiveStep.Complete => completeHint,
            _ => string.Empty
        };
        public string CurrentProgressMessage => GetProgressMessage(CurrentStep);

        private void Awake()
        {
            if (resetOnAwake)
            {
                ResetProgress();
            }
        }

        public void ResetProgress()
        {
            HasEnteredHouse = false;
            HasKey = false;
            HasUnlockedDoor = false;
            HasReachedRoom = false;
            HasEscaped = false;
            SetStep(ObjectiveStep.GetInside, true);
        }

        public bool RegisterEnteredHouse(PlayerController player)
        {
            if (player == null || IsComplete)
            {
                return false;
            }

            HasEnteredHouse = true;
            AdvanceTo(ObjectiveStep.FindKey);
            return true;
        }

        public bool RegisterKeyCollected(string keyId)
        {
            if (!IsRequiredKey(keyId) || IsComplete)
            {
                return false;
            }

            HasEnteredHouse = true;
            AdvanceTo(ObjectiveStep.FindKey);
            HasKey = true;
            AdvanceTo(ObjectiveStep.UnlockDoor);
            return true;
        }

        public bool RegisterDoorUnlocked(Door door)
        {
            if (door == null || !HasKey || door.IsLocked || IsComplete)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(requiredKeyId) && door.RequiredKeyId != requiredKeyId)
            {
                return false;
            }

            HasUnlockedDoor = true;
            AdvanceTo(ObjectiveStep.ReachRoom);
            return true;
        }

        public bool RegisterRoomReached(PlayerController player)
        {
            if (player == null || !HasUnlockedDoor || IsComplete)
            {
                return false;
            }

            HasReachedRoom = true;
            AdvanceTo(ObjectiveStep.Escape);
            return true;
        }

        public bool RegisterEscaped(PlayerController player)
        {
            if (player == null || !HasReachedRoom || IsComplete)
            {
                return false;
            }

            HasEscaped = true;
            AdvanceTo(ObjectiveStep.Complete);
            return true;
        }

        private bool IsRequiredKey(string keyId)
        {
            if (string.IsNullOrWhiteSpace(requiredKeyId))
            {
                return !string.IsNullOrWhiteSpace(keyId);
            }

            return keyId == requiredKeyId;
        }

        private void AdvanceTo(ObjectiveStep step)
        {
            if (step <= CurrentStep)
            {
                return;
            }

            SetStep(step, false);
        }

        private void SetStep(ObjectiveStep step, bool force)
        {
            if (!force && CurrentStep == step)
            {
                return;
            }

            CurrentStep = step;
            ProgressChanged?.Invoke(this);
            if (!force)
            {
                PlayerFeedbackEvents.ReportObjectiveProgress(
                    CurrentStepIndex,
                    TotalObjectiveSteps,
                    CurrentProgressMessage,
                    CurrentHint,
                    IsComplete);
            }
        }

        public static int GetStepIndex(ObjectiveStep step)
        {
            return step switch
            {
                ObjectiveStep.GetInside => 1,
                ObjectiveStep.FindKey => 2,
                ObjectiveStep.UnlockDoor => 3,
                ObjectiveStep.ReachRoom => 4,
                ObjectiveStep.Escape => 5,
                _ => TotalObjectiveSteps
            };
        }

        private string GetProgressMessage(ObjectiveStep step)
        {
            return step switch
            {
                ObjectiveStep.FindKey => enteredHouseFeedback,
                ObjectiveStep.UnlockDoor => keyCollectedFeedback,
                ObjectiveStep.ReachRoom => doorUnlockedFeedback,
                ObjectiveStep.Escape => roomReachedFeedback,
                ObjectiveStep.Complete => escapedFeedback,
                _ => CurrentHint
            };
        }
    }
}
