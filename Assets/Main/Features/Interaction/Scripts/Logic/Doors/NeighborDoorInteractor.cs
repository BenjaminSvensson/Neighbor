using Neighbor.Main.Features.Neighbor;
using UnityEngine;

namespace Neighbor.Main.Features.Interaction
{
    public sealed class NeighborDoorInteractor : MonoBehaviour
    {
        [SerializeField] private NeighborMotor motor;
        [SerializeField, Min(0f)] private float checkRadius = 0.55f;
        [SerializeField, Min(0f)] private float checkDistance = 1.2f;
        [SerializeField] private LayerMask doorMask = ~0;
        [SerializeField, Min(0f)] private float interactionCooldown = 0.35f;
        [SerializeField, Min(0.1f)] private float blockedDoorKickDuration = 2.25f;
        [SerializeField, Min(0.05f)] private float blockedDoorKickInterval = 0.48f;
        [SerializeField, Min(0f)] private float blockedDoorKickAbortDistance = 2.2f;
        [SerializeField, Min(0.05f)] private float lockedDoorRetryInterval = 0.7f;
        [SerializeField, Min(0f)] private float lockedDoorAbortDistance = 2.2f;
        [SerializeField, Min(0f)] private float kickTurnSharpness = 12f;

        private readonly Collider[] hits = new Collider[8];
        private float nextInteractionTime;
        private Door kickingDoor;
        private Door lockedOutDoor;
        private float kickCompleteTime;
        private float nextKickFeedbackTime;
        private float nextLockedDoorFeedbackTime;

        private void Awake()
        {
            motor = motor != null ? motor : GetComponent<NeighborMotor>();
        }

        private void Update()
        {
            if (kickingDoor != null)
            {
                UpdateBlockedDoorKick();
                return;
            }

            if (lockedOutDoor != null)
            {
                UpdateLockedDoorWait();
                return;
            }

            if (Time.time < nextInteractionTime)
            {
                return;
            }

            Vector3 center = transform.position + Vector3.up + transform.forward * checkDistance;
            int hitCount = Physics.OverlapSphereNonAlloc(center, checkRadius, hits, doorMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Door door = hits[i] != null ? hits[i].GetComponentInParent<Door>() : null;
                if (door == null)
                {
                    continue;
                }

                if (door.IsBlocked)
                {
                    BeginBlockedDoorKick(door);
                    return;
                }

                if (door.IsLocked && !door.NeighborCanUnlock)
                {
                    BeginLockedDoorWait(door);
                    return;
                }

                bool opened = door.TryOpenForNeighbor(transform);
                nextInteractionTime = Time.time + interactionCooldown;
                if (opened)
                {
                    return;
                }
            }
        }

        private void BeginBlockedDoorKick(Door door)
        {
            if (door == null || !door.NeighborCanKickBlockedDoor)
            {
                nextInteractionTime = Time.time + interactionCooldown;
                return;
            }

            kickingDoor = door;
            kickCompleteTime = Time.time + blockedDoorKickDuration;
            nextKickFeedbackTime = Time.time;
            motor?.Stop();
            UpdateBlockedDoorKick();
        }

        private void BeginLockedDoorWait(Door door)
        {
            lockedOutDoor = door;
            nextLockedDoorFeedbackTime = Time.time;
            motor?.Stop();
            UpdateLockedDoorWait();
        }

        private void UpdateBlockedDoorKick()
        {
            if (kickingDoor == null)
            {
                nextInteractionTime = Time.time + interactionCooldown;
                return;
            }

            motor?.Stop();
            motor?.FaceTowards(kickingDoor.transform.position, kickTurnSharpness);

            if (Vector3.Distance(transform.position, kickingDoor.transform.position) > blockedDoorKickAbortDistance)
            {
                CancelBlockedDoorKick();
                return;
            }

            if (!kickingDoor.IsBlocked)
            {
                kickingDoor.TryOpenForNeighbor(transform);
                FinishBlockedDoorKick();
                return;
            }

            if (Time.time >= nextKickFeedbackTime)
            {
                kickingDoor.PlayNeighborKickFeedback();
                nextKickFeedbackTime = Time.time + blockedDoorKickInterval;
            }

            if (Time.time < kickCompleteTime)
            {
                return;
            }

            kickingDoor.TryKickOpenForNeighbor(transform);
            FinishBlockedDoorKick();
        }

        private void UpdateLockedDoorWait()
        {
            if (lockedOutDoor == null)
            {
                nextInteractionTime = Time.time + interactionCooldown;
                return;
            }

            motor?.Stop();
            motor?.FaceTowards(lockedOutDoor.transform.position, kickTurnSharpness);

            if (Vector3.Distance(transform.position, lockedOutDoor.transform.position) > lockedDoorAbortDistance)
            {
                CancelLockedDoorWait();
                return;
            }

            if (lockedOutDoor.IsBlocked)
            {
                Door blockedDoor = lockedOutDoor;
                lockedOutDoor = null;
                BeginBlockedDoorKick(blockedDoor);
                return;
            }

            if (!lockedOutDoor.IsLocked || lockedOutDoor.NeighborCanUnlock)
            {
                lockedOutDoor.TryOpenForNeighbor(transform);
                FinishLockedDoorWait();
                return;
            }

            if (Time.time >= nextLockedDoorFeedbackTime)
            {
                lockedOutDoor.TryOpenForNeighbor(transform);
                nextLockedDoorFeedbackTime = Time.time + lockedDoorRetryInterval;
            }
        }

        private void FinishBlockedDoorKick()
        {
            kickingDoor = null;
            nextInteractionTime = Time.time + interactionCooldown;
        }

        private void CancelBlockedDoorKick()
        {
            kickingDoor = null;
            nextInteractionTime = Time.time + interactionCooldown;
        }

        private void FinishLockedDoorWait()
        {
            lockedOutDoor = null;
            nextInteractionTime = Time.time + interactionCooldown;
        }

        private void CancelLockedDoorWait()
        {
            lockedOutDoor = null;
            nextInteractionTime = Time.time + interactionCooldown;
        }
    }
}
