# Audit coverage and source inventory

Snapshot: `7276a5c018160fecec7508bff0cbc51a0afbf45c`. Counts describe that tree, not later branches. [Findings and priority](README.md).

## Scope and method

Enumerated all 37 asmdefs and 2,954 C# files under `Assets`, assigning each source to its nearest parent asmdef. No `.asmref` files were found. The project has 12 runtime assemblies, 10 editor-only project assemblies, and 15 test assemblies. `Tools.GoodsModel` is editor-only by `includePlatforms`, despite its name lacking the `.Editor` suffix. Platform metadata, rather than directory name or suffix alone, determines the audit boundary.

Read each runtime reference list, then searched namespace imports, aliases, static imports, fully qualified names, namespace-relative qualifiers, and relevant component discovery/reflection call sites. Removed comments from lexical candidates; reviewed candidates against target type declarations and actual usage. Namespace imports alone do not prove a concrete dependency. Strings naming friend assemblies/input assets, documentation references, same-module subnamespaces and Core types were excluded. This is a static source audit with manually reviewed candidates, not a compiler semantic-model proof or a runtime test. Conditional source is included; no Unity execution, player build, scene load, plate, save, or pricing verification is claimed.

The table below records all project runtime assembly references; Unity/package references are outside the feature graph. Core has no project dependency. Every runtime project reference is name-form in this snapshot. No runtime assembly references an editor assembly, and the project graph has no cycles.

## Runtime graph

| Assembly | Project references | Classification |
|---|---|---|
| [App](../../../Assets/_Project/Code/App/HiddenHarbours.App.asmdef) | Core, World, Environment, Boats, Fishing, Economy, Player | Composition |
| [Art](../../../Assets/_Project/Code/Art/HiddenHarbours.Art.asmdef) | Core | Feature |
| [Audio](../../../Assets/_Project/Code/Audio/HiddenHarbours.Audio.asmdef) | Core | Feature |
| [Boats](../../../Assets/_Project/Code/Boats/HiddenHarbours.Boats.asmdef) | Core | Feature |
| [Core](../../../Assets/_Project/Code/Core/HiddenHarbours.Core.asmdef) | None | Contract root |
| [Economy](../../../Assets/_Project/Code/Economy/HiddenHarbours.Economy.asmdef) | Core | Feature |
| [Environment](../../../Assets/_Project/Code/Environment/HiddenHarbours.Environment.asmdef) | Core | Feature |
| [Fishing](../../../Assets/_Project/Code/Fishing/HiddenHarbours.Fishing.asmdef) | Core, Economy | Feature; recorded Economy exception |
| [Player](../../../Assets/_Project/Code/Player/HiddenHarbours.Player.asmdef) | Core, Boats, Economy, Art | Composition |
| [UI](../../../Assets/_Project/Code/UI/HiddenHarbours.UI.asmdef) | Core | Composition |
| [Vehicles](../../../Assets/_Project/Code/Vehicles/HiddenHarbours.Vehicles.asmdef) | Core | Feature |
| [World](../../../Assets/_Project/Code/World/HiddenHarbours.World.asmdef) | Core | Feature |

There are ten directed non-Core runtime project edges: one recorded feature exception, three Player composition edges, and six App composition edges. Nine have concrete runtime consumers. App → Economy is an unused assembly reference in this source snapshot. No new unrecorded feature-to-feature edge was found. UI is allowed composition but currently references only Core.

## Cross-feature import ledger

All 40 non-Core cross-feature `using` directives in runtime project sources are listed below, including the unused import noted on the Player → Boats page. Source lines are pinned to the snapshot. Full concrete type and member inventories are on the individual edge pages. Paths are relative to `Assets/_Project/Code/`.

| Edge | Source/import line | Imported namespace |
|---|---|---|
| App → Boats | [App/ArrivalCabinWalk.cs:3](../../../Assets/_Project/Code/App/ArrivalCabinWalk.cs#L3) | `HiddenHarbours.Boats` |
| App → Boats | [App/ArrivalDeckWalk.cs:3](../../../Assets/_Project/Code/App/ArrivalDeckWalk.cs#L3) | `HiddenHarbours.Boats` |
| App → Boats | [App/ArrivalOpening.cs:3](../../../Assets/_Project/Code/App/ArrivalOpening.cs#L3) | `HiddenHarbours.Boats` |
| App → Boats | [App/NpcPotBoats.cs:4](../../../Assets/_Project/Code/App/NpcPotBoats.cs#L4) | `HiddenHarbours.Boats` |
| App → Boats | [App/PersistentHoldProxy.cs:5](../../../Assets/_Project/Code/App/PersistentHoldProxy.cs#L5) | `HiddenHarbours.Boats` |
| App → Boats | [App/PilotageHelm.cs:2](../../../Assets/_Project/Code/App/PilotageHelm.cs#L2) | `HiddenHarbours.Boats` |
| App → Boats | [App/RegionTravelCoordinator.cs:4](../../../Assets/_Project/Code/App/RegionTravelCoordinator.cs#L4) | `HiddenHarbours.Boats` |
| App → Environment | [App/ArrivalOpening.Sea.cs:2](../../../Assets/_Project/Code/App/ArrivalOpening.Sea.cs#L2) | `HiddenHarbours.Environment` |
| App → Environment | [App/GameRoot.cs:3](../../../Assets/_Project/Code/App/GameRoot.cs#L3) | `HiddenHarbours.Environment` |
| App → Fishing | [App/NpcPotBoats.cs:6](../../../Assets/_Project/Code/App/NpcPotBoats.cs#L6) | `HiddenHarbours.Fishing` |
| App → Player | [App/ArrivalDeckWalk.cs:4](../../../Assets/_Project/Code/App/ArrivalDeckWalk.cs#L4) | `HiddenHarbours.Player` |
| App → Player | [App/ArrivalOpening.cs:4](../../../Assets/_Project/Code/App/ArrivalOpening.cs#L4) | `HiddenHarbours.Player` |
| App → Player | [App/GameRoot.cs:4](../../../Assets/_Project/Code/App/GameRoot.cs#L4) | `HiddenHarbours.Player` |
| App → Player | [App/LandPassageAccess.cs:2](../../../Assets/_Project/Code/App/LandPassageAccess.cs#L2) | `HiddenHarbours.Player` |
| App → Player | [App/RegionTravelCoordinator.cs:5](../../../Assets/_Project/Code/App/RegionTravelCoordinator.cs#L5) | `HiddenHarbours.Player` |
| App → World | [App/ArrivalOpening.Sea.cs:3](../../../Assets/_Project/Code/App/ArrivalOpening.Sea.cs#L3) | `HiddenHarbours.World` |
| App → World | [App/ArrivalOpening.cs:5](../../../Assets/_Project/Code/App/ArrivalOpening.cs#L5) | `HiddenHarbours.World` |
| App → World | [App/ArrivalSeaDef.cs:1](../../../Assets/_Project/Code/App/ArrivalSeaDef.cs#L1) | `HiddenHarbours.World` |
| App → World | [App/NmcWharfBrowSpanVisual.cs:2](../../../Assets/_Project/Code/App/NmcWharfBrowSpanVisual.cs#L2) | `HiddenHarbours.World` |
| Fishing → Economy | [Fishing/DeckPot.cs:3](../../../Assets/_Project/Code/Fishing/DeckPot.cs#L3) | `HiddenHarbours.Economy` |
| Fishing → Economy | [Fishing/DevTrapInput.cs:4](../../../Assets/_Project/Code/Fishing/DevTrapInput.cs#L4) | `HiddenHarbours.Economy` |
| Fishing → Economy | [Fishing/FishingController.cs:3](../../../Assets/_Project/Code/Fishing/FishingController.cs#L3) | `HiddenHarbours.Economy` |
| Fishing → Economy | [Fishing/PlacedTrap.cs:4](../../../Assets/_Project/Code/Fishing/PlacedTrap.cs#L4) | `HiddenHarbours.Economy` |
| Fishing → Economy | [Fishing/PlacedTrapService.cs:4](../../../Assets/_Project/Code/Fishing/PlacedTrapService.cs#L4) | `HiddenHarbours.Economy` |
| Player → Art | [Player/CarriableBucket.cs:4](../../../Assets/_Project/Code/Player/CarriableBucket.cs#L4) | `HiddenHarbours.Art` |
| Player → Art | [Player/CarriableCatch.cs:3](../../../Assets/_Project/Code/Player/CarriableCatch.cs#L3) | `HiddenHarbours.Art` |
| Player → Art | [Player/CarriableFuelContainer.cs:3](../../../Assets/_Project/Code/Player/CarriableFuelContainer.cs#L3) | `HiddenHarbours.Art` |
| Player → Art | [Player/CarriableTool.cs:3](../../../Assets/_Project/Code/Player/CarriableTool.cs#L3) | `HiddenHarbours.Art` |
| Player → Art | [Player/CarryHands.cs:3](../../../Assets/_Project/Code/Player/CarryHands.cs#L3) | `HiddenHarbours.Art` |
| Player → Art | [Player/DeckRiderMeshPresenter.cs:1](../../../Assets/_Project/Code/Player/DeckRiderMeshPresenter.cs#L1) | `HiddenHarbours.Art` |
| Player → Art | [Player/DriveSeatMath.cs:2](../../../Assets/_Project/Code/Player/DriveSeatMath.cs#L2) | `HiddenHarbours.Art` |
| Player → Art | [Player/RodFightPresenter.cs:3](../../../Assets/_Project/Code/Player/RodFightPresenter.cs#L3) | `HiddenHarbours.Art` |
| Player → Boats | [Player/ControlSwitcher.cs:4](../../../Assets/_Project/Code/Player/ControlSwitcher.cs#L4) | `HiddenHarbours.Boats` |
| Player → Boats | [Player/DeckRiderMeshPresenter.cs:2](../../../Assets/_Project/Code/Player/DeckRiderMeshPresenter.cs#L2) | `HiddenHarbours.Boats` |
| Player → Boats | [Player/DeckRiderVisual.cs:2](../../../Assets/_Project/Code/Player/DeckRiderVisual.cs#L2) | `HiddenHarbours.Boats` |
| Player → Boats | [Player/DeckWalkController.cs:2](../../../Assets/_Project/Code/Player/DeckWalkController.cs#L2) | `HiddenHarbours.Boats` |
| Player → Boats | [Player/MooringController.cs:4](../../../Assets/_Project/Code/Player/MooringController.cs#L4) | `HiddenHarbours.Boats` |
| Player → Economy | [Player/ControlSwitcher.cs:5](../../../Assets/_Project/Code/Player/ControlSwitcher.cs#L5) | `HiddenHarbours.Economy` |
| Player → Economy | [Player/GinnyFreezer.cs:5](../../../Assets/_Project/Code/Player/GinnyFreezer.cs#L5) | `HiddenHarbours.Economy` |
| Player → Economy | [Player/WetBucketPoint.cs:4](../../../Assets/_Project/Code/Player/WetBucketPoint.cs#L4) | `HiddenHarbours.Economy` |

Three fully qualified runtime feature uses supplement the imports:

| Edge | Source | Token |
|---|---|---|
| Player → Art | [Player/CarryHands.cs:84](../../../Assets/_Project/Code/Player/CarryHands.cs#L84) | `HiddenHarbours.Art.CatchItemLibrary` |
| Player → Art | [Player/CarryHands.cs:599](../../../Assets/_Project/Code/Player/CarryHands.cs#L599) | `HiddenHarbours.Art.CatchItemLibrary` |
| Player → Art | [Player/CarryHands.cs:602](../../../Assets/_Project/Code/Player/CarryHands.cs#L602) | `HiddenHarbours.Art.CatchItemLibrary` |

## Exclusions and negative findings

- Boats, Vehicles, Economy, Environment, World, Audio and Art have no concrete runtime feature dependency outside Core. World imports of its own TerrainPlan namespace are not cross-module references. Core and UI have no concrete feature consumers. These are source/assembly findings, not a claim that all goods behavior meets ADR 0055.
- App/Art/Core `InternalsVisibleTo` strings grant access to tests or editor tooling; they do not create reverse runtime references. Art/Boats strings naming the input-actions asset are not module names. Comments citing feature APIs are documentation.
- All ten project editor-only assemblies may inspect or bake concrete features under the documented editor exception; the fifteen test assemblies may reference subjects under test. Their assembly references were included in the boundary/cycle inventory; their internal type call sites are outside this runtime feature-edge audit.
- No scenes, prefabs, shader contents or serialized asset references were used to infer an executable edge. This audit cannot establish which inspector fields are populated in a shipped scene.

## Reproduction

From the repository root at the snapshot, enumerate and inspect (no editor required):

```text
rg --files Assets -g "*.asmdef" -g "*.asmref" -g "*.cs"
rg -n "HiddenHarbours\." Assets/_Project/Code -g "*.asmdef"
rg -n "^\s*(global\s+)?using\s+" Assets/_Project/Code -g "*.cs"
rg -n "HiddenHarbours\.|Type.GetType|GetComponent|SendMessage" Assets/_Project/Code -g "*.cs"
```

Resolve each source through its nearest asmdef and inspect type declarations, members and namespace-relative qualifiers before counting an edge. In particular, do not classify a source as runtime merely because its assembly name lacks `.Editor`, or count comment/string matches as concrete references. Re-read the per-edge source ranges after any change of base.

## Existing guard: what it does and misses

[`ModuleBoundaryTests.cs:15`](../../../Assets/Tests/EditMode/ModuleBoundaryTests.cs#L15) permits App, Player and UI as composition; lines 31–45 permit the entire Fishing → Economy assembly edge; lines 50–67 check runtime/editor references and cycles. The reader at lines 84–117 resolves GUID-form references but scans only `Assets/_Project/Code`. `Assets/_Project/Art/Editor` lies outside that reader; it is included in this audit. The test class has four `[Test]` methods. They were inspected, not executed.

Unexecuted guard failure case: add another Economy concrete consumer to Fishing without changing the already-permitted asmdef edge. The existing graph test has no source/type assertion that could detect that expansion. Likewise a new concrete reference inside the allowed App/Player/UI lists is not checked for goods authority, reach, item conservation or event ordering. This is a coverage gap, not an assertion that the current four graph tests fail.

A later guard proposal should pin the small temporary exception by reviewed concrete type/use-site inventory, resolve assembly ownership and editor platform constraints, and exercise transfer conservation separately. Do not remove the graph assertions, broaden the exception, replace exact counts with minimums, or treat a regex match as semantic proof. Removing an assembly edge requires compiling all configurations and preserving serialized Def identity; no such removal occurs here.
