# ART BRIEF — the small-boat pass: dory, her motor, punt, console skiff

**To:** art-director · **From:** the owner, 2026-07-25
**Status:** requested. The dory half is partly done (motor board, stern bench, ring bolts landed).
**Companion briefs:** [`dory-outboard.md`](dory-outboard.md) (Ned's motor in detail) ·
[`fuel-and-fuel-storage.md`](fuel-and-fuel-storage.md) (cans, pumps)

The dory was revised to carry a motor and gained visible mooring fittings. **This pass brings the
other two open boats up to that same standard, and builds the one piece of art that does not yet
exist: Ned's engine.** Four boats, one coherent pass, so the small end of the fleet reads as a set.

---

## 1. Colour is SELECTABLE — so the requirement is material separation, not a palette

The owner has full colour selection, built into the rigs by the art director. So this brief does
**not** hand you a fixed scheme. What it asks for is the thing that makes selection possible:

> ⭐ **Every independently recolourable surface needs its own `MATS` entry.**

That is the whole requirement, and it is structural rather than aesthetic. Two surfaces sharing a
ramp can never be picked apart later — if the cove pinstripe and a cowling both draw from `GOLD`,
the player recolours them together whether they meant to or not. Splitting by **role** — topsides,
sheer band, cove, interior, gunwale cap, ironwork, engine — is what turns a painted boat into a
paintable one.

### ⚠️ The dory cannot currently be painted at all

Measured, not assumed: `doryIsoRig.js` declares exactly two ramps — `RAMP` (the wood) and `IRON` —
and **every face in her list is `mat:'wood'`**. One surface, one colour. There is nothing to select
between.

So if she is to take player colour, her faces must first be **split into roles** (topsides ·
interior · sheer/gunwale cap · thwarts), each with its own `MATS` entry. That is a rig change, and
it changes her material ids, so **her mesh needs re-baking after it** — worth knowing before it is
scheduled as a small job.

**Please do not do this on your own initiative.** Her bare wood is currently the strongest thing the
palette says (§1.1), and turning her into a paintable boat is a design decision, not a consistency
fix. Ask first.

### 1.1 What the palette currently says, and what is worth preserving under selection

**The dory is the only boat with no paint on her** — `WOOD` + `IRON` and nothing else, where every
other hull wears white topsides, a teal sheer band and a gold cove. That reads as: *she is the boat
you were given; they are the boats you buy.*

Under player colour selection that contrast is no longer automatic. **If she becomes paintable, the
question is whether she starts bare and painting her is an earned beat, or whether she is simply
another boat with a colour picker** — those are very different games. Owner's call (§7).

### 1.2 The ramps as they stand today

Useful as defaults and as the vocabulary the fleet already speaks. **Reuse them where a surface is
not player-selectable; do not introduce a new ramp without saying so** (ADR 0015 palette guard-rail
— ramps only, dark → light).

### The fleet scheme (punt · console skiff · sport skiff share these exactly)

| ramp | hex, dark → light | what it is |
|---|---|---|
| `PAINT` | `#5d6a70` `#7e8c90` `#a3b0b1` `#c2cdca` `#dde5df` `#eef0ea` `#f7f8f3` | white topsides |
| `TRIM` | `#0d3f3c` `#14554e` `#1c7367` `#2ba39a` `#49b8aa` | **teal** sheer band + bottom |
| `GOLD` | `#7a5a1c` `#a8842a` `#e0b13a` | cove pinstripe |
| `WOOD` | `#33271b` `#473627` `#5e4630` `#6b4f35` `#8a6a48` `#9a7853` `#a98352` | bare interior — **this is the dory's ramp** |
| `IRON` | `#20180f` `#2a2014` `#3a2c1c` | fittings, ring bolts, thole pins |
| `MOTO` | `#101317` `#1d2127` `#2b323a` `#3d454e` `#525c63` `#6b767b` `#8a9499` | engine grey-blacks |

Per-boat extras already in use: `RED` `#4a100e` `#7c1a15` `#a8241b` `#cf3626` `#e2573c` (punt's
upgrade stripe) · `STEEL` `#3a4148` `#565f66` `#7a858c` `#9fabb1` `#c3ced2` `#e6edee` (skiff motor)
· `CANV` `#2a5750` `#3d7469` `#559182` `#74ad97` `#97c6ab` (console canopy) · `GLAS` `#16333c`
`#24505a` `#3a7680` `#5fa3a6` `#8fc9c4` (windscreen).

### ⭐ The one thing the palette already says, and must keep saying

**The dory is the only boat with no paint on her.** She carries `RAMP` (= `WOOD`) + `IRON` + her key
`#1c140d`, and nothing else — no white topsides, no teal, no gold. Every other boat in the fleet
wears the scheme.

That is not an omission, it is the story: **she is the boat you were given, and they are the boats
you buy.** Nothing in this pass should paint her. If she ever gets colour, that is a progression
beat someone decides on purpose, not a consistency fix.

---

## 2. Ned's two-stroke — the only genuinely new art

Full shape brief in [`dory-outboard.md`](dory-outboard.md). Colours, since that is the ask:

- **Body: `MOTO`.** Same family as every other engine in the fleet — it should read as an engine
  first.
- **⚠️ But it must read as OLDER than the punt's.** The punt's `basic` is a maintained four-stroke;
  this one sat in a shed. Suggested handling, your call on execution: sit it **lower in `MOTO`**
  (skew toward `#1d2127`–`#3d454e` rather than the bright `#8a9499` end) so it reads as dulled and
  chalky rather than freshly cowled, and let `IRON` show at the clamp, the bracket and the
  pull-start where paint would have worn through first.
- **No `RED`.** That ramp means *the punt's upgrade* in this fleet's language; putting it on Ned's
  engine would say "newer", which is the opposite of true.
- If you want one spot of colour — a faded maker's decal, a stripe gone chalky — propose it, but a
  single hue at 4 px will read as noise unless it sits on a flat panel. Your judgement.

**Size:** visibly smaller than the punt's `basic` in cowl volume and leg length (that comparison is
the whole point of the ladder). Cell **188 × 156, pivot (94,88)** — see the outboard brief §1.

---

## 3. Punt — bring her up to the dory's standard

She is the boat immediately above the dory, and she is now the *only* small boat without visible
fittings.

1. **Visible mooring fittings, matching the dory's**, in `IRON`: a stem-head ring bolt, and one
   stern ring on the quarter knee. Same humble treatment — a ring you can see through, no plate
   behind it, no chandlery. Her data-only painter point does not move.
2. **A mirrored `CLEATS` section in her sidecar** (`bow_1`, `stern_port`), with the same provenance
   notes the dory now carries.
3. **Deck obstruction `_notes`** — thwarts + stern bench, footprint *and height above the floor
   plane*. Heights are the load-bearing part: they decide step-over versus wall.
4. ⭐ **The sole measurement** — see §5. This is the item I actually need.

Her paint is unchanged: `PAINT` topsides, `TRIM` sheer band and bottom, `GOLD` cove, `WOOD`
interior. Nothing in this pass repaints her.

---

## 4. Console skiff — the same two things

1. **Visible mooring fittings** in `IRON`, matching the dory and punt. Note her sidecar already
   types her bow point as a `cleat` rather than a `painter` — she is aluminium and a bit more
   modern, so if a small cleat is more honest on her than a ring, do that and say why. Consistency
   of *treatment* matters more than identical hardware.
2. **Deck obstruction `_notes` + the sole measurement**, as §3.

Her palette is unchanged: fleet scheme plus `CANV` canopy and `GLAS` windscreen.

**Sport skiff:** not named in the ask, but she shares the hull envelope and the same motor. If the
fittings are cheap on her too, do her in the same pass and the small end of the fleet is finished.

---

## 5. ⭐ The measurement that matters more than any of the art

For the punt, the console skiff and (if cheap) the sport skiff, the same numbers you produced for
the dory:

- widest sole width, and width at the ends
- the pocket sizes between full-width obstructions
- how many crossings separate the authored stations

**Why:** the dory was ruled **stations, not a walkable deck** because her sole is 0.45 m at its
widest — narrower than a standing stance. The punt is *flat-floored and beamier* (her `floorPt`
uses the bottom width where the dory uses a narrow bilge) and stiffer in roll (4.2° vs 5.0°), so she
may genuinely be walkable at 5.2 m.

**Where the fleet stops being stations and starts being decks is currently unknown, and M2-37 will
design against whatever we assume.** One measuring pass settles it for the whole small end.

---

## 6. What is NOT wanted

- **No repainting.** Every hull's scheme is already right, and the dory's bare wood is deliberate (§1).
- **No sprite sheets for the motor** — the fleet is mesh (ADR 0022 phase 7). `renderMotor()` is
  needed only as the acceptance oracle. The *fittings* are hull geometry and bake with the hull as
  usual.
- **No new ramps** unless you argue for one.
- **No deck furniture holes in the floor polygons** — obstructions are `_notes`, colliders are
  game-side (owner ruling, 2026-07-22).

---

## 7. Open

1. **⚠️ Colour selection and the boats — how far does it reach?** Which ramps are player-selectable
   and which are fixed? (Engine grey and ironwork are probably fixed; topsides, sheer and cove are
   probably not.) And: **should the dory be paintable at all** (§1) — she structurally cannot be
   today, and making her so is a rig change plus a re-bake, not a tweak.
2. **⚠️ Does Ned's motor carry a fuel tank aboard, and if so what kind?** A small two-stroke either
   has an integral tank on the cowl or a separate portable tank sitting in the boat with a hose.
   The second is a visible object on her sole — it interacts with the obstruction notes, the jerry
   can, and how the fuel economy reads on screen. **Owner's call; it changes what you author.**
2. The oil-tin question from
   [`../../design/fuel-and-refuelling.md`](../../design/fuel-and-refuelling.md) §7 Q4 (does a
   two-stroke premix get modelled) is still open and still blocks that one prop.
3. Console skiff: ring or small cleat? (§4)
