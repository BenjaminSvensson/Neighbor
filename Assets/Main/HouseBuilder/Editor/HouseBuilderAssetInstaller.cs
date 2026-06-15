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
        private const int InstallerVersion = 13;
        private const string InstallerVersionKey = "Neighbor.HouseBuilder.DefaultAssetsVersion";
        private const string CategoryPath = DataPath + "/Categories";
        private const string DefinitionPath = DataPath + "/Placeables";
        private const string MaterialPath = DataPath + "/Materials";
        private const string PrefabPath = RootPath + "/Prefabs";
        private const string StructurePrefabPath = PrefabPath + "/Structures";
        private const string StructureMeshPath = RootPath + "/Meshes/Structures";

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
                CreateCategory("ReinforcementLocations", HouseBuilderCategories.ReinforcementLocation, "Reinforcement Locations", new Color(0.1f, 0.9f, 1f)),
                CreateCategory("NeighborSpawnPoints", HouseBuilderCategories.NeighborSpawnPoint, "Neighbor Spawn Points", new Color(0.75f, 0.2f, 1f)),
                CreateCategory("Wiring", HouseBuilderCategories.Wiring, "Wiring", new Color(1f, 0.85f, 0.1f))
            };

            Material defaultMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Main/Objects/ProBuilder/ProBuilderDefault_URP.mat");
            List<HousePlaceableDefinition> placeables = new();
            Vector3 wallSize = HouseBuilderEditorInteractionUtility.SuggestedSize(HouseGeometryKind.Wall);
            Vector3 floorSize = HouseBuilderEditorInteractionUtility.SuggestedSize(HouseGeometryKind.Floor);
            Vector3 ceilingSize = HouseBuilderEditorInteractionUtility.SuggestedSize(HouseGeometryKind.Ceiling);
            AddDefinition(placeables, "BasicWall", "Basic Wall", HouseBuilderCategories.Wall,
                CreateStructurePrefab("BasicWall", HouseGeometryKind.Wall, wallSize, defaultMaterial), HouseSurfaceType.Ground, HouseSurfaceAlignment.None,
                placementOffset: Vector3.up * wallSize.y * 0.5f, boundsSize: wallSize);
            AddDefinition(placeables, "BasicFloor", "Basic Floor", HouseBuilderCategories.Floor,
                CreateStructurePrefab("BasicFloor", HouseGeometryKind.Floor, floorSize, defaultMaterial), HouseSurfaceType.Ground, HouseSurfaceAlignment.None,
                placementOffset: Vector3.up * floorSize.y * 0.5f, boundsSize: floorSize);
            AddDefinition(placeables, "BasicCeiling", "Basic Ceiling", HouseBuilderCategories.Ceiling,
                CreateStructurePrefab("BasicCeiling", HouseGeometryKind.Ceiling, ceilingSize, defaultMaterial), HouseSurfaceType.Ground | HouseSurfaceType.Ceiling, HouseSurfaceAlignment.None,
                placementOffset: Vector3.up * ceilingSize.y * 0.5f, boundsSize: ceilingSize);
            AddDefinition(placeables, "Door", "Door", HouseBuilderCategories.Door,
                "Assets/Main/Features/Interaction/Items/Doors/Prefabs/Door.prefab", HouseSurfaceType.Ground | HouseSurfaceType.Wall, HouseSurfaceAlignment.ForwardToNormal,
                true, new Vector3(1.3f, 2.25f, 0.6f), new Vector3(0f, 1.125f, 0f), Vector3.up * 0.05f, groundedOnWall: true);
            AddDefinition(placeables, "LockedDoor", "Locked Door", HouseBuilderCategories.Door,
                "Assets/Main/Features/Interaction/Items/Doors/Prefabs/LockedDoor.prefab", HouseSurfaceType.Ground | HouseSurfaceType.Wall, HouseSurfaceAlignment.ForwardToNormal,
                true, new Vector3(1.3f, 2.25f, 0.6f), new Vector3(0f, 1.125f, 0f), Vector3.up * 0.05f, groundedOnWall: true);
            AddDefinition(placeables, "WindowBlinds", "Window", HouseBuilderCategories.Window,
                "Assets/Main/Features/Interaction/Items/Windows/Blinds/Prefabs/PlaceholderWindowBlinds.prefab", HouseSurfaceType.Wall, HouseSurfaceAlignment.ForwardToNormal,
                true, new Vector3(1.5f, 1.3f, 0.5f), new Vector3(0f, 1.5f, 0f));
            AddDefinition(placeables, "Chair", "Chair", HouseBuilderCategories.Furniture,
                "Assets/Main/Features/Interaction/Items/Chairs/Prefabs/PlaceholderChair.prefab", HouseSurfaceType.Any, HouseSurfaceAlignment.None);
            AddDefinition(placeables, "Closet", "Closet", HouseBuilderCategories.Furniture,
                "Assets/Main/Features/Interaction/Items/Closets/Prefabs/PlaceholderCloset.prefab", HouseSurfaceType.Any, HouseSurfaceAlignment.None);
            AddDefinition(placeables, "Cupboard", "Cupboard", HouseBuilderCategories.Furniture,
                "Assets/Main/Features/Interaction/Items/Cupboards/Prefabs/PlaceholderCupboard.prefab", HouseSurfaceType.Any, HouseSurfaceAlignment.None);
            AddDefinition(placeables, "CardboardBox", "Cardboard Box", HouseBuilderCategories.Prop,
                "Assets/Main/Features/Interaction/Items/Boxes/CardboardBox/Prefabs/CardboardBox.prefab", HouseSurfaceType.Any, HouseSurfaceAlignment.None);
            AddDefinition(placeables, "LightSwitch", "Light Switch", HouseBuilderCategories.Wiring,
                "Assets/Main/Features/Interaction/Items/Lights/LightSwitches/Prefabs/PlaceholderLightSwitch.prefab", HouseSurfaceType.Any, HouseSurfaceAlignment.ForwardToNormal);
            AddDefinition(placeables, "CeilingLight", "Ceiling Light", HouseBuilderCategories.Wiring,
                "Assets/Main/Features/Interaction/Items/Lights/CeilingLight/Prefabs/PlaceholderCeilingLight.prefab", HouseSurfaceType.Any, HouseSurfaceAlignment.UpToNormal);
            AddDefinition(placeables, "Doorbell", "Button / Doorbell", HouseBuilderCategories.Wiring,
                "Assets/Main/Features/Interaction/Items/Doorbells/Prefabs/PlaceholderDoorbell.prefab", HouseSurfaceType.Any, HouseSurfaceAlignment.ForwardToNormal);
            AddDefinition(placeables, "BoxingGloveTrap", "Boxing Glove Trap", HouseBuilderCategories.Prop,
                "Assets/Main/Features/Interaction/Items/Traps/BoxingGloveTrap/Prefabs/PlaceholderSpringLoadedBoxingGloveTrap.prefab", HouseSurfaceType.Any, HouseSurfaceAlignment.None);
            AddDefinition(placeables, "SawBladeTrap", "Saw Blade Trap", HouseBuilderCategories.Prop,
                "Assets/Main/Features/Interaction/Items/Traps/SawBladeTrap/Prefabs/PlaceholderRaySawBladeTrap.prefab", HouseSurfaceType.Any, HouseSurfaceAlignment.None);
            AddDefinition(placeables, "SearchSpot", "Search Spot", HouseBuilderCategories.SearchSpot,
                "Assets/Main/Features/Neighbor/Prefabs/Search/Prefabs/BaseSearchPoint.prefab", HouseSurfaceType.Ground, HouseSurfaceAlignment.None);
            AddDefinition(placeables, "TaskSpot", "Task Spot", HouseBuilderCategories.TaskSpot,
                "Assets/Main/Features/Neighbor/Prefabs/Task/Prefabs/BaseTask.prefab", HouseSurfaceType.Ground, HouseSurfaceAlignment.None);
            AddDefinition(placeables, "ReinforcementTrigger", "Reinforcement Trigger", HouseBuilderCategories.ReinforcementTrigger,
                "Assets/Main/Features/Neighbor/Prefabs/Reinforcement/Prefabs/BaseReinforcementTrigger.prefab", HouseSurfaceType.Ground, HouseSurfaceAlignment.None);
            AddDefinition(placeables, "SecurityCameraReinforcement", "Security Camera Reinforcement", HouseBuilderCategories.Reinforcement,
                "Assets/Main/Features/Interaction/Items/SecurityCameras/Prefabs/PlaceholderSecurityCamera.prefab", HouseSurfaceType.Any, HouseSurfaceAlignment.ForwardToNormal);

            AddFlexibleDefinition(placeables, "AlarmClock", "Alarm Clock", HouseBuilderCategories.Prop, "Assets/Main/Features/Interaction/Items/AlarmClocks/Prefabs/PlaceholderAlarmClock.prefab");
            AddFlexibleDefinition(placeables, "Beartrap", "Bear Trap", HouseBuilderCategories.Prop, "Assets/Main/Features/Interaction/Items/Beartraps/Prefabs/BeartrapPlaceholder.prefab");
            AddFlexibleDefinition(placeables, "Bed", "Bed", HouseBuilderCategories.Furniture, "Assets/Main/Features/Interaction/Items/Beds/Prefabs/PlaceholderHideableBed.prefab");
            AddFlexibleDefinition(placeables, "WoodBoard", "Wood Board", HouseBuilderCategories.Prop, "Assets/Main/Features/Interaction/Items/Boards/Prefabs/PlaceholderWoodBoard.prefab");
            AddFlexibleDefinition(placeables, "Notebook", "Notebook", HouseBuilderCategories.Prop, "Assets/Main/Features/Interaction/Items/Books/Prefabs/PlaceholderNotebook.prefab");
            AddFlexibleDefinition(placeables, "ReadableBook", "Readable Book", HouseBuilderCategories.Prop, "Assets/Main/Features/Interaction/Items/Books/Prefabs/PlaceholderReadableBook.prefab");
            AddFlexibleDefinition(placeables, "PhotoCamera", "Photo Camera", HouseBuilderCategories.Prop, "Assets/Main/Features/Interaction/Items/Cameras/Prefabs/PlaceholderPhotoCamera.prefab");
            AddFlexibleDefinition(placeables, "Curtains", "Curtains", HouseBuilderCategories.Furniture, "Assets/Main/Features/Interaction/Items/Curtains/Prefabs/PlaceholderHideableCurtains.prefab", HouseSurfaceAlignment.ForwardToNormal);
            AddFlexibleDefinition(placeables, "Flashlight", "Flashlight", HouseBuilderCategories.Prop, "Assets/Main/Features/Interaction/Items/Flashlights/Prefabs/PlaceholderFlashlight.prefab");
            AddFlexibleDefinition(placeables, "Tomato", "Tomato", HouseBuilderCategories.Prop, "Assets/Main/Features/Interaction/Items/Food/Tomato/Prefabs/PlaceholderTomato.prefab");
            AddDefinition(placeables, "Glass", "Glass", HouseBuilderCategories.Prop,
                "Assets/Main/Features/Interaction/Items/Glass/Prefabs/PlaceholderGlass.prefab", HouseSurfaceType.Any, HouseSurfaceAlignment.RightToNormal,
                true, new Vector3(0.85f, 1.15f, 0.5f), Vector3.zero, Vector3.up * 0.575f, deriveOpeningFromVisualBounds: false);
            AddFlexibleDefinition(placeables, "Key", "Key", HouseBuilderCategories.Prop, "Assets/Main/Features/Interaction/Items/Keys/Prefabs/TestKey.prefab");
            AddFlexibleDefinition(placeables, "Mirror", "Mirror", HouseBuilderCategories.Furniture, "Assets/Main/Features/Interaction/Items/Mirrors/Prefabs/PlaceholderMirror.prefab", HouseSurfaceAlignment.ForwardToNormal, Vector3.up * 0.87f);
            AddFlexibleDefinition(placeables, "RemoteControl", "Remote Control", HouseBuilderCategories.Prop, "Assets/Main/Features/Interaction/Items/RemoteControls/Prefabs/PlaceholderRemoteControl.prefab");
            AddFlexibleDefinition(placeables, "LaserGrid", "Laser Grid", HouseBuilderCategories.Wiring, "Assets/Main/Features/Interaction/Items/Security/LaserGrid/Prefabs/PlaceholderLaserGrid.prefab", HouseSurfaceAlignment.ForwardToNormal, Vector3.up * 0.14499998f);
            AddFlexibleDefinition(placeables, "Basketball", "Basketball", HouseBuilderCategories.Prop, "Assets/Main/Features/Interaction/Items/Sports/Basketball/Prefabs/PlaceholderBasketball.prefab");
            AddFlexibleDefinition(placeables, "SprayCan", "Spray Can", HouseBuilderCategories.Prop, "Assets/Main/Features/Interaction/Items/SprayCans/Prefabs/PlaceholderSprayCan.prefab");
            AddFlexibleDefinition(placeables, "SwingingAxe", "Swinging Axe", HouseBuilderCategories.Prop, "Assets/Main/Features/Interaction/Items/SwingingAxes/Prefabs/HugeSwingingAxePlaceholder.prefab");
            AddFlexibleDefinition(placeables, "Crowbar", "Crowbar", HouseBuilderCategories.Prop, "Assets/Main/Features/Interaction/Items/Tools/Crowbar/Prefabs/PlaceholderCrowbar.prefab");
            AddFlexibleDefinition(placeables, "Plunger", "Plunger", HouseBuilderCategories.Prop, "Assets/Main/Features/Interaction/Items/Tools/Plunger/Prefabs/PlaceholderPlunger.prefab");
            AddFlexibleDefinition(placeables, "Screwdriver", "Screwdriver", HouseBuilderCategories.Prop, "Assets/Main/Features/Interaction/Items/Tools/Screwdriver/Prefabs/PlaceholderScrewdriver.prefab");
            AddFlexibleDefinition(placeables, "WindUpToy", "Wind-up Toy", HouseBuilderCategories.Prop, "Assets/Main/Features/Interaction/Items/Toys/WindUpToy/Prefabs/PlaceholderWindUpToy.prefab");
            AddFlexibleDefinition(placeables, "TrapDoor", "Trap Door", HouseBuilderCategories.Wiring, "Assets/Main/Features/Interaction/Items/TrapDoors/Prefabs/FakeFloorTrapDoorPlaceholder.prefab");
            AddFlexibleDefinition(placeables, "TV", "TV", HouseBuilderCategories.Wiring, "Assets/Main/Features/Interaction/Items/TVs/Prefabs/PlaceholderTV.prefab", HouseSurfaceAlignment.ForwardToNormal, Vector3.up * 0.66f);
            AddFlexibleDefinition(placeables, "Umbrella", "Umbrella", HouseBuilderCategories.Prop, "Assets/Main/Features/Interaction/Items/Umbrellas/Prefabs/PlaceholderUmbrella.prefab");
            AddFlexibleDefinition(placeables, "VentCover", "Vent Cover", HouseBuilderCategories.Prop, "Assets/Main/Features/Interaction/Items/Vents/Prefabs/PlaceholderVentCover.prefab", HouseSurfaceAlignment.ForwardToNormal, Vector3.up * 0.425f);

            GameObject spawnPrefab = CreateMarkerPrefab("NeighborSpawnPoint", typeof(HouseNeighborSpawnPoint));
            AddDefinition(placeables, "NeighborSpawnPoint", "Neighbor Spawn Point", HouseBuilderCategories.NeighborSpawnPoint, spawnPrefab,
                HouseSurfaceType.Ground, HouseSurfaceAlignment.None);

            GameObject patrolPrefab = CreateMarkerPrefab("PatrolPoint", typeof(HousePatrolPoint));
            AddDefinition(placeables, "PatrolPoint", "Patrol Point", HouseBuilderCategories.PatrolPoint, patrolPrefab,
                HouseSurfaceType.Ground, HouseSurfaceAlignment.None);

            GameObject reinforcementLocationPrefab = CreateMarkerPrefab("ReinforcementLocation", typeof(HouseReinforcementLocation));
            AddDefinition(placeables, "ReinforcementLocation", "Reinforcement Location", HouseBuilderCategories.ReinforcementLocation, reinforcementLocationPrefab,
                HouseSurfaceType.Ground, HouseSurfaceAlignment.None, boundsSize: Vector3.one * 0.25f, hideFromCatalog: true);

            ConfigureWirePorts("LightSwitch", new WirePortSetup("state", "State", HouseWirePortDirection.Output, HouseSignalKind.Bool, new Vector3(0f, 0.15f, 0.08f)));
            ConfigureWirePorts("CeilingLight", new WirePortSetup("power", "Power", HouseWirePortDirection.Input, HouseSignalKind.Bool, Vector3.down * 0.1f));
            ConfigureWirePorts("Door", new WirePortSetup("activate", "Activate", HouseWirePortDirection.Input, HouseSignalKind.Pulse, Vector3.up));
            ConfigureWirePorts("LockedDoor", new WirePortSetup("activate", "Activate", HouseWirePortDirection.Input, HouseSignalKind.Pulse, Vector3.up));
            ConfigureWirePorts("Doorbell", new WirePortSetup("pressed", "Pressed", HouseWirePortDirection.Output, HouseSignalKind.Pulse, Vector3.up * 0.15f));
            ConfigureWirePorts("BoxingGloveTrap", new WirePortSetup("trigger", "Trigger", HouseWirePortDirection.Input, HouseSignalKind.Pulse, Vector3.up * 0.25f));
            ConfigureWirePorts("SawBladeTrap", new WirePortSetup("active", "Active", HouseWirePortDirection.Input, HouseSignalKind.Bool, Vector3.up * 0.25f));
            ConfigureWirePorts("ReinforcementTrigger", new WirePortSetup("triggered", "Triggered", HouseWirePortDirection.Output, HouseSignalKind.Pulse, Vector3.up * 0.5f));
            ConfigureWirePorts("SecurityCameraReinforcement", new WirePortSetup("activate", "Activate", HouseWirePortDirection.Input, HouseSignalKind.Pulse, Vector3.up * 0.25f));
            ConfigureWirePorts("Beartrap", new WirePortSetup("trigger", "Trigger", HouseWirePortDirection.Input, HouseSignalKind.Pulse, Vector3.up * 0.15f));
            ConfigureWirePorts("LaserGrid", new WirePortSetup("active", "Active", HouseWirePortDirection.Input, HouseSignalKind.Bool, Vector3.up * 0.5f));
            ConfigureWirePorts("SwingingAxe", new WirePortSetup("trigger", "Trigger", HouseWirePortDirection.Input, HouseSignalKind.Pulse, Vector3.up));
            ConfigureWirePorts("TrapDoor", new WirePortSetup("trigger", "Trigger", HouseWirePortDirection.Input, HouseSignalKind.Pulse, Vector3.up * 0.1f));
            ConfigureWirePorts("TV", new WirePortSetup("power", "Power", HouseWirePortDirection.Input, HouseSignalKind.Bool, Vector3.up * 0.5f));

            List<HouseMaterialDefinition> materials = new();
            if (defaultMaterial != null)
            {
                materials.Add(CreateMaterial("Prototype", "Prototype", defaultMaterial));
            }

            AddMaterialIfPresent(materials, "Brick", "Brick", "Assets/Main/Art/Materials/AmbientCG/Bricks104/Bricks104.mat");
            AddMaterialIfPresent(materials, "WoodPlanks", "Wood Planks", "Assets/Main/Art/Materials/AmbientCG/Planks023B/Planks023B.mat");
            AddMaterialIfPresent(materials, "Tiles", "Tiles", "Assets/Main/Art/Materials/AmbientCG/Tiles133A/Tiles133A.mat");
            AddMaterialIfPresent(materials, "Marble", "Marble", "Assets/Main/Art/Materials/AmbientCG/Marble020/Marble020.mat");
            AddMaterialIfPresent(materials, "Wallpaper", "Wallpaper", "Assets/Main/Art/Materials/AmbientCG/Paper005/Paper005.mat");
            AddMaterialIfPresent(materials, "Ground", "Ground", "Assets/Main/Art/Materials/AmbientCG/Ground048/Ground048.mat");

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
            else
            {
                for (int i = 0; i < catalog.Materials.Count; i++)
                {
                    HouseMaterialDefinition existingMaterial = catalog.Materials[i];
                    if (existingMaterial != null && !materials.Contains(existingMaterial))
                    {
                        materials.Add(existingMaterial);
                    }
                }
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
            EnsureFolder(PrefabPath, "Structures");
            EnsureFolder(RootPath, "Meshes");
            EnsureFolder(RootPath + "/Meshes", "Structures");
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
            Vector3 openingCenter = default,
            Vector3 placementOffset = default,
            Vector3 boundsSize = default,
            bool hideFromCatalog = false,
            bool groundedOnWall = false,
            bool deriveOpeningFromVisualBounds = true)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                AddDefinition(output, fileName, displayName, categoryId, prefab, surfaces, alignment, opening, openingSize, openingCenter, placementOffset, boundsSize, hideFromCatalog, groundedOnWall, deriveOpeningFromVisualBounds);
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
            Vector3 openingCenter = default,
            Vector3 placementOffset = default,
            Vector3 boundsSize = default,
            bool hideFromCatalog = false,
            bool groundedOnWall = false,
            bool deriveOpeningFromVisualBounds = true)
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
            serialized.FindProperty("hideFromCatalog").boolValue = hideFromCatalog;

            SerializedProperty placement = serialized.FindProperty("placement");
            placement.FindPropertyRelative("allowedSurfaces").intValue = (int)surfaces;
            placement.FindPropertyRelative("surfaceAlignment").enumValueIndex = (int)alignment;
            placement.FindPropertyRelative("requireSurface").boolValue =
                opening
                || alignment != HouseSurfaceAlignment.None
                || (surfaces & HouseSurfaceType.Ground) == 0;
            placement.FindPropertyRelative("groundOnWall").boolValue = groundedOnWall;
            bool structural = categoryId is HouseBuilderCategories.Structure
                or HouseBuilderCategories.Wall
                or HouseBuilderCategories.Floor
                or HouseBuilderCategories.Ceiling
                or HouseBuilderCategories.Door
                or HouseBuilderCategories.Window;
            placement.FindPropertyRelative("validateCollisions").boolValue = structural;
            SetBoundsFromPrefab(placement, prefab, out Bounds placementBounds, out bool hasVisualBounds);
            if (boundsSize.sqrMagnitude > 0f)
            {
                placement.FindPropertyRelative("boundsSize").vector3Value = boundsSize;
                placement.FindPropertyRelative("boundsCenter").vector3Value = Vector3.zero;
            }

            if (placementOffset.sqrMagnitude <= Mathf.Epsilon
                && hasVisualBounds
                && alignment == HouseSurfaceAlignment.None
                && (surfaces & HouseSurfaceType.Ground) != 0)
            {
                placementOffset = Vector3.up * Mathf.Max(0f, -placementBounds.min.y);
            }

            placement.FindPropertyRelative("placementOffset").vector3Value = placementOffset;

            SerializedProperty wallOpening = serialized.FindProperty("wallOpening");
            wallOpening.FindPropertyRelative("enabled").boolValue = opening;
            if (opening)
            {
                if (hasVisualBounds && deriveOpeningFromVisualBounds)
                {
                    openingSize.x = placementBounds.size.x;
                    openingSize.y = placementBounds.size.y;
                    openingCenter.x = placementBounds.center.x;
                    openingCenter.y = placementBounds.center.y;
                }

                wallOpening.FindPropertyRelative("size").vector3Value = openingSize;
                wallOpening.FindPropertyRelative("center").vector3Value = openingCenter;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            output.Add(definition);
        }

        private static void AddFlexibleDefinition(
            ICollection<HousePlaceableDefinition> output,
            string fileName,
            string displayName,
            string categoryId,
            string prefabPath,
            HouseSurfaceAlignment alignment = HouseSurfaceAlignment.None,
            Vector3 placementOffset = default)
        {
            AddDefinition(output, fileName, displayName, categoryId, prefabPath, HouseSurfaceType.Any, alignment, placementOffset: placementOffset);
        }

        private static void SetBoundsFromPrefab(
            SerializedProperty placement,
            GameObject prefab,
            out Bounds placementBounds,
            out bool hasVisualBounds)
        {
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            Collider[] colliders = prefab.GetComponentsInChildren<Collider>(true);
            Bounds bounds = new(Vector3.zero, Vector3.one);
            bool hasBounds = false;
            hasVisualBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Bounds local = ToLocalBounds(prefab.transform, renderers[i].bounds);
                bounds = hasBounds ? Encapsulate(bounds, local) : local;
                hasBounds = true;
                hasVisualBounds = true;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                Bounds local = ToLocalBounds(prefab.transform, colliders[i].bounds);
                bounds = hasBounds ? Encapsulate(bounds, local) : local;
                hasBounds = true;
            }

            placementBounds = bounds;
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

        private static GameObject CreateStructurePrefab(string name, HouseGeometryKind kind, Vector3 size, Material material)
        {
            string path = $"{StructurePrefabPath}/{name}.prefab";
            Mesh persistentMesh = CreateOrUpdateStructureMesh(name, kind, size);
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            HouseGeometryObject existingGeometry = existing != null ? existing.GetComponent<HouseGeometryObject>() : null;
            if (existingGeometry == null || existingGeometry.Descriptor.Kind != kind || existingGeometry.Descriptor.Size != size)
            {
                if (existing != null)
                {
                    AssetDatabase.DeleteAsset(path);
                }

                GameObject temporary = HouseGeometryFactory.Create(new HouseGeometryDescriptor(kind, size), material);
                temporary.name = name;
                PrefabUtility.SaveAsPrefabAsset(temporary, path);
                Object.DestroyImmediate(temporary);
            }

            GameObject prefabContents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                MeshFilter filter = prefabContents.GetComponent<MeshFilter>();
                MeshCollider collider = prefabContents.GetComponent<MeshCollider>();
                filter.sharedMesh = persistentMesh;
                collider.sharedMesh = persistentMesh;
                prefabContents.GetComponent<HouseGeometryObject>().PreparePhysicalObject();
                PrefabUtility.SaveAsPrefabAsset(prefabContents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static Mesh CreateOrUpdateStructureMesh(string name, HouseGeometryKind kind, Vector3 size)
        {
            string path = $"{StructureMeshPath}/{name}.asset";
            Mesh generated = HouseGeometryFactory.BuildMesh(new HouseGeometryDescriptor(kind, size));
            generated.name = name;

            Mesh persistent = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (persistent == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }

            EditorUtility.CopySerialized(generated, persistent);
            persistent.name = name;
            EditorUtility.SetDirty(persistent);
            Object.DestroyImmediate(generated);
            return persistent;
        }

        private static void AddMaterialIfPresent(ICollection<HouseMaterialDefinition> output, string fileName, string displayName, string materialPath)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null)
            {
                output.Add(CreateMaterial(fileName, displayName, material));
            }
        }

        public static HouseMaterialDefinition EnsureMaterialDefinition(Material material, HouseBuilderCatalog targetCatalog)
        {
            if (material == null || targetCatalog == null)
            {
                return null;
            }

            for (int i = 0; i < targetCatalog.Materials.Count; i++)
            {
                HouseMaterialDefinition existing = targetCatalog.Materials[i];
                if (existing != null && existing.Material == material)
                {
                    return existing;
                }
            }

            EnsureFolders();
            string materialAssetPath = AssetDatabase.GetAssetPath(material);
            string guid = AssetDatabase.AssetPathToGUID(materialAssetPath);
            if (string.IsNullOrWhiteSpace(guid))
            {
                return null;
            }

            string path = $"{MaterialPath}/Imported_{guid}.asset";
            HouseMaterialDefinition definition = AssetDatabase.LoadAssetAtPath<HouseMaterialDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<HouseMaterialDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            SerializedObject serializedDefinition = new(definition);
            serializedDefinition.FindProperty("id").stringValue = $"neighbor.material.imported.{guid}";
            serializedDefinition.FindProperty("displayName").stringValue = material.name;
            serializedDefinition.FindProperty("material").objectReferenceValue = material;
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedCatalog = new(targetCatalog);
            SerializedProperty catalogMaterials = serializedCatalog.FindProperty("materials");
            int index = catalogMaterials.arraySize;
            catalogMaterials.InsertArrayElementAtIndex(index);
            catalogMaterials.GetArrayElementAtIndex(index).objectReferenceValue = definition;
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            EditorUtility.SetDirty(targetCatalog);
            AssetDatabase.SaveAssets();
            return definition;
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
