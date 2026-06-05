using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CarImpactAlarm : MonoBehaviour
    {
        [Header("Impact Trigger")]
        [SerializeField, Min(0f)] private float minimumImpactImpulse = 3f;
        [SerializeField, Min(0f)] private float minimumRelativeSpeed = 2.5f;
        [SerializeField, Min(0f)] private float alertCooldown = 1.25f;

        [Header("Neighbor Alert")]
        [SerializeField, Min(0f)] private float hearingRadius = 22f;
        [SerializeField, Range(0f, 1f)] private float loudness = 1f;
        [SerializeField, Range(0f, 1f)] private float alertUrgency = 1f;
        [SerializeField, Min(0.02f)] private float noiseLifetime = 0.75f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] alarmClips;
        [SerializeField, Range(0f, 1f)] private float alarmVolume = 0.85f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.04f;

        private AudioClip generatedAlarmClip;
        private float nextAlertTime;

        private void Awake()
        {
            ConfigureBody();
            ResolveAudioSource();
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryAlert(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            TryAlert(collision);
        }

        private void TryAlert(Collision collision)
        {
            if (Time.time < nextAlertTime || collision.rigidbody == null)
            {
                return;
            }

            float impulse = collision.impulse.magnitude;
            float relativeSpeed = collision.relativeVelocity.magnitude;
            if (impulse < minimumImpactImpulse && relativeSpeed < minimumRelativeSpeed)
            {
                return;
            }

            Vector3 origin = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            nextAlertTime = Time.time + alertCooldown;
            PlayAlarm(origin);
            SpawnNoiseEvent(origin);
        }

        private void PlayAlarm(Vector3 origin)
        {
            AudioClip clip = GetAlarmClip();
            if (clip == null)
            {
                return;
            }

            if (audioSource != null && audioSource.transform != transform)
            {
                audioSource.transform.position = origin;
                audioSource.pitch = Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
                audioSource.PlayOneShot(clip, alarmVolume);
                return;
            }

            PlayAlarmAtImpact(origin, clip);
        }

        private void PlayAlarmAtImpact(Vector3 origin, AudioClip clip)
        {
            GameObject audioObject = new GameObject("CarAlarm3DAudio");
            audioObject.transform.position = origin;

            AudioSource source = audioObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = alarmVolume;
            source.pitch = Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = audioSource != null ? audioSource.minDistance : 1f;
            source.maxDistance = hearingRadius;
            source.dopplerLevel = 0.1f;
            source.Play();

            Destroy(audioObject, clip.length / Mathf.Max(0.01f, source.pitch) + 0.05f);
        }

        private AudioClip GetAlarmClip()
        {
            if (alarmClips != null && alarmClips.Length > 0)
            {
                return alarmClips[Random.Range(0, alarmClips.Length)];
            }

            if (generatedAlarmClip == null)
            {
                generatedAlarmClip = CreateGeneratedAlarmClip();
            }

            return generatedAlarmClip;
        }

        private AudioClip CreateGeneratedAlarmClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.65f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float pulse = Mathf.PingPong(time * 8f, 1f) > 0.45f ? 1f : 0f;
                float envelope = Mathf.Clamp01(1f - time / duration) * pulse;
                float highTone = Mathf.Sin(2f * Mathf.PI * 880f * time);
                float lowTone = Mathf.Sin(2f * Mathf.PI * 440f * time) * 0.35f;
                samples[i] = (highTone + lowTone) * envelope * 0.35f;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedCarAlarm", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void SpawnNoiseEvent(Vector3 origin)
        {
            if (hearingRadius <= 0f || loudness <= 0f)
            {
                return;
            }

            GameObject noiseObject = new GameObject("CarImpactNoiseEvent");
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

        private void ConfigureBody()
        {
            Rigidbody body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private void ResolveAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = 1f;
            audioSource.maxDistance = hearingRadius;
            audioSource.dopplerLevel = 0.1f;
        }

        private void OnValidate()
        {
            alertCooldown = Mathf.Max(0f, alertCooldown);
            hearingRadius = Mathf.Max(0f, hearingRadius);
            noiseLifetime = Mathf.Max(0.02f, noiseLifetime);
        }
    }
}
