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
        }

        [TearDown]
        public void TearDown()
        {
            context.Dispose();
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
