#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.Boats;               // BoatHullDef (the carried Dory/Punt hulls handed to the core)
using HiddenHarbours.Fishing;             // FishSpeciesDef (the clam the on-foot player's fishing reads)
using HiddenHarbours.App;                 // RegionAnchor — St Peters' travel bind point (the persistent rig rebinds here)

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// One-click <b>St Peters Island</b> — the owner-ratified OPENING region as GREYBOX (no final art),
    /// built by its own builder so the cove (GreyboxBuilder) and Greywick (GreywickBuilder) scenes are
    /// untouched (scene per region, CLAUDE.md §3). Menu: Hidden Harbours ▸ Build St Peters Scene.
    /// Re-runnable (idempotent on the assets).
    ///
    /// <para><b>The opening (this builder authors the PLACES; gameplay-systems builds the ACTIONS).</b>
    /// You start on St Peters with a shovel + bucket → at LOW tide the sandbar/seabed bares → dig clams on
    /// the exposed flats → walk the emerged sandbar PATH to Greywick (first trip, on foot) → at Greywick
    /// sell clams + buy a cod licence + rod, save up, buy the DAMAGED dory + repair → sail home to the cove.
    /// This builder lays the island, the coast, the tide-gated sandbar (a walk path at low / boat channel at
    /// high), the clam-holes, and the START spawn. The dig/walk/gear actions are gameplay-systems'.</para>
    ///
    /// <para><b>The showcase is the TIDE.</b> A <see cref="TidalTerrain"/> authors the elevation zones so the
    /// sandbar is a ridge JUST BELOW high water (covered at high, exposing as the tide falls — widest flat at
    /// low) WITH a deeper CHANNEL cut through it (boat-crossable at higher tide, narrowing as it falls). The
    /// two are inverse over the tide. St Peters runs a BIG tide amplitude (~3.5 m) vs the cove's gentle one,
    /// so the bar visibly bares and floods (P1 at its purest, the kindest tide-gate — being cut off costs
    /// only time). The terrain registers itself into <see cref="GameServices.TidalTerrain"/> at runtime.</para>
    ///
    /// <para>St Peters is now the GAME'S START scene (#64 made it the entry point), so it stands up the
    /// shared PERSISTENT CORE via <see cref="PersistentCoreBuilder"/> — the same rig the cove builds: the
    /// services root (clock/environment/wallet + HUD), the controllable on-foot Player (spawned at
    /// <see cref="StartSpawnPos"/>), the moored hand-rowed Dory, the follow camera at the on-foot framing,
    /// the control switcher, and the travel rig (loader + coordinator) so the sandbar passage to Greywick
    /// works. The extracted helper keeps the core IDENTICAL between the two start scenes (it can't diverge).
    /// This builder then authors only the REGION content around that core (island/coast/sandbar/clam-holes/
    /// passage/anchor). The dig/walk/gear ACTIONS remain gameplay-systems'.</para>
    ///
    /// <para>FLAG lead-architect (follow-ups, not this PR): (1) the cove (GreyboxBuilder) STILL authors its
    /// own copy of the core because it was the old start — now that St Peters is the start and the cove is a
    /// sail-to region, the cove must be DEMOTED to a plain region (drop its core authoring, give it a
    /// RegionAnchor like Greywick) before the full sail-home chain is tested. (2) The long-term fix is the
    /// dedicated Bootstrap scene (VS-01) carrying the one core that additively loads the start region.</para>
    /// </summary>
    public static class StPetersBuilder
    {
        const string DataRegions = "Assets/_Project/Data/Regions";
        const string DataConfig  = "Assets/_Project/Data/Config";   // shared GameConfig (cove builder authors it)
        const string DataBoats   = "Assets/_Project/Data/Boats";    // the Dory + Punt hulls (the carried rig)
        const string DataFish    = "Assets/_Project/Data/Fish";     // the soft-shell clam (the flats' catch)
        const string ArtSprites  = "Assets/_Project/Art/Sprites";
        const string ArtSea      = "Assets/_Project/Art/Tilesets/Water/SeaTile.png";
        const string ArtGrass    = "Assets/_Project/Art/Tilesets/Grass.png";
        const string ArtSand     = "Assets/_Project/Art/Tilesets/Sand.png";
        const string ArtCottage  = "Assets/_Project/Art/Sprites/Buildings/Cottage.png";
        const string Scenes      = "Assets/_Project/Scenes";
        const string SceneName   = "StPeters";
        const string ScenePath   = Scenes + "/" + SceneName + ".unity";

        // --- St Peters region tunables (authored data; mirrored onto the RegionDef + TidalTerrain + Env) ---
        // The opening's defining feature is a BIG tide vs the cove's gentle 1.6 m — the bar must visibly bare
        // and flood. Mean 0 (chart datum centred), amplitude 3.5 m → water swings ≈ -3.5 .. +3.5 m.
        public const float TideMean = 0f;
        public const float TideAmplitude = 3.5f;
        public const float TidePhaseHours = 1f;

        // --- TidalTerrain elevation zones (authored geometry; single source of truth shared with the
        // EditMode test). Island plateau is high (always exposed). The sandbar crest sits JUST BELOW high
        // water (covers at high, bares as the tide falls). The channel bed is between crest and deep floor
        // (a gut a boat crosses at higher tide). Deep harbour is the low floor everywhere else. ---
        public const float DeepHarbourElevation   = -4f;
        public static readonly Vector2 IslandCenter = new Vector2(-40f, 0f);
        public const float IslandRadius            = 22f;
        public const float IslandFalloff          = 10f;
        public const float IslandElevation         = 6f;     // dry at every tide
        public static readonly Vector2 SandbarFrom  = new Vector2(-22f, 0f); // toward the island
        public static readonly Vector2 SandbarTo    = new Vector2(34f, 0f);  // toward Greywick
        public const float SandbarHalfWidth        = 9f;
        public const float SandbarCrestElevation    = 1.6f;   // < high water (~2.5) → covers at high, bares falling
        public const float ChannelAlong            = 0.62f;
        public const float ChannelHalfWidth        = 4.5f;
        public const float ChannelBedElevation      = -0.6f;  // a gut: boat-crossable high, narrows as tide falls

        // Player START spawn (on the island, by the slip). The walk path runs east along the sandbar.
        public static readonly Vector3 StartSpawnPos = new Vector3(-40f, -2f, 0f);
        // Where the walk path reaches Greywick — a forgiving band at the Greywick (east) end of the bar.
        public static readonly Vector3 ToGreywickPassagePos = new Vector3(36f, 0f, 0f);

        // --- St Peters DOCK / mooring geometry (the persistent rig binds here; mirrors the cove pattern) ---
        // The uncle's dory is moored in the deep water off the island's SOUTH coast (the island plateau sits
        // above (-40,0) radius ~22, so south of ≈ -20 is open water). Board/disembark at this slip once the
        // dory is yours (the opening's first trip is on foot — this is for the sail home). The arrival point
        // sits within DockZoneRadius of the dock zone so the persistent ControlSwitcher's pure-distance
        // disembark test registers the moment you're home (the cove/Greywick proven pattern — don't regress).
        public const float DockZoneRadius = 3.5f;                                  // ControlSwitcher's default _zoneRadius
        public static readonly Vector3 DoryMooredPos  = new Vector3(-40f, -26f, 0f); // off the island's south coast (deep water)
        public static readonly Vector3 DockZonePos    = new Vector3(-40f, -26f, 0f); // the slip head — dock here
        public static readonly Vector3 DisembarkPos   = new Vector3(-40f, -21f, 0f); // step onto the island's south shore
        public static readonly Vector3 ArrivalPos     = new Vector3(-40f, -25f, 0f); // sail home: park just off the slip, in range

        [MenuItem("Hidden Harbours/Build St Peters Scene")]
        public static void Build()
        {
            EnsureFolders();

            // --- DATA: the St Peters region + the regions it links to (by stable id) -------------------
            var stPeters = LoadOrCreate<RegionDef>(DataRegions + "/StPeters.asset", r =>
            {
                r.Id = "region.st_peters"; r.DisplayName = "St Peters Island"; r.SceneName = SceneName;
                r.IsDeepHarbour = false; r.HarbourDepthMeters = 2f;
                r.TideMeanLevel = TideMean; r.TideAmplitude = TideAmplitude; r.TidePhaseHours = TidePhaseHours;
                r.UnlockFlag = "";   // the opening — always reachable, no gate
                r.SpawnFishIds = new[] { "fish.soft_shell_clam" };  // the flats' by-hand catch (gated by exposure)
                r.Description = "The home island where the game begins: a tiny weathered coast cut off from " +
                                "the mainland except when the big tide bares the sandbar. Dig clams on the " +
                                "flats at low water, walk the bar to Greywick, earn your way to the dory.";
            });
            // Greywick + cove regions referenced for the walk passage / loader (created here if absent;
            // GreyboxBuilder/GreywickBuilder author the canonical versions under the same stable ids).
            var greywick = LoadOrCreate<RegionDef>(DataRegions + "/PortGreywick.asset", r =>
            {
                r.Id = "region.port_greywick"; r.DisplayName = "Port Greywick"; r.SceneName = "Greywick";
                r.IsDeepHarbour = true; r.HarbourDepthMeters = 6f;
                r.TideMeanLevel = 0f; r.TideAmplitude = 0.8f; r.TidePhaseHours = 2f;
                r.Description = "The market town: a deep, sheltered harbour where the coast's business gets done.";
            });

            // The persistent-core data refs (shared stable assets; the cove builder authors the canonical
            // versions — created here if absent so this builder is self-sufficient/re-runnable). LoadOrCreate
            // reloads the PERSISTED asset so they serialize into the scene rather than saving as "None".
            var config = LoadOrCreate<GameConfig>(DataConfig + "/GameConfig.asset");
            var dory   = AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/Dory.asset");
            var punt   = AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/Punt.asset");
            var clam   = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(DataFish + "/SoftShellClam.asset");

            // --- SCENE ----------------------------------------------------------------------------------
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- SEA backdrop (the deep water surrounding the island + bar) -----------------------------
            var waterSprite = MakeSquareSprite(ArtSprites + "/Square.png");
            var seaTile = LoadSpriteAny(ArtSea);
            var water = new GameObject("Sea");
            var wsr = water.AddComponent<SpriteRenderer>();
            wsr.sortingOrder = -10;
            if (seaTile != null)
            {
                wsr.sprite = seaTile;
                wsr.drawMode = SpriteDrawMode.Tiled;
                wsr.size = new Vector2(160f, 120f);
                water.transform.localScale = Vector3.one;
            }
            else
            {
                wsr.sprite = waterSprite; wsr.color = new Color(0.15f, 0.27f, 0.34f);
                water.transform.localScale = new Vector3(160f, 120f, 1f);
            }

            // Reload data assets from disk before wiring refs (an intervening import can invalidate the
            // in-memory instances — the gotcha the cove/Greywick builders guard against).
            stPeters = AssetDatabase.LoadAssetAtPath<RegionDef>(DataRegions + "/StPeters.asset");
            greywick = AssetDatabase.LoadAssetAtPath<RegionDef>(DataRegions + "/PortGreywick.asset");
            config   = AssetDatabase.LoadAssetAtPath<GameConfig>(DataConfig + "/GameConfig.asset");
            dory     = AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/Dory.asset");
            punt     = AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/Punt.asset");
            clam     = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(DataFish + "/SoftShellClam.asset");

            // --- PERSISTENT CORE (THE FIX) --------------------------------------------------------------
            // St Peters is the START scene, so it stands up the SAME persistent rig the cove builds — a
            // controllable on-foot Player at the START spawn, the moored hand-rowed Dory, the follow camera
            // at the on-foot framing, the services (clock/environment/wallet → the tide actually advances),
            // the control switcher, and the travel rig. Extracted to PersistentCoreBuilder so the two start
            // scenes can't diverge. The walk is TIDE-GATED here (St Peters' falling-tide wading edge, P1).
            var core = PersistentCoreBuilder.Build(new PersistentCoreBuilder.Params
            {
                Config           = config,
                StartDory        = dory,
                PuntHull         = punt,
                RegionFish       = clam != null ? new[] { clam } : null,
                Square           = waterSprite,
                CameraBackground = new Color(0.07f, 0.14f, 0.18f),   // St Peters' cool dawn water
                PlayerStartPos   = StartSpawnPos,
                BoatMooredPos    = DoryMooredPos,
                TideGatedWalk    = true,
                CurrentSceneName = SceneName,
                TideMean         = TideMean,        // St Peters' BIG tide (±3.5 m) so the bar bares + floods
                TideAmplitude    = TideAmplitude,
                TidePhaseHours   = TidePhaseHours,
            });

            // --- ISLAND (the high home ground; greybox grass + sand beach) ------------------------------
            // Tiled ground patches over the island plateau (centre (-40,0), radius ~22). Sand rim under grass.
            MakeTiledGround("IslandBeach",  LoadSpriteAny(ArtSand),  new Vector2(-40f, 0f), new Vector2(26f, 26f), -8, waterSprite, new Color(0.86f, 0.79f, 0.55f));
            MakeTiledGround("IslandGround", LoadSpriteAny(ArtGrass), new Vector2(-40f, 0f), new Vector2(20f, 20f), -7, waterSprite, new Color(0.38f, 0.58f, 0.32f));

            // The cottage / the hard where the uncle's dory waits (greybox marker — the actual damaged-dory
            // OFFER lives at the Greywick Shipwright this round; the slip here is set dressing for the opening).
            var cottageGo = new GameObject("IslandCottage");
            cottageGo.transform.position = new Vector3(-44f, 4f, 0f);
            var cottageSr = cottageGo.AddComponent<SpriteRenderer>();
            cottageSr.sortingOrder = 2;
            var cottageSprite = LoadSpriteAny(ArtCottage);
            if (cottageSprite != null) { cottageSr.sprite = cottageSprite; cottageGo.transform.localScale = Vector3.one; }
            else { cottageSr.sprite = waterSprite; cottageSr.color = new Color(0.70f, 0.50f, 0.40f); cottageGo.transform.localScale = new Vector3(6f, 6f, 1f); }

            // --- SANDBAR (greybox visual of the tide-gated flats running island → Greywick) -------------
            // A sand strip along the bar centre-line (From (-22,0) → To (34,0)). It is VISUAL only — the
            // walkable/boatable state is decided by the TidalTerrain elevation + the water level, not by this
            // sprite. A subtle channel-coloured patch marks where the boat channel is cut through the bar.
            Vector2 barMid = (SandbarFrom + SandbarTo) * 0.5f;
            float barLen = Vector2.Distance(SandbarFrom, SandbarTo);
            MakeTiledGround("Sandbar", LoadSpriteAny(ArtSand), barMid, new Vector2(barLen, SandbarHalfWidth * 2f), -9, waterSprite, new Color(0.80f, 0.74f, 0.56f));

            Vector2 channelPos = Vector2.Lerp(SandbarFrom, SandbarTo, ChannelAlong);
            var channelGo = new GameObject("BoatChannelMarker");
            channelGo.transform.position = new Vector3(channelPos.x, channelPos.y, 0f);
            var chSr = channelGo.AddComponent<SpriteRenderer>();
            chSr.sprite = waterSprite; chSr.color = new Color(0.20f, 0.36f, 0.44f, 0.85f); chSr.sortingOrder = -8;
            channelGo.transform.localScale = new Vector3(ChannelHalfWidth * 2f, SandbarHalfWidth * 2f + 2f, 1f);

            // --- TIDAL TERRAIN (THE SHOWCASE — authored elevation zones) --------------------------------
            // The world's height map for the region: island high (always exposed), sandbar crest just below
            // high water (bares as the tide falls), a deeper channel cut through (boat-crossable high), deep
            // harbour elsewhere. Registers itself into GameServices.TidalTerrain on enable so gameplay's
            // on-foot walkability + the future water shader read it through Core. Values mirror the public
            // constants above (single source of truth shared with the EditMode zone test).
            var terrainGo = new GameObject("TidalTerrain");
            var terrain = terrainGo.AddComponent<TidalTerrain>();
            ConfigureTidalTerrain(terrain);

            // --- CLAM-HOLE spots (positions only; gameplay implements the dig) --------------------------
            // Dropped on the sandbar flats + along the island coast. Each yields fish.soft_shell_clam by id;
            // gameplay reads each spot's tide-exposure (TidalTerrain + TidalExposure) to gate the dig.
            var clamRoot = new GameObject("ClamHoles");
            Vector2[] clamPositions =
            {
                // On the bar flats (bare at low water — the showcase dig ground).
                new Vector2(-10f,  3f), new Vector2(-4f, -4f), new Vector2(2f,  4f),
                new Vector2(8f, -3f),   new Vector2(16f, 5f),  new Vector2(22f, -5f),
                // Along the island's own intertidal coast (a few near home).
                new Vector2(-26f, -8f), new Vector2(-30f, 9f), new Vector2(-20f, -10f),
            };
            foreach (var p in clamPositions)
                MakeClamSpot(clamRoot.transform, p, "fish.soft_shell_clam", waterSprite);

            // --- THE MOORED DORY'S SLIP (set dressing; the persistent Dory floats at DoryMooredPos) ------
            // The real, controllable Player is now spawned by the PersistentCoreBuilder above at
            // StartSpawnPos, and the persistent Dory floats off the south coast. This is a small marker on
            // the south shore showing where the uncle's dory is moored / where you board once she's yours.
            var slipGo = new GameObject("DorySlipMarker");
            slipGo.transform.position = new Vector3(DisembarkPos.x, DisembarkPos.y, 0f);
            var slipSr = slipGo.AddComponent<SpriteRenderer>();
            slipSr.sprite = waterSprite; slipSr.color = new Color(0.55f, 0.40f, 0.24f, 0.85f); slipSr.sortingOrder = -6;
            slipGo.transform.localScale = new Vector3(2.5f, 1.2f, 1f);

            // --- REGION DISPLAY NAME (the world registrar → Core) ---------------------------------------
            // Register "St Peters Island" / "Port Greywick" so the UI crossing-card resolves them by scene
            // name or id without referencing World (the RegionDisplayNames seam, #59/#54).
            var namesGo = new GameObject("RegionDisplayNames");
            var registrar = namesGo.AddComponent<RegionDisplayNameRegistrar>();
            SetRefArray(registrar, "_regions", new Object[] { stPeters, greywick });

            // --- REGION SCENE-LOAD PATH + the WALK PASSAGE to Greywick ----------------------------------
            // The persistent travel rig (loader + coordinator) was built with the core; here we fill the
            // loader's region list and wire this region's passage to it. Consistent with the TidalTerrain:
            // the bar is a WALK path at low water (the first trip is on foot). The passage band sits at the
            // Greywick (east) end of the bar; triggering is forgiving (greybox) — gameplay gates the actual
            // crossing on the bar being EXPOSED (TidalExposure) and on the player being on foot.
            var loader = core.Loader;
            SetRefArray(loader, "_regions", new Object[] { stPeters, greywick });

            var passageGo = new GameObject("PassageToPortGreywick");
            passageGo.transform.position = ToGreywickPassagePos;
            var trigger = passageGo.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(3f, SandbarHalfWidth * 2f);   // a band across the bar's Greywick end
            var passage = passageGo.AddComponent<RegionPassage>();
            SetRef(passage, "_target", greywick);
            SetRef(passage, "_loader", loader);

            // --- ST PETERS DOCK + ARRIVAL ANCHOR (the persistent rig binds here on the sail home) --------
            // St Peters' own board/dock geometry, mirroring the cove/Greywick pattern. The persistent
            // ControlSwitcher starts pointed at this slip so you can board the moored Dory once she's yours;
            // the RegionAnchor lets the RegionTravelCoordinator re-bind the rig here when you sail back from
            // the cove. The arrival sits within DockZoneRadius of the dock zone (the proven disembark
            // geometry — don't regress #52). Disembark steps onto the island's south shore.
            var dockZone = new GameObject("StPetersDockZone");
            dockZone.transform.position = DockZonePos;
            var disembark = new GameObject("StPetersDisembark");
            disembark.transform.position = DisembarkPos;
            var arrival = new GameObject("StPetersArrival");
            arrival.transform.position = ArrivalPos;

            // Point the persistent switcher's dock at this slip so boarding works in the start region.
            core.Switcher.SetDock(dockZone.transform, disembark.transform);

            var anchor = new GameObject("StPetersRegionAnchor").AddComponent<RegionAnchor>();
            anchor.Configure("region.st_peters", arrival.transform, dockZone.transform, disembark.transform);

            // --- SAVE & REGISTER ------------------------------------------------------------------------
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterScene(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[StPetersBuilder] Built StPeters.unity — the OPENING + START region (greybox), now " +
                      "with the PERSISTENT CORE so it's playable: press Play and you control the on-foot " +
                      "fisher at the START spawn (WASD), the camera follows at the on-foot framing, and the " +
                      "clock/tide run (GameServices online → the tide advances + the bar bares/floods). The " +
                      "moored hand-rowed Dory floats off the south coast (board at the slip once she's " +
                      "yours). Island = high (always exposed); the SANDBAR bridges it to Greywick as a " +
                      "tide-gated path: the crest (1.6 m) bares as the BIG tide (±3.5 m) falls, while a " +
                      "deeper CHANNEL (-0.6 m) stays boat-crossable at higher tide. Clam-holes on the flats " +
                      "(fish.soft_shell_clam, gated by exposure). The walk passage at the Greywick end leads " +
                      "on. StPeters is now BUILD-INDEX-0 (the start scene). RE-RUN this builder + " +
                      "GreywickBuilder, then open StPeters.unity and press Play.");
            EditorUtility.DisplayDialog("Hidden Harbours",
                "St Peters Island built — now a PLAYABLE START scene (greybox).\n\nPress Play:\n• You control " +
                "the on-foot fisher (WASD / arrows) at the start spawn; the camera follows.\n• The clock + " +
                "tide RUN — watch the sandbar bare as the big tide (±3.5 m) falls (the tide-reveal is the " +
                "point).\n• The hand-rowed Dory is moored off the south coast (board at the slip once she's " +
                "yours).\n• Walk the bared bar east to reach Greywick.\n\nThe dig/walk/gear ACTIONS are " +
                "gameplay-systems'. StPeters is now build-index-0 (the start scene). RE-RUN 'Build St Peters " +
                "Scene' AND 'Build Greywick Scene', then open StPeters.unity and press Play.", "Fair winds");
        }

        // ---- shared config (single source of truth with the EditMode test) -------------------------

        /// <summary>Apply the authored elevation zones onto a <see cref="TidalTerrain"/> via SerializedObject
        /// (the builder's persist-the-refs convention). One place so the values live on the component, never
        /// duplicated — and an EditMode test asserts the same constants drive the showcase.</summary>
        public static void ConfigureTidalTerrain(TidalTerrain terrain)
        {
            var so = new SerializedObject(terrain);
            SetF(so, "_deepHarbourElevation", DeepHarbourElevation);
            SetV2(so, "_islandCenter", IslandCenter);
            SetF(so, "_islandRadius", IslandRadius);
            SetF(so, "_islandFalloff", IslandFalloff);
            SetF(so, "_islandElevation", IslandElevation);
            SetV2(so, "_sandbarFrom", SandbarFrom);
            SetV2(so, "_sandbarTo", SandbarTo);
            SetF(so, "_sandbarHalfWidth", SandbarHalfWidth);
            SetF(so, "_sandbarCrestElevation", SandbarCrestElevation);
            SetF(so, "_channelAlong", ChannelAlong);
            SetF(so, "_channelHalfWidth", ChannelHalfWidth);
            SetF(so, "_channelBedElevation", ChannelBedElevation);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- helpers (self-contained; mirror GreywickBuilder's) ------------------------------------

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
            if (p == null) { Debug.LogWarning($"[StPetersBuilder] no array field '{field}'."); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetF(SerializedObject so, string field, float value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.floatValue = value;
            else Debug.LogWarning($"[StPetersBuilder] no float field '{field}'.");
        }

        static void SetV2(SerializedObject so, string field, Vector2 value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.vector2Value = value;
            else Debug.LogWarning($"[StPetersBuilder] no Vector2 field '{field}'.");
        }

        // Imported art is sliced (spriteMode Multiple, one sub-sprite), so LoadAssetAtPath<Sprite> returns
        // null — fall back to the first sub-sprite. Null if the art isn't imported.
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

        static void MakeClamSpot(Transform parent, Vector2 pos, string yieldFishId, Sprite marker)
        {
            var go = new GameObject("ClamHole");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();          // a faint greybox marker; gameplay/art replace it
            sr.sprite = marker; sr.color = new Color(0.30f, 0.24f, 0.16f, 0.65f); sr.sortingOrder = -6;
            go.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            var spot = go.AddComponent<ClamSpot>();
            SetString(spot, "_yieldFishId", yieldFishId);
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

        // St Peters is the GAME'S START scene, so it must be build-index-0 (the scene that boots on Play in
        // a player build). Insert it at the front (moving an existing entry to index 0 if a prior run added
        // it at the end), so the persistent core boots here. Idempotent on re-runs.
        static void RegisterScene(string path)
        {
            var list = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            list.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        static void EnsureFolders()
        {
            foreach (var f in new[] { DataRegions, DataConfig, DataBoats, DataFish, ArtSprites, Scenes })
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
