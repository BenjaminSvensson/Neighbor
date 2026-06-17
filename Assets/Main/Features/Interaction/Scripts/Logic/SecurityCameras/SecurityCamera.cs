using System.Collections.Generic;
using Neighbor.Main.Features.Neighbor;
using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Pickupable))]
    public sealed class SecurityCamera : MonoBehaviour, IPrimaryUseInteractable, IPickupLifecycleReceiver
    {
        private const int MaximumNeighborPlacedCameras = 5;
        private static readonly HashSet<SecurityCamera> NeighborPlacedCameras = new();

        [Header("Wall Attachment")]
        [SerializeField, Min(0.1f)] private float attachRange = 3.2f;
        [SerializeField, Min(0f)] private float wallOffset = 0.08f;
        [SerializeField, Range(0f, 1f)] private float maximumWallUpDot = 0.35f;
        [SerializeField] private LayerMask attachMask = ~0;

        [Header("Vision")]
        [SerializeField] private Transform eye;
        [SerializeField] private Transform sightBeam;
        [SerializeField, Min(0.1f)] private float viewDistance = 24f;
        [SerializeField, Range(1f, 180f)] private float viewAngle = 20f;
        [SerializeField, Min(0.02f)] private float scanInterval = 0.12f;
        [SerializeField] private LayerMask lineOfSightMask = ~0;
        [SerializeField, Min(0.02f)] private float sightOcclusionRefreshInterval = 0.08f;
        [SerializeField, Min(0f)] private float sightWallPadding = 0.08f;

        [Header("Scanning")]
        [SerializeField] private bool sweepWhenAttached = true;
        [SerializeField, Range(0f, 120f)] private float sweepAngle = 55f;
        [SerializeField, Min(0.01f)] private float sweepPeriod = 3.5f;
        [SerializeField, Range(0f, 80f)] private float downwardScanAngle = 24f;
        [SerializeField, Min(0f)] private float trackingTurnSpeed = 8f;
        [SerializeField, Min(0f)] private float trackingMemory = 2.25f;
        [SerializeField] private Color sightConeColor = new(1f, 0f, 0f, 0.22f);

        [Header("Neighbor Alert")]
        [SerializeField, Min(0f)] private float alertRadius = 35f;
        [SerializeField, Range(0f, 1f)] private float loudness = 1f;
        [SerializeField, Range(0f, 1f)] private float alertUrgency = 0.95f;
        [SerializeField, Min(0.02f)] private float noiseLifetime = 0.6f;
        [SerializeField, Min(0f)] private float alertCooldown = 1.5f;
        [SerializeField] private AudioClip alertSound;
        [SerializeField, Range(0f, 1f)] private float alertVolume = 0.9f;
        [SerializeField, Min(0f)] private float alertSoundMinDistance = 3f;
        [SerializeField, Min(0.1f)] private float alertSoundMaxDistance = 35f;

        [Header("Impact Disable")]
        [SerializeField, Min(0f)] private float minimumDisableImpactImpulse = 1.5f;
        [SerializeField, Min(0f)] private float minimumDisableRelativeSpeed = 2.5f;

        private Pickupable pickupable;
        private Rigidbody body;
        private PlayerController player;
        private float nextScanTime;
        private float nextAlertTime;
        private float trackingUntilTime;
        private float nextSightOcclusionRefreshTime;
        private Quaternion baseEyeLocalRotation;
        private Vector3 lastDetectedPosition;
        private AudioSource alertAudioSource;
        private AudioClip generatedAlertSound;
        private MeshFilter sightBeamFilter;
        private MeshRenderer sightBeamRenderer;
        private Mesh generatedSightBeamMesh;
        private Material sightBeamMaterial;
        private float configuredViewDistance = -1f;
        private float configuredViewAngle = -1f;
        private Collider[] ownColliders;
        private readonly RaycastHit[] sightOcclusionHits = new RaycastHit[16];
        private Collider[] attachedPickupableColliders;
        private Transform originalParent;
        private Pickupable attachedPickupable;
        private Vector3 attachedLocalPosition;
        private Quaternion attachedLocalRotation;
        private float blindedUntilTime;
        private bool isAttached;
        private bool isNeighborPlaced;
        private bool isDisabled;

        public bool IsBlinded => Time.time < blindedUntilTime;
        public bool IsDisabled => isDisabled;
        public bool IsNeighborPlaced => isNeighborPlaced;
        public static int NeighborPlacedCameraCount
        {
            get
            {
                NeighborPlacedCameras.RemoveWhere(camera => camera == null);
                return NeighborPlacedCameras.Count;
            }
        }
        public static bool CanPlaceNeighborCamera => NeighborPlacedCameraCount < MaximumNeighborPlacedCameras;

        public static bool IsNeighborCameraWithinDistance(Vector3 position, float distance)
        {
            NeighborPlacedCameras.RemoveWhere(camera => camera == null);
            float distanceSquared = Mathf.Max(0f, distance) * Mathf.Max(0f, distance);
            foreach (SecurityCamera camera in NeighborPlacedCameras)
            {
                if ((camera.transform.position - position).sqrMagnitude < distanceSquared)
                {
                    return true;
                }
            }

            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetNeighborPlacedCameras()
        {
            NeighborPlacedCameras.Clear();
        }

        private void Awake()
        {
            pickupable = GetComponent<Pickupable>();
            body = GetComponent<Rigidbody>();
            ownColliders = GetComponentsInChildren<Collider>();
            originalParent = transform.parent;

            if (eye == null)
            {
                eye = transform;
            }

            baseEyeLocalRotation = eye.localRotation;
            ConfigureSightBeam();
        }

        private void Update()
        {
            ConfigureSightBeam();

            if (isDisabled || IsBlinded || !isAttached || pickupable != null && pickupable.IsHeld)
            {
                trackingUntilTime = 0f;
                UpdateScanningRotation();
                return;
            }

            if (Time.time >= nextScanTime)
            {
                nextScanTime = Time.time + scanInterval;
                if (TryDetectPlayer(out Vector3 detectedPosition))
                {
                    lastDetectedPosition = GetPlayerAimPoint(player);
                    trackingUntilTime = Time.time + trackingMemory;
                    AlertNeighbor(detectedPosition);
                }
            }

            UpdateScanningRotation();
        }

        private void FixedUpdate()
        {
            MaintainPickupableAttachment();
        }

        private void LateUpdate()
        {
            MaintainPickupableAttachment();
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

            Pickupable hitPickupable = hit.collider.GetComponentInParent<Pickupable>();
            bool attachesToOtherPickupable = hitPickupable != null && hitPickupable != pickupable;
            if (!attachesToOtherPickupable && Mathf.Abs(Vector3.Dot(hit.normal.normalized, Vector3.up)) > maximumWallUpDot)
            {
                return;
            }

            Quaternion rotation = Quaternion.LookRotation(hit.normal.normalized, Vector3.up);
            Vector3 position = hit.point + hit.normal.normalized * wallOffset;
            pickupable.Place(position, rotation, true);
            interactor.ForgetHeldPickup(pickupable);
            AttachToSurface(attachesToOtherPickupable ? hitPickupable : null);
        }

        public void OnPickupStarted(Pickupable _, PlayerInteractor __)
        {
            DetachFromSurface(true);
            ResetEyeRotation();
            trackingUntilTime = 0f;
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryDisableFromThrownImpact(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            TryDisableFromThrownImpact(collision);
        }

        private void DetachFromSurface(bool releasePhysics)
        {
            isAttached = false;
            MarkNoLongerNeighborPlaced();
            RestoreAttachedPickupableCollision();
            attachedPickupable = null;
            transform.SetParent(originalParent, true);

            if (!releasePhysics)
            {
                return;
            }

            if (body == null)
            {
                return;
            }

            body.constraints = RigidbodyConstraints.None;
            body.isKinematic = false;
            body.useGravity = true;
        }

        private void TryDisableFromThrownImpact(Collision collision)
        {
            if (isDisabled || collision == null || collision.contactCount == 0)
            {
                return;
            }

            Pickupable thrownPickup = collision.collider != null
                ? collision.collider.GetComponentInParent<Pickupable>()
                : null;
            if (thrownPickup == null || thrownPickup == pickupable || !thrownPickup.IsRecentlyThrown)
            {
                return;
            }

            float impulse = collision.impulse.magnitude;
            float relativeSpeed = collision.relativeVelocity.magnitude;
            if (impulse < minimumDisableImpactImpulse && relativeSpeed < minimumDisableRelativeSpeed)
            {
                return;
            }

            DisableFromImpact();
        }

        private void DisableFromImpact()
        {
            isDisabled = true;
            trackingUntilTime = 0f;
            DetachFromSurface(true);
            ResetEyeRotation();
            ConfigureSightBeam();
        }

        public void OnPickupPlaced(Pickupable _)
        {
        }

        public void BlindFor(float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            blindedUntilTime = Mathf.Max(blindedUntilTime, Time.time + duration);
            nextScanTime = blindedUntilTime;
            ConfigureSightBeam();
        }

        public bool TryAttachByNeighbor(Vector3 wallPosition, Vector3 wallNormal)
        {
            if (!CanPlaceNeighborCamera || pickupable == null || wallNormal.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            Vector3 normalizedWallNormal = wallNormal.normalized;
            transform.SetPositionAndRotation(
                wallPosition + normalizedWallNormal * wallOffset,
                Quaternion.LookRotation(normalizedWallNormal, Vector3.up));
            pickupable.Place(transform.position, transform.rotation, true);
            AttachToSurface(null);
            isNeighborPlaced = true;
            NeighborPlacedCameras.Add(this);
            return true;
        }

        private void AttachToSurface(Pickupable parentPickupable)
        {
            RestoreAttachedPickupableCollision();
            isAttached = true;
            isDisabled = false;
            attachedPickupable = parentPickupable;
            transform.SetParent(attachedPickupable != null ? attachedPickupable.transform : originalParent, true);
            attachedLocalPosition = transform.localPosition;
            attachedLocalRotation = transform.localRotation;
            IgnoreAttachedPickupableCollision();
            baseEyeLocalRotation = eye != null ? eye.localRotation : Quaternion.identity;

            if (body == null)
            {
                return;
            }

            RigidbodyVelocityUtility.ClearIfDynamic(body);
            body.isKinematic = true;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeAll;
            body.Sleep();
        }

        private void MaintainPickupableAttachment()
        {
            if (!isAttached || attachedPickupable == null)
            {
                return;
            }

            Transform attachmentParent = attachedPickupable.transform;
            if (transform.parent != attachmentParent)
            {
                transform.SetParent(attachmentParent, false);
            }

            transform.localPosition = attachedLocalPosition;
            transform.localRotation = attachedLocalRotation;

            if (body == null)
            {
                return;
            }

            body.position = transform.position;
            body.rotation = transform.rotation;
            body.Sleep();
        }

        private void IgnoreAttachedPickupableCollision()
        {
            if (attachedPickupable == null || ownColliders == null)
            {
                attachedPickupableColliders = null;
                return;
            }

            attachedPickupableColliders = attachedPickupable.GetComponentsInChildren<Collider>();
            SetAttachedPickupableCollisionIgnored(true);
        }

        private void RestoreAttachedPickupableCollision()
        {
            SetAttachedPickupableCollisionIgnored(false);
            attachedPickupableColliders = null;
        }

        private void SetAttachedPickupableCollisionIgnored(bool ignored)
        {
            if (ownColliders == null || attachedPickupableColliders == null)
            {
                return;
            }

            for (int i = 0; i < ownColliders.Length; i++)
            {
                Collider ownCollider = ownColliders[i];
                if (ownCollider == null)
                {
                    continue;
                }

                for (int j = 0; j < attachedPickupableColliders.Length; j++)
                {
                    Collider attachedCollider = attachedPickupableColliders[j];
                    if (attachedCollider == null || attachedCollider == ownCollider)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(ownCollider, attachedCollider, ignored);
                }
            }
        }

        private void UpdateScanningRotation()
        {
            if (eye == null || !isAttached || pickupable != null && pickupable.IsHeld)
            {
                return;
            }

            Quaternion targetRotation;
            if (Time.time < trackingUntilTime)
            {
                Vector3 trackingDirection = lastDetectedPosition - eye.position;
                if (trackingDirection.sqrMagnitude > 0.001f)
                {
                    Quaternion worldTarget = Quaternion.LookRotation(trackingDirection.normalized, Vector3.up);
                    targetRotation = eye.parent != null
                        ? Quaternion.Inverse(eye.parent.rotation) * worldTarget
                        : worldTarget;
                    eye.localRotation = Quaternion.Slerp(
                        eye.localRotation,
                        targetRotation,
                        Time.deltaTime * trackingTurnSpeed);
                    return;
                }
            }

            Quaternion downwardRotation = baseEyeLocalRotation * Quaternion.Euler(downwardScanAngle, 0f, 0f);
            if (!sweepWhenAttached || sweepAngle <= 0f)
            {
                eye.localRotation = Quaternion.Slerp(
                    eye.localRotation,
                    downwardRotation,
                    Time.deltaTime * trackingTurnSpeed);
                return;
            }

            float sweep = Mathf.Sin(Time.time / Mathf.Max(0.01f, sweepPeriod) * Mathf.PI * 2f) * sweepAngle * 0.5f;
            targetRotation = baseEyeLocalRotation * Quaternion.Euler(downwardScanAngle, sweep, 0f);
            eye.localRotation = Quaternion.Slerp(
                eye.localRotation,
                targetRotation,
                Time.deltaTime * trackingTurnSpeed);
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
            PlayerFeedbackEvents.ReportCameraDetection();
            AdaptiveSecurityDirector.ReportCameraDetection();
            PlayAlertSound();

            GameObject noiseObject = new GameObject("SecurityCameraAlertNoiseEvent");
            noiseObject.transform.position = detectedPosition;

            SphereCollider sphereCollider = noiseObject.AddComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
            sphereCollider.radius = alertRadius;

            Rigidbody noiseBody = noiseObject.AddComponent<Rigidbody>();
            noiseBody.isKinematic = true;
            noiseBody.useGravity = false;

            NoiseEvent noiseEvent = noiseObject.AddComponent<NoiseEvent>();
            noiseEvent.Initialize(detectedPosition, alertRadius, loudness, gameObject, noiseLifetime, alertUrgency);
        }

        private void PlayAlertSound()
        {
            if (alertVolume <= 0f)
            {
                return;
            }

            if (alertAudioSource == null)
            {
                alertAudioSource = gameObject.AddComponent<AudioSource>();
                alertAudioSource.playOnAwake = false;
                alertAudioSource.loop = false;
                alertAudioSource.spatialBlend = 1f;
                alertAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            }

            alertAudioSource.minDistance = alertSoundMinDistance;
            alertAudioSource.maxDistance = alertSoundMaxDistance;
            alertAudioSource.PlayOneShot(alertSound != null ? alertSound : GetGeneratedAlertSound(), alertVolume);
        }

        private AudioClip GetGeneratedAlertSound()
        {
            if (generatedAlertSound != null)
            {
                return generatedAlertSound;
            }

            const int sampleRate = 44100;
            const float duration = 0.42f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float pulse = Mathf.Sin(time * Mathf.PI * 2f * 4f) > 0f ? 1f : 0.18f;
                float envelope = Mathf.Min(1f, time * 30f) * Mathf.Clamp01((duration - time) * 12f);
                samples[i] = Mathf.Sin(time * Mathf.PI * 2f * 1120f) * pulse * envelope * 0.35f;
            }

            generatedAlertSound = AudioClip.Create("GeneratedSecurityCameraAlert", sampleCount, 1, sampleRate, false);
            generatedAlertSound.SetData(samples, 0);
            return generatedAlertSound;
        }

        private void ConfigureSightBeam()
        {
            if (sightBeam == null)
            {
                return;
            }

            sightBeam.localPosition = Vector3.zero;
            sightBeam.localRotation = Quaternion.identity;
            sightBeam.localScale = GetInverseWorldScale(sightBeam.parent);
            sightBeam.gameObject.SetActive(!IsBlinded && isAttached && (pickupable == null || !pickupable.IsHeld));

            sightBeamFilter ??= sightBeam.GetComponent<MeshFilter>();
            sightBeamRenderer ??= sightBeam.GetComponent<MeshRenderer>();
            if (sightBeamFilter != null && (configuredViewDistance != viewDistance || configuredViewAngle != viewAngle))
            {
                if (generatedSightBeamMesh != null)
                {
                    Destroy(generatedSightBeamMesh);
                }

                generatedSightBeamMesh = CreateSightConeMesh(viewDistance, viewAngle, 28);
                sightBeamFilter.sharedMesh = generatedSightBeamMesh;
                configuredViewDistance = viewDistance;
                configuredViewAngle = viewAngle;
            }

            if (sightBeamFilter != null
                && generatedSightBeamMesh != null
                && sightBeam.gameObject.activeInHierarchy
                && Time.time >= nextSightOcclusionRefreshTime)
            {
                nextSightOcclusionRefreshTime = Time.time + sightOcclusionRefreshInterval;
                UpdateSightConeOcclusion(generatedSightBeamMesh, viewDistance, viewAngle, 28);
            }

            if (sightBeamRenderer != null)
            {
                sightBeamMaterial ??= CreateSightConeMaterial();
                sightBeamRenderer.sharedMaterial = sightBeamMaterial;
            }
        }

        private static Vector3 GetInverseWorldScale(Transform parent)
        {
            if (parent == null)
            {
                return Vector3.one;
            }

            Vector3 scale = parent.lossyScale;
            return new Vector3(
                Mathf.Abs(scale.x) > 0.0001f ? 1f / Mathf.Abs(scale.x) : 1f,
                Mathf.Abs(scale.y) > 0.0001f ? 1f / Mathf.Abs(scale.y) : 1f,
                Mathf.Abs(scale.z) > 0.0001f ? 1f / Mathf.Abs(scale.z) : 1f);
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

        private void UpdateSightConeOcclusion(Mesh mesh, float distance, float angle, int segments)
        {
            Vector3[] vertices = mesh.vertices;
            if (vertices.Length != segments + 2)
            {
                return;
            }

            vertices[0] = Vector3.zero;
            float radiusAtOneMeter = Mathf.Tan(angle * 0.5f * Mathf.Deg2Rad);
            Vector3 origin = EyePosition;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments * Mathf.PI * 2f;
                Vector3 localDirection = new Vector3(
                    Mathf.Cos(t) * radiusAtOneMeter,
                    Mathf.Sin(t) * radiusAtOneMeter,
                    1f).normalized;
                Vector3 worldDirection = sightBeam.TransformDirection(localDirection).normalized;
                float visibleDistance = FindSightObstacleDistance(origin, worldDirection, distance);
                vertices[i + 1] = localDirection * visibleDistance;
            }

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
        }

        private float FindSightObstacleDistance(Vector3 origin, Vector3 direction, float maximumDistance)
        {
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                sightOcclusionHits,
                maximumDistance,
                lineOfSightMask,
                QueryTriggerInteraction.Ignore);
            float nearestDistance = maximumDistance;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = sightOcclusionHits[i];
                if (hit.collider == null || IsOwnCollider(hit.collider))
                {
                    continue;
                }

                nearestDistance = Mathf.Min(nearestDistance, Mathf.Max(0f, hit.distance - sightWallPadding));
            }

            return nearestDistance;
        }

        private bool IsOwnCollider(Collider candidate)
        {
            if (ownColliders == null)
            {
                return false;
            }

            for (int i = 0; i < ownColliders.Length; i++)
            {
                if (ownColliders[i] == candidate)
                {
                    return true;
                }
            }

            return false;
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
            material.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
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

        private void OnDestroy()
        {
            MarkNoLongerNeighborPlaced();

            if (generatedSightBeamMesh != null)
            {
                Destroy(generatedSightBeamMesh);
            }

            if (sightBeamMaterial != null)
            {
                Destroy(sightBeamMaterial);
            }

            if (generatedAlertSound != null)
            {
                Destroy(generatedAlertSound);
            }
        }

        private void OnValidate()
        {
            attachRange = Mathf.Max(0.1f, attachRange);
            wallOffset = Mathf.Max(0f, wallOffset);
            viewDistance = Mathf.Max(0.1f, viewDistance);
            viewAngle = Mathf.Clamp(viewAngle, 1f, 180f);
            scanInterval = Mathf.Max(0.02f, scanInterval);
            sightOcclusionRefreshInterval = Mathf.Max(0.02f, sightOcclusionRefreshInterval);
            sightWallPadding = Mathf.Max(0f, sightWallPadding);
            sweepAngle = Mathf.Clamp(sweepAngle, 0f, 120f);
            sweepPeriod = Mathf.Max(0.01f, sweepPeriod);
            downwardScanAngle = Mathf.Clamp(downwardScanAngle, 0f, 80f);
            trackingTurnSpeed = Mathf.Max(0f, trackingTurnSpeed);
            trackingMemory = Mathf.Max(0f, trackingMemory);
            alertRadius = Mathf.Max(0f, alertRadius);
            noiseLifetime = Mathf.Max(0.02f, noiseLifetime);
            alertCooldown = Mathf.Max(0f, alertCooldown);
            alertSoundMinDistance = Mathf.Max(0f, alertSoundMinDistance);
            alertSoundMaxDistance = Mathf.Max(0.1f, alertSoundMaxDistance);
            minimumDisableImpactImpulse = Mathf.Max(0f, minimumDisableImpactImpulse);
            minimumDisableRelativeSpeed = Mathf.Max(0f, minimumDisableRelativeSpeed);
        }

        private void MarkNoLongerNeighborPlaced()
        {
            isNeighborPlaced = false;
            NeighborPlacedCameras.Remove(this);
        }
    }
}
