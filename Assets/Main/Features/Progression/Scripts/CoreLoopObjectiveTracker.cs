using System;
using Neighbor.Main.Features.Interaction;
using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Progression
{
    [DisallowMultipleComponent]
    public sealed class CoreLoopObjectiveTracker : MonoBehaviour
    {
        public enum ObjectiveStep
        {
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
        [SerializeField] private string findKeyHint = "Find the key.";
        [SerializeField] private string unlockDoorHint = "Use the key on the locked door.";
        [SerializeField] private string reachRoomHint = "Reach the target room.";
        [SerializeField] private string escapeHint = "Escape the house.";
        [SerializeField] private string completeHint = "Escaped.";

        public event Action<CoreLoopObjectiveTracker> ProgressChanged;

        public ObjectiveStep CurrentStep { get; private set; } = ObjectiveStep.FindKey;
        public string RequiredKeyId => requiredKeyId;
        public bool HasKey { get; private set; }
        public bool HasUnlockedDoor { get; private set; }
        public bool HasReachedRoom { get; private set; }
        public bool HasEscaped { get; private set; }
        public bool IsComplete => CurrentStep == ObjectiveStep.Complete;
        public string CurrentHint => CurrentStep switch
        {
            ObjectiveStep.FindKey => findKeyHint,
            ObjectiveStep.UnlockDoor => unlockDoorHint,
            ObjectiveStep.ReachRoom => reachRoomHint,
            ObjectiveStep.Escape => escapeHint,
            ObjectiveStep.Complete => completeHint,
            _ => string.Empty
        };

        private void Awake()
        {
            if (resetOnAwake)
            {
                ResetProgress();
            }
        }

        public void ResetProgress()
        {
            HasKey = false;
            HasUnlockedDoor = false;
            HasReachedRoom = false;
            HasEscaped = false;
            SetStep(ObjectiveStep.FindKey, true);
        }

        public bool RegisterKeyCollected(string keyId)
        {
            if (!IsRequiredKey(keyId) || IsComplete)
            {
                return false;
            }

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
        }
    }
}
