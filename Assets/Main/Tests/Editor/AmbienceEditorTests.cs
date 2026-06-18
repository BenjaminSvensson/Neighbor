using Neighbor.Main.Features.Audio;
using Neighbor.Main.Features.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Neighbor.Main.Tests
{
    public sealed class AmbienceEditorTests
    {
        private const string AreaPrefabPath = "Assets/Main/Features/Audio/Ambience/Prefabs/AreaSpecificAmbienceArea.prefab";
        private const string LargeAreaPrefabPath = "Assets/Main/Features/Audio/Ambience/Prefabs/LargeAreaSpecificAmbienceArea.prefab";

        [TestCase(AreaPrefabPath)]
        [TestCase(LargeAreaPrefabPath)]
        public void AreaSpecificAmbiencePrefab_HasTriggerColliderAndAreaComponent(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<AmbienceArea>(), Is.Not.Null);

            Collider collider = prefab.GetComponent<Collider>();
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.isTrigger, Is.True);
        }

        [Test]
        public void AmbienceArea_ContainsPointInsideAssignedCollider()
        {
            GameObject areaObject = new("Ambience Area Test");
            try
            {
                BoxCollider collider = areaObject.AddComponent<BoxCollider>();
                collider.size = new Vector3(4f, 3f, 4f);
                collider.center = new Vector3(0f, 1.5f, 0f);
                AmbienceArea area = areaObject.AddComponent<AmbienceArea>();

                Physics.SyncTransforms();

                Assert.That(area.Contains(new Vector3(0f, 1.5f, 0f)), Is.True);
                Assert.That(area.Contains(new Vector3(5f, 1.5f, 0f)), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(areaObject);
            }
        }

        [Test]
        public void AmbienceArea_PlayerTriggerContactMarksAreaActive()
        {
            GameObject areaObject = new("Ambience Area Test");
            GameObject playerObject = new("Player Test");
            try
            {
                areaObject.AddComponent<BoxCollider>().isTrigger = true;
                AmbienceArea area = areaObject.AddComponent<AmbienceArea>();
                Collider playerCollider = playerObject.AddComponent<BoxCollider>();
                playerObject.AddComponent<PlayerController>();

                GameplaySmokeTestReflection.Invoke(area, "OnTriggerEnter", playerCollider);

                Assert.That(area.HasPlayerInside, Is.True);

                GameplaySmokeTestReflection.Invoke(area, "OnTriggerExit", playerCollider);

                Assert.That(area.HasPlayerInside, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(areaObject);
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void AmbienceManager_UsesMarkedDefaultOnlyWhenNoNormalAreaIsActive()
        {
            GameObject managerObject = new("Ambience Manager Test");
            GameObject listenerObject = new("Listener Test");
            GameObject activeAreaObject = new("Active Area Test");
            GameObject defaultAreaObject = new("Default Area Test");
            AmbienceProfile activeProfile = ScriptableObject.CreateInstance<AmbienceProfile>();
            AmbienceProfile defaultAreaProfile = ScriptableObject.CreateInstance<AmbienceProfile>();

            try
            {
                AmbienceManager manager = managerObject.AddComponent<AmbienceManager>();
                GameplaySmokeTestReflection.SetField(manager, "listener", listenerObject.transform);
                GameplaySmokeTestReflection.SetField(manager, "player", null);

                AmbienceArea activeArea = CreateArea(activeAreaObject, activeProfile, false, Vector3.zero);
                AmbienceArea defaultArea = CreateArea(defaultAreaObject, defaultAreaProfile, true, new Vector3(20f, 0f, 0f));
                GameplaySmokeTestReflection.InvokeIfPresent(activeArea, "OnEnable");
                GameplaySmokeTestReflection.InvokeIfPresent(defaultArea, "OnEnable");

                listenerObject.transform.position = new Vector3(20f, 1.5f, 0f);
                Physics.SyncTransforms();

                Assert.That(
                    GameplaySmokeTestReflection.InvokeResult<AmbienceProfile>(manager, "GetDesiredProfile"),
                    Is.SameAs(defaultAreaProfile));

                listenerObject.transform.position = new Vector3(0f, 1.5f, 0f);
                Physics.SyncTransforms();

                Assert.That(
                    GameplaySmokeTestReflection.InvokeResult<AmbienceProfile>(manager, "GetDesiredProfile"),
                    Is.SameAs(activeProfile));

                GameplaySmokeTestReflection.InvokeIfPresent(activeArea, "OnDisable");
                GameplaySmokeTestReflection.InvokeIfPresent(defaultArea, "OnDisable");
            }
            finally
            {
                Object.DestroyImmediate(activeProfile);
                Object.DestroyImmediate(defaultAreaProfile);
                Object.DestroyImmediate(managerObject);
                Object.DestroyImmediate(listenerObject);
                Object.DestroyImmediate(activeAreaObject);
                Object.DestroyImmediate(defaultAreaObject);
            }
        }

        private static AmbienceArea CreateArea(
            GameObject areaObject,
            AmbienceProfile profile,
            bool playWhenNoAreaActive,
            Vector3 position)
        {
            areaObject.transform.position = position;
            BoxCollider collider = areaObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(4f, 3f, 4f);
            collider.center = new Vector3(0f, 1.5f, 0f);

            AmbienceArea area = areaObject.AddComponent<AmbienceArea>();
            GameplaySmokeTestReflection.SetField(area, "profile", profile);
            GameplaySmokeTestReflection.SetField(area, "playWhenNoAreaActive", playWhenNoAreaActive);
            return area;
        }
    }
}
