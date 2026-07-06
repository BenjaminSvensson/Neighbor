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

            public SecurityEscalationFeedback(int level, int budget, int doorCount, int locationCount)
            {
                Level = Mathf.Max(0, level);
                Budget = Mathf.Max(0, budget);
                DoorCount = Mathf.Max(0, doorCount);
                LocationCount = Mathf.Max(0, locationCount);
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

        public static event Action<NoiseFeedback> NoiseEmitted;
        public static event Action CameraDetectedPlayer;
        public static event Action<SecurityEscalationFeedback> SecurityEscalated;
        public static event Action<AmbienceZoneFeedback> AmbienceZoneChanged;

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

        public static void ReportAmbienceZone(string message, float intensity)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            AmbienceZoneChanged?.Invoke(new AmbienceZoneFeedback(message, intensity));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            NoiseEmitted = null;
            CameraDetectedPlayer = null;
            SecurityEscalated = null;
            AmbienceZoneChanged = null;
        }
    }
}
