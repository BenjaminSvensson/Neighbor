using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    [Serializable]
    public sealed class HouseBuilderDocument
    {
        [SerializeField] private string format = HouseBuilderSaveSystem.FormatId;
        [SerializeField] private int version = HouseBuilderSaveSystem.CurrentVersion;
        [SerializeField] private string documentId = Guid.NewGuid().ToString("N");
        [SerializeField] private string displayName = "House";
        [SerializeField] private long savedAtUtcTicks;
        [SerializeField] private List<HouseBuilderObjectData> objects = new();
        [SerializeField] private List<HouseWireConnection> connections = new();

        public string Format => format;
        public int Version => version;
        public string DocumentId => documentId;
        public string DisplayName => displayName;
        public long SavedAtUtcTicks => savedAtUtcTicks;
        public IReadOnlyList<HouseBuilderObjectData> Objects => objects;
        public IReadOnlyList<HouseWireConnection> Connections => connections;

        public HouseBuilderDocument(string displayName, IEnumerable<HouseBuilderObjectData> objects, IEnumerable<HouseWireConnection> connections)
        {
            this.displayName = string.IsNullOrWhiteSpace(displayName) ? "House" : displayName;
            savedAtUtcTicks = DateTime.UtcNow.Ticks;
            if (objects != null)
            {
                this.objects.AddRange(objects);
            }

            if (connections != null)
            {
                this.connections.AddRange(connections);
            }
        }
    }

    [Serializable]
    public sealed class HouseBuilderObjectData
    {
        [SerializeField] private string instanceId;
        [SerializeField] private string definitionId;
        [SerializeField] private string categoryId;
        [SerializeField] private string name;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Quaternion localRotation;
        [SerializeField] private Vector3 localScale;
        [SerializeField] private HouseGeometryDescriptor geometry;
        [SerializeField] private List<HouseMaterialBinding> materials = new();
        [SerializeField] private List<HouseBuilderProperty> properties = new();
        [SerializeField] private List<HouseBuilderComponentState> componentStates = new();

        public string InstanceId => instanceId;
        public string DefinitionId => definitionId;
        public string CategoryId => categoryId;
        public string Name => name;
        public Vector3 LocalPosition => localPosition;
        public Quaternion LocalRotation => localRotation;
        public Vector3 LocalScale => localScale;
        public HouseGeometryDescriptor Geometry => geometry;
        public IReadOnlyList<HouseMaterialBinding> Materials => materials;
        public IReadOnlyList<HouseBuilderProperty> Properties => properties;
        public IReadOnlyList<HouseBuilderComponentState> ComponentStates => componentStates;

        public HouseBuilderObjectData(
            HouseBuilderObject source,
            Transform root,
            HouseGeometryDescriptor geometry,
            IEnumerable<HouseMaterialBinding> materials,
            IEnumerable<HouseBuilderComponentState> componentStates)
        {
            instanceId = source.InstanceId;
            definitionId = source.DefinitionId;
            categoryId = source.CategoryId;
            name = source.name;
            localPosition = root.InverseTransformPoint(source.transform.position);
            localRotation = Quaternion.Inverse(root.rotation) * source.transform.rotation;
            localScale = Divide(source.transform.lossyScale, root.lossyScale);
            this.geometry = geometry;
            properties.AddRange(source.Properties);
            if (materials != null)
            {
                this.materials.AddRange(materials);
            }

            if (componentStates != null)
            {
                this.componentStates.AddRange(componentStates);
            }
        }

        private static Vector3 Divide(Vector3 value, Vector3 divisor)
        {
            return new Vector3(
                Mathf.Abs(divisor.x) > 0.0001f ? value.x / divisor.x : value.x,
                Mathf.Abs(divisor.y) > 0.0001f ? value.y / divisor.y : value.y,
                Mathf.Abs(divisor.z) > 0.0001f ? value.z / divisor.z : value.z);
        }
    }

    [Serializable]
    public sealed class HouseBuilderComponentState
    {
        [SerializeField] private string typeId;
        [SerializeField] private string componentPath;
        [SerializeField, Min(0)] private int occurrence;
        [SerializeField] private bool explicitContract;
        [SerializeField] private string json;

        public string TypeId => typeId;
        public string ComponentPath => componentPath;
        public int Occurrence => occurrence;
        public bool ExplicitContract => explicitContract;
        public string Json => json;

        public HouseBuilderComponentState(string typeId, string json, string componentPath = "", int occurrence = 0, bool explicitContract = true)
        {
            this.typeId = typeId;
            this.componentPath = componentPath ?? string.Empty;
            this.occurrence = Mathf.Max(0, occurrence);
            this.explicitContract = explicitContract;
            this.json = json;
        }
    }

    public static class HouseBuilderSaveSystem
    {
        public const string FormatId = "neighbor.house-builder";
        public const int CurrentVersion = 1;

        public static string ToJson(HouseBuilderDocument document, bool prettyPrint = true)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            return JsonUtility.ToJson(document, prettyPrint);
        }

        public static HouseBuilderDocument FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Save data is empty.", nameof(json));
            }

            HouseBuilderDocument document = JsonUtility.FromJson<HouseBuilderDocument>(json);
            if (document == null || document.Format != FormatId)
            {
                throw new InvalidDataException("The file is not a Neighbor House Builder document.");
            }

            if (document.Version > CurrentVersion)
            {
                throw new InvalidDataException($"Save version {document.Version} is newer than supported version {CurrentVersion}.");
            }

            return document;
        }

        public static void SaveFile(string path, HouseBuilderDocument document)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, ToJson(document));
        }

        public static HouseBuilderDocument LoadFile(string path)
        {
            return FromJson(File.ReadAllText(path));
        }
    }
}
