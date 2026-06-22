# CLAUDE.md — Operating Manual for Hidden Harbours

**You are an AI agent working on _Hidden Harbours_, a PC-first 2D pixel-art fishing & trade
RPG built in Unity 6.3 LTS (mobile kept as a viable later port).** Read this file first, every session. It is short on purpose and
links to the detail. If anything here conflicts with another doc, **`docs/vision-and-pillars.md`
(the canon) wins**, then this file, then the rest.

---

## 1. What you're building (in one breath)
You inherit your uncle's dory on a North Atlantic island and grow from hand-lining fish to
commanding a cargo fleet — reading tides, wind, and a living market. Cozy, but the sea is
dangerous. Full pitch + the five pillars: **`docs/vision-and-pillars.md`**. Every change must
serve at least one pillar (P1 Sea Has Moods · P2 Dory to Dynasty · P3 Living Working Coast ·
P4 Earn It Then Automate It · P5 Cozy but with Teeth). If it serves none, don't build it.

## 2. Stack & ground truth
- **Engine:** Unity **6.3 LTS** (6000.3.x), **2D URP**, **C#**. **PC-first** (Windows/desktop;
  landscape; KB/mouse + gamepad), **mobile/console later (mobile kept as a viable port)**. Pin the
  editor version; don't upgrade without `lead-architect` sign-off. (ADR 0005)
- **Architecture:** `docs/architecture/tech-architecture.md`
- **Repo & code layout:** `docs/architecture/project-structure.md`
- **Data model:** `docs/architecture/data-model.md`
- **Decisions (ADRs):** `docs/adr/` — engine choice, procedural-vs-handcrafted, data-driven, scenes, PC-first target
- **What to build & in what order:** `docs/roadmap.md`, `backlog/` ← **start here for work**

## 3. The ten rules (non-negotiable)
1. **Read the canon and your role charter before coding.** Your role: `agents/` (see §6).
2. **Content is data, not code.** New fish/boat/region/NPC/recipe = a new ScriptableObject asset,
   **one entity per file**, with a stable `id`. Never hard-code content. (ADR 0003)
3. **One perspective:** ¾ top-down, KTC mood. **Scene per region, loaded additively.** Author as
   **prefabs**; never build "one giant scene". (ADR 0004)
4. **Cross-module talk goes through Core** (interfaces + EventBus). A feature module never
   references another feature module's concrete classes. (`project-structure.md` §5)
5. **Simulation is deterministic** from `(worldSeed, gameTime)`. Tide/wind/weather are
   *recomputed, not saved*. Don't add hidden global randomness to sim systems.
6. **No magic numbers.** Balance/tunables live in Def assets / `GameConfig`, editable by the owner.
7. **Performance budget is a feature.** Target **60fps on a typical desktop/laptop GPU** (the
   PC-first baseline). Keep the discipline that also keeps mobile portable: pool objects, mind draw
   calls, throttle heavy sim to the slow tick, no per-frame HUD allocations, mind texture memory —
   don't paint the later mobile port into a corner. (ADR 0005)
8. **Stay in your phase.** Don't build M3 systems during M0. If the backlog doesn't ask for it
   yet, raise it — don't sneak it in. (`docs/roadmap.md`)
9. **Version control discipline:** binaries via **Git LFS**, scenes/prefabs via **smart merge**,
   **Force Text** serialization. Never commit `Library/`, `Temp/`, builds, or secrets.
10. **Leave a working build.** Don't merge anything that breaks the playable build or the tests.
    CI isn't an automated gate on this repo — **observe CI green before you merge** (`coordination.md` §5.2).

## 4. How to do a piece of work
1. **Pick** the top unblocked item for *your role* from `backlog/` (M1 detail lives in
   `backlog/milestone-1-vertical-slice.md`). Claim it (see `agents/coordination.md`).
2. **Branch:** `type/short-desc` (`feat/dory-controller`, `fix/tide-drift`).
3. **Build** to the item's **acceptance criteria**, following your role charter and these rules.
4. **Test:** add/along EditMode tests for logic & determinism, PlayMode for integration. Run them.
   Run the content-validation test if you touched Data.
5. **Self-review** against §3 and the item's acceptance criteria. Update any affected doc.
6. **Open a PR** using `.github/pull_request_template.md`. Keep PRs small and single-purpose.
7. **Definition of Done** is in `agents/coordination.md`. Don't mark done until it's all true.

## 5. Conventions (quick)
- Namespaces `HiddenHarbours.<Module>`; PascalCase types/methods, `_camelCase` private fields.
  Full table: `project-structure.md` §7. Style enforced by `.editorconfig`.
- Def ids: `type.snake_case` (`fish.atlantic_cod`). Ids are **append-only & stable**.
- Keep gameplay logic engine-light and testable (POCOs + services) where practical.
- Commit messages: imperative, scoped (`feat(boats): add rudder authority by speed`).

## 6. Who does what (the agent roster)
Each role has a charter in `agents/` with its mission, owned folders, and guardrails. Coordination,
ownership map, branching, and Definition of Done: **`agents/coordination.md`**.

| Role | Owns (high level) |
|------|-------------------|
| `lead-architect` | architecture, Core, integration, ADRs, reviews, release/build |
| `gameplay-systems` | boats/physics/navigation, fishing, time/tide/weather services, player controller |
| `world-content` | regions, scenes, tilemaps, NPCs & routines, quests, dialogue |
| `economy-sim` | market, business, staff/automation, production, logistics, economy state |
| `ui-ux` | HUD, menus, market/management UI, mobile input mapping |
| `art-pipeline` | sprites, tilesets, animation, palette, import settings, water/lighting |
| `audio` | music, SFX, ambient, adaptive audio |
| `qa-test` | test framework, content validation, playtest, acceptance, CI |
| `tools-editor` | custom editor tooling, content authoring aids, build/deploy scripts |

## 7. Note for the owner (non-developer)
You steer; agents build. The best leverage points: (a) play the **M0 greybox** loop and say if
it's *fun* before we spend on art (`docs/roadmap.md` → "How the owner steers"); (b) tune feel via
`GameConfig`/Def assets — no code needed; (c) approve PRs and ADRs. You do **not** need to read the
code to direct this project well — read the canon, the roadmap, and the PR summaries.

## 8. Common mistakes to avoid (seen these before)
- Building a giant single scene → merge hell. **Scene per region.**
- Hard-coding a fish/price/boat stat in C# → un-tunable + merge conflicts. **Make it data.**
- Reaching into another module's classes → coupling. **Go through Core/EventBus.**
- Saving tide/weather → bloated, fragile saves. **Recompute from seed+time.**
- Committing a `.png`/`.wav` not covered by LFS → bloated repo. **Check `.gitattributes`.**
- Implementing a cool later-phase idea now → scope creep. **Stay in your phase; log the idea.**
