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

        public static event Action<NoiseFeedback> NoiseEmitted;
        public static event Action CameraDetectedPlayer;
        public static event Action<SecurityEscalationFeedback> SecurityEscalated;
        public static event Action<AmbienceZoneFeedback> AmbienceZoneChanged;
        public static event Action<DayPhaseFeedback> DayPhaseChanged;
        public static event Action<CheckpointFeedback> CheckpointReached;

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            NoiseEmitted = null;
            CameraDetectedPlayer = null;
            SecurityEscalated = null;
            AmbienceZoneChanged = null;
            DayPhaseChanged = null;
            CheckpointReached = null;
        }
    }
}
