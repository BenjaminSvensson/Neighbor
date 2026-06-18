using System.Collections.Generic;
using Neighbor.Main.Features.Interaction;
using Neighbor.Main.Features.Neighbor;
using Neighbor.Main.Features.Player;
using NUnit.Framework;
using UnityEngine;

namespace Neighbor.Main.Tests
{
    public sealed class InteractionGameplaySmokeTests
    {
        private GameplaySmokeTestContext context;

        [SetUp]
        public void SetUp()
        {
            context = new GameplaySmokeTestContext();
        }

        [TearDown]
        public void TearDown()
        {
            context.Dispose();
        }

        [Test]
        public void Door_CanBeLockedUnlockedAndMatchedToItsKey()
        {
            Door door = context.AddInitializedComponent<Door>();
            DoorKey matchingKey = context.AddInitializedComponent<DoorKey>("MatchingKey");
            DoorKey wrongKey = context.AddInitializedComponent<DoorKey>("WrongKey");
            GameplaySmokeTestReflection.SetField(wrongKey, "keyId", "wrong_key");

            Assert.That(door.IsLocked, Is.True);
            Assert.That(matchingKey.Opens(door), Is.True);
            Assert.That(wrongKey.Opens(door), Is.False);

            door.Unlock();
            Assert.That(door.IsLocked, Is.False);

            door.Lock();
            Assert.That(door.IsLocked, Is.True);
        }

        [Test]
        public void NeighborDoorInteractor_DoesNotCloseDoorWithoutConfirmedPassage()
        {
            Door door = context.AddInitializedComponent<Door>();
            door.Unlock();
            Assert.That(door.TryOpenForNeighbor(door.transform), Is.True);

            NeighborDoorInteractor doorInteractor = context.AddInitializedComponent<NeighborDoorInteractor>();
            GameplaySmokeTestReflection.SetField(doorInteractor, "closeBehindDelay", 0f);
            GameplaySmokeTestReflection.SetField(doorInteractor, "closeBehindFailsafeDelay", 0f);
            GameplaySmokeTestReflection.Invoke(doorInteractor, "TrackOpenedDoor", door);
            GameplaySmokeTestReflection.Invoke(doorInteractor, "UpdateOpenedDoors");

            Assert.That(door.IsOpen, Is.True);
        }

        [Test]
        public void NeighborDoorInteractor_YieldsWhenNeighborIsAtTaskUsePoint()
        {
            GameObject neighborObject = context.CreateObject("Neighbor");
            NeighborMotor motor = context.AddInitializedComponent<NeighborMotor>(neighborObject);
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(neighborObject);
            NeighborDoorInteractor doorInteractor = context.AddInitializedComponent<NeighborDoorInteractor>(neighborObject);

            GameObject car = context.CreateObject("CarTask");
            NeighborTaskLocation task = context.AddInitializedComponent<NeighborTaskLocation>(car);
            Assert.That(task.TryReserve(brain), Is.True);
            GameplaySmokeTestReflection.SetField(brain, "currentState", NeighborBrain.BehaviorState.Task);
            GameplaySmokeTestReflection.SetField(brain, "currentTaskLocation", task);
            GameplaySmokeTestReflection.SetField(doorInteractor, "brain", brain);
            GameplaySmokeTestReflection.SetField(doorInteractor, "motor", motor);

            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<bool>(
                    doorInteractor,
                    "ShouldYieldToTaskUse"),
                Is.True);
        }

        [Test]
        public void Door_AllowsMultipleReinforcementBoardsWithStaggeredPose()
        {
            Door door = context.AddInitializedComponent<Door>();
            DoorBlockerChair firstBoard = CreateBoardBlocker("FirstBoard");
            DoorBlockerChair secondBoard = CreateBoardBlocker("SecondBoard");

            Assert.That(firstBoard.TryBlockDoorAsReinforcement(door, 0), Is.True);
            Assert.That(secondBoard.TryBlockDoorAsReinforcement(door, 1), Is.True);

            Assert.That(door.IsBlocked, Is.True);
            Assert.That(door.ActiveBlockerCount, Is.EqualTo(2));
            Assert.That(Mathf.Abs(firstBoard.transform.position.y - secondBoard.transform.position.y), Is.GreaterThan(0.2f));
            Assert.That(Quaternion.Angle(firstBoard.transform.rotation, secondBoard.transform.rotation), Is.GreaterThan(1f));
        }

        [Test]
        public void Glass_PlayerBreakReinforcementRestoresPaneAndBoardsOpening()
        {
            GameObject boardPrefab = CreateBoardObject("BoardPrefab");
            GameObject glassObject = context.CreateObject("Glass");
            BoxCollider glassCollider = glassObject.AddComponent<BoxCollider>();
            glassCollider.size = new Vector3(0.08f, 1.5f, 2f);
            GlassShatter glass = context.AddInitializedComponent<GlassShatter>(glassObject);
            ConfigureQuietGlass(glass);
            ConfigureReinforcementOption(glass, boardPrefab);
            GameplaySmokeTestReflection.SetField(glass, "reinforcementBoardCount", 2);

            glass.ShatterFromPlayer(glass.transform.position, Vector3.forward * 4f, null);

            Assert.That(glassCollider.enabled, Is.False);

            GlassShatter.ResetAllToStartingState();
            GlassShatter.ApplyRunReinforcements(1, new ReinforcementBudget(3));

            List<GameObject> spawnedReinforcements =
                GameplaySmokeTestReflection.GetField<List<GameObject>>(glass, "spawnedReinforcements");
            Assert.That(glassCollider.enabled, Is.True);
            Assert.That(spawnedReinforcements, Has.Count.EqualTo(2));
            Assert.That(spawnedReinforcements[0].GetComponent<DoorBlockerChair>().IsBlockingDoor, Is.True);
            Assert.That(spawnedReinforcements[1].GetComponent<DoorBlockerChair>().IsBlockingDoor, Is.True);
            Assert.That(
                Mathf.Abs(spawnedReinforcements[0].transform.position.y - spawnedReinforcements[1].transform.position.y),
                Is.GreaterThan(0.2f));
            Assert.That(
                Quaternion.Angle(spawnedReinforcements[0].transform.rotation, spawnedReinforcements[1].transform.rotation),
                Is.GreaterThan(1f));

            for (int i = 0; i < spawnedReinforcements.Count; i++)
            {
                if (spawnedReinforcements[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(spawnedReinforcements[i]);
                }
            }
        }

        [Test]
        public void PlayerKeyRing_TracksOwnedKeysAndIgnoresInvalidKeys()
        {
            PlayerKeyRing keyRing = context.AddInitializedComponent<PlayerKeyRing>();

            keyRing.AddKey("front_door");
            keyRing.AddKey(" ");

            Assert.That(keyRing.HasKey("front_door"), Is.True);
            Assert.That(keyRing.HasKey("back_door"), Is.False);
            Assert.That(keyRing.HasKey(" "), Is.False);
        }

        [Test]
        public void PickupDrop_RestoresPhysicsAndHeldState()
        {
            GameObject pickupObject = context.CreateObject("Pickup");
            Rigidbody body = pickupObject.AddComponent<Rigidbody>();
            BoxCollider collider = pickupObject.AddComponent<BoxCollider>();
            PickupLifecycleProbe lifecycle = pickupObject.AddComponent<PickupLifecycleProbe>();
            Pickupable pickup = context.AddInitializedComponent<Pickupable>(pickupObject);

            pickup.Pickup(null);

            Assert.That(pickup.IsHeld, Is.True);
            Assert.That(body.isKinematic, Is.True);
            Assert.That(body.useGravity, Is.False);
            Assert.That(collider.enabled, Is.False);
            Assert.That(lifecycle.PickupStartedCount, Is.EqualTo(1));

            pickup.Drop();

            Assert.That(pickup.IsHeld, Is.False);
            Assert.That(body.isKinematic, Is.False);
            Assert.That(body.useGravity, Is.True);
            Assert.That(collider.enabled, Is.True);
        }

        [Test]
        public void PickupPlace_CompletesLifecycleAndMovesItem()
        {
            GameObject pickupObject = context.CreateObject("Pickup");
            Rigidbody body = pickupObject.AddComponent<Rigidbody>();
            pickupObject.AddComponent<BoxCollider>();
            PickupLifecycleProbe lifecycle = pickupObject.AddComponent<PickupLifecycleProbe>();
            Pickupable pickup = context.AddInitializedComponent<Pickupable>(pickupObject);
            Vector3 placementPosition = new(2f, 3f, 4f);

            pickup.Pickup(null);
            pickup.Place(placementPosition, Quaternion.Euler(0f, 90f, 0f), false);

            Assert.That(pickup.IsHeld, Is.False);
            Assert.That(pickup.transform.position, Is.EqualTo(placementPosition));
            Assert.That(body.isKinematic, Is.False);
            Assert.That(lifecycle.PickupStartedCount, Is.EqualTo(1));
            Assert.That(lifecycle.PickupPlacedCount, Is.EqualTo(1));
        }

        [Test]
        public void PlayerPickup_SnapsItemToHandsBeforeGrabAnimationStarts()
        {
            GameObject interactorObject = context.CreateObject("PlayerInteractor");
            PlayerInteractor interactor = interactorObject.AddComponent<PlayerInteractor>();
            Transform holdPoint = context.CreateObject("MediumHoldPoint").transform;
            holdPoint.SetPositionAndRotation(new Vector3(2f, 3f, 4f), Quaternion.Euler(10f, 20f, 30f));
            GameplaySmokeTestReflection.SetField(interactor, "mediumHoldPoint", holdPoint);
            GameplaySmokeTestReflection.SetField(interactor, "holdObstructionRadius", 0f);

            Pickupable pickup = CreatePickup("Pickup");
            pickup.transform.position = new Vector3(-10f, -10f, -10f);
            Vector3 positionWhenAnimationStarted = Vector3.zero;
            interactor.PickupStarted += () => positionWhenAnimationStarted = pickup.transform.position;

            interactor.Pickup(pickup);

            Assert.That(pickup.transform.position, Is.EqualTo(holdPoint.position));
            Assert.That(positionWhenAnimationStarted, Is.EqualTo(holdPoint.position));
        }

        [Test]
        public void PlayerInteraction_StartsAnimationBeforeInteractableRuns()
        {
            PlayerInteractor interactor = context.CreateObject("PlayerInteractor").AddComponent<PlayerInteractor>();
            GameObject interactableObject = context.CreateObject("Interactable");
            interactableObject.transform.position = Vector3.forward * 1.5f;
            interactableObject.AddComponent<BoxCollider>();
            InteractionOrderProbe interactable = interactableObject.AddComponent<InteractionOrderProbe>();
            interactor.InteractionStarted += () => interactable.InteractionSignalReceived = true;
            Physics.SyncTransforms();

            GameplaySmokeTestReflection.Invoke(interactor, "TryInteract");

            Assert.That(interactable.InteractCount, Is.EqualTo(1));
            Assert.That(interactable.SignalWasReceivedBeforeInteract, Is.True);
        }

        [Test]
        public void MatchingInventoryPickup_AutoEquipsSilentlyAfterActionDelay()
        {
            GameObject interactorObject = context.CreateObject("PlayerInteractor");
            PlayerInteractor interactor = interactorObject.AddComponent<PlayerInteractor>();
            Pickupable releasedPickup = CreatePickup("Tomato");
            Pickupable nextPickup = CreatePickup("Tomato (1)");
            nextPickup.StoreInInventory(null);

            GameplaySmokeTestReflection.SetField(interactor, "inventorySlots", new Pickupable[] { null, nextPickup });
            GameplaySmokeTestReflection.SetField(interactor, "activeInventorySlot", 0);
            int pickupAnimationCount = 0;
            interactor.PickupStarted += () => pickupAnimationCount++;

            GameplaySmokeTestReflection.Invoke(interactor, "QueueMatchingInventoryPickup", releasedPickup, 1f);

            Assert.That(interactor.HeldPickup, Is.Null);
            Assert.That(nextPickup.IsInventoryStored, Is.True);

            GameplaySmokeTestReflection.SetField(interactor, "pendingAutoEquipAt", -1f);
            GameplaySmokeTestReflection.Invoke(interactor, "UpdatePendingAutoEquip");

            Assert.That(interactor.HeldPickup, Is.SameAs(nextPickup));
            Assert.That(interactor.ActiveInventorySlot, Is.EqualTo(1));
            Assert.That(nextPickup.IsInventoryStored, Is.False);
            Assert.That(pickupAnimationCount, Is.Zero);
        }

        [Test]
        public void NoiseEvent_ReportsReadablePlayerFeedback()
        {
            PlayerFeedbackEvents.NoiseFeedback received = default;
            bool reported = false;
            PlayerFeedbackEvents.NoiseEmitted += HandleNoise;

            GameObject noiseObject = context.CreateObject("NoiseEvent");
            noiseObject.AddComponent<SphereCollider>();
            NoiseEvent noiseEvent = noiseObject.AddComponent<NoiseEvent>();
            noiseEvent.Initialize(Vector3.one, 12f, 0.75f, noiseObject, 1f);

            PlayerFeedbackEvents.NoiseEmitted -= HandleNoise;
            Assert.That(reported, Is.True);
            Assert.That(received.Loudness, Is.EqualTo(0.75f));
            Assert.That(received.Radius, Is.EqualTo(12f));

            void HandleNoise(PlayerFeedbackEvents.NoiseFeedback feedback)
            {
                received = feedback;
                reported = true;
            }
        }

        private Pickupable CreatePickup(string name)
        {
            GameObject pickupObject = context.CreateObject(name);
            pickupObject.AddComponent<Rigidbody>();
            pickupObject.AddComponent<BoxCollider>();
            return context.AddInitializedComponent<Pickupable>(pickupObject);
        }

        private DoorBlockerChair CreateBoardBlocker(string name)
        {
            DoorBlockerChair blocker = CreateBoardObject(name).GetComponent<DoorBlockerChair>();
            GameplaySmokeTestReflection.SetField(blocker, "attachToDoorSurface", true);
            GameplaySmokeTestReflection.SetField(blocker, "leanAngle", 0f);
            return blocker;
        }

        private GameObject CreateBoardObject(string name)
        {
            GameObject boardObject = context.CreateObject(name);
            Rigidbody body = boardObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            BoxCollider collider = boardObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(2.3f, 0.22f, 0.22f);
            Pickupable pickupable = boardObject.AddComponent<Pickupable>();
            DoorBlockerChair blocker = boardObject.AddComponent<DoorBlockerChair>();
            WoodBoardPryTarget pryTarget = boardObject.AddComponent<WoodBoardPryTarget>();

            GameplaySmokeTestReflection.InvokeIfPresent(pickupable, "Awake");
            GameplaySmokeTestReflection.InvokeIfPresent(blocker, "Awake");
            GameplaySmokeTestReflection.InvokeIfPresent(pryTarget, "Awake");
            return boardObject;
        }

        private static void ConfigureQuietGlass(GlassShatter glass)
        {
            GameplaySmokeTestReflection.SetField(glass, "shatterClips", new AudioClip[] { null });
            GameplaySmokeTestReflection.SetField(glass, "hearingRadius", 0f);
        }

        private static void ConfigureReinforcementOption(GlassShatter glass, GameObject prefab)
        {
            ReinforcementPrefabOption option = new();
            GameplaySmokeTestReflection.SetField(option, "prefab", prefab);
            GameplaySmokeTestReflection.SetField(glass, "blockerReinforcementOptions", new[] { option });
        }
    }
}
