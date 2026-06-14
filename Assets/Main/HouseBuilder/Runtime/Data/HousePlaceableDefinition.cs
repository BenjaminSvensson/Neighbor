using System;
using System.Collections.Generic;
using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    [CreateAssetMenu(menuName = "Neighbor/House Builder/Placeable", fileName = "HousePlaceable")]
    public sealed class HousePlaceableDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName = "Placeable";
        [SerializeField] private string categoryId = HouseBuilderCategories.Prop;
        [SerializeField] private GameObject prefab;
        [SerializeField] private Texture2D preview;
        [SerializeField] private HousePlacementProfile placement = new();
        [SerializeField] private HouseWallOpeningProfile wallOpening = new();
        [SerializeField] private List<HouseWirePortTemplate> wirePorts = new();
        [SerializeField] private List<string> tags = new();

        public string Id => id;
        public string DisplayName => displayName;
        public string CategoryId => categoryId;
        public GameObject Prefab => prefab;
        public Texture2D Preview => preview;
        public HousePlacementProfile Placement => placement;
        public HouseWallOpeningProfile WallOpening => wallOpening;
        public IReadOnlyList<HouseWirePortTemplate> WirePorts => wirePorts;
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

            if (string.IsNullOrWhiteSpace(categoryId))
            {
                categoryId = HouseBuilderCategories.Prop;
            }
        }
    }
}
