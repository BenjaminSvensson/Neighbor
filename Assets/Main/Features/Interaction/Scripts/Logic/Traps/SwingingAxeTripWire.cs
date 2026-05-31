using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Collider))]
    public sealed class SwingingAxeTripWire : MonoBehaviour
    {
        [SerializeField] private SwingingAxeTrap targetAxe;
        [SerializeField] private bool activateOnlyForPlayer = true;
        [SerializeField] private bool disableAfterTrigger = true;
        [SerializeField] private Renderer wireRenderer;
        [SerializeField] private Color armedColor = new(1f, 0.05f, 0.02f, 0.65f);
        [SerializeField] private Color triggeredColor = new(0.25f, 0.25f, 0.25f, 0.35f);

        private Collider tripCollider;
        private MaterialPropertyBlock propertyBlock;
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

        private void OnTriggerEnter(Collider other)
        {
            if (triggered || other == null)
            {
                return;
            }

            if (activateOnlyForPlayer && other.GetComponentInParent<PlayerController>() == null)
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
