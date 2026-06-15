using System;
using System.Collections.Generic;
using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HouseWireGraph))]
    [AddComponentMenu("Neighbor/House Builder/Builder World")]
    public sealed class HouseBuilderWorld : MonoBehaviour
    {
        [SerializeField] private string documentName = "House";
        [SerializeField] private HouseBuilderCatalog catalog;
        [SerializeField] private HouseWireGraph wireGraph;

        public string DocumentName => documentName;
        public HouseBuilderCatalog Catalog => catalog;
        public HouseWireGraph WireGraph => ResolveWireGraph();

        public void Configure(HouseBuilderCatalog sourceCatalog, string name = null)
        {
            catalog = sourceCatalog;
            if (!string.IsNullOrWhiteSpace(name))
            {
                documentName = name;
            }

            ResolveWireGraph();
        }

        public GameObject CreatePlaceable(HousePlaceableDefinition definition, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (definition == null || definition.Prefab == null)
            {
                return null;
            }

            GameObject instance = Instantiate(definition.Prefab, position, rotation, parent != null ? parent : transform);
            RegisterPlaceable(instance, definition);
            return instance;
        }

        public HouseBuilderObject RegisterPlaceable(GameObject instance, HousePlaceableDefinition definition, string requestedId = null)
        {
            if (instance == null)
            {
                return null;
            }

            HouseBuilderObject builderObject = instance.GetComponent<HouseBuilderObject>();
            if (builderObject == null)
            {
                builderObject = instance.AddComponent<HouseBuilderObject>();
            }

            builderObject.Initialize(definition != null ? definition.Id : string.Empty, definition != null ? definition.CategoryId : HouseBuilderCategories.Prop, requestedId);
            HouseGeometryObject[] geometryObjects = instance.GetComponentsInChildren<HouseGeometryObject>(true);
            for (int i = 0; i < geometryObjects.Length; i++)
            {
                geometryObjects[i].PrepareForPlacement();
            }

            ApplyWirePorts(instance, definition);
            ResolveWireGraph().InvalidateEndpointCache();
            instance.GetComponent<HouseBuilderMaterialController>()?.Apply(catalog);
            return builderObject;
        }

        public bool TryCreateWallOpening(GameObject placedObject, HousePlaceableDefinition definition, Collider placementSurface)
        {
            if (placedObject == null || definition?.WallOpening == null || !definition.WallOpening.Enabled || placementSurface == null)
            {
                return false;
            }

            HouseGeometryObject wall = placementSurface.GetComponentInParent<HouseGeometryObject>();
            HouseBuilderObject owner = placedObject.GetComponent<HouseBuilderObject>();
            if (wall == null || wall.Descriptor.Kind != HouseGeometryKind.Wall || owner == null)
            {
                return false;
            }

            if (definition.WallOpening.PlaceInsideWallOpening)
            {
                placedObject.transform.position = wall.CenterOnWallMidplane(placedObject.transform.position);
            }

            HouseWallOpeningLink link = placedObject.GetComponent<HouseWallOpeningLink>();
            if (link == null)
            {
                link = placedObject.AddComponent<HouseWallOpeningLink>();
            }

            link.Initialize(wall, definition.WallOpening);
            return true;
        }

        public HouseBuilderDocument CaptureDocument()
        {
            List<HouseBuilderObjectData> objects = new();
            HouseBuilderObject[] builderObjects = GetComponentsInChildren<HouseBuilderObject>(true);
            for (int i = 0; i < builderObjects.Length; i++)
            {
                HouseBuilderObject builderObject = builderObjects[i];
                builderObject.EnsureIdentity();
                HouseGeometryObject geometry = builderObject.GetComponent<HouseGeometryObject>();
                HouseBuilderMaterialController materials = builderObject.GetComponent<HouseBuilderMaterialController>();
                List<HouseBuilderComponentState> states = CaptureComponentStates(builderObject);
                objects.Add(new HouseBuilderObjectData(
                    builderObject,
                    transform,
                    geometry != null ? geometry.Descriptor : null,
                    materials != null ? materials.Bindings : null,
                    states));
            }

            return new HouseBuilderDocument(documentName, objects, ResolveWireGraph().Connections);
        }

        public string SaveToJson(bool prettyPrint = true)
        {
            return HouseBuilderSaveSystem.ToJson(CaptureDocument(), prettyPrint);
        }

        public void LoadFromJson(string json)
        {
            LoadDocument(HouseBuilderSaveSystem.FromJson(json));
        }

        public void LoadDocument(HouseBuilderDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            documentName = document.DisplayName;
            Clear();
            Dictionary<string, HouseBuilderObject> loadedById = new();
            for (int i = 0; i < document.Objects.Count; i++)
            {
                HouseBuilderObjectData data = document.Objects[i];
                HouseBuilderObject loaded = CreateFromData(data);
                if (loaded != null)
                {
                    loadedById[loaded.InstanceId] = loaded;
                }
            }

            ResolveWireGraph().SetConnections(document.Connections);
            RestoreOpeningLinks(loadedById);
        }

        public void Clear()
        {
            HouseBuilderObject[] objects = GetComponentsInChildren<HouseBuilderObject>(true);
            for (int i = objects.Length - 1; i >= 0; i--)
            {
                if (Application.isPlaying)
                {
                    Destroy(objects[i].gameObject);
                }
                else
                {
                    DestroyImmediate(objects[i].gameObject);
                }
            }

            ResolveWireGraph().SetConnections(Array.Empty<HouseWireConnection>());
        }

        private HouseBuilderObject CreateFromData(HouseBuilderObjectData data)
        {
            GameObject instance = null;
            HousePlaceableDefinition definition = null;
            if (!string.IsNullOrWhiteSpace(data.DefinitionId) && catalog != null)
            {
                catalog.TryGetPlaceable(data.DefinitionId, out definition);
            }

            if (definition != null && definition.Prefab != null)
            {
                instance = Instantiate(definition.Prefab, transform);
            }
            else if (data.Geometry != null)
            {
                instance = HouseGeometryFactory.Create(data.Geometry);
                instance.transform.SetParent(transform, false);
            }
            else
            {
                instance = new GameObject(data.Name);
                instance.transform.SetParent(transform, false);
            }

            instance.name = data.Name;
            instance.transform.localPosition = data.LocalPosition;
            instance.transform.localRotation = data.LocalRotation;
            instance.transform.localScale = data.LocalScale;

            if (data.Geometry != null)
            {
                HouseGeometryObject geometry = instance.GetComponent<HouseGeometryObject>();
                if (geometry == null)
                {
                    geometry = instance.AddComponent<HouseGeometryObject>();
                }

                geometry.Configure(data.Geometry);
                geometry.PrepareForPlacement();
            }

            HouseBuilderObject builderObject = instance.GetComponent<HouseBuilderObject>();
            if (builderObject == null)
            {
                builderObject = instance.AddComponent<HouseBuilderObject>();
            }

            builderObject.Initialize(data.DefinitionId, data.CategoryId, data.InstanceId);
            builderObject.SetProperties(data.Properties);
            ApplyWirePorts(instance, definition);
            ResolveWireGraph().InvalidateEndpointCache();

            HouseBuilderMaterialController materialController = instance.GetComponent<HouseBuilderMaterialController>();
            if (data.Materials.Count > 0 && materialController == null)
            {
                materialController = instance.AddComponent<HouseBuilderMaterialController>();
            }

            if (materialController != null)
            {
                materialController.SetBindings(data.Materials);
                materialController.Apply(catalog);
            }

            RestoreComponentStates(instance, data.ComponentStates);
            return builderObject;
        }

        private void RestoreOpeningLinks(Dictionary<string, HouseBuilderObject> objectsById)
        {
            HouseGeometryObject[] geometryObjects = GetComponentsInChildren<HouseGeometryObject>(true);
            for (int geometryIndex = 0; geometryIndex < geometryObjects.Length; geometryIndex++)
            {
                HouseGeometryObject wall = geometryObjects[geometryIndex];
                if (wall.Descriptor.Kind != HouseGeometryKind.Wall)
                {
                    continue;
                }

                for (int openingIndex = 0; openingIndex < wall.Descriptor.WallOpenings.Count; openingIndex++)
                {
                    HouseWallOpeningData opening = wall.Descriptor.WallOpenings[openingIndex];
                    if (opening == null
                        || !objectsById.TryGetValue(opening.OwnerObjectId, out HouseBuilderObject owner)
                        || catalog == null
                        || !catalog.TryGetPlaceable(owner.DefinitionId, out HousePlaceableDefinition definition)
                        || !definition.WallOpening.Enabled)
                    {
                        continue;
                    }

                    HouseWallOpeningLink link = owner.GetComponent<HouseWallOpeningLink>();
                    if (link == null)
                    {
                        link = owner.gameObject.AddComponent<HouseWallOpeningLink>();
                    }

                    link.Initialize(wall, definition.WallOpening);
                }
            }
        }

        private HouseWireGraph ResolveWireGraph()
        {
            if (wireGraph == null)
            {
                wireGraph = GetComponent<HouseWireGraph>();
            }

            if (wireGraph == null)
            {
                wireGraph = gameObject.AddComponent<HouseWireGraph>();
            }

            return wireGraph;
        }

        private static void ApplyWirePorts(GameObject instance, HousePlaceableDefinition definition)
        {
            if (instance == null || definition == null || definition.WirePorts.Count == 0)
            {
                return;
            }

            HouseWireEndpoint endpoint = instance.GetComponent<HouseWireEndpoint>();
            if (endpoint == null)
            {
                endpoint = instance.AddComponent<HouseWireEndpoint>();
            }

            endpoint.ConfigureIdentity($"{definition.Id}.endpoint");
            HouseWireInputRelay inputRelay = null;
            for (int i = 0; i < definition.WirePorts.Count; i++)
            {
                HouseWirePortTemplate template = definition.WirePorts[i];
                if (template == null || endpoint.TryGetPort(template.Id, out _))
                {
                    continue;
                }

                HouseWirePortDefinition port = endpoint.AddPort(
                    template.DisplayName,
                    template.Direction,
                    template.SignalKind,
                    template.MaximumConnections,
                    template.VisualOffset,
                    template.Id);
                if (template.Direction == HouseWirePortDirection.Input)
                {
                    inputRelay ??= instance.GetComponent<HouseWireInputRelay>() ?? instance.AddComponent<HouseWireInputRelay>();
                    port.OnSignalReceived.AddListener(inputRelay.Receive);
                }
                else
                {
                    HouseWireOutputRelay outputRelay = instance.AddComponent<HouseWireOutputRelay>();
                    outputRelay.Configure(endpoint, port.Id);
                }
            }
        }

        private static List<HouseBuilderComponentState> CaptureComponentStates(HouseBuilderObject builderObject)
        {
            List<HouseBuilderComponentState> states = new();
            MonoBehaviour[] behaviours = builderObject.GetComponentsInChildren<MonoBehaviour>(true);
            Dictionary<string, int> occurrences = new();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IHouseBuilderSerializable serializable)
                {
                    states.Add(new HouseBuilderComponentState(serializable.HouseBuilderTypeId, serializable.CaptureHouseBuilderState()));
                    continue;
                }

                if (IsBuilderInfrastructure(behaviours[i]))
                {
                    continue;
                }

                string path = GetRelativePath(builderObject.transform, behaviours[i].transform);
                string key = $"{path}|{behaviours[i].GetType().FullName}";
                occurrences.TryGetValue(key, out int occurrence);
                occurrences[key] = occurrence + 1;
                states.Add(new HouseBuilderComponentState(
                    behaviours[i].GetType().FullName,
                    JsonUtility.ToJson(behaviours[i]),
                    path,
                    occurrence,
                    false));
            }

            return states;
        }

        private static void RestoreComponentStates(GameObject root, IReadOnlyList<HouseBuilderComponentState> states)
        {
            if (states == null || states.Count == 0)
            {
                return;
            }

            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int stateIndex = 0; stateIndex < states.Count; stateIndex++)
            {
                HouseBuilderComponentState state = states[stateIndex];
                if (!state.ExplicitContract)
                {
                    RestoreAutomaticState(root.transform, behaviours, state);
                    continue;
                }

                for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
                {
                    if (behaviours[behaviourIndex] is IHouseBuilderSerializable serializable
                        && serializable.HouseBuilderTypeId == state.TypeId)
                    {
                        serializable.RestoreHouseBuilderState(state.Json);
                        break;
                    }
                }
            }
        }

        private static void RestoreAutomaticState(Transform root, MonoBehaviour[] behaviours, HouseBuilderComponentState state)
        {
            int occurrence = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null
                    || behaviour.GetType().FullName != state.TypeId
                    || GetRelativePath(root, behaviour.transform) != state.ComponentPath)
                {
                    continue;
                }

                if (occurrence++ == state.Occurrence)
                {
                    JsonUtility.FromJsonOverwrite(state.Json, behaviour);
                    return;
                }
            }
        }

        private static bool IsBuilderInfrastructure(MonoBehaviour behaviour)
        {
            return behaviour is HouseBuilderObject
                or HouseGeometryObject
                or HouseBuilderMaterialController
                or HouseWallOpeningLink
                or HouseWireEndpoint
                or HouseWireGraph
                or HouseBuilderWorld;
        }

        private static string GetRelativePath(Transform root, Transform target)
        {
            if (root == target)
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

    }
}
