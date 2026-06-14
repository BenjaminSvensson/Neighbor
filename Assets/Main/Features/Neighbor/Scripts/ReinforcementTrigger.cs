using System.Collections.Generic;
using Neighbor.Main.HouseBuilder;
using Neighbor.Main.Features.Interaction;
using Neighbor.Main.Features.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace Neighbor.Main.Features.Neighbor
{
    [System.Serializable]
    public sealed class ReinforcementPrefabOption
    {
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(1)] private int cost = 1;

        public GameObject Prefab => prefab;
        public int Cost => Mathf.Max(1, cost);
    }

    public sealed class ReinforcementBudget
    {
        public int Remaining { get; private set; }

        public ReinforcementBudget(int amount)
        {
            Remaining = Mathf.Max(0, amount);
        }

        public bool CanAfford(int cost)
        {
            return Remaining >= Mathf.Max(1, cost);
        }

        public bool TrySpend(int cost)
        {
            int normalizedCost = Mathf.Max(1, cost);
            if (Remaining < normalizedCost)
            {
                return false;
            }

            Remaining -= normalizedCost;
            return true;
        }
    }

    public readonly struct ReinforcementPrefabSelection
    {
        public GameObject Prefab { get; }
        public int Cost { get; }
        public Transform Anchor { get; }

        public ReinforcementPrefabSelection(GameObject prefab, int cost, Transform anchor = null)
        {
            Prefab = prefab;
            Cost = Mathf.Max(1, cost);
            Anchor = anchor;
        }
    }

    [AddComponentMenu("Neighbor/Reinforcement Trigger")]
    [RequireComponent(typeof(Collider))]
    public sealed class ReinforcementTrigger : MonoBehaviour
    {
        private static readonly List<ReinforcementTrigger> ActiveTriggers = new();

        [Header("Run Tracking")]
        [SerializeField, Min(0.01f)] private float visitWeight = 1f;

        [Header("Reinforcement")]
        [SerializeField] private ReinforcementPrefabOption[] reinforcementOptions;
        [SerializeField, HideInInspector, FormerlySerializedAs("reinforcementPrefabs")] private GameObject[] legacyReinforcementPrefabs;
        [SerializeField] private Transform spawnPoint;
        [SerializeField, Min(0)] private int maximumPersistentReinforcements = 1;
        [SerializeField] private bool randomizeYaw;
        [SerializeField, Min(0f)] private float repeatedLocationPenalty = 0.8f;

        [Header("Security Camera Placement")]
        [SerializeField] private GameObject securityCameraPrefab;
        [SerializeField, Min(1)] private int securityCameraCost = 2;
        [SerializeField, Range(0f, 1f)] private float securityCameraPlacementChance = 0.5f;
        [SerializeField] private bool attachSecurityCamerasToWalls = true;
        [SerializeField, Min(0.1f)] private float cameraWallSearchDistance = 4f;
        [SerializeField, Min(0f)] private float cameraMountHeight = 2.25f;
        [SerializeField, Range(0f, 1f)] private float cameraMaximumWallUpDot = 0.3f;
        [SerializeField] private LayerMask cameraWallMask = ~0;
        [SerializeField, Min(0f)] private float cameraMinimumSpacing = 2.5f;
        [SerializeField, Min(0f)] private float cameraCandidateOffset = 1.5f;
        [SerializeField, Min(0f)] private float cameraRecentPlacementPenaltyRadius = 4f;
        [SerializeField, Min(1)] private int cameraRecentPlacementMemory = 6;
        [SerializeField, Min(0f)] private float cameraPlacementScoreRandomness = 0.35f;

        [Header("Neighbor Avoidance")]
        [SerializeField] private bool addNeighborAvoidanceObstacle = true;
        [SerializeField, Min(0f)] private float avoidancePadding = 0.65f;
        [SerializeField, Min(0.05f)] private float minimumAvoidanceHeight = 1.2f;

        [Header("Editor Gizmo")]
        [SerializeField] private Color gizmoColor = new Color(1f, 0.16f, 0.08f, 0.55f);
        [SerializeField] private Color directionGizmoColor = new Color(1f, 0.85f, 0.08f, 0.95f);
        [SerializeField, Min(0.1f)] private float directionGizmoLength = 1.15f;
        [SerializeField, Min(0.01f)] private float directionGizmoHeadSize = 0.22f;

        private readonly HashSet<PlayerController> playersInside = new();
        private readonly List<CameraWallCandidate> cameraWallCandidates = new();
        private readonly List<Vector3> recentCameraPlacements = new();
        private float runScore;
        private int spawnedReinforcementCount;
        private float ReinforcementRankingScore => runScore / (1f + spawnedReinforcementCount * repeatedLocationPenalty);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveTriggers()
        {
            ActiveTriggers.Clear();
        }

        private void Awake()
        {
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
        }

        private void OnEnable()
        {
            if (!ActiveTriggers.Contains(this))
            {
                ActiveTriggers.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveTriggers.Remove(this);
            playersInside.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player != null && playersInside.Add(player))
            {
                runScore += visitWeight;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                playersInside.Remove(player);
            }
        }

        public static void ApplyRunReinforcements(int maximumLocations, ReinforcementBudget budget)
        {
            List<ReinforcementTrigger> rankedTriggers = new();
            for (int i = 0; i < ActiveTriggers.Count; i++)
            {
                ReinforcementTrigger trigger = ActiveTriggers[i];
                if (trigger != null && trigger.runScore > 0f && trigger.CanSpawnReinforcement(budget))
                {
                    rankedTriggers.Add(trigger);
                }
            }

            rankedTriggers.Sort((a, b) => b.ReinforcementRankingScore.CompareTo(a.ReinforcementRankingScore));
            int maximumSpawnCount = Mathf.Min(Mathf.Max(0, maximumLocations), rankedTriggers.Count);
            int spawnedCount = 0;
            bool cameraPlacedThisPass = false;
            for (int i = 0; i < rankedTriggers.Count && spawnedCount < maximumSpawnCount; i++)
            {
                int cameraCountBeforeSpawn = SecurityCamera.NeighborPlacedCameraCount;
                if (rankedTriggers[i].SpawnReinforcement(budget, !cameraPlacedThisPass))
                {
                    spawnedCount++;
                    cameraPlacedThisPass |= SecurityCamera.NeighborPlacedCameraCount > cameraCountBeforeSpawn;
                }
            }

            for (int i = 0; i < ActiveTriggers.Count; i++)
            {
                if (ActiveTriggers[i] != null)
                {
                    ActiveTriggers[i].runScore = 0f;
                    ActiveTriggers[i].playersInside.Clear();
                }
            }
        }

        public bool TryGetConfiguredBuilderReinforcement(ReinforcementBudget budget, out ReinforcementPrefabSelection selection)
        {
            return TryGetBuilderLocationSelection(budget, true, out selection, out _);
        }

        private bool CanSpawnReinforcement(ReinforcementBudget budget)
        {
            return HasConfiguredReinforcement()
                && TryGetRandomAffordableReinforcement(budget, out _)
                && (maximumPersistentReinforcements <= 0 || spawnedReinforcementCount < maximumPersistentReinforcements);
        }

        private bool SpawnReinforcement(ReinforcementBudget budget, bool allowSecurityCamera)
        {
            if (!TryGetRandomAffordableReinforcement(budget, out ReinforcementPrefabSelection selection, allowSecurityCamera))
            {
                return false;
            }

            Transform anchor = selection.Anchor != null ? selection.Anchor : spawnPoint != null ? spawnPoint : transform;
            Quaternion rotation = anchor.rotation;
            if (randomizeYaw)
            {
                rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }

            SecurityCamera cameraPrefab = selection.Prefab.GetComponentInChildren<SecurityCamera>(true);
            RaycastHit wallHit = default;
            if (cameraPrefab != null && (!SecurityCamera.CanPlaceNeighborCamera || !TryFindCameraWall(anchor, out wallHit)))
            {
                if (!TryGetRandomAffordableReinforcement(budget, out selection, false))
                {
                    return false;
                }

                cameraPrefab = null;
            }

            GameObject reinforcement = Instantiate(selection.Prefab, anchor.position, rotation);
            DoorBlockerChair[] doorBlockers = reinforcement.GetComponentsInChildren<DoorBlockerChair>(true);
            for (int i = 0; i < doorBlockers.Length; i++)
            {
                doorBlockers[i]?.MarkAsReinforcement();
            }

            if (cameraPrefab != null)
            {
                SecurityCamera spawnedCamera = reinforcement.GetComponentInChildren<SecurityCamera>(true);
                if (spawnedCamera == null || !spawnedCamera.TryAttachByNeighbor(wallHit.point, wallHit.normal))
                {
                    Destroy(reinforcement);
                    return SpawnReinforcement(budget, false);
                }

                RememberCameraPlacement(wallHit.point);
            }

            if (cameraPrefab == null)
            {
                ConfigureNeighborAvoidance(reinforcement);
            }
            spawnedReinforcementCount++;
            budget.TrySpend(selection.Cost);
            return true;
        }

        private bool TryGetRandomAffordableReinforcement(
            ReinforcementBudget budget,
            out ReinforcementPrefabSelection selection,
            bool allowSecurityCamera = true)
        {
            selection = default;
            if (budget == null)
            {
                return false;
            }

            if (TryGetBuilderLocationSelection(budget, allowSecurityCamera, out selection, out bool hasBuilderLocations))
            {
                return true;
            }

            if (hasBuilderLocations)
            {
                return false;
            }

            if (allowSecurityCamera
                && (SecurityCamera.NeighborPlacedCameraCount == 0 || Random.value <= securityCameraPlacementChance)
                && CanUsePrefab(securityCameraPrefab, true)
                && budget.CanAfford(securityCameraCost))
            {
                selection = new ReinforcementPrefabSelection(securityCameraPrefab, securityCameraCost);
                return true;
            }

            if (reinforcementOptions != null && reinforcementOptions.Length > 0)
            {
                int startIndex = Random.Range(0, reinforcementOptions.Length);
                for (int i = 0; i < reinforcementOptions.Length; i++)
                {
                    ReinforcementPrefabOption option = reinforcementOptions[(startIndex + i) % reinforcementOptions.Length];
                    if (option != null && CanUsePrefab(option.Prefab, allowSecurityCamera) && budget.CanAfford(option.Cost))
                    {
                        selection = new ReinforcementPrefabSelection(option.Prefab, option.Cost);
                        return true;
                    }
                }
            }

            if (legacyReinforcementPrefabs != null && legacyReinforcementPrefabs.Length > 0 && budget.CanAfford(1))
            {
                int startIndex = Random.Range(0, legacyReinforcementPrefabs.Length);
                for (int i = 0; i < legacyReinforcementPrefabs.Length; i++)
                {
                    GameObject prefab = legacyReinforcementPrefabs[(startIndex + i) % legacyReinforcementPrefabs.Length];
                    if (CanUsePrefab(prefab, allowSecurityCamera))
                    {
                        selection = new ReinforcementPrefabSelection(prefab, 1);
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasConfiguredReinforcement()
        {
            if (HasBuilderLocations())
            {
                return true;
            }

            if (securityCameraPrefab != null)
            {
                return true;
            }

            if (reinforcementOptions != null)
            {
                for (int i = 0; i < reinforcementOptions.Length; i++)
                {
                    if (reinforcementOptions[i]?.Prefab != null)
                    {
                        return true;
                    }
                }
            }

            if (legacyReinforcementPrefabs != null)
            {
                for (int i = 0; i < legacyReinforcementPrefabs.Length; i++)
                {
                    if (legacyReinforcementPrefabs[i] != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryGetBuilderLocationSelection(
            ReinforcementBudget budget,
            bool allowSecurityCamera,
            out ReinforcementPrefabSelection selection,
            out bool hasBuilderLocations)
        {
            selection = default;
            hasBuilderLocations = false;
            HouseBuilderObject owner = GetComponent<HouseBuilderObject>();
            HouseBuilderWorld world = GetComponentInParent<HouseBuilderWorld>();
            if (owner == null || world == null || world.Catalog == null)
            {
                return false;
            }

            HouseReinforcementLocation[] locations = world.GetComponentsInChildren<HouseReinforcementLocation>(true);
            int startIndex = locations.Length > 0 ? Random.Range(0, locations.Length) : 0;
            for (int i = 0; i < locations.Length; i++)
            {
                HouseReinforcementLocation location = locations[(startIndex + i) % locations.Length];
                if (location == null || !location.IsLinkedTo(owner.InstanceId))
                {
                    continue;
                }

                hasBuilderLocations = true;
                List<HousePlaceableDefinition> definitions = new(location.ResolveDefinitions(world.Catalog));
                int definitionStartIndex = definitions.Count > 0 ? Random.Range(0, definitions.Count) : 0;
                for (int definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
                {
                    HousePlaceableDefinition definition = definitions[(definitionStartIndex + definitionIndex) % definitions.Count];
                    if (definition?.Prefab != null && CanUsePrefab(definition.Prefab, allowSecurityCamera) && budget.CanAfford(1))
                    {
                        selection = new ReinforcementPrefabSelection(definition.Prefab, 1, location.transform);
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasBuilderLocations()
        {
            HouseBuilderObject owner = GetComponent<HouseBuilderObject>();
            HouseBuilderWorld world = GetComponentInParent<HouseBuilderWorld>();
            if (owner == null || world == null)
            {
                return false;
            }

            HouseReinforcementLocation[] locations = world.GetComponentsInChildren<HouseReinforcementLocation>(true);
            for (int i = 0; i < locations.Length; i++)
            {
                if (locations[i] != null && locations[i].IsLinkedTo(owner.InstanceId))
                {
                    return true;
                }
            }

            return false;
        }

        private bool CanUsePrefab(GameObject prefab, bool allowSecurityCamera = true)
        {
            if (prefab == null)
            {
                return false;
            }

            SecurityCamera camera = prefab.GetComponentInChildren<SecurityCamera>(true);
            return camera == null || allowSecurityCamera && attachSecurityCamerasToWalls && SecurityCamera.CanPlaceNeighborCamera;
        }

        private bool TryFindCameraWall(Transform anchor, out RaycastHit bestHit)
        {
            bestHit = default;
            if (!attachSecurityCamerasToWalls || anchor == null)
            {
                return false;
            }

            Vector3 coverageDirection = Vector3.ProjectOnPlane(anchor.forward, Vector3.up).normalized;
            if (coverageDirection.sqrMagnitude <= 0.001f)
            {
                coverageDirection = transform.forward;
            }

            Vector3 right = Vector3.Cross(Vector3.up, coverageDirection).normalized;
            Vector3[] directions =
            {
                -coverageDirection,
                -right,
                right,
                coverageDirection,
                (-coverageDirection - right).normalized,
                (-coverageDirection + right).normalized,
                (coverageDirection - right).normalized,
                (coverageDirection + right).normalized
            };
            Vector3[] originOffsets =
            {
                Vector3.zero,
                right * cameraCandidateOffset,
                -right * cameraCandidateOffset,
                coverageDirection * cameraCandidateOffset,
                -coverageDirection * cameraCandidateOffset,
                (right + coverageDirection).normalized * cameraCandidateOffset,
                (right - coverageDirection).normalized * cameraCandidateOffset,
                (-right + coverageDirection).normalized * cameraCandidateOffset,
                (-right - coverageDirection).normalized * cameraCandidateOffset
            };
            float[] heightOffsets =
            {
                0f,
                cameraMountHeight * 0.5f,
                cameraMountHeight
            };

            cameraWallCandidates.Clear();
            for (int offsetIndex = 0; offsetIndex < originOffsets.Length; offsetIndex++)
            {
                for (int heightIndex = 0; heightIndex < heightOffsets.Length; heightIndex++)
                {
                    Vector3 origin = anchor.position + originOffsets[offsetIndex] + Vector3.up * heightOffsets[heightIndex];
                    for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
                    {
                        if (!Physics.Raycast(origin, directions[directionIndex], out RaycastHit hit, cameraWallSearchDistance, cameraWallMask, QueryTriggerInteraction.Ignore)
                            || Mathf.Abs(Vector3.Dot(hit.normal.normalized, Vector3.up)) > cameraMaximumWallUpDot
                            || hit.collider.GetComponentInParent<SecurityCamera>() != null
                            || SecurityCamera.IsNeighborCameraWithinDistance(hit.point, cameraMinimumSpacing))
                        {
                            continue;
                        }

                        float coverageAlignment = Vector3.Dot(hit.normal.normalized, coverageDirection);
                        float heightPreference = 1f - Mathf.Abs(heightOffsets[heightIndex] - cameraMountHeight) / Mathf.Max(0.1f, cameraMountHeight);
                        float recentPlacementPenalty = GetRecentCameraPlacementPenalty(hit.point);
                        float score = coverageAlignment * 3f
                            + heightPreference * 0.35f
                            - hit.distance * 0.1f
                            - recentPlacementPenalty
                            + Random.Range(-cameraPlacementScoreRandomness, cameraPlacementScoreRandomness);
                        AddOrImproveCameraWallCandidate(hit, score);
                    }
                }
            }

            if (cameraWallCandidates.Count == 0)
            {
                return false;
            }

            CameraWallCandidate bestCandidate = cameraWallCandidates[0];
            for (int i = 1; i < cameraWallCandidates.Count; i++)
            {
                if (cameraWallCandidates[i].Score > bestCandidate.Score)
                {
                    bestCandidate = cameraWallCandidates[i];
                }
            }

            bestHit = bestCandidate.Hit;
            return true;
        }

        private void AddOrImproveCameraWallCandidate(RaycastHit hit, float score)
        {
            const float duplicateDistance = 0.45f;
            float duplicateDistanceSquared = duplicateDistance * duplicateDistance;
            for (int i = 0; i < cameraWallCandidates.Count; i++)
            {
                if ((cameraWallCandidates[i].Hit.point - hit.point).sqrMagnitude > duplicateDistanceSquared)
                {
                    continue;
                }

                if (score > cameraWallCandidates[i].Score)
                {
                    cameraWallCandidates[i] = new CameraWallCandidate(hit, score);
                }

                return;
            }

            cameraWallCandidates.Add(new CameraWallCandidate(hit, score));
        }

        private float GetRecentCameraPlacementPenalty(Vector3 candidatePosition)
        {
            if (cameraRecentPlacementPenaltyRadius <= 0f)
            {
                return 0f;
            }

            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < recentCameraPlacements.Count; i++)
            {
                closestDistance = Mathf.Min(closestDistance, Vector3.Distance(candidatePosition, recentCameraPlacements[i]));
            }

            return Mathf.Clamp01(1f - closestDistance / cameraRecentPlacementPenaltyRadius) * 4f;
        }

        private void RememberCameraPlacement(Vector3 position)
        {
            recentCameraPlacements.Add(position);
            int maximumRememberedPlacements = Mathf.Max(1, cameraRecentPlacementMemory);
            while (recentCameraPlacements.Count > maximumRememberedPlacements)
            {
                recentCameraPlacements.RemoveAt(0);
            }
        }

        private void ConfigureNeighborAvoidance(GameObject reinforcement)
        {
            if (!addNeighborAvoidanceObstacle || reinforcement == null)
            {
                return;
            }

            NavMeshObstacle obstacle = reinforcement.GetComponent<NavMeshObstacle>();
            if (obstacle == null)
            {
                obstacle = reinforcement.AddComponent<NavMeshObstacle>();
            }

            Bounds bounds = CalculateWorldBounds(reinforcement);
            Vector3 localCenter = reinforcement.transform.InverseTransformPoint(bounds.center);
            Vector3 localSize = WorldSizeToLocalSize(bounds.size + Vector3.one * avoidancePadding, reinforcement.transform);
            localSize.y = Mathf.Max(localSize.y, minimumAvoidanceHeight);

            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = localCenter;
            obstacle.size = localSize;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
        }

        private static Bounds CalculateWorldBounds(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>();
            bool hasBounds = false;
            Bounds bounds = new(root.transform.position, Vector3.one * 0.5f);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }

        private static Vector3 WorldSizeToLocalSize(Vector3 worldSize, Transform transform)
        {
            Vector3 scale = transform != null ? transform.lossyScale : Vector3.one;
            return new Vector3(
                DivideByAxis(worldSize.x, scale.x),
                DivideByAxis(worldSize.y, scale.y),
                DivideByAxis(worldSize.z, scale.z));
        }

        private static float DivideByAxis(float value, float axis)
        {
            float divisor = Mathf.Abs(axis);
            return divisor > 0.0001f ? value / divisor : value;
        }

        private void OnDrawGizmos()
        {
            Collider trigger = GetComponent<Collider>();
            if (trigger == null)
            {
                return;
            }

            Color previousColor = Gizmos.color;
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireCube(trigger.bounds.center, trigger.bounds.size);

            Transform anchor = spawnPoint != null ? spawnPoint : transform;
            Gizmos.DrawSphere(anchor.position, 0.18f);
            DrawDirectionArrow(anchor);
            Gizmos.color = previousColor;
        }

        private void DrawDirectionArrow(Transform anchor)
        {
            if (anchor == null)
            {
                return;
            }

            Vector3 start = anchor.position;
            Vector3 direction = anchor.forward.sqrMagnitude > 0.001f ? anchor.forward.normalized : transform.forward;
            Vector3 end = start + direction * directionGizmoLength;
            Gizmos.color = directionGizmoColor;
            Gizmos.DrawLine(start, end);

            Quaternion leftRotation = Quaternion.AngleAxis(145f, Vector3.up);
            Quaternion rightRotation = Quaternion.AngleAxis(-145f, Vector3.up);
            Gizmos.DrawLine(end, end + leftRotation * direction * directionGizmoHeadSize);
            Gizmos.DrawLine(end, end + rightRotation * direction * directionGizmoHeadSize);
        }

        private readonly struct CameraWallCandidate
        {
            public RaycastHit Hit { get; }
            public float Score { get; }

            public CameraWallCandidate(RaycastHit hit, float score)
            {
                Hit = hit;
                Score = score;
            }
        }
    }
}
