# Licence and saved-bait failure decisions

These decisions prepare the Fishing-to-Economy adapter migration described in the
[coupling audit](../architecture/module-coupling/fishing-to-economy.md). They introduce
read-only typed queries; no runtime caller, serialized field, content asset or restore
behavior migrates in this change.

## Landing permission

Use `CatchLicensePolicy.Evaluate` when the Core permission adapter is introduced.
`MissingCatalog` refuses acquisition and identifies a configuration failure; it must
not be advertised as a missing player licence. A null catalog means unavailable.
An explicitly supplied empty catalog is available and intentionally ungated. The
composition adapter must therefore pass null on loading failure, never substitute an
empty collection. Empty species ids return `InvalidSpecies` and refuse acquisition.
Only `Ungated` and `Licensed` permit acquisition.

For available content, retain exact authored ids and first-licence-in-catalog
precedence, including the existing missing-wallet refusal for a gated species.
Do not add species-side licence fields or change prices, preferences or stock timing.
The legacy `MayLand` helper retains its null-catalog allowance for compatibility;
its Fishing callers must migrate explicitly with controller tests in the adapter PR.
This PR does not claim to fix that runtime fallback.

## Saved bait

Use `PlacedTrapService.ResolveSavedBait` to distinguish `Unbaited`, `Resolved`,
`MissingCatalog` and `UnknownId`. Only a null/empty saved id means unbaited. A nonempty
id needs the exact original authored profile, including its species preferences.
An absent/empty registry cannot resolve a nonempty id. Matching is case-sensitive;
never normalize ids or substitute another bait. These queries do not consume stock,
rewrite DTOs, resolve catch, publish events or rebuild live traps.

Chosen restore policy for the future adapter: preflight **all** saved bait ids before
clearing any live trap or publishing any restore event. On either content failure,
abort the whole trap restore, retain the original DTOs and existing live objects,
and surface the failing trap instance and bait id as a typed diagnostic. Restore can
be retried when the catalog is repaired; it must produce the original catch list at
the original seed/time. Do not skip just the affected baited trap or downgrade it to
unbaited. A cold load with no previous live world stays unavailable until repaired.
No alternate save schema, automatic save, or migration rewrite is authorized here.

The current `RestoreFromSave` still has its audited legacy fallback. Atomic restore
and its composition failure presentation belong to the adapter migration, which must
test that failure leaves all live objects, DTOs, stock and event counts unchanged.

## Regression proof

The new EditMode cases cover explicit decisions, exact authored mapping and profile
identity, first-match precedence, missing wallets, repeated read-only resolution and
case-sensitive unknown ids. Existing `SaveLoad_YieldsIdenticalCatchList`, exact bait
expenditure, licence-gate and pot-stock tests remain intact. No rule assertion was
weakened or migrated. Assets and tunable values remain byte-identical.
