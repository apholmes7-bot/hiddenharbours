# Rod, pot and clam dig: reading living animals

Status: proposed integration design only. P1, P3 and P4. No catch, goods, save, scene or rendering
behaviour changes here. Phase names describe dependency order inside the existing roadmap;
they do not authorize a new milestone or move nets, leases or staff into the present phase.
See [fishing methods](fishing-methods.md) and [roadmap](../roadmap.md).

## What the baseline actually does

Audited at `3e7188a426c00dde0b18f94ab9512ea1a2bc0c21`. Source paths below start at
`Assets/_Project/Code/`. The living-fish design (#1044), inactive SchoolField (#1041), ecology
references (#1053), benthic arithmetic (#1057), pot-clock correction (#1046), and catch-honesty
work (#1033) are drafts, not assumed merged dependencies. Their owned files are not edited.

| Method and source | Current answer | Gap before an actual-animal catch |
|---|---|---|
| Rod: `Fishing/FishingController.cs`, `OnBite`; `Fishing/SchoolInfluence.cs`, `At`; `Fishing/CatchResolver.cs` | School presence and held depth bias bite delay and species weights. At bite, the resolver still picks a species from the eligible table; body weight is rolled separately. Empty school influence is neutral, so it does not prove an empty habitat cannot yield a fish. | No individual token is selected or removed from the background field. Sonar agreement at school level is not a capture transaction. |
| Pot: `Fishing/PlacedTrap.cs`, `ResolveCatches`; `Fishing/TrapFill.cs`; `Fishing/PlacedTrapCatch.cs`, `ResolveMany` | Placement facts, soak/fill and stable indexed streams generate count, species and weights. Bait favours softly weight the pool. No query proves a lobster or crab physically approached an entrance during the soak. | Requires supported approach paths, historical encounters, shared reservations and an animal list belonging to the pot. |
| Clam: `Fishing/ClamDig.cs`, `TryDig`; `Fishing/ClamSpurtMath.cs` | A hole's spurt is seed/position/time-derived, but a successful dig creates a species CatchItem with a separate weight roll. The consumed flag is session state at this base. | A fixed burrow must identify its occupant(s), size and availability before digging. Removal/recruitment cannot be an arbitrary reroll or mistaken for the spurt animation. |

Main already has hand landing through `Core/Interaction/ICatchHands.cs` in some contexts, and
fallback paths add directly to a hold or pail. `Fishing/DeckPot.cs`, `TryBandNext` also combines
banding and hold admission. These facts are **not** the target handling rule: catch and goods
must be lifted, carried and placed by hand, with storage visibly filling. Existing direct-to-hold
paths are a named integration gap; this document neither edits nor blesses them.

## Shared proposed boundary

Before replacing any roll, have the architecture role review a **Core** contract. Fishing
implements it; Art, UI, Boats, World and Player consume Core values rather than concrete
Fishing types. Suggested names below are proposed, not existing APIs:

| Operation/value | Required meaning |
|---|---|
| `LivingOccupant` | Full stable identity tuple (region, species id, authored anchor/cell, member/burrow index, generation), position, actual depth/support, species, deterministic size and availability. A short hash may accelerate lookup but cannot be the sole identity: collisions must not merge animals. |
| `OccupantsIn` | Bounded query into caller storage for a metre-space region at an explicit double Core game time. Stable order and explicit overflow/incomplete flag. A display cap is never proof there are no more animals. Unknown habitat is distinct from a known-empty query. |
| `TryReserve` | Atomic available-to-reserved transition for occupant id, action id and snapshot version. Two rods or a rod and a pot cannot own the same animal. Repeating the same action is idempotent; a stale read cannot create a second animal. |
| `CommitRemoval` / `ReleaseReservation` | One ownership transition with explicit outcome. Hook loss releases at the last supported/wet position with an explicit disturbance event; successful capture moves the same token into held/trapped state. It never resamples a species or size. |
| `CatchTransfer` | Admission into hand, deck support or container succeeds before removal/ownership commits. An interrupted or capacity-failed transfer leaves the animal with its previous owner. The transfer has a recoverable action id. No temporary second CatchItem is independently sellable. |

The background field is a recomputable function of world seed, game time and immutable ecology.
**Reservations, removals and player-caused events are additional explicit state.** A pure field
alone cannot remember a clam dug yesterday or a fish put into a pot. Persisting captured
identity, depletion generations and in-progress transactions needs reviewed save design and
migration before activation. No save-format permission is implied here. Do not serialize the
entire background field or pretend persistent harvest follows from seed/time alone.

Use existing species region, gear, season, tide, hour, licence and size rules as appropriate
for the method. Physical presence is distinct from willingness to bite; an off-feed fish can
still exist and appear on the finder. An incompatible lure changes encounter acceptance,
not the animal's identity. A known-empty eligible set gives no bite/empty pot/empty burrow;
it must not silently fall through to the legacy table once that method is enabled.

## Phase 0: data and inactive logic, current foundation

Review and merge the ecology and school-model prerequisites independently. Typical reference
depth bands do not become hard occupancy limits without life-stage and ground certification.
For benthic animals, certify complete supported routes and fixed attachments. #1057 only
evaluates an already-certified straight segment; it provides no terrain adapter, habitat
certificate, population accounting, tide exposure or capture integration. School mark counts
and pooled display sprite counts are not an inventory of physical individual tokens.

Do not enable a runtime path in this phase. Keep the current playable methods intact while
reviewing data, species tunings and the future Core boundary. The water request is separate:
[fish under water](fish-water-request.md). Visibility or refraction cannot change occupancy.

## Phase 1: read-only encounter proof, before an M2 activation

Behind a default-off diagnostic, query actual candidates alongside the current resolver without
changing its result, spawning a second visible population, reserving animals or writing saves.
Compare species, metre depth, tide, gear and known-empty cases. Report mismatches, incomplete
buffers and unknown support; do not tune the field to reproduce each old random roll.

Proposed rod encounter score is `availability * lureAffinity * distanceWeight * depthWeight *
feedingWeight`, all nonnegative and supplied by reviewed species/gear data. Spatial and depth
tolerances belong in those Defs, not this document. Deterministic selection uses the action id,
world seed and a stable candidate order. This selects among actual eligible animals; randomness
can govern a bite or approach, never invent an absent species. Run exact replay across 1,000
seeds, long times, negative diagnostic times, region transitions and reordered candidate reads.

Acceptance: the diagnostic changes **zero catch results, containers, save bytes or pixels**.
No graphic comparison is claimed here; any later diagnostic wiring that affects presentation
requires its owning lane's proof. Incomplete occupancy queries block selection rather than
being misreported as empty. Slow-tick and memory budgets must be measured before activation.

## Phase 2: rod and clam transactions in the working-coast slice

**Rod sequence:** cast/drop samples its real lure XY and depth; obtain eligible nearby animals;
select and reserve one at the bite/commit boundary; keep that same species, weight and id through
bite, strike and fight. Presentation must transition continuously from the reserved animal's
current supported/wet position to the line constraint. An escaping fish returns once; a landed
fish transfers into the angler's hand or a physically supported landing surface. Boat holds
are destinations reached by manual placement, not an automatic landing target. Capacity and
licence failure leave a recoverable animal or a supported release, never a consumed invisible
fish. Exact licence gating and existing bait-spend flags remain independently tested rules.

**Clam sequence:** the authored burrow id identifies a fixed attachment and a reviewed number
of occupants. Reveal/spurt is a readable cue, not the creation or relocation of a clam. At a
successful shovel interaction reserve the available burrow token, validate exposure and hand
admission, then remove exactly that token into the hand. A missed dig does not manufacture
another occupant. Persisted depletion and tide-driven recruitment need a distinct generation
rule; #1033's tide refresh must be reconciled before choosing that rule. A high tide animation
or scene reload alone cannot duplicate a sold clam. Clam digging stays a hand job; no staff
automation is added to it.

Acceptance: two competing consumers yield exactly one owner; retry yields exactly one catch;
full hands/container, lost fight, invalid licence, scene unload and save/reload at each boundary
conserve the token. Verify the animal remains fixed in a burrow until removal, the rod selects
only a present species, and changes in visual visibility never change catch eligibility.
Any persistence or manual-handling prerequisite absent at review keeps activation disabled.

## Phase 3: pots read historical supported encounters, still M2

Placement validates the pot's support, depth, bait, licence and entrance. During soak, an animal
can be caught only after a supported path reaches the entrance while it is open and capacity
exists. Bait favours adjust approach probability among real candidates, retaining their soft
preference rather than turning them into a species gate. Never move a distant lobster straight
from a patch centre into a trap or use a midwater school depth as its feet.

Record accepted entrance events as occupant id, event time and pot action id. Order contenders
by event time, then stable pot id and full occupant id; reject occupied/reserved animals.
The pot owns its trapped tokens until a physical release or haul transfer. Capacity caps accepted
tokens, not a freshly rolled haul count. On haul, show those same animals on the supported
pot/deck, inspect and band/release by hand, then carry keepers into storage. Banded is a handling
state, not permission to teleport into a hold. Freshness keeps its existing species/handling
policy and stamps once at the reviewed transition, not on every query or container transfer.

The encounter solver must produce identical events for slow ticks, one long elapsed-time query,
sleep, unloaded regions and save/reload. Sampling only positions at haul, or one point per coarse
tick, misses paths crossing between samples and is not acceptable proof. Proposed solutions are
analytic intersections for fixed periodic supported routes or bounded indexed encounter epochs
with an exact carry-over cursor. Neither is implemented here. If capacity/solver limits cannot
resolve an interval, report it and retain work; never silently drop events or fill missing slots
with table rolls. Price long skips and multi-pot contention before selecting that algorithm.

Time uses `IGameClock.TotalSeconds` and its configured compressed day: game hours elapsed are
`(now - placed) * 24 / SecondsPerDay`. The baseline TrapSoak hard-codes 3,600 seconds per hour;
reconcile the already-owned correction #1046 rather than building another conversion. Test a
non-default day length, not just the default. Tide and habitat at **event time** govern entry;
the hauling weather cannot retroactively replace the pot's animals.

Acceptance: an actually empty habitat remains an empty pot; an animal crossing between ticks
is found once; a clam cannot walk into a lobster pot; same seed and explicit history yield the
same ordered catch. Two pots sharing an entrance candidate cannot duplicate it. Hauling twice,
returning a short/berried animal, a full hold and a failed manual transfer conserve all tokens.

## Later phases and release gates

M3 nets, long-duration fleet operations and leases retain their own roadmap dependencies.
A wild mussel attachment is not a grown lease crop. Crew eventually perform the same physical
pick/carry/place transactions after the player has earned automation; no remote goods transfer
is introduced. The rod remains a manual skill. This note does not implement longline, nets,
leases, restocking ecology or a fleet simulation.

Before any method switches from rolls to occupancy: merge prerequisites, approve Core and
save contracts, author species and ground data, pass exact conservation and replay tests,
measure slow-tick budgets, and obtain the responsible art lane's support/occlusion plates.
Keep rollback at a safe transaction boundary with explicit migration; do not switch to legacy
rolls midway through a reserved fight or a partially filled physical pot. No existing assertion
is weakened: tests pinning table-selected outcomes are migrated only in that implementation PR,
with each moved rule named and its occupancy/conservation equivalent proven.
