# Architecture Decision Records — index

One file per decision, numbered and append-only. **Status** is the line inside each ADR; this
table mirrors it and was reconciled on 2026-09-01 (four ADRs that had shipped and been acted on
for weeks still read "Proposed" — flipped with a dated note inside each; one superseded).
When you add an ADR, add its row here in the same PR.

| # | Title (short) | Status |
|---|---|---|
| 0001 | Game engine: Unity (pinned at 6000.5.0f1 — see amendment) | Accepted |
| 0002 | Procedural vs handcrafted world | Accepted |
| 0003 | Data-driven content via ScriptableObjects | Accepted |
| 0004 | Perspective & scene strategy | Accepted |
| 0005 | Platform target: PC-first, mobile as a later port | Accepted |
| 0006 | Boat art pipeline: pre-rendered 3D → sprite sheets | **Superseded** by 0021 / 0022 |
| 0007 | Active-boat heading seam (`IActiveBoatService`) | Accepted |
| 0008 | Save schema v1 & versioning | Accepted |
| 0009 | Tidal exposure + region display-name seams | Accepted |
| 0010 | Water rendering: layered height-map-driven shader | Accepted |
| 0011 | Committed, hand-authored scenes (the hybrid) | Accepted |
| 0012 | Shoreline rendering | Accepted |
| 0013 | Deterministic 24-hour dynamic lighting | Accepted |
| 0014 | Painted seabed-height authoring | Accepted |
| 0015 | Water palette guard-rail | Accepted |
| 0016 | Additive 2D lights (+ 2026-08 amendments: beam relief, the lamp array) | Accepted |
| 0017 | Weather-driven water palette | Accepted |
| 0018 | One shared deterministic wave field (C#/HLSL twins) | Accepted (flipped 2026-09-01; shipped #147/#152/#313, owner feel verdict 2026-07-31) |
| 0019 | Hand-authored scenes are the source of truth: CREATE once, REFRESH | Accepted (flipped 2026-09-01; every committed scene is built this way) |
| 0020 | World-placed object persistence (`PlacedTraps`) | Accepted (flipped 2026-09-01; merged 2026-07-06, schema v2→3) |
| 0021 | Bake the art director's rigs in-engine (embedded V8) | Accepted |
| 0022 | Large boat hulls become real-time 3D meshes | Accepted |
| 0023 | The water becomes a displaced surface | Accepted |
| 0024 | Fishing characters draw as facet meshes | Ratified (scope extended by the owner) |
| 0025 | Diegetic UI rigs: runtime rendering | Accepted |
| 0026 | Rig pivot conventions | Accepted |
| 0027 | The water realness pass (ten techniques) | Accepted |
| 0028 | Splat-shaded ground | Accepted |
| 0029 | Character colour is runtime, structure is baked | Accepted |
| 0030 | Per-hull instrument ownership | Accepted |
| 0031 | The keyline retires | Accepted |
| 0032 | Y-sort band re-based to the region | Accepted |
| 0033 | One depth unit: the hull frame's y→z shear | Implemented |
| 0034 | A facing is a ground bearing | Accepted |
| 0035 | A `Vehicles` module | Accepted |
| 0036 | A second storey is a second interior layer | Accepted (amended 2026-08-23) |
| 0037 | The save carries a rest anchor | Accepted |
| 0038 | Boat interiors: a cabin is a level that rides | Accepted (rendering half retiring under 0041) |
| 0039 | The quiet HUD | Accepted |
| 0040 | Waves that break: lip, barrel, pocket, whitewater | Accepted (flipped 2026-09-01; #675/#680/#682 merged, two owner rulings 2026-08-28) |
| 0041 | Full mesh interiors: the room becomes geometry | Accepted — rolling out |
| 0042 | The squash is an art fact: the world plane vs the bake projection | Accepted (ruled 2026-08-29; the station kit migrated in the same PR) |

**Conventions.** `Proposed` = awaiting the named decider; `Accepted` = ratified (by the owner where
the ADR says so, otherwise by `lead-architect` on merge); `Implemented` = accepted and the code
landed in the same PR; `Superseded` = kept for the record, points at its successor. An ADR whose
PR merged and whose rulings were taken is Accepted in fact — flip the line, date it, keep the
original wording underneath so the history reads.
