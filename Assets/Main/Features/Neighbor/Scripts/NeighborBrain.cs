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
        [SerializeField, Min(0f)] private float investigationWaitTime = 2.2f;
        [SerializeField, Min(0f)] private float searchDuration = 4f;

        [Header("Chase")]
        [SerializeField, Min(0f)] private float chaseMemoryTime = 3.5f;
        [SerializeField, Min(0f)] private float chaseRepathInterval = 0.12f;
        [SerializeField, Min(0f)] private float catchDistance = 1.15f;
        [SerializeField, Min(0f)] private float offMeshDirectChaseSpeed = 4.5f;
        [SerializeField, Min(0f)] private float climbCommitVerticalDifference = 0.45f;
        [SerializeField, Min(0f)] private float dropCommitVerticalDifference = 0.55f;
        [SerializeField, Min(0f)] private float heightAssistRepathInterval = 0.55f;
        [SerializeField, Min(0f)] private float heightAssistArrivalDistance = 0.8f;
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
        private float lastPlayerSeenTime;
        private float nextChaseRepathTime;
        private float nextHeightAssistSearchTime;
        private float nextClimbLinkSearchTime;
        private Vector3 heightAssistGoal;
        private float stunnedUntilTime;
        private float bestChaseDistance;
        private float lastChaseProgressTime;
        private float ignorePlayerSightUntilTime;
        private bool isResettingScene;
        private bool waitingAtGoal;
        private bool hasHeightAssistGoal;

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
            hasHeightAssistGoal = false;
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

            motor.FaceTowards(chasePosition, 12f);

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
            hasHeightAssistGoal = false;
            currentClimbLink = null;
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
                motor.FaceTowards(chasePosition, 18f);
                if (motor.TryClimbToward(player))
                {
                    hasHeightAssistGoal = false;
                    currentClimbLink = null;
                    return true;
                }

                if (TryUseClimbLink(chasePosition))
                {
                    return true;
                }

                return TryUseHeightAssist(chasePosition);
            }

            if (verticalDelta <= -dropCommitVerticalDifference || motor.IsDetachedFromNavMesh)
            {
                motor.FaceTowards(chasePosition, 18f);
                if (motor.TryJumpDownToward(player))
                {
                    hasHeightAssistGoal = false;
                    currentClimbLink = null;
                    return true;
                }
            }

            hasHeightAssistGoal = false;
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
                    hasHeightAssistGoal = false;
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

        private bool TryUseHeightAssist(Vector3 chasePosition)
        {
            if (hasHeightAssistGoal && Vector3.Distance(transform.position, heightAssistGoal) <= heightAssistArrivalDistance)
            {
                hasHeightAssistGoal = false;
                return false;
            }

            if (!hasHeightAssistGoal || Time.time >= nextHeightAssistSearchTime)
            {
                nextHeightAssistSearchTime = Time.time + heightAssistRepathInterval;
                if (motor.TryFindHeightAssistPoint(chasePosition, out Vector3 assistPoint))
                {
                    heightAssistGoal = assistPoint;
                    hasHeightAssistGoal = true;
                    motor.SetDestination(heightAssistGoal);
                }
            }

            if (!hasHeightAssistGoal)
            {
                return false;
            }

            motor.SetDestination(heightAssistGoal);
            motor.FaceTowards(heightAssistGoal, 12f);
            return true;
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

            currentGoal = stimulus.Position;
            goalWaitDuration = investigationWaitTime;
            waitingAtGoal = false;
            motor?.SetMoveMode(NeighborMotor.MoveMode.Run);
            if (motor != null && motor.SetDestination(currentGoal))
            {
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
                SetState(BehaviorState.Idle);
                return;
            }

            bool shouldWander = Random.value < wanderChance || NeighborTaskLocation.Locations.Count == 0;
            if (shouldWander && motor.TryGetRandomReachablePoint(transform.position, wanderRadius, out Vector3 wanderPoint))
            {
                currentGoal = wanderPoint;
                goalWaitDuration = Random.Range(idleWaitMinimum, Mathf.Max(idleWaitMinimum, idleWaitMaximum));
                waitingAtGoal = false;
                if (motor.SetDestination(currentGoal))
                {
                    SetState(BehaviorState.Wander);
                    return;
                }
            }

            NeighborTaskLocation taskLocation = GetRandomTaskLocation();
            if (taskLocation == null)
            {
                SetState(BehaviorState.Idle);
                goalWaitDuration = Random.Range(idleWaitMinimum, Mathf.Max(idleWaitMinimum, idleWaitMaximum));
                waitingAtGoal = false;
                return;
            }

            lastTaskLocation = taskLocation;
            currentGoal = taskLocation.Position;
            goalWaitDuration = taskLocation.RandomWaitTime;
            waitingAtGoal = false;
            if (motor.SetDestination(currentGoal))
            {
                SetState(BehaviorState.Task);
                return;
            }

            SetState(BehaviorState.Idle);
        }

        private bool GoalWaitComplete()
        {
            if (!waitingAtGoal)
            {
                waitingAtGoal = true;
                waitUntilTime = Time.time + goalWaitDuration;
            }

            return Time.time >= waitUntilTime;
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

            currentState = state;
            if (state == BehaviorState.Chase)
            {
                bestChaseDistance = player != null
                    ? Vector3.Distance(transform.position, player.position)
                    : float.PositiveInfinity;
                lastChaseProgressTime = Time.time;
            }
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
