#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;         // BoatHullDef / PropulsionType (the hull defs authored as data)
using HiddenHarbours.Fishing;       // FishSpeciesDef / Gear (the cove's fishing-ground species)
using HiddenHarbours.Economy;       // Market / FishBuyer / WharfSellPoint / Shipwright (the cove's services)
using HiddenHarbours.World;         // RegionDef / RegionSceneLoader / RegionPassage
using HiddenHarbours.App;           // RegionAnchor / PersistentHoldProxy / PersistentWalletProxy
using UnityEngine.Rendering.Universal; // PixelPerfectCamera
using HiddenHarbours.Art.Editor;   // VS-23 locked Pixel-Perfect camera convention

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// One-click <b>Coddle Cove</b> — the player's HOME HARBOUR as a PLAIN region scene (#66 demotion).
    /// Menu: Hidden Harbours ▸ Build Greybox Scene. Re-runnable (idempotent on the assets).
    ///
    /// <para><b>Demoted from the start (#66 / StPetersBuilder's flag #1).</b> The decided opening arc is
    /// St Peters → Greywick → buy + repair the dory → SAIL HOME to Coddle Cove. The cove USED to be the
    /// start, so this builder used to author its OWN persistent core (GameRoot / on-foot player / camera /
    /// ControlSwitcher / fishing rig / travel coordinator). With St Peters as the start, sailing to the cove
    /// would DUPLICATE that rig. So the cove is now a PLAIN region exactly like <see cref="GreywickBuilder"/>:
    /// it authors only the region's own content (water / island / wharf / cottage / Fish Buyer + Shipwright /
    /// decor) plus a <see cref="RegionAnchor"/> (arrival / dock / disembark). The persistent rig is carried in
    /// from the START scene (St Peters) via the <see cref="RegionTravelCoordinator"/> and BINDS on arrival to
    /// this anchor — the same way Greywick binds. The cove stays the home base; it's just REACHED by travel
    /// now, not the start.</para>
    ///
    /// <para>Like Greywick, this scene carries its own Main Camera + AudioListener so it can be opened and
    /// reviewed standalone; the coordinator silences them on arrival (the persistent core owns the live
    /// camera). The wharf's hold/wallet live in the persistent core (a different scene), so the Fish Buyer /
    /// Shipwright resolve them through scene-local <see cref="PersistentHoldProxy"/> / <see cref="PersistentWalletProxy"/>
    /// shims, which the coordinator binds to the real hold on arrival (the Greywick pattern).</para>
    ///
    /// <para>This is a dev convenience, not shipping content — real scenes are authored by world-content
    /// (backlog VS-02; and see ADR 0011 for the committed hand-authored-scene plan).</para>
    ///
    /// <para>FLAG world-content (follow-up, not this PR): the cove home base could later host its OWN NPCs
    /// (a returning Aunt Ginny, etc.). They were dropped from the cove in this demotion because their
    /// interaction driver (<c>WorldInteractor</c>) needs the persistent on-foot player transform, which an
    /// additively-loaded region can't serialize-reference — it needs a bind-on-arrival shim like the wharf
    /// hold/wallet proxies. The inheritance OPENING (Ginny's intro, Ned's logbook, the one-loop onboarding)
    /// now belongs to the START scene (St Peters), so it must not re-trigger on arriving home at the cove.</para>
    /// </summary>
    public static class GreyboxBuilder
    {
        const string DataConfig = "Assets/_Project/Data/Config";
        const string DataBoats  = "Assets/_Project/Data/Boats";
        const string DataFish   = "Assets/_Project/Data/Fish";
        const string DataShip   = "Assets/_Project/Data/Shipwright";
        const string DataRegions= "Assets/_Project/Data/Regions";        // VS-22 region defs (cove + Greywick)
        const string ArtSprites = "Assets/_Project/Art/Sprites";
        const string ArtTrees   = "Assets/_Project/Art/Sprites/Environment/Trees"; // imported tree decor pack (TreeNN.png)
        const string ArtPunt    = "Assets/_Project/Art/Boats/Punt.png";          // tier-1 swap sprite (VS-16) — kept on the hull asset
        const string ArtSea     = "Assets/_Project/Art/Tilesets/Water/SeaTile.png"; // final tile (VS-24)
        const string ArtGrass    = "Assets/_Project/Art/Tilesets/Grass.png";              // island ground
        const string ArtSand     = "Assets/_Project/Art/Tilesets/Sand.png";               // beach border
        const string ArtCottage  = "Assets/_Project/Art/Sprites/Buildings/Cottage.png";   // home on the island
        const string ArtWharfDeck= "Assets/_Project/Art/Tilesets/WharfDeck.png";          // dock planks
        const string ArtWharfPost= "Assets/_Project/Art/Sprites/WharfPost.png";           // dock pilings
        const string Scenes     = "Assets/_Project/Scenes";
        const string SceneName  = "Greybox";
        const string ScenePath  = Scenes + "/" + SceneName + ".unity";

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

        [MenuItem("Hidden Harbours/Build Greybox Scene")]
        public static void Build()
        {
            EnsureFolders();

            // --- DATA ASSETS ---------------------------------------------------------------
            // The hull defs (Dory + Punt) + the Punt offer + the region defs live as data. The cove is no
            // longer the start, so it doesn't stand up the boat rig — but it still authors/keeps these stable
            // assets (first run wins; StPeters/Greywick author the canonical versions under the same ids).
            var config = LoadOrCreate<GameConfig>(DataConfig + "/GameConfig.asset");

            var dory = LoadOrCreate<BoatHullDef>(DataBoats + "/Dory.asset", h =>
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
            // Authored as data, never hardcoded in gameplay C#.
            var punt = LoadOrCreate<BoatHullDef>(DataBoats + "/Punt.asset", ApplyPuntStats);

            // Regions (VS-22 travel): this cove + Port Greywick, as data, for the loader/passage. Created
            // here if absent and authored by the other builders under the same stable ids (first run wins).
            var coveRegion = LoadOrCreate<RegionDef>(DataRegions + "/CoddleCove.asset", r =>
            {
                r.Id = "region.coddle_cove"; r.DisplayName = "Coddle Cove"; r.SceneName = SceneName;
                r.IsDeepHarbour = false; r.HarbourDepthMeters = 2f;
                r.TideMeanLevel = 0f; r.TideAmplitude = 1.6f; r.TidePhaseHours = 0f;
                r.Description = "Your home harbour — the sheltered greybox cove.";
            });
            var greywickRegion = LoadOrCreate<RegionDef>(DataRegions + "/PortGreywick.asset", r =>
            {
                r.Id = "region.port_greywick"; r.DisplayName = "Port Greywick"; r.SceneName = "Greywick";
                r.IsDeepHarbour = true; r.HarbourDepthMeters = 6f;
                r.TideMeanLevel = 0f; r.TideAmplitude = 0.8f; r.TidePhaseHours = 2f;
                r.Description = "The market town: a deep, sheltered harbour where the coast's business gets done.";
            });

            // The cove's fishing ground species (authored as data; the persistent FishingController carried in
            // from the start reads its region species by id — these are kept stable for the dory's catch at the
            // cove). Created here if absent; no scene ref needed now the boat rig is carried in, so we just
            // ensure the assets exist (data-driven, CLAUDE.md rule 2).
            Fish("fish.atlantic_cod", "Atlantic Cod", FishCategory.InshoreGroundfish, Rarity.Common,    14, 0.20f, 1.0f, 2f, 12f);
            Fish("fish.haddock",      "Haddock",      FishCategory.InshoreGroundfish, Rarity.Common,    16, 0.20f, 0.9f, 1f, 6f);
            Fish("fish.mackerel",     "Mackerel",     FishCategory.Pelagic,           Rarity.Uncommon,  10, 0.35f, 0.8f, 0.3f, 1.5f);
            Fish("fish.lobster",      "American Lobster", FishCategory.Shellfish,     Rarity.Prize,     28, 0.35f, 0.4f, 0.4f, 4f);

            // --- SCENE ---------------------------------------------------------------------
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera (standalone-viewable; the coordinator silences it on arrival — the persistent core owns
            // the live camera). Mirrors Greywick's locked pixel-perfect, on-foot landscape framing so the cove
            // reads at the same scale when reviewed standalone.
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = CameraFollow.OrthoSizeForWorldHeight(CameraFollow.OnFootWorldHeightMeters);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.13f, 0.18f);
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();
            // VS-23: lock the Pixel-Perfect camera (PPU 32) and override its reference to a 16:9 LANDSCAPE
            // reference (PC-first, ADR 0005) so the standalone render is pixel-perfect.
            ArtCameraSetup.ConfigurePixelPerfect(camGo);
            var ppc = camGo.GetComponent<PixelPerfectCamera>();
            if (ppc != null)
            {
                CameraFollow.ReferenceResolutionForWorldHeight(CameraFollow.OnFootWorldHeightMeters, out int refW, out int refH);
                ppc.refResolutionX = refW;
                ppc.refResolutionY = refH;
                EditorUtility.SetDirty(ppc);
            }

            // Water backdrop. Use the final tiling sea tile if it's been imported (the placeholder→final swap,
            // VS-24); otherwise fall back to a flat slate-blue square so the greybox still builds before art.
            var waterSprite = MakeSquareSprite(ArtSprites + "/Square.png");
            var seaTile = LoadArtSprite(ArtSea);
            var water = new GameObject("Water");
            var wsr = water.AddComponent<SpriteRenderer>();
            wsr.sortingOrder = -10;
            if (seaTile != null)
            {
                wsr.sprite = seaTile;
                wsr.drawMode = SpriteDrawMode.Tiled;     // repeat the 2 m tile across the cove
                wsr.size = new Vector2(140f, 140f);       // metres of open water
                water.transform.localScale = Vector3.one;
            }
            else
            {
                wsr.sprite = waterSprite; wsr.color = new Color(0.17f, 0.30f, 0.38f);
                water.transform.localScale = new Vector3(120f, 120f, 1f); // 0.5m sprite → big sea
            }

            // Scatter markers so motion reads on the open water (a flat sea looks static otherwise).
            var markerRng = new System.Random(7);
            var markers = new GameObject("SeaMarkers");
            for (int i = 0; i < 40; i++)
            {
                var m = new GameObject("Marker");
                m.transform.SetParent(markers.transform);
                m.transform.position = new Vector3(
                    (float)(markerRng.NextDouble() * 100 - 50),
                    (float)(markerRng.NextDouble() * 100 - 50), 0f);
                var msr = m.AddComponent<SpriteRenderer>();
                msr.sprite = waterSprite;
                msr.color = new Color(0.31f, 0.44f, 0.50f);
                msr.sortingOrder = -5;
                m.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
            }

            // Reload data assets from disk before wiring. An intervening AssetDatabase import (the sprite
            // SaveAndReimport above) can invalidate the in-memory references created earlier. Reloading
            // guarantees valid refs so nothing serializes into the scene as "None".
            config = AssetDatabase.LoadAssetAtPath<GameConfig>(DataConfig + "/GameConfig.asset");
            dory = AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/Dory.asset");
            if (dory != null)   // gentle greybox tuning so the dory is slow enough to control on screen
            {
                dory.Propulsion = PropulsionType.Oars;   // the dory is hand-rowed (the oar-tunable defaults ride the Def)
                dory.EnginePower = 500f; dory.ForwardDrag = 120f; dory.LateralDrag = 320f; dory.WindExposure = 0.6f;
                dory.CameraWorldHeightMeters = 14f;
                EditorUtility.SetDirty(dory);
            }
            // Reload + re-apply the Punt stats (idempotent on re-runs, like the Dory above) and attach its
            // hull sprite so the carried OwnedFleet has something to swap the renderer to on the grant.
            punt = AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/Punt.asset");
            if (punt != null)
            {
                ApplyPuntStats(punt);
                punt.Sprite = LoadArtSprite(ArtPunt);
                EditorUtility.SetDirty(punt);
            }
            coveRegion     = AssetDatabase.LoadAssetAtPath<RegionDef>(DataRegions + "/CoddleCove.asset");
            greywickRegion = AssetDatabase.LoadAssetAtPath<RegionDef>(DataRegions + "/PortGreywick.asset");

            // --- HOME ISLAND (the land the cove sits on) -----------------------------------
            // A small island the player stands on once disembarked: tiled sand beach + grass, the cottage, a
            // dock into the water with pilings. A closed shore-edge collider keeps the player out of open
            // water. Tiles use the same tiled-SpriteRenderer approach as the sea backdrop; all art is imported.
            MakeTiledGround("Beach",  LoadArtSprite(ArtSand),      new Vector2(0f, 2f),    new Vector2(20f, 14f), -8, waterSprite, new Color(0.86f, 0.79f, 0.55f));
            MakeTiledGround("Ground", LoadArtSprite(ArtGrass),     new Vector2(0f, 2.5f),  new Vector2(17f, 11f), -7, waterSprite, new Color(0.38f, 0.58f, 0.32f));
            MakeTiledGround("Dock",   LoadArtSprite(ArtWharfDeck), new Vector2(0f, -8.5f), new Vector2(3f, 7f),   -6, waterSprite, new Color(0.55f, 0.40f, 0.24f));

            // Dock pilings down both sides.
            var postSprite = LoadArtSprite(ArtWharfPost);
            for (int i = 0; i < 3; i++)
            {
                float py = -6f - i * 3f; // -6, -9, -12
                MakePost(postSprite, new Vector2(-1.5f, py), waterSprite);
                MakePost(postSprite, new Vector2( 1.5f, py), waterSprite);
            }

            // The cottage (home) on the grass.
            var cottageGo = new GameObject("Cottage");
            cottageGo.transform.position = new Vector3(-4.5f, 5f, 0f);
            var cottageSr = cottageGo.AddComponent<SpriteRenderer>();
            cottageSr.sortingOrder = 2;
            var cottageSprite = LoadArtSprite(ArtCottage);
            if (cottageSprite != null) { cottageSr.sprite = cottageSprite; cottageGo.transform.localScale = Vector3.one; }
            else { cottageSr.sprite = waterSprite; cottageSr.color = new Color(0.70f, 0.50f, 0.40f); cottageGo.transform.localScale = new Vector3(6f, 6f, 1f); }

            // Shore edge: a closed collider fence tracing the beach + dock so the player can roam the island
            // and walk out the dock, but can't wander into open water (P5 cozy bounds). The boat bumps it too.
            var shore = new GameObject("ShoreEdge");
            var edge = shore.AddComponent<EdgeCollider2D>();
            edge.points = new[]
            {
                new Vector2(-10f, 9f), new Vector2(10f, 9f), new Vector2(10f, -5f),
                new Vector2(1.5f, -5f), new Vector2(1.5f, -12f), new Vector2(-1.5f, -12f),
                new Vector2(-1.5f, -5f), new Vector2(-10f, -5f), new Vector2(-10f, 9f),
            };

            // --- FISHING SPOT (greybox flavour) --------------------------------------------
            // A visible fishing-spot marker so there IS a spot in the cove water beside the dock. The carried
            // FishingController casts from the persistent dory (Space); this is just the look (no logic ref).
            var fishingSpot = new GameObject("FishingSpot");
            fishingSpot.transform.position = new Vector3(5f, -10f, 0f); // in the water beside the dock
            var spotSr = fishingSpot.AddComponent<SpriteRenderer>();
            spotSr.sprite = waterSprite;
            spotSr.color = new Color(0.30f, 0.58f, 0.66f, 0.7f);
            spotSr.sortingOrder = -4;
            fishingSpot.transform.localScale = new Vector3(1.5f, 1.5f, 1f);

            // --- WHARF (Fish Buyer + Shipwright; resolved through the persistent proxies) ---
            // The cove keeps its Fish Buyer (sell your catch) and the Shipwright (buy the Punt). The player's
            // hold + wallet live in the PERSISTENT core (a different scene), so they can't be serialize-
            // referenced here — the wharf resolves them through scene-local proxies (the Greywick pattern):
            // PersistentHoldProxy forwards to the dory's ShipHold (the coordinator binds it on arrival), and
            // PersistentWalletProxy always forwards to GameServices.Wallet. So you sell + buy at the cove
            // against the same hold + coin you sailed in with.
            var providersGo = new GameObject("PersistentProviders");
            providersGo.AddComponent<PersistentHoldProxy>();
            providersGo.AddComponent<PersistentWalletProxy>();

            var wharf = new GameObject("Wharf");
            var market = wharf.AddComponent<Market>();
            var buyer = wharf.AddComponent<FishBuyer>();
            var sellPoint = wharf.AddComponent<WharfSellPoint>();
            wharf.AddComponent<DevSellInput>();            // RequireComponent(WharfSellPoint) — present (greybox B to sell)
            SetRef(market, "_config", config);
            SetRef(buyer, "_market", market);
            SetRef(sellPoint, "_buyer", buyer);
            SetRef(sellPoint, "_holdProvider", providersGo);
            SetRef(sellPoint, "_walletProvider", providersGo);

            // Shipwright buy flow (VS-16): P buys the Punt with the persistent wallet; on success the
            // Shipwright raises BoatPurchased (the carried OwnedFleet swaps the boat). Economy side only — the
            // price lives in a ShipwrightOffer asset, and we reference the boat by id, never the Boats module.
            var puntOffer = LoadOrCreate<ShipwrightOffer>(DataShip + "/PuntOffer.asset", o =>
            {
                o.BoatId = "boat.punt"; o.DisplayName = "The Punt"; o.Price = 1800;
            });
            puntOffer = AssetDatabase.LoadAssetAtPath<ShipwrightOffer>(DataShip + "/PuntOffer.asset");
            var shipwrightGo = new GameObject("Shipwright");
            var shipwright = shipwrightGo.AddComponent<Shipwright>();
            shipwrightGo.AddComponent<DevBuyInput>();      // RequireComponent(Shipwright) — present (greybox P to buy)
            SetRef(shipwright, "_offer", puntOffer);
            SetRef(shipwright, "_walletProvider", providersGo);

            // --- REGION SCENE-LOAD PATH ----------------------------------------------------
            // The persistent travel rig (loader + coordinator) is carried in from the start scene. This scene
            // places its OWN RegionSceneLoader (so it's reviewable standalone and the additive loader can
            // re-activate it) and the Cove→Greywick passage. On arrival the carried coordinator drives travel.
            var loaderGo = new GameObject("RegionSceneLoader");
            var loader = loaderGo.AddComponent<RegionSceneLoader>();
            SetRefArray(loader, "_regions", new Object[] { coveRegion, greywickRegion });
            SetString(loader, "_currentSceneName", SceneName);   // this scene; explicit (don't rely on Awake vs DDOL order)

            // Cove→Greywick passage: SAIL WEST to cross — Port Greywick lies west of the cove (canon map). A
            // wide, forgiving band down the WEST edge of the open water, reachable by sailing west out of the
            // dock; sailing west into it crosses to Greywick (where you arrive still HEADING WEST — the hop
            // preserves the boat's heading). The matching return passage is on Greywick's EAST edge.
            var toGreywickGo = new GameObject("PassageToPortGreywick");
            toGreywickGo.transform.position = ToGreywickPassagePos;
            var toGreywickTrigger = toGreywickGo.AddComponent<BoxCollider2D>();
            toGreywickTrigger.isTrigger = true;
            toGreywickTrigger.size = new Vector2(3f, 28f);   // a tall west-edge band (forgiving, wide)
            var toGreywickPassage = toGreywickGo.AddComponent<RegionPassage>();
            SetRef(toGreywickPassage, "_target", greywickRegion);
            SetRef(toGreywickPassage, "_loader", loader);

            // --- REGION ANCHOR (the persistent rig binds here on arrival) -------------------
            // The cove's board/dock geometry as a RegionAnchor (mirrors Greywick + St Peters). When you sail
            // home from Greywick (which lies WEST), the carried RegionTravelCoordinator reads this anchor and:
            // parks the boat at the arrival point (just WEST of the dock, heading east), re-points the
            // persistent ControlSwitcher's dock to the cove dock zone + disembark spot, so E disembarks the
            // moment you're home. The arrival sits within CoveDockZoneRadius of the dock zone — the proven
            // pure-distance disembark geometry (don't regress #52). NO persistent core is authored here.
            var dockZone = new GameObject("CoveDockZone");
            dockZone.transform.position = CoveDockZonePos;          // the dock head / mooring
            var disembarkPoint = new GameObject("CoveDisembark");
            disembarkPoint.transform.position = CoveDisembarkPos;   // on the dock planks
            var coveArrival = new GameObject("CoveArrival");
            coveArrival.transform.position = CoveArrivalPos;        // just WEST of the dock head (arrive from the west)
            var coveAnchor = new GameObject("CoveRegionAnchor").AddComponent<RegionAnchor>();
            coveAnchor.Configure("region.coddle_cove", coveArrival.transform, dockZone.transform, disembarkPoint.transform);

            // --- TREE DECOR (greybox dressing; world-content) ------------------------------
            // A tasteful, sparse-to-moderate scatter of cold-coast trees along the LAND/coast edges of the
            // island. NEVER in open water, on the dock/wharf, on the paths, in the dock/disembark zones, or
            // overlapping the cottage. Data-driven (CoveTrees) so counts/positions tweak freely; sortingOrder
            // is derived from base Y so trees further north sort behind.
            PlaceTrees("Cove", CoveTrees, waterSprite);

            // --- SAVE & REGISTER -----------------------------------------------------------
            // The cove is NO LONGER the start, so it must NOT force itself to build-index-0 (St Peters owns
            // index 0). Register it if absent, but do not reorder the list (StPetersBuilder pins index 0).
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterScene(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[GreyboxBuilder] Built Greybox.unity — Coddle Cove as a PLAIN region (#66 demotion): " +
                      "no persistent core (St Peters is the start), just the cove's water/island/wharf/cottage/" +
                      "Fish Buyer + Shipwright (resolved through the persistent hold/wallet proxies) + decor, " +
                      "plus a RegionAnchor (arrival WEST of the dock, dock zone, disembark) so the persistent " +
                      "rig carried from St Peters binds on arrival — exactly like Greywick. You SAIL WEST to " +
                      "cross to Greywick; you arrive HOME from the WEST. RE-RUN 'Build St Peters Scene', 'Build " +
                      "Greywick Scene', AND this, then test the sail-home from the START scene (StPeters.unity).");
            EditorUtility.DisplayDialog("Hidden Harbours",
                "Coddle Cove built as a PLAIN region (demoted from the start, #66).\n\nThe cove no longer " +
                "authors the persistent core — St Peters is the start, and the player/boat/camera are carried " +
                "in and bind to the cove's RegionAnchor on arrival (the Greywick pattern). The Fish Buyer + " +
                "Shipwright sell/buy against the persistent hold + wallet via proxies.\n\nTo test the full " +
                "sail-home: open StPeters.unity (the start), repair the dory at Greywick, sail home to the " +
                "cove, and confirm there's ONE player/camera (not duplicated).\n\nRE-RUN 'Build St Peters " +
                "Scene', 'Build Greywick Scene', and this builder, then re-test.", "Fair winds");
        }

        // ---- helpers ------------------------------------------------------------------------
        static FishSpeciesDef Fish(string id, string name, FishCategory cat, Rarity rarity,
                                   int value, float elasticity, float spawnWeight, float minKg, float maxKg)
        {
            return LoadOrCreate<FishSpeciesDef>($"{DataFish}/{name.Replace(" ", "")}.asset", f =>
            {
                f.Id = id; f.DisplayName = name; f.Category = cat; f.Rarity = rarity;
                f.RegionIds = new[] { "region.coddle_cove" };
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
            // Reload so callers wire the PERSISTED asset (a freshly-created instance may not serialize into
            // the scene reliably). This is what fixes "No GameConfig assigned".
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

        // Like LoadArtSprite but also handles single-frame sliced sheets (spriteMode Multiple): some imported
        // art carries one sub-sprite, so LoadAssetAtPath<Sprite> returns null and we fall back to the first
        // sub-sprite. Null if absent. (imported-art-spritemode-multiple)
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

        // A tiled ground/dock patch (mirrors the sea backdrop's tiled SpriteRenderer). Falls back to a tinted
        // square if the tile art isn't imported, so the greybox still builds.
        static void MakeTiledGround(string name, Sprite sprite, Vector2 center, Vector2 size, int order,
                                    Sprite fallback, Color fallbackColor)
        {
            var go = new GameObject(name);
            go.transform.position = new Vector3(center.x, center.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            if (sprite != null)
            {
                sr.sprite = sprite;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = size;
            }
            else
            {
                sr.sprite = fallback;
                sr.color = fallbackColor;
                go.transform.localScale = new Vector3(size.x * 2f, size.y * 2f, 1f); // 0.5 m fallback → metres
            }
        }

        // A single dock piling. A small circular collider so the boat bumps the pilings (cozy, no damage)
        // when nudging into the slip, on top of the shore-edge fence that bounds the island.
        static void MakePost(Sprite sprite, Vector2 pos, Sprite fallback)
        {
            var go = new GameObject("WharfPost");
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 3;
            if (sprite != null) { sr.sprite = sprite; go.transform.localScale = Vector3.one; }
            else { sr.sprite = fallback; sr.color = new Color(0.45f, 0.32f, 0.20f); go.transform.localScale = new Vector3(0.5f, 1.5f, 1f); }
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.3f;   // slim piling; leaves a navigable channel between the two rows of posts
        }

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
            imp.spritePixelsPerUnit = 32f;        // canon PPU
            imp.filterMode = FilterMode.Point;     // crisp pixels
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void RegisterScene(string path)
        {
            var list = EditorBuildSettings.scenes.ToList();
            if (list.Any(s => s.path == path)) return;
            // Append (don't insert at 0) — St Peters is the start scene and owns build-index-0.
            list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ---- tree decor (greybox dressing) ----------------------------------------------------------
        // One placed tree: world position (the trunk base, since the sprite pivot is BottomCenter) + the
        // imported variety file ("TreeNN"). Kept as a plain struct so placement is a tweakable data list.
        struct TreeSpec
        {
            public float X, Y;
            public string Variety;   // "TreeNN" → Art/Sprites/Environment/Trees/TreeNN.png
            public TreeSpec(float x, float y, string variety) { X = x; Y = y; Variety = variety; }
        }

        // COLD NORTH ATLANTIC coast scatter for the cove. Tasteful & sparse-to-moderate, hugging the LAND
        // edges only — a back (north) treeline behind the cottage/grass (y≈7–9, inside the shore fence top at
        // y=9) and a few down the east & west grass/beach margins. NONE in the open water, on the dock
        // (x∈[-1.5,1.5], y≤-5) or its zones, on the paths, or over the cottage (≈x[-7,-2] y[2,8]). Varieties:
        // green broadleaf (Tree01/05/06/08/18/21/34/35), pine (Tree02/22) and birch (Tree25).
        static readonly TreeSpec[] CoveTrees =
        {
            // Back treeline along the north edge (behind the cottage + grass), left → right.
            new TreeSpec(-9.0f, 8.4f, "Tree02"),   // pine, far NW corner
            new TreeSpec(-7.2f, 8.7f, "Tree25"),   // birch
            new TreeSpec(-3.4f, 8.6f, "Tree01"),   // broadleaf (clear of the cottage top, x≈-2)
            new TreeSpec(-1.0f, 8.8f, "Tree22"),   // pine
            new TreeSpec( 1.6f, 8.5f, "Tree05"),   // broadleaf
            new TreeSpec( 4.0f, 8.7f, "Tree06"),   // broadleaf
            new TreeSpec( 6.8f, 8.4f, "Tree02"),   // pine, far NE corner
            // West grass/beach margin (clear of the cottage at x≈-4.5).
            new TreeSpec(-9.2f, 6.0f, "Tree08"),   // broadleaf
            new TreeSpec(-8.8f, 1.0f, "Tree25"),   // birch, lower west margin
            new TreeSpec(-7.6f, -1.8f, "Tree18"),  // broadleaf, SW beach band (north of the channel)
            // East grass/beach margin.
            new TreeSpec( 7.0f, 6.2f, "Tree21"),   // broadleaf
            new TreeSpec( 8.0f, 2.4f, "Tree02"),   // pine, mid-east margin
            new TreeSpec( 6.6f, -1.6f, "Tree34"),  // broadleaf, SE beach band (east of the dock)
            new TreeSpec( 4.4f, -2.6f, "Tree35"),  // broadleaf, lower SE (well east of the dock x=1.5)
        };

        // Instance the tree decor under a single "Decor/Trees" parent. sortingOrder is DERIVED FROM the tree's
        // base Y (BottomCenter pivot) so trees further "back"/north (higher Y) render behind ones in front.
        // Loads each variety via LoadSpriteAny (the pack is Sprite Mode Multiple → one TreeNN_0 sub-sprite).
        // Falls back to a tinted square so the greybox still builds before the art is imported.
        static void PlaceTrees(string sceneLabel, TreeSpec[] specs, Sprite fallback)
        {
            var decor = new GameObject("Decor");
            var trees = new GameObject("Trees");
            trees.transform.SetParent(decor.transform, false);
            int placed = 0;
            foreach (var t in specs)
            {
                var go = new GameObject(t.Variety);
                go.transform.SetParent(trees.transform, false);
                go.transform.position = new Vector3(t.X, t.Y, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = Mathf.RoundToInt(-t.Y * 2f);
                var sprite = LoadSpriteAny($"{ArtTrees}/{t.Variety}.png");
                if (sprite != null) { sr.sprite = sprite; go.transform.localScale = Vector3.one; }
                else { sr.sprite = fallback; sr.color = new Color(0.24f, 0.40f, 0.26f); go.transform.localScale = new Vector3(1.6f, 3.2f, 1f); }
                placed++;
            }
            Debug.Log($"[GreyboxBuilder] Placed {placed} decor trees in {sceneLabel} (under Decor/Trees).");
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
