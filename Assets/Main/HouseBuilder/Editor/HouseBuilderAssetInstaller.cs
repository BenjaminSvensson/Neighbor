#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Neighbor.Main.HouseBuilder.Editor
{
    [InitializeOnLoad]
    public static class HouseBuilderAssetInstaller
    {
        public const string RootPath = "Assets/Main/HouseBuilder";
        public const string DataPath = RootPath + "/Data";
        public const string DefaultCatalogPath = DataPath + "/DefaultHouseBuilderCatalog.asset";
        private const int InstallerVersion = 3;
        private const string InstallerVersionKey = "Neighbor.HouseBuilder.DefaultAssetsVersion";
        private const string CategoryPath = DataPath + "/Categories";
        private const string DefinitionPath = DataPath + "/Placeables";
        private const string MaterialPath = DataPath + "/Materials";
        private const string PrefabPath = RootPath + "/Prefabs";

        static HouseBuilderAssetInstaller()
        {
            EditorApplication.delayCall += EnsureDefaults;
        }

        [MenuItem("Tools/Neighbor/House Builder/Create or Refresh Default Assets")]
        public static void CreateOrRefreshDefaults()
        {
            EnsureFolders();

            List<HouseBuilderCategoryDefinition> categories = new()
            {
                CreateCategory("Structure", HouseBuilderCategories.Structure, "Structure", new Color(0.55f, 0.72f, 0.9f)),
                CreateCategory("Walls", HouseBuilderCategories.Wall, "Walls", new Color(0.55f, 0.72f, 0.9f)),
                CreateCategory("Floors", HouseBuilderCategories.Floor, "Floors", new Color(0.72f, 0.6f, 0.42f)),
                CreateCategory("Ceilings", HouseBuilderCategories.Ceiling, "Ceilings", new Color(0.82f, 0.82f, 0.9f)),
                CreateCategory("Doors", HouseBuilderCategories.Door, "Doors", new Color(0.65f, 0.42f, 0.24f)),
                CreateCategory("Windows", HouseBuilderCategories.Window, "Windows", new Color(0.35f, 0.78f, 0.95f)),
                CreateCategory("Furniture", HouseBuilderCategories.Furniture, "Furniture", new Color(0.85f, 0.58f, 0.3f)),
                CreateCategory("Props", HouseBuilderCategories.Prop, "Props", new Color(0.85f, 0.85f, 0.85f)),
                CreateCategory("SearchSpots", HouseBuilderCategories.SearchSpot, "Search Spots", new Color(1f, 0.65f, 0.1f)),
                CreateCategory("TaskSpots", HouseBuilderCategories.TaskSpot, "Task Spots", new Color(0.1f, 0.85f, 1f)),
                CreateCategory("PatrolPoints", HouseBuilderCategories.PatrolPoint, "Patrol Points", new Color(0.1f, 0.95f, 0.7f)),
                CreateCategory("ReinforcementTriggers", HouseBuilderCategories.ReinforcementTrigger, "Reinforcement Triggers", new Color(1f, 0.2f, 0.12f)),
                CreateCategory("Reinforcements", HouseBuilderCategories.Reinforcement, "Reinforcements", new Color(0.9f, 0.2f, 0.55f)),
                CreateCategory("NeighborSpawnPoints", HouseBuilderCategories.NeighborSpawnPoint, "Neighbor Spawn Points", new Color(0.75f, 0.2f, 1f)),
                CreateCategory("Wiring", HouseBuilderCategories.Wiring, "Wiring", new Color(1f, 0.85f, 0.1f))
            };

            List<HousePlaceableDefinition> placeables = new();
            AddDefinition(placeables, "Door", "Door", HouseBuilderCategories.Door,
                "Assets/Main/Features/Interaction/Items/Doors/Prefabs/Door.prefab", HouseSurfaceType.Wall, HouseSurfaceAlignment.ForwardToNormal,
                true, new Vector3(1.3f, 2.25f, 0.6f), new Vector3(0f, 1.125f, 0f));
            AddDefinition(placeables, "LockedDoor", "Locked Door", HouseBuilderCategories.Door,
                "Assets/Main/Features/Interaction/Items/Doors/Prefabs/LockedDoor.prefab", HouseSurfaceType.Wall, HouseSurfaceAlignment.ForwardToNormal,
                true, new Vector3(1.3f, 2.25f, 0.6f), new Vector3(0f, 1.125f, 0f));
            AddDefinition(placeables, "WindowBlinds", "Window", HouseBuilderCategories.Window,
                "Assets/Main/Features/Interaction/Items/Windows/Blinds/Prefabs/PlaceholderWindowBlinds.prefab", HouseSurfaceType.Wall, HouseSurfaceAlignment.ForwardToNormal,
                true, new Vector3(1.5f, 1.3f, 0.5f), new Vector3(0f, 1.5f, 0f));
            AddDefinition(placeables, "Chair", "Chair", HouseBuilderCategories.Furniture,
                "Assets/Main/Features/Interaction/Items/Chairs/Prefabs/PlaceholderChair.prefab", HouseSurfaceType.Ground, HouseSurfaceAlignment.None);
            AddDefinition(placeables, "Closet", "Closet", HouseBuilderCategories.Furniture,
                "Assets/Main/Features/Interaction/Items/Closets/Prefabs/PlaceholderCloset.prefab", HouseSurfaceType.Ground, HouseSurfaceAlignment.None);
            AddDefinition(placeables, "Cupboard", "Cupboard", HouseBuilderCategories.Furniture,
                "Assets/Main/Features/Interaction/Items/Cupboards/Prefabs/PlaceholderCupboard.prefab", HouseSurfaceType.Ground, HouseSurfaceAlignment.None);
            AddDefinition(placeables, "CardboardBox", "Cardboard Box", HouseBuilderCategories.Prop,
                "Assets/Main/Features/Interaction/Items/Boxes/CardboardBox/Prefabs/CardboardBox.prefab", HouseSurfaceType.Ground, HouseSurfaceAlignment.None);
            AddDefinition(placeables, "LightSwitch", "Light Switch", HouseBuilderCategories.Wiring,
                "Assets/Main/Features/Interaction/Items/Lights/LightSwitches/Prefabs/PlaceholderLightSwitch.prefab", HouseSurfaceType.Wall, HouseSurfaceAlignment.ForwardToNormal);
            AddDefinition(placeables, "CeilingLight", "Ceiling Light", HouseBuilderCategories.Wiring,
                "Assets/Main/Features/Interaction/Items/Lights/CeilingLight/Prefabs/PlaceholderCeilingLight.prefab", HouseSurfaceType.Ceiling, HouseSurfaceAlignment.UpToNormal);
            AddDefinition(placeables, "Doorbell", "Button / Doorbell", HouseBuilderCategories.Wiring,
                "Assets/Main/Features/Interaction/Items/Doorbells/Prefabs/PlaceholderDoorbell.prefab", HouseSurfaceType.Wall, HouseSurfaceAlignment.ForwardToNormal);
            AddDefinition(placeables, "BoxingGloveTrap", "Boxing Glove Trap", HouseBuilderCategories.Prop,
                "Assets/Main/Features/Interaction/Items/Traps/BoxingGloveTrap/Prefabs/PlaceholderSpringLoadedBoxingGloveTrap.prefab", HouseSurfaceType.Ground, HouseSurfaceAlignment.None);
            AddDefinition(placeables, "SawBladeTrap", "Saw Blade Trap", HouseBuilderCategories.Prop,
                "Assets/Main/Features/Interaction/Items/Traps/SawBladeTrap/Prefabs/PlaceholderRaySawBladeTrap.prefab", HouseSurfaceType.Ground, HouseSurfaceAlignment.None);
            AddDefinition(placeables, "SearchSpot", "Search Spot", HouseBuilderCategories.SearchSpot,
                "Assets/Main/Features/Neighbor/Prefabs/Search/Prefabs/BaseSearchPoint.prefab", HouseSurfaceType.Ground, HouseSurfaceAlignment.None);
            AddDefinition(placeables, "TaskSpot", "Task Spot", HouseBuilderCategories.TaskSpot,
                "Assets/Main/Features/Neighbor/Prefabs/Task/Prefabs/BaseTask.prefab", HouseSurfaceType.Ground, HouseSurfaceAlignment.None);
            AddDefinition(placeables, "ReinforcementTrigger", "Reinforcement Trigger", HouseBuilderCategories.ReinforcementTrigger,
                "Assets/Main/Features/Neighbor/Prefabs/Reinforcement/Prefabs/BaseReinforcementTrigger.prefab", HouseSurfaceType.Ground, HouseSurfaceAlignment.None);
            AddDefinition(placeables, "SecurityCameraReinforcement", "Security Camera Reinforcement", HouseBuilderCategories.Reinforcement,
                "Assets/Main/Features/Interaction/Items/SecurityCameras/Prefabs/PlaceholderSecurityCamera.prefab", HouseSurfaceType.Wall, HouseSurfaceAlignment.ForwardToNormal);

            GameObject spawnPrefab = CreateMarkerPrefab("NeighborSpawnPoint", typeof(HouseNeighborSpawnPoint));
            AddDefinition(placeables, "NeighborSpawnPoint", "Neighbor Spawn Point", HouseBuilderCategories.NeighborSpawnPoint, spawnPrefab,
                HouseSurfaceType.Ground, HouseSurfaceAlignment.None);

            GameObject patrolPrefab = CreateMarkerPrefab("PatrolPoint", typeof(HousePatrolPoint));
            AddDefinition(placeables, "PatrolPoint", "Patrol Point", HouseBuilderCategories.PatrolPoint, patrolPrefab,
                HouseSurfaceType.Ground, HouseSurfaceAlignment.None);

            ConfigureWirePorts("LightSwitch", new WirePortSetup("state", "State", HouseWirePortDirection.Output, HouseSignalKind.Bool, new Vector3(0f, 0.15f, 0.08f)));
            ConfigureWirePorts("CeilingLight", new WirePortSetup("power", "Power", HouseWirePortDirection.Input, HouseSignalKind.Bool, Vector3.down * 0.1f));
            ConfigureWirePorts("Door", new WirePortSetup("activate", "Activate", HouseWirePortDirection.Input, HouseSignalKind.Pulse, Vector3.up));
            ConfigureWirePorts("LockedDoor", new WirePortSetup("activate", "Activate", HouseWirePortDirection.Input, HouseSignalKind.Pulse, Vector3.up));
            ConfigureWirePorts("Doorbell", new WirePortSetup("pressed", "Pressed", HouseWirePortDirection.Output, HouseSignalKind.Pulse, Vector3.up * 0.15f));
            ConfigureWirePorts("BoxingGloveTrap", new WirePortSetup("trigger", "Trigger", HouseWirePortDirection.Input, HouseSignalKind.Pulse, Vector3.up * 0.25f));
            ConfigureWirePorts("SawBladeTrap", new WirePortSetup("active", "Active", HouseWirePortDirection.Input, HouseSignalKind.Bool, Vector3.up * 0.25f));
            ConfigureWirePorts("ReinforcementTrigger", new WirePortSetup("triggered", "Triggered", HouseWirePortDirection.Output, HouseSignalKind.Pulse, Vector3.up * 0.5f));
            ConfigureWirePorts("SecurityCameraReinforcement", new WirePortSetup("activate", "Activate", HouseWirePortDirection.Input, HouseSignalKind.Pulse, Vector3.up * 0.25f));

            List<HouseMaterialDefinition> materials = new();
            Material defaultMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Main/Objects/ProBuilder/ProBuilderDefault_URP.mat");
            if (defaultMaterial != null)
            {
                materials.Add(CreateMaterial("Prototype", "Prototype", defaultMaterial));
            }

            HouseBuilderCatalog catalog = AssetDatabase.LoadAssetAtPath<HouseBuilderCatalog>(DefaultCatalogPath);
            if (!HasValidScriptReference(catalog))
            {
                AssetDatabase.DeleteAsset(DefaultCatalogPath);
                catalog = null;
            }

            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<HouseBuilderCatalog>();
                AssetDatabase.CreateAsset(catalog, DefaultCatalogPath);
            }

            SerializedObject serializedCatalog = new(catalog);
            SetObjectList(serializedCatalog.FindProperty("categories"), categories);
            SetObjectList(serializedCatalog.FindProperty("placeables"), placeables);
            SetObjectList(serializedCatalog.FindProperty("materials"), materials);
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorPrefs.SetInt(InstallerVersionKey, InstallerVersion);
            Debug.Log($"House Builder default assets are ready at {DefaultCatalogPath}.", catalog);
        }

        private static void EnsureDefaults()
        {
            if (AssetDatabase.LoadAssetAtPath<HouseBuilderCatalog>(DefaultCatalogPath) == null
                || EditorPrefs.GetInt(InstallerVersionKey, 0) < InstallerVersion)
            {
                CreateOrRefreshDefaults();
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Main", "HouseBuilder");
            EnsureFolder(RootPath, "Data");
            EnsureFolder(DataPath, "Categories");
            EnsureFolder(DataPath, "Placeables");
            EnsureFolder(DataPath, "Materials");
            EnsureFolder(RootPath, "Prefabs");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static HouseBuilderCategoryDefinition CreateCategory(string fileName, string id, string displayName, Color color)
        {
            string path = $"{CategoryPath}/{fileName}.asset";
            HouseBuilderCategoryDefinition definition = AssetDatabase.LoadAssetAtPath<HouseBuilderCategoryDefinition>(path);
            if (!HasValidScriptReference(definition))
            {
                AssetDatabase.DeleteAsset(path);
                definition = null;
            }

            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<HouseBuilderCategoryDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            SerializedObject serialized = new(definition);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("color").colorValue = color;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static void AddDefinition(
            ICollection<HousePlaceableDefinition> output,
            string fileName,
            string displayName,
            string categoryId,
            string prefabPath,
            HouseSurfaceType surfaces,
            HouseSurfaceAlignment alignment,
            bool opening = false,
            Vector3 openingSize = default,
            Vector3 openingCenter = default)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                AddDefinition(output, fileName, displayName, categoryId, prefab, surfaces, alignment, opening, openingSize, openingCenter);
            }
        }

        private static void AddDefinition(
            ICollection<HousePlaceableDefinition> output,
            string fileName,
            string displayName,
            string categoryId,
            GameObject prefab,
            HouseSurfaceType surfaces,
            HouseSurfaceAlignment alignment,
            bool opening = false,
            Vector3 openingSize = default,
            Vector3 openingCenter = default)
        {
            string path = $"{DefinitionPath}/{fileName}.asset";
            HousePlaceableDefinition definition = AssetDatabase.LoadAssetAtPath<HousePlaceableDefinition>(path);
            if (!HasValidScriptReference(definition))
            {
                AssetDatabase.DeleteAsset(path);
                definition = null;
            }

            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<HousePlaceableDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            SerializedObject serialized = new(definition);
            serialized.FindProperty("id").stringValue = $"neighbor.{categoryId}.{fileName.ToLowerInvariant()}";
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("categoryId").stringValue = categoryId;
            serialized.FindProperty("prefab").objectReferenceValue = prefab;

            SerializedProperty placement = serialized.FindProperty("placement");
            placement.FindPropertyRelative("allowedSurfaces").intValue = (int)surfaces;
            placement.FindPropertyRelative("surfaceAlignment").enumValueIndex = (int)alignment;
            placement.FindPropertyRelative("requireSurface").boolValue = true;
            placement.FindPropertyRelative("validateCollisions").boolValue = true;
            SetBoundsFromPrefab(placement, prefab);

            SerializedProperty wallOpening = serialized.FindProperty("wallOpening");
            wallOpening.FindPropertyRelative("enabled").boolValue = opening;
            if (opening)
            {
                wallOpening.FindPropertyRelative("size").vector3Value = openingSize;
                wallOpening.FindPropertyRelative("center").vector3Value = openingCenter;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            output.Add(definition);
        }

        private static void SetBoundsFromPrefab(SerializedProperty placement, GameObject prefab)
        {
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            Collider[] colliders = prefab.GetComponentsInChildren<Collider>(true);
            Bounds bounds = new(Vector3.zero, Vector3.one);
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Bounds local = ToLocalBounds(prefab.transform, renderers[i].bounds);
                bounds = hasBounds ? Encapsulate(bounds, local) : local;
                hasBounds = true;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                Bounds local = ToLocalBounds(prefab.transform, colliders[i].bounds);
                bounds = hasBounds ? Encapsulate(bounds, local) : local;
                hasBounds = true;
            }

            placement.FindPropertyRelative("boundsCenter").vector3Value = bounds.center;
            placement.FindPropertyRelative("boundsSize").vector3Value = Vector3.Max(bounds.size, Vector3.one * 0.05f);
        }

        private static Bounds ToLocalBounds(Transform root, Bounds worldBounds)
        {
            Vector3 center = root.InverseTransformPoint(worldBounds.center);
            Vector3 size = root.InverseTransformVector(worldBounds.size);
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            return new Bounds(center, size);
        }

        private static Bounds Encapsulate(Bounds a, Bounds b)
        {
            a.Encapsulate(b.min);
            a.Encapsulate(b.max);
            return a;
        }

        private static GameObject CreateMarkerPrefab(string name, System.Type componentType)
        {
            string path = $"{PrefabPath}/{name}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && existing.GetComponent(componentType) != null)
            {
                return existing;
            }

            if (existing != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            GameObject temporary = new(name);
            temporary.AddComponent(componentType);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporary, path);
            Object.DestroyImmediate(temporary);
            return prefab;
        }

        private static HouseMaterialDefinition CreateMaterial(string fileName, string displayName, Material material)
        {
            string path = $"{MaterialPath}/{fileName}.asset";
            HouseMaterialDefinition definition = AssetDatabase.LoadAssetAtPath<HouseMaterialDefinition>(path);
            if (!HasValidScriptReference(definition))
            {
                AssetDatabase.DeleteAsset(path);
                definition = null;
            }

            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<HouseMaterialDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            SerializedObject serialized = new(definition);
            serialized.FindProperty("id").stringValue = $"neighbor.material.{fileName.ToLowerInvariant()}";
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("material").objectReferenceValue = material;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static void SetObjectList<T>(SerializedProperty property, IList<T> values) where T : Object
        {
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static bool HasValidScriptReference(ScriptableObject asset)
        {
            if (asset == null)
            {
                return false;
            }

            SerializedObject serialized = new(asset);
            SerializedProperty script = serialized.FindProperty("m_Script");
            return script != null && script.objectReferenceValue != null;
        }

        private static void ConfigureWirePorts(string definitionName, params WirePortSetup[] ports)
        {
            HousePlaceableDefinition definition = AssetDatabase.LoadAssetAtPath<HousePlaceableDefinition>($"{DefinitionPath}/{definitionName}.asset");
            if (definition == null)
            {
                return;
            }

            SerializedObject serialized = new(definition);
            SerializedProperty wirePorts = serialized.FindProperty("wirePorts");
            wirePorts.arraySize = ports.Length;
            for (int i = 0; i < ports.Length; i++)
            {
                SerializedProperty port = wirePorts.GetArrayElementAtIndex(i);
                port.FindPropertyRelative("id").stringValue = ports[i].Id;
                port.FindPropertyRelative("displayName").stringValue = ports[i].DisplayName;
                port.FindPropertyRelative("direction").enumValueIndex = (int)ports[i].Direction;
                port.FindPropertyRelative("signalKind").enumValueIndex = (int)ports[i].SignalKind;
                port.FindPropertyRelative("maximumConnections").intValue = 0;
                port.FindPropertyRelative("visualOffset").vector3Value = ports[i].VisualOffset;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private readonly struct WirePortSetup
        {
            public string Id { get; }
            public string DisplayName { get; }
            public HouseWirePortDirection Direction { get; }
            public HouseSignalKind SignalKind { get; }
            public Vector3 VisualOffset { get; }

            public WirePortSetup(string id, string displayName, HouseWirePortDirection direction, HouseSignalKind signalKind, Vector3 visualOffset)
            {
                Id = id;
                DisplayName = displayName;
                Direction = direction;
                SignalKind = signalKind;
                VisualOffset = visualOffset;
            }
        }
    }
}
#endif
