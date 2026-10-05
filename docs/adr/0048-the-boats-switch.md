# ADR 0048 — The boats switch: each hull's rig carries its room, its cut and its walk

- **Status: PROPOSED** (2026-09-27). The owner ruled the switch itself on 2026-09-26 (§1). Merging
  this ADR accepts how it is done. Three decisions stay the owner's and are open (§11): ghost or gate,
  drops 15 and 16, and where the sloops go. Batch 0's intake records the rulings and flips this line.
  `lead-architect` records.
- **Date:** 2026-09-27
- **Decision owner:** the owner. `lead-architect` owns the Core contracts the switch touches and records
  this ADR. The other roles:
  - **Claude Design (CD)** and **`art-director`** own the rigs, the walk sidecars and the spec (§8);
  - **`art-pipeline`** owns the bake and the facet shader;
  - **`tools-editor`** owns the walk reader and its importer;
  - **`gameplay-systems`** owns the deck rider, the cabin and the door;
  - **`qa-test`** owns the guards each batch adds, moves and retires (§9).
- **Serves:**
  - **P2 Dory to Dynasty:** every boat you buy, up to the 90, has rooms you walk into. Six new boats
    are blocked until the engine reads the new layout.
  - **P5 Cozy but with Teeth:** the cutaway shows where you stand, aboard a boat that rocks.
- **Amends in part:**
  - [ADR 0041](0041-full-mesh-interiors.md): decision 1 (the room's source), decision 3 (the surface
    generators become an optional field of the layout), and the rollout switch with its retirement
    predicate. Decision 2, the room's own 24-slot palette, stands.
  - [ADR 0038](0038-boat-interiors.md): the cut. The declared one-hop lid becomes a lift set, and the
    cutaway kits retire with their hulls. The 08-26 ruling (a boat interior is a cutaway composite)
    stands.
  - The door and cabin rulings of 08-28 and 09-17 stand for every switched hull (§6.9).

---

## 1. The ruling

The owner ruled the switch on 2026-09-26. At 21:11:59Z: *"I know mesh interiors arent included now
but i want to switch immediately to the new sidecar/mesh interior/gameplay done in design"*. At
21:13:47Z: *"Not just on the sport but to all hulls"*.

The switch, for every hull:
- **The room** is the hull rig's own tagged mesh, `interiorFaces()`, not the interiors kit's `build()`.
- **The cut, the walk and the anchors** come from one walk sidecar per hull, written by the rig and
  stamped with the rig's sha.
- **The engine reads them on one path.** During the rollout a hull reads either the new path or
  today's, never both (§5).

This ADR decides what the engine becomes, and plans the rollout in batches (§9). It changes no code:
each batch lands under its own charter.

**What was measured.** CD's v3 sport fisher, delivered as a proposal on 2026-09-26, is the only
example of the new layout in hand. Its files are "not for landing": none of them is committed, and
this ADR records only numbers. CD's spec and batch 1 are not in hand. Every number here was measured
in the standalone V8 harness (the repo's ClearScript, net8), running the rigs' own code. Nothing ran
in Unity.

## 2. Today: three sources, made at three times

- **The room** comes from the interiors kit (`docs/art/rigs/boat-interiors-kit/boatInteriorRig.js`,
  ADR 0041). `BoatInteriorGeometryExtractor` runs the kit's `build()` at each of the eight facings,
  takes the union and drops the section lips. The hull it fits is loaded from the kit's own
  `hull-rigs/` copy, not from the rig the game draws. ADR 0041 already had to note that the sport
  fisher's room binds only from that copy.
- **The rollout switch** is `RigMeshAssetBaker.MeshInteriorHulls` (`RigMeshAssetBaker.cs:888`). It
  has seven entries: the lobster, the cape, the eighteen lobster variants as one family, the side
  dragger, both stern trawlers and the packet. The sport fishers and the tanker have kit rooms but
  still draw sprite sheets (`Art/Boats/Interiors/BoatInteriors.json`, three rows).
- **The cut** comes from the cutaway kits (`boat-cutaway-kit`, `-2`, `-5`; ADR 0038). Entering level
  L culls L's own exterior and the one lid L's `geometry()` ceiling record names: one hop,
  declaration-driven (`HullMeshCutawayTableTests`). The shader test is `HHLevelDiscards`
  (`HiddenHarboursIsoFacet.shader:424`), fed `_HHLevelShown` and `_HHLevelLid` per draw.
- **The deck** comes from a gameplay sidecar, `hidden-harbours/boat-gameplay-geometry@1`, read by
  `DeckSidecarReader` and `DeckSidecarImporter` (`*.gameplay.json`, `DeckSidecarImporter.cs:160`).
  Its classifier is negative on purpose (`NonHullSchemaFamilies`, `DeckSidecarReader.cs:204`): a
  family it has never heard of stays a hull and reddens the deck parity suite by name.
- **Why six boats are blocked.** The trawlers' Phase 1 sidecars use a layout the deck reader refuses,
  and the cabin cruiser, the bowrider and the cigarette boat have no extractor.

## 3. The new layout, as the v3 sport fisher has it

- **The rig** publishes, per hull (`byId(id)`):
  - `faceList(state, tower)`: the exterior;
  - `doorFaces(t)`: the door leaf at t in [0, 1];
  - `interiorFaces()`: the room, as one list in hull space;
  - `geometry()`: schema `hidden-harbours/hull-geometry@1`, with the tag ids, the roles, the
    ghostable roles, `dwlZ`, the levels (sole, ceiling, hard or open), and a cutaway table per mode.
- **Every face carries `lv` (level), `ro` (role) and `it` (interior)**, with `mat`, `b` and `db`.
  - `lv`: hull 0, main_deck 1, house 2, bridge 3, below 4, rigging 5, foredeck 6, top 7.
  - `ro`: shell 0, wall 1, roof 2, lining 3, part 4, floor 5, fit 6, tall 7, trim 8, rig 9.
  - Ghostable roles: shell, wall, roof, lining, part, tall, trim.
- **The walk sidecar** is `hidden-harbours/hull-walk@1`, one per hull, in `gameplay/`. The rig writes it
  ("regenerate from the rig; do not hand-edit"), stamped with the rig's LF sha256. Its sections:
  - `cell`, `DWL` and `cockpitZ`;
  - `tags` and `faces` (the counts by tag);
  - `geometry`;
  - `house`: the sole, the door, the levels and the decks;
  - `walk`: the radius (0.25 m), the step tolerance (0.34 m), the spawn, the rule, and the areas. Each
    area has an id, a cut key, a polygon, holes, and either a height or a ramp. A stair or ladder
    switches its cut key at `zMid` (`cutHi`, `cutLo`);
  - `anchors`, `rock` and `qa`.
- **The frame** is the `@1` sidecars' frame: metres, +x starboard, +y bow, +z up, the origin amidships
  at the keel bottom on the centreline.

## 4. What was measured (A1)

### 4.1 The proposal, per hull

| | 53 | 53, tower up | 90 |
|---|---:|---:|---:|
| exterior faces | 6,056 | 6,149 | 7,655 |
| door leaf faces | 2 | 2 | 2 |
| room faces (all `it`) | 1,888 | 1,888 | 3,316 |
| triangles: exterior + leaf + room | 12,127 + 4 + 4,560 = **16,691** | 12,313 + 4 + 4,560 = 16,877 | 15,327 + 4 + 7,216 = **22,547** |
| vertices: exterior + leaf + room | 24,239 + 8 + 8,336 = 32,583 | 24,611 + 8 + 8,336 = 32,955 | 30,637 + 8 + 13,848 = 44,493 |
| paints: hull (of 16) / room (of 24) / both | 9 / 10 / 5 | 9 / 10 / 5 | 9 / 11 / 6 |
| faces with a surface generator | 0 | 0 | 0 |

- **Exterior by level** (53 / 90): hull 1,365 / 1,437; main deck 289 / 396; house 1,783 / 1,730
  (walls 1,537 / 1,444, roof 246 / 286); bridge 1,675 / 2,379; top – / 789; rigging 518 (611 with the
  tower) / 530; foredeck 426 / 394.
- **The room by level** (53 / 90): house 1,094 / 1,476; bridge – / 1,164; below 794 / 676.
- **The room by role** (53 / 90): lining 957 / 2,014; part 288 / 312; floor 77 / 111; fit 528 / 769;
  tall 38 / 110.
- **Tags on every face.** Every face carries `lv`, `ro`, `it`, `b` and `db`. `it` is false on every
  exterior face and true on every room face.
- **The 90 is the largest mesh measured:** 44,493 vertices is under 65,535, so 16-bit indices hold.

### 4.2 CD's QA, reproduced

| CD's claim | measured |
|---|---|
| `fitCheck(0.01)`: 0 of 1,888 room faces outside on the 53, 0 of 3,316 on the 90 | 0 and 0, worst 0 |
| exterior faces: 6,056, 6,149 with the tower, 7,655 | the same |
| walk areas: 12 and 14 | the same |
| leak, door shut and nothing cut: 0 px at every heading on the 53; 229 px at N and 31 px at NW on the 90 | the same |
| both walk sidecars' `derivedFromRigSha256` is the rig's LF sha | Exact, on both |
| `tags` equal the rig's `LV` and `RO`; `faces.byTagExterior` and `byTagInterior` equal the rig's tallies | equal, tag by tag |
| every area names a cut key | yes: `deck`, `house`, `fly`, `below` (53); `deck`, `house`, `bridge`, `below` (90) |
| every climb switches its key at `zMid`, between its ends, to the key of the area it lands in | yes: 53 `flyladder` 2.93 → `fly`, `companionway` 1.135 → `below`; 90 `upstair` 4.274 → `bridge`, `companionway` 1.85 → `below` |

Every number reproduced; none is a finding. The 90's leak is the skybridge carpet, seen through the
skybridge's aft doorway, which has no leaf. CD's README calls it expected, and §6.4 says why the
engine never shows it under the gate.

### 4.3 Today's rooms, measured the same way

`build()` at the eight facings, the union, lips dropped, exactly as `BoatInteriorGeometryExtractor`
does it.

| hull | levels (faces) | room faces | tris | faces with a generator (plank / board / quilt) | paints | source |
|---|---|---:|---:|---|---:|---|
| lobster | house 374, cuddy 93 | 467 | 1,014 | 166 = **35.5%** (3 / 148 / 15) | 22 names, 19 ramps | harness; ADR 0041's record also has 467 and 1,014 (hull + room 1,428 + 1,014 = 2,442), and the 19 ramps |
| stern trawler | bridge 275, house 288, below 105 | 668 | 1,412 | 274 = **41.0%** (18 / 242 / 14) | 21 names, 18 ramps | harness; ADR 0041's record also has 668 and 1,412 (1,762 + 1,412 = 3,174), and the 18 ramps |
| 53 (kit room) | house 414, below 163 | 577 | 1,250 | 124 = 21.5% (20 / 48 / 56) | 19 names | harness only; never converted |
| 90 (kit room) | bridge 279, house 382, below 237 | 898 | 1,932 | 163 = 18.2% (31 / 48 / 84) | 19 names | harness only; never converted |

- **The proposal's rooms** are 3.3× and 3.7× the kit's faces, and 3.6× and 3.7× its triangles.
- **Whole meshes**:
  - the 53 is 16,691 triangles. As baked today, with no room, she is 6,472 (the committed v2 rig's
    6,446 plus the 26-triangle leaf); converted on the kit path she would be 7,722. So v3 is 2.2× the
    kit path and 2.6× today;
  - the 90 is 22,547. Today she is 7,664 (7,616 + 48); on the kit path 9,596. So v3 is 2.3× the kit
    path and 2.9× today;
  - for scale, the converted lobster is 2,442 and the stern trawler 3,174.
- **The v2 control:** the committed rig's `faceCount` is 3,239 with a 13-face leaf on the 53, and
  3,824 with a 24-face leaf on the 90.

## 5. The mixed fleet, and the guard

During the rollout, a hull with a walk sidecar reads the new path, and every other hull reads today's.
- **The switch is data.** A hull is switched when its `<rig>.<hull>.walk.json` exists.
- **The bake stamps it.** It writes the walk's schema id into a new Core field,
  `HullMeshDef.LayoutSchema`.
- **Tests read the committed defs.** Every fixture and the retirement predicate derive the switched
  set from defs with a non-empty `LayoutSchema`, never from a list typed in C#. That is how ADR 0041's
  retirement derives from `HasMeshInterior()` (`HullMeshDef.cs:210`).
- **The guard is `NoHullReadsBothLayoutsTests`**, new in batch 0. It refuses, by hull id:
  - a hull with both an `@1` deck sidecar and a walk sidecar, in either order of landing;
  - a switched hull still in `MeshInteriorHulls`, or still in `BoatInteriors.json`;
  - a switched def whose `LayoutSchema` differs from its walk sidecar's schema.

## 6. Decisions

### 6.1 The room's source

**Decision.** For every switched hull, the room is the hull rig's own `interiorFaces()`, extracted in
the same host run as `faceList`, from the file the game draws, with its tags carried to the mesh.
- There is no second rig and no kit copy of the hull, so the room cannot drift from the hull it fits.
- There is no eight-facing union and no section lip: `interiorFaces()` is already one list in hull
  space (fitCheck: 0 outside on both hulls).

**What becomes of today's pieces.** They serve the kit hulls until the last one moves (batch 3), then
retire:

| piece | today | after the switch |
|---|---|---|
| `BoatInteriorGeometryExtractor` (and `WidenInteriorRig`) | runs the kit's `build()` per facing | retires at batch 3 |
| `BoatInteriorRigHost` | loads the kit and its `hull-rigs/` copies | retires at batch 3 |
| `BoatInteriorHullResolver` | maps a hull to its kit key | retires at batch 3 |
| `BoatInteriorKit` (`Art/Editor`) | the kit's catalogue | retires at batch 3 |
| `BoatInteriorSidecarReader`, `BoatInteriorGameplayMerge` | read `.interior.json` and the kit's gameplay copy | replaced by the walk importer (§6.5); retire at batch 3 |
| `BoatInteriorDefBuilder`, `BoatInteriorRouteLanding` | build `BoatInteriorDef` and land its routes | the walk importer builds the same def; retire at batch 3 |
| `RigMeshBuilder` | writes UV0, UV1 and UV2 | stays; UV1 gains `.z` (§6.2) |

**Rejected.**
- *Both room paths, kept for good.* Two sources of one room, two sets of tests, and kit copies of the
  hull rigs that drift from the rigs the game draws. The owner ruled all hulls.
- *Re-pinning the kit for the sport fishers (drop 16).* It lands a room on the path this ADR retires,
  and spends a bake slot on hulls batch 1 re-bakes.

**Cost.** The sport fishers' rooms are 3.6× and 3.7× the kit's triangles, and their meshes 2.2× and
2.3× a kit conversion (§4.3). Like every converted room today, the room draws with the hull and is
discarded per fragment while no level is shown (`HHLevelDiscards`). Whether 16,691 and 22,547
triangles cost anything at the desktop budget, and on a mobile GPU, is batch 1's slot question (§10).
If they do, the room becomes a separate draw made only while a level is shown. That is a bake change,
not a contract change.

### 6.2 The packing

**Decision.** `ro` rides in **`TexCoord1.z`**, stored as `ro + 1`.
- 0 means untagged: a rig with no role vocabulary. The shader reads it as every role, so every hull on
  today's path cuts exactly as it does now.
- `TexCoord1.x` stays the level and `TexCoord1.y` stays the interior flag (ADR 0041).
- `b` and `db` ride UV0 as today, since every v3 face carries both.

**Rejected.**
- *`TexCoord1.y`, CD's example.* It is already the interior flag.
- *Packing into `.x`* (level + 16 × role). It saves 4 B a vertex, and changes what the level channel
  means for every reader at once: the level test, the guard pass and the tag tests.
- *One baked "ghostable" bit.* It bakes the ghost's role list into the mesh, so changing `ghostRoles`
  would need a re-bake. The gate's lift sets need roles too: the ghost keeps a level's walls and lifts
  its roof (§6.4).

**Cost.**
- 4 B a vertex, on switched meshes only: 130 KB on the 53 (32,583 vertices), 178 KB on the 90
  (44,493).
- A mesh with no `ro` bakes byte-identical, because UV1 stays two-wide when no face carries a role.
- The varying `lvl` goes from float3 to float4 in the same interpolator register, so there is no new
  interpolator.

**The palettes hold.**
- The hull's 16 slots: v3 paints 9 on each hull.
- The room's `_RampMetaInterior[24]`: 10 on the 53 and 11 on the 90.
- The 5 and 6 paints both use are copied into each palette, as ADR 0041 decision 2 already does.

ADR 0041 decision 2 stands. The bake refuses a room over 24 and never borrows the hull's slots, and a
refusal is an intake finding CD answers. Today's kit rooms bake 18–19 ramps; each switched room is
measured against the cap at its batch's intake.

### 6.3 The surface generators

**v3 carries none.** No face in either list names a generator or carries a uv.
- The loss, in the share of room faces textured today: the lobster 35.5%, the stern trawler 41.0%,
  the 53 21.5%, the 90 18.2% (§4.3).
- A switched room without generators is flat everywhere.

**Decision.** The generators become an **optional per-face field** of the layout: `sg` (`plank`,
`board` or `quilt`) with its `period` and a uv per vertex. These are the kit's generators, which the
shader already transcribes (ADR 0041 decision 3, pinned by `InteriorTexTranscriptionTests`).
- Today `RigMeshBuilder` writes `TexCoord2` whenever a mesh carries a room (`RigMeshBuilder.cs:194`).
  On a switched mesh it writes it only when some face carries `sg`.
- A flat room then bakes no `TexCoord2`, and saves 16 B a vertex (712 KB on the 90).

**The ask for CD** (§8): carry `sg` where the kit's rooms carried a generator, or say that flat is
intended at the new face density. If flat is intended, decision 3's transcription and
`InteriorTexTranscriptionTests` retire with the last kit hull (batch 3).

### 6.4 The cut: ghost or gate

**What each mode lifts.**
- *Ghost* and *gate as v3 writes it* come from the rig's own predicates.
- *Gate, lift-set form* is this ADR's (see "The finding" below).
- Counts are faces, 53 / 90: the exterior, then any room faces the mode lifts.

| key (where you stand) | ghost lifts | gate, as v3 writes it | gate, lift-set form |
|---|---|---|---|
| `deck`: cockpit, mezzanine, side stairs, the 90's aft deck | nothing | nothing | nothing |
| `house`: the salon | levels bridge, top and rigging, and the house roof: 2,439 / 3,984, and the 90's skybridge room, 1,164 | the salon's own exterior, walls and roof: 1,785 (with the 2 leaf faces) / 1,732 | the salon's exterior and the ghost's levels: 3,978 / 5,430, and the same room |
| `bridge`: the 90's skybridge | top and rigging, and the bridge roof: – / 1,565 | the skybridge's own exterior: – / 2,379 | the skybridge's exterior, top and rigging: – / 3,698 |
| `fly`: the 53's open flybridge | top and rigging: 518 (611 with the tower) / – | rigging: 518 (611) / – | rigging, and top where there is one: 518 (611) / – |
| `below`: the staterooms | levels foredeck, house, bridge, top and rigging: 4,404 / 5,824, and the rooms above, 1,094 / 2,640 | the foredeck lid: 426 / 394 | the ghost's levels: 4,404 / 5,824, and the same rooms |

**The finding: both modes need lift sets.**
- On both sport fishers a level stands on the salon's roof: the 53's flybridge, and the 90's
  skybridge and top.
- Today's engine lifts one declared lid, and v3 declares no lid for the house.
- So the gate as written leaves the salon under its flybridge. The rig's own renders show 30,356 room
  pixels on the 53's salon (the sum over the 8 headings) where the lift-set form shows 117,922: 26%.

**Room pixels.** These are the pixels that change when the room is removed, under the same cut: how
much of the room the cut shows. Each is the sum over the 8 headings, from the rig's own renderer.

| hull, key | ghost: lift | ghost: lift + hole at the room's centre | gate, as written | gate, lift-set form |
|---|---:|---:|---:|---:|
| 53 house | 76,809 | 79,129 | 30,356 | **117,922** |
| 53 fly | 0 | 0 | 0 | 0 |
| 53 below | 207,620 | 207,895 | 89,375 | 207,620 |
| 90 house | 237,805 | 238,353 | 164,113 | **460,767** |
| 90 bridge | 88,611 | 96,542 | 91,676 | **226,756** |
| 90 below | 499,469 | 499,492 | 110,419 | 499,469 |

The rig's renderer culls no back faces, so the near walls' linings still stand in these renders. The
engine culls them (`lvl.z`), so its gate shows at least this much.

**The engine form, for both modes.**
- `_HHLevelLid` becomes **`_HHLiftLevels`**, a bitmask of whole levels. A hull that declares one lid
  bakes a one-bit mask, so every hull on today's path cuts exactly as now.
- A second mask names **the shown level's lifted roles**: every role in the gate (walls and roof), the
  roof alone in the ghost (the walls stay, and are ghosted).
- An untagged `ro` counts as every role.
- The lift set per key comes from `geometry().cutaway`. CD is asked to publish `gate[key]` complete,
  in the same vocabulary as `ghost[key]` (whole levels, and `level:role`). The bake refuses a gate
  list that lifts fewer whole levels than the ghost's list for the same key.

**The prices** in the facet shader, counted from source. The compiled counts are a slot question.

| | gate, lift-set form | ghost |
|---|---|---|
| per fragment | two bit tests (level in the mask, role in the mask) replace the lid's two compares: about flat | adds, on the fragments it can touch: a role test, a depth compare, the ellipse (a mad and a dot), its compare, the core compare, a sqrt, and the dither compare. About 8–10 ALU, 1 sqrt and 4 compares. A fragment that fails the role or depth test first pays 2–3 |
| reads | none new | none new: the player's depth is already per hull (`_DeckOccupant`), and the dither is `_Bayer` |
| uniforms | `_HHLiftLevels` replaces `_HHLevelLid`; one role mask | also two float4 (the ellipse's centre and inverse radii; the depth threshold, the core, its inverse, on or off) and the ghostable-role mask |
| tunables (rule 6) | none | five, in `GameConfig`: ellipse 1.25 × 1.10 m, centred 0.95 m above the feet, depth margin 0.20 m, solid core 0.60 R |
| keywords and variants | none new | none new: a uniform branch inside `HH_LEVEL_GATE`. A keyword `HH_GHOST` would double the gated variants: rejected |
| CPU per frame | none | project the player's torso to the screen, and write it into the hull's property block, which already carries the shown level |
| desktop, 60 fps | nothing to measure | small: only the hull's fragments under the ellipse pay the full test |
| the mobile port | nothing to measure | `discard` inside the ellipse on a tile-based GPU. If the ghost runs on deck, the room must draw whenever the player is aboard: overdraw behind the whole hull |

**CD's claim is short.** The README says the ghost needs *"one uniform (the ellipse) and one
compare"*. It needs the ellipse's centre and radii, the depth margin, the core and the role mask: two
float4 and a mask, plus four compares and a sqrt on the fragments it touches.

**What happens to:**
- **the lid:** it folds into the lift set, as a one-bit mask. `HullMeshCutawayTableTests` becomes a
  lift-set table in batch 0.
- **a second occupant:** the ghost follows the player only. Crew keep their occupant bands (the
  `_DeckOccupant` band loop), and no second ellipse is made.
- **the interior water mask (ADR 0038, Proposal 2):** the guard pass (`fragGuard`) runs the lift set,
  never the ghost. A ghost hole in the guard would let the water draw into the hull.
- **the depth-step keyline:** it rings the solid hole for free. In the dither ring every other pixel
  steps from wall to room, so the keyline would stipple the ring. That is a look for the slot.
- **the 90's leafless skybridge doorway:** under the gate the room never draws uncut, so from the deck
  the doorway shows the shell's inner faces, not the carpet. Under the ghost on deck it would show the
  carpet, as CD renders it. The slot looks at both (§10).

**Recommendation: the gate, in its lift-set form, for the switch.** Every switched bake carries `ro`,
so the ghost can follow later with no re-bake.
1. **One cut, fleet-wide, through the rollout.** Unswitched hulls keep today's gate, and switched hulls
   get the same gate with lift sets, so the player never meets two cutaways at once.
2. **It shows more of the room:** 1.5× the ghost's pixels in the 53's salon, 1.9× in the 90's salon,
   2.6× in the 90's skybridge, and the same below, where the ghost already lifts whole levels.
3. **It adds nothing to tune.** No tunable, no per-frame work, and nothing to price on the mobile port.

The ghost's gain is a picture: the boat keeps its silhouette, and the player shows behind a
partition. The owner judges it on a slot (§11.1). If the owner rules the ghost, it lands as its own
shader PR after batch 0, on tags every switched mesh already carries.

### 6.5 The walk sidecar

**Decision.** A new reader and importer, **`HullWalkSidecarReader`**, for
`docs/art/rigs/gameplay/*.walk.json`.
- **It refuses any family but `hidden-harbours/hull-walk`**, and any version it does not know.
- **The deck importer is unchanged.** It keeps globbing `*.gameplay.json` with its negative
  classifier.
  - `hidden-harbours/hull-walk` is NOT added to `NonHullSchemaFamilies`. A walk misnamed
    `*.gameplay.json` must redden the deck parity suite by name, not be skipped in silence.
  - Two globs on two suffixes cannot overlap.
- **No hull reads both.** `NoHullReadsBothLayoutsTests` (§5).
- **The staleness rule is kept.** `derivedFromRigSha256` is matched by `DeckSidecarReader.MatchRigHash`
  (Exact, LineEndingNormalized, or None, which refuses). `DeckSidecarReader`'s sha helpers stay: the
  sailing and seagull readers and the shovel kit's bake already use them.
- **The importer makes today's defs.** It writes `BoatDeckDef` and `BoatInteriorDef` at the same
  asset paths, under the same hull ids, so the deck rider, the cabin and the occupancy table read one
  shape whichever path made it.

**The mapping:**
- an area whose `cut` names an open level (`deck`, the 53's `fly`) becomes a `DeckArea`. A ramp
  becomes a `polygon3d` area;
- an area whose `cut` names a hard level (`house`, the 90's `bridge`, `below`) becomes part of that
  `BoatInteriorLevel`. A raised area inside a level (the `vip` at 0.98 over below's 0.55) maps the way
  the kit's raised helm deck maps today (2.23 over the 53's salon at 1.78, joined by a stair);
- a climbing ramp (`cutHi` or `cutLo`) becomes a `BoatInteriorRoute`. Its ends are the landings, its
  rise is z1 − z0, and it is exterior when both ends are open levels;
- a hole becomes a `BoatInteriorObstruction` footprint indoors, and a `DeckArea` hole on deck.
  `DeckArea` learns holes; the guard is a new case in `DeckPolygonWalkTests`;
- the areas are player-centre polygons, already inset by the walk radius. The deck rider tests the
  centre point (`DeckAreaMath.Contains`), so the importer uses them as given and never insets them
  again. The spec must say so.

**Rejected.**
- *`DeckSidecarReader` taught the new family.* One reader then holds two schemas with different
  shapes (`DECK` against `walk.areas`, `ANCHORS` against `anchors`), and each batch's removal of `@1`
  code has to cut through a live reader.
- *New defs for the new layout.* Every consumer would read two shapes for the whole rollout.

### 6.6 The bake contract

**The anchors the baker needs today:**
- `RigMeshExtractor.Required = { F, MATS, GAIN, BIAS, LN }`, with `BAYER` optional
  (`RigMeshExtractor.cs:352`). `WidenExportedLiteral` widens these into the rig's global.
- Two tables keyed by file name:
  - `Reconstructions`: a symbol rebuilt from an expression, such as the dory's `MATS`. Most hull rigs
    have an entry;
  - `InnerWidenings`: an anchor regex and an insert. The sport fisher's is the only hull's entry (the
    other is a character rig's). It inserts `faceList, F, RIGF,` after
    `(?:return|const\s+RIG\s*=)\s*\{\s*\n\s*id:spec\.id,` (`RigMeshExtractor.cs:1194`), and
    `FleetPackHullTests` pins that it still matches exactly once.
- The interiors kit's own anchor, `root.BoatInterior = {` (`WidenInteriorRig`).
- `HullPaintSchemeBaker`'s `matsExpr` per fleet: `matsFor({0}).MATS` or `palette({scheme}).mats`.

**v3 breaks two of them.**
- Its `const RIG={ id:spec.id, …` stands on one line, so the inner widening's newline never matches.
- It has no `.MATS`, only `matsFor(id)`, which returns the ramps and a name map. Its shade lives in
  `loft.shade`, which holds `GAIN`, `BIAS` and `EDGE` but no `LN` and no `BAYER`.

**Decision: the contract goes through the global.** Each hull's global publishes, by name:
- `faceList`, `doorFaces`, `interiorFaces` and `geometry()`;
- its paints (`matsFor(id)`, or a `MATS`-shaped table);
- its shade (`GAIN`, `BIAS`, `LN`, and `BAYER` when it dithers);
- `PX`, `W`, `H`, `pivot` and `defaultElev`.

The bake reads them by name, never through text anchors.
- A rig that publishes all of them needs no table entry.
- The `Reconstructions` and `InnerWidenings` entries retire rig by rig as each switches.
- `WidenExportedLiteral` and `WidenInteriorRig` retire with the last kit hull (batch 3).

**CD's ask 6 stands.** Each rig keeps its file name and global. Batch 1 lands as
`sportFisherIsoRig2.js` and `SportFisherIso2`, not as v3's `sportFisherIsoRig3.js` and
`SportFisherIso3`, which is what its README's first engine step names. The Art desk tells CD (§8).

**Rejected.**
- *Keep the text anchors.* Every rig vintage re-aims a regex keyed by file name. The sport fisher's
  already needed a union pattern to match two vintages of one file.
- *Ask CD to keep `const RIG = {` on its own line.* A formatting rule on CD's source that CD's own
  checker cannot see.

### 6.7 Flotation

**Decision.** The flotation table (`FleetFlotationTableTests`) stays the source of every hull's
draft.
- A walk's `DWL` must agree with it within **±0.015 m**, half a pixel at 32 px/m.
- A mismatch is an intake finding, never a table edit. v3's README asks for the table's draft entry
  to follow the rig when they differ; this ADR does not: the rig moves.
- The table's deck column moves only by a ruling at the intake.

**The sport fishers' changed numbers**, for the owner (§11.4). Today's are the `@1` sidecar, the kit
room, the v2 rig and the table. v3's are the walk sidecar and the rig.

| | 53: today → v3 | 90: today → v3 |
|---|---|---|
| waterline (draft) | **0.67** (ruled 09-27) → 1.00; CD has been asked to move it | 1.13 → 1.13 |
| cockpit sole (the table's deck) | 1.25 → 1.30 | **2.05 → 1.45** |
| mezzanine | 1.77 → 1.72 | 2.83 → 2.88 |
| salon sole / ceiling | 1.78 → 1.72 / 3.74 | 2.83 → 2.88 / 5.08 |
| below sole | 0.55 → 0.55, with a raised cabin at 0.98 | 0.60 → 0.82, with a raised cabin at 1.12 |
| flybridge / skybridge sole | **4.86 → 3.92** | **7.30 → 5.56** (ceiling 7.62) |
| the aft deck up top | 4.66 → no walk area | 7.28 → 5.52 |
| tower / top station | platform 7.00 → no walk area (station at 8.00) | helm coaming 9.74 → no walk area (top at 7.97) |
| foredeck | 3.03–4.12 → no walk area (level at 2.965) | 4.03–5.79 → no walk area (level at 3.839) |
| helm | (0, −1.85, 4.80) → (0, −2.86, 4.40) | (0.65, −3.60, 7.45) → (0, −3.30, 6.20) |
| lower helm in the salon | at 2.23 → none | – |
| the way up | an inside stair (3.08) and an outside ladder → the outside ladder only (1.72 → 3.92) | an inside stair (4.47) and outside ladders → the inside stair (2.88 → 5.56); none to the top |
| the way below | a stair of 1.23 → 1.17 | a stair of 2.23 → 2.06 |
| door, clear width × height | 0.80 × 1.75 → 1.00 × 1.83 | 1.40 × 1.75 → 1.60 × 2.05 |
| door threshold | (−0.45, −5.26, 1.78) → (0.95, −4.36, 1.72) | (−0.60, −10.24, 2.83) → (−1.00, −8.30, 2.88) |
| door leaf | 13 faces → 2 | 24 faces → 2 |
| interact points | helm, lower helm, bridge helm, tower helm, stove, locker, bunk → none named | helm, bridge helm, stove, locker, bunk → none named |
| cleats | 3 bitts → none | 3 bitts → none |
| rock: roll°, pitch°, heave px | 2.1, 1.25, 0.95 → the same | 1.35, 0.80, 0.70 → the same |
| LOA | 16.2 → 16.2 | 27.4 → 27.4 |

The last five changed rows (interact points, cleats, and the missing walk areas) are gaps in the
layout, not rulings: §7 asks CD for them.

### 6.8 Retirement

**Decision.** Retirement goes through the bake's own predicate, as ADR 0041's did. A hull is switched
when its committed `HullMeshDef` carries a `LayoutSchema` (§5). `MeshInteriorHulls` shrinks as each
kit hull moves, is empty after batch 3, and retires then.

**Per hull, when its batch lands:**
- its `@1` deck sidecar;
- its kit `.interior.json` and the kit's gameplay copy of it;
- its copy in the kit's `hull-rigs/`, once no kit hull uses that copy;
- its `MeshInteriorHulls` entry, and its rows in `BoatInteriors.json` with their sheets;
- its cutaway-kit rigs, and its `Reconstructions` and `InnerWidenings` entries;
- its rows in the kit-path tests (§9).

**Per folder.** A kit's folder goes only when its last hull has moved:
- `boat-interiors-kit/` at batch 3;
- each cutaway kit with the last hull it serves, batch 3 at the latest.

`boat-interiors-intake/` stays, as the record, and `BoatInteriorS0LedgerTests` with it: its subject is
`boat-interiors-intake/s0-verdicts.json`. `DeckSidecarReader`'s `DECK` parsing goes with the last hull
`@1` sidecar. Its sha helpers stay (§6.5).

### 6.9 The door, the cabin and the swap

The 08-28 and 09-17 rulings hold for every switched hull:
- the door is closed until opened, and open means walk through freely;
- E toggles it from either side, and at the door E only opens it;
- the press moves the leaf;
- a swapped hull rebuilds its cabin (`SetHull`, then `RebuildCabin`, then the installer's `Rebuild()`).

**The door is derived from `house.door`:**
- `ThresholdPoint` = ((x0 + x1)/2, y, sillZ);
- `ClearWidthMeters` = x1 − x0, and `ClearHeightMeters` = z1 − z0;
- `Mechanism` = `Sliding` for `kind: slide`;
- the leaf is the rig's `doorFaces(0)` and `doorFaces(1)`, which bake to `HullMeshDef.DoorLeafClosed`
  and `DoorLeafOpen` through Core `IHullDoorLeaf`;
- `BoatInterior.LevelIndexAtHeight(sillZ)` returns the house, on both sport fishers.

The working ships and the tanker have hinged doors (`BoatInteriorDoorMechanism.Hinged`), and v3 has
only a sliding one. That field is batch 3's ask (§7).

**The guards gain the switched hulls' rows in each batch:** `IsoFacetHullDoorLeafTests`,
`BoatCabinDoorLeafTests`, `BoatCabinDoorWalkthroughTests`, `BoatCabinOnASwappedHullPlayTests`,
`EveryInteriorHullReachesItsDoorFromItsStandTests` and `ControlSwitcherTests`.

## 7. The parity table (A2)

For every field today's engine reads to build a boat's room, cut, deck and seats: what reads it, the
test that guards it today, what the new layout carries, and, where that is MISSING, the answer.
- (a) is a named ask for CD's spec, which the Art desk relays.
- (b) is an engine derivation, with the test that will guard it.

| # | field | today: reader → def | guard today | new layout | answer |
|---|---|---|---|---|---|
| 1 | deck walk areas and heights | `DeckSidecarReader` `DECK` {id, polygon or polygon3d, z} → `BoatDeckDef.Areas` | `DeckSidecarReaderTests`, `DeckSidecarImportParityTests`, `DeckPolygonWalkTests` | `walk.areas` on open levels: {id, cut, poly, holes, z or ramp} | carried; ramps become `polygon3d`; holes: (b) `DeckArea` learns holes, guard `DeckPolygonWalkTests` |
| 2 | every surface a figure stands on | the `@1` sidecars also have the foredeck, the 53's tower platform and flybridge aft deck, and the 90's helm coaming | as row 1 | none of these has an area | **MISSING** → (a) an area for every such surface, or an `_excluded` entry giving the reason |
| 3 | washboards | `WASHBOARD` {side, mirror, outer_edge, width_m} | `OverTheSidePlayTests`, `SternDeckLoopTests` | none (the sport fishers' `@1` excludes them by design) | **MISSING** → (a) `washboards[]` {side, outer_edge, width_m}, or an `_excluded` entry giving the reason, on every hull |
| 4 | cleats and their types | `CLEATS` {id, pos, type} → `BoatDeckDef.Cleats` | `MooringCleatsTests`, `MooringLinePlayTests` | none | **MISSING** → (a) `cleats[]` {id, type, pos [x, y, z]} |
| 5 | the helm station | `ANCHORS.helm` or `STATIONS` → `HelmStation` | `HelmStationImportTests`, `HelmStationTests`, `SportFisherFlybridgeHasAWayDownTests` | `anchors.helm` {x, y, z} | carried (lower-case `anchors`) |
| 6 | the other anchors | `ANCHORS`: tower station, chair, mezzanine seat, rods, doors, tubs, nav | `FleetDeckOccupancyTableTests`, `DeckOccupantSlotTests` | `anchors`, the same eight keys | carried |
| 7 | LOA | `frame.LOA_m` or `hull.loa_m` → `LoaMeters` | `DeckSidecarReaderTests`, `HelmStationHeadingTests` | `loa` is display text ("16.2 m · 53′") | **MISSING** → (a) `loa_m`, a number |
| 8 | the room's geometry | the kit's `build()`, 8-facing union → the mesh's room | `FullMeshInteriorRenderTests`, `IsoFacetSideDraggerAcceptanceTests` | the rig's `interiorFaces()` | carried (§6.1) |
| 9 | levels, soles and declared ceilings | the cutaway kit's `geometry()`; the interior sidecar's `cell.levels` → `BoatInteriorLevel` and the lid | `HullMeshCutawayTableTests`, `HullLevelTagBakeTests`, `HullCutawayAssetTests` | `geometry.levels` {id, deck, sole, ceil, kind}; `house.decks` | carried; the lid becomes the lift set (§6.4) |
| 10 | the cut per key | one declared lid per level | `HullMeshCutawayTableTests`, `BoatCutawayPlayTests` | `geometry.cutaway.ghost` complete; `.gate` names only the level's own exterior | **MISSING** → (a) publish `gate[key]` complete, in the ghost's vocabulary; the bake refuses a shorter one |
| 11 | the level shown where you stand | `BoatInterior.LevelIndexAtHeight`, used by `ArrivalCabinWalk` and `BoatCabinDoor` | `ConvertedHullEntryLevelTests`, `BoatInteriorRuntimeTests` | each area's `cut`, and `cutHi`/`cutLo` at `zMid` on climbs | carried; new case: every area's key names the level `LevelIndexAtHeight` returns at its height (`ConvertedHullEntryLevelTests`) |
| 12 | level outlines | `WALKABLE` → `BoatInteriorLevel.Outline` | `BoatInteriorSidecarReaderTests`, `BoatInteriorGameplayMergeTests` | `walk.areas` on hard levels | carried |
| 13 | a raised floor inside a level | a walkable with its own height, joined by a stair (the 53's helm deck) | `CabinUpstairsTests` | an area with its own z, joined by a ramp (`vip`, `belowstep`) | carried, mapped as today's raised helm deck |
| 14 | stairs and ladders, with their landings | `STAIRS`, `LADDER` → `BoatInteriorRoute` {from, to, mechanism, exterior, rise, from and to points} | `BoatInteriorRouteLandingTests`, `CabinUpstairsTests`, `SportFisherFlybridgeHasAWayDownTests` | climbing ramps: {axis, a0, a1, z0, z1}, `zMid`, `cutHi`/`cutLo` | carried: the landings are the ramp's ends |
| 15 | stair or ladder | the route's `Mechanism` | as row 14 | not said: the 53's `flyladder` is a ladder by its name only | **MISSING** → (a) `kind: stair \| ladder` on every climb |
| 16 | treads | the kit's `treads[]`, whose `going_m` the reader sums into a flight's run | as row 14 | none | (b) not needed: a ramp gives the run (a1 − a0) and the rise (z1 − z0) directly |
| 17 | the door and its threshold | `THRESHOLD` {side, clear width, clear height, threshold point, mechanism, slide} → `BoatInteriorDoor` | `BoatCabinDoorLeafTests`, `BoatCabinDoorWalkthroughTests`, `IsoFacetHullDoorLeafTests`, `EveryInteriorHullReachesItsDoorFromItsStandTests` | `house.door` {kind, face, y, x0, x1, z0, z1, travel, clearAt, sillZ} and `doorFaces(t)` | carried (§6.9) |
| 18 | the door's keep-clear and cue | `keep_clear`, cue frames and timing → `KeepClear`, `CueFrames` | `BoatCabinDoorLeafTests`, `BoatCabinDoorWalkthroughTests`, `BoatInteriorPlayTests` | none | (b) derive: keep-clear is the clear width by one walker's diameter (0.50 m) each side of the threshold; the cue comes from `GameConfig`; the same guards |
| 19 | hinged doors, and a second door | `Hinged` (the working ships, the tanker); `AdditionalDoors` | as row 17 | one sliding `house.door` | **MISSING** for batch 3 → (a) `kind: hinge` with its hinge side and swept arc, and a door list |
| 20 | interact points | `INTERACT` {id, action, reach_point, visible_facings} → `BoatInteriorAnchor` | `BoatInteriorSidecarReaderTests`, `BoatInteriorDefShapeTests`, `BoatInteriorGameplayMergeTests` | anchors are positions, with no action and no reach point | **MISSING** → (a) `interact[]` {id, action, reach {x, y, z}} for every point E acts on; today's sport fishers have their helms, the stove, the locker and the bunk |
| 21 | obstructions | `WALKABLE` obstructions → `BoatInteriorObstruction` | `BoatInteriorGameplayMergeTests` | area `holes` | (b) derive: each hole becomes a footprint; guard `HullWalkImportParityTests` |
| 22 | the room's footprint | `FOOTPRINT` → `FootprintOutline` | `BoatInteriorDefShapeTests` | none | (b) derive: the union of the room's level outlines; guard `BoatInteriorDefShapeTests` |
| 23 | which hulls a room fits | `fits_hulls`, `hull_stem` → `FitsHulls` | `BoatInteriorDefContractAgreementTests` | `hull`, `exportSymbol`, `rig`: one sidecar per hull | carried |
| 24 | the room rides the hull's rock | `rides_hull_rock` → `RidesHullRock` | `MeshRockSmoothnessTests` | none | (b) derive: true, since the room is in the hull's mesh |
| 25 | the sheet cell | `cell` → `PixelsPerMetre`, `CellPixels`, `PivotPixels` | `BoatInteriorSheetTests`, `BoatInteriorCellHandednessTests` | `cell` {W, H, pivot, pxPerM, defaultElev, order} | carried |
| 26 | the waterline | the flotation table | `FleetFlotationTableTests` | `DWL`, `geometry.dwlZ` | carried; checked against the table (§6.7) |
| 27 | deck and sole heights | `@1` `hull.sole`; the table's deck | `FleetFlotationTableTests` | `cockpitZ`, each area's z, the level soles | carried; each change is named (§6.7) |
| 28 | the rock | `HullMeshDef.RockRollDegrees`, `RockPitchDegrees`, `RockHeavePixels` | `MeshRockSmoothnessTests`, `HullMeshFleetTests` | `rock` {rollA, pitchA, heaveA, period, frames} | carried; equal today on both sport fishers |
| 29 | the rig's sha | `rig`, `derivedFromRigSha256`, under `RigHashMatch` | `DeckSidecarReaderTests`, `BoatInteriorDefContractAgreementTests` | `rig`, `derivedFromRigSha256` | carried; Exact on both v3 sidecars |
| 30 | level and interior per face | UV1 (level, interior) | `HullLevelTagBakeTests` | `lv`, `it` | carried; `ro` added (§6.2) |
| 31 | paints and schemes | `.MATS` or a reconstruction; `HullPaintSchemeBaker`'s `matsExpr` | `HullPaintSchemeBakeTests`, `FleetPackHullTests` | `matsFor(id)` | carried, through the global (§6.6) |
| 32 | the shade constants | `GAIN`, `BIAS`, `LN`, `BAYER` | the extractor refuses a rig without them (`RigMeshExtractor.Required`) | `loft.shade`: `GAIN`, `BIAS`, `EDGE` | **MISSING** `LN` and `BAYER` → (a) publish them on the global |
| 33 | surface generators | the kit's `tex` → `TexCoord2` | `InteriorTexTranscriptionTests` | none | **MISSING** → (a) optional `sg`, `period` and uv (§6.3) |
| 34 | each fleet's rig and global | `HullMeshFleet`, `LobsterVariantFleet`, `ZodiacFleet`, `SportFisherFleet` (`ScriptPath`, `GlobalName`) | `HullMeshFleetTests`, `FleetPackHullTests` | `rig`, `exportSymbol` | carried, since each rig keeps its name (ask 6). v3's new names are not landed (§6.6) |
| 35 | sailing | `SailingSidecarReader` (HULL_FORM, POLAR_REFERENCE, SAIL_PLAN, STATES, WIND), `RigCatalog.SailKit` | `SailRigKitTests` | not the walk's business | not MISSING: the sailing sidecar stays its own family |
| 36 | deck occupancy | the anchors against `DeckOccupantSlots` = 12 | `FleetDeckOccupancyTableTests` | `anchors` | carried; rows re-derived per batch |
| 37 | the top station and the tower | an area (53 tower platform; 90 helm coaming), a ladder, an interact point | `SportFisherFlybridgeHasAWayDownTests` | an anchor only (`towerStation`); no area, no climb | **MISSING** → (a) an area and a climb, or mark them non-walkable (see row 2) |

## 8. What the engine needs from the new layout

The Art desk relays this list to CD and checks each batch against it. The units are metres unless
stated. The frame is the hull's: +x starboard, +y bow, +z up, the origin amidships at the keel
bottom on the centreline. Polygons are counter-clockwise seen from above. **R** means required, **O**
optional; **NEW** marks a field v3 does not carry yet.

**A. The rig, through its global** (the file name and global kept, ask 6). Per hull id, via `byId(id)`:

| field | what it is | |
|---|---|---|
| `faceList(state, tower)` | the exterior faces: `v` (the corners, in hull space), `mat`, `b`, `db`, `lv`, `ro`, and `it: false` | R |
| `interiorFaces()` | the room's faces, the same keys with `it: true`, inside the shell (fitCheck ≤ 0.01 m) | R for a hull with a room |
| `doorFaces(t)` | the leaf at t in [0, 1], tagged like the exterior | R for a hull with a door leaf |
| `geometry()` | `hidden-harbours/hull-geometry@1`: the tag ids, the roles, `ghostRoles`, `dwlZ`, `levels` {id, deck, sole, ceil (null if open), kind `hard`/`open`}, and `cutaway` {ghost, gate}, each gate list complete (§6.4) | R |
| paints | `matsFor(id)` (the ramps, and name → {ramp, off}) or a `MATS`-shaped table. Every face's `mat` names one. The hull ≤ 16 ramps, the room ≤ 24 | R |
| shade | `GAIN`, `BIAS`, `LN`; `BAYER` when the hull dithers | R (**NEW**: `LN`, `BAYER` on the global) |
| cell | `PX` (px/m), `W`, `H`, `pivot` {x, y} (px), `defaultElev` (degrees) | R |
| per face `sg`, `period`, uv per vertex | the room's surface generator: `plank`, `board` or `quilt` | O (**NEW**, §6.3) |

**B. The walk sidecar**, `docs/art/rigs/gameplay/<rig stem>.<hull id>.walk.json`, one per hull:

| field | what it is | |
|---|---|---|
| `schema` | `hidden-harbours/hull-walk@1` | R |
| `rig`, `exportSymbol`, `hull` | the rig's file name, its global, and the hull id | R |
| `derivedFromRigSha256` | the rig's LF sha256 | R |
| `units`, `frame` | `metres`; the frame above | R |
| `loa_m` | LOA, a number | R (**NEW**) |
| `DWL` | the waterline above the keel bottom; must equal the flotation table ± 0.015 m | R |
| `cockpitZ` | the cockpit sole | R |
| `tags` | {lv, ro, ghostRoles}, equal to the rig's | R |
| `house.door` | {kind `slide` or `hinge`, face, y, x0, x1, z0, z1, sillZ; `travel` for a slide; hinge side and swept arc for a hinge} | R for a hull with a cabin (**NEW**: `hinge`) |
| `house.doors[]` | a list, when a hull has more than one door | O (**NEW**) |
| `house.levels`, `house.decks` | each level's sole and ceiling, as `geometry()` has them | R for a hull with a cabin |
| `walk.radius`, `walk.stepTol` | the walker's radius; the largest step between heights | R |
| `walk.areas[]` | `id`; `cut` (a key of the cutaway table); `poly` [[x, y]…], player-centre, already inset by `radius`; `holes`; and either `z` or `ramp` {axis `x`/`y`, a0, a1, z0, z1} | R |
| climbs | `zMid`, `cutHi` or `cutLo`, and `kind: stair` or `ladder` | R (**NEW**: `kind`) |
| every standing surface | an area, or an `_excluded` entry giving the reason: foredecks, side decks, towers, top stations | R (**NEW**) |
| `walk.spawn` | {x, y, area} | O (not read today) |
| `anchors.helm` | {x, y, z} | R for a hull with a helm |
| `anchors` others | `towerStation`, `chair`, `mezzSeat`, `rods[]`, `doors{}`, `tubs[]`, `nav{}` | O |
| `interact[]` | {id, action, reach {x, y, z}}, one for every point E acts on: helms, the stove, lockers, bunks | R (**NEW**) |
| `cleats[]` | {id, type, pos [x, y, z]} | R for a hull that moors (**NEW**) |
| `washboards[]` | {side, outer_edge, width_m}, or an `_excluded` entry giving the reason | R, one or the other (**NEW**) |
| `rock` | {frames, rollA (°), pitchA (°), heaveA (px), period} | R |
| `qa` | {fitCheck, leakPx} | O (the checker's) |

**C. In the spec itself** (ask 5):
- the schema ids and their versions, and the Node checker;
- which fields each kind of hull must carry: an open boat (deck only), a cabin boat, a ship;
- that the walk is written by the rig and never edited by hand;
- the 90's leafless skybridge doorway: a leaf, or a statement that it stays open (§6.4).

## 9. The migration plan, and the rollout record

**The engine path goes first: batch 0, on a synthetic fixture, starting now.**
- It cannot be built on the v3 files, which are not for landing. It can be built on a small fixture of
  its own: a box hull with three levels, a rig and a walk written for the tests, never a CD file.
- It turns the owner's "immediately" into work that starts before CD delivers.
- Batch 1's slot is then spent on the bake and the plates, not on engine faults.
- If CD's spec renames a field, the reader changes in one place.
- **Rejected:** the engine landing inside batch 1. That is one PR fewer, but no engine work starts
  until CD delivers, and a red in one PR carrying the engine and eight hulls' data cannot tell which
  of the two broke.

**Every batch** is one intake PR under its own charter, in one session, with one editor slot for its
bake. Each extends the table at the end of this section.

### Batch 0: the engine path

- **Lands:**
  - `HullWalkSidecarReader` and its importer (§6.5);
  - `NoHullReadsBothLayoutsTests` and `HullMeshDef.LayoutSchema` (§5);
  - the bake reading a hull through its global (§6.6);
  - `ro` in `TexCoord1.z` (§6.2);
  - `_HHLiftLevels` and the shown level's role mask, replacing `_HHLevelLid`, with today's lids as
    one-bit masks (§6.4);
  - `DeckArea` holes;
  - the synthetic fixture.
- **Retires:** nothing. No hull moves.
- **Tests:**
  - *added:*
    - `HullWalkSidecarReaderTests` (subject `HullWalkSidecarReader`);
    - `HullWalkImportParityTests` (the importer: defs made from the fixture equal the ones built by
      hand);
    - `NoHullReadsBothLayoutsTests` (the two importers' file sets, `MeshInteriorHulls`,
      `BoatInteriors.json`);
    - a holes case in `DeckPolygonWalkTests` (`DeckAreaMath`);
  - *moved:*
    - `HullMeshCutawayTableTests` becomes a lift-set table (the bake's lid stamp);
    - `HullLevelTagBakeTests` checks `ro` in UV1 (`RigMeshBuilder`);
    - `IsoFacetShaderCompileGuardTests` compiles the new uniforms (the facet shader);
  - *retired:* none.
- **The owner sees:** nothing new. Every hull draws and cuts as before, and the slot's plates prove it.
- **Waits on:** this ADR's merge, and a short slot. The ghost, if ruled, is its own PR after this one.

### Batch 1: the six new boats and the two sport fishers

- **The hulls:**
  - the six new boats: the trawlers, the cabin cruiser, the bowrider and the cigarette boat; CD names
    them, and their fleet rows;
  - the two sport fishers (`SportFisherFleet`).
- **Lands:**
  - CD's batch 1 rigs, under their kept names: the sport fishers as `sportFisherIsoRig2.js` /
    `SportFisherIso2`;
  - one walk sidecar per hull;
  - the baked meshes with `LayoutSchema`, and the deck and interior defs at today's paths.
- **Drops 15 and 16:**
  - drop 15 folds in: the same hull lines, now with their rooms and walks;
  - drop 16 never lands (§11.2).
- **Retires:**
  - the sport fishers' `@1` sidecars (`sportFisherConvertibleIso.gameplay.json`,
    `sportFisherSkybridgeIso.gameplay.json`);
  - their kit rooms (`sportFisherIsoRig2.convertible.interior.json`, `.skybridge.interior.json`) and
    the kit's gameplay copies of them;
  - the kit's `hull-rigs/sportFisherIsoRig2.js`;
  - four sheets (`SportFisherConvertibleIsoInterior.png`, `SportFisherSkybridgeIsoInterior0–2.png`) and
    two of the three rows in `BoatInteriors.json`;
  - the sport fisher's `InnerWidenings` and `Reconstructions` entries;
  - her copies in the cutaway kits (`boat-cutaway-kit-5/hull-rigs/`, and two rooms in
    `boat-cutaway-kit/sidecars/`), or with their kit if the kit's own record still lists them.
- **Tests:**
  - *added:* rows for the eight hulls in `HullWalkImportParityTests`, `FleetFlotationTableTests`,
    `FleetDeckOccupancyTableTests`, `HullMeshFleetTests`, `FleetPackHullTests`,
    `IsoFacetFleetPackAcceptanceTests`, and the walk twin rows in `HelmStationImportTests`;
  - *moved:*
    - `SportFisherFlybridgeHasAWayDownTests` re-points to the walk's `flyladder` (the sport fisher's
      route off the flybridge);
    - `DeckSidecarImportParityTests` loses the sport rows that `HullWalkImportParityTests` gains;
    - `BoatInteriorSheetTests` loses two sheet rows;
  - *retired:* no class. `FleetPackHullTests`' case
    `TheSportFishersInnerWidening_IsStillAimedAtHerReturnLiteral` goes with the entry it pins.
- **The owner sees:**
  - the two sport fishers on their new lines, with rooms you walk into and the gate cut;
  - their changed numbers as ruled at the intake (§6.7);
  - six new boats in the fleet, with rooms.
- **Waits on:** CD's batch 1, carrying §8; the §11 rulings; batch 0; a slot.

### Batch 2: the lobster family

- **The hulls:** the lobster and the cape (`HullMeshFleet`), and the eighteen variants
  (`LobsterVariantFleet`).
- **Why second:** they are the most-played boats. They are already converted on the kit path, so
  ADR 0041's rollout record is the baseline each room is measured against: faces, ramps, triangles
  and reveal.
- **Lands:** their rigs in the new layout under their kept names (including
  `lobsterBoatVariantsIsoRig.js` / `LobsterBoatVariantsIso`), their walks, and the re-bakes.
- **Retires:**
  - their `@1` sidecars (the lobster's declares no schema);
  - their kit rooms, and `boat-interiors-kit/lobsterBoatVariants/`;
  - their `hull-rigs/` copies;
  - the `MeshInteriorHulls` entries `LobsterBoatIso`, `CapeIslanderIso` and the variants' family;
  - their cutaway-kit rigs, and their `Reconstructions` entries.
- **Tests:**
  - *moved:* rows from the kit-path fixtures to the switched set:
    - `ConvertedInteriors` gives way to a `SwitchedHulls` fixture derived from `LayoutSchema`;
    - `FullMeshInteriorRenderTests` and `ConvertedHullEntryLevelTests`;
    - `BoatInteriorDefContractAgreementTests` and `BoatInteriorCellHandednessTests` lose these hulls'
      rows;
  - *added:* their rows in the walk parity and fleet tables.
- **The owner sees:** the same boats, their rooms re-made from their own rigs, each against its ADR
  0041 numbers.
- **Waits on:** CD's batch 2, and batch 1 landed as the first proof on real hulls.

### Batch 3: the working ships and the tanker: the kit path retires

- **The hulls** (`HullMeshFleet`): the side dragger, both stern trawlers, the packet and the tanker.
  These are the last kit hulls. The tanker's room has only ever been a sheet (16 px/m), so hers
  becomes a mesh for the first time.
- **Lands:** their rigs, walks and re-bakes, with hinged doors (§7 row 19).
- **Retires:**
  - `boat-interiors-kit/`;
  - the cutaway kits;
  - the tanker's six sheets and `BoatInteriors.json` itself;
  - `BoatInteriorGeometryExtractor`, `BoatInteriorRigHost`, `BoatInteriorHullResolver`,
    `BoatInteriorKit`, `BoatInteriorSidecarReader`, `BoatInteriorGameplayMerge`,
    `BoatInteriorDefBuilder`, `BoatInteriorRouteLanding`, `WidenInteriorRig`, `WidenExportedLiteral`
    and `MeshInteriorHulls`;
  - ADR 0041 decision 3's transcription, if flat is intended (§6.3).
- **Tests retired**, with their production subjects:
  - `BoatInteriorSidecarReaderTests` (`BoatInteriorSidecarReader`);
  - `BoatInteriorGameplayMergeTests` (`BoatInteriorGameplayMerge`);
  - `BoatInteriorRouteLandingTests` (`BoatInteriorRouteLanding`);
  - `BoatInteriorDefContractAgreementTests` (the kit sidecar and the bake pinning the same rig);
  - `BoatInteriorCellHandednessTests` (the baked sheet cells);
  - `BoatInteriorSheetTests` (the `BoatInteriors.json` sheets);
  - `BoatInteriorPlacementTests` (the sheet wiring);
  - `InteriorTexTranscriptionTests` (the generators; moved to the rigs' `sg` instead if CD carries it).
- **Tests moved:** `BoatInteriorDefShapeTests` (the def's shape, now from the walk importer).
- **Tests staying:** `BoatInteriorS0LedgerTests` (the intake ledger stays).
- **The owner sees:** the working ships' rooms re-made, and the tanker's room walkable.
- **Waits on:** CD's batch 3, with hinged doors, and batch 2.

### Batch 4: the open boats

- **The hulls:** the dory, the punt, the console skiff and both sport skiffs (`HullMeshFleet`), and
  the zodiacs (`ZodiacFleet`).
- **What they get:** a deck only: walks with areas, cleats, washboards and a helm.
- **Retires:** the last hull `@1` sidecars. `DeckSidecarReader`'s `DECK` parsing goes with the last
  `@1` file, the sloops' if §11.3 puts them in `@1` first.
- **Tests:** `OverTheSidePlayTests`, `SternDeckLoopTests`, `MooringCleatsTests` and
  `MooringLinePlayTests` move their rows to the walk.
- **The owner sees:** the same boats. Their decks, cleats and washboards now come from the walk.
- **Waits on:** CD's washboard and cleat fields (§8), which matter most on these boats; and batch 1.
  It may run beside batches 2 and 3.

### Batch 5: the sloops

- **The hulls:** `HullMeshFleet`'s `sloop30` and `sloop88`, placed by §11.3.
- **The sailing sidecar** (`*.sailing.json`, `SailingSidecarReader`) stays its own family, beside the
  walk.
- **The charter's premise is stale.** `HullMeshFleet.BakeBlocked` is empty on main, and the sloops
  bake as `MeshOnly` rows (`HullMeshFleet.cs:412–415`). The bake no longer waits for a level stamp.
  What is left is what `gameplay/sail/README.md` lists for the PR that unblocks the bake: move their
  `@1` sidecars up one level, with their deck defs, their `VisualsBySidecar` rows and their visuals.
- **The two placements:**
  - *folded in:* the sloop deck waits for CD's sloop batch, the last in this plan, and no `@1` work is
    done that the switch later retires;
  - *their own charter first, in `@1`:* the sidecars exist and the bake is unblocked, so the sloops get
    a deck soon, for one small charter. Batch 5 then retires two more `@1` files and moves their rows.

### The rollout record

| batch | hulls | PR | merged | room faces | room ramps (cap 24) | tris, hull + room | Δ tris against the kit path | reveal | notes |
|---|---|---|---|---|---|---|---|---|---|
| 0 | none (engine path) | – | pending | – | – | – | – | today's, unchanged | |
| 1 | the six new boats; 53, 90 | – | pending | | | | | | |
| 2 | lobster, cape, 18 variants | – | pending | | | | | | |
| 3 | side dragger, stern trawler, Mk2, packet, tanker | – | pending | | | | | | |
| 4 | dory, punt, console skiff, sport skiffs, zodiacs | – | pending | | | | | | |
| 5 | sloop 30, sloop 88 | – | pending | | | | | | |

## 10. Open questions for batch 1's slot

These need Unity, so this PR did not answer them:
1. the compiled instruction counts of the gate's lift-set form, and of the ghost if ruled, on desktop
   and on a mobile target;
2. the frame cost of the sport fishers' 16,691- and 22,547-triangle meshes, whose rooms draw with the
   hull and are discarded while no level is shown;
3. the ghost's overdraw if it runs on deck, and what `discard` costs on a tile-based GPU;
4. the keyline around the ghost's dither ring;
5. the engine's back-face cull of v3's near linings: the reveal numbers of §6.4 are a floor;
6. the gate plates of both sport fishers at the 8 headings, including the 90's leafless skybridge
   doorway seen from the deck.

## 11. Decisions for the owner

1. **Ghost or gate.** The recommendation is the gate, in its lift-set form, for the switch, with `ro`
   baked so the ghost can follow on a slot's evidence (§6.4).
2. **Drops 15 and 16.**
   - Recommended: 16 never lands, because it re-pins the kit this ADR retires.
   - Recommended: 15 folds into batch 1, so each sport fisher lands once, on one slot.
   - The alternative is to land 15 first, if the owner wants the new hull lines before batch 1. Drop
     15 carries `@1` deck sidecars for the new lines, so the decks follow them. But the sport fishers'
     rooms stay the kit's, fitted to the old lines, until batch 1 (re-fitting them is what 16 does).
     And it costs a second slot and a re-bake at batch 1.
3. **The sloops.** Recommended: their own charter first, in `@1`, because the lean to fold them in
   rests on `BakeBlocked`, which is empty. The alternative is to fold them into batch 5 (§9).
4. **Changed numbers.** Each is ruled at its batch's intake, the sport fishers' first (§6.7).
5. **This ADR** is accepted by merging it.

**Already ruled:**
- the switch, for all hulls (2026-09-26, 21:11:59Z and 21:13:47Z);
- the 53 at 0.67 m (2026-09-27);
- the door and the cabin on a swapped hull (2026-09-17).

## 12. Consequences

- **One source per hull.** The rig and its walk give each hull its room, its cut, its deck and its
  anchors, stamped with one sha. The kit's copies of the hull rigs, and the drift they allowed, go.
- **The six new boats unblock** at batch 1, on a path batch 0 has already proven.
- **The cut becomes data:** a lift set per key, published by the rig and checked by the bake.
- **The rooms cost more.** The sport fishers' meshes are 2.2× and 2.3× a kit conversion, measured on a
  slot before batch 1 merges.
- **CD's spec grows:** cleats, washboards, interact points, climb kinds, LOA as a number, complete gate
  lists, hinged doors, and an area for every standing surface (§8).
- **For a few weeks the fleet is mixed.** One guard proves no hull reads both paths (§5), and each
  batch leaves the tests as green as it found them.
