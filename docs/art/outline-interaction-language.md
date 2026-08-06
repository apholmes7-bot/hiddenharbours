# The outline becomes a language — survey, pilot, and a proposal for the owner

**Status: §4.1–§4.5 are still a PROPOSAL and nothing in them is built** — the owner rules on those
before any interaction-language slice is written. **§4.6 item 7 (the bulk background retirement) is
RULED and in flight**: §1–§2b record what was measured and what has shipped, family by family.

**Owner's ruling this responds to (2026-08-05 playtest, and the same night's refinement):**
> reduce the near-black outline on a lot of art, if not everything — but a light one on the
> character and boats … *refined:* outlines become an **interaction language** — things you can
> interact with get a slightly darker outline than their surroundings; anything considered
> background gets none.

That refinement turns a style knob into an **affordance signal**, which is why it deserves a
design decision rather than a bulk re-bake. It also serves the diegetic-UI vision directly:
information is earned from the world, not from floating icons.

---

## 1. The survey — what actually draws a dark edge, and with which knob

### 1.1 `grep KEYLINE` finds eight rigs and lies about six of them

The single most important survey finding is that **"the keyline" names three unrelated
mechanisms**, and a sweep that treats them alike will break shading while chasing outlines.

| # | Mechanism | What it does | Rigs | Retiring it means |
|---|-----------|--------------|------|-------------------|
| **1** | **RING (dilation post-pass)** | paints near-black into *empty* pixels adjacent to geometry — expands the opaque footprint by 1 px | `shorePlantRig` *(pilot — retired)*, `shrubIsoRig` *(wave 2 — retired)*, `treeIsoRig`/`treeIsoRig2` *(wave 2 — retired)*, `driftWeedRig` *(see §1.1a)*, `characterIsoRig`/`6`, `houseIsoRig`, `wharfBuildingRig`, `fisherRig` | delete/gate the pass |
| **2** | **RAMP SLOT (`out:` / `'__out'`)** | the outline is a *palette entry* selected per pixel and drawn **inside** the sprite | `flowerRig`, `shorelineRig`, `wharfKitRig` (`index 0 = key/outline`) | re-point the ramp slot — there is no pass to delete |
| **3** | **MIX TARGET** | `KEYLINE` is just the palette's darkest value, used as a `mix()` anchor for wet/dark shading | `rockIsoRig` (`mix(c, KEYLINE, 0.30)` = wet rock), `_rockBake` | **nothing to retire — touching it destroys the shading** |

⚠ **`rockIsoRig` is the booby trap.** It has ten `KEYLINE` hits and draws no outline at all: the
constant is the wet-rock darkening anchor. A naive "remove the keyline everywhere" sweep would
flatten every wet rock on the shore.

### ⚠ 1.1a Correction (wave 2): `driftWeedRig` was filed under the wrong mechanism

The table above originally listed `driftWeedRig` as a **ramp slot**, on the strength of its
`out: KEYLINE` palette entry, and concluded "there is no pass to delete". **That is wrong, and it
matters because it makes the family look harder than it is.** The rig has a real dilation pass —
`shade()` walks every *empty* pixel, tests its eight neighbours and sets `outl[i]`, exactly the
mechanism-1 loop — and `toRGBA` then *colours* those pixels from the `out:` slot. The ramp entry is
the ring's **palette**, not its selection rule. `out:` is used for nothing else.

So the mechanism is a clean gate, the same shape as the shore plants'. What actually blocks the
family is not the rig — see §2b.

⚠ **Rim light is a fourth vocabulary and is NOT outline machinery.** The back rim (mask G) is
derived from the alpha silhouette's distance transform and gated per-rig; ADR 0031 already
verified outline-free rebakes keep rim lighting. The lit-sprite path (#428) must not be touched
by this arc.

### 1.2 Measured on the committed pixels

A family-agnostic diagnostic over shipped art. For each sprite, opaque pixels split into the
**ring** (touching transparency) and the **interior**; `darkness = 1 − ring mean luminance ÷
interior mean luminance`; `ringCols` = distinct colours in the ring (a **flat** drawn keyline is
literally one colour whatever it wraps; a **tinted** one is a handful; form-carried edges vary).

| family | darkness | ringCols | ring % of art | reading |
|---|---|---|---|---|
| **shore plants (shipped)** | **0.57** | **1** | **15.9%** | flat ring — **the pilot** |
| boats (sprite sheets) | 0.77 | 1 | 3.4% | flat ring — *but see §1.3* |
| buildings (village) | 0.72 | 1 | 1.3% | flat ring |
| shore finds | 0.71 | 4 | 29.2% | tinted ring |
| fish | 0.66 | 1 | 21.5% | flat ring |
| characters (iso) | 0.65 | 29 | 22.7% | **tinted** ring (`keyTint`, see §1.4) |
| flowers | 0.63 | 3 | 28.4% | tinted ring (ramp slot) |
| drift weed | 0.56 | 1 | 24.5% | flat ring (ramp slot) |
| trees | 0.53 | 1 | 6.9% | flat ring |
| gear | 0.48 | 5 | 16.9% | tinted ring |
| shrubs | 0.47 | 11 | 16.2% | tinted ring |
| shoreline iso tiles | 0.11 | 82 | 7.7% | **no drawn ring** |
| road tiles | 0.06 | 20 | 2.1% | full-bleed — metric not meaningful |
| wharf tiles | 0.02 | 22 | 8.3% | **no drawn ring** |
| grass | **0.00** | 6 | 55.9% | **no drawn ring — already compliant** |

**Honesty about the metric.** It ranks *harshness*; the rig source decides *mechanism*. It
mislabels the character rig (29 tinted colours reads as "form" but the rig demonstrably draws a
ring), and it is meaningless for full-bleed tiles that have no silhouette. Use it to prioritise,
never to conclude.

**Two useful readings:** the **tile kits and grass are already outline-free** — the arc does not
need to touch them. And **`ring %` is not proportional to `darkness`**: buildings carry the
second-harshest ring over 1.3% of their art, while shore plants carry a milder one over 15.9%.
Which is the §1.5 law.

### 1.3 The fleet: already outline-free in the world

ADR 0031 shipped the mesh fleet outline-free (`GameConfig.HullKeylineFlood`, code default
`false`). The `Art/Boats/*.png` row above measures the **sprite sheets** — legacy/fallback and
mesh-source art — not what the player sees afloat. **The handoff's hypothesis is confirmed: the
owner's "light outline on boats" needs no new bake.**

⚠ **But "light" is not available today.** The shader branches
`if (_HHKeylineFlood < 0.5)` — a hard binary on a `Float` uniform. The only two states are *the
full legacy keyline* or *none*. A **light** boat outline needs the uniform read as a **strength**
(and probably a tint toward the wrapped colour), which is a small, contained change — and it is
art-pipeline's lane, not this one.

### 1.4 The character already has the "light outline" the owner asked for

`characterIsoRig6.js` does not draw flat ink. It draws `keyTint(c) = mix(KEY #101a19, c, 0.22)`
— *"tinted outline: dark, but carries the local hue"* — on 4-neighbours only. That is why it
measures 29 distinct ring colours where a flat ring measures 1.

**So for the character the knob already exists and is a single number: `0.22`.** Raising it
lightens the outline while keeping it hue-carrying. No new mechanism is required to satisfy the
character half of the owner's ruling.

### 1.5 The law the survey turns up: **the ring is a PERIMETER cost**

A 1-px ring costs pixels in proportion to *perimeter*, while the art it wraps is *area*. So the
thinner and more filamentary a form, the more of it is outline:

- **Shore plants, measured across all 16 species: 28.5% of every visible pixel was ring.**
- Correlation between a species' strap (blade/frond/culm) fraction and its ring burden:
  **Pearson r = +0.815** over 16 species.
- Glasswort (99.9% strap): **0.83 ring px per plant px** — the outline nearly outweighed the
  plant. Bayberry (0% strap, solid woody masses): 0.12×.

**The shrub rig had already discovered this law and encoded it**, exempting its filament `VEIL`
material from the keyline because *"an outline round every 1 px strand turns the whole field into
a grey solid, which is what the first pass did."* The shore-plant rig declared `STRAP` exempt
from the mass floor and from the rim — **but not from the keyline.** The pilot closes that gap.

---

## 2. The pilot — shore plants, shipped in this PR

**What changed:** one gate in `docs/art/rigs/shorePlantRig.js`. The ring pass is **retired by
default** (`KEYLINE_DEFAULT = false`) and still reachable via `{outline:true}` — deliberately
mirroring ADR 0031's own engine pattern (`HullKeylineFlood`, default off, kept as a live A/B)
rather than deleting the code.

**It is provably a pure ring deletion.** Every pixel that differs between the retired and
restored arms is a pixel with *no geometry under it*. Measured on all 16 species: 4,072 ring px
against 10,482 painted px, **0 violations** — no painted pixel of any plant changes value, so no
colour, band, rim, tide state, cell or pivot can have moved with it. Pinned by
`TheKeylineIsRetired_AndTurningItBackOn_ChangesOnlyTheRing`, which carries its own positive
control (the `{outline:true}` arm must bring the ring *back*, or "zero ring pixels" would also
pass on a broken renderer).

**Consequences that did NOT happen, and that is the point:** cell, pivot, sheet dimensions and
the atlas ink budget are **identical** — the rig's `inkOf` measures the *geometry* bounding box,
which the ring never entered. So there is no re-slice, no pivot drift, no meta churn, and the
baker's contract guard passes unchanged.

**Proofs** (in `docs/art/proofs/`):
- `outline-pilot-shore-plants-species.png` — all 16 species, before/after, each on the ground of
  its own tidal zone, dead low tide, 4× zoom.
- `outline-pilot-shore-plants-insitu-lowtide.png` — a low-tide shore, before/after, at the real
  ADR 0013 day and dusk tints. ⚠ A **composite from the shipped sheets, not an engine capture**
  (labelled as such on the image): real sprites, real low-tide state, real tints — but no
  lit-sprite response, no shadows, no water shader.
- `outline-pilot-strap-edge-falsification.png` — the three-arm test behind §2.1.

Worth noting from the dusk panels: **the two arms converge as the light drops.** The ring was
unlit by construction, so it is at its harshest in daylight and recedes at dusk — consistent
with the owner seeing it on dawn/day screenshots.

### ⚠ 2.1 The handoff's diagnosis was inverted — reporting back

The handoff named *"the shore-plant rig's strap 'dark edge'"* as the owner's worst offender. It
is not the offender; **it is the replacement.**

- The strap dark edge is an *interior shading* term (`lum += edge × 0.075` over the outer 1–2 px
  of a blade). It never leaves the silhouette.
- The near-black the owner saw is the **ring pass** — a flat `#101d21` at **10.4:1 contrast
  against dry beach sand**, and unlit by construction (the ring carries no key-light mask value,
  so it can never lighten).
- Rendering a third arm with the strap edge removed makes things **worse**: blades brighten
  ~4–12% and lose the margin that separates one from the next. That margin is precisely ADR
  0031's *"the form's own dark side"* — for a 2 px form, it is the only edge available.

**Recommendation: leave the strap dark edge alone.** Softening it, as instructed, would have
removed the load-bearing replacement for the outline while leaving the actual outline in place.
Noted in the rig and in the contract so the next pass does not re-open it.

---

## 2b. Wave 2 — trees and shrubs retired; drift weed reported and held

The owner's running order for §4.6 item 7 put the pure-background foliage cluster first. Wave 2
gates **trees** (`treeIsoRig2.js` *and* `treeIsoRig.js`) and **shrubs** (`shrubIsoRig.js`) on the
pilot's pattern — `KEYLINE_DEFAULT = false`, ring code kept, `{outline:true}` restores it.

**Both tree passes, not just the current one.** `TreeKitCatalog.HeldBackSpecies` keeps **Tamarack**
on the pass-1 rig, so its shipped sheets come off `treeIsoRig.js`. Gating only the pass-2 rig would
have left one species inked forever, and no pass-2 test could have caught it. Verified against the
committed pixels: the pass-1 rig reproduces `Tamarack_mature_summer.png` byte-for-byte, the pass-2
rig does not even match its dimensions (364×144 vs the shipped 416×149).

**Measured, per family** (variant 0; ring px against painted px):

| family | ring | painted | ratio | reading |
|---|---|---|---|---|
| trees (10 species) | 6,821 | 59,450 | **0.11×** | the AREA end of the perimeter law |
| shrubs (20 species) | 8,399 | 28,813 | **0.29×** | between trees and the shore plants |
| *(shore plants, pilot)* | *4,072* | *10,482* | *0.39×* | *— for scale* |

0 painted-pixel violations and 0 geometry movement in every case, so it is a pure ring deletion in
both families. On the **shipped sheets** the ring was 8.8% of every visible tree pixel and 21.0% of
every visible shrub pixel.

**The shrub rig carries the same trap `rockIsoRig` does, one level down.** `KEYLINE` names two
different things in `shrubIsoRig.js`: the ring pass, and the **fruit seat** — one dark pixel mixed
*under* a berry so a 2 px fruit reads as sitting in front of the canopy rather than as a hole
punched in it. The seat lands on a **painted** pixel, so it is an interior shading term and §5's
"do not soften the interior dark edge" applies to it directly. It is not gated, and the shrub test
renders the `fruit` phase specifically so that sweeping it up with the ring fails loudly.

The shrubs' `VEIL`/`FLECK` keyline exemption is **kept, not replaced** — with the flag ON it is
still what keeps a filament field from collapsing into a grey solid.

### ⚠ 2b.1 Drift weed is held back — and the blocker is not the mechanism

Drift weed is the **worst offender of the three** (32.0% of every visible pixel is ring, against
21.0% for shrubs and 8.8% for trees), and per §1.1a its ring is a clean, gateable dilation pass. It
is held back anyway, because **the family has no bake path in this repo**:

1. **There is no `DriftWeedBaker`.** Every other family here has one under
   `Assets/_Project/Code/Tools/Editor/RigBaking/`; drift weed has only a *slicer* and a *kit
   builder*. The sheets arrived as an owner drop (2026-07-23, landed in #391) and
   `driftWeedRig.render()` returns **one cell** — nothing in the repo assembles the shipped
   variants × 3 ramp-row sheet.
2. **The rig source is hash-pinned to the shipped art.** `DriftWeed.json` carries
   `derivedFromRigSha256`, and `DriftWeedSheetSliceTests.Sidecar_RigHash_MatchesTheLandedRigSource`
   fails on any edit to the rig with: *"the rig source changed after the bake; re-bake the sheets +
   sidecar together (the rig is art-director source: never edit it in-repo)."*

So gating the rig would turn a test red and assert a provenance that is not true, in exchange for
zero pixels — the sheets would keep their ring either way. **Retiring drift weed's ring is a slice
that must write the baker first**, and it should regenerate the sidecar in the same bake. Two
things that make it cheaper than it looks: the buoy pivots are computed from the geometry mask, not
from `outl`, so **retiring the ring cannot move a buoy**; and the cell dimensions are fixed by
`S.cell`, so the ring's removal cannot re-slice the sheet.

## 3. What the pilot does *not* settle

The pilot proves the **background** half of the owner's ruling on one family. It says nothing
about the **interactable** half, which is a system, not a bake — §4.

---

## 4. PROPOSAL — how the interactable outline should work

### 4.1 The principle, stated so it can be tested

> An outline means **"you can act on this."** Absence of an outline means background. The signal
> must survive every ground the object can stand on, and must never be confused with the
> object's own shading.

Two consequences fall straight out, and both are testable:

1. **Outlines must be exclusive.** If background art keeps drawn outlines, the signal is noise.
   The bulk retirement (§1.2's flat-ring families) is therefore not cosmetic housekeeping — it is
   **a prerequisite** for the language to mean anything.
2. **The outline must be applied by something that knows the interaction state**, because
   interactability is dynamic (a pot is haulable when it has soaked; a boat is boardable when
   you are not already aboard).

### 4.2 The three mechanisms, and why one wins

| | **A · Baked into the asset class** | **B · A shader term on a whitelist** | **C · Runtime outline driven by the interaction layer** ⭐ |
|---|---|---|---|
| How | interactable kits bake a ring; background kits don't | material variant with an outline term, assigned to interactable art | the interaction system raises a signal; a presenter draws the outline |
| Dynamic state | ✗ impossible | ~ needs per-instance material data anyway | ✓ native |
| "Slightly darker than surroundings" | ✗ baked against an assumed ground | ✓ can sample/derive | ✓ can sample/derive |
| Cost of being wrong | a **re-bake** per correction | a material sweep | a value change |
| New art needed | yes, per family | no | **no** |
| Fights ADR 0031 | yes — re-introduces baked ink | no | no |

**Recommend C.** A is disqualified by dynamism alone: a baked ring cannot turn off when the pot
is empty, and every tuning correction costs a re-bake of a whole family. B is really C with a
worse coupling story — it still needs per-instance data, but discovers interactability by
whitelist instead of by asking. C also keeps the outline *out of the art*, which is what lets the
background retirement be permanent.

**Architecture sketch (Core-mediated, per CLAUDE.md rule 4).** The interaction module already
computes "what is the player's current candidate" (`WorldInteractor` picks `_nearest` every
frame and drives a floating prompt). That answer is the signal. Cross-module talk goes through
Core: the interaction layer publishes it, and an art-side presenter consumes it and applies the
outline — the art module never references `WorldInteractor`, and the interaction module never
references a renderer. The `SpriteLightBinder`/`TreeTrunkAnchor` pattern (#428/#430) is the
existing mould for exactly this: an art-side component that binds per-instance draw data.

**Two states, not one** (worth an owner ruling in itself): *reachable-and-actionable* (in range,
the thing you would act on) versus *known-to-be-interactable* (visible, not in range). The
cheapest first slice is one state — the current candidate — which is also the honest replacement
for the floating prompt.

### 4.3 What counts as interactable

**The existing truth is narrow.** `Interactable` today carries exactly two verbs —
`InteractKind { Talk, Read }` — i.e. NPCs and letters, plus an optional onboarding flag. That is
the whole surface.

That matters twice over:
- The near-term set the outline would mark is **small enough to pilot safely**.
- The set is about to grow a lot: boarding/mooring (M2-39 verb, the deck/cleats vision), traps
  and buoys, the culling table, shop counters, doors and interiors. **The outline should be
  driven by the interaction system's own answer, never by a hand-kept list of prefabs** — a list
  would be stale the first time a verb is added.

**Recommended rule:** *if the interaction layer would let the player act on it right now, it
outlines; otherwise it does not.* One source of truth, and it cannot drift from the verb surface
because it **is** the verb surface.

⚠ **This will visibly conflict with the floating prompt** (`WorldInteractor.ShowPrompt` draws a
`Text` + `Outline` above the target). The diegetic-UI vision wants that gone. Whether the outline
*replaces* the prompt or ships alongside it first is an owner call, and it belongs in the same
ruling.

### 4.4 "Slightly darker than surroundings" — making it legible on every ground

The literal reading fails. A **fixed** dark outline is not "slightly darker" on every ground: the
retired plant ring measured **10.4:1** against dry beach sand but only **2.0:1** against shallow
water — the same ink is a hard black line in one place and nearly invisible in another. Any fixed
value is wrong somewhere, and the shore is precisely where the grounds vary most (sand, cobble,
mud, marsh, water, wharf deck).

Three ways to hold a constant *relationship* instead of a constant *colour*:

1. **Derive from the object, not the ground** — outline = the object's own darkest ramp step,
   pushed one step further. Self-consistent, palette-safe (ADR 0015), needs no sampling, and
   ties the signal to the art rather than the backdrop. **Cheapest and most robust; recommend as
   the default.**
2. **Derive from the ground** — sample behind the object and target a fixed contrast *ratio*.
   Most faithful to the words "slightly darker than its surroundings", but needs a read of what
   is behind a sprite, which is real work in URP 2D and interacts with the water's own
   transparency.
3. **A lit outline** — let the outline take the ADR 0013 day/night tint like everything else, so
   it recedes at dusk instead of staying a fixed black. **Note the retired ring did the opposite:
   it carried no key-light value and so stayed maximally hard in daylight** — which is exactly
   when the owner saw it. Worth folding into whichever of 1/2 wins.

**Recommendation: 1 + 3.** Object-derived, and lit like the rest of the world. Revisit 2 only if
the owner finds a real case where an object disappears into its ground.

### 4.5 Character and boats vs ADR 0031

There is a real tension and it should be named: ADR 0031 retired the outline from **world art**,
and the owner now wants a **light** one on the character and boats. These reconcile cleanly if
the light outline is understood as *the player-agency signal*, not a style revival — the
character and the boat are the two things the player **is**, so they read as permanently
"actionable" under §4.1. That is a coherent extension of the interaction language, not an
exception to it.

Practically:
- **Character: no new mechanism.** The tinted keyline exists; the knob is `keyTint`'s `0.22`
  (§1.4). Needs a re-bake of the character sheets and an owner eyeball on the value.
- **Boats: no new bake, but a small engine change.** `_HHKeylineFlood` must become a *strength*
  rather than a binary branch (§1.3), ideally tinted toward the wrapped hull colour the way the
  character's is. Art-pipeline's lane.
- **ADR 0031 should be amended, not overturned** — a short amendment recording that outlines
  return as an *interaction signal* on player-agency subjects, with the dark-side rule still
  governing every background form. If the owner rules for §4, that amendment is part of the
  first implementation slice.

### 4.6 What the owner is being asked to rule on

1. **Mechanism** — runtime, interaction-driven (C)? *(recommended)*
2. **Scope of "interactable"** — driven by the interaction layer's own answer, never a prefab
   list? *(recommended)*
3. **Contrast rule** — object-derived and lit (1 + 3)? *(recommended)*
4. **One state or two** — just the current candidate, or also a dimmer "interactable nearby"?
5. **The floating prompt** — does the outline replace it, or ship beside it first?
6. **Character/boat light outline** — proceed with the `keyTint` value + a flood *strength*?
7. **Bulk background retirement** — §1.2's flat-ring families (trees, buildings, fish, drift
   weed, shore finds, gear, shrubs, flowers), family by family as ADR 0031 §4 intends. This is a
   prerequisite for the language, so it wants a running order. **Ruled: continue the removal
   (2026-08-06).** The running order in flight:

   | wave | families | status |
   |---|---|---|
   | pilot | shore plants | shipped (#433) |
   | **2** | **trees · shrubs · drift weed** | **trees + shrubs retired at the rig; drift weed held — §2b.1** |
   | 3 | flowers · buildings | not started |
   | 4 | fish · shore finds · gear | blocked on rulings 1–6 above — these entangle with the interaction language |

---

## 5. Traps recorded for whoever builds the next slice

- **Do not sweep `KEYLINE` mechanically** — §1.1. `rockIsoRig` would lose its wet shading.
- **Do not conflate rim light with outline** — different vocabulary, different mask channel.
- **Do not soften a family's interior dark edge while retiring its ring** — §2.1. On thin forms
  the interior edge is the *only* silhouette carrier left.
- **`ring %` and `darkness` measure different harms** — a small, very dark ring (buildings) and a
  large, milder one (plants) are both worth fixing, for different reasons.
- **Check whether a family's ring is a pass or a ramp slot before estimating cost** — §1.1
  mechanism 2 has no pass to delete. **And check it in the rig, not from the palette:** a `out:`
  ramp entry does not prove mechanism 2. Drift weed has both — a dilation pass that *selects* the
  pixels and a ramp slot that *colours* them — and was filed wrong on the palette alone (§1.1a).
- **Ask where a family's sheets come from before promising pixels.** A gate in the rig ships
  nothing on its own; the family needs a baker in `Tools/Editor/RigBaking/` to re-bake. Drift weed
  has none, and its sidecar hash-pins the rig source to the shipped art (§2b.1). Check for the
  baker *first* — it is the difference between a one-line slice and a tooling slice.
- **`KEYLINE` can name two mechanisms inside a single rig.** `rockIsoRig` is the famous case, but
  `shrubIsoRig` has a ring pass *and* a fruit-seat mix target on the same constant. Gate the pass,
  never the constant.
- **A ring pass expands the opaque footprint**, so retiring it *tightens* coverage by 1 px. That
  is correct (it matches the geometry), but any consumer keyed off sprite alpha — including the
  lit-sprite path's coverage channel — sees a slightly smaller sprite.
