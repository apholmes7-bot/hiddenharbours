# ADR 0011 — Committed, hand-authored scenes (the hybrid: builders own LOGIC, the owner paints the VISUALS)

- **Status:** **Proposed** — lead-architect. Records the **decision and plan** so the owner can draw
  real, durable scenes. This change is **docs only**: it ships no scene conversion, no Core change, no
  builder change. The pilot conversion is a **follow-up** the owner approves first (CLAUDE.md rule 8).
- **Date:** 2026-06-23
- **Decision owner:** lead-architect (scene strategy is a cross-cutting/architectural call —
  `agents/coordination.md` §1 "Scenes" ownership, §8; CLAUDE.md rule 3). world-content owns region
  *content*; the owner hand-authors the *visual layer*; tools-editor owns the bake/refresh tooling.
- **Flagged from:** the owner wanting to **hand-paint terrain + place decor in the REAL scenes and have
  it PERSIST** (not just a scratch scene). Today scenes are **builder-generated and uncommitted** (to
  dodge scene merge-hell), which means a painted scene is thrown away on the next build. `docs/authoring-scenes.md`
  §1/§6 explicitly parks this: *"which scenes get saved into the project is still being decided … a
  separate call the lead architect is working out."* This ADR is that call.
- **Related:** `0004-perspective-and-scene-strategy.md` (additive scene-per-region + prefab-first +
  Force-Text/UnityYAMLMerge — the backbone this builds on), `docs/authoring-scenes.md` (the #71
  scene-painting toolkit + the owner's paint guide this ADR unblocks), `agents/coordination.md` §1/§1.1
  (ownership map + shared seams), §5 (keeping `main` green), §5.1 (one-feature-per-push / branch-always),
  the region builders `GreyboxBuilder` / `GreywickBuilder` / `StPetersBuilder` and the persistent rig
  `PersistentCoreBuilder` (the logic these scenes carry/scaffold), `RegionAnchor` /
  `RegionTravelCoordinator` (the bind-on-arrival seam a committed region still exposes).

## Context

Two facts pull against each other:

1. **Agents cannot reliably author `.unity` files headless.** That is *why* the builders exist: a
   menu command (`Hidden Harbours ▸ Build … Scene`) reconstructs a whole scene from C# every time,
   because an agent editing scene YAML by hand is fragile and unreviewable. Builders are the agents'
   only safe way to "produce a scene."

2. **The owner wants to draw.** With the #71 toolkit (Tile Palette + paintable tilemaps + drag-in
   decor prefabs) the owner can paint a beautiful, hand-composed coastline — but only into a *scratch*
   scene, because the next `Build … Scene` overwrites the real scene wholesale. The hand-painting does
   not persist. The owner specifically wants the painted scenes to be **real and committed**.

The historical reason scenes are uncommitted is **merge-hell**: a `.unity` file is one big serialized
graph; two authors editing the same scene produces conflicts that even UnityYAMLMerge can't always
auto-resolve (`agents/coordination.md` §5). Keeping scenes builder-generated meant the *only* author of
any scene was a deterministic C# function — no two humans/agents ever fought over a `.unity`.

So the real question is **not** "scenes vs no scenes" — ADR 0004 already says scene-per-region, additive,
Force-Text, smart-merge. It is: **who may write a given scene's bytes, and how does the builder keep
baking gameplay LOGIC into that scene without wiping the owner's hand-painted VISUALS?**

## Decision — the HYBRID: split every committed scene into a LOGIC layer (agents bake) and a VISUAL layer (the owner authors)

A committed region scene has two clearly separated layers with **one author each**:

- **LOGIC layer — owned by the builders/code (agents).** The invisible gameplay scaffolding: the
  region's `RegionSceneLoader` + region list, the `RegionPassage`(s), the **one** `RegionAnchor`
  (arrival/dock/disembark — the bind point the persistent rig latches onto), the wharf economy
  components + their `PersistentHoldProxy`/`PersistentWalletProxy`, the `TidalTerrain` height-map zones,
  the clam-spot field, the `RegionDisplayNameRegistrar`, colliders that gate gameplay (shore edges).
  This is exactly what `GreywickBuilder` / `StPetersBuilder` already author *minus* the throwaway
  placeholder art. The **start** scene additionally carries the persistent core via
  `PersistentCoreBuilder`; plain regions do not (see ADR 0004 + the cove demotion, #66).

- **VISUAL layer — owned by the owner (one human).** Everything you *see* and nothing the simulation
  reads: painted terrain **Tilemaps** (sand/grass/rock/shoreline) and dropped **decor prefab instances**
  (trees, buildings, props) from the #71 toolkit. The builder's old placeholder visuals (tiled
  `SpriteRenderer` ground, tinted-square fallbacks, the hardcoded `CoveTrees`/`GreywickTrees` scatter)
  are **retired** from a converted scene — the owner's painting replaces them.

The two layers share the scene file but never the same GameObjects: **agents only ever touch
LOGIC-tagged objects; the owner only ever touches the VISUAL objects.** Because each layer has a single
author, there is no within-scene contention to merge.

This keeps every constraint already on the books: ADR 0004's scene-per-region + Force-Text +
UnityYAMLMerge (`.gitattributes`); CLAUDE.md rule 2 (content/tunables stay data — the *gameplay* numbers
remain in `TidalTerrain`/Defs/`GameConfig`, never in the painted pixels); rule 3 (author as prefabs —
decor is dragged in as prefab *instances*, so a prefab edit doesn't dirty the scene).

## How the builder bakes LOGIC without wiping the owner's VISUALS — two mechanisms, one recommended

The builders today do `EditorSceneManager.NewScene(EmptyScene)` and rebuild from zero — that is the
exact step that destroys hand-painting. A committed scene needs the logic to be **refreshable in place**.
Two designs were considered:

### Option A — Idempotent "Refresh Logic" (tagged logic subtree, additive-update, never NewScene)

The builder gains a second entry point — **`Hidden Harbours ▸ Refresh <Region> Logic`** — that, instead
of `NewScene`, **opens the committed scene and reconciles only a tagged logic subtree.** All
builder-authored objects live under one root, e.g. `--LOGIC-- (RegionLogicRoot)`. Refresh:

1. Opens the committed scene (does **not** create a new one).
2. Finds (or creates) the `--LOGIC--` root by a stable marker component (`RegionLogicRoot`, a tiny
   tag MonoBehaviour the builder owns).
3. **Destroys and re-creates only that subtree** (or upserts named children), then re-wires the
   serialized refs exactly as the builders do now (`RegionAnchor.Configure`, loader region list,
   passage targets, proxies).
4. Leaves every object **outside** `--LOGIC--` — i.e. the owner's `Grid`/Tilemaps + decor — untouched.
5. Saves the scene.

- **Pros:** one file, exactly as ADR 0004 + the #71 guide already picture it; the owner sees their
  painting and the gameplay in the same scene view; nothing new to learn about scene composition.
- **Cons:** the builder must be disciplined to *only* mutate the tagged subtree (a bug that touches an
  untagged object could clobber painting); diffs to the committed `.unity` happen on every Refresh
  (kept small because logic objects are few and named, but non-zero).

### Option B — Logic in an additive sub-scene/prefab the committed VISUAL scene references

The committed scene is **visual-only**; the logic lives in a separate, builder-owned artifact loaded
additively at play (a `<Region>.Logic.unity` sub-scene, or a `<Region>Logic` prefab the visual scene
instances). The builder rebuilds the logic artifact freely (even from zero) and **never opens the
owner's visual scene.**

- **Pros:** total write-isolation — the builder literally cannot touch the painted file, so merge-hell
  between agent and owner is structurally impossible; the agent keeps its "rebuild from zero" habit.
- **Cons:** two files per region to keep in sync (the visual scene must reference the right logic
  artifact); a second additive load at play (more boot wiring on top of the already-pragmatic VS-22
  travel rig); the owner's positions (where the dock/anchor sit) and the painted dock planks live in
  *different* files, so "paint the wharf, then the anchor must sit on it" becomes a cross-file dance.
  It also fights the #71 guide's mental model ("paint, drop decor, save the scene") by splitting "the
  scene" in two.

### Recommendation: **Option A (idempotent Refresh Logic into a tagged subtree).**

It matches what ADR 0004, the builders, and the #71 owner guide already assume ("one scene per region"),
keeps the owner's painting and the gameplay in one place, and adds the *least* new machinery. Option B's
structural isolation is tempting, but the project already carries enough additive-load pragmatism
(VS-22's toggled region scenes); adding a per-region logic sub-scene is more moving parts than the
problem needs. The single-author-per-layer rule (below) gives Option A the safety Option B gets
structurally, at a fraction of the complexity. Option B stays on file as the fallback if Refresh ever
proves too fiddly to keep surgical.

**Implementation note (tools-editor, the pilot follow-up):** the builders refactor from
`NewScene → build everything` into `BuildLogic(scene)` (the tagged subtree, callable by both a
first-time **Build** that does create the scene, and a **Refresh** that opens an existing committed one).
The placeholder-visual code paths are removed from converted scenes. The `RegionLogicRoot` tag + the
"destroy-and-recreate only under it" reconciler is the one new, small piece of editor tooling.

## How this coexists with "agents commit via PRs, the owner's checkout is contested"

Two structural rules keep merge-hell away even though scenes are now committed:

1. **Single author per layer, per scene.** A given committed scene's **VISUAL** layer is edited by
   **the owner only** (or, if ever delegated, exactly **one** agent at a time — never two). The
   **LOGIC** layer is edited only by the builder/code via Refresh. Because the two layers never share
   objects and each has one writer, two parties never edit the same bytes. This is the same principle
   ADR 0004 §B leans on ("two agents on two regions edit two different files"), applied *within* a file
   by layer.

2. **The owner lands painted scenes the same way agents do — on a branch, via a PR.** The owner's
   working checkout is contested (concurrent agents run in `git worktree`s; `main` is often checked out
   in a sibling worktree — `agents/coordination.md` §5.1, the PR-workflow memory). So the owner does
   **not** commit straight to `main`. Two clean paths, in order of preference:

   - **Preferred — owner commits the scene on a branch + opens a PR.** The owner saves the painted
     scene, commits just that `.unity` (+ its `.meta` and any new decor-instance changes) on a
     short-lived branch, pushes, and opens a PR. lead-architect reviews (the LOGIC layer is unchanged,
     so the diff is the owner's painting only — easy to eyeball) and merges once CI is green. This keeps
     the owner inside the same green-`main` discipline as everyone else and produces a reviewable diff.
   - **Fallback — owner hands the scene file to an agent.** If the git steps are unwelcome, the owner
     pastes the absolute path of the saved scene to an agent (or drops it somewhere agreed), and the
     agent commits it on a branch + opens the PR on the owner's behalf. Same end state, the owner just
     skips the git.

   Either way: **branch, PR, green CI, merge** — never a raw push to `main`, never two writers on one
   scene's visual layer at once.

Force-Text serialization + UnityYAMLMerge stay configured (ADR 0004) as the safety net for the *rare*
case (e.g. the owner repaints while an agent's Refresh-Logic PR is in flight): the conflict is then
between the LOGIC subtree and the VISUAL objects, which are different YAML nodes, so smart-merge resolves
it. If two people ever do edit the *same* layer of the *same* scene, that's an authoring mistake — we
re-author the smaller change, exactly as §5 prescribes.

## Recommended pilot scene: **Coddle Cove (`Greybox.unity`)**

Convert the cove first. Reasons:

- **It was just demoted to a plain region (#66, this same wave).** It no longer authors the persistent
  core — its LOGIC layer is now small and clean (water/island colliders, the wharf + proxies, the
  loader, the Cove→Greywick passage, and **one** `RegionAnchor`). A small, well-understood LOGIC layer
  is the easiest first thing to wrap in a `--LOGIC--` root and Refresh.
- **It is the home base, reached by travel, not the start.** A bug in the pilot can't break the game's
  *opening* (that's St Peters). The cove is the lowest-stakes scene to experiment on.
- **It is not St Peters.** St Peters is explicitly *held for the owner's own decor* and carries the
  start-scene persistent core + the tide showcase — the highest-stakes, most-moving-parts scene. Piloting
  there would conflate the new committed-scene workflow with the start-scene complexity. Greywick is a
  fine *second* pilot (also a plain region), but the cove is the simplest place to prove the loop.
- **It already has the owner's attention** as the "home" the sail-home arc lands in, so a hand-painted
  cove is a high-value, motivating first result.

Once the cove proves the **bake-logic → paint → save → PR** loop end-to-end, roll the same treatment to
Greywick, then (with the owner) to St Peters.

## The owner workflow (concrete, non-dev) — bake logic → paint → save → commit + push

> This is the flow `docs/authoring-scenes.md` §6 promised "when the workflow for real regions is ready."
> It will be folded into that guide when the pilot lands. No coding required.

1. **Get latest, on a branch.** Open a terminal (the project folder) and run the block in step 6's
   PowerShell — actually, do the *branch* first (so you never paint on a stale `main`):

2. **Bake the gameplay logic into the real scene** (Unity, menu). For the cove pilot:
   `Hidden Harbours ▸ Build Greybox Scene` the **first** time (it creates the committed scene with the
   `--LOGIC--` subtree), and `Hidden Harbours ▸ Refresh Greybox Logic` **every time after** (it updates
   only the logic and leaves your painting alone). *(Build vs Refresh is the pilot's one new menu item;
   until it ships, treat "Build" as first-time-only.)*

3. **Paint terrain + drag decor** (Unity) — straight from `docs/authoring-scenes.md`:
   - `Hidden Harbours ▸ Art ▸ Add Paintable Tilemap` to get a `Grid`/`TerrainTilemap` canvas.
   - `Window ▸ 2D ▸ Tile Palette` → choose **HiddenHarboursTerrain** → paint Sand/Grass/Rock/Shoreline.
   - Drag decor prefabs from `Assets/_Project/Prefabs/Decor/{Trees,Buildings,Props}/` into the Scene.
   - Leave the `--LOGIC--` objects alone (don't move the dock/anchor markers — they're the gameplay).

4. **Save the scene** (Unity): `File ▸ Save` (Ctrl+S). Save often.

5. **Sanity-check** (Unity): press **Play** from the start scene (`StPeters.unity`) and confirm the
   sail-home still lands at the cove with ONE player/camera — your painting shouldn't change behaviour.

6. **Commit + push the painted scene on a branch, then open a PR** (terminal — **PowerShell, one
   command per line, never `&&`/`||`**). Paste-ready (cove pilot shown):

   ```powershell
   # --- do this BEFORE you start painting (step 1): start from a fresh branch off main ---
   cd "C:\Users\aphol\Claude\Projects\Hidden Harbours"
   git fetch origin
   git switch -c paint/coddle-cove origin/main

   # --- ... now bake logic (step 2), paint (step 3), save (step 4) in Unity ... ---

   # --- after saving, back in the terminal: commit JUST the scene you painted ---
   cd "C:\Users\aphol\Claude\Projects\Hidden Harbours"
   git add "Assets/_Project/Scenes/Greybox.unity" "Assets/_Project/Scenes/Greybox.unity.meta"
   git status                      # confirm ONLY your scene (+ .meta) is staged — nothing else
   git commit -m "art(cove): hand-paint Coddle Cove terrain + decor"
   git push -u origin paint/coddle-cove

   # --- open the PR for the lead architect to review + merge once CI is green ---
   $env:GH_REPO = "apholmes7-bot/hiddenharbours"
   & "C:\Program Files\GitHub CLI\gh.exe" pr create --base main --head paint/coddle-cove `
     --title "art(cove): hand-paint Coddle Cove (committed scene pilot)" `
     --body "Hand-authored visual layer for Coddle Cove (ADR 0011 pilot). Logic untouched; diff is painting only."
   ```

   Notes for the owner:
   - If a decor prefab you placed is *new* (not previously committed), `git status` may list extra files
     under `Assets/_Project/Prefabs/Decor/` — that's fine; `git add` those too, or ask an agent.
   - **Don't** `git push` to `main` directly, and **don't** `git add .` (that would sweep in unrelated
     work). Stage the scene explicitly, as above.
   - If the git steps are unwelcome, stop after step 4 (save) and hand an agent the path
     `Assets/_Project/Scenes/Greybox.unity` — they'll do steps 6 for you (the fallback path above).

## Consequences

- **tools-editor** builds the `Build`/`Refresh` split + the `RegionLogicRoot` tag + the surgical
  reconciler (the one new piece). `docs/authoring-scenes.md` §1/§6 get the "save-back" steps appended.
- **The builders** stop authoring placeholder visuals for *converted* scenes; their LOGIC paths move
  under a tagged root and become Refresh-safe. Un-converted scenes keep building as today until they're
  piloted.
- **Committed `.unity` files re-enter version control** (the cove first). Their diffs are reviewable
  (logic is named + small; the owner's visual diff is the painting). LFS still tracks binaries; scenes
  stay Force-Text + smart-merge (ADR 0004) — committing them does not change that.
- **The owner gains durable, hand-painted regions** without learning the engine internals, and stays
  inside the green-`main` PR discipline like every other contributor.
- **The contested-checkout risk is bounded** by the single-author-per-layer rule + the branch/PR path;
  the only residual conflict (owner repaint vs in-flight Refresh PR) lands on different YAML nodes and
  smart-merges.

## Open questions (resolve during the pilot)

- **Refresh granularity:** destroy-and-recreate the whole `--LOGIC--` subtree each Refresh (simplest,
  bigger-but-still-small diff) vs upsert named children (smaller diff, more code). Start with
  destroy-and-recreate; optimise only if the diff noise annoys.
- **Decor as prefab instances vs flattened sprites:** ADR 0004 rule 3 prefers prefab *instances* (so a
  prefab edit doesn't dirty every scene). Confirm the #71 decor prefabs instance cleanly into a committed
  scene and that re-saving doesn't bloat the `.unity` with overrides.
- **Whether Greywick/St Peters keep a standalone-review camera once committed**, or drop it (the
  `RegionTravelCoordinator` already silences region cameras on arrival). Decide per scene when piloted.
- **Multiple visual layers** (a separate tilemap for paths/decoration): the toolkit supports extra
  paintable layers; confirm they commit + Refresh cleanly alongside the logic root.
