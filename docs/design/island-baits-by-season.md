# Island baits by season

Data audit at `f808af6ae5d5a0041f3d2147d74b1ddef7dce609`. The minimum working bait list is **one type: capelin**, already sold on the island. That is enough for a positive bait weight for every in-season island rod species; it is not a preferred bait for all of them. Recommendations below are marked **PROPOSAL**.

## Scope and meaning

The first week is confined to **St Peters water, `region.st_peters`**, under the [first-week decision](../adr/0062-the-first-week-is-closed-by-weather.md). Adjacent East Water, South Water, West Water and the mainland are outside this first-week stock calculation. This is the accepted geographic scope, not a claim that the whole weather gate is already active: the shipped [east barrier](../../Assets/_Project/Data/Resources/CampaignBarriers/StPetersStormEast.asset) requires `st_peters.first_week`, and [the director](../../Assets/_Project/Code/World/Campaign/CampaignBarrierDirector.cs) leaves it down without that flag. The calendar starts on day 1 of High Summer in [GameConfig](../../Assets/_Project/Data/Config/GameConfig.asset); the other seasons are a year-round data comparison, not four seasons played within the demo week.

“Catchable” below means eligible for the island's shipped rod pool and season, subject to the remaining time, tide, moving-water, gear and location conditions. It does not mean a guaranteed bite or permission to keep the catch. Licences remain a separate condition; an unlicensed catch goes back. This audit does not claim a playtest or change licence availability.

There is **no first/second bait ranking in the shipped data**. [BaitDef](../../Assets/_Project/Code/Economy/BaitDef.cs) stores a `FavorsSpeciesIds` list, not an ordered preference or a seasonal preference. [CatchResolver.BaitAffinity](../../Assets/_Project/Code/Fishing/CatchResolver.cs) gives every matching bait the same multiplier: **3** with the shipped settings. A bait with a nonempty favours list that does not include the fish gives **0.35**, still positive. These are multipliers on a species' weight, not catch probabilities. Bait does not override a closed season or another eligibility condition. “No preference” below means none of the seven shipped bait definitions lists that species; it does not mean any bait gets the preferred multiplier.

## Island rod catches by season

The island's serialized `_regionFish` list matches the [authored pool](../../Assets/_Project/Code/App/Editor/StPetersBuilder.cs#L1531), also pinned by [FishDutyCycleAtTheLandingTests](../../Assets/Tests/EditMode/FishDutyCycleAtTheLandingTests.cs): eight species. Intersecting it with `RegionIds` and `AllowedGear` leaves four rod species on the island. Seasons use the masks in [Gear](../../Assets/_Project/Code/Fishing/Gear.cs), checked by [CatchResolver.Matches](../../Assets/_Project/Code/Fishing/CatchResolver.cs). The table contains all ten resulting species/season pairs.

| Season | Species catchable by the shipped island rod | Preferred bait | Second choice in shipped data | Island store today |
| --- | --- | --- | --- | --- |
| Early Spring | [Atlantic cod](../../Assets/_Project/Data/Fish/AtlanticCod.asset) | Capelin, sea worm, shucked clam and squid strip, all tied | No ranked second; the other three are equally favoured alternatives | Capelin: **yes**. Other three: **no** |
| Early Spring | [Winter flounder](../../Assets/_Project/Data/Fish/WinterFlounder.asset) | None authored | None authored; capelin works at 0.35, without a preference bonus | Capelin: **yes**; no preferred bait exists |
| High Summer | Atlantic cod | Capelin, sea worm, shucked clam and squid strip, all tied | No ranked second; the other three are equally favoured alternatives | Capelin: **yes**. Other three: **no** |
| High Summer | [Mackerel](../../Assets/_Project/Data/Fish/Mackerel.asset) | Capelin only | None authored; every other shipped bait is unfavoured | Capelin: **yes** |
| High Summer | [Striped bass](../../Assets/_Project/Data/Fish/StripedBass.asset) | None authored | None authored; capelin works at 0.35, without a preference bonus | Capelin: **yes**; no preferred bait exists |
| The Turn | Atlantic cod | Capelin, sea worm, shucked clam and squid strip, all tied | No ranked second; the other three are equally favoured alternatives | Capelin: **yes**. Other three: **no** |
| The Turn | Mackerel | Capelin only | None authored; every other shipped bait is unfavoured | Capelin: **yes** |
| The Turn | Striped bass | None authored | None authored; capelin works at 0.35, without a preference bonus | Capelin: **yes**; no preferred bait exists |
| The Turn | Winter flounder | None authored | None authored; capelin works at 0.35, without a preference bonus | Capelin: **yes**; no preferred bait exists |
| Hard Winter | Atlantic cod | Capelin, sea worm, shucked clam and squid strip, all tied | No ranked second; the other three are equally favoured alternatives | Capelin: **yes**. Other three: **no** |

Cod has season mask 15 (all four); mackerel and bass have 6 (High Summer and The Turn); flounder has 5 (Early Spring and The Turn). Cod and mackerel allow all hours. Flounder allows 06:00–18:00. Bass allows 05:00–09:00 and 17:00–21:00 and requires moving water. These four species have the broad authored tide window of -10 to 10 metres; actual water, reach and school conditions still matter. Haddock and pollock are in the persistent pool but their species data excludes St Peters. Herring is in the pool but is net-only, and the clam is dug rather than caught by a rod.

### Rainbow smelt: authored for the island, absent from its rod pool

[RainbowSmelt](../../Assets/_Project/Data/Fish/RainbowSmelt.asset) lists St Peters, handline/jig gear and season mask 13. It is absent from the eight serialized rod references, so it is not claimed as a shipped island rod catch above. Its bait data still belongs in a first-week coverage audit:

| Season allowed by the species asset | Species | Preferred bait | Second choice | Island store today |
| --- | --- | --- | --- | --- |
| Early Spring | Rainbow smelt | None authored | None authored; capelin would give 0.35 if the species were in the pool | Capelin: **yes** |
| The Turn | Rainbow smelt | None authored | None authored; capelin would give 0.35 if the species were in the pool | Capelin: **yes** |
| Hard Winter | Rainbow smelt | None authored | None authored; capelin would give 0.35 if the species were in the pool | Capelin: **yes** |

Smelt is absent in High Summer by its season mask as well. Buying another bait cannot repair the missing pool reference.

## Other island species and methods

These complete the fourteen species whose shipped `RegionIds` include St Peters. Seasons here describe their **species assets**, not proof that every gathering interaction is installed. None adds a bait requirement to the first-week rod-and-hand stock calculation.

| Species | Authored seasons | Method and availability distinction | Preferred bait / second choice | Island store sells it today? |
| --- | --- | --- | --- | --- |
| [Soft-shell clam](../../Assets/_Project/Data/Fish/SoftShellClam.asset) | All four | Clam-fork gear; the island's serialized digging holes reference this species | No bait used; no second choice | Not applicable |
| [Blue mussel](../../Assets/_Project/Data/Fish/BlueMussel.asset) | All four | Clam-fork tag; accepted hand-picking work is separate from this pinned base's live interactions | No bait used; no second choice | Not applicable |
| [American oyster](../../Assets/_Project/Data/Fish/AmericanOyster.asset) | All four | Clam-fork tag; same hand-picking distinction as mussels | No bait used; no second choice | Not applicable |
| [Northern quahog](../../Assets/_Project/Data/Fish/NorthernQuahog.asset) | All four | Clam-fork tag; no quahog reference in the island's serialized digging holes | No bait used; no second choice | Not applicable |
| [Atlantic surf clam](../../Assets/_Project/Data/Fish/AtlanticSurfClam.asset) | High Summer, The Turn | Clam-fork tag; no surf-clam reference in the island's serialized digging holes | No bait used; no second choice | Not applicable |
| [Atlantic herring](../../Assets/_Project/Data/Fish/AtlanticHerring.asset) | Early Spring, The Turn | Net-only; not an island rod catch | None authored / none authored | No preferred bait to stock |
| [American lobster](../../Assets/_Project/Data/Fish/AmericanLobster.asset) | All four | Trap-only; keeping also has a separate fishing season and later licence requirements | Herring and mackerel are tied favoured baits; no ranked second | **No** for both |
| [Rock crab](../../Assets/_Project/Data/Fish/RockCrab.asset) | All four | Trap-only; crab licensing belongs after the first week | Fish scrap only / none authored | **No** |
| [Green crab](../../Assets/_Project/Data/Fish/GreenCrab.asset) | High Summer, The Turn | Trap-only; listed in the crab pot's allowed catch | None authored / none authored | No preferred bait to stock |

The [lobster pot](../../Assets/_Project/Data/Traps/LobsterTrap.asset) names `bait.herring` as `RequiredBaitId`; the [crab pot](../../Assets/_Project/Data/Traps/CrabTrap.asset) names `bait.fish_scrap`. Those pot inputs are distinct from a species preference: fish scrap does not list green crab, and the equal lobster preference for mackerel does not change the pot's named bait. [LobsterStPeters](../../Assets/_Project/Data/FishingSeasons/LobsterStPeters.asset) opens keeping from Early Spring day 15 through High Summer day 21. These pot facts are outside the first-week shelf minimum; they do not justify bringing later commercial fishing into the opening week.

## What the island store actually sells

“Yes” requires both an enabled catalog listing for `seller.leblancs` and a sale line in [StPetersStoreShelves](../../Assets/_Project/Data/Resources/IslandSupply/Shelves/StPetersStoreShelves.asset). Merely naming the seller is insufficient: [CatalogListing.IsStockedBy](../../Assets/_Project/Code/Economy/CatalogListing.cs) first checks `Listed`. Availability here means an authored offer, not a promise that finite stock is always on the shelf.

| Shipped bait definition | Species it favours, exactly as authored | Listed for island sale? | Island shelf sale line? | Sold today? |
| --- | --- | --- | --- | --- |
| [Capelin](../../Assets/_Project/Data/Resources/Catalog/Bait/Capelin.asset) (`bait.capelin`) | Atlantic cod, mackerel, pollock | Yes | `line.bait_box` | **Yes** |
| [Herring](../../Assets/_Project/Data/Resources/Catalog/Bait/Herring.asset) (`bait.herring`) | American lobster | No (`Listed: 0`) | No | **No** |
| [Mackerel](../../Assets/_Project/Data/Resources/Catalog/Bait/Mackerel.asset) (`bait.mackerel`) | American lobster | No (`Listed: 0`) | No | **No** |
| [Fish scrap](../../Assets/_Project/Data/Resources/Catalog/Bait/FishScrap.asset) (`bait.fish_scrap`) | Rock crab | No (`Listed: 0`) | No | **No** |
| [Sea worm](../../Assets/_Project/Data/Resources/Catalog/Bait/SeaWorm.asset) (`bait.sea_worm`) | Haddock, Atlantic cod | No (`Listed: 0`) | No | **No** |
| [Shucked clam](../../Assets/_Project/Data/Resources/Catalog/Bait/ShuckedClam.asset) (`bait.shucked_clam`) | Haddock, Atlantic cod | No (`Listed: 0`) | No | **No** |
| [Squid strip](../../Assets/_Project/Data/Resources/Catalog/Bait/SquidStrip.asset) (`bait.squid_strip`) | Atlantic cod, pollock | No (`Listed: 0`) | No | **No** |

No bait definition supplies a season-specific preference or a preferred-bait entry for rainbow smelt, winter flounder or striped bass. No biological preference, planned bait or unmerged stock change has been substituted for the shipped lists.

## Minimum stock proposal

**PROPOSAL — retain one bait type on the island shelf: `bait.capelin`. No additional bait type is needed for working-bait coverage under the shipped rules.** “Working” here means a positive bait multiplier for each species while its season and other catch conditions are satisfied. It does not promise an easy catch, a target catch rate or enough stock for four players.

| Season | Current rod species covered by capelin | Capelin multiplier | Additional data-eligible coverage if smelt is wired later |
| --- | --- | --- | --- |
| Early Spring | Cod; winter flounder | 3; 0.35 | Smelt: 0.35 |
| High Summer | Cod; mackerel; striped bass | 3; 3; 0.35 | None: smelt is out of season |
| The Turn | Cod; mackerel; striped bass; winter flounder | 3; 3; 0.35; 0.35 | Smelt: 0.35 |
| Hard Winter | Cod | 3 | Smelt: 0.35 |

The lower bound is one distinct bait id because the requirement asks for **at least one bait**; a bare lure is not a stocked bait. Capelin meets that bound for all ten live rod species/season pairs and, conditionally, the three smelt pairs. Exhaustively checking the seven shipped baits gives seven possible one-type covers under the positive-weight rule. Capelin is the one already sold and favours both cod and mackerel. Hand gathering uses no bait. This calculation concerns distinct bait types, not pack sizes, stock counts, prices, restock cadence or catch balance.

**PROPOSAL — if “working” is intended to mean a preferred-bait bonus for every species, resolve the missing preferences before expanding stock.** No list made solely from the seven shipped baits can provide that coverage: none favours flounder, bass or smelt. Adding sea worm, shucked clam or squid strip would add another equal cod preference without covering those gaps. Ranked second choices likewise require a new decision; they cannot be inferred from list order.

**PROPOSAL — treat the missing smelt pool reference as separate integration work before describing smelt as a live island rod catch.** No scene, species, bait, shelf, configuration or saved field changes accompany this document.

## Verification boundary

This audit read all nineteen shipped fish definitions, all seven bait definitions, the island's shelf lines, the eight serialized rod-pool references and the serialized clam-species references. It checked the region, gear and season intersections and computed the positive-weight covers from the shipped settings. Relevant existing logic and content suites include `FishDutyCycleAtTheLandingTests`, `ShellfishSpeciesContentTests` and `IslandGeneralStoreTests`; this document adds no runtime validator or test. The editor, gameplay and automated tests were not run for this documentation change. The tables establish authored eligibility and wiring, not physical reachability from a particular shore position or an observed catch rate.
