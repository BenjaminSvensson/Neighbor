#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class ProjectHealthValidator
{
    private const string MenuPath = "Tools/Neighbor/Validate Project";

    [MenuItem(MenuPath)]
    private static void ValidateFromMenu()
    {
        int issueCount = ValidateProject();
        if (issueCount == 0)
        {
            Debug.Log("Project validation passed. No missing scripts were found.");
            return;
        }

        Debug.LogError($"Project validation found {issueCount} missing script reference(s).");
    }

    public static void ValidateFromCommandLine()
    {
        int issueCount = ValidateProject();
        EditorApplication.Exit(issueCount == 0 ? 0 : 1);
    }

    private static int ValidateProject()
    {
        AssetDatabase.Refresh();
        int issueCount = ValidatePrefabs();
        issueCount += ValidateScenes();
        return issueCount;
    }

    private static int ValidatePrefabs()
    {
        int issueCount = 0;
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                issueCount += ReportMissingScripts(prefab, path);
            }
        }

        return issueCount;
    }

    private static int ValidateScenes()
    {
        int issueCount = 0;
        Scene activeScene = SceneManager.GetActiveScene();
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });

        for (int i = 0; i < sceneGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;

            try
            {
                if (openedForValidation)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }

                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    issueCount += ReportMissingScripts(roots[rootIndex], path);
                }
            }
            catch (Exception exception)
            {
                issueCount++;
                Debug.LogError($"Failed to validate scene '{path}': {exception.Message}");
            }
            finally
            {
                if (openedForValidation && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        if (activeScene.IsValid() && activeScene.isLoaded)
        {
            SceneManager.SetActiveScene(activeScene);
        }

        return issueCount;
    }

    private static int ReportMissingScripts(GameObject root, string assetPath)
    {
        int issueCount = 0;
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            GameObject gameObject = transforms[i].gameObject;
            int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
            if (missingCount == 0)
            {
                continue;
            }

            issueCount += missingCount;
            Debug.LogError(
                $"Missing script reference(s): {missingCount} on '{GetHierarchyPath(gameObject.transform)}' in '{assetPath}'.",
                gameObject);
        }

        return issueCount;
    }

    private static string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = $"{transform.name}/{path}";
        }

        return path;
    }
}
#endif
