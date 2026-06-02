using Neighbor.Main.Features.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        [SerializeField] private bool resetSceneOnPlayerHit = true;
        [SerializeField, Min(0f)] private float playerKnockbackDistance = 1.8f;
        [SerializeField, Min(0f)] private float rigidbodyImpulse = 7f;
        [SerializeField, Min(0f)] private float upwardPush = 0.25f;

        [Header("Visuals")]
        [SerializeField] private Transform bladeVisual;
        [SerializeField] private Transform slidingVisual;
        [SerializeField] private LineRenderer dangerLine;
        [SerializeField] private Renderer warningRenderer;
        [SerializeField, Min(0f)] private float bladeSpinDegreesPerSecond = 720f;
        [SerializeField] private Vector3 bladeSpinAxis = Vector3.up;
        [SerializeField, Min(0f)] private float pathTravelDistance;
        [SerializeField, Min(0f)] private float pathTravelSpeed = 2.2f;
        [SerializeField, Min(0f)] private float slideAmplitude = 0.04f;
        [SerializeField, Min(0f)] private float slideFrequency = 6f;
        [SerializeField] private Color activeColor = new(1f, 0.08f, 0.04f, 1f);
        [SerializeField] private Color warningColor = new(1f, 0.75f, 0.06f, 1f);
        [SerializeField] private Color inactiveColor = new(0.25f, 0.25f, 0.25f, 1f);

        private readonly Collider[] hitColliders = new Collider[16];
        private MaterialPropertyBlock propertyBlock;
        private Vector3 slidingRestLocalPosition;
        private Vector3 previousBladeHitCenter;
        private float cycleStateStartTime;
        private float armedAtTime;
        private float nextHitTime;
        private bool isResettingScene;
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
            previousBladeHitCenter = GetBladeHitCenter();
        }

        private void Update()
        {
            Vector3 previousHitCenter = previousBladeHitCenter;
            UpdateCycle();
            UpdateAnimation();
            Vector3 currentHitCenter = GetBladeHitCenter();
            ConfigureDangerLine();
            ApplyVisualState();

            if (IsArmed)
            {
                CheckBladeSweep(previousHitCenter, currentHitCenter);
            }

            previousBladeHitCenter = currentHitCenter;
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

        private void CheckBladeSweep(Vector3 previousCenter, Vector3 currentCenter)
        {
            if (Time.time < nextHitTime)
            {
                return;
            }

            float hitRadius = Mathf.Max(0.01f, castRadius);
            int hitCount = Physics.OverlapCapsuleNonAlloc(
                previousCenter,
                currentCenter,
                hitRadius,
                hitColliders,
                detectionMask,
                triggerInteraction);

            Collider bestHit = null;
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = hitColliders[i];
                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (!IsValidTriggerCollider(hit))
                {
                    continue;
                }

                bestHit = hit;
                break;
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

        private bool IsValidTriggerCollider(Collider hit)
        {
            if (hit.GetComponentInParent<PlayerController>() != null)
            {
                return true;
            }

            if (hit.attachedRigidbody != null)
            {
                return true;
            }

            return hit.GetComponentInParent<Pickupable>() != null;
        }

        private bool TryApplyPlayerEffect(Collider hit)
        {
            PlayerController player = hit.GetComponentInParent<PlayerController>();
            if (player == null)
            {
                return false;
            }

            CharacterController controller = player.GetComponent<CharacterController>();
            if (resetSceneOnPlayerHit)
            {
                ResetScene();
                return true;
            }

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
                Vector3 spinAxis = bladeSpinAxis.sqrMagnitude > 0.0001f ? bladeSpinAxis.normalized : Vector3.up;
                bladeVisual.Rotate(spinAxis, bladeSpinDegreesPerSecond * Time.deltaTime, Space.Self);
            }

            if (slidingVisual != null)
            {
                Vector3 localMoveDirection = localDirection.sqrMagnitude > 0.0001f ? localDirection.normalized : Vector3.forward;
                float travelDistance = pathTravelDistance > 0f ? pathTravelDistance : activeRange;
                float pathPosition = isActive && travelDistance > 0f
                    ? Mathf.PingPong(Time.time * pathTravelSpeed, travelDistance)
                    : 0f;
                float wobble = isActive && slideAmplitude > 0f
                    ? Mathf.Sin(Time.time * slideFrequency * Mathf.PI * 2f) * slideAmplitude
                    : 0f;

                slidingVisual.localPosition = slidingRestLocalPosition
                    + localMoveDirection * pathPosition
                    + Vector3.up * wobble;
            }
        }

        private Vector3 GetBladeHitCenter()
        {
            if (bladeVisual != null)
            {
                return bladeVisual.position;
            }

            return slidingVisual != null ? slidingVisual.position : transform.position;
        }

        private void ResetScene()
        {
            if (isResettingScene)
            {
                return;
            }

            isResettingScene = true;
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.name);
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
                Vector3 bladeCenter = bladeVisual != null ? bladeVisual.position : transform.position;
                Gizmos.DrawWireSphere(bladeCenter, castRadius);
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
            pathTravelDistance = Mathf.Max(0f, pathTravelDistance);
            pathTravelSpeed = Mathf.Max(0f, pathTravelSpeed);
            slideAmplitude = Mathf.Max(0f, slideAmplitude);
            slideFrequency = Mathf.Max(0f, slideFrequency);
        }
    }
}
