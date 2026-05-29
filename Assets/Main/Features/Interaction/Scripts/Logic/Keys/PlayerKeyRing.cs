using System.Collections.Generic;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class PlayerKeyRing : MonoBehaviour
    {
        [SerializeField] private string[] startingKeys;

        private readonly HashSet<string> keys = new();

        private void Awake()
        {
            if (startingKeys == null)
            {
                return;
            }

            foreach (string keyId in startingKeys)
            {
                AddKey(keyId);
            }
        }

        public bool HasKey(string keyId)
        {
            return !string.IsNullOrWhiteSpace(keyId) && keys.Contains(keyId);
        }

        public void AddKey(string keyId)
        {
            if (!string.IsNullOrWhiteSpace(keyId))
            {
                keys.Add(keyId);
            }
        }
    }
}
