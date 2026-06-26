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

        // ---- the directional fishing-boat SKIN swap (reversible "for now" placeholder; #93/#94/#97) -----
        // The owner wants the playable boat to WEAR the 4-way directional fishing-boat facings AND drive as an
        // ENGINE boat ("a power boat skin, not a rowboat") instead of the dory hull + oars. The builder gates
        // this on the UseDirectionalFishingBoatVisual flag (currently ON). These guard the swap's observable
        // effects — the boat's hull is swapped to the Engine boat.fishing_skiff (propulsion match), the
        // directional component is added + configured, the hull picture is hidden, and the oar rig is hidden
        // (with its BoatRowAnimator ref severed so it can't re-show the rig). The swap loads the real committed
        // FishingBoat_*.png + FishingSkiff.asset via AssetDatabase; if EITHER isn't imported in this environment
        // the builder no-ops the swap (by design — no half-state), so the tests skip rather than false-fail.

        // The facings the builder loads (CW from North) — mirrors PersistentCoreBuilder.FishingBoatFacingPaths.
        const string FishingBoatNorthPath = "Assets/_Project/Art/Boats/FishingBoat_N.png";
        // The Engine hull the skin drives on — mirrors PersistentCoreBuilder.DataFishingSkiff.
        const string FishingSkiffPath     = "Assets/_Project/Data/Boats/FishingSkiff.asset";

        private static bool FishingBoatArtImported()
        {
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(FishingBoatNorthPath) != null) return true;
            // Match the builder's Single-OR-Multiple-import-tolerant lookup.
            return UnityEditor.AssetDatabase.LoadAllAssetsAtPath(FishingBoatNorthPath).OfType<Sprite>().Any();
        }

        private static bool FishingSkiffDefImported()
            => UnityEditor.AssetDatabase.LoadAssetAtPath<BoatHullDef>(FishingSkiffPath) != null;

        // The swap (visual + propulsion) only runs when BOTH the facing art AND the Engine Def are present —
        // the builder no-ops to the rowed dory otherwise (no half-state). Tests that assert the swap skip
        // unless both are imported in this environment.
        private static bool SkinSwapApplied() => FishingBoatArtImported() && FishingSkiffDefImported();

        // ---- the propulsion match: the skin drives on the Engine fishing-skiff hull (#97) ---------

        [Test]
        public void Build_DirectionalSkin_DrivesOnTheEngineFishingSkiffHull()
        {
            if (!SkinSwapApplied())
                Assert.Ignore("FishingBoat facings and/or FishingSkiff.asset not imported in this environment — " +
                              "the builder no-ops the skin swap by design (leaves the rowed dory); nothing to assert.");

            var boat = _core.DoryGo.GetComponent<BoatController>();
            Assert.IsNotNull(boat.Hull, "the swapped boat has a hull");
            Assert.AreEqual("boat.fishing_skiff", boat.Hull.Id,
                "the directional skin must drive on the dedicated Engine hull (a power boat, not the rowed dory)");
            Assert.AreEqual(PropulsionType.Engine, boat.Hull.Propulsion,
                "the skin's hull is Engine-propelled so the controls match the powerboat picture");
            Assert.IsTrue(BoatController.UsesEngineHelm(boat.Hull.Propulsion),
                "the controller takes the outboard helm (throttle + speed-scaled rudder), not the oars");

            // The hold tracks the same Engine hull so capacity/feel stay consistent with the boat under helm.
            var hold = _core.DoryGo.GetComponent<ShipHold>();
            var hso = new UnityEditor.SerializedObject(hold);
            Assert.AreEqual("boat.fishing_skiff",
                ((BoatHullDef)hso.FindProperty("_hull").objectReferenceValue).Id,
                "the hold is on the same Engine skiff hull");

            // The Engine skiff is deliberately NOT in OwnedFleet's id-keyed registry ({Dory, Punt}), so a
            // buy/grant/load never reverts the skin's hull — it STICKS.
            var fleet = _core.DoryGo.GetComponent<OwnedFleet>();
            var fso = new UnityEditor.SerializedObject(fleet);
            var registry = fso.FindProperty("_registry");
            for (int i = 0; i < registry.arraySize; i++)
            {
                var h = registry.GetArrayElementAtIndex(i).objectReferenceValue as BoatHullDef;
                Assert.AreNotEqual("boat.fishing_skiff", h != null ? h.Id : null,
                    "the fishing-skiff hull must NOT be in the fleet registry (so OwnedFleet never reverts it)");
            }
        }

        [Test]
        public void Build_DirectionalVisual_AddsConfiguredSnapDirectionalSprite_WithFourFacings()
        {
            if (!SkinSwapApplied())
                Assert.Ignore("FishingBoat facings and/or FishingSkiff.asset not imported in this environment — " +
                              "the builder no-ops the swap by design (it warns + leaves the dory look); nothing to assert.");

            var directional = _core.DoryGo.GetComponent<DirectionalBoatSprite>();
            Assert.IsNotNull(directional, "the playable boat wears the DirectionalBoatSprite (the #93 component)");
            Assert.AreEqual(DirectionalBoatSprite.RotationMode.SnapDirectional, directional.Mode,
                "configured for 4-way SNAP (swap facings, keep the picture screen-aligned) — the owner's ask");

            var so = new UnityEditor.SerializedObject(directional);
            var facings = so.FindProperty("_facings");
            Assert.AreEqual(4, facings.arraySize, "all 4 N/E/S/W facings are assigned");
            for (int i = 0; i < facings.arraySize; i++)
                Assert.IsNotNull(facings.GetArrayElementAtIndex(i).objectReferenceValue, $"facing {i} is assigned");

            // The component draws onto a CHILD renderer (counter-rotated to stay screen-aligned), not the hull.
            var child = _core.DoryGo.transform.Find("FishingBoatVisual");
            Assert.IsNotNull(child, "a child 'FishingBoatVisual' renderer carries the facing");
            Assert.IsNotNull(child.GetComponent<SpriteRenderer>(), "the child has the SpriteRenderer to draw into");
        }

        [Test]
        public void Build_DirectionalVisual_HidesTheDoryHullAndOarRig()
        {
            if (!SkinSwapApplied())
                Assert.Ignore("FishingBoat facings and/or FishingSkiff.asset not imported — the swap no-ops by " +
                              "design; nothing to assert.");

            // The hull picture is hidden (renderer disabled) so the owner doesn't see the dory hull under the
            // fishing boat. The renderer + its sprite ref stay (OwnedFleet's id-keyed swap sets .sprite, not
            // .enabled), so ownership logic is unaffected — we only stopped DRAWING it.
            var hull = _core.DoryGo.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(hull, "the hull SpriteRenderer still exists (kept for OwnedFleet's sprite swap)");
            Assert.IsFalse(hull.enabled, "but it's DISABLED so the dory hull isn't drawn under the fishing boat");

            // The oar rig (rower + both oars) is hidden, and the BoatRowAnimator's _oarRig ref is SEVERED so
            // the animator can't re-activate the rig each frame (it re-enables it while the home hull is active).
            var oarRig = _core.DoryGo.transform.Find("OarRig");
            Assert.IsNotNull(oarRig, "the oar rig object still exists (kept so the swap is reversible)");
            Assert.IsFalse(oarRig.gameObject.activeSelf, "but it's DEACTIVATED — no oars on the motorboat");

            var rowAnim = _core.DoryGo.GetComponent<BoatRowAnimator>();
            var so = new UnityEditor.SerializedObject(rowAnim);
            Assert.IsNull(so.FindProperty("_oarRig").objectReferenceValue,
                "the animator's _oarRig ref is severed so it can't re-show the rig each Update");
        }
    }
}
#endif
