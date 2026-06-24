using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HouseWireEndpoint))]
    [RequireComponent(typeof(HouseWireInputRelay))]
    [AddComponentMenu("Neighbor/House Builder/Wiring/Pulley Elevator Motion")]
    public sealed class HousePulleyElevatorMotion : MonoBehaviour, IHouseWireSignalReceiver
    {
        private const string ActivatePortId = "activate";
        private const string DefaultPlatformName = "Platform";
        private const string AlternatePlatformName = "Elevator Platform";

        [SerializeField] private Transform platform;
        [SerializeField] private Vector3 loweredLocalPosition;
        [SerializeField] private Vector3 raisedLocalOffset = new(0f, 3f, 0f);
        [SerializeField, Min(0.01f)] private float travelDuration = 1.5f;
        [SerializeField] private bool startsRaised;

        private float progress;
        private float targetProgress;
        private bool initialized;

        public bool IsRaised => targetProgress >= 1f;
        public bool IsFullyRaised => targetProgress >= 1f && Mathf.Approximately(progress, 1f);
        public bool IsFullyLowered => targetProgress <= 0f && Mathf.Approximately(progress, 0f);
        public bool IsMoving => !Mathf.Approximately(progress, targetProgress);
        public float Progress => progress;
        public float TargetProgress => targetProgress;

        public void Configure(Transform elevatorPlatform, Vector3 loweredPosition, Vector3 raisedOffset, float duration, bool initiallyRaised)
        {
            platform = elevatorPlatform;
            loweredLocalPosition = loweredPosition;
            raisedLocalOffset = raisedOffset;
            travelDuration = Mathf.Max(0.01f, duration);
            startsRaised = initiallyRaised;
            initialized = false;
            EnsureWiringPort();
            InitializeState();
        }

        public void Toggle() => SetRaised(targetProgress < 0.5f);
        public void Raise() => SetRaised(true);
        public void Lower() => SetRaised(false);
        public void SetRaised(bool raised) => SetTargetProgress(raised ? 1f : 0f);

        public void SetTargetProgress(float value)
        {
            InitializeState();
            targetProgress = Mathf.Clamp01(value);
            if (!Application.isPlaying)
            {
                progress = targetProgress;
                ApplyPlatformPosition();
            }
        }

        public void ReceiveHouseWireSignal(HouseSignal signal)
        {
            if (signal == null)
            {
                return;
            }

            switch (signal.Kind)
            {
                case HouseSignalKind.Bool:
                    SetRaised(signal.BoolValue);
                    break;
                case HouseSignalKind.Float:
                    SetTargetProgress(signal.FloatValue);
                    break;
                default:
                    Toggle();
                    break;
            }
        }

        private void Awake()
        {
            EnsureWiringPort();
            InitializeState();
        }

        private void Update()
        {
            if (platform == null || Mathf.Approximately(progress, targetProgress))
            {
                return;
            }

            progress = Mathf.MoveTowards(progress, targetProgress, Time.deltaTime / Mathf.Max(0.01f, travelDuration));
            ApplyPlatformPosition();
        }

        private void Reset()
        {
            ResolvePlatform();
            if (platform != null)
            {
                loweredLocalPosition = platform.localPosition;
            }

            EnsureWiringPort();
        }

        private void OnValidate()
        {
            travelDuration = Mathf.Max(0.01f, travelDuration);
            ResolvePlatform();
            if (!Application.isPlaying && platform != null)
            {
                progress = startsRaised ? 1f : 0f;
                targetProgress = progress;
                ApplyPlatformPosition();
            }
        }

        private void InitializeState()
        {
            if (initialized)
            {
                return;
            }

            ResolvePlatform();
            progress = startsRaised ? 1f : 0f;
            targetProgress = progress;
            ApplyPlatformPosition();
            initialized = true;
        }

        private void ResolvePlatform()
        {
            if (platform != null)
            {
                return;
            }

            Transform candidate = transform.Find(DefaultPlatformName) ?? transform.Find(AlternatePlatformName);
            platform = candidate != null ? candidate : transform;
        }

        private void ApplyPlatformPosition()
        {
            if (platform == null)
            {
                return;
            }

            platform.localPosition = loweredLocalPosition + raisedLocalOffset * Mathf.Clamp01(progress);
        }

        private void EnsureWiringPort()
        {
            HouseWireEndpoint endpoint = GetComponent<HouseWireEndpoint>();
            HouseWireInputRelay inputRelay = GetComponent<HouseWireInputRelay>();
            if (endpoint == null || inputRelay == null)
            {
                return;
            }

            endpoint.EnsureIdentity();
            if (!endpoint.TryGetPort(ActivatePortId, out HouseWirePortDefinition port))
            {
                port = endpoint.AddPort(
                    "Raise / Toggle",
                    HouseWirePortDirection.Input,
                    HouseSignalKind.Any,
                    visualOffset: Vector3.up,
                    requestedId: ActivatePortId);
            }

            port.OnSignalReceived.RemoveListener(inputRelay.Receive);
            port.OnSignalReceived.AddListener(inputRelay.Receive);
        }
    }
}
