using System.Collections.Generic;
using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    [CreateAssetMenu(menuName = "Neighbor/House Builder/Catalog", fileName = "HouseBuilderCatalog")]
    public sealed class HouseBuilderCatalog : ScriptableObject
    {
        [SerializeField] private List<HouseBuilderCategoryDefinition> categories = new();
        [SerializeField] private List<HousePlaceableDefinition> placeables = new();
        [SerializeField] private List<HouseMaterialDefinition> materials = new();

        public IReadOnlyList<HouseBuilderCategoryDefinition> Categories => categories;
        public IReadOnlyList<HousePlaceableDefinition> Placeables => placeables;
        public IReadOnlyList<HouseMaterialDefinition> Materials => materials;

        public bool TryGetPlaceable(string id, out HousePlaceableDefinition definition)
        {
            definition = placeables.Find(candidate => candidate != null && candidate.Id == id);
            return definition != null;
        }

        public bool TryGetMaterial(string id, out Material material)
        {
            HouseMaterialDefinition definition = materials.Find(candidate => candidate != null && candidate.Id == id);
            material = definition != null ? definition.Material : null;
            return material != null;
        }
    }
}
