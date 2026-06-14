# House Builder Level Editor

`HouseBuilderLevelEditor` is a runtime-capable, data-driven house and level construction system with a Unity Editor front end.

Open it from:

`Tools > Neighbor > House Builder > Level Editor`

The default catalog, starter structure prefabs, paint materials, and marker prefabs are created automatically in `Assets/Main/HouseBuilder/Data` and `Assets/Main/HouseBuilder/Prefabs`. They can also be refreshed from:

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
3. Use **Place** and click an asset card. Move into Scene view and left-click to place copies. Ground placeables are automatically lifted from their functional root so their visible bounds rest on the surface. Click the selected green card again or press `Esc` to stop placement. Right-click or hold and drag right-click over matching placed objects to erase them. Use `Q`/`E` for yaw, `R`/`F` for pitch, and `Z`/`C` for roll.
4. Use **Draw** to create walls, floors, ceilings, doorway blocks, window blocks, ramps, stairs, and blocks. Click-drag to size the shape directly in Scene view.
5. Select a generated shape while the **Draw** tab is open to resize it with direct Scene handles or friendly dimension fields.
6. Use **Paint** to choose a material and click-drag over the actual faces in Scene view. Material cards and normal Unity Material assets can also be dragged directly onto a face.
7. Use **Connect** to click an orange output port followed by a cyan input port.
8. Use **Combine** to subtract, intersect, or union two mesh objects.
9. Use **Save** to write or load a versioned `.house.json` document.

Placement supports grid, surface, edge, corner, and rotation snapping. These options, collision masks, snap distances, and collision padding are under the collapsed **Setup & Snapping** section so everyday building controls remain uncluttered.

Ground-based assets such as starter structures, furniture, props, and AI markers can be placed on the snapped build grid when no collider surface is available. Wall- and ceiling-mounted assets still require the appropriate real surface.

### Reinforcement Trigger Workflow

Placing a `Reinforcement Trigger` immediately opens a reinforcement picker. Check every reinforcement that the trigger may spawn, then choose **Place Locations**. The Scene view switches directly into repeatable reinforcement-location placement:

- the first selected reinforcement is shown as the placement ghost and as a cyan preview on every saved location;
- each location stores the complete checked reinforcement set and a stable link to its trigger;
- left-click keeps placing more locations;
- `Q`/`E` rotates the location;
- `Esc` finishes location placement.

At runtime, the existing `ReinforcementTrigger` gameplay system chooses an affordable configured reinforcement and spawns it at one of its linked locations. Save/load preserves trigger links, selected definitions, transforms, and previews. Erasing a trigger through Place mode also erases its linked locations.

### Starter Structures

The default catalog includes ready-to-place `Basic Wall`, `Basic Floor`, and `Basic Ceiling` prefabs under `Assets/Main/HouseBuilder/Prefabs/Structures`. Their visible meshes are persistent assets under `Assets/Main/HouseBuilder/Meshes/Structures`, so the prefabs are immediately visible in the Project and Prefab views. They use the same parametric geometry, face-painting, wall-opening, snapping, and serialization systems as shapes created with the Draw tool.

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

The Paint tool resolves the triangle under the Scene-view cursor to one of these roles, highlights it, and assigns the active brush to that face. For ordinary prefabs it stores the clicked renderer path and material slot instead.

`HouseBuilderMaterialController` stores stable material-definition IDs plus renderer path and material index. Assignments are included in JSON and reapplied from the active catalog on load. Dragging a normal Unity Material onto a face automatically creates a stable material definition and adds it to the active catalog.

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

Each primitive has a useful suggested size rather than sharing one generic dimension. Draw mode creates walls from a line and horizontal structures from a rectangle. Resizing changes the serialized geometry descriptor and preserves linked door/window openings.

Boolean operations use ProBuilder's CSG implementation in the editor. Results are baked into `HouseMeshData`, then wrapped as normal builder geometry with snapping, collision, materials, and save/load support. Inputs are left unchanged.

Parametric walls should be preferred when automatic door/window openings are needed. Baked Boolean meshes are intended for custom static forms.

## AI And Existing Systems

The default catalog reuses the project's existing:

- `NeighborSearchPoint`;
- `NeighborTaskLocation`;
- `ReinforcementTrigger`;
- reinforcement prefabs.

It also supplies `HouseNeighborSpawnPoint`, `HousePatrolPoint`, and linked `HouseReinforcementLocation` marker prefabs. Scalar per-instance MonoBehaviour configuration is captured automatically in the JSON document. Components with specialized state or migration needs can implement `IHouseBuilderSerializable`.

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

EditMode coverage is in `Assets/Main/Tests/Editor/HouseBuilderEditorTests.cs`. It covers geometry primitives and suggested dimensions, parametric wall holes, resize preservation, Scene face picking, linked opening removal, typed wire routing, snapping, and JSON round trips.
