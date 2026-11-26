# Dormant shop stock path audit

The old shop stock service does not run in the game at this snapshot. Only tests construct it. Its one shop asset describes three lines of goods, but the live wares book buys from catalog listings through vendor components and never asks that service for a count. Removing the service would therefore not, by itself, change a live purchase. Removing its data indiscriminately would break content checks: the newer island shelves still refer to the old shop identity, and a test requires their line ids to differ from the old ones.

The newer island ledger and shelves are also uncomposed at this snapshot. They are a different stock model, not an adapter for the old one: bulk goods, tide-dependent runs and intraday consumption replace fixed weekdays and immediate minimum-stock replenishment. Their quantities and line ids cannot be substituted directly. Keep the shop's stable identity, reconcile the stock rules and purchase units, then retire the unused machinery in small changes. This audit changes no code or assets.

## Snapshot and scope

Audited commit: `832c6f78f9e607cfbdede4845a3d205551439c91`. Source locations below refer to that commit. The ledger and shelf additions from PRs 1456 and 1473 are present. On 2026-11-23, open PR 1480 was changing the shelf host, purchase gating, shelf definitions and presentation; PR 1474 was a separate shelf-data audit, and PR 1230 concerned vehicle sales and identity checks. Their work is not treated as shipped here. Recheck their final state before implementing a retirement.

The search covered tracked C#, Markdown, JSON, asset, prefab and scene text for every old type named below, then the shop script/asset GUIDs and stable ids. It found 111 literal type-reference lines in 16 files. Reflection-based identity discovery was inspected separately. Scene and prefab checks were targeted text searches, not scene loads. “No runtime reader” below means no reachable composition or consumer found in this source snapshot; no editor, player, profiler, Unity import, EditMode or PlayMode run was performed. This is static evidence, not an executed gameplay proof.

## Source key

Tables use `key:line` so every reference resolves to a file without repeating long paths. Line lists name distinct relevant reads or assertions, not a claimed execution count.

| Key | File |
|---|---|
| Def | [Assets/_Project/Code/Economy/ShopDef.cs](../../Assets/_Project/Code/Economy/ShopDef.cs) |
| Spec | [Assets/_Project/Code/Economy/ShopStockSpec.cs](../../Assets/_Project/Code/Economy/ShopStockSpec.cs) |
| Service | [Assets/_Project/Code/Economy/ShopStockService.cs](../../Assets/_Project/Code/Economy/ShopStockService.cs) |
| Restock | [Assets/_Project/Code/Economy/ShopRestock.cs](../../Assets/_Project/Code/Economy/ShopRestock.cs) |
| State | [Assets/_Project/Code/Economy/MemoryShopStockStateStore.cs](../../Assets/_Project/Code/Economy/MemoryShopStockStateStore.cs) |
| Core | [Assets/_Project/Code/Core/Economy/IShopStock.cs](../../Assets/_Project/Code/Core/Economy/IShopStock.cs) |
| Rules | [Assets/Tests/EditMode/Economy/ShopStockRulesTests.cs](../../Assets/Tests/EditMode/Economy/ShopStockRulesTests.cs) |
| Content | [Assets/Tests/EditMode/Economy/ShopStockContentTests.cs](../../Assets/Tests/EditMode/Economy/ShopStockContentTests.cs) |
| Headlamp | [Assets/Tests/EditMode/HeadlampGearDefTests.cs](../../Assets/Tests/EditMode/HeadlampGearDefTests.cs) |
| Shipped | [Assets/Tests/EditMode/IslandSupply/IslandShelvesShippedTests.cs](../../Assets/Tests/EditMode/IslandSupply/IslandShelvesShippedTests.cs) |
| Identity | [Assets/Tests/EditMode/DefIdentityContractTests.cs](../../Assets/Tests/EditMode/DefIdentityContractTests.cs) |
| Pilot | [Assets/_Project/Data/Shops/StPetersGeneral.asset](../../Assets/_Project/Data/Shops/StPetersGeneral.asset) |
| NewDef | [Assets/_Project/Code/Economy/Island/IslandStoreShelvesDef.cs](../../Assets/_Project/Code/Economy/Island/IslandStoreShelvesDef.cs) |
| NewShelves | [Assets/_Project/Code/Economy/Island/IslandStoreShelves.cs](../../Assets/_Project/Code/Economy/Island/IslandStoreShelves.cs) |
| NewAsset | [Assets/_Project/Data/IslandLedgers/StPetersStoreShelves.asset](../../Assets/_Project/Data/IslandLedgers/StPetersStoreShelves.asset) |
| StockDoc | [docs/design/shop-stock-rules.md](../design/shop-stock-rules.md) |
| LivingDoc | [docs/design/st-peters-living-economy.md](../design/st-peters-living-economy.md) |

## Reachability and type inventory

`ShopStockService` has no Unity lifecycle or automatic registration. Its two construction sites are `Rules:45,223`. All stock reads, takes, returns, ticks and notifications remain inside this isolated implementation and its tests. There is no `IShopStock` service registration, event subscriber, save adapter, scene component, Resources loader or catalog hook for it. The default publisher at `Service:31` is not evidence of a live publisher: the constructor is never reached by shipped composition. The test factory supplies its own notification action.

| Type and declaration | Code references and behavior | Tests, assets and documents |
|---|---|---|
| `ShopDef` — Def:9 | Owns `Spec`; `BuildSpec` at Def:14 validates seller membership, allowed listing kind and pack size, then snapshots. NewDef:41 names it only in a documentation comment. No runtime loader found. | Content:17,23,32,47,49; Headlamp:62,67; Shipped:53. Pilot:12–15 binds the script. Identity discovers `Spec.Id` reflectively. StockDoc:7,105; LivingDoc:176. |
| `ShopStockSpec` — Spec:50 | Def:11,14; Service:17,24,32,38. Validation and deep copy at Spec:59–115. | Rules:31,43 and their tests; Content:25,34,52. Pilot:15–57. StockDoc:7,31–34. |
| `ShopShelfSlots` — Spec:16 | Spec:55,65,81–86 declares, copies and validates logical capacities. No physical fixture binding. | Rules:34,264; Content:41; Pilot:19–25; StockDoc:22–26. |
| `ShopStockLine` — Spec:24 | Spec:56,66,90–113; Service:18,43,86,97,103; Restock:32,41,49. | Rules:35 and subsequent stock operations; Content:36–40,54–60; Shipped:55,64. Pilot:26–53; StockDoc:12–26,48–58. |
| `ShopDeliveryDays` — Spec:8 | Bitmask held by Spec:32; counted by Restock:12–28; checked by Spec:102–105. Values: None=0, Monday=1, Tuesday=2, Wednesday=4, Thursday=8, Friday=16, Saturday=32, Sunday=64. | Rules:39,155,164,307; Content:40. Pilot:33,42,51 stores mask 9. StockDoc:36–44. |
| `ShopCredit` — Spec:40 | Spec:57,66,78 copies it and rejects anything except disabled/zero. No lending, debt balance or payment behavior. | Rules:311 rejects enabling it; Pilot:54–57. StockDoc:27,72. |
| `ShopStockService` — Service:12 | Implements Core stock operations; snapshots specs; settles on reads and explicit ticks; stores only below-full records. No production constructor call. | Rules:43–45,223, with behavioral coverage through Rules:253. StockDoc:31–58; LivingDoc:107,205. No serialized service. |
| `ShopRestock` — Restock:7 | Service:64–65,89. Counts elapsed delivery days without walking the whole interval; clamps capacity/floor; accepts but does not use `worldSeed`. | Rules:140,142,155,165–166. StockDoc:36–52. No Def asset of its own. |
| `MemoryShopStockStateStore` — State:8 | Only implementation of `IShopStockStateStore`; dictionary access and export, no serializer. | Rules:43,45,172,189,203,213,223. StockDoc:62–81. No asset or save records. |
| `IShopStockView` — Core:6 | `CountOf`, `IsSoldOut`; inherited by Core:12. Implemented by Service:52–53, with no external production consumer. | Tests invoke the concrete service at Rules:52–56 and throughout. StockDoc:31. |
| `IShopStock` — Core:12 | Adds committed take/return and explicit tick; implemented by Service:12,55–56,74. | Rules:51–70,86–117 and later scenarios. StockDoc:31,54–58. |
| `IShopStockStateStore` — Core:22 | `TryRead`, `Write`, `Remove`, `CopyTo`; Service:15,25,44,88,99–100; State:8,13–30. | Rules:174,177–183,190–216 exercise its concrete implementation. StockDoc:32,62–81. |
| `ShopStockRecord` — Core:31 | Value passed through Core:24–27, Service:100, State:10–27. Not a save DTO. | Rules:174,190,203,213,215–216; StockDoc:68–81 describes future persistence; LivingDoc:230,236 refers to it in a proposal. |
| `ShopStockChange` — Core:44 | `Taken`, `Returned`, `Restocked`; used by Core:52,54 and Service:70,93. | Rules:96 checks Restocked; StockDoc:48–52 describes notifications. No serialized enum field. |
| `ShopStockChanged` — Core:47 | Service:20,25,31,69,93 creates/publishes changes; no production listener found. | Rules:44,88,96–99 captures/asserts events. StockDoc:51. No asset. |

The literal symbol search outside these six implementation files found only NewDef's comment, the four named test files, the pilot asset, two identity-addition JSON files and the two design documents. The identity addition for the pilot is [the GUID registration:2](../../Assets/Tests/EditMode/Fixtures/DefIdentityAdditions/af26710f2ad847ce92076059b02c578a.json#L2); its type declaration is [the ShopDef type registration:2](../../Assets/Tests/EditMode/Fixtures/DefIdentityAdditions/HiddenHarbours.Economy.ShopDef.type.json#L2). Neither is runtime registration. The generic identity reader combines those additions with the baseline, as described in [the additions README:3](../../Assets/Tests/EditMode/Fixtures/DefIdentityAdditions/README.md#L3); Identity:109–141 checks them against imported assets. No backlog reference to these type names was found.

## Every old field and its readers

None of the fields below is read by composed gameplay at this commit. “Reader” means a dormant implementation read, validation/copy operation, or test. Unity can import the pilot's serialized fields in the editor; that does not compose the service. The shared copy paths are Def:29 and Spec:59–67; `ShopShelfSlots`, `ShopStockLine` and `ShopCredit` use `MemberwiseClone` at Spec:20,35,45.

### Authored fields

`StockDoc:7–27` describes the authoring fields as a group; its floor, schedule and persistence semantics are at lines 36–81. `LivingDoc:59,176` still describes the old lines as store stock. These are documentation consumers, not reads by the game.

| Field declaration | Code readers beyond the declaration/copy | Test references; serialized pilot location |
|---|---|---|
| `ShopDef.Spec` — Def:11 | Def:20–29; generic identity lookup at Identity:74–85 | Content:34,52; Headlamp:69; Shipped:53,55; Pilot:15 |
| `ShopStockSpec.Id` — Spec:52 | Spec:73; Service:39,43–44,82 | Content:35; Rules:222–237,310; Shipped:53; Identity:79–85,109–123; Pilot:16 |
| `SellerId` — Spec:53 | Spec:74,107; Def:20 supplies catalog membership callback | Rules:273–277; Content:58; Pilot:17 |
| `PremisesId` — Spec:54 | Spec:75 validates spelling; no lookup of a real premises | Rules:33 supplies it; Pilot:18. No dedicated premises-resolution assertion found. |
| `Shelves` — Spec:55 | Spec:76,81–86 builds fixture capacity lookup | Rules:34,264; Content:41; Pilot:19–25 |
| `Stock` — Spec:56 | Spec:76,90–113; Def:21; Service:41,82 | Content:36–40,54–60; Headlamp:69; Shipped:55; Rules:35 and all stock scenarios; Pilot:26–53 |
| `Credit` — Spec:57 | Spec:78 rejects null/nonempty credit | Rules:311; Pilot:54–57 |
| `ShopShelfSlots.FixtureId` — Spec:18 | Spec:83,85–86 supplies the lookup used by line validation | Rules:34,264,281–288; Pilot:20,22,24 |
| `SlotCount` — Spec:19 | Spec:83,86,99 checks positive capacity and bounds | Rules:34,264,304; Content:41; Pilot:21,23,25 |
| `ShopStockLine.Id` — Spec:26 | Spec:93–94; Service:43–44,69,82,88,93,99–100 | Rules:37,224–228,263,284,309; Shipped:55,64; Pilot:27,36,45 |
| `ListingId` — Spec:27 | Spec:95,107; Def:23,27 resolves the real offer | Rules:37,78,262,274–276,284; Content:36,59; Headlamp:69; Pilot:28,37,46 |
| `FixtureId` — Spec:28 | Spec:97,99,111 validates membership and overlap | Rules:37,261,287; Pilot:29,38,47 |
| `FirstSlot` — Spec:29 | Spec:98–99,112–113 checks range and overlap; service never uses it | Rules:37,281–288,304; Pilot:30,39,48 |
| `MaxOnShelf` — Spec:30 | Spec:98–100,112–113; Service:88,91,99; Restock:36–37,52 | Rules:38,163,235; Content:37; Pilot:31,40,49 |
| `UnitsPerItem` — Spec:31 | Spec:100; Def:26 checks portable listing pack size; service never converts units | Rules:38,306; Content:38,54–56; Pilot:32,41,50 |
| `DeliveryWeekdays` — Spec:32 | Spec:102–104; Restock:34 then 12–28 | Rules:39,146–166,307; Content:40; Pilot:33,42,51 |
| `ItemsPerDelivery` — Spec:33 | Spec:104; Restock:37 multiplies delivery count | Rules:38,163,308; Pilot:34,43,52 |
| `Floor` — Spec:34 | Spec:100; Restock:36,45 implements immediate replenishment | Rules:38,74–82,203–204,235,305; Content:39; Pilot:35,44,53 |
| `ShopCredit.Enabled` — Spec:42 | Spec:78 requires false | Rules:311 tests true refusal; Pilot:55 = 0 |
| `MinimumLifetimeSales` — Spec:43 | Spec:78 requires zero; never counts sales | Default zero in Rules:31's spec, checked through validation; Pilot:56 = 0. No separate mutation test found. |
| `Ceiling` — Spec:44 | Spec:78 requires zero; never limits credit | Same validation path; Pilot:57 = 0. No separate mutation test found. |

### Service, state and event fields

These fields have no Def asset values. The dictionaries, clocks, watermark and event payloads are created in memory only. StockDoc:31–81 documents the intended behavior; it does not supply a save implementation.

| Field declaration | Reads/writes and relevant tests |
|---|---|
| `ShopStockService._clock` — Service:14 | Assigned at 28, read at 112; Rules:86–117,208–216 advances/rewinds its test clock. |
| `_state` — Service:15 | Assigned at 29; read/updated at 44,88,99–100; Rules:170–216 checks sparse records and restore. |
| `_worldSeed` — Service:16 | Assigned at 30, passed at 89; Restock:32 accepts it but does not read it. Rules:121–142 tests replay; no seeded delivery jitter exists. |
| `_shops` — Service:17 | Allocated at 32, filled at 40, traversed at 79–82; Rules:220–237 covers shop isolation and snapshot independence. |
| `_lines` — Service:18 | Filled at 43, looked up at 105; Rules:220–228 covers independent keys and unknown ids. |
| `_notify` — Service:20 | Assigned at 31, invoked at 69,93; Rules:88–104 captures events and count/payload. |
| `_observedDay` — Service:21 | Initial -1; read at 113, updated at 115; Rules:208–216 rejects rewind. |
| `_slowTickDay` — Service:22 | Initial -1; read/updated at 77–78; Rules:86–117,241–253 covers repeat ticks and warm allocation. |
| `MemoryShopStockStateStore._records` — State:10 | Lookup at 14; replace at 21; remove at 24; enumerate at 30. Rules:174–183,189–216 checks export/read/removal and invalid data. |
| `ShopRestock.DaysPerWeek` — Restock:9 | Derived from `Weekday.Sunday + 1`; used at 17–25. Rules:146–166 enumerates all nonempty masks and long intervals. |
| `ShopStockRecord.ShopId` — Core:33 | Constructor Core:40; State:18,21 keys/validates it. Rules:190,203,213–216 supplies it; no standalone property assertion. |
| `LineId` — Core:34 | Core:40; State:18,21 keys/validates it. Same record tests as ShopId. |
| `Count` — Core:35 | Core:40; State:19 rejects negative; Service:45,89,91,93 settles it. Rules:178,215 asserts count/refusal. |
| `SettledThroughDay` — Core:36 | Core:40; State:19; Service:45,89,91 checks future/negative days and advances once. Rules:179,194,213,216. |
| `ShopStockChanged.ShopId` — Core:49 | Assigned at Core:56; Rules:97 reads it. No game subscriber. |
| `LineId` — Core:50 | Assigned at Core:56; Rules:98 reads it. No game subscriber. |
| `Count` — Core:51 | Assigned at Core:56; Rules:99 reads it. No game subscriber. |
| `Reason` — Core:52 | Assigned at Core:56 from the enum; Rules:96 reads it. No game subscriber. |

## Assets carrying old values

There is exactly one serialized `ShopDef`: Pilot. Its script GUID is `0dc15eaef94948dd8e3403e5cdcbba6f` (Pilot:12; `ShopDef.cs.meta:2`), and its asset GUID is `af26710f2ad847ce92076059b02c578a` (`StPetersGeneral.asset.meta:2`). The GUID search found no scene or prefab reference to the pilot. It is outside `Resources`. Content:17–25 separately pins the exact one-asset inventory, though that test was not run here.

Pilot:16–18 stores `shop.st_peters_general`, `seller.leblancs`, `premises.st_peters_general`. Its three logical fixtures and three stock lines are:

| Stock line / listing | Fixture / slots | First slot | Maximum | Units/object | Weekday mask | Objects/delivery | Floor | Asset lines |
|---|---|---|---|---|---|---|---|---|
| `line.rod` / `gear.rod` | `fixture.rod_rack` / 4 | 0 | 4 | 1 | 9 (Mon + Thu) | 4 | 0 | Pilot:20–21,27–35 |
| `line.capelin` / `bait.capelin` | `fixture.bait_shelf` / 20 | 0 | 20 | 10 | 9 (Mon + Thu) | 20 | 1 | Pilot:22–23,36–44 |
| `line.ice` / `supply.ice` | `fixture.ice_chest` / 20 | 0 | 20 | 1 | 9 (Mon + Thu) | 20 | 1 | Pilot:24–25,45–53 |

Credit is disabled and both numeric fields are zero (Pilot:54–57). There are 44 logical slots, no coordinates or physical anchors. `PremisesId` is a reserved string, not a resolved building reference. No second shop, wholesaler or credit Def carries this old spec.

The referenced offers carry live price and seller data, not old stock quantities: [Rod.asset:15–25](../../Assets/_Project/Data/Resources/Catalog/Gear/Rod.asset#L15) has price 60 and two catalog sellers; [Capelin.asset:15–27](../../Assets/_Project/Data/Resources/Catalog/Bait/Capelin.asset#L15) has unit price 2; [Ice.asset:15–25](../../Assets/_Project/Data/Resources/Catalog/Supplies/Ice.asset#L15) has price 6. Capelin's omitted serialized `LotSize` takes the code default 10 at [BaitDef.cs:41](../../Assets/_Project/Code/Economy/BaitDef.cs#L41), also pinned by Content:38. Its live lot price uses `BaitShop.LotPriceOf`, not any old stock field.

NewAsset:16 is a string reference to the same shop id, not a Unity object reference to Pilot. NewAsset:21–40 instead contains `line.grocery_box` (5 units, maximum 18), `line.ice_bag` (5, 20), `line.bait_box` (2.5, 20) and `line.gas_jerry_can` (20, 4). These are `IslandShelfLine` fields, not serialized copies of `ShopStockLine`. Shipped:53–64 deliberately checks the shop exists and these line ids do not reuse the old three.

## The actual purchase and newer stock paths

The live source path is `CatalogBookPresenter` → `BuyCatalog` → the appropriate vendor. [CatalogBookPresenter.cs:127,198](../../Assets/_Project/Code/Economy/CatalogBookPresenter.cs#L127) builds and confirms rows. [BuyCatalog.cs:105–115](../../Assets/_Project/Code/Economy/BuyCatalog.cs#L105) resolves loaded vendor components by seller; lines 155–159 sweep catalog listings; lines 196–269 quote offers; lines 288–309 dispatch purchases. [CatalogSource.cs:70–92](../../Assets/_Project/Code/Economy/CatalogSource.cs#L70) loads `Resources/Catalog` and filters `Catalog.IsStockedBy`. None reads `ShopDef` or either stock simulation.

For example, [GearShop.cs:73–106](../../Assets/_Project/Code/Economy/GearShop.cs#L73) checks ownership, spends the offer price, records ownership and publishes the purchase. [BaitShop.cs:92–120](../../Assets/_Project/Code/Economy/BaitShop.cs#L92) spends the lot price and adds the lot to the player's tackle box. These operations are not a shelf-stock decrement. [BuyLogic.cs:60–81](../../Assets/_Project/Code/Economy/BuyLogic.cs#L60) gates by ownership/money, not available shelf count. “Stock” in `BuyCatalog.CatalogStock` means eligible listings; the book's pot/bait/supply stock notes describe player holdings (BuyCatalog:315,339,358).

NewShelves:99–106 constructs an `IslandStoreLedger` from its definition and then the shelves. The only calls to this factory or the new model constructors outside their own implementation are tests. The Core `IIslandShelves` contract likewise has no composed consumer at this base. NewShelves:118–141 can count and take goods if a caller supplies it, but no live buy path does so here. NewShelves:163–199 derives shelf objects from bulk stock after intraday use and player draws. Its comment at lines 15–19 and draw list at 42,127–141 explicitly describe session-only purchases.

[IslandStoreLedger.cs:70–110](../../Assets/_Project/Code/Economy/Island/IslandStoreLedger.cs#L70) uses route capacity, cadence, buffer, lookback and a run-possible predicate; it does not use `ShopDeliveryDays` or the old state store. A five-kilogram ice bag and a 2.5-kilogram bait box cannot be assumed to mean one catalog ice purchase or ten capelin: [island Ice.asset:18](../../Assets/_Project/Data/Resources/IslandSupply/Goods/Ice.asset#L18) and [Bait.asset:18](../../Assets/_Project/Data/Resources/IslandSupply/Goods/Bait.asset#L18) use kilograms, while the old pack validation counts purchase units. There is no listing-to-new-line field in NewDef:10–25 at this base.

## Other duplicated or overlapping economy paths encountered

| Overlap | Evidence and distinction | Retirement implication |
|---|---|---|
| Two count/restock models for the same shop | Service:52–100 uses weekday additions and a takeable floor; NewShelves:118–199 uses bulk ledger levels, intraday use and session draws. Old bait/ice can be taken indefinitely at floor 1 (Restock:45; Rules:74–82); new shelves refuse when less than an item remains (NewShelves:133). | This is a rule conflict, not a rename. Choose the depletion/essentials rule and exact units before connecting or substituting either path. |
| Catalog membership, a shop's stock subset, and scene-wired offers | [CatalogListing.cs:106–135](../../Assets/_Project/Code/Economy/CatalogListing.cs#L106) names sellers; Spec:53,56 names one seller and three shelf listings; [GearShop.cs:20,26,56,65](../../Assets/_Project/Code/Economy/GearShop.cs#L20) retains an inspector offer and both no-argument and catalog-selected buy entrypoints. The latter share the same purchase method. | The book uses catalog membership; old stock is only a validated subset. Do not replace the book's seller list with the three-line pilot or delete direct offer wiring without a separate caller/scene audit. |
| Several seller identity representations | `CatalogListing.Sellers`, vendor `_sellerId`, Spec.SellerId and [SellerDef.cs:10–16](../../Assets/_Project/Code/Economy/SellerDef.cs#L10) repeat seller keys for different jobs. [SellerDirectory.cs:12–26](../../Assets/_Project/Code/Economy/SellerDirectory.cs#L12) resolves keeper/trade identity; it does not choose catalog wares or count stock. | Preserve one stable seller key; extend [SellerContentTests.cs:13–74](../../Assets/Tests/EditMode/Economy/SellerContentTests.cs#L13) for consistency, not a second authoritative wares list. |
| Buy quote versus purchase price | BuyCatalog:209,231,240 quotes the offer or bait lot. GearShop:90 and BaitShop:108–110 charge it. [BaitShop.cs:59–62](../../Assets/_Project/Code/Economy/BaitShop.cs#L59) is the shared lot calculation; Def:25 independently mirrors its minimum-one pack rule for validation. | No copied price was found in the old model. Keep the price on the offer, migrate pack validation alongside any unit mapping, and preserve quote/charge agreement. |
| Two fish unit-price helpers | [MarketMath.cs:46–51](../../Assets/_Project/Code/Economy/MarketMath.cs#L46) retains base marginal pricing; [SellPricing.cs:52–56](../../Assets/_Project/Code/Economy/SellPricing.cs#L52) also includes freshness and outlet level. [Market.cs:115–117](../../Assets/_Project/Code/Economy/Market.cs#L115) and [SellService.cs:116,160](../../Assets/_Project/Code/Economy/SellService.cs#L116) use SellPricing. Direct MarketMath.MarginalPrice calls are confined to tests; the Market reference is a comment. | A separate small price-helper audit could unify the neutral-case helper without dropping non-neutral factors. Existing callers: IslandGeneralStoreTests:242; MarketDepth2Tests:154,177; NineMileCreekMarketTests:114–136, all under `Assets/Tests/EditMode/Economy/`. |
| Two sell entrypoints, one transaction implementation | [FishBuyer.cs:22–29](../../Assets/_Project/Code/Economy/FishBuyer.cs#L22) delegates to SellService; it does not implement a second payout algorithm. Fixed offer purchases and [FuelPricing.cs:143–154](../../Assets/_Project/Code/Economy/FuelPricing.cs#L143)'s per-litre rounding are distinct product contracts. | Do not classify these as redundant pricing just because they reach different counters. None needs changing to retire old shop stock. |
| Two persistence proposals for shelf effects | StockDoc:68–81 proposes sparse count/watermark records; LivingDoc:228–238 proposes economy ledger rows including player draws and old stock records. NewShelves currently keeps draws in memory only. | Do not persist both independent counts for the same goods. Resolve one authority in a separately authorized save design; this audit supplies no migration and changes no save format. |

## Safe retirement sequence, smallest changes first

These are proposed follow-ups, not authorization to change gameplay, art or saves. The smallest safe stopping point is to leave the old model dormant and retain its identity. Full deletion has prerequisites.

1. **Documentation reconciliation.** After checking PR 1480's final state, update StockDoc:96–115 and LivingDoc:16,59,176,205,230–238 to distinguish the legacy weekday pilot, the new model and whatever is actually composed. Preserve historical intent, but remove any suggestion that the dormant floor or old persistence proposal already governs live goods. Touch only these documents and this audit's snapshot note. No simulation tests are needed for a wording-only correction.

2. **Pin the unit/identity contract in existing tests.** Once the integration's chosen mapping exists, extend Content and Shipped rather than add another validator. Touch Headlamp:61–70 to check the actual stock source while retaining the explicit nonempty inventory and “not sold/stocked” assertions. Shipped:53–64 must still require a real shop and preserve the old/new id distinction or an explicit historical-id mapping; do not merely remove its assertions. Keep Pilot's exact id, type and GUID. Retain the catalog membership checks already in Content:25,58 and SellerContentTests. PR 1480 owns the integration/definition changes; coordinate by waiting for its final source before preparing this slice.

3. **Specify and migrate stock-rule tests before retiring arithmetic.** Extend `IslandStoreShelvesTests.cs` and `IslandStoreLedgerTests.cs`, which already cover replay, bounded stock, tide-blocked runs and player draws. Carry forward the applicable Rules cases: bounds (48–71), repeat settlement (86–117), replay (121–166), isolation (220–228) and snapshot ownership (232–237). Check which assumptions the new constructor actually guarantees. The old floor (74–82,201–204), committed put-back, rewind refusal and sparse record/export rules are not interchangeable with the new model. Record each intentional rule replacement explicitly and assert the replacement exactly; no deleted assertion, loosened count or silent abandonment of a contract. The floor-versus-finite-goods choice and any put-back/reservation transaction need a concrete design first. Physical goods must still be loaded, carried and unloaded by hand; a count update cannot stand in for transport.

4. **Retire only the uncomposed service cluster after step 3.** The candidate code set is `ShopStockService.cs`, `ShopRestock.cs`, `MemoryShopStockStateStore.cs`, `Core/Economy/IShopStock.cs`, their `.meta` files and the migrated portions of Rules. Keep ShopDef/Spec/Pilot initially. Re-run the complete reference and GUID search on the then-current base, compile Core and Economy, and run the migrated stock suites plus catalog/vendor purchase tests. Confirm no production registration or listener appeared meanwhile. No save migration is needed for these currently unsaved types alone; adding persistence is a separate task. If rule migration is not accepted, retain this cluster dormant.

5. **Reduce the authoring payload only after all consumers move.** Candidate files are Def, Spec, Pilot, Content, Headlamp, Shipped and the two design documents. Preserve `shop.st_peters_general` and its GUID/type binding; an identity-only ShopDef or a compatibility shell is safer than deleting the asset. Moving `Spec.Id` to a top-level id would need an explicit serialized-asset migration, exact identity tests and import proof. Removing Shelves/Stock/Credit first would break validation and current content tests. This slice must account for the new shelf's ShopId and the old line-id history. It must not alter catalog prices, scene wiring or visible fixtures as an incidental cleanup.

6. **Stop before deleting the identity or inventing persistence.** Identity:109–123 requires every recorded GUID/type/id to survive; Identity:127–141 requires declared types to match discovered types. Deleting the asset, class or its registration would violate those checks. Preserve the frozen baseline and existing addition records; [the additions contract](../../Assets/Tests/EditMode/Fixtures/DefIdentityAdditions/README.md#L17) specifies stable registrations. Any true identity retirement requires an explicit supported retirement policy first. Likewise, reconcile the two persistence proposals separately; do not implement a save bump as part of this cleanup. PR 1230 currently touches identity-contract code, so that file also needs a fresh ownership check before any later edit.

No tests or validators were added or changed for this audit. Existing coverage found before proposing changes: ShopStockRulesTests, ShopStockContentTests, HeadlampGearDefTests, IslandShelvesShippedTests, IslandStoreShelvesTests, IslandStoreLedgerTests, BuyCatalogTests, SellerContentTests and DefIdentityContractTests. Their presence explains the dependency and migration plan; it is not a claim that they passed in this run. A future implementation must report the exact classes and case counts it actually executes.
