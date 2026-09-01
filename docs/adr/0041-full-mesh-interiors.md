# ADR 0041 — Full mesh interiors: the room becomes geometry, and gets a palette of its own

- **Status:** **Accepted — rolling out.** PR 1 (#688, the lobster) merged 2026-08-30 with the
  owner's eyeball passed 2026-08-29; PR 2 (#690, the cape) merged 2026-09-01 on the owner's
  approval. The sprite-sheet interior system still draws every unconverted hull. Rollout record at
  the bottom of this file.
- **Date:** 2026-08-29
- **Decision owner:** `lead-architect` (a shipped-shader change plus a new Core field on
  `HullMeshDef` — CLAUDE.md rule 4). `gameplay-systems` owns the extraction and the bake;
  `art-pipeline` owns the look and the upstream asks.
- **Serves:** **P2 "Dory to Dynasty"** — a boat you can go inside is a boat you can own rather than
  drive; and **P5 "Cozy but with Teeth"** — the cabin is the cozy half, and it has to hold up at the
  camera distances the owner actually plays at.
- **Flagged from:** the owner, 2026-08-28: *"the hybrid mesh does not work, we need full mesh
  interiors."* That supersedes the interior-mesh spike's HYBRID verdict (shell as mesh, fit-out as
  sprites) and the coordinator's recommendation of the same day.
- **Related:** `0038-boat-interiors.md` (the system this replaces the rendering half of),
  `0022-mesh-hulls.md` (the fleet is mesh), `0023-displaced-water-surface.md` (UV0.w, the *other*
  meaning of "interior"), `0036-interior-levels-as-layers.md`, `0031-keyline-retired.md`.
  Measurements: `docs/design/spikes/interior-palette-census.txt`,
  `interior-geometry-view-dependence.txt`, `interior-palette-cost.txt`, and the spike that preceded
  all three, `interior-mesh-verdict.md`.

---

## Context

ADR 0038 draws a boat's interior as a **sprite sheet** composited over her hull: 24 hulls, 424 cells,
45 pages, 9.83 MiB, and 1.24 GB of RGBA32 if anything ever held them all resident. It works, it
shipped, and the owner has now ruled it out in favour of geometry.

The spike (#644) had already answered *can the shell be geometry* — yes, at +7.5–12.6% tris, straight
out of committed `BoatInteriorDef` data — and had banked the two levers that make a room readable
inside a hull: the per-face **level tag** in `TexCoord1.x` behind
`HH_LEVEL_GATE` (declared `shader_feature_local` in the spike, and **`multi_compile_local` now** —
the hull's material is built at runtime with `new Material(...)`, so nothing in the project carries
the keyword and the variant stripper would drop the gated program, making the cutaway work in the
editor and silently never in the player), and **`UV0.z = db`**, which reproduces the sprite path's
"composite over the hull" inside the depth test (a revealed room survives at 97.6% with it and 20.3%
without).

What it could not answer was the **fit-out**, and the reason was one number: the facet shader
declares `float4 _RampMeta[16]`, the hulls already spend 10–13 of it, and the interior rig declares
far more than the remainder.

## The three things that had to be measured first

**1. How big is the palette wall, really?** Counting distinct ramp *colour arrays* rather than names,
over every hull × level × facing (`interior-palette-census.txt`):

| | hull ramps | interior distinct | one table would hold |
|---|---|---|---|
| cape | 10 | 21 | 31 |
| lobster / lobvar | 11 | 22 | 33 |
| dragger / trawler / trawler2 / packet | 12 | 21 | 33 |
| **tanker** | 13 | 21 | **34** |
| sport53 / sport90 | 12 | 15 | 27 |

So **widening `_RampMeta` to 24 or 32 cannot work at either size.** Two further facts fell out:
deduping hull ramps against interior ramps saves **nothing** (measured union == sum on every hull —
the interior's `t()` grimes every ramp, so none matches the hull's), while four name pairs *inside*
the interior set are byte-identical on every hull (`cab=wains`, `daylight=glass`, `flame=screen`,
`iron=panel`) and collapse for free.

**2. Is the rig's geometry even bakeable?** (`interior-geometry-view-dependence.txt`)
Yes, and the rule is exact. The vertices are **pose-free** — identical under roll 6°, pitch 3°, heave
0.4 m. The facing dependence is **pure culling**: over eight facings the per-face appearance
histogram is quantised to k ∈ {3, 5, 8}, with nothing at 1, 2, 4, 6 or 7. And every k=3 face carries
the `cut` material while `cut` appears at no other k — 129 faces fleet-wide, all of them the rig's
hand-drawn per-facing **section lip**.

**3. What does each palette design cost?** (`interior-palette-cost.txt`, compiled on the shipped
target.) Widening is **byte-identical in the compiled program** — it costs 512 B of constant buffer,
not instructions — but every hull carries it in every frame. A second table scoped inside
`HH_LEVEL_GATE` costs 384 B and ~148 B of fragment code, and only on hulls that have a room.

## Decision

**1. The room is geometry, shell and fit-out, extracted from `boatInteriorRig.js`'s own `build()`**
— the union over all eight facings, with `cut` dropped and the facet shader's continuous
`sign(dot(worldNormal, towardCamera))` doing the near-wall cut instead, at every heading rather than
at eight.

**2. The interior gets its own ramp table, `_RampMetaInterior[24]`, in its own index space** —
selected per fragment by `lvl.y`, the interior flag the bake already writes and `HHLevelDiscards`
already reads. No sub-mesh, no extra draw call.

The hull's `float4[16]` is **not widened**. That cap is guarded in three bake suites, and the road
fleet's #668 night-lamp slot-reuse ruling rests on it explicitly ("one `_RampMeta` slot carries
whichever the build names, with no colour merged and **no uniform widened**"). Widening would have
falsified those guards' stated reasons and handed the road fleet headroom it was deliberately denied.
Two separate tables also mean neither side can starve the other: a room that outgrows 24 cannot be
rescued by spending the hull's 16, and the bake refuses rather than borrowing.

**3. The rig's three procedural surface generators are transcribed into the shader**
(`plankTex`, `boardTex`, `quiltTex`), carried per face in `TexCoord2` alongside the rig's own
per-vertex uv. Measured, 35.5% of the lobster's baked room carries one — 28.6% of her wheelhouse and
63.4% of her cuddy — so a mesh that dropped them would lose most of the surface of a berth space and
could not be called parity.

## The consequences, including the ones that cost something

**A converted hull keeps `HH_LEVEL_GATE` on permanently.** This is the sharpest edge of the design
and it was found by measurement, not by reasoning. The room's faces live in the hull mesh, and the
only thing that hides them is `HHLevelDiscards`, which exists only inside that keyword's `#ifdef`.
The pre-existing rule — "on only while a cut is live" — was correct while no mesh contained interior
geometry and is wrong now: with the keyword off, the lobster drew her cabin through her own topsides,
in the hull's palette, at **31–42% of her inked pixels** in single clusters of 11k–15k px.

So the cost statement is **per hull, not fleet-wide**: an unconverted hull's compiled program is
byte-identical to before (1362 / 1878 vertex/fragment, measured either side of the change), and a
converted hull pays the gate's per-fragment discard always. That price is the same whichever palette
design had been chosen; it is the price of the room being geometry at all.

**Tris.** The lobster: hull 1428 + room 1014 = **2442 (+71.0%)**, 251.9 KB. Far above the spike's
shell-only +11.4%, and far below anything that matters at this scale — but it is a per-hull number
that belongs in each rollout batch's PR body (rule 7), because a 60 m packet's room is not a lobster
boat's.

**The sheets retire per hull, at parity, and not before.** `RigMeshAssetBaker.MeshInteriorHulls` is
the rollout's only switch: a batch adds its hulls, re-bakes, and retires those hulls' sheets once the
pictures agree. A hull on that list whose sheets are still wired would draw her cabin twice.

## What this found upstream, and did not fix

**⚠️⚠️ The rig's per-plank hash is dead code, in the sprite path too.** `plankTex` and `quiltTex`
both branch on `hash2(...) < 0.5`, intending a per-plank and per-cell coin flip. `hash2` ends with
`((h ^ (h >> 16)) >>> 0) / 4294967296`, and in JS `>>` coerces to int32 and sign-extends — so bit 31
of `h ^ (h >> 16)` is the sign bit xored with itself, always 0, and the value can never reach 0.5.
Measured in the repo's own V8 over a ∈ [−40, 40] × b ∈ [−20, 20]: **3321 of 3321 samples below 0.5,
max 0.49996**. `plankTex` therefore never returns −1 and `quiltTex` never returns +1.

The shader transcribes **what the rig does, not what it meant** — transcribing the intent would make
the mesh disagree with the shipped sheets on every plank, which is the opposite of parity. This is an
upstream report for the art director, not a fix: the sprite art is shipped, and if `hash2` is
repaired both paths move together.

**The shipped sport-fisher rig does not publish `loft`.** `docs/art/rigs/sportFisherIsoRig2.js` — the
rig the game draws — lacks the block the interior rig reads, so her room binds only from the kit's
`hull-rigs/` copy. Her rollout batch needs that export first.

**Rigs still without a `geometry()` export** cannot be converted at all: the extraction refuses a
hull whose exterior rig publishes no level vocabulary, because a room tagged level 0 is a room that
is never cut and never hidden.

## Owner's eye — what PR 1 is asking

1. The lobster's wheelhouse and cuddy, mesh against sprite, at several headings.
2. A deliberate close-up pair posing the **`dith`** question — the mesh path does not model the rigs'
   per-material dither weight, a known fleet-wide gap.
3. One frame showing the **mast**, which is tagged to the house and is therefore culled when the
   house is: nobody has ruled on whether that is right.
4. Whether the room's **depth shift** (14.542 m on the lobster — her bounding-sphere diameter, which
   is what puts her cabin in front of her own near topsides) reads correctly at every heading.

## Alternatives rejected

- **Widen `_RampMeta` to 48.** Fits, and costs nothing in instructions — but re-opens a settled fleet
  law and its three guards for a benefit the scoped table also delivers.
- **A second table by material slot / sub-mesh.** The handoff's own framing of candidate (b). Costs
  one extra draw per hull with a visible room and splits a mesh that is `subMeshCount = 1` on 24/24
  hulls today, to buy exactly what a per-vertex tag already in the data buys for free.
- **Keeping the fit-out as sprites** (the spike's HYBRID). Ruled out by the owner.
- **Baking the procedural texture into geometry** by subdividing faces. Multiplies the tri count to
  reproduce three pure functions of (u, v) that fit in a dozen lines of HLSL.

## Rollout record

Per-hull numbers, recorded as each batch lands (the "belongs in each rollout batch's PR body"
promise above, kept here so the fleet's cost is one table rather than an archaeology of PR bodies).
`RigMeshAssetBaker.MeshInteriorHulls` is the switch; the parity fixture derives its hull list from
it, so a batch that adds a hull without its evidence reddens.

| hull | PR | merged | room faces | interior ramps (cap 24) | tris hull + room | Δ tris | closed-up parity | cutaway reveal |
|---|---|---|---|---|---|---|---|---|
| lobster | #688 | 2026-08-30 | — (PR 1 end to end) | 22 | 1428 + 1014 = 2442 | +71.0% | owner eyeball passed 08-29 | — |
| cape | #690 | 2026-09-01 | 450 | **18** | 1126 + 980 = 2106 | +87.0% | 0 px vs room-stripped control, 4 headings | 19.6–28.5% vs 4% floor |

**Census correction (the cape).** The palette census above projected 21 distinct interior ramps for
the cape; her baked room paints **18**. The decision the census drove is untouched — 10 + 18 = 28
still does not fit a widened 16, and the scoped 24-slot table holds with headroom — but the census
column is a projection and this table is the measurement.

**Split out of the cape batch:** her sheet is a PRE-RIG 8-facing hand-export with the legacy CCW
flag (not the lobster's staleness pattern) — its 8→32 CCW→CW migration is **PR 2b**, charter seeded
in #690's body. Upstream asks recorded there: the cape rig exports no GAIN/BIAS/LN and no real
F/MATS symbol (her reconstruction goes stale silently if her paint changes).

**Still owed before boat-lights PR 2 rides on a converted hull:** lamps-over-mesh-interiors has no
measurement — #690's parity fixture takes the ToSetup path, so no `BoatLamps` exists in either arm
and the un-gate is structurally unclaimed.
