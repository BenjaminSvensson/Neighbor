using System.Collections;
using System.Collections.Generic;
using Neighbor.Main.Features.Neighbor;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Collider))]
    public sealed class LightSwitch : MonoBehaviour, IInteractable
    {
        [SerializeField] private string circuitId = "default_room";
        [SerializeField] private CeilingLight[] explicitLights;
        [SerializeField] private Transform switchLever;
        [SerializeField] private Vector3 offLocalEuler = new Vector3(-18f, 0f, 0f);
        [SerializeField] private Vector3 onLocalEuler = new Vector3(18f, 0f, 0f);
        [SerializeField, Min(0.01f)] private float flipDuration = 0.08f;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] toggleClips;
        [SerializeField, Range(0f, 1f)] private float toggleVolume = 0.55f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.04f;

        private Coroutine animationRoutine;
        private AudioClip generatedToggleClip;
        private bool isOn = true;

        private void Awake()
        {
            if (switchLever == null)
            {
                switchLever = transform;
            }

            ResolveAudioSource();
            SyncStateFromTargets();
            SetLeverInstant();
        }

        public bool CanInteract(PlayerInteractor interactor)
        {
            return true;
        }

        public void Interact(PlayerInteractor interactor)
        {
            NeighborEnvironmentalAwareness.Report(transform.position, 0.3f, gameObject);
            Toggle();
        }

        public void Toggle()
        {
            bool nextState = !isOn;
            List<CeilingLight> lights = GetTargetLights();
            for (int i = 0; i < lights.Count; i++)
            {
                if (lights[i] != null)
                {
                    lights[i].SetOn(nextState);
                }
            }

            isOn = nextState;
            AnimateLever();
            PlayToggleSound();
        }

        private void SyncStateFromTargets()
        {
            List<CeilingLight> lights = GetTargetLights();
            for (int i = 0; i < lights.Count; i++)
            {
                if (lights[i] != null)
                {
                    isOn = lights[i].IsOn;
                    return;
                }
            }
        }

        private List<CeilingLight> GetTargetLights()
        {
            List<CeilingLight> lights = new();
            if (explicitLights != null)
            {
                for (int i = 0; i < explicitLights.Length; i++)
                {
                    if (explicitLights[i] != null && !lights.Contains(explicitLights[i]))
                    {
                        lights.Add(explicitLights[i]);
                    }
                }
            }

            IReadOnlyList<CeilingLight> circuitLights = CeilingLight.GetCircuitLights(circuitId);
            for (int i = 0; i < circuitLights.Count; i++)
            {
                if (circuitLights[i] != null && !lights.Contains(circuitLights[i]))
                {
                    lights.Add(circuitLights[i]);
                }
            }

            return lights;
        }

        private void AnimateLever()
        {
            if (switchLever == null)
            {
                return;
            }

            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
            }

            animationRoutine = StartCoroutine(AnimateLeverRotation());
        }

        private IEnumerator AnimateLeverRotation()
        {
            Quaternion from = switchLever.localRotation;
            Quaternion to = Quaternion.Euler(isOn ? onLocalEuler : offLocalEuler);
            float timer = 0f;
            float duration = Mathf.Max(0.01f, flipDuration);
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / duration));
                switchLever.localRotation = Quaternion.Slerp(from, to, t);
                yield return null;
            }

            switchLever.localRotation = to;
            animationRoutine = null;
        }

        private void SetLeverInstant()
        {
            if (switchLever != null)
            {
                switchLever.localRotation = Quaternion.Euler(isOn ? onLocalEuler : offLocalEuler);
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
            const float duration = 0.07f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Exp(-time * 55f);
                float click = Mathf.Sin(2f * Mathf.PI * 1100f * time) * 0.3f;
                float snap = Random.Range(-1f, 1f) * 0.25f;
                samples[i] = (click + snap) * envelope;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedLightSwitchToggle", sampleCount, 1, sampleRate, false);
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
            audioSource.minDistance = 0.35f;
            audioSource.maxDistance = 5f;
            audioSource.dopplerLevel = 0.05f;
        }

        private void OnValidate()
        {
            flipDuration = Mathf.Max(0.01f, flipDuration);
        }
    }
}
