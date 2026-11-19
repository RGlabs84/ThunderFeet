# ThunderFeet

A Valheim 1.0.21 / BepInEx 5 mod. Your sprint and jog/walk speed change with the ground under your feet. A HUD status-effect icon
shows the surface you're on and the speed change.

## Surfaces (31)

- **Painted terrain** (the paint mask wins over the biome): Paved road, Dirt path, Cultivated soil.
- **Natural terrain:** Meadows grass, Black Forest floor, Plains grass, Swamp mud, Snow, Deep snow, Mistlands ground, Ashlands ground,
  Lava, Seabed, Rock slope (unpainted terrain steeper than `Rock Slope Angle`).
- **Built pieces** (by the piece's material): Wood, Core wood, Timberwood, Stone, Black marble, Grausten, Ancient, Iron, Ice.
- **Other:** Ship deck, Natural rock, Log, Ice, Dungeon floor, Shallow water, Tar, Other.

The **paved road** (hoe paint on terrain) and **built stone** (stone floor pieces) are detected by different code paths and have
separate settings.

## Config (`BepInEx/config/wubarrk.thunderfeet.cfg`)

- Each surface has its own section with `Sprint Speed %` and `Jog Walk Speed %`. 100 is vanilla, 115 is 1.15× (+15%), 80 is 0.8×.
  The allowed range is 10 to 300.
- `00 General`: `Lock Configuration`, `Enabled`, `Rock Slope Angle`, `Paint Threshold`, `Surface Change Delay` (how long new ground
  must be read steadily before the speed and icon switch; 0.35 s).
- `00 Display (local)`: `Show Surface Icon` (Always / OnlyWhenSpeedChanges / Never), `Icon Shows Percent`, `Log Surface Changes`.

### Server authority (ServerSync)

- Every setting except `00 Display (local)` belongs to the server. Joined players run on the server's values, and their own file
  comes back when they disconnect.
- With `Lock Configuration` on (the default), only the server and admins in `adminlist.txt` can change them. An admin can change
  them in game with Configuration Manager.
- The server's cfg is watched. Edit it on disk and every player gets the new values within a second.
- A client with a different ThunderFeet version can't join a server that runs it. Players without the mod can still join and run at
  vanilla speed.

## Icons

There are six icons, one per family of surfaces: `Road`, `Stone`, `Wood`, `Earth`, `Grass` and `Frost`. Put them in
`BepInEx/plugins/ThunderFeet/icons/` as `<Name>.png`. The exact surface's name still shows under the icon.

Until the art exists, each surface uses a fitting vanilla icon (hoe road, cultivator, wood, stone, ...). If none is found, it uses a
generated coloured disc. Which surface uses which icon is set in `SurfaceInfo.GroupOf` (`Surface.cs`). The art brief is in
`ICON-BRIEF.md`.

## Rune footsteps

Every footstep gives off a small burst of glowing runes, coloured by the icon family:

| Family | Colour |
|---|---|
| Road | Gold |
| Stone | Pale blue |
| Wood | Amber |
| Earth | Ember |
| Grass | Green |
| Frost | Ice |

- Ground that makes you faster glows brighter, and slower ground glows dimmer.
- A landing gives a bigger ring burst.
- Sprinting gives more runes per step than walking.
- There are no runes in water or tar.

How it works, following the rune effects in Wings of the Valkyrie and Njord:

- One world-space particle system, emitted by hand only, so standing still costs nothing.
- A procedural 16-rune atlas and an additive particle material found at runtime. Nothing extra ships with the mod.
- It hooks `FootStep.RPC_Step`, the step RPC every client already receives, so everyone sees everyone's runes with no extra network
  traffic.

Settings:

- `00 General / Rune Footsteps Allowed` (server) can turn the runes off for everybody.
- `00 Display (local) / Rune Footsteps` is each player's own: Everyone, OnlyMine or Off.
- `00 Display (local) / Rune Intensity` is each player's own, from 0.25 to 3.

## Autotest (in game, unattended)

```bash
~/valheim-testbed/heavy.sh 4G thunderfeet-autotest dotnet build tools/thunderfeet-autotest/ThunderFeetAutoTest.csproj -c Release
~/valheim-testbed/heavy.sh 10G thunderfeet-client tools/thunderfeet-autotest/run-autotest.sh
TF_AUTOTEST_PROFILE="$HOME/.local/share/com.kesomannen.gale/valheim/profiles/<profile>/BepInEx" \
  ~/valheim-testbed/heavy.sh 10G thunderfeet-client tools/thunderfeet-autotest/run-autotest.sh   # beside a player's whole mod list
```

The Steam client starts in a throwaway HOME and profile (your saves and prefs are never touched), one client at a time, about two
minutes. Phases:

- **A:** the plugin loads and its patches are in place.
- **S:** jog and sprint with ThunderFeet off, then every ground at 200% and at 50%. Each run reads both the game's computed
  speed and the measured body speed.
- **F:** flicker while sprinting over natural ground. Every raw change is logged with its collider, slope and paint in
  `flicker.log`.
- **P:** the loaded percentages on a 28 m stone-floor strip, against vanilla.
- **D:** detection on a wood floor, a stone floor and a hoe-paved road.

It ends with `VERDICT: PASS / FAIL / NOT ASSESSED`, and results are in `~/valheim-testbed/autotest-thunderfeet/latest/`.

## Build and release

```bash
dotnet build -c Release                 # builds against ../libs-Tools; stages the DLL and icons/*.png in HexiumDist/plugins/ThunderFeet/
python3 fold-package.py --check-badges  # forges the store README, checks every badge, validates, folds HexiumDist/ThunderFeet-v<ver>.zip
```

- **Store page:** the store README is **forged**. Edit `README.src.md` (README schema v3, `../libs-Tools/README-SCHEMA-v3.md`), never
  `HexiumDist/README.md`. The Ledger's numbers come from `Surface.cs` through `readme_gen.py`, and every setting named on the page is
  checked against the code.
- **One zip, both stores:** a release is a single zip uploaded unchanged to Hexium and to Thunderstore, under the **Wubarrk** namespace.
  `website_url` is `https://ravenirongames.com/`.
- **Versions:** a folded zip is never rebuilt (`--force` only before it is uploaded). To change anything, bump `<Version>` in the csproj,
  `Plugin.Version`, the manifest and the CHANGELOG together.

## How the speed is applied

The mod scales `m_speed`, `m_walkSpeed`, `m_crouchSpeed` and `m_runSpeed` only for the length of `Character.UpdateWalking` on the
local player, then restores them in a Harmony finalizer. Turning, swimming and other mods' status-effect speed bonuses are untouched
and stack as normal.
