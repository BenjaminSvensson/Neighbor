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
        [SerializeField, Min(0f)] private float selectionPriority = 1f;
        [SerializeField] private bool canRepeatImmediately;
        [SerializeField] private NeighborTaskLocation forcedNextTask;
        [SerializeField, Min(0.1f)] private float arrivalDistance = 0.75f;
        [SerializeField, Min(0f)] private float navigationSampleRadius = 1.5f;
        [SerializeField, Min(0.1f)] private float lookArrowLength = 1.2f;
        [SerializeField, Min(0.05f)] private float lookArrowHeadSize = 0.25f;
        [SerializeField] private Color lookArrowColor = new Color(0.1f, 0.85f, 1f, 0.9f);

        [Header("Task Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] taskStartClips;
        [SerializeField] private AudioClip[] taskClips;
        [SerializeField] private AudioClip[] taskFinishClips;
        [SerializeField] private TaskAudioPlaybackMode audioPlaybackMode = TaskAudioPlaybackMode.OneShot;
        [SerializeField, Range(0f, 1f)] private float audioVolume = 0.65f;
        [SerializeField, Range(0f, 1f)] private float startAudioVolume = 0.65f;
        [SerializeField, Range(0f, 1f)] private float finishAudioVolume = 0.65f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.04f;
        [SerializeField, Min(0f)] private float audioMinDistance = 0.4f;
        [SerializeField, Min(0.1f)] private float audioMaxDistance = 12f;

        private AudioClip activeLoopClip;

        public Vector3 Position => transform.position;
        public Vector3 LookDirection => transform.forward;
        public float RandomWaitTime => Random.Range(minimumWaitTime, Mathf.Max(minimumWaitTime, maximumWaitTime));
        public bool CanRepeatImmediately => canRepeatImmediately;
        public float SelectionPriority => selectionPriority;
        public NeighborTaskLocation ForcedNextTask => forcedNextTask;
        public float ArrivalDistance => arrivalDistance;
        public float NavigationSampleRadius => navigationSampleRadius;
        public static IReadOnlyList<NeighborTaskLocation> Locations => ActiveLocations;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveLocations()
        {
            ActiveLocations.Clear();
        }

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
            PlayOneShot(taskStartClips, startAudioVolume);

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

        public void StopTaskAudio(bool playFinishSound = false)
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

            if (playFinishSound)
            {
                PlayOneShot(taskFinishClips, finishAudioVolume);
            }
        }

        private void OnValidate()
        {
            maximumWaitTime = Mathf.Max(minimumWaitTime, maximumWaitTime);
            arrivalDistance = Mathf.Max(0.1f, arrivalDistance);
            audioMaxDistance = Mathf.Max(0.1f, audioMaxDistance);
            audioMinDistance = Mathf.Min(audioMinDistance, audioMaxDistance);
        }

        private void OnDrawGizmos()
        {
            DrawLookDirectionGizmo(false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawLookDirectionGizmo(true);
        }

        private void DrawLookDirectionGizmo(bool selected)
        {
            float arrowLength = Mathf.Max(0.1f, lookArrowLength);
            float arrowHeadSize = Mathf.Max(0.05f, lookArrowHeadSize);
            Vector3 start = transform.position + Vector3.up * 0.08f;
            Vector3 direction = transform.forward.sqrMagnitude > 0.001f ? transform.forward.normalized : Vector3.forward;
            Vector3 end = start + direction * arrowLength;

            Color previousColor = Gizmos.color;
            Color color = lookArrowColor;
            if (!selected)
            {
                color.a *= 0.65f;
            }

            Gizmos.color = color;
            Gizmos.DrawSphere(start, selected ? 0.12f : 0.08f);
            Gizmos.DrawLine(start, end);

            Quaternion headRotation = Quaternion.LookRotation(direction, Vector3.up);
            Vector3 left = headRotation * Quaternion.Euler(0f, 150f, 0f) * Vector3.forward;
            Vector3 right = headRotation * Quaternion.Euler(0f, -150f, 0f) * Vector3.forward;
            Gizmos.DrawLine(end, end + left * arrowHeadSize);
            Gizmos.DrawLine(end, end + right * arrowHeadSize);

            Gizmos.color = previousColor;
        }

        private AudioClip GetTaskClip()
        {
            return GetRandomClip(taskClips);
        }

        private void PlayOneShot(AudioClip[] clips, float volume)
        {
            AudioClip clip = GetRandomClip(clips);
            if (clip == null || audioSource == null)
            {
                return;
            }

            audioSource.loop = false;
            audioSource.pitch = Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
            audioSource.PlayOneShot(clip, volume);
        }

        private AudioClip GetRandomClip(AudioClip[] clips)
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
