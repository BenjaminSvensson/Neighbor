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
        [SerializeField] private Color lockedColor = new(1f, 0.2f, 0.12f, 0.95f);
        [SerializeField] private Color tooFarColor = new(0.65f, 0.78f, 1f, 0.78f);
        [SerializeField] private Color holdingColor = new(0.45f, 1f, 0.7f, 0.95f);
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

            PlayerInteractor.InteractionReticleState state = InteractionOverlayState.IsGameplayInputBlocked
                ? PlayerInteractor.InteractionReticleState.Idle
                : interactor.ReticleState;
            bool active = state != PlayerInteractor.InteractionReticleState.Idle;
            pulseTime = active ? pulseTime + Time.unscaledDeltaTime * pulseFrequency : 0f;

            float pulseScale = active
                ? 1f + Mathf.Sin(pulseTime) * interactablePulseScale
                : 1f;
            float stateScale = GetStateScale(state);

            Vector3 targetScale = active
                ? baseScale * interactableScale * stateScale * pulseScale
                : baseScale;

            crosshair.localScale = Vector3.Lerp(
                crosshair.localScale,
                targetScale,
                1f - Mathf.Exp(-scaleSharpness * Time.unscaledDeltaTime));

            UpdateGraphic(state);
        }

        private void UpdateGraphic(PlayerInteractor.InteractionReticleState state)
        {
            if (crosshairGraphic == null)
            {
                return;
            }

            crosshairGraphic.raycastTarget = false;
            Color targetColor = GetStateColor(state);
            currentColor = Color.Lerp(
                currentColor,
                targetColor,
                1f - Mathf.Exp(-colorSharpness * Time.unscaledDeltaTime));
            crosshairGraphic.color = currentColor;
        }

        private float GetStateScale(PlayerInteractor.InteractionReticleState state)
        {
            return state switch
            {
                PlayerInteractor.InteractionReticleState.Locked => 0.92f,
                PlayerInteractor.InteractionReticleState.TooFar => 0.82f,
                PlayerInteractor.InteractionReticleState.Holding => 1f + interactor.ThrowCharge * 0.22f,
                _ => 1f
            };
        }

        private Color GetStateColor(PlayerInteractor.InteractionReticleState state)
        {
            return state switch
            {
                PlayerInteractor.InteractionReticleState.Usable => interactableColor,
                PlayerInteractor.InteractionReticleState.Locked => lockedColor,
                PlayerInteractor.InteractionReticleState.TooFar => tooFarColor,
                PlayerInteractor.InteractionReticleState.Holding => holdingColor,
                _ => idleColor
            };
        }

        private void Reset()
        {
            crosshair = GetComponentInChildren<RectTransform>();
            crosshairGraphic = GetComponentInChildren<Graphic>();
            interactor = GetComponentInChildren<PlayerInteractor>() ?? GetComponentInParent<PlayerInteractor>();
        }
    }
}
