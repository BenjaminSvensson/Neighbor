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
            Debug.Log("Project validation passed. No missing scripts, invalid materials, or broken LOD references were found.");
            return;
        }

        Debug.LogError($"Project validation found {issueCount} project health issue(s).");
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
                issueCount += ReportVisualHealth(prefab, path);
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
                    issueCount += ReportVisualHealth(roots[rootIndex], path);
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

    private static int ReportVisualHealth(GameObject root, string assetPath)
    {
        int issueCount = 0;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            issueCount += ReportRendererMaterialIssues(renderers[i], assetPath);
        }

        LODGroup[] lodGroups = root.GetComponentsInChildren<LODGroup>(true);
        for (int i = 0; i < lodGroups.Length; i++)
        {
            issueCount += ReportLodIssues(lodGroups[i], assetPath);
        }

        return issueCount;
    }

    private static int ReportRendererMaterialIssues(Renderer renderer, string assetPath)
    {
        if (renderer == null)
        {
            return 0;
        }

        int issueCount = 0;
        Material[] materials = renderer.sharedMaterials;
        if ((renderer is MeshRenderer || renderer is SkinnedMeshRenderer) && (materials == null || materials.Length == 0))
        {
            Debug.LogError(
                $"Renderer has no material slots: '{GetHierarchyPath(renderer.transform)}' in '{assetPath}'.",
                renderer);
            issueCount++;
            return issueCount;
        }

        if (materials == null)
        {
            return issueCount;
        }

        for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
        {
            Material material = materials[materialIndex];
            if (material == null)
            {
                Debug.LogError(
                    $"Missing material reference: slot {materialIndex} on '{GetHierarchyPath(renderer.transform)}' in '{assetPath}'.",
                    renderer);
                issueCount++;
                continue;
            }

            Shader shader = material.shader;
            if (shader == null || IsErrorShader(shader))
            {
                Debug.LogError(
                    $"Invalid material shader: '{material.name}' on '{GetHierarchyPath(renderer.transform)}' in '{assetPath}'.",
                    material);
                issueCount++;
            }
        }

        return issueCount;
    }

    private static int ReportLodIssues(LODGroup lodGroup, string assetPath)
    {
        if (lodGroup == null)
        {
            return 0;
        }

        int issueCount = 0;
        LOD[] lods = lodGroup.GetLODs();
        if (lods == null || lods.Length == 0)
        {
            Debug.LogError(
                $"LODGroup has no LOD levels: '{GetHierarchyPath(lodGroup.transform)}' in '{assetPath}'.",
                lodGroup);
            return 1;
        }

        for (int lodIndex = 0; lodIndex < lods.Length; lodIndex++)
        {
            Renderer[] renderers = lods[lodIndex].renderers;
            if (renderers == null || renderers.Length == 0)
            {
                Debug.LogError(
                    $"LODGroup level {lodIndex} has no renderers: '{GetHierarchyPath(lodGroup.transform)}' in '{assetPath}'.",
                    lodGroup);
                issueCount++;
                continue;
            }

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                if (renderers[rendererIndex] != null)
                {
                    continue;
                }

                Debug.LogError(
                    $"LODGroup level {lodIndex} has a missing renderer reference at index {rendererIndex}: '{GetHierarchyPath(lodGroup.transform)}' in '{assetPath}'.",
                    lodGroup);
                issueCount++;
            }
        }

        return issueCount;
    }

    private static bool IsErrorShader(Shader shader)
    {
        string shaderName = shader.name;
        return string.Equals(shaderName, "Hidden/InternalErrorShader", StringComparison.Ordinal)
            || shaderName.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0;
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
