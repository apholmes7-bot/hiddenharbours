# Fleet flotation — resting drafts and watertight clamps for the rig-pack hulls

**What this is.** The hand-authored gameplay half of twenty-one incoming hulls: `RestingDraftMeters`,
`WatertightDeckHeightMeters` and `WatertightHalfBeamMeters` for the 18 lobster-boat variants, the two
coast-guard RHIB builds and the reshaped sport skiff. The baker never writes these three fields — that
is what lets them survive a re-bake (`RigMeshAssetBaker`, and the `HullMeshDef` field docs) — so they
have to be derived and written by hand, once, against a method somebody can check.

**Status.** The numbers below are authored and pinned by `FleetFlotationTableTests`. They are **not yet
on any asset**, because no `HullMeshDef` exists for these hulls yet: `lobsterBoatVariantsIsoRig.js` is
still in `HullMeshFleet.NotHulls`, and neither the zodiac rig nor the sport-skiff v2 rig is in the repo
at all. When the bake lands, it writes these values into the defs it creates and the coverage test in
that fixture stops being inert.

---

## 1. What the field actually controls

`HullMeshDef.RestingDraftMeters` is a **design waterline drawn exactly** — how far up a hull's planking
the sea stands at rest, in metres above the rig origin. Every boat rig's pivot is the keel bottom
("amidships, keel bottom, centreline"), so it doubles as her resting draft. `HullSettleMath`
pre-divides by the iso projection gain, so the number typed here is the number the sea draws.

**It is not a hydrostatic result, and the shipped fleet proves it.** Solving a crude block displacement
for each committed hull and comparing against her authored draft:

| hull | LOA | mass kg | authored draft | hydrostatic T\* | ratio |
| --- | --: | --: | --: | --: | --: |
| dory | 4.5 | 400 | 0.11 | 0.14 | 0.80 |
| punt | 5.2 | 700 | 0.19 | 0.18 | 1.06 |
| sport skiff | 7.0 | 950 | 0.19 | 0.13 | 1.41 |
| console skiff | 7.0 | 1200 | 0.21 | 0.17 | 1.24 |
| lobster boat | 12.0 | 6800 | 0.50 | 0.33 | 1.53 |
| cape islander | 12.9 | 6000 | 0.53 | 0.25 | 2.12 |
| side dragger | 25 | 90000 | 1.10 | 1.00 | 1.10 |
| stern trawler | 38 | 316000 | 1.60 | 1.52 | 1.05 |
| trawler Mk2 | 38 | 330000 | 1.63 | 1.59 | 1.03 |
| coastal packet | 60 | 1244000 | 1.90 | 2.40 | 0.79 |
| tanker | 110 | 7668000 | 2.47 | 4.40 | 0.56 |

The ratio swings 0.56 → 2.12. There is no physics being honoured here and there should not be: these
are look numbers, tuned so a hull reads right on screen. **Do not "correct" the fleet toward
hydrostatics.**

What *is* consistent is the ladder. Across the 12–38 m band — four different hull types — draft/LOA
sits in **0.041–0.044**. That band is the calibration target for anything landing in it, and every
hull in this document does.

## 2. The invariant that decides the hard cases

> **`RestingDraftMeters` ≤ `WatertightDeckHeightMeters`.**

The clamp bounds the drawn waterline so the sea can never climb past the lowest open interior surface.
If the design waterline is *above* that line, the boat is authored to float with her cockpit sole
underwater and the clamp spends its life fighting the draft. Nine of the eleven committed hulls satisfy
it; the dory and punt are the exceptions, and both are open flat-bottomed boats whose "deck" is a
floorboard essentially at the waterline. Every hull in this document satisfies it.

---

## 3. The 18 lobster variants

### The anchor is exact

`lobsterBoatIsoRig.js` — the shipped `hullmesh.lobster_boat_iso`, `RestingDraftMeters` 0.50 — carries
`L = 12.0`, `DECK = 0.50`, `RAKE = 0.50` and an offsets table **byte-identical** to the variants rig's
`northumberland` table. So `standard/*/northumberland` **is** her, and her three authored values pin the
whole family. Every derived number below reproduces hers exactly at that cell.

### Style is not a hull axis — measured, not assumed

All 18 sidecars report identical `loa`/`beam`/`depth`/`freeboard`/`sole`/sheer half-width within each
`(size, region)` pair. The style axis (`open` / `hardtop`) changes the roof cantilever and the arch, not
the planking. **9 distinct hull-metric sets, 18 hulls** — so `open` and `hardtop` of the same
size+region share one row.

### Draft scales with the rig's own depth scalar

The rig scales every vertical hull dimension by `dep` (`inshore` 0.88, `standard` 1.00, `offshore` 1.10)
and sets `DECK = 0.50 · dep`. Taking the draft down the same scalar makes all three sizes float at the
same proportional point on their own planking, and reproduces the anchor exactly:

```
draft = 0.50 · dep   →   inshore 0.44 · standard 0.50 · offshore 0.55
```

### ⚠️ Region deliberately gets NO draft delta — and here is the measurement

Region *is* a genuine hull-form axis: at 12 m the bottom half-width amidships runs Fundy 1.44,
Northumberland 1.50, Newfoundland 1.58, so a Fundy boat really would sit deeper than a Newfoundland one
at equal displacement. Solving displacement over each region's own station table, at a mass held
constant per size and an effective density calibrated on the anchor:

| size | Northumberland | Fundy | Newfoundland | spread |
| --- | --: | --: | --: | --: |
| inshore | 0.440 | 0.469 | 0.410 | 59 mm |
| standard | 0.500 | 0.533 | 0.466 | 67 mm |
| offshore | 0.550 | 0.586 | 0.513 | 73 mm |

Two facts kill it:

1. **It is one pixel.** The worst deviation from the size-scalar answer is 37 mm. At 32 px/m that is
   **1.2 px of drawn waterline** — below the threshold at which anyone could adjudicate it by eye.
2. **It breaks the §2 invariant.** Fundy at every size lands *above* her own cockpit sole (0.533 vs a
   0.50 sole at standard), so she would be authored to float with her working deck under water and the
   watertight clamp would fight her draft for the life of the boat.

So the region axis changes the picture of the hull and not the height she floats at. Recorded here
because it was asked and measured, not waved off.

### The table

`WatertightDeckHeightMeters` is the rig's own `DECK` constant (`0.50 · dep`), confirmed against each
sidecar's `hull.sole` and its cockpit `DECK` polygon z. `WatertightHalfBeamMeters` is the hull's true
maximum sheer half-width carried out by the margin the committed lobster boat already uses
(2.50 / 2.220 = 1.126) — generous is a touch drier, too small re-opens far-rail flooding.

| size / region | draft | deck | half-beam | true half-beam |
| --- | --: | --: | --: | --: |
| inshore / northumberland | 0.44 | 0.44 | 2.00 | 1.776 |
| inshore / fundy | 0.44 | 0.44 | 1.95 | 1.728 |
| inshore / newfoundland | 0.44 | 0.44 | 2.04 | 1.808 |
| **standard / northumberland** | **0.50** | **0.50** | **2.50** | 2.220 |
| standard / fundy | 0.50 | 0.50 | 2.43 | 2.160 |
| standard / newfoundland | 0.50 | 0.50 | 2.55 | 2.260 |
| offshore / northumberland | 0.55 | 0.55 | 2.85 | 2.531 |
| offshore / fundy | 0.55 | 0.55 | 2.77 | 2.462 |
| offshore / newfoundland | 0.55 | 0.55 | 2.90 | 2.576 |

The bold row is the anchor: it must equal the committed `LobsterBoatIsoHullMesh.asset` field for field,
and `FleetFlotationTableTests` asserts exactly that against the shipped asset.

Every half-beam clears its own cell: the variants bake at 480 px / 32 px per m, so the fixture's
`halfBeam < cellW/2` bound is 7.5 m against a worst case of 2.90.

---

## 4. Coast-guard zodiac — two builds

Her sidecar carries `derivedFromRigSha256` and it **matches her own rig exactly**, so her geometry is
trustworthy as supplied.

She is a chined deep-V with an inflated collar swept along the sheer. Landmarks, from the rig's own
loft (`hullHalf()`), in metres above the keel:

| build | LOA over tubes | sole | chine (amid) | sheer (amid) | collar underside |
| --- | --: | --: | --: | --: | --: |
| hurricane | 7.28 | 0.420 | 0.600 | 0.810 | 0.540 |
| frc | 6.66 | 0.403 | 0.576 | 0.778 | 0.508 |

**Rest state, and what `plane(t)` does to it.** She carries a planing pose in her rig
(`hump_pitch 11.5°`, `cruise_pitch 6.6°`, `lift_px 9` — 0.28 m of lift at 32 px/m). That is a **pose the
presenter applies on top of the datum**, not a second draft: `RestingDraftMeters` is documented as where
the sea stands "when she is floating **at rest**", and the field has no throttle input. So her draft is
her at-rest draft and planing lifts her out of it, which is the correct RHIB behaviour and needs no new
field.

Drafts are set at the console skiff's draft/sole ratio (0.750) — the nearest small-planing-boat
precedent in the committed fleet — and cross-check cleanly on both other axes:

| build | draft | deck | half-beam | implied mass | draft/LOA |
| --- | --: | --: | --: | --: | --: |
| hurricane | 0.32 | 0.42 | 1.61 | 1035 kg | 0.0440 |
| frc | 0.30 | 0.40 | 1.52 | 804 kg | 0.0450 |

Both land in the fleet's 0.041–0.044 band, both leave the collar 0.21–0.22 m clear of the water at rest
(right for a RHIB: the tube kisses the sea when she rolls, it does not carry her), and both satisfy §2
with ~0.10 m to spare. Half-beam is measured over the **tubes** (the sidecar's `beam_over_tubes_m`),
carried out by the small-boat margin 1.25/1.15.

---

## 5. Sport skiff v2 — she does not inherit

**⚠️ Her pack sidecar is stale and would have told you she does.** `sportSkiffIsoRig.gameplay.json`
pins `derivedFromRigSha256 = 03c6755a…`, which is the LF hash of the **committed** 366-line rig, not of
the 1025-line v2 beside it. It reports a 0.28 sole; the v2 rig says `DECK = 0.46`. Deriving her from
that sidecar produces the committed skiff's numbers and looks perfectly reasonable.

Read off the v2 rig itself, she is a different hull at the same 7.0 m:

| | committed | v2 |
| --- | --: | --: |
| max beam | 2.30 | 2.54 |
| sheer (amid) | 0.62 | 1.10 |
| sole (`DECK`) | 0.28 | 0.46 |
| section | soft-chine trapezoid | chined deep-V, 0.04 keel-pad half-width |

Her draft is the equal-mass hydrostatic answer against her own predecessor: calibrate an effective
density on the committed skiff (950 kg at her authored 0.19 m), then float the v2's section at the same
mass and the same density. She comes out at **0.363 m, +173 mm** — which is what a deep-V with almost no
waterplane down at the keel does. She is heavier than 950 kg in reality, so this is if anything shallow.

| | draft | deck | half-beam | draft/sole | draft/LOA |
| --- | --: | --: | --: | --: | --: |
| sport skiff v2 | 0.36 | 0.46 | 1.38 | 0.789 | 0.0519 |

`draft/sole` sits between the console skiff (0.750) and the stern trawler (0.914). `draft/LOA` runs
above the 12–38 m band, which is expected: the band is a displacement-hull ladder and she is a deep-V
at the small end. **The reshape justifies the delta; she is a second hull under a new id and the
committed skiff keeps her own numbers untouched.**

---

## 6. Proposed ids, and what is provisional

Ids follow `hullmesh.snake_case` with the fleet's `_iso` suffix and are **append-only once baked**:

- `hullmesh.lobster_{size}_{style}_{region}_iso` × 18
- `hullmesh.zodiac_hurricane_iso`, `hullmesh.zodiac_frc_iso`
- `hullmesh.sport_skiff_mk2_iso`

⚠️ **The ids are a proposal; the numbers are not.** The bake PR owns the ids. The table is keyed by
`(size, style, region)` / build, which is unambiguous, so if the bake picks different ids only the key
column moves.

⚠️ **Three hulls are provisional in a way the lobster variants are not.** The zodiac and sport-skiff-v2
rigs are in the fleet rig pack and not in `docs/art/rigs/`, so nothing here can be verified against a
committed source until art-pipeline imports them. Their derivations are recorded above in full so that
import is the only outstanding step. The 18 lobster variants rest entirely on committed sources.

## 7. What drafts do not fix

Hulls visually sitting too low was diagnosed as **iso gain plus animator phase**, not draft. If a hull
looks wrong in the water at the number above, measure before touching it — the draft may be right and
the presentation at fault.
