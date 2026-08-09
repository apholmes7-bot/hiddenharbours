# Shipyard iso kit — what the repo MEASURED at import

The drop's own `README.md` sits beside this file, imported verbatim. **This file is the repo's, and
where the two disagree this one is the evidence.** Every figure here was measured by running the
committed rig in a standalone V8 host (Node 22, no DOM), never read off a README — the rig lane has
been burned by mislabelled drop documentation before (`../README.md`, the 2026-07-19 azimuth
correction; the shrub kit's catalogue keys not matching its README names).

Nothing in the rig source was edited. Two of the findings below are things the rig gets *wrong* by
current canon; both are recorded for an upstream fix rather than patched here, because rig sources
are the art director's and a local edit is silently reverted by the next drop.

---

## 1. What arrived, and what was actually imported

| the drop shipped | verdict |
|---|---|
| `shipyardIsoRig.js` (100,542 B) | **NEW family — IMPORTED verbatim.** No `shipyardIsoRig.js` existed in the repo. |
| `gameplay/shipyardIsoRig.gameplay.json` (32,500 B) | **IMPORTED verbatim** — generated contract for all 5 sites + 20 parts. |
| `harness.html` (6,987 B) | **IMPORTED verbatim.** ⚠️ It `<script src="hulls/…">`s the fleet rigs from a sibling `hulls/` folder that this kit deliberately does not have — see below. The repo's copies are two levels up in `../`. |
| `README.md` (11,693 B) | **IMPORTED verbatim**, with one marked correction block added at its head. |
| `hulls/` — 9 fleet rigs | **NOT IMPORTED. 8 are byte-identical to the repo's; the 9th is a REGRESSION.** See §4. |
| pre-rendered PNGs | **none in the drop.** Nothing for Git LFS; no `.meta` files needed. Bakes remain the coordinator's. |

## 2. Azimuth — measured, and the sign calibrated against a known sibling

**Result: `counterClockwise`.** Registered that way in `RigCatalog`.

The rig carries `th = dir*Math.PI/4` — a **positive** sign. Per `../README.md`'s 2026-07-19
correction that sign is **not admissible evidence**, so it was ignored.

Measuring the screen angle of the model `+X` axis per dir step gives **+46.7526°/step**, whose
*magnitude* matches the family figure (46.75) to the digit but whose *sign* is the opposite of the
`−46.75` recorded for `houseIsoRig` / `wharfBuildingRig` / `interiorIsoRig` / `wharfIsoRig`.

That apparent conflict is **an artifact of the probe, not a property of the rig.** Running the same
probe against `wharfIsoRig` — already measured and registered `CounterClockwise` — returns the
*identical* figure:

| dir | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
|---|---|---|---|---|---|---|---|---|
| `WharfIso.project()` | 0 | 32.7324 | 90 | 147.2676 | 180 | −147.2676 | −90 | −32.7324 |
| `ShipyardIso.project()` | 0 | 32.7324 | 90 | 147.2676 | 180 | −147.2676 | −90 | −32.7324 |

Agreement to 4 dp at **every one of the 8 dirs**. The two rigs' `camBasis`/`projRaw` are the same
formula character-for-character (as are `doryIsoRig`'s and `houseIsoRig`'s). The shipyard rides
**one turntable with the wharf kit and the fleet** and inherits its convention.

> **The lesson worth keeping:** a probe's sign is its own convention. What settles handedness is
> **calibrating the probe against a rig already measured** — not the sign it prints, and not the
> `th = ±dir·π/4` term. A cross-check that had asserted "positive ⇒ clockwise" would have registered
> this family mirrored and flipped all 25 keys at once.

Pixel evidence agrees independently: the `wet` mask centroid (the rig's own submerged structure,
which by the axis contract sits on the water side, `+y`) sweeps the same way, at 47.0–49.6°/step
across `backyardSlip` / `smallYard` / `workingYard`.

## 3. Determinism, and the facing count from pixels

- **Two cold V8 runs, byte-identical: 208 cells.** All 20 parts and all 5 sites × 8 dirs, plus the
  `tide` (0 / 0.45 / 0.9 / 1.8) and `variant` (0 / 1 / 2 / 7) paths — the places hidden global
  randomness would surface. No drift.
- **8 distinct facings, counted from rendered pixels**, for every part and site tested: all 8 dir
  hashes differ, so the turntable is real and not 4 facings labelled as 8.
- **Key guard: all 25 keys resolve** (`resolve()` non-null for every site and part id). This is the
  guard that matters most on an import — a missing key does not throw, it renders *the wrong thing*
  silently, and only a cell-size mismatch would ever expose it.
- The rig's catalogue keys **do match** its README's names (20 parts, 5 sites) — the shrub-kit
  failure mode did not recur here.

## 4. The `hulls/` folder was refused, and one file in it is a regression

The drop bundles the 9 fleet rigs the yards composite. Byte-diffed against the repo, file by file:

| dropped file | vs repo |
|---|---|
| `coastalPacketIsoRig.js` · `consoleIsoRig.js` · `doryIsoRig.js` · `lobsterBoatIsoRig.js` · `puntIsoRig.js` · `sideDraggerIsoRig.js` · `sternTrawlerMk2IsoRig.js` · `tankerIsoRig.js` | **byte-identical (8 of 9)** — nothing to import |
| `capeIslanderIsoRig.js` | **DIFFERS — and the drop's copy is OLDER** |

The Cape Islander diff is not cosmetic. The repo's copy runs her washboards along the **full sheer,
transom to foredeck bulkhead**, with an `xin()` clamp keeping them clear of the house wall, and cites
the ruling that put them there:

> `// owner 2026-07-22: "capes washboards go all the way to foredeck"`

The drop's copy is the **pre-ruling** version — washboards stopped at the house front
(`if(station(u0).y > HY0-0.05) continue;`). The repo's version landed in **#420**. Importing the
drop's `hulls/` wholesale would have silently reverted an owner ruling on a hull that has nothing to
do with shipyards, and no test would have caught it — this is the #454 lesson repeating, and the byte
diff is the only thing that catches it.

**So the whole `hulls/` folder is refused.** The 8 identical files are already in the repo, and the
9th must not overwrite it. `harness.html` expects them at `hulls/*.js`; in this repo they live at
`../` (e.g. `../doryIsoRig.js`), so a browser run needs the paths adjusted or a symlink — the rig
itself needs no hulls at all (`hullRigs()` reports which globals are present and skips the rest).

## 5. Two things the rig gets wrong by current canon

**G3 — the keyline is unconditional.** The rig bakes the 1 px `#1a1c22` ring with **no
`{outline:false}` gate anywhere in the source** (`out[i] = KEY;`, one call site, ungated), and its
README advertises it as a feature. **ADR 0031** (accepted 2026-08-05) retired the keyline as the
world-art default, and the four `iso-rig-pack` families all carry `KEYLINE_DEFAULT = false`. This
family arrived *after* that ruling with the ring hard-on. Recorded in the contract as
`keylineDefault: true` because that is what it *does*; the fix belongs upstream.

**G1 — there is no interior.** `opts.ghost` x-rays the buildings; that is a massing aid, not a room.
The existing `InteriorIso` rig cannot stand in either — **measured** at Wd 6.0–8.4 m × Ln 7.0–11.2 m
(a domestic range), against the NMC workshop's 13.0 × 6.6 m. The 13.0 m length is **outside its
parametric reach**, so it cannot be made true-to-footprint. Gameplay data for the interior is
authored in full (`gameplay/shipyardIsoRig.nmc_yard.gameplay.json`); only the art is missing.

**G2 — no hull undersides.** Covered in the sidecar's `_gaps`. Short form: berths composite the
fleet's existing bakes **upright**, positioned by keel bottom, so no pose shows a hull's bottom.
Nothing was registered here as a hull-underside state — boat rigs are their own family. The path that
*does* exist: the hull rigs already model the underside (`doryIsoRig`: "bottom (underside)") and
already honour `roll` / `pitch` / `heave` — all three verified to change the render — so a keel-over
pose belongs on the boat rigs, and grounding inherits from there.

## 6. Cells and sheet plans — every one measured, every one under the cap

Cells follow `wharfIsoRig`'s rule exactly (**pivot-aligned union of the returned BUFFER extents**
across all 8 facings — not the ink bbox). Pivot per **ADR 0026**, `(H−pivotY)/H`, unchanged and not
re-investigated. Full table: `Assets/_Project/Art/Sprites/Shipyard/Iso/shipyardIsoRig.contract.json`.

**No sheet fits 8 facings on one row.** All 25 keys pack into 3×3 or 2×4 grids instead. The cap is
**4096** (Unity's hard limit, which `SpriteSheetSlicer` lifts to from the manifest) — and since
*every* sheet here exceeds Unity's **default 2048**, that lift is **mandatory** for all of them or
they import silently downscaled with every slice rect landing wrong.

Three sites cannot hold 8 facings under the cap at their own `fitScale()`, so they step down the
rig's own ladder; the contract records the scale that actually fits:

| site | fitScale says | contract bakes at | cell | grid | sheet |
|---|---|---|---|---|---|
| `workingYard` | 32 | **16** | 959 × 692 | 2×4 | 1918 × 2768 |
| `largeYard` | 24 | **12** | 1334 × 923 | 2×4 | 2668 × 3692 |
| `industrialYard` | 16 | **8** | 1332 × 901 | 2×4 | 2664 × 3604 |

Worst by longest side — **size import headroom off this, never off worst-by-area**:
`largeYard`, **3692 px, 404 px of headroom** to the 4096 cap.

NMC's own yard, `smallYard`: cell **1230 × 953**, pivot **(614, 534)**, 3×3 → **3690 × 2859**.

## 7. Canon alignment (measured against the ruling, not assumed)

The drop's tier ladder lands on the owner's ruling without adjustment:

| ruling (vision-and-pillars.md §5.3) | rig site | tier |
|---|---|---|
| NMC **small** | `smallYard` | 2 |
| East Point **working-fleet** | `workingYard` | 3 |
| Finnigan's Landing **large** | `largeYard` | 4 |

`backyardSlip` (tier 1) and `industrialYard` (tier 5) are spare capacity the ruling does not name.
Only `shipyard.nmc_yard` is registered in the catalogue — a catalogue key implies a placed, named
business, and only NMC has been ruled and scoped.

**No indoor berth exists below tier 4** (measured: `largeYard` and `industrialYard` each publish one
`indoor` mount, tiers 1–3 publish none). So NMC's yard has no sheltered berth to inherit, which is
part of why its interior had to be authored rather than imported.
