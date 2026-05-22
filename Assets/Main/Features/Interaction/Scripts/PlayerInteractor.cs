using Neighbor.Main.Features.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [Header("Raycast")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string interactActionMap = "Player";
        [SerializeField] private string interactActionName = "Interact";
        [SerializeField, Min(0.1f)] private float interactRange = 3f;
        [SerializeField] private LayerMask interactMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Held Item")]
        [SerializeField] private Transform holdPoint;
        [SerializeField, Min(0.2f)] private float holdDistance = 1.65f;
        [SerializeField, Min(0f)] private float holdFollowStrength = 24f;
        [SerializeField, Min(0f)] private float holdRotationStrength = 9f;
        [SerializeField, Min(0f)] private float holdMaxVelocity = 12f;

        [Header("Throwing")]
        [SerializeField, Min(0f)] private float throwHoldThreshold = 0.22f;
        [SerializeField, Min(0f)] private float throwChargePullDistance = 0.35f;
        [SerializeField, Min(0f)] private float throwForce = 8.5f;
        [SerializeField, Min(0f)] private float throwUpwardAssist = 0.8f;

        private Pickupable heldPickup;
        private InputAction interactAction;
        private float releaseButtonDownTime;
        private bool releaseButtonWasHeld;

        public bool IsHoldingPickup => heldPickup != null;
        public Vector3 ViewForward => ViewTransform.forward;
        public Transform ViewTransform => viewCamera != null ? viewCamera.transform : transform;

        private void Awake()
        {
            if (viewCamera == null)
            {
                viewCamera = GetComponent<Camera>() ?? GetComponentInChildren<Camera>() ?? GetComponentInParent<Camera>();
            }

            ResolveInteractAction();
        }

        private void OnEnable()
        {
            ResolveInteractAction();
            interactAction?.Enable();
        }

        private void OnDisable()
        {
            interactAction?.Disable();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (InteractWasPressedThisFrame(keyboard))
            {
                TryInteract();
            }

            if (heldPickup == null || mouse == null)
            {
                releaseButtonWasHeld = false;
                return;
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                releaseButtonDownTime = Time.time;
                releaseButtonWasHeld = true;
            }

            if (releaseButtonWasHeld && mouse.rightButton.wasReleasedThisFrame)
            {
                float heldDuration = Time.time - releaseButtonDownTime;
                bool shouldThrow = heldDuration >= throwHoldThreshold;
                ReleaseHeldPickup(shouldThrow);
            }
        }

        private void FixedUpdate()
        {
            if (heldPickup == null)
            {
                return;
            }

            heldPickup.MoveHeld(
                GetHoldPosition(),
                ViewTransform.rotation,
                holdFollowStrength,
                holdRotationStrength,
                holdMaxVelocity);
        }

        public void Pickup(Pickupable pickupable)
        {
            if (pickupable == null)
            {
                return;
            }

            if (heldPickup != null)
            {
                ReleaseHeldPickup(false);
            }

            heldPickup = pickupable;
            heldPickup.Pickup(this);
        }

        private void TryInteract()
        {
            if (heldPickup != null)
            {
                ReleaseHeldPickup(false);
                return;
            }

            Ray ray = new Ray(ViewTransform.position, ViewTransform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, interactRange, interactMask, triggerInteraction))
            {
                return;
            }

            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null && interactable.CanInteract(this))
            {
                interactable.Interact(this);
            }
        }

        private void ReleaseHeldPickup(bool throwPickup)
        {
            if (heldPickup == null)
            {
                return;
            }

            Pickupable releasedPickup = heldPickup;
            heldPickup = null;
            releaseButtonWasHeld = false;

            if (throwPickup)
            {
                Vector3 throwVelocity = ViewTransform.forward * throwForce + Vector3.up * throwUpwardAssist;
                releasedPickup.Throw(throwVelocity);
                return;
            }

            releasedPickup.Drop();
        }

        private Vector3 GetHoldPosition()
        {
            if (holdPoint != null)
            {
                return holdPoint.position - ViewTransform.forward * ThrowChargePullAmount;
            }

            return ViewTransform.position + ViewTransform.forward * (holdDistance - ThrowChargePullAmount);
        }

        private float ThrowChargePullAmount
        {
            get
            {
                if (!releaseButtonWasHeld)
                {
                    return 0f;
                }

                float charge01 = throwHoldThreshold <= 0f
                    ? 1f
                    : Mathf.Clamp01((Time.time - releaseButtonDownTime) / throwHoldThreshold);

                return Mathf.SmoothStep(0f, throwChargePullDistance, charge01);
            }
        }

        private bool InteractWasPressedThisFrame(Keyboard keyboard)
        {
            if (interactAction != null)
            {
                return interactAction.WasPressedThisFrame();
            }

            return keyboard != null && keyboard.eKey.wasPressedThisFrame;
        }

        private void ResolveInteractAction()
        {
            if (inputActions == null)
            {
                interactAction = null;
                return;
            }

            InputActionMap actionMap = inputActions.FindActionMap(interactActionMap, false);
            interactAction = actionMap != null
                ? actionMap.FindAction(interactActionName, false)
                : inputActions.FindAction(interactActionName, false);
        }

        private void Reset()
        {
            viewCamera = GetComponent<Camera>();
        }
    }
}
