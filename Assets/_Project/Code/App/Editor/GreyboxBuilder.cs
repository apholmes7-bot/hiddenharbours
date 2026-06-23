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
    /// cottage sprite, the hardcoded tree scatter) are RETIRED — the owner's painting replaces them.</item>
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
            // ADR 0011 guard: a from-zero build wipes the owner's painted/decor layer. If a committed cove
            // already exists, make the owner confirm — Refresh Cove Logic is the safe path once it's painted.
            bool sceneExists = File.Exists(ScenePath);
            if (sceneExists && !EditorUtility.DisplayDialog(
                    "Hidden Harbours — full rebuild will WIPE hand-authored visuals",
                    "'Build Greybox Scene' rebuilds Coddle Cove FROM ZERO. It will DISCARD any terrain you've " +
                    "painted and any decor you've dropped (the whole VISUAL layer).\n\nIf you only want to " +
                    "update the gameplay logic and KEEP your painting, cancel and run " +
                    "'Hidden Harbours ▸ Refresh Cove Logic' instead.\n\nRebuild from zero anyway?",
                    "Rebuild from zero (lose painting)", "Cancel"))
            {
                Debug.Log("[GreyboxBuilder] Full rebuild cancelled — run 'Refresh Cove Logic' to update logic " +
                          "without touching the painted visual layer (ADR 0011).");
                return;
            }

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
                      "passage, shore/piling colliders, fishing spot, standalone camera). NO placeholder " +
                      "visuals — PAINT the terrain + drop decor OUTSIDE the --LOGIC-- root, then File ▸ Save. " +
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
            // the dock, but can't wander into open water (P5 cozy bounds). The boat bumps it too. The owner
            // paints the visible coastline to MATCH this fence (or we refine the geometry post-pilot — ADR 0011
            // open question). The fence is LOGIC; the look is painted.
            var shore = new GameObject("ShoreEdge");
            shore.transform.SetParent(root, false);
            var edge = shore.AddComponent<EdgeCollider2D>();
            edge.points = new[]
            {
                new Vector2(-10f, 9f), new Vector2(10f, 9f), new Vector2(10f, -5f),
                new Vector2(1.5f, -5f), new Vector2(1.5f, -12f), new Vector2(-1.5f, -12f),
                new Vector2(-1.5f, -5f), new Vector2(-10f, -5f), new Vector2(-10f, 9f),
            };

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
                EditorUtility.SetDirty(dory);
            }
            var punt = AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/Punt.asset");
            if (punt != null)
            {
                ApplyPuntStats(punt);
                punt.Sprite = LoadArtSprite(ArtPunt);
                EditorUtility.SetDirty(punt);
            }

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

        // Tier-1 Punt stats (design/boats-and-navigation.md §1.1) + greybox-tuned propulsion. One place so the
        // values live on the asset, never duplicated in C#. Seaworthiness "4 — Popple" maps to SeaState.Lively.
        static void ApplyPuntStats(BoatHullDef h)
        {
            h.Id = "boat.punt"; h.DisplayName = "The Punt";
            h.Propulsion = PropulsionType.Engine;   // buying the Punt swaps hand-rowing for an engine helm (P4)
            h.LengthMeters = 6.0f; h.DraughtMeters = 0.5f; h.MassKg = 700f;
            h.HoldUnits = 14; h.CrewSlots = 1;
            h.EnginePower = 650f; h.RudderAuthority = 600f;
            h.ForwardDrag = 140f; h.LateralDrag = 360f; h.WindExposure = 0.5f;
            h.MaxSafeSeaState = SeaState.Lively;
            h.CameraWorldHeightMeters = 17f;   // a bigger boat → the camera pulls back a touch on the upgrade
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
