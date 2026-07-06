using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Neighbor.Main.Features.Audio
{
    [CreateAssetMenu(fileName = "AmbienceProfile", menuName = "Neighbor/Audio/Ambience Profile")]
    public sealed class AmbienceProfile : ScriptableObject
    {
        [SerializeField, Min(0.01f)] private float transitionDuration = 2f;
        [SerializeField] private AudioMixerGroup outputMixerGroup;
        [SerializeField] private AmbienceLayer[] layers = Array.Empty<AmbienceLayer>();
        [Header("Zone Feel")]
        [SerializeField, Range(500f, 22000f)] private float listenerLowPassCutoff = 22000f;
        [SerializeField] private AudioReverbPreset listenerReverbPreset = AudioReverbPreset.Off;
        [SerializeField, Range(0f, 1f)] private float zoneWarningIntensity;
        [SerializeField] private string zoneWarningText;

        public float TransitionDuration => transitionDuration;
        public AudioMixerGroup OutputMixerGroup => outputMixerGroup;
        public AmbienceLayer[] Layers => layers;
        public float ListenerLowPassCutoff => Mathf.Clamp(listenerLowPassCutoff, 500f, 22000f);
        public AudioReverbPreset ListenerReverbPreset => listenerReverbPreset;
        public float ZoneWarningIntensity => Mathf.Clamp01(zoneWarningIntensity);
        public string ZoneWarningText => zoneWarningText;

        private void OnValidate()
        {
            transitionDuration = Mathf.Max(0.01f, transitionDuration);
            listenerLowPassCutoff = Mathf.Clamp(listenerLowPassCutoff, 500f, 22000f);
        }
    }

    [Serializable]
    public sealed class AmbienceLayer
    {
        [SerializeField] private string name = "Ambience Layer";
        [Tooltip("One clip is chosen when this profile starts. Add separate layers to play sounds simultaneously.")]
        [SerializeField] private AudioClip[] clipVariations = Array.Empty<AudioClip>();
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField, Range(-3f, 3f)] private float pitch = 1f;
        [SerializeField] private bool startAtRandomTime = true;

        public string Name => name;
        public AudioClip[] ClipVariations => clipVariations;
        public float Volume => volume;
        public float Pitch => pitch;
        public float PlaybackPitch => Mathf.Approximately(pitch, 0f) ? 1f : pitch;
        public bool StartAtRandomTime => startAtRandomTime;
    }
}
