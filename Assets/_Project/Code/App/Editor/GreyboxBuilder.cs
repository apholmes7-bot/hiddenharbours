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
        const string ArtSea     = "Assets/_Project/Art/Tilesets/Water/SeaTile.png"; // final tile (VS-24)
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
            cam.orthographic = true; cam.orthographicSize = 9f; // PPC recomputes this at runtime (~16 m tall)
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.13f, 0.18f);
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();
            // VS-23: lock the Pixel-Perfect camera (PPU 32, pixel-snapping) so there's no sub-pixel
            // shimmer as the follow-cam tracks the dory. Shared art-pipeline convention (bible §3.7).
            ArtCameraSetup.ConfigurePixelPerfect(camGo);

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
                EditorUtility.SetDirty(dory);
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
            doryGo.AddComponent<DevBoatInput>();
            var fishing = doryGo.AddComponent<FishingController>();
            doryGo.AddComponent<DevFishingInput>();
            SetRef(boat, "_hull", dory);
            SetRef(hold, "_hull", dory);
            SetRef(fishing, "_holdProvider", doryGo);
            SetRefArray(fishing, "_regionFish", fish);

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

            // Camera follows the dory so it stays on screen as you sail.
            camGo.AddComponent<CameraFollow>().Target = doryGo.transform;

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

            Debug.Log("[GreyboxBuilder] Built Greybox.unity. Press Play: W/Up = throttle, A/D = steer, Space = cast, B = sell your hold, P = buy the Punt (₲1,800).");
            EditorUtility.DisplayDialog("Hidden Harbours",
                "Greybox scene built and opened.\n\nPress Play, then:\n• W / Up = throttle\n• A / D = steer\n• Space = cast for a fish\n• B = sell your hold at the wharf\n• P = buy the Punt at the Shipwright (₲1,800)\n\nWatch the Console for catches, sales, and purchases.", "Fair winds");
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
