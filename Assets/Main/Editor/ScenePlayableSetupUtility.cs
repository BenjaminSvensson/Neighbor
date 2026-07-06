#if UNITY_EDITOR
using Neighbor.Main.Features.Audio;
using Neighbor.Main.Features.Environment;
using Neighbor.Main.Features.Interaction;
using Neighbor.Main.Features.Neighbor;
using Neighbor.Main.Features.Player;
using Neighbor.Main.Features.Progression;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

internal static class ScenePlayableSetupUtility
{
    private const string MenuPath = "Tools/Neighbor/Make Scene Playable";
    private const string MenuPathWithoutBake = "Tools/Neighbor/Make Scene Playable Without NavMesh Bake";
    private const string PlayerPrefabPath = "Assets/Main/Features/Player/Prefabs/Main/Player.prefab";
    private const string NeighborPrefabPath = "Assets/Main/Features/Neighbor/Prefabs/Neighbor.prefab";
    private const string StartCheckpointName = "Start Checkpoint";

    [MenuItem(MenuPath)]
    private static void MakeActiveScenePlayableFromMenu()
    {
        ScenePlayableSetupResult result = MakeActiveScenePlayable(true);
        Debug.Log(result.GetSummary());
    }

    [MenuItem(MenuPathWithoutBake)]
    private static void MakeActiveScenePlayableWithoutBakeFromMenu()
    {
        ScenePlayableSetupResult result = MakeActiveScenePlayable(false);
        Debug.Log(result.GetSummary());
    }

    public static void MakeActiveScenePlayableFromCommandLine()
    {
        ScenePlayableSetupResult result = MakeActiveScenePlayable(true);
        Debug.Log(result.GetSummary());
        EditorApplication.Exit(0);
    }

    internal static ScenePlayableSetupResult MakeActiveScenePlayable(bool bakeNavMesh)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            throw new System.InvalidOperationException("No valid active scene is open.");
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Make Scene Playable");

        ScenePlayableSetupResult result = new(scene.path);
        Vector3 playerPosition = FindPlayableOrigin(scene);

        PlayerController player = EnsurePlayer(scene, playerPosition, result);
        EnsurePlayerRuntimeComponents(player, result);
        NeighborBrain neighbor = EnsureNeighbor(scene, GetGroundedPosition(scene, playerPosition + new Vector3(4f, 0f, 4f)), result);
        CoreLoopObjectiveTracker objectiveTracker = EnsureObjectiveTracker(scene, result);
        PlayerRespawnCheckpoint startCheckpoint = EnsureStartCheckpoint(scene, player, playerPosition, result);
        NavMeshSurface navMeshSurface = EnsureNavMeshSurface(scene, result);
        AmbienceManager ambienceManager = EnsureAmbienceManager(scene, player, result);
        PlayerAwarenessHudView awarenessHud = EnsureAwarenessHud(scene, result);
        PlayerInventoryHudView inventoryHud = EnsureInventoryHud(scene, player, result);
        EventSystem eventSystem = EnsureEventSystem(scene, result);
        Light directionalLight = EnsureDirectionalLight(scene, result);
        Light moonLight = EnsureMoonLight(scene, result);
        DayNightCycle dayNightCycle = EnsureDayNightCycle(scene, directionalLight, moonLight, result);

        result.Player = player;
        result.Neighbor = neighbor;
        result.ObjectiveTracker = objectiveTracker;
        result.StartCheckpoint = startCheckpoint;
        result.NavMeshSurface = navMeshSurface;
        result.AmbienceManager = ambienceManager;
        result.AwarenessHud = awarenessHud;
        result.InventoryHud = inventoryHud;
        result.EventSystem = eventSystem;
        result.DirectionalLight = directionalLight;
        result.MoonLight = moonLight;
        result.DayNightCycle = dayNightCycle;

        if (bakeNavMesh && navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
            result.BakedNavMesh = true;
            EditorUtility.SetDirty(navMeshSurface);
        }

        if (result.HasChanges)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        Undo.CollapseUndoOperations(undoGroup);
        return result;
    }

    private static PlayerController EnsurePlayer(Scene scene, Vector3 position, ScenePlayableSetupResult result)
    {
        PlayerController existingPlayer = FindInScene<PlayerController>(scene);
        if (existingPlayer != null)
        {
            return existingPlayer;
        }

        GameObject playerObject = InstantiatePrefab(PlayerPrefabPath, scene, "Player");
        playerObject.transform.SetPositionAndRotation(position, Quaternion.identity);
        result.CreatedObjectCount++;
        result.CreatedPlayer = true;
        return playerObject.GetComponentInChildren<PlayerController>(true);
    }

    private static NeighborBrain EnsureNeighbor(Scene scene, Vector3 position, ScenePlayableSetupResult result)
    {
        NeighborBrain existingNeighbor = FindInScene<NeighborBrain>(scene);
        if (existingNeighbor != null)
        {
            return existingNeighbor;
        }

        GameObject neighborObject = InstantiatePrefab(NeighborPrefabPath, scene, "Neighbor");
        neighborObject.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.back, Vector3.up));
        result.CreatedObjectCount++;
        result.CreatedNeighbor = true;
        return neighborObject.GetComponentInChildren<NeighborBrain>(true);
    }

    private static void EnsurePlayerRuntimeComponents(PlayerController player, ScenePlayableSetupResult result)
    {
        if (player == null)
        {
            return;
        }

        GameObject playerObject = player.gameObject;

        result.PlayerDeathController = playerObject.GetComponent<PlayerDeathController>();
        if (result.PlayerDeathController == null)
        {
            result.PlayerDeathController = Undo.AddComponent<PlayerDeathController>(playerObject);
            result.AddedComponentCount++;
            result.AddedPlayerDeathController = true;
        }

        result.PlayerKeyRing = playerObject.GetComponent<PlayerKeyRing>();
        if (result.PlayerKeyRing == null)
        {
            result.PlayerKeyRing = Undo.AddComponent<PlayerKeyRing>(playerObject);
            result.AddedComponentCount++;
            result.AddedPlayerKeyRing = true;
        }

        result.PlayerHidingState = playerObject.GetComponent<PlayerHidingState>();
        if (result.PlayerHidingState == null)
        {
            result.PlayerHidingState = Undo.AddComponent<PlayerHidingState>(playerObject);
            result.AddedComponentCount++;
            result.AddedPlayerHidingState = true;
        }

        result.OnboardingDirector = playerObject.GetComponent<PlayerOnboardingDirector>();
        if (result.OnboardingDirector == null)
        {
            result.OnboardingDirector = Undo.AddComponent<PlayerOnboardingDirector>(playerObject);
            result.AddedComponentCount++;
            result.AddedOnboardingDirector = true;
        }

        result.PauseMenu = playerObject.GetComponent<PlayerPauseMenu>();
        if (result.PauseMenu == null)
        {
            result.PauseMenu = Undo.AddComponent<PlayerPauseMenu>(playerObject);
            result.AddedComponentCount++;
            result.AddedPauseMenu = true;
        }

        if (SetSerializedObjectReference(player, "deathController", result.PlayerDeathController))
        {
            result.UpdatedPlayerReferences = true;
        }
    }

    private static CoreLoopObjectiveTracker EnsureObjectiveTracker(Scene scene, ScenePlayableSetupResult result)
    {
        CoreLoopObjectiveTracker tracker = FindInScene<CoreLoopObjectiveTracker>(scene);
        if (tracker != null)
        {
            return tracker;
        }

        GameObject trackerObject = CreateSceneObject(scene, "CoreLoopObjectiveTracker");
        tracker = Undo.AddComponent<CoreLoopObjectiveTracker>(trackerObject);
        result.CreatedObjectCount++;
        result.CreatedObjectiveTracker = true;
        return tracker;
    }

    private static PlayerRespawnCheckpoint EnsureStartCheckpoint(
        Scene scene,
        PlayerController player,
        Vector3 fallbackPosition,
        ScenePlayableSetupResult result)
    {
        PlayerRespawnCheckpoint checkpoint = FindNamedInScene<PlayerRespawnCheckpoint>(scene, StartCheckpointName);
        bool created = false;
        if (checkpoint == null)
        {
            GameObject checkpointObject = CreateSceneObject(scene, StartCheckpointName);
            checkpointObject.transform.SetPositionAndRotation(
                player != null ? player.transform.position : fallbackPosition,
                player != null ? player.transform.rotation : Quaternion.identity);
            checkpoint = Undo.AddComponent<PlayerRespawnCheckpoint>(checkpointObject);
            result.CreatedObjectCount++;
            result.CreatedStartCheckpoint = true;
            created = true;
        }

        if (checkpoint == null)
        {
            return null;
        }

        bool updated = ConfigureStartCheckpoint(checkpoint);
        result.UpdatedStartCheckpoint = result.UpdatedStartCheckpoint || updated && !created;
        return checkpoint;
    }

    private static bool ConfigureStartCheckpoint(PlayerRespawnCheckpoint checkpoint)
    {
        bool changed = false;
        Transform checkpointTransform = checkpoint.transform;
        changed |= SetSerializedObjectReference(checkpoint, "respawnPoint", checkpointTransform);
        changed |= SetSerializedString(checkpoint, "checkpointId", "Start");
        changed |= SetSerializedBool(checkpoint, "persistCheckpoint", false);
        changed |= SetSerializedBool(checkpoint, "activateOnTrigger", true);
        changed |= SetSerializedBool(checkpoint, "activateOnInteract", false);
        changed |= SetSerializedBool(checkpoint, "activateOnce", true);

        BoxCollider trigger = checkpoint.GetComponent<BoxCollider>();
        if (trigger == null)
        {
            trigger = Undo.AddComponent<BoxCollider>(checkpoint.gameObject);
            changed = true;
        }

        if (!trigger.isTrigger)
        {
            trigger.isTrigger = true;
            changed = true;
        }

        Vector3 desiredSize = new(1.5f, 2.2f, 1.5f);
        if ((trigger.size - desiredSize).sqrMagnitude > 0.0001f)
        {
            trigger.size = desiredSize;
            changed = true;
        }

        Vector3 desiredCenter = Vector3.up * 1.1f;
        if ((trigger.center - desiredCenter).sqrMagnitude > 0.0001f)
        {
            trigger.center = desiredCenter;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(checkpoint);
            EditorUtility.SetDirty(trigger);
        }

        return changed;
    }

    private static NavMeshSurface EnsureNavMeshSurface(Scene scene, ScenePlayableSetupResult result)
    {
        NavMeshSurface existingSurface = FindInScene<NavMeshSurface>(scene);
        if (existingSurface != null)
        {
            ConfigureNavMeshSurface(existingSurface);
            return existingSurface;
        }

        GameObject surfaceObject = CreateSceneObject(scene, "NavMesh Surface");
        NavMeshSurface surface = surfaceObject.AddComponent<NavMeshSurface>();
        ConfigureNavMeshSurface(surface);
        result.CreatedObjectCount++;
        result.CreatedNavMeshSurface = true;
        return surface;
    }

    private static void ConfigureNavMeshSurface(NavMeshSurface surface)
    {
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        surface.layerMask = ~0;
        surface.defaultArea = 0;
        EditorUtility.SetDirty(surface);
    }

    private static AmbienceManager EnsureAmbienceManager(Scene scene, PlayerController player, ScenePlayableSetupResult result)
    {
        AmbienceManager manager = FindInScene<AmbienceManager>(scene);
        if (manager == null)
        {
            GameObject managerObject = CreateSceneObject(scene, "AmbienceManager");
            manager = managerObject.AddComponent<AmbienceManager>();
            result.CreatedObjectCount++;
            result.CreatedAmbienceManager = true;
        }

        manager.SetPlayer(player);
        EditorUtility.SetDirty(manager);
        return manager;
    }

    private static PlayerAwarenessHudView EnsureAwarenessHud(Scene scene, ScenePlayableSetupResult result)
    {
        PlayerAwarenessHudView hud = FindInScene<PlayerAwarenessHudView>(scene);
        if (hud != null)
        {
            return hud;
        }

        GameObject hudObject = CreateSceneObject(scene, "PlayerAwarenessHud");
        hud = hudObject.AddComponent<PlayerAwarenessHudView>();
        result.CreatedObjectCount++;
        result.CreatedAwarenessHud = true;
        return hud;
    }

    private static PlayerInventoryHudView EnsureInventoryHud(Scene scene, PlayerController player, ScenePlayableSetupResult result)
    {
        PlayerInventoryHudView hud = FindInScene<PlayerInventoryHudView>(scene);
        PlayerInteractor interactor = player != null ? player.GetComponentInChildren<PlayerInteractor>(true) : null;
        if (hud != null)
        {
            hud.SetInteractor(interactor);
            EditorUtility.SetDirty(hud);
            return hud;
        }

        hud = PlayerInventoryHudView.CreateRuntimeHud(interactor);
        SceneManager.MoveGameObjectToScene(hud.gameObject, scene);
        result.CreatedObjectCount++;
        result.CreatedInventoryHud = true;
        return hud;
    }

    private static EventSystem EnsureEventSystem(Scene scene, ScenePlayableSetupResult result)
    {
        EventSystem eventSystem = FindInScene<EventSystem>(scene);
        if (eventSystem == null)
        {
            GameObject eventSystemObject = CreateSceneObject(scene, "EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
            result.CreatedObjectCount++;
            result.CreatedEventSystem = true;
        }

        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            result.UpdatedEventSystem = true;
        }

        StandaloneInputModule legacyInputModule = eventSystem.GetComponent<StandaloneInputModule>();
        if (legacyInputModule != null)
        {
            Object.DestroyImmediate(legacyInputModule);
            result.UpdatedEventSystem = true;
        }

        EditorUtility.SetDirty(eventSystem);
        return eventSystem;
    }

    private static Light EnsureDirectionalLight(Scene scene, ScenePlayableSetupResult result)
    {
        Light[] lights = FindAllInScene<Light>(scene);
        Light fallback = null;
        for (int i = 0; i < lights.Length; i++)
        {
            Light candidate = lights[i];
            if (candidate == null || candidate.type != LightType.Directional)
            {
                continue;
            }

            string candidateName = candidate.name.ToLowerInvariant();
            if (candidateName.Contains("moon"))
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = candidate;
            }
            if (candidateName.Contains("sun") || candidateName.Contains("directional"))
            {
                return candidate;
            }
        }

        if (fallback != null)
        {
            return fallback;
        }

        GameObject lightObject = CreateSceneObject(scene, "Directional Light");
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;
        result.CreatedObjectCount++;
        result.CreatedDirectionalLight = true;
        return light;
    }

    private static Light EnsureMoonLight(Scene scene, ScenePlayableSetupResult result)
    {
        Light[] lights = FindAllInScene<Light>(scene);
        for (int i = 0; i < lights.Length; i++)
        {
            Light candidate = lights[i];
            if (candidate != null
                && candidate.type == LightType.Directional
                && candidate.name.ToLowerInvariant().Contains("moon"))
            {
                ConfigureMoonLight(candidate);
                return candidate;
            }
        }

        GameObject moonObject = CreateSceneObject(scene, "Moon Light");
        moonObject.transform.rotation = Quaternion.Euler(250f, 150f, 0f);
        Light moon = moonObject.AddComponent<Light>();
        moon.type = LightType.Directional;
        ConfigureMoonLight(moon);
        result.CreatedObjectCount++;
        result.CreatedMoonLight = true;
        return moon;
    }

    private static void ConfigureMoonLight(Light moon)
    {
        moon.color = new Color(0.62f, 0.7f, 1f, 1f);
        moon.intensity = 0.18f;
        moon.shadows = LightShadows.None;
        EditorUtility.SetDirty(moon);
    }

    private static DayNightCycle EnsureDayNightCycle(Scene scene, Light sun, Light moon, ScenePlayableSetupResult result)
    {
        DayNightCycle cycle = FindInScene<DayNightCycle>(scene);
        if (cycle == null)
        {
            GameObject cycleObject = CreateSceneObject(scene, "DayNightCycle");
            cycle = cycleObject.AddComponent<DayNightCycle>();
            cycle.SetLights(sun, moon);
            cycle.SetTimeOfDay(0.36f);
            cycle.SetCycleRunning(true);
            result.CreatedObjectCount++;
            result.CreatedDayNightCycle = true;
            return cycle;
        }

        if (cycle.SunLight != sun || cycle.MoonLight != moon)
        {
            cycle.SetLights(sun, moon);
            result.UpdatedDayNightCycle = true;
        }

        EditorUtility.SetDirty(cycle);
        return cycle;
    }

    private static GameObject InstantiatePrefab(string path, Scene scene, string fallbackName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            throw new System.InvalidOperationException($"Required prefab is missing at '{path}'.");
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance == null)
        {
            throw new System.InvalidOperationException($"Failed to instantiate prefab '{path}'.");
        }

        if (string.IsNullOrWhiteSpace(instance.name))
        {
            instance.name = fallbackName;
        }

        Undo.RegisterCreatedObjectUndo(instance, $"Create {fallbackName}");
        return instance;
    }

    private static GameObject CreateSceneObject(Scene scene, string name)
    {
        GameObject gameObject = new(name);
        SceneManager.MoveGameObjectToScene(gameObject, scene);
        Undo.RegisterCreatedObjectUndo(gameObject, $"Create {name}");
        return gameObject;
    }

    private static Vector3 FindPlayableOrigin(Scene scene)
    {
        Terrain terrain = FindInScene<Terrain>(scene);
        if (terrain != null && terrain.terrainData != null)
        {
            Vector3 center = terrain.transform.position + Vector3.Scale(terrain.terrainData.size, new Vector3(0.5f, 0f, 0.5f));
            return GetGroundedPosition(scene, center);
        }

        Renderer[] renderers = FindAllInScene<Renderer>(scene);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return new Vector3(bounds.center.x, bounds.max.y + 1f, bounds.center.z);
        }

        return Vector3.up;
    }

    private static Vector3 GetGroundedPosition(Scene scene, Vector3 position)
    {
        Terrain terrain = FindInScene<Terrain>(scene);
        if (terrain != null)
        {
            position.y = terrain.transform.position.y + terrain.SampleHeight(position) + 1f;
            return position;
        }

        if (Physics.Raycast(position + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore))
        {
            position.y = hit.point.y + 1f;
            return position;
        }

        position.y = Mathf.Max(position.y, 1f);
        return position;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        T[] components = FindAllInScene<T>(scene);
        return components.Length > 0 ? components[0] : null;
    }

    private static T FindNamedInScene<T>(Scene scene, string objectName) where T : Component
    {
        T[] components = FindAllInScene<T>(scene);
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component != null && component.name == objectName)
            {
                return component;
            }
        }

        return null;
    }

    private static T[] FindAllInScene<T>(Scene scene) where T : Component
    {
        T[] components = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        int writeIndex = 0;
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component != null && component.gameObject.scene == scene)
            {
                components[writeIndex] = component;
                writeIndex++;
            }
        }

        if (writeIndex == components.Length)
        {
            return components;
        }

        T[] filtered = new T[writeIndex];
        System.Array.Copy(components, filtered, writeIndex);
        return filtered;
    }

    private static bool SetSerializedObjectReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue == value)
        {
            return false;
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        return true;
    }

    private static bool SetSerializedString(Object target, string propertyName, string value)
    {
        SerializedObject serializedObject = new(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.stringValue == value)
        {
            return false;
        }

        property.stringValue = value;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        return true;
    }

    private static bool SetSerializedBool(Object target, string propertyName, bool value)
    {
        SerializedObject serializedObject = new(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.boolValue == value)
        {
            return false;
        }

        property.boolValue = value;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        return true;
    }
}

internal sealed class ScenePlayableSetupResult
{
    public ScenePlayableSetupResult(string scenePath)
    {
        ScenePath = scenePath;
    }

    public string ScenePath { get; }
    public PlayerController Player { get; set; }
    public PlayerDeathController PlayerDeathController { get; set; }
    public PlayerKeyRing PlayerKeyRing { get; set; }
    public PlayerHidingState PlayerHidingState { get; set; }
    public PlayerOnboardingDirector OnboardingDirector { get; set; }
    public PlayerPauseMenu PauseMenu { get; set; }
    public NeighborBrain Neighbor { get; set; }
    public CoreLoopObjectiveTracker ObjectiveTracker { get; set; }
    public PlayerRespawnCheckpoint StartCheckpoint { get; set; }
    public NavMeshSurface NavMeshSurface { get; set; }
    public AmbienceManager AmbienceManager { get; set; }
    public PlayerAwarenessHudView AwarenessHud { get; set; }
    public PlayerInventoryHudView InventoryHud { get; set; }
    public EventSystem EventSystem { get; set; }
    public Light DirectionalLight { get; set; }
    public Light MoonLight { get; set; }
    public DayNightCycle DayNightCycle { get; set; }
    public int CreatedObjectCount { get; set; }
    public int AddedComponentCount { get; set; }
    public bool CreatedPlayer { get; set; }
    public bool AddedPlayerDeathController { get; set; }
    public bool AddedPlayerKeyRing { get; set; }
    public bool AddedPlayerHidingState { get; set; }
    public bool AddedOnboardingDirector { get; set; }
    public bool AddedPauseMenu { get; set; }
    public bool UpdatedPlayerReferences { get; set; }
    public bool CreatedNeighbor { get; set; }
    public bool CreatedObjectiveTracker { get; set; }
    public bool CreatedStartCheckpoint { get; set; }
    public bool UpdatedStartCheckpoint { get; set; }
    public bool CreatedNavMeshSurface { get; set; }
    public bool CreatedAmbienceManager { get; set; }
    public bool CreatedAwarenessHud { get; set; }
    public bool CreatedInventoryHud { get; set; }
    public bool CreatedEventSystem { get; set; }
    public bool UpdatedEventSystem { get; set; }
    public bool CreatedDirectionalLight { get; set; }
    public bool CreatedMoonLight { get; set; }
    public bool CreatedDayNightCycle { get; set; }
    public bool UpdatedDayNightCycle { get; set; }
    public bool BakedNavMesh { get; set; }
    public bool HasChanges =>
        CreatedObjectCount > 0
        || AddedComponentCount > 0
        || UpdatedPlayerReferences
        || UpdatedStartCheckpoint
        || UpdatedEventSystem
        || UpdatedDayNightCycle
        || BakedNavMesh;

    public string GetSummary()
    {
        string sceneLabel = string.IsNullOrWhiteSpace(ScenePath) ? "unsaved active scene" : ScenePath;
        return $"Made '{sceneLabel}' playable. Created {CreatedObjectCount} object(s), added {AddedComponentCount} component(s). NavMesh baked: {BakedNavMesh}.";
    }
}
#endif
