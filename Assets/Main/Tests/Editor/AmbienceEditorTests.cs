using Neighbor.Main.Features.Audio;
using Neighbor.Main.Features.Interaction;
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
            AmbienceArea area = prefab.GetComponent<AmbienceArea>();
            Assert.That(area, Is.Not.Null);
            Assert.That(area.ZoneLocation, Is.EqualTo(AmbienceZoneLocation.Inside));

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
                GameplaySmokeTestReflection.SetField<PlayerController>(manager, "player", null);

                AmbienceArea activeArea = CreateArea(activeAreaObject, activeProfile, false, Vector3.zero);
                AmbienceArea defaultArea = CreateArea(defaultAreaObject, defaultAreaProfile, true, new Vector3(20f, 0f, 0f), AmbienceZoneLocation.Outside);
                GameplaySmokeTestReflection.InvokeIfPresent(activeArea, "OnEnable");
                GameplaySmokeTestReflection.InvokeIfPresent(defaultArea, "OnEnable");

                listenerObject.transform.position = new Vector3(20f, 1.5f, 0f);
                Physics.SyncTransforms();

                Assert.That(
                    GameplaySmokeTestReflection.InvokeResult<AmbienceProfile>(manager, "GetDesiredProfile"),
                    Is.SameAs(defaultAreaProfile));
                Assert.That(
                    GameplaySmokeTestReflection.InvokeResult<AmbienceZoneLocation>(manager, "GetDesiredZoneLocation"),
                    Is.EqualTo(AmbienceZoneLocation.Outside));

                listenerObject.transform.position = new Vector3(0f, 1.5f, 0f);
                Physics.SyncTransforms();

                Assert.That(
                    GameplaySmokeTestReflection.InvokeResult<AmbienceProfile>(manager, "GetDesiredProfile"),
                    Is.SameAs(activeProfile));
                Assert.That(
                    GameplaySmokeTestReflection.InvokeResult<AmbienceZoneLocation>(manager, "GetDesiredZoneLocation"),
                    Is.EqualTo(AmbienceZoneLocation.Inside));

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

        [Test]
        public void AmbienceManager_BasementZoneAppliesListenerFeelAndWarning()
        {
            GameObject managerObject = new("Ambience Manager Test");
            GameObject listenerObject = new("Listener Test");
            GameObject areaObject = new("Basement Area Test");
            AmbienceProfile basementProfile = ScriptableObject.CreateInstance<AmbienceProfile>();
            PlayerFeedbackEvents.AmbienceZoneFeedback receivedFeedback = default;
            bool receivedWarning = false;
            void HandleZoneWarning(PlayerFeedbackEvents.AmbienceZoneFeedback feedback)
            {
                receivedFeedback = feedback;
                receivedWarning = true;
            }

            try
            {
                listenerObject.AddComponent<AudioListener>();
                GameplaySmokeTestReflection.SetField(basementProfile, "listenerLowPassCutoff", 1800f);
                GameplaySmokeTestReflection.SetField(basementProfile, "listenerReverbPreset", AudioReverbPreset.Cave);
                GameplaySmokeTestReflection.SetField(basementProfile, "zoneWarningIntensity", 0.85f);
                GameplaySmokeTestReflection.SetField(basementProfile, "zoneWarningText", "Basement pressure");

                AmbienceManager manager = managerObject.AddComponent<AmbienceManager>();
                GameplaySmokeTestReflection.SetField(manager, "listener", listenerObject.transform);
                GameplaySmokeTestReflection.SetField<PlayerController>(manager, "player", null);

                AmbienceArea area = CreateArea(areaObject, basementProfile, false, Vector3.zero, AmbienceZoneLocation.Basement);
                GameplaySmokeTestReflection.InvokeIfPresent(area, "OnEnable");
                listenerObject.transform.position = new Vector3(0f, 1.5f, 0f);
                Physics.SyncTransforms();

                PlayerFeedbackEvents.AmbienceZoneChanged += HandleZoneWarning;
                GameplaySmokeTestReflection.Invoke(manager, "Update");

                Assert.That(manager.CurrentZoneLocation, Is.EqualTo(AmbienceZoneLocation.Basement));
                Assert.That(listenerObject.GetComponent<AudioLowPassFilter>(), Is.Not.Null);
                Assert.That(listenerObject.GetComponent<AudioLowPassFilter>().enabled, Is.True);
                Assert.That(listenerObject.GetComponent<AudioReverbFilter>(), Is.Not.Null);
                Assert.That(listenerObject.GetComponent<AudioReverbFilter>().reverbPreset, Is.EqualTo(AudioReverbPreset.Cave));
                Assert.That(receivedWarning, Is.True);
                Assert.That(receivedFeedback.Message, Is.EqualTo("Basement pressure"));
                Assert.That(receivedFeedback.Intensity, Is.EqualTo(0.85f).Within(0.001f));

                GameplaySmokeTestReflection.InvokeIfPresent(area, "OnDisable");
            }
            finally
            {
                PlayerFeedbackEvents.AmbienceZoneChanged -= HandleZoneWarning;
                AmbienceManager.ResetNoiseAcousticModifiers();
                Object.DestroyImmediate(basementProfile);
                Object.DestroyImmediate(managerObject);
                Object.DestroyImmediate(listenerObject);
                Object.DestroyImmediate(areaObject);
            }
        }

        [Test]
        public void AmbienceManager_GarageZoneAmplifiesNoiseEvents()
        {
            GameObject managerObject = new("Ambience Manager Test");
            GameObject listenerObject = new("Listener Test");
            GameObject areaObject = new("Garage Area Test");
            GameObject noiseObject = new("Garage Noise Test");
            AmbienceProfile garageProfile = ScriptableObject.CreateInstance<AmbienceProfile>();
            PlayerFeedbackEvents.NoiseFeedback receivedNoise = default;
            bool receivedFeedback = false;

            void HandleNoise(PlayerFeedbackEvents.NoiseFeedback feedback)
            {
                receivedNoise = feedback;
                receivedFeedback = true;
            }

            try
            {
                AmbienceManager.ResetNoiseAcousticModifiers();
                GameplaySmokeTestReflection.SetField(garageProfile, "noiseLoudnessMultiplier", 1.25f);
                GameplaySmokeTestReflection.SetField(garageProfile, "noiseRadiusMultiplier", 1.1f);

                AmbienceManager manager = managerObject.AddComponent<AmbienceManager>();
                GameplaySmokeTestReflection.SetField(manager, "listener", listenerObject.transform);
                GameplaySmokeTestReflection.SetField<PlayerController>(manager, "player", null);

                AmbienceArea area = CreateArea(areaObject, garageProfile, false, Vector3.zero, AmbienceZoneLocation.Garage);
                GameplaySmokeTestReflection.InvokeIfPresent(area, "OnEnable");
                listenerObject.transform.position = new Vector3(0f, 1.5f, 0f);
                Physics.SyncTransforms();

                GameplaySmokeTestReflection.Invoke(manager, "Update");

                float expectedLoudness = Mathf.Clamp01(0.4f * 1.25f * 1.12f);
                float expectedRadius = 5f * 1.1f * 1.35f;
                Assert.That(AmbienceManager.ActiveNoiseLoudnessMultiplier, Is.EqualTo(1.25f * 1.12f).Within(0.001f));
                Assert.That(AmbienceManager.ActiveNoiseRadiusMultiplier, Is.EqualTo(1.1f * 1.35f).Within(0.001f));

                PlayerFeedbackEvents.NoiseEmitted += HandleNoise;
                noiseObject.AddComponent<SphereCollider>();
                NoiseEvent noiseEvent = noiseObject.AddComponent<NoiseEvent>();
                noiseEvent.Initialize(Vector3.zero, 5f, 0.4f, noiseObject, 1f);

                Assert.That(noiseEvent.Loudness01, Is.EqualTo(expectedLoudness).Within(0.001f));
                Assert.That(noiseEvent.Radius, Is.EqualTo(expectedRadius).Within(0.001f));
                Assert.That(noiseObject.GetComponent<SphereCollider>().radius, Is.EqualTo(expectedRadius).Within(0.001f));
                Assert.That(receivedFeedback, Is.True);
                Assert.That(receivedNoise.Loudness, Is.EqualTo(expectedLoudness).Within(0.001f));
                Assert.That(receivedNoise.Radius, Is.EqualTo(expectedRadius).Within(0.001f));

                GameplaySmokeTestReflection.InvokeIfPresent(area, "OnDisable");
            }
            finally
            {
                PlayerFeedbackEvents.NoiseEmitted -= HandleNoise;
                AmbienceManager.ResetNoiseAcousticModifiers();
                Object.DestroyImmediate(garageProfile);
                Object.DestroyImmediate(managerObject);
                Object.DestroyImmediate(listenerObject);
                Object.DestroyImmediate(areaObject);
                Object.DestroyImmediate(noiseObject);
            }
        }

        [Test]
        public void AmbienceLayer_ZeroPitchFallsBackToNormalPlaybackSpeed()
        {
            AmbienceLayer layer = new();

            GameplaySmokeTestReflection.SetField(layer, "pitch", 0f);

            Assert.That(layer.PlaybackPitch, Is.EqualTo(1f));
        }

        private static AmbienceArea CreateArea(
            GameObject areaObject,
            AmbienceProfile profile,
            bool playWhenNoAreaActive,
            Vector3 position,
            AmbienceZoneLocation zoneLocation = AmbienceZoneLocation.Inside)
        {
            areaObject.transform.position = position;
            BoxCollider collider = areaObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(4f, 3f, 4f);
            collider.center = new Vector3(0f, 1.5f, 0f);

            AmbienceArea area = areaObject.AddComponent<AmbienceArea>();
            GameplaySmokeTestReflection.SetField(area, "profile", profile);
            GameplaySmokeTestReflection.SetField(area, "playWhenNoAreaActive", playWhenNoAreaActive);
            GameplaySmokeTestReflection.SetField(area, "zoneLocation", zoneLocation);
            return area;
        }
    }
}
