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
        public void CoreLoop_GetInsidePickupKeyUnlockDoorReachRoomAndEscape_CompletesObjective()
        {
            CoreLoopObjectiveTracker tracker = CreateObject("Objective").AddComponent<CoreLoopObjectiveTracker>();
            PlayerController player = CreatePlayer("Player", Vector3.zero, out PlayerInteractor interactor);
            Pickupable key = CreateObjectiveKey("BasementKey");
            Door door = CreateObjectiveDoor("BasementDoor");

            Assert.That(tracker.CurrentStep, Is.EqualTo(CoreLoopObjectiveTracker.ObjectiveStep.GetInside));
            Assert.That(tracker.CurrentHint, Is.Not.Empty);

            ObjectiveTriggerZone entryZone = CreateObjectiveZone("EntryZone", ObjectiveTriggerZone.TriggerRole.EnterHouse);
            Assert.That(entryZone.NotifyPlayerEntered(player), Is.True);
            Assert.That(tracker.HasEnteredHouse, Is.True);
            Assert.That(tracker.CurrentStep, Is.EqualTo(CoreLoopObjectiveTracker.ObjectiveStep.FindKey));

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
        public void CoreLoop_KeyPickupImplicitlyCompletesEntryStepForPrototypeScenes()
        {
            CoreLoopObjectiveTracker tracker = CreateObject("Objective").AddComponent<CoreLoopObjectiveTracker>();
            CreatePlayer("Player", Vector3.zero, out PlayerInteractor interactor);
            Pickupable key = CreateObjectiveKey("BasementKey");

            Assert.That(tracker.CurrentStep, Is.EqualTo(CoreLoopObjectiveTracker.ObjectiveStep.GetInside));

            interactor.Pickup(key);

            Assert.That(tracker.HasEnteredHouse, Is.True);
            Assert.That(tracker.HasKey, Is.True);
            Assert.That(tracker.CurrentStep, Is.EqualTo(CoreLoopObjectiveTracker.ObjectiveStep.UnlockDoor));
        }

        [Test]
        public void CoreLoop_ReportsReadableObjectiveProgressFeedback()
        {
            CoreLoopObjectiveTracker tracker = CreateObject("Objective").AddComponent<CoreLoopObjectiveTracker>();
            PlayerController player = CreatePlayer("Player", Vector3.zero, out _);
            List<PlayerFeedbackEvents.ObjectiveProgressFeedback> feedback = new();
            PlayerFeedbackEvents.ObjectiveProgressed += HandleProgress;

            try
            {
                Assert.That(tracker.RegisterEnteredHouse(player), Is.True);

                Assert.That(feedback, Has.Count.EqualTo(1));
                Assert.That(feedback[0].StepIndex, Is.EqualTo(2));
                Assert.That(feedback[0].TotalSteps, Is.EqualTo(CoreLoopObjectiveTracker.TotalObjectiveSteps));
                Assert.That(feedback[0].Message, Is.EqualTo("Inside. Find the key."));
                Assert.That(feedback[0].Hint, Is.EqualTo(tracker.CurrentHint));
                Assert.That(feedback[0].IsComplete, Is.False);

                Assert.That(tracker.RegisterKeyCollected(tracker.RequiredKeyId), Is.True);

                Assert.That(feedback, Has.Count.EqualTo(2));
                Assert.That(feedback[1].StepIndex, Is.EqualTo(3));
                Assert.That(feedback[1].Message, Is.EqualTo("Key found. Unlock the door."));
                Assert.That(feedback[1].Hint, Is.EqualTo(tracker.CurrentHint));
            }
            finally
            {
                PlayerFeedbackEvents.ObjectiveProgressed -= HandleProgress;
            }

            void HandleProgress(PlayerFeedbackEvents.ObjectiveProgressFeedback progressFeedback)
            {
                feedback.Add(progressFeedback);
            }
        }

        [Test]
        public void LockedDoor_WrongHeldKeyReportsReadableFeedbackAndStaysLocked()
        {
            CreatePlayer("Player", Vector3.zero, out PlayerInteractor interactor);
            Pickupable wrongKey = CreateObjectiveKey("GarageKey");
            SetField(wrongKey.GetComponent<DoorKey>(), "keyId", "garage_key");
            Door door = CreateObjectiveDoor("BasementDoor");

            PlayerFeedbackEvents.DoorInteractionFeedback feedback = default;
            bool receivedFeedback = false;
            PlayerFeedbackEvents.DoorInteractionReported += HandleFeedback;
            try
            {
                interactor.Pickup(wrongKey);
                door.Interact(interactor);
            }
            finally
            {
                PlayerFeedbackEvents.DoorInteractionReported -= HandleFeedback;
            }

            Assert.That(door.IsLocked, Is.True);
            Assert.That(door.IsOpen, Is.False);
            Assert.That(receivedFeedback, Is.True);
            Assert.That(feedback.Kind, Is.EqualTo(PlayerFeedbackEvents.DoorInteractionFeedbackKind.Locked));
            Assert.That(feedback.Message, Is.EqualTo("Need Test Key"));
            Assert.That(feedback.Intensity, Is.GreaterThan(0.5f));

            void HandleFeedback(PlayerFeedbackEvents.DoorInteractionFeedback reportedFeedback)
            {
                feedback = reportedFeedback;
                receivedFeedback = true;
            }
        }

        [Test]
        public void LockedDoor_KeyRingUnlocksWithoutHoldingKeyAndReportsSuccess()
        {
            PlayerController player = CreatePlayer("Player", Vector3.zero, out PlayerInteractor interactor);
            player.GetComponent<PlayerKeyRing>().AddKey("test_key");
            Door door = CreateObjectiveDoor("BasementDoor");

            PlayerFeedbackEvents.DoorInteractionFeedback feedback = default;
            bool receivedFeedback = false;
            PlayerFeedbackEvents.DoorInteractionReported += HandleFeedback;
            try
            {
                door.Interact(interactor);
            }
            finally
            {
                PlayerFeedbackEvents.DoorInteractionReported -= HandleFeedback;
            }

            Assert.That(door.IsLocked, Is.False);
            Assert.That(door.IsOpen, Is.True);
            Assert.That(receivedFeedback, Is.True);
            Assert.That(feedback.Kind, Is.EqualTo(PlayerFeedbackEvents.DoorInteractionFeedbackKind.Unlocked));
            Assert.That(feedback.Message, Is.EqualTo("Unlocked"));

            void HandleFeedback(PlayerFeedbackEvents.DoorInteractionFeedback reportedFeedback)
            {
                feedback = reportedFeedback;
                receivedFeedback = true;
            }
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

        [TestCase(ClosetHideSpot.HideSpotKind.Bed, "bed", "Hide under bed", "Slide out")]
        [TestCase(ClosetHideSpot.HideSpotKind.Curtain, "curtain", "Hide behind curtain", "Step out")]
        public void TypedHideSpot_PlayerHidesAndNeighborCanInspect(
            ClosetHideSpot.HideSpotKind kind,
            string displayName,
            string hideAction,
            string exitAction)
        {
            PlayerController player = CreatePlayer("Player", Vector3.zero, out _);
            ClosetHideSpot hideSpot = CreateObject($"{kind}HideSpot").AddComponent<ClosetHideSpot>();
            SetField(hideSpot, "hideSpotKind", kind);
            SetField(hideSpot, "transitionDuration", 0.05f);
            SetField(hideSpot, "doorLeadTime", 0f);
            SetField(hideSpot, "doorCloseDelay", 0f);

            PlayerHidingState hiddenState = player.gameObject.AddComponent<PlayerHidingState>();
            SetField(hideSpot, "hiddenPlayer", player);
            SetField(hideSpot, "hiddenState", hiddenState);

            Assert.That(hideSpot.DisplayName, Is.EqualTo(displayName));
            Assert.That(hideSpot.GetInteractionActionText(), Is.EqualTo(exitAction));

            RunCoroutine(InvokeResult<IEnumerator>(hideSpot, "HideTransition"), $"{kind} hide transition did not complete.");

            Assert.That(hideSpot.Kind, Is.EqualTo(kind));
            Assert.That(hideSpot.HasHiddenPlayer, Is.True);
            Assert.That(hiddenState.IsHidden, Is.True);
            Assert.That(hiddenState.CurrentHideSpot, Is.SameAs(hideSpot));
            Assert.That(player.enabled, Is.False);

            Assert.That(hideSpot.SearchByNeighbor(), Is.SameAs(player));
            Assert.That(hiddenState.WasInspectedRecently, Is.True);
            Assert.That(hiddenState.IsCompromised, Is.True);
            Assert.That(hideSpot.GetInteractionActionText(), Is.EqualTo(exitAction));

            hiddenState.SetHidden(false);
            SetField<PlayerController>(hideSpot, "hiddenPlayer", null);
            Assert.That(hideSpot.GetInteractionActionText(), Is.EqualTo(hideAction));
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

            PlayerFeedbackEvents.RespawnFeedback respawnFeedback = default;
            bool receivedRespawnFeedback = false;
            PlayerFeedbackEvents.PlayerRespawned += HandleRespawn;
            player.transform.position = new Vector3(8f, 0f, 0f);
            try
            {
                RunCoroutine(
                    InvokeResult<IEnumerator>(deathController, "DeathAndReset", Vector3.zero),
                    "Player death reset did not finish.");
            }
            finally
            {
                PlayerFeedbackEvents.PlayerRespawned -= HandleRespawn;
            }

            Assert.That(deathController.IsDead, Is.False);
            Assert.That(Vector3.Distance(player.transform.position, spawnPosition), Is.LessThan(0.05f));
            Assert.That(player.enabled, Is.True);
            Assert.That(player.IsBeartrapLocked, Is.False);
            Assert.That(receivedRespawnFeedback, Is.True);
            Assert.That(respawnFeedback.UsedCheckpoint, Is.False);
            Assert.That(respawnFeedback.Name, Is.EqualTo("Start"));

            void HandleRespawn(PlayerFeedbackEvents.RespawnFeedback feedback)
            {
                respawnFeedback = feedback;
                receivedRespawnFeedback = true;
            }
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
            SetField(checkpoint, "checkpointDisplayName", "Guest Bed");
            SetField(checkpoint, "persistCheckpoint", false);

            Assert.That(
                checkpoint.TryGetInteractionTooltip(
                    null,
                    InteractionTooltipContext.FocusedInteractable,
                    out string checkpointAction,
                    out _),
                Is.True);
            Assert.That(checkpointAction, Is.EqualTo("Save Guest Bed"));
            Assert.That(checkpoint.Activate(player), Is.True);
            Assert.That(deathController.HasCheckpoint, Is.True);
            Assert.That(deathController.ActiveCheckpointId, Is.EqualTo("Guest Bed"));

            SetField(deathController, "fallDuration", 0.01f);
            SetField(deathController, "impactDuration", 0.01f);
            SetField(deathController, "groundHoldDuration", 0.01f);
            SetField(deathController, "fadeOutDuration", 0.01f);

            PlayerFeedbackEvents.RespawnFeedback respawnFeedback = default;
            bool receivedRespawnFeedback = false;
            PlayerFeedbackEvents.PlayerRespawned += HandleRespawn;
            player.transform.position = new Vector3(12f, 0f, -4f);
            try
            {
                RunCoroutine(
                    InvokeResult<IEnumerator>(deathController, "DeathAndReset", Vector3.zero),
                    "Player checkpoint death reset did not finish.");
            }
            finally
            {
                PlayerFeedbackEvents.PlayerRespawned -= HandleRespawn;
            }

            Assert.That(deathController.IsDead, Is.False);
            Assert.That(Vector3.Distance(player.transform.position, checkpoint.RespawnPosition), Is.LessThan(0.05f));
            Assert.That(Quaternion.Angle(player.transform.rotation, checkpoint.RespawnRotation), Is.LessThan(1f));
            Assert.That(player.enabled, Is.True);
            Assert.That(receivedRespawnFeedback, Is.True);
            Assert.That(respawnFeedback.UsedCheckpoint, Is.True);
            Assert.That(respawnFeedback.Name, Is.EqualTo("Guest Bed"));

            void HandleRespawn(PlayerFeedbackEvents.RespawnFeedback feedback)
            {
                respawnFeedback = feedback;
                receivedRespawnFeedback = true;
            }
        }

        [Test]
        public void PlayerDeath_ResetRestoresMovedPickupHomeState()
        {
            PlayerController player = CreatePlayer("Player", Vector3.zero, out _);
            PlayerDeathController deathController = player.GetComponent<PlayerDeathController>();
            deathController.ClearCheckpoint(true);

            Pickupable pickup = CreatePickup("MovedPickup", new Vector3(3f, 0f, 0f));
            Vector3 homePosition = pickup.HomePosition;
            Quaternion homeRotation = pickup.HomeRotation;
            pickup.transform.SetPositionAndRotation(new Vector3(8f, 0f, 2f), Quaternion.Euler(0f, 90f, 0f));

            Assert.That(pickup.IsMissingFromHome, Is.True);

            SetField(deathController, "fallDuration", 0.01f);
            SetField(deathController, "impactDuration", 0.01f);
            SetField(deathController, "groundHoldDuration", 0.01f);
            SetField(deathController, "fadeOutDuration", 0.01f);

            RunCoroutine(
                InvokeResult<IEnumerator>(deathController, "DeathAndReset", Vector3.zero),
                "Player death reset did not restore pickup world state.");

            Assert.That(Vector3.Distance(pickup.transform.position, homePosition), Is.LessThan(0.05f));
            Assert.That(Quaternion.Angle(pickup.transform.rotation, homeRotation), Is.LessThan(1f));
            Assert.That(pickup.IsAtHome, Is.True);
        }

        [Test]
        public void PlayerDeath_ResetRestoresTrapDoorBeartrapAndCameraRunState()
        {
            PlayerController player = CreatePlayer("Player", Vector3.zero, out _);
            PlayerDeathController deathController = player.GetComponent<PlayerDeathController>();
            deathController.ClearCheckpoint(true);

            Beartrap beartrap = CreateBeartrap("Beartrap");
            Invoke(beartrap, "Start");
            Invoke(beartrap, "SetState", GetNestedEnumValue(typeof(Beartrap), "TrapState", "Triggered"));
            Assert.That(beartrap.IsTriggered, Is.True);

            FakeFloorTrapDoor trapDoor = CreateFakeFloorTrapDoor("TrapDoor", out Collider blockingCollider);
            trapDoor.Open();
            Assert.That(trapDoor.IsOpen, Is.True);
            Assert.That(blockingCollider.enabled, Is.False);

            SecurityCamera camera = CreateSecurityCamera("SecurityCamera", new Vector3(4f, 0f, 0f));
            Vector3 cameraHomePosition = camera.transform.position;
            Quaternion cameraHomeRotation = camera.transform.rotation;
            Assert.That(camera.TryAttachByNeighbor(new Vector3(6f, 1f, 0f), Vector3.back), Is.True);
            Assert.That(camera.IsAttached, Is.True);
            Assert.That(camera.IsNeighborPlaced, Is.True);
            Assert.That(SecurityCamera.NeighborPlacedCameraCount, Is.EqualTo(1));

            SetField(deathController, "fallDuration", 0.01f);
            SetField(deathController, "impactDuration", 0.01f);
            SetField(deathController, "groundHoldDuration", 0.01f);
            SetField(deathController, "fadeOutDuration", 0.01f);

            RunCoroutine(
                InvokeResult<IEnumerator>(deathController, "DeathAndReset", Vector3.zero),
                "Player death reset did not restore world run state.");

            Assert.That(beartrap.IsClosed, Is.True);
            Assert.That(trapDoor.IsOpen, Is.False);
            Assert.That(blockingCollider.enabled, Is.True);
            Assert.That(camera.IsAttached, Is.False);
            Assert.That(camera.IsNeighborPlaced, Is.False);
            Assert.That(camera.IsDisabled, Is.False);
            Assert.That(SecurityCamera.NeighborPlacedCameraCount, Is.Zero);
            Assert.That(Vector3.Distance(camera.transform.position, cameraHomePosition), Is.LessThan(0.05f));
            Assert.That(Quaternion.Angle(camera.transform.rotation, cameraHomeRotation), Is.LessThan(1f));
        }

        [Test]
        public void PlayerDeath_RespawnReleasesHideSpotAndBeartrapConstraints()
        {
            PlayerController player = CreatePlayer("Player", Vector3.zero, out _);
            PlayerDeathController deathController = player.GetComponent<PlayerDeathController>();
            deathController.ClearCheckpoint(true);

            ClosetHideSpot hideSpot = CreateObject("Closet").AddComponent<ClosetHideSpot>();
            PlayerHidingState hiddenState = player.gameObject.AddComponent<PlayerHidingState>();
            SetField(hideSpot, "hiddenPlayer", player);
            SetField(hideSpot, "hiddenState", hiddenState);
            hiddenState.SetHidden(true, hideSpot);
            player.PrepareForHiding();

            Beartrap beartrap = CreateBeartrap("Beartrap");
            Invoke(beartrap, "Start");
            SetField(beartrap, "stuckPlayer", player);
            Invoke(beartrap, "SetState", GetNestedEnumValue(typeof(Beartrap), "TrapState", "Triggered"));
            player.SetBeartrapLocked(true);

            Assert.That(hideSpot.HasHiddenPlayer, Is.True);
            Assert.That(hiddenState.IsHidden, Is.True);
            Assert.That(player.enabled, Is.False);
            Assert.That(player.IsBeartrapLocked, Is.True);

            SetField(deathController, "fallDuration", 0.01f);
            SetField(deathController, "impactDuration", 0.01f);
            SetField(deathController, "groundHoldDuration", 0.01f);
            SetField(deathController, "fadeOutDuration", 0.01f);

            RunCoroutine(
                InvokeResult<IEnumerator>(deathController, "DeathAndReset", Vector3.zero),
                "Player death reset did not release hide spot and beartrap constraints.");

            Assert.That(hideSpot.HasHiddenPlayer, Is.False);
            Assert.That(hiddenState.IsHidden, Is.False);
            Assert.That(player.enabled, Is.True);
            Assert.That(player.IsBeartrapLocked, Is.False);
            Assert.That(beartrap.IsClosed, Is.True);
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

        private Pickupable CreatePickup(string name, Vector3 position)
        {
            GameObject pickupObject = CreateObject(name);
            pickupObject.transform.position = position;
            pickupObject.AddComponent<Rigidbody>();
            pickupObject.AddComponent<BoxCollider>();
            return pickupObject.AddComponent<Pickupable>();
        }

        private Beartrap CreateBeartrap(string name)
        {
            GameObject trapObject = CreateObject(name);
            Rigidbody body = trapObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            trapObject.AddComponent<BoxCollider>();
            trapObject.AddComponent<Pickupable>();
            return trapObject.AddComponent<Beartrap>();
        }

        private FakeFloorTrapDoor CreateFakeFloorTrapDoor(string name, out Collider blockingCollider)
        {
            GameObject trapObject = CreateObject(name);
            Transform leftPanel = CreateObject(name + "LeftPanel").transform;
            Transform rightPanel = CreateObject(name + "RightPanel").transform;
            leftPanel.SetParent(trapObject.transform, false);
            rightPanel.SetParent(trapObject.transform, false);
            leftPanel.localPosition = new Vector3(-0.25f, 0f, 0f);
            rightPanel.localPosition = new Vector3(0.25f, 0f, 0f);
            blockingCollider = trapObject.AddComponent<BoxCollider>();

            FakeFloorTrapDoor trapDoor = trapObject.AddComponent<FakeFloorTrapDoor>();
            SetField(trapDoor, "leftPanel", leftPanel);
            SetField(trapDoor, "rightPanel", rightPanel);
            SetField(trapDoor, "blockingColliders", new[] { blockingCollider });
            Invoke(trapDoor, "CacheClosedPose");
            return trapDoor;
        }

        private SecurityCamera CreateSecurityCamera(string name, Vector3 position)
        {
            GameObject cameraObject = CreateObject(name);
            cameraObject.transform.position = position;
            Rigidbody body = cameraObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            cameraObject.AddComponent<BoxCollider>();
            cameraObject.AddComponent<Pickupable>();
            return cameraObject.AddComponent<SecurityCamera>();
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

        private static object GetNestedEnumValue(Type ownerType, string enumTypeName, string enumValueName)
        {
            Type enumType = ownerType.GetNestedType(enumTypeName, BindingFlags.NonPublic);
            Assert.That(enumType, Is.Not.Null, $"Could not find nested enum '{enumTypeName}'.");
            return Enum.Parse(enumType, enumValueName);
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
