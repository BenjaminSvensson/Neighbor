using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Progression
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class ObjectiveTriggerZone : MonoBehaviour
    {
        public enum TriggerRole
        {
            EnterHouse,
            ReachRoom,
            Escape
        }

        [SerializeField] private CoreLoopObjectiveTracker tracker;
        [SerializeField] private TriggerRole role;

        public TriggerRole Role
        {
            get => role;
            set => role = value;
        }

        private void Awake()
        {
            ConfigureCollider();
            ResolveTracker();
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerController player = other != null ? other.GetComponentInParent<PlayerController>() : null;
            NotifyPlayerEntered(player);
        }

        public bool NotifyPlayerEntered(PlayerController player)
        {
            CoreLoopObjectiveTracker objectiveTracker = ResolveTracker();
            if (objectiveTracker == null || player == null)
            {
                return false;
            }

            return role switch
            {
                TriggerRole.EnterHouse => objectiveTracker.RegisterEnteredHouse(player),
                TriggerRole.Escape => objectiveTracker.RegisterEscaped(player),
                _ => objectiveTracker.RegisterRoomReached(player)
            };
        }

        private void ConfigureCollider()
        {
            Collider triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
        }

        private CoreLoopObjectiveTracker ResolveTracker()
        {
            if (tracker == null)
            {
                tracker = FindAnyObjectByType<CoreLoopObjectiveTracker>();
            }

            return tracker;
        }

        private void OnValidate()
        {
            ConfigureCollider();
        }
    }
}
