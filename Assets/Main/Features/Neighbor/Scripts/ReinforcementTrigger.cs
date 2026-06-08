using System.Collections.Generic;
using Neighbor.Main.Features.Player;
using UnityEngine;
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

        [Header("Editor Gizmo")]
        [SerializeField] private Color gizmoColor = new Color(1f, 0.16f, 0.08f, 0.55f);

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

            Instantiate(selection.Prefab, anchor.position, rotation);
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
            return reinforcementOptions != null && reinforcementOptions.Length > 0
                || legacyReinforcementPrefabs != null && legacyReinforcementPrefabs.Length > 0;
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
            Gizmos.DrawLine(anchor.position, anchor.position + anchor.forward);
            Gizmos.color = previousColor;
        }
    }
}
