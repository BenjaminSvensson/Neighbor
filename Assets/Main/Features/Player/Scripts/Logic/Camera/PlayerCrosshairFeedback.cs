using Neighbor.Main.Features.Interaction;
using UnityEngine;

namespace Neighbor.Main.Features.Player
{
    public sealed class PlayerCrosshairFeedback : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private RectTransform crosshair;
        [SerializeField, Min(1f)] private float interactableScale = 1.25f;
        [SerializeField, Min(0f)] private float scaleSharpness = 18f;

        private Vector3 baseScale;

        private void Awake()
        {
            if (interactor == null)
            {
                interactor = GetComponentInChildren<PlayerInteractor>() ?? GetComponentInParent<PlayerInteractor>();
            }

            if (crosshair == null)
            {
                crosshair = GetComponentInChildren<RectTransform>();
            }

            baseScale = crosshair != null ? crosshair.localScale : Vector3.one;
        }

        private void Update()
        {
            if (crosshair == null || interactor == null)
            {
                return;
            }

            Vector3 targetScale = interactor.HasFocusedInteractable
                ? baseScale * interactableScale
                : baseScale;

            crosshair.localScale = Vector3.Lerp(
                crosshair.localScale,
                targetScale,
                1f - Mathf.Exp(-scaleSharpness * Time.deltaTime));
        }

        private void Reset()
        {
            crosshair = GetComponentInChildren<RectTransform>();
            interactor = GetComponentInChildren<PlayerInteractor>() ?? GetComponentInParent<PlayerInteractor>();
        }
    }
}
