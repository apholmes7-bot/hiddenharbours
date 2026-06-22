#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.Boats;
using HiddenHarbours.Fishing;
using HiddenHarbours.Economy;
using HiddenHarbours.Player;
using HiddenHarbours.World;         // VS-21: NPCs, dialogue, the inheritance opening (world-content)
using HiddenHarbours.UI;            // VS-17: the glanceable HUD (ui-ux)
using HiddenHarbours.Art.Editor;   // VS-23: locked Pixel-Perfect camera convention
using UnityEngine.Rendering.Universal; // PixelPerfectCamera — PC-first landscape reference override

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// One-click greybox: creates the data assets (GameConfig, the Dory, a few fish), builds a
    /// playable scene wiring the services + dory + wharf, and opens it. Menu: Hidden Harbours ▸
    /// Build Greybox Scene. Re-runnable (idempotent on the assets). This is a dev convenience, not
    /// shipping content — real scenes are authored by world-content (see backlog VS-02).
    /// </summary>
    public static class GreyboxBuilder
    {
        const string DataConfig = "Assets/_Project/Data/Config";
        const string DataBoats  = "Assets/_Project/Data/Boats";
        const string DataFish   = "Assets/_Project/Data/Fish";
        const string DataShip   = "Assets/_Project/Data/Shipwright";
        const string DataRegions= "Assets/_Project/Data/Regions";        // VS-22 region defs (cove + Greywick)
        const string ArtSprites = "Assets/_Project/Art/Sprites";
        const string ArtDory    = "Assets/_Project/Art/Boats/Dory.png";          // legacy single-sprite hull (fallback)
        const string ArtDoryHull = "Assets/_Project/Art/Boats/DoryHull.png";     // oar-less hull base (64×144, centre)
        const string ArtOar      = "Assets/_Project/Art/Boats/Oar.png";          // one oar (56×16, handle/LeftCenter pivot), used ×2
        const string ArtDoryRower = "Assets/_Project/Art/Boats/DoryRower.png";   // rower figure (26×28, centre)
        const string ArtPunt    = "Assets/_Project/Art/Boats/Punt.png";          // tier-1 swap sprite (VS-16)
        const string ArtSea     = "Assets/_Project/Art/Tilesets/Water/SeaTile.png"; // final tile (VS-24)
        const string ArtTensionGauge     = "Assets/_Project/Art/UI/TensionGauge.png";     // VS-13 rod gauge
        const string ArtLineHook         = "Assets/_Project/Art/UI/LineHook.png";         // VS-13 rod gauge
        const string ArtFishSilhouette   = "Assets/_Project/Art/UI/FishOnSilhouette.png"; // VS-13 rod gauge
        const string ArtFisher   = "Assets/_Project/Art/Characters/FisherSheet.png";      // on-foot player (sliced 3×4)
        const string ArtGrass    = "Assets/_Project/Art/Tilesets/Grass.png";              // island ground
        const string ArtSand     = "Assets/_Project/Art/Tilesets/Sand.png";               // beach border
        const string ArtCottage  = "Assets/_Project/Art/Sprites/Buildings/Cottage.png";   // home on the island
        const string ArtWharfDeck= "Assets/_Project/Art/Tilesets/WharfDeck.png";          // dock planks
        const string ArtWharfPost= "Assets/_Project/Art/Sprites/WharfPost.png";           // dock pilings
        const string ArtGinny    = "Assets/_Project/Art/Characters/Ginny.png";            // VS-21 Aunt Ginny (world)
        const string ArtNeighbour= "Assets/_Project/Art/Characters/Neighbour.png";        // VS-21 a neighbour
        const string ArtPortraitGinny = "Assets/_Project/Art/Portraits/Ginny.png";        // VS-21 dialogue portrait
        const string ArtPortraitNed   = "Assets/_Project/Art/Portraits/Ned.png";          // VS-21 logbook portrait
        const string ArtDialoguePanel = "Assets/_Project/Art/UI/DialoguePanel.png";       // VS-21 dialogue panel
        const string ArtNamePlate     = "Assets/_Project/Art/UI/NamePlate.png";           // VS-21 nameplate
        const string Scenes     = "Assets/_Project/Scenes";
        const string ScenePath  = Scenes + "/Greybox.unity";

        // VS-22 crossing geometry (canon map: Port Greywick lies WEST of the cove — "PORT GREYWICK ——+——
        // CODDLE COVE"). So you CROSS BY SAILING WEST, and you RETURN to the cove dock FROM THE WEST. These
        // are public so an EditMode test can assert the crossing reads true without loading a scene.
        // CoveDockZoneRadius mirrors ControlSwitcher's default _zoneRadius (the cove disembark is a pure
        // distance test), so the return arrival must park within it of the cove dock or E can't disembark.
        public const float CoveDockZoneRadius = 3.5f;
        public static readonly Vector3 CoveDockZonePos      = new Vector3(0f, -12f, 0f);     // cove dock head / mooring
        public static readonly Vector3 CoveArrivalPos       = new Vector3(-2.5f, -13.5f, 0f); // return from Greywick: just WEST of the dock
        public static readonly Vector3 ToGreywickPassagePos = new Vector3(-22f, -12f, 0f);   // WEST edge of the open water → sail west to cross

        [MenuItem("Hidden Harbours/Build Greybox Scene")]
        public static void Build()
        {
            EnsureFolders();

            // --- DATA ASSETS ---------------------------------------------------------------
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

            // Tier-1 "Punt / Skiff" (design/boats-and-navigation.md §1.1) — the first boat you BUY, the
            // payoff of the buy-the-Punt loop (VS-16). Authored as data, never hardcoded in gameplay C#.
            var punt = LoadOrCreate<BoatHullDef>(DataBoats + "/Punt.asset", ApplyPuntStats);

            // Regions (VS-22 travel): this cove + Port Greywick, as data, for the loader/passage. Created
            // here if absent and authored by GreywickBuilder under the same stable ids (first run wins).
            var coveRegion = LoadOrCreate<RegionDef>(DataRegions + "/CoddleCove.asset", r =>
            {
                r.Id = "region.coddle_cove"; r.DisplayName = "Coddle Cove"; r.SceneName = "Greybox";
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

            var fish = new[]
            {
                Fish("fish.atlantic_cod", "Atlantic Cod", FishCategory.InshoreGroundfish, Rarity.Common,    14, 0.20f, 1.0f, 2f, 12f),
                Fish("fish.haddock",      "Haddock",      FishCategory.InshoreGroundfish, Rarity.Common,    16, 0.20f, 0.9f, 1f, 6f),
                Fish("fish.mackerel",     "Mackerel",     FishCategory.Pelagic,           Rarity.Uncommon,  10, 0.35f, 0.8f, 0.3f, 1.5f),
                Fish("fish.lobster",      "American Lobster", FishCategory.Shellfish,     Rarity.Prize,     28, 0.35f, 0.4f, 0.4f, 4f),
            };

            // --- SCENE ---------------------------------------------------------------------
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            // PC-first intimate LANDSCAPE framing (ADR 0005), DATA-DRIVEN: the game starts ON FOOT, so the
            // camera frames the tighter on-foot view (~9 m). The same mapping zooms it out to the boat
            // tiers when sailing (CameraFollow / OwnedFleet). Authored here for the scene view; CameraFollow
            // re-applies it at play. Single source of truth for the mapping is in CameraFollow.
            cam.orthographicSize = CameraFollow.OrthoSizeForWorldHeight(CameraFollow.OnFootWorldHeightMeters);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.13f, 0.18f);
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();
            // VS-23: lock the Pixel-Perfect camera (PPU 32, pixel-snapping) so there's no sub-pixel
            // shimmer as the follow-cam tracks the dory. Shared art-pipeline convention (bible §3.7).
            ArtCameraSetup.ConfigurePixelPerfect(camGo);
            // PC-first (ADR 0005): the locked convention's reference is portrait (mobile-era). Override
            // it to a 16:9 LANDSCAPE reference — PPU stays locked at 32 — so the live render is
            // pixel-perfect at 1920×1080 (exact ×3 zoom) and the framing stays intimate in smaller
            // desktop windows instead of collapsing to an over-wide view. (art-pipeline: fold a PC
            // landscape reference into the locked convention in a VS-23 follow-up.)
            var ppc = camGo.GetComponent<PixelPerfectCamera>();
            if (ppc != null)
            {
                CameraFollow.ReferenceResolutionForWorldHeight(CameraFollow.OnFootWorldHeightMeters, out int refW, out int refH);
                ppc.refResolutionX = refW;   // on-foot 9 m → 480×270 (exact ×4 pixel-perfect zoom at 1080p)
                ppc.refResolutionY = refH;   // 16:9; the boat tiers set a larger reference when sailing
                EditorUtility.SetDirty(ppc);
            }

            // Water backdrop. Use the final tiling sea tile if it's been imported (the placeholder→
            // final swap, VS-24); otherwise fall back to a flat slate-blue square so the greybox
            // still builds before any art exists.
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

            // Reload data assets from disk before wiring. An intervening AssetDatabase import (the
            // sprite SaveAndReimport above) can invalidate the in-memory references created earlier,
            // which is what left GameConfig / hull showing "None". Reloading guarantees valid refs.
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
            // hull sprite so OwnedFleet has something to swap the renderer to on the grant (null-safe).
            punt = AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/Punt.asset");
            if (punt != null)
            {
                ApplyPuntStats(punt);
                punt.Sprite = LoadArtSprite(ArtPunt);
                EditorUtility.SetDirty(punt);
            }
            fish = new[]
            {
                AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(DataFish + "/AtlanticCod.asset"),
                AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(DataFish + "/Haddock.asset"),
                AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(DataFish + "/Mackerel.asset"),
                AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(DataFish + "/AmericanLobster.asset"),
            };
            coveRegion     = AssetDatabase.LoadAssetAtPath<RegionDef>(DataRegions + "/CoddleCove.asset");
            greywickRegion = AssetDatabase.LoadAssetAtPath<RegionDef>(DataRegions + "/PortGreywick.asset");

            // Services root
            var root = new GameObject("GameRoot");
            var clock = root.AddComponent<GameClock>();
            var env = root.AddComponent<EnvironmentService>();
            var wallet = root.AddComponent<PlayerWallet>();
            var gameRoot = root.AddComponent<GameRoot>();
            SetRef(clock, "_config", config);
            SetRef(env, "_config", config);
            SetTideProfile(env, 0f, 1.6f, 0f);
            SetRef(gameRoot, "_clock", clock);
            SetRef(gameRoot, "_environment", env);
            SetRef(gameRoot, "_wallet", wallet);

            // HUD (VS-17 + partial VS-19, owned by ui-ux). Self-contained: builds its own Canvas in
            // Awake, reads state only through Core. _config gives it SecondsPerHour for the tide
            // time-to-turn conversion (no magic numbers). Added on GameRoot so it persists like the
            // services. (This single additive line is the only ui-ux touch in App.Editor — tagged
            // for lead-architect review.)
            var hud = root.AddComponent<HudController>();
            SetRef(hud, "_config", config);

            // Wharf (market + buyer + sell interaction)
            var wharf = new GameObject("Wharf");
            var market = wharf.AddComponent<Market>();
            var buyer = wharf.AddComponent<FishBuyer>();
            SetRef(market, "_config", config);
            SetRef(buyer, "_market", market);

            // The Dory
            var doryGo = new GameObject("Dory");
            doryGo.transform.position = Vector3.zero;
            var sr = doryGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 0;
            // Layered oar-rework rig (imported-assets.md Batch 6): the boat's own renderer is the OAR-LESS
            // hull base (DoryHull); the rower + the two oars are children built below and animated by
            // BoatRowAnimator. OwnedFleet swaps THIS renderer to the Punt on a purchase (and the animator
            // then hides the oar rig). Fall back to the legacy Dory.png, then a tinted square, so the
            // greybox still builds before the art is imported.
            var hullSprite = LoadSpriteAny(ArtDoryHull) ?? LoadSpriteAny(ArtDory);
            if (hullSprite != null)
            {
                sr.sprite = hullSprite;                    // 64×144 px @ PPU 32 = 2 m × 4.5 m, bow-up
                doryGo.transform.localScale = Vector3.one; // honest metric size — never scale a real sprite
            }
            else
            {
                sr.sprite = waterSprite; sr.color = new Color(0.82f, 0.45f, 0.25f); // dory hull colour
                doryGo.transform.localScale = new Vector3(3.6f, 9f, 1f); // ~1.8 m beam × 4.5 m length
            }
            var rb = doryGo.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            var boat = doryGo.AddComponent<BoatController>();
            // Hull collider so the boat bumps the shore edge + dock pilings (cozy, no damage). BoatController
            // requires a CapsuleCollider2D; size it just inside the hull sprite (2 m × 4.5 m) with rounded
            // ends so nudging up to the dock reads gentle. Stays CENTRED on the hull (the oars are visual
            // children only, so they never move the collider). Default zero-bounce material → no bouncing.
            var hullCol = doryGo.GetComponent<CapsuleCollider2D>() ?? doryGo.AddComponent<CapsuleCollider2D>();
            hullCol.direction = CapsuleDirection2D.Vertical;
            hullCol.size = new Vector2(1.7f, 4.0f);
            hullCol.offset = Vector2.zero;
            var hold = doryGo.AddComponent<ShipHold>();
            var devBoat = doryGo.AddComponent<DevBoatInput>();

            // --- Oar-rework rig: rower + two independently-rotating oars, children of the dory ---------
            // Stack back→front: hull (sr, order 0) → oars (order 1) → rower (order 2). Each oar is one
            // Oar.png parented under an oarlock PIVOT transform at its gunwale; BoatRowAnimator rotates the
            // pivots about the oar fulcrum from each oar's per-oar state. One parent ("OarRig") so the
            // animator can hide the whole rig when a bought engine hull (no oars) is active.
            var oarRig = new GameObject("OarRig");
            oarRig.transform.SetParent(doryGo.transform, false);

            var rower = new GameObject("DoryRower");
            rower.transform.SetParent(oarRig.transform, false);
            var rowerSr = rower.AddComponent<SpriteRenderer>();
            rowerSr.sortingOrder = 2;                       // in front of the oars (hands meet the handles)
            var rowerSprite = LoadSpriteAny(ArtDoryRower);
            if (rowerSprite != null) rowerSr.sprite = rowerSprite;
            else { rowerSr.sprite = waterSprite; rowerSr.color = new Color(0.25f, 0.20f, 0.15f); rower.transform.localScale = new Vector3(1.6f, 1.8f, 1f); }

            var oarSprite = LoadSpriteAny(ArtOar);
            var leftOarPivot  = MakeOar(oarRig.transform, "LeftOar",  new Vector2(-0.9f, -0.1f), true,  oarSprite, waterSprite);
            var rightOarPivot = MakeOar(oarRig.transform, "RightOar", new Vector2( 0.9f, -0.1f), false, oarSprite, waterSprite);

            // Rowing animation: rotate the two oar pivots from per-oar state (forward/back/idle → sweep),
            // ease to neutral at rest, hide the rig on an engine hull. No baked frames — real transforms now.
            var rowAnim = doryGo.AddComponent<BoatRowAnimator>();
            SetRef(rowAnim, "_leftOarPivot", leftOarPivot);
            SetRef(rowAnim, "_rightOarPivot", rightOarPivot);
            SetRef(rowAnim, "_oarRig", oarRig);
            // STEP 1: the Dory is static scenery moored at the dock — WASD drives only the on-foot player.
            // Disable its dev input AND the controller so ambient wind/current doesn't drift the moored
            // boat. Both stay present (unbroken); step 2's board/disembark re-enables them on boarding.
            devBoat.enabled = false;
            boat.enabled = false;
            var fishing = doryGo.AddComponent<FishingController>();
            doryGo.AddComponent<DevFishingInput>();
            SetRef(boat, "_hull", dory);
            SetRef(hold, "_hull", dory);
            SetRef(fishing, "_holdProvider", doryGo);
            SetRefArray(fishing, "_regionFish", fish);

            // Fishing mini-game (VS-13, gameplay-systems). A simple visible fishing-spot marker so there
            // IS a spot (greybox flavour — proximity gating is future; Space still casts from the dory),
            // plus the TRANSIENT rod gauge overlay. The gauge reads the fight purely through the Core
            // FishingStateChanged signal, so it needs no controller ref — just the imported UI art,
            // loaded fresh (post-reload) and wired by serialized ref (null-safe if a sprite is missing).
            var fishingSpot = new GameObject("FishingSpot");
            fishingSpot.transform.position = new Vector3(5f, -10f, 0f); // in the water beside the dock
            var spotSr = fishingSpot.AddComponent<SpriteRenderer>();
            spotSr.sprite = waterSprite;
            spotSr.color = new Color(0.30f, 0.58f, 0.66f, 0.7f);
            spotSr.sortingOrder = -4;
            fishingSpot.transform.localScale = new Vector3(1.5f, 1.5f, 1f);

            var gaugeGo = new GameObject("FishingGauge");
            var gauge = gaugeGo.AddComponent<RodGaugeView>();
            SetRef(gauge, "_gaugeSprite", LoadArtSprite(ArtTensionGauge));
            SetRef(gauge, "_lineHookSprite", LoadArtSprite(ArtLineHook));
            SetRef(gauge, "_fishSprite", LoadArtSprite(ArtFishSilhouette));

            // Boat grant (VS-16, gameplay-systems): OwnedFleet listens for the Shipwright's BoatPurchased
            // signal and swaps the active hull to the bought boat (data-driven by id). It lives on the
            // Dory GO so it has the BoatController/ShipHold/renderer to swap, and is registered with the
            // {Dory, Punt} hulls. Refs are the reloaded (persisted) assets, so they don't save as None.
            var fleet = doryGo.AddComponent<OwnedFleet>();
            SetRefArray(fleet, "_registry", new Object[] { dory, punt });
            SetRef(fleet, "_boat", boat);
            SetRef(fleet, "_hold", hold);
            SetRef(fleet, "_spriteRenderer", sr);

            // Active-boat heading seam (VS-19, lead-architect / ADR 0007): a tiny Core IActiveBoatService
            // producer on the Dory. The HUD (UI lane, Core-only) pulls the boat's heading + course-over-
            // ground from it at ~4 Hz to drive the compass + set-&-drift predictor + apparent wind, never
            // referencing the Boats module. It self-registers into GameServices.ActiveBoat on enable and
            // reports HasActiveBoat off the controller's enabled flag (moored/on-foot → false). _boat is
            // the same persistent dory OwnedFleet swaps the hull on.
            var activeBoatProbe = doryGo.AddComponent<ActiveBoatProbe>();
            SetRef(activeBoatProbe, "_boat", boat);

            // Wharf sell interaction (VS-22): B sells the dory's hold to the buyer, paying the wallet.
            // Wired here because it needs the dory (IHold) and the services root (IWallet), both built
            // above. _boat is left unset so 'B' always sells, keeping the greybox frictionless — assign
            // it + tune _dockRadius later to require docking. (DevSellInput is a placeholder; ui-ux
            // replaces it with the real Interact intent.)
            var sellPoint = wharf.AddComponent<WharfSellPoint>();
            wharf.AddComponent<DevSellInput>();
            SetRef(sellPoint, "_buyer", buyer);
            SetRef(sellPoint, "_holdProvider", doryGo);
            SetRef(sellPoint, "_walletProvider", root);

            // Shipwright buy flow (VS-16): P buys the Punt with the wallet; on success the Shipwright
            // raises BoatPurchased (gameplay-systems listens to swap the boat). Economy side only — the
            // price lives in a ShipwrightOffer asset, and we reference the boat by id, never the Boats
            // module. (DevBuyInput is a placeholder; ui-ux replaces it with the real buy screen.)
            var puntOffer = LoadOrCreate<ShipwrightOffer>(DataShip + "/PuntOffer.asset", o =>
            {
                o.BoatId = "boat.punt"; o.DisplayName = "The Punt"; o.Price = 1800;
            });
            var shipwrightGo = new GameObject("Shipwright");
            var shipwright = shipwrightGo.AddComponent<Shipwright>();
            shipwrightGo.AddComponent<DevBuyInput>();
            SetRef(shipwright, "_offer", puntOffer);
            SetRef(shipwright, "_walletProvider", root);

            // --- DEMO ISLAND (on-foot player, step 1/2; additive — flagged for lead-architect) --------
            // A small island the player stands on: tiled sand beach + grass, the cottage, a dock into the
            // water with pilings, and the (now static) Dory moored at the dock end. A closed shore-edge
            // collider keeps the player out of open water. Tiles use the same tiled-SpriteRenderer
            // approach as the sea backdrop; all art is imported.
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

            // The Dory is now static scenery moored at the dock end (its controller is disabled above).
            // Parked just clear of the dock-head shore-edge wall (y=-12) so its new 4 m hull collider
            // doesn't start overlapping it (which would jolt the moored boat free on the first frame).
            doryGo.transform.position = new Vector3(0f, -14.2f, 0f);

            // Shore edge: a closed collider fence tracing the beach + dock so the player can roam the
            // island and walk out the dock, but can't wander into open water (P5 cozy bounds).
            var shore = new GameObject("ShoreEdge");
            var edge = shore.AddComponent<EdgeCollider2D>();
            edge.points = new[]
            {
                new Vector2(-10f, 9f), new Vector2(10f, 9f), new Vector2(10f, -5f),
                new Vector2(1.5f, -5f), new Vector2(1.5f, -12f), new Vector2(-1.5f, -12f),
                new Vector2(-1.5f, -5f), new Vector2(-10f, -5f), new Vector2(-10f, 9f),
            };

            // --- ON-FOOT PLAYER ------------------------------------------------------------------------
            // Top-down WASD walk from the sliced FisherSheet. Honest 1×2 m frame (~1.8 m fisher); never
            // rescaled. A footprint collider + the shore edge keep the player on land/dock.
            var playerGo = new GameObject("Player");
            playerGo.transform.position = new Vector3(-4.5f, 2.5f, 0f); // in front of the cottage
            playerGo.transform.localScale = Vector3.one;
            var playerSr = playerGo.AddComponent<SpriteRenderer>();
            playerSr.sortingOrder = 10;                                 // in front of ground/cottage
            var prb = playerGo.AddComponent<Rigidbody2D>();
            prb.gravityScale = 0f; prb.freezeRotation = true;
            prb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var foot = playerGo.AddComponent<CircleCollider2D>();
            foot.radius = 0.35f; foot.offset = new Vector2(0f, -0.7f);  // footprint at the feet
            var walk = playerGo.AddComponent<PlayerWalkController>();
            var fisherFrames = LoadSheetFrames(ArtFisher);
            SetRefArray(walk, "_frames", fisherFrames);
            if (fisherFrames.Length > 0 && fisherFrames[0] != null)
                playerSr.sprite = fisherFrames[0];                     // idle-down for the scene view

            // Camera starts following the PLAYER at the tighter on-foot framing. It also knows both
            // mode targets (player + boat) and switches between them on the Core ControlModeChanged
            // signal (VS boarding, step 2) — data-driven, pixel-perfect, same approach as the boat tiers.
            var cameraFollow = camGo.AddComponent<CameraFollow>();
            cameraFollow.Target = playerGo.transform;
            var cfSo = new SerializedObject(cameraFollow);
            var cfWorldH = cfSo.FindProperty("_worldHeightMeters");
            if (cfWorldH != null) { cfWorldH.floatValue = CameraFollow.OnFootWorldHeightMeters; cfSo.ApplyModifiedPropertiesWithoutUndo(); }
            SetRef(cameraFollow, "_onFootTarget", playerGo.transform);
            SetRef(cameraFollow, "_boatTarget", doryGo.transform);

            // --- BOARDING (control switch, step 2/2; additive — flagged for lead-architect) ------------
            // A dock zone at the mooring: walk into it on foot and press E to board; sail the boat back
            // into it and press E to disembark onto the dock. The ControlSwitcher (Player lane) toggles
            // the player vs boat controllers and hands the camera off via Core signals — it never
            // references the App camera. Start ON FOOT (the boat's controller/input are disabled above).
            var dockZone = new GameObject("DockZone");
            dockZone.transform.position = CoveDockZonePos;   // the dock end / mooring
            var disembarkPoint = new GameObject("DisembarkPoint");
            disembarkPoint.transform.position = new Vector3(0f, -10.5f, 0f); // on the dock planks

            var switcherGo = new GameObject("ControlSwitcher");
            var switcher = switcherGo.AddComponent<ControlSwitcher>();
            SetRef(switcher, "_playerWalk", walk);
            SetRef(switcher, "_boatController", boat);
            SetRef(switcher, "_boatInput", devBoat);
            SetRef(switcher, "_dockZone", dockZone.transform);
            SetRef(switcher, "_disembarkPoint", disembarkPoint.transform);

            // --- REGION TRAVEL (VS-22 Cove↔Greywick; persistent-core / additive-region) ----------------
            // FLAG lead-architect: this is the pragmatic VS-01 persistent-core approach. The core (GameRoot
            // — which already DontDestroyOnLoads itself — plus the player, dory+hold, camera, control
            // switcher, fishing gauge) is tagged PersistentObject so it SURVIVES the additive region hop,
            // carrying the player, boat, hold and wallet. A persistent RegionSceneLoader + a
            // RegionTravelCoordinator move the rig across and re-bind it to whichever region became active;
            // region scenes are TOGGLED (roots SetActive), not reloaded, so the core is never duplicated.
            // The longer-term home for the core is a dedicated Bootstrap scene (lead-architect's).
            playerGo.AddComponent<PersistentObject>();
            doryGo.AddComponent<PersistentObject>();
            camGo.AddComponent<PersistentObject>();
            switcherGo.AddComponent<PersistentObject>();
            gaugeGo.AddComponent<PersistentObject>();

            var loaderGo = new GameObject("RegionSceneLoader");
            loaderGo.AddComponent<PersistentObject>();
            var loader = loaderGo.AddComponent<RegionSceneLoader>();
            SetRefArray(loader, "_regions", new Object[] { coveRegion, greywickRegion });
            SetString(loader, "_currentSceneName", "Greybox");   // this scene; explicit (don't rely on Awake vs DDOL order)

            var coordinatorGo = new GameObject("RegionTravelCoordinator");
            coordinatorGo.AddComponent<PersistentObject>();
            var coordinator = coordinatorGo.AddComponent<RegionTravelCoordinator>();
            SetRef(coordinator, "_player", playerGo.transform);
            SetRef(coordinator, "_boat", doryGo.transform);
            SetRef(coordinator, "_switcher", switcher);
            SetRef(coordinator, "_hold", hold);

            // This region's anchor: when you sail back from Greywick you arrive in the channel just WEST of
            // the dock (Greywick lies west — so you come home FROM THE WEST), still heading east; board/
            // disembark at the cove dock. The coordinator re-points the switcher here on arrival. The arrival
            // sits within CoveDockZoneRadius of the dock zone so E disembarks the moment you're home.
            var coveArrival = new GameObject("CoveArrival");
            coveArrival.transform.position = CoveArrivalPos;   // just WEST of the dock head (arrive from the west)
            var coveAnchor = new GameObject("CoveRegionAnchor").AddComponent<RegionAnchor>();
            coveAnchor.Configure("region.coddle_cove", coveArrival.transform, dockZone.transform, disembarkPoint.transform);

            // Cove→Greywick passage: SAIL WEST to cross — Port Greywick lies west of the cove (canon map).
            // A wide, forgiving band down the WEST edge of the open water, reachable by sailing west out of
            // the dock; the only mover out here is the player's boat. Sailing west into it crosses to
            // Greywick, where you arrive still HEADING WEST (the hop preserves the boat's heading).
            var toGreywickGo = new GameObject("PassageToPortGreywick");
            toGreywickGo.transform.position = ToGreywickPassagePos;
            var toGreywickTrigger = toGreywickGo.AddComponent<BoxCollider2D>();
            toGreywickTrigger.isTrigger = true;
            toGreywickTrigger.size = new Vector2(3f, 28f);   // a tall west-edge band (forgiving, wide)
            var toGreywickPassage = toGreywickGo.AddComponent<RegionPassage>();
            SetRef(toGreywickPassage, "_target", greywickRegion);
            SetRef(toGreywickPassage, "_loader", loader);

            // --- DEMO PEOPLE & THE INHERITANCE OPENING (VS-21, world-content; additive — flagged for
            // lead-architect) -------------------------------------------------------------------------
            // Aunt Ginny + a neighbour by the cottage, Ned's logbook to read, a self-built dialogue panel,
            // the proximity INTERACT driver, and a light one-loop onboarding nudge. Everything sits UP BY
            // THE COTTAGE, well clear of the dock zone (0,-12), so the shared E key never fires both "talk"
            // and "board" (context-aware by proximity). Belt-and-braces, the open dialogue also raises the
            // Core InteractionGate which ControlSwitcher now honours (seam flagged for gameplay-systems).
            // Art is loaded fresh here (post-reload) and wired by serialized ref; null-safe if a sprite is
            // missing. The character/portrait/panel art is sliced (spriteMode Multiple) so it's loaded via
            // LoadSpriteAny (the single sub-sprite), not LoadArtSprite.

            // Dialogue panel (builds its own canvas in Awake; needs only the panel + nameplate art).
            var dialogueGo = new GameObject("DialogueUI");
            var presenter = dialogueGo.AddComponent<DialoguePresenter>();
            SetRef(presenter, "_panelSprite", LoadSpriteAny(ArtDialoguePanel));
            SetRef(presenter, "_nameplateSprite", LoadSpriteAny(ArtNamePlate));

            // Aunt Ginny — anchored by the cottage (no daily routine; routines are M2). Warm intro about
            // Uncle Ned, the dory he left, and a nudge to go fish. Finishing it sets the met_ginny flag.
            var ginnyGo = MakeNpc("AuntGinny", new Vector3(-2.2f, 4.2f, 0f), LoadSpriteAny(ArtGinny), waterSprite, new Color(0.78f, 0.55f, 0.62f));
            var ginny = ginnyGo.AddComponent<Interactable>();
            ConfigureInteractable(ginny, InteractKind.Talk, WorldStrings.GinnyName,
                LoadSpriteAny(ArtPortraitGinny), WorldStrings.ConvoGinny, OnboardingFlags.MetGinnyKey);

            // A neighbour, for warmth (optional). No neighbour portrait shipped → name + text only; no flag.
            var bramGo = MakeNpc("Neighbour", new Vector3(2.8f, 4.8f, 0f), LoadSpriteAny(ArtNeighbour), waterSprite, new Color(0.55f, 0.60f, 0.70f));
            var bram = bramGo.AddComponent<Interactable>();
            ConfigureInteractable(bram, InteractKind.Talk, WorldStrings.NeighbourName,
                null, WorldStrings.ConvoNeighbour, "");

            // "Ned's Unfinished Lines" — a readable logbook on the cottage step (no logbook art yet → a
            // small tinted marker). Framing the inheritance, bittersweet but hopeful. Sets read_logbook.
            var logbookGo = MakeNpc("NedsLogbook", new Vector3(-6.4f, 3.6f, 0f), null, waterSprite, new Color(0.62f, 0.47f, 0.30f));
            logbookGo.transform.localScale = new Vector3(0.6f, 0.8f, 1f); // a book-sized marker for the greybox
            var logbook = logbookGo.AddComponent<Interactable>();
            ConfigureInteractable(logbook, InteractKind.Read, WorldStrings.LogbookName,
                LoadSpriteAny(ArtPortraitNed), WorldStrings.ConvoLogbook, OnboardingFlags.ReadLogbookKey);

            // The proximity INTERACT driver: shows "E: …" near an interactable and runs the conversation.
            var interactorGo = new GameObject("WorldInteractor");
            var interactor = interactorGo.AddComponent<WorldInteractor>();
            SetRef(interactor, "_player", playerGo.transform);
            SetRef(interactor, "_presenter", presenter);
            SetRefArray(interactor, "_interactables", new Object[] { ginny, bram, logbook });

            // Light onboarding: one nudge through cast off → fish → return → sell, then it bows out and
            // persists 'onboarded' so the opening never re-triggers on reload.
            var onboardingGo = new GameObject("Onboarding");
            onboardingGo.AddComponent<OnboardingDirector>();

            // --- SAVE & REGISTER -----------------------------------------------------------
            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = EditorBuildSettings.scenes.ToList();
            if (!list.Any(s => s.path == ScenePath))
            {
                list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[GreyboxBuilder] Built Greybox.unity. Press Play: WASD = walk on foot. By the cottage, walk up to AUNT GINNY and press E to talk, or read NED'S LOGBOOK (E) — the inheritance opening, with a light nudge through your first loop. Aboard the DORY (hand-rowed, independent oars): W/S = both oars ahead/astern, A = port-oar stroke, D = starboard-oar stroke (one oar rows = bow swings the OTHER way; A or D alone with no W/S = spin in place), Space = brace oars (brake). Buy the Punt (engine) and it's W/S throttle + A/D steer. E = board / disembark, Space = cast then HOLD to reel / RELEASE to ease, B = sell your hold, P = buy the Punt (₲1,800).");
            EditorUtility.DisplayDialog("Hidden Harbours",
                "Greybox scene built and opened.\n\nPress Play, then:\n• WASD / arrows = walk on foot\n• Aboard the DORY (hand-rowed — the two oars move independently): W/S = both oars ahead/astern · A = port-oar stroke · D = starboard-oar stroke (one oar rows → the bow swings the OTHER way; A or D alone with no W/S spins in place) · Space = brace the oars to brake. Both oars together track straight.\n• Buy the Punt (engine helm): W/S = throttle, A/D = steer (the old controls).\n• E = board at the dock / disembark · B = sell your hold · P = buy the Punt (₲1,800)\n\nNote: Space also casts the fishing line — handy, you brace to hold station while you fish.", "Fair winds");
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

        // Tier-1 Punt stats (design/boats-and-navigation.md §1.1) + greybox-tuned propulsion. One place
        // so the values live on the asset, never duplicated in C#. Propulsion is a touch stronger and
        // drier than the greybox Dory (EnginePower 500 / drag 120·320 / WindExposure 0.6) for a bigger,
        // steadier hull. Seaworthiness "4 — Popple" maps to SeaState.Lively, same as the Dory.
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
            // Reload so callers wire the PERSISTED asset (a freshly-created instance may not
            // serialize into the scene reliably). This is what fixes "No GameConfig assigned".
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

        static void SetTideProfile(Component env, float mean, float amp, float phase)
        {
            var so = new SerializedObject(env);
            var tp = so.FindProperty("_activeTideProfile");
            if (tp == null) return;
            tp.FindPropertyRelative("MeanLevel").floatValue = mean;
            tp.FindPropertyRelative("Amplitude").floatValue = amp;
            tp.FindPropertyRelative("PhaseHours").floatValue = phase;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Load a final art sprite if it has been imported; null if the project is still greybox-only.
        static Sprite LoadArtSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        // Like LoadArtSprite but also handles single-frame sliced sheets (spriteMode Multiple): the
        // VS-21 character/portrait/panel art each carry one sub-sprite (e.g. DialoguePanel_0), so
        // LoadAssetAtPath<Sprite> returns null and we fall back to the first sub-sprite. Null if absent.
        static Sprite LoadSpriteAny(string path)
        {
            var direct = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (direct != null) return direct;
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                                 .OrderBy(s => SpriteIndex(s.name)).FirstOrDefault();
        }

        // A standing world NPC / marker: a SpriteRenderer above the ground (just under the player at 10),
        // with a tinted-square fallback so the greybox still builds before the art is imported.
        static GameObject MakeNpc(string name, Vector3 pos, Sprite sprite, Sprite fallback, Color fallbackColor)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 9;
            if (sprite != null) { sr.sprite = sprite; go.transform.localScale = Vector3.one; }
            else { sr.sprite = fallback; sr.color = fallbackColor; go.transform.localScale = new Vector3(1f, 2f, 1f); }
            return go;
        }

        // Set every Interactable field in one SerializedObject pass (the builder's persist-the-refs
        // convention, extended to the string/enum fields the dialogue needs).
        static void ConfigureInteractable(Interactable it, InteractKind kind, string speaker,
                                          Sprite portrait, string conversationId, string completionFlag)
        {
            var so = new SerializedObject(it);
            so.FindProperty("_kind").enumValueIndex = (int)kind;
            so.FindProperty("_speaker").stringValue = speaker;
            so.FindProperty("_portrait").objectReferenceValue = portrait;
            so.FindProperty("_conversationId").stringValue = conversationId;
            so.FindProperty("_completionFlag").stringValue = completionFlag;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Load the sub-sprites of a sliced sheet (Sprite Mode Multiple), ordered by their _N suffix so
        // index 0..N-1 matches the slice order (e.g. FisherSheet_0..11).
        static Sprite[] LoadSheetFrames(string path)
            => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                            .OrderBy(s => SpriteIndex(s.name)).ToArray();

        static int SpriteIndex(string spriteName)
        {
            int u = spriteName.LastIndexOf('_');
            return (u >= 0 && int.TryParse(spriteName.Substring(u + 1), out int n)) ? n : 0;
        }

        // A tiled ground/dock patch (mirrors the sea backdrop's tiled SpriteRenderer). Falls back to a
        // tinted square if the tile art isn't imported, so the greybox still builds.
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

        // One oar of the row rig: an oarlock PIVOT transform at the gunwale, with Oar.png as a child
        // offset so the oar's fulcrum (≈0.33 along the 1.75 m loom from the handle/LeftCenter pivot) sits
        // on the pivot origin — so rotating the returned transform swings the oar about its oarlock
        // (imported-assets.md Batch 6). The port oar is mirrored (flipX). Returns the pivot transform for
        // BoatRowAnimator to rotate; falls back to a tinted bar so the greybox still builds without the art.
        static Transform MakeOar(Transform parent, string name, Vector2 oarlockLocalPos, bool mirror,
                                 Sprite oarSprite, Sprite fallback)
        {
            const float fulcrumFromHandle = 0.56f;   // 0.33 × 1.75 m loom, handle → fulcrum (toward blade)

            var pivot = new GameObject(name + "Pivot");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = new Vector3(oarlockLocalPos.x, oarlockLocalPos.y, 0f);

            var oar = new GameObject(name);
            oar.transform.SetParent(pivot.transform, false);
            var osr = oar.AddComponent<SpriteRenderer>();
            osr.sortingOrder = 1;                    // over the hull, under the rower
            if (oarSprite != null)
            {
                osr.sprite = oarSprite;
                osr.flipX = mirror;                  // port oar is the mirror of starboard
                // Shift the sprite so the fulcrum lands on the pivot origin (mirrored for the port oar).
                oar.transform.localPosition = new Vector3(mirror ? fulcrumFromHandle : -fulcrumFromHandle, 0f, 0f);
            }
            else
            {
                osr.sprite = fallback; osr.color = new Color(0.55f, 0.40f, 0.24f);
                oar.transform.localScale = new Vector3(3.5f, 0.4f, 1f);   // ~1.75 m × 0.2 m bar
                oar.transform.localPosition = new Vector3(mirror ? 0.9f : -0.9f, 0f, 0f);
            }
            return pivot.transform;
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
