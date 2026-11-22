# Remaining legacy fish art audit

Audited main: `1abca5974ce3d16a3257777797bb2547c9ad8d53` (2026-11-22).
All line numbers below refer to that commit. This is a source and committed-reference audit,
not a report of a new play session.

**No remaining player-facing reference to the retired sprite set was found.** There are
zero class **(a)** reference swaps and zero class **(b)** missing-species replacements in this
audit. Do not create replacement art or change a reference just to make this list nonempty.
The remaining class **(c)** work is retirement-guard maintenance, not a demonstrated runtime
old-fish bug. The guard still exempts the retired soft-shell clam although its current icon
is already wired. Nothing in this audit warrants changing a scene, size, pivot or importer.

## Scope and method

- Read GUIDs from the retired PNG metadata and searched 7,833 tracked text files under
  `Assets`: assets, prefabs, scenes, atlases, materials, animations, controllers, source,
  interface markup/styles and JSON. Scenes and other protected assets were read only.
- Expanded the initial 25-file retirement inventory to 27 by adding the two flat trap-kit
  icons. All 27 have **zero GUID references** in those text file types. The shoreline group
  deliberately includes non-animal finds as a conservative check.
- Checked all 19 `Data/Fish` Def sprites against their 19 `Resources/IconLibrary` entries:
  exact `(GUID, fileID)` agreement, all pointing at current `Art/Fishing/Icons` PNGs.
- Checked 10 fight species against 10 swim-library species, plus the existence of 90 iso
  deck/tail/swim sheets (10 species x three sizes x three states). This is file/wiring
  verification, not execution of the animation or importer.
- Searched source for retired folder paths, sheet stems, name-based loads and fallbacks;
  inspected the consumers listed below. Files outside a Resources directory are not loaded
  merely because a retired PNG remains on disk. No name-based runtime loader for them was found.
- Checked the LFS inventory before reading any PNG: no pointer-only entries were present.
  No editor was launched; native content tests and gameplay rendering were not run.

## Remaining references and classification

Class (a) = a same-species reference-only swap; (b) = a missing replacement sprite;
(c) = code work. `Retired` and `Current` below are exclusions, not unclassified work.

| Class | File and line | Reference / player-visible condition | Disposition |
|---|---|---|---|
| (c), guard only | `Assets/Tests/EditMode/FishArtEverywhereTests.cs:283`, `:766` | `KeptShellfishIcons = { "SoftShellClam" }` removes that old GUID from the retirement test. No player-facing load; it would let a future regression escape this particular test. | Remove the stale exception while preserving the all-assets scan and the current species-icon checks. |
| (c), comments only | `Assets/_Project/Code/Art/CatchItemLibrary.cs:17` | The class summary still names `CatchItem_<kind>` as the storage source. No executable load here. | Describe `CatchItem2_<kind>` and current held art; do not change runtime selection. |
| Retired, editor import registration | `Assets/_Project/Art/Editor/CatchStorageSheetSlicer.cs:50`, `:51`, `:54`, `:55` | Old lobster/crab/mussel/clam strip layouts remain importable. The player does not see them through this registration. | Preserve historical import identity; current builder refuses these strips. |
| Retired, editor import registration | `Assets/_Project/Art/Editor/SpriteSheetSlicer.cs:137` through `:148` | Old shore-find sheet grid declarations, including shellfish. No shipped GUID consumer. | Do not replace sliced sheets with single iso cells or change their pivots. |
| Retired, editor-only writer | `Assets/_Project/Code/Tools/Editor/RigBaking/CatchStorageBaker.cs:138` | Can write pass-1 `CatchItem_<kind>` strips on an explicit old bake command; this is not a runtime fallback. | No bake or source edit in this task; current builder rejects the results as catch-item sources. |
| Current refusal, keep | `Assets/_Project/Code/App/Editor/CatchItemLibraryBuilder.cs:414` | `RefusePass1` discovers old stems only to report they were not used; pass-2 loading occurs at `:204`. | Keep this safeguard. Finding an old name in this method is not finding old art on screen. |
| Retired, comments only | `Assets/_Project/Code/App/Editor/PersistentCoreBuilder.cs:99`; `Assets/_Project/Code/App/Editor/NineMileCreekBuilder.cs:87` | `FishOnSilhouette` is explicitly described as no longer wired. | No UI swap required. |

## Retired file inventory

Paths in this table are relative to `Assets/_Project/Art/`. Each source's `.meta:2`
declares its GUID. **No consumer file/line exists in the scanned set for any row**; these
are files retained on disk, not visible uses. Keep the files and metadata untouched.

| Retired source PNG | Shipped GUID references | Player exposure / class |
|---|---:|---|
| `Fishing/Lobster.png` | 0 | None found; retired, no swap. |
| `Fishing/RockCrab.png` | 0 | None found; retired, no swap. |
| `Fishing/Storage/CatchItem_clam.png` | 0 | None found; retired, no swap. |
| `Fishing/Storage/CatchItem_crab.png` | 0 | None found; retired, no swap. |
| `Fishing/Storage/CatchItem_lobster.png` | 0 | None found; retired, no swap. |
| `Fishing/Storage/CatchItem_mussel.png` | 0 | None found; retired, no swap. |
| `Sprites/Fish/BlueMussel.png` | 0 | None found; retired, no swap. |
| `Sprites/Fish/Cod.png` | 0 | None found; retired, no swap. |
| `Sprites/Fish/Haddock.png` | 0 | None found; retired, no swap. |
| `Sprites/Fish/Lobster.png` | 0 | None found; retired, no swap. |
| `Sprites/Fish/Mackerel.png` | 0 | None found; retired, no swap. |
| `Sprites/Fish/Pollock.png` | 0 | None found; retired, no swap. |
| `Sprites/Fish/RockCrab.png` | 0 | None found; retired, no swap. |
| `Sprites/Fish/SoftShellClam.png` | 0 | None found; retired, no swap. |
| `Sprites/Shore/Finds/Bone.png` | 0 | None found; retired, no swap. |
| `Sprites/Shore/Finds/CrabMoult.png` | 0 | None found; retired, no swap. |
| `Sprites/Shore/Finds/Driftwood.png` | 0 | None found; retired, no swap. |
| `Sprites/Shore/Finds/GullFeather.png` | 0 | None found; retired, no swap. |
| `Sprites/Shore/Finds/Mussel.png` | 0 | None found; retired, no swap. |
| `Sprites/Shore/Finds/Oyster.png` | 0 | None found; retired, no swap. |
| `Sprites/Shore/Finds/Periwinkle.png` | 0 | None found; retired, no swap. |
| `Sprites/Shore/Finds/SandDollar.png` | 0 | None found; retired, no swap. |
| `Sprites/Shore/Finds/Scallop.png` | 0 | None found; retired, no swap. |
| `Sprites/Shore/Finds/SeaGlass.png` | 0 | None found; retired, no swap. |
| `Sprites/Shore/Finds/SoftShellClam.png` | 0 | None found; retired, no swap. |
| `Sprites/Shore/Finds/Starfish.png` | 0 | None found; retired, no swap. |
| `UI/FishOnSilhouette.png` | 0 | None found; retired, no swap. |

## Every shipped species icon

Each Def location below is its `Sprite` line. Icon-library locations are the matching `Icon`
line in `Assets/_Project/Resources/IconLibrary.asset`. PNG names are relative to
`Assets/_Project/Art/Fishing/Icons/`. All rows are **Current**: no class (a) or (b) action.

| Species | Def file and line | Icon-library line | Current PNG |
|---|---|---:|---|
| `fish.lobster` | `Assets/_Project/Data/Fish/AmericanLobster.asset:20` | 31 | `ShellIcon_lobster.png` |
| `fish.american_oyster` | `Assets/_Project/Data/Fish/AmericanOyster.asset:20` | 121 | `ShellIcon_american_oyster.png` |
| `fish.atlantic_cod` | `Assets/_Project/Data/Fish/AtlanticCod.asset:20` | 17 | `FishIcon_cod.png` |
| `fish.atlantic_herring` | `Assets/_Project/Data/Fish/AtlanticHerring.asset:47` | 29 | `FishIcon_herring.png` |
| `fish.atlantic_surf_clam` | `Assets/_Project/Data/Fish/AtlanticSurfClam.asset:38` | 47 | `ShellIcon_atlantic_surf_clam.png` |
| `fish.atlantic_tomcod` | `Assets/_Project/Data/Fish/AtlanticTomcod.asset:41` | 41 | `FishIcon_tomcod.png` |
| `fish.blue_mussel` | `Assets/_Project/Data/Fish/BlueMussel.asset:20` | 123 | `ShellIcon_blue_mussel.png` |
| `fish.cunner` | `Assets/_Project/Data/Fish/Cunner.asset:42` | 43 | `FishIcon_cunner.png` |
| `fish.green_crab` | `Assets/_Project/Data/Fish/GreenCrab.asset:41` | 35 | `ShellIcon_green_crab.png` |
| `fish.haddock` | `Assets/_Project/Data/Fish/Haddock.asset:20` | 19 | `FishIcon_haddock.png` |
| `fish.mackerel` | `Assets/_Project/Data/Fish/Mackerel.asset:20` | 21 | `FishIcon_mackerel.png` |
| `fish.northern_quahog` | `Assets/_Project/Data/Fish/NorthernQuahog.asset:38` | 45 | `ShellIcon_northern_quahog.png` |
| `fish.pollock` | `Assets/_Project/Data/Fish/Pollock.asset:20` | 23 | `FishIcon_pollock.png` |
| `fish.rainbow_smelt` | `Assets/_Project/Data/Fish/RainbowSmelt.asset:44` | 39 | `FishIcon_smelt.png` |
| `fish.rock_crab` | `Assets/_Project/Data/Fish/RockCrab.asset:22` | 33 | `ShellIcon_rock_crab.png` |
| `fish.sea_scallop` | `Assets/_Project/Data/Fish/SeaScallop.asset:20` | 125 | `ShellIcon_sea_scallop.png` |
| `fish.soft_shell_clam` | `Assets/_Project/Data/Fish/SoftShellClam.asset:24` | 37 | `ShellIcon_soft_shell_clam.png` |
| `fish.striped_bass` | `Assets/_Project/Data/Fish/StripedBass.asset:49` | 25 | `FishIcon_bass.png` |
| `fish.winter_flounder` | `Assets/_Project/Data/Fish/WinterFlounder.asset:45` | 27 | `FishIcon_flounder.png` |

## Player-facing consumers and fallback trace

| When the player sees it | File and line | Current selection; audit result |
|---|---|---|
| Catch card after landing | `Assets/_Project/Code/UI/HudController.cs:739` | `IconRegistry.Get(item.SpeciesId)`; the 19 current entries above. The icon overlays the world (`:1019`), rather than an old fish silhouette. |
| Registry setup / other icon consumers | `Assets/_Project/Code/Core/Services/IconRegistrar.cs:24` | Loads `Resources/IconLibrary`. Missing icons are not substituted with a legacy PNG by this registrar. |
| Hooked fish, fight and landing pose | `Assets/_Project/Code/Player/RodFightPresenter.cs:441`, `:471` | Resolves a size ladder through `FishArtLibrary`; shared asset has ten species at `Resources/FishArtLibrary.asset:16` through `:8755`. No retired GUID is present even in inline scene copies. |
| Old scene without an inline ladder | `Assets/_Project/Code/Player/FishArtLibrary.cs:85` | An absent/empty ladder uses the shared current table. This fallback already repairs the earlier missing-art route. |
| Catch in the hands | `Assets/_Project/Code/Player/CarryHands.cs:333`, `:1083`; `Assets/_Project/Code/Player/CarriableCatch.cs:143` | Shared catch library supplies current held art. Missing held art falls back to the current species icon, not a legacy-name load. |
| Fish on a cooking station | `Assets/_Project/Code/Player/CookingStationFoodPresenter.cs:128`, `:133` | Uses current catch-library deck art at the catch size; processed fillets are constructed separately. Neither path reads a retired fish PNG. |
| Tote / open container fill | `Assets/_Project/Code/Art/CatchItemLibrary.cs:204`, `:399`; `Assets/_Project/Code/App/Editor/CatchItemLibraryBuilder.cs:147`, `:204` | Current `Fish_*_deck` and `CatchItem2_*` lays. Unknown species use the current cod kind; this is a species-fidelity fallback, not pre-iso art. |
| Fish visible in the water | `Assets/_Project/Resources/FishSwimSpriteLibrary.asset:2257` through `:2298` | Ten species map to their current swim kinds; cod fallback is also current iso art. The live presenter is owned by an open change and was not edited. |
| Crustaceans from a pot on deck | `Assets/_Project/Data/Traps/PotDeckWork.asset:41`, `:59`, `:77` | `LobsterDeck`, `RockCrabDeck`, `GreenCrabDeck` respectively, not the retired flat `Lobster` / `RockCrab` icons. |

## Boundaries and unresolved fidelity

The quahog, surf clam and green crab still use shared `clam` / `crab` **current** catch kinds
in sprite/container paths (`Data/Art/CatchItemLibrary.asset:1106`, `:1122`, `:1138`). Their
species-specific held meshes are already wired (`:1109`, `:1125`, `:1141`) and win in
`Code/Art/CatchItemLibrary.cs:289`. Shared species representation is a separate fidelity
question; it is not evidence of a pre-iso sprite or authorization to substitute another
species' sprite. It creates no class (b) row for this retirement task.

`docs/art/fish-sprites-owed.md` retains historical stand-in descriptions. Its older claims
about absent held meshes and old icons must not be used as current reference evidence.
The committed assets and current guards take precedence for this audit.

## Picture evidence and remaining weakness

`11-old-fish-contact-sheet.png` is retained in local evidence, not committed. It shows cod,
mackerel, soft-shell clam and lobster: the unreferenced retired file, pinned main before,
unchanged main after, and two accepted neighbours, at native 1x and nearest-neighbour 4x.
The backdrop is an unchanged water crop from the committed visible-fish plate. This is a
static composition of source pixels for inspection; it is not a new gameplay capture or
proof of a live landing pose. Before and after are intentionally identical.

In that composition the mussel's dark edge is weak against dark water at 1x; lobster leg
and antenna details are fine at that scale. Live hand/landing readability and camera scale
were not rendered here and remain unjudged. No palette, art, import setting or baseline was
changed. Picture acceptance remains with the coordinator.
