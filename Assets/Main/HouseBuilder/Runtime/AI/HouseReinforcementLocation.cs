using System;
using System.Collections.Generic;
using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Neighbor/House Builder/AI/Reinforcement Location")]
    public sealed class HouseReinforcementLocation : MonoBehaviour, IHouseBuilderSerializable
    {
        [SerializeField] private string triggerInstanceId;
        [SerializeField] private List<string> reinforcementDefinitionIds = new();
        [SerializeField] private Color previewColor = new(0.1f, 0.9f, 1f, 0.45f);

        public string TriggerInstanceId => triggerInstanceId;
        public IReadOnlyList<string> ReinforcementDefinitionIds => reinforcementDefinitionIds;
        public string HouseBuilderTypeId => "neighbor.house-builder.reinforcement-location";

        public void Configure(string sourceTriggerInstanceId, IEnumerable<string> definitionIds)
        {
            triggerInstanceId = sourceTriggerInstanceId ?? string.Empty;
            reinforcementDefinitionIds.Clear();
            if (definitionIds == null)
            {
                return;
            }

            foreach (string definitionId in definitionIds)
            {
                if (!string.IsNullOrWhiteSpace(definitionId) && !reinforcementDefinitionIds.Contains(definitionId))
                {
                    reinforcementDefinitionIds.Add(definitionId);
                }
            }
        }

        public bool IsLinkedTo(string sourceTriggerInstanceId)
        {
            return !string.IsNullOrWhiteSpace(sourceTriggerInstanceId) && triggerInstanceId == sourceTriggerInstanceId;
        }

        public string CaptureHouseBuilderState()
        {
            return JsonUtility.ToJson(new State(triggerInstanceId, reinforcementDefinitionIds));
        }

        public void RestoreHouseBuilderState(string json)
        {
            State state = JsonUtility.FromJson<State>(json);
            Configure(state?.TriggerInstanceId, state?.ReinforcementDefinitionIds);
        }

        public IEnumerable<HousePlaceableDefinition> ResolveDefinitions(HouseBuilderCatalog catalog)
        {
            if (catalog == null)
            {
                yield break;
            }

            for (int i = 0; i < reinforcementDefinitionIds.Count; i++)
            {
                if (catalog.TryGetPlaceable(reinforcementDefinitionIds[i], out HousePlaceableDefinition definition))
                {
                    yield return definition;
                }
            }
        }

        private void OnDrawGizmos()
        {
            Color previousColor = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.color = previewColor;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.12f, 0.2f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward);

            HouseBuilderWorld world = GetComponentInParent<HouseBuilderWorld>();
            if (world != null)
            {
                foreach (HousePlaceableDefinition definition in ResolveDefinitions(world.Catalog))
                {
                    DrawPrefabPreview(definition.Prefab);
                    break;
                }
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        private void DrawPrefabPreview(GameObject prefab)
        {
            if (prefab == null)
            {
                return;
            }

            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i].sharedMesh == null)
                {
                    continue;
                }

                Gizmos.matrix = transform.localToWorldMatrix * GetRelativeMatrix(prefab.transform, filters[i].transform);
                Gizmos.DrawMesh(filters[i].sharedMesh);
                Gizmos.DrawWireMesh(filters[i].sharedMesh);
            }

            SkinnedMeshRenderer[] skinnedMeshes = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedMeshes.Length; i++)
            {
                if (skinnedMeshes[i].sharedMesh == null)
                {
                    continue;
                }

                Gizmos.matrix = transform.localToWorldMatrix * GetRelativeMatrix(prefab.transform, skinnedMeshes[i].transform);
                Gizmos.DrawMesh(skinnedMeshes[i].sharedMesh);
                Gizmos.DrawWireMesh(skinnedMeshes[i].sharedMesh);
            }
        }

        private static Matrix4x4 GetRelativeMatrix(Transform root, Transform target)
        {
            return root.worldToLocalMatrix * target.localToWorldMatrix;
        }

        [Serializable]
        private sealed class State
        {
            [SerializeField] private string triggerInstanceId;
            [SerializeField] private List<string> reinforcementDefinitionIds = new();

            public string TriggerInstanceId => triggerInstanceId;
            public IReadOnlyList<string> ReinforcementDefinitionIds => reinforcementDefinitionIds;

            public State(string sourceTriggerInstanceId, IEnumerable<string> definitionIds)
            {
                triggerInstanceId = sourceTriggerInstanceId;
                if (definitionIds != null)
                {
                    reinforcementDefinitionIds.AddRange(definitionIds);
                }
            }
        }
    }
}
