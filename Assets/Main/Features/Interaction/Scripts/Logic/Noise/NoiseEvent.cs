using System.Collections.Generic;
using Neighbor.Main.Features.Neighbor;
using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(SphereCollider))]
    public sealed class NoiseEvent : MonoBehaviour
    {
        private SphereCollider noiseTrigger;

        public Vector3 Origin { get; private set; }
        public float Radius { get; private set; }
        public float Loudness01 { get; private set; }
        public float Urgency01 { get; private set; }
        public GameObject SourceObject { get; private set; }
        public GameObject InstigatorObject { get; private set; }

        private void Awake()
        {
            noiseTrigger = GetComponent<SphereCollider>();
            noiseTrigger.isTrigger = true;
        }

        public void Initialize(
            Vector3 origin,
            float radius,
            float loudness01,
            GameObject sourceObject,
            float lifetime,
            float urgency01 = 1f,
            GameObject instigatorObject = null)
        {
            Origin = origin;
            Radius = radius;
            Loudness01 = Mathf.Clamp01(loudness01);
            Urgency01 = Mathf.Clamp01(urgency01);
            SourceObject = sourceObject;
            InstigatorObject = instigatorObject != null
                ? instigatorObject
                : sourceObject != null && sourceObject.GetComponentInParent<NeighborBrain>() != null
                    ? sourceObject
                    : null;
            Destroy(gameObject, Mathf.Max(0f, lifetime));

            if (noiseTrigger == null)
            {
                noiseTrigger = GetComponent<SphereCollider>();
            }

            noiseTrigger.isTrigger = true;
            noiseTrigger.radius = radius;

            if (!IsNeighborObject(InstigatorObject)
                && (sourceObject == null || sourceObject.GetComponentInParent<SecurityCamera>() == null))
            {
                PlayerFeedbackEvents.ReportNoise(origin, Loudness01, radius);
                AdaptiveSecurityDirector.ReportDisturbance(Loudness01);
            }

            NotifyListenersInRange();
        }

        private static bool IsNeighborObject(GameObject candidate)
        {
            return candidate != null && candidate.GetComponentInParent<NeighborBrain>() != null;
        }

        private void NotifyListenersInRange()
        {
            float radiusSqr = Radius * Radius;
            IReadOnlyList<NeighborHearing> listeners = NeighborHearing.Listeners;

            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                NeighborHearing listener = listeners[i];
                if (listener == null)
                {
                    continue;
                }

                if ((listener.transform.position - Origin).sqrMagnitude <= radiusSqr)
                {
                    listener.TryHear(this);
                }
            }
        }
    }
}
