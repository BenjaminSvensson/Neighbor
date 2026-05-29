namespace Neighbor.Main.Features.Interaction
{
    public interface IPickupInteractionOverride
    {
        bool CanPickup(PlayerInteractor interactor);
    }
}
