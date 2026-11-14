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
| `Tier` | Authored priority for the future highest-tier-owned selection |
| `StartsOwned` | Whether the ownership integration should grant this watch as starting equipment |
| `LegacyGearId` | Earlier ownership id for the same watch, preserving `gear.watch` |
| `Price`, `SellerId` | Cost and stable seller identity; a free starting watch can have no seller |

`watch.harbour_digital` is the only authored watch: tier 1, starting equipment, zero cost, no
seller, with all seven current readouts. It registers its identity and type through separate
identity-addition files; the frozen identity ledger stays unchanged. `TryValidate` supplies the
authoring rules used by `WatchDefContentTests` and `WatchDefValidationTests`.

This is the data portion of helm slice 16. The current HUD still draws its existing digital
watch directly. Starting grants and highest-tier-owned selection are pending the watch
presenter integration, which must edit the HUD file currently reserved by PR 1180. The data's
`StartsOwned` flag does not itself mutate a save or grant ownership. No wearing field, save
schema change, shop listing, or additional face is introduced here.

The remaining integration should grant starting watch ids into the existing `OwnedGear` list
for both new games and old saves without duplicating ids or discarding existing gear. Selection
should use the highest owned `Tier`, with ordinal id order breaking ties deterministically,
accept the authored legacy id, and dispatch the chosen `Face` through the existing drawing
path. Analog, luxury, and outdoor faces belong to their later art slices.
