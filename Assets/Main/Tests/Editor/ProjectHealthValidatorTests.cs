using System.Text.RegularExpressions;
using Neighbor.Main.Features.Interaction;
using Neighbor.Main.Features.Progression;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Neighbor.Main.Tests
{
    public sealed class ProjectHealthValidatorTests
    {
        [Test]
        public void ProjectHealthValidatorScript_IsAvailableForBatchValidation()
        {
            MonoScript validatorScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
                "Assets/Main/Editor/ProjectHealthValidator.cs");

            Assert.That(validatorScript, Is.Not.Null);
        }

        [Test]
        public void Validator_FlagsAudioSourcePlayingOnAwakeWithoutClip()
        {
            GameObject root = new("BadAudioRoot");
            try
            {
                AudioSource audioSource = root.AddComponent<AudioSource>();
                audioSource.playOnAwake = true;
                audioSource.clip = null;

                LogAssert.Expect(LogType.Error, new Regex("AudioSource plays on awake without a clip"));
                Assert.That(ProjectHealthValidator.ValidateGameObjectForTests(root, "SyntheticAudio.prefab"), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Validator_FlagsDoorKeyOutsidePickupableHierarchy()
        {
            GameObject root = new("StandaloneKey");
            try
            {
                root.AddComponent<DoorKey>();

                LogAssert.Expect(LogType.Error, new Regex("DoorKey is not attached to a Pickupable hierarchy"));
                Assert.That(ProjectHealthValidator.ValidateGameObjectForTests(root, "SyntheticKey.prefab"), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Validator_FlagsDoorWithoutCollider()
        {
            GameObject root = new("DoorWithoutCollider");
            try
            {
                root.AddComponent<Door>();

                LogAssert.Expect(LogType.Error, new Regex("Door has no collider"));
                Assert.That(ProjectHealthValidator.ValidateGameObjectForTests(root, "SyntheticDoor.prefab"), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Validator_FlagsLockedDoorWithoutRequiredKeyId()
        {
            GameObject root = new("LockedDoorWithoutKeyId");
            try
            {
                root.AddComponent<BoxCollider>();
                Door door = root.AddComponent<Door>();
                SetDoorString(door, "requiredKeyId", " ");
                SetDoorString(door, "missingKeyFeedbackFormat", "Need {0}");

                LogAssert.Expect(LogType.Error, new Regex("Locked Door has no required key id"));
                Assert.That(ProjectHealthValidator.ValidateGameObjectForTests(root, "SyntheticLockedDoor.prefab"), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Validator_FlagsLockedDoorFeedbackWithoutKeyNamePlaceholder()
        {
            GameObject root = new("LockedDoorWithoutReadableHint");
            try
            {
                root.AddComponent<BoxCollider>();
                Door door = root.AddComponent<Door>();
                SetDoorString(door, "requiredKeyId", "basement_key");
                SetDoorString(door, "missingKeyFeedbackFormat", "Locked");

                LogAssert.Expect(LogType.Error, new Regex("missing-key feedback must include"));
                Assert.That(ProjectHealthValidator.ValidateGameObjectForTests(root, "SyntheticLockedDoor.prefab"), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Validator_FlagsObjectiveZoneWithoutTriggerCollider()
        {
            GameObject root = new("ObjectiveZoneWithoutTrigger");
            try
            {
                BoxCollider collider = root.AddComponent<BoxCollider>();
                root.AddComponent<ObjectiveTriggerZone>();
                collider.isTrigger = false;

                LogAssert.Expect(LogType.Error, new Regex("Objective trigger zone requires a trigger collider"));
                Assert.That(ProjectHealthValidator.ValidateGameObjectForTests(root, "SyntheticObjectiveZone.prefab"), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Validator_FlagsTerrainWithoutTerrainData()
        {
            GameObject root = new("TerrainWithoutData");
            try
            {
                root.AddComponent<Terrain>();

                LogAssert.Expect(LogType.Error, new Regex("Terrain has no TerrainData"));
                Assert.That(ProjectHealthValidator.ValidateGameObjectForTests(root, "SyntheticTerrain.prefab"), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Validator_FlagsTerrainTreePrototypeWithoutPrefab()
        {
            GameObject root = new("TerrainWithMissingTreePrototypePrefab");
            TerrainData terrainData = new();
            try
            {
                AddBudgetedTerrain(root, terrainData);
                terrainData.treePrototypes = new[]
                {
                    new TreePrototype()
                };

                LogAssert.Expect(LogType.Error, new Regex("Tree prefab at index 0 is missing"));
                LogAssert.Expect(LogType.Error, new Regex("Terrain tree prototype 0 is missing a prefab"));
                Assert.That(ProjectHealthValidator.ValidateGameObjectForTests(root, "SyntheticTerrain.prefab"), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(terrainData);
            }
        }

        [Test]
        public void Validator_FlagsTerrainTreePrototypeWithoutRenderer()
        {
            GameObject root = new("TerrainWithRendererlessTreePrototype");
            GameObject treePrefab = new("TreePrototypeWithoutRenderer");
            TerrainData terrainData = new();
            try
            {
                AddBudgetedTerrain(root, terrainData);
                terrainData.treePrototypes = new[]
                {
                    new TreePrototype { prefab = treePrefab }
                };

                LogAssert.Expect(LogType.Error, new Regex("Terrain tree prototype 0 prefab has no renderers"));
                Assert.That(ProjectHealthValidator.ValidateGameObjectForTests(root, "SyntheticTerrain.prefab"), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(treePrefab);
                Object.DestroyImmediate(terrainData);
            }
        }

        [Test]
        public void Validator_FlagsTerrainTreePrototypeWithMissingMaterial()
        {
            GameObject root = new("TerrainWithMissingTreePrototypeMaterial");
            GameObject treePrefab = new("TreePrototypeMissingMaterial");
            TerrainData terrainData = new();
            try
            {
                AddBudgetedTerrain(root, terrainData);
                MeshRenderer renderer = treePrefab.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = new Material[] { null };
                terrainData.treePrototypes = new[]
                {
                    new TreePrototype { prefab = treePrefab }
                };

                LogAssert.Expect(LogType.Error, new Regex("Missing material reference: slot 0"));
                Assert.That(ProjectHealthValidator.ValidateGameObjectForTests(root, "SyntheticTerrain.prefab"), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(treePrefab);
                Object.DestroyImmediate(terrainData);
            }
        }

        [Test]
        public void Validator_FlagsTerrainTreePrototypeWithPinkFallbackMaterial()
        {
            GameObject root = new("TerrainWithPinkTreePrototypeMaterial");
            GameObject treePrefab = new("TreePrototypePinkMaterial");
            Material material = CreatePinkFallbackMaterial();
            TerrainData terrainData = new();
            try
            {
                AddBudgetedTerrain(root, terrainData);
                MeshRenderer renderer = treePrefab.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                terrainData.treePrototypes = new[]
                {
                    new TreePrototype { prefab = treePrefab }
                };

                LogAssert.Expect(LogType.Error, new Regex("Pink fallback material likely missing texture"));
                Assert.That(ProjectHealthValidator.ValidateGameObjectForTests(root, "SyntheticTerrain.prefab"), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(treePrefab);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(terrainData);
            }
        }

        [Test]
        public void Validator_FlagsTerrainTreePrototypeWithTexturelessVegetationMaterial()
        {
            GameObject root = new("TerrainWithTexturelessTreePrototypeMaterial");
            GameObject treePrefab = new("TreePrototypeTexturelessLeafMaterial");
            Material material = CreateTexturelessVegetationMaterial("leaf_textureless");
            TerrainData terrainData = new();
            try
            {
                AddBudgetedTerrain(root, terrainData);
                MeshRenderer renderer = treePrefab.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                terrainData.treePrototypes = new[]
                {
                    new TreePrototype { prefab = treePrefab }
                };

                LogAssert.Expect(LogType.Error, new Regex("Vegetation material has no albedo texture"));
                Assert.That(ProjectHealthValidator.ValidateGameObjectForTests(root, "SyntheticTerrain.prefab"), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(treePrefab);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(terrainData);
            }
        }

        [Test]
        public void Validator_FlagsLodRendererWithPinkFallbackMaterial()
        {
            GameObject root = new("TreeLodRoot");
            GameObject lodObject = new("TreeLod0");
            Material material = CreatePinkFallbackMaterial();
            try
            {
                lodObject.transform.SetParent(root.transform);
                MeshRenderer renderer = lodObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                LODGroup lodGroup = root.AddComponent<LODGroup>();
                lodGroup.SetLODs(new[] { new LOD(0.5f, new Renderer[] { renderer }) });

                LogAssert.Expect(LogType.Error, new Regex("Pink fallback material likely missing texture"));
                Assert.That(ProjectHealthValidator.ValidateGameObjectForTests(root, "SyntheticTree.prefab"), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void Validator_FlagsLodRendererWithTexturelessVegetationMaterial()
        {
            GameObject root = new("TreeLodRoot");
            GameObject lodObject = new("TreeLod0");
            Material material = CreateTexturelessVegetationMaterial("bark_textureless");
            try
            {
                lodObject.transform.SetParent(root.transform);
                MeshRenderer renderer = lodObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                LODGroup lodGroup = root.AddComponent<LODGroup>();
                lodGroup.SetLODs(new[] { new LOD(0.5f, new Renderer[] { renderer }) });

                LogAssert.Expect(LogType.Error, new Regex("Vegetation material has no albedo texture"));
                Assert.That(ProjectHealthValidator.ValidateGameObjectForTests(root, "SyntheticTree.prefab"), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(material);
            }
        }

        private static void AddBudgetedTerrain(GameObject root, TerrainData terrainData)
        {
            Terrain terrain = root.AddComponent<Terrain>();
            terrain.terrainData = terrainData;
            terrain.treeDistance = 200f;
            terrain.treeBillboardDistance = 80f;
            terrain.treeMaximumFullLODCount = 20;
            terrain.detailObjectDistance = 80f;
        }

        private static void SetDoorString(Door door, string propertyName, string value)
        {
            SerializedObject serializedDoor = new(door);
            SerializedProperty property = serializedDoor.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, $"Could not find Door property '{propertyName}'.");
            property.stringValue = value;
            serializedDoor.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material CreatePinkFallbackMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Color");
            Assert.That(shader, Is.Not.Null, "No built-in shader was available for material validation test.");

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

        private static Material CreateTexturelessVegetationMaterial(string materialName)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Color");
            Assert.That(shader, Is.Not.Null, "No built-in shader was available for material validation test.");

            Material material = new(shader)
            {
                name = materialName
            };

            Color naturalTint = new(0.31f, 0.42f, 0.19f, 1f);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", naturalTint);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", naturalTint);
            }

            return material;
        }
    }
}
