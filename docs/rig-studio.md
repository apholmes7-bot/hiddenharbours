# The Rig Studio — browse every rig kit, bake what you like

**Menu: `Hidden Harbours ▸ Art ▸ Rig Studio`.** One window, one tab per art kit. You don't need
to read any code to use it — that's the point.

## What it is

Every art kit in this project (village buildings, shrubs, trees, shore rocks — more as they're
imported) is generated from a parametric **rig**: a program that can draw the thing at any
combination of its dials. The Rig Studio lets you drive those dials yourself:

- **pick a kit** (a tab),
- **scrub its dials** (species, phase, facing, variant, snow…) and watch the actual pixels update,
- **bake** what you're looking at into the game's placeable art with one button.

Two promises the window keeps:

1. **What you see is what bakes.** The preview is rendered by the *same* code path the bake uses —
   bit-for-bit identical, and a test enforces it. You are never approving a mock-up.
2. **It never commits anything.** A bake lands files in your working tree exactly as the equivalent
   `Hidden Harbours ▸ Art ▸ Bake …` menu command would. You review and commit (or discard) like any
   other change.

## How to use it

1. Open **Hidden Harbours ▸ Art ▸ Rig Studio** and pick a tab.
2. Scrub. Chips are enumerated values (every legal option visible at once); sliders are bounded
   numbers; **Variant** has a `next ▶` step. The preview re-renders only when you change something.
3. Read the caption — it's the kit's own measurements (footprint or height in metres, cell size).
   The **red cross** is the pivot: where the thing plants when placed. `1:1` shows native pixels
   (pixel art is never smoothed); `pivot` toggles the cross.
4. Press **Bake this**. The report says exactly what was written (the *minted* list) and where the
   thing you were looking at is now placeable — usually a prefab to drag into a region scene, or
   sliced sprites a paint/placement tool picks up.
5. Place it: drag the named prefab into a scene, or use the tool the report names
   (e.g. `Hidden Harbours ▸ Dev ▸ Place Village Lineup`).

## When the studio says no

The window refuses rather than guesses — always in the kit's own words, shown in the panel. These
are not bugs; they're the kits' own rules holding:

- **A greyed-out chip** (e.g. the **Tamarack** tree) is *held back*: the kit's own quality gate
  rejects it, so it can't be previewed or baked. The reason is written next to it.
- **A shrub at deep snow** may refuse with "draw a drift" — past 95 % buried, the scene draws a
  snowdrift instead of the shrub. Lighter snow draws a pale line over the preview: that's the snow
  *surface*; the game clips the sprite there at placement (snow is never baked into sheets).
- **A shrub at `young`/`half` stage** refuses until the kit's contract is re-exported at that stage
  — a deliberate step, not a dial.
- If a **bake** fails partway (the slicer refuses a sheet, a contract disagrees), the full reason
  appears in the report area, verbatim. Nothing half-baked is silently kept.

## What each kit's bake writes (and what committing means)

| Tab | "Bake this" writes | Committed? |
|-----|--------------------|------------|
| Village buildings | the whole M1 set (5 sheets + contract), sliced, + prefabs | sheets/contract are committed art; prefabs are built to order |
| Acadian shrubs | ONE species' phase sheet × 3 channels, sliced | **no** — bakes to order; committing a sheet is your atlas-budget call (a test gates it) |
| Acadian trees | the whole 9-species set + contract, sliced, + prefabs | committed art; an unchanged rig re-bakes byte-identical |
| Shore rocks | ONE sheet (form × stone × tide), sliced | **no** — bakes to order |

If a bake leaves changes you don't want, `git checkout` / discard them like any other file — the
studio wrote nothing outside the working tree.

## For agents: adding the next kit

The studio core knows nothing about any specific rig. A kit joins by shipping an
`IRigStudioAdapter` (editor-only; see
`Assets/_Project/Code/Tools/Editor/RigStudio/IRigStudioAdapter.cs`) — describe the axes *from the
kit's own catalog/contract*, preview through the kit's *own* bake render path, delegate the bake to
the kit's existing chain. The tab appears by discovery; the registry-driven tests pin the new
adapter automatically. **Since 2026-07-30 this is part of the kit import standard** (the
#312/#318/#337/#352 lineage): every rig import ships its adapter — the interior rig is the first
due to comply.
