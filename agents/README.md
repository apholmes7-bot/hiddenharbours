# The Hidden Harbours Agent Team

This folder defines the **roster of specialized AI agents** that build Hidden Harbours, and the
rules they work under. It's the "org chart + job descriptions" for a team where every teammate is
an AI agent and the owner (Alex) is the director.

## Start here
1. Read **`../CLAUDE.md`** — the operating manual (rules, stack, workflow).
2. Read **`coordination.md`** — how we work in parallel without collisions (ownership map,
   Definition of Done, PR/review, handoffs).
3. Read **your role charter** below.
4. Read the **canon** (`../docs/vision-and-pillars.md`) and the docs your role owns.
5. Pick the top unblocked item for your role in **`../backlog/`** and go.

## The roster

| Role | Charter | One-line mission |
|------|---------|------------------|
| Lead / Architect | [`lead-architect.md`](lead-architect.md) | Owns architecture, Core, integration, reviews; keeps the whole thing coherent and shippable. |
| Gameplay Systems | [`gameplay-systems.md`](gameplay-systems.md) | The sea, the boats, the fishing — time/tide/weather, navigation physics, the catch. |
| World & Content | [`world-content.md`](world-content.md) | Regions, scenes, NPCs and their routines, quests, dialogue — the living coast. |
| Economy & Simulation | [`economy-sim.md`](economy-sim.md) | The market, the business, staff & automation, production, logistics. |
| UI / UX | [`ui-ux.md`](ui-ux.md) | The HUD and menus, mobile controls, making complex systems glanceable on a phone. |
| Art Pipeline | [`art-pipeline.md`](art-pipeline.md) | Sprites, tilesets, animation, palette, water & light — the look, at the locked scale. |
| Audio | [`audio.md`](audio.md) | Music, SFX, ambience that breathes with weather and region. |
| QA / Test | [`qa-test.md`](qa-test.md) | The test framework, content validation, determinism guards, playtest & acceptance, CI. |
| Tools / Editor | [`tools-editor.md`](tools-editor.md) | Custom Unity tooling and content-authoring aids that keep everyone fast. |

## How to read a charter
Each charter states the role's **mission**, the **pillars** it most serves, what it **owns**, what
it **hands off**, the **docs to read**, its **per-phase focus**, and **guardrails**. Ownership is
the anti-collision contract — respect the boundaries in `coordination.md` §1.

> One agent can wear several hats on a small team. The roles are a division of *responsibility and
> ownership*, not a headcount requirement — but when two agents work at once, the ownership map is
> what stops them clobbering each other.
