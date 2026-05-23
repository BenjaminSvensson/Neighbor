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
        [SerializeField, Min(0f)] private float interactRadius = 0.18f;
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

        [Header("Throw Arc")]
        [SerializeField] private bool showThrowArc = true;
        [SerializeField] private LineRenderer throwArcRenderer;
        [SerializeField, Range(4, 64)] private int throwArcSegments = 28;
        [SerializeField, Min(0.02f)] private float throwArcTimeStep = 0.07f;
        [SerializeField, Min(0.001f)] private float throwArcLineWidth = 0.025f;
        [SerializeField] private LayerMask throwArcCollisionMask = ~0;
        [SerializeField] private Color throwArcStartColor = new Color(1f, 0.92f, 0.45f, 0.9f);
        [SerializeField] private Color throwArcEndColor = new Color(1f, 0.45f, 0.2f, 0.15f);

        private Pickupable heldPickup;
        private InputAction interactAction;
        private Collider[] playerColliders;
        private readonly RaycastHit[] interactHits = new RaycastHit[12];
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

            playerColliders = GetComponentsInParent<Collider>();
            ResolveInteractAction();
            EnsureThrowArcRenderer();
        }

        private void OnEnable()
        {
            ResolveInteractAction();
            interactAction?.Enable();
        }

        private void OnDisable()
        {
            interactAction?.Disable();
            HideThrowArc();
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

        private void LateUpdate()
        {
            UpdateThrowArc();
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
            IInteractable interactable = FindBestInteractable(ray);

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
                Vector3 throwVelocity = CalculateThrowVelocity(1f);
                releasedPickup.Throw(throwVelocity);
                HideThrowArc();
                return;
            }

            releasedPickup.Drop();
            HideThrowArc();
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

        private float ThrowCharge01
        {
            get
            {
                if (!releaseButtonWasHeld)
                {
                    return 0f;
                }

                return throwHoldThreshold <= 0f
                    ? 1f
                    : Mathf.Clamp01((Time.time - releaseButtonDownTime) / throwHoldThreshold);
            }
        }

        private Vector3 CalculateThrowVelocity(float charge01)
        {
            float chargedForce = Mathf.Lerp(throwForce * 0.35f, throwForce, Mathf.Clamp01(charge01));
            return ViewTransform.forward * chargedForce + Vector3.up * throwUpwardAssist;
        }

        private bool InteractWasPressedThisFrame(Keyboard keyboard)
        {
            bool actionPressed = interactAction != null && interactAction.WasPressedThisFrame();
            bool fallbackPressed = keyboard != null && keyboard.eKey.wasPressedThisFrame;

            return actionPressed || fallbackPressed;
        }

        private IInteractable FindBestInteractable(Ray ray)
        {
            int hitCount = interactRadius > 0f
                ? Physics.SphereCastNonAlloc(ray, interactRadius, interactHits, interactRange, interactMask, triggerInteraction)
                : Physics.RaycastNonAlloc(ray, interactHits, interactRange, interactMask, triggerInteraction);

            IInteractable bestInteractable = null;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = interactHits[i];
                if (hit.collider == null || IsPlayerCollider(hit.collider))
                {
                    continue;
                }

                IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable == null || !interactable.CanInteract(this))
                {
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    bestInteractable = interactable;
                }
            }

            return bestInteractable;
        }

        private bool IsPlayerCollider(Collider collider)
        {
            if (playerColliders == null)
            {
                return false;
            }

            foreach (Collider playerCollider in playerColliders)
            {
                if (playerCollider == collider)
                {
                    return true;
                }
            }

            return false;
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

        private void UpdateThrowArc()
        {
            if (!showThrowArc || heldPickup == null || !releaseButtonWasHeld)
            {
                HideThrowArc();
                return;
            }

            EnsureThrowArcRenderer();
            if (throwArcRenderer == null)
            {
                return;
            }

            throwArcRenderer.enabled = true;

            Vector3 origin = heldPickup.ThrowOrigin;
            Vector3 velocity = CalculateThrowVelocity(ThrowCharge01);
            Vector3 previousPoint = origin;
            int pointCount = 1;

            throwArcRenderer.positionCount = throwArcSegments;
            throwArcRenderer.SetPosition(0, origin);

            for (int i = 1; i < throwArcSegments; i++)
            {
                float time = i * throwArcTimeStep;
                Vector3 nextPoint = origin + velocity * time + Physics.gravity * (0.5f * time * time);
                Vector3 segment = nextPoint - previousPoint;

                if (Physics.Raycast(previousPoint, segment.normalized, out RaycastHit hit, segment.magnitude, throwArcCollisionMask, QueryTriggerInteraction.Ignore))
                {
                    throwArcRenderer.SetPosition(i, hit.point);
                    pointCount = i + 1;
                    break;
                }

                throwArcRenderer.SetPosition(i, nextPoint);
                previousPoint = nextPoint;
                pointCount = i + 1;
            }

            throwArcRenderer.positionCount = pointCount;
        }

        private void EnsureThrowArcRenderer()
        {
            if (throwArcRenderer != null)
            {
                return;
            }

            GameObject arcObject = new GameObject("ThrowArcPreview");
            arcObject.transform.SetParent(transform, false);
            throwArcRenderer = arcObject.AddComponent<LineRenderer>();
            throwArcRenderer.enabled = false;
            throwArcRenderer.useWorldSpace = true;
            throwArcRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            throwArcRenderer.receiveShadows = false;
            throwArcRenderer.textureMode = LineTextureMode.Stretch;
            throwArcRenderer.alignment = LineAlignment.View;
            throwArcRenderer.widthMultiplier = throwArcLineWidth;
            throwArcRenderer.startColor = throwArcStartColor;
            throwArcRenderer.endColor = throwArcEndColor;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                throwArcRenderer.material = new Material(shader);
            }
        }

        private void HideThrowArc()
        {
            if (throwArcRenderer != null)
            {
                throwArcRenderer.enabled = false;
                throwArcRenderer.positionCount = 0;
            }
        }

        private void Reset()
        {
            viewCamera = GetComponent<Camera>();
        }
    }
}
