using System;
using System.Collections.Generic;
using Neighbor.Main.Features.Interaction;
using UnityEngine;

namespace Neighbor.Main.Features.Neighbor
{
    public readonly struct NeighborNoiseStimulus
    {
        public NeighborNoiseStimulus(Vector3 position, float loudness01, float urgency01, float radius, GameObject sourceObject)
        {
            Position = position;
            Loudness01 = loudness01;
            Urgency01 = urgency01;
            Radius = radius;
            SourceObject = sourceObject;
        }

        public Vector3 Position { get; }
        public float Loudness01 { get; }
        public float Urgency01 { get; }
        public float Radius { get; }
        public GameObject SourceObject { get; }
    }

    public sealed class NeighborHearing : MonoBehaviour
    {
        private static readonly List<NeighborHearing> ActiveListeners = new();

        [SerializeField, Range(0f, 1f)] private float minimumLoudness = 0.05f;
        [SerializeField, Min(0f)] private float hearingCooldown = 0.2f;

        private float lastHeardTime;

        public static IReadOnlyList<NeighborHearing> Listeners => ActiveListeners;
        public event Action<NeighborNoiseStimulus> NoiseHeard;

        private void OnEnable()
        {
            if (!ActiveListeners.Contains(this))
            {
                ActiveListeners.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveListeners.Remove(this);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryHear(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryHear(other);
        }

        public void TryHear(NoiseEvent noiseEvent)
        {
            if (Time.time - lastHeardTime < hearingCooldown)
            {
                return;
            }

            if (noiseEvent == null || noiseEvent.Loudness01 < minimumLoudness)
            {
                return;
            }

            lastHeardTime = Time.time;
            NoiseHeard?.Invoke(new NeighborNoiseStimulus(
                noiseEvent.Origin,
                noiseEvent.Loudness01,
                noiseEvent.Urgency01,
                noiseEvent.Radius,
                noiseEvent.SourceObject));
        }

        private void TryHear(Collider other)
        {
            NoiseEvent noiseEvent = other.GetComponent<NoiseEvent>() ?? other.GetComponentInParent<NoiseEvent>();
            TryHear(noiseEvent);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveListeners()
        {
            ActiveListeners.Clear();
        }
    }
}
