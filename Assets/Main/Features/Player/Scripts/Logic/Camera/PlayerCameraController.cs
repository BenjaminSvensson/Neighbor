using Neighbor.Main.Features.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Neighbor.Main.Features.Player
{
    [RequireComponent(typeof(Camera))]
    public sealed class PlayerCameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private Transform yawRoot;

        [Header("Look")]
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.08f;
        [SerializeField] private bool invertLookY;
        [SerializeField] private Vector2 pitchLimits = new Vector2(-82f, 82f);
        [SerializeField, Min(0f)] private float lookSmoothing = 18f;
        [SerializeField] private bool lockCursorOnStart = true;

        [Header("Zoom")]
        [SerializeField, Min(1f)] private float defaultFieldOfView = 60f;
        [SerializeField, Min(1f)] private float minimumFieldOfView = 33f;
        [SerializeField, Min(1f)] private float maximumFieldOfView = 72f;
        [SerializeField, Min(0f)] private float zoomScrollSpeed = 3.5f;
        [SerializeField, Min(0f)] private float zoomDragSpeed = 0.18f;
        [SerializeField, Min(0f)] private float zoomSmoothing = 12f;
        [SerializeField, Range(0f, 1f)] private float zoomWobbleBoost = 0.55f;

        [Header("Lean")]
        [SerializeField, Min(0f)] private float leanDistance = 0.32f;
        [SerializeField, Min(0f)] private float leanAngle = 9f;
        [SerializeField, Min(0f)] private float leanSmoothing = 12f;

        [Header("Handheld Feel")]
        [SerializeField, Min(0f)] private float idleWobbleAmount = 0.08f;
        [SerializeField, Min(0f)] private float moveWobbleAmount = 0.16f;
        [SerializeField, Min(0f)] private float runWobbleAmount = 0.28f;
        [SerializeField, Min(0f)] private float wobbleFrequency = 1.7f;
        [SerializeField, Min(0f)] private float bobPositionAmount = 0.055f;
        [SerializeField, Min(0f)] private float bobRollAmount = 1.4f;

        [Header("Jump Camera")]
        [SerializeField, Min(0f)] private float jumpTakeoffKick = 0.1f;
        [SerializeField, Min(0f)] private float jumpTakeoffPitch = 2.8f;
        [SerializeField, Min(0f)] private float fallStretchAmount = 0.035f;
        [SerializeField, Min(0f)] private float landingKick = 0.22f;
        [SerializeField, Min(0f)] private float landingPitchKick = 5.5f;
        [SerializeField, Min(0f)] private float landingShake = 0.32f;
        [SerializeField, Min(0f)] private float landingFovKick = 2.4f;
        [SerializeField, Min(0f)] private float heavyLandingKick = 0.42f;
        [SerializeField, Min(0f)] private float heavyLandingPitchKick = 11f;
        [SerializeField, Min(0f)] private float heavyLandingRollKick = 7f;
        [SerializeField, Min(0f)] private float heavyLandingShake = 0.7f;
        [SerializeField, Min(0f)] private float heavyLandingFovKick = 4.5f;

        [Header("Stair Camera")]
        [SerializeField, Min(0f)] private float stairStepKick = 0.11f;
        [SerializeField, Min(0f)] private float stairPitchKick = 2.2f;
        [SerializeField, Min(0f)] private float stairRollKick = 2.8f;
        [SerializeField, Min(0f)] private float stairShake = 0.18f;

        [Header("Climb Camera")]
        [SerializeField, Min(0f)] private float climbStartKick = 0.18f;
        [SerializeField, Min(0f)] private float climbStartPitchKick = 5.5f;
        [SerializeField, Min(0f)] private float climbStartRollKick = 3.5f;
        [SerializeField, Min(0f)] private float climbPullVerticalOffset = 0.16f;
        [SerializeField, Min(0f)] private float climbPullPitchOffset = 5f;
        [SerializeField, Min(0f)] private float climbPullRollOffset = 4f;
        [SerializeField, Min(0f)] private float climbEndKick = 0.14f;
        [SerializeField, Min(0f)] private float climbEndPitchKick = 4f;
        [SerializeField, Min(0f)] private float climbEndShake = 0.28f;
        [SerializeField, Min(0f)] private float climbCameraSmoothing = 16f;

        [Header("Impact Return")]
        [SerializeField, Min(0f)] private float impactReturnSpeed = 18f;

        private Camera playerCamera;
        private Vector3 baseLocalPosition;
        private float yaw;
        private float pitch;
        private float smoothedPitch;
        private float smoothedLean;
        private float scrolledFieldOfView;
        private float currentFieldOfView;
        private float bobTime;
        private float noiseSeed;
        private float impactVerticalOffset;
        private float impactPitchOffset;
        private float impactRollOffset;
        private float impactFovOffset;
        private float impactShake;
        private float climbVerticalOffset;
        private float climbPitchOffset;
        private float climbRollOffset;
        private float targetImpactVerticalOffset;
        private float targetImpactPitchOffset;
        private float targetImpactRollOffset;
        private float targetImpactFovOffset;
        private float targetImpactShake;
        private float stairStepSide = 1f;

        public int ZoomDirection { get; private set; }

        private void Awake()
        {
            playerCamera = GetComponent<Camera>();
            baseLocalPosition = transform.localPosition;
            noiseSeed = Random.Range(0f, 1000f);

            if (playerController == null)
            {
                playerController = GetComponentInParent<PlayerController>();
            }

            if (yawRoot == null)
            {
                yawRoot = playerController != null ? playerController.transform : transform.root;
            }

            Vector3 yawEuler = yawRoot.rotation.eulerAngles;
            yaw = yawEuler.y;
            pitch = NormalizeAngle(transform.localEulerAngles.x);
            smoothedPitch = pitch;

            defaultFieldOfView = playerCamera.fieldOfView > 1f ? playerCamera.fieldOfView : defaultFieldOfView;
            minimumFieldOfView = Mathf.Min(minimumFieldOfView, defaultFieldOfView);
            maximumFieldOfView = Mathf.Max(maximumFieldOfView, defaultFieldOfView);
            currentFieldOfView = maximumFieldOfView;
            scrolledFieldOfView = maximumFieldOfView;
            playerCamera.fieldOfView = maximumFieldOfView;
        }

        private void Start()
        {
            if (lockCursorOnStart)
            {
                SetCursorLocked(true);
            }
        }

        private void LateUpdate()
        {
            PlayerFrameInput input = PlayerInputReader.ReadFrameInput(mouseSensitivity, invertLookY);

            if (InteractionOverlayState.IsGameplayInputBlocked)
            {
                ZoomDirection = 0;
                return;
            }

            if (input.CursorUnlockPressed)
            {
                SetCursorLocked(false);
            }
            else if (Cursor.lockState != CursorLockMode.Locked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                SetCursorLocked(true);
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                yaw += input.Look.x;
                if (!input.ZoomHeld)
                {
                    pitch = Mathf.Clamp(pitch + input.Look.y, pitchLimits.x, pitchLimits.y);
                }
            }

            UpdateZoom(input);
            UpdateCameraPose(input);
        }

        private void UpdateZoom(PlayerFrameInput input)
        {
            float previousFieldOfView = scrolledFieldOfView;

            if (Mathf.Abs(input.ZoomScroll) > 0.01f)
            {
                float scrollSteps = Mathf.Abs(input.ZoomScroll) > 10f
                    ? input.ZoomScroll / 120f
                    : input.ZoomScroll;

                scrolledFieldOfView = Mathf.Clamp(
                    scrolledFieldOfView - scrollSteps * zoomScrollSpeed,
                    minimumFieldOfView,
                    maximumFieldOfView);
            }

            if (input.ZoomHeld && Mathf.Abs(input.ZoomDrag) > 0.01f)
            {
                scrolledFieldOfView = Mathf.Clamp(
                    scrolledFieldOfView - input.ZoomDrag * zoomDragSpeed,
                    minimumFieldOfView,
                    maximumFieldOfView);
            }

            float targetFieldOfView = Mathf.Clamp(scrolledFieldOfView, minimumFieldOfView, maximumFieldOfView);
            float zoomInputDelta = targetFieldOfView - previousFieldOfView;
            ZoomDirection = Mathf.Abs(zoomInputDelta) > 0.001f ? (zoomInputDelta < 0f ? 1 : -1) : 0;

            currentFieldOfView = Damp(currentFieldOfView, targetFieldOfView, zoomSmoothing);
            playerCamera.fieldOfView = Mathf.Clamp(currentFieldOfView + impactFovOffset, minimumFieldOfView, maximumFieldOfView);
        }

        private void UpdateCameraPose(PlayerFrameInput input)
        {
            yawRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
            smoothedPitch = Damp(smoothedPitch, pitch, lookSmoothing);
            UpdateImpactFeedback();

            float leanInput = 0f;
            leanInput -= input.LeanLeftHeld ? 1f : 0f;
            leanInput += input.LeanRightHeld ? 1f : 0f;
            smoothedLean = Damp(smoothedLean, leanInput, leanSmoothing);

            float moveAmount = playerController != null ? playerController.MoveAmount : input.Move.magnitude;
            bool running = playerController != null && playerController.IsRunning;
            float wobbleStrength = idleWobbleAmount;
            wobbleStrength += Mathf.Lerp(0f, moveWobbleAmount, moveAmount);
            wobbleStrength += running ? runWobbleAmount : 0f;
            wobbleStrength *= 1f + Zoom01 * zoomWobbleBoost;

            bobTime += Time.deltaTime * Mathf.Lerp(4.5f, running ? 10.5f : 7.5f, moveAmount);
            float bobStep = moveAmount > 0.01f ? Mathf.Sin(bobTime) : 0f;
            Vector3 bobOffset = new Vector3(
                Mathf.Sin(bobTime * 0.5f) * bobPositionAmount * moveAmount,
                Mathf.Abs(bobStep) * bobPositionAmount * moveAmount,
                0f);

            float time = Time.time * wobbleFrequency;
            float wobbleX = Noise(time, 0.13f) * wobbleStrength;
            float wobbleY = Noise(time, 3.71f) * wobbleStrength;
            float wobbleRoll = Noise(time, 8.19f) * wobbleStrength * 5f;
            float shakeX = Noise(time * 2.4f, 14.23f) * impactShake;
            float shakeY = Noise(time * 2.1f, 18.67f) * impactShake;
            float shakeRoll = Noise(time * 2.7f, 22.41f) * impactShake * 12f;

            Vector3 leanOffset = Vector3.right * (smoothedLean * leanDistance);
            Vector3 impactOffset = new Vector3(shakeX * 0.01f, impactVerticalOffset + climbVerticalOffset + shakeY * 0.01f, 0f);
            transform.localPosition = baseLocalPosition + leanOffset + bobOffset + impactOffset + new Vector3(wobbleX, wobbleY, 0f) * 0.01f;

            float roll = -smoothedLean * leanAngle + wobbleRoll + shakeRoll + impactRollOffset + climbRollOffset + bobStep * bobRollAmount * moveAmount;
            transform.localRotation = Quaternion.Euler(smoothedPitch + wobbleY + impactPitchOffset + climbPitchOffset, wobbleX, roll);
        }

        private void UpdateImpactFeedback()
        {
            if (playerController != null)
            {
                if (playerController.JumpStartedThisFrame)
                {
                    targetImpactVerticalOffset -= jumpTakeoffKick;
                    targetImpactPitchOffset -= jumpTakeoffPitch;
                    targetImpactShake += jumpTakeoffKick * 0.6f;
                }

                if (!playerController.IsGrounded && playerController.VerticalSpeed < 0f)
                {
                    targetImpactVerticalOffset += Mathf.Clamp01(-playerController.VerticalSpeed / 14f) * fallStretchAmount * Time.deltaTime;
                }

                if (playerController.LandedThisFrame)
                {
                    float impact = Mathf.Max(0.35f, playerController.LandingImpact);
                    targetImpactVerticalOffset -= landingKick * impact;
                    targetImpactPitchOffset += landingPitchKick * impact;
                    targetImpactFovOffset += landingFovKick * impact;
                    targetImpactShake += landingShake * impact;
                }

                if (playerController.HeavyLandingThisFrame)
                {
                    float impact = Mathf.Max(0.25f, playerController.HeavyLandingImpact);
                    stairStepSide *= -1f;
                    targetImpactVerticalOffset -= heavyLandingKick * impact;
                    targetImpactPitchOffset += heavyLandingPitchKick * impact;
                    targetImpactRollOffset += stairStepSide * heavyLandingRollKick * impact;
                    targetImpactFovOffset += heavyLandingFovKick * impact;
                    targetImpactShake += heavyLandingShake * impact;
                }

                if (playerController.StepImpactThisFrame)
                {
                    float impact = Mathf.Lerp(0.55f, 1f, playerController.StepImpact);
                    stairStepSide *= -1f;
                    targetImpactVerticalOffset -= stairStepKick * impact;
                    targetImpactPitchOffset += stairPitchKick * impact;
                    targetImpactRollOffset += stairStepSide * stairRollKick * impact;
                    targetImpactShake += stairShake * impact;
                }

                if (playerController.LedgeClimbStartedThisFrame)
                {
                    stairStepSide *= -1f;
                    targetImpactVerticalOffset -= climbStartKick;
                    targetImpactPitchOffset -= climbStartPitchKick;
                    targetImpactRollOffset += stairStepSide * climbStartRollKick;
                    targetImpactShake += climbStartKick;
                }

                if (playerController.LedgeClimbEndedThisFrame)
                {
                    targetImpactVerticalOffset -= climbEndKick;
                    targetImpactPitchOffset += climbEndPitchKick;
                    targetImpactShake += climbEndShake;
                }
            }

            UpdateClimbCameraFeedback();

            float settleSpeed = impactReturnSpeed * 0.45f;
            impactVerticalOffset = Damp(impactVerticalOffset, targetImpactVerticalOffset, impactReturnSpeed);
            impactPitchOffset = Damp(impactPitchOffset, targetImpactPitchOffset, impactReturnSpeed);
            impactRollOffset = Damp(impactRollOffset, targetImpactRollOffset, impactReturnSpeed);
            impactFovOffset = Damp(impactFovOffset, targetImpactFovOffset, impactReturnSpeed);
            impactShake = Damp(impactShake, targetImpactShake, impactReturnSpeed);

            targetImpactVerticalOffset = Damp(targetImpactVerticalOffset, 0f, settleSpeed);
            targetImpactPitchOffset = Damp(targetImpactPitchOffset, 0f, settleSpeed);
            targetImpactRollOffset = Damp(targetImpactRollOffset, 0f, settleSpeed);
            targetImpactFovOffset = Damp(targetImpactFovOffset, 0f, settleSpeed);
            targetImpactShake = Damp(targetImpactShake, 0f, settleSpeed);
        }

        private void UpdateClimbCameraFeedback()
        {
            float targetVertical = 0f;
            float targetPitch = 0f;
            float targetRoll = 0f;

            if (playerController != null && playerController.IsLedgeClimbing)
            {
                float progress = playerController.LedgeClimbProgress;
                float effort = Mathf.Sin(progress * Mathf.PI);
                float reach = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 2.2f));
                targetVertical = -climbPullVerticalOffset * effort;
                targetPitch = Mathf.Lerp(-climbPullPitchOffset, climbPullPitchOffset * 0.45f, reach) * Mathf.Lerp(0.6f, 1f, effort);
                targetRoll = Mathf.Sin(progress * Mathf.PI * 2f) * climbPullRollOffset;
            }

            climbVerticalOffset = Damp(climbVerticalOffset, targetVertical, climbCameraSmoothing);
            climbPitchOffset = Damp(climbPitchOffset, targetPitch, climbCameraSmoothing);
            climbRollOffset = Damp(climbRollOffset, targetRoll, climbCameraSmoothing);
        }

        private float Zoom01 => Mathf.InverseLerp(maximumFieldOfView, minimumFieldOfView, currentFieldOfView);

        private float Noise(float time, float offset)
        {
            return (Mathf.PerlinNoise(noiseSeed + offset, time) - 0.5f) * 2f;
        }

        private static float Damp(float current, float target, float sharpness)
        {
            if (sharpness <= 0f)
            {
                return target;
            }

            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-sharpness * Time.deltaTime));
        }

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            return angle > 180f ? angle - 360f : angle;
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        public void SyncAfterRespawn()
        {
            yaw = yawRoot != null ? yawRoot.rotation.eulerAngles.y : transform.root.rotation.eulerAngles.y;
            pitch = NormalizeAngle(transform.localEulerAngles.x);
            smoothedPitch = pitch;
            smoothedLean = 0f;
            bobTime = 0f;
            impactVerticalOffset = 0f;
            impactPitchOffset = 0f;
            impactRollOffset = 0f;
            impactFovOffset = 0f;
            impactShake = 0f;
            climbVerticalOffset = 0f;
            climbPitchOffset = 0f;
            climbRollOffset = 0f;
            targetImpactVerticalOffset = 0f;
            targetImpactPitchOffset = 0f;
            targetImpactRollOffset = 0f;
            targetImpactFovOffset = 0f;
            targetImpactShake = 0f;
            ZoomDirection = 0;

            if (playerCamera != null)
            {
                currentFieldOfView = playerCamera.fieldOfView;
                scrolledFieldOfView = playerCamera.fieldOfView;
            }
        }

        private void Reset()
        {
            playerController = GetComponentInParent<PlayerController>();
            yawRoot = playerController != null ? playerController.transform : transform.root;
        }
    }
}
