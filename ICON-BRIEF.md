# ThunderFeet: status-effect icon brief

**What it is:** ThunderFeet is a Valheim mod that makes you faster or slower depending on the ground under your feet. While you
walk, a small status-effect icon sits in the top-right of the HUD, next to the game's own buffs like Rested or Wet. The exact
surface's name shows under the icon ("Paved road", "Black marble"), and the speed change shows on it ("+15%").

**What we need:** six icons. Each one covers a family of surfaces.

## Format

- PNG, **128 × 128 px**, transparent background, 32-bit RGBA.
- It must still read at **32 × 32 px**. That's its real size on screen. Use one bold silhouette and no fine detail.
- Keep the subject inside a 112 px safe area. The game prints text over the bottom-right corner, so keep that corner quiet.
- **File names must match exactly** (case-sensitive). They're listed below.

## Style

- Match Valheim's own HUD icons: hand-painted, slightly desaturated, earthy Norse palette, soft painted shading, a thin dark outline
  so the icon reads on any background. No glossy or vector-flat look, no text, and no frame or border (the game draws the slot).
- **Make the set cohesive:** same lighting (top-left), same outline weight, same viewing angle on all six. The motif is a **boot print
  pressed into or standing on the material**. That way every icon says "ground under your feet", and the material tells them apart.
- Use original art in that style. Don't trace or copy the game's existing icons.

## The six icons

| File | Covers | What to show |
|---|---|---|
| `Road.png` | Paved road | Fitted grey flagstones with brown earth in the seams: a laid road. **It must not look like `Stone.png`.** |
| `Stone.png` | Built stone, black marble, grausten, iron floors, boulders, cliffs, dungeon floors | Dressed square masonry blocks with sharp, tight joints. |
| `Wood.png` | Wood floors, core wood, timberwood, ship decks, logs | Nailed wooden planks with visible grain. |
| `Earth.png` | Dirt paths, farmland, swamp mud, sand, ash, lava, tar, anything else | Packed brown earth with a deep boot print. |
| `Grass.png` | Meadows, Black Forest and Plains ground | A green grass tuft, maybe a dandelion. |
| `Frost.png` | Snow, deep snow, ice, wading water | Crisp white snow with a pale-blue boot print and a hint of ice. |

**Road vs. Stone is the one pair players must tell apart at a glance:**
- Road: irregular flagstones, warm brown seams, a worn, walked-on look.
- Stone: regular cut blocks, cool grey, tight joints.

**Delivery:** a zip of the six PNGs. They go in the mod's `icons/` folder.
