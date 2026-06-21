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
        const string ArtSprites = "Assets/_Project/Art/Sprites";
        const string ArtDory    = "Assets/_Project/Art/Boats/Dory.png";          // final sprite (VS-26)
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
        const string Scenes     = "Assets/_Project/Scenes";
        const string ScenePath  = Scenes + "/Greybox.unity";

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
            var dorySprite = LoadArtSprite(ArtDory);
            if (dorySprite != null)
            {
                sr.sprite = dorySprite;                    // 64×144 px @ PPU 32 = 2 m × 4.5 m, bow-up
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
            var hold = doryGo.AddComponent<ShipHold>();
            var devBoat = doryGo.AddComponent<DevBoatInput>();
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
            doryGo.transform.position = new Vector3(0f, -13.8f, 0f);

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

            // Camera follows the PLAYER at the tighter on-foot framing (data-driven; same pixel-perfect
            // approach as the boat tiers). Step 2 switches the target/framing between player and boat.
            var cameraFollow = camGo.AddComponent<CameraFollow>();
            cameraFollow.Target = playerGo.transform;
            var cfSo = new SerializedObject(cameraFollow);
            var cfWorldH = cfSo.FindProperty("_worldHeightMeters");
            if (cfWorldH != null) { cfWorldH.floatValue = CameraFollow.OnFootWorldHeightMeters; cfSo.ApplyModifiedPropertiesWithoutUndo(); }

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

            Debug.Log("[GreyboxBuilder] Built Greybox.unity. Press Play: WASD = walk the island on foot, Space = cast then HOLD to reel / RELEASE to ease, B = sell your hold, P = buy the Punt (₲1,800). (Boarding the moored Dory comes in the next step.)");
            EditorUtility.DisplayDialog("Hidden Harbours",
                "Greybox scene built and opened.\n\nPress Play, then:\n• WASD / arrows = walk the island on foot\n• Space = cast, then HOLD to reel & RELEASE to ease — pulse to land the fish before the line snaps\n• B = sell your hold at the wharf\n• P = buy the Punt at the Shipwright (₲1,800)\n\nThe Dory is moored at the dock end — boarding & sailing come in the next step.", "Fair winds");
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

        // A single dock piling.
        static void MakePost(Sprite sprite, Vector2 pos, Sprite fallback)
        {
            var go = new GameObject("WharfPost");
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 3;
            if (sprite != null) { sr.sprite = sprite; go.transform.localScale = Vector3.one; }
            else { sr.sprite = fallback; sr.color = new Color(0.45f, 0.32f, 0.20f); go.transform.localScale = new Vector3(0.5f, 1.5f, 1f); }
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
            foreach (var f in new[] { DataConfig, DataBoats, DataFish, DataShip, ArtSprites, Scenes })
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
