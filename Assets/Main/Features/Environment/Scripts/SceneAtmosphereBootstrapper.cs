using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Neighbor.Main.Features.Environment
{
    public static class SceneAtmosphereBootstrapper
    {
        private const string DirectorName = "SceneAtmosphere";
        private const string MoonName = "Runtime Atmosphere Moon";
        private const string VolumeName = "Runtime Atmosphere Color Grade Volume";
        private const string FlickerName = "Flicker Practical Light";
        private const int DressingTextureSize = 32;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeLoadedScenes()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                EnsureAtmosphereForScene(SceneManager.GetSceneAt(i));
            }
        }

        public static SceneAtmosphereDirector EnsureAtmosphereForScene(Scene scene, bool force = false)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            if (!force && !ShouldAutoBootstrapScene(scene))
            {
                return null;
            }

            VegetationMaterialGuard.EnsureForScene(scene);

            SceneAtmosphereDirector existingDirector = FindInScene<SceneAtmosphereDirector>(scene);
            if (existingDirector != null)
            {
                existingDirector.ApplyAtmosphere();
                return existingDirector;
            }

            Vector3 groundCenter = FindSceneGroundCenter(scene);
            Light sun = FindSunLight(scene) ?? CreateSunLight(scene, groundCenter);
            Light moon = FindNamedInScene<Light>(scene, MoonName) ?? CreateMoonLight(scene, groundCenter);
            Volume volume = FindNamedInScene<Volume>(scene, VolumeName) ?? CreateColorGradeVolume(scene);
            AtmosphereFlickerLight flicker = FindNamedInScene<AtmosphereFlickerLight>(scene, FlickerName)
                ?? CreateFlickerLight(scene, groundCenter);
            AtmosphereDressingAnchor[] anchors = EnsureDressing(scene, groundCenter);

            GameObject directorObject = CreateSceneObject(scene, DirectorName);
            SceneAtmosphereDirector director = directorObject.AddComponent<SceneAtmosphereDirector>();
            director.Configure(
                sun,
                moon,
                volume,
                new[] { flicker },
                anchors);
            return director;
        }

        public static bool ShouldAutoBootstrapScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                return false;
            }

            string path = scene.path.Replace('\\', '/');
            return path.StartsWith("Assets/Main/Scenes/Main/")
                || path.StartsWith("Assets/Main/Scenes/Testing/");
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureAtmosphereForScene(scene);
        }

        private static Light FindSunLight(Scene scene)
        {
            Light[] lights = FindAllInScene<Light>(scene);
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light != null && light.type == LightType.Directional && light.name != MoonName)
                {
                    return light;
                }
            }

            return null;
        }

        private static Light CreateSunLight(Scene scene, Vector3 groundCenter)
        {
            GameObject lightObject = CreateSceneObject(scene, "Runtime Atmosphere Sun");
            lightObject.transform.SetPositionAndRotation(
                groundCenter + new Vector3(0f, 8f, -3f),
                Quaternion.Euler(48f, -32f, 0f));
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.88f, 0.68f, 1f);
            light.intensity = 0.82f;
            light.shadows = LightShadows.Soft;
            return light;
        }

        private static Light CreateMoonLight(Scene scene, Vector3 groundCenter)
        {
            GameObject lightObject = CreateSceneObject(scene, MoonName);
            lightObject.transform.SetPositionAndRotation(
                groundCenter + new Vector3(0f, 9f, 3f),
                Quaternion.Euler(132f, 38f, 0f));
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.56f, 0.66f, 1f, 1f);
            light.intensity = 0.22f;
            light.shadows = LightShadows.Soft;
            return light;
        }

        private static Volume CreateColorGradeVolume(Scene scene)
        {
            GameObject volumeObject = CreateSceneObject(scene, VolumeName);
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 20f;
            volume.weight = 1f;
            return volume;
        }

        private static AtmosphereFlickerLight CreateFlickerLight(Scene scene, Vector3 groundCenter)
        {
            GameObject lightObject = CreateSceneObject(scene, FlickerName);
            lightObject.transform.SetPositionAndRotation(
                groundCenter + new Vector3(1.8f, 2.4f, 1.2f),
                Quaternion.identity);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.78f, 0.45f, 1f);
            light.range = 7.5f;
            light.intensity = 0.95f;
            light.shadows = LightShadows.Soft;

            AtmosphereFlickerLight flicker = lightObject.AddComponent<AtmosphereFlickerLight>();
            flicker.Configure(0.95f, 0.28f, 8.5f);
            return flicker;
        }

        private static AtmosphereDressingAnchor[] EnsureDressing(Scene scene, Vector3 groundCenter)
        {
            return new[]
            {
                EnsureDressingAnchor(
                    scene,
                    "Runtime Dirty Decal - Entry Scuffs",
                    AtmosphereDressingAnchor.DressingKind.DirtyDecal,
                    PrimitiveType.Quad,
                    groundCenter + new Vector3(-1.6f, 0.015f, 1.2f),
                    Quaternion.Euler(90f, 22f, 0f),
                    new Vector3(1.4f, 0.55f, 1f),
                    new Color(0.08f, 0.07f, 0.055f, 0.62f)),
                EnsureDressingAnchor(
                    scene,
                    "Runtime Dirty Decal - Basement Drag",
                    AtmosphereDressingAnchor.DressingKind.DirtyDecal,
                    PrimitiveType.Quad,
                    groundCenter + new Vector3(1.45f, 0.016f, -1.35f),
                    Quaternion.Euler(90f, -18f, 0f),
                    new Vector3(1.7f, 0.42f, 1f),
                    new Color(0.12f, 0.095f, 0.07f, 0.58f)),
                EnsureDressingAnchor(
                    scene,
                    "Runtime Prop Dressing - Stacked Boxes",
                    AtmosphereDressingAnchor.DressingKind.PropDressing,
                    PrimitiveType.Cube,
                    groundCenter + new Vector3(-2.2f, 0.35f, -0.8f),
                    Quaternion.Euler(0f, 12f, 0f),
                    new Vector3(0.7f, 0.7f, 0.7f),
                    new Color(0.36f, 0.23f, 0.13f, 1f)),
                EnsureDressingAnchor(
                    scene,
                    "Runtime Prop Dressing - Loose Plank",
                    AtmosphereDressingAnchor.DressingKind.PropDressing,
                    PrimitiveType.Cube,
                    groundCenter + new Vector3(2.1f, 0.08f, 0.2f),
                    Quaternion.Euler(0f, -28f, 2f),
                    new Vector3(1.6f, 0.12f, 0.22f),
                    new Color(0.24f, 0.14f, 0.075f, 1f))
            };
        }

        private static AtmosphereDressingAnchor EnsureDressingAnchor(
            Scene scene,
            string objectName,
            AtmosphereDressingAnchor.DressingKind kind,
            PrimitiveType primitiveType,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            Color color)
        {
            AtmosphereDressingAnchor anchor = FindNamedInScene<AtmosphereDressingAnchor>(scene, objectName);
            if (anchor == null)
            {
                GameObject dressingObject = GameObject.CreatePrimitive(primitiveType);
                dressingObject.name = objectName;
                SceneManager.MoveGameObjectToScene(dressingObject, scene);
                anchor = dressingObject.AddComponent<AtmosphereDressingAnchor>();
            }

            anchor.transform.SetPositionAndRotation(position, rotation);
            anchor.transform.localScale = scale;

            Renderer renderer = anchor.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateRuntimeMaterial(objectName, kind, color);
            }

            anchor.Configure(kind, kind == AtmosphereDressingAnchor.DressingKind.DirtyDecal ? 0.72f : 0.52f);

            Collider collider = anchor.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = kind == AtmosphereDressingAnchor.DressingKind.PropDressing;
            }

            return anchor;
        }

        private static Material CreateRuntimeMaterial(
            string name,
            AtmosphereDressingAnchor.DressingKind kind,
            Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color");
            Material material = new(shader)
            {
                name = $"{name} Runtime Material",
                hideFlags = HideFlags.DontSave
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.22f);
            }

            Texture2D texture = CreateRuntimeDressingTexture(name, kind, color);
            SetTexture(material, "_BaseMap", texture);
            SetTexture(material, "_MainTex", texture);

            if (kind == AtmosphereDressingAnchor.DressingKind.DirtyDecal)
            {
                ConfigureTransparentMaterial(material);
            }

            return material;
        }

        private static Texture2D CreateRuntimeDressingTexture(
            string name,
            AtmosphereDressingAnchor.DressingKind kind,
            Color color)
        {
            Texture2D texture = new(DressingTextureSize, DressingTextureSize, TextureFormat.RGBA32, true)
            {
                name = $"{name} Runtime Texture",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            Color[] pixels = new Color[DressingTextureSize * DressingTextureSize];
            for (int y = 0; y < DressingTextureSize; y++)
            {
                for (int x = 0; x < DressingTextureSize; x++)
                {
                    float horizontal = (float)x / (DressingTextureSize - 1);
                    float vertical = (float)y / (DressingTextureSize - 1);
                    float noise = Mathf.PerlinNoise(horizontal * 8.7f + 0.11f, vertical * 7.3f + 0.37f);
                    Color pixel = kind == AtmosphereDressingAnchor.DressingKind.DirtyDecal
                        ? CreateDirtyDecalPixel(color, horizontal, vertical, noise)
                        : CreatePropDressingPixel(color, horizontal, vertical, noise);
                    pixels[y * DressingTextureSize + x] = pixel;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true, false);
            return texture;
        }

        private static Color CreateDirtyDecalPixel(Color color, float horizontal, float vertical, float noise)
        {
            float smear = Mathf.Abs(Mathf.Sin((horizontal + noise * 0.16f) * Mathf.PI * 5.5f));
            float footfall = Mathf.PerlinNoise(horizontal * 2.1f + 2.4f, vertical * 11.8f + 0.5f);
            float alpha = color.a * Mathf.Clamp01(0.2f + noise * 0.65f + smear * 0.22f + footfall * 0.18f);
            Color pixel = Color.Lerp(color * 0.55f, color * 1.25f, Mathf.Clamp01(noise * 0.85f + smear * 0.25f));
            pixel.a = alpha;
            return pixel;
        }

        private static Color CreatePropDressingPixel(Color color, float horizontal, float vertical, float noise)
        {
            float grain = Mathf.Abs(Mathf.Sin((horizontal * 18f + vertical * 5f + noise * 2f) * Mathf.PI));
            float knots = Mathf.PerlinNoise(horizontal * 18f + 4.2f, vertical * 18f + 1.7f);
            float shade = Mathf.Clamp01(noise * 0.5f + grain * 0.36f + knots * 0.22f);
            Color pixel = Color.Lerp(color * 0.62f, color * 1.28f, shade);
            pixel.a = color.a;
            return pixel;
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetInt("_ZWrite", 0);
            }

            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void SetTexture(Material material, string propertyName, Texture texture)
        {
            if (material != null && texture != null && material.HasProperty(propertyName))
            {
                material.SetTexture(propertyName, texture);
            }
        }

        private static Vector3 FindSceneGroundCenter(Scene scene)
        {
            Renderer[] renderers = FindAllInScene<Renderer>(scene);
            if (renderers.Length == 0)
            {
                return Vector3.zero;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        private static GameObject CreateSceneObject(Scene scene, string name)
        {
            GameObject gameObject = new(name);
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            return gameObject;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            T[] components = FindAllInScene<T>(scene);
            return components.Length > 0 ? components[0] : null;
        }

        private static T FindNamedInScene<T>(Scene scene, string objectName) where T : Component
        {
            T[] components = FindAllInScene<T>(scene);
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component != null && component.name == objectName)
                {
                    return component;
                }
            }

            return null;
        }

        private static T[] FindAllInScene<T>(Scene scene) where T : Component
        {
            T[] components = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            int writeIndex = 0;
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component != null && component.gameObject.scene == scene)
                {
                    components[writeIndex] = component;
                    writeIndex++;
                }
            }

            if (writeIndex == components.Length)
            {
                return components;
            }

            T[] filtered = new T[writeIndex];
            System.Array.Copy(components, filtered, writeIndex);
            return filtered;
        }
    }
}
