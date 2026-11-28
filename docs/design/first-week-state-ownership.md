# First-week state ownership

Two friends need one island, clock, stock of goods and working fleet, with separate bodies,
hands, notebooks and places to sleep. Main still connects many of those personal facts to
one global player or one flat save. Duplicating the character would therefore let one
player overwrite the other's state. This audit identifies those connections and adds a
source guard against new instances of the same patterns; it does not add multiplayer.

Source baseline: `c626cea10d1cb496df41cca2f9ab2f580ad47b28`, save version 25. **WORLD** means
one campaign, including objects keyed by their own identity. **PLAYER** means separately
addressable for each participant, even when a single campaign authority stores it. Stored
means action-dependent state; it does not imply that the state currently survives a reload.
Recomputed state also requires the same authored definitions and configuration, not just
the same two numbers.

The first week and demo stay on St Peters: arrival, lodging, clams, shore fishing, buying
the broken dory at the island slip, repairing her by hand, launching and fishing aboard.
An islander's word opens the way around day seven; weather closes other regions until then.
This is the current direction, not a claim that the pinned code already implements every
beat. The older geography and paid repair paths below are recorded as debts, not endorsed.
The existing [shared-business ruling](multiplayer-scoping.md#611-the-shared-purse-ruling-owner-2026-08-04)
keeps purse, licences and fleet with the outfit. The later
[campaign proposal](campaign-coop-plan.md) is explicitly unaccepted; its personal-licence
and same-region assumptions do not override that ruling or separate-region bunk eligibility.

## State by system

Paths in this table are relative to `Assets/_Project/Code/` unless otherwise stated.

| System | Where it lives today: file and type | Computed or stored today | Scope as written today | Scope for two, and why |
|---|---|---|---|---|
| Clock and calendar | `Environment/GameClock.cs`: `GameClock._t`, `_flow`; `Core/Save/SaveData.cs`: `GameTimeSeconds`, `DayIndex`, `WorldSeed` | Clock is mutable and saved; day/calendar derive from it. `_flow` is presentation time and is reset on seek. | WORLD clock; pause/hold and needs ticking assume one local player. | WORLD time advanced once. Personal menus must not pause a friend's day; sleep advances only with the agreed quorum. |
| Tide and tidal access | `Environment/TideModel.cs`: `Height`, `CycleIndex`; `Environment/EnvironmentService.cs`: active tide/current profiles | Pure time + profile/config; tide itself does not use the seed and is not saved. | WORLD, with one active-region environment context. | WORLD samples by region and position. A personal tide would let two people disagree about exposed ground. |
| Weather | `Environment/WeatherModel.cs`: `Sample`; `Core/Environment/LocalWeather.cs`; `World/Campaign/CampaignBarrierDirector.cs` | Baseline wind/sea/fog derive from seed + time + regional profiles; local/campaign overrides also depend on authored facts and progress. | WORLD, but active region is selected globally. | WORLD regional weather, same seed/time; evaluate the region each player occupies. Do not save sampled weather. Pending weather work is outside this audit's edits. |
| Shelf stock | `Economy/Island/IslandStoreShelves.cs`: `_draws`, `_ledger`, `TryTakeOne`; `IslandStoreLedger`, `IslandShelvesHost`; `Core/IslandSupply/IslandShelvesDirectory.cs` | Deliveries/use are computed from the route, calendar/weather and data. Player withdrawals are session history in `_draws`; seed/time alone cannot reproduce purchases. No v25 shelf-withdrawal field. | WORLD per shop/line, registered once; no buyer id. | WORLD, one ordered withdrawal for the last item. Each line empties independently. Preserve action history in a later save step. |
| Sold-out signs | `World/Shops/SoldOutSigns.cs`: `SoldOutSignRule.IsSoldOut`, `SoldOutSigns.Refresh` | Derived from the same shelf count; no saved sign bit. Visibility uses one building's `IsInside`. | WORLD stock truth, single-player view selection. | WORLD sold-out truth with a view for each player. Buying the last unit changes both players' reading, never creates a second stock pool. |
| Purse and sale value | `Player/PlayerWallet.cs`: `_money`, `TrySpend`; `Economy/SellService.cs`, `Market.cs`; `Core/Services/GameServices.cs`: `Wallet`; `SaveData.Money` | Balance saved. Market supply changes with sales and recovers with days; not reconstructed from seed/time alone and not saved in v25. | One PLAYER-named wallet used as the campaign purse. | WORLD outfit purse under the existing ruling. Serialize debit, goods handoff and receipt together; neither player may spend the same remaining money twice. |
| Supplies, bait and tools | `Core/Save/SupplyLocker.cs`, `Core/Save/PotLocker.cs`; `Economy/BaitShop.cs`, `Fishing/PlacedTrapService.cs`; `Player/PlayerGear.cs`; `SaveData.SupplyStock`, `BaitStock`, `PotStock`, `OwnedGear` | Stored counts/presence; purchase and use mutate them. These legacy wallets do not express physical location. | WORLD flat lists used as one PLAYER's inventory. | WORLD physical item/container stock, PLAYER possession/access. Shared purchase does not give both players a held shovel or ice bag; transport remains by hand. |
| Shopping bag | No shopping-bag type on the pinned main. `Economy/BuyCatalog.cs` and `IslandCounterShelves` still link counter purchase to shelf depletion. | No v25 bag contents or carrier record. The protected shelf/bag change is pending. | Absent; current supply grant goes to the flat stock. | WORLD bag instance with contents, at most one PLAYER carrying it. Buy at the shelf into a reachable paper bag, carry and put away by hand. Integrate the pending bag rule when it lands; do not invent a parallel bag. |
| Ordinary and limited licences | `Economy/LicenseService.cs`: singleton `_held`; `LicenseVendor`, `LicenseDef`; `SaveData.OwnedLicenses`; `docs/design/limited-licences-and-crewing.md` | Owned ordinary ids saved. Finite licence holders, leases and seasonal tags are design, not implemented ownership records here. | One global licence set, effectively WORLD outfit scope. | WORLD outfit entitlements by current ruling; future finite licence instances must remain conserved globally. Who may fish under another holder's licence needs the crewing policy, not copied licence rows. |
| Catch eligibility | `Fishing/CatchReleaseRule.cs`, `UnlicensedCatch.cs`; `Economy/CatchLicensePolicy.cs`; `Core/Economy/ILicenseService.cs` | Decision from species/rules and held licences; releases do not grant physical inventory. | Uses the global licence service and actorless catch signals. | Evaluate the acting PLAYER's permitted use of WORLD licences. Every catch needs cover; an uncovered catch is let go, not blocked from being caught. |
| Loose catch in hand | `Player/CarryHands.cs`: per-component `HandSlots`, `_lastTaken`; `CarriableCatch`; `Core/Services/GameServices.cs`: `Hands`, `CatchHands` | Session object and hand slots. No general loose-hand-catch save field. | PLAYER components exposed through one global PLAYER slot. | PLAYER hands holding one WORLD item instance at a time. Transfer changes custody once; it must not duplicate the catch or increment the catch record twice. |
| Pail catch | `Player/ClamBucket.cs`: `_items`, `SyncToSave`; `SaveData.BucketCatch` | Action history plus freshness clocks saved in one list. | One PLAYER pail in a WORLD flat field. | WORLD pail/container id and contents; PLAYER carrier. A second pail cannot bind to the same `BucketCatch` list. |
| Stored catch | `Boats/ShipHold.cs`, `Player/GinnyFreezer.cs`, `Core/Save/HoldCatchCodec.cs`; `SaveData.BoatHoldCatch`, `FreezerCatch`; `Core/Save/StoredContainers.cs` | Catch/freshness stored, spoil settled from elapsed game time. `StoredContainers` already supports keyed locations for its consumers, but does not replace every legacy hold. | WORLD physical holds, with one legacy boat-hold and freezer slot and global `PlayerStorage` access. | WORLD containers keyed by instance, with explicit access and reach. Ownership and carrier identity are different; neither allows remote unloading or sale. |
| Clam and bar-clam beds | `Fishing/ClamDig.cs`: `_spent`, `_spentAtSeconds`, `_id`; `ClamHoleLedger`; `SaveData.SpentClamHoles`; `Data/Fish/AtlanticSurfClam.asset` supplies the bar-clam species | Yield/spurt timing use seed/time/hole facts; depletion is saved by hole id and consumed tide cycle. `SameTide` controls reuse. No separate bar-clam ownership service found. | WORLD hole depletion; dig inputs/hand destination are global PLAYER seams. | WORLD holes/beds: one successful take per available hole for both players. Separate tools/reach/licence checks per actor; keep spent identity across region visits. |
| Set traps | `Fishing/PlacedTrapService.cs`: `_live`; `PlacedTrap`, `PlacedTrapCatch`; `SaveData.PlacedTraps`, `BaitStock`, `PotStock` | Placement facts saved; catch/soak recomputed from seed, placement identity/time and current time. DTO has no player owner. | WORLD placements, one outfit's counted pots and bait. | WORLD trap instances and depletion/haul operations. A player claims work on a trap; access policy may be shared, but two hauls cannot both consume its catch. |
| Dory ownership and repair | `Boats/OwnedFleet.cs`; `Economy/Shipwright.cs`, `RepairLedger`; `SaveData.OwnedBoats`, `RepairedBoats`, `ActiveHullId` | Hull ids and a repaired/not-repaired flag saved. Main's paid-repair record has no hand-work stages. | WORLD roster interpreted through one PLAYER's active hull. | WORLD single earned dory and repair progress. Both can contribute by hand; no second dory or second repair completion. Stage persistence requires a future approved migration. |
| Where the dory lies | `Boats/BoatController.cs`: live transform; `OwnedFleet` swaps one runtime hull; `Core/Save/RestLocker.cs`: `RestBoat*` | Authored placement then live motion; only the sleep anchor carries a particular boat pose in the audited save. No general per-hull world pose table. | WORLD object, one active-boat/rest-pose assumption. | WORLD position/support for the physical dory. St Peters slip placement is current direction; this change neither relocates a scene nor claims the older yard flow has moved. |
| Who is aboard and at the helm | `Player/ControlSwitcher.cs`: `_playerWalk`, mode/deck state; `Boats/OwnedFleet.cs`: `_aboard`; `Core/Boats/HelmSlot.cs` | Session state/events, not an occupancy roster in v25. | One PLAYER's mode controls one active helm/hull. | PLAYER location/support, WORLD occupant set per hull and exclusive helm holder. Being aboard is not permission to overwrite the helm. Physical support for two needs later scene validation. |
| Notebook CAUGHT | `Core/Fishing/CatchRecord.cs`: `CatchRecordLog`, static `CatchRecords.Log`; `SaveData.CaughtSpecies`; `World/NotebookContentSource.cs`: `Caught` | Saved counts, released counts and best weight; transient handoff deduplication uses frame ticks. | One PLAYER record, merged by actorless events and one session log. | PLAYER achievements; a friend's catch should not silently become his own. Preserve catcher identity through handoff, storage and sale. Frame-based deduplication is not a multiplayer transaction id. |
| Notebook TASKS and written notes | `World/NotebookContentSource.cs`: `Notes`, `Flags`; `NotebookContent`, `QuestBoard`; `SaveData.PlayerNotes`, `OnboardingFlags` | Personal text/order/checkmarks saved; authored quest sections derived from Defs + flags. | One PLAYER note list; quest facts from one WORLD flag list. | PLAYER free text/task checkmarks, WORLD shared campaign objectives with personal read/acknowledgement where needed. One player ticking a private reminder must not edit the other's note. |
| Story flags | `World/OnboardingFlags.cs`, `SaveFlagStore`; `Core/Save/SaveService.cs`: `GetFlag`, `SetFlag`; `SaveData.OnboardingFlags` | Saved key/value facts. Mixed facts: boat purchase and first catch, meeting and reading. | WORLD one flag namespace representing one PLAYER's experience. | WORLD purchase/repair/route opening; PLAYER met/read/tutorial acknowledgement. Classify each key explicitly; a shared route unlock must not claim both heard the same conversation. |
| Sleep, rest and saving | `Core/Save/SleepFlow.cs`, `RestQuorum`, `RestSaveResponder`, `RestLocker`, `SleepPlace`, `SaveService`; `Core/Time/SleepPlan.cs` | Session sleep phase/bed claims; saved clock, one `Rest*` anchor and boat pose. Eight-hour cap already in `SleepPlan.RulingMaxHours`. Pause/quit/mutation writes also exist. | WORLD flow with `Players => 1`, `LocalSleeper = 0`, one request and one PLAYER anchor. | WORLD unanimous decision and one clock advance/write; PLAYER distinct bunk claims and rest anchors. Wheelhouse bench counts; regions may differ. Recheck occupancy when sleep starts and before time advances. Keep technical recovery writes distinct from the sleep checkpoint design. |
| Body, needs and identity | `Core/Services/GameServices.cs`: `Appearance`, `Needs`, `Body`, `Cleanliness`, `PlayerStrength`; `SaveData.PlayerName`, `PlayerRecipe`, `PlayerTraits`, `PlayerNeeds`, `PlayerBody`, `PlayerCleanliness`, `WornOutfitId` | Personal action history and identity saved; time settles needs/body. | One PLAYER in flat fields and global services. | PLAYER. Both sleep, eat and recover independently while the world clock is shared. Traits must affect only the acting player. |
| NPC routines | `World/Routines/RoutinePlanner.cs`, `RoutineSchedule`, `VillagerRoutine`; `Conversations/NpcConversationDirector.cs` | Seeded authored schedule + day/time/region; current conversation holds and daily run history are session state. | WORLD NPCs; presentation/director queries one current region. | WORLD one NPC timeline with explicit temporary conversation claims. Two players see the same NPC; different loaded regions cannot each advance a duplicate NPC. |
| Gifts and what an NPC remembers | `World/Gifts/GiftLedger.cs`: `Shared`, NPC/day counts; `GiftRule`, `GiftInventory`; `Core/Relationships/RelationshipGraph.cs`, `RelationshipService`; `World/WorldInteractor.cs` | `NpcGiftDays` saved; relationship graph/pending day events are session state, seeded from tie Defs, not v25 save fields. Dialogue encounter memories also use story flags. | Gift counts WORLD by NPC; relationships use one literal `RelationshipGraph.PlayerId`, so personal interactions collapse to one PLAYER. | WORLD NPC-to-NPC ties and world events; PLAYER-to-NPC memories keyed by both parties. Decide whether daily gift cap is per giver or NPC-wide. Preserve real delivery: access to all `PlayerStorage` is not permission to give distant goods. |

## Single-player assumptions and the guard

The existing `Assets/Tests/EditMode/ModuleBoundaryTests.cs` already guards the assembly
graph. It is extended as a partial class in `ModuleBoundaryTests.PlayerState.cs`, rather
than creating a second architecture fixture. Its named debt list is
`Assets/Tests/EditMode/Fixtures/FirstWeekPlayerStateKnownDebts.json`.

The scan covers every C# file below `Assets/_Project/Code`, including Core, feature and
composition modules; `Tools/` and path segments named `Editor` are excluded. It masks
comments and ordinary/verbatim string and character literals while preserving line
numbers. Declarations and lookups can span lines. Preprocessor branches are all scanned.

| Rule | Exact family of patterns guarded |
|---|---|
| SP001 | Static fields/properties whose type is one of the explicit actor/service/log types in `ActorTypes`: player controllers, hands, pail, wallet, appearance, needs/body/cleanliness/strength, catch/gift log, active boat, helm or licence service. Methods are excluded. |
| SP002 | Generic `FindObjectOfType`, `FindAnyObjectByType` or `FindFirstObjectByType` selecting one of those types, `ShipHold` or `DeckWalkController`; optional namespace qualification and whitespace are accepted. |
| SP003 | Nonstatic field declarations of `PlayerWalkController`, `ControlSwitcher`, `CarryHands` or `ClamBucket`, including serialized fields. A component's own field can be sound, but it still needs a named per-instance ownership explanation. |
| SP004 | Public flat fields in `Core/Save/SaveData.cs` named `Player*`, `Rest*`, `ActiveHullId`, `BucketCatch`, `CaughtSpecies`, `WornOutfitId`, `NavWaypoints`, `NavRoute`, `NavTrack` or `NpcGiftDays`. |
| SP005 | Executable references to `RelationshipGraph.PlayerId`. |
| SP006 | `RestQuorum.LocalSleeper`, literal `RestQuorum.IsMet(1)`, or a public static integer `Players => 1`. |

Each allowance has a stable name, rule, exact normalized token, exact file, exact occurrence
count ceiling and reason. New files, extra occurrences in an allowed file, and changed tokens fail
with `[PLAYER-STATE-SPnnn]` plus source and line. Removing a debt is allowed so independently
based fixes can land in either order; retire its allowance when the fixture is available in
that change. Reintroducing an identical retired occurrence cannot be distinguished while
its old allowance remains. No whole file/module is exempted. Scanner cases pin multiline declarations, modern/legacy discovery APIs,
comment/literal exclusion, explicit actor inputs, repeated tokens and source line reporting.

This is a bounded lexical guard, not a proof of multiplayer safety. New actor types,
aliases, reflection, nongeneric discovery and indirect singleton access require review;
extend the pattern families deliberately when new evidence appears. A single component
field is recorded conservatively, not declared inherently wrong. No allowance authorizes
changing a protected file. The code and tests were inspected without running Unity;
compilation and EditMode execution are pending CI.

### Additional ownership debt requiring semantic review

- `Core/Services/GameServices.cs:678,792,861`: one `PlayerTransform`, `CurrentRegionId`
  and `Whereabouts`. Environment selection, reach, travel and rest must accept the
  relevant actor/region instead of relying on whichever region was registered last.
- `Boats/OwnedFleet.cs:31,32,44`: one `_boat`, `_hold`, `_aboard`. A list of hull ids is
  not a fleet of physical instances, nor an occupant list.
- `Player/ClamBucket.cs:123`, `Boats/ShipHold.cs:84` (`SyncToSave`),
  `Player/GinnyFreezer.cs:174`: legacy single catch lists bind physical storage to global
  slots. `Core/Save/StoredContainers.cs:26` is the existing keyed-container seam to extend.
- `Core/Relationships/RelationshipGraph.cs:77`: literal `PlayerId = "player"`.
  The graph already supports arbitrary pairs; callers need actual player ids.
- `Core/Fishing/CatchRecord.cs:323` and `Core/Events/` catch signals: catches are
  recorded without an actor id. Splitting the log alone would still credit the wrong player.
- `World/NotebookContentSource.cs:88`, `World/SaveFlagStore.cs:22`:
  one note list and mixed personal/world flags, with no actor argument.
- `Core/Save/SleepFlow.cs:71,132`, `Core/Save/RestQuorum.cs:20,24`: one pending request,
  literal one-player quorum and one local sleeper. A successful chooser is not evidence
  that everyone is still lying down when time starts passing.
- `Core/Save/SaveService.cs:41,252`: one campaign DTO is appropriate; its flat personal
  fields are not. A later save step must introduce player identity/records, preserve the
  original player's data exactly, and store each world item and boat once. This audit adds
  no saved field, migration or new Def identity.

### Named known debts at the source baseline

Line numbers below refer to the pinned baseline. The executable allowances use token and
count instead, so inserting unrelated lines does not grant new permission.

| Debt / rule | Source at the audit base | Exact declaration or lookup | Why it needs review |
|---|---|---|---|
| D001 / SP001 | `Core/Fishing/CatchRecord.cs`: [275](../../Assets/_Project/Code/Core/Fishing/CatchRecord.cs#L275) | `public static CatchRecordLog Log` (1) | One process-wide actor slot or log; bind it to an explicit player context before two players use it. |
| D002 / SP001 | `Core/Services/GameServices.cs`: [348](../../Assets/_Project/Code/Core/Services/GameServices.cs#L348) | `private static IBodyService _body` (1) | One process-wide actor slot or log; bind it to an explicit player context before two players use it. |
| D003 / SP001 | `Core/Services/GameServices.cs`: [720](../../Assets/_Project/Code/Core/Services/GameServices.cs#L720) | `private static ICarrier _hands` (1) | One process-wide actor slot or log; bind it to an explicit player context before two players use it. |
| D004 / SP001 | `Core/Services/GameServices.cs`: [744](../../Assets/_Project/Code/Core/Services/GameServices.cs#L744) | `private static ICatchHands _catchHands` (1) | One process-wide actor slot or log; bind it to an explicit player context before two players use it. |
| D005 / SP001 | `Core/Services/GameServices.cs`: [402](../../Assets/_Project/Code/Core/Services/GameServices.cs#L402) | `private static ICleanlinessService _cleanliness` (1) | One process-wide actor slot or log; bind it to an explicit player context before two players use it. |
| D006 / SP001 | `Core/Services/GameServices.cs`: [332](../../Assets/_Project/Code/Core/Services/GameServices.cs#L332) | `private static INeedsService _needs` (1) | One process-wide actor slot or log; bind it to an explicit player context before two players use it. |
| D007 / SP001 | `Core/Services/GameServices.cs`: [300](../../Assets/_Project/Code/Core/Services/GameServices.cs#L300) | `private static IPlayerAppearance _appearance` (1) | One process-wide actor slot or log; bind it to an explicit player context before two players use it. |
| D008 / SP001 | `Core/Services/GameServices.cs`: [375](../../Assets/_Project/Code/Core/Services/GameServices.cs#L375) | `private static IPlayerStrength _playerStrength` (1) | One process-wide actor slot or log; bind it to an explicit player context before two players use it. |
| D009 / SP001 | `Core/Services/GameServices.cs`: [69](../../Assets/_Project/Code/Core/Services/GameServices.cs#L69) | `public static HelmSlot Helm` (1) | One process-wide actor slot or log; bind it to an explicit player context before two players use it. |
| D010 / SP001 | `Core/Services/GameServices.cs`: [56](../../Assets/_Project/Code/Core/Services/GameServices.cs#L56) | `public static IActiveBoatService ActiveBoat` (1) | One process-wide actor slot or log; bind it to an explicit player context before two players use it. |
| D011 / SP001 | `Core/Services/GameServices.cs`: [342](../../Assets/_Project/Code/Core/Services/GameServices.cs#L342) | `public static IBodyService Body` (1) | One process-wide actor slot or log; bind it to an explicit player context before two players use it. |
| D012 / SP001 | `Core/Services/GameServices.cs`: [714](../../Assets/_Project/Code/Core/Services/GameServices.cs#L714) | `public static ICarrier Hands` (1) | One process-wide actor slot or log; bind it to an explicit player context before two players use it. |
| D013 / SP001 | `Core/Services/GameServices.cs`: [738](../../Assets/_Project/Code/Core/Services/GameServices.cs#L738) | `public static ICatchHands CatchHands` (1) | One process-wide actor slot or log; bind it to an explicit player context before two players use it. |
| D014 / SP001 | `Core/Services/GameServices.cs`: [396](../../Assets/_Project/Code/Core/Services/GameServices.cs#L396) | `public static ICleanlinessService Cleanliness` (1) | One process-wide actor slot or log; bind it to an explicit player context before two players use it. |
| D015 / SP001 | `Core/Services/GameServices.cs`: [89](../../Assets/_Project/Code/Core/Services/GameServices.cs#L89) | `public static IHelmControl HelmControl` (1) | One process-wide actor slot or log; bind it to an explicit player context before two players use it. |
| D016 / SP001 | `Core/Services/GameServices.cs`: [124](../../Assets/_Project/Code/Core/Services/GameServices.cs#L124) | `public static IHelmInstruments HelmInstruments` (1) | One process-wide actor slot or log; bind it to an explicit player context before two players use it. |
| D017 / SP001 | `Core/Services/GameServices.cs`: [326](../../Assets/_Project/Code/Core/Services/GameServices.cs#L326) | `public static INeedsService Needs` (1) | One process-wide actor slot or log; bind it to an explicit player context before two players use it. |
| D018 / SP001 | `Core/Services/GameServices.cs`: [294](../../Assets/_Project/Code/Core/Services/GameServices.cs#L294) | `public static IPlayerAppearance Appearance` (1) | One process-wide actor slot or log; bind it to an explicit player context before two players use it. |
| D019 / SP001 | `Core/Services/GameServices.cs`: [369](../../Assets/_Project/Code/Core/Services/GameServices.cs#L369) | `public static IPlayerStrength PlayerStrength` (1) | One process-wide actor slot or log; bind it to an explicit player context before two players use it. |
| D020 / SP001 | `Economy/LicenseService.cs`: [25](../../Assets/_Project/Code/Economy/LicenseService.cs#L25) | `private static LicenseService _instance` (1) | One outfit licence service fits the existing shared-business ruling; personal or finite-holder policy would need explicit identity. |
| D021 / SP001 | `World/Gifts/GiftLedger.cs`: [25](../../Assets/_Project/Code/World/Gifts/GiftLedger.cs#L25) | `public static GiftLedger Shared` (1) | Gift counts are keyed only by NPC and day; separate personal relationships need a giver identity and an agreed shared cap. |
| D022 / SP002 | `App/LandPassageAccess.cs`: [15](../../Assets/_Project/Code/App/LandPassageAccess.cs#L15) | `FindFirstObjectByType<ControlSwitcher>` (1) | Scene-wide first/any lookup chooses an arbitrary actor; resolve the requesting player or addressed container. |
| D023 / SP002 | `App/PersistentHoldProxy.cs`: [29](../../Assets/_Project/Code/App/PersistentHoldProxy.cs#L29) | `FindAnyObjectByType<ShipHold>` (1) | Scene-wide first/any lookup chooses an arbitrary actor; resolve the requesting player or addressed container. |
| D024 / SP002 | `Player/CamperFreezer.cs`: [166](../../Assets/_Project/Code/Player/CamperFreezer.cs#L166) | `FindAnyObjectByType<ClamBucket>` (1) | Scene-wide first/any lookup chooses an arbitrary actor; resolve the requesting player or addressed container. |
| D025 / SP002 | `Player/CarriableBucket.cs`: [335](../../Assets/_Project/Code/Player/CarriableBucket.cs#L335) | `FindAnyObjectByType<CarryHands>` (1) | Scene-wide first/any lookup chooses an arbitrary actor; resolve the requesting player or addressed container. |
| D026 / SP002 | `Player/CarriableFuelContainer.cs`: [309](../../Assets/_Project/Code/Player/CarriableFuelContainer.cs#L309) | `FindAnyObjectByType<CarryHands>` (1) | Scene-wide first/any lookup chooses an arbitrary actor; resolve the requesting player or addressed container. |
| D027 / SP002 | `Player/CarriableTool.cs`: [307](../../Assets/_Project/Code/Player/CarriableTool.cs#L307) | `FindAnyObjectByType<CarryHands>` (1) | Scene-wide first/any lookup chooses an arbitrary actor; resolve the requesting player or addressed container. |
| D028 / SP002 | `Player/GinnyFreezer.cs`: [189](../../Assets/_Project/Code/Player/GinnyFreezer.cs#L189) | `FindAnyObjectByType<ClamBucket>` (1) | Scene-wide first/any lookup chooses an arbitrary actor; resolve the requesting player or addressed container. |
| D029 / SP002 | `Player/StowageRack.cs`: [511](../../Assets/_Project/Code/Player/StowageRack.cs#L511) | `FindAnyObjectByType<CarryHands>` (1) | Scene-wide first/any lookup chooses an arbitrary actor; resolve the requesting player or addressed container. |
| D030 / SP002 | `Player/WetBucketPoint.cs`: [97](../../Assets/_Project/Code/Player/WetBucketPoint.cs#L97) | `FindAnyObjectByType<ClamBucket>` (1) | Scene-wide first/any lookup chooses an arbitrary actor; resolve the requesting player or addressed container. |
| D031 / SP003 | `App/ArrivalOpening.cs`: [294](../../Assets/_Project/Code/App/ArrivalOpening.cs#L294) | `private PlayerWalkController _walk` (1) | One actor reference is wired here today; an additional actor needs its own instance or explicit actor input. |
| D032 / SP003 | `App/LandPassageAccess.cs`: [10](../../Assets/_Project/Code/App/LandPassageAccess.cs#L10) | `private ControlSwitcher _switcher` (1) | One actor reference is wired here today; an additional actor needs its own instance or explicit actor input. |
| D033 / SP003 | `App/RegionTravelCoordinator.cs`: [33](../../Assets/_Project/Code/App/RegionTravelCoordinator.cs#L33) | `private ControlSwitcher _switcher` (1) | One actor reference is wired here today; an additional actor needs its own instance or explicit actor input. |
| D034 / SP003 | `Player/CamperFreezer.cs`: [45](../../Assets/_Project/Code/Player/CamperFreezer.cs#L45) | `private ClamBucket _bucket` (1) | One actor reference is wired here today; an additional actor needs its own instance or explicit actor input. |
| D035 / SP003 | `Player/CarriableBucket.cs`: [72](../../Assets/_Project/Code/Player/CarriableBucket.cs#L72) | `private CarryHands _hands` (1) | One actor reference is wired here today; an additional actor needs its own instance or explicit actor input. |
| D036 / SP003 | `Player/CarriableFuelContainer.cs`: [72](../../Assets/_Project/Code/Player/CarriableFuelContainer.cs#L72) | `private CarryHands _hands` (1) | One actor reference is wired here today; an additional actor needs its own instance or explicit actor input. |
| D037 / SP003 | `Player/CarriableTool.cs`: [76](../../Assets/_Project/Code/Player/CarriableTool.cs#L76) | `private CarryHands _hands` (1) | One actor reference is wired here today; an additional actor needs its own instance or explicit actor input. |
| D038 / SP003 | `Player/CarryHands.cs`: [73](../../Assets/_Project/Code/Player/CarryHands.cs#L73) | `private PlayerWalkController _walk` (1) | One actor reference is wired here today; an additional actor needs its own instance or explicit actor input. |
| D039 / SP003 | `Player/ControlSwitcher.cs`: [52](../../Assets/_Project/Code/Player/ControlSwitcher.cs#L52) | `private PlayerWalkController _playerWalk` (1) | One actor reference is wired here today; an additional actor needs its own instance or explicit actor input. |
| D040 / SP003 | `Player/DeckRiderMeshPresenter.cs`: [184](../../Assets/_Project/Code/Player/DeckRiderMeshPresenter.cs#L184) | `private PlayerWalkController _walk` (1) | One actor reference is wired here today; an additional actor needs its own instance or explicit actor input. |
| D041 / SP003 | `Player/GinnyFreezer.cs`: [52](../../Assets/_Project/Code/Player/GinnyFreezer.cs#L52) | `private ClamBucket _bucket` (1) | One actor reference is wired here today; an additional actor needs its own instance or explicit actor input. |
| D042 / SP003 | `Player/PlayerFixturePosePresenter.cs`: [56](../../Assets/_Project/Code/Player/PlayerFixturePosePresenter.cs#L56) | `private PlayerWalkController _walk` (1) | One actor reference is wired here today; an additional actor needs its own instance or explicit actor input. |
| D043 / SP003 | `Player/PlayerSleepPresenter.cs`: [50](../../Assets/_Project/Code/Player/PlayerSleepPresenter.cs#L50) | `private PlayerWalkController _walk` (1) | One actor reference is wired here today; an additional actor needs its own instance or explicit actor input. |
| D044 / SP003 | `Player/RodFightPresenter.cs`: [288](../../Assets/_Project/Code/Player/RodFightPresenter.cs#L288) | `private CarryHands _hands` (1) | One actor reference is wired here today; an additional actor needs its own instance or explicit actor input. |
| D045 / SP003 | `Player/StowageRack.cs`: [71](../../Assets/_Project/Code/Player/StowageRack.cs#L71) | `private CarryHands _hands` (1) | One actor reference is wired here today; an additional actor needs its own instance or explicit actor input. |
| D046 / SP003 | `Player/WetBucketPoint.cs`: [49](../../Assets/_Project/Code/Player/WetBucketPoint.cs#L49) | `private ClamBucket _bucket` (1) | One actor reference is wired here today; an additional actor needs its own instance or explicit actor input. |
| D047 / SP004 | `Core/Save/SaveData.cs`: [351](../../Assets/_Project/Code/Core/Save/SaveData.cs#L351) | `public CharacterRecipe PlayerRecipe` (1) | The flat save has one personal record; a future migration must key it by player or physical container. |
| D048 / SP004 | `Core/Save/SaveData.cs`: [445](../../Assets/_Project/Code/Core/Save/SaveData.cs#L445) | `public List<CaughtSpeciesDto> CaughtSpecies` (1) | The flat save has one personal record; a future migration must key it by player or physical container. |
| D049 / SP004 | `Core/Save/SaveData.cs`: [105](../../Assets/_Project/Code/Core/Save/SaveData.cs#L105) | `public List<HeldCatchDto> BucketCatch` (1) | The flat save has one personal record; a future migration must key it by player or physical container. |
| D050 / SP004 | `Core/Save/SaveData.cs`: [187](../../Assets/_Project/Code/Core/Save/SaveData.cs#L187) | `public List<NavRoutePointDto> NavRoute` (1) | The flat save has one personal record; a future migration must key it by player or physical container. |
| D051 / SP004 | `Core/Save/SaveData.cs`: [192](../../Assets/_Project/Code/Core/Save/SaveData.cs#L192) | `public List<NavTrackPointDto> NavTrack` (1) | The flat save has one personal record; a future migration must key it by player or physical container. |
| D052 / SP004 | `Core/Save/SaveData.cs`: [182](../../Assets/_Project/Code/Core/Save/SaveData.cs#L182) | `public List<NavWaypointDto> NavWaypoints` (1) | The flat save has one personal record; a future migration must key it by player or physical container. |
| D053 / SP004 | `Core/Save/SaveData.cs`: [451](../../Assets/_Project/Code/Core/Save/SaveData.cs#L451) | `public List<NpcGiftDayDto> NpcGiftDays` (1) | Gift counts are keyed only by NPC and day; separate personal relationships need a giver identity and an agreed shared cap. |
| D054 / SP004 | `Core/Save/SaveData.cs`: [210](../../Assets/_Project/Code/Core/Save/SaveData.cs#L210) | `public List<PlayerNoteDto> PlayerNotes` (1) | The flat save has one personal record; a future migration must key it by player or physical container. |
| D055 / SP004 | `Core/Save/SaveData.cs`: [385](../../Assets/_Project/Code/Core/Save/SaveData.cs#L385) | `public List<string> PlayerTraits` (1) | The flat save has one personal record; a future migration must key it by player or physical container. |
| D056 / SP004 | `Core/Save/SaveData.cs`: [405](../../Assets/_Project/Code/Core/Save/SaveData.cs#L405) | `public PlayerBodyDto PlayerBody` (1) | The flat save has one personal record; a future migration must key it by player or physical container. |
| D057 / SP004 | `Core/Save/SaveData.cs`: [426](../../Assets/_Project/Code/Core/Save/SaveData.cs#L426) | `public PlayerCleanlinessDto PlayerCleanliness` (1) | The flat save has one personal record; a future migration must key it by player or physical container. |
| D058 / SP004 | `Core/Save/SaveData.cs`: [395](../../Assets/_Project/Code/Core/Save/SaveData.cs#L395) | `public PlayerNeedsDto PlayerNeeds` (1) | The flat save has one personal record; a future migration must key it by player or physical container. |
| D059 / SP004 | `Core/Save/SaveData.cs`: [317](../../Assets/_Project/Code/Core/Save/SaveData.cs#L317) | `public bool RestAboard` (1) | The flat save has one personal record; a future migration must key it by player or physical container. |
| D060 / SP004 | `Core/Save/SaveData.cs`: [313](../../Assets/_Project/Code/Core/Save/SaveData.cs#L313) | `public double RestWokeAtSeconds` (1) | The flat save has one personal record; a future migration must key it by player or physical container. |
| D061 / SP004 | `Core/Save/SaveData.cs`: [328](../../Assets/_Project/Code/Core/Save/SaveData.cs#L328) | `public float RestBoatPosX` (1) | One sleeper embeds one boat pose; future saves need per-player rest anchors and one shared pose per hull instance. |
| D062 / SP004 | `Core/Save/SaveData.cs`: [331](../../Assets/_Project/Code/Core/Save/SaveData.cs#L331) | `public float RestBoatPosY` (1) | One sleeper embeds one boat pose; future saves need per-player rest anchors and one shared pose per hull instance. |
| D063 / SP004 | `Core/Save/SaveData.cs`: [334](../../Assets/_Project/Code/Core/Save/SaveData.cs#L334) | `public float RestBoatRotation` (1) | One sleeper embeds one boat pose; future saves need per-player rest anchors and one shared pose per hull instance. |
| D064 / SP004 | `Core/Save/SaveData.cs`: [288](../../Assets/_Project/Code/Core/Save/SaveData.cs#L288) | `public float RestPosX` (1) | The flat save has one personal record; a future migration must key it by player or physical container. |
| D065 / SP004 | `Core/Save/SaveData.cs`: [291](../../Assets/_Project/Code/Core/Save/SaveData.cs#L291) | `public float RestPosY` (1) | The flat save has one personal record; a future migration must key it by player or physical container. |
| D066 / SP004 | `Core/Save/SaveData.cs`: [299](../../Assets/_Project/Code/Core/Save/SaveData.cs#L299) | `public int RestLevel` (1) | The flat save has one personal record; a future migration must key it by player or physical container. |
| D067 / SP004 | `Core/Save/SaveData.cs`: [44](../../Assets/_Project/Code/Core/Save/SaveData.cs#L44) | `public string ActiveHullId` (1) | One selected hull conflates fleet state with the player at its helm; select per player and store boats by instance. |
| D068 / SP004 | `Core/Save/SaveData.cs`: [354](../../Assets/_Project/Code/Core/Save/SaveData.cs#L354) | `public string PlayerName` (1) | The flat save has one personal record; a future migration must key it by player or physical container. |
| D069 / SP004 | `Core/Save/SaveData.cs`: [309](../../Assets/_Project/Code/Core/Save/SaveData.cs#L309) | `public string RestBedId` (1) | The flat save has one personal record; a future migration must key it by player or physical container. |
| D070 / SP004 | `Core/Save/SaveData.cs`: [321](../../Assets/_Project/Code/Core/Save/SaveData.cs#L321) | `public string RestBoatHullId` (1) | One sleeper embeds one boat pose; future saves need per-player rest anchors and one shared pose per hull instance. |
| D071 / SP004 | `Core/Save/SaveData.cs`: [325](../../Assets/_Project/Code/Core/Save/SaveData.cs#L325) | `public string RestBoatRegion` (1) | One sleeper embeds one boat pose; future saves need per-player rest anchors and one shared pose per hull instance. |
| D072 / SP004 | `Core/Save/SaveData.cs`: [283](../../Assets/_Project/Code/Core/Save/SaveData.cs#L283) | `public string RestRegion` (1) | The flat save has one personal record; a future migration must key it by player or physical container. |
| D073 / SP004 | `Core/Save/SaveData.cs`: [232](../../Assets/_Project/Code/Core/Save/SaveData.cs#L232) | `public string WornOutfitId` (1) | The flat save has one personal record; a future migration must key it by player or physical container. |
| D074 / SP005 | `World/WorldInteractor.cs`: [555](../../Assets/_Project/Code/World/WorldInteractor.cs#L555) | `RelationshipGraph.PlayerId` (1) | The literal player relationship endpoint merges two people; supply the acting player identity. |
| D075 / SP006 | `Core/Save/RestQuorum.cs`: [24](../../Assets/_Project/Code/Core/Save/RestQuorum.cs#L24) | `public static int Players => 1` (1) | The local sleep facade assumes one participant; a session roster must decide the quorum. |
| D076 / SP006 | `Core/Save/SleepFlow.cs`: [132](../../Assets/_Project/Code/Core/Save/SleepFlow.cs#L132) | `RestQuorum.IsMet(1)` (1) | The local sleep facade assumes one participant; a session roster must decide the quorum. |
| D077 / SP006 | `Core/Save/SleepFlow.cs`: [90](../../Assets/_Project/Code/Core/Save/SleepFlow.cs#L90), [209](../../Assets/_Project/Code/Core/Save/SleepFlow.cs#L209), [238](../../Assets/_Project/Code/Core/Save/SleepFlow.cs#L238) | `RestQuorum.LocalSleeper` (3) | The local sleep facade assumes one participant; a session roster must decide the quorum. |
