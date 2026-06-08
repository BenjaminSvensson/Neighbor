using System.Collections;
using Neighbor.Main.Features.Neighbor;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class WindowBlinds : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform movingPart;
        [SerializeField] private Vector3 openLocalOffset = new Vector3(0f, 1.15f, 0f);
        [SerializeField] private Vector3 openLocalScale = new Vector3(1f, 0.22f, 1f);
        [SerializeField, Min(0.01f)] private float moveDuration = 0.4f;
        [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private bool startsOpen;

        private Coroutine moveRoutine;
        private Vector3 closedLocalPosition;
        private Vector3 closedLocalScale;
        private bool isOpen;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            if (movingPart == null)
            {
                movingPart = transform;
            }

            closedLocalPosition = movingPart.localPosition;
            closedLocalScale = movingPart.localScale;
            SetImmediate(startsOpen);
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
            NeighborEnvironmentalAwareness.Report(transform.position, 0.2f, gameObject);
            Toggle();
        }

        public void Toggle()
        {
            SetOpen(!isOpen);
        }

        public void SetOpen(bool open)
        {
            if (movingPart == null)
            {
                return;
            }

            isOpen = open;
            Vector3 targetPosition = GetTargetPosition(open);
            Vector3 targetScale = GetTargetScale(open);

            if (moveRoutine != null)
            {
                StopCoroutine(moveRoutine);
            }

            moveRoutine = StartCoroutine(AnimateBlinds(
                movingPart.localPosition,
                targetPosition,
                movingPart.localScale,
                targetScale));
        }

        private void SetImmediate(bool open)
        {
            isOpen = open;
            if (movingPart == null)
            {
                return;
            }

            movingPart.localPosition = GetTargetPosition(open);
            movingPart.localScale = GetTargetScale(open);
        }

        private IEnumerator AnimateBlinds(Vector3 positionFrom, Vector3 positionTo, Vector3 scaleFrom, Vector3 scaleTo)
        {
            float timer = 0f;
            float duration = Mathf.Max(0.01f, moveDuration);

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float progress = Mathf.Clamp01(timer / duration);
                float easedProgress = EvaluateCurve(progress);

                movingPart.localPosition = Vector3.LerpUnclamped(positionFrom, positionTo, easedProgress);
                movingPart.localScale = Vector3.LerpUnclamped(scaleFrom, scaleTo, easedProgress);
                yield return null;
            }

            movingPart.localPosition = positionTo;
            movingPart.localScale = scaleTo;
            moveRoutine = null;
        }

        private Vector3 GetTargetPosition(bool open)
        {
            return open ? closedLocalPosition + openLocalOffset : closedLocalPosition;
        }

        private Vector3 GetTargetScale(bool open)
        {
            return open ? Vector3.Scale(closedLocalScale, openLocalScale) : closedLocalScale;
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
            openLocalScale.x = Mathf.Max(0.01f, openLocalScale.x);
            openLocalScale.y = Mathf.Max(0.01f, openLocalScale.y);
            openLocalScale.z = Mathf.Max(0.01f, openLocalScale.z);
        }
    }
}
