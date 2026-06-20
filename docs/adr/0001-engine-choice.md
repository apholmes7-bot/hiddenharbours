# ADR 0001 — Game Engine: Unity 6.3 LTS

- **Status:** Accepted
- **Date:** 2026-06-17
- **Decision owner:** lead-architect (recommendation by request of the owner)

## Context

Hidden Harbours is a **mobile-first 2D pixel-art RPG** with deep systems, to be built **largely
by AI coding agents directed by a non-developer owner**, and later ported to desktop/console. The
owner's instinct was Unity and asked for an honest recommendation rather than a rubber stamp. The
realistic candidates are **Unity 6.3 LTS** and **Godot 4.5**.

## What the research said (June 2026)

- **Unity 6.3 LTS** is current, supported through **Dec 2027**, with a new **Box2D-v3** 2D physics
  backend (multi-threaded, more deterministic) — directly useful for our wind/tide/boat forces —
  plus mobile-optimised 2D rendering. The **Runtime Fee was cancelled** (Sept 2024); **Unity
  Personal is free up to $200K** annual revenue/funding, so it is free at this project's scale.
- **Godot 4.5** is genuinely excellent for 2D: a **dedicated 2D pipeline**, **MIT-licensed and
  free forever with no revenue cap**, **tiny build sizes**, and **much faster iteration** (instant
  launch, fast reimport). Its weaknesses are a **far smaller asset/plugin ecosystem** and a
  **much smaller training corpus**.

## Decision

**Use Unity 6.3 LTS (2D URP), mobile-first.**

## Why — weighted to *this* project

1. **AI-agent code quality is the deciding factor.** The build model is "AI agents, owner
   directs." AI agents write **Unity C#** far more reliably and with fewer hallucinations than
   Godot's GDScript, because the public training corpus of Unity C# is enormous by comparison.
   For an owner who can't easily debug, fewer agent mistakes is worth more than any engine feature.
2. **Ecosystem leverage.** Unity's Asset Store offers battle-tested building blocks (input,
   save, dialogue, tilemap, mobile UI, pathfinding) a non-developer can lean on instead of
   commissioning agents to build everything from scratch.
3. **Learning resources.** Vastly more tutorials, courses, and Q&A — important even when agents do
   the typing, because the owner will need to understand and make calls.
4. **Mobile + physics fit.** Unity's mobile export is proven at scale, and Box2D-v3 is a strong fit
   for the signature boat-against-wind-and-tide handling (P1).
5. **Cost is a non-issue** at hobby/indie scale (free under $200K), and the licensing drama is
   resolved.

## What we give up (Godot's real advantages)

Faster iteration, smaller builds, zero licensing risk at any revenue, and a cleaner native 2D
pipeline. These are real. We accept them because the **agent-corpus and ecosystem advantages
matter more for this team and this owner.**

## When we would reconsider

- If iteration speed or Unity's heaviness becomes a daily pain **and** the owner shifts toward
  hands-on coding (reducing the corpus advantage), **or**
- If the project's revenue ever approached Unity's paid thresholds in a way that made Godot's
  zero-royalty model materially valuable.
  In either case, the **data-driven, engine-agnostic design** (ADR 0003) keeps a future port less
  painful than it would otherwise be — but a port is still a significant cost and not planned.

## Consequences

- Target **Unity 6.3 LTS (6000.3.x)**, **2D URP**, C#. Pin the editor version for all agents.
- **Use the LTS stream, not interim Update releases.** Unity 6.3 LTS is supported with fixes to
  **Dec 2027**. Update releases (6.4, 6.5, …) ship newer features but are only supported until the
  *next* release and carry *planned breaking changes* — a poor fit for a long, agent-built project
  where API churn is expensive and hard for a non-developer to absorb. Revisit the version only at
  the next annual **LTS**, with `lead-architect` sign-off (CLAUDE.md rule 2/8).
- Keep gameplay logic **engine-light** where practical (POCOs, deterministic services) so it's
  testable and not needlessly wedded to Unity APIs.
- Mobile performance budgets are a first-class constraint from M0 (`tech-architecture.md` §7).

## Sources
- [Unity 6.3 LTS is now available](https://unity.com/blog/unity-6-3-lts-is-now-available) · [Unity 6 support/releases](https://unity.com/releases/unity-6/support)
- [Unity is Canceling the Runtime Fee](https://unity.com/blog/unity-is-canceling-the-runtime-fee) · [Unity pricing updates](https://unity.com/products/pricing-updates)
- [Godot 4.5 vs Unity 6.3 for 2D in 2026 (comparison)](https://gamineai.com/blog/godot-4-5-stable-vs-unity-6-3-for-2d-games-2026)
