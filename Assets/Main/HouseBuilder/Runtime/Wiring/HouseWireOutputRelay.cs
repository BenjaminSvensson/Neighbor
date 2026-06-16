using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    [AddComponentMenu("Neighbor/House Builder/Wiring/Signal Output Relay")]
    public sealed class HouseWireOutputRelay : MonoBehaviour
    {
        [SerializeField] private HouseWireEndpoint endpoint;
        [SerializeField] private string outputPortId;

        private IHouseWireSignalSource[] subscribedSources = System.Array.Empty<IHouseWireSignalSource>();

        public void Configure(HouseWireEndpoint target, string portId)
        {
            endpoint = target;
            outputPortId = portId;
            RefreshSignalSources();
        }

        public void EmitPulse() => ResolveEndpoint()?.Emit(outputPortId, HouseSignal.Pulse());
        public void EmitTrue() => ResolveEndpoint()?.Emit(outputPortId, HouseSignal.Bool(true));
        public void EmitFalse() => ResolveEndpoint()?.Emit(outputPortId, HouseSignal.Bool(false));
        public void EmitBool(bool value) => ResolveEndpoint()?.Emit(outputPortId, HouseSignal.Bool(value));
        public void EmitFloat(float value) => ResolveEndpoint()?.Emit(outputPortId, HouseSignal.Float(value));
        public void EmitString(string value) => ResolveEndpoint()?.Emit(outputPortId, HouseSignal.String(value));

        public bool IsConfiguredFor(HouseWireEndpoint target, string portId)
        {
            return ResolveEndpoint() == target && outputPortId == portId;
        }

        private HouseWireEndpoint ResolveEndpoint()
        {
            endpoint ??= GetComponent<HouseWireEndpoint>();
            return endpoint;
        }

        private void OnEnable() => RefreshSignalSources();
        private void OnDisable() => ClearSignalSources();

        private void RefreshSignalSources()
        {
            ClearSignalSources();

            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            System.Collections.Generic.List<IHouseWireSignalSource> sources = new();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IHouseWireSignalSource source)
                {
                    source.HouseWireSignalEmitted += EmitSignal;
                    sources.Add(source);
                }
            }

            subscribedSources = sources.ToArray();
        }

        private void ClearSignalSources()
        {
            for (int i = 0; i < subscribedSources.Length; i++)
            {
                if (subscribedSources[i] != null)
                {
                    subscribedSources[i].HouseWireSignalEmitted -= EmitSignal;
                }
            }

            subscribedSources = System.Array.Empty<IHouseWireSignalSource>();
        }

        private void EmitSignal(HouseSignal signal)
        {
            ResolveEndpoint()?.Emit(outputPortId, signal);
        }
    }
}
