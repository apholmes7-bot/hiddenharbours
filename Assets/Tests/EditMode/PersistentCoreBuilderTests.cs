#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Guards the bug that left St Peters un-playable (#64 scoped the persistent core OUT of the START
    /// scene → no controllable character, nothing moving, no clock/tide). The fix extracts
    /// <see cref="PersistentCoreBuilder"/> so EVERY start scene stands up the SAME rig; this asserts a
    /// single <c>Build</c> produces the controllable core: a Player with a <see cref="PlayerWalkController"/>
    /// at the spawn, a moored (disabled) Dory with its hull + hold, the services root, the follow camera
    /// pointed at the player on foot, and the travel rig — every piece tagged persistent so it survives a
    /// region hop. Pure editor-object construction (no scene load, no PlayMode); the play-mode glue
    /// (DDOL/region toggling) is covered by RegionTravelPersistenceTests + verified in Unity.
    /// </summary>
    public class PersistentCoreBuilderTests
    {
        private PersistentCoreBuilder.Handle _core;
        private GameConfig _config;
        private BoatHullDef _dory;
        private BoatHullDef _punt;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _dory = ScriptableObject.CreateInstance<BoatHullDef>();
            _dory.Id = "boat.dory"; _dory.HoldUnits = 6;
            _punt = ScriptableObject.CreateInstance<BoatHullDef>();
            _punt.Id = "boat.punt"; _punt.HoldUnits = 14;

            _core = PersistentCoreBuilder.Build(new PersistentCoreBuilder.Params
            {
                Config = _config,
                StartDory = _dory,
                PuntHull = _punt,
                RegionFish = null,
                Square = null,
                CameraBackground = Color.black,
                PlayerStartPos = new Vector3(-40f, -2f, 0f),
                BoatMooredPos = new Vector3(-40f, -26f, 0f),
                TideGatedWalk = true,
                CurrentSceneName = "StPeters",
                TideMean = 0f, TideAmplitude = 3.5f, TidePhaseHours = 1f,
            });
        }

        [TearDown]
        public void TearDown()
        {
            // Destroy every root the builder created (each persistent piece is its own root GameObject).
            foreach (var go in new[]
                     {
                         _core.ServicesRoot, _core.CameraGo, _core.PlayerGo, _core.DoryGo, _core.GaugeGo,
                         _core.SwitcherGo, _core.LoaderGo,
                         _core.Coordinator != null ? _core.Coordinator.gameObject : null,
                     })
                if (go != null) Object.DestroyImmediate(go);
            if (_config != null) Object.DestroyImmediate(_config);
            if (_dory != null) Object.DestroyImmediate(_dory);
            if (_punt != null) Object.DestroyImmediate(_punt);
            GameServices.Reset();
        }

        // ---- the player is real + controllable, at the spawn -------------------------------

        [Test]
        public void Build_SpawnsAControllablePlayer_AtTheStartSpawn()
        {
            Assert.IsNotNull(_core.PlayerGo, "the core must spawn a Player (the bug: there was none)");
            var walk = _core.PlayerGo.GetComponent<PlayerWalkController>();
            Assert.IsNotNull(walk, "the Player has a PlayerWalkController so WASD drives it");
            Assert.IsTrue(walk.enabled, "and it starts ENABLED (on foot at the start)");
            Assert.IsNotNull(_core.PlayerGo.GetComponent<Rigidbody2D>(), "a Rigidbody2D for movement");
            Assert.IsNotNull(_core.PlayerGo.GetComponent<CircleCollider2D>(), "a footprint collider keeps it on land");
            Assert.AreEqual(new Vector3(-40f, -2f, 0f), _core.PlayerGo.transform.position, "spawned at the START spawn");
        }

        // ---- the moored dory exists but is static at start ----------------------------------

        [Test]
        public void Build_MoorsTheDory_DisabledAtStart_WithItsHullAndHold()
        {
            Assert.IsNotNull(_core.DoryGo, "the persistent Dory exists (it carries across a region hop)");
            var boat = _core.DoryGo.GetComponent<BoatController>();
            Assert.IsNotNull(boat);
            Assert.IsFalse(boat.enabled, "the Dory is MOORED at start (controller disabled) — WASD drives the player");
            Assert.IsNotNull(_core.DoryGo.GetComponent<ShipHold>(), "it has a hold");
            Assert.IsNotNull(_core.DoryGo.GetComponent<OwnedFleet>(), "and the fleet (so a bought hull swaps in)");
            Assert.AreEqual(new Vector3(-40f, -26f, 0f), _core.DoryGo.transform.position, "moored at the slip");
        }

        // ---- the services run (so the clock/tide advance) -----------------------------------

        [Test]
        public void Build_StandsUpTheServicesRoot_WithClockEnvironmentWalletAndHud()
        {
            Assert.IsNotNull(_core.ServicesRoot, "the services root exists (clock/env/wallet → the tide advances)");
            Assert.IsNotNull(_core.ServicesRoot.GetComponent<GameRoot>(), "GameRoot composes the services");
            Assert.IsNotNull(_core.ServicesRoot.GetComponent<HiddenHarbours.Environment.GameClock>(), "a GameClock to tick time");
            Assert.IsNotNull(_core.ServicesRoot.GetComponent<HiddenHarbours.Environment.EnvironmentService>(), "an EnvironmentService for the tide");
            Assert.IsNotNull(_core.ServicesRoot.GetComponent<PlayerWallet>(), "a wallet");
            Assert.IsNotNull(_core.ServicesRoot.GetComponent<HiddenHarbours.UI.HudController>(), "the glanceable HUD");
        }

        // ---- the camera follows the player on foot ------------------------------------------

        [Test]
        public void Build_CameraFollowsThePlayer_AtTheOnFootFraming()
        {
            Assert.IsNotNull(_core.Camera, "the follow camera exists");
            Assert.AreSame(_core.PlayerGo.transform, _core.Camera.Target,
                "the camera starts following the PLAYER (not the boat) — the on-foot start");
        }

        // ---- the travel rig is present (the sandbar passage to Greywick works) --------------

        [Test]
        public void Build_PlacesTheTravelRig_LoaderAndCoordinator()
        {
            Assert.IsNotNull(_core.Loader, "a RegionSceneLoader carries region travel");
            Assert.AreEqual("StPeters", _core.Loader.CurrentSceneName, "the loader knows its home scene");
            Assert.IsNotNull(_core.Coordinator, "a RegionTravelCoordinator re-binds the rig on arrival");
        }

        // ---- every carried piece survives a region hop --------------------------------------

        [Test]
        public void Build_TagsEveryCarriedPiecePersistent()
        {
            foreach (var go in new[] { _core.PlayerGo, _core.DoryGo, _core.CameraGo, _core.SwitcherGo, _core.GaugeGo, _core.LoaderGo })
                Assert.IsNotNull(go.GetComponent<PersistentObject>(),
                    $"{go.name} must be tagged PersistentObject so it survives the additive region hop");
        }

        // ---- the fleet registers both hulls (Dory + Punt) -----------------------------------

        [Test]
        public void Build_RegistersBothHullsInTheFleet_WhenThePuntIsProvided()
        {
            var fleet = _core.DoryGo.GetComponent<OwnedFleet>();
            var so = new UnityEditor.SerializedObject(fleet);
            var registry = so.FindProperty("_registry");
            Assert.AreEqual(2, registry.arraySize, "the fleet registry holds the Dory + the Punt swap hull");
        }

        // ---- the directional boat SKIN (the iso dory; #93/#94 visual, #202 rock, #204 oars) -------------
        // The playable boat WEARS 8-way directional facings (the iso dory art when imported, else the older
        // FishingBoat_* compass) instead of the single rotating dory hull picture + the legacy transform oar
        // rig. The builder gates this on the UseDirectionalFishingBoatVisual flag (currently ON). These guard
        // the skin's observable effects — the directional component is added + configured, the hull picture is
        // hidden, the LEGACY oar rig is retired (with its BoatRowAnimator ref severed so it can't re-show the
        // rig and double-render against the baked DoryOarLayer overlay), and — the REVERT — the boat still
        // drives ROWED on boat.dory.
        //
        // HISTORY: #93/#94/#97 ALSO swapped the hull to the Engine boat.fishing_skiff so the CONTROLS matched
        // a POWERBOAT picture ("a power boat skin, not a rowboat" — the facings were M2 fleet art). The owner
        // has since decided the dory ROWS again (the art is a rowboat; the independent oars landed), so that
        // swap is gone and Build_DirectionalSkin_DrivesOnTheRowedDoryHull below asserts the new truth — it is
        // the direct rewrite of the old Build_DirectionalSkin_DrivesOnTheEngineFishingSkiffHull.
        //
        // The skin loads the real committed art via AssetDatabase; if it isn't imported in this environment
        // the builder no-ops (by design — no half-state), so the tests skip rather than false-fail.

        // The facings the builder loads (CW from North) — mirrors PersistentCoreBuilder.FishingBoatFacingPaths.
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
        // The iso dory sheets the skin PREFERS (mirrors PersistentCoreBuilder.ArtDoryIso*) + the oar overlays.
        const string IsoDoryPath     = "Assets/_Project/Art/Boats/DoryIso.png";
        const string IsoDoryRockPath = "Assets/_Project/Art/Boats/DoryIsoRock.png";
        const string OarPortPath     = "Assets/_Project/Art/Boats/DoryOarPort.png";
        const string OarStarPath     = "Assets/_Project/Art/Boats/DoryOarStar.png";

        private static Sprite[] Slices(string path)
            => UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();

        private static bool FishingBoatArtImported()
        {
            // The builder no-ops unless EVERY facing loads (a partial compass would snap into a stale
            // picture), so the skip-guard must demand all of them too. Match the builder's
            // Single-OR-Multiple-import-tolerant lookup per path.
            return FishingBoatFacingPaths.All(p =>
                UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(p) != null ||
                UnityEditor.AssetDatabase.LoadAllAssetsAtPath(p).OfType<Sprite>().Any());
        }

        // The iso art the skin prefers: 8 static headings + the 64-frame rock grid, fully sliced.
        private static bool IsoDoryArtImported()
            => Slices(IsoDoryPath).Length == 8 && Slices(IsoDoryRockPath).Length == 64;

        // Both 80-slice oar sheets — the gate the builder's oar-overlay wiring demands (no partial sheet).
        private static bool OarSheetsImported()
            => Slices(OarPortPath).Length == 80 && Slices(OarStarPath).Length == 80;

        // The skin runs when EITHER facing set is present (iso preferred, FishingBoat_* the fallback); with
        // neither, the builder no-ops to the plain dory look. Tests that assert the skin skip unless one is.
        private static bool SkinSwapApplied() => IsoDoryArtImported() || FishingBoatArtImported();

        // ---- THE REVERT: the skin is VISUAL — the dory drives ROWED on boat.dory -----------------
        // (The direct rewrite of Build_DirectionalSkin_DrivesOnTheEngineFishingSkiffHull, which asserted the
        // OLD #97 truth — that the skin swapped the hull to the Engine boat.fishing_skiff. The owner has
        // decided the dory rows again, so this now guards the opposite: the skin must NOT touch the hull.)

        [Test]
        public void Build_DirectionalSkin_DrivesOnTheRowedDoryHull()
        {
            if (!SkinSwapApplied())
                Assert.Ignore("No directional boat art imported in this environment — the builder no-ops the " +
                              "skin by design; the rowed hull assertions below are what it leaves anyway, but " +
                              "the skin path they guard didn't run.");

            var boat = _core.DoryGo.GetComponent<BoatController>();
            Assert.IsNotNull(boat.Hull, "the skinned boat has a hull");
            Assert.AreEqual("boat.dory", boat.Hull.Id,
                "the skin is VISUAL — the boat must still drive on the ROWED dory hull (the owner's call: the " +
                "art is a rowboat again, so the helm is oars again; #97's fishing_skiff swap is gone)");
            Assert.AreEqual(PropulsionType.Oars, boat.Hull.Propulsion,
                "boat.dory is oar-propelled, so the controls are per-oar strokes (BoatController.ApplyOarDrive)");
            Assert.IsFalse(BoatController.UsesEngineHelm(boat.Hull.Propulsion),
                "the controller must NOT take the outboard helm — no throttle/rudder on the dory");

            // The hold tracks the same rowed hull (the skin never re-points it either).
            var hold = _core.DoryGo.GetComponent<ShipHold>();
            var hso = new UnityEditor.SerializedObject(hold);
            Assert.AreEqual("boat.dory",
                ((BoatHullDef)hso.FindProperty("_hull").objectReferenceValue).Id,
                "the hold is on the same rowed dory hull");

            // The dory IS in OwnedFleet's id-keyed registry — this is the seam #97's skiff was deliberately
            // kept OUT of. Reverting to boat.dory must leave the buy-the-Punt grant + save-restore (both keyed
            // off ActiveHullId/BoatPurchased) resolving the boat's own hull, not a hull they've never heard of.
            var fleet = _core.DoryGo.GetComponent<OwnedFleet>();
            var fso = new UnityEditor.SerializedObject(fleet);
            var registry = fso.FindProperty("_registry");
            bool doryRegistered = false;
            for (int i = 0; i < registry.arraySize; i++)
            {
                var h = registry.GetArrayElementAtIndex(i).objectReferenceValue as BoatHullDef;
                if (h != null && h.Id == "boat.dory") doryRegistered = true;
                Assert.AreNotEqual("boat.fishing_skiff", h != null ? h.Id : null,
                    "the Engine fishing-skiff hull is not the player's boat and stays out of the fleet registry");
            }
            Assert.IsTrue(doryRegistered,
                "the active hull (boat.dory) is in the fleet registry, so OwnedFleet's id-keyed grant/restore " +
                "resolves it — the seam the engine skin used to sidestep");
        }

        // ---- the independent OAR OVERLAY (#204) — the builder wires the baked sheets --------------

        [Test]
        public void Build_IsoDory_LayersTheIndependentOarOverlays_AboveTheHull()
        {
            if (!IsoDoryArtImported() || !OarSheetsImported())
                Assert.Ignore("Iso dory and/or the 80-slice oar sheets not imported in this environment — the " +
                              "builder wires no oar overlay by design (no half-state); nothing to assert.");

            var layer = _core.DoryGo.GetComponent<DoryOarLayer>();
            Assert.IsNotNull(layer, "the playable dory carries DoryOarLayer (the baked oar overlay)");
            Assert.IsTrue(layer.IsWired, "…wired with both full 80-frame heading×column sheets + both renderers");

            // The overlays are CHILDREN of the hull's visual, so they inherit DirectionalBoatSprite's snap /
            // counter-rotation stomp — they can never smooth-rotate while the hull snaps.
            var visual = _core.DoryGo.transform.Find("FishingBoatVisual");
            Assert.IsNotNull(visual, "the hull's directional visual child exists");
            var port = visual.Find("OarPort");
            var star = visual.Find("OarStar");
            Assert.IsNotNull(port, "the port oar renders under the hull's visual child (same snap treatment)");
            Assert.IsNotNull(star, "…and so does the starboard oar");

            // Draw order (art README): hull → port oar → star oar, on the SAME sorting layer.
            var hullSr = visual.GetComponent<SpriteRenderer>();
            var portSr = port.GetComponent<SpriteRenderer>();
            var starSr = star.GetComponent<SpriteRenderer>();
            Assert.AreEqual(hullSr.sortingLayerID, portSr.sortingLayerID, "the oars share the hull's sorting layer");
            Assert.AreEqual(hullSr.sortingLayerID, starSr.sortingLayerID, "…both of them");
            Assert.Greater(portSr.sortingOrder, hullSr.sortingOrder, "the port oar draws ABOVE the hull");
            Assert.Greater(starSr.sortingOrder, portSr.sortingOrder, "…and the starboard oar above the port oar");

            // Shared cell + waterline pivot ⇒ the overlay registers on the hull at the hull's own position.
            Assert.AreEqual(Vector3.zero, port.localPosition, "the port overlay registers on the hull (pivot-shared)");
            Assert.AreEqual(Vector3.zero, star.localPosition, "…and so does the starboard overlay");
        }

        [Test]
        public void Build_IsoDory_RetiresTheLegacyOarRig_SoTheOarsAreNeverDoubleRendered()
        {
            if (!SkinSwapApplied())
                Assert.Ignore("No directional boat art imported — the skin (and the rig retirement) no-ops.");

            // The baked DoryOarLayer overlay now draws this hull's oars from the SAME LeftOar/RightOar state
            // the legacy BoatRowAnimator rig reads. Both up = two sets of oars on one boat.
            var oarRig = _core.DoryGo.transform.Find("OarRig");
            Assert.IsNotNull(oarRig, "the legacy rig object still exists (kept so the skin flag stays reversible)");
            Assert.IsFalse(oarRig.gameObject.activeSelf, "…but it's DEACTIVATED — the baked overlay owns the oars");

            var rowAnim = _core.DoryGo.GetComponent<BoatRowAnimator>();
            Assert.IsNotNull(rowAnim, "BoatRowAnimator stays on the boat (the class serves non-iso hulls)");
            var so = new UnityEditor.SerializedObject(rowAnim);
            Assert.IsNull(so.FindProperty("_oarRig").objectReferenceValue,
                "its _oarRig ref is severed, so it can't re-activate the rig each Update and double-render oars");
        }

        [Test]
        public void Build_DirectionalVisual_AddsConfiguredSnapDirectionalSprite_WithTheFullEightFacingCompass()
        {
            if (!SkinSwapApplied())
                Assert.Ignore("No directional boat art imported in this environment — the builder no-ops the " +
                              "skin by design (it warns + leaves the plain dory look); nothing to assert.");

            var directional = _core.DoryGo.GetComponent<DirectionalBoatSprite>();
            Assert.IsNotNull(directional, "the playable boat wears the DirectionalBoatSprite (the #93 component)");
            Assert.AreEqual(DirectionalBoatSprite.RotationMode.SnapDirectional, directional.Mode,
                "configured for 8-way SNAP (swap facings, keep the picture screen-aligned) — the owner's ask");

            var so = new UnityEditor.SerializedObject(directional);
            var facings = so.FindProperty("_facings");
            Assert.AreEqual(8, facings.arraySize, "all 8 N/NE/E/SE/S/SW/W/NW facings are assigned");
            for (int i = 0; i < facings.arraySize; i++)
                Assert.IsNotNull(facings.GetArrayElementAtIndex(i).objectReferenceValue, $"facing {i} is assigned");

            // The component draws onto a CHILD renderer (counter-rotated to stay screen-aligned), not the hull.
            var child = _core.DoryGo.transform.Find("FishingBoatVisual");
            Assert.IsNotNull(child, "a child 'FishingBoatVisual' renderer carries the facing");
            Assert.IsNotNull(child.GetComponent<SpriteRenderer>(), "the child has the SpriteRenderer to draw into");
        }

        [Test]
        public void Build_WaveMotion_IsWiredToTheDirectionalVisual_AndItsTiltHook()
        {
            if (!SkinSwapApplied())
                Assert.Ignore("No directional boat art imported — the skin (and the wave-motion rig on its " +
                              "child visual) no-ops by design; nothing to assert.");

            // B2 (ADR 0018): the boat ROCKS on the shared wave field — visual-only. The builder must wire
            // BoatWaveMotion to the FishingBoatVisual CHILD (never the physics root: no forces until B3) and
            // to the DirectionalBoatSprite tilt hook (that component stomps the child's rotation every
            // LateUpdate, so an unrouted roll would be silently overwritten — the spotlight lesson).
            var waveMotion = _core.DoryGo.GetComponent<BoatWaveMotion>();
            Assert.IsNotNull(waveMotion, "the playable boat carries BoatWaveMotion (ADR 0018 B2)");

            var so = new UnityEditor.SerializedObject(waveMotion);
            var visual = so.FindProperty("_visual").objectReferenceValue as Transform;
            Assert.IsNotNull(visual, "the wave motion drives a visual transform");
            Assert.AreEqual(_core.DoryGo.transform.Find("FishingBoatVisual"), visual,
                "…and it is the FishingBoatVisual CHILD, never the physics root (visual-only by phasing)");

            var hook = so.FindProperty("_directionalSprite").objectReferenceValue as DirectionalBoatSprite;
            Assert.AreEqual(_core.DoryGo.GetComponent<DirectionalBoatSprite>(), hook,
                "the roll routes through DirectionalBoatSprite.VisualTiltDegrees (it stomps rotation each frame)");

            Assert.Greater(so.FindProperty("_masterStrength").floatValue, 0f,
                "the rock ships ON (master strength > 0) so the owner can feel it without touching the Inspector");

            // Settings parity (ADR 0018 §(4)): the boat derives its trains from the same Default settings the
            // Art-side bridge publishes to the shader — same field, same waves. Spot-check the anchors.
            var settings = so.FindProperty("_settings");
            Assert.AreEqual(WaveFieldSettings.Default.PrimaryAmplitude,
                settings.FindPropertyRelative("PrimaryAmplitude").floatValue, 1e-6f,
                "wave-field settings start from WaveFieldSettings.Default (parity with the shader bridge)");
            Assert.AreEqual(WaveFieldSettings.Default.CrestSharpening,
                settings.FindPropertyRelative("CrestSharpening").floatValue, 1e-6f,
                "…crest sharpening too (B3/GameConfig unifies the two instances into one tunable source)");
        }

        [Test]
        public void Build_DirectionalVisual_HidesTheSingleRotatingHullPicture()
        {
            if (!SkinSwapApplied())
                Assert.Ignore("No directional boat art imported — the skin no-ops by design; nothing to assert.");

            // The single rotating hull picture is hidden (renderer disabled) so the owner doesn't see it under
            // the directional facing. The renderer + its sprite ref stay (OwnedFleet's id-keyed swap sets
            // .sprite, not .enabled), so ownership logic is unaffected — we only stopped DRAWING it.
            var hull = _core.DoryGo.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(hull, "the hull SpriteRenderer still exists (kept for OwnedFleet's sprite swap)");
            Assert.IsFalse(hull.enabled, "but it's DISABLED so it isn't drawn under the directional facing");
        }
    }
}
#endif
