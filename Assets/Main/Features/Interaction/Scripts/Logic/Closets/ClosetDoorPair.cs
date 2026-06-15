using System.Collections;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class ClosetDoorPair : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform leftDoor;
        [SerializeField] private Transform rightDoor;
        [SerializeField] private ClosetHideSpot hideSpot;
        [SerializeField] private Vector3 leftOpenEuler = new Vector3(0f, -85f, 0f);
        [SerializeField] private Vector3 rightOpenEuler = new Vector3(0f, 85f, 0f);
        [SerializeField, Min(0.01f)] private float moveDuration = 0.32f;
        [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private Coroutine moveRoutine;
        private Quaternion leftClosedRotation;
        private Quaternion rightClosedRotation;
        private bool isOpen;
        private ItemAudioFeedback audioFeedback;

        public bool IsOpen => isOpen;
        public ClosetHideSpot HideSpot => hideSpot;

        private void Awake()
        {
            hideSpot = hideSpot != null
                ? hideSpot
                : GetComponentInParent<ClosetHideSpot>() ?? GetComponentInChildren<ClosetHideSpot>(true);
            if (leftDoor != null)
            {
                leftClosedRotation = leftDoor.localRotation;
            }

            if (rightDoor != null)
            {
                rightClosedRotation = rightDoor.localRotation;
            }

            audioFeedback = ItemAudioFeedback.Resolve(gameObject);
        }

        public bool CanInteract(PlayerInteractor interactor)
        {
            return hideSpot != null ? hideSpot.CanInteract(interactor) : leftDoor != null || rightDoor != null;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (hideSpot != null)
            {
                hideSpot.Interact(interactor);
                return;
            }

            Toggle();
        }

        public void Toggle()
        {
            SetOpen(!isOpen);
        }

        public void SetOpen(bool open)
        {
            if (isOpen == open)
            {
                return;
            }

            isOpen = open;
            audioFeedback?.Play(ItemSoundProfile.HingedWood, 0.55f);
            if (moveRoutine != null)
            {
                StopCoroutine(moveRoutine);
            }

            moveRoutine = StartCoroutine(AnimateDoors(open));
        }

        private IEnumerator AnimateDoors(bool open)
        {
            Quaternion leftFrom = leftDoor != null ? leftDoor.localRotation : Quaternion.identity;
            Quaternion rightFrom = rightDoor != null ? rightDoor.localRotation : Quaternion.identity;
            Quaternion leftTo = open ? leftClosedRotation * Quaternion.Euler(leftOpenEuler) : leftClosedRotation;
            Quaternion rightTo = open ? rightClosedRotation * Quaternion.Euler(rightOpenEuler) : rightClosedRotation;

            float timer = 0f;
            float duration = Mathf.Max(0.01f, moveDuration);
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = movementCurve != null && movementCurve.length > 0
                    ? movementCurve.Evaluate(Mathf.Clamp01(timer / duration))
                    : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / duration));

                if (leftDoor != null)
                {
                    leftDoor.localRotation = Quaternion.SlerpUnclamped(leftFrom, leftTo, t);
                }

                if (rightDoor != null)
                {
                    rightDoor.localRotation = Quaternion.SlerpUnclamped(rightFrom, rightTo, t);
                }

                yield return null;
            }

            if (leftDoor != null)
            {
                leftDoor.localRotation = leftTo;
            }

            if (rightDoor != null)
            {
                rightDoor.localRotation = rightTo;
            }

            moveRoutine = null;
        }
    }
}
