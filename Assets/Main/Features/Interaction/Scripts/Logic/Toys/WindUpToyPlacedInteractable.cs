using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class WindUpToyPlacedInteractable : MonoBehaviour, IInteractable, IInteractionTooltipProvider
    {
        [SerializeField] private WindUpToy toy;

        private void Awake()
        {
            if (toy == null)
            {
                toy = GetComponentInParent<WindUpToy>();
            }
        }

        public bool CanInteract(PlayerInteractor interactor)
        {
            return toy != null && toy.CanWindFromWorld;
        }

        public void Interact(PlayerInteractor interactor)
        {
            toy?.TryWindFromWorld(interactor);
        }

        public bool TryGetInteractionTooltip(
            PlayerInteractor interactor,
            InteractionTooltipContext context,
            out string actionText,
            out string keyText)
        {
            actionText = null;
            keyText = null;

            if (context != InteractionTooltipContext.FocusedInteractable)
            {
                return false;
            }

            actionText = toy != null && toy.IsRunning ? "Toy running" : "Wind up toy";
            keyText = "E";
            return true;
        }
    }
}
