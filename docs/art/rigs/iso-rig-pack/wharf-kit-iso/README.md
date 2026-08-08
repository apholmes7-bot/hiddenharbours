# Hidden Harbours — Wharf ISO Rig

The working waterfront, rebuilt as a **rig**: one parametric 3D structure per family, baked through
the **shared ¾ camera** (45° steps, elev 40°, upper-LEFT key, z-buffered, ordered dither, per-face uv
texture, depth-edge darkening, no AA; the 1 px keyline is retired by default per ADR 0031 and
reachable with `{outline:true}`) at **32 px = 1 m** — the same projection as
`doryIsoRig` / `lobsterBoatIsoRig` / `houseIsoRig` / `wharfBuildingRig` / `characterIsoRig`.

This replaces the near-plan tile kit for structure. **The baked kit is untouched** —
`Wharf Kit.dc.html`, `Art/wharfKitRig.js`, `WharfAtlas.png` and `WharfOverlays.png` all stay where
they are; this rig sits beside them.

## Why

The old kit's deck cells were hand-painted in a near-plan 32×32 grid that never went through the
camera, so nothing on it was in scale with anything else in the game. Measured straight off
`WharfOverlays.json`:

| fitting | baked sprite | true size | short by |
|---|---|---|---|
| access ladder | 9 × 20 px = 0.28 × 0.63 m | 0.45 × 3.40 m | 5.4× |
| tyre fender | 11 × 14 px = 0.34 × 0.44 m | 1.00 m OD | 2.3× |
| cast bollard | 11 × 12 px = 0.34 × 0.37 m | 0.32 × 0.75 m | 2.0× |
| gangway | 16 × 40 px = 0.50 × 1.25 m | 0.95 × 6.00 m | 4.8× |
| `tallpier` face | 19 px = 0.59 m of drop | 1.6 m stated → 39 px projected | 2.7× |

Here a 0.45 m ladder is 14 px wide **because it is 0.45 m**. Nothing is drawn to fit a cell.

## Families

`quay` · `pier` · `crib` · `float` · `gangway` · `slipway` · `riprap`

- **quay** — mass-concrete face, no piles. Coping slab, timber rubbing strake, vertical fender piles,
  weep holes.
- **pier** — pile-driven timber wharf: piles → pile caps → stringers → planking, with transverse
  X-bracing and longitudinal walers that the falling tide uncovers.
- **crib** — stone-filled log crib, alternating header/stretcher courses, drift pins, rubble ballast
  under the deck. Atlantic Canada vernacular.
- **float** — rides the tide (`deckZ = tide + freeboard`), poly-shelled foam billets under the frame,
  galvanised hoops sliding on fixed guide piles, chain to a seabed block that straightens as the
  water drops. Rocks in place: roll 0.85°, pitch 0.55°, heave 22 mm, 8-frame loop, no translation.
- **gangway** — alloy ramp whose slope is **re-solved** from deck height and water level; side
  trusses, treads, plumb stanchions, top hinge plate, bottom rollers.
- **slipway** — ribbed concrete ramp descending past datum, sloped kerbs, timber cradle rails, algae
  film below mid tide.
- **riprap** — two-grade armour-stone revetment seated on a real slope plane, weed on the lower
  courses, optional crest beam so it can meet a deck.

## Styles, and the 17 presets

Every style axis defaults to the original look; the variants are additive.

| axis | family | options |
|---|---|---|
| `face` | quay | `concrete` · `steelSheet` (driven pans, rust, waler + tie-rod heads) · `timberSheet` (close-driven timber, pile heads proud) |
| `struct` | pier | `open` · `sheeted` (closed face under the deck) · `steelPile` (steel pipe piles) |
| `cap` | crib | `plank` · `concrete` (cast cap slab) |
| `hull` | float | `timber` (frame + foam billets) · `plastic` (modular HDPE cube raft, pin bosses, grid deck) |
| `stone` | riprap | `granite` (grey-blue) · `sandstone` (PEI red) |
| `mound` | riprap | `revetment` (one slope) · `breakwater` (mound both ways off a crest) · `sheetCell` (sheet-pile cell, rock-filled, capped) |
| `curb` | all decks | `wood` · `yellow` (the painted bull rail) · `none` |

`render(preset, dir, opts)` takes a preset name in place of a family. Presets include the four the
original near-plan kit shipped — `lowPier`, `tallPier`, `concreteQuay`, `timberFloat` — plus
`sheetedPier`, `steelPier`, `torbayQuay`, `timberQuay`, `logCrib`, `cappedCrib`, `plasticFloat`,
`floatSet`, `plasticSet`, `graniteEdge`, `redEdge`, `breakwater`, `sheetCell`.

Armour stone is a jittered polyhedron whose crown is a **fan of facets to a raised apex** — a broad
broken top, never a smooth cap — seated shallow on the slope so each block reads wider than tall.

## Berths decide the ladders

A ladder belongs to a berth. The working face is packed with the best **mix** of hulls (utilisation
first, then largest hull served), from a fleet table spanning a 4.9 m dory to an 18 m dragger:

| face | berths | utilisation |
|---|---|---|
| 11.2 m | console skiff | 0.71 |
| 24.0 m | coastal packet + console skiff | 0.99 |
| 32.4 m | side dragger + dory + dory | 0.99 |

Each berth gets its own ladder at mid-berth and its fenders out at the quarters, so a tyre or buoy
fender can never land on a ladder head (measured minimum clearance 1.0 m). `gameplay().berths`
reports each berth's class, span, deck-to-gunwale clearance at the current tide, and whether a ladder
is in reach.

## Gangways

`run` 3–14 m, height free — the slope re-solves from deck height and water level. A float with
`gangway: true` carries its own: the hinge and its abutment are fixed, the landing **rides the
float**, and the ramp is re-solved from the float's rocked deck height every frame. Anything driven
into the seabed — guide piles, chain, anchor block — is tagged `fixed` and never rocks with the deck.

## Modular dimensions

```js
WharfIso.render('pier', dir, {
  bays: 6, bayLen: 2.8, width: 4.2,   // 16.8 × 4.2 m
  tide: 0.4, tideRange: 1.8,          // metres above chart datum
  clearance: 1.0,                     // deck clear of HIGHEST water (deckZ derives from it)
  rail: 'pipe', railSides: ['shore','ends'],
  fittings: { ladder: 2, tyre: 4, foam: 1, cleat: 4, bollard: 2, ring: 2, dolphin: true },
  weather: 0.35, variant: 2,
});
// -> { data: Uint8ClampedArray, w, h, px, py, wet: Uint8Array }
```

`px,py` is the projection of the model origin (footprint centre at datum) — blit at
`screen(anchor) - (px,py)` and the structure lands where the game says it is. Cells size themselves
from the projected bbox; there is no fixed sheet.

`gameplay(family, opts).snap` returns `-X` / `+X` sockets at deck height, so modules chain
end-to-end into a whole harbour without per-instance nudging.

## Tide

```
z = 0        chart datum — lowest water
tideRange    datum → highest water, 0.3 m to 14 m (PEI 1.2, Gulf 1.8, Fundy 8+)
tide         current water level in metres, continuous — no baked states
```

The **tidal frame is fixed**; only the water moves through it. That is what makes a falling tide
read as *uncovering* rather than *repainting*:

| band | where | what it is |
|---|---|---|
| dark tide stain | ≤ HHW + 0.10 | wetting stain, creosote bleed |
| ice scour scar | 0.90 R – 1.04 R | winter ice line, growth scraped off |
| barnacle crust | 0.40 R – 0.80 R | white crust, +7 % on the silhouette |
| rockweed | 0.06 R – 0.40 R | olive band plus droopy fronds hanging below |
| wet sheen | below tide + 0.05 | ramp transform, plus the `wet` mask |

Growth is a **transform of the host material**, never its own colour — `concB` is barnacled
concrete, `poleG` is weeded creosote. Bands therefore never poster-stripe a face.

## Water contract (ADR 0010/0012/0023)

**Zero water pixels are baked.** No foam, no reflection, no sea. What the rig hands the shader
instead is `wet` — a per-pixel mask of its own submerged pixels — so the shader can tint, refract or
cut with no knowledge of the geometry. `opts.clipBelowWater: true` drops those pixels outright.

## Gameplay

`WharfIso.gameplay(family, opts)` returns metres-space data the pixels cannot carry:

- **walk** — walkable deck polygons, surface z, surface type, slope, and the float's rock amplitude
- **blockers** — bollards (circle), rail runs (wall), guide piles, dolphin
- **mooring** — cleats / bollards / rings / dolphin with line size and what each will hold
- **board** — boarding points, the deck-to-water **drop at this tide**, and `needsLadder` once that
  drop beats a step-across (1.2 m)
- **ladders** — reach, `rungsDry`, whether the foot is submerged
- **fenders** — contact circles a hull pushes off, and whether each is at the waterline right now
- **piles** — positions, radii, cap heights
- **snap** — module sockets
- **zones / exposure** — the tidal frame, and which bands are showing at this water level

`anchors(family, dir, opts, cell)` bakes the same points to **cell pixels** for the compositor.

Worked example, one pier (11.2 × 4.2 m, deck 2.80 m, range 1.80 m):

| tide | freeboard | boarding | rungs dry | showing |
|---|---|---|---|---|
| 0.00 LLW | 2.80 m | ladder needed | 9 | barnacle, weed, ice scar |
| 0.45 | 2.35 m | ladder needed | 7 | barnacle, weed |
| 0.90 | 1.90 m | ladder needed | 6 | barnacle, weed |
| 1.35 | 1.45 m | ladder needed | 4 | barnacle |
| 1.80 HHW | 1.00 m | step across | 3 | covered |

## Files

- `wharfIsoRig.js` — the rig. Plain script, exposes `globalThis.WharfIso`, no dependencies.
- `gameplay/wharfIsoRig.gameplay.json` — the contract plus generated samples for all seven families
  and the tide-response table above.
- `harness.html` — bakes every family across a tide sweep to one contact sheet in the browser.

Viewer: `Wharf Kit Iso.dc.html` (turntable, tide sweep, scale plate with the fisher on the deck and
the dory afloat alongside, old-vs-true fitting compare, family catalogue, gameplay overlay).
