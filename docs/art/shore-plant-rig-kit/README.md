# Shoreline Plant Rig — handoff

One file. Everything else is output.

    Art/shorePlantRig.js   the rig (plain JS, no imports, no build step)

Handoff page (in-project): `Shoreline Plant Iso.dc.html` — species-by-zone picker, continuous tide
slider with the five bake steps, live channel switch, four-rule audit, the shore staircase, the ebb
sequence, per-species sheet gate, and per-sprite / per-sheet / contract downloads.

16 species × 5 zones × 5 tide steps × 4 variants × 3 seasons × 3 growth stages = 2880 sprites.

## Use the rig

    <script src="shorePlantRig.js"></script>

    ShorePlants.render(key, { variant, season, frame, tide, stage })   // or { size: 0.2-1.3 }

`tide` is 0–1 of the nominal harbour range (0 = mean low water, 1 = mean high water). Returns, per
pixel:

    rgba                  composited sprite, binary alpha, no AA
    masks.front           N·L from the fixed upper-left key — ALL materials
    masks.rim             back rim — body and wood only, identically 0 on strap
    masks.depth           surface depth, normalised per sprite
    masks.wet             wetness 0-255
    masks.sub             255 below the baked waterline
    nx, ny, nz            view-space surface normals (Float32Array)
    alpha, dist, thick, mat
    pivot {x, y}          ground contact — the holdfast / root crown
    waterRow              row where the surface crosses, or null if outside the sprite
    submerged             which side of the sprite the surface is on, when waterRow is null
    tide                  the resolved tide state: {waterM, overM, sub, wet, lay, mode, still}
    report                measured rule compliance for this sprite

    ShorePlants.packMask(res)    -> LIGHT RGBA: R key, G rim, B depth, A coverage
    ShorePlants.packState(res)   -> TIDE  RGBA: R wet, G submerged, B strap flag, A coverage
    ShorePlants.normalView(res)  -> RGBA normal map
    ShorePlants.massView(res)    -> rule-1 audit view (body / core / strap / fleck / too-thin)
    ShorePlants.tideView(res)    -> submerged / wet / dry view
    ShorePlants.sheetSpec(key, size, 'tide'|undefined) -> cell, pivot, sheet dims, 2048 check
    ShorePlants.cellOf(sp, size) -> measured union cell for a species at a size
    ShorePlants.tideOf(t, sp)    -> the resolved tide state, without rendering
    ShorePlants.sheetAudit(size)  -> per-species cell / worst ink / sheet dims / cap headroom / MB
    ShorePlants.contract(size)    -> the machine-readable import contract (see below)

Channels are computed from the geometry on every call. Change a species row in `SPECIES` and every
channel changes with it — nothing here is a stored bake.

## The tide is an axis, not a variant

Water height over a plant's own ground — `tideM − zone.base`, in metres — is resolved once in
`tideOf()` and drives four things, so they cannot disagree:

    lay      algae need water to stand. Drained, the same skeleton concertinas onto its substrate
             and falls downslope. It puddles; it does not stretch out flat.
    wet      soaked while any part is under, then dries off over DRY_M (0.85 m) of further fall.
             Glossy → matte → bleached or near-black. Upland plants are never wet, with no
             special case, because the tide never reaches their ground.
    water    everything below the waterline takes the cold water ramp and loses contrast with
             depth. COLOUR ONLY — see below.
    sway     breeze above the waterline, phase-lagged surge below, and NOTHING when drained and
             limp. That stillness is most of why a low tide reads as low.

Zones, metres above chart datum: subtidal fringe 0.15 · mid intertidal 1.55 · low marsh 2.55 ·
high marsh 3.35 · upland 4.65. Nominal range `TIDE_M` = 4.0 m. One constant sets the staircase.

## No baked water, no baked moving light

Submergence bakes **colour only** — darker, cooler, contrast falling monotonically with depth. There
is no dapple, no spatial pattern and nothing per-frame in the water response. The live sprite-light
shader's caustics are swell-driven, and a baked dapple would put two uncorrelated patterns on the
same frond. Assert it: `contract().water.bakesCaustics === false`.

`waterRow` is reported so a scene can line its water plane up with the bake. It is `null` when the
surface is outside the sprite in either direction — read `submerged` to tell which side.

## The four rules, enforced in code

1. **Mass floor, with a declared exemption.** A 2 px rim must leave 6 px of interior, so no BODY
   mass leaves the emitter under a 5 px radius in any axis — clamped there rather than trusted to
   sixteen author sites (`report.floored` counts what was raised). But a grass blade IS 2 px wide,
   so blades, culms, fronds and sheets are declared **STRAP**: exempt from the floor, and in
   exchange forbidden a rim. WOOD and FLECK are linear too. The exemption is a material, not a
   fudge, which is what makes it auditable.
2. **Silhouette, strap-aware.** Blades are spaced by arc length around the base, and the de-speckle
   pass runs on body masses ONLY. The tree rig's pass kills any pixel with one neighbour — run that
   here and it eats every blade tip in the family, because a 1-px tip is authored here and noise
   there. `report.spared` counts the tips it declined to remove.
3. **Rim gate.** `rim × smoothstep(local mass thickness)`, and hard zero on strap and fleck. This is
   where the rule-1 exemption is actually enforced. `report.strapRimLeak` must be 0.
4. **Sub-pixel detail is a decision.** At 32 px/m an Ascophyllum bladder is 0.6 px and a bayberry
   wax berry is 0.1 px. Nothing sub-pixel is promoted to a mass: a bladder is a width bulge on its
   strap, a glasswort joint is a knot on a strap, wax berries are single FLECK pixels.
   `report.promoted` names what happened per species.

All 720 sprite states at full stage pass all four.

## Cells stay unioned

ONE cell and ONE ground-contact pivot per species per growth stage, unioned over 4 variants ×
2 seasons × 5 tides. The tide axis is continuous at runtime, so plants swap sprite states constantly
as water rises over their ground; with one pivot those swaps are anchored by construction. Per-tide
cells would give every state its own pivot, and a 1 px disagreement becomes a visible hop the moment
the tide crosses a threshold. Sheet waste is only memory; a state hop is an artifact.

The cost is measured, not assumed — `sheetAudit()` reports per species: union cell, worst
ink-to-cell ratio across the tide states (with the state that causes it), both sheet sizes, headroom
to the 2048 cap, and KB. At full stage: **all 32 sheets fit, smallest headroom 1338 px, 11.42 MB RGBA
uncompressed**, worst ink Sea Lettuce at 9% (265 KB — not worth an exception). If a species ever
busts 2048 or the budget gets ugly, flip **that species** to per-tide cells as a targeted exception,
decided on these numbers.

## Contract

`ShorePlants.contract(size)` serialises the import contract straight out of the live rig, so it
cannot drift from the bake. The page has a download button for it. It carries the projection, the
four materials with their floor/rim rules, both bakes' per-channel semantics, the water law, the
tide model and input mapping, the zone table, per-species sheet numbers, and five importer asserts.

The one thing a consuming shader must be told rather than guess:

- `light.R` is key light for **all** materials — a strap's lit spine is real N·L off its bowed
  cross-section normal, so it arrives here with everything else.
- `light.G` is the back rim for **body and wood only**, and is identically 0 on strap and fleck. It
  is *not* re-used as a spine channel; no channel changes meaning per material.
- `state.B` is 255 on strap pixels — the no-rim flag, and the branch. Gate every read of `light.G`
  on it. Never infer strap-vs-mass from the sprite.

Other constants:

- **PPU 32** — 32 px = 1 m. `h` in the species table is TRUE standing world height.
- **Camera** — ADR-0006/0022: ¾ from the south at 40°, orthographic. Height ×0.766, ground depth
  ×0.643. Same as the boat, rock, shoreline and tree bakes.
- **Pivot** — ground contact. Read it from `sheetSpec().pivot`; the drape pad projects below it, so
  it is not the cell's bottom row.
- **Sway** — 4 frames, pinned at the pivot row. Masks shear with the sprite, frame for frame. Play
  5–7 fps.
- **Sheets** — two axes per species: `variant × sway` and `tide × sway`. Both asserted ≤ 2048 px.

Palette: cold ambient bounce #1d3b4a + one warm key #e8b06a, as everywhere else in the world.
Wetness adds a hard specular dot at the top of the ramp, held to pixels with body behind them so it
never fringes an edge.

## Species

| species | latin | zone | height | unit | limp |
|---|---|---|---|---|---|
| Sugar Kelp | Saccharina latissima | fringe | 2.75 m | plant | 0.92 |
| Irish Moss | Chondrus crispus | fringe | 0.53 m | mat | 0.50 |
| Eelgrass | Zostera marina | fringe | 1.44 m | clump | 0.62 |
| Knotted Wrack | Ascophyllum nodosum | mid | 1.63 m | plant | 0.84 |
| Bladderwrack | Fucus vesiculosus | mid | 1.06 m | plant | 0.80 |
| Sea Lettuce | Ulva lactuca | mid | 0.47 m | mat | 0.95 |
| Saltmarsh Cordgrass | Spartina alterniflora | low marsh | 1.81 m | clump | 0 |
| Glasswort | Salicornia maritima | low marsh | 0.47 m | mat | 0 |
| Saltmeadow Hay | Spartina patens | high marsh | 0.75 m | mat | 0 |
| Black Rush | Juncus gerardii | high marsh | 0.66 m | clump | 0 |
| Cattail | Typha latifolia | high marsh | 2.06 m | clump | 0 |
| Threesquare Bulrush | Schoenoplectus pungens | high marsh | 1.13 m | clump | 0 |
| Marram Grass | Ammophila breviligulata | upland | 1.03 m | clump | 0 |
| Bayberry | Morella pensylvanica | upland | 1.81 m | plant | 0 |
| Sweet Fern | Comptonia peregrina | upland | 1.16 m | plant | 0 |
| Beach Pea | Lathyrus japonicus | upland | 0.50 m | mat | 0 |

`unit` says what one sprite IS: **plant** = one individual · **clump** = a hummock of many stems ·
**mat** = one square metre of cover. Irish moss and sea lettuce are mats because a single frond is
3 px at this PPU — the honest unit, not a shrunk plant.

Seasonal behaviour: the marsh grasses and the deciduous uplanders take their `fall` colour in
autumn; saltmeadow hay lays over under winter ice (`winterFlat`); glasswort dies back to bleached
sticks (`winterBare`); algae ice-scour cooler rather than turning. Upland plants take snow on
up-facing normals.
