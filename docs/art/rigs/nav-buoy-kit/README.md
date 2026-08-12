# Hidden Harbours — Nav Buoy Rig Kit

The aids to navigation: **14 marks × 5 sizes × 3 wear states × 8 facings**, plus a live sea solver that
tells you how each one moves. IALA Region B as the Canadian Coast Guard flies it — lateral can and nun,
the lighted steel pair, four cardinals, isolated danger, safe water, special, regulatory, mooring, spar.

This is **not** `buoyIsoRig.js`. That is the lobster-pot float — 1.2 m of foam in a fisher's colours,
saying *my pots are here*. This kit says *the channel is here*, and it is steel, and some of it is 6 m tall.

Conventions (ADR-0006 bake): **32 px = 1 m**, ¾ camera in 45° steps at **elev 40°** (the fleet's
turntable), flat-facet shading from the fixed upper-LEFT key, z-buffered, ordered dither, depth-edge
darkening, **no AA**, binary alpha, **ringless** per ADR-0031 (`{keyline:true}` is kept as the live A/B).

## The bake is dry — nothing is cut at the water

Every cell in this kit is baked as a **whole solid**: hull, skirt and keel included, painted all the way
down. No water is composited over it, no alpha is knocked out below the waterline, no facing is clipped.

- **`pivot` is the still waterline.** Model `z = 0` projects to the pivot row, identical in all 8 cells.
- Everything **above** `pivot.y` is freeboard and tower; everything **below** it is the underwater body,
  down to `keelY`. `topY` / `keelY` / `bbox` in the contract are **measured painted rows** — the union of
  the alpha bounding box over all 8 facings, fresh and derelict. Crop and pack from those. The
  metre-space `zHiModel` / `zLoModel` are paint extents, *not* sheet rows: the tower and topmark rise
  above one and the skirt hangs below the other. The harness asserts the published keel row is the true
  lowest painted row in at least one facing, and that no facing paints outside the published box.
- **The clip is the engine's, at draw time.** `motion()` hands you `waterPx` — the *local* surface
  relative to the buoy, which is not the same as the buoy's own heave. Clip the sprite at
  `pivot.y + waterPx` and a lagging hull visibly buries its lowest band in a crest. Skip the clip and you
  have a dry buoy: slipway, winter store, chart legend, UI icon, catalogue sheet.
- The demo page's water painter (`Art/_buoySea.js`, which draws the sea back over the hull) is
  **deliberately not in this kit**. Water belongs to your renderer.

## Files

| File | What it is |
| --- | --- |
| `isoSolid.js` → `globalThis.IsoSolid` | The shared projector/rasteriser — the turntable. **Load first.** |
| `navBuoyRig.js` → `globalThis.NavBuoy` | The marks: geometry, palette, wear, lights, mooring, and the sea solver. |
| `navBuoy.contract.json` | Every number in this kit, machine-readable: mark table, sizes, wear, sea states, light characters, per-mark×size cell / pivot / measured painted bbox (`topY`, `keelY`, `belowWaterPx`) and hull metrics, and the 350-row response table. |
| `harness.html` | Standalone bake + solver harness. Open it in a browser: no build, no deps, no project. |
| `NavBuoyIso_CardinalW_s20.png` | 8-dir sheet, 8 × (178 × 304), pivot 89,210 — pillar, topmark, fresh, lit. |
| `NavBuoyIso_PortCan_s18.png` | 8 × (92 × 136), pivot 46,77 — the working default can, working wear. |
| `NavBuoyIso_StbdLit_s24.png` | 8 × (210 × 368), pivot 105,249 — the steel lighted hull, tower and cage. |
| `NavBuoyIso_Spar_s12.png` | 8 × (78 × 142), pivot 39,76 — the winter buoy that replaces the steel ones. |
| `NavBuoyIso_wear_StbdLit_s24.png` | 3 × (210 × 368), pivot 105,249 — fresh · working · derelict, one facing. |

8-dir sheets run **N NE E SE S SW W NW**, pivot pinned identically in every cell.

## Load order

```html
<script src="isoSolid.js"></script>     <!-- always first: every rig lathes against it -->
<script src="navBuoyRig.js"></script>
```

## The three axes

**14 marks** — `PortCan` `StbdNun` `PortLit` `StbdLit` `CardinalN/S/E/W` `Isolated` `SafeWater`
`Special` `Regulatory` `Mooring` `Spar`. Bands run bottom→top as fractions of the *whole painted height*
(hull and tower as one body), which is how they are actually painted.

**5 sizes** — 1.2 m (harbour, 0–10 m of water) · 1.8 m (the working default) · 2.0 m (main channel) ·
2.4 m (shipping channel, lit, radar-fitted) · 3.0 m (landfall and offshore).

**3 wear** — `fresh` · `working` · `derelict`. Rust takes whole facets rather than speckling, so it
streaks the way a steel hull actually goes; the growth band sits at the waterline.

## The motion is the point, and it is not a sine wave

`motion()` is a solver, not a loop: hand it sea state, wave direction, time and the buoy's **world
position**, get back roll / pitch / heave / yaw / sink in **world axes** (so the same numbers are correct
at all eight headings, and two buoys 30 m apart are on the same wave, not in sync). Three things make it
read right, all size-dependent:

- **Slope, not height.** A buoy tilts to the local wave *slope* (`a·k`). Harbour chop is 0.35 m on a 9 m
  wavelength and tilts a buoy 7°; ocean swell is 1.6 m on 190 m and tilts it 1.5° while heaving it five
  times as far. Get this wrong and every sea state looks like the same sea.
- **The waterplane averages.** A hull of radius R spanning wavenumber k sees the disc mean
  `2·J1(kR)/(kR)`, not the peak. A 3 m buoy in a 9 m chop straddles a third of a wavelength and shrugs.
- **Slope-follow share.** `follow = BM/(BM+BG)` — a flat can is 0.64 and rolls its guts out, a
  counterweighted steel buoy is 0.18 and stands up, a spar is ~0.002 and stays dead vertical, which is
  the entire reason spar buoys exist.

Then a second-order response about each natural period (`T_heave = 2π√(d/g)`, `T_roll = 2π·kxx/√(g·GM)`)
with phase lag, three wave components at spread headings, chain snub on the up-heave, current lean and
watch-circle yaw. Rough seas overtop the small hulls — `motion()` reports `submerged` (0..1).

## Wiring cheat-sheet

1. **Bake** `NavBuoy.render(type, dir, {size, wear, roll, pitch, yaw, heave, lit, mark, keyline})` →
   `Uint8ClampedArray(W·H·4)`. `NavBuoy.cell(type, size)` gives `{W, H, cx, cy}` up front, before any
   pixels. `sheet(type, opts)` bakes all 8. Cells carry a **16° tilt allowance**, so a buoy at full roll
   never leaves its own quad.
2. **Solve** `NavBuoy.motion(type, size, {sea, t, x, y, waveDeg, current, depth, seed})`. Pass
   `roll/pitch/yaw` straight into `render`. Place the sprite so `pivot` lands on the buoy's water point
   and offset by `heavePx`; pass `heave` into `render` instead only if you cannot move your quad.
3. **Water** — clip at `pivot.y + waterPx` (or don't; see above). `surface({sea, t, x, y, waveDeg})`
   returns `{eta, slopeX, slopeY}` from the same field, for anything that must agree with the buoys —
   the water shader, a hull, a second buoy.
4. **Light** — `lightOn(id, t)` against `LIGHTS[id]` (`Fl G 4s`, `Q(9) 15s`, `Mo(A) W 6s`, …). The lens
   material brightens with `{lit:true}`; the **glow, halo and reflection are yours** — no glow is baked.
5. **Mooring** — `riserPath(type, size, dir, {depth, scope, current, leadDeg, heave})` returns a
   polyline in **cell pixels** from shackle to sinker, with a colour ramp. 30 m of chain is 960 px and no
   cell holds that, so the chain is a line you draw, not pixels you blit. Fade it with `pt.t`.
6. **Budget** — `respond(type, size, sea)` is a cached peak-response summary (tilt, heave range,
   submerged fraction) for LOD and spawn decisions, precomputed in the contract for all 350 combinations.

## Known limits

- **No foam, wake collar or spray.** A buoy in a running sea wants a foam ring; it is a runtime overlay,
  not pixels in the sheet.
- **No hull numbers or letters.** Odd/even channel numbering is a decal layer — at 32 px/m a painted "7"
  is three pixels. The regulatory mark carries a topmark plate rather than a painted symbol for the same
  reason.
- **No light glow baked** — only the lens material changes with `lit`.
- Wear is three discrete states, not a continuum; there is no per-buoy random seed on the *paint* (the
  seed in `motion()` affects motion only), so two derelict buoys of the same mark rust identically.
- `roll`/`pitch` come back in **world** axes, not cell axes. This is a deliberate break with the rest of
  the fleet's rigs — pass them through unchanged and do not re-rotate by facing.
- Ice, snow load and the winter-buoy swap are the spar mark's job; there is no iced variant.

## Demo page (in the main project, not this kit)

`Nav Buoy Rig.dc.html` — the live builder: the fleet in a running sea, every mark as a strip, the wear
triptych, the light characters on the same clock, and the riser. It uses `Art/_buoySea.js` to paint water
over the hulls, which is exactly the compositing this kit leaves to you.
