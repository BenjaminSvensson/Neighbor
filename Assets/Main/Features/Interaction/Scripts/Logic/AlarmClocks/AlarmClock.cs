using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Pickupable))]
    public sealed class AlarmClock : MonoBehaviour, IPrimaryUseInteractable, IInteractionTooltipProvider
    {
        [Header("Alarm")]
        [SerializeField, Min(0.1f)] private float ringDelay = 10f;
        [SerializeField, Min(0f)] private float restartCooldown = 0.5f;

        [Header("Neighbor Alert")]
        [SerializeField, Min(0f)] private float hearingRadius = 20f;
        [SerializeField, Range(0f, 1f)] private float loudness = 1f;
        [SerializeField, Range(0f, 1f)] private float alertUrgency = 0.75f;
        [SerializeField, Min(0.02f)] private float noiseLifetime = 0.8f;

        [Header("Placeholder Feedback")]
        [SerializeField] private Renderer feedbackRenderer;
        [SerializeField] private Transform tickingVisual;
        [SerializeField] private Color idleColor = new Color(0.78f, 0.72f, 0.58f, 1f);
        [SerializeField] private Color tickingColor = new Color(1f, 0.86f, 0.28f, 1f);
        [SerializeField] private Color ringingColor = new Color(1f, 0.18f, 0.08f, 1f);
        [SerializeField, Min(0f)] private float tickPulseScale = 0.08f;
        [SerializeField, Min(0f)] private float ringPulseScale = 0.18f;
        [SerializeField, Min(0f)] private float handSpinDegreesPerSecond = 360f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] tickClips;
        [SerializeField] private AudioClip[] ringClips;
        [SerializeField, Range(0f, 1f)] private float tickVolume = 0.28f;
        [SerializeField, Range(0f, 1f)] private float ringVolume = 0.9f;
        [SerializeField, Min(0.05f)] private float tickInterval = 0.5f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.04f;
        [SerializeField, Min(0f)] private float audioMinDistance = 0.5f;
        [SerializeField, Min(0.1f)] private float audioMaxDistance = 20f;

        private Pickupable pickupable;
        private MaterialPropertyBlock feedbackPropertyBlock;
        private AudioClip generatedTickClip;
        private AudioClip generatedRingClip;
        private Vector3 tickingVisualBaseScale;
        private float ringTime;
        private float nextTickTime;
        private float nextStartTime;
        private bool isTicking;
        private bool hasRung;

        private void Awake()
        {
            pickupable = GetComponent<Pickupable>();
            if (feedbackRenderer == null)
            {
                feedbackRenderer = GetComponentInChildren<Renderer>();
            }

            if (tickingVisual != null)
            {
                tickingVisualBaseScale = tickingVisual.localScale;
            }

            ResolveAudioSource();
            ApplyFeedbackColor(idleColor);
        }

        private void Update()
        {
            if (!isTicking)
            {
                return;
            }

            UpdatePlaceholderFeedback();

            if (!hasRung && Time.time >= nextTickTime)
            {
                nextTickTime = Time.time + tickInterval;
                PlayTick();
            }

            if (!hasRung && Time.time >= ringTime)
            {
                Ring();
            }
        }

        public bool CanPrimaryUse(PlayerInteractor interactor)
        {
            return pickupable != null && pickupable.IsHeld && !isTicking && Time.time >= nextStartTime;
        }

        public void PrimaryUse(PlayerInteractor interactor)
        {
            if (!CanPrimaryUse(interactor))
            {
                return;
            }

            StartTicking();
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

            actionText = isTicking ? "Alarm ticking" : "Start alarm";
            keyText = "Left Mouse";
            return true;
        }

        private void StartTicking()
        {
            isTicking = true;
            hasRung = false;
            ringTime = Time.time + ringDelay;
            nextTickTime = Time.time;
            nextStartTime = Time.time + restartCooldown;
            ApplyFeedbackColor(tickingColor);
        }

        private void Ring()
        {
            hasRung = true;
            isTicking = false;
            nextStartTime = Time.time + restartCooldown;
            ApplyFeedbackColor(ringingColor);
            PlayRing();
            EmitNeighborNoise();
        }

        private void UpdatePlaceholderFeedback()
        {
            if (tickingVisual == null)
            {
                return;
            }

            tickingVisual.Rotate(Vector3.forward, handSpinDegreesPerSecond * Time.deltaTime, Space.Self);

            float phase = Mathf.PingPong(Time.time * 4f, 1f);
            float scaleOffset = hasRung ? ringPulseScale : tickPulseScale;
            tickingVisual.localScale = tickingVisualBaseScale * (1f + phase * scaleOffset);
        }

        private void ApplyFeedbackColor(Color color)
        {
            if (feedbackRenderer == null)
            {
                return;
            }

            feedbackPropertyBlock ??= new MaterialPropertyBlock();
            feedbackRenderer.GetPropertyBlock(feedbackPropertyBlock);
            feedbackPropertyBlock.SetColor("_BaseColor", color);
            feedbackPropertyBlock.SetColor("_Color", color);
            feedbackPropertyBlock.SetColor("_EmissionColor", hasRung ? color * 0.55f : Color.black);
            feedbackRenderer.SetPropertyBlock(feedbackPropertyBlock);
        }

        private void PlayTick()
        {
            PlayOneShot(GetTickClip(), tickVolume);
        }

        private void PlayRing()
        {
            PlayOneShot(GetRingClip(), ringVolume);
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

        private AudioClip GetTickClip()
        {
            if (tickClips != null && tickClips.Length > 0)
            {
                return tickClips[Random.Range(0, tickClips.Length)];
            }

            if (generatedTickClip == null)
            {
                generatedTickClip = CreateGeneratedTickClip();
            }

            return generatedTickClip;
        }

        private AudioClip GetRingClip()
        {
            if (ringClips != null && ringClips.Length > 0)
            {
                return ringClips[Random.Range(0, ringClips.Length)];
            }

            if (generatedRingClip == null)
            {
                generatedRingClip = CreateGeneratedRingClip();
            }

            return generatedRingClip;
        }

        private AudioClip CreateGeneratedTickClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.06f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Exp(-time * 80f);
                float tick = Mathf.Sin(2f * Mathf.PI * 1800f * time);
                samples[i] = tick * envelope * 0.42f;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedAlarmClockTick", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateGeneratedRingClip()
        {
            const int sampleRate = 22050;
            const float duration = 1.2f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float pulse = Mathf.PingPong(time * 12f, 1f) > 0.38f ? 1f : 0f;
                float envelope = Mathf.Clamp01(1f - time / duration) * pulse;
                float bell = Mathf.Sin(2f * Mathf.PI * 1320f * time);
                float wobble = Mathf.Sin(2f * Mathf.PI * 980f * time) * 0.55f;
                samples[i] = (bell + wobble) * envelope * 0.36f;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedAlarmClockRing", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void EmitNeighborNoise()
        {
            if (hearingRadius <= 0f || loudness <= 0f)
            {
                return;
            }

            Vector3 origin = transform.position;
            GameObject noiseObject = new GameObject("AlarmClockNoiseEvent");
            noiseObject.transform.position = origin;

            SphereCollider sphereCollider = noiseObject.AddComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
            sphereCollider.radius = hearingRadius;

            Rigidbody noiseBody = noiseObject.AddComponent<Rigidbody>();
            noiseBody.isKinematic = true;
            noiseBody.useGravity = false;

            NoiseEvent noiseEvent = noiseObject.AddComponent<NoiseEvent>();
            noiseEvent.Initialize(origin, hearingRadius, loudness, gameObject, noiseLifetime, alertUrgency);
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
            audioSource.dopplerLevel = 0.1f;
        }

        private void OnValidate()
        {
            ringDelay = Mathf.Max(0.1f, ringDelay);
            restartCooldown = Mathf.Max(0f, restartCooldown);
            hearingRadius = Mathf.Max(0f, hearingRadius);
            noiseLifetime = Mathf.Max(0.02f, noiseLifetime);
            tickInterval = Mathf.Max(0.05f, tickInterval);
            audioMaxDistance = Mathf.Max(0.1f, audioMaxDistance);
        }
    }
}
