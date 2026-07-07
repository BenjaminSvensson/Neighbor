using System.Collections.Generic;
using Neighbor.Main.Features.Player;
using UnityEngine;
using UnityEngine.Audio;

namespace Neighbor.Main.Features.Audio
{
    public sealed class AmbienceManager : MonoBehaviour
    {
        private const float NeutralLowPassCutoff = 21999f;

        [Header("References")]
        [Tooltip("Usually the player camera or AudioListener. Automatically resolved when empty.")]
        [SerializeField] private Transform listener;
        [Tooltip("The player whose trigger presence drives area ambience. Automatically resolved when empty.")]
        [SerializeField] private PlayerController player;
        [SerializeField] private AmbienceProfile defaultProfile;
        [SerializeField] private AudioMixerGroup fallbackOutputMixerGroup;

        [Header("Playback")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Min(0.01f)] private float fallbackTransitionDuration = 2f;

        private readonly List<ProfilePlayback> playbacks = new List<ProfilePlayback>();
        private static float activeNoiseLoudnessMultiplier = 1f;
        private static float activeNoiseRadiusMultiplier = 1f;
        private AmbienceProfile targetProfile;
        private AudioLowPassFilter listenerLowPassFilter;
        private AudioReverbFilter listenerReverbFilter;
        private AmbienceZoneLocation lastReportedZoneLocation = (AmbienceZoneLocation)(-1);
        private AmbienceProfile lastReportedZoneProfile;
        private float nextListenerSearchTime;
        private float nextPlayerSearchTime;

        public AmbienceZoneLocation CurrentZoneLocation { get; private set; } = AmbienceZoneLocation.Outside;
        public static float ActiveNoiseLoudnessMultiplier => activeNoiseLoudnessMultiplier;
        public static float ActiveNoiseRadiusMultiplier => activeNoiseRadiusMultiplier;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticAcousticState()
        {
            ResetNoiseAcousticModifiers();
        }

        private void Awake()
        {
            ResolveListener();
            ResolvePlayer();
        }

        private void OnDisable()
        {
            StopAllPlaybacks();
            ClearListenerZoneFeel();
        }

        private void Update()
        {
            if (listener == null && Time.unscaledTime >= nextListenerSearchTime)
            {
                ResolveListener();
                nextListenerSearchTime = Time.unscaledTime + 1f;
            }

            if (player == null && Time.unscaledTime >= nextPlayerSearchTime)
            {
                ResolvePlayer();
                nextPlayerSearchTime = Time.unscaledTime + 1f;
            }

            DesiredAmbienceState desiredState = GetDesiredState();
            CurrentZoneLocation = desiredState.ZoneLocation;
            ApplyListenerZoneFeel(desiredState);
            ApplyZoneAcoustics(desiredState);
            ReportZoneChange(desiredState);
            if (desiredState.Profile != targetProfile)
            {
                TransitionTo(desiredState.Profile);
            }

            UpdatePlaybacks();
        }

        public void SetListener(Transform newListener)
        {
            listener = newListener;
        }

        public void SetPlayer(PlayerController newPlayer)
        {
            player = newPlayer;
        }

        public static float ModifyNoiseLoudness(float loudness01)
        {
            return Mathf.Clamp01(loudness01 * activeNoiseLoudnessMultiplier);
        }

        public static float ModifyNoiseRadius(float radius)
        {
            return Mathf.Max(0f, radius * activeNoiseRadiusMultiplier);
        }

        public static void ResetNoiseAcousticModifiers()
        {
            activeNoiseLoudnessMultiplier = 1f;
            activeNoiseRadiusMultiplier = 1f;
        }

        private void ResolveListener()
        {
            AudioListener audioListener = FindAnyObjectByType<AudioListener>();
            if (audioListener != null)
            {
                listener = audioListener.transform;
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                listener = mainCamera.transform;
            }
        }

        private void ResolvePlayer()
        {
            player = FindAnyObjectByType<PlayerController>();
        }

        private AmbienceProfile GetDesiredProfile()
        {
            return GetDesiredState().Profile;
        }

        private AmbienceZoneLocation GetDesiredZoneLocation()
        {
            return GetDesiredState().ZoneLocation;
        }

        private DesiredAmbienceState GetDesiredState()
        {
            AmbienceArea bestArea = null;
            AmbienceArea bestDefaultArea = null;
            Transform trackingTransform = player != null ? player.transform : listener;
            IReadOnlyList<AmbienceArea> areas = AmbienceArea.Areas;
            for (int i = areas.Count - 1; i >= 0; i--)
            {
                AmbienceArea area = areas[i];
                if (area == null || area.Profile == null)
                {
                    continue;
                }

                if (area.PlayWhenNoAreaActive)
                {
                    if (bestDefaultArea == null || area.Priority > bestDefaultArea.Priority)
                    {
                        bestDefaultArea = area;
                    }

                    continue;
                }

                if (!area.IsActiveFor(trackingTransform))
                {
                    continue;
                }

                if (bestArea == null || area.Priority > bestArea.Priority)
                {
                    bestArea = area;
                }
            }

            if (bestArea != null)
            {
                return new DesiredAmbienceState(bestArea.Profile, bestArea.ZoneLocation);
            }

            if (bestDefaultArea != null)
            {
                return new DesiredAmbienceState(bestDefaultArea.Profile, bestDefaultArea.ZoneLocation);
            }

            return new DesiredAmbienceState(defaultProfile, AmbienceZoneLocation.Outside);
        }

        private void ApplyListenerZoneFeel(DesiredAmbienceState desiredState)
        {
            ReleaseStaleListenerFilters();

            if (listener == null || !CanUseAudioFilters(listener))
            {
                return;
            }

            float cutoff = GetEffectiveListenerLowPassCutoff(desiredState.Profile, desiredState.ZoneLocation);
            AudioReverbPreset reverbPreset = GetEffectiveListenerReverbPreset(
                desiredState.Profile,
                desiredState.ZoneLocation);

            if (cutoff < NeutralLowPassCutoff || listenerLowPassFilter != null)
            {
                bool createdFilter = false;
                if (listenerLowPassFilter == null)
                {
                    listenerLowPassFilter = listener.GetComponent<AudioLowPassFilter>();
                }

                if (listenerLowPassFilter == null)
                {
                    listenerLowPassFilter = listener.gameObject.AddComponent<AudioLowPassFilter>();
                    createdFilter = true;
                }

                listenerLowPassFilter.enabled = cutoff < NeutralLowPassCutoff;
                listenerLowPassFilter.cutoffFrequency = createdFilter
                    ? cutoff
                    : Mathf.MoveTowards(
                        listenerLowPassFilter.cutoffFrequency,
                        cutoff,
                        Time.unscaledDeltaTime * 12000f);
            }

            if (reverbPreset != AudioReverbPreset.Off || listenerReverbFilter != null)
            {
                if (listenerReverbFilter == null)
                {
                    listenerReverbFilter = listener.GetComponent<AudioReverbFilter>();
                }

                if (listenerReverbFilter == null)
                {
                    listenerReverbFilter = listener.gameObject.AddComponent<AudioReverbFilter>();
                }

                listenerReverbFilter.reverbPreset = reverbPreset;
                listenerReverbFilter.enabled = reverbPreset != AudioReverbPreset.Off;
            }
        }

        private void ClearListenerZoneFeel()
        {
            if (listenerLowPassFilter != null)
            {
                listenerLowPassFilter.enabled = false;
                listenerLowPassFilter = null;
            }

            if (listenerReverbFilter != null)
            {
                listenerReverbFilter.enabled = false;
                listenerReverbFilter = null;
            }
        }

        private static void ApplyZoneAcoustics(DesiredAmbienceState desiredState)
        {
            float profileLoudness = desiredState.Profile != null
                ? desiredState.Profile.NoiseLoudnessMultiplier
                : 1f;
            float profileRadius = desiredState.Profile != null
                ? desiredState.Profile.NoiseRadiusMultiplier
                : 1f;

            activeNoiseLoudnessMultiplier = Mathf.Clamp(
                profileLoudness * GetDefaultZoneNoiseLoudnessMultiplier(desiredState.ZoneLocation),
                0.1f,
                4f);
            activeNoiseRadiusMultiplier = Mathf.Clamp(
                profileRadius * GetDefaultZoneNoiseRadiusMultiplier(desiredState.ZoneLocation),
                0.1f,
                4f);
        }

        private void ReleaseStaleListenerFilters()
        {
            if (listenerLowPassFilter != null && listenerLowPassFilter.transform != listener)
            {
                listenerLowPassFilter.enabled = false;
                listenerLowPassFilter = null;
            }

            if (listenerReverbFilter != null && listenerReverbFilter.transform != listener)
            {
                listenerReverbFilter.enabled = false;
                listenerReverbFilter = null;
            }
        }

        private static bool CanUseAudioFilters(Transform candidate)
        {
            return candidate != null
                && (candidate.GetComponent<AudioListener>() != null || candidate.GetComponent<AudioSource>() != null);
        }

        private static float GetEffectiveListenerLowPassCutoff(
            AmbienceProfile profile,
            AmbienceZoneLocation zoneLocation)
        {
            if (profile != null && profile.ListenerLowPassCutoff < NeutralLowPassCutoff)
            {
                return profile.ListenerLowPassCutoff;
            }

            return GetDefaultZoneLowPassCutoff(zoneLocation);
        }

        private static AudioReverbPreset GetEffectiveListenerReverbPreset(
            AmbienceProfile profile,
            AmbienceZoneLocation zoneLocation)
        {
            if (profile != null && profile.ListenerReverbPreset != AudioReverbPreset.Off)
            {
                return profile.ListenerReverbPreset;
            }

            return GetDefaultZoneReverbPreset(zoneLocation);
        }

        private void ReportZoneChange(DesiredAmbienceState desiredState)
        {
            if (desiredState.ZoneLocation == lastReportedZoneLocation && desiredState.Profile == lastReportedZoneProfile)
            {
                return;
            }

            lastReportedZoneLocation = desiredState.ZoneLocation;
            lastReportedZoneProfile = desiredState.Profile;

            string warningText = desiredState.Profile != null
                ? desiredState.Profile.ZoneWarningText
                : null;
            float warningIntensity = desiredState.Profile != null
                ? desiredState.Profile.ZoneWarningIntensity
                : 0f;

            if (string.IsNullOrWhiteSpace(warningText))
            {
                warningText = GetDefaultZoneWarning(desiredState.ZoneLocation);
                warningIntensity = Mathf.Max(warningIntensity, GetDefaultZoneWarningIntensity(desiredState.ZoneLocation));
            }

            if (warningIntensity <= 0f || string.IsNullOrWhiteSpace(warningText))
            {
                return;
            }

            PlayerFeedbackEvents.ReportAmbienceZone(warningText, warningIntensity);
        }

        private static string GetDefaultZoneWarning(AmbienceZoneLocation zoneLocation)
        {
            return zoneLocation switch
            {
                AmbienceZoneLocation.Basement => "BASEMENT AIR FEELS HEAVY",
                AmbienceZoneLocation.Garage => "GARAGE ECHOES",
                _ => null
            };
        }

        private static float GetDefaultZoneWarningIntensity(AmbienceZoneLocation zoneLocation)
        {
            return zoneLocation switch
            {
                AmbienceZoneLocation.Basement => 0.72f,
                AmbienceZoneLocation.Garage => 0.45f,
                _ => 0f
            };
        }

        private static float GetDefaultZoneLowPassCutoff(AmbienceZoneLocation zoneLocation)
        {
            return zoneLocation switch
            {
                AmbienceZoneLocation.Inside => 16000f,
                AmbienceZoneLocation.Basement => 2600f,
                AmbienceZoneLocation.Garage => 8500f,
                _ => 22000f
            };
        }

        private static AudioReverbPreset GetDefaultZoneReverbPreset(AmbienceZoneLocation zoneLocation)
        {
            return zoneLocation switch
            {
                AmbienceZoneLocation.Inside => AudioReverbPreset.Room,
                AmbienceZoneLocation.Basement => AudioReverbPreset.Cave,
                AmbienceZoneLocation.Garage => AudioReverbPreset.ParkingLot,
                _ => AudioReverbPreset.Off
            };
        }

        private static float GetDefaultZoneNoiseLoudnessMultiplier(AmbienceZoneLocation zoneLocation)
        {
            return zoneLocation switch
            {
                AmbienceZoneLocation.Basement => 1.18f,
                AmbienceZoneLocation.Garage => 1.12f,
                _ => 1f
            };
        }

        private static float GetDefaultZoneNoiseRadiusMultiplier(AmbienceZoneLocation zoneLocation)
        {
            return zoneLocation switch
            {
                AmbienceZoneLocation.Basement => 1.2f,
                AmbienceZoneLocation.Garage => 1.35f,
                _ => 1f
            };
        }

        private void TransitionTo(AmbienceProfile profile)
        {
            targetProfile = profile;

            for (int i = 0; i < playbacks.Count; i++)
            {
                playbacks[i].TargetGain = playbacks[i].Profile == profile ? 1f : 0f;
            }

            if (profile == null || FindPlayback(profile) != null)
            {
                return;
            }

            ProfilePlayback playback = CreatePlayback(profile);
            if (playback != null)
            {
                playbacks.Add(playback);
            }
        }

        private ProfilePlayback FindPlayback(AmbienceProfile profile)
        {
            for (int i = 0; i < playbacks.Count; i++)
            {
                if (playbacks[i].Profile == profile)
                {
                    return playbacks[i];
                }
            }

            return null;
        }

        private ProfilePlayback CreatePlayback(AmbienceProfile profile)
        {
            GameObject root = new GameObject(profile.name);
            root.transform.SetParent(transform, false);
            ProfilePlayback playback = new ProfilePlayback(profile, root, GetTransitionDuration(profile));
            AmbienceLayer[] layers = profile.Layers;

            if (layers == null)
            {
                DestroyPlaybackRoot(root);
                return null;
            }

            for (int i = 0; i < layers.Length; i++)
            {
                AmbienceLayer layer = layers[i];
                if (layer == null)
                {
                    continue;
                }

                AudioClip clip = GetRandomClip(layer.ClipVariations);
                if (clip == null)
                {
                    continue;
                }

                GameObject layerObject = new GameObject(string.IsNullOrWhiteSpace(layer.Name) ? $"Layer {i + 1}" : layer.Name);
                layerObject.transform.SetParent(root.transform, false);
                AudioSource source = layerObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                source.clip = clip;
                source.pitch = layer.PlaybackPitch;
                source.outputAudioMixerGroup = profile.OutputMixerGroup != null
                    ? profile.OutputMixerGroup
                    : fallbackOutputMixerGroup;
                source.volume = 0f;

                if (layer.StartAtRandomTime && clip.length > 0f)
                {
                    source.time = Random.Range(0f, clip.length);
                }

                source.Play();
                playback.Layers.Add(new LayerPlayback(source, layer.Volume));
            }

            if (playback.Layers.Count == 0)
            {
                DestroyPlaybackRoot(root);
                return null;
            }

            return playback;
        }

        private void UpdatePlaybacks()
        {
            for (int i = playbacks.Count - 1; i >= 0; i--)
            {
                ProfilePlayback playback = playbacks[i];
                playback.TransitionDuration = GetTransitionDuration(playback.Profile);
                playback.Gain = Mathf.MoveTowards(
                    playback.Gain,
                    playback.TargetGain,
                    Time.unscaledDeltaTime / playback.TransitionDuration);

                for (int layerIndex = 0; layerIndex < playback.Layers.Count; layerIndex++)
                {
                    LayerPlayback layer = playback.Layers[layerIndex];
                    layer.Source.volume = masterVolume * layer.Volume * playback.Gain;
                }

                if (playback.TargetGain <= 0f && playback.Gain <= 0f)
                {
                    DestroyPlaybackRoot(playback.Root);
                    playbacks.RemoveAt(i);
                }
            }
        }

        private float GetTransitionDuration(AmbienceProfile profile)
        {
            return Mathf.Max(0.01f, profile != null ? profile.TransitionDuration : fallbackTransitionDuration);
        }

        private static AudioClip GetRandomClip(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
            {
                return null;
            }

            int startIndex = Random.Range(0, clips.Length);
            for (int i = 0; i < clips.Length; i++)
            {
                AudioClip clip = clips[(startIndex + i) % clips.Length];
                if (clip != null)
                {
                    return clip;
                }
            }

            return null;
        }

        private void StopAllPlaybacks()
        {
            for (int i = 0; i < playbacks.Count; i++)
            {
                if (playbacks[i].Root != null)
                {
                    DestroyPlaybackRoot(playbacks[i].Root);
                }
            }

            playbacks.Clear();
            targetProfile = null;
            ResetNoiseAcousticModifiers();
        }

        private static void DestroyPlaybackRoot(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(root);
            }
            else
            {
                Object.DestroyImmediate(root);
            }
        }

        private sealed class ProfilePlayback
        {
            public ProfilePlayback(AmbienceProfile profile, GameObject root, float transitionDuration)
            {
                Profile = profile;
                Root = root;
                TransitionDuration = transitionDuration;
                TargetGain = 1f;
            }

            public AmbienceProfile Profile { get; }
            public GameObject Root { get; }
            public List<LayerPlayback> Layers { get; } = new List<LayerPlayback>();
            public float Gain { get; set; }
            public float TargetGain { get; set; }
            public float TransitionDuration { get; set; }
        }

        private readonly struct LayerPlayback
        {
            public LayerPlayback(AudioSource source, float volume)
            {
                Source = source;
                Volume = volume;
            }

            public AudioSource Source { get; }
            public float Volume { get; }
        }

        private readonly struct DesiredAmbienceState
        {
            public DesiredAmbienceState(AmbienceProfile profile, AmbienceZoneLocation zoneLocation)
            {
                Profile = profile;
                ZoneLocation = zoneLocation;
            }

            public AmbienceProfile Profile { get; }
            public AmbienceZoneLocation ZoneLocation { get; }
        }
    }
}
