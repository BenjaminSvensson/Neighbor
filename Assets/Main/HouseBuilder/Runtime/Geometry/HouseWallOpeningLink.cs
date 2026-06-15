using System.Collections.Generic;
using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Neighbor/House Builder/Wall Opening Link")]
    public sealed class HouseWallOpeningLink : MonoBehaviour
    {
        [SerializeField] private HouseGeometryObject wall;
        [SerializeField] private HouseWallOpeningProfile profile;
        private Vector3 lastPosition;
        private Quaternion lastRotation;
        private Vector3 lastScale;
        private int lastColliderStateHash;
        private readonly List<Collider> trackedColliders = new();

        public HouseGeometryObject Wall => wall;

        public void Initialize(HouseGeometryObject targetWall, HouseWallOpeningProfile openingProfile)
        {
            wall = targetWall;
            profile = openingProfile;
            RefreshOpening();
        }

        public void RefreshOpening()
        {
            HouseBuilderObject owner = GetComponent<HouseBuilderObject>();
            HouseWallOpeningProfile currentProfile = ResolveCurrentProfile(owner);
            if (wall != null && owner != null && currentProfile != null)
            {
                profile = currentProfile;
                wall.AddOrUpdateWallOpening(owner, transform, currentProfile);
            }

            lastPosition = transform.position;
            lastRotation = transform.rotation;
            lastScale = transform.lossyScale;
            lastColliderStateHash = CalculateColliderStateHash();
        }

        private void OnEnable()
        {
            RefreshOpening();
        }

        private void Update()
        {
            RefreshIfMoved(!Application.isPlaying);
        }

        public void RefreshIfMoved(bool allowTransformRefresh)
        {
            if (allowTransformRefresh
                && wall != null
                && (transform.position != lastPosition
                    || transform.rotation != lastRotation
                    || transform.lossyScale != lastScale
                    || CalculateColliderStateHash() != lastColliderStateHash))
            {
                RefreshOpening();
            }
        }

        private int CalculateColliderStateHash()
        {
            unchecked
            {
                int hash = 17;
                trackedColliders.Clear();
                GetComponentsInChildren(true, trackedColliders);
                for (int i = 0; i < trackedColliders.Count; i++)
                {
                    Collider collider = trackedColliders[i];
                    if (collider == null)
                    {
                        continue;
                    }

                    hash = hash * 31 + collider.GetHashCode();
                    hash = hash * 31 + collider.enabled.GetHashCode();
                    hash = hash * 31 + collider.gameObject.activeInHierarchy.GetHashCode();
                    if (collider.enabled && collider.gameObject.activeInHierarchy)
                    {
                        hash = hash * 31 + collider.bounds.center.GetHashCode();
                        hash = hash * 31 + collider.bounds.size.GetHashCode();
                    }
                }

                return hash;
            }
        }

        private void OnDestroy()
        {
            bool editorPlayingOrChangingMode = false;
#if UNITY_EDITOR
            editorPlayingOrChangingMode = UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode;
#endif
            if (!ShouldRemoveOpeningOnDestroy(Application.isPlaying, gameObject.scene.isLoaded, editorPlayingOrChangingMode))
            {
                return;
            }

            HouseBuilderObject owner = GetComponent<HouseBuilderObject>();
            if (wall != null && owner != null)
            {
                wall.RemoveWallOpening(owner.InstanceId);
            }
        }

        public static bool ShouldRemoveOpeningOnDestroy(
            bool applicationIsPlaying,
            bool sceneIsLoaded,
            bool editorPlayingOrChangingMode)
        {
            return !applicationIsPlaying && sceneIsLoaded && !editorPlayingOrChangingMode;
        }

        private HouseWallOpeningProfile ResolveCurrentProfile(HouseBuilderObject owner)
        {
            HouseBuilderWorld world = GetComponentInParent<HouseBuilderWorld>();
            if (owner != null
                && world != null
                && world.Catalog != null
                && world.Catalog.TryGetPlaceable(owner.DefinitionId, out HousePlaceableDefinition definition)
                && definition.WallOpening != null
                && definition.WallOpening.Enabled)
            {
                return definition.WallOpening;
            }

            return profile;
        }
    }
}
