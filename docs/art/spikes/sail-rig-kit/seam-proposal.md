# The sail seam — a proposal for `lead-architect`, not a decision

**Question:** the fleet is mesh (ADR 0022, `3d-boat-hulls-spike-verdict`). `HullMeshDef` carries no
articulation axes, only `LevelTags`. How does a boat whose whole picture is a *continuous pose* get
across that seam?

**Status:** PR 0 of the sail rig kit takes no position in code. Nothing here is wired. This is the
measured case, the three options, and a recommendation.

---

## What is actually true, measured

`render(dir, opts)` composes `F.concat(dynamicFaces(o, pose, view))`.

- **`F` is the body** — hull, deck, coachroof, cockpit, cabin, spars, standing rigging. Built once at
  module scope; byte-identical (same array identity, same fingerprint) after renders at opposite poses.
  1,852 faces / 7,514 vertices (30) and 3,088 / 12,530 (88).
- **`dynamicFaces` is everything that answers the wind** — main, headsail, staysail, boom, sheets, lazy
  jacks, stack pack, wheel, door. `R.faces()` returns `F` alone.

> **So a mesh bake here does not freeze one sail pose. It has no sails at all.**

That reframes the question. It is not "which pose do we freeze" — it is "how does a part whose vertices
are *regenerated per pose* cross a seam built for rigid geometry".

### What an AXES model (the road fleet's `VehicleMeshDef` shape) can carry

| part | rigid? | measurement |
| --- | --- | --- |
| **boom** | **YES** | gooseneck→boom-end ground length is 3.55 m (30) / 10.6 m (88) at every awa from 0° to 172° — **spread 0.0000 m**. One rotation about the gooseneck reproduces it exactly. |
| **hoist** | no | 11 steps → **11 distinct pictures**: the luff shortens along the mast *and* the stack pack grows in the trough. Two coupled effects, not one transform. |
| **furl** | no | 11 steps → **11 distinct pictures**: the sail rolls onto the forestay and the rolled sausage wears the UV strip. |
| **the cloth** | no | `fill`, `flog`, `stall` re-camber it per (awa, aws, sheet). A rigid transform cannot make a sail belly. |
| **flutter** | no | 8 frames in irons draw 8 different silhouettes. |

So an axes model gets the boom, and nothing else that matters.

### How much picture is at stake

| | sails' share of the painted picture, close-hauled |
| --- | --- |
| sloop 30 | **27.5 %** |
| sloop 88 | **47.5 %** |

### And the ramp arithmetic, which sizes it independently

The body names **14** materials. `HullMeshDef.HullRampSlots` is **16**. The sail needs `canvas`, `sail`
and `batten` — **14 + 3 = 17**. A sail part cannot share the body's ramp table; it needs its own,
whatever else is decided.

---

## The three options

### 1 · Body-only mesh — what the fleet table would do today

Bake `F` like every other hull. No Core change, no new concept.

- ✅ Correct for a moored, stowed or motoring sloop — which is exactly what PR 2's north-shore resident
  needs, and the first thing the owner will see.
- ❌ Wrong the moment she sails: no sails at all, on a boat whose name is her rig.
- Note: **blocked today anyway** by an upstream rig defect (`HullMeshFleet.BakeBlocked`), unrelated to
  this question.

### 2 · State ladder — N baked parts, each a frozen pose

`stored · hoisting · close-hauled P/S · reach P/S · run`, each its own `HullPropMeshDef`-shaped part.

- ✅ No Core contract change. Uses machinery that exists (`IHullPropRenderer`, the outboards' path).
- ❌ It is a **5-sample approximation of two independent 11-step axes plus a continuous camber**, on a
  part that is half the 88's picture. Every tack pops. `hoist` and `furl` have no expression at all
  between the rungs — so "reef her down" and "roll the genoa away" become invisible.
- ❌ Needs its own ramp table regardless (17 > 16), so the "no contract change" saving is smaller than
  it looks.
- This is the fallback the handoff named. **It is the wrong answer for a boat whose whole point is a
  continuous pose**, and the numbers above are why.

### 3 · `SailRigDef` — keep the sails on the rig's own renderer ★ recommended

The body is a mesh (option 1). The **sails stay a pose**, drawn per frame from the rig's own
`dynamicFaces` through the existing sprite path, composited over the mesh hull, with `(awa, aws, main,
jib, hoist, furl)` driven by the game and `mode` / `heelDeg` read back.

- ✅ Preserves the entire law — camber, luff, stall, flutter, hoist, furl, the tack and gybe paths — at
  zero fidelity cost, because it *is* the law.
- ✅ The seam is small and already half-built: PR 1 has to run that pose loop every frame anyway (the
  handoff specifies it), and `heelDeg` becomes a third pose term beside heading and wave rock in
  `IsoFacetHullPresentationService`.
- ⚠️ Costs a second render path per sailing boat, and a sorting question against the mesh hull
  (`urp2d-mesh-vs-sprite-sorting` — the fleet has met this before and it is not free).
- ⚠️ Needs a Core-side contract for "this hull's picture has a posed part": either a sibling
  `SailRigDef` or a nullable field on `HullMeshDef`. **That is the decision being asked for.**

---

## Recommendation

**Option 3, with option 1 as the interim** — which is where PR 0 already leaves things: the body mesh is
specified and its bake is one upstream art fix away, and nothing has been wired that option 3 would have
to unpick.

The argument in one line: **options 1 and 2 both spend the thing the kit was built to deliver.** The
art director shipped a rig whose sails are a continuous function of the wind, sampled and documented in
a 3,500-line sidecar; a state ladder throws that away to avoid a contract change, and a body-only mesh
throws away the sails entirely.

If option 3 is refused or deferred, **say so in the PR that refuses it** and bake the ladder with the
rung count justified against the 11-step measurement above — not as an equivalent, but as a stated
approximation with its error named.

---

## What is NOT being asked

- No change to the motor fleet's engine seam.
- No change to `HullMeshDef` for any existing hull.
- Nothing about the polar, the drive or the input verbs — those are PR 1's, and none of them depends on
  this answer.

## Prerequisite, in either direction

Both rigs must be fixed upstream before ANY mesh of them can bake. They publish `geometry().ids` and
then stamp ~45 % of their faces with no level, in a vocabulary that is not `ids`. See
`HullMeshFleet.BakeBlocked` for the measurement and the ask. That is an art-side fix, not an
architecture one, and it blocks all three options equally.
