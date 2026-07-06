using Neighbor.Main.Features.Interaction;
using UnityEngine;

namespace Neighbor.Main.Features.Progression
{
    [DisallowMultipleComponent]
    public sealed class ObjectiveDoorUnlockWatcher : MonoBehaviour
    {
        [SerializeField] private CoreLoopObjectiveTracker tracker;
        [SerializeField] private Door door;

        private void Awake()
        {
            ResolveDoor();
            ResolveTracker();
        }

        private void OnEnable()
        {
            Door.Unlocked += HandleDoorUnlocked;
        }

        private void OnDisable()
        {
            Door.Unlocked -= HandleDoorUnlocked;
        }

        private void HandleDoorUnlocked(Door unlockedDoor)
        {
            Door targetDoor = ResolveDoor();
            if (targetDoor != null && unlockedDoor == targetDoor)
            {
                ResolveTracker()?.RegisterDoorUnlocked(unlockedDoor);
            }
        }

        private Door ResolveDoor()
        {
            if (door == null)
            {
                door = GetComponent<Door>() ?? GetComponentInParent<Door>();
            }

            return door;
        }

        private CoreLoopObjectiveTracker ResolveTracker()
        {
            if (tracker == null)
            {
                tracker = FindAnyObjectByType<CoreLoopObjectiveTracker>();
            }

            return tracker;
        }
    }
}
