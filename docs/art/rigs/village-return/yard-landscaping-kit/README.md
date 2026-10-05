# Hidden Harbours — Yard & Landscaping ISO Rig (the dooryard)

Everything that goes **between the house and the road**: the fence, the hedge, the bed, the barrel
planter, the clothesline, the woodpile, the buoy post and the trap bench. The house rig gives the
building, the road rig gives the street, the tree / shrub / flower rigs give the wild plants. This one
gives the stuff a townsperson puts on their own lawn — which is what makes one house on a street read
as lived-in and the next as let-go.

**62 pieces across 10 families**, each one parametric geometry baked through the
**shared ¾ camera** (45° steps, elev 40°, upper-LEFT key, z-buffered, ordered dither, per-face uv
texture, depth-edge darkening, 1 px keyline, no AA) at **32 px = 1 m** — the same projection as
`houseIsoRig` / `utilityIsoRig` / `wharfDecorRig` / the fleet. No PNGs ship in this kit on purpose:
every facing, season and upkeep is generated from the geometry on call, so the rig **is** the asset.

    yardIsoRig.js                        the rig (plain script, no imports, no build step)
    gameplay/yardIsoRig.gameplay.json    placement sidecar, hash-stamped to the rig
    harness.html                         bake sheets, regenerate + verify the sidecar
    SHA256SUMS.txt                       rig + sidecar hashes

Viewer (in-project): `Yard & Landscaping Iso.dc.html` — turntable, catalogue, upkeep/calendar/material
panel, three dooryards on one street at three upkeeps, per-piece and per-sheet downloads.

## Use the rig

    <script src="yardIsoRig.js"></script>

    YardIso.render(name, dir, {                 // dir 0..7 = N NE E SE S SW W NW
      elev: 40, variant: 0, len: 0.4,          // len grows a run — see the table below
      kept: 0.62, phase: 'green', snow: null,  // snow defaults off the phase
      paint: 'white', planted: true,
      weather: 0.4, night: false, compose: true
    })                                          // -> Uint8ClampedArray, 470 x 540 px RGBA

    YardIso.list() · byCat() · PROPS · CATS · CAT_LABEL
    YardIso.footprint(name, opts) -> { w, d }        true metres, len applied
    YardIso.height(name, opts)    -> metres
    YardIso.mounts(name, opts)    -> { ground, ends, wall, foot }   metres-space attach points
    YardIso.anchors(name, dir, opts) -> the same points in CELL PIXELS for the compositor
    YardIso.project(dir, [x,y,z], elev) -> { x, y }
    YardIso.PHASES · LEAFING · SNOW_BY_PHASE · BODY · FOL · BLOOM · KEY

## Two axes this rig owns that the other prop rigs don't

**KEPT** — 0..1 upkeep, and it is not a paint fade. One number moves seven things at once, because
that is what kept actually looks like:

| kept | what moves |
|---|---|
| paint | chalks and peels; at 0 whole pickets are gone |
| posts | lean off plumb, lines sag |
| tools | left where they were last used |
| bases | a weed skirt at every foot, density 1 − kept |
| beds | mulch gives way to grass |
| hedges | clip roughness is a direct function of (1 − kept) |
| clumps | foliage clump size variance and jitter scale with (1 − kept) |

A row of houses at 0.15 / 0.55 / 0.95 reads as three different families, not three colour grades.

**PHASE** — the `shrubIsoRig` 8-station calendar, verbatim (dormant · catkin · bloom · leaf · green · fruit · turn · bare),
so a hedge, a bed and a wild alder behind the fence can never disagree about what month it is.
Foliage fullness, colour, bloom and berry all derive from one table; `snow` defaults off the phase and
caps horizontal surfaces — a fence rail, a hedge top, a bench seat, a woodpile. The lawn dries toward
straw on the same clock, so a dooryard in October cannot be July green.

## Foliage — clumps, not columns (pass 2)

Hedges, shrub mounds, bed mounds and planter spill are built from **lobed ellipsoid clumps**, the
`treeIsoRig` foliage unit rebuilt in face space: radius carries two low harmonics plus an
under-weighted tooth term, so a rim scallops into leaf ends instead of closing to a billiard ball.
A mass is a **shell** — one jittered clump per cell of the top surface, then brick-bonded courses
walked round the plan silhouette by arc length, every other course stepped half a clump along, plus a
dark interior fill so nothing is see-through. Pass 1 extruded a field of vertical columns and read as
corduroy however it was toned.

Rules carried over from the tree rigs:

- **Clump radius does not scale with the mass.** A bigger hedge carries MORE clumps, never fatter ones.
- **Silhouette taper is per piece.** A cedar screen batters wider at the foot; a shrub narrows to its legs.
- **Leaf-off is a real loss.** `dormant` carries no foliage mass at all — twig cage and snow drift are
  the silhouette — and `bare` / `catkin` keep a few clumps at reduced radius.

## Families

| key | family | pieces | notes |
|---|---|---|---|
| `fence` | Fences & gates | 10 | picket, post-and-rail, split rail, page wire, snow fence, lattice, fieldstone, gate pillar, corner |
| `hedge` | Hedges & shrubs | 8 | clipped runs and corners, balls, cedar screen, lilac, rugosa, hydrangea, trellis vine |
| `bed` | Beds & gardens | 9 | foundation / island / border beds, vegetable rows, potato drills, rhubarb, ferns, hostas, raspberry canes |
| `planter` | Planters | 5 | half barrel, window box, clay pots, tyre planter, hanging basket |
| `fixture` | Dooryard fixtures | 7 | mailbox, clothesline, woodpile, rain barrel, hose and tap, compost, oil tank |
| `sitting` | Sitting & gathering | 4 | garden bench, picnic table, Muskoka chairs, fire pit |
| `play` | Play & work | 5 | swing set, sandbox, doghouse, sawhorses, tools and post |
| `ornament` | Lawn ornaments | 6 | buoy post, dory planter, anchor and coil, trap bench, birdbath, birdhouse |
| `ground` | Paths & ground | 4 | stepping stones, gravel apron, mown edge, creeping cover |
| `sign` | Signs & poles | 4 | flagpole, name board, number post, roadside stand |

### The green ones

| piece | key | footprint @ len 0.4 | height | variants |
|---|---|---|---|---|
| Clipped hedge | `hedgeRun` | 2.36 × 0.62 | 0.85 | low · tall |
| Clipped ball | `hedgeBall` | 0.90 × 0.86 | 0.96 | deciduous · cedar |
| Cedar hedge | `cedarHedge` | 2.18 × 0.66 | 2.00 | screen |
| Dooryard lilac | `lilac` | 1.30 × 1.10 | 2.14 | bush |
| Rugosa rose | `roseBush` | 1.10 × 1.00 | 0.88 | mound |
| Hedge corner | `hedgeCorner` | 1.13 × 0.95 | 0.88 | low · tall |
| Hydrangea | `hydrangea` | 1.15 × 1.00 | 0.97 | blue · white |
| Trellis & vine | `trellisVine` | 1.24 × 0.16 | 1.82 | sweet pea · clematis |
| Fern clump | `fernBed` | 1.05 × 0.80 | 0.60 | shade |
| Hosta edging | `hostaEdge` | 1.60 × 0.50 | 0.70 | white spike · pink spike |
| Raspberry canes | `raspberryRow` | 1.88 × 0.60 | 1.25 | wired |
| Hanging basket | `hangingBasket` | 0.52 × 0.40 | 1.72 | crook |
| Creeping cover | `groundCover` | 1.40 × 0.85 | 0.14 | thyme · moss |

## Run lengths

29 pieces grow with `len`. Footprint is `foot[0] × (1 + len × runGain)`, depth fixed:

| piece | key | width, len 0 → 1 | depth | height |
|---|---|---|---|---|
| Picket fence | `picketPanel` | 1.70 → 3.30 | 0.10 | 1.06 |
| Post & rail | `postRail` | 2.00 → 3.80 | 0.14 | 1.14 |
| Split rail | `splitRail` | 2.20 → 4.00 | 0.60 | 1.05 |
| Page wire | `wireFence` | 2.20 → 4.20 | 0.10 | 1.16 |
| Snow fence | `snowFence` | 2.00 → 3.60 | 0.20 | 1.04 |
| Lattice skirt | `lattice` | 1.50 → 2.60 | 0.12 | 0.82 |
| Fieldstone wall | `stoneWall` | 1.80 → 3.40 | 0.46 | 0.72 |
| Clipped hedge | `hedgeRun` | 1.60 → 3.50 | 0.62 | 0.85 |
| Cedar hedge | `cedarHedge` | 1.50 → 3.19 | 0.66 | 1.80 → 2.30 |
| Hedge corner | `hedgeCorner` | 0.95 → 1.40 | 0.95 | 0.88 |
| Trellis & vine | `trellisVine` | 1.00 → 1.60 | 0.16 | 1.70 → 2.00 |
| Foundation bed | `foundationBed` | 1.60 → 2.99 | 0.62 | 0.50 |
| Island bed | `islandBed` | 1.10 → 2.00 | 0.80 | 0.60 |
| Border bed | `borderBed` | 1.80 → 3.40 | 0.45 | 0.45 |

The rest are in the sidecar, each with measured `size[]` at len 0 / 0.4 / 1.

## Contract

- **PPU 32** — 32 px = 1 m. Every dimension in the sidecar is true metres.
- **Camera** — 3/4 from the south, 45deg steps, elev 40deg default (30-50 accepted), orthographic — shared with houseIsoRig / utilityIsoRig / wharfDecorRig / the fleet
- **Origin** — ground-centre of the footprint, z = 0 = lawn plane. A piece drops on a lawn tile with no offset maths.
- **Axes** — +X along the run, +Y away from the house (wall-backed pieces put their back plane toward -Y), +Z up
- **Cell** — 470 x 540 px, pivot at 235,442 — one cell for every piece, so a swap at runtime is pivot-stable
- **Alpha** — binary, no AA, 1 px keyline, ordered dither, upper-LEFT key
- **Sheets** — Art/Sprites/Yard/<piece>.png — 8 cells of 470 x 540 (3760 x 540)
- **Wall-backed pieces** (window box, rain barrel, foundation bed) carry their back plane toward −Y, so
  the house stands behind them and the face reads.

## Composition

Beds compose, hedges are native. A planted bed asks `globalThis.Flowers` / `globalThis.Shrubs` for real
sprites and blits them back-to-front over its own soil (same PPU, same 40° elevation, so they land
true). If those rigs are not loaded the bed falls back to native foliage mounds and still reads;
`compose: false` forces the native path. A clipped hedge is a **mass** with a machined top, not a row of
shrubs — the two are drawn by different rules on purpose.

## The sidecar

`gameplay/yardIsoRig.gameplay.json`, schema `hidden-harbours/prop-placement-geometry@1`, stamped
`derivedFromRigSha256: d2ab2a906f4f099e…`.

**Exact, read from the rig:** label, family, variants, `runs`/`runGain`, defaults, `readsPlanted`,
`size[]` (footprint and height at len 0 / 0.4 / 1), `mounts` (ground / ends / wall). Nothing is
transcribed by hand — the file is generated by evaluating the rig source unmodified and interrogating
its public API.

**Ours, by rule, and open to correction:** `block.shape`, `block.axis`, `block.passable`. The rig
authors no collision volume, so these are read off the footprint and height per family: fences and
hedges are wall blockers along +X with thickness = `size.d`; round-plan pieces take a cylinder of
radius `size.w / 2`; beds are passable plots; ground pieces are surface overlays that never block.
`reading._confirm` says so in the file. Demote any of them by ruling and we re-emit.

### Verify before trusting it

    sha256 yardIsoRig.js   ->  d2ab2a906f4f099e3054643034a936327ae547fc5ea375b3198cc8f503d23706
                               must equal derivedFromRigSha256 in the sidecar
    sha256 gameplay/yardIsoRig.gameplay.json
                           ->  89136c3a18b71cb2c2ccfcc182c52fd9c88df0fefa29c7de8b5c0701dc681fb5

`harness.html` → **VERIFY HASH** does both against the files beside it. If the rig hash has moved, a
piece was reshaped: regenerate with **SIDECAR JSON** rather than editing the file. The harness refuses
to write an unstamped sidecar — an unstamped sidecar is the defect (`Art/_sidecarExport.js`, D1).

## Bake

`harness.html` runs from the folder with no server, and needs nothing but `yardIsoRig.js`:

- **8-DIR SHEET** — the selected piece, 8 cells of 470 x 540 (3760 x 540), pivot identical in every cell.
- **CONTACT SHEET** — all 62 pieces, SE facing, cropped to content: the review sheet.
- **FAMILY SHEETS** — one PNG per family at the current kept / phase.
- **SIDECAR JSON** — regenerates this sidecar, hash stamped from the bytes fetched at that moment.

Sweep `kept` and `phase` before shipping any sheet: they are the two axes a consumer will drive hardest.
