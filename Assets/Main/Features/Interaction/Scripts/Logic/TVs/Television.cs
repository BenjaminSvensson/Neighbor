using System.Collections.Generic;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Collider))]
    public sealed class Television : MonoBehaviour, IInteractable, IInteractionTooltipProvider
    {
        private static readonly List<Television> ActiveTelevisions = new();

        [Header("TV")]
        [SerializeField] private bool startsOn;
        [SerializeField] private Renderer screenRenderer;
        [SerializeField] private Color offScreenColor = new Color(0.015f, 0.018f, 0.022f, 1f);
        [SerializeField] private Color onScreenColor = new Color(0.18f, 0.55f, 1f, 1f);
        [SerializeField] private Color onEmissionColor = new Color(0.1f, 0.45f, 1f, 1f);

        [Header("Neighbor Distraction")]
        [SerializeField, Min(0f)] private float noiseRadius = 16f;
        [SerializeField, Range(0f, 1f)] private float noiseLoudness = 0.65f;
        [SerializeField, Range(0f, 1f)] private float alertUrgency = 0.35f;
        [SerializeField, Min(0.02f)] private float noiseLifetime = 0.6f;
        [SerializeField, Min(0.1f)] private float noiseInterval = 1.25f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] toggleClips;
        [SerializeField] private AudioClip[] staticClips;
        [SerializeField, Range(0f, 1f)] private float toggleVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] private float staticVolume = 0.35f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.04f;
        [SerializeField, Min(0f)] private float audioMinDistance = 0.75f;
        [SerializeField, Min(0.1f)] private float audioMaxDistance = 16f;

        private MaterialPropertyBlock screenPropertyBlock;
        private AudioClip generatedToggleClip;
        private AudioClip generatedStaticClip;
        private float nextNoiseTime;
        private bool isOn;

        public bool IsOn => isOn;
        public Vector3 RemoteTargetPosition => screenRenderer != null ? screenRenderer.bounds.center : transform.position;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveTelevisions()
        {
            ActiveTelevisions.Clear();
        }

        private void Awake()
        {
            if (screenRenderer == null)
            {
                screenRenderer = GetComponentInChildren<Renderer>();
            }

            ResolveAudioSource();
            SetOn(startsOn, false);
        }

        private void OnEnable()
        {
            if (!ActiveTelevisions.Contains(this))
            {
                ActiveTelevisions.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveTelevisions.Remove(this);
        }

        private void Update()
        {
            if (!isOn)
            {
                return;
            }

            if (Time.time >= nextNoiseTime)
            {
                nextNoiseTime = Time.time + noiseInterval;
                EmitNeighborNoise();
                PlayStatic();
            }
        }

        public bool CanInteract(PlayerInteractor interactor)
        {
            return true;
        }

        public void Interact(PlayerInteractor interactor)
        {
            Toggle();
        }

        public void Toggle()
        {
            SetOn(!isOn, true);
        }

        public void SetOn(bool on, bool playFeedback)
        {
            isOn = on;
            nextNoiseTime = Time.time;
            ApplyScreenState();

            if (playFeedback)
            {
                PlayToggleSound();
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

            if (context != InteractionTooltipContext.FocusedInteractable)
            {
                return false;
            }

            actionText = isOn ? "Turn TV off" : "Turn TV on";
            keyText = "E";
            return true;
        }

        public static bool TryFindNearest(Vector3 origin, float range, out Television nearest)
        {
            nearest = null;
            float bestSqrDistance = range * range;

            for (int i = ActiveTelevisions.Count - 1; i >= 0; i--)
            {
                Television tv = ActiveTelevisions[i];
                if (tv == null || !tv.isActiveAndEnabled)
                {
                    ActiveTelevisions.RemoveAt(i);
                    continue;
                }

                float sqrDistance = (tv.RemoteTargetPosition - origin).sqrMagnitude;
                if (sqrDistance > bestSqrDistance)
                {
                    continue;
                }

                bestSqrDistance = sqrDistance;
                nearest = tv;
            }

            return nearest != null;
        }

        private void ApplyScreenState()
        {
            if (screenRenderer == null)
            {
                return;
            }

            screenPropertyBlock ??= new MaterialPropertyBlock();
            screenRenderer.GetPropertyBlock(screenPropertyBlock);
            screenPropertyBlock.SetColor("_BaseColor", isOn ? onScreenColor : offScreenColor);
            screenPropertyBlock.SetColor("_Color", isOn ? onScreenColor : offScreenColor);
            screenPropertyBlock.SetColor("_EmissionColor", isOn ? onEmissionColor : Color.black);
            screenRenderer.SetPropertyBlock(screenPropertyBlock);
        }

        private void EmitNeighborNoise()
        {
            if (noiseRadius <= 0f || noiseLoudness <= 0f)
            {
                return;
            }

            GameObject noiseObject = new GameObject("TelevisionNoiseEvent");
            noiseObject.transform.position = RemoteTargetPosition;

            SphereCollider sphereCollider = noiseObject.AddComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
            sphereCollider.radius = noiseRadius;

            Rigidbody noiseBody = noiseObject.AddComponent<Rigidbody>();
            noiseBody.isKinematic = true;
            noiseBody.useGravity = false;

            NoiseEvent noiseEvent = noiseObject.AddComponent<NoiseEvent>();
            noiseEvent.Initialize(RemoteTargetPosition, noiseRadius, noiseLoudness, gameObject, noiseLifetime, alertUrgency);
        }

        private void PlayToggleSound()
        {
            PlayOneShot(GetToggleClip(), toggleVolume);
        }

        private void PlayStatic()
        {
            PlayOneShot(GetStaticClip(), staticVolume);
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (audioSource == null || clip == null)
            {
                return;
            }

            audioSource.pitch = Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
            audioSource.PlayOneShot(clip, volume);
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

        private AudioClip GetStaticClip()
        {
            if (staticClips != null && staticClips.Length > 0)
            {
                return staticClips[Random.Range(0, staticClips.Length)];
            }

            if (generatedStaticClip == null)
            {
                generatedStaticClip = CreateGeneratedStaticClip();
            }

            return generatedStaticClip;
        }

        private AudioClip CreateGeneratedToggleClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.12f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Exp(-time * 32f);
                float tone = Mathf.Sin(2f * Mathf.PI * 920f * time);
                samples[i] = tone * envelope * 0.35f;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedTvToggle", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateGeneratedStaticClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.25f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Clamp01(1f - time / duration);
                float hum = Mathf.Sin(2f * Mathf.PI * 110f * time) * 0.25f;
                float staticNoise = Random.Range(-1f, 1f) * 0.45f;
                samples[i] = (hum + staticNoise) * envelope * 0.35f;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedTvStatic", sampleCount, 1, sampleRate, false);
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
            audioSource.minDistance = audioMinDistance;
            audioSource.maxDistance = audioMaxDistance;
            audioSource.dopplerLevel = 0.05f;
        }

        private void OnValidate()
        {
            noiseRadius = Mathf.Max(0f, noiseRadius);
            noiseLifetime = Mathf.Max(0.02f, noiseLifetime);
            noiseInterval = Mathf.Max(0.1f, noiseInterval);
            audioMaxDistance = Mathf.Max(0.1f, audioMaxDistance);
        }
    }
}
