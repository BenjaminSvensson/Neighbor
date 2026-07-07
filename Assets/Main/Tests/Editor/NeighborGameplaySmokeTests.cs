using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.AI.Navigation;
using Neighbor.Main.Features.Interaction;
using Neighbor.Main.Features.Neighbor;
using Neighbor.Main.Features.Player;
using Neighbor.Main.Features.Progression;
using Neighbor.Main.HouseBuilder;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

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
            Assert.That(plan.CameraCount, Is.EqualTo(1));
            Assert.That(plan.TrapCount, Is.EqualTo(1));
            Assert.That(plan.PatrolPointCount, Is.EqualTo(2));
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
            Assert.That(latestPlan.PatrolPointCount, Is.GreaterThan(firstPlan.PatrolPointCount));
        }

        [Test]
        public void AdaptiveSecurity_PatrolPlanMarksNeighborPostRespawnVigilant()
        {
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>();

            brain.HandlePlayerRespawned(0f, 3);

            Assert.That(brain.AdaptiveSecurityPatrolsRemaining, Is.EqualTo(3));
            Assert.That(brain.IsPostEncounterVigilant, Is.True);

            brain.HandlePlayerRespawned(0f, 0);

            Assert.That(brain.AdaptiveSecurityPatrolsRemaining, Is.Zero);
            Assert.That(brain.IsPostEncounterVigilant, Is.False);
        }

        [Test]
        public void AwarenessHud_ShowsPersistentObjectiveProgress()
        {
            CoreLoopObjectiveTracker tracker = context.AddInitializedComponent<CoreLoopObjectiveTracker>(
                context.CreateObject("Objective"));
            PlayerAwarenessHudView hud = context.AddInitializedComponent<PlayerAwarenessHudView>(
                context.CreateObject("AwarenessHud"));

            GameplaySmokeTestReflection.Invoke(hud, "UpdateObjective");
            Text objectiveText = GameplaySmokeTestReflection.GetField<Text>(hud, "objectiveText");

            Assert.That(objectiveText.text, Does.Contain("OBJECTIVE 1/5"));
            Assert.That(objectiveText.text, Does.Contain(tracker.CurrentHint.ToUpperInvariant()));

            tracker.RegisterKeyCollected(tracker.RequiredKeyId);

            Text warningText = GameplaySmokeTestReflection.GetField<Text>(hud, "warningText");
            Assert.That(objectiveText.text, Does.Contain("OBJECTIVE 3/5"));
            Assert.That(warningText.text, Does.Contain("OBJECTIVE 3/5"));
            Assert.That(warningText.text, Does.Contain("KEY FOUND"));

            GameplaySmokeTestReflection.Invoke(
                hud,
                "HandlePlayerRespawned",
                new PlayerFeedbackEvents.RespawnFeedback("Guest Bed", true, Vector3.one));

            Assert.That(warningText.text, Is.EqualTo("RESPAWNED AT GUEST BED"));

            GameplaySmokeTestReflection.Invoke(
                hud,
                "HandleHidingChanged",
                new PlayerFeedbackEvents.HidingFeedback(
                    "curtain",
                    PlayerFeedbackEvents.HidingFeedbackKind.Entered,
                    0f,
                    true,
                    false));

            Assert.That(warningText.text, Is.EqualTo("HIDDEN IN CURTAIN"));

            GameplaySmokeTestReflection.Invoke(
                hud,
                "HandleHidingChanged",
                new PlayerFeedbackEvents.HidingFeedback(
                    "curtain",
                    PlayerFeedbackEvents.HidingFeedbackKind.Found,
                    1f,
                    true,
                    true));

            Assert.That(warningText.text, Is.EqualTo("FOUND IN CURTAIN"));

            GameplaySmokeTestReflection.Invoke(
                hud,
                "HandleNeighborInvestigationChanged",
                new PlayerFeedbackEvents.NeighborInvestigationFeedback(
                    PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.FollowingTrail,
                    Vector3.zero,
                    "Broken Window",
                    0.72f,
                    0.8f));

            Assert.That(warningText.text, Is.EqualTo("HE IS FOLLOWING YOUR TRAIL"));

            GameplaySmokeTestReflection.Invoke(
                hud,
                "HandleNeighborInvestigationChanged",
                new PlayerFeedbackEvents.NeighborInvestigationFeedback(
                    PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Searching,
                    Vector3.zero,
                    "Noise Source",
                    0.6f,
                    0.8f));

            Assert.That(warningText.text, Is.EqualTo("NEIGHBOR SEARCHING"));
        }

        [Test]
        public void AwarenessHud_LabelsActiveInvestigationAsSearching()
        {
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(context.CreateObject("Neighbor"));
            PlayerAwarenessHudView hud = context.AddInitializedComponent<PlayerAwarenessHudView>(
                context.CreateObject("AwarenessHud"));

            GameplaySmokeTestReflection.SetField(brain, "currentState", NeighborBrain.BehaviorState.Investigate);
            GameplaySmokeTestReflection.SetField(hud, "trackedNeighbor", brain);
            GameplaySmokeTestReflection.Invoke(hud, "UpdateAwareness");

            Text awarenessText = GameplaySmokeTestReflection.GetField<Text>(hud, "awarenessText");
            Assert.That(awarenessText.text, Is.EqualTo("SEARCHING"));
        }

        [Test]
        public void AwarenessHud_LabelsActiveMemoryHuntAsTrail()
        {
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(context.CreateObject("Neighbor"));
            PlayerAwarenessHudView hud = context.AddInitializedComponent<PlayerAwarenessHudView>(
                context.CreateObject("AwarenessHud"));

            GameplaySmokeTestReflection.SetField(brain, "currentState", NeighborBrain.BehaviorState.HuntMode);
            GameplaySmokeTestReflection.SetField(brain, "currentHuntMemoryClueActive", true);
            GameplaySmokeTestReflection.SetField(hud, "trackedNeighbor", brain);
            GameplaySmokeTestReflection.Invoke(hud, "UpdateAwareness");
            GameplaySmokeTestReflection.Invoke(
                hud,
                "HandleNeighborMemoryChanged",
                new PlayerFeedbackEvents.NeighborMemoryFeedback(
                    PlayerFeedbackEvents.NeighborMemoryClueKind.KeyStolen,
                    "Basement Key",
                    Vector3.zero,
                    0.82f,
                    3));
            GameplaySmokeTestReflection.Invoke(hud, "UpdateStealthStatus");

            Text awarenessText = GameplaySmokeTestReflection.GetField<Text>(hud, "awarenessText");
            Text stealthStatusText = GameplaySmokeTestReflection.GetField<Text>(hud, "stealthStatusText");
            Assert.That(awarenessText.text, Is.EqualTo("TRAIL"));
            Assert.That(stealthStatusText.text, Is.EqualTo("RECOVERY / TRAIL"));
        }

        [Test]
        public void AwarenessHud_ShowsStealthLoopAndNeighborMemoryFeedback()
        {
            PlayerAwarenessHudView hud = context.AddInitializedComponent<PlayerAwarenessHudView>(
                context.CreateObject("AwarenessHud"));
            Text warningText = GameplaySmokeTestReflection.GetField<Text>(hud, "warningText");
            Image tensionFill = GameplaySmokeTestReflection.GetField<Image>(hud, "tensionFill");

            GameplaySmokeTestReflection.Invoke(
                hud,
                "HandleStealthLoopChanged",
                new PlayerFeedbackEvents.StealthLoopFeedback(
                    PlayerFeedbackEvents.StealthLoopPhase.PostChase,
                    0.55f,
                    0f,
                    0.8f,
                    "Stay hidden. He is checking the area."));

            Assert.That(warningText.text, Is.EqualTo("STAY HIDDEN. HE IS CHECKING THE AREA."));
            GameplaySmokeTestReflection.Invoke(hud, "UpdateTension");
            Assert.That(tensionFill.fillAmount, Is.GreaterThan(0.75f));

            GameplaySmokeTestReflection.SetField(hud, "messageUntil", 0f);
            GameplaySmokeTestReflection.Invoke(hud, "UpdateWarning");
            Assert.That(warningText.text, Is.EqualTo("POST-CHASE TENSION"));

            GameplaySmokeTestReflection.Invoke(
                hud,
                "HandleNeighborMemoryChanged",
                new PlayerFeedbackEvents.NeighborMemoryFeedback(
                    PlayerFeedbackEvents.NeighborMemoryClueKind.GlassBroken,
                    "Kitchen Window",
                    Vector3.zero,
                    0.72f,
                    3));

            Assert.That(warningText.text, Is.EqualTo("HE REMEMBERS THE BROKEN GLASS"));
        }

        [Test]
        public void AwarenessHud_MemoryPressureFeedsStealthStatusAndTension()
        {
            PlayerAwarenessHudView hud = context.AddInitializedComponent<PlayerAwarenessHudView>(
                context.CreateObject("AwarenessHud"));

            GameplaySmokeTestReflection.Invoke(
                hud,
                "HandleNeighborMemoryChanged",
                new PlayerFeedbackEvents.NeighborMemoryFeedback(
                    PlayerFeedbackEvents.NeighborMemoryClueKind.KeyStolen,
                    "Basement Key",
                    Vector3.zero,
                    0.82f,
                    3));
            GameplaySmokeTestReflection.Invoke(hud, "UpdateAwareness");
            GameplaySmokeTestReflection.Invoke(hud, "UpdateStealthStatus");
            GameplaySmokeTestReflection.Invoke(hud, "UpdateTension");

            Text awarenessText = GameplaySmokeTestReflection.GetField<Text>(hud, "awarenessText");
            Text stealthStatusText = GameplaySmokeTestReflection.GetField<Text>(hud, "stealthStatusText");
            Image tensionFill = GameplaySmokeTestReflection.GetField<Image>(hud, "tensionFill");

            Assert.That(awarenessText.text, Is.EqualTo("MEMORY"));
            Assert.That(stealthStatusText.text, Is.EqualTo("SUSPICIOUS / YOUR TRAIL"));
            Assert.That(tensionFill.fillAmount, Is.GreaterThan(0.8f));
        }

        [Test]
        public void NeighborMemoryFeedback_UrgencyReflectsClueKindAndStackedMemory()
        {
            PlayerFeedbackEvents.NeighborMemoryFeedback openedDoor = new(
                PlayerFeedbackEvents.NeighborMemoryClueKind.DoorOpened,
                "Front Door",
                Vector3.zero,
                0.05f,
                1);
            PlayerFeedbackEvents.NeighborMemoryFeedback stolenKeyTrail = new(
                PlayerFeedbackEvents.NeighborMemoryClueKind.KeyStolen,
                "Basement Key",
                Vector3.zero,
                0.05f,
                4);

            Assert.That(openedDoor.Suspicion, Is.EqualTo(0.05f).Within(0.001f));
            Assert.That(openedDoor.Urgency, Is.GreaterThanOrEqualTo(0.42f));
            Assert.That(stolenKeyTrail.Suspicion, Is.EqualTo(0.05f).Within(0.001f));
            Assert.That(stolenKeyTrail.Urgency, Is.GreaterThan(0.85f));
            Assert.That(stolenKeyTrail.Urgency, Is.GreaterThan(openedDoor.Urgency));
        }

        [Test]
        public void AwarenessHud_UsesMemoryUrgencyForLowSuspicionStolenKeyTrail()
        {
            PlayerAwarenessHudView hud = context.AddInitializedComponent<PlayerAwarenessHudView>(
                context.CreateObject("AwarenessHud"));

            GameplaySmokeTestReflection.Invoke(
                hud,
                "HandleNeighborMemoryChanged",
                new PlayerFeedbackEvents.NeighborMemoryFeedback(
                    PlayerFeedbackEvents.NeighborMemoryClueKind.KeyStolen,
                    "Basement Key",
                    Vector3.zero,
                    0.05f,
                    4));
            GameplaySmokeTestReflection.Invoke(hud, "UpdateStealthStatus");
            GameplaySmokeTestReflection.Invoke(hud, "UpdateTension");

            Text warningText = GameplaySmokeTestReflection.GetField<Text>(hud, "warningText");
            Text stealthStatusText = GameplaySmokeTestReflection.GetField<Text>(hud, "stealthStatusText");
            Image tensionFill = GameplaySmokeTestReflection.GetField<Image>(hud, "tensionFill");

            Assert.That(warningText.text, Is.EqualTo("HE KNOWS A KEY IS GONE"));
            Assert.That(stealthStatusText.text, Is.EqualTo("SUSPICIOUS / YOUR TRAIL"));
            Assert.That(tensionFill.fillAmount, Is.GreaterThan(0.85f));
        }

        [Test]
        public void AwarenessHud_ShowsPersistentStealthStatusAndLoudNoiseWarning()
        {
            GameObject playerObject = context.CreateObject("Player");
            context.AddInitializedComponent<PlayerController>(playerObject);
            PlayerAwarenessHudView hud = context.AddInitializedComponent<PlayerAwarenessHudView>(
                context.CreateObject("AwarenessHud"));

            GameplaySmokeTestReflection.Invoke(
                hud,
                "HandleNoise",
                new PlayerFeedbackEvents.NoiseFeedback(Vector3.zero, 0.82f, 6f));
            GameplaySmokeTestReflection.Invoke(hud, "UpdateNoise");
            GameplaySmokeTestReflection.Invoke(hud, "UpdateStealthStatus");
            GameplaySmokeTestReflection.Invoke(hud, "UpdateWarning");

            Text noiseLabel = GameplaySmokeTestReflection.GetField<Text>(hud, "noiseLabel");
            Text stealthStatusText = GameplaySmokeTestReflection.GetField<Text>(hud, "stealthStatusText");
            Text warningText = GameplaySmokeTestReflection.GetField<Text>(hud, "warningText");
            Assert.That(noiseLabel.text, Is.EqualTo("NOISE"));
            Assert.That(stealthStatusText.text, Is.EqualTo("QUIET / NOISE FADING"));
            Assert.That(warningText.text, Is.EqualTo("LOUD NOISE"));

            GameplaySmokeTestReflection.Invoke(
                hud,
                "HandleStealthLoopChanged",
                new PlayerFeedbackEvents.StealthLoopFeedback(
                    PlayerFeedbackEvents.StealthLoopPhase.Searching,
                    0.52f,
                    0.65f,
                    0.18f,
                    "He is investigating."));
            GameplaySmokeTestReflection.Invoke(hud, "UpdateStealthStatus");

            Assert.That(stealthStatusText.text, Is.EqualTo("SEARCHING / NOISE TRACE"));
        }

        [Test]
        public void AwarenessHud_ShowsNeighborHeardNoiseWarningFromNoiseFeedback()
        {
            GameObject playerObject = context.CreateObject("Player");
            context.AddInitializedComponent<PlayerController>(playerObject);
            PlayerAwarenessHudView hud = context.AddInitializedComponent<PlayerAwarenessHudView>(
                context.CreateObject("AwarenessHud"));

            GameplaySmokeTestReflection.Invoke(
                hud,
                "HandleNoise",
                new PlayerFeedbackEvents.NoiseFeedback(
                    Vector3.zero,
                    0.58f,
                    6f,
                    0.9f,
                    true,
                    1));
            GameplaySmokeTestReflection.Invoke(hud, "UpdateNoise");
            GameplaySmokeTestReflection.Invoke(hud, "UpdateWarning");

            Text noiseLabel = GameplaySmokeTestReflection.GetField<Text>(hud, "noiseLabel");
            Image noiseFill = GameplaySmokeTestReflection.GetField<Image>(hud, "noiseFill");
            Text warningText = GameplaySmokeTestReflection.GetField<Text>(hud, "warningText");
            Assert.That(noiseLabel.text, Is.EqualTo("HEARD"));
            Assert.That(noiseFill.fillAmount, Is.GreaterThan(0.85f));
            Assert.That(warningText.text, Is.EqualTo("HE HEARD THAT"));
        }

        [Test]
        public void AwarenessHud_HidingStatusShowsBreathPressure()
        {
            PlayerHidingState hidingState = context.AddInitializedComponent<PlayerHidingState>(
                context.CreateObject("Player"));
            hidingState.SetHidden(true);
            hidingState.AddBreathTension(0.76f);
            PlayerAwarenessHudView hud = context.AddInitializedComponent<PlayerAwarenessHudView>(
                context.CreateObject("AwarenessHud"));
            GameplaySmokeTestReflection.SetField(hud, "hidingState", hidingState);

            GameplaySmokeTestReflection.Invoke(hud, "UpdateStealthStatus");

            Text stealthStatusText = GameplaySmokeTestReflection.GetField<Text>(hud, "stealthStatusText");
            Assert.That(stealthStatusText.text, Is.EqualTo("HIDDEN / BREATH HIGH"));
        }

        [Test]
        public void StealthLoop_LeavingChaseStartsPostChaseTension()
        {
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(context.CreateObject("Neighbor"));
            PlayerFeedbackEvents.StealthLoopFeedback feedback = default;
            bool received = false;
            PlayerFeedbackEvents.StealthLoopChanged += HandleStealthLoopChanged;
            try
            {
                GameplaySmokeTestReflection.SetField(brain, "currentState", NeighborBrain.BehaviorState.Chase);

                GameplaySmokeTestReflection.Invoke(brain, "SetState", NeighborBrain.BehaviorState.HuntMode);

                Assert.That(brain.PostChaseTensionTimeRemaining, Is.GreaterThan(0f));
                Assert.That(brain.PostChaseTension01, Is.GreaterThan(0f));
                Assert.That(received, Is.True);
                Assert.That(feedback.Phase, Is.EqualTo(PlayerFeedbackEvents.StealthLoopPhase.PostChase));
            }
            finally
            {
                PlayerFeedbackEvents.StealthLoopChanged -= HandleStealthLoopChanged;
            }

            void HandleStealthLoopChanged(PlayerFeedbackEvents.StealthLoopFeedback reportedFeedback)
            {
                feedback = reportedFeedback;
                received = true;
            }
        }

        [Test]
        public void HidingBreath_RecoversWhenDangerHasPassed()
        {
            PlayerHidingState hidingState = context.AddInitializedComponent<PlayerHidingState>(
                context.CreateObject("Player"));
            hidingState.SetHidden(true);
            hidingState.AddBreathTension(0.58f);
            GameplaySmokeTestReflection.SetField(hidingState, "hiddenSinceTime", Time.time - 5f);
            GameplaySmokeTestReflection.SetField(hidingState, "hiddenBreathRecoveryDelay", 0f);
            GameplaySmokeTestReflection.SetField(hidingState, "calmHiddenBreathRecoveryRate", 0.2f);

            float before = hidingState.BreathTension01;

            GameplaySmokeTestReflection.Invoke(hidingState, "UpdateHiddenBreathTension", 1f);

            Assert.That(hidingState.BreathTension01, Is.LessThan(before));
        }

        [Test]
        public void HidingBreath_RecoveryReportsSteadyHiddenLoop()
        {
            PlayerHidingState hidingState = context.AddInitializedComponent<PlayerHidingState>(
                context.CreateObject("Player"));
            hidingState.SetHidden(true);
            hidingState.AddBreathTension(0.58f);
            GameplaySmokeTestReflection.SetField(hidingState, "hiddenSinceTime", Time.time - 5f);
            GameplaySmokeTestReflection.SetField(hidingState, "hiddenBreathRecoveryDelay", 0f);
            GameplaySmokeTestReflection.SetField(hidingState, "calmHiddenBreathRecoveryRate", 0.5f);
            GameplaySmokeTestReflection.SetField(hidingState, "recoveredBreathTensionThreshold", 0.25f);

            PlayerFeedbackEvents.HidingFeedback hidingFeedback = default;
            PlayerFeedbackEvents.StealthLoopFeedback stealthFeedback = default;
            bool hidingReported = false;
            bool stealthReported = false;
            PlayerFeedbackEvents.HidingChanged += HandleHidingChanged;
            PlayerFeedbackEvents.StealthLoopChanged += HandleStealthLoopChanged;
            try
            {
                GameplaySmokeTestReflection.Invoke(hidingState, "UpdateHiddenBreathTension", 1f);
            }
            finally
            {
                PlayerFeedbackEvents.HidingChanged -= HandleHidingChanged;
                PlayerFeedbackEvents.StealthLoopChanged -= HandleStealthLoopChanged;
            }

            Assert.That(hidingReported, Is.True);
            Assert.That(stealthReported, Is.True);
            Assert.That(hidingFeedback.Kind, Is.EqualTo(PlayerFeedbackEvents.HidingFeedbackKind.Recovered));
            Assert.That(hidingFeedback.IsHidden, Is.True);
            Assert.That(hidingFeedback.BreathTension, Is.LessThanOrEqualTo(0.25f));
            Assert.That(stealthFeedback.Phase, Is.EqualTo(PlayerFeedbackEvents.StealthLoopPhase.Hiding));
            Assert.That(stealthFeedback.IsCalming, Is.True);
            Assert.That(stealthFeedback.Message, Is.EqualTo("Breathing under control."));
            Assert.That(stealthFeedback.Tension, Is.LessThanOrEqualTo(0.25f));

            void HandleHidingChanged(PlayerFeedbackEvents.HidingFeedback feedback)
            {
                hidingFeedback = feedback;
                hidingReported = true;
            }

            void HandleStealthLoopChanged(PlayerFeedbackEvents.StealthLoopFeedback feedback)
            {
                stealthFeedback = feedback;
                stealthReported = true;
            }
        }

        [Test]
        public void AwarenessHud_ShowsHidingRecoveryFeedback()
        {
            PlayerAwarenessHudView hud = context.AddInitializedComponent<PlayerAwarenessHudView>(
                context.CreateObject("AwarenessHud"));

            GameplaySmokeTestReflection.Invoke(
                hud,
                "HandleHidingChanged",
                new PlayerFeedbackEvents.HidingFeedback(
                    "closet",
                    PlayerFeedbackEvents.HidingFeedbackKind.Recovered,
                    0.12f,
                    true,
                    false));

            Text warningText = GameplaySmokeTestReflection.GetField<Text>(hud, "warningText");
            Assert.That(warningText.text, Is.EqualTo("BREATH STEADY"));
        }

        [Test]
        public void HidingBreath_BuildsWhenNeighborSearchesNearby()
        {
            PlayerHidingState hidingState = context.AddInitializedComponent<PlayerHidingState>(
                context.CreateObject("Player"));
            hidingState.SetHidden(true);
            hidingState.AddBreathTension(0.18f);
            GameplaySmokeTestReflection.SetField(hidingState, "hiddenSinceTime", Time.time - 5f);
            GameplaySmokeTestReflection.SetField(hidingState, "hiddenBreathGraceTime", 0f);
            GameplaySmokeTestReflection.SetField(hidingState, "breathTensionBuildRate", 0.2f);

            GameObject neighborObject = context.CreateObject("Neighbor");
            neighborObject.transform.position = Vector3.forward;
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(neighborObject);
            GameplaySmokeTestReflection.SetField(brain, "currentState", NeighborBrain.BehaviorState.HuntMode);
            GameplaySmokeTestReflection.SetField(hidingState, "dangerNeighbor", brain);

            float before = hidingState.BreathTension01;

            GameplaySmokeTestReflection.Invoke(hidingState, "UpdateHiddenBreathTension", 1f);

            Assert.That(hidingState.BreathTension01, Is.GreaterThan(before));
        }

        [Test]
        public void NeighborMemory_RemembersDoorObjectBrokenGlassAndStolenKeyClues()
        {
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(context.CreateObject("Neighbor"));
            brain.transform.position = Vector3.zero;

            Door door = context.AddInitializedComponent<Door>("RememberedDoor");
            door.SetLocked(false, false, false);
            Transform opener = context.CreateObject("PlayerOpener").transform;
            opener.position = Vector3.forward;
            Assert.That(door.TryOpenFor(opener), Is.True);

            Pickupable movedObject = CreatePickupable("MovedObject", new Vector3(0.5f, 0f, 0f), false);
            movedObject.transform.position = new Vector3(2f, 0f, 0f);
            GameplaySmokeTestReflection.Invoke(brain, "TryNoticeObjectLocationChanges");

            Pickupable stolenKey = CreatePickupable("StolenBasementKey", new Vector3(0.75f, 0f, 0f), true);
            stolenKey.transform.position = new Vector3(2.5f, 0f, 0f);
            GameplaySmokeTestReflection.SetField(brain, "nextObjectLocationCheckTime", float.NegativeInfinity);
            GameplaySmokeTestReflection.Invoke(brain, "TryNoticeObjectLocationChanges");

            GameObject glassObject = context.CreateObject("BrokenKitchenWindow");
            glassObject.AddComponent<BoxCollider>();
            GlassShatter glass = context.AddInitializedComponent<GlassShatter>(glassObject);
            glass.ShatterFromPlayer(Vector3.zero, Vector3.right, null);

            Assert.That(brain.RememberedOpenedDoorCount, Is.EqualTo(1));
            Assert.That(brain.RememberedMovedObjectCount, Is.EqualTo(1));
            Assert.That(brain.RememberedStolenKeyCount, Is.EqualTo(1));
            Assert.That(brain.RememberedBrokenGlassCount, Is.EqualTo(1));
            Assert.That(brain.TotalRememberedClueCount, Is.EqualTo(4));
            Assert.That(brain.LastRememberedClueKind, Is.EqualTo(PlayerFeedbackEvents.NeighborMemoryClueKind.GlassBroken));
        }

        [Test]
        public void NeighborMemory_StolenInventoryKeyQueuesFollowUpAtHome()
        {
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(context.CreateObject("Neighbor"));
            brain.transform.position = Vector3.zero;
            Pickupable stolenKey = CreatePickupable("StolenBasementKey", new Vector3(0.75f, 0f, 0f), true);
            GameObject inventory = context.CreateObject("PlayerInventory");

            stolenKey.StoreInInventory(inventory.transform);
            GameplaySmokeTestReflection.Invoke(brain, "TryNoticeObjectLocationChanges");

            Assert.That(brain.RememberedStolenKeyCount, Is.EqualTo(1));
            Assert.That(brain.LastRememberedClueKind, Is.EqualTo(PlayerFeedbackEvents.NeighborMemoryClueKind.KeyStolen));
            Assert.That(brain.HasPendingMemoryClueFollowUp, Is.True);
            Assert.That(brain.PendingMemoryClueKind, Is.EqualTo(PlayerFeedbackEvents.NeighborMemoryClueKind.KeyStolen));
            Assert.That(brain.PendingMemoryClueSource, Is.SameAs(stolenKey.gameObject));
            Assert.That(brain.PendingMemoryCluePosition, Is.EqualTo(stolenKey.HomePosition));
        }

        [Test]
        public void NeighborMemory_StrongerPendingTrailSurvivesWeakerLaterClue()
        {
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(context.CreateObject("Neighbor"));
            Pickupable stolenKey = CreatePickupable("StolenBasementKey", new Vector3(0.75f, 0f, 0f), true);
            DoorKey key = stolenKey.GetComponent<DoorKey>();
            Door door = context.AddInitializedComponent<Door>("LaterOpenedSideDoor");
            door.SetLocked(false, false, false);
            door.transform.position = new Vector3(3f, 0f, 0f);

            GameplaySmokeTestReflection.Invoke(
                brain,
                "RememberMemoryClue",
                PlayerFeedbackEvents.NeighborMemoryClueKind.KeyStolen,
                key,
                stolenKey.HomePosition,
                0.66f);

            float keyTrailScore = brain.PendingMemoryClueScore;

            GameplaySmokeTestReflection.Invoke(
                brain,
                "RememberMemoryClue",
                PlayerFeedbackEvents.NeighborMemoryClueKind.DoorOpened,
                door,
                door.transform.position,
                0.26f);

            Assert.That(brain.RememberedStolenKeyCount, Is.EqualTo(1));
            Assert.That(brain.RememberedOpenedDoorCount, Is.EqualTo(1));
            Assert.That(brain.LastRememberedClueKind, Is.EqualTo(PlayerFeedbackEvents.NeighborMemoryClueKind.DoorOpened));
            Assert.That(brain.HasPendingMemoryClueFollowUp, Is.True);
            Assert.That(brain.PendingMemoryClueKind, Is.EqualTo(PlayerFeedbackEvents.NeighborMemoryClueKind.KeyStolen));
            Assert.That(brain.PendingMemoryClueSource, Is.SameAs(stolenKey.gameObject));
            Assert.That(brain.PendingMemoryCluePosition, Is.EqualTo(stolenKey.HomePosition));
            Assert.That(brain.PendingMemoryClueScore, Is.EqualTo(keyTrailScore).Within(0.001f));
        }

        [Test]
        public void NeighborMemory_StackedDifferentCluesRaiseVigilancePressure()
        {
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(context.CreateObject("Neighbor"));
            Door door = context.AddInitializedComponent<Door>("RememberedDoor");
            door.SetLocked(false, false, false);
            GameObject glassObject = context.CreateObject("BrokenKitchenWindow");
            GlassShatter glass = context.AddInitializedComponent<GlassShatter>(glassObject);
            PlayerFeedbackEvents.NeighborMemoryFeedback feedback = default;
            bool received = false;
            PlayerFeedbackEvents.StealthLoopFeedback stealthFeedback = default;
            bool receivedStealthLoop = false;
            PlayerFeedbackEvents.NeighborMemoryChanged += HandleNeighborMemoryChanged;
            PlayerFeedbackEvents.StealthLoopChanged += HandleStealthLoopChanged;

            try
            {
                GameplaySmokeTestReflection.Invoke(
                    brain,
                    "RememberMemoryClue",
                    PlayerFeedbackEvents.NeighborMemoryClueKind.DoorOpened,
                    door,
                    door.transform.position,
                    0.26f);

                Assert.That(brain.TotalRememberedClueCount, Is.EqualTo(1));
                Assert.That(brain.Suspicion, Is.EqualTo(0.26f).Within(0.001f));
                Assert.That(brain.IsPostEncounterVigilant, Is.False);
                Assert.That(brain.RememberedClueTension01, Is.GreaterThan(0.2f));
                receivedStealthLoop = false;

                GameplaySmokeTestReflection.Invoke(
                    brain,
                    "RememberMemoryClue",
                    PlayerFeedbackEvents.NeighborMemoryClueKind.GlassBroken,
                    glass,
                    glass.transform.position,
                    0.56f);

                Assert.That(brain.TotalRememberedClueCount, Is.EqualTo(2));
                Assert.That(brain.Suspicion, Is.GreaterThan(0.56f));
                Assert.That(brain.CurrentSuspicionLevel, Is.EqualTo(NeighborBrain.SuspicionLevel.Suspicious));
                Assert.That(brain.IsPostEncounterVigilant, Is.True);
                Assert.That(brain.PostEncounterVigilanceTimeRemaining, Is.GreaterThan(0f));
                Assert.That(brain.PendingMemoryClueSuspicion, Is.GreaterThan(0.56f));
                Assert.That(received, Is.True);
                Assert.That(feedback.Kind, Is.EqualTo(PlayerFeedbackEvents.NeighborMemoryClueKind.GlassBroken));
                Assert.That(feedback.TotalMemoryCount, Is.EqualTo(2));
                Assert.That(feedback.Suspicion, Is.GreaterThan(0.56f));
                Assert.That(receivedStealthLoop, Is.True);
                Assert.That(stealthFeedback.Phase, Is.EqualTo(PlayerFeedbackEvents.StealthLoopPhase.Suspicious));
                Assert.That(stealthFeedback.Tension, Is.GreaterThan(0.5f));
                Assert.That(
                    stealthFeedback.Message.Contains("remembers") || stealthFeedback.Message.Contains("trail"),
                    Is.True);
            }
            finally
            {
                PlayerFeedbackEvents.NeighborMemoryChanged -= HandleNeighborMemoryChanged;
                PlayerFeedbackEvents.StealthLoopChanged -= HandleStealthLoopChanged;
            }

            void HandleNeighborMemoryChanged(PlayerFeedbackEvents.NeighborMemoryFeedback item)
            {
                feedback = item;
                received = true;
            }

            void HandleStealthLoopChanged(PlayerFeedbackEvents.StealthLoopFeedback item)
            {
                stealthFeedback = item;
                receivedStealthLoop = true;
            }
        }

        [Test]
        public void NeighborMemory_RespawnRequeuesStrongestRememberedClue()
        {
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(context.CreateObject("Neighbor"));
            Door door = context.AddInitializedComponent<Door>("RememberedDoor");
            door.SetLocked(false, false, false);
            GameObject glassObject = context.CreateObject("BrokenKitchenWindow");
            GlassShatter glass = context.AddInitializedComponent<GlassShatter>(glassObject);
            Pickupable stolenKey = CreatePickupable("StolenBasementKey", new Vector3(0.75f, 0f, 0f), true);
            DoorKey key = stolenKey.GetComponent<DoorKey>();
            GameObject inventory = context.CreateObject("PlayerInventory");
            stolenKey.StoreInInventory(inventory.transform);

            GameplaySmokeTestReflection.Invoke(
                brain,
                "RememberMemoryClue",
                PlayerFeedbackEvents.NeighborMemoryClueKind.DoorOpened,
                door,
                door.transform.position,
                0.26f);
            GameplaySmokeTestReflection.Invoke(
                brain,
                "RememberMemoryClue",
                PlayerFeedbackEvents.NeighborMemoryClueKind.GlassBroken,
                glass,
                glass.transform.position,
                0.56f);
            GameplaySmokeTestReflection.Invoke(
                brain,
                "RememberMemoryClue",
                PlayerFeedbackEvents.NeighborMemoryClueKind.KeyStolen,
                key,
                stolenKey.HomePosition,
                0.66f);
            PlayerFeedbackEvents.NeighborMemoryFeedback feedback = default;
            bool received = false;
            PlayerFeedbackEvents.NeighborMemoryChanged += HandleNeighborMemoryChanged;

            try
            {
                brain.HandlePlayerRespawned(0f);

                Assert.That(brain.TotalRememberedClueCount, Is.EqualTo(3));
                Assert.That(brain.HasPendingMemoryClueFollowUp, Is.True);
                Assert.That(brain.PendingMemoryClueKind, Is.EqualTo(PlayerFeedbackEvents.NeighborMemoryClueKind.KeyStolen));
                Assert.That(brain.PendingMemoryClueSource, Is.SameAs(stolenKey.gameObject));
                Assert.That(brain.PendingMemoryCluePosition, Is.EqualTo(stolenKey.HomePosition));
                Assert.That(received, Is.True);
                Assert.That(feedback.Kind, Is.EqualTo(PlayerFeedbackEvents.NeighborMemoryClueKind.KeyStolen));
                Assert.That(feedback.TotalMemoryCount, Is.EqualTo(3));
                Assert.That(feedback.Suspicion, Is.GreaterThan(0.65f));
            }
            finally
            {
                PlayerFeedbackEvents.NeighborMemoryChanged -= HandleNeighborMemoryChanged;
            }

            void HandleNeighborMemoryChanged(PlayerFeedbackEvents.NeighborMemoryFeedback item)
            {
                feedback = item;
                received = true;
            }
        }

        [Test]
        public void NeighborMemory_RememberedGlassStartsFollowUpInvestigation()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            NavMeshSurface surface = null;
            try
            {
                ground.name = "TemporaryMemoryFollowUpNavMeshGround";
                ground.transform.position = new Vector3(0f, -0.1f, 0f);
                ground.transform.localScale = new Vector3(10f, 0.2f, 10f);
                surface = ground.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.All;
                surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
                surface.layerMask = ~0;
                surface.defaultArea = 0;
                surface.BuildNavMesh();

                GameObject neighborObject = context.CreateObject("Neighbor");
                neighborObject.transform.position = Vector3.zero;
                NavMeshAgent agent = neighborObject.AddComponent<NavMeshAgent>();
                agent.radius = 0.3f;
                agent.height = 2f;
                agent.stoppingDistance = 0.1f;
                context.AddInitializedComponent<NeighborMotor>(neighborObject);
                NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(neighborObject);

                GameObject glassObject = context.CreateObject("BrokenKitchenWindow");
                glassObject.transform.position = new Vector3(2f, 0f, 1f);
                GlassShatter glass = context.AddInitializedComponent<GlassShatter>(glassObject);

                GameplaySmokeTestReflection.Invoke(
                    brain,
                    "RememberMemoryClue",
                    PlayerFeedbackEvents.NeighborMemoryClueKind.GlassBroken,
                    glass,
                    glassObject.transform.position,
                    0.65f);

                Assert.That(brain.HasPendingMemoryClueFollowUp, Is.True);

                GameplaySmokeTestReflection.Invoke(brain, "ChooseNextRoutineGoal");

                Assert.That(brain.CurrentState, Is.EqualTo(NeighborBrain.BehaviorState.Investigate));
                Assert.That(brain.HasActiveInvestigation, Is.True);
                Assert.That(brain.CurrentInvestigationSource, Is.SameAs(glassObject));
                Assert.That(brain.LastKnownInvestigationPosition, Is.EqualTo(glassObject.transform.position));
                Assert.That(brain.HasPendingMemoryClueFollowUp, Is.False);
                Assert.That(brain.CurrentSuspicionLevel, Is.EqualTo(NeighborBrain.SuspicionLevel.Suspicious));
            }
            finally
            {
                if (surface != null)
                {
                    surface.RemoveData();
                }

                UnityEngine.Object.DestroyImmediate(ground);
            }
        }

        [Test]
        public void NeighborMemory_PostChaseHuntUsesPendingClueAsTrailWaypoint()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            NavMeshSurface surface = null;
            try
            {
                ground.name = "TemporaryMemoryHuntNavMeshGround";
                ground.transform.position = new Vector3(0f, -0.1f, 0f);
                ground.transform.localScale = new Vector3(10f, 0.2f, 10f);
                surface = ground.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.All;
                surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
                surface.layerMask = ~0;
                surface.defaultArea = 0;
                surface.BuildNavMesh();

                GameObject neighborObject = context.CreateObject("Neighbor");
                neighborObject.transform.position = Vector3.zero;
                NavMeshAgent agent = neighborObject.AddComponent<NavMeshAgent>();
                agent.radius = 0.3f;
                agent.height = 2f;
                agent.stoppingDistance = 0.1f;
                Assert.That(NavMesh.SamplePosition(Vector3.zero, out NavMeshHit startHit, 2f, NavMesh.AllAreas), Is.True);
                neighborObject.transform.position = startHit.position;
                context.AddInitializedComponent<NeighborMotor>(neighborObject);
                NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(neighborObject);

                GameObject glassObject = context.CreateObject("BrokenKitchenWindow");
                glassObject.transform.position = new Vector3(2f, 0f, 1f);
                GlassShatter glass = context.AddInitializedComponent<GlassShatter>(glassObject);
                PlayerFeedbackEvents.NeighborInvestigationFeedback investigationFeedback = default;
                bool receivedInvestigation = false;
                PlayerFeedbackEvents.NeighborInvestigationChanged += HandleNeighborInvestigationChanged;

                try
                {
                    GameplaySmokeTestReflection.Invoke(
                        brain,
                        "RememberMemoryClue",
                        PlayerFeedbackEvents.NeighborMemoryClueKind.GlassBroken,
                        glass,
                        glassObject.transform.position,
                        0.65f);

                    Assert.That(brain.HasPendingMemoryClueFollowUp, Is.True);

                    GameplaySmokeTestReflection.Invoke(
                        brain,
                        "BeginHuntMode",
                        new Vector3(0.4f, 0f, 0.2f));

                    Assert.That(brain.CurrentState, Is.EqualTo(NeighborBrain.BehaviorState.HuntMode));
                    Assert.That(brain.IsHuntingMemoryClue, Is.True);
                    Assert.That(brain.CurrentHuntMemoryClueKind, Is.EqualTo(PlayerFeedbackEvents.NeighborMemoryClueKind.GlassBroken));
                    Assert.That(brain.CurrentHuntMemoryClueSource, Is.SameAs(glassObject));
                    Assert.That(brain.CurrentInvestigationSource, Is.SameAs(glassObject));
                    Assert.That(brain.HasPendingMemoryClueFollowUp, Is.False);
                    Assert.That(Vector3.Distance(brain.CurrentGoal, glassObject.transform.position), Is.LessThan(1.6f));
                    Assert.That(receivedInvestigation, Is.True);
                    Assert.That(investigationFeedback.Kind, Is.EqualTo(PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.FollowingTrail));
                    Assert.That(investigationFeedback.SourceName, Is.EqualTo(glassObject.name));
                    Assert.That(investigationFeedback.Urgency, Is.GreaterThanOrEqualTo(0.65f));
                }
                finally
                {
                    PlayerFeedbackEvents.NeighborInvestigationChanged -= HandleNeighborInvestigationChanged;
                }

                void HandleNeighborInvestigationChanged(PlayerFeedbackEvents.NeighborInvestigationFeedback item)
                {
                    investigationFeedback = item;
                    receivedInvestigation = true;
                }
            }
            finally
            {
                if (surface != null)
                {
                    surface.RemoveData();
                }

                UnityEngine.Object.DestroyImmediate(ground);
            }
        }

        [Test]
        public void ReinforcementTrigger_RecognizesCameraAndTrapPreferredPlacements()
        {
            GameObject triggerObject = context.CreateObject("ReinforcementTrigger");
            triggerObject.AddComponent<BoxCollider>();
            ReinforcementTrigger trigger = context.AddInitializedComponent<ReinforcementTrigger>(triggerObject);
            GameObject cameraPrefab = context.CreateObject("CameraPrefab");
            cameraPrefab.AddComponent<BoxCollider>();
            cameraPrefab.AddComponent<Pickupable>();
            cameraPrefab.AddComponent<SecurityCamera>();
            GameObject trapPrefab = context.CreateObject("TrapPrefab");
            trapPrefab.AddComponent<BoxCollider>();
            trapPrefab.AddComponent<SpringLoadedBoxingGloveTrap>();
            GameObject genericPrefab = context.CreateObject("GenericPrefab");
            genericPrefab.AddComponent<BoxCollider>();

            Type placementKindType = typeof(ReinforcementTrigger).GetNestedType(
                "ReinforcementPlacementKind",
                BindingFlags.NonPublic);
            Assert.That(placementKindType, Is.Not.Null);
            MethodInfo canUsePrefab = typeof(ReinforcementTrigger).GetMethod(
                "CanUsePrefab",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(canUsePrefab, Is.Not.Null);

            object cameraKind = Enum.Parse(placementKindType, "SecurityCamera");
            object trapKind = Enum.Parse(placementKindType, "Trap");
            object genericKind = Enum.Parse(placementKindType, "Generic");

            Assert.That((bool)canUsePrefab.Invoke(trigger, new[] { cameraPrefab, cameraKind }), Is.True);
            Assert.That((bool)canUsePrefab.Invoke(trigger, new[] { trapPrefab, cameraKind }), Is.False);
            Assert.That((bool)canUsePrefab.Invoke(trigger, new[] { trapPrefab, trapKind }), Is.True);
            Assert.That((bool)canUsePrefab.Invoke(trigger, new[] { cameraPrefab, trapKind }), Is.False);
            Assert.That((bool)canUsePrefab.Invoke(trigger, new[] { cameraPrefab, genericKind }), Is.False);
            Assert.That((bool)canUsePrefab.Invoke(trigger, new[] { genericPrefab, genericKind }), Is.True);
        }

        [Test]
        public void DoorSecurity_OnlyTargetsUnexpectedPlayerOpenedDoors()
        {
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>();
            Door authoredOpenDoor = context.AddInitializedComponent<Door>("AuthoredOpenDoor");
            GameplaySmokeTestReflection.SetField(authoredOpenDoor, "isOpen", true);
            Door playerOpenedDoor = context.AddInitializedComponent<Door>("PlayerOpenedDoor");
            playerOpenedDoor.SetLocked(false, false, false);
            Transform opener = context.CreateObject("DoorOpener").transform;

            Assert.That(playerOpenedDoor.TryOpenFor(opener), Is.True);

            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<bool>(
                    brain,
                    "IsOpenSecurityDoorCandidate",
                    playerOpenedDoor),
                Is.True);
            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<bool>(
                    brain,
                    "IsOpenSecurityDoorCandidate",
                    authoredOpenDoor),
                Is.False);
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
        public void Investigation_TurnsOffNoisyTelevisionAtSource()
        {
            GameObject tvObject = context.CreateObject("Television");
            tvObject.AddComponent<BoxCollider>();
            Television television = context.AddInitializedComponent<Television>(tvObject);
            television.SetOn(true, false);

            GameObject neighborObject = context.CreateObject("Neighbor");
            neighborObject.transform.position = tvObject.transform.position + Vector3.forward;
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(neighborObject);
            GameplaySmokeTestReflection.SetField(brain, "currentState", NeighborBrain.BehaviorState.Investigate);
            GameplaySmokeTestReflection.SetField(brain, "currentInvestigationSource", tvObject);

            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<bool>(
                    brain,
                    "TryResolveTelevisionInvestigationSource"),
                Is.True);
            Assert.That(television.IsOn, Is.False);
        }

        [Test]
        public void GarageDoorSecurity_TargetsPlayerOpenedWideDoor()
        {
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>();
            GameplaySmokeTestReflection.SetField(brain, "garageDoorSearchRadius", 8f);
            GameplaySmokeTestReflection.SetField(brain, "garageDoorSecurityRadius", 0f);
            GameplaySmokeTestReflection.SetField(brain, "closePlayerGarageDoorsWithoutRouteProof", true);

            GameObject garageObject = context.CreateObject("GarageDoor");
            garageObject.transform.position = Vector3.right * 100f;
            GameObject panelObject = context.CreateObject("Door Panel");
            panelObject.transform.SetParent(garageObject.transform, false);
            HouseGarageDoorMotion garageDoor = context.AddInitializedComponent<HouseGarageDoorMotion>(garageObject);
            garageDoor.Configure(panelObject.transform, Vector3.zero, Vector3.up, 0.01f, false);
            garageDoor.Open();

            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<bool>(
                    brain,
                    "IsGarageDoorCandidate",
                    garageDoor,
                    brain.transform.position,
                    false),
                Is.True);

            garageDoor.Close();
            garageDoor.MarkNextChangeAsNeighborRequested();
            garageDoor.Open();

            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<bool>(
                    brain,
                    "IsGarageDoorCandidate",
                    garageDoor,
                    brain.transform.position,
                    false),
                Is.True);

            GameplaySmokeTestReflection.SetField(brain, "closeAnyOpenGarageDoorForSecurity", false);
            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<bool>(
                    brain,
                    "IsGarageDoorCandidate",
                    garageDoor,
                    brain.transform.position,
                    false),
                Is.False);
        }

        [Test]
        public void GarageDoorSecurity_TargetsNeighborOpenedDoorPastTightSecurityRadius()
        {
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>();
            GameplaySmokeTestReflection.SetField(brain, "garageDoorSearchRadius", 20f);
            GameplaySmokeTestReflection.SetField(brain, "garageDoorSecurityRadius", 4f);
            GameplaySmokeTestReflection.SetField(brain, "closeAnyOpenGarageDoorForSecurity", true);

            GameObject garageObject = context.CreateObject("GarageDoor");
            garageObject.transform.position = Vector3.right * 12f;
            GameObject panelObject = context.CreateObject("Door Panel");
            panelObject.transform.SetParent(garageObject.transform, false);
            HouseGarageDoorMotion garageDoor = context.AddInitializedComponent<HouseGarageDoorMotion>(garageObject);
            garageDoor.Configure(panelObject.transform, Vector3.zero, Vector3.up, 0.01f, false);
            garageDoor.MarkNextChangeAsNeighborRequested();
            garageDoor.Open();

            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<bool>(
                    brain,
                    "IsGarageDoorCandidate",
                    garageDoor,
                    brain.transform.position,
                    false),
                Is.True);
        }

        [Test]
        public void GarageDoorUse_WaitsForGarageDoorToFullyClose()
        {
            GameObject neighborObject = context.CreateObject("Neighbor");
            NeighborMotor motor = context.AddInitializedComponent<NeighborMotor>(neighborObject);
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(neighborObject);
            GameplaySmokeTestReflection.SetField(brain, "motor", motor);

            GameObject garageObject = context.CreateObject("GarageDoor");
            GameObject panelObject = context.CreateObject("Door Panel");
            panelObject.transform.SetParent(garageObject.transform, false);
            HouseGarageDoorMotion garageDoor = context.AddInitializedComponent<HouseGarageDoorMotion>(garageObject);
            garageDoor.Configure(panelObject.transform, Vector3.zero, Vector3.up, 1f, true);
            GameplaySmokeTestReflection.SetField(garageDoor, "targetProgress", 0f);
            GameplaySmokeTestReflection.SetField(garageDoor, "progress", 0.5f);

            GameObject switchObject = context.CreateObject("GarageSwitch");
            switchObject.AddComponent<BoxCollider>();
            LightSwitch lightSwitch = context.AddInitializedComponent<LightSwitch>(switchObject);

            GameplaySmokeTestReflection.SetField(brain, "currentState", NeighborBrain.BehaviorState.GarageDoorUse);
            GameplaySmokeTestReflection.SetField(brain, "activeGarageDoor", garageDoor);
            GameplaySmokeTestReflection.SetField(brain, "activeGarageSwitch", lightSwitch);
            GameplaySmokeTestReflection.SetField(brain, "activeGarageDesiredOpen", false);
            GameplaySmokeTestReflection.SetField(brain, "garageSwitchToggled", true);
            GameplaySmokeTestReflection.SetField(brain, "garageDoorWaitUntilTime", Time.time + 10f);

            GameplaySmokeTestReflection.Invoke(brain, "UpdateGarageDoorUse");

            Assert.That(brain.CurrentState, Is.EqualTo(NeighborBrain.BehaviorState.GarageDoorUse));

            GameplaySmokeTestReflection.SetField(garageDoor, "progress", 0f);
            GameplaySmokeTestReflection.Invoke(brain, "UpdateGarageDoorUse");

            Assert.That(brain.CurrentState, Is.Not.EqualTo(NeighborBrain.BehaviorState.GarageDoorUse));
        }

        [Test]
        public void ObjectTask_ReservesAndProtectsMovableFurniture()
        {
            GameObject chair = context.CreateObject("ChairTask");
            BoxCollider chairCollider = chair.AddComponent<BoxCollider>();
            Rigidbody chairBody = chair.AddComponent<Rigidbody>();
            NeighborTaskLocation task = context.AddInitializedComponent<NeighborTaskLocation>(chair);
            GameplaySmokeTestReflection.SetField(task, "taskObjectRoot", chair);
            GameplaySmokeTestReflection.SetField(task, "taskObjectBody", chairBody);
            GameplaySmokeTestReflection.SetField(task, "ignoreTaskObjectCollisions", true);
            GameplaySmokeTestReflection.SetField(task, "stabilizeTaskObject", true);

            GameObject neighborObject = context.CreateObject("Neighbor");
            BoxCollider neighborCollider = neighborObject.AddComponent<BoxCollider>();
            NeighborBrain neighbor = context.AddInitializedComponent<NeighborBrain>(neighborObject);
            NeighborBrain otherNeighbor = context.AddInitializedComponent<NeighborBrain>();

            Assert.That(task.TryReserve(neighbor), Is.True);
            Assert.That(task.TryReserve(otherNeighbor), Is.False);
            Assert.That(chairBody.isKinematic, Is.True);
            Assert.That(chairBody.useGravity, Is.False);
            Assert.That(Physics.GetIgnoreCollision(chairCollider, neighborCollider), Is.True);

            task.EndTaskUse(neighbor, null);

            Assert.That(chairBody.isKinematic, Is.False);
            Assert.That(chairBody.useGravity, Is.True);
            Assert.That(Physics.GetIgnoreCollision(chairCollider, neighborCollider), Is.False);
            Assert.That(task.IsAvailable, Is.True);
        }

        [Test]
        public void FallenChairTask_IsUnavailableUntilPlacedUpright()
        {
            GameObject chair = context.CreateObject("ChairTask");
            chair.AddComponent<BoxCollider>();
            Rigidbody chairBody = chair.AddComponent<Rigidbody>();
            NeighborTaskLocation task = context.AddInitializedComponent<NeighborTaskLocation>(chair);
            GameplaySmokeTestReflection.SetField(
                task,
                "objectTaskType",
                NeighborTaskLocation.ObjectTaskType.Sit);
            GameplaySmokeTestReflection.SetField(task, "taskObjectBody", chairBody);
            chairBody.isKinematic = true;

            chair.transform.rotation = Quaternion.Euler(0f, 0f, 90f);

            Assert.That(task.IsAvailable, Is.False);
            Assert.That(task.NeedsObjectRecovery, Is.True);

            chair.transform.rotation = Quaternion.Euler(0f, 35f, 0f);

            Assert.That(task.IsAvailable, Is.True);
            Assert.That(task.NeedsObjectRecovery, Is.False);
        }

        [Test]
        public void ChairTask_DoesNotBeginFromAboveNavigationPoint()
        {
            GameObject chair = context.CreateObject("ChairTask");
            chair.AddComponent<BoxCollider>();
            Rigidbody chairBody = chair.AddComponent<Rigidbody>();
            chairBody.isKinematic = true;
            NeighborTaskLocation task = context.AddInitializedComponent<NeighborTaskLocation>(chair);
            GameplaySmokeTestReflection.SetField(
                task,
                "objectTaskType",
                NeighborTaskLocation.ObjectTaskType.Sit);
            GameplaySmokeTestReflection.SetField(task, "taskObjectBody", chairBody);
            GameplaySmokeTestReflection.SetField(task, "maximumUseVerticalOffset", 0.45f);

            GameObject neighborObject = context.CreateObject("Neighbor");
            neighborObject.transform.position = chair.transform.position + Vector3.up * 1f;
            NeighborMotor motor = context.AddInitializedComponent<NeighborMotor>(neighborObject);
            NeighborBrain neighbor = context.AddInitializedComponent<NeighborBrain>(neighborObject);

            Assert.That(task.TryReserve(neighbor), Is.True);
            Assert.That(task.BeginTaskUse(neighbor, motor), Is.False);
            Assert.That(motor.IsAnchoredForTask, Is.False);
        }

        [Test]
        public void AnchoredTask_ContinuesAfterUsePoseMovesNeighborAwayFromNavigationPoint()
        {
            GameObject chair = context.CreateObject("ChairTask");
            chair.AddComponent<BoxCollider>();
            chair.AddComponent<Rigidbody>();
            NeighborTaskLocation task = context.AddInitializedComponent<NeighborTaskLocation>(chair);
            GameplaySmokeTestReflection.SetField(task, "anchorNeighborAtUsePose", true);
            GameplaySmokeTestReflection.SetField(task, "usePoseLocalOffset", Vector3.forward * 2f);

            GameObject neighborObject = context.CreateObject("Neighbor");
            NeighborMotor motor = context.AddInitializedComponent<NeighborMotor>(neighborObject);
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(neighborObject);
            Assert.That(task.TryReserve(brain), Is.True);
            Assert.That(task.BeginTaskUse(brain, motor), Is.True);
            GameplaySmokeTestReflection.SetField(brain, "currentState", NeighborBrain.BehaviorState.Task);
            GameplaySmokeTestReflection.SetField(brain, "currentTaskLocation", task);
            GameplaySmokeTestReflection.SetField(brain, "waitingAtGoal", true);
            GameplaySmokeTestReflection.SetField(
                brain,
                "currentTaskAnimationPhase",
                NeighborTaskLocation.TaskAnimationPhase.Starting);
            GameplaySmokeTestReflection.SetField(brain, "waitUntilTime", Time.time + 10f);

            GameplaySmokeTestReflection.Invoke(brain, "UpdateRoutine");

            Assert.That(motor.IsAnchoredForTask, Is.True);
            Assert.That(brain.CurrentTaskLocation, Is.SameAs(task));
            Assert.That(
                GameplaySmokeTestReflection.GetField<NeighborTaskLocation.TaskAnimationPhase>(
                    brain,
                    "currentTaskAnimationPhase"),
                Is.EqualTo(NeighborTaskLocation.TaskAnimationPhase.Starting));
        }

        [Test]
        public void AnchoredTask_WithNoActivePhaseRecoversFromPausedState()
        {
            GameObject chair = context.CreateObject("ChairTask");
            chair.AddComponent<BoxCollider>();
            chair.AddComponent<Rigidbody>();
            NeighborTaskLocation task = context.AddInitializedComponent<NeighborTaskLocation>(chair);
            GameplaySmokeTestReflection.SetField(task, "anchorNeighborAtUsePose", true);

            GameObject neighborObject = context.CreateObject("Neighbor");
            NeighborMotor motor = context.AddInitializedComponent<NeighborMotor>(neighborObject);
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(neighborObject);
            Assert.That(task.TryReserve(brain), Is.True);
            Assert.That(task.BeginTaskUse(brain, motor), Is.True);
            GameplaySmokeTestReflection.SetField(brain, "currentState", NeighborBrain.BehaviorState.Task);
            GameplaySmokeTestReflection.SetField(brain, "currentTaskLocation", task);
            GameplaySmokeTestReflection.SetField(brain, "waitingAtGoal", false);
            GameplaySmokeTestReflection.SetField(
                brain,
                "currentTaskAnimationPhase",
                NeighborTaskLocation.TaskAnimationPhase.None);
            GameplaySmokeTestReflection.SetField(brain, "tasksSuppressedUntilTime", float.PositiveInfinity);

            GameplaySmokeTestReflection.Invoke(brain, "UpdateRoutine");

            Assert.That(motor.IsAnchoredForTask, Is.False);
            Assert.That(task.IsAvailable, Is.True);
        }

        [Test]
        public void AnchoredTask_WithNoActivePhaseWhileWaitingRecoversFromPausedState()
        {
            GameObject chair = context.CreateObject("ChairTask");
            chair.AddComponent<BoxCollider>();
            chair.AddComponent<Rigidbody>();
            NeighborTaskLocation task = context.AddInitializedComponent<NeighborTaskLocation>(chair);
            GameplaySmokeTestReflection.SetField(task, "anchorNeighborAtUsePose", true);

            GameObject neighborObject = context.CreateObject("Neighbor");
            NeighborMotor motor = context.AddInitializedComponent<NeighborMotor>(neighborObject);
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(neighborObject);
            Assert.That(task.TryReserve(brain), Is.True);
            Assert.That(task.BeginTaskUse(brain, motor), Is.True);
            GameplaySmokeTestReflection.SetField(brain, "currentState", NeighborBrain.BehaviorState.Task);
            GameplaySmokeTestReflection.SetField(brain, "currentTaskLocation", task);
            GameplaySmokeTestReflection.SetField(brain, "waitingAtGoal", true);
            GameplaySmokeTestReflection.SetField(
                brain,
                "currentTaskAnimationPhase",
                NeighborTaskLocation.TaskAnimationPhase.None);
            GameplaySmokeTestReflection.SetField(brain, "tasksSuppressedUntilTime", float.PositiveInfinity);

            GameplaySmokeTestReflection.Invoke(brain, "UpdateRoutine");

            Assert.That(motor.IsAnchoredForTask, Is.False);
            Assert.That(task.IsAvailable, Is.True);
        }

        [Test]
        public void FinishedNonAnchoredTaskClearsInheritedPause()
        {
            GameObject car = context.CreateObject("CarTask");
            car.AddComponent<BoxCollider>();
            NeighborTaskLocation task = context.AddInitializedComponent<NeighborTaskLocation>(car);

            GameObject neighborObject = context.CreateObject("Neighbor");
            NeighborMotor motor = context.AddInitializedComponent<NeighborMotor>(neighborObject);
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(neighborObject);
            Assert.That(task.TryReserve(brain), Is.True);
            motor.SetPaused(true);
            GameplaySmokeTestReflection.SetField(brain, "currentState", NeighborBrain.BehaviorState.Task);
            GameplaySmokeTestReflection.SetField(brain, "currentTaskLocation", task);
            GameplaySmokeTestReflection.SetField(brain, "waitingAtGoal", true);
            GameplaySmokeTestReflection.SetField(
                brain,
                "currentTaskAnimationPhase",
                NeighborTaskLocation.TaskAnimationPhase.Finishing);

            GameplaySmokeTestReflection.Invoke(brain, "FinishCurrentTaskUse");

            Assert.That(motor.IsPaused, Is.False);
            Assert.That(task.IsAvailable, Is.True);
        }

        [Test]
        public void TaskArrival_UsesSampledDestinationInsteadOfObjectCenter()
        {
            GameObject car = context.CreateObject("CarTask");
            NeighborTaskLocation task = context.AddInitializedComponent<NeighborTaskLocation>(car);
            GameplaySmokeTestReflection.SetField(task, "arrivalDistance", 0.25f);
            GameplaySmokeTestReflection.SetField(task, "maximumUseVerticalOffset", 0.45f);

            GameObject neighborObject = context.CreateObject("Neighbor");
            neighborObject.transform.position = car.transform.position + Vector3.right * 2f;
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(neighborObject);
            GameplaySmokeTestReflection.SetField(brain, "currentState", NeighborBrain.BehaviorState.Task);
            GameplaySmokeTestReflection.SetField(brain, "currentTaskLocation", task);

            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<bool>(
                    brain,
                    "IsAtCurrentTaskUseHeight"),
                Is.True);
            Assert.That(brain.IsAtTaskUsePoint, Is.False);
        }

        [Test]
        public void ObjectHandling_RejectsHeavyAndTaskObjects()
        {
            GameObject neighborObject = context.CreateObject("Neighbor");
            context.AddInitializedComponent<NeighborMotor>(neighborObject);
            context.AddInitializedComponent<NeighborBrain>(neighborObject);
            NeighborObjectHandling objectHandling = context.AddInitializedComponent<NeighborObjectHandling>(neighborObject);
            GameplaySmokeTestReflection.SetField(objectHandling, "maximumPickupMass", 5f);

            GameObject pickupObject = context.CreateObject("Pickup");
            Rigidbody body = pickupObject.AddComponent<Rigidbody>();
            pickupObject.AddComponent<BoxCollider>();
            Pickupable pickup = context.AddInitializedComponent<Pickupable>(pickupObject);

            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<bool>(
                    objectHandling,
                    "IsPickupCandidateValid",
                    pickup),
                Is.True);

            body.mass = 6f;
            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<bool>(
                    objectHandling,
                    "IsPickupCandidateValid",
                    pickup),
                Is.False);

            body.mass = 1f;
            context.AddInitializedComponent<NeighborTaskLocation>(pickupObject);
            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<bool>(
                    objectHandling,
                    "IsPickupCandidateValid",
                    pickup),
                Is.False);
        }

        [Test]
        public void ObjectHandling_AcceptsFallenChairTaskForRecovery()
        {
            GameObject neighborObject = context.CreateObject("Neighbor");
            context.AddInitializedComponent<NeighborMotor>(neighborObject);
            context.AddInitializedComponent<NeighborBrain>(neighborObject);
            NeighborObjectHandling objectHandling = context.AddInitializedComponent<NeighborObjectHandling>(neighborObject);

            GameObject chair = context.CreateObject("FallenChairTask");
            Rigidbody body = chair.AddComponent<Rigidbody>();
            body.isKinematic = true;
            chair.AddComponent<BoxCollider>();
            Pickupable pickup = context.AddInitializedComponent<Pickupable>(chair);
            chair.AddComponent<DoorBlockerChair>();
            NeighborTaskLocation task = context.AddInitializedComponent<NeighborTaskLocation>(chair);
            GameplaySmokeTestReflection.SetField(
                task,
                "objectTaskType",
                NeighborTaskLocation.ObjectTaskType.Sit);
            GameplaySmokeTestReflection.SetField(task, "taskObjectBody", body);
            chair.transform.rotation = Quaternion.Euler(0f, 0f, 90f);

            Assert.That(task.NeedsObjectRecovery, Is.True);
            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<bool>(
                    objectHandling,
                    "IsPickupCandidateValid",
                    pickup),
                Is.True);
        }

        [Test]
        public void ObjectHandling_IgnoresNeighborCollidersWhileCarryingPickup()
        {
            GameObject neighborObject = context.CreateObject("Neighbor");
            BoxCollider neighborCollider = neighborObject.AddComponent<BoxCollider>();
            context.AddInitializedComponent<NeighborMotor>(neighborObject);
            context.AddInitializedComponent<NeighborBrain>(neighborObject);
            NeighborObjectHandling objectHandling = context.AddInitializedComponent<NeighborObjectHandling>(neighborObject);

            GameObject pickupObject = context.CreateObject("Pickup");
            pickupObject.AddComponent<Rigidbody>();
            BoxCollider pickupCollider = pickupObject.AddComponent<BoxCollider>();
            Pickupable pickup = context.AddInitializedComponent<Pickupable>(pickupObject);
            GameplaySmokeTestReflection.SetField(pickup, "disableCollidersWhileHeld", false);

            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<bool>(
                    objectHandling,
                    "TryPickupForCarry",
                    pickup),
                Is.True);

            Assert.That(pickup.IsHeld, Is.True);
            Assert.That(pickupCollider.enabled, Is.True);
            Assert.That(Physics.GetIgnoreCollision(pickupCollider, neighborCollider), Is.True);

            pickup.Place(Vector3.right, Quaternion.identity);

            Assert.That(pickup.IsHeld, Is.False);
            Assert.That(Physics.GetIgnoreCollision(pickupCollider, neighborCollider), Is.False);
        }

        [Test]
        public void ObjectHandling_CancelReleasesHeldObject()
        {
            GameObject neighborObject = context.CreateObject("Neighbor");
            context.AddInitializedComponent<NeighborMotor>(neighborObject);
            context.AddInitializedComponent<NeighborBrain>(neighborObject);
            NeighborObjectHandling objectHandling = context.AddInitializedComponent<NeighborObjectHandling>(neighborObject);
            GameObject pickupObject = context.CreateObject("Pickup");
            pickupObject.AddComponent<Rigidbody>();
            pickupObject.AddComponent<BoxCollider>();
            Pickupable pickup = context.AddInitializedComponent<Pickupable>(pickupObject);
            pickup.Pickup(null, false);
            GameplaySmokeTestReflection.SetField(objectHandling, "heldPickup", pickup);
            objectHandling.EnableObjectHandling = true;

            objectHandling.EnableObjectHandling = false;

            Assert.That(pickup.IsHeld, Is.False);
            Assert.That(objectHandling.EnableObjectHandling, Is.False);
            Assert.That(objectHandling.IsActive, Is.False);
            Assert.That(objectHandling.HeldPickup, Is.Null);
        }

        [Test]
        public void ObjectHandling_PlacesHeldObjectOnClearGround()
        {
            GameObject neighborObject = context.CreateObject("Neighbor");
            context.AddInitializedComponent<NeighborMotor>(neighborObject);
            context.AddInitializedComponent<NeighborBrain>(neighborObject);
            NeighborObjectHandling objectHandling = context.AddInitializedComponent<NeighborObjectHandling>(neighborObject);
            GameObject pickupObject = context.CreateObject("Pickup");
            pickupObject.AddComponent<Rigidbody>();
            pickupObject.AddComponent<BoxCollider>();
            Pickupable pickup = context.AddInitializedComponent<Pickupable>(pickupObject);
            GameObject ground = context.CreateObject("Ground");
            BoxCollider groundCollider = ground.AddComponent<BoxCollider>();
            groundCollider.size = new Vector3(8f, 0.2f, 8f);
            ground.transform.position = Vector3.down * 0.1f;
            Physics.SyncTransforms();

            pickup.Pickup(null, false);
            GameplaySmokeTestReflection.SetField(objectHandling, "heldPickup", pickup);
            GameplaySmokeTestReflection.Invoke(objectHandling, "CacheHeldBounds", pickup);
            GameplaySmokeTestReflection.Invoke(objectHandling, "PlaceHeldObjectNear", Vector3.zero);

            Assert.That(pickup.IsHeld, Is.False);
            Assert.That(pickup.transform.position.y, Is.GreaterThan(0.45f));
            Assert.That(pickup.transform.position.y, Is.LessThan(0.6f));
        }

        [Test]
        public void ObjectHandling_DefaultCarryPoseUsesRootFallback()
        {
            GameObject neighborObject = context.CreateObject("Neighbor");
            neighborObject.transform.position = new Vector3(3f, 0f, -2f);
            context.AddInitializedComponent<NeighborMotor>(neighborObject);
            context.AddInitializedComponent<NeighborBrain>(neighborObject);
            NeighborObjectHandling objectHandling = context.AddInitializedComponent<NeighborObjectHandling>(neighborObject);
            Transform unstableHand = context.CreateObject("AnimatedHand").transform;
            unstableHand.SetParent(neighborObject.transform);
            unstableHand.localPosition = Vector3.up * 9f;
            GameplaySmokeTestReflection.SetField(objectHandling, "resolvedCarryAnchor", unstableHand);
            GameplaySmokeTestReflection.SetField(objectHandling, "fallbackCarryOffset", new Vector3(0.4f, 1.1f, 0.3f));

            GameObject pickupObject = context.CreateObject("Pickup");
            pickupObject.AddComponent<Rigidbody>();
            pickupObject.AddComponent<BoxCollider>();
            Pickupable pickup = context.AddInitializedComponent<Pickupable>(pickupObject);
            pickup.Pickup(null, false);
            GameplaySmokeTestReflection.SetField(objectHandling, "heldPickup", pickup);
            GameplaySmokeTestReflection.Invoke(objectHandling, "CacheHeldBounds", pickup);
            GameplaySmokeTestReflection.Invoke(objectHandling, "UpdateHeldPose");

            Vector3 expected = neighborObject.transform.TransformPoint(new Vector3(0.4f, 1.1f, 0.3f));
            Assert.That(pickup.transform.position.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(pickup.transform.position.y, Is.EqualTo(expected.y).Within(0.001f));
            Assert.That(pickup.transform.position.z, Is.EqualTo(expected.z).Within(0.001f));
        }

        [Test]
        public void Pickupable_HomeLocationDetectsMissingAndForeignPlacement()
        {
            GameObject firstObject = context.CreateObject("FirstPickup");
            firstObject.AddComponent<Rigidbody>();
            firstObject.AddComponent<BoxCollider>();
            Pickupable firstPickup = context.AddInitializedComponent<Pickupable>(firstObject);

            GameObject secondObject = context.CreateObject("SecondPickup");
            secondObject.transform.position = Vector3.right * 2f;
            secondObject.AddComponent<Rigidbody>();
            secondObject.AddComponent<BoxCollider>();
            Pickupable secondPickup = context.AddInitializedComponent<Pickupable>(secondObject);

            firstObject.transform.position = secondPickup.HomePosition;
            Physics.SyncTransforms();

            Assert.That(firstPickup.IsMissingFromHome, Is.True);
            Assert.That(firstPickup.TryGetForeignHome(out Pickupable foreignHome), Is.True);
            Assert.That(foreignHome, Is.SameAs(secondPickup));
        }

        [Test]
        public void ObjectLocationChange_RaisesSuspicionWhenPickupLeavesHome()
        {
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>();
            GameplaySmokeTestReflection.SetField(brain, "objectLocationAwarenessRadius", 6f);
            GameplaySmokeTestReflection.SetField(brain, "missingObjectSuspicion", 0.35f);

            GameObject pickupObject = context.CreateObject("MovedPickup");
            pickupObject.AddComponent<Rigidbody>();
            pickupObject.AddComponent<BoxCollider>();
            context.AddInitializedComponent<Pickupable>(pickupObject);
            pickupObject.transform.position = Vector3.right * 2f;
            Physics.SyncTransforms();

            GameplaySmokeTestReflection.Invoke(brain, "TryNoticeObjectLocationChanges");

            Assert.That(brain.Suspicion, Is.EqualTo(0.35f).Within(0.001f));
            Assert.That(
                GameplaySmokeTestReflection.GetField<GameObject>(brain, "currentInvestigationSource"),
                Is.SameAs(pickupObject));
        }

        [Test]
        public void ObjectLocationChange_OnlyRaisesSuspicionOnceUntilPickupReturnsHome()
        {
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>();
            GameplaySmokeTestReflection.SetField(brain, "objectLocationAwarenessRadius", 6f);
            GameplaySmokeTestReflection.SetField(brain, "missingObjectSuspicion", 0.35f);

            GameObject pickupObject = context.CreateObject("MovedPickup");
            pickupObject.AddComponent<Rigidbody>();
            pickupObject.AddComponent<BoxCollider>();
            Pickupable pickup = context.AddInitializedComponent<Pickupable>(pickupObject);

            pickupObject.transform.position = Vector3.right * 2f;
            Physics.SyncTransforms();
            GameplaySmokeTestReflection.Invoke(brain, "TryNoticeObjectLocationChanges");
            Assert.That(brain.Suspicion, Is.EqualTo(0.35f).Within(0.001f));

            GameplaySmokeTestReflection.SetField(brain, "nextObjectLocationCheckTime", float.NegativeInfinity);
            GameplaySmokeTestReflection.Invoke(brain, "TryNoticeObjectLocationChanges");
            Assert.That(brain.Suspicion, Is.EqualTo(0.35f).Within(0.001f));

            pickupObject.transform.SetPositionAndRotation(pickup.HomePosition, pickup.HomeRotation);
            Physics.SyncTransforms();
            GameplaySmokeTestReflection.SetField(brain, "nextObjectLocationCheckTime", float.NegativeInfinity);
            GameplaySmokeTestReflection.Invoke(brain, "TryNoticeObjectLocationChanges");

            pickupObject.transform.position = Vector3.right * 3f;
            Physics.SyncTransforms();
            GameplaySmokeTestReflection.SetField(brain, "nextObjectLocationCheckTime", float.NegativeInfinity);
            GameplaySmokeTestReflection.Invoke(brain, "TryNoticeObjectLocationChanges");

            Assert.That(brain.Suspicion, Is.EqualTo(0.7f).Within(0.001f));
        }

        [Test]
        public void ObjectLocationChange_IgnoresNeighborInstigatedPickup()
        {
            GameObject neighborObject = context.CreateObject("Neighbor");
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(neighborObject);
            GameplaySmokeTestReflection.SetField(brain, "objectLocationAwarenessRadius", 6f);
            GameplaySmokeTestReflection.SetField(brain, "missingObjectSuspicion", 0.35f);

            GameObject pickupObject = context.CreateObject("NeighborMovedPickup");
            pickupObject.AddComponent<Rigidbody>();
            pickupObject.AddComponent<BoxCollider>();
            Pickupable pickup = context.AddInitializedComponent<Pickupable>(pickupObject);
            pickup.MarkNeighborHomeDisplacement(neighborObject);
            pickupObject.transform.position = Vector3.right * 2f;
            Physics.SyncTransforms();

            GameplaySmokeTestReflection.Invoke(brain, "TryNoticeObjectLocationChanges");

            Assert.That(pickup.IsHomeDisplacementNeighborInstigated, Is.True);
            Assert.That(pickup.NeedsHomeRestoration, Is.False);
            Assert.That(brain.Suspicion, Is.Zero);
            Assert.That(
                GameplaySmokeTestReflection.GetField<GameObject>(brain, "currentInvestigationSource"),
                Is.Null);
        }

        [Test]
        public void ObjectLocationChange_UsesForeignSuspicionWhenPickupOccupiesAnotherHome()
        {
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>();
            GameplaySmokeTestReflection.SetField(brain, "objectLocationAwarenessRadius", 6f);
            GameplaySmokeTestReflection.SetField(brain, "missingObjectSuspicion", 0.1f);
            GameplaySmokeTestReflection.SetField(brain, "foreignObjectSuspicion", 0.42f);

            GameObject firstObject = context.CreateObject("ForeignPickup");
            firstObject.AddComponent<Rigidbody>();
            firstObject.AddComponent<BoxCollider>();
            Pickupable firstPickup = context.AddInitializedComponent<Pickupable>(firstObject);

            GameObject secondObject = context.CreateObject("HomePickup");
            secondObject.transform.position = Vector3.right * 2f;
            secondObject.AddComponent<Rigidbody>();
            secondObject.AddComponent<BoxCollider>();
            Pickupable secondPickup = context.AddInitializedComponent<Pickupable>(secondObject);

            firstObject.transform.position = secondPickup.HomePosition;
            Physics.SyncTransforms();

            GameplaySmokeTestReflection.Invoke(brain, "TryNoticeObjectLocationChanges");

            Assert.That(firstPickup.TryGetForeignHome(out _), Is.True);
            Assert.That(brain.Suspicion, Is.EqualTo(0.42f).Within(0.001f));
        }

        [Test]
        public void ObjectHandling_IgnoresNeighborInstigatedHomeRestoreCandidate()
        {
            GameObject neighborObject = context.CreateObject("Neighbor");
            context.AddInitializedComponent<NeighborMotor>(neighborObject);
            context.AddInitializedComponent<NeighborBrain>(neighborObject);
            NeighborObjectHandling objectHandling = context.AddInitializedComponent<NeighborObjectHandling>(neighborObject);

            GameObject pickupObject = context.CreateObject("NeighborMovedPickup");
            pickupObject.AddComponent<Rigidbody>();
            pickupObject.AddComponent<BoxCollider>();
            Pickupable pickup = context.AddInitializedComponent<Pickupable>(pickupObject);
            pickup.MarkNeighborHomeDisplacement(neighborObject);
            pickupObject.transform.position = Vector3.right * 2f;
            Physics.SyncTransforms();

            Pickupable candidate = GameplaySmokeTestReflection.InvokeResult<Pickupable>(
                objectHandling,
                "FindBestHomeRestoreCandidate");

            Assert.That(pickup.IsHomeDisplacementNeighborInstigated, Is.True);
            Assert.That(candidate, Is.Null);
        }

        [Test]
        public void ObjectHandling_PlacesHeldObjectBackAtHome()
        {
            GameObject neighborObject = context.CreateObject("Neighbor");
            context.AddInitializedComponent<NeighborMotor>(neighborObject);
            context.AddInitializedComponent<NeighborBrain>(neighborObject);
            NeighborObjectHandling objectHandling = context.AddInitializedComponent<NeighborObjectHandling>(neighborObject);
            GameObject pickupObject = context.CreateObject("MovedPickup");
            pickupObject.AddComponent<Rigidbody>();
            pickupObject.AddComponent<BoxCollider>();
            Pickupable pickup = context.AddInitializedComponent<Pickupable>(pickupObject);
            Vector3 homePosition = pickup.HomePosition;
            Quaternion homeRotation = pickup.HomeRotation;

            pickupObject.transform.SetPositionAndRotation(Vector3.right * 2f, Quaternion.Euler(0f, 45f, 0f));
            pickup.Pickup(null, false);
            GameplaySmokeTestReflection.SetField(objectHandling, "heldPickup", pickup);
            GameplaySmokeTestReflection.SetField(objectHandling, "isRestoringHeldPickupHome", true);

            GameplaySmokeTestReflection.Invoke(objectHandling, "PlaceHeldObjectAtHome");

            Assert.That(pickup.IsHeld, Is.False);
            Assert.That(pickup.transform.position, Is.EqualTo(homePosition));
            Assert.That(Quaternion.Angle(pickup.transform.rotation, homeRotation), Is.LessThan(0.001f));
            Assert.That(pickup.IsAtHome, Is.True);
        }

        [Test]
        public void LightSwitchInteractor_TogglesSwitchWithoutAlertingNeighbor()
        {
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>();
            NeighborLightSwitchInteractor interactor = brain.LightSwitchInteractor;
            Assert.That(interactor, Is.Not.Null);

            GameObject switchObject = context.CreateObject("LightSwitch");
            switchObject.AddComponent<BoxCollider>();
            LightSwitch lightSwitch = context.AddInitializedComponent<LightSwitch>(switchObject);
            bool emittedState = true;
            lightSwitch.HouseWireSignalEmitted += signal => emittedState = signal.BoolValue;

            GameplaySmokeTestReflection.Invoke(interactor, "UseSwitch", lightSwitch);

            Assert.That(emittedState, Is.False);
            Assert.That(brain.Suspicion, Is.Zero);
        }

        [Test]
        public void TaskLocation_AutoRoutineRoleInfersHouseActivity()
        {
            NeighborTaskLocation chairTask = context.AddInitializedComponent<NeighborTaskLocation>(
                context.CreateObject("Living Room Chair Task"));
            GameplaySmokeTestReflection.SetField(
                chairTask,
                "objectTaskType",
                NeighborTaskLocation.ObjectTaskType.Sit);

            NeighborTaskLocation fuseTask = context.AddInitializedComponent<NeighborTaskLocation>(
                context.CreateObject("Fuse Repair Task"));

            Assert.That(chairTask.Role, Is.EqualTo(NeighborTaskLocation.RoutineRole.Relax));
            Assert.That(fuseTask.Role, Is.EqualTo(NeighborTaskLocation.RoutineRole.Maintenance));
        }

        [Test]
        public void RoutineSelection_PenalizesRepeatedRoutineRoleWhenAlternativeExists()
        {
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>();
            GameplaySmokeTestReflection.SetField(
                brain,
                "lastRoutineRole",
                NeighborTaskLocation.RoutineRole.Relax);

            NeighborTaskLocation repeatRelaxTask = context.AddInitializedComponent<NeighborTaskLocation>(
                context.CreateObject("Sofa Task"));
            GameplaySmokeTestReflection.SetField(
                repeatRelaxTask,
                "routineRole",
                NeighborTaskLocation.RoutineRole.Relax);

            NeighborTaskLocation choreTask = context.AddInitializedComponent<NeighborTaskLocation>(
                context.CreateObject("Kitchen Task"));
            GameplaySmokeTestReflection.SetField(
                choreTask,
                "routineRole",
                NeighborTaskLocation.RoutineRole.Chore);

            float repeatScore = GameplaySmokeTestReflection.InvokeResult<float>(
                brain,
                "GetTaskSelectionScore",
                repeatRelaxTask,
                2,
                0f);
            float choreScore = GameplaySmokeTestReflection.InvokeResult<float>(
                brain,
                "GetTaskSelectionScore",
                choreTask,
                2,
                0f);

            Assert.That(choreScore, Is.GreaterThan(repeatScore));
        }

        [Test]
        public void RoutineSelection_SuspicionFavorsSecurityWorkOverRest()
        {
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>();
            GameplaySmokeTestReflection.SetField(brain, "suspicion", 0.65f);

            NeighborTaskLocation restTask = context.AddInitializedComponent<NeighborTaskLocation>(
                context.CreateObject("Bedroom Sleep Task"));
            GameplaySmokeTestReflection.SetField(
                restTask,
                "routineRole",
                NeighborTaskLocation.RoutineRole.Rest);

            NeighborTaskLocation securityTask = context.AddInitializedComponent<NeighborTaskLocation>(
                context.CreateObject("Security Camera Check Task"));
            GameplaySmokeTestReflection.SetField(
                securityTask,
                "routineRole",
                NeighborTaskLocation.RoutineRole.Security);

            float restScore = GameplaySmokeTestReflection.InvokeResult<float>(
                brain,
                "GetTaskSelectionScore",
                restTask,
                2,
                0f);
            float securityScore = GameplaySmokeTestReflection.InvokeResult<float>(
                brain,
                "GetTaskSelectionScore",
                securityTask,
                2,
                0f);

            Assert.That(securityScore, Is.GreaterThan(restScore));
        }

        [Test]
        public void DynamicObstacleAvoidance_OnlyUsesUsefulDetours()
        {
            NeighborMotor motor = context.AddInitializedComponent<NeighborMotor>();
            GameplaySmokeTestReflection.SetField(motor, "maximumUsefulDetourExtraDistance", 2f);
            GameplaySmokeTestReflection.SetField(motor, "maximumUsefulDetourDistanceRatio", 1.35f);

            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<bool>(
                    motor,
                    "IsDynamicObstacleDetourWorthTaking",
                    10f,
                    12f),
                Is.True);
            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<bool>(
                    motor,
                    "IsDynamicObstacleDetourWorthTaking",
                    10f,
                    14f),
                Is.False);
            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<bool>(
                    motor,
                    "IsDynamicObstacleDetourWorthTaking",
                    2f,
                    3f),
                Is.False);
        }

        [Test]
        public void LowClearanceCrouching_ShortensColliderAndSlowsMovement()
        {
            GameObject neighborObject = context.CreateObject("CrouchingNeighbor");
            CharacterController controller = neighborObject.AddComponent<CharacterController>();
            NeighborMotor motor = context.AddInitializedComponent<NeighborMotor>(neighborObject);
            GameplaySmokeTestReflection.SetField(motor, "crouchingHeight", 1.3f);
            GameplaySmokeTestReflection.SetField(motor, "crouchSpeedMultiplier", 0.5f);

            GameplaySmokeTestReflection.Invoke(motor, "SetCrouchingForClearance", true);

            Assert.That(motor.IsCrouchingForClearance, Is.True);
            Assert.That(controller.height, Is.EqualTo(1.3f).Within(0.001f));
            Assert.That(motor.GetMoveSpeed(NeighborMotor.MoveMode.Walk), Is.EqualTo(1.2f).Within(0.001f));

            GameplaySmokeTestReflection.Invoke(motor, "SetCrouchingForClearance", false);

            Assert.That(motor.IsCrouchingForClearance, Is.False);
            Assert.That(controller.height, Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void LowClearanceCrouching_EntersWhenStandingCapsuleDoesNotFit()
        {
            GameObject neighborObject = context.CreateObject("CrouchingNeighbor");
            neighborObject.AddComponent<CharacterController>();
            NeighborMotor motor = context.AddInitializedComponent<NeighborMotor>(neighborObject);
            GameObject ceiling = context.CreateObject("LowCeiling");
            BoxCollider ceilingCollider = ceiling.AddComponent<BoxCollider>();
            ceilingCollider.size = new Vector3(4f, 0.2f, 4f);
            ceiling.transform.position = Vector3.up * 1.65f;
            Physics.SyncTransforms();

            GameplaySmokeTestReflection.Invoke(motor, "UpdateLowClearanceCrouching");

            Assert.That(motor.IsCrouchingForClearance, Is.True);
        }

        [Test]
        public void DropRecovery_OnlyAcceptsSafeNearbyLowerLandings()
        {
            NeighborMotor motor = context.AddInitializedComponent<NeighborMotor>();
            GameplaySmokeTestReflection.SetField(motor, "targetDropMinimumHeight", 0.5f);
            GameplaySmokeTestReflection.SetField(motor, "targetDropMaximumHeight", 4f);
            GameplaySmokeTestReflection.SetField(motor, "dropRecoveryHorizontalReach", 4f);
            Vector3 start = Vector3.up * 3f;

            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<bool>(
                    motor,
                    "IsDropWithinRecoveryRange",
                    start,
                    Vector3.forward * 2f),
                Is.True);
            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<bool>(
                    motor,
                    "IsDropWithinRecoveryRange",
                    start,
                    Vector3.up * 3f + Vector3.forward),
                Is.False);
            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<bool>(
                    motor,
                    "IsDropWithinRecoveryRange",
                    start,
                    Vector3.forward * 5f),
                Is.False);
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
        public void Vision_IgnoresHiddenPlayerUntilHideSpotIsCompromised()
        {
            NeighborVision vision = context.AddInitializedComponent<NeighborVision>();
            Transform target = context.CreateObject("PlayerTarget").transform;
            GameplaySmokeTestReflection.SetField(vision, "target", target);
            GameplaySmokeTestReflection.SetField(vision, "eyeHeight", 0f);
            GameplaySmokeTestReflection.SetField(vision, "lineOfSightMask", (LayerMask)0);
            target.position = Vector3.forward * 5f;
            PlayerHidingState hidingState = target.gameObject.AddComponent<PlayerHidingState>();

            hidingState.SetHidden(true);

            Assert.That(vision.TrySeeTarget(out _, out _), Is.False);

            hidingState.RegisterNeighborInspection(true);

            Assert.That(hidingState.IsHidden, Is.True);
            Assert.That(hidingState.IsCompromised, Is.True);
            Assert.That(hidingState.IsConcealedFromVision, Is.False);
            Assert.That(vision.TrySeeTarget(out _, out _), Is.True);
        }

        [Test]
        public void Vision_SeesHiddenPlayerWhenPeekExposureIsHigh()
        {
            NeighborVision vision = context.AddInitializedComponent<NeighborVision>();
            Transform target = context.CreateObject("PlayerTarget").transform;
            GameplaySmokeTestReflection.SetField(vision, "target", target);
            GameplaySmokeTestReflection.SetField(vision, "eyeHeight", 0f);
            GameplaySmokeTestReflection.SetField(vision, "lineOfSightMask", (LayerMask)0);
            target.position = Vector3.forward * 5f;
            PlayerHidingState hidingState = target.gameObject.AddComponent<PlayerHidingState>();

            hidingState.SetHidden(true);
            Assert.That(vision.TrySeeTarget(out _, out _), Is.False);

            hidingState.SetPeekExposure(1f, 0f);

            Assert.That(hidingState.IsDangerouslyExposed, Is.True);
            Assert.That(hidingState.IsConcealedFromVision, Is.False);
            Assert.That(vision.TrySeeTarget(out _, out _), Is.True);
        }

        [Test]
        public void HidingState_InspectionRaisesBreathTensionAndCompromisesFoundPlayer()
        {
            PlayerHidingState hidingState = context.CreateObject("HiddenPlayer").AddComponent<PlayerHidingState>();
            ClosetHideSpot hideSpot = context.CreateObject("HideSpot").AddComponent<ClosetHideSpot>();
            List<PlayerFeedbackEvents.HidingFeedback> feedback = new();
            PlayerFeedbackEvents.HidingChanged += HandleHidingFeedback;

            try
            {
                hidingState.SetHidden(true, hideSpot);
                hidingState.RegisterNeighborInspection(true);
            }
            finally
            {
                PlayerFeedbackEvents.HidingChanged -= HandleHidingFeedback;
            }

            Assert.That(hidingState.IsHidden, Is.True);
            Assert.That(hidingState.CurrentHideSpot, Is.SameAs(hideSpot));
            Assert.That(hidingState.BreathTension01, Is.GreaterThan(0f));
            Assert.That(hidingState.WasInspectedRecently, Is.True);
            Assert.That(hidingState.IsCompromised, Is.True);
            Assert.That(feedback, Has.Count.EqualTo(2));
            Assert.That(feedback[0].Kind, Is.EqualTo(PlayerFeedbackEvents.HidingFeedbackKind.Entered));
            Assert.That(feedback[0].SpotName, Is.EqualTo("closet"));
            Assert.That(feedback[0].IsHidden, Is.True);
            Assert.That(feedback[1].Kind, Is.EqualTo(PlayerFeedbackEvents.HidingFeedbackKind.Found));
            Assert.That(feedback[1].IsCompromised, Is.True);
            Assert.That(feedback[1].BreathTension, Is.GreaterThan(0f));

            void HandleHidingFeedback(PlayerFeedbackEvents.HidingFeedback hidingFeedback)
            {
                feedback.Add(hidingFeedback);
            }
        }

        [Test]
        public void HidingState_PeekExposureBuildsTensionAndResetsOnExit()
        {
            PlayerHidingState hidingState = context.CreateObject("HiddenPlayer").AddComponent<PlayerHidingState>();

            hidingState.SetHidden(true);
            hidingState.SetPeekExposure(0.8f, 1f);

            Assert.That(hidingState.PeekExposure01, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(hidingState.BreathTension01, Is.GreaterThan(0f));

            hidingState.SetHidden(false);

            Assert.That(hidingState.PeekExposure01, Is.Zero);
            Assert.That(hidingState.IsConcealedFromVision, Is.False);
        }

        [Test]
        public void HidingState_HighBreathTensionEmitsReadableNoise()
        {
            PlayerFeedbackEvents.NoiseFeedback received = default;
            bool reported = false;
            PlayerFeedbackEvents.NoiseEmitted += HandleNoise;

            GameObject noiseObject = null;
            try
            {
                PlayerHidingState hidingState = context.CreateObject("HiddenPlayer").AddComponent<PlayerHidingState>();
                GameplaySmokeTestReflection.SetField(hidingState, "breathNoiseThreshold", 0.2f);
                GameplaySmokeTestReflection.SetField(hidingState, "breathNoiseCooldown", 0f);
                GameplaySmokeTestReflection.SetField(hidingState, "breathNoiseRadius", 6f);
                GameplaySmokeTestReflection.SetField(hidingState, "breathNoiseLoudness", 0.4f);

                hidingState.SetHidden(true);
                hidingState.AddBreathTension(0.9f);
                GameplaySmokeTestReflection.Invoke(hidingState, "Update");

                noiseObject = GameObject.Find("HiddenBreathNoiseEvent");
                Assert.That(reported, Is.True);
                Assert.That(received.Radius, Is.EqualTo(6f));
                Assert.That(received.Loudness, Is.GreaterThanOrEqualTo(0.2f));
                Assert.That(noiseObject, Is.Not.Null);
                Assert.That(noiseObject.GetComponent<SphereCollider>().isTrigger, Is.True);
            }
            finally
            {
                PlayerFeedbackEvents.NoiseEmitted -= HandleNoise;
                if (noiseObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(noiseObject);
                }
            }

            void HandleNoise(PlayerFeedbackEvents.NoiseFeedback feedback)
            {
                received = feedback;
                reported = true;
            }
        }

        [Test]
        public void PlayerAudio_HiddenBreathTensionDrivesBreathLoopTarget()
        {
            GameObject playerObject = context.CreateObject("Player");
            playerObject.AddComponent<CharacterController>();
            context.AddInitializedComponent<PlayerController>(playerObject);
            PlayerHidingState hidingState = playerObject.AddComponent<PlayerHidingState>();
            PlayerAudioController audioController = context.AddInitializedComponent<PlayerAudioController>(playerObject);
            AudioClip breathClip = AudioClip.Create("TestBreathLoop", 64, 1, 8000, false);

            try
            {
                GameplaySmokeTestReflection.SetField(audioController, "tiredBreathLoop", breathClip);
                GameplaySmokeTestReflection.SetField(audioController, "hiddenBreathVolume", 0.6f);
                GameplaySmokeTestReflection.SetField(audioController, "breathStartStamina", 0f);

                hidingState.SetHidden(true);
                hidingState.AddBreathTension(0.75f);
                GameplaySmokeTestReflection.Invoke(audioController, "UpdateBreathing");

                Assert.That(audioController.CurrentBreathStress01, Is.EqualTo(0.75f).Within(0.001f));
                Assert.That(audioController.CurrentBreathTargetVolume, Is.EqualTo(0.45f).Within(0.001f));

                hidingState.RegisterNeighborInspection(true);
                GameplaySmokeTestReflection.Invoke(audioController, "UpdateBreathing");

                Assert.That(audioController.CurrentBreathStress01, Is.EqualTo(1f).Within(0.001f));
                Assert.That(audioController.CurrentBreathTargetVolume, Is.EqualTo(0.6f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(breathClip);
            }
        }

        [Test]
        public void ClosetSearch_MarksHiddenPlayerAsInspected()
        {
            GameObject playerObject = context.CreateObject("Player");
            playerObject.AddComponent<CharacterController>();
            PlayerController player = playerObject.AddComponent<PlayerController>();
            PlayerHidingState hidingState = playerObject.AddComponent<PlayerHidingState>();
            ClosetHideSpot hideSpot = context.CreateObject("HideSpot").AddComponent<ClosetHideSpot>();
            GameplaySmokeTestReflection.SetField(hideSpot, "hiddenPlayer", player);
            GameplaySmokeTestReflection.SetField(hideSpot, "hiddenState", hidingState);
            hidingState.SetHidden(true, hideSpot);

            Assert.That(hideSpot.SearchByNeighbor(), Is.SameAs(player));

            Assert.That(hidingState.WasInspectedRecently, Is.True);
            Assert.That(hidingState.IsCompromised, Is.True);
            Assert.That(hidingState.BreathTension01, Is.GreaterThan(0f));
        }

        [Test]
        public void Perception_KeepsPlayerVisibleDuringShortChaseSightGrace()
        {
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>();
            Transform player = context.CreateObject("PlayerTarget").transform;
            GameplaySmokeTestReflection.SetField(brain, "player", player);
            GameplaySmokeTestReflection.SetField(brain, "currentState", NeighborBrain.BehaviorState.Chase);
            GameplaySmokeTestReflection.SetField(brain, "playerSightGraceTime", 0.25f);
            GameplaySmokeTestReflection.SetField(brain, "lastPlayerSeenTime", Time.time);

            GameplaySmokeTestReflection.Invoke(brain, "UpdatePerception");

            Assert.That(brain.IsPlayerVisible, Is.True);

            GameplaySmokeTestReflection.SetField(brain, "lastPlayerSeenTime", Time.time - 0.5f);
            GameplaySmokeTestReflection.Invoke(brain, "UpdatePerception");

            Assert.That(brain.IsPlayerVisible, Is.False);
        }

        [Test]
        public void ChaseGoalRefresh_RequiresLargerMovementWhenPlayerIsHidden()
        {
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>();
            GameplaySmokeTestReflection.SetField(brain, "currentGoal", Vector3.zero);
            GameplaySmokeTestReflection.SetField(brain, "visibleChaseGoalRefreshDistance", 0.35f);
            GameplaySmokeTestReflection.SetField(brain, "hiddenChaseGoalRefreshDistance", 0.75f);

            GameplaySmokeTestReflection.SetField(brain, "isPlayerVisible", true);
            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<bool>(
                    brain,
                    "HasChaseGoalMovedEnough",
                    Vector3.right * 0.2f),
                Is.False);
            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<bool>(
                    brain,
                    "HasChaseGoalMovedEnough",
                    Vector3.right * 0.5f),
                Is.True);

            GameplaySmokeTestReflection.SetField(brain, "isPlayerVisible", false);
            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<bool>(
                    brain,
                    "HasChaseGoalMovedEnough",
                    Vector3.right * 0.5f),
                Is.False);
            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<bool>(
                    brain,
                    "HasChaseGoalMovedEnough",
                    Vector3.right),
                Is.True);
        }

        [Test]
        public void AnimationState_UsesMovementHysteresisNearIdleThreshold()
        {
            NeighborAnimationController animation = context.AddInitializedComponent<NeighborAnimationController>();
            int idleState = Animator.StringToHash("Base Layer.Idle");
            int walkState = Animator.StringToHash("Base Layer.Walk");
            GameplaySmokeTestReflection.SetField(animation, "movingThreshold", 0.1f);
            GameplaySmokeTestReflection.SetField(animation, "movingExitThresholdMultiplier", 0.5f);

            GameplaySmokeTestReflection.SetField(animation, "currentState", walkState);
            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<int>(
                    animation,
                    "ChooseState",
                    0.07f),
                Is.EqualTo(walkState));

            GameplaySmokeTestReflection.SetField(animation, "currentState", idleState);
            Assert.That(
                GameplaySmokeTestReflection.InvokeResult<int>(
                    animation,
                    "ChooseState",
                    0.07f),
                Is.EqualTo(idleState));
        }

        [Test]
        public void NeighborPlacedCameraSpacing_PreventsOverlappingMounts()
        {
            GameObject cameraObject = context.CreateObject("NeighborPlacedCamera");
            BoxCollider cameraCollider = cameraObject.AddComponent<BoxCollider>();
            SecurityCamera camera = context.AddInitializedComponent<SecurityCamera>(cameraObject);
            GameplaySmokeTestReflection.InvokeIfPresent(cameraObject.GetComponent<Pickupable>(), "Awake");

            Assert.That(camera.TryAttachByNeighbor(Vector3.zero, Vector3.forward), Is.True);
            Assert.That(cameraCollider.enabled, Is.True);
            Assert.That(SecurityCamera.IsNeighborCameraWithinDistance(Vector3.right, 2f), Is.True);
            Assert.That(SecurityCamera.IsNeighborCameraWithinDistance(Vector3.right * 3f, 2f), Is.False);
        }

        [Test]
        public void NeighborPlacedCameraPickup_RemovesReinforcementPlacement()
        {
            GameObject cameraObject = context.CreateObject("NeighborPlacedCamera");
            cameraObject.AddComponent<BoxCollider>();
            SecurityCamera camera = context.AddInitializedComponent<SecurityCamera>(cameraObject);
            Pickupable pickupable = cameraObject.GetComponent<Pickupable>();
            GameplaySmokeTestReflection.InvokeIfPresent(pickupable, "Awake");

            Assert.That(camera.TryAttachByNeighbor(Vector3.zero, Vector3.forward), Is.True);

            pickupable.Pickup(null, false);

            Assert.That(camera.IsNeighborPlaced, Is.False);
            Assert.That(SecurityCamera.IsNeighborCameraWithinDistance(Vector3.zero, 2f), Is.False);
        }

        [Test]
        public void SecurityCameraImpactDisable_DetachesAndStopsDetection()
        {
            GameObject cameraObject = context.CreateObject("SecurityCamera");
            cameraObject.AddComponent<BoxCollider>();
            SecurityCamera camera = context.AddInitializedComponent<SecurityCamera>(cameraObject);
            GameplaySmokeTestReflection.InvokeIfPresent(cameraObject.GetComponent<Pickupable>(), "Awake");
            GameObject thrownObject = context.CreateObject("ThrownPickup");
            thrownObject.AddComponent<BoxCollider>();
            Pickupable thrownPickup = context.AddInitializedComponent<Pickupable>(thrownObject);

            Assert.That(camera.TryAttachByNeighbor(Vector3.zero, Vector3.forward), Is.True);

            thrownPickup.Throw(Vector3.forward * 4f);
            camera.ReceivePhysicsImpact(thrownPickup, Vector3.zero, Vector3.forward * 4f, 0f, 1f);

            Assert.That(camera.IsDisabled, Is.True);
            Assert.That(camera.IsNeighborPlaced, Is.False);
            Assert.That(SecurityCamera.IsNeighborCameraWithinDistance(Vector3.zero, 2f), Is.False);
        }

        [Test]
        public void NeighborImpactReceiver_OnlyAcceptsRecentlyThrownPhysicsPickup()
        {
            NeighborImpactReceiver receiver = context.AddInitializedComponent<NeighborImpactReceiver>();
            GameObject pickupObject = context.CreateObject("Pickup");
            pickupObject.AddComponent<Rigidbody>();
            pickupObject.AddComponent<BoxCollider>();
            Pickupable pickup = context.AddInitializedComponent<Pickupable>(pickupObject);
            int impactCount = 0;
            receiver.ImpactReceived += () => impactCount++;

            receiver.ReceivePhysicsImpact(pickup, Vector3.zero, Vector3.forward * 4f, 18f, 1f);

            Assert.That(impactCount, Is.Zero);

            pickup.Throw(Vector3.forward * 4f);
            receiver.ReceivePhysicsImpact(pickup, Vector3.zero, Vector3.forward * 4f, 18f, 1f);

            Assert.That(impactCount, Is.EqualTo(1));
        }

        [Test]
        public void SecurityCameraWallMount_AllowsPickupThroughMountingWall()
        {
            GameObject wallObject = context.CreateObject("WallBlocker");
            BoxCollider wallCollider = wallObject.AddComponent<BoxCollider>();
            wallCollider.size = new Vector3(4f, 4f, 0.1f);
            GameObject cameraObject = context.CreateObject("SecurityCamera");
            cameraObject.AddComponent<BoxCollider>();
            SecurityCamera camera = context.AddInitializedComponent<SecurityCamera>(cameraObject);
            GameplaySmokeTestReflection.InvokeIfPresent(cameraObject.GetComponent<Pickupable>(), "Awake");

            Assert.That(camera.TryAttachByNeighbor(Vector3.zero, Vector3.forward), Is.True);

            Assert.That(camera.ShouldAllowPickupThroughBlocker(wallCollider, 0.1f), Is.True);
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
            Assert.That(brain.HasActiveInvestigation, Is.True);
            Assert.That(brain.LastKnownInvestigationPosition, Is.EqualTo(neighborObject.transform.position));
            Assert.That(
                GameplaySmokeTestReflection.GetField<GameObject>(brain, "currentInvestigationSource"),
                Is.SameAs(source));
            Assert.That(hearing, Is.Not.Null);
        }

        [Test]
        public void HeardNoise_ReportsStealthLoopNoisePulse()
        {
            GameObject neighborObject = context.CreateObject("Neighbor");
            context.AddInitializedComponent<NeighborHearing>(neighborObject);
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(neighborObject);
            GameObject source = context.CreateObject("NoiseSource");
            GameObject noiseObject = context.CreateObject("NoiseEvent");
            noiseObject.AddComponent<SphereCollider>();
            NoiseEvent noiseEvent = noiseObject.AddComponent<NoiseEvent>();
            PlayerFeedbackEvents.StealthLoopFeedback feedback = default;
            bool received = false;
            PlayerFeedbackEvents.StealthLoopChanged += HandleStealthLoopChanged;

            try
            {
                noiseEvent.Initialize(neighborObject.transform.position, 5f, 0.74f, source, 1f, 1f);

                Assert.That(brain.HasActiveInvestigation, Is.True);
                Assert.That(received, Is.True);
                Assert.That(
                    feedback.Phase,
                    Is.EqualTo(PlayerFeedbackEvents.StealthLoopPhase.Searching)
                        .Or.EqualTo(PlayerFeedbackEvents.StealthLoopPhase.Suspicious));
                Assert.That(feedback.Noise, Is.GreaterThan(0.65f));
                Assert.That(
                    GameplaySmokeTestReflection.GetField<float>(brain, "recentHeardNoise"),
                    Is.EqualTo(0.74f).Within(0.001f));
            }
            finally
            {
                PlayerFeedbackEvents.StealthLoopChanged -= HandleStealthLoopChanged;
            }

            void HandleStealthLoopChanged(PlayerFeedbackEvents.StealthLoopFeedback item)
            {
                feedback = item;
                received = true;
            }
        }

        [Test]
        public void PlayerMovementNoise_PrimesNeighborInvestigation()
        {
            GameObject neighborObject = context.CreateObject("Neighbor");
            neighborObject.transform.position = Vector3.zero;
            NeighborHearing hearing = context.AddInitializedComponent<NeighborHearing>(neighborObject);
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(neighborObject);

            GameObject playerObject = context.CreateObject("Player");
            playerObject.transform.position = Vector3.right;
            PlayerController player = context.AddInitializedComponent<PlayerController>(playerObject);

            PlayerFeedbackEvents.NoiseFeedback feedback = default;
            bool feedbackReceived = false;
            PlayerFeedbackEvents.NoiseEmitted += HandleNoise;

            try
            {
                GameplaySmokeTestReflection.Invoke(player, "EmitNoise", 0.72f, 8f);

                Assert.That(feedbackReceived, Is.True);
                Assert.That(feedback.Loudness, Is.EqualTo(0.72f).Within(0.001f));
                Assert.That(feedback.Radius, Is.EqualTo(8f).Within(0.001f));
                Assert.That(feedback.HeardByNeighbor, Is.True);
                Assert.That(feedback.NeighborListenerCount, Is.EqualTo(1));
                Assert.That(brain.HasActiveInvestigation, Is.True);
                Assert.That(brain.LastKnownInvestigationPosition, Is.EqualTo(playerObject.transform.position));
                Assert.That(
                    GameplaySmokeTestReflection.GetField<GameObject>(brain, "currentInvestigationSource"),
                    Is.SameAs(playerObject));
                Assert.That(hearing.LastHeardSource, Is.SameAs(playerObject));
            }
            finally
            {
                PlayerFeedbackEvents.NoiseEmitted -= HandleNoise;
                foreach (NoiseEvent noiseEvent in UnityEngine.Object.FindObjectsByType<NoiseEvent>(FindObjectsInactive.Include))
                {
                    if (noiseEvent != null && noiseEvent.name == "PlayerMovementNoiseEvent")
                    {
                        UnityEngine.Object.DestroyImmediate(noiseEvent.gameObject);
                    }
                }
            }

            void HandleNoise(PlayerFeedbackEvents.NoiseFeedback item)
            {
                feedback = item;
                feedbackReceived = true;
            }
        }

        [Test]
        public void NoiseInvestigation_CapturesInterruptedRoutineAndLastKnownPosition()
        {
            GameObject neighborObject = context.CreateObject("Neighbor");
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(neighborObject);
            GameObject source = context.CreateObject("NoiseSource");
            GameObject taskObject = context.CreateObject("InterruptedTask");
            NeighborTaskLocation task = context.AddInitializedComponent<NeighborTaskLocation>(taskObject);
            Vector3 routineGoal = new(1f, 0f, 2f);
            Vector3 noisePosition = new(4f, 0f, -3f);

            GameplaySmokeTestReflection.SetField(brain, "currentState", NeighborBrain.BehaviorState.Task);
            GameplaySmokeTestReflection.SetField(brain, "currentTaskLocation", task);
            GameplaySmokeTestReflection.SetField(brain, "currentGoal", routineGoal);

            GameplaySmokeTestReflection.Invoke(
                brain,
                "BeginInvestigation",
                noisePosition,
                source,
                2f,
                NeighborMotor.MoveMode.Cautious,
                false);

            Assert.That(brain.HasActiveInvestigation, Is.True);
            Assert.That(brain.LastKnownInvestigationPosition, Is.EqualTo(noisePosition));
            Assert.That(brain.CurrentInvestigationSource, Is.SameAs(source));
            Assert.That(
                GameplaySmokeTestReflection.GetField<NeighborBrain.BehaviorState>(brain, "preInvestigationState"),
                Is.EqualTo(NeighborBrain.BehaviorState.Task));
            Assert.That(
                GameplaySmokeTestReflection.GetField<Vector3>(brain, "preInvestigationGoal"),
                Is.EqualTo(routineGoal));
            Assert.That(
                GameplaySmokeTestReflection.GetField<NeighborTaskLocation>(brain, "preInvestigationTaskLocation"),
                Is.SameAs(task));
            Assert.That(
                GameplaySmokeTestReflection.GetField<NeighborTaskLocation>(brain, "currentTaskLocation"),
                Is.Null);
        }

        [Test]
        public void NoiseInvestigation_ReportsReadableSearchFeedbackPhases()
        {
            GameObject neighborObject = context.CreateObject("Neighbor");
            NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(neighborObject);
            GameObject source = context.CreateObject("NoiseSource");
            Vector3 noisePosition = new(4f, 0f, -3f);
            List<PlayerFeedbackEvents.NeighborInvestigationFeedback> feedback = new();
            PlayerFeedbackEvents.NeighborInvestigationChanged += HandleInvestigationFeedback;

            try
            {
                GameplaySmokeTestReflection.Invoke(
                    brain,
                    "BeginInvestigation",
                    noisePosition,
                    source,
                    2f,
                    NeighborMotor.MoveMode.Cautious,
                    false);

                Assert.That(feedback, Has.Count.EqualTo(1));
                Assert.That(feedback[0].Kind, Is.EqualTo(PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Started));
                Assert.That(feedback[0].Position, Is.EqualTo(noisePosition));
                Assert.That(feedback[0].SourceName, Is.EqualTo("NoiseSource"));
                Assert.That(feedback[0].Urgency, Is.EqualTo(0.65f).Within(0.001f));

                GameplaySmokeTestReflection.Invoke(brain, "ReportInvestigationSearchIfNeeded");
                GameplaySmokeTestReflection.Invoke(brain, "ReportInvestigationSearchIfNeeded");

                Assert.That(feedback, Has.Count.EqualTo(2));
                Assert.That(brain.HasReportedInvestigationSearch, Is.True);
                Assert.That(feedback[1].Kind, Is.EqualTo(PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Searching));

                GameplaySmokeTestReflection.Invoke(brain, "FinishInvestigationAndReturnToRoutine");

                Assert.That(feedback, Has.Count.EqualTo(3));
                Assert.That(feedback[2].Kind, Is.EqualTo(PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Returning));
                Assert.That(brain.HasActiveInvestigation, Is.False);
            }
            finally
            {
                PlayerFeedbackEvents.NeighborInvestigationChanged -= HandleInvestigationFeedback;
            }

            void HandleInvestigationFeedback(PlayerFeedbackEvents.NeighborInvestigationFeedback item)
            {
                feedback.Add(item);
            }
        }

        [Test]
        public void AbandonedNoiseInvestigation_ReturnsToInterruptedWanderGoal()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            NavMeshSurface surface = null;
            try
            {
                ground.name = "TemporaryNavMeshGround";
                ground.transform.position = new Vector3(0f, -0.1f, 0f);
                ground.transform.localScale = new Vector3(10f, 0.2f, 10f);
                surface = ground.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.All;
                surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
                surface.layerMask = ~0;
                surface.defaultArea = 0;
                surface.BuildNavMesh();

                GameObject neighborObject = context.CreateObject("Neighbor");
                neighborObject.transform.position = Vector3.zero;
                NavMeshAgent agent = neighborObject.AddComponent<NavMeshAgent>();
                agent.radius = 0.3f;
                agent.height = 2f;
                agent.stoppingDistance = 0.1f;
                NeighborMotor motor = context.AddInitializedComponent<NeighborMotor>(neighborObject);
                NeighborBrain brain = context.AddInitializedComponent<NeighborBrain>(neighborObject);
                GameObject source = context.CreateObject("NoiseSource");
                Vector3 resumeGoal = new(1f, 0f, 1f);
                Vector3 noisePosition = new(3f, 0f, 2f);

                GameplaySmokeTestReflection.SetField(brain, "wanderChance", 0f);
                GameplaySmokeTestReflection.SetField(brain, "currentState", NeighborBrain.BehaviorState.Wander);
                GameplaySmokeTestReflection.SetField(brain, "currentGoal", resumeGoal);

                GameplaySmokeTestReflection.Invoke(
                    brain,
                    "BeginInvestigation",
                    noisePosition,
                    source,
                    1f,
                    NeighborMotor.MoveMode.Walk,
                    false);

                Assert.That(brain.CurrentState, Is.EqualTo(NeighborBrain.BehaviorState.Investigate));
                Assert.That(brain.HasActiveInvestigation, Is.True);

                GameplaySmokeTestReflection.Invoke(brain, "HandleDestinationAbandoned", noisePosition);

                Assert.That(brain.HasActiveInvestigation, Is.False);
                Assert.That(brain.CurrentState, Is.EqualTo(NeighborBrain.BehaviorState.Wander));
                Assert.That(Vector3.Distance(brain.CurrentGoal, resumeGoal), Is.LessThan(0.35f));
            }
            finally
            {
                if (surface != null)
                {
                    surface.RemoveData();
                }

                UnityEngine.Object.DestroyImmediate(ground);
            }
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
        public void NoiseEvent_InstigatedByNeighbor_IsIgnoredOnlyByThatNeighbor()
        {
            GameObject instigatorObject = context.CreateObject("InstigatorNeighbor");
            context.AddInitializedComponent<NeighborHearing>(instigatorObject);
            NeighborBrain instigatorBrain = context.AddInitializedComponent<NeighborBrain>(instigatorObject);
            GameObject witnessObject = context.CreateObject("WitnessNeighbor");
            context.AddInitializedComponent<NeighborHearing>(witnessObject);
            NeighborBrain witnessBrain = context.AddInitializedComponent<NeighborBrain>(witnessObject);
            GameObject source = context.CreateObject("KnockedObject");
            GameObject noiseObject = context.CreateObject("NoiseEvent");
            noiseObject.AddComponent<SphereCollider>();
            NoiseEvent noiseEvent = noiseObject.AddComponent<NoiseEvent>();

            noiseEvent.Initialize(
                instigatorObject.transform.position,
                5f,
                0.8f,
                source,
                1f,
                1f,
                instigatorObject);

            Assert.That(instigatorBrain.Suspicion, Is.Zero);
            Assert.That(witnessBrain.Suspicion, Is.GreaterThan(0f));
            Assert.That(
                GameplaySmokeTestReflection.GetField<GameObject>(witnessBrain, "currentInvestigationSource"),
                Is.SameAs(source));
        }

        private Pickupable CreatePickupable(string name, Vector3 position, bool addDoorKey)
        {
            GameObject pickupObject = context.CreateObject(name);
            pickupObject.transform.position = position;
            pickupObject.AddComponent<Rigidbody>();
            pickupObject.AddComponent<BoxCollider>();
            if (addDoorKey)
            {
                pickupObject.AddComponent<DoorKey>();
            }

            return context.AddInitializedComponent<Pickupable>(pickupObject);
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
