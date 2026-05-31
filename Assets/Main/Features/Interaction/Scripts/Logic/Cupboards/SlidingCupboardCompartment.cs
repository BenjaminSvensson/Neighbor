using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class SlidingCupboardCompartment : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform movingPart;
        [SerializeField] private Vector3 openLocalOffset = new Vector3(0f, 0f, -0.75f);
        [SerializeField, Min(0.01f)] private float moveDuration = 0.35f;
        [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private bool carrySupportedRigidbodies = true;
        [SerializeField, Min(0f)] private float carryBoundsPadding = 0.08f;
        [SerializeField, Min(0f)] private float carryBoundsUpPadding = 0.35f;
        [SerializeField] private LayerMask carryMask = ~0;

        private Coroutine moveRoutine;
        private Vector3 closedLocalPosition;
        private Vector3 targetLocalPosition;
        private bool isOpen;
        private readonly Collider[] carryHits = new Collider[32];
        private readonly List<Rigidbody> carriedBodies = new List<Rigidbody>(8);

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
                MovePartAndCarryBodies(Vector3.LerpUnclamped(from, targetLocalPosition, easedProgress));
                yield return null;
            }

            MovePartAndCarryBodies(targetLocalPosition);
            moveRoutine = null;
        }

        private void MovePartAndCarryBodies(Vector3 nextLocalPosition)
        {
            if (movingPart == null)
            {
                return;
            }

            Vector3 previousWorldPosition = movingPart.position;
            if (carrySupportedRigidbodies)
            {
                CollectCarriedBodies();
            }

            movingPart.localPosition = nextLocalPosition;
            Vector3 worldDelta = movingPart.position - previousWorldPosition;
            if (!carrySupportedRigidbodies || worldDelta.sqrMagnitude <= 0.0000001f)
            {
                return;
            }

            for (int i = 0; i < carriedBodies.Count; i++)
            {
                Rigidbody carriedBody = carriedBodies[i];
                if (carriedBody == null || carriedBody.isKinematic)
                {
                    continue;
                }

                Pickupable pickupable = carriedBody.GetComponentInParent<Pickupable>();
                if (pickupable != null && pickupable.IsHeld)
                {
                    continue;
                }

                carriedBody.position += worldDelta;
                carriedBody.WakeUp();
            }
        }

        private void CollectCarriedBodies()
        {
            carriedBodies.Clear();
            if (movingPart == null || !TryGetMovingPartBounds(out Bounds bounds))
            {
                return;
            }

            bounds.Expand(Vector3.one * carryBoundsPadding * 2f);
            bounds.Expand(new Vector3(0f, carryBoundsUpPadding * 2f, 0f));
            bounds.center += Vector3.up * carryBoundsUpPadding;

            int hitCount = Physics.OverlapBoxNonAlloc(
                bounds.center,
                bounds.extents,
                carryHits,
                Quaternion.identity,
                carryMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = carryHits[i];
                if (hit == null || hit.transform.IsChildOf(movingPart))
                {
                    continue;
                }

                Rigidbody body = hit.attachedRigidbody;
                if (body == null || body.transform.IsChildOf(movingPart) || carriedBodies.Contains(body))
                {
                    continue;
                }

                carriedBodies.Add(body);
            }
        }

        private bool TryGetMovingPartBounds(out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            Collider[] colliders = movingPart.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider childCollider = colliders[i];
                if (childCollider == null || !childCollider.enabled || childCollider.isTrigger)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = childCollider.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(childCollider.bounds);
            }

            if (hasBounds)
            {
                return true;
            }

            Renderer[] renderers = movingPart.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer childRenderer = renderers[i];
                if (childRenderer == null || !childRenderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = childRenderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(childRenderer.bounds);
            }

            return hasBounds;
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
            carryBoundsPadding = Mathf.Max(0f, carryBoundsPadding);
            carryBoundsUpPadding = Mathf.Max(0f, carryBoundsUpPadding);
        }
    }
}
