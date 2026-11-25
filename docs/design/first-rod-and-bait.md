# First rod and bait: counted stock pending a purchase place

PROPOSAL, nobody has accepted this

This takes the explicit **no-free-slot fallback**: add the data that can work without another
place in the room, then stop. The rod becomes counted stock in the existing counter book.
It does **not** become a shelf interaction or a physical purchase. The free rod and bait
remain. This is not completion of the paid-first-rod opening.

Baseline inspected: `d46bde303e6aa256d23d47a05ede5d7d6f38e51e`. The first-week source audit was
written against `f1089fe61462a7d1cc2a1366c6f918ef940cb66b`; its blocker 6 is the brief, not
execution proof. No editor, compiler, test runner, save file or visual comparison was run here.

## Acquisition on the baseline

Paths below are under `Assets/_Project/` unless they start with `Assets/Tests/`.

| Route | Code and authored content | What the player receives |
|---|---|---|
| Fresh save | `Code/Core/Save/SaveMigration.cs`, `NewGame`; `Code/Core/Save/SaveData.cs` | Empty `OwnedGear` and `BaitStock`. This is before scene startup, not the first playable morning. |
| Starting equipment | `Code/Player/StartingGear.cs`; `Scenes/StPeters.unity:40297`, `_startingGearIds` | Shovel and bucket ownership, once under `st_peters_starting_gear_granted`; no rod. Their physical objects are separate. |
| Headlamp | `Code/Player/PlayerGear.cs`, `Owns`; `Data/Config/GameConfig.asset:414`, `CarriedFromStartGearIds` | Headlamp ownership by derivation, also for older games. Left alone. |
| Free rod pickup | `Code/Player/CarriableTool.cs`, `IsCarriable` and `Interact`; `Data/Tools/Rod.asset`; `Scenes/StPeters.unity:50021` | One active `Rod`, instance `tool.st_peters.rod`, at `(6.1,-0.6)`. It can be picked up without paying or owning `gear.rod`. `DevFishingInput` on the island has `_requireRodOwnership: 0` at scene line 51522, so it can be used. The physical rod reference occurs once across the committed region scenes and prefabs. |
| Paid rod ownership | `Code/Economy/GearShop.cs`, `TryBuy`; `Data/Resources/Catalog/Gear/Rod.asset`; the island `GeneralStoreCounter` | Charges 60, adds `gear.rod`, saves and publishes `GearPurchased`; it does not place a rod in a hand or bag. The same offer names the Nine Mile Creek chandlery, an off-island route. Duplicate ownership refuses payment. |
| Free starting bait | `Code/Fishing/StartingBait.cs`; `Scenes/StPeters.unity:64338`; `Data/Resources/Catalog/Bait/Herring.asset` | **Six herring** on startup, once under `trap_starting_bait_granted`. The audit's rod/bait step omitted this grant. |
| Repeatable bait grant | `Code/Fishing/DevTrapInput.cs`, `GearKeysLive`, `GrantDevSupply`; `Scenes/StPeters.unity:51015` | The configured G press on deck adds **five herring**. The input requires deck mode and no modal interaction; the public grant method itself only requires the save and bait reference. This is another free acquisition path, independent of the starting grant. |
| Paid capelin | `Code/Economy/BaitShop.cs`, `TryBuy`; `Data/Resources/Catalog/Bait/Capelin.asset`; `Data/Resources/ShopShelfSlots/StPetersGeneralShelfSlots.asset`, `slot.bait` | One counted box costs 20 and credits ten capelin directly to `BaitStock`; `IntoBag` is false. The supply ledger's 2.5 units per box are not its bait count or price. |
| Other paid bait | `Code/Economy/BuyCatalog.cs`, `BuildCounted`/`BuildFrom`; `Data/Resources/Catalog/Bait/{Herring,Mackerel,SquidStrip,SeaWorm,ShuckedClam,FishScrap}.asset` | These six offers also name the island seller. Its existing `BaitShop` is an arm for all tagged bait, not only its serialized default. They stay in the book, uncounted by the island shelf lines: ten-unit lots costing 30, 40, 60, 40, 30 and 10 respectively. |

Searches of the runtime ownership writes, `BaitStock` additions, `TackleBox.AddBait` callers,
rod asset references, catalogue rows and startup components found no additional rod/bait gift
or pickup on the island. `TackleBox.AddTackle` has no runtime caller granting the rod. Catch
consumption and trap returns are not fresh-game gifts. Existing saves can already own a rod;
the older rack persistence can also restore an authored rod. Neither is a new-game purchase.

**First morning, before and after this proposal:** the player owns the shovel, bucket and
headlamp, receives six herring, and can pick up the free rod. He does not begin with paid rod
ownership. This proposal changes the store's count, not that starting kit. A test on
`SaveMigration.NewGame()` alone would falsely imply that the playable morning has no bait.

## Why there is no free slot

The live placement file is
`Data/Resources/ShopShelfSlots/StPetersGeneralShelfSlots.asset`, consumed by
`Code/World/Shops/SoldOutSignsDirector.cs`. Its seven slots are all assigned:

| Slot | Existing line | Existing use |
|---|---|---|
| `slot.groceries` | `line.grocery_box` | Empty-gondola cover; shared room surface, no purchase listing |
| `slot.ice` | `line.ice_bag` | Ice purchase, cover, goods and sold-out card |
| `slot.bait` | `line.bait_box` | Capelin purchase, bait goods and sold-out card |
| `slot.gas` | `line.gas_jerry_can` | Gas stock's sold-out card; no purchase listing |
| `slot.bread` | `line.bread` | Bread purchase and goods |
| `slot.oatcakes` | `line.oatcakes` | Oatcakes purchase and goods |
| `slot.beans` | `line.tinned_beans` | Beans purchase and goods |

An empty `ListingId` is not an unused location: groceries and gas still have assigned stock
and presentation. Replacing either line would change what its cover/card means. No slot,
offset, sprite reference, scene or prefab is changed. The existing rod sprite and bait-box
sprite remain available to the later placement work; no new art is proposed here.

`Data/Shops/StPetersGeneral.asset` describes four `fixture.rod_rack` slots in the separate
weekday stock model. It is **not** a `ShopShelfSlot`, and `IslandShelvesHost` does not install
an interaction from it. Treating those four logical slots as four placed purchase points
would repeat the audit's integration gap.

## Data added without a slot

The existing `StPetersStoreShelves.asset` gains `line.fishing_rod`, binding only `gear.rod`.
It has capacity four, matching the existing shop specification. It takes a proposed 10%
share of the existing **Hardware, lumber and gear** supply category, at 1.25 kg per packaged
rod. The full target is `4 buffer days × 10 kg/day × 1.25 allowance × 0.1 = 5 kg`, or four
rods. These are proposal stock assumptions, not a measured rod mass or accepted balance.
The category is already in the ledger; this adds no boat-hardware offer to the island.

`SoldAtShelf` stays false. The book therefore still offers the rod, now with a finite count;
the existing confirmation spends 60, records ownership and takes one rod from that line.
Moving the offer to the shelf before a slot exists would remove its only paid entry point.
The capelin asset now explicitly writes its already-effective `LotSize: 10`, keeping its
unit price 2 and box price 20. Rod and bait use different back-room goods and independent
purchase rows. All existing food/ice/gas lines and all art references are untouched.

No new Def type or identity is introduced. `line.fishing_rod` is a line inside the existing
shelf Def, distinct from the older shop specification's `line.rod`. No existing tuning value
changes; the only new tuning is the rod line.

## Bag and save boundary

The audit's claim that live shopping is unsaved is obsolete on this baseline:
`ShoppingBagContents` reads and writes `SaveData.ShoppingBags`, `IslandShelvesHost` imports
and exports `StorePurchases`, and `ShelfGood.Press` defers writes around purchase and bagging.
The existing `BagAndPurchasesSavedTests` covers that implementation.

The bag contract still reserves **supplies**, not gear ownership or bait lots.
`ShoppingBagContents.TryPut` requires `SupplyLocker.Owned`; `BagItemDto.SupplyId` denotes
that same supply key. Setting `IntoBag` on capelin would charge the player, credit bait, then
fail the bag reservation. Setting it on a rod would fail for the same reason. No flag is
flipped to pretend this delivers either purchase physically.

The new rod stock uses existing v26 purchase rows; no saved field or format changes. The
tests round-trip a v25 game's rod ownership, herring and purse through migration and rod/bait
purchase replay. This does not certify loose-rod placement persistence.

The eventual physical-delivery save step must retain the exact paid listing and lot count,
its exclusive hand/bag/storage reservation and location, and restore those together with the
purse and shelf debit. The present supply-only bag row does not establish that contract for
gear or bait. If that work remains session-only, expose a plain snapshot and keep the
reservation and corresponding payment consistent; do not persist a debit while losing its
delivery on restart.

A later new-game gate must be set only when the fresh game is created, using the existing
progress-flag store, and must never be inferred from an empty rod list on Continue. The main
baseline has `SaveMigration.NewGame` and `SaveService.LoadedExistingSave`; the explicit
`st_peters.dory_on_slip` fact cited by the audit belongs to PR 1539, not this baseline.
`ShellFlow.cs` and `StPeters.unity` are protected by that open work. No new-game fact or
pickup restriction is added here. Old games keep both owned and accessible equipment.

## First two days' purse

The current data still front 20 and price the shellfish licence at 15, leaving 5. Using the
audit's conditional one-coin-per-saleable-clam budget, before food or any other purchase:

| Saleable clams sold each day | End of day 1 | End of day 2 | After buying rod + capelin box on day 2 |
|---|---:|---:|---:|
| 20 | 25 | 45 | Not affordable; 35 short |
| 40 | 45 | 85 | 5 |
| 60 | 65 | 125 | 45 |

Rod 60 + ten capelin 20 = 80, requiring 75 earned coins after the licence. At 20 clams/day
this is day 4, at 40 or 60/day it is day 2, before food and other costs. A day-one purse of
65 buys the rod but leaves only 5 for bait. These are arithmetic envelopes, not an island
playthrough: physical-pail selling, eating, reachable flats and paid launch remain separate
work. Spending the initial 20 on bait also remains a licence-money trap.

## Questions with leans for review

- **Where should the rod be sold?** Add a real rack purchase slot using the existing rod art;
  keep all seven current assignments. Placement and its visual verification need a separate
  authorized change.
- **What should a rod go into?** The earlier shelf plan says a free hand; the present brief
  groups rod and bait with the paper bag. Lean toward the rod in a free hand and one sealed
  ten-capelin box in the bag, with the physical delivery contract settled before activation.
- **Keep 60 and 20?** Retain the current data for this proposal; balance after a playable
  food-and-clam loop. Do not call the two-day 85-coin envelope a guaranteed income.
- **Accept four rods, a 10% gear share and 1.25 kg per packaged rod?** Lean toward this small
  reserve as a first test, subject to a supply-run and affordability review.
- **When should free acquisition close?** In the same playable change as physical purchase:
  gate the loose rod and both herring grants only for explicitly new games. Preserve old
  saves and the starting headlamp. Also address the six other uncapped bait offers if every
  bait kind is to run out independently.

## Verification and remaining acceptance

Existing coverage was extended, without adding a parallel subsystem:

- `StoreSoldOutShippedTests`: counted listings also validate gear, keeping uniqueness and
  seller checks; the rod remains in the book pending a slot, with exact data prices/counts.
- `IslandShelvesShippedTests`: buy every rod or bait box, assert each decrement exactly and
  every other line unchanged; refuse the next item; replay both purchases in reverse day
  order with old rod ownership, bait, purse and seed preserved.
- `ShopAtTheShelfTests`: real shipped rod/capelin offers through the existing confirmation,
  85 → 25 → 5 coins, one rod and ten capelin, separate stock decrements, duplicate rod refusal.
  It explicitly asserts that these paths still create no bag contents.

The content validator's rule moved from “a counted listing is a supply or bait” to “a counted
listing may also be gear.” Its existing uniqueness and stocked-by-seller assertions remain.
The exact shelf-slot coverage and bag-grocery assertions are unchanged.

Compilation and all EditMode/content tests await the shared CI run. No test count is claimed
as executed. The required live no-rod/no-bait start, physical shelf sale of both goods, bag
delivery and new-game-only pickup restriction remain unimplemented under the no-slot
fallback. No visual acceptance or complete first-week proof is claimed.
