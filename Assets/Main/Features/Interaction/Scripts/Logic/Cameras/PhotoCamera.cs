using System.Collections;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Pickupable))]
    public sealed class PhotoCamera : MonoBehaviour, IPrimaryUseInteractable
    {
        [Header("Capture")]
        [SerializeField, Min(64)] private int captureWidth = 512;
        [SerializeField, Min(64)] private int captureHeight = 384;
        [SerializeField, Min(0f)] private float shutterCooldown = 0.45f;
        [SerializeField, Min(0.05f)] private float photoEjectDistance = 0.65f;
        [SerializeField, Min(0f)] private float photoEjectImpulse = 1.4f;

        [Header("Flash")]
        [SerializeField] private Light flashLight;
        [SerializeField, Min(0f)] private float flashDuration = 0.08f;
        [SerializeField, Min(0f)] private float flashIntensity = 9f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] shutterClips;
        [SerializeField, Range(0f, 1f)] private float shutterVolume = 0.75f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.035f;

        private Pickupable pickupable;
        private AudioClip generatedShutterClip;
        private float nextShutterTime;
        private Coroutine flashRoutine;

        private void Awake()
        {
            pickupable = GetComponent<Pickupable>();
            if (flashLight == null)
            {
                flashLight = GetComponentInChildren<Light>(true);
            }

            if (flashLight == null)
            {
                GameObject flashObject = new GameObject("GeneratedPhotoFlash");
                flashObject.transform.SetParent(transform, false);
                flashObject.transform.localPosition = new Vector3(0f, 0.05f, 0.26f);
                flashObject.transform.localRotation = Quaternion.identity;
                flashLight = flashObject.AddComponent<Light>();
                flashLight.type = LightType.Spot;
                flashLight.range = 18f;
                flashLight.spotAngle = 72f;
                flashLight.innerSpotAngle = 38f;
                flashLight.color = new Color(1f, 0.94f, 0.78f, 1f);
            }

            if (flashLight != null)
            {
                flashLight.enabled = false;
                flashLight.intensity = flashIntensity;
            }

            ResolveAudioSource();
        }

        public bool CanPrimaryUse(PlayerInteractor interactor)
        {
            return pickupable != null && pickupable.IsHeld && Time.time >= nextShutterTime;
        }

        public void PrimaryUse(PlayerInteractor interactor)
        {
            if (!CanPrimaryUse(interactor))
            {
                return;
            }

            nextShutterTime = Time.time + shutterCooldown;
            PlayShutterSound();
            TriggerFlash();

            Texture2D capturedTexture = CapturePlayerView(interactor);
            if (capturedTexture != null)
            {
                SpawnPhoto(interactor, capturedTexture);
            }
        }

        private Texture2D CapturePlayerView(PlayerInteractor interactor)
        {
            Camera sourceCamera = ResolveSourceCamera(interactor);
            if (sourceCamera == null)
            {
                return null;
            }

            RenderTexture previousTargetTexture = sourceCamera.targetTexture;
            RenderTexture previousActiveTexture = RenderTexture.active;
            RenderTexture renderTexture = new RenderTexture(captureWidth, captureHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = $"{name}_PhotoCapture"
            };

            Texture2D texture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGBA32, false)
            {
                name = $"{name}_CapturedPhoto"
            };

            sourceCamera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            sourceCamera.Render();
            texture.ReadPixels(new Rect(0f, 0f, captureWidth, captureHeight), 0, 0);
            texture.Apply(false, false);

            sourceCamera.targetTexture = previousTargetTexture;
            RenderTexture.active = previousActiveTexture;
            renderTexture.Release();
            Destroy(renderTexture);

            return texture;
        }

        private Camera ResolveSourceCamera(PlayerInteractor interactor)
        {
            if (interactor != null)
            {
                Camera camera = interactor.GetComponentInChildren<Camera>() ?? interactor.GetComponentInParent<Camera>();
                if (camera != null)
                {
                    return camera;
                }
            }

            if (Camera.main != null)
            {
                return Camera.main;
            }

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera != null && camera.cameraType == CameraType.Game && camera.isActiveAndEnabled)
                {
                    return camera;
                }
            }

            return null;
        }

        private void SpawnPhoto(PlayerInteractor interactor, Texture2D capturedTexture)
        {
            Transform viewTransform = interactor != null ? interactor.ViewTransform : transform;
            Vector3 spawnPosition = viewTransform.position + viewTransform.forward * photoEjectDistance - viewTransform.up * 0.18f;
            Quaternion spawnRotation = Quaternion.LookRotation(viewTransform.forward, viewTransform.up) * Quaternion.Euler(90f, 0f, 0f);

            GameObject photo = new GameObject("CapturedPhoto");
            photo.layer = gameObject.layer;
            photo.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

            BoxCollider collider = photo.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.62f, 0.02f, 0.48f);

            Rigidbody body = photo.AddComponent<Rigidbody>();
            body.mass = 0.08f;
            body.linearDamping = 0.25f;
            body.angularDamping = 0.35f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            photo.AddComponent<Pickupable>();

            GameObject paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
            paper.name = "PhotoPaper";
            paper.layer = photo.layer;
            paper.transform.SetParent(photo.transform, false);
            paper.transform.localPosition = Vector3.zero;
            paper.transform.localRotation = Quaternion.identity;
            paper.transform.localScale = new Vector3(0.62f, 0.018f, 0.48f);
            Destroy(paper.GetComponent<Collider>());
            SetRendererMaterial(paper.GetComponent<Renderer>(), Color.white, null);

            GameObject image = GameObject.CreatePrimitive(PrimitiveType.Quad);
            image.name = "PhotoImage";
            image.layer = photo.layer;
            image.transform.SetParent(photo.transform, false);
            image.transform.localPosition = new Vector3(0f, 0.011f, 0.02f);
            image.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            image.transform.localScale = new Vector3(0.52f, 0.34f, 1f);
            Destroy(image.GetComponent<Collider>());
            SetRendererMaterial(image.GetComponent<Renderer>(), Color.white, capturedTexture);

            body.AddForce(viewTransform.forward * photoEjectImpulse + Vector3.up * 0.25f, ForceMode.Impulse);
            body.AddTorque(Random.insideUnitSphere * 0.08f, ForceMode.Impulse);
        }

        private static void SetRendererMaterial(Renderer renderer, Color color, Texture texture)
        {
            if (renderer == null)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader)
            {
                color = color,
                mainTexture = texture
            };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            if (texture != null)
            {
                material.SetTexture("_BaseMap", texture);
                material.SetTexture("_MainTex", texture);
            }

            renderer.material = material;
        }

        private void TriggerFlash()
        {
            if (flashLight == null)
            {
                return;
            }

            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            flashRoutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            flashLight.intensity = flashIntensity;
            flashLight.enabled = true;
            yield return new WaitForSeconds(flashDuration);
            flashLight.enabled = false;
            flashRoutine = null;
        }

        private void PlayShutterSound()
        {
            if (audioSource == null)
            {
                return;
            }

            AudioClip clip = GetShutterClip();
            if (clip == null)
            {
                return;
            }

            audioSource.pitch = Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
            audioSource.PlayOneShot(clip, shutterVolume);
        }

        private AudioClip GetShutterClip()
        {
            if (shutterClips != null && shutterClips.Length > 0)
            {
                return shutterClips[Random.Range(0, shutterClips.Length)];
            }

            if (generatedShutterClip == null)
            {
                generatedShutterClip = CreateGeneratedShutterClip();
            }

            return generatedShutterClip;
        }

        private AudioClip CreateGeneratedShutterClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.16f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float snapEnvelope = Mathf.Exp(-time * 42f);
                float motorEnvelope = Mathf.Exp(-Mathf.Max(0f, time - 0.035f) * 28f);
                float snap = Mathf.Sin(2f * Mathf.PI * 1800f * time) * snapEnvelope * 0.36f;
                float clack = Mathf.Sin(2f * Mathf.PI * 330f * time) * motorEnvelope * 0.18f;
                float noise = Random.Range(-1f, 1f) * snapEnvelope * 0.16f;
                samples[i] = snap + clack + noise;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedCameraShutter", sampleCount, 1, sampleRate, false);
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
            audioSource.maxDistance = 8f;
            audioSource.dopplerLevel = 0.05f;
        }

        private void OnValidate()
        {
            captureWidth = Mathf.Max(64, captureWidth);
            captureHeight = Mathf.Max(64, captureHeight);
            shutterCooldown = Mathf.Max(0f, shutterCooldown);
            flashDuration = Mathf.Max(0f, flashDuration);
            flashIntensity = Mathf.Max(0f, flashIntensity);
        }
    }
}
