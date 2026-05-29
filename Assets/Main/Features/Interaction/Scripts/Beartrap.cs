using Neighbor.Main.Features.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Neighbor.Main.Features.Interaction
{
    [RequireComponent(typeof(Pickupable))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Beartrap : MonoBehaviour, IPrimaryUseInteractable, IHoldInteractable, IPickupInteractionOverride, IPickupLifecycleReceiver
    {
        private enum TrapState
        {
            Closed,
            Open,
            Triggered
        }

        [Header("State")]
        [SerializeField] private TrapState startingState = TrapState.Closed;
        [SerializeField] private bool startsPlaced;
        [SerializeField, Min(0.05f)] private float openCloseDuration = 0.22f;
        [SerializeField, Min(0.05f)] private float escapeHoldDuration = 1.4f;

        [Header("Jaw Animation")]
        [SerializeField] private Transform leftJaw;
        [SerializeField] private Transform rightJaw;
        [SerializeField] private Vector3 leftClosedEuler = new Vector3(0f, 0f, -10f);
        [SerializeField] private Vector3 leftOpenEuler = new Vector3(0f, 0f, -68f);
        [SerializeField] private Vector3 rightClosedEuler = new Vector3(0f, 0f, 10f);
        [SerializeField] private Vector3 rightOpenEuler = new Vector3(0f, 0f, 68f);

        private Pickupable pickupable;
        private Rigidbody trapBody;
        private Collider[] ownColliders;
        private TrapState state;
        private bool isPlaced;
        private float openAmount;
        private float targetOpenAmount;
        private float escapeHoldTimer;
        private PlayerController stuckPlayer;
        private Rigidbody stuckRigidbody;
        private NavMeshAgent stuckAgent;

        private bool IsMoving => !Mathf.Approximately(openAmount, targetOpenAmount);

        private void Awake()
        {
            pickupable = GetComponent<Pickupable>();
            trapBody = GetComponent<Rigidbody>();
            ownColliders = GetComponentsInChildren<Collider>();
        }

        private void Start()
        {
            state = startingState;
            isPlaced = startsPlaced;
            targetOpenAmount = state == TrapState.Open ? 1f : 0f;
            openAmount = targetOpenAmount;
            ApplyJawPose();
            ConfigureTrapBodyForState();
        }

        private void Update()
        {
            float step = openCloseDuration <= 0f ? 1f : Time.deltaTime / openCloseDuration;
            openAmount = Mathf.MoveTowards(openAmount, targetOpenAmount, step);
            ApplyJawPose();
            KeepTargetStuck();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (state != TrapState.Open || IsMoving || other == null || IsOwnCollider(other))
            {
                return;
            }

            Trigger(other);
        }

        public bool CanPickup(PlayerInteractor interactor)
        {
            return state == TrapState.Closed && !IsMoving && stuckPlayer == null && stuckRigidbody == null && stuckAgent == null;
        }

        public void OnPickupStarted(Pickupable pickupable, PlayerInteractor interactor)
        {
            isPlaced = false;
            SetState(TrapState.Closed);
            ReleaseStuckTarget();
            if (trapBody != null)
            {
                trapBody.isKinematic = false;
            }
        }

        public void OnPickupPlaced(Pickupable pickupable)
        {
            isPlaced = true;
        }

        public bool CanPrimaryUse(PlayerInteractor interactor)
        {
            return isPlaced && pickupable != null && !pickupable.IsHeld && state == TrapState.Closed && !IsMoving;
        }

        public void PrimaryUse(PlayerInteractor interactor)
        {
            SetState(TrapState.Open);
        }

        public bool CanHoldInteract(PlayerInteractor interactor)
        {
            if (state != TrapState.Triggered || stuckPlayer == null || interactor == null)
            {
                return false;
            }

            PlayerController interactingPlayer = interactor.GetComponentInParent<PlayerController>();
            return interactingPlayer == stuckPlayer;
        }

        public void BeginHoldInteract(PlayerInteractor interactor)
        {
            escapeHoldTimer = 0f;
        }

        public void HoldInteract(PlayerInteractor interactor, float deltaTime)
        {
            if (!CanHoldInteract(interactor))
            {
                return;
            }

            escapeHoldTimer += deltaTime;
            if (escapeHoldTimer >= escapeHoldDuration)
            {
                ReleaseStuckTarget();
                SetState(TrapState.Open);
            }
        }

        public void EndHoldInteract(PlayerInteractor interactor, bool completed)
        {
            if (!completed)
            {
                escapeHoldTimer = 0f;
            }
        }

        private void Trigger(Collider triggeringCollider)
        {
            SetState(TrapState.Triggered);

            stuckPlayer = triggeringCollider.GetComponentInParent<PlayerController>();
            if (stuckPlayer != null)
            {
                stuckPlayer.SetBeartrapLocked(true);
                return;
            }

            stuckAgent = triggeringCollider.GetComponentInParent<NavMeshAgent>();
            if (stuckAgent != null)
            {
                stuckAgent.velocity = Vector3.zero;
                stuckAgent.isStopped = true;
                if (stuckAgent.enabled && stuckAgent.isOnNavMesh)
                {
                    stuckAgent.ResetPath();
                }
            }

            stuckRigidbody = triggeringCollider.attachedRigidbody;
            if (stuckRigidbody != null && stuckRigidbody != trapBody)
            {
                stuckRigidbody.linearVelocity = Vector3.zero;
                stuckRigidbody.angularVelocity = Vector3.zero;
                stuckRigidbody.isKinematic = true;
            }
        }

        private void SetState(TrapState nextState)
        {
            state = nextState;
            targetOpenAmount = state == TrapState.Open ? 1f : 0f;
            ConfigureTrapBodyForState();
        }

        private void ConfigureTrapBodyForState()
        {
            if (trapBody == null || pickupable == null || pickupable.IsHeld)
            {
                return;
            }

            trapBody.isKinematic = state != TrapState.Closed;
            trapBody.linearVelocity = Vector3.zero;
            trapBody.angularVelocity = Vector3.zero;
        }

        private void KeepTargetStuck()
        {
            if (stuckRigidbody != null)
            {
                stuckRigidbody.linearVelocity = Vector3.zero;
                stuckRigidbody.angularVelocity = Vector3.zero;
            }

            if (stuckAgent != null)
            {
                stuckAgent.velocity = Vector3.zero;
            }
        }

        private void ReleaseStuckTarget()
        {
            if (stuckPlayer != null)
            {
                stuckPlayer.SetBeartrapLocked(false);
            }

            if (stuckAgent != null)
            {
                stuckAgent.isStopped = false;
            }

            stuckPlayer = null;
            stuckRigidbody = null;
            stuckAgent = null;
            escapeHoldTimer = 0f;
        }

        private void ApplyJawPose()
        {
            if (leftJaw != null)
            {
                leftJaw.localRotation = Quaternion.Euler(Vector3.Lerp(leftClosedEuler, leftOpenEuler, openAmount));
            }

            if (rightJaw != null)
            {
                rightJaw.localRotation = Quaternion.Euler(Vector3.Lerp(rightClosedEuler, rightOpenEuler, openAmount));
            }
        }

        private bool IsOwnCollider(Collider other)
        {
            if (ownColliders == null)
            {
                return false;
            }

            foreach (Collider ownCollider in ownColliders)
            {
                if (ownCollider == other)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
