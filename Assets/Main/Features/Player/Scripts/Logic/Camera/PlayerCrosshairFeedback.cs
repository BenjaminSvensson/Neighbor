using Neighbor.Main.Features.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace Neighbor.Main.Features.Player
{
    public sealed class PlayerCrosshairFeedback : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private RectTransform crosshair;
        [SerializeField] private Graphic crosshairGraphic;
        [SerializeField, Min(1f)] private float interactableScale = 1.28f;
        [SerializeField, Min(0f)] private float scaleSharpness = 20f;
        [SerializeField] private Color idleColor = new(1f, 1f, 1f, 0.46f);
        [SerializeField] private Color interactableColor = new(1f, 0.86f, 0.38f, 0.94f);
        [SerializeField, Min(0f)] private float interactablePulseScale = 0.055f;
        [SerializeField, Min(0f)] private float pulseFrequency = 8f;
        [SerializeField, Min(0f)] private float colorSharpness = 18f;

        private Vector3 baseScale;
        private Color currentColor;
        private float pulseTime;

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

            if (crosshairGraphic == null)
            {
                crosshairGraphic = crosshair != null
                    ? crosshair.GetComponent<Graphic>()
                    : GetComponentInChildren<Graphic>();
            }

            if (crosshairGraphic != null)
            {
                crosshairGraphic.raycastTarget = false;
                currentColor = idleColor;
                crosshairGraphic.color = currentColor;
            }
            else
            {
                currentColor = idleColor;
            }

            baseScale = crosshair != null ? crosshair.localScale : Vector3.one;
        }

        private void Update()
        {
            if (crosshair == null || interactor == null)
            {
                return;
            }

            bool focused = interactor.HasFocusedInteractable && !InteractionOverlayState.IsGameplayInputBlocked;
            pulseTime = focused ? pulseTime + Time.unscaledDeltaTime * pulseFrequency : 0f;

            float pulseScale = focused
                ? 1f + Mathf.Sin(pulseTime) * interactablePulseScale
                : 1f;

            Vector3 targetScale = focused
                ? baseScale * interactableScale * pulseScale
                : baseScale;

            crosshair.localScale = Vector3.Lerp(
                crosshair.localScale,
                targetScale,
                1f - Mathf.Exp(-scaleSharpness * Time.unscaledDeltaTime));

            UpdateGraphic(focused);
        }

        private void UpdateGraphic(bool focused)
        {
            if (crosshairGraphic == null)
            {
                return;
            }

            crosshairGraphic.raycastTarget = false;
            Color targetColor = focused ? interactableColor : idleColor;
            currentColor = Color.Lerp(
                currentColor,
                targetColor,
                1f - Mathf.Exp(-colorSharpness * Time.unscaledDeltaTime));
            crosshairGraphic.color = currentColor;
        }

        private void Reset()
        {
            crosshair = GetComponentInChildren<RectTransform>();
            crosshairGraphic = GetComponentInChildren<Graphic>();
            interactor = GetComponentInChildren<PlayerInteractor>() ?? GetComponentInParent<PlayerInteractor>();
        }
    }
}
