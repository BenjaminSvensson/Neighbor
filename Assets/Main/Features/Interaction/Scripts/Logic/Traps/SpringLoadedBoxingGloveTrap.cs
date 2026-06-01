using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class SpringLoadedBoxingGloveTrap : MonoBehaviour
    {
        private enum TrapState
        {
            Ready,
            Extending,
            Extended,
            Retracting,
            Cooldown
        }

        [Header("References")]
        [SerializeField] private Transform glove;
        [SerializeField] private Transform spring;
        [SerializeField] private Collider triggerVolume;

        [Header("Trigger")]
        [SerializeField] private bool useTriggerVolume = true;
        [SerializeField] private bool useForwardRayTrigger = true;
        [SerializeField, Min(0.1f)] private float triggerRange = 2.4f;
        [SerializeField, Min(0f)] private float triggerRadius = 0.32f;
        [SerializeField] private LayerMask triggerMask = ~0;
        [SerializeField] private QueryTriggerInteraction rayTriggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Motion")]
        [SerializeField, Min(0f)] private float extendDistance = 1.25f;
        [SerializeField, Min(0.01f)] private float extendTime = 0.08f;
        [SerializeField, Min(0f)] private float holdExtendedTime = 0.12f;
        [SerializeField, Min(0.01f)] private float resetTime = 0.45f;
        [SerializeField, Min(0f)] private float cooldown = 1.2f;

        [Header("Impact")]
        [SerializeField, Min(0f)] private float pushForce = 2.2f;
        [SerializeField, Min(0f)] private float rigidbodyImpulse = 8f;
        [SerializeField, Min(0f)] private float upwardPush = 0.25f;
        [SerializeField, Min(0f)] private float hitRadius = 0.45f;

        [Header("Feedback")]
        [SerializeField] private Renderer gloveRenderer;
        [SerializeField] private Color readyColor = new(0.9f, 0.05f, 0.04f, 1f);
        [SerializeField] private Color triggeredColor = new(1f, 0.65f, 0.08f, 1f);

        private readonly RaycastHit[] rayHits = new RaycastHit[8];
        private readonly Collider[] hitColliders = new Collider[16];
        private MaterialPropertyBlock propertyBlock;
        private Vector3 gloveRestLocalPosition;
        private Vector3 springRestLocalPosition;
        private Vector3 springRestLocalScale;
        private TrapState state = TrapState.Ready;
        private float stateStartTime;
        private float cooldownUntilTime;
        private bool hasHitThisPunch;

        private void Awake()
        {
            if (glove == null)
            {
                glove = transform.Find("Glove");
            }

            if (spring == null)
            {
                spring = transform.Find("Spring");
            }

            if (triggerVolume == null)
            {
                triggerVolume = GetComponentInChildren<Collider>();
            }

            if (triggerVolume != null)
            {
                triggerVolume.isTrigger = true;
                triggerVolume.enabled = useTriggerVolume;
            }

            if (gloveRenderer == null && glove != null)
            {
                gloveRenderer = glove.GetComponentInChildren<Renderer>();
            }

            gloveRestLocalPosition = glove != null ? glove.localPosition : Vector3.zero;
            springRestLocalPosition = spring != null ? spring.localPosition : Vector3.zero;
            springRestLocalScale = spring != null ? spring.localScale : Vector3.one;
            ApplyReadyVisual();
        }

        private void Update()
        {
            if (state == TrapState.Ready && useForwardRayTrigger)
            {
                CheckRayTrigger();
            }

            UpdateState();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!useTriggerVolume)
            {
                return;
            }

            if (other != null && other.GetComponentInParent<PlayerController>() != null)
            {
                TryTrigger();
            }
        }

        public void Trigger()
        {
            TryTrigger();
        }

        private void CheckRayTrigger()
        {
            Vector3 origin = transform.position;
            Vector3 direction = transform.forward;
            int hitCount = triggerRadius > 0f
                ? Physics.SphereCastNonAlloc(origin, triggerRadius, direction, rayHits, triggerRange, triggerMask, rayTriggerInteraction)
                : Physics.RaycastNonAlloc(origin, direction, rayHits, triggerRange, triggerMask, rayTriggerInteraction);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = rayHits[i].collider;
                if (hit != null && hit.GetComponentInParent<PlayerController>() != null)
                {
                    TryTrigger();
                    return;
                }
            }
        }

        private void TryTrigger()
        {
            if (state != TrapState.Ready || Time.time < cooldownUntilTime)
            {
                return;
            }

            state = TrapState.Extending;
            stateStartTime = Time.time;
            hasHitThisPunch = false;
            ApplyTriggeredVisual();
        }

        private void UpdateState()
        {
            switch (state)
            {
                case TrapState.Ready:
                    ApplyPose(0f);
                    break;
                case TrapState.Extending:
                    UpdateExtending();
                    break;
                case TrapState.Extended:
                    UpdateExtended();
                    break;
                case TrapState.Retracting:
                    UpdateRetracting();
                    break;
                case TrapState.Cooldown:
                    UpdateCooldown();
                    break;
            }
        }

        private void UpdateExtending()
        {
            float amount = Mathf.Clamp01((Time.time - stateStartTime) / extendTime);
            ApplyPose(Mathf.SmoothStep(0f, 1f, amount));
            TryHitTargets();

            if (amount >= 1f)
            {
                state = TrapState.Extended;
                stateStartTime = Time.time;
            }
        }

        private void UpdateExtended()
        {
            ApplyPose(1f);
            TryHitTargets();

            if (Time.time - stateStartTime >= holdExtendedTime)
            {
                state = TrapState.Retracting;
                stateStartTime = Time.time;
            }
        }

        private void UpdateRetracting()
        {
            float amount = Mathf.Clamp01((Time.time - stateStartTime) / resetTime);
            ApplyPose(1f - Mathf.SmoothStep(0f, 1f, amount));

            if (amount >= 1f)
            {
                state = TrapState.Cooldown;
                stateStartTime = Time.time;
                cooldownUntilTime = Time.time + cooldown;
                ApplyReadyVisual();
            }
        }

        private void UpdateCooldown()
        {
            ApplyPose(0f);
            if (Time.time >= cooldownUntilTime)
            {
                state = TrapState.Ready;
            }
        }

        private void ApplyPose(float amount)
        {
            Vector3 offset = Vector3.forward * (extendDistance * amount);

            if (glove != null)
            {
                glove.localPosition = gloveRestLocalPosition + offset;
            }

            if (spring != null)
            {
                spring.localPosition = springRestLocalPosition + offset * 0.5f;
                spring.localScale = new Vector3(
                    springRestLocalScale.x,
                    Mathf.Lerp(springRestLocalScale.y * 0.45f, springRestLocalScale.y, amount),
                    springRestLocalScale.z);
            }
        }

        private void TryHitTargets()
        {
            if (hasHitThisPunch || glove == null || hitRadius <= 0f)
            {
                return;
            }

            int hitCount = Physics.OverlapSphereNonAlloc(glove.position, hitRadius, hitColliders, triggerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = hitColliders[i];
                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (TryPushPlayer(hit) || TryPushRigidbody(hit))
                {
                    hasHitThisPunch = true;
                    return;
                }
            }
        }

        private bool TryPushPlayer(Collider hit)
        {
            PlayerController player = hit.GetComponentInParent<PlayerController>();
            if (player == null)
            {
                return false;
            }

            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller == null)
            {
                return false;
            }

            Vector3 pushDirection = (transform.forward + Vector3.up * upwardPush).normalized;
            controller.Move(pushDirection * pushForce);
            return true;
        }

        private bool TryPushRigidbody(Collider hit)
        {
            Rigidbody body = hit.attachedRigidbody;
            if (body == null || body.isKinematic)
            {
                return false;
            }

            Vector3 pushDirection = (transform.forward + Vector3.up * upwardPush).normalized;
            body.AddForce(pushDirection * rigidbodyImpulse, ForceMode.Impulse);
            return true;
        }

        private void ApplyReadyVisual()
        {
            ApplyGloveColor(readyColor);
        }

        private void ApplyTriggeredVisual()
        {
            ApplyGloveColor(triggeredColor);
        }

        private void ApplyGloveColor(Color color)
        {
            if (gloveRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            gloveRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            propertyBlock.SetColor("_EmissionColor", color == triggeredColor ? color * 0.35f : Color.black);
            gloveRenderer.SetPropertyBlock(propertyBlock);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position + transform.forward * triggerRange, triggerRadius);
            if (glove != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(glove.position, hitRadius);
            }
        }

        private void OnValidate()
        {
            triggerRange = Mathf.Max(0.1f, triggerRange);
            triggerRadius = Mathf.Max(0f, triggerRadius);
            extendDistance = Mathf.Max(0f, extendDistance);
            extendTime = Mathf.Max(0.01f, extendTime);
            holdExtendedTime = Mathf.Max(0f, holdExtendedTime);
            resetTime = Mathf.Max(0.01f, resetTime);
            cooldown = Mathf.Max(0f, cooldown);
            pushForce = Mathf.Max(0f, pushForce);
            rigidbodyImpulse = Mathf.Max(0f, rigidbodyImpulse);
            upwardPush = Mathf.Max(0f, upwardPush);
            hitRadius = Mathf.Max(0f, hitRadius);
        }
    }
}
