# Fuel storage & dispensing kit — what was MEASURED

The drop's own `README.md` sits beside this file, unmodified. This one records what the repo
measured for itself before registering the kit, in the order it was measured, with the numbers.

Everything here came from a standalone V8 harness running the rig through the repo's **own**
ClearScript against the **registered** turntable — not the drop's copy of it. Re-run any of it in
seconds; there is no `node` on this machine and none is needed.

---

## 0. The faithfulness control — ALL TWELVE reference sheets reproduce byte-for-byte

Nothing below is admissible without this. Rendered through
`docs/art/rigs/deck-loop-kit/Art/isoSolid.js` (the registered `deckIsoSolid`), the rig reproduces
every reference PNG the drop shipped, **exactly**:

| sheet | solved parameters | result |
|---|---|---|
| `FuelIso_JerryCan_s20_gas` | `s20 gas rest fill 0.62 working` | **0 bytes differ** |
| `FuelIso_Jug_s4_oil` | `s4 oil rest fill 52/96 working` | **0** |
| `FuelIso_Nozzle_auto` | `sAuto diesel` — **CARRY cell, not rest** | **0** |
| `FuelIso_Drum_s205_diesel` | `s205 diesel fill 60/96 working` | **0** |
| `FuelIso_Tote_s1000_diesel` | `s1000 diesel fill 53/96 working` | **0** |
| `FuelIso_SkidTank_s1200_gas` | `s1200 gas fill 46/96 working` | **0** |
| `FuelIso_BulkTank_s25k_diesel` | `s25k diesel fill 63/96 working` | **0** |
| `FuelIso_Dispenser_twin` | `sTwin gas + grade2 diesel` | **0** |
| `FuelIso_fill_JerryCan_s20` | **dir 3**, gas, working, 0→1 in eighths | **0** |
| `FuelIso_fill_SkidTank_s1200` | **dir 3**, diesel, working | **0** |
| `FuelIso_grades_Drum_s205` | **dir 3**, fill 58/96, working | **0** |
| `FuelIso_wear_Drum_s205` | **dir 3**, fill 58/96 | **0** |

⚠️ **The parameters were SOLVED by exhaustive search, not read off the README.** Three of them are
not what the file table implies: the **nozzle sheet is in the CARRY cell** (21×19, grip pivot) while
every other 8-dir sheet is at rest; its grade is **diesel**, not the default gas; and the strip
sheets are all at **dir 3 (SE)**, which nothing states. Guessing any of these produces a sheet that
looks perfectly reasonable and differs in a few hundred bytes.

`geo()` quantises fill to `Math.round(fill*96)/96`, so the search space is exactly 97 values per
vessel — small enough to sweep completely rather than sample.

## 1. The turntable is the deck-loop kit's, by identity

The drop ships `isoSolid.js`. It **is** `deck-loop-kit/Art/isoSolid.js`: LF-normalised sha256
`f7fc9db510b0346dea568c43dc7378f1a12fd71ee8f5a7653e78377812b792bd` on both, the 10,785 vs 11,001
byte difference being one `\r` per line. The drop's copy is therefore **gitignored** and the catalog
declares `deckIsoSolid` as this kit's prerequisite.

Proved in pixels rather than by hash, and separately by projection: `FuelIso.mount()` and
`IsoSolid.proj()` agree to **dx = 0 exactly at all 8 facings**, with a dir-INVARIANT y residual
(a different assumed height for the probe point, not a different camera — a second camera would
make the residual vary with dir).

To open `harness.html`, materialise the copy locally — and do not commit it:

```bash
cp docs/art/rigs/deck-loop-kit/Art/isoSolid.js docs/art/rigs/fuel-storage-kit/isoSolid.js
```

## 2. Handedness: CLOCKWISE, measured

Measured against `utilityIso`, which this repo registers **CounterClockwise** after its own probe,
in ONE host with one sign convention — the un-squashed ground-plane bearing of the +X axis:

| family | +X ground step / dir | screen step mean |
|---|---:|---:|
| **fuel kit** (this) | **−45.0000°** | −46.7525° |
| `utility-iso` (registered CCW) | **+45.0000°** | +46.7525° |

⚠️ **The screen figure does not distinguish them** — same magnitude, because it is an alternating
foreshortened quantity. Only the ground-plane bearing is a handedness test. This is the same trap
that has mislabelled kits in this repo repeatedly; `FuelStorageKitTests` pins it deliberately so a
"simplified" probe fails immediately.

The kit therefore takes **no facing correction** (`RigBaker.DirForCell` passes clockwise straight
through), exactly like the deck-loop family it shares a turntable with.

## 3. Shape: no standard triple, so `InstallModule`

`FuelIso` exposes no `W`/`H`/`pivot` — the cell is per (type, size, mode) from
`cell(type, size, opts)`. It *does* expose `DIRS` as a number (8) and `defaultElev` (40). Loading it
with `RigCatalog.Install` would throw on the missing pivot.

Pivot is the **base centre on the ground plane** for storage vessels and the **grip** for carried
ones, and it normalises the hull way — `(H − pivotY)/H`, ADR 0026 — because it is a projected POINT,
not a chosen row.

## 4. Two upstream defects, recorded not patched

His file runs unmodified (ADR 0021 §5). Both are worked around on our side and both are pinned by a
test that **goes RED the day a drop fixes them**.

### 4a. ⚠️ An omitted `wear` is a phantom fourth state that collides in the geometry cache

The streak passes take `o.wear` **raw** and bail on a falsy value:

```js
function streakCyl(F, r, z0, z1, count, seed, wear) {
  if (!wear || wear === 'fresh') return;      // undefined behaves as 'fresh'
```

…but `wearPass` and the cache key both normalise the same call to `'working'`:

```js
const key = [type, size, q, o.wear || 'working', shell, …].join('|');
G.F = wearPass(G.F, o.wear || 'working', type, size);
```

So an omitted `wear` renders **`wearPass` at 'working' with no rust streaks** — an image that is
neither `fresh` nor `working` — and stores it under `'working'`'s cache key. Measured on
`drum s205` dir 3, three distinct images, and the collision is order-dependent:

| call order | first call | second call |
|---|---|---|
| omitted, then `working` | `4367f5c1…` | `4367f5c1…` ← **the omitted image, returned for `working`** |
| `working`, then omitted | `121757c0…` | `121757c0…` ← **the reverse** |

`'fresh'` is a third image (`c902230e…`) in both orders. Only the four steel vessels are affected
(drum, skid, bulk carry rust streaks; the HDPE ones do not), which is why this looks like a
cross-rig collision at first — it is not, and bisecting every registered rig ruled that out.

**Mitigation:** `FuelSheetBaker` passes an explicit `wear` on every single render.

### 4b. `resolveSize` substitutes a default for any size it does not know

```js
function resolveSize(t, s) { const k = sizesOf(t); return (s && TYPES[t].sizes[s]) ? s : k[Math.min(k.length-1,1)]; }
```

`resolveSize('drum', 's_205')` → `'s205'`, silently. A typo'd size bakes a real vessel of the wrong
size under the right filename, and every downstream check passes because the sheet is valid.
**Mitigation:** `FuelSheetBaker.AssertKnown` checks type, size and grade against the rig's own
tables before rendering.

## 5. The ring pass

- `FuelIso` exports **no** `KEYLINE_DEFAULT` — same gap the nav-buoy drop had. The gate binds to
  `IsoSolid.KEYLINE_DEFAULT`, which is `false` and owns the only ring pass the kit has.
- `{keyline:true}` changes **376 px** on a drum — the A/B arm works, so "ringless" is falsifiable.
- ⚠️ `{outline:true}`, the spelling the rest of this repo uses, changes **0 px**. It is silently
  ignored, and an A/B driven with it comes back ringless — indistinguishable from a successful
  gate-off render.

## 6. Fill is a WHOLE-CONTAINER axis — there is no overlay path

The handoff's question, answered by measurement. `fill` is an argument to the geometry builder and
the wall is split at the liquid surface *before* projection (`shellBox`); the rig exposes no
`renderFill`/`fillOverlay`/`renderLayers` entry point at all. So a fill state costs a **full cell**.

What a fill change actually moves, by read mechanism, at dir 3 from empty to full:

| read | vessel | changed px | % of cell | diff bbox as % of ink area |
|---|---|---:|---:|---:|
| `body` | jug s4 | 37 | 20.6% | 80.0% |
| `body` | jerry s20 | 134 | 31.5% | 88.9% |
| `body` | tote s1000 | 723 | 19.5% | 57.5% |
| `tube` | drum s205 | 34 | **3.1%** | **11.8%** |
| `tube` | skid s1200 | 65 | **1.3%** | **9.9%** |
| `board` | bulk s25k | 418 | 1.4% | 48.5% |

The silhouette is essentially invariant (0–11 px of alpha differ across the whole ladder), so fill
is pure interior repaint.

⚠️ **This is the flagged budget finding.** For the `tube`-read vessels the fill state changes ~1–3%
of the cell inside a gauge-sized box, so a *derived* overlay — bake the base once, bake the gauge
patch per fill — would cut the storage half of the kit by roughly 80%. The rig does not offer that,
so building it means the BAKER inventing a compositing mechanism, with dither and depth-edge
interactions at the patch seam. Deliberately **not** done in this PR; costed in the PR body for the
owner to rule on.

Also measured: **`nozzle` and `pump` have exactly ONE distinct frame across all 97 fill quanta**
(their `read` is `none`). Baking the ladder for them would ship four duplicate dispensers per grade.

## 7. Facings: all eight distinct, at every wear

Unlike the nav-buoy kit — where bodies of revolution collapse to four at `fresh` — every one of the
eight vessels renders 8 distinct facings at `fresh`, `working` **and** `derelict`. Nothing here is
collapsible, so a reduced facing count is a budget decision and never a free one.

## 8. Crop headroom

Cells are the projected bbox unioned over all eight facings at full fill, so a single facing never
fills its own cell; carry cells additionally carry a 15° tilt allowance this bake does not use.
Cropping to the pivot-inclusive ink union saves:

| family | saving |
|---|---|
| jug / jerry / nozzle (rest) | 34–79% |
| drum / tote / skid / pump | 10–30% |
| bulk | 4.6–6.5% |

The bake crops from the rendered cells rather than from this table, and unions over **every facing
and every fill** so the fill rows slice into identical rects.

## 9. Globals

`fuelRig.js` installs exactly one global, `FuelIso`. Loaded LAST after all 28 rigs the catalog
registers, it still adds only that one and its renders are unchanged — no collision with the crowded
prop-rig family (`BucketIso`, `FishTote`, `TrapIso`, `BuoyIso`, …).

---

## Re-running the measurements

The harness is a ~60-line `net8.0` console app referencing
`Assets/_Project/Plugins/Editor/JsEngine/` in place — same DLLs `V8RigScriptHost` loads, so it and
the Unity baker run one engine. Recipe and traps: the `run-rigs-in-standalone-v8-harness` note.
In-editor, the same measurements run at every bake through `FuelRegistrationProbe`, and as tests in
`Assets/Tests/EditMode/RigBaking/FuelStorageKitTests.cs`.
