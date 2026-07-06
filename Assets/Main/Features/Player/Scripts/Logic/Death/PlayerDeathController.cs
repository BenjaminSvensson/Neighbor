using System.Collections;
using System.Collections.Generic;
using Neighbor.Main.Features.Interaction;
using Neighbor.Main.Features.Neighbor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Neighbor.Main.Features.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerDeathController : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField, Min(0.1f)] private float fallDuration = 0.38f;
        [SerializeField, Min(0f)] private float groundHoldDuration = 0.48f;
        [SerializeField, Min(0.05f)] private float fadeOutDuration = 0.45f;

        [Header("Camera Fall")]
        [SerializeField, Min(0f)] private float fallenCameraHeight = 0.14f;
        [SerializeField] private float fallRollDegrees = 62f;
        [SerializeField] private float fallPitchDegrees = 32f;
        [SerializeField, Min(1f)] private float deathFieldOfView = 52f;
        [SerializeField, Min(0f)] private float deathShakeAmount = 0.055f;
        [SerializeField, Min(0f)] private float deathShakeFrequency = 28f;

        [Header("Floor Impact")]
        [SerializeField, Min(0.05f)] private float impactDuration = 0.2f;
        [SerializeField, Min(0f)] private float impactDrop = 0.11f;
        [SerializeField] private float impactPitchKick = 14f;
        [SerializeField] private float impactRollKick = 9f;
        [SerializeField, Min(0f)] private float impactShakeAmount = 0.065f;
        [SerializeField, Min(0f)] private float impactFieldOfViewKick = 7f;
        [SerializeField, Range(0f, 1f)] private float impactBlackFlash = 0.18f;

        [Header("Reset")]
        [SerializeField, Min(0)] private int reinforcementBudgetPerDeath = 5;
        [SerializeField, Range(0, 12)] private int reinforcementLocationsPerDeath = 2;
        [SerializeField, Range(0, 12)] private int reinforcedDoorsPerDeath = 2;
        [SerializeField, Min(0f)] private float neighborRespawnSightGraceTime = 2.5f;

        [Header("Checkpoint")]
        [SerializeField] private bool loadSavedCheckpointOnAwake = true;
        [SerializeField] private bool persistCheckpoints = true;
        [SerializeField] private string checkpointSaveKey = "Neighbor.RespawnCheckpoint";

        [Header("Death UI")]
        [SerializeField] private string caughtMessage = "CAUGHT";
        [SerializeField] private string resetMessage = "RESETTING HOUSE";

        private readonly List<Behaviour> disabledBehaviours = new();
        private readonly List<Collider> disabledColliders = new();
        private PlayerController playerController;
        private CharacterController characterController;
        private PlayerCameraController cameraController;
        private Camera playerCamera;
        private Transform cameraTransform;
        private CanvasGroup fadeGroup;
        private Text deathText;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private Vector3 checkpointPosition;
        private Quaternion checkpointRotation;
        private string checkpointId;
        private bool hasCheckpoint;
        private Vector3 cameraRestLocalPosition;
        private Quaternion cameraRestLocalRotation;
        private float cameraRestFieldOfView;
        private bool initialized;

        public bool IsDead { get; private set; }
        public bool HasCheckpoint => hasCheckpoint;
        public string ActiveCheckpointId => checkpointId;
        public Vector3 CurrentRespawnPosition => hasCheckpoint ? checkpointPosition : spawnPosition;
        public Quaternion CurrentRespawnRotation => hasCheckpoint ? checkpointRotation : spawnRotation;

        private void Awake()
        {
            Initialize(GetComponent<PlayerController>());
        }

        public void Initialize(PlayerController controller)
        {
            if (initialized)
            {
                return;
            }

            playerController = controller != null ? controller : GetComponent<PlayerController>();
            characterController = GetComponent<CharacterController>();
            cameraController = GetComponentInChildren<PlayerCameraController>(true);
            playerCamera = GetComponentInChildren<Camera>(true);
            cameraTransform = playerCamera != null ? playerCamera.transform : null;

            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            if (cameraTransform != null)
            {
                cameraRestLocalPosition = cameraTransform.localPosition;
                cameraRestLocalRotation = cameraTransform.localRotation;
            }

            cameraRestFieldOfView = playerCamera != null ? playerCamera.fieldOfView : 60f;
            LoadSavedCheckpoint();
            initialized = true;
        }

        public static bool Kill(PlayerController player, Vector3 sourcePosition)
        {
            if (player == null)
            {
                return false;
            }

            PlayerDeathController deathController = player.GetComponent<PlayerDeathController>();
            if (deathController == null)
            {
                deathController = player.gameObject.AddComponent<PlayerDeathController>();
                deathController.Initialize(player);
            }

            return deathController.TryKill(sourcePosition);
        }

        public bool SetCheckpoint(Vector3 position, Quaternion rotation, string id = null, bool persist = true)
        {
            if (!IsFinite(position) || !IsFinite(rotation))
            {
                return false;
            }

            checkpointPosition = position;
            checkpointRotation = NormalizeRotation(rotation);
            checkpointId = string.IsNullOrWhiteSpace(id) ? "Checkpoint" : id.Trim();
            hasCheckpoint = true;

            if (persist && persistCheckpoints)
            {
                SaveCheckpoint();
            }

            PlayerFeedbackEvents.ReportCheckpoint(checkpointId);
            return true;
        }

        public void ClearCheckpoint(bool clearSaved = false)
        {
            hasCheckpoint = false;
            checkpointPosition = default;
            checkpointRotation = default;
            checkpointId = null;

            if (clearSaved)
            {
                ClearSavedCheckpoint();
            }
        }

        public bool TryKill(Vector3 sourcePosition)
        {
            if (IsDead)
            {
                return false;
            }

            StartCoroutine(DeathAndReset(sourcePosition));
            return true;
        }

        public void BeginCatchCameraFocus(Transform target)
        {
            if (target == null || IsDead)
            {
                return;
            }

            cameraController?.BeginCinematicLookAt(target);
        }

        private IEnumerator DeathAndReset(Vector3 sourcePosition)
        {
            IsDead = true;
            EnsureFadeOverlay();
            SetFade(0f);
            SetDeathMessage(caughtMessage);
            DisableGameplay();
            playerController?.PrepareForDeath();

            Vector3 startLocalPosition = cameraTransform != null ? cameraTransform.localPosition : Vector3.zero;
            Quaternion startLocalRotation = cameraTransform != null ? cameraTransform.localRotation : Quaternion.identity;
            float startFieldOfView = playerCamera != null ? playerCamera.fieldOfView : cameraRestFieldOfView;
            float fallSide = GetFallSide(sourcePosition);
            Vector3 fallenWorldPosition = cameraTransform != null ? cameraTransform.position : transform.position;
            fallenWorldPosition.y = transform.position.y + Mathf.Min(fallenCameraHeight, 0.14f);
            Vector3 fallenLocalPosition = cameraTransform != null && cameraTransform.parent != null
                ? cameraTransform.parent.InverseTransformPoint(fallenWorldPosition)
                : fallenWorldPosition;
            fallenLocalPosition.x += fallSide * 0.16f;
            float effectivePitch = Mathf.Max(fallPitchDegrees, 28f);
            float effectiveRoll = Mathf.Min(Mathf.Abs(fallRollDegrees), 62f);
            Quaternion fallenLocalRotation = startLocalRotation * Quaternion.Euler(effectivePitch, 0f, fallSide * effectiveRoll);

            float effectiveFallDuration = Mathf.Min(fallDuration, 0.38f);
            float timer = 0f;
            while (timer < effectiveFallDuration)
            {
                timer += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(timer / Mathf.Max(0.01f, effectiveFallDuration));
                float t = 1f - Mathf.Pow(1f - normalizedTime, 2.6f);
                ApplyDeathCamera(startLocalPosition, fallenLocalPosition, startLocalRotation, fallenLocalRotation, startFieldOfView, t);
                yield return null;
            }

            timer = 0f;
            while (timer < impactDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / Mathf.Max(0.01f, impactDuration));
                ApplyFloorImpact(fallenLocalPosition, fallenLocalRotation, t);
                SetFade(impactBlackFlash * (1f - t));
                yield return null;
            }

            ApplyFloorImpact(fallenLocalPosition, fallenLocalRotation, 1f);
            SetFade(0f);

            float effectiveGroundHoldDuration = Mathf.Min(groundHoldDuration, 0.48f);
            timer = 0f;
            while (timer < effectiveGroundHoldDuration)
            {
                timer += Time.deltaTime;
                SetDeathMessage(timer > effectiveGroundHoldDuration * 0.45f ? resetMessage : caughtMessage);
                float fadeStart = effectiveGroundHoldDuration * 0.2f;
                float fade = Mathf.InverseLerp(fadeStart, Mathf.Max(fadeStart + 0.01f, effectiveGroundHoldDuration), timer);
                SetFade(fade);
                yield return null;
            }

            ResetWorldStateForRespawn();
            AdaptiveSecurityPlan securityPlan = AdaptiveSecurityDirector.CompleteRun(
                reinforcementBudgetPerDeath,
                reinforcementLocationsPerDeath,
                reinforcedDoorsPerDeath);
            ReinforcementBudget reinforcementBudget = new ReinforcementBudget(securityPlan.Budget);
            Door.ApplyRunReinforcements(securityPlan.DoorCount, reinforcementBudget);
            GlassShatter.ApplyRunReinforcements(securityPlan.DoorCount, reinforcementBudget);
            ReinforcementTrigger.ApplyRunReinforcements(
                securityPlan.LocationCount,
                reinforcementBudget,
                securityPlan.CameraCount,
                securityPlan.TrapCount);
            ResetRun();

            timer = 0f;
            while (timer < fadeOutDuration)
            {
                timer += Time.deltaTime;
                SetFade(1f - Mathf.Clamp01(timer / Mathf.Max(0.01f, fadeOutDuration)));
                yield return null;
            }

            SetFade(0f);
            SetDeathMessage(null);
            IsDead = false;
        }

        private static void ResetWorldStateForRespawn()
        {
            Door.ResetAllToStartingState();
            GlassShatter.ResetAllToStartingState();
            Beartrap.ResetAllToStartingState();
            FakeFloorTrapDoor.ResetAllToStartingState();
            RaySawBladeTrap.ResetAllToStartingState();
            SpringLoadedBoxingGloveTrap.ResetAllToStartingState();
            SwingingAxeTrap.ResetAllToStartingState();
            SwingingAxeTripWire.ResetAllToStartingState();
            SecurityCamera.ResetAllToStartingState();
            Pickupable.ResetMissingPickupsToHome();
        }

        private void ApplyDeathCamera(
            Vector3 startPosition,
            Vector3 endPosition,
            Quaternion startRotation,
            Quaternion endRotation,
            float startFieldOfView,
            float t)
        {
            if (cameraTransform != null)
            {
                float shake = (1f - t) * deathShakeAmount;
                Vector3 shakeOffset = new Vector3(
                    Mathf.Sin(Time.time * deathShakeFrequency) * shake,
                    Mathf.Cos(Time.time * deathShakeFrequency * 0.83f) * shake,
                    0f);
                cameraTransform.localPosition = Vector3.LerpUnclamped(startPosition, endPosition, t) + shakeOffset;
                cameraTransform.localRotation = Quaternion.Slerp(startRotation, endRotation, t);
            }

            if (playerCamera != null)
            {
                playerCamera.fieldOfView = Mathf.Lerp(startFieldOfView, Mathf.Max(deathFieldOfView, 52f), t);
            }
        }

        private void ApplyFloorImpact(Vector3 groundPosition, Quaternion groundRotation, float t)
        {
            float decay = 1f - t;
            float kick = Mathf.Sin(t * Mathf.PI) * decay;
            float aftershock = Mathf.Sin(t * Mathf.PI * 4f) * decay;

            if (cameraTransform != null)
            {
                Vector3 shakeOffset = new Vector3(
                    Mathf.Sin(Time.time * deathShakeFrequency * 1.7f),
                    Mathf.Cos(Time.time * deathShakeFrequency * 1.31f),
                    0f) * (impactShakeAmount * decay);
                cameraTransform.localPosition = groundPosition + Vector3.down * (impactDrop * kick) + shakeOffset;
                cameraTransform.localRotation = groundRotation * Quaternion.Euler(
                    impactPitchKick * kick,
                    0f,
                    impactRollKick * aftershock);
            }

            if (playerCamera != null)
            {
                playerCamera.fieldOfView = Mathf.Max(deathFieldOfView, 52f) + impactFieldOfViewKick * kick;
            }
        }

        private float GetFallSide(Vector3 sourcePosition)
        {
            Vector3 toSource = sourcePosition - transform.position;
            toSource.y = 0f;
            if (toSource.sqrMagnitude <= 0.001f)
            {
                return Random.value < 0.5f ? -1f : 1f;
            }

            float side = Vector3.Dot(transform.right, toSource.normalized);
            return Mathf.Abs(side) > 0.05f ? Mathf.Sign(side) : Random.value < 0.5f ? -1f : 1f;
        }

        private void DisableGameplay()
        {
            disabledBehaviours.Clear();
            disabledColliders.Clear();
            DisableIfEnabled(cameraController);

            PlayerInteractor[] interactors = GetComponentsInChildren<PlayerInteractor>(true);
            for (int i = 0; i < interactors.Length; i++)
            {
                DisableIfEnabled(interactors[i]);
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider playerCollider = colliders[i];
                if (playerCollider == null || !playerCollider.enabled)
                {
                    continue;
                }

                playerCollider.enabled = false;
                disabledColliders.Add(playerCollider);
            }
        }

        private void DisableIfEnabled(Behaviour behaviour)
        {
            if (behaviour == null || !behaviour.enabled)
            {
                return;
            }

            behaviour.enabled = false;
            disabledBehaviours.Add(behaviour);
        }

        private void ResetRun()
        {
            ReleasePlayerFromWorldConstraints();
            playerController?.ResetForRespawn(CurrentRespawnPosition, CurrentRespawnRotation);

            if (cameraTransform != null)
            {
                cameraTransform.localPosition = cameraRestLocalPosition;
                cameraTransform.localRotation = cameraRestLocalRotation;
            }

            if (playerCamera != null)
            {
                playerCamera.fieldOfView = cameraRestFieldOfView;
            }

            cameraController?.SyncAfterRespawn();

            for (int i = 0; i < disabledColliders.Count; i++)
            {
                if (disabledColliders[i] != null)
                {
                    disabledColliders[i].enabled = true;
                }
            }

            disabledColliders.Clear();

            NeighborBrain[] neighbors = FindObjectsByType<NeighborBrain>(FindObjectsInactive.Exclude);
            for (int i = 0; i < neighbors.Length; i++)
            {
                neighbors[i]?.HandlePlayerRespawned(neighborRespawnSightGraceTime);
            }

            for (int i = 0; i < disabledBehaviours.Count; i++)
            {
                if (disabledBehaviours[i] != null)
                {
                    disabledBehaviours[i].enabled = true;
                }
            }

            disabledBehaviours.Clear();
        }

        private void LoadSavedCheckpoint()
        {
            if (!loadSavedCheckpointOnAwake || !persistCheckpoints)
            {
                return;
            }

            string prefix = GetCheckpointPreferencePrefix();
            if (!PlayerPrefs.HasKey(prefix + "Has"))
            {
                return;
            }

            Vector3 savedPosition = new(
                PlayerPrefs.GetFloat(prefix + "X", spawnPosition.x),
                PlayerPrefs.GetFloat(prefix + "Y", spawnPosition.y),
                PlayerPrefs.GetFloat(prefix + "Z", spawnPosition.z));
            Quaternion savedRotation = new(
                PlayerPrefs.GetFloat(prefix + "RotX", spawnRotation.x),
                PlayerPrefs.GetFloat(prefix + "RotY", spawnRotation.y),
                PlayerPrefs.GetFloat(prefix + "RotZ", spawnRotation.z),
                PlayerPrefs.GetFloat(prefix + "RotW", spawnRotation.w));

            if (!IsFinite(savedPosition) || !IsFinite(savedRotation))
            {
                ClearSavedCheckpoint();
                return;
            }

            checkpointPosition = savedPosition;
            checkpointRotation = NormalizeRotation(savedRotation);
            checkpointId = PlayerPrefs.GetString(prefix + "Id", "Checkpoint");
            hasCheckpoint = true;
        }

        private void SaveCheckpoint()
        {
            string prefix = GetCheckpointPreferencePrefix();
            PlayerPrefs.SetInt(prefix + "Has", 1);
            PlayerPrefs.SetFloat(prefix + "X", checkpointPosition.x);
            PlayerPrefs.SetFloat(prefix + "Y", checkpointPosition.y);
            PlayerPrefs.SetFloat(prefix + "Z", checkpointPosition.z);
            PlayerPrefs.SetFloat(prefix + "RotX", checkpointRotation.x);
            PlayerPrefs.SetFloat(prefix + "RotY", checkpointRotation.y);
            PlayerPrefs.SetFloat(prefix + "RotZ", checkpointRotation.z);
            PlayerPrefs.SetFloat(prefix + "RotW", checkpointRotation.w);
            PlayerPrefs.SetString(prefix + "Id", checkpointId ?? "Checkpoint");
            PlayerPrefs.Save();
        }

        private void ClearSavedCheckpoint()
        {
            string prefix = GetCheckpointPreferencePrefix();
            PlayerPrefs.DeleteKey(prefix + "Has");
            PlayerPrefs.DeleteKey(prefix + "X");
            PlayerPrefs.DeleteKey(prefix + "Y");
            PlayerPrefs.DeleteKey(prefix + "Z");
            PlayerPrefs.DeleteKey(prefix + "RotX");
            PlayerPrefs.DeleteKey(prefix + "RotY");
            PlayerPrefs.DeleteKey(prefix + "RotZ");
            PlayerPrefs.DeleteKey(prefix + "RotW");
            PlayerPrefs.DeleteKey(prefix + "Id");
            PlayerPrefs.Save();
        }

        private string GetCheckpointPreferencePrefix()
        {
            Scene scene = SceneManager.GetActiveScene();
            string sceneKey = !string.IsNullOrWhiteSpace(scene.path) ? scene.path : scene.name;
            if (string.IsNullOrWhiteSpace(sceneKey))
            {
                sceneKey = "UntitledScene";
            }

            return $"{checkpointSaveKey}.{sceneKey}.";
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.z)
                && IsFinite(value.w);
        }

        private static Quaternion NormalizeRotation(Quaternion rotation)
        {
            float magnitude = Mathf.Sqrt(
                rotation.x * rotation.x +
                rotation.y * rotation.y +
                rotation.z * rotation.z +
                rotation.w * rotation.w);
            return magnitude > 0.0001f ? new Quaternion(
                rotation.x / magnitude,
                rotation.y / magnitude,
                rotation.z / magnitude,
                rotation.w / magnitude) : Quaternion.identity;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void ReleasePlayerFromWorldConstraints()
        {
            PlayerHidingState hidingState = GetComponent<PlayerHidingState>();
            hidingState?.SetHidden(false);

            ClosetHideSpot[] hideSpots = FindObjectsByType<ClosetHideSpot>(FindObjectsInactive.Exclude);
            for (int i = 0; i < hideSpots.Length; i++)
            {
                hideSpots[i]?.ReleasePlayerForRespawn(playerController);
            }

            Beartrap[] beartraps = FindObjectsByType<Beartrap>(FindObjectsInactive.Exclude);
            for (int i = 0; i < beartraps.Length; i++)
            {
                beartraps[i]?.ReleasePlayerForRespawn(playerController);
            }
        }

        private void EnsureFadeOverlay()
        {
            if (fadeGroup != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject("Player Death Fade");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            fadeGroup = canvasObject.AddComponent<CanvasGroup>();

            GameObject imageObject = new GameObject("Fade");
            imageObject.transform.SetParent(canvasObject.transform, false);
            Image image = imageObject.AddComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;

            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            GameObject textObject = new GameObject("Death Message", typeof(RectTransform));
            textObject.transform.SetParent(canvasObject.transform, false);
            deathText = textObject.AddComponent<Text>();
            deathText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            deathText.fontSize = 34;
            deathText.fontStyle = FontStyle.Bold;
            deathText.alignment = TextAnchor.MiddleCenter;
            deathText.color = new Color(1f, 0.82f, 0.46f, 0.95f);
            deathText.raycastTarget = false;

            RectTransform textRect = deathText.rectTransform;
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = new Vector2(0f, -28f);
            textRect.sizeDelta = new Vector2(620f, 70f);
            SetDeathMessage(null);
        }

        private void SetFade(float alpha)
        {
            if (fadeGroup != null)
            {
                fadeGroup.alpha = Mathf.Clamp01(alpha);
            }
        }

        private void SetDeathMessage(string message)
        {
            if (deathText == null)
            {
                return;
            }

            bool hasMessage = !string.IsNullOrWhiteSpace(message);
            deathText.gameObject.SetActive(hasMessage);
            deathText.text = hasMessage ? message : string.Empty;
        }
    }
}
