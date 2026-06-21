using System.Collections.Generic;
using Neighbor.Main.Features.Neighbor;
using Neighbor.Main.Features.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class GlassShatter : MonoBehaviour
    {
        private static readonly List<GlassShatter> ActiveGlass = new();

        [Header("Shatter Trigger")]
        [SerializeField, Min(0f)] private float minimumImpactImpulse = 2.2f;
        [SerializeField, Min(0f)] private float minimumRelativeSpeed = 3.5f;

        [Header("Pieces")]
        [SerializeField] private GameObject intactVisualRoot;
        [SerializeField] private Collider intactCollider;
        [SerializeField] private Transform shardRoot;
        [SerializeField, Min(0f)] private float shardImpulse = 2.5f;
        [SerializeField, Min(0f)] private float shardTorque = 5f;
        [SerializeField, Min(0.05f)] private float shardLifetime = 12f;

        [Header("Noise")]
        [SerializeField, Min(0f)] private float hearingRadius = 13f;
        [SerializeField, Range(0f, 1f)] private float loudness = 0.75f;
        [SerializeField, Range(0f, 1f)] private float alertUrgency = 0.85f;
        [SerializeField, Min(0.02f)] private float noiseLifetime = 0.45f;

        [Header("Audio")]
        [SerializeField] private AudioClip[] shatterClips;
        [SerializeField, Range(0f, 1f)] private float shatterVolume = 0.8f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.12f;

        [Header("Adaptive Reinforcement")]
        [SerializeField] private bool trackPlayerBreakForReinforcement = true;
        [SerializeField, Min(0f)] private float playerBreakWeight = 1.25f;
        [SerializeField] private bool reinforcementCanBoardOpening = true;
        [SerializeField, Min(1)] private int reinforcementBoardCount = 2;
        [SerializeField] private ReinforcementPrefabOption[] blockerReinforcementOptions;
        [SerializeField, HideInInspector, FormerlySerializedAs("blockerReinforcementPrefabs")] private GameObject[] legacyBlockerReinforcementPrefabs;
        [SerializeField, Min(0f)] private float reinforcementBoardSurfaceOffset = 0.035f;
        [SerializeField, Min(0f)] private float reinforcementBoardVerticalSpacing = 0.38f;
        [SerializeField, Range(0f, 25f)] private float reinforcementBoardRollVariation = 7f;

        private readonly List<GameObject> spawnedReinforcements = new();
        private AudioClip generatedShatterClip;
        private bool isShattered;
        private Bounds openingBounds;
        private bool hasOpeningBounds;
        private float runReinforcementScore;
        private bool playerBrokeThisRun;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveGlass()
        {
            ActiveGlass.Clear();
        }

        private void Awake()
        {
            if (intactVisualRoot == null)
            {
                intactVisualRoot = gameObject;
            }

            if (intactCollider == null)
            {
                intactCollider = GetComponent<Collider>();
            }

            CaptureOpeningBounds();
            if (shardRoot != null)
            {
                shardRoot.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (!ActiveGlass.Contains(this))
            {
                ActiveGlass.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveGlass.Remove(this);
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryShatter(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            TryShatter(collision);
        }

        private void TryShatter(Collision collision)
        {
            if (isShattered || collision.contactCount == 0)
            {
                return;
            }

            float impulse = collision.impulse.magnitude;
            float relativeSpeed = collision.relativeVelocity.magnitude;
            if (impulse < minimumImpactImpulse && relativeSpeed < minimumRelativeSpeed)
            {
                return;
            }

            ContactPoint contact = collision.GetContact(0);
            GameObject instigator = ResolvePlayerCausedInstigator(collision, out bool causedByPlayer);
            Shatter(contact.point, collision.relativeVelocity, instigator, causedByPlayer);
        }

        public void ShatterFromNeighbor(Vector3 origin, Vector3 incomingVelocity, NeighborBrain instigator)
        {
            Shatter(origin, incomingVelocity, instigator != null ? instigator.gameObject : null, false);
        }

        public void ShatterFromPlayer(Vector3 origin, Vector3 incomingVelocity, PlayerController instigator)
        {
            Shatter(origin, incomingVelocity, instigator != null ? instigator.gameObject : null, true);
        }

        public static void ResetAllToStartingState()
        {
            for (int i = 0; i < ActiveGlass.Count; i++)
            {
                ActiveGlass[i]?.ResetToStartingState();
            }
        }

        public static void ApplyRunReinforcements(int maximumGlassOpenings, ReinforcementBudget budget)
        {
            List<GlassShatter> rankedGlass = new();
            for (int i = 0; i < ActiveGlass.Count; i++)
            {
                GlassShatter glass = ActiveGlass[i];
                if (glass != null && glass.runReinforcementScore > 0f && glass.CanApplyReinforcement(budget))
                {
                    rankedGlass.Add(glass);
                }
            }

            rankedGlass.Sort((a, b) => b.runReinforcementScore.CompareTo(a.runReinforcementScore));
            int maximumReinforcementCount = Mathf.Min(Mathf.Max(0, maximumGlassOpenings), rankedGlass.Count);
            int reinforcedCount = 0;
            for (int i = 0; i < rankedGlass.Count && reinforcedCount < maximumReinforcementCount; i++)
            {
                if (rankedGlass[i].ApplyReinforcement(budget))
                {
                    reinforcedCount++;
                }
            }

            for (int i = 0; i < ActiveGlass.Count; i++)
            {
                ActiveGlass[i]?.ClearRunTracking();
            }
        }

        private void Shatter(Vector3 origin, Vector3 incomingVelocity, GameObject instigator = null, bool causedByPlayer = false)
        {
            if (isShattered)
            {
                return;
            }

            isShattered = true;
            TrackPlayerBreak(causedByPlayer);
            NeighborEnvironmentalAwareness.Report(origin, 0.8f, instigator != null ? instigator : gameObject);

            if (intactVisualRoot != null)
            {
                foreach (Renderer intactRenderer in intactVisualRoot.GetComponentsInChildren<Renderer>())
                {
                    intactRenderer.enabled = false;
                }
            }

            if (intactCollider != null)
            {
                intactCollider.enabled = false;
            }

            ReleaseShards(origin, incomingVelocity);
            PlayShatterAudio(origin);
            SpawnNoiseEvent(origin, instigator);
        }

        private void TrackPlayerBreak(bool causedByPlayer)
        {
            if (!causedByPlayer || !trackPlayerBreakForReinforcement)
            {
                return;
            }

            runReinforcementScore += playerBreakWeight;
            playerBrokeThisRun = true;
        }

        private GameObject ResolvePlayerCausedInstigator(Collision collision, out bool causedByPlayer)
        {
            causedByPlayer = false;
            if (collision == null || collision.collider == null)
            {
                return null;
            }

            PlayerController player = collision.collider.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                causedByPlayer = true;
                return player.gameObject;
            }

            Pickupable pickup = collision.collider.GetComponentInParent<Pickupable>();
            if (pickup != null && pickup.IsRecentlyThrown)
            {
                causedByPlayer = true;
                return pickup.gameObject;
            }

            return null;
        }

        private bool CanApplyReinforcement(ReinforcementBudget budget)
        {
            return reinforcementCanBoardOpening
                && playerBrokeThisRun
                && TryGetBlockerReinforcement(budget, out _);
        }

        private bool ApplyReinforcement(ReinforcementBudget budget)
        {
            RestoreIntactGlass();
            return TrySpawnBlockerReinforcements(budget);
        }

        private bool TrySpawnBlockerReinforcements(ReinforcementBudget budget)
        {
            int desiredSpawnCount = Mathf.Max(1, reinforcementBoardCount);
            int placementStartIndex = GetSpawnedReinforcementCount();
            bool spawnedAny = false;
            for (int i = 0; i < desiredSpawnCount; i++)
            {
                if (!TryGetBlockerReinforcement(budget, out ReinforcementPrefabSelection selection))
                {
                    break;
                }

                GameObject reinforcement = Instantiate(selection.Prefab, transform.position, transform.rotation);
                DoorBlockerChair blocker = reinforcement.GetComponent<DoorBlockerChair>() ?? reinforcement.GetComponentInChildren<DoorBlockerChair>();
                if (blocker == null)
                {
                    Destroy(reinforcement);
                    break;
                }

                Pickupable pickupable = reinforcement.GetComponent<Pickupable>() ?? reinforcement.GetComponentInChildren<Pickupable>();
                int placementIndex = placementStartIndex + i;
                Quaternion rotation = GetBoardReinforcementRotation(placementIndex);
                Vector3 position = GetBoardReinforcementPosition(reinforcement.transform, pickupable, rotation, placementIndex);
                if (!blocker.TryBlockOpeningAsReinforcement(position, rotation))
                {
                    Destroy(reinforcement);
                    break;
                }

                spawnedReinforcements.Add(reinforcement);
                budget.TrySpend(selection.Cost);
                spawnedAny = true;
            }

            return spawnedAny;
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

        private Quaternion GetBoardReinforcementRotation(int placementIndex)
        {
            Vector3 surfaceNormal = GetSurfaceNormal();
            Quaternion facingOpening = Quaternion.LookRotation(-surfaceNormal, Vector3.up);
            return facingOpening * Quaternion.AngleAxis(GetBoardRoll(placementIndex), Vector3.forward);
        }

        private Vector3 GetBoardReinforcementPosition(
            Transform boardTransform,
            Pickupable pickupable,
            Quaternion rotation,
            int placementIndex)
        {
            Bounds boardBounds = GetBoardBounds(boardTransform, pickupable, rotation);
            Bounds glassBounds = GetOpeningBounds();
            Vector3 surfaceNormal = GetSurfaceNormal();

            float normalExtent =
                Mathf.Abs(surfaceNormal.x) * boardBounds.extents.x +
                Mathf.Abs(surfaceNormal.y) * boardBounds.extents.y +
                Mathf.Abs(surfaceNormal.z) * boardBounds.extents.z;

            float targetY = glassBounds.center.y
                + GetAlternatingPlacementOffset(placementIndex) * reinforcementBoardVerticalSpacing;
            float minY = glassBounds.min.y + boardBounds.extents.y + 0.02f;
            float maxY = glassBounds.max.y - boardBounds.extents.y - 0.02f;
            if (minY <= maxY)
            {
                targetY = Mathf.Clamp(targetY, minY, maxY);
            }

            return glassBounds.center
                + Vector3.up * (targetY - glassBounds.center.y)
                + surfaceNormal * (normalExtent + reinforcementBoardSurfaceOffset);
        }

        private Bounds GetBoardBounds(Transform boardTransform, Pickupable pickupable, Quaternion rotation)
        {
            if (boardTransform == null || pickupable == null)
            {
                return new Bounds(transform.position, Vector3.one * 0.25f);
            }

            Quaternion originalRotation = boardTransform.rotation;
            boardTransform.rotation = rotation;
            Bounds bounds = pickupable.GetPlacementBounds();
            boardTransform.rotation = originalRotation;
            return bounds;
        }

        private Bounds GetOpeningBounds()
        {
            if (!hasOpeningBounds)
            {
                CaptureOpeningBounds();
            }

            return hasOpeningBounds ? openingBounds : new Bounds(transform.position, new Vector3(0.1f, 1.5f, 2f));
        }

        private void CaptureOpeningBounds()
        {
            hasOpeningBounds = false;
            if (intactCollider != null)
            {
                openingBounds = intactCollider.bounds;
                hasOpeningBounds = true;
                return;
            }

            if (intactVisualRoot == null)
            {
                return;
            }

            Renderer[] renderers = intactVisualRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer intactRenderer = renderers[i];
                if (intactRenderer == null)
                {
                    continue;
                }

                if (!hasOpeningBounds)
                {
                    openingBounds = intactRenderer.bounds;
                    hasOpeningBounds = true;
                    continue;
                }

                openingBounds.Encapsulate(intactRenderer.bounds);
            }
        }

        private Vector3 GetSurfaceNormal()
        {
            Vector3 surfaceNormal = transform.right;
            return surfaceNormal.sqrMagnitude > 0.001f ? surfaceNormal.normalized : Vector3.forward;
        }

        private float GetBoardRoll(int placementIndex)
        {
            int safeIndex = Mathf.Max(0, placementIndex);
            float direction = safeIndex % 2 == 0 ? 1f : -1f;
            float magnitude = safeIndex / 2 + 1f;
            return direction * magnitude * reinforcementBoardRollVariation;
        }

        private static int GetAlternatingPlacementOffset(int placementIndex)
        {
            int safeIndex = Mathf.Max(0, placementIndex);
            if (safeIndex == 0)
            {
                return 0;
            }

            int row = (safeIndex + 1) / 2;
            return safeIndex % 2 == 0 ? row : -row;
        }

        private void ResetToStartingState()
        {
            RestoreIntactGlass();
            PruneSpawnedReinforcements();
        }

        private void RestoreIntactGlass()
        {
            isShattered = false;

            if (intactVisualRoot != null)
            {
                foreach (Renderer intactRenderer in intactVisualRoot.GetComponentsInChildren<Renderer>(true))
                {
                    intactRenderer.enabled = true;
                }
            }

            if (intactCollider != null)
            {
                intactCollider.enabled = true;
            }

            if (shardRoot != null)
            {
                shardRoot.gameObject.SetActive(false);
            }

            CaptureOpeningBounds();
        }

        private int GetSpawnedReinforcementCount()
        {
            PruneSpawnedReinforcements();
            return spawnedReinforcements.Count;
        }

        private void PruneSpawnedReinforcements()
        {
            for (int i = spawnedReinforcements.Count - 1; i >= 0; i--)
            {
                if (spawnedReinforcements[i] == null)
                {
                    spawnedReinforcements.RemoveAt(i);
                }
            }
        }

        private void ClearRunTracking()
        {
            runReinforcementScore = 0f;
            playerBrokeThisRun = false;
        }

        private void ReleaseShards(Vector3 origin, Vector3 incomingVelocity)
        {
            if (shardRoot == null)
            {
                return;
            }

            shardRoot.gameObject.SetActive(true);
            Vector3 inheritedVelocity = incomingVelocity.sqrMagnitude > 0.01f ? incomingVelocity * 0.25f : Vector3.zero;

            for (int i = shardRoot.childCount - 1; i >= 0; i--)
            {
                Transform shard = shardRoot.GetChild(i);
                shard.SetParent(null, true);
                shard.gameObject.SetActive(true);

                Rigidbody shardBody = shard.GetComponent<Rigidbody>();
                if (shardBody == null)
                {
                    continue;
                }

                shardBody.isKinematic = false;
                shardBody.useGravity = true;
                shardBody.linearVelocity = inheritedVelocity;

                Vector3 away = (shard.position - origin).sqrMagnitude > 0.0001f
                    ? (shard.position - origin).normalized
                    : Random.onUnitSphere;
                shardBody.AddForce((away + Vector3.up * 0.35f) * shardImpulse, ForceMode.Impulse);
                shardBody.AddTorque(Random.onUnitSphere * shardTorque, ForceMode.Impulse);

                Destroy(shard.gameObject, shardLifetime);
            }

            Destroy(shardRoot.gameObject);
        }

        private void PlayShatterAudio(Vector3 origin)
        {
            AudioClip clip = GetShatterClip();
            if (clip == null)
            {
                return;
            }

            GameObject audioObject = new GameObject("GlassShatter3DAudio");
            audioObject.transform.position = origin;

            AudioSource source = audioObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = shatterVolume;
            source.pitch = Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 0.4f;
            source.maxDistance = hearingRadius;
            source.dopplerLevel = 0.1f;
            source.Play();

            Destroy(audioObject, clip.length / Mathf.Max(0.01f, source.pitch) + 0.05f);
        }

        private AudioClip GetShatterClip()
        {
            if (shatterClips != null && shatterClips.Length > 0)
            {
                return shatterClips[Random.Range(0, shatterClips.Length)];
            }

            if (generatedShatterClip == null)
            {
                generatedShatterClip = CreateGeneratedShatterClip();
            }

            return generatedShatterClip;
        }

        private AudioClip CreateGeneratedShatterClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.42f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Exp(-time * 9f);
                float brightRing = Mathf.Sin(2f * Mathf.PI * 2100f * time) * Mathf.Exp(-time * 18f);
                float grit = Random.Range(-1f, 1f) * Mathf.Exp(-time * 14f);
                float tinkle = Mathf.Sin(2f * Mathf.PI * 3800f * time) * Mathf.Exp(-time * 28f);
                samples[i] = (brightRing * 0.35f + grit * 0.45f + tinkle * 0.2f) * envelope;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedGlassShatter", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void SpawnNoiseEvent(Vector3 origin, GameObject instigator)
        {
            if (hearingRadius <= 0f || loudness <= 0f)
            {
                return;
            }

            GameObject noiseObject = new GameObject("GlassShatterNoiseEvent");
            noiseObject.transform.position = origin;

            SphereCollider sphereCollider = noiseObject.AddComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
            sphereCollider.radius = hearingRadius;

            Rigidbody noiseBody = noiseObject.AddComponent<Rigidbody>();
            noiseBody.isKinematic = true;
            noiseBody.useGravity = false;

            NoiseEvent noiseEvent = noiseObject.AddComponent<NoiseEvent>();
            noiseEvent.Initialize(origin, hearingRadius, loudness, gameObject, noiseLifetime, alertUrgency, instigator);
        }

        private void OnValidate()
        {
            minimumImpactImpulse = Mathf.Max(0f, minimumImpactImpulse);
            minimumRelativeSpeed = Mathf.Max(0f, minimumRelativeSpeed);
            shardLifetime = Mathf.Max(0.05f, shardLifetime);
            hearingRadius = Mathf.Max(0f, hearingRadius);
            noiseLifetime = Mathf.Max(0.02f, noiseLifetime);
        }
    }
}
