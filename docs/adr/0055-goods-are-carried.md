# ADR 0055 — Goods are carried

- **Status:** Proposed
- **Date:** 2026-11-10
- **Decision owner:** owner; technical steward lead-architect. Review: gameplay-systems, economy-sim, art-director, art-pipeline, world-content, ui-ux and qa-test.
- **Serves:** P2 From Dory to Dynasty, P3 Living Working Coast, P4 Earn It Then Automate It, P5 Cozy but with Teeth.
- **Related:** ADR 0003 (data-driven content), 0008 (save versioning), 0020 (placed objects), 0035 (Vehicles), 0043 (input intents), 0053 (held props). Extends their application to goods; does not supersede them.

## Context

The manual-handling ruling requires catch/goods to move through hands and physical storage, with visible contents and transport as gameplay. Main already has two hands, carried catches, pails, freshness and visible deck storage. It also has direct hold landing, batch freezer exchange, wired-hold sales, lifecycle stowing and all-scene spoiled-catch disposal. Its save DTOs preserve species/count/freshness but omit exact rolled size and portable item locations. These are scaffolding limits, not authority for remote selling or unlimited hands. [Source trace](../design/goods-handling/00-today.md) records the base and evidence.

## Proposed decision

1. A gameplay transfer moves one existing item/container identity along a reachable supported route. It passes through hands or a compatible lifting device. No proximity-to-market sale, remote inventory pull or implicit boat-to-wharf delivery. A counter buys only the selected goods delivered into its receiving area.
2. Two physical hands remain the carry resource. A full hand refuses without fallback; a long/awkward load may reserve both. Metric shape/size controls fit; gross load only limits lifting/device capacity. No extra cargo slot arises from a sling. New limits and compatibility live in Defs/GameConfig, not code constants.
3. Fixed/portable containers, constrained visible nesting, supported stacks and later carriers form one ownership/location graph. Contents show as actual-species sprites filling/emptying the storage. Held prop presentation still follows ADR 0053. Ordinary overflow blocks; a later explicit pan-tip action can place items on a validated surface. Goods never hover above their support.
4. Core owns the shared read/transfer/support/sale contracts. Gameplay-systems implements mechanics, economy-sim preservation/buyer policy, lead-architect the transaction/save boundary. Other feature modules consume Core interfaces/events, never another feature's concrete classes. Events report completed state; they do not carry the only copy of inventory.
5. Save stable instance ids, exact new-catch size/weight, authoritative membership, hand/support locations and freshness/cold history. Recompute derived totals, fit, sprites, current spoil and tide/weather. A transfer or sale snapshots atomically; rejected/stale/retried requests cannot lose, duplicate or double-pay goods. Lead-architect chooses the then-next schema and ships a tested migration, preserving legacy overflow in visible take-only source storage.
6. Preserve existing settle-on-read freshness and settle-before-mode-change rules. Proposed alive/dead state is distinct from spoil and never graphic; freezing cannot revive life. Species-compatible live holding, floor deadlines and persistent ice are explicit later data, not a second clock or silently changed M1 balance.
7. Deliver the bounded M1 repair loop first, then M2 grid/storage/cart and existing-vehicle cargo, M3 machines and earned staff handling, M4 freight. Intake/lifecycle shortcuts must be closed before declaring the handling conversion complete. This decision does not authorize later-phase runtime work merely because a Def can describe it.

## Consequences and alternatives

The first benefit is a playable hold→hand→tote/counter path: catch has a way ashore and sale matches what was delivered. Costs include new persistent identity, exact-size migration, source-specific access, fill refresh and transaction/integration testing. The three new machine charters also require later art/tooling/routes; a bed anchor alone is not a logistics system.

Keep no infinite row inventory, remotely sellable boat hold or automatic full-container spill. Those would erase transport choices or surprise the player with lost goods. A full 3D physics/packing simulation is unnecessary: authored metric placements first, the canon grid in M2, and deterministic state changes keep the handling readable and saveable.

No code, asset, scene, save format, balance or look changes in the proposing PR. [Model](../design/goods-handling/02-model.md), [data proposal](../design/goods-handling/03-data-model-proposal.md), [first slice](../design/goods-handling/04-first-slice.md), [phasing](../design/goods-handling/06-phasing.md) and [backlog](../../backlog/proposed-goods-handling.md) define the reviewable handoffs. The owner explicitly accepted the nine recommendations on 2026-11-11: **"i go with all leans for now"**. The [decision page](../design/goods-handling/09-owner-questions.md) and [ruling record](../../backlog/proposed-goods-handling.md#ruled-handling-choices--2026-11-11) resolve those choices. This ADR remains Proposed for its technical contracts, save migration and phased implementation; the handling choices do not ratify that architecture or the remaining tunables.
