using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Pickupable))]
    public sealed class Plunger : MonoBehaviour, IPrimaryUseInteractable, IInteractionTooltipProvider, IPickupLifecycleReceiver
    {
        private readonly struct PullSurfaceHit
        {
            public PullSurfaceHit(
                Rigidbody body,
                Pickupable pickupable,
                Collider collider,
                Vector3 point,
                Vector3 normal,
                float distance)
            {
                Body = body;
                TargetPickupable = pickupable;
                Collider = collider;
                Point = point;
                Normal = normal;
                Distance = distance;
            }

            public Rigidbody Body { get; }
            public Pickupable TargetPickupable { get; }
            public Collider Collider { get; }
            public Vector3 Point { get; }
            public Vector3 Normal { get; }
            public float Distance { get; }
        }

        private enum PullState
        {
            Idle,
            Extending,
            Retracting
        }

        [Header("Pull")]
        [SerializeField, Min(0.1f)] private float pullRange = 6f;
        [SerializeField, Min(0f)] private float pullProbeRadius = 0.08f;
        [SerializeField, Min(0f)] private float useCooldown = 0.45f;
        [SerializeField, Min(0f)] private float maximumPullMass = 3f;
        [SerializeField, Min(0f)] private float pullForce = 18f;
        [SerializeField, Min(0f)] private float pullDamping = 7f;
        [SerializeField, Min(0f)] private float maximumPullAcceleration = 45f;
        [SerializeField, Min(0.01f)] private float pullDuration = 2f;
        [SerializeField, Min(0f)] private float pullContactRadius = 0.18f;
        [SerializeField, Min(0.01f)] private float pullExtendSpeed = 12f;
        [SerializeField, Min(0.01f)] private float pullRetractSpeed = 8f;
        [SerializeField, Min(0f)] private float pullSurfaceClearance = 0.04f;
        [SerializeField] private LayerMask pullMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Pull Feedback")]
        [SerializeField] private Transform visualRoot;

        [Header("Surface Stick")]
        [SerializeField] private bool stickToSurfacesWhenThrown = true;
        [SerializeField, Min(0f)] private float minimumStickSpeed = 2.2f;
        [SerializeField, Min(0f)] private float stickSurfaceOffset = 0.035f;
        [SerializeField] private Vector3 localCupDirection = Vector3.forward;
        [SerializeField] private Transform stickContactPoint;
        [SerializeField] private Vector3 localStickContactPoint = new(0f, 0f, 0.64f);
        [SerializeField] private LayerMask stickMask = ~0;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] pullClips;
        [SerializeField] private AudioClip[] attachClips;
        [SerializeField] private AudioClip[] detachClips;
        [SerializeField, Range(0f, 1f)] private float pullVolume = 0.55f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.06f;

        private readonly Collider[] pullContactHits = new Collider[16];
        private readonly RaycastHit[] pullSweepHits = new RaycastHit[16];
        private Pickupable pickupable;
        private Rigidbody ownBody;
        private Collider[] ownColliders;
        private bool[] ownColliderEnabledBeforeSuppression;
        private Rigidbody activeBody;
        private Pickupable stuckTargetPickupable;
        private Transform stuckTransform;
        private Collider[] stuckTargetColliders;
        private AudioClip generatedAttachClip;
        private AudioClip generatedDetachClip;
        private Transform[] pullMotionRoots;
        private Vector3[] pullMotionRestLocalPositions;
        private Vector3 restLocalStickContactPoint;
        private Vector3 activePullLocalPoint;
        private Vector3 activePullLocalNormal;
        private Vector3 previousPullCupPoint;
        private Vector3 stuckLocalPosition;
        private Quaternion stuckLocalRotation;
        private RigidbodyConstraints unstuckConstraints;
        private PullState pullState;
        private float activePullExtension;
        private float activePullEndTime;
        private float nextUseTime;
        private bool isStuck;
        private bool hasPreviousPullCupPoint;
        private bool hasAttachedPullTarget;
        private bool areOwnCollidersSuppressed;

        private void Awake()
        {
            pickupable = GetComponent<Pickupable>();
            ownBody = GetComponent<Rigidbody>();
            ownColliders = GetComponentsInChildren<Collider>();
            ownColliderEnabledBeforeSuppression = new bool[ownColliders.Length];
            restLocalStickContactPoint = stickContactPoint != null
                ? transform.InverseTransformPoint(stickContactPoint.position)
                : localStickContactPoint;
            CachePullMotionRoots();

            ResolveAudioSource();
        }

        private void Update()
        {
            UpdatePullMotion();
        }

        private void LateUpdate()
        {
            UpdateStuckPose();
        }

        private void OnDisable()
        {
            ClearActivePull();
            RestoreOwnCollidersAfterSuppression();
            SetStuckTargetCollisionIgnored(false);
        }

        private void FixedUpdate()
        {
            UpdateStuckPose();

            if (pullState == PullState.Idle)
            {
                return;
            }

            if (Time.time >= activePullEndTime)
            {
                FinishPullCycle();
                return;
            }

            if (pullState == PullState.Extending)
            {
                UpdatePullExtension();
                return;
            }

            UpdatePullRetraction();
        }

        public bool CanPrimaryUse(PlayerInteractor interactor)
        {
            return pickupable != null && pickupable.IsHeld && pullState == PullState.Idle && Time.time >= nextUseTime;
        }

        public void PrimaryUse(PlayerInteractor interactor)
        {
            if (!CanPrimaryUse(interactor))
            {
                return;
            }

            nextUseTime = Time.time + useCooldown;
            activePullExtension = 0f;
            activePullEndTime = Time.time + pullDuration;
            pullState = PullState.Extending;
            ResetPullAttachment();
        }

        public void OnPickupStarted(Pickupable pickupable, PlayerInteractor interactor)
        {
            ClearActivePull();
            Unstick();
        }

        public void OnPickupPlaced(Pickupable pickupable)
        {
            ClearActivePull();
        }

        public bool TryGetInteractionTooltip(
            PlayerInteractor interactor,
            InteractionTooltipContext context,
            out string actionText,
            out string keyText)
        {
            actionText = null;
            keyText = null;

            if (context != InteractionTooltipContext.HeldPrimaryUse)
            {
                return false;
            }

            actionText = "Pull Object";
            keyText = "Left Mouse";
            return true;
        }

        private bool TryFindPullSurface(float maxDistance, out PullSurfaceHit surfaceHit)
        {
            surfaceHit = default;
            Vector3 cupBasePoint = GetBaseCupContactPoint();
            Vector3 cupDirection = GetCupDirection();
            float contactRadius = Mathf.Max(pullProbeRadius, pullContactRadius);
            float bestDistance = float.PositiveInfinity;

            int overlapCount = Physics.OverlapSphereNonAlloc(
                cupBasePoint,
                contactRadius,
                pullContactHits,
                pullMask,
                triggerInteraction);

            for (int i = 0; i < overlapCount; i++)
            {
                Collider hitCollider = pullContactHits[i];
                if (ShouldIgnorePullCollider(hitCollider))
                {
                    continue;
                }

                Vector3 closestPoint = hitCollider.ClosestPoint(cupBasePoint);
                Vector3 normal = cupBasePoint - closestPoint;
                if (normal.sqrMagnitude <= 0.0001f)
                {
                    normal = -cupDirection;
                }
                else
                {
                    normal.Normalize();
                }

                surfaceHit = CreatePullSurfaceHit(hitCollider, closestPoint, normal, 0f);
                return true;
            }

            int sweepCount = Physics.SphereCastNonAlloc(
                cupBasePoint,
                contactRadius,
                cupDirection,
                pullSweepHits,
                maxDistance,
                pullMask,
                triggerInteraction);

            for (int i = 0; i < sweepCount; i++)
            {
                RaycastHit hit = pullSweepHits[i];
                if (ShouldIgnorePullCollider(hit.collider) || hit.distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = hit.distance;
                surfaceHit = CreatePullSurfaceHit(hit.collider, hit.point, hit.normal, hit.distance);
            }

            return bestDistance < float.PositiveInfinity;
        }

        private PullSurfaceHit CreatePullSurfaceHit(Collider hitCollider, Vector3 point, Vector3 normal, float distance)
        {
            Rigidbody body = hitCollider.attachedRigidbody;
            Pickupable targetPickupable = hitCollider.GetComponentInParent<Pickupable>();
            if (targetPickupable != null)
            {
                body = targetPickupable.GetComponent<Rigidbody>();
            }

            return new PullSurfaceHit(body, targetPickupable, hitCollider, point, normal.normalized, distance);
        }

        private bool ShouldIgnorePullCollider(Collider hitCollider)
        {
            return hitCollider == null
                || hitCollider.transform.IsChildOf(transform)
                || hitCollider.GetComponentInParent<PlayerInteractor>() != null;
        }

        private bool CanPull(Rigidbody body, Pickupable targetPickupable, Collider hitCollider)
        {
            if (body == null || body.isKinematic || body == ownBody)
            {
                return false;
            }

            if (targetPickupable == pickupable || (targetPickupable != null && targetPickupable.IsHeld))
            {
                return false;
            }

            PlungerPullTarget pullTarget = hitCollider.GetComponentInParent<PlungerPullTarget>();
            if (pullTarget != null)
            {
                return pullTarget.AllowPull
                    && (pullTarget.AllowHeavyPull || body.mass <= Mathf.Max(maximumPullMass, pullTarget.MaximumPullMass));
            }

            return true;
        }

        private void UpdatePullExtension()
        {
            float nextExtension = Mathf.MoveTowards(activePullExtension, pullRange, pullExtendSpeed * Time.fixedDeltaTime);
            if (TryFindPullSurface(nextExtension, out PullSurfaceHit surfaceHit))
            {
                activePullExtension = Mathf.Clamp(surfaceHit.Distance - pullSurfaceClearance, 0f, pullRange);
                if (CanPull(surfaceHit.Body, surfaceHit.TargetPickupable, surfaceHit.Collider))
                {
                    AttachPullTarget(surfaceHit);
                }

                BeginPullRetraction();
                return;
            }

            activePullExtension = nextExtension;
            if (Mathf.Approximately(activePullExtension, pullRange))
            {
                BeginPullRetraction();
            }
        }

        private void AttachPullTarget(PullSurfaceHit surfaceHit)
        {
            activeBody = surfaceHit.Body;
            activePullLocalPoint = activeBody.transform.InverseTransformPoint(surfaceHit.Point);
            activePullLocalNormal = activeBody.transform.InverseTransformDirection(surfaceHit.Normal).normalized;
            previousPullCupPoint = GetCupContactPoint();
            hasPreviousPullCupPoint = true;
            hasAttachedPullTarget = true;
            PlayAttachSound();
        }

        private void BeginPullRetraction()
        {
            pullState = PullState.Retracting;
        }

        private void UpdatePullRetraction()
        {
            if (activeBody != null && !activeBody.isKinematic)
            {
                ApplyActivePull();
            }

            activePullExtension = Mathf.MoveTowards(activePullExtension, 0f, pullRetractSpeed * Time.fixedDeltaTime);
            if (activePullExtension <= 0.0001f)
            {
                FinishPullCycle();
            }
        }

        private void ApplyActivePull()
        {
            if (activeBody == null)
            {
                return;
            }

            Vector3 surfacePoint = activeBody.transform.TransformPoint(activePullLocalPoint);
            Vector3 surfaceNormal = activeBody.transform.TransformDirection(activePullLocalNormal).normalized;
            Vector3 cupPoint = GetCupContactPoint();
            Vector3 desiredSurfacePoint = cupPoint - surfaceNormal * Mathf.Max(stickSurfaceOffset, pullSurfaceClearance);
            Vector3 anchorError = desiredSurfacePoint - surfacePoint;
            if (anchorError.sqrMagnitude <= 0.0001f)
            {
                previousPullCupPoint = cupPoint;
                hasPreviousPullCupPoint = true;
                return;
            }

            Vector3 cupVelocity = Vector3.zero;
            if (hasPreviousPullCupPoint && Time.fixedDeltaTime > 0f)
            {
                cupVelocity = (cupPoint - previousPullCupPoint) / Time.fixedDeltaTime;
            }

            Vector3 pointVelocity = activeBody.GetPointVelocity(surfacePoint);
            Vector3 relativeVelocity = pointVelocity - cupVelocity;
            Vector3 acceleration = anchorError * pullForce - relativeVelocity * pullDamping;
            if (maximumPullAcceleration > 0f)
            {
                acceleration = Vector3.ClampMagnitude(acceleration, maximumPullAcceleration);
            }

            activeBody.WakeUp();
            activeBody.AddForceAtPosition(acceleration, surfacePoint, ForceMode.Acceleration);
            previousPullCupPoint = cupPoint;
            hasPreviousPullCupPoint = true;
        }

        private Vector3 GetCupContactPoint()
        {
            return GetBaseCupContactPoint() + GetCupDirection() * activePullExtension;
        }

        private Vector3 GetBaseCupContactPoint()
        {
            return transform.TransformPoint(restLocalStickContactPoint);
        }

        private Vector3 GetCupDirection()
        {
            return transform.TransformDirection(GetLocalCupDirection());
        }

        private Vector3 GetLocalCupDirection()
        {
            return localCupDirection.sqrMagnitude > 0.0001f ? localCupDirection.normalized : Vector3.forward;
        }

        private void ClearActivePull()
        {
            ResetPullAttachment();
            activePullExtension = 0f;
            pullState = PullState.Idle;
            ResetPullMotion();
        }

        private void FinishPullCycle()
        {
            if (hasAttachedPullTarget)
            {
                PlayDetachSound();
            }

            ClearActivePull();
        }

        private void ResetPullAttachment()
        {
            activeBody = null;
            hasPreviousPullCupPoint = false;
            hasAttachedPullTarget = false;
        }

        private void CachePullMotionRoots()
        {
            if (transform.childCount > 0)
            {
                pullMotionRoots = new Transform[transform.childCount];
                pullMotionRestLocalPositions = new Vector3[transform.childCount];
                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform child = transform.GetChild(i);
                    pullMotionRoots[i] = child;
                    pullMotionRestLocalPositions[i] = child.localPosition;
                }

                return;
            }

            if (visualRoot == null)
            {
                return;
            }

            pullMotionRoots = new[] { visualRoot };
            pullMotionRestLocalPositions = new[] { visualRoot.localPosition };
        }

        private void UpdatePullMotion()
        {
            if (pullMotionRoots == null || pullMotionRestLocalPositions == null)
            {
                return;
            }

            Vector3 localOffset = pullState == PullState.Idle || pickupable == null || !pickupable.IsHeld
                ? Vector3.zero
                : GetLocalCupDirection() * activePullExtension;

            for (int i = 0; i < pullMotionRoots.Length; i++)
            {
                Transform pullMotionRoot = pullMotionRoots[i];
                if (pullMotionRoot == null)
                {
                    continue;
                }

                pullMotionRoot.localPosition = pullMotionRestLocalPositions[i] + localOffset;
            }
        }

        private void ResetPullMotion()
        {
            if (pullMotionRoots == null || pullMotionRestLocalPositions == null)
            {
                return;
            }

            for (int i = 0; i < pullMotionRoots.Length; i++)
            {
                Transform pullMotionRoot = pullMotionRoots[i];
                if (pullMotionRoot == null)
                {
                    continue;
                }

                pullMotionRoot.localPosition = pullMotionRestLocalPositions[i];
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryStickToSurface(collision);
        }

        private void TryStickToSurface(Collision collision)
        {
            if (!stickToSurfacesWhenThrown || isStuck || collision == null || pickupable == null || pickupable.IsHeld || ownBody == null)
            {
                return;
            }

            if (collision.relativeVelocity.magnitude < minimumStickSpeed || collision.contactCount == 0)
            {
                return;
            }

            ContactPoint contact = collision.GetContact(0);
            Collider targetCollider = contact.otherCollider != null ? contact.otherCollider : collision.collider;
            if (targetCollider == null || targetCollider.transform.IsChildOf(transform) || ((1 << targetCollider.gameObject.layer) & stickMask.value) == 0)
            {
                return;
            }

            Vector3 cupDirection = GetCupDirection();
            if (Vector3.Dot(cupDirection, -contact.normal) < 0.35f)
            {
                return;
            }

            Quaternion stuckRotation = Quaternion.FromToRotation(cupDirection, -contact.normal) * transform.rotation;
            Vector3 localContactPoint = restLocalStickContactPoint;
            Vector3 stuckPosition = contact.point + contact.normal * stickSurfaceOffset - stuckRotation * localContactPoint;
            transform.SetPositionAndRotation(stuckPosition, stuckRotation);
            ownBody.linearVelocity = Vector3.zero;
            ownBody.angularVelocity = Vector3.zero;
            ownBody.useGravity = false;
            ownBody.isKinematic = true;
            unstuckConstraints = ownBody.constraints;
            ownBody.constraints = RigidbodyConstraints.FreezeAll;
            isStuck = true;

            Rigidbody targetBody = targetCollider.attachedRigidbody;
            stuckTargetPickupable = targetCollider.GetComponentInParent<Pickupable>();
            stuckTransform = targetBody != null ? targetBody.transform : targetCollider.transform;
            if (stuckTransform != null && targetBody != ownBody)
            {
                stuckLocalPosition = stuckTransform.InverseTransformPoint(transform.position);
                stuckLocalRotation = Quaternion.Inverse(stuckTransform.rotation) * transform.rotation;
                CacheAndIgnoreStuckTargetCollisions(stuckTransform);
            }
        }

        private void Unstick()
        {
            if (!isStuck || ownBody == null)
            {
                return;
            }

            ownBody.constraints = unstuckConstraints;
            ownBody.isKinematic = false;
            ownBody.useGravity = true;
            SetStuckTargetCollisionIgnored(false);
            RestoreOwnCollidersAfterSuppression();
            stuckTargetColliders = null;
            stuckTargetPickupable = null;
            stuckTransform = null;
            isStuck = false;
        }

        private void UpdateStuckPose()
        {
            if (!isStuck)
            {
                return;
            }

            if (stuckTransform == null)
            {
                Unstick();
                return;
            }

            transform.SetPositionAndRotation(
                stuckTransform.TransformPoint(stuckLocalPosition),
                stuckTransform.rotation * stuckLocalRotation);
            UpdateStuckColliderSuppression();
        }

        private void CacheAndIgnoreStuckTargetCollisions(Transform targetRoot)
        {
            SetStuckTargetCollisionIgnored(false);
            stuckTargetColliders = targetRoot.GetComponentsInChildren<Collider>();
            SetStuckTargetCollisionIgnored(true);
        }

        private void SetStuckTargetCollisionIgnored(bool ignore)
        {
            if (ownColliders == null || stuckTargetColliders == null)
            {
                return;
            }

            foreach (Collider ownCollider in ownColliders)
            {
                if (ownCollider == null)
                {
                    continue;
                }

                foreach (Collider targetCollider in stuckTargetColliders)
                {
                    if (targetCollider == null || targetCollider == ownCollider)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(ownCollider, targetCollider, ignore);
                }
            }
        }

        private void UpdateStuckColliderSuppression()
        {
            if (stuckTargetPickupable != null && stuckTargetPickupable.IsHeld)
            {
                SuppressOwnColliders();
                return;
            }

            RestoreOwnCollidersAfterSuppression();
        }

        private void SuppressOwnColliders()
        {
            if (areOwnCollidersSuppressed || ownColliders == null || ownColliderEnabledBeforeSuppression == null)
            {
                return;
            }

            for (int i = 0; i < ownColliders.Length; i++)
            {
                Collider ownCollider = ownColliders[i];
                if (ownCollider == null)
                {
                    continue;
                }

                ownColliderEnabledBeforeSuppression[i] = ownCollider.enabled;
                ownCollider.enabled = false;
            }

            areOwnCollidersSuppressed = true;
        }

        private void RestoreOwnCollidersAfterSuppression()
        {
            if (!areOwnCollidersSuppressed || ownColliders == null || ownColliderEnabledBeforeSuppression == null)
            {
                return;
            }

            for (int i = 0; i < ownColliders.Length; i++)
            {
                Collider ownCollider = ownColliders[i];
                if (ownCollider == null)
                {
                    continue;
                }

                ownCollider.enabled = ownColliderEnabledBeforeSuppression[i];
            }

            areOwnCollidersSuppressed = false;
        }

        private void PlayAttachSound()
        {
            PlayPlungerSound(GetAttachClip());
        }

        private void PlayDetachSound()
        {
            PlayPlungerSound(GetDetachClip());
        }

        private void PlayPlungerSound(AudioClip clip)
        {
            if (audioSource == null || clip == null)
            {
                return;
            }

            audioSource.pitch = Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
            audioSource.PlayOneShot(clip, pullVolume);
        }

        private AudioClip GetAttachClip()
        {
            AudioClip clip = GetRandomClip(attachClips);
            if (clip != null)
            {
                return clip;
            }

            clip = GetRandomClip(pullClips);
            if (clip != null)
            {
                return clip;
            }

            if (generatedAttachClip == null)
            {
                generatedAttachClip = CreateGeneratedPullClip(true);
            }

            return generatedAttachClip;
        }

        private AudioClip GetDetachClip()
        {
            AudioClip clip = GetRandomClip(detachClips);
            if (clip != null)
            {
                return clip;
            }

            if (generatedDetachClip == null)
            {
                generatedDetachClip = CreateGeneratedPullClip(false);
            }

            return generatedDetachClip;
        }

        private static AudioClip GetRandomClip(AudioClip[] clips)
        {
            return clips != null && clips.Length > 0 ? clips[Random.Range(0, clips.Length)] : null;
        }

        private AudioClip CreateGeneratedPullClip(bool attach)
        {
            const int sampleRate = 22050;
            float duration = attach ? 0.18f : 0.16f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Clamp01(1f - time / duration);
                float popFrequency = attach ? 95f : 180f;
                float suctionFrequency = attach ? 42f : 72f;
                float pop = Mathf.Sin(2f * Mathf.PI * popFrequency * time) * (attach ? 0.18f : 0.32f);
                float suction = Mathf.Sin(2f * Mathf.PI * suctionFrequency * time) * (attach ? 0.24f : 0.1f);
                samples[i] = (pop + suction) * envelope;
            }

            string clipName = attach ? $"{name}_GeneratedPlungerAttach" : $"{name}_GeneratedPlungerDetach";
            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void ResolveAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = 0.35f;
            audioSource.maxDistance = 8f;
            audioSource.dopplerLevel = 0.08f;
        }

        private void OnValidate()
        {
            pullRange = Mathf.Max(0.1f, pullRange);
            pullProbeRadius = Mathf.Max(0f, pullProbeRadius);
            useCooldown = Mathf.Max(0f, useCooldown);
            maximumPullMass = Mathf.Max(0f, maximumPullMass);
            pullForce = Mathf.Max(0f, pullForce);
            pullDamping = Mathf.Max(0f, pullDamping);
            maximumPullAcceleration = Mathf.Max(0f, maximumPullAcceleration);
            pullDuration = Mathf.Max(0.01f, pullDuration);
            pullContactRadius = Mathf.Max(0f, pullContactRadius);
            pullExtendSpeed = Mathf.Max(0.01f, pullExtendSpeed);
            pullRetractSpeed = Mathf.Max(0.01f, pullRetractSpeed);
            pullSurfaceClearance = Mathf.Max(0f, pullSurfaceClearance);
            minimumStickSpeed = Mathf.Max(0f, minimumStickSpeed);
            stickSurfaceOffset = Mathf.Max(0f, stickSurfaceOffset);
        }
    }
}
