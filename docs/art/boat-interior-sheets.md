# Boat interior sheets — the bake record

**What this is.** The art half of ADR 0038: the rig-rendered cabin sheets that ADR 0038's runtime will
draw. The def half landed in #589 (`BoatInteriorDef`, the reader, the merge); this is the pixels.

**Scope: exactly the 24 hulls the intake cleared.** The S0 ledger
(`rigs/boat-interiors-intake/s0-verdicts.json`) refuses three of the drop's 27 — the two sport fishers
on an unstamped renderer pin, the Cape Islander on a forked rig — and the law is that **no sheet is
ever baked for a refused hull, and no def may exist for one either**. They join later, by their own
upstream PRs. `BoatInteriorSheetTests` asserts both halves of that: 24 sheets, and no refused hull
named anywhere, on disk or in the contract.

---

## What shipped

| | |
|---|---|
| Hulls | **24** — 1 lobster boat, 18 lobster variants, side dragger, 2 stern trawlers, coastal packet, tanker |
| Cells | **424** = Σ (levels × 8 facings); 2 levels on the lobster hulls, 3 on the five ships |
| Pages | **45** PNGs under `Assets/_Project/Art/Boats/Interiors/` |
| On disk | **9.83 MiB** of PNG (LFS-tracked by `.gitattributes`) |
| Bake time | **59.6 s** on the 4060, V8/ClearScript 7.5.1 |
| Contract | `Assets/_Project/Art/Boats/Interiors/BoatInteriors.json` |

Re-bake: **Hidden Harbours ▸ Art ▸ Bake Boat Interior Sheets**, then **Slice Boat Interior Sheets**.
Headless: `-executeMethod HiddenHarbours.Tools.RigBaking.BoatInteriorBakeMenu.BakeAndSliceCli`.

> ⚠️ **Both CLIs' exit codes mean something now.** `BoatInteriorDefBuilder.BuildAllCli` exits 1 on
> ANY refusal — which made it permanently 1 while the ledger held refusals (three, then one). Since
> the cape's rig merge cleared the last refusal (2026-08-27, the 27/27 ledger), a clean run exits 0
> and a nonzero exit is REAL. This one separates the two cases either way: a **ledger** refusal does
> not colour the exit code; a **cleared** hull that failed to bake does. The parseable line is still
> emitted: `N baked, M refused by the ledger, K cleared-hull failures.`

---

## The registration property, and how it is proven

The kit's own `cell.note`: *"One sheet per level per facing, baked to the full hull cell at the hull
pivot — composites under the exterior 1:1."* Everything downstream rests on that sentence, so the bake
proves it four ways rather than trusting it once.

1. **Four declarations of one cell must be one number.** The interior rig's `cellOf(hull)`, the KIT's
   exterior rig's `W/H/pivot`, the **repository's shipped** exterior rig's `W/H/pivot`, and the
   sidecar's `cell` block. All 24 agree exactly; any disagreement refuses the hull.
2. **The handedness is measured from a bearing** — see the next section.
3. **Containment, from pixels, every level and every facing.** The interior's ink must lie inside the
   exterior hull's own ink. 424 cells probed: **18 of 24 hulls at exactly 0 px** (all five ships, the
   12 m lobster boat, and every variant outside Northumberland); four Northumberland variants at 1 px
   and two at **3 px** — always the cuddy, always the bottom edge, always at the two aft diagonals,
   where the sole's forward section lip clears the hull's own outline. Tolerance 4 px, and it is a
   tight guard rather than a shrug: the
   *lateral* margins are never under 11 px, so a turntable off by one 45° step lands hundreds of
   pixels out and trips at the first diagonal.
4. **Every sprite is the full cell at the hull's one pivot**, asserted per sprite by the tests — not
   per sheet. A pivot that quietly reverts to the importer's default still imports, still counts
   right, and still shows a perfectly good cabin three pixels off its own boat for ever.

---

## ⚠️ The finding: the silhouette azimuth probe is WRONG on 18 of these 24 hulls

`RigAzimuthProbe.MeasureFromQuarterTurn` reads a principal axis out of a rendered outline. Fed the
lobster **variants** it returns **Clockwise**, at elongations of 2.3–3.2 — comfortably past the 1.5
threshold that would have produced a warning. It is wrong. The first bake of this kit was made on that
answer and **all eighteen variant sheets had every facing reversed**, with every individual cell still
a plausible cabin and every other check green.

The authority is the **un-squashed ground-plane bearing**, the same oracle
`RigCatalog.LobsterVariants.cs` records having used and sabotage-proved. Every one of these rigs
exposes `navMounts(dir, opts)` with a `port`/`star` pair that is a pure ±X separation at equal y and z.
Its screen displacement is `(Δxr·S, −Δyr·S·sin e)`, so dividing the screen Δy by `sin(elev)` **before**
taking any angle recovers the true ground bearing and `S` cancels. Step it through the eight headings:
**+45° per step is counter-clockwise, −45° is clockwise, and the answer is in the sign** — which a
silhouette's principal axis simply does not carry (an X-mirrored rig gives the same magnitude).

Measured on all eight exterior rigs: **+45.000°/step, deviation 0.000°, counter-clockwise, 8 of 8.**
The zero deviation is itself the proof the divisor is right — ÷1.0 gives 12.27°, sin 30° gives 7.12°,
sin 50° gives 5.00°, and only sin 40° returns 0.00, which independently confirms the elevation too.

The silhouette's verdict is now **recorded and not used**, and the bake reports when the two disagree.
`BoatInteriorSheetTests.SheetConvention_IsTheExteriorsMeasuredHandedness` re-takes the bearing in a
second, independent transcription and pins the contract against it.

## ⚠️ A second trap: the variant is spelled two different ways

The interior renderer takes the variant **nested** — `render(dir, {hull, level, variant:{size, style,
region}})` — and hands it whole to the exterior's `interiorEnv(v)`. The exterior rig's own
`render(dir, opts)` takes the three **at the top level** of its options. Each rig resolves each field
independently against its own table with a fall-through default (`standard`/`hardtop`/`northumberland`),
so handing either one the other's shape draws a **real, correct-looking boat that is not the one you
asked for**, with no error anywhere.

Measured: hand the eighteen variants their own joined stems as a string and all eighteen render **one
identical picture**, filed under eighteen names. The baker therefore builds both shapes from one triple
and takes that triple from the committed gameplay sidecar's `variant` node — a declaration, never a
split of the file stem.

Related and legitimate: **style is roof-only**, so the 18 variants carry **9 distinct interiors**
(hardtop and open share a house for a given size × region). That is the kit's own design, recorded here
so it is not mistaken for the fall-through above. Each variant still gets its own sheet, named for its
own hull, so the runtime never has to know the equivalence.

---

## The budget, and the one decision left open

The cells are **full hull cells**, uncropped, because that is what "composites under the exterior 1:1"
means and what the acceptance test asserts. That is cheap on disk and expensive in texture memory.

| hull | cell | levels | pages | RGBA32 |
|---|---|---|---|---|
| coastal packet | 2112×1760 | 3 | 12 | **340.3 MB** |
| tanker (16 px/m) | 1920×1600 | 3 | 6 | **281.3 MB** |
| stern trawler ×2 | 1344×1152 | 3 | 3 each | **159.5 MB** each |
| side dragger | 896×792 | 3 | 2 | 65.0 MB |
| lobster boat | 456×420 | 2 | 1 | 11.7 MB |
| lobster variants ×18 | 480×420 | 2 | 1 each | 12.3 MB each |
| **all 24 resident** | | | **45** | **1238.6 MB** |

**Nothing loads all 45 pages**, and ADR 0038 Proposal 3 draws exactly one level at a time, so the
figure that will actually be paid is per hull and per level — 12 MB for the boat the player owns, and
the packet and tanker are M3 ships that appear in no region yet. But the big-ship numbers are real and
they are the reason to state this rather than leave it to be discovered.

**The lever, measured, for whoever builds the runtime.** Cropping each level's cells to their
pivot-inclusive ink union would cost every consumer an offset and would break the "same cell" law as
written, so it is **not taken here** — it is an architecture call, not an art-pipeline one. Measured
per hull (the pivot lies inside every level's ink union, so the pivot-inclusive crop IS the bbox):

| | trawlers | dragger | lobster variants | lobster boat | tanker | packet |
|---|---|---|---|---|---|---|
| cropped cost | **16.5%** | 38.6% | 38.9% | 36.7% | 49.5% | **59.0%** |
| MB each | 159.5 → 26.3 | 65.0 → 25.1 | 12.3 → 4.8 | 11.7 → 4.3 | 281.3 → 139.3 | 340.3 → 200.6 |

Fleet total **1238.7 MB → 508.1 MB (41%)**. Note the shape: it helps the *trawlers* most and the
*packet* least — a room that swings around a 60 m hull's pivot sweeps most of the cell even while
inking 1.3% of its pixels, so there is little air left to crop.

## What is deliberately NOT baked

- **The door cue.** The kit's door is an 8-frame baked cue (`doorOpen = k/7`, ~70 ms a frame, reversed
  on exit). Baking it would multiply this kit by eight, and nothing plays it yet — ADR 0038 ships no
  runtime. Every cell is baked at the resting fraction, `doorOpen 0`, and the contract records it, so
  the sheets state what they are instead of leaving it to be inferred.
- **`night` / `lamp` / `weather` / `clutter`.** One state each, written into the contract rather than
  left to `resolve()`'s defaults. A default nobody wrote down is one nobody can reproduce.
- **The three refused hulls.** See the top of this file.

## Two pixel grids in one kit

The tanker renders at **16 px/m** where the rest of the fleet is 32 (ADR 0038 Proposal 2: *px/m per
def, builder refuses an omission*). The sidecar reader already refuses an absent `scale_px_per_m`; the
bake additionally refuses a sidecar whose px/m disagrees with the rig's own `cell.S`, and the slicer
imports **each sheet at its own hull's px/m as its pixels-per-unit**. At the fleet's 32 her cabins
would land at half scale — in focus, with nothing to catch it but the eye — which is why the tests
assert the importer's PPU per page and not just once for the kit.

The import cap is **4096**, not the usual 2048: one packet cell is 2112 px on its long side, and that
number is the exterior hull's cell, so the cap moves rather than the art. The bake cap and the import
cap are deliberately the same constant — between them a sheet bakes fine and imports silently
downscaled, with the sprite count still correct.
