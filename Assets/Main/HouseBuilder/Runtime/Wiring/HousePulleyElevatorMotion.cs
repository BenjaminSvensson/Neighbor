using System.Collections.Generic;
using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    [DefaultExecutionOrder(100)]
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
        [SerializeField] private bool carryPlayersInTrigger = true;

        private readonly Dictionary<PlayerController, CharacterController> carriedPlayers = new();
        private readonly List<PlayerController> staleCarriedPlayers = new();
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
            AdvanceMotion(Time.deltaTime);
        }

        private void AdvanceMotion(float deltaTime)
        {
            if (platform == null || Mathf.Approximately(progress, targetProgress))
            {
                return;
            }

            progress = Mathf.MoveTowards(progress, targetProgress, deltaTime / Mathf.Max(0.01f, travelDuration));
            Vector3 motionDelta = ApplyPlatformPosition();
            CarryPlayers(motionDelta);
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

        private Vector3 ApplyPlatformPosition()
        {
            if (platform == null)
            {
                return Vector3.zero;
            }

            Vector3 previousPosition = platform.position;
            platform.localPosition = loweredLocalPosition + raisedLocalOffset * Mathf.Clamp01(progress);
            return platform.position - previousPosition;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!carryPlayersInTrigger)
            {
                return;
            }

            PlayerController player = other != null ? other.GetComponentInParent<PlayerController>() : null;
            if (player == null || carriedPlayers.ContainsKey(player))
            {
                return;
            }

            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                carriedPlayers.Add(player, controller);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerController player = other != null ? other.GetComponentInParent<PlayerController>() : null;
            if (player != null)
            {
                carriedPlayers.Remove(player);
            }
        }

        private void OnDisable()
        {
            carriedPlayers.Clear();
            staleCarriedPlayers.Clear();
        }

        private void CarryPlayers(Vector3 motionDelta)
        {
            if (!carryPlayersInTrigger || motionDelta.sqrMagnitude <= 0f || carriedPlayers.Count == 0)
            {
                return;
            }

            staleCarriedPlayers.Clear();
            foreach (KeyValuePair<PlayerController, CharacterController> entry in carriedPlayers)
            {
                PlayerController player = entry.Key;
                CharacterController controller = entry.Value;
                if (player == null || controller == null || !player.isActiveAndEnabled || !controller.enabled)
                {
                    staleCarriedPlayers.Add(player);
                    continue;
                }

                controller.Move(motionDelta);
            }

            for (int i = 0; i < staleCarriedPlayers.Count; i++)
            {
                carriedPlayers.Remove(staleCarriedPlayers[i]);
            }
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
