using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    [AddComponentMenu("Neighbor/House Builder/Wiring/Signal Output Relay")]
    public sealed class HouseWireOutputRelay : MonoBehaviour
    {
        [SerializeField] private HouseWireEndpoint endpoint;
        [SerializeField] private string outputPortId;

        public void Configure(HouseWireEndpoint target, string portId)
        {
            endpoint = target;
            outputPortId = portId;
        }

        public void EmitPulse() => ResolveEndpoint()?.Emit(outputPortId, HouseSignal.Pulse());
        public void EmitTrue() => ResolveEndpoint()?.Emit(outputPortId, HouseSignal.Bool(true));
        public void EmitFalse() => ResolveEndpoint()?.Emit(outputPortId, HouseSignal.Bool(false));
        public void EmitBool(bool value) => ResolveEndpoint()?.Emit(outputPortId, HouseSignal.Bool(value));
        public void EmitFloat(float value) => ResolveEndpoint()?.Emit(outputPortId, HouseSignal.Float(value));
        public void EmitString(string value) => ResolveEndpoint()?.Emit(outputPortId, HouseSignal.String(value));

        private HouseWireEndpoint ResolveEndpoint()
        {
            endpoint ??= GetComponent<HouseWireEndpoint>();
            return endpoint;
        }
    }
}
