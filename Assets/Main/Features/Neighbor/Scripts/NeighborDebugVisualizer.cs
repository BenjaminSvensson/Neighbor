using Neighbor.Main.Features.Interaction;
using UnityEngine;

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine.AI;
#endif

namespace Neighbor.Main.Features.Neighbor
{
    [DisallowMultipleComponent]
    public sealed class NeighborDebugVisualizer : MonoBehaviour
    {
#if UNITY_EDITOR
        [Header("References")]
        [SerializeField] private NeighborBrain brain;
        [SerializeField] private NeighborMotor motor;
        [SerializeField] private NeighborVision vision;
        [SerializeField] private NeighborHearing hearing;
        [SerializeField] private NeighborDoorInteractor doorInteractor;

        [Header("Editor Display")]
        [SerializeField] private bool showOverheadStatus = true;
        [SerializeField] private bool showVision = true;
        [SerializeField] private bool showPath = true;
        [SerializeField] private bool showCurrentGoal = true;
        [SerializeField] private bool showLastKnownPlayer = true;
        [SerializeField] private bool showRecentSound = true;
        [SerializeField] private bool showInteractionTargets = true;
        [SerializeField, Min(0.5f)] private float overheadHeight = 3.1f;
        [SerializeField, Min(0.1f)] private float markerSize = 0.3f;
        [SerializeField, Min(0.1f)] private float recentSoundDisplayTime = 5f;
        [SerializeField, Min(0.1f)] private float recoveryDisplayTime = 5f;

        private GUIStyle statusStyle;
        private GUIStyle markerStyle;

        private void OnDrawGizmos()
        {
            ResolveReferences();
            EnsureStyles();

            if (showVision)
            {
                DrawVision();
            }

            if (!Application.isPlaying)
            {
                DrawStatus("NEIGHBOR AI\nEnter Play Mode for live thinking data", new Color(0.35f, 0.85f, 1f));
                return;
            }

            Color stateColor = GetStateColor();
            DrawSuspicionHalo(stateColor);

            if (showPath)
            {
                DrawPath();
            }

            if (showCurrentGoal)
            {
                DrawCurrentGoal();
            }

            if (showLastKnownPlayer)
            {
                DrawLastKnownPlayer();
            }

            if (showRecentSound)
            {
                DrawRecentSound();
            }

            if (showInteractionTargets)
            {
                DrawInteractionTargets();
            }

            if (showOverheadStatus)
            {
                DrawStatus(BuildStatusText(), stateColor);
            }
        }

        private void ResolveReferences()
        {
            brain = brain != null ? brain : GetComponent<NeighborBrain>();
            motor = motor != null ? motor : GetComponent<NeighborMotor>();
            vision = vision != null ? vision : GetComponent<NeighborVision>();
            hearing = hearing != null ? hearing : GetComponent<NeighborHearing>();
            doorInteractor = doorInteractor != null ? doorInteractor : GetComponent<NeighborDoorInteractor>();
        }

        private void EnsureStyles()
        {
            statusStyle ??= new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                richText = true,
                padding = new RectOffset(7, 7, 5, 5)
            };
            markerStyle ??= new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                richText = true
            };
        }

        private string BuildStatusText()
        {
            if (brain == null)
            {
                return "NEIGHBOR AI\nMissing NeighborBrain";
            }

            StringBuilder text = new StringBuilder(240);
            text.Append("<b>").Append(brain.CurrentState).Append("</b>");
            text.Append("  |  Suspicion ").Append(brain.Suspicion.ToString("P0"));
            text.Append(" (").Append(brain.CurrentSuspicionLevel).Append(')');
            text.Append("\nPlayer: ").Append(brain.IsPlayerVisible ? "<color=#66FF88>VISIBLE</color>" : "<color=#FF7777>not visible</color>");

            if (motor != null)
            {
                text.Append("  |  Speed ").Append(motor.CurrentSpeed.ToString("0.0"));
                text.Append(" / ").Append(motor.ConfiguredSpeed.ToString("0.0"));
                text.Append("  |  Remaining ").Append(FormatDistance(motor.RemainingDistance));
                if (motor.IsPaused)
                {
                    text.Append("  |  PAUSED");
                }

                if (motor.IsAvoidingDynamicObstacle)
                {
                    text.Append("  |  DETOUR");
                }

                if (motor.NoProgressAttemptCount > 0)
                {
                    text.Append("  |  Blocked tries ").Append(motor.NoProgressAttemptCount);
                    text.Append('/').Append(motor.MaximumNoProgressAttempts);
                }

                if (motor.LastRecoveryAge <= recoveryDisplayTime)
                {
                    text.Append("  |  RECOVERED ").Append(motor.LastRecoveryAge.ToString("0.0")).Append("s ago");
                }

                if (motor.IsTraversingSpecialMove)
                {
                    text.Append("  |  Traverse: ").Append(motor.CurrentTraversalAnimationPhase);
                }
            }

            string focus = GetFocusDescription();
            if (!string.IsNullOrEmpty(focus))
            {
                text.Append("\nFocus: ").Append(focus);
            }

            if (brain.IsWaitingAtGoal)
            {
                text.Append("  |  Wait ").Append(brain.WaitTimeRemaining.ToString("0.0")).Append('s');
            }

            if (brain.CurrentState == NeighborBrain.BehaviorState.HuntMode)
            {
                text.Append("\nHunt: ").Append(brain.HuntTimeRemaining.ToString("0.0")).Append("s");
                text.Append("  |  Search points ").Append(brain.VisitedSearchPointCount);
                text.Append('/').Append(brain.RequiredSearchPointVisits);
            }
            else if (brain.IsVerifyingLastSeenPosition)
            {
                text.Append("\nChase loss: VERIFYING LAST SEEN");
                if (brain.LastSeenVerificationTimeRemaining > 0f)
                {
                    text.Append("  |  Sweep ").Append(brain.LastSeenVerificationTimeRemaining.ToString("0.0")).Append('s');
                }
            }
            else if (brain.IsPostEncounterVigilant)
            {
                text.Append("\nPost-encounter vigilance: ");
                text.Append(brain.PostEncounterVigilanceTimeRemaining.ToString("0.0")).Append("s");
                text.Append("  |  TASKS SUPPRESSED");
            }

            if (doorInteractor != null && doorInteractor.IsInteractingWithDoor)
            {
                text.Append("\nDoor: ");
                text.Append(doorInteractor.IsKickingBlockedDoor
                    ? "KICKING BLOCKER"
                    : doorInteractor.IsReactingToLockedDoor
                        ? "LOCKED OUT"
                        : "OPENING");
            }

            if (hearing != null && hearing.LastHeardAge <= recentSoundDisplayTime)
            {
                text.Append("\nHeard: loud ").Append(hearing.LastHeardLoudness.ToString("0.00"));
                text.Append("  urgent ").Append(hearing.LastHeardUrgency.ToString("0.00"));
                text.Append("  ").Append(hearing.LastHeardAge.ToString("0.0")).Append("s ago");
            }

            return text.ToString();
        }

        private string GetFocusDescription()
        {
            if (brain.IsVerifyingLastSeenPosition)
            {
                return "Verify last seen player position";
            }

            if (brain.IsPostEncounterVigilant && brain.CurrentState == NeighborBrain.BehaviorState.Wander)
            {
                return "Cautious post-encounter patrol";
            }

            if (brain.CurrentTaskLocation != null)
            {
                return $"Task {brain.CurrentTaskLocation.name} ({brain.ActiveTaskAnimationPhase})";
            }

            if (brain.CurrentSearchPoint != null)
            {
                return brain.CurrentUnexpectedOpenDoor != null
                    ? $"Room beyond {brain.CurrentUnexpectedOpenDoor.name} via {brain.CurrentSearchPoint.name}"
                    : $"Search point {brain.CurrentSearchPoint.name}";
            }

            if (brain.CurrentHideSpot != null)
            {
                return $"Hide spot {brain.CurrentHideSpot.name}";
            }

            if (brain.CurrentInvestigationSource != null)
            {
                return brain.CurrentUnexpectedOpenDoor != null
                    ? $"Unexpected open door {brain.CurrentUnexpectedOpenDoor.name}"
                    : $"Noise source {brain.CurrentInvestigationSource.name}";
            }

            return brain.CurrentState == NeighborBrain.BehaviorState.Chase && brain.Player != null
                ? $"Player ({Vector3.Distance(transform.position, brain.Player.position):0.0}m)"
                : null;
        }

        private void DrawStatus(string text, Color color)
        {
            if (!showOverheadStatus)
            {
                return;
            }

            statusStyle.normal.textColor = color;
            Handles.Label(transform.position + Vector3.up * overheadHeight, text, statusStyle);
        }

        private void DrawVision()
        {
            if (vision == null)
            {
                return;
            }

            Vector3 origin = vision.EyePosition;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
            Vector3 left = Quaternion.AngleAxis(-vision.ViewAngle * 0.5f, Vector3.up) * forward;
            Vector3 right = Quaternion.AngleAxis(vision.ViewAngle * 0.5f, Vector3.up) * forward;
            Color visionColor = brain != null && brain.IsPlayerVisible
                ? new Color(1f, 0.18f, 0.12f, 0.22f)
                : new Color(1f, 0.75f, 0.1f, 0.12f);

            Handles.color = visionColor;
            Handles.DrawSolidArc(origin, Vector3.up, left, vision.ViewAngle, vision.ViewDistance);
            Handles.color = new Color(visionColor.r, visionColor.g, visionColor.b, 0.8f);
            Handles.DrawLine(origin, origin + left * vision.ViewDistance);
            Handles.DrawLine(origin, origin + right * vision.ViewDistance);
            Handles.DrawWireArc(origin, Vector3.up, left, vision.ViewAngle, vision.ViewDistance);
            Handles.DrawWireDisc(origin, Vector3.up, vision.CloseDetectionDistance);

            Vector3 rightAxis = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 upwardLimit = Quaternion.AngleAxis(-vision.MaximumUpwardViewAngle, rightAxis) * forward;
            Handles.DrawDottedLine(origin, origin + upwardLimit * vision.ViewDistance, 4f);
        }

        private void DrawSuspicionHalo(Color stateColor)
        {
            if (brain == null)
            {
                return;
            }

            Vector3 center = transform.position + Vector3.up * 2.25f;
            float radius = Mathf.Lerp(0.35f, 0.75f, brain.Suspicion);
            Handles.color = new Color(stateColor.r, stateColor.g, stateColor.b, 0.25f);
            Handles.DrawSolidArc(center, Vector3.up, Vector3.forward, 360f * brain.Suspicion, radius);
            Handles.color = stateColor;
            Handles.DrawWireDisc(center, Vector3.up, radius);
        }

        private void DrawPath()
        {
            NavMeshAgent agent = GetComponent<NavMeshAgent>();
            if (agent == null || !agent.enabled || !agent.hasPath || agent.path == null)
            {
                return;
            }

            Vector3[] corners = agent.path.corners;
            if (corners == null || corners.Length < 2)
            {
                return;
            }

            Handles.color = motor != null && motor.IsAvoidingDynamicObstacle
                ? new Color(1f, 0.25f, 0.75f, 0.95f)
                : new Color(0.15f, 0.8f, 1f, 0.95f);
            Handles.DrawAAPolyLine(5f, corners);
            for (int i = 1; i < corners.Length; i++)
            {
                Handles.SphereHandleCap(0, corners[i], Quaternion.identity, markerSize * 0.4f, EventType.Repaint);
            }

            if (motor != null && motor.IsAvoidingDynamicObstacle)
            {
                DrawMarker(motor.DynamicObstacleDetour, new Color(1f, 0.25f, 0.75f), "DETOUR");
            }

            if (motor != null && motor.LastRecoveryAge <= recoveryDisplayTime)
            {
                DrawMarker(motor.LastRecoveryPosition, new Color(0.2f, 1f, 0.55f), "ANTI-STUCK RECOVERY");
            }
        }

        private void DrawCurrentGoal()
        {
            if (brain == null)
            {
                return;
            }

            Color goalColor = new Color(0.1f, 0.9f, 1f);
            Handles.color = goalColor;
            Handles.DrawDottedLine(transform.position + Vector3.up * 0.15f, brain.CurrentGoal + Vector3.up * 0.15f, 5f);
            DrawMarker(brain.CurrentGoal, goalColor, "GOAL");

            if (brain.CurrentSearchPoint != null)
            {
                DrawArrow(
                    brain.CurrentSearchPoint.Position + Vector3.up * 0.12f,
                    brain.CurrentSearchPoint.LookDirection,
                    new Color(1f, 0.65f, 0.08f),
                    1.5f);
            }
            else if (brain.CurrentTaskLocation != null)
            {
                DrawArrow(
                    brain.CurrentTaskLocation.Position + Vector3.up * 0.12f,
                    brain.CurrentTaskLocation.LookDirection,
                    new Color(0.1f, 0.85f, 1f),
                    1.5f);
            }

            if (brain.CurrentUnexpectedOpenDoor != null)
            {
                Color doorEvidenceColor = new Color(1f, 0.35f, 0.08f);
                Vector3 doorPosition = brain.CurrentUnexpectedOpenDoor.transform.position;
                Handles.color = doorEvidenceColor;
                Handles.DrawDottedLine(doorPosition + Vector3.up * 0.2f, brain.CurrentDoorRoomCheckPosition + Vector3.up * 0.2f, 4f);
                DrawMarker(doorPosition, doorEvidenceColor, "UNEXPECTED OPEN DOOR");
                DrawMarker(brain.CurrentDoorRoomCheckPosition, doorEvidenceColor, "CHECK ROOM");
                DrawArrow(
                    doorPosition + Vector3.up * 0.25f,
                    brain.CurrentDoorRoomCheckPosition - doorPosition,
                    doorEvidenceColor,
                    Mathf.Min(2f, Vector3.Distance(doorPosition, brain.CurrentDoorRoomCheckPosition)));
            }
        }

        private void DrawLastKnownPlayer()
        {
            if (brain == null || brain.Player == null || !brain.HasSeenPlayer)
            {
                return;
            }

            Color color = brain.IsPlayerVisible ? new Color(1f, 0.15f, 0.1f) : new Color(1f, 0.45f, 0.12f);
            DrawMarker(brain.LastKnownPlayerPosition, color, brain.IsPlayerVisible ? "PLAYER VISIBLE" : "LAST KNOWN PLAYER");
            Handles.color = color;
            Handles.DrawDottedLine(transform.position + Vector3.up, brain.LastKnownPlayerPosition + Vector3.up, 4f);
            DrawArrow(brain.LastKnownPlayerPosition, brain.LastSeenPlayerMoveDirection, color, 1.8f);
            if (brain.IsVerifyingLastSeenPosition)
            {
                DrawMarker(brain.LastSeenVerificationPosition, new Color(1f, 0.8f, 0.08f), "MUST VERIFY");
            }
        }

        private void DrawRecentSound()
        {
            if (hearing == null || hearing.LastHeardAge > recentSoundDisplayTime)
            {
                return;
            }

            float fade = 1f - Mathf.Clamp01(hearing.LastHeardAge / recentSoundDisplayTime);
            Color color = Color.Lerp(new Color(0.25f, 0.65f, 1f), new Color(1f, 0.15f, 0.75f), hearing.LastHeardUrgency);
            color.a = fade;
            Handles.color = color;
            Handles.DrawWireDisc(hearing.LastHeardPosition, Vector3.up, Mathf.Lerp(0.35f, 1.5f, hearing.LastHeardLoudness));
            Handles.DrawDottedLine(transform.position + Vector3.up * 0.5f, hearing.LastHeardPosition + Vector3.up * 0.2f, 6f);
            DrawMarker(hearing.LastHeardPosition, color, $"HEARD {hearing.LastHeardAge:0.0}s");
        }

        private void DrawInteractionTargets()
        {
            if (doorInteractor == null || doorInteractor.ActiveDoor == null)
            {
                return;
            }

            Color color = doorInteractor.IsKickingBlockedDoor
                ? new Color(1f, 0.15f, 0.1f)
                : doorInteractor.IsReactingToLockedDoor
                    ? new Color(1f, 0.3f, 0.8f)
                    : new Color(0.2f, 1f, 0.45f);
            string label = doorInteractor.IsKickingBlockedDoor ? "KICK DOOR" : doorInteractor.IsReactingToLockedDoor ? "LOCKED DOOR" : "OPEN DOOR";
            Handles.color = color;
            Handles.DrawLine(transform.position + Vector3.up, doorInteractor.ActiveDoor.transform.position + Vector3.up);
            DrawMarker(doorInteractor.ActiveDoor.transform.position, color, label);
        }

        private void DrawMarker(Vector3 position, Color color, string label)
        {
            Handles.color = color;
            Handles.DrawWireDisc(position + Vector3.up * 0.05f, Vector3.up, markerSize);
            Handles.DrawLine(position, position + Vector3.up * markerSize * 2f);
            markerStyle.normal.textColor = color;
            Handles.Label(position + Vector3.up * (markerSize * 2.2f), label, markerStyle);
        }

        private static void DrawArrow(Vector3 origin, Vector3 direction, Color color, float length)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            direction.Normalize();
            Vector3 end = origin + direction * length;
            Handles.color = color;
            Handles.DrawAAPolyLine(4f, origin, end);
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
            Handles.ConeHandleCap(0, end, rotation, 0.22f, EventType.Repaint);
        }

        private Color GetStateColor()
        {
            if (brain == null)
            {
                return Color.white;
            }

            return brain.CurrentState switch
            {
                NeighborBrain.BehaviorState.Chase => new Color(1f, 0.12f, 0.08f),
                NeighborBrain.BehaviorState.Catching => new Color(1f, 0.05f, 0.5f),
                NeighborBrain.BehaviorState.HuntMode => new Color(1f, 0.45f, 0.05f),
                NeighborBrain.BehaviorState.Investigate => new Color(1f, 0.8f, 0.08f),
                NeighborBrain.BehaviorState.Stunned => new Color(0.8f, 0.25f, 1f),
                NeighborBrain.BehaviorState.Task => new Color(0.25f, 0.9f, 1f),
                NeighborBrain.BehaviorState.Wander => new Color(0.25f, 1f, 0.45f),
                _ => new Color(0.75f, 0.85f, 1f)
            };
        }

        private static string FormatDistance(float distance)
        {
            return float.IsInfinity(distance) ? "--" : $"{distance:0.0}m";
        }
#endif
    }
}
