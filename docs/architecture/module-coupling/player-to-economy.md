# Player → Economy

Audit base: `12bc04b19a2efb33e352b4eea58104f7a5599be1`. Static source review; no Unity execution. This is a **permitted composition edge**, under [project structure §5](../project-structure.md#5-assembly-definitions-asmdefs--the-dependency-rules-and-the-real-graph), with goods migration debt. It is not an additional unrecorded feature-module exception.

**Goods priority: high for the freezer interaction; low for the repair query and reach default.** Replacing the namespace import alone would not make transfers physical or atomic. [ADR 0055](../../adr/0055-goods-are-carried.md) requires an explicitly selected, reachable container and one transaction authority.

## Complete source inventory

[Player's assembly definition](../../../Assets/_Project/Code/Player/HiddenHarbours.Player.asmdef#L4) references Economy. Three Player files import it, and each uses one Economy concrete type:

| Player consumer and import | Foreign type and all explicit type-use lines | Behavior carried across the boundary |
|---|---|---|
| [ControlSwitcher.cs:5](../../../Assets/_Project/Code/Player/ControlSwitcher.cs#L5) | `RepairLedger` at 688 | `BoardableNow()` reads the current save, checks the active hull id and ownership, then calls `RepairLedger.IsRepaired(save, hullId)` (670–688). |
| [GinnyFreezer.cs:5](../../../Assets/_Project/Code/Player/GinnyFreezer.cs#L5) | `StallReach` at 36 | Serialized reach helper; `Enable` at 75, `Disable` at 81 and `CanInteract` at 105. It gates the freezer's private keyboard path. |
| [WetBucketPoint.cs:4](../../../Assets/_Project/Code/Player/WetBucketPoint.cs#L4) | `StallGate` at 43 | Takes only `DefaultRange` to initialize the serialized interaction reach. Actual interaction runs through Core `IInteractable` and `InteractVerb`. |

`Freshness`, `SpoilContext`, `StorageMode`, `CatchItem`, `IHold`, `HoldCatchCodec`, `GameServices`, `ControlModeChanged` and `BoatRepaired` are **already Core**; their use does not add Economy concrete dependencies. `Mode` hits from a name-only search are fields/properties of Core state, not Economy's unrelated nested enum. No fully qualified Economy type references, namespace aliases, string type lookups or reflection-based Economy loads were found in Player.

## Core handoff proposal

All new names below are proposals, not existing APIs or an instruction to move authored assets.

| Current dependency | Contract or signal that should carry it | Owner and migration constraint |
|---|---|---|
| `RepairLedger.IsRepaired` plus direct ownership reads | Proposed Core `IBoatReadiness.Read(hullInstanceOrLegacyId)` returns ownership/repair state and a typed readiness result. Keep existing Core `BoatRepaired` for notification; a missed event must not become the only readiness record. | Economy implements the query; App injects it. Preserve the existing disabled-gate, missing-save, unknown-hull and owned-damaged branches exactly until a separately reviewed behavior change. Stable hull-instance identity belongs to the planned identity migration. |
| `StallReach` | Core's existing `IInteractable`, `InteractActor`, `InteractContext`, `InteractResolver` and `InteractVerb` can arbitrate the freezer offer, as the wet point already does. A proposed goods command receives the selected actor/container id and validates reach again at commit. | Player owns the interaction adapter; the shared transfer authority owns inventory mutation. Do not relocate the whole scene-search helper into Core or preserve a private input reader behind a new interface. |
| `StallGate.DefaultRange` | A shared interaction range read from the existing serialized field or a reviewed Core config/Def contract. No runtime Economy object is required. | Preserve the authored serialized values and the current default during any later migration; changing the number is a separate tuning decision. |
| Freezer and wet-point stock mutations | Proposed `IGoodsContainer` read/revision view and `IGoodsTransfer.TryMove(request)` from the [first-slice handoff](../../design/goods-handling/04-first-slice.md#core-boundary-handoff), plus a proposed preservation command for wetting the selected pail in place. `GoodsMoved` reports committed transfers; a separate preservation-change notification may invalidate freshness reads without implying a move. | Mechanical adapter remains Player; preservation policy is implemented behind Core by Economy. Existing Core `Freshness.WithMode` remains the settle-before-change operation. Do not invent a second spoil clock. |

## Source-proven gaps and failing-case descriptions

These are static deductions and proposed reproductions, **not executed failing tests**.

1. **The freezer does not use the selected physical container.** [GinnyFreezer.cs:113](../../../Assets/_Project/Code/Player/GinnyFreezer.cs#L113) resolves any `ClamBucket`, then deposit copies that bucket's entire list and clears/repopulates it (120–131). Withdraw adds to the same cached bucket (138–153). A portable `CarriableBucket` selected in the hands is not the exchange partner. Reproduction: make the legacy `ClamBucket` contain catch A, carry a different portable pail containing catch B to the freezer, and exchange. The code operates on A, not B. ADR 0055's desired result is that only the explicitly delivered source can move, with unselected stock untouched.
2. **The freezer bypasses the single interaction offer.** [GinnyFreezer.cs:101](../../../Assets/_Project/Code/Player/GinnyFreezer.cs#L101) reads `_interactKey` directly and calls `Exchange` after the reach helper passes. The field defaults to `Key.F` at 33. `Exchange` itself performs no actor/reach validation. Reproduction: invoke that key while within reach and while another modal/interaction owns the normal verb; the freezer path does not consult `InteractVerb` or the shell input gate. A future migrated route must prove exactly one permitted operation and no stock change on refusal. This is an existing input-path gap, not proof of a specific current scene overlap.
3. **The wet point also selects global legacy stock.** [WetBucketPoint.cs:88](../../../Assets/_Project/Code/Player/WetBucketPoint.cs#L88) discards the supplied actor; `Toggle` resolves any `ClamBucket` at 97, calculates new freshness states, clears and repopulates at 140–141. Reproduction: hold a portable pail at the wet point while another legacy bucket contains dry shellfish. Only the legacy bucket receives the mode change. The future preservation command must address the carried pail's stable id and revision, validate species and available water, and retain the current protection settlement rules.
4. **Freezer exchange has intermediate save snapshots.** `TryAdd` snapshots the freezer after each insert (46–54), then deposit clears and repopulates the bucket (130–131). Withdraw calls bucket `TryAdd` before removing from the freezer (150) and only later snapshots freezer changes (153). This is not a claim that a save is currently written in the middle of a synchronous call. Fault-injection acceptance: interruption after destination insertion must restore either the entire pre-transfer state or the entire committed state, never both copies. A Core facade around today's methods would not provide that guarantee.

No defect was established in the current repair query itself. It shares the Economy ledger's result and intentionally fails open in documented pre-bootstrap cases. Its replacement must preserve that behavior until the readiness policy is explicitly changed.

## Safe delivery order and proof

First define the shared identity/revision/transfer boundary with lead-architect, gameplay-systems and economy-sim. Then migrate the freezer and wet-point interaction adapters alongside the actual storage implementation. The small repair query and reach-default cleanup can follow independently; neither should delay the first reachable hold→hand→counter route.

Future acceptance needs exact inventory-conservation, refusal, preservation and interrupted-transaction tests; interaction arbitration needs integration coverage. Keep the existing freshness rules and assertions, migrating only tests that pin the superseded batch/global-source behavior and naming the rule change. This audit changes documents only: no input binding, range, save field, price, asset or runtime behavior is changed.
