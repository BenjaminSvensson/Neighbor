using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(HingeJoint))]
    public sealed class PhysicsSwingingObject : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Rigidbody targetBody;
        [SerializeField] private HingeJoint hinge;

        [Header("Startup")]
        [SerializeField] private bool wakeOnStart = true;
        [SerializeField, Min(0f)] private float startImpulse = 1.5f;
        [SerializeField] private bool randomizeStartDirection = true;

        [Header("Sustain")]
        [SerializeField] private bool sustainSwing;
        [SerializeField, Min(0f)] private float sustainTorque = 2.5f;
        [SerializeField, Min(0f)] private float maximumSustainSpeed = 2.4f;
        [SerializeField, Min(0f)] private float limitTurnaroundPadding = 6f;

        private float sustainDirection = 1f;

        public Rigidbody Body => targetBody;
        public HingeJoint Hinge => hinge;

        private void Reset()
        {
            ResolveReferences();

            if (targetBody != null)
            {
                targetBody.useGravity = true;
                targetBody.isKinematic = false;
                targetBody.linearDamping = 0.02f;
                targetBody.angularDamping = 0.05f;
                targetBody.interpolation = RigidbodyInterpolation.Interpolate;
                targetBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            if (hinge != null)
            {
                hinge.axis = Vector3.forward;
                hinge.anchor = Vector3.zero;
                hinge.autoConfigureConnectedAnchor = true;
                hinge.useLimits = true;

                JointLimits limits = hinge.limits;
                limits.min = -55f;
                limits.max = 55f;
                limits.bounciness = 0.15f;
                limits.bounceMinVelocity = 0.2f;
                limits.contactDistance = 2f;
                hinge.limits = limits;
            }
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            if (!wakeOnStart || targetBody == null)
            {
                return;
            }

            targetBody.WakeUp();

            if (startImpulse > 0f)
            {
                float direction = randomizeStartDirection && Random.value < 0.5f ? -1f : 1f;
                Kick(startImpulse * direction);
            }
        }

        private void FixedUpdate()
        {
            if (!sustainSwing || targetBody == null || hinge == null || sustainTorque <= 0f)
            {
                return;
            }

            UpdateSustainDirection();

            Vector3 hingeAxis = GetWorldHingeAxis();
            float signedSpeed = Vector3.Dot(targetBody.angularVelocity, hingeAxis);
            if (Mathf.Abs(signedSpeed) >= maximumSustainSpeed)
            {
                return;
            }

            targetBody.AddTorque(hingeAxis * sustainTorque * sustainDirection, ForceMode.Acceleration);
        }

        public void Kick(float impulse)
        {
            ResolveReferences();

            if (targetBody == null || targetBody.isKinematic)
            {
                return;
            }

            targetBody.WakeUp();
            targetBody.AddTorque(GetWorldHingeAxis() * impulse, ForceMode.Impulse);
        }

        private void UpdateSustainDirection()
        {
            if (!hinge.useLimits)
            {
                return;
            }

            JointLimits limits = hinge.limits;
            float angle = hinge.angle;
            if (angle >= limits.max - limitTurnaroundPadding)
            {
                sustainDirection = -1f;
            }
            else if (angle <= limits.min + limitTurnaroundPadding)
            {
                sustainDirection = 1f;
            }
        }

        private Vector3 GetWorldHingeAxis()
        {
            Vector3 localAxis = hinge != null && hinge.axis.sqrMagnitude > 0.0001f
                ? hinge.axis.normalized
                : Vector3.forward;
            return transform.TransformDirection(localAxis).normalized;
        }

        private void ResolveReferences()
        {
            if (targetBody == null)
            {
                TryGetComponent(out targetBody);
            }

            if (hinge == null)
            {
                TryGetComponent(out hinge);
            }
        }

        private void OnValidate()
        {
            startImpulse = Mathf.Max(0f, startImpulse);
            sustainTorque = Mathf.Max(0f, sustainTorque);
            maximumSustainSpeed = Mathf.Max(0f, maximumSustainSpeed);
            limitTurnaroundPadding = Mathf.Max(0f, limitTurnaroundPadding);
            ResolveReferences();
        }
    }
}
