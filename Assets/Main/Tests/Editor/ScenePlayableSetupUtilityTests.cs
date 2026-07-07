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
    }
}
