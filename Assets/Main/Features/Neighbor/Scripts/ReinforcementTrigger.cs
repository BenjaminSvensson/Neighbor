using System.Collections.Generic;
using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Neighbor
{
    [AddComponentMenu("Neighbor/Reinforcement Trigger")]
    [RequireComponent(typeof(Collider))]
    public sealed class ReinforcementTrigger : MonoBehaviour
    {
        private static readonly List<ReinforcementTrigger> ActiveTriggers = new();

        [Header("Run Tracking")]
        [SerializeField, Min(0.01f)] private float visitWeight = 1f;

        [Header("Reinforcement")]
        [SerializeField] private GameObject[] reinforcementPrefabs;
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

        public static void ApplyRunReinforcements(int maximumLocations)
        {
            List<ReinforcementTrigger> rankedTriggers = new();
            for (int i = 0; i < ActiveTriggers.Count; i++)
            {
                ReinforcementTrigger trigger = ActiveTriggers[i];
                if (trigger != null && trigger.runScore > 0f && trigger.CanSpawnReinforcement())
                {
                    rankedTriggers.Add(trigger);
                }
            }

            rankedTriggers.Sort((a, b) => b.runScore.CompareTo(a.runScore));
            int spawnCount = Mathf.Min(Mathf.Max(0, maximumLocations), rankedTriggers.Count);
            for (int i = 0; i < spawnCount; i++)
            {
                rankedTriggers[i].SpawnReinforcement();
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

        private bool CanSpawnReinforcement()
        {
            return reinforcementPrefabs != null
                && reinforcementPrefabs.Length > 0
                && (maximumPersistentReinforcements <= 0 || spawnedReinforcementCount < maximumPersistentReinforcements);
        }

        private void SpawnReinforcement()
        {
            GameObject prefab = GetRandomReinforcementPrefab();
            if (prefab == null)
            {
                return;
            }

            Transform anchor = spawnPoint != null ? spawnPoint : transform;
            Quaternion rotation = anchor.rotation;
            if (randomizeYaw)
            {
                rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }

            Instantiate(prefab, anchor.position, rotation);
            spawnedReinforcementCount++;
        }

        private GameObject GetRandomReinforcementPrefab()
        {
            for (int i = 0; i < reinforcementPrefabs.Length; i++)
            {
                GameObject prefab = reinforcementPrefabs[Random.Range(0, reinforcementPrefabs.Length)];
                if (prefab != null)
                {
                    return prefab;
                }
            }

            return null;
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
