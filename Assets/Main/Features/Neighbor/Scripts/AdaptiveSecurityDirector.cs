using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Neighbor
{
    public readonly struct AdaptiveSecurityPlan
    {
        public int Level { get; }
        public int Budget { get; }
        public int DoorCount { get; }
        public int LocationCount { get; }
        public int CameraCount { get; }
        public int TrapCount { get; }
        public int PatrolPointCount { get; }

        public AdaptiveSecurityPlan(int level, int budget, int doorCount, int locationCount)
            : this(level, budget, doorCount, locationCount, 0, 0, 0)
        {
        }

        public AdaptiveSecurityPlan(
            int level,
            int budget,
            int doorCount,
            int locationCount,
            int cameraCount,
            int trapCount,
            int patrolPointCount)
        {
            Level = Mathf.Clamp(level, 0, 3);
            Budget = Mathf.Max(0, budget);
            DoorCount = Mathf.Max(0, doorCount);
            LocationCount = Mathf.Max(0, locationCount);
            CameraCount = Mathf.Max(0, cameraCount);
            TrapCount = Mathf.Max(0, trapCount);
            PatrolPointCount = Mathf.Max(0, patrolPointCount);
        }
    }

    public static class AdaptiveSecurityDirector
    {
        private const float MaximumRunPressure = 8f;
        private const float MaximumPersistentPressure = 6f;

        private static float runPressure;
        private static float persistentPressure;
        private static int deathCount;

        public static float RunPressure => runPressure;
        public static float PersistentPressure => persistentPressure;
        public static int DeathCount => deathCount;

        public static void ReportDisturbance(float severity)
        {
            runPressure = Mathf.Clamp(runPressure + Mathf.Clamp01(severity) * 0.55f, 0f, MaximumRunPressure);
        }

        public static void ReportCameraDetection()
        {
            runPressure = Mathf.Clamp(runPressure + 1.15f, 0f, MaximumRunPressure);
        }

        public static void ReportChaseStarted()
        {
            runPressure = Mathf.Clamp(runPressure + 1.4f, 0f, MaximumRunPressure);
        }

        public static AdaptiveSecurityPlan CompleteRun(int baseBudget, int baseLocationCount, int baseDoorCount)
        {
            deathCount++;
            float pressure = runPressure + persistentPressure + deathCount * 0.45f;
            int level = GetLevel(pressure);

            AdaptiveSecurityPlan plan = new(
                level,
                baseBudget + level * 2,
                baseDoorCount + level / 2,
                baseLocationCount + (level + 1) / 2,
                GetCameraCount(level),
                GetTrapCount(level),
                GetPatrolPointCount(level));

            persistentPressure = Mathf.Clamp(
                persistentPressure + runPressure * 0.3f + 0.35f,
                0f,
                MaximumPersistentPressure);
            runPressure = 0f;

            PlayerFeedbackEvents.ReportSecurityEscalation(
                plan.Level,
                plan.Budget,
                plan.DoorCount,
                plan.LocationCount,
                plan.CameraCount,
                plan.TrapCount,
                plan.PatrolPointCount);
            return plan;
        }

        public static void ResetProgression()
        {
            runPressure = 0f;
            persistentPressure = 0f;
            deathCount = 0;
        }

        private static int GetLevel(float pressure)
        {
            if (pressure >= 5f)
            {
                return 3;
            }

            if (pressure >= 2.75f)
            {
                return 2;
            }

            return pressure >= 1.25f ? 1 : 0;
        }

        private static int GetCameraCount(int level)
        {
            return level <= 0 ? 0 : 1 + (level - 1) / 2;
        }

        private static int GetTrapCount(int level)
        {
            return level < 2 ? 0 : level - 1;
        }

        private static int GetPatrolPointCount(int level)
        {
            return level;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            ResetProgression();
        }
    }
}
