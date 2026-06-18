using System.Linq;
using Neighbor.Main.Features.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
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
    }
}
