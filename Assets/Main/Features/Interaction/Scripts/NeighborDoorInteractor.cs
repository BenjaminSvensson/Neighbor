using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class NeighborDoorInteractor : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float checkRadius = 0.55f;
        [SerializeField, Min(0f)] private float checkDistance = 1.2f;
        [SerializeField] private LayerMask doorMask = ~0;
        [SerializeField, Min(0f)] private float interactionCooldown = 0.35f;

        private readonly Collider[] hits = new Collider[8];
        private float nextInteractionTime;

        private void Update()
        {
            if (Time.time < nextInteractionTime)
            {
                return;
            }

            Vector3 center = transform.position + Vector3.up + transform.forward * checkDistance;
            int hitCount = Physics.OverlapSphereNonAlloc(center, checkRadius, hits, doorMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Door door = hits[i] != null ? hits[i].GetComponentInParent<Door>() : null;
                if (door == null)
                {
                    continue;
                }

                if (door.IsLocked)
                {
                    continue;
                }

                if (door.TryOpenFor(transform))
                {
                    nextInteractionTime = Time.time + interactionCooldown;
                    return;
                }
            }
        }
    }
}
