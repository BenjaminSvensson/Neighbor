namespace Neighbor.Main.Features.Interaction
{
    public interface IInteractable
    {
        bool CanInteract(PlayerInteractor interactor);
        void Interact(PlayerInteractor interactor);
    }
}
