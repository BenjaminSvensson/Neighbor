using System.Collections.Generic;
using Neighbor.Main.Features.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            Search,
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
        [SerializeField, Min(0f)] private float noiseDestinationSampleRadius = 4f;
        [SerializeField, Min(0f)] private float investigationWaitTime = 2.2f;
        [SerializeField, Min(0f)] private float searchDuration = 4f;

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
        private NeighborTaskLocation lastTaskLocation;
        private NeighborClimbLink currentClimbLink;
        private Vector3 currentGoal;
        private Vector3 lastKnownPlayerPosition;
        private float waitUntilTime;
        private float goalWaitDuration;
        private float lastPlayerSeenTime = float.NegativeInfinity;
        private float nextChaseRepathTime;
        private float nextClimbLinkSearchTime;
        private float stunnedUntilTime;
        private float bestChaseDistance;
        private float lastChaseProgressTime;
        private float ignorePlayerSightUntilTime;
        private bool isResettingScene;
        private bool waitingAtGoal;
        private NeighborTaskLocation currentTaskLocation;
        private NeighborTaskLocation activeTaskAudioLocation;

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
                SetState(BehaviorState.Search);
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
                case BehaviorState.Search:
                    UpdateSearch();
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
                ResetCurrentScene();
                return;
            }

            if (!canStillChase)
            {
                currentGoal = lastKnownPlayerPosition;
                motor.SetDestination(currentGoal);
                goalWaitDuration = searchDuration;
                waitingAtGoal = false;
                SetState(BehaviorState.Search);
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

        private void ResetCurrentScene()
        {
            if (isResettingScene)
            {
                return;
            }

            isResettingScene = true;
            motor?.Stop();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void GiveUpChase(Vector3 chasePosition)
        {
            lastKnownPlayerPosition = chasePosition;
            currentGoal = chasePosition;
            goalWaitDuration = searchDuration;
            waitingAtGoal = false;
            currentClimbLink = null;
            currentTaskLocation = null;
            StopActiveTaskAudio();
            ignorePlayerSightUntilTime = Time.time + giveUpSightIgnoreTime;

            if (motor != null && !motor.SetDestination(currentGoal))
            {
                motor.Stop();
            }

            SetState(BehaviorState.Search);
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

            motor.SetMoveMode(NeighborMotor.MoveMode.Walk);
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

        private void UpdateSearch()
        {
            if (motor == null)
            {
                return;
            }

            motor.SetMoveMode(NeighborMotor.MoveMode.Run);
            if (!motor.HasArrived)
            {
                return;
            }

            if (!GoalWaitComplete())
            {
                motor.FaceTowards(lastKnownPlayerPosition, 5f);
                return;
            }

            ChooseNextRoutineGoal();
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
            motor?.SetMoveMode(NeighborMotor.MoveMode.Run);
            if (motor != null && motor.TrySetDestinationNear(stimulus.Position, noiseDestinationSampleRadius, out Vector3 investigatePosition))
            {
                currentGoal = investigatePosition;
                SetState(BehaviorState.Investigate);
            }
        }

        private bool ShouldIgnoreNoiseBecausePlayerIsKnown()
        {
            if (currentState == BehaviorState.Chase || currentState == BehaviorState.Search)
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
