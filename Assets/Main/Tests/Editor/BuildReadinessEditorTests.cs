using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Neighbor.Main.Tests
{
    public sealed class BuildReadinessEditorTests
    {
        [Test]
        public void BuildReadiness_CurrentProjectHasNoBlockingIssues()
        {
            Assert.That(NeighborBuildReadinessValidator.CollectIssues(), Is.Empty);
        }

        [Test]
        public void BuildSettings_StartInTrueTreeHouseAndKeepTestingMapOutOfPlayer()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            EditorBuildSettingsScene[] enabledScenes = scenes.Where(scene => scene.enabled).ToArray();

            Assert.That(enabledScenes, Is.Not.Empty);
            Assert.That(enabledScenes[0].path, Is.EqualTo(NeighborBuildAutomation.PrimaryScenePath));
            Assert.That(
                scenes.Single(scene => scene.path.EndsWith("AITestingMap.unity")).enabled,
                Is.False);
        }

        [Test]
        public void PlayerSettings_UseReleaseSafeIdentityAndDisplayDefaults()
        {
            Assert.That(PlayerSettings.companyName, Is.EqualTo("Benjamin Svensson"));
            Assert.That(PlayerSettings.productName, Is.EqualTo("Neighbor"));
            Assert.That(PlayerSettings.defaultScreenWidth, Is.GreaterThanOrEqualTo(1280));
            Assert.That(PlayerSettings.defaultScreenHeight, Is.GreaterThanOrEqualTo(720));
            Assert.That(PlayerSettings.resizableWindow, Is.True);
            Assert.That(PlayerSettings.colorSpace, Is.EqualTo(ColorSpace.Linear));
        }
    }
}
