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
            if (wall != null && owner != null && profile != null)
            {
                wall.AddOrUpdateWallOpening(owner, transform, profile);
            }

            lastPosition = transform.position;
            lastRotation = transform.rotation;
            lastScale = transform.lossyScale;
        }

        private void Update()
        {
            if (wall != null
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
    }
}
