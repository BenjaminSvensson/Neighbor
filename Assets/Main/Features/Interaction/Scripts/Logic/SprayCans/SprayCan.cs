using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Pickupable))]
    public sealed class SprayCan : MonoBehaviour, IPrimaryUseInteractable, IInteractionTooltipProvider
    {
        [Header("Spray")]
        [SerializeField, Min(0.1f)] private float sprayRange = 3.5f;
        [SerializeField, Range(1f, 80f)] private float coneAngle = 28f;
        [SerializeField, Range(1, 16)] private int rayCount = 7;
        [SerializeField, Min(0f)] private float useCooldown = 0.18f;
        [SerializeField] private LayerMask sprayMask = ~0;

        [Header("Paint")]
        [SerializeField] private Color paintColor = new Color(0.1f, 0.85f, 1f, 1f);
        [SerializeField, Min(0.01f)] private float splatSize = 0.18f;
        [SerializeField, Min(0.01f)] private float splatLifetime = 45f;
        [SerializeField, Min(0f)] private float surfaceOffset = 0.006f;

        [Header("Effects")]
        [SerializeField, Min(0f)] private float securityCameraBlindDuration = 8f;
        [SerializeField, Min(0f)] private float revealRadius = 1.1f;
        [SerializeField, Min(0f)] private float revealDuration = 8f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] sprayClips;
        [SerializeField, Range(0f, 1f)] private float sprayVolume = 0.45f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.04f;

        private readonly RaycastHit[] sprayHits = new RaycastHit[4];
        private readonly Collider[] revealHits = new Collider[24];
        private Pickupable pickupable;
        private AudioClip generatedSprayClip;
        private float nextUseTime;

        private void Awake()
        {
            pickupable = GetComponent<Pickupable>();
            ResolveAudioSource();
        }

        public bool CanPrimaryUse(PlayerInteractor interactor)
        {
            return pickupable != null && pickupable.IsHeld && Time.time >= nextUseTime;
        }

        public void PrimaryUse(PlayerInteractor interactor)
        {
            if (!CanPrimaryUse(interactor))
            {
                return;
            }

            nextUseTime = Time.time + useCooldown;
            PlaySpraySound();

            Transform viewTransform = interactor != null ? interactor.ViewTransform : transform;
            Vector3 origin = viewTransform.position;
            SprayCone(origin, viewTransform.forward, viewTransform.up, viewTransform.right);
            RevealNearbyTripwires(origin + viewTransform.forward * Mathf.Min(sprayRange, revealRadius + 0.5f));
        }

        public bool TryGetInteractionTooltip(
            PlayerInteractor interactor,
            InteractionTooltipContext context,
            out string actionText,
            out string keyText)
        {
            actionText = null;
            keyText = null;

            if (context != InteractionTooltipContext.HeldPrimaryUse)
            {
                return false;
            }

            actionText = "Spray";
            keyText = "Left Mouse";
            return true;
        }

        private void SprayCone(Vector3 origin, Vector3 forward, Vector3 up, Vector3 right)
        {
            int count = Mathf.Max(1, rayCount);
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = GetSprayDirection(i, count, forward, up, right);
                int hitCount = Physics.RaycastNonAlloc(origin, direction, sprayHits, sprayRange, sprayMask, QueryTriggerInteraction.Ignore);
                if (!TryChooseClosestHit(hitCount, out RaycastHit hit))
                {
                    continue;
                }

                SpawnPaintSplat(hit);
                BlindSecurityCamera(hit);
                RevealNearbyTripwires(hit.point);
            }
        }

        private Vector3 GetSprayDirection(int index, int count, Vector3 forward, Vector3 up, Vector3 right)
        {
            if (count <= 1 || index == 0)
            {
                return forward;
            }

            float angle = (index - 1) / (float)(count - 1) * Mathf.PI * 2f;
            float radius01 = index % 2 == 0 ? 1f : 0.55f;
            Vector3 offset = (Mathf.Cos(angle) * right + Mathf.Sin(angle) * up) * Mathf.Tan(coneAngle * 0.5f * Mathf.Deg2Rad) * radius01;
            return (forward + offset).normalized;
        }

        private bool TryChooseClosestHit(int hitCount, out RaycastHit closestHit)
        {
            closestHit = default;
            bool hasHit = false;
            float closestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = sprayHits[i];
                if (hit.collider == null || hit.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hit.distance;
                closestHit = hit;
                hasHit = true;
            }

            return hasHit;
        }

        private void SpawnPaintSplat(RaycastHit hit)
        {
            GameObject splat = GameObject.CreatePrimitive(PrimitiveType.Quad);
            splat.name = "PlaceholderPaintSplat";
            splat.layer = hit.collider != null ? hit.collider.gameObject.layer : gameObject.layer;
            splat.transform.position = hit.point + hit.normal * surfaceOffset;
            splat.transform.rotation = Quaternion.LookRotation(hit.normal, Vector3.up) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            float size = splatSize * Random.Range(0.75f, 1.25f);
            splat.transform.localScale = new Vector3(size, size, 1f);

            Collider splatCollider = splat.GetComponent<Collider>();
            if (splatCollider != null)
            {
                Destroy(splatCollider);
            }

            Renderer renderer = splat.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = PaintSplat.CreateMaterial(paintColor);
            }

            PaintSplat paintSplat = splat.AddComponent<PaintSplat>();
            paintSplat.Initialize(splatLifetime);
        }

        private void BlindSecurityCamera(RaycastHit hit)
        {
            SecurityCamera securityCamera = hit.collider != null ? hit.collider.GetComponentInParent<SecurityCamera>() : null;
            securityCamera?.BlindFor(securityCameraBlindDuration);
        }

        private void RevealNearbyTripwires(Vector3 center)
        {
            if (revealRadius <= 0f || revealDuration <= 0f)
            {
                return;
            }

            int hitCount = Physics.OverlapSphereNonAlloc(center, revealRadius, revealHits, sprayMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = revealHits[i];
                if (hit == null)
                {
                    continue;
                }

                SwingingAxeTripWire tripWire = hit.GetComponentInParent<SwingingAxeTripWire>();
                tripWire?.HighlightFor(revealDuration);

                SprayRevealTarget revealTarget = hit.GetComponentInParent<SprayRevealTarget>() ?? hit.GetComponentInChildren<SprayRevealTarget>();
                revealTarget?.RevealFor(revealDuration);
            }
        }

        private void PlaySpraySound()
        {
            if (audioSource == null)
            {
                return;
            }

            AudioClip clip = GetSprayClip();
            if (clip == null)
            {
                return;
            }

            audioSource.pitch = Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
            audioSource.PlayOneShot(clip, sprayVolume);
        }

        private AudioClip GetSprayClip()
        {
            if (sprayClips != null && sprayClips.Length > 0)
            {
                return sprayClips[Random.Range(0, sprayClips.Length)];
            }

            if (generatedSprayClip == null)
            {
                generatedSprayClip = CreateGeneratedSprayClip();
            }

            return generatedSprayClip;
        }

        private AudioClip CreateGeneratedSprayClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.18f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Clamp01(1f - time / duration);
                float hiss = Random.Range(-1f, 1f) * 0.45f;
                float pressure = Mathf.Sin(2f * Mathf.PI * 180f * time) * 0.12f;
                samples[i] = (hiss + pressure) * envelope;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedSpray", sampleCount, 1, sampleRate, false);
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
            sprayRange = Mathf.Max(0.1f, sprayRange);
            coneAngle = Mathf.Clamp(coneAngle, 1f, 80f);
            rayCount = Mathf.Clamp(rayCount, 1, 16);
            useCooldown = Mathf.Max(0f, useCooldown);
            splatSize = Mathf.Max(0.01f, splatSize);
            splatLifetime = Mathf.Max(0.01f, splatLifetime);
            securityCameraBlindDuration = Mathf.Max(0f, securityCameraBlindDuration);
            revealRadius = Mathf.Max(0f, revealRadius);
            revealDuration = Mathf.Max(0f, revealDuration);
        }
    }
}
