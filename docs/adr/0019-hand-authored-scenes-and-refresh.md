# ADR 0019 — Hand-authored scenes are the SOURCE OF TRUTH: builders CREATE once, then REFRESH logic only — the owner designs the levels

- **Status:** **Proposed — awaiting owner sign-off.** Docs-only: this ADR ships no code. Merging this
  PR = the owner's go-ahead to build **Phase 1** of the owner-tooling plan (generalize the
  create/refresh split to every region builder, adopt the regions one by one, then the authoring
  tools on top). Records the decision that the scene-authoring model **flips**: a region builder's
  job splits into **CREATE (scaffold, run once)** and **REFRESH LOGIC (non-destructive re-wire, the
  only re-runnable mode)**, and an **adopted** region's committed `.unity` becomes the source of
  truth — hand edits are the truth, and no tool may wipe them.
- **Date:** 2026-07-04
- **Decision owner:** lead-architect (scene strategy is a cross-cutting/architectural call — the same
  ownership ADR 0011 records; `agents/coordination.md` §1 "Scenes", §8). tools-editor owns the
  builder/Refresh tooling this ratifies; world-content owns region *content*; **the owner authors the
  scenes themselves** — that is the point of this ADR.
- **Serves:** the owner-steering model itself (CLAUDE.md §7 — "you steer; agents build" becomes "you
  *design*; agents wire") and through it **P3 Living Working Coast** — places composed by a human
  eye, not a scatter loop. P1 is untouched by construction: the sim keeps reading the data seams
  (ADR 0009/0014), never the painted pixels.
- **Flagged from:** the owner's directive (2026-07-03): he wants to design regions/levels **himself,
  in the Unity editor, with no AI in the loop**. The blocker is structural: outside the cove pilot,
  scenes are builder-generated and a re-run rebuilds them **from zero**
  (`EditorSceneManager.NewScene(EmptyScene)`) — this session a builder re-run **ate a hand-added
  boat spotlight**. Solo level design is impossible while any menu command can destroy hand work.
- **Related:** `0011-committed-hand-authored-scenes.md` (**this ADR evolves it** — the cove pilot
  becomes the project-wide rule; its "Greywick + St Peters stay un-converted until separately
  piloted" clause ends here), `0004-perspective-and-scene-strategy.md` (scene-per-region, additive,
  prefab-first, Force-Text — the unchanged backbone), **CLAUDE.md rule 9** (Force Text serialization
  + UnityYAMLMerge smart merge — the discipline that makes committed `.unity` files viable at all),
  `0014-painted-seabed-height-authoring.md` (the owner already authors the *terrain data*; this ADR
  is the same owner-authoring arc for the *scene*), `0002-procedural-vs-handcrafted.md` (a
  handcrafted world — this is its authoring endgame), `docs/authoring-scenes.md` §1/§7 (the owner
  guide this ADR extends beyond the cove), the region builders `GreyboxBuilder` / `GreywickBuilder` /
  `StPetersBuilder` / `PersistentCoreBuilder`, and the `RegionLogicRoot` marker + `Refresh Cove
  Logic` (the shipped ADR 0011 pilot this generalizes).

## Context

ADR 0011 answered "who may write a given scene's bytes?" for **one** scene. It split a committed
region scene into a **LOGIC layer** (builder-baked, under one tagged root named
`--LOGIC-- (generated, do not edit)`, keyed on the `RegionLogicRoot` marker component) and a
**VISUAL layer** (everything outside that root, authored by the owner), and shipped the pilot on
Coddle Cove: `GreyboxBuilder` gained the **`Refresh Cove Logic`** command that opens the committed
scene — never `NewScene` — destroys + regenerates **only** the tagged subtree, and leaves every
other object untouched (#76). The pilot **works, end to end**:

- `Greybox.unity` is a real, committed, hand-paintable scene — the only committed region scene today.
- The Refresh path has carried a real logic upgrade into it: **#150 (shoreline convergence)**
  delivered the tide-driven water rig (`RectTidalTerrain` + the layered `WaterSurface` sea) into the
  committed cove *through* `Refresh Cove Logic`, touching none of the hand-authored layer. Refresh
  is not a hope; it is the shipped delivery vehicle for logic into a hand-authored scene.
- An EditMode test (`CoveLogicRefreshTests`) pins the reconciler's contract: one tagged root after a
  refresh, idempotent, painted layer untouched.

But ADR 0011 was deliberately a **pilot**, and its residual rule is now the problem:

1. **Greywick and St Peters are still builder-generated and uncommitted** ("stay un-converted until
   separately piloted"). Their builders rebuild from zero on every run. That was *safe* when a
   deterministic C# function was each scene's only author — wiping a file nobody hand-edits loses
   nothing. It is now a trap: the owner has started editing scenes by hand, and this session a
   builder re-run **destroyed a hand-added boat spotlight**. That is exactly the failure ADR 0011's
   Context predicted for un-converted scenes, landing on schedule.
2. **The owner's directive raises the bar past the pilot's framing.** ADR 0011 pictured the owner
   painting a *visual* layer (Tilemaps + decor prefabs) while agents keep authoring all gameplay
   logic. The owner now wants to **design regions/levels himself, with no AI in the loop** — which
   means hand edits are no longer confined to tiles and trees: hand-placed lights (the spotlight
   class), particles, tweaked component values, and eventually hand-placed *gameplay* markers are
   all owner work the tooling must treat as permanent.

So the question must be re-answered for the **whole project**, with the default flipped: not "which
scenes are safe for the owner to touch?" but "which narrow, tagged subtree is the *builder* still
allowed to touch?"

## Decision

**Flip the scene-authoring model. Every region builder splits into two entry points — CREATE
(scaffold a new region's scene, run once) and REFRESH LOGIC (a non-destructive, idempotent re-wire
of only the builder-owned subtree, the only mode ever re-run). Once the owner ADOPTS a region, its
committed `.unity` is the source of truth: hand edits are the truth, the builder's CREATE path is
retired for that scene except as an explicit, warned, opt-in full rebuild. ADR 0011's pilot
mechanics (tagged `RegionLogicRoot` + surgical refresh) generalize from the cove to every region and
every future region. This ADR decides the model and the Refresh contract; the per-builder refactors
and the owner tools ship as separate Phase 1 PRs.**

### (1) The create-once / refresh-only split — every region builder, the same two entry points

- **CREATE / SCAFFOLD** (`Hidden Harbours ▸ Build <Region> Scene`) — run **once** to birth a new
  region. Produces a scene containing **only** the tagged logic root and its subtree: the water
  surface (the `WaterSurface` sea plane + material), the terrain registration (the region's
  `ITidalTerrain` component — analytic zones or an ADR 0014 painted map), the **one**
  `RegionAnchor` (arrival/dock/disembark), the travel stubs (`RegionSceneLoader` region list +
  `RegionPassage`s), the wharf economy + persistent proxies, gameplay colliders, the
  standalone-review camera, and the `EditorBuildSettings` registration. The **start** scene's CREATE
  additionally scaffolds the persistent core (`PersistentCoreBuilder`). No placeholder visuals — the
  owner paints the look (ADR 0011, unchanged). After adoption, CREATE is never run again on that
  scene except as the escape hatch below.
- **REFRESH LOGIC** (`Hidden Harbours ▸ Refresh <Region> Logic`) — the **only** mode safe to re-run,
  and the only mode agents ever run on an adopted scene. Opens the committed scene (never
  `NewScene`), reconciles **only** the builder-owned tagged subtree, saves. This is
  `GreyboxBuilder.RefreshLogic` / `RebuildLogicSubtree` generalized: the same
  find-tagged-root → rebuild-subtree → touch-nothing-else reconciler, one per region builder,
  sharing the mechanism (and ideally a common helper) rather than three forks.
- **The escape hatch** — a full from-zero rebuild stays available for genuine resets, but it is
  **opt-in and loudly warned**: the cove's existing guard (a modal "this will WIPE hand-authored
  visuals" dialog whenever the scene file already exists on disk) becomes mandatory on **every**
  region builder's CREATE, **immediately** — including not-yet-adopted Greywick/St Peters, because
  the spotlight incident proves hand edits happen *before* formal adoption. Keying the guard on
  file-exists needs no adoption bookkeeping.

### (2) Adoption — the committed scene becomes the source of truth (the ADR 0011 amendment)

**Adopting** a region = the owner (or an adopting PR on his behalf) commits the region's `.unity`
(+ `.meta`) to the repo, after which hand edits to that file are canon and the builder interacts
with it through Refresh only. This **explicitly amends ADR 0011's residual state**: ADR 0011 kept
"builder-generated and uncommitted" as the default for every scene except the pilot — uncommitted
being precisely *why* re-runs were safe to wipe. That default **ends**. The target state is:
**every region scene is adopted and committed; "builder-generated, uncommitted, wipe-on-rerun"
ceases to exist as a category.** The CREATE path exists only to scaffold *new* regions (which are
then adopted) or to serve the warned escape hatch.

What **stands** from ADR 0011, unchanged:

- **Single author per layer.** The builder writes only under the tagged root; the owner writes only
  outside it. Each layer has exactly one author, so two parties never edit the same bytes.
- **The landing path.** Adopted scenes ride branches and PRs like all work — the owner's
  bake → commit → paint → save → PR flow (`docs/authoring-scenes.md` §7) is this ADR's per-region
  migration recipe, verbatim.
- **Rule 9 is the enabler.** Force-Text serialization + UnityYAMLMerge (ADR 0004, CLAUDE.md rule 9)
  are what make committed `.unity` files reviewable and mergeable; the rare cross-layer conflict
  (owner repaint vs an in-flight Refresh PR) lands on different YAML nodes and smart-merges.

What **changes** relative to ADR 0011:

- **Scope:** pilot → rule. The mechanics were proven on the cove; they become the requirement for
  every region builder, current and future.
- **The owner's layer broadens.** ADR 0011 described the owner's layer as painted Tilemaps + decor
  prefab instances. It is now defined **negatively — everything outside the builder root is the
  owner's**, whatever it is: decor, lights, particles, audio emitters, extra tilemap layers,
  hand-placed markers, tweaked component values. The spotlight class of hand-adds is protected by
  definition, not by enumeration.
- **The default for new regions:** scaffold once, adopt, refresh — not "builder-owned indefinitely."

### (3) The Refresh contract — precisely what it MAY touch and what it MUST NEVER touch

The contract that makes "no AI in the loop" safe. **Refresh MAY (and should, idempotently):**

- **Rebuild the tagged subtree.** Destroy and re-create (or upsert — granularity stays the open
  question ADR 0011 logged) every GameObject **under a `RegionLogicRoot`-tagged root**. Those
  objects are builder-truth by definition; an owner tweak *inside* the root is the one thing Refresh
  legitimately resets (see the sharp edge below).
- **Re-point serialized asset/Def references** on objects it owns: loader region lists, passage
  target regions, the proxies' Defs, `GameConfig`, hull Defs, the `WaterSurface` material and
  painted-height-map reference (ADR 0014). Content stays data (rule 2) — the scene carries
  *placement and wiring*, never balance numbers (rule 6).
- **Re-establish registrations** the code path needs: the scene's `EditorBuildSettings` entry
  (already idempotent — `RegisterScene` de-dupes), the presence of the region's `ITidalTerrain`
  component (its `GameServices` registration is runtime `OnEnable`, not scene data), the
  `RegionAnchor`'s configured spawn/dock/disembark points, region ids on the root marker.

**Refresh MUST NEVER**, under any code path:

- **Destroy, move, reparent, rename, or edit any object outside the tagged root** — the owner's
  painted Tilemaps, decor, lights (the spotlight), particles, audio, hand-placed gameplay markers,
  camera or lighting tweaks, and any component value on any owner object. Not "preserve where
  possible": *never touch*.
- **Create a new scene or clear the open one.** `NewScene`/`OpenScene(Single)`-then-rebuild is the
  CREATE path's privilege, once. Refresh opens the committed scene and reconciles in place.
- **Write scene-wide state it did not author** (render/lighting settings, scene sorting tweaks) or
  save any *other* open scene as a side effect.
- **Silently expand its own footprint.** If a logic change needs a new object, it appears **under
  the tagged root**. A builder that "helpfully" places something at the scene root is a contract
  bug, not a feature.

Two boundary notes that keep the contract honest and small:

- **Self-installing systems need nothing from Refresh.** The project's runtime services and bridges
  (`DayNightController`, `GrassWindBridge`, `WaveFieldBridge`, `MoonCycle`, …) self-install via
  `RuntimeInitializeOnLoadMethod` and have **zero scene presence** — Refresh's surface is only the
  builder-*placed* objects, their serialized references, and registrations. Every future system that
  can self-install should, precisely because it keeps the refresh surface shrinking.
- **The one sharp edge, stated plainly:** anything the owner tweaks *inside* the `--LOGIC--` root is
  builder-truth and **will be reset** by the next Refresh. Today that means "don't hand-move the
  dock/anchor" (ADR 0011's standing instruction). The trajectory — deliberately *not* designed here —
  is Phase 1+ **marker prefabs**: owner-placed markers *outside* the root that the logic **reads**
  (Refresh re-points logic *at* the owner's markers instead of owning their placement), migrating
  positional truth from builder-truth to owner-truth one seam at a time. Until a seam migrates, the
  rule is the simple one: inside the root = the builder's; outside = the owner's, untouchable.

**The mechanism (recommended): one tagged root per region — the shipped `RegionLogicRoot`.** Keyed
on the **marker component, never the name** (a renamed or duplicated root is still found; a painted
object that merely shares the name is never harmed — `GreyboxBuilder.RebuildLogicSubtree` already
does exactly this). One root = one boundary to police, trivially auditable in the Hierarchy
("everything under `--LOGIC--` is generated"), and test-pinnable. Alternatives considered:
**per-object marker components** (`BuilderOwned` on every generated object — finer-grained, but N
boundaries to police, easy to orphan, and the Hierarchy no longer shows the split at a glance);
**name conventions** (fragile — a rename or copy-paste breaks ownership silently); a **GUID
manifest** (a baked list of builder-owned object ids — precise but a second artifact that drifts
from the scene it describes). The single tagged root is the cleanest and is already proven; the
others stay noted in case a region someday genuinely needs interleaved ownership.

**Every adopted region ships the pilot's test shape**: an EditMode `<Region>LogicRefreshTests`
pinning (a) exactly one tagged root after Refresh, (b) idempotence (refresh twice = same tree),
(c) objects outside the root untouched in count and identity — `CoveLogicRefreshTests` generalized.

### (4) Migration — St Peters, Cove, Greywick: build once → adopt → commit → refresh-only, no big-bang

Each region flips **independently, in its own small PR**, on the ADR 0011 owner workflow
(`docs/authoring-scenes.md` §7). Nothing forces a date; until a region is adopted its builder works
as today — but the CREATE wipe-warning (§1) lands on **all** builders in the first Phase 1 PR, so
the spotlight incident cannot recur even pre-adoption.

1. **Coddle Cove — already adopted.** The ADR 0011 pilot is complete: `Greybox.unity` is committed,
   `GreyboxBuilder` has both entry points, the refresh test exists. Nothing to migrate; it is the
   template.
2. **Greywick — next.** Refactor `GreywickBuilder` into `BuildLogicTree` + `Refresh Greywick Logic`
   (the same #76 refactor shape), retiring its placeholder visuals into the tagged root as
   collider-only objects where gameplay needs them. Owner runs Build once, paints or not, commits
   `Greywick.unity` (+ `.meta`) on a branch, PR, adopted. Lowest stakes of the two remaining — the
   same reasoning ADR 0011 used to pick the cove pilot.
3. **St Peters — last, deliberately.** The start scene carries the most moving parts (the persistent
   core via `PersistentCoreBuilder`, onboarding, the tide showcase, the ADR 0014 painted-seabed
   adoption running in parallel). Same refactor; its Refresh must also reconcile the
   persistent-core scaffold (whether that lives under the same tagged root or a second
   `PersistentCoreBuilder`-owned one is an implementation call for that PR — the contract in §3
   binds either way).

After step 3, **no full-wipe builder remains in the project**, and `docs/authoring-scenes.md` §1's
"don't paint Greywick/St Peters yet" warning is deleted — the owner may design in any region.

### (5) What this ADR is the foundation FOR — Phase 1 and the owner-tooling plan (named, not designed)

The owner-tooling plan builds on this contract; each tool is its own future PR and **none is
designed here**: the **Region Validator** (checks an adopted scene's logic wiring instead of
rebuilding it), the **New Region Wizard** (a friendly wrapper over CREATE + adoption), the **Decor
Brush**, **marker prefabs** (the owner-truth positional seam from §3), and **Region Preview**. They
all *assume* create-once/refresh-only + adopted-committed scenes — which is why this ADR merges
first: it is the keystone that makes the owner's hand-crafted work permanent before any tool
encourages him to make more of it.

### What does NOT change (determinism, data, save, Core)

Scenes are authored content **read** at load; nothing here touches the sim contract. Tide/wind/
weather stay recomputed from `(worldSeed, gameTime)` (rule 5); tunables stay in Defs/`GameConfig`
(rules 2/6); the render==sim seams (ADR 0009/0014) are untouched. **No Core change at all** — the
one runtime type involved (`RegionLogicRoot`, App module) already exists; this ADR is process +
editor tooling. No save-format change, no schema bump (ADR 0008). Runtime cost: one marker
MonoBehaviour per region scene — nil (rule 7).

## Consequences

- **The owner can design levels solo, permanently.** On an adopted scene, no builder re-run can
  destroy hand work — the spotlight class of edits is protected *by construction* (outside the root
  is untouchable), not by anyone remembering to be careful.
- **Agents keep a safe delivery path into hand-authored scenes.** Refresh is how logic upgrades
  reach adopted scenes — #150's water convergence already proved the shape. "No AI in the loop" for
  design does not mean "no logic evolution": it means the AI's write access shrinks to one tagged
  subtree.
- **Scenes re-enter version control as they are adopted** (the cove already has). Diffs are
  reviewable — the logic subtree is small and named; the owner's diff is his authoring. Rule 9
  (Force Text + smart merge) carries the merge risk; single-author-per-layer keeps it rare
  (ADR 0011, unchanged). Scene churn in PRs is bounded by adopting one region at a time.
- **Builders demote from scene-author-of-record to scaffolding + wiring tools.** Their placeholder-
  visual code paths retire per region as it adopts (as the cove's did in #76); their remaining job —
  birth new regions, keep wiring current — is the durable one.
- **One honest sharp edge remains:** owner tweaks *inside* the tagged root are reset by Refresh.
  Mitigated now by the root's "do not edit" name + the docs, and structurally over Phase 1+ by
  migrating positional truth to owner-placed markers (§3).
- **Docs move in the adopting PRs:** `docs/authoring-scenes.md` §1/§7 update per region;
  ADR 0011 gains a one-line pointer here (its decision stands; its pilot scope is superseded).

## Rejected alternatives

- **Keep everything builder-generated (the status quo outside the cove).** The alternative that ate
  the spotlight. It optimizes for the project's *past* (agents were the only authors; wipes were
  free) against its *present* (the owner is becoming the level designer). Every day it persists,
  hand work in un-adopted scenes lives one menu click from destruction.
- **Fully hand-build — delete the builders.** Loses the scaffold for *new* regions (a blank scene
  needs a dozen precisely-wired components before it plays; error-prone for anyone, hostile for a
  non-dev), loses drift-safety (when a shared contract changes — #150's water model — someone must
  re-wire N scenes *by hand*, and they will drift), and strands agents entirely (headless agents
  cannot author a valid `.unity` — ADR 0011's founding fact — so logic could no longer be delivered
  by PR at all). The builder is not the enemy; the builder *re-run wiping the file* is.
- **Prefab-variant per region** (region content lives in a prefab; the scene is a thin host
  instancing it; "refresh" = prefab propagation). Evaluated honestly: prefab overrides *are* a
  refresh mechanism, and nested prefabs give layering for free. But it is the wrong authoring
  surface for this owner: Tile Palette painting into prefab-mode tilemaps is genuinely awkward
  (the toolkit paints scene tilemaps); prefab-instance override YAML is far noisier to review than
  plain scene objects; one mistaken "Apply overrides" silently pushes scene-specific edits into the
  shared prefab (the inverse of the wipe bug — corruption flowing *up*); and it adds an indirection
  ("which edits live on the instance vs the asset?") exactly where we are optimizing for a non-dev's
  mental model of "open the scene, edit, save". A *builder-owned* logic-bundle prefab under the
  tagged root remains a compatible implementation option for the generalizing PRs — as plumbing,
  not as the owner's authoring surface.
- **Logic in an additive sub-scene** (ADR 0011's Option B). Still rejected for ADR 0011's reasons —
  two files per region, the cross-file anchor/dock dance, more boot wiring — and it stays on file as
  the fallback if Refresh discipline ever proves unkeepable. Nothing since the pilot suggests it
  will: the reconciler has held through #150.
- **Process-side guards only** (CODEOWNERS, branch rules, "agents must not run Build"). The wipe
  happens **in the editor, before git is involved** — no repo-side rule can stop a local menu click.
  The guard must live in the tool (the warn dialog, the refresh-only default), which is exactly
  where this ADR puts it.
- **Hand-edit overlay/patch replay** (builder rebuilds from zero, then re-applies recorded hand
  edits). Inverts authority — the builder stays author-of-record and the owner's work becomes a
  diff to replay against regenerated objects whose fileIDs churn every rebuild. Fragile, opaque,
  unreviewable. The owner's work must be the *base*, not the patch.

## Open questions for the owner (rule on these before or during Phase 1)

- **Adoption cadence: per-region opt-in, or all-at-once?** Recommended: per-region (Greywick when
  you're ready, St Peters after the seabed pass) — it matches the pilot and keeps each PR small. But
  all-at-once is defensible: after it, no full-wipe builder exists anywhere. Your call on pace; the
  wipe-warning lands everywhere immediately either way.
- **How should Refresh show its hand before touching anything?** Options, cheap → rich: the current
  confirm dialog + console summary; a **dry-run** listing exactly which objects it will destroy/
  re-create (cheap, recommended as the Phase 1 default); a proper before/after visual diff (a real
  tool — only if the dry-run proves insufficient). How much preview do you want?
- **Does the from-zero escape hatch stay on the menu once a region is adopted?** Keep it (behind the
  red warning) for genuine resets, or hide it for adopted regions so a reset requires an agent
  (recoverable via git either way)?
- **St Peters timing.** Its scene adoption (this ADR) and its painted-seabed adoption (ADR 0014's
  explicit swap) are independent steps — do you want them in one sitting or separately?
- **Which seams migrate to owner-truth markers first?** The dock/anchor placement, passage gates,
  fishing spots, NPC spawn points — the order you want to *place these yourself* drives the Phase 1+
  marker-prefab priorities (each migration is its own small PR under §3's contract).
