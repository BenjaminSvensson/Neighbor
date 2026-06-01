using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Pickupable))]
    public sealed class Screwdriver : MonoBehaviour, IPrimaryUseInteractable, IInteractionTooltipProvider
    {
        [Header("Unscrewing")]
        [SerializeField, Min(0.1f)] private float unscrewRange = 3f;
        [SerializeField, Min(0f)] private float unscrewRadius = 0.16f;
        [SerializeField, Min(0f)] private float useCooldown = 0.25f;
        [SerializeField] private LayerMask unscrewMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] turnClips;
        [SerializeField, Range(0f, 1f)] private float turnVolume = 0.45f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.05f;

        private readonly RaycastHit[] hits = new RaycastHit[8];
        private Pickupable pickupable;
        private AudioClip generatedTurnClip;
        private float nextUseTime;

        private void Awake()
        {
            pickupable = GetComponent<Pickupable>();
            ResolveAudioSource();
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
            if (TryFindVentCover(interactor, out VentCover ventCover))
            {
                ventCover.UnscrewOne(gameObject);
                PlayTurnSound();
            }
        }

        public bool TryGetInteractionTooltip(
            PlayerInteractor interactor,
            InteractionTooltipContext context,
            out string actionText,
            out string keyText)
        {
            actionText = null;
            keyText = null;

            if (context != InteractionTooltipContext.HeldPrimaryUse || !TryFindVentCover(interactor, out VentCover ventCover))
            {
                return false;
            }

            actionText = ventCover.HasScrewsRemaining ? "Unscrew" : "Vent open";
            keyText = "Left Mouse";
            return true;
        }

        private bool TryFindVentCover(PlayerInteractor interactor, out VentCover ventCover)
        {
            ventCover = null;
            Transform viewTransform = interactor != null ? interactor.ViewTransform : transform;
            Ray ray = new(viewTransform.position, viewTransform.forward);
            int hitCount = unscrewRadius > 0f
                ? Physics.SphereCastNonAlloc(ray, unscrewRadius, hits, unscrewRange, unscrewMask, triggerInteraction)
                : Physics.RaycastNonAlloc(ray, hits, unscrewRange, unscrewMask, triggerInteraction);

            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.distance >= bestDistance)
                {
                    continue;
                }

                VentCover candidate = hit.collider.GetComponentInParent<VentCover>();
                if (candidate == null || !candidate.CanUnscrew)
                {
                    continue;
                }

                ventCover = candidate;
                bestDistance = hit.distance;
            }

            return ventCover != null;
        }

        private void PlayTurnSound()
        {
            if (audioSource == null)
            {
                return;
            }

            AudioClip clip = GetTurnClip();
            if (clip == null)
            {
                return;
            }

            audioSource.pitch = Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
            audioSource.PlayOneShot(clip, turnVolume);
        }

        private AudioClip GetTurnClip()
        {
            if (turnClips != null && turnClips.Length > 0)
            {
                return turnClips[Random.Range(0, turnClips.Length)];
            }

            if (generatedTurnClip == null)
            {
                generatedTurnClip = CreateGeneratedTurnClip();
            }

            return generatedTurnClip;
        }

        private AudioClip CreateGeneratedTurnClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.16f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Clamp01(1f - time / duration);
                float scrape = Mathf.Sin(2f * Mathf.PI * 720f * time) * 0.18f;
                float click = Random.Range(-1f, 1f) * 0.14f;
                samples[i] = (scrape + click) * envelope;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedScrewdriverTurn", sampleCount, 1, sampleRate, false);
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
            audioSource.dopplerLevel = 0.05f;
        }

        private void OnValidate()
        {
            unscrewRange = Mathf.Max(0.1f, unscrewRange);
            unscrewRadius = Mathf.Max(0f, unscrewRadius);
            useCooldown = Mathf.Max(0f, useCooldown);
        }
    }
}
