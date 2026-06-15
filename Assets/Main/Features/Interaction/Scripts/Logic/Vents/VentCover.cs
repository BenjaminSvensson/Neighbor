using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Collider))]
    public sealed class VentCover : MonoBehaviour, IInteractable, IInteractionTooltipProvider
    {
        [Header("Screws")]
        [SerializeField, Min(0)] private int screwCount = 4;
        [SerializeField] private GameObject[] screwVisuals;
        [SerializeField] private GameObject[] removedScrewVisuals;

        [Header("Detach")]
        [SerializeField] private bool detachWhenOpen = true;
        [SerializeField] private bool addPickupableWhenOpen = true;
        [SerializeField, Min(0f)] private float detachForwardImpulse = 1.2f;
        [SerializeField, Min(0f)] private float detachDownImpulse = 0.3f;

        [Header("Feedback")]
        [SerializeField] private Renderer coverRenderer;
        [SerializeField] private Color lockedColor = new(0.42f, 0.46f, 0.5f, 1f);
        [SerializeField] private Color looseColor = new(0.7f, 0.74f, 0.78f, 1f);

        private MaterialPropertyBlock propertyBlock;
        private Collider[] coverColliders;
        private Rigidbody coverBody;
        private int screwsRemaining;
        private bool isOpen;
        private ItemAudioFeedback audioFeedback;

        public bool HasScrewsRemaining => screwsRemaining > 0;
        public bool CanUnscrew => !isOpen && screwsRemaining > 0;

        private void Awake()
        {
            coverColliders = GetComponentsInChildren<Collider>();
            coverBody = GetComponent<Rigidbody>();
            if (coverRenderer == null)
            {
                coverRenderer = GetComponentInChildren<Renderer>();
            }

            screwsRemaining = Mathf.Max(0, screwCount);
            audioFeedback = ItemAudioFeedback.Resolve(gameObject);
            ApplyScrewVisuals();
            ApplyCoverColor();
        }

        public bool CanInteract(PlayerInteractor interactor)
        {
            return interactor != null
                && interactor.HeldPickup != null
                && interactor.HeldPickup.GetComponentInChildren<Screwdriver>() != null
                && CanUnscrew;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (CanInteract(interactor))
            {
                UnscrewOne(interactor.HeldPickup.gameObject);
            }
        }

        public void UnscrewOne(GameObject source)
        {
            if (!CanUnscrew)
            {
                return;
            }

            screwsRemaining--;
            audioFeedback?.Play(ItemSoundProfile.ScrewTurn, 0.48f);
            ApplyScrewVisuals();
            ApplyCoverColor();

            if (screwsRemaining <= 0)
            {
                Open(source);
            }
        }

        public bool TryGetInteractionTooltip(
            PlayerInteractor interactor,
            InteractionTooltipContext context,
            out string actionText,
            out string keyText)
        {
            actionText = null;
            keyText = null;

            if (context != InteractionTooltipContext.FocusedInteractable || !CanInteract(interactor))
            {
                return false;
            }

            actionText = "Unscrew";
            keyText = "Left Mouse";
            return true;
        }

        private void Open(GameObject source)
        {
            isOpen = true;
            audioFeedback?.Play(ItemSoundProfile.MetalDetach, 0.68f);
            ApplyScrewVisuals();
            ApplyCoverColor();

            if (!detachWhenOpen)
            {
                SetBlockingColliders(false);
                return;
            }

            if (coverBody == null)
            {
                coverBody = gameObject.AddComponent<Rigidbody>();
            }

            coverBody.isKinematic = false;
            coverBody.useGravity = true;
            coverBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (addPickupableWhenOpen && GetComponent<Pickupable>() == null)
            {
                gameObject.AddComponent<Pickupable>();
            }

            Vector3 impulse = -transform.forward * detachForwardImpulse + Vector3.down * detachDownImpulse;
            coverBody.AddForce(impulse, ForceMode.Impulse);
            coverBody.AddTorque(Random.insideUnitSphere * 0.6f, ForceMode.Impulse);
        }

        private void SetBlockingColliders(bool enabled)
        {
            if (coverColliders == null)
            {
                return;
            }

            foreach (Collider coverCollider in coverColliders)
            {
                if (coverCollider != null)
                {
                    coverCollider.enabled = enabled;
                }
            }
        }

        private void ApplyScrewVisuals()
        {
            SetVisualArray(screwVisuals, true);
            SetVisualArray(removedScrewVisuals, false);

            int removedCount = Mathf.Max(0, screwCount - screwsRemaining);
            for (int i = 0; screwVisuals != null && i < screwVisuals.Length; i++)
            {
                if (screwVisuals[i] != null)
                {
                    screwVisuals[i].SetActive(i >= removedCount && i < screwCount);
                }
            }

            for (int i = 0; removedScrewVisuals != null && i < removedScrewVisuals.Length; i++)
            {
                if (removedScrewVisuals[i] != null)
                {
                    removedScrewVisuals[i].SetActive(i < removedCount);
                }
            }
        }

        private static void SetVisualArray(GameObject[] visuals, bool active)
        {
            if (visuals == null)
            {
                return;
            }

            foreach (GameObject visual in visuals)
            {
                if (visual != null)
                {
                    visual.SetActive(active);
                }
            }
        }

        private void ApplyCoverColor()
        {
            if (coverRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            coverRenderer.GetPropertyBlock(propertyBlock);
            Color color = screwsRemaining > 0 ? lockedColor : looseColor;
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            coverRenderer.SetPropertyBlock(propertyBlock);
        }

        private void OnValidate()
        {
            screwCount = Mathf.Max(0, screwCount);
            detachForwardImpulse = Mathf.Max(0f, detachForwardImpulse);
            detachDownImpulse = Mathf.Max(0f, detachDownImpulse);
        }
    }
}
