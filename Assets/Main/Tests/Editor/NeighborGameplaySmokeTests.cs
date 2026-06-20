using Neighbor.Main.Features.Interaction;
using Neighbor.Main.Features.Neighbor;
using Neighbor.Main.HouseBuilder;
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
