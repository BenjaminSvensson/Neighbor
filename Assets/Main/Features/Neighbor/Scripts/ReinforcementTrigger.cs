using System.Collections.Generic;
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

        public ReinforcementPrefabSelection(GameObject prefab, int cost)
        {
            Prefab = prefab;
            Cost = Mathf.Max(1, cost);
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
        private float runScore;
        private int spawnedReinforcementCount;

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

            rankedTriggers.Sort((a, b) => b.runScore.CompareTo(a.runScore));
            int maximumSpawnCount = Mathf.Min(Mathf.Max(0, maximumLocations), rankedTriggers.Count);
            int spawnedCount = 0;
            for (int i = 0; i < rankedTriggers.Count && spawnedCount < maximumSpawnCount; i++)
            {
                if (rankedTriggers[i].SpawnReinforcement(budget))
                {
                    spawnedCount++;
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

        private bool CanSpawnReinforcement(ReinforcementBudget budget)
        {
            return HasConfiguredReinforcement()
                && TryGetRandomAffordableReinforcement(budget, out _)
                && (maximumPersistentReinforcements <= 0 || spawnedReinforcementCount < maximumPersistentReinforcements);
        }

        private bool SpawnReinforcement(ReinforcementBudget budget)
        {
            if (!TryGetRandomAffordableReinforcement(budget, out ReinforcementPrefabSelection selection))
            {
                return false;
            }

            Transform anchor = spawnPoint != null ? spawnPoint : transform;
            Quaternion rotation = anchor.rotation;
            if (randomizeYaw)
            {
                rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }

            GameObject reinforcement = Instantiate(selection.Prefab, anchor.position, rotation);
            ConfigureNeighborAvoidance(reinforcement);
            spawnedReinforcementCount++;
            budget.TrySpend(selection.Cost);
            return true;
        }

        private bool TryGetRandomAffordableReinforcement(ReinforcementBudget budget, out ReinforcementPrefabSelection selection)
        {
            selection = default;
            if (budget == null)
            {
                return false;
            }

            if (reinforcementOptions != null && reinforcementOptions.Length > 0)
            {
                int startIndex = Random.Range(0, reinforcementOptions.Length);
                for (int i = 0; i < reinforcementOptions.Length; i++)
                {
                    ReinforcementPrefabOption option = reinforcementOptions[(startIndex + i) % reinforcementOptions.Length];
                    if (option != null && option.Prefab != null && budget.CanAfford(option.Cost))
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
                    if (prefab != null)
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
    }
}
