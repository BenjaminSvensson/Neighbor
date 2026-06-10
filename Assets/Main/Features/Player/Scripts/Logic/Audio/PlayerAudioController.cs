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
        [SerializeField] private AudioSource slideLoopSource;
        [SerializeField] private AudioSource zoomLoopSource;

        [Header("Footsteps")]
        [SerializeField] private AudioClip[] walkFootsteps;
        [SerializeField] private AudioClip[] runFootsteps;
        [SerializeField] private AudioClip[] crouchFootsteps;
        [SerializeField, Min(0.05f)] private float walkStepInterval = 0.48f;
        [SerializeField, Min(0.05f)] private float runStepInterval = 0.32f;
        [SerializeField, Min(0.05f)] private float crouchStepInterval = 0.62f;
        [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.55f;

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

        [Header("Ledge Climb")]
        [SerializeField] private AudioClip[] ledgeClimbStartClips;
        [SerializeField] private AudioClip[] ledgeClimbEndClips;
        [SerializeField, Range(0f, 1f)] private float ledgeClimbVolume = 0.65f;

        [Header("3D Audio")]
        [SerializeField, Min(0f)] private float minDistance = 0.25f;
        [SerializeField, Min(0.1f)] private float maxDistance = 12f;
        [SerializeField, Min(0f)] private float pitchRandomness = 0.04f;

        private float footstepTimer;
        private bool wasCrouching;
        private bool wasSliding;
        private bool wasLedgeClimbing;

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

            if (zoomLoopSource == null)
            {
                zoomLoopSource = audioAnchor.gameObject.AddComponent<AudioSource>();
            }

            ConfigureSource(oneShotSource, false);
            ConfigureSource(slideLoopSource, true);
            ConfigureSource(zoomLoopSource, true);
            wasCrouching = playerController != null && playerController.IsCrouching;
            wasSliding = playerController != null && playerController.IsSliding;
            wasLedgeClimbing = playerController != null && playerController.IsLedgeClimbing;
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
            UpdateZoomLoop();
            UpdatePreviousState();
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
            bool shouldStep = playerController.IsGrounded
                && !playerController.IsSliding
                && !playerController.IsLedgeClimbing
                && playerController.MoveAmount > 0.08f;

            if (!shouldStep)
            {
                footstepTimer = 0f;
                return;
            }

            float interval = playerController.IsCrouching
                ? crouchStepInterval
                : playerController.IsRunning ? runStepInterval : walkStepInterval;

            footstepTimer -= Time.deltaTime;
            if (footstepTimer > 0f)
            {
                return;
            }

            AudioClip[] clips = playerController.IsCrouching
                ? crouchFootsteps
                : playerController.IsRunning ? runFootsteps : walkFootsteps;

            PlayRandom(clips, footstepVolume);
            footstepTimer = interval;
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

        private void UpdateZoomLoop()
        {
            if (zoomLoopSource == null)
            {
                return;
            }

            AudioClip targetClip = cameraController != null
                ? cameraController.ZoomDirection > 0 ? zoomInLoopClip : cameraController.ZoomDirection < 0 ? zoomOutLoopClip : null
                : null;

            if (targetClip == null)
            {
                if (zoomLoopSource.isPlaying)
                {
                    zoomLoopSource.Stop();
                }

                return;
            }

            if (zoomLoopSource.clip != targetClip)
            {
                zoomLoopSource.Stop();
                zoomLoopSource.clip = targetClip;
            }

            zoomLoopSource.volume = zoomLoopVolume;
            if (!zoomLoopSource.isPlaying)
            {
                zoomLoopSource.Play();
            }
        }

        private void OnDisable()
        {
            if (slideLoopSource != null)
            {
                slideLoopSource.Stop();
            }

            if (zoomLoopSource != null)
            {
                zoomLoopSource.Stop();
            }
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
