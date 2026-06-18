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

        public float TransitionDuration => transitionDuration;
        public AudioMixerGroup OutputMixerGroup => outputMixerGroup;
        public AmbienceLayer[] Layers => layers;

        private void OnValidate()
        {
            transitionDuration = Mathf.Max(0.01f, transitionDuration);
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
