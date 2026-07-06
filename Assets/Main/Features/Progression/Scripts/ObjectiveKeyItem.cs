using Neighbor.Main.Features.Interaction;
using UnityEngine;

namespace Neighbor.Main.Features.Progression
{
    [DisallowMultipleComponent]
    public sealed class ObjectiveKeyItem : MonoBehaviour, IPickupLifecycleReceiver
    {
        [SerializeField] private CoreLoopObjectiveTracker tracker;
        [SerializeField] private string keyId;

        public void OnPickupStarted(Pickupable pickupable, PlayerInteractor interactor)
        {
            string resolvedKeyId = ResolveKeyId();
            if (string.IsNullOrWhiteSpace(resolvedKeyId))
            {
                return;
            }

            PlayerKeyRing keyRing = interactor != null ? interactor.GetComponentInParent<PlayerKeyRing>() : null;
            keyRing?.AddKey(resolvedKeyId);
            ResolveTracker()?.RegisterKeyCollected(resolvedKeyId);
        }

        public void OnPickupPlaced(Pickupable pickupable)
        {
        }

        private string ResolveKeyId()
        {
            if (!string.IsNullOrWhiteSpace(keyId))
            {
                return keyId;
            }

            DoorKey key = GetComponent<DoorKey>() ?? GetComponentInChildren<DoorKey>();
            return key != null ? key.KeyId : string.Empty;
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
