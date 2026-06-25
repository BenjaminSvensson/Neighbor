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
        private const float ProgressSnapEpsilon = 0.0001f;
        private const float MotionEpsilon = 0.00001f;

        [SerializeField] private Transform platform;
        [SerializeField] private Vector3 loweredLocalPosition;
        [SerializeField] private Vector3 raisedLocalOffset = new(0f, 3f, 0f);
        [SerializeField, Min(0.01f)] private float travelDuration = 1.5f;
        [SerializeField] private bool startsRaised;
        [SerializeField] private bool carryPlayersInTrigger = true;

        private readonly Dictionary<PlayerController, CharacterController> carriedPlayers = new();
        private readonly List<PlayerController> staleCarriedPlayers = new();
        private readonly Collider[] riderOverlapHits = new Collider[16];
        private BoxCollider riderTrigger;
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

        private void LateUpdate()
        {
            AdvanceMotion(Time.deltaTime);
        }

        private void AdvanceMotion(float deltaTime)
        {
            if (platform == null || Mathf.Approximately(progress, targetProgress))
            {
                return;
            }

            float nextProgress = Mathf.MoveTowards(
                progress,
                targetProgress,
                Mathf.Max(0f, deltaTime) / Mathf.Max(0.01f, travelDuration));
            if (Mathf.Abs(nextProgress - targetProgress) <= ProgressSnapEpsilon)
            {
                nextProgress = targetProgress;
            }

            if (Mathf.Abs(nextProgress - progress) <= ProgressSnapEpsilon && !Mathf.Approximately(nextProgress, targetProgress))
            {
                return;
            }

            progress = nextProgress;
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
            AddPotentialRider(other);
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerController player = other != null ? other.GetComponentInParent<PlayerController>() : null;
            if (player == null)
            {
                return;
            }

            CharacterController controller = carriedPlayers.TryGetValue(player, out CharacterController trackedController)
                ? trackedController
                : player.GetComponent<CharacterController>();
            if (!IsPlayerInsideMovingRideVolume(player, controller))
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
            if (!carryPlayersInTrigger || motionDelta.sqrMagnitude <= MotionEpsilon * MotionEpsilon)
            {
                return;
            }

            RefreshRidersInsideMovingVolume();
            if (carriedPlayers.Count == 0)
            {
                return;
            }

            staleCarriedPlayers.Clear();
            bool movedAnyPlayer = false;
            foreach (KeyValuePair<PlayerController, CharacterController> entry in carriedPlayers)
            {
                PlayerController player = entry.Key;
                CharacterController controller = entry.Value;
                if (player == null
                    || controller == null
                    || !player.isActiveAndEnabled
                    || !controller.enabled
                    || !IsPlayerInsideMovingRideVolume(player, controller))
                {
                    staleCarriedPlayers.Add(player);
                    continue;
                }

                player.ApplyExternalDisplacement(motionDelta);
                movedAnyPlayer = true;
            }

            if (movedAnyPlayer)
            {
                Physics.SyncTransforms();
            }

            for (int i = 0; i < staleCarriedPlayers.Count; i++)
            {
                carriedPlayers.Remove(staleCarriedPlayers[i]);
            }
        }

        private void AddPotentialRider(Collider other)
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

        private void RefreshRidersInsideMovingVolume()
        {
            if (!TryGetMovingRideBounds(out Bounds rideBounds))
            {
                return;
            }

            Physics.SyncTransforms();
            int hitCount = Physics.OverlapBoxNonAlloc(
                rideBounds.center,
                rideBounds.extents,
                riderOverlapHits,
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hitCount; i++)
            {
                AddPotentialRider(riderOverlapHits[i]);
                riderOverlapHits[i] = null;
            }
        }

        private bool IsPlayerInsideMovingRideVolume(PlayerController player, CharacterController controller)
        {
            if (!TryGetMovingRideBounds(out Bounds rideBounds))
            {
                return true;
            }

            Bounds playerBounds = controller != null
                ? controller.bounds
                : new Bounds(player != null ? player.transform.position : Vector3.zero, Vector3.one * 0.5f);
            rideBounds.Expand(Mathf.Max(0.05f, controller != null ? controller.skinWidth * 2f : 0.05f));
            return rideBounds.Intersects(playerBounds);
        }

        private bool TryGetMovingRideBounds(out Bounds rideBounds)
        {
            ResolveRiderTrigger();
            if (riderTrigger == null || platform == null)
            {
                rideBounds = default;
                return false;
            }

            rideBounds = riderTrigger.bounds;
            rideBounds.center += platform.position - GetLoweredPlatformWorldPosition();
            return true;
        }

        private Vector3 GetLoweredPlatformWorldPosition()
        {
            return platform != null && platform.parent != null
                ? platform.parent.TransformPoint(loweredLocalPosition)
                : loweredLocalPosition;
        }

        private void ResolveRiderTrigger()
        {
            if (riderTrigger != null)
            {
                return;
            }

            BoxCollider[] boxColliders = GetComponents<BoxCollider>();
            for (int i = 0; i < boxColliders.Length; i++)
            {
                if (boxColliders[i] != null && boxColliders[i].isTrigger)
                {
                    riderTrigger = boxColliders[i];
                    return;
                }
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
