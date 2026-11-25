# Def identity additions

The test-only ledger reader combines `../DefIdentityLedger.json` with every `*.json`
file in this directory, including subdirectories. No manifest or generated aggregate is needed.
This initial directory contains no content registrations; every existing row stays in the baseline.

For a new asset, add `<asset-guid>.json` with exactly three string fields:

```json
{
  "Type": "HiddenHarbours.Fishing.FishDef",
  "Guid": "0123456789abcdef0123456789abcdef",
  "Id": "fish.example"
}
```

Copy the asset's existing Unity GUID from its `.asset.meta`; the example GUID and id are
illustrative. The filename must match `Guid`, using 32 lowercase hexadecimal characters.
Do not generate a replacement asset GUID. Its registration file gets its own, different `.meta` GUID.

For a new id-bearing Def type, also add `<full-type-name>.type.json`, even if it has no assets:

```json
{
  "Type": "HiddenHarbours.Fishing.ExampleDef"
}
```

The filename must match `Type` followed by `.type.json`. Existing types already declared
in the baseline need no second declaration. Fields are case-sensitive; extra, missing,
non-string and repeated fields are errors. Duplicate GUIDs, `(Type, Id)` pairs or type
declarations fail even when identical, and the diagnostic names both sources. Nothing is
silently deduplicated. An identity may precede its type file in enumeration order.

Commit each new file and its Unity `.meta`, following the adjacent importer shape; preserve
existing metadata. Run `DefIdentityLedgerTests`, `DefIdentityContractTests` and
`NmcH7ContentValidationTests`. CI runs the reflected asset inventory; text inspection alone
does not prove native type discovery or the thirteen pinned set-piece registrations.

See [content authoring](../../../../../docs/architecture/data-model.md#6-how-agents-add-content-safely-in-parallel).
Conversion of pending PRs and an enforced baseline freeze are separate integration work.
