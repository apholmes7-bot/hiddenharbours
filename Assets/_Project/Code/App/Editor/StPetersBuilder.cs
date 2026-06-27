#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.Boats;               // BoatHullDef (the carried Dory/Punt hulls handed to the core)
using HiddenHarbours.Fishing;             // FishSpeciesDef + ClamDig/ClamHoleVisual (the clam dig action + look)
using HiddenHarbours.Player;              // ClamBucket (the on-foot clam hold the dig fills)
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
        const string DataNpcs    = "Assets/_Project/Data/NPCs";     // the opening cast (Aunt Ginny, Ned's letter)
        const string ArtSprites  = "Assets/_Project/Art/Sprites";
        // Opening-cast art (greybox; final St Peters storekeeper etc. are on the owner's draw-list).
        const string ArtGinny         = "Assets/_Project/Art/Characters/Ginny.png";   // Aunt Ginny standee
        const string ArtPortraitGinny = "Assets/_Project/Art/Portraits/Ginny.png";    // Ginny dialogue portrait
        const string ArtPortraitNed   = "Assets/_Project/Art/Portraits/Ned.png";      // Ned portrait on his letter
        const string ArtDialoguePanel = "Assets/_Project/Art/UI/DialoguePanel.png";   // dialogue panel art
        const string ArtNamePlate     = "Assets/_Project/Art/UI/NamePlate.png";       // nameplate art
        const string ArtSea      = "Assets/_Project/Art/Tilesets/Water/SeaTile.png";
        const string ArtWaterMat = "Assets/_Project/Art/Materials/Water.mat";   // the layered SIM-driven water shader (ADR 0010)
        const string ArtGrass    = "Assets/_Project/Art/Tilesets/Grass.png";
        const string ArtSand     = "Assets/_Project/Art/Tilesets/Sand.png";
        const string ArtCottage  = "Assets/_Project/Art/Sprites/Buildings/Cottage.png";
        const string ArtClamHole   = "Assets/_Project/Art/Sprites/ClamHole.png";    // the still dig-spot sprite
        const string ArtClamSquirt = "Assets/_Project/Art/Sprites/ClamSquirt.png";  // 4-frame "two squirts" tell
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

        // --- CLAM-HOLE scatter (deterministic; single source of truth shared with the EditMode test) ------
        // Holes scatter over the bar's footprint on a jittered grid, kept only where the authored ground is
        // INTERTIDAL — between the lowest and highest water of the swing (mean ∓ amplitude), so a hole bares
        // as the tide falls and floods as it rises. ClamScatterStep = grid spacing; ClamScatterJitter = the
        // max deterministic offset (hashed off the cell, no RNG) that breaks the grid look. The band is the
        // tide swing inset by a small margin so a hole isn't perpetually at the very waterline edge.
        public const float ClamScatterStep   = 6f;     // one candidate hole per ~6×6 m cell over the bar
        public const float ClamScatterJitter = 2.0f;   // ± world units of stable hash jitter per cell
        public const float ClamBandMargin     = 0.4f;   // inset (m) from the extreme water levels (kindness band)
        // The bar footprint to scatter over (a margin around the bar so the flats either side are covered).
        public const float ClamScatterMargin = 8f;

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
            // The Sea plane carries the layered WaterSurface shader (below), whose clip(depth) makes the
            // plane TRANSPARENT over dry ground (depth<=0) and renders OPAQUE water over covered ground
            // (depth>0) — the smooth wet/dry reveal. For "the ground shows through when dry, water covers it
            // when wet" (design/water-rendering.md §5.1: depth<=0 → hands off to terrain) the water plane must
            // sit ABOVE the authored tidal-ground sprites it reveals/covers: the Sandbar (-9), the
            // BoatChannelMarker (-8), the island beach/grass (-8/-7) and the slip (-6). At -5 it sits just
            // above that ground stack and still below the clam holes (clamped -4..4) and the on-foot
            // characters (the player Y-sorts within +2..+40, #110) — so holes and the player draw on the
            // bared flat IN FRONT of the water. (Before this change a 2 m TidalFlatVisual colour grid sat at
            // -5 and masked BOTH the sand and the shader; retiring that grid is what lets the smooth shader
            // show, and the Sea takes the vacated -5 slot. The always-dry island stays visible because the
            // shader clips over it: its 6 m elevation is above every water level, so depth<0 → transparent.)
            wsr.sortingOrder = -5;
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

            // --- LAYERED SIM-DRIVEN WATER SHADER (ADR 0010 / design/water-rendering.md) ------------------
            // FLAG lead-architect (this PR, the FREE touch): apply the HiddenHarbours/Water material to the
            // Sea plane and hang the runtime WaterSurface on it so the layered shader (depth gradient / surface
            // distortion / foam fringe / specular / caustics) MOVES off the live deterministic sim. WaterSurface
            // feeds the EnvironmentSample + WaterLevelAt into the shader each throttled tick (current→flow,
            // wind→roughness, level→shoreline, sea→chop) and BAKES the TidalTerrain seabed into a height texture,
            // so the visible waterline/depth match the SAME number the walkability/boat-cross gate reads. The
            // material's colours/speeds/thresholds are all Inspector tunables (no magic numbers) — the owner
            // art-directs the LOOK on the Water.mat after re-running this builder and viewing it in Play.
            // Visual-only: drives no sim, saves nothing (CLAUDE.md rule 5). If the material isn't present (first
            // checkout before import) the Sea stays the plain tiled/flat backdrop — the wiring no-ops safely.
            var waterMat = AssetDatabase.LoadAssetAtPath<Material>(ArtWaterMat);
            if (waterMat != null)
            {
                wsr.sharedMaterial = waterMat;
                var surface = water.AddComponent<HiddenHarbours.Art.WaterSurface>();
                // Bake the seabed height map at 192² (ADR 0012 §A step 1): over the 160×120 m plane that's a
                // ~0.83 m texel (half the old 96²/~1.67 m), so the shader's depth/foam shoreline follows a
                // FINE grid — the wet edge stops reading as ~1.5 m rectangular steps on the near-flat bar
                // crest. The bake is a one-time R8 texture on enable (trivial CPU/VRAM, rule 7). 256 is
                // available if the crest still facets, but 192 is the ADR's recommended start.
                ConfigureWaterSurface(surface, new Vector2(0f, 0f), new Vector2(160f, 120f), 192);
            }
            else
            {
                Debug.LogWarning("[StPetersBuilder] Water.mat not found at " + ArtWaterMat + " — Sea left as the " +
                                 "plain backdrop. Re-run after the material imports to get the layered water shader.");
            }

            // Reload data assets from disk before wiring refs (an intervening import can invalidate the
            // in-memory instances — the gotcha the cove/Greywick builders guard against).
            stPeters = AssetDatabase.LoadAssetAtPath<RegionDef>(DataRegions + "/StPeters.asset");
            greywick = AssetDatabase.LoadAssetAtPath<RegionDef>(DataRegions + "/PortGreywick.asset");
            config   = AssetDatabase.LoadAssetAtPath<GameConfig>(DataConfig + "/GameConfig.asset");
            dory     = AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/Dory.asset");
            punt     = AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataBoats + "/Punt.asset");
            clam     = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(DataFish + "/SoftShellClam.asset");

            // The opening cast as DATA (CLAUDE.md rule 2): Aunt Ginny (teaches the buy-and-repair loop) and
            // Ned's letter (the remembered presence — no inherited dory). Each NpcDef carries its name, its
            // DialogueDef, the verb, and the completion flag, so the builder only PLACES them — the words
            // live in assets the owner can edit (Data/NPCs + Data/NPCs/Dialogue), never hard-coded here.
            var ginnyNpc = AssetDatabase.LoadAssetAtPath<NpcDef>(DataNpcs + "/AuntGinny.asset");
            var nedNpc   = AssetDatabase.LoadAssetAtPath<NpcDef>(DataNpcs + "/NedLetter.asset");

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

            // --- TIDE-REVEAL: now owned by the smooth WaterSurface shader (ADR 0012) --------------------
            // The falling-tide reveal (the bar VISIBLY baring/covering) is rendered by the layered WaterSurface
            // shader on the Sea plane above — it reads the SAME deterministic tide + TidalTerrain the
            // walkability/boat-cross gate reads (depth = WaterLevelAt(t) − elevation), clips to expose the
            // authored sand ground where dry and renders a depth-graded, foam-fringed water where covered.
            // That is the SMOOTH wavy shore. An older purely-visual 2 m colour-cell grid (TidalFlatVisual)
            // used to be stamped here on top of the shader; it double-drew the bar as big flat blue/teal
            // squares and HID the smooth shader (the owner-reported blockiness). ADR 0012 "converge the live
            // shoreline on the shader path" retired it — the shader alone now owns the reveal. The P1
            // gameplay (clam-baring + the crossing gate) is unchanged: it reads ITidalTerrain/WaterLevelAt
            // directly (see ScatterClamHoles + ClamDig + the StPeters/Clam EditMode tests), never this visual.

            // --- OPTIONAL DEV FAST-TIDE (greybox aid; OFF by default) -----------------------------------
            // The real-time tide is slow by design (~minutes per high→low), so in a short playtest the
            // reveal is gradual. This dev component lets a tester TICK 'Enabled' on it in the Inspector
            // during Play to fast-forward the clock and watch the WHOLE swing in seconds — it never changes
            // the shipping default (it only multiplies the live TimeScale while ticked, and is OFF by
            // default). The in-editor Tide Scrubber remains the precise way to jump the tide.
            var devTideGo = new GameObject("DevFastTide");
            devTideGo.AddComponent<HiddenHarbours.Environment.DevFastTide>();

            // --- CLAM-HOLES (VISIBLE + diggable; scattered wherever the tide bares ground) ---------------
            // The owner's expectation: clam holes appear ANYWHERE the falling tide bares the flats, and are
            // diggable. So instead of a fixed handful of points, we DETERMINISTICALLY scatter holes over the
            // bar's whole footprint and keep only the cells whose authored ground is INTERTIDAL — ground that
            // actually reveals as the big tide falls (between the lowest and highest water of the swing). That
            // skips always-dry island and always-deep harbour, so holes land exactly on the bared flats.
            //
            // Each hole gets: a SpriteRenderer (ClamHole.png, base-Y sorted so it sits on the flat), a World
            // ClamSpot marker (names the yield by id), a ClamDig (the hole's gate-and-yield — wired to the
            // species + the player's ClamBucket, gating reach/exposure/shovel/room), and a ClamHoleVisual that
            // shows the hole ONLY while its ground is exposed and runs the ClamSquirt tell off
            // ClamDig.ShowingSquirt. The position + the exposure are a pure function of (position, tide) — no
            // RNG drift (CLAUDE.md rule 5); the scatter jitter is a stable hash of the grid cell, so a rebuild
            // reproduces the same field. The holes DON'T listen for input themselves — a single ClamDigger on
            // the player (below) owns the Interact key and digs only the nearest in-range hole, one clam per
            // press (the fix for "E dug every exposed hole at once + filled the bucket in two presses").
            var clamRoot = new GameObject("ClamHoles");
            var holeSprite = LoadSpriteAny(ArtClamHole);
            var squirtFrames = LoadSheetFrames(ArtClamSquirt);
            foreach (var p in ScatterClamHoles(terrain))
                MakeClamHole(clamRoot.transform, p, clam, "fish.soft_shell_clam",
                             core.Bucket, holeSprite, squirtFrames, waterSprite);

            // THE DIGGER (gameplay-systems): one Interact owner on the player. On E it digs the nearest hole
            // that's BOTH in shovel reach AND exposed — so a press is exactly one clam from the hole you're
            // standing on, and nothing when you're not at a bared hole. Sits on the persistent Player so it
            // carries across region hops with the player (no per-region re-wire needed).
            var digger = core.PlayerGo.AddComponent<ClamDigger>();
            SetRef(digger, "_player", core.PlayerGo.transform);

            // --- THE MOORED DORY'S SLIP (set dressing; the persistent Dory floats at DoryMooredPos) ------
            // The real, controllable Player is now spawned by the PersistentCoreBuilder above at
            // StartSpawnPos, and the persistent Dory floats off the south coast. This is a small marker on
            // the south shore showing where the uncle's dory is moored / where you board once she's yours.
            var slipGo = new GameObject("DorySlipMarker");
            slipGo.transform.position = new Vector3(DisembarkPos.x, DisembarkPos.y, 0f);
            var slipSr = slipGo.AddComponent<SpriteRenderer>();
            slipSr.sprite = waterSprite; slipSr.color = new Color(0.55f, 0.40f, 0.24f, 0.85f); slipSr.sortingOrder = -6;
            slipGo.transform.localScale = new Vector3(2.5f, 1.2f, 1f);

            // --- THE OPENING CAST + ONBOARDING (world-content; the buy-and-repair beat, canon §5.8) ------
            // Aunt Ginny + Ned's LETTER, anchored up on the island near the cottage (no routines — that's
            // M2), the self-built dialogue panel, the proximity INTERACT driver, and the light one-line
            // onboarding nudge that walks the NEW loop: dig clams → cross the bar → Greywick (cod licence +
            // rod) → buy + REPAIR the damaged dory → sail home. The dory is EARNED, never inherited.
            //
            // Everything sits UP BY THE COTTAGE (≈ y +3..+6), well clear of the dock zone at (-40,-26), so
            // the shared E key never fires both "talk" and "board" (context-aware by proximity). Belt-and-
            // braces, the open dialogue raises the Core InteractionGate the ControlSwitcher honours. Content
            // is DATA — the Interactables carry NpcDef refs, not strings; the words live in Data/NPCs.
            // Art is greybox (Ginny standee + portraits + panel), null-safe if a sprite isn't imported.

            // Dialogue panel (builds its own canvas in Awake; needs only the panel + nameplate art).
            var dialogueGo = new GameObject("DialogueUI");
            var presenter = dialogueGo.AddComponent<DialoguePresenter>();
            SetRef(presenter, "_panelSprite", LoadSpriteAny(ArtDialoguePanel));
            SetRef(presenter, "_nameplateSprite", LoadSpriteAny(ArtNamePlate));

            // Aunt Ginny — by the cottage on the island plateau. Teaches the buy-and-repair loop; finishing
            // her conversation sets met_ginny (which gates the first onboarding nudge + her warmer re-greet).
            var ginnyGo = MakeNpc("AuntGinny", new Vector3(-42f, 1.5f, 0f), LoadSpriteAny(ArtGinny),
                                  waterSprite, new Color(0.78f, 0.55f, 0.62f));
            var ginnyIt = ginnyGo.AddComponent<Interactable>();
            ConfigureInteractableNpc(ginnyIt, ginnyNpc, LoadSpriteAny(ArtPortraitGinny));

            // "Ned's Letter" on the cottage step — the remembered presence (no inherited dory; the boat is
            // bought + mended). Read it to set read_logbook. A small book-sized marker if the art's absent.
            var letterGo = MakeNpc("NedsLetter", new Vector3(-45.5f, 2.4f, 0f), null,
                                   waterSprite, new Color(0.62f, 0.47f, 0.30f));
            letterGo.transform.localScale = new Vector3(0.6f, 0.8f, 1f);
            var letterIt = letterGo.AddComponent<Interactable>();
            ConfigureInteractableNpc(letterIt, nedNpc, LoadSpriteAny(ArtPortraitNed));

            // The proximity INTERACT driver: shows "E: …" near an interactable and runs its conversation.
            var interactorGo = new GameObject("WorldInteractor");
            var interactor = interactorGo.AddComponent<WorldInteractor>();
            SetRef(interactor, "_player", core.PlayerGo.transform);
            SetRef(interactor, "_presenter", presenter);
            SetRefArray(interactor, "_interactables", new Object[] { ginnyIt, letterIt });

            // Light onboarding: one nudge per beat of the new earned-dory loop, then it bows out and persists
            // 'onboarded' (on the dory being REPAIRED) so the opening never re-triggers on reload.
            new GameObject("Onboarding").AddComponent<OnboardingDirector>();

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
                      "deeper CHANNEL (-0.6 m) stays boat-crossable at higher tide. The layered WaterSurface " +
                      "shader VISIBLY reveals the bar/flats from the live water level (smooth depth-graded " +
                      "water that clips to bare the sand as the tide falls, foam hugging the moving edge) — " +
                      "the smooth shoreline, no blocky grid overlay (ADR 0012). Up by the " +
                      "cottage, AUNT GINNY (E to talk) teaches the buy-and-repair loop and NED'S LETTER (E " +
                      "to read) frames it — the dory is EARNED, not inherited; a one-line onboarding nudge " +
                      "walks the loop (clams → cross the bar → Greywick licence+rod → buy+repair the dory → " +
                      "sail home). Clam-holes on " +
                      "the flats (fish.soft_shell_clam, gated by exposure). The walk passage at the Greywick " +
                      "end leads on. NOTE: the tide is SLOW by design (~minutes per high→low); to see the " +
                      "full swing fast, tick 'Enabled' on the DevFastTide object in Play (OFF by default) or " +
                      "use the in-editor Tide Scrubber. StPeters is now BUILD-INDEX-0 (the start scene). " +
                      "RE-RUN this builder + GreywickBuilder, then open StPeters.unity and press Play.");
            EditorUtility.DisplayDialog("Hidden Harbours",
                "St Peters Island built — now a PLAYABLE START scene (greybox).\n\nPress Play:\n• You control " +
                "the on-foot fisher (WASD / arrows) at the start spawn; the camera follows.\n• The clock + " +
                "tide RUN — the smooth WaterSurface shader bares the sandbar/flats (the sand shows through) " +
                "as the big tide (±3.5 m) falls and covers them with depth-graded water as it floods (the " +
                "tide-reveal is the point).\n• The tide is SLOW by design (~a few " +
                "minutes per high→low). To watch the full swing FAST: tick 'Enabled' on the DevFastTide " +
                "object in the Hierarchy while in Play (OFF by default), or use Tools ▸ Tide Scrubber.\n• " +
                "The hand-rowed Dory is moored off the south coast (board at the slip once she's yours).\n• " +
                "Walk the bared bar east to reach Greywick.\n\nThe dig/walk/gear ACTIONS are " +
                "gameplay-systems'. StPeters is now build-index-0 (the start scene). RE-RUN 'Build St Peters " +
                "Scene' AND 'Build Greywick Scene', then open StPeters.unity and press Play.", "Fair winds");
        }

        // ---- shared config (single source of truth with the EditMode test) -------------------------

        /// <summary>Configure the runtime <see cref="HiddenHarbours.Art.WaterSurface"/> so the layered shader's
        /// baked seabed height map covers the visible water (the Sea plane bounds) at a chosen bake resolution.
        /// The sim → uniform tuning (current/wind thresholds, refresh) lives on the component defaults and the
        /// LOOK on the Water material (no magic numbers — CLAUDE.md rule 6); here we set the world rectangle the
        /// height map bakes over (so the depth gradient + foam band line up with the St Peters TidalTerrain) and
        /// the bake <paramref name="resolution"/>. A FINER bake (ADR 0012 §A: 96 → 192) shrinks the texel from
        /// ~1.67 m to ~0.83 m over the 160×120 m plane, so the shader's wet edge follows a fine grid instead of
        /// reading as ~1.5 m rectangular steps — the smoothed shoreline. Clamped to the component's
        /// [Range(16,256)]; pass 256 if the near-flat bar crest still facets at 192.</summary>
        public static void ConfigureWaterSurface(HiddenHarbours.Art.WaterSurface surface,
                                                 Vector2 worldCenter, Vector2 worldSize, int resolution)
        {
            var so = new SerializedObject(surface);
            SetV2(so, "_heightWorldCenter", worldCenter);
            SetV2(so, "_heightWorldSize", worldSize);
            SetInt(so, "_heightResolution", Mathf.Clamp(resolution, 16, 256));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

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

        static void SetInt(SerializedObject so, string field, int value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.intValue = value;
            else Debug.LogWarning($"[StPetersBuilder] no int field '{field}'.");
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

        // All sub-sprites of a sliced sheet (spriteMode Multiple), in frame order — e.g. ClamSquirt_0.._3.
        static Sprite[] LoadSheetFrames(string path)
            => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                            .OrderBy(s => SpriteIndex(s.name)).ToArray();

        static int SpriteIndex(string spriteName)
        {
            int u = spriteName.LastIndexOf('_');
            return (u >= 0 && int.TryParse(spriteName.Substring(u + 1), out int n)) ? n : 0;
        }

        // A standing world NPC / marker: a SpriteRenderer above the ground (just under the player, which
        // draws at +10), with a tinted-square fallback so the greybox still builds before the art imports.
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

        // Wire an Interactable to an NpcDef (the data-driven path) + an optional portrait, via the builder's
        // persist-the-refs SerializedObject convention so the refs survive into the saved scene.
        static void ConfigureInteractableNpc(Interactable it, NpcDef npc, Sprite portrait)
        {
            var so = new SerializedObject(it);
            var npcProp = so.FindProperty("_npc");
            if (npcProp != null) npcProp.objectReferenceValue = npc;
            var portraitProp = so.FindProperty("_portrait");
            if (portraitProp != null) portraitProp.objectReferenceValue = portrait;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (npc == null)
                Debug.LogWarning("[StPetersBuilder] NpcDef missing for an Interactable — re-run after the " +
                                 "Data/NPCs assets import (the opening NPC will show no dialogue otherwise).");
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

        /// <summary>
        /// DETERMINISTIC clam-hole field: a hash-jittered grid over the bar footprint, keeping only the
        /// cells whose authored ground (<paramref name="terrain"/>) is INTERTIDAL — between the lowest and
        /// highest water of the St Peters swing (mean ∓ amplitude), inset by <see cref="ClamBandMargin"/>.
        /// That puts holes exactly where the falling tide bares ground (skipping always-dry island and
        /// always-deep harbour), so wherever a flat reveals there are holes to find. Pure: position is a
        /// function of the cell index (a stable hash, no <c>System.Random</c>) and the kept/dropped decision
        /// is a function of (position, tide band) — a rebuild reproduces the same field (CLAUDE.md rule 5).
        /// Public + static so the EditMode test can assert the same field the scene is built from.
        /// </summary>
        public static System.Collections.Generic.List<Vector2> ScatterClamHoles(ITidalTerrain terrain)
        {
            var holes = new System.Collections.Generic.List<Vector2>();
            if (terrain == null) return holes;

            // The intertidal band: ground that bares AND floods over the swing. Mean ∓ amplitude is the
            // extreme water; inset by the kindness margin so a hole isn't perpetually at the very edge.
            float lowWater  = TideMean - TideAmplitude + ClamBandMargin;
            float highWater = TideMean + TideAmplitude - ClamBandMargin;

            // Bounding box around the bar (From→To) plus a margin so the flats either side are covered.
            float minX = Mathf.Min(SandbarFrom.x, SandbarTo.x) - ClamScatterMargin;
            float maxX = Mathf.Max(SandbarFrom.x, SandbarTo.x) + ClamScatterMargin;
            float minY = Mathf.Min(SandbarFrom.y, SandbarTo.y) - SandbarHalfWidth - ClamScatterMargin;
            float maxY = Mathf.Max(SandbarFrom.y, SandbarTo.y) + SandbarHalfWidth + ClamScatterMargin;

            int nx = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / ClamScatterStep));
            int ny = Mathf.Max(1, Mathf.CeilToInt((maxY - minY) / ClamScatterStep));
            for (int ix = 0; ix < nx; ix++)
            for (int iy = 0; iy < ny; iy++)
            {
                // Cell centre, then a stable hash-jitter (deterministic, no RNG) so the field isn't a grid.
                float cx = minX + (ix + 0.5f) * ClamScatterStep;
                float cy = minY + (iy + 0.5f) * ClamScatterStep;
                cx += (Hash01(ix, iy, 1) * 2f - 1f) * ClamScatterJitter;
                cy += (Hash01(ix, iy, 2) * 2f - 1f) * ClamScatterJitter;

                var pos = new Vector2(cx, cy);
                float ground = terrain.ElevationAt(pos);
                if (ground > lowWater && ground < highWater)   // intertidal → bares as the tide falls
                    holes.Add(pos);
            }
            return holes;
        }

        // A stable 0..1 hash of two integer cell coords + a salt — deterministic jitter without System.Random
        // (so the scatter is a pure function of the cell; a rebuild reproduces the same field — rule 5).
        static float Hash01(int x, int y, int salt)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)x) * 16777619u;
                h = (h ^ (uint)y) * 16777619u;
                h = (h ^ (uint)salt) * 16777619u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
                return (h & 0xFFFFFF) / (float)0x1000000;   // 24-bit mantissa → [0,1)
            }
        }

        /// <summary>
        /// Build one VISIBLE, diggable clam hole: a SpriteRenderer (ClamHole.png, base-Y sorted so it sits on
        /// the flat) used as the visual's BASE (two-holes) renderer, the World <see cref="ClamSpot"/> marker,
        /// the <see cref="ClamDig"/> action wired to the clam species + the player's <see cref="ClamBucket"/>,
        /// and the <see cref="ClamHoleVisual"/> that shows the holes while exposed, layers the squirt tell on a
        /// SEPARATE overlay renderer it creates (the squirt is added on top, never a sprite-swap), runs the
        /// skittish-clam proximity escape, and vanishes the hole once it's been dug (a hole yields once).
        /// </summary>
        static void MakeClamHole(Transform parent, Vector2 pos, FishSpeciesDef clam, string yieldFishId,
                                 ClamBucket bucket, Sprite holeSprite, Sprite[] squirtFrames, Sprite fallback)
        {
            var go = new GameObject("ClamHole");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            // Sit ON the bared flat: above the Sandbar ground (-9) AND the water shader plane (Sea, -5) so a
            // hole on dry ground draws in front of the (clipped-transparent) water, and below the on-foot
            // characters (the player Y-sorts within +2..+40, #110). A small base-Y term sorts holes among
            // themselves (lower on screen = higher Y-negated = draws in front) without ever crossing the
            // character band — clamped to -4..4 so a far-south hole can't pop in front of the player.
            sr.sortingOrder = Mathf.Clamp(-Mathf.RoundToInt(pos.y), -4, 4);
            if (holeSprite != null) { sr.sprite = holeSprite; go.transform.localScale = Vector3.one; }
            else { sr.sprite = fallback; sr.color = new Color(0.30f, 0.24f, 0.16f, 0.65f); go.transform.localScale = new Vector3(0.5f, 0.5f, 1f); }

            // World marker: names the yield by id (no Fishing reference from World).
            var spot = go.AddComponent<ClamSpot>();
            SetString(spot, "_yieldFishId", yieldFishId);

            // The DIG action (gameplay-systems): species + the player's bucket; gates exposure/shovel/room.
            // The exposure test reads this object's transform (the hole position) via TidalTerrain.
            var dig = go.AddComponent<ClamDig>();
            SetRef(dig, "_clamSpecies", clam);
            SetRef(dig, "_bucketProvider", bucket != null ? bucket.gameObject : null);
            SetRef(dig, "_spot", go.transform);

            // The LOOK: the two-holes sprite stays on the BASE renderer while exposed; the squirt plays as an
            // OVERLAY on a separate child renderer the visual creates on top (added, never a sprite-swap), so
            // the holes are always visible when exposed. Driven off the SAME exposure read the dig gates on, so
            // picture and gate agree. The visual also runs the skittish-clam escape and vanishes a dug hole.
            var visual = go.AddComponent<ClamHoleVisual>();
            SetRef(visual, "_dig", dig);
            SetRef(visual, "_holeSprite", holeSprite);
            if (squirtFrames != null && squirtFrames.Length > 0)
                SetRefArray(visual, "_squirtFrames", squirtFrames.Cast<Object>().ToArray());
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
