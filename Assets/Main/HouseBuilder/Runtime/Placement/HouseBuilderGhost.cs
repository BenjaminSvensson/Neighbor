using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    [DisallowMultipleComponent]
    public sealed class HouseBuilderGhost : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock propertyBlock;
        private Renderer[] renderers;

        public void Initialize()
        {
            gameObject.name = $"Ghost_{gameObject.name}";
            HouseGeometryObject[] geometryObjects = GetComponentsInChildren<HouseGeometryObject>(true);
            for (int i = 0; i < geometryObjects.Length; i++)
            {
                MeshFilter filter = geometryObjects[i].GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh == null)
                {
                    geometryObjects[i].Rebuild();
                }
            }

            renderers = GetComponentsInChildren<Renderer>(true);
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != this)
                {
                    behaviours[i].enabled = false;
                }
            }

            SetValid(true);
        }

        public void SetValid(bool valid)
        {
            renderers ??= GetComponentsInChildren<Renderer>(true);
            propertyBlock ??= new MaterialPropertyBlock();
            Color color = valid ? new Color(0.15f, 1f, 0.45f, 0.45f) : new Color(1f, 0.15f, 0.15f, 0.45f);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, color);
                propertyBlock.SetColor(ColorId, color);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
