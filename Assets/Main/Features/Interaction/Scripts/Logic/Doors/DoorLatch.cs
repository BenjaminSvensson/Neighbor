using System.Collections;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Collider))]
    public sealed class DoorLatch : MonoBehaviour, IInteractable, IInteractionTooltipProvider
    {
        [Header("Latch")]
        [SerializeField] private Door door;
        [SerializeField] private bool startsLatched = true;
        [SerializeField] private Transform latchVisual;
        [SerializeField] private Vector3 unlatchedLocalEuler = new Vector3(0f, 0f, -35f);
        [SerializeField] private Vector3 latchedLocalEuler = new Vector3(0f, 0f, 35f);
        [SerializeField, Min(0.01f)] private float toggleDuration = 0.1f;
        [SerializeField, Range(-1f, 1f)] private float minimumUseSideDot = 0.15f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] toggleClips;
        [SerializeField, Range(0f, 1f)] private float toggleVolume = 0.55f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.04f;

        private Coroutine animationRoutine;
        private AudioClip generatedToggleClip;
        private bool isLatched;

        public bool IsLatched => isLatched;

        private void Awake()
        {
            if (door == null)
            {
                door = GetComponentInParent<Door>();
            }

            if (latchVisual == null)
            {
                latchVisual = transform;
            }

            ResolveAudioSource();
        }

        private void Start()
        {
            SetLatched(startsLatched, false, true);
        }

        public bool CanInteract(PlayerInteractor interactor)
        {
            return door != null && IsInteractorOnLatchSide(interactor);
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            SetLatched(!isLatched, true, false);
        }

        public bool TryGetInteractionTooltip(
            PlayerInteractor interactor,
            InteractionTooltipContext context,
            out string actionText,
            out string keyText)
        {
            actionText = null;
            keyText = null;

            if (context != InteractionTooltipContext.FocusedInteractable || !CanInteract(interactor))
            {
                return false;
            }

            actionText = isLatched ? "Unlatch door" : "Latch door";
            keyText = "E";
            return true;
        }

        private bool IsInteractorOnLatchSide(PlayerInteractor interactor)
        {
            Transform interactorTransform = interactor != null ? interactor.transform : null;
            if (interactorTransform == null)
            {
                return false;
            }

            Vector3 toInteractor = interactorTransform.position - transform.position;
            return Vector3.Dot(transform.forward, toInteractor) >= minimumUseSideDot;
        }

        private void SetLatched(bool latched, bool playFeedback, bool instant)
        {
            isLatched = latched;

            if (isLatched)
            {
                door.SetLocked(true, true, false);
            }
            else
            {
                door.SetLocked(false, false, false);
            }

            if (instant)
            {
                SetVisualInstant();
            }
            else
            {
                AnimateVisual();
            }

            if (playFeedback)
            {
                PlayToggleSound();
            }
        }

        private void AnimateVisual()
        {
            if (latchVisual == null)
            {
                return;
            }

            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
            }

            animationRoutine = StartCoroutine(AnimateVisualRotation());
        }

        private IEnumerator AnimateVisualRotation()
        {
            Quaternion from = latchVisual.localRotation;
            Quaternion to = Quaternion.Euler(isLatched ? latchedLocalEuler : unlatchedLocalEuler);
            float timer = 0f;
            float duration = Mathf.Max(0.01f, toggleDuration);

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / duration));
                latchVisual.localRotation = Quaternion.Slerp(from, to, t);
                yield return null;
            }

            latchVisual.localRotation = to;
            animationRoutine = null;
        }

        private void SetVisualInstant()
        {
            if (latchVisual != null)
            {
                latchVisual.localRotation = Quaternion.Euler(isLatched ? latchedLocalEuler : unlatchedLocalEuler);
            }
        }

        private void PlayToggleSound()
        {
            if (audioSource == null)
            {
                return;
            }

            AudioClip clip = GetToggleClip();
            if (clip == null)
            {
                return;
            }

            audioSource.pitch = Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
            audioSource.PlayOneShot(clip, toggleVolume);
        }

        private AudioClip GetToggleClip()
        {
            if (toggleClips != null && toggleClips.Length > 0)
            {
                return toggleClips[Random.Range(0, toggleClips.Length)];
            }

            if (generatedToggleClip == null)
            {
                generatedToggleClip = CreateGeneratedToggleClip();
            }

            return generatedToggleClip;
        }

        private AudioClip CreateGeneratedToggleClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.09f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Exp(-time * 48f);
                float click = Mathf.Sin(2f * Mathf.PI * 1200f * time) * 0.32f;
                float scrape = Random.Range(-1f, 1f) * 0.18f;
                samples[i] = (click + scrape) * envelope;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedDoorLatchToggle", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
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
            audioSource.minDistance = 0.3f;
            audioSource.maxDistance = 5f;
            audioSource.dopplerLevel = 0.05f;
        }

        private void OnValidate()
        {
            toggleDuration = Mathf.Max(0.01f, toggleDuration);
            minimumUseSideDot = Mathf.Clamp(minimumUseSideDot, -1f, 1f);
        }
    }
}
