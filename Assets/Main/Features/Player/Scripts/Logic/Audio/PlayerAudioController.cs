using UnityEngine;

namespace Neighbor.Main.Features.Player
{
    public sealed class PlayerAudioController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerCameraController cameraController;
        [SerializeField] private Transform audioAnchor;
        [SerializeField] private AudioSource oneShotSource;
        [SerializeField] private AudioSource footstepLoopSource;
        [SerializeField] private AudioSource slideLoopSource;
        [SerializeField] private AudioSource zoomLoopSource;

        [Header("Footsteps")]
        [SerializeField] private AudioClip walkFootstepLoop;
        [SerializeField] private AudioClip runFootstepLoop;
        [SerializeField] private AudioClip crouchFootstepLoop;
        [SerializeField, Range(0f, 1f)] private float footstepMinimumVolume = 0.2f;
        [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.55f;
        [SerializeField, Min(0f)] private float footstepLoopFadeSharpness = 12f;
        [SerializeField, HideInInspector] private AudioClip[] walkFootsteps;
        [SerializeField, HideInInspector] private AudioClip[] runFootsteps;
        [SerializeField, HideInInspector] private AudioClip[] crouchFootsteps;

        [Header("Movement Actions")]
        [SerializeField] private AudioClip[] jumpClips;
        [SerializeField] private AudioClip[] landingClips;
        [SerializeField] private AudioClip[] stairStepClips;
        [SerializeField] private AudioClip[] crouchDownClips;
        [SerializeField] private AudioClip[] standUpClips;
        [SerializeField, Range(0f, 1f)] private float jumpVolume = 0.65f;
        [SerializeField, Range(0f, 1f)] private float landingMinimumVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float landingMaximumVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float stanceVolume = 0.45f;

        [Header("Slide")]
        [SerializeField] private AudioClip[] slideStartClips;
        [SerializeField] private AudioClip slideLoopClip;
        [SerializeField] private AudioClip[] slideEndClips;
        [SerializeField, Range(0f, 1f)] private float slideStartVolume = 0.75f;
        [SerializeField, Range(0f, 1f)] private float slideLoopVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] private float slideEndVolume = 0.45f;

        [Header("Camera Zoom")]
        [SerializeField] private AudioClip zoomInLoopClip;
        [SerializeField] private AudioClip zoomOutLoopClip;
        [SerializeField, Range(0f, 1f)] private float zoomLoopVolume = 0.45f;
        [SerializeField, Min(0f)] private float zoomStartOffset;
        [SerializeField, Min(0f)] private float zoomResumeWindow = 0.12f;

        [Header("Ledge Climb")]
        [SerializeField] private AudioClip[] ledgeClimbStartClips;
        [SerializeField] private AudioClip[] ledgeClimbEndClips;
        [SerializeField, Range(0f, 1f)] private float ledgeClimbVolume = 0.65f;

        [Header("3D Audio")]
        [SerializeField, Min(0f)] private float minDistance = 0.25f;
        [SerializeField, Min(0.1f)] private float maxDistance = 12f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.04f;

        private AudioClip activeFootstepLoopClip;
        private float activeFootstepLoopPitch = 1f;
        private bool wasCrouching;
        private bool wasSliding;
        private bool wasLedgeClimbing;
        private AudioClip pausedZoomClip;
        private int pausedZoomSample;
        private float zoomPausedAt = float.NegativeInfinity;

        private void Awake()
        {
            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
            }

            if (audioAnchor == null)
            {
                audioAnchor = transform;
            }

            if (cameraController == null)
            {
                cameraController = GetComponentInChildren<PlayerCameraController>();
            }

            if (oneShotSource == null)
            {
                oneShotSource = audioAnchor.gameObject.AddComponent<AudioSource>();
            }

            if (slideLoopSource == null)
            {
                slideLoopSource = audioAnchor.gameObject.AddComponent<AudioSource>();
            }

            if (footstepLoopSource == null)
            {
                footstepLoopSource = audioAnchor.gameObject.AddComponent<AudioSource>();
            }

            if (zoomLoopSource == null)
            {
                zoomLoopSource = audioAnchor.gameObject.AddComponent<AudioSource>();
            }

            ConfigureSource(oneShotSource, false);
            ConfigureSource(footstepLoopSource, true);
            ConfigureSource(slideLoopSource, true);
            ConfigureSource(zoomLoopSource, true);
            zoomLoopSource.spatialBlend = 0f;
            zoomLoopSource.dopplerLevel = 0f;
            MigrateFootstepLoopClips();
            SubscribeToCameraZoom();
            wasCrouching = playerController != null && playerController.IsCrouching;
            wasSliding = playerController != null && playerController.IsSliding;
            wasLedgeClimbing = playerController != null && playerController.IsLedgeClimbing;
        }

        private void OnEnable()
        {
            if (cameraController == null)
            {
                cameraController = GetComponentInChildren<PlayerCameraController>();
            }

            SubscribeToCameraZoom();
        }

        private void Update()
        {
            if (playerController == null)
            {
                return;
            }

            UpdateOneShotMovementSounds();
            UpdateFootsteps();
            UpdateSlideLoop();
            UpdatePreviousState();
        }

        private void LateUpdate()
        {
            if (cameraController != null)
            {
                UpdateZoomLoop(cameraController.ZoomDirection);
            }
        }

        private void UpdateOneShotMovementSounds()
        {
            if (playerController.JumpStartedThisFrame)
            {
                PlayRandom(jumpClips, jumpVolume);
            }

            if (playerController.LandedThisFrame)
            {
                float volume = Mathf.Lerp(landingMinimumVolume, landingMaximumVolume, playerController.LandingImpact);
                PlayRandom(landingClips, volume);
            }

            if (playerController.StepImpactThisFrame)
            {
                PlayRandom(stairStepClips, Mathf.Lerp(0.35f, 0.75f, playerController.StepImpact));
            }

            if (!wasCrouching && playerController.IsCrouching)
            {
                PlayRandom(crouchDownClips, stanceVolume);
            }
            else if (wasCrouching && !playerController.IsCrouching)
            {
                PlayRandom(standUpClips, stanceVolume);
            }

            if (!wasSliding && playerController.IsSliding)
            {
                PlayRandom(slideStartClips, slideStartVolume);
            }
            else if (wasSliding && !playerController.IsSliding)
            {
                PlayRandom(slideEndClips, slideEndVolume);
            }

            if (!wasLedgeClimbing && playerController.IsLedgeClimbing)
            {
                PlayRandom(ledgeClimbStartClips, ledgeClimbVolume);
            }
            else if (wasLedgeClimbing && !playerController.IsLedgeClimbing)
            {
                PlayRandom(ledgeClimbEndClips, ledgeClimbVolume);
            }
        }

        private void UpdateFootsteps()
        {
            bool shouldLoopFootsteps = playerController.IsGrounded
                && !playerController.IsSliding
                && !playerController.IsLedgeClimbing
                && playerController.MoveAmount > 0.08f;

            AudioClip targetClip = shouldLoopFootsteps ? GetFootstepLoopClip() : null;
            if (targetClip != null && targetClip != activeFootstepLoopClip)
            {
                SwitchFootstepLoop(targetClip);
            }

            float targetVolume = shouldLoopFootsteps && targetClip != null
                ? Mathf.Lerp(footstepMinimumVolume, footstepVolume, playerController.Speed01)
                : 0f;
            float targetPitch = activeFootstepLoopPitch * Mathf.Lerp(0.96f, 1.04f, playerController.Speed01);
            FadeFootstepLoop(targetVolume, targetPitch);
        }

        private void MigrateFootstepLoopClips()
        {
            walkFootstepLoop = walkFootstepLoop != null ? walkFootstepLoop : GetFirstAssignedClip(walkFootsteps);
            runFootstepLoop = runFootstepLoop != null ? runFootstepLoop : GetFirstAssignedClip(runFootsteps);
            crouchFootstepLoop = crouchFootstepLoop != null ? crouchFootstepLoop : GetFirstAssignedClip(crouchFootsteps);
        }

        private static AudioClip GetFirstAssignedClip(AudioClip[] clips)
        {
            if (clips == null)
            {
                return null;
            }

            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null)
                {
                    return clips[i];
                }
            }

            return null;
        }

        private AudioClip GetFootstepLoopClip()
        {
            if (playerController.IsCrouching)
            {
                return crouchFootstepLoop != null ? crouchFootstepLoop : walkFootstepLoop;
            }

            return playerController.IsRunning && runFootstepLoop != null ? runFootstepLoop : walkFootstepLoop;
        }

        private void SwitchFootstepLoop(AudioClip clip)
        {
            if (footstepLoopSource == null || clip == null)
            {
                activeFootstepLoopClip = null;
                return;
            }

            activeFootstepLoopClip = clip;
            activeFootstepLoopPitch = Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
            footstepLoopSource.Stop();
            footstepLoopSource.clip = clip;
            footstepLoopSource.loop = true;
            footstepLoopSource.volume = 0f;
            footstepLoopSource.pitch = activeFootstepLoopPitch;
            footstepLoopSource.Play();
        }

        private void FadeFootstepLoop(float targetVolume, float targetPitch)
        {
            if (footstepLoopSource == null)
            {
                activeFootstepLoopClip = null;
                return;
            }

            float fade = 1f - Mathf.Exp(-footstepLoopFadeSharpness * Time.deltaTime);
            footstepLoopSource.volume = Mathf.Lerp(footstepLoopSource.volume, targetVolume, fade);
            footstepLoopSource.pitch = Mathf.Lerp(footstepLoopSource.pitch, targetPitch, fade);

            if (targetVolume <= 0.001f && footstepLoopSource.isPlaying && footstepLoopSource.volume <= 0.01f)
            {
                footstepLoopSource.Stop();
                activeFootstepLoopClip = null;
            }
            else if (targetVolume > 0.001f && footstepLoopSource.clip != null && !footstepLoopSource.isPlaying)
            {
                footstepLoopSource.Play();
            }
        }

        private void UpdateSlideLoop()
        {
            if (slideLoopSource == null)
            {
                return;
            }

            if (playerController.IsSliding && slideLoopClip != null)
            {
                if (slideLoopSource.clip != slideLoopClip)
                {
                    slideLoopSource.clip = slideLoopClip;
                }

                slideLoopSource.volume = Mathf.Lerp(slideLoopSource.volume, slideLoopVolume, 1f - Mathf.Exp(-12f * Time.deltaTime));
                if (!slideLoopSource.isPlaying)
                {
                    slideLoopSource.Play();
                }

                return;
            }

            if (slideLoopSource.isPlaying)
            {
                slideLoopSource.volume = Mathf.Lerp(slideLoopSource.volume, 0f, 1f - Mathf.Exp(-18f * Time.deltaTime));
                if (slideLoopSource.volume <= 0.01f)
                {
                    slideLoopSource.Stop();
                    slideLoopSource.volume = slideLoopVolume;
                }
            }
        }

        private void UpdateZoomLoop(int zoomDirection)
        {
            if (zoomLoopSource == null)
            {
                return;
            }

            AudioClip targetClip = zoomDirection > 0 ? zoomInLoopClip : zoomDirection < 0 ? zoomOutLoopClip : null;

            if (targetClip == null)
            {
                if (zoomLoopSource.isPlaying)
                {
                    pausedZoomClip = zoomLoopSource.clip;
                    pausedZoomSample = zoomLoopSource.timeSamples;
                    zoomPausedAt = Time.unscaledTime;
                    zoomLoopSource.Stop();
                }

                return;
            }

            if (zoomLoopSource.isPlaying && zoomLoopSource.clip == targetClip)
            {
                return;
            }

            bool resumePausedClip = pausedZoomClip == targetClip
                && Time.unscaledTime - zoomPausedAt <= zoomResumeWindow;

            zoomLoopSource.volume = zoomLoopVolume;
            zoomLoopSource.Stop();
            zoomLoopSource.clip = targetClip;
            zoomLoopSource.timeSamples = resumePausedClip
                ? Mathf.Clamp(pausedZoomSample, 0, targetClip.samples - 1)
                : Mathf.Clamp(Mathf.RoundToInt(zoomStartOffset * targetClip.frequency), 0, targetClip.samples - 1);
            pausedZoomClip = null;
            zoomLoopSource.Play();
        }

        private void OnDisable()
        {
            if (cameraController != null)
            {
                cameraController.ZoomDirectionChanged -= UpdateZoomLoop;
            }

            if (slideLoopSource != null)
            {
                slideLoopSource.Stop();
            }

            if (footstepLoopSource != null)
            {
                footstepLoopSource.Stop();
            }

            if (zoomLoopSource != null)
            {
                zoomLoopSource.Stop();
            }

            activeFootstepLoopClip = null;
            pausedZoomClip = null;
            pausedZoomSample = 0;
            zoomPausedAt = float.NegativeInfinity;
        }

        private void SubscribeToCameraZoom()
        {
            if (cameraController == null)
            {
                return;
            }

            cameraController.ZoomDirectionChanged -= UpdateZoomLoop;
            cameraController.ZoomDirectionChanged += UpdateZoomLoop;
        }

        private void UpdatePreviousState()
        {
            wasCrouching = playerController.IsCrouching;
            wasSliding = playerController.IsSliding;
            wasLedgeClimbing = playerController.IsLedgeClimbing;
        }

        private void PlayRandom(AudioClip[] clips, float volume)
        {
            if (clips == null || clips.Length == 0 || oneShotSource == null)
            {
                return;
            }

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null)
            {
                return;
            }

            oneShotSource.pitch = Random.Range(1f - pitchRandomness, 1f + pitchRandomness);
            oneShotSource.PlayOneShot(clip, volume);
        }

        private void ConfigureSource(AudioSource source, bool loop)
        {
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0.1f;
        }
    }
}
