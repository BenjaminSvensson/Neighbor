using System.Collections;
using Neighbor.Main.Features.Interaction;
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

        [Header("Chase Pursuit")]
        [SerializeField, Min(0f)] private float chaseStoppingDistance = 0.08f;
        [SerializeField, Min(1f)] private float chaseAccelerationMultiplier = 1.6f;

        [Header("Low Clearance Crouching")]
        [SerializeField] private bool enableLowClearanceCrouching = true;
        [SerializeField] private LayerMask lowClearanceMask = ~0;
        [SerializeField, Min(0.5f)] private float crouchingHeight = 1.3f;
        [SerializeField, Range(0.1f, 1f)] private float crouchSpeedMultiplier = 0.55f;
        [SerializeField, Min(0f)] private float lowClearanceProbeForwardDistance = 0.7f;
        [SerializeField, Min(0f)] private float lowClearanceProbePadding = 0.04f;
        [SerializeField, Min(0f)] private float crouchExitDelay = 0.2f;

        [Header("Closest Reachable Destination")]
        [SerializeField] private bool approachClosestReachableDestination = true;
        [SerializeField, Min(0.5f)] private float closestReachableSearchRadius = 12f;
        [SerializeField, Range(4, 32)] private int closestReachableCandidateCount = 12;

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
        [SerializeField, Min(0f)] private float maximumUsefulDetourExtraDistance = 2.25f;
        [SerializeField, Min(1f)] private float maximumUsefulDetourDistanceRatio = 1.35f;
        [SerializeField] private bool climbDynamicObstaclesWhenDetourIsPoor = true;

        [Header("Blocked Destination")]
        [SerializeField, Min(0f)] private float destinationRefreshDistance = 0.18f;
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
        [SerializeField, Min(0f)] private float postClimbNavMeshVerticalSnapTolerance = 0.35f;
        [SerializeField] private LayerMask climbMask = ~0;
        [SerializeField] private LayerMask traversalGlassBreakMask = ~0;
        [SerializeField, Min(0.01f)] private float traversalGlassBreakRadius = 0.35f;

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

        [Header("Unreachable Drop Recovery")]
        [SerializeField] private bool enableUnreachableDropRecovery = true;
        [SerializeField, Min(0.5f)] private float dropRecoveryHorizontalReach = 4f;
        [SerializeField, Range(8, 32)] private int dropRecoveryCandidateCount = 20;
        [SerializeField, Range(2, 8)] private int dropRecoveryDistanceSteps = 4;
        [SerializeField, Min(0.05f)] private float dropRecoveryLandingSampleRadius = 0.8f;
        [SerializeField, Min(0f)] private float dropRecoveryGoalSeparation = 1.5f;
        [SerializeField, Min(0f)] private float dropRecoveryCooldown = 1.25f;
        [SerializeField, Min(0)] private int dropRecoveryNoProgressAttempts = 1;

        private NavMeshAgent agent;
        private Coroutine traversalRoutine;
        private Coroutine knockbackRoutine;
        private Vector3 requestedDestination;
        private bool hasRequestedDestination;
        private Vector3 navigationGoal;
        private bool hasNavigationGoal;
        private float nextDropRecoveryTime;
        private float offMeshChaseUntilTime;
        private readonly RaycastHit[] climbProbeHits = new RaycastHit[10];
        private readonly RaycastHit[] traversalGlassHits = new RaycastHit[12];
        private readonly RaycastHit[] dynamicObstacleHits = new RaycastHit[12];
        private readonly Collider[] recoveryOverlapHits = new Collider[16];
        private readonly Collider[] lowClearanceOverlapHits = new Collider[16];
        private NavMeshPath destinationPath;
        private NavMeshPath dynamicObstaclePath;
        private NavMeshPath recoveryPath;
        private bool isAvoidingDynamicObstacle;
        private bool isPaused;
        private bool chasePursuitActive;
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
        private bool isAnchoredForTask;
        private Collider[] activeClimbSurfaceColliders;
        private Collider[] ownColliders;
        private bool hasPendingKnockback;
        private Vector3 pendingKnockbackDirection;
        private float pendingKnockbackDistance;
        private float pendingKnockbackDuration;
        private CharacterController characterController;
        private NeighborBrain brain;
        private MoveMode currentMoveMode = MoveMode.Walk;
        private bool isCrouchingForClearance;
        private float standingAgentHeight;
        private float standingAgentBaseOffset;
        private float standingControllerHeight;
        private Vector3 standingControllerCenter;
        private float clearToStandSinceTime = float.NegativeInfinity;

        public bool IsTraversingSpecialMove => traversalRoutine != null;
        public TraversalAnimationPhase CurrentTraversalAnimationPhase => traversalAnimationPhase;
        public bool IsOffMeshChasing => Time.time < offMeshChaseUntilTime;
        public bool IsDetachedFromNavMesh => agent != null && (!agent.updatePosition || !agent.isOnNavMesh);
        public bool IsPaused => isPaused;
        public bool IsAnchoredForTask => isAnchoredForTask;
        public bool IsAvoidingDynamicObstacle => isAvoidingDynamicObstacle;
        public bool IsCrouchingForClearance => isCrouchingForClearance;
        public Vector3 DynamicObstacleDetour => dynamicObstacleDetour;
        public Vector3 RequestedDestination => requestedDestination;
        public Vector3 NavigationGoal => navigationGoal;
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
            destinationPath = new NavMeshPath();
            dynamicObstaclePath = new NavMeshPath();
            recoveryPath = new NavMeshPath();
            agent = GetComponent<NavMeshAgent>();
            characterController = GetComponent<CharacterController>();
            brain = GetComponent<NeighborBrain>();
            ownColliders = GetComponentsInChildren<Collider>(true);
            CaptureStandingDimensions();
            ConfigureAgent();
            SnapToNavMeshIfNeeded();
        }

        private void OnDisable()
        {
            SetClimbSurfaceCollisionIgnored(false);
            SetCrouchingForClearance(false);
        }

        private void Update()
        {
            if (agent == null)
            {
                return;
            }

            UpdateLowClearanceCrouching();

            if (isAnchoredForTask)
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

            if (TryStartUnreachableDropRecovery())
            {
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
            currentMoveMode = mode;
            if (agent == null)
            {
                return;
            }

            if (IsOffMeshChasing)
            {
                return;
            }

            ApplyMoveSpeed();
        }

        public void SetChasePursuitActive(bool active)
        {
            if (chasePursuitActive == active)
            {
                return;
            }

            chasePursuitActive = active;
            ApplyMoveSpeed();
        }

        public float GetMoveSpeed(MoveMode mode)
        {
            float speed = mode switch
            {
                MoveMode.Run => runSpeed,
                MoveMode.Cautious => cautiousSpeed,
                _ => walkSpeed
            };

            return isCrouchingForClearance ? speed * crouchSpeedMultiplier : speed;
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

            navigationGoal = destination;
            hasNavigationGoal = true;
            if (!TryResolveClosestReachableDestination(
                    destination,
                    Mathf.Max(0f, sampleRadius),
                    out Vector3 resolvedDestination))
            {
                sampledDestination = destination;
                return false;
            }

            bool destinationChanged = !hasRequestedDestination
                || Vector3.Distance(requestedDestination, resolvedDestination) > destinationChangeResetDistance;
            bool shouldRefreshPath = !hasRequestedDestination
                || Vector3.Distance(requestedDestination, resolvedDestination) > destinationRefreshDistance
                || (!agent.hasPath && !agent.pathPending);
            requestedDestination = resolvedDestination;
            hasRequestedDestination = true;
            Vector3 movementSinceProgressCheck = transform.position - progressCheckPosition;
            movementSinceProgressCheck.y = 0f;
            if (destinationChanged
                && (!hasProgressCheckPosition
                    || movementSinceProgressCheck.sqrMagnitude >= minimumProgressPerAttempt * minimumProgressPerAttempt))
            {
                ResetDestinationProgressTracking();
            }

            sampledDestination = resolvedDestination;
            agent.isStopped = isPaused;
            if (isAvoidingDynamicObstacle)
            {
                return true;
            }

            if (!shouldRefreshPath)
            {
                return true;
            }

            return agent.SetPath(destinationPath);
        }

        private bool TryResolveClosestReachableDestination(
            Vector3 destination,
            float sampleRadius,
            out Vector3 resolvedDestination)
        {
            resolvedDestination = destination;
            bool found = TryEvaluateDestinationCandidate(
                destination,
                sampleRadius,
                destination,
                out Vector3 bestDestination,
                out float bestScore,
                out bool completePath);
            if (completePath)
            {
                resolvedDestination = bestDestination;
                return agent.CalculatePath(resolvedDestination, destinationPath)
                    && destinationPath.status == NavMeshPathStatus.PathComplete;
            }

            if (!approachClosestReachableDestination)
            {
                return false;
            }

            int candidateCount = Mathf.Max(4, closestReachableCandidateCount);
            float maximumRadius = Mathf.Max(sampleRadius, closestReachableSearchRadius);
            if (maximumRadius > sampleRadius
                && TryEvaluateDestinationCandidate(
                    destination,
                    maximumRadius,
                    destination,
                    out Vector3 expandedDestination,
                    out float expandedScore,
                    out _)
                && expandedScore < bestScore)
            {
                found = true;
                bestDestination = expandedDestination;
                bestScore = expandedScore;
            }

            const float goldenAngle = 137.50776f;
            for (int i = 0; i < candidateCount; i++)
            {
                float radius01 = Mathf.Sqrt((i + 1f) / candidateCount);
                float radius = Mathf.Lerp(Mathf.Max(0.1f, sampleRadius), maximumRadius, radius01);
                Vector3 direction = Quaternion.AngleAxis(i * goldenAngle, Vector3.up) * Vector3.forward;
                Vector3 candidate = destination + direction * radius;
                if (!TryEvaluateDestinationCandidate(
                        candidate,
                        Mathf.Max(0.5f, sampleRadius),
                        destination,
                        out Vector3 candidateDestination,
                        out float candidateScore,
                        out bool candidateComplete)
                    || candidateScore >= bestScore)
                {
                    continue;
                }

                found = true;
                bestDestination = candidateDestination;
                bestScore = candidateScore;
                if (candidateComplete && bestScore <= sampleRadius * sampleRadius)
                {
                    break;
                }
            }

            if (!found)
            {
                return false;
            }

            resolvedDestination = bestDestination;
            return agent.CalculatePath(resolvedDestination, destinationPath)
                && destinationPath.status == NavMeshPathStatus.PathComplete;
        }

        private bool TryEvaluateDestinationCandidate(
            Vector3 candidate,
            float sampleRadius,
            Vector3 desiredDestination,
            out Vector3 reachableDestination,
            out float score,
            out bool completePath)
        {
            reachableDestination = candidate;
            score = float.PositiveInfinity;
            completePath = false;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRadius, agent.areaMask)
                || !agent.CalculatePath(hit.position, destinationPath)
                || destinationPath.status == NavMeshPathStatus.PathInvalid
                || destinationPath.corners == null
                || destinationPath.corners.Length == 0)
            {
                return false;
            }

            completePath = destinationPath.status == NavMeshPathStatus.PathComplete;
            reachableDestination = completePath
                ? hit.position
                : destinationPath.corners[destinationPath.corners.Length - 1];
            score = (reachableDestination - desiredDestination).sqrMagnitude;
            return true;
        }

        public bool CanReach(Vector3 destination, out float pathDistance, out Vector3 sampledPosition)
        {
            return CanReachNear(destination, destinationSampleRadius, out pathDistance, out sampledPosition);
        }

        public bool CanReachNear(Vector3 destination, float sampleRadius, out float pathDistance, out Vector3 sampledPosition)
        {
            pathDistance = float.PositiveInfinity;
            sampledPosition = destination;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                return false;
            }

            if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, Mathf.Max(destinationSampleRadius, sampleRadius), agent.areaMask))
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
            if (!TryFindTopBelowTarget(target, approachDirection, out Vector3 climbTarget, out Collider climbSurface))
            {
                return false;
            }

            isAvoidingDynamicObstacle = false;
            traversalRoutine = StartCoroutine(ClimbLedge(
                climbTarget,
                climbSurface,
                chaseClimbDuration,
                chaseClimbArcHeight));
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
            if (dropHeight < targetDropMinimumHeight || dropHeight > targetDropMaximumHeight)
            {
                return false;
            }

            Vector3 landingPoint = default;
            bool foundLanding = horizontalDistance <= targetDropHorizontalReach
                && TryFindDropLanding(target.position, out landingPoint);
            if (!foundLanding
                && !TryFindSafeDropRecoveryLanding(target.position, out landingPoint))
            {
                return false;
            }

            isAvoidingDynamicObstacle = false;
            nextDropRecoveryTime = Time.time + dropRecoveryCooldown;
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
                null,
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
                QueueKnockback(direction.normalized, distance, duration);
                return;
            }

            isAvoidingDynamicObstacle = false;
            knockbackRoutine = StartCoroutine(Knockback(direction.normalized, distance, duration));
        }

        private void QueueKnockback(Vector3 direction, float distance, float duration)
        {
            if (!hasPendingKnockback || distance >= pendingKnockbackDistance)
            {
                pendingKnockbackDirection = direction;
                pendingKnockbackDistance = distance;
                pendingKnockbackDuration = duration;
            }

            hasPendingKnockback = true;
        }

        private void FinishTraversal()
        {
            traversalRoutine = null;
            traversalAnimationPhase = TraversalAnimationPhase.None;
            SetClimbSurfaceCollisionIgnored(false);
            if (!hasPendingKnockback)
            {
                return;
            }

            Vector3 direction = pendingKnockbackDirection;
            float distance = pendingKnockbackDistance;
            float duration = pendingKnockbackDuration;
            hasPendingKnockback = false;
            pendingKnockbackDistance = 0f;
            pendingKnockbackDuration = 0f;
            isAvoidingDynamicObstacle = false;
            knockbackRoutine = StartCoroutine(Knockback(direction, distance, duration));
        }

        private static Transform GetClimbAnchor(Collider climbSurface)
        {
            if (climbSurface == null)
            {
                return null;
            }

            return climbSurface.attachedRigidbody != null
                ? climbSurface.attachedRigidbody.transform
                : climbSurface.transform;
        }

        private void BeginClimbSurfaceCollisionIgnore(Collider climbSurface)
        {
            SetClimbSurfaceCollisionIgnored(false);
            Transform climbAnchor = GetClimbAnchor(climbSurface);
            if (climbAnchor == null)
            {
                return;
            }

            activeClimbSurfaceColliders = climbAnchor.GetComponentsInChildren<Collider>(true);
            SetClimbSurfaceCollisionIgnored(true);
        }

        private void SetClimbSurfaceCollisionIgnored(bool ignored)
        {
            if (activeClimbSurfaceColliders == null)
            {
                return;
            }

            if (ownColliders == null || ownColliders.Length == 0)
            {
                ownColliders = GetComponentsInChildren<Collider>(true);
            }

            for (int surfaceIndex = 0; surfaceIndex < activeClimbSurfaceColliders.Length; surfaceIndex++)
            {
                Collider surfaceCollider = activeClimbSurfaceColliders[surfaceIndex];
                if (surfaceCollider == null || surfaceCollider.isTrigger)
                {
                    continue;
                }

                for (int ownIndex = 0; ownIndex < ownColliders.Length; ownIndex++)
                {
                    Collider ownCollider = ownColliders[ownIndex];
                    if (ownCollider != null && ownCollider != surfaceCollider)
                    {
                        Physics.IgnoreCollision(ownCollider, surfaceCollider, ignored);
                    }
                }
            }

            if (!ignored)
            {
                activeClimbSurfaceColliders = null;
            }
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
            hasNavigationGoal = false;
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
            SetCrouchingForClearance(false);

            if (traversalRoutine != null)
            {
                StopCoroutine(traversalRoutine);
                traversalRoutine = null;
                traversalAnimationPhase = TraversalAnimationPhase.None;
                SetClimbSurfaceCollisionIgnored(false);
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
            hasNavigationGoal = false;
            isAvoidingDynamicObstacle = false;
            isPaused = false;
            isAnchoredForTask = false;
            hasPendingKnockback = false;
            ResetDestinationProgressTracking();
            offMeshChaseUntilTime = 0f;

            if (agent != null && agent.enabled)
            {
                agent.updatePosition = true;
                agent.updateRotation = true;
                if (agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                }

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
                    if (agent.isOnNavMesh)
                    {
                        agent.nextPosition = position;
                    }
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

        public void BeginAnchoredTask(Transform anchor)
        {
            if (anchor == null)
            {
                return;
            }

            BeginAnchoredTask(anchor.position, anchor.rotation);
        }

        public void BeginAnchoredTask(Vector3 position, Quaternion rotation)
        {
            Stop();
            isAnchoredForTask = true;
            isPaused = true;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.updatePosition = false;
                agent.updateRotation = false;
            }

            transform.SetPositionAndRotation(position, rotation);
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.nextPosition = position;
            }
        }

        public void EndAnchoredTask(Vector3 exitPosition, Quaternion exitRotation)
        {
            if (!isAnchoredForTask)
            {
                return;
            }

            ResetToPosition(exitPosition, exitRotation);
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
            ApplyMoveSpeed();
            agent.angularSpeed = angularSpeed;
            agent.autoRepath = true;
            agent.autoTraverseOffMeshLink = false;
        }

        private void CaptureStandingDimensions()
        {
            standingAgentHeight = agent != null ? agent.height : 2f;
            standingAgentBaseOffset = agent != null ? agent.baseOffset : 0f;
            if (characterController == null)
            {
                standingControllerHeight = standingAgentHeight;
                standingControllerCenter = Vector3.up * (standingControllerHeight * 0.5f);
                return;
            }

            standingControllerHeight = characterController.height;
            standingControllerCenter = characterController.center;
        }

        private void UpdateLowClearanceCrouching()
        {
            if (!enableLowClearanceCrouching || traversalRoutine != null || knockbackRoutine != null)
            {
                return;
            }

            Vector3 forward = GetClearanceProbeDirection();
            Vector3 aheadPosition = transform.position + forward * lowClearanceProbeForwardDistance;
            float standingHeight = Mathf.Max(standingAgentHeight, standingControllerHeight);
            float loweredHeight = Mathf.Min(crouchingHeight, standingHeight);
            bool standingClearHere = HasClearanceForHeight(transform.position, standingHeight);
            bool standingClearAhead = HasClearanceForHeight(aheadPosition, standingHeight);

            if (!isCrouchingForClearance)
            {
                bool canCrouchHere = HasClearanceForHeight(transform.position, loweredHeight);
                bool canCrouchAhead = HasClearanceForHeight(aheadPosition, loweredHeight);
                if ((!standingClearHere && canCrouchHere) || (!standingClearAhead && canCrouchAhead))
                {
                    SetCrouchingForClearance(true);
                }

                return;
            }

            if (!standingClearHere || !standingClearAhead)
            {
                clearToStandSinceTime = float.NegativeInfinity;
                return;
            }

            if (float.IsNegativeInfinity(clearToStandSinceTime))
            {
                clearToStandSinceTime = Time.time;
                return;
            }

            if (Time.time - clearToStandSinceTime >= crouchExitDelay)
            {
                SetCrouchingForClearance(false);
            }
        }

        private Vector3 GetClearanceProbeDirection()
        {
            if (agent != null)
            {
                Vector3 direction = agent.desiredVelocity;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.01f)
                {
                    return direction.normalized;
                }

                if (agent.hasPath && !agent.pathPending)
                {
                    direction = agent.steeringTarget - transform.position;
                    direction.y = 0f;
                    if (direction.sqrMagnitude > 0.01f)
                    {
                        return direction.normalized;
                    }
                }
            }

            Vector3 forward = transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
        }

        private bool HasClearanceForHeight(Vector3 feetPosition, float height)
        {
            float sourceRadius = characterController != null
                ? characterController.radius
                : agent != null
                    ? agent.radius
                    : 0.4f;
            float radius = Mathf.Max(0.05f, sourceRadius - lowClearanceProbePadding);
            float checkedHeight = Mathf.Max(radius * 2f, height);
            Vector3 bottom = feetPosition + Vector3.up * (radius + lowClearanceProbePadding);
            Vector3 top = feetPosition + Vector3.up * (checkedHeight - radius - lowClearanceProbePadding);
            int hitCount = Physics.OverlapCapsuleNonAlloc(
                bottom,
                top,
                radius,
                lowClearanceOverlapHits,
                lowClearanceMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = lowClearanceOverlapHits[i];
                if (hit != null && !hit.transform.IsChildOf(transform) && !transform.IsChildOf(hit.transform))
                {
                    return false;
                }
            }

            return true;
        }

        private void SetCrouchingForClearance(bool crouching)
        {
            if (isCrouchingForClearance == crouching)
            {
                return;
            }

            isCrouchingForClearance = crouching;
            clearToStandSinceTime = float.NegativeInfinity;
            float targetHeight = crouching
                ? Mathf.Min(crouchingHeight, Mathf.Max(standingAgentHeight, standingControllerHeight))
                : standingAgentHeight;

            if (agent != null)
            {
                agent.height = targetHeight;
                agent.baseOffset = standingAgentBaseOffset;
            }

            if (characterController != null)
            {
                float controllerHeight = crouching
                    ? Mathf.Min(crouchingHeight, standingControllerHeight)
                    : standingControllerHeight;
                Vector3 center = standingControllerCenter;
                center.y -= (standingControllerHeight - controllerHeight) * 0.5f;
                characterController.height = controllerHeight;
                characterController.center = center;
            }

            ApplyMoveSpeed();
        }

        private void ApplyMoveSpeed()
        {
            if (agent != null)
            {
                agent.speed = GetMoveSpeed(currentMoveMode);
                agent.acceleration = chasePursuitActive
                    ? acceleration * chaseAccelerationMultiplier
                    : acceleration;
                agent.stoppingDistance = chasePursuitActive
                    ? chaseStoppingDistance
                    : stoppingDistance;
                agent.autoBraking = !chasePursuitActive;
            }
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
            if (!TryFindDynamicObstacleResponse(
                    out Collider blockingObstacle,
                    out Vector3 detour,
                    out bool shouldUseDetour))
            {
                return;
            }

            if (!shouldUseDetour)
            {
                if (climbDynamicObstaclesWhenDetourIsPoor
                    && enableLedgeClimb
                    && TryStartClimbOverDynamicObstacle(blockingObstacle))
                {
                    return;
                }

                ResumeRequestedDestination();
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

            Vector3 abandonedDestination = hasNavigationGoal ? navigationGoal : requestedDestination;
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

        private void ResetDestinationProgressTracking()
        {
            noProgressAttemptCount = 0;
            recoveryAttemptCount = 0;
            hasProgressCheckPosition = false;
            nextProgressCheckTime = Time.time + noProgressCheckInterval;
        }

        private bool TryFindDynamicObstacleResponse(
            out Collider blockingObstacle,
            out Vector3 detour,
            out bool shouldUseDetour)
        {
            blockingObstacle = null;
            detour = default;
            shouldUseDetour = false;
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

                blockingObstacle = hit.collider;
                closestDistance = hit.distance;
            }

            if (blockingObstacle == null)
            {
                return false;
            }

            Bounds bounds = blockingObstacle.bounds;
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
                return true;
            }

            float bestScore;
            if (!foundRight || foundLeft && leftScore <= rightScore)
            {
                detour = leftDetour;
                bestScore = leftScore;
            }
            else
            {
                detour = rightDetour;
                bestScore = rightScore;
            }

            float directDistance = GetCurrentDirectRouteEstimate();
            shouldUseDetour = IsDynamicObstacleDetourWorthTaking(directDistance, bestScore);
            return true;
        }

        private float GetCurrentDirectRouteEstimate()
        {
            float directDistance = Vector3.Distance(transform.position, requestedDestination);
            if (!agent.pathPending && !float.IsInfinity(agent.remainingDistance))
            {
                directDistance = Mathf.Max(directDistance, agent.remainingDistance);
            }

            return Mathf.Max(0.1f, directDistance);
        }

        private bool IsDynamicObstacleDetourWorthTaking(float directDistance, float detourDistance)
        {
            float extraDistance = detourDistance - directDistance;
            return extraDistance <= maximumUsefulDetourExtraDistance
                && detourDistance <= directDistance * maximumUsefulDetourDistanceRatio;
        }

        private bool TryStartClimbOverDynamicObstacle(Collider obstacle)
        {
            if (obstacle == null
                || traversalRoutine != null
                || (climbMask.value & 1 << obstacle.gameObject.layer) == 0)
            {
                return false;
            }

            Vector3 forward = requestedDestination - transform.position;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.01f)
            {
                forward = transform.forward;
            }

            forward.Normalize();
            Bounds bounds = obstacle.bounds;
            Vector3 extents = bounds.extents;
            float forwardExtent = Mathf.Abs(forward.x) * extents.x + Mathf.Abs(forward.z) * extents.z;
            Vector3 topProbeOrigin = bounds.center
                + forward * Mathf.Min(ledgeTopProbeForwardOffset, forwardExtent * 0.5f)
                + Vector3.up * (extents.y + 0.5f);
            Ray topRay = new Ray(topProbeOrigin, Vector3.down);
            if (!obstacle.Raycast(topRay, out RaycastHit topHit, bounds.size.y + 1f))
            {
                return false;
            }

            float ledgeHeight = topHit.point.y - transform.position.y;
            if (ledgeHeight < ledgeMinimumHeight || ledgeHeight > ledgeMaximumHeight)
            {
                return false;
            }

            isAvoidingDynamicObstacle = false;
            Vector3 climbTarget = topHit.point + Vector3.up * 0.03f;
            traversalRoutine = StartCoroutine(ClimbLedge(
                climbTarget,
                obstacle,
                ledgeClimbDuration,
                0f));
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

        private bool TryFindTopBelowTarget(
            Transform target,
            Vector3 approachDirection,
            out Vector3 climbTarget,
            out Collider climbSurface)
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
            climbSurface = null;
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
                    climbSurface = hit.collider;
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
                Vector3 previousPosition = transform.position;
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / offMeshTraverseDuration);
                if (t >= traversalStartPortion)
                {
                    traversalAnimationPhase = TraversalAnimationPhase.Loop;
                }

                float arc = Mathf.Sin(t * Mathf.PI) * offMeshJumpHeight;
                Vector3 nextPosition = Vector3.Lerp(start, end, t) + Vector3.up * arc;
                BreakGlassAlongTraversal(previousPosition, nextPosition);
                transform.position = nextPosition;
                FaceTowards(end, 16f);
                yield return null;
            }

            BreakGlassAlongTraversal(transform.position, end);
            transform.position = end;
            agent.updatePosition = true;
            agent.updateRotation = true;
            agent.Warp(end);
            agent.CompleteOffMeshLink();
            yield return PlayLandingReaction();
            FinishTraversal();
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
            traversalRoutine = StartCoroutine(ClimbLedge(
                climbTarget,
                topHit.collider,
                ledgeClimbDuration,
                0f));
            return true;
        }

        private bool TryStartUnreachableDropRecovery()
        {
            if (!enableUnreachableDropRecovery
                || Time.time < nextDropRecoveryTime
                || !hasNavigationGoal
                || !hasRequestedDestination
                || isPaused
                || isAnchoredForTask
                || traversalRoutine != null
                || knockbackRoutine != null
                || agent == null
                || !agent.enabled
                || !agent.isOnNavMesh)
            {
                return false;
            }

            if (transform.position.y - navigationGoal.y < targetDropMinimumHeight)
            {
                return false;
            }

            float goalSeparation = Vector3.Distance(requestedDestination, navigationGoal);
            bool reachedClosestReachablePoint = HasArrived
                || noProgressAttemptCount >= dropRecoveryNoProgressAttempts;
            if (goalSeparation < dropRecoveryGoalSeparation || !reachedClosestReachablePoint)
            {
                return false;
            }

            nextDropRecoveryTime = Time.time + dropRecoveryCooldown;
            if (!TryFindSafeDropRecoveryLanding(navigationGoal, out Vector3 landingPoint))
            {
                return false;
            }

            isAvoidingDynamicObstacle = false;
            ResetDestinationProgressTracking();
            traversalRoutine = StartCoroutine(JumpToPoint(landingPoint, chaseDropDuration, chaseDropArcHeight));
            return true;
        }

        private bool TryFindSafeDropRecoveryLanding(Vector3 goal, out Vector3 landingPoint)
        {
            landingPoint = default;
            if (agent == null || !agent.enabled)
            {
                return false;
            }

            Vector3 origin = transform.position;
            Vector3 toGoal = goal - origin;
            toGoal.y = 0f;
            Vector3 preferredDirection = toGoal.sqrMagnitude > 0.01f
                ? toGoal.normalized
                : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (preferredDirection.sqrMagnitude <= 0.01f)
            {
                preferredDirection = Vector3.forward;
            }

            bool hasGoalOnNavMesh = NavMesh.SamplePosition(
                goal,
                out NavMeshHit goalNavMeshHit,
                dropRecoveryLandingSampleRadius,
                agent.areaMask);
            float currentGoalDistance = Vector3.Distance(origin, goal);
            float minimumProbeDistance = Mathf.Min(
                dropRecoveryHorizontalReach,
                Mathf.Max(0.75f, agent.radius * 1.6f));
            float bestScore = float.PositiveInfinity;
            bool found = false;
            int directionCount = Mathf.Max(8, dropRecoveryCandidateCount);
            int distanceSteps = Mathf.Max(2, dropRecoveryDistanceSteps);

            for (int directionIndex = 0; directionIndex < directionCount; directionIndex++)
            {
                float angle = directionIndex * (360f / directionCount);
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * preferredDirection;
                float alignmentPenalty = (1f - Mathf.Clamp01(Vector3.Dot(direction, preferredDirection))) * 2f;
                for (int distanceIndex = 0; distanceIndex < distanceSteps; distanceIndex++)
                {
                    float distance = Mathf.Lerp(
                        minimumProbeDistance,
                        dropRecoveryHorizontalReach,
                        distanceSteps <= 1 ? 1f : distanceIndex / (distanceSteps - 1f));
                    Vector3 probePosition = origin + direction * distance;
                    Vector3 probeOrigin = probePosition + Vector3.up * 0.75f;
                    if (!Physics.Raycast(
                            probeOrigin,
                            Vector3.down,
                            out RaycastHit surfaceHit,
                            targetDropMaximumHeight + 1.5f,
                            climbMask,
                            QueryTriggerInteraction.Ignore)
                        || surfaceHit.transform.IsChildOf(transform)
                        || transform.IsChildOf(surfaceHit.transform)
                        || !IsDropWithinRecoveryRange(origin, surfaceHit.point)
                        || !NavMesh.SamplePosition(
                            surfaceHit.point,
                            out NavMeshHit landingNavMeshHit,
                            dropRecoveryLandingSampleRadius,
                            agent.areaMask)
                        || Mathf.Abs(landingNavMeshHit.position.y - surfaceHit.point.y) > dropRecoveryLandingSampleRadius
                        || !IsDropWithinRecoveryRange(origin, landingNavMeshHit.position)
                        || !IsRecoveryPositionClear(landingNavMeshHit.position)
                        || !IsDropTrajectoryClear(origin, landingNavMeshHit.position))
                    {
                        continue;
                    }

                    float routeDistance;
                    if (hasGoalOnNavMesh)
                    {
                        if (!NavMesh.CalculatePath(
                                landingNavMeshHit.position,
                                goalNavMeshHit.position,
                                agent.areaMask,
                                recoveryPath)
                            || recoveryPath.status != NavMeshPathStatus.PathComplete)
                        {
                            continue;
                        }

                        routeDistance = GetPathDistance(recoveryPath);
                    }
                    else
                    {
                        float landingGoalDistance = Vector3.Distance(landingNavMeshHit.position, goal);
                        if (landingGoalDistance >= currentGoalDistance - minimumProgressPerAttempt)
                        {
                            continue;
                        }

                        routeDistance = landingGoalDistance;
                    }

                    float score = routeDistance + distance * 0.35f + alignmentPenalty;
                    if (score >= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    landingPoint = landingNavMeshHit.position;
                    found = true;
                }
            }

            return found;
        }

        private bool IsDropWithinRecoveryRange(Vector3 start, Vector3 landing)
        {
            float dropHeight = start.y - landing.y;
            Vector3 flatOffset = landing - start;
            flatOffset.y = 0f;
            return dropHeight >= targetDropMinimumHeight
                && dropHeight <= targetDropMaximumHeight
                && flatOffset.sqrMagnitude <= dropRecoveryHorizontalReach * dropRecoveryHorizontalReach;
        }

        private bool IsDropTrajectoryClear(Vector3 start, Vector3 landing)
        {
            float bodyHeight = agent != null ? agent.height : standingAgentHeight;
            float bodyRadius = agent != null ? agent.radius : 0.5f;
            float centerHeight = Mathf.Max(bodyRadius + 0.1f, bodyHeight * 0.5f);
            Vector3 startCenter = start + Vector3.up * centerHeight;
            Vector3 endCenter = landing + Vector3.up * centerHeight;
            Vector3 trajectory = endCenter - startCenter;
            float distance = trajectory.magnitude;
            if (distance <= 0.05f)
            {
                return false;
            }

            int hitCount = Physics.SphereCastNonAlloc(
                startCenter,
                Mathf.Max(0.05f, bodyRadius * 0.65f),
                trajectory / distance,
                climbProbeHits,
                distance,
                climbMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = climbProbeHits[i];
                if (hit.collider != null
                    && !hit.transform.IsChildOf(transform)
                    && !transform.IsChildOf(hit.transform))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryFindDropLanding(Vector3 targetPosition, out Vector3 landingPoint)
        {
            if (NavMesh.SamplePosition(targetPosition, out NavMeshHit navMeshHit, dropLandingSampleRadius, agent.areaMask)
                && IsDropWithinRecoveryRange(transform.position, navMeshHit.position)
                && IsRecoveryPositionClear(navMeshHit.position)
                && IsDropTrajectoryClear(transform.position, navMeshHit.position))
            {
                landingPoint = navMeshHit.position;
                return true;
            }

            landingPoint = targetPosition;
            return false;
        }

        private IEnumerator ClimbLedge(
            Vector3 target,
            Collider climbSurface,
            float duration,
            float arcHeight)
        {
            Vector3 start = transform.position;
            Transform climbAnchor = GetClimbAnchor(climbSurface);
            Vector3 localTarget = climbAnchor != null ? climbAnchor.InverseTransformPoint(target) : default;
            float timer = 0f;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }

            BeginClimbSurfaceCollisionIgnore(climbSurface);
            agent.updatePosition = false;
            agent.updateRotation = false;
            traversalAnimationPhase = TraversalAnimationPhase.Climb;

            float moveDuration = Mathf.Max(0.01f, duration);
            while (timer < moveDuration)
            {
                Vector3 previousPosition = transform.position;
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / moveDuration));
                float arc = Mathf.Sin(t * Mathf.PI) * arcHeight;
                Vector3 currentTarget = climbAnchor != null
                    ? climbAnchor.TransformPoint(localTarget)
                    : target;
                Vector3 nextPosition = Vector3.Lerp(start, currentTarget, t) + Vector3.up * arc;
                BreakGlassAlongTraversal(previousPosition, nextPosition);
                transform.position = nextPosition;
                FaceTowards(currentTarget, 16f);
                yield return null;
            }

            target = climbAnchor != null ? climbAnchor.TransformPoint(localTarget) : target;
            BreakGlassAlongTraversal(transform.position, target);
            transform.position = target;

            bool warpedToNavMesh = false;
            if (NavMesh.SamplePosition(target, out NavMeshHit navMeshHit, startNavMeshSnapRadius, agent.areaMask)
                && Mathf.Abs(navMeshHit.position.y - target.y) <= postClimbNavMeshVerticalSnapTolerance)
            {
                warpedToNavMesh = agent.Warp(navMeshHit.position);
            }

            if (!warpedToNavMesh)
            {
                agent.nextPosition = transform.position;
                agent.updatePosition = false;
                agent.updateRotation = false;
                offMeshChaseUntilTime = Time.time + postClimbOffMeshChaseTime;
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

            SetClimbSurfaceCollisionIgnored(false);
            yield return PlayLandingReaction();
            FinishTraversal();
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
                Vector3 previousPosition = transform.position;
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / moveDuration);
                if (t >= traversalStartPortion)
                {
                    traversalAnimationPhase = TraversalAnimationPhase.Loop;
                }

                float arc = Mathf.Sin(t * Mathf.PI) * arcHeight;
                Vector3 nextPosition = Vector3.Lerp(start, target, t) + Vector3.up * arc;
                BreakGlassAlongTraversal(previousPosition, nextPosition);
                transform.position = nextPosition;
                FaceTowards(target, 18f);
                yield return null;
            }

            BreakGlassAlongTraversal(transform.position, target);
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
            ResumeNavigationGoalAfterTraversal();
            FinishTraversal();
        }

        private void BreakGlassAlongTraversal(Vector3 start, Vector3 end)
        {
            Vector3 movement = end - start;
            float distance = movement.magnitude;
            if (distance <= 0.001f || traversalGlassBreakRadius <= 0f)
            {
                return;
            }

            int hitCount = Physics.SphereCastNonAlloc(
                start,
                traversalGlassBreakRadius,
                movement / distance,
                traversalGlassHits,
                distance,
                traversalGlassBreakMask,
                QueryTriggerInteraction.Collide);
            Vector3 incomingVelocity = movement / Mathf.Max(Time.deltaTime, 0.001f);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = traversalGlassHits[i];
                if (hit.collider == null
                    || hit.transform.IsChildOf(transform)
                    || transform.IsChildOf(hit.transform))
                {
                    continue;
                }

                GlassShatter glass = hit.collider.GetComponentInParent<GlassShatter>();
                glass?.ShatterFromNeighbor(hit.point, incomingVelocity, brain);
            }
        }

        private void ResumeNavigationGoalAfterTraversal()
        {
            if (!hasNavigationGoal
                || agent == null
                || !agent.enabled
                || !agent.isOnNavMesh
                || IsOffMeshChasing)
            {
                return;
            }

            Vector3 goal = navigationGoal;
            TrySetDestination(goal, destinationSampleRadius, out _);
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
