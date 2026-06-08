using System.Collections.Generic;
using Neighbor.Main.Features.Neighbor;
using Neighbor.Main.Features.Player;
using UnityEngine;
using UnityEngine.Serialization;

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

        [Header("Wall Collision")]
        [SerializeField] private Collider[] wallCollisionColliders;
        [SerializeField] private LayerMask wallCollisionMask = ~0;
        [SerializeField] private QueryTriggerInteraction wallTriggerInteraction = QueryTriggerInteraction.Ignore;
        [SerializeField, Min(0.1f)] private float collisionStepDegrees = 1.5f;
        [SerializeField, Min(0f)] private float wallRetreatDegrees = 0.35f;
        [SerializeField, Min(0f)] private float wallBounceCooldown = 0.08f;

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
        [FormerlySerializedAs("resetSceneOnPlayerHit")]
        [SerializeField] private bool killPlayerOnHit = true;
        [SerializeField] private bool allowRootTriggerHits;

        [Header("Audio")]
        [SerializeField] private Transform audioAnchor;
        [SerializeField] private AudioSource swingLoopSource;
        [SerializeField] private AudioSource wallImpactSource;
        [SerializeField] private AudioClip swingLoopClip;
        [SerializeField] private AudioClip[] wallImpactClips;
        [SerializeField] private AudioClip[] hitImpactClips;
        [SerializeField, Range(0f, 1f)] private float swingLoopVolume = 0.42f;
        [SerializeField, Range(0f, 1f)] private float wallImpactVolume = 0.75f;
        [SerializeField, Range(0f, 1f)] private float hitImpactVolume = 0.8f;
        [SerializeField, Min(0f)] private float minimumAudibleAngularSpeed = 4f;
        [SerializeField, Min(0f)] private float audioFadeSpeed = 7f;
        [SerializeField, Min(0f)] private float audioMinDistance = 0.8f;
        [SerializeField, Min(0.1f)] private float audioMaxDistance = 18f;

        private readonly Dictionary<Collider, float> nextHitTimes = new();
        private readonly Collider[] obstructionHits = new Collider[32];
        private PartPose[] basePoses;
        private AudioClip generatedSwingLoopClip;
        private AudioClip generatedWallImpactClip;
        private AudioClip generatedHitImpactClip;
        private bool isActive;
        private float activationTime;
        private float swingPhase;
        private float swingDirection = 1f;
        private float currentAngle;
        private float previousAngle;
        private float angularSpeed;
        private float signedAngularSpeed;
        private float lastWallBounceTime = float.NegativeInfinity;

        private struct PartPose
        {
            public Transform Transform;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
        }

        private void Awake()
        {
            CacheBasePoses();
            ResolveWallCollisionColliders();
            ResolveAudioSources();
            isActive = startsActive;
            activationTime = Time.time;
            swingPhase = phaseOffset * swingsPerSecond * Mathf.PI * 2f;
            currentAngle = Mathf.Sin(swingPhase) * maximumAngle;
            previousAngle = currentAngle;
            ApplySwing(currentAngle);
        }

        private void Update()
        {
            if (!isActive)
            {
                signedAngularSpeed = 0f;
                angularSpeed = 0f;
                previousAngle = 0f;
                currentAngle = 0f;
                ApplySwing(0f);
                UpdateSwingAudio();
                return;
            }

            float activeTime = Time.time - activationTime;
            float amplitude = dampenAfterActivation
                ? maximumAngle * Mathf.Exp(-swingDamping * activeTime)
                : maximumAngle;
            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            float phaseDelta = swingsPerSecond * Mathf.PI * 2f * deltaTime * swingDirection;
            float nextPhase = swingPhase + phaseDelta;
            float targetAngle = Mathf.Sin(nextPhase) * amplitude;
            float angle = ResolveWallCollision(currentAngle, targetAngle, amplitude, ref nextPhase);

            signedAngularSpeed = Mathf.DeltaAngle(previousAngle, angle) / deltaTime;
            angularSpeed = Mathf.Abs(signedAngularSpeed);
            previousAngle = angle;
            currentAngle = angle;
            swingPhase = nextPhase;
            UpdateSwingAudio();

            if (dampenAfterActivation && amplitude <= haltAngle && Mathf.Abs(angle) <= haltAngle)
            {
                isActive = false;
                signedAngularSpeed = 0f;
                angularSpeed = 0f;
                previousAngle = 0f;
                currentAngle = 0f;
                ApplySwing(0f);
                UpdateSwingAudio();
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
            swingPhase = phaseOffset * swingsPerSecond * Mathf.PI * 2f;
            swingDirection = 1f;
            currentAngle = Mathf.Sin(swingPhase) * maximumAngle;
            previousAngle = currentAngle;
            ApplySwing(currentAngle);
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
            if (player != null && killPlayerOnHit)
            {
                PlayHitImpactSound();
                PlayerDeathController.Kill(player, transform.position);
                return;
            }

            Vector3 pushDirection = GetPushDirection(other.transform.position);
            NeighborImpactReceiver neighbor = other.GetComponentInParent<NeighborImpactReceiver>();
            if (neighbor != null)
            {
                PlayHitImpactSound();
                neighbor.ReceiveImpact(other.ClosestPoint(transform.position), pushDirection * rigidbodyImpulse, 1f);
                return;
            }

            Rigidbody body = other.attachedRigidbody;
            if (body != null && !body.isKinematic)
            {
                PlayHitImpactSound();
                body.AddForce((pushDirection + Vector3.up * upwardPush).normalized * rigidbodyImpulse, ForceMode.Impulse);
                return;
            }

            CharacterController controller = player != null
                ? player.GetComponent<CharacterController>()
                : other.GetComponentInParent<CharacterController>();
            if (controller != null)
            {
                PlayHitImpactSound();
                controller.Move((pushDirection + Vector3.up * upwardPush).normalized * playerPushDistance);
            }
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

        private float ResolveWallCollision(float fromAngle, float toAngle, float amplitude, ref float nextPhase)
        {
            float angleDelta = Mathf.DeltaAngle(fromAngle, toAngle);
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(angleDelta) / Mathf.Max(collisionStepDegrees, 0.1f)));
            float lastClearAngle = fromAngle;

            for (int i = 1; i <= steps; i++)
            {
                float testAngle = fromAngle + angleDelta * (i / (float)steps);
                ApplySwing(testAngle);

                if (!IsTouchingWall())
                {
                    lastClearAngle = testAngle;
                    continue;
                }

                float blockedAngle = testAngle;
                for (int search = 0; search < 6; search++)
                {
                    float middleAngle = Mathf.Lerp(lastClearAngle, blockedAngle, 0.5f);
                    ApplySwing(middleAngle);

                    if (IsTouchingWall())
                    {
                        blockedAngle = middleAngle;
                    }
                    else
                    {
                        lastClearAngle = middleAngle;
                    }
                }

                float stoppedAngle = Mathf.MoveTowards(lastClearAngle, fromAngle, wallRetreatDegrees);
                ApplySwing(stoppedAngle);
                BounceFromWall(stoppedAngle, amplitude, angleDelta, ref nextPhase);
                return stoppedAngle;
            }

            ApplySwing(toAngle);
            return toAngle;
        }

        private void BounceFromWall(float stoppedAngle, float amplitude, float attemptedAngleDelta, ref float nextPhase)
        {
            swingDirection *= -1f;
            nextPhase = PhaseForAngle(stoppedAngle, amplitude, -Mathf.Sign(attemptedAngleDelta));

            if (Time.time - lastWallBounceTime >= wallBounceCooldown)
            {
                lastWallBounceTime = Time.time;
                PlayWallImpactSound();
            }
        }

        private float PhaseForAngle(float angle, float amplitude, float desiredAngleDirection)
        {
            if (amplitude <= 0.0001f)
            {
                return swingPhase;
            }

            float normalizedAngle = Mathf.Clamp(angle / amplitude, -1f, 1f);
            float phaseA = Mathf.Asin(normalizedAngle);
            float phaseB = Mathf.PI - phaseA;

            if (PhaseMatchesDirection(phaseA, desiredAngleDirection))
            {
                return phaseA;
            }

            if (PhaseMatchesDirection(phaseB, desiredAngleDirection))
            {
                return phaseB;
            }

            return phaseA;
        }

        private bool PhaseMatchesDirection(float phase, float desiredAngleDirection)
        {
            if (Mathf.Abs(desiredAngleDirection) < 0.0001f)
            {
                return true;
            }

            float resultingDirection = Mathf.Sign(Mathf.Cos(phase) * swingDirection);
            return Mathf.Approximately(resultingDirection, Mathf.Sign(desiredAngleDirection));
        }

        private bool IsTouchingWall()
        {
            if (wallCollisionColliders == null)
            {
                return false;
            }

            Physics.SyncTransforms();

            for (int i = 0; i < wallCollisionColliders.Length; i++)
            {
                Collider probe = wallCollisionColliders[i];
                if (probe == null || !probe.enabled)
                {
                    continue;
                }

                int hitCount = OverlapProbe(probe);
                for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    Collider hit = obstructionHits[hitIndex];
                    obstructionHits[hitIndex] = null;

                    if (IsBlockingWall(hit))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private int OverlapProbe(Collider probe)
        {
            switch (probe)
            {
                case BoxCollider box:
                    Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, Abs(box.transform.lossyScale));
                    return Physics.OverlapBoxNonAlloc(
                        box.transform.TransformPoint(box.center),
                        halfExtents,
                        obstructionHits,
                        box.transform.rotation,
                        wallCollisionMask,
                        wallTriggerInteraction);

                case SphereCollider sphere:
                    float radius = sphere.radius * MaxAxis(Abs(sphere.transform.lossyScale));
                    return Physics.OverlapSphereNonAlloc(
                        sphere.transform.TransformPoint(sphere.center),
                        radius,
                        obstructionHits,
                        wallCollisionMask,
                        wallTriggerInteraction);

                default:
                    Bounds bounds = probe.bounds;
                    return Physics.OverlapBoxNonAlloc(
                        bounds.center,
                        bounds.extents,
                        obstructionHits,
                        Quaternion.identity,
                        wallCollisionMask,
                        wallTriggerInteraction);
            }
        }

        private bool IsBlockingWall(Collider hit)
        {
            if (hit == null || hit.transform.IsChildOf(transform))
            {
                return false;
            }

            if (hit.GetComponentInParent<PlayerController>() != null)
            {
                return false;
            }

            if (hit.GetComponentInParent<NeighborImpactReceiver>() != null)
            {
                return false;
            }

            Rigidbody body = hit.attachedRigidbody;
            return body == null || body.isKinematic;
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

        private void ResolveWallCollisionColliders()
        {
            if (wallCollisionColliders != null && wallCollisionColliders.Length > 0)
            {
                return;
            }

            SwingingAxeHitbox[] hitboxes = GetComponentsInChildren<SwingingAxeHitbox>();
            wallCollisionColliders = new Collider[hitboxes.Length];
            for (int i = 0; i < hitboxes.Length; i++)
            {
                wallCollisionColliders[i] = hitboxes[i] != null ? hitboxes[i].GetComponent<Collider>() : null;
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

        private void ResolveAudioSources()
        {
            if (audioAnchor == null && swingingParts != null && swingingParts.Length > 0)
            {
                audioAnchor = swingingParts[swingingParts.Length - 1];
            }

            if (audioAnchor == null)
            {
                audioAnchor = transform;
            }

            if (swingLoopSource == null)
            {
                swingLoopSource = audioAnchor.GetComponent<AudioSource>();
            }

            if (swingLoopSource == null)
            {
                swingLoopSource = audioAnchor.gameObject.AddComponent<AudioSource>();
            }

            if (wallImpactSource == null)
            {
                wallImpactSource = audioAnchor.gameObject.AddComponent<AudioSource>();
            }

            Configure3DSource(swingLoopSource, true);
            Configure3DSource(wallImpactSource, false);
            swingLoopSource.clip = GetSwingLoopClip();
            swingLoopSource.volume = 0f;
        }

        private void Configure3DSource(AudioSource source, bool loop)
        {
            if (source == null)
            {
                return;
            }

            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = audioMinDistance;
            source.maxDistance = audioMaxDistance;
            source.dopplerLevel = 0.65f;
        }

        private void UpdateSwingAudio()
        {
            if (swingLoopSource == null)
            {
                return;
            }

            if (swingLoopSource.clip == null)
            {
                swingLoopSource.clip = GetSwingLoopClip();
            }

            float maximumAngularSpeed = Mathf.Max(minimumAudibleAngularSpeed + 0.01f, maximumAngle * swingsPerSecond * Mathf.PI * 2f);
            float speed01 = isActive ? Mathf.InverseLerp(minimumAudibleAngularSpeed, maximumAngularSpeed, angularSpeed) : 0f;
            float targetVolume = swingLoopVolume * Mathf.SmoothStep(0f, 1f, speed01);
            swingLoopSource.volume = Mathf.MoveTowards(swingLoopSource.volume, targetVolume, audioFadeSpeed * Time.deltaTime);
            swingLoopSource.pitch = Mathf.Lerp(0.75f, 1.35f, speed01);

            if (swingLoopSource.volume > 0.001f)
            {
                if (!swingLoopSource.isPlaying)
                {
                    swingLoopSource.Play();
                }
            }
            else if (swingLoopSource.isPlaying)
            {
                swingLoopSource.Stop();
            }
        }

        private void PlayWallImpactSound()
        {
            if (wallImpactSource == null)
            {
                return;
            }

            AudioClip clip = GetWallImpactClip();
            if (clip == null)
            {
                return;
            }

            wallImpactSource.pitch = Random.Range(0.88f, 1.08f);
            wallImpactSource.PlayOneShot(clip, wallImpactVolume);
        }

        private void PlayHitImpactSound()
        {
            if (wallImpactSource == null)
            {
                return;
            }

            AudioClip clip = GetHitImpactClip();
            if (clip == null)
            {
                return;
            }

            wallImpactSource.pitch = Random.Range(0.92f, 1.12f);
            wallImpactSource.PlayOneShot(clip, hitImpactVolume);
        }

        private AudioClip GetSwingLoopClip()
        {
            if (swingLoopClip != null)
            {
                return swingLoopClip;
            }

            if (generatedSwingLoopClip == null)
            {
                generatedSwingLoopClip = CreateGeneratedSwingLoopClip();
            }

            return generatedSwingLoopClip;
        }

        private AudioClip GetWallImpactClip()
        {
            if (wallImpactClips != null && wallImpactClips.Length > 0)
            {
                return wallImpactClips[Random.Range(0, wallImpactClips.Length)];
            }

            if (generatedWallImpactClip == null)
            {
                generatedWallImpactClip = CreateGeneratedWallImpactClip();
            }

            return generatedWallImpactClip;
        }

        private AudioClip GetHitImpactClip()
        {
            if (hitImpactClips != null && hitImpactClips.Length > 0)
            {
                return hitImpactClips[Random.Range(0, hitImpactClips.Length)];
            }

            if (generatedHitImpactClip == null)
            {
                generatedHitImpactClip = CreateGeneratedHitImpactClip();
            }

            return generatedHitImpactClip;
        }

        private AudioClip CreateGeneratedSwingLoopClip()
        {
            const int sampleRate = 22050;
            const float duration = 1f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float cycle = Mathf.Sin(2f * Mathf.PI * time);
                float envelope = 0.2f + 0.8f * Mathf.Abs(cycle);
                float air = Mathf.Sin(2f * Mathf.PI * 95f * time) * 0.16f
                    + Mathf.Sin(2f * Mathf.PI * 181f * time) * 0.08f
                    + Mathf.Sin(2f * Mathf.PI * 347f * time) * 0.035f;
                samples[i] = air * envelope;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedAxeSwingLoop", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateGeneratedWallImpactClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.34f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Exp(-time * 13f);
                float metal = Mathf.Sin(2f * Mathf.PI * 170f * time) * 0.28f
                    + Mathf.Sin(2f * Mathf.PI * 411f * time) * 0.22f
                    + Mathf.Sin(2f * Mathf.PI * 910f * time) * 0.14f;
                float scrape = Mathf.Sin(2f * Mathf.PI * 62f * time) * 0.1f;
                samples[i] = (metal + scrape) * envelope;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedAxeWallImpact", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateGeneratedHitImpactClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.24f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Exp(-time * 18f);
                float thump = Mathf.Sin(2f * Mathf.PI * 72f * time) * 0.42f;
                float blade = Mathf.Sin(2f * Mathf.PI * 530f * time) * 0.11f;
                samples[i] = (thump + blade) * envelope;
            }

            AudioClip clip = AudioClip.Create($"{name}_GeneratedAxeHitImpact", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private static float MaxAxis(Vector3 value)
        {
            return Mathf.Max(value.x, Mathf.Max(value.y, value.z));
        }

        private void OnValidate()
        {
            maximumAngle = Mathf.Max(0f, maximumAngle);
            swingsPerSecond = Mathf.Max(0.01f, swingsPerSecond);
            collisionStepDegrees = Mathf.Max(0.1f, collisionStepDegrees);
            wallRetreatDegrees = Mathf.Max(0f, wallRetreatDegrees);
            wallBounceCooldown = Mathf.Max(0f, wallBounceCooldown);
            swingDamping = Mathf.Max(0f, swingDamping);
            haltAngle = Mathf.Max(0f, haltAngle);
            minimumAudibleAngularSpeed = Mathf.Max(0f, minimumAudibleAngularSpeed);
            audioFadeSpeed = Mathf.Max(0f, audioFadeSpeed);
            audioMinDistance = Mathf.Max(0f, audioMinDistance);
            audioMaxDistance = Mathf.Max(0.1f, audioMaxDistance);
        }
    }
}
