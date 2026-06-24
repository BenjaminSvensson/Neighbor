using System;
using Neighbor.Main.Features.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class PlayerInteractor : MonoBehaviour
    {
        private const int MaximumInventorySlots = 6;

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

        [Header("Tooltip")]
        [SerializeField] private InteractionTooltipView tooltipView;
        [SerializeField] private PlayerInventoryHudView inventoryHudView;

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

        [Header("Inventory")]
        [SerializeField, Range(1, MaximumInventorySlots)] private int inventorySlotCount = MaximumInventorySlots;
        [SerializeField] private Transform inventoryStashRoot;
        [SerializeField, Min(0f)] private float autoEquipAfterThrowDelay = 0.32f;
        [SerializeField, Min(0f)] private float autoEquipAfterDropDelay = 0.42f;

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
        private Pickupable[] inventorySlots;
        private int activeInventorySlot;
        private float releaseButtonDownTime;
        private bool releaseButtonWasHeld;
        private string pendingAutoEquipMatchKey;
        private float pendingAutoEquipAt;
        private Material throwArcMaterial;
        private Pickupable cachedHeldPickup;
        private DoorKey cachedHeldDoorKey;
        private DoorBlockerChair cachedHeldDoorBlocker;
        private IPrimaryUseInteractable cachedHeldPrimaryUseInteractable;

        public event Action PickupStarted;
        public event Action DropStarted;
        public event Action ThrowStarted;
        public event Action InteractionStarted;

        public bool IsHoldingPickup => heldPickup != null;
        public Pickupable HeldPickup => heldPickup;
        public float ThrowCharge => ThrowCharge01;
        public int ActiveInventorySlot => activeInventorySlot;
        public int InventorySlotCount => inventorySlotCount;
        public Pickupable GetInventorySlotPickup(int slotIndex)
        {
            EnsureInventorySlots();
            return slotIndex >= 0 && slotIndex < inventorySlots.Length ? inventorySlots[slotIndex] : null;
        }

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
            EnsureTooltipView();
            EnsureInventorySlots();
            EnsureInventoryHudView();
        }

        private void OnEnable()
        {
            ResolveInteractAction();
            interactAction?.Enable();
        }

        private void OnDisable()
        {
            CancelPendingAutoEquip();
            interactAction?.Disable();
            EndActiveHoldInteraction(false);
            DropInventoryForDisable();
            HideThrowArc();
            if (tooltipView != null)
            {
                tooltipView.Hide();
            }

            if (inventoryHudView != null)
            {
                inventoryHudView.Hide();
            }
        }

        private void OnDestroy()
        {
            ReleaseThrowArcResources();
        }

        private void Update()
        {
            UpdatePendingAutoEquip();
            RefreshHeldPickupComponentCache();

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (InteractionOverlayState.IsGameplayInputBlocked)
            {
                EndActiveHoldInteraction(false);
                HideThrowArc();
                if (tooltipView != null)
                {
                    tooltipView.Hide();
                }

                return;
            }

            bool interactPressed = InteractWasPressedThisFrame(keyboard);
            bool interactHeld = InteractIsPressed(keyboard);
            int requestedInventorySlot = GetPressedInventorySlot(keyboard);
            if (requestedInventorySlot >= 0)
            {
                SelectInventorySlot(requestedInventorySlot);
            }

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
                UpdateInteractionTooltip(mouse);
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

            UpdateInteractionTooltip(mouse);
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

            CancelPendingAutoEquip();
            EnsureInventorySlots();
            int existingSlot = FindInventorySlot(pickupable);
            if (existingSlot >= 0)
            {
                SelectInventorySlot(existingSlot);
                return;
            }

            if (heldPickup != null)
            {
                int emptySlot = FindFirstEmptyInventorySlot();
                if (emptySlot >= 0)
                {
                    StashHeldPickupInSlot(emptySlot);
                }
                else
                {
                    DropHeldPickupForReplacement();
                }
            }

            inventorySlots[activeInventorySlot] = pickupable;
            heldPickup = pickupable;
            heldPickup.Pickup(this);
            heldPickup.SnapHeldPose(GetHoldPosition(heldPickup), ViewTransform.rotation);
            PickupStarted?.Invoke();
        }

        public bool ForgetHeldPickup(Pickupable pickupable)
        {
            if (heldPickup != pickupable)
            {
                return false;
            }

            heldPickup = null;
            ClearInventorySlot(pickupable);
            releaseButtonWasHeld = false;
            HideThrowArc();
            TrySelectMatchingInventoryPickup(pickupable);
            return true;
        }

        private void TryInteract()
        {
            Ray ray = new Ray(ViewTransform.position, ViewTransform.forward);
            IInteractable interactable = FocusedInteractable ?? FindBestInteractable(ray);

            if (interactable != null && interactable.CanInteract(this))
            {
                InteractionStarted?.Invoke();
                interactable.Interact(this);
            }
        }

        private bool TryUseHeldPickupOnFocusedInteractable()
        {
            DoorKey heldKey = GetHeldDoorKey();
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

            InteractionStarted?.Invoke();
            door.Interact(this);
            return true;
        }

        private bool TryPrimaryUseHeldPickup()
        {
            IPrimaryUseInteractable primaryUseInteractable = GetHeldPrimaryUseInteractable();

            if (primaryUseInteractable == null || !primaryUseInteractable.CanPrimaryUse(this))
            {
                return false;
            }

            InteractionStarted?.Invoke();
            primaryUseInteractable.PrimaryUse(this);
            return true;
        }

        private bool TryUseHeldPickupOnDoorBlocker()
        {
            return TryBlockHeldPickupOnFocusedDoor(heldPickup);
        }

        private bool TryBlockHeldPickupOnFocusedDoor(Pickupable pickup)
        {
            DoorBlockerChair blocker = pickup == heldPickup
                ? GetHeldDoorBlocker()
                : pickup != null
                    ? pickup.GetComponentInChildren<DoorBlockerChair>()
                    : null;
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

            InteractionStarted?.Invoke();
            if (heldPickup == pickup)
            {
                heldPickup = null;
                ClearInventorySlot(pickup);
                TrySelectMatchingInventoryPickup(pickup);
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
            ClearInventorySlot(releasedPickup);
            releaseButtonWasHeld = false;

            if (throwPickup)
            {
                ThrowStarted?.Invoke();
                Vector3 throwVelocity = CalculateThrowVelocity(1f);
                releasedPickup.Throw(throwVelocity, playerColliders);
                HideThrowArc();
                QueueMatchingInventoryPickup(releasedPickup, autoEquipAfterThrowDelay);
                return;
            }

            if (TryGetPlacementPose(releasedPickup, out Vector3 placementPosition, out Quaternion placementRotation, out bool foundPlacementSurface, out bool shouldSleepAfterPlacement))
            {
                DropStarted?.Invoke();
                releasedPickup.Place(placementPosition, placementRotation, shouldSleepAfterPlacement);
                QueueMatchingInventoryPickup(releasedPickup, autoEquipAfterDropDelay);
            }
            else if (foundPlacementSurface)
            {
                heldPickup = releasedPickup;
                inventorySlots[activeInventorySlot] = releasedPickup;
            }
            else
            {
                DropStarted?.Invoke();
                releasedPickup.Drop();
                QueueMatchingInventoryPickup(releasedPickup, autoEquipAfterDropDelay);
            }

            HideThrowArc();
        }

        private void DropHeldPickupForReplacement()
        {
            if (heldPickup == null)
            {
                return;
            }

            Pickupable replacedPickup = heldPickup;
            heldPickup = null;
            ClearInventorySlot(replacedPickup);
            releaseButtonWasHeld = false;
            HideThrowArc();
            replacedPickup.Drop();
        }

        private void DropInventoryForDisable()
        {
            EnsureInventorySlots();

            Vector3 dropPosition = ViewTransform.position + ViewTransform.forward * holdDistance;
            Quaternion dropRotation = ViewTransform.rotation;

            if (heldPickup != null && FindInventorySlot(heldPickup) < 0)
            {
                heldPickup.transform.SetPositionAndRotation(dropPosition, dropRotation);
                heldPickup.Drop();
            }

            for (int i = 0; i < inventorySlots.Length; i++)
            {
                Pickupable inventoryPickup = inventorySlots[i];
                if (inventoryPickup == null)
                {
                    continue;
                }

                inventoryPickup.transform.SetPositionAndRotation(dropPosition, dropRotation);
                inventoryPickup.Drop();
                inventorySlots[i] = null;
            }

            heldPickup = null;
            releaseButtonWasHeld = false;
        }

        private void SelectInventorySlot(int slotIndex)
        {
            CancelPendingAutoEquip();
            SelectInventorySlot(slotIndex, true);
        }

        private void SelectInventorySlot(int slotIndex, bool playPickupAnimation)
        {
            EnsureInventorySlots();
            if (slotIndex < 0 || slotIndex >= inventorySlots.Length || slotIndex == activeInventorySlot)
            {
                return;
            }

            EndActiveHoldInteraction(false);
            releaseButtonWasHeld = false;
            HideThrowArc();

            Pickupable previousPickup = heldPickup;
            if (previousPickup != null)
            {
                inventorySlots[activeInventorySlot] = previousPickup;
                previousPickup.StoreInInventory(GetInventoryStashRoot());
                heldPickup = null;
            }

            activeInventorySlot = slotIndex;
            Pickupable selectedPickup = inventorySlots[activeInventorySlot];
            if (selectedPickup == null)
            {
                return;
            }

            selectedPickup.EquipFromInventory(this, GetHoldPosition(selectedPickup), ViewTransform.rotation);
            heldPickup = selectedPickup;
            if (playPickupAnimation)
            {
                PickupStarted?.Invoke();
            }
        }

        private bool StashHeldPickupInSlot(int slotIndex)
        {
            EnsureInventorySlots();
            if (heldPickup == null || slotIndex < 0 || slotIndex >= inventorySlots.Length || inventorySlots[slotIndex] != null)
            {
                return false;
            }

            Pickupable pickupToStore = heldPickup;
            ClearInventorySlot(pickupToStore);
            inventorySlots[slotIndex] = pickupToStore;
            heldPickup = null;
            releaseButtonWasHeld = false;
            HideThrowArc();
            pickupToStore.StoreInInventory(GetInventoryStashRoot());
            return true;
        }

        private int GetPressedInventorySlot(Keyboard keyboard)
        {
            if (keyboard == null)
            {
                return -1;
            }

            if (WasPressedThisFrame(keyboard.digit1Key) || WasPressedThisFrame(keyboard.numpad1Key))
            {
                return 0;
            }

            if (inventorySlotCount >= 2 && (WasPressedThisFrame(keyboard.digit2Key) || WasPressedThisFrame(keyboard.numpad2Key)))
            {
                return 1;
            }

            if (inventorySlotCount >= 3 && (WasPressedThisFrame(keyboard.digit3Key) || WasPressedThisFrame(keyboard.numpad3Key)))
            {
                return 2;
            }

            if (inventorySlotCount >= 4 && (WasPressedThisFrame(keyboard.digit4Key) || WasPressedThisFrame(keyboard.numpad4Key)))
            {
                return 3;
            }

            if (inventorySlotCount >= 5 && (WasPressedThisFrame(keyboard.digit5Key) || WasPressedThisFrame(keyboard.numpad5Key)))
            {
                return 4;
            }

            if (inventorySlotCount >= 6 && (WasPressedThisFrame(keyboard.digit6Key) || WasPressedThisFrame(keyboard.numpad6Key)))
            {
                return 5;
            }

            return -1;
        }

        private static bool WasPressedThisFrame(ButtonControl control)
        {
            return control != null && control.wasPressedThisFrame;
        }

        private int FindFirstEmptyInventorySlot()
        {
            EnsureInventorySlots();
            for (int i = 0; i < inventorySlots.Length; i++)
            {
                if (inventorySlots[i] == null)
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindInventorySlot(Pickupable pickupable)
        {
            EnsureInventorySlots();
            if (pickupable == null)
            {
                return -1;
            }

            for (int i = 0; i < inventorySlots.Length; i++)
            {
                if (inventorySlots[i] == pickupable)
                {
                    return i;
                }
            }

            return -1;
        }

        private void TrySelectMatchingInventoryPickup(Pickupable releasedPickup)
        {
            int matchingSlot = FindFirstMatchingInventorySlot(releasedPickup);
            if (matchingSlot >= 0)
            {
                SelectInventorySlot(matchingSlot);
            }
        }

        private void QueueMatchingInventoryPickup(Pickupable releasedPickup, float delay)
        {
            pendingAutoEquipMatchKey = GetPickupMatchKey(releasedPickup);
            if (string.IsNullOrWhiteSpace(pendingAutoEquipMatchKey)
                || FindFirstMatchingInventorySlot(pendingAutoEquipMatchKey) < 0)
            {
                CancelPendingAutoEquip();
                return;
            }

            pendingAutoEquipAt = Time.time + Mathf.Max(0f, delay);
        }

        private void UpdatePendingAutoEquip()
        {
            if (string.IsNullOrWhiteSpace(pendingAutoEquipMatchKey) || Time.time < pendingAutoEquipAt)
            {
                return;
            }

            string matchKey = pendingAutoEquipMatchKey;
            CancelPendingAutoEquip();
            if (heldPickup != null)
            {
                return;
            }

            int matchingSlot = FindFirstMatchingInventorySlot(matchKey);
            if (matchingSlot >= 0)
            {
                SelectInventorySlot(matchingSlot, false);
            }
        }

        private void CancelPendingAutoEquip()
        {
            pendingAutoEquipMatchKey = null;
            pendingAutoEquipAt = 0f;
        }

        private int FindFirstMatchingInventorySlot(Pickupable pickupable)
        {
            return FindFirstMatchingInventorySlot(GetPickupMatchKey(pickupable));
        }

        private int FindFirstMatchingInventorySlot(string matchKey)
        {
            EnsureInventorySlots();
            if (string.IsNullOrWhiteSpace(matchKey))
            {
                return -1;
            }

            for (int i = 0; i < inventorySlots.Length; i++)
            {
                Pickupable inventoryPickup = inventorySlots[i];
                if (inventoryPickup == null)
                {
                    continue;
                }

                if (string.Equals(GetPickupMatchKey(inventoryPickup), matchKey, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string GetPickupMatchKey(Pickupable pickupable)
        {
            if (pickupable == null)
            {
                return null;
            }

            string itemName = pickupable.gameObject.name.Replace("(Clone)", string.Empty).Trim();
            return RemoveUnityDuplicateSuffix(itemName);
        }

        private static string RemoveUnityDuplicateSuffix(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName) || !itemName.EndsWith(")", StringComparison.Ordinal))
            {
                return itemName;
            }

            int openParenIndex = itemName.LastIndexOf(" (", StringComparison.Ordinal);
            if (openParenIndex < 0 || openParenIndex >= itemName.Length - 2)
            {
                return itemName;
            }

            for (int i = openParenIndex + 2; i < itemName.Length - 1; i++)
            {
                if (!char.IsDigit(itemName[i]))
                {
                    return itemName;
                }
            }

            return itemName.Substring(0, openParenIndex).TrimEnd();
        }

        private void ClearInventorySlot(Pickupable pickupable)
        {
            int slotIndex = FindInventorySlot(pickupable);
            if (slotIndex >= 0)
            {
                inventorySlots[slotIndex] = null;
            }
        }

        private void EnsureInventorySlots()
        {
            int clampedSlotCount = Mathf.Clamp(inventorySlotCount, 1, MaximumInventorySlots);
            inventorySlotCount = clampedSlotCount;

            if (inventorySlots != null && inventorySlots.Length == clampedSlotCount)
            {
                activeInventorySlot = Mathf.Clamp(activeInventorySlot, 0, clampedSlotCount - 1);
                return;
            }

            Pickupable[] resizedSlots = new Pickupable[clampedSlotCount];
            if (inventorySlots != null)
            {
                int copyCount = Mathf.Min(inventorySlots.Length, resizedSlots.Length);
                for (int i = 0; i < copyCount; i++)
                {
                    resizedSlots[i] = inventorySlots[i];
                }
            }

            inventorySlots = resizedSlots;
            activeInventorySlot = Mathf.Clamp(activeInventorySlot, 0, clampedSlotCount - 1);
        }

        private Transform GetInventoryStashRoot()
        {
            if (inventoryStashRoot != null)
            {
                return inventoryStashRoot;
            }

            GameObject stashObject = new GameObject("InventoryStash");
            inventoryStashRoot = stashObject.transform;
            inventoryStashRoot.SetParent(transform, false);
            inventoryStashRoot.localPosition = Vector3.zero;
            inventoryStashRoot.localRotation = Quaternion.identity;
            return inventoryStashRoot;
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
            return GetHoldPosition(heldPickup);
        }

        private Vector3 GetHoldPosition(Pickupable pickupable)
        {
            Transform activeHoldPoint = GetHoldPointFor(pickupable);
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
                InteractionStarted?.Invoke();
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
            Ray ray = new Ray(ViewTransform.position, ViewTransform.forward);
            FocusedInteractable = FindBestInteractable(ray);
            HasFocusedInteractable = FocusedInteractable != null;
        }

        private void UpdateInteractionTooltip(Mouse mouse)
        {
            EnsureTooltipView();
            if (tooltipView == null)
            {
                return;
            }

            if (heldPickup != null)
            {
                if (FocusedInteractable != null
                    && TryGetTooltip(FocusedInteractable, InteractionTooltipContext.FocusedInteractable, "Interact", "E", out string heldFocusAction, out string heldFocusKey))
                {
                    tooltipView.Show(heldFocusKey, heldFocusAction);
                    return;
                }

                ShowHeldPickupTooltip(mouse);
                return;
            }

            if (FocusedInteractable != null
                && TryGetTooltip(FocusedInteractable, InteractionTooltipContext.FocusedInteractable, "Interact", "E", out string focusAction, out string focusKey))
            {
                tooltipView.Show(focusKey, focusAction);
                return;
            }

            Ray ray = new Ray(ViewTransform.position, ViewTransform.forward);
            IHoldInteractable holdInteractable = FindBestHoldInteractable(ray);
            if (holdInteractable != null
                && TryGetTooltip(holdInteractable, InteractionTooltipContext.HoldInteractable, "Hold interact", "Hold E", out string holdAction, out string holdKey))
            {
                tooltipView.Show(holdKey, holdAction);
                return;
            }

            tooltipView.Hide();
        }

        private void ShowHeldPickupTooltip(Mouse mouse)
        {
            IPrimaryUseInteractable primaryUseInteractable = GetHeldPrimaryUseInteractable();

            if (mouse != null
                && primaryUseInteractable != null
                && primaryUseInteractable.CanPrimaryUse(this)
                && TryGetTooltip(primaryUseInteractable, InteractionTooltipContext.HeldPrimaryUse, "Use", "Left Mouse", out string primaryAction, out string primaryKey))
            {
                tooltipView.Show(primaryKey, primaryAction);
                return;
            }

            string secondaryAction = releaseButtonWasHeld ? "Release to throw" : "Place";
            string secondaryKey = releaseButtonWasHeld ? "Release Right Mouse" : "Right Mouse";
            if (TryGetTooltip(heldPickup, InteractionTooltipContext.HeldSecondaryUse, secondaryAction, secondaryKey, out string action, out string key))
            {
                tooltipView.Show(key, action);
                return;
            }

            tooltipView.Hide();
        }

        private bool TryGetTooltip(
            object tooltipSource,
            InteractionTooltipContext context,
            string fallbackAction,
            string fallbackKey,
            out string actionText,
            out string keyText)
        {
            IInteractionTooltipProvider provider = GetTooltipProvider(tooltipSource);
            if (provider != null && provider.TryGetInteractionTooltip(this, context, out actionText, out keyText))
            {
                return true;
            }

            actionText = GetDefaultTooltipAction(tooltipSource, context, fallbackAction);
            keyText = fallbackKey;
            return !string.IsNullOrWhiteSpace(actionText) && !string.IsNullOrWhiteSpace(keyText);
        }

        private static IInteractionTooltipProvider GetTooltipProvider(object tooltipSource)
        {
            if (tooltipSource is IInteractionTooltipProvider directProvider)
            {
                return directProvider;
            }

            return tooltipSource is Component component
                ? component.GetComponentInChildren<IInteractionTooltipProvider>()
                : null;
        }

        private static string GetDefaultTooltipAction(object tooltipSource, InteractionTooltipContext context, string fallbackAction)
        {
            if (tooltipSource is Pickupable)
            {
                return context == InteractionTooltipContext.HeldSecondaryUse ? fallbackAction : "Pick up";
            }

            if (tooltipSource is Door door)
            {
                if (door.IsBlocked)
                {
                    return "Blocked";
                }

                if (door.IsLocked)
                {
                    return "Unlock";
                }

                return door.IsOpen ? "Close" : "Open";
            }

            if (tooltipSource is LightSwitch)
            {
                return "Toggle light";
            }

            if (tooltipSource is Doorbell)
            {
                return "Ring doorbell";
            }

            if (tooltipSource is ClosetHideSpot closet)
            {
                return closet.HasHiddenPlayer ? "Exit hiding spot" : "Hide";
            }

            if (tooltipSource is ClosetDoorPair closetDoors)
            {
                return closetDoors.HideSpot != null && closetDoors.HideSpot.HasHiddenPlayer
                    ? "Exit hiding spot"
                    : "Hide";
            }

            if (tooltipSource is SlidingCupboardCompartment)
            {
                return "Slide compartment";
            }

            if (tooltipSource is Flashlight)
            {
                return "Toggle flashlight";
            }

            if (tooltipSource is PhotoCamera)
            {
                return "Take photo";
            }

            if (tooltipSource is SecurityCamera)
            {
                return "Attach camera";
            }

            if (tooltipSource is Beartrap)
            {
                return context == InteractionTooltipContext.HoldInteractable ? "Free yourself" : "Set trap";
            }

            if (tooltipSource is ReadableBook)
            {
                return "Read";
            }

            if (tooltipSource is WritableNotebook)
            {
                return "Write";
            }

            if (tooltipSource is Crowbar)
            {
                return "Pry";
            }

            if (tooltipSource is IHoldInteractable)
            {
                return fallbackAction;
            }

            return fallbackAction;
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
                    || AllowsSecurityCameraWallPickupThroughBlocker(blocker, candidateDistance - blockerHit.distance, candidateInteractable)
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

        private static bool AllowsSecurityCameraWallPickupThroughBlocker(
            Collider blocker,
            float blockerLeadDistance,
            object candidateInteractable)
        {
            if (candidateInteractable is not Component candidateComponent)
            {
                return false;
            }

            Pickupable pickupable = candidateComponent as Pickupable
                ?? candidateComponent.GetComponentInParent<Pickupable>()
                ?? candidateComponent.GetComponentInChildren<Pickupable>();
            SecurityCamera securityCamera = pickupable != null
                ? pickupable.GetComponentInChildren<SecurityCamera>()
                : candidateComponent.GetComponentInParent<SecurityCamera>() ?? candidateComponent.GetComponentInChildren<SecurityCamera>();

            return securityCamera != null && securityCamera.ShouldAllowPickupThroughBlocker(blocker, blockerLeadDistance);
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
            throwArcMaterial = CreateThrowArcMaterial();
            throwArcRenderer.sharedMaterial = throwArcMaterial;
        }

        private void EnsureTooltipView()
        {
            if (tooltipView != null)
            {
                return;
            }

            tooltipView = FindAnyObjectByType<InteractionTooltipView>();
            if (tooltipView == null)
            {
                tooltipView = InteractionTooltipView.CreateRuntimeTooltip();
            }
        }

        private void EnsureInventoryHudView()
        {
            if (inventoryHudView != null)
            {
                inventoryHudView.SetInteractor(this);
                return;
            }

            inventoryHudView = FindAnyObjectByType<PlayerInventoryHudView>();
            if (inventoryHudView == null)
            {
                inventoryHudView = PlayerInventoryHudView.CreateRuntimeHud(this);
                return;
            }

            inventoryHudView.SetInteractor(this);
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

        private DoorKey GetHeldDoorKey()
        {
            RefreshHeldPickupComponentCache();
            return cachedHeldDoorKey;
        }

        private DoorBlockerChair GetHeldDoorBlocker()
        {
            RefreshHeldPickupComponentCache();
            return cachedHeldDoorBlocker;
        }

        private IPrimaryUseInteractable GetHeldPrimaryUseInteractable()
        {
            RefreshHeldPickupComponentCache();
            return cachedHeldPrimaryUseInteractable;
        }

        private void RefreshHeldPickupComponentCache()
        {
            if (heldPickup == null)
            {
                cachedHeldPickup = null;
                cachedHeldDoorKey = null;
                cachedHeldDoorBlocker = null;
                cachedHeldPrimaryUseInteractable = null;
                return;
            }

            if (ReferenceEquals(cachedHeldPickup, heldPickup))
            {
                return;
            }

            cachedHeldPickup = heldPickup;
            cachedHeldDoorKey = heldPickup.GetComponentInChildren<DoorKey>();
            cachedHeldDoorBlocker = heldPickup.GetComponentInChildren<DoorBlockerChair>();
            cachedHeldPrimaryUseInteractable = heldPickup.GetComponentInChildren<IPrimaryUseInteractable>();
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

        private void ReleaseThrowArcResources()
        {
            if (throwArcRenderer != null && throwArcMaterial != null && throwArcRenderer.sharedMaterial == throwArcMaterial)
            {
                throwArcRenderer.sharedMaterial = null;
            }

            if (throwArcMaterial != null)
            {
                Destroy(throwArcMaterial);
                throwArcMaterial = null;
            }
        }

        private void Reset()
        {
            viewCamera = GetComponent<Camera>();
        }
    }
}
