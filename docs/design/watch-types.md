# Watch definitions

The first watch definition describes the existing Harbour digital face. It is shared data in
Core, one `WatchDef` asset per watch, under `Data/Resources/Watches`. Nothing in the definition
draws pixels or changes the existing watch renderer, clock mapper, or repaint cadence.

| Field | Meaning |
|---|---|
| `Id` | Append-only `watch.snake_case` identity, suitable for the existing `OwnedGear` list |
| `DisplayName` | Authored watch name |
| `Face` | Append-only renderer key; only `HarbourDigital` is implemented |
| `Readouts` | Time, weekday, date, season, year, market day, and night light capability flags |
| `Tier` | Higher tiers take precedence among owned watches |
| `StartsOwned` | Grants this watch as starting equipment on the first live watch repaint |
| `LegacyGearId` | Earlier ownership id for the same watch, preserving `gear.watch` |
| `Price`, `SellerId` | Cost and stable seller identity; a free starting watch can have no seller |

`watch.harbour_digital` is the only authored watch: tier 1, starting equipment, zero cost, no
seller, with all seven current readouts. It registers its identity and type through separate
identity-addition files; the frozen identity ledger stays unchanged. `TryValidate` supplies the
authoring rules used by `WatchDefContentTests` and `WatchDefValidationTests`.

The HUD asks Core's `WatchEquipment.GetWorn` for the watch to draw, after its existing hidden
and held-clock gates. The helper loads the resource catalog once and grants starting watch ids
into the live save's existing `OwnedGear` list. The grant is additive and idempotent for both new
games and old saves, preserving all earlier gear and legacy ids. The normal save flow persists
that list; the presenter does not write a save file. No wearing field, save schema change,
migration code, shop listing, or additional face is introduced here.

Selection uses the highest owned `Tier`, with ascending ordinal id order breaking ties
deterministically, and accepts the authored legacy id as ownership of the same watch. The
presenter dispatches the selected `HarbourDigital` face through its existing drawing path with
the same clock mapping and rendering arguments. Analog, luxury, and outdoor faces belong to
their later art slices.
