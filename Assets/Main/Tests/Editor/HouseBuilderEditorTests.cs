using System.Collections.Generic;
using Neighbor.Main.Features.Interaction;
using Neighbor.Main.HouseBuilder;
using Neighbor.Main.HouseBuilder.Editor;
using Neighbor.Main.Features.Neighbor;
using NUnit.Framework;
using UnityEditor;
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
        public void Geometry_WallOpeningFragmentsShareMeterScaledUvs()
        {
            HouseGeometryDescriptor descriptor = new(HouseGeometryKind.Wall, new Vector3(4f, 3f, 0.25f));
            descriptor.AddOrUpdateWallOpening(new HouseWallOpeningData("window", Vector2.zero, new Vector2(1.5f, 1.25f)));

            Mesh mesh = Track(HouseGeometryFactory.BuildMesh(descriptor));
            Vector3[] vertices = mesh.vertices;
            Vector2[] uv = mesh.uv;
            int[] exteriorIndices = mesh.GetIndices((int)HouseFaceRole.Exterior);
            for (int i = 0; i < exteriorIndices.Length; i++)
            {
                int index = exteriorIndices[i];
                Assert.That(uv[index].x, Is.EqualTo(vertices[index].x).Within(0.001f));
                Assert.That(uv[index].y, Is.EqualTo(vertices[index].y).Within(0.001f));
            }

            Assert.That(mesh.bounds.size.x, Is.EqualTo(4f).Within(0.001f));
            Assert.That(mesh.bounds.size.y, Is.EqualTo(3f).Within(0.001f));
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
        public void Snapping_PreservesSurfacePlaneWhileSnappingGrid()
        {
            Vector3 result = HouseBuilderSnapUtility.SnapOnSurface(
                new Vector3(1.13f, 0.2f, 2.12f),
                Vector3.up,
                0.25f);

            Assert.That(result.x, Is.EqualTo(1.25f).Within(0.001f));
            Assert.That(result.y, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(result.z, Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void StarterFloorSnapping_AlignsItsBoundsToExistingEdge()
        {
            HousePlaceableDefinition floor = AssetDatabase.LoadAssetAtPath<HousePlaceableDefinition>(
                "Assets/Main/HouseBuilder/Data/Placeables/BasicFloor.asset");
            Bounds existingFloor = new(Vector3.up * 0.1f, new Vector3(4f, 0.2f, 4f));

            HousePlacementResult result = HouseBuilderSnapUtility.Calculate(
                new Vector3(2.1f, 0f, 0f),
                Quaternion.identity,
                false,
                default,
                floor.Placement,
                new HouseBuilderPlacementSettings(),
                new[] { existingFloor });

            Assert.That(floor.Placement.SnapBoundsToFeatures, Is.True);
            Assert.That(result.SnapKind, Is.EqualTo(HouseSnapKind.Edge));
            Assert.That(result.Position.x, Is.EqualTo(4f).Within(0.001f));
            Assert.That(result.Position.y, Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(result.Position.z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void StarterWallSnapping_GroundsBottomOnFloorTopEdge()
        {
            HousePlaceableDefinition wall = AssetDatabase.LoadAssetAtPath<HousePlaceableDefinition>(
                "Assets/Main/HouseBuilder/Data/Placeables/BasicWall.asset");
            GameObject floorObject = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floorObject.transform.position = Vector3.up * 0.1f;
            floorObject.transform.localScale = new Vector3(4f, 0.2f, 4f);
            Physics.SyncTransforms();
            Ray ray = new(new Vector3(1.9f, 5f, 0f), Vector3.down);
            Collider floorCollider = floorObject.GetComponent<Collider>();
            Assert.That(floorCollider.Raycast(ray, out RaycastHit hit, 10f), Is.True);

            HousePlacementResult result = HouseBuilderSnapUtility.Calculate(
                hit.point,
                Quaternion.identity,
                true,
                hit,
                wall.Placement,
                new HouseBuilderPlacementSettings(),
                new[] { floorCollider.bounds },
                ray.direction);

            float wallBottom = result.Position.y + wall.Placement.BoundsCenter.y - wall.Placement.BoundsSize.y * 0.5f;
            Assert.That(wall.Placement.GroundOnFeatureSnaps, Is.True);
            Assert.That(result.SnapKind, Is.EqualTo(HouseSnapKind.Edge));
            Assert.That(wallBottom, Is.EqualTo(floorCollider.bounds.max.y).Within(0.001f));
        }

        [Test]
        public void CeilingPlacement_CentersOverFloorAndRestsOnAdjacentWalls()
        {
            Bounds floor = new(new Vector3(2f, 0.1f, 2f), new Vector3(4f, 0.2f, 4f));
            Bounds wall = new(new Vector3(0f, 1.7f, 2f), new Vector3(0.25f, 3f, 4f));

            bool found = HouseBuilderEditorInteractionUtility.TryCalculateCeilingFootprintPlacement(
                new Vector3(0f, 1.5f, 2f),
                Vector3.up * 0.075f,
                new[] { floor },
                new[] { wall },
                out Vector3 position);

            Assert.That(found, Is.True);
            Assert.That(position.x, Is.EqualTo(floor.center.x).Within(0.001f));
            Assert.That(position.z, Is.EqualTo(floor.center.z).Within(0.001f));
            Assert.That(position.y, Is.EqualTo(wall.max.y + 0.075f).Within(0.001f));
        }

        [Test]
        public void CeilingPlacement_DoesNotSnapToFloorWithoutSupportingWalls()
        {
            Bounds floor = new(new Vector3(2f, 0.1f, 2f), new Vector3(4f, 0.2f, 4f));

            bool found = HouseBuilderEditorInteractionUtility.TryCalculateCeilingFootprintPlacement(
                floor.center,
                Vector3.up * 0.075f,
                new[] { floor },
                System.Array.Empty<Bounds>(),
                out _);

            Assert.That(found, Is.False);
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

        [Test]
        public void Geometry_ResizePreservesLinkedWallOpenings()
        {
            GameObject wallObject = Track(HouseGeometryFactory.Create(new HouseGeometryDescriptor(HouseGeometryKind.Wall, new Vector3(4f, 3f, 0.25f))));
            HouseGeometryObject wall = wallObject.GetComponent<HouseGeometryObject>();
            wall.Descriptor.AddOrUpdateWallOpening(new HouseWallOpeningData("door", Vector2.zero, new Vector2(1.2f, 2.1f)));

            wall.Resize(new Vector3(8f, 4f, 0.35f));

            Assert.That(wall.Descriptor.Size, Is.EqualTo(new Vector3(8f, 4f, 0.35f)));
            Assert.That(wall.Descriptor.WallOpenings.Count, Is.EqualTo(1));
            Assert.That(wall.GetComponent<MeshFilter>().sharedMesh.bounds.size.x, Is.EqualTo(8f).Within(0.01f));
        }

        [Test]
        public void FacePicker_MapsGeneratedWallTriangleToMaterialRole()
        {
            GameObject wallObject = Track(HouseGeometryFactory.Create(new HouseGeometryDescriptor(HouseGeometryKind.Wall, new Vector3(4f, 3f, 0.25f))));
            Physics.SyncTransforms();

            bool found = HouseBuilderEditorInteractionUtility.TryPickFace(
                new Ray(new Vector3(0f, 0f, 2f), Vector3.back),
                ~0,
                out HouseBuilderFaceHit face);

            Assert.That(found, Is.True);
            Assert.That(face.Owner, Is.EqualTo(wallObject.GetComponent<HouseBuilderObject>()));
            Assert.That(face.FaceRole, Is.EqualTo(HouseFaceRole.Exterior));
            Assert.That(face.MaterialIndex, Is.EqualTo((int)HouseFaceRole.Exterior));
        }

        [Test]
        public void GeometrySuggestedSizes_AreUsefulPerShape()
        {
            Vector3 wall = HouseBuilderEditorInteractionUtility.SuggestedSize(HouseGeometryKind.Wall);
            Vector3 floor = HouseBuilderEditorInteractionUtility.SuggestedSize(HouseGeometryKind.Floor);

            Assert.That(wall.y, Is.GreaterThan(wall.z));
            Assert.That(floor.z, Is.GreaterThan(floor.y));
            Assert.That(wall, Is.Not.EqualTo(floor));
        }

        [Test]
        public void SaveLoad_PreservesResizedStarterPrefabGeometry()
        {
            HouseBuilderCatalog catalog = AssetDatabase.LoadAssetAtPath<HouseBuilderCatalog>(HouseBuilderAssetInstaller.DefaultCatalogPath);
            HousePlaceableDefinition definition = AssetDatabase.LoadAssetAtPath<HousePlaceableDefinition>(
                "Assets/Main/HouseBuilder/Data/Placeables/BasicWall.asset");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(definition, Is.Not.Null);

            GameObject worldObject = Track(new GameObject("World"));
            HouseBuilderWorld world = worldObject.AddComponent<HouseBuilderWorld>();
            world.Configure(catalog);
            GameObject wall = world.CreatePlaceable(definition, Vector3.zero, Quaternion.identity);
            wall.GetComponent<HouseGeometryObject>().Resize(new Vector3(7f, 3.5f, 0.3f));

            world.LoadFromJson(world.SaveToJson());

            HouseGeometryObject loaded = world.GetComponentInChildren<HouseGeometryObject>();
            Assert.That(loaded.Descriptor.Size, Is.EqualTo(new Vector3(7f, 3.5f, 0.3f)));
        }

        [Test]
        public void StarterPrefabGhost_RebuildsVisibleGeometryAndDisablesCollisions()
        {
            HousePlaceableDefinition definition = AssetDatabase.LoadAssetAtPath<HousePlaceableDefinition>(
                "Assets/Main/HouseBuilder/Data/Placeables/BasicFloor.asset");
            Assert.That(definition, Is.Not.Null);

            GameObject preview = Track(Object.Instantiate(definition.Prefab));
            HouseBuilderGhost ghost = preview.AddComponent<HouseBuilderGhost>();
            ghost.Initialize();

            Transform physical = preview.transform.Find(HouseGeometryObject.PhysicalObjectName);
            Assert.That(physical, Is.Not.Null);
            Assert.That(physical.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
            Assert.That(physical.GetComponent<MeshRenderer>().enabled, Is.True);
            Assert.That(physical.GetComponent<MeshCollider>().enabled, Is.False);
        }

        [TestCase("BasicWall")]
        [TestCase("BasicFloor")]
        [TestCase("BasicCeiling")]
        public void StarterPrefab_ContainsPersistentVisibleGeometry(string prefabName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Main/HouseBuilder/Prefabs/Structures/{prefabName}.prefab");

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
            Assert.That(prefab.GetComponent<MeshCollider>().sharedMesh, Is.SameAs(prefab.GetComponent<MeshFilter>().sharedMesh));
            Assert.That(AssetDatabase.Contains(prefab.GetComponent<MeshFilter>().sharedMesh), Is.True);
            Transform physical = prefab.transform.Find(HouseGeometryObject.PhysicalObjectName);
            Assert.That(physical, Is.Not.Null);
            Assert.That(physical.GetComponent<MeshRenderer>().enabled, Is.True);
            Assert.That(physical.GetComponent<MeshCollider>().enabled, Is.True);
            Assert.That(physical.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(prefab.GetComponent<MeshFilter>().sharedMesh));
        }

        [TestCase("BasicWall")]
        [TestCase("BasicFloor")]
        [TestCase("BasicCeiling")]
        public void StarterPrefab_RegisteredEditorPlacementRemainsVisible(string prefabName)
        {
            HouseBuilderCatalog catalog = AssetDatabase.LoadAssetAtPath<HouseBuilderCatalog>(HouseBuilderAssetInstaller.DefaultCatalogPath);
            HousePlaceableDefinition definition = AssetDatabase.LoadAssetAtPath<HousePlaceableDefinition>(
                $"Assets/Main/HouseBuilder/Data/Placeables/{prefabName}.asset");
            GameObject worldObject = Track(new GameObject("World"));
            HouseBuilderWorld world = worldObject.AddComponent<HouseBuilderWorld>();
            world.Configure(catalog);
            GameObject instance = Track((GameObject)PrefabUtility.InstantiatePrefab(definition.Prefab, world.transform));

            HouseBuilderGhost accidentalPreviewState = instance.AddComponent<HouseBuilderGhost>();
            accidentalPreviewState.Initialize();
            Object.DestroyImmediate(accidentalPreviewState);
            world.RegisterPlaceable(instance, definition);

            MeshRenderer renderer = instance.GetComponent<MeshRenderer>();
            MeshFilter filter = instance.GetComponent<MeshFilter>();
            MeshCollider collider = instance.GetComponent<MeshCollider>();
            Transform physical = instance.transform.Find(HouseGeometryObject.PhysicalObjectName);
            Assert.That(instance.activeInHierarchy, Is.True);
            Assert.That(instance.GetComponent<HouseGeometryObject>().enabled, Is.True);
            Assert.That(physical, Is.Not.Null);
            Assert.That(renderer.enabled, Is.False);
            Assert.That(filter.sharedMesh, Is.Not.Null);
            Assert.That(filter.sharedMesh.vertexCount, Is.GreaterThan(0));
            Assert.That(collider.enabled, Is.False);
            Assert.That(physical.GetComponent<MeshRenderer>().enabled, Is.True);
            Assert.That(physical.GetComponent<MeshCollider>().enabled, Is.True);
            Assert.That(physical.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(filter.sharedMesh));
            Assert.That(physical.GetComponent<MeshCollider>().sharedMesh, Is.SameAs(filter.sharedMesh));
        }

        [TestCase("BasicWall")]
        [TestCase("BasicFloor")]
        [TestCase("BasicCeiling")]
        public void StarterPrefab_RegistrationRepairsEmptyPhysicalGeometry(string prefabName)
        {
            HouseBuilderCatalog catalog = AssetDatabase.LoadAssetAtPath<HouseBuilderCatalog>(HouseBuilderAssetInstaller.DefaultCatalogPath);
            HousePlaceableDefinition definition = AssetDatabase.LoadAssetAtPath<HousePlaceableDefinition>(
                $"Assets/Main/HouseBuilder/Data/Placeables/{prefabName}.asset");
            GameObject worldObject = Track(new GameObject("World"));
            HouseBuilderWorld world = worldObject.AddComponent<HouseBuilderWorld>();
            world.Configure(catalog);
            GameObject instance = Track((GameObject)PrefabUtility.InstantiatePrefab(definition.Prefab, world.transform));
            Mesh emptyMesh = Track(new Mesh());
            instance.GetComponent<MeshFilter>().sharedMesh = emptyMesh;
            instance.GetComponent<MeshCollider>().sharedMesh = emptyMesh;

            world.RegisterPlaceable(instance, definition);

            MeshFilter filter = instance.GetComponent<MeshFilter>();
            Transform physical = instance.transform.Find(HouseGeometryObject.PhysicalObjectName);
            Assert.That(filter.sharedMesh, Is.Not.SameAs(emptyMesh));
            Assert.That(filter.sharedMesh.vertexCount, Is.GreaterThan(0));
            Assert.That(physical, Is.Not.Null);
            Assert.That(physical.GetComponent<MeshRenderer>().enabled, Is.True);
            Assert.That(physical.GetComponent<MeshCollider>().sharedMesh, Is.SameAs(filter.sharedMesh));
        }

        [Test]
        public void PlacementRotation_SupportsYawPitchAndRoll()
        {
            Quaternion yaw = HouseBuilderEditorInteractionUtility.RotatePlacement(
                Quaternion.identity, HousePlacementRotationAxis.Yaw, 15f, 1f);
            Quaternion pitch = HouseBuilderEditorInteractionUtility.RotatePlacement(
                Quaternion.identity, HousePlacementRotationAxis.Pitch, 15f, 1f);
            Quaternion roll = HouseBuilderEditorInteractionUtility.RotatePlacement(
                Quaternion.identity, HousePlacementRotationAxis.Roll, 15f, 1f);

            Assert.That(Quaternion.Angle(yaw, Quaternion.Euler(0f, 15f, 0f)), Is.LessThan(0.01f));
            Assert.That(Quaternion.Angle(pitch, Quaternion.Euler(15f, 0f, 0f)), Is.LessThan(0.01f));
            Assert.That(Quaternion.Angle(roll, Quaternion.Euler(0f, 0f, 15f)), Is.LessThan(0.01f));
        }

        [Test]
        public void PlaceableCard_ClickingSelectedDefinitionRequestsDeselection()
        {
            HousePlaceableDefinition selected = AssetDatabase.LoadAssetAtPath<HousePlaceableDefinition>(
                "Assets/Main/HouseBuilder/Data/Placeables/BasicWall.asset");
            HousePlaceableDefinition different = AssetDatabase.LoadAssetAtPath<HousePlaceableDefinition>(
                "Assets/Main/HouseBuilder/Data/Placeables/BasicFloor.asset");

            Assert.That(HouseBuilderEditorInteractionUtility.ShouldDeselectPlaceable(selected, selected), Is.True);
            Assert.That(HouseBuilderEditorInteractionUtility.ShouldDeselectPlaceable(selected, different), Is.False);
            Assert.That(HouseBuilderEditorInteractionUtility.ShouldDeselectPlaceable(null, selected), Is.False);
        }

        [Test]
        public void WiringPorts_ShowWhenConnectableObjectIsSelected()
        {
            GameObject connectableObject = Track(new GameObject("Connectable"));
            HouseBuilderObject connectable = connectableObject.AddComponent<HouseBuilderObject>();
            connectableObject.AddComponent<HouseWireEndpoint>();
            GameObject plainObject = Track(new GameObject("Plain"));
            HouseBuilderObject plain = plainObject.AddComponent<HouseBuilderObject>();

            Assert.That(HouseBuilderEditorInteractionUtility.ShouldShowWirePorts(false, false, connectable), Is.True);
            Assert.That(HouseBuilderEditorInteractionUtility.ShouldShowWirePorts(false, false, plain), Is.False);
            Assert.That(HouseBuilderEditorInteractionUtility.ShouldShowWirePorts(true, false, null), Is.True);
            Assert.That(HouseBuilderEditorInteractionUtility.ShouldShowWirePorts(false, true, null), Is.True);
        }

        [Test]
        public void PlacementEraser_OnlyPicksMatchingNearestPlacedObject()
        {
            GameObject world = Track(new GameObject("World"));
            GameObject matching = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            matching.transform.SetParent(world.transform);
            matching.AddComponent<HouseBuilderObject>().Initialize("definition.wall", HouseBuilderCategories.Wall);
            Physics.SyncTransforms();

            bool found = HouseBuilderEditorInteractionUtility.TryPickMatchingPlacedObject(
                new Ray(new Vector3(0f, 0f, 5f), Vector3.back),
                ~0,
                world.transform,
                null,
                "definition.wall",
                out HouseBuilderObject result);

            Assert.That(found, Is.True);
            Assert.That(result.gameObject, Is.EqualTo(matching));

            GameObject different = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            different.transform.SetParent(world.transform);
            different.transform.position = new Vector3(0f, 0f, 2f);
            different.AddComponent<HouseBuilderObject>().Initialize("definition.chair", HouseBuilderCategories.Furniture);
            Physics.SyncTransforms();

            Assert.That(HouseBuilderEditorInteractionUtility.TryPickMatchingPlacedObject(
                new Ray(new Vector3(0f, 0f, 5f), Vector3.back),
                ~0,
                world.transform,
                null,
                "definition.wall",
                out _), Is.False);
        }

        [Test]
        public void StarterPlacement_GroundAssetsCanUseGridButWallMountedAssetsRequireSurface()
        {
            HousePlaceableDefinition floor = AssetDatabase.LoadAssetAtPath<HousePlaceableDefinition>(
                "Assets/Main/HouseBuilder/Data/Placeables/BasicFloor.asset");
            HousePlaceableDefinition door = AssetDatabase.LoadAssetAtPath<HousePlaceableDefinition>(
                "Assets/Main/HouseBuilder/Data/Placeables/Door.asset");

            Assert.That(floor.Placement.RequireSurface, Is.False);
            Assert.That(door.Placement.RequireSurface, Is.True);
        }

        [Test]
        public void ExpandedCatalog_ContainsFlexibleProjectPrefabs()
        {
            HouseBuilderCatalog catalog = AssetDatabase.LoadAssetAtPath<HouseBuilderCatalog>(HouseBuilderAssetInstaller.DefaultCatalogPath);

            Assert.That(catalog.Placeables.Count, Is.GreaterThanOrEqualTo(45));
            Assert.That(catalog.TryGetPlaceable("neighbor.prop.crowbar", out _), Is.True);
            Assert.That(catalog.TryGetPlaceable("neighbor.furniture.bed", out _), Is.True);
            Assert.That(catalog.TryGetPlaceable("neighbor.wiring.lasergrid", out _), Is.True);
            Assert.That(catalog.TryGetPlaceable("neighbor.wiring.trapdoor", out _), Is.True);
        }

        [Test]
        public void FlexiblePlaceables_AllowAnySurfaceWithoutCollisionRestriction()
        {
            HousePlaceableDefinition chair = AssetDatabase.LoadAssetAtPath<HousePlaceableDefinition>(
                "Assets/Main/HouseBuilder/Data/Placeables/Chair.asset");
            HousePlaceableDefinition lightSwitch = AssetDatabase.LoadAssetAtPath<HousePlaceableDefinition>(
                "Assets/Main/HouseBuilder/Data/Placeables/LightSwitch.asset");

            Assert.That(chair.Placement.AllowedSurfaces, Is.EqualTo(HouseSurfaceType.Any));
            Assert.That(chair.Placement.ValidateCollisions, Is.False);
            Assert.That(lightSwitch.Placement.AllowedSurfaces, Is.EqualTo(HouseSurfaceType.Any));
        }

        [Test]
        public void WallAlignment_FacesMountedObjectOutFromWall()
        {
            HousePlaceableDefinition lightSwitch = AssetDatabase.LoadAssetAtPath<HousePlaceableDefinition>(
                "Assets/Main/HouseBuilder/Data/Placeables/LightSwitch.asset");
            GameObject wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.localScale = new Vector3(5f, 5f, 0.1f);
            Physics.SyncTransforms();
            Ray[] rays =
            {
                new(Vector3.forward * 5f, Vector3.back),
                new(Vector3.back * 5f, Vector3.forward)
            };

            for (int i = 0; i < rays.Length; i++)
            {
                Assert.That(Physics.Raycast(rays[i], out RaycastHit hit), Is.True);
                HousePlacementResult placement = HouseBuilderSnapUtility.Calculate(
                    hit.point,
                    Quaternion.identity,
                    true,
                    hit,
                    lightSwitch.Placement,
                    new HouseBuilderPlacementSettings(),
                    null,
                    rays[i].direction);

                Assert.That(Vector3.Dot(placement.Rotation * Vector3.forward, -rays[i].direction), Is.GreaterThan(0.99f));
            }
        }

        [Test]
        public void WallAlignment_FlipsBackFaceNormalTowardApproachedSide()
        {
            Assert.That(
                HouseBuilderSnapUtility.ResolveSurfaceNormal(Vector3.forward, Vector3.forward),
                Is.EqualTo(Vector3.back));
            Assert.That(
                HouseBuilderSnapUtility.ResolveSurfaceNormal(Vector3.forward, Vector3.back),
                Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void Glass_AlignsPaneFlatToWallAndCreatesWindowOpening()
        {
            HousePlaceableDefinition glass = AssetDatabase.LoadAssetAtPath<HousePlaceableDefinition>(
                "Assets/Main/HouseBuilder/Data/Placeables/Glass.asset");
            GameObject wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.localScale = new Vector3(5f, 5f, 0.1f);
            Physics.SyncTransforms();
            Ray ray = new(Vector3.forward * 5f, Vector3.back);
            Assert.That(Physics.Raycast(ray, out RaycastHit hit), Is.True);

            HousePlacementResult placement = HouseBuilderSnapUtility.Calculate(
                hit.point,
                Quaternion.identity,
                true,
                hit,
                glass.Placement,
                new HouseBuilderPlacementSettings(),
                null,
                ray.direction);

            Assert.That(glass.Placement.SurfaceAlignment, Is.EqualTo(HouseSurfaceAlignment.RightToNormal));
            Assert.That(Vector3.Dot(placement.Rotation * Vector3.right, -ray.direction), Is.GreaterThan(0.99f));
            Assert.That(glass.WallOpening.Enabled, Is.True);
            Assert.That(glass.WallOpening.Size.x, Is.EqualTo(0.85f).Within(0.001f));
            Assert.That(glass.WallOpening.Size.y, Is.EqualTo(1.15f).Within(0.001f));
        }

        [Test]
        public void Glass_PlacedOnBuilderWallCreatesOpening()
        {
            HouseBuilderCatalog catalog = AssetDatabase.LoadAssetAtPath<HouseBuilderCatalog>(HouseBuilderAssetInstaller.DefaultCatalogPath);
            Assert.That(catalog.TryGetPlaceable("neighbor.prop.glass", out HousePlaceableDefinition glass), Is.True);
            GameObject worldObject = Track(new GameObject("World"));
            HouseBuilderWorld world = worldObject.AddComponent<HouseBuilderWorld>();
            world.Configure(catalog);
            GameObject wallObject = Track(HouseGeometryFactory.Create(
                new HouseGeometryDescriptor(HouseGeometryKind.Wall, new Vector3(4f, 3f, 0.25f))));
            wallObject.transform.SetParent(worldObject.transform);
            HouseGeometryObject wall = wallObject.GetComponent<HouseGeometryObject>();
            GameObject glassObject = world.CreatePlaceable(glass, Vector3.forward * wall.Descriptor.Size.z * 0.5f, Quaternion.identity);

            bool created = world.TryCreateWallOpening(glassObject, glass, wallObject.GetComponent<Collider>());

            Assert.That(created, Is.True);
            Assert.That(glass.WallOpening.CenterPlacedObjectInWall, Is.True);
            Assert.That(wallObject.transform.InverseTransformPoint(glassObject.transform.position).z, Is.EqualTo(0f).Within(0.001f));
            Assert.That(wall.Descriptor.WallOpenings.Count, Is.EqualTo(1));
            Assert.That(wall.Descriptor.WallOpenings[0].Size.x, Is.EqualTo(glass.WallOpening.Size.x + glass.WallOpening.Margin * 2f).Within(0.001f));
            Assert.That(wall.Descriptor.WallOpenings[0].Size.y, Is.EqualTo(glass.WallOpening.Size.y + glass.WallOpening.Margin * 2f).Within(0.001f));
        }

        [Test]
        public void GlassPrefab_IntactPaneIsFixedAndOnlyShardsHaveRigidbodies()
        {
            GameObject glassPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Main/Features/Interaction/Items/Glass/Prefabs/PlaceholderGlass.prefab");

            Assert.That(glassPrefab, Is.Not.Null);
            Assert.That(glassPrefab.GetComponent<Pickupable>(), Is.Null);
            Assert.That(glassPrefab.GetComponent<Rigidbody>(), Is.Null);
            Assert.That(glassPrefab.GetComponent<GlassShatter>(), Is.Not.Null);
            Assert.That(glassPrefab.transform.Find("Shards").GetComponentsInChildren<Rigidbody>(true).Length, Is.GreaterThan(0));
        }

        [TestCase("Mirror")]
        [TestCase("Curtains")]
        [TestCase("LaserGrid")]
        [TestCase("TV")]
        [TestCase("VentCover")]
        public void WallDecoration_UsesOutwardWallAlignment(string definitionName)
        {
            HousePlaceableDefinition definition = AssetDatabase.LoadAssetAtPath<HousePlaceableDefinition>(
                $"Assets/Main/HouseBuilder/Data/Placeables/{definitionName}.asset");

            Assert.That(definition.Placement.SurfaceAlignment, Is.EqualTo(HouseSurfaceAlignment.ForwardToNormal));
        }

        [Test]
        public void Mirror_UsesFixedWideReflectionFieldOfView()
        {
            GameObject mirrorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Main/Features/Interaction/Items/Mirrors/Prefabs/PlaceholderMirror.prefab");
            PlanarMirror mirror = mirrorPrefab.GetComponent<PlanarMirror>();

            Assert.That(mirror, Is.Not.Null);
            Assert.That(mirror.ReflectionFieldOfView, Is.EqualTo(72f));
        }

        [Test]
        public void Door_CanPlaceOnGroundOrWallAndGroundsWallPlacement()
        {
            HousePlaceableDefinition door = AssetDatabase.LoadAssetAtPath<HousePlaceableDefinition>(
                "Assets/Main/HouseBuilder/Data/Placeables/Door.asset");
            GameObject wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.position = Vector3.up * 2f;
            wall.transform.localScale = new Vector3(5f, 4f, 0.1f);
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(new Ray(new Vector3(0f, 2f, 5f), Vector3.back), out RaycastHit hit), Is.True);

            HousePlacementResult placement = HouseBuilderSnapUtility.Calculate(
                hit.point,
                Quaternion.identity,
                true,
                hit,
                door.Placement,
                new HouseBuilderPlacementSettings(),
                null);
            float placedMinimum = placement.Position.y + door.Placement.BoundsCenter.y - door.Placement.BoundsSize.y * 0.5f;

            Assert.That(door.Placement.AllowedSurfaces.HasFlag(HouseSurfaceType.Ground), Is.True);
            Assert.That(door.Placement.AllowedSurfaces.HasFlag(HouseSurfaceType.Wall), Is.True);
            Assert.That(door.Placement.GroundOnWall, Is.True);
            Assert.That(placedMinimum, Is.EqualTo(wall.GetComponent<Collider>().bounds.min.y).Within(0.001f));
        }

        [TestCase("Door")]
        [TestCase("LockedDoor")]
        [TestCase("WindowBlinds")]
        public void WallOpening_MatchesVisiblePrefabBounds(string definitionName)
        {
            HousePlaceableDefinition definition = AssetDatabase.LoadAssetAtPath<HousePlaceableDefinition>(
                $"Assets/Main/HouseBuilder/Data/Placeables/{definitionName}.asset");

            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.WallOpening.Enabled, Is.True);
            Assert.That(definition.WallOpening.Size.x, Is.EqualTo(definition.Placement.BoundsSize.x).Within(0.001f));
            Assert.That(definition.WallOpening.Size.y, Is.EqualTo(definition.Placement.BoundsSize.y).Within(0.001f));
            Assert.That(definition.WallOpening.Center.x, Is.EqualTo(definition.Placement.BoundsCenter.x).Within(0.001f));
            Assert.That(definition.WallOpening.Center.y, Is.EqualTo(definition.Placement.BoundsCenter.y).Within(0.001f));
        }

        [Test]
        public void WallOpeningLink_RefreshUsesLatestCatalogProfile()
        {
            HouseBuilderCatalog catalog = AssetDatabase.LoadAssetAtPath<HouseBuilderCatalog>(HouseBuilderAssetInstaller.DefaultCatalogPath);
            Assert.That(catalog.TryGetPlaceable("neighbor.window.windowblinds", out HousePlaceableDefinition window), Is.True);

            GameObject worldObject = Track(new GameObject("World"));
            HouseBuilderWorld world = worldObject.AddComponent<HouseBuilderWorld>();
            world.Configure(catalog);
            GameObject wallObject = Track(HouseGeometryFactory.Create(
                new HouseGeometryDescriptor(HouseGeometryKind.Wall, new Vector3(5f, 3f, 0.25f))));
            wallObject.transform.SetParent(worldObject.transform);
            HouseGeometryObject wall = wallObject.GetComponent<HouseGeometryObject>();
            GameObject windowObject = world.CreatePlaceable(window, Vector3.zero, Quaternion.identity);
            HouseWallOpeningLink link = windowObject.AddComponent<HouseWallOpeningLink>();

            link.Initialize(wall, new HouseWallOpeningProfile(Vector3.one, Vector3.zero));
            link.RefreshOpening();

            HouseWallOpeningData opening = wall.Descriptor.WallOpenings[0];
            Assert.That(opening.Size.x, Is.EqualTo(window.WallOpening.Size.x + window.WallOpening.Margin * 2f).Within(0.001f));
            Assert.That(opening.Size.y, Is.EqualTo(window.WallOpening.Size.y + window.WallOpening.Margin * 2f).Within(0.001f));
            Assert.That(opening.Center.y, Is.EqualTo(window.WallOpening.Center.y).Within(0.001f));
        }

        [Test]
        public void WallOpeningLink_DoesNotFollowAnimatedDoorTransformDuringGameplay()
        {
            GameObject wallObject = Track(HouseGeometryFactory.Create(
                new HouseGeometryDescriptor(HouseGeometryKind.Wall, new Vector3(5f, 3f, 0.25f))));
            HouseGeometryObject wall = wallObject.GetComponent<HouseGeometryObject>();
            GameObject doorObject = Track(new GameObject("Door"));
            HouseBuilderObject door = doorObject.AddComponent<HouseBuilderObject>();
            door.Initialize("door", HouseBuilderCategories.Door);
            HouseWallOpeningLink link = doorObject.AddComponent<HouseWallOpeningLink>();
            link.Initialize(wall, new HouseWallOpeningProfile(new Vector3(1.2f, 2.3f, 0.6f), new Vector3(0f, 1.1f, 0f)));
            HouseWallOpeningData initial = wall.Descriptor.WallOpenings[0];

            doorObject.transform.SetPositionAndRotation(new Vector3(1f, 0f, 0.5f), Quaternion.Euler(0f, 95f, 0f));
            link.RefreshIfMoved(false);

            HouseWallOpeningData afterAnimation = wall.Descriptor.WallOpenings[0];
            Assert.That(afterAnimation.Center, Is.EqualTo(initial.Center));
            Assert.That(afterAnimation.Size, Is.EqualTo(initial.Size));
        }

        [Test]
        public void WallOpeningLink_PlayModeEnableRestoresOpeningWithoutFollowingAnimation()
        {
            GameObject wallObject = Track(HouseGeometryFactory.Create(
                new HouseGeometryDescriptor(HouseGeometryKind.Wall, new Vector3(5f, 3f, 0.25f))));
            HouseGeometryObject wall = wallObject.GetComponent<HouseGeometryObject>();
            GameObject doorObject = Track(new GameObject("Door"));
            HouseBuilderObject door = doorObject.AddComponent<HouseBuilderObject>();
            door.Initialize("door", HouseBuilderCategories.Door);
            HouseWallOpeningLink link = doorObject.AddComponent<HouseWallOpeningLink>();
            link.Initialize(wall, new HouseWallOpeningProfile(new Vector3(1.2f, 2.3f, 0.6f), new Vector3(0f, 1.1f, 0f)));
            wall.Descriptor.RemoveWallOpening(door.InstanceId);
            wall.Rebuild();

            link.RefreshOpening();
            doorObject.transform.SetPositionAndRotation(new Vector3(1f, 0f, 0.5f), Quaternion.Euler(0f, 95f, 0f));
            link.RefreshIfMoved(false);

            Assert.That(wall.Descriptor.WallOpenings.Count, Is.EqualTo(1));
            Assert.That(wall.Descriptor.WallOpenings[0].Center, Is.EqualTo(new Vector2(0f, 1.1f)));
        }

        [TestCase(true, true, false, false)]
        [TestCase(false, false, false, false)]
        [TestCase(false, true, true, false)]
        [TestCase(false, true, false, true)]
        public void WallOpeningLink_OnlyRemovesOpeningForRealEditModeDeletion(
            bool applicationIsPlaying,
            bool sceneIsLoaded,
            bool editorPlayingOrChangingMode,
            bool expected)
        {
            Assert.That(
                HouseWallOpeningLink.ShouldRemoveOpeningOnDestroy(
                    applicationIsPlaying,
                    sceneIsLoaded,
                    editorPlayingOrChangingMode),
                Is.EqualTo(expected));
        }

        [Test]
        public void MaterialBindings_ReapplyAfterGeometryRebuild()
        {
            HouseBuilderCatalog catalog = AssetDatabase.LoadAssetAtPath<HouseBuilderCatalog>(HouseBuilderAssetInstaller.DefaultCatalogPath);
            HouseMaterialDefinition material = catalog.Materials[catalog.Materials.Count - 1];
            GameObject worldObject = Track(new GameObject("World"));
            HouseBuilderWorld world = worldObject.AddComponent<HouseBuilderWorld>();
            world.Configure(catalog);
            GameObject wallObject = Track(HouseGeometryFactory.Create(new HouseGeometryDescriptor(HouseGeometryKind.Wall, new Vector3(4f, 3f, 0.25f))));
            wallObject.transform.SetParent(worldObject.transform);
            HouseGeometryObject geometry = wallObject.GetComponent<HouseGeometryObject>();
            geometry.PrepareForPlacement();
            HouseBuilderMaterialController controller = wallObject.GetComponent<HouseBuilderMaterialController>();
            controller.SetBinding(HouseFaceRole.Top, string.Empty, (int)HouseFaceRole.Top, material.Id);

            controller.ApplyFromWorld();
            geometry.Rebuild();

            Renderer physical = wallObject.transform.Find(HouseGeometryObject.PhysicalObjectName).GetComponent<Renderer>();
            Assert.That(physical.sharedMaterials[(int)HouseFaceRole.Top], Is.EqualTo(material.Material));
        }

        [TestCase("BoxingGloveTrap")]
        [TestCase("CardboardBox")]
        [TestCase("Chair")]
        [TestCase("Closet")]
        [TestCase("Cupboard")]
        [TestCase("SawBladeTrap")]
        public void VisibleGroundPlaceable_PlacementBoundsRestOnSurface(string definitionName)
        {
            HousePlaceableDefinition definition = AssetDatabase.LoadAssetAtPath<HousePlaceableDefinition>(
                $"Assets/Main/HouseBuilder/Data/Placeables/{definitionName}.asset");

            Assert.That(definition, Is.Not.Null);
            float placedMinimum = definition.Placement.BoundsCenter.y
                - definition.Placement.BoundsSize.y * 0.5f
                + definition.Placement.PlacementOffset.y;

            Assert.That(placedMinimum, Is.GreaterThanOrEqualTo(-0.001f), definition.DisplayName);
        }

        [Test]
        public void ReinforcementLocation_RoundTripsStableTriggerLinkAndSelections()
        {
            HouseBuilderCatalog catalog = AssetDatabase.LoadAssetAtPath<HouseBuilderCatalog>(HouseBuilderAssetInstaller.DefaultCatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryGetPlaceable("neighbor.ai.reinforcement_trigger.reinforcementtrigger", out HousePlaceableDefinition triggerDefinition), Is.True);
            Assert.That(catalog.TryGetPlaceable("neighbor.ai.reinforcement_location.reinforcementlocation", out HousePlaceableDefinition locationDefinition), Is.True);
            Assert.That(catalog.TryGetPlaceable("neighbor.ai.reinforcement.securitycamerareinforcement", out HousePlaceableDefinition reinforcementDefinition), Is.True);

            GameObject worldObject = Track(new GameObject("World"));
            HouseBuilderWorld world = worldObject.AddComponent<HouseBuilderWorld>();
            world.Configure(catalog);
            GameObject triggerObject = world.CreatePlaceable(triggerDefinition, Vector3.zero, Quaternion.identity);
            HouseBuilderObject trigger = triggerObject.GetComponent<HouseBuilderObject>();
            string triggerId = trigger.InstanceId;
            GameObject locationObject = world.CreatePlaceable(locationDefinition, Vector3.right * 3f, Quaternion.Euler(0f, 45f, 0f));
            HouseReinforcementLocation location = locationObject.GetComponent<HouseReinforcementLocation>();
            location.Configure(triggerId, new[] { reinforcementDefinition.Id });

            world.LoadFromJson(world.SaveToJson());

            HouseReinforcementLocation loaded = world.GetComponentInChildren<HouseReinforcementLocation>();
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.TriggerInstanceId, Is.EqualTo(triggerId));
            Assert.That(loaded.ReinforcementDefinitionIds, Is.EquivalentTo(new[] { reinforcementDefinition.Id }));
        }

        [Test]
        public void ReinforcementTrigger_SelectsLinkedBuilderLocationAsSpawnAnchor()
        {
            HouseBuilderCatalog catalog = AssetDatabase.LoadAssetAtPath<HouseBuilderCatalog>(HouseBuilderAssetInstaller.DefaultCatalogPath);
            Assert.That(catalog.TryGetPlaceable("neighbor.ai.reinforcement_trigger.reinforcementtrigger", out HousePlaceableDefinition triggerDefinition), Is.True);
            Assert.That(catalog.TryGetPlaceable("neighbor.ai.reinforcement_location.reinforcementlocation", out HousePlaceableDefinition locationDefinition), Is.True);
            Assert.That(catalog.TryGetPlaceable("neighbor.ai.reinforcement.securitycamerareinforcement", out HousePlaceableDefinition reinforcementDefinition), Is.True);

            GameObject worldObject = Track(new GameObject("World"));
            HouseBuilderWorld world = worldObject.AddComponent<HouseBuilderWorld>();
            world.Configure(catalog);
            GameObject triggerObject = world.CreatePlaceable(triggerDefinition, Vector3.zero, Quaternion.identity);
            GameObject locationObject = world.CreatePlaceable(locationDefinition, Vector3.right * 3f, Quaternion.identity);
            locationObject.GetComponent<HouseReinforcementLocation>().Configure(
                triggerObject.GetComponent<HouseBuilderObject>().InstanceId,
                new[] { reinforcementDefinition.Id });

            bool found = triggerObject.GetComponent<ReinforcementTrigger>().TryGetConfiguredBuilderReinforcement(
                new ReinforcementBudget(10),
                out ReinforcementPrefabSelection selection);

            Assert.That(found, Is.True);
            Assert.That(selection.Anchor, Is.EqualTo(locationObject.transform));
            Assert.That(selection.Prefab, Is.EqualTo(reinforcementDefinition.Prefab));
        }

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }
    }
}
