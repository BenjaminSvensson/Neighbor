using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Pickupable))]
    public sealed class Plunger : MonoBehaviour, IPrimaryUseInteractable, IInteractionTooltipProvider, IPickupLifecycleReceiver
    {
        [Header("Pull")]
        [SerializeField, Min(0.1f)] private float pullRange = 4f;
        [SerializeField, Min(0f)] private float pullProbeRadius = 0.08f;
        [SerializeField, Min(0f)] private float useCooldown = 0.45f;
        [SerializeField, Min(0f)] private float maximumPullMass = 3f;
        [SerializeField, Min(0f)] private float pullForce = 18f;
        [SerializeField, Min(0f)] private float pullDamping = 7f;
        [SerializeField, Min(0f)] private float maximumPullAcceleration = 45f;
        [SerializeField, Min(0.01f)] private float pullDuration = 0.65f;
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

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private Pickupable pickupable;
        private Rigidbody ownBody;
        private Collider[] ownColliders;
        private Rigidbody activeBody;
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
        private float activePullEndTime;
        private float nextUseTime;
        private bool isStuck;
        private bool hasPreviousPullCupPoint;

        private void Awake()
        {
            pickupable = GetComponent<Pickupable>();
            ownBody = GetComponent<Rigidbody>();
            ownColliders = GetComponentsInChildren<Collider>();
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
            if (!TryFindPullTarget(interactor, out Rigidbody targetBody, out RaycastHit targetHit))
            {
                return;
            }

            activeBody = targetBody;
            activePullLocalPoint = activeBody.transform.InverseTransformPoint(targetHit.point);
            activePullLocalNormal = activeBody.transform.InverseTransformDirection(targetHit.normal).normalized;
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

        private bool TryFindPullTarget(PlayerInteractor interactor, out Rigidbody targetBody, out RaycastHit targetHit)
        {
            targetBody = null;
            targetHit = default;
            Transform viewTransform = interactor != null ? interactor.ViewTransform : transform;
            Ray ray = new(viewTransform.position, viewTransform.forward);
            int hitCount = pullProbeRadius > 0f
                ? Physics.SphereCastNonAlloc(ray, pullProbeRadius, hits, pullRange, pullMask, triggerInteraction)
                : Physics.RaycastNonAlloc(ray, hits, pullRange, pullMask, triggerInteraction);

            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.distance >= bestDistance || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                Rigidbody body = hit.rigidbody != null ? hit.rigidbody : hit.collider.attachedRigidbody;
                Pickupable targetPickupable = hit.collider.GetComponentInParent<Pickupable>();
                if (targetPickupable != null)
                {
                    body = targetPickupable.GetComponent<Rigidbody>();
                }

                if (!CanPull(body, targetPickupable, hit.collider))
                {
                    continue;
                }

                targetBody = body;
                targetHit = hit;
                bestDistance = hit.distance;
            }

            return targetBody != null;
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
            Vector3 cupPoint = GetCupContactPoint();
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

            Vector3 cupDirection = transform.TransformDirection(localCupDirection.sqrMagnitude > 0.0001f ? localCupDirection.normalized : Vector3.forward);
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
            stuckTargetColliders = null;
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
            pullMotionFrequency = Mathf.Max(0f, pullMotionFrequency);
            minimumStickSpeed = Mathf.Max(0f, minimumStickSpeed);
            stickSurfaceOffset = Mathf.Max(0f, stickSurfaceOffset);
        }
    }
}
