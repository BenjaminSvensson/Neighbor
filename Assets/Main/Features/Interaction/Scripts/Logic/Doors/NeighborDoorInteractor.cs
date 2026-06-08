using Neighbor.Main.Features.Neighbor;
using System.Collections.Generic;
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
        [SerializeField] private bool closeDoorsBehindNeighbor = true;
        [SerializeField, Min(0f)] private float closeBehindDelay = 0.15f;
        [SerializeField, Min(0f)] private float closeBehindDistance = 1.5f;
        [SerializeField, Min(0f)] private float closeBehindClearDistance = 0.65f;
        [SerializeField, Min(0f)] private float closeBehindFailsafeDelay = 2.5f;
        [SerializeField] private bool requirePassingThroughDoorToClose = true;

        private readonly Collider[] hits = new Collider[8];
        private readonly List<OpenedDoorTracker> openedDoors = new();
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
            UpdateOpenedDoors();

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
                    TrackOpenedDoor(door);
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
                if (kickingDoor.TryOpenForNeighbor(transform))
                {
                    TrackOpenedDoor(kickingDoor);
                }

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

            if (kickingDoor.TryKickOpenForNeighbor(transform))
            {
                TrackOpenedDoor(kickingDoor);
            }

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
                if (lockedOutDoor.TryOpenForNeighbor(transform))
                {
                    TrackOpenedDoor(lockedOutDoor);
                }

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

        private void TrackOpenedDoor(Door door)
        {
            if (!closeDoorsBehindNeighbor || door == null)
            {
                return;
            }

            bool openedFromDefaultSide = door.IsOnDefaultOpeningSide(transform.position);
            for (int i = 0; i < openedDoors.Count; i++)
            {
                if (openedDoors[i].Door == door)
                {
                    return;
                }
            }

            openedDoors.Add(new OpenedDoorTracker(
                door,
                openedFromDefaultSide,
                Time.time + closeBehindDelay,
                Time.time + closeBehindFailsafeDelay));
        }

        private void UpdateOpenedDoors()
        {
            if (!closeDoorsBehindNeighbor)
            {
                openedDoors.Clear();
                return;
            }

            for (int i = openedDoors.Count - 1; i >= 0; i--)
            {
                OpenedDoorTracker tracker = openedDoors[i];
                Door door = tracker.Door;
                if (door == null || !door.IsOpen)
                {
                    openedDoors.RemoveAt(i);
                    continue;
                }

                if (Time.time < tracker.CloseAfterTime)
                {
                    continue;
                }

                bool hasPassedThrough = door.IsOnDefaultOpeningSide(transform.position) != tracker.OpenedFromDefaultSide;
                bool failsafeReady = Time.time >= tracker.FailsafeCloseTime;
                if (requirePassingThroughDoorToClose && !hasPassedThrough && !failsafeReady)
                {
                    continue;
                }

                float distanceToDoor = Vector3.Distance(transform.position, door.transform.position);
                bool isClearOfDoorway = distanceToDoor >= closeBehindClearDistance;
                bool hasLeftCloseRange = closeBehindDistance > 0f && distanceToDoor > closeBehindDistance;
                if (!isClearOfDoorway && !failsafeReady)
                {
                    continue;
                }

                if (hasLeftCloseRange && !hasPassedThrough && !failsafeReady)
                {
                    continue;
                }

                door.Close();
                openedDoors.RemoveAt(i);
            }
        }

        private readonly struct OpenedDoorTracker
        {
            public OpenedDoorTracker(Door door, bool openedFromDefaultSide, float closeAfterTime, float failsafeCloseTime)
            {
                Door = door;
                OpenedFromDefaultSide = openedFromDefaultSide;
                CloseAfterTime = closeAfterTime;
                FailsafeCloseTime = failsafeCloseTime;
            }

            public Door Door { get; }
            public bool OpenedFromDefaultSide { get; }
            public float CloseAfterTime { get; }
            public float FailsafeCloseTime { get; }
        }
    }
}
