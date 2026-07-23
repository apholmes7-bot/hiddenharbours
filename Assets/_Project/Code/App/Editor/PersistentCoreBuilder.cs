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

        // The Rod Fishing v2 FIGHT sheets (8 dir × N frames, sliced d/f — the character rig kit, PR #244).
        // Wired onto PlayerFishingAnimator below; a sheet that hasn't imported/sliced yet simply leaves
        // that pose inert (null-safe greybox rule), so this builder is safe to run before/after the art.
        const string ArtFisherIsoFolder = "Assets/_Project/Art/Characters/Iso";

        // THE ON-FOOT PLAYER'S 8-DIRECTION ISO SKIN — an ASSET path, not an art path. Everything about the
        // sheets (counts, frame rates, which way the rows run) lives in the def; CharacterVisualLibraryBuilder
        // is the only thing that knows where the PNGs are.
        const string IsoFisherVisual = "Assets/_Project/Data/Characters/FisherIso.asset";

        // Heading (deg, 0 = N, CW) the fisher faces at boot: 180 = South = looking toward the camera.
        const float FacingCameraHeadingDegrees = 180f;

        // The iso cell in metres (88 px at PPU 32) and the neck-deep cap as a fraction of it. Both are
        // measurements of the ART, read off the Fisher sheets — see the wade block below for the derivation.
        const float IsoCellHeightMeters = 88f / 32f;    // 2.75
        const float IsoNeckDeepFraction = 0.47f;        // just under the top of the head at uv.y 0.545

        // The owner's PlayerHaul sheet spec: the first HaulFrameCount slices are the animation — 0..5 the
        // hand-over-hand pull cycle, 6 STRAIN, 7 EASE. The sheet keeps the historical 12-cell shape (like
        // FisherSheet); the tail cells are unused alternates the animator never reads.
        const int HaulFrameCount = 8;

        // --- THE HULL SKIN — DATA, NOT A CONST ------------------------------------------------------
        // The 8-way iso-dory facings, the 64-frame wave-coupled rock grid and the two 80-cell oar sheets
        // used to be a const bool + a fistful of const art paths RIGHT HERE, which meant a hull could not
        // say what it looked like and OwnedFleet could not re-skin the boat on a swap. That is now DATA
        // (rule 2): p.StartDory.Visual points at a BoatVisualDef, BoatVisualLibraryBuilder imports the
        // sheets into it, and BoatHullSkinner (runtime, shared with OwnedFleet and the ambient fleet) is
        // the one place that installs the rig. To take the skin off, clear the hull asset's Visual ref and
        // re-run — no code edit, no flag.
        //
        // HISTORY (read before touching): #93/#94/#97 also swapped the hull to the Engine
        // boat.fishing_skiff so the CONTROLS matched a POWERBOAT picture ("a power boat skin, not a
        // rowboat" — the facings were M2 fleet art). The owner has since decided the dory ROWS again: the
        // art is a rowboat (#202 iso dory) and the independent oars landed (#204), so the engine-helm swap
        // is GONE and boat.dory stands. The FishingBoat_* placeholder compass this builder used to fall
        // back to is likewise gone: it was never reachable once the iso art landed, and a compass is now
        // authorable as a BoatVisualDef asset if one is ever wanted again.

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
            // DEV BOAT PICKER (null/empty = no picker spawned): every hull F cycles through at the helm, in
            // order. Deliberately SEPARATE from the fleet registry above — this is a workbench for feeling
            // hulls, not a roster of boats you own, and it must not grow into the M2 ladder (rule 8).
            public BoatHullDef[] DevPickerRoster;
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
            // Interpolated, so the DRAWN boat is not a fixed-step staircase sampled by a per-frame
            // renderer (ADR 0022 phase 5 — the second half of the owner's "stuttery" rocking).
            // BoatController.Awake sets this too, which is what heals scenes built BEFORE this line;
            // setting it here means a newly built scene is already right on disk.
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
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
            SetRef(fishing, "_config", p.Config);   // the owner's flick-cast tuning (GameConfig.FlickCast)
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

            // --- THE HULL SKIN (directional facings + rock grid + independent oars; #93/#202/#204) --------
            // The boat the player sails wears whatever its HULL ASSET says it looks like: p.StartDory.Visual
            // → a BoatVisualDef → BoatHullSkinner. For the dory that is the owner's 8-way iso facings, the
            // wave-coupled rock grid and the layered per-side oar overlays, instead of the single rotating
            // DoryHull picture + the legacy transform oar rig. VISUAL ONLY: the hull stays the ROWED
            // p.StartDory (boat.dory, Propulsion = Oars) set above, so the helm is per-oar strokes and
            // OwnedFleet's id-keyed registry/save-restore behave exactly as they do without the skin.
            // Null-safe: a hull with no Visual (or an unsliced sheet) keeps the plain dory look.
            ApplyHullSkin(doryGo, sr, oarRig, rowAnim, boat, p.StartDory);

            // --- THE BOAT SPOTLIGHT (ADR 0016) — durable, root-hosted, follows the bow -----------------------
            // The owner re-added this by hand every session and lost it on rebuild; mount it at BUILD time so the
            // night-nav beam is durable. It rides the BoatController ROOT (the Rigidbody2D 'Dory'), NOT the
            // counter-rotated FishingBoatVisual child — the beam must ride the ROTATING body to follow the bow
            // (the child is stomped back to world-identity every LateUpdate). BoatSpotlight adds + drives its own
            // SceneLight cone and auto-gates to night (invisible by day). This mirrors the 'Lighting ▸ Add
            // Spotlight' menu (GetComponentInParent<Rigidbody2D>). Its bounce reads the FishingBoatVisual child's
            // wave rock by NAME (Art stays decoupled from Boats), so it bobs/sways with the hull when B2 is on.
            // Added AFTER the directional skin so the visual child exists for the bounce to find.
            //
            // MOUNTED, BUT DARK. The beam is now a TOGGLE — press L at the helm — and it starts OFF: the owner's
            // call, "spotlight should be a toggle button… the rowboat should not have one currently". Note the
            // mount here is still UNCONDITIONAL, and must be: this is the ONE persistent player boat, and the dev
            // picker re-skins it IN PLACE, so the component rides EVERY hull. That inheritance is precisely why
            // the rowboat wore a lit searchlight nobody asked for. The fix is therefore the DEFAULT
            // (BoatSpotlight._startOn = false), not a per-hull mount — a button means any boat can light up when
            // the owner wants it, which is the affordance he chose.
            if (doryGo.GetComponent<BoatSpotlight>() == null)
                doryGo.AddComponent<BoatSpotlight>();

            // --- THE DEV BOAT PICKER (owner affordance): F at the helm re-skins the boat IN PLACE ---------
            // The owner asked to see all his boats available to pilot when he builds St Peters. This is the
            // shape he chose: not moored boats to walk to, not shipwright offers to buy — a key at the helm
            // that swaps the hull under him, so he can A/B two boats in the SAME wave, in the same spot,
            // seconds apart. That comparison is the whole point, and it's one no amount of mooring gets you.
            //
            // Scoped as a DEV affordance on purpose: it builds none of the M2 fleet roster/economy the canon
            // defers (rule 8). The roster is DATA (a BoatHullDef[]) and rides the persistent boat, so the
            // picked hull survives a region hop like every other piece of boat state.
            if (p.DevPickerRoster != null && p.DevPickerRoster.Length > 0)
            {
                var picker = doryGo.AddComponent<DevBoatPicker>();
                SetRefArray(picker, "_roster", p.DevPickerRoster.Cast<Object>().ToArray());
                SetRef(picker, "_boat", boat);
                SetRef(picker, "_hold", hold);
                SetRef(picker, "_hullRenderer", sr);
                // NOT disabled at start (unlike DevBoatInput, which the ControlSwitcher owns): the picker
                // gates itself on the CONTROLLER actually driving, so it's inert ashore without the
                // switcher needing to learn it exists. One less thing to forget to re-enable.
            }

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
            // The OLD flat 3×4 FisherSheet is still wired as the FALLBACK picture. It is what draws the
            // fisher if the iso skin below is missing or incomplete (a fresh clone before the def is built,
            // a re-sliced sheet that stales the refs), so the player is never invisible. When the iso skin IS
            // complete the walk controller yields the renderer to it entirely — see PlayerWalkController's
            // _isoSkinOwnsSprite.
            var fisherFrames = LoadSheetFrames(ArtFisher);
            SetRefArray(walk, "_frames", fisherFrames.Cast<Object>().ToArray());
            if (fisherFrames.Length > 0 && fisherFrames[0] != null)
                playerSr.sprite = fisherFrames[0];

            // --- THE 8-DIRECTION ISO SKIN (what the owner actually sees walking the coast) -----------------
            // Idle / walk / run, one drawn cell per compass direction, picked by the character's own motion.
            // Everything about the art — how many directions, which heading row 0 depicts, which way round
            // the rows run, the frame counts, the frame rates and the gait thresholds — is DATA on the def
            // (rule 2/6); nothing about the sheets is known here. Missing def → the component is inert and
            // the FisherSheet fallback above still draws (null-safe greybox rule).
            var isoVisual = AssetDatabase.LoadAssetAtPath<HiddenHarbours.Core.CharacterVisualDef>(IsoFisherVisual);
            var isoSkin = playerGo.AddComponent<HiddenHarbours.Core.IsoCharacterSprite>();
            SetRef(isoSkin, "_visual", isoVisual);
            if (isoVisual != null && isoVisual.HasAnyArt())
            {
                // Face SOUTH at boot — looking toward the camera, the friendliest way to meet the fisher —
                // and seed the renderer with that cell so the scene view shows the iso art before Play.
                SetFloat(isoSkin, "_initialHeadingDegrees", FacingCameraHeadingDegrees);
                var firstCell = isoVisual.SpriteFor(HiddenHarbours.Core.CharacterGait.Idle,
                                                    isoVisual.FacingRowFor(FacingCameraHeadingDegrees), 0);
                if (firstCell != null) playerSr.sprite = firstCell;
            }
            else
            {
                Debug.LogWarning($"[PersistentCoreBuilder] No complete iso character skin at " +
                                 $"'{IsoFisherVisual}' — the fisher falls back to the old 4-direction " +
                                 "FisherSheet. Run Hidden Harbours ▸ Art ▸ Build Character Visual Defs " +
                                 "(and Slice Iso Character Sheets first if the sheets were re-imported).");
            }

            // --- WADE SUBMERSION, RE-CALIBRATED FOR THE TALLER ISO CELL -----------------------------------
            // PlayerSubmergeVisual clips the waterline on the SPRITE's uv.y, so its two tunables describe the
            // CELL, not the character — and the cell just changed shape. The old flat sheet was 32×64 px
            // (2.0 m at PPU 32) with the fisher filling it; the iso cell is 64×88 px (2.75 m) with the fisher
            // occupying only its lower half (measured across all three Fisher sheets: opaque pixels span rows
            // 40..87 of 88, i.e. uv.y 0.01 at the feet to 0.545 at the top of the hat). Left at the old 1.8 m
            // /0.85 the waterline would have run off the top of the head. Both are serialized tunables, so
            // the owner can still taste-tune them in the inspector without a code change.
            if (isoVisual != null && isoVisual.HasAnyArt())
            {
                var submerge = playerGo.AddComponent<HiddenHarbours.Art.PlayerSubmergeVisual>();
                SetFloat(submerge, "_bodyHeightMeters", IsoCellHeightMeters);
                SetFloat(submerge, "_maxSubmerge", IsoNeckDeepFraction);
            }

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

            // THE ROD-FIGHT ANIMATION (Rod Fishing v2 wave 3): while a rod interaction is live the fisher
            // plays the baked fight sheets — the two-tap BITE tell, the STRIKE as the hook sets, the REEL
            // cycle through the deep→surface fight (and the legacy fight), the LAND beat — all read off
            // the Core FishingStateChanged snapshots (rule 4: no Fishing reference), facing the published
            // fish offset. Sheets are 8-dir × N frames in d/f slice order; a sheet that gave no clean
            // 8-row set wires empty and that pose stays inert (null-safe greybox rule).
            var fightAnim = playerGo.AddComponent<PlayerFishingAnimator>();
            SetRefArray(fightAnim, "_biteFrames", LoadIsoDirFrames($"{ArtFisherIsoFolder}/Fisher_bite.png").Cast<Object>().ToArray());
            SetRefArray(fightAnim, "_strikeFrames", LoadIsoDirFrames($"{ArtFisherIsoFolder}/Fisher_strike.png").Cast<Object>().ToArray());
            SetRefArray(fightAnim, "_reelFrames", LoadIsoDirFrames($"{ArtFisherIsoFolder}/Fisher_reel.png").Cast<Object>().ToArray());
            SetRefArray(fightAnim, "_landFrames", LoadIsoDirFrames($"{ArtFisherIsoFolder}/Fisher_land.png").Cast<Object>().ToArray());

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

        // ---- the hull skin (directional facings #93 · wave rock grid #202 · independent oars #204) ----

        /// <summary>
        /// Dress the persistent Dory in the skin ITS HULL ASSET asks for. Formerly
        /// <c>ApplyDirectionalFishingBoatVisual</c> — a misnomer since #205: it applies no fishing-boat
        /// skin (the owner decided the dory rows again) and no fishing-boat HULL (the engine-helm swap was
        /// reverted). It applies whatever <see cref="BoatHullDef.Visual"/> binds; today, for
        /// <c>boat.dory</c>, that is the iso dory.
        ///
        /// <para>All this does is the two things the SKINNER cannot: (1) it HIDES the plain rotating dory
        /// hull picture — renderer <c>.enabled</c> only, never its sprite ref, so an unskinned hull can
        /// still fall back to it; and (2) it retires the LEGACY transform OAR RIG (rower + two rotating
        /// Oar.png pivots), because the baked <see cref="DoryOarLayer"/> overlay draws this hull's oars
        /// from the SAME LeftOar/RightOar state and two rigs would double-render them. <b>Order is
        /// load-bearing:</b> <see cref="BoatRowAnimator"/> RE-ACTIVATES its <c>_oarRig</c> every Update
        /// while its home hull is active, so the ref must be SEVERED (null) BEFORE the rig object is
        /// deactivated, or the animator turns it straight back on. The animator class itself stays
        /// (Boats-core; any hull that still wires a rig keeps using it).</para>
        ///
        /// <para>The rig itself — the screen-aligned compass child, the wave-coupled rock grid, the wave
        /// motion, the layered oars — is built by <see cref="BoatHullSkinner"/>, the ONE shared installer
        /// that <see cref="OwnedFleet"/> (on a hull swap) and the ambient fleet also use, so the player's
        /// rig can only be wrong in one place.</para>
        ///
        /// <para><b>VISUAL ONLY — the dory ROWS.</b> Nothing here re-points <c>_hull</c>: the boat drives
        /// on the rowed <c>boat.dory</c> (Propulsion = Oars → per-oar strokes,
        /// <c>BoatController.ApplyOarDrive</c>) the caller already serialized. #93/#94/#97 used to swap it
        /// to the Engine <c>boat.fishing_skiff</c> so the helm matched a POWERBOAT picture ("a power boat
        /// skin, not a rowboat") — that swap is gone, and the fleet registry / save-restore see the plain
        /// <c>boat.dory</c> they were always keyed to.</para>
        ///
        /// <para>Null-safe: a hull with no <see cref="BoatHullDef.Visual"/> — or one whose sheets are
        /// unsliced, so the def's compass is empty — leaves the plain rotating dory hull + its legacy oar
        /// rig EXACTLY as built (no half-state, no PARTIAL compass snapping into a stale facing), warns,
        /// and the build never breaks before the assets are in. With the iso hull but no oar sheets the
        /// hull skin still applies and the oar layer alone no-ops (the legacy rig stays retired — a rowboat
        /// with no oars drawn beats two oar rigs fighting).</para>
        /// </summary>
        static void ApplyHullSkin(GameObject doryGo, SpriteRenderer hullRenderer, GameObject oarRig,
                                  BoatRowAnimator rowAnim, BoatController boat, BoatHullDef hull)
        {
            var visual = hull != null ? hull.Visual : null;
            if (visual == null || !visual.HasFullCompass())
            {
                Debug.LogWarning("[PersistentCoreBuilder] Hull '" + (hull != null ? hull.Id : "(none)") +
                                 "' binds no directional Visual with a full compass — left the plain rotating " +
                                 "dory hull + the legacy oar rig in place. Point the hull asset's Visual at a " +
                                 "BoatVisualDef; if its sheets are unsliced, slice them (Hidden Harbours ▸ Art " +
                                 "▸ Slice…), re-run Hidden Harbours ▸ Art ▸ Build Boat Visual Defs, then re-run " +
                                 "the start builder.");
                return;
            }

            // (1) Hide the plain hull PICTURE. Renderer .enabled only — the sprite ref stays intact so a
            // later swap to an UNSKINNED hull can bring this renderer back with something to draw.
            if (hullRenderer != null) hullRenderer.enabled = false;

            // (2) Retire the LEGACY transform oar rig. Sever the ref FIRST (see the order note above), then
            // deactivate. Fully reversible: clear the hull asset's Visual + re-run to get the old rig back.
            SetRef(rowAnim, "_oarRig", null);
            if (oarRig != null) oarRig.SetActive(false);

            // (3) The rig, from data, through the one shared installer.
            var rig = BoatHullSkinner.Apply(doryGo, visual, boat);

            Debug.Log("[PersistentCoreBuilder] Playable boat wears the '" + visual.Id + "' skin (" +
                      visual.HeadingCount + " facings" +
                      (visual.HasRockGrid() ? ", " + visual.RockFrameCount + " rock frames per heading" : "") +
                      ") bound by " + hull.Id + ".Visual — no const flag, no art paths: re-skin by re-pointing " +
                      "the hull asset. " +
                      (visual.HasRockGrid()
                          ? "BoatWaveMotion drives the visible rock BY FRAME from the wave phase under the hull " +
                            "(crest → frame 2, trough → 6), so the rock corresponds to the waves and the fake " +
                            "transform rock is retired for this hull. "
                          : "No rock grid bound — the static compass stands with the legacy transform rock. ") +
                      "The boat drives ROWED on " +
                      (boat != null && boat.Hull != null ? boat.Hull.Id : "(no hull)") +
                      " (per-oar strokes), and the independent oar overlays are " +
                      (rig.Oars != null
                          ? "LAYERED + animated from the live LeftOar/RightOar state."
                          : "NOT wired (the Visual binds no complete oar sheets) — no oars are drawn; slice " +
                            "DoryOarPort/DoryOarStar, re-run Build Boat Visual Defs, then re-run this builder."));
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

        static void SetFloat(Component c, string field, float value)
        {
            var so = new SerializedObject(c);
            var prop = so.FindProperty(field);
            if (prop != null && prop.propertyType == SerializedPropertyType.Float)
            { prop.floatValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
            else Debug.LogWarning($"[PersistentCoreBuilder] {c.GetType().Name} has no float field '{field}'.");
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

        // An 8-direction iso sheet's sprites in direction·framesPerDir + frame order, recovered from the
        // _d<dir>_f<frame> sub-sprite names (the CharacterVisualLibraryBuilder convention — a trailing-
        // number sort would collate every direction's frame 0 together). All-or-nothing: a missing/dupe
        // cell or a count that isn't a clean 8 rows returns EMPTY, so the consumer stays inert rather
        // than half-bound (the visual-def builder's gate).
        static Sprite[] LoadIsoDirFrames(string path)
        {
            const int directions = 8;
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            if (sprites.Length == 0 || sprites.Length % directions != 0) return System.Array.Empty<Sprite>();

            int perDir = sprites.Length / directions;
            var ordered = new Sprite[sprites.Length];
            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (var s in sprites)
            {
                if (s == null || !TryParseIsoCell(s.name, out int dir, out int frame)) return System.Array.Empty<Sprite>();
                if (dir < 0 || dir >= directions || frame < 0 || frame >= perDir) return System.Array.Empty<Sprite>();
                int idx = dir * perDir + frame;
                if (!seen.Add(idx)) return System.Array.Empty<Sprite>();
                ordered[idx] = s;
            }
            return seen.Count == sprites.Length ? ordered : System.Array.Empty<Sprite>();
        }

        static bool TryParseIsoCell(string spriteName, out int dir, out int frame)
        {
            dir = frame = -1;
            if (string.IsNullOrEmpty(spriteName)) return false;
            int f = spriteName.LastIndexOf("_f", System.StringComparison.Ordinal);
            if (f < 0) return false;
            int d = spriteName.LastIndexOf("_d", f, System.StringComparison.Ordinal);
            if (d < 0) return false;
            return int.TryParse(spriteName.Substring(d + 2, f - d - 2), out dir)
                && int.TryParse(spriteName.Substring(f + 2), out frame);
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
