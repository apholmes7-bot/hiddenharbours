# Multi-Agent Coordination Protocol

> How a team of specialized AI agents builds Hidden Harbours in parallel **without corrupting each
> other's work**. This is the rulebook the roster in `agents/*.md` operates under. Read with
> `../CLAUDE.md` and `../docs/architecture/project-structure.md`.

Agents don't chat in real time. Coordination happens through **four durable artifacts**: the
**backlog** (what to do), **file/area ownership** (who may touch what), **pull requests** (proposed
changes + review), and **ADRs/doc updates** (decisions). Keep these clean and the team scales.

---

## 1. Ownership map (who may edit what)

Primary ownership prevents collisions. You may always *read* anything; you may *edit* your owned
areas freely; editing someone else's area needs a heads-up + their review on the PR (see §6).

| Area / path | Primary owner | Notes |
|-------------|---------------|-------|
| `Assets/_Project/Code/Core/` | **lead-architect** | shared contracts; changes ripple — review-heavy |
| `Assets/_Project/Code/Environment/` | gameplay-systems | clock, tide, weather, EnvironmentService |
| `Assets/_Project/Code/Boats/`, `Fishing/`, `Player/` | gameplay-systems | physics, navigation, fishing, controller, stamina |
| `Assets/_Project/Code/World/` | world-content | regions, scene flow, NPC, routines, quests |
| `Assets/_Project/Code/Economy/` | economy-sim | market, business, staff, production, logistics |
| `Assets/_Project/Code/UI/` | ui-ux | HUD, menus, screens, input mapping |
| `Assets/_Project/Code/Audio/` | audio | audio director, adaptive music/sfx |
| `Assets/_Project/Data/Fish/`, `Commodities/`, `Recipes/`, `Staff/` | economy-sim (+ world-content for flavor) | one entity per file |
| `Assets/_Project/Data/Regions/`, `NPCs/` | world-content | one entity per file |
| `Assets/_Project/Data/Boats/`, `Gear/`, `Bait/` | gameplay-systems | one entity per file |
| `Assets/_Project/Data/Config/` (`GameConfig`) | economy-sim + lead-architect | balance/tunables; owner-facing |
| `Assets/_Project/Scenes/<Region>.unity` | world-content | **one scene per region** — main anti-collision device |
| `Assets/_Project/Scenes/Bootstrap.unity` | lead-architect | persistent services |
| `Assets/_Project/Art/**` | art-pipeline | LFS-tracked |
| `Assets/_Project/Audio/**` | audio | LFS-tracked |
| `Assets/_Project/Code/**/Editor/`, `Tools.Editor` | tools-editor | authoring aids, validators |
| `Assets/Tests/**` | qa-test (+ each role adds tests for its code) | EditMode/PlayMode |
| `docs/**` | the role that owns the system (canon = lead-architect) | update docs in the same PR as the change |

**The two structural guarantees that make parallel work safe:**
1. **Scene per region** → two world agents on two regions never touch the same `.unity` file.
2. **One entity = one data file** → adding the 100th fish never edits a shared list.

When you genuinely must change shared code in `Core/`, keep it additive (new interface/event),
coordinate via the PR, and tag `lead-architect`.

### 1.1 Shared seams (legitimately co-owned — agree before diverging)
A few things are touched by more than one role. To stop them drifting, the seam is named here:
- **`GameConfig`:** `lead-architect` owns its **shape** (which fields exist); `economy-sim` owns the
  **values** (balance); `tools-editor` builds the owner-facing **tuning UI** over it. New fields land
  via a PR `lead-architect` reviews.
- **Content-validation rule** (unique ids / resolvable refs / required fields): authored **once by
  `qa-test`** as the single source of truth; `tools-editor`'s in-editor validator **calls that same
  rule**, never a forked copy.
- **NPC ↔ Staff seam** (a character who is both a townsperson and hireable): `world-content` owns
  their personality/routine/friendship; `economy-sim` owns their employment/automation side; they
  share **one `id`** and co-design the handoff. (Design docs flag this as an open question — resolve
  it together, don't invent a unilateral rule.)
- **Water/fog/lighting:** `art-pipeline` authors the look; the URP shader/rendering plumbing is owned
  by `lead-architect` unless reassigned. Tune together.

## 2. The work cycle

```
 pick → claim → branch → build → test → self-review → PR → review → merge → (main stays green)
```

1. **Pick** the top **unblocked** item for your role from `backlog/` (see each item's `owner`).
2. **Claim** it so no one doubles up: set the backlog item / task to in-progress with your role as
   owner (the project task list is the live claim board). If two agents could overlap, the one who
   claims first holds it; the other picks the next item.
3. **Branch** off `main`: `type/short-desc` (`feat/`, `fix/`, `art/`, `docs/`, `test/`, `tool/`).
4. **Build** to the acceptance criteria, obeying `../CLAUDE.md` §3 (the ten rules).
5. **Test & validate** (see §4).
6. **Self-review** against the Definition of Done (§3).
7. **PR** with the template; small and single-purpose (see §6).
8. **Review → merge.** Keep `main` always shippable (§5).

## 3. Definition of Done (a change isn't "done" until all true)
- [ ] Meets the backlog item's **acceptance criteria**.
- [ ] Obeys the ten rules in `../CLAUDE.md` §3 (data-driven, scene/module boundaries, determinism,
      no magic numbers, mobile budget, in-phase).
- [ ] **Tests:** new logic has EditMode tests; determinism-sensitive logic has a determinism test;
      integration has a PlayMode test where reasonable. All tests pass locally.
- [ ] If `Data/` changed: the **content-validation test passes** (unique ids, resolvable refs).
- [ ] No stray files committed (`Library/`, builds, secrets); new binaries are **LFS-tracked**.
- [ ] **Docs updated** in the same PR (the relevant `design/`/`architecture/` doc, and the canon if
      a locked fact changed — change canon *first*).
- [ ] Leaves a **working playable build**.
- [ ] PR description filled from the template; scoped and readable.

## 4. Testing & validation responsibilities
- Every role **writes tests for its own code** (`qa-test` owns the framework, shared fixtures, and
  the content-validation test, and runs the playtest/acceptance pass per milestone).
- **Determinism tests are sacred:** tide height / weather / market price for a given
  `(seed, gameTime)` must be reproducible. These guard the save system.
- Post-M0, CI (GameCI on GitHub Actions) builds + runs tests on every PR; red CI blocks merge.

## 5. Keeping `main` green (integration)
- `main` is **always shippable**. Work happens on short-lived branches; merge often in small pieces
  to avoid long-running divergence.
- **Before opening a PR, update your branch from `main`** (rebase or merge). Resolve scene/prefab
  conflicts with **UnityYAMLMerge** (configured per machine — `project-structure.md` §6); if two
  agents edited the *same object* in a scene, smart-merge can't auto-resolve — coordinate and
  re-author the smaller change.
- A green build + passing tests is the merge gate. Never merge over failing tests "to fix later."

### 5.1 Git & push hygiene (one feature per push)
Learned the hard way (the VS-22 work rode along on an unrelated push): a plain `git push` of `main`
sends **every** local commit on `main` — so if another agent had merged work locally that wasn't
pushed yet, your push publishes it too. To keep pushes scoped and history clean:
- **Pull first.** `git pull --ff-only origin main` (or fetch + rebase) before starting any task, so
  you begin from the latest `main`.
- **Branch always.** Do the work on a short-lived `type/short-desc` branch — never commit straight to `main`.
- **Land via the PR, not via local main.** Merge with `gh pr merge --squash --delete-branch` (or the
  GitHub merge button). Do **not** `git merge <branch>` into local `main` and then `git push main` —
  that's exactly what drags sibling agents' unpushed work along.
- **Look before you push.** Run `git log --oneline origin/main..HEAD` and confirm every commit listed
  is yours. If another agent's commit appears, stop and flag it to the owner — never rewrite shared
  history (rebase/force-push of pushed commits) without their explicit say-so.
- **Keep your tree clean.** Commit or stash your own changes before switching branches, so an
  unrelated edit isn't swept into someone else's commit.

## 6. Pull requests & review
- **Small and single-purpose.** One backlog item per PR where possible. Big PRs get split.
- **Reviewers:** `lead-architect` reviews anything touching `Core/`, architecture, or cross-module
  boundaries; the **domain owner** reviews changes in their area; `qa-test` reviews test coverage on
  risky changes. A PR editing someone else's owned area needs **that owner's review**.
- Use `../.github/pull_request_template.md`. State which backlog item, which pillar(s), how you
  tested, and any doc/canon updates.

## 7. Cross-cutting requests & handoffs
Some work spans roles. Handle it as an explicit, asynchronous handoff — never reach across:
- **Need art/audio you don't own?** Use a **placeholder** now (greybox sprite / silent stub) and
  file a backlog item for `art-pipeline`/`audio` describing the asset (size, states, mood). Don't
  block; don't author it yourself.
- **Need a capability from another code module?** Don't call into their concrete classes. Propose a
  **Core contract** (interface/event), get `lead-architect` review, then both sides implement
  against it. (`project-structure.md` §5)
- **Need a tunable exposed?** Ask `economy-sim`/`lead-architect` to surface it in `GameConfig`.
- **Found a content gap (e.g., a fish needs a new gear type)?** File a backlog item for the owning
  role rather than adding it in their folder.

## 8. Decisions & escalation
- **Small calls:** make them, document in the PR.
- **Architectural/cross-cutting calls** (engine, save format, perspective, a new shared system):
  write or update an **ADR** in `docs/adr/`; `lead-architect` arbitrates technical ties.
- **Scope / "should we build this" calls:** belong to the **roadmap** and the **owner**. If work
  isn't in the current phase, log it as a future backlog item and move on — don't expand scope
  unilaterally (`../CLAUDE.md` rule 8).
- **Canon conflicts:** `docs/vision-and-pillars.md` wins. If it's wrong, fix canon *first* (with
  owner awareness for big creative changes), then propagate.

## 9. Milestone cadence
Per `docs/roadmap.md`: build to the current milestone's Definition of Done, then **stop and let the
owner playtest** before committing the next milestone's budget. M0 (greybox fun-check) is the most
important gate — treat "is the core loop fun?" as a real go/no-go, not a formality.

## 10. TL;DR for a new agent
Read `../CLAUDE.md` → read your `agents/<role>.md` → read the canon and the docs your role owns →
pick the top unblocked item for your role in `backlog/` → claim it → branch → build to acceptance
criteria → test → PR. Stay in your lane (ownership map §1), talk through Core/EventBus, keep
content as data, and leave the build playable.
