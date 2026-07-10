using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Neighbor
{
    [DisallowMultipleComponent]
    public sealed class NeighborMemoryClueVisual : MonoBehaviour
    {
        private const string WorldMarkerName = "Neighbor Memory Clue Evidence";
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
        [SerializeField, Min(0f)] private float rendererCueMaximumDistance = 0.9f;
        [SerializeField] private Color rememberedColor = new(1f, 0.7f, 0.25f, 1f);
        [SerializeField] private Color trackingColor = new(1f, 0.28f, 0.08f, 1f);
        [SerializeField] private Color settledColor = new(0.72f, 0.86f, 1f, 1f);

        private Renderer[] targetRenderers;
        private MaterialPropertyBlock propertyBlock;
        private NeighborMemoryClueWorldMarker worldMarker;
        private Color cueColor = Color.white;
        private Vector3 cueWorldPosition;
        private float currentIntensity;
        private float targetIntensity;
        private float holdUntilTime;
        private bool isTracking;
        private bool rendererCueAligned = true;
        private bool usesWorldCuePosition;
        private PlayerFeedbackEvents.NeighborMemoryClueKind lastKind;

        public bool IsCueActive => currentIntensity > 0.01f || targetIntensity > 0.01f;
        public bool IsTracking => isTracking && IsCueActive;
        public float CurrentIntensity => currentIntensity;
        public PlayerFeedbackEvents.NeighborMemoryClueKind LastKind => lastKind;
        public bool UsesWorldCuePosition => usesWorldCuePosition;
        public Vector3 CueWorldPosition => cueWorldPosition;
        public bool IsSourceCueAligned => rendererCueAligned;
        public Light WorldMarkerLight => worldMarker != null ? worldMarker.CueLight : null;

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
        }

        private void OnDestroy()
        {
            if (worldMarker == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(worldMarker.gameObject);
            }
            else
            {
                DestroyImmediate(worldMarker.gameObject);
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

            bool isAligned = GetRendererCueAlignment();
            if (isAligned != rendererCueAligned)
            {
                rendererCueAligned = isAligned;
                ApplyRendererCue();
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
            RememberAt(kind, urgency, transform.position, duration);
        }

        public void RememberAt(
            PlayerFeedbackEvents.NeighborMemoryClueKind kind,
            float urgency,
            Vector3 worldPosition,
            float duration = -1f)
        {
            SetCue(
                kind,
                Mathf.Max(rememberedCueIntensity, Mathf.Clamp01(urgency) * 0.62f),
                duration >= 0f ? duration : defaultRememberDuration,
                GetClueColor(kind, rememberedColor),
                worldPosition,
                false);
        }

        public void Track(
            PlayerFeedbackEvents.NeighborMemoryClueKind kind,
            float urgency,
            float duration = -1f)
        {
            TrackAt(kind, urgency, transform.position, duration);
        }

        public void TrackAt(
            PlayerFeedbackEvents.NeighborMemoryClueKind kind,
            float urgency,
            Vector3 worldPosition,
            float duration = -1f)
        {
            SetCue(
                kind,
                Mathf.Max(trackingCueIntensity, Mathf.Clamp01(urgency)),
                duration >= 0f ? duration : defaultTrackingDuration,
                GetClueColor(kind, trackingColor),
                worldPosition,
                true);
        }

        public void Settle(float duration = -1f)
        {
            isTracking = false;
            cueColor = GetClueColor(lastKind, settledColor);
            targetIntensity = Mathf.Min(
                Mathf.Max(settledCueIntensity, currentIntensity * 0.35f),
                Mathf.Max(currentIntensity, targetIntensity));
            currentIntensity = Mathf.Max(currentIntensity, targetIntensity);
            holdUntilTime = Time.unscaledTime + (duration >= 0f ? duration : settledDuration);
            worldMarker?.Settle(cueColor, targetIntensity, duration >= 0f ? duration : settledDuration);
            ApplyCue();
        }

        private void SetCue(
            PlayerFeedbackEvents.NeighborMemoryClueKind kind,
            float intensity,
            float duration,
            Color color,
            Vector3 worldPosition,
            bool tracking)
        {
            CaptureTargets();
            lastKind = kind;
            isTracking = tracking;
            cueColor = color;
            cueWorldPosition = worldPosition;
            usesWorldCuePosition = true;
            rendererCueAligned = GetRendererCueAlignment();
            targetIntensity = Mathf.Clamp01(intensity);
            currentIntensity = Mathf.Max(currentIntensity, targetIntensity);
            holdUntilTime = Time.unscaledTime + Mathf.Max(0f, duration);
            EnsureWorldMarker().Configure(
                WorldMarkerName,
                cueWorldPosition,
                cueColor,
                targetIntensity,
                duration,
                fadeSpeed,
                pulseLightIntensity,
                pulseLightRange,
                tracking);
            ApplyCue();
        }

        private void CaptureTargets()
        {
            targetRenderers = GetComponentsInChildren<Renderer>(true);
            propertyBlock ??= new MaterialPropertyBlock();
        }

        private NeighborMemoryClueWorldMarker EnsureWorldMarker()
        {
            if (worldMarker != null)
            {
                return worldMarker;
            }

            GameObject markerObject = new(WorldMarkerName)
            {
                hideFlags = HideFlags.DontSave
            };
            worldMarker = markerObject.AddComponent<NeighborMemoryClueWorldMarker>();
            return worldMarker;
        }

        private void ApplyCue()
        {
            ApplyRendererCue();
        }

        private void ApplyRendererCue()
        {
            if (targetRenderers == null)
            {
                return;
            }

            if (!rendererCueAligned)
            {
                ClearRendererCue();
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

        private bool GetRendererCueAlignment()
        {
            if (!usesWorldCuePosition)
            {
                return true;
            }

            float maximumDistance = Mathf.Max(0f, rendererCueMaximumDistance);
            return (transform.position - cueWorldPosition).sqrMagnitude <= maximumDistance * maximumDistance;
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

        private static Color GetClueColor(
            PlayerFeedbackEvents.NeighborMemoryClueKind kind,
            Color baseColor)
        {
            Color clueColor = kind switch
            {
                PlayerFeedbackEvents.NeighborMemoryClueKind.KeyStolen => new Color(1f, 0.16f, 0.05f, 1f),
                PlayerFeedbackEvents.NeighborMemoryClueKind.GlassBroken => new Color(0.42f, 0.76f, 1f, 1f),
                PlayerFeedbackEvents.NeighborMemoryClueKind.ObjectMoved => new Color(1f, 0.58f, 0.12f, 1f),
                _ => new Color(1f, 0.76f, 0.24f, 1f)
            };
            return Color.Lerp(baseColor, clueColor, 0.34f);
        }
    }

    [DisallowMultipleComponent]
    public sealed class NeighborMemoryClueWorldMarker : MonoBehaviour
    {
        private Light cueLight;
        private Color cueColor = Color.white;
        private float currentIntensity;
        private float targetIntensity;
        private float holdUntilTime;
        private float fadeSpeed = 1.8f;
        private float maximumLightIntensity = 0.32f;
        private float maximumLightRange = 2.1f;
        private float pulsePhase;
        private bool hasPulsePhase;
        private bool isTracking;

        public Light CueLight
        {
            get
            {
                EnsureLight();
                return cueLight;
            }
        }
        public bool IsCueActive => currentIntensity > 0.01f || targetIntensity > 0.01f;
        public bool IsTracking => isTracking && IsCueActive;
        public float CurrentIntensity => currentIntensity;

        private void Awake()
        {
            EnsureLight();
        }

        private void Update()
        {
            float desiredIntensity = Time.unscaledTime <= holdUntilTime ? targetIntensity : 0f;
            currentIntensity = Mathf.MoveTowards(
                currentIntensity,
                desiredIntensity,
                fadeSpeed * Mathf.Max(0f, Time.unscaledDeltaTime));
            ApplyLight();

            if (desiredIntensity <= 0f && currentIntensity <= 0.01f)
            {
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                else
                {
                    DestroyImmediate(gameObject);
                }
            }
        }

        public void Configure(
            string markerName,
            Vector3 worldPosition,
            Color color,
            float intensity,
            float duration,
            float markerFadeSpeed,
            float lightIntensity,
            float lightRange,
            bool tracking)
        {
            EnsureLight();
            name = markerName;
            transform.position = worldPosition + Vector3.up * 0.22f;
            cueColor = color;
            targetIntensity = Mathf.Clamp01(intensity);
            currentIntensity = Mathf.Max(currentIntensity, targetIntensity);
            holdUntilTime = Time.unscaledTime + Mathf.Max(0f, duration);
            fadeSpeed = Mathf.Max(0f, markerFadeSpeed);
            maximumLightIntensity = Mathf.Max(0f, lightIntensity);
            maximumLightRange = Mathf.Max(0f, lightRange);
            isTracking = tracking;
            ApplyLight();
        }

        public void Settle(Color color, float intensity, float duration)
        {
            cueColor = color;
            targetIntensity = Mathf.Clamp01(intensity);
            currentIntensity = Mathf.Max(currentIntensity, targetIntensity);
            holdUntilTime = Time.unscaledTime + Mathf.Max(0f, duration);
            isTracking = false;
            ApplyLight();
        }

        private void ApplyLight()
        {
            EnsureLight();

            bool showCue = currentIntensity > 0.01f;
            cueLight.enabled = showCue;
            if (!showCue)
            {
                return;
            }

            float pulseSpeed = isTracking ? 5.2f : 3.1f;
            float pulseDepth = isTracking ? 0.24f : 0.14f;
            float pulse = 1f - pulseDepth
                + Mathf.Sin(Time.unscaledTime * pulseSpeed + pulsePhase) * pulseDepth;
            cueLight.color = cueColor;
            cueLight.intensity = maximumLightIntensity * currentIntensity * Mathf.Max(0.55f, pulse);
            cueLight.range = maximumLightRange * Mathf.Lerp(0.7f, 1.25f, currentIntensity);
        }

        private void EnsureLight()
        {
            if (cueLight == null)
            {
                cueLight = GetComponent<Light>();
                if (cueLight == null)
                {
                    cueLight = gameObject.AddComponent<Light>();
                }

                cueLight.type = LightType.Point;
                cueLight.shadows = LightShadows.None;
                cueLight.renderMode = LightRenderMode.ForcePixel;
                cueLight.enabled = false;
            }

            if (!hasPulsePhase)
            {
                pulsePhase = Random.value * Mathf.PI * 2f;
                hasPulsePhase = true;
            }
        }
    }
}
