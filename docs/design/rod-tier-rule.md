# Rod reach proposal

PROPOSAL, nobody has accepted this

Give the coast rod more cast reach. Keep the cane rod useful in near water with
exactly the same species, bite sequence, strike window and fight rules. A better
rod buys access to water farther from the player; it does not make a nearby fish
impossible on the first rod. Both still require the species licence to keep it.

## Existing seams

The baseline has one purchasable rod, `GearOffer` at
`Data/Resources/Catalog/Gear/Rod.asset` (`gear.rod`). Cane/coast/deep presentation
assets are not separate purchasable gear definitions. A rod rack is storage.
`GearOffer` is extended rather than introducing another gear definition type.

`FishingController.ReleaseFlick` already hands a cap to the pure Core
`FlickCastMath.Evaluate`. The wind-back preview uses the same global cap.
`BiteSequenceSim` owns the later nibble/take/strike sequence with injected random
state. Weighted rigs use `DepthDropMath.FloorMeters` and the existing line length;
that branch does not cast. Reach is therefore a small, separate rule with an
existing test suite, and it does not need a new fight or line-breaking mechanic.

## Rule and defaults

`GearOffer.RodCastCapMetres` is an additive, unsaved tuning field. Zero inherits
the existing cap exactly. Positive finite metres replace it. Negative, NaN and
infinite overrides inherit; they never create an infinite reach. The helper
returns the inherited value unchanged, retaining the cast maths' existing
handling of malformed global settings.

The pure `FlickCastMath.ResolveRodCastCap` is the only new rule. The existing
cast maths still limits the cozy minimum to the cap and keeps short casts
possible. The same resolved cap must feed both aim and release.

An initial balance proposal is a 12-metre cane rod and an 18-metre coast rod.
The cane figure matches today's global cast reach. These are examples for review
and test fixtures, not authored stock: no rod asset, price or existing config
value changes here. Future gear data owns the chosen numbers. No fish gets a
new gear exclusion, and no deep-water or catch-size advantage is implied.

## Controller handoff

The controller is protected by the unlicensed-release work. This proposal does
not edit it. Once that work settles, resolve the equipped gear's scalar through
the Core boundary and make this call in a shared reach accessor:

```csharp
FlickCastMath.ResolveRodCastCap(equippedRodCapMetres, s.MaxCastDistanceMetres)
```

Use that accessor in **both** `ReleaseFlick` and the wind-back aim calculation.
The controller must obtain the scalar through a Core equipment contract; it
must not import Economy's concrete `GearOffer`. Equipment selection and that
adapter are not wired in this proposal. A missing equipped rod override passes
zero. Do not alter the licence verdict, bite schedule, bait spend, fish roll or
land/release sequence while wiring reach.

## Verification and limits

Extend `FlickCastMathTests`, which already tests cap scaling, aim agreement and
determinism. Added cases cover zero and malformed inheritance, positive caps
on either side of the global cap, a cap below the cozy minimum, a full cast,
shared near-water access, exact legacy asset/default equivalence, and repeated
results with other rods interleaved. The existing result struct's equality
checks every field, without tolerance, for the compatibility and replay cases.

No production caller opts in and no authored cap is nonzero, so the proposal
changes no visible output. No save step is needed for a static Def tuning field;
future ownership uses the existing gear id. The editor, compilation, EditMode
execution and player feel were not verified locally. CI must run the tests;
balance and controller integration still require a later review.
