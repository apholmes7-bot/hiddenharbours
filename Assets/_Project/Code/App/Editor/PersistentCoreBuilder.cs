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
using HiddenHarbours.Art;           // BoatSpotlight (durable night-nav beam on the boat root, ADR 0016)
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
        const string ArtPlayerHaul = "Assets/_Project/Art/Characters/PlayerHaul.png"; // deck-haul sheet (sliced 3×4; frames 0..7 used)

        // The owner's PlayerHaul sheet spec: the first HaulFrameCount slices are the animation — 0..5 the
        // hand-over-hand pull cycle, 6 STRAIN, 7 EASE. The sheet keeps the historical 12-cell shape (like
        // FisherSheet); the tail cells are unused alternates the animator never reads.
        const int HaulFrameCount = 8;

        // --- DIRECTIONAL BOAT VISUAL (the ISO DORY skin; #93 DirectionalBoatSprite, #202 rock grid) -------
        // REVERSIBLE FLAG. While true, the playable boat WEARS a directional facing set — snap to the nearest
        // of N/NE/E/SE/S/SW/W/NW from heading, picture stays screen-aligned — instead of the single rotating
        // DoryHull picture + the legacy transform oar rig. It is a VISUAL swap ONLY: the boat drives on the
        // ROWED dory hull (boat.dory, Propulsion = Oars → per-oar strokes), and the layered oar overlay
        // (DoryOarLayer) animates from the same LeftOar/RightOar state the physics reads.
        //
        // HISTORY (read before touching): #93/#94/#97 also swapped the hull to the Engine boat.fishing_skiff
        // so the CONTROLS matched a POWERBOAT picture ("a power boat skin, not a rowboat" — the facings were
        // M2 fleet art). The owner has since decided the dory ROWS again: the art is a rowboat (#202 iso dory)
        // and the independent oars landed (#204), so the engine-helm swap is GONE and boat.dory stands.
        // FishingSkiff.asset is left on disk (append-only ids; ambient/M2 hulls may want it) but the player's
        // boat never loads it. Flip this flag FALSE + re-run the start builder to get the plain rotating
        // DoryHull picture + the legacy oar rig back; the hull/propulsion is boat.dory either way.
        const bool UseDirectionalFishingBoatVisual = true;

        // The 8 owner-drawn facings, in CLOCKWISE order from the ZERO heading (North):
        // N, NE, E, SE, S, SW, W, NW. The DirectionalBoatSprite math (#93) is generalised to any facing
        // count, so the full compass drops in with NO math change; element 0 must be the North-facing
        // sprite and the zero-heading is 0° (North/up), matching the project's bearing convention.
        // Every facing shares the 128×128 canvas and the bbox-centre custom pivot convention from the
        // 4-cardinal pass (pivot = alpha-bbox centre, set in each .png.meta) so the hull stays put on a snap.
        static readonly string[] FishingBoatFacingPaths =
        {
            "Assets/_Project/Art/Boats/FishingBoat_N.png",
            "Assets/_Project/Art/Boats/FishingBoat_NE.png",
            "Assets/_Project/Art/Boats/FishingBoat_E.png",
            "Assets/_Project/Art/Boats/FishingBoat_SE.png",
            "Assets/_Project/Art/Boats/FishingBoat_S.png",
            "Assets/_Project/Art/Boats/FishingBoat_SW.png",
            "Assets/_Project/Art/Boats/FishingBoat_W.png",
            "Assets/_Project/Art/Boats/FishingBoat_NW.png",
        };

        // The owner's NEW isometric dory art (wave-coupled rock). DoryIso = 8 static hull headings
        // (index = heading N..NW); DoryIsoRock = 64 rock frames (index = heading×8 + frame). When both
        // import + slice (SpriteSheetSlicer manifest), the playable boat wears these AS the directional
        // facings PLUS a rock grid, so BoatWaveMotion drives the visible rock by frame from the wave
        // phase under the hull (crest → frame 2, trough → 6). Preferred over the FishingBoat_* placeholder
        // facings; if the iso sheets aren't imported yet, the FishingBoat compass stands (with the legacy
        // transform rock) so the build never breaks before the art lands.
        const string ArtDoryIso     = "Assets/_Project/Art/Boats/DoryIso.png";       // 8 static headings
        const string ArtDoryIsoRock = "Assets/_Project/Art/Boats/DoryIsoRock.png";   // 64 rock frames
        const int DoryIsoStaticCount = 8;
        const int DoryIsoRockFrameCount = 8;
        const int DoryIsoRockCount = DoryIsoStaticCount * DoryIsoRockFrameCount; // 64

        // The INDEPENDENT OAR overlays (#204 art): one sheet per side, 10 cols × 8 heading rows → 80 each,
        // index = heading×10 + col (cols 0..7 = the row-stroke cycle, 8 = resting/shipped, 9 = trailing).
        // Same 160×156 cell + waterline pivot as the hull sheets, so an overlay at the SAME localPosition
        // registers pixel-perfect on the hull (art README: every layer pinned to pivot (80,88); draw order
        // hull → rower → port oar → star oar). DoryOarLayer animates each side from BoatController's real
        // per-oar state, replacing the legacy transform oar rig for this hull.
        const string ArtDoryOarPort = "Assets/_Project/Art/Boats/DoryOarPort.png";
        const string ArtDoryOarStar = "Assets/_Project/Art/Boats/DoryOarStar.png";
        const int DoryOarColumnCount = 10;                                        // per heading row
        const int DoryOarFrameCount = DoryIsoStaticCount * DoryOarColumnCount;    // 80

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

            // --- THE ISO DORY SKIN (directional facings + rock grid + independent oars; #93/#202/#204) ----
            // When the flag is on, the boat the player sails WEARS the owner's 8-way iso dory facings, its
            // wave-coupled rock grid, and the layered per-side oar overlays — instead of the single rotating
            // DoryHull picture + the legacy transform oar rig. VISUAL ONLY: the hull stays the ROWED
            // p.StartDory (boat.dory, Propulsion = Oars) set above, so the helm is per-oar strokes and
            // OwnedFleet's id-keyed registry/save-restore behave exactly as they do without the skin.
            // Self-contained + null-safe (no-ops to the plain dory look if the art isn't imported).
            if (UseDirectionalFishingBoatVisual)
                ApplyDirectionalFishingBoatVisual(doryGo, sr, oarRig, rowAnim, boat);

            // --- THE BOAT SPOTLIGHT (ADR 0016) — durable, root-hosted, follows the bow -----------------------
            // The owner re-added this by hand every session and lost it on rebuild; mount it at BUILD time so the
            // night-nav beam is durable. It rides the BoatController ROOT (the Rigidbody2D 'Dory'), NOT the
            // counter-rotated FishingBoatVisual child — the beam must ride the ROTATING body to follow the bow
            // (the child is stomped back to world-identity every LateUpdate). BoatSpotlight adds + drives its own
            // SceneLight cone and auto-gates to night (invisible by day). This mirrors the 'Lighting ▸ Add
            // Spotlight' menu (GetComponentInParent<Rigidbody2D>). Its bounce reads the FishingBoatVisual child's
            // wave rock by NAME (Art stays decoupled from Boats), so it bobs/sways with the hull when B2 is on.
            // Added AFTER the directional skin so the visual child exists for the bounce to find.
            if (doryGo.GetComponent<BoatSpotlight>() == null)
                doryGo.AddComponent<BoatSpotlight>();

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

            // DECK WALKING (trap arc Build 5 — the on-deck control state): boarding lands the player ON
            // THE DECK as a walkable character; this controller drives them around the small bounded deck
            // while the boat rocks/drifts under them. Disabled here — the ControlSwitcher owns it (enabled
            // only while OnDeck; the walk controller drives ashore, the helm drives when taken). The deck
            // bounds/speed are its serialized tunables; the switcher parents the player to the boat's
            // PHYSICS ROOT (never the counter-rotated visual child) when boarding.
            var deckWalk = playerGo.AddComponent<DeckWalkController>();
            deckWalk.enabled = false;

            // THE HAUL ANIMATION (owner's PlayerHaul sheet): while a trap haul is live the deck-walking
            // fisher plays the hand-over-hand cycle as line comes in, the STRAIN frame while the rope
            // fights back, the EASE frame while the pawl holds — all read off the Core TrapHaulStateChanged
            // snapshots (rule 4: no Fishing reference), facing the worked buoy via flipX. Frames are the
            // first 8 slices of the sheet in slice order (0..5 cycle, 6 strain, 7 ease — the owner's spec);
            // missing art → an empty array → the component is inert (null-safe greybox rule).
            var haulAnim = playerGo.AddComponent<PlayerHaulAnimator>();
            var haulFrames = LoadSheetFrames(ArtPlayerHaul).Take(HaulFrameCount).ToArray();
            SetRefArray(haulAnim, "_frames", haulFrames.Cast<Object>().ToArray());
            if (haulFrames.Length < HaulFrameCount)
                Debug.LogWarning($"[PersistentCoreBuilder] PlayerHaul sheet gave {haulFrames.Length}/" +
                                 $"{HaulFrameCount} frames ({ArtPlayerHaul}) — the haul animation will be " +
                                 "inert/partial until the sheet imports with its 3×4 slicing. Re-run the " +
                                 "start builder after import.");

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
            SetRef(switcher, "_deckWalk", deckWalk);   // Build 5: board → deck; walk to the helm to drive
            // _dockZone / _disembarkPoint are left for the scene to wire via its RegionAnchor (SetDock on
            // arrival) — a start scene that wants an immediate cove-style dock can SetRef them after Build.

            // --- TRAVEL RIG (persistent loader + coordinator; the scene fills the region list) -------------
            var loaderGo = new GameObject("RegionSceneLoader");
            var loader = loaderGo.AddComponent<RegionSceneLoader>();
            SetString(loader, "_currentSceneName", p.CurrentSceneName);

            // --- GREYBOX TOASTS (Build 5 — DevToast): on-screen feedback for the trap loop so the owner
            // never needs the Unity Console. Listens to the Core DevNotice/FishCaught signals only (the
            // Fishing publishers never reference it — rule 4); pre-allocates its text pool (rule 7).
            var toastGo = new GameObject("DevToast");
            toastGo.AddComponent<DevToast>();

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
            toastGo.AddComponent<PersistentObject>();
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

        // ---- the iso-dory skin (directional facings #93 · wave rock grid #202 · independent oars #204) ----

        /// <summary>
        /// Make the playable boat WEAR the owner's 8-way directional facings — preferring the ISO DORY art
        /// (8 static headings + a 64-frame heading×rock grid + the two independent oar overlays) and falling
        /// back to the older FishingBoat_* compass while that art isn't imported. It (1) HIDES the single
        /// rotating dory hull picture (disables the hull SpriteRenderer; its sprite ref stays so OwnedFleet's
        /// id-keyed swap is unaffected); (2) retires the LEGACY transform oar rig (rower + rotating Oar.png on
        /// pivots) by severing the <see cref="BoatRowAnimator"/>'s <c>_oarRig</c> ref — the animator re-activates
        /// the rig every Update while its home hull is active, so the ref must go BEFORE the object is
        /// deactivated — because the baked <see cref="DoryOarLayer"/> overlay draws the oars for this hull and
        /// two rigs would double-render them; (3) adds a <see cref="DirectionalBoatSprite"/> on a child renderer
        /// (8 facings, CW from North, snap mode); (4) adds <see cref="BoatWaveMotion"/> on that child; and
        /// (5) layers the two baked oar overlays over it, animated from the boat's real per-oar state.
        ///
        /// <para><b>VISUAL ONLY — the dory ROWS.</b> The hull the boat drives on is the rowed <c>boat.dory</c>
        /// (Propulsion = Oars → per-oar strokes, <c>BoatController.ApplyOarDrive</c>) that the caller already
        /// serialized; nothing here re-points <c>_hull</c>. #93/#94/#97 used to swap it to the Engine
        /// <c>boat.fishing_skiff</c> so the helm matched a POWERBOAT picture ("a power boat skin, not a
        /// rowboat") — the owner has since decided the dory rows again now the art is a rowboat and the
        /// independent oars have landed, so that swap is gone and the fleet registry / save-restore see the
        /// plain <c>boat.dory</c> they were always keyed to.</para>
        ///
        /// <para>Null-safe: with NEITHER facing set imported the plain rotating dory hull + its legacy oar rig
        /// are left exactly as built (no half-state, no PARTIAL compass snapping into a stale facing), the skin
        /// no-ops with a warning, and the build never breaks before the assets are in. With the iso hull but no
        /// oar sheets, the hull skin still applies and the oar layer alone no-ops (the legacy rig stays retired
        /// — a rowboat with no oars drawn beats two oar rigs fighting).</para>
        /// </summary>
        static void ApplyDirectionalFishingBoatVisual(GameObject doryGo, SpriteRenderer hullRenderer,
                                                      GameObject oarRig, BoatRowAnimator rowAnim,
                                                      BoatController boat)
        {
            // Prefer the owner's ISO DORY art: 8 static hull headings + a 64-frame heading×rock grid. When
            // both sheets import + slice, the boat wears the iso dory AND gets a rock grid, so the visible
            // rock is DRAWN by frame from the wave under the hull (wave-coupled rock — the ask).
            var isoStatic = LoadSheetFrames(ArtDoryIso);       // ordered DoryIso_0..7 (index = heading)
            var isoRock = LoadSheetFrames(ArtDoryIsoRock);     // ordered DoryIsoRock_0..63 (index = heading×8+frame)
            bool useIsoDory =
                isoStatic.Length == DoryIsoStaticCount && isoStatic.All(s => s != null) &&
                isoRock.Length == DoryIsoRockCount && isoRock.All(s => s != null);

            // Fallback compass (pre-iso placeholder art). LoadSpriteAny handles Single OR Multiple import.
            var facings = new Sprite[FishingBoatFacingPaths.Length];
            for (int i = 0; i < FishingBoatFacingPaths.Length; i++)
                facings[i] = LoadSpriteAny(FishingBoatFacingPaths[i]);
            bool useFallbackCompass = !useIsoDory && facings.All(f => f != null);

            if (!useIsoDory && !useFallbackCompass)
            {
                // No directional art imported → leave the plain dory hull + its legacy oar rig EXACTLY as
                // built. Re-run after DoryIso/DoryIsoRock import + slice to get the iso dory skin.
                string missing = string.Join(", ", FishingBoatFacingPaths.Where((p, i) => facings[i] == null));
                Debug.LogWarning("[PersistentCoreBuilder] No directional boat art imported (iso dory sheets " +
                                 "missing/unsliced; fallback facings missing: " +
                                 (missing.Length > 0 ? missing : "none") + ") — left the plain rotating dory " +
                                 "hull + the legacy oar rig in place. Open Unity so the sheets import + slice " +
                                 "(Hidden Harbours ▸ Art ▸ Slice…), then re-run the start builder.");
                return;
            }

            Sprite[] visualFacings = useIsoDory ? isoStatic : facings;

            // (1) Hide the dory HULL picture. Disable the renderer only — keep its sprite ref intact so
            // OwnedFleet's id-keyed hull swap (which sets .sprite, never .enabled) is unaffected by the swap.
            if (hullRenderer != null) hullRenderer.enabled = false;

            // (2) Retire the LEGACY transform OAR RIG (rower + two rotating Oar.png pivots). The baked
            // DoryOarLayer overlay below draws this hull's oars from the SAME LeftOar/RightOar state, so
            // leaving the old rig up would double-render them. The BoatRowAnimator RE-ACTIVATES its _oarRig
            // every Update while the home hull is active, so SEVER that ref first (null) — then the animator
            // can't turn the rig back on — before deactivating the rig object. The animator class itself stays
            // (Boats-core; any non-iso hull that still wires a rig keeps using it). Fully reversible: flip the
            // flag false + re-run to re-wire _oarRig and leave the rig active.
            SetRef(rowAnim, "_oarRig", null);
            if (oarRig != null) oarRig.SetActive(false);

            // (3) Add the DirectionalBoatSprite on a CHILD renderer of the boat body (the body transform
            // turns with physics; the component counter-rotates this child to keep the facing screen-aligned).
            // sortingOrder 1 draws it above the (now-hidden) hull at 0 and below the on-foot player at 10.
            var spriteGo = new GameObject("FishingBoatVisual");
            spriteGo.transform.SetParent(doryGo.transform, false);
            var sr = spriteGo.AddComponent<SpriteRenderer>();
            sr.sprite = visualFacings[0];    // start facing North (until the first LateUpdate snaps to heading)
            sr.sortingOrder = 1;

            var directional = doryGo.AddComponent<DirectionalBoatSprite>();
            directional.Configure(
                visualFacings, sr,
                zeroHeadingDegrees: 0f,                       // element 0 is the North-facing sprite
                smoothModeSprite: visualFacings[0],           // unused in Snap; same art if ever toggled to Smooth
                mode: DirectionalBoatSprite.RotationMode.SnapDirectional);

            // Wire the wave-coupled ROCK GRID (iso dory only): DirectionalBoatSprite draws
            // rockGrid[heading×8 + RockFrame] instead of the static facing, and BoatWaveMotion below sets
            // RockFrame from the wave phase. With the FishingBoat fallback there is no rock grid, so the
            // static compass stands and BoatWaveMotion uses its legacy transform rock.
            if (useIsoDory)
                directional.ConfigureRock(isoRock, DoryIsoRockFrameCount);

            // (4) B2 (ADR 0018): the boat ROCKS on the shared wave field — visual-only. BoatWaveMotion
            // samples WaveMath under the hull each frame and rolls/pitches/bobs the FishingBoatVisual
            // CHILD (roll routed through DirectionalBoatSprite.VisualTiltDegrees, because that component
            // stomps the child's rotation every LateUpdate). The physics body, colliders and controller
            // forces are untouched (forces are B3, after the owner's feel verdict). NOTE: the plain
            // fallback path above draws its hull sprite ON the physics root — there is no separate
            // visual child to offset there, so the rock rides the directional-visual path only; B3
            // moves the body itself and will cover both.
            var waveMotion = doryGo.AddComponent<BoatWaveMotion>();
            waveMotion.Configure(spriteGo.transform, directional);

            // (5) THE OARS (#204): layer the two baked per-side overlays over the hull picture and animate
            // them from the boat's real per-oar state. Iso art only — the fallback compass has no oar sheets.
            bool oarsWired = useIsoDory && WireIsoDoryOars(doryGo, spriteGo.transform, sr, boat, directional);

            Debug.Log(useIsoDory
                ? "[PersistentCoreBuilder] Playable boat wears the ISO DORY skin (8 static headings + 64-frame " +
                  "rock grid): BoatWaveMotion drives the visible rock BY FRAME from the wave phase under the hull " +
                  "(crest → frame 2, trough → 6), so the rock corresponds to the waves. The fake transform rock is " +
                  "retired for this hull (frames own it). The boat drives ROWED on " +
                  (boat.Hull != null ? boat.Hull.Id : "(no hull)") + " (per-oar strokes), and the independent oar " +
                  "overlays are " + (oarsWired ? "LAYERED + animated from the live LeftOar/RightOar state." :
                  "NOT wired (DoryOarPort/DoryOarStar missing or unsliced) — no oars are drawn; import + slice " +
                  "them and re-run.")
                : "[PersistentCoreBuilder] Iso dory art not imported — fell back to the FishingBoat_* compass with " +
                  "the legacy transform rock and NO oar overlay. Import + slice DoryIso/DoryIsoRock/DoryOarPort/" +
                  "DoryOarStar, then re-run the start builder for the wave-coupled iso dory that rows.");
        }

        /// <summary>
        /// Layer the two BAKED oar overlays (port + starboard) over the iso hull picture and wire
        /// <see cref="DoryOarLayer"/> to animate them from the boat's real per-oar state. Both renderers are
        /// CHILDREN of the hull's visual child, so they inherit the exact snap/counter-rotation treatment the
        /// hull gets (they must never smooth-rotate while the hull snaps) and register pixel-perfect on it —
        /// the sheets share the hull's cell + waterline pivot, so localPosition is zero. Sorting follows the
        /// art README's draw order (hull → port oar → star oar) on the hull's own sorting layer. Returns false
        /// (wiring nothing) unless BOTH sheets give their full 80 ordered slices — a partial sheet would index
        /// into a stale cell.
        /// </summary>
        static bool WireIsoDoryOars(GameObject doryGo, Transform visual, SpriteRenderer hullVisual,
                                    BoatController boat, DirectionalBoatSprite directional)
        {
            var port = LoadSheetFrames(ArtDoryOarPort);   // DoryOarPort_0..79 (index = heading×10 + col)
            var star = LoadSheetFrames(ArtDoryOarStar);   // DoryOarStar_0..79
            if (port.Length != DoryOarFrameCount || port.Any(s => s == null) ||
                star.Length != DoryOarFrameCount || star.Any(s => s == null))
            {
                Debug.LogWarning($"[PersistentCoreBuilder] Oar sheets gave {port.Length}/{star.Length} of " +
                                 $"{DoryOarFrameCount} slices each ({ArtDoryOarPort} / {ArtDoryOarStar}) — the " +
                                 "independent oar overlay is NOT wired (no half-state: a partial sheet would " +
                                 "index a stale cell). Slice them (Hidden Harbours ▸ Art ▸ Slice Environment + " +
                                 "VFX Sheets) and re-run the start builder.");
                return false;
            }

            SpriteRenderer MakeOarRenderer(string name, Sprite first, int sortingOrder)
            {
                var go = new GameObject(name);
                go.transform.SetParent(visual, false);        // rides the hull picture: same snap, same pose
                go.transform.localPosition = Vector3.zero;    // shared pivot ⇒ pixel-perfect registration
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = first;
                r.sortingLayerID = hullVisual.sortingLayerID; // same layer as the hull — only the order differs
                r.sortingOrder = sortingOrder;
                return r;
            }

            // Draw order (art README): hull → rower → port oar → star oar. The hull visual sits at its own
            // order; the oars take the next two so they always draw ON it and never against each other.
            var portSr = MakeOarRenderer("OarPort", port[DoryOarMath.RestingColumn], hullVisual.sortingOrder + 1);
            var starSr = MakeOarRenderer("OarStar", star[DoryOarMath.RestingColumn], hullVisual.sortingOrder + 2);

            var layer = doryGo.AddComponent<DoryOarLayer>();
            layer.Configure(port, star, portSr, starSr, boat, directional, DoryIsoStaticCount, DoryOarColumnCount);
            return true;
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
