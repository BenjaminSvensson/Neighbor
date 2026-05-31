using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Pickupable))]
    public sealed class SecurityCamera : MonoBehaviour, IPrimaryUseInteractable, IPickupLifecycleReceiver
    {
        [Header("Wall Attachment")]
        [SerializeField, Min(0.1f)] private float attachRange = 3.2f;
        [SerializeField, Min(0f)] private float wallOffset = 0.08f;
        [SerializeField, Range(0f, 1f)] private float maximumWallUpDot = 0.35f;
        [SerializeField] private LayerMask attachMask = ~0;

        [Header("Vision")]
        [SerializeField] private Transform eye;
        [SerializeField] private Transform sightBeam;
        [SerializeField, Min(0.1f)] private float viewDistance = 9f;
        [SerializeField, Range(1f, 180f)] private float viewAngle = 70f;
        [SerializeField, Min(0.02f)] private float scanInterval = 0.12f;
        [SerializeField] private LayerMask lineOfSightMask = ~0;

        [Header("Scanning")]
        [SerializeField] private bool sweepWhenAttached = true;
        [SerializeField, Range(0f, 120f)] private float sweepAngle = 55f;
        [SerializeField, Min(0.01f)] private float sweepPeriod = 3.5f;
        [SerializeField] private Color sightConeColor = new(1f, 0f, 0f, 0.22f);

        [Header("Neighbor Alert")]
        [SerializeField, Min(0f)] private float alertRadius = 18f;
        [SerializeField, Range(0f, 1f)] private float loudness = 1f;
        [SerializeField, Min(0.02f)] private float noiseLifetime = 0.6f;
        [SerializeField, Min(0f)] private float alertCooldown = 1.5f;

        private Pickupable pickupable;
        private Rigidbody body;
        private PlayerController player;
        private float nextScanTime;
        private float nextAlertTime;
        private Quaternion baseEyeLocalRotation;
        private MeshFilter sightBeamFilter;
        private MeshRenderer sightBeamRenderer;
        private Material sightBeamMaterial;
        private float configuredViewDistance = -1f;
        private float configuredViewAngle = -1f;
        private bool isAttached;

        private void Awake()
        {
            pickupable = GetComponent<Pickupable>();
            body = GetComponent<Rigidbody>();

            if (eye == null)
            {
                eye = transform;
            }

            baseEyeLocalRotation = eye.localRotation;
            ConfigureSightBeam();
        }

        private void Update()
        {
            UpdateScanningRotation();
            ConfigureSightBeam();

            if (!isAttached || pickupable != null && pickupable.IsHeld || Time.time < nextScanTime)
            {
                return;
            }

            nextScanTime = Time.time + scanInterval;
            if (TryDetectPlayer(out Vector3 detectedPosition))
            {
                AlertNeighbor(detectedPosition);
            }
        }

        public bool CanPrimaryUse(PlayerInteractor interactor)
        {
            return interactor != null && pickupable != null && pickupable.IsHeld;
        }

        public void PrimaryUse(PlayerInteractor interactor)
        {
            if (interactor == null || pickupable == null)
            {
                return;
            }

            Ray ray = new Ray(interactor.ViewTransform.position, interactor.ViewTransform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, attachRange, attachMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            if (Mathf.Abs(Vector3.Dot(hit.normal.normalized, Vector3.up)) > maximumWallUpDot)
            {
                return;
            }

            Quaternion rotation = Quaternion.LookRotation(hit.normal.normalized, Vector3.up);
            Vector3 position = hit.point + hit.normal.normalized * wallOffset;
            pickupable.Place(position, rotation, true);
            interactor.ForgetHeldPickup(pickupable);
            AttachToWall();
        }

        public void OnPickupStarted(Pickupable _, PlayerInteractor __)
        {
            isAttached = false;
            ResetEyeRotation();

            if (body == null)
            {
                return;
            }

            body.constraints = RigidbodyConstraints.None;
            body.isKinematic = false;
            body.useGravity = true;
        }

        public void OnPickupPlaced(Pickupable _)
        {
        }

        private void AttachToWall()
        {
            isAttached = true;
            baseEyeLocalRotation = eye != null ? eye.localRotation : Quaternion.identity;

            if (body == null)
            {
                return;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeAll;
            body.Sleep();
        }

        private void UpdateScanningRotation()
        {
            if (eye == null || !isAttached || pickupable != null && pickupable.IsHeld)
            {
                return;
            }

            if (!sweepWhenAttached || sweepAngle <= 0f)
            {
                eye.localRotation = baseEyeLocalRotation;
                return;
            }

            float sweep = Mathf.Sin(Time.time / Mathf.Max(0.01f, sweepPeriod) * Mathf.PI * 2f) * sweepAngle * 0.5f;
            eye.localRotation = baseEyeLocalRotation * Quaternion.Euler(0f, sweep, 0f);
        }

        private void ResetEyeRotation()
        {
            if (eye != null)
            {
                eye.localRotation = baseEyeLocalRotation;
            }
        }

        private bool TryDetectPlayer(out Vector3 detectedPosition)
        {
            ResolvePlayer();
            detectedPosition = default;
            if (player == null)
            {
                return false;
            }

            PlayerHidingState hidingState = player.GetComponent<PlayerHidingState>() ?? player.GetComponentInChildren<PlayerHidingState>();
            if (hidingState != null && hidingState.IsHidden)
            {
                return false;
            }

            Vector3 origin = EyePosition;
            Vector3 target = GetPlayerAimPoint(player);
            Vector3 toTarget = target - origin;
            float distance = toTarget.magnitude;
            if (distance > viewDistance || distance <= 0.01f)
            {
                return false;
            }

            Vector3 direction = toTarget / distance;
            if (Vector3.Angle(eye != null ? eye.forward : transform.forward, direction) > viewAngle * 0.5f)
            {
                return false;
            }

            if (Physics.Raycast(origin, direction, out RaycastHit hit, distance, lineOfSightMask, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(player.transform)
                && hit.transform.root != player.transform.root)
            {
                return false;
            }

            detectedPosition = player.transform.position;
            return true;
        }

        private void AlertNeighbor(Vector3 detectedPosition)
        {
            if (Time.time < nextAlertTime || alertRadius <= 0f || loudness <= 0f)
            {
                return;
            }

            nextAlertTime = Time.time + alertCooldown;

            GameObject noiseObject = new GameObject("SecurityCameraAlertNoiseEvent");
            noiseObject.transform.position = detectedPosition;

            SphereCollider sphereCollider = noiseObject.AddComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
            sphereCollider.radius = alertRadius;

            Rigidbody noiseBody = noiseObject.AddComponent<Rigidbody>();
            noiseBody.isKinematic = true;
            noiseBody.useGravity = false;

            NoiseEvent noiseEvent = noiseObject.AddComponent<NoiseEvent>();
            noiseEvent.Initialize(detectedPosition, alertRadius, loudness, gameObject, noiseLifetime);
        }

        private void ConfigureSightBeam()
        {
            if (sightBeam == null)
            {
                return;
            }

            sightBeam.localPosition = Vector3.zero;
            sightBeam.localRotation = Quaternion.identity;
            sightBeam.localScale = Vector3.one;
            sightBeam.gameObject.SetActive(isAttached && (pickupable == null || !pickupable.IsHeld));

            sightBeamFilter ??= sightBeam.GetComponent<MeshFilter>();
            sightBeamRenderer ??= sightBeam.GetComponent<MeshRenderer>();
            if (sightBeamFilter != null && (configuredViewDistance != viewDistance || configuredViewAngle != viewAngle))
            {
                sightBeamFilter.sharedMesh = CreateSightConeMesh(viewDistance, viewAngle, 28);
                configuredViewDistance = viewDistance;
                configuredViewAngle = viewAngle;
            }

            if (sightBeamRenderer != null)
            {
                sightBeamMaterial ??= CreateSightConeMaterial();
                sightBeamRenderer.sharedMaterial = sightBeamMaterial;
            }
        }

        private Mesh CreateSightConeMesh(float distance, float angle, int segments)
        {
            Mesh mesh = new Mesh
            {
                name = "SecurityCameraSightCone"
            };

            float radius = Mathf.Tan(angle * 0.5f * Mathf.Deg2Rad) * distance;
            Vector3[] vertices = new Vector3[segments + 2];
            int[] triangles = new int[segments * 6];
            vertices[0] = Vector3.zero;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius, distance);
            }

            int triangleIndex = 0;
            for (int i = 0; i < segments; i++)
            {
                triangles[triangleIndex++] = 0;
                triangles[triangleIndex++] = i + 1;
                triangles[triangleIndex++] = i + 2;

                triangles[triangleIndex++] = 0;
                triangles[triangleIndex++] = i + 2;
                triangles[triangleIndex++] = i + 1;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private Material CreateSightConeMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            Material material = new Material(shader)
            {
                color = sightConeColor
            };

            material.SetColor("_BaseColor", sightConeColor);
            material.SetColor("_Color", sightConeColor);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHABLEND_ON");
            return material;
        }

        private Vector3 EyePosition => eye != null ? eye.position : transform.position;

        private static Vector3 GetPlayerAimPoint(PlayerController targetPlayer)
        {
            CharacterController controller = targetPlayer.GetComponent<CharacterController>() ?? targetPlayer.GetComponentInChildren<CharacterController>();
            return controller != null ? controller.bounds.center : targetPlayer.transform.position + Vector3.up;
        }

        private void ResolvePlayer()
        {
            if (player != null)
            {
                return;
            }

            player = FindAnyObjectByType<PlayerController>();
        }

        private void OnValidate()
        {
            attachRange = Mathf.Max(0.1f, attachRange);
            wallOffset = Mathf.Max(0f, wallOffset);
            viewDistance = Mathf.Max(0.1f, viewDistance);
            viewAngle = Mathf.Clamp(viewAngle, 1f, 180f);
            scanInterval = Mathf.Max(0.02f, scanInterval);
            sweepAngle = Mathf.Clamp(sweepAngle, 0f, 120f);
            sweepPeriod = Mathf.Max(0.01f, sweepPeriod);
            alertRadius = Mathf.Max(0f, alertRadius);
            noiseLifetime = Mathf.Max(0.02f, noiseLifetime);
            alertCooldown = Mathf.Max(0f, alertCooldown);
        }
    }
}
