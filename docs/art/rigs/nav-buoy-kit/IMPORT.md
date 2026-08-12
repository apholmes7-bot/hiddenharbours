# Nav Buoy Kit — import record (2026-08-11)

What arrived in the owner's drop, what was taken, what was **refused**, and what was **measured**.
Nothing below is read off the drop's README; every number came out of a rendered buffer or a live
`project()` call.

## The drop

`Pixel art capabilitiesnavbuoyrigkit.zip` → `export/nav-buoy-kit/`: one rig `.js`, the shared
turntable, a machine-readable contract, a standalone `harness.html`, and five pre-rendered reference
PNGs. Unusually for this repo, **the drop ships its own contract** — 4,866 lines of it — and, more
unusually still, it is **correct in every particular** (see §"70/70").

| file | verdict | action |
|---|---|---|
| `navBuoyRig.js` | **NEW** — `NavBuoy`, 14 marks × 5 sizes × 3 wear | imported verbatim |
| `navBuoy.contract.json` | **NEW** — the drop's own measurements | imported verbatim |
| `harness.html` | **NEW** — standalone bake + solver page | imported verbatim |
| `README.md` | **NEW** — the kit's own documentation | imported verbatim |
| `isoSolid.js` | **byte-identical** to the registered `deckIsoSolid` | **NOT committed** — see below |
| 5 × `NavBuoyIso_*.png` | reference sheets, up to 1680×368 | `reference/` (LFS), never `Assets/` |

## The collision, and why it resolved by identity

⚠️ **This is the second buoy family in the repo.** `buoyIso` (deck-loop kit, `RigCatalog.cs`) is the
**lobster spar float** — 1.2 m of foam in a fisher's colours on a 10×32 cell, baked to
`DeckLoopSheets/Buoys`, consumed by `TrapBuoyPresenter`. This kit is the **aids to navigation**:
steel, up to 6.6 m tall, and it says *the channel is here*. Different global, different sheet folder,
different consumer. Nothing here touches the trap-loop buoys.

The real hazard was not that name — it was `isoSolid.js`. The drop ships one, and it installs
`globalThis.IsoSolid`, **a global the deck-loop kit already owns**. Two different files claiming one
global is a load-order failure, and load-order failures in this repo render wrong rather than
throwing (the shop kit, #437).

They are not two different files:

| | bytes | sha256 |
|---|---:|---|
| `deck-loop-kit/Art/isoSolid.js` (registered) | 11,001 | `a3fe7008…` |
| the drop's `isoSolid.js` | 10,785 | `f7fc9db5…` |
| **both, LF-normalised** | 10,785 | **`f7fc9db5…`** |

216 lines, 216 bytes, one `\r` each — the repo's copy is CRLF and the drop's is LF. The content is
**character-for-character identical**. So the kit's copy is **gitignored** rather than committed
(`docs/art/rigs/README.md`'s no-edit rule: a second copy of a registered global's source is exactly
the drift it exists to prevent), and the catalog entry declares `deckIsoSolid` as its prerequisite.

`NavBuoyRegistrationProbeTests.TheRegisteredTurntableStillMatchesTheOneTheDropWasAuthoredAgainst`
pins that sha. A hash, not a file-diff: the uncommitted copy cannot be compared to in a clean
checkout, and a test that quietly skips is not a guard. It fires on the direction that can actually
change silently in-repo — someone editing `deckIsoSolid` for the trap loop and moving nav-buoy art
as a side effect.

### The faithfulness control — the substitution is proven in PIXELS, not by hash

A matching hash says the files agree. It does not say the *substitution renders the same art*, and
the memory of this lane is explicit that a plausible harness must reproduce a committed artefact
before any of its other findings are believed. The drop shipped four 8-facing reference sheets, so
they were the control. Loaded against the **registered `deckIsoSolid`**, in a standalone V8 host:

| reference sheet | options that reproduce it | result |
|---|---|---|
| `NavBuoyIso_PortCan_s18.png` (736×136) | `wear:'working'` | **byte-identical** |
| `NavBuoyIso_CardinalW_s20.png` (1424×304) | `wear:'fresh', lit:true` | **byte-identical** |
| `NavBuoyIso_StbdLit_s24.png` (1680×368) | `wear:'working', lit:true` | **byte-identical** |
| `NavBuoyIso_Spar_s12.png` (624×142) | `wear:'working'` | **byte-identical** |

All four, exact, first pass on the right options. The control also *worked as a control*: `StbdLit`
came up 1,068 px wrong (0.17%) on `wear:'working'` alone and only closed at `working + lit` — a
difference no eye would have caught in a screenshot and no hash comparison would have surfaced.

`lit` is a measured no-op on `PortCan` and `Spar` (identical with and without): the lens material
only exists on the shapes that carry a lamp.

## Handedness — measured, and the README is right

The drop declares CW. The repo's boats are CounterClockwise and a declared order has shipped wrong
five times here, so it was measured: +X ground-plane bearing per `dir`, depth un-squashed by
`/sin 40°`.

| quantity | value | verdict |
|---|---:|---|
| ground-plane step | **−45.000000°/dir** | **CLOCKWISE** |
| screen-space mean step | −46.7525°/dir | **inadmissible** — see below |

Bearings run `0, −45, −90, −135, −180, +135, +90, +45` — uniform, so the turntable is real and a
handedness can be named for it.

⚠️ **The screen mean is −46.7525°, numerically identical to the figure the iso-rig-pack contracts
record for their _CounterClockwise_ rigs.** The screen angle is an alternating, foreshortened
quantity; two families can share it and turn opposite ways, and these do. Only the un-squashed
ground-plane bearing is a handedness test. `NavBuoyRegistrationProbeTests.TheScreenAngleCannotDecideHandedness`
pins that trap so nobody re-derives handedness the easy way.

This kit rides the deck-loop turntable, so sharing its handedness is expected — but expected is not
measured, and the bake refuses on a mismatch like every sibling.

## 70/70 — the drop's own contract reproduces exactly

Every one of the 14 × 5 cells was re-derived from rendered pixels and compared to the committed
`navBuoy.contract.json`: `W`, `H`, `pivot`, `waterlineY`, `topY`, `keelY`, `bbox` (all four sides),
`belowWaterPx`, `aboveWaterPx`.

**70/70 cells match exactly, on every field.** That is a first for a drop in this repo and it is
recorded because it is unusual, not because it was assumed.

## The pivot is the WATERLINE, not the ground

Every other kit here pivots on the ground under the object. A nav buoy floats. Measured: model
`z = 0` projects to `(cx, cy)` **identically at all 8 facings** (0 px deviation, 1e-9 tolerance), and
the drop's `waterlineY` equals its `pivot.y` on all 70 cells. Everything above the pivot is freeboard
and tower; everything below is the underwater body down to `keelY`.

This matters downstream twice: the bob is measured from it, and a slicer that treats it as a ground
pivot puts every mark's keel on the surface — wrong, but plausible enough to ship.

## Measurements a README cannot give you

### The cells are 55–79% tilt allowance, and cropping is what settles the cap

The rig's native cells carry a **16° tilt allowance** so a buoy at full roll never leaves its quad.
We bake the **static pose**, so that allowance is dead weight:

| | worst sheet | longest side | headroom under 2048 |
|---|---|---:|---:|
| uncropped (native cells) | `PortLit\|s30` 2048×452 | **2048** | **0** |
| cropped to painted ink | `Mooring\|s30` 816×126 | **816** | **1,232** |

Baked uncropped, one sheet lands *precisely* on the cap — legal today, and one rig revision away from
importing silently downscaled. Cropped, the whole 70-cell kit clears it with 60% to spare and the
slicer's `maxTextureSize` lift never fires. The 50 shipping sheets drop from 20.25 Mpx to
**5.14 Mpx**. Cap is **2048**, one constant for both the pack and the import.

**The cell unions over all three wear states**, not just the one that bakes. Rust takes whole facets
and the growth band sits at the waterline, so a derelict hull paints pixels a fresh one does not.
Unioning over wear makes the slice rects wear-invariant, so `fresh`/`derelict` can be baked later
into the same rects with no re-slice.

### Facing collapse is a `fresh`-only property — rust is what makes eight facings real

Byte-distinct facings out of 8, at `s20`, measured in all three wear states:

| mark | `fresh` | `working` | `derelict` | collapses at `fresh` |
|---|---:|---:|---:|---|
| `PortCan` `StbdNun` `Regulatory` | **4** | 8 | 8 | `{0,4} {1,5} {2,6} {3,7}` |
| `StbdLit` `Mooring` | 6 | 8 | 8 | `{2,6} {3,7}` |
| `Spar` | 6 | 8 | 8 | `{0,4} {2,6}` |
| `PortLit` | 7 | 8 | 8 | `{3,7}` |
| `CardinalE` `Isolated` `Special` | 7 | 8 | 8 | `{2,6}` |
| `CardinalN` `CardinalS` `CardinalW` `SafeWater` | 8 | 8 | 8 | — |

⚠️ **This was nearly recorded wrong.** The first sweep measured at `fresh` and produced the 4/6/7/8
spread above; the in-engine measure, which runs at the shipping wear, came back **8 across the board**
and the two had to be reconciled rather than one believed. The reconciliation is the finding:

- At `fresh` a plain can is a **body of revolution** — seen from opposite sides it *is* the same
  picture, so `{0,4}` etc. collapse. That is correct art, not a missing facing.
- At `working` and `derelict`, **rust breaks the symmetry**. It takes whole facets rather than
  speckling, so *which* rusted facets face the camera depends on the facing, and all eight become
  distinct.

**Practical consequence: nothing in the shipped sheets is collapsible.** The kit bakes `working`, and
at `working` every mark has eight genuinely distinct pictures. A packer that collapsed rows on the
strength of the `fresh` measurement would flatten art that differs. The contract records
`distinctFacings` **at the baked wear** for exactly this reason.

### The cardinals are readable from every approach — which is the whole point

A cardinal read wrong wrecks the boat that trusted it, so "are the 8 facings distinct" is the wrong
question. The right one is: at **every** facing, is a North cardinal distinguishable from a South,
East or West one, and is the difference in the **topmark**?

Topmark-band agreement between cardinal pairs, all 8 facings (lower = more distinguishable):

| pair | agreement | pair | agreement |
|---|---:|---|---:|
| N vs S | 86.99–87.41% | S vs E | 83.14–83.26% |
| N vs E | 85.22–85.42% | S vs W | **75.91–76.03%** |
| N vs W | 77.27–77.53% | E vs W | 84.50–84.62% |

Every pair differs at every facing, and the figures are **near-constant across the eight** (N vs S
varies by 0.42 points). That constancy is the finding: the double-cone topmarks are **axisymmetric**,
so a cardinal presents the same mark from any approach — correct seamanship, and it means there is no
facing at which one cardinal misreads as another.

The closest pair is N vs S at ~87% of the topmark band. That is a real, visible difference (cones up
versus cones down) but it is the tightest read in the kit; see `_confirm` 3 on scale.

### Laterals — IALA Region B, checked by colour and shape

| mark | cell (s20) | mean painted RGB |
|---|---|---|
| `PortCan` | 104×158 | `(78,130,89)` — green |
| `StbdNun` | 116×182 | `(171,83,77)` — red |

Different shape *and* different colour, which is what the "the SHAPE is the mark" gloss requires.

### Shape facts

- **No `W`/`H`/`pivot`/`DIRS`/`defaultElev` globals** — all five measure `undefined`. Loads with
  `InstallModule`; `Install` would throw on the missing pivot. Facings come from the contract, never
  from a `DIRS` field (#452: `Install` reported 0 facings for three of four packs).
- **The rig installs exactly one global**, `NavBuoy`. Swept against every registered global in the
  catalog: no collision. `EveryRegisteredGlobalIsUnique` keeps it that way for future kits too.
- **A missing turntable THROWS** — `Cannot read properties of undefined (reading 'tube')` — rather
  than rendering wrong art. A happier failure mode than the pass-6 character body's silent fallback.
  The prerequisite is still declared: a loud failure is not a reason to leave the dependency to each
  caller.
- **Determinism**: repeated renders in one host are byte-identical.

## Two defects found in the drop — recorded, not patched

His file runs **unmodified** (ADR 0021 §5), so both are upstream requests rather than edits.

1. **No `KEYLINE_DEFAULT` export.** `IsoPackContract.AssertKeylineGated` probes exactly that field on
   the rig's own global and refuses a family without one — the shipyard kit landed in this state and
   could not bake until #477 added the export upstream. Measured here: the field is `undefined`, but
   the **behaviour** is correct (default render is ringless at 7,958 px; `{keyline:true}` reaches a
   live ring pass at 8,446 px; pure ring deletion, 0 painted pixels differ). So the bake asserts the
   behaviour instead of the declaration, both arms — "no ring pixels" would also pass on a renderer
   that had stopped drawing.

2. **`{outline:true}` is not wired to the ring pass.** It is silently ignored and returns the default
   render, byte-identical to passing nothing. `{outline:true}` is the name #463/#477 and the rest of
   the repo use, so an A/B driven with the standard name comes back ringless and **reads as a pass**.
   The deck-loop kit hit this exact defect and had it fixed upstream; this kit has it again.

Both are pinned by tests that will go RED when the owner's next drop fixes them, which is when
someone should notice.

## One internal inconsistency, surfaced as data

The rig's own header comment says **"13 TYPES"** and then the kit ships **14** (`TYPE_IDS.length`
measures 14; the drop's README and contract both say 14). Same class of slip as the deck-loop kit's
"Five clips" comment above six clips. **The count is 14.**

## What this PR bakes, and what it parks

`_confirm` 1's default, taken literally — the four cardinals, both lateral pairs (plain and lighted),
mooring and isolated danger: **10 marks × 5 sizes × 8 facings = 50 sheets, 400 renders**, at
`wear: working`, `lit: false`.

Parked, and genuinely one Build away — they are measured into the contract and listed in
`NavBuoyKit.AllTypes`; only `BakeTypes` gates them:

- **4 mark types**: `SafeWater` `Special` `Regulatory` `Spar`
- **2 wear states**: `fresh` `derelict` — and they will slice into *these* rects, by construction
- **the lit variants**: measured at 936 of 432,896 px (0.2%) on `CardinalW|s20` — a lens highlight,
  no glow/halo/reflection is baked at all. A whole second 50-sheet set for that is not the way to
  park a night feature (`_confirm` 2).

## Not in this PR

No placement — laying channels and marking rocks is the owner's mapping session with the painted
seabed, and it needs his rulings. No lights or flash characteristics. No chart/plotter integration.
No collision or gameplay effect. The marks ship as bobbing, correctly-faced furniture.

`motion()`, `surface()`, `respond()`, `riserPath()` and `lightOn()` are all **imported and untouched**
— a live sea solver, a mooring-chain polyline generator and a light-character clock that nothing in
the game calls yet. They are the kit's most interesting half and they are deliberately not wired.

## Reproducing the measurements

There is no `node` on this machine, so the sibling kits' `_verify.js` pattern does not run here. The
measurements above came from a standalone `net8.0` console app referencing the repo's **own**
ClearScript DLLs in place (`Assets/_Project/Plugins/Editor/JsEngine/`) — the same engine
`V8RigScriptHost` uses, so what it measures is what the baker measures. ~6 s for 700 renders against
~20 min for a cold Unity batch.

In-engine, the same numbers come from:

```
Hidden Harbours/Art/Measure Nav Buoy Kit (emit contract)
```

which re-derives all 70 cells and rewrites `navBuoyKit.contract.json`. The bake asserts against that
committed file and refuses when a cell stops reproducing.
