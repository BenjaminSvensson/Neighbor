using System.Collections;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Collider))]
    public sealed class Doorbell : MonoBehaviour, IInteractable
    {
        [Header("Doorbell")]
        [SerializeField, Min(0f)] private float interactionCooldown = 0.55f;
        [SerializeField] private Transform buttonVisual;
        [SerializeField] private Vector3 pressedLocalOffset = new Vector3(0f, 0f, -0.035f);
        [SerializeField, Min(0.01f)] private float pressDuration = 0.08f;
        [SerializeField, Min(0.01f)] private float releaseDuration = 0.12f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] ringClips;
        [SerializeField, Range(0f, 1f)] private float ringVolume = 0.75f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.03f;
        [SerializeField, Min(0f)] private float audioMinDistance = 1f;
        [SerializeField, Min(0.1f)] private float audioMaxDistance = 18f;

        [Header("Neighbor Alert")]
        [SerializeField, Min(0f)] private float noiseRadius = 18f;
        [SerializeField, Range(0f, 1f)] private float noiseLoudness = 1f;
        [SerializeField, Range(0f, 1f)] private float alertUrgency = 0.25f;
        [SerializeField, Min(0.02f)] private float noiseLifetime = 0.45f;

        private Coroutine pressRoutine;
        private AudioClip generatedRingClip;
        private Vector3 buttonRestLocalPosition;
        private float nextRingTime;

        private void Awake()
        {
            if (buttonVisual == null)
            {
                buttonVisual = transform;
            }

            buttonRestLocalPosition = buttonVisual.localPosition;
            ResolveAudioSource();
        }

        private void OnDisable()
        {
            if (pressRoutine != null)
            {
                StopCoroutine(pressRoutine);
                pressRoutine = null;
            }

            if (buttonVisual != null)
            {
                buttonVisual.localPosition = buttonRestLocalPosition;
            }
        }

        public bool CanInteract(PlayerInteractor interactor)
        {
            return Time.time >= nextRingTime;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            nextRingTime = Time.time + interactionCooldown;
            PlayRing();
            EmitNeighborNoise();
            AnimateButtonPress();
        }

        private void PlayRing()
        {
            if (audioSource == null)
            {
                return;
            }

            AudioClip clip = GetRingClip();
            if (clip == null)
            {
                return;
            }

            audioSource.pitch = Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
            audioSource.PlayOneShot(clip, ringVolume);
        }

        private AudioClip GetRingClip()
        {
            if (ringClips != null && ringClips.Length > 0)
            {
                return ringClips[Random.Range(0, ringClips.Length)];
            }

            if (generatedRingClip == null)
            {
                generatedRingClip = CreateGeneratedRingClip();
            }

            return generatedRingClip;
        }

        private AudioClip CreateGeneratedRingClip()
        {
            const int sampleRate = 22050;
            const float clipDuration = 0.7f;
            int sampleCount = Mathf.RoundToInt(sampleRate * clipDuration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Exp(-time * 4.2f);
                float firstTone = Mathf.Sin(2f * Mathf.PI * 880f * time);
                float secondTone = Mathf.Sin(2f * Mathf.PI * 1320f * time) * 0.45f;
                float delayedTone = time > 0.18f
                    ? Mathf.Sin(2f * Mathf.PI * 660f * (time - 0.18f)) * Mathf.Exp(-(time - 0.18f) * 5.6f)
                    : 0f;
                samples[i] = (firstTone + secondTone) * envelope * 0.38f + delayedTone * 0.32f;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedDoorbell", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void EmitNeighborNoise()
        {
            if (noiseRadius <= 0f || noiseLoudness <= 0f)
            {
                return;
            }

            GameObject noiseObject = new GameObject("DoorbellNoiseEvent");
            noiseObject.transform.position = transform.position;

            SphereCollider sphereCollider = noiseObject.AddComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
            sphereCollider.radius = noiseRadius;

            Rigidbody noiseBody = noiseObject.AddComponent<Rigidbody>();
            noiseBody.isKinematic = true;
            noiseBody.useGravity = false;

            NoiseEvent noiseEvent = noiseObject.AddComponent<NoiseEvent>();
            noiseEvent.Initialize(transform.position, noiseRadius, noiseLoudness, gameObject, noiseLifetime, alertUrgency);
        }

        private void AnimateButtonPress()
        {
            if (buttonVisual == null)
            {
                return;
            }

            if (pressRoutine != null)
            {
                StopCoroutine(pressRoutine);
            }

            pressRoutine = StartCoroutine(PressButton());
        }

        private IEnumerator PressButton()
        {
            Vector3 pressedPosition = buttonRestLocalPosition + pressedLocalOffset;
            yield return MoveButton(buttonVisual.localPosition, pressedPosition, pressDuration);
            yield return MoveButton(pressedPosition, buttonRestLocalPosition, releaseDuration);
            pressRoutine = null;
        }

        private IEnumerator MoveButton(Vector3 from, Vector3 to, float duration)
        {
            float timer = 0f;
            float moveDuration = Mathf.Max(0.01f, duration);
            while (timer < moveDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / moveDuration));
                buttonVisual.localPosition = Vector3.LerpUnclamped(from, to, t);
                yield return null;
            }

            buttonVisual.localPosition = to;
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
            audioSource.minDistance = audioMinDistance;
            audioSource.maxDistance = audioMaxDistance;
            audioSource.dopplerLevel = 0.1f;
        }

        private void OnValidate()
        {
            interactionCooldown = Mathf.Max(0f, interactionCooldown);
            pressDuration = Mathf.Max(0.01f, pressDuration);
            releaseDuration = Mathf.Max(0.01f, releaseDuration);
            audioMaxDistance = Mathf.Max(0.1f, audioMaxDistance);
            noiseLifetime = Mathf.Max(0.02f, noiseLifetime);
        }
    }
}
