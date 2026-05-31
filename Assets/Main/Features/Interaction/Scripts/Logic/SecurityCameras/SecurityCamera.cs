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
        private bool isAttached;

        private void Awake()
        {
            pickupable = GetComponent<Pickupable>();
            body = GetComponent<Rigidbody>();

            if (eye == null)
            {
                eye = transform;
            }

            ConfigureSightBeam();
        }

        private void Update()
        {
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

            sightBeam.localPosition = new Vector3(0f, 0f, viewDistance * 0.5f);
            sightBeam.localRotation = Quaternion.identity;
            sightBeam.localScale = new Vector3(0.04f, 0.04f, viewDistance);
            sightBeam.gameObject.SetActive(isAttached && (pickupable == null || !pickupable.IsHeld));
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
            scanInterval = Mathf.Max(0.02f, scanInterval);
            alertRadius = Mathf.Max(0f, alertRadius);
            noiseLifetime = Mathf.Max(0.02f, noiseLifetime);
            alertCooldown = Mathf.Max(0f, alertCooldown);
        }
    }
}
