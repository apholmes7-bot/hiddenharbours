> ⚠️ **REPO CORRECTION BLOCK — added at import (2026-08-09). Everything below this block is the art
> director's drop README, verbatim and unedited. Where it disagrees with what the repo MEASURED, the
> measurements win: see [`VERIFICATION.md`](VERIFICATION.md).**
>
> 1. **The `hulls/` folder this README documents is NOT in the repo copy of this kit.** Eight of its
>    nine files were byte-identical to the repo's; the ninth, `capeIslanderIsoRig.js`, was **older
>    than the repo's** and would have reverted an owner ruling (#420, the full-sheer washboards). The
>    folder is refused wholesale. The repo's fleet rigs are one level up, at `../doryIsoRig.js` etc.,
>    so `harness.html`'s `<script src="hulls/…">` tags need re-pointing before a browser run. The rig
>    needs no hulls to bake.
> 2. **"1 px keyline" is no longer canon.** ADR 0031 retired it; this rig bakes it unconditionally
>    with no `{outline:false}` gate. Recorded as a gap for an upstream fix, not patched here.
> 3. **There is no interior.** `ghost` x-rays the buildings — a massing aid, not a room. The NMC
>    yard's interior gameplay data is authored in
>    [`gameplay/shipyardIsoRig.nmc_yard.gameplay.json`](gameplay/shipyardIsoRig.nmc_yard.gameplay.json);
>    the art is an open gap.
> 4. **The facing convention is `counterClockwise`** — measured by calibrating against `wharfIsoRig`,
>    whose `project()` this rig matches to 4 dp at all 8 dirs. This README does not state a
>    convention; do not infer one from the `th = +dir*PI/4` sign.
> 5. **The px/m table below is the rig's `fitScale()`, not what the repo bakes.** Three sites step
>    further down the ladder to fit 8 facings under the 4096 cap: `workingYard` 32→16,
>    `largeYard` 24→12, `industrialYard` 16→8.

# HIDDEN HARBOURS — SHIPYARD ISO RIG KIT

The boatyard, as a **rig**: parts modelled in metres and named yards that assemble them around a hull.
Baked through the **shared ¾ camera** (45° steps, elev 40°, upper-LEFT key, z-buffered, ordered dither,
per-face uv texture, depth-edge darkening, 1 px keyline, no AA) at **32 px = 1 m** — the same projection
as `wharfIsoRig` / `doryIsoRig` / `lobsterBoatIsoRig` / `characterIsoRig`.

One file, no dependencies, no build step, no DOM. An IIFE that attaches `globalThis.ShipyardIso` and
returns raw RGBA buffers. The hull rigs in `hulls/` are optional — the yards bake without them.

```
shipyardIsoRig.js                       the rig                          → globalThis.ShipyardIso
hulls/*.js                              the 9 fleet rigs it composites   → DoryIso, PuntIso, …
gameplay/shipyardIsoRig.gameplay.json   the contract + generated samples for all 5 sites and 20 parts
harness.html                            open in any browser: bake a yard, turntable, parts sheet, tide sweep
```

---

## The two layers

**PARTS** — the yard's vocabulary, each a real object in metres.

| kind | parts |
|---|---|
| haulout | `keelBlocks` · `cradle` · `railway` · `slipway` · `davitPair` · `travelLift` · `liftDock` |
| machine | `gantryCrane` · `jibCrane` |
| building | `winchHouse` · `shed` (clear-span workshop) · `office` |
| dock | `basin` (wet basin + gate) · `basinRoof` |
| yard | `fenceRun` · `staging` · `timberStack` · `sparRack` · `drums` · `sawhorse` |

**SITES** — whole yards that assemble those parts. **A site is sized by the boat it serves**, so the
4.5 m dory yard and the 110 m tanker yard are the same code with a different hull. Pass `hull:` to
re-size any yard to any boat in the fleet.

| site | tier | serves | footprint | px/m | mounts | haulout |
|---|---|---|---|---|---|---|
| `backyardSlip` | 1 | dory | 17 × 14 m | 32 | 2 | slipway, davits |
| `smallYard` | 2 | lobster boat | 30 × 24 m | 32 | 3 | slipway, railway, davits |
| `workingYard` | 3 | side dragger | 48 × 36 m | 32 | 4 | slipway, railway, davits |
| `largeYard` | 4 | stern trawler | 80 × 56 m | 24 | 5 | travel-lift, davits |
| `industrialYard` | 5 | gas tanker | 78 × 156 m | 16 | 3 | gated basin, roofed |

Water is at **+Y**, the road and back fence at **−Y** — the same axis convention as the wharf rig, so a
yard and a wharf module sit on one shoreline. `gameplay(site).snap` gives ±X sockets at grade.

## The fleet the yards are built around

Dimensions are **read off the hull rigs' own offsets tables**, not guessed — a cradle cut for `lobster`
is cut for the boat `LobsterBoatIso` actually bakes. `draft` and `depth` (keel to sheer) are what a yard
needs: they set blocking height, cradle depth, gantry clearance and basin depth.

| id | boat | LOA | beam | draft | depth | rig global |
|---|---|---|---|---|---|---|
| `dory` | dory | 4.5 | 1.50 | 0.26 | 0.60 | `DoryIso` |
| `punt` | punt | 5.2 | 1.58 | 0.28 | 0.56 | `PuntIso` |
| `skiff` | console skiff | 7.0 | 2.30 | 0.42 | 0.82 | `ConsoleIso` |
| `lobster` | lobster boat | 12.0 | 4.44 | 1.35 | 2.84 | `LobsterBoatIso` |
| `cape` | Cape Islander | 12.8 | 4.40 | 1.40 | 2.90 | `CapeIslanderIso` |
| `dragger` | side dragger | 25.0 | 7.00 | 2.55 | 3.50 | `SideDraggerIso` |
| `trawler` | stern trawler Mk II | 38.0 | 9.00 | 3.90 | 5.10 | `SternTrawlerMk2Iso` |
| `packet` | coastal packet | 60.0 | 10.40 | 4.20 | 5.25 | `CoastalPacketIso` |
| `tanker` | gas tanker | 110.0 | 17.40 | 6.20 | 12.30 | `TankerIso` |

Derived, not typed in: blocking height `0.55 / 1.05 / 1.35 / 1.85 / 2.30 m` by LOA band, railway gauge
`clamp(beam × 0.72, 1.5, 9.0)`, shed span `beam × 1.8 + 5`, basin floor `−(draft + 1.6)`.

`ShipyardIso.hullRigs()` reports which rig globals are actually loaded.

## Render

```js
const cell = ShipyardIso.render(what, dir, opts);   // what = site id or part id, dir = 0..7
```

```js
{ data,      // Uint8ClampedArray RGBA, row-major from top-left, straight alpha, a=0 where empty
  w, h,      // cell size — cells size themselves from the projected bbox, there is no fixed sheet
  px, py,    // projection of the model origin (footprint centre at chart datum)
  wet,       // Uint8Array — 1 where this pixel is below the waterline
  dep,       // Float32Array — camera depth per pixel, Infinity where empty
  pxPerM,    // the scale this cell actually baked at
  mounts,    // hull mounts in metres (below)
  hulls }    // how many hulls were composited in
```

Blit at `screen(anchor) − (px, py)` and the yard lands where the game says it is.

**Options.** `hull` · `tide` · `tideRange` · `elev` · `pxPerM` · `surface` (`gravel` `conc` `dirt`
`grass` `ways`) · `shedColour` (`corr` `galv` `red` `grn` `buff` `white`) · `hulls` · `wet` ·
`gateClosed` · `fence` · `ghost` (x-ray the buildings) · `clipBelowWater` · `growth`
(`{stain,barn,weed,ice,wet}`) · `weather` 0–1 · `variant` (deterministic scatter seed) · `lod`.

## Hull mounts, and hulls

**No hulls are baked into the yard.** Every cradle, davit, blocking stack, sling and basin berth
publishes a mount instead:

```js
{ id, support:'blocks|cradle|davit|slings|afloat|ways', cls:'dragger', label:'side dragger',
  loa, beam, draft, depth, x, y, keelZ, heading, note:'chocked on the ways' }
```

`keelZ` is where the keel bottom sits, `heading` is degrees from +X. That is everything a compositor
needs to drop its own sprite.

**Or let the rig do it.** `render(what, dir, { hulls:true })` composites the **fleet's own bakes** onto
those mounts: same camera, same elevation, scaled from each hull rig's px/m to the yard's, and **z-tested
against the yard's depth buffer** — so a shed wall, a cradle upright or staging in front of a hull still
covers it. Load whichever hull rigs you want; a class whose rig is absent is skipped.

```html
<script src="hulls/lobsterBoatIsoRig.js"></script>
<script src="shipyardIsoRig.js"></script>
```

Two things to know:

- An **afloat** hull (the tanker in the basin) shows her whole underwater body. That is the water
  contract working as intended — the rig bakes no water, so nothing hides a hull below the surface
  until your shader draws it. Use the `wet` mask, or `clipBelowWater:true` for a flat cut.
- The hull depth test uses **mid-hull height** as the hull's z. Gear standing well above the sheer
  (masts, gantries, a trawler's blocks) can be occluded a touch early behind a near roof edge. Bake
  those berths with `hulls:false` and composite the hull yourself if it matters.

`ShipyardIso.hullDir(dir, heading)` maps a mount's heading to the hull rig's own turntable index.

## Scale: a site is a planning bake

Parts bake at the native **32 px = 1 m**. A whole yard cannot — 156 m of covered dock at 32 px/m is a
6000 px sheet. `fitScale()` drops to **24 / 16 / 12 / 8 px per metre** until the cell fits, and
`cell.pxPerM` reports what was used. Detail below ~0.15 m is dropped when the scale can't hold it:

| px/m | lod | what goes |
|---|---|---|
| ≥ 16 | 1 | everything |
| 10–15 | 0.5 | weed fringe, sub-0.15 m trim |
| < 10 | 0 | massing only |

Force it with `pxPerM:` if you'd rather bake a big yard in tiles.

## Tide

```
z = 0        chart datum — lowest water
tideRange    datum → highest water, 0.3 m to 14 m
tide         current water level in metres, continuous — no baked states
gradeZ       yard grade, derived: tideRange + the site's freeboard
```

The tidal frame is fixed; only the water moves through it. Slipway, railway, basin walls and the gate
all run through it, so they band exactly like a quay face does:

| band | where | what it is |
|---|---|---|
| tide stain | ≤ range + 0.10 | wetting stain, creosote bleed |
| ice scour | 0.90 R – 1.04 R | winter ice line, growth scraped off |
| barnacle crust | 0.40 R – 0.80 R | white crust, +7 % on the silhouette |
| rockweed | 0.06 R – 0.40 R | olive band, fronds hanging below |
| wet sheen | < tide + 0.05 | ramp transform, plus the `wet` mask |

Growth is a **transform of the host material**, never its own colour, so bands never poster-stripe a face.

## Water contract (ADR 0010/0012/0023)

**Zero water pixels are baked** — no sea, no foam, no reflection, not even inside the wet basin. The rig
publishes `wet`, a per-pixel mask of its own submerged pixels, and the shader owns everything else.
`clipBelowWater:true` drops those pixels outright.

## Gameplay

`ShipyardIso.gameplay(what, opts)` returns metres-space data the pixels can't carry:

- **servesHull / footprint / tier / blurb** — what this yard is for
- **hullMounts** — every berth, with class, support type, keel height and heading
- **haulout** — slipway (`launchable` at this tide), railway (gauge, run, winch), travel-lift
  (`maxBeam`), basin (`depthAtTide`, `maxDraft`, `maxLoa`, `gateClosed`, `roofed`), davits
- **walk** — walkable apron polygon, surface z, surface type
- **blockers** — buildings as boxes, fence as a wall with its gate, basin as an open-water hazard
- **buildings** — kind, position, size, eave, clear span, door width, whether it carries a gantry
- **zones** — the tidal frame's band edges at this range
- **snap** — ±X sockets at grade, so yards and wharf modules chain

`anchors(what, dir, opts, cell)` bakes the same points to **cell pixels** — each mount with its
projected origin, bow, stern and a four-corner footprint quad — for the compositor. Pass the render
result so the pivot lines up.

Worked example, `industrialYard` (basin 123.2 × 28.2 m, floor −8.20 m, range 1.8 m):

| tide | basin depth | max draft admitted |
|---|---|---|
| 0.00 LLW | 8.20 m | 7.40 m |
| 0.45 | 8.65 m | 7.85 m |
| 0.90 | 9.10 m | 8.30 m |
| 1.35 | 9.55 m | 8.75 m |
| 1.80 HHW | 10.00 m | 9.20 m |

`gameplay/shipyardIsoRig.gameplay.json` carries this generated for all five sites and all twenty parts,
plus the schema and the full option list.

## Baking sheets

```js
// one yard, 8 headings, at half the auto scale
const ppm = Math.round(ShipyardIso.fitScale('workingYard', {}) / 2);
const cells = [];
for (let d = 0; d < 8; d++) cells.push(ShipyardIso.render('workingYard', d, { pxPerM: ppm, hulls: true }));

const cw = Math.max(...cells.map(c => c.w)), ch = Math.max(...cells.map(c => c.h));
const cvs = document.createElement('canvas');
cvs.width = cw * 8; cvs.height = ch;
const x = cvs.getContext('2d');
cells.forEach((c, i) => {
  const t = document.createElement('canvas'); t.width = c.w; t.height = c.h;
  t.getContext('2d').putImageData(new ImageData(c.data, c.w, c.h), 0, 0);
  x.drawImage(t, i * cw + ((cw - c.w) >> 1), ch - c.h);
});
```

Record each cell's `pxPerM`, `px`, `py` alongside the sheet — a yard baked at 16 px/m and one at 32 do
not share an atlas grid.

Headless (Node) works the same way: the rig never touches the DOM, so hand the buffers to `sharp` or
`pngjs`. **Budget** ~15–60 ms for a part, ~0.25–2.5 s for a whole yard with hulls. Bake, atlas, blit —
never call `render` per game frame.

## Working on it

Sites are **plans**: each `SITES[id].plan(hull, s)` returns a layout in metres (cells, buildings, fence,
slip, railway, basin) and the builder walks it calling parts. Move a number in a plan and the yard
re-lays out; change a hull's entry in `SHIPS` and every cradle, shed and gantry cut for it follows.

Textures may return `null` for a **hole** — that is real see-through chain-link mesh and open truss web,
not a grey panel standing in for a fence.

Turntable in the art project: **Shipyard Iso** (site catalogue, parts catalogue, turntable, tide sweep,
hull toggle, mount overlay, gameplay readout).

Questions → art.
