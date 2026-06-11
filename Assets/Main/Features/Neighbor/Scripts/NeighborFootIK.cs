using System;
using UnityEngine;

namespace Neighbor.Main.Features.Neighbor
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class NeighborFootIK : MonoBehaviour
    {
        [Serializable]
        public sealed class Settings
        {
            [Tooltip("Allows the Neighbor's humanoid feet to conform to floors, stairs, and slopes.")]
            public bool enableFootIK = true;

            [Tooltip("Layers that the Neighbor may place his feet on.")]
            public LayerMask groundMask = ~0;

            [Min(0.01f)] public float raycastOriginHeight = 0.45f;
            [Min(0.01f)] public float groundProbeDistance = 0.75f;
            [Min(0f)] public float footSoleOffset = 0.025f;
            [Range(0f, 89f)] public float maximumGroundAngle = 58f;
            [Min(0.01f)] public float fullyPlantedHeight = 0.08f;
            [Min(0.02f)] public float maximumPlantHeight = 0.42f;
            [Range(0f, 1f)] public float footRotationWeight = 0.88f;
            [Range(0f, 1f)] public float runningWeight = 0.52f;
            [Min(0f)] public float runSpeedThreshold = 3.4f;
            [Min(0f)] public float maximumPelvisDrop = 0.3f;
            [Min(0f)] public float maximumPelvisRise = 0.12f;
            [Range(0f, 1f)] public float pelvisWeight = 0.78f;
            [Min(0.01f)] public float footPositionSharpness = 22f;
            [Min(0.01f)] public float footRotationSharpness = 18f;
            [Min(0.01f)] public float weightSharpness = 16f;
            [Min(0.01f)] public float pelvisSharpness = 12f;
        }

        private sealed class FootState
        {
            public bool HasTarget;
            public Vector3 AnimatedPosition;
            public Vector3 Position;
            public Quaternion Rotation;
            public float Weight;
            public float VerticalOffset;
        }

        private readonly RaycastHit[] probeHits = new RaycastHit[12];
        private readonly FootState leftFoot = new FootState();
        private readonly FootState rightFoot = new FootState();

        private Animator animator;
        private Transform characterRoot;
        private NeighborMotor motor;
        private Settings settings;
        private float currentWeight;
        private float currentPelvisOffset;

        public void Configure(Animator targetAnimator, Transform targetCharacterRoot, NeighborMotor targetMotor, Settings targetSettings)
        {
            animator = targetAnimator != null ? targetAnimator : GetComponent<Animator>();
            characterRoot = targetCharacterRoot != null ? targetCharacterRoot : transform.root;
            motor = targetMotor;
            settings = targetSettings ?? new Settings();
        }

        private void Awake()
        {
            animator = animator != null ? animator : GetComponent<Animator>();
            characterRoot = characterRoot != null ? characterRoot : transform.root;
            settings ??= new Settings();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || animator == null || !animator.isHuman)
            {
                return;
            }

            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            float desiredWeight = GetDesiredWeight();
            currentWeight = Damp(currentWeight, desiredWeight, settings.weightSharpness, deltaTime);

            UpdateFoot(HumanBodyBones.LeftFoot, leftFoot, deltaTime);
            UpdateFoot(HumanBodyBones.RightFoot, rightFoot, deltaTime);
            ApplyPelvis(deltaTime);
            ApplyFoot(AvatarIKGoal.LeftFoot, leftFoot);
            ApplyFoot(AvatarIKGoal.RightFoot, rightFoot);
        }

        private float GetDesiredWeight()
        {
            if (!settings.enableFootIK || motor != null && motor.IsTraversingSpecialMove)
            {
                return 0f;
            }

            float speed = motor != null ? motor.CurrentSpeed : 0f;
            float runBlend = settings.runSpeedThreshold <= 0f
                ? 1f
                : Mathf.InverseLerp(settings.runSpeedThreshold * 0.65f, settings.runSpeedThreshold, speed);
            return Mathf.Lerp(1f, settings.runningWeight, runBlend);
        }

        private void UpdateFoot(HumanBodyBones bone, FootState state, float deltaTime)
        {
            Transform footBone = animator.GetBoneTransform(bone);
            if (footBone == null)
            {
                state.Weight = 0f;
                state.HasTarget = false;
                return;
            }

            state.AnimatedPosition = footBone.position;
            if (!TryFindGround(state.AnimatedPosition, out RaycastHit hit))
            {
                state.Weight = Damp(state.Weight, 0f, settings.weightSharpness, deltaTime);
                if (state.Weight <= 0.001f)
                {
                    state.HasTarget = false;
                }

                state.VerticalOffset = 0f;
                return;
            }

            Vector3 targetPosition = hit.point + hit.normal * settings.footSoleOffset;
            Vector3 projectedForward = Vector3.ProjectOnPlane(footBone.forward, hit.normal);
            if (projectedForward.sqrMagnitude <= 0.001f)
            {
                projectedForward = Vector3.ProjectOnPlane(characterRoot.forward, hit.normal);
            }

            Quaternion targetRotation = Quaternion.LookRotation(projectedForward.normalized, hit.normal);
            float heightAboveGround = Vector3.Dot(state.AnimatedPosition - targetPosition, Vector3.up);
            float plantWeight = 1f - Mathf.InverseLerp(
                settings.fullyPlantedHeight,
                Mathf.Max(settings.fullyPlantedHeight + 0.01f, settings.maximumPlantHeight),
                heightAboveGround);

            if (!state.HasTarget || Vector3.Distance(state.Position, targetPosition) > settings.maximumPlantHeight * 2f)
            {
                state.Position = targetPosition;
                state.Rotation = targetRotation;
            }
            else
            {
                state.Position = Vector3.Lerp(
                    state.Position,
                    targetPosition,
                    ExponentialFactor(settings.footPositionSharpness, deltaTime));
                state.Rotation = Quaternion.Slerp(
                    state.Rotation,
                    targetRotation,
                    ExponentialFactor(settings.footRotationSharpness, deltaTime));
            }

            state.Weight = Damp(state.Weight, plantWeight, settings.weightSharpness, deltaTime);
            state.VerticalOffset = targetPosition.y - state.AnimatedPosition.y;
            state.HasTarget = true;
        }

        private bool TryFindGround(Vector3 footPosition, out RaycastHit closestHit)
        {
            Vector3 origin = footPosition + Vector3.up * settings.raycastOriginHeight;
            float distance = settings.raycastOriginHeight + settings.groundProbeDistance;
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                probeHits,
                distance,
                settings.groundMask,
                QueryTriggerInteraction.Ignore);

            closestHit = default;
            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = probeHits[i];
                if (hit.collider == null
                    || IsOwnCollider(hit.transform)
                    || Vector3.Angle(hit.normal, Vector3.up) > settings.maximumGroundAngle
                    || hit.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hit.distance;
                closestHit = hit;
            }

            return closestDistance < float.PositiveInfinity;
        }

        private bool IsOwnCollider(Transform hitTransform)
        {
            return hitTransform == characterRoot || hitTransform.IsChildOf(characterRoot);
        }

        private void ApplyPelvis(float deltaTime)
        {
            bool hasLeft = leftFoot.HasTarget && leftFoot.Weight > 0.01f;
            bool hasRight = rightFoot.HasTarget && rightFoot.Weight > 0.01f;
            float desiredOffset = 0f;

            if (hasLeft && hasRight)
            {
                desiredOffset = Mathf.Min(leftFoot.VerticalOffset * leftFoot.Weight, rightFoot.VerticalOffset * rightFoot.Weight);
            }
            else if (hasLeft)
            {
                desiredOffset = leftFoot.VerticalOffset * leftFoot.Weight;
            }
            else if (hasRight)
            {
                desiredOffset = rightFoot.VerticalOffset * rightFoot.Weight;
            }

            desiredOffset = Mathf.Clamp(desiredOffset, -settings.maximumPelvisDrop, settings.maximumPelvisRise);
            currentPelvisOffset = Damp(currentPelvisOffset, desiredOffset, settings.pelvisSharpness, deltaTime);

            Vector3 bodyPosition = animator.bodyPosition;
            bodyPosition.y += currentPelvisOffset * settings.pelvisWeight * currentWeight;
            animator.bodyPosition = bodyPosition;
        }

        private void ApplyFoot(AvatarIKGoal goal, FootState state)
        {
            float weight = currentWeight * state.Weight;
            animator.SetIKPositionWeight(goal, weight);
            animator.SetIKRotationWeight(goal, weight * settings.footRotationWeight);
            if (state.HasTarget)
            {
                animator.SetIKPosition(goal, state.Position);
                animator.SetIKRotation(goal, state.Rotation);
            }
        }

        private void OnDisable()
        {
            currentWeight = 0f;
            currentPelvisOffset = 0f;
            leftFoot.HasTarget = false;
            rightFoot.HasTarget = false;
            leftFoot.Weight = 0f;
            rightFoot.Weight = 0f;
        }

        private static float Damp(float current, float target, float sharpness, float deltaTime)
        {
            return Mathf.Lerp(current, target, ExponentialFactor(sharpness, deltaTime));
        }

        private static float ExponentialFactor(float sharpness, float deltaTime)
        {
            return 1f - Mathf.Exp(-sharpness * deltaTime);
        }
    }
}
