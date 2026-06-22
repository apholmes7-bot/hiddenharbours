# ADR 0008 — Save Schema v1 & Versioning: a self-installing `SaveService` behind a Core seam

- **Status:** **Accepted** — lead-architect. Delivers VS-08 (the save Definition-of-Done): the first
  concrete, versioned, migratable save schema, plus the autosave/load lifecycle.
- **Date:** 2026-06-21
- **Decision owner:** lead-architect (save format is an architectural/cross-cutting call —
  `agents/coordination.md` §8; `CLAUDE.md` rule 5 — the determinism dividend lives or dies here).
- **Related:** `architecture/tech-architecture.md` §2 (boot), §6 (save system); `architecture/data-model.md`
  §4 (mutable vs recomputed state); `0007-active-boat-heading-seam.md` (the same Core-mediated, no-new-
  signal discipline); `Core/Events/GameSignals.cs` (`BoatPurchased` / `ActiveBoatChanged`); backlog VS-08;
  the `OwnedFleet` `TODO(VS-08)` and the `FlagStore.cs` note ("a real save file replaces this at VS-08").

## Context

Everything before VS-08 persisted nothing except the three onboarding flags, via PlayerPrefs
(`PlayerPrefsFlagStore`, VS-21). Two things were explicitly waiting on this work: `OwnedFleet` carried a
`TODO(VS-08): persist the owned boat across save/load`, and `FlagStore.cs` documented that "a real save
file replaces this at VS-08." The architecture already commits (tech-architecture §6) to: **save only
what can't be recomputed** (tide/wind/weather regenerate from `worldSeed + gameTime`), **versioned +
migratable from M0**, **atomic writes**, and **autosave on app-suspend**.

The constraint that shaped the design: a save system reads/writes state owned by many modules, but this
change must stay in the **Core / integration lane** and not reach into the Boats/UI implementations.

## Decision

A schema-versioned `SaveService` in **Core**, behind an `ISaveService` seam, that captures state only
through existing contracts.

1. **`Core.SaveData` (schema v1)** — a flat, `JsonUtility`-serializable DTO:
   `SchemaVersion, WorldSeed, GameTimeSeconds (double), Money, DayIndex, OwnedBoats[] + ActiveHullId,
   OnboardingFlags[]`. Public fields, no behaviour. Field set is **append-only** — never rename/repurpose
   a shipped field (same rule as Def ids, data-model §5).
2. **Layered, independently testable internals** — `SaveSerialization` (JSON ⇄ DTO), `SaveStore`
   (atomic disk I/O: write `.tmp` → `File.Replace`), `SaveMigration` (forward-only upgrades). Each is
   pure/engine-light enough to unit-test without a scene.
3. **`Core.ISaveService` + `GameServices.Save`** — the running contract: `Current`, `GetFlag/SetFlag`,
   `Save()`. Optional/null-checked like `Wallet`/`ActiveBoat`.
4. **`Core.SaveService`** — a **self-installing** persistent `MonoBehaviour`
   (`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`, `DontDestroyOnLoad`): loads on launch, autosaves
   on `OnApplicationPause(true)` and `OnApplicationQuit`.
5. **Onboarding-flags consolidation** — `World.SaveFlagStore : IFlagStore` delegates to
   `GameServices.Save`, and the two `OnboardingFlags` construction sites switch from `PlayerPrefsFlagStore`
   to it. The flags now live in the one versioned slot and migrate with it.

### Capture through existing seams (staying in lane)

`SaveService` never mutates another module's objects to *read* state:

- **Money / gameTime / dayIndex / worldSeed** are pulled on demand at save time through `IWallet`,
  `IGameClock`, `IEnvironmentService` (all already had getters; `IGameClock` gained `DayIndex`, which is
  just the clock's existing private `TotalDays` promoted to the contract — removes a would-be magic
  `SecondsPerDay` in the save layer).
- **Owned boats / active hull** have no getter, so the service **listens** to the existing
  `BoatPurchased` / `ActiveBoatChanged` signals — reading the active-boat seam, adding **no new
  GameSignals** (the VS-08 brief's constraint, mirroring ADR 0007).

### Self-installing, not GameRoot-wired

The clock/environment/wallet are wired on the persistent `GameRoot`. The save service instead installs
itself before the first scene loads. Rationale: it needs **no scene references** (it pulls services that
register themselves), and self-install keeps the whole feature authorable **as text** — no edit to
`Bootstrap.unity`/`GreyboxBuilder`. It still reaches the rest of the game only through Core
(`GameServices`), so it doesn't usurp `GameRoot`'s composition-root role.

### Scope boundary: persist now, re-apply later

This change makes the state **durable and versioned** and round-trips it across save/load. **Re-applying**
loaded `gameTime`/`money`/`worldSeed`/fleet back into the live gameplay objects on load belongs to the
owning lanes (those `MonoBehaviour`s have no setters by design; adding them is gameplay-systems/Boats
work — e.g. `OwnedFleet`'s `TODO(VS-08)`), which read `ISaveService.Current`. Onboarding flags are the
exception and work **end-to-end now**, because the world reads them live through the save-backed store.

## Consequences

- **Zero new coupling.** The DTO + service are Core-owned; capture is via existing Core contracts/signals.
  No module references another's concrete classes; no new GameSignals.
- **Migratable from day one.** `v0→v1` is a no-op upgrade (empty fleet/flag lists + version bump; scalars
  untouched); the `if (SchemaVersion < N)` ladder is ready for the next field.
- **Robust loads.** Missing/corrupt/garbage JSON ⇒ "no save" ⇒ new game, never a crash; writes are atomic.
- **Cross-lane touches (flagged for owner review):** `World` (the two flag-store swaps + `SaveFlagStore`)
  for **world-content**; `Environment.GameClock` (`DayIndex`) for **gameplay-systems**. Both additive.
- **Tests:** EditMode covers the round-trip at sub-minute precision, the v0→v1 migration (object + JSON),
  owned-boat and onboarding-flag persistence across a real disk save/load, and corrupt/missing-file
  resilience.

## Rejected alternatives

- **Reflection / direct field-poking to restore live services** — fragile and opaque; violates the
  clean-seam discipline. Deferred restore through `ISaveService.Current` + lane-owned setters is honest.
- **A new `SaveRequested`/`GameLoaded` GameSignal** — the brief forbids new signals, and the existing
  `BoatPurchased`/`ActiveBoatChanged` already carry what the save needs to learn the fleet.
- **Keep onboarding flags in PlayerPrefs alongside the save** — two sources of truth that drift; VS-08's
  remit is precisely to consolidate them.
- **GameRoot-wired save service** — would need a `Bootstrap.unity` edit and a serialized scene ref for no
  benefit; the service has no scene dependencies, so self-install is simpler and text-authorable.

## Open questions (later)

- **Restore wiring (M1):** gameplay-systems/Boats apply `Current` (fleet re-grant, clock/seed/money
  restore) into the live objects on load.
- **One-time PlayerPrefs import:** migrating an existing player's VS-21 PlayerPrefs flags into the save
  on first v1 launch — skipped (greybox dev state); revisit if a real player save predates v1.
- **Multiple save slots / cloud** — out of scope; `ISaveService` keeps a backend swap behind the seam
  (tech-architecture §10).
