using System.Collections;
using System.Collections.Generic;
using Neighbor.Main.Features.Neighbor;
using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class ClosetHideSpot : MonoBehaviour, IInteractable
    {
        private static readonly List<ClosetHideSpot> ActiveHideSpots = new();

        [SerializeField] private ClosetDoorPair doors;
        [SerializeField] private Transform hidePoint;
        [SerializeField] private Transform exitPoint;
        [SerializeField, Min(0.05f)] private float transitionDuration = 0.42f;
        [SerializeField, Min(0f)] private float doorLeadTime = 0.16f;
        [SerializeField, Min(0f)] private float doorCloseDelay = 0.08f;
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Closet Peeking")]
        [SerializeField] private bool allowSidePeek = true;
        [SerializeField, Min(0f)] private float maximumSidePeek = 0.32f;
        [SerializeField, Min(0f)] private float sidePeekSpeed = 0.36f;

        private PlayerController hiddenPlayer;
        private PlayerHidingState hiddenState;
        private Coroutine transitionRoutine;
        private bool isTransitioning;
        private float sidePeekOffset;

        public bool HasHiddenPlayer => hiddenPlayer != null;
        public bool IsTransitioning => isTransitioning;
        public Vector3 SearchPosition => exitPoint != null ? exitPoint.position : transform.position;
        public static IReadOnlyList<ClosetHideSpot> HideSpots => ActiveHideSpots;

        private void OnEnable()
        {
            doors = doors != null
                ? doors
                : GetComponentInParent<ClosetDoorPair>() ?? GetComponentInChildren<ClosetDoorPair>(true);
            if (!ActiveHideSpots.Contains(this))
            {
                ActiveHideSpots.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveHideSpots.Remove(this);
            CancelTransition();
            if (hiddenPlayer != null)
            {
                hiddenPlayer.ResumeAfterHiding();
                hiddenState?.SetHidden(false);
                hiddenPlayer = null;
                hiddenState = null;
            }
        }

        private void Update()
        {
            UpdateSidePeek();
        }

        public bool CanInteract(PlayerInteractor interactor)
        {
            if (isTransitioning)
            {
                return false;
            }

            if (HasHiddenPlayer)
            {
                return IsHiddenPlayer(interactor);
            }

            return interactor != null && interactor.HeldPickup == null;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (HasHiddenPlayer)
            {
                if (IsHiddenPlayer(interactor))
                {
                    BeginExit();
                }

                return;
            }

            BeginHide(interactor);
        }

        private void BeginHide(PlayerInteractor interactor)
        {
            PlayerController player = interactor != null ? interactor.GetComponentInParent<PlayerController>() : null;
            if (player == null)
            {
                return;
            }

            hiddenPlayer = player;
            sidePeekOffset = 0f;
            NotifyNeighborsPlayerEntering(player);
            hiddenState = player.GetComponent<PlayerHidingState>();
            if (hiddenState == null)
            {
                hiddenState = player.gameObject.AddComponent<PlayerHidingState>();
            }

            transitionRoutine = StartCoroutine(HideTransition());
        }

        private void BeginExit()
        {
            if (hiddenPlayer == null)
            {
                return;
            }

            transitionRoutine = StartCoroutine(ExitTransition());
        }

        private IEnumerator HideTransition()
        {
            isTransitioning = true;
            doors?.SetOpen(true);
            yield return WaitForSeconds(doorLeadTime);

            hiddenPlayer.PrepareForHiding();

            Transform target = hidePoint != null ? hidePoint : transform;
            yield return MovePlayer(hiddenPlayer.transform, target);
            hiddenState?.SetHidden(true);
            NotifyNeighborsPlayerFinishedHiding(hiddenPlayer);
            yield return WaitForSeconds(doorCloseDelay);
            doors?.SetOpen(false);
            isTransitioning = false;
            transitionRoutine = null;
        }

        private IEnumerator ExitTransition()
        {
            isTransitioning = true;
            hiddenState?.SetHidden(false);
            doors?.SetOpen(true);
            yield return WaitForSeconds(doorLeadTime);

            Transform target = exitPoint != null ? exitPoint : transform;
            yield return MovePlayer(hiddenPlayer.transform, target);
            hiddenPlayer.ResumeAfterHiding();
            hiddenPlayer = null;
            hiddenState = null;
            sidePeekOffset = 0f;

            yield return WaitForSeconds(doorCloseDelay);
            doors?.SetOpen(false);
            isTransitioning = false;
            transitionRoutine = null;
        }

        public void ReleasePlayerForRespawn(PlayerController player)
        {
            if (player == null || hiddenPlayer != player)
            {
                return;
            }

            CancelTransition();
            hiddenPlayer.ResumeAfterHiding();
            hiddenState?.SetHidden(false);
            doors?.SetOpen(false);
            hiddenPlayer = null;
            hiddenState = null;
            sidePeekOffset = 0f;
        }

        public PlayerController SearchByNeighbor(NeighborBrain neighbor, out bool caughtPlayer)
        {
            caughtPlayer = false;
            doors?.SetOpen(true);
            PlayerController foundPlayer = hiddenPlayer;
            if (foundPlayer != null)
            {
                CancelTransition();
                caughtPlayer = PlayerDeathController.Kill(
                    foundPlayer,
                    neighbor != null ? neighbor.transform.position : transform.position);

                if (!caughtPlayer)
                {
                    hiddenPlayer.PrepareForHiding();
                    transitionRoutine = StartCoroutine(ExitTransition());
                }
            }

            return foundPlayer;
        }

        private bool IsHiddenPlayer(PlayerInteractor interactor)
        {
            return interactor != null
                && hiddenPlayer != null
                && interactor.GetComponentInParent<PlayerController>() == hiddenPlayer;
        }

        private void NotifyNeighborsPlayerEntering(PlayerController player)
        {
            NeighborBrain[] neighbors = FindObjectsByType<NeighborBrain>();
            for (int i = 0; i < neighbors.Length; i++)
            {
                neighbors[i]?.ObservePlayerEnteringHideSpot(this, player);
            }
        }

        private void NotifyNeighborsPlayerFinishedHiding(PlayerController player)
        {
            NeighborBrain[] neighbors = FindObjectsByType<NeighborBrain>();
            for (int i = 0; i < neighbors.Length; i++)
            {
                neighbors[i]?.HandlePlayerFinishedHiding(this, player);
            }
        }

        private void UpdateSidePeek()
        {
            if (!allowSidePeek
                || doors == null
                || hiddenPlayer == null
                || hiddenState == null
                || !hiddenState.IsHidden
                || isTransitioning)
            {
                return;
            }

            Transform center = hidePoint != null ? hidePoint : transform;
            float input = PlayerInputReader.ReadFrameInput().Move.x;
            sidePeekOffset = Mathf.Clamp(
                sidePeekOffset + Mathf.Clamp(input, -1f, 1f) * sidePeekSpeed * Time.deltaTime,
                -maximumSidePeek,
                maximumSidePeek);
            hiddenPlayer.transform.SetPositionAndRotation(
                center.position + center.right * sidePeekOffset,
                center.rotation);
        }

        private IEnumerator MovePlayer(Transform playerTransform, Transform target)
        {
            if (playerTransform == null || target == null)
            {
                yield break;
            }

            Vector3 startPosition = playerTransform.position;
            Quaternion startRotation = playerTransform.rotation;
            float timer = 0f;
            float duration = Mathf.Max(0.05f, transitionDuration);
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float progress = Mathf.Clamp01(timer / duration);
                float eased = transitionCurve != null && transitionCurve.length > 0
                    ? transitionCurve.Evaluate(progress)
                    : Mathf.SmoothStep(0f, 1f, progress);
                playerTransform.SetPositionAndRotation(
                    Vector3.LerpUnclamped(startPosition, target.position, eased),
                    Quaternion.SlerpUnclamped(startRotation, target.rotation, eased));
                yield return null;
            }

            playerTransform.SetPositionAndRotation(target.position, target.rotation);
        }

        private static IEnumerator WaitForSeconds(float duration)
        {
            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                yield return null;
            }
        }

        private void CancelTransition()
        {
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }

            isTransitioning = false;
            sidePeekOffset = 0f;
        }

        private void OnValidate()
        {
            transitionDuration = Mathf.Max(0.05f, transitionDuration);
            doorLeadTime = Mathf.Max(0f, doorLeadTime);
            doorCloseDelay = Mathf.Max(0f, doorCloseDelay);
            maximumSidePeek = Mathf.Max(0f, maximumSidePeek);
            sidePeekSpeed = Mathf.Max(0f, sidePeekSpeed);
        }
    }
}
