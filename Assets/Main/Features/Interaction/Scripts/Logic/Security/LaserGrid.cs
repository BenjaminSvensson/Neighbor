using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class LaserGrid : MonoBehaviour
    {
        [Header("Power")]
        [SerializeField] private bool startsEnabled = true;
        [SerializeField] private GameObject fuseReference;
        [SerializeField] private bool requireFuseReferenceActive;

        [Header("Beams")]
        [SerializeField, Min(1)] private int beamCount = 4;
        [SerializeField, Min(0.05f)] private float beamSpacing = 0.35f;
        [SerializeField, Min(0.1f)] private float beamLength = 3f;
        [SerializeField, Min(0f)] private float beamRadius = 0.05f;
        [SerializeField] private Vector3 localBeamOriginOffset = new(0f, 0.9f, 0f);
        [SerializeField] private Vector3 localBeamDirection = Vector3.right;
        [SerializeField] private Vector3 localSpacingDirection = Vector3.up;
        [SerializeField] private LayerMask detectionMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Alert")]
        [SerializeField, Min(0f)] private float alertRadius = 16f;
        [SerializeField, Range(0f, 1f)] private float alertLoudness = 0.75f;
        [SerializeField, Range(0f, 1f)] private float alertUrgency = 0.9f;
        [SerializeField, Min(0.02f)] private float alertLifetime = 0.75f;
        [SerializeField, Min(0f)] private float triggerCooldown = 1.2f;
        [SerializeField] private GameObject[] activationTargets;

        [Header("Visuals")]
        [SerializeField] private Transform beamVisualRoot;
        [SerializeField] private Renderer[] beamRenderers;
        [SerializeField] private Color enabledColor = new(1f, 0.04f, 0.02f, 1f);
        [SerializeField] private Color disabledColor = new(0.12f, 0.02f, 0.02f, 0.35f);

        private readonly RaycastHit[] hits = new RaycastHit[12];
        private MaterialPropertyBlock propertyBlock;
        private bool? lastVisualPowered;
        private bool powered;
        private float nextTriggerTime;
        private ItemAudioFeedback audioFeedback;

        public bool IsPowered => powered && IsFuseSatisfied();
        public int BeamCount => beamCount;
        public float BeamSpacing => beamSpacing;
        public float BeamLength => beamLength;
        public Vector3 BeamDirection => transform.TransformDirection(NormalizedOrFallback(localBeamDirection, Vector3.right));
        public Vector3 SpacingDirection => transform.TransformDirection(NormalizedOrFallback(localSpacingDirection, Vector3.up));
        private Vector3 BeamOrigin => transform.TransformPoint(localBeamOriginOffset);

        private void Awake()
        {
            powered = startsEnabled;
            audioFeedback = ItemAudioFeedback.Resolve(gameObject);
            ResolveBeamRenderers();
            ApplyVisualState(true);
        }

        private void Update()
        {
            ApplyVisualState();

            if (!IsPowered || Time.time < nextTriggerTime)
            {
                return;
            }

            CheckBeams();
        }

        public void SetPowered(bool isPowered)
        {
            powered = isPowered;
            ApplyVisualState();
        }

        public void TogglePowered()
        {
            SetPowered(!powered);
        }

        public Vector3 GetBeamStart(int index)
        {
            float centeredIndex = index - (beamCount - 1) * 0.5f;
            return BeamOrigin + SpacingDirection * (centeredIndex * beamSpacing);
        }

        public Vector3 GetBeamEnd(int index)
        {
            return GetBeamStart(index) + BeamDirection * beamLength;
        }

        private void CheckBeams()
        {
            for (int i = 0; i < beamCount; i++)
            {
                Vector3 origin = GetBeamStart(i);
                Vector3 direction = BeamDirection;
                int hitCount = beamRadius > 0f
                    ? Physics.SphereCastNonAlloc(origin, beamRadius, direction, hits, beamLength, detectionMask, triggerInteraction)
                    : Physics.RaycastNonAlloc(origin, direction, hits, beamLength, detectionMask, triggerInteraction);

                if (ContainsPlayerHit(hitCount))
                {
                    TriggerAlert();
                    return;
                }
            }
        }

        private bool ContainsPlayerHit(int hitCount)
        {
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = hits[i].collider;
                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (hit.GetComponentInParent<PlayerController>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void TriggerAlert()
        {
            nextTriggerTime = Time.time + triggerCooldown;
            audioFeedback?.Play(ItemSoundProfile.Alarm, 0.75f);
            EmitNeighborNoise();
            ActivateTargets();
        }

        private void EmitNeighborNoise()
        {
            if (alertRadius <= 0f || alertLoudness <= 0f)
            {
                return;
            }

            Vector3 origin = BeamOrigin + BeamDirection * (beamLength * 0.5f);
            GameObject noiseObject = new("LaserGridNoiseEvent");
            noiseObject.transform.position = origin;

            SphereCollider sphereCollider = noiseObject.AddComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
            sphereCollider.radius = alertRadius;

            Rigidbody noiseBody = noiseObject.AddComponent<Rigidbody>();
            noiseBody.isKinematic = true;
            noiseBody.useGravity = false;

            NoiseEvent noiseEvent = noiseObject.AddComponent<NoiseEvent>();
            noiseEvent.Initialize(origin, alertRadius, alertLoudness, gameObject, alertLifetime, alertUrgency);
        }

        private void ActivateTargets()
        {
            if (activationTargets == null)
            {
                return;
            }

            for (int i = 0; i < activationTargets.Length; i++)
            {
                GameObject target = activationTargets[i];
                if (target == null)
                {
                    continue;
                }

                target.SendMessage("Trigger", SendMessageOptions.DontRequireReceiver);
                target.SendMessage("Activate", SendMessageOptions.DontRequireReceiver);
            }
        }

        private bool IsFuseSatisfied()
        {
            return !requireFuseReferenceActive || fuseReference == null || fuseReference.activeInHierarchy;
        }

        private void ResolveBeamRenderers()
        {
            if ((beamRenderers == null || beamRenderers.Length == 0) && beamVisualRoot != null)
            {
                beamRenderers = beamVisualRoot.GetComponentsInChildren<Renderer>(true);
            }
        }

        private void ApplyVisualState(bool force = false)
        {
            ResolveBeamRenderers();
            bool visible = IsPowered;
            if (!force && lastVisualPowered.HasValue && lastVisualPowered.Value == visible)
            {
                return;
            }

            lastVisualPowered = visible;
            Color color = visible ? enabledColor : disabledColor;
            propertyBlock ??= new MaterialPropertyBlock();

            if (beamRenderers == null)
            {
                return;
            }

            for (int i = 0; i < beamRenderers.Length; i++)
            {
                Renderer beamRenderer = beamRenderers[i];
                if (beamRenderer == null)
                {
                    continue;
                }

                beamRenderer.enabled = visible;
                beamRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", color);
                propertyBlock.SetColor("_Color", color);
                propertyBlock.SetColor("_EmissionColor", visible ? color * 1.8f : Color.black);
                beamRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private static Vector3 NormalizedOrFallback(Vector3 value, Vector3 fallback)
        {
            return value.sqrMagnitude > 0.0001f ? value.normalized : fallback;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 direction = transform.TransformDirection(NormalizedOrFallback(localBeamDirection, Vector3.right));
            Vector3 spacingDirection = transform.TransformDirection(NormalizedOrFallback(localSpacingDirection, Vector3.up));
            Vector3 origin = transform.TransformPoint(localBeamOriginOffset);
            Gizmos.color = startsEnabled ? Color.red : Color.gray;

            int count = Mathf.Max(1, beamCount);
            for (int i = 0; i < count; i++)
            {
                float centeredIndex = i - (count - 1) * 0.5f;
                Vector3 start = origin + spacingDirection * (centeredIndex * beamSpacing);
                Vector3 end = start + direction * beamLength;
                Gizmos.DrawLine(start, end);
                if (beamRadius > 0f)
                {
                    Gizmos.DrawWireSphere(start, beamRadius);
                    Gizmos.DrawWireSphere(end, beamRadius);
                }
            }
        }

        private void OnValidate()
        {
            beamCount = Mathf.Max(1, beamCount);
            beamSpacing = Mathf.Max(0.05f, beamSpacing);
            beamLength = Mathf.Max(0.1f, beamLength);
            beamRadius = Mathf.Max(0f, beamRadius);
            alertRadius = Mathf.Max(0f, alertRadius);
            alertLifetime = Mathf.Max(0.02f, alertLifetime);
            triggerCooldown = Mathf.Max(0f, triggerCooldown);
        }
    }
}
