using System.Collections.Generic;
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
        public void SceneAtmosphereBootstrapper_DressingMaterialsUseProceduralSurfaceTextures()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Atmosphere Texture Floor";
            floor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            floor.transform.localScale = new Vector3(8f, 0.2f, 8f);
            SceneManager.MoveGameObjectToScene(floor, scene);

            SceneAtmosphereBootstrapper.EnsureAtmosphereForScene(scene, true);

            AtmosphereDressingAnchor[] anchors = FindDressingAnchorsInScene(scene);
            Assert.That(anchors, Has.Length.EqualTo(4));

            bool sawDirtyDecalTexture = false;
            bool sawPropTexture = false;
            for (int i = 0; i < anchors.Length; i++)
            {
                Renderer renderer = anchors[i].GetComponent<Renderer>();
                Assert.That(renderer, Is.Not.Null);
                Material material = renderer.sharedMaterial;
                Texture texture = GetMainTexture(material);
                Assert.That(texture, Is.Not.Null);

                if (anchors[i].Kind == AtmosphereDressingAnchor.DressingKind.DirtyDecal)
                {
                    sawDirtyDecalTexture = true;
                    Assert.That(TextureHasAlphaVariation(texture), Is.True);
                }
                else if (anchors[i].Kind == AtmosphereDressingAnchor.DressingKind.PropDressing)
                {
                    sawPropTexture = true;
                    Assert.That(TextureHasColorVariation(texture), Is.True);
                }
            }

            Assert.That(sawDirtyDecalTexture, Is.True);
            Assert.That(sawPropTexture, Is.True);
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
        public void SceneAtmosphereDirector_PostChasePressureIntensifiesFogAndGrade()
        {
            RenderSettingsSnapshot snapshot = RenderSettingsSnapshot.Capture();
            GameObject directorObject = new("Post Chase Atmosphere Director Test");
            GameObject volumeObject = new("Post Chase Atmosphere Volume Test");

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
                    PlayerFeedbackEvents.StealthLoopPhase.PostChase,
                    0.78f,
                    0.62f,
                    0.24f,
                    "Stay hidden. He is checking the area.");

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
        public void SceneAtmosphereDirector_CalmingPostChaseLetsFogAndGradeBreathe()
        {
            RenderSettingsSnapshot snapshot = RenderSettingsSnapshot.Capture();
            GameObject directorObject = new("Calming Post Chase Atmosphere Director Test");
            GameObject volumeObject = new("Calming Post Chase Atmosphere Volume Test");

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
                    PlayerFeedbackEvents.StealthLoopPhase.PostChase,
                    0.9f,
                    0.4f,
                    0.65f,
                    "Stay hidden. He is checking the area.");
                float dangerIntensity = director.CurrentStealthAtmosphereIntensity;

                PlayerFeedbackEvents.ReportStealthLoop(
                    PlayerFeedbackEvents.StealthLoopPhase.PostChase,
                    0.9f,
                    0f,
                    0.1f,
                    "He lost your trail. Stay quiet.",
                    true);
                GameplaySmokeTestReflection.Invoke(director, "UpdateStealthAtmosphere", 1f);

                Assert.That(director.TargetStealthAtmosphereIntensity, Is.LessThan(0.25f));
                Assert.That(director.CurrentStealthAtmosphereIntensity, Is.LessThan(dangerIntensity));
                Assert.That(director.CurrentStealthAtmosphereIntensity, Is.LessThan(0.25f));
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
        public void SceneAtmosphereDirector_CompromisedHidingIntensifiesFogAndGrade()
        {
            RenderSettingsSnapshot snapshot = RenderSettingsSnapshot.Capture();
            GameObject directorObject = new("Found Hiding Atmosphere Director Test");
            GameObject volumeObject = new("Found Hiding Atmosphere Volume Test");

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

                Assert.That(volume.profile.TryGet(out ColorAdjustments colorAdjustments), Is.True);
                Assert.That(volume.profile.TryGet(out Vignette vignette), Is.True);
                float baseFogDensity = RenderSettings.fogDensity;
                float baseExposure = colorAdjustments.postExposure.value;
                float baseVignette = vignette.intensity.value;

                PlayerFeedbackEvents.ReportStealthLoop(
                    PlayerFeedbackEvents.StealthLoopPhase.Hiding,
                    1f,
                    0f,
                    1f,
                    "He found your hiding spot.");

                Assert.That(director.CurrentStealthAtmosphereIntensity, Is.GreaterThan(0.95f));
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
        public void AtmosphereResponders_SevereMemoryCluesLingerLongerThanDoorClues()
        {
            GameObject directorObject = new("Memory Hold Director Test");
            GameObject flickerObject = new("Memory Hold Flicker Test");
            GameObject dressingObject = new("Memory Hold Dressing Test");

            try
            {
                SceneAtmosphereDirector director = directorObject.AddComponent<SceneAtmosphereDirector>();
                flickerObject.AddComponent<Light>();
                AtmosphereFlickerLight flicker = flickerObject.AddComponent<AtmosphereFlickerLight>();
                AtmosphereDressingAnchor dressing = dressingObject.AddComponent<AtmosphereDressingAnchor>();

                PlayerFeedbackEvents.NeighborMemoryFeedback openedDoor = new(
                    PlayerFeedbackEvents.NeighborMemoryClueKind.DoorOpened,
                    "Front Door",
                    Vector3.zero,
                    0.38f,
                    1);
                PlayerFeedbackEvents.NeighborMemoryFeedback stolenKey = new(
                    PlayerFeedbackEvents.NeighborMemoryClueKind.KeyStolen,
                    "Basement Key",
                    Vector3.zero,
                    0.38f,
                    1);
                PlayerFeedbackEvents.NeighborMemoryFeedback stackedDoor = new(
                    PlayerFeedbackEvents.NeighborMemoryClueKind.DoorOpened,
                    "Front Door",
                    Vector3.zero,
                    0.38f,
                    4);

                float directorDoorHold = GameplaySmokeTestReflection.InvokeResult<float>(
                    director,
                    "GetMemoryAtmosphereHoldDuration",
                    openedDoor);
                float directorKeyHold = GameplaySmokeTestReflection.InvokeResult<float>(
                    director,
                    "GetMemoryAtmosphereHoldDuration",
                    stolenKey);
                float directorStackedHold = GameplaySmokeTestReflection.InvokeResult<float>(
                    director,
                    "GetMemoryAtmosphereHoldDuration",
                    stackedDoor);

                Assert.That(directorKeyHold, Is.GreaterThan(directorDoorHold));
                Assert.That(directorStackedHold, Is.GreaterThan(directorDoorHold));

                float flickerDoorHold = GameplaySmokeTestReflection.InvokeResult<float>(
                    flicker,
                    "GetMemoryFlickerHoldDuration",
                    openedDoor);
                float flickerKeyHold = GameplaySmokeTestReflection.InvokeResult<float>(
                    flicker,
                    "GetMemoryFlickerHoldDuration",
                    stolenKey);

                Assert.That(flickerKeyHold, Is.GreaterThan(flickerDoorHold));

                float dressingDoorHold = GameplaySmokeTestReflection.InvokeResult<float>(
                    dressing,
                    "GetMemoryHoldDuration",
                    openedDoor);
                float dressingKeyHold = GameplaySmokeTestReflection.InvokeResult<float>(
                    dressing,
                    "GetMemoryHoldDuration",
                    stolenKey);

                Assert.That(dressingKeyHold, Is.GreaterThan(dressingDoorHold));
            }
            finally
            {
                GameplaySmokeTestReflection.InvokeIfPresent(
                    directorObject.GetComponent<SceneAtmosphereDirector>(),
                    "OnDisable");
                GameplaySmokeTestReflection.InvokeIfPresent(
                    flickerObject.GetComponent<AtmosphereFlickerLight>(),
                    "OnDisable");
                GameplaySmokeTestReflection.InvokeIfPresent(
                    dressingObject.GetComponent<AtmosphereDressingAnchor>(),
                    "OnDisable");
                Object.DestroyImmediate(directorObject);
                Object.DestroyImmediate(flickerObject);
                Object.DestroyImmediate(dressingObject);
            }
        }

        [Test]
        public void SceneAtmosphereDirector_StealthPressureDipsSunAndBoostsMoon()
        {
            RenderSettingsSnapshot snapshot = RenderSettingsSnapshot.Capture();
            GameObject directorObject = new("Danger Lighting Director Test");
            GameObject volumeObject = new("Danger Lighting Volume Test");
            GameObject sunObject = new("Danger Lighting Sun Test");
            GameObject moonObject = new("Danger Lighting Moon Test");

            try
            {
                Light sun = sunObject.AddComponent<Light>();
                sun.intensity = 1.2f;
                Light moon = moonObject.AddComponent<Light>();
                moon.intensity = 0.05f;
                Volume volume = volumeObject.AddComponent<Volume>();
                SceneAtmosphereDirector director = directorObject.AddComponent<SceneAtmosphereDirector>();
                director.Configure(
                    sun,
                    moon,
                    volume,
                    new AtmosphereFlickerLight[0],
                    new AtmosphereDressingAnchor[0]);

                float calmSunIntensity = sun.intensity;
                float calmMoonIntensity = moon.intensity;

                Assert.That(calmSunIntensity, Is.GreaterThan(0f));
                Assert.That(calmSunIntensity, Is.LessThanOrEqualTo(0.95f));
                Assert.That(calmMoonIntensity, Is.GreaterThanOrEqualTo(0.18f));

                PlayerFeedbackEvents.ReportStealthLoop(
                    PlayerFeedbackEvents.StealthLoopPhase.Chased,
                    1f,
                    0f,
                    1f,
                    "Run or hide");

                Assert.That(director.CurrentStealthAtmosphereIntensity, Is.EqualTo(1f).Within(0.001f));
                Assert.That(sun.intensity, Is.LessThan(calmSunIntensity));
                Assert.That(moon.intensity, Is.GreaterThan(calmMoonIntensity));

                float dangerSunIntensity = sun.intensity;
                float dangerMoonIntensity = moon.intensity;
                director.ApplyAtmosphere();
                director.ApplyAtmosphere();

                Assert.That(sun.intensity, Is.EqualTo(dangerSunIntensity).Within(0.001f));
                Assert.That(moon.intensity, Is.EqualTo(dangerMoonIntensity).Within(0.001f));
            }
            finally
            {
                snapshot.Restore();
                GameplaySmokeTestReflection.InvokeIfPresent(directorObject.GetComponent<SceneAtmosphereDirector>(), "OnDisable");
                Object.DestroyImmediate(directorObject);
                Object.DestroyImmediate(volumeObject);
                Object.DestroyImmediate(sunObject);
                Object.DestroyImmediate(moonObject);
            }
        }

        [Test]
        public void SceneAtmosphereDirector_NeighborTrailSearchIntensifiesFogAndGrade()
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
                    PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Searching,
                    Vector3.zero,
                    "Basement Key Trail",
                    0.66f,
                    0.9f,
                    true);

                Assert.That(director.CurrentStealthAtmosphereIntensity, Is.GreaterThan(0.85f));
                Assert.That(director.TargetStealthAtmosphereIntensity, Is.GreaterThan(0.85f));
                Assert.That(RenderSettings.fogDensity, Is.GreaterThan(baseFogDensity));
                Assert.That(colorAdjustments.postExposure.value, Is.LessThan(baseExposure));
                Assert.That(vignette.intensity.value, Is.GreaterThan(baseVignette));

                float trailIntensity = director.CurrentStealthAtmosphereIntensity;
                PlayerFeedbackEvents.ReportNeighborInvestigation(
                    PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Returning,
                    Vector3.zero,
                    "Basement Key Trail",
                    0.28f,
                    0.45f,
                    true);
                GameplaySmokeTestReflection.Invoke(director, "UpdateStealthAtmosphere", 0.75f);

                Assert.That(director.TargetStealthAtmosphereIntensity, Is.LessThan(0.25f));
                Assert.That(director.CurrentStealthAtmosphereIntensity, Is.LessThan(trailIntensity));
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
        public void SceneAtmosphereDirector_ResolvedNoiseInvestigationLeavesLowSettlingPulse()
        {
            RenderSettingsSnapshot snapshot = RenderSettingsSnapshot.Capture();
            GameObject directorObject = new("Resolved Noise Atmosphere Director Test");
            GameObject volumeObject = new("Resolved Noise Atmosphere Volume Test");

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

                PlayerFeedbackEvents.ReportNeighborInvestigation(
                    PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Searching,
                    Vector3.zero,
                    "Noise Source",
                    0.56f,
                    0.65f,
                    false);
                float searchIntensity = director.CurrentStealthAtmosphereIntensity;

                PlayerFeedbackEvents.ReportNeighborInvestigation(
                    PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Returning,
                    Vector3.zero,
                    "Noise Source",
                    0.16f,
                    0.28f,
                    false);
                GameplaySmokeTestReflection.Invoke(director, "UpdateStealthAtmosphere", 0.75f);

                Assert.That(director.TargetStealthAtmosphereIntensity, Is.InRange(0.08f, 0.14f));
                Assert.That(director.CurrentStealthAtmosphereIntensity, Is.LessThan(searchIntensity));
                Assert.That(director.CurrentStealthAtmosphereIntensity, Is.LessThan(0.22f));
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

                PlayerFeedbackEvents.NoiseFeedback singleListenerNoise = new(
                    Vector3.zero,
                    0.48f,
                    6f,
                    0.64f,
                    true,
                    1);
                PlayerFeedbackEvents.NoiseFeedback multiListenerNoise = new(
                    Vector3.zero,
                    0.48f,
                    6f,
                    0.64f,
                    true,
                    3);
                float singleListenerIntensity = GameplaySmokeTestReflection.InvokeResult<float>(
                    director,
                    "GetNoiseAtmosphereIntensity",
                    singleListenerNoise);
                float multiListenerIntensity = GameplaySmokeTestReflection.InvokeResult<float>(
                    director,
                    "GetNoiseAtmosphereIntensity",
                    multiListenerNoise);

                Assert.That(multiListenerIntensity, Is.GreaterThan(singleListenerIntensity));

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
        public void AtmosphereFlickerLight_CompromisedHidingAddsUnstableLight()
        {
            GameObject lightObject = new("Found Hiding Flicker Light Test");

            try
            {
                Light light = lightObject.AddComponent<Light>();
                AtmosphereFlickerLight flicker = lightObject.AddComponent<AtmosphereFlickerLight>();
                flicker.Configure(1f, 0.2f, 4f);

                PlayerFeedbackEvents.ReportStealthLoop(
                    PlayerFeedbackEvents.StealthLoopPhase.Hiding,
                    1f,
                    0f,
                    1f,
                    "He found your hiding spot.");

                Assert.That(flicker.CurrentStealthPressure, Is.GreaterThan(0.95f));
                Assert.That(flicker.EffectiveFlickerAmount, Is.GreaterThan(0.5f));
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
        public void AtmosphereFlickerLight_NeighborTrailSearchAddsUnstableLight()
        {
            GameObject lightObject = new("Trail Flicker Light Test");

            try
            {
                Light light = lightObject.AddComponent<Light>();
                AtmosphereFlickerLight flicker = lightObject.AddComponent<AtmosphereFlickerLight>();
                flicker.Configure(1f, 0.2f, 4f);

                PlayerFeedbackEvents.ReportNeighborInvestigation(
                    PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Searching,
                    Vector3.zero,
                    "Basement Key Trail",
                    0.66f,
                    0.9f,
                    true);

                Assert.That(flicker.CurrentStealthPressure, Is.GreaterThan(0.85f));
                Assert.That(flicker.EffectiveFlickerAmount, Is.GreaterThan(0.48f));
                Assert.That(flicker.EffectiveFlickerSpeed, Is.GreaterThan(8f));
                Assert.That(light.intensity, Is.LessThan(1f));

                float trailPressure = flicker.CurrentStealthPressure;
                PlayerFeedbackEvents.ReportNeighborInvestigation(
                    PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Abandoned,
                    Vector3.zero,
                    "Basement Key Trail",
                    0.28f,
                    0.45f,
                    true);
                GameplaySmokeTestReflection.Invoke(flicker, "UpdateStealthPressure", 0.75f);

                Assert.That(flicker.CurrentStealthPressure, Is.LessThan(trailPressure));
                Assert.That(flicker.CurrentStealthPressure, Is.LessThan(0.2f));
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
        public void AtmosphereFlickerLight_ResolvedNoiseInvestigationKeepsLowUneasyLight()
        {
            GameObject lightObject = new("Resolved Noise Flicker Light Test");

            try
            {
                Light light = lightObject.AddComponent<Light>();
                AtmosphereFlickerLight flicker = lightObject.AddComponent<AtmosphereFlickerLight>();
                flicker.Configure(1f, 0.2f, 4f);

                PlayerFeedbackEvents.ReportNeighborInvestigation(
                    PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Searching,
                    Vector3.zero,
                    "Noise Source",
                    0.56f,
                    0.65f,
                    false);

                Assert.That(flicker.CurrentStealthPressure, Is.GreaterThan(0.5f));
                float searchPressure = flicker.CurrentStealthPressure;

                PlayerFeedbackEvents.ReportNeighborInvestigation(
                    PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Abandoned,
                    Vector3.zero,
                    "Noise Source",
                    0.16f,
                    0.28f,
                    false);
                GameplaySmokeTestReflection.Invoke(flicker, "UpdateStealthPressure", 0.75f);

                Assert.That(flicker.CurrentStealthPressure, Is.LessThan(searchPressure));
                Assert.That(flicker.CurrentStealthPressure, Is.InRange(0.06f, 0.14f));
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
        public void AtmosphereFlickerLight_HeardNoiseAddsUnstableLight()
        {
            GameObject lightObject = new("Noise Flicker Light Test");

            try
            {
                Light light = lightObject.AddComponent<Light>();
                AtmosphereFlickerLight flicker = lightObject.AddComponent<AtmosphereFlickerLight>();
                flicker.Configure(1f, 0.2f, 4f);

                PlayerFeedbackEvents.NoiseFeedback singleListenerNoise = new(
                    Vector3.zero,
                    0.48f,
                    6f,
                    0.64f,
                    true,
                    1);
                PlayerFeedbackEvents.NoiseFeedback multiListenerNoise = new(
                    Vector3.zero,
                    0.48f,
                    6f,
                    0.64f,
                    true,
                    3);
                float singleListenerPressure = GameplaySmokeTestReflection.InvokeResult<float>(
                    flicker,
                    "GetNoisePressure",
                    singleListenerNoise);
                float multiListenerPressure = GameplaySmokeTestReflection.InvokeResult<float>(
                    flicker,
                    "GetNoisePressure",
                    multiListenerNoise);

                Assert.That(multiListenerPressure, Is.GreaterThan(singleListenerPressure));

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
        public void AtmosphereDressingAnchor_CompromisedHidingPressurizesDecalsAndProps()
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

                MaterialPropertyBlock block = new();
                decalRenderer.GetPropertyBlock(block);
                Color calmDecal = block.GetColor("_BaseColor");
                Vector3 calmPropScale = propObject.transform.localScale;

                PlayerFeedbackEvents.ReportStealthLoop(
                    PlayerFeedbackEvents.StealthLoopPhase.Hiding,
                    1f,
                    0f,
                    1f,
                    "He found your hiding spot.");

                decalRenderer.GetPropertyBlock(block);
                Color foundDecal = block.GetColor("_BaseColor");

                Assert.That(decal.CurrentStealthPressure, Is.GreaterThan(0.95f));
                Assert.That(prop.CurrentStealthPressure, Is.GreaterThan(0.95f));
                Assert.That(decal.EffectiveIntensity, Is.GreaterThan(0.6f));
                Assert.That(foundDecal.a, Is.GreaterThan(calmDecal.a));
                Assert.That(foundDecal.r, Is.LessThan(calmDecal.r));
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

                PlayerFeedbackEvents.NoiseFeedback singleListenerNoise = new(
                    Vector3.zero,
                    0.48f,
                    6f,
                    0.64f,
                    true,
                    1);
                PlayerFeedbackEvents.NoiseFeedback multiListenerNoise = new(
                    Vector3.zero,
                    0.48f,
                    6f,
                    0.64f,
                    true,
                    3);
                float singleListenerPressure = GameplaySmokeTestReflection.InvokeResult<float>(
                    decal,
                    "GetNoisePressure",
                    singleListenerNoise);
                float multiListenerPressure = GameplaySmokeTestReflection.InvokeResult<float>(
                    decal,
                    "GetNoisePressure",
                    multiListenerNoise);

                Assert.That(multiListenerPressure, Is.GreaterThan(singleListenerPressure));

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
        public void AtmosphereDressingAnchor_NeighborTrailSearchPressurizesDecalsAndProps()
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
                    PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Searching,
                    Vector3.zero,
                    "Basement Key Trail",
                    0.66f,
                    0.9f,
                    true);

                decalRenderer.GetPropertyBlock(block);
                Color trailDecal = block.GetColor("_BaseColor");

                Assert.That(decal.CurrentStealthPressure, Is.GreaterThan(0.85f));
                Assert.That(prop.CurrentStealthPressure, Is.GreaterThan(0.85f));
                Assert.That(decal.EffectiveIntensity, Is.GreaterThan(0.58f));
                Assert.That(trailDecal.a, Is.GreaterThan(calmDecal.a));
                Assert.That(trailDecal.r, Is.LessThan(calmDecal.r));
                Assert.That(propObject.transform.localScale.x, Is.GreaterThan(calmPropScale.x));

                float trailDecalPressure = decal.CurrentStealthPressure;
                float trailPropPressure = prop.CurrentStealthPressure;
                PlayerFeedbackEvents.ReportNeighborInvestigation(
                    PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Returning,
                    Vector3.zero,
                    "Basement Key Trail",
                    0.28f,
                    0.45f,
                    true);
                GameplaySmokeTestReflection.Invoke(decal, "UpdateStealthDressing", 0.75f);
                GameplaySmokeTestReflection.Invoke(prop, "UpdateStealthDressing", 0.75f);

                Assert.That(decal.CurrentStealthPressure, Is.LessThan(trailDecalPressure));
                Assert.That(prop.CurrentStealthPressure, Is.LessThan(trailPropPressure));
                Assert.That(decal.CurrentStealthPressure, Is.LessThan(0.2f));
                Assert.That(prop.CurrentStealthPressure, Is.LessThan(0.2f));
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
        public void AtmosphereDressingAnchor_ResolvedNoiseInvestigationLeavesLowSettle()
        {
            GameObject decalObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            GameObject propObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material decalMaterial = CreateTestMaterial(new Color(0.18f, 0.14f, 0.09f, 0.3f));
            Material propMaterial = CreateTestMaterial(new Color(0.34f, 0.23f, 0.14f, 1f));

            try
            {
                decalObject.name = "Resolved Noise Dirty Decal Dressing Test";
                propObject.name = "Resolved Noise Prop Dressing Test";
                propObject.transform.localScale = new Vector3(1.1f, 0.25f, 0.45f);

                decalObject.GetComponent<Renderer>().sharedMaterial = decalMaterial;
                propObject.GetComponent<Renderer>().sharedMaterial = propMaterial;

                AtmosphereDressingAnchor decal = decalObject.AddComponent<AtmosphereDressingAnchor>();
                AtmosphereDressingAnchor prop = propObject.AddComponent<AtmosphereDressingAnchor>();
                decal.Configure(AtmosphereDressingAnchor.DressingKind.DirtyDecal, 0.4f);
                prop.Configure(AtmosphereDressingAnchor.DressingKind.PropDressing, 0.48f);

                PlayerFeedbackEvents.ReportNeighborInvestigation(
                    PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Searching,
                    Vector3.zero,
                    "Noise Source",
                    0.56f,
                    0.65f,
                    false);

                Assert.That(decal.CurrentStealthPressure, Is.GreaterThan(0.5f));
                Assert.That(prop.CurrentStealthPressure, Is.GreaterThan(0.5f));
                float searchDecalPressure = decal.CurrentStealthPressure;
                float searchPropPressure = prop.CurrentStealthPressure;

                PlayerFeedbackEvents.ReportNeighborInvestigation(
                    PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Returning,
                    Vector3.zero,
                    "Noise Source",
                    0.16f,
                    0.28f,
                    false);
                GameplaySmokeTestReflection.Invoke(decal, "UpdateStealthDressing", 0.75f);
                GameplaySmokeTestReflection.Invoke(prop, "UpdateStealthDressing", 0.75f);

                Assert.That(decal.CurrentStealthPressure, Is.LessThan(searchDecalPressure));
                Assert.That(prop.CurrentStealthPressure, Is.LessThan(searchPropPressure));
                Assert.That(decal.CurrentStealthPressure, Is.InRange(0.06f, 0.14f));
                Assert.That(prop.CurrentStealthPressure, Is.InRange(0.06f, 0.14f));
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

        private static AtmosphereDressingAnchor[] FindDressingAnchorsInScene(Scene scene)
        {
            AtmosphereDressingAnchor[] components =
                Object.FindObjectsByType<AtmosphereDressingAnchor>(FindObjectsInactive.Include);
            List<AtmosphereDressingAnchor> anchors = new();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].gameObject.scene == scene)
                {
                    anchors.Add(components[i]);
                }
            }

            return anchors.ToArray();
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

        private static bool TextureHasAlphaVariation(Texture texture)
        {
            if (texture is not Texture2D texture2D)
            {
                return false;
            }

            Color[] pixels = texture2D.GetPixels();
            bool hasLowAlpha = false;
            bool hasHighAlpha = false;
            for (int i = 0; i < pixels.Length; i++)
            {
                hasLowAlpha |= pixels[i].a < 0.35f;
                hasHighAlpha |= pixels[i].a > 0.55f;
                if (hasLowAlpha && hasHighAlpha)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TextureHasColorVariation(Texture texture)
        {
            if (texture is not Texture2D texture2D)
            {
                return false;
            }

            Color[] pixels = texture2D.GetPixels();
            if (pixels.Length == 0)
            {
                return false;
            }

            Color first = pixels[0];
            for (int i = 1; i < pixels.Length; i++)
            {
                if (Mathf.Abs(pixels[i].r - first.r) > 0.03f
                    || Mathf.Abs(pixels[i].g - first.g) > 0.03f
                    || Mathf.Abs(pixels[i].b - first.b) > 0.03f)
                {
                    return true;
                }
            }

            return false;
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
