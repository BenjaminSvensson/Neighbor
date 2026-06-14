using System.Linq;
using Neighbor.Main.Features.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

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
                    "OpenDoor",
                    "Climb"
                },
                stateNames);

            Assert.That(states.Single(state => state.state.name == "Climb").state.motion.name, Is.EqualTo("ClimbUp_1m_RM"));
            Assert.That(states.Single(state => state.state.name == "Drop").state.motion.name, Is.EqualTo("PickUp_Table"));
            Assert.That(states.Single(state => state.state.name == "OpenDoor").state.motion.name, Is.EqualTo("Interact"));
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
        }
    }
}
