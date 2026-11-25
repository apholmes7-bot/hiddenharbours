# Module coupling audit for carried goods

Audited base: `12bc04b19a2efb33e352b4eea58104f7a5599be1`. This is a documentation-only review of rule 4 against [ADR 0055](../../adr/0055-goods-are-carried.md). It proposes handoffs; it changes no implementation, authored content, look, price, save format or accepted plate. It serves P2, P3 and P4 by identifying the boundaries that must support physically carrying stock to a buyer.

The current graph has **one recorded feature-to-feature exception: Fishing → Economy**. The other nine non-Core runtime assembly edges belong to the documented Player/App composition layers; one of those, App → Economy, has no concrete runtime consumer. There is no newly discovered unrecorded feature edge. The practical goods blockers are broader than assembly references: a perfectly legal Core interface can still grant remote inventory mutation, and a composition caller can still lose or relocate held stock during a lifecycle transition.

## How to read the findings

[Project structure §5](../project-structure.md#5-assembly-definitions-asmdefs--the-dependency-rules-and-the-real-graph) permits Player, UI and App composition, editor inspection/baking, and test dependencies. That exception is retained in this audit; an App constructor or Player rendering adapter is not automatically a rule violation. Each page distinguishes the concrete boundary, goods-related behavior or migration risk, the existing Core seams, and a proposed contract where one is warranted. Every proposed interface/event name is a design suggestion unless explicitly identified as existing.

The [coverage ledger](coverage.md) accounts for all 37 asmdefs, 12 runtime project assemblies, 3,027 C# files, 43 cross-feature runtime imports and four fully qualified runtime feature uses. It records excluded strings/comments, editor/test scope, the complete runtime graph, and weaknesses in the current graph-only guard. Per-edge pages inventory concrete types and their use sites, including legitimate composition and unused imports, so the absence of a finding is reviewable.

## Ranked edge reviews

Priority measures how much a boundary blocks the bounded manual carry route, not the amount of source code or severity of every hypothetical configuration failure. **High** means ownership, access or lifecycle handling must be settled before the route is complete. **Medium** means a related policy/presentation boundary must be preserved during integration or acquisition migration. **Low** means composition or a later-phase concern that should not delay the first route. Ties are intentional.

| Priority | Directed edge | Boundary status and next decision |
|---|---|---|
| High | [App → Boats](app-to-boats.md) | Permitted composition, but the persistent hold proxy grants source-agnostic read/add/clear authority. Carried counters now read the held pail; migrate remaining legacy provider routes with source-specific authority. |
| High | [Player → Boats](player-to-boats.md) | Permitted composition around boarding, deck support and hull access. Carry location, support and hand occupancy must survive each supported transition. |
| High | [Player → Economy](player-to-economy.md) | Permitted vendor/repair composition. The same consumers still operate on global legacy bucket stock at freezer/wet points; selected-source and interaction arbitration need migration. The repair query and range default themselves are low priority. |
| Medium | [App → Player](app-to-player.md) | Permitted composition for travel and player lifecycle, with no observed defect in these references. Preserve one authoritative hand/item state across the composed transition. |
| Medium | [Player → Art](player-to-art.md) | Permitted presentation composition, including gameplay reads of concrete presenters. Keep support/geometry and item truth accessible through Core without changing the rendered result. |
| Medium | [Fishing → Economy](fishing-to-economy.md) | Recorded rule-4 exception: six files use three concrete bait/licence types. Adapt authored profiles and permission through Core while preserving identities, deterministic catch behavior and expenditure. |
| Low | [App → World](app-to-world.md) | Permitted scene/arrival/data composition. Preserve access/support semantics and authored identities; do not turn this cleanup into a region or art rebuild. |
| Low | [App → Fishing](app-to-fishing.md) | Permitted fleet day/catch/landing composition. Clock-indexed crates are implemented; market payment and the broader goods authority remain separate. |
| Low | [App → Environment](app-to-environment.md) | Permitted clock/environment installation and arrival configuration. Existing Core time/environment readers are the downstream boundary. |
| Low | [App → Economy](app-to-economy.md) | Assembly reference only; no concrete runtime use found. Removal is a separate compile-verified cleanup, not a goods prerequisite. |

## Contract handoff and delivery order

Use the [first-slice boundary proposal](../../design/goods-handling/04-first-slice.md#core-boundary-handoff) as the common vocabulary. `IHold`, `ICatchHands`, `IHandsCarrier`, `ILicenseService`, `CatchLanded` and `FishCaught` already exist. `IGoodsContainer`, `IGoodsTransfer`, `IGoodsSupport`, `IDeliveredGoodsSale`, `GoodsMoved` and `GoodsLocationChanged` are proposed, not shipped APIs at the audited base.

1. Lead-architect settles item/container identity, revisions, transaction results and the snapshot boundary with gameplay-systems and economy-sim. Preserve exact size, freshness history and one authoritative location. This audit does not choose a save version or write a migration.
2. Gameplay-systems adapts hands, boat storage and reachable supports through those Core contracts. A request checks hands, source/destination, reach, support and current revisions before committing. A refusal leaves the same item at its source. Keep existing concrete composition only where it wires implementations or presentation.
3. Economy-sim preserves main's carried-pail selection and gives later receiving areas an explicit delivered selection and a quote/commit contract. A buyer gets no generic remote `IHold.Clear` privilege. App binds each adapter to an actual storage/support identity rather than whichever global `ShipHold` is found.
4. Audit the complete travel/boarding/load/disable path against the same state before calling the carry route complete. Rendering reads committed state; it does not become the sole owner. Art roles prove unchanged contact, fill, pose and occlusion in the editor when an implementation changes those adapters.
5. Migrate acquisition, licence/bait policy, freezer/banding/disposal shortcuts and lifecycle stowing in the bounded repair sequence. Preserve exact existing rule assertions until their replacement truth is explicit. Physical supply stocks, carts, trucks, machines and staff remain in the [goods phasing plan](../../design/goods-handling/06-phasing.md); this audit does not pull them forward.

Core commands need explicit synchronous results. EventBus signals report completed work and let fill/UI/onboarding refresh; subscriber presence or handler success must never determine whether an item exists. Rejected/stale requests emit no success event. Repeated delivery cannot award another catch or pay twice. Mere relocation of old stock must not replay acquisition progress.

## Findings, verification and ownership limits

The edge pages give file/line evidence and unexecuted failing-case descriptions. They distinguish known scaffold behavior, configuration hazards, stale comments and coverage gaps from proven live-scene defects. No scene wiring was inspected or inferred as proof, and no hypothetical failure is reported as an observed playtest. In particular, removing Fishing's Economy reference would not repair direct-hold landing. Main now makes flagged carried counters delivered-only through Core hands; an unrestricted legacy `IHold` provider still does not provide that guarantee.

Verification performed for this document set: exact-base assembly/source enumeration, independent edge reviews, graph cycle and runtime/editor checks, source-link and inventory reconciliation, and a final diff review restricted to new Markdown documents. Unity/EditMode/PlayMode tests were not run; no build, visual or save compatibility result is claimed. The existing four `ModuleBoundaryTests` methods were read, not executed. This is sufficient to review the documentation; each proposed implementation needs its own exact-count logic tests and relevant integration/editor proof.

Lead-architect reviews Core contracts and save ownership; gameplay-systems reviews hands/boat/acquisition adapters; economy-sim reviews delivered sale, licence and preservation policy; ui-ux and art roles review consumers and later visual proof; world-content reviews real receiving/support routes. The audit grants no permission to edit another active lane's source, scene, asset or rig. The nine accepted handling choices remain recorded in [the goods ruling](../../../backlog/proposed-goods-handling.md#ruled-handling-choices--2026-11-11); all new contract shapes here remain proposals.

## Refresh to current main

The refresh preserves all main changes and corrects two superseded findings: carried-pail counter sales now bypass the boat provider, and the NPC lobster fleet now derives catch, deck work and hand-carried landing from deterministic plans. The freezer/wet-point and broader identity/revision proposals remain outstanding. Re-ran `inventory.py`, `app_reference_scan.py`, `verify-graph.py` and `validate-docs.py` without an editor; all changed App type sites and the complete import ledger were regenerated against this base. Existing source tests were inspected, not executed. No runtime, asset, ledger identity or assertion was edited.
