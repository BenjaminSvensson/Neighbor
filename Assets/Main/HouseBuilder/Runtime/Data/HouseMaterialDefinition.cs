using System;
using System.Collections.Generic;
using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    [CreateAssetMenu(menuName = "Neighbor/House Builder/Material", fileName = "HouseMaterial")]
    public sealed class HouseMaterialDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName = "Material";
        [SerializeField] private Material material;
        [SerializeField] private Texture2D preview;
        [SerializeField] private List<string> tags = new();

        public string Id => id;
        public string DisplayName => displayName;
        public Material Material => material;
        public Texture2D Preview => preview;
        public IReadOnlyList<string> Tags => tags;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                id = Guid.NewGuid().ToString("N");
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = name;
            }
        }
    }
}
