using System;
using System.Collections.Generic;
using UnityEngine;

namespace Neighbor.Main.HouseBuilder
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    [AddComponentMenu("Neighbor/House Builder/Geometry Object")]
    public sealed class HouseGeometryObject : MonoBehaviour
    {
        public const string PhysicalObjectName = "Physical Geometry";

        [SerializeField] private HouseGeometryDescriptor descriptor = new(HouseGeometryKind.Cube, Vector3.one);
        private readonly List<Mesh> staleMeshes = new();

        public HouseGeometryDescriptor Descriptor => descriptor;

        public void Configure(HouseGeometryDescriptor value)
        {
            descriptor = value ?? new HouseGeometryDescriptor(HouseGeometryKind.Cube, Vector3.one);
            Rebuild();
        }

        public void Resize(Vector3 size)
        {
            descriptor ??= new HouseGeometryDescriptor(HouseGeometryKind.Cube, size);
            descriptor.SetSize(size);
            Rebuild();
        }

        public void BakeCurrentMesh(HouseGeometryKind kind = HouseGeometryKind.Custom)
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            descriptor = new HouseGeometryDescriptor(kind, filter.sharedMesh != null ? filter.sharedMesh.bounds.size : Vector3.one,
                bakedMesh: HouseMeshData.Capture(filter.sharedMesh));
        }

        public void PrepareForPlacement()
        {
            enabled = true;
            Rebuild();
            PreparePhysicalObject();
        }

        public void PreparePhysicalObject()
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            MeshRenderer renderer = GetComponent<MeshRenderer>();
            SyncPhysicalObject(filter.sharedMesh, renderer.sharedMaterials);
        }

        public Vector3 CenterOnWallMidplane(Vector3 worldPosition)
        {
            Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
            localPosition.z = 0f;
            return transform.TransformPoint(localPosition);
        }

        public void Rebuild()
        {
            Mesh mesh = descriptor.BakedMesh != null && descriptor.BakedMesh.HasGeometry
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
                    staleMeshes.Add(previousMesh);
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
            if (transform.Find(PhysicalObjectName) != null)
            {
                SyncPhysicalObject(mesh, renderer.sharedMaterials);
            }

            GetComponent<HouseBuilderMaterialController>()?.ApplyFromWorld();
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

        private void Start()
        {
            EnsureVisibleGeometry();
        }

        private void OnEnable()
        {
            EnsureVisibleGeometry();
        }

        private void Update()
        {
            if (!Application.isPlaying && gameObject.scene.IsValid() && transform.Find(PhysicalObjectName) == null)
            {
                PreparePhysicalObject();
            }

            if (Application.isPlaying || staleMeshes.Count == 0)
            {
                return;
            }

            for (int i = staleMeshes.Count - 1; i >= 0; i--)
            {
                if (staleMeshes[i] != null)
                {
                    DestroyImmediate(staleMeshes[i]);
                }
            }

            staleMeshes.Clear();
        }

        private void EnsureVisibleGeometry()
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            if (descriptor != null
                && gameObject.scene.IsValid()
                && (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0))
            {
                Rebuild();
            }
        }

        private void SyncPhysicalObject(Mesh mesh, Material[] materials)
        {
            Transform physicalTransform = transform.Find(PhysicalObjectName);
            if (physicalTransform == null)
            {
                GameObject physicalObject = new(PhysicalObjectName);
                physicalTransform = physicalObject.transform;
                physicalTransform.SetParent(transform, false);
            }

            MeshFilter physicalFilter = physicalTransform.GetComponent<MeshFilter>();
            if (physicalFilter == null)
            {
                physicalFilter = physicalTransform.gameObject.AddComponent<MeshFilter>();
            }

            MeshRenderer physicalRenderer = physicalTransform.GetComponent<MeshRenderer>();
            if (physicalRenderer == null)
            {
                physicalRenderer = physicalTransform.gameObject.AddComponent<MeshRenderer>();
            }

            MeshCollider physicalCollider = physicalTransform.GetComponent<MeshCollider>();
            if (physicalCollider == null)
            {
                physicalCollider = physicalTransform.gameObject.AddComponent<MeshCollider>();
            }

            physicalFilter.sharedMesh = mesh;
            physicalRenderer.sharedMaterials = materials;
            physicalRenderer.enabled = true;
            physicalRenderer.SetPropertyBlock(null);
            physicalCollider.sharedMesh = null;
            physicalCollider.sharedMesh = mesh;
            physicalCollider.enabled = true;

            GetComponent<MeshRenderer>().enabled = false;
            GetComponent<MeshCollider>().enabled = false;
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
