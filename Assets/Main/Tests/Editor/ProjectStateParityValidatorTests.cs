using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace Neighbor.Main.Tests
{
    public sealed class ProjectStateParityValidatorTests
    {
        [Test]
        public void ProjectStateParityValidatorScript_IsAvailableForBatchValidation()
        {
            MonoScript validatorScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
                "Assets/Main/Editor/ProjectStateParityValidator.cs");

            Assert.That(validatorScript, Is.Not.Null);
        }

        [Test]
        public void GitAttributes_KeepBinaryTerrainAndNavMeshAssetsInLfs()
        {
            string gitAttributes = File.ReadAllText(".gitattributes");

            Assert.That(gitAttributes, Does.Contain("[attr]unity-binary       filter=lfs diff=lfs merge=lfs -text -eol"));
            Assert.That(gitAttributes, Does.Contain("\"Assets/New Terrain*.asset\" unity-binary"));
            Assert.That(gitAttributes, Does.Contain("Assets/Main/Art/Terrain/Data/*.asset unity-binary"));
            Assert.That(gitAttributes, Does.Contain("Assets/Main/Art/Terrain/Data/**/*.asset unity-binary"));
            Assert.That(gitAttributes, Does.Contain("Assets/**/NavMesh*.asset unity-binary"));
        }

        [Test]
        public void ProjectStateParityValidator_CurrentProjectStatePasses()
        {
            Assert.That(ProjectStateParityValidator.ValidateProjectState(true), Is.Empty);
        }

        [Test]
        public void StashWarnings_FlagUnityProjectStateFiles()
        {
            string[] stashLines =
            {
                "stash@{0}: On main: Backup Main PC local changes"
            };

            var warnings = ProjectStateParityValidator.CollectProjectStateStashWarningsForTests(
                stashLines,
                stashReference => new[]
                {
                    "Assets/Main/Scenes/Testing.unity",
                    "Assets/Main/Features/Interaction/Items/Cameras/PlaceholderCameraBody.mat",
                    "Assets/Main/Scenes/Main/TrueHouse/TrueHouse/NavMesh-NavMesh Surface.asset",
                    "Assets/Main/Features/Player/Scripts/Logic/UI/PlayerAwarenessHudView.cs"
                });

            Assert.That(warnings, Has.Count.EqualTo(1));
            Assert.That(warnings[0], Does.Contain("stash@{0}"));
            Assert.That(warnings[0], Does.Contain("Assets/Main/Scenes/Testing.unity"));
            Assert.That(warnings[0], Does.Contain("PlaceholderCameraBody.mat"));
            Assert.That(warnings[0], Does.Contain("NavMesh-NavMesh Surface.asset"));
            Assert.That(warnings[0], Does.Not.Contain("PlayerAwarenessHudView.cs"));
        }

        [Test]
        public void StashWarnings_IgnoreNonProjectStateCodeOnlyStashes()
        {
            string[] stashLines =
            {
                "stash@{1}: On main: Code scratch"
            };

            var warnings = ProjectStateParityValidator.CollectProjectStateStashWarningsForTests(
                stashLines,
                stashReference => new[]
                {
                    "Assets/Main/Features/Player/Scripts/Logic/UI/PlayerAwarenessHudView.cs",
                    "Tools/Validate-Project.ps1"
                });

            Assert.That(warnings, Is.Empty);
        }
    }
}
