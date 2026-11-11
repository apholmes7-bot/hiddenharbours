# Landed catch state research

This editor-only plain-class experiment separates life from quality without registering a gameplay system. It follows the [RULED 2026-11-11 goods choices](../../backlog/proposed-goods-handling.md#ruled-handling-choices--2026-11-11): live shellfish and still fish. That decision settles the handling direction; it supplies no species facts, sprites, content assets, biological durations or shipping tuning. The current wet-bucket policy and all current catch, sale and save paths continue to use their existing code.

## Inputs and rules

`LandedCatchProfile` accepts stable profile/species ids, whether the catch lands alive, compatible live holding, optional maximum accumulated exposure outside live holding, and the species spoil rate. `LandedCatchSettings` accepts the configured seconds per game day and existing Core `SpoilPolicy`. All durations are **game seconds**, matching `IGameClock.TotalSeconds`. No clock or RNG is read; identical initial state, times and storage transitions produce identical output.

- `Life` is alive or dead; `IsStill` means dead/still handling, not an animation selection. Alive does not promise motion or sale eligibility.
- Compatible `Live` storage pauses accumulated exposure; returning to it does not erase earlier exposure. Missing species survival data permits compatible live holding but refuses live ambient/iced placement. It does not invent a mortality timer. Freezing and an explicit `StopLife` transition remain possible.
- Ice preserves freshness for a caller-supplied absolute depletion instant; it does not become live habitat. In this research model, a living catch on ice uses its authored ambient exposure budget. Species-specific iced live handling would require a separate reviewed policy before implementation.
- At the exposure boundary the catch becomes dead. Freezing makes it dead immediately. No operation revives it. When life is explicitly stopped in live holding, freshness changes to ambient at that instant; ice/frozen protection remains independent of life.
- Quality and the spoil-based sale refusal reuse Core `Freshness`, `WithMode` and `SettleThroughProtection`. Storage changes bank spoil first. At the exact ice boundary the returned mode is ambient. Putting warm catch on ice or in a freezer never improves its banked quality. This is a game freshness read, not a food-safety assessment or a buyer's complete acceptance policy.

`Advance` returns an immutable snapshot. Reads retain the last storage transition's time and freshness anchors, so reading repeatedly does not compound rounding. Actual storage transitions bank through Core's existing float accumulator. Backward/nonfinite time, unsupported live holding, invalid rates and malformed ice expiry are rejected without changing the source object. The caller owns physical transfer validation, habitat suitability and the container's ice budget; passing an expiry never purchases ice or moves an item.

## Isolation and verification

The model sits in `Tools/Editor/LandedCatch`, in a non-auto-referenced Editor-only assembly with no engine API references. Its only project dependency is Core. No runtime assembly references it, and it has no Unity component, initializer, Def asset, save DTO or scene hook. Its NUnit suite lives in a separate EditMode test assembly. This keeps later-phase mortality research out of the shipped M1/M2 loop.

The three test fixtures are `LandedCatchLifeTests`, `LandedCatchQualityTests` and `LandedCatchValidationTests`. They pin exact boundaries, rejected inputs, retained history, immutability, read-cadence independence and deterministic replay. All numeric profiles are synthetic test inputs, not Atlantic species survival claims. Run the namespace filter `HiddenHarbours.Tests.LandedCatch` in EditMode and verify a nonzero count for **each** fixture. Local standalone .NET execution of the same sources does not claim a Unity assembly-import or editor result.

Before any runtime integration: obtain architecture/economy review, author validated per-species profiles and container rules, persist life/exposure/cooling through the approved future schema, prove sleep/reload/region continuity, and route presentation to the art lanes. This experiment authorizes none of those changes.
