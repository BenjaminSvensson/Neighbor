using System.IO;
using System.Linq;
using Neighbor.Main.Features.Interaction;
using Neighbor.Main.Features.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Neighbor.Main.Tests
{
    public sealed class PlayerAnimationEditorTests
    {
        private const string PlayerVisualPath = "Assets/Main/Features/Player/Prefabs/Visual/PlayerVisual.prefab";
        private const string PlayerPrefabPath = "Assets/Main/Features/Player/Prefabs/Main/Player.prefab";

        [Test]
        public void PlayerVisual_UsesHumanoidModelAndPlayerAnimator()
        {
            GameObject playerVisual = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerVisualPath);
            Animator animator = playerVisual.GetComponentInChildren<Animator>(true);

            Assert.That(playerVisual.GetComponent<PlayerAnimationController>(), Is.Not.Null);
            Assert.That(animator, Is.Not.Null);

            AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isHuman, Is.True);
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.layers[0].iKPass, Is.True);
            Assert.That(animator.GetComponent<PlayerHandIK>(), Is.Not.Null);

            SerializedObject animationSettings = new(playerVisual.GetComponent<PlayerAnimationController>());
            Assert.That(animationSettings.FindProperty("grabHoldDuration").floatValue, Is.LessThanOrEqualTo(0.25f));
            Assert.That(animationSettings.FindProperty("dropHoldDuration").floatValue, Is.LessThanOrEqualTo(0.25f));
            Assert.That(animationSettings.FindProperty("grabPlaybackSpeed").floatValue, Is.GreaterThanOrEqualTo(2f));
            Assert.That(animationSettings.FindProperty("dropPlaybackSpeed").floatValue, Is.GreaterThanOrEqualTo(2f));
            Assert.That(animationSettings.FindProperty("interactHoldDuration").floatValue, Is.LessThanOrEqualTo(0.25f));
            Assert.That(animationSettings.FindProperty("interactPlaybackSpeed").floatValue, Is.GreaterThanOrEqualTo(2.5f));
            string[] animationClipProperties =
            {
                "idleAnimation",
                "walkAnimation",
                "runAnimation",
                "crouchIdleAnimation",
                "crouchWalkAnimation",
                "slideAnimation",
                "jumpStartAnimation",
                "airborneAnimation",
                "landAnimation",
                "grabAnimation",
                "dropAnimation",
                "throwAnimation",
                "interactAnimation",
                "climbAnimation"
            };
            foreach (string propertyName in animationClipProperties)
            {
                Assert.That(
                    animationSettings.FindProperty(propertyName).objectReferenceValue,
                    Is.Not.Null,
                    $"{propertyName} should have a default clip.");
            }

            ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
            string[] stateNames = states
                .Select(childState => childState.state.name)
                .ToArray();
            CollectionAssert.IsSubsetOf(
                new[]
                {
                    "Idle",
                    "Walk",
                    "Run",
                    "CrouchIdle",
                    "CrouchWalk",
                    "Slide",
                    "JumpStart",
                    "Airborne",
                    "Land",
                    "Grab",
                    "Drop",
                    "Throw",
                    "Interact",
                    "Climb"
                },
                stateNames);

            Assert.That(states.Single(state => state.state.name == "Climb").state.motion.name, Is.EqualTo("ClimbUp_1m_RM"));
            Assert.That(states.Single(state => state.state.name == "Interact").state.motion.name, Is.EqualTo("Interact"));
            Assert.That(
                states.Single(state => state.state.name == "Grab").state.motion,
                Is.Not.SameAs(states.Single(state => state.state.name == "Drop").state.motion));
            Assert.That(
                states.Single(state => state.state.name == "CrouchWalk").state.motion,
                Is.Not.SameAs(states.Single(state => state.state.name == "Slide").state.motion));
        }

        [Test]
        public void PlayerCamera_RendersPlayerLayerForFirstPersonBody()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Camera playerCamera = playerPrefab.GetComponentInChildren<Camera>(true);
            int playerLayer = LayerMask.NameToLayer("Player");

            Assert.That(playerCamera, Is.Not.Null);
            Assert.That(playerLayer, Is.GreaterThanOrEqualTo(0));
            Assert.That(playerCamera.cullingMask & (1 << playerLayer), Is.Not.Zero);
        }

        [Test]
        public void PlayerCamera_HasAnimatedHumanoidHeadAnchor()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            PlayerCameraController cameraController = playerPrefab.GetComponentInChildren<PlayerCameraController>(true);
            Animator animator = playerPrefab.GetComponentInChildren<Animator>(true);

            Assert.That(cameraController, Is.Not.Null);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.GetBoneTransform(HumanBodyBones.Head), Is.Not.Null);

            SerializedObject cameraSettings = new(cameraController);
            Assert.That(
                cameraSettings.FindProperty("maximumAnimatedHeadDistanceFromAnchor").floatValue,
                Is.InRange(0.1f, 3f));
        }

        [Test]
        public void PlayerCamera_HasWallPeekProtectionDefaults()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            PlayerCameraController cameraController = playerPrefab.GetComponentInChildren<PlayerCameraController>(true);
            int playerLayer = LayerMask.NameToLayer("Player");

            Assert.That(cameraController, Is.Not.Null);
            Assert.That(playerLayer, Is.GreaterThanOrEqualTo(0));

            SerializedObject cameraSettings = new(cameraController);
            int obstructionMask = cameraSettings.FindProperty("cameraObstructionMask").intValue;
            Assert.That(obstructionMask & (1 << playerLayer), Is.Zero);
            Assert.That(obstructionMask & Physics.IgnoreRaycastLayer, Is.Zero);
            Assert.That(cameraSettings.FindProperty("cameraCollisionRadius").floatValue, Is.GreaterThan(0f));
            Assert.That(cameraSettings.FindProperty("antiPeekNearClipPlane").floatValue, Is.InRange(0.01f, 0.1f));
        }

        [Test]
        public void PlayerPresentation_HasGameReadyFeedbackDefaults()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            PlayerInteractor interactor = playerPrefab.GetComponentInChildren<PlayerInteractor>(true);
            PlayerController playerController = playerPrefab.GetComponentInChildren<PlayerController>(true);
            PlayerCameraController cameraController = playerPrefab.GetComponentInChildren<PlayerCameraController>(true);
            PlayerCrosshairFeedback crosshairFeedback = playerPrefab.GetComponentInChildren<PlayerCrosshairFeedback>(true);
            PlayerAudioController audioController = playerPrefab.GetComponentInChildren<PlayerAudioController>(true);
            PlayerDeathController deathController = playerPrefab.GetComponentInChildren<PlayerDeathController>(true);
            Canvas playerCanvas = playerPrefab.GetComponentInChildren<Canvas>(true);
            CanvasScaler canvasScaler = playerCanvas != null ? playerCanvas.GetComponent<CanvasScaler>() : null;
            Graphic crosshairGraphic = crosshairFeedback != null ? crosshairFeedback.GetComponentInChildren<Graphic>(true) : null;

            Assert.That(interactor, Is.Not.Null);
            Assert.That(playerController, Is.Not.Null);
            Assert.That(cameraController, Is.Not.Null);
            Assert.That(crosshairFeedback, Is.Not.Null);
            Assert.That(audioController, Is.Not.Null);
            Assert.That(deathController, Is.Not.Null);
            Assert.That(playerCanvas, Is.Not.Null);
            Assert.That(canvasScaler, Is.Not.Null);
            Assert.That(crosshairGraphic, Is.Not.Null);

            SerializedObject interactorSettings = new(interactor);
            Assert.That(interactorSettings.FindProperty("showThrowArc").boolValue, Is.True);
            Assert.That(interactorSettings.FindProperty("throwArcLineWidth").floatValue, Is.GreaterThanOrEqualTo(0.03f));
            Assert.That(
                interactorSettings.FindProperty("reticleProbeRange").floatValue,
                Is.GreaterThan(interactorSettings.FindProperty("interactRange").floatValue));

            SerializedObject playerSettings = new(playerController);
            Assert.That(playerSettings.FindProperty("maximumStamina").floatValue, Is.GreaterThan(0f));
            Assert.That(playerSettings.FindProperty("sprintStaminaDrainPerSecond").floatValue, Is.GreaterThan(0f));
            Assert.That(
                playerSettings.FindProperty("runNoiseLoudness").floatValue,
                Is.GreaterThan(playerSettings.FindProperty("walkNoiseLoudness").floatValue));
            Assert.That(
                playerSettings.FindProperty("crouchNoiseLoudness").floatValue,
                Is.LessThan(playerSettings.FindProperty("walkNoiseLoudness").floatValue));
            Assert.That(playerSettings.FindProperty("movementNoiseInterval").floatValue, Is.InRange(0.1f, 0.8f));

            SerializedObject cameraSettings = new(cameraController);
            Assert.That(cameraSettings.FindProperty("walkFieldOfViewKick").floatValue, Is.GreaterThan(0f));
            Assert.That(cameraSettings.FindProperty("sprintFieldOfViewKick").floatValue, Is.GreaterThan(0f));
            Assert.That(cameraSettings.FindProperty("slideFieldOfViewKick").floatValue, Is.GreaterThan(0f));
            Assert.That(
                cameraSettings.FindProperty("slideFieldOfViewKick").floatValue,
                Is.GreaterThan(cameraSettings.FindProperty("walkFieldOfViewKick").floatValue));

            SerializedObject crosshairSettings = new(crosshairFeedback);
            Assert.That(crosshairSettings.FindProperty("interactableScale").floatValue, Is.GreaterThan(1f));
            Assert.That(crosshairSettings.FindProperty("interactablePulseScale").floatValue, Is.GreaterThan(0f));
            Assert.That(crosshairSettings.FindProperty("lockedColor"), Is.Not.Null);
            Assert.That(crosshairSettings.FindProperty("tooFarColor"), Is.Not.Null);
            Assert.That(crosshairSettings.FindProperty("holdingColor"), Is.Not.Null);
            Assert.That(crosshairGraphic.raycastTarget, Is.False);

            SerializedObject audioSettings = new(audioController);
            Assert.That(audioSettings.FindProperty("walkFootstepLoop").objectReferenceValue, Is.Not.Null);
            Assert.That(audioSettings.FindProperty("runFootstepLoop").objectReferenceValue, Is.Not.Null);
            Assert.That(audioSettings.FindProperty("crouchFootstepLoop").objectReferenceValue, Is.Not.Null);
            Assert.That(audioSettings.FindProperty("tiredBreathLoop").objectReferenceValue, Is.Not.Null);
            foreach (AudioSource audioSource in playerPrefab.GetComponentsInChildren<AudioSource>(true))
            {
                Assert.That(audioSource.playOnAwake, Is.False, $"{audioSource.name} should not play on awake.");
            }

            SerializedObject deathSettings = new(deathController);
            Assert.That(deathSettings.FindProperty("caughtMessage").stringValue, Is.Not.Empty);
            Assert.That(deathSettings.FindProperty("resetMessage").stringValue, Is.Not.Empty);

            Assert.That(
                AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Main/Features/Player/Scripts/Logic/UI/PlayerPauseMenu.cs"),
                Is.Not.Null);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Main/Features/Player/Scripts/Logic/Movement/PlayerInputBindings.cs"),
                Is.Not.Null);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Main/Features/Player/Scripts/Logic/UI/PlayerPerformanceSettings.cs"),
                Is.Not.Null);

            SerializedObject canvasSettings = new(playerCanvas);
            SerializedProperty receivesEventsProperty = canvasSettings.FindProperty("m_ReceivesEvents");
            Assert.That(receivesEventsProperty, Is.Not.Null);
            Assert.That(receivesEventsProperty.boolValue, Is.False);
            Assert.That(playerCanvas.sortingOrder, Is.GreaterThanOrEqualTo(80));
            Assert.That(canvasScaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
        }

        [Test]
        public void PlayerInputBindings_ArePersistentAndDuplicateSafe()
        {
            try
            {
                PlayerInputBindings.ResetToDefaults();

                Assert.That(PlayerInputBindings.GetBoundKey(PlayerInputBindingAction.Forward), Is.EqualTo(Key.W));
                Assert.That(PlayerInputBindings.GetBoundKey(PlayerInputBindingAction.Backward), Is.EqualTo(Key.S));
                Assert.That(PlayerInputBindings.TrySetBoundKey(PlayerInputBindingAction.Forward, Key.UpArrow), Is.True);
                Assert.That(PlayerInputBindings.GetBoundKey(PlayerInputBindingAction.Forward), Is.EqualTo(Key.UpArrow));

                Assert.That(PlayerInputBindings.TrySetBoundKey(PlayerInputBindingAction.Backward, Key.UpArrow), Is.True);
                Assert.That(PlayerInputBindings.GetBoundKey(PlayerInputBindingAction.Backward), Is.EqualTo(Key.UpArrow));
                Assert.That(PlayerInputBindings.GetBoundKey(PlayerInputBindingAction.Forward), Is.EqualTo(Key.S));
                Assert.That(PlayerInputBindings.TrySetBoundKey(PlayerInputBindingAction.Forward, Key.Escape), Is.False);

                Assert.That(PlayerInputBindings.GetBinding(PlayerInputBindingAction.PrimaryUse).Device, Is.EqualTo(PlayerInputBindingDevice.Mouse));
                Assert.That(PlayerInputBindings.GetBinding(PlayerInputBindingAction.PrimaryUse).MouseButton, Is.EqualTo(PlayerMouseButton.Left));
                Assert.That(PlayerInputBindings.GetControlLabel(PlayerInputBindingAction.PrimaryUse), Is.EqualTo("L MOUSE"));
                Assert.That(PlayerInputBindings.GetBoundKey(PlayerInputBindingAction.InspectHeld), Is.EqualTo(Key.F));
                Assert.That(PlayerInputBindings.GetActionLabel(PlayerInputBindingAction.InspectHeld), Is.EqualTo("Inspect Held"));
                Assert.That(PlayerInputBindings.TrySetBinding(
                    PlayerInputBindingAction.PrimaryUse,
                    PlayerInputControlBinding.ForKeyboard(Key.F)), Is.True);
                Assert.That(PlayerInputBindings.GetBoundKey(PlayerInputBindingAction.PrimaryUse), Is.EqualTo(Key.F));
                Assert.That(PlayerInputBindings.TrySetBinding(
                    PlayerInputBindingAction.Interact,
                    PlayerInputControlBinding.ForKeyboard(Key.F)), Is.True);
                Assert.That(PlayerInputBindings.GetBoundKey(PlayerInputBindingAction.Interact), Is.EqualTo(Key.F));
                Assert.That(PlayerInputBindings.GetBoundKey(PlayerInputBindingAction.PrimaryUse), Is.EqualTo(Key.E));
                Assert.That(PlayerInputBindings.TrySetBinding(
                    PlayerInputBindingAction.Zoom,
                    PlayerInputControlBinding.ForMouse(PlayerMouseButton.None)), Is.False);

                CollectionAssert.Contains(PlayerInputBindings.GetRebindableActions(), PlayerInputBindingAction.Inventory6);
                CollectionAssert.Contains(PlayerInputBindings.GetRebindableActions(), PlayerInputBindingAction.InspectHeld);
            }
            finally
            {
                PlayerInputBindings.ResetToDefaults();
            }
        }

        [Test]
        public void PlayerPerformanceProfiles_CycleThroughExpectedProfiles()
        {
            Assert.That(
                PlayerPerformanceSettings.GetNextProfile(PlayerPerformanceProfile.Performance),
                Is.EqualTo(PlayerPerformanceProfile.Balanced));
            Assert.That(
                PlayerPerformanceSettings.GetNextProfile(PlayerPerformanceProfile.Balanced),
                Is.EqualTo(PlayerPerformanceProfile.Quality));
            Assert.That(
                PlayerPerformanceSettings.GetPreviousProfile(PlayerPerformanceProfile.Performance),
                Is.EqualTo(PlayerPerformanceProfile.Quality));
        }

        [Test]
        public void ProjectQualitySettings_UsePrototypePerformanceBudgets()
        {
            string qualitySettings = File.ReadAllText("ProjectSettings/QualitySettings.asset");

            Assert.That(qualitySettings, Does.Not.Contain("terrainTreeDistance: 5000"));
            Assert.That(qualitySettings, Does.Contain("terrainTreeDistance: 260"));
            Assert.That(qualitySettings, Does.Contain("terrainTreeDistance: 420"));
            Assert.That(qualitySettings, Does.Contain("terrainMaxTrees: 18"));
            Assert.That(qualitySettings, Does.Contain("terrainMaxTrees: 32"));
        }

        [Test]
        public void PlayerCamera_ClampsProceduralLeanBeforeWall()
        {
            GameObject root = new("CameraCollisionRoot");
            GameObject head = new("CameraCollisionHead");
            GameObject cameraObject = new("Camera");
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);

            try
            {
                head.transform.SetParent(root.transform, false);
                cameraObject.transform.SetParent(head.transform, false);

                Camera camera = cameraObject.AddComponent<Camera>();
                camera.nearClipPlane = 0.3f;
                PlayerCameraController cameraController = cameraObject.AddComponent<PlayerCameraController>();

                wall.name = "CameraCollisionWall";
                wall.transform.position = new Vector3(0.45f, 0f, 0f);
                wall.transform.localScale = new Vector3(0.1f, 2f, 2f);
                Physics.SyncTransforms();

                GameplaySmokeTestReflection.InvokeIfPresent(cameraController, "Awake");
                GameplaySmokeTestReflection.Invoke(cameraController, "UpdateCameraPosition", Vector3.right * 0.7f);

                Assert.That(camera.nearClipPlane, Is.LessThanOrEqualTo(0.08f));
                Assert.That(cameraObject.transform.position.x, Is.GreaterThanOrEqualTo(0f));
                Assert.That(cameraObject.transform.position.x, Is.LessThan(0.32f));
            }
            finally
            {
                Object.DestroyImmediate(wall);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(head);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PlayerCamera_StartsFromAuthoredViewDirection()
        {
            GameObject root = new("AuthoredPlayerRoot");
            GameObject head = new("AuthoredPlayerHead");
            GameObject cameraObject = new("AuthoredCamera");

            try
            {
                head.transform.SetParent(root.transform, false);
                cameraObject.transform.SetParent(head.transform, false);

                root.transform.rotation = Quaternion.Euler(0f, 15f, 0f);
                cameraObject.transform.localRotation = Quaternion.Euler(-12f, 55f, 0f);
                Vector3 authoredForward = cameraObject.transform.forward;

                cameraObject.AddComponent<Camera>();
                PlayerCameraController cameraController = cameraObject.AddComponent<PlayerCameraController>();
                GameplaySmokeTestReflection.SetField(cameraController, "idleWobbleAmount", 0f);
                GameplaySmokeTestReflection.SetField(cameraController, "moveWobbleAmount", 0f);
                GameplaySmokeTestReflection.SetField(cameraController, "runWobbleAmount", 0f);
                GameplaySmokeTestReflection.SetField(cameraController, "bobPositionAmount", 0f);
                GameplaySmokeTestReflection.SetField(cameraController, "bobRollAmount", 0f);

                GameplaySmokeTestReflection.InvokeIfPresent(cameraController, "Awake");
                GameplaySmokeTestReflection.Invoke(cameraController, "UpdateCameraPose", default(PlayerFrameInput));

                Assert.That(Vector3.Angle(authoredForward, cameraObject.transform.forward), Is.LessThan(0.5f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(head);
                Object.DestroyImmediate(root);
            }
        }
    }
}
