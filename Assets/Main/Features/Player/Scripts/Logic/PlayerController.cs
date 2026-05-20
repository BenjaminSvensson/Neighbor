using UnityEngine;

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

        [Header("Crouch")]
        [SerializeField, Min(0.1f)] private float standingHeight = 2f;
        [SerializeField, Min(0.1f)] private float crouchingHeight = 1.2f;
        [SerializeField, Min(0f)] private float stanceSmoothTime = 0.08f;
        [SerializeField] private LayerMask crouchObstructionMask = ~0;

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
        private readonly Collider[] standCheckHits = new Collider[12];

        public Vector2 MoveInput { get; private set; }
        public float MoveAmount { get; private set; }
        public float Speed01 { get; private set; }
        public float VerticalSpeed => verticalVelocity;
        public bool JumpStartedThisFrame { get; private set; }
        public bool LandedThisFrame { get; private set; }
        public float LandingImpact { get; private set; }
        public bool StepImpactThisFrame { get; private set; }
        public float StepImpact { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsCrouching { get; private set; }
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

            UpdateGrounding();
            UpdateStance();
            UpdateMovement();
        }

        private void UpdateGrounding()
        {
            IsGrounded = characterController.isGrounded;
            if (IsGrounded)
            {
                lastGroundedTime = Time.time;
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
            JumpStartedThisFrame = false;
            LandedThisFrame = false;
            LandingImpact = 0f;
            StepImpactThisFrame = false;
            StepImpact = 0f;

            bool hasMoveInput = MoveInput.sqrMagnitude > 0.001f;
            IsRunning = LastInput.RunHeld && hasMoveInput && !IsCrouching && MoveInput.y > 0.1f;

            float targetSpeed = IsCrouching ? crouchSpeed : IsRunning ? runSpeed : walkSpeed;
            Vector3 inputDirection = transform.right * MoveInput.x + transform.forward * MoveInput.y;
            Vector3 targetHorizontalVelocity = inputDirection * targetSpeed;
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
                LandedThisFrame = true;
                LandingImpact = Mathf.InverseLerp(groundedStickForce, -18f, previousVerticalVelocity);
            }

            DetectStepImpact(previousY, wasGrounded);

            Vector3 flatVelocity = characterController.velocity;
            flatVelocity.y = 0f;

            MoveAmount = Mathf.InverseLerp(0f, runSpeed, flatVelocity.magnitude);
            Speed01 = targetSpeed <= 0f ? 0f : Mathf.Clamp01(flatVelocity.magnitude / runSpeed);
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
