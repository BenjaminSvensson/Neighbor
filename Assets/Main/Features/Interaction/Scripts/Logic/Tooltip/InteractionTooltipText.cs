using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class InteractionTooltipText : MonoBehaviour, IInteractionTooltipProvider
    {
        [Header("Looked At")]
        [SerializeField] private string focusedActionText;
        [SerializeField] private string focusedKeyText = "E";

        [Header("Held Primary Use")]
        [SerializeField] private string heldPrimaryActionText;
        [SerializeField] private string heldPrimaryKeyText = "Left Mouse";

        [Header("Held Secondary Use")]
        [SerializeField] private string heldSecondaryActionText;
        [SerializeField] private string heldSecondaryKeyText = "Right Mouse";

        [Header("Hold Interaction")]
        [SerializeField] private string holdActionText;
        [SerializeField] private string holdKeyText = "Hold E";

        public bool TryGetInteractionTooltip(
            PlayerInteractor interactor,
            InteractionTooltipContext context,
            out string actionText,
            out string keyText)
        {
            actionText = context switch
            {
                InteractionTooltipContext.HeldPrimaryUse => heldPrimaryActionText,
                InteractionTooltipContext.HeldSecondaryUse => heldSecondaryActionText,
                InteractionTooltipContext.HoldInteractable => holdActionText,
                _ => focusedActionText
            };

            keyText = context switch
            {
                InteractionTooltipContext.HeldPrimaryUse => heldPrimaryKeyText,
                InteractionTooltipContext.HeldSecondaryUse => heldSecondaryKeyText,
                InteractionTooltipContext.HoldInteractable => holdKeyText,
                _ => focusedKeyText
            };

            return !string.IsNullOrWhiteSpace(actionText) && !string.IsNullOrWhiteSpace(keyText);
        }
    }
}
