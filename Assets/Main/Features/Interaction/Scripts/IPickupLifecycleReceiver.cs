namespace Neighbor.Main.Features.Interaction
{
    public interface IPickupLifecycleReceiver
    {
        void OnPickupStarted(Pickupable pickupable, PlayerInteractor interactor);
        void OnPickupPlaced(Pickupable pickupable);
    }
}
