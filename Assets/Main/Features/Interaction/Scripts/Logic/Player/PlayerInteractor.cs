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

        [Header("Placement")]
        [SerializeField, Min(0.2f)] private float placementRange = 3.25f;
        [SerializeField, Min(0f)] private float placementProbeRadius = 0.08f;
        [SerializeField, Range(0f, 1f)] private float placementMinimumUpDot = 0.45f;
        [SerializeField, Min(0f)] private float placementSurfacePadding = 0.025f;
        [SerializeField, Min(0f)] private float placementClearanceShrink = 0.015f;
        [SerializeField, Min(0f)] private float placementSupportInset = 0.08f;
        [SerializeField, Min(0f)] private float placementSupportProbeLift = 0.08f;
        [SerializeField, Min(0.01f)] private float placementSupportProbeDistance = 0.18f;
        [SerializeField, Range(0f, 1f)] private float placementPickupableCenterPull = 0.22f;
        [SerializeField, Range(0, 6)] private int placementSearchRings = 3;
        [SerializeField, Min(0.05f)] private float placementSearchStep = 0.38f;
        [SerializeField, Min(0f)] private float placementFallbackDownDistance = 3f;
        [SerializeField] private LayerMask placementMask = ~0;

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
        private IHoldInteractable activeHoldInteractable;
        private Collider[] playerColliders;
        private readonly RaycastHit[] interactHits = new RaycastHit[12];
        private readonly RaycastHit[] throwArcHits = new RaycastHit[8];
        private readonly RaycastHit[] holdObstructionHits = new RaycastHit[12];
        private readonly RaycastHit[] placementHits = new RaycastHit[12];
        private readonly Collider[] placementBlockHits = new Collider[24];
        private float releaseButtonDownTime;
        private bool releaseButtonWasHeld;

        public bool IsHoldingPickup => heldPickup != null;
        public Pickupable HeldPickup => heldPickup;
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

            bool interactPressed = InteractWasPressedThisFrame(keyboard);
            bool interactHeld = InteractIsPressed(keyboard);
            if (interactPressed)
            {
                TryInteract();
            }

            UpdateFocusedInteractable();

            if (heldPickup == null || mouse == null)
            {
                if (heldPickup == null)
                {
                    UpdateHoldInteraction(interactHeld);
                }
                else
                {
                    EndActiveHoldInteraction(false);
                }

                releaseButtonWasHeld = false;
                return;
            }

            EndActiveHoldInteraction(false);

            if (mouse.leftButton.wasPressedThisFrame && TryPrimaryUseHeldPickup())
            {
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame && TryUseHeldPickupOnDoorBlocker())
            {
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                TryUseHeldPickupOnFocusedInteractable();
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

        public bool ForgetHeldPickup(Pickupable pickupable)
        {
            if (heldPickup != pickupable)
            {
                return false;
            }

            heldPickup = null;
            releaseButtonWasHeld = false;
            HideThrowArc();
            return true;
        }

        private void TryInteract()
        {
            if (heldPickup != null)
            {
                return;
            }

            Ray ray = new Ray(ViewTransform.position, ViewTransform.forward);
            IInteractable interactable = FocusedInteractable ?? FindBestInteractable(ray);

            if (interactable != null && interactable.CanInteract(this))
            {
                interactable.Interact(this);
            }
        }

        private bool TryUseHeldPickupOnFocusedInteractable()
        {
            DoorKey heldKey = heldPickup != null ? heldPickup.GetComponentInChildren<DoorKey>() : null;
            if (heldKey == null)
            {
                return false;
            }

            Ray ray = new Ray(ViewTransform.position, ViewTransform.forward);
            IInteractable interactable = FindBestInteractable(ray);
            if (interactable is not Door door || !door.CanInteract(this))
            {
                return false;
            }

            door.Interact(this);
            return true;
        }

        private bool TryPrimaryUseHeldPickup()
        {
            IPrimaryUseInteractable primaryUseInteractable = heldPickup != null
                ? heldPickup.GetComponentInChildren<IPrimaryUseInteractable>()
                : null;

            if (primaryUseInteractable == null || !primaryUseInteractable.CanPrimaryUse(this))
            {
                return false;
            }

            primaryUseInteractable.PrimaryUse(this);
            return true;
        }

        private bool TryUseHeldPickupOnDoorBlocker()
        {
            return TryBlockHeldPickupOnFocusedDoor(heldPickup);
        }

        private bool TryBlockHeldPickupOnFocusedDoor(Pickupable pickup)
        {
            DoorBlockerChair blocker = pickup != null ? pickup.GetComponentInChildren<DoorBlockerChair>() : null;
            if (blocker == null)
            {
                return false;
            }

            Ray ray = new Ray(ViewTransform.position, ViewTransform.forward);
            IInteractable interactable = FindBestInteractable(ray);
            if (interactable is not Door door || !door.CanInteract(this))
            {
                return false;
            }

            if (!blocker.TryBlockDoor(door, this))
            {
                return false;
            }

            if (heldPickup == pickup)
            {
                heldPickup = null;
            }

            releaseButtonWasHeld = false;
            HideThrowArc();
            return true;
        }

        private void ReleaseHeldPickup(bool throwPickup)
        {
            if (heldPickup == null)
            {
                return;
            }

            if (!throwPickup && TryBlockHeldPickupOnFocusedDoor(heldPickup))
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

            if (TryGetPlacementPose(releasedPickup, out Vector3 placementPosition, out Quaternion placementRotation, out bool foundPlacementSurface, out bool shouldSleepAfterPlacement))
            {
                releasedPickup.Place(placementPosition, placementRotation, shouldSleepAfterPlacement);
            }
            else if (foundPlacementSurface)
            {
                heldPickup = releasedPickup;
            }
            else
            {
                releasedPickup.Drop();
            }

            HideThrowArc();
        }

        private bool TryGetPlacementPose(Pickupable pickupable, out Vector3 position, out Quaternion rotation, out bool foundSurface, out bool shouldSleepAfterPlacement)
        {
            position = default;
            rotation = default;
            foundSurface = false;
            shouldSleepAfterPlacement = false;
            if (pickupable == null)
            {
                return false;
            }

            if (!TryGetPlacementHit(pickupable, out RaycastHit hit))
            {
                return false;
            }

            foundSurface = true;
            Vector3 placementPoint = PullPlacementPointTowardSurfaceCenter(hit.point, hit.normal, hit.collider);
            rotation = Quaternion.Euler(0f, pickupable.transform.eulerAngles.y, 0f);
            Quaternion originalRotation = pickupable.transform.rotation;
            pickupable.transform.rotation = rotation;
            Bounds bounds = pickupable.GetPlacementBounds();
            pickupable.transform.rotation = originalRotation;

            Vector3 extents = bounds.extents;
            Vector3 normal = hit.normal.normalized;
            float normalExtent =
                Mathf.Abs(normal.x) * extents.x +
                Mathf.Abs(normal.y) * extents.y +
                Mathf.Abs(normal.z) * extents.z;

            Vector3 transformToBoundsCenter = pickupable.transform.position - bounds.center;
            position = placementPoint + normal * (normalExtent + placementSurfacePadding) + transformToBoundsCenter;
            Vector3 placedBoundsCenter = position - transformToBoundsCenter;
            if (HasPlacementClearance(pickupable, placedBoundsCenter, bounds.extents, hit.collider))
            {
                shouldSleepAfterPlacement = HasPlacementSupport(pickupable, placedBoundsCenter, bounds.extents, normal, hit.collider);
                return true;
            }

            return TryFindNearbyPlacementPose(
                pickupable,
                hit,
                normal,
                normalExtent,
                bounds.extents,
                transformToBoundsCenter,
                out position,
                out shouldSleepAfterPlacement);
        }

        private Vector3 PullPlacementPointTowardSurfaceCenter(Vector3 point, Vector3 normal, Collider surfaceCollider)
        {
            if (placementPickupableCenterPull <= 0f || surfaceCollider == null || surfaceCollider.GetComponentInParent<Pickupable>() == null)
            {
                return point;
            }

            Vector3 surfaceCenter = surfaceCollider.bounds.center;
            Vector3 planarCenter = point + Vector3.ProjectOnPlane(surfaceCenter - point, normal);
            return Vector3.Lerp(point, planarCenter, placementPickupableCenterPull);
        }

        private bool TryFindNearbyPlacementPose(
            Pickupable pickupable,
            RaycastHit originalHit,
            Vector3 normal,
            float normalExtent,
            Vector3 extents,
            Vector3 transformToBoundsCenter,
            out Vector3 position,
            out bool shouldSleepAfterPlacement)
        {
            position = default;
            shouldSleepAfterPlacement = false;
            if (placementSearchRings <= 0)
            {
                return false;
            }

            Vector3 tangent = Vector3.ProjectOnPlane(ViewTransform.right, normal);
            if (tangent.sqrMagnitude <= 0.001f)
            {
                tangent = Vector3.Cross(normal, ViewTransform.forward);
            }

            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;

            for (int ring = 1; ring <= placementSearchRings; ring++)
            {
                float radius = placementSearchStep * ring;
                int samples = ring * 8;
                for (int i = 0; i < samples; i++)
                {
                    float angle = (Mathf.PI * 2f * i) / samples;
                    Vector3 offset = (Mathf.Cos(angle) * tangent + Mathf.Sin(angle) * bitangent) * radius;
                    Vector3 probeOrigin = originalHit.point + offset + normal * (normalExtent + 0.25f);

                    if (!Physics.Raycast(
                        probeOrigin,
                        -normal,
                        out RaycastHit surfaceHit,
                        normalExtent + 0.6f,
                        placementMask,
                        QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }

                    if (surfaceHit.normal.y < placementMinimumUpDot || ShouldIgnorePlacementSurface(surfaceHit.collider, pickupable))
                    {
                        continue;
                    }

                    Vector3 candidatePoint = PullPlacementPointTowardSurfaceCenter(surfaceHit.point, surfaceHit.normal, surfaceHit.collider);
                    Vector3 candidatePosition = candidatePoint + surfaceHit.normal.normalized * (normalExtent + placementSurfacePadding) + transformToBoundsCenter;
                    Vector3 candidateBoundsCenter = candidatePosition - transformToBoundsCenter;
                    if (!HasPlacementClearance(pickupable, candidateBoundsCenter, extents, surfaceHit.collider))
                    {
                        continue;
                    }

                    position = candidatePosition;
                    shouldSleepAfterPlacement = HasPlacementSupport(pickupable, candidateBoundsCenter, extents, surfaceHit.normal.normalized, surfaceHit.collider);
                    return true;
                }
            }

            return false;
        }

        private bool HasPlacementSupport(Pickupable pickupable, Vector3 center, Vector3 extents, Vector3 normal, Collider supportCollider)
        {
            Vector3 tangent = Vector3.ProjectOnPlane(ViewTransform.right, normal);
            if (tangent.sqrMagnitude <= 0.001f)
            {
                tangent = Vector3.Cross(normal, ViewTransform.forward);
            }

            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;

            float normalExtent =
                Mathf.Abs(normal.x) * extents.x +
                Mathf.Abs(normal.y) * extents.y +
                Mathf.Abs(normal.z) * extents.z;
            float tangentExtent = Mathf.Max(0.01f,
                Mathf.Abs(tangent.x) * extents.x +
                Mathf.Abs(tangent.y) * extents.y +
                Mathf.Abs(tangent.z) * extents.z - placementSupportInset);
            float bitangentExtent = Mathf.Max(0.01f,
                Mathf.Abs(bitangent.x) * extents.x +
                Mathf.Abs(bitangent.y) * extents.y +
                Mathf.Abs(bitangent.z) * extents.z - placementSupportInset);

            Vector3 bottomCenter = center - normal * normalExtent;
            return HasSupportProbe(bottomCenter + tangent * tangentExtent + bitangent * bitangentExtent, normal, pickupable, supportCollider)
                && HasSupportProbe(bottomCenter + tangent * tangentExtent - bitangent * bitangentExtent, normal, pickupable, supportCollider)
                && HasSupportProbe(bottomCenter - tangent * tangentExtent + bitangent * bitangentExtent, normal, pickupable, supportCollider)
                && HasSupportProbe(bottomCenter - tangent * tangentExtent - bitangent * bitangentExtent, normal, pickupable, supportCollider);
        }

        private bool HasSupportProbe(Vector3 point, Vector3 normal, Pickupable pickupable, Collider supportCollider)
        {
            Vector3 origin = point + normal * placementSupportProbeLift;
            if (!Physics.Raycast(origin, -normal, out RaycastHit hit, placementSupportProbeDistance, placementMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return hit.collider == supportCollider || !ShouldIgnorePlacementSurface(hit.collider, pickupable);
        }

        private bool HasPlacementClearance(Pickupable pickupable, Vector3 center, Vector3 extents, Collider supportCollider)
        {
            Vector3 clearanceExtents = new Vector3(
                Mathf.Max(0.001f, extents.x - placementClearanceShrink),
                Mathf.Max(0.001f, extents.y - placementClearanceShrink),
                Mathf.Max(0.001f, extents.z - placementClearanceShrink));

            int hitCount = Physics.OverlapBoxNonAlloc(
                center,
                clearanceExtents,
                placementBlockHits,
                Quaternion.identity,
                placementMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = placementBlockHits[i];
                if (hit == null || hit == supportCollider || ShouldIgnorePlacementSurface(hit, pickupable))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private bool TryGetPlacementHit(Pickupable ignoredPickup, out RaycastHit bestHit)
        {
            bestHit = default;
            Ray ray = new Ray(ViewTransform.position, ViewTransform.forward);
            int hitCount = placementProbeRadius > 0f
                ? Physics.SphereCastNonAlloc(
                    ray,
                    placementProbeRadius,
                    placementHits,
                    placementRange,
                    placementMask,
                    QueryTriggerInteraction.Ignore)
                : Physics.RaycastNonAlloc(
                    ray,
                    placementHits,
                    placementRange,
                    placementMask,
                    QueryTriggerInteraction.Ignore);

            if (TryChoosePlacementHit(hitCount, ignoredPickup, out bestHit))
            {
                return true;
            }

            if (placementFallbackDownDistance <= 0f)
            {
                return false;
            }

            Vector3 fallbackOrigin = GetHoldPosition() + Vector3.up * 0.35f;
            hitCount = Physics.RaycastNonAlloc(
                fallbackOrigin,
                Vector3.down,
                placementHits,
                placementFallbackDownDistance,
                placementMask,
                QueryTriggerInteraction.Ignore);

            return TryChoosePlacementHit(hitCount, ignoredPickup, out bestHit);
        }

        private bool TryChoosePlacementHit(int hitCount, Pickupable ignoredPickup, out RaycastHit bestHit)
        {
            bestHit = default;
            float bestDistance = float.PositiveInfinity;
            bool foundHit = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = placementHits[i];
                if (hit.collider == null || hit.normal.y < placementMinimumUpDot || ShouldIgnorePlacementSurface(hit.collider, ignoredPickup))
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

        private bool ShouldIgnorePlacementSurface(Collider collider, Pickupable ignoredPickup)
        {
            return IsPlayerCollider(collider) || collider.GetComponentInParent<Pickupable>() == ignoredPickup;
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

        private bool InteractIsPressed(Keyboard keyboard)
        {
            bool actionHeld = interactAction != null && interactAction.IsPressed();
            bool fallbackHeld = keyboard != null && keyboard.eKey.isPressed;

            return actionHeld || fallbackHeld;
        }

        private void UpdateHoldInteraction(bool interactHeld)
        {
            if (!interactHeld)
            {
                EndActiveHoldInteraction(false);
                return;
            }

            Ray ray = new Ray(ViewTransform.position, ViewTransform.forward);
            IHoldInteractable holdInteractable = FindBestHoldInteractable(ray);
            if (holdInteractable == null || !holdInteractable.CanHoldInteract(this))
            {
                EndActiveHoldInteraction(false);
                return;
            }

            if (activeHoldInteractable != holdInteractable)
            {
                EndActiveHoldInteraction(false);
                activeHoldInteractable = holdInteractable;
                activeHoldInteractable.BeginHoldInteract(this);
            }

            activeHoldInteractable.HoldInteract(this, Time.deltaTime);
        }

        private void EndActiveHoldInteraction(bool completed)
        {
            if (activeHoldInteractable == null)
            {
                return;
            }

            IHoldInteractable endedInteractable = activeHoldInteractable;
            activeHoldInteractable = null;
            endedInteractable.EndHoldInteract(this, completed);
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

                if (IsInteractionBlockedByCloserHit(hitCount, hit.distance, interactable))
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

        private IHoldInteractable FindBestHoldInteractable(Ray ray)
        {
            int hitCount = interactRadius > 0f
                ? Physics.SphereCastNonAlloc(ray, interactRadius, interactHits, interactRange, interactMask, triggerInteraction)
                : Physics.RaycastNonAlloc(ray, interactHits, interactRange, interactMask, triggerInteraction);

            IHoldInteractable bestInteractable = null;
            float bestAlignment = -1f;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = interactHits[i];
                if (hit.collider == null || IsPlayerCollider(hit.collider))
                {
                    continue;
                }

                IHoldInteractable interactable = hit.collider.GetComponentInParent<IHoldInteractable>();
                if (interactable == null || !interactable.CanHoldInteract(this))
                {
                    continue;
                }

                if (IsInteractionBlockedByCloserHit(hitCount, hit.distance, interactable))
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

        private bool IsInteractionBlockedByCloserHit(int hitCount, float candidateDistance, object candidateInteractable)
        {
            const float blockerDistanceTolerance = 0.01f;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit blockerHit = interactHits[i];
                Collider blocker = blockerHit.collider;
                if (blocker == null
                    || blocker.isTrigger
                    || blockerHit.distance >= candidateDistance - blockerDistanceTolerance
                    || IsPlayerCollider(blocker)
                    || BelongsToCandidate(blocker, candidateInteractable))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool BelongsToCandidate(Collider collider, object candidateInteractable)
        {
            if (candidateInteractable is not Component candidateComponent)
            {
                return false;
            }

            Transform candidateTransform = candidateComponent.transform;
            return collider.transform == candidateTransform
                || collider.transform.IsChildOf(candidateTransform)
                || candidateTransform.IsChildOf(collider.transform);
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
