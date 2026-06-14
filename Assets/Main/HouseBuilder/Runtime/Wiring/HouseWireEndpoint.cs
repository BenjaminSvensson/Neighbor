using System;
using System.Collections.Generic;
using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Neighbor/House Builder/Wire Endpoint")]
    public sealed class HouseWireEndpoint : MonoBehaviour
    {
        public static event Action<HouseWireEndpoint, string, HouseSignal> OutputEmitted;
        public event Action<string, HouseSignal> InputReceived;

        [SerializeField] private string endpointId;
        [SerializeField] private List<HouseWirePortDefinition> ports = new();

        public string EndpointId => endpointId;
        public IReadOnlyList<HouseWirePortDefinition> Ports => ports;
        public HouseBuilderObject Owner => GetComponentInParent<HouseBuilderObject>();

        public HouseWirePortDefinition AddPort(
            string displayName,
            HouseWirePortDirection direction,
            HouseSignalKind signalKind = HouseSignalKind.Any,
            int maximumConnections = 0,
            Vector3 visualOffset = default,
            string requestedId = null)
        {
            HouseWirePortDefinition port = new(displayName, direction, signalKind, maximumConnections, visualOffset);
            port.SetIdentity(requestedId);
            ports.Add(port);
            return port;
        }

        public void ConfigureIdentity(string requestedId)
        {
            if (!string.IsNullOrWhiteSpace(requestedId))
            {
                endpointId = requestedId;
            }

            EnsureIdentity();
        }

        public bool TryGetPort(string portId, out HouseWirePortDefinition port)
        {
            port = ports.Find(candidate => candidate != null && candidate.Id == portId);
            return port != null;
        }

        public Vector3 GetPortWorldPosition(HouseWirePortDefinition port)
        {
            return transform.TransformPoint(port != null ? port.VisualOffset : Vector3.zero);
        }

        public void Emit(string portId, HouseSignal signal)
        {
            if (!TryGetPort(portId, out HouseWirePortDefinition port) || port.Direction != HouseWirePortDirection.Output)
            {
                return;
            }

            signal ??= HouseSignal.Pulse();
            if (port.SignalKind != HouseSignalKind.Any && port.SignalKind != signal.Kind)
            {
                return;
            }

            OutputEmitted?.Invoke(this, portId, signal);
        }

        public void EmitPulse(string portId) => Emit(portId, HouseSignal.Pulse());
        public void EmitBool(string portId, bool value) => Emit(portId, HouseSignal.Bool(value));

        public void Receive(string portId, HouseSignal signal)
        {
            if (!TryGetPort(portId, out HouseWirePortDefinition port) || port.Direction != HouseWirePortDirection.Input)
            {
                return;
            }

            if (port.SignalKind != HouseSignalKind.Any && signal != null && port.SignalKind != signal.Kind)
            {
                return;
            }

            port.OnSignalReceived?.Invoke(signal);
            InputReceived?.Invoke(portId, signal);
        }

        public void EnsureIdentity()
        {
            if (string.IsNullOrWhiteSpace(endpointId))
            {
                endpointId = Guid.NewGuid().ToString("N");
            }

            for (int i = 0; i < ports.Count; i++)
            {
                ports[i]?.EnsureIdentity();
            }
        }

        private void Awake() => EnsureIdentity();
        private void OnValidate() => EnsureIdentity();
    }
}
