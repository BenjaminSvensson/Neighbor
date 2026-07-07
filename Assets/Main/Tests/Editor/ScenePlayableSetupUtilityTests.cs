using Neighbor.Main.Features.Audio;
using Neighbor.Main.Features.Environment;
using Neighbor.Main.Features.Interaction;
using Neighbor.Main.Features.Neighbor;
using Neighbor.Main.Features.Player;
using Neighbor.Main.Features.Progression;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Neighbor.Main.Tests
{
    public sealed class ScenePlayableSetupUtilityTests
    {
        [Test]
        public void ScenePlayableSetupUtilityScript_IsAvailableForEditorWorkflow()
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Main/Editor/ScenePlayableSetupUtility.cs");
            Assert.That(script, Is.Not.Null);
        }

        [Test]
        public void MakeActiveScenePlayable_CreatesRequiredGameplayRoots()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ScenePlayableSetupResult result = ScenePlayableSetupUtility.MakeActiveScenePlayable(false);

            Assert.That(result.CreatedPlayer, Is.True);
            Assert.That(result.CreatedNeighbor, Is.True);
            Assert.That(result.CreatedObjectiveTracker, Is.True);
            Assert.That(result.CreatedStartCheckpoint, Is.True);
            Assert.That(result.CreatedNavMeshSurface, Is.True);
            Assert.That(result.CreatedAmbienceManager, Is.True);
            Assert.That(result.CreatedAwarenessHud, Is.True);
            Assert.That(result.CreatedInventoryHud, Is.True);
            Assert.That(result.CreatedEventSystem, Is.True);
            Assert.That(result.CreatedDirectionalLight, Is.True);
            Assert.That(result.CreatedMoonLight, Is.True);
            Assert.That(result.CreatedDayNightCycle, Is.True);
            Assert.That(result.CreatedAtmosphereDirector, Is.True);
            Assert.That(result.CreatedAtmosphereVolume, Is.True);
            Assert.That(result.CreatedFlickerLight, Is.True);
            Assert.That(result.CreatedAtmosphereDressing, Is.True);
            Assert.That(result.CreatedVegetationMaterialGuard, Is.True);
            Assert.That(result.AddedPauseMenu, Is.True);
            Assert.That(result.Player, Is.Not.Null);
            Assert.That(result.PlayerDeathController, Is.Not.Null);
            Assert.That(result.PlayerKeyRing, Is.Not.Null);
            Assert.That(result.PlayerHidingState, Is.Not.Null);
            Assert.That(result.OnboardingDirector, Is.Not.Null);
            Assert.That(result.PauseMenu, Is.Not.Null);
            Assert.That(result.Neighbor, Is.Not.Null);
            Assert.That(result.ObjectiveTracker, Is.Not.Null);
            Assert.That(result.StartCheckpoint, Is.Not.Null);
            Assert.That(result.NavMeshSurface, Is.Not.Null);
            Assert.That(result.AmbienceManager, Is.Not.Null);
            Assert.That(result.AwarenessHud, Is.Not.Null);
            Assert.That(result.InventoryHud, Is.Not.Null);
            Assert.That(result.EventSystem, Is.Not.Null);
            Assert.That(result.EventSystem.GetComponent<InputSystemUIInputModule>(), Is.Not.Null);
            Assert.That(result.EventSystem.GetComponent<StandaloneInputModule>(), Is.Null);
            Assert.That(result.DirectionalLight, Is.Not.Null);
            Assert.That(result.MoonLight, Is.Not.Null);
            Assert.That(result.DayNightCycle, Is.Not.Null);
            Assert.That(result.AtmosphereDirector, Is.Not.Null);
            Assert.That(result.VegetationMaterialGuard, Is.Not.Null);
            Assert.That(result.AtmosphereDirector.HasColorGradingVolume, Is.True);
            Assert.That(result.AtmosphereDirector.FlickerLightCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(result.AtmosphereDirector.DirtyDecalAnchorCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(result.AtmosphereDirector.PropDressingAnchorCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(result.StartCheckpoint.CheckpointId, Is.EqualTo("Start"));
            Assert.That(result.StartCheckpoint.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(result.StartCheckpoint.GetComponent<BoxCollider>().isTrigger, Is.True);

            Assert.That(CountInScene<PlayerController>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerDeathController>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerKeyRing>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerHidingState>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerOnboardingDirector>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerPauseMenu>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<NeighborBrain>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<CoreLoopObjectiveTracker>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerRespawnCheckpoint>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<NavMeshSurface>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<AmbienceManager>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerAwarenessHudView>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerInventoryHudView>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<EventSystem>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<DayNightCycle>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<SceneAtmosphereDirector>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<VegetationMaterialGuard>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<AtmosphereFlickerLight>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<AtmosphereDressingAnchor>(scene), Is.EqualTo(4));
        }

        [Test]
        public void ApplyAtmosphereToActiveScene_CreatesVisualAtmosphereOnly()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ScenePlayableSetupResult result = ScenePlayableSetupUtility.ApplyAtmosphereToActiveScene();

            Assert.That(result.CreatedDirectionalLight, Is.True);
            Assert.That(result.CreatedMoonLight, Is.True);
            Assert.That(result.CreatedDayNightCycle, Is.True);
            Assert.That(result.CreatedAtmosphereDirector, Is.True);
            Assert.That(result.CreatedAtmosphereVolume, Is.True);
            Assert.That(result.CreatedFlickerLight, Is.True);
            Assert.That(result.CreatedAtmosphereDressing, Is.True);
            Assert.That(result.CreatedVegetationMaterialGuard, Is.True);
            Assert.That(result.Player, Is.Null);
            Assert.That(result.Neighbor, Is.Null);
            Assert.That(result.VegetationMaterialGuard, Is.Not.Null);

            Assert.That(CountInScene<PlayerController>(scene), Is.Zero);
            Assert.That(CountInScene<NeighborBrain>(scene), Is.Zero);
            Assert.That(CountInScene<SceneAtmosphereDirector>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<VegetationMaterialGuard>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<AtmosphereFlickerLight>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<AtmosphereDressingAnchor>(scene), Is.EqualTo(4));
        }

        [Test]
        public void SceneAtmosphereBootstrapper_SkipsUntitledScenesUnlessForced()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SceneAtmosphereDirector director = SceneAtmosphereBootstrapper.EnsureAtmosphereForScene(scene);

            Assert.That(director, Is.Null);
            Assert.That(CountInScene<SceneAtmosphereDirector>(scene), Is.Zero);
            Assert.That(CountInScene<VegetationMaterialGuard>(scene), Is.Zero);
            Assert.That(CountInScene<AtmosphereFlickerLight>(scene), Is.Zero);
            Assert.That(CountInScene<AtmosphereDressingAnchor>(scene), Is.Zero);
        }

        [Test]
        public void SceneAtmosphereBootstrapper_ForcedSceneCreatesFullAtmospherePass()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Atmosphere Bootstrap Floor";
            floor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            floor.transform.localScale = new Vector3(8f, 0.2f, 8f);
            SceneManager.MoveGameObjectToScene(floor, scene);

            SceneAtmosphereDirector director = SceneAtmosphereBootstrapper.EnsureAtmosphereForScene(scene, true);
            SceneAtmosphereDirector secondDirector = SceneAtmosphereBootstrapper.EnsureAtmosphereForScene(scene, true);

            Assert.That(director, Is.Not.Null);
            Assert.That(secondDirector, Is.SameAs(director));
            Assert.That(director.HasColorGradingVolume, Is.True);
            Assert.That(director.FlickerLightCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(director.DirtyDecalAnchorCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(director.PropDressingAnchorCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(CountInScene<SceneAtmosphereDirector>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<VegetationMaterialGuard>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<AtmosphereFlickerLight>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<AtmosphereDressingAnchor>(scene), Is.EqualTo(4));
        }

        [Test]
        public void SceneAtmosphereDirector_StealthLoopDangerIntensifiesFogAndGrade()
        {
            RenderSettingsSnapshot snapshot = RenderSettingsSnapshot.Capture();
            GameObject directorObject = new("Atmosphere Director Test");
            GameObject volumeObject = new("Atmosphere Volume Test");

            try
            {
                Volume volume = volumeObject.AddComponent<Volume>();
                SceneAtmosphereDirector director = directorObject.AddComponent<SceneAtmosphereDirector>();
                director.Configure(
                    null,
                    null,
                    volume,
                    new AtmosphereFlickerLight[0],
                    new AtmosphereDressingAnchor[0]);

                Assert.That(volume.profile, Is.Not.Null);
                Assert.That(volume.profile.TryGet(out ColorAdjustments colorAdjustments), Is.True);
                Assert.That(volume.profile.TryGet(out Vignette vignette), Is.True);
                Assert.That(volume.profile.TryGet(out FilmGrain filmGrain), Is.True);

                float baseFogDensity = RenderSettings.fogDensity;
                float baseExposure = colorAdjustments.postExposure.value;
                float baseContrast = colorAdjustments.contrast.value;
                float baseSaturation = colorAdjustments.saturation.value;
                float baseVignette = vignette.intensity.value;
                float baseFilmGrain = filmGrain.intensity.value;

                PlayerFeedbackEvents.ReportStealthLoop(
                    PlayerFeedbackEvents.StealthLoopPhase.Chased,
                    1f,
                    0.8f,
                    0.9f,
                    "Run or hide");

                Assert.That(director.CurrentStealthAtmosphereIntensity, Is.GreaterThan(0.95f));
                Assert.That(director.TargetStealthAtmosphereIntensity, Is.GreaterThan(0.95f));
                Assert.That(RenderSettings.fogDensity, Is.GreaterThan(baseFogDensity));
                Assert.That(colorAdjustments.postExposure.value, Is.LessThan(baseExposure));
                Assert.That(colorAdjustments.contrast.value, Is.GreaterThan(baseContrast));
                Assert.That(colorAdjustments.saturation.value, Is.LessThan(baseSaturation));
                Assert.That(vignette.intensity.value, Is.GreaterThan(baseVignette));
                Assert.That(filmGrain.intensity.value, Is.GreaterThan(baseFilmGrain));

                PlayerFeedbackEvents.ReportStealthLoop(
                    PlayerFeedbackEvents.StealthLoopPhase.Quiet,
                    0f,
                    0f,
                    0f,
                    string.Empty);
                GameplaySmokeTestReflection.Invoke(director, "UpdateStealthAtmosphere", 2f);

                Assert.That(director.CurrentStealthAtmosphereIntensity, Is.Zero.Within(0.001f));
            }
            finally
            {
                snapshot.Restore();
                GameplaySmokeTestReflection.InvokeIfPresent(directorObject.GetComponent<SceneAtmosphereDirector>(), "OnDisable");
                Object.DestroyImmediate(directorObject);
                Object.DestroyImmediate(volumeObject);
            }
        }

        [Test]
        public void SceneAtmosphereDirector_CalmingHidingRecoveryEasesFogAndGrade()
        {
            RenderSettingsSnapshot snapshot = RenderSettingsSnapshot.Capture();
            GameObject directorObject = new("Recovery Atmosphere Director Test");
            GameObject volumeObject = new("Recovery Atmosphere Volume Test");

            try
            {
                Volume volume = volumeObject.AddComponent<Volume>();
                SceneAtmosphereDirector director = directorObject.AddComponent<SceneAtmosphereDirector>();
                director.Configure(
                    null,
                    null,
                    volume,
                    new AtmosphereFlickerLight[0],
                    new AtmosphereDressingAnchor[0]);

                PlayerFeedbackEvents.ReportStealthLoop(
                    PlayerFeedbackEvents.StealthLoopPhase.Chased,
                    1f,
                    0.8f,
                    0.9f,
                    "Run or hide");
                float dangerIntensity = director.CurrentStealthAtmosphereIntensity;

                PlayerFeedbackEvents.ReportStealthLoop(
                    PlayerFeedbackEvents.StealthLoopPhase.Hiding,
                    0.08f,
                    0f,
                    0.12f,
                    "Breathing under control.",
                    true);
                GameplaySmokeTestReflection.Invoke(director, "UpdateStealthAtmosphere", 1f);

                Assert.That(director.TargetStealthAtmosphereIntensity, Is.LessThan(0.2f));
                Assert.That(director.CurrentStealthAtmosphereIntensity, Is.LessThan(dangerIntensity));
                Assert.That(director.CurrentStealthAtmosphereIntensity, Is.LessThan(0.2f));
            }
            finally
            {
                snapshot.Restore();
                GameplaySmokeTestReflection.InvokeIfPresent(directorObject.GetComponent<SceneAtmosphereDirector>(), "OnDisable");
                Object.DestroyImmediate(directorObject);
                Object.DestroyImmediate(volumeObject);
            }
        }

        [Test]
        public void SceneAtmosphereDirector_CertainSuspicionIntensifiesFogAndGrade()
        {
            RenderSettingsSnapshot snapshot = RenderSettingsSnapshot.Capture();
            GameObject directorObject = new("Certain Atmosphere Director Test");
            GameObject volumeObject = new("Certain Atmosphere Volume Test");

            try
            {
                Volume volume = volumeObject.AddComponent<Volume>();
                SceneAtmosphereDirector director = directorObject.AddComponent<SceneAtmosphereDirector>();
                director.Configure(
                    null,
                    null,
                    volume,
                    new AtmosphereFlickerLight[0],
                    new AtmosphereDressingAnchor[0]);

                Assert.That(volume.profile, Is.Not.Null);
                Assert.That(volume.profile.TryGet(out ColorAdjustments colorAdjustments), Is.True);
                Assert.That(volume.profile.TryGet(out Vignette vignette), Is.True);

                float baseFogDensity = RenderSettings.fogDensity;
                float baseExposure = colorAdjustments.postExposure.value;
                float baseVignette = vignette.intensity.value;

                PlayerFeedbackEvents.ReportStealthLoop(
                    PlayerFeedbackEvents.StealthLoopPhase.Certain,
                    0.86f,
                    0f,
                    0.2f,
                    "He is locking on.");

                Assert.That(director.CurrentStealthAtmosphereIntensity, Is.GreaterThan(0.8f));
                Assert.That(director.CurrentStealthAtmosphereIntensity, Is.LessThan(1f));
                Assert.That(RenderSettings.fogDensity, Is.GreaterThan(baseFogDensity));
                Assert.That(colorAdjustments.postExposure.value, Is.LessThan(baseExposure));
                Assert.That(vignette.intensity.value, Is.GreaterThan(baseVignette));
            }
            finally
            {
                snapshot.Restore();
                GameplaySmokeTestReflection.InvokeIfPresent(directorObject.GetComponent<SceneAtmosphereDirector>(), "OnDisable");
                Object.DestroyImmediate(directorObject);
                Object.DestroyImmediate(volumeObject);
            }
        }

        [Test]
        public void SceneAtmosphereDirector_NeighborMemoryClueIntensifiesFogAndGrade()
        {
            RenderSettingsSnapshot snapshot = RenderSettingsSnapshot.Capture();
            GameObject directorObject = new("Memory Atmosphere Director Test");
            GameObject volumeObject = new("Memory Atmosphere Volume Test");

            try
            {
                Volume volume = volumeObject.AddComponent<Volume>();
                SceneAtmosphereDirector director = directorObject.AddComponent<SceneAtmosphereDirector>();
                director.Configure(
                    null,
                    null,
                    volume,
                    new AtmosphereFlickerLight[0],
                    new AtmosphereDressingAnchor[0]);

                Assert.That(volume.profile, Is.Not.Null);
                Assert.That(volume.profile.TryGet(out ColorAdjustments colorAdjustments), Is.True);
                Assert.That(volume.profile.TryGet(out Vignette vignette), Is.True);

                float baseFogDensity = RenderSettings.fogDensity;
                float baseExposure = colorAdjustments.postExposure.value;
                float baseVignette = vignette.intensity.value;

                PlayerFeedbackEvents.ReportNeighborMemory(
                    PlayerFeedbackEvents.NeighborMemoryClueKind.KeyStolen,
                    "Basement Key",
                    Vector3.zero,
                    0.72f,
                    3);

                Assert.That(director.CurrentStealthAtmosphereIntensity, Is.GreaterThan(0.75f));
                Assert.That(director.TargetStealthAtmosphereIntensity, Is.GreaterThan(0.75f));
                Assert.That(RenderSettings.fogDensity, Is.GreaterThan(baseFogDensity));
                Assert.That(colorAdjustments.postExposure.value, Is.LessThan(baseExposure));
                Assert.That(vignette.intensity.value, Is.GreaterThan(baseVignette));
            }
            finally
            {
                snapshot.Restore();
                GameplaySmokeTestReflection.InvokeIfPresent(directorObject.GetComponent<SceneAtmosphereDirector>(), "OnDisable");
                Object.DestroyImmediate(directorObject);
                Object.DestroyImmediate(volumeObject);
            }
        }

        [Test]
        public void SceneAtmosphereDirector_NeighborTrailInvestigationIntensifiesFogAndGrade()
        {
            RenderSettingsSnapshot snapshot = RenderSettingsSnapshot.Capture();
            GameObject directorObject = new("Trail Atmosphere Director Test");
            GameObject volumeObject = new("Trail Atmosphere Volume Test");

            try
            {
                Volume volume = volumeObject.AddComponent<Volume>();
                SceneAtmosphereDirector director = directorObject.AddComponent<SceneAtmosphereDirector>();
                director.Configure(
                    null,
                    null,
                    volume,
                    new AtmosphereFlickerLight[0],
                    new AtmosphereDressingAnchor[0]);

                Assert.That(volume.profile, Is.Not.Null);
                Assert.That(volume.profile.TryGet(out ColorAdjustments colorAdjustments), Is.True);
                Assert.That(volume.profile.TryGet(out Vignette vignette), Is.True);

                float baseFogDensity = RenderSettings.fogDensity;
                float baseExposure = colorAdjustments.postExposure.value;
                float baseVignette = vignette.intensity.value;

                PlayerFeedbackEvents.ReportNeighborInvestigation(
                    PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.FollowingTrail,
                    Vector3.zero,
                    "Basement Key Trail",
                    0.66f,
                    0.9f);

                Assert.That(director.CurrentStealthAtmosphereIntensity, Is.GreaterThan(0.85f));
                Assert.That(director.TargetStealthAtmosphereIntensity, Is.GreaterThan(0.85f));
                Assert.That(RenderSettings.fogDensity, Is.GreaterThan(baseFogDensity));
                Assert.That(colorAdjustments.postExposure.value, Is.LessThan(baseExposure));
                Assert.That(vignette.intensity.value, Is.GreaterThan(baseVignette));
            }
            finally
            {
                snapshot.Restore();
                GameplaySmokeTestReflection.InvokeIfPresent(directorObject.GetComponent<SceneAtmosphereDirector>(), "OnDisable");
                Object.DestroyImmediate(directorObject);
                Object.DestroyImmediate(volumeObject);
            }
        }

        [Test]
        public void SceneAtmosphereDirector_HeardNoiseIntensifiesFogAndGrade()
        {
            RenderSettingsSnapshot snapshot = RenderSettingsSnapshot.Capture();
            GameObject directorObject = new("Noise Atmosphere Director Test");
            GameObject volumeObject = new("Noise Atmosphere Volume Test");

            try
            {
                Volume volume = volumeObject.AddComponent<Volume>();
                SceneAtmosphereDirector director = directorObject.AddComponent<SceneAtmosphereDirector>();
                director.Configure(
                    null,
                    null,
                    volume,
                    new AtmosphereFlickerLight[0],
                    new AtmosphereDressingAnchor[0]);

                Assert.That(volume.profile, Is.Not.Null);
                Assert.That(volume.profile.TryGet(out ColorAdjustments colorAdjustments), Is.True);
                Assert.That(volume.profile.TryGet(out Vignette vignette), Is.True);

                float baseFogDensity = RenderSettings.fogDensity;
                float baseExposure = colorAdjustments.postExposure.value;
                float baseVignette = vignette.intensity.value;

                PlayerFeedbackEvents.ReportNoise(Vector3.zero, 0.58f, 6f, 0.86f, true, 1);

                Assert.That(director.CurrentStealthAtmosphereIntensity, Is.GreaterThan(0.8f));
                Assert.That(director.TargetStealthAtmosphereIntensity, Is.GreaterThan(0.8f));
                Assert.That(RenderSettings.fogDensity, Is.GreaterThan(baseFogDensity));
                Assert.That(colorAdjustments.postExposure.value, Is.LessThan(baseExposure));
                Assert.That(vignette.intensity.value, Is.GreaterThan(baseVignette));
            }
            finally
            {
                snapshot.Restore();
                GameplaySmokeTestReflection.InvokeIfPresent(directorObject.GetComponent<SceneAtmosphereDirector>(), "OnDisable");
                Object.DestroyImmediate(directorObject);
                Object.DestroyImmediate(volumeObject);
            }
        }

        [Test]
        public void AtmosphereFlickerLight_StealthLoopDangerAddsUnstableLight()
        {
            GameObject lightObject = new("Flicker Light Test");

            try
            {
                Light light = lightObject.AddComponent<Light>();
                AtmosphereFlickerLight flicker = lightObject.AddComponent<AtmosphereFlickerLight>();
                flicker.Configure(1f, 0.2f, 4f);

                Assert.That(flicker.CurrentStealthPressure, Is.Zero);
                Assert.That(flicker.EffectiveFlickerAmount, Is.EqualTo(0.2f).Within(0.001f));
                Assert.That(flicker.EffectiveFlickerSpeed, Is.EqualTo(4f).Within(0.001f));

                PlayerFeedbackEvents.ReportStealthLoop(
                    PlayerFeedbackEvents.StealthLoopPhase.Chased,
                    1f,
                    0.8f,
                    0.9f,
                    "Run or hide");

                Assert.That(flicker.CurrentStealthPressure, Is.GreaterThan(0.95f));
                Assert.That(flicker.EffectiveFlickerAmount, Is.GreaterThan(0.5f));
                Assert.That(flicker.EffectiveFlickerSpeed, Is.GreaterThan(8f));
                Assert.That(light.intensity, Is.LessThan(1f));

                PlayerFeedbackEvents.ReportStealthLoop(
                    PlayerFeedbackEvents.StealthLoopPhase.Quiet,
                    0f,
                    0f,
                    0f,
                    string.Empty);
                GameplaySmokeTestReflection.Invoke(flicker, "UpdateStealthPressure", 1f);

                Assert.That(flicker.CurrentStealthPressure, Is.Zero.Within(0.001f));
                Assert.That(flicker.EffectiveFlickerAmount, Is.EqualTo(0.2f).Within(0.001f));
                Assert.That(flicker.EffectiveFlickerSpeed, Is.EqualTo(4f).Within(0.001f));
            }
            finally
            {
                GameplaySmokeTestReflection.InvokeIfPresent(
                    lightObject.GetComponent<AtmosphereFlickerLight>(),
                    "OnDisable");
                Object.DestroyImmediate(lightObject);
            }
        }

        [Test]
        public void AtmosphereFlickerLight_CalmingHidingRecoveryEasesUnstableLight()
        {
            GameObject lightObject = new("Recovery Flicker Light Test");

            try
            {
                Light light = lightObject.AddComponent<Light>();
                AtmosphereFlickerLight flicker = lightObject.AddComponent<AtmosphereFlickerLight>();
                flicker.Configure(1f, 0.2f, 4f);

                PlayerFeedbackEvents.ReportStealthLoop(
                    PlayerFeedbackEvents.StealthLoopPhase.Chased,
                    1f,
                    0.8f,
                    0.9f,
                    "Run or hide");
                float dangerPressure = flicker.CurrentStealthPressure;

                PlayerFeedbackEvents.ReportStealthLoop(
                    PlayerFeedbackEvents.StealthLoopPhase.Hiding,
                    0.08f,
                    0f,
                    0.08f,
                    "Breathing under control.",
                    true);
                GameplaySmokeTestReflection.Invoke(flicker, "UpdateStealthPressure", 1f);

                Assert.That(dangerPressure, Is.GreaterThan(0.95f));
                Assert.That(flicker.CurrentStealthPressure, Is.LessThan(0.2f));
                Assert.That(flicker.EffectiveFlickerAmount, Is.LessThan(0.3f));
                Assert.That(light.intensity, Is.GreaterThan(0f));
            }
            finally
            {
                GameplaySmokeTestReflection.InvokeIfPresent(
                    lightObject.GetComponent<AtmosphereFlickerLight>(),
                    "OnDisable");
                Object.DestroyImmediate(lightObject);
            }
        }

        [Test]
        public void AtmosphereFlickerLight_CertainSuspicionAddsUnstableLight()
        {
            GameObject lightObject = new("Certain Flicker Light Test");

            try
            {
                Light light = lightObject.AddComponent<Light>();
                AtmosphereFlickerLight flicker = lightObject.AddComponent<AtmosphereFlickerLight>();
                flicker.Configure(1f, 0.2f, 4f);

                PlayerFeedbackEvents.ReportStealthLoop(
                    PlayerFeedbackEvents.StealthLoopPhase.Certain,
                    0.86f,
                    0f,
                    0.2f,
                    "He is locking on.");

                Assert.That(flicker.CurrentStealthPressure, Is.GreaterThan(0.8f));
                Assert.That(flicker.CurrentStealthPressure, Is.LessThan(1f));
                Assert.That(flicker.EffectiveFlickerAmount, Is.GreaterThan(0.48f));
                Assert.That(flicker.EffectiveFlickerSpeed, Is.GreaterThan(8f));
                Assert.That(light.intensity, Is.LessThan(1f));
            }
            finally
            {
                GameplaySmokeTestReflection.InvokeIfPresent(
                    lightObject.GetComponent<AtmosphereFlickerLight>(),
                    "OnDisable");
                Object.DestroyImmediate(lightObject);
            }
        }

        [Test]
        public void AtmosphereFlickerLight_NeighborMemoryClueAddsUneasyLight()
        {
            GameObject lightObject = new("Memory Flicker Light Test");

            try
            {
                Light light = lightObject.AddComponent<Light>();
                AtmosphereFlickerLight flicker = lightObject.AddComponent<AtmosphereFlickerLight>();
                flicker.Configure(1f, 0.2f, 4f);

                PlayerFeedbackEvents.ReportNeighborMemory(
                    PlayerFeedbackEvents.NeighborMemoryClueKind.GlassBroken,
                    "Kitchen Window",
                    Vector3.zero,
                    0.7f,
                    3);

                Assert.That(flicker.CurrentStealthPressure, Is.GreaterThan(0.75f));
                Assert.That(flicker.EffectiveFlickerAmount, Is.GreaterThan(0.45f));
                Assert.That(flicker.EffectiveFlickerSpeed, Is.GreaterThan(7.5f));
                Assert.That(light.intensity, Is.LessThan(1f));
            }
            finally
            {
                GameplaySmokeTestReflection.InvokeIfPresent(
                    lightObject.GetComponent<AtmosphereFlickerLight>(),
                    "OnDisable");
                Object.DestroyImmediate(lightObject);
            }
        }

        [Test]
        public void AtmosphereFlickerLight_NeighborTrailInvestigationAddsUnstableLight()
        {
            GameObject lightObject = new("Trail Flicker Light Test");

            try
            {
                Light light = lightObject.AddComponent<Light>();
                AtmosphereFlickerLight flicker = lightObject.AddComponent<AtmosphereFlickerLight>();
                flicker.Configure(1f, 0.2f, 4f);

                PlayerFeedbackEvents.ReportNeighborInvestigation(
                    PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.FollowingTrail,
                    Vector3.zero,
                    "Basement Key Trail",
                    0.66f,
                    0.9f);

                Assert.That(flicker.CurrentStealthPressure, Is.GreaterThan(0.85f));
                Assert.That(flicker.EffectiveFlickerAmount, Is.GreaterThan(0.48f));
                Assert.That(flicker.EffectiveFlickerSpeed, Is.GreaterThan(8f));
                Assert.That(light.intensity, Is.LessThan(1f));
            }
            finally
            {
                GameplaySmokeTestReflection.InvokeIfPresent(
                    lightObject.GetComponent<AtmosphereFlickerLight>(),
                    "OnDisable");
                Object.DestroyImmediate(lightObject);
            }
        }

        [Test]
        public void AtmosphereFlickerLight_HeardNoiseAddsUnstableLight()
        {
            GameObject lightObject = new("Noise Flicker Light Test");

            try
            {
                Light light = lightObject.AddComponent<Light>();
                AtmosphereFlickerLight flicker = lightObject.AddComponent<AtmosphereFlickerLight>();
                flicker.Configure(1f, 0.2f, 4f);

                PlayerFeedbackEvents.ReportNoise(Vector3.zero, 0.58f, 6f, 0.86f, true, 1);

                Assert.That(flicker.CurrentStealthPressure, Is.GreaterThan(0.8f));
                Assert.That(flicker.EffectiveFlickerAmount, Is.GreaterThan(0.45f));
                Assert.That(flicker.EffectiveFlickerSpeed, Is.GreaterThan(7.5f));
                Assert.That(light.intensity, Is.LessThan(1f));
            }
            finally
            {
                GameplaySmokeTestReflection.InvokeIfPresent(
                    lightObject.GetComponent<AtmosphereFlickerLight>(),
                    "OnDisable");
                Object.DestroyImmediate(lightObject);
            }
        }

        [Test]
        public void AtmosphereDressingAnchor_StealthLoopDangerPressurizesDecalsAndProps()
        {
            GameObject decalObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            GameObject propObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material decalMaterial = CreateTestMaterial(new Color(0.2f, 0.16f, 0.1f, 0.35f));
            Material propMaterial = CreateTestMaterial(new Color(0.36f, 0.24f, 0.13f, 1f));

            try
            {
                decalObject.name = "Dirty Decal Dressing Test";
                propObject.name = "Prop Dressing Test";
                propObject.transform.localScale = new Vector3(1.4f, 0.3f, 0.5f);

                Renderer decalRenderer = decalObject.GetComponent<Renderer>();
                Renderer propRenderer = propObject.GetComponent<Renderer>();
                decalRenderer.sharedMaterial = decalMaterial;
                propRenderer.sharedMaterial = propMaterial;

                AtmosphereDressingAnchor decal = decalObject.AddComponent<AtmosphereDressingAnchor>();
                AtmosphereDressingAnchor prop = propObject.AddComponent<AtmosphereDressingAnchor>();
                decal.Configure(AtmosphereDressingAnchor.DressingKind.DirtyDecal, 0.45f);
                prop.Configure(AtmosphereDressingAnchor.DressingKind.PropDressing, 0.5f);

                MaterialPropertyBlock block = new();
                decalRenderer.GetPropertyBlock(block);
                Color calmDecal = block.GetColor("_BaseColor");
                Vector3 calmPropScale = propObject.transform.localScale;

                PlayerFeedbackEvents.ReportStealthLoop(
                    PlayerFeedbackEvents.StealthLoopPhase.Chased,
                    1f,
                    0.7f,
                    0.9f,
                    "Run or hide");

                decalRenderer.GetPropertyBlock(block);
                Color dangerDecal = block.GetColor("_BaseColor");

                Assert.That(decal.CurrentStealthPressure, Is.GreaterThan(0.95f));
                Assert.That(prop.CurrentStealthPressure, Is.GreaterThan(0.95f));
                Assert.That(decal.EffectiveIntensity, Is.GreaterThan(0.65f));
                Assert.That(dangerDecal.a, Is.GreaterThan(calmDecal.a));
                Assert.That(dangerDecal.r, Is.LessThan(calmDecal.r));
                Assert.That(propObject.transform.localScale.x, Is.GreaterThan(calmPropScale.x));

                PlayerFeedbackEvents.ReportStealthLoop(
                    PlayerFeedbackEvents.StealthLoopPhase.Quiet,
                    0f,
                    0f,
                    0f,
                    string.Empty);
                GameplaySmokeTestReflection.Invoke(decal, "UpdateStealthDressing", 1f);
                GameplaySmokeTestReflection.Invoke(prop, "UpdateStealthDressing", 1f);

                Assert.That(decal.CurrentStealthPressure, Is.Zero.Within(0.001f));
                Assert.That(prop.CurrentStealthPressure, Is.Zero.Within(0.001f));
                Assert.That(propObject.transform.localScale.x, Is.EqualTo(calmPropScale.x).Within(0.001f));
            }
            finally
            {
                GameplaySmokeTestReflection.InvokeIfPresent(
                    decalObject.GetComponent<AtmosphereDressingAnchor>(),
                    "OnDisable");
                GameplaySmokeTestReflection.InvokeIfPresent(
                    propObject.GetComponent<AtmosphereDressingAnchor>(),
                    "OnDisable");
                Object.DestroyImmediate(decalObject);
                Object.DestroyImmediate(propObject);
                Object.DestroyImmediate(decalMaterial);
                Object.DestroyImmediate(propMaterial);
            }
        }

        [Test]
        public void AtmosphereDressingAnchor_CalmingHidingRecoveryEasesDecalsAndProps()
        {
            GameObject decalObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            GameObject propObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material decalMaterial = CreateTestMaterial(new Color(0.18f, 0.14f, 0.09f, 0.3f));
            Material propMaterial = CreateTestMaterial(new Color(0.34f, 0.23f, 0.14f, 1f));

            try
            {
                Renderer decalRenderer = decalObject.GetComponent<Renderer>();
                Renderer propRenderer = propObject.GetComponent<Renderer>();
                decalRenderer.sharedMaterial = decalMaterial;
                propRenderer.sharedMaterial = propMaterial;

                AtmosphereDressingAnchor decal = decalObject.AddComponent<AtmosphereDressingAnchor>();
                AtmosphereDressingAnchor prop = propObject.AddComponent<AtmosphereDressingAnchor>();
                decal.Configure(AtmosphereDressingAnchor.DressingKind.DirtyDecal, 0.4f);
                prop.Configure(AtmosphereDressingAnchor.DressingKind.PropDressing, 0.48f);

                PlayerFeedbackEvents.ReportStealthLoop(
                    PlayerFeedbackEvents.StealthLoopPhase.Chased,
                    1f,
                    0.8f,
                    0.9f,
                    "Run or hide");
                float dangerDecalPressure = decal.CurrentStealthPressure;
                float dangerPropPressure = prop.CurrentStealthPressure;

                PlayerFeedbackEvents.ReportStealthLoop(
                    PlayerFeedbackEvents.StealthLoopPhase.Hiding,
                    0.08f,
                    0f,
                    0.08f,
                    "Breathing under control.",
                    true);
                GameplaySmokeTestReflection.Invoke(decal, "UpdateStealthDressing", 1f);
                GameplaySmokeTestReflection.Invoke(prop, "UpdateStealthDressing", 1f);

                Assert.That(dangerDecalPressure, Is.GreaterThan(0.95f));
                Assert.That(dangerPropPressure, Is.GreaterThan(0.95f));
                Assert.That(decal.CurrentStealthPressure, Is.LessThan(0.15f));
                Assert.That(prop.CurrentStealthPressure, Is.LessThan(0.15f));
            }
            finally
            {
                GameplaySmokeTestReflection.InvokeIfPresent(
                    decalObject.GetComponent<AtmosphereDressingAnchor>(),
                    "OnDisable");
                GameplaySmokeTestReflection.InvokeIfPresent(
                    propObject.GetComponent<AtmosphereDressingAnchor>(),
                    "OnDisable");
                Object.DestroyImmediate(decalObject);
                Object.DestroyImmediate(propObject);
                Object.DestroyImmediate(decalMaterial);
                Object.DestroyImmediate(propMaterial);
            }
        }

        [Test]
        public void AtmosphereDressingAnchor_CertainSuspicionPressurizesDecalsAndProps()
        {
            GameObject decalObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            GameObject propObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material decalMaterial = CreateTestMaterial(new Color(0.18f, 0.14f, 0.09f, 0.3f));
            Material propMaterial = CreateTestMaterial(new Color(0.34f, 0.23f, 0.14f, 1f));

            try
            {
                decalObject.name = "Certain Dirty Decal Dressing Test";
                propObject.name = "Certain Prop Dressing Test";
                propObject.transform.localScale = new Vector3(1.1f, 0.25f, 0.45f);

                Renderer decalRenderer = decalObject.GetComponent<Renderer>();
                Renderer propRenderer = propObject.GetComponent<Renderer>();
                decalRenderer.sharedMaterial = decalMaterial;
                propRenderer.sharedMaterial = propMaterial;

                AtmosphereDressingAnchor decal = decalObject.AddComponent<AtmosphereDressingAnchor>();
                AtmosphereDressingAnchor prop = propObject.AddComponent<AtmosphereDressingAnchor>();
                decal.Configure(AtmosphereDressingAnchor.DressingKind.DirtyDecal, 0.4f);
                prop.Configure(AtmosphereDressingAnchor.DressingKind.PropDressing, 0.48f);

                MaterialPropertyBlock block = new();
                decalRenderer.GetPropertyBlock(block);
                Color calmDecal = block.GetColor("_BaseColor");
                Vector3 calmPropScale = propObject.transform.localScale;

                PlayerFeedbackEvents.ReportStealthLoop(
                    PlayerFeedbackEvents.StealthLoopPhase.Certain,
                    0.86f,
                    0f,
                    0.2f,
                    "He is locking on.");

                decalRenderer.GetPropertyBlock(block);
                Color certainDecal = block.GetColor("_BaseColor");

                Assert.That(decal.CurrentStealthPressure, Is.GreaterThan(0.8f));
                Assert.That(prop.CurrentStealthPressure, Is.GreaterThan(0.8f));
                Assert.That(decal.CurrentStealthPressure, Is.LessThan(1f));
                Assert.That(decal.EffectiveIntensity, Is.GreaterThan(0.58f));
                Assert.That(certainDecal.a, Is.GreaterThan(calmDecal.a));
                Assert.That(certainDecal.r, Is.LessThan(calmDecal.r));
                Assert.That(propObject.transform.localScale.x, Is.GreaterThan(calmPropScale.x));
            }
            finally
            {
                GameplaySmokeTestReflection.InvokeIfPresent(
                    decalObject.GetComponent<AtmosphereDressingAnchor>(),
                    "OnDisable");
                GameplaySmokeTestReflection.InvokeIfPresent(
                    propObject.GetComponent<AtmosphereDressingAnchor>(),
                    "OnDisable");
                Object.DestroyImmediate(decalObject);
                Object.DestroyImmediate(propObject);
                Object.DestroyImmediate(decalMaterial);
                Object.DestroyImmediate(propMaterial);
            }
        }

        [Test]
        public void AtmosphereDressingAnchor_HeardNoisePressurizesDecalsAndProps()
        {
            GameObject decalObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            GameObject propObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material decalMaterial = CreateTestMaterial(new Color(0.18f, 0.14f, 0.09f, 0.3f));
            Material propMaterial = CreateTestMaterial(new Color(0.34f, 0.23f, 0.14f, 1f));

            try
            {
                decalObject.name = "Noise Dirty Decal Dressing Test";
                propObject.name = "Noise Prop Dressing Test";
                propObject.transform.localScale = new Vector3(1.1f, 0.25f, 0.45f);

                Renderer decalRenderer = decalObject.GetComponent<Renderer>();
                Renderer propRenderer = propObject.GetComponent<Renderer>();
                decalRenderer.sharedMaterial = decalMaterial;
                propRenderer.sharedMaterial = propMaterial;

                AtmosphereDressingAnchor decal = decalObject.AddComponent<AtmosphereDressingAnchor>();
                AtmosphereDressingAnchor prop = propObject.AddComponent<AtmosphereDressingAnchor>();
                decal.Configure(AtmosphereDressingAnchor.DressingKind.DirtyDecal, 0.4f);
                prop.Configure(AtmosphereDressingAnchor.DressingKind.PropDressing, 0.48f);

                MaterialPropertyBlock block = new();
                decalRenderer.GetPropertyBlock(block);
                Color calmDecal = block.GetColor("_BaseColor");
                Vector3 calmPropScale = propObject.transform.localScale;

                PlayerFeedbackEvents.ReportNoise(Vector3.zero, 0.58f, 6f, 0.86f, true, 1);

                decalRenderer.GetPropertyBlock(block);
                Color noiseDecal = block.GetColor("_BaseColor");

                Assert.That(decal.CurrentStealthPressure, Is.GreaterThan(0.8f));
                Assert.That(prop.CurrentStealthPressure, Is.GreaterThan(0.8f));
                Assert.That(decal.EffectiveIntensity, Is.GreaterThan(0.58f));
                Assert.That(noiseDecal.a, Is.GreaterThan(calmDecal.a));
                Assert.That(noiseDecal.r, Is.LessThan(calmDecal.r));
                Assert.That(propObject.transform.localScale.x, Is.GreaterThan(calmPropScale.x));
            }
            finally
            {
                GameplaySmokeTestReflection.InvokeIfPresent(
                    decalObject.GetComponent<AtmosphereDressingAnchor>(),
                    "OnDisable");
                GameplaySmokeTestReflection.InvokeIfPresent(
                    propObject.GetComponent<AtmosphereDressingAnchor>(),
                    "OnDisable");
                Object.DestroyImmediate(decalObject);
                Object.DestroyImmediate(propObject);
                Object.DestroyImmediate(decalMaterial);
                Object.DestroyImmediate(propMaterial);
            }
        }

        [Test]
        public void AtmosphereDressingAnchor_NeighborMemoryCluePressurizesDecalsAndProps()
        {
            GameObject decalObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            GameObject propObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material decalMaterial = CreateTestMaterial(new Color(0.18f, 0.14f, 0.09f, 0.3f));
            Material propMaterial = CreateTestMaterial(new Color(0.34f, 0.23f, 0.14f, 1f));

            try
            {
                decalObject.name = "Memory Dirty Decal Dressing Test";
                propObject.name = "Memory Prop Dressing Test";
                propObject.transform.localScale = new Vector3(1.1f, 0.25f, 0.45f);

                Renderer decalRenderer = decalObject.GetComponent<Renderer>();
                Renderer propRenderer = propObject.GetComponent<Renderer>();
                decalRenderer.sharedMaterial = decalMaterial;
                propRenderer.sharedMaterial = propMaterial;

                AtmosphereDressingAnchor decal = decalObject.AddComponent<AtmosphereDressingAnchor>();
                AtmosphereDressingAnchor prop = propObject.AddComponent<AtmosphereDressingAnchor>();
                decal.Configure(AtmosphereDressingAnchor.DressingKind.DirtyDecal, 0.4f);
                prop.Configure(AtmosphereDressingAnchor.DressingKind.PropDressing, 0.48f);

                MaterialPropertyBlock block = new();
                decalRenderer.GetPropertyBlock(block);
                Color calmDecal = block.GetColor("_BaseColor");
                Vector3 calmPropScale = propObject.transform.localScale;

                PlayerFeedbackEvents.ReportNeighborMemory(
                    PlayerFeedbackEvents.NeighborMemoryClueKind.KeyStolen,
                    "Basement Key",
                    Vector3.zero,
                    0.74f,
                    3);

                decalRenderer.GetPropertyBlock(block);
                Color memoryDecal = block.GetColor("_BaseColor");

                Assert.That(decal.CurrentStealthPressure, Is.GreaterThan(0.8f));
                Assert.That(prop.CurrentStealthPressure, Is.GreaterThan(0.8f));
                Assert.That(decal.EffectiveIntensity, Is.GreaterThan(0.58f));
                Assert.That(memoryDecal.a, Is.GreaterThan(calmDecal.a));
                Assert.That(memoryDecal.r, Is.LessThan(calmDecal.r));
                Assert.That(propObject.transform.localScale.x, Is.GreaterThan(calmPropScale.x));
            }
            finally
            {
                GameplaySmokeTestReflection.InvokeIfPresent(
                    decalObject.GetComponent<AtmosphereDressingAnchor>(),
                    "OnDisable");
                GameplaySmokeTestReflection.InvokeIfPresent(
                    propObject.GetComponent<AtmosphereDressingAnchor>(),
                    "OnDisable");
                Object.DestroyImmediate(decalObject);
                Object.DestroyImmediate(propObject);
                Object.DestroyImmediate(decalMaterial);
                Object.DestroyImmediate(propMaterial);
            }
        }

        [Test]
        public void AtmosphereDressingAnchor_NeighborTrailInvestigationPressurizesDecalsAndProps()
        {
            GameObject decalObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            GameObject propObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material decalMaterial = CreateTestMaterial(new Color(0.18f, 0.14f, 0.09f, 0.3f));
            Material propMaterial = CreateTestMaterial(new Color(0.34f, 0.23f, 0.14f, 1f));

            try
            {
                decalObject.name = "Trail Dirty Decal Dressing Test";
                propObject.name = "Trail Prop Dressing Test";
                propObject.transform.localScale = new Vector3(1.1f, 0.25f, 0.45f);

                Renderer decalRenderer = decalObject.GetComponent<Renderer>();
                Renderer propRenderer = propObject.GetComponent<Renderer>();
                decalRenderer.sharedMaterial = decalMaterial;
                propRenderer.sharedMaterial = propMaterial;

                AtmosphereDressingAnchor decal = decalObject.AddComponent<AtmosphereDressingAnchor>();
                AtmosphereDressingAnchor prop = propObject.AddComponent<AtmosphereDressingAnchor>();
                decal.Configure(AtmosphereDressingAnchor.DressingKind.DirtyDecal, 0.4f);
                prop.Configure(AtmosphereDressingAnchor.DressingKind.PropDressing, 0.48f);

                MaterialPropertyBlock block = new();
                decalRenderer.GetPropertyBlock(block);
                Color calmDecal = block.GetColor("_BaseColor");
                Vector3 calmPropScale = propObject.transform.localScale;

                PlayerFeedbackEvents.ReportNeighborInvestigation(
                    PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.FollowingTrail,
                    Vector3.zero,
                    "Basement Key Trail",
                    0.66f,
                    0.9f);

                decalRenderer.GetPropertyBlock(block);
                Color trailDecal = block.GetColor("_BaseColor");

                Assert.That(decal.CurrentStealthPressure, Is.GreaterThan(0.85f));
                Assert.That(prop.CurrentStealthPressure, Is.GreaterThan(0.85f));
                Assert.That(decal.EffectiveIntensity, Is.GreaterThan(0.58f));
                Assert.That(trailDecal.a, Is.GreaterThan(calmDecal.a));
                Assert.That(trailDecal.r, Is.LessThan(calmDecal.r));
                Assert.That(propObject.transform.localScale.x, Is.GreaterThan(calmPropScale.x));
            }
            finally
            {
                GameplaySmokeTestReflection.InvokeIfPresent(
                    decalObject.GetComponent<AtmosphereDressingAnchor>(),
                    "OnDisable");
                GameplaySmokeTestReflection.InvokeIfPresent(
                    propObject.GetComponent<AtmosphereDressingAnchor>(),
                    "OnDisable");
                Object.DestroyImmediate(decalObject);
                Object.DestroyImmediate(propObject);
                Object.DestroyImmediate(decalMaterial);
                Object.DestroyImmediate(propMaterial);
            }
        }

        [Test]
        public void MakeActiveScenePlayable_IsIdempotent()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ScenePlayableSetupUtility.MakeActiveScenePlayable(false);
            ScenePlayableSetupResult secondRun = ScenePlayableSetupUtility.MakeActiveScenePlayable(false);

            Assert.That(secondRun.CreatedObjectCount, Is.Zero);
            Assert.That(secondRun.AddedComponentCount, Is.Zero);
            Assert.That(secondRun.CreatedPlayer, Is.False);
            Assert.That(secondRun.AddedPlayerDeathController, Is.False);
            Assert.That(secondRun.AddedPlayerKeyRing, Is.False);
            Assert.That(secondRun.AddedPlayerHidingState, Is.False);
            Assert.That(secondRun.AddedOnboardingDirector, Is.False);
            Assert.That(secondRun.AddedPauseMenu, Is.False);
            Assert.That(secondRun.UpdatedPlayerReferences, Is.False);
            Assert.That(secondRun.CreatedNeighbor, Is.False);
            Assert.That(secondRun.CreatedObjectiveTracker, Is.False);
            Assert.That(secondRun.CreatedStartCheckpoint, Is.False);
            Assert.That(secondRun.UpdatedStartCheckpoint, Is.False);
            Assert.That(secondRun.CreatedNavMeshSurface, Is.False);
            Assert.That(secondRun.CreatedAmbienceManager, Is.False);
            Assert.That(secondRun.CreatedAwarenessHud, Is.False);
            Assert.That(secondRun.CreatedInventoryHud, Is.False);
            Assert.That(secondRun.CreatedEventSystem, Is.False);
            Assert.That(secondRun.CreatedDirectionalLight, Is.False);
            Assert.That(secondRun.CreatedMoonLight, Is.False);
            Assert.That(secondRun.CreatedDayNightCycle, Is.False);
            Assert.That(secondRun.UpdatedDayNightCycle, Is.False);
            Assert.That(secondRun.CreatedAtmosphereDirector, Is.False);
            Assert.That(secondRun.CreatedAtmosphereVolume, Is.False);
            Assert.That(secondRun.CreatedFlickerLight, Is.False);
            Assert.That(secondRun.CreatedAtmosphereDressing, Is.False);
            Assert.That(secondRun.CreatedVegetationMaterialGuard, Is.False);
            Assert.That(CountInScene<PlayerController>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerDeathController>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerKeyRing>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerHidingState>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerOnboardingDirector>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerPauseMenu>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<NeighborBrain>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<CoreLoopObjectiveTracker>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerRespawnCheckpoint>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<NavMeshSurface>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<AmbienceManager>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerAwarenessHudView>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerInventoryHudView>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<EventSystem>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<DayNightCycle>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<SceneAtmosphereDirector>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<VegetationMaterialGuard>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<AtmosphereFlickerLight>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<AtmosphereDressingAnchor>(scene), Is.EqualTo(4));
        }

        private static int CountInScene<T>(Scene scene) where T : Component
        {
            int count = 0;
            T[] components = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].gameObject.scene == scene)
                {
                    count++;
                }
            }

            return count;
        }

        private static Material CreateTestMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color");
            Material material = new(shader);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            return material;
        }

        private readonly struct RenderSettingsSnapshot
        {
            private readonly bool fog;
            private readonly Color fogColor;
            private readonly float fogDensity;
            private readonly AmbientMode ambientMode;
            private readonly Color ambientLight;
            private readonly Light sun;
            private readonly Material skybox;

            private RenderSettingsSnapshot(
                bool fog,
                Color fogColor,
                float fogDensity,
                AmbientMode ambientMode,
                Color ambientLight,
                Light sun,
                Material skybox)
            {
                this.fog = fog;
                this.fogColor = fogColor;
                this.fogDensity = fogDensity;
                this.ambientMode = ambientMode;
                this.ambientLight = ambientLight;
                this.sun = sun;
                this.skybox = skybox;
            }

            public static RenderSettingsSnapshot Capture()
            {
                return new RenderSettingsSnapshot(
                    RenderSettings.fog,
                    RenderSettings.fogColor,
                    RenderSettings.fogDensity,
                    RenderSettings.ambientMode,
                    RenderSettings.ambientLight,
                    RenderSettings.sun,
                    RenderSettings.skybox);
            }

            public void Restore()
            {
                RenderSettings.fog = fog;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogDensity = fogDensity;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambientLight;
                RenderSettings.sun = sun;
                RenderSettings.skybox = skybox;
            }
        }
    }
}
