using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Pickupable))]
    public sealed class WoodBoardPryTarget : MonoBehaviour, IPickupInteractionOverride
    {
        [SerializeField, Min(0f)] private float pryImpulse = 4.5f;
        [SerializeField, Min(0f)] private float upwardImpulse = 1.2f;
        [SerializeField, Min(0f)] private float torqueImpulse = 6f;
        [SerializeField, Min(0f)] private float hearingRadius = 9f;
        [SerializeField, Range(0f, 1f)] private float loudness = 0.55f;
        [SerializeField, Range(0f, 1f)] private float alertUrgency = 0.65f;
        [SerializeField, Min(0.02f)] private float noiseLifetime = 0.3f;

        private Rigidbody body;
        private DoorBlockerChair doorBlocker;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            doorBlocker = GetComponent<DoorBlockerChair>();
        }

        public bool CanPickup(PlayerInteractor interactor)
        {
            return doorBlocker == null || !doorBlocker.IsBlockingDoor;
        }

        public void PryLoose(Vector3 origin, Vector3 direction, GameObject instigator)
        {
            doorBlocker?.HandlePriedLoose();

            if (body != null)
            {
                body.constraints = RigidbodyConstraints.None;
                body.isKinematic = false;
                body.useGravity = true;
                body.WakeUp();

                Vector3 pryDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
                body.AddForce((pryDirection * pryImpulse) + Vector3.up * upwardImpulse, ForceMode.Impulse);
                body.AddTorque(Random.onUnitSphere * torqueImpulse, ForceMode.Impulse);
            }

            SpawnNoiseEvent(origin, instigator);
        }

        private void SpawnNoiseEvent(Vector3 origin, GameObject instigator)
        {
            if (hearingRadius <= 0f || loudness <= 0f)
            {
                return;
            }

            GameObject noiseObject = new GameObject("WoodBoardPryNoiseEvent");
            noiseObject.transform.position = origin;

            SphereCollider sphereCollider = noiseObject.AddComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
            sphereCollider.radius = hearingRadius;

            Rigidbody noiseBody = noiseObject.AddComponent<Rigidbody>();
            noiseBody.isKinematic = true;
            noiseBody.useGravity = false;

            NoiseEvent noiseEvent = noiseObject.AddComponent<NoiseEvent>();
            noiseEvent.Initialize(origin, hearingRadius, loudness, instigator != null ? instigator : gameObject, noiseLifetime, alertUrgency);
        }

        private void OnValidate()
        {
            pryImpulse = Mathf.Max(0f, pryImpulse);
            upwardImpulse = Mathf.Max(0f, upwardImpulse);
            torqueImpulse = Mathf.Max(0f, torqueImpulse);
            hearingRadius = Mathf.Max(0f, hearingRadius);
            noiseLifetime = Mathf.Max(0.02f, noiseLifetime);
        }
    }
}
