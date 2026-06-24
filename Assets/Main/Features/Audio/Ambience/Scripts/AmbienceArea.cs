using System.Collections.Generic;
using Neighbor.Main.Features.Player;
using UnityEngine;

namespace Neighbor.Main.Features.Audio
{
    public enum AmbienceZoneLocation
    {
        Inside = 0,
        Outside = 1
    }

    [RequireComponent(typeof(Collider))]
    public sealed class AmbienceArea : MonoBehaviour
    {
        private static readonly List<AmbienceArea> ActiveAreas = new List<AmbienceArea>();

        [SerializeField] private AmbienceProfile profile;
        [Tooltip("Higher-priority areas win when multiple areas overlap.")]
        [SerializeField] private int priority;
        [Tooltip("Whether this audio zone is indoors or outdoors. Outdoor-only effects are hidden in inside zones.")]
        [SerializeField] private AmbienceZoneLocation zoneLocation = AmbienceZoneLocation.Inside;
        [Tooltip("Use this area's profile when no non-default ambience area has the player inside it.")]
        [SerializeField] private bool playWhenNoAreaActive;
        [Tooltip("All assigned colliders define this area. Uses the collider on this object when empty.")]
        [SerializeField] private Collider[] boundaries;

        private readonly Dictionary<PlayerController, int> playerContacts = new Dictionary<PlayerController, int>();

        public AmbienceProfile Profile => profile;
        public int Priority => priority;
        public AmbienceZoneLocation ZoneLocation => zoneLocation;
        public bool PlayWhenNoAreaActive => playWhenNoAreaActive;
        public bool HasPlayerInside
        {
            get
            {
                PrunePlayerContacts();
                return playerContacts.Count > 0;
            }
        }

        internal static IReadOnlyList<AmbienceArea> Areas => ActiveAreas;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveAreas()
        {
            ActiveAreas.Clear();
        }

        private void OnEnable()
        {
            if (!ActiveAreas.Contains(this))
            {
                ActiveAreas.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveAreas.Remove(this);
            playerContacts.Clear();
        }

        public bool Contains(Vector3 worldPosition)
        {
            ResolveBoundaries();

            for (int i = 0; i < boundaries.Length; i++)
            {
                Collider boundary = boundaries[i];
                if (boundary == null || !boundary.enabled || !boundary.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 closestPoint = boundary.ClosestPoint(worldPosition);
                if ((closestPoint - worldPosition).sqrMagnitude < 0.000001f)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsActiveFor(Transform fallbackTransform)
        {
            if (HasPlayerInside)
            {
                return true;
            }

            return fallbackTransform != null && Contains(fallbackTransform.position);
        }

        private void OnTriggerEnter(Collider other)
        {
            TrackPlayerContact(other, 1);
        }

        private void OnTriggerExit(Collider other)
        {
            TrackPlayerContact(other, -1);
        }

        private void Reset()
        {
            boundaries = GetComponents<Collider>();
            SetBoundariesAsTriggers();
        }

        private void OnValidate()
        {
            ResolveBoundaries();
            SetBoundariesAsTriggers();
        }

        private void ResolveBoundaries()
        {
            if (boundaries == null || boundaries.Length == 0)
            {
                boundaries = GetComponents<Collider>();
            }
        }

        private void SetBoundariesAsTriggers()
        {
            if (boundaries == null)
            {
                return;
            }

            for (int i = 0; i < boundaries.Length; i++)
            {
                if (boundaries[i] != null)
                {
                    boundaries[i].isTrigger = true;
                }
            }
        }

        private void TrackPlayerContact(Collider other, int delta)
        {
            if (other == null)
            {
                return;
            }

            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null)
            {
                return;
            }

            playerContacts.TryGetValue(player, out int count);
            count += delta;

            if (count > 0)
            {
                playerContacts[player] = count;
                return;
            }

            playerContacts.Remove(player);
        }

        private void PrunePlayerContacts()
        {
            if (playerContacts.Count == 0)
            {
                return;
            }

            PlayerController[] players = new PlayerController[playerContacts.Count];
            playerContacts.Keys.CopyTo(players, 0);

            for (int i = 0; i < players.Length; i++)
            {
                PlayerController player = players[i];
                if (player == null || !player.isActiveAndEnabled || !player.gameObject.activeInHierarchy)
                {
                    playerContacts.Remove(player);
                }
            }
        }
    }
}
