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
        [SerializeField, Min(0f)] private float interactAlignmentTieTolerance = 0.002f;
        [SerializeField] private LayerMask interactMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Held Item")]
        [SerializeField] private Transform smallHoldPoint;
        [SerializeField] private Transform mediumHoldPoint;
        [SerializeField] private Transform largeHoldPoint;
        [SerializeField, HideInInspector] private Transform holdPoint;
        [SerializeField, Min(0.2f)] private float holdDistance = 1.65f;
        [SerializeField, Min(0f)] private float holdFollowStrength = 24f;
        [SerializeField, Min(0f)] private float holdRotationStrength = 9f;
        [SerializeField, Min(0f)] private float holdMaxVelocity = 12f;
        [SerializeField, Min(0f)] private float holdObstructionRadius = 0.35f;
        [SerializeField, Min(0f)] private float holdObstructionPadding = 0.18f;
        [SerializeField] private LayerMask holdObstructionMask = ~0;

        [Header("Throwing")]
        [SerializeField, Min(0f)] private float throwHoldThreshold = 0.22f;
        [SerializeField, Min(0f)] private float throwChargePullDistance = 0.35f;
        [SerializeField, Min(0f)] private float throwChargeLowerDistance = 0.45f;
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
        private readonly RaycastHit[] throwArcHits = new RaycastHit[8];
        private readonly RaycastHit[] holdObstructionHits = new RaycastHit[12];
        private float releaseButtonDownTime;
        private bool releaseButtonWasHeld;

        public bool IsHoldingPickup => heldPickup != null;
        public bool HasFocusedInteractable { get; private set; }
        public IInteractable FocusedInteractable { get; private set; }
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

            UpdateFocusedInteractable();

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
            IInteractable interactable = FocusedInteractable ?? FindBestInteractable(ray);

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
                releasedPickup.Throw(throwVelocity, playerColliders);
                HideThrowArc();
                return;
            }

            releasedPickup.Drop();
            HideThrowArc();
        }

        private Vector3 GetHoldPosition()
        {
            Transform activeHoldPoint = GetHoldPointFor(heldPickup);
            Vector3 desiredPosition;
            if (activeHoldPoint != null)
            {
                desiredPosition = activeHoldPoint.position - ViewTransform.forward * ThrowChargePullAmount - Vector3.up * ThrowChargeLowerAmount;
                return ResolveObstructedHoldPosition(desiredPosition);
            }

            desiredPosition = ViewTransform.position + ViewTransform.forward * (holdDistance - ThrowChargePullAmount) - Vector3.up * ThrowChargeLowerAmount;
            return ResolveObstructedHoldPosition(desiredPosition);
        }

        private Transform GetHoldPointFor(Pickupable pickupable)
        {
            if (pickupable == null)
            {
                return mediumHoldPoint != null ? mediumHoldPoint : holdPoint;
            }

            Transform sizedHoldPoint = pickupable.AssignedHoldPointSize switch
            {
                Pickupable.HoldPointSize.Small => smallHoldPoint,
                Pickupable.HoldPointSize.Large => largeHoldPoint,
                _ => mediumHoldPoint
            };

            return sizedHoldPoint != null ? sizedHoldPoint : mediumHoldPoint != null ? mediumHoldPoint : holdPoint;
        }

        private Vector3 ResolveObstructedHoldPosition(Vector3 desiredPosition)
        {
            if (holdObstructionRadius <= 0f)
            {
                return desiredPosition;
            }

            Vector3 origin = ViewTransform.position;
            Vector3 toDesired = desiredPosition - origin;
            float desiredDistance = toDesired.magnitude;
            if (desiredDistance <= 0.01f)
            {
                return desiredPosition;
            }

            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                holdObstructionRadius,
                toDesired / desiredDistance,
                holdObstructionHits,
                desiredDistance,
                holdObstructionMask,
                QueryTriggerInteraction.Ignore);

            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = holdObstructionHits[i];
                if (hit.collider == null || ShouldIgnoreHoldObstruction(hit.collider))
                {
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                }
            }

            if (float.IsPositiveInfinity(bestDistance))
            {
                return desiredPosition;
            }

            float safeDistance = Mathf.Max(0.15f, bestDistance - holdObstructionPadding);
            return origin + toDesired.normalized * safeDistance;
        }

        private bool ShouldIgnoreHoldObstruction(Collider collider)
        {
            return IsPlayerCollider(collider) || collider.GetComponentInParent<Pickupable>() == heldPickup;
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

        private float ThrowChargeLowerAmount
        {
            get
            {
                if (!releaseButtonWasHeld)
                {
                    return 0f;
                }

                return Mathf.SmoothStep(0f, throwChargeLowerDistance, ThrowCharge01);
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

        private void UpdateFocusedInteractable()
        {
            if (heldPickup != null)
            {
                FocusedInteractable = null;
                HasFocusedInteractable = false;
                return;
            }

            Ray ray = new Ray(ViewTransform.position, ViewTransform.forward);
            FocusedInteractable = FindBestInteractable(ray);
            HasFocusedInteractable = FocusedInteractable != null;
        }

        private IInteractable FindBestInteractable(Ray ray)
        {
            int hitCount = interactRadius > 0f
                ? Physics.SphereCastNonAlloc(ray, interactRadius, interactHits, interactRange, interactMask, triggerInteraction)
                : Physics.RaycastNonAlloc(ray, interactHits, interactRange, interactMask, triggerInteraction);

            IInteractable bestInteractable = null;
            float bestAlignment = -1f;
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

                float alignment = GetViewAlignment(ray, hit);
                bool isMoreCentered = alignment > bestAlignment + interactAlignmentTieTolerance;
                bool isTiedAndCloser = Mathf.Abs(alignment - bestAlignment) <= interactAlignmentTieTolerance && hit.distance < bestDistance;

                if (isMoreCentered || isTiedAndCloser)
                {
                    bestAlignment = alignment;
                    bestDistance = hit.distance;
                    bestInteractable = interactable;
                }
            }

            return bestInteractable;
        }

        private static float GetViewAlignment(Ray ray, RaycastHit hit)
        {
            Vector3 directionToHit = hit.point - ray.origin;
            if (directionToHit.sqrMagnitude <= 0.0001f)
            {
                return 1f;
            }

            return Vector3.Dot(ray.direction, directionToHit.normalized);
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

            Vector3 origin = GetThrowArcOrigin();
            Vector3 velocity = CalculateThrowVelocity(ThrowCharge01);
            Vector3 previousPoint = origin;
            int pointCount = 1;

            throwArcRenderer.positionCount = throwArcSegments;
            throwArcRenderer.SetPosition(0, origin);
            throwArcRenderer.widthMultiplier = throwArcLineWidth;

            for (int i = 1; i < throwArcSegments; i++)
            {
                float time = i * throwArcTimeStep;
                Vector3 nextPoint = origin + velocity * time + Physics.gravity * (0.5f * time * time);
                Vector3 segment = nextPoint - previousPoint;

                if (TryGetThrowArcHit(previousPoint, segment, out RaycastHit hit))
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

        private Vector3 GetThrowArcOrigin()
        {
            Vector3 origin = ViewTransform.position + ViewTransform.forward * 0.35f;
            origin += Vector3.down * 0.08f;
            return origin;
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
            throwArcRenderer.numCapVertices = 4;
            throwArcRenderer.numCornerVertices = 4;
            throwArcRenderer.startColor = throwArcStartColor;
            throwArcRenderer.endColor = throwArcEndColor;
            throwArcRenderer.sortingOrder = 100;
            throwArcRenderer.material = CreateThrowArcMaterial();
        }

        private void HideThrowArc()
        {
            if (throwArcRenderer != null)
            {
                throwArcRenderer.enabled = false;
                throwArcRenderer.positionCount = 0;
            }
        }

        private bool TryGetThrowArcHit(Vector3 previousPoint, Vector3 segment, out RaycastHit bestHit)
        {
            bestHit = default;
            if (segment.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            int hitCount = Physics.RaycastNonAlloc(
                previousPoint,
                segment.normalized,
                throwArcHits,
                segment.magnitude,
                throwArcCollisionMask,
                QueryTriggerInteraction.Ignore);

            float bestDistance = float.PositiveInfinity;
            bool foundHit = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = throwArcHits[i];
                if (hit.collider == null || IsPlayerCollider(hit.collider) || hit.collider.GetComponentInParent<Pickupable>() == heldPickup)
                {
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    bestHit = hit;
                    foundHit = true;
                }
            }

            return foundHit;
        }

        private Material CreateThrowArcMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Hidden/Internal-Colored");

            if (shader == null)
            {
                return null;
            }

            Material material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }

            if (material.HasProperty("_ZTest"))
            {
                material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            }

            return material;
        }

        private void Reset()
        {
            viewCamera = GetComponent<Camera>();
        }
    }
}
