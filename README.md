# Hidden Harbours

A cozy-but-dangerous **pixel-art fishing & trade RPG** set on a North Atlantic island. Buy a damaged
dory with clam money and put her right, read the tides and the wind, and grow from hand-lining cod to
commanding a cargo fleet. Built in **Unity 6.5 (6000.5)**, **PC-first** (mobile kept as a later
port — ADR 0005), and structured to be developed by a team of **AI agents** directed by the owner.

> **Status (2026-09-01):** In production. St Peters, Nine Mile Creek and the West Water are
> committed, hand-authored scenes; the fleet is real-time mesh from the dory to the tanker; boats
> have interiors you walk into; a road fleet drives the mainland; the sea is one deterministic wave
> field with a displaced surface, breaking surf, day/night and lights that touch the water.
> Work runs as **owner-directed arcs** on top of the milestone phasing — see `docs/roadmap.md`
> §0.5 "Where we are" for the snapshot and `docs/adr/README.md` for every decision. CI runs the
> EditMode + PlayMode suites on every PR (no GPU there; render suites are verified locally).

---

## 60-second tour of this repo

| Path | What's there |
|------|--------------|
| **`docs/vision-and-pillars.md`** | **Read this first.** The canon: pitch, the 5 pillars, and every locked name/scale/decision. |
| `docs/design/` | The detailed design — world & regions, tides/weather, boats, the 100-fish system, economy & business, NPCs, progression & housing, art bible, water rendering, interiors, vehicles, and more. |
| `docs/architecture/` | The technical backbone — project structure (as it really is), system architecture, data model. |
| `docs/adr/` | The decisions and *why* — 41 ADRs; `docs/adr/README.md` is the index with status. |
| `docs/art/rigs/` | The art director's rig sources (JS) and gameplay sidecars — baked in-engine (ADR 0021). |
| `docs/roadmap.md` | The phased plan (M0 → M4) with a scope-reality talk and the 2026-09-01 status snapshot. |
| `backlog/` | The work: `backlog.md` (M0–M4 epics), `plan-to-m1.md` (the current M1 audit and route), `milestone-1-vertical-slice.md` (the original slice spec). |
| **`CLAUDE.md`** | The operating manual every AI agent reads first. |
| `agents/` | The agent team: the roster, the coordination protocol, and a charter per role. |
| `Assets/_Project/` | The Unity project — `Code/` (Core, Environment, Boats, Vehicles, Fishing, Economy, World, Player, UI, Audio, Art, App, Tools), `Data/` (one entity per file), `Art/`, `Scenes/`, `Prefabs/`. |
| `Assets/Tests/` | EditMode (logic, determinism, render guards) and PlayMode (journeys) suites. |
| `HANDOFF-*.md` (untracked) | Paste-ready lane charters the coordinator writes and the owner deploys. |
| `SETUP-UNITY.md` | The original June 2026 scaffold walkthrough — historical; the project is now opened from its committed scenes. |

## For the owner (Alex) — how to use this

You don't need to read the code to run this project well. Your highest-leverage moves:
1. **Read the canon** (`docs/vision-and-pillars.md`) and the **roadmap** (`docs/roadmap.md`).
2. **Deploy lanes** by pasting the coordinator's handoffs into sessions; approve their PRs and the
   big decisions (ADRs). Each agent knows its job from `agents/` + `CLAUDE.md`.
3. **Play the build and rule on the eyeball gates** — every arc stops for your eye before it
   merges; your sentence is the acceptance test.
4. **Tune the feel yourself** — prices, tide strength, day length, the sea's push and more live in
   editable data assets (`GameConfig`, `Water.mat`), no coding required.

## Getting started

Open the repo root as a Unity **6000.5.0f1** project (Git LFS installed first — see
`docs/architecture/project-structure.md` §6), open `Assets/_Project/Scenes/StPeters.unity` and
press Play: the persistent core is baked into the start scene. Region scenes (`NineMileCreek`,
`WestWater`) can be played directly too — each carries a dev bootstrap. `SETUP-UNITY.md` is the
historical scaffold guide.

## For agents
Read **`CLAUDE.md`**, then your charter in **`agents/`**, then the canon and the docs your role
owns. Work from the handoff you were given (or the top unblocked item for your role in `backlog/`).
Respect the ownership map and Definition of Done in `agents/coordination.md`. Stop at PR-open —
the coordinator merges.

## Stack
Unity 6.5 (6000.5.0f1) · 2D URP 17.5 · C# · PC-first (Windows desktop; KB/mouse + gamepad),
mobile/console later · data-driven (ScriptableObjects) · deterministic simulation from
`(worldSeed, gameTime)` · in-engine JS rig baking (embedded V8) · Git + Git LFS.

## License
TBD — set before any public release. (Personal/closed during development.)
