# House Builder Level Editor

`HouseBuilderLevelEditor` is a runtime-capable, data-driven house and level construction system with a Unity Editor front end.

Open it from:

`Tools > Neighbor > House Builder > Level Editor`

The default catalog and marker prefabs are created automatically in `Assets/Main/HouseBuilder/Data` and `Assets/Main/HouseBuilder/Prefabs`. They can also be refreshed from:

`Tools > Neighbor > House Builder > Create or Refresh Default Assets`

## Architecture

The implementation is split into two layers:

- `Runtime`: IDs, catalogs, placement, snapping, validation, geometry, wall openings, face materials, wiring, AI markers, and versioned JSON save/load.
- `Editor`: the level-editor window, Scene view input and visualization, default asset generation, and ProBuilder CSG integration.

The editor window orchestrates runtime APIs instead of owning build state. A future in-game builder can replace the window and Scene view input while reusing `HouseBuilderWorld`, `HouseBuilderSnapUtility`, `HouseBuilderPlacementValidator`, `HouseGeometryFactory`, `HouseWireGraph`, and `HouseBuilderSaveSystem`.

Every placed object has a stable `HouseBuilderObject.InstanceId`. Definitions, materials, categories, wire ports, and connections use stable string IDs rather than Unity instance IDs. This is the basis for future multiplayer commands and mod-provided catalogs.

## Designer Workflow

1. Open the House Builder Level Editor.
2. Create or assign a `HouseBuilderWorld`.
3. Use **Build** to place catalog prefabs and AI nodes.
4. Use **Geometry** to create walls, floors, ceilings, doorway blocks, window blocks, ramps, stairs, and cubes.
5. Use **Materials** to assign a catalog material to a selected face or side.
6. Use **Wiring** to click an orange output port followed by a cyan input port.
7. Use **Boolean** to run subtract, intersect, or union on two mesh objects.
8. Use **Save** to write or load a versioned `.house.json` document.

Placement supports grid, surface, edge, corner, and rotation snapping. These options, collision masks, snap distances, and collision padding are exposed at the top of the editor window.

## Automatic Door And Window Holes

Door and window definitions can enable a `HouseWallOpeningProfile`. When one is placed on a parametric `HouseGeometryKind.Wall`:

- the placed object receives a `HouseWallOpeningLink`;
- the wall stores an opening keyed by the placed object's stable ID;
- the wall rebuilds around all linked openings;
- moving the object updates the hole;
- deleting the object removes the hole;
- save/load restores both the opening and link.

Opening size, local center, and margin are data-driven on the placeable definition. This allows different door and window prefabs to create appropriately sized holes.

## Materials

Generated geometry reserves stable submesh slots matching `HouseFaceRole`:

- Interior / Exterior
- Top / Underside
- Left / Right
- Front / Back
- Trim / Default

`HouseBuilderMaterialController` stores stable material-definition IDs plus renderer path and material index. Assignments are included in JSON and reapplied from the active catalog on load.

## Visual Wiring

`HouseWireGraph` routes typed, event-driven signals between generic `HouseWireEndpoint` ports. It contains no switch, lamp, door, trap, or reinforcement-specific logic.

Supported signals are pulse, bool, float, string, and any. Ports support connection limits and multiple inputs/outputs. Default catalog definitions include stable port templates for representative switches, lamps, doors, buttons, traps, triggers, and reinforcements.

Use these generic adapters when integrating behavior:

- `HouseWireOutputRelay`: exposes methods such as `EmitPulse`, `EmitBool`, and `EmitFloat` for UnityEvents or gameplay code.
- `HouseWireInputRelay`: converts incoming signals into typed UnityEvents.
- `HouseWireTriggerOutput`: emits a pulse from a trigger collider.

For a new interactable, add port templates to its `HousePlaceableDefinition`, then connect its behavior to the relays. No graph changes are required.

## Geometry And Booleans

`HouseGeometryFactory` creates serializable runtime meshes for:

- cubes;
- walls, floors, and ceilings;
- doorway and window blocks;
- ramps;
- stairs.

Boolean operations use ProBuilder's CSG implementation in the editor. Results are baked into `HouseMeshData`, then wrapped as normal builder geometry with snapping, collision, materials, and save/load support. Inputs are left unchanged.

Parametric walls should be preferred when automatic door/window openings are needed. Baked Boolean meshes are intended for custom static forms.

## AI And Existing Systems

The default catalog reuses the project's existing:

- `NeighborSearchPoint`;
- `NeighborTaskLocation`;
- `ReinforcementTrigger`;
- reinforcement prefabs.

It also supplies `HouseNeighborSpawnPoint` and `HousePatrolPoint` marker prefabs. Scalar per-instance MonoBehaviour configuration is captured automatically in the JSON document. Components with specialized state or migration needs can implement `IHouseBuilderSerializable`.

## Save Format

The current format is:

- format ID: `neighbor.house-builder`
- version: `1`

Documents include transforms, stable IDs, definition/category IDs, geometry descriptors or baked mesh data, wall openings, material bindings, object properties, component state, and wire connections.

`HouseBuilderSaveSystem` rejects newer unsupported versions. Add explicit migrations before increasing `CurrentVersion`.

## Extending

To add a custom placeable category:

1. Create a `HouseBuilderCategoryDefinition` with a unique namespaced ID.
2. Create `HousePlaceableDefinition` assets using that category ID.
3. Add them to a catalog.

To add mod content, load or construct a separate catalog with namespaced IDs. Avoid reusing IDs from the default catalog.

For multiplayer, send authoritative operations that reference stable object/definition/port IDs, validate them on the host, and apply them through the same runtime services. Do not synchronize Unity object instance IDs.

## Verification

EditMode coverage is in `Assets/Main/Tests/Editor/HouseBuilderEditorTests.cs`. It covers geometry primitives, parametric wall holes, linked opening removal, typed wire routing, snapping, and JSON round trips.
