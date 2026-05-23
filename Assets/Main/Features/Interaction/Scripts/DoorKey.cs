using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
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
