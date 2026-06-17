using Neighbor.Main.Features.Interaction;
using UnityEngine;

namespace Neighbor.Main.Features.Neighbor
{
    [DisallowMultipleComponent]
    public sealed class NeighborLightSwitchInteractor : MonoBehaviour
    {
        [Header("Routine")]
        [SerializeField] private bool enableLightSwitchUse = true;
        [SerializeField, Range(0f, 1f)] private float routineChance = 0.08f;
        [SerializeField, Min(0f)] private float retryCooldown = 14f;

        [Header("Selection")]
        [SerializeField, Min(0.5f)] private float switchSearchRadius = 9f;
        [SerializeField, Min(0.1f)] private float switchApproachDistance = 1.2f;
        [SerializeField, Min(0f)] private float switchDestinationSampleRadius = 1.5f;

        [Header("Use")]
        [SerializeField, Min(0f)] private float usePause = 0.35f;
        [SerializeField, Min(0.1f)] private float faceTurnSharpness = 10f;

        private NeighborMotor motor;
        private LightSwitch targetSwitch;
        private bool hasToggled;
        private float nextAttemptTime;
        private float finishUseAtTime;

        public bool EnableLightSwitchUse
        {
            get => enableLightSwitchUse;
            set
            {
                enableLightSwitchUse = value;
                if (!enableLightSwitchUse)
                {
                    CancelActivity();
                }
            }
        }

        public bool IsActive => targetSwitch != null;
        public LightSwitch TargetSwitch => targetSwitch;

        private void Awake()
        {
            motor = GetComponent<NeighborMotor>();
        }

        private void OnDisable()
        {
            CancelActivity();
        }

        public bool TryBeginRoutine(out Vector3 goal)
        {
            goal = transform.position;
            if (!enableLightSwitchUse
                || IsActive
                || motor == null
                || Time.time < nextAttemptTime
                || Random.value > routineChance)
            {
                return false;
            }

            nextAttemptTime = Time.time + retryCooldown;
            targetSwitch = FindBestSwitchCandidate();
            if (targetSwitch == null
                || !motor.TrySetDestinationNear(
                    targetSwitch.transform.position,
                    switchDestinationSampleRadius,
                    out goal))
            {
                targetSwitch = null;
                return false;
            }

            hasToggled = false;
            finishUseAtTime = 0f;
            return true;
        }

        public bool UpdateActivity(out Vector3 goal)
        {
            goal = targetSwitch != null ? targetSwitch.transform.position : transform.position;
            if (targetSwitch == null || !targetSwitch.isActiveAndEnabled)
            {
                ClearActivity();
                return false;
            }

            if (!hasToggled)
            {
                Vector3 toSwitch = targetSwitch.transform.position - transform.position;
                float verticalOffset = Mathf.Abs(toSwitch.y);
                toSwitch.y = 0f;
                bool closeEnough = toSwitch.sqrMagnitude
                    <= switchApproachDistance * switchApproachDistance
                    && verticalOffset <= switchApproachDistance;
                if (!closeEnough)
                {
                    if (motor != null && !motor.HasArrived)
                    {
                        return true;
                    }

                    ClearActivity();
                    return false;
                }

                motor?.Stop();
                motor?.FaceTowards(targetSwitch.transform.position, faceTurnSharpness);
                UseSwitch(targetSwitch);
                hasToggled = true;
                finishUseAtTime = Time.time + usePause;
                return true;
            }

            motor?.FaceTowards(targetSwitch.transform.position, faceTurnSharpness);
            if (Time.time < finishUseAtTime)
            {
                return true;
            }

            ClearActivity();
            return false;
        }

        public void CancelActivity()
        {
            if (targetSwitch != null)
            {
                nextAttemptTime = Mathf.Max(nextAttemptTime, Time.time + retryCooldown);
            }

            ClearActivity();
        }

        private void ClearActivity()
        {
            targetSwitch = null;
            hasToggled = false;
            finishUseAtTime = 0f;
        }

        private LightSwitch FindBestSwitchCandidate()
        {
            LightSwitch[] switches = Object.FindObjectsByType<LightSwitch>(FindObjectsInactive.Exclude);
            LightSwitch bestSwitch = null;
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < switches.Length; i++)
            {
                LightSwitch candidate = switches[i];
                if (!IsSwitchCandidateValid(candidate))
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, candidate.transform.position);
                float score = distance + Random.Range(0f, 1.75f);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestSwitch = candidate;
                }
            }

            return bestSwitch;
        }

        private bool IsSwitchCandidateValid(LightSwitch lightSwitch)
        {
            return lightSwitch != null
                && lightSwitch.isActiveAndEnabled
                && Vector3.Distance(transform.position, lightSwitch.transform.position) <= switchSearchRadius;
        }

        private void UseSwitch(LightSwitch lightSwitch)
        {
            lightSwitch?.Toggle();
        }
    }
}
