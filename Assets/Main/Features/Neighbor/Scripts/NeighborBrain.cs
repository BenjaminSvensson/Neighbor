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
            Search
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

        private BehaviorState currentState;
        private NeighborTaskLocation lastTaskLocation;
        private Vector3 currentGoal;
        private Vector3 lastKnownPlayerPosition;
        private float waitUntilTime;
        private float goalWaitDuration;
        private float lastPlayerSeenTime;
        private float nextChaseRepathTime;
        private bool waitingAtGoal;

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
            UpdatePerception();
            UpdateState();
        }

        private void UpdatePerception()
        {
            if (vision == null)
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
                motor.Stop();
                waitUntilTime = Time.time + 0.35f;
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

        private bool TryHandleVerticalChase(Vector3 chasePosition)
        {
            float verticalDelta = chasePosition.y - transform.position.y;
            if (verticalDelta >= climbCommitVerticalDifference)
            {
                motor.FaceTowards(chasePosition, 18f);
                return motor.TryClimbToward(player);
            }

            if (verticalDelta <= -dropCommitVerticalDifference || motor.IsDetachedFromNavMesh)
            {
                motor.FaceTowards(chasePosition, 18f);
                if (motor.TryJumpDownToward(player))
                {
                    return true;
                }
            }

            return false;
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
            if (stimulus.Loudness01 < minimumInvestigateLoudness || currentState == BehaviorState.Chase)
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
            currentState = state;
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
