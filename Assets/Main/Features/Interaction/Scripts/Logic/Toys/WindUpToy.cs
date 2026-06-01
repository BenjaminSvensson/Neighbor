using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Pickupable))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class WindUpToy : MonoBehaviour, IPrimaryUseInteractable, IInteractionTooltipProvider, IPickupLifecycleReceiver
    {
        [Header("Wind-Up")]
        [SerializeField, Min(0.1f)] private float driveDuration = 5f;
        [SerializeField, Min(0f)] private float driveSpeed = 2.4f;
        [SerializeField, Min(0f)] private float restartCooldown = 0.35f;
        [SerializeField] private bool stopWhenPickedUp = true;

        [Header("Movement")]
        [SerializeField] private bool useFacingDirection = true;
        [SerializeField] private Transform forwardReference;
        [SerializeField, Min(0f)] private float groundProbeDistance = 0.28f;
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Neighbor Noise")]
        [SerializeField, Min(0f)] private float noiseRadius = 12f;
        [SerializeField, Range(0f, 1f)] private float noiseLoudness = 0.55f;
        [SerializeField, Min(0.02f)] private float noiseLifetime = 0.35f;
        [SerializeField, Min(0.05f)] private float noiseInterval = 0.55f;

        [Header("Placeholder Feedback")]
        [SerializeField] private Renderer feedbackRenderer;
        [SerializeField] private Transform[] spinningParts;
        [SerializeField] private Color idleColor = new(0.95f, 0.22f, 0.14f, 1f);
        [SerializeField] private Color runningColor = new(1f, 0.82f, 0.16f, 1f);
        [SerializeField, Min(0f)] private float spinDegreesPerSecond = 720f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] windUpClips;
        [SerializeField] private AudioClip[] tickClips;
        [SerializeField, Range(0f, 1f)] private float windUpVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float tickVolume = 0.32f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.05f;
        [SerializeField, Min(0f)] private float audioMinDistance = 0.4f;
        [SerializeField, Min(0.1f)] private float audioMaxDistance = 12f;

        private Pickupable pickupable;
        private Rigidbody toyBody;
        private MaterialPropertyBlock feedbackPropertyBlock;
        private AudioClip generatedWindUpClip;
        private AudioClip generatedTickClip;
        private RigidbodyConstraints originalConstraints;
        private float stopTime;
        private float nextNoiseTime;
        private float nextStartTime;
        private bool isRunning;

        public bool IsRunning => isRunning;
        public bool CanWindFromWorld => pickupable == null || !pickupable.IsHeld;

        private void Awake()
        {
            pickupable = GetComponent<Pickupable>();
            toyBody = GetComponent<Rigidbody>();
            originalConstraints = toyBody != null ? toyBody.constraints : RigidbodyConstraints.None;

            if (forwardReference == null)
            {
                forwardReference = transform;
            }

            if (feedbackRenderer == null)
            {
                feedbackRenderer = GetComponentInChildren<Renderer>();
            }

            ResolveAudioSource();
            ApplyFeedbackColor(idleColor);
        }

        private void Update()
        {
            if (!isRunning)
            {
                return;
            }

            SpinPlaceholderParts();

            if (Time.time >= nextNoiseTime)
            {
                nextNoiseTime = Time.time + noiseInterval;
                EmitNeighborNoise();
                PlayTickSound();
            }

            if (Time.time >= stopTime)
            {
                StopDriving();
            }
        }

        private void FixedUpdate()
        {
            if (!isRunning || toyBody == null || toyBody.isKinematic)
            {
                return;
            }

            Vector3 forward = GetDriveDirection();
            Vector3 velocity = toyBody.linearVelocity;
            Vector3 targetVelocity = forward * driveSpeed;
            targetVelocity.y = velocity.y;
            toyBody.linearVelocity = targetVelocity;
            toyBody.WakeUp();
        }

        public bool CanPrimaryUse(PlayerInteractor interactor)
        {
            return pickupable != null && pickupable.IsHeld && Time.time >= nextStartTime;
        }

        public void PrimaryUse(PlayerInteractor interactor)
        {
            if (!CanPrimaryUse(interactor))
            {
                return;
            }

            if (pickupable != null)
            {
                pickupable.Drop();
                interactor?.ForgetHeldPickup(pickupable);
            }

            StartDriving();
        }

        public bool TryWindFromWorld(PlayerInteractor interactor)
        {
            if (!CanWindFromWorld || Time.time < nextStartTime)
            {
                return false;
            }

            StartDriving();
            return true;
        }

        public void OnPickupStarted(Pickupable pickupable, PlayerInteractor interactor)
        {
            if (stopWhenPickedUp)
            {
                StopDriving();
            }
        }

        public void OnPickupPlaced(Pickupable pickupable)
        {
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

            actionText = isRunning ? "Toy running" : "Wind up toy";
            keyText = "Left Mouse";
            return true;
        }

        private void StartDriving()
        {
            isRunning = true;
            stopTime = Time.time + driveDuration;
            nextNoiseTime = Time.time;
            nextStartTime = Time.time + restartCooldown;

            if (toyBody != null)
            {
                toyBody.isKinematic = false;
                toyBody.useGravity = true;
                toyBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                toyBody.interpolation = RigidbodyInterpolation.Interpolate;
                toyBody.constraints = originalConstraints | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                toyBody.WakeUp();
            }

            ApplyFeedbackColor(runningColor);
            PlayWindUpSound();
        }

        private void StopDriving()
        {
            if (!isRunning && toyBody == null)
            {
                return;
            }

            isRunning = false;
            nextStartTime = Mathf.Max(nextStartTime, Time.time + restartCooldown);

            if (toyBody != null)
            {
                toyBody.constraints = originalConstraints;
                if (!toyBody.isKinematic)
                {
                    Vector3 velocity = toyBody.linearVelocity;
                    toyBody.linearVelocity = new Vector3(0f, velocity.y, 0f);
                    toyBody.angularVelocity = Vector3.zero;
                }
            }

            ApplyFeedbackColor(idleColor);
        }

        private Vector3 GetDriveDirection()
        {
            Vector3 forward = useFacingDirection && forwardReference != null ? forwardReference.forward : transform.forward;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = transform.forward;
                forward.y = 0f;
            }

            forward.Normalize();

            if (groundProbeDistance <= 0f || toyBody == null)
            {
                return forward;
            }

            Vector3 origin = toyBody.worldCenterOfMass + Vector3.up * 0.05f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundProbeDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                forward = Vector3.ProjectOnPlane(forward, hit.normal).normalized;
            }

            return forward.sqrMagnitude > 0.0001f ? forward : transform.forward;
        }

        private void SpinPlaceholderParts()
        {
            if (spinningParts == null)
            {
                return;
            }

            float spin = spinDegreesPerSecond * Time.deltaTime;
            foreach (Transform spinningPart in spinningParts)
            {
                if (spinningPart != null)
                {
                    spinningPart.Rotate(Vector3.right, spin, Space.Self);
                }
            }
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
            feedbackPropertyBlock.SetColor("_EmissionColor", isRunning ? color * 0.35f : Color.black);
            feedbackRenderer.SetPropertyBlock(feedbackPropertyBlock);
        }

        private void EmitNeighborNoise()
        {
            if (noiseRadius <= 0f || noiseLoudness <= 0f)
            {
                return;
            }

            Vector3 origin = toyBody != null ? toyBody.worldCenterOfMass : transform.position;
            GameObject noiseObject = new("WindUpToyNoiseEvent");
            noiseObject.transform.position = origin;

            SphereCollider sphereCollider = noiseObject.AddComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
            sphereCollider.radius = noiseRadius;

            Rigidbody noiseBody = noiseObject.AddComponent<Rigidbody>();
            noiseBody.isKinematic = true;
            noiseBody.useGravity = false;

            NoiseEvent noiseEvent = noiseObject.AddComponent<NoiseEvent>();
            noiseEvent.Initialize(origin, noiseRadius, noiseLoudness, gameObject, noiseLifetime);
        }

        private void PlayWindUpSound()
        {
            PlayOneShot(GetWindUpClip(), windUpVolume);
        }

        private void PlayTickSound()
        {
            PlayOneShot(GetTickClip(), tickVolume);
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

        private AudioClip GetWindUpClip()
        {
            if (windUpClips != null && windUpClips.Length > 0)
            {
                return windUpClips[Random.Range(0, windUpClips.Length)];
            }

            if (generatedWindUpClip == null)
            {
                generatedWindUpClip = CreateGeneratedWindUpClip();
            }

            return generatedWindUpClip;
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

        private AudioClip CreateGeneratedWindUpClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.22f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Clamp01(1f - time / duration);
                float ratchet = Mathf.PingPong(time * 60f, 1f) > 0.55f ? 1f : -0.35f;
                float tone = Mathf.Sin(2f * Mathf.PI * 880f * time) * 0.2f;
                samples[i] = (ratchet * 0.18f + tone) * envelope;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedWindUp", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateGeneratedTickClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.08f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Exp(-time * 48f);
                float gear = Mathf.Sin(2f * Mathf.PI * 520f * time);
                float click = Random.Range(-1f, 1f) * 0.16f;
                samples[i] = (gear * 0.24f + click) * envelope;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedToyTick", sampleCount, 1, sampleRate, false);
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
            audioSource.dopplerLevel = 0.1f;
        }

        private void OnValidate()
        {
            driveDuration = Mathf.Max(0.1f, driveDuration);
            driveSpeed = Mathf.Max(0f, driveSpeed);
            restartCooldown = Mathf.Max(0f, restartCooldown);
            groundProbeDistance = Mathf.Max(0f, groundProbeDistance);
            noiseRadius = Mathf.Max(0f, noiseRadius);
            noiseLifetime = Mathf.Max(0.02f, noiseLifetime);
            noiseInterval = Mathf.Max(0.05f, noiseInterval);
            spinDegreesPerSecond = Mathf.Max(0f, spinDegreesPerSecond);
            audioMaxDistance = Mathf.Max(0.1f, audioMaxDistance);
        }
    }
}
