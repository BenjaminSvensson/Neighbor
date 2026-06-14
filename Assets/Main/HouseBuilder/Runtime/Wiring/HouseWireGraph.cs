using System.Collections.Generic;
using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Neighbor/House Builder/Wire Graph")]
    public sealed class HouseWireGraph : MonoBehaviour
    {
        [SerializeField] private List<HouseWireConnection> connections = new();

        public IReadOnlyList<HouseWireConnection> Connections => connections;

        public bool TryConnect(HouseWireEndpoint outputEndpoint, HouseWirePortDefinition outputPort, HouseWireEndpoint inputEndpoint, HouseWirePortDefinition inputPort, out string error)
        {
            error = string.Empty;
            if (outputEndpoint == null || inputEndpoint == null || outputPort == null || inputPort == null)
            {
                error = "Both endpoints and ports are required.";
                return false;
            }

            if (outputPort.Direction != HouseWirePortDirection.Output || inputPort.Direction != HouseWirePortDirection.Input)
            {
                error = "Connections must run from an output to an input.";
                return false;
            }

            if (outputPort.SignalKind != HouseSignalKind.Any && inputPort.SignalKind != HouseSignalKind.Any && outputPort.SignalKind != inputPort.SignalKind)
            {
                error = "Signal types are incompatible.";
                return false;
            }

            HouseBuilderObject outputOwner = outputEndpoint.Owner;
            HouseBuilderObject inputOwner = inputEndpoint.Owner;
            if (outputOwner == null || inputOwner == null)
            {
                error = "Both endpoints must belong to builder objects.";
                return false;
            }

            if (outputPort.MaximumConnections > 0 && CountConnections(outputOwner.InstanceId, outputEndpoint.EndpointId, outputPort.Id, true) >= outputPort.MaximumConnections)
            {
                error = "The output has reached its connection limit.";
                return false;
            }

            if (inputPort.MaximumConnections > 0 && CountConnections(inputOwner.InstanceId, inputEndpoint.EndpointId, inputPort.Id, false) >= inputPort.MaximumConnections)
            {
                error = "The input has reached its connection limit.";
                return false;
            }

            bool duplicate = connections.Exists(connection =>
                connection.OutputObjectId == outputOwner.InstanceId
                && connection.OutputEndpointId == outputEndpoint.EndpointId
                && connection.OutputPortId == outputPort.Id
                && connection.InputObjectId == inputOwner.InstanceId
                && connection.InputEndpointId == inputEndpoint.EndpointId
                && connection.InputPortId == inputPort.Id);
            if (duplicate)
            {
                error = "That connection already exists.";
                return false;
            }

            connections.Add(new HouseWireConnection(outputOwner.InstanceId, outputEndpoint.EndpointId, outputPort.Id, inputOwner.InstanceId, inputEndpoint.EndpointId, inputPort.Id));
            return true;
        }

        public bool RemoveConnection(string connectionId) => connections.RemoveAll(connection => connection.Id == connectionId) > 0;

        public void SetConnections(IEnumerable<HouseWireConnection> values)
        {
            connections.Clear();
            if (values != null)
            {
                connections.AddRange(values);
            }
        }

        public bool TryResolve(HouseWireConnection connection, out HouseWireEndpoint outputEndpoint, out HouseWirePortDefinition outputPort, out HouseWireEndpoint inputEndpoint, out HouseWirePortDefinition inputPort)
        {
            outputEndpoint = FindEndpoint(connection.OutputObjectId, connection.OutputEndpointId);
            inputEndpoint = FindEndpoint(connection.InputObjectId, connection.InputEndpointId);
            outputPort = null;
            inputPort = null;
            return outputEndpoint != null
                && inputEndpoint != null
                && outputEndpoint.TryGetPort(connection.OutputPortId, out outputPort)
                && inputEndpoint.TryGetPort(connection.InputPortId, out inputPort);
        }

        private int CountConnections(string objectId, string endpointId, string portId, bool output)
        {
            return connections.FindAll(connection => output
                ? connection.OutputObjectId == objectId && connection.OutputEndpointId == endpointId && connection.OutputPortId == portId
                : connection.InputObjectId == objectId && connection.InputEndpointId == endpointId && connection.InputPortId == portId).Count;
        }

        private HouseWireEndpoint FindEndpoint(string objectId, string endpointId)
        {
            HouseBuilderObject[] objects = GetComponentsInChildren<HouseBuilderObject>(true);
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i].InstanceId != objectId)
                {
                    continue;
                }

                HouseWireEndpoint[] endpoints = objects[i].GetComponentsInChildren<HouseWireEndpoint>(true);
                for (int endpointIndex = 0; endpointIndex < endpoints.Length; endpointIndex++)
                {
                    if (endpoints[endpointIndex].EndpointId == endpointId)
                    {
                        return endpoints[endpointIndex];
                    }
                }
            }

            return null;
        }

        private void OnEnable() => HouseWireEndpoint.OutputEmitted += HandleOutput;
        private void OnDisable() => HouseWireEndpoint.OutputEmitted -= HandleOutput;

        private void HandleOutput(HouseWireEndpoint endpoint, string portId, HouseSignal signal)
        {
            HouseBuilderObject owner = endpoint != null ? endpoint.Owner : null;
            if (owner == null || !owner.transform.IsChildOf(transform))
            {
                return;
            }

            for (int i = 0; i < connections.Count; i++)
            {
                HouseWireConnection connection = connections[i];
                if (connection.OutputObjectId == owner.InstanceId
                    && connection.OutputEndpointId == endpoint.EndpointId
                    && connection.OutputPortId == portId)
                {
                    FindEndpoint(connection.InputObjectId, connection.InputEndpointId)?.Receive(connection.InputPortId, signal);
                }
            }
        }
    }
}
