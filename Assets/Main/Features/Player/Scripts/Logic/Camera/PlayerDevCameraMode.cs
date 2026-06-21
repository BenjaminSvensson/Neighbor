using System.Collections.Generic;
using Neighbor.Main.Features.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Neighbor.Main.Features.Player
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1500)]
    public sealed class PlayerDevCameraMode : MonoBehaviour
    {
        private enum DevCameraMode
        {
            Off,
            Screenshot,
            Trailer
        }

        [Header("Activation")]
        [SerializeField] private Key screenshotModeKey = Key.F6;
        [SerializeField] private Key trailerModeKey = Key.F7;
        [SerializeField] private Key exitModeKey = Key.Escape;
        [SerializeField] private bool enableInReleaseBuilds;

        [Header("References")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerCameraController playerCameraController;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Transform yawRoot;

        [Header("Flight")]
        [SerializeField, Min(0f)] private float screenshotMoveSpeed = 8f;
        [SerializeField, Min(0f)] private float trailerMoveSpeed = 4f;
        [SerializeField, Min(1f)] private float fastMoveMultiplier = 4f;
        [SerializeField, Min(0f)] private float slowMoveMultiplier = 0.25f;
        [SerializeField, Min(0f)] private float screenshotMoveSmoothing = 14f;
        [SerializeField, Min(0f)] private float trailerMoveSmoothing = 4.5f;

        [Header("Look")]
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.08f;
        [SerializeField] private bool invertLookY;
        [SerializeField] private Vector2 pitchLimits = new(-88f, 88f);
        [SerializeField, Min(0f)] private float screenshotLookSmoothing = 22f;
        [SerializeField, Min(0f)] private float trailerLookSmoothing = 7.5f;
        [SerializeField, Min(0f)] private float rollDegreesPerSecond = 48f;
        [SerializeField, Min(0f)] private float rollSmoothing = 9f;

        [Header("Lens")]
        [SerializeField, Min(1f)] private float minimumFieldOfView = 24f;
        [SerializeField, Min(1f)] private float maximumFieldOfView = 90f;
        [SerializeField, Min(0f)] private float fieldOfViewScrollSpeed = 4f;
        [SerializeField, Min(0f)] private float fieldOfViewSmoothing = 10f;

        private readonly List<CanvasState> hiddenCanvasStates = new();

        private CharacterController characterController;
        private CursorLockMode previousCursorLockMode;
        private bool previousCursorVisible;
        private bool wasPlayerControllerEnabled;
        private bool wasCameraControllerEnabled;
        private bool wasCharacterControllerEnabled;
        private Vector3 restoreCameraLocalPosition;
        private float restoreFieldOfView;
        private Vector3 currentVelocity;
        private float targetYaw;
        private float currentYaw;
        private float targetPitch;
        private float currentPitch;
        private float targetRoll;
        private float currentRoll;
        private float targetFieldOfView;
        private float currentFieldOfView;
        private DevCameraMode currentMode;

        public bool IsActive => currentMode != DevCameraMode.Off;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnDisable()
        {
            if (IsActive)
            {
                DeactivateMode();
            }

            InteractionOverlayState.SetExternalGameplayInputBlocked(this, false);
            RestoreHiddenCanvases();
        }

        private void Update()
        {
            if (!CanUseDevCamera)
            {
                if (IsActive)
                {
                    DeactivateMode();
                }

                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (!IsActive)
            {
                if (WasPressed(keyboard, screenshotModeKey))
                {
                    ActivateMode(DevCameraMode.Screenshot);
                }
                else if (WasPressed(keyboard, trailerModeKey))
                {
                    ActivateMode(DevCameraMode.Trailer);
                }

                return;
            }

            if (WasPressed(keyboard, exitModeKey)
                || currentMode == DevCameraMode.Screenshot && WasPressed(keyboard, screenshotModeKey)
                || currentMode == DevCameraMode.Trailer && WasPressed(keyboard, trailerModeKey))
            {
                DeactivateMode();
                return;
            }

            if (WasPressed(keyboard, screenshotModeKey))
            {
                currentMode = DevCameraMode.Screenshot;
            }
            else if (WasPressed(keyboard, trailerModeKey))
            {
                currentMode = DevCameraMode.Trailer;
            }

            UpdateActiveMode(keyboard);
        }

        private void LateUpdate()
        {
            if (IsActive)
            {
                HideCanvases();
            }
        }

        private void ActivateMode(DevCameraMode mode)
        {
            ResolveReferences();
            if (cameraTransform == null)
            {
                return;
            }

            CaptureGameplayState();
            currentMode = mode;
            currentVelocity = Vector3.zero;

            Vector3 yawEuler = yawRoot != null ? yawRoot.rotation.eulerAngles : transform.rotation.eulerAngles;
            targetYaw = currentYaw = yawEuler.y;
            targetPitch = currentPitch = NormalizeAngle(cameraTransform.localEulerAngles.x);
            targetRoll = currentRoll = NormalizeAngle(cameraTransform.localEulerAngles.z);

            if (playerCamera != null)
            {
                restoreFieldOfView = playerCamera.fieldOfView;
                targetFieldOfView = currentFieldOfView = playerCamera.fieldOfView;
            }

            InteractionOverlayState.SetExternalGameplayInputBlocked(this, true);
            SetCursorLocked(true);
            HideCanvases();
        }

        private void DeactivateMode()
        {
            if (!IsActive)
            {
                return;
            }

            currentMode = DevCameraMode.Off;
            currentVelocity = Vector3.zero;
            InteractionOverlayState.SetExternalGameplayInputBlocked(this, false);
            RestoreHiddenCanvases();

            if (yawRoot != null)
            {
                yawRoot.rotation = Quaternion.Euler(0f, currentYaw, 0f);
            }

            if (cameraTransform != null)
            {
                cameraTransform.localPosition = restoreCameraLocalPosition;
                cameraTransform.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
            }

            if (playerCamera != null)
            {
                playerCamera.fieldOfView = restoreFieldOfView;
            }

            if (playerCameraController != null)
            {
                playerCameraController.SyncAfterRespawn();
                playerCameraController.enabled = wasCameraControllerEnabled;
            }

            if (characterController != null)
            {
                characterController.enabled = wasCharacterControllerEnabled;
            }

            if (playerController != null)
            {
                playerController.StopMotionForExternalControl();
                playerController.enabled = wasPlayerControllerEnabled;
            }

            Cursor.lockState = previousCursorLockMode;
            Cursor.visible = previousCursorVisible;
        }

        private void CaptureGameplayState()
        {
            previousCursorLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            wasPlayerControllerEnabled = playerController != null && playerController.enabled;
            wasCameraControllerEnabled = playerCameraController != null && playerCameraController.enabled;
            wasCharacterControllerEnabled = characterController != null && characterController.enabled;

            if (cameraTransform != null)
            {
                restoreCameraLocalPosition = cameraTransform.localPosition;
            }

            if (playerCamera != null)
            {
                restoreFieldOfView = playerCamera.fieldOfView;
            }

            if (playerController != null)
            {
                playerController.StopMotionForExternalControl();
                playerController.enabled = false;
            }

            if (playerCameraController != null)
            {
                playerCameraController.SyncAfterRespawn();
                playerCameraController.enabled = false;
            }

            if (characterController != null)
            {
                characterController.enabled = false;
            }
        }

        private void UpdateActiveMode(Keyboard keyboard)
        {
            float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

            UpdateLook(keyboard, deltaTime);
            UpdateMovement(keyboard, deltaTime);
            UpdateFieldOfView(deltaTime);
        }

        private void UpdateLook(Keyboard keyboard, float deltaTime)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 look = mouse.delta.ReadValue() * mouseSensitivity;
                targetYaw += look.x;
                targetPitch = Mathf.Clamp(targetPitch + (invertLookY ? look.y : -look.y), pitchLimits.x, pitchLimits.y);
            }

            float rollInput = 0f;
            rollInput += IsPressed(keyboard.qKey) ? 1f : 0f;
            rollInput -= IsPressed(keyboard.eKey) ? 1f : 0f;
            targetRoll += rollInput * rollDegreesPerSecond * deltaTime;
            if (keyboard.rKey.wasPressedThisFrame)
            {
                targetRoll = 0f;
            }

            currentYaw = DampAngle(currentYaw, targetYaw, ActiveLookSmoothing, deltaTime);
            currentPitch = DampAngle(currentPitch, targetPitch, ActiveLookSmoothing, deltaTime);
            currentRoll = DampAngle(currentRoll, targetRoll, rollSmoothing, deltaTime);

            if (yawRoot != null)
            {
                yawRoot.rotation = Quaternion.Euler(0f, currentYaw, 0f);
            }

            if (cameraTransform != null)
            {
                cameraTransform.localRotation = Quaternion.Euler(currentPitch, 0f, currentRoll);
            }
        }

        private void UpdateMovement(Keyboard keyboard, float deltaTime)
        {
            Transform viewTransform = cameraTransform != null ? cameraTransform : transform;
            Vector3 moveInput = Vector3.zero;

            moveInput += viewTransform.forward * ReadAxis(keyboard.wKey, keyboard.sKey);
            moveInput += viewTransform.right * ReadAxis(keyboard.dKey, keyboard.aKey);
            moveInput += Vector3.up * ReadAxis(keyboard.spaceKey, keyboard.leftCtrlKey);
            moveInput = Vector3.ClampMagnitude(moveInput, 1f);

            float speed = ActiveMoveSpeed;
            if (IsPressed(keyboard.leftShiftKey))
            {
                speed *= fastMoveMultiplier;
            }
            else if (IsPressed(keyboard.leftAltKey))
            {
                speed *= slowMoveMultiplier;
            }

            Vector3 targetVelocity = moveInput * speed;
            currentVelocity = Damp(currentVelocity, targetVelocity, ActiveMoveSmoothing, deltaTime);
            transform.position += currentVelocity * deltaTime;
        }

        private void UpdateFieldOfView(float deltaTime)
        {
            if (playerCamera == null)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            float scroll = mouse != null ? mouse.scroll.ReadValue().y : 0f;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                float scrollSteps = Mathf.Abs(scroll) > 10f ? scroll / 120f : scroll;
                targetFieldOfView = Mathf.Clamp(
                    targetFieldOfView - scrollSteps * fieldOfViewScrollSpeed,
                    minimumFieldOfView,
                    maximumFieldOfView);
            }

            currentFieldOfView = Damp(currentFieldOfView, targetFieldOfView, fieldOfViewSmoothing, deltaTime);
            playerCamera.fieldOfView = currentFieldOfView;
        }

        private void HideCanvases()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null)
                {
                    continue;
                }

                if (!HasHiddenCanvas(canvas))
                {
                    hiddenCanvasStates.Add(new CanvasState(canvas, canvas.enabled));
                }

                canvas.enabled = false;
            }
        }

        private void RestoreHiddenCanvases()
        {
            for (int i = 0; i < hiddenCanvasStates.Count; i++)
            {
                CanvasState state = hiddenCanvasStates[i];
                if (state.Canvas != null)
                {
                    state.Canvas.enabled = state.WasEnabled;
                }
            }

            hiddenCanvasStates.Clear();
        }

        private bool HasHiddenCanvas(Canvas canvas)
        {
            for (int i = 0; i < hiddenCanvasStates.Count; i++)
            {
                if (hiddenCanvasStates[i].Canvas == canvas)
                {
                    return true;
                }
            }

            return false;
        }

        private void ResolveReferences()
        {
            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
            }

            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            if (playerCameraController == null)
            {
                playerCameraController = GetComponentInChildren<PlayerCameraController>(true);
            }

            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>(true);
            }

            if (cameraTransform == null && playerCamera != null)
            {
                cameraTransform = playerCamera.transform;
            }

            if (yawRoot == null)
            {
                yawRoot = playerController != null ? playerController.transform : transform;
            }
        }

        private bool CanUseDevCamera
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return enableInReleaseBuilds;
#endif
            }
        }

        private float ActiveMoveSpeed => currentMode == DevCameraMode.Trailer ? trailerMoveSpeed : screenshotMoveSpeed;
        private float ActiveMoveSmoothing => currentMode == DevCameraMode.Trailer ? trailerMoveSmoothing : screenshotMoveSmoothing;
        private float ActiveLookSmoothing => currentMode == DevCameraMode.Trailer ? trailerLookSmoothing : screenshotLookSmoothing;

        private static float ReadAxis(ButtonControl positive, ButtonControl negative)
        {
            float value = 0f;
            value += IsPressed(positive) ? 1f : 0f;
            value -= IsPressed(negative) ? 1f : 0f;
            return value;
        }

        private static bool IsPressed(ButtonControl control)
        {
            return control != null && control.isPressed;
        }

        private static bool WasPressed(Keyboard keyboard, Key key)
        {
            if (keyboard == null || key == Key.None)
            {
                return false;
            }

            KeyControl control = keyboard[key];
            return control != null && control.wasPressedThisFrame;
        }

        private static Vector3 Damp(Vector3 current, Vector3 target, float sharpness, float deltaTime)
        {
            if (sharpness <= 0f)
            {
                return target;
            }

            return Vector3.Lerp(current, target, 1f - Mathf.Exp(-sharpness * deltaTime));
        }

        private static float Damp(float current, float target, float sharpness, float deltaTime)
        {
            if (sharpness <= 0f)
            {
                return target;
            }

            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-sharpness * deltaTime));
        }

        private static float DampAngle(float current, float target, float sharpness, float deltaTime)
        {
            if (sharpness <= 0f)
            {
                return target;
            }

            return Mathf.LerpAngle(current, target, 1f - Mathf.Exp(-sharpness * deltaTime));
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

        private readonly struct CanvasState
        {
            public CanvasState(Canvas canvas, bool wasEnabled)
            {
                Canvas = canvas;
                WasEnabled = wasEnabled;
            }

            public readonly Canvas Canvas;
            public readonly bool WasEnabled;
        }
    }
}
