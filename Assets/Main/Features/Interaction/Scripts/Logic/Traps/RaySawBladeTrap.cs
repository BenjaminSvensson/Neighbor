using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class RaySawBladeTrap : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private bool startsActive = true;
        [SerializeField, Min(0.1f)] private float activeRange = 3f;
        [SerializeField, Min(0f)] private float castRadius = 0.18f;
        [SerializeField] private Vector3 localDirection = Vector3.forward;
        [SerializeField] private LayerMask detectionMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float warningDelay = 0.15f;
        [SerializeField, Min(0f)] private float hitCooldown = 0.8f;
        [SerializeField] private bool cyclesOnOff;
        [SerializeField, Min(0.05f)] private float activeDuration = 2.5f;
        [SerializeField, Min(0.05f)] private float inactiveDuration = 1.2f;

        [Header("Effect")]
        [SerializeField, Min(0f)] private float playerKnockbackDistance = 1.8f;
        [SerializeField, Min(0f)] private float rigidbodyImpulse = 7f;
        [SerializeField, Min(0f)] private float upwardPush = 0.25f;

        [Header("Visuals")]
        [SerializeField] private Transform bladeVisual;
        [SerializeField] private Transform slidingVisual;
        [SerializeField] private LineRenderer dangerLine;
        [SerializeField] private Renderer warningRenderer;
        [SerializeField, Min(0f)] private float bladeSpinDegreesPerSecond = 720f;
        [SerializeField, Min(0f)] private float slideAmplitude = 0.18f;
        [SerializeField, Min(0f)] private float slideFrequency = 3f;
        [SerializeField] private Color activeColor = new(1f, 0.08f, 0.04f, 1f);
        [SerializeField] private Color warningColor = new(1f, 0.75f, 0.06f, 1f);
        [SerializeField] private Color inactiveColor = new(0.25f, 0.25f, 0.25f, 1f);

        private readonly RaycastHit[] hits = new RaycastHit[12];
        private MaterialPropertyBlock propertyBlock;
        private Vector3 slidingRestLocalPosition;
        private float cycleStateStartTime;
        private float armedAtTime;
        private float nextHitTime;
        private bool isActive;

        private Vector3 Direction => transform.TransformDirection(localDirection.sqrMagnitude > 0.0001f ? localDirection.normalized : Vector3.forward);
        private bool IsArmed => isActive && Time.time >= armedAtTime;

        private void Awake()
        {
            isActive = startsActive;
            cycleStateStartTime = Time.time;
            armedAtTime = isActive ? Time.time + warningDelay : float.PositiveInfinity;
            if (slidingVisual != null)
            {
                slidingRestLocalPosition = slidingVisual.localPosition;
            }

            ConfigureDangerLine();
            ApplyVisualState();
        }

        private void Update()
        {
            UpdateCycle();
            UpdateAnimation();
            ConfigureDangerLine();
            ApplyVisualState();

            if (IsArmed)
            {
                CheckPath();
            }
        }

        public void SetActive(bool active)
        {
            if (isActive == active)
            {
                return;
            }

            isActive = active;
            cycleStateStartTime = Time.time;
            armedAtTime = isActive ? Time.time + warningDelay : float.PositiveInfinity;
        }

        private void UpdateCycle()
        {
            if (!cyclesOnOff)
            {
                return;
            }

            float duration = isActive ? activeDuration : inactiveDuration;
            if (Time.time - cycleStateStartTime >= duration)
            {
                SetActive(!isActive);
            }
        }

        private void CheckPath()
        {
            if (Time.time < nextHitTime)
            {
                return;
            }

            Vector3 origin = transform.position;
            Vector3 direction = Direction;
            int hitCount = castRadius > 0f
                ? Physics.SphereCastNonAlloc(origin, castRadius, direction, hits, activeRange, detectionMask, triggerInteraction)
                : Physics.RaycastNonAlloc(origin, direction, hits, activeRange, detectionMask, triggerInteraction);

            float bestDistance = float.PositiveInfinity;
            Collider bestHit = null;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.distance >= bestDistance || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (hit.collider.GetComponentInParent<PlayerController>() == null && hit.collider.attachedRigidbody == null)
                {
                    continue;
                }

                bestHit = hit.collider;
                bestDistance = hit.distance;
            }

            if (bestHit == null)
            {
                return;
            }

            if (TryApplyPlayerEffect(bestHit) || TryApplyRigidbodyEffect(bestHit))
            {
                nextHitTime = Time.time + hitCooldown;
            }
        }

        private bool TryApplyPlayerEffect(Collider hit)
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

            Vector3 pushDirection = (Direction + Vector3.up * upwardPush).normalized;
            controller.Move(pushDirection * playerKnockbackDistance);
            return true;
        }

        private bool TryApplyRigidbodyEffect(Collider hit)
        {
            Rigidbody body = hit.attachedRigidbody;
            if (body == null || body.isKinematic)
            {
                return false;
            }

            Vector3 pushDirection = (Direction + Vector3.up * upwardPush).normalized;
            body.AddForce(pushDirection * rigidbodyImpulse, ForceMode.Impulse);
            return true;
        }

        private void UpdateAnimation()
        {
            if (bladeVisual != null && isActive)
            {
                bladeVisual.Rotate(Vector3.forward, bladeSpinDegreesPerSecond * Time.deltaTime, Space.Self);
            }

            if (slidingVisual != null)
            {
                float slide = isActive ? Mathf.Sin(Time.time * slideFrequency * Mathf.PI * 2f) * slideAmplitude : 0f;
                slidingVisual.localPosition = slidingRestLocalPosition + Vector3.forward * slide;
            }
        }

        private void ConfigureDangerLine()
        {
            if (dangerLine == null)
            {
                return;
            }

            dangerLine.positionCount = 2;
            dangerLine.useWorldSpace = true;
            dangerLine.SetPosition(0, transform.position);
            dangerLine.SetPosition(1, transform.position + Direction * activeRange);
            dangerLine.enabled = isActive;
        }

        private void ApplyVisualState()
        {
            Color color = !isActive ? inactiveColor : IsArmed ? activeColor : warningColor;
            if (dangerLine != null)
            {
                dangerLine.startColor = color;
                dangerLine.endColor = new Color(color.r, color.g, color.b, 0.35f);
            }

            if (warningRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            warningRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            propertyBlock.SetColor("_EmissionColor", isActive ? color * 0.35f : Color.black);
            warningRenderer.SetPropertyBlock(propertyBlock);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 direction = transform.TransformDirection(localDirection.sqrMagnitude > 0.0001f ? localDirection.normalized : Vector3.forward);
            Gizmos.color = startsActive ? Color.red : Color.gray;
            Gizmos.DrawLine(transform.position, transform.position + direction * activeRange);
            if (castRadius > 0f)
            {
                Gizmos.DrawWireSphere(transform.position, castRadius);
                Gizmos.DrawWireSphere(transform.position + direction * activeRange, castRadius);
            }
        }

        private void OnValidate()
        {
            activeRange = Mathf.Max(0.1f, activeRange);
            castRadius = Mathf.Max(0f, castRadius);
            warningDelay = Mathf.Max(0f, warningDelay);
            hitCooldown = Mathf.Max(0f, hitCooldown);
            activeDuration = Mathf.Max(0.05f, activeDuration);
            inactiveDuration = Mathf.Max(0.05f, inactiveDuration);
            playerKnockbackDistance = Mathf.Max(0f, playerKnockbackDistance);
            rigidbodyImpulse = Mathf.Max(0f, rigidbodyImpulse);
            upwardPush = Mathf.Max(0f, upwardPush);
            bladeSpinDegreesPerSecond = Mathf.Max(0f, bladeSpinDegreesPerSecond);
            slideAmplitude = Mathf.Max(0f, slideAmplitude);
            slideFrequency = Mathf.Max(0f, slideFrequency);
        }
    }
}
