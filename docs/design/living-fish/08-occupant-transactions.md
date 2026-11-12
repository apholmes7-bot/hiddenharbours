# Living-occupant identity, ownership and depletion

Status: proposed Core and persistence contract, for architecture, gameplay, economy and QA
review. Design only. P1, P3 and P4. No catch switches from a table roll, no save is read or
written by this work, and no schema version is reserved. The release gate below requires a
reviewed, implemented migration before activation. See the
[capture phases](../living-fish-capture-phases.md),
[behaviour model](01-behaviour-model.md) and
[goods backlog](../../../backlog/proposed-goods-handling.md).

## Baseline facts to preserve

At the audited baseline, `SaveMigration.CurrentVersion` is 17. `HeldCatchDto` stores species,
quantity and a freshness triple in the boat, bucket and freezer lists; it has no individual
identity, exact weight, size or living history. `PlacedTrapDto` stores placement facts,
including its unique instance id and double game time; the existing pot resolves table
catches from those facts. `SpentClamHoles` already records stable hole ids and spent times.
None of these records is a global wild-animal reservation or an ownership transaction.

Recomputed background motion remains a function of seed, game time and immutable content.
Harvest, a hook, a pot entry, a hand transfer and a release are player-caused mutations.
Persist those sparse exceptions; do not serialize all background schools or equate a display
sprite cap with the number of available animals. Sonar, visible readers and encounters must
consume the same availability overlay when the one tested LF-02 slice is activated.

## Identity and immutable animal facts

Proposed Core `OccupantId` is the full tuple:

`(worldPopulationId, populationEpochId, homeRegionId, populationScopeId, speciesId,
  stageId, anchorId, memberOrdinal, generation)`.

The population id is saved once for this world; its epoch pins the generation algorithm and
ecology/content revisions. Anchors use authored stable ids (including clam holes), or signed
integer home-cell coordinates with an explicit grid definition. Members never change home
identity when crossing a cell or region. Generation is a monotone authored recruitment epoch,
not the current day, queried time slot, scene-load count or hash of the current position.

Use exact tuple equality. Specify ordinal string ordering, integer encodings, tuple field
boundaries and a versioned stable hash for selection. A short hash can index a table but
collisions cannot merge animals or action ids. The full tuple remains available to disambiguate.
No runtime object id, `GetHashCode`, wall time or global random stream belongs in identity.

Species, stage, deterministic individual size/weight and generation are fixed when the
population creates that id. Snapshot these facts when first reserved/owned so a subsequent
content change cannot reroll the catch. Units, precision and existing catch tuning need a
reviewed implementation decision; no new weight/price constants are selected here. A school
centre alone supplies neither individual routes nor fishable-member cardinality; the future
provider must define both before this contract can be activated.

## Read contract

Proposed `OccupantsIn(region, volume, gameTime, snapshotRevision, buffer, cursor)` is a
read-only Core query. It returns a stable full-id order, physical position/depth/support,
availability and explicit `Complete`, `Unknown`, `Incomplete`, or `RevisionMismatch` status.
Do not change the ledger while enumerating. Buffer overflow retains a continuation bound to
the same immutable snapshot. Only a complete known result proves absence. Feeding and method
eligibility filter actual occupants; no fallback table invents one after a known-empty query.

Query at a committed simulation frontier. A reserved or captured individual is absent from
the free-wild subset but remains at its explicit hook, supported pot or handling location.
School/sonar counts must subtract exceptions without manufacturing replacement members.
Whether a trapped animal produces a sonar mark is a presentation decision, not a new free
occupant. Unknown habitat or unresolved history is not an empty set.

## State machine and atomic operations

Each live id has exactly one authority state: `FreeWild`, `Reserved(action)`, `Hooked(action)`,
`Trapped(potId)`, `Goods(locationId)`, or `ReleasedOverride`. A terminal sold/disposed/dead-wild
record leaves a depletion tombstone. `Goods` can be alive or still; vitality is independent
from ownership. Dead owned catch stays goods until a real terminal operation consumes it.
Background positions are never used for an id while an explicit authority owns it.

| Operation | Preconditions | Atomic result / refusal |
|---|---|---|
| `TryReserve(id, actionId, expectedRevision, eventTime)` | Complete eligible observation, free id, ordered event, valid source support and participant | One reservation plus immutable animal facts and new revision; stale/contended/unknown changes nothing |
| `ConfirmHook` / `ConfirmPotEntry` | Same reservation, compatible method, actual supported path/contact at event time | Same id becomes hooked/trapped; no species or size reroll |
| `TryTransfer(id, source, destination, actionId, revisions)` | Source authority, reachable opening/hands, destination capacity and support admission | Source removal and destination insertion commit together; one id, one physical location |
| `ReleaseReservation` | Same active reservation; no capture has committed | Return to its supported free trajectory, or an explicit supported release override if disturbed; one release event |
| `TryReleaseOwned` | Same id owned, alive and compatible reachable water/support; hand interaction | Ownership moves to a release override once; fail without deletion if the point is invalid |
| `ConsumeGoods` | Exact selected delivered/owned id, validated sale/disposal action | One terminal receipt; economy payment and removal are the same recoverable transaction |

Command results name `Applied`, `AlreadyApplied(same receipt)`, `Stale`, `Contended`,
`Unknown`, `AdmissionRefused`, or `Invalid`. Reusing an action id with different arguments is
invalid, never a retry. Idempotent replay returns the original receipt **before** stale
revision evaluation. A failed admission cannot consume an animal, bait twice, clear a hand,
award money or advance the depletion generation. EventBus notifications follow the commit;
they are readouts, not a second authoritative inventory/payment mutation.

Cross-container work is one Core transaction over all affected state and revisions. A
prepared intent can reserve admission but creates no second sellable catch. Durable states
are either pre-commit intent (source remains owner), or committed receipt (destination owns
it). Load resolves a prepared intent by aborting its admission and retaining the source;
load of a receipt never reapplies payment or creates another catch. The save snapshot must
include participants and journal at the same committed revision. If the current SaveService
cannot guarantee this, activation stays blocked until its own reviewed persistence change.

Concurrent commands are serialized by the simulation authority. Historical autonomous
entrances use `(eventTime, potId, fullOccupantId)` order across all competing pots; direct
player commands carry a persistent sequence and an explicit priority for ties with those
entrances. Proposed tie policy: settled entrances first, then player commands in sequence.
The authority settles history through a command's instant before admitting it; frame order,
query order and region residency cannot break a tie. This policy needs gameplay review.

Physical capture is not direct hold insertion. Land into eligible hands or a supported
landing surface, then carry and stow. Hauling keeps trapped ids through deck work; banding
does not stow. Release keeps the same animal at its real supported/wet position, with a
finite motion override until a continuous route rejoins a certified free path. A release
cannot snap it to the recomputed school centre or revive a still fish. Every transfer
preserves monotone vitality exposure, freshness and existing finite ice history.

## Sparse depletion, bounded state and recruitment

Every reserved/captured/terminal id suppresses exactly its original free-field member.
Persist sparse state keyed by full id; compact sorted member ranges only within identical
home/generation/state scopes. Never reuse a terminal id after a time-to-live expires.
New recruitment requires an explicit reviewed generation rule; it is later-phase work,
default disabled. No scene reload, sleep, high-tide animation or repeated query recruits.

Clam migration must reconcile the **existing** spent-hole rule, not reset it. Preserve each
hole's spent timestamp and original refresh semantics until a reviewed replacement supplies
a deterministic generation boundary and prior-save tests. While mapping is unresolved,
that burrow stays on its legacy method and outside the new physical-population slice.

Action ids are persistent `(participantId, sequence)` pairs; high-water marks never go
backwards on reload. Receipts can compact only after an acknowledged checkpoint makes their
effects durable. Old retries outside the retained receipt window return `Expired`, not
execute again. Checkpoint compaction preserves terminal membership/generations. Budget
limits are reviewed config, not arbitrary eviction: reject new work with explicit pressure
before losing existing state. Bounded per-call work does not imply constant total lifetime
storage; measure worst-case ledger growth and checkpoint cost before release.

## Proposed save migration, mandatory review before cutover

Use the **then-current** schema plus a new reviewed version. Do not assume the next version
is 18 or edit existing schemas in this proposal. Migration operates on synthetic DTO fixtures
and a copy in memory; never a player's live save during development.

| Data | Migration / compatibility rule |
|---|---|
| Existing scalar state, flags, gear, money, boats, placements and unrelated v17 additions | Preserve exactly; do not rebuild by querying live scenes |
| Existing held-catch runs | Preserve species, quantity, freshness and storage/source. Assign deterministic legacy provenance `(containerKind, rowOrdinal, unitOrdinal)` once under a migration marker; compact ranges avoid materializing huge runs. These are owned legacy goods, never harvested wild ids. |
| Unknown old size/weight and vitality | Mark size/weight provenance unknown, keep the legacy valuation/size policy until its reviewed goods migration; mark vitality Still. Do not invent an exact historical catch weight, a living history or a live premium. |
| Placed legacy pots / longlines | Persist an explicit legacy-resolver mode and its original placement facts. They finish once with their original resolver; new physical mode starts only after manual haul/rebait/redeployment and eligibility. Never reinterpret an already-soaked placement as a living encounter history. |
| Active hook / unresolved deck work | Inventory every existing producer before release. If its state cannot be losslessly mapped and saved, block cutover for that workflow. Never silently cancel or auto-stow a catch to permit migration. |
| Spent clam holes | Preserve hole ids/timestamps; require reviewed home-id and generation mapping before switching that bed |
| New sparse population state | Empty ledger, transaction journal and mode flags; no retroactive depletion inferred from legacy goods and no background school serialization |

New-format catches remain single-id goods and never also appear in legacy aggregate lists.
Migration may retain legacy lists as the sole authority for untouched runs; an explicit
per-container conversion marker makes the eventual hand/storage conversion exactly once.
Unknown Defs, duplicate ids/parents, invalid quantities or inconsistent receipts go to
recoverable blocked state, with their original data retained. Do not deduplicate by deleting
arbitrary units or grant a new catch as repair. Excess migrated goods remain take-only at
their existing source under the goods contract, without teleporting into a new container.

Future-version saves must be rejected intact at the load boundary before the existing
migrator's version clamp is invoked; otherwise older builds could drop the new ownership
ledger and duplicate catches. Keep a last-known-good file and replace snapshots atomically
only after validation. Backup, serialization and interrupted-write behaviour need explicit
Core review alongside the migration, rather than an assumption that the journal alone is
durable.

## Review and proof gate

Before **any** catch method switches: architecture approves Core authority, tie order and
save shape; gameplay/economy approve legacy-method compatibility and manual transfers; QA
reviews the migration fixtures and failure injection. Record the reviewed revision and real
test results in the implementation PR. This document does not claim those reviews occurred.

Required proof includes exact old-to-new counts and freshness for all containers; idempotent
migration; null/empty/unknown content; large compressed legacy runs; old pots finishing once;
existing spent-hole timestamps; collision pairs with equal short hashes; two methods sharing
one animal; stale versions; mismatched retries; full hands; broken support; release twice;
save/load before intent, after intent and after commit; interrupted file replacement; future
schema refusal; sleep/unload/reload; terminal-id replay after receipt compaction; and ledger
budget refusal without loss. At every checkpoint, compare the exact id multiset and money,
not just a nonzero count. No test may weaken a by-hand or catch-honesty assertion.

Migration and transaction implementation remain separate authorized slices. Activation is
off outside the single tested population slice and stays blocked everywhere that cannot
meet this contract. No reproduction, fleet automation, new livewell or art system is added.
