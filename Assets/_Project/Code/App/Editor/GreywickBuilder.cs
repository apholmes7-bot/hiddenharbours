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
        const string ArtSprites  = "Assets/_Project/Art/Sprites";
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
            camGo.transform.position = new Vector3(0f, 0f, -10f);
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

            // --- QUAY (the land the town sits on, along the north) --------------------------
            MakeTiledGround("Quay",      LoadSpriteAny(ArtGrass),     new Vector2(0f, 7f), new Vector2(28f, 8f), -7, waterSprite, new Color(0.40f, 0.46f, 0.40f));
            MakeTiledGround("QuayEdge",  LoadSpriteAny(ArtSand),      new Vector2(0f, 3.5f), new Vector2(28f, 3f), -6, waterSprite, new Color(0.62f, 0.58f, 0.46f));
            // The public wharf deck reaching out into the deep harbour.
            MakeTiledGround("PublicWharf", LoadSpriteAny(ArtWharfDeck), new Vector2(0f, 0f), new Vector2(6f, 8f), -5, waterSprite, new Color(0.55f, 0.40f, 0.24f));

            // Pilings down the wharf sides.
            var postSprite = LoadSpriteAny(ArtWharfPost);
            for (int i = 0; i < 3; i++)
            {
                float py = 1f - i * 3f; // 1, -2, -5
                MakePost(postSprite, new Vector2(-3f, py), waterSprite);
                MakePost(postSprite, new Vector2( 3f, py), waterSprite);
            }

            // --- BUILDINGS (services + a couple of flavour houses) --------------------------
            var shipwrightShed = MakeBuilding("ShipwrightShed",   LoadSpriteAny(ArtShipwright), new Vector2(-7f, 6f),  waterSprite, new Color(0.50f, 0.42f, 0.34f));
            var fishStall      = MakeBuilding("FishBuyerStall",   LoadSpriteAny(ArtFishStall),  new Vector2(-2.5f, 6f),waterSprite, new Color(0.42f, 0.50f, 0.52f));
            MakeBuilding("GreywickHouseRed",  LoadSpriteAny(ArtHouseRed),  new Vector2(3.5f, 6.3f), waterSprite, new Color(0.55f, 0.34f, 0.30f)); // flavour
            MakeBuilding("GreywickHouseTeal", LoadSpriteAny(ArtHouseTeal), new Vector2(7.5f, 6f),   waterSprite, new Color(0.30f, 0.48f, 0.48f)); // flavour

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
            // TODO (needs the origin scene/bootstrap): SetRef(sell, "_holdProvider", playerBoat);
            //                                          SetRef(sell, "_walletProvider", gameRoot);

            // Shipwright shed: buy the Punt by id (+ dev 'P' to buy). Wallet provider left unwired (TODO).
            var shipwright = shipwrightShed.AddComponent<Shipwright>();
            shipwrightShed.AddComponent<DevBuyInput>();      // RequireComponent(Shipwright) — present
            SetRef(shipwright, "_offer", puntOffer);
            // TODO (needs the origin scene/bootstrap): SetRef(shipwright, "_walletProvider", gameRoot);

            // --- REGION SCENE-LOAD PATH -----------------------------------------------------
            var loaderGo = new GameObject("RegionSceneLoader");
            var loader = loaderGo.AddComponent<RegionSceneLoader>();
            SetRefArray(loader, "_regions", new Object[] { greywick, cove });
            SetString(loader, "_currentSceneName", SceneName);

            // Return passage at the harbour mouth (south): sail/walk into it to head back to the Cove.
            // The matching Cove→Greywick passage lives in the cove scene (GreyboxBuilder) — TODO there.
            var passageGo = new GameObject("PassageToCoddleCove");
            passageGo.transform.position = new Vector3(0f, -11f, 0f);
            var trigger = passageGo.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(10f, 2f);
            var passage = passageGo.AddComponent<RegionPassage>();
            SetRef(passage, "_target", cove);
            SetRef(passage, "_loader", loader);

            // --- SAVE & REGISTER ------------------------------------------------------------
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterScene(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[GreywickBuilder] Built Greywick.unity — Port Greywick (services region). The wharf " +
                      "carries the Fish Buyer (B) + Shipwright (P), reused by id. Region scene loads " +
                      "additively via RegionSceneLoader; the return passage heads back to Coddle Cove. " +
                      "TODO: wire the player/wallet across the transition (needs the origin/bootstrap scene).");
            EditorUtility.DisplayDialog("Hidden Harbours",
                "Port Greywick scene built.\n\nIt's a SEPARATE region scene (services, not a town — M2 " +
                "grows it):\n• Public wharf with the Fish Buyer + Shipwright\n• A couple of flavour houses\n" +
                "• A return passage to Coddle Cove\n\nLoaded additively by RegionSceneLoader. The " +
                "Cove→Greywick hop + carrying the player across are TODOs (need the cove/bootstrap scene).",
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
        }

        static GameObject MakeBuilding(string name, Sprite sprite, Vector2 pos, Sprite fallback, Color fallbackColor)
        {
            var go = new GameObject(name);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 2;
            if (sprite != null) { sr.sprite = sprite; go.transform.localScale = Vector3.one; }
            else { sr.sprite = fallback; sr.color = fallbackColor; go.transform.localScale = new Vector3(5f, 5f, 1f); }
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
