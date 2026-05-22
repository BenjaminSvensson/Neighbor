using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class Pickupable : MonoBehaviour, IInteractable
    {
        [Header("Pickup")]
        [SerializeField, Min(0f)] private float maximumPickupMass = 20f;
        [SerializeField, Min(0f)] private float heldDrag = 8f;
        [SerializeField, Min(0f)] private float heldAngularDrag = 10f;
        [SerializeField] private bool alignToCameraWhileHeld = true;
        [SerializeField] private bool disableCollidersWhileHeld = true;

        private Rigidbody body;
        private Collider[] ownColliders;
        private bool[] originalColliderEnabled;
        private bool wasUsingGravity;
        private bool wasKinematic;
        private float originalDrag;
        private float originalAngularDrag;
        private CollisionDetectionMode originalCollisionDetection;
        private RigidbodyInterpolation originalInterpolation;

        public bool IsHeld { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            ownColliders = GetComponentsInChildren<Collider>();
            originalColliderEnabled = new bool[ownColliders.Length];
        }

        private void OnDisable()
        {
            if (IsHeld)
            {
                RestorePhysics();
            }
        }

        public bool CanInteract(PlayerInteractor interactor)
        {
            return !IsHeld && body != null && body.mass <= maximumPickupMass;
        }

        public void Interact(PlayerInteractor interactor)
        {
            interactor.Pickup(this);
        }

        public void Pickup(PlayerInteractor interactor)
        {
            if (IsHeld || body == null)
            {
                return;
            }

            IsHeld = true;
            wasUsingGravity = body.useGravity;
            wasKinematic = body.isKinematic;
            originalDrag = body.linearDamping;
            originalAngularDrag = body.angularDamping;
            originalCollisionDetection = body.collisionDetectionMode;
            originalInterpolation = body.interpolation;

            body.useGravity = false;
            body.isKinematic = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.linearDamping = heldDrag;
            body.angularDamping = heldAngularDrag;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            SetHeldColliderState(false);
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
            RestorePhysics();
        }

        public void Throw(Vector3 velocity)
        {
            RestorePhysics();
            body.linearVelocity = velocity;
        }

        private void RestorePhysics()
        {
            if (!IsHeld || body == null)
            {
                return;
            }

            IsHeld = false;
            SetHeldColliderState(true);
            body.useGravity = wasUsingGravity;
            body.isKinematic = wasKinematic;
            body.linearDamping = originalDrag;
            body.angularDamping = originalAngularDrag;
            body.collisionDetectionMode = originalCollisionDetection;
            body.interpolation = originalInterpolation;
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
