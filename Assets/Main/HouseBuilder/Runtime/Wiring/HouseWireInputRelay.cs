using UnityEngine;
using UnityEngine.Events;

namespace Neighbor.Main.HouseBuilder
{
    [AddComponentMenu("Neighbor/House Builder/Wiring/Signal Input Relay")]
    public sealed class HouseWireInputRelay : MonoBehaviour
    {
        [SerializeField] private UnityEvent onPulse = new();
        [SerializeField] private UnityEvent<bool> onBool = new();
        [SerializeField] private UnityEvent<float> onFloat = new();
        [SerializeField] private UnityEvent<string> onString = new();

        public void Receive(HouseSignal signal)
        {
            if (signal == null)
            {
                return;
            }

            switch (signal.Kind)
            {
                case HouseSignalKind.Pulse:
                    onPulse.Invoke();
                    break;
                case HouseSignalKind.Bool:
                    onBool.Invoke(signal.BoolValue);
                    break;
                case HouseSignalKind.Float:
                    onFloat.Invoke(signal.FloatValue);
                    break;
                case HouseSignalKind.String:
                    onString.Invoke(signal.StringValue);
                    break;
            }

            NotifySignalReceivers(signal);
        }

        private void NotifySignalReceivers(HouseSignal signal)
        {
            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || ReferenceEquals(behaviour, this) || !behaviour.isActiveAndEnabled)
                {
                    continue;
                }

                if (behaviour is IHouseWireSignalReceiver receiver)
                {
                    receiver.ReceiveHouseWireSignal(signal);
                }
            }
        }
    }
}
