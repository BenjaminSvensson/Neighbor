using UnityEngine;
using Neighbor.Main.Features.Neighbor;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Pickupable))]
    public sealed class GlassShatter : MonoBehaviour, IPickupInteractionOverride
    {
        [Header("Shatter Trigger")]
        [SerializeField, Min(0f)] private float minimumImpactImpulse = 2.2f;
        [SerializeField, Min(0f)] private float minimumRelativeSpeed = 3.5f;

        [Header("Pieces")]
        [SerializeField] private GameObject intactVisualRoot;
        [SerializeField] private Collider intactCollider;
        [SerializeField] private Transform shardRoot;
        [SerializeField, Min(0f)] private float shardImpulse = 2.5f;
        [SerializeField, Min(0f)] private float shardTorque = 5f;
        [SerializeField, Min(0.05f)] private float shardLifetime = 12f;

        [Header("Noise")]
        [SerializeField, Min(0f)] private float hearingRadius = 13f;
        [SerializeField, Range(0f, 1f)] private float loudness = 0.75f;
        [SerializeField, Range(0f, 1f)] private float alertUrgency = 0.85f;
        [SerializeField, Min(0.02f)] private float noiseLifetime = 0.45f;

        [Header("Audio")]
        [SerializeField] private AudioClip[] shatterClips;
        [SerializeField, Range(0f, 1f)] private float shatterVolume = 0.8f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.12f;

        private Rigidbody body;
        private AudioClip generatedShatterClip;
        private bool isShattered;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();

            if (intactVisualRoot == null)
            {
                intactVisualRoot = gameObject;
            }

            if (intactCollider == null)
            {
                intactCollider = GetComponent<Collider>();
            }

            if (shardRoot != null)
            {
                shardRoot.gameObject.SetActive(false);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryShatter(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            TryShatter(collision);
        }

        public bool CanPickup(PlayerInteractor interactor)
        {
            return !isShattered;
        }

        private void TryShatter(Collision collision)
        {
            if (isShattered || collision.contactCount == 0)
            {
                return;
            }

            float impulse = collision.impulse.magnitude;
            float relativeSpeed = collision.relativeVelocity.magnitude;
            if (impulse < minimumImpactImpulse && relativeSpeed < minimumRelativeSpeed)
            {
                return;
            }

            ContactPoint contact = collision.GetContact(0);
            Shatter(contact.point, collision.relativeVelocity);
        }

        private void Shatter(Vector3 origin, Vector3 incomingVelocity)
        {
            isShattered = true;
            NeighborEnvironmentalAwareness.Report(origin, 0.8f, gameObject);

            if (intactVisualRoot != null)
            {
                foreach (Renderer intactRenderer in intactVisualRoot.GetComponentsInChildren<Renderer>())
                {
                    intactRenderer.enabled = false;
                }
            }

            if (intactCollider != null)
            {
                intactCollider.enabled = false;
            }

            if (body != null)
            {
                RigidbodyVelocityUtility.ClearIfDynamic(body);
                body.isKinematic = true;
                body.useGravity = false;
            }

            ReleaseShards(origin, incomingVelocity);
            PlayShatterAudio(origin);
            SpawnNoiseEvent(origin);
        }

        private void ReleaseShards(Vector3 origin, Vector3 incomingVelocity)
        {
            if (shardRoot == null)
            {
                return;
            }

            shardRoot.gameObject.SetActive(true);
            Vector3 inheritedVelocity = incomingVelocity.sqrMagnitude > 0.01f ? incomingVelocity * 0.25f : Vector3.zero;

            for (int i = shardRoot.childCount - 1; i >= 0; i--)
            {
                Transform shard = shardRoot.GetChild(i);
                shard.SetParent(null, true);
                shard.gameObject.SetActive(true);

                Rigidbody shardBody = shard.GetComponent<Rigidbody>();
                if (shardBody == null)
                {
                    continue;
                }

                shardBody.isKinematic = false;
                shardBody.useGravity = true;
                shardBody.linearVelocity = inheritedVelocity;

                Vector3 away = (shard.position - origin).sqrMagnitude > 0.0001f
                    ? (shard.position - origin).normalized
                    : Random.onUnitSphere;
                shardBody.AddForce((away + Vector3.up * 0.35f) * shardImpulse, ForceMode.Impulse);
                shardBody.AddTorque(Random.onUnitSphere * shardTorque, ForceMode.Impulse);

                Destroy(shard.gameObject, shardLifetime);
            }

            Destroy(shardRoot.gameObject);
        }

        private void PlayShatterAudio(Vector3 origin)
        {
            AudioClip clip = GetShatterClip();
            if (clip == null)
            {
                return;
            }

            GameObject audioObject = new GameObject("GlassShatter3DAudio");
            audioObject.transform.position = origin;

            AudioSource source = audioObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = shatterVolume;
            source.pitch = Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 0.4f;
            source.maxDistance = hearingRadius;
            source.dopplerLevel = 0.1f;
            source.Play();

            Destroy(audioObject, clip.length / Mathf.Max(0.01f, source.pitch) + 0.05f);
        }

        private AudioClip GetShatterClip()
        {
            if (shatterClips != null && shatterClips.Length > 0)
            {
                return shatterClips[Random.Range(0, shatterClips.Length)];
            }

            if (generatedShatterClip == null)
            {
                generatedShatterClip = CreateGeneratedShatterClip();
            }

            return generatedShatterClip;
        }

        private AudioClip CreateGeneratedShatterClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.42f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Exp(-time * 9f);
                float brightRing = Mathf.Sin(2f * Mathf.PI * 2100f * time) * Mathf.Exp(-time * 18f);
                float grit = Random.Range(-1f, 1f) * Mathf.Exp(-time * 14f);
                float tinkle = Mathf.Sin(2f * Mathf.PI * 3800f * time) * Mathf.Exp(-time * 28f);
                samples[i] = (brightRing * 0.35f + grit * 0.45f + tinkle * 0.2f) * envelope;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedGlassShatter", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void SpawnNoiseEvent(Vector3 origin)
        {
            if (hearingRadius <= 0f || loudness <= 0f)
            {
                return;
            }

            GameObject noiseObject = new GameObject("GlassShatterNoiseEvent");
            noiseObject.transform.position = origin;

            SphereCollider sphereCollider = noiseObject.AddComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
            sphereCollider.radius = hearingRadius;

            Rigidbody noiseBody = noiseObject.AddComponent<Rigidbody>();
            noiseBody.isKinematic = true;
            noiseBody.useGravity = false;

            NoiseEvent noiseEvent = noiseObject.AddComponent<NoiseEvent>();
            noiseEvent.Initialize(origin, hearingRadius, loudness, gameObject, noiseLifetime, alertUrgency);
        }

        private void OnValidate()
        {
            minimumImpactImpulse = Mathf.Max(0f, minimumImpactImpulse);
            minimumRelativeSpeed = Mathf.Max(0f, minimumRelativeSpeed);
            shardLifetime = Mathf.Max(0.05f, shardLifetime);
            hearingRadius = Mathf.Max(0f, hearingRadius);
            noiseLifetime = Mathf.Max(0.02f, noiseLifetime);
        }
    }
}
