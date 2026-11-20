# Autopilot slice 2: buying the hold, leaving the wheel, and rough weather

Design proposal, 2026-11-16. No implementation or visual change accompanies this document.
The binding purchase rule is: **available on anything without an outboard, as an expensive
upgrade sold by Nine Mile Creek's new marine-electronics / boat-gear wholesaler.** Slice 1's
authored eligibility remains the starting point, including its rudder requirement; this
proposal does not reclassify hulls by their names or propulsion enum.

The player buys a heading hold, sets it at the wheel, and can work on the same boat's deck
while it steers. It holds the bow's heading, not a track over the ground, a fishing position,
or a safe route. Tide and wind still carry the boat sideways. It moves no catch or goods:
loading, carrying, hauling and unloading remain work done by hand. This serves P1, P4 and
P5 in the [canon](../vision-and-pillars.md#3-the-five-pillars); remote fleet work remains in
the later [roadmap](../roadmap.md#1-the-milestones-at-a-glance).

## 1. What was read, and what exists

Main was inspected at `32dfdecdf3c36167598b97401bc8cfda336583b9`. The source links below to
the unchanged main files use that baseline's line numbers. Draft dependencies were read
without checking them out or changing them:

- [Slice 1, PR 1270](https://github.com/apholmes7-bot/hiddenharbours/pull/1270), head
  `f83bed33a26e03675c956df4ba3ce8eb12f5edd2`: a manned heading hold, settings, eligibility,
  input and a Core transition event. Its diff, not only its description, was inspected.
- [Save v24, PR 1277](https://github.com/apholmes7-bot/hiddenharbours/pull/1277), head
  `9a8ccd0c7218a3327b54449f61372c4803666be6`: Loran waypoint rows, with **no autopilot
  field**. At inspection its body described buildings, unlike its title and changed files.
  The save source and diff establish the contract here; that unrelated body is not evidence
  that a wholesaler or an autopilot purchase exists.

| Area | Source and present behavior |
| --- | --- |
| Hold law and gate | PR 1270 [HeadingHoldAutopilot.cs:85–159][hold-gate] requires fitted, manned, afloat and forward way; [167–182][hold-law] integrates wrapped error, adds yaw damping, clamps and slews the helm. |
| Controller lifetime | PR 1270 [BoatController.cs:185–213][controller-state] owns the transient hold. [396–470][controller-gate] treats offered as fitted for now. `Stop`, hull change, disable and the unmanned tick all hand it back in that diff. |
| Wheel departure | Main [ControlSwitcher.cs:1324–1365][wheel] enables controller/input on taking the helm, then disables both and calls `Stop(false)` on leaving. Main [BoatController.cs:532–548][stop] clears throttle, steer, linear velocity and angular velocity. |
| Deck simulation | [ControlSwitcher.cs:3238–3249][deck-tick] calls `TickUnmannedDrift` on deck. [BoatController.cs:620–691][drift] runs the same force pass through the disabled component, with no duplicate manned tick; sail drive remains live. |
| Travel | [RegionTravelCoordinator.cs:331–335][seam-branch] returns through the world-seam path before the named-arrival [375–380][arrival-stop] `Stop()`. [788–818][seam-arrival] preserves sea-seam heading and speed. The claim that *every* crossing stops is therefore too broad on this main. Both paths need explicit treatment. |
| Buying | [InstrumentOffer.cs:23–44][offer] carries id, price and catalog tag; [InstrumentShop.cs:85–133][shop] checks target, ownership and wallet, adds ownership, saves and raises `InstrumentPurchased`. |
| Ownership and fit | [InstrumentLocker.cs:28–70][locker] reads/writes `(HullId, InstrumentId)`. [SaveData.cs:122–130][instrument-save] and [648–662][instrument-row] persist those rows. [HelmControlRelay.cs:316–330][fit-reader] reads them into [BoatEquipment.cs:51–103][fit] to resolve supported instruments. |
| Rudder and waves | [BoatController.cs:427–434][rudder], [796–805][engine], [864–874][sail] and [989–1068][sea-pass]; [SeakeepingForcesMath.cs:310–360][wave-yaw]. These are the actual force paths, distinct from visual roll. |
| Fuel | [BoatFuelTank.cs:430–440][fuel-tick] burns only when [548–553][fuel-authority] sees an enabled engine controller. The [engine thrust path][engine] does not itself check tank contents. Deck power and dry propulsion need an explicit contract before unattended hold ships. |

### Loran is the purchase precedent, with unfinished delivery

Main's [LoranOffer.asset:15–21][loran-offer] has id `instrument.loran` and price ₲380,
but no catalog listing. [PR 1269](https://github.com/apholmes7-bot/hiddenharbours/pull/1269),
head `92a982eb7c34b1122c1ab24e6b2442743bf55666`, adds a temporary general-store listing
on the Gear shelf and counter wiring. Its [offer diff][loran-listing] is additional context,
not a merged purchase on this baseline. The future wholesaler is not that temporary counter.

The real purchase path is `CatalogSource.For<InstrumentOffer>(sellerId)` → `BuyCatalog`
instrument row → `InstrumentShop.TryBuy(offer)` → wallet debit → `InstrumentLocker.Add`
→ save → Core `InstrumentPurchased`. See [CatalogSource.cs:70–92][catalog-source] and
[BuyCatalog.cs:244–256][catalog-row]. The Loran then fits only where `SupportsLoran` and
ownership/default fit allow it ([BoatEquipment.cs:60,89–91][fit]). Its read model also
requires `fitted` ([LoranReceiver.cs:98–104][loran-read]). Buying a receiver does not yet
supply its view, host, world-frame mapping or persisted-number wiring; those are still
listed in [loran-navigation.md:100–114][loran-owed].

## 2. Purchase and the smallest honest ownership fix

Proposed implementation contract, not additions made here:

1. Author one `InstrumentOffer`, `AutopilotOffer.asset`, under
   `Assets/_Project/Data/Resources/Catalog/Instruments/`. Stable id `instrument.autopilot`,
   display name **Heading-hold Autopilot**. Put price on that asset; propose **₲3,800**
   for owner review (ten times the present receiver price), with no free factory fit.
   This is a balance proposal, not a price already approved or implemented.
2. Set `Catalog.Listed`, `Catalog.Section = CatalogSection.Gear`, `Catalog.Sellers` to
   the new marine-electronics wholesaler's authored seller id, and a deterministic
   `SortOrder`. Those are the existing [catalog fields][catalog-fields]. The world-content
   change must provide that seller's counter, wallet and `InstrumentShop`. Until it lands,
   leave the offer unlisted; do not sell it through a nonexistent counter or silently use
   the boatyard / fishing-tackle wholesaler. Register the new identity through an addition
   fixture, never by appending to the frozen identity ledger.
3. Quote the named target boat before payment. `ActiveHullId` means the active saved hull,
   not proof that the player is physically aboard at the counter. Require an owned,
   registered active hull and a real eligible helm. Sell a fitting service on a boat present
   at the port's installation berth; the price includes fitting, not hardware teleported to
   an absent boat. Any later loose-device delivery must use the normal hand-carry/storage
   rules. Use a Core capability query supplied by
   Boats so Economy and the catalog do not reference `BoatHullDef`. Check the same eligibility
   at quote **and confirm**, before spending; if the target changed, refresh the quote.
   Main's generic shop presently permits buying an instrument a console cannot use; do not
   copy that expensive mistake for autopilot.
4. Preserve the existing atomic wallet-spend, idempotent add, save and purchase-event seam.
   Exact-price purchase succeeds; one coin short, missing target, incompatible target and
   already-owned purchase debit zero. A successful purchase writes exactly one row and one
   purchase event. It fits the selected boat; no portable equipment item or loose device
   moves between boats for free.
5. At engagement and on the simulation step, resolve **fitted = Def.AutopilotOffered AND
   a usable rudder/helm AND `InstrumentLocker.Owns(save, hullId, "instrument.autopilot")`**.
   A missing save or unknown id is false. The flag alone must no longer arm a hold. The
   purchase must not be bypassed by a console default or the development instrument display
   switch. Heading hold needs neither a Loran nor GPS; later route mode needs a position fix.

### Two boats of the same hull

`HullId` is the stable **type id**. With two Cape Islanders, both calls to `Owns` would ask
the same question; one purchase would fit both, and a second purchase would be refused.
Renaming a variable to `boatId` or passing an instance suffix into the existing locker only
at the shop does not repair all the readers.

Today [SaveService.cs:292–299][fleet-save] deduplicates owned hull ids and
[OwnedFleet.cs:125–138][fleet] restores one active hull. **Smallest honest slice: keep that
one-owned-boat-per-hull boundary**, name the target clearly, and use the existing instrument
row. Purchase needs **no schema change**, including no autopilot field in v24. Do not claim
that this supports two separately fitted boats of the same type.

Before duplicate hull ownership ships, make a separate, versioned save change: explicit
owned-boat records and an active owned-boat id, plus instrument rows keyed by that id.
Reuse [OwnedBoatId.FirstOf/Mint][owned-id]: the first boat retains its hull id, subsequent
boats receive `#2`, `#3`, and so on. Migrate each old instrument row to the first boat only;
a newly bought second boat starts unfitted. Change quote, shop, effective fit, runtime
hold identity, sale cleanup and relevant per-boat preferences together. Resolve old data
once during migration; do not read a hull-wide fallback that would refit every duplicate.
The identifier helper exists, but is not an implemented duplicate-boat fleet. Selling and
rebuying must have an explicit instrument cleanup rule before a sale flow ships.

## 3. A hold that outlives the helm

### State and one source of drive commands

Put one transient hold record on the **persistent owned boat**, outside the enabled lifetime
of `BoatController`, `DevBoatInput` and the helm UI. Boats owns its controller logic; Core
exposes commands and a read-only snapshot so Player, travel, UI and audio need no concrete
cross-module dependency. The record contains boat identity, mode, target heading, integral,
last commanded helm, latched throttle, reason, and any bounded hazard timers. None is saved.
A load starts Off; there is no offline sailing or elapsed-time catch-up.

Proposed modes: **Off → Holding → SuspendedForCrossing → Holding**, or a hand-back to Off.
An engagement uses slice 1's P / gamepad Y switch and can be requested only at the manned helm.
Keeping the hold requires the
skipper to remain aboard that boat, including walking/fishing on its deck. Replace the
slice-1 `Manned` retention rule with an explicit control-authority rule; do not pretend a
disabled component is manned.

- With a valid hold, wheel departure disables manual input and changes player mode, but
  does not call the general `Stop`, erase motion or clear the record. Without a hold,
  departure retains its present stop-and-drift behavior.
- Run the hold immediately before propulsion in the existing common force pass. Manned
  `FixedUpdate` or the on-deck caller runs it exactly once, never both. Preserve the disabled
  controller's current meaning for character/presentation ownership. Update the unmanned
  hold hand-back and `OnDisable` paths deliberately; do not globally remove safety from
  every disable or `Stop` call.
- Engine mode keeps the throttle the skipper selected; the hold cannot add speed. Fuel
  metering must read the same active drive authority while the controller is disabled on
  deck. Apply a dry-tank thrust gate as well as a disengage rule; an event alone would leave
  main's unconditional engine thrust running. Charge one burn per fixed step, using the
  existing formula and time basis, with defined order when the last fuel is consumed.
- Sail mode holds rudder, not wind, throttle or a new automatic tacking system. Preserve the
  skipper's sheet/auto-trim selection. The current `unmanned` round-up torque must distinguish
  a genuinely unattended rudder from one being driven by the hold; otherwise it fights the
  commanded course. On hand-back, restore the normal unattended sail behavior.
- Taking the wheel alone does not cancel it. A real steer input, wheel grab or second switch
  press does. Neutral/reverse and loss of forward way retain the existing no-way hand-back;
  an explicit emergency stop acts immediately. Ashore, anchored/moored, hull swap, boat
  disposal, invalid identity or loss of a runnable simulation ends the hold. No remote boat
  is driven after the player leaves it.

### Crossing is a handoff, not saving

Use a transient handoff tied to boat identity and one crossing token. Suspend drive while
region roots, terrain, environment and colliders are rebound. Do not count missing destination
sensors or a coordinator's temporary reset as physical low way. No synthetic ticks, blind
thrust, elapsed-load-time integration, or automatic re-arm after a failed handoff.

**Continuous sea seam:** retain heading target, command state, throttle and physical motion;
apply the world-frame translation once. Main already preserves heading/speed and treats both
helm and deck as a boat mover ([RegionTravelCoordinator.cs:556–574][mover]). Validate destination
depth, hazards, fuel, aboard identity and forward way before the next active force step. Resume
the same hold only if they pass. Integrator and last helm carry because there was no new physical
course; loading must not duplicate a simulation tick or generate off/on event chatter.

**Named arrival / teleport / berth:** keep the general `Stop()`. Preserve only a suspended target
and the reason **Arrival needs helm** in the boat's runtime record; lamp is not steady-on and
no propulsion resumes at the berth. The skipper must regain way and acknowledge resume at the
helm, with integral cleared and helm slewed from its current value. Generic teleportation may
change orientation and clearance; replaying incoming velocity or cached throttle there is
unsafe. Thus automatic continuity is proposed for real sea seams; a named teleport preserves
intent but requires manual resumption. This distinction needs the owner's choice below.

If travel fails or destination geometry is unknown, cancel with a reason and cut engine demand.
Do not fall back to the named-arrival stop and then silently accelerate on stale controls.
This is all runtime continuity: **no save field is required**.

### Safety hand-backs and their physical meaning

The following are proposed interlocks, not sensors already implemented by slice 1. Put limits,
sampling intervals, margins and dwell times in `GameConfig` / appropriate Def data. A shared
Core hazard sample carries validity and reason, not a hidden obstacle-avoidance route.

| Trigger | Proposed behavior |
| --- | --- |
| Shoal or grounding | Refuse/end on `IsAground`, unsafe under-keel clearance, or invalid depth ahead. Probe the swept hull and stopping envelope using present tide, draught, speed and hull size, not a point under the bow. Start with a proposed 0.5 m clearance margin, tuned per hull; no terrain means unknown, not guaranteed deep water. This safety sample does not grant an unowned depth readout. |
| Impact | Retain slice 1's configured hazardous-impact threshold (currently 0.5 m/s relative speed); one real knock ends the hold. Smaller fender contact alone does not. Capture the collision even when the manual controller is disabled. |
| Fuel / way | Empty powered tank ends engine hold immediately and cannot generate thrust. Low fuel warns; it does not invent a return journey. Forward through-water speed below configured `MinWayMetresPerSecond` ends hold, including astern. A fast current over the ground cannot supply false rudder authority. Sail's no-way rule applies without requiring an engine tank. |
| Weather / inability to steer | Proposed operating ceiling is the lower of the hull's `MaxSafeSeaState` and Rough. Above it, refuse/end. Within it, warn at persistent error/saturation; propose hand-back at error over 15° for 5 simulated seconds, with a 30° immediate ceiling. These are starting acceptance limits, not new seaworthiness or capsize rules. |
| Another vessel / fixed obstacle | A local swept-collider and closest-approach safety query trips before the combined hull envelopes plus stopping margin meet. Include relative motion, not just range. It cuts drive; it never chooses a passing side. Missing required collision coverage refuses hold. It does not reveal distant boats through fog or grant radar tracks. |
| Map edge | An open, valid neighbour goes through the handoff. A closed/unloaded/unsupported edge trips before the stopping envelope reaches it. Main's [edge clamp][edge] removes outward velocity but cannot be used as a wall to push against indefinitely. |
| Manual / mooring / leaving boat | Steer override and second press hand back at once. Deploying anchor/rope, going ashore, changing hull, losing ownership or stopping the game cancels drive authority. No automatic re-engagement when a hazard clears. |

On a hazard while on deck, cut engine demand and centre the unattended helm, sounding the
reason once. Let the hull coast, drift, ground or collide under existing physics: **do not
zero its velocity to simulate emergency brakes**. At a manned helm return control immediately,
with hazard drive cancellation applied before fresh manual commands. A sail still draws wind
after disengagement; the alarm asks the skipper to ease sheets and recover. Test this physical
limit rather than advertising a guaranteed collision stop. Safety look-ahead must account for
the stopping distance; it cannot promise safety in an already unavoidable encounter.

### What the skipper sees and hears

Propose a physical fitted dash lamp: off when inactive, steady while holding, a distinct pulse
while suspended/warning, off plus one hand-back chime on cancellation. Add an engage click and
one readable reason at the instrument; do not rely on color alone or an always-on screen HUD.
Unfitted boats show no functioning switch/lamp. Refused presses give one local explanation,
not a fake engagement transition. On deck the hand-back chime remains audible from the boat.

Use PR 1270's Core [AutopilotStateChanged { Boat, Engaged, Reason }][signals] for true on/off
transitions, exactly once. Preserve existing append-only reason values and append needed reasons.
Suspension/warning must be explicit in a Core snapshot/status seam because the current boolean
event cannot express them. Keep its `Boat` association stable across bindings; do not republish
as if a new helm widget were a new boat. A late subscriber reads the snapshot, then listens;
it cannot reconstruct a lit lamp from an event it missed. UI/audio consume Core, not controller
internals. Lamp, rig and sound delivery belong to later visual/audio work; no art acceptance,
rebake or plate rebaseline is implied by this document.

**Cozy with teeth:** warn before avoidable trouble, explain the hand-back, let a player recover,
but keep the real drift and consequences. A skipper fishing behind the wheel can still strand
the boat by ignoring a chime, falling tide, fuel or a worsening sea. No instant rescue, free fuel,
automatic unloading, invulnerable collision avoidance or permanent boat loss is added. Existing
[grounding and rescue design][teeth] remains the consequence contract.

## 4. Rough weather: why the light-sea result is insufficient

PR 1270 reports a Cape Islander run in Light seas at roughly 2.0–2.5 m/s: steady maximum error
0.40°, RMS 0.15°, and a later yaw kick recovering to maximum 0.69°. Its body also reports very
small routine helm demand. Those are **reported slice-1 measurements**, not rerun evidence here,
and do not establish performance in beam seas, near minimum way, or on other hulls.

- **Gain versus rudder authority:** [RudderTorque][rudder] scales with absolute way and saturates
  at 2 m/s for engine hulls. At the 0.5 m/s minimum, it has only one quarter of full authority.
  Sail uses its hull's full-authority threshold. The same PID gains command very different yaw
  acceleration across light jets, planing boats, working boats and a tanker. Do not raise the
  minimum way or weaken a fleet rule just to hide an unstable hull.
- **Integral wind-up:** [the step][hold-law] clamps integral to ±0.3 helm by default, but continues
  accumulating while output is clamped at ±0.6 or slew-limited (the current [settings][hold-settings]).
  This bounds wind-up, not full
  saturation-aware anti-wind-up. A sustained beam sea can fill it; when way returns or the wave
  turns, the stored command can overshoot. Measure first; if needed, condition integration on
  available authority/saturation or back-calculate it. Retain the gain and limits as tunables.
- **Yaw from waves:** [Resolve][wave-yaw] applies beam-slope yaw and adds a following-sea asymmetry.
  [The controller][sea-pass] also applies displaced-sea scaling, breaking-surf shove/yaw and
  hull-specific damping. Holding heading cannot cancel lateral set. A smooth-looking visual
  roll is not proof that the rigidbody yaw disturbance was large enough to exercise the hold.
- **Low speed and sailing:** head seas can repeatedly take way below the gate; high speed can
  make the same gains overcorrect. A sloop sailing into its no-go angle loses power regardless
  of heading error. The unmanned round-up/irons behavior is another torque, not PID noise.
  Keep zero/astern refusal and define genuine weather hand-back instead of driving harder.

### Editor acceptance plan for the later implementation

No Unity lease is granted to this design work. **No editor, EditMode or PlayMode tests were run
for this document.** The following are proposed gates for a later implementation, with any
numerical balance changes recorded as deliberate rule changes. Keep the current tests that
pin steer priority, wrap, clamp, slew, collision and no-way. Migrate the specific `LeftHelm`
expectation to “held while skipper remains aboard; cancelled ashore,” retaining exact asserts.

Use a granted editor slot in an isolated test scene with the real boat controller, configured
terrain, fuel, wave and travel services. Do not edit a shipped scene, water, hull art or import
settings. Fix and record world seed, initial game time, fixed step, hull/load, heading, current,
wind, sea enum **and** continuous `SeaState01`, exposure, displacement state and all controller
settings. Log heading error, yaw rate, through-water way, helm, integral, saturation duration,
fuel and transition reason per step; keep numeric summaries and editor evidence. Confirm nonzero
wave force/yaw in exposed sea cases, and sampled class/test counts; zero tests is not a pass.

| Coverage | Runnable sequence |
| --- | --- |
| Hull screen | Cape Islander, lobster boat, Side Dragger, tanker, go-fast (sterndrive), jet ski, Sloop 30 and Sloop 88. Run each at sustained 2 m/s in Light beam seas, then its highest permitted sea at achievable cruise. For sail, choose a viable point of sail and report actual way, not a fictitious throttle speed. Rowed/outboard dories and an outboard skiff are purchase/engage negative controls. |
| All seas | Glass, Calm, Light, Moderate, Lively, Rough, Gale and Storm. Screen each sea on the Cape and tanker at 2 m/s; over the proposed ceiling assert refusal/hand-back rather than inventing successful storm operation. Include a real time-varying crossing of the weather ceiling. |
| Speed boundaries | On Cape, jet ski and Sloop 30: rest, astern, 0.49, exactly 0.50, 0.60, 1.0 and 2.0 m/s through water, plus each hull's sustainable cruise. Test eligibility at the boundary with controlled sensor fixtures; live waves may legitimately drop the exact-minimum run below it. Repeat with opposing current giving high ground speed but low way. |
| Sea direction / load | On Cape, tanker and Sloop 30, at 0.6 and 2 m/s where sustainable: head, beam port/starboard, quartering and following seas, empty and 80% of rated hold. Use Moderate then Rough if permitted. This targeted set covers interacting risks without requiring every axis's full Cartesian product. |
| Recovery | At 2 m/s apply ±15° heading perturbations and a 20°/s yaw kick separately. Then a 30 s saturating yaw disturbance, remove it and observe unwinding. Cross 359°/0° and test port/starboard symmetry. Repeat exact sensor traces for deterministic controller output; physics replay on the same editor uses declared tolerances, not a claim of cross-machine bit-identical Unity physics. |
| Continuity / hazards | Leave/take helm while fishing, toggle/steer/wheel-grab, neutral/reverse, dry the tank on deck, soft/hard impact, shoal and unknown depth, crossing vessel, anchor/ashore, closed edge, successful/failed seam and named berth arrival. Check one force pass and fuel burn per tick, no event chatter, no stale target on hull swap, and load always Off. |

Durations here use accumulated fixed-step simulation seconds, not wall-clock time or the
accelerated calendar clock. For each maintained-heading case, settle for 30 s then measure a
120 s window. Proposed pass marks:

| Condition | RMS / maximum absolute error | Further gate |
| --- | --- | --- |
| Glass / Calm / Light, way at least 2 m/s | ≤1° / ≤2° | Preserve slice 1's 2° calm hold rule; no growing oscillation. |
| Moderate / Lively, way at least 1 m/s | ≤2° / ≤5° | Saturation below 20% of the window; no sustained weave. |
| Rough, within hull/hold ceiling, way at least 2 m/s | ≤4° / ≤10° | Saturation below 40%; otherwise reasoned inability-to-hold cancellation. Cancellation is safe behavior, not a successful Rough hold result. |
| ±15° perturbation or 20°/s kick, Light, 2 m/s | Back within ±2° in 30 s | No later growing peaks. After saturation release, integral unwinds and error returns to the permitted sea band in 30 s. |
| Boundary / hazard | Off and one exact reason | Immediate hazards by next fixed force step; predictive hazards before the swept stopping envelope is violated; weather dwell exactly as configured. No thrust on a dry powered tank. |

At 0.6 m/s require finite, clamped/slew-limited output and correct hand-back; do not promise the
2 m/s accuracy band. Measure sustained weaving as repeated alternating error peaks outside the
sea's tolerance with non-decreasing amplitude over three complete cycles. A failed hold case
is recorded as failed even if its eventual cancellation was safe. Performance: the hold and
snapshot introduce zero managed allocations per steady fixed/UI step; record profiler cost and
ensure hazards are sampled at their configured cadence. Capture the dash/deck hand-back once
for a later visual/audio review; this document approves no new look.

## 5. Loran waypoints: outline of a later slice

PR 1277 adds [SaveData.HullLoranWaypoints:463][v24-list] and [LoranWaypointDto:74–99][v24-row]:
`HullId`, `Number`, `ChainId`, `SecondaryA`, `SecondaryB`, `TdA`, `TdB`, with conversion helpers.
Its migration initializes old saves empty and repairs invalid rows. These are remembered
numbers, **not route order, a selected destination, a route cursor, or an engaged hold**.

First wire the instrument host and a Core locker to capture/restore `LoranWaypointStore`,
preserving exact slot numbers and TD values. `TryEnter` chooses the lowest free slot; replaying
rows through it would renumber gaps, so add an explicit validated import/export contract.
Enforce `(boat key, Number)` uniqueness, finite values, capacity and valid chain/secondary ids;
preserve valid slots deterministically, report rejected data, and never replace full memory
silently. [The store:64–106][waypoint-store] shows why a normal add loop is insufficient.

A first, explicitly selected single-waypoint mode can consume these rows without a new save
field: selection is transient and loading stays Off. Require autopilot and Loran ownership,
usable fit, **`Signal == Fix`**, and a valid resolved target. `HasNumbers` includes Blink and a
blink reading can still carry a fix, so do not use either boolean as the safety gate. Main's
[TryResolve / Steer:158–177][loran-steer] supplies the target, bearing and distance; reject
Blink/NoSignal on entry and hand back on signal loss. Map local boat coordinates to the
world frame before reading/resolving and back only when required. Loran bearing is clockwise
from north ([NavMath:82–85][bearing]); slice 1 uses counter-clockwise rigidbody degrees.
Convert once at the boundary and test all cardinal headings and the wrap.

Later route following needs an explicitly ordered list of waypoint references, leg/cursor,
cross-track and turn limits, arrival radius/hysteresis, speed/stopping clearance, world-frame
seam continuity and an error-aware corridor. A noisy Loran fix must not hunt the wheel or
declare arrival repeatedly. TD repeatability does not guarantee obstacle clearance; no hidden
perfect world position should correct it. No automatic tacking, avoidance planner, cargo work
or offscreen fleet orders are included. **Saving an ordered route/cursor requires a later
authorized versioned save step**; v24's slot numbers must never be reinterpreted as route order.
Even then, retain Off on reload unless automatic resume is separately approved and proved.

## 6. Delivery order and save boundary

| Order | Small implementation slice | Save impact / exit proof |
| --- | --- | --- |
| 2A | Paid instrument and offered-AND-owned helm gate; wholesaler counter supplied by its owning work | Existing `HullInstruments`, no version bump. Quote/confirm eligibility, no-charge refusals, exact row/event count, reload ownership and all eligible/excluded hulls. If the counter is absent, keep listing unpublished. |
| 2B | Persistent runtime boat hold, shared drive/fuel authority, deck operation, hazard hand-backs and Core status | No saved hold state. Prove manned/deck parity, dry fuel, aboard/ashore distinction, one tick/event, and reasoned physical coasting. Lamp/sound delivery follows its visual/audio review. |
| 2C | Validated continuous-seam handoff and explicit named-arrival suspension | Transient record only. Prove same target/speed at a sea seam; no thrust or velocity resurrection at a berth; failed handoff cancels. |
| 2D | Rough-weather validation and, only if measured necessary, saturation-aware control changes | No save field. Run the editor matrix and publish counts/metrics; never retune the sea, hull appearance or pixel expectations to pass. Gate broad release of deck hold on this proof. |
| Later L1 | Wire v24 Loran storage, host/frame mapping and explicit single-waypoint steering | Uses v24 rows; no additional field for transient selection. Prove slot/TD round-trip, signal loss and clearance/error behavior. |
| Later L2 | Ordered routes and independent duplicate-hull fitting, as separately scoped work | Each needs an approved versioned save migration: route/cursor data; owned-boat identity and instrument ownership. Neither is supplied by v24 or this design. |

[hold-gate]: https://github.com/apholmes7-bot/hiddenharbours/blob/f83bed33a26e03675c956df4ba3ce8eb12f5edd2/Assets/_Project/Code/Boats/HeadingHoldAutopilot.cs#L85-L159
[hold-law]: https://github.com/apholmes7-bot/hiddenharbours/blob/f83bed33a26e03675c956df4ba3ce8eb12f5edd2/Assets/_Project/Code/Boats/HeadingHoldAutopilot.cs#L167-L182
[hold-settings]: https://github.com/apholmes7-bot/hiddenharbours/blob/f83bed33a26e03675c956df4ba3ce8eb12f5edd2/Assets/_Project/Code/Core/Config/GameConfig.cs#L1787-L1838
[controller-state]: https://github.com/apholmes7-bot/hiddenharbours/blob/f83bed33a26e03675c956df4ba3ce8eb12f5edd2/Assets/_Project/Code/Boats/BoatController.cs#L185-L213
[controller-gate]: https://github.com/apholmes7-bot/hiddenharbours/blob/f83bed33a26e03675c956df4ba3ce8eb12f5edd2/Assets/_Project/Code/Boats/BoatController.cs#L396-L470
[signals]: https://github.com/apholmes7-bot/hiddenharbours/blob/f83bed33a26e03675c956df4ba3ce8eb12f5edd2/Assets/_Project/Code/Core/Events/AutopilotSignals.cs#L8-L47
[v24-list]: https://github.com/apholmes7-bot/hiddenharbours/blob/9a8ccd0c7218a3327b54449f61372c4803666be6/Assets/_Project/Code/Core/Save/SaveData.cs#L459-L463
[v24-row]: https://github.com/apholmes7-bot/hiddenharbours/blob/9a8ccd0c7218a3327b54449f61372c4803666be6/Assets/_Project/Code/Core/Save/SaveRowsV24.cs#L74-L99
[loran-listing]: https://github.com/apholmes7-bot/hiddenharbours/blob/92a982eb7c34b1122c1ab24e6b2442743bf55666/Assets/_Project/Data/Resources/Catalog/Instruments/LoranOffer.asset#L15-L27
[wheel]: ../../Assets/_Project/Code/Player/ControlSwitcher.cs#L1324-L1365
[stop]: ../../Assets/_Project/Code/Boats/BoatController.cs#L532-L548
[deck-tick]: ../../Assets/_Project/Code/Player/ControlSwitcher.cs#L3238-L3249
[drift]: ../../Assets/_Project/Code/Boats/BoatController.cs#L620-L691
[seam-branch]: ../../Assets/_Project/Code/App/RegionTravelCoordinator.cs#L331-L335
[arrival-stop]: ../../Assets/_Project/Code/App/RegionTravelCoordinator.cs#L375-L380
[seam-arrival]: ../../Assets/_Project/Code/App/RegionTravelCoordinator.cs#L788-L818
[offer]: ../../Assets/_Project/Code/Economy/InstrumentOffer.cs#L23-L44
[shop]: ../../Assets/_Project/Code/Economy/InstrumentShop.cs#L85-L133
[locker]: ../../Assets/_Project/Code/Core/Save/InstrumentLocker.cs#L28-L70
[instrument-save]: ../../Assets/_Project/Code/Core/Save/SaveData.cs#L122-L130
[instrument-row]: ../../Assets/_Project/Code/Core/Save/SaveData.cs#L648-L662
[fit-reader]: ../../Assets/_Project/Code/Boats/HelmControlRelay.cs#L316-L330
[fit]: ../../Assets/_Project/Code/Boats/BoatEquipment.cs#L51-L103
[rudder]: ../../Assets/_Project/Code/Boats/BoatController.cs#L427-L434
[engine]: ../../Assets/_Project/Code/Boats/BoatController.cs#L796-L805
[sail]: ../../Assets/_Project/Code/Boats/BoatController.cs#L864-L874
[sea-pass]: ../../Assets/_Project/Code/Boats/BoatController.cs#L989-L1068
[wave-yaw]: ../../Assets/_Project/Code/Boats/SeakeepingForcesMath.cs#L310-L360
[fuel-tick]: ../../Assets/_Project/Code/Boats/BoatFuelTank.cs#L430-L440
[fuel-authority]: ../../Assets/_Project/Code/Boats/BoatFuelTank.cs#L548-L553
[loran-offer]: ../../Assets/_Project/Data/Resources/Catalog/Instruments/LoranOffer.asset#L15-L21
[catalog-source]: ../../Assets/_Project/Code/Economy/CatalogSource.cs#L70-L92
[catalog-row]: ../../Assets/_Project/Code/Economy/BuyCatalog.cs#L244-L256
[catalog-fields]: ../../Assets/_Project/Code/Economy/CatalogListing.cs#L102-L119
[loran-read]: ../../Assets/_Project/Code/Core/Nav/LoranReceiver.cs#L98-L104
[loran-owed]: loran-navigation.md#L100-L113
[fleet-save]: ../../Assets/_Project/Code/Core/Save/SaveService.cs#L292-L299
[fleet]: ../../Assets/_Project/Code/Boats/OwnedFleet.cs#L125-L138
[owned-id]: ../../Assets/_Project/Code/Core/Boats/OwnedBoatId.cs#L37-L59
[mover]: ../../Assets/_Project/Code/App/RegionTravelCoordinator.cs#L556-L574
[edge]: ../../Assets/_Project/Code/App/RegionTravelCoordinator.cs#L766-L785
[teeth]: boats-and-navigation.md#L410-L498
[waypoint-store]: ../../Assets/_Project/Code/Core/Nav/LoranWaypointStore.cs#L64-L106
[loran-steer]: ../../Assets/_Project/Code/Core/Nav/LoranReceiver.cs#L158-L177
[bearing]: ../../Assets/_Project/Code/Core/Nav/NavData.cs#L82-L85

## 7. Owner decisions, with choices

These are review questions for later approval, not requests that block this document. Proposed
leans are marked; none silently changes the purchase ruling or the canon.

1. **How expensive is the upgrade?** A — flat ₲3,800 on the offer (**lean**, retune after earning-time playtest);
   B — flat ₲7,500; C — priced per hull class through authored offers. All choices keep it purchased.
2. **How independent may it be?** A — skipper may fish/walk aboard the same boat, but ashore/remote
   operation cancels (**lean**); B — retain manned-only hold until crewed automation is separately designed.
3. **What should named arrivals do?** A — continuous sea seams resume; named teleports/berths preserve
   a suspended target and require helm acknowledgement (**lean**); B — suspend for acknowledgement at
   every crossing. Automatic powered departure from a teleport berth would need another safety design.
4. **What weather should it promise?** A — at most Rough and no higher than the hull's safe sea,
   with measured inability-to-hold cancellation (**lean**); B — a lower per-hull operating ceiling
   until each hull passes; C — allow higher seas only after a separately reviewed editor proof.
5. **How should hazards intervene?** A — local depth/contact/stopping-envelope interlocks cut drive
   and chime, with no automatic avoidance (**lean**); B — warn only and require a permanently manned
   helm. Under A a sail continues drawing wind after hand-back; neither choice promises instant stopping.
6. **When should separately fitted duplicate hulls arrive?** A — keep one per hull now; migrate to
   owned-boat records before duplicates ship (**lean**); B — require that save migration before selling
   the autopilot. Neither choice treats one hull purchase as a fleet-wide licence.
7. **What navigation follows?** A — saved Loran numbers, then transient single-waypoint steering,
   then a separately saved ordered route (**lean**); B — keep heading hold only for this phase.
