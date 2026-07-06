using System.Collections;
using System.Collections.Generic;
using Neighbor.Main.Features.Neighbor;
using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class FakeFloorTrapDoor : MonoBehaviour
    {
        private static readonly List<FakeFloorTrapDoor> ActiveTrapDoors = new();

        [SerializeField] private Transform leftPanel;
        [SerializeField] private Transform rightPanel;
        [SerializeField] private Collider[] blockingColliders;
        [SerializeField] private bool triggerOnlyForPlayer = true;
        [SerializeField, Min(0.01f)] private float openDuration = 0.32f;
        [SerializeField] private Vector3 leftOpenEuler = new(0f, 0f, -82f);
        [SerializeField] private Vector3 rightOpenEuler = new(0f, 0f, 82f);
        [SerializeField] private Vector3 leftOpenOffset = new(-0.35f, -0.65f, 0f);
        [SerializeField] private Vector3 rightOpenOffset = new(0.35f, -0.65f, 0f);
        [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private Coroutine openRoutine;
        private Vector3 leftClosedPosition;
        private Vector3 rightClosedPosition;
        private Quaternion leftClosedRotation;
        private Quaternion rightClosedRotation;
        private bool isOpen;
        private ItemAudioFeedback audioFeedback;

        public bool IsOpen => isOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveTrapDoors()
        {
            ActiveTrapDoors.Clear();
        }

        private void Awake()
        {
            CacheClosedPose();
            audioFeedback = ItemAudioFeedback.Resolve(gameObject);
        }

        private void OnEnable()
        {
            if (!ActiveTrapDoors.Contains(this))
            {
                ActiveTrapDoors.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveTrapDoors.Remove(this);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryOpen(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryOpen(other);
        }

        private void TryOpen(Collider other)
        {
            if (isOpen || other == null)
            {
                return;
            }

            if (triggerOnlyForPlayer && other.GetComponentInParent<PlayerController>() == null)
            {
                if (other.GetComponentInParent<NeighborImpactReceiver>() == null)
                {
                    return;
                }
            }

            Open();
        }

        public void Open()
        {
            if (isOpen)
            {
                return;
            }

            isOpen = true;
            audioFeedback?.Play(ItemSoundProfile.TrapDoorOpen, 0.78f);
            SetBlockingCollidersEnabled(false);

            if (openRoutine != null)
            {
                StopCoroutine(openRoutine);
            }

            openRoutine = StartCoroutine(AnimateOpen());
        }

        public static void ResetAllToStartingState()
        {
            for (int i = 0; i < ActiveTrapDoors.Count; i++)
            {
                ActiveTrapDoors[i]?.ResetToStartingState();
            }
        }

        private void ResetToStartingState()
        {
            if (openRoutine != null)
            {
                StopCoroutine(openRoutine);
                openRoutine = null;
            }

            isOpen = false;
            SetBlockingCollidersEnabled(true);
            ApplyPanelPose(leftPanel, leftClosedPosition, leftClosedPosition, leftClosedRotation, leftClosedRotation, 1f);
            ApplyPanelPose(rightPanel, rightClosedPosition, rightClosedPosition, rightClosedRotation, rightClosedRotation, 1f);
        }

        private IEnumerator AnimateOpen()
        {
            Vector3 leftStartPosition = leftPanel != null ? leftPanel.localPosition : Vector3.zero;
            Vector3 rightStartPosition = rightPanel != null ? rightPanel.localPosition : Vector3.zero;
            Quaternion leftStartRotation = leftPanel != null ? leftPanel.localRotation : Quaternion.identity;
            Quaternion rightStartRotation = rightPanel != null ? rightPanel.localRotation : Quaternion.identity;

            Vector3 leftEndPosition = leftClosedPosition + leftOpenOffset;
            Vector3 rightEndPosition = rightClosedPosition + rightOpenOffset;
            Quaternion leftEndRotation = leftClosedRotation * Quaternion.Euler(leftOpenEuler);
            Quaternion rightEndRotation = rightClosedRotation * Quaternion.Euler(rightOpenEuler);

            float timer = 0f;
            float duration = Mathf.Max(0.01f, openDuration);
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float progress = Mathf.Clamp01(timer / duration);
                float easedProgress = EvaluateCurve(progress);

                ApplyPanelPose(leftPanel, leftStartPosition, leftEndPosition, leftStartRotation, leftEndRotation, easedProgress);
                ApplyPanelPose(rightPanel, rightStartPosition, rightEndPosition, rightStartRotation, rightEndRotation, easedProgress);
                yield return null;
            }

            ApplyPanelPose(leftPanel, leftEndPosition, leftEndPosition, leftEndRotation, leftEndRotation, 1f);
            ApplyPanelPose(rightPanel, rightEndPosition, rightEndPosition, rightEndRotation, rightEndRotation, 1f);
            openRoutine = null;
        }

        private void ApplyPanelPose(Transform panel, Vector3 fromPosition, Vector3 toPosition, Quaternion fromRotation, Quaternion toRotation, float progress)
        {
            if (panel == null)
            {
                return;
            }

            panel.localPosition = Vector3.LerpUnclamped(fromPosition, toPosition, progress);
            panel.localRotation = Quaternion.SlerpUnclamped(fromRotation, toRotation, progress);
        }

        private void SetBlockingCollidersEnabled(bool enabled)
        {
            if (blockingColliders == null)
            {
                return;
            }

            for (int i = 0; i < blockingColliders.Length; i++)
            {
                Collider blockingCollider = blockingColliders[i];
                if (blockingCollider != null)
                {
                    blockingCollider.enabled = enabled;
                }
            }
        }

        private void CacheClosedPose()
        {
            if (leftPanel != null)
            {
                leftClosedPosition = leftPanel.localPosition;
                leftClosedRotation = leftPanel.localRotation;
            }

            if (rightPanel != null)
            {
                rightClosedPosition = rightPanel.localPosition;
                rightClosedRotation = rightPanel.localRotation;
            }
        }

        private float EvaluateCurve(float progress)
        {
            if (movementCurve == null || movementCurve.length == 0)
            {
                return Mathf.SmoothStep(0f, 1f, progress);
            }

            return movementCurve.Evaluate(progress);
        }

        private void OnValidate()
        {
            openDuration = Mathf.Max(0.01f, openDuration);
        }
    }
}
