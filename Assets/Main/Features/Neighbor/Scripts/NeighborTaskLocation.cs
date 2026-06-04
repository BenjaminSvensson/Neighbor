using System.Collections.Generic;
using UnityEngine;

namespace Neighbor.Main.Features.Neighbor
{
    public sealed class NeighborTaskLocation : MonoBehaviour
    {
        public enum TaskAudioPlaybackMode
        {
            OneShot,
            LoopUntilTaskFinished
        }

        private static readonly List<NeighborTaskLocation> ActiveLocations = new();

        [Header("Task")]
        [SerializeField, Min(0f)] private float minimumWaitTime = 1.5f;
        [SerializeField, Min(0f)] private float maximumWaitTime = 5f;
        [SerializeField] private bool canRepeatImmediately;

        [Header("Task Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] taskClips;
        [SerializeField] private TaskAudioPlaybackMode audioPlaybackMode = TaskAudioPlaybackMode.OneShot;
        [SerializeField, Range(0f, 1f)] private float audioVolume = 0.65f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.04f;
        [SerializeField, Min(0f)] private float audioMinDistance = 0.4f;
        [SerializeField, Min(0.1f)] private float audioMaxDistance = 12f;

        private AudioClip activeLoopClip;

        public Vector3 Position => transform.position;
        public float RandomWaitTime => Random.Range(minimumWaitTime, Mathf.Max(minimumWaitTime, maximumWaitTime));
        public bool CanRepeatImmediately => canRepeatImmediately;
        public static IReadOnlyList<NeighborTaskLocation> Locations => ActiveLocations;

        private void Awake()
        {
            ResolveAudioSource();
        }

        private void OnEnable()
        {
            if (!ActiveLocations.Contains(this))
            {
                ActiveLocations.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveLocations.Remove(this);
            StopTaskAudio();
        }

        public void BeginTaskAudio()
        {
            AudioClip clip = GetTaskClip();
            if (clip == null || audioSource == null)
            {
                return;
            }

            audioSource.pitch = Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
            audioSource.volume = audioVolume;

            if (audioPlaybackMode == TaskAudioPlaybackMode.LoopUntilTaskFinished)
            {
                activeLoopClip = clip;
                audioSource.clip = activeLoopClip;
                audioSource.loop = true;
                if (!audioSource.isPlaying)
                {
                    audioSource.Play();
                }

                return;
            }

            audioSource.loop = false;
            audioSource.PlayOneShot(clip, audioVolume);
        }

        public void StopTaskAudio()
        {
            if (audioSource == null)
            {
                return;
            }

            if (activeLoopClip != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }

            if (audioSource.clip == activeLoopClip)
            {
                audioSource.clip = null;
            }

            activeLoopClip = null;
        }

        private void OnValidate()
        {
            maximumWaitTime = Mathf.Max(minimumWaitTime, maximumWaitTime);
            audioMaxDistance = Mathf.Max(0.1f, audioMaxDistance);
            audioMinDistance = Mathf.Min(audioMinDistance, audioMaxDistance);
        }

        private AudioClip GetTaskClip()
        {
            if (taskClips == null || taskClips.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < taskClips.Length; i++)
            {
                AudioClip clip = taskClips[Random.Range(0, taskClips.Length)];
                if (clip != null)
                {
                    return clip;
                }
            }

            return null;
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
            audioSource.minDistance = audioMinDistance;
            audioSource.maxDistance = audioMaxDistance;
            audioSource.dopplerLevel = 0.05f;
        }
    }
}
