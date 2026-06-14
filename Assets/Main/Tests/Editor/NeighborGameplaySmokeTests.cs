using Neighbor.Main.Features.Interaction;
using Neighbor.Main.Features.Neighbor;
using NUnit.Framework;
using UnityEngine;

namespace Neighbor.Main.Tests
{
    public sealed class NeighborGameplaySmokeTests
    {
        private GameplaySmokeTestContext context;

        [SetUp]
        public void SetUp()
        {
            context = new GameplaySmokeTestContext();
            AdaptiveSecurityDirector.ResetProgression();
        }

        [TearDown]
        public void TearDown()
        {
            context.Dispose();
            AdaptiveSecurityDirector.ResetProgression();
        }

        [Test]
        public void AdaptiveSecurity_LoudRunRaisesReinforcementPlan()
        {
            for (int i = 0; i < 5; i++)
            {
                AdaptiveSecurityDirector.ReportDisturbance(1f);
            }

            AdaptiveSecurityPlan plan = AdaptiveSecurityDirector.CompleteRun(5, 2, 2);

            Assert.That(plan.Level, Is.EqualTo(2));
            Assert.That(plan.Budget, Is.EqualTo(9));
            Assert.That(plan.LocationCount, Is.EqualTo(3));
            Assert.That(plan.DoorCount, Is.EqualTo(3));
            Assert.That(AdaptiveSecurityDirector.RunPressure, Is.Zero);
            Assert.That(AdaptiveSecurityDirector.PersistentPressure, Is.GreaterThan(0f));
        }

        [Test]
        public void AdaptiveSecurity_RepeatedDeathsIncreasePersistentResponse()
        {
            AdaptiveSecurityPlan firstPlan = AdaptiveSecurityDirector.CompleteRun(5, 2, 2);
            AdaptiveSecurityPlan latestPlan = firstPlan;
            for (int i = 0; i < 4; i++)
            {
                latestPlan = AdaptiveSecurityDirector.CompleteRun(5, 2, 2);
            }

            Assert.That(firstPlan.Level, Is.Zero);
            Assert.That(latestPlan.Level, Is.GreaterThan(firstPlan.Level));
            Assert.That(latestPlan.Budget, Is.GreaterThan(firstPlan.Budget));
        }

        [Test]
        public void Suspicion_UsesThresholdsAndDecaysOverTime()
        {
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>();

            NeighborEnvironmentalAwareness.Report(brain.transform.position, 0.2f, null);
            Assert.That(brain.CurrentSuspicionLevel, Is.EqualTo(NeighborBrain.SuspicionLevel.Curious));

            NeighborEnvironmentalAwareness.Report(brain.transform.position, 0.3f, null);
            Assert.That(brain.CurrentSuspicionLevel, Is.EqualTo(NeighborBrain.SuspicionLevel.Suspicious));

            NeighborEnvironmentalAwareness.Report(brain.transform.position, 0.4f, null);
            Assert.That(brain.CurrentSuspicionLevel, Is.EqualTo(NeighborBrain.SuspicionLevel.Certain));

            float beforeDecay = brain.Suspicion;
            GameplaySmokeTestReflection.Invoke(brain, "UpdateSuspicion", 1f);

            Assert.That(brain.Suspicion, Is.LessThan(beforeDecay));
        }

        [Test]
        public void EnvironmentalAwareness_OnlyRaisesSuspicionInsideAwarenessRadius()
        {
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>();

            NeighborEnvironmentalAwareness.Report(brain.transform.position + Vector3.right * 17f, 0.5f, null);
            Assert.That(brain.Suspicion, Is.Zero);

            NeighborEnvironmentalAwareness.Report(brain.transform.position + Vector3.right * 15f, 0.5f, null);
            Assert.That(brain.Suspicion, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void OrdinaryPickup_DoesNotAlertNeighbor()
        {
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>();
            PlayerInteractor interactor = context.CreateObject("PlayerInteractor").AddComponent<PlayerInteractor>();
            GameObject pickupObject = context.CreateObject("Pickup");
            pickupObject.AddComponent<Rigidbody>();
            pickupObject.AddComponent<BoxCollider>();
            Pickupable pickup = context.AddInitializedComponent<Pickupable>(pickupObject);

            pickup.Pickup(interactor);

            Assert.That(brain.Suspicion, Is.Zero);
        }

        [Test]
        public void Vision_RejectsPlayerAboveUpwardViewLimit()
        {
            NeighborVision vision = context.AddInitializedComponent<NeighborVision>();
            Transform target = context.CreateObject("PlayerTarget").transform;
            GameplaySmokeTestReflection.SetField(vision, "target", target);
            GameplaySmokeTestReflection.SetField(vision, "eyeHeight", 0f);
            GameplaySmokeTestReflection.SetField(vision, "lineOfSightMask", (LayerMask)0);

            target.position = Vector3.forward * 5f + Vector3.up * 5f;
            Assert.That(vision.TrySeeTarget(out _, out _), Is.False);

            target.position = Vector3.forward * 5f + Vector3.up;
            Assert.That(vision.TrySeeTarget(out _, out _), Is.True);
        }

        [Test]
        public void NeighborPlacedCameraSpacing_PreventsOverlappingMounts()
        {
            GameObject cameraObject = context.CreateObject("NeighborPlacedCamera");
            cameraObject.AddComponent<BoxCollider>();
            SecurityCamera camera = context.AddInitializedComponent<SecurityCamera>(cameraObject);
            GameplaySmokeTestReflection.InvokeIfPresent(cameraObject.GetComponent<Pickupable>(), "Awake");

            Assert.That(camera.TryAttachByNeighbor(Vector3.zero, Vector3.forward), Is.True);
            Assert.That(SecurityCamera.IsNeighborCameraWithinDistance(Vector3.right, 2f), Is.True);
            Assert.That(SecurityCamera.IsNeighborCameraWithinDistance(Vector3.right * 3f, 2f), Is.False);
        }

        [Test]
        public void HuntEnding_StartsPostEncounterTaskSuppression()
        {
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>();

            GameplaySmokeTestReflection.Invoke(brain, "EndHuntMode");

            Assert.That(brain.IsPostEncounterVigilant, Is.True);
            Assert.That(brain.PostEncounterVigilanceTimeRemaining, Is.GreaterThan(0f));
            Assert.That(brain.CurrentState, Is.Not.EqualTo(NeighborBrain.BehaviorState.Task));
        }

        [Test]
        public void NoiseEvent_InRange_PrimesNeighborInvestigation()
        {
            GameObject neighborObject = context.CreateObject("Neighbor");
            NeighborHearing hearing = context.AddInitializedComponent<NeighborHearing>(neighborObject);
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(neighborObject);
            GameObject source = context.CreateObject("NoiseSource");
            GameObject noiseObject = context.CreateObject("NoiseEvent");
            noiseObject.AddComponent<SphereCollider>();
            NoiseEvent noiseEvent = noiseObject.AddComponent<NoiseEvent>();

            noiseEvent.Initialize(neighborObject.transform.position, 5f, 0.6f, source, 1f, 1f);

            Assert.That(brain.Suspicion, Is.GreaterThan(0f));
            Assert.That(
                GameplaySmokeTestReflection.GetField<GameObject>(brain, "currentInvestigationSource"),
                Is.SameAs(source));
            Assert.That(hearing, Is.Not.Null);
        }

        [Test]
        public void NoiseEvent_OutOfRange_DoesNotPrimeInvestigation()
        {
            GameObject neighborObject = context.CreateObject("Neighbor");
            context.AddInitializedComponent<NeighborHearing>(neighborObject);
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(neighborObject);
            GameObject noiseObject = context.CreateObject("NoiseEvent");
            noiseObject.transform.position = Vector3.right * 6f;
            noiseObject.AddComponent<SphereCollider>();
            NoiseEvent noiseEvent = noiseObject.AddComponent<NoiseEvent>();

            noiseEvent.Initialize(noiseObject.transform.position, 5f, 1f, noiseObject, 1f);

            Assert.That(brain.Suspicion, Is.Zero);
        }

        [Test]
        public void Stun_EntersStunnedStateAndRecoversToRoutine()
        {
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>();

            brain.Stun(1f);
            Assert.That(brain.CurrentState, Is.EqualTo(NeighborBrain.BehaviorState.Stunned));
            Assert.That(brain.CurrentSuspicionLevel, Is.EqualTo(NeighborBrain.SuspicionLevel.Certain));

            GameplaySmokeTestReflection.SetField(brain, "stunnedUntilTime", float.NegativeInfinity);
            GameplaySmokeTestReflection.Invoke(brain, "Update");

            Assert.That(brain.CurrentState, Is.EqualTo(NeighborBrain.BehaviorState.Idle));
        }
    }
}
