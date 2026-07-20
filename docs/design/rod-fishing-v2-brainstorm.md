# Hidden Harbours — Rod Fishing v2 (BRAINSTORM CAPTURE)

> **Status: BRAINSTORM — NOT canon, NOT in phase.** This captures an owner-led design
> conversation (2026-07) about making the active rod/hand-line catch feel closer to
> *Stardew Valley* and *Red Dead Redemption 2*, while staying true to the pillars. It is a
> **thinking document to pick up on the dev machine** — nothing here is built, scheduled, or
> approved. It **extends**, and defers to, the canon: [`fish-and-content.md`](fish-and-content.md)
> §3.4 (the catch mini-interaction) wins on any conflict, then
> [`../vision-and-pillars.md`](../vision-and-pillars.md).
>
> Sibling docs it consumes: [`boats-and-navigation.md`](boats-and-navigation.md) (on-water
> interactions, leave-the-helm), [`time-tides-weather.md`](time-tides-weather.md) (the live sea
> state), [`ux-and-mobile-controls.md`](ux-and-mobile-controls.md) (control mapping),
> [`../adr/0003-data-driven-content.md`](../adr/0003-data-driven-content.md) (species are data).

---

## 0. Intent

Borrow the *feel* of two reference games without importing their genre:

- **Stardew Valley** — species have **movement personalities**; **upgrades tangibly ease the
  fight** (progression you feel); an unstable-equilibrium control loop. One input.
- **RDR2** — **weight and rhythm**: tactile cast/hook/land bookends around a
  *reel-when-calm / ease-when-it-runs / tire-it-out* fight, **pulling against the fish's runs**,
  read off a bending rod and taut line. Cinematic, slow, never twitchy.

Both already rhyme with what the repo has. The trap **haul-with-the-swell** minigame
([`../../Assets/_Project/Code/Fishing/TrapHaulMath.cs`](../../Assets/_Project/Code/Fishing/TrapHaulMath.cs),
Build 6) was redesigned to the owner's verdict **"richer action, faster, diegetic"** — HOLD not
tap, the rope *is* the instrument (no HUD bars), coupled to the live sea sim, fail costs **time
only, never the catch**. Rod Fishing v2 extends that same, owner-endorsed language to the rod.

**Deliberate divergence from canon §3.4:** the v2 rod is *more* demanding than the "one-thumb,
no precision twitch" cozy baseline — it adds mouse aim/steer and repositioning. The owner has
accepted this: **rod fishing becomes the PC-rich, skill-expressive method** (mouse required;
**not** mobile-friendly for now). Traps, pots, and clams stay cozy and simple. Teeth on the daily
rod loop stay light; the real fight is reserved for Prize/Legendary fish (see §7).

---

## 1. What exists today (grounding — don't rebuild)

- **Rod fight phase machine** —
  [`../../Assets/_Project/Code/Core/Events/FishingState.cs`](../../Assets/_Project/Code/Core/Events/FishingState.cs)
  (VS-13): `Idle → Waiting → Bite → Fighting → Landed/Snapped/NoBite`, publishing a Core value
  struct `FishingState { Tension01, Landing01, FishId, Category, WeightKg }` on `FishingStateChanged`.
  UI/audio consume it **through Core only**; ui-ux reskins it into the formal HUD later (VS-14)
  with no logic change. **v2 grows the phase set and struct; the cross-module contract stays.**
- **Trap haul-with-the-swell** — the proven diegetic HOLD template (above).
- **Deck work / multi-catch pots / clam dig** — the cozy passive variants; unchanged by v2.
- **Catch resolver + species data** — [`fish-and-content.md`](fish-and-content.md) §3.2: a weighted
  spawn table assembled per cast from `FishSpecies` assets. `depthBand` flags
  (`Tidepool, Shallows, Inshore, Midwater, Deep, Abyssal`), `GearTag` (`Rod, Handline, Jigging,
  Longline, …`), bait, season, weather, time all already weight the roll. **v2 adds player-chosen
  depth and (maybe) a lure tag as new weight inputs — see §6.**

---

## 2. The v2 loop, beat by beat

### 2.1 Rig & the two ways to fish

Tackle choice decides the bite tell, whether you cast, and the depth game. **Not all fish use a
bobber.**

| | **Cast fishing** | **Depth / floor fishing** |
|---|---|---|
| Water | Surface / shallows / inshore | Deep, over the drop-off |
| Gear (data) | `Rod`, light `Handline` | `Jigging`, `Longline`, weighted `Handline` |
| Tackle | Bobber **or** light lure | Weighted rig, **no bobber** |
| Get in the water | **Flick-cast** (§2.2) | **No cast** — drop and read the column (§2.3) |
| Bite tell | Bobber dips (surface, visual) | Rod-tip knock + line twitch (audio/feel, no bobber) |

This branches on the **gear the player chose**, not a new parallel system. It also makes the
dock→deck arc a natural progression (P2): **dock = stable ground** (early, matches the St Peters
clams → Greywick rod → boat ladder), **deck = a moving platform** (later, harder — §4).

### 2.2 The flick-cast (replaces the current long/short two-cast)

A gesture cast, suited to the mouse and the 3D fishing rig:

1. **Wind back** — drag the mouse *behind* the character; the rod animates back following the mouse.
2. **Flick forward** — sweep the mouse *past* the character in the cast direction.
3. **Release** — **click to let the spool loose**; the line flies.

- **Direction** = flick vector. **Distance/power** = flick speed & length, capped per rod/tackle
  (gear upgrades extend the cap — P4).
- **Skill beat** = release timing: clicking at the right point of the forward flick lands a long,
  clean cast; too early/late = short or piled-up line.
- **Cozy fail** = a bad cast is just a **short cast** — reel in, recast. No penalty.

> Replaces the two discrete casts on the `feat/*` cast branches; it is not a third mode.

### 2.3 The depth drop + the slack "bottom" tell (standout mechanic)

Depth is **continuous** and read **diegetically — no numeric readout**:

1. Drop the weighted rig; the **line animates as it sinks** (the "still working" cue).
2. **Click to re-engage the reel** and stop at any depth → hold a mid-water band.
3. Let it run and the **lure bottoms out → the line goes visibly slack** (sitting on the floor —
   you *feel* bottom, no number).
4. **Reel up slightly** to sit just off the floor.

**Approximating depth (owner's call, decision #4):** the player judges depth by **counting how
long the lure falls** — **heavier lures fall faster**. So lure weight is a real tactical choice
(reach a deep band quickly vs. fish a slow-sinking mid-column). No depth gauge; fall-time + the
slack tell are the whole read.

**Why it matters — depth is the species-targeting mechanic.** Tie the held column position into
the resolver (`FishingContext.depthHere`, today taken from bathymetry, becomes a **player choice**
within the reachable band):

- **Bottom species** (`Bottom` flag; cod/halibut/monkfish; `Deep`/`Abyssal`) → deliberately bottom
  out, then lift just off the floor.
- **Midwater species** → **stop the drop mid-column** (click before bottoming).

Where you hold in the water column shifts the catch probabilities alongside bait/lure/season/
weather/time. Depth becomes an *active tactic*, not just where the boat sits — a P1 read neither
reference game has.

---

## 3. The fight — a deep→surface arc

Three owner ideas combine into one escalating fight: **pull-on-slack / maintain-on-fight**, **steer
the rod opposite the fish**, and **you only see the line straight down at depth; it moves around
the screen as the fish rises**.

- **Deep phase (fish unseen, line straight down):** pure **timing**. Read the runs through the
  rod-tip/line/feel. **PULL to reel when she's slack; MAINTAIN when she's fighting** (hold steady,
  keep tension in the safe band, don't gain). No steering yet — you can't see her. Job: bring her up.
- **Rising:** the line's entry point drifts across the screen — steering comes live.
- **Surface phase (fish visible, line moving around the screen):** full **mouse-steer**. MAINTAIN +
  **move the rod opposite her darts** (the RDR2 "pull against the run") to tire her without parting
  the line; PULL when she tires; then land.

**How the axes combine (still to be modelled numerically — see §8):** reeling gain is gated by
**timing AND counter-direction**. Reel *into* a run → `Tension01` climbs toward snap. MAINTAIN +
steer opposite during a run → she tires, tension bleeds, the slack window opens, PULL gains
`Landing01`. Same cozy fail as the trap haul: a lost fish "threw the hook," costs the catch + bait
time, **never damage/gear**.

- **The mouse has one consistent job:** aim on the cast, steer on the surface fight.
- **Rod follows the mouse** throughout (wind-back, flick, and steer) — one continuous read.

---

## 4. Non-stationary fishing & boat-relative facing

### 4.1 Facing is a *local heading* with a frame (extends `refactor/core-iso-facing`)

- On the **dock**: facing is world-space.
- On a **boat**: facing is **deck-space** — parented to the hull transform. When the unmanned hull
  **weathervanes in the breeze**, the character yaws *with the deck for free*, so the character
  "always matches the boat." Player movement input just writes a **new local heading**.
- You fish **unmanned** (you left the helm), so the hull drifts/weathervanes — the same
  "leave-the-helm-to-haul" pattern already in [`boats-and-navigation.md`](boats-and-navigation.md).
  The drift is reachable via `feat/seakeeping-forces` / `fix/ambient-fleet-realistic-steering`.

> **Open wrinkle → decision #1 (deferred to game designer / art director):** an **8-direction
> sprite** snaps in 45° steps, so a slow weathervane would make an idle character *ratchet* through
> poses. The **3D fishing rig** (the rigs being inserted) rotates continuously and reads smooth.
> Recommendation to discuss: use the **3D rig for on-deck idle/fight**, keep the 2D iso sheets
> (`feat/iso-rod-sheets`) for locomotion. Not decided here.

### 4.2 Movement changes your line (owner's call, decision #2)

Fishing isn't stationary — the player repositions with directional movement on the dock or **on the
deck mid-fight**, and **that repositioning changes your position against the fish, which changes
line tension** (angle/distance to the fish). So movement is a *real* fight input, not just freedom:
walk the rail to ease a bad angle, or to keep the line off the hull/motor.

- **Concurrent PC inputs during a fight:** WASD reposition + mouse aim/steer + click/hold reel.
  A deliberate skill ceiling; mobile set aside for now.
- **The moving deck as a live factor (decision #3, leaning yes, still open):** because the deck
  weathervanes during the fight, your rod's world-direction and the line angle drift under you — so
  "hold opposite the fish" is against a moving platform. Consistent with #2 (position → tension).
  Whether this is a *light real factor* (occasional reposition to keep a clean line) or *cosmetic*
  is not finally settled.

---

## 5. Fish variety, as data (ADR-0003 clean)

A **`RodFightDef`** a species opts into — same pattern as `TrapDef → DeckWorkDef` today (no Def →
the simple/legacy fight). Stardew's "personalities" become literal:

- **Strength** — how hard runs pull / how fast `Tension01` climbs (bluefin, halibut "barn door",
  striped bass on moving water).
- **Movement pattern** — how she moves on screen near the surface: darts, bulldogs deep and won't
  rise, circles, thrashes side-to-side. This is what the mouse-steer reads against.
- **Stamina / cadence** — the run↔slack rhythm; `FightsHard` = long runs, short slacks.
- **Tell** — bobber-dip vs. deep rod-tip knock, tied to tackle.

Content agents author feel **per species, no code change** — as the repo already does for deck work.

---

## 6. Probability inputs (mostly already there)

bait · lure · season · depth · location · weather · time as weighted probabilities **is** the
existing `CatchResolver` ([`fish-and-content.md`](fish-and-content.md) §3.2). v2 adds:

1. **Player-selected depth** feeding `FishingContext` (the biggest change — §2.3).
2. **Lure type** distinct from bait — likely a new `LureTag` enum. Adding a gear/bait/lure **tag**
   is the one thing that touches code and is **review-gated** ([`fish-and-content.md`](fish-and-content.md)
   §6.1); species themselves stay data.

Everything else is tuning existing weights, not new systems.

---

## 7. Cozy vs. teeth (where difficulty lives)

- **Daily fish stay forgiving** — the everyday rod loop is cozy; a lost common fish is a shrug.
- **Teeth reserved for Prize/Legendary** — the 8 gated cryptids
  ([`fish-and-content.md`](fish-and-content.md) §4.8) are the sanctioned home for a real "fight of
  your life," with bespoke diegetic tells (the Sunker King surfacing white-eyed in the fog).
- **P4 payoff (Stardew upgrades, promised in §3.4):** better rod/reel + higher angler skill widen
  the keep tolerance, speed the reel, slow the tension climb — **the fight visibly eases as you
  master it, then you delegate it** to crew.
- **Fail is always cozy:** throw the hook, cost time/bait; never damage, gear loss, or death.
  Real danger stays in weather/tide/grounding (P1/P5 anti-pillars).

---

## 8. Open threads / next

- **Decision #1 — on-deck character: 3D rig or 8-dir sheets?** (Decides smooth vs. snapping
  weathervane rotation.) → *game designer / art director.*
- **Decision #3 — does the weathervaning deck materially affect the fight, or is it cosmetic?**
  (Leaning material, per #2.)
- **The fight math (the last unmodelled piece):** how `Tension01` (snap), `Landing01` (gain), the
  reel-timing (pull/maintain), and the steer-direction counter combine into signed rates — the v2
  twin of `TrapHaulMath`'s `HoldLineRate`/`SwellRopeLoad01`. Should be a **pure, EditMode-testable,
  deterministic** static class (rule 5), no RNG, nothing saved — sea state and `RodFightDef` in,
  tension/landing rates out.
- **Phase-set growth** for `FishingState`/`FishingPhase` (e.g. `Aiming/WindBack → Casting → Sinking
  → Waiting → Bite → FightDeep → FightSurface → Landed/Snapped`), keeping the Core struct contract
  so ui-ux (VS-14) and audio consume without logic changes.
- **Control mapping** (mouse gesture thresholds, gamepad right-stick fallback for aim/steer) →
  [`ux-and-mobile-controls.md`](ux-and-mobile-controls.md).

## 9. Touchpoints (for whoever picks this up — not a schedule)

- **gameplay-systems** — cast-flick, continuous-depth + slack tell, the fight math, phase machine.
- **lead-architect / Core** — `FishingState` phase-set + struct growth; the facing *frame*
  (world/deck) in `refactor/core-iso-facing`; any new enum (`LureTag`) is review-gated.
- **art-pipeline** — 3D fishing rig, rod-follows-mouse, sinking-line + slack/bottom line states.
- **ui-ux** — VS-14 HUD/diegetic readout consuming `FishingState`.
- **audio** — bite knock, reel, strain/snap, land.
- **world-content / economy-sim** — species `RodFightDef` authoring; depth/lure weighting balance.

> Stay in phase (CLAUDE.md rule 8): none of this is M0/M1. Capture, discuss, then slot into the
> roadmap deliberately.
