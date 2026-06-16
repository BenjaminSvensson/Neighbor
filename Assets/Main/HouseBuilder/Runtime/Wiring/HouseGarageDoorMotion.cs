using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Neighbor/House Builder/Wiring/Garage Door Motion")]
    public sealed class HouseGarageDoorMotion : MonoBehaviour, IHouseWireSignalReceiver
    {
        private const string DefaultPanelName = "Door Panel";

        [SerializeField] private Transform doorPanel;
        [SerializeField] private Vector3 closedLocalPosition = new(0f, 1.25f, 0f);
        [SerializeField] private Vector3 openLocalOffset = new(0f, 2.35f, 0f);
        [SerializeField, Min(0.01f)] private float travelDuration = 0.8f;
        [SerializeField] private bool startsOpen;

        private float progress;
        private float targetProgress;
        private bool initialized;

        public bool IsOpen => targetProgress >= 1f;

        public void Configure(Transform panel, Vector3 closedPosition, Vector3 openOffset, float duration, bool initiallyOpen)
        {
            doorPanel = panel;
            closedLocalPosition = closedPosition;
            openLocalOffset = openOffset;
            travelDuration = Mathf.Max(0.01f, duration);
            startsOpen = initiallyOpen;
            initialized = false;
            InitializeState();
        }

        public void Toggle() => SetOpen(!IsOpen);
        public void Open() => SetOpen(true);
        public void Close() => SetOpen(false);

        public void SetOpen(bool open)
        {
            InitializeState();
            targetProgress = open ? 1f : 0f;

            if (!Application.isPlaying)
            {
                progress = targetProgress;
                ApplyPanelPosition();
            }
        }

        public void ReceiveHouseWireSignal(HouseSignal signal)
        {
            if (signal == null)
            {
                return;
            }

            if (signal.Kind == HouseSignalKind.Bool)
            {
                SetOpen(signal.BoolValue);
                return;
            }

            Toggle();
        }

        private void Awake() => InitializeState();

        private void Update()
        {
            if (doorPanel == null || Mathf.Approximately(progress, targetProgress))
            {
                return;
            }

            progress = Mathf.MoveTowards(progress, targetProgress, Time.deltaTime / Mathf.Max(0.01f, travelDuration));
            ApplyPanelPosition();
        }

        private void Reset()
        {
            ResolvePanel();
            if (doorPanel != null)
            {
                closedLocalPosition = doorPanel.localPosition;
            }
        }

        private void OnValidate()
        {
            travelDuration = Mathf.Max(0.01f, travelDuration);
            ResolvePanel();
            if (!Application.isPlaying && doorPanel != null)
            {
                progress = startsOpen ? 1f : 0f;
                targetProgress = progress;
                ApplyPanelPosition();
            }
        }

        private void InitializeState()
        {
            if (initialized)
            {
                return;
            }

            ResolvePanel();
            progress = startsOpen ? 1f : 0f;
            targetProgress = progress;
            ApplyPanelPosition();
            initialized = true;
        }

        private void ResolvePanel()
        {
            if (doorPanel != null)
            {
                return;
            }

            Transform candidate = transform.Find(DefaultPanelName);
            if (candidate != null)
            {
                doorPanel = candidate;
            }
        }

        private void ApplyPanelPosition()
        {
            if (doorPanel == null)
            {
                return;
            }

            doorPanel.localPosition = closedLocalPosition + (openLocalOffset * Mathf.Clamp01(progress));
        }
    }
}
