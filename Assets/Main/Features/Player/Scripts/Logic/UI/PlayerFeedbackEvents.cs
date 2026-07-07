using System;
using UnityEngine;

namespace Neighbor.Main.Features.Player
{
    public static class PlayerFeedbackEvents
    {
        public readonly struct NoiseFeedback
        {
            public Vector3 Origin { get; }
            public float Loudness { get; }
            public float Radius { get; }

            public NoiseFeedback(Vector3 origin, float loudness, float radius)
            {
                Origin = origin;
                Loudness = Mathf.Clamp01(loudness);
                Radius = Mathf.Max(0f, radius);
            }
        }

        public readonly struct SecurityEscalationFeedback
        {
            public int Level { get; }
            public int Budget { get; }
            public int DoorCount { get; }
            public int LocationCount { get; }
            public int CameraCount { get; }
            public int TrapCount { get; }
            public int PatrolPointCount { get; }

            public SecurityEscalationFeedback(int level, int budget, int doorCount, int locationCount)
                : this(level, budget, doorCount, locationCount, 0, 0, 0)
            {
            }

            public SecurityEscalationFeedback(
                int level,
                int budget,
                int doorCount,
                int locationCount,
                int cameraCount,
                int trapCount,
                int patrolPointCount)
            {
                Level = Mathf.Max(0, level);
                Budget = Mathf.Max(0, budget);
                DoorCount = Mathf.Max(0, doorCount);
                LocationCount = Mathf.Max(0, locationCount);
                CameraCount = Mathf.Max(0, cameraCount);
                TrapCount = Mathf.Max(0, trapCount);
                PatrolPointCount = Mathf.Max(0, patrolPointCount);
            }
        }

        public readonly struct AmbienceZoneFeedback
        {
            public string Message { get; }
            public float Intensity { get; }

            public AmbienceZoneFeedback(string message, float intensity)
            {
                Message = message;
                Intensity = Mathf.Clamp01(intensity);
            }
        }

        public readonly struct DayPhaseFeedback
        {
            public string Phase { get; }
            public string Message { get; }
            public float Intensity { get; }
            public float TimeOfDay { get; }

            public DayPhaseFeedback(string phase, string message, float intensity, float timeOfDay)
            {
                Phase = phase;
                Message = message;
                Intensity = Mathf.Clamp01(intensity);
                TimeOfDay = Mathf.Repeat(timeOfDay, 1f);
            }
        }

        public readonly struct CheckpointFeedback
        {
            public string Name { get; }

            public CheckpointFeedback(string name)
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Checkpoint" : name.Trim();
            }
        }

        public readonly struct RespawnFeedback
        {
            public string Name { get; }
            public bool UsedCheckpoint { get; }
            public Vector3 Position { get; }

            public RespawnFeedback(string name, bool usedCheckpoint, Vector3 position)
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Start" : name.Trim();
                UsedCheckpoint = usedCheckpoint;
                Position = position;
            }
        }

        public readonly struct OnboardingPromptFeedback
        {
            public string Message { get; }
            public float Intensity { get; }

            public OnboardingPromptFeedback(string message, float intensity)
            {
                Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
                Intensity = Mathf.Clamp01(intensity);
            }
        }

        public readonly struct ObjectiveProgressFeedback
        {
            public int StepIndex { get; }
            public int TotalSteps { get; }
            public string Message { get; }
            public string Hint { get; }
            public bool IsComplete { get; }

            public ObjectiveProgressFeedback(
                int stepIndex,
                int totalSteps,
                string message,
                string hint,
                bool isComplete)
            {
                StepIndex = Mathf.Max(1, stepIndex);
                TotalSteps = Mathf.Max(StepIndex, totalSteps);
                Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
                Hint = string.IsNullOrWhiteSpace(hint) ? string.Empty : hint.Trim();
                IsComplete = isComplete;
            }
        }

        public enum DoorInteractionFeedbackKind
        {
            Info,
            Locked,
            Blocked,
            Unlocked
        }

        public readonly struct DoorInteractionFeedback
        {
            public string Message { get; }
            public DoorInteractionFeedbackKind Kind { get; }
            public float Intensity { get; }

            public DoorInteractionFeedback(string message, DoorInteractionFeedbackKind kind, float intensity)
            {
                Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
                Kind = kind;
                Intensity = Mathf.Clamp01(intensity);
            }
        }

        public static event Action<NoiseFeedback> NoiseEmitted;
        public static event Action CameraDetectedPlayer;
        public static event Action<SecurityEscalationFeedback> SecurityEscalated;
        public static event Action<AmbienceZoneFeedback> AmbienceZoneChanged;
        public static event Action<DayPhaseFeedback> DayPhaseChanged;
        public static event Action<CheckpointFeedback> CheckpointReached;
        public static event Action<RespawnFeedback> PlayerRespawned;
        public static event Action<OnboardingPromptFeedback> OnboardingPrompted;
        public static event Action<ObjectiveProgressFeedback> ObjectiveProgressed;
        public static event Action<DoorInteractionFeedback> DoorInteractionReported;

        public static void ReportNoise(Vector3 origin, float loudness, float radius)
        {
            NoiseEmitted?.Invoke(new NoiseFeedback(origin, loudness, radius));
        }

        public static void ReportCameraDetection()
        {
            CameraDetectedPlayer?.Invoke();
        }

        public static void ReportSecurityEscalation(int level, int budget, int doorCount, int locationCount)
        {
            SecurityEscalated?.Invoke(new SecurityEscalationFeedback(level, budget, doorCount, locationCount));
        }

        public static void ReportSecurityEscalation(
            int level,
            int budget,
            int doorCount,
            int locationCount,
            int cameraCount,
            int trapCount,
            int patrolPointCount)
        {
            SecurityEscalated?.Invoke(new SecurityEscalationFeedback(
                level,
                budget,
                doorCount,
                locationCount,
                cameraCount,
                trapCount,
                patrolPointCount));
        }

        public static void ReportAmbienceZone(string message, float intensity)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            AmbienceZoneChanged?.Invoke(new AmbienceZoneFeedback(message, intensity));
        }

        public static void ReportDayPhase(string phase, string message, float intensity, float timeOfDay)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            DayPhaseChanged?.Invoke(new DayPhaseFeedback(phase, message, intensity, timeOfDay));
        }

        public static void ReportCheckpoint(string checkpointName)
        {
            CheckpointReached?.Invoke(new CheckpointFeedback(checkpointName));
        }

        public static void ReportRespawn(string respawnName, bool usedCheckpoint, Vector3 position)
        {
            PlayerRespawned?.Invoke(new RespawnFeedback(respawnName, usedCheckpoint, position));
        }

        public static void ReportOnboardingPrompt(string message, float intensity = 0.35f)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            OnboardingPrompted?.Invoke(new OnboardingPromptFeedback(message, intensity));
        }

        public static void ReportObjectiveProgress(
            int stepIndex,
            int totalSteps,
            string message,
            string hint,
            bool isComplete)
        {
            if (string.IsNullOrWhiteSpace(message) && string.IsNullOrWhiteSpace(hint))
            {
                return;
            }

            ObjectiveProgressed?.Invoke(new ObjectiveProgressFeedback(stepIndex, totalSteps, message, hint, isComplete));
        }

        public static void ReportDoorInteraction(
            string message,
            DoorInteractionFeedbackKind kind = DoorInteractionFeedbackKind.Info,
            float intensity = 0.5f)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            DoorInteractionReported?.Invoke(new DoorInteractionFeedback(message, kind, intensity));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            NoiseEmitted = null;
            CameraDetectedPlayer = null;
            SecurityEscalated = null;
            AmbienceZoneChanged = null;
            DayPhaseChanged = null;
            CheckpointReached = null;
            PlayerRespawned = null;
            OnboardingPrompted = null;
            ObjectiveProgressed = null;
            DoorInteractionReported = null;
        }
    }
}
