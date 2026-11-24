# Rod tiers as numeric data

Proposal. The starter rod keeps its current behaviour. Two further Def assets explore
progression without an offer, a shelf entry, a placed object, or new artwork.

## What owns each rule

The existing `Player/ToolDef.cs` carries identity, the gear ownership link, storage
class and held appearance. It now also carries `RodFishingProfile`, a plain value
in Core. `CarriableTool` exposes it through `IRodFishingEquipment`; the existing
`CarriedItem.Both` read supports either hand. Fishing captures the value once per
cast. Changing what he holds during a fight cannot change that fight's numbers.

The original rod asset is unchanged. Its new profile takes the class defaults,
which match the former controller constants and the shipped configuration. The
new assets keep every appearance field and reference from that rod, including the
`cane` mesh variant. Their names describe proposed numeric tiers, not new artwork.
Neither is loaded into a shop. Both retain the original ownership link pending a
separate purchase design; the hand gate is the default gameplay gate.

| Number | Starter | Coast proposal | Deep proposal |
|---|---:|---:|---:|
| Cast reach, metres | 12 | 16 | 22 |
| Weighted line length, metres | 60 | 75 | 90 |
| Reel up, metres/second | 1.5 | 1.8 | 2.1 |
| Bite delay minimum/maximum, seconds | 1.5 / 4 | 1.5 / 4 | 1.5 / 4 |
| Legacy auto-hook delay, seconds | 1.2 | 1.2 | 1.2 |
| True-take window multiplier | 1 | 1.2 | 1.5 |
| Line strength multiplier | 1 | 1.2 | 1.5 |
| Landing speed multiplier | 1 | 1.2 | 1.5 |
| Legacy tension rise, easy/hard, per second | 0.60 / 1.00 | same | same |
| Legacy tension recovery, easy/hard, per second | 0.70 / 0.50 | same | same |
| Legacy landing fill, easy/hard, per second | 0.40 / 0.22 | same | same |
| Legacy surge interval, easy/hard, seconds | 3 / 1.6 | same | same |
| Legacy surge duration, seconds | 0.8 | same | same |
| Legacy surge pressure, easy/hard, per second | 0.15 / 0.40 | same | same |

Legacy fights interpolate the endpoint values against the existing category and
rolled-size strength. Stronger line reduces rising pressure, and landing power
increases progress. In the newer fight, the species' `RodFightDef` still owns its
rhythm and personality; the rod scales positive strain and landing progress.
Species' `BiteDef` still owns false nibbles and returns; the rod widens only the
true take. Neither system consumes additional random draws.

There is no existing rod-specific stamina charge or maximum landed weight.
`ActivitySignalListener.OnFishing` measures time in a fight and reports the
existing fishing activity. Shorter fights therefore reduce that activity's duration
per landed fish without changing its energy rate. Species eligibility, weight
ranges, tackle affinities and licences remain their existing data. No new species
or size exclusion is invented. Longer line can reach deeper water, and stronger
line helps land the fish that already bite; it does not bypass weather or licences.
Gesture thresholds, flight speed, presentation timing, fish movement and sea
pressure remain in their existing owners. Unwired test rigs still honour their
configuration's cast/line/reel values; an actual held rod owns those three limits.

## Validation and limits

Existing coverage extended: `ToolsInHandTests`, `FishFightTests` and
`RodFightControllerTests`. No parallel rod simulator or catalog is introduced.
The added cases cover all three authored profiles, every numeric field, identical
appearance references, real hands and cast gates, a frozen copy of the previous
legacy formula, starter parity for seeded days, strict increases in fish landed in
one thirty-minute day, deterministic replay, and capture at cast start. Day tests
cover legacy and newer fights under two seeds and one fixed fishing policy. They
are controlled balance experiments, not a promise about every player's catch rate.

No local Unity run or compilation was performed. CI must compile and execute these
cases plus the existing fishing, identity-ledger and content-validation suites.
No plate or playtest was performed and no appearance judgement is claimed.

Save version 25 is unchanged. `RodSnapshot` is a plain session value; the existing
cast already restarts rather than resuming from a save. Before selling new tiers,
a follow-up must define distinct purchase identities and verify reconstruction of
the correct physical Def in existing rack storage and loose placement. Persisting
new loose instances, if required, needs a separately approved save step. This
proposal adds no saved field and no automatic grant.

## Decisions still needed

1. Prices: keep the starter's current 60 coin; lean 180 and 480 for the next tiers,
   subject to first-week earning measurements. No price changes are made here.
2. Sellers: lean starter and the middle tier at the island general store; reserve
   the third for a later fishing supplier. No new sales route is made here.
3. Third-tier purpose: lean deeper access and easier fights with existing larger
   fish, rather than an exclusive species list. Weather and licence rules still apply.
