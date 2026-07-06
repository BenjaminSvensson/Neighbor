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
            Assert.That(result.Player, Is.Not.Null);
            Assert.That(result.PlayerDeathController, Is.Not.Null);
            Assert.That(result.PlayerKeyRing, Is.Not.Null);
            Assert.That(result.PlayerHidingState, Is.Not.Null);
            Assert.That(result.OnboardingDirector, Is.Not.Null);
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
            Assert.That(result.StartCheckpoint.CheckpointId, Is.EqualTo("Start"));
            Assert.That(result.StartCheckpoint.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(result.StartCheckpoint.GetComponent<BoxCollider>().isTrigger, Is.True);

            Assert.That(CountInScene<PlayerController>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerDeathController>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerKeyRing>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerHidingState>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerOnboardingDirector>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<NeighborBrain>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<CoreLoopObjectiveTracker>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerRespawnCheckpoint>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<NavMeshSurface>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<AmbienceManager>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerAwarenessHudView>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerInventoryHudView>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<EventSystem>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<DayNightCycle>(scene), Is.EqualTo(1));
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
            Assert.That(CountInScene<PlayerController>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerDeathController>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerKeyRing>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerHidingState>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerOnboardingDirector>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<NeighborBrain>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<CoreLoopObjectiveTracker>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerRespawnCheckpoint>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<NavMeshSurface>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<AmbienceManager>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerAwarenessHudView>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<PlayerInventoryHudView>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<EventSystem>(scene), Is.EqualTo(1));
            Assert.That(CountInScene<DayNightCycle>(scene), Is.EqualTo(1));
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
