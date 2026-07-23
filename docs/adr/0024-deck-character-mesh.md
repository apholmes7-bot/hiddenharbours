# ADR 0024 — On-deck characters draw as facet MESHES (a pose flipbook with live heading); the baked 8-dir sheets keep locomotion everywhere else

- **Status: DRAFT** — the `spike/deck-character-mesh` verdict, written for the owner to ratify.
  Ships no gameplay itself; the spike's demo rig and measurements are the evidence. Nothing is
  wired into the shipping player.
- **Date:** 2026-07-23 (spike)
- **Decision owner:** lead-architect ratifies; **gameplay-systems** owns the deck-fishing consumer
  (rod v2 Wave 4), **art-pipeline** owns the facet look, **tools-editor** owns the pose baking,
  **art-director** owns the one export-contract change flagged below.
- **Serves:** **P1 The Sea Has Moods** (the deck turns smoothly under a fisher instead of the
  fisher ratcheting through 45° poses) and **P5 Cozy but with Teeth** (long, calm deck sessions
  must not look broken).
- **Extends:** [ADR 0022](0022-3d-boat-hulls.md) (the facet pipeline gains a second *kind* of
  occupant, character meshes) and [ADR 0021](0021-in-engine-js-rig-baking.md) (the baker's V8
  machinery bakes the poses). **Amends nothing in ADR 0022** — every hull decision stands.
- **Explicitly does NOT change:** locomotion. Walking/running characters ashore and on deck stay
  8-direction baked sprites (ADR 0022's own scoping: "characters … are unaffected by the memory
  problem and are well served by sprites" — that remains true for *moving* characters, whose own
  heading changes are player-authored and snappy; the problem below is specific to a *stationary*
  character on a *slowly rotating platform*).

---

## Context

Rod fishing v2 (owner-locked, `docs/design/rod-fishing-v2-brainstorm.md` §4.1) fishes from an
unmanned, weathervaning deck: facing is **deck-space**, so the idle/fighting fisher yaws with the
hull. An 8-direction baked sprite crosses a 45° facing boundary roughly every few seconds of slow
weathervane and **ratchets** — the exact stepping artefact ADR 0022 killed for hulls, on the
character the player stares at longest. The design doc's decision #1 recommended validating a
3D-rig character for on-deck idle/fight, sprites for locomotion. This spike validated it.

Everything below was measured on branch `spike/deck-character-mesh`; the CI-safe measurements are
pinned by `Assets/Tests/EditMode/SpikeDeckCharacterMesh/` (CPU-only — the RigBaking oracle
pattern, no graphics device needed).

## The architectural finding (why this is cheap, and where it differs from ADR 0022)

**The character rig is a 3D flat-facet renderer of the same family as the boat rigs — but it is
not boat-shaped.** A boat rig builds `const F = []` once at load and applies heading/rock as
transforms; that is why a hull is ONE mesh. The character rig builds its face list **per pose**:
`facesOf(pose(anim, u, build))` bakes the skeleton's FK/IK result into the vertex positions.
There is no static F to extract.

What *is* still transform-shaped is exactly what the spike needs: **heading, roll, pitch and
heave** (`camBasis` — the rig's own DECK ROCK contract, "feed a hull rig's rock(i) straight in").
So:

> **Pose = a mesh flipbook** (one small mesh per animation frame, exactly the frames the sheets
> already play) · **rotation = a live transform** (continuous — the whole point).

Consequences of that split:

- **The entire ADR 0022 tail end is reused unchanged.** The spike's pose extractor widens the
  export literal with the pose surface (`facesOf, pose, makeMats, GAIN, BIAS, LN, BAYER` — the
  same loudly-marked in-memory shim as ADR 0022 open question #4; `docs/art/rigs/**` untouched,
  byte-identity pinned by test), then feeds the standard `RigMeshData → RigMeshBuilder → Mesh`
  path. The runtime draws through the **existing, unmodified** `IsoFacetHullRenderer` and facet
  URP pass — the character is simply one more registered facet object with its own id, overlay
  quad and SortingGroup. **Zero changes to Art, Boats, Core or water-lane files.**
- **Continuous pose interpolation is NOT attempted.** Between-frame blending would need a skinned
  mesh or morph targets — a genuinely new art tech. Unnecessary: the sprites play the same
  discrete frames today and the pose cadence is not what ratchets; the *heading* is.
- **Cost is trivial** (spike Q4). **Measured: the whole idle+hold flipbook is 12 meshes,
  4,576 tris TOTAL (≈381/frame), 411.1 KB of buffers** — the aggregate is ~3× one lobster hull's
  1,384 tris, and only ONE pose renderer is ever live per character. Swapping frames is a
  registry re-register (trivial). No new passes, no new RTs, no texture memory at all — against
  the alternative of ever baking more character sheets (a 64-facing idle sheet would be 8×
  today's).

## The trap the spike caught (and pinned): the turntable sign is NOT the label convention

The character rig turns its turntable `th = −dir·π/4` (the ADR-0006 label fix); the boat rigs and
the shared `IsoFacetMath` projection use `+dir·π/4`. Therefore:

- the **label probe** (`CharacterRigAzimuthProbe`) correctly answers "labels are CW-true" for
  characters, and
- the **facet mapping** (`HullMeshMath.HeadingToDirUnits`) must still **negate**
  (`azimuthCounterClockwise = true`) — the *opposite* of what naively storing the probe's answer
  would give. Characters and boats genuinely disagree label-wise yet agree turntable-wise.

Declaring either fact would have shipped the sixth mirrored artwork of this project's history. The
spike baker **adjudicates the sign from pixels** (rig's East render vs the reference oracle at
dir ±2; the wrong sign must lose by ≥4×) and the test
`FacetTurntableSign_IsNegated_MeasuredFromPixels` pins it.

## Spike questions, answered

1. **Does the character read as pixel art through the facet pass, next to baked sprites?**
   **Yes — measured 0.61–4.33% of inked pixels differing, inside ADR 0022's own hull band
   (2.47–4.81%).** The rig's own render vs the facet pipeline's CPU oracle
   (`Golden_RigRenderVsFacetOracle_StaysInTheSpikeBand`, idle+hold × 8 dirs, measured 2026-07-23):
   idle[0] worst 4.16% / largest connected cluster **10 px**; hold[0] worst 4.33% / cluster 10;
   **0 silhouette differences at every dir** (the outline and keyline are exact). The residual is
   edge-shaped and has one known systematic cause: **the fullscreen keyline resolve darkens depth
   edges `> 0.30 m` (a hull constant) while the character rig separates limbs at `0.13 m`** — so
   some limb/torso separation lines lose their 2-step darkening — plus single-step dither noise.
   Palette, ramps, ordered dither, keyline colour and the fixed key light are identical by
   construction (same extraction, same shader). *Condition flagged below.*
2. **Does a slowly-yawing deck read smooth under a mesh character?** This is the demo
   (`Hidden Harbours → Spike → Deck Character Mesh` → attach rig; keys **J** mesh↔sprite A/B,
   **H** idle↔hold, **U** forced weathervane, **O** displaced sea). Rotation smoothness for facet
   meshes vs 8-dir sprites was already measured by ADR 0022 (mesh 3.9–5.7× lower shading
   acceleration than *32*-facing sprites; the character sprite has 8) — the mechanism is
   identical here and the owner's eye is the ratifier. The displaced-sea attachment rules are
   honoured by construction: the character reads its hull's **live** renderer pose (post-
   `MeshHullDriver`, same frame), shares the hull's root y so the calibrated iso-depth frame
   (`DisplacedWaterRegistry.TryGetIsoDepthFrame`, water doc §24) lands both in one commensurate z
   frame, and rides the deck lift through the rig-honest **heave channel** — so hull-vs-character
   occlusion resolves **per-pixel** in the facet pass's private z-buffer, free (the §24 deck
   corollary: a raw world-z deck renderer would sort wrong; the spike's does not).
3. **Where does sprite↔mesh hand off, and is the pop acceptable?** At the boarding/locomotion
   boundary: the character walks (sprite) to the fishing spot, stops, and the *stationary* states
   (idle/hold/bite/strike/reel/land) present as mesh; walking away swaps back. The pop is bounded
   by the fidelity band above **plus** the 45°-snap-to-continuous heading change — worst case
   22.5° of heading, visible once, at a moment the player initiated an action. Mask: swap on the
   idle frame the sheets and the flipbook share (same rig, same frame — the golden diff *is* the
   pop). The spike A/B toggle (J) demonstrates the swap live; verdict on acceptability is the
   owner's, but nothing structural prevents masking it entirely.
4. **Cost & owed art-source work.** Perf: see above — no measurable budget impact (rule 7).
   Owed to the **art-director** (flagged, NOT built):
   - **Export-contract growth**: officially export the pose surface
     (`facesOf`/`pose`/`makeMats` + `GAIN`/`BIAS`/`LN`/`BAYER`) from `characterIsoRig.js` so the
     widening shim dies — the same ask as ADR 0022 open question #4, still unactioned there too.
   - **Optional, look-fidelity**: a per-object depth-edge threshold for the keyline resolve (the
     0.30 m vs 0.13 m delta). One uniform/LUT keyed by object id — an **Art-lane change the spike
     did not make** (the resolve shader and `IsoFacetHullFeature` are the water/art lane's; noted
     here per the spike's read-only constraint). Alternatively ship with 0.30 m and accept
     slightly softer limb separation on deck — the A/B demo shows what that looks like.
   - No new sheets, no new anims: the fight cycle (bite/strike/reel/land) already exists in the
     rig and bakes into the flipbook the same way.
5. **Recommendation: GO — with conditions** (below).

## Decision (proposed)

**On a boat deck, the player character's stationary fishing/idle states render as facet meshes —
a per-frame pose flipbook extracted from `characterIsoRig.js`, drawn through the existing ADR 0022
facet pipeline with continuous deck-space heading. The 8-direction baked sheets keep every
locomotion state and every off-deck context.**

### Conditions attached to the GO

1. **The owner ratifies the A/B demo** (the spike's key-J toggle on a weathervaning lobster boat,
   displaced sea ON) — the same gate the hull mesh passed.
2. **Production shape, not the spike's.** The spike quarantines everything
   (`Code/Spike/DeckCharacterMesh`, renderer-per-pose-frame, dev keys). Production wants: a
   `CharacterMeshDef` (Core) + one renderer whose mesh swaps per frame behind the existing
   `IHullMeshRenderer`-style seam, a proper presenter that the deck-fishing controller drives, and
   the pose baking folded into the real baker menus. The spike's per-frame
   `IsoFacetHullRenderer` children are a demo device, not the design.
3. **The turntable-sign fact stays measured** (bake-time pixel adjudication + pinned test) — never
   a declared constant, never the label probe's answer.
4. **The resolve-threshold decision** (accept 0.30 m or add the per-object hook) is made with the
   art-pipeline/water lane before Wave 4 polish; it does not block Wave 4 bring-up.
5. **Sea-OFF caveat noted:** with no displaced surface there is no calibrated z frame and
   character-vs-hull overlap degrades to whole-object sorting (exactly like sprite crew today —
   no regression, but per-pixel occlusion is a displaced-sea-ON feature until the flat-water path
   gains a frame of its own).

## Alternatives considered

- **More facings (16/32) for on-deck sheets.** Subdivides the ratchet instead of removing it, and
  multiplies sheet memory for every build×outfit×anim combination. Rejected — the same reasoning
  that rejected 64-facing hulls.
- **Rotate the baked sprite in screen space.** Breaks the ¾-iso projection immediately (a rotated
  iso cell is not the iso view of a rotated character). Rejected.
- **Skinned mesh / morph-target interpolation.** Solves a problem nobody has (pose cadence), at
  the cost of real new art tech. Rejected for now; the flipbook leaves the door open.
- **Do nothing (ship the ratchet).** The owner already called this out in §4.1; deck fishing is
  the longest-stared-at scene in v2. Rejected.

## Migration sketch (post-ratification, phased like ADR 0022)

1. `CharacterMeshDef` + baker (tools-editor), golden-mastered like `HullMeshDef`. Includes the
   fight states.
2. One production renderer/presenter behind a Core seam; the deck-fishing controller (Wave 4)
   drives heading/pose; `DeckWalkController` unchanged (locomotion = sprites).
3. The swap-at-the-boundary rule in the boarding/fishing state machine, masked on the shared idle
   frame.
4. Retire the spike quarantine.

## References

- Spike branch `spike/deck-character-mesh`: `Assets/_Project/Code/Spike/DeckCharacterMesh/`
  (runtime demo rig), `Assets/_Project/Code/Tools/Editor/SpikeDeckCharacterMesh/` (pose extractor
  + baker + demo menu), `Assets/Tests/EditMode/SpikeDeckCharacterMesh/` (pinned measurements).
- ADR 0022 (facet pipeline, evidence tables), water design doc §24 (calibrated iso z / deck
  corollary), `rod-fishing-v2-brainstorm.md` §4.1/§8 (the locked problem statement).
