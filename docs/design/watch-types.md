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
| `StartsOwned` | Makes this watch available as starting equipment without a saved ownership entry |
| `LegacyGearId` | Earlier ownership id for the same watch, preserving `gear.watch` |
| `Price`, `SellerId` | Cost and stable seller identity; a free starting watch can have no seller |

`watch.harbour_digital` is the only authored watch: tier 1, starting equipment, zero cost, no
seller, with all seven current readouts. It registers its identity and type through separate
identity-addition files; the frozen identity ledger stays unchanged. `TryValidate` supplies the
authoring rules used by `WatchDefContentTests` and `WatchDefValidationTests`.

The HUD asks Core's `WatchEquipment.GetWorn` for the watch to draw, after its existing hidden
and held-clock gates. The helper loads the resource catalog once and treats starting watches
as implicitly owned for both new games and old saves. Selection reads the existing `OwnedGear`
list without initializing it or adding ids. Continuing a save and repainting the watch preserve
the complete save snapshot and request no save write. Purchased ownership still comes from
the existing gear list when the player acts. No wearing field, save schema change, migration
code, shop listing, or additional face is introduced here.

Selection uses the highest owned `Tier`, with ascending ordinal id order breaking ties
deterministically, and accepts the authored legacy id as ownership of the same watch. The
presenter dispatches the selected `HarbourDigital` face through its existing drawing path with
the same clock mapping and rendering arguments. Analog, luxury, and outdoor faces belong to
their later art slices.
