using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Neighbor.Main.HouseBuilder
{
    public enum HouseGeometryKind
    {
        Cube,
        Wall,
        Floor,
        Ceiling,
        Doorway,
        Window,
        Ramp,
        Stairs,
        BakedBoolean,
        Custom
    }

    [Serializable]
    public sealed class HouseSubmeshData
    {
        [SerializeField] private int[] triangles = Array.Empty<int>();

        public int[] Triangles => triangles;

        public HouseSubmeshData(int[] triangles)
        {
            this.triangles = triangles ?? Array.Empty<int>();
        }
    }

    [Serializable]
    public sealed class HouseMeshData
    {
        [SerializeField] private Vector3[] vertices = Array.Empty<Vector3>();
        [SerializeField] private Vector3[] normals = Array.Empty<Vector3>();
        [SerializeField] private Vector2[] uv = Array.Empty<Vector2>();
        [SerializeField] private HouseSubmeshData[] submeshes = Array.Empty<HouseSubmeshData>();

        public Vector3[] Vertices => vertices;
        public Vector3[] Normals => normals;
        public Vector2[] Uv => uv;
        public HouseSubmeshData[] Submeshes => submeshes;
        public bool HasGeometry => vertices != null && vertices.Length > 0;

        public static HouseMeshData Capture(Mesh mesh)
        {
            if (mesh == null)
            {
                return null;
            }

            HouseSubmeshData[] submeshData = new HouseSubmeshData[mesh.subMeshCount];
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                submeshData[i] = new HouseSubmeshData(mesh.GetTriangles(i));
            }

            return new HouseMeshData
            {
                vertices = mesh.vertices,
                normals = mesh.normals,
                uv = mesh.uv,
                submeshes = submeshData
            };
        }

        public Mesh Build(string meshName)
        {
            Mesh mesh = new()
            {
                name = meshName,
                indexFormat = vertices.Length > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16,
                vertices = vertices
            };

            if (uv != null && uv.Length == vertices.Length)
            {
                mesh.uv = uv;
            }

            mesh.subMeshCount = Mathf.Max(1, submeshes?.Length ?? 0);
            if (submeshes != null)
            {
                for (int i = 0; i < submeshes.Length; i++)
                {
                    mesh.SetTriangles(submeshes[i]?.Triangles ?? Array.Empty<int>(), i);
                }
            }

            if (normals != null && normals.Length == vertices.Length)
            {
                mesh.normals = normals;
            }
            else
            {
                mesh.RecalculateNormals();
            }

            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }

    [Serializable]
    public sealed class HouseGeometryDescriptor
    {
        [SerializeField] private HouseGeometryKind kind = HouseGeometryKind.Cube;
        [SerializeField] private Vector3 size = new(4f, 3f, 0.25f);
        [SerializeField, Min(0.1f)] private float openingWidth = 1.2f;
        [SerializeField, Min(0.1f)] private float openingHeight = 2.1f;
        [SerializeField, Min(0f)] private float sillHeight = 0.9f;
        [SerializeField, Min(1)] private int stairCount = 8;
        [SerializeField] private HouseMeshData bakedMesh;
        [SerializeField] private List<HouseWallOpeningData> wallOpenings = new();

        public HouseGeometryKind Kind => kind;
        public Vector3 Size => size;
        public float OpeningWidth => openingWidth;
        public float OpeningHeight => openingHeight;
        public float SillHeight => sillHeight;
        public int StairCount => stairCount;
        public HouseMeshData BakedMesh => bakedMesh;
        public IReadOnlyList<HouseWallOpeningData> WallOpenings => wallOpenings;
        public bool IsValid => (bakedMesh != null && bakedMesh.HasGeometry)
            || (size.x >= 0.05f && size.y >= 0.05f && size.z >= 0.05f);

        public HouseGeometryDescriptor(
            HouseGeometryKind kind,
            Vector3 size,
            float openingWidth = 1.2f,
            float openingHeight = 2.1f,
            float sillHeight = 0.9f,
            int stairCount = 8,
            HouseMeshData bakedMesh = null)
        {
            this.kind = kind;
            this.size = new Vector3(
                Mathf.Max(0.05f, size.x),
                Mathf.Max(0.05f, size.y),
                Mathf.Max(0.05f, size.z));
            this.openingWidth = Mathf.Max(0.1f, openingWidth);
            this.openingHeight = Mathf.Max(0.1f, openingHeight);
            this.sillHeight = Mathf.Max(0f, sillHeight);
            this.stairCount = Mathf.Max(1, stairCount);
            this.bakedMesh = bakedMesh;
        }

        public void AddOrUpdateWallOpening(HouseWallOpeningData opening)
        {
            if (opening == null)
            {
                return;
            }

            int index = wallOpenings.FindIndex(candidate => candidate != null && candidate.OwnerObjectId == opening.OwnerObjectId);
            if (index >= 0)
            {
                wallOpenings[index] = opening;
            }
            else
            {
                wallOpenings.Add(opening);
            }
        }

        public void RemoveWallOpening(string ownerObjectId)
        {
            wallOpenings.RemoveAll(opening => opening != null && opening.OwnerObjectId == ownerObjectId);
        }

        public void SetSize(Vector3 value)
        {
            size = new Vector3(
                Mathf.Max(0.05f, value.x),
                Mathf.Max(0.05f, value.y),
                Mathf.Max(0.05f, value.z));
        }
    }

    [Serializable]
    public sealed class HouseWallOpeningData
    {
        [SerializeField] private string ownerObjectId;
        [SerializeField] private Vector2 center;
        [SerializeField] private Vector2 size;

        public string OwnerObjectId => ownerObjectId;
        public Vector2 Center => center;
        public Vector2 Size => size;

        public HouseWallOpeningData(string ownerObjectId, Vector2 center, Vector2 size)
        {
            this.ownerObjectId = ownerObjectId;
            this.center = center;
            this.size = size;
        }
    }

    public static class HouseGeometryFactory
    {
        public const int MaterialSlotCount = 10;

        public static GameObject Create(HouseGeometryDescriptor descriptor, Material defaultMaterial = null)
        {
            GameObject result = new(descriptor.Kind.ToString());
            result.AddComponent<MeshFilter>();
            MeshRenderer renderer = result.AddComponent<MeshRenderer>();
            if (defaultMaterial != null)
            {
                Material[] materials = new Material[MaterialSlotCount];
                Array.Fill(materials, defaultMaterial);
                renderer.sharedMaterials = materials;
            }

            HouseGeometryObject geometry = result.AddComponent<HouseGeometryObject>();
            geometry.Configure(descriptor);

            HouseBuilderObject builderObject = result.AddComponent<HouseBuilderObject>();
            builderObject.Initialize(string.Empty, CategoryFor(descriptor.Kind));
            result.AddComponent<HouseBuilderMaterialController>();
            return result;
        }

        public static Mesh BuildMesh(HouseGeometryDescriptor descriptor)
        {
            MeshBuilder builder = new();
            Vector3 size = descriptor.Size;

            switch (descriptor.Kind)
            {
                case HouseGeometryKind.Wall:
                    AddWallWithOpenings(builder, descriptor);
                    break;
                case HouseGeometryKind.Doorway:
                    AddDoorway(builder, descriptor);
                    break;
                case HouseGeometryKind.Window:
                    AddWindow(builder, descriptor);
                    break;
                case HouseGeometryKind.Ramp:
                    AddRamp(builder, size);
                    break;
                case HouseGeometryKind.Stairs:
                    AddStairs(builder, size, descriptor.StairCount);
                    break;
                default:
                    AddBox(builder, Vector3.zero, size, RolesFor(descriptor.Kind));
                    break;
            }

            return builder.Build($"HouseBuilder_{descriptor.Kind}");
        }

        public static string CategoryFor(HouseGeometryKind kind)
        {
            return kind switch
            {
                HouseGeometryKind.Wall or HouseGeometryKind.Doorway or HouseGeometryKind.Window => HouseBuilderCategories.Wall,
                HouseGeometryKind.Floor or HouseGeometryKind.Stairs or HouseGeometryKind.Ramp => HouseBuilderCategories.Floor,
                HouseGeometryKind.Ceiling => HouseBuilderCategories.Ceiling,
                _ => HouseBuilderCategories.Structure
            };
        }

        private static void AddDoorway(MeshBuilder builder, HouseGeometryDescriptor descriptor)
        {
            Vector3 size = descriptor.Size;
            float openingWidth = Mathf.Min(descriptor.OpeningWidth, size.x - 0.1f);
            float openingHeight = Mathf.Min(descriptor.OpeningHeight, size.y - 0.05f);
            float sideWidth = (size.x - openingWidth) * 0.5f;
            FaceRoles roles = RolesFor(HouseGeometryKind.Wall);

            AddBox(builder, new Vector3(-(openingWidth + sideWidth) * 0.5f, 0f, 0f), new Vector3(sideWidth, size.y, size.z), roles);
            AddBox(builder, new Vector3((openingWidth + sideWidth) * 0.5f, 0f, 0f), new Vector3(sideWidth, size.y, size.z), roles);

            float headerHeight = size.y - openingHeight;
            if (headerHeight > 0.01f)
            {
                AddBox(builder, new Vector3(0f, (size.y - headerHeight) * 0.5f, 0f), new Vector3(openingWidth, headerHeight, size.z), roles);
            }
        }

        private static void AddWallWithOpenings(MeshBuilder builder, HouseGeometryDescriptor descriptor)
        {
            if (descriptor.WallOpenings == null || descriptor.WallOpenings.Count == 0)
            {
                AddBox(builder, Vector3.zero, descriptor.Size, RolesFor(HouseGeometryKind.Wall));
                return;
            }

            float halfWidth = descriptor.Size.x * 0.5f;
            float halfHeight = descriptor.Size.y * 0.5f;
            List<float> xCuts = new() { -halfWidth, halfWidth };
            List<float> yCuts = new() { -halfHeight, halfHeight };
            for (int i = 0; i < descriptor.WallOpenings.Count; i++)
            {
                HouseWallOpeningData opening = descriptor.WallOpenings[i];
                if (opening == null)
                {
                    continue;
                }

                xCuts.Add(Mathf.Clamp(opening.Center.x - opening.Size.x * 0.5f, -halfWidth, halfWidth));
                xCuts.Add(Mathf.Clamp(opening.Center.x + opening.Size.x * 0.5f, -halfWidth, halfWidth));
                yCuts.Add(Mathf.Clamp(opening.Center.y - opening.Size.y * 0.5f, -halfHeight, halfHeight));
                yCuts.Add(Mathf.Clamp(opening.Center.y + opening.Size.y * 0.5f, -halfHeight, halfHeight));
            }

            xCuts.Sort();
            yCuts.Sort();
            FaceRoles roles = RolesFor(HouseGeometryKind.Wall);
            for (int x = 0; x < xCuts.Count - 1; x++)
            {
                for (int y = 0; y < yCuts.Count - 1; y++)
                {
                    float width = xCuts[x + 1] - xCuts[x];
                    float height = yCuts[y + 1] - yCuts[y];
                    if (width <= 0.001f || height <= 0.001f)
                    {
                        continue;
                    }

                    Vector2 center = new((xCuts[x] + xCuts[x + 1]) * 0.5f, (yCuts[y] + yCuts[y + 1]) * 0.5f);
                    bool insideOpening = false;
                    for (int openingIndex = 0; openingIndex < descriptor.WallOpenings.Count; openingIndex++)
                    {
                        HouseWallOpeningData opening = descriptor.WallOpenings[openingIndex];
                        if (opening != null
                            && Mathf.Abs(center.x - opening.Center.x) < opening.Size.x * 0.5f
                            && Mathf.Abs(center.y - opening.Center.y) < opening.Size.y * 0.5f)
                        {
                            insideOpening = true;
                            break;
                        }
                    }

                    if (!insideOpening)
                    {
                        AddBox(builder, new Vector3(center.x, center.y, 0f), new Vector3(width, height, descriptor.Size.z), roles);
                    }
                }
            }
        }

        private static void AddWindow(MeshBuilder builder, HouseGeometryDescriptor descriptor)
        {
            Vector3 size = descriptor.Size;
            float openingWidth = Mathf.Min(descriptor.OpeningWidth, size.x - 0.1f);
            float openingHeight = Mathf.Min(descriptor.OpeningHeight, size.y - 0.1f);
            float sill = Mathf.Min(descriptor.SillHeight, size.y - openingHeight);
            float sideWidth = (size.x - openingWidth) * 0.5f;
            FaceRoles roles = RolesFor(HouseGeometryKind.Wall);

            AddBox(builder, new Vector3(-(openingWidth + sideWidth) * 0.5f, 0f, 0f), new Vector3(sideWidth, size.y, size.z), roles);
            AddBox(builder, new Vector3((openingWidth + sideWidth) * 0.5f, 0f, 0f), new Vector3(sideWidth, size.y, size.z), roles);

            if (sill > 0.01f)
            {
                AddBox(builder, new Vector3(0f, (-size.y + sill) * 0.5f, 0f), new Vector3(openingWidth, sill, size.z), roles);
            }

            float headerHeight = size.y - sill - openingHeight;
            if (headerHeight > 0.01f)
            {
                AddBox(builder, new Vector3(0f, (size.y - headerHeight) * 0.5f, 0f), new Vector3(openingWidth, headerHeight, size.z), roles);
            }
        }

        private static void AddStairs(MeshBuilder builder, Vector3 size, int stairCount)
        {
            FaceRoles roles = RolesFor(HouseGeometryKind.Floor);
            float stepDepth = size.z / stairCount;
            float stepHeight = size.y / stairCount;
            for (int i = 0; i < stairCount; i++)
            {
                float height = stepHeight * (i + 1);
                Vector3 stepSize = new(size.x, height, stepDepth);
                Vector3 center = new(0f, -size.y * 0.5f + height * 0.5f, -size.z * 0.5f + stepDepth * (i + 0.5f));
                AddBox(builder, center, stepSize, roles);
            }
        }

        private static void AddRamp(MeshBuilder builder, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3[] vertices =
            {
                new(-h.x, -h.y, -h.z), new(h.x, -h.y, -h.z), new(h.x, -h.y, h.z), new(-h.x, -h.y, h.z),
                new(-h.x, h.y, h.z), new(h.x, h.y, h.z)
            };

            builder.AddQuad(vertices[3], vertices[2], vertices[1], vertices[0], HouseFaceRole.Underside);
            builder.AddQuad(vertices[0], vertices[1], vertices[5], vertices[4], HouseFaceRole.Top);
            builder.AddQuad(vertices[3], vertices[4], vertices[5], vertices[2], HouseFaceRole.Front);
            builder.AddTriangle(vertices[0], vertices[4], vertices[3], HouseFaceRole.Left);
            builder.AddTriangle(vertices[1], vertices[2], vertices[5], HouseFaceRole.Right);
        }

        private static FaceRoles RolesFor(HouseGeometryKind kind)
        {
            return kind switch
            {
                HouseGeometryKind.Wall or HouseGeometryKind.Doorway or HouseGeometryKind.Window =>
                    new FaceRoles(HouseFaceRole.Exterior, HouseFaceRole.Interior, HouseFaceRole.Top, HouseFaceRole.Underside, HouseFaceRole.Left, HouseFaceRole.Right),
                HouseGeometryKind.Floor or HouseGeometryKind.Ceiling or HouseGeometryKind.Stairs =>
                    new FaceRoles(HouseFaceRole.Front, HouseFaceRole.Back, HouseFaceRole.Top, HouseFaceRole.Underside, HouseFaceRole.Left, HouseFaceRole.Right),
                _ => new FaceRoles(HouseFaceRole.Front, HouseFaceRole.Back, HouseFaceRole.Top, HouseFaceRole.Underside, HouseFaceRole.Left, HouseFaceRole.Right)
            };
        }

        private static void AddBox(MeshBuilder builder, Vector3 center, Vector3 size, FaceRoles roles)
        {
            Vector3 h = size * 0.5f;
            Vector3 p000 = center + new Vector3(-h.x, -h.y, -h.z);
            Vector3 p001 = center + new Vector3(-h.x, -h.y, h.z);
            Vector3 p010 = center + new Vector3(-h.x, h.y, -h.z);
            Vector3 p011 = center + new Vector3(-h.x, h.y, h.z);
            Vector3 p100 = center + new Vector3(h.x, -h.y, -h.z);
            Vector3 p101 = center + new Vector3(h.x, -h.y, h.z);
            Vector3 p110 = center + new Vector3(h.x, h.y, -h.z);
            Vector3 p111 = center + new Vector3(h.x, h.y, h.z);

            builder.AddQuad(p001, p101, p111, p011, roles.Front);
            builder.AddQuad(p100, p000, p010, p110, roles.Back);
            builder.AddQuad(p011, p111, p110, p010, roles.Top);
            builder.AddQuad(p000, p100, p101, p001, roles.Bottom);
            builder.AddQuad(p000, p001, p011, p010, roles.Left);
            builder.AddQuad(p101, p100, p110, p111, roles.Right);
        }

        private readonly struct FaceRoles
        {
            public HouseFaceRole Front { get; }
            public HouseFaceRole Back { get; }
            public HouseFaceRole Top { get; }
            public HouseFaceRole Bottom { get; }
            public HouseFaceRole Left { get; }
            public HouseFaceRole Right { get; }

            public FaceRoles(HouseFaceRole front, HouseFaceRole back, HouseFaceRole top, HouseFaceRole bottom, HouseFaceRole left, HouseFaceRole right)
            {
                Front = front;
                Back = back;
                Top = top;
                Bottom = bottom;
                Left = left;
                Right = right;
            }
        }

        private sealed class MeshBuilder
        {
            private readonly List<Vector3> vertices = new();
            private readonly List<Vector2> uv = new();
            private readonly List<int>[] triangles = new List<int>[MaterialSlotCount];

            public MeshBuilder()
            {
                for (int i = 0; i < triangles.Length; i++)
                {
                    triangles[i] = new List<int>();
                }
            }

            public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, HouseFaceRole role)
            {
                int start = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                vertices.Add(d);
                uv.Add(new Vector2(0f, 0f));
                uv.Add(new Vector2(1f, 0f));
                uv.Add(new Vector2(1f, 1f));
                uv.Add(new Vector2(0f, 1f));
                triangles[(int)role].AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, HouseFaceRole role)
            {
                int start = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                uv.Add(new Vector2(0f, 0f));
                uv.Add(new Vector2(1f, 0f));
                uv.Add(new Vector2(0.5f, 1f));
                triangles[(int)role].AddRange(new[] { start, start + 1, start + 2 });
            }

            public Mesh Build(string meshName)
            {
                Mesh mesh = new()
                {
                    name = meshName,
                    indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
                };
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uv);
                mesh.subMeshCount = MaterialSlotCount;
                for (int i = 0; i < MaterialSlotCount; i++)
                {
                    mesh.SetTriangles(triangles[i], i);
                }

                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                return mesh;
            }
        }
    }
}
