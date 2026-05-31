using System.Collections.Generic;
using Neighbor.Main.Features.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class SwingingAxeTrap : MonoBehaviour
    {
        [Header("Swing")]
        [SerializeField] private Transform[] swingingParts;
        [SerializeField] private bool startsActive = true;
        [SerializeField] private Vector3 pivotLocalPosition = new(0f, 4.6f, 0f);
        [SerializeField, Min(0f)] private float maximumAngle = 42f;
        [SerializeField, Min(0.01f)] private float swingsPerSecond = 0.42f;
        [SerializeField, Min(0f)] private float phaseOffset;

        [Header("One Shot Decay")]
        [SerializeField] private bool dampenAfterActivation;
        [SerializeField, Min(0f)] private float swingDamping = 0.7f;
        [SerializeField, Min(0f)] private float haltAngle = 1.2f;

        [Header("Impact")]
        [SerializeField, Min(0f)] private float minimumHitAngularSpeed = 35f;
        [SerializeField, Min(0f)] private float hitCooldown = 0.45f;
        [SerializeField, Min(0f)] private float rigidbodyImpulse = 9f;
        [SerializeField, Min(0f)] private float playerPushDistance = 1.7f;
        [SerializeField, Min(0f)] private float upwardPush = 0.35f;
        [SerializeField] private bool resetSceneOnPlayerHit = true;
        [SerializeField] private bool allowRootTriggerHits;

        private readonly Dictionary<Collider, float> nextHitTimes = new();
        private PartPose[] basePoses;
        private bool isActive;
        private bool isResettingScene;
        private float activationTime;
        private float previousAngle;
        private float angularSpeed;
        private float signedAngularSpeed;

        private struct PartPose
        {
            public Transform Transform;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
        }

        private void Awake()
        {
            CacheBasePoses();
            isActive = startsActive;
            activationTime = Time.time;
        }

        private void Update()
        {
            if (!isActive)
            {
                signedAngularSpeed = 0f;
                angularSpeed = 0f;
                previousAngle = 0f;
                ApplySwing(0f);
                return;
            }

            float activeTime = Time.time - activationTime;
            float amplitude = dampenAfterActivation
                ? maximumAngle * Mathf.Exp(-swingDamping * activeTime)
                : maximumAngle;
            float angle = Mathf.Sin((activeTime + phaseOffset) * swingsPerSecond * Mathf.PI * 2f) * amplitude;
            signedAngularSpeed = Mathf.DeltaAngle(previousAngle, angle) / Mathf.Max(Time.deltaTime, 0.0001f);
            angularSpeed = Mathf.Abs(signedAngularSpeed);
            previousAngle = angle;
            ApplySwing(angle);

            if (dampenAfterActivation && amplitude <= haltAngle && Mathf.Abs(angle) <= haltAngle)
            {
                isActive = false;
                signedAngularSpeed = 0f;
                angularSpeed = 0f;
                previousAngle = 0f;
                ApplySwing(0f);
            }
        }

        public void Activate()
        {
            if (isActive)
            {
                return;
            }

            isActive = true;
            activationTime = Time.time;
            previousAngle = 0f;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!allowRootTriggerHits)
            {
                return;
            }

            TryHit(other);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!allowRootTriggerHits)
            {
                return;
            }

            TryHit(other);
        }

        public void HitFromAxe(Collider other)
        {
            TryHit(other);
        }

        private void TryHit(Collider other)
        {
            if (!isActive || other == null || other.isTrigger || angularSpeed < minimumHitAngularSpeed)
            {
                return;
            }

            if (nextHitTimes.TryGetValue(other, out float nextHitTime) && Time.time < nextHitTime)
            {
                return;
            }

            nextHitTimes[other] = Time.time + hitCooldown;

            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player != null && resetSceneOnPlayerHit)
            {
                ResetScene();
                return;
            }

            Vector3 pushDirection = GetPushDirection(other.transform.position);
            Rigidbody body = other.attachedRigidbody;
            if (body != null && !body.isKinematic)
            {
                body.AddForce((pushDirection + Vector3.up * upwardPush).normalized * rigidbodyImpulse, ForceMode.Impulse);
                return;
            }

            CharacterController controller = player != null
                ? player.GetComponent<CharacterController>()
                : other.GetComponentInParent<CharacterController>();
            if (controller != null)
            {
                controller.Move((pushDirection + Vector3.up * upwardPush).normalized * playerPushDistance);
            }
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

        private Vector3 GetPushDirection(Vector3 hitPosition)
        {
            Vector3 pivotWorld = transform.TransformPoint(pivotLocalPosition);
            Vector3 radialDirection = hitPosition - pivotWorld;
            radialDirection.y = 0f;

            if (radialDirection.sqrMagnitude < 0.0001f)
            {
                radialDirection = transform.right;
            }

            float swingSign = Mathf.Sign(signedAngularSpeed);
            Vector3 tangent = Vector3.Cross(Vector3.up, radialDirection.normalized) * swingSign;
            return tangent.sqrMagnitude > 0.0001f ? tangent.normalized : transform.right;
        }

        private void CacheBasePoses()
        {
            if (swingingParts == null || swingingParts.Length == 0)
            {
                swingingParts = new[] { transform };
            }

            basePoses = new PartPose[swingingParts.Length];
            for (int i = 0; i < swingingParts.Length; i++)
            {
                Transform part = swingingParts[i];
                basePoses[i] = new PartPose
                {
                    Transform = part,
                    LocalPosition = part != null ? part.localPosition : Vector3.zero,
                    LocalRotation = part != null ? part.localRotation : Quaternion.identity
                };
            }
        }

        private void ApplySwing(float angle)
        {
            if (basePoses == null)
            {
                return;
            }

            Quaternion swingRotation = Quaternion.AngleAxis(angle, Vector3.forward);
            for (int i = 0; i < basePoses.Length; i++)
            {
                PartPose pose = basePoses[i];
                if (pose.Transform == null)
                {
                    continue;
                }

                Vector3 offsetFromPivot = pose.LocalPosition - pivotLocalPosition;
                pose.Transform.localPosition = pivotLocalPosition + swingRotation * offsetFromPivot;
                pose.Transform.localRotation = swingRotation * pose.LocalRotation;
            }
        }

        private void OnValidate()
        {
            maximumAngle = Mathf.Max(0f, maximumAngle);
            swingsPerSecond = Mathf.Max(0.01f, swingsPerSecond);
            swingDamping = Mathf.Max(0f, swingDamping);
            haltAngle = Mathf.Max(0f, haltAngle);
        }
    }
}
