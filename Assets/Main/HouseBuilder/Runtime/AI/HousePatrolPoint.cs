using System.Collections.Generic;
using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    [AddComponentMenu("Neighbor/House Builder/AI/Patrol Point")]
    public sealed class HousePatrolPoint : MonoBehaviour
    {
        private static readonly List<HousePatrolPoint> ActivePoints = new();

        [SerializeField] private string patrolRoute = "default";
        [SerializeField, Min(0f)] private float waitTime = 1f;
        [SerializeField, Min(0f)] private float selectionWeight = 1f;
        [SerializeField] private HousePatrolPoint nextPoint;

        public string PatrolRoute => patrolRoute;
        public float WaitTime => waitTime;
        public float SelectionWeight => selectionWeight;
        public HousePatrolPoint NextPoint => nextPoint;
        public static IReadOnlyList<HousePatrolPoint> Points => ActivePoints;

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
            Gizmos.color = new Color(0.1f, 0.9f, 0.75f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.15f, 0.2f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward);
            if (nextPoint != null)
            {
                Gizmos.DrawLine(transform.position, nextPoint.transform.position);
            }

            Gizmos.color = previous;
        }
    }
}
