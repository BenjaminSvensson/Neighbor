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
    }
}
