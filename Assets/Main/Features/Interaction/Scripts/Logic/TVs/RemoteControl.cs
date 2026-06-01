using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Pickupable))]
    public sealed class RemoteControl : MonoBehaviour, IPrimaryUseInteractable, IInteractionTooltipProvider
    {
        [Header("Remote")]
        [SerializeField, Min(0f)] private float tvRange = 10f;
        [SerializeField, Min(0f)] private float useCooldown = 0.25f;
        [SerializeField] private Transform signalOrigin;

        [Header("Feedback")]
        [SerializeField] private Renderer buttonRenderer;
        [SerializeField] private Color readyColor = new Color(0.08f, 0.08f, 0.09f, 1f);
        [SerializeField] private Color pressedColor = new Color(1f, 0.12f, 0.08f, 1f);
        [SerializeField, Min(0f)] private float buttonFlashDuration = 0.08f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] clickClips;
        [SerializeField, Range(0f, 1f)] private float clickVolume = 0.45f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.04f;

        private Pickupable pickupable;
        private MaterialPropertyBlock buttonPropertyBlock;
        private AudioClip generatedClickClip;
        private float nextUseTime;
        private float buttonFlashEndTime;

        private void Awake()
        {
            pickupable = GetComponent<Pickupable>();
            if (signalOrigin == null)
            {
                signalOrigin = transform;
            }

            if (buttonRenderer == null)
            {
                buttonRenderer = GetComponentInChildren<Renderer>();
            }

            ResolveAudioSource();
            ApplyButtonColor(readyColor);
        }

        private void Update()
        {
            if (buttonFlashEndTime > 0f && Time.time >= buttonFlashEndTime)
            {
                buttonFlashEndTime = 0f;
                ApplyButtonColor(readyColor);
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
            PlayClick();
            FlashButton();

            Vector3 origin = signalOrigin != null ? signalOrigin.position : transform.position;
            if (Television.TryFindNearest(origin, tvRange, out Television tv))
            {
                tv.Toggle();
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

            if (context != InteractionTooltipContext.HeldPrimaryUse)
            {
                return false;
            }

            actionText = "Toggle nearest TV";
            keyText = "Left Mouse";
            return true;
        }

        private void FlashButton()
        {
            buttonFlashEndTime = Time.time + buttonFlashDuration;
            ApplyButtonColor(pressedColor);
        }

        private void ApplyButtonColor(Color color)
        {
            if (buttonRenderer == null)
            {
                return;
            }

            buttonPropertyBlock ??= new MaterialPropertyBlock();
            buttonRenderer.GetPropertyBlock(buttonPropertyBlock);
            buttonPropertyBlock.SetColor("_BaseColor", color);
            buttonPropertyBlock.SetColor("_Color", color);
            buttonPropertyBlock.SetColor("_EmissionColor", color == pressedColor ? pressedColor * 0.45f : Color.black);
            buttonRenderer.SetPropertyBlock(buttonPropertyBlock);
        }

        private void PlayClick()
        {
            if (audioSource == null)
            {
                return;
            }

            AudioClip clip = GetClickClip();
            if (clip == null)
            {
                return;
            }

            audioSource.pitch = Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
            audioSource.PlayOneShot(clip, clickVolume);
        }

        private AudioClip GetClickClip()
        {
            if (clickClips != null && clickClips.Length > 0)
            {
                return clickClips[Random.Range(0, clickClips.Length)];
            }

            if (generatedClickClip == null)
            {
                generatedClickClip = CreateGeneratedClickClip();
            }

            return generatedClickClip;
        }

        private AudioClip CreateGeneratedClickClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.07f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Exp(-time * 58f);
                float click = Mathf.Sin(2f * Mathf.PI * 1500f * time) * 0.28f;
                float snap = Random.Range(-1f, 1f) * 0.18f;
                samples[i] = (click + snap) * envelope;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedRemoteClick", sampleCount, 1, sampleRate, false);
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
            audioSource.minDistance = 0.25f;
            audioSource.maxDistance = 5f;
            audioSource.dopplerLevel = 0.05f;
        }

        private void OnValidate()
        {
            tvRange = Mathf.Max(0f, tvRange);
            useCooldown = Mathf.Max(0f, useCooldown);
            buttonFlashDuration = Mathf.Max(0f, buttonFlashDuration);
        }
    }
}
