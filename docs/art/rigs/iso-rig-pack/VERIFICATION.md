# ISO rig pack — independent verification of the committed contracts

**Role:** art-pipeline · **Date:** 2026-08-06 · **Harness:** [`_verify.js`](_verify.js)

This is the record of an independent re-measurement of the four contracts landed by
[#448](https://github.com/apholmes7-bot/hiddenharbours/pull/448), performed **before** the baker
slice was written against them. Everything below is reproducible from the committed rig sources with
a plain Node/V8 host and no shims:

```
node docs/art/rigs/iso-rig-pack/_verify.js          # contracts + the seven traps
node docs/art/rigs/iso-rig-pack/_verify.js --gate   # + the keyline-gate A/B
```

**Headline: all 156 committed cell entries now reproduce exactly** — cell, pivot, `pivotInsideInk`
and sheet packing. The contracts are a sound oracle. Five findings below change what the baker slice
should do, listed worst-first; **two of them have been applied to the contracts** under coordinator
ruling (2026-08-06), and are marked ✅ APPLIED where they appear.

On first measurement it was 155 of 156 — `fireCabinet` (§5) was the one that did not, and fixing it
is one of the two applied rulings.

---

## 1. The contract's cell RULE, recovered

#448 shipped the contracts but not the generator, so the rule behind each number had to be recovered
by measurement before a baker could reimplement `AssertMatchesContract`. **It is not the same rule
for all four families**, and using the wrong one does not throw — it silently disagrees with the
oracle on every key.

| family | rule |
|---|---|
| `wharfIso` | pivot-aligned union of the rig's **returned buffer** extents over 8 facings, floor/ceil |
| `wharfDecor` | fixed 420×520 buffer → pivot-**inclusive** union of the **ink** bbox over 8 facings |
| `utilityIso` | fixed 440×620 buffer → same as decor |
| `shoreFinds` | `cellOf(key)` verbatim — **analytic**, no render, no raster |

The wharf one is the trap. Measuring **ink** instead of the buffer gives `floatSet` 751×556 against
the committed 757×592, and **all 17 keys disagree**. The rig sizes its own buffer from the projected
geometry and reports a fractional `px,py` per bake; the contract records that buffer.

`_verify.js` encodes all four rules — the baker should port them from there rather than re-derive.

---

## 2. ✅ APPLIED — the 4096 cap bound on a preset the contract did not name

`wharfIsoRig.contract.json` declares `worstSheet: floatSet 3028×1184`. That is the worst sheet **by
area**. The import cap binds on **max dimension**, and by that measure the worst is `timberQuay`:

| | packed sheet | max dim | headroom to 4096 |
|---|---|---|---|
| `timberQuay` (8×1) | **4048×431** | **4048** | **48 px** |
| `sheetedPier` (8×1) | 3784×411 | 3784 | 312 px |
| `floatSet` (4×2) — *what the contract names* | 3028×1184 | 3028 | 1068 px |

A baker sizing its headroom off `worstSheet` would believe it has ~1068 px of slack. It has **48**.
And per trap 2 the wharf cell is parametric, so that margin is one authoring tweak deep:

| `timberQuay` at | packed 8×1 | |
|---|---|---|
| rig defaults (`bays:5, bayLen:3, width:6.5`) | 4048×431 | 48 px left |
| `bays: 6` | 4768×491 | **over cap** |
| `bayLen: 3.2` | 4256×449 | **over cap** |
| `width: 8.5` | 4416×458 | **over cap** |

Over the cap, Unity downscales the import and every slice rect silently lands wrong — this is the
texture-cap-poisons-slicing trap the handoff names, recurring one level up in the contract's own
summary field.

**Cheap fix, and it is not a cap raise:** pack `timberQuay` 4×2 like its larger siblings →
2024×862, comfortable. The 4096 cap ruling stands regardless; this is a packing-grid choice.

### ✅ What was applied

Coordinator ruling, 2026-08-06 — endorsed as recommended:

1. **`timberQuay` repacked 8×1 → 4×2**, so its sheet goes 4048×431 → **2024×862**. The binding
   constraint becomes `sheetedPier` at 3784 px, **312 px of headroom** instead of 48.
2. **Every contract now carries `worstSheetByMaxDim`** beside `worstSheet`, with `maxDim` and
   `headroomToCap`. `worstSheet` is the worst by *area* and is the wrong field to size import
   headroom off; two of the four families disagreed between the two measures — `wharfIso`
   (`floatSet` by area vs `sheetedPier` by max dim) and `shoreFinds` (`Driftwood` vs `RopeScrap`).
   `_verify.js` now asserts the new field reproduces, per family.

The cap itself is unchanged at 4096 — that is the owner's ruling and this did not touch it.

---

## 3. ⚠️ The keyline gate moves 103 of 156 cells — not all 156

#448's closing comment measured a uniform "2×2 px on the cold families, 1×1 on the warm finds, **no
exceptions**", and concluded all 156 entries move. Measured against **each family's own contract
rule** (§1) rather than a per-facing ink box, that is not what happens:

| family | cells that move | delta |
|---|---|---|
| `wharfDecor` | **61 / 61** | (−2,−2) ×60, `fireCabinet` (−2,−1) |
| `utilityIso` | **42 / 42** | (−2,−2) ×42 |
| `wharfIso` | **0 / 17** | — |
| `shoreFinds` | **0 / 36** | — |
| **total** | **103 / 156** | |

Why the two that don't move, don't:

- **`wharfIso`** — its cell comes from the rig's **buffer**, which the geometry sizes *before* the
  ring pass runs. The ring is drawn inside already-allocated space, so gating it changes pixels but
  not the cell.
- **`shoreFinds`** — `cellOf()` is **analytic**: it computes from form parameters and never renders.
  No renderer change can reach it.

The regeneration is still required, and still rides with the gated rigs. But it lands on **decor and
utility only**, and the wharf family — the one carrying the 4096 cap, the parametric cell and the
48 px of headroom — is untouched. Worth knowing before a wholesale regeneration is treated as
risk to the wharf numbers.

**The implementation trap #448 flags is real and confirmed:** gate the ring **pass**, never filter by
colour. Measured interior pixels sitting at exactly the keyline value, with no transparent
neighbour: `radioMast` **551**, `powerPole` 28. A colour-match removal punches 551 holes through the
mast's lattice.

---

## 4. ⚠️ The silent-0-facings trap hits three rigs, not one

The handoff and the catalog entry name `ShoreFinds` as the rig whose `DIRS` defeats
`RigCatalog.Install`'s `typeof DIRS === 'number'` check. Confirmed — but it is not alone:

| rig | `DIRS` | `typeof` | `Install` reports | contract declares |
|---|---|---|---|---|
| `WharfIso` | `8` | number | 8 | 8 |
| `WharfDecor` | **undefined** | undefined | **0** | **8** |
| `UtilityIso` | **undefined** | undefined | **0** | **8** |
| `ShoreFinds` | `["N","NE",…]` | object | **0** | 8 |

`WharfDecor` and `UtilityIso` are the two the catalog documents as the *safe* ones ("does expose the
W/H/pivot triple and loads with `Install`") — true as written, but neither exposes `DIRS` at all, so
both report 0 facings just as silently. `RigCatalog.cs:377` already treats 0 as "the rig does not
say", so nothing throws.

**Consequence for the baker: source facings from the CONTRACT (`facings: 8`), never from
`nativeDirs`.** Any cross-check asserting `nativeDirs == contract.facings` fails on three of four
families.

---

## 5. ✅ APPLIED — `fireCabinet`, the one cell that did not reproduce

The only failure in the sweep, and it was on the piece the trap was written to protect.

| | |
|---|---|
| ink union (8 facings) | 26×47 |
| **pivot-inclusive union** (the documented rule) | **26×53** |
| **contract** | **26×52** |

The contract's `cellH` equals `-T` exactly, i.e. it spans the top of the ink down to *one row above*
the pivot, excluding the pivot row itself. For all 60 other decor pieces the ink spans the pivot, so
`B ≥ 0` and `cellH = B - T + 1` — the off-by-one is unreachable. `fireCabinet` is wall-hung, `B = -6`,
and the bug surfaces.

Also: the handoff and #448 both say the pivot sits **5 px** below the ink. Measured across all 8
facings the tightest gap is **6 px** (`d1/d2/d6/d7`, ink bottom at −6 relative to pivot).

**Either fix the entry to 26×53 or record the exclusive convention explicitly** — otherwise a baker
implementing the documented pivot-inclusive rule refuses on `fireCabinet`, and it will read as a rig
regression rather than a contract typo.

### ✅ What was applied

Coordinator ruling, 2026-08-06 — the measurement wins over the doc:

1. **`fireCabinet.cellH` 52 → 53** (and its `sheet.sheetH` 52 → 53 to match). `pivotY` stays 52,
   which is what the rule already yielded.
2. **Every contract's `projection` now carries a `cellRule` string** stating that family's exact
   rule in words — pivot-INCLUSIVE ink union for `wharfDecor`/`utilityIso`, buffer union for
   `wharfIso`, analytic `cellOf` for `shoreFinds`. That is the convention note that stops the next
   reader re-litigating it, and it is also §1's recovered rule written where a baker will find it.

The pivot gap remains **6 px**, not the 5 px the handoff and #448 both state; the contract records no
gap figure, so nothing needed changing for that — it is recorded here.

---

## 6. `-46.75 deg/step` is a mean of a non-uniform quantity, not a rotation

The contract records `azimuth.measured: "-46.75 deg of screen rotation per dir step"`, matching
`houseIsoRig` / `wharfBuildingRig` / `interiorIsoRig` "to the digit". Reproduced exactly — and it is
worth knowing what it is:

```
raw SCREEN-space steps:  -32.73  -57.27  -57.27  -32.73  -32.73  -57.27  -57.27
mean of those 7          = -46.75          <-- the figure
ground-plane steps       = 45.000000 deg, uniform, all three rigs
```

Screen angle is foreshortened by `sin(40°)`, so the per-step screen rotation **alternates** and no
step is ever −46.75°. The mean is also an artifact of averaging 7 steps rather than 8 — including
the wrap step gives exactly −45.

It is a stable, reproducible fingerprint, and as a cross-family fingerprint it did its job. But
**the facings are exactly 45° apart**; a baker that used 46.75° as facing spacing would be wrong.

**The CounterClockwise registration is independently CORRECT.** Verified by the repo's own test
(`BuildingRigAzimuthProbe`): the `+Y` face lands 32 px screen-**west** at the cell `order[2]` labels
`"E"`, so the labels invert — counter-clockwise. All three directional rigs agree.

---

## 7. Everything else in the handoff, confirmed

| trap | verdict |
|---|---|
| 1 · union ≫ naive per-facing max | **confirmed exactly.** `floatSet`/`plasticSet` union **757×592** vs naive max **478×423** (1.58×). The "naive" figure is max *buffer* w × max *buffer* h — reproduces to the digit. |
| 2 · the wharf cell is parametric | **confirmed.** `sheetedPier` 473×411 at defaults → 740×584 at `bays:8` → 473×711 at `tideRange:14`. Exact packed figures in the handoff (1480×2332 / 764×2608) land within 4 px at a 2×4 packing. Assert off rendered cells, never a table. |
| 3 · `fireCabinet` pivot outside its ink | **confirmed**, gap is 6 px not 5, and it is exactly 1 of 61 (§5). |
| 4 · `ShoreFinds` breaks the loader twice | **confirmed** — and so do two others (§4). |
| 5 · finds squash 0.72, not 0.6428 | **confirmed.** `ShoreFinds.Q = 0.72`; iso rigs `sin(40°) = 0.6427876097`. Per family, never shared. |
| 6 · all three turn CCW | **confirmed** by the repo's own probe; see §6 for what −46.75 actually is. |
| 7 · utility spans are never baked | **confirmed.** 13 of 42 pieces export `ties()`; `powerPole.ties()` → `{wires, secondary, lamp}` in metres. Bake poles only, import ties beside the sheets. |

Determinism: two independent harness runs from a cold V8 host produce byte-identical output.

---

## What the baker slice should do with this

Two of the six are now done in the contracts themselves; four remain for the baker.

| | |
|---|---|
| ✅ | **`timberQuay` repacked 4×2 and `worstSheetByMaxDim` added to all four contracts** (§2). |
| ✅ | **`fireCabinet` fixed to 26×53 and `cellRule` recorded per family** (§5). |
| ☐ | **Port the four cell rules from `_verify.js`** — do not re-derive, and do not assume one rule. They are also written into each contract's `projection.cellRule`. |
| ☐ | **Take facings from the contract, not `nativeDirs`** — three of four rigs report 0 (§4). |
| ☐ | **Regenerate contracts for decor + utility only** when the gated rigs land; wharf and finds are unaffected (§3). |
| ☐ | **Gate the ring pass, never filter by colour** — 551 interior keyline pixels in `radioMast` alone (§3). |

Size import headroom off `worstSheetByMaxDim`, never off `worstSheet` — and because the wharf cell is
parametric (§7 trap 2), re-measure rather than trusting either field after an authoring change.
