using System.Collections.Generic;
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
