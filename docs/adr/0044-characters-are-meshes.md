# ADR 0044 — Characters are MESHES in every state; the baked 8-dir sheets retire per state, at parity

- **Status: PROPOSED** — written by the mesh-characters lane in PR 1 (`feat/character-mesh-tools`),
  amended 2026-09-10 by the skinned bake (`feat/character-skinned-assets`): §3.6 records that the
  owner took option (d) and what it measures, §3.7 the facet-pass gates
  because the owner's overrule of 2026-09-09 changed a ratified decision and the change must be
  recorded in the PR that makes it. **The seat ratifies.** Nothing here retires a sheet by itself:
  the retirements are PR 3's, one per state, each at measured parity (the ADR 0041 law).
  **Amended 2026-09-17 by the cast (`feat/cast-to-mesh`):** §7 records the owner's ruling that the
  cast follows the player onto skinned meshes, and closes §5 items 1, 3 and 4. Item 2 stays with the
  water lane.
- **Date:** 2026-09-09
- **Decision owner:** the owner ruled the scope; `lead-architect` ratifies the record.
  **`art-pipeline`** owns the facet look, **`tools-editor`** owns the baking, **`gameplay-systems`**
  owns the presenter. **`art-director`** owns `docs/art/rigs/**` — this lane widens an export
  literal in memory and pins the bytes; it never edits a rig.
- **Serves:** **P1 The Sea Has Moods** (the deck turns smoothly under her instead of ratcheting
  through 45° poses) and **P5 Cozy but with Teeth** (long calm sessions must not look broken).
- **Amends:** [ADR 0024](0024-deck-character-mesh.md) — its scope and its numbers. Extends
  [ADR 0022](0022-3d-boat-hulls.md) (the facet pipeline gains a second *kind* of occupant) and
  [ADR 0021](0021-in-engine-js-rig-baking.md) (the baker's V8 machinery bakes the poses).
  Inherits [ADR 0041](0041-full-mesh-interiors.md)'s retirement law verbatim.

---

## 1. The ruling

The lane was chartered to run a spike first. The owner overruled it, 2026-09-09 ~12:00Z, verbatim:

> *"again do we need character sprites? i overrulle a spike, lets continue with mesh tools,
> animations and characters"*

Three things follow, and they are the whole of this ADR's authority:

1. **"do we need character sprites?" is answered NO.** The mesh path is the destination for the
   character, not an alternative to the sheet path.
2. **ADR 0024's "explicitly does NOT change: locomotion" clause is overruled.** The mesh path
   extends to **all** character states — walking and running included, ashore and aboard. ADR 0024
   scoped meshes to *stationary fishing* states; that scoping no longer holds.
3. **The order of the owner's three words is the order of the PRs:** mesh **tools** first,
   **animations** second, **characters** third. This PR is the tools.

**No spike.** No further sheet bake, no sheet import, no `shovelTrail` PROPS row, no NW dig-pose
fix — all moot under the overrule.

## 2. The decision

- A character pose is a **mesh flipbook**: one small mesh per animation frame, per STATE. Heading
  is a live transform, not a baked row. (This half of ADR 0024 stands; it is the scope that grew.)
- Poses are baked from **`characterIsoRig6.js` rev 6.9** — the rig the shipped sheets are baked
  from — not from `characterIsoRig.js` pass 1, which ADR 0024 and the spike both measured.
- The bake writes **one `CharacterMeshDef` per preset** at
  `Assets/_Project/Data/Characters/<preset>.asset`, id `charmesh.<preset>`, carrying per anim → per
  frame → mesh, the shading facts, the pivot, the **pixel-adjudicated** azimuth sign, and the rig
  SHA-256 as a stale-bake guard (as `HullMeshDef` does).
- **A state, not an anim, is what a clip answers to.** The rig re-solves the whole pose per power,
  per carry stance and per rest height, so `PoseClip.State` carries the sheet baker's own
  `CharacterState.Key` spelling verbatim (`walk`, `cast_long`, `walk_buckets`, `reach_stowV`).
  `CharacterRigBaker.ExpandCarryStances` was made **public** so both paths grow the identical state
  list from the rig's own `CARRIES` — parity is not a question two recipes can answer.
- **Retirement is per state, at parity, and each one is a CAPABILITY change** (ADR 0041's law).
  The sprite presenter stays in parallel behind a per-state `MeshStates` set in the Def; a state
  moves only after the owner's eye on the plate.

## 3. What the rig-6 measurement found (and why ADR 0024's numbers are void)

ADR 0024's headline figures — *0.61–4.33% delta, 12 meshes, 4,576 tris, 411 KB* — are **all
measured on `characterIsoRig.js`, the OLD rig**. The game bakes from `characterIsoRig6.js`. Every
number below is fresh, measured in this PR on the fisher preset with the build correctly resolved,
through the standalone V8 harness (no editor slot).

### 3.1 The rig exports what the bake needs

`characterIsoRig6.js` already exports `facesOf`, `pose`, `makeMats`, `GAIN`, `BIAS`, `LN`, `BAYER`,
`ANIMS`, `BUILDS`, `CAST` and `propsOf` on its single `const API = {`. **No outer widening is
needed.** One **inner** widening is: `resolveOpts` is module-private, and it is what resolves
(build, u, power, carry, opts) exactly as the rig's own `render()` does. Re-deriving it in C# is
how a bake ships the wrong character, so `RigMeshSymbols.InnerWidenings` gains one entry anchored
on `const API = {`, inserting ` resolveOpts,`. The source bytes on disk are untouched, and pinned
by a byte-identity guard.

### 3.2 The budget

| measurement | value |
|---|---|
| `ANIMS` | **29 clips, 230 frames** (rig 6, at the time of writing; rig 7 ships **35 clips, 308 frames** — §3.6) |
| MATS declared for fisher | 36 (27 with a per-material gain, 8 with a fixed `idx`, **0 with `dith`**) |
| materials actually USED by fisher | **12** (`boot bootL brass collar hair over overD shirt skin sleeve sole stub`) |
| materials used, whole cast | 12–17 — **deckboss 17 and packer 17 exceed the `_RampMeta[16]` cap** |
| all 29 clips, fisher, flipbook | 230 meshes, 163,374 faces, 360,788 tris, 687,536 verts, **31,085 KB** |
| all 29 clips, whole cast | **317.8 MB** |
| **the PLAYER RECIPE** (44 states, 12 of them carry stances) | **334 pose meshes, 237,318 faces, 524,068 tris, 998,704 verts, 45,153 KB = 44.1 MB** |
| committed-YAML ratio (the spike asset: 833,011 B for a 411 KB buffer) | ≈2× → **~90 MB of YAML for one preset** |
| the shipped Fisher **sheets** | 44 PNGs, **1.50 MB on disk** |
| the whole committed character art | 141 PNGs, 4.24 MB |

**The budget is the wall, and it is the honest headline of this PR:** ~44 MB of GPU buffers and
~90 MB of committed YAML per preset, against 1.50 MB of PNG. Rule 7 says the performance budget is
a feature. This does not stop the tools — the tools are what let anyone argue about it with numbers
— but it is an open question for the seat (§5), not a solved one.

### 3.3 The fidelity delta: a decomposition (a PROXY) and a measurement (the oracle)

**These are two different numbers and this ADR was first written with only the first one.** The
correction belongs here because it is the lane's own:

**(a) The decomposition is a PROXY.** The table below was produced by rendering the rig against
*itself* with one expression patched out per term — a model of what each loss would cost — worst
of 20 probes (5 clips × 4 dirs), in a standalone V8 harness. It never ran the C# facet oracle. It
is a good decomposition and a bad ceiling.

| what the facet model drops | modelled delta |
|---|---|
| the head face **stamp** (a raster stamp, not geometry) | 0.00–2.82% |
| rev 6.8's per-direction `gridHead` sub-pixel nudge | 2.28–**15.00%** |
| ordered dither forced on where rig 6 uses none | 19.74–**30.21%** |
| **per-material `gain` flattened to one global** | **35.50–53.61%** |
| **all four together, modelled** | 44.85–56.63% |

**(b) The measurement.** CI run **34356893050** is the first time the C# facet oracle was ever
compared against the rig over the whole recipe — **44 states × 8 dirs = 352 probes**, one frame of
every state. Two statistics, and the distinction between them is the finding:

| statistic | min | median | max | sd |
|---|---|---|---|---|
| **shading** (inked colour differing) | 59.38% | 65.68% | **79.34%** | 4.30 |
| **outline** (opaque-vs-transparent, as % of inked) | 0.00% | 2.31% | **4.98%** | — |
| *reference:* ADR 0022 hulls, colour | — | — | 2.47–4.81% | — |
| *reference:* ADR 0024 characters, colour (on the OLD rig) | — | — | 0.61–4.33% | — |

The measured shading band sits **above** the modelled one everywhere — the real rasteriser composes
the four losses less kindly than patching them out one at a time predicts. There is no outlier in
it: the worst state (`balance`, 79.34%) is 2.2 sd above the mean of the 44 per-state worsts, the
top of one tight unimodal band, not a defect.

**The outline is the number that holds the bake to account.** Shading cannot move a silhouette; a
pose resolved against the wrong build, a heading applied twice or a mirrored turntable move it
everywhere — the mirror the sign guard rejects costs 21.8%. So the golden guard's load-bearing bar
is **outline < 8%** (above the measured 4.98%, below the 21.8% of the smallest real geometry defect
on record), and the shading band is kept only as a collapse/blow-up detector at 40–88%. **The
geometry is right everywhere; it is the shading that is 60–80% off, and that is a shader this lane
does not own.**

Read the decomposition by its biggest row. **The character does not look wrong because meshes are wrong; it
looks wrong because the facet shader carries one global gain/bias and rig 6 gives 27 of its 36
materials their own.** That is a shader widening, and it is small: `_RampMeta[m].z/.w` are free, and
filling `z = 1, w = _Bias` in `IsoFacetHullRenderer.Configure` keeps every shipped hull
byte-identical. Rig 6 also deliberately does **not** ordered-dither, so a per-object dither-mode
uniform is wanted alongside it.

Two of the four are **not** shader work, and are recorded as accepted losses or as PR 2 questions:
the head is a raster stamp (confirmed structurally — `_paint` never reads `M.idx`, so the 8 `idx`
materials colour no polygon), and `gridHead` is a per-DIRECTION nudge that a rotated flipbook
cannot carry by construction.

### 3.4 Three smaller findings

- **Deck rock enters the POSE.** `pose` reads `arguments[5]` and applies `counterLean` from
  `rock.counter`. ADR 0024's "rock remains a transform" is not true of rig 6.
- **The mesh path can carry four anims the sheet baker declines** — `swim`, `tread`, `sleep`,
  `drive` — declined only because they do not fit the 64×88 off-deck cell. A mesh has no cell.
- **ADR 0024's "zero changes to Art, Boats, Core" does not survive contact with rig 6.** Core gains
  `CharacterMeshDef`; the shader widening above is a real (and cross-lane) change.

### 3.5 The wall in §3.2 has an answer, and it arrived as rig 7 (added 2026-09-09)

**§3.2’s 44.1 MB is a FLIPBOOK number** — 334 pose meshes, one per state per frame. The rigidity
measurement that followed it said the same character is expressible as ONE mesh posed by bones (35
rigid bones, 0.000 mm drift over 308 poses), which is roughly two orders smaller (**74×**, once
baked and measured — §3.6; this paragraph originally estimated 90× from the rigidity study alone). The art director shipped
exactly that on 2026-09-09 as **`characterIsoRig7.js` rev 7.1**: a skeleton, one bind mesh, and bone
clips. It is registered as catalog key **`characterSkin`** (prerequisite `character`), and this ADR
records the claim only because the claim was **re-run in this repo**, not read off the drop:

| claim | measured, on the rigs as they sit here |
|---|---|
| the golden: bones land where the lathe lands | 10 builds × 56 golden rows × 462 frames = **4,620 frames**, worst vertex error **4.02e-13 m** against the rig’s own **1e-4 m** tolerance, **0 failing rows** |
| `renderSkinned()` against `render()` | **36,960 probes** (56 rows × 8 dirs × 10 builds), **0 moved, 0 pixels** |
| influences per vertex | **never more than two**, on any of the ten builds |
| the blend rings, adjudicated | collapsing every two-weight vertex onto its heavier bone costs **4.52e-2 m on the fisher — 452× tolerance, all 56 rows failing** (nan 2.53e-1 m, 2,533×) |

**What this does and does not settle.** It does not take §5.1 — that decision is still the seat’s.
It changes the MENU §5.1 chooses from: the question stops being “ship 334 meshes, bake them at
import, or bake them at build” and becomes “ship ONE mesh and its clips”, at **0.60 MB** per
preset against 44.1 MB (§3.6 — measured after the bake; the 0.47 MB written here first was an
estimate that had not yet paid for `boneWeights` and `bindposes`). The 17-materials cap (§5.3) and the shader widening (§5.2) are untouched by
it: an export changes how the vertices are produced, not what colours them.

⚠️ **It rides on the body, by prototype.** `CharacterIso7` is `Object.create(CharacterIso6)`, and it
names the base it re-expresses (`CharacterIso7.base === CharacterIso6.revision`). A body bump that
forgets the export costs nothing at load time and everything at draw time, so the agreement is a
CI guard (§6), not a README line.

### 3.6 Option (d) is taken, and this is what it costs (added 2026-09-10)

**The owner ruled on 2026-09-09: characters are ONE SKINNED MESH per preset — a bind mesh, a
skeleton, and one clip per `ANIMS` row.** That is option (d) of §5.1, and this PR is the bake that
makes it a fact instead of an estimate. Everything below is measured on `fisher`, the PLAYER preset,
by `CharacterSkinAssetBaker.Compose` — CPU-only, no editor slot, reproducible in CI.

#### The size, against the flipbook it replaces

| what | bytes | |
|---|---:|---|
| bind mesh geometry (pos + normal + uv0 + indices) | 138,520 | 135.3 KB |
| `boneWeights` (2,992 × 32 B) | 95,744 | 93.5 KB |
| `bindposes` (45 × 64 B) | 2,880 | 2.8 KB |
| **bind mesh, total** | **237,144** | **231.6 KB** |
| 35 clips (308 frames × 45 bones × 28 B) | 388,080 | 379.0 KB |
| **the player preset, total** | **625,224** | **610.6 KB = 0.60 MB** |
| the flipbook it replaces (§3.2) | | **44.1 MB** |
| **ratio** | | **74.0× smaller** |
| the whole cast at this rate (×10) | | **5.96 MB** |

Bake wall-clock: **2,101 ms** for the preset. The geometry: 711 faces, 1,570 triangles, 2,992
corners, 45 bones, 2,920 vertices at one weight and **72 at two**, max bone index 35.

**The 0.60 MB is the honest number and it is bigger than §3.5's 0.47 MB estimate**, because the
estimate priced the vertices and forgot what makes them skinnable: `boneWeights` is 93.5 KB, 40% of
the bind mesh, and it is not optional.

#### The fidelity, in two numbers, both of which must be quoted

The posed bind mesh was replayed against rig 6's own `facesOf` — every clip, every frame, 921,536
corner samples — outside Unity, on a standalone V8 host:

| what | measured | against the rig's own 1e-4 m tolerance |
|---|---|---|
| the transcription in double | **3.461e-13 m** | exact |
| the shipped **float32** Def, worst overall | **5.845e-5 m** | **1.71×** margin |
| the shipped float32 Def, the four `mount*` clips set aside | **3.907e-7 m** | **256×** margin |

⚠️ **The 1.71× is not the skinning; it is a rig-6 defect the skinning inherits.** The four `mount*`
clips fling `boot_R`/`boot_cuff_R` **100–356 m** from the rig origin for four or five consecutive
MID-clip frames and then cancel back — catastrophic cancellation through a 356 m lever arm, where a
float32 ulp is 3.05e-5 m. Every other clip stays inside 1.93 m. **The flipbook bakes those frames
too** (`goldenRow` walks the same `k`), so this is not a cost of the mesh route; it is a defect the
mesh route made visible. `docs/art/rigs/**` is the art-director's — **filed, not fixed here.**
Do not widen the guard to buy room: the bar is `CharacterIso7.TOL`, read off the rig.

#### ⚠️ A clip is discrete samples, not a curve

Adjacent frames step up to **178.3°** on a single bone (20 of 315 adjacent bone-steps in `walk`
alone exceed 90°). `solveAt(anim, u)` is a closed-form pose function sampled at `u = k/den`;
consecutive samples are not required to be near each other in quaternion space. **A presenter must
step frames — sample-and-hold at `FramesPerSecond` — never slerp between them, never cross-fade
between clips, never retarget through an `Animator` that assumes a continuous curve.** Smoothing has
to come from the rig authoring more frames. Guarded in §6.

#### ⚠️ Two things a skinned character does NOT bring with her

1. **No face.** The rig draws eyes, brows and mouth as a raster STAMP over the rendered figure, not
   as geometry — it is ≤2.82% of her pixels and it is not in the mesh. A mesh character is
   featureless until the presenter re-adds the stamp. This is a PR-2 obligation, not a nice-to-have.
2. **No facet pass, when she is ashore alone.** See §3.7.

#### The asset path

`Assets/_Project/Data/Characters/Skin/<preset>.asset`, id `charskin.<preset>`. The flipbook already
owns `Data/Characters/<preset>.asset` and `AssetDatabase.CreateAsset` REPLACES; a shared path would
delete the sheet Def the game currently draws from. `CharacterMeshDef` is left in place and
untouched — the two routes coexist until PR 2 chooses.

### 3.7 The riskiest unknown, asked as a test: does the facet pass draw a skinned renderer?

The mesh fleet is drawn by `IsoFacetHullFeature`, which collects subjects with a
`ShaderTagId("HHHullFacet")` renderer list. Every mesh that has ever gone through it is a
`MeshRenderer`; `SkinnedMeshRenderer` appears nowhere else in this repository. **It is two gates in
series, and one observation cannot tell them apart** — a blank frame is equally consistent with
"the pass never ran" and "the pass ran and skipped her" — so they are two tests
(`CharacterSkinFacetPassTests`).

**Gate 1 — the pass is recorded only while a mesh HULL is registered. FOUND, and it is the finding
that outranks the other one.** `AddRenderPasses` opens with `bool hulls = IsoFacetHullRegistry.Count
> 0` and returns early when no hull, water, reflector or foam wants the frame.
`IsoFacetHullRegistry.Register` (`:31`) is `internal` **and** typed to `IsoFacetHullRenderer`, with
exactly one production caller — `IsoFacetHullRenderer.cs:716`, in that component's `OnEnable`.

**The gate is not that early return, and the difference matters.** Water alone keeps the feature
alive past `:199`. What closes the door is the facet block itself: `IsoFacetHullFeature.cs:847`
opens `if (drawHulls)`, and **both** renderer lists are created inside it — the `HHHullFacet` list
**and** the `HHHullDeck` deck-occupant list, which is the one a character aboard would ride in on.
`drawHulls = DrawHulls && ResolveMaterial != null` (`:471`) is fed from `Count > 0` (`:163` →
`:247`). With no hull registered the raster pass is never added and neither list is created. So:

> **A mesh character draws through the facet path only in a frame that also carries a registered
> mesh hull.** Aboard the dory that is free. On the wharf it is nothing at all.

This is true of **both** candidate presenter paths — a CPU-skinned `MeshRenderer` is exactly as
invisible ashore as a `SkinnedMeshRenderer` — so it does not choose between them; it prices them
both. Opening the gate is a change to `IsoFacetHullFeature`/`IsoFacetHullRegistry`, which are the
**water lane's** files (§5.2 is the same boundary). **Filed for that lane, not taken here.**

**Gate 2 — given the pass IS recorded, does the list include her? MEASURED ON A GPU, and the
answer splits in two.**
The list is built with `DrawingSettings(HHHullFacet, sorting) { perObjectData = None }` +
`FilteringSettings(RenderQueueRange.all)`: no layer mask, no renderer-type discriminator — the same
API URP's own opaque pass uses to draw skinned characters, and Unity skins into a vertex buffer
*before* the draw, passing non-position streams through unchanged (so `attrs : TEXCOORD0`, which
carries the per-face facet data, survives). The necessary conditions are asserted headlessly and all
hold: the shader carries the tag, the material's queue is inside `RenderQueueRange.all`, the
renderer survives culling, `FilteringSettings` exposes no member that could name a renderer class.

**The evidence needs pixels, and CI has no GPU** — recording this pass on a null device does not
fail, it crashes the editor, so `TheFacetListDrawsASkinnedRenderer_ButNotWithTheSameFacetValues`
`Assert.Ignore`s there and **a green CI run carries no evidence about this section**. It was run on
a granted editor slot, on **Direct3D12 / NVIDIA GeForce RTX 4060**. It stands the hull up itself, so
gate 1 is known-open while gate 2 is read, and draws the same figure both ways into one 128×184
facet target sharing material, property block and transform.

| what | measured |
|---|---|
| control, **both** renderers off | **0** solid px — so the pixels below are hers, and not the hull's |
| (b) `MeshRenderer`, CPU-skinned | 3,181 solid px |
| (a) `SkinnedMeshRenderer` | 3,181 solid px |
| **silhouette** | **0 of 3,181 px lit by exactly one path = 0.000 %** |
| sabotage: `head` +90° | 96 px = **3.007 %** — the comparison can fail, so its agreement means something |
| **shading**, worst channel | **242/255**, mean 95.92/255, over 3,181 shared px |
| per channel | R 242 (mean 127.26) · G 240 (144.13) · B 234 (112.30) · **A 0 (0.00)** |

> **She is drawn, and she is drawn in exactly the right place. The facet VALUES are not the same.**

Read in that order. **Nothing about a `SkinnedMeshRenderer` keeps it out of a
`ShaderTagId`/`DrawRendererList` collection** — that was the riskiest unknown and the answer is yes,
in pixels, with a control frame proving they are her pixels. The geometry is not merely close but
exact: bindposes, the legacy `BoneWeight[]` and `SkinQuality.Bone2` land to the pixel, and the
sabotage shows the measure would have said so had they not. Then the second half: inside a
silhouette that agrees perfectly, the values written to the facet target are almost entirely
different, and the split is sharp — **alpha is byte-identical while R, G and B are unrelated**.

**⚠️ `SkinQuality.Bone2` is an obligation on the FUTURE PRESENTER, and nothing holds it today.** It
is pinned in the fixture and nowhere else: no code under `Assets/_Project/Code` creates a
`SkinnedMeshRenderer` or sets `SkinQuality` at all. A renderer left on `Auto` reads
`QualitySettings.skinWeights`, which is **per quality level, not one project default** — this
project ships Very Low 1, Low/Medium/High 2, Very High 4, Ultra 255 — so **ONE bone is reachable on
a real machine by the player's own quality choice**, reproducing §3.5's 4.52e-2 m (452× tolerance)
silently and only in the hems. `CharacterSkinDef` states the requirement — `MaxBoneInfluences` is
two, and `IsUsable` refuses a def claiming fewer — but **a Def cannot enforce a renderer setting**:
PR 2's presenter sets `SkinQuality.Bone2` explicitly and a guard asserts it. Option (b) is
unaffected; it skins in this lane's own code and uses both influences unconditionally.

**So option (a) is not available on today's shader.** Handing the presenter a
`SkinnedMeshRenderer` would draw the figure in precisely the right place carrying the wrong facet
values. *Why* is not established from here. Identical coverage with divergent values is consistent
with the shading inputs the facet shader derives from position — `wpos` feeds the dither frame —
resolving differently for a renderer Unity skins into its own space; that is a **hypothesis**, this
lane did not test it, and it must not be quoted as a result. `HiddenHarboursIsoFacet.shader` and
`IsoFacetHullFeature` are the **water lane's** files (§5.2, the same boundary gate 1 ran into).
**Filed for that lane, not chased here.** Option (b) stands, priced below.

`Assert.Greater(sh.Max, ShadingTolerance)` **pins** that divergence. It is not a bar production is
failing; it is the measured state of the world, asserted so that *fixing* it cannot pass unnoticed —
the failure message tells whoever closes the gap to invert the assertion and amend this section,
because at that moment option (a) becomes available and the presenter stops having to CPU-skin.

**Option (b) is costed regardless, because the handoff asked for the number either way.**
CPU-skinning the bind mesh — 45 bone matrices, 2,992 corners at ≤2 influences, positions and
normals, into a reused `Mesh` — is measured by `CpuSkinningTheBindMeshCostsThisMuchOfAFrame` and
reported against the 16.67 ms frame, per figure and ×10 for the cast. It is an EditMode stopwatch on
one machine: an order-of-magnitude figure for choosing a path, not a budget gate, and its assertion
is a loose ceiling that only catches a change of ORDER (four influences, or per-frame allocation).

## 4. What PR 1 landed

*(This section describes the mesh-tools PR that first wrote this ADR. The skinned bake of
§3.6 is a later PR against the same ADR; §3.6/§3.7 are its record.)*

`SpikeDeckCharacterMesh/` is retired, losslessly:

- `DeckCharacterSpikeMath` (pure, tested, 79 lines) is **promoted** to
  `Core/Iso/CharacterPoseMath.cs` with its EditMode test — it belongs to the presenter, not to the
  bake, and `DeckRideMath`'s pointer at it becomes valid instead of dangling.
- the pose extractor is promoted to `Tools/Editor/RigBaking/CharacterPoseMeshExtractor.cs`, joined
  by `CharacterMeshAssetBaker` and a `RigMeshMenu` entry ("Bake character poses").
- everything else — the spike Def, its rig MonoBehaviour, its 833 KB baked asset, three asmdefs —
  is deleted. No scene or prefab referenced its GUIDs.

`CharacterMeshAssetBaker.Measure()` walks the whole recipe, builds and destroys every mesh and
touches no `AssetDatabase`, so the numbers in §3.2 are reproducible in CI on the V8 host without an
editor slot.

**A renderer's swap is a registry re-register**, not a code change: the Def carries `MeshStates`,
this PR leaves it EMPTY, and PR 3 flips it per state.

## 5. Open questions for the seat (this ADR is PROPOSED, not Accepted)

1. **The ~90 MB of committed YAML per preset**, and ~44 MB of GPU buffers. Ship the Defs? Bake at
   import? Bake at build? This is the decision that decides whether the whole cast follows the
   player.
   **Update 2026-09-09:** still open, but the menu changed — rig 7 (§3.5) puts ONE skinned mesh
   plus clips on the table, **0.60 MB** per preset instead of 44.1 MB, measured to land on the same
   vertices to 4.02e-13 m. The choice is the seat’s; the option it was missing now exists.
   **TAKEN 2026-09-09 by the owner, and landed by this PR as §3.6: option (d), ship the skinned
   Def.** What stays open under this heading is only the CAST — §3.6’s 5.96 MB for ten presets is a
   number, not yet a ruling.
   **CLOSED 2026-09-17 (§7.1):** the owner ruled "yes make everyone a mesh now". The cast ships the
   skinned Def the player ships, one `charskin.<preset>` per preset, aboard first (§7.3).
2. **The shader widening** — `_RampMeta.zw` for per-material gain/bias, plus a per-object
   dither-mode uniform. `IsoFacetHullFeature` and the facet resolve shader are the **WATER lane's**;
   this is a boundary question, not this lane's to take.
   **Unchanged 2026-09-17:** still the water lane's. The cast amendment (§7) does not touch the
   shader.
3. **deckboss and packer declare 17 materials** against a 16-slot ramp table. The baker refuses
   above 16 rather than silently truncating. Widen the table, or split the preset.
   **CLOSED 2026-09-17 (§7.2), neither widened nor split:** the 17 was counted on rig 6's own head.
   Through the bake's composed face (#854) every preset fits, and deckboss and packer use exactly 16
   of the 16 slots.
4. **PR 2 re-lands the presenter** that the retired `DeckCharacterMeshSpikeRig` stood in for,
   behind a Core `ICharacterMeshPresenter` seam.
   **LANDED 2026-09-12 by the character-mesh-presenter PR**, with three things to record rather
   than leave as a silent difference.
   *The seam is not in Core.* It is `IDeckRiderFigure` in `HiddenHarbours.Player`. Its only
   implementer and its only consumer — `DeckRiderMeshPresenter` and `DeckRiderVisual` — are both in
   Player, so it crosses no module boundary; rule 4 asks for Core where one **is** crossed, and a
   Core interface with one implementer and one caller would be an abstraction standing in for
   nothing. If the CAST question in item 1 is ever answered yes, the seam moves to Core at that
   point. That is the seat's call, not this PR's.
   *`MeshStates` is authored here, not in a PR 3.* §4 says PR 1 leaves it empty and PR 3 flips it
   per state; an empty list means the presenter can never draw, so no toggle-1 plate could exist and
   the presenter would ship unverified. The capability switch the owner holds is therefore the other
   one: `GameConfig.MeshCharacter`, **default false** as that PR shipped it. The shipped game
   still drew the sprite. **Amended 2026-09-13 (owner ruling 2026-09-12, "ship it on"):** the
   default is now **true** — the shipped game draws the mesh aboard a mesh hull, and OFF remains
   byte-identical to the sprite path. The ruling accepts the named debt: rig 7 exports no helm
   and no oars clip, so at the wheel and at the oars she stands in a plain `idle` where the
   sprite sat and rowed; she is faceless; and she is 43–57% off the inked art until the shader
   look pass lands.
   *The `mount*` spike, measured on the rig-7 skeleton.* §3.2 names the part `boot_R`/`boot_cuff_R`;
   the exported skeleton's bone ids for it are `ankle_R`/`ankle_R_tip` — rig 7's own header calls
   `ankle_L` "the boot shaft". Walking all 35 fisher clips in a chained ClearScript V8 host (control:
   711 bind faces, the head-chain-missing shape being 454) puts the excursion at **23.7–356.7 m over
   18 frames** — `mountUp` f5–f9, `mountDown` f5–f9, `mountCab` f7–f10, `mountCabDown` f6–f9 — carried
   by 2 bones of the 45. Every one of the other 31 clips holds every bone inside **1.19 m**, so
   `CharacterSkinPose.FenceMetres = 8` sits in an empty gap rather than on a judgement call. Filed,
   not fixed: `docs/art/rigs/**` is the art-director's.
   **CLOSED 2026-09-17 (§7.4):** item 1 was answered yes, so the seam moved to Core as this item said
   it would. `IDeckRiderFigure` is now `ICharacterFigure` and `ICharacterFigureStand` in
   `HiddenHarbours.Core` (`Core/Iso/CharacterFigurePresentation.cs`), and the player's presenter and
   rider implement them with the same values. The `mount*` excursion stays filed with the
   art-director (§7.7).

## 6. Guards

- **turntable-sign oracle** — the character rig turns `th = −dir·π/4` where boats and
  `IsoFacetMath` use `+dir·π/4`. The sign is adjudicated **from pixels at bake time** with a ≥4×
  sabotage margin and stored on the Def. **The statistic is the SILHOUETTE** (opaque-vs-transparent),
  not the inked-colour diff: the facet model's own 45–57% shading delta (§3.3) drowns handedness
  out of any colour statistic — the same pair of renders reads 1.16× by colour and 11.1× by
  coverage — while shading cannot move an outline and a mirrored pose moves it everywhere. It is never declared. (`RigCatalog.CharacterKit` warns
  this lane has been CCW-mislabelled twice.)
- **golden fidelity across 8 directions** for one frame of every state, 352 probes, asserted on
  **two** statistics with the OUTLINE load-bearing: outline < 8% (measured 0.00–4.98%; a mirrored
  pose costs 21.8%), shading held only as a band, 40–88% (measured 59.38–79.34%, sd 4.30), so a
  collapse is as loud as a regression. **Not** against the hull band, which measures a different
  shader, and not against §3.3's modelled 44.85–56.63% — that is a proxy, and the real rasteriser
  sits above it.
- **`docs/art/rigs/**` byte-identity** — the widenings are in memory only.
- **pose is heading-independent** — extract at dir 0 and dir 4, assert identical face blobs.
- **the skinned export re-runs, it is not cited** — `CharacterSkinnedExportTests` (EditMode, ~1 s of
  V8) re-measures §3.5 on every CI run for `fisher` and `nan`: the prototype chain, the NAMED base
  revision, the golden inside the rig’s own tolerance, 64 render probes byte-identical, and the
  BLENDED collapse still costing >100× tolerance so the exception keeps earning itself.
- **Def completeness** and **materials-used ≤ 16**.

**Added by the skinned bake (§3.6), EditMode, `Assets/Tests/EditMode/RigBaking/`:**

- **the posed bind mesh IS the rig** — skin every vertex on every frame of every clip and compare
  against rig 6's own `facesOf`, within the rig's **1e-4 m**. This is the guard the whole option
  rests on: if linear blend skinning cannot reproduce the lathe, there is no skinned character.
- **vertex ORDER matches the rig's**, not just the vertex set — a reordering would pass a
  nearest-point comparison and ship a scrambled `boneWeights` table.
- **rig-hash identity** — the Def carries an LF-normalised SHA-256 of both rig sources; a bake
  that does not match the file on disk is stale and says so.
- **the two-weight floor, sabotaged at DEF level** — collapse every blended vertex onto its
  heavier bone and re-pose against rig 6, asserting the cost stays above **100×** the 1e-4 m
  tolerance. **Sweep EVERY clip.** The 4.52e-2 m §3.5 quotes is the max over the rig's 56 golden
  rows, and it does not live in `walk`: `walk` alone costs **3.620e-3 m**, an order UNDER the bar.
  Swept over all 35 def clips the guard measures **4.516e-2 m** on `sleep`, frame 1, face 399
  (`upper_R`), corner 2 — 451.6× tolerance, reproducing §3.5's number from the def instead of the rig.
  Then `toss` 3.389e-2, `reach` 3.295e-2, `ladderDown` 3.227e-2. Only 72 of 2,992 corners carry a
  second influence, so a guard that samples the wrong clip finds almost nothing to break.
- **the clips still STEP** — assert the worst adjacent-frame bone step is *large* (>90°), so a
  future “helpful” resample that quietly smooths them reddens instead of shipping a different
  character (§3.6). Loose delta (0.01°): float32 quantisation moves the measured angle.
- **the azimuth-sign oracle keeps a sabotage margin** — the handedness statistic must still
  separate a mirrored pose from an honest one by the margin §6 claims, measured, not asserted.
- **Def completeness** for the skinned Def — bones parented before use, one weight per vertex,
  `bindposes.Length == bones.Length`, every `ANIMS` row present.
- **the facet-pass gates** (§3.7) — gate 1 asserted headlessly and meant to REDDEN the day the
  registry opens to non-hulls; gate 2's necessary conditions headless, its evidence GPU-gated and
  skipping loudly on CI.

## 7. Amendment 2026-09-17: the cast follows the player

### 7.1 The ruling

§5 item 1 left the cast open after the player's mesh shipped ON. On 2026-09-17 (~02:00Z) the owner
answered it: **"yes make everyone a mesh now."** The nine cast presets (`CharacterRigBakeMenu.Cast`)
get the Def the player already ships (§3.6), one `charskin.<preset>` each, linked from the
character's art def (`CharacterVisualDef.Skin`).

The lane measured what the ruling can reach (§7.2, §7.3) and reported before it built anything. The
owner then chose the narrower first step: **"GO for Phase B, option (b). Villagers stay sprites
ashore."** A skipper aboard a moored boat draws as a mesh. A villager ashore draws the sprite, exactly
as today.

### 7.2 §5 item 3, measured: every preset fits the 16 ramp slots

Measured 2026-09-17 on `idle` frame 0, through the bake's bind table and the extractor the bake
uses. The first column is what the bake composes, face included (#854), and it is the column the
guard holds. The second column is the same extractor with the face layer held back, which reads rig
6's own head. §5 item 3's 17 was counted there.

| preset | composed, as baked (of 16) | face layer held back |
|---|---:|---:|
| fisher | 10 | 12 |
| ginny | 14 | 14 |
| skipper | 12 | 12 |
| nan | 13 | 13 |
| deckboss | **16** | 17 |
| packer | **16** | 17 |
| cutter | 13 | 14 |
| hand | 14 | 15 |
| boy | 13 | 13 |
| girl | 11 | 12 |

**Neither widened nor split.** Deckboss and packer have no slot to spare. The guards are in
`CharacterSkinCastBakeTests`:

- `EveryComposedMaterialTableFitsTheRampSlots` names any preset whose composed table outgrows the
  slots.
- `TheComposedRampCountsAreTheTableADR0044Quotes` holds the first column exactly, so a change to it
  must change this section in the same PR.
- `WithTheFaceHeldBackTheDeckbossAndThePackerNeedASeventeenthRamp` keeps the 17 as a control: the
  counter is shown to see past 16, not to clamp at it.
- The bake itself still refuses a 17th ramp inside `Compose` rather than truncating, and the cast
  bake fails loud on that preset (§7.5).

### 7.3 Ashore is still gated, so the owner ruled option (b)

§3.7's gate 1 stands: **a mesh character draws through the facet path only in a frame that also
carries a registered mesh hull.** Aboard, the hull under the figure is that hull. Ashore, a figure
would draw only while some mesh hull happened to be on screen, and not at all without one. Opening
the gate means changing `IsoFacetHullFeature` and `IsoFacetHullRegistry`, the water lane's files
(§5 item 2). Under option (b):

- **A `MooredBoat` skipper is wired.** It is the one cast member who stands on a facet hull, and the
  PlayMode proof is there.
- **Villagers are not wired.** `VillagerRoutine` attaches nothing, and the routine's shelter still
  owns `SpriteRenderer.enabled`. With the switch ON, a PlayMode guard loads St Peters and requires
  every villager's sprite. No villager carries a presenter or a figure, and no presenter exists off a
  moored boat.
- **The arrival keeps its staging.** The St Peters arrival re-sorts its skipper's sprite over the
  cabin room. The presenter reads the changed sort and hands the draw back to the sprite
  (`SpriteRestaged`) until the arrival puts the sort back.
- **Opening ashore is a separate charter** on the facet-hull files, not this amendment.

### 7.4 The presenter: in Art, behind a seam in Core

- **The seam is in Core** (`Core/Iso/CharacterFigurePresentation.cs`). `ICharacterFigureStand` is
  what a figure is posed from: the character, the hull, the stand point in the hull's rig metres and
  the deck bearing. `ICharacterFigure` is the figure: `DrawsInsteadOfSprite` and
  `PoseFigure(stand, aboard)`. `ICharacterFigurePresentationService.Attach(host, stand)` is reached
  through the static locator `CharacterFigurePresentation.Service`. Boats and World still declare
  Core and no Art, and their compiled assemblies reference no Art either (rule 4). The player's
  `DeckRiderMeshPresenter` and `DeckRiderVisual` implement the same seam in place of
  `IDeckRiderFigure`, and every member reads the value the player's presenter read before.
- **Art registers the service.** `CharacterFigurePresentationService` fills the locator at
  `BeforeSceneLoad` and never replaces a service already there, so a test double stays. `Attach`
  adds nothing to a character whose art def links no Skin: no presenter and no component. That is
  what makes "the sprite, exactly as today" checkable.
- **Wired through the data, not the scene.** `MooredBoat` asks Core for a figure as the last step of
  standing its skipper, after the sprite is sorted and the deck slot is claimed. A skin reaches the
  skipper only through the art def. No scene or builder changes, so the exporter does not re-run.
- **The sprite's flags.** The presenter READS `SpriteRenderer.enabled` and writes only
  `forceRenderingOff`. It gives that flag back whenever it stops, and it never clears a hide that
  something else set (`SpriteHiddenElsewhere`).
- **The same picture as the sprite.** The state is the requested stance with the gait the sprite
  plays, resolved through `CharacterSkinStateMap`. The frame comes from `CharacterSkinPose.FrameFor`
  on the game clock and the world seed (rule 5), as the player's does.
- **Where the figure stands.** `MeshCastFigure` is a child of the hull's posed mesh, on the hull's
  layer, at the deck slot's stand point, turned by the deck bearing. `IsoFacetHullRegistry` still
  takes hulls only. `FigureHull` answers only while the skipper holds a deck slot; a sprite hull
  never grants one, so there the sprite stands, as it always has.
- **It refuses by name, in order**, with no allocation per frame (`CharacterFigurePresenter.Refusal`):
  no stand; ashore; no sprite renderer; a sprite disabled, hidden elsewhere or restaged; no config;
  the switch off; no character; a suspended character (a clip player or work animator owns the
  picture); no skin; an unusable skin; not a facet hull; no clip for the state; the state not in
  `MeshStates`; the figure refused; the clip vanished; the pose refused. Every refusal gives the draw
  back to the sprite. The figure is hidden, not destroyed, and a destroyed hull releases the figure
  and the sprite together.

### 7.5 The switches

- **`GameConfig.MeshCast`** is the cast's switch, and it ships **ON** under the ruling. It is read
  live, so it flips with the game running, and OFF gives every cast sprite back exactly.
  `GameConfig.MeshCharacter` still governs the player alone (§5 item 4).
- **`MeshStates` is authored once, when a Def is created.** `CharacterSkinAssetBaker.BakeCastCli`
  bakes the player first, as a refresh that re-proves the path, then the nine cast presets. A Def it
  CREATES gets the four states the player's committed Def switched on, `idle`, `walk`, `run` and
  `balance` (`CastMeshStates`, held to `Skin/fisher.asset` by a guard). If the preset lacks a clip
  for any of them, the bake throws and names every such state. A committed Def keeps its list. An
  empty committed list is reported and never refilled, because emptying it is how a character goes
  back to sprites.
- **A link is never re-pointed.** `LinkSkin` refuses, by name, an art def that already links another
  preset's Skin. The cast bake stops at the first preset that fails, names it, and exits 1.
- **The bake output joins this PR on an editor slot.** The PR lands the code and the guards first.
  The nine new `charskin.*` Defs and the nine art-def links are added on a granted slot, by name, and
  the PR stays a draft until they are in.

### 7.6 Guards added by this amendment

**EditMode** (no editor; CI runs them):

- **`CharacterSkinCastBakeTests` (12).** The bake order: the player, then the nine in the rig's own
  cast order. A build entry, an art def and no crossed link for every preset. §7.2's table, its fit
  and its 17-control. The player's composed table checked against the committed Def's. The cast's
  states checked against the player's, each one a clip the rig bakes. The fresh-switch rules.
  **`EveryCastArtDefLinksItsOwnUsableSkin` is red until the bake output is in** (§7.5).
- **`CharacterFigurePresenterTests` (15).** A usable skin on a facet hull draws the figure and hides
  the sprite only through `forceRenderingOff`, and posing every frame reuses one figure. No skin
  means no presenter and no component. The switch off, a state the skin does not switch on, an
  unusable skin, ashore, a sprite hull and a suspended character each keep the sprite. `enabled` is
  read and never written, and a hide someone else set is never cleared. A restaged sprite takes the
  draw back until its staging returns. A destroyed hull releases both. A second `Attach` keeps one
  presenter. The registration fills an empty locator and never replaces a double.
- **`CharacterFigureSeamTests` (4).** A figure is asked for and posed through Core alone. The seam is
  compiled into Core. Neither Boats nor World declares Art, and neither compiled assembly references
  it.

**PlayMode:**

- **`CharacterMeshCastAboardPlayTests` (4).** A moored skipper wearing a skin draws as their mesh.
  The figure is a child of the hull's posed mesh at the slot's stand point, and the sprite is forced
  off but still enabled. The switch hands the draw both ways with the game running. An art def that
  names no skin gets nothing at all. Every skipper on the owners' register draws their own baked
  mesh (**red until the bake output is in**).
- **`CharacterMeshCastAshorePlayTests` (2).** With the switch ON, every villager on St Peters draws
  their sprite exactly as today, with no presenter or figure on any of them. One skinned def is a mesh
  aboard and a sprite ashore, in the same world.

### 7.7 Debts carried, not taken

The cast inherits the player's debts. This amendment takes none of them:

- the helm and oars clips, the additive rock table, the `mount*` boot excursion (§5 item 4) and the
  boy/girl noses at 32 ppm, all four in `BRIEF-2026-09-17-rig7-helm-oars-rock-mount-and-noses.md`
  for Claude Design;
- the shader look pass (43–57 % off the inked art), a separate handoff;
- the shin-clamp re-bake.
