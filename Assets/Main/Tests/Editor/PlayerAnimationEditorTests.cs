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

            string[] stateNames = controller.layers[0].stateMachine.states
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
                    "Land"
                },
                stateNames);
        }

        [Test]
        public void PlayerCamera_DoesNotRenderPlayerLayer()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Camera playerCamera = playerPrefab.GetComponentInChildren<Camera>(true);
            int playerLayer = LayerMask.NameToLayer("Player");

            Assert.That(playerCamera, Is.Not.Null);
            Assert.That(playerLayer, Is.GreaterThanOrEqualTo(0));
            Assert.That(playerCamera.cullingMask & (1 << playerLayer), Is.Zero);
        }
    }
}
