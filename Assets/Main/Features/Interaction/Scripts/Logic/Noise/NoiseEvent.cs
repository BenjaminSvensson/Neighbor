using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(SphereCollider))]
    public sealed class NoiseEvent : MonoBehaviour
    {
        private SphereCollider noiseTrigger;
        private float despawnTime;

        public Vector3 Origin { get; private set; }
        public float Radius { get; private set; }
        public float Loudness01 { get; private set; }
        public GameObject SourceObject { get; private set; }

        private void Awake()
        {
            noiseTrigger = GetComponent<SphereCollider>();
            noiseTrigger.isTrigger = true;
        }

        private void Update()
        {
            if (Time.time >= despawnTime)
            {
                Destroy(gameObject);
            }
        }

        public void Initialize(Vector3 origin, float radius, float loudness01, GameObject sourceObject, float lifetime)
        {
            Origin = origin;
            Radius = radius;
            Loudness01 = Mathf.Clamp01(loudness01);
            SourceObject = sourceObject;
            despawnTime = Time.time + lifetime;

            if (noiseTrigger == null)
            {
                noiseTrigger = GetComponent<SphereCollider>();
            }

            noiseTrigger.isTrigger = true;
            noiseTrigger.radius = radius;
        }
    }
}
