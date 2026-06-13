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

        private Rigidbody body;
        private Collider[] ownColliders;
        private Renderer[] ownRenderers;
        private IPickupInteractionOverride[] pickupInteractionOverrides;
        private IPickupLifecycleReceiver[] lifecycleReceivers;
        private bool[] originalColliderEnabled;
        private bool[] inventoryColliderEnabled;
        private bool[] originalRendererEnabled;
        private readonly Collider[] supportWakeHits = new Collider[24];
        private bool wasUsingGravity;
        private bool wasKinematic;
        private float originalDrag;
        private float originalAngularDrag;
        private CollisionDetectionMode originalCollisionDetection;
        private RigidbodyInterpolation originalInterpolation;
        private Collider[] ignoredPlayerColliders;
        private float restorePlayerCollisionTime;

        public bool IsHeld { get; private set; }
        public bool IsInventoryStored { get; private set; }
        public HoldPointSize AssignedHoldPointSize => holdPointSize;
        public Vector3 ThrowOrigin => body != null ? body.worldCenterOfMass : transform.position;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            ownColliders = GetComponentsInChildren<Collider>();
            ownRenderers = GetComponentsInChildren<Renderer>();
            pickupInteractionOverrides = GetComponentsInChildren<IPickupInteractionOverride>();
            lifecycleReceivers = GetComponentsInChildren<IPickupLifecycleReceiver>();
            originalColliderEnabled = new bool[ownColliders.Length];
            inventoryColliderEnabled = new bool[ownColliders.Length];
            originalRendererEnabled = new bool[ownRenderers.Length];
        }

        private void OnDisable()
        {
            if (IsHeld)
            {
                RestorePhysics();
            }

            RestoreIgnoredPlayerCollisions();
        }

        private void Update()
        {
            if (ignoredPlayerColliders != null && Time.time >= restorePlayerCollisionTime)
            {
                RestoreIgnoredPlayerCollisions();
            }
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
            if (IsHeld || body == null)
            {
                return;
            }

            NotifyPickupStarted(interactor);
            IsHeld = true;
            CapturePhysicsState();

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

        public void Drop()
        {
            RestoreInventoryState();
            RestorePhysics();
        }

        public void Place(Vector3 position, Quaternion rotation, bool sleepAfterPlacing = true)
        {
            RestoreInventoryState();
            transform.SetPositionAndRotation(position, rotation);

            if (body == null)
            {
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
            SetBodyLinearVelocity(velocity);
        }

        public void Throw(Vector3 velocity, Collider[] playerColliders)
        {
            RestoreInventoryState();
            RestorePhysics();
            IgnorePlayerCollisionsTemporarily(playerColliders);
            SetBodyLinearVelocity(velocity);
        }

        public void StoreInInventory(Transform inventoryParent)
        {
            if (body == null || IsInventoryStored)
            {
                return;
            }

            if (IsHeld)
            {
                IsHeld = false;
                SetHeldColliderState(true);
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
                if (ownCollider == null || !ownCollider.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = ownCollider.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(ownCollider.bounds);
            }

            return hasBounds;
        }

        private void RestorePhysics()
        {
            if (!IsHeld || body == null)
            {
                return;
            }

            IsHeld = false;
            SetHeldColliderState(true);
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
    }
}
