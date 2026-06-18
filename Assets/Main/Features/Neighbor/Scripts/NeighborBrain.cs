using System.Collections.Generic;
using Neighbor.Main.Features.Interaction;
using Neighbor.Main.HouseBuilder;
using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Neighbor
{
    public static class NeighborEnvironmentalAwareness
    {
        public static event System.Action<Vector3, float, GameObject> EnvironmentChanged;

        public static void Report(Vector3 position, float suspicion, GameObject source)
        {
            float normalizedSuspicion = Mathf.Clamp01(suspicion);
            if (source == null || source.GetComponentInParent<NeighborBrain>() == null)
            {
                AdaptiveSecurityDirector.ReportDisturbance(normalizedSuspicion);
            }

            EnvironmentChanged?.Invoke(position, normalizedSuspicion, source);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            EnvironmentChanged = null;
        }
    }

    public sealed class NeighborBrain : MonoBehaviour
    {
        public enum SuspicionLevel
        {
            Relaxed,
            Curious,
            Suspicious,
            Certain
        }

        public enum BehaviorState
        {
            Idle,
            Task,
            Wander,
            ObjectHandling,
            LightSwitchUse,
            GarageDoorUse,
            Investigate,
            Chase,
            Catching,
            HuntMode,
            Stunned
        }

        [Header("References")]
        [SerializeField] private NeighborMotor motor;
        [SerializeField] private NeighborVision vision;
        [SerializeField] private NeighborHearing hearing;
        [SerializeField] private NeighborObjectHandling objectHandling;
        [SerializeField] private NeighborLightSwitchInteractor lightSwitchInteractor;
        [SerializeField] private Transform player;

        [Header("Routine")]
        [SerializeField, Range(0f, 1f)] private float wanderChance = 0.35f;
        [SerializeField, Min(0f)] private float wanderRadius = 8f;
        [SerializeField, Min(0f)] private float idleWaitMinimum = 1f;
        [SerializeField, Min(0f)] private float idleWaitMaximum = 3f;

        [Header("Investigation")]
        [SerializeField, Min(0f)] private float minimumInvestigateLoudness = 0.08f;
        [SerializeField, Range(0f, 1f)] private float minimumUrgencyToRunToNoise = 0.65f;
        [SerializeField, Min(0f)] private float noiseDestinationSampleRadius = 4f;
        [SerializeField, Min(0f)] private float investigationWaitTime = 2.2f;
        [SerializeField, Min(0.1f)] private float televisionShutoffDistance = 2.2f;
        [SerializeField, Range(0f, 1f)] private float unexpectedOpenDoorSuspicion = 0.36f;
        [SerializeField, Min(0.1f)] private float doorRoomCheckDistance = 2.4f;
        [SerializeField, Min(0.1f)] private float doorRoomSearchPointRadius = 9f;
        [SerializeField, Min(0f)] private float doorRoomMinimumDepth = 0.35f;

        [Header("Garage Doors")]
        [SerializeField] private bool useGarageDoorSwitches = true;
        [SerializeField] private bool closePlayerOpenedGarageDoors = true;
        [SerializeField] private bool closeAnyOpenGarageDoorForSecurity = true;
        [SerializeField] private bool closePlayerGarageDoorsWithoutRouteProof = true;
        [SerializeField, Min(0.1f)] private float garageDoorSearchRadius = 22f;
        [SerializeField, Min(0f)] private float garageDoorSecurityRadius = 0f;
        [SerializeField, Min(0.1f)] private float garageSwitchUseDistance = 1.25f;
        [SerializeField, Min(0f)] private float garageSwitchDestinationSampleRadius = 1.5f;
        [SerializeField, Min(0f)] private float garageDoorWaitTimeout = 6f;
        [SerializeField, Min(0f)] private float garageDoorCloseCooldown = 4f;
        [SerializeField, Min(0.05f)] private float garageDoorSecurityCheckInterval = 0.75f;

        [Header("Suspicion And Memory")]
        [SerializeField, Min(0f)] private float suspicionDecayPerSecond = 0.035f;
        [SerializeField, Range(0f, 1f)] private float curiousThreshold = 0.18f;
        [SerializeField, Range(0f, 1f)] private float suspiciousThreshold = 0.48f;
        [SerializeField, Range(0f, 1f)] private float certainThreshold = 0.82f;
        [SerializeField, Min(0f)] private float environmentAwarenessRadius = 16f;
        [SerializeField, Min(0f)] private float repeatedDisturbanceBonus = 0.12f;
        [SerializeField, Min(0f)] private float learnedFalseAlarmPenalty = 0.08f;
        [SerializeField, Min(0f)] private float memoryDecayPerDeath = 0.82f;
        [SerializeField, Range(0f, 1f)] private float resumeInterruptedTaskChance = 0.72f;
        [SerializeField, Min(0f)] private float blockedTaskRetryDelay = 18f;

        [Header("Post-Encounter Vigilance")]
        [SerializeField, Min(0f)] private float postEncounterTaskCooldown = 25f;
        [SerializeField, Min(0f)] private float vigilancePatrolRadius = 10f;
        [SerializeField, Min(0f)] private float vigilanceWaitMinimum = 1.4f;
        [SerializeField, Min(0f)] private float vigilanceWaitMaximum = 3f;

        [Header("Readable Searching")]
        [SerializeField, Min(0f)] private float searchLookAngle = 70f;
        [SerializeField, Min(0.1f)] private float searchLookSpeed = 1.4f;
        [SerializeField, Min(0f)] private float closetSearchRadius = 8f;
        [SerializeField, Min(0f)] private float closetSearchWaitTime = 1.1f;
        [SerializeField, Min(0f)] private float favoriteHideSpotWeight = 5f;

        [Header("Hunt Mode")]
        [SerializeField, Min(0f)] private float huntDuration = 15f;
        [SerializeField, Min(0)] private int minimumSearchPointsAfterChase = 3;
        [SerializeField, Min(0f)] private float huntPointWaitTime = 0.65f;
        [SerializeField, Min(0f)] private float huntDirectionWeight = 8f;
        [SerializeField, Min(0f)] private float huntDistancePenalty = 0.08f;
        [SerializeField, Min(0f)] private float huntDestinationTimePadding = 0.35f;
        [SerializeField, Min(0f)] private float playerDirectionSampleMaximumGap = 0.5f;
        [SerializeField, Min(0f)] private float playerDirectionMinimumDistance = 0.01f;

        [Header("Chase")]
        [SerializeField, Min(0f)] private float chaseMemoryTime = 3.5f;
        [SerializeField, Min(0f)] private float chaseRepathInterval = 0.12f;
        [SerializeField, Min(0f)] private float catchDistance = 0.72f;
        [SerializeField, Min(0f)] private float catchAnimationFallbackDuration = 0.55f;
        [SerializeField, Min(0f)] private float maximumCatchAnimationDuration = 2.5f;
        [SerializeField, Min(0f)] private float quickCatchPresentationDuration = 0.8f;
        [SerializeField, Min(0f)] private float catchPresentationDistance = 1.35f;
        [SerializeField, Min(0f)] private float offMeshDirectChaseSpeed = 4.5f;
        [SerializeField, Min(0f)] private float climbCommitVerticalDifference = 0.45f;
        [SerializeField, Min(0f)] private float dropCommitVerticalDifference = 0.55f;
        [SerializeField, Min(0f)] private float climbLinkSearchInterval = 0.35f;
        [SerializeField, Min(0f)] private float climbLinkArrivalDistance = 0.75f;
        [SerializeField, Min(0f)] private float unreachableGiveUpTime = 8f;
        [SerializeField, Min(0f)] private float chaseProgressDistance = 0.45f;
        [SerializeField, Min(0f)] private float predictionMaximumDistance = 3.5f;
        [SerializeField, Range(0f, 1f)] private float predictionChance = 0.72f;
        [SerializeField, Range(0f, 1f)] private float predictionMistakeChance = 0.16f;
        [SerializeField, Min(0.1f)] private float predictionDecisionInterval = 0.85f;
        [SerializeField, Min(0f)] private float lastSeenVerificationDuration = 1.8f;
        [SerializeField, Min(0.1f)] private float lastSeenVerificationSampleRadius = 1.25f;

        private BehaviorState currentState;
        private readonly HashSet<NeighborSearchPoint> visitedSearchPoints = new();
        private readonly HashSet<ClosetHideSpot> searchedHideSpots = new();
        private readonly Dictionary<NeighborSearchPoint, float> searchPointMemory = new();
        private readonly Dictionary<ClosetHideSpot, float> hideSpotMemory = new();
        private readonly Dictionary<NeighborTaskLocation, float> taskCompletionMemory = new();
        private readonly Dictionary<GameObject, float> disturbanceMemory = new();
        private readonly Dictionary<GameObject, float> falseAlarmMemory = new();
        private readonly Dictionary<Door, int> observedUnexpectedDoorOpenSequences = new();
        private readonly Dictionary<NeighborTaskLocation, float> lastTaskCompletionTimes = new();
        private readonly Dictionary<NeighborTaskLocation, float> blockedTaskUntilTimes = new();
        private NeighborTaskLocation lastTaskLocation;
        private NeighborSearchPoint currentSearchPoint;
        private ClosetHideSpot currentHideSpot;
        private ClosetHideSpot witnessedPlayerHideSpot;
        private NeighborClimbLink currentClimbLink;
        private Vector3 currentGoal;
        private Vector3 lastKnownPlayerPosition;
        private Vector3 lastObservedPlayerPosition;
        private Vector3 lastSeenPlayerMoveDirection;
        private float waitUntilTime;
        private float goalWaitDuration;
        private float huntUntilTime;
        private int requiredSearchPointVisits;
        private float lastPlayerSeenTime = float.NegativeInfinity;
        private float nextChaseRepathTime;
        private float nextClimbLinkSearchTime;
        private float stunnedUntilTime;
        private float bestChaseDistance;
        private float lastChaseProgressTime;
        private float ignorePlayerSightUntilTime;
        private bool hasObservedPlayerPosition;
        private bool hasPlayerMoveDirectionForCurrentChase;
        private bool isPlayerVisible;
        private bool waitingAtGoal;
        private bool currentHideSpotKnownOccupied;
        private NeighborTaskLocation.TaskAnimationPhase currentTaskAnimationPhase;
        private NeighborTaskLocation currentTaskLocation;
        private NeighborTaskLocation activeTaskAudioLocation;
        private NeighborTaskLocation interruptedTaskLocation;
        private NeighborAnimationController animationController;
        private PlayerController caughtPlayer;
        private float catchPlayerAtTime;
        private NeighborMotor.MoveMode investigationMoveMode = NeighborMotor.MoveMode.Walk;
        private Vector3 startingPosition;
        private Quaternion startingRotation;
        private float suspicion;
        private GameObject currentInvestigationSource;
        private Door currentUnexpectedOpenDoor;
        private Vector3 currentDoorRoomCheckPosition;
        private Vector3 cachedPredictionDirection;
        private float nextPredictionDecisionTime;
        private bool isVerifyingLastSeenPosition;
        private Vector3 lastSeenVerificationPosition;
        private float lastSeenVerificationUntilTime;
        private float tasksSuppressedUntilTime;
        private HouseGarageDoorMotion activeGarageDoor;
        private LightSwitch activeGarageSwitch;
        private BehaviorState garageResumeState;
        private Vector3 garageResumeGoal;
        private bool activeGarageDesiredOpen;
        private bool garageSwitchToggled;
        private bool activeGarageSecurityResponse;
        private float garageDoorWaitUntilTime;
        private float nextGarageDoorCloseTime;
        private float nextGarageDoorSecurityCheckTime;

        public BehaviorState CurrentState => currentState;
        public Transform Player => player;
        public Vector3 CurrentGoal => currentGoal;
        public Vector3 LastKnownPlayerPosition => lastKnownPlayerPosition;
        public Vector3 LastSeenPlayerMoveDirection => lastSeenPlayerMoveDirection;
        public bool HasSeenPlayer => !float.IsNegativeInfinity(lastPlayerSeenTime);
        public bool IsPlayerVisible => isPlayerVisible;
        public bool IsWaitingAtGoal => waitingAtGoal;
        public float WaitTimeRemaining => waitingAtGoal ? Mathf.Max(0f, waitUntilTime - Time.time) : 0f;
        public float HuntTimeRemaining => currentState == BehaviorState.HuntMode ? Mathf.Max(0f, huntUntilTime - Time.time) : 0f;
        public int VisitedSearchPointCount => visitedSearchPoints.Count;
        public int RequiredSearchPointVisits => requiredSearchPointVisits;
        public NeighborTaskLocation CurrentTaskLocation => currentTaskLocation;
        public bool IsAtTaskUsePoint => currentState == BehaviorState.Task && IsAtCurrentTaskUseDistance();
        public NeighborSearchPoint CurrentSearchPoint => currentSearchPoint;
        public ClosetHideSpot CurrentHideSpot => currentHideSpot;
        public GameObject CurrentInvestigationSource => currentInvestigationSource;
        public Door CurrentUnexpectedOpenDoor => currentUnexpectedOpenDoor;
        public Vector3 CurrentDoorRoomCheckPosition => currentDoorRoomCheckPosition;
        public float Suspicion => suspicion;
        public bool IsVerifyingLastSeenPosition => currentState == BehaviorState.Chase && isVerifyingLastSeenPosition;
        public Vector3 LastSeenVerificationPosition => lastSeenVerificationPosition;
        public float LastSeenVerificationTimeRemaining => IsVerifyingLastSeenPosition && lastSeenVerificationUntilTime > 0f
            ? Mathf.Max(0f, lastSeenVerificationUntilTime - Time.time)
            : 0f;
        public bool IsPostEncounterVigilant => Time.time < tasksSuppressedUntilTime;
        public float PostEncounterVigilanceTimeRemaining => Mathf.Max(0f, tasksSuppressedUntilTime - Time.time);
        public SuspicionLevel CurrentSuspicionLevel => GetSuspicionLevel();
        public NeighborTaskLocation ActiveTaskLocation => currentState == BehaviorState.Task
            && currentTaskAnimationPhase != NeighborTaskLocation.TaskAnimationPhase.None
            ? currentTaskLocation
            : null;
        public NeighborTaskLocation.TaskAnimationPhase ActiveTaskAnimationPhase => ActiveTaskLocation != null
            ? currentTaskAnimationPhase
            : NeighborTaskLocation.TaskAnimationPhase.None;
        public bool IsCatchingPlayer => currentState == BehaviorState.Catching;
        public bool IsSearchingHideSpot => currentState == BehaviorState.HuntMode
            && currentHideSpot != null
            && waitingAtGoal
            && motor != null
            && motor.HasArrived;
        public bool IsWaitingDuringWander => currentState == BehaviorState.Wander
            && waitingAtGoal
            && motor != null
            && motor.HasArrived;
        public NeighborObjectHandling ObjectHandling => objectHandling;
        public NeighborLightSwitchInteractor LightSwitchInteractor => lightSwitchInteractor;
        public HouseGarageDoorMotion ActiveGarageDoor => activeGarageDoor;
        public LightSwitch ActiveGarageSwitch => activeGarageSwitch;
        public bool IsGarageSecurityResponse => activeGarageSecurityResponse;
        public bool IsAtInvestigationGoal => currentState == BehaviorState.Investigate
            && waitingAtGoal
            && motor != null
            && motor.HasArrived;

        private void Awake()
        {
            startingPosition = transform.position;
            startingRotation = transform.rotation;
            motor = motor != null ? motor : GetComponent<NeighborMotor>();
            vision = vision != null ? vision : GetComponent<NeighborVision>();
            hearing = hearing != null ? hearing : GetComponent<NeighborHearing>();
            objectHandling = objectHandling != null ? objectHandling : GetComponent<NeighborObjectHandling>();
            lightSwitchInteractor = lightSwitchInteractor != null
                ? lightSwitchInteractor
                : GetComponent<NeighborLightSwitchInteractor>();
            if (lightSwitchInteractor == null)
            {
                lightSwitchInteractor = gameObject.AddComponent<NeighborLightSwitchInteractor>();
            }

            animationController = GetComponent<NeighborAnimationController>();
            ResolvePlayer();
        }

        private void OnEnable()
        {
            NeighborEnvironmentalAwareness.EnvironmentChanged += HandleEnvironmentChanged;
            Door.UnexpectedlyOpened += HandleUnexpectedDoorOpened;
            Door.Disturbed += HandleDoorDisturbed;
            if (motor != null)
            {
                motor.DestinationAbandoned += HandleDestinationAbandoned;
            }

            if (hearing != null)
            {
                hearing.NoiseHeard += HandleNoiseHeard;
            }
        }

        private void OnDisable()
        {
            NeighborTaskLocation.ReleaseAllFor(this, motor);
            NeighborEnvironmentalAwareness.EnvironmentChanged -= HandleEnvironmentChanged;
            Door.UnexpectedlyOpened -= HandleUnexpectedDoorOpened;
            Door.Disturbed -= HandleDoorDisturbed;
            if (motor != null)
            {
                motor.DestinationAbandoned -= HandleDestinationAbandoned;
            }

            if (hearing != null)
            {
                hearing.NoiseHeard -= HandleNoiseHeard;
            }

            StopActiveTaskAudio();
        }

        private void Start()
        {
            ChooseNextRoutineGoal();
        }

        private void Update()
        {
            ResolvePlayer();
            UpdateSuspicion(Time.deltaTime);
            if (IsStunned)
            {
                return;
            }

            if (currentState == BehaviorState.Stunned)
            {
                ChooseNextRoutineGoal();
            }

            UpdatePerception();
            if (TryHandleGarageDoorSecurity())
            {
                return;
            }

            UpdateState();
        }

        public void Stun(float duration)
        {
            if (currentState == BehaviorState.Catching)
            {
                return;
            }

            RememberInterruptedTask();
            suspicion = Mathf.Max(suspicion, certainThreshold);
            RememberPlayerActivity(transform.position);
            stunnedUntilTime = Mathf.Max(stunnedUntilTime, Time.time + duration);
            currentClimbLink = null;
            motor?.Stop();
            SetState(BehaviorState.Stunned);
        }

        public void ObserveOpenDoor(Door door, Vector3 observerPosition)
        {
            HandleUnexpectedDoorOpened(door, observerPosition);
        }

        public void ObservePlayerEnteringHideSpot(ClosetHideSpot hideSpot, PlayerController hidingPlayer)
        {
            if (hideSpot == null || hidingPlayer == null || vision == null
                || !vision.TrySeeTarget(out Transform seenTarget, out Vector3 seenPosition)
                || seenTarget == null
                || seenTarget.root != hidingPlayer.transform.root)
            {
                return;
            }

            RememberInterruptedTask();
            player = hidingPlayer.transform;
            witnessedPlayerHideSpot = hideSpot;
            lastKnownPlayerPosition = seenPosition;
            lastPlayerSeenTime = Time.time;
            suspicion = 1f;
        }

        public void HandlePlayerFinishedHiding(ClosetHideSpot hideSpot, PlayerController hidingPlayer)
        {
            if (hideSpot == null || hidingPlayer == null || witnessedPlayerHideSpot != hideSpot)
            {
                return;
            }

            player = hidingPlayer.transform;
            lastKnownPlayerPosition = hideSpot.SearchPosition;
            suspicion = 1f;
            BeginHuntMode(lastKnownPlayerPosition);
        }

        private bool IsStunned => Time.time < stunnedUntilTime;

        private void UpdatePerception()
        {
            isPlayerVisible = false;
            if (vision == null)
            {
                return;
            }

            if (currentState == BehaviorState.Catching)
            {
                return;
            }

            if (Time.time < ignorePlayerSightUntilTime)
            {
                return;
            }

            if (vision.TrySeeTarget(out Transform seenTarget, out Vector3 seenPosition))
            {
                isPlayerVisible = true;
                isVerifyingLastSeenPosition = false;
                lastSeenVerificationUntilTime = 0f;
                RememberInterruptedTask();
                player = seenTarget;
                if (currentState != BehaviorState.Chase)
                {
                    hasObservedPlayerPosition = false;
                    hasPlayerMoveDirectionForCurrentChase = false;
                }

                UpdateLastSeenPlayerDirection(seenPosition);
                lastKnownPlayerPosition = seenPosition;
                lastPlayerSeenTime = Time.time;
                suspicion = 1f;
                RefreshPostEncounterVigilance();
                RememberPlayerActivity(seenPosition);
                SetState(BehaviorState.Chase);
            }
        }

        private void UpdateState()
        {
            switch (currentState)
            {
                case BehaviorState.Chase:
                    UpdateChase();
                    break;
                case BehaviorState.Catching:
                    UpdateCatching();
                    break;
                case BehaviorState.Investigate:
                    UpdateInvestigate();
                    break;
                case BehaviorState.HuntMode:
                    UpdateHuntMode();
                    break;
                case BehaviorState.ObjectHandling:
                    UpdateObjectHandling();
                    break;
                case BehaviorState.LightSwitchUse:
                    UpdateLightSwitchUse();
                    break;
                case BehaviorState.GarageDoorUse:
                    UpdateGarageDoorUse();
                    break;
                case BehaviorState.Task:
                case BehaviorState.Wander:
                case BehaviorState.Idle:
                    UpdateRoutine();
                    break;
            }
        }

        private void UpdateChase()
        {
            if (motor == null)
            {
                return;
            }

            motor.SetMoveMode(NeighborMotor.MoveMode.Run);
            if (isVerifyingLastSeenPosition)
            {
                UpdateLastSeenVerification();
                return;
            }

            bool canStillChase = player != null && Time.time - lastPlayerSeenTime <= chaseMemoryTime;
            Vector3 chasePosition = player != null && canStillChase
                ? (isPlayerVisible ? player.position : GetPredictedChasePosition())
                : lastKnownPlayerPosition;

            if (player != null && canStillChase && !isPlayerVisible && ShouldGiveUpChase(chasePosition))
            {
                GiveUpChase(chasePosition);
                return;
            }

            if (player != null && canStillChase && TryHandleVerticalChase(chasePosition))
            {
                return;
            }

            if (player != null && canStillChase && motor.IsOffMeshChasing)
            {
                motor.MoveDirectlyToward(chasePosition, offMeshDirectChaseSpeed, 12f);
                return;
            }

            if (Time.time >= nextChaseRepathTime)
            {
                currentGoal = chasePosition;
                if (!motor.SetDestination(chasePosition)
                    && TryStartGarageDoorUseForGoal(chasePosition, BehaviorState.Chase, true))
                {
                    return;
                }

                nextChaseRepathTime = Time.time + chaseRepathInterval;
            }

            if (isPlayerVisible && player != null)
            {
                motor.FaceTowards(player.position, 16f);
                bestChaseDistance = Vector3.Distance(transform.position, player.position);
                lastChaseProgressTime = Time.time;
            }
            else
            {
                motor.FaceMovementDirection(12f);
            }

            if (player != null && Vector3.Distance(transform.position, player.position) <= catchDistance)
            {
                PlayerController playerController = player.GetComponent<PlayerController>() ?? player.GetComponentInParent<PlayerController>();
                BeginCatchingPlayer(playerController);
                return;
            }

            if (!canStillChase)
            {
                BeginLastSeenVerification();
            }
        }

        private void BeginLastSeenVerification()
        {
            if (motor == null || isVerifyingLastSeenPosition)
            {
                return;
            }

            currentClimbLink = null;
            isVerifyingLastSeenPosition = true;
            lastSeenVerificationUntilTime = 0f;
            if (!motor.TrySetDestinationNear(
                    lastKnownPlayerPosition,
                    lastSeenVerificationSampleRadius,
                    out lastSeenVerificationPosition))
            {
                isVerifyingLastSeenPosition = false;
                BeginHuntMode(lastKnownPlayerPosition);
                return;
            }

            currentGoal = lastSeenVerificationPosition;
            motor.SetMoveMode(NeighborMotor.MoveMode.Run);
        }

        private void UpdateLastSeenVerification()
        {
            if (motor == null)
            {
                return;
            }

            motor.SetMoveMode(NeighborMotor.MoveMode.Run);
            if (!motor.HasArrived)
            {
                motor.FaceMovementDirection(14f);
                return;
            }

            if (lastSeenVerificationUntilTime <= 0f)
            {
                motor.Stop();
                lastSeenVerificationUntilTime = Time.time + lastSeenVerificationDuration;
            }

            if (Time.time < lastSeenVerificationUntilTime)
            {
                FaceSearchSweep();
                return;
            }

            isVerifyingLastSeenPosition = false;
            lastSeenVerificationUntilTime = 0f;
            BeginHuntMode(lastKnownPlayerPosition);
        }

        private void BeginCatchingPlayer(PlayerController playerController)
        {
            if (playerController == null || currentState == BehaviorState.Catching)
            {
                return;
            }

            caughtPlayer = playerController;
            player = playerController.transform;
            caughtPlayer.PrepareForDeath();
            currentClimbLink = null;
            motor?.Stop();
            PositionForCatchPresentation(playerController.transform);
            SetState(BehaviorState.Catching);

            float animationDuration = animationController != null
                ? animationController.CatchAnimationDuration
                : 0f;
            float duration = animationDuration > 0f ? animationDuration : catchAnimationFallbackDuration;
            duration = Mathf.Min(duration, maximumCatchAnimationDuration);
            duration = Mathf.Min(duration, quickCatchPresentationDuration > 0f ? quickCatchPresentationDuration : 0.8f);
            catchPlayerAtTime = Time.time + duration;

            PlayerDeathController deathController = caughtPlayer.GetComponent<PlayerDeathController>();
            deathController?.BeginCatchCameraFocus(transform);
        }

        public void TryCatchPlayer(PlayerController playerController)
        {
            if (currentState == BehaviorState.Chase)
            {
                BeginCatchingPlayer(playerController);
            }
        }

        private void PositionForCatchPresentation(Transform caughtPlayerTransform)
        {
            if (caughtPlayerTransform == null || catchPresentationDistance <= 0f)
            {
                return;
            }

            Vector3 fromPlayer = transform.position - caughtPlayerTransform.position;
            fromPlayer.y = 0f;
            if (fromPlayer.sqrMagnitude <= 0.001f)
            {
                fromPlayer = -caughtPlayerTransform.forward;
                fromPlayer.y = 0f;
            }

            Vector3 presentationPosition = caughtPlayerTransform.position
                + fromPlayer.normalized * catchPresentationDistance;
            presentationPosition.y = transform.position.y;
            Vector3 faceDirection = caughtPlayerTransform.position - presentationPosition;
            faceDirection.y = 0f;
            Quaternion presentationRotation = faceDirection.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(faceDirection.normalized, Vector3.up)
                : transform.rotation;
            if (motor != null)
            {
                motor.ResetToPosition(presentationPosition, presentationRotation);
            }
            else
            {
                transform.SetPositionAndRotation(presentationPosition, presentationRotation);
            }
        }

        private void UpdateCatching()
        {
            motor?.Stop();
            if (caughtPlayer != null)
            {
                motor?.FaceTowards(caughtPlayer.transform.position, 18f);
            }

            if (Time.time < catchPlayerAtTime)
            {
                return;
            }

            PlayerController playerToKill = caughtPlayer;
            caughtPlayer = null;
            if (!PlayerDeathController.Kill(playerToKill, transform.position))
            {
                BeginHuntMode(lastKnownPlayerPosition);
            }
        }

        private bool ShouldGiveUpChase(Vector3 chasePosition)
        {
            if (motor != null && (motor.IsTraversingSpecialMove || motor.IsOffMeshChasing))
            {
                bestChaseDistance = Vector3.Distance(transform.position, chasePosition);
                lastChaseProgressTime = Time.time;
                return false;
            }

            float currentDistance = Vector3.Distance(transform.position, chasePosition);
            if (currentDistance < bestChaseDistance - chaseProgressDistance)
            {
                bestChaseDistance = currentDistance;
                lastChaseProgressTime = Time.time;
                return false;
            }

            return Time.time - lastChaseProgressTime >= unreachableGiveUpTime;
        }

        public void HandlePlayerRespawned(float sightGraceTime)
        {
            objectHandling?.CancelActivity();
            ClearGarageDoorUse();
            currentClimbLink = null;
            currentSearchPoint = null;
            currentHideSpot = null;
            currentHideSpotKnownOccupied = false;
            witnessedPlayerHideSpot = null;
            caughtPlayer = null;
            catchPlayerAtTime = 0f;
            currentTaskLocation = null;
            visitedSearchPoints.Clear();
            searchedHideSpots.Clear();
            observedUnexpectedDoorOpenSequences.Clear();
            blockedTaskUntilTimes.Clear();
            StopActiveTaskAudio();
            motor?.ResetToPosition(startingPosition, startingRotation);
            lastPlayerSeenTime = float.NegativeInfinity;
            ignorePlayerSightUntilTime = Time.time + Mathf.Max(0f, sightGraceTime);
            hasObservedPlayerPosition = false;
            hasPlayerMoveDirectionForCurrentChase = false;
            isPlayerVisible = false;
            waitingAtGoal = false;
            suspicion = 0f;
            interruptedTaskLocation = null;
            currentInvestigationSource = null;
            currentUnexpectedOpenDoor = null;
            currentDoorRoomCheckPosition = default;
            isVerifyingLastSeenPosition = false;
            lastSeenVerificationUntilTime = 0f;
            tasksSuppressedUntilTime = 0f;
            DecayPersistentMemory();
            ChooseNextRoutineGoal();
        }

        private void GiveUpChase(Vector3 _)
        {
            BeginLastSeenVerification();
        }

        private bool TryHandleVerticalChase(Vector3 chasePosition)
        {
            float verticalDelta = chasePosition.y - transform.position.y;
            if (verticalDelta >= climbCommitVerticalDifference)
            {
                if (motor.TryClimbToward(player))
                {
                    currentClimbLink = null;
                    return true;
                }

                if (TryUseClimbLink(chasePosition))
                {
                    return true;
                }

                return false;
            }

            if (verticalDelta <= -dropCommitVerticalDifference || motor.IsDetachedFromNavMesh)
            {
                if (motor.TryJumpDownToward(player))
                {
                    currentClimbLink = null;
                    return true;
                }
            }

            currentClimbLink = null;
            return false;
        }

        private bool TryUseClimbLink(Vector3 chasePosition)
        {
            if (currentClimbLink != null && !currentClimbLink.CanUse(transform.position, chasePosition))
            {
                currentClimbLink = null;
            }

            if (currentClimbLink == null || Time.time >= nextClimbLinkSearchTime)
            {
                nextClimbLinkSearchTime = Time.time + climbLinkSearchInterval;
                currentClimbLink = FindBestClimbLink(chasePosition);
            }

            if (currentClimbLink == null)
            {
                return false;
            }

            Vector3 bottomPosition = currentClimbLink.BottomPosition;
            if (Vector3.Distance(transform.position, bottomPosition) <= climbLinkArrivalDistance)
            {
                motor.FaceTowards(currentClimbLink.TopPosition, 18f);
                if (motor.TryUseClimbLink(currentClimbLink))
                {
                    return true;
                }
            }

            if (motor.SetDestination(bottomPosition))
            {
                motor.FaceTowards(bottomPosition, 12f);
                return true;
            }

            currentClimbLink = null;
            return false;
        }

        private NeighborClimbLink FindBestClimbLink(Vector3 chasePosition)
        {
            NeighborClimbLink bestLink = null;
            float bestScore = float.NegativeInfinity;
            foreach (NeighborClimbLink climbLink in NeighborClimbLink.Links)
            {
                if (climbLink == null || !climbLink.CanUse(transform.position, chasePosition))
                {
                    continue;
                }

                if (!motor.CanReach(climbLink.BottomPosition, out float pathDistance, out _))
                {
                    continue;
                }

                float score = climbLink.Score(transform.position, chasePosition, pathDistance);
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestLink = climbLink;
            }

            return bestLink;
        }

        private void UpdateInvestigate()
        {
            if (motor == null)
            {
                return;
            }

            motor.SetMoveMode(investigationMoveMode);
            if (!motor.HasArrived)
            {
                return;
            }

            TryResolveTelevisionInvestigationSource();

            if (!GoalWaitComplete())
            {
                FaceSearchSweep();
                return;
            }

            suspicion = Mathf.Max(0f, suspicion - 0.12f);
            RememberFalseAlarm();
            currentSearchPoint = null;
            currentUnexpectedOpenDoor = null;
            currentDoorRoomCheckPosition = default;
            ChooseNextRoutineGoal();
        }

        private void UpdateHuntMode()
        {
            if (motor == null)
            {
                return;
            }

            motor.SetMoveMode(currentHideSpotKnownOccupied && !motor.HasArrived
                ? NeighborMotor.MoveMode.Run
                : currentHideSpot != null || motor.HasArrived
                    ? NeighborMotor.MoveMode.Cautious
                    : NeighborMotor.MoveMode.Run);
            if (Time.time >= huntUntilTime && HasCompletedRequiredSearchPoints())
            {
                EndHuntMode();
                return;
            }

            if (currentSearchPoint == null && currentHideSpot == null)
            {
                if (!TrySetNextHuntDestination())
                {
                    HandleNoHuntDestination();
                }

                return;
            }

            if (!motor.HasArrived)
            {
                return;
            }

            if (!GoalWaitComplete())
            {
                FaceSearchSweep();
                return;
            }

            if (currentHideSpot != null)
            {
                ClosetHideSpot searchedSpot = currentHideSpot;
                currentHideSpot = null;
                currentHideSpotKnownOccupied = false;
                if (witnessedPlayerHideSpot == searchedSpot)
                {
                    witnessedPlayerHideSpot = null;
                }

                searchedHideSpots.Add(searchedSpot);
                PlayerController foundPlayer = searchedSpot.SearchByNeighbor();
                if (foundPlayer != null)
                {
                    hideSpotMemory[searchedSpot] = GetMemory(hideSpotMemory, searchedSpot) + 1f;
                    player = foundPlayer.transform;
                    lastKnownPlayerPosition = foundPlayer.transform.position;
                    lastPlayerSeenTime = Time.time;
                    suspicion = 1f;
                    BeginCatchingPlayer(foundPlayer);
                    return;
                }
            }

            if (currentSearchPoint != null)
            {
                visitedSearchPoints.Add(currentSearchPoint);
            }

            currentSearchPoint = null;
            if (!TrySetNextHuntDestination())
            {
                HandleNoHuntDestination();
            }
        }

        private void HandleNoHuntDestination()
        {
            ReduceRequiredSearchPointsToReachableTotal();
            if (HasCompletedRequiredSearchPoints())
            {
                EndHuntMode();
            }
            else
            {
                motor?.Stop();
            }
        }

        private void UpdateRoutine()
        {
            if (motor == null)
            {
                return;
            }

            motor.SetMoveMode(IsPostEncounterVigilant || CurrentSuspicionLevel >= SuspicionLevel.Suspicious
                ? NeighborMotor.MoveMode.Cautious
                : NeighborMotor.MoveMode.Walk);
            if (currentState == BehaviorState.Task
                && (currentTaskLocation == null || !currentTaskLocation.isActiveAndEnabled))
            {
                NeighborTaskLocation.ReleaseAllFor(this, motor);
                StopActiveTaskAudio();
                ChooseNextRoutineGoal();
                return;
            }

            if (currentState == BehaviorState.Task
                && motor.IsAnchoredForTask
                && currentTaskAnimationPhase == NeighborTaskLocation.TaskAnimationPhase.None)
            {
                FinishCurrentTaskUse();
                ChooseNextRoutineGoal();
                return;
            }

            if (currentState == BehaviorState.Task && !waitingAtGoal && !HasReachedCurrentTask())
            {
                return;
            }

            if (currentState != BehaviorState.Task && !motor.HasArrived)
            {
                return;
            }

            if (!GoalWaitComplete())
            {
                if (currentState == BehaviorState.Task && currentTaskLocation != null)
                {
                    motor.FaceTowards(transform.position + currentTaskLocation.LookDirection, 5f);
                }
                else if (IsPostEncounterVigilant || CurrentSuspicionLevel >= SuspicionLevel.Curious)
                {
                    FaceSearchSweep();
                }

                return;
            }

            if (currentState == BehaviorState.Task && TryStartForcedNextTask())
            {
                return;
            }

            ChooseNextRoutineGoal();
        }

        private void HandleNoiseHeard(NeighborNoiseStimulus stimulus)
        {
            if (stimulus.Loudness01 < minimumInvestigateLoudness || ShouldIgnoreNoiseBecausePlayerIsKnown())
            {
                return;
            }

            RememberInterruptedTask();
            AddSuspicion(stimulus.Loudness01 * Mathf.Lerp(0.35f, 0.75f, stimulus.Urgency01), stimulus.SourceObject);
            RememberPlayerActivity(stimulus.Position);
            currentInvestigationSource = stimulus.SourceObject;
            currentUnexpectedOpenDoor = null;
            currentDoorRoomCheckPosition = default;
            goalWaitDuration = investigationWaitTime;
            waitingAtGoal = false;
            currentTaskLocation = null;
            StopActiveTaskAudio();
            investigationMoveMode = stimulus.Urgency01 >= minimumUrgencyToRunToNoise || CurrentSuspicionLevel == SuspicionLevel.Certain
                ? NeighborMotor.MoveMode.Run
                : CurrentSuspicionLevel >= SuspicionLevel.Suspicious
                    ? NeighborMotor.MoveMode.Cautious
                    : NeighborMotor.MoveMode.Walk;
            motor?.SetMoveMode(investigationMoveMode);
            if (motor != null && motor.TrySetDestinationNear(stimulus.Position, noiseDestinationSampleRadius, out Vector3 investigatePosition))
            {
                currentGoal = investigatePosition;
                SetState(BehaviorState.Investigate);
            }
            else if (motor != null)
            {
                currentGoal = stimulus.Position;
                TryStartGarageDoorUseForGoal(stimulus.Position, BehaviorState.Investigate, true);
            }
        }

        private void HandleDestinationAbandoned(Vector3 _)
        {
            waitingAtGoal = false;
            if (currentState != BehaviorState.GarageDoorUse
                && TryStartGarageDoorUseForGoal(currentGoal, currentState, true))
            {
                return;
            }

            switch (currentState)
            {
                case BehaviorState.Chase:
                    if (!isPlayerVisible)
                    {
                        if (isVerifyingLastSeenPosition)
                        {
                            isVerifyingLastSeenPosition = false;
                            lastSeenVerificationUntilTime = 0f;
                            BeginHuntMode(lastKnownPlayerPosition);
                        }
                        else
                        {
                            BeginLastSeenVerification();
                        }
                    }

                    return;
                case BehaviorState.HuntMode:
                    if (currentSearchPoint != null)
                    {
                        visitedSearchPoints.Add(currentSearchPoint);
                    }

                    if (currentHideSpot != null)
                    {
                        searchedHideSpots.Add(currentHideSpot);
                    }

                    currentSearchPoint = null;
                    currentHideSpot = null;
                    currentHideSpotKnownOccupied = false;
                    if (!TrySetNextHuntDestination())
                    {
                        HandleNoHuntDestination();
                    }

                    return;
                case BehaviorState.Investigate:
                    RememberFalseAlarm();
                    currentSearchPoint = null;
                    currentUnexpectedOpenDoor = null;
                    currentDoorRoomCheckPosition = default;
                    suspicion = Mathf.Max(0f, suspicion - 0.08f);
                    ChooseNextRoutineGoal();
                    return;
                case BehaviorState.Task:
                    StopActiveTaskAudio();
                    if (currentTaskLocation != null)
                    {
                        blockedTaskUntilTimes[currentTaskLocation] = Time.time + blockedTaskRetryDelay;
                    }

                    currentTaskLocation = null;
                    interruptedTaskLocation = null;
                    ChooseNextRoutineGoal();
                    return;
                case BehaviorState.Wander:
                case BehaviorState.Idle:
                    ChooseNextRoutineGoal();
                    return;
                case BehaviorState.ObjectHandling:
                    objectHandling?.CancelActivity();
                    ChooseNextRoutineGoal();
                    return;
                case BehaviorState.LightSwitchUse:
                    lightSwitchInteractor?.CancelActivity();
                    ChooseNextRoutineGoal();
                    return;
                case BehaviorState.GarageDoorUse:
                    ClearGarageDoorUse();
                    ChooseNextRoutineGoal();
                    return;
            }
        }

        private bool ShouldIgnoreNoiseBecausePlayerIsKnown()
        {
            if (currentState == BehaviorState.Chase
                || currentState == BehaviorState.Catching
                || currentState == BehaviorState.HuntMode)
            {
                return true;
            }

            return player != null && Time.time - lastPlayerSeenTime <= chaseMemoryTime;
        }

        private void ChooseNextRoutineGoal()
        {
            if (motor == null)
            {
                currentTaskLocation = null;
                SetState(BehaviorState.Idle);
                return;
            }

            if (IsPostEncounterVigilant)
            {
                if (TryStartVigilancePatrol())
                {
                    return;
                }

                currentTaskLocation = null;
                goalWaitDuration = vigilanceWaitMinimum;
                waitingAtGoal = false;
                SetState(BehaviorState.Idle);
                return;
            }

            if (interruptedTaskLocation != null
                && interruptedTaskLocation.isActiveAndEnabled
                && Random.value <= resumeInterruptedTaskChance
                && TryStartTask(interruptedTaskLocation))
            {
                interruptedTaskLocation = null;
                return;
            }

            if (objectHandling != null && objectHandling.TryBeginRoutine(out Vector3 objectHandlingGoal))
            {
                currentGoal = objectHandlingGoal;
                currentTaskLocation = null;
                waitingAtGoal = false;
                SetState(BehaviorState.ObjectHandling);
                return;
            }

            if (lightSwitchInteractor != null && lightSwitchInteractor.TryBeginRoutine(out Vector3 lightSwitchGoal))
            {
                currentGoal = lightSwitchGoal;
                currentTaskLocation = null;
                waitingAtGoal = false;
                SetState(BehaviorState.LightSwitchUse);
                return;
            }

            if (TryStartGarageDoorCloseRoutine())
            {
                return;
            }

            bool shouldWander = Random.value < GetAdjustedWanderChance() || NeighborTaskLocation.Locations.Count == 0;
            if (shouldWander && motor.TryGetRandomReachablePoint(transform.position, wanderRadius, out Vector3 wanderPoint))
            {
                currentGoal = wanderPoint;
                goalWaitDuration = Random.Range(idleWaitMinimum, Mathf.Max(idleWaitMinimum, idleWaitMaximum));
                waitingAtGoal = false;
                currentTaskLocation = null;
                if (motor.SetDestination(currentGoal))
                {
                    SetState(BehaviorState.Wander);
                    return;
                }
            }

            NeighborTaskLocation taskLocation = GetRandomTaskLocation();
            if (taskLocation == null)
            {
                currentTaskLocation = null;
                SetState(BehaviorState.Idle);
                goalWaitDuration = Random.Range(idleWaitMinimum, Mathf.Max(idleWaitMinimum, idleWaitMaximum));
                waitingAtGoal = false;
                return;
            }

            if (TryStartTask(taskLocation))
            {
                return;
            }

            currentTaskLocation = null;
            SetState(BehaviorState.Idle);
        }

        private bool TryStartVigilancePatrol()
        {
            if (motor == null || vigilancePatrolRadius <= 0f)
            {
                return false;
            }

            Vector3 patrolOrigin = HasSeenPlayer ? lastKnownPlayerPosition : transform.position;
            if (!motor.TryGetRandomReachablePoint(patrolOrigin, vigilancePatrolRadius, out Vector3 patrolPoint)
                && !motor.TryGetRandomReachablePoint(transform.position, vigilancePatrolRadius, out patrolPoint))
            {
                return false;
            }

            currentGoal = patrolPoint;
            goalWaitDuration = Random.Range(
                vigilanceWaitMinimum,
                Mathf.Max(vigilanceWaitMinimum, vigilanceWaitMaximum));
            waitingAtGoal = false;
            currentTaskLocation = null;
            motor.SetMoveMode(NeighborMotor.MoveMode.Cautious);
            if (!motor.SetDestination(currentGoal))
            {
                return false;
            }

            SetState(BehaviorState.Wander);
            return true;
        }

        private bool GoalWaitComplete()
        {
            if (!waitingAtGoal)
            {
                waitingAtGoal = true;
                if (currentState == BehaviorState.Task)
                {
                    if (currentTaskLocation == null || !currentTaskLocation.BeginTaskUse(this, motor))
                    {
                        NeighborTaskLocation.ReleaseAllFor(this, motor);
                        return true;
                    }

                    BeginCurrentTaskAudio();
                    BeginTaskAnimationPhase(NeighborTaskLocation.TaskAnimationPhase.Starting);
                }
                else
                {
                    waitUntilTime = Time.time + goalWaitDuration;
                }
            }

            if (Time.time < waitUntilTime)
            {
                return false;
            }

            if (currentState != BehaviorState.Task)
            {
                return true;
            }

            if (currentTaskAnimationPhase == NeighborTaskLocation.TaskAnimationPhase.Starting)
            {
                BeginTaskAnimationPhase(NeighborTaskLocation.TaskAnimationPhase.Performing, goalWaitDuration);
                return false;
            }

            if (currentTaskAnimationPhase == NeighborTaskLocation.TaskAnimationPhase.Performing)
            {
                if (currentTaskLocation != null)
                {
                    taskCompletionMemory[currentTaskLocation] = GetMemory(taskCompletionMemory, currentTaskLocation) + 1f;
                    lastTaskCompletionTimes[currentTaskLocation] = Time.time;
                }

                StopActiveTaskAudio(true);
                BeginTaskAnimationPhase(NeighborTaskLocation.TaskAnimationPhase.Finishing);
                if (Time.time < waitUntilTime)
                {
                    return false;
                }
            }

            FinishCurrentTaskUse();
            return true;
        }

        private void FinishCurrentTaskUse()
        {
            bool wasAnchored = motor != null && motor.IsAnchoredForTask;
            currentTaskAnimationPhase = NeighborTaskLocation.TaskAnimationPhase.None;
            waitingAtGoal = false;
            StopActiveTaskAudio();
            currentTaskLocation?.EndTaskUse(this, motor);
            if (motor != null && !wasAnchored && motor.IsPaused)
            {
                motor.SetPaused(false);
            }
        }

        private void UpdateObjectHandling()
        {
            if (objectHandling != null && objectHandling.UpdateActivity(out Vector3 activityGoal))
            {
                currentGoal = activityGoal;
                return;
            }

            ChooseNextRoutineGoal();
        }

        private void UpdateLightSwitchUse()
        {
            if (lightSwitchInteractor != null && lightSwitchInteractor.UpdateActivity(out Vector3 activityGoal))
            {
                currentGoal = activityGoal;
                return;
            }

            ChooseNextRoutineGoal();
        }

        private void UpdateGarageDoorUse()
        {
            if (motor == null || activeGarageDoor == null || activeGarageSwitch == null)
            {
                ClearGarageDoorUse();
                ChooseNextRoutineGoal();
                return;
            }

            motor.SetMoveMode(garageResumeState == BehaviorState.Chase || activeGarageSecurityResponse
                ? NeighborMotor.MoveMode.Run
                : NeighborMotor.MoveMode.Cautious);

            if (!garageSwitchToggled)
            {
                if (!motor.HasArrived)
                {
                    motor.FaceMovementDirection(12f);
                    return;
                }

                Vector3 toSwitch = activeGarageSwitch.transform.position - transform.position;
                float verticalOffset = Mathf.Abs(toSwitch.y);
                toSwitch.y = 0f;
                if (toSwitch.sqrMagnitude > garageSwitchUseDistance * garageSwitchUseDistance
                    || verticalOffset > garageSwitchUseDistance)
                {
                    ClearGarageDoorUse();
                    ChooseNextRoutineGoal();
                    return;
                }

                motor.Stop();
                motor.FaceTowards(activeGarageSwitch.transform.position, 12f);
                activeGarageDoor.MarkNextChangeAsNeighborRequested();
                activeGarageSwitch.Toggle();
                garageSwitchToggled = true;
                garageDoorWaitUntilTime = Time.time + garageDoorWaitTimeout;
                return;
            }

            motor.Stop();
            motor.FaceTowards(activeGarageDoor.Position, 10f);
            bool desiredStateReached = activeGarageDesiredOpen
                ? activeGarageDoor.AllowsNavigationPassage
                : !activeGarageDoor.IsOpen;
            if (!desiredStateReached && Time.time < garageDoorWaitUntilTime)
            {
                return;
            }

            FinishGarageDoorUse();
        }

        private bool TryHandleGarageDoorSecurity()
        {
            if (currentState == BehaviorState.Chase
                || currentState == BehaviorState.Catching
                || currentState == BehaviorState.HuntMode
                || currentState == BehaviorState.Stunned
                || currentState == BehaviorState.GarageDoorUse
                || Time.time < nextGarageDoorSecurityCheckTime)
            {
                return false;
            }

            nextGarageDoorSecurityCheckTime = Time.time + garageDoorSecurityCheckInterval;
            return TryStartGarageDoorCloseRoutine();
        }

        private bool TryStartGarageDoorUseForGoal(Vector3 blockedGoal, BehaviorState resumeState, bool desiredOpen)
        {
            if (!useGarageDoorSwitches
                || motor == null
                || currentState == BehaviorState.GarageDoorUse
                || !TryFindGarageDoorSwitch(
                    blockedGoal,
                    desiredOpen,
                    out HouseGarageDoorMotion garageDoor,
                    out LightSwitch controlSwitch,
                    out Vector3 switchDestination))
            {
                return false;
            }

            activeGarageDoor = garageDoor;
            activeGarageSwitch = controlSwitch;
            activeGarageDesiredOpen = desiredOpen;
            activeGarageSecurityResponse = false;
            garageResumeGoal = blockedGoal;
            garageResumeState = resumeState;
            garageSwitchToggled = false;
            garageDoorWaitUntilTime = 0f;
            currentGoal = switchDestination;
            currentTaskLocation = null;
            waitingAtGoal = false;
            StopActiveTaskAudio();
            motor.SetMoveMode(resumeState == BehaviorState.Chase
                ? NeighborMotor.MoveMode.Run
                : NeighborMotor.MoveMode.Cautious);
            if (!motor.TrySetDestinationNear(
                    controlSwitch.transform.position,
                    garageSwitchDestinationSampleRadius,
                    out currentGoal))
            {
                ClearGarageDoorUse();
                return false;
            }

            SetState(BehaviorState.GarageDoorUse);
            return true;
        }

        private bool TryStartGarageDoorCloseRoutine()
        {
            if (!closePlayerOpenedGarageDoors
                || Time.time < nextGarageDoorCloseTime
                || !TryFindGarageDoorSwitch(
                    transform.position,
                    false,
                    out HouseGarageDoorMotion garageDoor,
                    out LightSwitch controlSwitch,
                    out Vector3 switchDestination))
            {
                return false;
            }

            activeGarageDoor = garageDoor;
            activeGarageSwitch = controlSwitch;
            activeGarageDesiredOpen = false;
            activeGarageSecurityResponse = true;
            garageResumeGoal = transform.position;
            garageResumeState = BehaviorState.Idle;
            garageSwitchToggled = false;
            garageDoorWaitUntilTime = 0f;
            currentGoal = switchDestination;
            currentTaskLocation = null;
            waitingAtGoal = false;
            StopActiveTaskAudio();
            motor.SetMoveMode(NeighborMotor.MoveMode.Run);
            if (!motor.TrySetDestinationNear(
                    controlSwitch.transform.position,
                    garageSwitchDestinationSampleRadius,
                    out currentGoal))
            {
                ClearGarageDoorUse();
                return false;
            }

            nextGarageDoorCloseTime = Time.time + garageDoorCloseCooldown;
            SetState(BehaviorState.GarageDoorUse);
            return true;
        }

        private bool TryFindGarageDoorSwitch(
            Vector3 goal,
            bool desiredOpen,
            out HouseGarageDoorMotion garageDoor,
            out LightSwitch controlSwitch,
            out Vector3 switchDestination)
        {
            garageDoor = null;
            controlSwitch = null;
            switchDestination = transform.position;
            if (motor == null)
            {
                return false;
            }

            float bestScore = float.NegativeInfinity;
            IReadOnlyList<HouseGarageDoorMotion> garageDoors = HouseGarageDoorMotion.ActiveGarageDoors;
            for (int i = 0; i < garageDoors.Count; i++)
            {
                HouseGarageDoorMotion candidateDoor = garageDoors[i];
                if (!IsGarageDoorCandidate(candidateDoor, goal, desiredOpen)
                    || !candidateDoor.TryGetNearestControlSwitch(transform.position, out LightSwitch candidateSwitch)
                    || !motor.CanReachNear(
                        candidateSwitch.transform.position,
                        garageSwitchDestinationSampleRadius,
                        out float switchPathDistance,
                        out Vector3 sampledSwitchPosition))
                {
                    continue;
                }

                float routeScore = desiredOpen
                    ? -DistancePointToSegment(candidateDoor.Position, transform.position, goal)
                    : 0f;
                float score = routeScore
                    - switchPathDistance * 0.2f
                    - Vector3.Distance(transform.position, candidateDoor.Position) * 0.05f;
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                garageDoor = candidateDoor;
                controlSwitch = candidateSwitch;
                switchDestination = sampledSwitchPosition;
            }

            return garageDoor != null;
        }

        private bool IsGarageDoorCandidate(HouseGarageDoorMotion garageDoor, Vector3 goal, bool desiredOpen)
        {
            if (garageDoor == null || !garageDoor.isActiveAndEnabled)
            {
                return false;
            }

            if (desiredOpen)
            {
                if (garageDoor.AllowsNavigationPassage || garageDoor.IsOpen)
                {
                    return false;
                }

                return Vector3.Distance(transform.position, garageDoor.Position) <= garageDoorSearchRadius
                    || Vector3.Distance(goal, garageDoor.Position) <= garageDoorSearchRadius
                    || DistancePointToSegment(garageDoor.Position, transform.position, goal) <= garageDoorSearchRadius;
            }

            return garageDoor.IsOpen
                && (closeAnyOpenGarageDoorForSecurity || !garageDoor.LastOpenedByNeighbor)
                && IsGarageDoorWithinSecurityRange(garageDoor, goal)
                && (closePlayerGarageDoorsWithoutRouteProof || HasAlternativeRouteAroundGarageDoor(garageDoor));
        }

        private bool IsGarageDoorWithinSecurityRange(HouseGarageDoorMotion garageDoor, Vector3 goal)
        {
            if (garageDoor == null)
            {
                return false;
            }

            return garageDoorSecurityRadius <= 0f
                || Vector3.Distance(transform.position, garageDoor.Position) <= garageDoorSecurityRadius
                || Vector3.Distance(goal, garageDoor.Position) <= garageDoorSecurityRadius;
        }

        private bool HasAlternativeRouteAroundGarageDoor(HouseGarageDoorMotion garageDoor)
        {
            if (garageDoor == null || motor == null || !garageDoor.AllowsNavigationPassage)
            {
                return false;
            }

            garageDoor.GetNavigationEndpoints(out Vector3 firstEndpoint, out Vector3 secondEndpoint);
            Vector3 target = (transform.position - firstEndpoint).sqrMagnitude
                <= (transform.position - secondEndpoint).sqrMagnitude
                    ? secondEndpoint
                    : firstEndpoint;
            bool wasSuppressed = garageDoor.IsNavigationPassageSuppressed;
            garageDoor.SetNavigationPassageSuppressed(true);
            bool canReach = motor.CanReach(target, out _, out _);
            garageDoor.SetNavigationPassageSuppressed(wasSuppressed);
            return canReach;
        }

        private void FinishGarageDoorUse()
        {
            bool desiredOpen = activeGarageDesiredOpen;
            BehaviorState resumeState = garageResumeState;
            Vector3 resumeGoal = garageResumeGoal;
            ClearGarageDoorUse();
            if (desiredOpen && resumeState == BehaviorState.Chase)
            {
                SetState(BehaviorState.Chase);
                return;
            }

            if (desiredOpen && resumeState == BehaviorState.Investigate && motor != null)
            {
                if (motor.TrySetDestinationNear(resumeGoal, noiseDestinationSampleRadius, out Vector3 investigatePosition))
                {
                    currentGoal = investigatePosition;
                    goalWaitDuration = investigationWaitTime;
                    waitingAtGoal = false;
                    SetState(BehaviorState.Investigate);
                    return;
                }
            }

            ChooseNextRoutineGoal();
        }

        private void ClearGarageDoorUse()
        {
            activeGarageDoor = null;
            activeGarageSwitch = null;
            activeGarageDesiredOpen = false;
            garageSwitchToggled = false;
            activeGarageSecurityResponse = false;
            garageDoorWaitUntilTime = 0f;
            garageResumeGoal = default;
            garageResumeState = BehaviorState.Idle;
        }

        private bool TryResolveTelevisionInvestigationSource()
        {
            Television television = currentInvestigationSource != null
                ? currentInvestigationSource.GetComponentInParent<Television>()
                    ?? currentInvestigationSource.GetComponentInChildren<Television>()
                : null;
            if (television == null || !television.IsOn)
            {
                return false;
            }

            Vector3 target = television.RemoteTargetPosition;
            if (Vector3.Distance(transform.position, target) > televisionShutoffDistance)
            {
                return false;
            }

            motor?.Stop();
            motor?.FaceTowards(target, 12f);
            television.SetOn(false, true);
            return true;
        }

        private static float DistancePointToSegment(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd)
        {
            Vector3 segment = segmentEnd - segmentStart;
            segment.y = 0f;
            Vector3 toPoint = point - segmentStart;
            toPoint.y = 0f;
            float segmentLengthSqr = segment.sqrMagnitude;
            if (segmentLengthSqr <= 0.001f)
            {
                return toPoint.magnitude;
            }

            float t = Mathf.Clamp01(Vector3.Dot(toPoint, segment) / segmentLengthSqr);
            Vector3 closest = segmentStart + segment * t;
            closest.y = point.y;
            return Vector3.Distance(point, closest);
        }

        private void BeginTaskAnimationPhase(
            NeighborTaskLocation.TaskAnimationPhase phase,
            float overrideDuration = -1f)
        {
            currentTaskAnimationPhase = phase;
            float duration = overrideDuration >= 0f
                ? overrideDuration
                : currentTaskLocation != null
                    ? currentTaskLocation.GetAnimationDuration(phase)
                    : 0f;
            waitUntilTime = Time.time + Mathf.Max(0f, duration);
        }

        private bool TryStartForcedNextTask()
        {
            if (IsPostEncounterVigilant)
            {
                return false;
            }

            NeighborTaskLocation completedTask = currentTaskLocation;
            NeighborTaskLocation forcedNextTask = completedTask != null ? completedTask.ForcedNextTask : null;
            if (forcedNextTask == null || !forcedNextTask.isActiveAndEnabled)
            {
                return false;
            }

            return TryStartTask(forcedNextTask);
        }

        private bool TryStartTask(NeighborTaskLocation taskLocation)
        {
            if (taskLocation == null || motor == null || IsPostEncounterVigilant)
            {
                return false;
            }

            NeighborTaskLocation.ReleaseAllFor(this, motor);
            if (!taskLocation.TryReserve(this))
            {
                return false;
            }

            lastTaskLocation = taskLocation;
            currentTaskLocation = taskLocation;
            goalWaitDuration = taskLocation.RandomWaitTime;
            waitingAtGoal = false;
            currentTaskAnimationPhase = NeighborTaskLocation.TaskAnimationPhase.None;

            if (!motor.TrySetDestinationNear(
                    taskLocation.Position,
                    taskLocation.NavigationSampleRadius,
                    out Vector3 sampledTaskPosition))
            {
                taskLocation.EndTaskUse(this, motor);
                currentTaskLocation = null;
                return false;
            }

            Vector3 sampleOffset = sampledTaskPosition - taskLocation.Position;
            sampleOffset.y = 0f;
            if (sampleOffset.sqrMagnitude > taskLocation.ArrivalDistance * taskLocation.ArrivalDistance)
            {
                motor.Stop();
                taskLocation.EndTaskUse(this, motor);
                currentTaskLocation = null;
                return false;
            }

            currentGoal = sampledTaskPosition;
            SetState(BehaviorState.Task);
            return true;
        }

        private bool HasReachedCurrentTask()
        {
            if (motor == null
                || currentTaskLocation == null
                || motor.IsTraversingSpecialMove
                || !currentTaskLocation.IsObjectPoseUsable)
            {
                return false;
            }

            bool pausedAtDestination = motor.IsPaused
                && !float.IsNaN(motor.RemainingDistance)
                && !float.IsInfinity(motor.RemainingDistance)
                && motor.RemainingDistance <= currentTaskLocation.ArrivalDistance + 0.1f;
            if (!motor.HasArrived && !pausedAtDestination)
            {
                return false;
            }

            return IsAtCurrentTaskUseHeight();
        }

        private bool IsAtCurrentTaskUseHeight()
        {
            if (currentTaskLocation == null || !currentTaskLocation.IsObjectPoseUsable)
            {
                return false;
            }

            return Mathf.Abs(currentTaskLocation.Position.y - transform.position.y)
                <= currentTaskLocation.MaximumUseVerticalOffset;
        }

        private bool IsAtCurrentTaskUseDistance()
        {
            if (currentTaskLocation == null || !currentTaskLocation.IsObjectPoseUsable)
            {
                return false;
            }

            Vector3 toTaskLocation = currentTaskLocation.Position - transform.position;
            float verticalOffset = Mathf.Abs(toTaskLocation.y);
            toTaskLocation.y = 0f;
            return verticalOffset <= currentTaskLocation.MaximumUseVerticalOffset
                && toTaskLocation.sqrMagnitude
                    <= currentTaskLocation.ArrivalDistance * currentTaskLocation.ArrivalDistance;
        }

        private NeighborTaskLocation GetRandomTaskLocation()
        {
            IReadOnlyList<NeighborTaskLocation> locations = NeighborTaskLocation.Locations;
            if (locations.Count == 0)
            {
                return null;
            }

            NeighborTaskLocation bestLocation = null;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < locations.Count; i++)
            {
                NeighborTaskLocation candidate = locations[i];
                if (candidate == null
                    || !candidate.IsAvailable
                    || blockedTaskUntilTimes.TryGetValue(candidate, out float blockedUntilTime)
                    && Time.time < blockedUntilTime)
                {
                    continue;
                }

                float score = candidate.SelectionPriority + Random.Range(0f, 0.35f);
                score -= Vector3.Distance(transform.position, candidate.Position) * 0.035f;
                score -= GetMemory(taskCompletionMemory, candidate) * 0.18f;

                if (candidate == lastTaskLocation && !candidate.CanRepeatImmediately && locations.Count > 1)
                {
                    score -= 4f;
                }

                if (lastTaskCompletionTimes.TryGetValue(candidate, out float lastCompletedTime))
                {
                    score -= Mathf.Clamp01(1f - (Time.time - lastCompletedTime) / 35f) * 1.5f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestLocation = candidate;
                }
            }

            return bestLocation;
        }

        private void UpdateLastSeenPlayerDirection(Vector3 seenPosition)
        {
            if (hasObservedPlayerPosition && Time.time - lastPlayerSeenTime <= playerDirectionSampleMaximumGap)
            {
                Vector3 movement = seenPosition - lastObservedPlayerPosition;
                movement.y = 0f;
                if (movement.sqrMagnitude >= playerDirectionMinimumDistance * playerDirectionMinimumDistance)
                {
                    lastSeenPlayerMoveDirection = movement.normalized;
                    hasPlayerMoveDirectionForCurrentChase = true;
                }
            }

            lastObservedPlayerPosition = seenPosition;
            hasObservedPlayerPosition = true;
        }

        private void BeginHuntMode(Vector3 lostPlayerPosition)
        {
            ClosetHideSpot priorityHideSpot = witnessedPlayerHideSpot;
            lastKnownPlayerPosition = lostPlayerPosition;
            isVerifyingLastSeenPosition = false;
            lastSeenVerificationUntilTime = 0f;
            currentClimbLink = null;
            currentTaskLocation = null;
            currentSearchPoint = null;
            currentHideSpot = null;
            currentHideSpotKnownOccupied = false;
            StopActiveTaskAudio();
            visitedSearchPoints.Clear();
            searchedHideSpots.Clear();
            huntUntilTime = Time.time + huntDuration;
            requiredSearchPointVisits = GetReachableSearchPointCount(minimumSearchPointsAfterChase);
            RememberPlayerActivity(lostPlayerPosition);

            Vector3 fallbackDirection = lostPlayerPosition - transform.position;
            fallbackDirection.y = 0f;
            if ((!hasPlayerMoveDirectionForCurrentChase || lastSeenPlayerMoveDirection.sqrMagnitude <= 0.001f)
                && fallbackDirection.sqrMagnitude > 0.001f)
            {
                lastSeenPlayerMoveDirection = fallbackDirection.normalized;
            }

            SetState(BehaviorState.HuntMode);
            if (priorityHideSpot != null && TrySetKnownHideSpotSearch(priorityHideSpot))
            {
                return;
            }

            if (!TrySetNextHuntDestination())
            {
                motor?.Stop();
            }
        }

        private bool TrySetNextHuntDestination()
        {
            if (motor == null || Time.time >= huntUntilTime && HasCompletedRequiredSearchPoints())
            {
                return false;
            }

            if (Time.time < huntUntilTime && TrySetNextHideSpotSearch())
            {
                return true;
            }

            NeighborSearchPoint bestPoint = null;
            Vector3 bestDestination = default;
            float bestScore = float.NegativeInfinity;
            float remainingHuntTime = huntUntilTime - Time.time;
            float runSpeed = Mathf.Max(0.01f, motor.GetMoveSpeed(NeighborMotor.MoveMode.Run));
            IReadOnlyList<NeighborSearchPoint> points = NeighborSearchPoint.Points;

            for (int i = 0; i < points.Count; i++)
            {
                NeighborSearchPoint point = points[i];
                if (point == null || visitedSearchPoints.Contains(point)
                    || !motor.CanReach(point.Position, out float pathDistance, out Vector3 sampledPosition))
                {
                    continue;
                }

                float estimatedVisitTime = pathDistance / runSpeed + huntPointWaitTime + huntDestinationTimePadding;
                if (HasCompletedRequiredSearchPoints() && estimatedVisitTime > remainingHuntTime)
                {
                    continue;
                }

                Vector3 fromLastSeen = point.Position - lastKnownPlayerPosition;
                fromLastSeen.y = 0f;
                float alignment = fromLastSeen.sqrMagnitude > 0.001f && lastSeenPlayerMoveDirection.sqrMagnitude > 0.001f
                    ? Vector3.Dot(lastSeenPlayerMoveDirection.normalized, fromLastSeen.normalized)
                    : 0f;
                float score = alignment * huntDirectionWeight
                    + point.SelectionPriority
                    + GetMemory(searchPointMemory, point)
                    - pathDistance * huntDistancePenalty;
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestPoint = point;
                bestDestination = sampledPosition;
            }

            if (bestPoint == null)
            {
                return false;
            }

            currentSearchPoint = bestPoint;
            currentHideSpot = null;
            currentGoal = bestDestination;
            goalWaitDuration = huntPointWaitTime;
            waitingAtGoal = false;
            motor.SetMoveMode(NeighborMotor.MoveMode.Run);
            if (motor.SetDestination(bestDestination))
            {
                return true;
            }

            currentSearchPoint = null;
            return false;
        }

        private bool HasCompletedRequiredSearchPoints()
        {
            return visitedSearchPoints.Count >= requiredSearchPointVisits;
        }

        private int GetReachableSearchPointCount(int maximumCount)
        {
            if (motor == null || maximumCount <= 0)
            {
                return 0;
            }

            int reachableCount = 0;
            IReadOnlyList<NeighborSearchPoint> points = NeighborSearchPoint.Points;
            for (int i = 0; i < points.Count; i++)
            {
                NeighborSearchPoint point = points[i];
                if (point != null && motor.CanReach(point.Position, out _, out _))
                {
                    reachableCount++;
                    if (reachableCount >= maximumCount)
                    {
                        return maximumCount;
                    }
                }
            }

            return reachableCount;
        }

        private void ReduceRequiredSearchPointsToReachableTotal()
        {
            if (motor == null)
            {
                requiredSearchPointVisits = visitedSearchPoints.Count;
                return;
            }

            int reachableUnvisitedCount = 0;
            IReadOnlyList<NeighborSearchPoint> points = NeighborSearchPoint.Points;
            for (int i = 0; i < points.Count; i++)
            {
                NeighborSearchPoint point = points[i];
                if (point != null
                    && !visitedSearchPoints.Contains(point)
                    && motor.CanReach(point.Position, out _, out _))
                {
                    reachableUnvisitedCount++;
                }
            }

            requiredSearchPointVisits = Mathf.Min(
                requiredSearchPointVisits,
                visitedSearchPoints.Count + reachableUnvisitedCount);
        }

        private bool TrySetNextHideSpotSearch()
        {
            IReadOnlyList<ClosetHideSpot> hideSpots = ClosetHideSpot.HideSpots;
            ClosetHideSpot bestSpot = null;
            Vector3 bestDestination = default;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < hideSpots.Count; i++)
            {
                ClosetHideSpot hideSpot = hideSpots[i];
                if (hideSpot == null || searchedHideSpots.Contains(hideSpot))
                {
                    continue;
                }

                Vector3 toLastKnown = hideSpot.SearchPosition - lastKnownPlayerPosition;
                toLastKnown.y = 0f;
                if (toLastKnown.magnitude > closetSearchRadius)
                {
                    continue;
                }

                if (!motor.CanReach(hideSpot.SearchPosition, out float pathDistance, out Vector3 sampledPosition))
                {
                    continue;
                }

                float score = GetMemory(hideSpotMemory, hideSpot) * favoriteHideSpotWeight;
                score -= pathDistance * 0.1f;
                score += Random.Range(0f, 0.35f);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSpot = hideSpot;
                    bestDestination = sampledPosition;
                }
            }

            if (bestSpot == null)
            {
                return false;
            }

            currentHideSpot = bestSpot;
            currentHideSpotKnownOccupied = false;
            currentSearchPoint = null;
            currentGoal = bestDestination;
            goalWaitDuration = closetSearchWaitTime;
            waitingAtGoal = false;
            motor.SetMoveMode(NeighborMotor.MoveMode.Cautious);
            if (motor.SetDestination(bestDestination))
            {
                return true;
            }

            currentHideSpot = null;
            currentHideSpotKnownOccupied = false;
            return false;
        }

        private bool TrySetKnownHideSpotSearch(ClosetHideSpot hideSpot)
        {
            if (hideSpot == null || motor == null
                || !motor.CanReach(hideSpot.SearchPosition, out _, out Vector3 sampledPosition))
            {
                witnessedPlayerHideSpot = null;
                return false;
            }

            currentHideSpot = hideSpot;
            currentHideSpotKnownOccupied = true;
            currentSearchPoint = null;
            currentGoal = sampledPosition;
            goalWaitDuration = Mathf.Min(0.25f, closetSearchWaitTime);
            waitingAtGoal = false;
            motor.SetMoveMode(NeighborMotor.MoveMode.Run);
            if (motor.SetDestination(sampledPosition))
            {
                return true;
            }

            currentHideSpot = null;
            currentHideSpotKnownOccupied = false;
            witnessedPlayerHideSpot = null;
            return false;
        }

        private void EndHuntMode()
        {
            currentSearchPoint = null;
            currentHideSpot = null;
            currentHideSpotKnownOccupied = false;
            witnessedPlayerHideSpot = null;
            visitedSearchPoints.Clear();
            searchedHideSpots.Clear();
            requiredSearchPointVisits = 0;
            suspicion = Mathf.Max(suspicion, suspiciousThreshold);
            RefreshPostEncounterVigilance();
            ChooseNextRoutineGoal();
        }

        private void SetState(BehaviorState state)
        {
            if (currentState == state)
            {
                return;
            }

            if (currentState == BehaviorState.Investigate
                && state != BehaviorState.Investigate
                && state != BehaviorState.GarageDoorUse)
            {
                currentSearchPoint = null;
                currentUnexpectedOpenDoor = null;
                currentDoorRoomCheckPosition = default;
            }

            if (currentState == BehaviorState.Task && state != BehaviorState.Task)
            {
                NeighborTaskLocation.ReleaseAllFor(this, motor);
                currentTaskAnimationPhase = NeighborTaskLocation.TaskAnimationPhase.None;
                StopActiveTaskAudio();
            }

            if (currentState == BehaviorState.ObjectHandling && state != BehaviorState.ObjectHandling)
            {
                objectHandling?.CancelActivity();
            }

            if (currentState == BehaviorState.LightSwitchUse && state != BehaviorState.LightSwitchUse)
            {
                lightSwitchInteractor?.CancelActivity();
            }

            if (currentState == BehaviorState.GarageDoorUse && state != BehaviorState.GarageDoorUse)
            {
                ClearGarageDoorUse();
            }

            if (currentState == BehaviorState.Chase && state != BehaviorState.Chase)
            {
                isVerifyingLastSeenPosition = false;
                lastSeenVerificationUntilTime = 0f;
            }

            currentState = state;
            motor?.SetChasePursuitActive(state == BehaviorState.Chase);
            if (state == BehaviorState.Chase)
            {
                AdaptiveSecurityDirector.ReportChaseStarted();
                bestChaseDistance = player != null
                    ? Vector3.Distance(transform.position, player.position)
                    : float.PositiveInfinity;
                lastChaseProgressTime = Time.time;
            }
        }

        private void BeginCurrentTaskAudio()
        {
            if (currentTaskLocation == null || activeTaskAudioLocation == currentTaskLocation)
            {
                return;
            }

            StopActiveTaskAudio();
            activeTaskAudioLocation = currentTaskLocation;
            activeTaskAudioLocation.BeginTaskAudio();
        }

        private void StopActiveTaskAudio(bool playFinishSound = false)
        {
            if (activeTaskAudioLocation == null)
            {
                return;
            }

            activeTaskAudioLocation.StopTaskAudio(playFinishSound);
            activeTaskAudioLocation = null;
        }

        private void HandleEnvironmentChanged(Vector3 position, float changeSuspicion, GameObject source)
        {
            if (source != null && source.GetComponentInParent<NeighborBrain>() == this)
            {
                return;
            }

            if (Vector3.Distance(transform.position, position) > environmentAwarenessRadius)
            {
                return;
            }

            HandleEnvironmentalClue(position, changeSuspicion, source);
        }

        private void HandleDoorDisturbed(Door door, float changeSuspicion)
        {
            if (door == null || door.NeighborAlertDistance <= 0f
                || Vector3.Distance(transform.position, door.transform.position) > door.NeighborAlertDistance)
            {
                return;
            }

            HandleEnvironmentalClue(door.transform.position, changeSuspicion, door.gameObject);
        }

        private void HandleEnvironmentalClue(Vector3 position, float changeSuspicion, GameObject source)
        {
            RememberInterruptedTask();
            AddSuspicion(changeSuspicion, source);
            RememberPlayerActivity(position);
            currentInvestigationSource = source;
            currentUnexpectedOpenDoor = null;
            currentDoorRoomCheckPosition = default;

            if (currentState == BehaviorState.Chase
                || currentState == BehaviorState.Catching
                || currentState == BehaviorState.HuntMode
                || motor == null)
            {
                return;
            }

            goalWaitDuration = investigationWaitTime * Mathf.Lerp(0.75f, 1.4f, suspicion);
            waitingAtGoal = false;
            currentTaskLocation = null;
            StopActiveTaskAudio();
            investigationMoveMode = CurrentSuspicionLevel >= SuspicionLevel.Certain
                ? NeighborMotor.MoveMode.Run
                : NeighborMotor.MoveMode.Cautious;

            if (motor.TrySetDestinationNear(position, noiseDestinationSampleRadius, out Vector3 investigatePosition))
            {
                currentGoal = investigatePosition;
                SetState(BehaviorState.Investigate);
            }
        }

        private void HandleUnexpectedDoorOpened(Door door, Vector3 openerPosition)
        {
            if (door == null || !door.IsOpen || door.OpenSequence <= 0 || door.LastOpenedByNeighbor
                || observedUnexpectedDoorOpenSequences.TryGetValue(door, out int observedSequence)
                && observedSequence == door.OpenSequence)
            {
                return;
            }

            if (door.NeighborAlertDistance <= 0f
                || Vector3.Distance(transform.position, door.transform.position) > door.NeighborAlertDistance)
            {
                return;
            }

            observedUnexpectedDoorOpenSequences[door] = door.OpenSequence;
            RememberInterruptedTask();
            AddSuspicion(unexpectedOpenDoorSuspicion, door.gameObject);
            Vector3 roomCheckPosition = door.GetPositionBeyond(openerPosition, doorRoomCheckDistance);
            RememberPlayerActivity(roomCheckPosition);

            if (currentState == BehaviorState.Chase
                || currentState == BehaviorState.Catching
                || currentState == BehaviorState.HuntMode
                || motor == null)
            {
                return;
            }

            currentInvestigationSource = door.gameObject;
            currentUnexpectedOpenDoor = door;
            currentDoorRoomCheckPosition = roomCheckPosition;
            currentTaskLocation = null;
            currentHideSpot = null;
            StopActiveTaskAudio();
            goalWaitDuration = investigationWaitTime * Mathf.Lerp(1f, 1.45f, suspicion);
            waitingAtGoal = false;
            investigationMoveMode = CurrentSuspicionLevel >= SuspicionLevel.Certain
                ? NeighborMotor.MoveMode.Run
                : NeighborMotor.MoveMode.Cautious;
            motor.SetMoveMode(investigationMoveMode);

            if (!TrySetDoorRoomCheckDestination(door, openerPosition, roomCheckPosition))
            {
                currentInvestigationSource = null;
                currentUnexpectedOpenDoor = null;
                currentDoorRoomCheckPosition = default;
                return;
            }

            SetState(BehaviorState.Investigate);
        }

        private bool TrySetDoorRoomCheckDestination(Door door, Vector3 openerPosition, Vector3 roomCheckPosition)
        {
            Vector3 beyondDirection = door.GetDirectionBeyond(openerPosition);
            NeighborSearchPoint bestPoint = null;
            Vector3 bestDestination = default;
            float bestScore = float.NegativeInfinity;
            IReadOnlyList<NeighborSearchPoint> points = NeighborSearchPoint.Points;
            for (int i = 0; i < points.Count; i++)
            {
                NeighborSearchPoint point = points[i];
                if (point == null)
                {
                    continue;
                }

                Vector3 fromDoor = point.Position - door.transform.position;
                fromDoor.y = 0f;
                float depthBeyondDoor = Vector3.Dot(beyondDirection, fromDoor);
                if (depthBeyondDoor < doorRoomMinimumDepth || fromDoor.magnitude > doorRoomSearchPointRadius
                    || !motor.CanReach(point.Position, out float pathDistance, out Vector3 sampledPosition))
                {
                    continue;
                }

                float score = point.SelectionPriority
                    + depthBeyondDoor * 0.2f
                    - Vector3.Distance(point.Position, roomCheckPosition) * 0.18f
                    - pathDistance * 0.08f;
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestPoint = point;
                bestDestination = sampledPosition;
            }

            if (bestPoint != null)
            {
                currentSearchPoint = bestPoint;
                currentGoal = bestDestination;
                return motor.SetDestination(bestDestination);
            }

            currentSearchPoint = null;
            if (!motor.CanReach(roomCheckPosition, out _, out Vector3 fallbackDestination))
            {
                return false;
            }

            currentDoorRoomCheckPosition = fallbackDestination;
            currentGoal = fallbackDestination;
            return motor.SetDestination(fallbackDestination);
        }

        private void UpdateSuspicion(float deltaTime)
        {
            if (currentState == BehaviorState.Chase || currentState == BehaviorState.Catching)
            {
                suspicion = 1f;
                return;
            }

            if (IsPostEncounterVigilant)
            {
                suspicion = Mathf.MoveTowards(
                    suspicion,
                    suspiciousThreshold,
                    suspicionDecayPerSecond * 0.5f * deltaTime);
                return;
            }

            float decayMultiplier = currentState == BehaviorState.HuntMode ? 0.2f : 1f;
            suspicion = Mathf.MoveTowards(suspicion, 0f, suspicionDecayPerSecond * decayMultiplier * deltaTime);
        }

        private void RefreshPostEncounterVigilance()
        {
            tasksSuppressedUntilTime = Mathf.Max(
                tasksSuppressedUntilTime,
                Time.time + postEncounterTaskCooldown);
        }

        private void AddSuspicion(float amount, GameObject source)
        {
            float repeatedBonus = 0f;
            float falseAlarmPenalty = 0f;
            if (source != null)
            {
                float priorDisturbances = disturbanceMemory.TryGetValue(source, out float remembered) ? remembered : 0f;
                repeatedBonus = Mathf.Min(0.35f, priorDisturbances * repeatedDisturbanceBonus);
                disturbanceMemory[source] = priorDisturbances + 1f;
                falseAlarmPenalty = GetMemory(falseAlarmMemory, source) * learnedFalseAlarmPenalty;
            }

            suspicion = Mathf.Clamp01(suspicion + Mathf.Max(0f, amount + repeatedBonus - falseAlarmPenalty));
        }

        private SuspicionLevel GetSuspicionLevel()
        {
            if (suspicion >= certainThreshold)
            {
                return SuspicionLevel.Certain;
            }

            if (suspicion >= suspiciousThreshold)
            {
                return SuspicionLevel.Suspicious;
            }

            return suspicion >= curiousThreshold ? SuspicionLevel.Curious : SuspicionLevel.Relaxed;
        }

        private void FaceSearchSweep()
        {
            if (motor == null)
            {
                return;
            }

            Vector3 baseDirection = currentSearchPoint != null ? currentSearchPoint.LookDirection : transform.forward;
            baseDirection.y = 0f;
            if (baseDirection.sqrMagnitude <= 0.001f)
            {
                baseDirection = transform.forward;
            }

            float sweepAngle = Mathf.Sin(Time.time * searchLookSpeed) * searchLookAngle;
            Vector3 lookDirection = Quaternion.AngleAxis(sweepAngle, Vector3.up) * baseDirection.normalized;
            motor.FaceTowards(transform.position + lookDirection * 3f, 7f);
        }

        private Vector3 GetPredictedChasePosition()
        {
            if (player == null)
            {
                return lastKnownPlayerPosition;
            }

            CharacterController playerController = player.GetComponent<CharacterController>() ?? player.GetComponentInChildren<CharacterController>();
            Vector3 velocity = playerController != null ? playerController.velocity : lastSeenPlayerMoveDirection;
            velocity.y = 0f;
            if (velocity.sqrMagnitude <= 0.01f)
            {
                velocity = lastSeenPlayerMoveDirection;
            }

            if (velocity.sqrMagnitude <= 0.01f)
            {
                return player.position;
            }

            if (Time.time >= nextPredictionDecisionTime)
            {
                nextPredictionDecisionTime = Time.time + predictionDecisionInterval;
                cachedPredictionDirection = Random.value <= predictionChance ? velocity.normalized : Vector3.zero;
                if (cachedPredictionDirection.sqrMagnitude > 0.01f && Random.value < predictionMistakeChance)
                {
                    cachedPredictionDirection = Quaternion.AngleAxis(Random.Range(-70f, 70f), Vector3.up) * cachedPredictionDirection;
                }
            }

            Vector3 predictionDirection = cachedPredictionDirection;
            if (predictionDirection.sqrMagnitude <= 0.01f)
            {
                return player.position;
            }

            float leadDistance = Mathf.Min(predictionMaximumDistance, velocity.magnitude * 0.45f);
            return player.position + predictionDirection * leadDistance;
        }

        private void RememberInterruptedTask()
        {
            if (currentState == BehaviorState.Task && currentTaskLocation != null)
            {
                interruptedTaskLocation = currentTaskLocation;
            }
        }

        private void RememberPlayerActivity(Vector3 position)
        {
            IReadOnlyList<NeighborSearchPoint> points = NeighborSearchPoint.Points;
            NeighborSearchPoint closestPoint = null;
            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < points.Count; i++)
            {
                NeighborSearchPoint point = points[i];
                if (point == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(position, point.Position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPoint = point;
                }
            }

            if (closestPoint != null && closestDistance <= closetSearchRadius * 1.5f)
            {
                searchPointMemory[closestPoint] = GetMemory(searchPointMemory, closestPoint) + 0.35f;
            }
        }

        private void DecayPersistentMemory()
        {
            DecayMemory(searchPointMemory);
            DecayMemory(hideSpotMemory);
            DecayMemory(taskCompletionMemory);
            DecayMemory(disturbanceMemory);
            DecayMemory(falseAlarmMemory);
        }

        private void DecayMemory<TKey>(Dictionary<TKey, float> memory) where TKey : UnityEngine.Object
        {
            List<TKey> keys = new(memory.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                TKey key = keys[i];
                if (key == null)
                {
                    memory.Remove(key);
                    continue;
                }

                memory[key] *= memoryDecayPerDeath;
            }
        }

        private static float GetMemory<TKey>(Dictionary<TKey, float> memory, TKey key) where TKey : UnityEngine.Object
        {
            return key != null && memory.TryGetValue(key, out float value) ? value : 0f;
        }

        private float GetAdjustedWanderChance()
        {
            return CurrentSuspicionLevel switch
            {
                SuspicionLevel.Certain => wanderChance * 0.1f,
                SuspicionLevel.Suspicious => wanderChance * 0.35f,
                SuspicionLevel.Curious => wanderChance * 0.7f,
                _ => wanderChance
            };
        }

        private void RememberFalseAlarm()
        {
            if (currentInvestigationSource == null)
            {
                return;
            }

            falseAlarmMemory[currentInvestigationSource] = GetMemory(falseAlarmMemory, currentInvestigationSource) + 1f;
            currentInvestigationSource = null;
        }

        private void ResolvePlayer()
        {
            if (player != null)
            {
                return;
            }

            PlayerController playerController = FindAnyObjectByType<PlayerController>();
            if (playerController != null)
            {
                player = playerController.transform;
            }
        }
    }
}
