#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.Boats;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;
using HiddenHarbours.World;         // RegionSceneLoader (the persistent travel rig's loader)
using HiddenHarbours.UI;            // HudController (the single ui-ux touch, mirrored from the cove)
using HiddenHarbours.App;           // PersistentObject / CameraFollow / RegionTravelCoordinator
using HiddenHarbours.Art.Editor;    // VS-23 locked Pixel-Perfect camera convention
using UnityEngine.Rendering.Universal; // PixelPerfectCamera

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// The shared PERSISTENT-CORE rig that EVERY start scene needs (VS-01, pragmatic slice). Extracted so
    /// the cove (GreyboxBuilder) and St Peters (StPetersBuilder) build the SAME core and it can never
    /// diverge between scenes (the bug that left St Peters with no controllable player — #64 scoped the
    /// core OUT of St Peters because the cove used to be the start).
    ///
    /// <para>The core is the rig that must survive an additive region hop (each piece is tagged
    /// <see cref="PersistentObject"/> → DontDestroyOnLoad): the services root (GameClock / EnvironmentService
    /// / PlayerWallet + GameRoot + the glanceable HUD), the on-foot Player (FisherSheet + Rigidbody2D +
    /// footprint collider + <see cref="PlayerWalkController"/>), the hand-rowed Dory (full controller +
    /// hold + oar rig + fishing + fleet + active-boat probe, moored/disabled at start), the follow
    /// <see cref="CameraFollow"/> framing the player on foot, the <see cref="ControlSwitcher"/>, the
    /// transient fishing gauge, and the travel rig (<c>RegionSceneLoader</c> + <c>RegionTravelCoordinator</c>).</para>
    ///
    /// <para>Region-specific content (the island/coast/wharf/NPCs, the region anchors, the passages, the
    /// region's own RegionDefs) is authored by EACH scene builder around this core — this only stands up the
    /// carried rig and returns a <see cref="Handle"/> exposing the pieces a scene still positions or wires.</para>
    ///
    /// <para>FLAG lead-architect: the long-term home for this rig is a dedicated <c>Bootstrap.unity</c>
    /// (one persistent-core scene that additively loads the start region), so a start scene re-authoring the
    /// core goes away entirely (VS-01). Until then, this single helper keeps the two start scenes identical.</para>
    /// </summary>
    public static class PersistentCoreBuilder
    {
        const string ArtDory     = "Assets/_Project/Art/Boats/Dory.png";          // legacy single-sprite hull (fallback)
        const string ArtDoryHull = "Assets/_Project/Art/Boats/DoryHull.png";      // oar-less hull base (64×144, centre)
        const string ArtOar      = "Assets/_Project/Art/Boats/Oar.png";           // one oar (used ×2, mirrored)
        const string ArtDoryRower= "Assets/_Project/Art/Boats/DoryRower.png";     // rower figure
        const string ArtFisher   = "Assets/_Project/Art/Characters/FisherSheet.png"; // on-foot player (sliced 3×4)

        // --- DIRECTIONAL FISHING-BOAT VISUAL ("for now" placeholder swap; #93 DirectionalBoatSprite) ------
        // REVERSIBLE FLAG. While true, the playable boat WEARS the owner's 4-way hand-drawn fishing-boat
        // facings (snap to the nearest of N/E/S/W from heading, picture stays screen-aligned) instead of the
        // hand-rowed dory hull + oar rig — the owner LOVES the directional boat and wants it on the boat he
        // sails NOW, until he draws 8/16-way art. It is a VISUAL swap only: the boat PHYSICS + controls (the
        // BoatController / hull def / OwnedFleet) are UNCHANGED — the dory is still hand-rowed under the hood;
        // we only HIDE the oar-rig + hull picture and draw the chosen facing on top. Flip this to FALSE and
        // re-run the start builder (StPetersBuilder) to restore the dory hull + oars exactly as before (the
        // swap is the only thing this flag gates). The 4 facings are M2 fleet art used as a placeholder
        // (memory: M2 fleet art banked + frozen) — kept reversible so the dory render path returns with one bool.
        const bool UseDirectionalFishingBoatVisual = true;

        // The 4 owner-drawn facings, in CLOCKWISE order from the ZERO heading (North): N, E, S, W. The
        // DirectionalBoatSprite math (#93) already maps heading→facing; element 0 must be the North-facing
        // sprite and the zero-heading is 0° (North/up), matching the project's bearing convention.
        static readonly string[] FishingBoatFacingPaths =
        {
            "Assets/_Project/Art/Boats/FishingBoat_N.png",
            "Assets/_Project/Art/Boats/FishingBoat_E.png",
            "Assets/_Project/Art/Boats/FishingBoat_S.png",
            "Assets/_Project/Art/Boats/FishingBoat_W.png",
        };

        // The dedicated Engine-propulsion hull for the directional fishing-boat skin (boat.fishing_skiff).
        // While the directional visual is on, the playable boat is given THIS hull instead of the rowed
        // Dory, so the CONTROLS MATCH the powerboat picture (throttle ahead/astern + a speed-scaled rudder)
        // — "a power boat skin, not a rowboat" (owner). A MINIMAL, dedicated Def (one entity per file,
        // stable append-only id) rather than the Punt: it is the SAME small/shallow physical hull as the
        // Dory (length 4.5 m, draught 0.3 m, mass 400 kg, hold 6 HU, camera 14 m) — so NO Punt side-effects
        // (no camera-zoom-on-ActiveBoatChanged, no bigger mass/draught) — but Propulsion = Engine with a
        // modest outboard (EnginePower 500). It is deliberately NOT in OwnedFleet's registry, so the
        // buy-the-Punt grant + save-restore (both id-keyed off ActiveHullId/BoatPurchased) never touch it —
        // the skin's Engine hull STICKS past boarding/grants/load. Reversible under the SAME flag: with the
        // flag off this is never loaded and the rowed Dory hull (Oars) stands exactly as before.
        const string DataFishingSkiff = "Assets/_Project/Data/Boats/FishingSkiff.asset";

        const string ArtTensionGauge   = "Assets/_Project/Art/UI/TensionGauge.png";
        const string ArtLineHook       = "Assets/_Project/Art/UI/LineHook.png";
        const string ArtFishSilhouette = "Assets/_Project/Art/UI/FishOnSilhouette.png";

        /// <summary>Inputs a scene builder hands the core (all data refs are the RELOADED/persisted assets,
        /// so they serialize into the scene instead of saving as "None").</summary>
        public struct Params
        {
            public GameConfig Config;
            public BoatHullDef StartDory;         // the start hull on the persistent Dory (hand-rowed greybox dory)
            public BoatHullDef PuntHull;          // tier-1 swap hull for the fleet registry (null-safe)
            public FishSpeciesDef[] RegionFish;   // the fishing controller's region species (null-safe)
            public Sprite Square;                 // the 1×1 fallback square sprite the scene already made
            public Color CameraBackground;        // scene's clear colour (matches the region's water mood)
            public Vector3 PlayerStartPos;        // where the on-foot fisher spawns (the region's START)
            public Vector3 BoatMooredPos;         // where the moored Dory sits (its controller starts disabled)
            public bool TideGatedWalk;            // St Peters turns this ON (the falling-tide wading edge, P1)
            public string CurrentSceneName;       // the loader's home scene (don't rely on Awake vs DDOL order)
            // The START region's live tide (EnvironmentService reads _activeTideProfile directly — nothing
            // re-points it on a region hop yet), so the start scene's authored tide is what RUNS. St Peters
            // needs its BIG tide (mean 0, amp 3.5) so the bar visibly bares + floods (the showcase).
            public float TideMean;
            public float TideAmplitude;
            public float TidePhaseHours;
        }

        /// <summary>The pieces a scene builder still needs after the core is built — to position region
        /// content, attach region wharves, place region anchors/passages, and configure the loader/coordinator.</summary>
        public struct Handle
        {
            public GameObject ServicesRoot;       // GameRoot (clock/env/wallet/HUD) — also the IWallet provider
            public GameObject CameraGo;           // the Main Camera (CameraFollow + AudioListener + pixel-perfect)
            public GameObject PlayerGo;           // the on-foot Player (PlayerWalkController)
            public GameObject DoryGo;             // the persistent Dory (controller disabled = moored at start)
            public GameObject GaugeGo;            // the transient fishing gauge overlay
            public PlayerWalkController Walk;
            public ClamBucket Bucket;             // the on-foot clam hold (IHold) the dig fills; sits on the Player
            public BoatController Boat;
            public ShipHold Hold;
            public Behaviour BoatInput;           // DevBoatInput (enabled only while aboard)
            public OwnedFleet Fleet;
            public ControlSwitcher Switcher;
            public GameObject SwitcherGo;
            public CameraFollow Camera;
            public RegionSceneLoader Loader;
            public GameObject LoaderGo;
            public RegionTravelCoordinator Coordinator;
        }

        /// <summary>
        /// Stand up the full persistent core in the current scene and tag every piece persistent. Wires the
        /// camera to follow the player on foot, the control switcher to the player+boat, and the travel rig
        /// (loader + coordinator) to the carried player/boat/hold. The scene builder then positions region
        /// content, places its RegionAnchor + passages, and fills the loader's region list via the Handle.
        /// </summary>
        public static Handle Build(Params p)
        {
            // --- CAMERA (persistent; follows the player, pixel-perfect, PC-first landscape framing) -------
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = CameraFollow.OrthoSizeForWorldHeight(CameraFollow.OnFootWorldHeightMeters);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = p.CameraBackground;
            camGo.transform.position = new Vector3(p.PlayerStartPos.x, p.PlayerStartPos.y, -10f);
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

            // --- SERVICES ROOT (clock / environment / wallet + GameRoot + HUD) ----------------------------
            var root = new GameObject("GameRoot");
            var clock  = root.AddComponent<GameClock>();
            var env    = root.AddComponent<EnvironmentService>();
            var wallet = root.AddComponent<PlayerWallet>();
            var gameRoot = root.AddComponent<GameRoot>();
            SetRef(clock, "_config", p.Config);
            SetRef(env, "_config", p.Config);
            // The live tide the EnvironmentService runs (it reads _activeTideProfile directly). The start
            // scene authors its region's tide here so the bar/water actually swing on that region's curve.
            SetTideProfile(env, p.TideMean, p.TideAmplitude, p.TidePhaseHours);
            SetRef(gameRoot, "_clock", clock);
            SetRef(gameRoot, "_environment", env);
            SetRef(gameRoot, "_wallet", wallet);

            // The glanceable HUD (VS-17, ui-ux): self-contained, reads only through Core; _config gives it
            // SecondsPerHour for the tide time-to-turn. Mirrored from the cove (the one ui-ux touch).
            var hud = root.AddComponent<HudController>();
            SetRef(hud, "_config", p.Config);

            // --- THE DORY (full hand-rowed rig; moored = controller disabled at start) --------------------
            var doryGo = new GameObject("Dory");
            doryGo.transform.position = p.BoatMooredPos;
            var sr = doryGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 0;
            var hullSprite = LoadSpriteAny(ArtDoryHull) ?? LoadSpriteAny(ArtDory);
            if (hullSprite != null) { sr.sprite = hullSprite; doryGo.transform.localScale = Vector3.one; }
            else { sr.sprite = p.Square; sr.color = new Color(0.82f, 0.45f, 0.25f); doryGo.transform.localScale = new Vector3(3.6f, 9f, 1f); }

            var rb = doryGo.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            var boat = doryGo.AddComponent<BoatController>();
            var hullCol = doryGo.GetComponent<CapsuleCollider2D>() ?? doryGo.AddComponent<CapsuleCollider2D>();
            hullCol.direction = CapsuleDirection2D.Vertical;
            hullCol.size = new Vector2(1.7f, 4.0f);
            hullCol.offset = Vector2.zero;
            var hold = doryGo.AddComponent<ShipHold>();
            var devBoat = doryGo.AddComponent<DevBoatInput>();

            // Oar-rework rig: rower + two independently-rotating oars, children of the dory.
            var oarRig = new GameObject("OarRig");
            oarRig.transform.SetParent(doryGo.transform, false);
            var rower = new GameObject("DoryRower");
            rower.transform.SetParent(oarRig.transform, false);
            var rowerSr = rower.AddComponent<SpriteRenderer>();
            rowerSr.sortingOrder = 2;
            var rowerSprite = LoadSpriteAny(ArtDoryRower);
            if (rowerSprite != null) rowerSr.sprite = rowerSprite;
            else { rowerSr.sprite = p.Square; rowerSr.color = new Color(0.25f, 0.20f, 0.15f); rower.transform.localScale = new Vector3(1.6f, 1.8f, 1f); }

            var oarSprite = LoadSpriteAny(ArtOar);
            var leftOarPivot  = MakeOar(oarRig.transform, "LeftOar",  new Vector2(-0.9f, -0.1f), true,  oarSprite, p.Square);
            var rightOarPivot = MakeOar(oarRig.transform, "RightOar", new Vector2( 0.9f, -0.1f), false, oarSprite, p.Square);

            var rowAnim = doryGo.AddComponent<BoatRowAnimator>();
            SetRef(rowAnim, "_leftOarPivot", leftOarPivot);
            SetRef(rowAnim, "_rightOarPivot", rightOarPivot);
            SetRef(rowAnim, "_oarRig", oarRig);

            // Moored at start: WASD drives only the on-foot player. Boarding re-enables these (unbroken).
            devBoat.enabled = false;
            boat.enabled = false;

            var fishing = doryGo.AddComponent<FishingController>();
            doryGo.AddComponent<DevFishingInput>();
            SetRef(boat, "_hull", p.StartDory);
            SetRef(hold, "_hull", p.StartDory);
            SetRef(fishing, "_holdProvider", doryGo);
            if (p.RegionFish != null && p.RegionFish.Length > 0)
                SetRefArray(fishing, "_regionFish", p.RegionFish.Cast<Object>().ToArray());

            // Boat grant (VS-16): OwnedFleet swaps the active hull to a bought boat by id. Registry = {Dory,
            // Punt} when the Punt exists (null-safe — a region without the Punt offer just registers the Dory).
            var fleet = doryGo.AddComponent<OwnedFleet>();
            var registry = p.PuntHull != null
                ? new Object[] { p.StartDory, p.PuntHull }
                : new Object[] { p.StartDory };
            SetRefArray(fleet, "_registry", registry);
            SetRef(fleet, "_boat", boat);
            SetRef(fleet, "_hold", hold);
            SetRef(fleet, "_spriteRenderer", sr);

            // --- DIRECTIONAL FISHING-BOAT SKIN (reversible "for now" swap; #93/#94, propulsion #97) -------
            // When the flag is on, the boat the player sails WEARS the owner's 4-way fishing-boat facings AND
            // drives as an ENGINE boat (throttle + speed-scaled rudder) instead of the hand-rowed dory hull +
            // oars — "a power boat skin, not a rowboat" (owner). Applied HERE, AFTER the dory hull + OwnedFleet
            // are wired, so the Engine hull (boat.fishing_skiff) is the FINAL serialized state of the boat's
            // _hull/_hold and OwnedFleet's id-keyed registry {Dory[,Punt]} — which the skiff is deliberately NOT
            // in — never reverts it on board/grant/load. Self-contained + null-safe (no-ops to the rowed dory if
            // the skiff Def or facing art isn't imported). Flip UseDirectionalFishingBoatVisual to FALSE and
            // re-run the start builder to restore the hand-rowed Dory hull + oars exactly. Boats-core is NOT
            // modified — this only re-points the serialized hull refs the same way the dory hull was set.
            if (UseDirectionalFishingBoatVisual)
                ApplyDirectionalFishingBoatVisual(doryGo, sr, oarRig, rowAnim, boat, hold);

            // Active-boat heading seam (VS-19): the HUD pulls heading/COG through Core; HasActiveBoat tracks
            // the controller's enabled flag (moored/on-foot → false).
            var activeBoatProbe = doryGo.AddComponent<ActiveBoatProbe>();
            SetRef(activeBoatProbe, "_boat", boat);

            // --- THE TRANSIENT FISHING GAUGE (reads the fight via Core FishingStateChanged) ---------------
            var gaugeGo = new GameObject("FishingGauge");
            var gauge = gaugeGo.AddComponent<RodGaugeView>();
            SetRef(gauge, "_gaugeSprite", LoadArtSprite(ArtTensionGauge));
            SetRef(gauge, "_lineHookSprite", LoadArtSprite(ArtLineHook));
            SetRef(gauge, "_fishSprite", LoadArtSprite(ArtFishSilhouette));

            // --- THE ON-FOOT PLAYER (FisherSheet walk; footprint collider keeps it on land) ---------------
            var playerGo = new GameObject("Player");
            playerGo.transform.position = p.PlayerStartPos;
            playerGo.transform.localScale = Vector3.one;
            var playerSr = playerGo.AddComponent<SpriteRenderer>();
            playerSr.sortingOrder = 10;
            // Living-grass hooks: GrassFootstep makes the grass bend away as the player walks through; YSortSprite
            // (dynamic) re-sorts the player by world Y each frame so grass/trees IN FRONT draw over the player and
            // those BEHIND draw under — automatic ¾ layering, no per-piece tuning. (The order 10 above is just the
            // pre-Play default; YSortSprite recomputes it from Y on the same scale grass/trees use.)
            playerGo.AddComponent<HiddenHarbours.Art.GrassFootstep>();
            var playerYSort = playerGo.AddComponent<HiddenHarbours.Art.YSortSprite>();
            SetBool(playerYSort, "_dynamic", true);
            var prb = playerGo.AddComponent<Rigidbody2D>();
            prb.gravityScale = 0f; prb.freezeRotation = true;
            prb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var foot = playerGo.AddComponent<CircleCollider2D>();
            foot.radius = 0.35f; foot.offset = new Vector2(0f, -0.7f);
            var walk = playerGo.AddComponent<PlayerWalkController>();
            SetBool(walk, "_tideGatedWalk", p.TideGatedWalk);
            var fisherFrames = LoadSheetFrames(ArtFisher);
            SetRefArray(walk, "_frames", fisherFrames.Cast<Object>().ToArray());
            if (fisherFrames.Length > 0 && fisherFrames[0] != null)
                playerSr.sprite = fisherFrames[0];

            // The on-foot CLAM HOLD + STARTING GEAR (St Peters opening). Without these the dig chain is dead:
            // ClamDig writes a dug clam into an IHold (the bucket) and gates on owning gear.shovel — so the
            // player carries a ClamBucket (the hand-held hold) and a StartingGear grant that writes
            // gear.shovel + gear.bucket into the save on Start. (StartingGear is a no-op until a save exists,
            // and idempotent, so re-entering the scene never double-grants.) The dig spots wire their
            // ClamDig._bucketProvider to this Player via the Handle.
            var bucket = playerGo.AddComponent<ClamBucket>();
            playerGo.AddComponent<StartingGear>();

            // --- CAMERA FOLLOW (starts on the player at the on-foot framing; switches on ControlModeChanged) -
            var cameraFollow = camGo.AddComponent<CameraFollow>();
            cameraFollow.Target = playerGo.transform;
            var cfSo = new SerializedObject(cameraFollow);
            var cfWorldH = cfSo.FindProperty("_worldHeightMeters");
            if (cfWorldH != null) { cfWorldH.floatValue = CameraFollow.OnFootWorldHeightMeters; cfSo.ApplyModifiedPropertiesWithoutUndo(); }
            SetRef(cameraFollow, "_onFootTarget", playerGo.transform);
            SetRef(cameraFollow, "_boatTarget", doryGo.transform);

            // --- CONTROL SWITCHER (board/disembark; the scene re-points its dock via the RegionAnchor) -----
            var switcherGo = new GameObject("ControlSwitcher");
            var switcher = switcherGo.AddComponent<ControlSwitcher>();
            SetRef(switcher, "_playerWalk", walk);
            SetRef(switcher, "_boatController", boat);
            SetRef(switcher, "_boatInput", devBoat);
            // _dockZone / _disembarkPoint are left for the scene to wire via its RegionAnchor (SetDock on
            // arrival) — a start scene that wants an immediate cove-style dock can SetRef them after Build.

            // --- TRAVEL RIG (persistent loader + coordinator; the scene fills the region list) -------------
            var loaderGo = new GameObject("RegionSceneLoader");
            var loader = loaderGo.AddComponent<RegionSceneLoader>();
            SetString(loader, "_currentSceneName", p.CurrentSceneName);

            var coordinatorGo = new GameObject("RegionTravelCoordinator");
            var coordinator = coordinatorGo.AddComponent<RegionTravelCoordinator>();
            SetRef(coordinator, "_player", playerGo.transform);
            SetRef(coordinator, "_boat", doryGo.transform);
            SetRef(coordinator, "_switcher", switcher);
            SetRef(coordinator, "_hold", hold);

            // --- PERSIST: tag every carried piece so it survives the additive region hop ------------------
            // GameRoot already DontDestroyOnLoads itself; the rest carry the marker. The camera carries too
            // (the core owns the live camera; region scenes' cameras are silenced by the coordinator).
            root.AddComponent<PersistentObject>();      // belt-and-braces alongside GameRoot's own DDOL
            playerGo.AddComponent<PersistentObject>();
            doryGo.AddComponent<PersistentObject>();
            camGo.AddComponent<PersistentObject>();
            switcherGo.AddComponent<PersistentObject>();
            gaugeGo.AddComponent<PersistentObject>();
            loaderGo.AddComponent<PersistentObject>();
            coordinatorGo.AddComponent<PersistentObject>();

            return new Handle
            {
                ServicesRoot = root, CameraGo = camGo, PlayerGo = playerGo, DoryGo = doryGo, GaugeGo = gaugeGo,
                Walk = walk, Bucket = bucket, Boat = boat, Hold = hold, BoatInput = devBoat, Fleet = fleet,
                Switcher = switcher, SwitcherGo = switcherGo, Camera = cameraFollow,
                Loader = loader, LoaderGo = loaderGo, Coordinator = coordinator,
            };
        }

        // ---- directional fishing-boat skin (reversible "for now" swap; #93/#94 visual, #97 propulsion) ----

        /// <summary>
        /// Make the playable boat WEAR the owner's 4-way directional fishing-boat facings AND drive as an
        /// ENGINE boat instead of the hand-rowed dory hull + oar rig — the reversible "for now" placeholder
        /// the owner asked for (#93/#94 put the picture on; #97 makes the CONTROLS match it, "a power boat
        /// skin, not a rowboat"). It (0) swaps the boat's hull + hold to the dedicated Engine
        /// <see cref="BoatHullDef"/> <c>boat.fishing_skiff</c> (Propulsion = Engine → throttle + speed-scaled
        /// rudder, via the data-driven branch in <see cref="BoatController"/>), so the helm matches the
        /// powerboat picture; (1) HIDES the dory hull picture (disables the hull SpriteRenderer; its sprite
        /// ref stays so OwnedFleet's id-keyed swap is unaffected); (2) HIDES the oar rig (rower + oars) by
        /// severing the <see cref="BoatRowAnimator"/>'s <c>_oarRig</c> ref — so the animator can't re-activate
        /// it each frame (it re-enables the rig whenever the home hull is active) — then deactivating the rig
        /// object; and (3) adds a <see cref="DirectionalBoatSprite"/> on a child renderer, configured with the
        /// 4 facings (CW from North, snap mode).
        ///
        /// <para>The Engine hull is a dedicated MINIMAL Def, NOT the Punt and NOT in OwnedFleet's registry —
        /// so the buy-the-Punt grant + save-restore (both id-keyed) never revert the skin's hull on
        /// board/grant/load (it STICKS). Applied after the dory hull + OwnedFleet are wired, so it is the final
        /// serialized hull state. Null-safe: if EITHER the <c>boat.fishing_skiff</c> Def OR the facing art
        /// isn't imported, the rowed dory hull + oars are left exactly as built (no half-state powerboat-skin-
        /// on-rowboat), the swap no-ops with a warning, and the build never breaks before the assets are in.
        /// Boats-core source is NOT modified — this only re-points the same serialized hull refs the builder
        /// already sets for the dory.</para>
        /// </summary>
        static void ApplyDirectionalFishingBoatVisual(GameObject doryGo, SpriteRenderer hullRenderer,
                                                      GameObject oarRig, BoatRowAnimator rowAnim,
                                                      BoatController boat, ShipHold hold)
        {
            // Load the 4 facings up-front (CW from North). LoadSpriteAny handles Single OR Multiple import.
            var facings = new Sprite[FishingBoatFacingPaths.Length];
            for (int i = 0; i < FishingBoatFacingPaths.Length; i++)
                facings[i] = LoadSpriteAny(FishingBoatFacingPaths[i]);

            // Load the dedicated Engine hull (boat.fishing_skiff) the skin drives on. Reload from disk so an
            // intervening import can't hand back a stale instance (the builder's persist-the-refs gotcha).
            var engineHull = AssetDatabase.LoadAssetAtPath<BoatHullDef>(DataFishingSkiff);

            if (facings[0] == null || engineHull == null)
            {
                // Art or the Engine Def not imported yet → leave the dory hull + oars EXACTLY as built (no
                // half-swap: never a powerboat picture on rowboat controls, nor engine controls under the dory
                // hull). Re-run after the FishingBoat_N/E/S/W PNGs + FishingSkiff.asset import to get the skin.
                Debug.LogWarning("[PersistentCoreBuilder] Fishing-boat skin assets missing (facings[0]=" +
                                 (facings[0] != null) + ", engineHull=" + (engineHull != null) + "; e.g. " +
                                 FishingBoatFacingPaths[0] + " / " + DataFishingSkiff + ") — left the hand-rowed " +
                                 "dory hull + oars in place. Open Unity so the assets import, then re-run the " +
                                 "start builder to get the directional ENGINE fishing-boat skin.");
                return;
            }

            // (0) Drive as an ENGINE boat: re-point the boat's + hold's serialized hull to boat.fishing_skiff
            // (Propulsion = Engine), OVERRIDING the dory hull set above. The controller's data-driven branch
            // (BoatController.UsesEngineHelm) then takes the outboard helm (throttle + speed-scaled rudder) and
            // DevBoatInput sends the engine scheme — so the controls match the powerboat picture. Same
            // SetRef-the-serialized-field path the dory hull used; OwnedFleet's {Dory[,Punt]} registry doesn't
            // include the skiff, so a buy/grant/load never reverts it. Flag off → this line never runs and the
            // dory (Oars) hull from above stands.
            SetRef(boat, "_hull", engineHull);
            SetRef(hold, "_hull", engineHull);

            // (1) Hide the dory HULL picture. Disable the renderer only — keep its sprite ref intact so
            // OwnedFleet's id-keyed hull swap (which sets .sprite, never .enabled) is unaffected by the swap.
            if (hullRenderer != null) hullRenderer.enabled = false;

            // (2) Hide the OAR RIG (rower + both oars). The BoatRowAnimator RE-ACTIVATES its _oarRig every
            // Update while the home hull is active, so we must SEVER that ref first (null) — then the animator
            // can't turn the rig back on — before deactivating the rig object. The animator itself stays
            // (it's Boats-core, harmless: it just rotates now-inactive oar pivots). Fully reversible: flip the
            // flag false + re-run to re-wire _oarRig and leave the rig active.
            SetRef(rowAnim, "_oarRig", null);
            if (oarRig != null) oarRig.SetActive(false);

            // (3) Add the DirectionalBoatSprite on a CHILD renderer of the boat body (the body transform
            // turns with physics; the component counter-rotates this child to keep the facing screen-aligned).
            // sortingOrder 1 draws it above the (now-hidden) hull at 0 and below the on-foot player at 10.
            var spriteGo = new GameObject("FishingBoatVisual");
            spriteGo.transform.SetParent(doryGo.transform, false);
            var sr = spriteGo.AddComponent<SpriteRenderer>();
            sr.sprite = facings[0];          // start facing North (until the first LateUpdate snaps to heading)
            sr.sortingOrder = 1;

            var directional = doryGo.AddComponent<DirectionalBoatSprite>();
            directional.Configure(
                facings, sr,
                zeroHeadingDegrees: 0f,                       // facings[0] is the North-facing sprite
                smoothModeSprite: facings[0],                 // unused in Snap; same art if ever toggled to Smooth
                mode: DirectionalBoatSprite.RotationMode.SnapDirectional);

            Debug.Log("[PersistentCoreBuilder] Playable boat wears the 4-way DIRECTIONAL FISHING-BOAT skin " +
                      "(#93/#94 visual, #97 propulsion): hull + oar rig hidden, FishingBoat_N/E/S/W snap by " +
                      "heading, and the hull is boat.fishing_skiff (Propulsion = Engine) so the controls are " +
                      "an OUTBOARD HELM (throttle ahead/astern + a speed-scaled rudder that bites with way), " +
                      "NOT hand-rowed oars. The Engine hull is not in OwnedFleet's registry, so it sticks past " +
                      "boarding/grants/load. Reversible — set UseDirectionalFishingBoatVisual=false + re-run " +
                      "to restore the hand-rowed dory hull + oars.");
        }

        // ---- serialized-ref helpers (the builders' persist-the-refs convention) --------------------

        static void SetRef(Component c, string field, Object value)
        {
            var so = new SerializedObject(c);
            var prop = so.FindProperty(field);
            if (prop != null) { prop.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
            else Debug.LogWarning($"[PersistentCoreBuilder] {c.GetType().Name} has no field '{field}'.");
        }

        static void SetRefArray(Component c, string field, Object[] values)
        {
            var so = new SerializedObject(c);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"[PersistentCoreBuilder] no array field '{field}'."); return; }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetString(Component c, string field, string value)
        {
            var so = new SerializedObject(c);
            var prop = so.FindProperty(field);
            if (prop != null && prop.propertyType == SerializedPropertyType.String)
            { prop.stringValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        static void SetBool(Component c, string field, bool value)
        {
            var so = new SerializedObject(c);
            var prop = so.FindProperty(field);
            if (prop != null && prop.propertyType == SerializedPropertyType.Boolean)
            { prop.boolValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
            else Debug.LogWarning($"[PersistentCoreBuilder] {c.GetType().Name} has no bool field '{field}'.");
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

        // ---- art loading (mirrors the cove builder's; the dory rig art is shared) ------------------

        static Sprite LoadArtSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        static Sprite LoadSpriteAny(string path)
        {
            var direct = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (direct != null) return direct;
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                                 .OrderBy(s => SpriteIndex(s.name)).FirstOrDefault();
        }

        static Sprite[] LoadSheetFrames(string path)
            => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                            .OrderBy(s => SpriteIndex(s.name)).ToArray();

        static int SpriteIndex(string spriteName)
        {
            int u = spriteName.LastIndexOf('_');
            return (u >= 0 && int.TryParse(spriteName.Substring(u + 1), out int n)) ? n : 0;
        }

        // One oar of the row rig (mirrors the cove builder): an oarlock PIVOT at the gunwale with Oar.png
        // offset so the fulcrum sits on the pivot origin → rotating the pivot swings the oar about its oarlock.
        static Transform MakeOar(Transform parent, string name, Vector2 oarlockLocalPos, bool mirror,
                                 Sprite oarSprite, Sprite fallback)
        {
            const float fulcrumFromHandle = 0.56f;
            var pivot = new GameObject(name + "Pivot");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = new Vector3(oarlockLocalPos.x, oarlockLocalPos.y, 0f);

            var oar = new GameObject(name);
            oar.transform.SetParent(pivot.transform, false);
            var osr = oar.AddComponent<SpriteRenderer>();
            osr.sortingOrder = 1;
            if (oarSprite != null)
            {
                osr.sprite = oarSprite;
                osr.flipX = mirror;
                oar.transform.localPosition = new Vector3(mirror ? fulcrumFromHandle : -fulcrumFromHandle, 0f, 0f);
            }
            else
            {
                osr.sprite = fallback; osr.color = new Color(0.55f, 0.40f, 0.24f);
                oar.transform.localScale = new Vector3(3.5f, 0.4f, 1f);
                oar.transform.localPosition = new Vector3(mirror ? 0.9f : -0.9f, 0f, 0f);
            }
            return pivot.transform;
        }
    }
}
#endif
