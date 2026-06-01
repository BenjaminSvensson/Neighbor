using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Pickupable))]
    public sealed class Umbrella : MonoBehaviour, IPrimaryUseInteractable, IInteractionTooltipProvider, IPickupLifecycleReceiver
    {
        [Header("Umbrella")]
        [SerializeField] private bool startsOpen;
        [SerializeField, Min(0f)] private float useCooldown = 0.2f;
        [SerializeField, Min(0.1f)] private float maximumFallSpeed = 4.5f;

        [Header("Visuals")]
        [SerializeField] private GameObject openCanopy;
        [SerializeField] private GameObject closedCanopy;
        [SerializeField] private Collider blockerCollider;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] toggleClips;
        [SerializeField, Range(0f, 1f)] private float toggleVolume = 0.45f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.04f;

        private Pickupable pickupable;
        private PlayerController holder;
        private AudioClip generatedToggleClip;
        private float nextUseTime;
        private bool isOpen;

        private void Awake()
        {
            pickupable = GetComponent<Pickupable>();
            ResolveAudioSource();
            SetOpen(startsOpen, false);
        }

        private void Update()
        {
            if (!isOpen || pickupable == null || !pickupable.IsHeld || holder == null)
            {
                return;
            }

            if (blockerCollider != null && !blockerCollider.enabled)
            {
                blockerCollider.enabled = true;
            }

            holder.ClampDownwardVelocity(maximumFallSpeed);
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
            SetOpen(!isOpen, true);
        }

        public void OnPickupStarted(Pickupable pickupable, PlayerInteractor interactor)
        {
            holder = interactor != null ? interactor.GetComponentInParent<PlayerController>() : null;
        }

        public void OnPickupPlaced(Pickupable pickupable)
        {
            holder = null;
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

            actionText = isOpen ? "Close Umbrella" : "Open Umbrella";
            keyText = "Left Mouse";
            return true;
        }

        private void SetOpen(bool open, bool playFeedback)
        {
            isOpen = open;

            if (openCanopy != null)
            {
                openCanopy.SetActive(isOpen);
            }

            if (closedCanopy != null)
            {
                closedCanopy.SetActive(!isOpen);
            }

            if (blockerCollider != null)
            {
                blockerCollider.enabled = isOpen;
            }

            if (playFeedback)
            {
                PlayToggleSound();
            }
        }

        private void PlayToggleSound()
        {
            if (audioSource == null)
            {
                return;
            }

            AudioClip clip = GetToggleClip();
            if (clip == null)
            {
                return;
            }

            audioSource.pitch = Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
            audioSource.PlayOneShot(clip, toggleVolume);
        }

        private AudioClip GetToggleClip()
        {
            if (toggleClips != null && toggleClips.Length > 0)
            {
                return toggleClips[Random.Range(0, toggleClips.Length)];
            }

            if (generatedToggleClip == null)
            {
                generatedToggleClip = CreateGeneratedToggleClip();
            }

            return generatedToggleClip;
        }

        private AudioClip CreateGeneratedToggleClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.14f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Clamp01(1f - time / duration);
                float snap = Mathf.Sin(2f * Mathf.PI * 640f * time) * 0.2f;
                float cloth = Random.Range(-1f, 1f) * 0.12f;
                samples[i] = (snap + cloth) * envelope;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedUmbrellaToggle", sampleCount, 1, sampleRate, false);
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
            useCooldown = Mathf.Max(0f, useCooldown);
            maximumFallSpeed = Mathf.Max(0.1f, maximumFallSpeed);
        }
    }
}
