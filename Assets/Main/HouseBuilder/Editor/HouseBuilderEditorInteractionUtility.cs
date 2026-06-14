#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Neighbor.Main.HouseBuilder.Editor
{
    internal enum HousePlacementRotationAxis
    {
        Yaw,
        Pitch,
        Roll
    }

    internal readonly struct HouseBuilderFaceHit
    {
        public HouseBuilderObject Owner { get; }
        public Renderer Renderer { get; }
        public HouseFaceRole FaceRole { get; }
        public int MaterialIndex { get; }
        public string RendererPath { get; }
        public RaycastHit RaycastHit { get; }
        public Vector3[] Triangle { get; }

        public HouseBuilderFaceHit(
            HouseBuilderObject owner,
            Renderer renderer,
            HouseFaceRole faceRole,
            int materialIndex,
            string rendererPath,
            RaycastHit raycastHit,
            Vector3[] triangle)
        {
            Owner = owner;
            Renderer = renderer;
            FaceRole = faceRole;
            MaterialIndex = materialIndex;
            RendererPath = rendererPath;
            RaycastHit = raycastHit;
            Triangle = triangle;
        }
    }

    internal static class HouseBuilderEditorInteractionUtility
    {
        private const float MinimumDimension = 0.05f;

        public static Vector3 SuggestedSize(HouseGeometryKind kind)
        {
            return kind switch
            {
                HouseGeometryKind.Wall or HouseGeometryKind.Doorway or HouseGeometryKind.Window => new Vector3(4f, 3f, 0.25f),
                HouseGeometryKind.Floor => new Vector3(4f, 0.2f, 4f),
                HouseGeometryKind.Ceiling => new Vector3(4f, 0.15f, 4f),
                HouseGeometryKind.Ramp => new Vector3(2f, 1.5f, 4f),
                HouseGeometryKind.Stairs => new Vector3(2f, 2f, 4f),
                _ => new Vector3(2f, 2f, 2f)
            };
        }

        public static Vector3 SanitizeSize(Vector3 size)
        {
            return new Vector3(
                Mathf.Max(MinimumDimension, size.x),
                Mathf.Max(MinimumDimension, size.y),
                Mathf.Max(MinimumDimension, size.z));
        }

        public static Quaternion RotatePlacement(
            Quaternion rotation,
            HousePlacementRotationAxis axis,
            float step,
            float direction)
        {
            Vector3 localAxis = axis switch
            {
                HousePlacementRotationAxis.Pitch => Vector3.right,
                HousePlacementRotationAxis.Roll => Vector3.forward,
                _ => Vector3.up
            };
            return rotation * Quaternion.AngleAxis(step * Mathf.Sign(direction), localAxis);
        }

        public static bool ShouldDeselectPlaceable(
            HousePlaceableDefinition activeDefinition,
            HousePlaceableDefinition clickedDefinition)
        {
            return activeDefinition != null && activeDefinition == clickedDefinition;
        }

        public static bool ShouldShowWirePorts(
            bool connectTabActive,
            bool hasPendingOutput,
            HouseBuilderObject selectedObject)
        {
            return connectTabActive
                || hasPendingOutput
                || selectedObject?.GetComponentInChildren<HouseWireEndpoint>(true) != null;
        }

        public static bool TryPickFace(Ray ray, LayerMask mask, out HouseBuilderFaceHit faceHit)
        {
            RaycastHit[] hits = Physics.RaycastAll(ray, 10000f, mask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                HouseBuilderObject owner = hit.collider.GetComponentInParent<HouseBuilderObject>();
                if (owner == null)
                {
                    continue;
                }

                Renderer renderer = hit.collider.GetComponent<Renderer>() ?? owner.GetComponentInChildren<Renderer>(true);
                if (renderer == null)
                {
                    continue;
                }

                int materialIndex = ResolveMaterialIndex(hit);
                HouseFaceRole role = ResolveFaceRole(owner, hit, materialIndex);
                string rendererPath = GetRelativePath(owner.transform, renderer.transform);
                faceHit = new HouseBuilderFaceHit(owner, renderer, role, materialIndex, rendererPath, hit, ResolveTriangle(hit));
                return true;
            }

            faceHit = default;
            return false;
        }

        public static bool TryPickMatchingPlacedObject(
            Ray ray,
            LayerMask mask,
            Transform worldRoot,
            GameObject ignoredRoot,
            string definitionId,
            out HouseBuilderObject match)
        {
            RaycastHit[] hits = Physics.RaycastAll(ray, 10000f, mask, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Transform hitTransform = hits[i].collider.transform;
                if (ignoredRoot != null && hitTransform.IsChildOf(ignoredRoot.transform))
                {
                    continue;
                }

                HouseBuilderObject candidate = hitTransform.GetComponentInParent<HouseBuilderObject>();
                if (candidate == null || worldRoot != null && !candidate.transform.IsChildOf(worldRoot))
                {
                    continue;
                }

                if (candidate.DefinitionId == definitionId)
                {
                    match = candidate;
                    return true;
                }

                // A different placed object in front should prevent erasing through it.
                break;
            }

            match = null;
            return false;
        }

        private static int ResolveMaterialIndex(RaycastHit hit)
        {
            if (hit.collider is not MeshCollider meshCollider || meshCollider.sharedMesh == null || hit.triangleIndex < 0)
            {
                return 0;
            }

            Mesh mesh = meshCollider.sharedMesh;
            int triangleStart = 0;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                int triangleCount = (int)mesh.GetIndexCount(submesh) / 3;
                if (hit.triangleIndex < triangleStart + triangleCount)
                {
                    return submesh;
                }

                triangleStart += triangleCount;
            }

            return 0;
        }

        private static HouseFaceRole ResolveFaceRole(HouseBuilderObject owner, RaycastHit hit, int materialIndex)
        {
            HouseGeometryObject geometry = owner.GetComponent<HouseGeometryObject>();
            if (geometry != null && materialIndex >= 0 && materialIndex < HouseGeometryFactory.MaterialSlotCount)
            {
                return (HouseFaceRole)materialIndex;
            }

            Vector3 normal = owner.transform.InverseTransformDirection(hit.normal).normalized;
            Vector3 absolute = new(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            if (absolute.y >= absolute.x && absolute.y >= absolute.z)
            {
                return normal.y >= 0f ? HouseFaceRole.Top : HouseFaceRole.Underside;
            }

            if (absolute.x >= absolute.z)
            {
                return normal.x >= 0f ? HouseFaceRole.Right : HouseFaceRole.Left;
            }

            return normal.z >= 0f ? HouseFaceRole.Front : HouseFaceRole.Back;
        }

        private static Vector3[] ResolveTriangle(RaycastHit hit)
        {
            if (hit.collider is not MeshCollider meshCollider || meshCollider.sharedMesh == null || hit.triangleIndex < 0)
            {
                return Array.Empty<Vector3>();
            }

            Mesh mesh = meshCollider.sharedMesh;
            int[] triangles = mesh.triangles;
            int index = hit.triangleIndex * 3;
            if (index + 2 >= triangles.Length)
            {
                return Array.Empty<Vector3>();
            }

            Transform transform = meshCollider.transform;
            return new[]
            {
                transform.TransformPoint(mesh.vertices[triangles[index]]),
                transform.TransformPoint(mesh.vertices[triangles[index + 1]]),
                transform.TransformPoint(mesh.vertices[triangles[index + 2]])
            };
        }

        private static string GetRelativePath(Transform root, Transform target)
        {
            if (target == root)
            {
                return string.Empty;
            }

            System.Collections.Generic.Stack<string> names = new();
            Transform current = target;
            while (current != null && current != root)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }
    }
}
#endif
