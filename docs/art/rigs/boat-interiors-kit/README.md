# Boat interiors — export kit (tranches 1–4, COMPLETE, 2026-08-19)

Contents map to the coordinator brief (§1–§7):

1. **One rig source**: `boatInteriorRig.js` here IS the renderer — LF sha256
   `34bb781303b9de9a47c7029a685bf83d357adefe2ffb95ff54451233ea7c46a7`.
   Nothing stale beside it; every sheet must colour-census against this file. Tranche 4 revised the
   rig again (sport-fisher kind + variant-aware envs + parametric variant layout), so ALL sidecars
   were regenerated against it.
2. **Per-hull sidecars, hull-stem named**: `<hullStem>.interior.json` ×9 at the kit root
   (capeIslanderIsoRig, lobsterBoatIsoRig, sideDraggerIsoRig, sternTrawlerIsoRig,
   sternTrawlerMk2IsoRig, coastalPacketIsoRig, tankerIsoRig, sportFisherIsoRig2.convertible,
   sportFisherIsoRig2.skybridge) + ×18 in `lobsterBoatVariants/`
   (lobsterBoatVariantsIsoRig.<size>_<style>_<region>). Each declares fits_hulls, FOOTPRINT,
   WALKABLE (per level, obstructions listed) and every anchor. The lobster stem-mapping question is
   CLOSED: the 18 variants carry their own stems; the canonical file notes it. New stems
   (Mk II, packet, tanker, both sports, all variants) carry `_stem_note` for intake.
3. **THRESHOLD declared**, camper schema verbatim — hinged hulls (dragger, trawlers, packet, tanker)
   carry hinge_axis + swing with the swept-arc keep_clear; sliders (lobster, cape, both sport
   fishers, all 18 variants) carry a `slide` block in swing's position (no hinge exists; flagged in
   the note). The sport-fisher slider is glass, two-panel, sill at MEZZANINE height — walk in level
   off the mezz deck; clear width is the leaf, parking over the fixed pane. Door mechanism:
   **8-frame baked cue** (doorOpen = k/7, ~70 ms/frame), played reversed on exit — `door_cue` per door.
4. **INTERACT as data**: id · action · at? · reach_point · visible_facings · _note naming the shipped
   mechanism — helm→existing helm (enter_helm), bunk→InteriorBed (sleep), locker→InteriorWardrobe
   (storage), stove→camper stove (cook). **Stair routes are not INTERACT entries** — see the merge
   rule under Corrections. The sport fishers carry MULTIPLE
   control points: the 53 a rendered lower helm on a raised HELM DECK against the salon windshield
   (DECK helm_deck + STAIRS lounge_to_helm_deck) plus the flybridge pod and tower control head;
   the 90 a rendered helm on the SKYLOUNGE — a full deck dedicated to the helm (DECK sky_sole,
   its own slider onto the aft deck as THRESHOLD.additional sky_entry) — plus the coaming pod.
   Exterior LADDER legs are baked and declared: 53 mezzanine→flybridge; 90 mezzanine→aft deck
   →coaming.
   Ids are append-only from this file forward.
5. **Cell/pivot stated in-file**: `frame` + `cell` blocks per hull (m, px/m stated, origin
   amidships/keel/centreline, +x stbd +y bow +z up, heading-independent, facings ×8, levels).
   Tanker works at 16 px/m (scale ×2 in-engine); sports own big cells (820×770 / 1200×1170);
   variants share one 480×420 cell, pivot (240,232), all 18.
6. **derivedFromRigSha256** on every sidecar = LF sha256 of boatInteriorRig.js. `hullRigSha256`
   additionally pins the exterior rig each set of rooms was measured against.
7. **Scope — the fleet is COVERED**: tranche 4 adds the two sport fishers (salon + staterooms flat;
   the open bridge and all cockpit/foredeck surfaces stay the EXTRACTOR's; the 53's interior
   companionway salon→bridge_sole CLOSES the extractor's "no route up" finding; the 90's skylounge
   is dressed behind one stair trunk, flagged; PRIZE-BOAT FIT-OUT — the sport interiors read
   nothing like the workboat cabins: fitted carpet with teak margins, gloss-teak joinery, stone
   counters, cream-leather settees and quilted headboards, chrome hardware, table lamps; below
   deck the ensuite heads are authored WALL compartments in WALKABLE, with engine room, forepeak
   and the 90's crew cabin carried as _excluded abstractions) and all 18 lobster variants (variants rig pass 4:
   the aft doorway is a real sliding door on every variant; house/cuddy/loft published per variant;
   ONE parametric interior arrangement measured off each variant's published house). Motion (§2):
   interiors ride the hull's rock(i) — same camera basis, registration holds mid-wave; nothing
   assumes a level floor; comfort clamp compatible.

Sheets bake per level per facing into the full hull cell at the hull pivot. Hull-side gameplay
files (`Art/gameplay/*.gameplay.json`, variants in `Art/gameplay/lobsterBoatVariants/`) carry the
same sections merged into each hull's geometry, double-stamped (hull sha + interior sha), with
`_supersedes` chains kept. Sport-fisher merges preserve the extractor's own sections untouched.

## Corrections against the first drop (2026-08-19)

**1. Mixed renderer hash — FIXED, stamp only.** 25 of the 27 sidecars (and their merged gameplay
files) pinned `560aa92e…`, which was never the renderer in this kit. Cause: the export tooling
memoised each rig's source text and hash for the life of the harness page, so revising the rig
mid-session stamped later sidecars with the hash of the bytes fetched *before* the edit while the
geometry came from the hot-reloaded rig. The two sport fishers were exported after a reload and so
carried the true hash. The tooling no longer caches (`Art/_sidecarExport.js`): every stamp is
re-fetched and re-hashed at write time.

Before re-stamping, all 27 sidecars were regenerated headlessly from the renderer as shipped and
deep-diffed against the files that went out: **zero content differences — the only differing field
was `derivedFromRigSha256` itself.** The measurements were always tranche-4; only the provenance
field lied. That is why this was a re-stamp and not a re-measure.

**2. Merge rule — RULED: INTERACT is object interactables only.** Stair routes are declared in
`STAIRS` (endpoints, opening, rise, treads, direction, and their own `mechanism: InteriorStair`) and
are deliberately absent from `INTERACT`. Listing them in both was two spellings of one fact, and the
two generators had drifted into disagreeing about which spelling counted. The engine makes a
stairwell pressable from `STAIRS`, as land interiors already do.

`THRESHOLD/STAIRS/LADDER/INTERACT replace wholesale` stands unchanged as the merge rule — it now
succeeds because both files agree. Note this cost 27 file rewrites, not zero: the drop was in the
*merged gameplay* files, so the interior sidecars are the ones that changed. **34 stair entries were
removed** (`companionway` ×20 on the wheelhouse boats and variants, `companionway_up` +
`companionway_down` across 7 ships and sport fishers (14 entries)). `mechanism_map` loses its
`companionway` key; each sidecar now carries `_interact_scope` stating the rule in-file.

This retracts ids from a set §4 calls append-only. That is deliberate and is safe only because
nothing has adopted them yet — the ids never reached the repo. Append-only resumes from this drop.

**3. Cape Islander — NOT a stale rig. Two divergent branches, and neither is a superset.**

The repo's current `docs/art/rigs/capeIslanderIsoRig.js` was supplied and verified byte-exact
(33,162 bytes, LF sha256 `92c3061b69fb5e29f537e2bfd40bbe9b5863f83252452af82c1a97989d0a14ac`).
It is not a newer version of the rig in this kit. The two are parallel PASS 2 branches of the same
PASS 1 hull, developed against different briefs and never merged:

| | repo branch | this kit's branch |
|---|---|---|
| PASS 2 subject | **COLOURWAYS** | **THE DOOR AND THE PUBLISHED LOFT** |
| OKLCH mixer, `SCHEMES`, `palette()`, `rampFrom`, `chipWall` | yes | no |
| #247 washboards run the full sheer, clamped to house half-width | yes | no |
| `DOOR`, `doorFaces()`, `doorMount()` — the real aft opening + sliding leaf | no | yes |
| `HOUSE`, `loft`, `halfAtZ()`, `sheerZ()` — the published loft | no | yes |

**Adopting the repo file as the hull rig would remove the Cape Islander from this kit.** The whole
interiors contract measures each room from `<Hull>.loft` and `<Hull>.HOUSE` rather than re-deriving
the hull; the repo branch publishes neither, so `hullEnv('cape')` resolves to nothing and she has no
interior at all. Adopting ours reverts #247 and the colourways. Neither file can be taken whole.

**Her interior geometry does not move either way.** Every input the room is measured from is
byte-identical across the two branches — the `T` offsets table, `L`/`TH`/`DECK`, `NSEG`, the house
envelope `HX`/`HY0`/`HY1`/`HZ0`/`HZ1`, `ROOFZ`, `SOLE_U`, `station()`, `skin()`, `dfrac()`, and
`FYb`/`FYt` (2.54 / 3.10 in both; the only textual difference is a comment and whitespace). #247
moved the exterior washboard deck surface, which the interior rig never reads. So there is nothing
to re-measure: the finding is real, but its remedy is a rig merge, not an interior pass.

**What this drop contains for her, and what it needs.** Her sidecar is generated against the
loft-publishing branch (`hullRigSha256.capeIslanderIsoRig` =
`a3be1d618091695949a1d7ddb74fe7e796fe87f3c55665afdd699c52488c75d5`) and her numbers are correct for
both branches. **Do not adopt `hull-rigs/capeIslanderIsoRig.js` from this kit** — it is our branch
and would revert #247. The rig merge (repo as base, door + published loft re-applied on top) is
pending an owner call; it produces a third sha that has to land in the repo, not just here.
