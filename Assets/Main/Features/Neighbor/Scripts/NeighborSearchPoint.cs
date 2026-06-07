using System.Collections.Generic;
using UnityEngine;

namespace Neighbor.Main.Features.Neighbor
{
    [AddComponentMenu("Neighbor/Neighbor Search Point")]
    public sealed class NeighborSearchPoint : MonoBehaviour
    {
        private static readonly List<NeighborSearchPoint> ActivePoints = new();

        [Header("Search Point")]
        [SerializeField, Min(0f)] private float selectionPriority = 1f;

        [Header("Editor Gizmo")]
        [SerializeField, Min(0.05f)] private float gizmoRadius = 0.25f;
        [SerializeField] private Color gizmoColor = new Color(1f, 0.65f, 0.08f, 0.9f);

        public Vector3 Position => transform.position;
        public float SelectionPriority => selectionPriority;
        public static IReadOnlyList<NeighborSearchPoint> Points => ActivePoints;

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
            DrawGizmo(false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawGizmo(true);
        }

        private void DrawGizmo(bool selected)
        {
            Color previousColor = Gizmos.color;
            Color color = gizmoColor;
            if (!selected)
            {
                color.a *= 0.65f;
            }

            float radius = Mathf.Max(0.05f, gizmoRadius);
            Vector3 position = transform.position + Vector3.up * radius;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(position, radius);
            Gizmos.DrawLine(position + Vector3.down * radius, position + Vector3.up * radius);
            Gizmos.DrawLine(position + Vector3.left * radius, position + Vector3.right * radius);
            Gizmos.DrawLine(position + Vector3.back * radius, position + Vector3.forward * radius);
            Gizmos.color = previousColor;
        }
    }
}
