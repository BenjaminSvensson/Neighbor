using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Neighbor.Main.Features.Neighbor
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NeighborMotor : MonoBehaviour
    {
        public enum MoveMode
        {
            Walk,
            Cautious,
            Run
        }

        public enum TraversalAnimationPhase
        {
            None,
            Start,
            Loop,
            Landing,
            Climb
        }

        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 2.4f;
        [SerializeField, Min(0f)] private float cautiousSpeed = 1.65f;
        [SerializeField, Min(0f)] private float runSpeed = 5.8f;
        [SerializeField, Min(0f)] private float acceleration = 18f;
        [SerializeField, Min(0f)] private float angularSpeed = 540f;
        [SerializeField, Min(0f)] private float stoppingDistance = 0.45f;
        [SerializeField, Min(0f)] private float destinationSampleRadius = 1.5f;
        [SerializeField, Min(0f)] private float startNavMeshSnapRadius = 3f;

        [Header("Dynamic Obstacle Avoidance")]
        [SerializeField] private bool enableDynamicObstacleAvoidance = true;
        [SerializeField] private LayerMask dynamicObstacleMask = ~0;
        [SerializeField, Min(0.05f)] private float dynamicObstacleProbeRadius = 0.35f;
        [SerializeField, Min(0f)] private float dynamicObstacleProbeHeight = 0.55f;
        [SerializeField, Min(0.1f)] private float dynamicObstacleProbeDistance = 1.2f;
        [SerializeField, Min(0f)] private float dynamicObstacleSidePadding = 0.35f;
        [SerializeField, Min(0f)] private float dynamicObstacleSampleRadius = 0.8f;
        [SerializeField, Min(0.05f)] private float dynamicObstacleProbeInterval = 0.15f;
        [SerializeField, Min(0.1f)] private float dynamicObstacleDetourTimeout = 1.25f;

        [Header("Blocked Destination")]
        [SerializeField, Min(1)] private int maximumNoProgressAttempts = 3;
        [SerializeField, Min(0.25f)] private float noProgressCheckInterval = 1.5f;
        [SerializeField, Min(0.01f)] private float minimumProgressPerAttempt = 0.3f;
        [SerializeField, Min(0f)] private float destinationChangeResetDistance = 0.75f;
        [SerializeField, Min(1)] private int recoveryStartAttempt = 2;
        [SerializeField, Min(1)] private int maximumRecoveryAttempts = 2;
        [SerializeField, Min(0.1f)] private float recoverySearchRadius = 2.5f;
        [SerializeField, Min(0.1f)] private float recoveryNavMeshSampleRadius = 3.5f;
        [SerializeField, Min(0.05f)] private float minimumRecoveryRelocationDistance = 0.65f;
        [SerializeField, Range(4, 32)] private int recoveryCandidateCount = 16;

        [Header("Jump And Climb")]
        [SerializeField] private bool traverseOffMeshLinks = true;
        [SerializeField, Min(0.01f)] private float offMeshTraverseDuration = 0.34f;
        [SerializeField, Min(0f)] private float offMeshJumpHeight = 0.75f;
        [SerializeField, Range(0f, 0.8f)] private float traversalStartPortion = 0.24f;
        [SerializeField, Min(0f)] private float traversalLandingReactionDuration = 0.28f;
        [SerializeField] private bool enableLedgeClimb = true;
        [SerializeField, Min(0.1f)] private float ledgeCheckDistance = 0.75f;
        [SerializeField, Min(0f)] private float ledgeMinimumHeight = 0.45f;
        [SerializeField, Min(0f)] private float ledgeMaximumHeight = 1.25f;
        [SerializeField, Min(0f)] private float ledgeTopProbeForwardOffset = 0.35f;
        [SerializeField, Min(0.01f)] private float ledgeClimbDuration = 0.3f;
        [SerializeField] private LayerMask climbMask = ~0;

        [Header("Chase Climb Assist")]
        [SerializeField] private bool enableTargetClimbAssist = true;
        [SerializeField, Min(0f)] private float targetClimbHorizontalReach = 2.4f;
        [SerializeField, Min(0f)] private float targetClimbVerticalPadding = 0.25f;
        [SerializeField, Min(0f)] private float targetClimbForwardOffset = 0.25f;
        [SerializeField, Min(0f)] private float postClimbOffMeshChaseTime = 1.25f;
        [SerializeField, Min(0.01f)] private float chaseClimbDuration = 0.24f;
        [SerializeField, Min(0f)] private float chaseClimbArcHeight = 0.45f;

        [Header("Chase Drop Assist")]
        [SerializeField] private bool enableTargetDropAssist = true;
        [SerializeField, Min(0f)] private float targetDropHorizontalReach = 3.2f;
        [SerializeField, Min(0f)] private float targetDropMinimumHeight = 0.55f;
        [SerializeField, Min(0f)] private float targetDropMaximumHeight = 4f;
        [SerializeField, Min(0.01f)] private float chaseDropDuration = 0.26f;
        [SerializeField, Min(0f)] private float chaseDropArcHeight = 0.35f;
        [SerializeField, Min(0f)] private float dropLandingSampleRadius = 2.5f;

        private NavMeshAgent agent;
        private Coroutine traversalRoutine;
        private Coroutine knockbackRoutine;
        private Vector3 requestedDestination;
        private bool hasRequestedDestination;
        private float offMeshChaseUntilTime;
        private readonly RaycastHit[] climbProbeHits = new RaycastHit[10];
        private readonly RaycastHit[] dynamicObstacleHits = new RaycastHit[12];
        private readonly Collider[] recoveryOverlapHits = new Collider[16];
        private NavMeshPath dynamicObstaclePath;
        private NavMeshPath recoveryPath;
        private bool isAvoidingDynamicObstacle;
        private bool isPaused;
        private Vector3 dynamicObstacleDetour;
        private float dynamicObstacleDetourUntilTime;
        private float nextDynamicObstacleProbeTime;
        private float nextProgressCheckTime;
        private Vector3 progressCheckPosition;
        private bool hasProgressCheckPosition;
        private int noProgressAttemptCount;
        private int recoveryAttemptCount;
        private Vector3 lastRecoveryPosition;
        private float lastRecoveryTime = float.NegativeInfinity;
        private TraversalAnimationPhase traversalAnimationPhase;

        public bool IsTraversingSpecialMove => traversalRoutine != null;
        public TraversalAnimationPhase CurrentTraversalAnimationPhase => traversalAnimationPhase;
        public bool IsOffMeshChasing => Time.time < offMeshChaseUntilTime;
        public bool IsDetachedFromNavMesh => agent != null && (!agent.updatePosition || !agent.isOnNavMesh);
        public bool IsPaused => isPaused;
        public bool IsAvoidingDynamicObstacle => isAvoidingDynamicObstacle;
        public Vector3 DynamicObstacleDetour => dynamicObstacleDetour;
        public Vector3 RequestedDestination => requestedDestination;
        public float CurrentSpeed => agent != null ? agent.velocity.magnitude : 0f;
        public bool HasPath => agent != null && (agent.hasPath || agent.pathPending);
        public float ConfiguredSpeed => agent != null ? agent.speed : walkSpeed;
        public float RemainingDistance => agent == null || agent.pathPending ? float.PositiveInfinity : agent.remainingDistance;
        public int NoProgressAttemptCount => noProgressAttemptCount;
        public int MaximumNoProgressAttempts => Mathf.Max(maximumNoProgressAttempts, recoveryStartAttempt + maximumRecoveryAttempts);
        public Vector3 LastRecoveryPosition => lastRecoveryPosition;
        public float LastRecoveryAge => Time.time - lastRecoveryTime;
        public event System.Action<Vector3> DestinationAbandoned;
        public bool HasArrived => agent != null
            && !isAvoidingDynamicObstacle
            && !agent.pathPending
            && agent.remainingDistance <= agent.stoppingDistance + 0.1f
            && (!agent.hasPath || agent.velocity.sqrMagnitude < 0.08f);

        private void Awake()
        {
            dynamicObstaclePath = new NavMeshPath();
            recoveryPath = new NavMeshPath();
            agent = GetComponent<NavMeshAgent>();
            ConfigureAgent();
            SnapToNavMeshIfNeeded();
        }

        private void Update()
        {
            if (agent == null)
            {
                return;
            }

            if (!IsOffMeshChasing && !agent.updatePosition && traversalRoutine == null)
            {
                RecoverDetachedAgent();
            }

            if (traverseOffMeshLinks && agent.isOnOffMeshLink && traversalRoutine == null)
            {
                isAvoidingDynamicObstacle = false;
                traversalRoutine = StartCoroutine(TraverseOffMeshLink());
                return;
            }

            UpdateDynamicObstacleAvoidance();
            UpdateDestinationProgress();

            if (enableLedgeClimb && traversalRoutine == null && !isAvoidingDynamicObstacle && hasRequestedDestination)
            {
                TryStartLedgeClimb();
            }
        }

        public void SetMoveMode(MoveMode mode)
        {
            if (agent == null)
            {
                return;
            }

            if (IsOffMeshChasing)
            {
                return;
            }

            agent.speed = GetMoveSpeed(mode);
        }

        public float GetMoveSpeed(MoveMode mode)
        {
            return mode switch
            {
                MoveMode.Run => runSpeed,
                MoveMode.Cautious => cautiousSpeed,
                _ => walkSpeed
            };
        }

        public bool SetDestination(Vector3 destination)
        {
            return TrySetDestination(destination, destinationSampleRadius, out _);
        }

        public bool TrySetDestinationNear(Vector3 destination, float sampleRadius, out Vector3 sampledDestination)
        {
            return TrySetDestination(destination, Mathf.Max(destinationSampleRadius, sampleRadius), out sampledDestination);
        }

        private bool TrySetDestination(Vector3 destination, float sampleRadius, out Vector3 sampledDestination)
        {
            if (IsOffMeshChasing)
            {
                sampledDestination = destination;
                return false;
            }

            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                sampledDestination = destination;
                return false;
            }

            if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, Mathf.Max(0f, sampleRadius), agent.areaMask))
            {
                sampledDestination = destination;
                return false;
            }

            bool destinationChanged = !hasRequestedDestination
                || Vector3.Distance(requestedDestination, hit.position) > destinationChangeResetDistance;
            requestedDestination = hit.position;
            hasRequestedDestination = true;
            Vector3 movementSinceProgressCheck = transform.position - progressCheckPosition;
            movementSinceProgressCheck.y = 0f;
            if (destinationChanged
                && (!hasProgressCheckPosition
                    || movementSinceProgressCheck.sqrMagnitude >= minimumProgressPerAttempt * minimumProgressPerAttempt))
            {
                ResetDestinationProgressTracking();
            }

            sampledDestination = hit.position;
            agent.isStopped = isPaused;
            if (isAvoidingDynamicObstacle)
            {
                return true;
            }

            return agent.SetDestination(hit.position);
        }

        public bool CanReach(Vector3 destination, out float pathDistance, out Vector3 sampledPosition)
        {
            pathDistance = float.PositiveInfinity;
            sampledPosition = destination;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                return false;
            }

            if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, destinationSampleRadius, agent.areaMask))
            {
                return false;
            }

            NavMeshPath path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }

            sampledPosition = hit.position;
            pathDistance = GetPathDistance(path);
            return true;
        }

        public bool TryRepathAvoidingDoorway(
            Vector3 doorwayPosition,
            Vector3 doorwayPlaneNormal,
            float doorwayAvoidanceRadius,
            out float pathDistance,
            out float directDistance)
        {
            pathDistance = float.PositiveInfinity;
            directDistance = float.PositiveInfinity;
            if (!hasRequestedDestination
                || agent == null
                || !agent.enabled
                || !agent.isOnNavMesh
                || isAvoidingDynamicObstacle)
            {
                return false;
            }

            NavMeshPath path = new NavMeshPath();
            if (!agent.CalculatePath(requestedDestination, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }

            if (PathCrossesDoorway(path, doorwayPosition, doorwayPlaneNormal, doorwayAvoidanceRadius))
            {
                return false;
            }

            pathDistance = GetPathDistance(path);
            directDistance = Vector3.Distance(transform.position, requestedDestination);
            agent.isStopped = false;
            return agent.SetPath(path);
        }

        private static bool PathCrossesDoorway(
            NavMeshPath path,
            Vector3 doorwayPosition,
            Vector3 doorwayPlaneNormal,
            float doorwayAvoidanceRadius)
        {
            if (path == null || path.corners == null || path.corners.Length < 2)
            {
                return false;
            }

            Vector3 planeNormal = Vector3.ProjectOnPlane(doorwayPlaneNormal, Vector3.up).normalized;
            if (planeNormal.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            float avoidanceRadius = Mathf.Max(0.1f, doorwayAvoidanceRadius);
            for (int i = 1; i < path.corners.Length; i++)
            {
                Vector3 from = path.corners[i - 1];
                Vector3 to = path.corners[i];
                float fromSide = Vector3.Dot(from - doorwayPosition, planeNormal);
                float toSide = Vector3.Dot(to - doorwayPosition, planeNormal);
                if (Mathf.Sign(fromSide) == Mathf.Sign(toSide) && !Mathf.Approximately(fromSide, 0f))
                {
                    continue;
                }

                float denominator = fromSide - toSide;
                float segmentT = Mathf.Abs(denominator) > 0.0001f ? Mathf.Clamp01(fromSide / denominator) : 0f;
                Vector3 crossingPoint = Vector3.Lerp(from, to, segmentT);
                Vector3 flatOffset = Vector3.ProjectOnPlane(crossingPoint - doorwayPosition, Vector3.up);
                if (flatOffset.sqrMagnitude <= avoidanceRadius * avoidanceRadius)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryClimbToward(Transform target)
        {
            if (!enableTargetClimbAssist || target == null || traversalRoutine != null)
            {
                return false;
            }

            Vector3 toTarget = target.position - transform.position;
            Vector3 flatToTarget = new Vector3(toTarget.x, 0f, toTarget.z);
            float horizontalDistance = flatToTarget.magnitude;
            if (horizontalDistance > targetClimbHorizontalReach || horizontalDistance <= 0.05f)
            {
                return false;
            }

            float verticalDelta = target.position.y - transform.position.y;
            if (verticalDelta < ledgeMinimumHeight || verticalDelta > ledgeMaximumHeight + targetClimbVerticalPadding)
            {
                return false;
            }

            Vector3 approachDirection = flatToTarget / horizontalDistance;
            if (!TryFindTopBelowTarget(target, approachDirection, out Vector3 climbTarget))
            {
                return false;
            }

            isAvoidingDynamicObstacle = false;
            traversalRoutine = StartCoroutine(ClimbLedge(climbTarget, true, chaseClimbDuration, chaseClimbArcHeight));
            return true;
        }

        public bool TryJumpDownToward(Transform target)
        {
            if (!enableTargetDropAssist || target == null || traversalRoutine != null)
            {
                return false;
            }

            Vector3 toTarget = target.position - transform.position;
            Vector3 flatToTarget = new Vector3(toTarget.x, 0f, toTarget.z);
            float horizontalDistance = flatToTarget.magnitude;
            float dropHeight = transform.position.y - target.position.y;
            if (horizontalDistance > targetDropHorizontalReach || dropHeight < targetDropMinimumHeight || dropHeight > targetDropMaximumHeight)
            {
                return false;
            }

            if (!TryFindDropLanding(target.position, out Vector3 landingPoint))
            {
                return false;
            }

            isAvoidingDynamicObstacle = false;
            traversalRoutine = StartCoroutine(JumpToPoint(landingPoint, chaseDropDuration, chaseDropArcHeight));
            return true;
        }

        public bool TryUseClimbLink(NeighborClimbLink climbLink)
        {
            if (climbLink == null || traversalRoutine != null)
            {
                return false;
            }

            isAvoidingDynamicObstacle = false;
            traversalRoutine = StartCoroutine(ClimbLedge(
                climbLink.TopPosition,
                true,
                climbLink.ClimbDuration,
                climbLink.JumpArcHeight));
            return true;
        }

        public void ApplyKnockback(Vector3 direction, float distance, float duration)
        {
            if (direction.sqrMagnitude <= 0.001f || distance <= 0f)
            {
                return;
            }

            if (knockbackRoutine != null)
            {
                StopCoroutine(knockbackRoutine);
            }

            if (traversalRoutine != null)
            {
                StopCoroutine(traversalRoutine);
                traversalRoutine = null;
                traversalAnimationPhase = TraversalAnimationPhase.None;
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                }
            }

            isAvoidingDynamicObstacle = false;
            knockbackRoutine = StartCoroutine(Knockback(direction.normalized, distance, duration));
        }

        public void MoveDirectlyToward(Vector3 target, float speed, float turnSharpness)
        {
            isAvoidingDynamicObstacle = false;
            FaceTowards(target, turnSharpness);
            Vector3 toTarget = target - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.01f)
            {
                return;
            }

            transform.position += toTarget.normalized * (speed * Time.deltaTime);
        }

        public void Stop()
        {
            hasRequestedDestination = false;
            isAvoidingDynamicObstacle = false;
            ResetDestinationProgressTracking();
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                return;
            }

            agent.isStopped = false;
            agent.ResetPath();
        }

        public void SetPaused(bool paused)
        {
            if (isPaused != paused)
            {
                ResetDestinationProgressTracking();
            }

            isPaused = paused;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                return;
            }

            agent.isStopped = paused;
        }

        public void ResetToPosition(Vector3 position, Quaternion rotation)
        {
            if (traversalRoutine != null)
            {
                StopCoroutine(traversalRoutine);
                traversalRoutine = null;
                traversalAnimationPhase = TraversalAnimationPhase.None;
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                }
            }

            if (knockbackRoutine != null)
            {
                StopCoroutine(knockbackRoutine);
                knockbackRoutine = null;
            }

            hasRequestedDestination = false;
            isAvoidingDynamicObstacle = false;
            isPaused = false;
            ResetDestinationProgressTracking();
            offMeshChaseUntilTime = 0f;

            if (agent != null && agent.enabled)
            {
                agent.updatePosition = true;
                agent.updateRotation = true;
                agent.isStopped = false;

                if (NavMesh.SamplePosition(position, out NavMeshHit hit, startNavMeshSnapRadius, agent.areaMask))
                {
                    agent.Warp(hit.position);
                }
                else if (agent.isOnNavMesh)
                {
                    agent.Warp(position);
                }
                else
                {
                    transform.position = position;
                    agent.nextPosition = position;
                }

                if (agent.isOnNavMesh)
                {
                    agent.ResetPath();
                }
            }
            else
            {
                transform.position = position;
            }

            transform.rotation = rotation;
        }

        public void FaceTowards(Vector3 position, float turnSharpness)
        {
            Vector3 toPosition = position - transform.position;
            toPosition.y = 0f;
            if (toPosition.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(toPosition.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-turnSharpness * Time.deltaTime));
        }

        public void FaceMovementDirection(float turnSharpness)
        {
            if (agent == null)
            {
                return;
            }

            Vector3 direction = agent.desiredVelocity;
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = agent.velocity;
                direction.y = 0f;
            }

            if (direction.sqrMagnitude <= 0.001f && agent.hasPath && !agent.pathPending)
            {
                direction = agent.steeringTarget - transform.position;
                direction.y = 0f;
            }

            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-turnSharpness * Time.deltaTime));
        }

        public bool TryGetRandomReachablePoint(Vector3 origin, float radius, out Vector3 point)
        {
            for (int i = 0; i < 12; i++)
            {
                Vector2 randomCircle = Random.insideUnitCircle * radius;
                Vector3 candidate = origin + new Vector3(randomCircle.x, 0f, randomCircle.y);
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, destinationSampleRadius, agent.areaMask))
                {
                    point = hit.position;
                    return true;
                }
            }

            point = origin;
            return false;
        }

        private static float GetPathDistance(NavMeshPath path)
        {
            if (path == null || path.corners == null || path.corners.Length < 2)
            {
                return 0f;
            }

            float distance = 0f;
            for (int i = 1; i < path.corners.Length; i++)
            {
                distance += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            }

            return distance;
        }

        private void ConfigureAgent()
        {
            agent.speed = walkSpeed;
            agent.acceleration = acceleration;
            agent.angularSpeed = angularSpeed;
            agent.stoppingDistance = stoppingDistance;
            agent.autoBraking = true;
            agent.autoRepath = true;
            agent.autoTraverseOffMeshLink = false;
        }

        private void UpdateDynamicObstacleAvoidance()
        {
            if (!enableDynamicObstacleAvoidance)
            {
                if (isAvoidingDynamicObstacle)
                {
                    ResumeRequestedDestination();
                }

                return;
            }

            if (traversalRoutine != null
                || knockbackRoutine != null
                || IsOffMeshChasing
                || agent == null
                || !agent.enabled
                || !agent.isOnNavMesh)
            {
                return;
            }

            if (isAvoidingDynamicObstacle)
            {
                Vector3 toDetour = dynamicObstacleDetour - transform.position;
                toDetour.y = 0f;
                float arrivalDistance = agent.stoppingDistance + 0.15f;
                if (Time.time >= dynamicObstacleDetourUntilTime
                    || toDetour.sqrMagnitude <= arrivalDistance * arrivalDistance)
                {
                    ResumeRequestedDestination();
                }

                return;
            }

            if (!hasRequestedDestination
                || Time.time < nextDynamicObstacleProbeTime
                || agent.pathPending
                || !agent.hasPath)
            {
                return;
            }

            nextDynamicObstacleProbeTime = Time.time + dynamicObstacleProbeInterval;
            if (!TryFindDynamicObstacleDetour(out Vector3 detour))
            {
                return;
            }

            dynamicObstacleDetour = detour;
            dynamicObstacleDetourUntilTime = Time.time + dynamicObstacleDetourTimeout;
            isAvoidingDynamicObstacle = agent.SetDestination(detour);
        }

        private void UpdateDestinationProgress()
        {
            if (!hasRequestedDestination
                || isPaused
                || traversalRoutine != null
                || knockbackRoutine != null
                || IsOffMeshChasing
                || agent == null
                || !agent.enabled
                || !agent.isOnNavMesh
                || agent.pathPending
                || HasArrived)
            {
                return;
            }

            if (!hasProgressCheckPosition)
            {
                progressCheckPosition = transform.position;
                hasProgressCheckPosition = true;
                nextProgressCheckTime = Time.time + noProgressCheckInterval;
                return;
            }

            if (Time.time < nextProgressCheckTime)
            {
                return;
            }

            Vector3 movement = transform.position - progressCheckPosition;
            movement.y = 0f;
            progressCheckPosition = transform.position;
            nextProgressCheckTime = Time.time + noProgressCheckInterval;
            if (movement.sqrMagnitude >= minimumProgressPerAttempt * minimumProgressPerAttempt)
            {
                noProgressAttemptCount = 0;
                recoveryAttemptCount = 0;
                return;
            }

            noProgressAttemptCount++;
            if (noProgressAttemptCount >= recoveryStartAttempt
                && recoveryAttemptCount < maximumRecoveryAttempts
                && TryRecoverToNearbyNavMesh())
            {
                recoveryAttemptCount++;
                return;
            }

            if (noProgressAttemptCount < MaximumNoProgressAttempts)
            {
                isAvoidingDynamicObstacle = false;
                agent.SetDestination(requestedDestination);
                return;
            }

            Vector3 abandonedDestination = requestedDestination;
            Stop();
            DestinationAbandoned?.Invoke(abandonedDestination);
        }

        private bool TryRecoverToNearbyNavMesh()
        {
            if (agent == null || !agent.enabled || !hasRequestedDestination)
            {
                return false;
            }

            Vector3 origin = transform.position;
            Vector3 toDestination = requestedDestination - origin;
            toDestination.y = 0f;
            Vector3 forward = toDestination.sqrMagnitude > 0.01f ? toDestination.normalized : transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;

            Vector3 bestPoint = default;
            float bestScore = float.PositiveInfinity;
            bool found = false;
            int candidateCount = Mathf.Max(4, recoveryCandidateCount);
            for (int i = 0; i < candidateCount; i++)
            {
                float angle = i * (360f / candidateCount);
                float radius = Mathf.Lerp(
                    minimumRecoveryRelocationDistance,
                    recoverySearchRadius,
                    (i % 4 + 1f) / 4f);
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
                Vector3 candidate = origin + direction * radius;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, recoveryNavMeshSampleRadius, agent.areaMask)
                    || Vector3.Distance(origin, hit.position) < minimumRecoveryRelocationDistance
                    || !IsRecoveryPositionClear(hit.position)
                    || !NavMesh.CalculatePath(hit.position, requestedDestination, agent.areaMask, recoveryPath)
                    || recoveryPath.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                float score = GetPathDistance(recoveryPath)
                    + Vector3.Distance(origin, hit.position) * 0.25f
                    + Mathf.Max(0f, hit.position.y - origin.y) * 1.5f;
                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestPoint = hit.position;
                found = true;
            }

            if (!found)
            {
                return false;
            }

            CancelActiveTraversalForRecovery();
            agent.updatePosition = true;
            agent.updateRotation = true;
            agent.isStopped = false;
            if (agent.isOnNavMesh)
            {
                agent.ResetPath();
            }

            if (!agent.Warp(bestPoint))
            {
                return false;
            }

            lastRecoveryPosition = bestPoint;
            lastRecoveryTime = Time.time;
            isAvoidingDynamicObstacle = false;
            hasProgressCheckPosition = false;
            nextProgressCheckTime = Time.time + noProgressCheckInterval;
            return agent.SetDestination(requestedDestination);
        }

        private bool IsRecoveryPositionClear(Vector3 position)
        {
            float radius = Mathf.Max(0.05f, agent.radius * 0.9f);
            float height = Mathf.Max(radius * 2f, agent.height);
            Vector3 bottom = position + Vector3.up * (radius + 0.08f);
            Vector3 top = position + Vector3.up * (height - radius);
            int hitCount = Physics.OverlapCapsuleNonAlloc(
                bottom,
                top,
                radius,
                recoveryOverlapHits,
                ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = recoveryOverlapHits[i];
                if (hit != null && !hit.transform.IsChildOf(transform) && !transform.IsChildOf(hit.transform))
                {
                    return false;
                }
            }

            return true;
        }

        private void RecoverDetachedAgent()
        {
            if (agent == null || !agent.enabled)
            {
                return;
            }

            if (agent.isOnNavMesh)
            {
                Vector3 recoveredPosition = agent.nextPosition;
                transform.position = recoveredPosition;
                agent.updatePosition = true;
                agent.updateRotation = true;
                lastRecoveryPosition = recoveredPosition;
                lastRecoveryTime = Time.time;
                if (hasRequestedDestination)
                {
                    agent.SetDestination(requestedDestination);
                }

                return;
            }

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, recoveryNavMeshSampleRadius, agent.areaMask)
                && IsRecoveryPositionClear(hit.position))
            {
                agent.updatePosition = true;
                agent.updateRotation = true;
                agent.Warp(hit.position);
                lastRecoveryPosition = hit.position;
                lastRecoveryTime = Time.time;
                if (hasRequestedDestination)
                {
                    agent.SetDestination(requestedDestination);
                }
            }
        }

        private void CancelActiveTraversalForRecovery()
        {
            if (traversalRoutine != null)
            {
                StopCoroutine(traversalRoutine);
                traversalRoutine = null;
            }

            traversalAnimationPhase = TraversalAnimationPhase.None;
            offMeshChaseUntilTime = 0f;
        }

        private void ResetDestinationProgressTracking()
        {
            noProgressAttemptCount = 0;
            recoveryAttemptCount = 0;
            hasProgressCheckPosition = false;
            nextProgressCheckTime = Time.time + noProgressCheckInterval;
        }

        private bool TryFindDynamicObstacleDetour(out Vector3 detour)
        {
            detour = default;
            Vector3 forward = agent.desiredVelocity;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.01f)
            {
                forward = agent.steeringTarget - transform.position;
                forward.y = 0f;
            }

            if (forward.sqrMagnitude <= 0.01f)
            {
                return false;
            }

            forward.Normalize();
            Vector3 probeOrigin = transform.position + Vector3.up * dynamicObstacleProbeHeight;
            int hitCount = Physics.SphereCastNonAlloc(
                probeOrigin,
                dynamicObstacleProbeRadius,
                forward,
                dynamicObstacleHits,
                dynamicObstacleProbeDistance,
                dynamicObstacleMask,
                QueryTriggerInteraction.Ignore);

            Collider closestObstacle = null;
            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = dynamicObstacleHits[i];
                Rigidbody obstacleBody = hit.collider != null ? hit.collider.attachedRigidbody : null;
                if (obstacleBody == null
                    || obstacleBody.isKinematic
                    || hit.transform.IsChildOf(transform)
                    || transform.IsChildOf(hit.transform)
                    || hit.distance >= closestDistance)
                {
                    continue;
                }

                closestObstacle = hit.collider;
                closestDistance = hit.distance;
            }

            if (closestObstacle == null)
            {
                return false;
            }

            Bounds bounds = closestObstacle.bounds;
            Vector3 lateral = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 extents = bounds.extents;
            float lateralExtent = Mathf.Abs(lateral.x) * extents.x + Mathf.Abs(lateral.z) * extents.z;
            float forwardExtent = Mathf.Abs(forward.x) * extents.x + Mathf.Abs(forward.z) * extents.z;
            float sideOffset = agent.radius + lateralExtent + dynamicObstacleSidePadding;
            float forwardOffset = agent.radius + forwardExtent + dynamicObstacleSidePadding;
            Vector3 candidateCenter = bounds.center + forward * forwardOffset;
            candidateCenter.y = transform.position.y;

            bool foundLeft = TryEvaluateReachableDetour(
                candidateCenter + lateral * sideOffset,
                out Vector3 leftDetour,
                out float leftScore);
            bool foundRight = TryEvaluateReachableDetour(
                candidateCenter - lateral * sideOffset,
                out Vector3 rightDetour,
                out float rightScore);

            if (!foundLeft && !foundRight)
            {
                return false;
            }

            detour = !foundRight || foundLeft && leftScore <= rightScore
                ? leftDetour
                : rightDetour;
            return true;
        }

        private bool TryEvaluateReachableDetour(Vector3 candidate, out Vector3 detour, out float score)
        {
            detour = candidate;
            score = float.PositiveInfinity;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, dynamicObstacleSampleRadius, agent.areaMask))
            {
                return false;
            }

            if (!agent.CalculatePath(hit.position, dynamicObstaclePath)
                || dynamicObstaclePath.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }

            detour = hit.position;
            score = GetPathDistance(dynamicObstaclePath) + Vector3.Distance(detour, requestedDestination);
            return true;
        }

        private void ResumeRequestedDestination()
        {
            isAvoidingDynamicObstacle = false;
            if (hasRequestedDestination && agent.enabled && agent.isOnNavMesh)
            {
                agent.SetDestination(requestedDestination);
            }
        }

        private void SnapToNavMeshIfNeeded()
        {
            if (agent == null || agent.isOnNavMesh || startNavMeshSnapRadius <= 0f)
            {
                return;
            }

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, startNavMeshSnapRadius, agent.areaMask))
            {
                agent.Warp(hit.position);
                agent.updatePosition = true;
                agent.updateRotation = true;
            }
        }

        private bool TryFindTopBelowTarget(Transform target, Vector3 approachDirection, out Vector3 climbTarget)
        {
            Vector3 probeOrigin = target.position - approachDirection * targetClimbForwardOffset + Vector3.up * 0.75f;
            float probeDistance = ledgeMaximumHeight + 1.5f;
            int hitCount = Physics.RaycastNonAlloc(
                probeOrigin,
                Vector3.down,
                climbProbeHits,
                probeDistance,
                climbMask,
                QueryTriggerInteraction.Ignore);

            float bestHeight = float.NegativeInfinity;
            Vector3 bestPoint = Vector3.zero;
            bool found = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = climbProbeHits[i];
                if (hit.collider == null || hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(target))
                {
                    continue;
                }

                float ledgeHeight = hit.point.y - transform.position.y;
                if (ledgeHeight < ledgeMinimumHeight || ledgeHeight > ledgeMaximumHeight + targetClimbVerticalPadding)
                {
                    continue;
                }

                if (hit.point.y > bestHeight)
                {
                    bestHeight = hit.point.y;
                    bestPoint = hit.point;
                    found = true;
                }
            }

            climbTarget = bestPoint + Vector3.up * 0.03f;
            return found;
        }

        private IEnumerator TraverseOffMeshLink()
        {
            OffMeshLinkData linkData = agent.currentOffMeshLinkData;
            Vector3 start = transform.position;
            Vector3 end = linkData.endPos;
            float timer = 0f;

            agent.updatePosition = false;
            agent.updateRotation = false;
            traversalAnimationPhase = TraversalAnimationPhase.Start;

            while (timer < offMeshTraverseDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / offMeshTraverseDuration);
                if (t >= traversalStartPortion)
                {
                    traversalAnimationPhase = TraversalAnimationPhase.Loop;
                }

                float arc = Mathf.Sin(t * Mathf.PI) * offMeshJumpHeight;
                transform.position = Vector3.Lerp(start, end, t) + Vector3.up * arc;
                FaceTowards(end, 16f);
                yield return null;
            }

            transform.position = end;
            agent.updatePosition = true;
            agent.updateRotation = true;
            agent.Warp(end);
            agent.CompleteOffMeshLink();
            yield return PlayLandingReaction();
            traversalRoutine = null;
            traversalAnimationPhase = TraversalAnimationPhase.None;
        }

        private bool TryStartLedgeClimb()
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude > 0.25f)
            {
                return false;
            }

            Vector3 forward = transform.forward;
            Vector3 wallRayOrigin = transform.position + Vector3.up * (ledgeMinimumHeight + 0.1f);
            if (!Physics.Raycast(wallRayOrigin, forward, out RaycastHit wallHit, ledgeCheckDistance, climbMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (wallHit.normal.y > 0.25f)
            {
                return false;
            }

            float topProbeHeight = ledgeMaximumHeight + 0.35f;
            Vector3 topRayOrigin = wallHit.point + forward * ledgeTopProbeForwardOffset + Vector3.up * topProbeHeight;
            if (!Physics.Raycast(topRayOrigin, Vector3.down, out RaycastHit topHit, topProbeHeight + 0.5f, climbMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            float ledgeHeight = topHit.point.y - transform.position.y;
            if (ledgeHeight < ledgeMinimumHeight || ledgeHeight > ledgeMaximumHeight)
            {
                return false;
            }

            Vector3 climbTarget = topHit.point + Vector3.up * 0.03f;
            traversalRoutine = StartCoroutine(ClimbLedge(climbTarget, false, ledgeClimbDuration, 0f));
            return true;
        }

        private bool TryFindDropLanding(Vector3 targetPosition, out Vector3 landingPoint)
        {
            if (NavMesh.SamplePosition(targetPosition, out NavMeshHit navMeshHit, dropLandingSampleRadius, agent.areaMask))
            {
                landingPoint = navMeshHit.position;
                return true;
            }

            Vector3 probeOrigin = targetPosition + Vector3.up * 1.5f;
            if (Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit hit, targetDropMaximumHeight + 2f, climbMask, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(transform))
            {
                landingPoint = hit.point + Vector3.up * 0.03f;
                return true;
            }

            landingPoint = targetPosition;
            return false;
        }

        private IEnumerator ClimbLedge(Vector3 target, bool allowOffMeshFinish, float duration, float arcHeight)
        {
            Vector3 start = transform.position;
            float timer = 0f;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }

            agent.updatePosition = false;
            agent.updateRotation = false;
            traversalAnimationPhase = TraversalAnimationPhase.Climb;

            float moveDuration = Mathf.Max(0.01f, duration);
            while (timer < moveDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / moveDuration));
                float arc = Mathf.Sin(t * Mathf.PI) * arcHeight;
                transform.position = Vector3.Lerp(start, target, t) + Vector3.up * arc;
                FaceTowards(target, 16f);
                yield return null;
            }

            transform.position = target;

            bool warpedToNavMesh = false;
            if (NavMesh.SamplePosition(target, out NavMeshHit navMeshHit, startNavMeshSnapRadius, agent.areaMask))
            {
                warpedToNavMesh = agent.Warp(navMeshHit.position);
            }

            if (!warpedToNavMesh)
            {
                agent.nextPosition = transform.position;
                agent.updatePosition = !allowOffMeshFinish;
                agent.updateRotation = !allowOffMeshFinish;
                if (allowOffMeshFinish)
                {
                    offMeshChaseUntilTime = Time.time + postClimbOffMeshChaseTime;
                }
            }
            else
            {
                agent.updatePosition = true;
                agent.updateRotation = true;
                if (hasRequestedDestination)
                {
                    agent.SetDestination(requestedDestination);
                }
            }

            yield return PlayLandingReaction();
            traversalRoutine = null;
            traversalAnimationPhase = TraversalAnimationPhase.None;
        }

        private IEnumerator JumpToPoint(Vector3 target, float duration, float arcHeight)
        {
            Vector3 start = transform.position;
            float timer = 0f;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }

            agent.updatePosition = false;
            agent.updateRotation = false;
            traversalAnimationPhase = TraversalAnimationPhase.Start;

            float moveDuration = Mathf.Max(0.01f, duration);
            while (timer < moveDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / moveDuration);
                if (t >= traversalStartPortion)
                {
                    traversalAnimationPhase = TraversalAnimationPhase.Loop;
                }

                float arc = Mathf.Sin(t * Mathf.PI) * arcHeight;
                transform.position = Vector3.Lerp(start, target, t) + Vector3.up * arc;
                FaceTowards(target, 18f);
                yield return null;
            }

            transform.position = target;
            if (NavMesh.SamplePosition(target, out NavMeshHit navMeshHit, startNavMeshSnapRadius, agent.areaMask))
            {
                agent.Warp(navMeshHit.position);
                agent.updatePosition = true;
                agent.updateRotation = true;
            }
            else
            {
                agent.nextPosition = transform.position;
                agent.updatePosition = false;
                agent.updateRotation = false;
                offMeshChaseUntilTime = Time.time + postClimbOffMeshChaseTime;
            }

            yield return PlayLandingReaction();
            traversalRoutine = null;
            traversalAnimationPhase = TraversalAnimationPhase.None;
        }

        private IEnumerator PlayLandingReaction()
        {
            traversalAnimationPhase = TraversalAnimationPhase.Landing;
            bool restoreMovement = agent != null && agent.enabled && agent.isOnNavMesh && !agent.isStopped;
            if (restoreMovement)
            {
                agent.isStopped = true;
            }

            float timer = 0f;
            while (timer < traversalLandingReactionDuration)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (restoreMovement && agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
            }
        }

        private IEnumerator Knockback(Vector3 direction, float distance, float duration)
        {
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }

            agent.updatePosition = false;
            agent.updateRotation = false;

            Vector3 start = transform.position;
            Vector3 desiredEnd = start + direction * distance;
            Vector3 end = desiredEnd;
            if (NavMesh.SamplePosition(desiredEnd, out NavMeshHit hit, Mathf.Max(0.5f, distance), agent.areaMask))
            {
                end = hit.position;
            }

            float timer = 0f;
            float moveDuration = Mathf.Max(0.01f, duration);
            while (timer < moveDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / moveDuration));
                transform.position = Vector3.Lerp(start, end, t);
                yield return null;
            }

            transform.position = end;
            if (NavMesh.SamplePosition(end, out NavMeshHit finalHit, startNavMeshSnapRadius, agent.areaMask))
            {
                agent.Warp(finalHit.position);
                agent.updatePosition = true;
                agent.updateRotation = true;
            }
            else
            {
                agent.nextPosition = transform.position;
                agent.updatePosition = false;
                agent.updateRotation = false;
                offMeshChaseUntilTime = Time.time + postClimbOffMeshChaseTime;
            }

            knockbackRoutine = null;
        }
    }
}
