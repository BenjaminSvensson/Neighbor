using System;
using UnityEngine;
using UnityEngine.Events;

namespace Neighbor.Main.HouseBuilder
{
    public enum HouseSignalKind
    {
        Any,
        Pulse,
        Bool,
        Float,
        String
    }

    public enum HouseWirePortDirection
    {
        Input,
        Output
    }

    [Serializable]
    public sealed class HouseSignal
    {
        [SerializeField] private HouseSignalKind kind = HouseSignalKind.Pulse;
        [SerializeField] private bool boolValue;
        [SerializeField] private float floatValue;
        [SerializeField] private string stringValue;

        public HouseSignalKind Kind => kind;
        public bool BoolValue => boolValue;
        public float FloatValue => floatValue;
        public string StringValue => stringValue;

        public static HouseSignal Pulse() => new() { kind = HouseSignalKind.Pulse };
        public static HouseSignal Bool(bool value) => new() { kind = HouseSignalKind.Bool, boolValue = value };
        public static HouseSignal Float(float value) => new() { kind = HouseSignalKind.Float, floatValue = value };
        public static HouseSignal String(string value) => new() { kind = HouseSignalKind.String, stringValue = value ?? string.Empty };
    }

    [Serializable]
    public sealed class HouseSignalEvent : UnityEvent<HouseSignal>
    {
    }

    [Serializable]
    public sealed class HouseWirePortDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName = "Port";
        [SerializeField] private HouseWirePortDirection direction;
        [SerializeField] private HouseSignalKind signalKind = HouseSignalKind.Any;
        [SerializeField, Min(0)] private int maximumConnections;
        [SerializeField] private Vector3 visualOffset;
        [SerializeField] private HouseSignalEvent onSignalReceived = new();

        public string Id => id;
        public string DisplayName => displayName;
        public HouseWirePortDirection Direction => direction;
        public HouseSignalKind SignalKind => signalKind;
        public int MaximumConnections => maximumConnections;
        public Vector3 VisualOffset => visualOffset;
        public HouseSignalEvent OnSignalReceived => onSignalReceived;

        public HouseWirePortDefinition(
            string displayName,
            HouseWirePortDirection direction,
            HouseSignalKind signalKind = HouseSignalKind.Any,
            int maximumConnections = 0,
            Vector3 visualOffset = default)
        {
            id = Guid.NewGuid().ToString("N");
            this.displayName = displayName;
            this.direction = direction;
            this.signalKind = signalKind;
            this.maximumConnections = Mathf.Max(0, maximumConnections);
            this.visualOffset = visualOffset;
        }

        public void EnsureIdentity()
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                id = Guid.NewGuid().ToString("N");
            }
        }

        public void SetIdentity(string requestedId)
        {
            if (!string.IsNullOrWhiteSpace(requestedId))
            {
                id = requestedId;
            }

            EnsureIdentity();
        }
    }

    [Serializable]
    public sealed class HouseWireConnection
    {
        [SerializeField] private string id;
        [SerializeField] private string outputObjectId;
        [SerializeField] private string outputEndpointId;
        [SerializeField] private string outputPortId;
        [SerializeField] private string inputObjectId;
        [SerializeField] private string inputEndpointId;
        [SerializeField] private string inputPortId;

        public string Id => id;
        public string OutputObjectId => outputObjectId;
        public string OutputEndpointId => outputEndpointId;
        public string OutputPortId => outputPortId;
        public string InputObjectId => inputObjectId;
        public string InputEndpointId => inputEndpointId;
        public string InputPortId => inputPortId;

        public HouseWireConnection(
            string outputObjectId,
            string outputEndpointId,
            string outputPortId,
            string inputObjectId,
            string inputEndpointId,
            string inputPortId,
            string id = null)
        {
            this.id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            this.outputObjectId = outputObjectId;
            this.outputEndpointId = outputEndpointId;
            this.outputPortId = outputPortId;
            this.inputObjectId = inputObjectId;
            this.inputEndpointId = inputEndpointId;
            this.inputPortId = inputPortId;
        }
    }
}
