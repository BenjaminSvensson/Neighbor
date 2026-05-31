namespace Neighbor.Main.Features.Interaction
{
    public interface IInteractionTooltipProvider
    {
        bool TryGetInteractionTooltip(
            PlayerInteractor interactor,
            InteractionTooltipContext context,
            out string actionText,
            out string keyText);
    }
}
