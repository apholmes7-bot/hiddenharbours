# ADR 0033 — One depth unit: the hull frame takes a y→z shear, and three laws re-derive with it

- **Status: ACCEPTED (2026-08-12)** — lead-architect decision, commissioned by the owner after
  #491's diagnosis. This ADR ratifies the depth-contract change; the implementation ships as its
  own PR against the acceptance criteria below.
- **Date:** 2026-08-12
- **Decision owner:** lead-architect (the contract + this ADR); art-pipeline (the shear, the
  re-derivations, the sweeps); gameplay-systems (flotation re-check sign-off on `BoatHullDef`
  drafts); qa-test (golden-master re-baseline discipline).
- **Serves:** **P1 (The Sea Has Moods)** — a sea that can never reach the stern's planking is not
  a sea, it is a backdrop. Also **P5 (Cozy but with Teeth)**: a hull the water visibly climbs is
  most of what makes a following sea read as dangerous.
- **Related:** `0022` (the mesh fleet and its facet pass), `0023` (displaced water — the shared
  private z-buffer this contract lives in), `0031` (keyline retirement — its resolve threshold is
  one of the three laws touched). Evidence and oracle: **PR #491**,
  `Assets/Tests/EditMode/Art/HullWaterlineDepthResidualTests.cs`.

## Context

ADR 0023 phase 3 composites hull and water in a shared off-screen z-buffer: water records first,
hull fragments z-test against it, planking below the lifted surface loses and the sea shows
through. The construction is sound. The defect (#491, measured) is that **the two sides measure
"ground y" in different units**:

- The hull's vertex depth comes from the rig projection (`IsoFacetMath.RigToWorld`):
  `depth = ry·cos(elev) − rz·sin(elev)`. One rig ground metre aft is `sin(elev)` of screen
  travel but `cos(elev)` of depth.
- The water's depth (`HiddenHarboursWater.shader`, vertex stage; C# reference
  `DisplacedWaterMath.HullDepthBias`) advances at `cos(elev)` per **world y** — and a flat
  quad's world y *is* its screen y.

So along its own fore-aft axis the hull's depth ramp is **1/sin(elev) = 1.556× too steep** at the
fleet's 40° bake. `IsoFacetHullRenderer.ApplyPose` translates the hull's whole frame by one
constant per hull — deliberately, to preserve intra-hull depth relations — and a constant cancels
a gradient at exactly one line. The residual everywhere else is

```
residual = Δy_rigGround · cos(elev) · (1 − sin(elev))     (= Δ · 0.2736 at 40°)
```

— the very term `DisplacedWaterMath.cs` already carries as **"§24's beam residual"** in the
watertight clamp's per-point law. The clamp only ever met it across the beam (±half-beam, ~a
quarter metre). Bow-on, Δ is the **half-length**: −1.64 m of false depth at a 12 m lobster
boat's stern sailing north (≈1.06 m of false freeboard — more than a dory's entire freeboard),
sign-flipped sailing south, exactly zero east/west. That one sign structure explains both owner
reports ten months apart: *"the boat is visually in front of the water"* (north, 2026-08-11) and
*"you see water at the stern when the bow faces south"* (2026-07-25 — treated then as a clamp
reach problem and masked, which is why only the northern half survived to be reported again).

## Decision

**Adopt the y→z shear of the hull frame** proven in #491:

```
z −= (worldY − rootY) · cos(elev)·(1 − sin(elev))/sin(elev)      // g = 0.42571 at 40°
```

applied where the hull's frame enters the shared z-buffer (`IsoFacetHullRenderer.ApplyPose`'s
frame, beside the existing per-hull `HullDepthBias` constant). It is exact — it zeroes the ground
term at every facing and lands the height term on the true iso relation `−h/sin(elev)` — so the
z-test reduces to the only question the composite exists to ask: *is this bit of hull above or
below the water?* No heading term remains. `TargetLaw_WouldBeHeightOverSinElev` (on main since
#491) proves both halves numerically and becomes the implementation's oracle.

**Why the shear preserves the invariants the old "one constant per hull" rule protected:** under
the ortho camera two fragments sharing a pixel share a world y, so they take the same shift —
hull self-occlusion, the deck-occupant band encoding (#481), and intra-hull ordering (the golden
masters' subject) are all invariant by construction.

**The decision explicitly includes the three re-derivations.** These laws were built on top of
the wrong relation; correcting it without them would trade one lie for three:

1. **The watertight clamp** (`DisplacedWaterMath.WatertightZHeaveMeters`): its per-point law
   carries the residual as an explicit term and still governs any un-rebaked hull. The term is
   re-derived (to zero or to the new relation's honest remainder) — not deleted blind, because
   the clamp is also the fallback where the interior mask does not apply.
2. **`HullSettleMath`**: the 1.1457 flotation gain `(cos+sin)/(cos²+sin)` derives from the same
   wrong relation. Re-derive the gain under the shear; then **re-check the whole fleet's
   flotation** — every `RestingDraftMeters` on every `BoatHullDef` re-renders, and each hull's
   drawn waterline must be re-verified against its data (gameplay-systems signs off).
3. **The keyline resolve** (ADR 0031 family): rule 1's adjacent-pixel depth compare moves by
   `g/PPU = 0.013 m` — 4.4 % of its 0.30 m threshold. Re-verify the golden-mastered pass;
   marginal edges that flip get re-baselined **knowingly**, with before/after crops in the PR,
   never silently.

**Golden masters:** any that move, move because the depth contract moved — re-baseline them in
the implementing PR with the flip named in the PR body. A silent re-baseline is a defect.

## Consequences

- The stern reads *in* the water at every heading; the 2026-07-25 defect's remaining half
  closes with the same change. Waves can reach the planking a following sea should threaten.
- The per-hull depth constant stops being a tuning surface that can hide a unit error: after
  the shear it corrects only what a constant *can* correct.
- One-time churn: fleet flotation re-check (ten hulls), keyline golden re-verify, clamp
  re-derivation. This is the cost of ten months of compounding on a wrong unit, paid once.
- Un-rebaked/legacy sprite hulls do not use the facet z-buffer and are untouched; their path
  is the clamp, which is why its re-derivation ships in the same PR, not later.
- Risk: the three laws are **coupled** — any one changing means the other two re-derive. The
  implementing PR must land all three with the shear atomically; no partial merges.

## Acceptance (the implementing PR is judged against these)

1. `TargetLaw_WouldBeHeightOverSinElev` flips from "would be" to *is* — the law test asserts
   the shipped relation, and the residual tests assert **zero** residual at all 8 facings.
2. The **8-facing stern screenshot sweep** deferred out of #491: north shows water lapping the
   stern's waterline, south no longer drowns it, east/west unchanged, at low and high tide,
   calm and moderate sea. Plus a calm negative control.
3. Fleet flotation table in the PR body: per hull, drawn waterline vs `RestingDraftMeters`
   before/after, gameplay-systems-reviewable.
4. Full EditMode + PlayMode green; every golden-master change enumerated and justified.
