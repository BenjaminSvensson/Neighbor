using System.IO;
using System.Linq;
using System.Reflection;
using Neighbor.Main.Features.Interaction;
using Neighbor.Main.Features.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using EngineShadowResolution = UnityEngine.ShadowResolution;
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
            Assert.That(
                AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Main/Features/Player/Scripts/Logic/UI/PlayerOnboardingDirector.cs"),
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
        public void PlayerPauseMenu_ResetPersistentSettingsDoesNotResetControlBindings()
        {
            const string SensitivityKey = "Neighbor.MouseSensitivity";
            const string VolumeKey = "Neighbor.MasterVolume";
            const string FieldOfViewKey = "Neighbor.FieldOfView";
            const string InvertYKey = "Neighbor.InvertLookY";
            const string FullscreenKey = "Neighbor.Fullscreen";
            const string FrameRateLimitKey = "Neighbor.FrameRateLimit";

            try
            {
                PlayerInputBindings.ResetToDefaults();
                Assert.That(PlayerInputBindings.TrySetBoundKey(PlayerInputBindingAction.Forward, Key.UpArrow), Is.True);

                MethodInfo resetSettings = typeof(PlayerPauseMenu).GetMethod(
                    "ResetPersistentSettings",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(resetSettings, Is.Not.Null);

                resetSettings.Invoke(
                    null,
                    new object[]
                    {
                        0.11f,
                        0.42f,
                        80f,
                        true,
                        false,
                        PlayerPerformanceProfile.Quality,
                        PlayerFrameRateLimit.Fps120
                    });

                Assert.That(PlayerPrefs.GetFloat(SensitivityKey), Is.EqualTo(0.11f).Within(0.001f));
                Assert.That(PlayerPrefs.GetFloat(VolumeKey), Is.EqualTo(0.42f).Within(0.001f));
                Assert.That(PlayerPrefs.GetFloat(FieldOfViewKey), Is.EqualTo(80f).Within(0.001f));
                Assert.That(PlayerPrefs.GetInt(InvertYKey), Is.EqualTo(1));
                Assert.That(PlayerPrefs.GetInt(FullscreenKey), Is.EqualTo(0));
                Assert.That(PlayerPerformanceSettings.LoadProfile(), Is.EqualTo(PlayerPerformanceProfile.Quality));
                Assert.That(PlayerPerformanceSettings.LoadFrameRateLimit(), Is.EqualTo(PlayerFrameRateLimit.Fps120));
                Assert.That(PlayerInputBindings.GetBoundKey(PlayerInputBindingAction.Forward), Is.EqualTo(Key.UpArrow));
            }
            finally
            {
                PlayerPrefs.DeleteKey(SensitivityKey);
                PlayerPrefs.DeleteKey(VolumeKey);
                PlayerPrefs.DeleteKey(FieldOfViewKey);
                PlayerPrefs.DeleteKey(InvertYKey);
                PlayerPrefs.DeleteKey(FullscreenKey);
                PlayerPrefs.DeleteKey(PlayerPerformanceSettings.PreferenceKey);
                PlayerPrefs.DeleteKey(FrameRateLimitKey);
                PlayerInputBindings.ResetToDefaults();
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void PlayerPauseMenu_LoadsSavedSettingsIntoRuntimeAndUi()
        {
            const string SensitivityKey = "Neighbor.MouseSensitivity";
            const string VolumeKey = "Neighbor.MasterVolume";
            const string FieldOfViewKey = "Neighbor.FieldOfView";
            const string InvertYKey = "Neighbor.InvertLookY";
            const string FullscreenKey = "Neighbor.Fullscreen";
            const string FrameRateLimitKey = "Neighbor.FrameRateLimit";

            QualityRuntimeSnapshot qualitySnapshot = QualityRuntimeSnapshot.Capture();
            UrpRuntimeSnapshot urpSnapshot = UrpRuntimeSnapshot.Capture();
            TerrainRuntimeSnapshot[] terrainSnapshots = TerrainRuntimeSnapshot.CaptureAll();
            PlayerPerformanceProfile originalPerformanceProfile = PlayerPerformanceSettings.CurrentProfile;
            PlayerFrameRateLimit originalFrameRateLimit = PlayerPerformanceSettings.CurrentFrameRateLimit;
            float originalVolume = AudioListener.volume;
            bool originalFullscreen = Screen.fullScreen;
            GameObject root = new("PauseMenuSettingsSmokePlayer");

            try
            {
                PlayerPrefs.SetFloat(SensitivityKey, 0.123f);
                PlayerPrefs.SetFloat(VolumeKey, 0.37f);
                PlayerPrefs.SetFloat(FieldOfViewKey, 82f);
                PlayerPrefs.SetInt(InvertYKey, 1);
                PlayerPrefs.SetInt(FullscreenKey, 0);
                PlayerPrefs.SetInt(PlayerPerformanceSettings.PreferenceKey, (int)PlayerPerformanceProfile.Quality);
                PlayerPrefs.SetInt(PlayerPerformanceSettings.FrameRateLimitPreferenceKey, (int)PlayerFrameRateLimit.Fps120);
                PlayerPrefs.Save();

                PlayerController playerController = root.AddComponent<PlayerController>();
                GameObject cameraObject = new("Player Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera playerCamera = cameraObject.AddComponent<Camera>();
                playerCamera.fieldOfView = 70f;
                PlayerCameraController cameraController = cameraObject.AddComponent<PlayerCameraController>();
                GameplaySmokeTestReflection.InvokeIfPresent(cameraController, "Awake");

                PlayerPauseMenu pauseMenu = root.AddComponent<PlayerPauseMenu>();
                GameplaySmokeTestReflection.InvokeIfPresent(pauseMenu, "Awake");

                Assert.That(
                    GameplaySmokeTestReflection.GetField<float>(playerController, "mouseSensitivity"),
                    Is.EqualTo(0.123f).Within(0.001f));
                Assert.That(cameraController.RuntimeMouseSensitivity, Is.EqualTo(0.123f).Within(0.001f));
                Assert.That(cameraController.RuntimeFieldOfView, Is.EqualTo(82f).Within(0.001f));
                Assert.That(playerController.RuntimeInvertLookY, Is.True);
                Assert.That(cameraController.RuntimeInvertLookY, Is.True);
                Assert.That(playerCamera.fieldOfView, Is.EqualTo(82f).Within(0.001f));
                Assert.That(AudioListener.volume, Is.EqualTo(0.37f).Within(0.001f));
                Assert.That(Application.targetFrameRate, Is.EqualTo(120));

                Assert.That(
                    GameplaySmokeTestReflection.GetField<Text>(pauseMenu, "sensitivityValueText").text,
                    Is.EqualTo("0.123"));
                Assert.That(
                    GameplaySmokeTestReflection.GetField<Text>(pauseMenu, "volumeValueText").text,
                    Is.EqualTo("37"));
                Assert.That(
                    GameplaySmokeTestReflection.GetField<Text>(pauseMenu, "fieldOfViewValueText").text,
                    Is.EqualTo("82"));
                Assert.That(
                    GameplaySmokeTestReflection.GetField<Text>(pauseMenu, "invertLookYValueText").text,
                    Is.EqualTo("ON"));
                Assert.That(
                    GameplaySmokeTestReflection.GetField<Text>(pauseMenu, "fullscreenValueText").text,
                    Is.EqualTo("OFF"));
                Assert.That(
                    GameplaySmokeTestReflection.GetField<Text>(pauseMenu, "performanceProfileValueText").text,
                    Is.EqualTo("QUALITY"));
                Assert.That(
                    GameplaySmokeTestReflection.GetField<Text>(pauseMenu, "frameRateLimitValueText").text,
                    Is.EqualTo("120 FPS"));
            }
            finally
            {
                Object.DestroyImmediate(root);
                PlayerPrefs.DeleteKey(SensitivityKey);
                PlayerPrefs.DeleteKey(VolumeKey);
                PlayerPrefs.DeleteKey(FieldOfViewKey);
                PlayerPrefs.DeleteKey(InvertYKey);
                PlayerPrefs.DeleteKey(FullscreenKey);
                PlayerPrefs.DeleteKey(PlayerPerformanceSettings.PreferenceKey);
                PlayerPrefs.DeleteKey(FrameRateLimitKey);
                PlayerPrefs.Save();
                PlayerPerformanceSettings.ApplyFrameRateLimit(originalFrameRateLimit);
                PlayerPerformanceSettings.ApplyProfile(originalPerformanceProfile);
                AudioListener.volume = originalVolume;
                Screen.fullScreen = originalFullscreen;
                qualitySnapshot.Restore();
                urpSnapshot.Restore();
                TerrainRuntimeSnapshot.RestoreAll(terrainSnapshots);
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
            Assert.That(
                PlayerPerformanceSettings.GetNextFrameRateLimit(PlayerFrameRateLimit.Profile),
                Is.EqualTo(PlayerFrameRateLimit.Fps30));
            Assert.That(
                PlayerPerformanceSettings.GetPreviousFrameRateLimit(PlayerFrameRateLimit.Profile),
                Is.EqualTo(PlayerFrameRateLimit.Unlocked));
            Assert.That(
                PlayerPerformanceSettings.GetFrameRateLimitDisplayName(PlayerFrameRateLimit.Fps120),
                Is.EqualTo("120 FPS"));
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
        public void PlayerPerformanceProfile_AppliesPrototypeRuntimeBudgets()
        {
            QualityRuntimeSnapshot qualitySnapshot = QualityRuntimeSnapshot.Capture();
            UrpRuntimeSnapshot urpSnapshot = UrpRuntimeSnapshot.Capture();
            TerrainRuntimeSnapshot[] terrainSnapshots = TerrainRuntimeSnapshot.CaptureAll();
            PlayerPerformanceProfile originalPerformanceProfile = PlayerPerformanceSettings.CurrentProfile;
            PlayerFrameRateLimit originalFrameRateLimit = PlayerPerformanceSettings.CurrentFrameRateLimit;

            try
            {
                PlayerPerformanceSettings.ApplyFrameRateLimit(PlayerFrameRateLimit.Profile);
                PlayerPerformanceSettings.ApplyProfile(PlayerPerformanceProfile.Performance);

                Assert.That(Application.targetFrameRate, Is.EqualTo(60));
                Assert.That(QualitySettings.vSyncCount, Is.Zero);
                Assert.That(QualitySettings.lodBias, Is.EqualTo(0.85f).Within(0.001f));
                Assert.That(QualitySettings.maximumLODLevel, Is.EqualTo(1));
                Assert.That(QualitySettings.globalTextureMipmapLimit, Is.EqualTo(1));
                Assert.That(QualitySettings.shadowDistance, Is.EqualTo(24f).Within(0.001f));
                Assert.That(QualitySettings.shadowResolution, Is.EqualTo(EngineShadowResolution.Low));

                Terrain[] terrains = Terrain.activeTerrains;
                for (int i = 0; i < terrains.Length; i++)
                {
                    Terrain terrain = terrains[i];
                    if (terrain == null)
                    {
                        continue;
                    }

                    Assert.That(terrain.treeDistance, Is.EqualTo(220f).Within(0.001f));
                    Assert.That(terrain.treeBillboardDistance, Is.EqualTo(32f).Within(0.001f));
                    Assert.That(terrain.treeMaximumFullLODCount, Is.EqualTo(16));
                    Assert.That(terrain.detailObjectDistance, Is.EqualTo(45f).Within(0.001f));
                    Assert.That(terrain.detailObjectDensity, Is.EqualTo(0.55f).Within(0.001f));
                }

                UniversalRenderPipelineAsset urpAsset = UrpRuntimeSnapshot.GetActiveAsset();
                if (urpAsset != null)
                {
                    Assert.That(urpAsset.renderScale, Is.EqualTo(0.85f).Within(0.001f));
                    Assert.That(urpAsset.supportsHDR, Is.False);
                    Assert.That(urpAsset.msaaSampleCount, Is.EqualTo(1));
                    Assert.That(urpAsset.shadowDistance, Is.EqualTo(24f).Within(0.001f));
                    Assert.That(urpAsset.shadowCascadeCount, Is.EqualTo(1));
                    Assert.That(urpAsset.maxAdditionalLightsCount, Is.EqualTo(1));
                }
            }
            finally
            {
                PlayerPerformanceSettings.ApplyFrameRateLimit(originalFrameRateLimit);
                PlayerPerformanceSettings.ApplyProfile(originalPerformanceProfile);
                qualitySnapshot.Restore();
                urpSnapshot.Restore();
                TerrainRuntimeSnapshot.RestoreAll(terrainSnapshots);
            }
        }

        [Test]
        public void PlayerPerformanceFrameRateLimit_OverridesProfileTarget()
        {
            QualityRuntimeSnapshot qualitySnapshot = QualityRuntimeSnapshot.Capture();
            UrpRuntimeSnapshot urpSnapshot = UrpRuntimeSnapshot.Capture();
            TerrainRuntimeSnapshot[] terrainSnapshots = TerrainRuntimeSnapshot.CaptureAll();
            PlayerPerformanceProfile originalPerformanceProfile = PlayerPerformanceSettings.CurrentProfile;
            PlayerFrameRateLimit originalFrameRateLimit = PlayerPerformanceSettings.CurrentFrameRateLimit;

            try
            {
                PlayerPerformanceSettings.ApplyFrameRateLimit(PlayerFrameRateLimit.Fps120);
                PlayerPerformanceSettings.ApplyProfile(PlayerPerformanceProfile.Performance);

                Assert.That(PlayerPerformanceSettings.CurrentFrameRateLimit, Is.EqualTo(PlayerFrameRateLimit.Fps120));
                Assert.That(Application.targetFrameRate, Is.EqualTo(120));

                PlayerPerformanceSettings.ApplyFrameRateLimit(PlayerFrameRateLimit.Unlocked);

                Assert.That(PlayerPerformanceSettings.CurrentFrameRateLimit, Is.EqualTo(PlayerFrameRateLimit.Unlocked));
                Assert.That(Application.targetFrameRate, Is.EqualTo(-1));
            }
            finally
            {
                PlayerPerformanceSettings.ApplyFrameRateLimit(originalFrameRateLimit);
                PlayerPerformanceSettings.ApplyProfile(originalPerformanceProfile);
                qualitySnapshot.Restore();
                urpSnapshot.Restore();
                TerrainRuntimeSnapshot.RestoreAll(terrainSnapshots);
            }
        }

        [Test]
        public void PlayerPerformanceProfile_AppliesCurrentProfileToLateCamera()
        {
            QualityRuntimeSnapshot qualitySnapshot = QualityRuntimeSnapshot.Capture();
            UrpRuntimeSnapshot urpSnapshot = UrpRuntimeSnapshot.Capture();
            TerrainRuntimeSnapshot[] terrainSnapshots = TerrainRuntimeSnapshot.CaptureAll();
            PlayerPerformanceProfile originalPerformanceProfile = PlayerPerformanceSettings.CurrentProfile;
            PlayerFrameRateLimit originalFrameRateLimit = PlayerPerformanceSettings.CurrentFrameRateLimit;
            GameObject cameraObject = new("LatePerformanceCamera");

            try
            {
                PlayerPerformanceSettings.ApplyFrameRateLimit(PlayerFrameRateLimit.Profile);
                PlayerPerformanceSettings.ApplyProfile(PlayerPerformanceProfile.Performance);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.allowHDR = true;
                camera.allowMSAA = true;
                camera.useOcclusionCulling = false;

                PlayerPerformanceSettings.ApplyCurrentProfileToCamera(camera);

                Assert.That(PlayerPerformanceSettings.CurrentProfile, Is.EqualTo(PlayerPerformanceProfile.Performance));
                Assert.That(camera.allowHDR, Is.False);
                Assert.That(camera.allowMSAA, Is.False);
                Assert.That(camera.useOcclusionCulling, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                PlayerPerformanceSettings.ApplyFrameRateLimit(originalFrameRateLimit);
                PlayerPerformanceSettings.ApplyProfile(originalPerformanceProfile);
                qualitySnapshot.Restore();
                urpSnapshot.Restore();
                TerrainRuntimeSnapshot.RestoreAll(terrainSnapshots);
            }
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

        private readonly struct QualityRuntimeSnapshot
        {
            private readonly int qualityLevel;
            private readonly int targetFrameRate;
            private readonly int vSyncCount;
            private readonly float lodBias;
            private readonly int maximumLodLevel;
            private readonly int globalTextureMipmapLimit;
            private readonly bool streamingMipmapsActive;
            private readonly float streamingMipmapsMemoryBudget;
            private readonly int particleRaycastBudget;
            private readonly float shadowDistance;
            private readonly EngineShadowResolution shadowResolution;

            private QualityRuntimeSnapshot(
                int qualityLevel,
                int targetFrameRate,
                int vSyncCount,
                float lodBias,
                int maximumLodLevel,
                int globalTextureMipmapLimit,
                bool streamingMipmapsActive,
                float streamingMipmapsMemoryBudget,
                int particleRaycastBudget,
                float shadowDistance,
                EngineShadowResolution shadowResolution)
            {
                this.qualityLevel = qualityLevel;
                this.targetFrameRate = targetFrameRate;
                this.vSyncCount = vSyncCount;
                this.lodBias = lodBias;
                this.maximumLodLevel = maximumLodLevel;
                this.globalTextureMipmapLimit = globalTextureMipmapLimit;
                this.streamingMipmapsActive = streamingMipmapsActive;
                this.streamingMipmapsMemoryBudget = streamingMipmapsMemoryBudget;
                this.particleRaycastBudget = particleRaycastBudget;
                this.shadowDistance = shadowDistance;
                this.shadowResolution = shadowResolution;
            }

            public static QualityRuntimeSnapshot Capture()
            {
                return new QualityRuntimeSnapshot(
                    QualitySettings.GetQualityLevel(),
                    Application.targetFrameRate,
                    QualitySettings.vSyncCount,
                    QualitySettings.lodBias,
                    QualitySettings.maximumLODLevel,
                    QualitySettings.globalTextureMipmapLimit,
                    QualitySettings.streamingMipmapsActive,
                    QualitySettings.streamingMipmapsMemoryBudget,
                    QualitySettings.particleRaycastBudget,
                    QualitySettings.shadowDistance,
                    QualitySettings.shadowResolution);
            }

            public void Restore()
            {
                string[] qualityNames = QualitySettings.names;
                if (qualityNames != null && qualityNames.Length > 0)
                {
                    QualitySettings.SetQualityLevel(Mathf.Clamp(qualityLevel, 0, qualityNames.Length - 1), true);
                }

                Application.targetFrameRate = targetFrameRate;
                QualitySettings.vSyncCount = vSyncCount;
                QualitySettings.lodBias = lodBias;
                QualitySettings.maximumLODLevel = maximumLodLevel;
                QualitySettings.globalTextureMipmapLimit = globalTextureMipmapLimit;
                QualitySettings.streamingMipmapsActive = streamingMipmapsActive;
                QualitySettings.streamingMipmapsMemoryBudget = streamingMipmapsMemoryBudget;
                QualitySettings.particleRaycastBudget = particleRaycastBudget;
                QualitySettings.shadowDistance = shadowDistance;
                QualitySettings.shadowResolution = shadowResolution;
            }
        }

        private readonly struct UrpRuntimeSnapshot
        {
            private readonly UniversalRenderPipelineAsset asset;
            private readonly float renderScale;
            private readonly bool supportsHdr;
            private readonly bool supportsCameraOpaqueTexture;
            private readonly int msaaSampleCount;
            private readonly float shadowDistance;
            private readonly int shadowCascadeCount;
            private readonly int mainLightShadowmapResolution;
            private readonly int maxAdditionalLightsCount;
            private readonly int additionalLightsShadowmapResolution;

            private UrpRuntimeSnapshot(UniversalRenderPipelineAsset asset)
            {
                this.asset = asset;
                renderScale = asset != null ? asset.renderScale : 0f;
                supportsHdr = asset != null && asset.supportsHDR;
                supportsCameraOpaqueTexture = asset != null && asset.supportsCameraOpaqueTexture;
                msaaSampleCount = asset != null ? asset.msaaSampleCount : 0;
                shadowDistance = asset != null ? asset.shadowDistance : 0f;
                shadowCascadeCount = asset != null ? asset.shadowCascadeCount : 0;
                mainLightShadowmapResolution = asset != null ? asset.mainLightShadowmapResolution : 0;
                maxAdditionalLightsCount = asset != null ? asset.maxAdditionalLightsCount : 0;
                additionalLightsShadowmapResolution = asset != null ? asset.additionalLightsShadowmapResolution : 0;
            }

            public static UrpRuntimeSnapshot Capture()
            {
                return new UrpRuntimeSnapshot(GetActiveAsset());
            }

            public static UniversalRenderPipelineAsset GetActiveAsset()
            {
                RenderPipelineAsset renderPipelineAsset = GraphicsSettings.currentRenderPipeline != null
                    ? GraphicsSettings.currentRenderPipeline
                    : QualitySettings.renderPipeline;
                return renderPipelineAsset as UniversalRenderPipelineAsset;
            }

            public void Restore()
            {
                if (asset == null)
                {
                    return;
                }

                asset.renderScale = renderScale;
                asset.supportsHDR = supportsHdr;
                asset.supportsCameraOpaqueTexture = supportsCameraOpaqueTexture;
                asset.msaaSampleCount = msaaSampleCount;
                asset.shadowDistance = shadowDistance;
                asset.shadowCascadeCount = shadowCascadeCount;
                asset.mainLightShadowmapResolution = mainLightShadowmapResolution;
                asset.maxAdditionalLightsCount = maxAdditionalLightsCount;
                asset.additionalLightsShadowmapResolution = additionalLightsShadowmapResolution;
            }
        }

        private readonly struct TerrainRuntimeSnapshot
        {
            private readonly Terrain terrain;
            private readonly bool drawTreesAndFoliage;
            private readonly float treeDistance;
            private readonly float treeBillboardDistance;
            private readonly float treeCrossFadeLength;
            private readonly int treeMaximumFullLodCount;
            private readonly float detailObjectDistance;
            private readonly float detailObjectDensity;
            private readonly float heightmapPixelError;
            private readonly float basemapDistance;

            private TerrainRuntimeSnapshot(Terrain terrain)
            {
                this.terrain = terrain;
                drawTreesAndFoliage = terrain != null && terrain.drawTreesAndFoliage;
                treeDistance = terrain != null ? terrain.treeDistance : 0f;
                treeBillboardDistance = terrain != null ? terrain.treeBillboardDistance : 0f;
                treeCrossFadeLength = terrain != null ? terrain.treeCrossFadeLength : 0f;
                treeMaximumFullLodCount = terrain != null ? terrain.treeMaximumFullLODCount : 0;
                detailObjectDistance = terrain != null ? terrain.detailObjectDistance : 0f;
                detailObjectDensity = terrain != null ? terrain.detailObjectDensity : 0f;
                heightmapPixelError = terrain != null ? terrain.heightmapPixelError : 0f;
                basemapDistance = terrain != null ? terrain.basemapDistance : 0f;
            }

            public static TerrainRuntimeSnapshot[] CaptureAll()
            {
                Terrain[] terrains = Terrain.activeTerrains;
                return terrains == null
                    ? new TerrainRuntimeSnapshot[0]
                    : terrains.Where(terrain => terrain != null).Select(terrain => new TerrainRuntimeSnapshot(terrain)).ToArray();
            }

            public static void RestoreAll(TerrainRuntimeSnapshot[] snapshots)
            {
                for (int i = 0; i < snapshots.Length; i++)
                {
                    snapshots[i].Restore();
                }
            }

            private void Restore()
            {
                if (terrain == null)
                {
                    return;
                }

                terrain.drawTreesAndFoliage = drawTreesAndFoliage;
                terrain.treeDistance = treeDistance;
                terrain.treeBillboardDistance = treeBillboardDistance;
                terrain.treeCrossFadeLength = treeCrossFadeLength;
                terrain.treeMaximumFullLODCount = treeMaximumFullLodCount;
                terrain.detailObjectDistance = detailObjectDistance;
                terrain.detailObjectDensity = detailObjectDensity;
                terrain.heightmapPixelError = heightmapPixelError;
                terrain.basemapDistance = basemapDistance;
            }
        }
    }
}
