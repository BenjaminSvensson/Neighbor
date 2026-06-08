using System.Collections.Generic;
using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Neighbor
{
    public sealed class NeighborBrain : MonoBehaviour
    {
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

        private BehaviorState currentState;
        private readonly HashSet<NeighborSearchPoint> visitedSearchPoints = new();
        private NeighborTaskLocation lastTaskLocation;
        private NeighborSearchPoint currentSearchPoint;
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
        private NeighborTaskLocation currentTaskLocation;
        private NeighborTaskLocation activeTaskAudioLocation;
        private NeighborMotor.MoveMode investigationMoveMode = NeighborMotor.MoveMode.Walk;

        public BehaviorState CurrentState => currentState;
        public Vector3 LastKnownPlayerPosition => lastKnownPlayerPosition;

        private void Awake()
        {
            motor = motor != null ? motor : GetComponent<NeighborMotor>();
            vision = vision != null ? vision : GetComponent<NeighborVision>();
            hearing = hearing != null ? hearing : GetComponent<NeighborHearing>();
            ResolvePlayer();
        }

        private void OnEnable()
        {
            if (hearing != null)
            {
                hearing.NoiseHeard += HandleNoiseHeard;
            }
        }

        private void OnDisable()
        {
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
            stunnedUntilTime = Mathf.Max(stunnedUntilTime, Time.time + duration);
            currentClimbLink = null;
            motor?.Stop();
            SetState(BehaviorState.Stunned);
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
                player = seenTarget;
                if (currentState != BehaviorState.Chase)
                {
                    hasObservedPlayerPosition = false;
                    hasPlayerMoveDirectionForCurrentChase = false;
                }

                UpdateLastSeenPlayerDirection(seenPosition);
                lastKnownPlayerPosition = seenPosition;
                lastPlayerSeenTime = Time.time;
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
            Vector3 chasePosition = player != null && canStillChase ? player.position : lastKnownPlayerPosition;

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
            currentTaskLocation = null;
            visitedSearchPoints.Clear();
            StopActiveTaskAudio();
            motor?.Stop();
            lastPlayerSeenTime = float.NegativeInfinity;
            ignorePlayerSightUntilTime = Time.time + Mathf.Max(0f, sightGraceTime);
            hasObservedPlayerPosition = false;
            hasPlayerMoveDirectionForCurrentChase = false;
            waitingAtGoal = false;
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
                motor.FaceTowards(currentGoal, 5f);
                return;
            }

            ChooseNextRoutineGoal();
        }

        private void UpdateHuntMode()
        {
            if (motor == null)
            {
                return;
            }

            motor.SetMoveMode(NeighborMotor.MoveMode.Run);
            if (Time.time >= huntUntilTime)
            {
                EndHuntMode();
                return;
            }

            if (currentSearchPoint == null)
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
                motor.FaceTowards(currentGoal, 5f);
                return;
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

            motor.SetMoveMode(NeighborMotor.MoveMode.Walk);
            if (!motor.HasArrived)
            {
                return;
            }

            if (!GoalWaitComplete())
            {
                if (currentState == BehaviorState.Task && currentTaskLocation != null)
                {
                    motor.FaceTowards(transform.position + currentTaskLocation.LookDirection, 5f);
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

            goalWaitDuration = investigationWaitTime;
            waitingAtGoal = false;
            currentTaskLocation = null;
            StopActiveTaskAudio();
            investigationMoveMode = stimulus.Urgency01 >= minimumUrgencyToRunToNoise
                ? NeighborMotor.MoveMode.Run
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

            bool shouldWander = Random.value < wanderChance || NeighborTaskLocation.Locations.Count == 0;
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
            currentGoal = taskLocation.Position;
            goalWaitDuration = taskLocation.RandomWaitTime;
            waitingAtGoal = false;

            if (!motor.SetDestination(currentGoal))
            {
                currentTaskLocation = null;
                return false;
            }

            SetState(BehaviorState.Task);
            return true;
        }

        private NeighborTaskLocation GetRandomTaskLocation()
        {
            IReadOnlyList<NeighborTaskLocation> locations = NeighborTaskLocation.Locations;
            if (locations.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < 8; i++)
            {
                NeighborTaskLocation candidate = locations[Random.Range(0, locations.Count)];
                if (candidate != lastTaskLocation || candidate.CanRepeatImmediately || locations.Count == 1)
                {
                    return candidate;
                }
            }

            return locations[Random.Range(0, locations.Count)];
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
            lastKnownPlayerPosition = lostPlayerPosition;
            currentClimbLink = null;
            currentTaskLocation = null;
            currentSearchPoint = null;
            StopActiveTaskAudio();
            visitedSearchPoints.Clear();
            huntUntilTime = Time.time + huntDuration;

            Vector3 fallbackDirection = lostPlayerPosition - transform.position;
            fallbackDirection.y = 0f;
            if ((!hasPlayerMoveDirectionForCurrentChase || lastSeenPlayerMoveDirection.sqrMagnitude <= 0.001f)
                && fallbackDirection.sqrMagnitude > 0.001f)
            {
                lastSeenPlayerMoveDirection = fallbackDirection.normalized;
            }

            SetState(BehaviorState.HuntMode);
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

        private void EndHuntMode()
        {
            currentSearchPoint = null;
            visitedSearchPoints.Clear();
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
