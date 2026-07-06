using Neighbor.Main.Features.Interaction;
using UnityEngine;

namespace Neighbor.Main.Features.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerRespawnCheckpoint : MonoBehaviour, IInteractable, IInteractionTooltipProvider
    {
        [Header("Checkpoint")]
        [SerializeField] private Transform respawnPoint;
        [SerializeField] private string checkpointId = "Checkpoint";
        [SerializeField] private bool persistCheckpoint = true;

        [Header("Activation")]
        [SerializeField] private bool activateOnTrigger = true;
        [SerializeField] private bool activateOnInteract = true;
        [SerializeField] private bool activateOnce;

        public bool IsActivated { get; private set; }
        public string CheckpointId => string.IsNullOrWhiteSpace(checkpointId) ? name : checkpointId.Trim();
        public Vector3 RespawnPosition => ResolveRespawnPoint().position;
        public Quaternion RespawnRotation => ResolveRespawnPoint().rotation;

        private void Reset()
        {
            respawnPoint = transform;
        }

        private void Awake()
        {
            if (respawnPoint == null)
            {
                respawnPoint = transform;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!activateOnTrigger || !TryResolvePlayer(other, out PlayerController player))
            {
                return;
            }

            Activate(player);
        }

        public bool CanInteract(PlayerInteractor interactor)
        {
            return activateOnInteract
                && interactor != null
                && (!activateOnce || !IsActivated)
                && interactor.GetComponentInParent<PlayerController>() != null;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            Activate(interactor.GetComponentInParent<PlayerController>());
        }

        public bool Activate(PlayerController player)
        {
            if (player == null || activateOnce && IsActivated)
            {
                return false;
            }

            PlayerDeathController deathController = player.GetComponent<PlayerDeathController>();
            if (deathController == null)
            {
                deathController = player.gameObject.AddComponent<PlayerDeathController>();
                deathController.Initialize(player);
            }

            bool registered = deathController.SetCheckpoint(
                RespawnPosition,
                RespawnRotation,
                CheckpointId,
                persistCheckpoint);
            IsActivated = IsActivated || registered;
            return registered;
        }

        public bool TryGetInteractionTooltip(
            PlayerInteractor interactor,
            InteractionTooltipContext context,
            out string actionText,
            out string keyText)
        {
            actionText = null;
            keyText = null;

            if (context != InteractionTooltipContext.FocusedInteractable || !activateOnInteract)
            {
                return false;
            }

            actionText = activateOnce && IsActivated ? "Checkpoint saved" : "Save checkpoint";
            keyText = string.Empty;
            return true;
        }

        private Transform ResolveRespawnPoint()
        {
            return respawnPoint != null ? respawnPoint : transform;
        }

        private static bool TryResolvePlayer(Collider collider, out PlayerController player)
        {
            player = collider != null ? collider.GetComponentInParent<PlayerController>() : null;
            return player != null;
        }
    }
}
