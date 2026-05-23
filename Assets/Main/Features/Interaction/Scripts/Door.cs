using System.Collections;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class Door : MonoBehaviour, IInteractable
    {
        [Header("Door")]
        [SerializeField] private Transform hinge;
        [SerializeField, Min(0f)] private float openAngle = 95f;
        [SerializeField, Min(0.01f)] private float openCloseDuration = 0.28f;
        [SerializeField, Min(0f)] private float autoCloseDelay = 0f;

        [Header("Lock")]
        [SerializeField] private bool startsLocked = true;
        [SerializeField] private string requiredKeyId = "test_key";
        [SerializeField, Min(0f)] private float lockedNudgeAngle = 6f;
        [SerializeField, Min(0.01f)] private float lockedNudgeDuration = 0.12f;

        private Coroutine animationRoutine;
        private Quaternion closedRotation;
        private bool isOpen;
        private bool isLocked;
        private float currentAngle;
        private float closeAtTime;

        public bool IsLocked => isLocked;
        public bool IsOpen => isOpen;
        public string RequiredKeyId => requiredKeyId;

        private void Awake()
        {
            if (hinge == null)
            {
                hinge = transform;
            }

            closedRotation = hinge.localRotation;
            isLocked = startsLocked;
        }

        private void Update()
        {
            if (isOpen && autoCloseDelay > 0f && Time.time >= closeAtTime)
            {
                Close();
            }
        }

        public bool CanInteract(PlayerInteractor interactor)
        {
            return true;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (isLocked)
            {
                DoorKey heldKey = interactor != null && interactor.HeldPickup != null
                    ? interactor.HeldPickup.GetComponentInChildren<DoorKey>()
                    : null;

                if (heldKey != null && heldKey.Opens(this))
                {
                    Unlock();
                }
                else
                {
                    PlayLockedNudge(interactor != null ? interactor.transform : null);
                    return;
                }
            }

            Toggle(interactor != null ? interactor.transform : null);
        }

        public bool TryOpenFor(Transform opener)
        {
            if (isLocked)
            {
                PlayLockedNudge(opener);
                return false;
            }

            OpenAwayFrom(opener);
            return true;
        }

        public void Unlock()
        {
            isLocked = false;
        }

        public void Lock()
        {
            isLocked = true;
            Close();
        }

        public void Toggle(Transform opener)
        {
            if (isOpen)
            {
                Close();
                return;
            }

            OpenAwayFrom(opener);
        }

        public void OpenAwayFrom(Transform opener)
        {
            float direction = GetOpenDirectionAwayFrom(opener);
            isOpen = true;
            closeAtTime = Time.time + autoCloseDelay;
            AnimateTo(openAngle * direction, openCloseDuration);
        }

        public void Close()
        {
            isOpen = false;
            AnimateTo(0f, openCloseDuration);
        }

        private float GetOpenDirectionAwayFrom(Transform opener)
        {
            if (opener == null)
            {
                return 1f;
            }

            Vector3 toOpener = opener.position - transform.position;
            float side = Vector3.Dot(transform.right, toOpener);
            return side >= 0f ? -1f : 1f;
        }

        private void PlayLockedNudge(Transform opener)
        {
            float direction = GetOpenDirectionAwayFrom(opener);
            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
            }

            animationRoutine = StartCoroutine(Nudge(direction));
        }

        private void AnimateTo(float targetAngle, float duration)
        {
            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
            }

            animationRoutine = StartCoroutine(Animate(currentAngle, targetAngle, duration));
        }

        private IEnumerator Animate(float fromAngle, float toAngle, float duration)
        {
            float timer = 0f;
            float moveDuration = Mathf.Max(0.01f, duration);
            while (timer < moveDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / moveDuration));
                SetAngle(Mathf.Lerp(fromAngle, toAngle, t));
                yield return null;
            }

            SetAngle(toAngle);
            animationRoutine = null;
        }

        private IEnumerator Nudge(float direction)
        {
            float startAngle = currentAngle;
            float targetAngle = startAngle + lockedNudgeAngle * direction;
            yield return AnimateNudgeStep(startAngle, targetAngle, lockedNudgeDuration);
            yield return AnimateNudgeStep(targetAngle, startAngle, lockedNudgeDuration);
            animationRoutine = null;
        }

        private IEnumerator AnimateNudgeStep(float fromAngle, float toAngle, float duration)
        {
            float timer = 0f;
            float moveDuration = Mathf.Max(0.01f, duration);
            while (timer < moveDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / moveDuration));
                SetAngle(Mathf.Lerp(fromAngle, toAngle, t));
                yield return null;
            }

            SetAngle(toAngle);
        }

        private void SetAngle(float angle)
        {
            currentAngle = angle;
            hinge.localRotation = closedRotation * Quaternion.Euler(0f, currentAngle, 0f);
        }
    }
}
