namespace Neighbor.Main.Features.Interaction
{
    public interface IHoldInteractable
    {
        bool CanHoldInteract(PlayerInteractor interactor);
        void BeginHoldInteract(PlayerInteractor interactor);
        void HoldInteract(PlayerInteractor interactor, float deltaTime);
        void EndHoldInteract(PlayerInteractor interactor, bool completed);
    }
}
