#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Neighbor.Main.HouseBuilder.Editor
{
    [InitializeOnLoad]
    public static class HouseBuilderSceneRepairUtility
    {
        static HouseBuilderSceneRepairUtility()
        {
            EditorApplication.delayCall += RepairOpenScenes;
        }

        public static void RepairOpenScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                int repairedInScene = 0;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    HouseBuilderWorld[] worlds = roots[rootIndex].GetComponentsInChildren<HouseBuilderWorld>(true);
                    for (int worldIndex = 0; worldIndex < worlds.Length; worldIndex++)
                    {
                        HouseBuilderWorld world = worlds[worldIndex];
                        int repaired = world != null ? world.EnsureDefinitionFeatures() : 0;
                        if (repaired <= 0)
                        {
                            continue;
                        }

                        repairedInScene += repaired;
                        EditorUtility.SetDirty(world);
                        EditorUtility.SetDirty(world.WireGraph);
                    }
                }

                if (repairedInScene > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }
        }
    }
}
#endif
