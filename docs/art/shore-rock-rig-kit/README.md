# Shore & Rock Rig — handoff

The PEI coast in two halves that share one camera, one PPU and one red-sandstone ramp:
**ShoreIso2** owns the ground (tiles, fringes, cliffs, dunes), **RockIso** owns every stone
standing on it. Neither bakes a single water pixel — the shader owns the sea, the depth-0 contour,
foam, spray and tide-pool fill (ADR-0010 / 0012 / 0023).

Four files are the kit. Everything else in here is output.

    shoreIsoKitRig2.js   shoreline tiles      globalThis.ShoreIso2
    rockIsoRig.js        rock props           globalThis.RockIso
    _shoreBake.js        shoreline batch harness (needs _shoreHeroScene.js)
    _rockBake.js         rock batch harness

Plain JS, no imports, no build step. Both run in the browser and headless in a bake sandbox.
Handoff pages in-project: `Shoreline Kit.dc.html`, `Rock Iso.dc.html`.

## Contract — shared by both rigs

- **PPU 32** — 32 px = 1 m.
- **Camera** — ADR-0006/0022: ¾ from the south at 40°, orthographic. Height ×0.766, ground depth
  ×0.643, depth key along the view axis. Same as the boat and tree bakes.
- **Render law** — solid bands, Bayer dither at band edges ONLY, no AA, binary alpha, 1 px
  `#171009` keyline.
- **Pivots** — shoreline: the 32 × 32 tile. Rock: **bottom-centre ground contact** (the tile the
  rock stands on), not the bbox centre — read `anchors.pivot` / `anchors.footprint.ground`.
- **Ramp** — RockIso `sandstone` is ShoreIso2's `redrock` ramp verbatim, so a rock composites
  onto a cliff toe with no seam.

## Shoreline — ShoreIso2

    <script src="shoreIsoKitRig2.js"></script>

    ShoreIso2.ground('sand', { gx, gy, seed, style })          // opaque 32×32, seamless at any (gx,gy)
    ShoreIso2.fringe('grass', 'edN', { gx, gy, seed, style })  // stamp over the lower material
    ShoreIso2.cliff('faceS', { band:'mid', gx, gy, seed, feature:'cave', style })
    ShoreIso2.column('cornSE', 5, { seed, style })             // cap + mid×3 + toe
    ShoreIso2.dune('faceS', { band:'toe', gx, seed, style })
    ShoreIso2.contact('n', { style })                          // seat a landform on its ground tile

Two styles ship side by side. **Geometry, grid, piece names, pivots and API are identical** — a map
authored against one drops straight onto the other; only the shading law and the ramps differ.

| | `nat` — naturalist | `gfx` — graphic |
|---|---|---|
| ramps | 8-step rock, 7 sand, 8 grass | 6-step rock, 5 sand, 6 grass |
| band edges | Bayer 4×4, transition zone only | hard — no dither |
| grain | full | 0.12 (detail lives in clusters) |
| silhouettes | soft | unified near-black keyline |
| reads as | soft, atmospheric | punchy at gameplay zoom over a busy sea |

    shore/nat/ · shore/gfx/
      Ground.png    128×192  6 materials × 4 adjacent world tiles (seams invisible)
      Fringe.png    384×96   grass / marram / sand × 12 autotile pieces
      Cliff.png     320×96   cap / mid / toe × 9 pieces + cave toe
      Dune.png      288×64   cap / toe × 9 pieces
      Contact.png   160×32   n · ne · e · nw · w occlusion overlays
      ShorelineIso.json      the contract

Autotiles on all four sides: the higher material laps onto the lower one wherever they touch, so
terrain seams follow the coast instead of the grid. Occlusion is baked — cap lips carry an AO band,
the talus heap a contact shade, and every landform has a matching `Contact.png` overlay.

## Rock — RockIso

    <script src="rockIsoRig.js"></script>

    RockIso.render(species, { variant, dress, stone }, tide)   // or a params object
      -> { w, h, rgba, anchors, params, topM }
    RockIso.RAMPS[stone][tide]

Not drawings — a tiny iso renderer. Each species builds superellipsoid **lumps**; every lump
rasterises its top surface and drops a column to the ground, so silhouette, self-occlusion and the
near flank fall out of the geometry. Bedding strata are a function of world Z, so beds **wrap** the
volume like real sedimentary rock. Cracks are cell-noise on (worldX, worldZ).

    Erratic     52×44   4 variants   single shore boulder
    Outcrop     88×60   3            2–5 boulder cluster, shoreline edge dressing
    PoolLedge   80×48   3            wave-cut plate with a tide-pool basin
    Skerry     104×52   4            awash hazard rock, clipped at the sea plane
    Cloven      60×76   3            split landmark, chart-mark scale
    Cobble      52×32   3            beach cobbles & shingle, tiny filler

Axes: **stone** `sandstone · granite · basalt · quartzite` (colour AND structure — bedding,
angularity and grain are reweighted, so they read apart in silhouette, not just hue) ×
**tide** `dry · wet · awash` × **dress** `bare · barnacled · weeded` (real pixels, sheet rows).
`waterline` is a **clip** at the sea plane plus a wet-glint band, never baked water.

**Anchors ship as data**, measured off the built volume, per variant, in `RockIso.json`:

    footprint   collision ellipse (rx, ry in m) + ground contact px
    perch       highest standable point — flat:false ⇒ decorative, do not spawn on it
    snags       2–3 outer silhouette catches for rope & pot lines, ≥60° apart
    hazard      awash danger radius in m (waterline > 0 only)
    pool        tide-pool basin rect + depth (PoolLedge) — the shader's fill target
    weedLine    the tide mark: screen row + height in m where rockweed drapes

**Nothing rock-side is pre-baked.** The rig IS the deliverable — 6 species × 4 stones × 3 tides ×
3 dress is 72 sheets of output, and every one of them regenerates from the seeds in `SPECIES`.
Bake the pairs the game actually uses, when it uses them:

    <Species>_<stone>_<dry|wet|awash>.png   variant COLS × 3 dress ROWS, cell = species cell

What DOES ship is the sidecar: **`RockIso.json`** at the kit root — camera and render constants,
per-species cell + sheet layout, and every variant's params, height and anchors. Anchors are stone-
and tide-independent (the geometry is seed-stable), so one entry serves all 12 bakes of a variant:
the engine can be wired against the contract before a single PNG exists.

## Batch either set

    (0,eval)(await readFile('rockIsoRig.js'));
    (0,eval)(await readFile('_rockBake.js'));
    await ROCK_BAKE({ createCanvas, saveFile, log, dir:'rock',
      stones:['sandstone'], tides:['dry','wet','awash'] });   // + preview:'…png' for the lineup

`ROCK_BAKE` writes one sheet per species × stone × tide plus `RockIso.json`, and asserts every
sheet ≤ 2048 px. Omit `stones` / `tides` for the full 72.

    (0,eval)(await readFile('shoreIsoKitRig2.js'));
    (0,eval)(await readFile('_shoreHeroScene.js'));
    (0,eval)(await readFile('_shoreBake.js'));
    await SHORE_BAKE({ K: ShoreIso2, RockIso, dory, createCanvas, saveFile });

The shoreline sheets in `shore/` are committed because the tile grid is fixed. Rock output is not —
it is a scatter of props with 144 legal combinations, so it bakes to order.

## Previews

    _preview-shore-nat.png · _preview-shore-gfx.png   26×15-tile hero scene, both styles
    _preview-rock.png                                 every rock variant on one tide line (doc only)

    RockIso.json   the rock contract — 20 variants, anchors included, no pixels

## Decide before integration

- **Atlas budget.** Which stone × tide pairs ship. `sandstone` is the shore-matching one, the other
  three are regional. Anchors are stone-independent, so a shader tint may cover some of the rest.
- **Pool fill.** PoolLedge bakes an empty damp basin and ships the rect. Confirm the engine fills
  from the rect.
- **Perch flag.** Cobble and most Skerry builds report `perch.flat = false`. The gull / fox
  spawner must honour the flag, not the point.
- **N-facing cliff lips** reuse the plateau grass tile (occluded at this camera). Diagonals are 45°
  only.
- Overlay dressing — driftwood, fences, boardwalk, spruce — still comes from `ShoreOverlays.png`
  and the Wildflowers / Seaweed / Finds kits. They composite fine on this ground.
