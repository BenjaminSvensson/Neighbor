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
    }
}
