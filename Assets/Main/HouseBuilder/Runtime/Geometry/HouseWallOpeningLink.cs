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
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                RefreshOpening();
            }
        }

        private void Update()
        {
            RefreshIfMoved(!Application.isPlaying);
        }

        public void RefreshIfMoved(bool allowTransformRefresh)
        {
            if (allowTransformRefresh
                && wall != null
                && (transform.position != lastPosition || transform.rotation != lastRotation || transform.lossyScale != lastScale))
            {
                RefreshOpening();
            }
        }

        private void OnDestroy()
        {
            HouseBuilderObject owner = GetComponent<HouseBuilderObject>();
            if (wall != null && owner != null)
            {
                wall.RemoveWallOpening(owner.InstanceId);
            }
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
