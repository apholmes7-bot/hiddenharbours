# Region scene size and serialization churn

Small scene edits can produce large diffs because a fence run is stored as many
GameObjects, transforms, renderers, sort components and colliders, and replacing
their identities repeats most of that text. The two-yard change in PR #1458 is a
concrete example: **16,258 added/deleted scene lines, of which 12,251 repeat
serialization and 4,007 remain content changes or unmatched diff alignment**.
Only the two yard subtrees and their parent's child list change at the object
level. An ordinary line diff makes unchanged objects elsewhere look involved.

The best first investment is to preserve existing object identities in the
current patch writers, including when adopting old builder output. Deterministic
IDs already exist; adding another allocator would miss the problem. Then pilot
one prefab per yard or building. A region-wide split into additive child scenes
has more loading and reference risk and should follow a measured prefab pilot.
Do not round scene geometry merely to shorten the diff.

This is a read-only source/history audit at **`598013532f96006a90c324ae86d79c9e24a9e202`**,
dated 2026-11-22. It changes this document only. No scene, prefab, builder,
setting, ground, save format or visible asset changes. No editor, import,
EditMode/PlayMode suite, render, plate comparison or merge-driver execution ran.
The proposals below therefore have no claim of demonstrated visual equivalence.

## Scope and existing coverage

Seven committed scenes are named by the top-level RegionDef assets:
`Greybox` (Coddle Cove), `StPeters`, `NineMileCreek`, `RicePoint`, `Cumberland`,
`EastWater`, and `SouthWater`. `WestWater.asset` also names a scene, but
`WestWater.unity` is absent from tracked files at this revision; its size is
**not measured, not zero**. `Greywick.unity` is included as a legacy scene still
enabled in `ProjectSettings/EditorBuildSettings.asset`, although no current
top-level RegionDef names it. `SampleScene.unity` is not a region and is excluded.

Before considering new checks, the existing coverage was searched:

- [`SceneWeightGuardTests.cs`](../../Assets/Tests/EditMode/SceneWeightGuardTests.cs)
  already counts YAML documents and GameObjects and enforces size ceilings. Its
  general ceilings are 32 MiB, 50,000 objects and 12,000 GameObjects; its more
  specific St Peters limit is 4,806,598 + 65,536 bytes. The measured scene has
  41,840 bytes left under that specific limit. A size ceiling does not limit churn.
- [`StPetersVillageRefreshTests.cs`](../../Assets/Tests/EditMode/StPetersVillageRefreshTests.cs),
  [`StPetersLayerRefreshPatchTests.cs`](../../Assets/Tests/EditMode/StPetersLayerRefreshPatchTests.cs),
  and [`NineMileCreekLayerRefreshTests.cs`](../../Assets/Tests/EditMode/NineMileCreekLayerRefreshTests.cs)
  already exercise patch boundaries and preservation. Extend the relevant
  existing class for future identity/no-op coverage; do not duplicate the readers.
- [ADR 0004](../adr/0004-perspective-and-scene-strategy.md) already chooses
  additive regions, prefab-first content and Force Text. [ADR 0019](../adr/0019-hand-authored-scenes-and-refresh.md)
  makes the committed scene authoritative after adoption. A builder's ability to
  recreate an object is not permission to overwrite its authored placement.

## Scene sizes and group shares

Bytes are the exact UTF-8 Git blob bytes, including headers and newlines; MiB is
bytes / 1,048,576. One `--- !u!` document counts as one serialized object, including
settings and stripped prefab records. GameObjects count class 1 documents;
prefab instances count class 1001 documents. These are serialized counts, not
runtime counts: nested prefab assets and generated runtime meshes are not expanded.

| Scene | Bytes | MiB | Lines | Serialized objects | GameObjects | Prefab instances |
|---|---:|---:|---:|---:|---:|---:|
| `Cumberland` | 710,796 | 0.678 | 16,013 | 269 | 54 | 0 |
| `EastWater` | 21,792 | 0.021 | 796 | 36 | 10 | 0 |
| `Greybox` | 41,506 | 0.040 | 1,538 | 73 | 22 | 0 |
| `Greywick` | 134,722 | 0.128 | 4,904 | 181 | 46 | 0 |
| `NineMileCreek` | 12,932,076 | 12.333 | 439,092 | 15,533 | 3,148 | 24 |
| `RicePoint` | 657,167 | 0.627 | 14,316 | 213 | 52 | 0 |
| `SouthWater` | 28,612 | 0.027 | 1,049 | 46 | 13 | 0 |
| `StPeters` | 4,830,294 | 4.607 | 162,161 | 5,782 | 1,166 | 20 |

The following is an exclusive **group footprint**, not a component-type census.
Buildings include their rooms/fixtures; props include wharves, key scenes, shore
rocks and clam holes; NPC/stations includes shop counters and routine roots.
Lights embedded in a building or wharf remain charged to that group, so a zero
in the Lights column does not mean the scene has no lights. Other includes scene
settings, roads, terrain, cameras, player/core data and travel wiring.

| Scene | Yards/fences | Plants | Cliff walls | Buildings | Props | NPC/stations | Lights | Water | Other |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `Cumberland` | 0.00% | 0.00% | 0.00% | 0.00% | 0.00% | 9.27% | 0.00% | 0.73% | 89.99% |
| `EastWater` | 0.00% | 0.00% | 0.00% | 0.00% | 0.00% | 0.00% | 0.00% | 19.87% | 80.13% |
| `Greybox` | 0.00% | 0.00% | 0.00% | 0.00% | 37.83% | 3.53% | 0.00% | 10.11% | 48.53% |
| `Greywick` | 0.00% | 21.28% | 0.00% | 23.91% | 22.06% | 0.00% | 0.00% | 3.11% | 29.65% |
| `NineMileCreek` | 3.40% | 71.66% | 1.42% | 5.29% | 9.71% | 0.31% | 0.00% | 0.04% | 8.16% |
| `RicePoint` | 0.00% | 0.00% | 0.00% | 0.00% | 0.97% | 0.00% | 0.00% | 0.81% | 98.22% |
| `SouthWater` | 0.00% | 0.00% | 0.00% | 0.00% | 0.00% | 0.00% | 0.00% | 14.95% | 85.05% |
| `StPeters` | 13.58% | 35.19% | 15.43% | 8.03% | 19.07% | 1.19% | 1.12% | 0.11% | 6.29% |

Most buckets follow complete named roots, listed in the appendix. Exceptions:
`Shoreline/ShorePlants` in Nine Mile Creek and `Decor/Trees` in Greywick count as
plants; the rest of Shoreline counts as props. The `LandRegion/Visuals/Sea` child
counts as water; `LandRegion/Logic/Wrecks` counts as props. In Greybox's single
logic root, Sea is water, Pilings/Wharf/ShoreEdge are props and Shipwright is a
station. Whole mixed roots retain their dominant category; this is not a count
of every lamp or plant nested inside a building. Percentages may round by 0.01.

The main pressure points are concrete:

- St Peters: `IslandWoods` is 1,432,753 bytes (29.66%), `CliffWalls` 745,297
  (15.43%), and `Yards` 655,845 (13.58%). Those three roots alone are 58.67%.
  Yards contains 848 serialized objects. Only 20 prefab instances exist in the
  whole scene; none are under Yards or IslandVillage.
- Nine Mile Creek: `Shoreline` is 5,213,963 bytes (40.32%), almost all of it
  `Shoreline/ShorePlants` (5,147,600 bytes). The plant groups together are 71.66%.
  `NineMileCreekRoads` is another 977,927 bytes in only 11 objects: object count
  alone misses large serialized arrays. There are 24 prefab instances overall.
- Rice Point and Cumberland are dominated by `DevCoreEnvelope`: 595,273 and
  595,356 bytes respectively (90.58% and 83.76%). Their current region content
  has a different footprint from the two large settlement scenes.

## Generated origin versus authored placement

The files below are in [`Assets/_Project/Code/App/Editor`](../../Assets/_Project/Code/App/Editor/)
unless stated otherwise. Menu paths are exact suffixes **below the project's
top-level menu**, whose common prefix is omitted. A helper with no menu is
identified by its caller. These are source entry points, not instructions to run
them on an adopted scene.

| Groups | Writer/source and menu suffix | What is actually established |
|---|---|---|
| St Peters Yards | `YardDressing.cs` + `StPetersYards.cs`, called by `StPetersBuilder.cs`: `Build St Peters Scene`; `StPetersLayerRefresh.VillageYards.cs` through `StPetersLayerRefresh.VillagePass.cs`: `World/St Peters Layer Refresh/Apply Village Patch` | Generated geometry from authored yard plans. Main's patch covers five active village yards; the two homestead yards enter that patch path in #1458. |
| Nine Mile Creek Yards | `YardDressing.cs` + `NineMileCreekYards.cs`, called by `NineMileCreekBuilder.cs`: `Build Nine Mile Creek Scene` | Generated origin; no dedicated yard-only menu found. Banked yard placements are protected from broad recreation. |
| St Peters IslandWoods, IslandUnderstorey, ShorePlants | `StPetersWoodsPlanter.cs`, using `StPetersShorePlants.cs`, through `Build St Peters Scene` | Generated scatter origin, subsequently banked/edited. `StPetersPlantFieldPatch.cs` retires the old meadow roots; current `StPetersPlantField` stores field input, not every runtime plant. No independent menu in that patch helper. |
| Nine Mile Creek CreekFields/Hedgerows/Trees/Marsh/Understorey and Shoreline | `NineMileCreekFieldPlanter.cs` and `NineMileCreekShorePainter.cs`, through `Build Nine Mile Creek Scene` | Generated origin. Shore painter destroys/recreates its root. Later authored retirement and replanting are not reproduced simply by rerunning the original full builder. |
| Nine Mile Creek CreekPlanting | No committed production writer or menu found; #1255 changes the scene and two test files | Authored/banked placement. `RegionTreePlacementContentTests.cs` guards it. The snapshot does not establish whether an uncommitted script or manual editor placement originally produced each tree. Treat it as hand-authored for preservation. |
| Both CliffWalls roots | `StPetersCliffWalls.cs`, `NineMileCreekCliffWalls.cs` through their full scene builders; `StPetersLayerRefresh.CliffWalls.cs`: `World/St Peters Layer Refresh/Apply the Cliff Walls Patch to StPeters.unity`; `NineMileCreekLayerRefresh.cs`: `Nine Mile Creek Layers/Apply named patches` | Generated from wall Defs; narrow patch paths exist. The full Build helpers destroy/recreate the named root, unlike preserving unrelated documents. |
| St Peters IslandVillage, IslandShops, GinnyPlot | `StPetersVillage.cs`, `StPetersGinnyPlot.cs`, `StPetersBuilder.cs`: `Build St Peters Scene`; newer homes via `StPetersLayerRefresh.VillageHomes.cs` and `Apply Village Patch` | Authored sites assembled by helpers, with banked interiors and later fitouts. The full build is not a round-trip editor for the committed room contents. |
| St Peters commons, lights, key-scene props | `StPetersVillageDressing.cs`, `StPetersVillageLights.cs` through full build; `StPetersLayerRefresh.Village.cs` through `Apply Village Patch`; `StPetersLayerRefresh.KeyScenePieces.cs`: `World/St Peters Layer Refresh/Apply the Key Scenes Patch to StPeters.unity` | Generated from authored Def rows, with stable piece identities available to the patch writers. Lights under other roots stay owned by those roots. |
| Nine Mile Creek wharves, boat yard, fuel, dressing, houses | `NineMileCreekDressing.cs`, `NineMileCreekStation.cs`, `NineMileCreekWharfLandSide*.cs`, `NineMileCreekWharfYard.cs`, and `NineMileCreekGroundSteps.cs`; full build plus `Nine Mile Creek Layers/Apply named patches` | Authored layouts assembled by named steps. Source build and later patch stages must not be treated as interchangeable. |
| Nine Mile Creek NmcBuildings; keeper/counter stations | `NmcBuildingShells.cs`: `World/Nine Mile Creek Buildings/Build the Shell Prefabs` and `World/Nine Mile Creek Buildings/Apply the Patches`; `NmcWholesalerKeepers.cs`: `World/Nine Mile Creek Buildings/Place the Wholesaler Counters and Keepers` | Four building prefab instances already demonstrate the separation; keeper/counter objects remain scene wiring. |
| St Peters inhabitants/stations and Cumberland golfers | `StPetersRoutines.cs` / `StPetersInhabitants.cs` through full build; `StPetersLayerRefresh.VillagePeople.cs` through `Apply Village Patch`; `CumberlandRoutines.cs`: `Regions/Cumberland/Build Golf Stations and Lanes`, `Regions/Cumberland/Place Approved Golfers`, also called through `CumberlandBuilder.cs`: `Regions/Cumberland/REFRESH Logic` | Routine/station data is authored; scene objects are assembled/wired by helpers. Golf asset/routine-building menus are separate from placing the region objects. |
| Sea and region wiring in St Peters / Nine Mile Creek | Their full scene builders: `Build St Peters Scene`, `Build Nine Mile Creek Scene` | Generated scaffold followed by banked patches. Do not call full CREATE to make a small water/reference edit. |
| Rice Point and Cumberland LandRegion / DevCoreEnvelope | `RicePointBuilder.cs`, `CumberlandBuilder.cs` via `LandSceneTemplate.cs`: `Regions/Rice Point/CREATE Scene`, `Regions/Rice Point/REFRESH Logic`, and matching `Regions/Cumberland/...` | CREATE assembles the scaffold. `LandSceneTemplate.Refresh` changes references in place and explicitly avoids destroy/recreate and transform resets. A useful existing model for stable identity. |
| EastWater / SouthWater | `EastWaterBuilder.cs`, `SouthWaterBuilder.cs` via `WaterSceneTemplate.cs`: `Build East Water Scene`, `Build South Water Scene` | Generated water/terrain/camera/anchor/passages scaffold. Tiny serialized footprints; low return from splitting them further. |
| Greybox tagged logic root | `GreyboxBuilder.cs`: `Build Greybox Scene`, `Refresh Cove Logic` | Refresh destroys and rebuilds the tagged root. That is permitted ownership scope but still an identity-churn risk. The current file contains no separate visual root. |
| Greywick legacy roots | Banked scene, last changed in #432 (`422ad0d57e`); no `GreywickBuilder.cs` in this revision | Historical builder references in older ADRs are not a callable current menu. Preserve as authored legacy content; do not invent a regeneration path. |

“Generated” and “hand-placed” are not mutually exclusive lifetime labels. An
initial scatter or scaffold can have later manual edits, and the serialized
object does not record which editor gesture created it. The accepted policy is
therefore stronger than a provenance guess: preserve adopted scene objects unless
the specific named patch owns them. `CreekPlanting` and legacy Greywick have no
current regeneration entry point found; other groups have the traced generation
origins above, not proof that every present transform still equals that origin.

## History window and ten largest scene diffs

This mainline uses squash integrations. A literal first-parent `--merges -60`
query finds just one old two-parent merge (`cecc596367`, 2026-06-20), so it cannot
represent recent PR integration. The **last 60 first-parent mainline commits**
span 2026-11-20 through 2026-11-22 and contain only one region-scene diff:
#1350, `036006d9d7`, StPeters, **+1 / -0** (a freezer fixture reference).
There are not ten scene diffs in that strict window.

To provide ten useful comparisons without silently changing the denominator,
the following ranking uses the **last 60 scene-changing first-parent
integrations**, obtained with the scene path filter *before* the 60-result
limit. They span `e8de9cd075` (2026-09-30) through `036006d9d7` (2026-11-20):
60 commits, 69 commit/file rows. Rank is additions + deletions per scene file,
not net growth or the total PR size. #1458 is separate, since it was open at the
audited base.

| PR / commit | Scene | What the integration did | Added | Deleted | Total | Repeated | Content/unresolved |
|---|---|---|---:|---:|---:|---:|---:|
| #991 / `eefe1268b9` | NineMileCreek | Road/bridge/lot migration to the new ground and road-floor manifest | 125,975 | 289,020 | 414,995 | 249,435 | 165,560 |
| #921 / `883f27b5eb` | StPeters | Adopt the terrain-plan ground; retire/reposition old ground-era content | 49,716 | 98,265 | 147,981 | 114,942 | 33,039 |
| #959 / `b4b0c3db90` | NineMileCreek | Activate creek ground steps 04-09 and update serialized ground-dependent content | 76,616 | 38,192 | 114,808 | 27,978 | 86,830 |
| #1023 / `3a0b19cf1e` | NineMileCreek | Replace the boat-yard layout and trades with the delivered building kit | 41,396 | 29,842 | 71,238 | 53,829 | 17,409 |
| #1194 / `770edb589c` | NineMileCreek | Place the fuel-stop key scene; integration explicitly recorded unfinished verification | 27,082 | 22,686 | 49,768 | 25,876 | 23,892 |
| #1255 / `32cb3416eb` | NineMileCreek | Replant dry-ground wild stands and yard trees | 38,104 | 3,350 | 41,454 | 1,369 | 40,085 |
| #1246 / `89eae81626` | NineMileCreek | Retire trees/shrubs standing on beach or water | 4,682 | 32,505 | 37,187 | 13,750 | 23,437 |
| #983 / `4205c27cd8` | StPeters | Apply village buildings, yards, commons and lights | 24,723 | 10,518 | 35,241 | 12,818 | 22,423 |
| #1002 / `73cf9fe20c` | StPeters | Replace landing deck tiles with the baked deck representation | 2,917 | 19,649 | 22,566 | 6,262 | 16,304 |
| #927 / `321cd0fcc9` | StPeters | Place cliff walls by their Def identities on the adopted ground | 17,575 | 4,905 | 22,480 | 733 | 21,747 |

The last two columns add to the total for every row. **Repeated** is a
serialization comparison, not a visual verdict: byte-identical matched objects,
ID-equivalent objects, and identical lines re-emitted inside changed objects.
**Content/unresolved** retains real additions/removals/value changes and anything
the matcher could not establish as repeated. It is an upper bound on meaningful
change, not a claim that every residual line changes the game. No exact
“player-visible lines” count can be derived from YAML alone.

| PR | Retained-ID equivalent objects / raw lines | New-ID equivalent objects / raw lines | Repeated lines within other changed objects | Tiny-float candidate lines (retained in residual) |
|---|---:|---:|---:|---:|
| #991 | 4,739 / 237,909 | 0 / 0 | 11,526 | 0 |
| #921 | 4,244 / 112,740 | 0 / 0 | 2,202 | 0 |
| #959 | 9 / 358 | 0 / 0 | 27,620 | 0 |
| #1023 | 2,958 / 47,431 | 0 / 0 | 6,398 | 0 |
| #1194 | 708 / 17,436 | 0 / 0 | 8,440 | 0 |
| #1255 | 70 / 1,369 | 0 / 0 | 0 | 0 |
| #1246 | 587 / 13,750 | 0 / 0 | 0 | 0 |
| #983 | 202 / 6,258 | 74 / 2,382 | 4,178 | 6 |
| #1002 | 191 / 6,256 | 0 / 0 | 6 | 0 |
| #927 | 24 / 733 | 0 / 0 | 0 | 0 |

Equivalent objects in both columns mean complete documents equal after remapping
matched local references, not all objects whose IDs changed. A retained ID may
still have remapped references. Many added/removed
IDs correspond to genuinely new or retired content. Identical child-list
entries were re-emitted in #991 (4,190 added/deleted lines), #921 (1,356),
#1023 (408), #983 (84), #1246 (18), and #1194 (14). These are included in
Repeated, not added to it. None of those ten comparisons had a changed
`m_Children` list that was solely a permutation of the same entries. Changed
membership and Git's alignment of repetitive lists can still re-emit entries.

Only #983 produced tiny transform-delta candidates in the measured comparisons:
three position lines (six added/deleted lines) with maximum coordinate changes
of 0.0000004, 0.0000007 and 0.000001, on school furniture. They remain in
Content/unresolved. This audit does not call numerically different geometry
equivalent. The observed large churn is primarily object repetition and content
replacement, not demonstrated floating-point noise.

### The two-fence example

PR #1458's audited head is `3d513d17f9da0294226dffdd8a87159621578ac9`, whose
parent is `f1b4ab2f59f417eda06f2239f7dee25d4c52ad23`. The parent and this audit's
main revision contain the same StPeters blob (`6a812a33e24253b75f92ae100d55cc2409ab5642`).
Its scene diff is **+8,659 / -7,599**,
a net gain of only 1,060 lines. It is not a 16,258-line net enlargement.

- Comparing complete documents by fileID, all retained documents outside
  `Yards/RedSaltbox`, `Yards/WhiteFarmhouse` and the Yards transform are byte-equal.
  There are 248 removed IDs and 285 added IDs; among existing IDs, only the
  parent's transform changes. The two replaced subtree unions contain 221 and
  312 documents respectively.
- 104 complete documents match after local-ID remapping, contributing 3,592
  raw changed lines. Another 49 unchanged same-ID documents account for 1,475
  raw changed lines through line-diff alignment. A further 7,184 repeated lines
  are inside changed documents: **12,251 repeated lines, 75.36% of the diff**.
  The remaining **4,007** include the actual fence geometry, collider and
  membership changes, plus unresolved alignment. No tiny-transform candidates
  were found under the stated threshold.
- The Yards parent has a pure child-order permutation after ID remapping. It
  should be reviewed as an ordering change; equal children do not prove order
  is irrelevant. The common child/reference entries total 120 raw lines and
  overlap the repetition counts above.
- `StPetersLayerRefresh.VillageYards.cs` removes the owned subtrees from the ID
  reservation set, then allocates keys of the form
  `VillageYards/<yard>/<piece-key>_<index>/<component-type>`. This adopts old
  editor-created IDs into deterministic IDs once, and piece indexes can shift
  on later insertions. It writes complete component templates for desired
  pieces. The parent child list is rebuilt by removing old entries and appending
  the new yard transforms. These mechanisms explain the broad serialization
  without implying unrelated scene objects were actually edited.

## Proposals, ranked by value for effort

1. **Preserve existing identities and no-op bytes in the current patch path.**
   High value, small-to-medium effort. Extend `StPetersLayerRefresh.VillageYards.cs`
   and the existing patch tests to adopt existing component IDs by an unambiguous
   authored piece identity. Allocate only for new pieces. Replace positional
   piece indexes with stable authored piece/run identities where available;
   preserve old IDs through a one-time mapping when bringing legacy yards under
   patch ownership. `StPetersLayerRefresh.IdAllocator` already hashes keys and
   avoids reserved IDs, so reuse it. Require an unchanged plan to emit zero
   patches and a single-piece edit to preserve all other document bytes and
   references. **Risks:** ambiguous duplicate names, collisions whose salt depends
   on reservations, reusing an ID for a different object, and losing authored
   component overrides through full-template replacement. Fail on ambiguity;
   do not guess by nearest position. This is not a request to rewrite IDs in
   all existing scenes.

2. **Preserve deterministic emission and existing order; add a churn review.**
   High value, small effort. Touch `StPetersLayerRefresh.SceneYaml` / `LayerPatch`
   callers and their existing tests, plus an optional PR diff-summary check.
   Preserve unaffected document order and unchanged child positions; sort only
   newly authored unordered input by stable keys. `SceneYaml.Render` already
   emits its stored document order, and yard rows already use ordinal ordering.
   Add one insertion/deletion case, not just a second identical run. Report
   changed roots, surviving IDs, removed/added IDs and repeated serialization
   beside ordinary numstat. Extend `SceneWeightGuardTests` only if adding a size
   assertion, not to conflate size with churn. **Risks:** sibling order can affect
   callbacks, lookup behavior or rendering ties; sorting every child list or
   whole scene would itself create churn. Thresholds must allow legitimate large
   content migrations and should initially report rather than block them.

3. **Pilot one prefab per authored yard/building, with nested repeated parts.**
   High value, medium effort and a one-time migration diff. Touch selected
   scene roots, new prefab assets/metas, the corresponding placement/patch code
   and its existing tests. Yards is a bounded first candidate: 655,845 bytes
   and 848 documents in St Peters, with zero prefab instances today. Keep the
   outer instance's placement in the scene; author fence runs or building
   contents in that specific prefab so unrelated scene PRs stop sharing the
   same text. Nine Mile Creek's four NmcBuildings instances are a local example.
   Do not put every yard into one giant prefab. **Risks:** lost overrides,
   changed local/world transforms, broken scene-to-prefab references, nested
   instance identity changes, sorting/collider differences, and a new shared
   prefab becoming the contention point. Cross-boundary references need explicit
   wiring. Validate all references and byte-equal before/after plates before any
   visual migration is accepted; the measured bytes are exposure, not a promise
   that all of them disappear from the scene.

4. **Wire and exercise the intended merge driver on each integration machine.**
   Useful conflict reduction, small setup effort; it does not reduce diff size.
   Touch machine Git configuration and a setup verification/check, with no
   scene/settings migration. Use the pinned installed editor's UnityYAMLMerge
   executable and a disposable three-way conflict fixture outside tracked assets.
   Verify invocation, exit status and resulting references. **Risks:** a missing
   executable, wrong argument order or an unchecked conflict resolution can make
   configuration look valid while losing edits. Do not infer remote wiring from
   local attributes. Keep human review for edits to the same object.

5. **Use additive child scenes only for independently owned large layers.**
   Potentially high conflict isolation, high effort; after the prefab pilot.
   Candidate groups are Nine Mile Creek's large plant/shore groups, not its
   already-small water object. Touch scene assets, region composition data,
   `World/RegionSceneLoader.cs`, `App/RegionTravelCoordinator.cs`, registration,
   unloading and travel tests. ADR 0004 already loads each region additively,
   but current `RegionDef.SceneName` identifies one scene and the loader activates
   that scene; this is **not** already a region-with-many-subscenes contract.
   **Risks:** cross-scene object references, duplicate anchors/services, active
   scene changes triggering arrivals twice, wrong OnEnable order, preloaded
   groups remaining visible or collidable, and incomplete unload. Keep one
   authoritative region identity, deterministic load order, and explicit
   composition lifetime. No save-format change is assumed or authorized here.

6. **Canonicalize float text; defer numeric rounding.**
   Low measured benefit, potentially high risk. Touch only the relevant writer
   and numeric round-trip tests if evidence warrants it. `StPetersLayerRefresh.F`
   already emits invariant shortest text that parses to the same float, including
   signed zero handling. Reuse it rather than adding a whole-scene formatter.
   **Risks:** rounding transforms or collider points can change sorting,
   collisions, shore contact and rendered pixels; once quantized, lost authored
   precision cannot be recovered. Do not treat six candidate lines in one
   migration as justification to quantize hundreds of thousands of lines.

## Smart-merge wiring actually observed

- [`.gitattributes`](../../.gitattributes) defines
  `[attr]unity-yaml merge=unityyamlmerge eol=lf linguist-language=yaml` and applies
  it to `*.unity` and `*.prefab`. `git check-attr merge eol` reports
  `unityyamlmerge` / `lf` for all eight measured scene files. The macro supplies
  line endings but does not itself register an executable merge driver.
- `ProjectSettings/EditorSettings.asset` has `m_SerializationMode: 2` (Force Text).
- [`project-structure.md`, machine setup](project-structure.md#6-one-time-machine-setup-do-this-once-in-order)
  documents `merge.unityyamlmerge.name` and `.driver` with the base/other/current
  inputs and current output. It explicitly calls this per-machine setup.
- The audited checkout's effective `git config --get-regexp
  '^(merge|mergetool)\.(unityyamlmerge|unity)'` returned no values (exit 1).
  There is **no configured UnityYAMLMerge driver here**. A tracked-source search
  found the documentation and attributes, but no registration in the CI workflow.
  Thus attribute routing is present; executable wiring is not established on
  this machine. Remote integration-host configuration and GitHub's resolution
  behavior were not inspected or tested. No configuration was changed.

## Measurement method and limits

History can be selected without fetching or consulting moving refs:

```sh
GIT_NO_LAZY_FETCH=1 GIT_OPTIONAL_LOCKS=0 git log 598013532f96006a90c324ae86d79c9e24a9e202 --first-parent -60 --format='%H %ad %s' --date=short
GIT_NO_LAZY_FETCH=1 GIT_OPTIONAL_LOCKS=0 git log 598013532f96006a90c324ae86d79c9e24a9e202 --first-parent -60 --numstat --format='%H %ad %s' --date=short -- 'Assets/_Project/Scenes/*.unity'
```

Each ranked commit was compared to its first parent with Git's zero-context
line diff. Added/deleted line counts were reconciled to numstat before
classification. Scene blobs were split at line-start YAML document headers.
GameObject ownership, Transform parents, prefab-instance transform parents and
stripped-record instance references assign documents to roots; the preamble and
scene settings are separately accounted for. Every document and byte is assigned
exactly once. These are static serialized relationships, not loaded Unity objects.

The comparison pairs surviving fileIDs of the same class first. Removed/added
documents can then pair only on a unique hierarchy path, class, script identity
and corresponding prefab source. Duplicate paths are not guessed. It remaps
paired **local** IDs in headers and `{fileID: ...}` references to a common token;
external `(guid, fileID)` references remain intact. No floats, sprites, materials,
geometry, names or scripts are erased to force a match.

For complete normalized-equal pairs, every raw changed line attributed to those
documents is counted as repeated. For other paired documents, only the multiset
intersection of normalized added/deleted lines is counted (twice, for both
sides). Remaining lines stay content/unresolved. This latter count includes
reordered identical fields/list entries, whose ordering can still matter. Whole
object equality and repeated lines in changed objects are reported separately
so that a line-matching heuristic is not confused with a proof of identical objects.
Tiny-delta detection considers only changed local position/rotation/scale/Euler
vector lines with the same text structure and maximum numeric delta at most
0.000001; candidates are never subtracted from content/unresolved.

Raw marker counts, object-class counts and byte totals were independently
cross-checked; all root byte/object totals reconcile. The fence case was also
checked by exact document equality at retained fileIDs, independently of line
alignment. This verifies the reported static measurements, not runtime behavior
or the safety of any proposed asset migration.

## Appendix: every top-level root

Each row includes the root's descendants and attached serialized components.
Settings include the 41-byte YAML preamble. Shares are of that scene's complete
file. Prefab asset contents are not pulled into these totals. This appendix is
the exact hierarchy inventory underlying the functional summary above.


### Cumberland

| Root | Bytes | Share | Objects |
|---|---:|---:|---:|
| `DevCoreEnvelope` | 595,356 | 83.76% | 108 |
| `HeronViewGolf` | 65,895 | 9.27% | 78 |
| `LandRegion` | 44,205 | 6.22% | 74 |
| `[scene settings]` | 3,972 | 0.56% | 6 |
| `DevRegionBootstrap` | 1,368 | 0.19% | 3 |

### EastWater

| Root | Bytes | Share | Objects |
|---|---:|---:|---:|
| `Sea` | 4,329 | 19.87% | 4 |
| `EastWaterRegionAnchor` | 4,196 | 19.25% | 9 |
| `[scene settings]` | 3,623 | 16.63% | 5 |
| `Main Camera` | 2,934 | 13.46% | 5 |
| `PassageToStPeters` | 2,558 | 11.74% | 4 |
| `RegionSceneLoader` | 1,444 | 6.63% | 3 |
| `RegionDisplayNames` | 1,423 | 6.53% | 3 |
| `TidalTerrain` | 1,285 | 5.90% | 3 |

### Greybox

| Root | Bytes | Share | Objects |
|---|---:|---:|---:|
| `--LOGIC-- (generated, do not edit)` | 37,999 | 91.55% | 68 |
| `[scene settings]` | 3,507 | 8.45% | 5 |

### Greywick

| Root | Bytes | Share | Objects |
|---|---:|---:|---:|
| `Decor` | 29,510 | 21.90% | 37 |
| `WharfPost` | 20,267 | 15.04% | 24 |
| `DevCore` | 19,328 | 14.35% | 29 |
| `ShipwrightShed` | 6,119 | 4.54% | 8 |
| `FishBuyerStall` | 5,884 | 4.37% | 8 |
| `ShipwrightDoryYard` | 4,352 | 3.23% | 5 |
| `HarbourmasterOffice` | 4,293 | 3.19% | 5 |
| `GeneralStore` | 4,267 | 3.17% | 5 |
| `[scene settings]` | 4,220 | 3.13% | 5 |
| `Sea` | 4,189 | 3.11% | 4 |
| `GreywickHouseRed` | 3,648 | 2.71% | 4 |
| `GreywickHouseTeal` | 3,647 | 2.71% | 4 |
| `Main Camera` | 2,998 | 2.23% | 5 |
| `PassageToCoddleCove` | 2,547 | 1.89% | 4 |
| `QuayEdge` | 2,503 | 1.86% | 3 |
| `Quay` | 2,500 | 1.86% | 3 |
| `PublicWharf` | 2,495 | 1.85% | 3 |
| `Shoreline` | 1,948 | 1.45% | 3 |
| `PersistentProviders` | 1,747 | 1.30% | 4 |
| `TidalTerrain` | 1,484 | 1.10% | 3 |
| `RegionSceneLoader` | 1,478 | 1.10% | 3 |
| `GreywickRegionAnchor` | 1,424 | 1.06% | 3 |
| `DevRegionBootstrap` | 1,370 | 1.02% | 3 |
| `GreywickDisembark` | 837 | 0.62% | 2 |
| `GreywickArrival` | 835 | 0.62% | 2 |
| `GreywickDockZone` | 832 | 0.62% | 2 |

### NineMileCreek

| Root | Bytes | Share | Objects |
|---|---:|---:|---:|
| `Shoreline` | 5,213,963 | 40.32% | 6,799 |
| `CreekHedgerows` | 1,347,369 | 10.42% | 1,792 |
| `CreekPlanting` | 1,107,986 | 8.57% | 1,440 |
| `CreekTrees` | 1,106,240 | 8.55% | 1,402 |
| `NineMileCreekRoads` | 977,927 | 7.56% | 11 |
| `NineMileCreekDressing` | 471,888 | 3.65% | 609 |
| `Yards` | 440,057 | 3.40% | 566 |
| `NmcWharfLandSide` | 366,942 | 2.84% | 478 |
| `CreekMarsh` | 328,082 | 2.54% | 430 |
| `NineMileCreekWharf` | 319,142 | 2.47% | 397 |
| `NmcWharfWaterSide` | 302,456 | 2.34% | 520 |
| `CreekFuel` | 224,280 | 1.73% | 312 |
| `CliffWalls` | 183,170 | 1.42% | 198 |
| `CreekUnderstorey` | 140,137 | 1.08% | 187 |
| `CreekFields` | 90,167 | 0.70% | 3 |
| `NmcWharfBoatYard` | 63,535 | 0.49% | 78 |
| `NineMileCreekNavMarks` | 55,200 | 0.43% | 30 |
| `DevCoreEnvelope` | 23,919 | 0.18% | 36 |
| `NmcBuildings` | 13,128 | 0.10% | 12 |
| `Bucket` | 12,392 | 0.10% | 7 |
| `CreekHouses` | 10,855 | 0.08% | 14 |
| `NineMileCreekFleet` | 10,849 | 0.08% | 23 |
| `CreekPeople` | 9,483 | 0.07% | 14 |
| `NineMileCreekBridges` | 8,814 | 0.07% | 8 |
| `NmcFishingGearWholesalerKeeper` | 7,525 | 0.06% | 11 |
| `NmcBoatGearWholesalerKeeper` | 7,471 | 0.06% | 11 |
| `Sea` | 5,386 | 0.04% | 5 |
| `[scene settings]` | 4,953 | 0.04% | 5 |
| `GeneralStore` | 4,837 | 0.04% | 6 |
| `NineMileCreekRegionAnchor` | 4,455 | 0.03% | 9 |
| `DerelictDory` | 4,232 | 0.03% | 5 |
| `NmcWashStations` | 3,673 | 0.03% | 4 |
| `TerrainSplat` | 3,657 | 0.03% | 4 |
| `NmcWharfYardCatalogue` | 3,481 | 0.03% | 6 |
| `DuallyAtThePark` | 3,088 | 0.02% | 6 |
| `PassageToRicePointStrand` | 3,081 | 0.02% | 5 |
| `Modern3500AtThePark` | 3,065 | 0.02% | 6 |
| `OtterAtTheRamp` | 3,055 | 0.02% | 6 |
| `Main Camera` | 2,986 | 0.02% | 5 |
| `NmcBoatGearWholesalerCounter` | 2,877 | 0.02% | 5 |
| `DialogueUI` | 2,849 | 0.02% | 4 |
| `PassageToRicePointSea` | 2,610 | 0.02% | 4 |
| `NmcWharfMarketCounter` | 2,607 | 0.02% | 5 |
| `PassageToWestWater` | 2,580 | 0.02% | 4 |
| `PassageToCoddleCove` | 2,575 | 0.02% | 4 |
| `PassageToStPeters` | 2,569 | 0.02% | 4 |
| `NmcWharfOfficeVendor` | 2,146 | 0.02% | 4 |
| `PersistentProviders` | 1,737 | 0.01% | 4 |
| `RegionSceneLoader` | 1,699 | 0.01% | 3 |
| `NineMileCreekLots` | 1,695 | 0.01% | 4 |
| `NmcWharfYardDoryTrade` | 1,568 | 0.01% | 3 |
| `NmcWharfYardOutboardTrade` | 1,563 | 0.01% | 3 |
| `WorldInteractor` | 1,560 | 0.01% | 3 |
| `NmcFishingGearWholesalerCounter` | 1,537 | 0.01% | 3 |
| `TidalTerrain` | 1,384 | 0.01% | 3 |
| `DevRegionBootstrap` | 1,369 | 0.01% | 3 |
| `NineMileCreekArrival` | 865 | 0.01% | 2 |
| `NineMileCreekBarLanding` | 846 | 0.01% | 2 |
| `NineMileCreekDisembark` | 845 | 0.01% | 2 |
| `NineMileCreekDockZone` | 843 | 0.01% | 2 |
| `CreekShops` | 826 | 0.01% | 2 |

### RicePoint

| Root | Bytes | Share | Objects |
|---|---:|---:|---:|
| `DevCoreEnvelope` | 595,273 | 90.58% | 108 |
| `LandRegion` | 56,578 | 8.61% | 96 |
| `[scene settings]` | 3,947 | 0.60% | 6 |
| `DevRegionBootstrap` | 1,369 | 0.21% | 3 |

### SouthWater

| Root | Bytes | Share | Objects |
|---|---:|---:|---:|
| `SouthWaterRegionAnchor` | 5,143 | 17.97% | 11 |
| `Sea` | 4,277 | 14.95% | 4 |
| `[scene settings]` | 3,663 | 12.80% | 5 |
| `Main Camera` | 2,935 | 10.26% | 5 |
| `PassageToRicePoint` | 2,562 | 8.95% | 4 |
| `PassageToEastWater` | 2,561 | 8.95% | 4 |
| `PassageToStPeters` | 2,560 | 8.95% | 4 |
| `TidalTerrain` | 1,755 | 6.13% | 3 |
| `RegionSceneLoader` | 1,589 | 5.55% | 3 |
| `RegionDisplayNames` | 1,567 | 5.48% | 3 |

### StPeters

| Root | Bytes | Share | Objects |
|---|---:|---:|---:|
| `IslandWoods` | 1,432,753 | 29.66% | 1,815 |
| `CliffWalls` | 745,297 | 15.43% | 842 |
| `Yards` | 655,845 | 13.58% | 848 |
| `ClamHoles` | 372,942 | 7.72% | 458 |
| `IslandVillage` | 240,222 | 4.97% | 282 |
| `Player` | 182,895 | 3.79% | 27 |
| `StPetersKeyScenes` | 151,466 | 3.14% | 197 |
| `IslandUnderstorey` | 144,551 | 2.99% | 194 |
| `Shoreline` | 123,099 | 2.55% | 149 |
| `ShorePlants` | 120,336 | 2.49% | 158 |
| `GinnyPlot` | 116,129 | 2.40% | 134 |
| `StPetersVillageCommons` | 110,115 | 2.28% | 146 |
| `StPetersNavMarks` | 57,127 | 1.18% | 28 |
| `Dory` | 56,830 | 1.18% | 49 |
| `StPetersVillageLights` | 54,086 | 1.12% | 87 |
| `StPetersWharf` | 53,506 | 1.11% | 79 |
| `Tools` | 36,409 | 0.75% | 35 |
| `IslandInhabitants` | 28,591 | 0.59% | 42 |
| `IslandShops` | 28,302 | 0.59% | 32 |
| `IslandRoutines` | 12,171 | 0.25% | 8 |
| `GeneralStoreCounter` | 10,673 | 0.22% | 16 |
| `AuntGinny` | 6,149 | 0.13% | 9 |
| `Main Camera` | 5,467 | 0.11% | 9 |
| `Sea` | 5,389 | 0.11% | 5 |
| `[scene settings]` | 5,288 | 0.11% | 6 |
| `GameRoot` | 4,691 | 0.10% | 8 |
| `NedsLetter` | 3,678 | 0.08% | 5 |
| `WetBucketSpot` | 3,589 | 0.07% | 5 |
| `StPetersOpenAirFixtures` | 3,499 | 0.07% | 4 |
| `TerrainSplat` | 3,121 | 0.06% | 3 |
| `UtilityQuadAtTheStore` | 3,094 | 0.06% | 6 |
| `Enduro250AtTheStore` | 3,092 | 0.06% | 6 |
| `Trike200AtTheStore` | 3,091 | 0.06% | 6 |
| `StPetersCannery` | 3,052 | 0.06% | 4 |
| `TidalTerrain` | 2,934 | 0.06% | 3 |
| `DialogueUI` | 2,851 | 0.06% | 4 |
| `PassageToWestWater` | 2,585 | 0.05% | 4 |
| `PassageToEastWater` | 2,583 | 0.05% | 4 |
| `PassageToNineMileCreek` | 2,581 | 0.05% | 4 |
| `ControlSwitcher` | 2,454 | 0.05% | 4 |
| `StPetersArrivalOpening` | 2,112 | 0.04% | 3 |
| `RegionSceneLoader` | 2,062 | 0.04% | 4 |
| `StPetersPlantField` | 1,921 | 0.04% | 4 |
| `RegionTravelCoordinator` | 1,872 | 0.04% | 4 |
| `DevToast` | 1,836 | 0.04% | 4 |
| `Onboarding` | 1,744 | 0.04% | 4 |
| `PlacedTrapService` | 1,710 | 0.04% | 3 |
| `StPetersRegionAnchor` | 1,672 | 0.03% | 3 |
| `RegionDisplayNames` | 1,602 | 0.03% | 3 |
| `WorldInteractor` | 1,569 | 0.03% | 3 |
| `StartingPots` | 1,408 | 0.03% | 3 |
| `StartingBait` | 1,387 | 0.03% | 3 |
| `ArrivalLine` | 1,340 | 0.03% | 3 |
| `DevFastTide` | 1,309 | 0.03% | 3 |
| `StPetersEastWaterArrival` | 847 | 0.02% | 2 |
| `StPetersDockZone` | 844 | 0.02% | 2 |
| `StPetersArrival` | 843 | 0.02% | 2 |
| `StPetersDisembark` | 842 | 0.02% | 2 |
| `StPetersBarLanding` | 841 | 0.02% | 2 |
