# Market supply persistence proposal

Status: proposed for lead-architect review. This document changes no runtime code or save schema.

## The missing state

On the reviewed main revision (`3e7188a426`), `Market` keeps supply in a private
`Dictionary<FishCategory, float>` at `Assets/_Project/Code/Economy/Market.cs:40`.
`SupplyOf` returns zero for an absent category (line 101); `RegisterSale` adds
`SupplyPerSale * count` (lines 119–120). `SettleDaily` removes the configured fraction
on `DayStarted` (lines 140–155). No market code reads or writes the save or handles
`GameLoaded`. Recreating a market therefore loses its player-created glut.

`SaveData` stores the world seed, clock, day index and money (lines 25–36), ownership
and consumable stock, and the boat, pail and freezer catches (lines 100–109). Its
`SupplyStock` field (line 120) means owned consumables, such as ice; it is not market
supply. There is no market supply field. `SaveService.SnapshotLiveState` copies only
clock/day, world seed and money (lines 243–254); `SaveRestore.ApplyToLiveServices`
restores clock, wallet and licences, then publishes `GameLoaded` (lines 47–67).
The broader `EconomyState` in `docs/architecture/data-model.md` is a design target,
not an implemented supply ledger. The reviewed schema version is 17.

Demand, outlet price level, supply per sale, elasticity and daily recovery are authored
values on `Market`, `GameConfig` and species Defs. They should continue to come from
content. Current supply and the day through which it has been settled are mutable state.

## Smallest proposed addition

Add one append-only `SaveData.MarketStates` list, with one record per market:

| Field | Meaning |
|---|---|
| `MarketId` | Stable string identity for the economic market, separate from a vendor or scene object |
| `LastSettledDay` | Absolute game day through which daily recovery has already been applied |
| `Supply` | Sparse list of `{ CategoryId, Units }`, using stable category strings and finite non-negative units |

Map the three existing `MarketId` enum values to stable strings in the Economy adapter;
Core stores strings and DTOs, without referencing the Economy enum. Map `FishCategory`
similarly. Publish the mapping as an append-only contract. Multiple counters serving the
same market share one record; unloading a scene must not discard it. Author a separate
market identity only when an outlet is intended to have independent supply.

Let a small Core ledger own the DTO list. Economy reads and updates it through Core,
following the existing ownership-ledger pattern. On sale, update the market record in
`ISaveService.Current` together with the existing wallet and inventory mutations; retain
the current explicit save triggers. This proposal adds no disk write per fish. On each
daily recovery, update both supply and `LastSettledDay`. Restore before the first quote
on initial enable and after `GameLoaded`; do not wait for a future sale to restore.

For an unloaded market, replay each missing day with the existing
`MarketMath.SettleSupplyDaily`, using the configured recovery. Match the order and float
arithmetic of ordinary day rollovers; an exponent shortcut need not be bit-identical.
Catch up only to the restored game-clock day, never wall-clock time. The ledger owns the
day stamp so two counters or a load-time day event cannot settle the same market twice.
Keep long-absence work bounded without dropping recovery days: process a fixed work
budget per update and keep the market unavailable for quotes until its saved day catches
up. The work budget belongs in configuration if implementation requires it.

Use a stable order when serializing markets and categories. Drop rows whose supply is
negative or non-finite, giving that category its zero baseline; retain unrecognized ids
for later content rather than remapping them to another market. Reject duplicate ids in
validation; on load the first valid row for an id wins, so duplicates cannot double supply.
This is a small supply ledger, not price history,
contracts, NPC trading or the later business simulation.

## Migration

Take the next unused schema version at implementation time; do not reserve version 18,
because other save changes may land first. For each older save, initialize an empty
`MarketStates` list. An absent market starts at zero supply with `LastSettledDay` equal
to the restored clock day. That preserves today's baseline for saves that never recorded
sales. There is no truthful way to reconstruct their lost glut. Preserve all existing
money, ownership and catch fields. New games use the same empty-ledger convention.

## Can seed and time replace the field?

Not for the current player-created supply. Two games with the same seed and clock may
have sold different categories, quantities and markets at different times. Wallet and
remaining catch do not encode that history. Replaying sale events would work only if
those events were saved, which costs more than the current supply and settle day.

Rule 5 prefers recomputing environmental and future exogenous market inputs from seed
and time, while storing irreducible player actions or sufficient state. Persist the
player-created supply plus its settle day; recompute prices, authored demand and future
deterministic background inputs. Do not store a price quote or the weather.

## Acceptance for implementation

- Sell a known category and quantity, save, recreate the market and load: supply and the
  next marginal price are exactly equal before and after.
- Two markets retain independent gluts; two counters at one market see the same glut.
- Loading in the same day applies zero recovery. Advancing one day applies exactly one
  existing recovery step; a second counter, repeated `GameLoaded`, or region return adds none.
- An unloaded market catches up to the same supply as an equivalent continuously loaded
  market, including long absences, before quoting. No wall-clock catch-up occurs.
- Migration of a version-17 save preserves every existing field and supplies an empty
  market ledger. New games, missing categories, unknown ids and invalid rows have explicit tests.
- Fixed seed, clock, configuration and sale history reproduce the same ordered DTOs and prices.

Implementation requires the lead architect's Core/save change and migration review. This
proposal deliberately leaves that work for a separate implementation PR.
