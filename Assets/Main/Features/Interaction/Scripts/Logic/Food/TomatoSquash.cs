using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Pickupable))]
    public sealed class TomatoSquash : MonoBehaviour, IPickupInteractionOverride
    {
        [Header("Squash Trigger")]
        [SerializeField, Min(0f)] private float minimumImpactImpulse = 2.4f;
        [SerializeField, Min(0.01f)] private float fullSquashImpulse = 9f;
        [SerializeField, Min(0f)] private float minimumRelativeSpeed = 4f;

        [Header("Squashed Shape")]
        [SerializeField] private Transform tomatoVisual;
        [SerializeField] private SphereCollider roundCollider;
        [SerializeField] private BoxCollider squashedCollider;
        [SerializeField, Min(0.01f)] private float unsquashedDiameter = 0.42f;
        [SerializeField, Min(0.01f)] private float minimumSquashedThickness = 0.08f;
        [SerializeField, Min(0.01f)] private float maximumSquashedThickness = 0.16f;
        [SerializeField, Min(1f)] private float maximumSpreadMultiplier = 1.8f;

        [Header("Visual Color")]
        [SerializeField] private Renderer tomatoRenderer;
        [SerializeField] private Renderer stemRenderer;
        [SerializeField] private Color tomatoColor = new Color(0.95f, 0.06f, 0.035f, 1f);
        [SerializeField] private Color squashedColor = new Color(0.72f, 0.015f, 0.01f, 1f);
        [SerializeField] private Color stemColor = new Color(0.08f, 0.34f, 0.06f, 1f);

        private Rigidbody body;
        private Pickupable pickupable;
        private MaterialPropertyBlock tomatoPropertyBlock;
        private MaterialPropertyBlock stemPropertyBlock;
        private bool isSquashed;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            pickupable = GetComponent<Pickupable>();

            if (tomatoVisual == null)
            {
                tomatoVisual = transform;
            }

            if (roundCollider == null)
            {
                roundCollider = GetComponent<SphereCollider>();
            }

            if (squashedCollider == null)
            {
                squashedCollider = GetComponent<BoxCollider>();
            }

            if (tomatoRenderer == null)
            {
                tomatoRenderer = GetComponentInChildren<Renderer>();
            }

            SetSquashedColliderActive(false);
            ApplyColor(tomatoRenderer, ref tomatoPropertyBlock, tomatoColor);
            ApplyColor(stemRenderer, ref stemPropertyBlock, stemColor);
        }

        private void OnCollisionEnter(Collision collision)
        {
            TrySquash(collision);
        }

        public bool CanPickup(PlayerInteractor interactor)
        {
            return !isSquashed;
        }

        private void TrySquash(Collision collision)
        {
            if (isSquashed || pickupable != null && pickupable.IsHeld || collision.contactCount == 0)
            {
                return;
            }

            float impulse = collision.impulse.magnitude;
            float relativeSpeed = collision.relativeVelocity.magnitude;
            if (impulse < minimumImpactImpulse && relativeSpeed < minimumRelativeSpeed)
            {
                return;
            }

            ContactPoint contact = collision.GetContact(0);
            float squash01 = Mathf.Max(
                Mathf.InverseLerp(minimumImpactImpulse, fullSquashImpulse, impulse),
                Mathf.InverseLerp(minimumRelativeSpeed, minimumRelativeSpeed * 2f, relativeSpeed));

            Squash(contact.point, contact.normal, squash01);
        }

        private void Squash(Vector3 contactPoint, Vector3 surfaceNormal, float squash01)
        {
            isSquashed = true;

            surfaceNormal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;
            float thickness = Mathf.Lerp(maximumSquashedThickness, minimumSquashedThickness, squash01);
            float spread = Mathf.Lerp(1.2f, maximumSpreadMultiplier, squash01);

            Quaternion landingRotation = GetLandingRotation(surfaceNormal);
            transform.SetPositionAndRotation(contactPoint + surfaceNormal * (thickness * 0.5f), landingRotation);

            if (tomatoVisual != null)
            {
                tomatoVisual.localPosition = Vector3.zero;
                tomatoVisual.localRotation = Quaternion.identity;
                tomatoVisual.localScale = new Vector3(unsquashedDiameter * spread, thickness, unsquashedDiameter * spread);
            }

            if (roundCollider != null)
            {
                roundCollider.enabled = false;
            }

            if (squashedCollider != null)
            {
                squashedCollider.enabled = true;
                squashedCollider.center = Vector3.zero;
                squashedCollider.size = new Vector3(unsquashedDiameter * spread, thickness, unsquashedDiameter * spread);
            }

            if (body != null)
            {
                body.position = transform.position;
                body.rotation = transform.rotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.Sleep();
            }

            ApplyColor(tomatoRenderer, ref tomatoPropertyBlock, Color.Lerp(tomatoColor, squashedColor, squash01));
        }

        private Quaternion GetLandingRotation(Vector3 surfaceNormal)
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, surfaceNormal);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(transform.right, surfaceNormal);
            }

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.Cross(surfaceNormal, Vector3.right);
            }

            return Quaternion.LookRotation(forward.normalized, surfaceNormal);
        }

        private void SetSquashedColliderActive(bool active)
        {
            if (squashedCollider != null)
            {
                squashedCollider.enabled = active;
            }
        }

        private static void ApplyColor(Renderer targetRenderer, ref MaterialPropertyBlock propertyBlock, Color color)
        {
            if (targetRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        private void OnValidate()
        {
            fullSquashImpulse = Mathf.Max(minimumImpactImpulse + 0.01f, fullSquashImpulse);
            minimumRelativeSpeed = Mathf.Max(0f, minimumRelativeSpeed);
            unsquashedDiameter = Mathf.Max(0.01f, unsquashedDiameter);
            minimumSquashedThickness = Mathf.Max(0.01f, minimumSquashedThickness);
            maximumSquashedThickness = Mathf.Max(minimumSquashedThickness, maximumSquashedThickness);
            maximumSpreadMultiplier = Mathf.Max(1f, maximumSpreadMultiplier);
        }
    }
}
