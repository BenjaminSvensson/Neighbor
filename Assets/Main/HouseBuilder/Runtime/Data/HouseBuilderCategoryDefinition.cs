using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    [CreateAssetMenu(menuName = "Neighbor/House Builder/Category", fileName = "HouseBuilderCategory")]
    public sealed class HouseBuilderCategoryDefinition : ScriptableObject
    {
        [SerializeField] private string id = "custom";
        [SerializeField] private string displayName = "Custom";
        [SerializeField] private Color color = new(0.3f, 0.8f, 1f, 1f);
        [SerializeField] private Texture2D icon;

        public string Id => id;
        public string DisplayName => displayName;
        public Color Color => color;
        public Texture2D Icon => icon;
    }
}
