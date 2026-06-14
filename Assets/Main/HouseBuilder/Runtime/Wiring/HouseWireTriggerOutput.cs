using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    [AddComponentMenu("Neighbor/House Builder/Wiring/Trigger Output")]
    [RequireComponent(typeof(Collider))]
    public sealed class HouseWireTriggerOutput : MonoBehaviour
    {
        [SerializeField] private HouseWireEndpoint endpoint;
        [SerializeField] private string outputPortId;
        [SerializeField] private LayerMask triggerLayers = ~0;
        [SerializeField] private bool emitOnce;
        private bool emitted;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            endpoint ??= GetComponent<HouseWireEndpoint>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if ((!emitOnce || !emitted) && (triggerLayers.value & 1 << other.gameObject.layer) != 0)
            {
                endpoint?.Emit(outputPortId, HouseSignal.Pulse());
                emitted = true;
            }
        }
    }
}
