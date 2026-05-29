namespace Neighbor.Main.Features.Interaction
{
    public interface IPrimaryUseInteractable
    {
        bool CanPrimaryUse(PlayerInteractor interactor);
        void PrimaryUse(PlayerInteractor interactor);
    }
}
