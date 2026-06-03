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
            Run
        }

        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 2.4f;
        [SerializeField, Min(0f)] private float runSpeed = 5.8f;
        [SerializeField, Min(0f)] private float acceleration = 18f;
        [SerializeField, Min(0f)] private float angularSpeed = 540f;
        [SerializeField, Min(0f)] private float stoppingDistance = 0.45f;
        [SerializeField, Min(0f)] private float destinationSampleRadius = 1.5f;
        [SerializeField, Min(0f)] private float startNavMeshSnapRadius = 3f;

        [Header("Jump And Climb")]
        [SerializeField] private bool traverseOffMeshLinks = true;
        [SerializeField, Min(0.01f)] private float offMeshTraverseDuration = 0.34f;
        [SerializeField, Min(0f)] private float offMeshJumpHeight = 0.75f;
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

        public bool IsTraversingSpecialMove => traversalRoutine != null;
        public bool IsOffMeshChasing => Time.time < offMeshChaseUntilTime;
        public bool IsDetachedFromNavMesh => agent != null && (!agent.updatePosition || !agent.isOnNavMesh);
        public bool HasPath => agent != null && (agent.hasPath || agent.pathPending);
        public float RemainingDistance => agent == null || agent.pathPending ? float.PositiveInfinity : agent.remainingDistance;
        public bool HasArrived => agent != null
            && !agent.pathPending
            && agent.remainingDistance <= agent.stoppingDistance + 0.1f
            && (!agent.hasPath || agent.velocity.sqrMagnitude < 0.08f);

        private void Awake()
        {
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

            if (IsOffMeshChasing || !agent.updatePosition)
            {
                SnapToNavMeshIfNeeded();
            }

            if (traverseOffMeshLinks && agent.isOnOffMeshLink && traversalRoutine == null)
            {
                traversalRoutine = StartCoroutine(TraverseOffMeshLink());
                return;
            }

            if (enableLedgeClimb && traversalRoutine == null && hasRequestedDestination)
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

            agent.speed = mode == MoveMode.Run ? runSpeed : walkSpeed;
        }

        public bool SetDestination(Vector3 destination)
        {
            if (IsOffMeshChasing)
            {
                return false;
            }

            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                return false;
            }

            if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, destinationSampleRadius, agent.areaMask))
            {
                return false;
            }

            requestedDestination = hit.position;
            hasRequestedDestination = true;
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

            traversalRoutine = StartCoroutine(JumpToPoint(landingPoint, chaseDropDuration, chaseDropArcHeight));
            return true;
        }

        public bool TryUseClimbLink(NeighborClimbLink climbLink)
        {
            if (climbLink == null || traversalRoutine != null)
            {
                return false;
            }

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
            }

            knockbackRoutine = StartCoroutine(Knockback(direction.normalized, distance, duration));
        }

        public void MoveDirectlyToward(Vector3 target, float speed, float turnSharpness)
        {
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
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                return;
            }

            hasRequestedDestination = false;
            agent.ResetPath();
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
            agent.autoTraverseOffMeshLink = false;
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

            while (timer < offMeshTraverseDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / offMeshTraverseDuration);
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
            traversalRoutine = null;
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

            traversalRoutine = null;
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

            float moveDuration = Mathf.Max(0.01f, duration);
            while (timer < moveDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / moveDuration);
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

            traversalRoutine = null;
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
