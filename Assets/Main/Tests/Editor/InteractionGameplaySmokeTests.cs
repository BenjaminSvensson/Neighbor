using Neighbor.Main.Features.Interaction;
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
    }
}
