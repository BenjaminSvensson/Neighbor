using Neighbor.Main.Features.Neighbor;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
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
        [SerializeField, Min(0.02f)] private float noiseLifetime = 0.25f;

        [Header("3D Audio")]
        [SerializeField] private AudioClip[] impactClips;
        [SerializeField, Range(0f, 1f)] private float minimumVolume = 0.08f;
        [SerializeField, Range(0f, 1f)] private float maximumVolume = 0.85f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.08f;
        [SerializeField, Min(0f)] private float generatedClipDuration = 0.16f;

        private Rigidbody body;
        private AudioSource audioSource;
        private AudioClip generatedImpactClip;
        private float lastImpactTime;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
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

        private void EmitCollisionNoise(Collision collision)
        {
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
            SpawnNoiseTrigger(origin, loudness01);
            NotifyImpactReceiver(collision, origin, loudness01);
            NotifyDoorImpact(collision);
        }

        private void NotifyImpactReceiver(Collision collision, Vector3 origin, float loudness01)
        {
            NeighborImpactReceiver receiver = collision.collider.GetComponentInParent<NeighborImpactReceiver>();
            if (receiver == null)
            {
                return;
            }

            Vector3 incomingVelocity = body != null ? body.linearVelocity : collision.relativeVelocity;
            receiver.ReceiveImpact(origin, incomingVelocity, loudness01);
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

        private void SpawnNoiseTrigger(Vector3 origin, float loudness01)
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
            noiseEvent.Initialize(origin, radius, loudness01, gameObject, noiseLifetime);
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
    }
}
