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

        [Header("Visual Shake")]
        [SerializeField] private Transform visualRoot;
        [SerializeField, Min(0f)] private float impactShakeDuration = 0.28f;
        [SerializeField, Min(0f)] private float impactPositionAmount = 0.025f;
        [SerializeField, Min(0f)] private float impactRotationAmount = 1.2f;
        [SerializeField, Min(0f)] private float alarmPositionAmount = 0.002f;
        [SerializeField, Min(0f)] private float alarmRotationAmount = 0.12f;
        [SerializeField, Min(0f)] private float shakeFrequency = 30f;

        private AudioClip generatedAlarmClip;
        private float nextAlertTime;
        private float impactShakeEndTime;
        private float alarmShakeEndTime;
        private Vector3 visualRestPosition;
        private Quaternion visualRestRotation;
        private Vector3 impactShakeDirection;
        private float shakeSeed;

        private void Awake()
        {
            ConfigureBody();
            ResolveAudioSource();
            ResolveVisualRoot();
        }

        private void LateUpdate()
        {
            UpdateVisualShake();
        }

        private void OnDisable()
        {
            ResetVisualRoot();
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
            StartImpactShake(collision.relativeVelocity);
            alarmShakeEndTime = Mathf.Max(alarmShakeEndTime, Time.time + PlayAlarm(origin));
            SpawnNoiseEvent(origin);
        }

        private float PlayAlarm(Vector3 origin)
        {
            AudioClip clip = GetAlarmClip();
            if (clip == null)
            {
                return 0f;
            }

            float pitch = Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
            if (audioSource != null && audioSource.transform != transform)
            {
                audioSource.transform.position = origin;
                audioSource.pitch = pitch;
                audioSource.PlayOneShot(clip, alarmVolume);
                return clip.length / Mathf.Max(0.01f, pitch);
            }

            PlayAlarmAtImpact(origin, clip, pitch);
            return clip.length / Mathf.Max(0.01f, pitch);
        }

        private void PlayAlarmAtImpact(Vector3 origin, AudioClip clip, float pitch)
        {
            GameObject audioObject = new GameObject("CarAlarm3DAudio");
            audioObject.transform.position = origin;

            AudioSource source = audioObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = alarmVolume;
            source.pitch = pitch;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = audioSource != null ? audioSource.minDistance : 1f;
            source.maxDistance = hearingRadius;
            source.dopplerLevel = 0.1f;
            source.Play();

            Destroy(audioObject, clip.length / Mathf.Max(0.01f, source.pitch) + 0.05f);
        }

        private void StartImpactShake(Vector3 relativeVelocity)
        {
            Vector3 localVelocity = transform.InverseTransformDirection(relativeVelocity);
            localVelocity.y = 0f;
            impactShakeDirection = localVelocity.sqrMagnitude > 0.001f
                ? -localVelocity.normalized
                : Random.insideUnitSphere.normalized;
            impactShakeDirection.y = Mathf.Max(0.35f, Mathf.Abs(impactShakeDirection.y));
            impactShakeDirection.Normalize();
            impactShakeEndTime = Time.time + impactShakeDuration;
            shakeSeed = Random.Range(0f, 1000f);
        }

        private void UpdateVisualShake()
        {
            if (visualRoot == null)
            {
                return;
            }

            float impactAmount = impactShakeDuration > 0f
                ? Mathf.Clamp01((impactShakeEndTime - Time.time) / impactShakeDuration)
                : 0f;
            float alarmAmount = Time.time < alarmShakeEndTime ? 1f : 0f;
            if (impactAmount <= 0f && alarmAmount <= 0f)
            {
                ResetVisualRoot();
                return;
            }

            float sample = Time.time * shakeFrequency + shakeSeed;
            Vector3 noise = new Vector3(
                Mathf.PerlinNoise(sample, 0.13f) * 2f - 1f,
                Mathf.PerlinNoise(sample, 4.71f) * 2f - 1f,
                Mathf.PerlinNoise(sample, 9.37f) * 2f - 1f);

            Vector3 impactOffset = Vector3.Scale(noise, impactShakeDirection) * impactPositionAmount * impactAmount;
            Vector3 alarmOffset = noise * alarmPositionAmount * alarmAmount;
            visualRoot.localPosition = visualRestPosition + impactOffset + alarmOffset;

            Vector3 rotationNoise = new Vector3(noise.z, noise.x, noise.y);
            float rotationAmount = impactRotationAmount * impactAmount + alarmRotationAmount * alarmAmount;
            visualRoot.localRotation = visualRestRotation * Quaternion.Euler(rotationNoise * rotationAmount);
        }

        private void ResolveVisualRoot()
        {
            if (visualRoot == null && transform.childCount > 0)
            {
                visualRoot = transform.GetChild(0);
            }

            if (visualRoot != null)
            {
                visualRestPosition = visualRoot.localPosition;
                visualRestRotation = visualRoot.localRotation;
            }
        }

        private void ResetVisualRoot()
        {
            if (visualRoot == null)
            {
                return;
            }

            visualRoot.localPosition = visualRestPosition;
            visualRoot.localRotation = visualRestRotation;
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
            impactShakeDuration = Mathf.Max(0f, impactShakeDuration);
            impactPositionAmount = Mathf.Max(0f, impactPositionAmount);
            impactRotationAmount = Mathf.Max(0f, impactRotationAmount);
            alarmPositionAmount = Mathf.Min(Mathf.Max(0f, alarmPositionAmount), impactPositionAmount);
            alarmRotationAmount = Mathf.Min(Mathf.Max(0f, alarmRotationAmount), impactRotationAmount);
            shakeFrequency = Mathf.Max(0f, shakeFrequency);
        }
    }
}
