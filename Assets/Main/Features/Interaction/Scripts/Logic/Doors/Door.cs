using System.Collections;
using System.Collections.Generic;
using Neighbor.Main.Features.Neighbor;
using Neighbor.Main.Features.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class Door : MonoBehaviour, IInteractable
    {
        private static readonly List<Door> ActiveDoors = new();

        [Header("Door")]
        [SerializeField] private Transform hinge;
        [SerializeField] private Vector3 pivotOffset = new Vector3(-0.6f, 0f, 0f);
        [SerializeField, Min(0f)] private float openAngle = 95f;
        [SerializeField, Min(0.01f)] private float openCloseDuration = 0.28f;
        [SerializeField, Min(0f)] private float autoCloseDelay = 0f;
        [SerializeField] private bool startsOpen;

        [Header("Lock")]
        [SerializeField] private bool startsLocked = true;
        [SerializeField] private string requiredKeyId = "test_key";
        [SerializeField] private bool neighborCanUnlock = true;
        [SerializeField, Min(0f)] private float lockedNudgeAngle = 6f;
        [SerializeField, Min(0.01f)] private float lockedNudgeDuration = 0.12f;

        [Header("Neighbor Kick")]
        [SerializeField] private bool neighborCanKickBlockedDoor = true;
        [SerializeField, Min(0f)] private float blockerKickImpulse = 3.5f;
        [SerializeField, Min(0f)] private float blockerKickUpwardImpulse = 0.45f;

        [Header("Navigation")]
        [SerializeField] private bool blockNeighborNavigationWhenUnavailable = true;
        [SerializeField, Min(0f)] private float navigationObstaclePadding = 0.08f;

        [Header("Adaptive Reinforcement")]
        [SerializeField] private bool trackPlayerUseForReinforcement = true;
        [SerializeField, Min(0f)] private float playerOpenWeight = 0.5f;
        [SerializeField, Min(0f)] private float playerPassageWeight = 1f;
        [SerializeField] private bool reinforcementCanLockDoor;
        [SerializeField, Min(1)] private int reinforcementLockCost = 1;
        [SerializeField] private bool reinforcementCanBlockDoor = true;
        [SerializeField] private ReinforcementPrefabOption[] blockerReinforcementOptions;
        [SerializeField, HideInInspector, FormerlySerializedAs("blockerReinforcementPrefabs")] private GameObject[] legacyBlockerReinforcementPrefabs;
        [SerializeField] private Transform blockerReinforcementSpawnPoint;

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
        private NavMeshObstacle[] navigationObstacles;
        private PlayerController trackedOpeningPlayer;
        private float trackedPlayerOpeningSide;
        private float runReinforcementScore;
        private bool playerPassedThroughThisRun;
        private GameObject spawnedReinforcement;

        public bool IsLocked => isLocked;
        public bool IsBlocked => activeBlocker != null;
        public bool IsOpen => isOpen;
        public bool NeighborCanUnlock => neighborCanUnlock;
        public bool NeighborCanKickBlockedDoor => neighborCanKickBlockedDoor;
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
            isOpen = startsOpen;
            SetAngle(startsOpen ? openAngle : 0f);
            closeAtTime = startsOpen && autoCloseDelay > 0f ? Time.time + autoCloseDelay : 0f;
            ResolveAudioSource();
            ResolveNavigationObstacles();
            UpdateNavigationObstacles();
        }

        private void OnEnable()
        {
            if (!ActiveDoors.Contains(this))
            {
                ActiveDoors.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveDoors.Remove(this);
            RestoreIgnoredPlayerCollisions();
        }

        private void Update()
        {
            if (isOpen && autoCloseDelay > 0f && Time.time >= closeAtTime)
            {
                Close();
            }

            TrackPlayerPassage();
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

            TrackPlayerOpening(interactor);
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

        public bool TryOpenForNeighbor(Transform neighbor)
        {
            if (IsBlocked)
            {
                PlayLockedNudge();
                return false;
            }

            if (isLocked)
            {
                if (!neighborCanUnlock)
                {
                    PlayLockedNudge();
                    return false;
                }

                Unlock();
            }

            OpenIgnoring(neighbor);
            return true;
        }

        public bool TryKickOpenForNeighbor(Transform neighbor)
        {
            if (!IsBlocked || !neighborCanKickBlockedDoor)
            {
                PlayLockedNudge();
                return false;
            }

            DoorBlockerChair blocker = activeBlocker;
            Vector3 kickDirection = GetNeighborKickDirection(neighbor, blocker);
            if (blocker != null)
            {
                blocker.HandleKickedLoose(kickDirection, blockerKickImpulse, blockerKickUpwardImpulse);
            }

            SetLocked(false, false, false);
            OpenIgnoring(neighbor);
            return true;
        }

        public void PlayNeighborKickFeedback()
        {
            PlayNudge(0f);
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
            UpdateNavigationObstacles();
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
            UpdateNavigationObstacles();
            return true;
        }

        public void RemoveBlocker(DoorBlockerChair blocker)
        {
            if (activeBlocker == blocker)
            {
                activeBlocker = null;
                UpdateNavigationObstacles();
            }
        }

        public static void ResetAllToStartingState()
        {
            for (int i = 0; i < ActiveDoors.Count; i++)
            {
                ActiveDoors[i]?.ResetToStartingState();
            }
        }

        public static void ApplyRunReinforcements(int maximumDoors, ReinforcementBudget budget)
        {
            List<Door> rankedDoors = new();
            for (int i = 0; i < ActiveDoors.Count; i++)
            {
                Door door = ActiveDoors[i];
                if (door != null && door.runReinforcementScore > 0f && door.CanApplyReinforcement(budget))
                {
                    rankedDoors.Add(door);
                }
            }

            rankedDoors.Sort((a, b) => b.runReinforcementScore.CompareTo(a.runReinforcementScore));
            int maximumReinforcementCount = Mathf.Min(Mathf.Max(0, maximumDoors), rankedDoors.Count);
            int reinforcedCount = 0;
            for (int i = 0; i < rankedDoors.Count && reinforcedCount < maximumReinforcementCount; i++)
            {
                if (rankedDoors[i].ApplyReinforcement(budget))
                {
                    reinforcedCount++;
                }
            }

            for (int i = 0; i < ActiveDoors.Count; i++)
            {
                ActiveDoors[i]?.ClearRunTracking();
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
            UpdateNavigationObstacles();
            PlayRandomSound(openClips);
            AnimateTo(openAngle, openCloseDuration, interactor != null ? interactor.GetComponentsInParent<Collider>() : null);
        }

        private void OpenIgnoring(Transform opener)
        {
            isOpen = true;
            closeAtTime = Time.time + autoCloseDelay;
            UpdateNavigationObstacles();
            PlayRandomSound(openClips);
            AnimateTo(openAngle, openCloseDuration, opener != null ? opener.GetComponentsInParent<Collider>() : null);
        }

        public void Close()
        {
            bool shouldPlayCloseSound = isOpen || !Mathf.Approximately(currentAngle, 0f);
            isOpen = false;
            UpdateNavigationObstacles();
            if (shouldPlayCloseSound)
            {
                PlayRandomSound(closeClips);
            }

            AnimateTo(0f, openCloseDuration);
        }

        private void TrackPlayerOpening(PlayerInteractor interactor)
        {
            if (!trackPlayerUseForReinforcement || isOpen || interactor == null)
            {
                return;
            }

            PlayerController player = interactor.GetComponentInParent<PlayerController>();
            if (player == null)
            {
                return;
            }

            runReinforcementScore += playerOpenWeight;
            trackedOpeningPlayer = player;
            trackedPlayerOpeningSide = GetSideOfDoor(player.transform.position);
        }

        private void TrackPlayerPassage()
        {
            if (!trackPlayerUseForReinforcement || trackedOpeningPlayer == null || playerPassedThroughThisRun)
            {
                return;
            }

            float currentSide = GetSideOfDoor(trackedOpeningPlayer.transform.position);
            if (trackedPlayerOpeningSide == 0f || currentSide == 0f || Mathf.Sign(currentSide) == Mathf.Sign(trackedPlayerOpeningSide))
            {
                return;
            }

            playerPassedThroughThisRun = true;
            runReinforcementScore += playerPassageWeight;
            trackedOpeningPlayer = null;
        }

        private float GetSideOfDoor(Vector3 position)
        {
            return Vector3.Dot(transform.forward, position - transform.position);
        }

        private bool CanApplyReinforcement(ReinforcementBudget budget)
        {
            bool canLock = reinforcementCanLockDoor && playerPassedThroughThisRun && budget.CanAfford(reinforcementLockCost);
            bool canBlock = reinforcementCanBlockDoor && TryGetBlockerReinforcement(budget, out _);
            return canLock || canBlock;
        }

        private bool ApplyReinforcement(ReinforcementBudget budget)
        {
            bool reinforced = false;
            if (reinforcementCanLockDoor && playerPassedThroughThisRun && budget.TrySpend(reinforcementLockCost))
            {
                SetLocked(true, true, false);
                reinforced = true;
            }

            if (reinforcementCanBlockDoor && TrySpawnBlockerReinforcement(budget))
            {
                reinforced = true;
            }

            UpdateNavigationObstacles();
            return reinforced;
        }

        private bool TrySpawnBlockerReinforcement(ReinforcementBudget budget)
        {
            if (!TryGetBlockerReinforcement(budget, out ReinforcementPrefabSelection selection))
            {
                return false;
            }

            Transform spawnAnchor = blockerReinforcementSpawnPoint != null ? blockerReinforcementSpawnPoint : transform;
            GameObject reinforcement = Instantiate(selection.Prefab, spawnAnchor.position, spawnAnchor.rotation);
            DoorBlockerChair blocker = reinforcement.GetComponent<DoorBlockerChair>() ?? reinforcement.GetComponentInChildren<DoorBlockerChair>();
            if (blocker == null || !blocker.TryBlockDoorAsReinforcement(this))
            {
                Destroy(reinforcement);
                return false;
            }

            spawnedReinforcement = reinforcement;
            budget.TrySpend(selection.Cost);
            return true;
        }

        private bool TryGetBlockerReinforcement(ReinforcementBudget budget, out ReinforcementPrefabSelection selection)
        {
            selection = default;
            if (budget == null)
            {
                return false;
            }

            if (blockerReinforcementOptions != null && blockerReinforcementOptions.Length > 0)
            {
                int startIndex = Random.Range(0, blockerReinforcementOptions.Length);
                for (int i = 0; i < blockerReinforcementOptions.Length; i++)
                {
                    ReinforcementPrefabOption option = blockerReinforcementOptions[(startIndex + i) % blockerReinforcementOptions.Length];
                    if (option != null && option.Prefab != null && budget.CanAfford(option.Cost))
                    {
                        selection = new ReinforcementPrefabSelection(option.Prefab, option.Cost);
                        return true;
                    }
                }
            }

            if (legacyBlockerReinforcementPrefabs != null && legacyBlockerReinforcementPrefabs.Length > 0 && budget.CanAfford(1))
            {
                int startIndex = Random.Range(0, legacyBlockerReinforcementPrefabs.Length);
                for (int i = 0; i < legacyBlockerReinforcementPrefabs.Length; i++)
                {
                    GameObject prefab = legacyBlockerReinforcementPrefabs[(startIndex + i) % legacyBlockerReinforcementPrefabs.Length];
                    if (prefab != null)
                    {
                        selection = new ReinforcementPrefabSelection(prefab, 1);
                        return true;
                    }
                }
            }

            return false;
        }

        private void ResetToStartingState()
        {
            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
                animationRoutine = null;
            }

            RestoreIgnoredPlayerCollisions();
            activeBlocker?.ReleaseForDoorReset();

            if (spawnedReinforcement != null)
            {
                Destroy(spawnedReinforcement);
                spawnedReinforcement = null;
            }

            isLocked = startsLocked;
            isOpen = startsOpen;
            SetAngle(startsOpen ? openAngle : 0f);
            closeAtTime = startsOpen && autoCloseDelay > 0f ? Time.time + autoCloseDelay : 0f;
            trackedOpeningPlayer = null;
            UpdateNavigationObstacles();
        }

        private void ClearRunTracking()
        {
            runReinforcementScore = 0f;
            playerPassedThroughThisRun = false;
            trackedOpeningPlayer = null;
        }

        private void PlayLockedNudge()
        {
            PlayNudge(0f);
        }

        private Vector3 GetNeighborKickDirection(Transform neighbor, DoorBlockerChair blocker)
        {
            Vector3 direction = neighbor != null ? neighbor.forward : DefaultOpeningSideNormal;
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.001f && blocker != null)
            {
                direction = blocker.transform.position - transform.position;
                direction.y = 0f;
            }

            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = DefaultOpeningSideNormal;
                direction.y = 0f;
            }

            return direction.normalized;
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

        private void ResolveNavigationObstacles()
        {
            if (!blockNeighborNavigationWhenUnavailable || ownColliders == null)
            {
                navigationObstacles = new NavMeshObstacle[0];
                return;
            }

            List<NavMeshObstacle> obstacles = new List<NavMeshObstacle>();
            foreach (Collider ownCollider in ownColliders)
            {
                if (ownCollider == null || ownCollider.isTrigger)
                {
                    continue;
                }

                BoxCollider boxCollider = ownCollider as BoxCollider;
                if (boxCollider == null)
                {
                    continue;
                }

                NavMeshObstacle obstacle = boxCollider.GetComponent<NavMeshObstacle>();
                if (obstacle == null)
                {
                    obstacle = boxCollider.gameObject.AddComponent<NavMeshObstacle>();
                }

                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.center = boxCollider.center;
                obstacle.size = boxCollider.size + Vector3.one * navigationObstaclePadding;
                obstacle.carving = true;
                obstacle.carveOnlyStationary = false;
                obstacle.carvingMoveThreshold = 0.05f;
                obstacle.carvingTimeToStationary = 0.05f;
                obstacles.Add(obstacle);
            }

            navigationObstacles = obstacles.ToArray();
        }

        private void UpdateNavigationObstacles()
        {
            if (navigationObstacles == null)
            {
                return;
            }

            bool shouldBlock = blockNeighborNavigationWhenUnavailable
                && !isOpen
                && (IsBlocked || isLocked && !neighborCanUnlock);

            foreach (NavMeshObstacle obstacle in navigationObstacles)
            {
                if (obstacle != null)
                {
                    obstacle.enabled = shouldBlock;
                }
            }
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
