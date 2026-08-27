# Spike verdict — can the boat interiors be MESH?

**Verdict: HYBRID.** The room's **shell** should become geometry. The **fit-out** should stay pixels.
One upstream datum blocks the shell, and it is one number per level.

> **The fleet has 59 interior levels across 24 hulls, not 53.** The handoff and ADR 0038's
> surrounding numbers both say 53; the committed `BoatInteriorDef`s carry **59** — `house_sole` ×24,
> `cuddy_sole` ×19, `below_sole` ×5, `bridge_sole` ×5, `main_deck` ×5, `poop_deck` ×1. Every one of
> them is mesh-backed. Correct this wherever 53 appears.
>
> **And it has moved again since: 65 levels across 26 hulls** (2026-08-26, re-measured by this
> spike's own fixture — see `interior-mesh-measurements.txt`, regenerated alongside the cutaway
> bake). Two more interior-bearing hulls landed from other lanes, and `helm_deck` joins the level
> ids. The point of the correction stands; the number is a moving one and the FILE is the oracle,
> not this paragraph.

- **Branch:** `spike/interior-mesh` · **Date:** 2026-08-22 · **Author:** coordinator
- **Asked by:** the S-spike handoff of 2026-08-22 (questions A–D)
- **Reads:** [ADR 0022](../../adr/0022-3d-boat-hulls.md) · [ADR 0038](../../adr/0038-boat-interiors.md) ·
  [ADR 0036](../../adr/0036-interior-levels-as-layers.md) · `RigMeshInteriorClassifier` ·
  `docs/art/rigs/boat-interiors-kit/boatInteriorRig.js`
- **Numbers:** [`interior-mesh-measurements.txt`](interior-mesh-measurements.txt) ·
  [`interior-mesh-B-tag-proof.txt`](interior-mesh-B-tag-proof.txt) ·
  [`interior-mesh-C-renders.txt`](interior-mesh-C-renders.txt)
- **Pictures:** `docs/art/spikes/interior-mesh/`
- **Tool:** `Assets/_Project/Code/Tools/Editor/Spikes/` (+ the EditMode fixture
  `BoatInteriorMeshSpikeRenderTests`), run on the local RTX 4060, `skipped=0`, 3/3 green.

**Nothing in this spike changes a shipped bake, and nothing touches `docs/art/rigs/**`.** The
upstream ask in §D is written as a proposal, not as an edit. The one shipped file it does touch —
the facet shader — carries the gate behind `#pragma shader_feature_local HH_LEVEL_GATE`, **off by
default**, so the program every hull compiles is literally the pre-spike one.

---

## The short version

| | answer | the number that says so |
|---|---|---|
| **A. Extrudable?** | **Yes, and cheaply.** (shell not yet built) | +7.5% to +12.6% triangles, +24–31 KB per hull. **Zero** per-level section-containment violations on all three hulls. |
| **B. One tag, both halves?** | **Yes — and SHIPPED for batch 1, 2026-08-26.** | Keyword off (the shipped program) vs the gated variant at 0: **0** differing px. Gate on: the level's own hull faces draw **0 px**, the room draws 8,534 px. |
| **B. 59 subsets derivable?** | **Yes from geometry — but NOT from LAYOUT alone.** | **0.79%** of faces ambiguous fleet-wide (308 of 38,778, 24 hulls, two hulls at 0.00%) — *once a per-level ceiling exists*. It does not. |
| **C. The look** | Owner's call. Five PNGs committed. | — |
| **D. Hybrid cost** | Shell geometry, fit-out pixels. | Shell needs **3–6** palette ramps; the hull leaves **4–6** free of 16. The fit-out declares **21**. |

---

## A. Is the plan extrudable?

Yes. The extrusion needs no new authoring: `BoatInteriorDef` already carries, per level, the
sole `z`, the sole polygon **already inset by the rig's WT**, and the door's threshold point and
clear width/height. The spike emits sole + walls + ceiling + door aperture straight from it, in the
hull's own mesh space, following `RigMeshBuilder`'s conventions (flat per-face normals, UV0 =
`(material, b, db, interior side code)`).

### Cost

| hull | levels | hull tris | **room tris** | **+%** | room verts | buffer |
|---|---:|---:|---:|---:|---:|---:|
| Lobster Boat (12 m) | 2 | 1,384 | **158** | **+11.4%** | 474 | +24.1 KB |
| Stern Trawler (38 m) | 4 | 1,624 | **204** | **+12.6%** | 612 | +31.1 KB |
| Coastal Packet (60 m) | 4 | 2,508 | **188** | **+7.5%** | 564 | +28.6 KB |

Per level it is 14–100 triangles. Against ADR 0022's own cost table — the side dragger's mesh at
143.9 KB replacing 433.1 MiB of sheets — this is noise.

### Containment

Two tests, because "inside a triangle soup" is not well posed on a hull with inner strakes (the
classifier already met that). Both are run in the spike, both self-checked:

- **Whole-hull convex hull** — a 3D incremental hull over the deduplicated shell vertices, with a
  three-arm self-test (every input point inside; every edge shared by exactly two faces; a point
  pushed outside is caught). It refuses to report rather than pass silently: the **Stern Trawler's
  hull will not close at any tolerance in the ladder**, and is reported as unavailable.
- **Per-level section** — the convex hull of the shell's own vertices standing in that level's z
  band, in xy. Strictly the tighter test, because a boat tapers.

**Result, bow as published: 0 section violations on every level of all three hulls.** The
whole-hull test shows small excursions on three levels and none anywhere else:

| level | section | convex-hull | worst excursion |
|---|---:|---:|---|
| lobster `house_sole` | 0 / 258 | 23 / 258 | 0.087 m (**2.8 px** at 32 px/m) |
| lobster `cuddy_sole` | 0 / 216 | 25 / 216 | 0.037 m (**1.2 px**) |
| packet `house_sole` | 0 / 48 | 9 / 48 | 0.236 m |
| every other level | 0 | 0 | — |

### The frame is measured, not declared

The sidecar declares `+x starboard, +y bow, +z up`; `RigMeshExtractor` declares "rig-space, metres,
z-up". Two declarations are not a measurement, so the spike re-extrudes every level **with the bow
flipped** and reports containment both ways. The flipped control fails loudly where the hull is
asymmetric — packet `bridge_sole` **42/42** section violations at **45.95 m**, packet `main_deck`
72/258, packet `below_sole` 184/216, trawler `main_deck` 138/300 — and does **not** discriminate on
the lobster, who is near enough fore-and-aft symmetric in section that both orientations fit. So the
frame agreement is *measured* on the big hulls; the lobster is simply not a discriminating test, and
that is stated rather than glossed.

Two independent cross-checks landed on the same numbers: the lobster's `house_sole` at **0.5 m**
equals her `HullMeshDef.WatertightDeckHeightMeters` of 0.5 exactly, and the packet's at **5.0 m**
equals hers exactly. And the measured gap between the published FOOTPRINT and the sole outline is
**0.07 m** on the trawler and the packet — the rig's `WT` constant, recovered from the committed
data alone.

### ⚠ The one thing the extrusion cannot derive: the ceiling

`LAYOUT` publishes soles. It does not publish ceilings. The spike tried two ways to measure one off
the hull mesh and **both are wrong in ways only the picture caught**:

1. **Highest hull vertex over the outline** → measures the **masthead**. Lobster wheelhouse: 5.458 m
   of headroom. Packet main deck: 14.13 m. The rooms failed containment and swallowed the render.
2. **Lowest near-horizontal hull face above `sole + h`** → better, and it is what the committed
   numbers use — but `h` is a tunable and the answer moves with it. The spike prints the sweep:

   ```
   packet main_deck   0.6->1.10  0.9->1.10  1.2->1.35  1.5->1.53  1.8->1.93
   trawler main_deck  0.6->0.63  0.9->0.94  1.2->1.39  1.5->1.52  1.8->1.87
   lobster cuddy      0.6->1.88  0.9->1.88  1.2->1.88  1.5->1.88  1.8->1.88   <- finds no roof, takes the WHEELHOUSE's
   ```

   At every value in the sweep the lobster's **cuddy** — a crawl-in berth space — is given the
   wheelhouse roof at 2.12 m, and her room then stands visibly proud of her own foredeck. That is
   what panel 2 shows.

This is not a defect in the plan. It is a **missing published number**, and it is the whole of §D's
upstream ask. `docs/art/spikes/interior-mesh/interior-mesh-04-ceiling-supplied.png` is the same
mesh with that one number hand-set to 0.95 m; the room sits inside the foredeck and reads correctly.

---

## B. Does one tag serve both?

**Yes.** `TexCoord1.x` carries the level id on the interior faces *and* on the hull's own faces, and
one compare in the fragment shader does both halves of ADR 0038's swap. Measured on the lobster at
the dock pose, beam-on:

```
GATE OFF   hull + rooms  vs  hull alone            ->  0 differing pixels
GATE ON    the level's own hull faces, drawn alone ->  0 px            (the cull, asked exactly)
           the room                                ->  8,534 px
           room outside the house silhouette       ->  491 px          (containment, in pixels)
EXACTLY-ONE-LAYER-ON: TRUE
```

### The gate costs nothing, and "byte-identical picture" is not the same claim

The first version of this branch put the discard in the shipped facet fragment and argued from the
picture. That is a real per-fragment cost on every hull every frame (rule 7), and it is not what
"spike only" means once the file is on `main`. **The whole gate — the TEXCOORD1 vertex input, the
extra varying, the uniform and both discards — now lives inside
`#ifdef HH_LEVEL_GATE`, behind `#pragma shader_feature_local`, off by default.** With the keyword off
the compiled program has no extra input, no extra varying and no test; `shader_feature_local` also
means a player build in which no material enables it does not carry the variant at all. The spike
fixture enables it on the renderer's own instance material (`new Material(...)`,
`HideAndDontSave`, created per `Configure` — no asset is touched).

Measured across the variant boundary, because that is the only way the claim can be proven:

```
shipped program  vs  the gated variant at 0, same hull  ->  0 differing px
shipped program  vs  hull + ROOMS through the gate at 0 ->  0 differing px
```

The first says compiling the gate in changes no pixel. The second says the gate hides the interior
geometry rather than the geometry happening to be invisible.

Two properties fall out for free and are worth naming:

- **"THE CUT" becomes a sign test.** The rig hand-culls near walls per facing and draws a section
  lip. On a mesh, `sign(dot(worldNormal, towardCamera))` — the *same* decode `vertGuard` already
  uses for the ADR 0023 interior mask — culls the near wall at **every** heading instead of at
  eight, continuously, for nothing.
- **The room is the same UV0 layout the hull already uses**, so the facet pass needs no new vertex
  input to shade it. Side code 1 (interior both sides) keeps the sea off a cabin sole by the
  mechanism that already exists.

### ⚠ The swap alone is NOT enough, and this is the spike's biggest new finding

Turning the cabin off does not turn off the hull's **own near topsides**, which in a ¾ view stand
between the camera and a cabin sole. Measured:

| | room pixels surviving into the combined frame |
|---|---|
| the swap alone (`db` = 0) | **1,729 of 8,534 — 20.3%** |
| the swap + the rig's own per-face depth bias | **8,330 of 8,534 — 97.6%** |

The sprite path never met this because a sheet **composites over** the hull. **The mesh path already
owns the same lever:** `UV0.z` is the rig's `db`, "pull this face toward the camera", which the facet
pass subtracts from clip depth while leaving the true depth (`o.wpos.z` — the deck-occupant band and
the keyline resolve) untouched. Setting it on the room to the hull's own bounding-sphere diameter
(14.54 m on the lobster) reproduces the sprite's compositing in the depth test, per hull, with no
constant to tune. See `B-03-swap-alone-no-fore-bias.png` against `B-01-inside-the-house.png`.

### Are the 59 subsets derivable?

**From geometry, yes. From LAYOUT alone, no — one number short.**

The rule under test uses **nothing `RigMeshInteriorClassifier` already measured false** (no material
name, no normal direction, no face bias, no inset threshold): *a hull face belongs to the highest
level whose sole is at or below the face's own lowest point, among the levels whose published
outline contains its centroid in xy.* Everything **standing on** a level comes off with it.

> The first rule tried was "the face's whole z span lies between the sole and the ceiling". A
> deckhouse **wall** fails that — its top is the roof — so the wall stayed drawn and the room stayed
> invisible at 3.1% of its own pixels. Both this and the masthead ceiling were caught by the render,
> not by a number that looked wrong.

Fleet-wide, across all 24 interior-bearing hulls:

```
38,778 hull faces   7,992 tagged to a level   308 AMBIGUOUS = 0.79%
worst hull 1.40% (LobsterInshoreOpenNorthumberland)   two hulls at 0.00% (SideDragger, SternTrawlerMk2)
```

"Ambiguous" here is the honest residue: a face whose bottom is below a sole and whose top is above
it — the **deck plate itself**, which belongs to the level and to the hull at once. Under 1% is a
derivable rule, not a hand-authored one.

**But it needs the ceiling, twice over.** Besides §A's headroom, the rule cannot separate two levels
that share one sole, and two of the three hulls have exactly that:

```
⚠ TIE: main_deck + house_sole share sole z 3.5 m   (Stern Trawler)
⚠ TIE: main_deck + house_sole share sole z 5.0 m   (Coastal Packet)
```

The faces all go to whichever the def lists first — the trawler's `house_sole` gets **0** hull faces
and the packet's gets 132 only because her house also rises above the tie. A published ceiling per
level breaks the tie by construction.

---

## C. The look

Committed under `docs/art/spikes/interior-mesh/`. Every panel is the lobster at her own per-tier
zoom (`BoatHullDef.CameraWorldHeightMeters` = 23 m; the cell renders at her bake's 32 px/m, which is
the on-screen scale at that tier), heading 90°, sheet facing index 6 of 8 through the **committed**
CCW convention flag, same light, `InteriorRockScale` **0.45** on all three. Each strip is **dock on
the left, Moderate crest on the right**.

The crest is composed through the shipped path, not by hand: sea state Moderate → `seaState01`
0.4286 → `StormRockMath.StormBlend01` 0.00766 → amplitude ×1.0092 on the def's own ROCK (2.8° roll,
1.6° pitch, 1.2 px heave) → `HullMeshMath.RockPose` at crest phase 90° → **roll 2.826°, pitch 0°,
heave 1.211 px**, of which the interior draw takes 0.45. (Pitch is zero at the crest by the shipped
math's own quarter-cycle lead — it peaks at the zero crossing.)

| file | what it is |
|---|---|
| `interior-mesh-01-sprite-today.png` | **(1)** today: the committed #611 interior cell over the hull |
| `interior-mesh-02-mesh-shell.png` | **(2)** mesh shell, no fit-out |
| `interior-mesh-03-mesh-plus-fitout.png` | **(3)** mesh shell + the rig's own `['fitout','props','interact']` layers, rendered live through the editor's V8 host at the same clamped pose |
| `interior-mesh-00-side-by-side.png` | contact sheet: columns 1/2/3, row 1 dock, row 2 crest |
| `interior-mesh-04-ceiling-supplied.png` | **supplementary, and labelled as such** — panel 2 with the cuddy's headroom hand-set to 0.95 m. Not measured. It exists so the mechanism is judged and not the missing datum. |

**Two things to know before looking, both honest limits of the spike and not of the idea:**

- In panel 2 the cuddy **stands proud of the foredeck**. That is §A's missing ceiling, nothing else.
  Panel 4 is the same picture with the number supplied.
- The shell reuses **one of the hull's own materials** (id 2, her cream liner) because the spike is
  measuring geometry, not inventing a palette. A real shell would take the interior rig's own
  `SOLEW`/`CABIN` ramps and read as wood, not as white card.

**The owner judges this. This document does not.**

---

## D. Costing the hybrid honestly

### Which of the five LAYERS become geometry

`boatInteriorRig.js` names them: `['sole','shell','fitout','props','interact']`.

| layer | verdict | why, in a number |
|---|---|---|
| `sole` | **geometry** | 2–24 triangles per level; it is a polygon the def already publishes |
| `shell` | **geometry** | walls + ceiling + door aperture: 12–76 triangles; and it is the layer whose near-wall CUT the backface sign test does for free at every heading |
| `fitout` | **pixels** | see the palette budget below |
| `props` | **pixels** | ditto, and it is the layer that changes with clutter/night/lamp/weather |
| `interact` | **pixels** | it is a focus highlight over the fit-out, not a body |

### The palette budget is the hard wall, and it is measured

The facet pass declares `float4 _RampMeta[16]`, guarded in three places — the zodiac already
collided with that cap once (18 declared, 14 referenced). Measured on the committed meshes:

| hull | ramp slots her faces use | **free** |
|---|---:|---:|
| Lobster Boat | 10 of 16 | **6** |
| Stern Trawler | 12 of 16 | **4** |
| Coastal Packet | 12 of 16 | **4** |

The interior rig declares **21 ramps** (`SOLEW CABIN BIRCH SHEET QUILTL QUILTC QUILTN CUSH BRASS
SLICK CHART GLASSD GLASSN GLOW CAVITY CARPET LEATH TEAKG STONE WHITEG RUG`).

**A shell needs 3** — sole, liner, overhead — **and that fits every hull.** With glazing
(`GLASSD`/`GLASSN`) and a cavity dark it wants 6, which fits the lobster and *not* the trawler or the
packet. The fit-out at 21 does not fit anywhere without widening the shader, and widening
`_RampMeta` costs the hull-id budget the deck-occupant split spends. **That arithmetic is the whole
reason this is HYBRID and not GO.**

### What the rig must gain

**Proposed, for the art director's lane — not edited here.** A `geometry()` export beside
`render()`, publishing per level, in the same frame the sidecar already uses:

1. **`ceilingZ`** — the overhead's **underside**, in metres. *This was the blocking item;*
   **✅ SUPPLIED 2026-08-26** by the cutaway kit's pass-3 rigs, as `geometry().levels[].ceilingZ`
   with `kind: 'hard' | 'raked' | 'open'` and partial covers declared as covers. Both shared-sole
   ties (§B) are broken in the data. Batch 1 is baked and gated; see the ADR 0038 amendment.
   *Originally:* Without it
   the extrusion guesses (§A) and the tag cannot break a shared-sole tie (§B). The rig knows it: it
   draws the roof lip.
2. **`wallOuter`** — the un-inset polygon, so a wall can have its measured 0.07 m of thickness
   instead of being a single surface. Cheap and optional; the def already carries `FootprintOutline`
   for the house.
3. **`aperture`** — the door opening as a rect on a named wall edge, rather than a threshold point
   the consumer has to project. The spike's nearest-edge search lands **0.07–0.10 m** off the wall
   line on the lobster house and the packet house, and **2.06–3.89 m** off on `main_deck`,
   `bridge_sole` and the cuddy — where the threshold is a point *on the deck*, not on that level's
   wall. Guessing is what produces a door in the wrong bulkhead.
4. **The three shell ramps** (`SOLEW`, `CABIN`, one overhead) as a per-hull `MATS` slice, so the
   baker can append them to the hull's table inside the 16-slot budget.

Items 2–4 are conveniences. **Item 1 is the gate.**

### What #611's sheets are still for

- The **fit-out, props and interact** layers, at 8 facings — most of the visual interest, all of the
  art, and the part that varies with clutter/night/lamp/weather.
- The **3 hulls the S0 ledger refused**, and any hull that stays sprite.
- **The crop call gets easier, not cancelled.** The 1,238.6 → 508 MB crop lever was declined because
  a cell must "composite under the exterior 1:1". A fit-out-only sheet has far less ink than a full
  interior cell, so the same crop buys much more — but the pivot-registration law is unchanged and
  the call is still the owner's.

### Re-baking the fleet — from the record, not from feel

The compute is trivial. The **pass** is what costs.

| record | scope | elapsed |
|---|---|---|
| `#611` interior sheets | 24 hulls, 424 cells, 45 pages, on the 4060 | **59.6 s of machine time** |
| `#541` fleet mesh bake | 18 lobster variants, end to end | merged 08-15 **18:56** |
| `#543` fleet mesh bake | 5 hulls, three different rig shapes, two new extraction seams | merged 08-15 **21:00** — **2 h 04 min later** |
| `#546` placement | all 23 hulls moored for review | merged 08-15 **23:13** — 2 h 13 min later |

**Quote: ~2 hours of agent work per PR-sized batch of hulls; the 23-hull fleet went through in two
batches in one evening.** Budget one more for wiring. The honest caveat from that same record: #541
and #543 each surfaced **three defects on the way** (a mirrored azimuth probe, a descriptor that
never reached the render, three call sites dropping an optional parameter). The defects are the
cost, not the minute of compute.

And it is cheaper than that here, because **`HullMeshFleetTests.EveryCommittedHullMesh_MatchesAFresh
ExtractionFromItsRig` adjudicates a change to extraction against every committed hull without
re-baking anything.** A `geometry()` export can be landed and proven before a single asset moves.

---

## The verdict, and what it changes

### HYBRID

**Do:** the shell (sole, walls, ceiling, door aperture) becomes geometry on the hull's own mesh,
tagged by level in `TexCoord1.x`, gated by one fragment compare, pulled forward by the rig's own
`db`. **Do not:** move the fit-out. It is 21 ramps against 4 free slots, and it is where the art is.

**~~Blocked on one upstream item:~~ UNBLOCKED 2026-08-26** — the cutaway kit's pass-3 rigs publish
`ceilingZ` per level, and the lobster, trawler and packet are baked with their TexCoord1 tags and
gated at runtime. What landed is the TAG and the GATE (§B); the SHELL as geometry (§A, §D) is
still a separate lane and still wants its three palette ramps inside the 16-slot budget.

### ADR 0038 amendments this needs

1. **Proposal 3 gains a second sentence.** *"Entering a cabin does not add a sorting rule — it swaps
   which sheet is on"* is true and insufficient on a mesh hull: **the swap also has to bring the room
   forward of the hull's own near topsides**, or 79.7% of it stays hidden. Record `db` as the
   mechanism and the hull's bounding-sphere diameter as the value, so nobody invents a sorting rule
   for it later.
2. **Proposal 3's ⚠ open item — the compositing window at the frame edge — is CLOSED for the shell,
   and stays open for the fit-out.** Geometry has no compositing window; it is in world space and the
   camera clips it like anything else. The fit-out sheets keep the problem, so the ADR's warning
   survives but its scope shrinks to the layers that are still pixels.
3. **Proposal 2 gains a note.** The room's faces carry interior side code 1 in UV0.w, so the ADR 0023
   guard keeps the sea off a cabin sole by the mechanism already shipped. No second water authority,
   exactly as Proposal 2 intended — now true of geometry as well as of sheets.
4. **A new "what the levels must publish" section.** `BoatInteriorDef` gains `CeilingZMeters` per
   level. State that a level with no ceiling is **refused**, the way the builder already refuses a
   sidecar that omits px/m — the spike's two wrong ceilings both came from a consumer being allowed
   to guess.
5. **Record the count.** The ADR and the handoff both say the fleet's levels are 53. They are **59**.

### S2 follow-ups this cancels

| follow-up | fate |
|---|---|
| **door-cue bake** (8 frames, `doorOpen = k/7`, never baked) | **CANCELLED.** A sliding leaf in the aperture is a transform on a handful of faces — continuous, at every heading, no frames, and it retires the "baked at doorOpen 0" caveat in `BoatInteriors.json` with it. |
| **per-level loading** (cells lazy from `Resources`) | **KEPT**, and it gets cheaper: what loads is a fit-out sheet, not a full interior cell. |
| **crop** (1,238.6 → 508 MB) | **KEPT and re-opened at better odds** — see above. Still the owner's call. |
| the S0 ledger's **3 refused hulls** | untouched; they were never in scope. |

### If the owner says NO to the look

Nothing is lost. The shell-as-geometry finding is independent of the fit-out, and the two mechanisms
this spike proved — one tag in `TexCoord1.x`, and `db` as the fore lever — are what **R1** needs
whatever the interiors end up being made of. They are rehearsed and measured now.

---

## What this spike did not settle

Stated because an unfinished question is a finding.

- **The Stern Trawler's shell will not form a closed convex hull** at any tolerance in the ladder
  (1e-5 … 5e-3). Her per-level section containment is clean, and the section test is the stricter
  one, so the conclusion holds — but the hull itself is worth a look by whoever owns her bake.
- **Only the lobster was rendered.** The trawler and the packet were measured, not photographed. The
  extruder handles them; the pictures were scoped to the boat the owner sails.
- **The mast.** A face over the house footprint and above the sole is tagged to the house, so
  entering the wheelhouse would cull the masthead with it. ADR 0036's "the storey you are not on is
  switched off" does not obviously cover rigging. Nobody has ruled on it and nothing here does.
- **`InteriorRockScale` still does not exist on `GameConfig`.** ADR 0038 scheduled it for the runtime
  change that consumes it; the spike used the accepted default of 0.45 as a literal in the fixture.
- **The shell's palette.** Three ramps fit every hull, six fit only the lobster. Which three the art
  director wants is not a spike question.
