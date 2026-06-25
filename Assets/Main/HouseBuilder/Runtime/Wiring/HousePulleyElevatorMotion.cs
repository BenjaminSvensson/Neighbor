using System.Collections.Generic;
using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HouseWireEndpoint))]
    [RequireComponent(typeof(HouseWireInputRelay))]
    [AddComponentMenu("Neighbor/House Builder/Wiring/Pulley Elevator Motion")]
    public sealed class HousePulleyElevatorMotion : MonoBehaviour, IHouseWireSignalReceiver
    {
        private const string ActivatePortId = "activate";
        private const string DefaultPlatformName = "Platform";
        private const string AlternatePlatformName = "Elevator Platform";
        private const float ProgressSnapEpsilon = 0.0001f;
        private const float MotionEpsilon = 0.00001f;

        [SerializeField] private Transform platform;
        [SerializeField] private Vector3 loweredLocalPosition;
        [SerializeField] private Vector3 raisedLocalOffset = new(0f, 3f, 0f);
        [SerializeField, Min(0.01f)] private float travelDuration = 1.5f;
        [SerializeField] private bool startsRaised;
        [SerializeField] private bool carryPlayersInTrigger = true;

        [Header("Audio")]
        [SerializeField] private AudioSource motionLoopSource;
        [SerializeField] private AudioSource oneShotSource;
        [SerializeField] private AudioClip startClip;
        [SerializeField] private AudioClip motionLoopClip;
        [SerializeField] private AudioClip arriveClip;
        [SerializeField] private AudioClip interruptClip;
        [SerializeField, Range(0f, 1f)] private float startVolume = 0.65f;
        [SerializeField, Range(0f, 1f)] private float motionLoopVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float arriveVolume = 0.72f;
        [SerializeField, Range(0f, 1f)] private float interruptVolume = 0.45f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.04f;
        [SerializeField, Min(0f)] private float audioFadeSpeed = 5f;
        [SerializeField, Min(0f)] private float audioMinDistance = 1.25f;
        [SerializeField, Min(0.1f)] private float audioMaxDistance = 22f;

        private readonly Dictionary<PlayerController, CharacterController> carriedPlayers = new();
        private readonly List<PlayerController> staleCarriedPlayers = new();
        private readonly Collider[] riderOverlapHits = new Collider[16];
        private BoxCollider riderTrigger;
        private AudioClip generatedStartClip;
        private AudioClip generatedMotionLoopClip;
        private AudioClip generatedArriveClip;
        private AudioClip generatedInterruptClip;
        private float progress;
        private float targetProgress;
        private bool motionAudioActive;
        private bool motionLoopRequested;
        private int motionAudioDirection;
        private bool initialized;

        public bool IsRaised => targetProgress >= 1f;
        public bool IsFullyRaised => targetProgress >= 1f && Mathf.Approximately(progress, 1f);
        public bool IsFullyLowered => targetProgress <= 0f && Mathf.Approximately(progress, 0f);
        public bool IsMoving => !Mathf.Approximately(progress, targetProgress);
        public float Progress => progress;
        public float TargetProgress => targetProgress;

        public void Configure(Transform elevatorPlatform, Vector3 loweredPosition, Vector3 raisedOffset, float duration, bool initiallyRaised)
        {
            platform = elevatorPlatform;
            loweredLocalPosition = loweredPosition;
            raisedLocalOffset = raisedOffset;
            travelDuration = Mathf.Max(0.01f, duration);
            startsRaised = initiallyRaised;
            initialized = false;
            EnsureWiringPort();
            InitializeState();
        }

        public void Toggle() => SetRaised(targetProgress < 0.5f);
        public void Raise() => SetRaised(true);
        public void Lower() => SetRaised(false);
        public void SetRaised(bool raised) => SetTargetProgress(raised ? 1f : 0f);

        public void SetTargetProgress(float value)
        {
            InitializeState();
            targetProgress = Mathf.Clamp01(value);
            if (!Application.isPlaying)
            {
                progress = targetProgress;
                ApplyPlatformPosition();
            }
        }

        public void ReceiveHouseWireSignal(HouseSignal signal)
        {
            if (signal == null)
            {
                return;
            }

            switch (signal.Kind)
            {
                case HouseSignalKind.Bool:
                    SetRaised(signal.BoolValue);
                    break;
                case HouseSignalKind.Float:
                    SetTargetProgress(signal.FloatValue);
                    break;
                default:
                    Toggle();
                    break;
            }
        }

        private void Awake()
        {
            EnsureWiringPort();
            InitializeState();
            ResolveAudioSources();
        }

        private void LateUpdate()
        {
            AdvanceMotion(Time.deltaTime);
            UpdateMotionLoopAudio(Time.deltaTime);
        }

        private void AdvanceMotion(float deltaTime)
        {
            if (platform == null || Mathf.Approximately(progress, targetProgress))
            {
                return;
            }

            int direction = targetProgress > progress ? 1 : -1;
            BeginMotionAudio(direction);

            float nextProgress = Mathf.MoveTowards(
                progress,
                targetProgress,
                Mathf.Max(0f, deltaTime) / Mathf.Max(0.01f, travelDuration));
            if (Mathf.Abs(nextProgress - targetProgress) <= ProgressSnapEpsilon)
            {
                nextProgress = targetProgress;
            }

            if (Mathf.Abs(nextProgress - progress) <= ProgressSnapEpsilon && !Mathf.Approximately(nextProgress, targetProgress))
            {
                return;
            }

            progress = nextProgress;
            Vector3 motionDelta = ApplyPlatformPosition();
            CarryPlayers(motionDelta);
            if (Mathf.Approximately(progress, targetProgress))
            {
                FinishMotionAudio();
            }
        }

        private void Reset()
        {
            ResolvePlatform();
            if (platform != null)
            {
                loweredLocalPosition = platform.localPosition;
            }

            EnsureWiringPort();
        }

        private void OnValidate()
        {
            travelDuration = Mathf.Max(0.01f, travelDuration);
            audioFadeSpeed = Mathf.Max(0f, audioFadeSpeed);
            audioMinDistance = Mathf.Max(0f, audioMinDistance);
            audioMaxDistance = Mathf.Max(0.1f, audioMaxDistance);
            ResolvePlatform();
            if (!Application.isPlaying && platform != null)
            {
                progress = startsRaised ? 1f : 0f;
                targetProgress = progress;
                ApplyPlatformPosition();
            }
        }

        private void InitializeState()
        {
            if (initialized)
            {
                return;
            }

            ResolvePlatform();
            progress = startsRaised ? 1f : 0f;
            targetProgress = progress;
            ApplyPlatformPosition();
            initialized = true;
        }

        private void ResolvePlatform()
        {
            if (platform != null)
            {
                return;
            }

            Transform candidate = transform.Find(DefaultPlatformName) ?? transform.Find(AlternatePlatformName);
            platform = candidate != null ? candidate : transform;
        }

        private Vector3 ApplyPlatformPosition()
        {
            if (platform == null)
            {
                return Vector3.zero;
            }

            Vector3 previousPosition = platform.position;
            platform.localPosition = loweredLocalPosition + raisedLocalOffset * Mathf.Clamp01(progress);
            return platform.position - previousPosition;
        }

        private void OnTriggerEnter(Collider other)
        {
            AddPotentialRider(other);
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerController player = other != null ? other.GetComponentInParent<PlayerController>() : null;
            if (player == null)
            {
                return;
            }

            CharacterController controller = carriedPlayers.TryGetValue(player, out CharacterController trackedController)
                ? trackedController
                : player.GetComponent<CharacterController>();
            if (!IsPlayerInsideMovingRideVolume(player, controller))
            {
                carriedPlayers.Remove(player);
            }
        }

        private void OnDisable()
        {
            carriedPlayers.Clear();
            staleCarriedPlayers.Clear();
            StopMotionAudio();
        }

        private void CarryPlayers(Vector3 motionDelta)
        {
            if (!carryPlayersInTrigger || motionDelta.sqrMagnitude <= MotionEpsilon * MotionEpsilon)
            {
                return;
            }

            RefreshRidersInsideMovingVolume();
            if (carriedPlayers.Count == 0)
            {
                return;
            }

            staleCarriedPlayers.Clear();
            bool movedAnyPlayer = false;
            foreach (KeyValuePair<PlayerController, CharacterController> entry in carriedPlayers)
            {
                PlayerController player = entry.Key;
                CharacterController controller = entry.Value;
                if (player == null
                    || controller == null
                    || !player.isActiveAndEnabled
                    || !controller.enabled
                    || !IsPlayerInsideMovingRideVolume(player, controller))
                {
                    staleCarriedPlayers.Add(player);
                    continue;
                }

                player.ApplyExternalDisplacement(motionDelta);
                movedAnyPlayer = true;
            }

            if (movedAnyPlayer)
            {
                Physics.SyncTransforms();
            }

            for (int i = 0; i < staleCarriedPlayers.Count; i++)
            {
                carriedPlayers.Remove(staleCarriedPlayers[i]);
            }
        }

        private void AddPotentialRider(Collider other)
        {
            if (!carryPlayersInTrigger)
            {
                return;
            }

            PlayerController player = other != null ? other.GetComponentInParent<PlayerController>() : null;
            if (player == null || carriedPlayers.ContainsKey(player))
            {
                return;
            }

            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                carriedPlayers.Add(player, controller);
            }
        }

        private void RefreshRidersInsideMovingVolume()
        {
            if (!TryGetMovingRideBounds(out Bounds rideBounds))
            {
                return;
            }

            Physics.SyncTransforms();
            int hitCount = Physics.OverlapBoxNonAlloc(
                rideBounds.center,
                rideBounds.extents,
                riderOverlapHits,
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hitCount; i++)
            {
                AddPotentialRider(riderOverlapHits[i]);
                riderOverlapHits[i] = null;
            }
        }

        private bool IsPlayerInsideMovingRideVolume(PlayerController player, CharacterController controller)
        {
            if (!TryGetMovingRideBounds(out Bounds rideBounds))
            {
                return true;
            }

            Bounds playerBounds = controller != null
                ? controller.bounds
                : new Bounds(player != null ? player.transform.position : Vector3.zero, Vector3.one * 0.5f);
            rideBounds.Expand(Mathf.Max(0.05f, controller != null ? controller.skinWidth * 2f : 0.05f));
            return rideBounds.Intersects(playerBounds);
        }

        private bool TryGetMovingRideBounds(out Bounds rideBounds)
        {
            ResolveRiderTrigger();
            if (riderTrigger == null || platform == null)
            {
                rideBounds = default;
                return false;
            }

            rideBounds = riderTrigger.bounds;
            rideBounds.center += platform.position - GetLoweredPlatformWorldPosition();
            return true;
        }

        private Vector3 GetLoweredPlatformWorldPosition()
        {
            return platform != null && platform.parent != null
                ? platform.parent.TransformPoint(loweredLocalPosition)
                : loweredLocalPosition;
        }

        private void ResolveRiderTrigger()
        {
            if (riderTrigger != null)
            {
                return;
            }

            BoxCollider[] boxColliders = GetComponents<BoxCollider>();
            for (int i = 0; i < boxColliders.Length; i++)
            {
                if (boxColliders[i] != null && boxColliders[i].isTrigger)
                {
                    riderTrigger = boxColliders[i];
                    return;
                }
            }
        }

        private void ResolveAudioSources()
        {
            ResolvePlatform();
            Transform audioAnchor = platform != null ? platform : transform;
            if (audioAnchor == null)
            {
                return;
            }

            if (motionLoopSource == null)
            {
                motionLoopSource = audioAnchor.GetComponent<AudioSource>();
            }

            if (motionLoopSource == null)
            {
                motionLoopSource = audioAnchor.gameObject.AddComponent<AudioSource>();
            }

            if (oneShotSource == null)
            {
                AudioSource[] sources = audioAnchor.GetComponents<AudioSource>();
                for (int i = 0; i < sources.Length; i++)
                {
                    if (sources[i] != null && sources[i] != motionLoopSource)
                    {
                        oneShotSource = sources[i];
                        break;
                    }
                }
            }

            if (oneShotSource == null)
            {
                oneShotSource = audioAnchor.gameObject.AddComponent<AudioSource>();
            }

            ConfigureAudioSource(motionLoopSource, true);
            ConfigureAudioSource(oneShotSource, false);
            if (motionLoopSource != null)
            {
                motionLoopSource.clip = GetMotionLoopClip();
                if (!motionLoopSource.isPlaying)
                {
                    motionLoopSource.volume = 0f;
                }
            }
        }

        private void ConfigureAudioSource(AudioSource source, bool loop)
        {
            if (source == null)
            {
                return;
            }

            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = audioMinDistance;
            source.maxDistance = audioMaxDistance;
            source.dopplerLevel = 0.15f;
        }

        private void BeginMotionAudio(int direction)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ResolveAudioSources();
            int normalizedDirection = direction >= 0 ? 1 : -1;
            if (motionAudioActive)
            {
                if (motionAudioDirection != normalizedDirection)
                {
                    motionAudioDirection = normalizedDirection;
                    ConfigureMotionLoopPitch(normalizedDirection);
                    PlayOneShot(GetInterruptClip(), interruptVolume, normalizedDirection > 0 ? 1.02f : 0.94f);
                }

                motionLoopRequested = true;
                return;
            }

            motionAudioActive = true;
            motionLoopRequested = true;
            motionAudioDirection = normalizedDirection;
            ConfigureMotionLoopPitch(normalizedDirection);
            PlayOneShot(GetStartClip(), startVolume, normalizedDirection > 0 ? 1.04f : 0.96f);

            if (motionLoopSource != null && motionLoopSource.clip != null && !motionLoopSource.isPlaying)
            {
                motionLoopSource.Play();
            }
        }

        private void FinishMotionAudio()
        {
            if (!Application.isPlaying || !motionAudioActive)
            {
                return;
            }

            PlayOneShot(GetArriveClip(), arriveVolume, motionAudioDirection > 0 ? 1.02f : 0.92f);
            motionAudioActive = false;
            motionLoopRequested = false;
            motionAudioDirection = 0;
        }

        private void StopMotionAudio()
        {
            motionAudioActive = false;
            motionLoopRequested = false;
            motionAudioDirection = 0;
            if (motionLoopSource == null)
            {
                return;
            }

            motionLoopSource.Stop();
            motionLoopSource.volume = 0f;
        }

        private void ConfigureMotionLoopPitch(int direction)
        {
            if (motionLoopSource == null)
            {
                return;
            }

            motionLoopSource.clip = GetMotionLoopClip();
            motionLoopSource.pitch = direction >= 0 ? 1.03f : 0.92f;
        }

        private void UpdateMotionLoopAudio(float deltaTime)
        {
            if (!Application.isPlaying || motionLoopSource == null)
            {
                return;
            }

            AudioClip loopClip = GetMotionLoopClip();
            if (motionLoopSource.clip != loopClip)
            {
                motionLoopSource.clip = loopClip;
            }

            float targetVolume = motionLoopRequested ? motionLoopVolume : 0f;
            motionLoopSource.volume = Mathf.MoveTowards(
                motionLoopSource.volume,
                targetVolume,
                Mathf.Max(0f, audioFadeSpeed) * Mathf.Max(0f, deltaTime));

            if (motionLoopRequested && motionLoopSource.clip != null && !motionLoopSource.isPlaying)
            {
                motionLoopSource.Play();
            }
            else if (!motionLoopRequested && motionLoopSource.volume <= 0.001f && motionLoopSource.isPlaying)
            {
                motionLoopSource.Stop();
            }
        }

        private void PlayOneShot(AudioClip clip, float volume, float basePitch)
        {
            if (!Application.isPlaying || oneShotSource == null || clip == null || volume <= 0f)
            {
                return;
            }

            float randomness = Mathf.Max(0f, pitchRandomness);
            oneShotSource.pitch = Mathf.Clamp(basePitch + Random.Range(-randomness, randomness), 0.2f, 2.5f);
            oneShotSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private AudioClip GetStartClip()
        {
            if (startClip != null)
            {
                return startClip;
            }

            if (generatedStartClip == null)
            {
                generatedStartClip = CreateGeneratedStartClip();
            }

            return generatedStartClip;
        }

        private AudioClip GetMotionLoopClip()
        {
            if (motionLoopClip != null)
            {
                return motionLoopClip;
            }

            if (generatedMotionLoopClip == null)
            {
                generatedMotionLoopClip = CreateGeneratedMotionLoopClip();
            }

            return generatedMotionLoopClip;
        }

        private AudioClip GetArriveClip()
        {
            if (arriveClip != null)
            {
                return arriveClip;
            }

            if (generatedArriveClip == null)
            {
                generatedArriveClip = CreateGeneratedArriveClip();
            }

            return generatedArriveClip;
        }

        private AudioClip GetInterruptClip()
        {
            if (interruptClip != null)
            {
                return interruptClip;
            }

            if (generatedInterruptClip == null)
            {
                generatedInterruptClip = CreateGeneratedInterruptClip();
            }

            return generatedInterruptClip;
        }

        private AudioClip CreateGeneratedStartClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.26f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float clickEnvelope = Mathf.Exp(-time * 36f);
                float motorEnvelope = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / duration)) * Mathf.Exp(-time * 1.4f);
                float click = Mathf.Sin(2f * Mathf.PI * 520f * time) * 0.32f
                    + Mathf.Sin(2f * Mathf.PI * 1180f * time) * 0.18f;
                float motor = Mathf.Sin(2f * Mathf.PI * 74f * time) * 0.18f
                    + Mathf.Sin(2f * Mathf.PI * 148f * time) * 0.08f;
                samples[i] = click * clickEnvelope + motor * motorEnvelope;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedElevatorStart", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateGeneratedMotionLoopClip()
        {
            const int sampleRate = 22050;
            const float duration = 1f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float wheelCycle = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * 3f * time);
                float motor = Mathf.Sin(2f * Mathf.PI * 72f * time) * 0.2f
                    + Mathf.Sin(2f * Mathf.PI * 144f * time) * 0.09f
                    + Mathf.Sin(2f * Mathf.PI * 216f * time) * 0.045f;
                float pulley = Mathf.Sin(2f * Mathf.PI * 420f * time) * 0.025f
                    + Mathf.Sin(2f * Mathf.PI * 610f * time) * 0.015f;
                samples[i] = motor * wheelCycle + pulley;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedElevatorLoop", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateGeneratedArriveClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.32f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Exp(-time * 14f);
                float thump = Mathf.Sin(2f * Mathf.PI * 58f * time) * 0.42f;
                float chain = Mathf.Sin(2f * Mathf.PI * 360f * time) * 0.16f
                    + Mathf.Sin(2f * Mathf.PI * 790f * time) * 0.09f;
                samples[i] = (thump + chain) * envelope;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedElevatorArrive", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateGeneratedInterruptClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.22f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Exp(-time * 18f);
                float brake = Mathf.Sin(2f * Mathf.PI * 230f * time) * 0.24f
                    + Mathf.Sin(2f * Mathf.PI * 930f * time) * 0.12f;
                samples[i] = brake * envelope;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedElevatorInterrupt", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void EnsureWiringPort()
        {
            HouseWireEndpoint endpoint = GetComponent<HouseWireEndpoint>();
            HouseWireInputRelay inputRelay = GetComponent<HouseWireInputRelay>();
            if (endpoint == null || inputRelay == null)
            {
                return;
            }

            endpoint.EnsureIdentity();
            if (!endpoint.TryGetPort(ActivatePortId, out HouseWirePortDefinition port))
            {
                port = endpoint.AddPort(
                    "Raise / Toggle",
                    HouseWirePortDirection.Input,
                    HouseSignalKind.Any,
                    visualOffset: Vector3.up,
                    requestedId: ActivatePortId);
            }

            port.OnSignalReceived.RemoveListener(inputRelay.Receive);
            port.OnSignalReceived.AddListener(inputRelay.Receive);
        }
    }
}
