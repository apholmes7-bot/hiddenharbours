# Boat interiors — export kit (tranches 1–4, COMPLETE, 2026-08-19)

Contents map to the coordinator brief (§1–§7):

1. **One rig source**: `boatInteriorRig.js` here IS the renderer — LF sha256 `560aa92e28d27719577ed07efec212ede9b33f58636fe5770836f8f729d260ae`.
   Nothing stale beside it; every sheet must colour-census against this file. Tranche 4 revised the
   rig again (sport-fisher kind + variant-aware envs + parametric variant layout), so ALL sidecars
   were regenerated against it — earlier hashes are superseded, not mixed.
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
   (storage), stove→camper stove (cook), companionways→InteriorStair. The sport fishers carry MULTIPLE
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
Nothing remains queued.
