# Capture — Deck boarding, cleats & ropes, diegetic interact highlight

> **Status:** owner vision capture, 2026-07-21. Not yet built; backlog rows M2-37..M2-39. This doc
> records the ask, the recommended data pipeline, and the acceptance seeds so nothing is lost between
> now and the M2 pull. Canon homes when these graduate: `boats-and-navigation.md` (deck/cleats),
> `diegetic-ui-and-inventory.md` (interact highlight).

---

## 1. What the owner asked for (2026-07-21, verbatim intent)

1. **Walkable decks & washboards.** Should the art director define the deck areas of the boats, and
   the walkable washboard areas? The player presses **Space to climb onto the washboards** of boats
   that support them.
2. **Cleat points & rope toss.** Boats have cleat points where ropes can be tied — e.g. the lobster
   boat: *"3 on stern, 2 mid ship and 1 on the bow or 3 on bow."* The player can **grab a rope and
   toss it towards a shore cleat in the same manner you cast in the new fishing mini game**.
3. **Diegetic interact highlight.** *"A shader of some sort highlights an object in front of the
   player that can be right clicked or something to interact — pick up a bucket, a rod, place a
   bucket in a place. The shader is just an indicator, without having a distracting UI."*

Pillars served: P1 (mooring in a moving sea is seamanship), P3 (a working coast where ropes and
buckets are real), P5 (physical, cozy-with-teeth interactions instead of menus). The winch-era
automation of mooring, if ever, is P4 and much later.

## 2. The answer to "should the art director define these?" — yes, as RIG DATA

**Yes — and in the rig source, not as painted overlays or hand-placed Unity markers.** ADR 0022
phases 1–2 (merged 2026-07-21) make each boat rig a queryable geometry source: the same export-object
mechanism that is delivering `F`, `MATS`, `GAIN`, `BIAS`, `LN` per hull can carry gameplay geometry.
Ask the art director to add, per boat rig, in hull-local metres:

| Symbol | Shape | Meaning |
|---|---|---|
| `DECK` | polygon(s) | walkable deck area(s) |
| `WASHBOARD` | strip polygon(s) | walkable washboards, only on hulls that have them |
| `CLEATS` | named points | tie-off points, e.g. `bow_1`, `mid_port`, `mid_stbd`, `stern_1..3` |

Cleat **counts and placement per hull are an owner/art-director design conversation** — the table
above is the schema, not the layout. Absence of a symbol = the hull doesn't support the feature
(a punt has no washboards); that absence is data, not an error.

**Why rig-data and not hand-authoring (lessons already paid for):**

- The baked anchors JSON turned out to be dead code — the runtime's real motor mount was a
  hand-transcribed constant on `BoatVisualDef`. Hand transcription is the failure mode; the
  extractor must carry these sets **straight through to Def assets** with no human copy step.
- One source of truth: the same rig then drives the sprite bake, the 3D mesh, *and* gameplay
  geometry — when the art director moves a rail, the walkable area and cleats move with it.
- Content-is-data (ADR 0003): per-boat cleat sets and deck polygons are Def data; zero per-boat code.
- ⚠️ **Sprite hulls need the iso foreshortening applied per-artwork** when projecting world-metre
  rig data to screen (the overlay-pose lesson: never rescale a tuned constant onto a different lever
  arm). Mesh hulls (phases 4–5) get this for free from the projection itself. The extractor should
  emit hull-local metres and let each presenter project.

**Do it now, cheaply:** fold `DECK`/`WASHBOARD`/`CLEATS` into the already-open export ask to the
art director, and teach `RigMeshExtractor` to pass named point/polygon sets through when present
(per-symbol probing already tolerates them arriving one hull at a time). The gameplay features then
build on data that already exists.

## 3. Feature sketches (acceptance seeds)

### M2-37 — Deck & washboard boarding (Space to climb)
- On-foot movement on a boat is constrained to the `DECK` (+ `WASHBOARD`) polygons of that hull.
  **(The `DECK` half is built** — rig sidecar → `BoatDeckDef` → per-hull polygon clamp, with the
  artwork's iso foreshortening applied when the hull metres are projected onto the drawn hull.
  `WASHBOARD` strips are imported and tagged but stay out of the free-walk area: they are what the
  Space climb promotes, and that verb is still to build.**)**
- `Space` climbs deck↔washboard (and boat↔wharf where sensible) **only where the hull's data offers
  it**; no prompt on hulls without washboards.
- The boat keeps riding the wave field underneath the player (the deck is a moving platform —
  reuses the shared wave/rock phase, same as the leave-helm gaff-haul in M2-33).
- Extends M2-33's leave-the-helm precedent from "stand at the rail" to "walk the working deck".

#### The tide decides HOW you get aboard — the ladder route (**BUILT 2026-08-07**)

A wharf deck stands still above chart datum; a boat floats. The vertical gap between the planks and
her deck is therefore **tide-driven**, and past some state of the ebb it stops being something a
fisher can step across. Past that, the boarding move goes down the wharf **ladder** instead — the
same `E`, the same gates, the same landing, a different way across the last stretch.

- **The threshold is data**, not a literal: `GameConfig.LadderBoarding.BoardClampMetres`.
  ⚠️ The art kit states two numbers for it and they measure *different quantities*.
  `characterIsoRig6.js` cites **1.2 m** on the deck-to-**water** drop as where its `board` clip
  soft-clamps; `wharfIsoRig.js:1103` *implements* a stricter **0.55 m** on `drop − freeboard` — the
  deck-to-**gunwale** gap, which is the quantity the config actually compares. **We ship 1.2**, so a
  step aboard survives the top of an ordinary tide and the ladder is what the ebb earns you. Dial the
  one field to 0.55 for the wharf kit's stricter rule; no code moves.
- **Ladder placement is region DATA** — the wharf builders place a `WharfLadder` from the same
  fittings table that positions the ladder sprite, so the ladder you can see is the ladder you can
  climb. Gameplay reads it through Core (`IBoardingLadder` / `BoardingLadders`), never the component.
- **A wharf with no ladder is a valid wharf.** Boarding falls back to the step however deep the gap;
  nothing strands the fisher.
- ⚠️ **The descent is a STAIR, not a ramp.** The climber drops when a leg extends — a rung eased
  through each foot swing, dead flat while both feet are planted. `LadderBoardingMath.DescendMetresAt`
  is a twin of the rig's own `ladderCurve()`, pinned frame-by-frame against the shipped
  `FisherFightAnchors.json`. Driving it at the clip's average rate instead slides a planted sole
  ~2.5 px at the locked 32 px = 1 m — better than a quarter of a rung.
- **The climb is never rate-scaled** the way the boarding vault is: real rungs are a real distance
  apart, so a deeper gap takes *longer*. That is what makes the tide legible on the way down.
- ⚠️ **Three gaps the 6.2 kit leaves open**, and what covers them today: the **turn-around at the
  top** and the **step-off at the bottom** are unauthored — both are covered with the authored
  `boardDown`/`board` step rather than a hard cut (the kit names that cover and says the turn-around
  is the one players notice). And there is **no `ladderUp` at all**, so going up reuses the descent
  stair sign-flipped: the rung quantization carries over exactly, the limb choreography does not.
  All three become clip swaps the day the kit authors them; none of them moves the maths.
- ⚠️ **Still open (not this slice):** `board` sheets are baked at `railZ` 0.55 m (a dory sheer). A
  hull with a different sheer wants its own sheet — the multi-hull case needs `railZ` on
  `CharacterState` and one sheet per height.

#### ⚠️ Not every hull is walked — the dory has STATIONS (owner-ratified, 2026-07-25)

The smallest boats are not scaled-down decks; they are a different thing, and the dory's own
geometry settles it. Measured by the art director off `doryIsoRig.js` (the sidecar's `DECK[0]._notes`
carries the full working):

- The sole is a **centreline strip: 0.45 m at its widest** (y −0.72) and **~0.25 m at the ends** —
  **narrower than a standing stance**, so a player's weight physically cannot leave the centreline.
- **Three full-width crossings inside 2.35 m of length** (two thwarts + the stern bench). Every seat
  spans the sole wall-to-wall — seat half-widths are ~2× the sole's — so none is an island to walk
  around.
- Seats clear the floor by exactly **0.24 m** (`SEAT − FLOOR`, identical bow to stern by
  construction): **step-overs, not walls**.
- Sheer is only ~0.40 m above the sole amidships, and her rock loop is lively (roll 5°, pitch 3°).

**Ruling: the dory is authored as STATIONS plus a move-aft transition, not free walking.** The
supporting fact that makes this cheap as well as truthful: her two authored stations — the rowing
position (y −0.30) and the motor position (y −1.28) — sit in **adjacent pockets separated by exactly
one crossing** (`thwart_aft`). Fore-to-aft is *one deliberate step-over*, not four. A nav mesh with
step-over traversal would cost more and model her worse.

**⏳ Where the line falls is OPEN.** The Cape Islander and up are unambiguously walked. The punt is
the interesting case and must be **measured, not assumed**: she is *flat-floored and beamier* (her
`floorPt` uses the bottom width where the dory uses a narrow bilge) and her roll is stiffer (4.2° vs
5.0°), so her sole may genuinely be walkable at 5.2 m. Same for the skiffs. **M2-37 must not design
against "all small boats are stations" until those measurements exist.**

Note this ruling is about *locomotion*, not about the symbols: the dory still exports `DECK`,
`CLEATS` and the rest as data. A station is a place you occupy; the polygon is still what contains
you while you occupy it.

### M2-38 — Cleats, ropes, and the toss-a-line moor — **BUILT 2026-08-06**

- Each hull exposes its named `CLEATS`; shore furniture (wharves, floats) has counterpart cleat/bollard
  points (world-content authors those in-scene — shore is hand-authored, boats are rig data).
- Player at a cleat grabs a line; the **toss reuses the fishing-cast verb** (same input feel, same
  skill curve — one verb, two contexts).
- A made-fast line **actually holds the boat** against tide/wind drift (the sim keeps computing;
  the rope constrains). Cast quality can affect whether the loop catches (cozy fail: the line slips
  into the water, coil and try again).
- Un-tying is the same interaction in reverse. No menu.

**How it landed.** Canon home is now `boats-and-navigation.md` §9.6, which carries the full tide law, the
tuning, and the worked St Peters numbers. In brief:

- **Both sides are data.** Hull cleats ride the rig sidecar (`BoatDeckDef.Cleats` → `BoatCleats`, projected
  onto the drawn hull through `DeckAreaMath.DeckToWorld` so a bow cleat is where the bow is *drawn*). Shore
  cleats are placed by the wharf builders from **the same fittings table that positions the bollard
  sprites** — so the bollard you tie to is the bollard you can see, with no second copy of the geometry.
  Both register into a Core `MooringCleats` registry, mirroring `StandableSurfaces`.
- **One verb, and literally one function.** The pure `FlickCastMath` moved from Fishing up into **Core** so
  the rope toss resolves through the very function the rod uses (and previews through the very preview the
  rod shows) rather than growing a second copy — rule 4 plus the never-compute-one-quantity-two-ways rule.
  The shared cast flick is arbitrated between rod and rope by `CastActionClaim`, claimed by *proximity* to
  a cleat so the outcome can never depend on component execution order.
- **The tide is the mechanic.** A line's scope must cover the 3-D gap between two cleats, and only the
  shore end holds still. `MooringLineMath.HorizontalReach` is the one place that law is written; it covers
  the falling-tide and rising-tide hazards from a single absolute value. Failure is a **slipped loop** — no
  damage, no parted rope.
- **Not built, on purpose:** a second line (bow + stern), a winch (P4), rope damage/breaking strain, rafting
  boat-to-boat, and the wharf rig's own mount-symbol export (art-director — when it lands, only the shore
  cleats' PLACEMENT changes, not `ShoreCleat` and not a single consumer).

### M2-39 — Diegetic interact highlight (shader, no UI)
- An `IInteractable` seam in Core; a facing-aware detector on the player picks **exactly one**
  current candidate (nearest in a forward arc).
- The candidate gets a **subtle shader highlight** (outline/rim on the sprite — art-pipeline owns
  the look; it must read at KTC palette values and in night scenes without glowing like UI).
- One bound input (owner said right-click; route it through `InputService` intents — bindings
  retarget, intents don't) performs the context action: pick up bucket/rod, place bucket, grab rope,
  climb. **This is the same verb M2-37/38 consume** — build it first among the three.
- No screen-space prompts, labels, or icons by default. No per-frame allocation in the detector.

#### The gameplay half — **LANDED** (the seam; the shader is still art-pipeline's)

**The pressure that finally forced it:** the dev key ledger is spent. Every letter A–Z is claimed,
across four different binding styles (`Key.X` enums, `.xKey` named properties, `.inputactions`
bindings, and numeric `Key` overrides serialized into `StPeters.unity`), so a feature that wants a
button has nowhere left to go. The verb is the pressure valve: **you register a candidate, you do
not bind a key.**

| Piece | Where | What it is |
|---|---|---|
| `IInteractable` | `Core/Interaction` | Id · live world position · own reach · priority · contexts · requires-facing · own availability gate · `Interact()` |
| `Interactables` | `Core/Interaction` | Scene-scoped registry, register-on-enable / relinquish-on-disable — the `MooringCleats` / `StandableSurfaces` mould |
| `InteractResolver` | `Core/Interaction` | The **pure** selection rule: filters (context → availability → reach → arc), then priority → distance → id ordinal |
| `InteractVerb` | `Core/Interaction` | Dispatch on a press edge; publishes `InteractPerformed`, and `InteractCandidateChanged` **on change only** |
| `InteractActionClaim` | `Core/Interaction` | Transitional: an older direct reader of the key (`WorldInteractor`) stands the verb down by proximity. Dies when NPCs become candidates |
| driver | `ControlSwitcher` | Reads no new key: `BeginInteract()` consults the registry **after** board / helm / step-ashore |

- **No new binding.** The verb generalises the key the game already calls Interact (E). Nothing E
  did before it changed; the verb takes only the presses that used to do nothing.
- **The arc is the affordance, not a label.** `InteractCandidateChanged` carries the id of the one
  thing the press would act on — that is the signal `outline-interaction-language.md` §4.3 asks for
  ("if the interaction layer would let the player act on it right now, it outlines"). Nothing
  screen-space was added.
- **Migrated as proof:** `WetBucketPoint` (the seawater spot). Same 4 m reach, same on-foot gate,
  still omnidirectional — its `F` key and its private `Update()` are gone.
- **Open ordering question, flagged for lead-architect:** the verb is consulted LAST, so boarding
  keeps the press where both would apply. That is provably non-regressive but it is not obviously
  right for a thing at your feet; the end state is boarding registering as a candidate too, and the
  resolver arbitrating it by distance, priority and **facing**.
- **Not built here:** the outline shader itself (art-pipeline), the fuel-container carry, shop
  counters, and the migration of boarding / NPCs / `MooringController`'s proximity read onto the seam.

## 4. Phasing

| When | What |
|---|---|
| ~~**Now (rides ADR 0022)**~~ **LANDED** | Add `DECK`/`WASHBOARD`/`CLEATS` to the art-director export ask; extractor pass-through to Def data. Additive, small. — **done**: the eleven sidecars import to `BoatDeckDef` assets (`DeckSidecarImporter`) and the on-deck player is clamped to each hull's own polygons instead of the one-size rectangle. `CLEATS` and `WASHBOARD` ride along as data; no rope gameplay and no Space climb yet. |
| ~~**The boarding MOVE**~~ **LANDED** (owner ask, 2026-08-06) | *"You push E and get teleported; the character should jump/climb/whatever to get onto the boat."* E now plays a MOVE: the fisher walks to the nearest point on that hull's own rail — her `DECK` outline, with `WASHBOARD` strips opened to the clamp on the hulls whose data carries them — vaults it, and lands where boarding always seated them. Stepping ashore is the same move mirrored. `ControlSwitcher` only; the state machine, the E-verb, the reach and the repair gate are all untouched — the move changes WHEN the same transition lands (at the far end of the arc, when the feet meet the deck), never WHETHER it may. **No new art**: the arc is built from the shipped walk frames, which the character's sprite driver selects from measured speed. A bespoke `board`/climb clip from the art-director is the open follow-up, and the owner's call. |
| ~~**The interact VERB**~~ **LANDED** (M2-39 gameplay half) | The `IInteractable` seam, its registry, the pure resolver and the press dispatch — see §3 above. **No new key**: the verb generalises E and is consulted after board / helm / step-ashore, so with an empty registry the interact key behaves bit-for-bit as it did. `WetBucketPoint` migrated onto it as proof and gave up its `F`. The **outline shader** (the other half of M2-39) is art-pipeline's and rides `InteractCandidateChanged`; nothing draws a highlight yet. |
| **M2, in order** | M2-39 (the interact verb — the other two consume it) → M2-37 (boarding) → M2-38 (ropes). Alongside M2-33, which shares the leave-the-helm/moving-deck substrate. **M2-37's own `Space` deck↔washboard climb is still to build** — the boarding move above consumes the same polygons but is a different verb (E, boat↔shore) and does not promote you onto a washboard to stand there. — **M2-38 done** (2026-08-06): it did not in fact need M2-39 first, because the rope rides the CAST flick rather than the interact verb. **M2-39's seam is now in** (2026-08-12): the "grab a rope" beat can become an `IInteractable` candidate whenever `MooringController`'s proximity read is retired onto it — not done here, deliberately, because that read is load-bearing for the cast-flick arbitration. |
| **Owner's call** | ~~M2-39 is a strong candidate to pull forward earlier~~ — pulled forward and landed (seam only). What is still the owner's to call: whether a thing **at your feet** should outrank **boarding** for the same press (today boarding wins), and what the outline actually looks like. |

## 5. What these symbols do NOT do — occlusion (owner follow-up, 2026-07-21)

**Q: will `DECK`/`WASHBOARD`/`CLEATS` be enough for the character to be hidden behind portions of the
boat that should block the sprite from the camera?**

**No — and nothing extra is needed from the art director either.** Those three symbols are gameplay
geometry (where you can stand, where ropes tie); they carry no depth. Occlusion comes from the mesh
itself: ADR 0022 hulls render with a real z-buffer under the projection trick, so a character sprite
drawn as a **depth-tested, alpha-tested billboard** at its deck position is hidden per-pixel by any
hull part nearer the camera (wheelhouse, rail, gunwale) — free, with no masks and no sorting hacks.
This is one of the quiet wins of meshes over baked sprites.

Requirements this places on the pipeline (fed to the phase 3 agent 2026-07-21):
- The hull pass's **depth buffer must stay available** for later depth-tested sprite passes
  (injection-point choice in the URP render feature must not discard it).
- The reversed-Z conventions (ZTest GEqual, depth clear 0) apply to the future character pass too.

Open edges, for when M2-37 is pulled:
- **Hulls that stay sprites** (dory, punt, skiffs) have no z-buffer. They are small open boats where
  full-body occlusion barely arises; if it ever matters, the rig baker can additionally bake a
  **per-facing depth map** (it runs the real geometry), enabling the same per-pixel test for sprite
  hulls. Option, not scheduled.
- **Full occlusion can hide the player entirely** (inside a wheelhouse). If that ever feels bad, the
  standard cozy fix is a subtle stencil **silhouette** through the hull — cheap to add, owner's call
  on whether hidden-is-hidden or faint-outline.
