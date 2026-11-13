# Playing the campaign together

> **Historical audit, superseded in scope and sequence on 2026-11-13.**
> [ADR 0057](../adr/0057-campaign-coop-host-world.md) and
> [CO-01 to CO-12](../../backlog/coop-campaign.md) now govern delivery: internet
> joining, shared licences, each player moving their body and steered boat,
> both aboard the intro boat at sea from the opening, and intro before the save
> migration. CO-01 to CO-04 may run beside M1; reassess at CO-05. The audit below
> describes its earlier proposal and is retained for its source findings.

Status: **Proposed. Nobody has accepted this plan.** Documents only.

Audit base: `00b030cf8d286bc9d3b1464acd55c025e5a26a32`. Trial inspected at
`11b09e2637` on `gpt-coop/two-machine-trial`. These are source findings, not a
new playtest. No Unity, build, player, or networking test ran for this document.

## The answer and the size

**Main cannot run the campaign with a friend today.** The private East Water
trial shares boat presence and a clock so both computers can reconstruct the sea
and fish schools. It has no shared catch, hands, wallet, quests, or disk save.
Seeing another boat is the beginning of co-op, not a shared campaign.

My first choice is **two people working in the host's campaign**: one world,
one save on the host, one shared purse, separate hands and characters, and one
region at a time. When a suitable boat has been earned, one person can steer
while the other works aboard. Until then they work ashore. The friend keeps a
character in that campaign; goods and money do not travel back to another save.

The proposed eight-PR sequence costs **37–63 focused days of lane work**. The
first useful shared job arrives at PR 4, after **17–29 days**: catch or dig,
put the catch into a pail by hand, and carry it together to the wharf. That is
an in-memory rehearsal in a campaign location. PR 6, after **27–46 days**, is
the first persistent co-op fishing, delivery, sale, and resume loop. PRs 7–8
add travel and the shared opening/story, bringing the currently implemented
campaign arc together. This does not promise the future enterprise campaign.

One lane day means one focused implementation-and-validation day by one lane,
not a calendar day or a guaranteed completion date. Ranges include the tests
below and ordinary integration fixes. Review queues, CI waits, new art, and
unresolved product decisions are outside them. The save design must be reviewed
early even though its implementation is PR 6. A failed dependency stops that
slice; it does not justify shipping around a rule.

## Six choices to settle before implementation

These are decisions requested by the proposal, not decisions already granted.
The estimates assume every lean below.

| Choice | Options | My lean and its cost to the players |
|---|---|---|
| Whose campaign? | Equal partners in a jointly owned world; or a host's world which a friend visits. | **Host's world.** Both can do useful work, but the host controls admission and the world exists only while that host runs it. Equal ownership without the host would also need a hosting/ownership policy. |
| Where does progress live? | One shared save on the host; or each player keeps and merges progress into a personal campaign. | **One host save**, including the guest's campaign-specific character record. No portable money, fish, boat, or quest rewards. Separate progress needs conflict and duplication rules and is outside this estimate. |
| One boat or two? | Two people crew one owned boat; or each operates an independently owned boat. | **One crewed boat first**, with exclusive helm control. Both start ashore; no free second boat. Boarding requires authored space/support for two. Two active boats need independent holds, fuel, ownership, travel and restore records; the trial's two replicas do not supply those. |
| One wallet or two? | A shared purse; or separate balances and payment/trade rules. | **Shared purse**, spent through the host's ordered transactions. Both see who spent what. Personal licences/tools remain assigned to a player; a shared purse does not grant either player two copies. |
| When the friend is away? | Freeze campaign progression until both return; or let the host continue. | **Host may continue**; the friend catches up to world progress on return. No income accrues while the host world is closed. Planned departure is at a safe place after stowing goods. An unexpected drop retains the guest and goods in the host's world, as described below. |
| How far apart? | Stay in one region; or visit different regions freely. | **One region at a time**, with a consent/readiness gate at a passage. Players can separate inside it. Crossing waits for both at the passage or aboard the same boat; it never summons a distant player or cargo. Free travel requires multiple active region contexts and a different performance/save budget. |

Additional defaults: two invited participants, no host migration, no public
matchmaking, and local menus that do not pause the other person's work. Sleep
or fast-forward requires both connected players ready at valid rest points and
no unfinished transfer. The host alone advances time once. An absent player is
not a permanent veto, but their cargo continues to age with the world's clock.

## What the campaign contains on this main

Here **host** means the one authority that accepts an action and changes shared
state. **Per-player** means separately addressed state, still validated by the
host when it affects play. **Local reconstruction** means deriving the same
answer from agreed seed, game time and identical content. It does not mean
running two independent copies of purchases or quest rewards.

| System | Main today, with source | Proposed ownership; what breaks with two players |
|---|---|---|
| New Game, Continue, intro at sea | [ShellFlow](../../Assets/_Project/Code/Core/Shell/ShellFlow.cs) enters one world and applies one save. [ArrivalOpening](../../Assets/_Project/Code/App/ArrivalOpening.cs) handles one passenger, arrival flag and rest anchor; its [sea journey](../../Assets/_Project/Code/App/ArrivalOpening.Sea.cs) holds the hour and transfers between scenes. | **Host** selects New/Continue and runs one arrival state machine; **per-player** character creation, input and passenger positions. Two local openings would each steer the arrival boat, set flags and transfer scenes. Initially admit the guest only after landfall at a safe join point. PR 8 adds starting together aboard the same arrival boat, readiness/skip agreement and disconnect recovery; joining midway waits for a stable point. |
| Region travel and seams | [RegionSceneLoader](../../Assets/_Project/Code/World/RegionSceneLoader.cs) loads additively or reactivates an already loaded scene. [RegionTravelCoordinator](../../Assets/_Project/Code/App/RegionTravelCoordinator.cs) carries one player/boat rig. [RegionSeamGraph](../../Assets/_Project/Code/World/RegionSeamGraph.cs) audits authored passages and arrival keys. | **Host** commits one group crossing; **per-player** loading/camera cover. One global current region and pending arrival cannot describe two destinations. A guest loading slowly must not duplicate cargo or leave the host activating the next world alone. Tide/access checks remain authoritative at departure. |
| Clock, pause, sleep, fast-forward | [GameClock](../../Assets/_Project/Code/Environment/GameClock.cs) has hour and flow readings. `SeekTo` restores without replaying days; `FastForwardTo` emits each crossed day once. [RestSaveResponder](../../Assets/_Project/Code/Core/Save/RestSaveResponder.cs) advances to morning and stamps one rest anchor. | **Host** owns time, rate, pause, hold and sleep acceptance. **Local reconstruction** supplies tide/weather/schools. A guest must not seek the world, pause everyone with a menu, or settle the market a second time. Synchronize both clock readings; see the clock risk below. |
| SaveService and autosave | [SaveService](../../Assets/_Project/Code/Core/Save/SaveService.cs) writes on suspend/quit and explicit mutation/rest requests; title-state writes are blocked. [SaveStore](../../Assets/_Project/Code/Core/Save/SaveStore.cs) uses atomic replacement and retained copies. Although architecture prose mentions day-end autosave, this service has **no `DayStarted` subscription**. | **Host only** writes the campaign. Client pause/quit must never overwrite a local solo save with replicated state. A write must observe an entire committed transaction, not a debit before cargo removal. Preserve existing save-safety tests; add an explicit co-op checkpoint policy instead of assuming a periodic autosave exists. |
| Save format and resume | [SaveMigration](../../Assets/_Project/Code/Core/Save/SaveMigration.cs) is **v17** here. [SaveData](../../Assets/_Project/Code/Core/Save/SaveData.cs) has one money balance, active hull, character/name, notes and rest anchor, and aggregate hold/bucket/freezer lists. Held catch records carry species, count and freshness, not exact rolled size or item identity. | **Host world record plus per-player records**, with stable actor/boat/container/item identities. Current fields cannot restore two hands, two bodies, a guest's notes, or two distinct holds. The lead-architect owns any migration; this plan reserves no schema version and changes no format. |
| Hands, inventory, personal gear | [CarryHands](../../Assets/_Project/Code/Player/CarryHands.cs) uses Core [HandSlots](../../Assets/_Project/Code/Core/Interaction/HandSlots.cs); [GameServices](../../Assets/_Project/Code/Core/Services/GameServices.cs) exposes one hands/catch-hands/player-hold context. Portable world pails and held-object placement are not a complete persistent inventory. | **Per-player** hands, gear, licences and appearance; **host** grants one owner/location per object. The last enabled hands component must not become everyone's hands. Introduce actor-addressed Core requests and local-player views, rather than swapping global services per packet. Two claims on one pail produce exactly one winner. |
| Catch, carrying and freshness | [FishingController](../../Assets/_Project/Code/Fishing/FishingController.cs) can still fall through to a hold; [ClamDig](../../Assets/_Project/Code/Fishing/ClamDig.cs) has a bucket fallback. `CatchLanded` and `FishCaught` distinguish hand landing from storage. [ShipHold](../../Assets/_Project/Code/Boats/ShipHold.cs) is one aggregate. The [goods proposal](goods-handling/03-data-model-proposal.md) describes future item/container records, not a shipped complete solution. | **Host** resolves a catch once, its exact size/freshness and each transfer; **per-player** hands; **local reconstruction** of undisturbed schools only. Two digs at one spent hole, duplicate landing messages or both players lifting one catch must not mint fish. No network shortcut from sea to hold or boat to counter: lift, carry, stow, unload, deliver. Visible contents must follow accepted state. |
| Buying, selling, wallet and living market | [Market](../../Assets/_Project/Code/Economy/Market.cs) keeps category supply in memory and recovers it on day rollover; [SellService](../../Assets/_Project/Code/Economy/SellService.cs) removes catch, changes supply and pays the wallet. [WharfSellPoint](../../Assets/_Project/Code/Economy/WharfSellPoint.cs) can sell a carried pail via [CarriedHold](../../Assets/_Project/Code/Economy/CarriedHold.cs), retaining the empty pail; wired-hold and screen paths also exist. Market supply is not represented in the audited save DTO. | **Host** owns wallet, stock, prices, purchases and sale commits. Prices depend on sale history as well as time; seed alone cannot reconstruct them. Two simultaneous sales need ordered quotes and stale-quote refusal/reconfirmation, not two payments at the old price. Every enabled co-op purchase/delivery must respect physical goods. PR 6 must persist the relevant market history/state. |
| Boats owned and crewed | [OwnedFleet](../../Assets/_Project/Code/Boats/OwnedFleet.cs) buys/restores by hull id and swaps one active controller/hold. [HelmSlot](../../Assets/_Project/Code/Core/Boats/HelmSlot.cs) arbitrates the local player's piloted hull. Deck presentation occupancy exists; it is not a network crew roster. | **Host** owns boat instances, fuel, cargo and helm claims; **per-player** crew position/input. One steers at a time; another may work if deck capacity permits. Duplicating `OwnedFleet` would swap the same hold or clone an owned hull. A guest must never submit an authoritative hull purchase or transform. |
| NPC routines and working boats | [RoutinePlanner](../../Assets/_Project/Code/World/Routines/RoutinePlanner.cs) derives schedules using seed; [VillagerRoutine](../../Assets/_Project/Code/World/Routines/VillagerRoutine.cs) samples the day. [ConversationHold](../../Assets/_Project/Code/World/Routines/ConversationHold.cs) is a temporary interruption over that schedule. NPC fishing/landing activity also exists. | **Local reconstruction** of untouched schedules; **host** owns interaction reservations and economic effects. Two speakers cannot each claim the same clerk's attention. Conversation holds must be replicated, then return to the shared schedule. Any NPC landing that affects goods/market commits on the host once, not once on each computer. |
| Quests, onboarding and dialogue | [QuestBoard](../../Assets/_Project/Code/World/QuestBoard.cs) derives progress from flags through [SaveFlagStore](../../Assets/_Project/Code/World/SaveFlagStore.cs). [OnboardingDirector](../../Assets/_Project/Code/World/OnboardingDirector.cs) reads flags and player facts. [DialogueModel](../../Assets/_Project/Code/World/DialogueModel.cs) and current request signals lack a campaign participant context. | **Host** owns shared story flags and one-time world grants; **per-player** reading/choices and tutorial acknowledgements. A quest view recomputes from acknowledged flags; rewards do not recompute. Concurrent choices must not pay twice or close the other's conversation. Inventory/permission checks refer to the acting player, not whichever global player was installed last. |
| Notebook UI | [NotebookContentSource](../../Assets/_Project/Code/World/NotebookContentSource.cs) combines quest flags, authored knowledge and one ordered `PlayerNotes` list; [NotebookPresenter](../../Assets/_Project/Code/World/NotebookPresenter.cs) is a local singleton. | **Per-player** page, input and private notes; **local reconstruction** of quest/knowledge pages from shared acknowledged facts. UI stays local. Sharing the one notes list loses authorship or edits; opening a book must not seize the other player's controls. Persist guest notes in their host-world record, without broadcasting private text. |
| Business and automation | The design covers processing, hiring, wages and freight. Main has an isolated fixed-step [FishChainModel](../../Assets/_Project/Code/Economy/SupplyChain/FishChainModel.cs) using Core physical-work receipts; this audit found no runtime construction of it in the game modules. It has no scene binding/save writer. That is not a playable, saved management campaign. | **Host** would own jobs, staff assignments, stores and payments; **local reconstruction** only for derivable schedules. Double-running shifts doubles output/wages, and stock edits bypass hauling. Integrate only when its roadmap slice is approved; the eight PRs do not build or network the future enterprise layer. |

The [canon](../vision-and-pillars.md) still requires earned boats and manual work;
the [roadmap](../roadmap.md#05-where-we-are--snapshot-2026-09-01-regenerate-at-every-coordinator-handoff)
records the rescope and later owner-directed work. Old milestone examples are
not evidence that a system is currently implemented. Likewise, the older
[goods audit](goods-handling/00-today.md) predates the carried-pail counter path;
the source at this document's audit base governs the table above.

## The contract the trial needs before main

Reuse the trial's Netcode/transport setup as a candidate, its endpoint validation,
two-participant admission cases, boat/character identity serialization, and
join/disconnect test scenarios. Reuse the experiment's lesson that each screen
needs a distinct local view and another participant's presence. Re-run those
tests on the accepted implementation; their existence is not proof of a run.

Rebuild the following boundaries rather than cherry-picking the entire trial:

- `CoopBoatReplica` lets the owner write boat/presentation state. Campaign
  clients send **intent**, and the host validates movement, helm occupancy,
  reach, inventory and spending before publishing results. Smooth remote motion
  locally; do not trust a client to award a catch or move a loaded boat anywhere.
- The spike assembly references several feature modules. Put session/actor
  contracts and accepted-action events through **Core/EventBus**, with feature
  adapters and composition in App. Do not turn networking into another module
  that calls every feature's concrete classes, or broadcast the entire EventBus
  back into itself. UI notices and authoritative state changes are different.
- Trial boot supplies an in-memory `ISaveService` and bypasses the normal
  opening. Main needs explicit Host/Join/Leave states, compatibility checks,
  a client save facade that cannot write, and the ordinary shell/restore path.
  Tests use disposable fixtures, never a player's live save.
- Use stable Def ids for content and separate instance ids for players, boats,
  catches and containers. Allocate mutable identities from an agreed persistent
  sequence, never wall-clock time or hidden global randomness. Tune tick/rate,
  timeouts, packet limits and tolerances in configuration. Match protocol,
  build/content fingerprint and seed before accepting actions. Identity
  declarations follow the additions mechanism; the frozen ledger stays frozen.
- Every action carries actor identity, ordering and a retry key. The host checks
  expected object revisions and commits inventory, money and flags together.
  Retrying a committed action returns its result without executing again;
  stale/invalid actions change nothing. Join/rejoin installs one snapshot and
  only newer changes before enabling input. Persist the relevant retry boundary
  with a durable checkpoint so reconnect or reload cannot repeat a payment.

Local reconstruction is suitable for environmental fields and unperturbed
schedules. Catch rolls also need an agreed action context/order; human actions
are not derivable from seed and time alone. Unity boat physics is not promised
to produce identical trajectories on two computers. Simulate authoritative
boats on the host and measure any client prediction/correction.

On an unexpected guest disconnect, stop accepting their inputs and release
interaction/helm claims. Keep their actor and held goods at the authoritative
support/boat-relative pose; retain the container contents and freshness. Never
auto-sell, remotely unload, or teleport them home. The host can recover goods
only through normal nearby hand actions. Rejoin attaches to that same record.
An orderly departure stows first; group travel waits until the absent actor's
goods and position can cross legitimately. If the host quits, the session ends;
if it crashes, both resume the last complete checkpoint. No host election or
merge of a guest's speculative state is proposed.

## PR sequence and acceptance gates

All rows are proposed future work. PRs 1–5 remain opt-in rehearsals using an
isolated in-memory campaign fixture; no progress from them is advertised as
saved. Unsupported purchases, travel, story effects and other verbs are refused
until their row is ready, including on the host. Production solo play keeps its
existing path. Each slice must be small enough to review; if a row needs several
commits/PRs, retain its gate and charge that work to its estimate.

| PR / order | Lane days | Player result and boundary | Required proof before that gate passes |
|---|---:|---|---|
| **1. Transport and session menu** | **3–5** | Host/Join/Leave an opt-in two-person rehearsal; sensible connection errors and no save writes. Choose the supported network path explicitly. | EditMode admission/compatibility cases: wrong build/content, malformed payload, third player, unauthorized actor, duplicate join, failed start. PlayMode lifecycle: leave/rejoin without two service roots or any solo-save write. Transport harness exercises delay/loss/disconnect. |
| **2. Shared seed and clock** | **2–4** | Both read the same sea/day. Local menus stay local; pause/hold/sleep requests go through the host. Sleep UI may remain unavailable until safe-rest integration. | Extend clock hold/day-boundary tests for hour **and** flow, rate, late join during a hold, stale samples and wrong seed. Compare environment/school samples at agreed instants. Fast-forward crosses each midnight exactly once on the authority; client refresh causes no second sale recovery or reward. |
| **3. Player presence and one crewed boat** | **5–8** | Two independently controlled bodies; exclusive helm and supported boarding/standing on one legitimately owned hull. Separate boat instances/actor contexts replace the single-player assumptions needed here. | Core tests: two helm claims yield one holder; permissions and disconnect release; distinct player services cannot overwrite each other. PlayMode: shore/aboard handoff, late join, hull swap and guest loss retain support/identity. Two-machine moving-deck proof and art-lane visual acceptance are release dependencies. |
| **4. Hands, catch and carry** | **7–12** | First useful joint job: gather/fish, visibly stow, take turns carrying the same pail and deliver it to the wharf. No sale/save yet. Include the minimal runtime goods ownership/transfer adapters; existing proposals/tool tests alone do not supply them. | EditMode: competing pickup/dig/haul, duplicate landing, full hands/storage, out-of-reach transfer and late join preserve exact item count, mass and freshness. Rejected transfer changes nothing. PlayMode: water/flat → hand → pail → supported placement; re-pickup, disconnect and aboard transfer. All enabled paths obey manual carrying; unsupported gear paths stay unavailable. Art lane verifies held objects and visible fill. |
| **5. Economy** | **4–7** | Carry the pail to a buyer, sell, retain it empty, spend the shared purse on supported existing offers. New physical goods appear at a reachable hand/pickup point and must be carried. | Race two purchases against limited funds and two sales against the same pail: one valid result per action, no negative balance, no duplicated item. Retry and stale-quote cases preserve exact wallet/stock totals; marginal prices match ordered sales and freshness. Remote boat-hold sale is refused. Client and host show the same completed receipt. |
| **6. Host saves and resume** | **6–10** | First persistent co-op loop: fish/dig, hand-deliver, sell, quit and return as the same two characters. Host may continue solo with the guest record retained. Initially one region, guest admitted after landfall. | Lead-architect-reviewed schema/migration; temp-fixture old-save migration and repeat-load idempotence. Round-trip both players, goods locations/sizes/freshness, shared wallet, boat and market supply. Crash/fault at transaction/write boundary gives a complete before-or-after state. Guest disk remains untouched on pause/quit. Restore hands/boat support, reconnect retry boundaries and private notes; every relevant autosave trigger observes a committed snapshot. |
| **7. Travel together** | **4–7** | Walk the tide-gated route or sail a passage together, keeping carried pails and boat cargo attached to their real owners/supports. | Run existing seam/arrival audits plus a two-peer readiness barrier. Test one slow loader, failed destination, disconnect before/after commit, repeated crossing request, tide closing and re-entry to loaded scenes. Both remain in the old region on failure or reach the new one once; no cargo copy and no distant-player summon. |
| **8. Opening, quests, dialogue and Notebook** | **6–10** | Start a fresh campaign aboard together, reach shore, complete the existing opening arc and see shared tasks; each keeps their own reading position and notes. | New/Continue and intro-at-sea tests with two passengers, agreed skip, held-clock join and disconnect across landfall. Concurrent dialogue choices/quest grants commit once; host-only progress is reflected on rejoin. Note edits stay per-player and survive restore. Full two-machine new-game → gather → cross → buy/repair → sail → rest/resume acceptance, using only currently available content. |

The first **real task** is PR 4. The first **saved campaign evening** is PR 6.
The first **start-to-current-story co-op acceptance** is PR 8. Do not call an
earlier gate a completed campaign merely because the connection is green.

Estimates assume a private direct connection on a suitable LAN, matching the
trial's transport scope. If friends need an ordinary internet invite without
router setup, propose a separate connection-service choice and **4–7 additional
lane days** for invites, identity binding, reconnect/error handling and tests
(**41–70 days** total). Provider charges and platform requirements need separate
evaluation; no service or credentials are chosen here. A LAN success is not
internet-access proof.

## Risks, ownership and stop conditions

**Save compatibility is the largest structural risk.** A boat Def id names a
type, not an independently owned vessel, and a species/count list cannot restore
a specific fish in a specific guest's hand. The lead-architect must agree the
world/participant/instance boundary with the goods work before PR 3 relies on
it. Implement the minimal records at PR 6, preserving old single-player saves
and retained-copy/atomic-write guarantees. Do not split competing goods and
co-op schemas. The [goods migration proposal](goods-handling/03-data-model-proposal.md#migration-plan-owned-by-lead-architect)
already identifies lost legacy weight and unsaved hand/world-pail state; those
facts cannot be recovered by networking. No schema number is reserved here.

**Clock agreement needs more than the trial's packet.** `CoopSeaState` carries
seed, seconds, rate and pause, but no flow reading or hour-hold state. Main's
[clock contract](../../Assets/_Project/Code/Core/Services/IGameClock.cs) lets flow
advance during an intro hour hold. Repeated `SeekTo` would equalize the two and
erase that distinction. Prefer an atomic host hour/flow/rate/hold sample through
a reviewed Core seam, with local interpolation for display. Loading a save may
equalize flow as main already does; joining an ongoing held session must not
reset the host's sea. Never save weather or introduce a second authoritative
clock. Transport timeouts may use elapsed real time; simulation outcomes may not
use the remote machine's wall-clock date or a hidden random source.

**The typical laptop still owes 60 fps.** Target about 16.7 ms per frame with two
actors, one boat and the ordinary water/NPC workload. There is no measured
headroom claim in this plan. Compare solo and host/client frame-time percentiles,
allocations, memory, bytes per second, snapshot size and correction frequency on
the intended laptop. Pool network objects, avoid per-frame allocations, keep
economic work on a slow tick, send changes and relevant entities, and recompute
the sea. If the extra CPU/network work breaks the budget, reduce its rate/work
or narrow scope; do not lower the world's visual quality as a networking fix.
Free regional travel is deliberately outside this budget.

**The trial's floating held rod remains a visual dependency.** The charter
reports a rod above the figure and assigns its main fix to an art lane. This
audit did not run or judge that view and does not claim the fix has landed.
Consume the accepted main hand/standing contract; do not preserve the trial's
ad hoc visual offset as a gameplay attachment. At PRs 3–4 the art lane must check
both screens on deck, ashore and while carrying, including visible storage fill.
No rig, sprite, shader, terrain, animation or plate baseline changes belong to
this proposal. Missing supported poses block the affected experience, not the
remaining logic work.

**Scope and ownership remain gates.** Networking contracts, save schema and
composition need lead-architect review; gameplay owns boats/hands/fishing,
economy owns transaction/market rules, world owns travel/story, UI owns the
shell/Notebook, and QA owns the integrated acceptance. Integrate accepted work
from those areas rather than editing another active lane's scene or art.
Business/staff/freight automation stays in its roadmap phase. New network
permission rules do not authorize later-phase mechanics.

## Ranked recommendation and the remaining empirical question

1. **Choose host-world visits, same region, shared purse and one crewed boat.**
   Agree the six choices and schema boundary, then fund through PR 4 with a
   two-machine checkpoint after PR 3. If working aboard feels sound, continue
   to the saved loop at PR 6 and the current campaign arc at PR 8.
2. **Keep the trial separate if those constraints are unacceptable.** It can
   remain a boat outing while solo campaign work continues; it must be labelled
   as having no shared progression. This costs less than pretending separate
   inventories or unsaved cargo are campaign co-op.
3. **Defer equal independent worlds, transferable progress, two independent
   fleets and unrestricted region travel.** They require a new estimate after
   identity, goods ownership, save migration and laptop performance are proven.

The one fact only the intended **two-machine test** can settle is: **does working
together on the moving boat feel usable to the guest over the actual connection?**
Replay tests can prove conservation and ordering; a same-process test cannot
establish that experience. At the PR 3 checkpoint, drive, board and walk aboard
from both screens, then repeat with carrying at PR 4; record connection latency,
guest corrections and both machines' frame times alongside the visible result.
This document makes no claim that that acceptance has passed.
