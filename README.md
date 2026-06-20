# Hidden Harbours

A cozy-but-dangerous **pixel-art fishing & trade RPG** set on a North Atlantic island. Inherit your
uncle's dory, read the tides and the wind, and grow from hand-lining cod to commanding a cargo
fleet. Built in **Unity 6.3 LTS**, **mobile-first**, and structured to be developed by a team of
**AI agents** directed by the owner.

> **Status:** Pre-production, with a running **code scaffold**. The full design and multi-agent
> system are written, and the first systems are coded — the deterministic clock/tide/weather
> services and a drivable dory that responds to wind and tide. To turn this into a running Unity
> project, follow **`SETUP-UNITY.md`**. Next up: the rest of the **M0 greybox** fishing→sell loop.

---

## 60-second tour of this repo

| Path | What's there |
|------|--------------|
| **`docs/vision-and-pillars.md`** | **Read this first.** The canon: pitch, the 5 pillars, and every locked name/scale/decision. |
| `docs/design/` | The detailed design — world & regions, tides/weather, boats, the 100-fish system, economy & business, NPCs, progression & housing, art bible, mobile UX. |
| `docs/architecture/` | The technical backbone — project structure, system architecture, data model. |
| `docs/adr/` | The big decisions and *why* — engine choice, procedural-vs-handcrafted, data-driven content, perspective & scenes. |
| `docs/roadmap.md` | The phased plan (M0 greybox → M1 vertical slice → … → M4 dynasty) with a scope-reality talk. |
| `backlog/` | The work, broken into agent-ready items. `milestone-1-vertical-slice.md` is the first 31 tasks. |
| **`CLAUDE.md`** | The operating manual every AI agent reads first. |
| `agents/` | The agent team: the roster, the coordination protocol, and a charter per role. |
| `Assets/_Project/` | The Unity **code scaffold** — Core, Environment (clock/tide/weather), Boats (the dory), App. |
| **`SETUP-UNITY.md`** | Step-by-step: turn this into a running Unity project and drive the dory. |

## For the owner (Alex) — how to use this

You don't need to read the code to run this project well. Your highest-leverage moves:
1. **Read the canon** (`docs/vision-and-pillars.md`) and the **roadmap** (`docs/roadmap.md`).
2. **Direct agents** by pointing them at backlog items and approving their PRs and big decisions
   (ADRs). Each agent knows its job from `agents/` + `CLAUDE.md`.
3. **Playtest M0** the moment the greybox loop exists, and tell us honestly if it's *fun* before we
   spend on art. That go/no-go is the most important decision in the whole project.
4. **Tune the feel yourself** — prices, tide strength, day length and more live in editable data
   assets (`GameConfig`), no coding required.

## Getting started (when you're ready to build)

Follow **`SETUP-UNITY.md`** — it walks you, step by step, from installing Unity **6.3 LTS** through
creating the project, importing this code scaffold, wiring the Bootstrap scene, and **driving the
dory** so you can feel the wind and tide. Then hand an agent the top item in
`backlog/milestone-1-vertical-slice.md`.

## For agents
Read **`CLAUDE.md`**, then your charter in **`agents/`**, then the canon and the docs your role
owns. Pick the top unblocked item for your role in `backlog/`. Respect the ownership map and
Definition of Done in `agents/coordination.md`.

## Stack
Unity 6.3 LTS · 2D URP · C# · mobile-first (iOS/Android), desktop/console later · data-driven
(ScriptableObjects) · deterministic simulation · Git + Git LFS.

## License
TBD — set before any public release. (Personal/closed during development.)
