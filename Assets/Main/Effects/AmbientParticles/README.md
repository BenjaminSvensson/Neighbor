# Ambient Particle Library

Drop `Prefabs/AmbientWeatherRig.prefab` into an outdoor area for a combined
leaves, wind, dust, pollen, and mist setup. Its `AmbientParticleWind` component
controls the shared wind direction, base strength, and gusts.

Individual prefabs are also ready to use:

- `FallingLeaves`: broad outdoor leaf fall with tumbling motion.
- `WindStreaks`: subtle stretched streaks that make strong wind visible.
- `DustMotes`: warm, slow indoor dust particles.
- `Pollen`: brighter outdoor floating particles.
- `GroundMist`: low, slowly expanding fog patches.
- `ChimneySmoke`: rising smoke for chimneys, vents, or fires.

Move and scale each prefab to fit its area. All effects use world-space
simulation, so particles already emitted will not move when their emitter moves.

Use `Tools > Neighbor > Create or Refresh Ambient Particle Library` to rebuild
the procedural textures, materials, and prefabs after editing the generator.
