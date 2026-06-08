using System;
using UnityEngine;
using UnityEngine.AI;

namespace Neighbor.Main.Features.Neighbor
{
    [DisallowMultipleComponent]
    public sealed class NeighborAudioController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NeighborBrain brain;
        [SerializeField] private NeighborMotor motor;
        [SerializeField] private Transform bodyAnchor;
        [SerializeField] private Transform voiceAnchor;
        [SerializeField] private Transform leftFootAnchor;
        [SerializeField] private Transform rightFootAnchor;
        [SerializeField] private AudioSource leftFootSource;
        [SerializeField] private AudioSource rightFootSource;
        [SerializeField] private AudioSource bodyOneShotSource;
        [SerializeField] private AudioSource voiceSource;
        [SerializeField] private AudioSource breathingLoopSource;
        [SerializeField] private AudioSource chaseLoopSource;
        [SerializeField] private AudioSource movementFoleyLoopSource;

        [Header("Footsteps")]
        [SerializeField] private AudioClip[] downstairsWalkFootsteps;
        [SerializeField] private AudioClip[] downstairsRunFootsteps;
        [SerializeField] private AudioClip[] upstairsWalkFootsteps;
        [SerializeField] private AudioClip[] upstairsRunFootsteps;
        [SerializeField, Min(0f)] private float upstairsHeight = 2.2f;
        [SerializeField, Min(0f)] private float minimumFootstepSpeed = 0.32f;
        [SerializeField, Min(0.1f)] private float runSpeedReference = 5.8f;
        [SerializeField, Min(0.05f)] private float slowStepInterval = 0.54f;
        [SerializeField, Min(0.05f)] private float fastStepInterval = 0.24f;
        [SerializeField, Range(0f, 1f)] private float footstepMinimumVolume = 0.34f;
        [SerializeField, Range(0f, 1f)] private float footstepMaximumVolume = 0.78f;
        [SerializeField, Range(0f, 1f)] private float upstairsVolumeBoost = 0.14f;
        [SerializeField, Range(0f, 1f)] private float footstepPitchRandomness = 0.07f;
        [SerializeField, Range(0f, 1f)] private float footstepStereoWidth = 0.22f;

        [Header("Movement Foley")]
        [SerializeField] private AudioClip movementFoleyLoopClip;
        [SerializeField, Range(0f, 1f)] private float movementFoleyVolume = 0.16f;
        [SerializeField, Min(0f)] private float movementFoleySpeedThreshold = 0.45f;

        [Header("Climb And Landing")]
        [SerializeField] private AudioClip[] climbStartClips;
        [SerializeField] private AudioClip[] climbEndClips;
        [SerializeField] private AudioClip[] dropLandingClips;
        [SerializeField, Range(0f, 1f)] private float climbVolume = 0.72f;
        [SerializeField, Range(0f, 1f)] private float landingVolume = 0.82f;
        [SerializeField, Min(0f)] private float dropLandingHeight = 0.25f;

        [Header("Voice And Alert")]
        [SerializeField] private AudioClip[] alertedClips;
        [SerializeField] private AudioClip[] chaseStartClips;
        [SerializeField] private AudioClip[] searchLostClips;
        [SerializeField] private AudioClip[] stunnedClips;
        [SerializeField] private AudioClip[] idleMutterClips;
        [SerializeField, Range(0f, 1f)] private float alertedVolume = 0.74f;
        [SerializeField, Range(0f, 1f)] private float chaseStartVolume = 0.88f;
        [SerializeField, Range(0f, 1f)] private float searchLostVolume = 0.48f;
        [SerializeField, Range(0f, 1f)] private float stunnedVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float idleMutterVolume = 0.2f;
        [SerializeField, Min(0f)] private float idleMutterMinimumDelay = 7f;
        [SerializeField, Min(0f)] private float idleMutterMaximumDelay = 15f;

        [Header("Breathing And Chase")]
        [SerializeField] private AudioClip breathingLoopClip;
        [SerializeField] private AudioClip chaseLoopClip;
        [SerializeField, Range(0f, 1f)] private float idleBreathingVolume = 0.08f;
        [SerializeField, Range(0f, 1f)] private float alertedBreathingVolume = 0.18f;
        [SerializeField, Range(0f, 1f)] private float chaseBreathingVolume = 0.34f;
        [SerializeField, Range(0f, 1f)] private float chaseLoopVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] private float searchLoopVolume = 0.16f;
        [SerializeField, Min(0f)] private float loopFadeSharpness = 8f;

        [Header("3D Audio")]
        [SerializeField, Min(0f)] private float minDistance = 0.3f;
        [SerializeField, Min(0.1f)] private float footstepMaxDistance = 14f;
        [SerializeField, Min(0.1f)] private float voiceMaxDistance = 18f;
        [SerializeField, Min(0.1f)] private float chaseMaxDistance = 22f;
        [SerializeField, Min(0f)] private float dopplerLevel = 0.12f;

        [Header("Occlusion")]
        [SerializeField] private bool enableOcclusion = true;
        [SerializeField] private LayerMask occlusionMask = ~0;
        [SerializeField, Range(0f, 1f)] private float occludedVolumeMultiplier = 0.56f;
        [SerializeField, Min(10f)] private float clearLowPassCutoff = 22000f;
        [SerializeField, Min(10f)] private float occludedLowPassCutoff = 1600f;
        [SerializeField, Min(0.02f)] private float occlusionRefreshInterval = 0.12f;
        [SerializeField, Min(0f)] private float occlusionFadeSharpness = 9f;

        [Header("Generated Fallback Clips")]
        [SerializeField] private bool useGeneratedFallbacks = true;
        [SerializeField, Min(8000)] private int generatedSampleRate = 22050;
        [SerializeField, Range(1, 8)] private int generatedFootstepVariants = 4;

        private readonly RaycastHit[] occlusionHits = new RaycastHit[12];
        private SpatialAudioEmitter[] emitters;
        private NavMeshAgent agent;
        private Vector3 lastPosition;
        private float planarSpeed;
        private float verticalTraversalStartY;
        private float footstepTimer;
        private float nextMutterTime;
        private float nextOcclusionTime;
        private int footstepSide;
        private bool wasTraversing;
        private NeighborBrain.BehaviorState previousState;
        private AudioListener listener;

        private AudioClip[] generatedDownstairsWalkFootsteps;
        private AudioClip[] generatedDownstairsRunFootsteps;
        private AudioClip[] generatedUpstairsWalkFootsteps;
        private AudioClip[] generatedUpstairsRunFootsteps;
        private AudioClip[] generatedClimbStartClips;
        private AudioClip[] generatedClimbEndClips;
        private AudioClip[] generatedDropLandingClips;
        private AudioClip[] generatedAlertedClips;
        private AudioClip[] generatedChaseStartClips;
        private AudioClip[] generatedSearchLostClips;
        private AudioClip[] generatedStunnedClips;
        private AudioClip[] generatedIdleMutterClips;
        private AudioClip generatedBreathingLoopClip;
        private AudioClip generatedChaseLoopClip;
        private AudioClip generatedMovementFoleyLoopClip;

        private void Awake()
        {
            brain = brain != null ? brain : GetComponent<NeighborBrain>();
            motor = motor != null ? motor : GetComponent<NeighborMotor>();
            agent = GetComponent<NavMeshAgent>();

            ResolveAnchors();
            ResolveAudioSources();
            CreateGeneratedFallbacks();

            lastPosition = transform.position;
            previousState = brain != null ? brain.CurrentState : NeighborBrain.BehaviorState.Idle;
            ScheduleNextMutter();
        }

        private void Start()
        {
            previousState = brain != null ? brain.CurrentState : previousState;
            StartLoop(breathingLoopSource, GetBreathingLoopClip());
            StartLoop(chaseLoopSource, GetChaseLoopClip());
            StartLoop(movementFoleyLoopSource, GetMovementFoleyLoopClip());
        }

        private void Update()
        {
            UpdateMotion();
            UpdateStateSounds();
            UpdateTraversalSounds();
            UpdateFootsteps();
            UpdateLoops();
            UpdateIdleMutter();
            UpdateOcclusion();
        }

        private void ResolveAnchors()
        {
            bodyAnchor = bodyAnchor != null ? bodyAnchor : CreateAnchor("Neighbor Body Audio Anchor", new Vector3(0f, 1f, 0f));
            voiceAnchor = voiceAnchor != null ? voiceAnchor : CreateAnchor("Neighbor Voice Audio Anchor", new Vector3(0f, 1.62f, 0.08f));
            leftFootAnchor = leftFootAnchor != null ? leftFootAnchor : CreateAnchor("Neighbor Left Foot Audio Anchor", new Vector3(-footstepStereoWidth, 0.08f, 0.08f));
            rightFootAnchor = rightFootAnchor != null ? rightFootAnchor : CreateAnchor("Neighbor Right Foot Audio Anchor", new Vector3(footstepStereoWidth, 0.08f, 0.08f));
        }

        private Transform CreateAnchor(string anchorName, Vector3 localPosition)
        {
            Transform existing = transform.Find(anchorName);
            if (existing != null)
            {
                existing.localPosition = localPosition;
                return existing;
            }

            GameObject anchorObject = new GameObject(anchorName);
            anchorObject.transform.SetParent(transform);
            anchorObject.transform.localPosition = localPosition;
            anchorObject.transform.localRotation = Quaternion.identity;
            anchorObject.transform.localScale = Vector3.one;
            return anchorObject.transform;
        }

        private void ResolveAudioSources()
        {
            leftFootSource = leftFootSource != null ? leftFootSource : leftFootAnchor.gameObject.AddComponent<AudioSource>();
            rightFootSource = rightFootSource != null ? rightFootSource : rightFootAnchor.gameObject.AddComponent<AudioSource>();
            bodyOneShotSource = bodyOneShotSource != null ? bodyOneShotSource : bodyAnchor.gameObject.AddComponent<AudioSource>();
            voiceSource = voiceSource != null ? voiceSource : voiceAnchor.gameObject.AddComponent<AudioSource>();
            breathingLoopSource = breathingLoopSource != null ? breathingLoopSource : voiceAnchor.gameObject.AddComponent<AudioSource>();
            chaseLoopSource = chaseLoopSource != null ? chaseLoopSource : bodyAnchor.gameObject.AddComponent<AudioSource>();
            movementFoleyLoopSource = movementFoleyLoopSource != null ? movementFoleyLoopSource : bodyAnchor.gameObject.AddComponent<AudioSource>();

            ConfigureSource(leftFootSource, false, footstepMaxDistance);
            ConfigureSource(rightFootSource, false, footstepMaxDistance);
            ConfigureSource(bodyOneShotSource, false, voiceMaxDistance);
            ConfigureSource(voiceSource, false, voiceMaxDistance);
            ConfigureSource(breathingLoopSource, true, voiceMaxDistance);
            ConfigureSource(chaseLoopSource, true, chaseMaxDistance);
            ConfigureSource(movementFoleyLoopSource, true, footstepMaxDistance);

            emitters = new[]
            {
                new SpatialAudioEmitter(leftFootSource),
                new SpatialAudioEmitter(rightFootSource),
                new SpatialAudioEmitter(bodyOneShotSource),
                new SpatialAudioEmitter(voiceSource),
                new SpatialAudioEmitter(breathingLoopSource),
                new SpatialAudioEmitter(chaseLoopSource),
                new SpatialAudioEmitter(movementFoleyLoopSource)
            };
        }

        private void ConfigureSource(AudioSource source, bool loop, float maxDistance)
        {
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = dopplerLevel;
            source.spread = 0f;
            source.spatialize = true;
        }

        private void UpdateMotion()
        {
            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 delta = (transform.position - lastPosition) / deltaTime;
            delta.y = 0f;

            float speed = delta.magnitude;
            if (agent != null && agent.enabled)
            {
                Vector3 agentVelocity = agent.velocity;
                agentVelocity.y = 0f;
                speed = Mathf.Max(speed, agentVelocity.magnitude);
            }

            planarSpeed = Mathf.Lerp(planarSpeed, speed, 1f - Mathf.Exp(-14f * Time.deltaTime));
            lastPosition = transform.position;
        }

        private void UpdateStateSounds()
        {
            if (brain == null)
            {
                return;
            }

            NeighborBrain.BehaviorState currentState = brain.CurrentState;
            if (currentState == previousState)
            {
                return;
            }

            switch (currentState)
            {
                case NeighborBrain.BehaviorState.Investigate:
                    PlayRandom(voiceSource, alertedClips, generatedAlertedClips, alertedVolume, 0.05f);
                    break;
                case NeighborBrain.BehaviorState.Chase:
                    PlayRandom(voiceSource, chaseStartClips, generatedChaseStartClips, chaseStartVolume, 0.04f);
                    break;
                case NeighborBrain.BehaviorState.HuntMode:
                    PlayRandom(voiceSource, searchLostClips, generatedSearchLostClips, searchLostVolume, 0.06f);
                    break;
                case NeighborBrain.BehaviorState.Stunned:
                    PlayRandom(voiceSource, stunnedClips, generatedStunnedClips, stunnedVolume, 0.06f);
                    break;
            }

            previousState = currentState;
        }

        private void UpdateTraversalSounds()
        {
            bool isTraversing = motor != null && motor.IsTraversingSpecialMove;
            if (!wasTraversing && isTraversing)
            {
                verticalTraversalStartY = transform.position.y;
                PlayRandom(bodyOneShotSource, climbStartClips, generatedClimbStartClips, climbVolume, 0.06f);
            }
            else if (wasTraversing && !isTraversing)
            {
                bool landedFromDrop = verticalTraversalStartY - transform.position.y >= dropLandingHeight;
                if (landedFromDrop)
                {
                    PlayRandom(bodyOneShotSource, dropLandingClips, generatedDropLandingClips, landingVolume, 0.05f);
                }
                else
                {
                    PlayRandom(bodyOneShotSource, climbEndClips, generatedClimbEndClips, climbVolume, 0.06f);
                }
            }

            wasTraversing = isTraversing;
        }

        private void UpdateFootsteps()
        {
            if (motor != null && motor.IsTraversingSpecialMove)
            {
                footstepTimer = 0f;
                return;
            }

            if (brain != null && brain.CurrentState == NeighborBrain.BehaviorState.Stunned)
            {
                footstepTimer = 0f;
                return;
            }

            if (planarSpeed < minimumFootstepSpeed)
            {
                footstepTimer = 0f;
                return;
            }

            footstepTimer -= Time.deltaTime;
            if (footstepTimer > 0f)
            {
                return;
            }

            float speed01 = Mathf.InverseLerp(minimumFootstepSpeed, runSpeedReference, planarSpeed);
            bool upstairs = transform.position.y >= upstairsHeight;
            bool running = speed01 >= 0.58f || brain != null && IsUrgentState(brain.CurrentState);
            AudioClip[] clips = GetFootstepClips(upstairs, running);
            AudioClip[] generatedClips = GetGeneratedFootstepClips(upstairs, running);
            AudioSource source = footstepSide == 0 ? leftFootSource : rightFootSource;

            float upstairsBoost = upstairs ? upstairsVolumeBoost : 0f;
            float volume = Mathf.Clamp01(Mathf.Lerp(footstepMinimumVolume, footstepMaximumVolume, speed01) + upstairsBoost);
            float pitch = Mathf.Lerp(0.88f, 1.12f, speed01);
            PlayRandom(source, clips, generatedClips, volume, footstepPitchRandomness, pitch);

            footstepSide = 1 - footstepSide;
            footstepTimer = Mathf.Lerp(slowStepInterval, fastStepInterval, speed01);
        }

        private void UpdateLoops()
        {
            NeighborBrain.BehaviorState state = brain != null ? brain.CurrentState : NeighborBrain.BehaviorState.Idle;
            float speed01 = Mathf.InverseLerp(movementFoleySpeedThreshold, runSpeedReference, planarSpeed);

            float breathingTarget = idleBreathingVolume + speed01 * 0.05f;
            float breathingPitch = Mathf.Lerp(0.94f, 1.12f, speed01);
            if (brain != null)
            {
                breathingTarget = Mathf.Max(breathingTarget, Mathf.Lerp(idleBreathingVolume, alertedBreathingVolume, brain.Suspicion));
                breathingPitch += brain.Suspicion * 0.04f;
            }
            if (state == NeighborBrain.BehaviorState.Investigate || state == NeighborBrain.BehaviorState.HuntMode)
            {
                breathingTarget = Mathf.Max(breathingTarget, alertedBreathingVolume);
                breathingPitch += 0.05f;
            }
            else if (state == NeighborBrain.BehaviorState.Chase)
            {
                breathingTarget = Mathf.Max(breathingTarget, chaseBreathingVolume);
                breathingPitch += 0.11f;
            }
            else if (state == NeighborBrain.BehaviorState.Stunned)
            {
                breathingTarget = Mathf.Max(breathingTarget, alertedBreathingVolume * 0.9f);
                breathingPitch = 0.82f;
            }

            SetLoopTarget(breathingLoopSource, GetBreathingLoopClip(), breathingTarget, breathingPitch);

            float chaseTarget = 0f;
            if (state == NeighborBrain.BehaviorState.Chase)
            {
                chaseTarget = chaseLoopVolume;
            }
            else if (state == NeighborBrain.BehaviorState.HuntMode)
            {
                chaseTarget = searchLoopVolume;
            }

            SetLoopTarget(chaseLoopSource, GetChaseLoopClip(), chaseTarget, 1f);

            float foleyTarget = planarSpeed >= movementFoleySpeedThreshold
                ? movementFoleyVolume * speed01
                : 0f;
            if (motor != null && motor.IsTraversingSpecialMove)
            {
                foleyTarget = Mathf.Max(foleyTarget, movementFoleyVolume * 0.8f);
            }

            SetLoopTarget(movementFoleyLoopSource, GetMovementFoleyLoopClip(), foleyTarget, Mathf.Lerp(0.9f, 1.16f, speed01));
        }

        private void UpdateIdleMutter()
        {
            if (brain == null || Time.time < nextMutterTime)
            {
                return;
            }

            if (brain.CurrentState == NeighborBrain.BehaviorState.Idle
                || brain.CurrentState == NeighborBrain.BehaviorState.Task
                || brain.CurrentState == NeighborBrain.BehaviorState.Wander)
            {
                float suspicionVolume = Mathf.Lerp(idleMutterVolume, idleMutterVolume * 1.35f, brain.Suspicion);
                PlayRandom(voiceSource, idleMutterClips, generatedIdleMutterClips, suspicionVolume, 0.08f);
            }

            ScheduleNextMutter();
        }

        private void UpdateOcclusion()
        {
            if (emitters == null)
            {
                return;
            }

            if (listener == null)
            {
                listener = FindAnyObjectByType<AudioListener>();
            }

            bool shouldRefresh = Time.time >= nextOcclusionTime;
            if (shouldRefresh)
            {
                nextOcclusionTime = Time.time + occlusionRefreshInterval;
            }

            for (int i = 0; i < emitters.Length; i++)
            {
                SpatialAudioEmitter emitter = emitters[i];
                if (emitter == null || emitter.Source == null)
                {
                    continue;
                }

                if (shouldRefresh)
                {
                    emitter.TargetOcclusion = CalculateOcclusion01(emitter.Source.transform);
                }

                emitter.Occlusion = Mathf.Lerp(
                    emitter.Occlusion,
                    emitter.TargetOcclusion,
                    1f - Mathf.Exp(-occlusionFadeSharpness * Time.deltaTime));

                if (emitter.LowPass != null)
                {
                    emitter.LowPass.cutoffFrequency = Mathf.Lerp(clearLowPassCutoff, occludedLowPassCutoff, emitter.Occlusion);
                }
            }
        }

        private float CalculateOcclusion01(Transform sourceTransform)
        {
            if (!enableOcclusion || listener == null || sourceTransform == null)
            {
                return 0f;
            }

            Vector3 listenerPosition = listener.transform.position;
            Vector3 toSource = sourceTransform.position - listenerPosition;
            float distance = toSource.magnitude;
            if (distance <= 0.1f)
            {
                return 0f;
            }

            Vector3 direction = toSource / distance;
            int hitCount = Physics.RaycastNonAlloc(
                listenerPosition,
                direction,
                occlusionHits,
                distance - 0.05f,
                occlusionMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Transform hitTransform = occlusionHits[i].transform;
                if (hitTransform == null || hitTransform.IsChildOf(transform) || hitTransform.IsChildOf(listener.transform))
                {
                    continue;
                }

                return 1f;
            }

            return 0f;
        }

        private void SetLoopTarget(AudioSource source, AudioClip clip, float targetVolume, float pitch)
        {
            if (source == null || clip == null)
            {
                return;
            }

            if (source.clip != clip)
            {
                source.clip = clip;
            }

            float occlusionMultiplier = GetOcclusionVolumeMultiplier(source);
            source.volume = Mathf.Lerp(
                source.volume,
                targetVolume * occlusionMultiplier,
                1f - Mathf.Exp(-loopFadeSharpness * Time.deltaTime));
            source.pitch = Mathf.Lerp(source.pitch, pitch, 1f - Mathf.Exp(-loopFadeSharpness * Time.deltaTime));

            if (targetVolume > 0.001f && !source.isPlaying)
            {
                source.Play();
            }
            else if (targetVolume <= 0.001f && source.isPlaying && source.volume <= 0.01f)
            {
                source.Stop();
            }
        }

        private void StartLoop(AudioSource source, AudioClip clip)
        {
            if (source == null || clip == null)
            {
                return;
            }

            source.clip = clip;
            source.volume = 0f;
            source.Play();
        }

        private void PlayRandom(AudioSource source, AudioClip[] clips, AudioClip[] generatedClips, float volume, float pitchRandomness, float basePitch = 1f)
        {
            if (source == null)
            {
                return;
            }

            AudioClip clip = PickClip(clips);
            if (clip == null)
            {
                clip = PickClip(generatedClips);
            }

            if (clip == null)
            {
                return;
            }

            source.volume = GetOcclusionVolumeMultiplier(source);
            source.pitch = basePitch * UnityEngine.Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
            source.PlayOneShot(clip, volume);
        }

        private AudioClip PickClip(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
            {
                return null;
            }

            int startIndex = UnityEngine.Random.Range(0, clips.Length);
            for (int i = 0; i < clips.Length; i++)
            {
                AudioClip clip = clips[(startIndex + i) % clips.Length];
                if (clip != null)
                {
                    return clip;
                }
            }

            return null;
        }

        private float GetOcclusionVolumeMultiplier(AudioSource source)
        {
            if (emitters == null)
            {
                return 1f;
            }

            for (int i = 0; i < emitters.Length; i++)
            {
                SpatialAudioEmitter emitter = emitters[i];
                if (emitter != null && emitter.Source == source)
                {
                    return Mathf.Lerp(1f, occludedVolumeMultiplier, emitter.Occlusion);
                }
            }

            return 1f;
        }

        private AudioClip[] GetFootstepClips(bool upstairs, bool running)
        {
            if (upstairs)
            {
                return running ? upstairsRunFootsteps : upstairsWalkFootsteps;
            }

            return running ? downstairsRunFootsteps : downstairsWalkFootsteps;
        }

        private AudioClip[] GetGeneratedFootstepClips(bool upstairs, bool running)
        {
            if (!useGeneratedFallbacks)
            {
                return null;
            }

            if (upstairs)
            {
                return running ? generatedUpstairsRunFootsteps : generatedUpstairsWalkFootsteps;
            }

            return running ? generatedDownstairsRunFootsteps : generatedDownstairsWalkFootsteps;
        }

        private AudioClip GetBreathingLoopClip()
        {
            return breathingLoopClip != null ? breathingLoopClip : generatedBreathingLoopClip;
        }

        private AudioClip GetChaseLoopClip()
        {
            return chaseLoopClip != null ? chaseLoopClip : generatedChaseLoopClip;
        }

        private AudioClip GetMovementFoleyLoopClip()
        {
            return movementFoleyLoopClip != null ? movementFoleyLoopClip : generatedMovementFoleyLoopClip;
        }

        private bool IsUrgentState(NeighborBrain.BehaviorState state)
        {
            return state == NeighborBrain.BehaviorState.Chase
                || state == NeighborBrain.BehaviorState.HuntMode
                || state == NeighborBrain.BehaviorState.Investigate;
        }

        private void ScheduleNextMutter()
        {
            float maximumDelay = Mathf.Max(idleMutterMinimumDelay, idleMutterMaximumDelay);
            nextMutterTime = Time.time + UnityEngine.Random.Range(idleMutterMinimumDelay, maximumDelay);
        }

        private void CreateGeneratedFallbacks()
        {
            if (!useGeneratedFallbacks)
            {
                return;
            }

            int variants = Mathf.Max(1, generatedFootstepVariants);
            generatedDownstairsWalkFootsteps = CreateFootstepSet("DownstairsWalk", variants, false, false);
            generatedDownstairsRunFootsteps = CreateFootstepSet("DownstairsRun", variants, false, true);
            generatedUpstairsWalkFootsteps = CreateFootstepSet("UpstairsWalk", variants, true, false);
            generatedUpstairsRunFootsteps = CreateFootstepSet("UpstairsRun", variants, true, true);
            generatedClimbStartClips = new[] { CreateClimbClip("GeneratedClimbStart", 0.42f, true) };
            generatedClimbEndClips = new[] { CreateClimbClip("GeneratedClimbEnd", 0.32f, false) };
            generatedDropLandingClips = new[] { CreateImpactClip("GeneratedDropLanding", 0.42f, 78f, 0.92f) };
            generatedAlertedClips = new[] { CreateVoiceClip("GeneratedAlerted", 0.48f, 0.28f, 0.68f, 0.34f) };
            generatedChaseStartClips = new[] { CreateVoiceClip("GeneratedChaseStart", 0.86f, 0.44f, 0.92f, 0.5f) };
            generatedSearchLostClips = new[] { CreateVoiceClip("GeneratedSearchLost", 0.58f, 0.22f, 0.58f, -0.18f) };
            generatedStunnedClips = new[] { CreateVoiceClip("GeneratedStunned", 0.44f, 0.32f, 0.68f, -0.34f) };
            generatedIdleMutterClips = new[]
            {
                CreateVoiceClip("GeneratedIdleMutterA", 0.68f, 0.13f, 0.34f, -0.12f),
                CreateVoiceClip("GeneratedIdleMutterB", 0.82f, 0.12f, 0.28f, 0.08f)
            };
            generatedBreathingLoopClip = CreateBreathingLoopClip();
            generatedChaseLoopClip = CreateChaseLoopClip();
            generatedMovementFoleyLoopClip = CreateMovementFoleyLoopClip();
        }

        private AudioClip[] CreateFootstepSet(string clipName, int variants, bool upstairs, bool running)
        {
            AudioClip[] clips = new AudioClip[variants];
            for (int i = 0; i < variants; i++)
            {
                clips[i] = CreateFootstepClip($"{clipName}{i + 1}", upstairs, running, 1000 + i * 71 + (upstairs ? 300 : 0) + (running ? 900 : 0));
            }

            return clips;
        }

        private AudioClip CreateFootstepClip(string clipName, bool upstairs, bool running, int seed)
        {
            float duration = running ? 0.2f : 0.24f;
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(generatedSampleRate * duration));
            float[] samples = new float[sampleCount];
            System.Random random = new System.Random(seed);
            float noiseState = 0f;
            float baseFrequency = upstairs ? 145f : 95f;
            if (running)
            {
                baseFrequency += upstairs ? 35f : 18f;
            }

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)generatedSampleRate;
                float envelope = Mathf.Exp(-time * (running ? 24f : 20f));
                float thump = Mathf.Sin(2f * Mathf.PI * baseFrequency * time) * (upstairs ? 0.5f : 0.78f);
                float scrapeNoise = (float)(random.NextDouble() * 2.0 - 1.0);
                noiseState = Mathf.Lerp(noiseState, scrapeNoise, upstairs ? 0.42f : 0.28f);

                float creak = 0f;
                if (upstairs)
                {
                    float creakEnvelope = Mathf.Exp(-time * 7.5f) * Mathf.Sin(Mathf.Clamp01(time / duration) * Mathf.PI);
                    creak = Mathf.Sin(2f * Mathf.PI * (running ? 760f : 610f) * time + Mathf.Sin(time * 64f) * 0.8f) * creakEnvelope * 0.36f;
                }

                float dirt = noiseState * (upstairs ? 0.26f : 0.18f);
                samples[i] = Mathf.Clamp((thump + dirt + creak) * envelope, -1f, 1f);
            }

            return CreateClip(clipName, samples);
        }

        private AudioClip CreateClimbClip(string clipName, float duration, bool start)
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(generatedSampleRate * duration));
            float[] samples = new float[sampleCount];
            System.Random random = new System.Random(start ? 4417 : 6619);
            float scrapeState = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)generatedSampleRate;
                float progress = Mathf.Clamp01(time / duration);
                float scrapeEnvelope = Mathf.Sin(progress * Mathf.PI);
                float thumpEnvelope = Mathf.Exp(-time * 16f);
                float scrapeNoise = (float)(random.NextDouble() * 2.0 - 1.0);
                scrapeState = Mathf.Lerp(scrapeState, scrapeNoise, 0.22f);

                float scrape = scrapeState * scrapeEnvelope * 0.35f;
                float thump = Mathf.Sin(2f * Mathf.PI * (start ? 115f : 86f) * time) * thumpEnvelope * 0.56f;
                float fabric = Mathf.Sin(2f * Mathf.PI * 310f * time) * scrapeEnvelope * 0.08f;
                samples[i] = Mathf.Clamp(scrape + thump + fabric, -1f, 1f);
            }

            return CreateClip(clipName, samples);
        }

        private AudioClip CreateImpactClip(string clipName, float duration, float baseFrequency, float intensity)
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(generatedSampleRate * duration));
            float[] samples = new float[sampleCount];
            System.Random random = new System.Random(9321);
            float noiseState = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)generatedSampleRate;
                float envelope = Mathf.Exp(-time * 18f);
                float thump = Mathf.Sin(2f * Mathf.PI * baseFrequency * time) * envelope * intensity;
                noiseState = Mathf.Lerp(noiseState, (float)(random.NextDouble() * 2.0 - 1.0), 0.18f);
                samples[i] = Mathf.Clamp(thump + noiseState * envelope * 0.24f, -1f, 1f);
            }

            return CreateClip(clipName, samples);
        }

        private AudioClip CreateVoiceClip(string clipName, float duration, float noiseAmount, float bodyAmount, float pitchBend)
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(generatedSampleRate * duration));
            float[] samples = new float[sampleCount];
            System.Random random = new System.Random(clipName.GetHashCode());
            float noiseState = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)generatedSampleRate;
                float progress = Mathf.Clamp01(time / duration);
                float fadeIn = Mathf.Clamp01(progress / 0.12f);
                float fadeOut = 1f - Mathf.Clamp01((progress - 0.72f) / 0.28f);
                float envelope = Mathf.SmoothStep(0f, 1f, fadeIn) * Mathf.SmoothStep(0f, 1f, fadeOut);
                float frequency = Mathf.Lerp(92f, 138f, progress + pitchBend * 0.25f);
                float body = Mathf.Sin(2f * Mathf.PI * frequency * time) * bodyAmount;
                float overtone = Mathf.Sin(2f * Mathf.PI * frequency * 1.72f * time) * bodyAmount * 0.24f;
                noiseState = Mathf.Lerp(noiseState, (float)(random.NextDouble() * 2.0 - 1.0), 0.18f);
                samples[i] = Mathf.Clamp((body + overtone + noiseState * noiseAmount) * envelope, -1f, 1f);
            }

            return CreateClip(clipName, samples);
        }

        private AudioClip CreateBreathingLoopClip()
        {
            float duration = 3.2f;
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(generatedSampleRate * duration));
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)generatedSampleRate;
                float phase = time / duration;
                float breath = 0.5f + Mathf.Sin(2f * Mathf.PI * phase - Mathf.PI * 0.5f) * 0.5f;
                float air = Mathf.Sin(2f * Mathf.PI * 740f * time + Mathf.Sin(2f * Mathf.PI * phase) * 0.8f) * 0.045f;
                air += Mathf.Sin(2f * Mathf.PI * 1120f * time + 1.7f) * 0.025f;
                float chest = Mathf.Sin(2f * Mathf.PI * 72f * time) * 0.035f;
                samples[i] = (air + chest) * (0.12f + breath * 0.78f);
            }

            return CreateClip("GeneratedNeighborBreathingLoop", samples);
        }

        private AudioClip CreateChaseLoopClip()
        {
            float duration = 2f;
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(generatedSampleRate * duration));
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)generatedSampleRate;
                float phase = time / duration;
                float firstPulse = Pulse(phase, 0.11f, 0.018f);
                float secondPulse = Pulse(phase, 0.32f, 0.022f);
                float rumble = Mathf.Sin(2f * Mathf.PI * 38f * time) * 0.08f;
                rumble += Mathf.Sin(2f * Mathf.PI * 51f * time + 0.7f) * 0.04f;
                float heartbeat = Mathf.Sin(2f * Mathf.PI * 84f * time) * (firstPulse * 0.7f + secondPulse * 0.45f);
                samples[i] = Mathf.Clamp(rumble + heartbeat, -1f, 1f);
            }

            return CreateClip("GeneratedNeighborChaseLoop", samples);
        }

        private AudioClip CreateMovementFoleyLoopClip()
        {
            float duration = 1.35f;
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(generatedSampleRate * duration));
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)generatedSampleRate;
                float phase = time / duration;
                float swish = Mathf.Sin(2f * Mathf.PI * phase * 2f);
                float fabric = Mathf.Sin(2f * Mathf.PI * 330f * time + swish * 0.8f) * 0.035f;
                fabric += Mathf.Sin(2f * Mathf.PI * 520f * time + phase * 5f) * 0.025f;
                samples[i] = fabric * (0.35f + Mathf.Abs(swish) * 0.65f);
            }

            return CreateClip("GeneratedNeighborMovementFoleyLoop", samples);
        }

        private float Pulse(float phase, float center, float width)
        {
            float distance = Mathf.Abs(Mathf.DeltaAngle(phase * 360f, center * 360f)) / 360f;
            return Mathf.Exp(-(distance * distance) / Mathf.Max(0.0001f, width * width));
        }

        private AudioClip CreateClip(string clipName, float[] samples)
        {
            AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, generatedSampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private sealed class SpatialAudioEmitter
        {
            public SpatialAudioEmitter(AudioSource source)
            {
                Source = source;
                if (source != null)
                {
                    LowPass = source.GetComponent<AudioLowPassFilter>();
                    if (LowPass == null)
                    {
                        LowPass = source.gameObject.AddComponent<AudioLowPassFilter>();
                    }
                }
            }

            public AudioSource Source { get; }
            public AudioLowPassFilter LowPass { get; }
            public float Occlusion { get; set; }
            public float TargetOcclusion { get; set; }
        }
    }
}
