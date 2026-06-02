using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Collider))]
    public sealed class LaserGridPowerSwitch : MonoBehaviour, IInteractable, IInteractionTooltipProvider
    {
        [SerializeField] private LaserGrid targetGrid;
        [SerializeField] private Transform switchLever;
        [SerializeField] private Vector3 offLocalEuler = new(-24f, 0f, 0f);
        [SerializeField] private Vector3 onLocalEuler = new(24f, 0f, 0f);
        [SerializeField] private string onActionText = "Disable Laser Grid";
        [SerializeField] private string offActionText = "Enable Laser Grid";

        private void Awake()
        {
            if (targetGrid == null)
            {
                targetGrid = GetComponentInParent<LaserGrid>();
            }

            if (switchLever == null)
            {
                switchLever = transform;
            }

            ApplyLeverPose();
        }

        public bool CanInteract(PlayerInteractor interactor)
        {
            return targetGrid != null;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (targetGrid == null)
            {
                return;
            }

            targetGrid.TogglePowered();
            ApplyLeverPose();
        }

        public bool TryGetInteractionTooltip(
            PlayerInteractor interactor,
            InteractionTooltipContext context,
            out string actionText,
            out string keyText)
        {
            actionText = null;
            keyText = null;

            if (context != InteractionTooltipContext.FocusedInteractable || targetGrid == null)
            {
                return false;
            }

            actionText = targetGrid.IsPowered ? onActionText : offActionText;
            keyText = "E";
            return true;
        }

        private void ApplyLeverPose()
        {
            if (switchLever == null || targetGrid == null)
            {
                return;
            }

            switchLever.localRotation = Quaternion.Euler(targetGrid.IsPowered ? onLocalEuler : offLocalEuler);
        }
    }
}
