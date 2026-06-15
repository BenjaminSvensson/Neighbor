using System.Collections;
using System.Collections.Generic;
using Neighbor.Main.Features.Interaction;
using Neighbor.Main.Features.Neighbor;
using UnityEngine;
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

        private readonly List<Behaviour> disabledBehaviours = new();
        private readonly List<Collider> disabledColliders = new();
        private PlayerController playerController;
        private CharacterController characterController;
        private PlayerCameraController cameraController;
        private Camera playerCamera;
        private Transform cameraTransform;
        private CanvasGroup fadeGroup;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private Vector3 cameraRestLocalPosition;
        private Quaternion cameraRestLocalRotation;
        private float cameraRestFieldOfView;
        private Coroutine catchCameraFocusRoutine;
        private bool initialized;

        public bool IsDead { get; private set; }

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

        public bool TryKill(Vector3 sourcePosition)
        {
            if (IsDead)
            {
                return false;
            }

            StartCoroutine(DeathAndReset(sourcePosition));
            return true;
        }

        public void ScheduleCatchCameraFocus(Transform target, float delay)
        {
            if (target == null || IsDead)
            {
                return;
            }

            if (catchCameraFocusRoutine != null)
            {
                StopCoroutine(catchCameraFocusRoutine);
            }

            catchCameraFocusRoutine = StartCoroutine(BeginCatchCameraFocusAfterDelay(target, delay));
        }

        private IEnumerator BeginCatchCameraFocusAfterDelay(Transform target, float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            cameraController?.BeginCinematicLookAt(target);
            catchCameraFocusRoutine = null;
        }

        private IEnumerator DeathAndReset(Vector3 sourcePosition)
        {
            IsDead = true;
            if (catchCameraFocusRoutine != null)
            {
                StopCoroutine(catchCameraFocusRoutine);
                catchCameraFocusRoutine = null;
            }

            EnsureFadeOverlay();
            SetFade(0f);
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
                float fadeStart = effectiveGroundHoldDuration * 0.2f;
                float fade = Mathf.InverseLerp(fadeStart, Mathf.Max(fadeStart + 0.01f, effectiveGroundHoldDuration), timer);
                SetFade(fade);
                yield return null;
            }

            Door.ResetAllToStartingState();
            AdaptiveSecurityPlan securityPlan = AdaptiveSecurityDirector.CompleteRun(
                reinforcementBudgetPerDeath,
                reinforcementLocationsPerDeath,
                reinforcedDoorsPerDeath);
            ReinforcementBudget reinforcementBudget = new ReinforcementBudget(securityPlan.Budget);
            Door.ApplyRunReinforcements(securityPlan.DoorCount, reinforcementBudget);
            ReinforcementTrigger.ApplyRunReinforcements(securityPlan.LocationCount, reinforcementBudget);
            ResetRun();

            timer = 0f;
            while (timer < fadeOutDuration)
            {
                timer += Time.deltaTime;
                SetFade(1f - Mathf.Clamp01(timer / Mathf.Max(0.01f, fadeOutDuration)));
                yield return null;
            }

            SetFade(0f);
            IsDead = false;
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
            playerController?.ResetForRespawn(spawnPosition, spawnRotation);

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
        }

        private void SetFade(float alpha)
        {
            if (fadeGroup != null)
            {
                fadeGroup.alpha = Mathf.Clamp01(alpha);
            }
        }
    }
}
