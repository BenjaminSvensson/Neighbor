using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Neighbor.Main.HouseBuilder
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Neighbor/House Builder/Wiring/Garage Door Motion")]
    public sealed class HouseGarageDoorMotion : MonoBehaviour, IHouseWireSignalReceiver
    {
        private const string DefaultPanelName = "Door Panel";

        [SerializeField] private Transform doorPanel;
        [SerializeField] private Vector3 closedLocalPosition = new(0f, 1.25f, 0f);
        [SerializeField] private Vector3 openLocalOffset = new(0f, 2.35f, 0f);
        [SerializeField, Min(0.01f)] private float travelDuration = 0.8f;
        [SerializeField] private bool startsOpen;
        [Header("Audio")]
        [SerializeField] private AudioSource oneShotSource;
        [SerializeField] private AudioSource loopSource;
        [SerializeField] private AudioClip[] openStartClips;
        [SerializeField] private AudioClip[] closeStartClips;
        [SerializeField] private AudioClip movingLoopClip;
        [SerializeField] private AudioClip[] stopClips;
        [SerializeField, Range(0f, 1f)] private float startVolume = 0.75f;
        [SerializeField, Range(0f, 1f)] private float loopVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] private float stopVolume = 0.65f;
        [SerializeField, Range(0f, 0.25f)] private float pitchRandomness = 0.04f;
        [SerializeField, Min(0.01f)] private float audioMinDistance = 0.8f;
        [SerializeField, Min(0.01f)] private float audioMaxDistance = 12f;
        [Header("Navigation")]
        [SerializeField] private bool enableNavigationPassage = true;
        [SerializeField, Min(0.1f)] private float navigationLinkWidth = 2.6f;
        [SerializeField, Min(0.1f)] private float navigationLinkHalfDepth = 1.15f;
        [SerializeField, Min(0f)] private float navigationLinkHeight = 0.05f;
        [SerializeField, Range(0f, 1f)] private float navigationOpenProgress = 0.78f;
        [SerializeField] private NavMeshLink navigationLink;
        [SerializeField] private bool carveClosedPanel = true;
        [SerializeField, Min(0f)] private float navigationObstaclePadding = 0.05f;
        [SerializeField] private NavMeshObstacle panelNavigationObstacle;

        private float progress;
        private float targetProgress;
        private bool initialized;
        private AudioClip generatedOpenStartClip;
        private AudioClip generatedCloseStartClip;
        private AudioClip generatedMovingLoopClip;
        private AudioClip generatedStopClip;

        public bool IsOpen => targetProgress >= 1f;
        public bool AllowsNavigationPassage => IsNavigationPassageOpen();

        public void Configure(Transform panel, Vector3 closedPosition, Vector3 openOffset, float duration, bool initiallyOpen)
        {
            doorPanel = panel;
            closedLocalPosition = closedPosition;
            openLocalOffset = openOffset;
            travelDuration = Mathf.Max(0.01f, duration);
            startsOpen = initiallyOpen;
            initialized = false;
            InitializeState();
        }

        public void Toggle() => SetOpen(!IsOpen);
        public void Open() => SetOpen(true);
        public void Close() => SetOpen(false);

        public void SetOpen(bool open)
        {
            InitializeState();
            float nextTarget = open ? 1f : 0f;
            bool changedTarget = !Mathf.Approximately(targetProgress, nextTarget);
            targetProgress = nextTarget;
            if (Application.isPlaying && changedTarget)
            {
                PlayStartSound(open);
            }

            if (!Application.isPlaying)
            {
                progress = targetProgress;
                ApplyPanelPosition();
            }

            UpdateNavigationState();
        }

        public void ReceiveHouseWireSignal(HouseSignal signal)
        {
            if (signal == null)
            {
                return;
            }

            Toggle();
        }

        private void Awake() => InitializeState();

        private void Update()
        {
            if (doorPanel == null || Mathf.Approximately(progress, targetProgress))
            {
                StopMovingLoop(false);
                UpdateNavigationState();
                return;
            }

            StartMovingLoop();
            progress = Mathf.MoveTowards(progress, targetProgress, Time.deltaTime / Mathf.Max(0.01f, travelDuration));
            ApplyPanelPosition();
            UpdateNavigationState();
            if (Mathf.Approximately(progress, targetProgress))
            {
                StopMovingLoop(true);
            }
        }

        private void Reset()
        {
            ResolvePanel();
            if (doorPanel != null)
            {
                closedLocalPosition = doorPanel.localPosition;
            }
        }

        private void OnValidate()
        {
            travelDuration = Mathf.Max(0.01f, travelDuration);
            audioMaxDistance = Mathf.Max(audioMinDistance, audioMaxDistance);
            ResolvePanel();
            if (!Application.isPlaying && doorPanel != null)
            {
                progress = startsOpen ? 1f : 0f;
                targetProgress = progress;
                ApplyPanelPosition();
                ConfigureExistingNavigationComponents();
            }
        }

        private void InitializeState()
        {
            if (initialized)
            {
                return;
            }

            ResolvePanel();
            progress = startsOpen ? 1f : 0f;
            targetProgress = progress;
            ApplyPanelPosition();
            ResolveNavigationComponents();
            ConfigureNavigationComponents();
            UpdateNavigationState();
            initialized = true;
        }

        private void ResolvePanel()
        {
            if (doorPanel != null)
            {
                return;
            }

            Transform candidate = transform.Find(DefaultPanelName);
            if (candidate != null)
            {
                doorPanel = candidate;
            }
        }

        private void ApplyPanelPosition()
        {
            if (doorPanel == null)
            {
                return;
            }

            doorPanel.localPosition = closedLocalPosition + (openLocalOffset * Mathf.Clamp01(progress));
        }

        private void OnDisable()
        {
            StopMovingLoop(false);
            if (navigationLink != null)
            {
                navigationLink.activated = false;
            }

            if (panelNavigationObstacle != null)
            {
                panelNavigationObstacle.enabled = false;
            }
        }

        private void ResolveNavigationComponents()
        {
            if (!enableNavigationPassage)
            {
                return;
            }

            ResolvePanel();
            if (navigationLink == null)
            {
                navigationLink = GetComponent<NavMeshLink>();
            }

            if (navigationLink == null)
            {
                navigationLink = gameObject.AddComponent<NavMeshLink>();
            }

            if (panelNavigationObstacle == null && doorPanel != null)
            {
                panelNavigationObstacle = doorPanel.GetComponent<NavMeshObstacle>();
            }

            if (panelNavigationObstacle == null && doorPanel != null)
            {
                BoxCollider panelCollider = doorPanel.GetComponent<BoxCollider>();
                if (panelCollider != null && !panelCollider.isTrigger)
                {
                    panelNavigationObstacle = doorPanel.gameObject.AddComponent<NavMeshObstacle>();
                }
            }
        }

        private void ConfigureExistingNavigationComponents()
        {
            if (navigationLink == null)
            {
                navigationLink = GetComponent<NavMeshLink>();
            }

            if (panelNavigationObstacle == null && doorPanel != null)
            {
                panelNavigationObstacle = doorPanel.GetComponent<NavMeshObstacle>();
            }

            ConfigureNavigationComponents();
            UpdateNavigationState();
        }

        private void ConfigureNavigationComponents()
        {
            if (navigationLink != null)
            {
                navigationLink.startPoint = new Vector3(0f, navigationLinkHeight, -navigationLinkHalfDepth);
                navigationLink.endPoint = new Vector3(0f, navigationLinkHeight, navigationLinkHalfDepth);
                navigationLink.width = navigationLinkWidth;
                navigationLink.bidirectional = true;
                navigationLink.costModifier = -1f;
                navigationLink.autoUpdate = false;
            }

            if (panelNavigationObstacle == null)
            {
                return;
            }

            BoxCollider panelCollider = panelNavigationObstacle.GetComponent<BoxCollider>();
            if (panelCollider == null)
            {
                return;
            }

            panelNavigationObstacle.shape = NavMeshObstacleShape.Box;
            panelNavigationObstacle.center = panelCollider.center;
            panelNavigationObstacle.size = panelCollider.size + Vector3.one * navigationObstaclePadding;
            panelNavigationObstacle.carving = true;
            panelNavigationObstacle.carveOnlyStationary = false;
            panelNavigationObstacle.carvingMoveThreshold = 0.03f;
            panelNavigationObstacle.carvingTimeToStationary = 0.03f;
        }

        private void UpdateNavigationState()
        {
            bool passageOpen = IsNavigationPassageOpen();
            if (navigationLink != null)
            {
                if (!enableNavigationPassage)
                {
                    navigationLink.activated = false;
                    navigationLink.enabled = false;
                }
                else
                {
                    navigationLink.enabled = true;
                    navigationLink.activated = passageOpen;
                }
            }

            if (panelNavigationObstacle != null)
            {
                panelNavigationObstacle.enabled = enableNavigationPassage && carveClosedPanel && !passageOpen;
            }
        }

        private bool IsNavigationPassageOpen()
        {
            return enableNavigationPassage
                && targetProgress >= 1f
                && progress >= navigationOpenProgress;
        }

        private void PlayStartSound(bool opening)
        {
            AudioClip clip = GetStartClip(opening);
            PlayOneShot(clip, startVolume);
        }

        private void StartMovingLoop()
        {
            ResolveAudioSources();
            if (loopSource == null)
            {
                return;
            }

            AudioClip clip = movingLoopClip != null ? movingLoopClip : GetGeneratedMovingLoopClip();
            if (clip == null)
            {
                return;
            }

            if (loopSource.clip != clip)
            {
                loopSource.clip = clip;
            }

            loopSource.volume = loopVolume;
            loopSource.loop = true;
            if (!loopSource.isPlaying)
            {
                loopSource.pitch = Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
                loopSource.Play();
            }
        }

        private void StopMovingLoop(bool playStopSound)
        {
            if (loopSource != null && loopSource.isPlaying)
            {
                loopSource.Stop();
            }

            if (playStopSound)
            {
                PlayOneShot(GetStopClip(), stopVolume);
            }
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            ResolveAudioSources();
            if (oneShotSource == null || clip == null)
            {
                return;
            }

            oneShotSource.pitch = Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
            oneShotSource.PlayOneShot(clip, volume);
        }

        private AudioClip GetStartClip(bool opening)
        {
            AudioClip[] clips = opening ? openStartClips : closeStartClips;
            if (clips != null && clips.Length > 0)
            {
                return clips[Random.Range(0, clips.Length)];
            }

            if (opening)
            {
                generatedOpenStartClip ??= CreateGeneratedStartClip(true);
                return generatedOpenStartClip;
            }

            generatedCloseStartClip ??= CreateGeneratedStartClip(false);
            return generatedCloseStartClip;
        }

        private AudioClip GetStopClip()
        {
            if (stopClips != null && stopClips.Length > 0)
            {
                return stopClips[Random.Range(0, stopClips.Length)];
            }

            generatedStopClip ??= CreateGeneratedStopClip();
            return generatedStopClip;
        }

        private AudioClip GetGeneratedMovingLoopClip()
        {
            generatedMovingLoopClip ??= CreateGeneratedMovingLoopClip();
            return generatedMovingLoopClip;
        }

        private AudioClip CreateGeneratedStartClip(bool opening)
        {
            const int sampleRate = 22050;
            const float duration = 0.16f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            float baseFrequency = opening ? 90f : 72f;

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Exp(-time * 10f);
                float motor = Mathf.Sin(2f * Mathf.PI * baseFrequency * time) * 0.34f;
                float clank = Mathf.Sin(2f * Mathf.PI * 420f * time) * Mathf.Exp(-time * 35f) * 0.22f;
                samples[i] = (motor + clank) * envelope;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedGarageDoor{(opening ? "Open" : "Close")}Start", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateGeneratedMovingLoopClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.4f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float motor = Mathf.Sin(2f * Mathf.PI * 60f * time) * 0.23f;
                float belt = Mathf.Sin(2f * Mathf.PI * 180f * time) * 0.08f;
                float shimmer = Mathf.Sin(2f * Mathf.PI * 360f * time) * 0.035f;
                samples[i] = motor + belt + shimmer;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedGarageDoorMoveLoop", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateGeneratedStopClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.14f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Exp(-time * 38f);
                float thud = Mathf.Sin(2f * Mathf.PI * 95f * time) * 0.45f;
                float metal = Mathf.Sin(2f * Mathf.PI * 520f * time) * 0.12f;
                samples[i] = (thud + metal) * envelope;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedGarageDoorStop", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void ResolveAudioSources()
        {
            if (oneShotSource == null || loopSource == null)
            {
                AudioSource[] sources = GetComponents<AudioSource>();
                if (oneShotSource == null && sources.Length > 0)
                {
                    oneShotSource = sources[0];
                }

                if (loopSource == null)
                {
                    for (int i = 0; i < sources.Length; i++)
                    {
                        if (sources[i] != null && sources[i] != oneShotSource)
                        {
                            loopSource = sources[i];
                            break;
                        }
                    }
                }
            }

            if (oneShotSource == null)
            {
                oneShotSource = gameObject.AddComponent<AudioSource>();
            }

            if (loopSource == null)
            {
                loopSource = gameObject.AddComponent<AudioSource>();
            }

            ConfigureAudioSource(oneShotSource, false);
            ConfigureAudioSource(loopSource, true);
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
            source.dopplerLevel = 0.05f;
        }
    }
}
