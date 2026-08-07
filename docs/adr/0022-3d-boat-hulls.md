# ADR 0022 — Large boat hulls become real-time 3D meshes, baked from the same rigs, coexisting with sprite hulls

- **Status:** **ACCEPTED 2026-07-20** by the owner ("adr 22 the new 3d is the highest priority right now.
  i want them in game"). Ships no code itself. Records a decision measured by the `spike/3d-boats` harness
  (not merged) and the images at `scratchpad/3dspike/`. The pipeline work is separate and phased
  (§ Migration); phases 1 and 2 are in flight.
- **Date:** 2026-07-19 (proposed) · 2026-07-20 (accepted)
- **Decision owner:** lead-architect. **art-pipeline** owns the facet shader and the look; **tools-editor**
  owns mesh extraction in the baker; **gameplay-systems** owns the hull presenter seam and heading consumers.
- **Serves:** **P1 "The Sea Has Moods"** (continuous rocking and heading, instead of quantised frames) and
  **P2 "Dory to Dynasty"** (a fleet of large hulls becomes affordable at all).
- **Amends:** [ADR 0004](0004-perspective-and-scene-strategy.md) — the ¾ iso perspective is unchanged, but
  hulls are no longer necessarily 2D sprites. **Extends, does not replace,**
  [ADR 0021](0021-in-engine-js-rig-baking.md) — the baker survives and gains a second output format.
- **Supersedes for large hulls only:** the 32-facing and rock-frame decisions inside ADR 0021 §2.

---

## Context

ADR 0021 made sprite sheets an editor operation: Unity runs the art director's `.js` rigs and bakes facings.
That shipped and works — the punt golden master is **byte-identical** to the hand-exported sheet, and the
lobster boat sails at 32 facings.

**It does not scale to large hulls.** The side dragger (Tier 4, ~25 m) has a **896 × 792** cell — 2.707 MiB
per cell uncompressed:

| layout | cells | RGBA32 |
|---|---:|---:|
| 32 facings, base only | 32 | 86.6 MiB |
| **32 facings × 4 rock** | 160 | **433.1 MiB** |
| 32 facings × 8 rock | 288 | 779.6 MiB |
| **64 facings × 4 rock** | 320 | **866.2 MiB** |

⚠️ Quote these figures, not the "454 MB / 908 MB" that circulated during the investigation — those counted the
same 160/320 cells but in *decimal* MB. The per-cell arithmetic above is the checked one.

CLAUDE.md rule 7 makes the performance budget a feature. A fleet of hulls at this size is not shippable as
sprites, and the owner separately observed that 8 facings visibly steps on big boats — the two problems have
one answer.

## Decision

**Render large boat hulls as real-time 3D meshes, extracted from the same rigs the baker already runs, shaded
by a facet shader that reproduces the rig's own pipeline. Sprite hulls and mesh hulls coexist behind one
interface; small hulls may stay sprites indefinitely.**

### Why this is not a rewrite

**The rig already IS a flat-facet 3D renderer** — z-buffered triangles, per-face normals, a fixed key light,
a palette-ramp lookup and ordered dither. A GPU shader is not *approximating* the pixel-art look; it can be
made the same pipeline. That is why the match is measured in single-digit percentages rather than "close
enough", and it is the load-bearing fact of this ADR.

The rigs build a face list **once at load** (`const F = []`, `(function build(){…})`); heading and rocking are
applied afterwards as transforms. The geometry is therefore static and the motion is already a transform —
exactly the shape a mesh wants.

### The projection trick that makes it work

Bake the iso rotation into the **object transform**: `Rx(elev−90) · Rz(heading)` applied to the rig's own
coordinates. The hull then sits in ordinary world space, and the game's ordinary straight-down 2D orthographic
camera reproduces the rig's exact projection **and** its z-buffer depth. This also collapses the rig's
`shadeOf(n, se, ce)` to a plain `dot(worldNormal, LN)` — **the key light stays fixed in SCREEN space**, which
is precisely what makes the result read as pixel art rather than as lit 3D.

## Evidence

Measured on two hulls. Sprite behaviour is the control throughout.

**Still fidelity** — inked pixels differing from the art director's own software render:
**1.3–4.4%** (lobster boat, 12 m) · **2.47–4.81%** (side dragger, 25 m). Residual is facet- and
dither-boundary single-step noise, not shape or shading.

**Dither crawl** — change per frame measured in the *hull's own frame*, where a translating hull must not
change at all:

| | lobster | dragger |
|---|---|---|
| baked sprite (control) | 0.00% | 0.00% |
| mesh, screen-pinned dither | 13.07% | 16.10% |
| **mesh, dither indexed in hull-cell frame + rig pivot** | **0.00%** | **0.00%** |

⚠️ Locking the dither to the hull kills the crawl but leaves an *arbitrary phase*. **Adding the rig's pivot to
the dither index** puts mesh and sprite hulls on the same dither grid — which is what lets both kinds coexist
in one scene without the mesh reading "off grid". The whole fix is one uniform (`_DitherPhase`).

**Rotation smoothness** — full 360°, and the result that overturned the stated risk:

| | lobster sprite | lobster mesh | dragger sprite | dragger mesh |
|---|---|---|---|---|
| worst single jump | 66.3% | 40.1% | **79.6%** | **52.2%** |
| shading acceleration, mean | 1.027 | 0.265 | **1.322** | **0.230** |
| shading acceleration, max | 5.388 | 1.410 | 5.600 | 1.409 |

Shading *acceleration* (second derivative) is what is perceived as a pop. **The mesh is 3.9× smoother than
32-facing sprites at 12 m and 5.7× at 25 m.** The panel-size fear inverted: larger flat panels make the
**sprite** worse (1.027 → 1.322) while the mesh is unchanged (0.265 → 0.230), because a facing-snap displaces
more screen area on a big hull and a mesh has no snap to displace.

⚠️ The flat-panel comparison was auto-cropped to the **mesh's own worst-changing window** — not a flattering
crop — and landed on the side dragger's cream lower house, the largest uninterrupted panel in the kit. The
sprite lurches; the mesh holds one flat tone.

**Cost**

| | lobster | dragger |
|---|---|---|
| mesh | 1,384 tris / 123 KB | 1,616 tris / 143.9 KB |
| sheets replaced | 117 MiB | 433.1 MiB |
| ratio | ~950× | **~3,082×** |

Triangle count tracks *parameterisation*, not hull size — both rigs use `NSEG = 24`.

## Consequences

**Gained**
- Continuous heading. Facing count ceases to exist as a concept for mesh hulls.
- **Continuous rocking, free.** The rig already applies rock as roll/pitch/heave transforms, so rock frames
  stop being a memory trade. This retires the 4-vs-8 rock-frame compromise on large hulls entirely.
- **Anchors become live 3D transforms** of the same points (`helmSeat`, `haulerMount`, `tubMounts`,
  `navMounts`) instead of baked per-cell pixels — strictly better than the JSON tables.
- Wake and spotlight, which ride the physics root and today expect a snapped facing, read a **continuous
  heading** — which is what they always wanted (see `boat-rotation-and-sprite-centering`).
- ⚠️ **New capability, untested:** a mesh hull has real geometry below the waterline that a sprite never
  bakes. At varying tide the keel would show unless clipped at the water plane — one clip plane in 3D, and
  *impossible* with sprites. An opportunity, not a defect.

**Lost / obsoleted for mesh hulls**
- `DirectionalBoatSprite`'s facing array and its screen-align counter-rotation.
- `FacingsAreCounterClockwise` — meaningless for a mesh. **Retained for legacy sprite sheets**, which still
  need it (characters `false`, boats `true`; see `iso-art-baked-counter-clockwise`).
- Per-cell baked anchor JSON for mesh hulls.

**Unchanged**
- **Sorting is solved exactly as well as it is today — no better, no worse.** The mesh lives in world space
  with the same screen footprint as the sprite, so it y-sorts at whole-object granularity through the
  existing `SortingGroup` "Sort as 2D" workaround (`RegionValidatorWindow.cs:1052-1062`). Per-pixel
  interpenetration is still not available — but the sprite pipeline cannot do that either, so it is not a
  regression.

**ADR 0021 survives, emphatically.** Characters, buildings, props, gear, flowers and shoreline stay sprites —
that is most of the 39 rigs. This work is built *entirely* on the baker's machinery (the same V8 host, the
same `RigCatalog`, the same convention probe). In a 3D-hull world **the baker is what produces the mesh.**

## Migration

**Coexist behind one interface. Do not big-bang the fleet.**

- `IBoatHullPresenter` with `SpriteHullPresenter` and `MeshHullPresenter`.
- An anchor contract whose sprite implementation reads the baked JSON and whose mesh implementation
  transforms the point live.
- `BoatVisualDef` gains a **variant discriminator**. Small hulls (dory, punt, skiffs) may stay sprites
  indefinitely; only hulls where memory or stepping actually hurts need to move.

Suggested phasing, each independently verifiable:
1. `IBoatHullPresenter` seam with the existing sprite path behind it — **no behaviour change**, all tests green. ✅ (#234)
2. Mesh extraction in the baker (`RigMeshExtractor`), gated, with a golden-master style check against the
   rig's own render. ✅ (#233)
3. The facet shader as a real URP pass + the keyline as a fullscreen shader. ✅ (#239)
4. First mesh hull end-to-end (lobster boat — she already has both a mesh and a baked sheet to compare).
   ✅ (`feat/lobster-mesh-hull`): the baked format is `HullMeshDef` in Core (mesh sub-asset + ramps/light/
   dither + two MEASURED pose facts: the rig's azimuth convention via `RigAzimuthProbe`, and its `ROCK`
   amplitudes), produced by `RigMeshAssetBaker` and committed. Boats poses it through the Core seam
   (`IHullMeshRenderer` / `HullMeshPresentation.Service`, implemented by Art's `IsoFacetHullRenderer`) —
   `MeshHullPresenter`+`MeshHullDriver` are the second `IBoatHullPresenter`, with CONTINUOUS heading
   (`HullMeshMath.HeadingToDirUnits`) and CONTINUOUS wave rock (the same reconstructed phase that picks a
   sprite hull's frame, unquantised — superseded in phase 5: the reconstruction stuttered, #243 moved the
   mesh to the animator's forward-read dominant phase, and on the owner's 2026-07-22 ruling the sprite
   quantiser now reads that same forward phase too, its frame rounding unchanged). The consumers were repointed to the presenter seam as planned. The
   lobster's `BoatVisualDef` is the Mesh variant with her 32-facing compass kept wired — the dev A/B
   toggle (V at the helm) flips her between the two representations in place. Acceptance: her in-scene
   mesh render vs her own baked sheet at matching headings, cluster metric, flipped-azimuth sabotage
   proven caught.
5. Side dragger, the hull that motivated this. ✅ (`feat/side-dragger-mesh`) — and the first hull that is
   **mesh-only**: no baked sheet, none wanted, so nothing about her is a memory trade-off any more. Baked by
   the same generic `RigMeshAssetBaker.Bake`, which needed no changes: **792 faces → 1,616 tris / 3,200 verts,
   12 materials, 143.9 KB**, against the 433.1 MiB of sheets tabled above (~3,082×). Because she has no sheet
   there is no `BoatVisualDef` for the bake to *wire*, so it CREATES a mesh-only one (`Facings` empty, which
   is what makes `HasFullCompass()` correctly false: the V-key A/B reports "only one look", and sprite-only
   overlays refuse to bind). Her azimuth was MEASURED CounterClockwise and her `ROCK` read off her own rig —
   (2.0°, 1.1°, 1.0 px), a deliberately slower, stiffer roll than the 12 m lobster's (2.8, 1.6, 1.2), and
   guarded by a test precisely because copying the lobster's def would have looked plausible.

   **Acceptance had to change shape**: with no sheet to compare against, the truth is phase 2's CPU reference
   rasterizer — the art director's own renderer — instead of a baked cell. Four checks, two of which need no
   GPU and therefore run on CI: the committed bake still matches a fresh rig extraction (exact, cluster 0);
   the committed azimuth flag still matches a fresh `RigAzimuthProbe` measurement; the GPU reproduces the
   oracle across cardinal and fractional headings driven through the production compass→dir mapping (worst
   cluster 505 cardinal / 254 fractional, worst cell 3.312% — inside the 2.47–4.81% band tabled above); and
   the flipped-azimuth sabotage is caught by a factor of ~278 (cluster 180,660). Her floors were re-measured,
   not inherited: they land at roughly DOUBLE the lobster's, because a 25 m hull runs longer straight edges
   and larger flat panels, so its single-ramp-step dither boundaries run longer.

   ⚠️ Open question 4 is still open: the extractor's shim fired for **all five** of `F, MATS, GAIN, BIAS, LN`
   on her rig too. `docs/art/rigs/**` was not touched.
6. **The rest of the fleet — DONE (2026-07-23).** The owner's verdict on the lobster A/B (2026-07-22) was
   **"much better as a mesh — all boats will need to be a mesh"**, and phase 5 was the proof the path scales
   to a second hull without the baker, the shader or the seam changing. Phase 6 **baked all eleven** and
   **presents seven**; the gap is the point and is explained below.

   The per-hull menu items became a per-hull TABLE (`HullMeshFleet`), because eleven hand-written bake
   entry points is not a fleet. Two families, and the difference is the sheet rather than the size: six
   hulls convert from baked 32-facing art and **keep their sprite compass** (that is the owner's V-key A/B,
   the only check on the mesh path that works by eye), five are mesh-only.

   ⚠️ **The rollout is variant-gated, and the gate is the sprite overlays.** Baking a hull and PRESENTING
   her as one are separate decisions. Five hulls (six visuals) wear overlays that are baked per facing
   cell — the dory's oars, the outboards on the punt, the console skiff and the sport skiff — and
   `BoatHullSkinner.ApplyMesh` drops them by design, because a mesh rotates continuously and there is no
   cell to look up. So their meshes are baked, measured and wired into `HullMesh`, but their `Variant`
   stays `Sprite`. That wiring is inert (`ShouldPresentMesh` gates on the variant alone), so the flip is a
   one-field change the day those overlays have meshes of their own — **the natural phase 7**. This was
   not foresight: flipping them turned four `PilotableFleetPlayTests` red with "the dory has her oars:
   expected not null, but was null", which is exactly the visible regression the owner would have hit on
   his first press of F. The Cape Islander is the only sheeted hull that wears no overlay, and so the only
   one phase 6 could flip — which makes her the owner's second A/B after the lobster.

   **Measured, whole fleet, against the phase-2 CPU oracle** (worst whole-cell divergence over 8 headings,
   built mesh at f32 — the bar is 0.5%):

   | hull | LOA | tris | asset | sheet set would be | ratio | worst |
   |---|---|---|---|---|---|---|
   | dory | 4.3 m | 814 | 141.6 KB | 12.2 MiB | 88× | **0.0000%** |
   | punt | 5.2 m | 968 | 169.3 KB | 15.1 MiB | 91× | 0.0361% |
   | console skiff | 7.0 m | 954 | 167.3 KB | 25.7 MiB | 158× | 0.0000% |
   | sport skiff | 7.0 m | 1,242 | 215.5 KB | 25.7 MiB | 122× | 0.0083% |
   | Cape Islander | 12.8 m | 1,082 | 184.3 KB | 93.5 MiB | 520× | 0.0097% |
   | lobster boat | 12.0 m | 1,384 | 237.7 KB | 93.5 MiB | 403× | 0.0047% |
   | side dragger | 25 m | 1,616 | 276.9 KB | 346.5 MiB | 1,282× | 0.0107% |
   | stern trawler | 38 m | 1,624 | 277.7 KB | 756.0 MiB | 2,787× | 0.0071% |
   | stern trawler Mk2 | 38 m | 2,458 | 417.8 KB | 756.0 MiB | 1,853× | 0.0103% |
   | coastal packet | 60 m | 2,508 | 428.9 KB | **1,815.0 MiB** | 4,333× | 0.0161% |
   | tanker | 110 m | 3,492 | 596.6 KB | **1,500.0 MiB** | 2,574× | 0.0025% |

   The upper four had never been in Unity at all, and the table says why: a sheet set for the packet alone
   would have been 1.8 GiB. **The tanker is the ADR's best argument** — she is authored at 16 px/m, half
   the fleet standard, because at 32 she would be a ~3,500 px cell. The mesh does not care; px/m is data
   (`HullMeshDef.PxPerMetre`, read from the rig, asserted per-hull), and she is now the regression target
   for anything downstream that quietly assumes 32.

   Two things phase 6 found that phases 4–5 could not:

   - **The dory needed a real fix.** She is the oldest hull rig and predates the `MATS` convention
     entirely, selecting her two ramps inline (`f.mat==='iron' ? IRON[idx-2] : RAMP[idx]`). The shim
     widens a private symbol so it can be READ — it had nothing to widen, and failed with "MATS is not
     defined". The shim can now also **reconstruct** a symbol from a rig's own values, reported separately
     from a widening because the claims differ: widening asserts nothing about the art, a reconstruction is
     our reading of what `_paint` means. Hers is adjudicated in pixels against her own renderer and comes
     back **0.0000% across all 8 headings**, with a sabotage (one-step ramp offset) proving the check is
     load-bearing. `docs/art/rigs/**` untouched; exporting a real `MATS` retires the entry.
   - **A file that exists but does not load is not a new asset.** Treating it as one runs field
     initialisers and silently resets every field the baker does not write — `RestingDraftMeters`, which
     the waterline work tunes per hull. It fired for real during this build: a run off a borrowed
     `Library/` (stale script→guid map) wrote `m_Script: {fileID: 0}` into every def, after which they
     stopped resolving to their own type, and the next run "created" them over the top and zeroed the
     lobster's 0.5 and the dragger's 1.1. The baker now stops instead.

   ⚠️ Open question 4 remains open and is now measured across the whole fleet: the shim fires for all five
   of `F, MATS, GAIN, BIAS, LN` on **every** hull rig. Phase 7 adds to the tally: a FITTING also needs
   its builder and its pivot widened (`buildOar`/`oarlockPt`, `motorFaces`), and
   `skiffMotorRig.js` — a layer rather than a hull — exports neither `PX` nor `defaultElev`, so both
   are reconstructed from its own `S`/`DEFAULT_ELEV`. Neither motor rig exposes its swivel as a
   point (it is two private consts, `YA` and `ZT`), so `swivelPt()` is a reconstruction too, and
   therefore adjudicated in pixels like every other one.

7. **The overlays — DONE (2026-07-24).** The dory's oars and the fleet's outboards were the last
   sprite art bolted to a hull, and they were what held five hulls on the sprite compass (above).
   They had rigs already (`doryIsoRig.renderOars`, `puntIsoRig.renderMotor`, `skiffMotorRig.js`), so
   the path was the one this ADR had already run five times: extract, build, bake, measure against
   the CPU oracle. **Every hull in the fleet is now presented as a mesh** — eleven hulls, fourteen
   visuals — and `HullMeshFleet` carries no `OverlayBlockedReason` at all.

   **The oars — DONE.** `HullPropMeshDef` + `IHullPropRenderer` in Core, whose entire contract is a
   *local rotation*: Boats owns what a stroke or a steer means and Art never learns, which is why oars
   and outboards — which articulate nothing alike — ride one seam. The extractor gained the one
   structural difference a fitting has: a hull's geometry is the static `F` array, a fitting's comes from
   a **builder that takes a pose** (`buildOar(side,{sweep,dip})`), called at a canonical pose chosen so
   the runtime transform is exactly rigid. `DoryOarMeshLayer` poses them from the same `DoryOarMath`
   state machine the sprite oars use, reading the **continuous** stroke phase and discarding the column's
   rounding — the same unquantising #243 did for wave rock. A fitting is parented to the hull's posed
   mesh child, so it shares the depth buffer: that retires the sprite era's `upper`/`lower` part split
   and its draw-the-far-engine-first rule, and the oars' whole rock-coupling block (five tunables to lean
   a rock-free overlay onto a rock-baked hull) simply becomes true.

   ⚠️ **What acceptance had to learn, and it generalises to every rig surface built this way.** The rigs
   make a zero-thickness blade double-sided by pushing each face **twice** — `q` then its exact reverse
   (`doryIsoRig.js:241`) — bit-identical vertices, identical `db`, opposite normals, so ramp index 5
   against index 0. **Which twin you see is decided by the last bit of a barycentric sum.** Instrumenting
   the CPU oracle per pixel (`RigPaintTrace` records every triangle covering a pixel, winner *and*
   losers) settled it: over 8 headings × 8 stroke phases, 1,519 tied pixels, the rig's own choice agrees
   with `deff < zbuf` 50.6%, `deff <= zbuf` 50.4%, its own `Float32Array` z-buffer 44.6%, front-facing
   51.2%, lit 51.2%, dark 48.8%. Six rules, all chance — **no renderer reproduces the rig there, and
   neither would the rig re-run with other arithmetic.** That is also why dropping the twin measured
   *worse* (cluster 49 → 60): it makes us deterministic, so we agree less often than a coin.

   So the fixture stops comparing what is not a fact and asserts what is: **zero** silhouette differences
   (the twins have identical vertices, so coverage is never ambiguous and the mask cannot suppress one);
   cluster ≤ 6 outside the twins (measured worst 3); and inside them, the rig must have painted one of
   the **two** colours we computed — `RigAmbiguousPixels`, 0 exceptions in 1,519, twins identified
   structurally rather than by a depth tolerance (float32 breaks the quad's planarity and the two
   triangulations drift ~7e-8 apart). The old sabotage was proving nothing — a naive shortest-arc pose
   scored cluster **11 against a floor of 12** — so it was replaced by a measured sensitivity curve
   (extra feather twist → detections, 2 oars × 64 poses): 0° → **0**, 0.25° → 38, 0.5° → 76, 1° → 138,
   2° → 310, 4° → 814. The fixture resolves **half a degree** of blade roll.

   `visual.dory_iso` is therefore `Variant = Mesh` — the first hull whose overlay **crossed over** rather
   than being dropped — keeping her sheets *and* her compass so the owner's V-key A/B still covers the
   whole boat. ⚠️ On a GPU the same tie is z-fighting; the deterministic cure is shading from whichever
   side faces the camera (`SV_IsFrontFace` in the facet pass), which is art-pipeline's shader and not a
   bake decision. Flagged, not done.

   **The outboards — DONE**, and with them the phase, the rollout and the owner's mandate. Two rigs
   (the punt owns her tiller engine outright — own 212×168 cell, own ±32°, `basic`/`upgraded`;
   `skiffMotorRig`'s remote-steer four-stroke fits BOTH 7 m skiffs, 272×216, ±30°, `work`/`sport`),
   four bakes, five visuals flipped. The variant is DATA (`BoatVisualDef.MotorVariant`, and the
   punt's upgrade is a real gameplay distinction), and the twin is **two instances of one def** at
   the ±0.34 m its rig declares — no second bake. `MOTOR.parts`, `MOTOR.behind=[3,4,5]` and the
   draw-the-far-engine-first rule are deliberately not transcribed; a shared depth buffer makes them
   true, and the twin is now measured saying so: both engines into one z-buffer against the rig
   painting both face lists in one pass, **0 silhouette differences, worst cluster 0**.

   The steer is read CONTINUOUSLY. `OutboardMotorMath` always carried the drawn swivel as a float
   and rounded it to one of nine baked columns at the last step; `SteerDegreesForPosition` is that
   same affine map without the rounding, so the mesh engine swivels through the tuned cadence,
   deadzone and per-hull authority the owner already signed off — the same unquantising #243 did for
   the hull's rock and #285 for the dory's stroke, and it agrees with the sprite path at every
   column the sprite path had.

   ⚠️ **What the outboards found, and it generalises: a fitting is not necessarily one rigid body.**
   Both rigs build the clamp bracket (and the skiff's tilt-tube cap) through the IDENTITY placement
   `I` rather than the posed `X`, because the bracket is bolted to the transom and the engine swivels
   ON it. A mesh that rotated the whole face list carried the bracket round with the cowl — measured
   against the rigs as 489 silhouette differences and a 39–53 px connected patch, **invisible dead
   ahead** and worst at hard-over and full tilt. The dory's oar had no such part, so the seam had
   never met one. `HullPropMeshDef` gains a `FixedMesh` and the renderer a second child that takes
   the clamp offset and nothing else; **which faces belong to it is MEASURED** (build the face list
   again at a pose with both articulation axes off zero, keep the faces whose vertices did not move)
   rather than read off the rig — 6 of 96 on the punt, 12 of 100 on the skiff, which is one box and
   two boxes respectively. The extractor refuses to split a rig whose fixed faces are interleaved
   with its moving ones, because two meshes rasterise in the order (fixed, moving) and that is only
   the rig's own order while every fixed face precedes every moving one.

   Two more facts about the rigs' RASTERISER that their data cannot carry, found by reading `_paint`
   rather than assuming it: `skiffMotorRig` rescues **every** back-facing face where the hull rigs
   gate it on `b <= -1` (and not one of its faces carries a bias that low, so the two rules genuinely
   differ there — ⚠️ the facet shader implements the hull rule, so this is a flagged, unmeasured GPU
   difference in art-pipeline's lane), and every fitting entry point passes `doEdge = false`. Both
   are declared per fitting and honoured by the CPU oracle.

   **Measured, four fittings × 8 headings × 13 poses** (steer swept through
   `SteerDegreesForPosition` at deliberately FRACTIONAL positions — poses no baked column exists for
   — plus tilt at both hard-overs), against each rig's own `renderMotor`:

   | fitting | tris | asset | silhouette | unresolvable colour | worst cluster |
   |---|---:|---:|---:|---:|---:|
   | punt outboard, basic | 200 | 17.7 KB | 0 | 0 | **0** |
   | punt outboard, upgraded | 200 | 17.7 KB | 0 | 0 | **0** |
   | skiff outboard, work | 208 | 18.4 KB | 0 | 0 | **0** |
   | skiff outboard, sport (twin, both mounts) | 208 | 18.4 KB | 0 | 0 | 1 |
   | skiff outboard, sport (single) | 208 | 18.4 KB | 0 | 0 | **0** |

   Three of the five are identical, not close; the twin's single pixel is 1 in 84,526. **Neither
   motor rig builds a double-sided twin**, so there are no ambiguous pixels to exclude — the phase-7
   machinery (`RigAmbiguousPixels`) stays wired for the day a rig grows one, and reports 0.

   Sensitivity, sport outboard, steer error → (silhouette, worst cluster): **0° → (0, 0)** ·
   0.25° → (34, 17) · 0.5° → (54, 17) · 1° → (94, 17) · 2° → (233, 29) · 4° → (468, 34). **The
   fixture resolves a quarter of a degree of swivel**, on both channels at once, and zero error is
   the only clean point. Swapping the steer/tilt composition order scores 240 against a correct 0 —
   a sabotage that is invisible unless BOTH angles are non-zero, which is why tilt is swept at all.

   Two defects in phase 7's own machinery fell out of doing this, both invisible until a second
   fitting and a second mesh hull existed:
   - `IsoFacetPropRenderer` never wrote `_HullOrigin` or `_HullId`, so the oars dithered against the
     WORLD origin — the 13–16% dither crawl this ADR measured and drove to 0.00%, reintroduced on the
     parts with the largest flat panels — and wrote hull id 0 into the keyline resolve.
   - The service found the posed child by NAME. Re-configuring a hull renderer (what a swap does)
     destroys the old child and builds a new one with the same name, and in play mode `Destroy` is
     deferred to end of frame — so `Find` returned the DOOMED one and the boat you swapped TO lost
     her fittings one frame later. The renderer now hands the child out directly.

   ⚠️ **Known and accepted, unchanged from the oars:** where a rig DOES build coplanar twins the tie
   is z-fighting on a GPU, and the deterministic cure is `SV_IsFrontFace` in the facet pass —
   art-pipeline's shader, not a bake decision. The outboards are the lower risk of the two: they
   have no zero-thickness double-sided surface at all. Their exposure is ordinary coincident
   geometry (a decal quad on a cowl panel, a leg box against the cavitation plate), which the CPU
   oracle resolves deterministically at cluster 0 and a GPU resolves by `LEqual` — stable while
   nothing is exactly coplanar, and nothing here is.

## Alternatives considered

- **Stay on sprites, go to 64 facings.** Rejected: 866 MiB for one hull, and it does not solve stepping so
  much as subdivide it. Still available for small hulls.
- **Port each rig to C# mesh generation.** Rejected: recurring per-rig cost for every future hull, and two
  implementations of one renderer will drift silently when the art director edits the JS.
- **Export static meshes (FBX/OBJ) from the rigs.** Rejected: loses parametricity — no build options, no
  re-bake when a rig changes.
- **Full 3D for everything.** Not proposed. Characters, buildings and props are unaffected by the memory
  problem and are well served by sprites.

## Open questions / not yet proven

1. **URP integration.** ✅ **CLOSED by phase 3** (`feat/facet-shader-urp`). What the unknown turned out to be:
   URP 17.5's 2D renderer supports RenderGraph render features through its own injection system
   (`ScriptableRenderPass2D` / `RenderPassEvent2D`, per-sorting-layer-batch) — the facet pass runs at
   `BeforeRenderingSprites` on the lowest sorting layer (the plain `BeforeRendering` event records **before**
   camera matrices are set up and cannot draw geometry). Two structural discoveries reshaped the plan:
   **(a)** the hull cannot draw directly in the 2D transparent pass — it needs a z-buffer, but sprites z-test
   (`ZTest LEqual`, `ZWrite Off`) against the *shared* depth buffer, so a depth-writing mesh punches holes in
   every later sprite above it. The facet pass therefore draws off-screen into a 4-target MRT with a **private**
   depth buffer, and a cell-sized **overlay quad** re-composes the hull (keyline included) in-scene, sorting
   whole-object through the SortingGroup exactly as a baked sprite would. **(b)** the spike's screen-space
   `_DitherPhase` calibration is unnecessary in production: deriving the dither index **from world position in
   the hull-cell frame** (`(worldXY − hullOrigin)·PPU + pivot`) is the same number by construction, y-flip-proof,
   and hull-locked with no probe. Acceptance: GPU vs the phase-2 CPU oracle, connected-cluster metric, with
   convention-flip sabotage (light sign, dither phase, heading mirror) proven caught.
2. **The keyline** ✅ **CLOSED by phase 3**: a fullscreen resolve shader (darken far side of >0.30 m true-depth
   discontinuities via a precomputed RINDEX-faithful darkened-ramp LUT, flood the 1 px keyline with the
   neighbour's key colour and hull id), written into a persistent screen texture the overlay quads sample.
   *(Amended by [ADR 0031](0031-keyline-retirement.md): the 1 px flood is production-gated —
   `_HHKeylineFlood`, default OFF; the keyline is retired from the world style. The depth-edge darkening
   half of this resolve is untouched, and the GPU oracle fixtures force the gate ON so this phase's
   verbatim claim stays pinned.)*
   Phase 3 also honours the owner's deck-walking decision (2026-07-21): a second renderer list (LightMode
   `HHHullDeck`) draws **between** the facet pass and the resolve against the **same private z-buffer**, so a
   character-on-deck billboard is per-pixel occluded by nearer hull geometry — probed in-repo
   (`IsoFacetUrpPassTests.DeckRenderers_AreDepthTestedAgainstTheHull_PerPixel`). Note for phase 4: that path
   uses plain `ZTest LEqual` — the render-graph camera path handles reversed-Z; the spike's `GEqual`/clear-0
   convention belonged to its hand-built command buffer only.
   ✅ **The billboard stopped being "future" on 2026-08-07** (owner playtest: *"rider/player sprites visible
   THROUGH closed cabins on models with doors and a cockpit"*). `HullDeckOccupant` +
   `HiddenHarbours/HullDeckSprite` draw the on-deck character through that list, carrying the hull's id so
   the hull's own overlay quad re-composes boat and crew as **one image**. Compositing was the only
   available fix, not a preference: a mesh hull composes through ONE overlay quad at ONE sorting order, so
   a crew sprite compared against her is wholly in front of the boat or wholly behind — and behind is
   invisible, because the sole under their boots is hull pixels too. (Every shipped `BoatVisualDef` carries
   `SortingOrder` 1, below `SortingBands.DecorFloor`, while the player is Y-sorted inside the decor band, so
   the fisher won that comparison at every position on the water.) Nor could a cleverer screen-space rule
   replace it: under the ¾ bake screen height is `alongView·sin(elev) + height·cos(elev)` while distance is
   `alongView·cos(elev) − height·sin(elev)` — two **independent** combinations of one pair, so no cut
   through the picture separates "nearer than the fisher" from "further". The boots' depth is
   `DeckAreaMath.DeckDepth`, the third row of the very projection the deck walk already uses for the first
   two, pinned to `IsoFacetMath.RigToWorld`'s own depth row by
   `DeckDepthTests.TheDecksDepthRow_IsTheRigsOwnDepthRow`; the shipping path is pixel-tested by
   `IsoFacetUrpPassTests.TheCrew_IsCompositedIntoTheHull_AndDepthTestedPerPixel`. A **sprite** hull returns
   false from `IBoatHullPresenter.SetDeckOccupant` and its figure is drawn in-scene exactly as it always
   was — there is no depth behind a baked compass sheet to be occluded by, and absence is data.
3. **Waterline clipping** (above) — designed, untested.
4. ⚠️ **Geometry access.** The rigs' face list `F` is **closure-private and not exported**. The spike reads it
   via a loudly-marked in-memory string widening of the exported object literal. **In production the art
   director adds one property (`F,`) per rig and that hack disappears** — that is the entire delta.
   **`docs/art/rigs/**` must never be edited on our side.**

   ⚠️ **Still open, re-verified by measurement 2026-07-20.** All four boat rigs (`lobsterBoatIsoRig`,
   `sideDraggerIsoRig`, `puntIsoRig`, `capeIslanderIsoRig`) declare `const F = [];` but their export object
   (`root.LobsterBoatIso = { W, H, PX, DIRS:8, … }`) omits it — the art director has not yet made the change.
   ⚠️ Grepping for `F,` **false-positives on 14 rigs**; it matches ordinary code. Only the export object
   literal at the end of the file is evidence. Therefore `RigMeshExtractor` must **probe for an exported `F`
   first** and fall back to the widening shim, so the shim becomes dead code the day the property lands,
   with no edit on our side.
5. GPU timings from the spike (47–52 ms) are dominated by dual-target `ReadPixels`, **not** by drawing;
   1,616 triangles is nothing. They are not a performance signal.

## References

- Harness and images: branch `spike/3d-boats` (not merged); `scratchpad/3dspike/` —
  `1-ab-sprite-vs-3d.png`, `3-in-scene.png`, `4-dither-crawl-*`, `5-rotation-mesh-vs-sprite.gif`,
  `6-dragger-worst-transition.png`, `6-dragger-rotation-mesh-vs-sprite.gif`.
- Conventions the spike had to discover the hard way, now probed at runtime rather than assumed:
  `GL.GetGPUProjectionMatrix(renderIntoTexture:true)` double-counts the D3D render-target Y-flip against the
  readback flip; a hand-built `CommandBuffer` with explicit matrices needs **`ZTest GEqual` + depth clear 0**
  for reversed-Z or the hull's *bottom* wins the depth test and draws through the deck; the Bayer grid needs a
  **(0,1) phase offset**.
