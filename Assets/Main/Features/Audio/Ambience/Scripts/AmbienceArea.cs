using System.Collections.Generic;
using UnityEngine;

namespace Neighbor.Main.Features.Audio
{
    [RequireComponent(typeof(Collider))]
    public sealed class AmbienceArea : MonoBehaviour
    {
        private static readonly List<AmbienceArea> ActiveAreas = new List<AmbienceArea>();

        [SerializeField] private AmbienceProfile profile;
        [Tooltip("Higher-priority areas win when multiple areas overlap.")]
        [SerializeField] private int priority;
        [Tooltip("All assigned colliders define this area. Uses the collider on this object when empty.")]
        [SerializeField] private Collider[] boundaries;

        public AmbienceProfile Profile => profile;
        public int Priority => priority;
        internal static IReadOnlyList<AmbienceArea> Areas => ActiveAreas;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveAreas()
        {
            ActiveAreas.Clear();
        }

        private void OnEnable()
        {
            if (!ActiveAreas.Contains(this))
            {
                ActiveAreas.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveAreas.Remove(this);
        }

        public bool Contains(Vector3 worldPosition)
        {
            ResolveBoundaries();

            for (int i = 0; i < boundaries.Length; i++)
            {
                Collider boundary = boundaries[i];
                if (boundary == null || !boundary.enabled || !boundary.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 closestPoint = boundary.ClosestPoint(worldPosition);
                if ((closestPoint - worldPosition).sqrMagnitude < 0.000001f)
                {
                    return true;
                }
            }

            return false;
        }

        private void Reset()
        {
            boundaries = GetComponents<Collider>();
            SetBoundariesAsTriggers();
        }

        private void OnValidate()
        {
            ResolveBoundaries();
            SetBoundariesAsTriggers();
        }

        private void ResolveBoundaries()
        {
            if (boundaries == null || boundaries.Length == 0)
            {
                boundaries = GetComponents<Collider>();
            }
        }

        private void SetBoundariesAsTriggers()
        {
            if (boundaries == null)
            {
                return;
            }

            for (int i = 0; i < boundaries.Length; i++)
            {
                if (boundaries[i] != null)
                {
                    boundaries[i].isTrigger = true;
                }
            }
        }
    }
}
