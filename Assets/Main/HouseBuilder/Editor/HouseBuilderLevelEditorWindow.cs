#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Neighbor.Main.HouseBuilder.Editor
{
    public sealed class HouseBuilderLevelEditorWindow : EditorWindow
    {
        private static readonly string[] Tabs = { "Build", "Geometry", "Materials", "Wiring", "Boolean", "Save" };

        [SerializeField] private HouseBuilderPlacementSettings placementSettings = new();
        [SerializeField] private HouseBuilderCatalog catalog;
        [SerializeField] private HouseBuilderWorld world;
        [SerializeField] private HouseGeometryKind geometryKind = HouseGeometryKind.Wall;
        [SerializeField] private Vector3 geometrySize = new(4f, 3f, 0.25f);
        [SerializeField] private float openingWidth = 1.2f;
        [SerializeField] private float openingHeight = 2.1f;
        [SerializeField] private float sillHeight = 0.9f;
        [SerializeField] private int stairCount = 8;

        private int activeTab;
        private Vector2 scroll;
        private string search = string.Empty;
        private string categoryFilter = string.Empty;
        private HousePlaceableDefinition placingDefinition;
        private GameObject ghostObject;
        private HouseBuilderGhost ghost;
        private Quaternion requestedRotation = Quaternion.identity;
        private HousePlacementResult lastPlacement;
        private HousePlacementValidationResult lastValidation;
        private Collider lastSurfaceCollider;
        private HouseFaceRole materialFaceRole = HouseFaceRole.Default;
        private GameObject booleanLeft;
        private GameObject booleanRight;
        private HouseWireEndpoint selectedOutputEndpoint;
        private HouseWirePortDefinition selectedOutputPort;
        private string statusMessage = "Ready";

        [MenuItem("Tools/Neighbor/House Builder/Level Editor")]
        public static void Open()
        {
            GetWindow<HouseBuilderLevelEditorWindow>("House Builder");
        }

        private void OnEnable()
        {
            if (catalog == null)
            {
                catalog = AssetDatabase.LoadAssetAtPath<HouseBuilderCatalog>(HouseBuilderAssetInstaller.DefaultCatalogPath);
            }

            ResolveWorld();
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            DestroyGhost();
        }

        private void OnGUI()
        {
            DrawHeader();
            activeTab = GUILayout.Toolbar(activeTab, Tabs);
            EditorGUILayout.Space(4f);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            switch (activeTab)
            {
                case 0:
                    DrawBuildTab();
                    break;
                case 1:
                    DrawGeometryTab();
                    break;
                case 2:
                    DrawMaterialsTab();
                    break;
                case 3:
                    DrawWiringTab();
                    break;
                case 4:
                    DrawBooleanTab();
                    break;
                case 5:
                    DrawSaveTab();
                    break;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("House Builder Level Editor", EditorStyles.boldLabel);
            catalog = (HouseBuilderCatalog)EditorGUILayout.ObjectField("Catalog", catalog, typeof(HouseBuilderCatalog), false);
            world = (HouseBuilderWorld)EditorGUILayout.ObjectField("World", world, typeof(HouseBuilderWorld), true);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create World"))
                {
                    CreateWorld();
                }

                if (GUILayout.Button("Refresh Defaults"))
                {
                    HouseBuilderAssetInstaller.CreateOrRefreshDefaults();
                    catalog = AssetDatabase.LoadAssetAtPath<HouseBuilderCatalog>(HouseBuilderAssetInstaller.DefaultCatalogPath);
                }

                if (GUILayout.Button("Select World") && world != null)
                {
                    Selection.activeObject = world.gameObject;
                }
            }

            SerializedObject serializedWindow = new(this);
            SerializedProperty settings = serializedWindow.FindProperty("placementSettings");
            EditorGUILayout.PropertyField(settings, new GUIContent("Snapping & Validation"), true);
            serializedWindow.ApplyModifiedProperties();
        }

        private void DrawBuildTab()
        {
            if (catalog == null)
            {
                EditorGUILayout.HelpBox("Assign or create a House Builder catalog.", MessageType.Warning);
                return;
            }

            search = EditorGUILayout.TextField("Search", search);
            DrawCategoryFilters();
            EditorGUILayout.Space(4f);

            IEnumerable<HousePlaceableDefinition> definitions = catalog.Placeables.Where(definition =>
                definition != null
                && (string.IsNullOrEmpty(categoryFilter) || definition.CategoryId == categoryFilter)
                && (string.IsNullOrWhiteSpace(search)
                    || definition.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                    || definition.CategoryId.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                    || definition.Tags.Any(tag => tag.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)));

            foreach (HousePlaceableDefinition definition in definitions)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label(definition.Preview != null ? definition.Preview : AssetPreview.GetAssetPreview(definition.Prefab), GUILayout.Width(42f), GUILayout.Height(42f));
                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField(definition.DisplayName, EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(definition.CategoryId, EditorStyles.miniLabel);
                    }

                    if (GUILayout.Button(placingDefinition == definition ? "Placing" : "Place", GUILayout.Width(68f), GUILayout.Height(36f)))
                    {
                        StartPlacement(definition);
                    }
                }
            }

            if (placingDefinition != null && GUILayout.Button("Cancel Placement"))
            {
                CancelPlacement();
            }
        }

        private void DrawCategoryFilters()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Toggle(string.IsNullOrEmpty(categoryFilter), "All", EditorStyles.miniButton, GUILayout.Width(42f)))
                {
                    categoryFilter = string.Empty;
                }

                foreach (HouseBuilderCategoryDefinition category in catalog.Categories)
                {
                    if (category == null)
                    {
                        continue;
                    }

                    GUI.backgroundColor = categoryFilter == category.Id ? category.Color : Color.white;
                    if (GUILayout.Toggle(categoryFilter == category.Id, category.DisplayName, EditorStyles.miniButton))
                    {
                        categoryFilter = category.Id;
                    }
                }

                GUI.backgroundColor = Color.white;
            }
        }

        private void DrawGeometryTab()
        {
            geometryKind = (HouseGeometryKind)EditorGUILayout.EnumPopup("Primitive", geometryKind);
            geometrySize = EditorGUILayout.Vector3Field("Size", geometrySize);
            if (geometryKind is HouseGeometryKind.Doorway or HouseGeometryKind.Window)
            {
                openingWidth = EditorGUILayout.FloatField("Opening Width", openingWidth);
                openingHeight = EditorGUILayout.FloatField("Opening Height", openingHeight);
            }

            if (geometryKind == HouseGeometryKind.Window)
            {
                sillHeight = EditorGUILayout.FloatField("Sill Height", sillHeight);
            }

            if (geometryKind == HouseGeometryKind.Stairs)
            {
                stairCount = EditorGUILayout.IntSlider("Stair Count", stairCount, 1, 32);
            }

            EditorGUILayout.HelpBox(
                "Walls are parametric and can carry multiple linked door/window openings. Doorway and Window primitives create standalone framed blocks.",
                MessageType.None);
            if (GUILayout.Button("Create Geometry", GUILayout.Height(30f)))
            {
                CreateGeometry();
            }
        }

        private void DrawMaterialsTab()
        {
            HouseBuilderObject selected = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<HouseBuilderObject>()
                : null;
            if (selected == null)
            {
                EditorGUILayout.HelpBox("Select a builder object to assign materials.", MessageType.Info);
                return;
            }

            EditorGUILayout.ObjectField("Selected", selected, typeof(HouseBuilderObject), true);
            materialFaceRole = (HouseFaceRole)EditorGUILayout.EnumPopup("Face / Side", materialFaceRole);
            if (catalog == null || catalog.Materials.Count == 0)
            {
                EditorGUILayout.HelpBox("The catalog has no material definitions.", MessageType.Warning);
                return;
            }

            for (int i = 0; i < catalog.Materials.Count; i++)
            {
                HouseMaterialDefinition material = catalog.Materials[i];
                if (material != null && GUILayout.Button(material.DisplayName))
                {
                    AssignMaterial(selected, material);
                }
            }

            HouseBuilderMaterialController controller = selected.GetComponent<HouseBuilderMaterialController>();
            if (controller != null)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Saved Bindings", EditorStyles.boldLabel);
                foreach (HouseMaterialBinding binding in controller.Bindings)
                {
                    EditorGUILayout.LabelField($"{binding.FaceRole}: {binding.MaterialId}", EditorStyles.miniLabel);
                }
            }
        }

        private void DrawWiringTab()
        {
            if (world == null)
            {
                EditorGUILayout.HelpBox("Create or assign a builder world first.", MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox(
                "In the Scene view, click an orange output port and then a cyan input port. Ports and signal events are generic and support multiple connections.",
                MessageType.None);
            if (selectedOutputEndpoint != null && selectedOutputPort != null)
            {
                EditorGUILayout.LabelField($"Output selected: {selectedOutputEndpoint.name} / {selectedOutputPort.DisplayName}", EditorStyles.boldLabel);
                if (GUILayout.Button("Clear Output Selection"))
                {
                    selectedOutputEndpoint = null;
                    selectedOutputPort = null;
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField($"Connections ({world.WireGraph.Connections.Count})", EditorStyles.boldLabel);
            for (int i = world.WireGraph.Connections.Count - 1; i >= 0; i--)
            {
                HouseWireConnection connection = world.WireGraph.Connections[i];
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"{ShortId(connection.OutputObjectId)}:{ShortId(connection.OutputPortId)} -> {ShortId(connection.InputObjectId)}:{ShortId(connection.InputPortId)}");
                    if (GUILayout.Button("Remove", GUILayout.Width(64f)))
                    {
                        Undo.RecordObject(world.WireGraph, "Remove House Wire");
                        world.WireGraph.RemoveConnection(connection.Id);
                        EditorUtility.SetDirty(world.WireGraph);
                    }
                }
            }
        }

        private void DrawBooleanTab()
        {
            EditorGUILayout.HelpBox(
                "Boolean operations use ProBuilder's CSG implementation and bake the result into a serializable builder mesh. Inputs remain unchanged.",
                MessageType.None);
            booleanLeft = (GameObject)EditorGUILayout.ObjectField("Base (A)", booleanLeft, typeof(GameObject), true);
            booleanRight = (GameObject)EditorGUILayout.ObjectField("Operand (B)", booleanRight, typeof(GameObject), true);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("A - B"))
                {
                    PerformBoolean(HouseBooleanOperation.Subtract);
                }

                if (GUILayout.Button("Intersect"))
                {
                    PerformBoolean(HouseBooleanOperation.Intersect);
                }

                if (GUILayout.Button("Union"))
                {
                    PerformBoolean(HouseBooleanOperation.Union);
                }
            }
        }

        private void DrawSaveTab()
        {
            if (world == null)
            {
                EditorGUILayout.HelpBox("Create or assign a builder world first.", MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox(
                $"Format: {HouseBuilderSaveSystem.FormatId}, version {HouseBuilderSaveSystem.CurrentVersion}. JSON includes structures, geometry, materials, props, AI nodes, triggers, reinforcements, custom component state, and wires.",
                MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save JSON", GUILayout.Height(30f)))
                {
                    SaveWorld();
                }

                if (GUILayout.Button("Load JSON", GUILayout.Height(30f)))
                {
                    LoadWorld();
                }
            }

            if (GUILayout.Button("Clear Builder World"))
            {
                if (EditorUtility.DisplayDialog("Clear Builder World", "Delete all builder objects and connections in this world?", "Clear", "Cancel"))
                {
                    Undo.RegisterFullObjectHierarchyUndo(world.gameObject, "Clear Builder World");
                    world.Clear();
                    MarkSceneDirty();
                }
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            DrawWires();
            if (activeTab == 3)
            {
                DrawWirePorts();
            }

            if (placingDefinition == null || ghostObject == null)
            {
                return;
            }

            Event current = Event.current;
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape
                || current.type == EventType.MouseDown && current.button == 1)
            {
                CancelPlacement();
                current.Use();
                return;
            }

            if (current.type == EventType.KeyDown && (current.keyCode == KeyCode.Q || current.keyCode == KeyCode.E))
            {
                float direction = current.keyCode == KeyCode.Q ? -1f : 1f;
                requestedRotation *= Quaternion.Euler(0f, placementSettings.RotationStep * direction, 0f);
                current.Use();
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            bool hasSurface = TryRaycastSurface(ray, out RaycastHit hit);
            Vector3 fallback = GetFallbackPosition(ray);
            lastPlacement = HouseBuilderSnapUtility.Calculate(
                fallback,
                requestedRotation,
                hasSurface,
                hit,
                placingDefinition.Placement,
                placementSettings,
                GatherNearbyBounds());
            lastSurfaceCollider = hasSurface ? hit.collider : null;
            lastValidation = HouseBuilderPlacementValidator.Validate(
                lastPlacement.Position,
                lastPlacement.Rotation,
                placingDefinition.Placement,
                placementSettings,
                lastPlacement,
                lastSurfaceCollider != null ? lastSurfaceCollider.transform : null);

            ghostObject.transform.SetPositionAndRotation(lastPlacement.Position, lastPlacement.Rotation);
            ghost.SetValid(lastValidation.IsValid);
            Handles.color = lastValidation.IsValid ? Color.green : Color.red;
            Handles.Label(lastPlacement.Position + Vector3.up * 0.4f, $"{lastPlacement.SnapKind}: {lastValidation.Message}");

            if (current.type == EventType.MouseDown && current.button == 0 && !current.alt)
            {
                if (lastValidation.IsValid)
                {
                    PlaceCurrent();
                }
                else
                {
                    statusMessage = lastValidation.Message;
                }

                current.Use();
            }

            sceneView.Repaint();
        }

        private void DrawWires()
        {
            if (world == null)
            {
                return;
            }

            foreach (HouseWireConnection connection in world.WireGraph.Connections)
            {
                if (!world.WireGraph.TryResolve(connection, out HouseWireEndpoint output, out HouseWirePortDefinition outputPort, out HouseWireEndpoint input, out HouseWirePortDefinition inputPort))
                {
                    continue;
                }

                Vector3 start = output.GetPortWorldPosition(outputPort);
                Vector3 end = input.GetPortWorldPosition(inputPort);
                float tangent = Mathf.Max(0.5f, Vector3.Distance(start, end) * 0.35f);
                Handles.DrawBezier(start, end, start + output.transform.forward * tangent, end - input.transform.forward * tangent, new Color(1f, 0.75f, 0.1f, 0.9f), null, 3f);
            }
        }

        private void DrawWirePorts()
        {
            if (world == null)
            {
                return;
            }

            HouseWireEndpoint[] endpoints = world.GetComponentsInChildren<HouseWireEndpoint>(true);
            for (int endpointIndex = 0; endpointIndex < endpoints.Length; endpointIndex++)
            {
                HouseWireEndpoint endpoint = endpoints[endpointIndex];
                for (int portIndex = 0; portIndex < endpoint.Ports.Count; portIndex++)
                {
                    HouseWirePortDefinition port = endpoint.Ports[portIndex];
                    if (port == null)
                    {
                        continue;
                    }

                    Vector3 position = endpoint.GetPortWorldPosition(port);
                    Handles.color = port.Direction == HouseWirePortDirection.Output ? new Color(1f, 0.55f, 0.05f) : new Color(0.05f, 0.9f, 1f);
                    float size = HandleUtility.GetHandleSize(position) * 0.08f;
                    if (Handles.Button(position, Quaternion.identity, size, size * 1.2f, Handles.SphereHandleCap))
                    {
                        HandlePortClick(endpoint, port);
                    }

                    Handles.Label(position + Vector3.up * size, port.DisplayName);
                }
            }
        }

        private void HandlePortClick(HouseWireEndpoint endpoint, HouseWirePortDefinition port)
        {
            if (port.Direction == HouseWirePortDirection.Output)
            {
                selectedOutputEndpoint = endpoint;
                selectedOutputPort = port;
                statusMessage = $"Selected output {endpoint.name}/{port.DisplayName}.";
                Repaint();
                return;
            }

            if (selectedOutputEndpoint == null || selectedOutputPort == null)
            {
                statusMessage = "Select an output port first.";
                return;
            }

            Undo.RecordObject(world.WireGraph, "Connect House Wire");
            if (world.WireGraph.TryConnect(selectedOutputEndpoint, selectedOutputPort, endpoint, port, out string error))
            {
                statusMessage = $"Connected {selectedOutputPort.DisplayName} to {port.DisplayName}.";
                selectedOutputEndpoint = null;
                selectedOutputPort = null;
                EditorUtility.SetDirty(world.WireGraph);
                MarkSceneDirty();
            }
            else
            {
                statusMessage = error;
            }

            Repaint();
        }

        private void StartPlacement(HousePlaceableDefinition definition)
        {
            ResolveWorld();
            if (world == null)
            {
                CreateWorld();
            }

            if (definition == null || definition.Prefab == null)
            {
                statusMessage = "The selected definition has no prefab.";
                return;
            }

            DestroyGhost();
            placingDefinition = definition;
            requestedRotation = Quaternion.identity;
            ghostObject = Instantiate(definition.Prefab);
            SetHideFlags(ghostObject, HideFlags.HideAndDontSave);
            ghost = ghostObject.AddComponent<HouseBuilderGhost>();
            ghost.Initialize();
            statusMessage = $"Placing {definition.DisplayName}. Left click to place, Q/E to rotate, Esc/right click to cancel.";
            SceneView.RepaintAll();
        }

        private void PlaceCurrent()
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(placingDefinition.Prefab, world.transform);
            Undo.RegisterCreatedObjectUndo(instance, $"Place {placingDefinition.DisplayName}");
            instance.transform.SetPositionAndRotation(lastPlacement.Position, lastPlacement.Rotation);
            HouseBuilderObject builderObject = world.RegisterPlaceable(instance, placingDefinition);
            world.TryCreateWallOpening(instance, placingDefinition, lastSurfaceCollider);
            Selection.activeObject = builderObject.gameObject;
            statusMessage = $"Placed {placingDefinition.DisplayName}.";
            MarkSceneDirty();
        }

        private void CancelPlacement()
        {
            placingDefinition = null;
            DestroyGhost();
            statusMessage = "Placement cancelled.";
            SceneView.RepaintAll();
            Repaint();
        }

        private void DestroyGhost()
        {
            if (ghostObject != null)
            {
                DestroyImmediate(ghostObject);
            }

            ghostObject = null;
            ghost = null;
        }

        private void CreateGeometry()
        {
            ResolveWorld();
            if (world == null)
            {
                CreateWorld();
            }

            Material defaultMaterial = catalog != null && catalog.Materials.Count > 0 ? catalog.Materials[0]?.Material : null;
            HouseGeometryDescriptor descriptor = new(
                geometryKind,
                geometrySize,
                openingWidth,
                openingHeight,
                sillHeight,
                stairCount);
            GameObject created = HouseGeometryFactory.Create(descriptor, defaultMaterial);
            Undo.RegisterCreatedObjectUndo(created, $"Create {geometryKind}");
            created.transform.SetParent(world.transform, true);
            Vector3 position = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            created.transform.position = placementSettings.GridSnapping
                ? HouseBuilderSnapUtility.SnapVector(position, placementSettings.GridSize)
                : position;

            if (catalog != null && catalog.Materials.Count > 0 && catalog.Materials[0] != null)
            {
                HouseBuilderMaterialController controller = created.GetComponent<HouseBuilderMaterialController>();
                for (int i = 0; i < HouseGeometryFactory.MaterialSlotCount; i++)
                {
                    controller.SetBinding((HouseFaceRole)i, string.Empty, i, catalog.Materials[0].Id);
                }
            }

            Selection.activeObject = created;
            statusMessage = $"Created {geometryKind}.";
            MarkSceneDirty();
        }

        private void AssignMaterial(HouseBuilderObject selected, HouseMaterialDefinition material)
        {
            HouseBuilderMaterialController controller = selected.GetComponent<HouseBuilderMaterialController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<HouseBuilderMaterialController>(selected.gameObject);
            }

            Undo.RecordObject(controller, "Assign House Builder Material");
            HouseGeometryObject geometry = selected.GetComponent<HouseGeometryObject>();
            if (geometry != null)
            {
                controller.SetBinding(materialFaceRole, string.Empty, (int)materialFaceRole, material.Id);
            }
            else
            {
                Renderer renderer = selected.GetComponentInChildren<Renderer>(true);
                if (renderer == null)
                {
                    statusMessage = "The selected object has no renderer.";
                    return;
                }

                controller.SetBinding(materialFaceRole, GetRelativePath(selected.transform, renderer.transform), 0, material.Id);
            }

            controller.Apply(catalog);
            EditorUtility.SetDirty(controller);
            statusMessage = $"Assigned {material.DisplayName} to {materialFaceRole}.";
            MarkSceneDirty();
        }

        private void PerformBoolean(HouseBooleanOperation operation)
        {
            try
            {
                ResolveWorld();
                GameObject result = HouseBuilderBooleanUtility.Perform(booleanLeft, booleanRight, operation);
                if (world != null)
                {
                    result.transform.SetParent(world.transform, true);
                }

                Selection.activeObject = result;
                statusMessage = $"Created {operation} result.";
                MarkSceneDirty();
            }
            catch (Exception exception)
            {
                statusMessage = exception.InnerException?.Message ?? exception.Message;
                Debug.LogException(exception);
            }
        }

        private void SaveWorld()
        {
            string path = EditorUtility.SaveFilePanel("Save House Builder Document", Application.dataPath, world.DocumentName, "house.json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            HouseBuilderSaveSystem.SaveFile(path, world.CaptureDocument());
            statusMessage = $"Saved {Path.GetFileName(path)}.";
        }

        private void LoadWorld()
        {
            string path = EditorUtility.OpenFilePanel("Load House Builder Document", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(world.gameObject, "Load House Builder Document");
            world.LoadDocument(HouseBuilderSaveSystem.LoadFile(path));
            statusMessage = $"Loaded {Path.GetFileName(path)}.";
            MarkSceneDirty();
        }

        private void CreateWorld()
        {
            GameObject root = new("HouseBuilderWorld");
            Undo.RegisterCreatedObjectUndo(root, "Create House Builder World");
            world = root.AddComponent<HouseBuilderWorld>();
            world.Configure(catalog);
            Selection.activeObject = root;
            statusMessage = "Created House Builder world.";
            MarkSceneDirty();
        }

        private void ResolveWorld()
        {
            if (world == null)
            {
                world = FindAnyObjectByType<HouseBuilderWorld>();
            }
        }

        private bool TryRaycastSurface(Ray ray, out RaycastHit bestHit)
        {
            RaycastHit[] hits = Physics.RaycastAll(ray, 10000f, placementSettings.SurfaceMask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                if (ghostObject == null || !hits[i].collider.transform.IsChildOf(ghostObject.transform))
                {
                    bestHit = hits[i];
                    return true;
                }
            }

            bestHit = default;
            return false;
        }

        private static Vector3 GetFallbackPosition(Ray ray)
        {
            Plane ground = new(Vector3.up, Vector3.zero);
            return ground.Raycast(ray, out float distance) ? ray.GetPoint(distance) : ray.GetPoint(8f);
        }

        private IEnumerable<Bounds> GatherNearbyBounds()
        {
            if (world == null)
            {
                yield break;
            }

            Collider[] colliders = world.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].enabled)
                {
                    yield return colliders[i].bounds;
                }
            }
        }

        private void MarkSceneDirty()
        {
            if (world != null && world.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
            }
        }

        private static string GetRelativePath(Transform root, Transform target)
        {
            if (target == root)
            {
                return string.Empty;
            }

            Stack<string> names = new();
            Transform current = target;
            while (current != null && current != root)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }

        private static string ShortId(string id)
        {
            return string.IsNullOrEmpty(id) || id.Length <= 6 ? id : id[..6];
        }

        private static void SetHideFlags(GameObject root, HideFlags flags)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].gameObject.hideFlags = flags;
            }
        }
    }
}
#endif
