# The exterior half of a boat interior — the S0 spike, and the question it leaves open

**Status: MEASURED, awaiting a ruling.** Nothing here changes code. It is the evidence the
coordinator asked for before the placement pass wires a single hull, because the answer is an
**art/mesh contract change** and that is not the placement pass's to decide.

**This is a SOURCE-LEVEL spike**, run in a cloud container with no Unity and no LFS objects. It
says so wherever that limits it — see *"What could not be measured here"* at the end, which names
the gaps rather than guessing across them. Two things were measurable anyway, and directly: the
committed hull meshes (`*.asset` is `unity-yaml`, not `lfs`) and the rigs (pure JS on these
paths, so they run in node's V8).

**Related:** ADR 0038 (boat interiors — the four ruled behaviours) · ADR 0036 (interior levels as
layers) · ADR 0022 (3D boat hulls) · ADR 0023 (the per-face interior mask) ·
`docs/art/boat-interior-sheets.md` (the 24 baked sheets) ·
`docs/art/proofs/boat-interior-exterior-half-log.txt` (the run) ·
`docs/art/proofs/_boatInteriorExteriorHalfProof.mjs` (regenerate it).

---

## The question

`BoatInterior` (#622) is a **layer swap**: ADR 0038 proposal 3, carried verbatim from ADR 0036 —
*exactly one of {the exterior's interior-facing draw, the interior} is on at a time*, off and not
sorted behind. The component takes that exterior half as a `Renderer` and switches it.

**There is nothing in the repository to hand it.** ADR 0038 proposal 3 says "the cabin's occluding
sheet turns **off**", and no such sheet exists on either path:

- A **mesh** hull is one `Mesh` drawn through one `IsoFacetHullRenderer` (an off-screen `FacetMesh`
  plus one in-scene `HullOverlay` quad). The house is facets inside that mesh.
- A **sprite** hull is one directional sheet plus oar/motor overlays. `BoatVisualDef` has no
  house or cabin layer either.

`ExactlyOneLayerOn` therefore reports `false` on every hull — which #622 already documents as the
honest answer for a half-wired hull, and which is exactly the flag that sent this spike.

**And the sprite path is not a way out.** All 24 cleared hulls declare `Variant = Mesh` in their
committed `BoatVisualDef`, and **23 of the 24 carry `hasBakedSheet: false`** in `HullMeshFleet` —
the five ships and the eighteen lobster variants have no sprite compass at all, and never did. Only
`lobsterBoatIsoRig` is `Sheeted`, and she too draws as a mesh.

---

## What was measured

One hull end to end (the lobster boat — ADR 0022 phase 4's hull, and the owner's first A/B), then
the finding cross-checked on the side dragger and the stern trawler. Every number is the rig's own
rasteriser, run verbatim in the same V8 the bakers use. Full run in the proof log.

### 1 · The baked meshes carry no facet groups. Read off the committed bake, not inferred.

`*.asset` is `unity-yaml` in `.gitattributes`, so the real baked meshes are readable text even
with no LFS and no Unity. **All 34 committed hull meshes**, measured directly:

| | |
|---|---|
| submeshes | **1** — on every one of the 34. There is no group to toggle. |
| vertex layout | **one layout fleet-wide**: `Position×3 Normal×3 TexCoord0×4`, stride **40 B** |
| `TexCoord0` | full — `(materialId, faceBias b, depthBias db, per-side interior code)`; ADR 0023's classifier already owns `.w` |
| **free channels** | `Tangent, Color, TexCoord1…TexCoord7` — **nine of them, on every hull** |
| fleet size | 133,852 baked vertices → **+1 float32 per vertex costs 522.9 KiB fleet-wide** |

And upstream of the bake:

| where | what it carries |
|---|---|
| `RigFace` (extraction) | `V`, `Mat`, `B`, `Db`, `FixedInPose` — **no group, name, or tag** |
| material names | do **not** separate — the lobster's `cream` is 111 faces spanning x ±2.14 m, y −6.0…3.6 m: the topsides *and* the house. Dragger 80, trawler 85, same story. |
| `IHullMeshRenderer` (the presenter seam) | `HeadingDirUnits`, `RollDegrees`, `PitchDegrees`, `HeavePixels`, `RidePixels`, `IsConfigured`, `SetSorting`, `SetDeckOccupant`, `DeckOccluderId`, `DeckOccupants` — **no visibility or per-group toggle of any kind** |

The sibling seam is worth noting, because it means R1's new member is a shape the architecture
already has rather than a novel one: **`IHullPropRenderer` carries `bool Visible { get; set; }`**,
for exactly the reason R1 needs it — *"drawn or not, without tearing the fitting down … rebuilding
a renderer per state would allocate every time the owner trims his engine (rule 7)."* A hull's
level toggle is the same idea one level up.

That last row is not a new finding; it is `RigMeshInteriorClassifier`'s own recorded result
("the dory's entire palette is `{wood, iron}`"), reproduced on the hull this pass would wire.

The rig's **source** does group them — the lobster's `// ---- WHEELHOUSE` / `EXTENDED HARDTOP`
block builds all 41 of her house faces — but the extractor reads `Global.F` and that structure is
gone by the time anything is baked.

### 2 · The sidecar's published geometry cannot name the subset either.

Scoring the rig-published `HOUSE` envelope (`soleZ 0.5, roofZ 2.96, yAft 0.55, yFwd 3.6,
hx 1.5→1.08`) as a containment rule against the 41-face ground truth:

| tolerance | picked | TP | FP | FN | precision | recall |
|---|---|---|---|---|---|---|
| 0.00 m | 13 | 4 | 9 | 37 | 0.31 | 0.10 |
| 0.05 m | 27 | 11 | 16 | 30 | 0.41 | 0.27 |
| **0.12 m** | 37 | 17 | 20 | 24 | **0.46** | **0.41** |
| 0.25 m | 48 | 17 | 31 | 24 | 0.35 | 0.41 |
| 0.40 m | 53 | 17 | 36 | 24 | 0.32 | 0.41 |

Best case is fewer than half right, missing three-fifths. **The reason is structural, not a
tolerance to tune:** the published `HOUSE` is the envelope the *room* was measured in, not the one
the *drawn house* occupies. Her wheelhouse reaches x ±1.78 against `hxAft` 1.5 and y −5.81 against
`yAft` 0.55, because the extended hardtop runs aft over the whole cockpit on two posts. An envelope
cannot bound geometry it was never measured to bound. (Sweeping the tolerance is the same
continuum-not-a-split that already defeated the inset threshold for ADR 0023.)

Rendered, the mis-cull is not subtle — **9.1% (N) / 15.5% (E) / 22.0% (S)** of the composited
image differs from the ground-truth composite, and the residue is a solid slab of un-culled
wheelhouse standing behind the room (proof sheet B, column 2).

### 3 · The existing per-pixel machinery does not isolate it. (Tried, and measured false.)

The deck-occupant split already answers "is this facet nearer than this rig-local point?" per pixel
(`i.wpos.z < _DeckOccupant[k].x`), with no facet groups and no re-bake — so it looked like a free
answer. It is not: a plane is not a house.

| plane at | recall | precision | collateral pixels |
|---|---|---|---|
| the house sole centroid | 0.68 – 1.00 | 0.36 – 0.44 | 9,187 – 22,353 |
| the door sill | 0.94 – 1.00 | 0.35 – 0.52 | 9,783 – 22,387 |

It does remove the house, and takes 22–58% of the boat with it — the near topsides, the foredeck,
the near rail.

### 4 · Not culling is not an option: the interior is *wholly* behind the exterior.

`I \ E` — interior-sheet pixels falling outside the exterior's silhouette:

| hull | levels × facings sampled | `I \ E` |
|---|---|---|
| lobster boat | 2 × 8 | 0 – 56 px of 3,722 – 11,953 (≤ 0.5%) |
| side dragger | 3 × 3 | **0** |
| stern trawler | 3 × 3 | **0** |

A swap that turns nothing off shows nothing. Turning the *whole* exterior off instead is one line
to wire and costs the boat: `I` is 9,504 – 11,953 px against `E` at 28,597 – 46,006 — 70–80% of her
image gone, leaving a room floating on the sea.

### 5 · ⚠️ The occluder is per **LEVEL**, not per hull — and that is new.

ADR 0038 proposal 3 reads as one occluder per cabin. The shipped data is not shaped that way. All
24 sheets carry 2–3 levels (19 × `house,cuddy`; 5 × `bridge,house,below`), and on the lobster's
**`cuddy`** level the house is not the occluder at all:

```
  cuddy  E   I&Hp = 0     cuddy  SE  I&Hp = 0     cuddy  S   I&Hp = 0
  cuddy  SW  I&Hp = 0     cuddy  W   I&Hp = 0
```

Five of eight facings share **not one pixel** with the house: the cuddy is under the *foredeck*.
Culling the house with the cuddy sheet on draws the berth *and* deletes the wheelhouse (proof
sheet B, column 3). What the runtime needs is an exterior subset per **(hull, level)** — **53**
across the 24 cleared hulls (19 × 2 levels + 5 × 3), not 24. That is the same 53 whose × 8 facings
make `BoatInteriors.json`'s 424 cells.

### 6 · What the design actually wants, and does get, when the subset is named.

Ground-truth cull + that level's sheet is right, at every facing sampled: the room sits in the
boat, the near bulwark still correctly occludes the part of the sole behind it (that is the
sidecar's own *"composites under the exterior 1:1"* working as intended), and the silhouette loses
exactly the house. **ADR 0038's design is sound. What is missing is a name for the subset.**

---

## The options, cheapest first

**R1 — the rig names it, the baker carries it, nothing guesses.**
The grouping already exists in the rig source. Ask art-director for a per-face level tag on the
exported faces — `f.lvl = 'house'`, the same shape as the `f.b` / `f.db` the rigs already carry.
Then `RigFace` gains one field, `RigMeshBuilder` packs it into **one of the nine channels every
baked mesh already leaves free** (`TexCoord1.x`, or `Color` if a byte will do — `TexCoord0` is
full), and `IHullMeshRenderer` gains one member (`SetHiddenLevel`). No geometry moves, so the
pixel acceptance is a re-bake whose "hide nothing" state is byte-identical to today.
*Cost, measured rather than estimated: **522.9 KiB** of vertex data across all 34 hulls, plus an
export-contract change, a fleet re-bake and a facet-shader change — the last three in the art
lane.* It is the only option measured here that is both correct and per-level.

**R2 — land the placement pass without the exterior half, honestly.**
Wire everything the def states — sliced cells in `BoatInteriorKit.CellIndex` order, levels, doors
as ordinary `IInteractable`s with their thresholds projected, per-hull PPU with the builder
refusing an omission, the cue timings, the interior renderer parented as `BoatInterior`'s own doc
requires — and leave `_exterior` null. `ExactlyOneLayerOn` then reports `false` **by design**,
which is the answer #622 already defines for a hull given only half a swap. The door presses, the
cue runs, the level resolves from the sill, the state survives a region hop, and the tests pin all
of it. When R1 lands, one `Configure` argument stops being null and 24 hulls light up with no
other change.
*Cost: a slice that is complete except the picture.*

**R3 — ship the geometric rule. Do not.**
P 0.46 / R 0.41; 9–22% of the composited image wrong; a slab of un-culled wheelhouse standing
behind the room.

**Recommendation: R2 now, R1 as the follow-on**, because R1 is upstream work in another lane and
R2 leaves nothing to redo when it arrives.

---

## What could not be measured here

Named, not guessed across. This container has no Unity and no LFS objects.

- **Anything needing the compiled game.** No EditMode or PlayMode run, and no compile against
  Unity assemblies. The runtime behaviour of a cull is *argued* from the source and *measured*
  only in the rig's own rasteriser.
- **The shipped interior sheets.** `Assets/_Project/Art/Boats/Interiors/*.png` are LFS pointers
  here, so no shipped sheet pixel was read. Every interior pixel above is re-rendered from
  `boatInteriorRig.js` — the same renderer `BoatInteriorSheetBaker` drives, pinned by
  `BoatInteriors.json`'s own `interiorRigSha256` — but it is a re-render, not the shipped bytes.
- **How the facet shader behaves with a cull applied.** Not run: no GPU, no Unity.
- **21 of the 24 cleared hulls.** The lobster was rendered end to end and the per-level finding
  cross-checked on the dragger and trawler; the rest were not rendered. The *mesh* measurements
  in §1 do cover all 34 committed hulls, because those are read off disk.

One thing that *is* proven rather than assumed: the harness's source transform is **inert to the
render** — pristine rig against harness-loaded rig, byte-identical at all 8 facings, re-checked
on every run (log §0a). If it ever stops being true the harness says so in place of the numbers.

---

## Still open, and NOT closed by this spike

- **The compositing window at the frame edge** stays open exactly as ADR 0038 records it. Nothing
  here touches it, and this document is not permission to close it silently.
- **Rest anchors aboard** (ADR 0037) are a later slice — they touch the save. Logged, not built.
- **Deck-occluder ids on entry** belong to `DeckRiderVisual` in the Player lane. ADR 0038 proposal
  3 names the write; this spike does not make it.
- The three **refused** hulls (2 sport fishers REFUSED-PIN, 1 cape FORKED-RIG) are untouched. The
  S0 ledger is the truth and nothing here bakes for or wires one.
