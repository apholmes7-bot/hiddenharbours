#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.World;
using HiddenHarbours.Art.Editor;        // VS-23 locked Pixel-Perfect camera convention
using UnityEngine.Rendering.Universal;   // PixelPerfectCamera

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// One-click <b>Port Greywick</b> (VS-22): the market town as a SEPARATE region scene, built by
    /// its own builder so the greybox cove (GreyboxBuilder) is untouched. Menu: Hidden Harbours ▸
    /// Build Greywick Scene. Re-runnable (idempotent on the assets).
    ///
    /// Greywick is a <b>services region, not a town</b> (M2 grows it): a deep, sheltered harbour with a
    /// public wharf carrying the Fish Buyer + the Shipwright (the same Economy components the cove uses,
    /// referenced by stable id), plus a couple of flavour buildings. It is authored as a per-region
    /// scene that the <see cref="RegionSceneLoader"/> loads additively (CLAUDE.md §3), and it adds the
    /// Port Greywick + Coddle Cove <see cref="RegionDef"/> assets and a return passage.
    ///
    /// SCOPE / TODO: this scene currently carries its own Main Camera + AudioListener so it can be
    /// opened and reviewed standalone. When the additive Cove↔Greywick transition is fully wired (player
    /// persistence + unloading the origin region — a bootstrap/GreyboxBuilder change, out of scope here),
    /// region scenes should drop their camera/listener in favour of a persistent bootstrap. The wharf's
    /// hold/wallet providers (the player's boat + wallet) live in the origin scene, so they're left
    /// unwired here with the same TODO. The matching Cove→Greywick passage belongs in the cove scene.
    /// </summary>
    public static class GreywickBuilder
    {
        const string DataConfig  = "Assets/_Project/Data/Config";
        const string DataShip    = "Assets/_Project/Data/Shipwright";
        const string DataRegions = "Assets/_Project/Data/Regions";
        const string DataLicenses= "Assets/_Project/Data/Licenses";   // St Peters opening: the cod licence
        const string DataGear    = "Assets/_Project/Data/Gear";        // St Peters opening: the rod
        const string ArtSprites  = "Assets/_Project/Art/Sprites";
        const string ArtTrees    = "Assets/_Project/Art/Sprites/Environment/Trees"; // imported tree decor pack (TreeNN.png)
        const string TreeMatPath = "Assets/_Project/Art/Materials/Tree.mat";        // canopy wind-sway material (HiddenHarbours/TreeWind)
        const string ArtSea      = "Assets/_Project/Art/Tilesets/Water/SeaTile.png";
        const string ArtGrass    = "Assets/_Project/Art/Tilesets/Grass.png";
        const string ArtSand     = "Assets/_Project/Art/Tilesets/Sand.png";
        const string ArtWharfDeck= "Assets/_Project/Art/Tilesets/WharfDeck.png";
        const string ArtWharfPost= "Assets/_Project/Art/Sprites/WharfPost.png";
        const string ArtShipwright = "Assets/_Project/Art/Sprites/Buildings/ShipwrightShed.png";
        const string ArtFishStall  = "Assets/_Project/Art/Sprites/Buildings/FishBuyerStall.png";
        const string ArtHouseRed   = "Assets/_Project/Art/Sprites/Buildings/GreywickHouseRed.png";
        const string ArtHouseTeal  = "Assets/_Project/Art/Sprites/Buildings/GreywickHouseTeal.png";
        const string Scenes      = "Assets/_Project/Scenes";
        const string SceneName   = "Greywick";
        const string ScenePath   = Scenes + "/" + SceneName + ".unity";

        // VS-22 arrival/dock geometry — single source of truth shared with GreywickDockTests. The persistent
        // ControlSwitcher disembarks via a pure DISTANCE test (Vector2.Distance(boat, dockZone) <= radius);
        // the boat parks at ArrivalPos on arrival, so ArrivalPos MUST sit within DockZoneRadius of DockZonePos
        // or the player lands out of dock range and can't disembark (the owner-playtest gap #52 fixed — keep it).
        //
        // CROSSING DIRECTION (canon map): Port Greywick lies WEST of the cove, so you cross by SAILING WEST
        // and ARRIVE HEADING WEST (the hop preserves heading). The harbour reads true: the public wharf is a
        // peninsula pointing EAST into the deep harbour (open to the east), its dockable HEAD the EAST tip.
        // You enter from the EAST, park just east of the head (still heading west), and step WEST onto the deck.
        public const float DockZoneRadius = 3.5f;                              // ControlSwitcher's default _zoneRadius (cove pattern)
        public static readonly Vector3 ArrivalPos   = new Vector3(7f, 0f, 0f);  // deep harbour, just EAST of the wharf head
        public static readonly Vector3 DockZonePos  = new Vector3(4f, 0f, 0f);  // the wharf's seaward (EAST) HEAD — dock here
        public static readonly Vector3 DisembarkPos = new Vector3(2f, 0f, 0f);  // on the public wharf deck planks (west of the head)
        public static readonly Vector3 ToCovePassagePos = new Vector3(14f, 0f, 0f); // return passage: EAST edge → sail east back to the cove

        [MenuItem("Hidden Harbours/Build Greywick Scene")]
        public static void Build()
        {
            EnsureFolders();

            // --- DATA: regions + the boat offer (reused by stable id) -----------------------
            var greywick = LoadOrCreate<RegionDef>(DataRegions + "/PortGreywick.asset", r =>
            {
                r.Id = "region.port_greywick"; r.DisplayName = "Port Greywick"; r.SceneName = SceneName;
                r.IsDeepHarbour = true; r.HarbourDepthMeters = 6f;
                r.TideMeanLevel = 0f; r.TideAmplitude = 0.8f; r.TidePhaseHours = 2f;
                r.Description = "The market town: a deep, sheltered harbour where the coast's business " +
                                "gets done — selling, buying, hiring. Services, not a fishing ground.";
            });
            var cove = LoadOrCreate<RegionDef>(DataRegions + "/CoddleCove.asset", r =>
            {
                r.Id = "region.coddle_cove"; r.DisplayName = "Coddle Cove"; r.SceneName = "Greybox";
                r.IsDeepHarbour = false; r.HarbourDepthMeters = 2f;
                r.TideMeanLevel = 0f; r.TideAmplitude = 1.6f; r.TidePhaseHours = 0f;
                r.Description = "Your home harbour — the sheltered greybox cove.";
            });

            var config = LoadOrCreate<GameConfig>(DataConfig + "/GameConfig.asset");
            // Reuse the Punt offer by id (boat.punt) — created by the cove builder; create if absent.
            var puntOffer = LoadOrCreate<ShipwrightOffer>(DataShip + "/PuntOffer.asset", o =>
            {
                o.BoatId = "boat.punt"; o.DisplayName = "The Punt"; o.Price = 1800;
            });

            // --- St Peters opening vendors (economy data from #60; authored there, PLACED here) ----------
            // The cod licence, the rod, and the DAMAGED dory offer. Created here if absent so the builder is
            // self-sufficient (re-runnable), but economy-sim owns the canonical assets under the same paths.
            var codLicense = LoadOrCreate<LicenseDef>(DataLicenses + "/CodLicense.asset", l =>
            {
                l.Id = "license.cod"; l.DisplayName = "Cod Fishing License"; l.Price = 120;
                l.PermittedSpeciesIds = new[] { "fish.atlantic_cod" };
                l.Flavor = "Greywick's harbourmaster signs you off to take cod on rod and line.";
            });
            var rodOffer = LoadOrCreate<GearOffer>(DataGear + "/Rod.asset", g =>
            {
                g.Id = "gear.rod"; g.DisplayName = "Fishing Rod"; g.Price = 60;
                g.Flavor = "A proper rod and reel - the step up from a hand-line.";
            });
            var damagedDoryOffer = LoadOrCreate<ShipwrightOffer>(DataShip + "/DamagedDoryOffer.asset", o =>
            {
                o.BoatId = "boat.dory"; o.DisplayName = "The Old Dory (needs work)";
                o.Price = 400; o.StartsDamaged = true; o.RepairCost = 300;
            });

            // --- SCENE ----------------------------------------------------------------------
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera (standalone-viewable; see the class TODO about additive cleanup). Mirrors the
            // cove's locked pixel-perfect, on-foot landscape framing so Greywick reads at the same scale.
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = CameraFollow.OrthoSizeForWorldHeight(CameraFollow.OnFootWorldHeightMeters);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.10f, 0.15f); // deep-harbour dusk
            camGo.transform.position = new Vector3(-2f, 0f, -10f); // frame the west town + the east-poking wharf (standalone review)
            camGo.AddComponent<AudioListener>();
            ArtCameraSetup.ConfigurePixelPerfect(camGo);
            var ppc = camGo.GetComponent<PixelPerfectCamera>();
            if (ppc != null)
            {
                CameraFollow.ReferenceResolutionForWorldHeight(CameraFollow.OnFootWorldHeightMeters, out int refW, out int refH);
                ppc.refResolutionX = refW;
                ppc.refResolutionY = refH;
                EditorUtility.SetDirty(ppc);
            }

            // --- DEEP HARBOUR WATER ---------------------------------------------------------
            var waterSprite = MakeSquareSprite(ArtSprites + "/Square.png");
            var seaTile = LoadSpriteAny(ArtSea);
            var water = new GameObject("Harbour");
            var wsr = water.AddComponent<SpriteRenderer>();
            wsr.sortingOrder = -10;
            if (seaTile != null)
            {
                wsr.sprite = seaTile;
                wsr.drawMode = SpriteDrawMode.Tiled;
                wsr.size = new Vector2(120f, 120f);
                wsr.color = new Color(0.78f, 0.86f, 0.92f); // tint the tile a touch deeper/colder
                water.transform.localScale = Vector3.one;
            }
            else
            {
                wsr.sprite = waterSprite; wsr.color = new Color(0.12f, 0.22f, 0.30f); // deep harbour
                water.transform.localScale = new Vector3(120f, 120f, 1f);
            }

            // A few drifting markers so the deep water reads as moving, not a flat slab.
            var markerRng = new System.Random(22);
            var markers = new GameObject("HarbourMarkers");
            for (int i = 0; i < 24; i++)
            {
                var m = new GameObject("Marker");
                m.transform.SetParent(markers.transform);
                m.transform.position = new Vector3((float)(markerRng.NextDouble() * 80 - 40),
                                                   (float)(markerRng.NextDouble() * 40 - 30), 0f);
                var msr = m.AddComponent<SpriteRenderer>();
                msr.sprite = waterSprite; msr.color = new Color(0.26f, 0.38f, 0.46f); msr.sortingOrder = -5;
                m.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
            }

            // Reload assets from disk before wiring refs (an intervening import can invalidate the
            // in-memory instances created above — same gotcha the cove builder guards against).
            config    = AssetDatabase.LoadAssetAtPath<GameConfig>(DataConfig + "/GameConfig.asset");
            puntOffer = AssetDatabase.LoadAssetAtPath<ShipwrightOffer>(DataShip + "/PuntOffer.asset");
            greywick  = AssetDatabase.LoadAssetAtPath<RegionDef>(DataRegions + "/PortGreywick.asset");
            cove      = AssetDatabase.LoadAssetAtPath<RegionDef>(DataRegions + "/CoddleCove.asset");
            codLicense       = AssetDatabase.LoadAssetAtPath<LicenseDef>(DataLicenses + "/CodLicense.asset");
            rodOffer         = AssetDatabase.LoadAssetAtPath<GearOffer>(DataGear + "/Rod.asset");
            damagedDoryOffer = AssetDatabase.LoadAssetAtPath<ShipwrightOffer>(DataShip + "/DamagedDoryOffer.asset");

            // --- QUAY (the land the town sits on, along the WEST) ---------------------------
            // Greywick lies WEST of the cove, so you arrive from the EAST and the town is to the WEST; the
            // public wharf is a peninsula reaching EAST into the deep harbour (open water is to the east).
            MakeTiledGround("Quay",      LoadSpriteAny(ArtGrass),     new Vector2(-10f, 0f), new Vector2(10f, 30f), -7, waterSprite, new Color(0.40f, 0.46f, 0.40f));
            MakeTiledGround("QuayEdge",  LoadSpriteAny(ArtSand),      new Vector2(-4.5f, 0f), new Vector2(3f, 30f), -6, waterSprite, new Color(0.62f, 0.58f, 0.46f));
            // The public wharf deck reaching EAST out into the deep harbour (head = the east tip, x=4).
            MakeTiledGround("PublicWharf", LoadSpriteAny(ArtWharfDeck), new Vector2(0f, 0f), new Vector2(8f, 6f), -5, waterSprite, new Color(0.55f, 0.40f, 0.24f));

            // Pilings along the wharf's north & south edges, out toward the EAST head (fenders at the head).
            var postSprite = LoadSpriteAny(ArtWharfPost);
            for (int i = 0; i < 3; i++)
            {
                float px = i * 2f; // 0, 2, 4 (out toward the east head)
                MakePost(postSprite, new Vector2(px,  3f), waterSprite);
                MakePost(postSprite, new Vector2(px, -3f), waterSprite);
            }

            // --- BUILDINGS (services + a couple of flavour houses), on the WEST land ---------
            var shipwrightShed = MakeBuilding("ShipwrightShed",   LoadSpriteAny(ArtShipwright), new Vector2(-8f,  3f), waterSprite, new Color(0.50f, 0.42f, 0.34f));
            var fishStall      = MakeBuilding("FishBuyerStall",   LoadSpriteAny(ArtFishStall),  new Vector2(-8f, -3f), waterSprite, new Color(0.42f, 0.50f, 0.52f));
            MakeBuilding("GreywickHouseRed",  LoadSpriteAny(ArtHouseRed),  new Vector2(-12f,  5f), waterSprite, new Color(0.55f, 0.34f, 0.30f)); // flavour
            MakeBuilding("GreywickHouseTeal", LoadSpriteAny(ArtHouseTeal), new Vector2(-12f, -5f), waterSprite, new Color(0.30f, 0.48f, 0.48f)); // flavour

            // --- SHORELINE BOUNDARY ---------------------------------------------------------
            // Mirror the cove's ShoreEdge (an EdgeCollider2D fence dividing land from water) so the boat
            // can't sail THROUGH the quay/wharf geometry (owner playtest gap #2). The fence runs along the
            // WEST land waterline (x=-4) but DIPS EAST around the public wharf deck (centred (0,0), size
            // (8,6) → x ∈ [-4,4], seaward head at the EAST tip x=4): out the north edge, down the head, back
            // along the south edge. That makes the wharf a SOLID peninsula pointing east — the boat
            // approaches the head from the deep harbour (EAST) and stops against it to dock (dock zone
            // (4,0)), but cannot slip onto the deck or land. The disembark spot (2,0) sits on the deck BEHIND
            // the fence; the player is teleported there, then their footprint keeps them land-side. Open
            // water (EAST) is left fully open for the arrival + sail-in.
            MakeShoreline();

            // --- ECONOMY (reuse the cove's components, referenced by id) --------------------
            // Fish Buyer stall: Market → FishBuyer → WharfSellPoint (+ dev 'B' to sell). The hold/wallet
            // providers (player's boat + wallet) live in the origin scene → left unwired (TODO).
            var market = fishStall.AddComponent<Market>();
            var buyer  = fishStall.AddComponent<FishBuyer>();
            var sell   = fishStall.AddComponent<WharfSellPoint>();
            fishStall.AddComponent<DevSellInput>();          // RequireComponent(WharfSellPoint) — present
            SetRef(market, "_config", config);
            SetRef(buyer, "_market", market);
            SetRef(sell, "_buyer", buyer);

            // VS-22 travel: the player's hold + wallet live in the PERSISTENT core (a different scene), so
            // they can't be serialize-referenced here. Wire the wharf to scene-local PROXIES that forward
            // to the live persistent hold (PersistentHoldProxy → the dory's ShipHold) and wallet
            // (PersistentWalletProxy → GameServices.Wallet). The RegionTravelCoordinator binds the hold
            // proxy to the real hold on arrival; the wallet proxy always forwards to the live wallet — so
            // you sell your catch + buy the Punt here against the same hold + coin you sailed in with.
            var providersGo = new GameObject("PersistentProviders");
            providersGo.AddComponent<PersistentHoldProxy>();
            providersGo.AddComponent<PersistentWalletProxy>();
            SetRef(sell, "_holdProvider", providersGo);
            SetRef(sell, "_walletProvider", providersGo);

            // Shipwright shed: buy the Punt by id (+ dev 'P' to buy), paid from the persistent wallet proxy.
            var shipwright = shipwrightShed.AddComponent<Shipwright>();
            shipwrightShed.AddComponent<DevBuyInput>();      // RequireComponent(Shipwright) — present
            SetRef(shipwright, "_offer", puntOffer);
            SetRef(shipwright, "_walletProvider", providersGo);

            // --- ST PETERS OPENING VENDORS (places + data wiring; interaction drivers are gameplay/ui) ---
            // The opening's earn-your-way loop ends at Greywick: sell clams (the Fish Buyer above —
            // baseline Shellfish demand handles the clam, no override needed), buy the COD LICENCE + the
            // ROD, save up, then buy the DAMAGED DORY and pay to repair her. world-content places these
            // components + wires their DATA (the licence/gear/damaged-dory offers, by stable id) and the
            // wallet provider (the persistent proxy). The buy/repair SCREENS + the dig/walk/gear gates are
            // ui-ux / gameplay-systems — NOT wired here (no dev-input is attached so nothing collides with
            // the Punt's 'P'); each is the named seam those lanes attach their driver to.

            // Harbourmaster's office: sells the cod licence (LicenseVendor → license.cod). Reuse a flavour
            // house sprite (no new art — art-pipeline's lane); it sits north on the WEST land.
            var harbourOffice = MakeBuilding("HarbourmasterOffice", LoadSpriteAny(ArtHouseRed), new Vector2(-12f, 9f), waterSprite, new Color(0.46f, 0.40f, 0.52f));
            var licenseVendor = harbourOffice.AddComponent<LicenseVendor>();
            SetRef(licenseVendor, "_license", codLicense);
            SetRef(licenseVendor, "_walletProvider", providersGo);

            // General store / chandlery: sells the rod (GearShop → gear.rod). South on the WEST land.
            var store = MakeBuilding("GeneralStore", LoadSpriteAny(ArtHouseTeal), new Vector2(-12f, -9f), waterSprite, new Color(0.40f, 0.50f, 0.40f));
            var gearShop = store.AddComponent<GearShop>();
            SetRef(gearShop, "_offer", rodOffer);
            SetRef(gearShop, "_walletProvider", providersGo);

            // The DAMAGED DORY at the shipwright (the opening's prize): a second Shipwright stall wired with
            // the damaged-dory offer (buy → owned-but-unusable; pay TryRepair → usable). Its own GO so it
            // doesn't fight the Punt shipwright's offer; west land, beside the shed. Buy/repair screens are
            // ui-ux's, so no dev-input is added here (P already buys the Punt next door).
            var doryYard = MakeBuilding("ShipwrightDoryYard", LoadSpriteAny(ArtShipwright), new Vector2(-8f, 8f), waterSprite, new Color(0.46f, 0.40f, 0.32f));
            var doryShipwright = doryYard.AddComponent<Shipwright>();
            SetRef(doryShipwright, "_offer", damagedDoryOffer);
            SetRef(doryShipwright, "_walletProvider", providersGo);

            // --- REGION SCENE-LOAD PATH -----------------------------------------------------
            var loaderGo = new GameObject("RegionSceneLoader");
            var loader = loaderGo.AddComponent<RegionSceneLoader>();
            SetRefArray(loader, "_regions", new Object[] { greywick, cove });
            SetString(loader, "_currentSceneName", SceneName);

            // Return passage out to the EAST (toward the cove, which lies east of Greywick): sail east into
            // this wide, forgiving band to head back to Coddle Cove — so you arrive home at the cove dock
            // FROM THE WEST (heading east). The matching Cove→Greywick passage lives on the cove's WEST edge
            // (GreyboxBuilder.ToGreywickPassagePos).
            var passageGo = new GameObject("PassageToCoddleCove");
            passageGo.transform.position = ToCovePassagePos;
            var trigger = passageGo.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(3f, 16f);   // a tall east-edge band (forgiving, wide)
            var passage = passageGo.AddComponent<RegionPassage>();
            SetRef(passage, "_target", cove);
            SetRef(passage, "_loader", loader);

            // VS-22 arrival anchor: where the persistent rig binds when you sail in from the cove. The boat
            // appears in the deep harbour just off the public wharf head; board/disembark at the wharf deck.
            // The App RegionTravelCoordinator reads this on arrival to reposition the rig + re-point the dock.
            //
            // DISEMBARK GEOMETRY (the cove's proven pattern; do NOT regress #52): ControlSwitcher.InDockZone()
            // is a pure DISTANCE test — Vector2.Distance(boat, dockZone) <= _zoneRadius (3.5 m default on the
            // persistent switcher). It needs NO trigger collider on the dock zone; it only needs the BOAT to
            // PARK within 3.5 m of the dock zone on arrival. With the EAST-facing wharf the head is the deck's
            // east tip (4,0); you cross by sailing WEST, so you enter the harbour from the EAST still heading
            // west and park just east of the head at (7,0) — 3.0 m from the dock zone, comfortably in range —
            // then step WEST onto the deck. The PublicWharf deck is centred (0,0) size (8,6) → its seaward
            // (EAST) edge is x=4; the deep harbour is open east of it. The three positions are public
            // constants (single source of truth) so an EditMode test can assert the arrival↔dock distance
            // stays inside DockZoneRadius without a scene (GreywickDockTests).
            var gwArrival = new GameObject("GreywickArrival");
            gwArrival.transform.position = ArrivalPos;       // deep harbour, just EAST of the wharf head (open water; deck ends at x=4)
            var gwDock = new GameObject("GreywickDockZone");
            gwDock.transform.position = DockZonePos;          // the wharf's seaward (EAST) HEAD — within the dock radius of arrival
            var gwDisembark = new GameObject("GreywickDisembark");
            gwDisembark.transform.position = DisembarkPos;    // on the public wharf deck planks (west of the head)
            var gwAnchor = new GameObject("GreywickRegionAnchor").AddComponent<RegionAnchor>();
            gwAnchor.Configure("region.port_greywick", gwArrival.transform, gwDock.transform, gwDisembark.transform);

            // --- TREE DECOR (greybox dressing; world-content) ------------------------------------------
            // A sparse-to-moderate scatter of cold-coast trees on the WEST quay land only — the far-west
            // back edge behind the houses and a few in the gaps between/around the buildings — to soften
            // the harbour town. NEVER in the open harbour water (EAST of x=-4), on the public wharf deck
            // (x∈[-4,4], y∈[-3,3]) or its dock/disembark zones, on the paths, or overlapping a building
            // footprint (the x=-8 and x=-12 building rows). Cold-coast varieties only (green broadleaf,
            // pine, birch). Data-driven (GreywickTrees) so counts/positions tweak freely; sortingOrder is
            // derived from base Y so trees further north sort behind, and the band sits below buildings.
            PlaceTrees("Greywick", GreywickTrees, waterSprite);

            // --- SAVE & REGISTER ------------------------------------------------------------
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterScene(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[GreywickBuilder] Built Greywick.unity — Port Greywick (services region), EAST-FACING " +
                      "to read true (canon: Greywick lies WEST of the cove). You cross by sailing WEST, arrive " +
                      "from the EAST heading west, and continue west onto the wharf (Fish Buyer B + Shipwright " +
                      "P, reused by id). NEW St Peters opening vendors PLACED: Harbourmaster (cod licence), " +
                      "General Store (rod), and a Shipwright DORY YARD with the DAMAGED dory (buy + repair) — " +
                      "data-wired to the persistent wallet proxy; their buy/repair screens are ui-ux/gameplay's. " +
                      "Clams sell at the Fish Buyer (baseline Shellfish demand). The return passage heads EAST " +
                      "back to Coddle Cove. Loaded additively via RegionSceneLoader.");
            EditorUtility.DisplayDialog("Hidden Harbours",
                "Port Greywick scene built (EAST-FACING — the crossing now reads true).\n\nCanon: Greywick " +
                "lies WEST of the cove, so:\n• You SAIL WEST to cross\n• You ARRIVE from the EAST, heading " +
                "west, and continue west onto the wharf (Fish Buyer + Shipwright)\n• The return passage heads " +
                "EAST → you arrive home at the cove dock from the WEST\n\nLoaded additively by " +
                "RegionSceneLoader. RE-RUN both 'Build Greybox Scene' and 'Build Greywick Scene', then re-test.",
                "Fair winds");
        }

        // ---- helpers (self-contained; the cove builder's are private) -----------------------

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
            if (p != null && p.propertyType == SerializedPropertyType.ObjectReference)
            { p.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        static void SetString(Component c, string field, string value)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p != null && p.propertyType == SerializedPropertyType.String)
            { p.stringValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        static void SetRefArray(Component c, string field, Object[] values)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[GreywickBuilder] no array field '{field}'."); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Imported art is sliced (spriteMode Multiple, one sub-sprite), so LoadAssetAtPath<Sprite>
        // returns null — fall back to the first sub-sprite. Null if the art isn't imported.
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

        static void MakeTiledGround(string name, Sprite sprite, Vector2 center, Vector2 size, int order,
                                    Sprite fallback, Color fallbackColor)
        {
            var go = new GameObject(name);
            go.transform.position = new Vector3(center.x, center.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            if (sprite != null) { sr.sprite = sprite; sr.drawMode = SpriteDrawMode.Tiled; sr.size = size; }
            else { sr.sprite = fallback; sr.color = fallbackColor; go.transform.localScale = new Vector3(size.x * 2f, size.y * 2f, 1f); }
        }

        static void MakePost(Sprite sprite, Vector2 pos, Sprite fallback)
        {
            var go = new GameObject("WharfPost");
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 3;
            if (sprite != null) { sr.sprite = sprite; go.transform.localScale = Vector3.one; }
            else { sr.sprite = fallback; sr.color = new Color(0.45f, 0.32f, 0.20f); go.transform.localScale = new Vector3(0.5f, 1.5f, 1f); }
            // Solid piling so the boat bumps the pilings (cozy, no damage), mirroring the cove's MakePost.
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.3f;   // slim piling
        }

        // The land/water boundary: an EdgeCollider2D fence (like the cove's ShoreEdge) tracing the WEST land
        // waterline (x=-4) and dipping EAST around the public wharf deck so the wharf reads as a solid
        // peninsula pointing into the deep harbour and the boat can't sail through Greywick. Land is WEST
        // (x < -4); the deep harbour is open to the EAST. The boat arrives from the east and stops against
        // the wharf HEAD (the deck's east tip, x=4 — the dock zone). Public so an EditMode test can assert
        // its shape without a scene.
        public static readonly Vector2[] ShorelinePoints =
        {
            new Vector2(-4f,  20f),  // west land waterline, north
            new Vector2(-4f,   3f),  // in to the deck's north edge
            new Vector2( 4f,   3f),  // east along the deck's north edge to the head (the east tip)
            new Vector2( 4f,  -3f),  // down the head (east edge) — the boat stops here to dock (dock zone (4,0))
            new Vector2(-4f,  -3f),  // west along the deck's south edge back to the waterline
            new Vector2(-4f, -20f),  // west land waterline, south
        };

        static void MakeShoreline()
        {
            var shore = new GameObject("Shoreline");
            var edge = shore.AddComponent<EdgeCollider2D>();
            edge.points = ShorelinePoints;   // non-trigger by default → a solid wall the boat bumps
        }

        static GameObject MakeBuilding(string name, Sprite sprite, Vector2 pos, Sprite fallback, Color fallbackColor)
        {
            var go = new GameObject(name);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 2;
            if (sprite != null) { sr.sprite = sprite; go.transform.localScale = Vector3.one; }
            else { sr.sprite = fallback; sr.color = fallbackColor; go.transform.localScale = new Vector3(5f, 5f, 1f); }
            // Solid building so the boat / on-foot player can't pass through Greywick's geometry. Non-trigger
            // BoxCollider2D sized to the rendered footprint (sprite bounds in local space, or the fallback
            // square's metric size). Buildings sit on the quay land beyond the shoreline; this makes each
            // read solid in its own right (owner playtest: "boat sails through the buildings").
            var box = go.AddComponent<BoxCollider2D>();
            if (sprite != null) { box.size = sprite.bounds.size; box.offset = sprite.bounds.center; }
            else { box.size = Vector2.one; }   // 1 unit × the (5,5,1) localScale → a 5 m × 5 m footprint
            return go;
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
            imp.spritePixelsPerUnit = 32f;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void RegisterScene(string path)
        {
            var list = EditorBuildSettings.scenes.ToList();
            if (list.Any(s => s.path == path)) return;
            list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ---- tree decor (greybox dressing) ----------------------------------------------------------
        // One placed tree: world position (the trunk base — the sprite pivot is BottomCenter) + the
        // imported variety file ("TreeNN"). Plain struct so placement is a tweakable data list.
        struct TreeSpec
        {
            public float X, Y;
            public string Variety;   // "TreeNN" → Art/Sprites/Environment/Trees/TreeNN.png
            public TreeSpec(float x, float y, string variety) { X = x; Y = y; Variety = variety; }
        }

        // COLD NORTH ATLANTIC scatter for Greywick — WEST quay land ONLY (land is x < -4; the deep harbour
        // is open to the EAST). Hugs the far-west back edge behind the x=-12 house row and tucks into the
        // gaps north/south of the building rows. NONE in the open harbour water, on the public wharf deck
        // (x∈[-4,4], y∈[-3,3]) or its zones, on the paths, or over a building (rows at x=-8 and x=-12, each
        // ≈5 m). Varieties: green broadleaf (Tree01/05/06/08/18/21/34/35), pine (Tree02/22), birch (Tree25).
        static readonly TreeSpec[] GreywickTrees =
        {
            // Far-west back edge of the quay, behind the x=-12 house row (north → south), clear of houses.
            new TreeSpec(-14.4f, 12.0f, "Tree02"),  // pine, NW
            new TreeSpec(-14.2f,  7.0f, "Tree08"),  // broadleaf (between HarbourOffice y9 and HouseRed y5)
            new TreeSpec(-14.5f,  0.0f, "Tree25"),  // birch, mid west edge
            new TreeSpec(-14.2f, -7.0f, "Tree06"),  // broadleaf (between HouseTeal y-5 and GeneralStore y-9)
            new TreeSpec(-14.4f,-12.0f, "Tree22"),  // pine, SW
            // North end of the quay, above the building rows (y>10, clear of HarbourOffice at -12,9).
            new TreeSpec(-9.2f,  12.5f, "Tree01"),  // broadleaf
            new TreeSpec(-6.0f,  13.0f, "Tree05"),  // broadleaf, NE land edge (well west of the x=-4 shore)
            // South end of the quay, below the building rows (y<-11, clear of GeneralStore at -12,-9).
            new TreeSpec(-9.4f, -12.5f, "Tree18"),  // broadleaf
            new TreeSpec(-5.8f, -13.0f, "Tree21"),  // broadleaf, SE land edge
            // A couple between the x=-8 service row and the shore strip (north/south of the wharf, clear of
            // the deck y∈[-3,3] and the sheds at -8,±3 / -8,8).
            new TreeSpec(-5.2f,   7.5f, "Tree34"),  // broadleaf, north of the wharf
            new TreeSpec(-5.0f,  -7.5f, "Tree35"),  // broadleaf, south of the wharf
        };

        // Instance the tree decor under a single "Decor/Trees" parent. sortingOrder derives from the
        // tree's base Y (BottomCenter pivot) so trees further north (higher Y) render behind nearer ones;
        // trees behind the x=-12 house row (high Y → negative order) sort under the buildings (order 2).
        // Loads each variety via LoadSpriteAny (Sprite Mode Multiple → one TreeNN_0 sub-sprite, so
        // LoadAssetAtPath<Sprite> is null; [[imported-art-spritemode-multiple]]). Tinted-square fallback so
        // the scene still builds before the art is imported.
        static void PlaceTrees(string sceneLabel, TreeSpec[] specs, Sprite fallback)
        {
            var decor = new GameObject("Decor");
            var trees = new GameObject("Trees");
            trees.transform.SetParent(decor.transform, false);
            // The canopy wind-sway material (HiddenHarbours/TreeWind), shared with the drag-in tree prefabs, so
            // Greywick's baked trees sway off the SAME wind as the grass + water. Optional — null leaves them
            // static (re-run after importing the TreeWind shader + Tree.mat).
            var treeMaterial = AssetDatabase.LoadAssetAtPath<Material>(TreeMatPath);
            int placed = 0;
            foreach (var t in specs)
            {
                var go = new GameObject(t.Variety);
                go.transform.SetParent(trees.transform, false);
                go.transform.position = new Vector3(t.X, t.Y, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = Mathf.RoundToInt(-t.Y * 2f);
                if (treeMaterial != null) sr.sharedMaterial = treeMaterial;   // canopy sway off the shared wind
                var sprite = LoadSpriteAny($"{ArtTrees}/{t.Variety}.png");
                if (sprite != null) { sr.sprite = sprite; go.transform.localScale = Vector3.one; }
                else { sr.sprite = fallback; sr.color = new Color(0.24f, 0.40f, 0.26f); go.transform.localScale = new Vector3(1.6f, 3.2f, 1f); }
                placed++;
            }
            Debug.Log($"[GreywickBuilder] Placed {placed} decor trees in {sceneLabel} (under Decor/Trees).");
        }

        static void EnsureFolders()
        {
            foreach (var f in new[] { DataConfig, DataShip, DataRegions, ArtSprites, Scenes })
            {
                if (AssetDatabase.IsValidFolder(f)) continue;
                var parent = Path.GetDirectoryName(f).Replace('\\', '/');
                var leaf = Path.GetFileName(f);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }
    }
}
#endif
