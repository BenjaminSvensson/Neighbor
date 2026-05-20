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

        private Rigidbody body;
        private bool wasUsingGravity;
        private float originalDrag;
        private float originalAngularDrag;
        private CollisionDetectionMode originalCollisionDetection;
        private RigidbodyInterpolation originalInterpolation;

        public bool IsHeld { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
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
            originalDrag = body.linearDamping;
            originalAngularDrag = body.angularDamping;
            originalCollisionDetection = body.collisionDetectionMode;
            originalInterpolation = body.interpolation;

            body.useGravity = false;
            body.linearDamping = heldDrag;
            body.angularDamping = heldAngularDrag;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
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

            Vector3 toTarget = targetPosition - body.position;
            Vector3 targetVelocity = Vector3.ClampMagnitude(toTarget * followStrength, maxVelocity);
            body.linearVelocity = targetVelocity;

            if (!alignToCameraWhileHeld)
            {
                return;
            }

            Quaternion rotationDelta = targetRotation * Quaternion.Inverse(body.rotation);
            rotationDelta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f)
            {
                angle -= 360f;
            }

            if (axis.sqrMagnitude < 0.001f)
            {
                return;
            }

            body.angularVelocity = axis.normalized * (angle * Mathf.Deg2Rad * rotationStrength);
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
            body.useGravity = wasUsingGravity;
            body.linearDamping = originalDrag;
            body.angularDamping = originalAngularDrag;
            body.collisionDetectionMode = originalCollisionDetection;
            body.interpolation = originalInterpolation;
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
