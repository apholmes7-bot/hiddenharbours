# St Peters: the first week's catch census

The island has fourteen species declared in its fish data, but it does not yet
have fourteen working ways to catch them. The opening starts on High Summer day
1. Cod, mackerel and striped bass can enter the rod pool that week; the flats
yield soft-shell clams; lobster and two crabs have pot catch lists. Mussels and
oysters have the right shellfish licence but no picking producer yet. Ten region
species have no licence mapping, and the cod licence is sold off-island.

This is a census of the authored baseline, not a promise that all these catches
are available in the demo. The first-week direction keeps the player on St
Peters; moving a seller, adding a producer or changing a licence is separate work.

## The data

Heights are metres relative to datum, inclusive at both ends. Hours use the
24-hour clock, including the start and excluding the end. All day means 0–24.
ES = Early Spring, HS = High Summer, TT = The Turn, HW = Hard Winter.
The rod methods share the species gear mask: the data does not distinguish shore
from boat. A boat still needs suitable water and the working rod setup.

| Species id | Method declared | Seasons | Tide | Hours | Required licence | Seller and location | Producer on the island |
|---|---|---|---|---|---|---|---|
| `fish.atlantic_cod` | Shore rod, boat rod; longline | All | -10 to 10 | All day | `license.cod` | `seller.nmc_harbourmaster`, Nine Mile Creek harbourmaster, off-island | Rod pool |
| `fish.mackerel` | Shore rod, boat rod | HS, TT | -10 to 10 | All day | **Missing** | None | Rod pool |
| `fish.striped_bass` | Shore rod, boat rod; net | HS, TT | -10 to 10, moving water only | 05–09 and 17–21 | **Missing** | None | Rod pool |
| `fish.winter_flounder` | Shore rod, boat rod; longline | ES, TT | -10 to 10 | 06–18 | **Missing** | None | Rod pool, outside opening season |
| `fish.rainbow_smelt` | Shore rod, boat rod; jig | ES, TT, HW | -10 to 10 | All day | **Missing** | None | Not in the island rod pool |
| `fish.atlantic_herring` | Net only; none of the five opening methods | ES, TT | -10 to 10 | All day | **Missing** | None | In rod list but rejected by rod gear; no opening net producer |
| `fish.soft_shell_clam` | Dig (`ClamFork`) | All | -10 to 0 | 06–20 | `license.clam` | `seller.leblancs`, general-store counter, St Peters | Authored clam holes |
| `fish.atlantic_surf_clam` | Dig (`ClamFork`) | HS, TT | -10 to -0.44 | 06–20 | **Missing** | None | No authored yielding hole |
| `fish.northern_quahog` | Dig (`ClamFork`) | All | -10 to -0.3 | 06–20 | **Missing** | None | No authored yielding hole |
| `fish.blue_mussel` | Pick by hand (`ClamFork`) | All | -10 to 0.11 | All day | `license.clam` | `seller.leblancs`, general-store counter, St Peters | No picking producer |
| `fish.american_oyster` | Pick by hand (`ClamFork`) | All | -10 to -0.44 | All day | `license.clam` | `seller.leblancs`, general-store counter, St Peters | No picking producer |
| `fish.lobster` | Trap | All | -10 to 10 | All day | **Missing** | None | `trap.lobster` |
| `fish.rock_crab` | Trap | All | -10 to 10 | All day | **Missing** | None | `trap.crab` |
| `fish.green_crab` | Trap | HS, TT | -10 to 10 | All day | **Missing** | None | `trap.crab` |

Lobster has a separate keep season in `FishingSeasons/LobsterStPeters.asset`:
Early Spring day 15 through High Summer day 21, inclusive. All seven opening
days fall inside it. Size and berried-animal sorting still apply independently.
Striped bass use the existing moving-water threshold from `GameConfig.FishSchools`.

The dig/pick distinction is currently prose in the species assets: both use
`ClamFork`. The table preserves that distinction without inventing another gear
flag. Species tide/hour fields above describe their data; `ClamDig.TryDig` does
not run the species resolver. It checks local exposure, equipment, capacity and
hole state. In particular the clam's authored 06–20 window is not a runtime
daylight gate on digging.

## Gaps

- **Missing licences:** the ten bold entries above have no `LicenseDef` listing
  their id. The single `KnownStPetersCatchLicenceGaps` list in
  `StPetersContentValidationTests` records each with a reason. It does not excuse
  a blank licence id. Adding an unlicensed region species fails the test;
  fixing a gap requires removing its stale exception.
- **Island seller:** the shellfish licence covers exactly clam, mussel and oyster.
  Cod is sold by the Nine Mile Creek harbourmaster, which the closed first week
  cannot reach. No island seller for that licence is authored. No licence covers
  surf clams or quahogs just because they are shellfish.
- **Opening season:** shore rod and boat rod have three eligible species, digging
  has soft-shell clams, and traps have three eligible species. No one of those
  methods has an empty seasonal pool. Picking has two all-year declarations but
  zero producers. Herring, smelt and winter flounder are outside High Summer.
- **Unproducible declarations:** surf clam, quahog, mussel and oyster are registered
  by id but have no yielding island interaction. Smelt is absent from the island
  rod pool. Herring is present in that list but net-only, so no rod roll produces
  it. No `FishingGroundDef` is authored for St Peters; those named grounds are not
  the rod's spawn table. A registered species alone is not a spawn source.
- **Baseline licence behaviour:** there is no per-fish required-licence field.
  `CatchLicensePolicy.RequiredLicenseFor` reverses `LicenseDef.PermittedSpeciesIds`.
  Missing mappings currently mean ungated, and the baseline clam dig has no
  unlicensed-release call. The separate unlicensed-release change is required
  before the intended let-go rule is shared by all methods.

## Reproducing the census

The existing `StPetersContentValidationTests` now enumerates every fish whose
`RegionIds` contains `region.st_peters`, resolves licence ids and sellers from
the licence assets, finds trap allow-lists, checks the runtime species index and
prints the masks, both hour windows, tide bounds and moving-water flag. The
licence join is intentionally based on all region declarations, including future
producers, so unfinished wiring cannot hide a licensing gap. The existing
licence-id and permitted-species validation remains in place.

Source data: `Data/Fish`, `Data/Resources/Catalog/Licenses`,
`Data/Resources/Sellers`, `Data/Shops/StPetersGeneral.asset`, `Data/Traps`,
`Data/FishingSeasons`, and `Data/Config/GameConfig.asset` beneath the asset root.
Production evidence: `StPetersBuilder` hands eight species to the persistent
controller; `StPeters.unity` serializes that same rod pool and soft-shell-only
clam holes. `FishSpeciesLibrary` registers all fourteen region species, but does
not add them to the controller's pool. `CatchResolver` and `PlacedTrapCatch`
apply gear, region, season, tide and hour gates. No scene was opened or changed.

This change adds documentation and a content guard only. The editor and EditMode
tests were not run locally; compilation and execution are left to CI.
