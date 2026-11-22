# Randomness and clock source audit

Base: `f1b4ab2f59f417eda06f2239f7dee25d4c52ad23`. Audited 2026-11-22.
This is a source audit only: no production code, content, configuration or save format changes.
It supports P1 and P3 by identifying inputs that can prevent reconstruction of the sea and working coast.

## Scope and method

All 1,908 C# files below `Assets/_Project/Code` were searched, including Editor folders.
Paths and lines below are relative to that root at the base above. Each row is one matching source
line; multiple tokens on that line are retained in the token column. Exact identifier boundaries
exclude `Time.timeScale`, `Time.deltaTime` and unrelated prefixes. The search includes the requested
`UnityEngine.Random`, `System.Random`, `Guid.NewGuid`, `DateTime.Now`, `DateTime.UtcNow`,
`Time.time`, `Time.realtimeSinceStartup`, `Time.unscaledTime`, `Environment.TickCount`, and
`Stopwatch`, plus `AsDouble` clocks, `TickCount64`, and unqualified `new Random`.
Supplementary searches for RNG aliases/static calls found the appearance shuffle and editor
Stopwatch aliases; no additional runtime entropy source was found in those searches.

The inventory deliberately retains comments, XML references, string literals, declarations and
editor measurements. A mention is not a call: their dispositions say so. The companion guard
uses this conservative text boundary too, so comments and interpolated expressions cannot hide
a new hit. No AST/data-flow analysis, reflection detection, cross-runtime RNG equivalence or
whole-game replay certification is claimed.

SIM means a simulation/interaction input, PRESENTATION means visual/audio only, and TOOLING
means editor/test support. Pure world models reconstruct from world seed and game time plus
authored/spatial inputs. Player actions additionally require the same action history and persisted
mutations; seeded stateful fights require the same draw/delta sequence. Different explicit inputs
are not hidden entropy. Presentation allowances do not promise equal pixels after a seek.

## Findings

The earlier [simulation audit](simulation-determinism-audit.md) is historical, not this baseline.
In particular its D09 null-RNG fallbacks are now gone: `BiteSequenceSim`, `FishFight`,
`RodFightRhythm` and `RodFightSim` reject null. The fishing constructors still use explicit seeds
or caller streams. Default clam/rod seeds use world seed, action identity and game time; serialized
seed overrides and unavailable-service zero fallbacks are not claims of world reconstruction.
There is no executable runtime `UnityEngine.Random`, `Guid.NewGuid`, `DateTime.Now/UtcNow`,
`Environment.TickCount` or realtime-since-startup read in this snapshot. The unqualified,
parameterless RNG in `UI/CharacterCreatorModel.cs:22` shuffles appearance only on user request.

Known clock-dependent simulation hits are retained as faults, not approved rules:

- Beach roster caching can change outing rank when objects arrive in different discovery batches.
- Beach talk expiry and body refresh can change conversation holds/eligibility at the same game time.
- Villager shelter/seed refresh can leave talkability or a prior-world plan stale until a session timer fires.
- Legacy region passage cooldowns depend on session time, even at equal game time; framed-world triggers stand down.
- Inner-voice repeat eligibility/session history depends on session time; the consequence is narrative history,
  not catch or price. Once-only save flags are separate and are not changed here.

These are source-traced counterexamples, not reproduced whole-game failures. The table explains
the divergence per SIM hit; acceptable RNG declarations and documentation mentions state why they
do not introduce entropy. No runtime fix is bundled with the audit or guard.

## Guard boundary and existing coverage

The selected runtime folders are `Core`, `Environment`, `Fishing`, `Economy`, `World`, `Boats`,
`Vehicles`, `App`, `Player`, `Art`, `UI`, and `Audio`. The first seven own world state/models;
App and Player compose inputs, while Art/UI/Audio are retained because host and interaction logic
can live beside presentation. Scan every C# file recursively, excluding path segments named
`Editor`; `Tools` currently contains only editor code and remains in this audit. Reject new runtime
top-level folders until reviewed, rather than silently leaving them outside the guard.

Existing coverage found before implementation: `SimulationReplayAuditTests.cs` exercises model
reconstruction; `CatchRollDeterminismTests.cs` checks action seeds;
`Art/SeagullFlockMathTests.cs` and `FuelBurnTests.cs` contain narrow source prohibitions.
`ModuleBoundaryTests.cs` is the existing project-wide architecture fence. Extend that fixture
with a separate source-policy partial; retain the stronger local prohibitions unchanged.
There is no existing all-module randomness/clock allowlist.

Freeze the reviewed source line and occurrence count, not a whole file or API permission. Moving
a line is harmless; changing it, adding a duplicate, or leaving an obsolete allowance must fail.
The allowlist includes acceptable presentation/editor boundaries, explicit RNG declarations,
non-code mentions and known faults with their reasons. A green scan means no unreviewed textual
change to these inputs; it does not prove the known faults fixed or detect a seed change elsewhere.

## Verification limits

Inventory rows were checked against a fresh source search at the named base. No Unity editor,
EditMode runner, PlayMode runner, scene or visual capture was run for this documentation change.
No tide, wind or weather is added to saved state. The existing historical replay results are not
results for this snapshot.

## Complete matching-line inventory

348 matching lines / 381 token occurrences: PRESENTATION 112, SIM 50, TOOLING 186. Known-fault lines: 13.

| File | Line | Module | Tokens | Class | Disposition / divergence |
| --- | ---: | --- | --- | --- | --- |
| `App/CameraFollow.cs` | 709 | App | `Time.timeAsDouble` | PRESENTATION | Camera framing time only. |
| `App/Editor/NineMileCreekFields.cs` | 31 | App | `System.Random` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/NineMileCreekRoads.cs` | 36 | App | `System.Random` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/NineMileCreekStarterSplat.cs` | 41 | App | `System.Random` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/NineMileCreekWharfLandSide.Trucks.cs` | 31 | App | `DateTime.UtcNow` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/NineMileCreekWharfLandSide.Trucks.cs` | 33 | App | `DateTime.UtcNow` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/NineMileCreekWharfLandSide.Trucks.cs` | 34 | App | `DateTime.UtcNow` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/NineMileCreekWharfLandSide.Yard.cs` | 34 | App | `DateTime.UtcNow` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/NineMileCreekWharfLandSide.Yard.cs` | 35 | App | `DateTime.UtcNow` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/NineMileCreekWharfLandSide.Yard.cs` | 36 | App | `DateTime.UtcNow` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/NineMileCreekWharfLandSide.Yard.cs` | 69 | App | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/RegionValidatorWindow.cs` | 209 | App | `DateTime.Now` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/StillWaterPlate.cs` | 921 | App | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/StPetersBuilder.cs` | 2758 | App | `System.Random` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/StPetersBuilder.cs` | 2797 | App | `System.Random` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/StPetersShoreMap.cs` | 32 | App | `System.Random` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/StPetersShoreMap.cs` | 309 | App | `System.Random` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/StPetersShoreMap.cs` | 450 | App | `System.Random` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/StPetersShoreMap.cs` | 612 | App | `System.Random` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/StPetersStarterSplat.cs` | 42 | App | `System.Random` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/StPetersWoods.cs` | 330 | App | `System.Random` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/TerrainPass9Plate.cs` | 533 | App | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/TerrainPlan/TerrainPlanWindow.cs` | 113 | App | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/TerrainPlan/TerrainPlanWindow.cs` | 136 | App | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/TerrainPlan/TerrainPlanWindow.cs` | 161 | App | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/TerrainPlan/TerrainPlanWindow.cs` | 171 | App | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/TerrainPlan/TerrainPlanWindow.cs` | 181 | App | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/TerrainPlan/TerrainPlanWindow.cs` | 192 | App | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/TerrainPlan/TerrainPlanWindow.cs` | 205 | App | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/TerrainPlan/TerrainPlanWindow.cs` | 250 | App | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/WaterHullsPlates.cs` | 485 | App | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/WaterHullsPlates.cs` | 533 | App | `Time.time` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/WaterHullsPlates.cs` | 565 | App | `Time.time` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/WaterHullsPlates.cs` | 574 | App | `Time.time` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/WaterHullsPlates.cs` | 774 | App | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/WoodlandZones.cs` | 345 | App | `System.Random` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/World/BakeAllBuildingsMenu.cs` | 116 | App | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/World/BakeAllBuildingsMenu.cs` | 141 | App | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/World/BakeAllBuildingsMenu.cs` | 182 | App | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/Editor/World/BakeAllBuildingsMenu.cs` | 195 | App | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `App/FightStrainCamera.cs` | 160 | App | `Time.unscaledTime` | PRESENTATION | Camera shake phase only. |
| `App/NmcWharfCraneVisual.cs` | 14 | App | `Time.unscaledTime` | PRESENTATION | Crane picture refresh only. |
| `App/NmcWharfCraneVisual.cs` | 17 | App | `Time.unscaledTime` | PRESENTATION | Crane picture refresh only. |
| `App/NpcPotBoats.cs` | 187 | App | `Time.unscaledTime` | PRESENTATION | Throttles water/deck picture refresh; daily work is sampled from the game clock. |
| `App/NpcPotBoats.cs` | 190 | App | `Time.unscaledTime` | PRESENTATION | Throttles water/deck picture refresh; daily work is sampled from the game clock. |
| `App/NpcTrawlBoats.cs` | 139 | App | `Time.unscaledTime` | PRESENTATION | Throttles loaded-scene visibility refresh; the trip pose is sampled from the game clock. |
| `App/NpcTrawlBoats.cs` | 142 | App | `Time.unscaledTime` | PRESENTATION | Throttles loaded-scene visibility refresh; the trip pose is sampled from the game clock. |
| `Art/AmbientParticleMath.cs` | 13 | Art | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Art/AmbientParticleMath.cs` | 26 | Art | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Art/BoatSpotlight.cs` | 623 | Art | `Time.time` | PRESENTATION | Spotlight flicker only; process-dependent hash is also presentation. |
| `Art/CharacterFigureLife.cs` | 27 | Art | `UnityEngine.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Art/ChimneySmoke.cs` | 16 | Art | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Art/DriftWeedKit.cs` | 38 | Art | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Art/DustMotes.cs` | 14 | Art | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Art/DustMotes.cs` | 109 | Art | `Time.time` | PRESENTATION | Dust particle phase only. |
| `Art/GrassDevWind.cs` | 36 | Art | `Time.time` | PRESENTATION | Preview grass wind phase only. |
| `Art/GrassFootstep.cs` | 46 | Art | `Time.time` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Art/GrassFootstep.cs` | 300 | Art | `Time.time` | PRESENTATION | Cosmetic grass displacement fade only. |
| `Art/GroundPrintRenderer.cs` | 161 | Art | `Time.unscaledTime` | PRESENTATION | Ground-print picture refresh only. |
| `Art/GroundPrintRenderer.cs` | 163 | Art | `Time.unscaledTime` | PRESENTATION | Ground-print picture refresh only. |
| `Art/GullFlock.cs` | 60 | Art | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Art/HoldCatchFillSource.cs` | 163 | Art | `Time.unscaledTime` | PRESENTATION | Reads spoil to tint contents; does not settle or mutate the hold. |
| `Art/HoldCatchFillSource.cs` | 164 | Art | `Time.unscaledTime` | PRESENTATION | Reads spoil to tint contents; does not settle or mutate the hold. |
| `Art/HullWaveBridge.cs` | 102 | Art | `Time.timeAsDouble` | PRESENTATION | Hull picture wave phase; engine-time fallback without a clock. |
| `Art/InteractAffordancePresenter.cs` | 218 | Art | `Time.time` | PRESENTATION | Offer highlight pulse timing; does not decide interaction eligibility. |
| `Art/InteractAffordancePresenter.cs` | 375 | Art | `Time.time` | PRESENTATION | Offer highlight pulse timing; does not decide interaction eligibility. |
| `Art/LightMath.cs` | 9 | Art | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Art/LightMath.cs` | 401 | Art | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Art/LightMath.cs` | 643 | Art | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Art/NavLight.cs` | 168 | Art | `Time.timeAsDouble` | PRESENTATION | Navigation lamp phase; engine-time fallback without a clock. |
| `Art/PlantField.cs` | 211 | Art | `Time.unscaledTime` | PRESENTATION | Refreshes plant appearance and shadow meshes only. |
| `Art/PlantField.cs` | 215 | Art | `Time.unscaledTime` | PRESENTATION | Refreshes plant appearance and shadow meshes only. |
| `Art/PlantField.cs` | 216 | Art | `Time.unscaledTime` | PRESENTATION | Refreshes plant appearance and shadow meshes only. |
| `Art/PreconfiguredLight.cs` | 23 | Art | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Art/RainEmitter.cs` | 32 | Art | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Art/SceneLight.cs` | 30 | Art | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Art/SceneLight.cs` | 138 | Art | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Art/SceneLight.cs` | 521 | Art | `Time.timeAsDouble` | PRESENTATION | Lamp flicker phase; engine-time fallback without a clock. |
| `Art/SeagullFlockMath.cs` | 39 | Art | `Time.time` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Art/SeagullSplashMath.cs` | 14 | Art | `UnityEngine.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Art/SeaMistEmitter.cs` | 24 | Art | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Art/SeaweedMath.cs` | 15 | Art | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Art/SeaweedPresenter.cs` | 19 | Art | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Art/SeaweedPresenter.cs` | 209 | Art | `Time.unscaledTime` | PRESENTATION | Decor-only activation and snag/strand cadence; no catch, load or saved mutation. |
| `Art/SeaweedPresenter.cs` | 211 | Art | `Time.unscaledTime` | PRESENTATION | Decor-only activation and snag/strand cadence; no catch, load or saved mutation. |
| `Art/SeaweedPresenter.cs` | 226 | Art | `Time.unscaledTime` | PRESENTATION | Decor-only activation and snag/strand cadence; no catch, load or saved mutation. |
| `Art/SeaweedPresenter.cs` | 227 | Art | `Time.unscaledTime` | PRESENTATION | Decor-only activation and snag/strand cadence; no catch, load or saved mutation. |
| `Art/ShorePlantTideDriver.cs` | 105 | Art | `Time.unscaledTime` | PRESENTATION | Plant tide appearance refresh only. |
| `Art/ShorePlantTideDriver.cs` | 106 | Art | `Time.unscaledTime` | PRESENTATION | Plant tide appearance refresh only. |
| `Art/SprayEmitter.cs` | 34 | Art | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Art/SurfSprayEmitter.cs` | 21 | Art | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Art/WadeSplashEmitter.cs` | 172 | Art | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Art/WalkerLights.cs` | 190 | Art | `Time.unscaledTime` | PRESENTATION | Press/hold gesture controls lamp appearance; not autonomous world simulation. |
| `Art/WalkerLights.cs` | 194 | Art | `Time.unscaledTime` | PRESENTATION | Press/hold gesture controls lamp appearance; not autonomous world simulation. |
| `Audio/AudioDirector.cs` | 386 | Audio | `Time.unscaledTimeAsDouble` | PRESENTATION | Oar sound scheduling and listener discovery only. |
| `Audio/AudioDirector.cs` | 395 | Audio | `Time.unscaledTime` | PRESENTATION | Oar sound scheduling and listener discovery only. |
| `Audio/AudioDirector.cs` | 397 | Audio | `Time.unscaledTime` | PRESENTATION | Oar sound scheduling and listener discovery only. |
| `Audio/FishingAudio.cs` | 157 | Audio | `Time.unscaledTime` | PRESENTATION | Fishing sound cooldown only. |
| `Boats/AmbientFleetPlan.cs` | 26 | Boats | `UnityEngine.Random` | SIM | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Boats/AmbientFleetPresenter.cs` | 202 | Boats | `Time.unscaledTime` | PRESENTATION | Decor-only activation and steering probes; no placed gear, catch or economy mutation. |
| `Boats/AmbientFleetPresenter.cs` | 204 | Boats | `Time.unscaledTime` | PRESENTATION | Decor-only activation and steering probes; no placed gear, catch or economy mutation. |
| `Boats/AmbientFleetPresenter.cs` | 217 | Boats | `Time.unscaledTime` | PRESENTATION | Decor-only activation and steering probes; no placed gear, catch or economy mutation. |
| `Boats/AmbientFleetPresenter.cs` | 220 | Boats | `Time.unscaledTime` | PRESENTATION | Decor-only activation and steering probes; no placed gear, catch or economy mutation. |
| `Boats/BoatWakeEmitter.cs` | 61 | Boats | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Boats/BoatWakeEmitter.cs` | 395 | Boats | `Time.timeAsDouble` | PRESENTATION | Wake picture phase; engine-time fallback without a clock. |
| `Boats/BoatWaveMotion.cs` | 443 | Boats | `Time.timeAsDouble` | PRESENTATION | Visual wave riding; engine-time fallback without a clock. |
| `Boats/BowImpactMath.cs` | 38 | Boats | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Boats/BuoyWaveVisual.cs` | 262 | Boats | `Time.timeAsDouble` | PRESENTATION | Buoy picture wave phase; engine-time fallback without a clock. |
| `Boats/DeckContainerPresenter.cs` | 177 | Boats | `Time.unscaledTime` | PRESENTATION | Reads spoil to tint the container; does not settle the hold. |
| `Boats/DeckContainerPresenter.cs` | 179 | Boats | `Time.unscaledTime` | PRESENTATION | Reads spoil to tint the container; does not settle the hold. |
| `Boats/DoryOarMeshLayer.cs` | 381 | Boats | `Time.time` | PRESENTATION | Visual interpolation between rowing physics steps. |
| `Boats/DoryOarSplashSystem.cs` | 417 | Boats | `UnityEngine.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Boats/HelmControlRelay.cs` | 347 | Boats | `Time.time` | PRESENTATION | Caches depth/wind instrument readouts; physical grounding and forces do not read this cache. |
| `Boats/HelmControlRelay.cs` | 382 | Boats | `Time.time` | PRESENTATION | Caches depth/wind instrument readouts; physical grounding and forces do not read this cache. |
| `Boats/MeshHullDriver.cs` | 307 | Boats | `Time.timeAsDouble` | PRESENTATION | Smooths displayed hull trim; engine-time fallback without a clock. |
| `Boats/WakeBubbleSystem.cs` | 38 | Boats | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Boats/WakeParticleSystem.cs` | 34 | Boats | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Boats/WakeParticleSystem.cs` | 365 | Boats | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Boats/WakeParticleSystem.cs` | 389 | Boats | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Core/Environment/WakeFoamAgeing.cs` | 36 | Core | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Core/Environment/WaveFieldAnimator.cs` | 108 | Core | `Time.timeAsDouble` | PRESENTATION | Presentation wave-clock fallback; explicit-input wave methods do not read engine time. |
| `Core/Environment/WaveFieldAnimator.cs` | 112 | Core | `Time.timeAsDouble` | PRESENTATION | Presentation wave-clock fallback; explicit-input wave methods do not read engine time. |
| `Core/Fishing/FishJumpArc.cs` | 83 | Core | `UnityEngine.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Core/Iso/CharacterFigureBlink.cs` | 15 | Core | `UnityEngine.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Core/Iso/CharacterSkinPose.cs` | 61 | Core | `UnityEngine.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Core/Nav/LoranMath.cs` | 26 | Core | `System.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Core/Nav/NavLightCharacter.cs` | 45 | Core | `Time.time` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Core/Needs/EffortGate.cs` | 214 | Core | `Time.unscaledTime` | PRESENTATION | Refusal speech cooldown only; the effort check precedes this read. |
| `Core/World/SceneLoadHold.cs` | 143 | Core | `Stopwatch`, `Stopwatch` | TOOLING | Editor load-read timeout; production call sites are editor-conditional, not a world-model clock. |
| `Environment/CurrentModel.cs` | 17 | Environment | `UnityEngine.Random` | SIM | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Environment/DugHolePresenter.cs` | 139 | Environment | `Time.unscaledTime` | PRESENTATION | Refreshes visible dug-hole pictures from explicit game time. |
| `Environment/DugHolePresenter.cs` | 141 | Environment | `Time.unscaledTime` | PRESENTATION | Refreshes visible dug-hole pictures from explicit game time. |
| `Environment/WeatherModel.cs` | 8 | Environment | `UnityEngine.Random` | SIM | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Environment/WeatherModel.cs` | 198 | Environment | `UnityEngine.Random` | SIM | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Fishing/BiteSequenceSim.cs` | 60 | Fishing | `System.Random` | SIM | Acceptable caller stream; null throws. Equal seed, draw order and action/delta history reproduce it; this declaration creates no entropy. |
| `Fishing/BiteSequenceSim.cs` | 92 | Fishing | `System.Random` | SIM | Acceptable caller stream; null throws. Equal seed, draw order and action/delta history reproduce it; this declaration creates no entropy. |
| `Fishing/CatchResolver.cs` | 156 | Fishing | `System.Random` | SIM | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Fishing/CatchResolver.cs` | 174 | Fishing | `System.Random` | SIM | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Fishing/CatchResolver.cs` | 176 | Fishing | `System.Random` | SIM | Acceptable explicit RNG parameter; caller supplies the stream. Different stream history can change catch; no RNG is created here. |
| `Fishing/CatchResolver.cs` | 187 | Fishing | `System.Random` | SIM | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Fishing/CatchResolver.cs` | 191 | Fishing | `System.Random` | SIM | Acceptable explicit RNG parameter; caller supplies the stream. Different stream history can change catch; no RNG is created here. |
| `Fishing/CatchResolver.cs` | 202 | Fishing | `System.Random` | SIM | Acceptable explicit RNG parameter; caller supplies the stream. Different stream history can change catch; no RNG is created here. |
| `Fishing/CatchResolver.cs` | 296 | Fishing | `System.Random` | SIM | Acceptable explicit RNG parameter; caller supplies the stream. Different stream history can change catch; no RNG is created here. |
| `Fishing/CatchResolver.cs` | 310 | Fishing | `System.Random` | SIM | Acceptable explicit RNG parameter; caller supplies the stream. Different stream history can change catch; no RNG is created here. |
| `Fishing/ClamDig.cs` | 103 | Fishing | `System.Random` | SIM | Acceptable explicit stream: default seed hashes world seed, hole ID and action game time; nonzero override and missing-service zero remain explicit limitations. |
| `Fishing/ClamDig.cs` | 176 | Fishing | `System.Random` | SIM | Acceptable explicit stream: default seed hashes world seed, hole ID and action game time; nonzero override and missing-service zero remain explicit limitations. |
| `Fishing/ClamDig.cs` | 185 | Fishing | `System.Random` | SIM | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Fishing/ClamSpurtMath.cs` | 15 | Fishing | `System.Random` | SIM | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Fishing/FishFight.cs` | 104 | Fishing | `System.Random` | SIM | Acceptable caller stream; null throws. Equal seed, draw order and action/delta history reproduce it; this declaration creates no entropy. |
| `Fishing/FishFight.cs` | 116 | Fishing | `System.Random` | SIM | Acceptable caller stream; null throws. Equal seed, draw order and action/delta history reproduce it; this declaration creates no entropy. |
| `Fishing/FishingController.cs` | 107 | Fishing | `System.Random` | SIM | Acceptable explicit stream: default seed hashes world seed, region and cast game time; nonzero override and missing-service zero remain explicit limitations. |
| `Fishing/FishingController.cs` | 390 | Fishing | `System.Random` | SIM | Acceptable explicit stream: default seed hashes world seed, region and cast game time; nonzero override and missing-service zero remain explicit limitations. |
| `Fishing/FishSchoolMath.cs` | 26 | Fishing | `System.Random`, `UnityEngine.Random` | SIM | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Fishing/LonglineCatch.cs` | 76 | Fishing | `System.Random` | SIM | Acceptable: seed hashes world seed, line ID, set game time and hook index; equal placement inputs reproduce the draw. |
| `Fishing/PlacedTrapCatch.cs` | 17 | Fishing | `System.Random` | SIM | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Fishing/PlacedTrapCatch.cs` | 58 | Fishing | `System.Random` | SIM | Acceptable: supplied stream or seed from world seed, trap ID, placement game time and animal index; equal inputs reproduce the draw. |
| `Fishing/PlacedTrapCatch.cs` | 111 | Fishing | `System.Random` | SIM | Acceptable: supplied stream or seed from world seed, trap ID, placement game time and animal index; equal inputs reproduce the draw. |
| `Fishing/PotDeckWorkController.cs` | 822 | Fishing | `Time.timeAsDouble` | PRESENTATION | Crawl flipbook frame only. |
| `Fishing/RodFightRhythm.cs` | 33 | Fishing | `System.Random` | SIM | Acceptable caller stream; null throws. Equal seed, draw order and action/delta history reproduce it; this declaration creates no entropy. |
| `Fishing/RodFightRhythm.cs` | 49 | Fishing | `System.Random` | SIM | Acceptable caller stream; null throws. Equal seed, draw order and action/delta history reproduce it; this declaration creates no entropy. |
| `Fishing/RodFightSim.cs` | 98 | Fishing | `System.Random` | SIM | Acceptable caller stream; null throws. Equal seed, draw order and action/delta history reproduce it; this declaration creates no entropy. |
| `Fishing/StableHash.cs` | 75 | Fishing | `System.Random` | SIM | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Fishing/TrapFill.cs` | 72 | Fishing | `System.Random` | SIM | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Fishing/TrapHaulController.cs` | 489 | Fishing | `Time.timeAsDouble` | PRESENTATION | Rope lift/shudder picture only; no catch or haul-state input. |
| `Fishing/TrapHaulController.cs` | 650 | Fishing | `Time.time` | PRESENTATION | Rope lift/shudder picture only; no catch or haul-state input. |
| `Fishing/TrawlSession.cs` | 104 | Fishing | `System.Random` | SIM | Acceptable required caller stream, consumed at setup only; null throws. Equal stream, tow and track inputs reproduce outcomes. |
| `Player/DeckRiderMeshPresenter.cs` | 807 | Player | `Time.timeAsDouble` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Player/DeckRiderMeshPresenter.cs` | 816 | Player | `Time.timeAsDouble` | PRESENTATION | Visual seat/ladder tween timing only. |
| `Player/PlayerFrothPresenter.cs` | 100 | Player | `Time.time` | PRESENTATION | Cosmetic froth animation only. |
| `Player/RodFightPresenter.cs` | 454 | Player | `UnityEngine.Random` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `Player/RodFightPresenter.cs` | 994 | Player | `Time.time` | PRESENTATION | Drawn churn and shudder phases only. |
| `Player/RodFightPresenter.cs` | 1267 | Player | `Time.time` | PRESENTATION | Drawn churn and shudder phases only. |
| `Player/StinkLines.cs` | 161 | Player | `Time.time` | PRESENTATION | Cosmetic dirt/odour animation only. |
| `Tools/Editor/GrassTestBuilder.cs` | 91 | Tools | `System.Random` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/PlayerBuild.cs` | 111 | Tools | `DateTime.UtcNow` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/BoatInteriorSheetBaker.cs` | 9 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/BoatInteriorSheetBaker.cs` | 105 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/BoatInteriorSheetBaker.cs` | 422 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/BoatInteriorSheetBaker.cs` | 521 | Tools | `DateTime.UtcNow` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/BoatsLookDitherBuild.cs` | 92 | Tools | `DateTime.UtcNow` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/BoatsLookDitherBuild.cs` | 150 | Tools | `DateTime.UtcNow` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/BoatsLookDitherBuild.cs` | 155 | Tools | `DateTime.UtcNow` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/BoatsLookDitherBuild.cs` | 184 | Tools | `DateTime.UtcNow` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/BoatsLookDitherBuild.cs` | 318 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/BubbleKitBaker.cs` | 6 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/BubbleKitBaker.cs` | 214 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/BubbleKitBaker.cs` | 215 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/BuildingRigBaker.cs` | 7 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/BuildingRigBaker.cs` | 357 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/BuildingRigBaker.cs` | 496 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/BuildingRigBaker.cs` | 497 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CamperSheetBaker.cs` | 8 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CamperSheetBaker.cs` | 109 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CamperSheetBaker.cs` | 137 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CamperSheetBaker.cs` | 195 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CamperSheetBaker.cs` | 499 | Tools | `DateTime.UtcNow` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CatchPass2Baker.cs` | 7 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CatchPass2Baker.cs` | 257 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CatchPass2Baker.cs` | 319 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CatchPass2Baker.cs` | 361 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CatchPass2StorageBaker.cs` | 5 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CatchPass2StorageBaker.cs` | 65 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CatchPass2StorageBaker.cs` | 83 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CatchPass2StorageBaker.cs` | 198 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CatchPass2StorageBaker.cs` | 230 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CatchPass2StorageBaker.cs` | 322 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CatchPass2StorageBaker.cs` | 349 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CatchPass2StorageBaker.cs` | 417 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CatchPass2StorageBaker.cs` | 540 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CatchPass2StorageBaker.cs` | 572 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CatchStorageBaker.cs` | 6 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CatchStorageBaker.cs` | 103 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CatchStorageBaker.cs` | 128 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CatchStorageBaker.cs` | 176 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CatchStorageBaker.cs` | 198 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CatchStorageBaker.cs` | 343 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CatchStorageBaker.cs` | 377 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CharacterRigBaker.cs` | 7 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CharacterRigBaker.cs` | 204 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CharacterRigBaker.cs` | 258 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CharacterSkinAssetBaker.cs` | 102 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CharacterSkinAssetBaker.cs` | 431 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CharacterSkinAssetBaker.V9.cs` | 66 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CliffBaker.cs` | 9 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CliffBaker.cs` | 147 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CliffBaker.cs` | 166 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CliffBaker.cs` | 191 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CliffBaker.cs` | 245 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CliffBaker.cs` | 290 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/CliffBaker.cs` | 351 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/FishingKitBaker.cs` | 7 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/FishingKitBaker.cs` | 222 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/FishingKitBaker.cs` | 239 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/FishingKitBaker.cs` | 328 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/FishingKitBaker.cs` | 373 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/FishingKitBaker.cs` | 623 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/FuelSheetBaker.cs` | 8 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/FuelSheetBaker.cs` | 64 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/FuelSheetBaker.cs` | 112 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/FuelSheetBaker.cs` | 277 | Tools | `DateTime.UtcNow` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/GasStationSheetBaker.cs` | 8 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/GasStationSheetBaker.cs` | 68 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/GasStationSheetBaker.cs` | 129 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/GasStationSheetBaker.cs` | 331 | Tools | `DateTime.UtcNow` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/GrassLibraryBaker.cs` | 8 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/GrassLibraryBaker.cs` | 81 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/GrassLibraryBaker.cs` | 92 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/HullPaintSchemeProofSheet.cs` | 172 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/InteriorRigBaker.cs` | 263 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/InteriorRigBaker.cs` | 314 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/IsoPropSheetBaker.cs` | 7 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/IsoPropSheetBaker.cs` | 156 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/IsoPropSheetBaker.cs` | 376 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/NavBuoyBakeMenu.cs` | 6 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/NavBuoyBakeMenu.cs` | 27 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/NavBuoyBakeMenu.cs` | 126 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/NavBuoySheetBaker.cs` | 4 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/NavBuoySheetBaker.cs` | 50 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/NavBuoySheetBaker.cs` | 51 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/NotebookKitBaker.cs` | 6 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/NotebookKitBaker.cs` | 284 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/NotebookKitBaker.cs` | 285 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/RigBaker.cs` | 7 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/RigBaker.cs` | 109 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/RigBaker.cs` | 154 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/RigMeshMenu.cs` | 6 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/RigMeshMenu.cs` | 111 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/RoadKitSheetBaker.cs` | 7 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/RoadKitSheetBaker.cs` | 74 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/RoadKitSheetBaker.cs` | 75 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/RockIsoBaker.cs` | 7 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/RockIsoBaker.cs` | 99 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/RockIsoBaker.cs` | 130 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/RockIsoBaker.cs` | 317 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/SeagullBaker.cs` | 7 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/SeagullBaker.cs` | 157 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/SeagullBaker.cs` | 184 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/ShopFixtureSheetBaker.cs` | 8 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/ShopFixtureSheetBaker.cs` | 82 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/ShopFixtureSheetBaker.cs` | 136 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/ShopFixtureSheetBaker.cs` | 256 | Tools | `DateTime.UtcNow` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/ShopLevelBaker.cs` | 64 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/ShopLevelBaker.cs` | 147 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/ShoreFindsSheetBaker.cs` | 5 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/ShoreFindsSheetBaker.cs` | 101 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/ShoreFindsSheetBaker.cs` | 172 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/ShorePlantBaker.cs` | 7 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/ShorePlantBaker.cs` | 128 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/ShorePlantBaker.cs` | 155 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/ShorePlantBaker.cs` | 379 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/ShovelKitBaker.cs` | 122 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/ShovelKitBaker.cs` | 182 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/ShrubBaker.cs` | 7 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/ShrubBaker.cs` | 189 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/ShrubBaker.cs` | 219 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/ShrubBaker.cs` | 539 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/StPetersSetPieceBaker.cs` | 8 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/StPetersSetPieceBaker.cs` | 108 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/TreePass4Baker.cs` | 8 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/TreePass4Baker.cs` | 105 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/TreePass4Baker.cs` | 113 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/TreePass4Baker.cs` | 379 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/TreePass4Baker.cs` | 393 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/TreeRigBaker.cs` | 6 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/TreeRigBaker.cs` | 132 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/TreeRigBaker.cs` | 173 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/TreeRigBaker.cs` | 393 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/WharfIsoSheetBaker.cs` | 6 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/WharfIsoSheetBaker.cs` | 109 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/WharfIsoSheetBaker.cs` | 310 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/YardSheetBaker.cs` | 8 | Tools | `Stopwatch`, `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/YardSheetBaker.cs` | 86 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/YardSheetBaker.cs` | 183 | Tools | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `Tools/Editor/RigBaking/YardSheetBaker.cs` | 354 | Tools | `DateTime.UtcNow` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `UI/CharacterCreatorModel.cs` | 22 | UI | `new Random` | PRESENTATION | User-requested appearance shuffle; rolls body/colours/outfit, not traits or world outcomes. |
| `UI/Editor/ChartplotterProofEyeball.cs` | 259 | UI | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `UI/Editor/DashProofEyeball.cs` | 88 | UI | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `UI/Editor/FishFinderRepaintBench.cs` | 60 | UI | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `UI/Editor/FishFinderRepaintBench.cs` | 65 | UI | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `UI/Editor/PilothouseDashProofEyeball.cs` | 132 | UI | `Stopwatch` | TOOLING | Editor-only measurement, metadata or source reference; no runtime simulation read. |
| `UI/FishFinderOverlayHost.cs` | 209 | UI | `Time.time` | PRESENTATION | Instrument blink and waterfall display phases only. |
| `UI/FishFinderOverlayHost.cs` | 212 | UI | `Time.time` | PRESENTATION | Instrument blink and waterfall display phases only. |
| `UI/FishFinderOverlayLayout.cs` | 75 | UI | `Time.time` | PRESENTATION | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `UI/HelmOverlayHost.cs` | 338 | UI | `Time.time` | PRESENTATION | Instrument alarm blink only. |
| `UI/RadarOverlayHost.cs` | 461 | UI | `Time.unscaledTime` | PRESENTATION | Radar repaint throttle only. |
| `UI/RadarOverlayHost.cs` | 462 | UI | `Time.unscaledTime` | PRESENTATION | Radar repaint throttle only. |
| `UI/SounderOverlayHost.cs` | 170 | UI | `Time.time` | PRESENTATION | Sounder alarm blink only. |
| `World/Beach/BeachLifeDirector.cs` | 219 | World | `Time.unscaledTime` | SIM | KNOWN FAULT: wall-time roster caching can change rank and the day outing at equal seed/time when discovery occurs in different batches. |
| `World/Beach/BeachLifeDirector.Gatherings.cs` | 156 | World | `Time.unscaledTime` | SIM | KNOWN FAULT: wall-time talk timeout/body cache can change who is held or eligible for a conversation at equal seed/time. |
| `World/Beach/BeachLifeDirector.Gatherings.cs` | 259 | World | `Time.unscaledTime` | SIM | KNOWN FAULT: wall-time talk timeout/body cache can change who is held or eligible for a conversation at equal seed/time. |
| `World/Beach/BeachLifeDirector.Gatherings.cs` | 333 | World | `Time.unscaledTime` | SIM | KNOWN FAULT: wall-time talk timeout/body cache can change who is held or eligible for a conversation at equal seed/time. |
| `World/Campaign/CampaignBarrierDirector.cs` | 140 | World | `Time.unscaledTime` | PRESENTATION | Speech cooldown only; barrier and weather decisions precede this read. |
| `World/Campaign/CampaignBarrierDirector.cs` | 142 | World | `Time.unscaledTime` | PRESENTATION | Speech cooldown only; barrier and weather decisions precede this read. |
| `World/Conversations/ConversationSchedule.cs` | 10 | World | `UnityEngine.Random` | SIM | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `World/InnerVoiceDirector.cs` | 61 | World | `Time.unscaledTime` | SIM | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `World/InnerVoiceDirector.cs` | 242 | World | `Time.unscaledTime` | SIM | KNOWN FAULT: repeat-line eligibility and session speech history differ with elapsed session time at equal seed/time; not a catch/price fault. |
| `World/InnerVoiceDirector.cs` | 247 | World | `Time.unscaledTime` | SIM | KNOWN FAULT: repeat-line eligibility and session speech history differ with elapsed session time at equal seed/time; not a catch/price fault. |
| `World/RegionPassage.cs` | 90 | World | `Time.unscaledTime` | SIM | KNOWN FAULT: equal game-time re-entry can travel or refuse depending on elapsed session time; limited to legacy/arrival trigger paths. |
| `World/RegionPassage.cs` | 130 | World | `Time.unscaledTime` | SIM | KNOWN FAULT: equal game-time re-entry can travel or refuse depending on elapsed session time; limited to legacy/arrival trigger paths. |
| `World/RegionPassage.cs` | 164 | World | `Time.unscaledTime` | SIM | KNOWN FAULT: equal game-time re-entry can travel or refuse depending on elapsed session time; limited to legacy/arrival trigger paths. |
| `World/Routines/RoutineSchedule.cs` | 18 | World | `Time.time` | SIM | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `World/Routines/RoutineSchedule.cs` | 23 | World | `UnityEngine.Random` | SIM | Comment or literal only; no executed read, RNG construction or divergence at this hit. |
| `World/Routines/VillagerRoutine.cs` | 296 | World | `Time.unscaledTime` | SIM | KNOWN FAULT: wall-time shelter/seed refresh can leave talkability or a prior-world plan stale at equal seed/time. |
| `World/Routines/VillagerRoutine.cs` | 298 | World | `Time.unscaledTime` | SIM | KNOWN FAULT: wall-time shelter/seed refresh can leave talkability or a prior-world plan stale at equal seed/time. |
| `World/Routines/VillagerRoutine.cs` | 503 | World | `Time.unscaledTime` | SIM | KNOWN FAULT: wall-time shelter/seed refresh can leave talkability or a prior-world plan stale at equal seed/time. |
| `World/Routines/VillagerRoutine.cs` | 505 | World | `Time.unscaledTime` | SIM | KNOWN FAULT: wall-time shelter/seed refresh can leave talkability or a prior-world plan stale at equal seed/time. |
| `World/WharfLadder.cs` | 154 | World | `Time.unscaledTime` | PRESENTATION | Refreshes the drawn ladder span; boarding registry and eligibility do not use this timer. |
| `World/WharfLadder.cs` | 160 | World | `Time.unscaledTime` | PRESENTATION | Refreshes the drawn ladder span; boarding registry and eligibility do not use this timer. |
