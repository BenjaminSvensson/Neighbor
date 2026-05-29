using System.Collections;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class Door : MonoBehaviour, IInteractable
    {
        [Header("Door")]
        [SerializeField] private Transform hinge;
        [SerializeField] private Vector3 pivotOffset = new Vector3(-0.6f, 0f, 0f);
        [SerializeField, Min(0f)] private float openAngle = 95f;
        [SerializeField, Min(0.01f)] private float openCloseDuration = 0.28f;
        [SerializeField, Min(0f)] private float autoCloseDelay = 0f;

        [Header("Lock")]
        [SerializeField] private bool startsLocked = true;
        [SerializeField] private string requiredKeyId = "test_key";
        [SerializeField, Min(0f)] private float lockedNudgeAngle = 6f;
        [SerializeField, Min(0.01f)] private float lockedNudgeDuration = 0.12f;

        [Header("Door Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] openClips;
        [SerializeField] private AudioClip[] closeClips;
        [SerializeField] private AudioClip[] lockedClips;
        [SerializeField] private AudioClip[] unlockClips;
        [SerializeField, Range(0f, 1f)] private float audioVolume = 0.65f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.04f;
        [SerializeField, Min(0f)] private float audioMinDistance = 0.5f;
        [SerializeField, Min(0.1f)] private float audioMaxDistance = 10f;

        private Coroutine animationRoutine;
        private Vector3 closedPosition;
        private Quaternion closedRotation;
        private bool isOpen;
        private bool isLocked;
        private float currentAngle;
        private float closeAtTime;
        private DoorBlockerChair activeBlocker;

        public bool IsLocked => isLocked;
        public bool IsBlocked => activeBlocker != null;
        public bool IsOpen => isOpen;
        public string RequiredKeyId => requiredKeyId;
        public Vector3 DefaultOpeningSideNormal => transform.right * Mathf.Sign(openAngle == 0f ? 1f : openAngle);

        private void Awake()
        {
            if (hinge == null)
            {
                hinge = transform;
            }

            closedPosition = hinge.localPosition;
            closedRotation = hinge.localRotation;
            isLocked = startsLocked;
            ResolveAudioSource();
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
            if (IsBlocked)
            {
                return;
            }

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
            if (IsBlocked)
            {
                return false;
            }

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
            if (!isLocked)
            {
                return;
            }

            isLocked = false;
            PlayRandomSound(unlockClips);
        }

        public void Lock()
        {
            isLocked = true;
            Close();
        }

        public bool IsOnDefaultOpeningSide(Vector3 worldPosition)
        {
            return Vector3.Dot(DefaultOpeningSideNormal, worldPosition - transform.position) > 0f;
        }

        public bool TryAddBlocker(DoorBlockerChair blocker, Vector3 playerPosition)
        {
            if (blocker == null || activeBlocker != null && activeBlocker != blocker)
            {
                return false;
            }

            if (!IsOnDefaultOpeningSide(playerPosition))
            {
                PlayLockedNudge(null);
                return false;
            }

            activeBlocker = blocker;
            Close();
            return true;
        }

        public void RemoveBlocker(DoorBlockerChair blocker)
        {
            if (activeBlocker == blocker)
            {
                activeBlocker = null;
            }
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
            PlayRandomSound(openClips);
            AnimateTo(openAngle * direction, openCloseDuration);
        }

        public void Close()
        {
            bool shouldPlayCloseSound = isOpen || !Mathf.Approximately(currentAngle, 0f);
            isOpen = false;
            if (shouldPlayCloseSound)
            {
                PlayRandomSound(closeClips);
            }

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

            PlayRandomSound(lockedClips);
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
            Quaternion angleRotation = Quaternion.Euler(0f, currentAngle, 0f);
            hinge.localPosition = closedPosition + closedRotation * (pivotOffset - angleRotation * pivotOffset);
            hinge.localRotation = closedRotation * angleRotation;
        }

        private void ResolveAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            ConfigureAudioSource();
        }

        private void PlayRandomSound(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0 || audioSource == null)
            {
                return;
            }

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null)
            {
                return;
            }

            audioSource.pitch = Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
            audioSource.PlayOneShot(clip, audioVolume);
        }

        private void ConfigureAudioSource()
        {
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = audioMinDistance;
            audioSource.maxDistance = audioMaxDistance;
            audioSource.dopplerLevel = 0.1f;
        }
    }
}
