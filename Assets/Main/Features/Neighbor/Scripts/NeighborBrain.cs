using System.Collections.Generic;
using Neighbor.Main.Features.Interaction;
using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Neighbor
{
    public static class NeighborEnvironmentalAwareness
    {
        public static event System.Action<Vector3, float, GameObject> EnvironmentChanged;

        public static void Report(Vector3 position, float suspicion, GameObject source)
        {
            EnvironmentChanged?.Invoke(position, Mathf.Clamp01(suspicion), source);
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
            Investigate,
            Chase,
            HuntMode,
            Stunned
        }

        [Header("References")]
        [SerializeField] private NeighborMotor motor;
        [SerializeField] private NeighborVision vision;
        [SerializeField] private NeighborHearing hearing;
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

        [Header("Readable Searching")]
        [SerializeField, Min(0f)] private float searchLookAngle = 70f;
        [SerializeField, Min(0.1f)] private float searchLookSpeed = 1.4f;
        [SerializeField, Min(0f)] private float closetSearchRadius = 8f;
        [SerializeField, Min(0f)] private float closetSearchWaitTime = 1.1f;
        [SerializeField, Min(0f)] private float favoriteHideSpotWeight = 5f;

        [Header("Hunt Mode")]
        [SerializeField, Min(0f)] private float huntDuration = 15f;
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
        [SerializeField, Min(0f)] private float offMeshDirectChaseSpeed = 4.5f;
        [SerializeField, Min(0f)] private float climbCommitVerticalDifference = 0.45f;
        [SerializeField, Min(0f)] private float dropCommitVerticalDifference = 0.55f;
        [SerializeField, Min(0f)] private float climbLinkSearchInterval = 0.35f;
        [SerializeField, Min(0f)] private float climbLinkArrivalDistance = 0.75f;
        [SerializeField, Min(0f)] private float unreachableGiveUpTime = 8f;
        [SerializeField, Min(0f)] private float chaseProgressDistance = 0.45f;
        [SerializeField, Min(0f)] private float giveUpSightIgnoreTime = 3f;
        [SerializeField, Min(0f)] private float predictionMaximumDistance = 3.5f;
        [SerializeField, Range(0f, 1f)] private float predictionChance = 0.72f;
        [SerializeField, Range(0f, 1f)] private float predictionMistakeChance = 0.16f;
        [SerializeField, Min(0.1f)] private float predictionDecisionInterval = 0.85f;

        private BehaviorState currentState;
        private readonly HashSet<NeighborSearchPoint> visitedSearchPoints = new();
        private readonly HashSet<ClosetHideSpot> searchedHideSpots = new();
        private readonly Dictionary<NeighborSearchPoint, float> searchPointMemory = new();
        private readonly Dictionary<ClosetHideSpot, float> hideSpotMemory = new();
        private readonly Dictionary<NeighborTaskLocation, float> taskCompletionMemory = new();
        private readonly Dictionary<GameObject, float> disturbanceMemory = new();
        private readonly Dictionary<GameObject, float> falseAlarmMemory = new();
        private readonly Dictionary<NeighborTaskLocation, float> lastTaskCompletionTimes = new();
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
        private float lastPlayerSeenTime = float.NegativeInfinity;
        private float nextChaseRepathTime;
        private float nextClimbLinkSearchTime;
        private float stunnedUntilTime;
        private float bestChaseDistance;
        private float lastChaseProgressTime;
        private float ignorePlayerSightUntilTime;
        private bool hasObservedPlayerPosition;
        private bool hasPlayerMoveDirectionForCurrentChase;
        private bool waitingAtGoal;
        private bool currentHideSpotKnownOccupied;
        private NeighborTaskLocation currentTaskLocation;
        private NeighborTaskLocation activeTaskAudioLocation;
        private NeighborTaskLocation interruptedTaskLocation;
        private NeighborMotor.MoveMode investigationMoveMode = NeighborMotor.MoveMode.Walk;
        private Vector3 startingPosition;
        private Quaternion startingRotation;
        private float suspicion;
        private GameObject currentInvestigationSource;
        private Vector3 cachedPredictionDirection;
        private float nextPredictionDecisionTime;

        public BehaviorState CurrentState => currentState;
        public Vector3 LastKnownPlayerPosition => lastKnownPlayerPosition;
        public float Suspicion => suspicion;
        public SuspicionLevel CurrentSuspicionLevel => GetSuspicionLevel();
        public NeighborTaskLocation ActiveTaskLocation => currentState == BehaviorState.Task && waitingAtGoal
            ? currentTaskLocation
            : null;

        private void Awake()
        {
            startingPosition = transform.position;
            startingRotation = transform.rotation;
            motor = motor != null ? motor : GetComponent<NeighborMotor>();
            vision = vision != null ? vision : GetComponent<NeighborVision>();
            hearing = hearing != null ? hearing : GetComponent<NeighborHearing>();
            ResolvePlayer();
        }

        private void OnEnable()
        {
            NeighborEnvironmentalAwareness.EnvironmentChanged += HandleEnvironmentChanged;
            if (hearing != null)
            {
                hearing.NoiseHeard += HandleNoiseHeard;
            }
        }

        private void OnDisable()
        {
            NeighborEnvironmentalAwareness.EnvironmentChanged -= HandleEnvironmentChanged;
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
            UpdateSuspicion();
            if (IsStunned)
            {
                return;
            }

            if (currentState == BehaviorState.Stunned)
            {
                ChooseNextRoutineGoal();
            }

            UpdatePerception();
            UpdateState();
        }

        public void Stun(float duration)
        {
            RememberInterruptedTask();
            suspicion = Mathf.Max(suspicion, certainThreshold);
            RememberPlayerActivity(transform.position);
            stunnedUntilTime = Mathf.Max(stunnedUntilTime, Time.time + duration);
            currentClimbLink = null;
            motor?.Stop();
            SetState(BehaviorState.Stunned);
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
            if (vision == null)
            {
                return;
            }

            if (Time.time < ignorePlayerSightUntilTime)
            {
                return;
            }

            if (vision.TrySeeTarget(out Transform seenTarget, out Vector3 seenPosition))
            {
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
                case BehaviorState.Investigate:
                    UpdateInvestigate();
                    break;
                case BehaviorState.HuntMode:
                    UpdateHuntMode();
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
            bool canStillChase = player != null && Time.time - lastPlayerSeenTime <= chaseMemoryTime;
            Vector3 chasePosition = player != null && canStillChase ? GetPredictedChasePosition() : lastKnownPlayerPosition;

            if (player != null && canStillChase && ShouldGiveUpChase(chasePosition))
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
                motor.SetDestination(chasePosition);
                nextChaseRepathTime = Time.time + chaseRepathInterval;
            }

            motor.FaceMovementDirection(12f);

            if (player != null && Vector3.Distance(transform.position, player.position) <= catchDistance)
            {
                PlayerController playerController = player.GetComponent<PlayerController>() ?? player.GetComponentInParent<PlayerController>();
                if (PlayerDeathController.Kill(playerController, transform.position))
                {
                    motor.Stop();
                }

                return;
            }

            if (!canStillChase)
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
            currentClimbLink = null;
            currentSearchPoint = null;
            currentHideSpot = null;
            currentHideSpotKnownOccupied = false;
            witnessedPlayerHideSpot = null;
            currentTaskLocation = null;
            visitedSearchPoints.Clear();
            searchedHideSpots.Clear();
            StopActiveTaskAudio();
            motor?.ResetToPosition(startingPosition, startingRotation);
            lastPlayerSeenTime = float.NegativeInfinity;
            ignorePlayerSightUntilTime = Time.time + Mathf.Max(0f, sightGraceTime);
            hasObservedPlayerPosition = false;
            hasPlayerMoveDirectionForCurrentChase = false;
            waitingAtGoal = false;
            suspicion = 0f;
            interruptedTaskLocation = null;
            currentInvestigationSource = null;
            DecayPersistentMemory();
            ChooseNextRoutineGoal();
        }

        private void GiveUpChase(Vector3 chasePosition)
        {
            ignorePlayerSightUntilTime = Time.time + giveUpSightIgnoreTime;
            BeginHuntMode(chasePosition);
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

            if (!GoalWaitComplete())
            {
                FaceSearchSweep();
                return;
            }

            suspicion = Mathf.Max(0f, suspicion - 0.12f);
            RememberFalseAlarm();
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
            if (Time.time >= huntUntilTime)
            {
                EndHuntMode();
                return;
            }

            if (currentSearchPoint == null && currentHideSpot == null)
            {
                if (!TrySetNextHuntDestination())
                {
                    motor.Stop();
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
                PlayerController foundPlayer = searchedSpot.SearchByNeighbor(this, out bool caughtPlayer);
                if (caughtPlayer)
                {
                    hideSpotMemory[searchedSpot] = GetMemory(hideSpotMemory, searchedSpot) + 1f;
                    motor.Stop();
                    return;
                }

                if (foundPlayer != null)
                {
                    player = foundPlayer.transform;
                    hideSpotMemory[searchedSpot] = GetMemory(hideSpotMemory, searchedSpot) + 1f;
                    lastKnownPlayerPosition = foundPlayer.transform.position;
                    lastPlayerSeenTime = Time.time;
                    suspicion = 1f;
                    SetState(BehaviorState.Chase);
                    return;
                }
            }

            currentSearchPoint = null;
            if (!TrySetNextHuntDestination())
            {
                motor.Stop();
            }
        }

        private void UpdateRoutine()
        {
            if (motor == null)
            {
                return;
            }

            motor.SetMoveMode(CurrentSuspicionLevel >= SuspicionLevel.Suspicious
                ? NeighborMotor.MoveMode.Cautious
                : NeighborMotor.MoveMode.Walk);
            if (currentState == BehaviorState.Task
                && (currentTaskLocation == null || !currentTaskLocation.isActiveAndEnabled))
            {
                StopActiveTaskAudio();
                ChooseNextRoutineGoal();
                return;
            }

            if (currentState == BehaviorState.Task && !HasReachedCurrentTask())
            {
                if (waitingAtGoal)
                {
                    waitingAtGoal = false;
                    StopActiveTaskAudio();
                }

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
                else if (CurrentSuspicionLevel >= SuspicionLevel.Curious)
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
        }

        private bool ShouldIgnoreNoiseBecausePlayerIsKnown()
        {
            if (currentState == BehaviorState.Chase || currentState == BehaviorState.HuntMode)
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

            if (interruptedTaskLocation != null
                && interruptedTaskLocation.isActiveAndEnabled
                && Random.value <= resumeInterruptedTaskChance
                && TryStartTask(interruptedTaskLocation))
            {
                interruptedTaskLocation = null;
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

        private bool GoalWaitComplete()
        {
            if (!waitingAtGoal)
            {
                waitingAtGoal = true;
                waitUntilTime = Time.time + goalWaitDuration;
                if (currentState == BehaviorState.Task)
                {
                    BeginCurrentTaskAudio();
                }
            }

            bool waitComplete = Time.time >= waitUntilTime;
            if (waitComplete && currentState == BehaviorState.Task)
            {
                if (currentTaskLocation != null)
                {
                    taskCompletionMemory[currentTaskLocation] = GetMemory(taskCompletionMemory, currentTaskLocation) + 1f;
                    lastTaskCompletionTimes[currentTaskLocation] = Time.time;
                }

                StopActiveTaskAudio(true);
            }

            return waitComplete;
        }

        private bool TryStartForcedNextTask()
        {
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
            if (taskLocation == null || motor == null)
            {
                return false;
            }

            lastTaskLocation = taskLocation;
            currentTaskLocation = taskLocation;
            goalWaitDuration = taskLocation.RandomWaitTime;
            waitingAtGoal = false;

            if (!motor.TrySetDestinationNear(
                    taskLocation.Position,
                    taskLocation.NavigationSampleRadius,
                    out Vector3 sampledTaskPosition))
            {
                currentTaskLocation = null;
                return false;
            }

            Vector3 sampleOffset = sampledTaskPosition - taskLocation.Position;
            sampleOffset.y = 0f;
            if (sampleOffset.sqrMagnitude > taskLocation.ArrivalDistance * taskLocation.ArrivalDistance)
            {
                motor.Stop();
                currentTaskLocation = null;
                return false;
            }

            currentGoal = sampledTaskPosition;
            SetState(BehaviorState.Task);
            return true;
        }

        private bool HasReachedCurrentTask()
        {
            if (motor == null || currentTaskLocation == null || !motor.HasArrived)
            {
                return false;
            }

            Vector3 toTaskLocation = currentTaskLocation.Position - transform.position;
            toTaskLocation.y = 0f;
            return toTaskLocation.sqrMagnitude
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
                if (candidate == null)
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

            return bestLocation != null ? bestLocation : locations[Random.Range(0, locations.Count)];
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
            currentClimbLink = null;
            currentTaskLocation = null;
            currentSearchPoint = null;
            currentHideSpot = null;
            currentHideSpotKnownOccupied = false;
            StopActiveTaskAudio();
            visitedSearchPoints.Clear();
            searchedHideSpots.Clear();
            huntUntilTime = Time.time + huntDuration;
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
            if (motor == null || Time.time >= huntUntilTime)
            {
                return false;
            }

            if (TrySetNextHideSpotSearch())
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
                if (estimatedVisitTime > remainingHuntTime)
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
            visitedSearchPoints.Add(bestPoint);
            currentGoal = bestDestination;
            goalWaitDuration = huntPointWaitTime;
            waitingAtGoal = false;
            motor.SetMoveMode(NeighborMotor.MoveMode.Run);
            if (motor.SetDestination(bestDestination))
            {
                return true;
            }

            visitedSearchPoints.Remove(bestPoint);
            currentSearchPoint = null;
            return false;
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
            suspicion = Mathf.Max(suspicion, suspiciousThreshold);
            ChooseNextRoutineGoal();
        }

        private void SetState(BehaviorState state)
        {
            if (currentState == state)
            {
                return;
            }

            if (currentState == BehaviorState.Task && state != BehaviorState.Task)
            {
                StopActiveTaskAudio();
            }

            currentState = state;
            if (state == BehaviorState.Chase)
            {
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

            RememberInterruptedTask();
            AddSuspicion(changeSuspicion, source);
            RememberPlayerActivity(position);
            currentInvestigationSource = source;

            if (currentState == BehaviorState.Chase || currentState == BehaviorState.HuntMode || motor == null)
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

        private void UpdateSuspicion()
        {
            if (currentState == BehaviorState.Chase)
            {
                suspicion = 1f;
                return;
            }

            float decayMultiplier = currentState == BehaviorState.HuntMode ? 0.2f : 1f;
            suspicion = Mathf.MoveTowards(suspicion, 0f, suspicionDecayPerSecond * decayMultiplier * Time.deltaTime);
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
