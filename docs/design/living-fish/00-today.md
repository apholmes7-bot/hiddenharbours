# Living fish: what the baseline does

Audited against `3e7188a426c00dde0b18f94ab9512ea1a2bc0c21`. Line numbers below refer to that revision, not an editor rebuild. This is a code audit; no visual plate was captured or approved.

## Fish in the water

The game already has schools. `Assets/_Project/Code/Fishing/FishSchoolMath.cs:20` describes a spatial cell and time-slot lattice. `CellSlotKey` at line 102 hashes world seed, region id, cell coordinates and slot; independent streams choose presence, centre, radius, depth, marks and appearance window. `FishSchoolModel.cs:294` builds the same school for point and view queries. Its buffers are allocated once (`:98`); the point search examines nine cells and the view search is capped at 25 (`:52`, `:180`). Schools are recomputed, never saved.

`Assets/_Project/Code/Core/Fishing/FishSchool.cs:40` carries centre, encounter radius, depth in metres, mark count, stable species ids and an active time window. The mark count serves both sonar density and bite influence. `FishSchoolModel.cs:497` excludes shellfish, checks season, daily windows and moving-water preference. Depth matching can relax to a second selection pass (`:438`): today's depth preference is not a strict biological occupancy constraint. The centre is fixed within each slot (`:381`); a replacement slot can choose a different centre. This is not a continuous travelling population.

`FishSchoolPresenter.cs:250` queries those schools and lays out pooled sprite swimmers using `ShoalMath.Fill` (`:285`). `ShoalMath.cs:48` specifies the reference 0.35 m/s local loop. `ShoalEventMath` supplies deterministic swim events; a gull landing adds a local scatter offset (`FishSchoolPresenter.cs:288`, `:354`). That scatter is presentation only, leaving bite rates alone. Species choose school size, spread, density and jump permission. Shellfish are deliberately barred from swimming schools (`FishSpeciesDef.cs:117`).

The charter's searches find `VisibleFishTests` and `VisibleFishSeenTests`, rather than a production type named VisibleFish. There is no independent fishing `SpawnTable` class: the species pool and weighted resolver are the table. Other uses of lattice/shoal in wave and wake maths are not fish populations.

## What a fish Def contains

`Assets/_Project/Code/Fishing/FishSpeciesDef.cs` is the actual schema; the older design schema includes ambitions that are not implemented fields.

| Baseline lines | Available data |
|---|---|
| 16–24 | Stable id, name, category, rarity, flavour, optional sprite |
| 27–44 | Region ids, allowed gear, four-season mask, absolute tide-height interval, daily window plus optional second window |
| 51–63 | Moving-water-only gate, depth-band mask, behaviour flags including Bottom and Jumps |
| 70–103 | Per-species school mark range, shoal spread and school density; zero means use existing defaults |
| 111–154 | Hooked jump period, favoured lure mask, optional bite personality and rod-fight personality |
| 157–169 | Weight range, base value, spoil/day, supply elasticity and spawn weight |

There is no species habitat or vegetation preference, continuous metre depth interval, crawler/sessile behaviour kind, oxygen budget, vitality state or live individual id. `ITidalTerrain.cs:50` publishes elevation only. `PaintedTidalTerrain.cs:86` samples the painted height field shared with the water. Terrain plans also contain paint-material strings (`World/TerrainPlan/GroundPatchDef.cs:21`) and plant ground payloads (`World/PlantFieldGroundData.cs:13`), but these are not an ecological query through Core. They do not establish underwater eelgrass, kelp or piling habitat. Never infer fish habitat from a displayed colour.

## Bites and landing

`FishingController.cs:175` constructs `System.Random`: an explicit nonzero testing seed is reproducible; the normal zero setting uses the time-seeded constructor. This is an existing determinism gap in catch rolling, not a property to copy into the new field. The school distribution itself has no such RNG dependency. The controller's wait/strike interaction runs on real-time deltas; its default bite wait is 1.5–4 seconds (`:79`).

At bite time `FishingController.cs:549` builds context and calls `CatchResolver.Resolve`. `CatchResolver.cs:200` filters eligible species, totals effective weights and takes a seeded-stream weighted draw. `:249` combines spawn weight with depth/bottom affinity, bait, lure and school influence. Depth/lure mismatches damp rather than universally exclude. `:296` rolls weight between the Def limits, with the sea-state bias. A species with a BiteDef runs `BiteSequenceSim` (teases, take and strike); otherwise the forgiving auto-hook beat remains (`FishingController.cs:579`). FishFight or the opt-in rod fight then determines landing or escape.

`FishingController.cs:704` checks landing permission and stamps a CatchItem with `Freshness.Landed` at the Core game clock (`:725`). A landing cue fires at `:747`. On foot it tries the carry hand; otherwise the current implementation adds to a hold (`:750`). That at-sea direct addition is a gap against the current by-hand transport ruling. PR 1033 owns the controller's full-hold honesty changes; this work does not edit that controller or presume that PR is merged.

`Player/CarriableCatch.cs:88` uses a baked held sprite or icon, with facing changes at `:160`; it has no individual vitality/flop clock. `Art/CatchFillRenderer.cs:7` shows actual baked catch sprites in container openings using pooled renderers and event-driven refresh. Visible storage fill exists; a living/dead individual handling model does not.

## Sorting, depth and freshness

`FishSchoolPresenter.cs:47` defaults swimmers to order -3, below hull order 1 and wake -1 on its configured sorting layer. `SwimmerVisibility.cs:60` maps Tidepool/Shallows to full fish, Inshore to dim fish, Midwater to shadow and Deep/Abyssal to no visible swimmer. These rules do not remove sonar marks or bites. `FishSchoolPresenter.cs:369` adds alpha 0.65 in Inshore; the sprite cells already carry their baked depth tint. This is discrete visibility plus baked art, not physical per-fish transmission through the water shader.

`Art/Shaders/HiddenHarboursWater.shader:2366` bends the sampled bed via `ShallowsBend`, using surface slope, depth and `_ShallowsFloorRefraction`. It does not refract arbitrary SpriteRenderers. `HiddenHarboursWaterOverlay.shader:9` explains that the displaced surface is composited in a SortingGroup at the flat surface's slot; `Art/DisplacedWaterSurface.cs:650` copies that slot. An ordinary swimmer's order alone does not place it inside the water's offscreen shading pass.

`Core/Economy/Freshness.cs:83` stores accrued spoil, last settle time and storage mode. `:140` computes spoil from elapsed game-clock time, spoil/day and storage multiplier; changing storage first settles (`:167`). Default policy at `:69` is Ambient 1, Live/Frozen/Iced 0, refusal at 0.9. These are food-quality rules, not evidence that a fish is alive. `SpoilContext.cs:48` is the shared read. Future vitality must coexist with this clock and must not reset accrued spoil on a transfer or revive a still fish on ice.
