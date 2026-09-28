# Wharf rig kit pass 2 — drop 12, Phase A

This imports the complete, unchanged delivery and registers its editor-only load chain. No pass-2 sheet is placed or loaded by the game. Pass 1's rig, sheets, contract, bakers, slicers, readers and scenes are unchanged. Phase B (Unity bake and plates) requires a later owner grant and map ruling.

The owner retired the scene exporter on 2026-09-27 at 01:17:31Z. Its intake steps are void: no exporter or exporter tests were run, and no package is changed. `EditMode + PlayMode tests` is the CI gate. An interim red `Scene export (python)` is main's known red pending retirement, not a reason to regenerate packages. In particular, this PR does not change `MANIFEST.json`.

## Delivery and checks

Archive: `9fb9a2c8-Pixel_art_capabilities_12wharfrigkitv2.zip`, 3,818,192 bytes, SHA-256 `facfc6fe0dc504bf2467d6aec873516e37a572be46e181617eafc9b98241dccd`. All 79 files are preserved under `docs/art/rigs/wharf-rig-kit-v2/`; [file table](wharf-pass2-file-table.md) gives every file and its reader. Text is pinned to LF by extension; PNGs retain LFS and `-text`.

Real Node `checks/run.js`, without `--write`: exit 0. All eight generated reports are byte-identical to the delivery. Sidecars: 29 identical; fits: 150 climbs, 750 ties, 200 boardings, 430 fixtures, 2,790 verbs, zero unfit; joins: 10, zero problems; berths: 14, zero problems; gangways and snow: zero problems; sprite entries: 200; sums: 77 covered files, zero problems.

All ten bundled libraries match the named copies on the base commit, #888 or #882 by Git blob identity. The baker needs **none** of `lib/` or `support.js`: those serve the viewer. Per-kit copies stay intact because the manifest covers the whole delivery.

Standalone ClearScript V8 7.5.1 executes the five files concatenated in README order. All **12 supplied engine-map PNGs** reproduce exactly in decoded RGBA, including every frame in the timberFloat sheets. tallPier: 421 x 386, pivot (210,212), 58,151 covered pixels. timberFloat: 1088 x 1344, cell 272 x 336, pivot (135,171), 313,089 covered pixels across the loop. There are no byte-channel or alpha differences. Comparison is by pixels, never PNG compression bytes.

`view()` is a diagnostic viewer for height, snow and layer. The engine encodings must be packed from the existing G-buffer: height R=high byte/G=low byte of millimetres above -10 m; snow byte in RGB; layer R=raised/G=group*40. This host-side encoding reproduces all supplied maps without editing the rig. It is separate from OUR key/rim/depth/coverage mask.

### Catalog and the approved Baseline exception

Install `wharfRig2Kit` with `RigCatalog.InstallModule`. Dependency order is geo → fam → verbs → renderer → kit. Entries:

| Key | Completion object | Convention | Basis |
|---|---|---|---|
| `wharfRig2` | `WharfRig2` | CounterClockwise | Visible G-buffer coordinates fit back to raster pixels: +X-axis angle 0.000000° at dir 0, -45.000000° at dir 1 after undoing 40° foreshortening |
| `wharfRig2Families` | `WharfGeo2.FAMILIES` | Clockwise (unused) | Family builders have no own rendered facing; follows the existing non-directional catalog placeholder convention |
| `wharfRig2Geometry` | `WharfGeo2` | Clockwise (unused) | Geometry library, no own rendered facing; same placeholder convention |
| `wharfRig2Kit` | `WharfRig2Kit` | CounterClockwise | Harbour models are rendered by the same measured WharfRig2 camera |
| `wharfRig2Verbs` | `WharfVerbs2` | Clockwise (unused) | Interaction data, no own rendered facing; same placeholder convention |

The family script augments `WharfGeo2`. Registering that same completion name twice would skip the family script through the catalog's existing de-duplication rule. Its distinct installed object `WharfGeo2.FAMILIES` preserves the source bytes, load order and global uniqueness; no shared loader changes are needed.

The owner authorized **only these five sorted Baseline rows** in `RigCatalogAssemblyTests`, each with a drop-12 comment. Existing rows and all assertions are unchanged. Coverage expands in `TheCatalogHoldsExactlyTheKeysMainDeclared` and `EveryEntryCarriesTheValuesMainDeclared`; neither test is renamed, removed or weakened. The remaining catalog tests, `NavBuoyRegistrationProbeTests.EveryRegisteredGlobalIsUnique`, `ShrubRigBakeTests` and `TreeRigBakeTests` are unedited.

## Fleet and thresholds — owner decisions, no balance changes

The [fleet comparison](wharf-pass2-fleet-comparison.md) lists every mismatch and its source asset. Nineteen kit categories map to all 39 current boat ids. Every mapped BoatDef differs in at least one of LOA, gameplay footprint beam or draught. Bowrider and goFast have no boat id; Otter is a vehicle. BoatHullDef has no beam field, so the comparison follows its Visual.HullMesh to the gameplay half-beam.

Only `dory_outboard` exactly shares its base hull dimensions. `punt_upgraded`, `sport_skiff_twin` and `sport_skiff_mk2` require their own depth/width checks; inheriting the base berth unchanged is not safe.

The kit exposes mutable `HULLS` and `HULL`, but `gameplay`, `define` and `berthPlan` do not accept a replacement fleet argument. The packing closures use those tables and model caches preserve earlier choices. There is no supported, declarative fleet-input API. **Recommend sending a fleet-input request to Claude Design**, with our Def dimensions and stable ids, rather than treating mutation of exposed objects as a settled API. No rig or sidecar is changed here. The owner also decides whether bowrider/goFast become boats or lose their berths, and whether Otter belongs at a slipway.

The charter's threshold premise needed correction: at d0488e18, shipped `GameConfig.asset` and `LadderBoardingSettings.Default` both use **1.2 m**. Existing `LadderBoardingConfigTests` explicitly preserves that choice. **0.55 m belongs to pass 1's stricter rig rule**, measured on the gunwale gap. Pass 2's plan uses actual step endpoints, so the values are not interchangeable without reconciling the measured quantities.

| Quantity | Existing game / pass 1 | Pass 2 |
|---|---|---|
| Shipped ladder threshold | GameConfig 1.2 m; pass-1 rig 0.55 m | Direction-dependent step planning; typically ladder beyond 1.30 m, with a boarding-step path onto a hull up to 1.324 m |
| Step modes | Existing board/ladder choice | Walk below 0.06 m; board with grip 0.549–0.724 m; reversed boardDown below 0.549 m; boardDown/no-grip reverse up to 1.30 m; intermediate boarding step 0.60 m |
| Rungs | 0.30 m | 0.24 m (-0.06) |
| Ladder standoff | 0.275 m | 0.275 m (same) |
| Float freeboard reference | 0.40 m | Timber/drum 0.45 (+0.05), plastic 0.32 (-0.08), concrete 0.50 (+0.10), finger 0.40 (same); joined fingers inherit main float |
| Top-out / animation | Existing rig6 ladder cycle: two 0.30 m steps per 1.10 s | Top-out at three 0.24 m rungs = 0.72 m; root motion 0.436 m/s; character transition requests remain open |

No threshold, freeboard or character animation is changed by this intake.

## Map decision and cost

The delivered layout has **25 presets x 8 facings = 200 entries**. Six presets are moving, including the gangway, each with a 4x4 loop. Max dimension is **1732 px**, below 2048. Total area for one complete map is **63,615,916 pixels**. Costs below are uncompressed RGBA32, no mips, one variant/season/stage, all facings resident. MB is decimal; MiB is binary. Additional geometry states multiply the cost; loading only the placed subset reduces residency.

| Option | Full-kit textures | MiB / decimal MB | Draw and per-frame model |
|---|---|---:|---|
| W1 | `_lit` | 242.68 / 254.46 | One existing albedo pass per visible module; one sample per fragment. No new lighting CPU work. Reference light is frozen; ordinary scene tint still applies. |
| W2 | `_unlit`, `_normal`, `_light` | 728.03 / 763.39 | One proposed lit-sprite pass per visible module, three texture reads plus normal/light arithmetic; existing shared weather globals. No new per-sprite CPU relight. |
| W3 | W2 + `_height` | 970.70 / 1017.85 | Four map reads plus water-depth/contact calculations; integration with the water compositor must be agreed with S1. A separate water pass, if selected, adds a draw per module and is NOT authorized here. |

These are map payloads, not measured frame times or draw-call counts. For N visible independently drawn modules, the unbatched structural budget is N draws in one pass; material/atlas batching may reduce that, while raised/floor splitting, shadows and local lights increase it. Data maps are sampled with the albedo UVs, not drawn as separate sprites.

- **W1 waterline:** zero water pixels does not mean the structure is clipped to the current tide. Submerged piles and faces remain in the image. Existing water sorting alone cannot recover their per-pixel height, so W1 cannot promise a moving wash line or correct tide occlusion.
- **W2 light fidelity:** `_light` is not our mask. Keep R key/G back rim/B depth/A coverage unchanged and bind a new texture/property for the kit's sky visibility, porosity and height. Add new terms to HLSL and `SpriteLightMath.cs` together, with twin/mask tests. Three maps do not encode the complete CPU reference: `_unlit` already contains flat-sky banding, while directional self-shadow maps and material palette ramps are not fully represented. Live directional response is feasible; exact reference reproduction needs a separately agreed data contract. Do not promise it from three textures.
- **W3 contact:** the reference `contact(frame)` depends on tide and pose. A static contact sheet is not valid for every tide. An R8 contact sheet for one state adds 60.67 MiB (63.62 MB), RGBA32 adds 242.68 MiB (254.46 MB). Prefer deriving the moving contact band on GPU from height and OUR water surface. This overlaps S1's field sampling, compositing and foam law. Shader/pass choice waits for its owner.
- **Snow:** optional R8 adds 60.67 MiB; RGBA32 adds 242.68 MiB. Read #882's one snow global after it lands. No duplicate global.
- **Detail:** would feed face/part debugging or a later material lookup; leave unbaked until it has a reader.
- **Layer:** would feed fixed-versus-moving and floor-versus-raised composition. It cannot make independently baked floats rigid by itself. Leave unbaked until the placement/composition design is ruled.

**Recommend W2 as the first live-light option**, coordinated with drop 14, with the owner explicitly accepting its fidelity limits. W1 is a frozen-light fallback. W3 is the right eventual waterline capability but should be integrated with S1 after the shared surface contract is settled. The owner chooses the shared-light lane once. This PR changes neither light twin nor either water shader.

Future files/tests by option:

- All options: new pass-2 baker, slicer, contract and `Assets/_Project/Art/Sprites/Wharf/Pass2/`; names/layout from sprites.json; contract drift, import settings, pivots, caps and pixel tests. No bare-name sheet lookup and no pass-1 slicer changes.
- W1: those files only, with reference-sky golden pixels and an explicit tide-occlusion limitation.
- W2: additional/new lit-sprite material path, shared light include and C# twin together, tests for weather response, each new term, unchanged mask order and common UV/coverage. One pass, no CPU per-pixel lighting.
- W3: height import/encoding plus S1's agreed water include/compositor; tests for moving-tide occlusion, contact continuity, shared sea sampling, and no added pass/allocation unless explicitly granted.

## Float mechanism and joined modules

The kit's assembled harbours really do use **one motion group** for connected main floats and fingers. `snap` propagates group/freeboard, and `buildHarbour` computes a common waterplane and rigid transform. Guide piles, anchor and hinge are static. This answers #846's independent-tilt problem for assembled models.

The individual preset sheets do not automatically carry that guarantee into placement. Their distinct lengths and waterplanes produce different heave/pitch/roll; a shared frame index alone does not close a joint. A future bake must preserve a group's common origin/pose when cropping module outputs or bake connected-group variants. It must also separate the fixed guide piles from tide lift. The 25 default sheets are therefore an inventory, not proof that arbitrary joined placements are ready.

Proposed later runtime mechanism: a pooled registry of connected groups, sampled on the existing water/simulation tick. Read the same field at each group's position from worldSeed/gameTime through Core. Select one of 16 precomputed pose samples by a weighted squared error in heave/pitch/roll; weights and response limits are Def data. Publish one index per group, shared by members, only when it changes. No `Time.time`, kit-wind clock, LINQ, new collections or per-frame CPU relight. Fixed-size scratch arrays and cached sprite references allocate at registration, not at tick time.

For G groups and N members, budget G sea-footprint samples + 16G small pose comparisons per tick and at most N sprite assignments when indices change; no additional per-frame CPU step. Pose triples cost 16 x 3 x 4 = 192 bytes per baked group/state before indices and references. Multi-point footprint sampling multiplies the sea-query term; its count belongs in configuration. Frame quantization remains visible and cannot exactly reproduce arbitrary sea slopes from one 16-pose loop. Calm/rough coverage, rigid joins, grounded floats and gangway endpoints must pass plates before placement. Tests: seed/time repeatability, zero steady-state allocations, no free clock, calm frame, group coherence and fixed-guide invariance.

## Validation, test forecast and remaining work

New `WharfPass2IntakeTests`: **16 cases**, all for the delivered editor rig/data:

- `DeliveredKitMatchesPinnedManifestAndLfBytes` — immutable manifest and all delivered bytes.
- `CatalogInstallsExactlyTheReadmeChainWithoutViewerLibraries` — order, family/preset counts and no viewer dependencies.
- `AllSidecarsMatchLiveModelsAndPinTheCharacterTheyName` — all 29 schemas/keys, byte regeneration and CHAR.rigSha. That existing hash names the character rig, not a missing wharf source hash; the wharf sources are pinned by SHA256SUMS.
- `RasterAndHarbourConventionsFollowMeasuredPixelCoordinates` — measured facing convention.
- `SuppliedEngineMapMatchesV8Pixels`: tallPier × lit/unlit/normal/light/height/snow/detail/layer (8); timberFloat × unlit/normal/height/snow (4).

Only the approved Baseline rows change in the existing test file. No test is moved, renamed, retired or disabled. No production subject is retired. Against the seat's cf6a819b forecast, this lane adds **16 EditMode passes and zero PlayMode cases**: EditMode **13152/12900/252/0**, PlayMode **998/916/82/0**, assuming no further main drift. Counts are total/passed/skipped/failed; final CI must be checked by name.

Offline compile succeeds for current Core (241 sources), Art.Editor (83), RigBaking.Editor (166), and the new/affected plus named guard fixtures (5 files), using read-only borrowed ScriptAssemblies and Unity's compiler. Rebuilding Core and Art.Editor removed stale-reference failures in untouched files. Nine pure guard methods pass against these compiled assemblies: all six catalog tests, global uniqueness, and the tree/shrub catalog exclusions. The full Unity fixtures and all other tests remain CI's job; no local editor was launched.

Bake time, actual texture residency, draw calls and frame time are **not measured** in Phase A. No slot has been used. Request Phase B only after the owner chooses maps, the shared-light lane, fleet input/boat-less categories and threshold treatment. Its test stage must cover fishing harbour at noon/golden/night x low/mid/high tide, calm/blow floats and a pass-1 comparison, with the editor closed and shared save unchanged at return. Placing either real wharf and retiring pass 1 are later world-content work.
