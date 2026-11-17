# Changelog

## 0.1.0

First release, for Valheim 1.0.21 (network version 41) and BepInEx 5.4.23. Put it on the server and on every client.

### Added
- **31 surfaces**, each with its own `Sprint Speed %` and `Jog Walk Speed %` (10 to 300, 100 = vanilla) in a section
  per surface (`01 Paved road` to `31 Other`).
  - Painted terrain (the Heightmap paint mask): Paved road, Dirt path, Cultivated soil. Paint wins over the biome.
  - Natural terrain: Meadows grass, Black Forest floor, Plains grass, Swamp mud, Snow, Deep snow, Mistlands ground,
    Ashlands ground, Lava, Seabed, and Rock slope (unpainted ground steeper than `Rock Slope Angle`, 35°).
  - Built pieces, by `WearNTear.MaterialType`: Wood, Core wood (HardWood), Timberwood, Stone, Black marble (Marble),
    Grausten (Ashstone), Ancient, Iron, Ice.
  - Other: Ship deck, Natural rock, Log, Ice, Dungeon floor, Shallow water, Tar, Other.
- **The paved road and built stone never mix.** The road is read from the terrain paint mask (blue channel); a stone
  floor from the piece's material. In the Deep North the green channel is deep snow, as in vanilla (0.12 = deep).
- **Speed applies on foot only.** `m_speed`, `m_walkSpeed`, `m_crouchSpeed` (jog / walk / sneak) and `m_runSpeed`
  (sprint) are scaled for the length of `Character.UpdateWalking` on the local player and restored in a Harmony
  finalizer. Turning, swimming, flying and status-effect speed mods are untouched and multiply as before. While
  airborne the last ground is kept, so a jump does not lose the road's speed.
- **HUD status effect** `SE_ThunderFeet` (display only, never saved): the surface name under the icon, the change on
  it (`+15%`, or `+15% / +10%`). Six icons in `plugins/ThunderFeet/icons/` (Road, Stone, Wood, Earth, Grass, Frost);
  a missing PNG falls back to a vanilla icon, then a tinted disc. `Show Surface Icon`: Always / OnlyWhenSpeedChanges /
  Never. `Icon Shows Percent`.
- **Rune footsteps**: a postfix on `FootStep.RPC_Step` emits runes on every client for every player's step (no new
  network traffic). 1 / 2 / 4 runes for walk / jog / sprint (by horizontal speed), a 10-rune ring on a Land-only step
  effect. Tinted by icon family, brightness by the surface's sprint multiplier. None under water level, in water or
  tar, or beyond 40 m from the camera. One world-space ParticleSystem (600 max), a procedural 16-rune atlas and an
  additive material from a built-in particle shader. A remote player's surface is read by a 1.5 m raycast under the
  step. `Rune Footsteps` (Everyone / OnlyMine / Off) and `Rune Intensity` (0.25 to 3) are local;
  `Rune Footsteps Allowed` is the server's.
- **ServerSync** (compiled in): every setting outside `00 Display (local)` is the server's; `Lock Configuration` (on)
  limits changes to the server and `adminlist.txt` admins. The server's cfg is watched and re-read 0.5 s after a write.
  `CurrentVersion` = `MinimumRequiredVersion` = 0.1.0; `ModRequired` off (players without the mod may join).
- `Log Surface Changes` writes each change of surface, with the position, to the BepInEx log.
