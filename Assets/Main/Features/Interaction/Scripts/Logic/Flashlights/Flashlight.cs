using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Pickupable))]
    public sealed class Flashlight : MonoBehaviour, IPrimaryUseInteractable
    {
        [Header("Light")]
        [SerializeField] private Light flashlightLight;
        [SerializeField] private Renderer lensRenderer;
        [SerializeField] private bool startsOn;
        [SerializeField] private Color lensOffColor = new Color(0.16f, 0.16f, 0.14f, 1f);
        [SerializeField] private Color lensOnColor = new Color(1f, 0.92f, 0.55f, 1f);
        [SerializeField] private Color lensEmissionColor = new Color(1f, 0.82f, 0.28f, 1f);

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] toggleClips;
        [SerializeField, Range(0f, 1f)] private float toggleVolume = 0.45f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.04f;

        private Pickupable pickupable;
        private MaterialPropertyBlock lensPropertyBlock;
        private AudioClip generatedToggleClip;
        private bool isOn;

        private void Awake()
        {
            pickupable = GetComponent<Pickupable>();

            if (flashlightLight == null)
            {
                flashlightLight = GetComponentInChildren<Light>(true);
            }

            if (lensRenderer == null)
            {
                lensRenderer = GetComponentInChildren<Renderer>();
            }

            ResolveAudioSource();
            SetOn(startsOn, false);
        }

        public bool CanPrimaryUse(PlayerInteractor interactor)
        {
            return pickupable != null && pickupable.IsHeld;
        }

        public void PrimaryUse(PlayerInteractor interactor)
        {
            Toggle();
        }

        public void Toggle()
        {
            SetOn(!isOn, true);
        }

        private void SetOn(bool on, bool playSound)
        {
            isOn = on;

            if (flashlightLight != null)
            {
                flashlightLight.enabled = isOn;
            }

            ApplyLensState();

            if (playSound)
            {
                PlayToggleSound();
            }
        }

        private void ApplyLensState()
        {
            if (lensRenderer == null)
            {
                return;
            }

            lensPropertyBlock ??= new MaterialPropertyBlock();
            lensRenderer.GetPropertyBlock(lensPropertyBlock);
            lensPropertyBlock.SetColor("_BaseColor", isOn ? lensOnColor : lensOffColor);
            lensPropertyBlock.SetColor("_Color", isOn ? lensOnColor : lensOffColor);
            lensPropertyBlock.SetColor("_EmissionColor", isOn ? lensEmissionColor : Color.black);
            lensRenderer.SetPropertyBlock(lensPropertyBlock);
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
            const float duration = 0.08f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Exp(-time * 46f);
                float click = Mathf.Sin(2f * Mathf.PI * 1400f * time) * 0.4f;
                float snap = Random.Range(-1f, 1f) * 0.28f;
                samples[i] = (click + snap) * envelope;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedFlashlightToggle", sampleCount, 1, sampleRate, false);
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
            audioSource.maxDistance = 6f;
            audioSource.dopplerLevel = 0.05f;
        }
    }
}
