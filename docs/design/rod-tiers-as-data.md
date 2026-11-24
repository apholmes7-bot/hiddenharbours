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

## Relationship to the reach-only proposal

#1502 proposes a narrower alternative: a zero-default `GearOffer.RodCastCapMetres`
and a pure `FlickCastMath.ResolveRodCastCap` helper, with no production caller.
Its example tiers are 12 and 18 metres. Zero or malformed overrides inherit the
global cap, and species, bite timing, line depth and fight rules stay unchanged.

This proposal deliberately differs. Capability belongs to the physical `ToolDef`
held through `IRodFishingEquipment`, is captured once per cast, and uses positive
validated profiles rather than a zero-as-inherit offer field. Both wind-back aim
and release read that same captured cap. Its examples are 12, 16 and 22 metres;
upgrades also extend weighted line length and change fight assistance. The seeded
day tests therefore assert improved landings, which the reach-only proposal does
not promise for the same nearby fish. Neither proposal changes species eligibility.

These are alternative balance and data-ownership proposals, not two cumulative
sources of rod tuning. This branch does not copy or consume #1502's offer field or
resolver. Combining them requires selecting one source of truth and deciding
whether progression is reach alone or also fight assistance; the unsold profiles
here do not settle that decision. No existing cast, fight or parity assertion is
relaxed to make the alternatives appear equivalent.

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

The first automated run reported six EditMode failures. Four parameterizations of
`StarterRod_ReplaysThePreProfileFightFormula` differed by a floating-point unit:
the reference calculation had collapsed the old clamped `weight01` intermediate
into the strength expression. The frozen reference now preserves the former
arguments, normalization and operation order, and compares all seven tuning fields
exactly before comparing the complete fight. No tolerance is introduced.
The two `HigherRodInHand_OpensTheExistingCastGateAndCapturesItsNumbers` cases
inherited the dry clam-flat fixture. They now provide wet landing water and assert
the live input gate before retaining the exact `Cast` and captured-profile checks.
The corrected cases still require automated execution; no passing result is
claimed for these changes. Pinned main was merged without a rod census change.

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
