using Neighbor.Main.Features.Environment;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Neighbor.Main.Tests
{
    public sealed class VegetationMaterialGuardEditorTests
    {
        [Test]
        public void RepairSceneVegetationMaterials_ReplacesPinkTreeLodMaterial()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject tree = new("BasicTree");
            GameObject leaf = new("leaf_maple_lod0");
            Material pinkMaterial = CreatePinkMaterial();
            try
            {
                leaf.transform.SetParent(tree.transform);
                MeshRenderer renderer = leaf.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = pinkMaterial;
                LODGroup lodGroup = tree.AddComponent<LODGroup>();
                lodGroup.SetLODs(new[] { new LOD(0.4f, new Renderer[] { renderer }) });

                VegetationMaterialGuard guard = VegetationMaterialGuard.EnsureForScene(scene);
                int repairCount = guard.LastTotalRepairs;

                Material repairedMaterial = renderer.sharedMaterial;
                Assert.That(repairCount, Is.EqualTo(1));
                Assert.That(guard.LastSceneRendererRepairs, Is.EqualTo(1));
                Assert.That(repairedMaterial, Is.Not.Null);
                Assert.That(repairedMaterial, Is.Not.SameAs(pinkMaterial));
                Assert.That(VegetationMaterialGuard.IsBrokenVegetationMaterial(repairedMaterial), Is.False);
                Assert.That(HasMainTexture(repairedMaterial), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(tree);
                Object.DestroyImmediate(pinkMaterial);
            }
        }

        [Test]
        public void RepairSceneVegetationMaterials_ReplacesTexturelessBasicTreeLodMaterialFromParentContext()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject tree = new("BasicTree");
            GameObject lod = new("LOD0");
            Material texturelessMaterial = CreateTexturelessMaterial();
            try
            {
                lod.transform.SetParent(tree.transform);
                MeshRenderer renderer = lod.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = texturelessMaterial;
                LODGroup lodGroup = tree.AddComponent<LODGroup>();
                lodGroup.SetLODs(new[] { new LOD(0.6f, new Renderer[] { renderer }) });

                VegetationMaterialGuard guard = VegetationMaterialGuard.EnsureForScene(scene);
                int repairCount = guard.LastTotalRepairs;

                Material repairedMaterial = renderer.sharedMaterial;
                Assert.That(repairCount, Is.EqualTo(1));
                Assert.That(guard.LastSceneRendererRepairs, Is.EqualTo(1));
                Assert.That(repairedMaterial, Is.Not.Null);
                Assert.That(repairedMaterial, Is.Not.SameAs(texturelessMaterial));
                Assert.That(VegetationMaterialGuard.IsBrokenVegetationMaterial(repairedMaterial), Is.False);
                Assert.That(HasMainTexture(repairedMaterial), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(tree);
                Object.DestroyImmediate(texturelessMaterial);
            }
        }

        [Test]
        public void RepairSceneVegetationMaterials_LeafFallbackTextureUsesAlphaCutoutPattern()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject tree = new("BasicTree");
            GameObject leaf = new("leaf_maple_lod0");
            Material pinkMaterial = CreatePinkMaterial();
            try
            {
                leaf.transform.SetParent(tree.transform);
                MeshRenderer renderer = leaf.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = pinkMaterial;
                LODGroup lodGroup = tree.AddComponent<LODGroup>();
                lodGroup.SetLODs(new[] { new LOD(0.4f, new Renderer[] { renderer }) });

                VegetationMaterialGuard guard = VegetationMaterialGuard.EnsureForScene(scene);
                Material repairedMaterial = renderer.sharedMaterial;
                Texture2D texture = GetMainTexture(repairedMaterial) as Texture2D;

                Assert.That(guard.LastTotalRepairs, Is.EqualTo(1));
                Assert.That(texture, Is.Not.Null);

                Color[] pixels = texture.GetPixels();
                int transparentPixels = 0;
                int opaquePixels = 0;
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].a < 0.25f)
                    {
                        transparentPixels++;
                    }

                    if (pixels[i].a > 0.75f)
                    {
                        opaquePixels++;
                    }
                }

                Assert.That(transparentPixels, Is.GreaterThan(pixels.Length / 6));
                Assert.That(opaquePixels, Is.GreaterThan(pixels.Length / 6));
            }
            finally
            {
                Object.DestroyImmediate(tree);
                Object.DestroyImmediate(pinkMaterial);
            }
        }

        [Test]
        public void RepairSceneVegetationMaterials_ReplacesTerrainTreePrototypeMaterial()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject terrainObject = new("Terrain");
            GameObject prototype = new("PrototypePrefab");
            TerrainData terrainData = new();
            Material pinkMaterial = CreatePinkMaterial();
            try
            {
                Terrain terrain = terrainObject.AddComponent<Terrain>();
                terrain.terrainData = terrainData;
                MeshRenderer prototypeRenderer = prototype.AddComponent<MeshRenderer>();
                prototypeRenderer.sharedMaterial = pinkMaterial;
                terrainData.treePrototypes = new[]
                {
                    new TreePrototype { prefab = prototype }
                };

                VegetationMaterialGuard guard = VegetationMaterialGuard.EnsureForScene(scene);
                int repairCount = guard.LastTotalRepairs;

                Material repairedMaterial = prototypeRenderer.sharedMaterial;
                Assert.That(repairCount, Is.EqualTo(1));
                Assert.That(guard.LastSceneRendererRepairs, Is.Zero);
                Assert.That(guard.LastTerrainPrototypeRepairs, Is.EqualTo(1));
                Assert.That(repairedMaterial, Is.Not.Null);
                Assert.That(repairedMaterial, Is.Not.SameAs(pinkMaterial));
                Assert.That(VegetationMaterialGuard.IsBrokenVegetationMaterial(repairedMaterial), Is.False);
                Assert.That(HasMainTexture(repairedMaterial), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(terrainObject);
                Object.DestroyImmediate(prototype);
                Object.DestroyImmediate(terrainData);
                Object.DestroyImmediate(pinkMaterial);
            }
        }

        private static Material CreatePinkMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color");
            Material material = new(shader)
            {
                name = "PinkMissingTextureMaterial"
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.magenta);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.magenta);
            }

            return material;
        }

        private static Material CreateTexturelessMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color");
            Material material = new(shader)
            {
                name = "PrototypeDefaultMaterial"
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", new Color(0.52f, 0.48f, 0.39f, 1f));
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", new Color(0.52f, 0.48f, 0.39f, 1f));
            }

            return material;
        }

        private static bool HasMainTexture(Material material)
        {
            if (material == null)
            {
                return false;
            }

            return GetMainTexture(material) != null;
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
    }
}
