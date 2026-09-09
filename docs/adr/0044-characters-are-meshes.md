# ADR 0044 — Characters are MESHES in every state; the baked 8-dir sheets retire per state, at parity

- **Status: PROPOSED** — written by the mesh-characters lane in PR 1 (`feat/character-mesh-tools`)
  because the owner's overrule of 2026-09-09 changed a ratified decision and the change must be
  recorded in the PR that makes it. **The seat ratifies.** Nothing here retires a sheet by itself:
  the retirements are PR 3's, one per state, each at measured parity (the ADR 0041 law).
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
| `ANIMS` | **29 clips, 230 frames** |
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

### 3.3 The fidelity delta, decomposed

Measured against the rig's own raster, per direction, per frame:

| what the facet model drops | delta |
|---|---|
| the head face **stamp** (a raster stamp, not geometry) | 0.00–2.82% |
| rev 6.8's per-direction `gridHead` sub-pixel nudge | 2.28–**15.00%** |
| ordered dither forced on where rig 6 uses none | 19.74–**30.21%** |
| **per-material `gain` flattened to one global** | **35.50–53.61%** |
| **all four together — the facet model as it stands today** | **44.85–56.63%** |
| *reference:* ADR 0022 hulls | 2.47–4.81% |
| *reference:* ADR 0024 characters (on the OLD rig) | 0.61–4.33% |

Read the table by its biggest row. **The character does not look wrong because meshes are wrong; it
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

## 4. What this PR actually lands

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
2. **The shader widening** — `_RampMeta.zw` for per-material gain/bias, plus a per-object
   dither-mode uniform. `IsoFacetHullFeature` and the facet resolve shader are the **WATER lane's**;
   this is a boundary question, not this lane's to take.
3. **deckboss and packer declare 17 materials** against a 16-slot ramp table. The baker refuses
   above 16 rather than silently truncating. Widen the table, or split the preset.
4. **PR 2 re-lands the presenter** that the retired `DeckCharacterMeshSpikeRig` stood in for,
   behind a Core `ICharacterMeshPresenter` seam.

## 6. Guards

- **turntable-sign oracle** — the character rig turns `th = −dir·π/4` where boats and
  `IsoFacetMath` use `+dir·π/4`. The sign is adjudicated **from pixels at bake time** with a ≥4×
  sabotage margin and stored on the Def. **The statistic is the SILHOUETTE** (opaque-vs-transparent),
  not the inked-colour diff: the facet model's own 45–57% shading delta (§3.3) drowns handedness
  out of any colour statistic — the same pair of renders reads 1.16× by colour and 11.1× by
  coverage — while shading cannot move an outline and a mirrored pose moves it everywhere. It is never declared. (`RigCatalog.CharacterKit` warns
  this lane has been CCW-mislabelled twice.)
- **golden fidelity across 8 directions** for one frame of every state, reported against the
  measured pipeline delta of §3.3 — **not** against the hull band, which measures a different
  shader.
- **`docs/art/rigs/**` byte-identity** — the widenings are in memory only.
- **pose is heading-independent** — extract at dir 0 and dir 4, assert identical face blobs.
- **Def completeness** and **materials-used ≤ 16**.
