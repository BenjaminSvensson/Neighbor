using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Collider))]
    public sealed class SwingingAxeTripWire : MonoBehaviour
    {
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

        private void Awake()
        {
            tripCollider = GetComponent<Collider>();
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

        private void Update()
        {
            if (highlightedUntilTime > 0f && Time.time >= highlightedUntilTime)
            {
                highlightedUntilTime = 0f;
                ApplyWireColor(triggered ? triggeredColor : armedColor);
            }
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
