using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Neighbor
{
    [DisallowMultipleComponent]
    public sealed class NeighborMemoryClueVisual : MonoBehaviour
    {
        private const string PulseLightName = "Neighbor Memory Clue Pulse";
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField, Range(0f, 1f)] private float rememberedCueIntensity = 0.34f;
        [SerializeField, Range(0f, 1f)] private float trackingCueIntensity = 0.72f;
        [SerializeField, Range(0f, 1f)] private float settledCueIntensity = 0.12f;
        [SerializeField, Min(0f)] private float defaultRememberDuration = 4f;
        [SerializeField, Min(0f)] private float defaultTrackingDuration = 5.5f;
        [SerializeField, Min(0f)] private float settledDuration = 1.4f;
        [SerializeField, Min(0f)] private float fadeSpeed = 1.8f;
        [SerializeField, Range(0f, 1f)] private float tintStrength = 0.34f;
        [SerializeField, Min(0f)] private float emissionBoost = 0.85f;
        [SerializeField, Min(0f)] private float pulseLightIntensity = 0.32f;
        [SerializeField, Min(0f)] private float pulseLightRange = 2.1f;
        [SerializeField] private Color rememberedColor = new(1f, 0.7f, 0.25f, 1f);
        [SerializeField] private Color trackingColor = new(1f, 0.28f, 0.08f, 1f);
        [SerializeField] private Color settledColor = new(0.72f, 0.86f, 1f, 1f);

        private Renderer[] targetRenderers;
        private MaterialPropertyBlock propertyBlock;
        private Light pulseLight;
        private Color cueColor = Color.white;
        private float currentIntensity;
        private float targetIntensity;
        private float holdUntilTime;
        private bool isTracking;
        private PlayerFeedbackEvents.NeighborMemoryClueKind lastKind;

        public bool IsCueActive => currentIntensity > 0.01f || targetIntensity > 0.01f;
        public bool IsTracking => isTracking && IsCueActive;
        public float CurrentIntensity => currentIntensity;
        public PlayerFeedbackEvents.NeighborMemoryClueKind LastKind => lastKind;

        private void Awake()
        {
            CaptureTargets();
        }

        private void OnEnable()
        {
            CaptureTargets();
            ApplyCue();
        }

        private void OnDisable()
        {
            ClearRendererCue();
            if (pulseLight != null)
            {
                pulseLight.enabled = false;
            }
        }

        private void Update()
        {
            float desiredIntensity = Time.unscaledTime <= holdUntilTime ? targetIntensity : 0f;
            float previousIntensity = currentIntensity;
            currentIntensity = Mathf.MoveTowards(
                currentIntensity,
                desiredIntensity,
                fadeSpeed * Mathf.Max(0f, Time.unscaledDeltaTime));

            if (desiredIntensity <= 0f && currentIntensity <= 0.01f)
            {
                isTracking = false;
            }

            if (!Mathf.Approximately(previousIntensity, currentIntensity))
            {
                ApplyCue();
            }
        }

        public static NeighborMemoryClueVisual EnsureFor(GameObject source)
        {
            if (source == null)
            {
                return null;
            }

            return source.GetComponent<NeighborMemoryClueVisual>()
                ?? source.AddComponent<NeighborMemoryClueVisual>();
        }

        public void Remember(
            PlayerFeedbackEvents.NeighborMemoryClueKind kind,
            float urgency,
            float duration = -1f)
        {
            SetCue(
                kind,
                Mathf.Max(rememberedCueIntensity, Mathf.Clamp01(urgency) * 0.62f),
                duration >= 0f ? duration : defaultRememberDuration,
                rememberedColor,
                false);
        }

        public void Track(
            PlayerFeedbackEvents.NeighborMemoryClueKind kind,
            float urgency,
            float duration = -1f)
        {
            SetCue(
                kind,
                Mathf.Max(trackingCueIntensity, Mathf.Clamp01(urgency)),
                duration >= 0f ? duration : defaultTrackingDuration,
                trackingColor,
                true);
        }

        public void Settle(float duration = -1f)
        {
            isTracking = false;
            cueColor = settledColor;
            targetIntensity = Mathf.Min(
                Mathf.Max(settledCueIntensity, currentIntensity * 0.35f),
                Mathf.Max(currentIntensity, targetIntensity));
            currentIntensity = Mathf.Max(currentIntensity, targetIntensity);
            holdUntilTime = Time.unscaledTime + (duration >= 0f ? duration : settledDuration);
            ApplyCue();
        }

        private void SetCue(
            PlayerFeedbackEvents.NeighborMemoryClueKind kind,
            float intensity,
            float duration,
            Color color,
            bool tracking)
        {
            CaptureTargets();
            lastKind = kind;
            isTracking = tracking;
            cueColor = color;
            targetIntensity = Mathf.Clamp01(intensity);
            currentIntensity = Mathf.Max(currentIntensity, targetIntensity);
            holdUntilTime = Time.unscaledTime + Mathf.Max(0f, duration);
            ApplyCue();
        }

        private void CaptureTargets()
        {
            targetRenderers = GetComponentsInChildren<Renderer>(true);
            propertyBlock ??= new MaterialPropertyBlock();
            pulseLight = pulseLight != null ? pulseLight : FindPulseLight();
        }

        private Light FindPulseLight()
        {
            Transform existing = transform.Find(PulseLightName);
            if (existing != null && existing.TryGetComponent(out Light existingLight))
            {
                return existingLight;
            }

            GameObject lightObject = new(PulseLightName);
            lightObject.hideFlags = HideFlags.DontSave;
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = Vector3.up * 0.25f;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.enabled = false;
            return light;
        }

        private void ApplyCue()
        {
            ApplyRendererCue();
            ApplyPulseLight();
        }

        private void ApplyRendererCue()
        {
            if (targetRenderers == null)
            {
                return;
            }

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer targetRenderer = targetRenderers[i];
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.GetPropertyBlock(propertyBlock);
                Color baseColor = GetBaseColor(targetRenderer);
                Color tinted = Color.Lerp(baseColor, cueColor, tintStrength * currentIntensity);
                tinted.a = Mathf.Max(baseColor.a, tinted.a);
                propertyBlock.SetColor(BaseColorId, tinted);
                propertyBlock.SetColor(ColorId, tinted);
                propertyBlock.SetColor(EmissionColorId, cueColor * (emissionBoost * currentIntensity));
                targetRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void ApplyPulseLight()
        {
            if (pulseLight == null)
            {
                return;
            }

            bool enabledCue = currentIntensity > 0.01f;
            pulseLight.enabled = enabledCue;
            if (!enabledCue)
            {
                return;
            }

            pulseLight.color = cueColor;
            pulseLight.intensity = pulseLightIntensity * currentIntensity;
            pulseLight.range = pulseLightRange * Mathf.Lerp(0.7f, 1.25f, currentIntensity);
        }

        private void ClearRendererCue()
        {
            if (targetRenderers == null)
            {
                return;
            }

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                if (targetRenderers[i] != null)
                {
                    targetRenderers[i].SetPropertyBlock(null);
                }
            }
        }

        private static Color GetBaseColor(Renderer targetRenderer)
        {
            Material material = targetRenderer.sharedMaterial;
            if (material == null)
            {
                return Color.white;
            }

            if (material.HasProperty(BaseColorId))
            {
                return material.GetColor(BaseColorId);
            }

            return material.HasProperty(ColorId) ? material.GetColor(ColorId) : Color.white;
        }
    }
}
