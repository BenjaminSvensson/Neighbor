using System.Collections.Generic;
using Neighbor.Main.Features.Neighbor;
using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Collider))]
    public sealed class SwingingAxeTripWire : MonoBehaviour
    {
        private static readonly List<SwingingAxeTripWire> ActiveTripWires = new();

        [SerializeField] private SwingingAxeTrap targetAxe;
        [SerializeField] private bool activateOnlyForPlayer = true;
        [SerializeField] private bool activateForPhysicsObjects = true;
        [SerializeField] private bool disableAfterTrigger = true;
        [SerializeField] private Renderer wireRenderer;
        [SerializeField] private Color armedColor = new(1f, 0.05f, 0.02f, 0.65f);
        [SerializeField] private Color triggeredColor = new(0.25f, 0.25f, 0.25f, 0.35f);
        [SerializeField] private Color highlightedColor = new(0.1f, 0.85f, 1f, 0.95f);

        private Collider tripCollider;
        private MaterialPropertyBlock propertyBlock;
        private float highlightedUntilTime;
        private bool triggered;
        private bool startingColliderEnabled;

        public bool IsTriggered => triggered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveTripWires()
        {
            ActiveTripWires.Clear();
        }

        private void Awake()
        {
            tripCollider = GetComponent<Collider>();
            startingColliderEnabled = tripCollider.enabled;
            tripCollider.isTrigger = true;

            if (targetAxe == null)
            {
                targetAxe = GetComponentInParent<SwingingAxeTrap>();
            }

            if (wireRenderer == null)
            {
                wireRenderer = GetComponentInChildren<Renderer>();
            }

            ApplyWireColor(armedColor);
        }

        private void OnEnable()
        {
            if (!ActiveTripWires.Contains(this))
            {
                ActiveTripWires.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveTripWires.Remove(this);
        }

        private void Update()
        {
            if (highlightedUntilTime > 0f && Time.time >= highlightedUntilTime)
            {
                highlightedUntilTime = 0f;
                ApplyWireColor(triggered ? triggeredColor : armedColor);
            }
        }

        public static void ResetAllToStartingState()
        {
            for (int i = 0; i < ActiveTripWires.Count; i++)
            {
                ActiveTripWires[i]?.ResetToStartingState();
            }
        }

        private void ResetToStartingState()
        {
            highlightedUntilTime = 0f;
            triggered = false;
            if (tripCollider != null)
            {
                tripCollider.enabled = startingColliderEnabled;
            }

            ApplyWireColor(armedColor);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (triggered || other == null)
            {
                return;
            }

            if (!CanTrigger(other))
            {
                return;
            }

            triggered = true;
            if (other.GetComponentInParent<PlayerController>() != null)
            {
                NeighborEnvironmentalAwareness.Report(transform.position, 0.65f, gameObject);
            }

            targetAxe?.Activate();
            ApplyWireColor(triggeredColor);

            if (disableAfterTrigger)
            {
                tripCollider.enabled = false;
            }
        }

        public void HighlightFor(float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            highlightedUntilTime = Mathf.Max(highlightedUntilTime, Time.time + duration);
            if (wireRenderer != null)
            {
                wireRenderer.enabled = true;
            }

            ApplyWireColor(highlightedColor);
        }

        private bool CanTrigger(Collider other)
        {
            if (!activateOnlyForPlayer)
            {
                return true;
            }

            if (other.GetComponentInParent<PlayerController>() != null)
            {
                return true;
            }

            if (other.GetComponentInParent<NeighborImpactReceiver>() != null)
            {
                return true;
            }

            return activateForPhysicsObjects && IsTriggeredByPhysicsObject(other);
        }

        private static bool IsTriggeredByPhysicsObject(Collider other)
        {
            WindUpToy toy = other.GetComponentInParent<WindUpToy>();
            if (toy != null && toy.IsRunning)
            {
                return true;
            }

            Pickupable pickupable = other.GetComponentInParent<Pickupable>();
            if (pickupable != null && !pickupable.IsHeld)
            {
                return true;
            }

            Rigidbody body = other.attachedRigidbody;
            return body != null && !body.isKinematic;
        }

        private void ApplyWireColor(Color color)
        {
            if (wireRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            wireRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            wireRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
