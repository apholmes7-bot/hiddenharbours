# Carried goods persistence proposal

**Proposed for lead-architect review.** Persist each carried item once, with an exact size, a single location and its preservation history. A fish in two hands, a pail on a truck and an unconfirmed delivery at a buyer must resume where the player left them. This extends the [goods handling proposal](https://github.com/apholmes7-bot/hiddenharbours/pull/1042) under [P2, P4 and P5](../vision-and-pillars.md#3-the-five-pillars). It changes no save code and reserves no schema version.

## Current format and missing facts

At the pinned baseline, [SaveMigration](../../Assets/_Project/Code/Core/Save/SaveMigration.cs#L16) has `CurrentVersion = 17`. Allocate the next version from the actual main at implementation time. [ADR 0008](../adr/0008-save-schema-and-versioning.md) requires append-only fields, forward migration, Core seams and atomic writes.

| Current source | What survives | What carried goods still need |
|---|---|---|
| [SaveData hold lists](../../Assets/_Project/Code/Core/Save/SaveData.cs#L94) and [HeldCatchDto](../../Assets/_Project/Code/Core/Save/SaveData.cs#L606) | Species, quantity and freshness triple for boat hold, clam pail and freezer | Individual identity, exact mass/envelope, hands, portable containers, placements and support parent |
| [HoldCatchCodec](../../Assets/_Project/Code/Core/Save/HoldCatchCodec.cs#L20) | Consecutive equal species/freshness runs; no exact weight | Different individuals must remain distinct even when a display groups them |
| [SpeciesCatchItemFactory](../../Assets/_Project/Code/Fishing/SpeciesCatchItemFactory.cs#L18) | Rebuilds weight at the species midpoint | The actual rolled mass and resolved dimensions cannot be recovered from an old file |
| [ShipHold restore](../../Assets/_Project/Code/Boats/ShipHold.cs#L48) | One legacy boat-hold list, restored without capacity rejection | Per-instance carrier identity, so stock is not copied into every owned boat |
| [DeckIceBox limit](../../Assets/_Project/Code/Boats/DeckIceBox.cs#L32) | Freshness is saved; remaining ice/lid state is explicitly not persisted | Portable cooling budget and lid state, settled on the same game clock |

The present DTO has no general hand/world-container graph. A future migration cannot recover a dropped pail or in-hand fish that was never recorded. Keep that distinction visible in release notes; do not invent historical stock, size rolls or remaining ice.

## Proposed records and ownership

These are logical records, not approved C# names or a JSON contract. Core owns the persisted contracts; modules capture and restore through Core interfaces. Resolve Def ids through the content registry. An instance id identifies one owned object; a Def id identifies its type. A boat instance must not be confused with a hull type.

| Record | Authoritative fields | Derived elsewhere |
|---|---|---|
| Goods registry | Format marker, legacy-import marker, next persistent instance sequence and committed state revision | Instance-id lookup and reverse parent index |
| Animal or item | Instance id; profile/species id; acquisition provenance; actual mass in kg; resolved envelope in metres; freshness `(spoilAccrued, lastSettleGameSeconds, mode)` | Def prices, current handling class, current spoil and sale quote |
| Optional life history | Life state, accumulated exposure, exposure anchor in game seconds and the immutable profile/rule revision used to interpret it | Current life at a supplied game time; never infer life from freshness alone |
| Portable container | Instance id, container Def id, lid/drain state and cooling state | Capacity, grips, fill sprites and authored dimensions |
| Membership | Parent-container id, child-entity id and selected placement/slot or metric local pose | Child count, total occupied volume/mass, reverse parent index |
| Root location | Entity id and exactly one tagged case: hand actor plus left/right/both; supported parent plus local pose; or recovery record | Region and level inherited through the support chain |
| Mutable support | Instance id, support key and player-mutated region/level/pose for a boat, vehicle or movable fixture | Authored geometry, support polygon, collision and rendering |
| Receiving lot | Counter id, receiving container, selected delivered entity ids, player ownership and settlement revision | Displayed grouping and current quote |

Membership is the sole containment authority. Save root locations only for entities outside compartments; do not independently store item-parent pointers and another copy in vehicle cargo. A two-hand location reserves both hands for **one** entity. A carried container keeps its descendants. The union of containment and physical-support edges must be acyclic and resolve to a valid region/level. Stacking on a lid is support; being inside a tote is containment.

Use a deterministic save-scoped id allocator, such as world seed plus a persisted monotonically increasing sequence. Allocate in a stable operation order. Do not generate simulation identity from wall time, Unity instance ids or global randomness. Deleted/sold ids are never reused.

Individual animals have quantity one. Ice, water and bait batches need explicit units and split/merge provenance before they join the format; a stack count must not silently replace animal identities. Preserve sampled mass/envelope as irreducible facts even though ordinary Def balance fields remain references. If content later makes an envelope incompatible, preserve it in the take-only/recovery route rather than resizing it on load.

## Cooling and life across time changes

Save one cooling authority per container: budget at its last settle time, lid state and the interpretation needed for that budget. Under a constant regime derive an absolute expiry from it; do not let separately saved budget and expiry disagree. Settle the old regime before adding ice, changing a lid, changing storage or freezing. Ice and water consume container volume and mass when those resources become physical goods.

Keep the existing Core freshness triple and settle through every protection boundary. Life, spoil and buyer grade are separate facts. Freezing cannot revive an animal, moving cannot remove old spoil, and a fresh-looking state cannot prove life. The isolated [landed-catch experiment](https://github.com/apholmes7-bot/hiddenharbours/pull/1062) is research, not the runtime or save contract. Mortality remains disabled in the shipping loop until species data and durable exposure history are approved.

Save all authoritative timestamps as absolute game seconds from `IGameClock.TotalSeconds`. Save/load restores the same instant. Sleep advances through the same cooling/life boundaries as continuous play. Wall-clock absence advances nothing before an explicitly approved offline policy; staff/offline accrual is M3 work. Tide, weather and other deterministic fields continue to recompute from seed and game time.

## Transfers and sale commits

1. Validate source and destination revisions, reach, route, hands, opening, fit, support, weight and ownership. A normal put that does not fit refuses without mutation; a hand put leaves the same item in hand, following the [RULED 2026-11-11 choices](../../backlog/proposed-goods-handling.md#ruled-handling-choices--2026-11-11). A large fish requires both hands after setting down the rod; the one-hand pail limit is 8 kg gross. An explicit later tip/spill is a separate operation. These handling decisions do not ratify this proposed schema.
2. At the pickup commit, move the entity from source to a durable hand/device location. Movement and animation never hold the only copy. A destination disappearing during carrying leaves it in the carrier's hands; a cancelled pickup leaves it at source.
3. At set-down, validate again and commit one location change. A save may capture source, held or destination state, never a half-applied transfer. Publish the Core event only after commit.
4. The first buyer delivery is one fish set on the counter. Delivery retains player ownership until confirmation or, in later staff work, an authorized sell policy commits. Take-back uses ordinary hand rules. Later whole-pail delivery transfers selected contents; it does not sell the reusable pail.
5. Sale removes only delivered goods and updates wallet, market supply and receipt in one snapshot revision. Duplicate confirmation/retry must produce the same result once. No remote hold pointer supplies a counter sale.

An in-memory snapshot lock/revision can make a single-slot save atomic without introducing an unbounded transaction journal. If asynchronous durable commands are later added, specify their bounded idempotency ledger separately. Capture failure keeps the previous complete file; it must not overwrite a valid save with a partial graph.

Coordinate the market part with the separate [market supply persistence proposal](https://github.com/apholmes7-bot/hiddenharbours/pull/1049). Use the approved economy record in the shared commit rather than adding another authoritative supply copy to goods.

## Forward migration

1. Clone and validate the legacy DTO; leave the original file intact until a complete migrated snapshot passes validation and atomic replacement. Reject an unsupported future format before an older migrator can reinterpret it. Do not change old fields or integer enum meanings.
2. Expand the three legacy hold lists in a defined order: boat, pail, freezer; then list row; then unit index. Create stable distinct ids from that order and preserve every freshness triple. A persisted import marker makes load-again idempotent. After import, old lists remain legacy fields and cease to drive stock; never run two writers.
3. Attach the boat aggregate only to the saved active boat, not all owned boats. Give the legacy pail/freezer their original ownership and supported fixtures. Missing/ambiguous carrier identity enters the explicit recovery manifest; no guess distributes goods across the fleet.
4. For old mass/size, use the current midpoint fallback **only for migration**, tag `legacy_estimate` provenance and freeze the resulting dimensions. Preserve exact sampled values for all new goods. Historical rolls remain unknowable.
5. If legal legacy stock exceeds new size/count/weight limits, retain it in a visible, reachable **take-only legacy compartment at the original store**. Refuse new puts, permit ordinary hand withdrawals, preserve all payload and spoil. Enable migration only once that interaction and support exist. Never scatter an overfull load, discard it or grant a larger purchasable container.
6. Preserve existing legacy ice-load semantics: no remaining cooling is invented when none was saved. Legacy live-mode shellfish retain the existing compatible wet-holding interpretation; other legacy catch begins still. Tag inferred life provenance and grant no new premium for that inference. Do not introduce mortality retroactively into old stock.
7. Unknown species, container, region or support ids retain their raw payload in a visible recovery record. Retrieval needs an explicitly owned recovery interaction and normal carry limits. Define that route before release. Do not copy goods to a buyer, discard unknown ids or silently clamp quantities.
8. Validate and write goods plus the import marker in the same atomic commit. Reload the result and prove stable ids/counts/history. Downgrade to an older writer is unsupported; preserve the newer file rather than dual-writing a lossy legacy inventory.

## Acceptance and delivery order

| Disposable fixture | Required result |
|---|---|
| Fish in left/right/both hands; pail on cart on truck | One entity per location, exact mass/size, hand reservations and parent chain after reload |
| Two owned boats and one legacy boat aggregate | One import at the active boat or recovery, never one copy per hull |
| Maximum legal old stock beyond new geometry | Every unit retained, accessible take-only, normal new puts refused |
| Unknown Def/support; duplicate parent; containment/support cycle | Raw unknown state retained; invalid graph rejected before replacing the good file |
| Warm → iced → expired → frozen; dry → wet exposure | Same spoil/life at the same game time after continuous play, sleep and reload; no regained quality/life |
| Save during pickup, held travel, placement and sale | One complete state per revision; retry neither duplicates stock nor pays twice |
| Load legacy twice; save/reload new format twice | Import occurs once; identities, exact new sizes, provenance and budgets remain stable |
| Interrupted disk write or unsupported newer format | Previous complete file retained; no partial overwrite or lossy downgrade |

Approve the schema and recovery/migration contract first. Then implement Core records and tests, wire the smallest manual fish-to-counter route, and add portable containers/cooling with their save proof. M2 arrangement and carts follow their own acceptance; M3 staff adds durable jobs; M4 freight adds its own transit records. Architecture review owns the version and contracts, gameplay-systems owns physical transfers, economy-sim owns cold chain and settlement, and art/world/UI lanes own supported fixtures and readable interactions.
