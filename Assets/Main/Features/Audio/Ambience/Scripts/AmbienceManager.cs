using System.Collections.Generic;
using Neighbor.Main.Features.Player;
using UnityEngine;
using UnityEngine.Audio;

namespace Neighbor.Main.Features.Audio
{
    public sealed class AmbienceManager : MonoBehaviour
    {
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
        private AmbienceProfile targetProfile;
        private float nextListenerSearchTime;
        private float nextPlayerSearchTime;

        private void Awake()
        {
            ResolveListener();
            ResolvePlayer();
        }

        private void OnDisable()
        {
            StopAllPlaybacks();
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

            AmbienceProfile desiredProfile = GetDesiredProfile();
            if (desiredProfile != targetProfile)
            {
                TransitionTo(desiredProfile);
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

        private void ResolveListener()
        {
            AudioListener audioListener = FindAnyObjectByType<AudioListener>();
            if (audioListener != null)
            {
                listener = audioListener.transform;
                return;
            }

            if (Camera.main != null)
            {
                listener = Camera.main.transform;
            }
        }

        private void ResolvePlayer()
        {
            player = FindAnyObjectByType<PlayerController>();
        }

        private AmbienceProfile GetDesiredProfile()
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
                return bestArea.Profile;
            }

            return bestDefaultArea != null ? bestDefaultArea.Profile : defaultProfile;
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
                Destroy(root);
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
                source.pitch = layer.Pitch;
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
                Destroy(root);
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
                    Destroy(playback.Root);
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
                    Destroy(playbacks[i].Root);
                }
            }

            playbacks.Clear();
            targetProfile = null;
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
    }
}
