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

            Vector3 localCenter;
            Vector3 localSize;
            if (!TryGetColliderBoundsInWallSpace(openingOwner.gameObject, out Bounds colliderBounds))
            {
                Vector3 worldCenter = openingTransform.TransformPoint(profile.Center);
                localCenter = transform.InverseTransformPoint(worldCenter);
                Vector3 worldSize = Vector3.Scale(profile.Size, Abs(openingTransform.lossyScale));
                localSize = Divide(worldSize, Abs(transform.lossyScale));
            }
            else
            {
                localCenter = colliderBounds.center;
                localSize = colliderBounds.size;
            }

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

        private bool TryGetColliderBoundsInWallSpace(GameObject openingRoot, out Bounds bounds)
        {
            bounds = default;
            if (openingRoot == null)
            {
                return false;
            }

            Collider[] colliders = openingRoot.GetComponentsInChildren<Collider>(true);
            bool found = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (TryGetLocalColliderBounds(collider, out Bounds localBounds))
                {
                    EncapsulateTransformedBounds(collider.transform, localBounds, ref bounds, ref found);
                }
                else
                {
                    EncapsulateWorldBounds(collider.bounds, ref bounds, ref found);
                }
            }

            return found;
        }

        private static bool TryGetLocalColliderBounds(Collider collider, out Bounds bounds)
        {
            switch (collider)
            {
                case BoxCollider box:
                    bounds = new Bounds(box.center, box.size);
                    return true;
                case MeshCollider mesh when mesh.sharedMesh != null:
                    bounds = mesh.sharedMesh.bounds;
                    return true;
                case SphereCollider sphere:
                    bounds = new Bounds(sphere.center, Vector3.one * sphere.radius * 2f);
                    return true;
                case CapsuleCollider capsule:
                    Vector3 size = Vector3.one * capsule.radius * 2f;
                    size[capsule.direction] = Mathf.Max(capsule.height, capsule.radius * 2f);
                    bounds = new Bounds(capsule.center, size);
                    return true;
                default:
                    bounds = default;
                    return false;
            }
        }

        private void EncapsulateTransformedBounds(Transform source, Bounds sourceBounds, ref Bounds target, ref bool found)
        {
            Vector3 min = sourceBounds.min;
            Vector3 max = sourceBounds.max;
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        Vector3 localPoint = new(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        EncapsulatePoint(transform.InverseTransformPoint(source.TransformPoint(localPoint)), ref target, ref found);
                    }
                }
            }
        }

        private void EncapsulateWorldBounds(Bounds worldBounds, ref Bounds target, ref bool found)
        {
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        Vector3 worldPoint = new(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        EncapsulatePoint(transform.InverseTransformPoint(worldPoint), ref target, ref found);
                    }
                }
            }
        }

        private static void EncapsulatePoint(Vector3 point, ref Bounds bounds, ref bool found)
        {
            if (!found)
            {
                bounds = new Bounds(point, Vector3.zero);
                found = true;
                return;
            }

            bounds.Encapsulate(point);
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
