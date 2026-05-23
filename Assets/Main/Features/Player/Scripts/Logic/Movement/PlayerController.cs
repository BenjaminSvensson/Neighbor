using Neighbor.Main.Features.Neighbor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Neighbor.Main.Features.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform playerHead;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 4.2f;
        [SerializeField, Min(0f)] private float runSpeed = 6.8f;
        [SerializeField, Min(0f)] private float crouchSpeed = 2.3f;
        [SerializeField, Min(0f)] private float groundAcceleration = 18f;
        [SerializeField, Min(0f)] private float airAcceleration = 6f;

        [Header("Jumping")]
        [SerializeField, Min(0f)] private float jumpHeight = 1.15f;
        [SerializeField, Min(0f)] private float coyoteTime = 0.12f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float groundedStickForce = -2f;

        [Header("Heavy Landing")]
        [SerializeField, Min(0f)] private float heavyLandingMinimumFallTime = 0.65f;
        [SerializeField, Min(0f)] private float heavyLandingMinimumFallSpeed = 13f;
        [SerializeField, Min(0f)] private float heavyLandingFullFallSpeed = 24f;
        [SerializeField, Range(0f, 1f)] private float heavyLandingSpeedMultiplier = 0.45f;
        [SerializeField, Min(0f)] private float heavyLandingSlowDuration = 0.45f;
        [SerializeField, Range(0f, 1f)] private float heavyLandingHorizontalDamping = 0.32f;

        [Header("Ledge Climb")]
        [SerializeField] private bool enableLedgeClimb = true;
        [SerializeField, Min(0.1f)] private float ledgeCheckDistance = 0.75f;
        [SerializeField, Min(0f)] private float ledgeMinimumHeight = 0.45f;
        [SerializeField, Min(0f)] private float ledgeMaximumHeight = 1.25f;
        [SerializeField, Min(0f)] private float ledgeTopProbeForwardOffset = 0.35f;
        [SerializeField, Min(0.01f)] private float ledgeClimbDuration = 0.24f;
        [SerializeField] private LayerMask ledgeClimbMask = ~0;

        [Header("Crouch")]
        [SerializeField, Min(0.1f)] private float standingHeight = 2f;
        [SerializeField, Min(0.1f)] private float crouchingHeight = 1.2f;
        [SerializeField, Min(0f)] private float stanceSmoothTime = 0.08f;
        [SerializeField] private LayerMask crouchObstructionMask = ~0;

        [Header("Slide")]
        [SerializeField, Min(0f)] private float minimumSlideStartSpeed = 5.2f;
        [SerializeField, Min(0f)] private float slideStartSpeed = 8.2f;
        [SerializeField, Min(0f)] private float slideEndSpeed = 2.4f;
        [SerializeField, Min(0.01f)] private float slideDuration = 0.55f;
        [SerializeField, Min(0f)] private float slideSteerStrength = 3f;
        [SerializeField, Min(0f)] private float sprintSlideGraceTime = 0.18f;
        [SerializeField, Min(0f)] private float slideDownhillAcceleration = 10f;
        [SerializeField, Min(0f)] private float slideUphillDeceleration = 8f;
        [SerializeField, Min(0f)] private float slideMaximumSlopeSpeed = 12f;
        [SerializeField, Min(0f)] private float slideObjectImpulse = 8f;
        [SerializeField, Min(0f)] private float slideObjectUpwardImpulse = 1.5f;

        [Header("Step Feedback")]
        [SerializeField, Min(0f)] private float minimumStepImpactHeight = 0.08f;
        [SerializeField, Min(0f)] private float stepImpactCooldown = 0.13f;

        [Header("Input")]
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.08f;

        private CharacterController characterController;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private float lastGroundedTime;
        private float headBaseHeight;
        private float headHeightVelocity;
        private float controllerHeightVelocity;
        private float currentControllerHeight;
        private float lastStepImpactTime;
        private float lastSprintIntentTime;
        private float slideTimer;
        private float airborneTimer;
        private float heavyLandingSlowTimer;
        private float heavyLandingSlowImpact;
        private bool isResettingScene;
        private Vector3 slideDirection;
        private float currentSlideSpeed;
        private float slideBonusSpeed;
        private Vector3 ledgeClimbStart;
        private Vector3 ledgeClimbTarget;
        private float ledgeClimbTimer;
        private readonly Collider[] standCheckHits = new Collider[12];

        public Vector2 MoveInput { get; private set; }
        public float MoveAmount { get; private set; }
        public float Speed01 { get; private set; }
        public float VerticalSpeed => verticalVelocity;
        public bool JumpStartedThisFrame { get; private set; }
        public bool LandedThisFrame { get; private set; }
        public float LandingImpact { get; private set; }
        public bool HeavyLandingThisFrame { get; private set; }
        public float HeavyLandingImpact { get; private set; }
        public bool StepImpactThisFrame { get; private set; }
        public float StepImpact { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsSliding { get; private set; }
        public bool IsLedgeClimbing { get; private set; }
        public PlayerFrameInput LastInput { get; private set; }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            if (characterController == null)
            {
                characterController = gameObject.AddComponent<CharacterController>();
                characterController.radius = 0.5f;
                characterController.height = standingHeight;
                characterController.center = Vector3.up * (standingHeight * 0.5f);
            }

            currentControllerHeight = characterController.height > 0f ? characterController.height : standingHeight;

            if (playerHead == null)
            {
                Transform foundHead = transform.Find("PlayerHead");
                playerHead = foundHead != null ? foundHead : GetComponentInChildren<Camera>()?.transform.parent;
            }

            headBaseHeight = playerHead != null ? playerHead.localPosition.y : standingHeight * 0.9f;
            ApplyControllerHeight(standingHeight);
        }

        private void Update()
        {
            LastInput = PlayerInputReader.ReadFrameInput(mouseSensitivity);
            MoveInput = LastInput.Move;
            ResetTransientFeedback();

            if (IsLedgeClimbing)
            {
                UpdateLedgeClimb();
                return;
            }

            UpdateGrounding();
            UpdateStance();
            UpdateMovement();
        }

        private void ResetTransientFeedback()
        {
            JumpStartedThisFrame = false;
            LandedThisFrame = false;
            LandingImpact = 0f;
            HeavyLandingThisFrame = false;
            HeavyLandingImpact = 0f;
            StepImpactThisFrame = false;
            StepImpact = 0f;
        }

        private void UpdateGrounding()
        {
            IsGrounded = characterController.isGrounded;
            if (IsGrounded)
            {
                lastGroundedTime = Time.time;
                airborneTimer = 0f;
                if (verticalVelocity < 0f)
                {
                    verticalVelocity = groundedStickForce;
                }
            }
        }

        private void UpdateStance()
        {
            bool wantsCrouch = LastInput.CrouchHeld;
            bool canStand = !wantsCrouch && CanStandUp();
            IsCrouching = wantsCrouch || !canStand;

            float targetHeight = IsCrouching ? crouchingHeight : standingHeight;
            currentControllerHeight = Mathf.SmoothDamp(
                currentControllerHeight,
                targetHeight,
                ref controllerHeightVelocity,
                stanceSmoothTime);

            ApplyControllerHeight(currentControllerHeight);

            if (playerHead == null)
            {
                return;
            }

            float targetHeadHeight = IsCrouching ? Mathf.Min(headBaseHeight, crouchingHeight - 0.1f) : headBaseHeight;
            Vector3 localPosition = playerHead.localPosition;
            localPosition.y = Mathf.SmoothDamp(localPosition.y, targetHeadHeight, ref headHeightVelocity, stanceSmoothTime);
            playerHead.localPosition = localPosition;
        }

        private void UpdateMovement()
        {
            bool hasMoveInput = MoveInput.sqrMagnitude > 0.001f;
            bool wasRunning = IsRunning;
            float currentFlatSpeed = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z).magnitude;
            bool hasSprintIntent = LastInput.RunHeld && hasMoveInput && MoveInput.y > 0.1f;

            if (hasSprintIntent)
            {
                lastSprintIntentTime = Time.time;
            }

            bool canGraceSlide = Time.time - lastSprintIntentTime <= sprintSlideGraceTime;
            if (LastInput.CrouchPressed && (wasRunning || canGraceSlide) && IsGrounded && currentFlatSpeed >= minimumSlideStartSpeed)
            {
                StartSlide();
            }

            UpdateSlideState(hasMoveInput);
            IsRunning = hasSprintIntent && !IsCrouching && !IsSliding;

            float targetSpeed = (IsCrouching ? crouchSpeed : IsRunning ? runSpeed : walkSpeed) * HeavyLandingSpeedScale;
            Vector3 inputDirection = transform.right * MoveInput.x + transform.forward * MoveInput.y;
            currentSlideSpeed = IsSliding ? CurrentSlideSpeed : 0f;
            if (IsSliding)
            {
                ApplySlideSlopeAcceleration();
            }

            Vector3 targetHorizontalVelocity = IsSliding
                ? slideDirection * currentSlideSpeed
                : inputDirection * targetSpeed;
            float acceleration = IsGrounded ? groundAcceleration : airAcceleration;

            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                targetHorizontalVelocity,
                acceleration * Time.deltaTime);

            bool canUseCoyoteJump = Time.time - lastGroundedTime <= coyoteTime;
            if (LastInput.JumpPressed && canUseCoyoteJump && !IsCrouching)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                lastGroundedTime = -999f;
                JumpStartedThisFrame = true;
            }

            verticalVelocity += gravity * Time.deltaTime;

            bool wasGrounded = IsGrounded;
            float previousY = transform.position.y;
            float previousVerticalVelocity = verticalVelocity;
            Vector3 velocity = horizontalVelocity;
            velocity.y = verticalVelocity;
            characterController.Move(velocity * Time.deltaTime);
            IsGrounded = characterController.isGrounded;

            if (!wasGrounded && IsGrounded)
            {
                HandleLanding(previousVerticalVelocity);
            }
            else if (!IsGrounded)
            {
                airborneTimer += Time.deltaTime;
            }

            DetectStepImpact(previousY, wasGrounded);

            if (!IsGrounded)
            {
                TryStartLedgeClimb();
            }

            Vector3 flatVelocity = characterController.velocity;
            flatVelocity.y = 0f;

            MoveAmount = Mathf.InverseLerp(0f, runSpeed, flatVelocity.magnitude);
            Speed01 = targetSpeed <= 0f ? 0f : Mathf.Clamp01(flatVelocity.magnitude / runSpeed);
        }

        private float HeavyLandingSpeedScale
        {
            get
            {
                if (heavyLandingSlowTimer <= 0f)
                {
                    return 1f;
                }

                heavyLandingSlowTimer = Mathf.Max(0f, heavyLandingSlowTimer - Time.deltaTime);
                float recovery01 = heavyLandingSlowDuration <= 0f ? 1f : 1f - heavyLandingSlowTimer / heavyLandingSlowDuration;
                float slowedMultiplier = Mathf.Lerp(1f, heavyLandingSpeedMultiplier, heavyLandingSlowImpact);
                return Mathf.Lerp(slowedMultiplier, 1f, Mathf.SmoothStep(0f, 1f, recovery01));
            }
        }

        private void HandleLanding(float previousVerticalVelocity)
        {
            LandedThisFrame = true;
            float fallSpeed = Mathf.Max(0f, -previousVerticalVelocity);
            LandingImpact = Mathf.InverseLerp(-groundedStickForce, 18f, fallSpeed);

            bool fellLongEnough = airborneTimer >= heavyLandingMinimumFallTime;
            bool fellFastEnough = fallSpeed >= heavyLandingMinimumFallSpeed;
            if (fellLongEnough || fellFastEnough)
            {
                HeavyLandingThisFrame = true;
                HeavyLandingImpact = Mathf.Max(
                    Mathf.InverseLerp(heavyLandingMinimumFallTime, heavyLandingMinimumFallTime * 1.9f, airborneTimer),
                    Mathf.InverseLerp(heavyLandingMinimumFallSpeed, heavyLandingFullFallSpeed, fallSpeed));

                heavyLandingSlowImpact = HeavyLandingImpact;
                heavyLandingSlowTimer = heavyLandingSlowDuration;
                horizontalVelocity *= Mathf.Lerp(1f, heavyLandingHorizontalDamping, HeavyLandingImpact);
            }

            airborneTimer = 0f;
        }

        private void StartSlide()
        {
            IsSliding = true;
            slideTimer = slideDuration;
            slideBonusSpeed = 0f;

            Vector3 flatVelocity = horizontalVelocity;
            flatVelocity.y = 0f;
            slideDirection = flatVelocity.sqrMagnitude > 0.01f
                ? flatVelocity.normalized
                : transform.forward;

            horizontalVelocity = slideDirection * Mathf.Max(slideStartSpeed, flatVelocity.magnitude);
        }

        private void UpdateSlideState(bool hasMoveInput)
        {
            if (!IsSliding)
            {
                return;
            }

            slideTimer -= Time.deltaTime;

            if (!LastInput.CrouchHeld || !IsGrounded || slideTimer <= 0f)
            {
                IsSliding = false;
                slideBonusSpeed = 0f;
                return;
            }

            if (!hasMoveInput || slideSteerStrength <= 0f)
            {
                return;
            }

            Vector3 inputDirection = transform.right * MoveInput.x + transform.forward * MoveInput.y;
            if (inputDirection.sqrMagnitude > 0.001f)
            {
                slideDirection = Vector3.Slerp(
                    slideDirection,
                    inputDirection.normalized,
                    slideSteerStrength * Time.deltaTime).normalized;
            }
        }

        private float CurrentSlideSpeed
        {
            get
            {
                float slide01 = Mathf.Clamp01(1f - slideTimer / slideDuration);
                return Mathf.Min(Mathf.Lerp(slideStartSpeed, slideEndSpeed, slide01) + slideBonusSpeed, slideMaximumSlopeSpeed);
            }
        }

        private void ApplySlideSlopeAcceleration()
        {
            if (!TryGetGroundNormal(out Vector3 groundNormal))
            {
                return;
            }

            Vector3 downhillDirection = Vector3.ProjectOnPlane(Vector3.down, groundNormal);
            if (downhillDirection.sqrMagnitude < 0.001f)
            {
                slideBonusSpeed = Mathf.MoveTowards(slideBonusSpeed, 0f, slideUphillDeceleration * Time.deltaTime);
                return;
            }

            float downhillAlignment = Vector3.Dot(slideDirection.normalized, downhillDirection.normalized);
            if (downhillAlignment > 0f)
            {
                slideBonusSpeed += slideDownhillAcceleration * downhillAlignment * Time.deltaTime;
                slideBonusSpeed = Mathf.Min(slideBonusSpeed, Mathf.Max(0f, slideMaximumSlopeSpeed - slideEndSpeed));
                return;
            }

            slideBonusSpeed = Mathf.MoveTowards(
                slideBonusSpeed,
                0f,
                slideUphillDeceleration * -downhillAlignment * Time.deltaTime);
        }

        private bool TryGetGroundNormal(out Vector3 groundNormal)
        {
            Vector3 origin = transform.position + Vector3.up * 0.2f;
            float castDistance = currentControllerHeight * 0.5f + 0.35f;

            if (Physics.SphereCast(
                origin,
                Mathf.Max(0.05f, characterController.radius * 0.9f),
                Vector3.down,
                out RaycastHit hit,
                castDistance,
                ~0,
                QueryTriggerInteraction.Ignore))
            {
                groundNormal = hit.normal;
                return true;
            }

            groundNormal = Vector3.up;
            return false;
        }

        private void DetectStepImpact(float previousY, bool wasGrounded)
        {
            if (!wasGrounded || !IsGrounded || MoveInput.sqrMagnitude < 0.1f)
            {
                return;
            }

            float climbAmount = transform.position.y - previousY;
            float maximumStepHeight = characterController.stepOffset + characterController.skinWidth + 0.03f;
            bool climbedStep = climbAmount >= minimumStepImpactHeight && climbAmount <= maximumStepHeight;
            if (!climbedStep || Time.time - lastStepImpactTime < stepImpactCooldown)
            {
                return;
            }

            lastStepImpactTime = Time.time;
            StepImpactThisFrame = true;
            StepImpact = Mathf.InverseLerp(minimumStepImpactHeight, maximumStepHeight, climbAmount);
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.collider != null && hit.collider.GetComponentInParent<NeighborBrain>() != null)
            {
                ResetCurrentScene();
                return;
            }

            if (!IsSliding || hit.rigidbody == null || hit.rigidbody.isKinematic)
            {
                return;
            }

            if (hit.moveDirection.y < -0.25f)
            {
                return;
            }

            Vector3 impulseDirection = slideDirection.sqrMagnitude > 0.001f
                ? slideDirection
                : new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z).normalized;

            if (impulseDirection.sqrMagnitude < 0.001f)
            {
                impulseDirection = transform.forward;
            }

            float speed01 = Mathf.InverseLerp(slideEndSpeed, slideStartSpeed, currentSlideSpeed);
            Vector3 impulse = impulseDirection.normalized * (slideObjectImpulse * speed01);
            impulse += Vector3.up * (slideObjectUpwardImpulse * speed01);

            hit.rigidbody.AddForceAtPosition(impulse, hit.point, ForceMode.Impulse);
        }

        private void ResetCurrentScene()
        {
            if (isResettingScene)
            {
                return;
            }

            isResettingScene = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private bool TryStartLedgeClimb()
        {
            if (!enableLedgeClimb || IsLedgeClimbing || IsCrouching || IsSliding || MoveInput.y < 0.1f)
            {
                return false;
            }

            if (verticalVelocity > 4f)
            {
                return false;
            }

            Vector3 forward = transform.forward;
            Vector3 wallRayOrigin = transform.position + Vector3.up * (ledgeMinimumHeight + 0.1f);
            if (!Physics.Raycast(wallRayOrigin, forward, out RaycastHit wallHit, ledgeCheckDistance, ledgeClimbMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (wallHit.normal.y > 0.25f)
            {
                return false;
            }

            float topProbeHeight = ledgeMaximumHeight + 0.35f;
            Vector3 topRayOrigin = wallHit.point + forward * ledgeTopProbeForwardOffset + Vector3.up * topProbeHeight;
            if (!Physics.Raycast(topRayOrigin, Vector3.down, out RaycastHit topHit, topProbeHeight + 0.5f, ledgeClimbMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            float ledgeHeight = topHit.point.y - transform.position.y;
            if (ledgeHeight < ledgeMinimumHeight || ledgeHeight > ledgeMaximumHeight)
            {
                return false;
            }

            Vector3 targetPosition = new Vector3(topHit.point.x, topHit.point.y + 0.03f, topHit.point.z);
            if (!HasStandingClearance(targetPosition))
            {
                return false;
            }

            StartLedgeClimb(targetPosition);
            return true;
        }

        private void StartLedgeClimb(Vector3 targetPosition)
        {
            IsLedgeClimbing = true;
            IsSliding = false;
            IsRunning = false;
            IsCrouching = false;
            ledgeClimbTimer = 0f;
            ledgeClimbStart = transform.position;
            ledgeClimbTarget = targetPosition;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            characterController.enabled = false;
        }

        private void UpdateLedgeClimb()
        {
            ledgeClimbTimer += Time.deltaTime;
            float climb01 = Mathf.Clamp01(ledgeClimbTimer / ledgeClimbDuration);
            float easedClimb = Mathf.SmoothStep(0f, 1f, climb01);

            transform.position = Vector3.Lerp(ledgeClimbStart, ledgeClimbTarget, easedClimb);

            if (climb01 < 1f)
            {
                return;
            }

            characterController.enabled = true;
            IsLedgeClimbing = false;
            IsGrounded = true;
            lastGroundedTime = Time.time;
            verticalVelocity = groundedStickForce;
        }

        private bool HasStandingClearance(Vector3 feetPosition)
        {
            float radius = Mathf.Max(0.05f, characterController.radius * 0.95f);
            Vector3 bottom = feetPosition + Vector3.up * (radius + characterController.skinWidth);
            Vector3 top = feetPosition + Vector3.up * (standingHeight - radius);
            int hitCount = Physics.OverlapCapsuleNonAlloc(
                bottom,
                top,
                radius,
                standCheckHits,
                ledgeClimbMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = standCheckHits[i];
                if (hit != null && !hit.transform.IsChildOf(transform))
                {
                    return false;
                }
            }

            return hitCount < standCheckHits.Length;
        }

        private bool CanStandUp()
        {
            if (standingHeight <= crouchingHeight)
            {
                return true;
            }

            float radius = Mathf.Max(0.05f, characterController.radius * 0.95f);
            Vector3 bottom = transform.position + Vector3.up * (radius + characterController.skinWidth);
            Vector3 top = transform.position + Vector3.up * (standingHeight - radius);
            int hitCount = Physics.OverlapCapsuleNonAlloc(
                bottom,
                top,
                radius,
                standCheckHits,
                crouchObstructionMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = standCheckHits[i];
                if (hit != null && !hit.transform.IsChildOf(transform))
                {
                    return false;
                }
            }

            return hitCount < standCheckHits.Length;
        }

        private void ApplyControllerHeight(float height)
        {
            characterController.height = height;
            characterController.center = Vector3.up * (height * 0.5f);
        }

        private void Reset()
        {
            playerHead = transform.Find("PlayerHead");
        }
    }
}
