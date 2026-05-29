namespace Neighbor.Main.Features.Interaction
{
    /// <summary>
    /// Contract for objects the PlayerInteractor can focus and activate.
    /// </summary>
    public interface IInteractable
    {
        bool CanInteract(PlayerInteractor interactor);
        void Interact(PlayerInteractor interactor);
    }
}
