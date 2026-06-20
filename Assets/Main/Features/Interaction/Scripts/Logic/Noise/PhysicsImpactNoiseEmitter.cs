using Neighbor.Main.Features.Neighbor;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    public interface IPhysicsImpactReceiver
    {
        void ReceivePhysicsImpact(
            Pickupable impactingPickup,
            Vector3 hitPoint,
            Vector3 incomingVelocity,
            float impulse,
            float loudness01);
    }

    [RequireComponent(typeof(Rigidbody))]
    public sealed class PhysicsImpactNoiseEmitter : MonoBehaviour
    {
        [Header("Impact Loudness")]
        [SerializeField, Min(0f)] private float minimumImpactImpulse = 1.2f;
        [SerializeField, Min(0.01f)] private float maximumImpactImpulse = 18f;
        [SerializeField, Min(0f)] private float impactCooldown = 0.08f;

        [Header("Noise Trigger")]
        [SerializeField, Min(0f)] private float minimumNoiseRadius = 1.5f;
        [SerializeField, Min(0f)] private float maximumNoiseRadius = 14f;
        [SerializeField, Range(0f, 1f)] private float alertUrgency = 0.7f;
        [SerializeField, Min(0.02f)] private float noiseLifetime = 0.25f;
        [SerializeField, Min(0f)] private float neighborAttributionTime = 1f;
        [SerializeField, Min(0f)] private float neighborAttributionMaximumObjectSpeed = 0.75f;

        [Header("3D Audio")]
        [SerializeField] private AudioClip[] impactClips;
        [SerializeField, Range(0f, 1f)] private float minimumVolume = 0.08f;
        [SerializeField, Range(0f, 1f)] private float maximumVolume = 0.85f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.08f;
        [SerializeField, Min(0f)] private float generatedClipDuration = 0.16f;

        private Rigidbody body;
        private Pickupable pickupable;
        private AudioSource audioSource;
        private AudioClip generatedImpactClip;
        private float lastImpactTime = float.NegativeInfinity;
        private float speedBeforePhysicsStep;
        private float neighborAttributionUntilTime;
        private GameObject recentNeighborInstigator;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            pickupable = GetComponentInParent<Pickupable>();
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            ConfigureAudioSource();
        }

        private void OnCollisionEnter(Collision collision)
        {
            EmitCollisionNoise(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            EmitCollisionNoise(collision);
        }

        private void FixedUpdate()
        {
            speedBeforePhysicsStep = body != null ? body.linearVelocity.magnitude : 0f;
        }

        private void EmitCollisionNoise(Collision collision)
        {
            UpdateNoiseAttribution(collision);
            NotifyPhysicsImpactReceivers(collision);

            if (Time.time - lastImpactTime < impactCooldown)
            {
                return;
            }

            float impulse = collision.impulse.magnitude;
            if (impulse < minimumImpactImpulse)
            {
                return;
            }

            float loudness01 = Mathf.InverseLerp(minimumImpactImpulse, maximumImpactImpulse, impulse);
            ContactPoint contact = collision.GetContact(0);
            Vector3 origin = contact.point;

            lastImpactTime = Time.time;
            PlayImpactAudio(origin, loudness01);
            SpawnNoiseTrigger(origin, loudness01, ResolveNoiseInstigator());
            NotifyDoorImpact(collision);
        }

        private void NotifyPhysicsImpactReceivers(Collision collision)
        {
            if (collision == null || collision.collider == null || collision.contactCount == 0)
            {
                return;
            }

            IPhysicsImpactReceiver[] receivers = collision.collider.GetComponentsInParent<IPhysicsImpactReceiver>();
            if (receivers == null || receivers.Length == 0)
            {
                return;
            }

            Pickupable impactPickup = pickupable != null ? pickupable : GetComponentInParent<Pickupable>();
            float impulse = collision.impulse.magnitude;
            float loudness01 = Mathf.InverseLerp(minimumImpactImpulse, maximumImpactImpulse, impulse);
            Vector3 incomingVelocity = collision.relativeVelocity;
            Vector3 origin = collision.GetContact(0).point;

            for (int i = 0; i < receivers.Length; i++)
            {
                receivers[i]?.ReceivePhysicsImpact(impactPickup, origin, incomingVelocity, impulse, loudness01);
            }
        }

        private void UpdateNoiseAttribution(Collision collision)
        {
            NeighborBrain neighbor = collision.collider != null
                ? collision.collider.GetComponentInParent<NeighborBrain>()
                : null;
            if (neighbor != null && speedBeforePhysicsStep <= neighborAttributionMaximumObjectSpeed)
            {
                MarkNeighborInstigator(neighbor.gameObject);
                return;
            }

            PhysicsImpactNoiseEmitter otherEmitter = collision.collider != null
                ? collision.collider.GetComponentInParent<PhysicsImpactNoiseEmitter>()
                : null;
            GameObject propagatedInstigator = otherEmitter != null && otherEmitter != this
                ? otherEmitter.ResolveNoiseInstigator()
                : null;
            if (propagatedInstigator != null)
            {
                MarkNeighborInstigator(propagatedInstigator);
            }
        }

        public void MarkNeighborInstigator(GameObject neighborInstigator)
        {
            if (neighborInstigator == null || neighborInstigator.GetComponentInParent<NeighborBrain>() == null)
            {
                return;
            }

            recentNeighborInstigator = neighborInstigator;
            neighborAttributionUntilTime = Time.time + neighborAttributionTime;
            pickupable?.MarkNeighborHomeDisplacement(neighborInstigator);
        }

        private GameObject ResolveNoiseInstigator()
        {
            if (recentNeighborInstigator != null && Time.time <= neighborAttributionUntilTime)
            {
                return recentNeighborInstigator;
            }

            recentNeighborInstigator = null;
            return gameObject;
        }

        private void NotifyDoorImpact(Collision collision)
        {
            Door door = collision.collider.GetComponentInParent<Door>();
            door?.PlayImpactNudge();
        }

        private void PlayImpactAudio(Vector3 origin, float loudness01)
        {
            AudioClip clip = GetImpactClip(loudness01);
            if (clip == null)
            {
                return;
            }

            GameObject audioObject = new GameObject("Impact3DAudio");
            audioObject.transform.position = origin;

            AudioSource impactSource = audioObject.AddComponent<AudioSource>();
            ConfigureAudioSource(impactSource);
            impactSource.volume = Mathf.Lerp(minimumVolume, maximumVolume, loudness01);
            impactSource.pitch = Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
            impactSource.maxDistance = Mathf.Lerp(minimumNoiseRadius, maximumNoiseRadius, loudness01);
            impactSource.clip = clip;
            impactSource.Play();

            Destroy(audioObject, clip.length / Mathf.Max(0.01f, impactSource.pitch) + 0.05f);
        }

        private AudioClip GetImpactClip(float loudness01)
        {
            if (impactClips != null && impactClips.Length > 0)
            {
                return impactClips[Random.Range(0, impactClips.Length)];
            }

            if (generatedImpactClip == null)
            {
                generatedImpactClip = CreateGeneratedImpactClip();
            }

            return generatedImpactClip;
        }

        private AudioClip CreateGeneratedImpactClip()
        {
            const int sampleRate = 22050;
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * generatedClipDuration));
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Exp(-t * 34f);
                float lowThump = Mathf.Sin(2f * Mathf.PI * 95f * t);
                float scrape = Random.Range(-1f, 1f) * 0.35f;
                samples[i] = (lowThump * 0.75f + scrape) * envelope;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedImpact", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void SpawnNoiseTrigger(Vector3 origin, float loudness01, GameObject instigatorObject)
        {
            float radius = Mathf.Lerp(minimumNoiseRadius, maximumNoiseRadius, loudness01);
            GameObject noiseObject = new GameObject("NoiseEvent");
            noiseObject.transform.position = origin;

            SphereCollider sphereCollider = noiseObject.AddComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
            sphereCollider.radius = radius;

            Rigidbody noiseBody = noiseObject.AddComponent<Rigidbody>();
            noiseBody.isKinematic = true;
            noiseBody.useGravity = false;

            NoiseEvent noiseEvent = noiseObject.AddComponent<NoiseEvent>();
            noiseEvent.Initialize(origin, radius, loudness01, gameObject, noiseLifetime, alertUrgency, instigatorObject);
        }

        private void ConfigureAudioSource()
        {
            ConfigureAudioSource(audioSource);
        }

        private void ConfigureAudioSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 0.5f;
            source.maxDistance = maximumNoiseRadius;
            source.dopplerLevel = 0.2f;
        }

        private void Reset()
        {
            AudioSource source = GetComponent<AudioSource>();
            if (source != null)
            {
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.playOnAwake = false;
            }
        }

        private void OnValidate()
        {
            maximumImpactImpulse = Mathf.Max(0.01f, maximumImpactImpulse);
            neighborAttributionTime = Mathf.Max(0f, neighborAttributionTime);
            neighborAttributionMaximumObjectSpeed = Mathf.Max(0f, neighborAttributionMaximumObjectSpeed);
        }
    }
}
