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
        private static readonly string[] Tabs = { "Place", "Draw", "Paint", "Connect", "Combine", "Save" };
        private static readonly HouseGeometryKind[] ShapeKinds =
        {
            HouseGeometryKind.Wall,
            HouseGeometryKind.Floor,
            HouseGeometryKind.Ceiling,
            HouseGeometryKind.Cube,
            HouseGeometryKind.Doorway,
            HouseGeometryKind.Window,
            HouseGeometryKind.Ramp,
            HouseGeometryKind.Stairs
        };
        private static readonly string[] ShapeNames = { "Wall", "Floor", "Ceiling", "Block", "Doorway", "Window", "Ramp", "Stairs" };

        [SerializeField] private HouseBuilderPlacementSettings placementSettings = new();
        [SerializeField] private HouseBuilderCatalog catalog;
        [SerializeField] private HouseBuilderWorld world;
        [SerializeField] private HouseGeometryKind geometryKind = HouseGeometryKind.Wall;
        [SerializeField] private Vector3 geometrySize = new(4f, 3f, 0.25f);
        [SerializeField] private float openingWidth = 1.2f;
        [SerializeField] private float openingHeight = 2.1f;
        [SerializeField] private float sillHeight = 0.9f;
        [SerializeField] private int stairCount = 8;
        [SerializeField] private bool showSetup;
        [SerializeField] private bool drawGeometryInScene;
        [SerializeField] private bool showSavedBindings;
        [SerializeField] private HouseMaterialDefinition paintMaterial;
        [SerializeField] private bool paintMode;

        private int activeTab;
        private Vector2 scroll;
        private string search = string.Empty;
        private string categoryFilter = string.Empty;
        private HousePlaceableDefinition placingDefinition;
        private HouseBuilderObject reinforcementTrigger;
        private HousePlaceableDefinition reinforcementLocationDefinition;
        private readonly List<HousePlaceableDefinition> reinforcementLocationChoices = new();
        private bool placingReinforcementLocations;
        private GameObject ghostObject;
        private HouseBuilderGhost ghost;
        private Quaternion requestedRotation = Quaternion.identity;
        private HousePlacementResult lastPlacement;
        private HousePlacementValidationResult lastValidation;
        private Collider lastSurfaceCollider;
        private GameObject booleanLeft;
        private GameObject booleanRight;
        private HouseWireEndpoint selectedOutputEndpoint;
        private HouseWirePortDefinition selectedOutputPort;
        private bool clickedWireInputThisEvent;
        private string statusMessage = "Choose an asset to place or a shape to draw.";
        private bool drawingGeometry;
        private Vector3 geometryDrawStart;
        private Vector3 geometryDrawCurrent;
        private float geometryDrawPlaneY;
        private string lastPaintKey = string.Empty;

        [MenuItem("Tools/Neighbor/House Builder/Level Editor")]
        public static void Open()
        {
            GetWindow<HouseBuilderLevelEditorWindow>("House Builder");
        }

        private void OnEnable()
        {
            minSize = new Vector2(340f, 420f);
            if (catalog == null)
            {
                catalog = AssetDatabase.LoadAssetAtPath<HouseBuilderCatalog>(HouseBuilderAssetInstaller.DefaultCatalogPath);
            }

            geometrySize = HouseBuilderEditorInteractionUtility.SanitizeSize(geometrySize);
            ResolveWorld();
            SceneView.duringSceneGui += OnSceneGUI;
            Selection.selectionChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Selection.selectionChanged -= OnSelectionChanged;
            DestroyGhost();
        }

        private void OnSelectionChanged()
        {
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnGUI()
        {
            DrawHeader();
            int columns = position.width < 560f ? 3 : Tabs.Length;
            activeTab = GUILayout.SelectionGrid(activeTab, Tabs, columns, EditorStyles.miniButton);
            EditorGUILayout.Space(6f);

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
            if (!string.IsNullOrWhiteSpace(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("House Builder", EditorStyles.boldLabel);
            if (world == null)
            {
                EditorGUILayout.HelpBox("Create a Builder World to start. Placed objects will be kept together under it.", MessageType.Info);
                if (GUILayout.Button("Create Builder World", GUILayout.Height(30f)))
                {
                    CreateWorld();
                }
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"Building in: {world.name}", EditorStyles.boldLabel);
                    if (GUILayout.Button("Select", GUILayout.Width(56f)))
                    {
                        Selection.activeObject = world.gameObject;
                    }
                }
            }

            showSetup = EditorGUILayout.Foldout(showSetup, "Setup & Snapping", true);
            if (!showSetup)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                catalog = (HouseBuilderCatalog)EditorGUILayout.ObjectField("Asset Catalog", catalog, typeof(HouseBuilderCatalog), false);
                world = (HouseBuilderWorld)EditorGUILayout.ObjectField("Builder World", world, typeof(HouseBuilderWorld), true);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Refresh Starter Assets"))
                    {
                        HouseBuilderAssetInstaller.CreateOrRefreshDefaults();
                        catalog = AssetDatabase.LoadAssetAtPath<HouseBuilderCatalog>(HouseBuilderAssetInstaller.DefaultCatalogPath);
                    }

                    if (GUILayout.Button("New World"))
                    {
                        CreateWorld();
                    }
                }

                SerializedObject serializedWindow = new(this);
                SerializedProperty settings = serializedWindow.FindProperty("placementSettings");
                EditorGUILayout.PropertyField(settings, new GUIContent("Snap & Collision Settings"), true);
                serializedWindow.ApplyModifiedProperties();
            }
        }

        private void DrawBuildTab()
        {
            if (catalog == null)
            {
                EditorGUILayout.HelpBox("No asset catalog is assigned. Open Setup & Snapping and refresh the starter assets.", MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox("Click a card to start placing it; click the green selected card again to stop. Rotate with Q/E (yaw), R/F (pitch), or Z/C (roll).", MessageType.None);
            if (placingDefinition != null || placingReinforcementLocations)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    string placementName = placingReinforcementLocations ? "Reinforcement Locations" : placingDefinition.DisplayName;
                    EditorGUILayout.LabelField($"Placing: {placementName}", EditorStyles.boldLabel);
                    if (GUILayout.Button("Stop", GUILayout.Width(56f)))
                    {
                        CancelPlacement();
                    }
                }
            }

            search = EditorGUILayout.TextField("Find Asset", search);
            DrawCategoryFilters();
            EditorGUILayout.Space(4f);

            IEnumerable<HousePlaceableDefinition> definitions = catalog.Placeables.Where(definition =>
                definition != null
                && !definition.HideFromCatalog
                && MatchesCategoryFilter(definition.CategoryId)
                && (string.IsNullOrWhiteSpace(search)
                    || definition.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                    || FriendlyCategoryName(definition.CategoryId).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                    || definition.Tags.Any(tag => tag.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)));

            bool drewAny = false;
            foreach (HousePlaceableDefinition definition in definitions)
            {
                drewAny = true;
                DrawPlaceableCard(definition);
            }

            if (!drewAny)
            {
                EditorGUILayout.HelpBox("No assets match this filter.", MessageType.Info);
            }
        }

        private void DrawPlaceableCard(HousePlaceableDefinition definition)
        {
            Texture preview = definition.Preview != null
                ? definition.Preview
                : AssetPreview.GetAssetPreview(definition.Prefab) ?? AssetPreview.GetMiniThumbnail(definition.Prefab);
            GUIStyle cardStyle = new(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                imagePosition = ImagePosition.ImageLeft,
                fontStyle = placingDefinition == definition ? FontStyle.Bold : FontStyle.Normal,
                padding = new RectOffset(8, 8, 5, 5)
            };
            GUIContent content = new(
                $"{definition.DisplayName}\n{FriendlyCategoryName(definition.CategoryId)}",
                preview,
                placingDefinition == definition ? $"Stop placing {definition.DisplayName}" : $"Place {definition.DisplayName}");
            Color previous = GUI.backgroundColor;
            if (placingDefinition == definition)
            {
                GUI.backgroundColor = new Color(0.35f, 0.85f, 0.5f);
            }

            if (GUILayout.Button(content, cardStyle, GUILayout.ExpandWidth(true), GUILayout.Height(58f)))
            {
                if (HouseBuilderEditorInteractionUtility.ShouldDeselectPlaceable(placingDefinition, definition))
                {
                    CancelPlacement();
                }
                else
                {
                    StartPlacement(definition);
                }
            }

            GUI.backgroundColor = previous;
        }

        private void DrawCategoryFilters()
        {
            List<string> labels = new() { "All" };
            List<string> ids = new() { string.Empty };
            bool addedAi = false;
            for (int i = 0; i < catalog.Categories.Count; i++)
            {
                HouseBuilderCategoryDefinition category = catalog.Categories[i];
                if (category == null)
                {
                    continue;
                }

                if (category.Id.StartsWith("ai.", StringComparison.Ordinal))
                {
                    if (!addedAi)
                    {
                        labels.Add("AI");
                        ids.Add("ai.*");
                        addedAi = true;
                    }

                    continue;
                }

                labels.Add(category.DisplayName);
                ids.Add(category.Id);
            }

            int current = Mathf.Max(0, ids.IndexOf(categoryFilter));
            int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 24f) / 145f));
            int selected = GUILayout.SelectionGrid(current, labels.ToArray(), columns, EditorStyles.miniButton);
            if (selected >= 0 && selected < ids.Count)
            {
                categoryFilter = ids[selected];
            }
        }

        private void DrawGeometryTab()
        {
            EditorGUILayout.HelpBox("Choose a shape, set its useful dimensions, then draw it directly in Scene view. Select an existing shape to resize it.", MessageType.None);

            int selectedShape = Array.IndexOf(ShapeKinds, geometryKind);
            selectedShape = Mathf.Max(0, selectedShape);
            int nextShape = GUILayout.SelectionGrid(selectedShape, ShapeNames, 3, GUILayout.MinHeight(48f));
            if (nextShape != selectedShape)
            {
                geometryKind = ShapeKinds[nextShape];
                geometrySize = HouseBuilderEditorInteractionUtility.SuggestedSize(geometryKind);
                drawingGeometry = false;
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("New Shape Dimensions", EditorStyles.boldLabel);
            DrawDimensionFields(ref geometrySize, geometryKind);
            if (GUILayout.Button($"Reset to Suggested {FriendlyGeometryName(geometryKind)} Size"))
            {
                geometrySize = HouseBuilderEditorInteractionUtility.SuggestedSize(geometryKind);
            }

            if (geometryKind is HouseGeometryKind.Doorway or HouseGeometryKind.Window)
            {
                openingWidth = Mathf.Max(0.1f, EditorGUILayout.FloatField("Opening Width", openingWidth));
                openingHeight = Mathf.Max(0.1f, EditorGUILayout.FloatField("Opening Height", openingHeight));
            }

            if (geometryKind == HouseGeometryKind.Window)
            {
                sillHeight = Mathf.Max(0f, EditorGUILayout.FloatField("Sill Height", sillHeight));
            }

            if (geometryKind == HouseGeometryKind.Stairs)
            {
                stairCount = EditorGUILayout.IntSlider("Number of Steps", stairCount, 1, 32);
            }

            Color previous = GUI.backgroundColor;
            if (drawGeometryInScene)
            {
                GUI.backgroundColor = new Color(0.35f, 0.8f, 1f);
            }

            if (GUILayout.Button(drawGeometryInScene ? $"Stop Drawing {FriendlyGeometryName(geometryKind)}" : $"Draw {FriendlyGeometryName(geometryKind)} in Scene", GUILayout.Height(34f)))
            {
                drawGeometryInScene = !drawGeometryInScene;
                drawingGeometry = false;
                statusMessage = drawGeometryInScene
                    ? $"Draw mode active. Click and drag in Scene view to create a {FriendlyGeometryName(geometryKind).ToLowerInvariant()}."
                    : "Draw mode stopped.";
                SceneView.lastActiveSceneView?.Focus();
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = previous;
            if (GUILayout.Button("Create One at Scene View Center", GUILayout.Height(25f)))
            {
                CreateGeometryAtSceneCenter();
            }

            HouseGeometryObject selectedGeometry = GetSelectedGeometry();
            if (selectedGeometry == null)
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox("Tip: the Place tab also contains ready-to-use Basic Wall, Basic Floor, and Basic Ceiling prefabs.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(10f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Resize Selected: {selectedGeometry.name}", EditorStyles.boldLabel);
                Vector3 selectedSize = selectedGeometry.Descriptor.Size;
                EditorGUI.BeginChangeCheck();
                DrawDimensionFields(ref selectedSize, selectedGeometry.Descriptor.Kind);
                if (EditorGUI.EndChangeCheck())
                {
                    ResizeGeometry(selectedGeometry, selectedSize);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Use Suggested Size"))
                    {
                        ResizeGeometry(selectedGeometry, HouseBuilderEditorInteractionUtility.SuggestedSize(selectedGeometry.Descriptor.Kind));
                    }

                    if (GUILayout.Button("Frame in Scene"))
                    {
                        Selection.activeObject = selectedGeometry.gameObject;
                        SceneView.lastActiveSceneView?.FrameSelected();
                    }
                }
            }
        }

        private static void DrawDimensionFields(ref Vector3 size, HouseGeometryKind kind)
        {
            size.x = Mathf.Max(0.05f, EditorGUILayout.FloatField("Width", size.x));
            if (kind is HouseGeometryKind.Floor or HouseGeometryKind.Ceiling)
            {
                size.z = Mathf.Max(0.05f, EditorGUILayout.FloatField("Depth", size.z));
                size.y = Mathf.Max(0.05f, EditorGUILayout.FloatField("Thickness", size.y));
            }
            else if (kind is HouseGeometryKind.Wall or HouseGeometryKind.Doorway or HouseGeometryKind.Window)
            {
                size.y = Mathf.Max(0.05f, EditorGUILayout.FloatField("Height", size.y));
                size.z = Mathf.Max(0.05f, EditorGUILayout.FloatField("Thickness", size.z));
            }
            else
            {
                size.y = Mathf.Max(0.05f, EditorGUILayout.FloatField("Height", size.y));
                size.z = Mathf.Max(0.05f, EditorGUILayout.FloatField("Depth", size.z));
            }
        }

        private void DrawMaterialsTab()
        {
            if (catalog == null || catalog.Materials.Count == 0)
            {
                EditorGUILayout.HelpBox("The catalog has no paint materials. Refresh the starter assets from Setup & Snapping.", MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox("Click a material, then click or drag over faces in Scene view. You can also drag a card, or any Unity Material from the Project view, directly onto a face.", MessageType.None);
            if (paintMaterial != null)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"Brush: {paintMaterial.DisplayName}", EditorStyles.boldLabel);
                    if (GUILayout.Button(paintMode ? "Stop Painting" : "Start Painting", GUILayout.Width(96f)))
                    {
                        paintMode = !paintMode;
                        SceneView.lastActiveSceneView?.Focus();
                        SceneView.RepaintAll();
                    }
                }
            }

            for (int i = 0; i < catalog.Materials.Count; i++)
            {
                HouseMaterialDefinition material = catalog.Materials[i];
                if (material != null)
                {
                    DrawMaterialCard(material);
                }
            }

            HouseBuilderObject selected = GetSelectedBuilderObject();
            if (selected == null)
            {
                return;
            }

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Selected: {selected.name}", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(paintMaterial == null))
                {
                    if (GUILayout.Button("Apply Brush to Entire Object"))
                    {
                        AssignMaterialToWholeObject(selected, paintMaterial);
                    }
                }

                HouseBuilderMaterialController controller = selected.GetComponent<HouseBuilderMaterialController>();
                if (controller != null)
                {
                    showSavedBindings = EditorGUILayout.Foldout(showSavedBindings, $"Saved Face Paint ({controller.Bindings.Count})", true);
                    if (showSavedBindings)
                    {
                        foreach (HouseMaterialBinding binding in controller.Bindings)
                        {
                            EditorGUILayout.LabelField($"{binding.FaceRole}: {binding.MaterialId}", EditorStyles.miniLabel);
                        }
                    }
                }
            }
        }

        private void DrawMaterialCard(HouseMaterialDefinition material)
        {
            Rect card = GUILayoutUtility.GetRect(0f, 54f, GUILayout.ExpandWidth(true));
            int controlId = GUIUtility.GetControlID(FocusType.Passive, card);
            bool selected = paintMaterial == material;
            if (Event.current.type == EventType.Repaint)
            {
                GUIStyle style = selected ? GUI.skin.button : EditorStyles.helpBox;
                style.Draw(card, GUIContent.none, false, false, selected, false);
                Texture preview = material.Preview != null
                    ? material.Preview
                    : AssetPreview.GetAssetPreview(material.Material) ?? AssetPreview.GetMiniThumbnail(material.Material);
                Rect previewRect = new(card.x + 5f, card.y + 5f, 44f, 44f);
                if (preview != null)
                {
                    GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);
                }

                GUI.Label(new Rect(card.x + 57f, card.y + 9f, card.width - 64f, 20f), material.DisplayName, EditorStyles.boldLabel);
                GUI.Label(new Rect(card.x + 57f, card.y + 28f, card.width - 64f, 18f), selected ? "Active brush - drag me onto a face" : "Click to paint, or drag onto a face", EditorStyles.miniLabel);
            }

            Event current = Event.current;
            switch (current.GetTypeForControl(controlId))
            {
                case EventType.MouseDown when current.button == 0 && card.Contains(current.mousePosition):
                    GUIUtility.hotControl = controlId;
                    paintMaterial = material;
                    paintMode = true;
                    statusMessage = $"Painting with {material.DisplayName}. Click or drag across faces in Scene view.";
                    current.Use();
                    Repaint();
                    SceneView.RepaintAll();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == controlId:
                    GUIUtility.hotControl = 0;
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.objectReferences = new Object[] { material };
                    DragAndDrop.StartDrag($"Paint {material.DisplayName}");
                    current.Use();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == controlId:
                    GUIUtility.hotControl = 0;
                    current.Use();
                    break;
            }
        }

        private void DrawWiringTab()
        {
            if (world == null)
            {
                EditorGUILayout.HelpBox("Create or assign a Builder World first.", MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox("Select any connectable object to reveal its ports in Scene view. Click an orange output, then choose a cyan input while the live cable follows the cursor.", MessageType.None);
            if (selectedOutputEndpoint != null && selectedOutputPort != null)
            {
                EditorGUILayout.LabelField($"From: {selectedOutputEndpoint.name} / {selectedOutputPort.DisplayName}", EditorStyles.boldLabel);
                if (GUILayout.Button("Cancel Connection"))
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
                    EditorGUILayout.LabelField($"{ShortId(connection.OutputObjectId)} -> {ShortId(connection.InputObjectId)}");
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
            EditorGUILayout.HelpBox("Combine two mesh objects into a new editable builder shape. The originals remain unchanged.", MessageType.None);
            booleanLeft = (GameObject)EditorGUILayout.ObjectField("Main Shape", booleanLeft, typeof(GameObject), true);
            booleanRight = (GameObject)EditorGUILayout.ObjectField("Second Shape", booleanRight, typeof(GameObject), true);
            if (GUILayout.Button("Cut Second Shape from Main", GUILayout.Height(28f)))
            {
                PerformBoolean(HouseBooleanOperation.Subtract);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Keep Overlap"))
                {
                    PerformBoolean(HouseBooleanOperation.Intersect);
                }

                if (GUILayout.Button("Join Together"))
                {
                    PerformBoolean(HouseBooleanOperation.Union);
                }
            }
        }

        private void DrawSaveTab()
        {
            if (world == null)
            {
                EditorGUILayout.HelpBox("Create or assign a Builder World first.", MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox("Save the complete building, including geometry, face paint, props, AI points, wall holes, and connections.", MessageType.None);
            if (GUILayout.Button("Save Building File", GUILayout.Height(32f)))
            {
                SaveWorld();
            }

            if (GUILayout.Button("Load Building File", GUILayout.Height(32f)))
            {
                LoadWorld();
            }

            EditorGUILayout.Space(12f);
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
            clickedWireInputThisEvent = false;
            DrawWires();
            if (ShouldDrawWirePorts())
            {
                DrawWirePorts();
            }

            if (HandleWireCancellation())
            {
                return;
            }

            if (placingDefinition != null && ghostObject != null)
            {
                HandlePrefabPlacement(sceneView);
                return;
            }

            if (placingReinforcementLocations && ghostObject != null)
            {
                HandleReinforcementLocationPlacement(sceneView);
                return;
            }

            if (activeTab == 1)
            {
                DrawSelectedGeometryHandle();
                if (drawGeometryInScene)
                {
                    HandleGeometryDrawing(sceneView);
                }

                return;
            }

            if (activeTab == 2)
            {
                HandleMaterialPainting(sceneView);
            }
        }

        private void HandlePrefabPlacement(SceneView sceneView)
        {
            Event current = Event.current;
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            DrawSceneBanner(sceneView, $"Placing {placingDefinition.DisplayName}", "Left click: place    Right click/drag: erase    Q/E: yaw    R/F: pitch    Z/C: roll    Esc: stop");
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                CancelPlacement();
                current.Use();
                return;
            }

            if (TryRotatePlacement(current))
            {
                current.Use();
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            bool canErase = HouseBuilderEditorInteractionUtility.TryPickMatchingPlacedObject(
                ray,
                placementSettings.SurfaceMask,
                world != null ? world.transform : null,
                ghostObject,
                placingDefinition.Id,
                out HouseBuilderObject eraseTarget);
            if (canErase)
            {
                DrawEraseHighlight(eraseTarget);
            }

            if ((current.type == EventType.MouseDown || current.type == EventType.MouseDrag) && current.button == 1 && !current.alt)
            {
                if (canErase)
                {
                    ErasePlacedObject(eraseTarget);
                }
                else
                {
                    statusMessage = $"Point at a placed {placingDefinition.DisplayName} to erase it.";
                }

                current.Use();
                sceneView.Repaint();
                return;
            }

            bool hasSurface = TryRaycastSurface(ray, out RaycastHit hit);
            Vector3 fallback = GetFallbackPosition(ray);
            lastPlacement = HouseBuilderSnapUtility.Calculate(
                fallback,
                requestedRotation,
                hasSurface,
                hit,
                placingDefinition.Placement,
                placementSettings,
                GatherNearbyBounds(),
                ray.direction);
            if (hasSurface
                && placingDefinition.WallOpening != null
                && placingDefinition.WallOpening.PlaceInsideWallOpening
                && hit.collider.GetComponentInParent<HouseGeometryObject>() is { } openingWall
                && openingWall.Descriptor.Kind == HouseGeometryKind.Wall)
            {
                lastPlacement = new HousePlacementResult(
                    openingWall.CenterOnWallMidplane(lastPlacement.Position),
                    lastPlacement.Rotation,
                    lastPlacement.SurfaceNormal,
                    lastPlacement.SurfaceType,
                    lastPlacement.SnapKind,
                    lastPlacement.HasSurface);
            }

            if (placingDefinition.CategoryId == HouseBuilderCategories.Ceiling
                && HouseBuilderEditorInteractionUtility.TryCalculateCeilingFootprintPlacement(
                    hasSurface ? hit.point : fallback,
                    lastPlacement.Rotation * placingDefinition.Placement.PlacementOffset,
                    GatherGeometryBounds(HouseGeometryKind.Floor),
                    GatherGeometryBounds(HouseGeometryKind.Wall),
                    out Vector3 ceilingPosition))
            {
                lastPlacement = new HousePlacementResult(
                    ceilingPosition,
                    lastPlacement.Rotation,
                    Vector3.up,
                    HouseSurfaceType.Ground,
                    HouseSnapKind.Surface,
                    true);
            }

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
            Handles.Label(lastPlacement.Position + Vector3.up * 0.4f, lastValidation.IsValid ? "Click to place" : lastValidation.Message);

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

        private void HandleGeometryDrawing(SceneView sceneView)
        {
            Event current = Event.current;
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            DrawSceneBanner(sceneView, $"Drawing {FriendlyGeometryName(geometryKind)}", "Click and drag to size    Click once for the entered dimensions    Esc: stop");

            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                if (drawingGeometry)
                {
                    drawingGeometry = false;
                    statusMessage = "Current shape cancelled. Draw mode is still active.";
                }
                else
                {
                    drawGeometryInScene = false;
                    statusMessage = "Draw mode stopped.";
                }

                current.Use();
                Repaint();
                return;
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            if (!TryGetConstructionPoint(ray, drawingGeometry, out Vector3 point))
            {
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 0 && !current.alt)
            {
                drawingGeometry = true;
                geometryDrawStart = point;
                geometryDrawCurrent = point;
                geometryDrawPlaneY = point.y;
                current.Use();
            }
            else if (drawingGeometry && (current.type == EventType.MouseDrag || current.type == EventType.MouseMove))
            {
                geometryDrawCurrent = point;
                current.Use();
            }
            else if (drawingGeometry && current.type == EventType.MouseUp && current.button == 0)
            {
                geometryDrawCurrent = point;
                CreateGeometryFromStroke();
                drawingGeometry = false;
                current.Use();
            }

            if (drawingGeometry && TryCalculateDrawShape(out Vector3 position, out Quaternion rotation, out Vector3 size))
            {
                Handles.color = new Color(0.1f, 0.85f, 1f);
                Matrix4x4 previous = Handles.matrix;
                Handles.matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
                Handles.DrawWireCube(Vector3.zero, size);
                Handles.matrix = previous;
                Handles.Label(position + Vector3.up * size.y * 0.55f, $"{size.x:0.##} x {size.y:0.##} x {size.z:0.##} m");
            }
            else
            {
                Handles.color = new Color(0.1f, 0.85f, 1f);
                Handles.DrawWireDisc(point, Vector3.up, HandleUtility.GetHandleSize(point) * 0.08f);
            }

            sceneView.Repaint();
        }

        private void HandleMaterialPainting(SceneView sceneView)
        {
            Event current = Event.current;
            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            bool overFace = HouseBuilderEditorInteractionUtility.TryPickFace(ray, placementSettings.SurfaceMask, out HouseBuilderFaceHit face);
            if (current.type is EventType.DragUpdated or EventType.DragPerform)
            {
                Object dragged = DragAndDrop.objectReferences.FirstOrDefault(candidate => candidate is HouseMaterialDefinition or Material);
                if (dragged != null && overFace)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    DrawFaceHighlight(face, dragged.name);
                    if (current.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        HouseMaterialDefinition definition = ResolveDraggedMaterial(dragged);
                        if (definition != null)
                        {
                            paintMaterial = definition;
                            paintMode = true;
                            AssignMaterial(face, definition);
                        }
                    }

                    current.Use();
                    sceneView.Repaint();
                    return;
                }
            }

            if (!paintMode || paintMaterial == null)
            {
                return;
            }

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            DrawSceneBanner(sceneView, $"Painting with {paintMaterial.DisplayName}", "Click or drag over faces    Drag a material here    Esc or right click: stop");
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape
                || current.type == EventType.MouseDown && current.button == 1)
            {
                paintMode = false;
                lastPaintKey = string.Empty;
                statusMessage = "Paint mode stopped.";
                current.Use();
                Repaint();
                return;
            }

            if (overFace)
            {
                DrawFaceHighlight(face, paintMaterial.DisplayName);
                if ((current.type == EventType.MouseDown || current.type == EventType.MouseDrag) && current.button == 0 && !current.alt)
                {
                    string paintKey = $"{face.Owner.InstanceId}|{face.RendererPath}|{face.MaterialIndex}";
                    if (current.type == EventType.MouseDown || paintKey != lastPaintKey)
                    {
                        AssignMaterial(face, paintMaterial);
                        lastPaintKey = paintKey;
                    }

                    current.Use();
                }
            }

            if (current.type == EventType.MouseUp)
            {
                lastPaintKey = string.Empty;
            }

            sceneView.Repaint();
        }

        private static void DrawFaceHighlight(HouseBuilderFaceHit face, string materialName)
        {
            Handles.color = new Color(0.15f, 0.9f, 1f, 0.85f);
            if (face.Triangle.Length == 3)
            {
                Handles.DrawAAConvexPolygon(face.Triangle);
                Handles.DrawAAPolyLine(4f, face.Triangle[0], face.Triangle[1], face.Triangle[2], face.Triangle[0]);
            }
            else
            {
                Handles.DrawWireDisc(face.RaycastHit.point, face.RaycastHit.normal, HandleUtility.GetHandleSize(face.RaycastHit.point) * 0.12f);
            }

            Handles.Label(face.RaycastHit.point + face.RaycastHit.normal * 0.05f, $"{face.FaceRole}  <-  {materialName}");
        }

        private static void DrawSceneBanner(SceneView sceneView, string title, string instructions)
        {
            Handles.BeginGUI();
            float width = Mathf.Max(220f, Mathf.Min(520f, sceneView.position.width - 24f));
            GUI.Box(new Rect(12f, 12f, width, 48f), $"{title}\n{instructions}");
            Handles.EndGUI();
        }

        private void DrawSelectedGeometryHandle()
        {
            if (drawGeometryInScene)
            {
                return;
            }

            HouseGeometryObject geometry = GetSelectedGeometry();
            if (geometry == null)
            {
                return;
            }

            Transform target = geometry.transform;
            EditorGUI.BeginChangeCheck();
            Vector3 size = Handles.ScaleHandle(
                geometry.Descriptor.Size,
                target.position,
                target.rotation,
                HandleUtility.GetHandleSize(target.position));
            if (EditorGUI.EndChangeCheck())
            {
                ResizeGeometry(geometry, size);
            }

            Handles.Label(target.position + Vector3.up * geometry.Descriptor.Size.y * 0.6f, "Drag handles to resize");
        }

        private void DrawWires()
        {
            if (world == null)
            {
                return;
            }

            int removedConnections = world.WireGraph.PruneInvalidConnections();
            if (removedConnections > 0)
            {
                statusMessage = $"Removed {removedConnections} invalid or duplicate connection{(removedConnections == 1 ? string.Empty : "s")}.";
                EditorUtility.SetDirty(world.WireGraph);
                MarkSceneDirty();
                Repaint();
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

            if (selectedOutputEndpoint != null && selectedOutputPort != null && Event.current != null)
            {
                Vector3 start = selectedOutputEndpoint.GetPortWorldPosition(selectedOutputPort);
                Ray mouseRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                Vector3 end = GetFallbackPosition(mouseRay);
                float tangent = Mathf.Max(0.5f, Vector3.Distance(start, end) * 0.35f);
                Handles.DrawBezier(start, end, start + selectedOutputEndpoint.transform.forward * tangent, end - mouseRay.direction * tangent,
                    new Color(1f, 0.75f, 0.1f, 0.95f), null, 4f);
                Handles.Label(end, "Select a cyan input");
            }
        }

        private void DrawWirePorts()
        {
            if (world == null)
            {
                return;
            }

            IReadOnlyList<HouseWireEndpoint> endpoints = world.WireGraph.Endpoints;
            HouseBuilderObject selectedObject = GetSelectedBuilderObject();
            bool showAll = activeTab == 3 || selectedOutputEndpoint != null;
            for (int endpointIndex = 0; endpointIndex < endpoints.Count; endpointIndex++)
            {
                HouseWireEndpoint endpoint = endpoints[endpointIndex];
                if (!showAll && (selectedObject == null || endpoint.Owner != selectedObject))
                {
                    continue;
                }

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

        private bool ShouldDrawWirePorts()
        {
            return HouseBuilderEditorInteractionUtility.ShouldShowWirePorts(
                activeTab == 3,
                selectedOutputEndpoint != null,
                GetSelectedBuilderObject());
        }

        private void HandlePortClick(HouseWireEndpoint endpoint, HouseWirePortDefinition port)
        {
            if (port.Direction == HouseWirePortDirection.Output)
            {
                if (selectedOutputEndpoint != null)
                {
                    CancelWireConnection();
                    return;
                }

                selectedOutputEndpoint = endpoint;
                selectedOutputPort = port;
                statusMessage = $"Selected output {endpoint.name}/{port.DisplayName}.";
                Repaint();
                return;
            }

            clickedWireInputThisEvent = true;
            if (selectedOutputEndpoint == null || selectedOutputPort == null)
            {
                statusMessage = "Select an orange output first.";
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

        private bool HandleWireCancellation()
        {
            if (selectedOutputEndpoint == null || Event.current == null)
            {
                return false;
            }

            Event current = Event.current;
            bool escape = current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape;
            bool clickedAway = current.type == EventType.MouseDown
                && current.button == 0
                && !current.alt
                && HouseBuilderEditorInteractionUtility.ShouldCancelPendingWire(true, clickedWireInputThisEvent);
            if (!escape && !clickedAway)
            {
                return false;
            }

            CancelWireConnection();
            current.Use();
            SceneView.RepaintAll();
            return true;
        }

        private void CancelWireConnection()
        {
            selectedOutputEndpoint = null;
            selectedOutputPort = null;
            statusMessage = "Connection cancelled.";
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
                statusMessage = "This asset has no prefab assigned.";
                return;
            }

            ClearReinforcementLocationPlacement();
            DestroyGhost();
            placingDefinition = definition;
            requestedRotation = Quaternion.identity;
            ghostObject = Instantiate(definition.Prefab);
            SetHideFlags(ghostObject, HideFlags.HideAndDontSave);
            ghost = ghostObject.AddComponent<HouseBuilderGhost>();
            ghost.Initialize();
            statusMessage = $"Placing {definition.DisplayName}. Move into Scene view and left-click to place.";
            SceneView.lastActiveSceneView?.Focus();
            SceneView.RepaintAll();
            Repaint();
        }

        private void PlaceCurrent()
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(placingDefinition.Prefab, world.transform);
            Undo.RegisterCreatedObjectUndo(instance, $"Place {placingDefinition.DisplayName}");
            instance.transform.SetPositionAndRotation(lastPlacement.Position, lastPlacement.Rotation);
            HouseBuilderObject builderObject = world.RegisterPlaceable(instance, placingDefinition);
            world.TryCreateWallOpening(instance, placingDefinition, lastSurfaceCollider);
            Selection.activeObject = builderObject.gameObject;
            statusMessage = $"Placed {placingDefinition.DisplayName}. Left-click to place another, or press Esc to stop.";
            MarkSceneDirty();

            if (placingDefinition.CategoryId == HouseBuilderCategories.ReinforcementTrigger)
            {
                BeginReinforcementTriggerSetup(builderObject);
            }
        }

        private void CancelPlacement()
        {
            if (placingReinforcementLocations)
            {
                ClearReinforcementLocationPlacement();
            }

            placingDefinition = null;
            DestroyGhost();
            statusMessage = "Placement stopped.";
            SceneView.RepaintAll();
            Repaint();
        }

        private void ErasePlacedObject(HouseBuilderObject target)
        {
            if (target == null)
            {
                return;
            }

            string displayName = placingDefinition != null ? placingDefinition.DisplayName : target.name;
            string actionName = $"Erase {displayName}";
            HouseWallOpeningLink openingLink = target.GetComponent<HouseWallOpeningLink>();
            if (openingLink != null && openingLink.Wall != null)
            {
                Undo.RecordObject(openingLink.Wall, actionName);
            }

            if (world != null && world.WireGraph != null)
            {
                Undo.RecordObject(world.WireGraph, actionName);
                world.WireGraph.RemoveConnectionsForObject(target.InstanceId);
            }

            if (world != null && target.CategoryId == HouseBuilderCategories.ReinforcementTrigger)
            {
                HouseReinforcementLocation[] locations = world.GetComponentsInChildren<HouseReinforcementLocation>(true);
                for (int i = locations.Length - 1; i >= 0; i--)
                {
                    if (locations[i] != null && locations[i].IsLinkedTo(target.InstanceId))
                    {
                        Undo.DestroyObjectImmediate(locations[i].gameObject);
                    }
                }
            }

            Undo.DestroyObjectImmediate(target.gameObject);
            statusMessage = $"Erased {displayName}. Keep holding right-click to erase more.";
            MarkSceneDirty();
            Repaint();
        }

        private void BeginReinforcementTriggerSetup(HouseBuilderObject trigger)
        {
            List<HousePlaceableDefinition> choices = catalog.Placeables
                .Where(definition => definition != null
                    && definition.Prefab != null
                    && definition.CategoryId == HouseBuilderCategories.Reinforcement)
                .ToList();
            placingDefinition = null;
            DestroyGhost();
            if (choices.Count == 0)
            {
                statusMessage = "The trigger was placed, but the catalog has no reinforcement assets to choose from.";
                return;
            }

            reinforcementTrigger = trigger;
            statusMessage = "Choose which reinforcements this trigger can spawn.";
            Vector2 screenPosition = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            HouseReinforcementPickerPopup.Show(
                screenPosition,
                choices,
                selected => StartReinforcementLocationPlacement(trigger, selected),
                () =>
                {
                    reinforcementTrigger = null;
                    statusMessage = "Reinforcement trigger placed without custom spawn locations.";
                    Repaint();
                });
            Repaint();
        }

        private void StartReinforcementLocationPlacement(
            HouseBuilderObject trigger,
            IReadOnlyList<HousePlaceableDefinition> choices)
        {
            if (trigger == null || choices == null || choices.Count == 0 || catalog == null)
            {
                return;
            }

            reinforcementLocationDefinition = catalog.Placeables.FirstOrDefault(definition =>
                definition != null && definition.CategoryId == HouseBuilderCategories.ReinforcementLocation);
            if (reinforcementLocationDefinition?.Prefab == null)
            {
                statusMessage = "The Reinforcement Location marker is missing. Refresh the House Builder default assets.";
                return;
            }

            reinforcementTrigger = trigger;
            reinforcementLocationChoices.Clear();
            reinforcementLocationChoices.AddRange(choices);
            placingReinforcementLocations = true;
            requestedRotation = Quaternion.identity;
            DestroyGhost();
            ghostObject = Instantiate(reinforcementLocationChoices[0].Prefab);
            SetHideFlags(ghostObject, HideFlags.HideAndDontSave);
            ghost = ghostObject.AddComponent<HouseBuilderGhost>();
            ghost.Initialize();
            statusMessage = "Place reinforcement locations in Scene view. Keep clicking to add more; press Esc when finished.";
            SceneView.lastActiveSceneView?.Focus();
            SceneView.RepaintAll();
            Repaint();
        }

        private void HandleReinforcementLocationPlacement(SceneView sceneView)
        {
            Event current = Event.current;
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            if (reinforcementTrigger == null)
            {
                ClearReinforcementLocationPlacement();
                DestroyGhost();
                statusMessage = "Reinforcement location placement stopped because its trigger no longer exists.";
                sceneView.Repaint();
                Repaint();
                return;
            }

            string previewName = reinforcementLocationChoices.Count > 1
                ? $"{reinforcementLocationChoices[0].DisplayName} + {reinforcementLocationChoices.Count - 1} alternative(s)"
                : reinforcementLocationChoices[0].DisplayName;
            DrawSceneBanner(sceneView, "Placing Reinforcement Locations", $"Preview: {previewName}    Left click: place another    Q/E: yaw    R/F: pitch    Z/C: roll    Esc: finish");
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                ClearReinforcementLocationPlacement();
                DestroyGhost();
                statusMessage = "Finished placing reinforcement locations.";
                current.Use();
                Repaint();
                return;
            }

            if (TryRotatePlacement(current))
            {
                current.Use();
            }

            HousePlacementProfile profile = reinforcementLocationChoices[0].Placement;
            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            bool hasSurface = TryRaycastSurface(ray, out RaycastHit hit);
            lastPlacement = HouseBuilderSnapUtility.Calculate(
                GetFallbackPosition(ray),
                requestedRotation,
                hasSurface,
                hit,
                profile,
                placementSettings,
                GatherNearbyBounds(),
                ray.direction);
            lastValidation = HouseBuilderPlacementValidator.Validate(
                lastPlacement.Position,
                lastPlacement.Rotation,
                profile,
                placementSettings,
                lastPlacement,
                hasSurface ? hit.collider.transform : null);

            ghostObject.transform.SetPositionAndRotation(lastPlacement.Position, lastPlacement.Rotation);
            ghost.SetValid(lastValidation.IsValid);
            Handles.color = lastValidation.IsValid ? new Color(0.1f, 0.9f, 1f) : Color.red;
            Handles.Label(lastPlacement.Position + Vector3.up * 0.4f, lastValidation.IsValid ? "Click to place spawn location" : lastValidation.Message);

            if (current.type == EventType.MouseDown && current.button == 0 && !current.alt)
            {
                if (lastValidation.IsValid)
                {
                    PlaceReinforcementLocation();
                }
                else
                {
                    statusMessage = lastValidation.Message;
                }

                current.Use();
            }

            sceneView.Repaint();
        }

        private void PlaceReinforcementLocation()
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(reinforcementLocationDefinition.Prefab, world.transform);
            Undo.RegisterCreatedObjectUndo(instance, "Place Reinforcement Location");
            instance.transform.SetPositionAndRotation(lastPlacement.Position, lastPlacement.Rotation);
            HouseBuilderObject builderObject = world.RegisterPlaceable(instance, reinforcementLocationDefinition);
            HouseReinforcementLocation location = instance.GetComponent<HouseReinforcementLocation>();
            location.Configure(reinforcementTrigger.InstanceId, reinforcementLocationChoices.Select(definition => definition.Id));
            instance.name = $"Reinforcement Location - {reinforcementLocationChoices[0].DisplayName}";
            Selection.activeObject = builderObject.gameObject;
            statusMessage = "Placed reinforcement location. Click to place another, or press Esc to finish.";
            MarkSceneDirty();
        }

        private void ClearReinforcementLocationPlacement()
        {
            placingReinforcementLocations = false;
            reinforcementTrigger = null;
            reinforcementLocationChoices.Clear();
        }

        private bool TryRotatePlacement(Event current)
        {
            if (current.type != EventType.KeyDown)
            {
                return false;
            }

            HousePlacementRotationAxis axis;
            float direction;
            switch (current.keyCode)
            {
                case KeyCode.Q:
                    axis = HousePlacementRotationAxis.Yaw;
                    direction = -1f;
                    break;
                case KeyCode.E:
                    axis = HousePlacementRotationAxis.Yaw;
                    direction = 1f;
                    break;
                case KeyCode.R:
                    axis = HousePlacementRotationAxis.Pitch;
                    direction = 1f;
                    break;
                case KeyCode.F:
                    axis = HousePlacementRotationAxis.Pitch;
                    direction = -1f;
                    break;
                case KeyCode.Z:
                    axis = HousePlacementRotationAxis.Roll;
                    direction = -1f;
                    break;
                case KeyCode.C:
                    axis = HousePlacementRotationAxis.Roll;
                    direction = 1f;
                    break;
                default:
                    return false;
            }

            requestedRotation = HouseBuilderEditorInteractionUtility.RotatePlacement(
                requestedRotation,
                axis,
                placementSettings.RotationStep,
                direction);
            return true;
        }

        private static void DrawEraseHighlight(HouseBuilderObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
            Bounds bounds = new(target.transform.position, Vector3.one * 0.25f);
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                bounds = Encapsulate(bounds, renderers[i].bounds, ref hasBounds);
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                bounds = Encapsulate(bounds, colliders[i].bounds, ref hasBounds);
            }

            Handles.color = new Color(1f, 0.2f, 0.1f);
            Handles.DrawWireCube(bounds.center, bounds.size);
            Handles.Label(bounds.center + Vector3.up * (bounds.extents.y + 0.2f), "Right click to erase");
        }

        private static Bounds Encapsulate(Bounds current, Bounds addition, ref bool hasBounds)
        {
            if (!hasBounds)
            {
                hasBounds = true;
                return addition;
            }

            current.Encapsulate(addition);
            return current;
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

        private void CreateGeometryAtSceneCenter()
        {
            ResolveWorld();
            if (world == null)
            {
                CreateWorld();
            }

            Vector3 basePosition = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            if (placementSettings.GridSnapping)
            {
                basePosition = HouseBuilderSnapUtility.SnapVector(basePosition, placementSettings.GridSize);
            }

            Vector3 size = HouseBuilderEditorInteractionUtility.SanitizeSize(geometrySize);
            CreateGeometryObject(basePosition + Vector3.up * size.y * 0.5f, Quaternion.identity, size);
        }

        private void CreateGeometryFromStroke()
        {
            if (!TryCalculateDrawShape(out Vector3 position, out Quaternion rotation, out Vector3 size))
            {
                return;
            }

            CreateGeometryObject(position, rotation, size);
            statusMessage = $"Created {FriendlyGeometryName(geometryKind)}. Draw another, or press Esc to stop.";
        }

        private void CreateGeometryObject(Vector3 position, Quaternion rotation, Vector3 size)
        {
            ResolveWorld();
            if (world == null)
            {
                CreateWorld();
            }

            Material defaultMaterial = catalog != null && catalog.Materials.Count > 0 ? catalog.Materials[0]?.Material : null;
            HouseGeometryDescriptor descriptor = new(
                geometryKind,
                HouseBuilderEditorInteractionUtility.SanitizeSize(size),
                openingWidth,
                openingHeight,
                sillHeight,
                stairCount);
            GameObject created = HouseGeometryFactory.Create(descriptor, defaultMaterial);
            created.name = FriendlyGeometryName(geometryKind);
            Undo.RegisterCreatedObjectUndo(created, $"Create {FriendlyGeometryName(geometryKind)}");
            created.transform.SetParent(world.transform, true);
            created.transform.SetPositionAndRotation(position, rotation);
            InitializeDefaultMaterialBindings(created);
            Selection.activeObject = created;
            statusMessage = $"Created {FriendlyGeometryName(geometryKind)}.";
            MarkSceneDirty();
        }

        private void InitializeDefaultMaterialBindings(GameObject created)
        {
            if (catalog == null || catalog.Materials.Count == 0 || catalog.Materials[0] == null)
            {
                return;
            }

            HouseBuilderMaterialController controller = created.GetComponent<HouseBuilderMaterialController>();
            for (int i = 0; i < HouseGeometryFactory.MaterialSlotCount; i++)
            {
                controller.SetBinding((HouseFaceRole)i, string.Empty, i, catalog.Materials[0].Id);
            }
        }

        private bool TryCalculateDrawShape(out Vector3 position, out Quaternion rotation, out Vector3 size)
        {
            Vector3 delta = geometryDrawCurrent - geometryDrawStart;
            bool wallLike = geometryKind is HouseGeometryKind.Wall or HouseGeometryKind.Doorway or HouseGeometryKind.Window;
            if (wallLike)
            {
                Vector3 direction = Vector3.ProjectOnPlane(delta, Vector3.up);
                float length = direction.magnitude;
                if (length < 0.05f)
                {
                    length = geometrySize.x;
                    direction = Vector3.right;
                }
                else
                {
                    direction /= length;
                }

                size = new Vector3(length, geometrySize.y, geometrySize.z);
                Vector3 end = geometryDrawStart + direction * length;
                position = (geometryDrawStart + end) * 0.5f + Vector3.up * size.y * 0.5f;
                rotation = Quaternion.LookRotation(Vector3.Cross(direction, Vector3.up), Vector3.up);
                return true;
            }

            float width = Mathf.Abs(delta.x);
            float depth = Mathf.Abs(delta.z);
            if (width < 0.05f || depth < 0.05f)
            {
                size = geometrySize;
                position = geometryDrawStart + Vector3.up * size.y * 0.5f;
            }
            else
            {
                size = new Vector3(width, geometrySize.y, depth);
                position = new Vector3(
                    (geometryDrawStart.x + geometryDrawCurrent.x) * 0.5f,
                    geometryDrawStart.y + size.y * 0.5f,
                    (geometryDrawStart.z + geometryDrawCurrent.z) * 0.5f);
            }

            rotation = Quaternion.identity;
            return true;
        }

        private bool TryGetConstructionPoint(Ray ray, bool useActivePlane, out Vector3 point)
        {
            if (useActivePlane)
            {
                Plane plane = new(Vector3.up, new Vector3(0f, geometryDrawPlaneY, 0f));
                if (!plane.Raycast(ray, out float distance))
                {
                    point = default;
                    return false;
                }

                point = ray.GetPoint(distance);
            }
            else
            {
                RaycastHit[] hits = Physics.RaycastAll(ray, 10000f, placementSettings.SurfaceMask, QueryTriggerInteraction.Ignore);
                Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
                bool found = false;
                point = default;
                for (int i = 0; i < hits.Length; i++)
                {
                    if (Vector3.Dot(hits[i].normal, Vector3.up) > 0.65f)
                    {
                        point = hits[i].point;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    Plane ground = new(Vector3.up, Vector3.zero);
                    if (!ground.Raycast(ray, out float distance))
                    {
                        return false;
                    }

                    point = ray.GetPoint(distance);
                }
            }

            if (placementSettings.GridSnapping)
            {
                point = HouseBuilderSnapUtility.SnapVector(point, placementSettings.GridSize);
            }

            return true;
        }

        private void ResizeGeometry(HouseGeometryObject geometry, Vector3 size)
        {
            Undo.RecordObject(geometry, "Resize House Builder Geometry");
            geometry.Resize(HouseBuilderEditorInteractionUtility.SanitizeSize(size));
            EditorUtility.SetDirty(geometry);
            statusMessage = $"Resized {geometry.name}.";
            MarkSceneDirty();
            SceneView.RepaintAll();
        }

        private void AssignMaterial(HouseBuilderFaceHit face, HouseMaterialDefinition material)
        {
            HouseBuilderMaterialController controller = face.Owner.GetComponent<HouseBuilderMaterialController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<HouseBuilderMaterialController>(face.Owner.gameObject);
            }

            Undo.RecordObject(controller, "Paint House Builder Face");
            controller.SetBinding(face.FaceRole, face.RendererPath, face.MaterialIndex, material.Id);
            controller.Apply(catalog);
            EditorUtility.SetDirty(controller);
            statusMessage = $"Painted {face.Owner.name} / {face.FaceRole} with {material.DisplayName}.";
            MarkSceneDirty();
        }

        private void AssignMaterialToWholeObject(HouseBuilderObject selected, HouseMaterialDefinition material)
        {
            if (selected == null || material == null)
            {
                return;
            }

            HouseBuilderMaterialController controller = selected.GetComponent<HouseBuilderMaterialController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<HouseBuilderMaterialController>(selected.gameObject);
            }

            Undo.RecordObject(controller, "Paint Entire House Builder Object");
            HouseGeometryObject geometry = selected.GetComponent<HouseGeometryObject>();
            if (geometry != null)
            {
                for (int i = 0; i < HouseGeometryFactory.MaterialSlotCount; i++)
                {
                    controller.SetBinding((HouseFaceRole)i, string.Empty, i, material.Id);
                }
            }
            else
            {
                Renderer[] renderers = selected.GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    Renderer renderer = renderers[rendererIndex];
                    string path = GetRelativePath(selected.transform, renderer.transform);
                    int materialCount = Mathf.Max(1, renderer.sharedMaterials.Length);
                    for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
                    {
                        controller.SetBinding(HouseFaceRole.Default, path, materialIndex, material.Id);
                    }
                }
            }

            controller.Apply(catalog);
            EditorUtility.SetDirty(controller);
            statusMessage = $"Painted all of {selected.name} with {material.DisplayName}.";
            MarkSceneDirty();
        }

        private HouseMaterialDefinition ResolveDraggedMaterial(Object dragged)
        {
            if (dragged is HouseMaterialDefinition definition)
            {
                return definition;
            }

            if (dragged is Material material)
            {
                HouseMaterialDefinition imported = HouseBuilderAssetInstaller.EnsureMaterialDefinition(material, catalog);
                if (imported == null)
                {
                    statusMessage = "That material could not be added to the current catalog.";
                }
                else
                {
                    statusMessage = $"Added {material.name} to the paint catalog.";
                    Repaint();
                }

                return imported;
            }

            return null;
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
            string path = EditorUtility.SaveFilePanel("Save Building File", Application.dataPath, world.DocumentName, "house.json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            HouseBuilderSaveSystem.SaveFile(path, world.CaptureDocument());
            statusMessage = $"Saved {Path.GetFileName(path)}.";
        }

        private void LoadWorld()
        {
            string path = EditorUtility.OpenFilePanel("Load Building File", Application.dataPath, "json");
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
            statusMessage = "Created a Builder World. Choose an asset or draw a shape.";
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

        private IEnumerable<Bounds> GatherGeometryBounds(HouseGeometryKind kind)
        {
            if (world == null)
            {
                yield break;
            }

            HouseGeometryObject[] geometryObjects = world.GetComponentsInChildren<HouseGeometryObject>(true);
            for (int geometryIndex = 0; geometryIndex < geometryObjects.Length; geometryIndex++)
            {
                HouseGeometryObject geometry = geometryObjects[geometryIndex];
                if (geometry == null || geometry.Descriptor.Kind != kind)
                {
                    continue;
                }

                Collider[] colliders = geometry.GetComponentsInChildren<Collider>(true);
                bool hasBounds = false;
                Bounds bounds = default;
                for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
                {
                    Collider collider = colliders[colliderIndex];
                    if (collider == null || !collider.enabled)
                    {
                        continue;
                    }

                    bounds = hasBounds ? Encapsulate(bounds, collider.bounds) : collider.bounds;
                    hasBounds = true;
                }

                if (hasBounds)
                {
                    yield return bounds;
                }
            }
        }

        private static Bounds Encapsulate(Bounds bounds, Bounds other)
        {
            bounds.Encapsulate(other.min);
            bounds.Encapsulate(other.max);
            return bounds;
        }

        private HouseGeometryObject GetSelectedGeometry()
        {
            return Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<HouseGeometryObject>()
                : null;
        }

        private static HouseBuilderObject GetSelectedBuilderObject()
        {
            return Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<HouseBuilderObject>()
                : null;
        }

        private string FriendlyCategoryName(string categoryId)
        {
            if (catalog != null)
            {
                for (int i = 0; i < catalog.Categories.Count; i++)
                {
                    HouseBuilderCategoryDefinition category = catalog.Categories[i];
                    if (category != null && category.Id == categoryId)
                    {
                        return category.DisplayName;
                    }
                }
            }

            return string.IsNullOrWhiteSpace(categoryId) ? "Uncategorized" : categoryId;
        }

        private bool MatchesCategoryFilter(string categoryId)
        {
            return string.IsNullOrEmpty(categoryFilter)
                || categoryFilter == "ai.*" && categoryId.StartsWith("ai.", StringComparison.Ordinal)
                || categoryId == categoryFilter;
        }

        private static string FriendlyGeometryName(HouseGeometryKind kind)
        {
            return kind == HouseGeometryKind.Cube ? "Block" : kind.ToString();
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
