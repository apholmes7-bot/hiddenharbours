# Bounded historical pot encounters

Status: algorithm design with an executable specification, **outside the Unity runtime**.
P1, P3 and P4. No catch, pot tuning, scene, save or presentation is changed. This proves a
restricted periodic-route algorithm; it does not claim that current schools supply physical
individual routes or that the existing pot resolver already implements it. See the
[capture phases](../living-fish-capture-phases.md),
[behaviour foundation](01-behaviour-model.md) and
[identity/migration proposal](https://github.com/apholmes7-bot/hiddenharbours/pull/1074).

## Chosen scope and facts required

Choose exact analytical entrances on a finite set of certified periodic, supported routes.
Use a persistent resumable merge across **all** competing pots, rather than evaluating each
pot independently at haul or sampling one position at each slow tick. An animal must actually
cross an entrance; no count roll fills the pot. A clam has no travelling entrance stream.

The initial proof cohort has immutable placement/removal times, capacity, route definitions,
individual ids and a complete history manifest. It permits initial external reservations but
no releases, recruitment, moving pots, mid-soak hauls or capacity changes. Those commands must
settle the shared frontier first and open a new cohort preserving existing ownership. They
cannot reset the solver at the new time and forget the previous soak. This restriction is
why full gameplay integration and its reviewed save migration remain prerequisites.

Required provider data:

- Full stable individual identity and a population/content revision; one size and species
  per individual, with all contenders including unloaded regions and externally reserved ids.
- Complete candidate coverage for every entrance over the cohort. Broad-phase culling must
  conservatively bound the whole route, not just the animal's current cell or rendered sprites.
  A cap is `Incomplete`, not a smaller complete population. Admission above the manifest budget
  is explicit `WorkLimit`, with no authoritative effects.
- Certified supported routes and entrance geometry in a common region frame. The certificate
  must bound all segments, not just endpoints, and the route must reach the pot continuously.
  Unknown support is an explicit failure, not an interpolated floating lobster.
- Event-time histories for wetness, support, entrance state, bait, licence/method eligibility
  and permitted stage. Classify intervals as known admissible, known inadmissible or unknown.
  An actual deterministic tide evaluator must prove interval boundaries or evaluate the exact
  event instant. Finite point samples cannot certify every tide in an interval.

The specification combines those last conditions into immutable half-open `Window` values.
They are **synthetic evidence inputs**, not a tide model or a claim that real ecology can be
read from scene colours. Unknown windows or gaps block relevant candidates. Known empty
certified manifests finish empty. No table fallback, implicit open window or hidden RNG exists.

## Exact entrance streams

For the proof, represent one supported line by distance `x` from home, length `L > 0`, speed
`v > 0`, home rest `H >= 0`, away rest `A >= 0`, and explicit cycle origin `phi`. A pot entrance
at `q` is strictly inside the line: `0 < q < L`. The individual walks out, rests, walks back,
rests and repeats. Its period is `P = H + 2L/v + A`. Entrance instants are:

`out(n) = phi + H + q/v + nP`

`in(n) = phi + H + L/v + A + (L-q)/v + nP`, for integer `n`.

For either base `b`, the next crossing at/after `t` is
`b + ceil((t-b)/P)*P`. This works at negative diagnostic times and very long game times.
It finds a crossing between slow ticks without advancing a creature through those ticks.
Entrance-endpoint contacts, finite door widths, curved routes and approach steering need
their own reviewed root/interval proof; the line specification does not approximate them.

The existing `BenthicMovement` uses straight supported segments with rests, but its hashed
phase and double arithmetic are **not imported** by this specification. A future C# adapter
must prove its phase convention, route/member identity and numerical ordering against shared
fixtures. The first school field's group centre is not an individual supported route.

The reference uses rational geometry and game times to avoid tolerances changing ties.
Inputs are integers or explicit `Fraction` values, never silently rounded floats. Core clock
seconds remain the compressed game-clock domain; there is no 3,600-second hour or fixed
astronomical day. A production representation needs a reviewed canonical arithmetic policy;
Python big-integer arithmetic is not a Unity performance or bit-identical floating-point proof.

## Shared merge, cursor and bounded work

Create at most two streams per eligible route/pot pair. Seek each directly to placement and
store its next entrance in a min-heap ordered by `(eventTime, stablePotId, fullOccupantId)`.
All comparisons use full identity, not a collision-prone short hash. Placement is inclusive,
removal exclusive. Settling `[start, until)` leaves an event exactly at `until` for the next
call, independent of tick boundaries.

At the minimum candidate:

1. Retire that stream if its animal is already owned/reserved, or its pot has reached capacity.
   These facts cannot reverse inside this immutable cohort.
2. Unknown admission stops **before popping the candidate**. Keep its exact cursor; do not
   let a later competing pot take the animal while this earlier event is unresolved.
3. Known inadmissible admission discards that crossing. If the same rejection is certified
   through interval end, seek directly to the next crossing at/after that end. This skips
   arbitrarily many cycles without dropping a potentially accepted entrance.
4. Known admissible admission atomically assigns that id to the pot and records its event
   receipt. All later streams see it as unavailable; no second catch token is created.

`advance(until, budget)` examines at most `budget` candidates. It returns `Complete`,
`Pending` or `Unknown` with the last fully committed frontier. A call ending partway through
a tie group does not publish partial catch contents. Targets are monotone; reducing the
target or supplying a zero work budget is invalid. Repeating a completed target adds nothing.
The reference exposes catch results only at a complete frontier.

Initialization admits explicit limits for manifest records/history windows and route/pot
pairs before sorting/building streams. Let `M <= 2 * pairLimit` and let `W` be the maximum
admitted windows per pot. Heap memory is `O(M)`; retained catch receipts are at most both
the number of individuals and total pot capacity. Each examined event costs
`O(log M + log W)` arithmetic operations. One advance costs at most that times its work budget.
Input validation/indexing is bounded by admitted manifest size; rational bit cost additionally
depends on input magnitude. These are operation/storage bounds, not measured frame budgets.

For wholly known interval histories, a stream visits each closed window at most once, then
is accepted, retired or passes the horizon. Total settlement work is bounded by manifest
streams and history transitions, rather than elapsed slow ticks. Arbitrary future per-crossing
probability/rejection functions invalidate that stronger bound: they need their own indexed
proof or must return `Pending` and retain work. Bait preferences remain soft preferences in
the game; this spec neither retunes them nor claims to implement their future approach model.

Persist the exact manifest revision, cohort start, target and committed frontier, each
stream's next event/direction, unavailable identities, counts and accepted receipts atomically
under the proposed ownership contract. Restoring just a time or just each pot's count is
insufficient. Checkpoint copying in this proof is structural/in-memory; no production save
format or crash durability is tested. Never discard an old journal because a region unloads.

## Why scheduling cannot change the answer

Each stream enumerates exactly its analytical crossings in increasing order. The heap merge
always chooses the least remaining global event. Window skipping removes only events with
the same certified rejection, while retirement removes only permanently unavailable work
inside this cohort. Consequently those optimizations preserve the next accepted event.

Induct on accepted events: identical immutable inputs and initial ownership give the same
minimum admissible event, same capacity decision and same ownership update. Applying the
inductive step yields the same ordered receipts regardless of caller tick partition, sleep,
buffer budget, input enumeration order or region residency. A resumed cursor is the same
merge suffix. Unknown input intentionally prevents an answer; it cannot prove an empty pot.

## Executable evidence and limits

Run the specification through the existing Python CI gate, without a graphics device:

```text
python -I -X utf8 -m unittest discover -s tools/ci -p test_pot_history_spec.py -v
```

`tools/ci/pot_history_spec.py` and its test file are nonshipping proof artifacts. The existing
CI discovery command already includes the new tests; no workflow edit is required. The suite
compares **1,000 seeds** of generated immutable fixtures against an independent exhaustive
point-sampling oracle on a half-second lattice on which every fixture crossing falls. It
compares exact receipt tuples for a long skip, one-second ticks, reversed input orders and
one-candidate resumptions with region data present but no scene. This oracle is exhaustive
for those fixture routes, not an argument for coarse sampling in production.

Other cases prove a between-tick crossing, inbound/rest timing, exact placement/removal/query
boundaries, competing animals/pots, external reservations, full/empty capacity, unknown gaps,
unsupported/incomplete/oversized manifests, negative/huge times and checkpoint cursor copying.
A `10^30`-second closed interval followed by opening settles with exactly **four** examined
candidates, including retirement of the losing stream; it does not iterate those cycles.
The suite asserts bounded calls cannot expose a partially resolved tie group.

Before implementation activation, require the reviewed Core ecology and ownership/migration
contracts, certified individual routes and complete event-time tide/eligibility histories,
shared-frontier command barriers, preserved pot/bait tuning, C# replay fixtures, actual
slow-tick CPU/GC measurements and manual supported haul/handling proof. Continuous curves,
reactions, migrations, release/recruitment, moving pots and partial histories remain outside
this proof. This work authorizes no runtime catch switch and no visual baseline change.
