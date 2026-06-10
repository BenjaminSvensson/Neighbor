using System.Collections.Generic;
using UnityEngine;

namespace Neighbor.Main.Features.Neighbor
{
    public sealed class NeighborClimbLink : MonoBehaviour
    {
        private static readonly List<NeighborClimbLink> ActiveLinks = new();

        [SerializeField] private Transform bottomPoint;
        [SerializeField] private Transform topPoint;
        [SerializeField, Min(0f)] private float activationRadius = 8f;
        [SerializeField, Min(0f)] private float minimumHeightGain = 0.35f;
        [SerializeField, Min(0.01f)] private float climbDuration = 0.32f;
        [SerializeField, Min(0f)] private float jumpArcHeight = 0.9f;
        [SerializeField] private bool canUseForChase = true;

        public Vector3 BottomPosition => bottomPoint != null ? bottomPoint.position : transform.position;
        public Vector3 TopPosition => topPoint != null ? topPoint.position : transform.position;
        public float ClimbDuration => climbDuration;
        public float JumpArcHeight => jumpArcHeight;
        public static IReadOnlyList<NeighborClimbLink> Links => ActiveLinks;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveLinks()
        {
            ActiveLinks.Clear();
        }

        public bool CanUse(Vector3 neighborPosition, Vector3 targetPosition)
        {
            if (!canUseForChase || topPoint == null)
            {
                return false;
            }

            float heightGain = TopPosition.y - neighborPosition.y;
            if (heightGain < minimumHeightGain)
            {
                return false;
            }

            float targetHeightAdvantage = targetPosition.y - neighborPosition.y;
            if (targetHeightAdvantage < minimumHeightGain)
            {
                return false;
            }

            return Vector3.Distance(BottomPosition, targetPosition) <= activationRadius
                || Vector3.Distance(TopPosition, targetPosition) <= activationRadius;
        }

        public float Score(Vector3 neighborPosition, Vector3 targetPosition, float pathDistanceToBottom)
        {
            float heightGain = TopPosition.y - neighborPosition.y;
            float topDistanceToTarget = Vector3.Distance(TopPosition, targetPosition);
            float bottomDistanceToNeighbor = Vector3.Distance(BottomPosition, neighborPosition);
            return heightGain * 12f - topDistanceToTarget * 1.25f - pathDistanceToBottom * 0.25f - bottomDistanceToNeighbor * 0.15f;
        }

        private void OnEnable()
        {
            if (!ActiveLinks.Contains(this))
            {
                ActiveLinks.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveLinks.Remove(this);
        }

        private void OnDrawGizmos()
        {
            Vector3 bottom = BottomPosition;
            Vector3 top = TopPosition;
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(bottom, 0.16f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(top, 0.16f);
            Gizmos.color = Color.Lerp(Color.yellow, Color.cyan, 0.5f);
            Gizmos.DrawLine(bottom, top);
        }
    }
}
