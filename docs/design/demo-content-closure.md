# Island demo content closure

An island-only scene list retains **336,529,942 bytes (320.94 MiB)** across **7,581 project source files** on the pinned base `f808af6ae5d5a0041f3d2147d74b1ddef7dce609`. Another **179,024,831 bytes (170.74 MiB)** across **4,935 files** have no route from the recorded demo roots. The largest avoidable inclusion is the Nine Mile Creek road floor: **162,807,070 bytes (155.26 MiB), 3,745 files**, copied through StreamingAssets even though its scene is absent. Scene selection alone does not make a small demo.

**Distribution status:** [the corrected demo configuration](https://github.com/apholmes7-bot/hiddenharbours/pull/1510) blocks both release and development demo packaging until an authored island opening is implemented and verified. Its save slot is isolated at `demo/savegame.json`; the full game retains `savegame.json`, with no automatic import. This source audit neither opens saves nor enables packaging. The single-island closure remains a prospective content budget, not evidence of a releasable demo. The configuration at `5ed2f3e1c55c05a4f1a6434eb121a52cd170d8a9` is a separate dependency and its source changes are not included in this measurement. It includes and replaces #1504: destination refusal and its runtime scene-availability correction travel in #1510 together. An unavailable scene must be refused safely even if its data remains reachable.

**Ending dependency:** This packaging audit expects [#1572](https://github.com/apholmes7-bot/hiddenharbours/pull/1572), inspected at `28ecb02f4b13321612d3cc54555aa69ecb08b01f`, and its #1511 and #1510 prerequisites. Ginny's completed word, available from 17:00 until before 04:30 once the island facts hold, records `st_peters.ginny_word_completed` and raises `StoryWordGiven`. In demo mode, `DemoWeek` records `demo.island_week_ended` in the existing flag list and raises `DemoEnded`. `DemoClosingCard` listens to that event, pauses the world and offers the existing return-to-title path. A fresh deserialized save reads the ended flag without replaying the event or card. #1535's saved-sleep ending is superseded and must not be included as a second publisher. No sleep, autosave, weather-open flag or day rollover substitutes for her word; the demo keeps outward routes closed.

The source-root contract stays the island scene plus shared runtime content. A closing card is not a second gameplay region. #1572 now supplies `DemoClosingCard`, text in `ShellStrings` and Resources story-gate data. Those files are covered by the existing runtime-source and Resources root rules, but that separate draft is **not included in the byte measurement above**. After integration, rerun the same closure and missing-reference checks without waiving dependencies or increasing the byte ceiling. No root, exception or budget changes here.

Before distribution, verify a successful demo-slot write through the word and ended flag before the card; a refused, deferred or failed write must not masquerade as completion. #1572 calls `SetFlag` before publishing, but the real save service can return without writing; its ending tests use in-memory saves, so they do not establish disk commit or failure recovery. Check relaunch with a fresh deserialized save, the card's return-to-title button and full-game suppression. The household ending has a host-write seam, but guest word relay, event delivery, guest exit/pause authority and reconnect behavior still need multiplayer integration and proof. Source reachability establishes none of these release checks. Save version 26 is unchanged.

These are **source bytes, not download, installed, imported-texture or resident-memory sizes**. Registry-package content, engine binaries, compiled code, compression, generated import artifacts and metadata bytes are outside this number. All runtime source and shader files are conservatively retained, even where stripping might remove compiled output. The two sets partition the audited project-source inventory; the unreachable set is a review list, not permission to delete files or a promised reduction in a packaged player.

## Ten heaviest reachable files

| Source file | Bytes | Why retained |
| --- | ---: | --- |
| `Assets/_Project/Data/Boats/HullMeshes/ExpeditionYacht120IsoHullMesh.asset` | 8,267,941 | `Assets/_Project/Scenes/StPeters.unity` |
| `Assets/_Project/Data/Boats/HullMeshes/SportFisherSkybridgeIsoHullMesh.asset` | 4,967,836 | `Assets/_Project/Scenes/StPeters.unity` |
| `Assets/_Project/Scenes/StPeters.unity` | 4,877,271 | `Scene root` |
| `Assets/_Project/Data/Boats/HullMeshes/MotorYacht84IsoHullMesh.asset` | 3,829,803 | `Assets/_Project/Scenes/StPeters.unity` |
| `Assets/_Project/Data/Boats/HullMeshes/SportFisherConvertibleIsoHullMesh.asset` | 3,759,255 | `Assets/_Project/Scenes/StPeters.unity` |
| `Assets/StreamingAssets/RoadFloor/NineMileCreek/RoadFloor.json` | 2,422,656 | `StreamingAssets copy` |
| `Assets/_Project/Data/Characters/Skin/skipper.asset` | 2,219,245 | `Assets/_Project/Scenes/StPeters.unity` |
| `Assets/_Project/Data/Characters/Skin/nan.asset` | 2,139,964 | `Assets/_Project/Scenes/StPeters.unity` |
| `Assets/_Project/Data/Characters/Skin/packer.asset` | 2,091,565 | `Assets/_Project/Scenes/StPeters.unity` |
| `Assets/_Project/Data/Characters/Skin/girl.asset` | 2,090,728 | `Assets/_Project/Resources/CharacterBuilder10.asset` |

## Ten heaviest files outside the closure

These can stay out of the island package under the audited reference rules. Some, such as the scene template, would normally be absent from any player already. Nothing is removed in this change.

| Source file | Bytes |
| --- | ---: |
| `Assets/_Project/Scenes/NineMileCreek.unity` | 12,934,571 |
| `Assets/Settings/Lit2DSceneTemplate.scenetemplate` | 3,942,809 |
| `Assets/_Project/Art/Sprites/Shipyard/Iso/smallYard.png` | 2,974,929 |
| `Assets/_Project/Art/Sprites/Shipyard/Iso/industrialYard.png` | 2,572,859 |
| `Assets/_Project/Art/Sprites/Shipyard/Iso/largeYard.png` | 2,514,924 |
| `Assets/_Project/Art/Sprites/Shipyard/Iso/workingYard.png` | 2,015,516 |
| `Assets/_Project/Data/Characters/SkinNext/nan_rig11.asset` | 2,013,026 |
| `Assets/_Project/Data/Characters/SkinNext/fisher_rig11.asset` | 1,922,341 |
| `Assets/_Project/Art/Sprites/Shipyard/Iso/shed.png` | 1,540,436 |
| `Assets/_Project/Data/Dwellings/Meshes/CamperClipperDwellingMesh.asset` | 1,529,994 |

## Where other places and later boats enter

- `Assets/StreamingAssets/RoadFloor/NineMileCreek/` is copied in full. Removing the town from the scene list does not affect it. A later packaging change must explicitly scope streaming content and prove the full-game copy still works.
- `Assets/_Project/Data/Resources/WorldMap.asset` references region Defs including Cumberland and Rice Point. Those are data references, not scene-load roots: their `SceneName` strings do not pull in the region scenes.
- `Assets/_Project/Resources/NpcTrawlFleet.asset` reaches the Rice Point trawl-day data. `MoodGrade/NineMileCreek.asset` and `MoodGrade/CoddleCove.asset` are themselves Resources roots. The tanker interior-cell asset also ships because it lives under Resources. A purchase restriction does not exclude any of these files.
- St Peters itself reaches later hulls. One exact route is `StPeters.unity -> Data/Boats/ExpeditionYacht120.asset -> Data/Boats/Visuals/ExpeditionYacht120Iso.asset -> Data/Boats/HullMeshes/ExpeditionYacht120IsoHullMesh.asset` (paths after the scene are relative to `Assets/_Project/`). The 43 reachable files under `Data/Boats/HullMeshes/` total **50,131,503 bytes**. Turning off a development hull-switch key does not erase serialized references. A future demo-specific content catalog or scene wiring would be needed to remove those dependencies; neither is changed here.
- Shared character, fish, conversation and appearance libraries under Resources retain their entire referenced catalogs. They are not filtered by first-week availability.
- There is **no regional music saving to claim on this base**: `Audio/Music` has no clip files, `AudioClipSet.asset` has `Songs: []`, and all 23 audio clips are reachable. The two unreachable files in the Audio directory are documentation, not music.

## Exact set definition and reproduction

The implementation is `Assets/Tests/EditMode/DemoContentClosure.cs`; the byte budget, roots, importer inputs and **single `knownExceptions` list** are in `Assets/Tests/EditMode/Fixtures/DemoContentBudget.json`. `AssetGuidIndex` is the existing `RegionSceneTileRefsTests.IndexMetas` implementation extracted unchanged for both audits. No previous source-closure or demo-size fixture existed; the existing tile-reference guard continues to use the shared index.

The universe is importable files beneath Assets plus embedded package assets with metadata, excluding metadata itself, Editor, Editor Default Resources, Gizmos, Tests and ignored names. Folders and registry package caches are not assets in this universe. The **reachable set** is the transitive closure of the following roots and dependency rules. The **unreachable set** is every other file in that universe. Every file is counted once, even when roots or references overlap.

1. `Assets/_Project/Scenes/StPeters.unity`. `PlayerBuild.cs` starts its explicit six-scene list with this scene. The accepted demo configuration keeps that first scene once its packaging prerequisite is met. The editor build-settings list also contains prototypes; those are not demo roots. There is no committed Bootstrap or separate always-loaded scene. `GameRoot` is serialized into St Peters and uses `DontDestroyOnLoad`; the shell is a state of that persistent core, not another scene.
2. Every player Resources file, whether or not any code happens to call its resource key during the week, and all StreamingAssets files. Runtime source, plugins, shader sources and includes are conservative roots to avoid pretending a serialized-script scan models managed-code stripping or dynamic shader lookup.
3. Asset GUID references from graphics, quality and player settings, plus only `m_configObjects` from editor build settings. `preloadedAssets` is empty on this base. There is no Addressables package, settings asset or bundle catalog. The package manifest is a checked semantic source contract: object order and whitespace do not affect its hash, while every key, value and array order does. Adding a delivery path or changing a package still requires a new audit. The existing editor JSON parser is reused; the standalone Windows reader uses the framework JSON parser and the same canonical hash.
4. Serialized GUID references in scenes, prefabs, materials, Defs, other text assets and their import metadata; JSON asset GUIDs and local shader includes. Built-in engine GUIDs have no project source bytes. Code comments are not asset references. Each scene reference retains its serialized object file ID and line for a useful failure message.
5. Custom terrain importers construct dependencies from strings. The checked importer-input data records **63 detail PNGs, 180 relight PNGs plus their manifest, and 15 shore-edge PNGs**. Their recipe/source hashes use UTF-8 with LF line endings. A changed recipe, importer, packer or relight manifest fails `DemoContent.ImportContractChanged` until the dependencies are re-audited. This observes existing inputs; it does not bake, change or judge their appearance.

Run the same reader without opening an editor:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build/measure-demo-content.ps1 -OutputDirectory Evidence~/demo-content
```

The command writes `content-closure.tsv` (the **complete two sets**, source bytes, disk versus LFS-pointer size, and one deterministic dependency chain for every reachable path) and `content-closure.json` (roots, top-ten lists, every pointer path, unresolved references and guard failures). Output is restricted to Evidence~ and is not committed. The EditMode fixture builds the same graph directly; there is no second approximation in another language. Both paths run the same budget and exception checks.

## Guard and known limits

The original recorded source budget remains **336,273,589 bytes**, with a data-held **16,777,216-byte margin**. The exact ceiling is **353,050,805 bytes**. The current scan is 256,353 bytes above the original measurement and still inside that unchanged ceiling, with zero guard violations. Main added saved purchases and bags plus dormant island barriers. The package manifest contract now ignores formatting only. Five exact registry include paths are recorded alongside the existing external GUID exceptions; no project-reference exception or byte allowance was enlarged. The margin allows small additions and text line-ending differences; it is not a new memory or download target. The guard fails at the first byte above it with `DemoContent.ReachableSizeGrew`. Missing roots, missing dependency targets, opaque serialized inputs and stale exceptions cannot quietly shrink the measured set.

`DemoContent.ForeignSceneReference` names any direct St Peters object reference whose target is beneath a foreign-region folder prefix in the budget. There are **zero such references on this base**. A region-named Def in a shared data folder is not the same condition, and the Resources and StreamingAssets inclusions above remain visible in the inventory. The guard is about direct scene-object references; it does not claim every nested library must be island-exclusive. Folder prefixes are explicit reviewable data, including the existing legacy place spellings.

There are **196 distinct unresolved registry-package GUIDs**, recorded as `ExternalPackageGuid` exceptions in the one list. They are seen in render-pipeline settings, the shared default sprite material, the pixel-perfect camera and the input importer. Their package bytes cannot be obtained from this checkout and are deliberately not guessed. The remaining exact exception is the island object's old `_detailArray512` GUID (`8c0da9ac9cc7ea640b61f54b9647b3ce`, object `1201741992`). The field has no declaration on the current `TerrainSplatSurface`, and its old asset is absent. This is recorded as unresolved legacy serialization, not a claimed live texture or a licence to waive future missing references. No scene is edited.

The five external include paths are the core package?s `Runtime/Utilities/Blit.hlsl` and `ShaderLibrary/Common.hlsl`, plus the universal package?s `ShaderLibrary/Core.hlsl`, `Shaders/2D/Include/Core2D.hlsl` and `Shaders/2D/Include/2DCommon.hlsl`. They are recorded whether or not the editor resolves package bytes locally. An exception applies only to that exact package include; a changed path, missing project include or stale exception still fails. Package bytes remain outside this source-only budget.

The source closure cannot predict engine-generated assets, import expansion, compression, or package dependency sizes. A player build and its build report are still required to measure the deliverable. No Unity run, EditMode run, player build or visual comparison was performed locally. The standalone C# source audit ran without importing assets and reported zero violations; the 24 authored EditMode cases are for CI. The disk fixture now uses an explicit project-relative evidence directory because the runner can leave its work-directory property unset. No visual assets, existing tuning values, save fields or scene lists changed.

## LFS objects absent from the checkout

The inventory uses the declared `size` from a valid LFS pointer when object bytes are not hydrated at that file. It does not fetch or hydrate anything, and it does not claim the object is absent from every local object store. **162 reachable pointers contribute 5,300,578 bytes; 52 unreachable pointers contribute 1,208,744 bytes.** All remaining sizes are file lengths on disk. The paths below are the complete pointer-size subset, so the measurement does not hide small placeholder files as real images.

| Set | Source file | Declared bytes |
| --- | --- | ---: |
| reachable | `Assets/_Project/Art/Fishing/GreenCrabDeck.png` | 1,998 |
| reachable | `Assets/_Project/Art/Fishing/Icons/FishIcon_bass.png` | 524 |
| reachable | `Assets/_Project/Art/Fishing/Icons/FishIcon_cod.png` | 409 |
| reachable | `Assets/_Project/Art/Fishing/Icons/FishIcon_cunner.png` | 786 |
| reachable | `Assets/_Project/Art/Fishing/Icons/FishIcon_flounder.png` | 472 |
| reachable | `Assets/_Project/Art/Fishing/Icons/FishIcon_haddock.png` | 342 |
| reachable | `Assets/_Project/Art/Fishing/Icons/FishIcon_herring.png` | 362 |
| reachable | `Assets/_Project/Art/Fishing/Icons/FishIcon_mackerel.png` | 517 |
| reachable | `Assets/_Project/Art/Fishing/Icons/FishIcon_pollock.png` | 374 |
| reachable | `Assets/_Project/Art/Fishing/Icons/FishIcon_smelt.png` | 456 |
| reachable | `Assets/_Project/Art/Fishing/Icons/FishIcon_tomcod.png` | 599 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_bait_capelin.png` | 389 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_bait_fish_scrap.png` | 535 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_bait_herring.png` | 512 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_bait_mackerel.png` | 418 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_bait_sea_worm.png` | 348 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_bait_shucked_clam.png` | 463 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_bait_squid_strip.png` | 277 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_cod_jig.png` | 200 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_crab_pot.png` | 553 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_depth_sounder.png` | 395 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_fish_finder.png` | 390 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_gps.png` | 375 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_harbour_watch.png` | 251 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_headlamp.png` | 446 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_lobster_pot.png` | 662 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_longline_tub.png` | 425 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_loran.png` | 249 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_mackerel_feathers.png` | 343 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_mussel_sack.png` | 376 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_oyster_tongs.png` | 269 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_rod.png` | 224 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_rod_rack.png` | 480 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_shellfish_basket.png` | 435 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_shellfish_rake.png` | 237 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_soft_bait.png` | 221 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_spinner.png` | 243 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_spoon.png` | 204 |
| reachable | `Assets/_Project/Art/Fishing/Icons/GearIcon_trawl_net.png` | 407 |
| reachable | `Assets/_Project/Art/Fishing/Icons/ShellIcon_american_oyster.png` | 455 |
| reachable | `Assets/_Project/Art/Fishing/Icons/ShellIcon_atlantic_surf_clam.png` | 919 |
| reachable | `Assets/_Project/Art/Fishing/Icons/ShellIcon_blue_mussel.png` | 268 |
| reachable | `Assets/_Project/Art/Fishing/Icons/ShellIcon_green_crab.png` | 1,132 |
| reachable | `Assets/_Project/Art/Fishing/Icons/ShellIcon_lobster.png` | 581 |
| reachable | `Assets/_Project/Art/Fishing/Icons/ShellIcon_northern_quahog.png` | 797 |
| reachable | `Assets/_Project/Art/Fishing/Icons/ShellIcon_rock_crab.png` | 465 |
| reachable | `Assets/_Project/Art/Fishing/Icons/ShellIcon_sea_scallop.png` | 545 |
| reachable | `Assets/_Project/Art/Fishing/Icons/ShellIcon_soft_shell_clam.png` | 266 |
| unreachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_cradle.png` | 2,891 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_dart.png` | 3,731 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_deck.png` | 4,919 |
| unreachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_gill.png` | 2,702 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_jump.png` | 8,262 |
| unreachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_lg_cradle.png` | 3,554 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_lg_dart.png` | 4,632 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_lg_deck.png` | 5,931 |
| unreachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_lg_gill.png` | 3,304 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_lg_jump.png` | 10,112 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_lg_roll.png` | 7,212 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_lg_shadow.png` | 2,196 |
| unreachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_lg_swim.png` | 8,873 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_lg_tail.png` | 3,108 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_lg_thrash.png` | 6,717 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_roll.png` | 5,698 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_shadow.png` | 2,021 |
| unreachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_sm_cradle.png` | 2,421 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_sm_dart.png` | 3,158 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_sm_deck.png` | 4,187 |
| unreachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_sm_gill.png` | 2,300 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_sm_jump.png` | 6,732 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_sm_roll.png` | 4,635 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_sm_shadow.png` | 1,899 |
| unreachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_sm_swim.png` | 6,266 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_sm_tail.png` | 2,260 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_sm_thrash.png` | 4,466 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_swim.png` | 7,225 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_tail.png` | 2,681 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_cunner_thrash.png` | 5,527 |
| unreachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_cradle.png` | 1,933 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_dart.png` | 2,937 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_deck.png` | 3,534 |
| unreachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_gill.png` | 2,028 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_jump.png` | 6,032 |
| unreachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_lg_cradle.png` | 2,079 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_lg_dart.png` | 3,257 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_lg_deck.png` | 3,922 |
| unreachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_lg_gill.png` | 2,197 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_lg_jump.png` | 6,448 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_lg_roll.png` | 4,383 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_lg_shadow.png` | 1,930 |
| unreachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_lg_swim.png` | 6,391 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_lg_tail.png` | 2,075 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_lg_thrash.png` | 4,152 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_roll.png` | 4,079 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_shadow.png` | 1,858 |
| unreachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_sm_cradle.png` | 1,855 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_sm_dart.png` | 2,738 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_sm_deck.png` | 3,371 |
| unreachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_sm_gill.png` | 1,816 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_sm_jump.png` | 5,557 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_sm_roll.png` | 3,754 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_sm_shadow.png` | 1,822 |
| unreachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_sm_swim.png` | 5,510 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_sm_tail.png` | 1,879 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_sm_thrash.png` | 3,601 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_swim.png` | 5,817 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_tail.png` | 2,003 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_smelt_thrash.png` | 3,876 |
| unreachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_cradle.png` | 2,550 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_dart.png` | 3,372 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_deck.png` | 4,323 |
| unreachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_gill.png` | 2,425 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_jump.png` | 7,208 |
| unreachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_lg_cradle.png` | 2,992 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_lg_dart.png` | 4,043 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_lg_deck.png` | 5,116 |
| unreachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_lg_gill.png` | 2,751 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_lg_jump.png` | 8,387 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_lg_roll.png` | 5,701 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_lg_shadow.png` | 2,057 |
| unreachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_lg_swim.png` | 7,617 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_lg_tail.png` | 2,784 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_lg_thrash.png` | 5,665 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_roll.png` | 4,711 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_shadow.png` | 1,964 |
| unreachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_sm_cradle.png` | 2,103 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_sm_dart.png` | 2,914 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_sm_deck.png` | 3,654 |
| unreachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_sm_gill.png` | 2,104 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_sm_jump.png` | 6,283 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_sm_roll.png` | 4,152 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_sm_shadow.png` | 1,856 |
| unreachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_sm_swim.png` | 5,852 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_sm_tail.png` | 2,059 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_sm_thrash.png` | 4,124 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_swim.png` | 6,701 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_tail.png` | 2,406 |
| reachable | `Assets/_Project/Art/Fishing/Iso/Fish_tomcod_thrash.png` | 4,753 |
| reachable | `Assets/_Project/Art/Resources/Appliances/Appliance_basin.png` | 42,559 |
| reachable | `Assets/_Project/Art/Resources/Appliances/Appliance_bath.png` | 141,002 |
| reachable | `Assets/_Project/Art/Resources/Appliances/Appliance_chestFreezer.png` | 54,606 |
| reachable | `Assets/_Project/Art/Resources/Appliances/Appliance_chestFreezerLarge.png` | 67,635 |
| reachable | `Assets/_Project/Art/Resources/Appliances/Appliance_cookStove.png` | 7,800 |
| reachable | `Assets/_Project/Art/Resources/Appliances/Appliance_fridge.png` | 46,937 |
| reachable | `Assets/_Project/Art/Resources/Appliances/Appliance_kitchenSink.png` | 76,689 |
| reachable | `Assets/_Project/Art/Resources/Appliances/Appliance_microwave.png` | 3,905 |
| reachable | `Assets/_Project/Art/Resources/Appliances/Appliance_outhouse.png` | 63,441 |
| reachable | `Assets/_Project/Art/Resources/Appliances/Appliance_prepCounter.png` | 9,150 |
| reachable | `Assets/_Project/Art/Resources/Appliances/Appliance_shower.png` | 93,055 |
| reachable | `Assets/_Project/Art/Resources/Appliances/Appliance_toilet.png` | 17,998 |
| reachable | `Assets/_Project/Art/Resources/Appliances/Appliance_wharfShower.png` | 34,747 |
| reachable | `Assets/_Project/Art/Sprites/Buildings/Ginny/Village_ginnyCottage.png` | 521,048 |
| reachable | `Assets/_Project/Art/Sprites/Buildings/Ginny/Village_ginnyCottage_emit.png` | 13,927 |
| reachable | `Assets/_Project/Art/Sprites/Buildings/Ginny/Village_ginnyCottage_mask.png` | 344,298 |
| reachable | `Assets/_Project/Art/Sprites/Buildings/Ginny/Village_ginnyCottage_normal.png` | 115,169 |
| unreachable | `Assets/_Project/Art/Sprites/Buildings/NineMileCreek/NmcCommunityHall_cutaway.png` | 7,282 |
| unreachable | `Assets/_Project/Art/Sprites/Buildings/NineMileCreek/NmcCommunityHall_exterior.png` | 18,642 |
| unreachable | `Assets/_Project/Art/Sprites/Buildings/NineMileCreek/NmcFishPlant_cutaway.png` | 28,628 |
| unreachable | `Assets/_Project/Art/Sprites/Buildings/NineMileCreek/NmcFishPlant_exterior.png` | 53,800 |
| reachable | `Assets/_Project/Art/Sprites/Buildings/Shops/Signs/SoldOutCard.png` | 210 |
| reachable | `Assets/_Project/Art/Sprites/Buildings/Village/Village_harbourGable.png` | 276,264 |
| reachable | `Assets/_Project/Art/Sprites/Buildings/Village/Village_harbourGable_emit.png` | 14,040 |
| reachable | `Assets/_Project/Art/Sprites/Buildings/Village/Village_harbourGable_mask.png` | 267,674 |
| reachable | `Assets/_Project/Art/Sprites/Buildings/Village/Village_harbourGable_normal.png` | 87,848 |
| reachable | `Assets/_Project/Art/Sprites/Buildings/Village/Village_redSaltbox.png` | 322,543 |
| reachable | `Assets/_Project/Art/Sprites/Buildings/Village/Village_redSaltbox_mask.png` | 284,900 |
| reachable | `Assets/_Project/Art/Sprites/Buildings/Village/Village_redSaltbox_normal.png` | 94,784 |
| reachable | `Assets/_Project/Art/Sprites/ClamHole.png` | 171 |
| reachable | `Assets/_Project/Art/Sprites/ClamSquirt.png` | 402 |
| reachable | `Assets/_Project/Art/Sprites/Crate.png` | 407 |
| unreachable | `Assets/_Project/Art/Sprites/Fixtures/Fixture_bath.png` | 2,924 |
| unreachable | `Assets/_Project/Art/Sprites/Fixtures/Fixture_bathroomSink.png` | 1,415 |
| unreachable | `Assets/_Project/Art/Sprites/Fixtures/Fixture_fridge.png` | 2,925 |
| unreachable | `Assets/_Project/Art/Sprites/Fixtures/Fixture_fridgeModern.png` | 2,851 |
| unreachable | `Assets/_Project/Art/Sprites/Fixtures/Fixture_iceCooler.png` | 1,791 |
| unreachable | `Assets/_Project/Art/Sprites/Fixtures/Fixture_kitchenSink.png` | 4,456 |
| unreachable | `Assets/_Project/Art/Sprites/Fixtures/Fixture_microwave.png` | 2,801 |
| unreachable | `Assets/_Project/Art/Sprites/Fixtures/Fixture_outhouse.png` | 11,879 |
| unreachable | `Assets/_Project/Art/Sprites/Fixtures/Fixture_prepCounter.png` | 2,629 |
| unreachable | `Assets/_Project/Art/Sprites/Fixtures/Fixture_shower.png` | 5,645 |
| unreachable | `Assets/_Project/Art/Sprites/Fixtures/Fixture_stove.png` | 3,305 |
| unreachable | `Assets/_Project/Art/Sprites/Fixtures/Fixture_toilet.png` | 4,864 |
| unreachable | `Assets/_Project/Art/Sprites/Fixtures/Fixture_toiletModern.png` | 2,750 |
| unreachable | `Assets/_Project/Art/Sprites/Fixtures/Fixture_washstand.png` | 4,676 |
| unreachable | `Assets/_Project/Art/Sprites/Fixtures/Fixture_wharfShower.png` | 6,255 |
| reachable | `Assets/_Project/Art/Sprites/Interiors/Painted/GinnyCottage_Upstairs.png` | 13,775 |
| reachable | `Assets/_Project/Art/Sprites/Interiors/Rooms/Interior_ginnyCottage.png` | 255,452 |
| reachable | `Assets/_Project/Art/Sprites/Interiors/Rooms/Interior_harbourGable.png` | 316,570 |
| reachable | `Assets/_Project/Art/Sprites/Interiors/Rooms/Interior_sageCottage.png` | 219,503 |
| unreachable | `Assets/_Project/Art/Sprites/NineMileCreek/FuelStop/SetPiece_bench.png` | 4,338 |
| unreachable | `Assets/_Project/Art/Sprites/NineMileCreek/FuelStop/SetPiece_picnic.png` | 6,973 |
| reachable | `Assets/_Project/Art/Terrain/Cliffs/Decals/Toe_E.png` | 3,148 |
| reachable | `Assets/_Project/Art/Terrain/Cliffs/Decals/Toe_E_slump.png` | 3,398 |
| reachable | `Assets/_Project/Art/Terrain/Cliffs/Decals/Toe_S.png` | 2,817 |
| reachable | `Assets/_Project/Art/Terrain/Cliffs/Decals/Toe_SE.png` | 3,148 |
| reachable | `Assets/_Project/Art/Terrain/Cliffs/Decals/Toe_SE_slump.png` | 3,398 |
| reachable | `Assets/_Project/Art/Terrain/Cliffs/Decals/Toe_SW.png` | 2,817 |
| reachable | `Assets/_Project/Art/Terrain/Cliffs/Decals/Toe_SW_slump.png` | 3,020 |
| reachable | `Assets/_Project/Art/Terrain/Cliffs/Decals/Toe_S_slump.png` | 3,020 |
| unreachable | `Assets/_Project/Art/Terrain/Cliffs/Decals/Toe_W.png` | 2,817 |
| unreachable | `Assets/_Project/Art/Terrain/Cliffs/Decals/Toe_W_slump.png` | 3,020 |
| unreachable | `Assets/_Project/Data/Terrain/RicePointGround/RicePointSeabed_HeightTex_plan.png` | 905,723 |
| unreachable | `Assets/_Project/Data/Terrain/RicePointGround/RicePointSplatA.png` | 12,441 |
| unreachable | `Assets/_Project/Data/Terrain/RicePointGround/RicePointSplatB.png` | 9,020 |
| unreachable | `Assets/_Project/Data/Terrain/RicePointGround/RicePointSplatC.png` | 4,638 |
| unreachable | `Assets/_Project/Data/Terrain/RicePointGround/RicePointSplatE.png` | 5,742 |
| reachable | `Assets/_Project/Data/Terrain/StPetersSeabed_HeightTex.png` | 459,851 |
| reachable | `Assets/_Project/Data/Terrain/StPetersSeabed_SeabedTex.png` | 400,486 |
| reachable | `Assets/_Project/Data/Terrain/StPetersSplatA.png` | 53,896 |
| reachable | `Assets/_Project/Data/Terrain/StPetersSplatB.png` | 74,594 |
| reachable | `Assets/_Project/Data/Terrain/StPetersSplatC.png` | 43,806 |
| reachable | `Assets/_Project/Data/Terrain/StPetersSplatD.png` | 42,691 |
| reachable | `Assets/_Project/Data/Terrain/StPetersSplatE.png` | 54,032 |
| reachable | `Assets/_Project/Data/Terrain/StPetersStillWater.png` | 34,786 |
