using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class PaintSplat : MonoBehaviour
    {
        private static Material sharedPaintMaterial;
        private Material ownedMaterial;
        private float destroyTime;

        private void Update()
        {
            if (Time.time >= destroyTime)
            {
                Destroy(gameObject);
            }
        }

        public void Initialize(float lifetime, Material material)
        {
            destroyTime = Time.time + Mathf.Max(0.01f, lifetime);
            ownedMaterial = material;
        }

        public static Material CreateMaterial(Color color)
        {
            if (sharedPaintMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
                sharedPaintMaterial = new Material(shader)
                {
                    name = "GeneratedPlaceholderPaintSplat",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            Material material = new Material(sharedPaintMaterial)
            {
                color = color,
                hideFlags = HideFlags.HideAndDontSave
            };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetColor("_EmissionColor", color * 0.15f);
            return material;
        }

        private void OnDestroy()
        {
            if (ownedMaterial != null)
            {
                Destroy(ownedMaterial);
                ownedMaterial = null;
            }
        }
    }
}
