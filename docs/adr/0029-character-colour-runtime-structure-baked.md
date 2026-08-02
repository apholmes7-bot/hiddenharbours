# ADR 0029 — The character: colour is runtime, structure is baked

- **Status:** Accepted (lead-architect, 2026-08-02; the layer question decided by measurement)
- **Deciders:** lead-architect, art-director, tools-editor
- **Phase:** M1 (owner-directed arc — the character creator and the wardrobe economy are gated
  on this split)
- **Related:** ADR 0021 (in-engine JS rig baking — the licence fence), ADR 0026 (rig pivot
  conventions), ADR 0003 (content is data), `docs/art/rigs/README.md` § the character rig kit,
  pass 6

## Context

The owner wants a character creator at the start of the game and clothes that are purchasable
and swappable later. The pass-6 rig can express all of it: seven colour axes, seven structural
ones, ten built cast members — 3136 wardrobe combinations before proportions are touched.

**But no JavaScript runs in a shipped build.** ADR 0021 makes the V8 bake an editor-only
operation, and that fence is licence-load-bearing, not hygiene. So nothing at runtime can ask
the rig to draw a person. Every appearance the game can show must already exist as pixels, and
3136 × 14 animations × 8 facings is not a set of pixels anyone is going to ship.

That is the whole problem this ADR exists to solve, and it has a seam in it. The rig does not
draw a person one way: **it draws geometry, and then it colours the geometry from ramps.** Sex,
age, garment, hat, hair, beard and the proportion dials change what is drawn. Skin, hair colour,
outfit, shirt, hat band, apron and eyes change only which ramp a material reads. The repo has
already settled the same distinction twice — the shrub kit's "swap ramp ROW, not structure", the
tree kit's colour variants — but never for a thing the player customises.

## Decision

**Split the character's parameter space along that seam.**

1. **COLOUR IS RUNTIME.** All seven colour axes (skin 9 · hair 9 · outfit 6 · shirt 7 · hatCol 6
   · apronCol 3 · eyes 5) ship as palette ramps in data and are applied by ramp swap. Colour
   never multiplies the bake. This is what makes a shirt colour free — to buy, to change, and to
   store in a save as three characters of key.

   The ramps are **lifted, not transcribed**: `CharacterRampsBuilder` generates
   `CharacterRampsDef` from the kit's own `docs/art/rigs/character/options.json`. Sixty-odd ramps
   of five or six hex values is exactly the table that gets copied once and drifts on the next
   drop. Ids are `ramp.<axis>.<key>` and **append-only** — a `ramp.shirt.ochre` sitting in a save
   as the player's build must resolve forever.

2. **STRUCTURE IS BAKED.** Sex, age, garment, hat, hairStyle, beard, height, weight and headSize
   change geometry, so they exist only as sheets baked in the editor.

3. **NPCs bake per cast preset.** Ten presets in the rig's own `BUILDS` table, bounded, no
   explosion. The player is one of them (`fisher`) and travels the same path.

4. **Anchors are data.** `anchors()` / `tool()` / `carry()` become JSON sidecars per sheet set.
   Gameplay pins every prop from a sidecar, never by hand.

### The layer question, and how it was answered

The open question was whether structure could ALSO be cheap: if the rig rendered separable
layers, we could bake a few bases plus one overlay per garment/hat/hair/beard and let the
creator combine them freely — thirty-odd sheets instead of thousands.

**It does not.** `CharacterLayerSeparationProbe` measured it (menu: *Dev ▸ Probe Character Rig*;
full output quoted below), across five poses spanning the away, profile and toward-camera
facings and both a mid-stride walk and the dig's f8 torso twist. Two measurements:

**1. Containment** — change one axis, classify every differing pixel. An overlay composites, so
it can only ADD.

```
garment workshirt→oilskins @dir4 idle f0 : 180 delta (2 added, 178 repainted, 0 erased), 85 in the head band
garment workshirt→oilskins @dir6 walk f3 : 127 delta (5 added, 120 repainted, 2 erased), 51 in the head band
hat none→souwester       @dir2 walk f3 : 111 delta (28 added,  81 repainted, 2 erased)
hairStyle crop→long      @dir0 idle f0 :  84 delta (16 added,  68 repainted, 0 erased)
```

Two things are already fatal here. **Six pixels are ERASED** across the set — a layer cannot
subtract silhouette from the layer beneath it. And **326 of the garment's delta pixels land in
the head band**: changing a torso garment repaints the face. A renderer that re-solves the head
when you change the coat is not compositing parts, it is re-solving the figure. The
added/repainted ratio says the same thing quantitatively — 2 added against 178 repainted is not
a layer going on top, it is the same pixels being drawn again differently.

**2. Independence** — make the same change over two different bodies and compare the deltas. The
share that matches is the share a baked overlay could actually reuse.

```
garment  workshirt→oilskins  under beard none vs full :  95.9% reusable
hat      none→souwester      under beard none vs full :  99.1% reusable
hairStyle crop→long          under beard none vs full :  88.6% reusable
garment  workshirt→oilskins  under age adult vs elder :  31.4% reusable
hat      none→souwester      under age adult vs elder :   9.9% reusable
beard    none→full           under age adult vs elder :   0.0% reusable
```

Across a **skeleton** change (age moves the head-to-body ratio and the elder gains a real stoop)
reuse collapses to nothing, which settles the creator's actual question: an overlay baked for an
adult cannot be composited onto a child or an elder. Within a fixed skeleton the numbers are
close — but 88.6% on hairStyle is not "nearly separable", it is a visible seam on one pixel in
nine, at a resolution where the whole head is about ten pixels across. Hair and beard genuinely
share the sideburn; they are not independent layers pretending to be, they are one solved
surface.

**⇒ OUTCOME B. The structural axes are integrated.** A layered structural bake is not available
from this rig.

### What Outcome B costs, and what it does not

- **The creator's structural axes ship preset-quantized.** The player picks a cast build (and
  whatever bounded structural set a later bake affords), not a free combination of garment × hat
  × hair × beard. Every COLOUR axis stays free — that is the whole point of the split, and it is
  where most of "looks like me" lives at 32 px/m anyway.
- **The wardrobe economy still works.** Colour changes are instant. A structural change (a
  different coat) is a sheet switch, so a garment that is genuinely a different shape needs its
  sheets baked — content, on a bounded list, priced in a Def.
- **The escape hatch is an art-director ask, not an engine change.** A pass-7 export that renders
  a garment / hat / hair layer in isolation over a stated base would flip this to Outcome A with
  no change to the split above: colour would still be runtime, structure would still be baked,
  and the bake would simply get cheaper per combination. The probe is committed and is the
  acceptance test for such an export — re-run it and read the verdict line.

## Consequences

- `CharacterRampsDef` (Core) + `CharacterRampsBuilder` (App.Editor) carry the colour space.
- `CharacterRigBaker` bakes structure: one sheet per (build × anim × power × carry stance).
- The creator slice (S4) is scoped to preset-quantized structure + free colour, and must not be
  designed as if free structural combination were coming.
- `CharacterLayerSeparationProbe` stays committed. It is cheap, it is the acceptance test for a
  future layer-isolating export, and re-running it is how this ADR gets revisited rather than
  re-argued.

## Alternatives considered

**Bake the combinatorial space.** 3136 wardrobe combinations × 25 sheets each is not a shippable
texture budget by three orders of magnitude. Rejected without measurement.

**Run the rig at runtime.** Rejected by ADR 0021, and that fence is licence-load-bearing. If you
find yourself wanting the rig at runtime, you are re-opening ADR 0021, not this one.

**Composite layers anyway and accept the seams.** Tempting on the within-skeleton numbers
(95.9% / 99.1%). Rejected: the failures are not distributed noise, they are the silhouette edge
and the hairline — the two places the eye goes. And the erased pixels mean some combinations
would show a stripe of the body through the coat.

**Treat colour as baked too, for uniformity.** It would make the split simpler to explain and
make the wardrobe unaffordable. The whole reason a shirt can be bought is that its colour is not
pixels.
