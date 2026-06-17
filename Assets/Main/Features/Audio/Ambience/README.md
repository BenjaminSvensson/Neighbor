# Area Ambience

1. Create profiles with `Create > Neighbor > Audio > Ambience Profile`.
2. Add one layer for every sound that should play simultaneously. Add multiple clips to a layer when it should randomly choose one variation.
3. Add `AmbienceManager` once in the scene and optionally assign a default profile.
4. Add `AmbienceArea` and a collider to each area, then assign its profile. The collider is automatically made a trigger.
5. Increase an area's priority when it should override an overlapping area.

The manager follows the scene's `AudioListener` automatically and smoothly crossfades profiles using the incoming profile's transition duration.

Prefab starters live in `Prefabs/`:

- `AreaSpecificAmbienceArea` covers a standard room-sized area.
- `LargeAreaSpecificAmbienceArea` covers larger rooms or outdoor regions.

Drop a prefab into the scene, resize its trigger collider to match the space, and assign the area's `AmbienceProfile`. While the player listener is inside that collider, the assigned ambience profile plays.
