using System.Collections.Generic;
using UnityEngine;

namespace Neighbor.Main.Features.Neighbor
{
    public sealed class NeighborTaskLocation : MonoBehaviour
    {
        private static readonly List<NeighborTaskLocation> ActiveLocations = new();

        [SerializeField, Min(0f)] private float minimumWaitTime = 1.5f;
        [SerializeField, Min(0f)] private float maximumWaitTime = 5f;
        [SerializeField] private bool canRepeatImmediately;

        public Vector3 Position => transform.position;
        public float RandomWaitTime => Random.Range(minimumWaitTime, Mathf.Max(minimumWaitTime, maximumWaitTime));
        public bool CanRepeatImmediately => canRepeatImmediately;
        public static IReadOnlyList<NeighborTaskLocation> Locations => ActiveLocations;

        private void OnEnable()
        {
            if (!ActiveLocations.Contains(this))
            {
                ActiveLocations.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveLocations.Remove(this);
        }

        private void OnValidate()
        {
            maximumWaitTime = Mathf.Max(minimumWaitTime, maximumWaitTime);
        }
    }
}
