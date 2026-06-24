using System.Collections.Generic;
using Neighbor.Main.Features.Neighbor;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class Pickupable : MonoBehaviour, IInteractable
    {
        public enum HoldPointSize
        {
            Small,
            Medium,
            Large
        }

        [Header("Pickup")]
        [SerializeField, Min(0f)] private float maximumPickupMass = 20f;
        [SerializeField] private HoldPointSize holdPointSize = HoldPointSize.Medium;
        [SerializeField, Min(0f)] private float heldDrag = 8f;
        [SerializeField, Min(0f)] private float heldAngularDrag = 10f;
        [SerializeField] private bool alignToCameraWhileHeld = true;
        [SerializeField] private bool disableCollidersWhileHeld = true;
        [SerializeField, Min(0f)] private float supportWakePadding = 0.12f;
        [SerializeField, Min(0f)] private float postThrowPlayerCollisionIgnoreTime = 0.35f;
        [SerializeField, Min(0f)] private float recentThrowImpactWindow = 5f;

        [Header("Home Location")]
        [SerializeField] private bool trackHomeLocation = true;
        [SerializeField, Min(0f)] private float homePositionTolerance = 0.65f;
        [SerializeField, Min(0f)] private float homeRotationTolerance = 35f;
        [SerializeField, Min(0f)] private float foreignHomeRadius = 0.75f;
        [SerializeField, Min(0f)] private float neighborHomeDisturbanceGraceTime = 2f;
        [SerializeField, Min(0f)] private float neighborHomeDisturbanceMaximumSpeed = 0.75f;

        [Header("Pickup Audio")]
        [SerializeField] private AudioClip[] pickupClips;
        [SerializeField, Range(0f, 1f)] private float pickupVolume = 0.65f;
        [SerializeField, Min(0f)] private float pickupPitchRandomness = 0.05f;
        [SerializeField, Min(0f)] private float pickupAudioMinDistance = 0.4f;
        [SerializeField, Min(0.1f)] private float pickupAudioMaxDistance = 8f;

        [Header("Placement Audio")]
        [SerializeField] private AudioClip[] placementClips;
        [SerializeField, Range(0f, 1f)] private float placementVolume = 0.55f;
        [SerializeField, Min(0f)] private float placementPitchRandomness = 0.04f;
        [SerializeField, Min(0f)] private float placementAudioMinDistance = 0.4f;
        [SerializeField, Min(0.1f)] private float placementAudioMaxDistance = 8f;

        private static readonly List<Pickupable> ActivePickups = new();

        private Rigidbody body;
        private Collider[] ownColliders;
        private Renderer[] ownRenderers;
        private IPickupInteractionOverride[] pickupInteractionOverrides;
        private IPickupLifecycleReceiver[] lifecycleReceivers;
        private bool[] originalColliderEnabled;
        private bool[] inventoryColliderEnabled;
        private bool[] originalRendererEnabled;
        private readonly Collider[] supportWakeHits = new Collider[24];
        private readonly Vector3[] colliderBoundsCorners = new Vector3[8];
        private bool wasUsingGravity;
        private bool wasKinematic;
        private float originalDrag;
        private float originalAngularDrag;
        private CollisionDetectionMode originalCollisionDetection;
        private RigidbodyInterpolation originalInterpolation;
        private Collider[] ignoredPlayerColliders;
        private Collider[] ignoredHeldColliders;
        private float restorePlayerCollisionTime;
        private float recentlyThrownUntilTime;
        private float speedBeforePhysicsStep;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private Transform homeParent;
        private bool hasHomeLocation;
        private GameObject homeDisplacementInstigator;
        private bool homeDisplacementInstigatedByNeighbor;
        private float homeDisplacementAttributionUntilTime;

        public bool IsHeld { get; private set; }
        public bool IsInventoryStored { get; private set; }
        public bool IsRecentlyThrown => Time.time < recentlyThrownUntilTime;
        public bool TracksHomeLocation => trackHomeLocation && hasHomeLocation;
        public Vector3 HomePosition => homePosition;
        public Quaternion HomeRotation => homeRotation;
        public float HomePositionTolerance => homePositionTolerance;
        public float ForeignHomeRadius => foreignHomeRadius;
        public bool IsAtHome => !CheckMissingFromHome();
        public bool IsMissingFromHome => CheckMissingFromHome();
        public bool IsHomeDisplacementNeighborInstigated
        {
            get
            {
                RefreshHomeDisplacementAttribution();
                return homeDisplacementInstigatedByNeighbor && CheckMissingFromHome();
            }
        }

        public bool NeedsHomeRestoration => TracksHomeLocation
            && !IsHeld
            && !IsInventoryStored
            && !IsHomeDisplacementNeighborInstigated
            && IsMissingFromHome;
        public HoldPointSize AssignedHoldPointSize => holdPointSize;
        public Vector3 ThrowOrigin => body != null ? body.worldCenterOfMass : transform.position;
        public static IReadOnlyList<Pickupable> Pickups => ActivePickups;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            ownColliders = GetComponentsInChildren<Collider>();
            ownRenderers = GetComponentsInChildren<Renderer>();
            pickupInteractionOverrides = GetComponentsInChildren<IPickupInteractionOverride>();
            lifecycleReceivers = GetComponentsInChildren<IPickupLifecycleReceiver>();
            CaptureHomeLocation();
            originalColliderEnabled = new bool[ownColliders.Length];
            inventoryColliderEnabled = new bool[ownColliders.Length];
            originalRendererEnabled = new bool[ownRenderers.Length];

            for (int i = 0; i < ownColliders.Length; i++)
            {
                originalColliderEnabled[i] = ownColliders[i] != null && ownColliders[i].enabled;
                inventoryColliderEnabled[i] = originalColliderEnabled[i];
            }

            for (int i = 0; i < ownRenderers.Length; i++)
            {
                originalRendererEnabled[i] = ownRenderers[i] != null && ownRenderers[i].enabled;
            }
        }

        private void OnEnable()
        {
            if (!ActivePickups.Contains(this))
            {
                ActivePickups.Add(this);
            }
        }

        private void OnDisable()
        {
            if (IsHeld)
            {
                RestorePhysics();
            }

            RestoreHeldCollisionIgnores();
            RestoreIgnoredPlayerCollisions();
            ActivePickups.Remove(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActivePickups()
        {
            ActivePickups.Clear();
        }

        private void Update()
        {
            RefreshHomeDisplacementAttribution();
            if (ignoredPlayerColliders != null && Time.time >= restorePlayerCollisionTime)
            {
                RestoreIgnoredPlayerCollisions();
            }
        }

        private void FixedUpdate()
        {
            speedBeforePhysicsStep = body != null ? body.linearVelocity.magnitude : 0f;
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryMarkNeighborCollisionDisturbance(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            TryMarkNeighborCollisionDisturbance(collision);
        }

        public bool CanInteract(PlayerInteractor interactor)
        {
            if (IsHeld || IsInventoryStored || body == null || body.mass > maximumPickupMass)
            {
                return false;
            }

            if (pickupInteractionOverrides == null)
            {
                return true;
            }

            foreach (IPickupInteractionOverride pickupInteractionOverride in pickupInteractionOverrides)
            {
                if (pickupInteractionOverride != null && !pickupInteractionOverride.CanPickup(interactor))
                {
                    return false;
                }
            }

            return true;
        }

        public void Interact(PlayerInteractor interactor)
        {
            interactor.Pickup(this);
        }

        public void Pickup(PlayerInteractor interactor, bool playPickupFeedback = true)
        {
            Pickup(interactor, null, playPickupFeedback);
        }

        public void Pickup(PlayerInteractor interactor, Collider[] heldCollisionIgnoreColliders, bool playPickupFeedback = true)
        {
            if (IsHeld || body == null)
            {
                return;
            }

            recentlyThrownUntilTime = 0f;
            NotifyPickupStarted(interactor);
            IsHeld = true;
            CapturePhysicsState();
            IgnoreHeldCollisions(heldCollisionIgnoreColliders);

            ClearBodyVelocity();
            body.useGravity = false;
            body.isKinematic = true;
            body.linearDamping = heldDrag;
            body.angularDamping = heldAngularDrag;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            WakeSupportedBodiesAfterColliderStateChange(false);
            if (playPickupFeedback)
            {
                PlayPickupSound();
            }
        }

        public void MoveHeld(
            Vector3 targetPosition,
            Quaternion targetRotation,
            float followStrength,
            float rotationStrength,
            float maxVelocity)
        {
            if (!IsHeld || body == null)
            {
                return;
            }

            float followStep = Mathf.Clamp01(followStrength * Time.fixedDeltaTime);
            Vector3 nextPosition = Vector3.Lerp(body.position, targetPosition, followStep);
            if (maxVelocity > 0f)
            {
                Vector3 movement = nextPosition - body.position;
                float maximumStepDistance = maxVelocity * Time.fixedDeltaTime;
                if (movement.sqrMagnitude > maximumStepDistance * maximumStepDistance)
                {
                    nextPosition = body.position + movement.normalized * maximumStepDistance;
                }
            }

            body.MovePosition(nextPosition);

            if (!alignToCameraWhileHeld)
            {
                return;
            }

            float rotationStep = Mathf.Clamp01(rotationStrength * Time.fixedDeltaTime);
            body.MoveRotation(Quaternion.Slerp(body.rotation, targetRotation, rotationStep));
        }

        public void SnapHeldPose(Vector3 targetPosition, Quaternion targetRotation)
        {
            if (!IsHeld || body == null)
            {
                return;
            }

            SetBodyPose(targetPosition, targetRotation);
        }

        public void Drop()
        {
            recentlyThrownUntilTime = 0f;
            RestoreInventoryState();
            RestorePhysics();
        }

        public void Place(Vector3 position, Quaternion rotation, bool sleepAfterPlacing = true)
        {
            recentlyThrownUntilTime = 0f;
            RestoreInventoryState();
            transform.SetPositionAndRotation(position, rotation);

            if (body == null)
            {
                RestoreHeldCollisionIgnores();
                return;
            }

            if (IsHeld)
            {
                IsHeld = false;
                body.useGravity = wasUsingGravity;
                body.isKinematic = wasKinematic;
                body.linearDamping = originalDrag;
                body.angularDamping = originalAngularDrag;
                body.collisionDetectionMode = originalCollisionDetection;
                body.interpolation = originalInterpolation;
            }

            body.position = position;
            body.rotation = rotation;
            ClearBodyVelocity();
            SetHeldColliderState(true);
            RestoreHeldCollisionIgnores();

            if (sleepAfterPlacing)
            {
                body.Sleep();
            }
            else
            {
                body.WakeUp();
            }

            PlayPlacementSound();
            NotifyPickupPlaced();
        }

        public void Throw(Vector3 velocity)
        {
            RestoreInventoryState();
            RestorePhysics();
            MarkRecentlyThrown();
            SetBodyLinearVelocity(velocity);
        }

        public void Throw(Vector3 velocity, Collider[] playerColliders)
        {
            RestoreInventoryState();
            RestorePhysics();
            IgnorePlayerCollisionsTemporarily(playerColliders);
            MarkRecentlyThrown();
            SetBodyLinearVelocity(velocity);
        }

        public void StoreInInventory(Transform inventoryParent)
        {
            if (body == null || IsInventoryStored)
            {
                return;
            }

            recentlyThrownUntilTime = 0f;
            if (IsHeld)
            {
                IsHeld = false;
                SetHeldColliderState(true);
                RestoreHeldCollisionIgnores();
                RestoreCapturedPhysicsState();
            }
            else
            {
                CapturePhysicsState();
            }

            ClearBodyVelocity();
            body.useGravity = false;
            body.isKinematic = true;
            body.Sleep();

            SetInventoryVisibility(false);
            if (inventoryParent != null)
            {
                transform.SetParent(inventoryParent, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                SetBodyPose(transform.position, transform.rotation);
            }

            IsInventoryStored = true;
        }

        public void EquipFromInventory(PlayerInteractor interactor)
        {
            EquipFromInventory(interactor, transform.position, transform.rotation);
        }

        public void EquipFromInventory(PlayerInteractor interactor, Vector3 equipPosition, Quaternion equipRotation)
        {
            RestoreInventoryState();
            SetBodyPose(equipPosition, equipRotation);
            Pickup(interactor, false);
        }

        public Bounds GetPlacementBounds()
        {
            if (TryGetRendererBounds(out Bounds rendererBounds))
            {
                return rendererBounds;
            }

            if (TryGetColliderBounds(out Bounds colliderBounds))
            {
                return colliderBounds;
            }

            return new Bounds(transform.position, Vector3.one * 0.25f);
        }

        public bool TryGetForeignHome(out Pickupable homePickup)
        {
            homePickup = null;
            if (!TracksHomeLocation || IsAtHome)
            {
                return false;
            }

            for (int i = 0; i < ActivePickups.Count; i++)
            {
                Pickupable candidate = ActivePickups[i];
                if (candidate == null
                    || candidate == this
                    || !candidate.TracksHomeLocation
                    || candidate.IsHeld
                    || candidate.IsInventoryStored)
                {
                    continue;
                }

                float radius = Mathf.Max(foreignHomeRadius, candidate.ForeignHomeRadius);
                if ((transform.position - candidate.HomePosition).sqrMagnitude <= radius * radius)
                {
                    homePickup = candidate;
                    return true;
                }
            }

            return false;
        }

        public void PlaceAtHome(bool sleepAfterPlacing = true)
        {
            if (!TracksHomeLocation)
            {
                return;
            }

            Place(homePosition, homeRotation, sleepAfterPlacing);
            transform.SetParent(homeParent, true);
            SetBodyPose(homePosition, homeRotation);
        }

        public void MarkNeighborHomeDisplacement(GameObject neighborInstigator)
        {
            if (!TracksHomeLocation
                || neighborInstigator == null
                || neighborInstigator.GetComponentInParent<NeighborBrain>() == null)
            {
                return;
            }

            homeDisplacementInstigator = neighborInstigator;
            homeDisplacementAttributionUntilTime = Time.time + neighborHomeDisturbanceGraceTime;
            if (CheckMissingFromHome())
            {
                homeDisplacementInstigatedByNeighbor = true;
            }
        }

        private void CaptureHomeLocation()
        {
            if (!trackHomeLocation)
            {
                hasHomeLocation = false;
                return;
            }

            homeParent = transform.parent;
            homePosition = transform.position;
            homeRotation = transform.rotation;
            hasHomeLocation = true;
        }

        private bool CheckMissingFromHome()
        {
            if (!TracksHomeLocation)
            {
                return false;
            }

            if (IsHeld || IsInventoryStored)
            {
                return true;
            }

            float positionTolerance = Mathf.Max(0f, homePositionTolerance);
            if ((transform.position - homePosition).sqrMagnitude > positionTolerance * positionTolerance)
            {
                return true;
            }

            return Quaternion.Angle(transform.rotation, homeRotation) > Mathf.Max(0f, homeRotationTolerance);
        }

        private void TryMarkNeighborCollisionDisturbance(Collision collision)
        {
            if (collision == null
                || collision.collider == null
                || speedBeforePhysicsStep > neighborHomeDisturbanceMaximumSpeed)
            {
                return;
            }

            NeighborBrain neighbor = collision.collider.GetComponentInParent<NeighborBrain>();
            if (neighbor != null)
            {
                MarkNeighborHomeDisplacement(neighbor.gameObject);
            }
        }

        private void RefreshHomeDisplacementAttribution()
        {
            if (homeDisplacementInstigator == null)
            {
                homeDisplacementInstigatedByNeighbor = false;
                return;
            }

            if (!TracksHomeLocation)
            {
                ClearHomeDisplacementAttribution();
                return;
            }

            if (!CheckMissingFromHome())
            {
                if (Time.time > homeDisplacementAttributionUntilTime)
                {
                    ClearHomeDisplacementAttribution();
                }

                return;
            }

            if (homeDisplacementInstigatedByNeighbor || Time.time <= homeDisplacementAttributionUntilTime)
            {
                homeDisplacementInstigatedByNeighbor = true;
                return;
            }

            ClearHomeDisplacementAttribution();
        }

        private void ClearHomeDisplacementAttribution()
        {
            homeDisplacementInstigator = null;
            homeDisplacementInstigatedByNeighbor = false;
            homeDisplacementAttributionUntilTime = 0f;
        }

        private bool TryGetRendererBounds(out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            if (ownRenderers == null)
            {
                return false;
            }

            foreach (Renderer ownRenderer in ownRenderers)
            {
                if (ownRenderer == null || !ownRenderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = ownRenderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(ownRenderer.bounds);
            }

            return hasBounds;
        }

        private bool TryGetColliderBounds(out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            if (ownColliders == null)
            {
                return false;
            }

            foreach (Collider ownCollider in ownColliders)
            {
                if (ownCollider == null || !TryGetColliderWorldBounds(ownCollider, out Bounds colliderBounds))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = colliderBounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(colliderBounds);
            }

            return hasBounds;
        }

        private bool TryGetColliderWorldBounds(Collider collider, out Bounds bounds)
        {
            if (collider.enabled && collider.bounds.size.sqrMagnitude > 0f)
            {
                bounds = collider.bounds;
                return true;
            }

            if (collider is BoxCollider boxCollider)
            {
                return TryGetTransformedLocalBounds(boxCollider.transform, new Bounds(boxCollider.center, boxCollider.size), out bounds);
            }

            if (collider is SphereCollider sphereCollider)
            {
                Vector3 center = sphereCollider.transform.TransformPoint(sphereCollider.center);
                float scale = Mathf.Max(
                    Mathf.Abs(sphereCollider.transform.lossyScale.x),
                    Mathf.Abs(sphereCollider.transform.lossyScale.y),
                    Mathf.Abs(sphereCollider.transform.lossyScale.z));
                bounds = new Bounds(center, Vector3.one * (sphereCollider.radius * scale * 2f));
                return true;
            }

            if (collider is CapsuleCollider capsuleCollider)
            {
                return TryGetCapsuleBounds(capsuleCollider, out bounds);
            }

            if (collider is MeshCollider meshCollider && meshCollider.sharedMesh != null)
            {
                return TryGetTransformedLocalBounds(meshCollider.transform, meshCollider.sharedMesh.bounds, out bounds);
            }

            bounds = default;
            return false;
        }

        private bool TryGetCapsuleBounds(CapsuleCollider capsuleCollider, out Bounds bounds)
        {
            Vector3 scale = capsuleCollider.transform.lossyScale;
            float radiusScale;
            float heightScale;
            Vector3 localSize = Vector3.zero;
            switch (capsuleCollider.direction)
            {
                case 0:
                    radiusScale = Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                    heightScale = Mathf.Abs(scale.x);
                    localSize = new Vector3(
                        Mathf.Max(capsuleCollider.height * heightScale, capsuleCollider.radius * radiusScale * 2f),
                        capsuleCollider.radius * radiusScale * 2f,
                        capsuleCollider.radius * radiusScale * 2f);
                    break;
                case 2:
                    radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                    heightScale = Mathf.Abs(scale.z);
                    localSize = new Vector3(
                        capsuleCollider.radius * radiusScale * 2f,
                        capsuleCollider.radius * radiusScale * 2f,
                        Mathf.Max(capsuleCollider.height * heightScale, capsuleCollider.radius * radiusScale * 2f));
                    break;
                default:
                    radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                    heightScale = Mathf.Abs(scale.y);
                    localSize = new Vector3(
                        capsuleCollider.radius * radiusScale * 2f,
                        Mathf.Max(capsuleCollider.height * heightScale, capsuleCollider.radius * radiusScale * 2f),
                        capsuleCollider.radius * radiusScale * 2f);
                    break;
            }

            bounds = new Bounds(capsuleCollider.transform.TransformPoint(capsuleCollider.center), localSize);
            return true;
        }

        private bool TryGetTransformedLocalBounds(Transform owner, Bounds localBounds, out Bounds bounds)
        {
            if (owner == null || localBounds.size.sqrMagnitude <= 0f)
            {
                bounds = default;
                return false;
            }

            Vector3 min = localBounds.min;
            Vector3 max = localBounds.max;
            colliderBoundsCorners[0] = new Vector3(min.x, min.y, min.z);
            colliderBoundsCorners[1] = new Vector3(max.x, min.y, min.z);
            colliderBoundsCorners[2] = new Vector3(min.x, max.y, min.z);
            colliderBoundsCorners[3] = new Vector3(max.x, max.y, min.z);
            colliderBoundsCorners[4] = new Vector3(min.x, min.y, max.z);
            colliderBoundsCorners[5] = new Vector3(max.x, min.y, max.z);
            colliderBoundsCorners[6] = new Vector3(min.x, max.y, max.z);
            colliderBoundsCorners[7] = new Vector3(max.x, max.y, max.z);

            bounds = new Bounds(owner.TransformPoint(colliderBoundsCorners[0]), Vector3.zero);
            for (int i = 1; i < colliderBoundsCorners.Length; i++)
            {
                bounds.Encapsulate(owner.TransformPoint(colliderBoundsCorners[i]));
            }

            return true;
        }

        private void RestorePhysics()
        {
            if (!IsHeld || body == null)
            {
                return;
            }

            IsHeld = false;
            SetHeldColliderState(true);
            RestoreHeldCollisionIgnores();
            RestoreCapturedPhysicsState();
        }

        private void CapturePhysicsState()
        {
            if (body == null)
            {
                return;
            }

            wasUsingGravity = body.useGravity;
            wasKinematic = body.isKinematic;
            originalDrag = body.linearDamping;
            originalAngularDrag = body.angularDamping;
            originalCollisionDetection = body.collisionDetectionMode;
            originalInterpolation = body.interpolation;
        }

        private void RestoreCapturedPhysicsState()
        {
            if (body == null)
            {
                return;
            }

            body.useGravity = wasUsingGravity;
            body.isKinematic = wasKinematic;
            body.linearDamping = originalDrag;
            body.angularDamping = originalAngularDrag;
            body.collisionDetectionMode = originalCollisionDetection;
            body.interpolation = originalInterpolation;
        }

        private void ClearBodyVelocity()
        {
            RigidbodyVelocityUtility.ClearIfDynamic(body);
        }

        private void SetBodyLinearVelocity(Vector3 velocity)
        {
            RigidbodyVelocityUtility.SetLinearIfDynamic(body, velocity);
        }

        private void MarkRecentlyThrown()
        {
            recentlyThrownUntilTime = Time.time + recentThrowImpactWindow;
        }

        private void SetBodyPose(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            if (body == null)
            {
                return;
            }

            body.position = position;
            body.rotation = rotation;
        }

        private void SetHeldColliderState(bool restore)
        {
            if (!disableCollidersWhileHeld || ownColliders == null)
            {
                return;
            }

            for (int i = 0; i < ownColliders.Length; i++)
            {
                Collider ownCollider = ownColliders[i];
                if (ownCollider == null)
                {
                    continue;
                }

                if (restore)
                {
                    ownCollider.enabled = originalColliderEnabled[i];
                    continue;
                }

                originalColliderEnabled[i] = ownCollider.enabled;
                ownCollider.enabled = false;
            }
        }

        private void SetInventoryVisibility(bool visible)
        {
            if (ownRenderers != null)
            {
                for (int i = 0; i < ownRenderers.Length; i++)
                {
                    Renderer ownRenderer = ownRenderers[i];
                    if (ownRenderer == null)
                    {
                        continue;
                    }

                    if (visible)
                    {
                        ownRenderer.enabled = originalRendererEnabled[i];
                        continue;
                    }

                    originalRendererEnabled[i] = ownRenderer.enabled;
                    ownRenderer.enabled = false;
                }
            }

            if (ownColliders != null)
            {
                for (int i = 0; i < ownColliders.Length; i++)
                {
                    Collider ownCollider = ownColliders[i];
                    if (ownCollider == null)
                    {
                        continue;
                    }

                    if (visible)
                    {
                        ownCollider.enabled = inventoryColliderEnabled[i];
                        continue;
                    }

                    inventoryColliderEnabled[i] = ownCollider.enabled;
                    ownCollider.enabled = false;
                }
            }
        }

        private void RestoreInventoryState()
        {
            if (!IsInventoryStored)
            {
                return;
            }

            IsInventoryStored = false;
            transform.SetParent(null, true);
            SetInventoryVisibility(true);
            RestoreCapturedPhysicsState();
            body?.WakeUp();
        }

        private void PlayPickupSound()
        {
            PlayOneShot3D(pickupClips, pickupVolume, pickupPitchRandomness, pickupAudioMinDistance, pickupAudioMaxDistance, "Pickup3DAudio");
        }

        private void NotifyPickupStarted(PlayerInteractor interactor)
        {
            if (lifecycleReceivers == null)
            {
                return;
            }

            foreach (IPickupLifecycleReceiver lifecycleReceiver in lifecycleReceivers)
            {
                lifecycleReceiver?.OnPickupStarted(this, interactor);
            }
        }

        private void NotifyPickupPlaced()
        {
            if (lifecycleReceivers == null)
            {
                return;
            }

            foreach (IPickupLifecycleReceiver lifecycleReceiver in lifecycleReceivers)
            {
                lifecycleReceiver?.OnPickupPlaced(this);
            }
        }

        private void PlayPlacementSound()
        {
            PlayOneShot3D(placementClips, placementVolume, placementPitchRandomness, placementAudioMinDistance, placementAudioMaxDistance, "Placement3DAudio");
        }

        private void PlayOneShot3D(
            AudioClip[] clips,
            float volume,
            float pitchRandomness,
            float minDistance,
            float maxDistance,
            string objectName)
        {
            if (clips == null || clips.Length == 0)
            {
                return;
            }

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null)
            {
                return;
            }

            GameObject audioObject = new GameObject(objectName);
            audioObject.transform.position = ThrowOrigin;

            AudioSource audioSource = audioObject.AddComponent<AudioSource>();
            audioSource.clip = clip;
            audioSource.volume = volume;
            audioSource.pitch = Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = minDistance;
            audioSource.maxDistance = maxDistance;
            audioSource.dopplerLevel = 0.1f;
            audioSource.Play();

            Destroy(audioObject, clip.length / Mathf.Max(0.01f, audioSource.pitch) + 0.05f);
        }

        private void IgnorePlayerCollisionsTemporarily(Collider[] playerColliders)
        {
            if (postThrowPlayerCollisionIgnoreTime <= 0f || ownColliders == null || playerColliders == null)
            {
                return;
            }

            RestoreIgnoredPlayerCollisions();
            ignoredPlayerColliders = playerColliders;
            restorePlayerCollisionTime = Time.time + postThrowPlayerCollisionIgnoreTime;
            SetPlayerCollisionIgnored(true);
        }

        private void RestoreIgnoredPlayerCollisions()
        {
            if (ignoredPlayerColliders == null)
            {
                return;
            }

            SetPlayerCollisionIgnored(false);
            ignoredPlayerColliders = null;
        }

        private void IgnoreHeldCollisions(Collider[] holderColliders)
        {
            if (holderColliders == null || holderColliders.Length == 0)
            {
                return;
            }

            RestoreHeldCollisionIgnores();
            ignoredHeldColliders = holderColliders;
            SetHeldCollisionIgnored(true);
        }

        private void RestoreHeldCollisionIgnores()
        {
            if (ignoredHeldColliders == null)
            {
                return;
            }

            SetHeldCollisionIgnored(false);
            ignoredHeldColliders = null;
        }

        private void SetHeldCollisionIgnored(bool ignore)
        {
            if (ownColliders == null || ignoredHeldColliders == null)
            {
                return;
            }

            foreach (Collider ownCollider in ownColliders)
            {
                if (ownCollider == null)
                {
                    continue;
                }

                foreach (Collider heldCollider in ignoredHeldColliders)
                {
                    if (heldCollider == null || ownCollider == heldCollider)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(ownCollider, heldCollider, ignore);
                }
            }
        }

        private void SetPlayerCollisionIgnored(bool ignore)
        {
            if (ownColliders == null || ignoredPlayerColliders == null)
            {
                return;
            }

            foreach (Collider ownCollider in ownColliders)
            {
                if (ownCollider == null)
                {
                    continue;
                }

                foreach (Collider playerCollider in ignoredPlayerColliders)
                {
                    if (playerCollider == null || ownCollider == playerCollider)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(ownCollider, playerCollider, ignore);
                }
            }
        }

        private void WakeSupportedBodiesAfterColliderStateChange(bool restore)
        {
            if (restore || !TryGetColliderBounds(out Bounds bounds))
            {
                SetHeldColliderState(restore);
                return;
            }

            Vector3 halfExtents = bounds.extents + Vector3.one * supportWakePadding;
            int hitCount = Physics.OverlapBoxNonAlloc(
                bounds.center,
                halfExtents,
                supportWakeHits,
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Ignore);

            SetHeldColliderState(false);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = supportWakeHits[i];
                Rigidbody hitBody = hit != null ? hit.attachedRigidbody : null;
                if (hitBody == null || hitBody == body)
                {
                    continue;
                }

                hitBody.WakeUp();
            }
        }

        private void Reset()
        {
            Rigidbody rigidbody = GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.mass = 4f;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        private void OnValidate()
        {
            homePositionTolerance = Mathf.Max(0f, homePositionTolerance);
            homeRotationTolerance = Mathf.Max(0f, homeRotationTolerance);
            foreignHomeRadius = Mathf.Max(0f, foreignHomeRadius);
            neighborHomeDisturbanceGraceTime = Mathf.Max(0f, neighborHomeDisturbanceGraceTime);
            neighborHomeDisturbanceMaximumSpeed = Mathf.Max(0f, neighborHomeDisturbanceMaximumSpeed);
        }
    }
}
