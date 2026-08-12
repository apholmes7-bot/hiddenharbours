# Hidden Harbours — Lobster Boat Iso, Paint Kit

The canonical Tier-3 hull with its colour made a parameter. One 12.0 m Northumberland-Strait hardtop lobster
boat — the same geometry the fleet has been measured against — now bakeable in **12 paint schemes × 8
facings**, with the sampled white gelcoat still the default.

This is **not** `lobsterBoatVariantsIsoRig.js`. That rig is the fleet generator: 3 sizes × 3 styles × 3
regions × 12 paints, 324 legal combinations, a resolver, a glazing planner and generated gameplay sidecars.
This is the **hero hull**, unchanged in every dimension, that now takes the variants rig's paint axis and
nothing else. Load it where you already load `lobsterBoatIsoRig.js`.

Conventions (ADR-0006 bake): **32 px = 1 m**, ¾ camera in 45° steps at **elev 40°**, flat-facet shading from
the fixed upper-LEFT key, z-buffered, ordered dither, depth-edge darkening, 1 px keyline, **no AA**, binary
alpha. Single cell **456 × 420**, pivot **(228, 258)** = boat origin (amidships, keel bottom, centreline),
pinned every heading and every scheme.

## Drop-in: nothing but colour is new

`render(dir)` with no `paint` bakes **exactly** what the pre-paint rig baked — `gelcoat` holds the sampled
KTC ramps as literals, not as a regenerated approximation of them.

- **Paint never moves a vertex.** The face list is built once at load and is paint-independent (materials are
  named, `hull` / `boot` / `blue` / `cream`); a scheme only swaps the ramp each name resolves to. So all 12
  schemes share one silhouette, one pivot and one z-buffer result — the harness asserts the alpha mask is
  identical across all 12 (39,362 px, bbox 330 × 224).
- **Cost of a swap is a re-raster, not a rebuild.** Ramps and material tables are cached per scheme id.
- **Deck anchors are untouched.** `helmSeat` / `haulerMount` / `tubMounts` / `navMounts` and `HELM` /
  `HAULER` / `TUBS` return the same numbers as before, and take no `paint`. Existing sidecars
  (`lobsterBoatIsoRig.gameplay.json`) stay valid — the sidecar has no colour in it.
- **`HULL` / `BOOT` / `CREAM` / `BLUE` still export the gelcoat ramps**, so anything reading the rig's palette
  for a UI swatch or a buoy tint keeps working. `GRIP` is now exported too (it was internal).

## Files

| File | What it is |
| --- | --- |
| `lobsterBoatIsoRig.js` → `globalThis.LobsterBoatIso` | The rig. One file, no deps, no DOM, no build step. |
| `harness.html` | Standalone bake harness: the 12 schemes at heading SE with their ramps, an 8-heading turntable, and the silhouette assertion. Open it in a browser. |
| `sheets/LobsterBoatIso_<paint>.png` | 12 baked sheets, one per scheme: 8 × (456 × 420), 3648 × 420, pivot **228,258** in every cell. |
| `sheets/LobsterBoatIso_paints_contact.png` | Contact sheet — all 12 at heading SE, cropped to the alpha box (330 × 224), 4 × 3. Reference only, not for packing. |

8-dir order is **N NE E SE S SW W NW**. The sheets are uncropped full cells so the pivot is at a fixed
offset — crop and pack from the alpha box if your atlas wants it tight.

### Sheet audit (all 12, machine-checked)

Every sheet is a full 8-facing rig: **3648 × 420**, 8 cells, pivot **228,258** in every one, no empty cell,
nothing touching a cell edge. Painted pixels and measured alpha box per heading — identical across all 12
schemes, which is the paint-moves-no-vertex guarantee holding on the baked output, not just in the rig:

| Heading | Painted px | Alpha box (x0,y0 → x1,y1) |
| --- | --: | --- |
| N | 37,980 | 156,54 → 299,380 |
| NE | 42,370 | 75,74 → 404,365 |
| E | 46,006 | 19,90 → 420,292 |
| SE | 39,362 | 75,115 → 404,338 |
| S | 28,597 | 156,104 → 299,375 |
| SW | 39,361 | 51,115 → 380,338 |
| W | 46,006 | 35,90 → 436,292 |
| NW | 42,372 | 51,74 → 380,365 |

The union box is **19,54 → 436,380** (418 × 327) — use that if you want one crop for all headings and want
the pivot to stay common.

## Load order

```html
<script src="lobsterBoatIsoRig.js"></script>
```

Then bake:

```js
const rgba = LobsterBoatIso.render(3, { paint:'harbour' });   // SE, navy — Uint8ClampedArray(456*420*4)
const rgba = LobsterBoatIso.render(3);                        // SE, white gelcoat (default)
const r    = LobsterBoatIso.rock(i);                          // ride the wave
LobsterBoatIso.render(3, { paint:'spruce', roll:r.roll, pitch:r.pitch, heave:r.heave, elev:40 });
```

`opts` = `{ paint, elev, roll, pitch, heave }`. An unknown `paint` id falls back to `gelcoat` rather than
throwing. `render(dir, 34)` still works as the old elevation shorthand.

## The 12 schemes

Each scheme is four ramps — **topsides · boot · stripe · house** — generated in OKLCH at a fixed hue with
chroma easing off toward the light end, so every scheme sits on the same lightness/chroma discipline as the
sampled gelcoat. Roles: `top` = topsides paint · `boot` = boot-top and bottom · `stripe` = waterline and cove
accent · `house` = wheelhouse gelcoat, inner bulwark liner and cockpit sole margin.

Hexes below are the **value step** of each ramp (the step most lit faces land on), not the whole ramp — call
`paintRamps(id)` for all four arrays.

| id | Label | Topsides | Boot | Stripe | House | Note |
| --- | --- | --- | --- | --- | --- | --- |
| `gelcoat` | WHITE GELCOAT | #e4e9e3 | #171d27 | #2668a9 | #e8ebe5 | White topsides, near-black boot, twin blue stripes — the canonical bake. |
| `harbour` | HARBOUR NAVY | #384e77 | #0f1b32 | #a83f31 | #dadee3 | Deep navy topsides, white house, red waterline and cove. |
| `spruce` | SPRUCE GREEN | #385a41 | #151b16 | #aca180 | #dfdbce | Dark spruce over a black bottom, cream cove — the old wooden-boat scheme. |
| `ochre` | OCHRE | #a88747 | #102314 | #2b4831 | #e2dfd6 | Mustard-ochre topsides, dark green boot — the loudest hull in the harbour. |
| `oxblood` | OXBLOOD | #8e4842 | #2a1313 | #b3a78a | #e3dbd4 | Deep oxblood topsides with a cream cove stripe. |
| `fog` | FOG GREY | #a3acb2 | #4f1a19 | #83423f | #e0e3e6 | Pale cool grey topsides, white house, oxblood boot. |
| `capelin` | CAPELIN | #94b5ad | #0c1e1f | #2b7195 | #dbe2e1 | Seafoam blue-green topsides, blue cove — the inshore favourite. |
| `buff` | DORY BUFF | #c4ad90 | #4f1a19 | #81443f | #e6dfd6 | Buff-tan topsides over an oxblood bottom, straight off a dory. |
| `tarblack` | TAR BLACK | #393d41 | #661713 | #b2b4b8 | #d5d8db | Black topsides, white cove, red boot — the hard-used workhorse. |
| `bluefin` | BLUEFIN | #4e7eaf | #0c1c2b | #b6bbbf | #e0e3e6 | Mid cerulean topsides, white house, white cove. |
| `rust` | RED LEAD | #b26c4b | #2e1b14 | #513d34 | #ddd1c6 | Red-lead primer topsides — never finished, always working. |
| `pearl` | PEARL & GOLD | #c2bdb2 | #212c38 | #a9864e | #e8e5e0 | Off-white pearl topsides, blue-grey boot, a gold cove line. |

Ids, labels, notes and OKLCH specs are **verbatim from `lobsterBoatVariantsIsoRig.js`**, so a hull baked here
as `harbour` and the same scheme baked out of the variants rig are the same navy. Change a spec in one place
and it has to be changed in both — they are two copies of one table, deliberately, so neither rig depends on
the other.

### Why the dark schemes have compressed L ranges

Flat-facet shading (`GAIN 3.0`, `BIAS 2.7`) lands most lit hull faces on ramp steps 3–5. A dark paint given a
wide lightness range therefore renders as pale blue-grey: the steps that actually get used are all near the
top. Each spec's `[Ldark, Llight]` is chosen so **step 4 sits on the paint's true value** and step 6 is only
its highlight. If you add a scheme, tune it against the harness at heading SE and NW (the lit and shaded
sides), not in a swatch strip.

## Paint-independent materials

Non-skid deck, grippy cockpit sole, sea-grey glass, stainless and dark iron do **not** take a colour — they
are the same sampled ramps on every hull, because they are the same materials on every hull. That is what
keeps 12 schemes reading as one boat in different paint rather than 12 different boats.

## API

```
W H PX DIRS pivot defaultElev order        cell / turntable contract (unchanged)
render(dir, opts)                          opts = { paint, elev, roll, pitch, heave }
PAINTS                                     [{ id, label, note, … }] — the 12, in swatch order
paintRamps(id)                             { top[7], boot[5], stripe[5], house[7] } hex, dark→light
defaultPaint                               'gelcoat'
ROCK  rock(i)                              8-frame wave loop (roll 2.8° / pitch 1.6° / heave 1.2 px)
helmSeat(dir, opts)  HELM                  wheelhouse operator
haulerMount(dir, opts)  HAULER             starboard hauling block
tubMounts(dir, opts)  TUBS                 5 cockpit crate anchors
navMounts(dir, opts)                       { port, star, stern, mast } nav-light points
HULL BOOT CREAM DECKF GRIP GLAS BLUE STEEL IRON KEY    gelcoat + shared ramps
```

## Known limits

- **No per-boat weathering.** Two `rust` hulls rust identically; there is no seed on the paint. Streaking,
  scuffed boot-top and a green waterline band are a wear axis this kit does not have.
- **No name, port of registry or numbers.** At 32 px/m a painted transom name is three pixels. Decals are a
  runtime overlay layer.
- **No boot-top height variation.** The paint bands are fixed fractions of the hull height (`OB` in the rig):
  boot to 0.27, stripe to 0.315, topsides to 0.90, cove to 0.945, rubrail above. A scheme cannot move them.
- **The stripe is always twin.** Waterline and cove take the same ramp; a hull with a cove line and no
  waterline stripe is not expressible.
- **Not the variants rig.** One size, one style, one region. For inshore/offshore hulls, open boats, shelter
  decks or the Fundy and Newfoundland lines, use `lobsterBoatVariantsIsoRig.js` — same paint ids.
