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
        private Collider[] ownColliders;
        private Collider[] ignoredPlayerColliders;
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
        public Vector3 DefaultOpeningSideNormal => -transform.forward * Mathf.Sign(openAngle == 0f ? 1f : openAngle);

        private void Awake()
        {
            if (hinge == null)
            {
                hinge = transform;
            }

            ownColliders = GetComponentsInChildren<Collider>();
            closedPosition = hinge.localPosition;
            closedRotation = hinge.localRotation;
            isLocked = startsLocked;
            ResolveAudioSource();
        }

        private void OnDisable()
        {
            RestoreIgnoredPlayerCollisions();
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
                PlayLockedNudge();
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
                    PlayLockedNudge();
                    return;
                }
            }

            Toggle(interactor);
        }

        public bool TryOpenFor(Transform _)
        {
            if (IsBlocked)
            {
                PlayLockedNudge();
                return false;
            }

            if (isLocked)
            {
                PlayLockedNudge();
                return false;
            }

            Open();
            return true;
        }

        public void PlayImpactNudge()
        {
            float returnAngle = IsLocked || IsBlocked ? 0f : currentAngle;
            PlayNudge(returnAngle);
        }

        public void Unlock()
        {
            SetLocked(false, false, true);
        }

        public void Lock()
        {
            SetLocked(true, true, true);
        }

        public void SetLocked(bool locked, bool closeWhenLocked = true, bool playSound = true)
        {
            if (isLocked == locked)
            {
                return;
            }

            isLocked = locked;
            if (isLocked)
            {
                if (closeWhenLocked)
                {
                    Close();
                }

                return;
            }

            if (playSound)
            {
                PlayRandomSound(unlockClips);
            }
        }

        public bool IsOnDefaultOpeningSide(Vector3 worldPosition)
        {
            return Vector3.Dot(DefaultOpeningSideNormal, worldPosition - transform.position) > 0f;
        }

        public bool TryAddBlocker(DoorBlockerChair blocker, Vector3 blockerPosition, bool playFailureFeedback = true)
        {
            if (blocker == null || activeBlocker != null && activeBlocker != blocker)
            {
                return false;
            }

            if (!IsOnDefaultOpeningSide(blockerPosition))
            {
                if (playFailureFeedback)
                {
                    PlayLockedNudge();
                }

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

        public void Toggle(PlayerInteractor interactor = null)
        {
            if (isOpen)
            {
                Close();
                return;
            }

            Open(interactor);
        }

        public void Open(PlayerInteractor interactor = null)
        {
            isOpen = true;
            closeAtTime = Time.time + autoCloseDelay;
            PlayRandomSound(openClips);
            AnimateTo(openAngle, openCloseDuration, interactor != null ? interactor.GetComponentsInParent<Collider>() : null);
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

        private void PlayLockedNudge()
        {
            PlayNudge(0f);
        }

        private void PlayNudge(float returnAngle)
        {
            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
                RestoreIgnoredPlayerCollisions();
            }

            PlayRandomSound(lockedClips);
            animationRoutine = StartCoroutine(Nudge(returnAngle));
        }

        private void AnimateTo(float targetAngle, float duration, Collider[] playerColliders = null)
        {
            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
                RestoreIgnoredPlayerCollisions();
            }

            animationRoutine = StartCoroutine(Animate(currentAngle, targetAngle, duration, playerColliders));
        }

        private IEnumerator Animate(float fromAngle, float toAngle, float duration, Collider[] playerColliders)
        {
            IgnorePlayerCollisions(playerColliders);

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
            RestoreIgnoredPlayerCollisions();
            animationRoutine = null;
        }

        private IEnumerator Nudge(float returnAngle)
        {
            float targetAngle = returnAngle + lockedNudgeAngle;
            isOpen = !Mathf.Approximately(returnAngle, 0f);
            yield return AnimateNudgeStep(currentAngle, targetAngle, lockedNudgeDuration);
            yield return AnimateNudgeStep(targetAngle, returnAngle, lockedNudgeDuration);
            SetAngle(returnAngle);
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

        private void IgnorePlayerCollisions(Collider[] playerColliders)
        {
            if (ownColliders == null || playerColliders == null || playerColliders.Length == 0)
            {
                return;
            }

            ignoredPlayerColliders = playerColliders;
            SetPlayerCollisionIgnored(true);
        }

        private void RestoreIgnoredPlayerCollisions()
        {
            if (ignoredPlayerColliders == null)
            {
                return;
            }

            SetPlayerCollisionIgnored(false);
            ignoredPlayerColliders = null;
        }

        private void SetPlayerCollisionIgnored(bool ignore)
        {
            if (ownColliders == null || ignoredPlayerColliders == null)
            {
                return;
            }

            foreach (Collider ownCollider in ownColliders)
            {
                if (ownCollider == null)
                {
                    continue;
                }

                foreach (Collider playerCollider in ignoredPlayerColliders)
                {
                    if (playerCollider == null || ownCollider == playerCollider)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(ownCollider, playerCollider, ignore);
                }
            }
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
