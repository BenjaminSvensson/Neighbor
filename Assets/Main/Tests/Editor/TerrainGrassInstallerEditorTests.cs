using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Neighbor.Main.Tests
{
    public sealed class TerrainGrassInstallerEditorTests
    {
        [Test]
        public void BasicTreeTerrainPrefab_HasRootMeshRendererForTerrainPainting()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Main/Art/Models/TreeObjects/BasicTreeTerrain.prefab");

            Assert.That(prefab, Is.Not.Null);
            Assert.That(global::TerrainGrassInstaller.HasTerrainCompatibleTreeRenderer(prefab), Is.True);

            MeshRenderer renderer = prefab.GetComponent<MeshRenderer>();
            Assert.That(renderer.sharedMaterials, Has.All.Not.Null);
            foreach (Material material in renderer.sharedMaterials)
            {
                Assert.That(global::TerrainGrassInstaller.IsRenderableTerrainTreeMaterial(material), Is.True);
                Assert.That(global::TerrainGrassInstaller.UsesTerrainTreeShader(material), Is.True);
                Assert.That(global::TerrainGrassInstaller.HasTerrainTreeAlbedoTexture(material), Is.True);
                Assert.That(material.shader.name, Does.StartWith("Nature/Soft Occlusion"));
            }
        }
    }
}
