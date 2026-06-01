using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Pickupable))]
    public sealed class Plunger : MonoBehaviour, IPrimaryUseInteractable, IInteractionTooltipProvider
    {
        [Header("Pull")]
        [SerializeField, Min(0.1f)] private float pullRange = 4f;
        [SerializeField, Min(0f)] private float pullProbeRadius = 0.08f;
        [SerializeField, Min(0f)] private float useCooldown = 0.45f;
        [SerializeField, Min(0f)] private float maximumPullMass = 3f;
        [SerializeField, Min(0f)] private float pullForce = 18f;
        [SerializeField, Min(0.01f)] private float pullDuration = 0.65f;
        [SerializeField, Min(0f)] private float snapDelay = 0.28f;
        [SerializeField, Min(0f)] private float pullTargetDistance = 1.25f;
        [SerializeField] private bool snapAfterDelay = true;
        [SerializeField] private LayerMask pullMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] pullClips;
        [SerializeField, Range(0f, 1f)] private float pullVolume = 0.55f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.06f;

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private Pickupable pickupable;
        private Rigidbody activeBody;
        private Transform activeViewTransform;
        private AudioClip generatedPullClip;
        private float activePullEndTime;
        private float activeSnapTime;
        private float nextUseTime;
        private bool hasSnappedActivePull;

        private void Awake()
        {
            pickupable = GetComponent<Pickupable>();
            ResolveAudioSource();
        }

        private void FixedUpdate()
        {
            if (activeBody == null)
            {
                return;
            }

            if (Time.time >= activePullEndTime || activeBody.isKinematic)
            {
                ClearActivePull();
                return;
            }

            Vector3 targetPosition = GetPullTargetPosition();
            Vector3 toTarget = targetPosition - activeBody.worldCenterOfMass;
            if (toTarget.sqrMagnitude <= 0.01f)
            {
                ClearActivePull();
                return;
            }

            activeBody.WakeUp();
            activeBody.AddForce(toTarget.normalized * pullForce, ForceMode.Acceleration);

            if (snapAfterDelay && !hasSnappedActivePull && Time.time >= activeSnapTime)
            {
                hasSnappedActivePull = true;
                activeBody.position += toTarget * Mathf.Clamp01(Time.fixedDeltaTime * 14f);
            }
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
            if (!TryFindPullTarget(interactor, out Rigidbody targetBody))
            {
                return;
            }

            activeBody = targetBody;
            activeViewTransform = interactor != null ? interactor.ViewTransform : transform;
            activePullEndTime = Time.time + pullDuration;
            activeSnapTime = Time.time + snapDelay;
            hasSnappedActivePull = false;
            PlayPullSound();
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

        private bool TryFindPullTarget(PlayerInteractor interactor, out Rigidbody targetBody)
        {
            targetBody = null;
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
                bestDistance = hit.distance;
            }

            return targetBody != null;
        }

        private bool CanPull(Rigidbody body, Pickupable targetPickupable, Collider hitCollider)
        {
            if (body == null || body.isKinematic || body == GetComponent<Rigidbody>())
            {
                return false;
            }

            if (targetPickupable == pickupable || (targetPickupable != null && targetPickupable.IsHeld))
            {
                return false;
            }

            PlungerPullTarget pullTarget = hitCollider.GetComponentInParent<PlungerPullTarget>();
            if (pullTarget != null && pullTarget.AllowPull)
            {
                return pullTarget.AllowHeavyPull || body.mass <= Mathf.Max(maximumPullMass, pullTarget.MaximumPullMass);
            }

            return targetPickupable != null && body.mass <= maximumPullMass;
        }

        private Vector3 GetPullTargetPosition()
        {
            Transform viewTransform = activeViewTransform != null ? activeViewTransform : transform;
            return viewTransform.position + viewTransform.forward * pullTargetDistance;
        }

        private void ClearActivePull()
        {
            activeBody = null;
            activeViewTransform = null;
            hasSnappedActivePull = false;
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
            pullDuration = Mathf.Max(0.01f, pullDuration);
            snapDelay = Mathf.Max(0f, snapDelay);
            pullTargetDistance = Mathf.Max(0f, pullTargetDistance);
        }
    }
}
