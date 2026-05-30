using System.Collections;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class SlidingCupboardCompartment : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform movingPart;
        [SerializeField] private Vector3 openLocalOffset = new Vector3(0f, 0f, -0.75f);
        [SerializeField, Min(0.01f)] private float moveDuration = 0.35f;
        [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private Coroutine moveRoutine;
        private Vector3 closedLocalPosition;
        private Vector3 targetLocalPosition;
        private bool isOpen;

        private void Awake()
        {
            if (movingPart == null)
            {
                movingPart = transform;
            }

            closedLocalPosition = movingPart.localPosition;
            targetLocalPosition = closedLocalPosition;
        }

        private void OnDisable()
        {
            if (moveRoutine != null)
            {
                StopCoroutine(moveRoutine);
                moveRoutine = null;
            }
        }

        public bool CanInteract(PlayerInteractor interactor)
        {
            return movingPart != null;
        }

        public void Interact(PlayerInteractor interactor)
        {
            Toggle();
        }

        public void Toggle()
        {
            if (movingPart == null)
            {
                return;
            }

            isOpen = !isOpen;
            Vector3 destination = isOpen
                ? closedLocalPosition + openLocalOffset
                : closedLocalPosition;

            AnimateTo(destination);
        }

        private void AnimateTo(Vector3 destination)
        {
            if (moveRoutine != null)
            {
                StopCoroutine(moveRoutine);
            }

            moveRoutine = StartCoroutine(Move(movingPart.localPosition, destination));
        }

        private IEnumerator Move(Vector3 from, Vector3 to)
        {
            targetLocalPosition = to;

            float timer = 0f;
            float duration = Mathf.Max(0.01f, moveDuration);
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float progress = Mathf.Clamp01(timer / duration);
                float easedProgress = EvaluateCurve(progress);
                movingPart.localPosition = Vector3.LerpUnclamped(from, targetLocalPosition, easedProgress);
                yield return null;
            }

            movingPart.localPosition = targetLocalPosition;
            moveRoutine = null;
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
            moveDuration = Mathf.Max(0.01f, moveDuration);
        }
    }
}
