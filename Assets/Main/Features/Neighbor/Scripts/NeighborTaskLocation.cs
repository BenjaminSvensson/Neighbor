using System.Collections.Generic;
using Neighbor.Main.Features.Interaction;
using UnityEngine;

namespace Neighbor.Main.Features.Neighbor
{
    public sealed class NeighborTaskLocation : MonoBehaviour
    {
        public enum TaskAnimationPhase
        {
            None,
            Starting,
            Performing,
            Finishing
        }

        public enum TaskAudioPlaybackMode
        {
            OneShot,
            LoopUntilTaskFinished
        }

        public enum ObjectTaskType
        {
            None,
            Sit,
            Sleep,
            Repair
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

        [Header("Object Task")]
        [SerializeField] private ObjectTaskType objectTaskType;
        [SerializeField] private Transform navigationPoint;
        [SerializeField] private Vector3 navigationLocalOffset;
        [SerializeField] private Transform usePose;
        [SerializeField] private Vector3 usePoseLocalOffset;
        [SerializeField] private Vector3 usePoseLocalEulerAngles;
        [SerializeField] private GameObject taskObjectRoot;
        [SerializeField] private Rigidbody taskObjectBody;
        [SerializeField] private bool anchorNeighborAtUsePose;
        [SerializeField] private bool ignoreTaskObjectCollisions;
        [SerializeField] private bool stabilizeTaskObject;
        [SerializeField, Range(0f, 1f)] private float minimumUprightDot = 0.9f;
        [SerializeField, Min(0f)] private float maximumUseVerticalOffset = 0.45f;
        [SerializeField, Min(0f)] private float maximumObjectSpeedForUse = 0.2f;
        [SerializeField, Min(0f)] private float maximumObjectAngularSpeedForUse = 0.35f;

        [Header("Task Animation")]
        [Tooltip("Optional one-shot animation played after arriving, before the task begins.")]
        [SerializeField] private AnimationClip startTaskAnimation;
        [SerializeField, Min(0.05f)] private float startAnimationPlaybackSpeed = 1f;
        [Tooltip("Optional animation played while performing this task. Leave blank to use the generic task animation.")]
        [SerializeField] private AnimationClip taskAnimation;
        [SerializeField, Min(0.05f)] private float animationPlaybackSpeed = 1f;
        [Tooltip("Optional one-shot animation played after the task finishes, before the Neighbor leaves.")]
        [SerializeField] private AnimationClip endTaskAnimation;
        [SerializeField, Min(0.05f)] private float endAnimationPlaybackSpeed = 1f;

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
        private NeighborBrain reservedBy;
        private Pickupable taskPickupable;
        private DoorBlockerChair taskDoorBlocker;
        private Collider[] taskObjectColliders;
        private Collider[] reservedNeighborColliders;
        private bool taskUseActive;
        private bool bodyStateCaptured;
        private bool bodyWasKinematic;
        private bool bodyWasUsingGravity;

        public Vector3 Position => navigationPoint != null ? navigationPoint.position : transform.TransformPoint(navigationLocalOffset);
        public Vector3 UsePosition => usePose != null ? usePose.position : transform.TransformPoint(usePoseLocalOffset);
        public Quaternion UseRotation => usePose != null
            ? usePose.rotation
            : transform.rotation * Quaternion.Euler(usePoseLocalEulerAngles);
        public Vector3 LookDirection => UseRotation * Vector3.forward;
        public float RandomWaitTime => Random.Range(minimumWaitTime, Mathf.Max(minimumWaitTime, maximumWaitTime));
        public bool CanRepeatImmediately => canRepeatImmediately;
        public float SelectionPriority => selectionPriority;
        public NeighborTaskLocation ForcedNextTask => forcedNextTask;
        public float ArrivalDistance => arrivalDistance;
        public float MaximumUseVerticalOffset => maximumUseVerticalOffset;
        public float NavigationSampleRadius => navigationSampleRadius;
        public ObjectTaskType TaskType => objectTaskType;
        public bool IsObjectPoseUsable => IsTaskObjectPoseUsable();
        public bool NeedsObjectRecovery => objectTaskType == ObjectTaskType.Sit
            && (taskPickupable == null || !taskPickupable.IsHeld)
            && (taskDoorBlocker == null || !taskDoorBlocker.IsBlockingDoor)
            && !IsTaskObjectPoseUsable();
        public bool IsAvailable => reservedBy == null
            && (taskPickupable == null || !taskPickupable.IsHeld)
            && IsTaskObjectPoseUsable();
        public static IReadOnlyList<NeighborTaskLocation> Locations => ActiveLocations;

        public AnimationClip GetAnimation(TaskAnimationPhase phase)
        {
            return phase switch
            {
                TaskAnimationPhase.Starting => startTaskAnimation,
                TaskAnimationPhase.Performing => taskAnimation,
                TaskAnimationPhase.Finishing => endTaskAnimation,
                _ => null
            };
        }

        public float GetAnimationPlaybackSpeed(TaskAnimationPhase phase)
        {
            return phase switch
            {
                TaskAnimationPhase.Starting => startAnimationPlaybackSpeed,
                TaskAnimationPhase.Performing => animationPlaybackSpeed,
                TaskAnimationPhase.Finishing => endAnimationPlaybackSpeed,
                _ => 1f
            };
        }

        public float GetAnimationDuration(TaskAnimationPhase phase)
        {
            AnimationClip clip = GetAnimation(phase);
            return clip != null ? clip.length / Mathf.Max(0.05f, GetAnimationPlaybackSpeed(phase)) : 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveLocations()
        {
            ActiveLocations.Clear();
        }

        private void Awake()
        {
            ResolveAudioSource();
            ResolveTaskObject();
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
            ReleaseReservation(reservedBy, reservedBy != null ? reservedBy.GetComponent<NeighborMotor>() : null);
            ActiveLocations.Remove(this);
            StopTaskAudio();
        }

        private void Update()
        {
            if (ReferenceEquals(reservedBy, null))
            {
                return;
            }

            if (reservedBy == null
                || reservedBy.CurrentState != NeighborBrain.BehaviorState.Task
                || reservedBy.CurrentTaskLocation != this
                || taskPickupable != null && taskPickupable.IsHeld)
            {
                NeighborMotor motor = reservedBy != null ? reservedBy.GetComponent<NeighborMotor>() : null;
                ReleaseReservation(reservedBy, motor);
            }
        }

        public bool TryReserve(NeighborBrain neighbor)
        {
            ResolveTaskObject();
            if (!ReferenceEquals(reservedBy, null) && reservedBy == null)
            {
                ReleaseReservation(null, null);
            }

            if (neighbor == null
                || reservedBy != null && reservedBy != neighbor
                || taskPickupable != null && taskPickupable.IsHeld
                || !IsTaskObjectPoseUsable())
            {
                return false;
            }

            reservedBy = neighbor;
            ApplyTaskObjectProtection(neighbor);
            return true;
        }

        public bool BeginTaskUse(NeighborBrain neighbor, NeighborMotor motor)
        {
            if (neighbor == null
                || reservedBy != neighbor
                || motor == null
                || motor.IsTraversingSpecialMove
                || !IsTaskObjectPoseUsable()
                || Mathf.Abs(neighbor.transform.position.y - Position.y) > maximumUseVerticalOffset)
            {
                return false;
            }

            taskUseActive = true;
            if (anchorNeighborAtUsePose)
            {
                motor?.BeginAnchoredTask(UsePosition, UseRotation);
            }

            return true;
        }

        public void EndTaskUse(NeighborBrain neighbor, NeighborMotor motor)
        {
            ReleaseReservation(neighbor, motor);
        }

        public static void ReleaseAllFor(NeighborBrain neighbor, NeighborMotor motor)
        {
            if (neighbor == null)
            {
                return;
            }

            for (int i = ActiveLocations.Count - 1; i >= 0; i--)
            {
                NeighborTaskLocation location = ActiveLocations[i];
                if (location != null && location.reservedBy == neighbor)
                {
                    location.ReleaseReservation(neighbor, motor);
                }
            }
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
            maximumUseVerticalOffset = Mathf.Max(0f, maximumUseVerticalOffset);
            maximumObjectSpeedForUse = Mathf.Max(0f, maximumObjectSpeedForUse);
            maximumObjectAngularSpeedForUse = Mathf.Max(0f, maximumObjectAngularSpeedForUse);
            startAnimationPlaybackSpeed = Mathf.Max(0.05f, startAnimationPlaybackSpeed);
            animationPlaybackSpeed = Mathf.Max(0.05f, animationPlaybackSpeed);
            endAnimationPlaybackSpeed = Mathf.Max(0.05f, endAnimationPlaybackSpeed);
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
            Vector3 start = Position + Vector3.up * 0.08f;
            Vector3 direction = LookDirection.sqrMagnitude > 0.001f ? LookDirection.normalized : Vector3.forward;
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

        private void ResolveTaskObject()
        {
            GameObject root = taskObjectRoot != null
                ? taskObjectRoot
                : gameObject;
            taskPickupable = root.GetComponentInParent<Pickupable>() ?? root.GetComponentInChildren<Pickupable>();
            taskDoorBlocker = root.GetComponentInParent<DoorBlockerChair>()
                ?? root.GetComponentInChildren<DoorBlockerChair>();
            taskObjectBody = taskObjectBody != null
                ? taskObjectBody
                : root.GetComponentInParent<Rigidbody>() ?? root.GetComponentInChildren<Rigidbody>();
            taskObjectColliders = root.GetComponentsInChildren<Collider>(true);
        }

        private bool IsTaskObjectPoseUsable()
        {
            if (objectTaskType != ObjectTaskType.Sit)
            {
                return true;
            }

            if (taskDoorBlocker != null && taskDoorBlocker.IsBlockingDoor)
            {
                return false;
            }

            if (taskObjectBody == null)
            {
                return true;
            }

            Transform objectTransform = taskObjectBody.transform;
            if (Vector3.Dot(objectTransform.up, Vector3.up) < minimumUprightDot)
            {
                return false;
            }

            return taskObjectBody.linearVelocity.sqrMagnitude
                    <= maximumObjectSpeedForUse * maximumObjectSpeedForUse
                && taskObjectBody.angularVelocity.sqrMagnitude
                    <= maximumObjectAngularSpeedForUse * maximumObjectAngularSpeedForUse;
        }

        private void ApplyTaskObjectProtection(NeighborBrain neighbor)
        {
            if (neighbor == null)
            {
                return;
            }

            if (ignoreTaskObjectCollisions)
            {
                reservedNeighborColliders = neighbor.GetComponentsInChildren<Collider>(true);
                SetTaskCollisionIgnored(true);
            }

            if (stabilizeTaskObject && taskObjectBody != null && !bodyStateCaptured)
            {
                bodyWasKinematic = taskObjectBody.isKinematic;
                bodyWasUsingGravity = taskObjectBody.useGravity;
                bodyStateCaptured = true;
                taskObjectBody.linearVelocity = Vector3.zero;
                taskObjectBody.angularVelocity = Vector3.zero;
                taskObjectBody.isKinematic = true;
                taskObjectBody.useGravity = false;
            }
        }

        private void ReleaseReservation(NeighborBrain neighbor, NeighborMotor motor)
        {
            if (ReferenceEquals(reservedBy, null) || neighbor != null && reservedBy != neighbor)
            {
                return;
            }

            if (taskUseActive && anchorNeighborAtUsePose)
            {
                Quaternion exitRotation = LookDirection.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(LookDirection, Vector3.up)
                    : reservedBy != null ? reservedBy.transform.rotation : UseRotation;
                motor?.EndAnchoredTask(Position, exitRotation);
            }

            SetTaskCollisionIgnored(false);
            if (bodyStateCaptured && taskObjectBody != null)
            {
                taskObjectBody.isKinematic = bodyWasKinematic;
                taskObjectBody.useGravity = bodyWasUsingGravity;
            }

            bodyStateCaptured = false;
            taskUseActive = false;
            reservedNeighborColliders = null;
            reservedBy = null;
        }

        private void SetTaskCollisionIgnored(bool ignored)
        {
            if (taskObjectColliders == null || reservedNeighborColliders == null)
            {
                return;
            }

            for (int objectIndex = 0; objectIndex < taskObjectColliders.Length; objectIndex++)
            {
                Collider objectCollider = taskObjectColliders[objectIndex];
                if (objectCollider == null || objectCollider.isTrigger)
                {
                    continue;
                }

                for (int neighborIndex = 0; neighborIndex < reservedNeighborColliders.Length; neighborIndex++)
                {
                    Collider neighborCollider = reservedNeighborColliders[neighborIndex];
                    if (neighborCollider != null && neighborCollider != objectCollider)
                    {
                        Physics.IgnoreCollision(objectCollider, neighborCollider, ignored);
                    }
                }
            }
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
