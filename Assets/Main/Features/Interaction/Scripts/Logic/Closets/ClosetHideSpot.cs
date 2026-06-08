using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class ClosetHideSpot : MonoBehaviour, IInteractable
    {
        [SerializeField] private ClosetDoorPair doors;
        [SerializeField] private Transform hidePoint;
        [SerializeField] private Transform exitPoint;
        [SerializeField] private bool closeDoorsWhenHidden = true;
        [SerializeField] private bool openDoorsWhenExiting = true;

        private PlayerController hiddenPlayer;
        private CharacterController hiddenCharacterController;
        private PlayerHidingState hiddenState;

        public bool HasHiddenPlayer => hiddenPlayer != null;

        public bool CanInteract(PlayerInteractor interactor)
        {
            if (HasHiddenPlayer)
            {
                return IsHiddenPlayer(interactor);
            }

            return interactor != null && interactor.HeldPickup == null;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (HasHiddenPlayer)
            {
                if (IsHiddenPlayer(interactor))
                {
                    Exit();
                }

                return;
            }

            Hide(interactor);
        }

        private void Hide(PlayerInteractor interactor)
        {
            PlayerController player = interactor != null ? interactor.GetComponentInParent<PlayerController>() : null;
            if (player == null)
            {
                return;
            }

            hiddenPlayer = player;
            hiddenCharacterController = player.GetComponent<CharacterController>();
            hiddenState = player.GetComponent<PlayerHidingState>();
            if (hiddenState == null)
            {
                hiddenState = player.gameObject.AddComponent<PlayerHidingState>();
            }

            Transform target = hidePoint != null ? hidePoint : transform;
            if (hiddenCharacterController != null)
            {
                hiddenCharacterController.enabled = false;
            }

            player.transform.SetPositionAndRotation(target.position, target.rotation);
            player.enabled = false;
            hiddenState.SetHidden(true);

            if (closeDoorsWhenHidden)
            {
                doors?.SetOpen(false);
            }
        }

        private void Exit()
        {
            if (hiddenPlayer == null)
            {
                return;
            }

            Transform target = exitPoint != null ? exitPoint : transform;
            hiddenPlayer.transform.SetPositionAndRotation(target.position, target.rotation);

            if (hiddenCharacterController != null)
            {
                hiddenCharacterController.enabled = true;
            }

            hiddenPlayer.enabled = true;
            hiddenState?.SetHidden(false);

            hiddenPlayer = null;
            hiddenCharacterController = null;
            hiddenState = null;

            if (openDoorsWhenExiting)
            {
                doors?.SetOpen(true);
            }
        }

        public void ReleasePlayerForRespawn(PlayerController player)
        {
            if (player == null || hiddenPlayer != player)
            {
                return;
            }

            if (hiddenCharacterController != null)
            {
                hiddenCharacterController.enabled = true;
            }

            hiddenState?.SetHidden(false);
            hiddenPlayer = null;
            hiddenCharacterController = null;
            hiddenState = null;
        }

        private bool IsHiddenPlayer(PlayerInteractor interactor)
        {
            return interactor != null
                && hiddenPlayer != null
                && interactor.GetComponentInParent<PlayerController>() == hiddenPlayer;
        }
    }
}
