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
    }
}
#endif
