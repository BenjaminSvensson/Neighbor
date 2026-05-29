using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    /// <summary>
    /// Key identity component placed on pickupable key objects.
    /// </summary>
    public sealed class DoorKey : MonoBehaviour
    {
        [SerializeField] private string keyId = "test_key";

        public string KeyId => keyId;

        public bool Opens(Door door)
        {
            return door != null && !string.IsNullOrWhiteSpace(keyId) && keyId == door.RequiredKeyId;
        }
    }
}
