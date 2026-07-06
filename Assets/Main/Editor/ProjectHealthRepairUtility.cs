#if UNITY_EDITOR
using System;
using Neighbor.Main.Features.Interaction;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class ProjectHealthRepairUtility
{
    private const string MenuPath = "Tools/Neighbor/Repair Project Health Issues";
    private static readonly Vector3 DefaultDoorColliderCenter = new(0f, 1f, 0f);
    private static readonly Vector3 DefaultDoorColliderSize = new(0.18f, 2.1f, 0.9f);

    [MenuItem(MenuPath)]
    private static void RepairFromMenu()
    {
        int repairCount = RepairProject();
        Debug.Log($"Project health repair finished. Applied {repairCount} repair(s).");
    }

    public static void RepairFromCommandLine()
    {
        int repairCount = RepairProject();
        Debug.Log($"Project health repair finished. Applied {repairCount} repair(s).");
        EditorApplication.Exit(0);
    }

    internal static int RepairProject()
    {
        AssetDatabase.Refresh();
        int repairCount = RepairPrefabs();
        repairCount += RepairScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return repairCount;
    }

    private static int RepairPrefabs()
    {
        int repairCount = 0;
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                int prefabRepairCount = RepairGameObject(root, path);
                if (prefabRepairCount == 0)
                {
                    continue;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                if (!saved)
                {
                    Debug.LogError($"Failed to save repaired prefab '{path}'.");
                    continue;
                }

                repairCount += prefabRepairCount;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to repair prefab '{path}': {exception.Message}");
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        return repairCount;
    }

    private static int RepairScenes()
    {
        int repairCount = 0;
        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });

        try
        {
            for (int i = 0; i < sceneGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int sceneRepairCount = 0;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    sceneRepairCount += RepairGameObject(roots[rootIndex], path);
                }

                sceneRepairCount += RepairNavMeshSurfaces(scene, path);
                if (sceneRepairCount == 0)
                {
                    continue;
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                repairCount += sceneRepairCount;
            }
        }
        finally
        {
            if (previousSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }
        }

        return repairCount;
    }

    private static int RepairGameObject(GameObject root, string assetPath)
    {
        if (root == null)
        {
            return 0;
        }

        int repairCount = RepairAudioSources(root, assetPath);
        repairCount += RepairDoors(root, assetPath);
        return repairCount;
    }

    private static int RepairAudioSources(GameObject root, string assetPath)
    {
        int repairCount = 0;
        AudioSource[] audioSources = root.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < audioSources.Length; i++)
        {
            AudioSource audioSource = audioSources[i];
            if (audioSource == null || !audioSource.playOnAwake || audioSource.clip != null)
            {
                continue;
            }

            audioSource.playOnAwake = false;
            EditorUtility.SetDirty(audioSource);
            repairCount++;
            Debug.Log($"Disabled Play On Awake on AudioSource '{GetHierarchyPath(audioSource.transform)}' in '{assetPath}'.", audioSource);
        }

        return repairCount;
    }

    private static int RepairDoors(GameObject root, string assetPath)
    {
        int repairCount = 0;
        Door[] doors = root.GetComponentsInChildren<Door>(true);
        for (int i = 0; i < doors.Length; i++)
        {
            Door door = doors[i];
            if (door == null)
            {
                continue;
            }

            repairCount += RepairDoorHinge(door, assetPath);
            repairCount += RepairDoorCollider(door, assetPath);
        }

        return repairCount;
    }

    private static int RepairDoorHinge(Door door, string assetPath)
    {
        SerializedObject serializedDoor = new(door);
        SerializedProperty hingeProperty = serializedDoor.FindProperty("hinge");
        if (hingeProperty == null || hingeProperty.objectReferenceValue != null)
        {
            return 0;
        }

        hingeProperty.objectReferenceValue = door.transform;
        serializedDoor.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(door);
        Debug.Log($"Repaired Door hinge reference on '{GetHierarchyPath(door.transform)}' in '{assetPath}'.", door);
        return 1;
    }

    private static int RepairDoorCollider(Door door, string assetPath)
    {
        Collider[] colliders = door.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider != null && collider.enabled && !collider.isTrigger)
            {
                return 0;
            }
        }

        BoxCollider boxCollider = door.gameObject.AddComponent<BoxCollider>();
        ConfigureDoorCollider(boxCollider, door.transform);
        EditorUtility.SetDirty(boxCollider);
        Debug.Log($"Added fallback Door collider on '{GetHierarchyPath(door.transform)}' in '{assetPath}'.", door);
        return 1;
    }

    private static void ConfigureDoorCollider(BoxCollider boxCollider, Transform doorTransform)
    {
        Bounds localBounds;
        if (TryGetLocalRendererBounds(doorTransform, out localBounds))
        {
            boxCollider.center = localBounds.center;
            boxCollider.size = Vector3.Max(localBounds.size, DefaultDoorColliderSize * 0.35f);
            return;
        }

        boxCollider.center = DefaultDoorColliderCenter;
        boxCollider.size = DefaultDoorColliderSize;
    }

    private static bool TryGetLocalRendererBounds(Transform root, out Bounds localBounds)
    {
        localBounds = default;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Bounds rendererBounds = renderer.bounds;
            Vector3 min = root.InverseTransformPoint(rendererBounds.min);
            Vector3 max = root.InverseTransformPoint(rendererBounds.max);
            Bounds candidate = new((min + max) * 0.5f, Vector3.Max(max - min, Vector3.zero));
            if (!hasBounds)
            {
                localBounds = candidate;
                hasBounds = true;
                continue;
            }

            localBounds.Encapsulate(candidate.min);
            localBounds.Encapsulate(candidate.max);
        }

        return hasBounds;
    }

    private static int RepairNavMeshSurfaces(Scene scene, string assetPath)
    {
        int repairCount = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            NavMeshSurface[] surfaces = roots[rootIndex].GetComponentsInChildren<NavMeshSurface>(true);
            for (int surfaceIndex = 0; surfaceIndex < surfaces.Length; surfaceIndex++)
            {
                NavMeshSurface surface = surfaces[surfaceIndex];
                if (surface == null || !HasBrokenNavMeshDataReference(surface))
                {
                    continue;
                }

                surface.BuildNavMesh();
                EditorUtility.SetDirty(surface);
                repairCount++;
                Debug.Log($"Rebuilt broken NavMeshSurface data on '{GetHierarchyPath(surface.transform)}' in '{assetPath}'.", surface);
            }
        }

        return repairCount;
    }

    private static bool HasBrokenNavMeshDataReference(NavMeshSurface surface)
    {
        SerializedObject serializedSurface = new(surface);
        SerializedProperty navMeshDataProperty = serializedSurface.FindProperty("m_NavMeshData");
        return navMeshDataProperty != null && HasMissingObjectReference(navMeshDataProperty);
    }

#pragma warning disable CS0618
    private static bool HasMissingObjectReference(SerializedProperty property)
    {
        return property.propertyType == SerializedPropertyType.ObjectReference
            && property.objectReferenceValue == null
            && property.objectReferenceInstanceIDValue != 0;
    }
#pragma warning restore CS0618

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
