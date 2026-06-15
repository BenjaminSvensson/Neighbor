using System;
using System.Collections.Generic;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    public enum ItemSoundProfile
    {
        MechanicalSlide,
        FabricMove,
        HingedWood,
        ScrewTurn,
        MetalDetach,
        WoodPry,
        SwitchClick,
        TrapSnap,
        TrapDoorOpen,
        SpringLaunch,
        Impact,
        SawStart,
        Alarm,
        BookOpen,
        WetSquash
    }

    [Serializable]
    public sealed class ItemSoundOverride
    {
        public ItemSoundProfile profile;
        public AudioClip[] clips;
    }

    public sealed class ItemAudioFeedback : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private ItemSoundOverride[] soundOverrides;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.05f;
        [SerializeField, Min(0f)] private float minDistance = 0.4f;
        [SerializeField, Min(0.1f)] private float maxDistance = 12f;

        private readonly Dictionary<ItemSoundProfile, AudioClip> generatedClips = new();

        public static ItemAudioFeedback Resolve(GameObject owner)
        {
            if (owner == null)
            {
                return null;
            }

            ItemAudioFeedback feedback = owner.GetComponent<ItemAudioFeedback>();
            return feedback != null ? feedback : owner.AddComponent<ItemAudioFeedback>();
        }

        private void Awake()
        {
            ResolveAudioSource();
        }

        public void Play(ItemSoundProfile profile, float volume = 0.6f)
        {
            if (volume <= 0f)
            {
                return;
            }

            ResolveAudioSource();
            AudioClip clip = GetOverride(profile) ?? GetGeneratedClip(profile);
            if (audioSource == null || clip == null)
            {
                return;
            }

            audioSource.pitch = UnityEngine.Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
            audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private AudioClip GetOverride(ItemSoundProfile profile)
        {
            if (soundOverrides == null)
            {
                return null;
            }

            for (int i = 0; i < soundOverrides.Length; i++)
            {
                ItemSoundOverride soundOverride = soundOverrides[i];
                if (soundOverride == null || soundOverride.profile != profile || soundOverride.clips == null)
                {
                    continue;
                }

                int startIndex = UnityEngine.Random.Range(0, soundOverride.clips.Length);
                for (int j = 0; j < soundOverride.clips.Length; j++)
                {
                    AudioClip clip = soundOverride.clips[(startIndex + j) % soundOverride.clips.Length];
                    if (clip != null)
                    {
                        return clip;
                    }
                }
            }

            return null;
        }

        private AudioClip GetGeneratedClip(ItemSoundProfile profile)
        {
            if (generatedClips.TryGetValue(profile, out AudioClip clip) && clip != null)
            {
                return clip;
            }

            clip = CreateGeneratedClip(profile);
            generatedClips[profile] = clip;
            return clip;
        }

        private AudioClip CreateGeneratedClip(ItemSoundProfile profile)
        {
            const int sampleRate = 22050;
            float duration = profile switch
            {
                ItemSoundProfile.MechanicalSlide => 0.28f,
                ItemSoundProfile.FabricMove => 0.34f,
                ItemSoundProfile.HingedWood => 0.26f,
                ItemSoundProfile.ScrewTurn => 0.2f,
                ItemSoundProfile.MetalDetach => 0.24f,
                ItemSoundProfile.WoodPry => 0.3f,
                ItemSoundProfile.TrapSnap => 0.18f,
                ItemSoundProfile.TrapDoorOpen => 0.34f,
                ItemSoundProfile.SpringLaunch => 0.22f,
                ItemSoundProfile.Alarm => 0.48f,
                ItemSoundProfile.BookOpen => 0.24f,
                ItemSoundProfile.WetSquash => 0.3f,
                _ => 0.12f
            };

            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            int seed = name.GetHashCode() ^ (int)profile * 7919;
            System.Random random = new(seed);

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float progress = time / duration;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                samples[i] = GenerateSample(profile, time, progress, noise);
            }

            AudioClip clip = AudioClip.Create($"{name}_Generated{profile}", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float GenerateSample(ItemSoundProfile profile, float time, float progress, float noise)
        {
            float decay = Mathf.Exp(-progress * 6f);
            float fade = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
            float tone(float frequency) => Mathf.Sin(2f * Mathf.PI * frequency * time);

            return profile switch
            {
                ItemSoundProfile.MechanicalSlide => (noise * 0.18f + tone(95f) * 0.12f) * fade,
                ItemSoundProfile.FabricMove => noise * 0.2f * fade * (0.65f + 0.35f * tone(7f)),
                ItemSoundProfile.HingedWood => (tone(115f + progress * 60f) * 0.2f + noise * 0.08f) * fade,
                ItemSoundProfile.ScrewTurn => (tone(620f) * 0.12f + noise * 0.18f) * fade * (tone(18f) > 0f ? 1f : 0.35f),
                ItemSoundProfile.MetalDetach => (tone(440f) * 0.24f + tone(910f) * 0.12f + noise * 0.12f) * decay,
                ItemSoundProfile.WoodPry => (tone(120f - progress * 45f) * 0.26f + noise * 0.22f) * fade,
                ItemSoundProfile.SwitchClick => (tone(1450f) * 0.25f + noise * 0.18f) * decay,
                ItemSoundProfile.TrapSnap => (tone(180f) * 0.38f + noise * 0.28f) * decay,
                ItemSoundProfile.TrapDoorOpen => (tone(75f) * 0.28f + noise * 0.16f) * fade,
                ItemSoundProfile.SpringLaunch => (tone(260f + progress * 520f) * 0.25f + noise * 0.12f) * fade,
                ItemSoundProfile.Impact => (tone(105f) * 0.32f + noise * 0.2f) * decay,
                ItemSoundProfile.SawStart => (tone(180f + progress * 520f) * 0.18f + noise * 0.12f) * fade,
                ItemSoundProfile.Alarm => tone(1050f) * (tone(7f) > 0f ? 0.3f : 0.08f) * fade,
                ItemSoundProfile.BookOpen => noise * 0.16f * fade * (0.6f + 0.4f * tone(11f)),
                ItemSoundProfile.WetSquash => (noise * 0.25f + tone(70f - progress * 35f) * 0.22f) * fade,
                _ => noise * 0.15f * decay
            };
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
            audioSource.loop = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = minDistance;
            audioSource.maxDistance = maxDistance;
            audioSource.dopplerLevel = 0.08f;
        }

        private void OnDestroy()
        {
            foreach (AudioClip clip in generatedClips.Values)
            {
                if (clip != null)
                {
                    Destroy(clip);
                }
            }
        }
    }
}
