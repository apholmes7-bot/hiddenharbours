# ADR 0003 — Data-Driven Content via ScriptableObjects

- **Status:** Accepted
- **Date:** 2026-06-17
- **Decision owner:** lead-architect

## Context

The game needs **100 fish**, many boats, regions, NPCs, recipes, facilities, and a lot of balance
tuning — produced by **many agents in parallel**, and tuned by a **non-developer owner** who
should not have to edit code to change a price or a spawn rate.

## Decision

**All game content is data**, authored as **ScriptableObject Defs, one entity per file**, with
stable string ids. Code consumes Defs; code does not hard-code content.
(See `architecture/data-model.md` for the catalog and `architecture/project-structure.md` §4.)

## Rationale

1. **Parallel-safe.** Adding the 100th fish is a *new file*, never an edit to a shared list, so
   content agents never collide in version control.
2. **Owner-tunable.** Prices, tide constants, day length, spawn weights live in Def assets /
   `GameConfig`, editable in the Unity Inspector without touching C#.
3. **Testable & validatable.** A content-validation test can check every Def for unique ids,
   resolvable references, and required fields — catching content errors before they ship.
4. **Engine-soft.** Content as data keeps the door open to a future engine port (ADR 0001) and
   keeps gameplay logic decoupled from specific assets.

## Consequences

- Each content type gets a `[CreateAssetMenu]` Def class owned by the relevant module.
- Cross-content references use **ids resolved via `ContentDatabase`** across module boundaries.
- ids are **append-only and stable** once shipped (rename = new id + migration).
- `tools-editor` builds authoring aids (bulk editors, validators, spawn-table previews) so content
  authoring stays fast as the catalog grows.

## Trade-offs

- Slightly more upfront plumbing than hard-coding early content. Accepted — it pays for itself the
  moment content volume or parallelism rises (immediately, for us).
- Designers/agents must follow the one-file-per-entity + id discipline; enforced by review and the
  validation test.
