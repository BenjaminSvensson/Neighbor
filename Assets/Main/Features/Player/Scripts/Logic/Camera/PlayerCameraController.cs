using Neighbor.Main.Features.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Neighbor.Main.Features.Player
{
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(1000)]
    public sealed class PlayerCameraController : MonoBehaviour
    {
        private const int IgnoreRaycastLayer = 2;
        private const int PlayerLayer = 9;
        private const int DefaultCameraObstructionMask = ~((1 << IgnoreRaycastLayer) | (1 << PlayerLayer));

        [Header("References")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private Transform yawRoot;

        [Header("First Person Body")]
        [SerializeField] private Animator bodyAnimator;
        [SerializeField] private Vector3 headViewOffset = new Vector3(0f, 0.08f, 0.08f);
        [SerializeField, Min(0f)] private float maximumAnimatedHeadDistanceFromAnchor = 1.5f;

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
        [SerializeField, Min(0f)] private float zoomSnapThreshold = 0.08f;
        [SerializeField, Range(0f, 1f)] private float zoomWobbleBoost = 0.55f;

        [Header("Movement Field Of View")]
        [SerializeField, Min(0f)] private float walkFieldOfViewKick = 0.8f;
        [SerializeField, Min(0f)] private float sprintFieldOfViewKick = 3.2f;
        [SerializeField, Min(0f)] private float slideFieldOfViewKick = 4.6f;
        [SerializeField, Min(0f)] private float movementFieldOfViewSharpness = 9f;

        [Header("Stealth Camera Pressure")]
        [SerializeField] private bool respondToStealthLoop = true;
        [SerializeField] private bool respondToNeighborMemory = true;
        [SerializeField] private bool respondToNeighborInvestigation = true;
        [SerializeField] private bool respondToNoiseFeedback = true;
        [SerializeField, Range(0f, 1f)] private float stealthWobbleBoost = 0.45f;
        [SerializeField, Min(0f)] private float stealthShakeAmount = 0.18f;
        [SerializeField, Min(0f)] private float stealthFieldOfViewKick = 2.4f;
        [SerializeField, Range(0f, 1f)] private float calmHidingCameraPressure = 0.08f;
        [SerializeField, Range(0f, 1f)] private float calmPostChaseCameraPressure = 0.14f;
        [SerializeField, Range(0f, 1f)] private float investigationCameraPressure = 0.36f;
        [SerializeField, Range(0f, 1f)] private float trailInvestigationCameraPressure = 0.54f;
        [SerializeField, Range(0f, 1f)] private float resolvedTrailCameraPressure = 0.12f;
        [SerializeField, Range(0f, 1f)] private float heardNoiseCameraPressure = 0.34f;
        [SerializeField, Range(0f, 1f)] private float heardNoiseListenerCameraBoost = 0.08f;
        [SerializeField, Range(0f, 1f)] private float memoryCameraPressure = 0.24f;
        [SerializeField, Range(0f, 1f)] private float stackedMemoryCameraBoost = 0.12f;
        [SerializeField, Min(0f)] private float stealthCameraPressureHoldDuration = 2.4f;
        [SerializeField, Min(0f)] private float investigationCameraPressureHoldDuration = 2.2f;
        [SerializeField, Min(0f)] private float heardNoiseCameraPressureHoldDuration = 1.6f;
        [SerializeField, Min(0f)] private float memoryCameraPressureHoldDuration = 2.8f;
        [SerializeField, Min(1f)] private float maximumMemoryCameraPressureHoldMultiplier = 1.5f;
        [SerializeField, Min(0f)] private float stealthCameraPressureFadeSpeed = 2.1f;

        [Header("Lean")]
        [SerializeField, Min(0f)] private float leanDistance = 0.32f;
        [SerializeField, Min(0f)] private float leanAngle = 9f;
        [SerializeField, Min(0f)] private float leanSmoothing = 12f;

        [Header("Handheld Feel")]
        [SerializeField, Range(0f, 1f)] private float cameraMotionIntensity = 1f;
        [SerializeField, Min(0f)] private float idleWobbleAmount = 0.08f;
        [SerializeField, Min(0f)] private float moveWobbleAmount = 0.16f;
        [SerializeField, Min(0f)] private float runWobbleAmount = 0.28f;
        [SerializeField, Min(0f)] private float wobbleFrequency = 1.7f;
        [SerializeField, Min(0f)] private float bobPositionAmount = 0.055f;
        [SerializeField, Min(0f)] private float bobRollAmount = 1.4f;

        [Header("Camera Collision")]
        [SerializeField] private LayerMask cameraObstructionMask = DefaultCameraObstructionMask;
        [SerializeField, Min(0f)] private float cameraCollisionRadius = 0.12f;
        [SerializeField, Min(0f)] private float cameraCollisionSkin = 0.03f;
        [SerializeField, Min(0.01f)] private float antiPeekNearClipPlane = 0.08f;

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

        [Header("Cinematic Look")]
        [SerializeField, Min(0f)] private float cinematicLookHeight = 1.45f;

        private Camera playerCamera;
        private Transform animatedHead;
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
        private float movementFieldOfViewOffset;
        private float climbVerticalOffset;
        private float climbPitchOffset;
        private float climbRollOffset;
        private float targetImpactVerticalOffset;
        private float targetImpactPitchOffset;
        private float targetImpactRollOffset;
        private float targetImpactFovOffset;
        private float targetImpactShake;
        private float stairStepSide = 1f;
        private Vector3 currentProceduralOffset;
        private bool playZoomAudioThroughSmoothing;
        private PlayerFrameInput frameInput;
        private bool gameplayInputBlocked;
        private Transform cinematicLookTarget;
        private Vector3 cinematicLookOffset;
        private float currentStealthCameraPressure;
        private float targetStealthCameraPressure;
        private float stealthCameraPressureHoldUntilTime;
        private readonly RaycastHit[] cameraCollisionHits = new RaycastHit[8];

        public int ZoomDirection { get; private set; }
        public float RuntimeMouseSensitivity => mouseSensitivity;
        public float RuntimeFieldOfView => maximumFieldOfView;
        public bool RuntimeInvertLookY => invertLookY;
        public float RuntimeCameraMotionIntensity => cameraMotionIntensity;
        public float CurrentStealthCameraPressure => currentStealthCameraPressure;
        public float StealthFieldOfViewOffset => stealthFieldOfViewKick * currentStealthCameraPressure;
        public event System.Action<int> ZoomDirectionChanged;

        private void Awake()
        {
            playerCamera = GetComponent<Camera>();
            baseLocalPosition = transform.localPosition;
            noiseSeed = Random.Range(0f, 1000f);
            ApplyAntiPeekNearClipPlane();

            if (playerController == null)
            {
                playerController = GetComponentInParent<PlayerController>();
            }

            if (yawRoot == null)
            {
                yawRoot = playerController != null ? playerController.transform : transform.root;
            }

            ResolveAnimatedHead();

            SyncLookAnglesFromCurrentView();

            defaultFieldOfView = playerCamera.fieldOfView > 1f ? playerCamera.fieldOfView : defaultFieldOfView;
            minimumFieldOfView = Mathf.Min(minimumFieldOfView, defaultFieldOfView);
            maximumFieldOfView = Mathf.Max(maximumFieldOfView, defaultFieldOfView);
            currentFieldOfView = maximumFieldOfView;
            scrolledFieldOfView = maximumFieldOfView;
            playerCamera.fieldOfView = maximumFieldOfView;
        }

        private void OnEnable()
        {
            PlayerFeedbackEvents.StealthLoopChanged -= HandleStealthLoopChanged;
            PlayerFeedbackEvents.StealthLoopChanged += HandleStealthLoopChanged;
            PlayerFeedbackEvents.NeighborMemoryChanged -= HandleNeighborMemoryChanged;
            PlayerFeedbackEvents.NeighborMemoryChanged += HandleNeighborMemoryChanged;
            PlayerFeedbackEvents.NeighborInvestigationChanged -= HandleNeighborInvestigationChanged;
            PlayerFeedbackEvents.NeighborInvestigationChanged += HandleNeighborInvestigationChanged;
            PlayerFeedbackEvents.NoiseEmitted -= HandleNoiseEmitted;
            PlayerFeedbackEvents.NoiseEmitted += HandleNoiseEmitted;
        }

        private void OnDisable()
        {
            PlayerFeedbackEvents.StealthLoopChanged -= HandleStealthLoopChanged;
            PlayerFeedbackEvents.NeighborMemoryChanged -= HandleNeighborMemoryChanged;
            PlayerFeedbackEvents.NeighborInvestigationChanged -= HandleNeighborInvestigationChanged;
            PlayerFeedbackEvents.NoiseEmitted -= HandleNoiseEmitted;
        }

        private void Start()
        {
            if (lockCursorOnStart)
            {
                SetCursorLocked(true);
            }
        }

        private void Update()
        {
            frameInput = PlayerInputReader.ReadFrameInput(mouseSensitivity, invertLookY);
            gameplayInputBlocked = InteractionOverlayState.IsGameplayInputBlocked;

            if (gameplayInputBlocked)
            {
                playZoomAudioThroughSmoothing = false;
                SetZoomDirection(0);
                return;
            }

            if (frameInput.CursorUnlockPressed)
            {
                SetCursorLocked(false);
            }
            else if (Cursor.lockState != CursorLockMode.Locked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                SetCursorLocked(true);
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                yaw += frameInput.Look.x;
                if (!frameInput.ZoomHeld)
                {
                    pitch = Mathf.Clamp(pitch + frameInput.Look.y, pitchLimits.x, pitchLimits.y);
                }
            }

            UpdateZoom(frameInput);
        }

        private void LateUpdate()
        {
            UpdateStealthCameraPressure(Time.unscaledDeltaTime);
            if (cinematicLookTarget != null)
            {
                UpdateCinematicLook();
                return;
            }

            if (!gameplayInputBlocked)
            {
                UpdateCameraPose(frameInput);
                return;
            }

            UpdateCameraPosition(currentProceduralOffset);
        }

        public void BeginCinematicLookAt(Transform target)
        {
            Animator targetAnimator = target != null ? target.GetComponentInChildren<Animator>(true) : null;
            Transform head = targetAnimator != null && targetAnimator.isHuman
                ? targetAnimator.GetBoneTransform(HumanBodyBones.Head)
                : null;
            cinematicLookTarget = head != null ? head : target;
            cinematicLookOffset = head != null ? Vector3.zero : Vector3.up * cinematicLookHeight;
            UpdateCinematicLook();
        }

        private void UpdateCinematicLook()
        {
            Vector3 lookTarget = cinematicLookTarget.position + cinematicLookOffset;
            Vector3 toTarget = lookTarget - transform.position;
            if (toTarget.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Quaternion desiredWorldRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            Vector3 flatForward = desiredWorldRotation * Vector3.forward;
            flatForward.y = 0f;
            if (yawRoot != null && flatForward.sqrMagnitude > 0.001f)
            {
                yawRoot.rotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
            }

            transform.rotation = desiredWorldRotation;
        }

        private void UpdateZoom(PlayerFrameInput input)
        {
            float previousFieldOfView = scrolledFieldOfView;
            bool hasScrollInput = Mathf.Abs(input.ZoomScroll) > 0.01f;
            bool hasDragInput = input.ZoomHeld && Mathf.Abs(input.ZoomDrag) > 0.01f;

            if (hasScrollInput)
            {
                float scrollSteps = Mathf.Abs(input.ZoomScroll) > 10f
                    ? input.ZoomScroll / 120f
                    : input.ZoomScroll;

                scrolledFieldOfView = Mathf.Clamp(
                    scrolledFieldOfView - scrollSteps * zoomScrollSpeed,
                    minimumFieldOfView,
                    maximumFieldOfView);
            }

            if (hasDragInput)
            {
                scrolledFieldOfView = Mathf.Clamp(
                    scrolledFieldOfView - input.ZoomDrag * zoomDragSpeed,
                    minimumFieldOfView,
                    maximumFieldOfView);
            }

            float targetFieldOfView = Mathf.Clamp(scrolledFieldOfView, minimumFieldOfView, maximumFieldOfView);
            float zoomInputDelta = targetFieldOfView - previousFieldOfView;
            if (Mathf.Abs(zoomInputDelta) > 0.001f)
            {
                playZoomAudioThroughSmoothing = hasScrollInput && !hasDragInput;
                SetZoomDirection(zoomInputDelta < 0f ? 1 : -1);
            }
            else if (hasDragInput)
            {
                playZoomAudioThroughSmoothing = false;
                SetZoomDirection(0);
            }
            else if (playZoomAudioThroughSmoothing && Mathf.Abs(targetFieldOfView - currentFieldOfView) > 0.01f)
            {
                SetZoomDirection(targetFieldOfView < currentFieldOfView ? 1 : -1);
            }
            else
            {
                playZoomAudioThroughSmoothing = false;
                SetZoomDirection(0);
            }

            currentFieldOfView = Damp(currentFieldOfView, targetFieldOfView, zoomSmoothing);
            if (Mathf.Abs(targetFieldOfView - currentFieldOfView) <= zoomSnapThreshold)
            {
                currentFieldOfView = targetFieldOfView;
                playZoomAudioThroughSmoothing = false;
                SetZoomDirection(0);
            }

            UpdateMovementFieldOfViewOffset();
            float motionIntensity = cameraMotionIntensity;
            float maximumProceduralFieldOfView = maximumFieldOfView
                + (Mathf.Max(walkFieldOfViewKick + sprintFieldOfViewKick, slideFieldOfViewKick)
                    + stealthFieldOfViewKick) * motionIntensity;
            playerCamera.fieldOfView = Mathf.Clamp(
                currentFieldOfView
                    + (impactFovOffset + movementFieldOfViewOffset + StealthFieldOfViewOffset) * motionIntensity,
                minimumFieldOfView,
                maximumProceduralFieldOfView);
        }

        private void UpdateMovementFieldOfViewOffset()
        {
            float targetOffset = 0f;
            if (playerController != null)
            {
                targetOffset = Mathf.Lerp(0f, walkFieldOfViewKick, playerController.MoveAmount);
                if (playerController.IsRunning)
                {
                    targetOffset += sprintFieldOfViewKick * Mathf.Max(0.35f, playerController.Speed01);
                }

                if (playerController.IsSliding)
                {
                    targetOffset = Mathf.Max(targetOffset, slideFieldOfViewKick);
                }
            }

            movementFieldOfViewOffset = Damp(
                movementFieldOfViewOffset,
                targetOffset,
                movementFieldOfViewSharpness);
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
            wobbleStrength *= 1f
                + Zoom01 * zoomWobbleBoost
                + currentStealthCameraPressure * stealthWobbleBoost;

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
            float stealthShake = currentStealthCameraPressure * stealthShakeAmount;
            float combinedShake = impactShake + stealthShake;
            float shakeX = Noise(time * 2.4f, 14.23f) * combinedShake;
            float shakeY = Noise(time * 2.1f, 18.67f) * combinedShake;
            float shakeRoll = Noise(time * 2.7f, 22.41f) * combinedShake * 12f;

            Vector3 leanOffset = Vector3.right * (smoothedLean * leanDistance);
            Vector3 impactOffset = new Vector3(shakeX * 0.01f, impactVerticalOffset + climbVerticalOffset + shakeY * 0.01f, 0f);
            Vector3 comfortMotionOffset = bobOffset + impactOffset + new Vector3(wobbleX, wobbleY, 0f) * 0.01f;
            currentProceduralOffset = leanOffset + comfortMotionOffset * cameraMotionIntensity;

            float comfortPitch = wobbleY + impactPitchOffset + climbPitchOffset;
            float comfortRoll = wobbleRoll + shakeRoll + impactRollOffset + climbRollOffset + bobStep * bobRollAmount * moveAmount;
            float roll = -smoothedLean * leanAngle + comfortRoll * cameraMotionIntensity;
            transform.localRotation = Quaternion.Euler(
                smoothedPitch + comfortPitch * cameraMotionIntensity,
                wobbleX * cameraMotionIntensity,
                roll);
            UpdateCameraPosition(currentProceduralOffset);
        }

        private void ResolveAnimatedHead()
        {
            if (bodyAnimator == null && playerController != null)
            {
                bodyAnimator = playerController.GetComponentInChildren<Animator>(true);
            }

            animatedHead = bodyAnimator != null && bodyAnimator.isHuman
                ? bodyAnimator.GetBoneTransform(HumanBodyBones.Head)
                : null;
        }

        private void SyncLookAnglesFromCurrentView()
        {
            Quaternion currentWorldRotation = transform.rotation;
            yaw = ResolveYawFromView(currentWorldRotation);
            pitch = Mathf.Clamp(ResolvePitchForYaw(currentWorldRotation, yaw), pitchLimits.x, pitchLimits.y);
            smoothedPitch = pitch;
        }

        private float ResolveYawFromView(Quaternion currentWorldRotation)
        {
            Vector3 forward = currentWorldRotation * Vector3.forward;
            Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (flatForward.sqrMagnitude > 0.0001f)
            {
                return Quaternion.LookRotation(flatForward.normalized, Vector3.up).eulerAngles.y;
            }

            if (yawRoot != null)
            {
                return yawRoot.rotation.eulerAngles.y;
            }

            return transform.root.rotation.eulerAngles.y;
        }

        private float ResolvePitchForYaw(Quaternion currentWorldRotation, float targetYaw)
        {
            Quaternion parentRotation = GetPredictedParentRotation(Quaternion.Euler(0f, targetYaw, 0f));
            Vector3 localForward = Quaternion.Inverse(parentRotation) * (currentWorldRotation * Vector3.forward);
            if (localForward.sqrMagnitude <= 0.0001f)
            {
                return NormalizeAngle(transform.localEulerAngles.x);
            }

            localForward.Normalize();
            return NormalizeAngle(Mathf.Atan2(-localForward.y, localForward.z) * Mathf.Rad2Deg);
        }

        private Quaternion GetPredictedParentRotation(Quaternion targetYawRootRotation)
        {
            if (transform.parent == null)
            {
                return Quaternion.identity;
            }

            if (yawRoot != null && transform.parent.IsChildOf(yawRoot))
            {
                return targetYawRootRotation * Quaternion.Inverse(yawRoot.rotation) * transform.parent.rotation;
            }

            return transform.parent.rotation;
        }

        private void UpdateCameraPosition(Vector3 proceduralOffset)
        {
            if (animatedHead == null)
            {
                ResolveAnimatedHead();
            }

            Vector3 fallbackLocalPosition = baseLocalPosition + proceduralOffset;
            Vector3 fallbackAnchorPosition = transform.parent != null
                ? transform.parent.TransformPoint(baseLocalPosition)
                : transform.position;

            if (animatedHead == null || yawRoot == null || transform.parent == null)
            {
                if (transform.parent == null)
                {
                    transform.localPosition = fallbackLocalPosition;
                    return;
                }

                Vector3 fallbackDesiredPosition = transform.parent.TransformPoint(fallbackLocalPosition);
                transform.position = ResolveCameraObstruction(fallbackAnchorPosition, fallbackDesiredPosition);
                return;
            }

            Vector3 fallbackPosition = transform.parent.TransformPoint(fallbackLocalPosition);
            Vector3 animatedHeadPosition = animatedHead.position
                + yawRoot.TransformVector(headViewOffset + proceduralOffset);
            float maximumDistance = Mathf.Max(0f, maximumAnimatedHeadDistanceFromAnchor);

            Vector3 desiredPosition = maximumDistance > 0f
                && Vector3.SqrMagnitude(animatedHeadPosition - fallbackPosition) <= maximumDistance * maximumDistance
                    ? animatedHeadPosition
                    : fallbackPosition;

            transform.position = ResolveCameraObstruction(fallbackAnchorPosition, desiredPosition);
        }

        private Vector3 ResolveCameraObstruction(Vector3 anchorPosition, Vector3 desiredPosition)
        {
            Vector3 offset = desiredPosition - anchorPosition;
            float distance = offset.magnitude;
            if (distance <= 0.0001f || cameraObstructionMask.value == 0)
            {
                return desiredPosition;
            }

            Vector3 direction = offset / distance;
            if (!TryGetNearestCameraObstruction(anchorPosition, direction, distance, GetCameraCollisionRadius(), out RaycastHit hit))
            {
                return desiredPosition;
            }

            return anchorPosition + direction * Mathf.Max(0f, hit.distance - cameraCollisionSkin);
        }

        private bool TryGetNearestCameraObstruction(
            Vector3 origin,
            Vector3 direction,
            float distance,
            float radius,
            out RaycastHit nearestHit)
        {
            int hitCount = radius > 0f
                ? Physics.SphereCastNonAlloc(
                    origin,
                    radius,
                    direction,
                    cameraCollisionHits,
                    distance,
                    cameraObstructionMask,
                    QueryTriggerInteraction.Ignore)
                : Physics.RaycastNonAlloc(
                    origin,
                    direction,
                    cameraCollisionHits,
                    distance,
                    cameraObstructionMask,
                    QueryTriggerInteraction.Ignore);

            nearestHit = default;
            float nearestDistance = float.PositiveInfinity;
            bool foundHit = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = cameraCollisionHits[i];
                if (hit.collider == null || ShouldIgnoreCameraObstruction(hit.collider) || hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestHit = hit;
                nearestDistance = hit.distance;
                foundHit = true;
            }

            return foundHit;
        }

        private bool ShouldIgnoreCameraObstruction(Collider hit)
        {
            return hit.transform == transform
                || hit.transform.IsChildOf(transform)
                || (yawRoot != null && hit.transform.IsChildOf(yawRoot));
        }

        private float GetCameraCollisionRadius()
        {
            float radius = Mathf.Max(0f, cameraCollisionRadius);
            if (playerCamera == null)
            {
                return radius;
            }

            float nearClip = Mathf.Max(0.01f, playerCamera.nearClipPlane);
            float halfHeight = Mathf.Tan(playerCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * nearClip;
            float halfWidth = halfHeight * Mathf.Max(1f, playerCamera.aspect);
            return Mathf.Max(radius, Mathf.Sqrt(halfHeight * halfHeight + halfWidth * halfWidth));
        }

        private void ApplyAntiPeekNearClipPlane()
        {
            if (playerCamera == null || antiPeekNearClipPlane <= 0f)
            {
                return;
            }

            playerCamera.nearClipPlane = Mathf.Min(playerCamera.nearClipPlane, antiPeekNearClipPlane);
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

        private void HandleStealthLoopChanged(PlayerFeedbackEvents.StealthLoopFeedback feedback)
        {
            if (!respondToStealthLoop)
            {
                return;
            }

            RaiseStealthCameraPressure(GetStealthCameraPressure(feedback), stealthCameraPressureHoldDuration);
        }

        private void HandleNeighborMemoryChanged(PlayerFeedbackEvents.NeighborMemoryFeedback feedback)
        {
            if (!respondToNeighborMemory)
            {
                return;
            }

            RaiseStealthCameraPressure(GetMemoryCameraPressure(feedback), GetMemoryCameraPressureHoldDuration(feedback));
        }

        private void HandleNeighborInvestigationChanged(PlayerFeedbackEvents.NeighborInvestigationFeedback feedback)
        {
            if (!respondToNeighborInvestigation)
            {
                return;
            }

            RaiseStealthCameraPressure(
                GetInvestigationCameraPressure(feedback),
                GetInvestigationCameraPressureHoldDuration(feedback));
        }

        private void HandleNoiseEmitted(PlayerFeedbackEvents.NoiseFeedback feedback)
        {
            if (!respondToNoiseFeedback || !feedback.HeardByNeighbor)
            {
                return;
            }

            RaiseStealthCameraPressure(GetNoiseCameraPressure(feedback), heardNoiseCameraPressureHoldDuration);
        }

        private void RaiseStealthCameraPressure(float pressure, float holdDuration)
        {
            targetStealthCameraPressure = Mathf.Clamp01(pressure);
            stealthCameraPressureHoldUntilTime = targetStealthCameraPressure <= 0f
                ? 0f
                : Time.unscaledTime + holdDuration;

            if (targetStealthCameraPressure > currentStealthCameraPressure)
            {
                currentStealthCameraPressure = targetStealthCameraPressure;
            }
        }

        private void UpdateStealthCameraPressure(float deltaTime)
        {
            if (!respondToStealthLoop
                && !respondToNeighborMemory
                && !respondToNeighborInvestigation
                && !respondToNoiseFeedback)
            {
                targetStealthCameraPressure = 0f;
            }

            float desiredPressure = Time.unscaledTime <= stealthCameraPressureHoldUntilTime
                ? targetStealthCameraPressure
                : 0f;
            currentStealthCameraPressure = Mathf.MoveTowards(
                currentStealthCameraPressure,
                desiredPressure,
                stealthCameraPressureFadeSpeed * Mathf.Max(0f, deltaTime));
        }

        private float GetStealthCameraPressure(PlayerFeedbackEvents.StealthLoopFeedback feedback)
        {
            float pressure = Mathf.Max(feedback.Suspicion, feedback.Noise, feedback.Tension);
            return feedback.Phase switch
            {
                PlayerFeedbackEvents.StealthLoopPhase.Chased => 1f,
                PlayerFeedbackEvents.StealthLoopPhase.PostChase => feedback.IsCalming
                    ? Mathf.Max(calmPostChaseCameraPressure, feedback.Tension)
                    : Mathf.Max(0.64f, pressure),
                PlayerFeedbackEvents.StealthLoopPhase.Searching => Mathf.Max(0.52f, pressure),
                PlayerFeedbackEvents.StealthLoopPhase.Certain => Mathf.Max(0.78f, pressure),
                PlayerFeedbackEvents.StealthLoopPhase.Suspicious => Mathf.Max(0.4f, pressure),
                PlayerFeedbackEvents.StealthLoopPhase.Hiding => feedback.IsCalming
                    ? Mathf.Max(calmHidingCameraPressure, feedback.Tension)
                    : Mathf.Max(0.3f, feedback.Tension),
                PlayerFeedbackEvents.StealthLoopPhase.Curious => Mathf.Max(0.18f, pressure * 0.65f),
                _ => 0f
            };
        }

        private float GetInvestigationCameraPressure(PlayerFeedbackEvents.NeighborInvestigationFeedback feedback)
        {
            float pressure = Mathf.Max(feedback.Suspicion, feedback.Urgency);
            return feedback.Kind switch
            {
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.CheckingHideSpot => 1f,
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.FollowingTrail =>
                    Mathf.Max(trailInvestigationCameraPressure, pressure),
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Searching when feedback.IsTrailRelated =>
                    Mathf.Max(trailInvestigationCameraPressure, pressure),
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Searching =>
                    Mathf.Max(investigationCameraPressure, pressure),
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Started =>
                    Mathf.Max(investigationCameraPressure, pressure),
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Returning when feedback.IsTrailRelated =>
                    Mathf.Max(resolvedTrailCameraPressure, pressure * 0.24f),
                PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Abandoned when feedback.IsTrailRelated =>
                    Mathf.Max(resolvedTrailCameraPressure, pressure * 0.18f),
                _ => 0f
            };
        }

        private float GetInvestigationCameraPressureHoldDuration(PlayerFeedbackEvents.NeighborInvestigationFeedback feedback)
        {
            if (feedback.Kind == PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Returning
                || feedback.Kind == PlayerFeedbackEvents.NeighborInvestigationFeedbackKind.Abandoned)
            {
                return investigationCameraPressureHoldDuration * 0.7f;
            }

            float pressure = Mathf.Max(feedback.Suspicion, feedback.Urgency);
            return investigationCameraPressureHoldDuration * Mathf.Lerp(1f, 1.45f, pressure);
        }

        private float GetNoiseCameraPressure(PlayerFeedbackEvents.NoiseFeedback feedback)
        {
            float listenerPressure = Mathf.Clamp01(Mathf.Max(0, feedback.NeighborListenerCount - 1) / 2f)
                * heardNoiseListenerCameraBoost;
            return Mathf.Clamp01(Mathf.Max(heardNoiseCameraPressure, Mathf.Max(feedback.Loudness, feedback.Urgency))
                + listenerPressure);
        }

        private float GetMemoryCameraPressure(PlayerFeedbackEvents.NeighborMemoryFeedback feedback)
        {
            float stackPressure = Mathf.Clamp01(feedback.TotalMemoryCount / 4f) * stackedMemoryCameraBoost;
            return Mathf.Clamp01(Mathf.Max(memoryCameraPressure, feedback.Urgency) + stackPressure);
        }

        private float GetMemoryCameraPressureHoldDuration(PlayerFeedbackEvents.NeighborMemoryFeedback feedback)
        {
            float stackPressure = Mathf.Clamp01(Mathf.Max(0, feedback.TotalMemoryCount - 1) / 3f);
            float commitment = Mathf.Max(GetMemoryClueSeverity(feedback.Kind), stackPressure);
            return memoryCameraPressureHoldDuration * Mathf.Lerp(1f, maximumMemoryCameraPressureHoldMultiplier, commitment);
        }

        private static float GetMemoryClueSeverity(PlayerFeedbackEvents.NeighborMemoryClueKind kind)
        {
            return kind switch
            {
                PlayerFeedbackEvents.NeighborMemoryClueKind.KeyStolen => 1f,
                PlayerFeedbackEvents.NeighborMemoryClueKind.GlassBroken => 0.78f,
                PlayerFeedbackEvents.NeighborMemoryClueKind.ObjectMoved => 0.45f,
                PlayerFeedbackEvents.NeighborMemoryClueKind.DoorOpened => 0.16f,
                _ => 0f
            };
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
            cinematicLookTarget = null;
            cinematicLookOffset = Vector3.zero;
            SyncLookAnglesFromCurrentView();
            smoothedLean = 0f;
            bobTime = 0f;
            impactVerticalOffset = 0f;
            impactPitchOffset = 0f;
            impactRollOffset = 0f;
            impactFovOffset = 0f;
            impactShake = 0f;
            movementFieldOfViewOffset = 0f;
            climbVerticalOffset = 0f;
            climbPitchOffset = 0f;
            climbRollOffset = 0f;
            currentProceduralOffset = Vector3.zero;
            targetImpactVerticalOffset = 0f;
            targetImpactPitchOffset = 0f;
            targetImpactRollOffset = 0f;
            targetImpactFovOffset = 0f;
            targetImpactShake = 0f;
            currentStealthCameraPressure = 0f;
            targetStealthCameraPressure = 0f;
            stealthCameraPressureHoldUntilTime = 0f;
            playZoomAudioThroughSmoothing = false;
            SetZoomDirection(0);

            if (playerCamera != null)
            {
                currentFieldOfView = playerCamera.fieldOfView;
                scrolledFieldOfView = playerCamera.fieldOfView;
            }
        }

        public void SetRuntimeMouseSensitivity(float sensitivity)
        {
            mouseSensitivity = Mathf.Max(0f, sensitivity);
        }

        public void SetRuntimeInvertLookY(bool invert)
        {
            invertLookY = invert;
        }

        public void SetRuntimeFieldOfView(float fieldOfView)
        {
            float clampedFieldOfView = Mathf.Clamp(fieldOfView, Mathf.Max(1f, minimumFieldOfView), 100f);
            defaultFieldOfView = clampedFieldOfView;
            maximumFieldOfView = Mathf.Max(clampedFieldOfView, minimumFieldOfView);
            scrolledFieldOfView = Mathf.Clamp(clampedFieldOfView, minimumFieldOfView, maximumFieldOfView);
            currentFieldOfView = scrolledFieldOfView;

            if (playerCamera != null)
            {
                playerCamera.fieldOfView = currentFieldOfView;
            }
        }

        public void SetRuntimeCameraMotionIntensity(float intensity)
        {
            cameraMotionIntensity = Mathf.Clamp01(intensity);
        }

        private void Reset()
        {
            playerController = GetComponentInParent<PlayerController>();
            yawRoot = playerController != null ? playerController.transform : transform.root;
        }

        private void SetZoomDirection(int direction)
        {
            if (ZoomDirection == direction)
            {
                return;
            }

            ZoomDirection = direction;
            ZoomDirectionChanged?.Invoke(direction);
        }
    }
}
