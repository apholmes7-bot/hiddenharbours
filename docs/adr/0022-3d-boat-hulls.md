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
   sprite hull's frame, unquantised). The consumers were repointed to the presenter seam as planned. The
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
6. The rest of the fleet. The owner's verdict on the lobster A/B (2026-07-22) was **"much better as a mesh —
   all boats will need to be a mesh"**, and phase 5 is the proof the path scales to a second hull without the
   baker, the shader or the seam changing. ← next

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
   Phase 3 also honours the owner's deck-walking decision (2026-07-21): a second renderer list (LightMode
   `HHHullDeck`) draws **between** the facet pass and the resolve against the **same private z-buffer**, so a
   future character-on-deck billboard is per-pixel occluded by nearer hull geometry — probed in-repo
   (`IsoFacetUrpPassTests.DeckRenderers_AreDepthTestedAgainstTheHull_PerPixel`). Note for phase 4: that path
   uses plain `ZTest LEqual` — the render-graph camera path handles reversed-Z; the spike's `GEqual`/clear-0
   convention belonged to its hand-built command buffer only.
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
