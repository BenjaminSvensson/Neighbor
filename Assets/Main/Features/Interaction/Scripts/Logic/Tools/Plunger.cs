using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Pickupable))]
    public sealed class Plunger : MonoBehaviour, IPrimaryUseInteractable, IInteractionTooltipProvider, IPickupLifecycleReceiver
    {
        private readonly struct PullSurfaceHit
        {
            public PullSurfaceHit(Vector3 point, Vector3 normal, float distance)
            {
                Point = point;
                Normal = normal;
                Distance = distance;
            }

            public Vector3 Point { get; }
            public Vector3 Normal { get; }
            public float Distance { get; }
        }

        [Header("Pull")]
        [SerializeField, Min(0.1f)] private float pullRange = 4f;
        [SerializeField, Min(0f)] private float pullProbeRadius = 0.08f;
        [SerializeField, Min(0f)] private float useCooldown = 0.45f;
        [SerializeField, Min(0f)] private float maximumPullMass = 3f;
        [SerializeField, Min(0f)] private float pullForce = 18f;
        [SerializeField, Min(0f)] private float pullDamping = 7f;
        [SerializeField, Min(0f)] private float maximumPullAcceleration = 45f;
        [SerializeField, Min(0.01f)] private float pullDuration = 0.65f;
        [SerializeField, Min(0f)] private float pullContactRadius = 0.18f;
        [SerializeField, Min(0f)] private float pullStrokeDistance = 0.85f;
        [SerializeField] private LayerMask pullMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Pull Feedback")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Vector3 pullMotionLocalOffset = new(0f, 0f, -0.18f);
        [SerializeField, Min(0f)] private float pullMotionFrequency = 7f;

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
        [SerializeField, Range(0f, 1f)] private float pullVolume = 0.55f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.06f;

        private readonly Collider[] pullContactHits = new Collider[16];
        private Pickupable pickupable;
        private Rigidbody ownBody;
        private Collider[] ownColliders;
        private bool[] ownColliderEnabledBeforeSuppression;
        private Rigidbody activeBody;
        private Pickupable stuckTargetPickupable;
        private Transform stuckTransform;
        private Collider[] stuckTargetColliders;
        private AudioClip generatedPullClip;
        private Vector3 visualRestLocalPosition;
        private Vector3 activePullLocalPoint;
        private Vector3 activePullLocalNormal;
        private Vector3 previousPullCupPoint;
        private Vector3 stuckLocalPosition;
        private Quaternion stuckLocalRotation;
        private RigidbodyConstraints unstuckConstraints;
        private float activePullStartTime;
        private float activePullEndTime;
        private float nextUseTime;
        private bool isStuck;
        private bool hasPreviousPullCupPoint;
        private bool areOwnCollidersSuppressed;

        private void Awake()
        {
            pickupable = GetComponent<Pickupable>();
            ownBody = GetComponent<Rigidbody>();
            ownColliders = GetComponentsInChildren<Collider>();
            ownColliderEnabledBeforeSuppression = new bool[ownColliders.Length];
            if (visualRoot == null && transform.childCount > 0)
            {
                visualRoot = transform.GetChild(0);
            }

            if (visualRoot != null)
            {
                visualRestLocalPosition = visualRoot.localPosition;
            }

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

            if (activeBody == null)
            {
                return;
            }

            if (Time.time >= activePullEndTime || activeBody.isKinematic)
            {
                ClearActivePull();
                return;
            }

            ApplyActivePull();
        }

        public bool CanPrimaryUse(PlayerInteractor interactor)
        {
            return pickupable != null && pickupable.IsHeld && Time.time >= nextUseTime;
        }

        public void PrimaryUse(PlayerInteractor interactor)
        {
            if (!CanPrimaryUse(interactor))
            {
                return;
            }

            nextUseTime = Time.time + useCooldown;
            if (!TryFindPullTarget(interactor, out Rigidbody targetBody, out PullSurfaceHit targetHit))
            {
                return;
            }

            activeBody = targetBody;
            activePullLocalPoint = activeBody.transform.InverseTransformPoint(targetHit.Point);
            activePullLocalNormal = activeBody.transform.InverseTransformDirection(targetHit.Normal).normalized;
            activePullStartTime = Time.time;
            activePullEndTime = Time.time + pullDuration;
            previousPullCupPoint = GetCupContactPoint();
            hasPreviousPullCupPoint = true;
            PlayPullSound();
        }

        public void OnPickupStarted(Pickupable pickupable, PlayerInteractor interactor)
        {
            Unstick();
            ResetPullMotion();
        }

        public void OnPickupPlaced(Pickupable pickupable)
        {
            ResetPullMotion();
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

        private bool TryFindPullTarget(PlayerInteractor interactor, out Rigidbody targetBody, out PullSurfaceHit targetHit)
        {
            targetBody = null;
            targetHit = default;
            Vector3 cupPoint = GetCupContactPoint();
            Vector3 cupDirection = GetCupDirection();
            float contactRadius = Mathf.Max(pullProbeRadius, pullContactRadius);
            int hitCount = Physics.OverlapSphereNonAlloc(
                cupPoint,
                contactRadius,
                pullContactHits,
                pullMask,
                triggerInteraction);

            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = pullContactHits[i];
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                Rigidbody body = hitCollider.attachedRigidbody;
                Pickupable targetPickupable = hitCollider.GetComponentInParent<Pickupable>();
                if (targetPickupable != null)
                {
                    body = targetPickupable.GetComponent<Rigidbody>();
                }

                if (!CanPull(body, targetPickupable, hitCollider)
                    || !TryGetCupSurfaceHit(hitCollider, cupPoint, cupDirection, contactRadius, out PullSurfaceHit hit)
                    || hit.Distance >= bestDistance)
                {
                    continue;
                }

                targetBody = body;
                targetHit = hit;
                bestDistance = hit.Distance;
            }

            return targetBody != null;
        }

        private bool TryGetCupSurfaceHit(
            Collider targetCollider,
            Vector3 cupPoint,
            Vector3 cupDirection,
            float contactRadius,
            out PullSurfaceHit hit)
        {
            hit = default;
            Vector3 rayOrigin = cupPoint - cupDirection * contactRadius;
            if (targetCollider.Raycast(new Ray(rayOrigin, cupDirection), out RaycastHit rayHit, contactRadius * 2f))
            {
                hit = new PullSurfaceHit(rayHit.point, rayHit.normal, rayHit.distance);
                return Vector3.Dot(cupDirection, -hit.Normal) >= 0.25f;
            }

            Vector3 closestPoint = targetCollider.ClosestPoint(cupPoint);
            Vector3 toCup = cupPoint - closestPoint;
            if (toCup.sqrMagnitude > contactRadius * contactRadius)
            {
                return false;
            }

            Vector3 normal = toCup.sqrMagnitude > 0.0001f ? toCup.normalized : -cupDirection;
            if (Vector3.Dot(cupDirection, -normal) < 0.25f)
            {
                return false;
            }

            hit = new PullSurfaceHit(closestPoint, normal, toCup.magnitude);
            return true;
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

        private void ApplyActivePull()
        {
            Vector3 surfacePoint = activeBody.transform.TransformPoint(activePullLocalPoint);
            Vector3 surfaceNormal = activeBody.transform.TransformDirection(activePullLocalNormal).normalized;
            Vector3 cupPoint = GetActivePullCupPoint();
            Vector3 desiredSurfacePoint = cupPoint - surfaceNormal * stickSurfaceOffset;
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
            return stickContactPoint != null
                ? stickContactPoint.position
                : transform.TransformPoint(localStickContactPoint);
        }

        private Vector3 GetActivePullCupPoint()
        {
            float pull01 = Mathf.InverseLerp(activePullStartTime, activePullEndTime, Time.time);
            float stroke = Mathf.SmoothStep(0f, pullStrokeDistance, pull01);
            return GetCupContactPoint() - GetCupDirection() * stroke;
        }

        private Vector3 GetCupDirection()
        {
            Vector3 localDirection = localCupDirection.sqrMagnitude > 0.0001f ? localCupDirection.normalized : Vector3.forward;
            return transform.TransformDirection(localDirection);
        }

        private void ClearActivePull()
        {
            activeBody = null;
            hasPreviousPullCupPoint = false;
            ResetPullMotion();
        }

        private void UpdatePullMotion()
        {
            if (visualRoot == null)
            {
                return;
            }

            if (activeBody == null || pickupable == null || !pickupable.IsHeld)
            {
                ResetPullMotion();
                return;
            }

            float pullTime01 = Mathf.InverseLerp(activePullEndTime - pullDuration, activePullEndTime, Time.time);
            float pulse = Mathf.Sin(pullTime01 * pullMotionFrequency * Mathf.PI);
            visualRoot.localPosition = visualRestLocalPosition + pullMotionLocalOffset * Mathf.Clamp01(Mathf.Abs(pulse));
        }

        private void ResetPullMotion()
        {
            if (visualRoot != null)
            {
                visualRoot.localPosition = visualRestLocalPosition;
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
            Vector3 localContactPoint = stickContactPoint != null
                ? transform.InverseTransformPoint(stickContactPoint.position)
                : localStickContactPoint;
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

        private void PlayPullSound()
        {
            if (audioSource == null)
            {
                return;
            }

            AudioClip clip = GetPullClip();
            if (clip == null)
            {
                return;
            }

            audioSource.pitch = Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
            audioSource.PlayOneShot(clip, pullVolume);
        }

        private AudioClip GetPullClip()
        {
            if (pullClips != null && pullClips.Length > 0)
            {
                return pullClips[Random.Range(0, pullClips.Length)];
            }

            if (generatedPullClip == null)
            {
                generatedPullClip = CreateGeneratedPullClip();
            }

            return generatedPullClip;
        }

        private AudioClip CreateGeneratedPullClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.22f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Clamp01(1f - time / duration);
                float pop = Mathf.Sin(2f * Mathf.PI * 130f * time) * 0.28f;
                float suction = Mathf.Sin(2f * Mathf.PI * 52f * time) * 0.16f;
                samples[i] = (pop + suction) * envelope;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedPlungerPull", sampleCount, 1, sampleRate, false);
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
            pullStrokeDistance = Mathf.Max(0f, pullStrokeDistance);
            pullMotionFrequency = Mathf.Max(0f, pullMotionFrequency);
            minimumStickSpeed = Mathf.Max(0f, minimumStickSpeed);
            stickSurfaceOffset = Mathf.Max(0f, stickSurfaceOffset);
        }
    }
}
