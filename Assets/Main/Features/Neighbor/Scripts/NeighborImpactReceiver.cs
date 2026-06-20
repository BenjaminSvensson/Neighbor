using System;
using Neighbor.Main.Features.Interaction;
using UnityEngine;

namespace Neighbor.Main.Features.Neighbor
{
    public sealed class NeighborImpactReceiver : MonoBehaviour, IPhysicsImpactReceiver
    {
        [SerializeField] private NeighborBrain brain;
        [SerializeField] private NeighborMotor motor;
        [SerializeField] private bool requireRecentlyThrownPickupImpact = true;
        [SerializeField, Min(0f)] private float minimumPhysicsImpactSpeed = 1.25f;
        [SerializeField, Range(0f, 1f)] private float minimumStunLoudness = 0.18f;
        [SerializeField, Min(0f)] private float minimumStunDuration = 0.35f;
        [SerializeField, Min(0f)] private float maximumStunDuration = 1.4f;
        [SerializeField, Min(0f)] private float minimumKnockbackDistance = 0.25f;
        [SerializeField, Min(0f)] private float maximumKnockbackDistance = 1.25f;
        [SerializeField, Min(0.01f)] private float knockbackDuration = 0.18f;
        [SerializeField, Min(0f)] private float impactCooldown = 0.35f;

        private float lastImpactTime = float.NegativeInfinity;

        public event Action ImpactReceived;

        private void Awake()
        {
            brain = brain != null ? brain : GetComponent<NeighborBrain>();
            motor = motor != null ? motor : GetComponent<NeighborMotor>();
        }

        public void ReceivePhysicsImpact(
            Pickupable impactingPickup,
            Vector3 hitPoint,
            Vector3 incomingVelocity,
            float impulse,
            float loudness01)
        {
            if (requireRecentlyThrownPickupImpact
                && (impactingPickup == null || !impactingPickup.IsRecentlyThrown))
            {
                return;
            }

            Vector3 flatVelocity = incomingVelocity;
            flatVelocity.y = 0f;
            if (flatVelocity.magnitude < minimumPhysicsImpactSpeed && loudness01 < 1f)
            {
                return;
            }

            ReceiveImpact(hitPoint, incomingVelocity, loudness01);
        }

        public void ReceiveImpact(Vector3 hitPoint, Vector3 incomingVelocity, float loudness01)
        {
            if (loudness01 < minimumStunLoudness || Time.time - lastImpactTime < impactCooldown)
            {
                return;
            }

            lastImpactTime = Time.time;
            float impact01 = Mathf.InverseLerp(minimumStunLoudness, 1f, loudness01);
            Vector3 knockbackDirection = GetKnockbackDirection(hitPoint, incomingVelocity);
            float stunDuration = Mathf.Lerp(minimumStunDuration, maximumStunDuration, impact01);
            float knockbackDistance = Mathf.Lerp(minimumKnockbackDistance, maximumKnockbackDistance, impact01);

            ImpactReceived?.Invoke();
            brain?.Stun(stunDuration);
            motor?.ApplyKnockback(knockbackDirection, knockbackDistance, knockbackDuration);
        }

        private Vector3 GetKnockbackDirection(Vector3 hitPoint, Vector3 incomingVelocity)
        {
            Vector3 direction = transform.position - hitPoint;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                return direction.normalized;
            }

            direction = incomingVelocity;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                return direction.normalized;
            }

            return -transform.forward;
        }
    }
}
