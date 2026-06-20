# ADR 0002 — Procedural vs Handcrafted World

- **Status:** Accepted
- **Date:** 2026-06-17
- **Decision owner:** lead-architect + world-content

## Context

The owner asked whether the map and NPCs should be procedural. Two pillars pull on this: **P3 A
Living Working Coast** wants authored identity and memorable, inhabited places; **P2 From Dory to
Dynasty** and replayability want scale and variety that's expensive to author by hand. Fully
procedural risks a generic, soulless world; fully handcrafted risks an enormous content bill and
thin late-game variety.

## Decision

**Hybrid, split along a clear seam: author identity, simulate variety.**

| Handcrafted (authored) | Procedural / simulated |
|------------------------|------------------------|
| Region macro-layouts, landmarks, coastlines | Fish spawn distribution within a region |
| The named **core NPC cast** + their routines, quests, relationships | Tide-pool & flats contents, flotsam, beachcombing |
| Port Greywick, the town, key buildings | Weather, wind fields, sea state, fog |
| Story beats, unlock gating | "Extra" background NPCs (crowd/dock workers) appearance, names, schedules |
| The signature tide-revealed set-pieces (Drownded Lands, Sunkers) | An **optional endless outer-banks zone** for late-game fishing variety |

## Rationale

- The places you return to and the people you know are **authored** — that's where a sense of
  place and story live (P3). Procedural towns and procedural lead NPCs read as hollow in an RPG.
- The things you want *fresh each day* — what's biting, the weather, who's milling around the
  wharf — are **simulated**, which is cheaper to produce and directly serves "the sea has moods"
  (P1) and long-term variety (P2).
- This also matches the data-driven approach (ADR 0003): authored content is Defs + scenes;
  simulated content is rules over those Defs.

## Consequences

- `world-content` hand-builds region scenes and the core cast; `gameplay-systems`/`economy-sim`
  build the spawn/weather/market simulations that fill them.
- The "extras" NPC generator and the optional outer-banks generator are explicitly **later-phase**
  (M2+), not M0/M1 — the vertical slice is fully authored.
- Determinism (seed + time) gives procedural elements *lawful* behavior players can learn, instead
  of pure randomness.

## Open questions
- How far the optional endless outer-banks zone goes (full procedural region vs reskinned
  spawn-only) — defer to M4 planning.
