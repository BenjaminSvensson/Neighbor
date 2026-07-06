using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Neighbor.Main.Features.Interaction;
using Neighbor.Main.Features.Neighbor;
using Neighbor.Main.Features.Player;
using Neighbor.Main.Features.Progression;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

#if UNITY_INCLUDE_TESTS
namespace Neighbor.Main.Tests
{
    public sealed class CoreLoopPlayModeSmokeTests
    {
        private readonly List<GameObject> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void CoreLoop_PickupKeyUnlockDoorReachRoomAndEscape_CompletesObjective()
        {
            CoreLoopObjectiveTracker tracker = CreateObject("Objective").AddComponent<CoreLoopObjectiveTracker>();
            PlayerController player = CreatePlayer("Player", Vector3.zero, out PlayerInteractor interactor);
            Pickupable key = CreateObjectiveKey("BasementKey");
            Door door = CreateObjectiveDoor("BasementDoor");

            Assert.That(tracker.CurrentStep, Is.EqualTo(CoreLoopObjectiveTracker.ObjectiveStep.FindKey));
            Assert.That(tracker.CurrentHint, Is.Not.Empty);

            interactor.Pickup(key);

            Assert.That(key.IsHeld, Is.True);
            Assert.That(interactor.HeldPickup, Is.SameAs(key));
            Assert.That(tracker.HasKey, Is.True);
            Assert.That(tracker.CurrentStep, Is.EqualTo(CoreLoopObjectiveTracker.ObjectiveStep.UnlockDoor));
            Assert.That(door.IsLocked, Is.True);

            door.Interact(interactor);

            Assert.That(door.IsLocked, Is.False);
            Assert.That(tracker.HasUnlockedDoor, Is.True);
            Assert.That(tracker.CurrentStep, Is.EqualTo(CoreLoopObjectiveTracker.ObjectiveStep.ReachRoom));

            ObjectiveTriggerZone roomZone = CreateObjectiveZone("TargetRoom", ObjectiveTriggerZone.TriggerRole.ReachRoom);
            Assert.That(roomZone.NotifyPlayerEntered(player), Is.True);
            Assert.That(tracker.HasReachedRoom, Is.True);
            Assert.That(tracker.CurrentStep, Is.EqualTo(CoreLoopObjectiveTracker.ObjectiveStep.Escape));

            ObjectiveTriggerZone escapeZone = CreateObjectiveZone("EscapeZone", ObjectiveTriggerZone.TriggerRole.Escape);
            Assert.That(escapeZone.NotifyPlayerEntered(player), Is.True);
            Assert.That(tracker.HasEscaped, Is.True);
            Assert.That(tracker.IsComplete, Is.True);
            Assert.That(tracker.CurrentHint, Is.Not.Empty);
        }

        [Test]
        public void NeighborVision_PlayerInSight_EntersChaseState()
        {
            CreatePlayer("Player", Vector3.forward * 4f, out _);
            GameObject neighborObject = CreateObject("Neighbor");
            neighborObject.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            neighborObject.AddComponent<NeighborVision>();
            NeighborBrain brain = neighborObject.AddComponent<NeighborBrain>();

            Invoke(brain, "Update");

            Assert.That(brain.CurrentState, Is.EqualTo(NeighborBrain.BehaviorState.Chase));
            Assert.That(brain.IsPlayerVisible, Is.True);
            Assert.That(brain.LastKnownPlayerPosition.z, Is.GreaterThan(3f));
        }

        [Test]
        public void ClosetHideSpot_PlayerHidesAndNeighborInspectionFindsPlayer()
        {
            PlayerController player = CreatePlayer("Player", Vector3.zero, out PlayerInteractor interactor);
            ClosetHideSpot hideSpot = CreateObject("Closet").AddComponent<ClosetHideSpot>();
            SetField(hideSpot, "transitionDuration", 0.05f);
            SetField(hideSpot, "doorLeadTime", 0f);
            SetField(hideSpot, "doorCloseDelay", 0f);

            PlayerHidingState hiddenState = player.gameObject.AddComponent<PlayerHidingState>();
            SetField(hideSpot, "hiddenPlayer", player);
            SetField(hideSpot, "hiddenState", hiddenState);
            RunCoroutine(InvokeResult<IEnumerator>(hideSpot, "HideTransition"), "Hide transition did not complete.");

            Assert.That(hideSpot.HasHiddenPlayer, Is.True);
            Assert.That(hiddenState.IsHidden, Is.True);
            Assert.That(player.enabled, Is.False);

            Assert.That(hideSpot.SearchByNeighbor(), Is.SameAs(player));
        }

        [Test]
        public void PlayerDeath_RespawnsAtStartAndClearsDeathState()
        {
            Vector3 spawnPosition = new(2f, 0f, 0f);
            PlayerController player = CreatePlayer("Player", spawnPosition, out _);
            PlayerDeathController deathController = player.GetComponent<PlayerDeathController>();
            Assert.That(deathController, Is.Not.Null);
            deathController.ClearCheckpoint(true);

            SetField(deathController, "fallDuration", 0.01f);
            SetField(deathController, "impactDuration", 0.01f);
            SetField(deathController, "groundHoldDuration", 0.01f);
            SetField(deathController, "fadeOutDuration", 0.01f);

            player.transform.position = new Vector3(8f, 0f, 0f);
            RunCoroutine(
                InvokeResult<IEnumerator>(deathController, "DeathAndReset", Vector3.zero),
                "Player death reset did not finish.");

            Assert.That(deathController.IsDead, Is.False);
            Assert.That(Vector3.Distance(player.transform.position, spawnPosition), Is.LessThan(0.05f));
            Assert.That(player.enabled, Is.True);
            Assert.That(player.IsBeartrapLocked, Is.False);
        }

        [Test]
        public void PlayerDeath_RespawnsAtActivatedCheckpoint()
        {
            Vector3 spawnPosition = new(2f, 0f, 0f);
            PlayerController player = CreatePlayer("Player", spawnPosition, out _);
            PlayerDeathController deathController = player.GetComponent<PlayerDeathController>();
            Assert.That(deathController, Is.Not.Null);
            deathController.ClearCheckpoint(true);

            PlayerRespawnCheckpoint checkpoint = CreateObject("BedCheckpoint").AddComponent<PlayerRespawnCheckpoint>();
            checkpoint.transform.SetPositionAndRotation(new Vector3(9f, 0f, 3f), Quaternion.Euler(0f, 135f, 0f));
            SetField(checkpoint, "checkpointId", "Bed");
            SetField(checkpoint, "persistCheckpoint", false);

            Assert.That(checkpoint.Activate(player), Is.True);
            Assert.That(deathController.HasCheckpoint, Is.True);
            Assert.That(deathController.ActiveCheckpointId, Is.EqualTo("Bed"));

            SetField(deathController, "fallDuration", 0.01f);
            SetField(deathController, "impactDuration", 0.01f);
            SetField(deathController, "groundHoldDuration", 0.01f);
            SetField(deathController, "fadeOutDuration", 0.01f);

            player.transform.position = new Vector3(12f, 0f, -4f);
            RunCoroutine(
                InvokeResult<IEnumerator>(deathController, "DeathAndReset", Vector3.zero),
                "Player checkpoint death reset did not finish.");

            Assert.That(deathController.IsDead, Is.False);
            Assert.That(Vector3.Distance(player.transform.position, checkpoint.RespawnPosition), Is.LessThan(0.05f));
            Assert.That(Quaternion.Angle(player.transform.rotation, checkpoint.RespawnRotation), Is.LessThan(1f));
            Assert.That(player.enabled, Is.True);
        }

        [Test]
        public void PlayerOnboardingDirector_IsRuntimeInstalledAndEmitsFeedback()
        {
            PlayerController player = CreatePlayer("Player", Vector3.zero, out _);
            PlayerOnboardingDirector director = player.GetComponent<PlayerOnboardingDirector>();
            Assert.That(director, Is.Not.Null);

            string receivedMessage = null;
            PlayerFeedbackEvents.OnboardingPrompted += HandlePrompt;
            try
            {
                bool emitted = InvokeResult<bool>(
                    director,
                    "TryEmitPrompt",
                    "Find a way inside.",
                    0.35f,
                    true);

                Assert.That(emitted, Is.True);
                Assert.That(receivedMessage, Is.EqualTo("Find a way inside."));
                Assert.That(director.LastPrompt, Is.EqualTo("Find a way inside."));
            }
            finally
            {
                PlayerFeedbackEvents.OnboardingPrompted -= HandlePrompt;
            }

            void HandlePrompt(PlayerFeedbackEvents.OnboardingPromptFeedback feedback)
            {
                receivedMessage = feedback.Message;
            }
        }

        private PlayerController CreatePlayer(string name, Vector3 position, out PlayerInteractor interactor)
        {
            GameObject playerObject = CreateObject(name);
            playerObject.transform.position = position;
            CharacterController controller = playerObject.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.35f;
            controller.center = Vector3.up;
            playerObject.AddComponent<PlayerKeyRing>();
            PlayerController player = playerObject.AddComponent<PlayerController>();
            interactor = playerObject.AddComponent<PlayerInteractor>();
            return player;
        }

        private Pickupable CreateObjectiveKey(string name)
        {
            GameObject keyObject = CreateObject(name);
            keyObject.AddComponent<Rigidbody>();
            keyObject.AddComponent<BoxCollider>();
            keyObject.AddComponent<DoorKey>();
            keyObject.AddComponent<ObjectiveKeyItem>();
            return keyObject.AddComponent<Pickupable>();
        }

        private Door CreateObjectiveDoor(string name)
        {
            GameObject doorObject = CreateObject(name);
            doorObject.AddComponent<BoxCollider>();
            Door door = doorObject.AddComponent<Door>();
            doorObject.AddComponent<ObjectiveDoorUnlockWatcher>();
            return door;
        }

        private ObjectiveTriggerZone CreateObjectiveZone(string name, ObjectiveTriggerZone.TriggerRole role)
        {
            GameObject zoneObject = CreateObject(name);
            BoxCollider trigger = zoneObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            ObjectiveTriggerZone zone = zoneObject.AddComponent<ObjectiveTriggerZone>();
            zone.Role = role;
            return zone;
        }

        private GameObject CreateObject(string name)
        {
            GameObject gameObject = new(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static void RunCoroutine(IEnumerator coroutine, string failureMessage)
        {
            Assert.That(coroutine, Is.Not.Null);
            Stack<IEnumerator> stack = new();
            stack.Push(coroutine);
            int steps = 0;
            while (stack.Count > 0 && steps < 1024)
            {
                IEnumerator current = stack.Peek();
                if (!current.MoveNext())
                {
                    stack.Pop();
                    steps++;
                    continue;
                }

                if (current.Current is IEnumerator nested)
                {
                    stack.Push(nested);
                }

                steps++;
            }

            Assert.That(stack.Count, Is.Zero, failureMessage);
        }

        private static void SetField<TValue>(object target, string fieldName, TValue value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Could not find private field '{fieldName}'.");
            field.SetValue(target, value);
        }

        private static void Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = FindMethod(target, methodName);
            method.Invoke(target, arguments);
        }

        private static TResult InvokeResult<TResult>(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = FindMethod(target, methodName);
            return (TResult)method.Invoke(target, arguments);
        }

        private static MethodInfo FindMethod(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Could not find private method '{methodName}'.");
            return method;
        }
    }
}
#endif
