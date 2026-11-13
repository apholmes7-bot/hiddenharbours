# ADR draft: a friend in the host's campaign

**Status: Proposed. Nobody has accepted this proposal.**

Decision owners: product owner and lead-architect. No ADR number or save-schema
version is reserved. This draft authorizes no implementation or roadmap change.

## Context

At main `00b030cf8d286bc9d3b1464acd55c025e5a26a32`, the campaign is single-player.
The East Water trial (`11b09e2637`) shares boat presence and environment time,
but no hands, catch, economy, story or disk save. Main's v17 save and services
describe one player/active boat. Its separate hour and flow readings also exceed
the trial's clock contract. The [campaign plan](../../design/campaign-coop-plan.md)
contains the source audit, estimates and test gates.

## Decision asked for

Approve or amend this initial scope as one coherent set:

- Two participants in **the host's world**, with a guest character saved there.
  No goods, money or story progress export to another campaign.
- **One host save and shared wallet**; separate hands, personal gear/licences,
  character and private Notebook notes. The host validates each action.
- **One earned boat crewed by two**, with authored deck capacity and exclusive
  helm control. Work ashore before ownership; no free guest boat.
- **One region at a time**, crossing only when both are ready and physically
  at the passage or aboard. No teleporting the distant player or cargo.
- **Host may continue without the guest.** Disconnect retains actor/goods at
  their supported position and releases control claims. Recovery uses nearby
  hand actions; rejoin binds the same record. Host exit ends play; no host
  migration or closed-world income. Crashes restore the last complete checkpoint.
- Local menus remain local. **Sleep/fast-forward needs both connected players'
  readiness**; the host commits it once after transfers/rest positions are safe.

The lead-architect is asked to accept host-authoritative commands/snapshots
through **Core/EventBus**, actor-addressed adapters and one coordinated
world/player/goods persistence design. Schema versioning belongs to implementation.

## Options

| Option | Benefit | Consequence |
|---|---|---|
| **A. Host-world visits (recommended)** | Shared work with one authority for fish, purchases, story and saves. | Guest depends on the host and accepts shared spending/progress and group travel. |
| B. Equal partners with separate personal progress, wallets, boats and free travel | More independence; progress follows each player. | Requires progress-conflict rules, duplication prevention, multiple region contexts and broader save/performance work. Not estimated or approved here. |
| C. Keep only the separate boat trial for now | No campaign migration or production-network commitment. | Friends can meet on the water, but cannot play or retain the campaign together. |

## Consequences and delivery boundary

Host commits are ordered and retry-safe. Def ids and mutable instance identities
remain distinct. Recompute weather, tide and undisturbed schools/routines from
shared seed, time and content; host owns path-dependent goods, market and flags.
Synchronize hour and flow; do not save weather or assume identical peer physics.
All loading, carrying and unloading remains physical. Enterprise stays phase-gated.

Implement the plan in order: transport/menu → clock/seed → actors/boat →
hands/carry → economy → saves → group travel → opening/story/Notebook.
Estimate: **37–63 lane days**, excluding review/CI waits and art changes. PR 4
permits a shared job in a disposable rehearsal (**17–29 days**); PR 6 permits
the first saved loop (**27–46 days**). Initial scope is private LAN/direct access;
internet invites require a service decision and **4–7 additional days**, with
service charges evaluated separately.

The lead-architect resolves the schema with goods work before actor integration:
preserve legacy saves and exact new ownership/size/freshness; unsaved historical
hand/pail state is unrecoverable. Required tests cover races/retries, migration,
atomic write failures, late join and failed travel. Solo play must remain intact.
Profile the **60 fps laptop** target; an art lane accepts supported occupants,
visible cargo and the reported floating-rod fix. This draft changes no look.

The two-machine checkpoint after PR 3, repeated with carrying at PR 4, asks:
**is working aboard usable for the guest over the actual connection?** Accept
that evidence and the save design before persistent integration. No Unity,
build, player or two-machine test ran for this documentation change.
