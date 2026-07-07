using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Neighbor.Main.Features.Environment
{
    [DisallowMultipleComponent]
    public sealed class VegetationMaterialGuard : MonoBehaviour
    {
        private const string GuardName = "Vegetation Material Guard";
        private const int FallbackTextureSize = 32;

        [SerializeField] private bool repairOnAwake = true;
        [SerializeField] private bool includeInactiveRenderers = true;
        [SerializeField] private Color fallbackBarkColor = new(0.34f, 0.22f, 0.13f, 1f);
        [SerializeField] private Color fallbackLeafColor = new(0.28f, 0.48f, 0.18f, 1f);

        private static Texture2D fallbackBarkTexture;
        private static Texture2D fallbackLeafTexture;

        public int LastSceneRendererRepairs { get; private set; }
        public int LastTerrainPrototypeRepairs { get; private set; }
        public int LastTotalRepairs => LastSceneRendererRepairs + LastTerrainPrototypeRepairs;

        private void Awake()
        {
            if (repairOnAwake)
            {
                RepairSceneVegetationMaterials(gameObject.scene);
            }
        }

        public static VegetationMaterialGuard EnsureForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            VegetationMaterialGuard existing = FindInScene<VegetationMaterialGuard>(scene);
            if (existing != null)
            {
                existing.RepairSceneVegetationMaterials(scene);
                return existing;
            }

            GameObject guardObject = new(GuardName);
            SceneManager.MoveGameObjectToScene(guardObject, scene);
            VegetationMaterialGuard guard = guardObject.AddComponent<VegetationMaterialGuard>();
            if (guard.LastTotalRepairs == 0)
            {
                guard.RepairSceneVegetationMaterials(scene);
            }

            return guard;
        }

        public int RepairSceneVegetationMaterials(Scene scene)
        {
            LastSceneRendererRepairs = RepairSceneRenderers(scene);
            LastTerrainPrototypeRepairs = RepairTerrainTreePrototypes(scene);
            return LastTotalRepairs;
        }

        public static bool IsBrokenVegetationMaterial(Material material)
        {
            return material == null
                || material.shader == null
                || !material.shader.isSupported
                || IsPinkFallbackMaterial(material);
        }

        private int RepairSceneRenderers(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return 0;
            }

            int repairCount = 0;
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(
                includeInactiveRenderers ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer.gameObject.scene != scene)
                {
                    continue;
                }

                repairCount += RepairRendererMaterials(renderer, false);
            }

            return repairCount;
        }

        private int RepairTerrainTreePrototypes(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return 0;
            }

            int repairCount = 0;
            Terrain[] terrains = Object.FindObjectsByType<Terrain>(
                includeInactiveRenderers ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null || terrain.gameObject.scene != scene || terrain.terrainData == null)
                {
                    continue;
                }

                TreePrototype[] prototypes = terrain.terrainData.treePrototypes;
                if (prototypes == null)
                {
                    continue;
                }

                for (int prototypeIndex = 0; prototypeIndex < prototypes.Length; prototypeIndex++)
                {
                    GameObject prefab = prototypes[prototypeIndex].prefab;
                    if (prefab == null)
                    {
                        continue;
                    }

                    Renderer[] prototypeRenderers = prefab.GetComponentsInChildren<Renderer>(true);
                    for (int rendererIndex = 0; rendererIndex < prototypeRenderers.Length; rendererIndex++)
                    {
                        repairCount += RepairRendererMaterials(prototypeRenderers[rendererIndex], true);
                    }
                }
            }

            return repairCount;
        }

        private int RepairRendererMaterials(Renderer renderer, bool forceVegetationContext)
        {
            if (renderer == null)
            {
                return 0;
            }

            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                return 0;
            }

            bool changed = false;
            int repairCount = 0;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (!ShouldRepair(renderer, material, forceVegetationContext))
                {
                    continue;
                }

                materials[i] = CreateReplacementMaterial(renderer, material);
                repairCount++;
                changed = true;
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }

            return repairCount;
        }

        private bool ShouldRepair(Renderer renderer, Material material, bool forceVegetationContext)
        {
            bool isVegetation = forceVegetationContext || LooksLikeVegetation(renderer, material);
            if (!isVegetation)
            {
                return false;
            }

            if (IsBrokenVegetationMaterial(material))
            {
                return true;
            }

            return NeedsVisibleTexture(renderer, material);
        }

        private static bool LooksLikeVegetation(Renderer renderer, Material material)
        {
            return ContainsVegetationToken(material != null ? material.name : null)
                || ContainsVegetationToken(renderer != null ? renderer.name : null)
                || ContainsVegetationToken(renderer != null ? renderer.gameObject.name : null)
                || ContainsVegetationToken(GetAncestorNames(renderer != null ? renderer.transform : null));
        }

        private static bool NeedsVisibleTexture(Renderer renderer, Material material)
        {
            if (material == null || GetMainTexture(material) != null)
            {
                return false;
            }

            return ContainsVegetationToken(material.name)
                || ContainsVegetationToken(renderer != null ? renderer.name : null)
                || ContainsVegetationToken(renderer != null ? renderer.gameObject.name : null)
                || ContainsVegetationToken(GetAncestorNames(renderer != null ? renderer.transform : null));
        }

        private Material CreateReplacementMaterial(Renderer renderer, Material original)
        {
            bool isLeaf = IsLeafContext(renderer, original);
            Texture replacementTexture = GetMainTexture(original) ?? GetFallbackTexture(isLeaf);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Unlit/Texture")
                ?? Shader.Find("Unlit/Color");
            Material material = new(shader)
            {
                name = isLeaf ? "Runtime Vegetation Leaf Fallback" : "Runtime Vegetation Bark Fallback",
                hideFlags = HideFlags.DontSave
            };

            Color tint = isLeaf ? fallbackLeafColor : fallbackBarkColor;
            SetColor(material, "_BaseColor", tint);
            SetColor(material, "_Color", tint);
            SetTexture(material, "_BaseMap", replacementTexture);
            SetTexture(material, "_MainTex", replacementTexture);
            SetFloat(material, "_Smoothness", isLeaf ? 0.12f : 0.24f);
            SetFloat(material, "_Metallic", 0f);

            if (isLeaf)
            {
                SetFloat(material, "_AlphaClip", 1f);
                SetFloat(material, "_Cutoff", 0.32f);
                SetFloat(material, "_Cull", 0f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }

            return material;
        }

        private static Texture2D GetFallbackTexture(bool isLeaf)
        {
            if (isLeaf)
            {
                fallbackLeafTexture ??= CreateFallbackTexture(
                    "Runtime Vegetation Leaf Texture",
                    new Color(0.15f, 0.27f, 0.11f, 1f),
                    new Color(0.46f, 0.68f, 0.26f, 1f),
                    true);
                return fallbackLeafTexture;
            }

            fallbackBarkTexture ??= CreateFallbackTexture(
                "Runtime Vegetation Bark Texture",
                new Color(0.16f, 0.1f, 0.055f, 1f),
                new Color(0.48f, 0.31f, 0.17f, 1f),
                false);
            return fallbackBarkTexture;
        }

        private static Texture2D CreateFallbackTexture(string name, Color low, Color high, bool leaf)
        {
            Texture2D texture = new(FallbackTextureSize, FallbackTextureSize, TextureFormat.RGBA32, true)
            {
                name = name,
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            Color[] pixels = new Color[FallbackTextureSize * FallbackTextureSize];
            for (int y = 0; y < FallbackTextureSize; y++)
            {
                for (int x = 0; x < FallbackTextureSize; x++)
                {
                    float horizontal = (float)x / (FallbackTextureSize - 1);
                    float vertical = (float)y / (FallbackTextureSize - 1);
                    float noise = Mathf.PerlinNoise(horizontal * 7.4f + 0.17f, vertical * 9.2f + 0.43f);
                    if (!leaf)
                    {
                        noise = Mathf.Clamp01(noise * 0.55f + Mathf.Abs(Mathf.Sin(horizontal * 24f)) * 0.45f);
                    }

                    pixels[y * FallbackTextureSize + x] = Color.Lerp(low, high, noise);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static Texture GetMainTexture(Material material)
        {
            if (material == null)
            {
                return null;
            }

            if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null)
            {
                return material.GetTexture("_BaseMap");
            }

            if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") != null)
            {
                return material.GetTexture("_MainTex");
            }

            return material.mainTexture;
        }

        private static bool IsPinkFallbackMaterial(Material material)
        {
            if (material == null)
            {
                return true;
            }

            string materialName = material.name;
            if (!string.IsNullOrWhiteSpace(materialName)
                && (materialName.IndexOf("pink", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || materialName.IndexOf("missing", System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }

            return TryGetMaterialColor(material, out Color color)
                && color.r > 0.82f
                && color.g < 0.22f
                && color.b > 0.82f;
        }

        private static bool TryGetMaterialColor(Material material, out Color color)
        {
            color = default;
            if (material == null)
            {
                return false;
            }

            if (material.HasProperty("_BaseColor"))
            {
                color = material.GetColor("_BaseColor");
                return true;
            }

            if (material.HasProperty("_Color"))
            {
                color = material.GetColor("_Color");
                return true;
            }

            return false;
        }

        private static bool IsLeafContext(Renderer renderer, Material material)
        {
            return ContainsLeafToken(material != null ? material.name : null)
                || ContainsLeafToken(renderer != null ? renderer.name : null)
                || ContainsLeafToken(renderer != null ? renderer.gameObject.name : null)
                || ContainsLeafToken(GetAncestorNames(renderer != null ? renderer.transform : null));
        }

        private static string GetAncestorNames(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            string names = string.Empty;
            Transform current = transform.parent;
            for (int i = 0; i < 4 && current != null; i++)
            {
                names += $" {current.name}";
                current = current.parent;
            }

            return names;
        }

        private static bool ContainsVegetationToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.IndexOf("tree", System.StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("leaf", System.StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("leaves", System.StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("bark", System.StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("branch", System.StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("trunk", System.StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("bush", System.StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("grass", System.StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("vegetation", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsLeafToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.IndexOf("leaf", System.StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("leaves", System.StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("foliage", System.StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("bush", System.StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("grass", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void SetColor(Material material, string propertyName, Color value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static void SetTexture(Material material, string propertyName, Texture value)
        {
            if (material != null && value != null && material.HasProperty(propertyName))
            {
                material.SetTexture(propertyName, value);
            }
        }

        private static void SetFloat(Material material, string propertyName, float value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            T[] components = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component != null && component.gameObject.scene == scene)
                {
                    return component;
                }
            }

            return null;
        }
    }
}
