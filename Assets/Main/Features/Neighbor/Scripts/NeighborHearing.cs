using System;
using Neighbor.Main.Features.Interaction;
using UnityEngine;

namespace Neighbor.Main.Features.Neighbor
{
    /// <summary>
    /// Immutable payload describing a noise the neighbor heard.
    /// </summary>
    public readonly struct NeighborNoiseStimulus
    {
        public NeighborNoiseStimulus(Vector3 position, float loudness01, float radius, GameObject sourceObject)
        {
            Position = position;
            Loudness01 = loudness01;
            Radius = radius;
            SourceObject = sourceObject;
        }

        public Vector3 Position { get; }
        public float Loudness01 { get; }
        public float Radius { get; }
        public GameObject SourceObject { get; }
    }

    /// <summary>
    /// Trigger-based hearing sensor that converts NoiseEvent overlaps into throttled
    /// notifications for the neighbor brain.
    /// </summary>
    public sealed class NeighborHearing : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float minimumLoudness = 0.05f;
        [SerializeField, Min(0f)] private float hearingCooldown = 0.2f;

        private float lastHeardTime;

        public event Action<NeighborNoiseStimulus> NoiseHeard;

        private void OnTriggerEnter(Collider other)
        {
            TryHear(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryHear(other);
        }

        private void TryHear(Collider other)
        {
            if (Time.time - lastHeardTime < hearingCooldown)
            {
                return;
            }

            NoiseEvent noiseEvent = other.GetComponent<NoiseEvent>() ?? other.GetComponentInParent<NoiseEvent>();
            if (noiseEvent == null || noiseEvent.Loudness01 < minimumLoudness)
            {
                return;
            }

            // Cooldown is global for this sensor, preventing one lingering trigger from
            // firing repeated investigation requests every physics step.
            lastHeardTime = Time.time;
            NoiseHeard?.Invoke(new NeighborNoiseStimulus(
                noiseEvent.Origin,
                noiseEvent.Loudness01,
                noiseEvent.Radius,
                noiseEvent.SourceObject));
        }
    }
}
