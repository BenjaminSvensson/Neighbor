using System;
using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    [AddComponentMenu("Neighbor/House Builder/Geometry Object")]
    public sealed class HouseGeometryObject : MonoBehaviour
    {
        [SerializeField] private HouseGeometryDescriptor descriptor = new(HouseGeometryKind.Cube, Vector3.one);

        public HouseGeometryDescriptor Descriptor => descriptor;

        public void Configure(HouseGeometryDescriptor value)
        {
            descriptor = value ?? new HouseGeometryDescriptor(HouseGeometryKind.Cube, Vector3.one);
            Rebuild();
        }

        public void BakeCurrentMesh(HouseGeometryKind kind = HouseGeometryKind.Custom)
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            descriptor = new HouseGeometryDescriptor(kind, filter.sharedMesh != null ? filter.sharedMesh.bounds.size : Vector3.one,
                bakedMesh: HouseMeshData.Capture(filter.sharedMesh));
        }

        public void Rebuild()
        {
            Mesh mesh = descriptor.BakedMesh != null
                ? descriptor.BakedMesh.Build($"{name}_{descriptor.Kind}")
                : HouseGeometryFactory.BuildMesh(descriptor);
            mesh.hideFlags = HideFlags.DontSave;

            MeshFilter filter = GetComponent<MeshFilter>();
            Mesh previousMesh = filter.sharedMesh;
            filter.sharedMesh = mesh;
            if (previousMesh != null && (previousMesh.hideFlags & HideFlags.DontSave) != 0)
            {
                if (Application.isPlaying)
                {
                    Destroy(previousMesh);
                }
                else
                {
                    DestroyImmediate(previousMesh);
                }
            }

            MeshRenderer renderer = GetComponent<MeshRenderer>();
            Material[] materials = renderer.sharedMaterials;
            if (materials.Length < HouseGeometryFactory.MaterialSlotCount)
            {
                Array.Resize(ref materials, HouseGeometryFactory.MaterialSlotCount);
                renderer.sharedMaterials = materials;
            }

            MeshCollider collider = GetComponent<MeshCollider>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<MeshCollider>();
            }

            collider.sharedMesh = null;
            collider.sharedMesh = mesh;
        }

        public bool AddOrUpdateWallOpening(HouseBuilderObject openingOwner, Transform openingTransform, HouseWallOpeningProfile profile)
        {
            if (descriptor == null
                || descriptor.Kind != HouseGeometryKind.Wall
                || openingOwner == null
                || openingTransform == null
                || profile == null
                || !profile.Enabled)
            {
                return false;
            }

            Vector3 worldCenter = openingTransform.TransformPoint(profile.Center);
            Vector3 localCenter = transform.InverseTransformPoint(worldCenter);
            Vector3 worldSize = Vector3.Scale(profile.Size, Abs(openingTransform.lossyScale));
            Vector3 localSize = Divide(worldSize, Abs(transform.lossyScale));
            Vector2 openingSize = new(
                Mathf.Min(descriptor.Size.x, localSize.x + profile.Margin * 2f),
                Mathf.Min(descriptor.Size.y, localSize.y + profile.Margin * 2f));

            descriptor.AddOrUpdateWallOpening(new HouseWallOpeningData(
                openingOwner.InstanceId,
                new Vector2(localCenter.x, localCenter.y),
                openingSize));
            Rebuild();
            return true;
        }

        public void RemoveWallOpening(string ownerObjectId)
        {
            descriptor?.RemoveWallOpening(ownerObjectId);
            Rebuild();
        }

        private void OnValidate()
        {
            if (descriptor != null && gameObject.scene.IsValid())
            {
                Rebuild();
            }
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private static Vector3 Divide(Vector3 value, Vector3 divisor)
        {
            return new Vector3(
                divisor.x > 0.0001f ? value.x / divisor.x : value.x,
                divisor.y > 0.0001f ? value.y / divisor.y : value.y,
                divisor.z > 0.0001f ? value.z / divisor.z : value.z);
        }
    }
}
