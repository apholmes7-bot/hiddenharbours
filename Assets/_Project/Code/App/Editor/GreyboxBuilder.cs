#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;         // BoatHullDef / PropulsionType (the hull defs authored as data)
using HiddenHarbours.Fishing;       // FishSpeciesDef / Gear (the cove's fishing-ground species)
using HiddenHarbours.Economy;       // Market / FishBuyer / WharfSellPoint / Shipwright (the cove's services)
using HiddenHarbours.World;         // RegionDef / RegionSceneLoader / RegionPassage
using HiddenHarbours.App;           // RegionAnchor / RegionLogicRoot / PersistentHoldProxy / PersistentWalletProxy
using UnityEngine.Rendering.Universal; // PixelPerfectCamera
using HiddenHarbours.Art.Editor;   // VS-23 locked Pixel-Perfect camera convention

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// One-click <b>Coddle Cove</b> — the player's HOME HARBOUR as a PLAIN region scene (#66 demotion),
    /// and the <b>committed hand-authored-scene pilot</b> (ADR 0011).
    ///
    /// <para><b>The ADR 0011 hybrid.</b> A committed region scene has two layers, ONE author each:</para>
    /// <list type="bullet">
    /// <item><b>LOGIC layer (code/builder).</b> The invisible gameplay scaffolding the simulation reads —
    /// the <see cref="RegionAnchor"/> + its arrival/dock/disembark markers, the wharf economy (Fish Buyer +
    /// Shipwright) resolved through the persistent hold/wallet proxies, the region loader + the Cove→Greywick
    /// passage, the shore/piling colliders that gate gameplay, the fishing-spot marker, and the
    /// standalone-review camera. ALL of it is parented under ONE tagged root —
    /// <c>--LOGIC-- (generated, do not edit)</c>, marked with <see cref="RegionLogicRoot"/>.</item>
    /// <item><b>VISUAL layer (the owner).</b> Everything you SEE — painted terrain Tilemaps + dropped decor
    /// prefab instances from the #71 toolkit. It lives OUTSIDE the <c>--LOGIC--</c> root and the builder NEVER
    /// touches it. The old placeholder visuals (the tiled sea/ground/dock sprites, the sea-marker scatter, the
    /// cottage sprite, the hardcoded tree scatter) are RETIRED — the owner's painting replaces them.
    /// EXCEPTION: the SEA is back under <c>--LOGIC--</c>, but as SYSTEMIC plumbing, not a placeholder — the
    /// tide-driven WaterSurface water (the converged St Peters model, ADR 0012) plus its RectTidalTerrain
    /// height source. It is config-heavy and regenerated on refresh; it renders ABOVE the painted ground
    /// (tilemaps sort at −20) and clips itself transparent over dry land, so the painting shows through.</item>
    /// </list>
    ///
    /// <para><b>Two menu entry points (ADR 0011 Option A — idempotent Refresh):</b></para>
    /// <list type="bullet">
    /// <item><b>Hidden Harbours ▸ Build Greybox Scene</b> — FULL build. Creates the scene from zero (the
    /// <c>--LOGIC--</c> root + nothing else). Use this ONCE to bake the committed cove. After the owner has
    /// painted, this WARNS first, because a from-zero rebuild discards the hand-authored visual layer.</item>
    /// <item><b>Hidden Harbours ▸ Refresh Cove Logic</b> — SAFE in-place logic update. Opens the committed
    /// cove scene, destroys + regenerates ONLY the <c>--LOGIC--</c> subtree, and leaves every painted/decor
    /// object alone. This is the command the owner runs after the scene is committed + hand-painted whenever
    /// the gameplay logic needs to move forward.</item>
    /// </list>
    ///
    /// <para><b>Demoted from the start (#66).</b> The opening arc is St Peters → Greywick → buy + repair the
    /// dory → SAIL HOME to Coddle Cove. The cove is a PLAIN region exactly like <see cref="GreywickBuilder"/>:
    /// it authors only the region's own logic + a <see cref="RegionAnchor"/>; the persistent rig is carried in
    /// from the START scene (St Peters) via the <see cref="RegionTravelCoordinator"/> and BINDS on arrival.</para>
    ///
    /// <para>This is a dev convenience, not shipping content — real scenes are authored by world-content
    /// (backlog VS-02; ADR 0011 for the committed hand-authored-scene plan).</para>
    /// </summary>
    public static class GreyboxBuilder
    {
        const string DataConfig = "Assets/_Project/Data/Config";
        const string DataBoats  = "Assets/_Project/Data/Boats";
        const string DataFish   = "Assets/_Project/Data/Fish";
        const string DataShip   = "Assets/_Project/Data/Shipwright";
        const string DataRegions= "Assets/_Project/Data/Regions";        // VS-22 region defs (cove + Greywick)
        const string ArtSprites = "Assets/_Project/Art/Sprites";
        const string ArtPunt    = "Assets/_Project/Art/Boats/Punt.png";          // tier-1 swap sprite (VS-16) — kept on the hull asset
        const string ArtSea      = "Assets/_Project/Art/Tilesets/Water/SeaTile.png";
        const string ArtWaterMat = "Assets/_Project/Art/Materials/Water.mat";   // the layered SIM-driven water shader (ADR 0010)
        // Weather-driven water palette anchor presets (ADR 0017) — same wiring as St Peters: base = null on
        // purpose so the live Water.mat is the calm baseline (never a frozen preset copy).
        const string ArtWaterPresets   = "Assets/_Project/Art/Materials/WaterPresets";
        const string ArtWaterCalmMood  = ArtWaterPresets + "/Water_GlassyCalm.mat";    // CALM (low sea-state)
        const string ArtWaterStormMood = ArtWaterPresets + "/Water_StormGrey.mat";     // STORM (high sea-state)
        const string ArtWaterFogMood   = ArtWaterPresets + "/Water_FoggySmother.mat";  // FOG (low visibility)
        const string Scenes     = "Assets/_Project/Scenes";
        const string SceneName  = "Greybox";
        const string ScenePath  = Scenes + "/" + SceneName + ".unity";

        // The region id the cove's generated logic root belongs to (ADR 0011 single-author-per-layer marker).
        const string CoveRegionId = "region.coddle_cove";

        // The one tagged root that holds ALL builder-generated LOGIC. The owner's painted VISUAL layer
        // (Grid/Tilemaps + decor prefab instances) lives OUTSIDE this root and the builder never touches it.
        public const string LogicRootName = "--LOGIC-- (generated, do not edit)";

        // VS-22 crossing geometry (canon map: Port Greywick lies WEST of the cove — "PORT GREYWICK ——+——
        // CODDLE COVE"). So you CROSS BY SAILING WEST, and you RETURN to the cove dock FROM THE WEST. These
        // are public so an EditMode test (CrossingDirectionTests) can assert the crossing reads true without
        // loading a scene. CoveDockZoneRadius mirrors ControlSwitcher's default _zoneRadius (the cove disembark
        // is a pure distance test), so the return arrival must park within it of the cove dock or E can't
        // disembark — the proven cove/Greywick pattern (#52).
        public const float CoveDockZoneRadius = 3.5f;
        public static readonly Vector3 CoveDockZonePos      = new Vector3(0f, -12f, 0f);     // cove dock head / mooring
        public static readonly Vector3 CoveArrivalPos       = new Vector3(-2.5f, -13.5f, 0f); // return from Greywick: just WEST of the dock
        public static readonly Vector3 CoveDisembarkPos     = new Vector3(0f, -10.5f, 0f);    // on the dock planks
        public static readonly Vector3 ToGreywickPassagePos = new Vector3(-22f, -12f, 0f);   // WEST edge of the open water → sail west to cross

        // --- CONVERGED TIDE-DRIVEN WATER MODEL (ADR 0012 rec. 4 / ADR 0014; shoreline convergence) ------
        // The cove now runs the SAME water model as St Peters: an analytic seabed (a RectTidalTerrain, the
        // rectangular twin of St Peters' TidalTerrain) registered into GameServices.TidalTerrain + a Sea
        // plane carrying the layered WaterSurface shader that bakes THAT terrain — so the visible waterline
        // and the walkability/boat-grounding gate read the one same height (P1). The old model — a static
        // no-tide shore (a fixed collider fence beside a flat sea sprite) — is retired as the LOOK; the
        // ShoreEdge fence REMAINS as the cozy gameplay bounds collider (P5), it just no longer stands in for
        // a shoreline. Geometry mirrors the existing cove layout (land north of the y=-5 fence line, the
        // dock spur running south to y=-12): the land is a rectangular plateau whose SOUTH falloff is the
        // visible beach the tide sweeps, and the dock spur is a second, steep-sided plateau so the planks
        // stay walkable at high water while the moored boat floats beside them at low water.
        //
        // NOTE the LIVE tide these values are authored against is the persistent core's (the START scene's,
        // St Peters: mean 0, amplitude ±3.5 m — PersistentCoreBuilder: "nothing re-points it on a region hop
        // yet"). The cove RegionDef's gentler 1.6 m amplitude is aspirational data for the future per-region
        // tide seam (gameplay-systems); until that lands, author against ±3.5. All tunables (rule 6);
        // public + single-source-of-truth so the EditMode convergence test asserts the same coast the scene
        // is built from (the StPetersBuilder convention).
        public const float CoveDeepElevation = -4f;   // open-water floor: never bares at low water (-3.5)
        public const float CoveLandElevation = 6f;    // the home ground: dry at every tide (high water +3.5)
        public const float CoveBeachFalloff  = 5f;    // gentle south beach → the waterline visibly sweeps ~2-3 m
        public const float CoveDockFalloff   = 1f;    // steep-sided dock spur → deep enough to float alongside
        public static readonly Vector2 CoveLandCenter   = new Vector2(0f, 3.5f);
        public static readonly Vector2 CoveLandHalfSize = new Vector2(12f, 8.5f);   // x -12..12, y -5..12 (⊇ the fence interior)
        public static readonly Vector2 CoveDockCenter   = new Vector2(0f, -8.5f);
        public static readonly Vector2 CoveDockHalfSize = new Vector2(1.5f, 3.5f);  // the spur: x -1.5..1.5, y -12..-5
        // The Sea plane + height-map bake rectangle (spans the visible/playable water incl. the west passage).
        public static readonly Vector2 CoveSeaCenter = new Vector2(0f, -2f);
        public static readonly Vector2 CoveSeaSize   = new Vector2(80f, 50f);
        public const int   CoveHeightResolution = 192;   // ADR 0012 §A step 1 (the smoothed-shore bake)
        public const float CoveHeightMin = -4f;           // brackets the deep floor …
        public const float CoveHeightMax = 6f;            // … and the land plateau

        // =====================================================================================
        //  ENTRY POINTS
        // =====================================================================================

        /// <summary>
        /// FULL build: create <c>Greybox.unity</c> from zero with just the <c>--LOGIC--</c> root. Use this the
        /// FIRST time to bake the committed cove. After the owner has hand-painted, this discards their visual
        /// layer, so it WARNS first (ADR 0011: never silently destroy the owner's work).
        /// </summary>
        [MenuItem("Hidden Harbours/Build Greybox Scene")]
        public static void Build()
        {
            // ADR 0019 §1 / ADR 0011 guard: a from-zero build wipes the owner's hand-authored layer. If a
            // committed cove already exists, make the owner confirm — Refresh Cove Logic is the safe path once
            // it's painted. Shared with every region builder via RegionBuildGuard so the wording is identical.
            if (!RegionBuildGuard.ConfirmOverwrite("Coddle Cove", ScenePath))
                return;

            var data = PrepareData();

            // Fresh empty scene → place ONLY the tagged logic root + its subtree. No placeholder visuals: the
            // owner's painting is the visual layer now (ADR 0011).
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = CreateLogicRoot(scene);
            BuildLogicTree(root.transform, data);

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterScene(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[GreyboxBuilder] Built Greybox.unity (ADR 0011 committed-scene pilot): a single " +
                      $"'{LogicRootName}' root holds all of the cove's LOGIC (RegionAnchor + arrival/dock/" +
                      "disembark, Fish Buyer + Shipwright via the persistent proxies, loader + Cove→Greywick " +
                      "passage, shore/piling colliders, fishing spot, standalone camera — PLUS the converged " +
                      "tide-driven water, ADR 0012: a RectTidalTerrain + the layered WaterSurface Sea whose " +
                      "shoreline visibly sweeps the south beach off the live tide, the SAME height the " +
                      "walkability/grounding reads). NO placeholder " +
                      "visuals — PAINT the terrain + drop decor OUTSIDE the --LOGIC-- root, then File ▸ Save " +
                      "(the Sea clips transparent over dry land, so your painting shows through). " +
                      "After it's painted, use 'Refresh Cove Logic' (never 'Build') to update logic.");
            EditorUtility.DisplayDialog("Hidden Harbours",
                "Coddle Cove baked as a COMMITTED scene (ADR 0011 pilot).\n\nThe scene now contains ONE " +
                $"'{LogicRootName}' object holding all the gameplay logic. There are no placeholder visuals " +
                "— that's deliberate: YOU paint the look.\n\nNext:\n" +
                "1. Hidden Harbours ▸ Art ▸ Add Paintable Tilemap, then paint terrain.\n" +
                "2. Drag decor prefabs from Assets/_Project/Prefabs/Decor into the Scene.\n" +
                "3. Leave the --LOGIC-- object alone.\n" +
                "4. File ▸ Save (Ctrl+S).\n\nFrom now on, to update the logic WITHOUT losing your painting, " +
                "use 'Hidden Harbours ▸ Refresh Cove Logic' — NOT this command.", "Fair winds");
        }

        /// <summary>
        /// SAFE in-place logic update (ADR 0011 Option A). Opens the committed cove scene (or uses it if it's
        /// already the active scene), destroys + regenerates ONLY the <c>--LOGIC--</c> subtree, and leaves
        /// every other object — the owner's painted Tilemaps + decor — untouched. Idempotent: running it twice
        /// yields the same logic tree.
        /// </summary>
        [MenuItem("Hidden Harbours/Refresh Cove Logic")]
        public static void RefreshLogic()
        {
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("Hidden Harbours — no committed cove yet",
                    "There's no committed Coddle Cove scene to refresh.\n\nRun 'Hidden Harbours ▸ Build " +
                    "Greybox Scene' once to bake the initial committed scene, then paint, then use this " +
                    "command to update the logic later.", "OK");
                return;
            }

            // Open the committed scene single (or reuse it if already active) — NEVER NewScene (that's the
            // exact step that destroys hand-painting).
            var active = EditorSceneManager.GetActiveScene();
            Scene scene = (active.IsValid() && active.path == ScenePath)
                ? active
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var data = PrepareData();
            RebuildLogicSubtree(scene, data);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            RegisterScene(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[GreyboxBuilder] Refreshed Coddle Cove LOGIC in place (ADR 0011): destroyed + " +
                      $"regenerated only the '{LogicRootName}' subtree; the painted/decor visual layer was " +
                      "left untouched. Saved Greybox.unity.");
            EditorUtility.DisplayDialog("Hidden Harbours",
                "Coddle Cove logic refreshed.\n\nOnly the --LOGIC-- object was rebuilt; your painted terrain " +
                "and decor are untouched. The scene has been saved.\n\nIf this was an art session, commit your " +
                "scene on a branch + open a PR (see docs/authoring-scenes.md §7).", "Fair winds");
        }

        /// <summary>
        /// Rebuild ONLY the tagged logic subtree of <paramref name="scene"/>: destroy every existing
        /// <see cref="RegionLogicRoot"/> root in the scene, then create one fresh root and populate it. This is
        /// the surgical reconciler that protects the painted layer — it touches nothing outside the root.
        /// Public so an EditMode test can drive it against an in-memory scene.
        /// </summary>
        public static void RebuildLogicSubtree(Scene scene, DataRefs data)
        {
            // Destroy any pre-existing logic roots (there should be exactly one; tolerate zero or many). We key
            // on the RegionLogicRoot tag component, NOT on the name, so a renamed/duplicated root is still found
            // and we never accidentally destroy a painted object that merely shares the name.
            var roots = scene.GetRootGameObjects();
            foreach (var go in roots)
            {
                if (go == null) continue;
                if (go.GetComponent<RegionLogicRoot>() != null)
                    Object.DestroyImmediate(go);
            }

            var root = CreateLogicRoot(scene);
            BuildLogicTree(root.transform, data);
        }

        // =====================================================================================
        //  LOGIC ROOT + TREE
        // =====================================================================================

        /// <summary>Create the single tagged <c>--LOGIC--</c> root GameObject in <paramref name="scene"/>.</summary>
        static GameObject CreateLogicRoot(Scene scene)
        {
            var go = new GameObject(LogicRootName);
            // Ensure it lands in the target scene (NewScene/OpenScene make it active, so new GOs go there; this
            // is belt-and-braces for the test path where the scene may not be active).
            if (go.scene != scene) SceneManager.MoveGameObjectToScene(go, scene);
            var tag = go.AddComponent<RegionLogicRoot>();
            tag.SetRegionId(CoveRegionId);
            return go;
        }

        /// <summary>
        /// Populate the LOGIC layer under <paramref name="root"/>. EVERYTHING the builder authors is parented
        /// here so the owner's painted/decor layer (outside the root) is never touched. No placeholder visuals
        /// (sea/ground/cottage sprites, tree scatter) are authored — the owner paints those (ADR 0011).
        /// </summary>
        static void BuildLogicTree(Transform root, DataRefs data)
        {
            // --- CAMERA (standalone-viewable; the coordinator silences it on arrival) ----------
            // Mirrors Greywick's locked pixel-perfect, on-foot landscape framing so the cove reads at the same
            // scale when reviewed standalone. The persistent core owns the live camera in play.
            var camGo = new GameObject("Main Camera");
            camGo.transform.SetParent(root, false);
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = CameraFollow.OrthoSizeForWorldHeight(CameraFollow.OnFootWorldHeightMeters);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.13f, 0.18f);   // slate so an as-yet-unpainted cove isn't black
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();
            // VS-23: lock the Pixel-Perfect camera (PPU 32), 16:9 LANDSCAPE reference (PC-first, ADR 0005).
            ArtCameraSetup.ConfigurePixelPerfect(camGo);
            var ppc = camGo.GetComponent<PixelPerfectCamera>();
            if (ppc != null)
            {
                CameraFollow.ReferenceResolutionForWorldHeight(CameraFollow.OnFootWorldHeightMeters, out int refW, out int refH);
                ppc.refResolutionX = refW;
                ppc.refResolutionY = refH;
                EditorUtility.SetDirty(ppc);
            }

            // --- SHORE EDGE (gameplay bounds; collider only, no placeholder sprite) -------------
            // A closed collider fence tracing the beach + dock so the player can roam the island and walk out
            // the dock, but can't wander into open water (P5 cozy bounds). The boat bumps it too. NOTE this
            // fence is BOUNDS, not the shoreline: the visible waterline is the tide-driven WaterSurface shore
            // below (ADR 0012 — the fence sits just inside the land plateau, and the tide sweeps the beach
            // south of it, where the shallows also SLOW the boat before it ever reaches the fence). The fence
            // is LOGIC; the look is the shader + the owner's painting.
            var shore = new GameObject("ShoreEdge");
            shore.transform.SetParent(root, false);
            var edge = shore.AddComponent<EdgeCollider2D>();
            edge.points = new[]
            {
                new Vector2(-10f, 9f), new Vector2(10f, 9f), new Vector2(10f, -5f),
                new Vector2(1.5f, -5f), new Vector2(1.5f, -12f), new Vector2(-1.5f, -12f),
                new Vector2(-1.5f, -5f), new Vector2(-10f, -5f), new Vector2(-10f, 9f),
            };

            // --- TIDAL TERRAIN (the converged one-height source; ADR 0012 rec. 4) ----------------
            // The cove's analytic seabed: a RectTidalTerrain (land plateau + dock spur over a deep floor)
            // that registers into GameServices.TidalTerrain at runtime, so the on-foot walkability, the
            // boat grounding AND the water shader below all read the SAME height (P1 — what you see is
            // what you can sail/walk). Created BEFORE the Sea so on a region toggle-on the terrain's
            // OnEnable registers before the WaterSurface's OnEnable bakes (children enable in order).
            // Hand-painting later replaces this via the Terrain Paint Tool's Adopt step (ADR 0014) —
            // the same adoption seam St Peters has.
            var terrainGo = new GameObject("TidalTerrain");
            terrainGo.transform.SetParent(root, false);
            var terrain = terrainGo.AddComponent<RectTidalTerrain>();
            ConfigureCoveTerrain(terrain);

            // --- SEA (the layered SIM-DRIVEN water shader — the St Peters model; ADR 0010/0012) ---
            // A tiled Sea plane carrying Water.mat + a WaterSurface that bakes the terrain above into the
            // shader's height map: the depth gradient, foam band and the hard wet/dry clip all follow the
            // live deterministic tide — the shoreline visibly advances and retreats. Sorting -5 (the St
            // Peters slot): ABOVE the owner's painted ground tilemaps (-20, PaintableTilemapMenu) so
            // flooded ground is covered and dry ground shows through the clip, BELOW decor/buildings
            // (0..9) and the player (10). The Sea is builder-authored LOGIC-adjacent plumbing (config-
            // heavy, regenerated on refresh), so it lives under --LOGIC--; the owner's painted layer
            // stays untouched outside it (ADR 0011).
            var seaSprite = LoadSpriteAny(ArtSea);
            var seaGo = new GameObject("Sea");
            seaGo.transform.SetParent(root, false);
            seaGo.transform.position = new Vector3(CoveSeaCenter.x, CoveSeaCenter.y, 0f);
            var seaSr = seaGo.AddComponent<SpriteRenderer>();
            seaSr.sortingOrder = -5;
            if (seaSprite != null)
            {
                seaSr.sprite = seaSprite;
                seaSr.drawMode = SpriteDrawMode.Tiled;
                seaSr.size = CoveSeaSize;
            }
            else
            {
                seaSr.sprite = MakeSquareSprite(ArtSprites + "/Square.png");
                seaSr.color = new Color(0.15f, 0.27f, 0.34f);
                seaGo.transform.localScale = new Vector3(CoveSeaSize.x, CoveSeaSize.y, 1f);
            }
            var waterMat = AssetDatabase.LoadAssetAtPath<Material>(ArtWaterMat);
            if (waterMat != null)
            {
                seaSr.sharedMaterial = waterMat;
                var surface = seaGo.AddComponent<HiddenHarbours.Art.WaterSurface>();
                ConfigureWaterSurface(surface, CoveSeaCenter, CoveSeaSize,
                                      CoveHeightResolution, CoveHeightMin, CoveHeightMax);
                // (ADR 0017) The same weather-driven palette wiring as St Peters: base = null ON PURPOSE
                // (the live Water.mat is the calm baseline), storm/fog/calm anchors ease with the
                // deterministic weather. Null-safe if a preset hasn't imported yet.
                ConfigureWeatherPalette(
                    surface,
                    /*baseMood (null = the live Water.mat is the calm baseline)*/ null,
                    AssetDatabase.LoadAssetAtPath<Material>(ArtWaterCalmMood),
                    AssetDatabase.LoadAssetAtPath<Material>(ArtWaterStormMood),
                    AssetDatabase.LoadAssetAtPath<Material>(ArtWaterFogMood));
            }
            else
            {
                Debug.LogWarning("[GreyboxBuilder] Water.mat not found at " + ArtWaterMat + " — the cove Sea " +
                                 "is a plain backdrop. Re-run after the material imports for the tide-driven water.");
            }

            // --- DOCK PILING COLLIDERS (boat bumpers; collider only) ----------------------------
            // Slim circular colliders so the boat bumps the pilings (cozy, no damage) when nudging into the
            // slip, leaving a navigable channel between the two rows. The post SPRITES are the owner's decor.
            var pilings = new GameObject("Pilings");
            pilings.transform.SetParent(root, false);
            for (int i = 0; i < 3; i++)
            {
                float py = -6f - i * 3f; // -6, -9, -12
                MakePilingCollider(pilings.transform, new Vector2(-1.5f, py));
                MakePilingCollider(pilings.transform, new Vector2( 1.5f, py));
            }

            // --- FISHING SPOT (the gameplay spot location; collider/marker, no placeholder art) --
            // Marks WHERE the cove's fishing spot sits in the water beside the dock. The carried
            // FishingController casts from the persistent dory; this is the spot's gameplay position.
            var fishingSpot = new GameObject("FishingSpot");
            fishingSpot.transform.SetParent(root, false);
            fishingSpot.transform.position = new Vector3(5f, -10f, 0f); // in the water beside the dock

            // --- WHARF (Fish Buyer + Shipwright; resolved through the persistent proxies) --------
            // The cove keeps its Fish Buyer (sell) + Shipwright (buy the Punt). The player's hold + wallet live
            // in the PERSISTENT core (a different scene), so the wharf resolves them through scene-local proxies:
            // PersistentHoldProxy forwards to the dory's ShipHold (coordinator binds it on arrival),
            // PersistentWalletProxy forwards to GameServices.Wallet. So you sell + buy against the hold + coin
            // you sailed in with.
            var providersGo = new GameObject("PersistentProviders");
            providersGo.transform.SetParent(root, false);
            providersGo.AddComponent<PersistentHoldProxy>();
            providersGo.AddComponent<PersistentWalletProxy>();

            var wharf = new GameObject("Wharf");
            wharf.transform.SetParent(root, false);
            var market = wharf.AddComponent<Market>();
            var buyer = wharf.AddComponent<FishBuyer>();
            var sellPoint = wharf.AddComponent<WharfSellPoint>();
            wharf.AddComponent<DevSellInput>();            // RequireComponent(WharfSellPoint) — present (greybox B to sell)
            SetRef(market, "_config", data.Config);
            SetRef(buyer, "_market", market);
            SetRef(sellPoint, "_buyer", buyer);
            SetRef(sellPoint, "_holdProvider", providersGo);
            SetRef(sellPoint, "_walletProvider", providersGo);

            var shipwrightGo = new GameObject("Shipwright");
            shipwrightGo.transform.SetParent(root, false);
            var shipwright = shipwrightGo.AddComponent<Shipwright>();
            shipwrightGo.AddComponent<DevBuyInput>();      // RequireComponent(Shipwright) — present (greybox P to buy)
            SetRef(shipwright, "_offer", data.PuntOffer);
            SetRef(shipwright, "_walletProvider", providersGo);

            // --- REGION SCENE-LOAD PATH ---------------------------------------------------------
            // The persistent travel rig (loader + coordinator) is carried in from the start scene. This scene
            // places its OWN RegionSceneLoader (reviewable standalone; the additive loader re-activates it) and
            // the Cove→Greywick passage. On arrival the carried coordinator drives travel.
            var loaderGo = new GameObject("RegionSceneLoader");
            loaderGo.transform.SetParent(root, false);
            var loader = loaderGo.AddComponent<RegionSceneLoader>();
            SetRefArray(loader, "_regions", new Object[] { data.CoveRegion, data.GreywickRegion });
            SetString(loader, "_currentSceneName", SceneName);   // this scene; explicit (don't rely on Awake vs DDOL order)

            // Cove→Greywick passage: SAIL WEST to cross — Port Greywick lies west of the cove (canon map). A
            // wide, forgiving band down the WEST edge of the open water.
            var toGreywickGo = new GameObject("PassageToPortGreywick");
            toGreywickGo.transform.SetParent(root, false);
            toGreywickGo.transform.position = ToGreywickPassagePos;
            var toGreywickTrigger = toGreywickGo.AddComponent<BoxCollider2D>();
            toGreywickTrigger.isTrigger = true;
            toGreywickTrigger.size = new Vector2(3f, 28f);   // a tall west-edge band (forgiving, wide)
            var toGreywickPassage = toGreywickGo.AddComponent<RegionPassage>();
            SetRef(toGreywickPassage, "_target", data.GreywickRegion);
            SetRef(toGreywickPassage, "_loader", loader);

            // --- REGION ANCHOR (the persistent rig binds here on arrival) ------------------------
            // The cove's board/dock geometry as a RegionAnchor (mirrors Greywick + St Peters). When you sail
            // home from Greywick (WEST), the carried RegionTravelCoordinator reads this anchor and: parks the
            // boat at the arrival point (just WEST of the dock), re-points the persistent ControlSwitcher's dock
            // to the cove dock zone + disembark spot. The arrival sits within CoveDockZoneRadius of the dock
            // zone (don't regress #52). NO persistent core is authored here.
            var anchorGo = new GameObject("CoveRegionAnchor");
            anchorGo.transform.SetParent(root, false);
            var dockZone = new GameObject("CoveDockZone");
            dockZone.transform.SetParent(anchorGo.transform, false);
            dockZone.transform.position = CoveDockZonePos;          // the dock head / mooring
            var disembarkPoint = new GameObject("CoveDisembark");
            disembarkPoint.transform.SetParent(anchorGo.transform, false);
            disembarkPoint.transform.position = CoveDisembarkPos;   // on the dock planks
            var coveArrival = new GameObject("CoveArrival");
            coveArrival.transform.SetParent(anchorGo.transform, false);
            coveArrival.transform.position = CoveArrivalPos;        // just WEST of the dock head (arrive from the west)
            var coveAnchor = anchorGo.AddComponent<RegionAnchor>();
            coveAnchor.Configure(CoveRegionId, coveArrival.transform, dockZone.transform, disembarkPoint.transform);
        }

        // =====================================================================================
        //  DATA ASSETS
        // =====================================================================================

        /// <summary>
        /// Refs the logic tree wires to. A struct so both Build and Refresh prepare/reload the data once and
        /// pass the (re)loaded, on-disk assets into <see cref="BuildLogicTree"/>.
        /// </summary>
        public struct DataRefs
        {
            public GameConfig Config;
            public BoatHullDef Dory;
            public BoatHullDef Punt;
            public RegionDef CoveRegion;
            public RegionDef GreywickRegion;
            public ShipwrightOffer PuntOffer;
        }

        /// <summary>
        /// Create/keep the cove's stable data assets and return RELOADED on-disk refs (a fresh CreateInstance
        /// may not serialize into a scene reliably). The cove is no longer the start, so it doesn't stand up
        /// the boat rig — but it still keeps these stable assets (first run wins; StPeters/Greywick author the
        /// canonical versions under the same ids). Public so a test can prep data without scene side effects.
        /// </summary>
        public static DataRefs PrepareData()
        {
            EnsureFolders();

            var config = LoadOrCreate<GameConfig>(DataConfig + "/GameConfig.asset");

            LoadOrCreate<BoatHullDef>(DataBoats + "/Dory.asset", h =>
            {
                h.Id = "boat.dory"; h.DisplayName = "The Dory";
                h.LengthMeters = 4.5f; h.DraughtMeters = 0.3f; h.MassKg = 400f;
                h.HoldUnits = 6; h.CrewSlots = 1;
                h.EnginePower = 1200f; h.RudderAuthority = 600f;
                h.ForwardDrag = 40f; h.LateralDrag = 240f; h.WindExposure = 1.2f;
                h.MaxSafeSeaState = SeaState.Lively;
                h.CameraWorldHeightMeters = 14f;   // intimate framing for the little dory
            });

            // Tier-1 "Punt / Skiff" (design/boats-and-navigation.md §1.1) — the first boat you BUY (VS-16).
            LoadOrCreate<BoatHullDef>(DataBoats + "/Punt.asset", ApplyPuntStats);

            // THE PILOTABLE FLEET. These are hulls the owner can put himself in via the dev boat picker —
            // NOT a shipwright ladder: nothing offers them for sale and OwnedFleet's purchase registry does
            // not know they exist. That is deliberate (rule 8): the M2 fleet roster and its economy are a
            // later phase, and this is a dev affordance for feeling the hulls in the same wave.
            LoadOrCreate<BoatHullDef>(DataBoats + "/ConsoleSkiff.asset", ApplyConsoleSkiffStats);
            LoadOrCreate<BoatHullDef>(DataBoats + "/SportSkiff.asset", ApplySportSkiffStats);
            LoadOrCreate<BoatHullDef>(DataBoats + "/SportSkiffTwin.asset", ApplySportSkiffTwinStats);
            // The punt's upgraded engine — a picker rung, NOT a purchase (no ShipwrightOffer: see the note
            // on ApplyPuntUpgradedStats).
            LoadOrCreate<BoatHullDef>(DataBoats + "/PuntUpgraded.asset", ApplyPuntUpgradedStats);

            // Regions (VS-22 travel): this cove + Port Greywick, as data, for the loader/passage.
            LoadOrCreate<RegionDef>(DataRegions + "/CoddleCove.asset", r =>
            {
                r.Id = CoveRegionId; r.DisplayName = "Coddle Cove"; r.SceneName = SceneName;
                r.IsDeepHarbour = false; r.HarbourDepthMeters = 2f;
                r.TideMeanLevel = 0f; r.TideAmplitude = 1.6f; r.TidePhaseHours = 0f;
                r.Description = "Your home harbour — the sheltered greybox cove.";
            });
            LoadOrCreate<RegionDef>(DataRegions + "/PortGreywick.asset", r =>
            {
                r.Id = "region.port_greywick"; r.DisplayName = "Port Greywick"; r.SceneName = "Greywick";
                r.IsDeepHarbour = true; r.HarbourDepthMeters = 6f;
                r.TideMeanLevel = 0f; r.TideAmplitude = 0.8f; r.TidePhaseHours = 2f;
                r.Description = "The market town: a deep, sheltered harbour where the coast's business gets done.";
            });

            // The cove's fishing-ground species (data-driven; the persistent FishingController reads its region
            // species by id). Created here if absent.
            Fish("fish.atlantic_cod", "Atlantic Cod", FishCategory.InshoreGroundfish, Rarity.Common,    14, 0.20f, 1.0f, 2f, 12f);
            Fish("fish.haddock",      "Haddock",      FishCategory.InshoreGroundfish, Rarity.Common,    16, 0.20f, 0.9f, 1f, 6f);
            Fish("fish.mackerel",     "Mackerel",     FishCategory.Pelagic,           Rarity.Uncommon,  10, 0.35f, 0.8f, 0.3f, 1.5f);
            Fish("fish.lobster",      "American Lobster", FishCategory.Shellfish,     Rarity.Prize,     28, 0.35f, 0.4f, 0.4f, 4f);

            // The Punt offer (VS-16): the price lives in a ShipwrightOffer asset; reference the boat by id.
            LoadOrCreate<ShipwrightOffer>(DataShip + "/PuntOffer.asset", o =>
            {
                o.BoatId = "boat.punt"; o.DisplayName = "The Punt"; o.Price = 1800;
            });

            // Reload from disk before wiring. An intervening AssetDatabase import can invalidate the in-memory
            // references created above; reloading guarantees valid refs so nothing serializes as "None".
            var dory = AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/Dory.asset");
            if (dory != null)   // gentle greybox tuning so the dory is slow enough to control on screen
            {
                dory.Propulsion = PropulsionType.Oars;   // the dory is hand-rowed (the oar-tunable defaults ride the Def)
                dory.EnginePower = 500f; dory.ForwardDrag = 120f; dory.LateralDrag = 320f; dory.WindExposure = 0.6f;
                dory.CameraWorldHeightMeters = 14f;
                ApplyDeckTray(dory);   // small boat → the fish tray (the deck-container ladder, data)
                EditorUtility.SetDirty(dory);
            }
            // The Punt keeps her old flat top-down Sprite as the FALLBACK picture — the one the skinner
            // brings back if her Visual is ever missing. Her stats + her iso skin are applied with the rest
            // of the fleet below (ApplyFleetHull), so there is ONE path that binds a hull to a BoatVisualDef.
            var punt = AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/Punt.asset");
            if (punt != null)
            {
                punt.Sprite = LoadArtSprite(ArtPunt);
                EditorUtility.SetDirty(punt);
            }

            // --- THE PILOTABLE FLEET: stats + the skin each hull wears ---------------------------------
            // Re-applied on every run (LoadOrCreate only inits on CREATE), so re-running the builder is how
            // a stat tweak in C# reaches the asset — the same contract Dory/Punt have always had.
            //
            // A hull's LOOK is its Visual ref (rule 2): the skiffs' compass + rock grid + outboard all ride
            // one BoatVisualDef, so the picker's re-skin is a data lookup, not a branch in code. A null ref
            // here (visuals not yet built) is NOT fatal — the hull just wears its plain rotating Sprite,
            // which for these is nothing. Hence the warning: it is the difference between "the owner sees
            // his skiff" and "the owner sees an invisible boat", and it is the single most likely thing to
            // be wrong after a fresh pull.
            ApplyFleetHull(DataBoats + "/ConsoleSkiff.asset", ApplyConsoleSkiffStats, "ConsoleSkiff");
            ApplyFleetHull(DataBoats + "/SportSkiff.asset", ApplySportSkiffStats, "SportSkiffSingle");
            ApplyFleetHull(DataBoats + "/SportSkiffTwin.asset", ApplySportSkiffTwinStats, "SportSkiffTwin");
            ApplyFleetHull(DataBoats + "/FishingSkiff.asset", ApplyFishingSkiffStats, "FishingBoat");
            // The Punt goes through the SAME path, though she is not a picker-only hull: she is a real,
            // purchasable M1 boat (PuntOffer, ₲1800) who simply never had a skin. Her iso kit landed in #210
            // and this is what makes her wear it. Her upgraded-engine sister rides the same 5.2 m hull.
            ApplyFleetHull(DataBoats + "/Punt.asset", ApplyPuntStats, "PuntIsoBasic");
            ApplyFleetHull(DataBoats + "/PuntUpgraded.asset", ApplyPuntUpgradedStats, "PuntIsoUpgraded");

            return new DataRefs
            {
                Config         = AssetDatabase.LoadAssetAtPath<GameConfig>(DataConfig + "/GameConfig.asset"),
                Dory           = dory,
                Punt           = punt,
                CoveRegion     = AssetDatabase.LoadAssetAtPath<RegionDef>(DataRegions + "/CoddleCove.asset"),
                GreywickRegion = AssetDatabase.LoadAssetAtPath<RegionDef>(DataRegions + "/PortGreywick.asset"),
                PuntOffer      = AssetDatabase.LoadAssetAtPath<ShipwrightOffer>(DataShip + "/PuntOffer.asset"),
            };
        }

        // =====================================================================================
        //  CONVERGED WATER MODEL — shared config (single source of truth with the EditMode test)
        // =====================================================================================

        /// <summary>Author the cove's analytic seabed from the public constants above (the land plateau
        /// whose south falloff is the visible tide beach + the steep-sided dock spur, over the deep floor).
        /// One place, mirrored by the EditMode shoreline-convergence test — the StPetersBuilder convention.</summary>
        public static void ConfigureCoveTerrain(RectTidalTerrain terrain)
        {
            terrain.Configure(CoveDeepElevation, new[]
            {
                new RectTidalTerrain.LandZone(CoveLandCenter, CoveLandHalfSize, CoveLandElevation, CoveBeachFalloff),
                new RectTidalTerrain.LandZone(CoveDockCenter, CoveDockHalfSize, CoveLandElevation, CoveDockFalloff),
            });
        }

        /// <summary>Configure the Sea's <see cref="HiddenHarbours.Art.WaterSurface"/>: the world rectangle
        /// the seabed height map bakes over, the bake resolution (ADR 0012 §A: 192), and the elevation range
        /// the baked R channel maps across (must bracket the deep floor and the land plateau). Persisted via
        /// SerializedObject (the builders' persist-the-refs convention).</summary>
        static void ConfigureWaterSurface(HiddenHarbours.Art.WaterSurface surface,
                                          Vector2 worldCenter, Vector2 worldSize, int resolution,
                                          float heightMin, float heightMax)
        {
            var so = new SerializedObject(surface);
            SetV2(so, "_heightWorldCenter", worldCenter);
            SetV2(so, "_heightWorldSize", worldSize);
            SetInt(so, "_heightResolution", Mathf.Clamp(resolution, 16, 256));
            SetF(so, "_heightMin", heightMin);
            SetF(so, "_heightMax", heightMax);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Enable the weather-driven water palette (ADR 0017) and assign the anchor mood presets —
        /// the same wiring St Peters uses (a null base = the live Water.mat is the calm baseline; a null
        /// anchor no-ops safely).</summary>
        static void ConfigureWeatherPalette(HiddenHarbours.Art.WaterSurface surface,
                                            Material baseMood, Material calmMood,
                                            Material stormMood, Material fogMood)
        {
            var so = new SerializedObject(surface);
            var enabledProp = so.FindProperty("_weatherPaletteEnabled");
            if (enabledProp != null) enabledProp.boolValue = true;
            SetObj(so, "_baseMoodMaterial", baseMood);
            SetObj(so, "_calmMoodMaterial", calmMood);
            SetObj(so, "_stormMoodMaterial", stormMood);
            SetObj(so, "_fogMoodMaterial", fogMood);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // =====================================================================================
        //  HELPERS
        // =====================================================================================

        static FishSpeciesDef Fish(string id, string name, FishCategory cat, Rarity rarity,
                                   int value, float elasticity, float spawnWeight, float minKg, float maxKg)
        {
            return LoadOrCreate<FishSpeciesDef>($"{DataFish}/{name.Replace(" ", "")}.asset", f =>
            {
                f.Id = id; f.DisplayName = name; f.Category = cat; f.Rarity = rarity;
                f.RegionIds = new[] { CoveRegionId };
                f.AllowedGear = Gear.Handline | Gear.Longline;
                f.Seasons = SeasonMask.AllYear;
                f.MinTide = -10f; f.MaxTide = 10f; f.StartHour = 0f; f.EndHour = 24f;
                f.MinWeightKg = minKg; f.MaxWeightKg = maxKg;
                f.BaseValue = value; f.SupplyElasticity = elasticity; f.SpawnWeight = spawnWeight;
            });
        }

        // =====================================================================================
        //  THE HULL LADDER — stats, and where they come from
        // =====================================================================================
        //
        // TERMINAL SPEED IS DERIVED, NOT GUESSED. BoatController assembles thrust and drag like this:
        //
        //   thrust  = EnginePower · ForceFeelScale                    (ForceFeelScale = 0.01)
        //   hull    = v · ForwardDrag · ForceFeelScale                (drag along the hull, vs the water)
        //   damping = v · mass · Rigidbody2D.linearDamping            (mass = MassKg/100, damping = 0.2)
        //
        // At terminal, thrust == hull + damping, so:
        //
        //   v = (EnginePower · 0.01) / (ForwardDrag · 0.01 + (MassKg/100) · 0.2)
        //
        // The rigidbody's own linearDamping is the term a naive EnginePower/ForwardDrag ratio DROPS, and it
        // is not small — on the console it is well over half the total resistance. Ignore it and every hull
        // comes out fast. SkiffTerminalSpeedPlayTests measures each committed hull on real physics and holds
        // these numbers to the design targets, so this comment can't quietly rot.
        //
        //   Dory (rowed, both oars)  600·0.01 / (120·0.01 + 4·0.2)    = 3.00 m/s
        //   Punt                     650·0.01 / (140·0.01 + 7·0.2)    = 2.32 m/s
        //   Fishing skiff            550·0.01 / (130·0.01 + 4.5·0.2)  = 2.50 m/s
        //   Console skiff           1600·0.01 / (170·0.01 + 12·0.2)   = 3.90 m/s   (target 3.8–4.0)
        //   Sport skiff            1600·0.01 / (155·0.01 + 9.5·0.2)   = 4.64 m/s   (target 4.6)
        //   Sport skiff, twin      2000·0.01 / (155·0.01 + 10·0.2)    = 5.63 m/s   (target 5.6)
        //
        // These land inside BowSprayGrading's SpeedRef 1.7→6 frame, which the dory alone could never
        // exercise: the twin nearly maxes the spray sheet, which is what that art was drawn for.
        //
        // A NOTE ON THE TWIN, because the number looks wrong: two identical outboards do NOT get 2×
        // EnginePower. Drag here is LINEAR in v, so doubling thrust would exactly double terminal speed —
        // 9 m/s, past the spray sheet and past anything the owner asked for. Real hulls don't do that
        // (drag goes as v²). EnginePower is a design-unit tunable calibrated to a designed terminal speed,
        // not a Newton count, so the second engine is worth +25% (1600 → 2000). If the force model ever
        // goes quadratic, these are the numbers to re-derive.
        //
        // RudderAuthority scales with MASS, because AddTorque fights the rigidbody's inertia: the Punt's
        // 600 on m=7 is the tuned reference, so each hull takes roughly 600·(m/7) to answer the helm the
        // same way. Without that, the console (m=12) would feel like a barge on the same number.

        /// <summary>
        /// Tier-1 Punt stats (design/boats-and-navigation.md §1.1) + greybox-tuned propulsion. One place so
        /// the values live on the asset, never duplicated in C#. Seaworthiness "4 — Popple" maps to
        /// SeaState.Lively.
        ///
        /// <para><b>Her FEEL is deliberately untouched.</b> Unlike the pilotable-fleet hulls, the Punt is a
        /// real M1 boat the owner buys for ₲1800 and already knows how she handles, so EnginePower, the
        /// drags, the mass and the hold are left exactly as they were. What #211 changed is only what was
        /// WRONG: she had no picture, and she claimed a length her own art never drew.</para>
        ///
        /// <para><b>5.2 m, not 6 (this is not cosmetic).</b> The art director draws her at ~5.2 m and the
        /// slicer cuts her at that scale; the asset said 6. LengthMeters is not decoration — WakeGrading
        /// anchors the stern plume at LengthMeters·0.5, so at 6 she threw her wake ~40 cm astern of a transom
        /// that isn't there, and the length also feeds the wake/spray size term. Authored against the DRAWN
        /// hull. It very slightly reduces her wake grade: correct-to-art, and visible.</para>
        ///
        /// <para><b>Seakeeping, authored for the first time.</b> She predates those fields and has been
        /// sitting on the raw defaults (1 / 1 / 0 — the dory's reference read). Placed on the ladder by mass
        /// between the fishing skiff (450 kg → 1.1 / 0.95 / 0.05) and the sport skiff (950 kg → 1.8 / 0.75 /
        /// 0.3). The ART agrees independently, which is the check that matters: her rig's rollA is 4.2 against
        /// the dory's 5.0 and the sport's 3.8, so she must cork about LESS than the dory and MORE than the
        /// sport — and 0.85 sits exactly there.</para>
        /// </summary>
        static void ApplyPuntStats(BoatHullDef h)
        {
            h.Id = "boat.punt"; h.DisplayName = "The Punt";
            h.Propulsion = PropulsionType.Engine;   // buying the Punt swaps hand-rowing for an engine helm (P4)
            h.LengthMeters = 5.2f;                  // ART FACT: the drawn hull. Was 6 — see the note above.
            h.DraughtMeters = 0.5f; h.MassKg = 700f;
            h.HoldUnits = 14; h.CrewSlots = 1;
            h.EnginePower = 650f; h.RudderAuthority = 600f;   // her tuned feel — DO NOT retune, she is bought
            h.ForwardDrag = 140f; h.LateralDrag = 360f; h.WindExposure = 0.5f;
            h.MaxSafeSeaState = SeaState.Lively;
            h.SeakeepingMassFactor = 1.45f; h.SeakeepingLiveliness = 0.85f; h.SeakeepingDamping = 0.15f;
            h.CameraWorldHeightMeters = 17f;   // a bigger boat → the camera pulls back a touch on the upgrade
            ApplyDeckTray(h);                  // still a small boat → the fish tray (blue totes are M2 hulls)
        }

        /// <summary>
        /// THE PUNT, UPGRADED (<c>boat.punt_upgraded</c>) — the SAME 5.2 m tiller hull, wearing the kit's
        /// upgraded engine (domed cowl, gloss pan, red wrap stripe). Everything about the boat is the punt's;
        /// only the engine is bigger.
        ///
        /// <para><b>825 was MEASURED, not computed.</b> EnginePower is a design-unit tunable calibrated to a
        /// designed terminal speed, not a Newton count — the naive EnginePower/ForwardDrag ratio drops the
        /// rigidbody's own linearDamping, which here is HALF her resistance. Run to terminal on real physics
        /// (PilotableFleetPlayTests): basic 2.32 m/s → upgraded 2.89 m/s, +25%. She stays a workboat, well
        /// under the sport skiff's 4.64.</para>
        ///
        /// <para><b>No ShipwrightOffer, on purpose.</b> The owner called this "an upgrade"; a PURCHASABLE
        /// engine upgrade is real economy work (P4) he has not asked for, and building it here would be scope
        /// creep (rule 8). For now she is simply another rung on the dev picker's F cycle. Note this is a
        /// second HULL id rather than an upgrade slot on the punt — the M2 way to model this is an engine slot
        /// on one hull, and when that lands, this id is what gets retired (append-only: it can be orphaned,
        /// never reused for something else).</para>
        /// </summary>
        static void ApplyPuntUpgradedStats(BoatHullDef h)
        {
            ApplyPuntStats(h);                  // the SAME boat — only the engine differs
            h.Id = "boat.punt_upgraded"; h.DisplayName = "The Punt (Upgraded)";
            h.DraughtMeters = 0.55f;            // the heavier engine sits her down by the stern
            h.MassKg = 725f;                    // the upgraded block: ~15% more cowl, and the weight with it
            h.EnginePower = 825f;               // → 2.89 m/s measured (basic 2.32): +25%, still a workboat
            h.RudderAuthority = 620f;           // ≈ 600·(7.25/7): the helm keeps pace with the mass
            h.SeakeepingMassFactor = 1.5f;      // marginally more inertia against the sea
        }

        /// <summary>
        /// Refresh one pilotable hull on disk: re-apply its stats, then point its <c>Visual</c> at the
        /// BoatVisualDef of the given name (the asset <see cref="BoatVisualLibraryBuilder"/> writes). Art
        /// PATHS stay in that builder — this only binds the DEF, so "how the boat looks" remains data.
        /// A missing hull asset is skipped silently (it is created on the next run); a missing VISUAL warns
        /// loudly, because it is the one failure the owner would see as an invisible boat.
        /// </summary>
        static void ApplyFleetHull(string hullPath, System.Action<BoatHullDef> applyStats, string visualName)
        {
            var h = AssetDatabase.LoadAssetAtPath<BoatHullDef>(hullPath);
            if (h == null) return;

            applyStats(h);

            string visualPath = DataBoats + "/Visuals/" + visualName + ".asset";
            var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(visualPath);
            if (visual != null) h.Visual = visual;
            else
                Debug.LogWarning($"[GreyboxBuilder] {hullPath}: no BoatVisualDef at {visualPath}, so " +
                                 $"{h.Id} has NO PICTURE (its fallback Sprite is empty — it would sail " +
                                 "invisible). Run Hidden Harbours ▸ Art ▸ Build Boat Visual Defs, then " +
                                 "re-run this builder.");

            EditorUtility.SetDirty(h);
        }

        /// <summary>
        /// THE CONSOLE SKIFF (<c>boat.console_skiff</c>) — 7 m of workboat. The heavy, stiff, unglamorous
        /// one: it carries the most, shrugs off the most sea, and gets there last. 1200 kg and a
        /// SeakeepingLiveliness of 0.5 are what make it feel planted next to its glass sister.
        /// </summary>
        static void ApplyConsoleSkiffStats(BoatHullDef h)
        {
            h.Id = "boat.console_skiff"; h.DisplayName = "The Console Skiff";
            h.Propulsion = PropulsionType.Engine;
            h.LengthMeters = 7.0f;              // art fact: the 244 px hull cell at PPU 32, less its padding
            h.DraughtMeters = 0.55f; h.MassKg = 1200f;
            h.HoldUnits = 20; h.CrewSlots = 2;  // the workboat's whole point: it brings the catch home
            h.EnginePower = 1600f;              // → 3.90 m/s (see the ladder note above)
            h.RudderAuthority = 1000f;          // ≈ 600·(12/7): heavy hull, proportionate helm
            h.ForwardDrag = 170f; h.LateralDrag = 450f;   // Lat/Fwd 2.65 — tracks stiff, skids little
            h.WindExposure = 0.45f;             // heavy and low: the wind gets less of a grip than on the punt
            h.MaxSafeSeaState = SeaState.Rough; // far more seaworthy than the dory (Lively)
            h.SeakeepingMassFactor = 2.2f; h.SeakeepingLiveliness = 0.5f; h.SeakeepingDamping = 0.5f;
            h.CameraWorldHeightMeters = 18.5f;  // the 14@4.5 → 17@6 ladder, carried to 7 m
            // NO DeckContainer, deliberately: the FishTray is drawn screen-upright and does not sit in an
            // iso hull at most headings. The owner's standing decision is no code workarounds — the
            // 8-direction deck props are coming from his art director. Leave this null until they land.
        }

        /// <summary>
        /// THE SPORT SKIFF (<c>boat.sport_skiff</c>) — the console's glass sister on the same 7 m hull.
        /// Lighter, slipperier, livelier in a sea, and a good deal faster; a small hold, because she is
        /// built to run rather than to carry.
        /// </summary>
        static void ApplySportSkiffStats(BoatHullDef h)
        {
            h.Id = "boat.sport_skiff"; h.DisplayName = "The Sport Skiff";
            h.Propulsion = PropulsionType.Engine;
            h.LengthMeters = 7.0f; h.DraughtMeters = 0.5f; h.MassKg = 950f;
            h.HoldUnits = 8; h.CrewSlots = 2;
            h.EnginePower = 1600f;              // the SAME outboard as the console → 4.64 m/s on a lighter hull
            h.RudderAuthority = 850f;           // ≈ 600·(9.5/7)
            h.ForwardDrag = 155f; h.LateralDrag = 400f;   // Lat/Fwd 2.58 — a shade looser in a turn
            h.WindExposure = 0.5f;              // lighter, so the same breeze moves her more
            h.MaxSafeSeaState = SeaState.Rough;
            h.SeakeepingMassFactor = 1.8f; h.SeakeepingLiveliness = 0.75f; h.SeakeepingDamping = 0.3f;
            h.CameraWorldHeightMeters = 19f;    // same hull as the console, +0.5 m of look-ahead for the speed
        }

        /// <summary>
        /// THE SPORT SKIFF, TWIN (<c>boat.sport_skiff_twin</c>) — the sport hull with a second outboard on
        /// the transom. Same boat, same character: 50 kg heavier, a bit more thrust, and the fastest thing
        /// the owner can currently point at the horizon.
        /// </summary>
        static void ApplySportSkiffTwinStats(BoatHullDef h)
        {
            ApplySportSkiffStats(h);            // the SAME hull — only the engines differ
            h.Id = "boat.sport_skiff_twin"; h.DisplayName = "The Sport Skiff (Twin)";
            h.DraughtMeters = 0.55f;            // she sits a little deeper by the stern
            h.MassKg = 1000f;                   // the second engine
            h.EnginePower = 2000f;              // → 5.63 m/s (NOT 2×; see the ladder note above)
            h.RudderAuthority = 900f;           // ≈ 600·(10/7)
            h.SeakeepingMassFactor = 1.9f; h.SeakeepingLiveliness = 0.7f; h.SeakeepingDamping = 0.35f;
            h.CameraWorldHeightMeters = 19.5f;
        }

        /// <summary>
        /// THE FISHING SKIFF (<c>boat.fishing_skiff</c>) — the 8-direction fishing boat. This hull id has
        /// existed, ORPHANED, since #97's engine-helm experiment was reverted: nothing pointed at it and its
        /// stats were a copy of the dory's with the propulsion flipped. Rather than mint a sixth id for art
        /// that already has one, it is un-orphaned here — pointed at the new <c>visual.fishing_boat</c>
        /// compass and given stats of its own. The id is append-only and stable; a new id would have
        /// stranded this one forever and broken the PlayMode tests already keyed to it.
        ///
        /// <para>Sized from the ART, not from a guess: the <c>FishingBoat_*</c> files are 128×128 cells at
        /// PPU 32, so the drawn hull cannot exceed 4.0 m. (The old asset claimed 4.5 m — longer than its own
        /// picture.) That makes her the smallest powered boat on the ladder: a dory with an outboard on it,
        /// which is exactly what the art shows. She also predates the Seakeeping* fields, so they are
        /// authored here for the first time.</para>
        /// </summary>
        static void ApplyFishingSkiffStats(BoatHullDef h)
        {
            h.Id = "boat.fishing_skiff"; h.DisplayName = "The Fishing Skiff";
            h.Propulsion = PropulsionType.Engine;
            h.LengthMeters = 4.0f;              // art fact: the 128 px cell at PPU 32 is a hard ceiling
            h.DraughtMeters = 0.35f; h.MassKg = 450f;
            h.HoldUnits = 6; h.CrewSlots = 1;
            h.EnginePower = 550f;               // → 2.50 m/s: the slowest powered hull, quicker than the punt
            h.RudderAuthority = 400f;           // ≈ 600·(4.5/7)
            h.ForwardDrag = 130f; h.LateralDrag = 340f;
            h.WindExposure = 0.6f;              // small and light, like the dory
            h.MaxSafeSeaState = SeaState.Lively;   // a small open boat — no more seaworthy than the dory
            h.SeakeepingMassFactor = 1.1f; h.SeakeepingLiveliness = 0.95f; h.SeakeepingDamping = 0.05f;
            h.CameraWorldHeightMeters = 13.5f;  // the ladder, read back below the dory's 14 @ 4.5 m
            ApplyDeckTray(h);                   // she keeps her tray (the content validator requires it)
        }

        // The deck-container ladder (owner canon): every small hull the builders generate carries the
        // committed fish tray, anchored on the starboard quarter of the drawn deck. Values mirror the
        // committed Dory/FishingSkiff assets — one place for the builder-generated hulls.
        static void ApplyDeckTray(BoatHullDef h)
        {
            h.DeckContainer = AssetDatabase.LoadAssetAtPath<DeckContainerDef>(DataBoats + "/Containers/FishTray.asset");
            h.DeckContainerOffset = new Vector2(0.35f, -0.9f);
        }

        static T LoadOrCreate<T>(string path, System.Action<T> init = null) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            if (init != null) init(asset);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        static void SetRef(Component c, string field, Object value)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p != null) { p.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
            else Debug.LogWarning($"[GreyboxBuilder] {c.GetType().Name} has no field '{field}'.");
        }

        static void SetRefArray(Component c, string field, Object[] values)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[GreyboxBuilder] no array field '{field}'."); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetString(Component c, string field, string value)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p != null && p.propertyType == SerializedPropertyType.String)
            { p.stringValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        // Load a final art sprite if it has been imported; null if the project is still greybox-only.
        static Sprite LoadArtSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        // Imported art is sliced (spriteMode Multiple, one sub-sprite), so LoadAssetAtPath<Sprite> returns
        // null — fall back to the first sub-sprite. Null if the art isn't imported. (Mirrors GreywickBuilder.)
        static Sprite LoadSpriteAny(string path)
        {
            var direct = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (direct != null) return direct;
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                                 .OrderBy(s => SpriteIndex(s.name)).FirstOrDefault();
        }

        static int SpriteIndex(string spriteName)
        {
            int u = spriteName.LastIndexOf('_');
            return (u >= 0 && int.TryParse(spriteName.Substring(u + 1), out int n)) ? n : 0;
        }

        // The 1×1 white fallback square (created on demand; mirrors GreywickBuilder's).
        static Sprite MakeSquareSprite(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            var tex = new Texture2D(16, 16);
            var px = Enumerable.Repeat(Color.white, 16 * 16).ToArray();
            tex.SetPixels(px); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spritePixelsPerUnit = 32f;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // --- SerializedObject value setters (the persist-the-refs convention, scalar flavours) -------
        static void SetF(SerializedObject so, string field, float value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.floatValue = value;
            else Debug.LogWarning($"[GreyboxBuilder] no float field '{field}'.");
        }

        static void SetInt(SerializedObject so, string field, int value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.intValue = value;
            else Debug.LogWarning($"[GreyboxBuilder] no int field '{field}'.");
        }

        static void SetV2(SerializedObject so, string field, Vector2 value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.vector2Value = value;
            else Debug.LogWarning($"[GreyboxBuilder] no Vector2 field '{field}'.");
        }

        static void SetObj(SerializedObject so, string field, Object value)
        {
            var p = so.FindProperty(field);
            if (p != null && p.propertyType == SerializedPropertyType.ObjectReference) p.objectReferenceValue = value;
            else Debug.LogWarning($"[GreyboxBuilder] no object-reference field '{field}'.");
        }

        // A single dock-piling collider (boat bumper). No sprite — the visible post is the owner's decor.
        static void MakePilingCollider(Transform parent, Vector2 pos)
        {
            var go = new GameObject("Piling");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.3f;   // slim piling; leaves a navigable channel between the two rows of posts
        }

        static void RegisterScene(string path)
        {
            var list = EditorBuildSettings.scenes.ToList();
            if (list.Any(s => s.path == path)) return;
            // Append (don't insert at 0) — St Peters is the start scene and owns build-index-0.
            list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        static void EnsureFolders()
        {
            foreach (var f in new[] { DataConfig, DataBoats, DataFish, DataShip, DataRegions, ArtSprites, Scenes })
            {
                if (AssetDatabase.IsValidFolder(f)) continue;
                var parent = Path.GetDirectoryName(f).Replace('\\', '/');
                var leaf = Path.GetFileName(f);
                if (!AssetDatabase.IsValidFolder(parent)) EnsureFolders(); // parents exist already in scaffold
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }
    }
}
#endif
