# Four numbers on each rod

Each rod carries four authored numbers in its own existing equipment definition.
They are read through Core's `IRodFishingEquipment.TryGetRodFishing`, implemented by
the definition and the carried instance. No fishing simulation, gate, casting,
bite timing, fight rule or configuration value changes in this proposal.
PR 1502 is not taken.

| Identity | Tier | Cast reach (m) | Line (m) | Reel speed (m/s) | Fight help |
|---|---:|---:|---:|---:|---:|
| `tool.rod` | 1 | 12 | 60 | 1.5 | 1 |
| `tool.rod_coast` | 2 | 16 | 75 | 1.8 | 1.2 |
| `tool.rod_deep` | 3 | 22 | 90 | 2.1 | 1.5 |

These are equipment contributions, before skill. Fight help is dimensionless and
is not a multiplier. The future skill consumer adds the player's corresponding
skill contribution to each rod number outside this interface. It must specify
how those effective values enter fishing when that behavior is introduced.
No skill, selection, automatic upgrade, or cast snapshot service is implemented here.

## Contract

A successful read returns the four numbers by value. It neither selects a player
nor uses global equipment state. A caller retains its own snapshot and may add its
own player's skill without modifying the shared asset or another player's result.
A missing definition, zero tier, or any nonpositive, NaN or infinite number returns
false and the default empty value. There are no fallback tuning values in code;
the starter rod authors its four values explicitly, as the higher rods do.

The three identities and existing metadata are preserved. The two additional rod
identities remain registered through the existing identity addition files; the
frozen baseline is untouched. There is no new definition type.

## Scope and delivery

The two higher rods remain unplaced and unsold. Their appearance fields match the
starter exactly. Adding numeric fields to the starter changes none of its appearance
fields. No scene, sprite, material, rig, animation or shelf placement is changed.
The live cast gate still recognizes its configured starter identity; these data
alone do not activate higher rods or grant stronger fishing behavior.

All three currently share the existing rod ownership category. Before upgrades
become purchases, their sale identities and the mapping from a physical instance
to its definition must be settled. The value returned here is session-only.
A later save step would need to restore the exact equipment definition associated
with each player's physical rod and any separate per-player skill state. It must
not infer the equipped tier from the shared ownership category. Version 26, saved
fields, and migration are unchanged.

## Verification

`ToolsInHandTests` is the existing coverage extended here: exact values and tiers
for all three assets, exactly four profile fields, unchanged appearance fields,
independent equipment reads and immutable-by-copy snapshots, unchanged upgrade
gating, and refusal of missing/non-rod/invalid data. The existing baseline gate,
fight and controller assertions remain intact.

The prior draft's behavior-changing fight and cast assertions are replaced by the
accepted data-only rule: equipment exposes numbers, while fishing remains as on
the baseline. All fishing source and the two fight test fixtures match the pinned
baseline. Serialized-data and source comparisons are static checks; compilation,
EditMode, content validation and PlayMode await CI. No local editor or visual
verification was run.
