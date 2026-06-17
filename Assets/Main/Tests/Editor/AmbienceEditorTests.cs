using Neighbor.Main.Features.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Neighbor.Main.Tests
{
    public sealed class AmbienceEditorTests
    {
        private const string AreaPrefabPath = "Assets/Main/Features/Audio/Ambience/Prefabs/AreaSpecificAmbienceArea.prefab";
        private const string LargeAreaPrefabPath = "Assets/Main/Features/Audio/Ambience/Prefabs/LargeAreaSpecificAmbienceArea.prefab";

        [TestCase(AreaPrefabPath)]
        [TestCase(LargeAreaPrefabPath)]
        public void AreaSpecificAmbiencePrefab_HasTriggerColliderAndAreaComponent(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<AmbienceArea>(), Is.Not.Null);

            Collider collider = prefab.GetComponent<Collider>();
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.isTrigger, Is.True);
        }

        [Test]
        public void AmbienceArea_ContainsPointInsideAssignedCollider()
        {
            GameObject areaObject = new("Ambience Area Test");
            try
            {
                BoxCollider collider = areaObject.AddComponent<BoxCollider>();
                collider.size = new Vector3(4f, 3f, 4f);
                collider.center = new Vector3(0f, 1.5f, 0f);
                AmbienceArea area = areaObject.AddComponent<AmbienceArea>();

                Physics.SyncTransforms();

                Assert.That(area.Contains(new Vector3(0f, 1.5f, 0f)), Is.True);
                Assert.That(area.Contains(new Vector3(5f, 1.5f, 0f)), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(areaObject);
            }
        }
    }
}
