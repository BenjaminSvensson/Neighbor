using System.Collections.Generic;
using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    [AddComponentMenu("Neighbor/House Builder/AI/Neighbor Spawn Point")]
    public sealed class HouseNeighborSpawnPoint : MonoBehaviour
    {
        private static readonly List<HouseNeighborSpawnPoint> ActivePoints = new();

        [SerializeField] private string spawnGroup = "default";
        [SerializeField, Min(0f)] private float selectionWeight = 1f;
        [SerializeField] private bool enabledForRuntime = true;

        public string SpawnGroup => spawnGroup;
        public float SelectionWeight => selectionWeight;
        public bool EnabledForRuntime => enabledForRuntime;
        public static IReadOnlyList<HouseNeighborSpawnPoint> Points => ActivePoints;

        private void OnEnable()
        {
            if (!ActivePoints.Contains(this))
            {
                ActivePoints.Add(this);
            }
        }

        private void OnDisable()
        {
            ActivePoints.Remove(this);
        }

        private void OnDrawGizmos()
        {
            Color previous = Gizmos.color;
            Gizmos.color = new Color(0.85f, 0.15f, 1f, 0.85f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.25f);
            Gizmos.color = previous;
        }
    }
}
