# ADR 0004 — Perspective & Scene Strategy

- **Status:** Accepted
- **Date:** 2026-06-17
- **Decision owner:** lead-architect + art-pipeline + world-content

## Context

Two references pull in different directions: **Stardew Valley** is ¾ top-down; **Kingdoms Two
Crowns** is a side-scrolling parallax world. We need one coherent, buildable perspective that works
for land, town, on-water sailing, **and** the signature walkable-seabed-at-low-tide moments — and a
scene setup that lets many agents work without corrupting shared files.

## Decision A — One perspective: ¾ top-down, KTC mood

- **Canonical camera: ¾ top-down** (Stardew-style) everywhere — land, town, and on water.
- Borrow **Kingdoms Two Crowns** for **mood, not geometry**: painterly parallax layers (sky/horizon
  band, mid water, foreground), atmospheric lighting, fog, and a limited palette.
- **Tide-revealed seabed** reads naturally in top-down: at low tide the water layer recedes and the
  authored seabed terrain becomes walkable in the same view — no perspective switch needed.
- **Scale standard is locked** at PPU=32, 1 tile = 1 m (`design/art-and-audio-bible.md`), which is
  what sells the dory→tanker size fantasy (P2) in a single consistent view.

**Why not mixed perspectives (top-down on land, side-on at sea):** doubles the art and camera
complexity and fractures the world's readability. One perspective is far cheaper to build with
agents and keeps the world legible on a phone.

## Decision B — Additive scene-per-region + prefab-first

- **`Bootstrap.unity`** holds persistent services; **each region is its own scene**, loaded
  additively (`architecture/project-structure.md` §3, `tech-architecture.md` §2).
- **Author content as prefabs**; place prefab *instances* in scenes so prefab edits don't dirty
  scene files.
- Scenes/prefabs are serialized as **Force Text** and merged via **UnityYAMLMerge**
  (`.gitattributes`).

**Why:** two agents on two regions edit two different scene files → no conflict. Prefab-first keeps
`.unity` diffs tiny. This is the backbone of safe parallel world-building.

## Consequences

- `art-pipeline` delivers all sprites at PPU=32 with point filtering and a pixel-perfect camera.
- Water is a parallax + shader treatment whose coverage is driven by `EnvironmentSample.TideHeight`.
- `world-content` never builds "one giant scene"; new playable areas are new scenes registered in
  the `MapGraph`.
- Camera zoom adapts so a 110 m tanker still fits frame while a 4.5 m dory still feels intimate
  (`design/art-and-audio-bible.md`).

## Open questions
- Exact parallax layer count and whether water is a shader vs animated tilemap — `art-pipeline`
  prototypes in M1.
- Below-deck / interior scenes (cottage, shops) as separate small additive scenes vs prefab rooms —
  start as prefab interiors, revisit if they grow.
