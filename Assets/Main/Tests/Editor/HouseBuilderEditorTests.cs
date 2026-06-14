using System.Collections.Generic;
using Neighbor.Main.HouseBuilder;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Neighbor.Main.Tests
{
    public sealed class HouseBuilderEditorTests
    {
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null)
                {
                    Object.DestroyImmediate(created[i]);
                }
            }

            created.Clear();
        }

        [Test]
        public void WallOpening_RemovesAllFrontFacesInsideOpening()
        {
            HouseGeometryDescriptor descriptor = new(HouseGeometryKind.Wall, new Vector3(4f, 3f, 0.25f));
            descriptor.AddOrUpdateWallOpening(new HouseWallOpeningData("door", new Vector2(0f, -0.45f), new Vector2(1.2f, 2.1f)));

            Mesh mesh = Track(HouseGeometryFactory.BuildMesh(descriptor));
            int[] triangles = mesh.GetTriangles((int)HouseFaceRole.Exterior);
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 center = (vertices[triangles[i]] + vertices[triangles[i + 1]] + vertices[triangles[i + 2]]) / 3f;
                bool insideOpening = Mathf.Abs(center.x) < 0.6f && center.y > -1.5f && center.y < 0.6f;
                Assert.That(insideOpening, Is.False, $"Front face remained inside the door opening at {center}.");
            }
        }

        [Test]
        public void WallOpening_LinkUpdatesAndRemovesOpening()
        {
            GameObject wallObject = Track(HouseGeometryFactory.Create(new HouseGeometryDescriptor(HouseGeometryKind.Wall, new Vector3(4f, 3f, 0.25f))));
            HouseGeometryObject wall = wallObject.GetComponent<HouseGeometryObject>();
            GameObject door = Track(new GameObject("Door"));
            HouseBuilderObject owner = door.AddComponent<HouseBuilderObject>();
            owner.Initialize("door", HouseBuilderCategories.Door);
            HouseWallOpeningLink link = door.AddComponent<HouseWallOpeningLink>();

            link.Initialize(wall, new HouseWallOpeningProfile(new Vector3(1.2f, 2.1f, 0.5f), new Vector3(0f, 1.05f, 0f)));
            Assert.That(wall.Descriptor.WallOpenings.Count, Is.EqualTo(1));

            Object.DestroyImmediate(link);
            Assert.That(wall.Descriptor.WallOpenings.Count, Is.Zero);
        }

        [Test]
        public void Wiring_RoutesTypedSignalThroughSerializableConnection()
        {
            GameObject worldObject = Track(new GameObject("World"));
            HouseBuilderWorld world = worldObject.AddComponent<HouseBuilderWorld>();
            GameObject sourceObject = Track(new GameObject("Source"));
            sourceObject.transform.SetParent(worldObject.transform);
            HouseBuilderObject sourceOwner = sourceObject.AddComponent<HouseBuilderObject>();
            sourceOwner.Initialize("source", HouseBuilderCategories.Wiring);
            HouseWireEndpoint source = sourceObject.AddComponent<HouseWireEndpoint>();
            source.EnsureIdentity();
            HouseWirePortDefinition output = source.AddPort("Output", HouseWirePortDirection.Output, HouseSignalKind.Bool);

            GameObject targetObject = Track(new GameObject("Target"));
            targetObject.transform.SetParent(worldObject.transform);
            HouseBuilderObject targetOwner = targetObject.AddComponent<HouseBuilderObject>();
            targetOwner.Initialize("target", HouseBuilderCategories.Wiring);
            HouseWireEndpoint target = targetObject.AddComponent<HouseWireEndpoint>();
            target.EnsureIdentity();
            HouseWirePortDefinition input = target.AddPort("Input", HouseWirePortDirection.Input, HouseSignalKind.Bool);

            bool received = false;
            target.InputReceived += (_, signal) => received = signal.BoolValue;
            Assert.That(world.WireGraph.TryConnect(source, output, target, input, out string error), Is.True, error);

            source.Emit(output.Id, HouseSignal.Bool(true));

            Assert.That(received, Is.True);
            Assert.That(world.WireGraph.Connections.Count, Is.EqualTo(1));
        }

        [Test]
        public void SaveLoad_RoundTripsGeometryOpeningsMaterialsAndConnections()
        {
            GameObject worldObject = Track(new GameObject("World"));
            HouseBuilderWorld world = worldObject.AddComponent<HouseBuilderWorld>();
            HouseGeometryDescriptor descriptor = new(HouseGeometryKind.Wall, new Vector3(5f, 3f, 0.25f));
            descriptor.AddOrUpdateWallOpening(new HouseWallOpeningData("linked-door", new Vector2(0.5f, -0.4f), new Vector2(1.2f, 2.2f)));
            GameObject wall = HouseGeometryFactory.Create(descriptor);
            wall.transform.SetParent(worldObject.transform);
            HouseBuilderMaterialController materials = wall.GetComponent<HouseBuilderMaterialController>();
            materials.SetBinding(HouseFaceRole.Interior, string.Empty, (int)HouseFaceRole.Interior, "material.interior");

            string json = world.SaveToJson();
            world.LoadFromJson(json);

            HouseGeometryObject loadedGeometry = world.GetComponentInChildren<HouseGeometryObject>();
            Assert.That(loadedGeometry, Is.Not.Null);
            Assert.That(loadedGeometry.Descriptor.Kind, Is.EqualTo(HouseGeometryKind.Wall));
            Assert.That(loadedGeometry.Descriptor.WallOpenings.Count, Is.EqualTo(1));
            Assert.That(loadedGeometry.Descriptor.WallOpenings[0].OwnerObjectId, Is.EqualTo("linked-door"));
            Assert.That(loadedGeometry.GetComponent<HouseBuilderMaterialController>().Bindings.Count, Is.EqualTo(1));
        }

        [Test]
        public void Snapping_PrefersNearbyCornerAndSnapsRotation()
        {
            HouseBuilderPlacementSettings settings = new();
            HousePlacementResult result = HouseBuilderSnapUtility.Calculate(
                new Vector3(0.9f, 0.9f, 0.9f),
                Quaternion.Euler(0f, 17f, 0f),
                false,
                default,
                new HousePlacementProfile(),
                settings,
                new[] { new Bounds(Vector3.one * 0.5f, Vector3.one) });

            Assert.That(result.SnapKind, Is.EqualTo(HouseSnapKind.Corner));
            Assert.That(result.Position, Is.EqualTo(Vector3.one));
            Assert.That(result.Rotation.eulerAngles.y, Is.EqualTo(15f).Within(0.01f));
        }

        [Test]
        public void Geometry_AllSupportedPrimitivesBuildValidMeshes()
        {
            HouseGeometryKind[] kinds =
            {
                HouseGeometryKind.Cube,
                HouseGeometryKind.Wall,
                HouseGeometryKind.Floor,
                HouseGeometryKind.Ceiling,
                HouseGeometryKind.Doorway,
                HouseGeometryKind.Window,
                HouseGeometryKind.Ramp,
                HouseGeometryKind.Stairs
            };

            foreach (HouseGeometryKind kind in kinds)
            {
                Mesh mesh = Track(HouseGeometryFactory.BuildMesh(new HouseGeometryDescriptor(kind, new Vector3(4f, 3f, 2f))));
                Assert.That(mesh.vertexCount, Is.GreaterThan(0), kind.ToString());
                Assert.That(mesh.subMeshCount, Is.EqualTo(HouseGeometryFactory.MaterialSlotCount), kind.ToString());
            }
        }

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }
    }
}
