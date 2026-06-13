using Neighbor.Main.Features.Interaction;
using Neighbor.Main.Features.Neighbor;
using UnityEngine;
using UnityEngine.UI;

namespace Neighbor.Main.Features.Player
{
    public sealed class PlayerAwarenessHudView : MonoBehaviour
    {
        private const float MessageDuration = 4.5f;

        private CanvasGroup canvasGroup;
        private Image suspicionFill;
        private Image noiseFill;
        private Text awarenessText;
        private Text warningText;
        private PlayerController player;
        private NeighborBrain trackedNeighbor;
        private float noiseLevel;
        private float cameraWarningUntil;
        private float messageUntil;

        private void Awake()
        {
            BuildHud();
        }

        private void OnEnable()
        {
            PlayerFeedbackEvents.NoiseEmitted += HandleNoise;
            PlayerFeedbackEvents.CameraDetectedPlayer += HandleCameraDetection;
            PlayerFeedbackEvents.SecurityEscalated += HandleSecurityEscalation;
        }

        private void OnDisable()
        {
            PlayerFeedbackEvents.NoiseEmitted -= HandleNoise;
            PlayerFeedbackEvents.CameraDetectedPlayer -= HandleCameraDetection;
            PlayerFeedbackEvents.SecurityEscalated -= HandleSecurityEscalation;
        }

        private void Update()
        {
            ResolveTargets();
            noiseLevel = Mathf.MoveTowards(noiseLevel, 0f, Time.unscaledDeltaTime * 0.55f);

            bool inputBlocked = InteractionOverlayState.IsGameplayInputBlocked;
            canvasGroup.alpha = inputBlocked ? 0f : 1f;

            UpdateAwareness();
            UpdateNoise();
            UpdateWarning();
        }

        private void ResolveTargets()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerController>();
            }

            if (trackedNeighbor == null)
            {
                trackedNeighbor = FindAnyObjectByType<NeighborBrain>();
            }
        }

        private void UpdateAwareness()
        {
            float suspicion = trackedNeighbor != null ? trackedNeighbor.Suspicion : 0f;
            suspicionFill.fillAmount = suspicion;
            suspicionFill.color = GetAwarenessColor(suspicion);

            if (trackedNeighbor == null || trackedNeighbor.CurrentSuspicionLevel == NeighborBrain.SuspicionLevel.Relaxed)
            {
                awarenessText.text = "UNNOTICED";
                return;
            }

            awarenessText.text = trackedNeighbor.CurrentState == NeighborBrain.BehaviorState.Chase
                ? "CHASE"
                : trackedNeighbor.CurrentSuspicionLevel.ToString().ToUpperInvariant();
        }

        private void UpdateNoise()
        {
            noiseFill.fillAmount = noiseLevel;
            noiseFill.color = Color.Lerp(
                new Color(0.35f, 0.72f, 1f, 0.85f),
                new Color(1f, 0.34f, 0.12f, 0.95f),
                noiseLevel);
        }

        private void UpdateWarning()
        {
            if (Time.unscaledTime < cameraWarningUntil)
            {
                warningText.text = "CAMERA DETECTED YOU";
                warningText.color = new Color(1f, 0.18f, 0.12f, 1f);
                return;
            }

            if (Time.unscaledTime < messageUntil)
            {
                return;
            }

            warningText.text = string.Empty;
        }

        private void HandleNoise(PlayerFeedbackEvents.NoiseFeedback feedback)
        {
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerController>();
            }

            if (player == null || Vector3.Distance(player.transform.position, feedback.Origin) > Mathf.Max(5f, feedback.Radius))
            {
                return;
            }

            noiseLevel = Mathf.Max(noiseLevel, feedback.Loudness);
        }

        private void HandleCameraDetection()
        {
            cameraWarningUntil = Time.unscaledTime + 2.25f;
        }

        private void HandleSecurityEscalation(PlayerFeedbackEvents.SecurityEscalationFeedback feedback)
        {
            warningText.text = feedback.Level > 0
                ? $"SECURITY ADAPTED - LEVEL {feedback.Level}"
                : "THE HOUSE RESET";
            warningText.color = feedback.Level > 0
                ? new Color(1f, 0.58f, 0.18f, 1f)
                : new Color(0.8f, 0.82f, 0.86f, 1f);
            messageUntil = Time.unscaledTime + MessageDuration;
        }

        private void BuildHud()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 75;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            awarenessText = CreateText("Awareness", font, 14, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(awarenessText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(280f, 24f));

            Image suspicionBackground = CreateImage("SuspicionBackground", new Color(0f, 0f, 0f, 0.55f));
            SetRect(suspicionBackground.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(280f, 8f));
            suspicionFill = CreateFill("SuspicionFill", suspicionBackground.transform);

            Text noiseLabel = CreateText("NoiseLabel", font, 12, FontStyle.Bold, TextAnchor.MiddleLeft);
            noiseLabel.text = "NOISE";
            noiseLabel.color = new Color(1f, 1f, 1f, 0.68f);
            SetRect(noiseLabel.rectTransform, Vector2.zero, Vector2.zero, new Vector2(32f, 104f), new Vector2(58f, 18f));

            Image noiseBackground = CreateImage("NoiseBackground", new Color(0f, 0f, 0f, 0.5f));
            SetRect(noiseBackground.rectTransform, Vector2.zero, Vector2.zero, new Vector2(102f, 108f), new Vector2(170f, 7f));
            noiseFill = CreateFill("NoiseFill", noiseBackground.transform);

            warningText = CreateText("Warning", font, 18, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(warningText.rectTransform, new Vector2(0.5f, 0.75f), new Vector2(0.5f, 0.75f), Vector2.zero, new Vector2(560f, 32f));
        }

        private Text CreateText(string objectName, Font font, int fontSize, FontStyle style, TextAnchor alignment)
        {
            GameObject textObject = new(objectName, typeof(RectTransform));
            textObject.transform.SetParent(transform, false);
            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private Image CreateImage(string objectName, Color color)
        {
            GameObject imageObject = new(objectName, typeof(RectTransform));
            imageObject.transform.SetParent(transform, false);
            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Image CreateFill(string objectName, Transform parent)
        {
            GameObject fillObject = new(objectName, typeof(RectTransform));
            fillObject.transform.SetParent(parent, false);
            Image fill = fillObject.AddComponent<Image>();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.raycastTarget = false;
            SetRect(fill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return fill;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Color GetAwarenessColor(float suspicion)
        {
            return suspicion < 0.48f
                ? new Color(1f, 0.76f, 0.2f, 0.9f)
                : Color.Lerp(new Color(1f, 0.48f, 0.12f, 0.95f), new Color(1f, 0.08f, 0.05f, 1f), suspicion);
        }
    }
}
