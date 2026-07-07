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
            public float Urgency { get; }
            public bool HeardByNeighbor { get; }
            public int NeighborListenerCount { get; }

            public NoiseFeedback(Vector3 origin, float loudness, float radius)
                : this(origin, loudness, radius, loudness, false, 0)
            {
            }

            public NoiseFeedback(
                Vector3 origin,
                float loudness,
                float radius,
                float urgency,
                bool heardByNeighbor,
                int neighborListenerCount)
            {
                Origin = origin;
                Loudness = Mathf.Clamp01(loudness);
                Radius = Mathf.Max(0f, radius);
                Urgency = Mathf.Clamp01(urgency);
                HeardByNeighbor = heardByNeighbor;
                NeighborListenerCount = Mathf.Max(0, neighborListenerCount);
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

        public enum HidingFeedbackKind
        {
            Entered,
            Exited,
            Inspected,
            BreathNoisy,
            Found,
            Recovered
        }

        public readonly struct HidingFeedback
        {
            public string SpotName { get; }
            public HidingFeedbackKind Kind { get; }
            public float BreathTension { get; }
            public bool IsHidden { get; }
            public bool IsCompromised { get; }

            public HidingFeedback(
                string spotName,
                HidingFeedbackKind kind,
                float breathTension,
                bool isHidden,
                bool isCompromised)
            {
                SpotName = string.IsNullOrWhiteSpace(spotName) ? "hiding spot" : spotName.Trim();
                Kind = kind;
                BreathTension = Mathf.Clamp01(breathTension);
                IsHidden = isHidden;
                IsCompromised = isCompromised;
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

        public enum NeighborInvestigationFeedbackKind
        {
            Started,
            FollowingTrail,
            Searching,
            CheckingHideSpot,
            Returning,
            Abandoned
        }

        public enum StealthLoopPhase
        {
            Quiet,
            Curious,
            Suspicious,
            Certain,
            Searching,
            Chased,
            Hiding,
            PostChase
        }

        public readonly struct StealthLoopFeedback
        {
            public StealthLoopPhase Phase { get; }
            public float Suspicion { get; }
            public float Noise { get; }
            public float Tension { get; }
            public string Message { get; }
            public bool IsCalming { get; }

            public StealthLoopFeedback(
                StealthLoopPhase phase,
                float suspicion,
                float noise,
                float tension,
                string message)
                : this(phase, suspicion, noise, tension, message, false)
            {
            }

            public StealthLoopFeedback(
                StealthLoopPhase phase,
                float suspicion,
                float noise,
                float tension,
                string message,
                bool isCalming)
            {
                Phase = phase;
                Suspicion = Mathf.Clamp01(suspicion);
                Noise = Mathf.Clamp01(noise);
                Tension = Mathf.Clamp01(tension);
                Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
                IsCalming = isCalming;
            }
        }

        public enum NeighborMemoryClueKind
        {
            DoorOpened,
            ObjectMoved,
            GlassBroken,
            KeyStolen
        }

        public readonly struct NeighborMemoryFeedback
        {
            public NeighborMemoryClueKind Kind { get; }
            public string SourceName { get; }
            public Vector3 Position { get; }
            public float Suspicion { get; }
            public int TotalMemoryCount { get; }
            public float Urgency { get; }

            public NeighborMemoryFeedback(
                NeighborMemoryClueKind kind,
                string sourceName,
                Vector3 position,
                float suspicion,
                int totalMemoryCount)
                : this(
                    kind,
                    sourceName,
                    position,
                    suspicion,
                    totalMemoryCount,
                    CalculateMemoryUrgency(kind, suspicion, totalMemoryCount))
            {
            }

            public NeighborMemoryFeedback(
                NeighborMemoryClueKind kind,
                string sourceName,
                Vector3 position,
                float suspicion,
                int totalMemoryCount,
                float urgency)
            {
                Kind = kind;
                SourceName = string.IsNullOrWhiteSpace(sourceName) ? "clue" : sourceName.Trim();
                Position = position;
                Suspicion = Mathf.Clamp01(suspicion);
                TotalMemoryCount = Mathf.Max(0, totalMemoryCount);
                Urgency = Mathf.Clamp01(urgency);
            }
        }

        public readonly struct NeighborInvestigationFeedback
        {
            public NeighborInvestigationFeedbackKind Kind { get; }
            public Vector3 Position { get; }
            public string SourceName { get; }
            public float Suspicion { get; }
            public float Urgency { get; }
            public bool IsTrailRelated { get; }
            public bool HasMemoryClueKind { get; }
            public NeighborMemoryClueKind MemoryClueKind { get; }

            public NeighborInvestigationFeedback(
                NeighborInvestigationFeedbackKind kind,
                Vector3 position,
                string sourceName,
                float suspicion,
                float urgency,
                bool isTrailRelated = false)
                : this(kind, position, sourceName, suspicion, urgency, isTrailRelated, false, default)
            {
            }

            public NeighborInvestigationFeedback(
                NeighborInvestigationFeedbackKind kind,
                Vector3 position,
                string sourceName,
                float suspicion,
                float urgency,
                bool isTrailRelated,
                NeighborMemoryClueKind memoryClueKind)
                : this(kind, position, sourceName, suspicion, urgency, isTrailRelated, true, memoryClueKind)
            {
            }

            private NeighborInvestigationFeedback(
                NeighborInvestigationFeedbackKind kind,
                Vector3 position,
                string sourceName,
                float suspicion,
                float urgency,
                bool isTrailRelated,
                bool hasMemoryClueKind,
                NeighborMemoryClueKind memoryClueKind)
            {
                Kind = kind;
                Position = position;
                SourceName = string.IsNullOrWhiteSpace(sourceName) ? "disturbance" : sourceName.Trim();
                Suspicion = Mathf.Clamp01(suspicion);
                Urgency = Mathf.Clamp01(urgency);
                HasMemoryClueKind = hasMemoryClueKind;
                MemoryClueKind = hasMemoryClueKind ? memoryClueKind : default;
                IsTrailRelated = isTrailRelated
                    || kind == NeighborInvestigationFeedbackKind.FollowingTrail
                    || kind == NeighborInvestigationFeedbackKind.CheckingHideSpot
                    || hasMemoryClueKind;
            }
        }

        public static event Action<NoiseFeedback> NoiseEmitted;
        public static event Action CameraDetectedPlayer;
        public static event Action<SecurityEscalationFeedback> SecurityEscalated;
        public static event Action<AmbienceZoneFeedback> AmbienceZoneChanged;
        public static event Action<DayPhaseFeedback> DayPhaseChanged;
        public static event Action<CheckpointFeedback> CheckpointReached;
        public static event Action<RespawnFeedback> PlayerRespawned;
        public static event Action<HidingFeedback> HidingChanged;
        public static event Action<OnboardingPromptFeedback> OnboardingPrompted;
        public static event Action<ObjectiveProgressFeedback> ObjectiveProgressed;
        public static event Action<DoorInteractionFeedback> DoorInteractionReported;
        public static event Action<StealthLoopFeedback> StealthLoopChanged;
        public static event Action<NeighborMemoryFeedback> NeighborMemoryChanged;
        public static event Action<NeighborInvestigationFeedback> NeighborInvestigationChanged;

        public static void ReportNoise(Vector3 origin, float loudness, float radius)
        {
            NoiseEmitted?.Invoke(new NoiseFeedback(origin, loudness, radius));
        }

        public static void ReportNoise(
            Vector3 origin,
            float loudness,
            float radius,
            float urgency,
            bool heardByNeighbor,
            int neighborListenerCount)
        {
            NoiseEmitted?.Invoke(new NoiseFeedback(
                origin,
                loudness,
                radius,
                urgency,
                heardByNeighbor,
                neighborListenerCount));
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

        public static void ReportHiding(
            string spotName,
            HidingFeedbackKind kind,
            float breathTension,
            bool isHidden,
            bool isCompromised)
        {
            HidingChanged?.Invoke(new HidingFeedback(spotName, kind, breathTension, isHidden, isCompromised));
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

        public static void ReportStealthLoop(
            StealthLoopPhase phase,
            float suspicion,
            float noise,
            float tension,
            string message,
            bool isCalming = false)
        {
            StealthLoopChanged?.Invoke(new StealthLoopFeedback(phase, suspicion, noise, tension, message, isCalming));
        }

        public static void ReportNeighborMemory(
            NeighborMemoryClueKind kind,
            string sourceName,
            Vector3 position,
            float suspicion,
            int totalMemoryCount)
        {
            NeighborMemoryChanged?.Invoke(new NeighborMemoryFeedback(
                kind,
                sourceName,
                position,
                suspicion,
                totalMemoryCount,
                CalculateMemoryUrgency(kind, suspicion, totalMemoryCount)));
        }

        public static void ReportNeighborInvestigation(
            NeighborInvestigationFeedbackKind kind,
            Vector3 position,
            string sourceName,
            float suspicion,
            float urgency,
            bool isTrailRelated = false)
        {
            NeighborInvestigationChanged?.Invoke(new NeighborInvestigationFeedback(
                kind,
                position,
                sourceName,
                suspicion,
                urgency,
                isTrailRelated));
        }

        public static void ReportNeighborInvestigation(
            NeighborInvestigationFeedbackKind kind,
            Vector3 position,
            string sourceName,
            float suspicion,
            float urgency,
            bool isTrailRelated,
            NeighborMemoryClueKind memoryClueKind)
        {
            NeighborInvestigationChanged?.Invoke(new NeighborInvestigationFeedback(
                kind,
                position,
                sourceName,
                suspicion,
                urgency,
                isTrailRelated,
                memoryClueKind));
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
            HidingChanged = null;
            OnboardingPrompted = null;
            ObjectiveProgressed = null;
            DoorInteractionReported = null;
            StealthLoopChanged = null;
            NeighborMemoryChanged = null;
            NeighborInvestigationChanged = null;
        }

        private static float CalculateMemoryUrgency(
            NeighborMemoryClueKind kind,
            float suspicion,
            int totalMemoryCount)
        {
            float kindFloor = kind switch
            {
                NeighborMemoryClueKind.KeyStolen => 0.68f,
                NeighborMemoryClueKind.GlassBroken => 0.6f,
                NeighborMemoryClueKind.ObjectMoved => 0.5f,
                NeighborMemoryClueKind.DoorOpened => 0.42f,
                _ => 0.3f
            };
            float stackPressure = Mathf.Clamp01(Mathf.Max(0, totalMemoryCount - 1) / 3f) * 0.22f;
            return Mathf.Clamp01(Mathf.Max(Mathf.Clamp01(suspicion), kindFloor) + stackPressure);
        }
    }
}
