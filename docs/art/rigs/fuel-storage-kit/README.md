# Hidden Harbours — Fuel Storage & Dispensing Kit

Everything on this coast that holds fuel, from a one-litre oil jug to fifty thousand litres on a
concrete pad: **8 vessels × 4 grades × 21 sizes × 8 facings × 3 wear states**, plus a continuous fill
solver that turns a volume fraction into a surface height, a mass and a sprite row.

Conventions (ADR-0006 bake): **32 px = 1 m**, ¾ camera in 45° steps at **elev 40°** (the fleet's
turntable), flat-facet shading from the fixed upper-LEFT key, z-buffered, ordered dither, depth-edge
darkening, **no AA**, binary alpha, **ringless** per ADR-0031 (`{keyline:true}` kept as the live A/B).

## The one rule: every vessel reads its level from outside

That was the brief and it is the whole design. Three mechanisms do it, and which one a vessel gets is
decided by **what it is made of**, not by what would be convenient to draw.

| Read | Vessels | How |
| --- | --- | --- |
| **`body`** | jug · jerry can · IBC tote | Dyed HDPE. The wall *is* the gauge: the `pale` ramp is plastic with air behind it, the `deep` ramp is the same plastic with fuel behind it, and the seam dithers to the 1 px line that does all the reading. |
| **`tube`** | steel drum · skid tank · steel jerry can | Painted steel can't be seen through, so the level comes outboard — a sight glass on a graduated white plate, standing proud on brackets with a gland at each end. |
| **`board`** | bulk tank | Five metres of steel has to be read from the fuel dock. Float-and-tape: a painted scale up the shell with a pointer riding the level in the grade colour. |

Two consequences worth knowing before you bake anything:

- **Gauges are mirrored on both long sides.** One gauge is invisible from three of eight facings, and
  a level you cannot see from the north is not a level. Real tanks often carry two for exactly this
  reason; here it is a hard rule, not a coincidence.
- **Hue survives the fill; only value moves.** An empty red can is still visibly the gas can. The
  `pale`/`deep` pair of any grade is the same hue at two different lightnesses, so the level line is a
  value break inside one colour rather than a change of colour.

## The grade code is the identification system

Four grades, four maximally-separated hues. `code` is the saturated colour that never lies about what
is in the vessel — caps, handles, grade bands, gauge pointers, dispenser crowns. It does not change
with fill.

| Grade | Code | Density | Notes |
| --- | --- | --- | --- |
| `gas` | red | 0.745 kg/L | The outboard grade, and the one you never want next to a hot exhaust. |
| `diesel` | amber | 0.840 | Everything with a wheelhouse runs on it; heaviest per litre. |
| `mixed` | blue | 0.752 | 50:1 premix for the two-strokes. A separate can, or a ruined powerhead. |
| `oil` | green | 0.885 | Sold in jugs, not pumped. The liquid itself is dark amber and shows that way in a glass — a green jug of brown oil, which is what a green jug of oil looks like. |

## The vessels

**Carry** — `pivotCarry` is the **grip**, and the character carries **one per hand**.

| Type | Sizes | Grip |
| --- | --- | --- |
| `jug` | 1 L · 4 L · 10 L | Moulded loop at the back shoulder (the 1 L has none — the hand takes the neck). |
| `jerry` | 5 L · 10 L · 20 L · 25 L | The middle of three handles: one man, two men, or hand it across a gunwale. `{shell:'steel'}` swaps the translucent body for painted steel and welds a sight glass on each end. |
| `nozzle` | auto · high-flow | The pistol grip. Cast body, trigger, hold-open latch, splash cone, hose whip off the back. |

**Storage** — pivot is the **base centre on the ground plane**.

| Type | Sizes | Notes |
| --- | --- | --- |
| `drum` | 60 L · 205 L | Two rolling hoops, two bungs, strap-on sight glass each side. `{pump:true}` fits a rotary hand pump in the large bung. |
| `tote` | 600 L · 1000 L | The whole bottle is the gauge — the one fuel vessel in the world that never needed a gauge fitted. Pallet base, ball valve, galvanised cage. |
| `skid` | 500 L · 1200 L · 2500 L | Horizontal cylinder on saddle legs — the wharf day tank. Fill cap, vent, outlet valve, end dial. |
| `bulk` | 10 kL · 25 kL · 50 kL | The commercial farm: pad, containment berm (`{berm:false}` to drop it), caged ladder, roof rail, vent stack, manway, grade band at the shoulder, bottom manifold, fill cabinet. |
| `pump` | single · twin | The pedestal on the fuel float: counter face, grade band, hose mast, nozzle boot. Twin serves two grades off one plinth — pass `grade2`. |

## Fill is a volume, not a height

`fill` is **0..1 of capacity by volume**. The rig solves the surface height itself, which matters
exactly once and matters a lot: a **horizontal cylinder is not linear in depth**. A skid tank at 25% by
volume stands **29.8%** of the way up its diameter, and the sight glass shows that, not a quarter.
`heightFrac('hcyl', v)` is the circular-segment inverse if you need it in your own code.

Every vessel keeps a small **ullage** above the surface at `fill: 1` — a container painted to its own
rim reads as overfilled, which is a different thing from full.

`level(type, size, fill, {fuel, dir})` returns litres, kilograms (tare + fuel × density), the height
fraction, the read mechanism and — when you pass `dir` — `readY`, the **cell row the surface lands on**.
Hang a HUD tick off it, or assert against it in your own bake.

Weights are the reason the numbers are there: a full 20 L jerry can of diesel is **19 kg** and a fisher
is carrying two. A 1000 L tote of diesel is **902 kg**, which is why it arrives on a pallet and leaves
on a forklift, and why nothing in the storage half of this kit has a carry pivot.

## Files

| File | What it is |
| --- | --- |
| `isoSolid.js` → `globalThis.IsoSolid` | The shared projector/rasteriser — the turntable. **Load first.** |
| `fuelRig.js` → `globalThis.FuelIso` | The vessels: geometry, grades, fill solver, wear, mounts, hose. |
| `fuel.contract.json` | Every number in the kit, machine-readable: grades, per-type × size cell / pivot / capacity / tare / full mass, read mechanism, both carry and rest cells. |
| `harness.html` | Standalone builder. Open it in a browser: no build, no deps, no project. |
| `FuelIso_JerryCan_s20_gas.png` | 8-dir, 8 × (17 × 25), pivot 8,19 — rest frame, working wear, 62% |
| `FuelIso_Jug_s4_oil.png` | 8-dir, 8 × (12 × 15), pivot 6,10 |
| `FuelIso_Nozzle_auto.png` | 8-dir, 8 × (21 × 19), pivot 10,9 — grip pivot |
| `FuelIso_Drum_s205_diesel.png` | 8-dir, 8 × (28 × 39), pivot 14,30 |
| `FuelIso_Tote_s1000_diesel.png` | 8-dir, 8 × (57 × 65), pivot 28,48 |
| `FuelIso_SkidTank_s1200_gas.png` | 8-dir, 8 × (67 × 74), pivot 33,54 |
| `FuelIso_BulkTank_s25k_diesel.png` | 8-dir, 8 × (170 × 177), pivot 85,121 |
| `FuelIso_Dispenser_twin.png` | 8-dir, 8 × (39 × 62), pivot 19,48 — gas + diesel |
| `FuelIso_fill_JerryCan_s20.png` | 9 × (17 × 25) — the level ramp, 0 → 100% in eighths |
| `FuelIso_fill_SkidTank_s1200.png` | 9 × (67 × 74) — the same fractions on a horizontal cylinder. Compare the two strips. |
| `FuelIso_grades_Drum_s205.png` | 4 × (28 × 39) — gas · diesel · mixed · oil |
| `FuelIso_wear_Drum_s205.png` | 3 × (28 × 39) — fresh · working · derelict |

8-dir sheets run **N NE E SE S SW W NW**, pivot pinned identically in every cell.

## Load order

```html
<script src="isoSolid.js"></script>   <!-- always first: every rig lathes against it -->
<script src="fuelRig.js"></script>
```

## Wiring cheat-sheet

1. **Measure first.** `FuelIso.cell(type, size, {rest})` → `{W, H, cx, cy}` before any pixels. Cells are
   the projected bounding box unioned over all eight facings at full fill, so nothing clips and the
   pivot lands on the same pixel in every cell. Carry cells add a **15° tilt allowance**, so a can at
   full swing never leaves its own quad — which is why a jerry can's carry cell (22 × 26) is bigger
   than its rest cell (17 × 25).
2. **Bake.** `render(type, dir, {size, fuel, fill, wear, shell, swing, tilt, roll, pitch, pump, berm,
   grade2, flow, keyline})` → `Uint8ClampedArray(W·H·4)`. `sheet(type, opts)` bakes all 8.
3. **Carry.** Carry types default to the **carry** pivot; pass `{rest:true}` for the placed frame. Pin
   `pivot` to `CharacterIso.carry(dir, {carry:'buckets'})` `handL`/`handR` and pass the returned
   `swingL`/`swingR` straight into `opts.swing` (**radians**). The vessel pendulums about the hand, not
   about its own base. Honour `behindL`/`behindR` for draw order.
4. **Mount.** `mount(type, dir, name, opts)` → `{x, y, depth}` in cell pixels. Names by type:
   `boot`/`boot2`/`hose` on the dispenser, `valve`/`cap` on the tote, `outlet`/`cap` on the skid,
   `bung`/`spout` on the drum, `fill`/`outlet`/`ladder` on the bulk tank, `tip`/`hose` on the nozzle,
   `spout`/`cap` on a jerry can, `grip` on anything carried.
5. **Hose.** 4.5 m of hose is 144 px and no cell holds that. `hosePath(from, to, {slack, seg})` returns
   a sagging polyline in cell pixels between two mount points; your renderer strokes it.
6. **Gauge.** `level()` for the numbers (see above). `contract()` dumps the whole table.

## Known limits

- **No liquid animation.** No slosh in a carried can, no stream from the nozzle, no splash in a funnel.
  `mount('nozzle', dir, 'tip')` gives you the emitter point; the particles are yours.
- **No spill, drip pan or absorbent boom.** A working fuel dock has all three; they are ground decals
  and runtime overlays, not pixels in these sheets.
- **No labels, placards or hull numbers.** At 32 px/m a painted "DIESEL" is four pixels of mush. The
  grade band and the code colour carry it instead, which is also how it reads at a distance in life.
- **No night/lit state.** The dispenser has a canopy light box but nothing emits; glow is a runtime
  layer, same as the buoy kit.
- Wear is three discrete states, not a continuum, and there is no per-vessel random seed on the paint,
  so two derelict drums of the same size rust identically.
- The **steel jerry can** is the one place a gauge is fitted that life would not fit. A steel can is
  opaque and has no glass; this kit welds one on because the brief says every vessel reads its level
  and a 20 L NATO can is too common a prop to be the exception. Pass `{shell:'steel'}` knowingly.

## Demo page (in the main project, not this kit)

`Fuel Rig.dc.html` — the live builder: the yard, the three reads side by side across five vessels, the
grade code, the carry pins running against `Art/characterIsoRig6.js`, the size ladder, eight headings,
wear, and the hose solving live between a dispenser and a nozzle.
