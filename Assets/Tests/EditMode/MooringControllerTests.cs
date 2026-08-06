using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The rope controller's loop (M2-38), driven through its input-free <see cref="MooringController.Tick"/>
    /// with synthesized gesture samples — the same discipline the fishing tests use, so none of this needs a
    /// keyboard, a camera or a scene.
    ///
    /// <list type="bullet">
    ///   <item><b>ONE VERB, ONE FUNCTION</b> — the toss resolves through <c>FlickCastMath</c>, so a gesture
    ///   that is not a cast is not a throw either, and the live preview is the rod's own preview.</item>
    ///   <item><b>THE CATCH</b> — a loop that lands near an opposing cleat makes fast; one that lands in the
    ///   water is the cozy nothing.</item>
    ///   <item><b>THE KEY ARBITRATION</b> — standing at a cleat claims the cast flick from the rod, by
    ///   proximity, so the outcome never depends on component execution order.</item>
    /// </list>
    /// </summary>
    public class MooringControllerTests
    {
        private sealed class FakeCleat : IMooringCleat
        {
            public string Id { get; set; } = "cleat.test";
            public CleatSide Side { get; set; }
            public Vector2 WorldPosition { get; set; }
            public float ElevationMeters { get; set; }
        }

        private GameObject _boatGo, _playerGo;
        private BoatMooring _mooring;
        private MooringController _rope;

        [SetUp]
        public void SetUp()
        {
            MooringCleats.Clear();
            CastActionClaim.Reset();

            _boatGo = new GameObject("Boat");
            _boatGo.AddComponent<Rigidbody2D>();
            _mooring = _boatGo.AddComponent<BoatMooring>();

            _playerGo = new GameObject("Player");
            _playerGo.transform.position = Vector3.zero;
            _rope = _playerGo.AddComponent<MooringController>();
            _rope.Configure(_mooring, _playerGo.transform);

            // On foot at the wharf: the player works SHORE cleats and throws to the BOAT.
            _rope.OnControlModeChanged(new ControlModeChanged(ControlMode.OnFoot));
        }

        [TearDown]
        public void TearDown()
        {
            MooringCleats.Clear();
            CastActionClaim.Reset();
            if (_boatGo != null) Object.DestroyImmediate(_boatGo);
            if (_playerGo != null) Object.DestroyImmediate(_playerGo);
        }

        /// <summary>A clean flick: wound back behind the player, swept forward, released. Modelled on the
        /// gesture <c>FlickCastMathTests</c> pins, so if the shared maths ever changes its mind about what
        /// counts as a cast, this moves with it rather than drifting.</summary>
        private void ThrowFlick(float windBackMetres = 4f)
        {
            // Wind back (the pointer drawn BEHIND the player, −x), then sweep forward past them (+x).
            _rope.Tick(0.05f, true, new Vector2(-0.2f, 0f), true, false, false);
            _rope.Tick(0.05f, true, new Vector2(-windBackMetres * 0.5f, 0f), true, false, false);
            _rope.Tick(0.05f, true, new Vector2(-windBackMetres, 0f), true, false, false);   // apex
            _rope.Tick(0.05f, true, new Vector2(-1f, 0f), true, false, false);               // sweep…
            _rope.Tick(0.05f, true, new Vector2(1f, 0f), true, false, false);
            _rope.Tick(0.05f, true, new Vector2(2.5f, 0f), true, false, false);              // release point
            _rope.Tick(0.05f, false, new Vector2(2.5f, 0f), true, false, false);             // let go
        }

        private FakeCleat AddCleat(CleatSide side, Vector2 at, float elevation, string id)
        {
            var c = new FakeCleat { Id = id, Side = side, WorldPosition = at, ElevationMeters = elevation };
            MooringCleats.Register(c);
            return c;
        }

        // ---- the key arbitration ---------------------------------------------------------------

        [Test]
        public void StandingAtACleat_ClaimsTheCastFlickFromTheRod_AndSteppingOffReturnsIt()
        {
            AddCleat(CleatSide.Shore, new Vector2(0.5f, 0f), 5.35f, "wharf.bollard_0");

            _rope.Tick(0.02f, false, Vector2.zero, true, false, false);
            Assert.IsTrue(CastActionClaim.IsClaimed, "at a cleat, your hands are on the rope");

            _playerGo.transform.position = new Vector3(20f, 0f, 0f);      // walk away
            _rope.Tick(0.02f, false, Vector2.zero, true, false, false);
            Assert.IsFalse(CastActionClaim.IsClaimed, "a metre off the fitting and the rod is yours again");
        }

        [Test]
        public void WithNoCleatInReach_TheRopeNeverClaimsTheFlick()
        {
            AddCleat(CleatSide.Shore, new Vector2(30f, 0f), 5.35f, "wharf.far");
            _rope.Tick(0.02f, false, Vector2.zero, true, false, false);
            Assert.IsFalse(CastActionClaim.IsClaimed);
        }

        // ---- the throw -------------------------------------------------------------------------

        [Test]
        public void AThrowThatLandsOnAnOpposingCleat_MakesFast()
        {
            AddCleat(CleatSide.Shore, new Vector2(0.5f, 0f), 5.35f, "wharf.bollard_0");
            // The boat lies off the wharf. A full wind-back aims the cast cap (12 m) down the flick, and
            // the sweep here is due +x, so put her cleat out that way.
            AddCleat(CleatSide.Boat, new Vector2(12f, 0f), 0.6f, "boat.dory.bow_1");

            ThrowFlick();

            Assert.AreEqual(MooringPhase.MadeFast, _rope.Phase, "the loop caught");
            Assert.IsTrue(_mooring.IsMadeFast);
            Assert.AreEqual("boat.dory.bow_1", _mooring.BoatCleat.Id, "the hull's end");
            Assert.AreEqual("wharf.bollard_0", _mooring.ShoreCleat.Id, "the shore end");
            Assert.AreEqual(MooringLineSettings.Default.DefaultScopeMetres, _mooring.ScopeMetres, 1e-4f);
        }

        [Test]
        public void AThrowThatLandsInTheWater_IsTheCozyNothing()
        {
            AddCleat(CleatSide.Shore, new Vector2(0.5f, 0f), 5.35f, "wharf.bollard_0");
            AddCleat(CleatSide.Boat, new Vector2(12f, 40f), 0.6f, "boat.dory.bow_1");   // nowhere near the arc

            ThrowFlick();

            Assert.AreEqual(MooringPhase.Idle, _rope.Phase, "coil it and try again");
            Assert.IsFalse(_mooring.IsMadeFast);
            Assert.AreEqual(MooringState.Stowed, _mooring.State, "…and nothing was tied");
        }

        [Test]
        public void AGestureThatIsNotACast_IsNotAThrowEither()
        {
            AddCleat(CleatSide.Shore, new Vector2(0.5f, 0f), 5.35f, "wharf.bollard_0");
            AddCleat(CleatSide.Boat, new Vector2(12f, 0f), 0.6f, "boat.dory.bow_1");

            // A twitch: pressed and released with no wind-back at all. The SHARED maths rejects it, so the
            // rope must reject it too — that is the point of there being only one function.
            _rope.Tick(0.02f, true, new Vector2(0.05f, 0f), true, false, false);
            _rope.Tick(0.02f, false, new Vector2(0.06f, 0f), true, false, false);

            Assert.AreEqual(MooringPhase.Idle, _rope.Phase);
            Assert.IsFalse(_mooring.IsMadeFast, "a twitch is not a flick, and a flick is what throws a line");
        }

        [Test]
        public void AThrowCannotStart_AwayFromACleat()
        {
            AddCleat(CleatSide.Boat, new Vector2(12f, 0f), 0.6f, "boat.dory.bow_1");   // nothing to throw FROM

            ThrowFlick();

            Assert.AreEqual(MooringPhase.Idle, _rope.Phase, "you pick the coil up AT a cleat");
            Assert.IsFalse(_mooring.IsMadeFast);
        }

        // ---- working the line ------------------------------------------------------------------

        [Test]
        public void AtTheCleat_TappingTheWorkKey_TightensAndSlackens()
        {
            AddCleat(CleatSide.Shore, new Vector2(0.5f, 0f), 5.35f, "wharf.bollard_0");
            AddCleat(CleatSide.Boat, new Vector2(12f, 0f), 0.6f, "boat.dory.bow_1");
            ThrowFlick();
            Assert.AreEqual(MooringPhase.MadeFast, _rope.Phase);

            MooringLineSettings s = MooringLineSettings.Default;
            float start = _mooring.ScopeMetres;

            // A TAP: down for one short tick, then up.
            _rope.Tick(0.02f, false, Vector2.zero, true, workHeld: true, slacken: false);
            _rope.Tick(0.02f, false, Vector2.zero, true, workHeld: false, slacken: false);
            Assert.AreEqual(start - s.ScopeStepMetres, _mooring.ScopeMetres, 1e-4f, "tap = tighten a step");

            _rope.Tick(0.02f, false, Vector2.zero, true, workHeld: true, slacken: true);
            _rope.Tick(0.02f, false, Vector2.zero, true, workHeld: false, slacken: true);
            Assert.AreEqual(start, _mooring.ScopeMetres, 1e-4f, "…with the modifier, slacken a step back");
        }

        [Test]
        public void AtTheCleat_HoldingTheWorkKey_CastsOff()
        {
            AddCleat(CleatSide.Shore, new Vector2(0.5f, 0f), 5.35f, "wharf.bollard_0");
            AddCleat(CleatSide.Boat, new Vector2(12f, 0f), 0.6f, "boat.dory.bow_1");
            ThrowFlick();
            Assert.AreEqual(MooringPhase.MadeFast, _rope.Phase);

            // Held well past the cast-off threshold — letting a boat go is never a mistyped tap.
            for (int i = 0; i < 60; i++)
                _rope.Tick(0.02f, false, Vector2.zero, true, workHeld: true, slacken: false);

            Assert.AreEqual(MooringPhase.Idle, _rope.Phase);
            Assert.AreEqual(MooringState.Stowed, _mooring.State, "she is free");
        }

        [Test]
        public void AwayFromTheCleat_TheLineCannotBeWorked()
        {
            AddCleat(CleatSide.Shore, new Vector2(0.5f, 0f), 5.35f, "wharf.bollard_0");
            AddCleat(CleatSide.Boat, new Vector2(12f, 0f), 0.6f, "boat.dory.bow_1");
            ThrowFlick();
            float scope = _mooring.ScopeMetres;

            _playerGo.transform.position = new Vector3(25f, 0f, 0f);
            for (int i = 0; i < 60; i++)
                _rope.Tick(0.02f, false, Vector2.zero, true, workHeld: true, slacken: false);

            Assert.AreEqual(MooringState.MadeFastToCleat, _mooring.State,
                            "you can watch a moored boat from anywhere, but you work her at the fitting");
            Assert.AreEqual(scope, _mooring.ScopeMetres, 1e-4f);
        }

        [Test]
        public void ASlippedLoop_PutsTheRopeBackInTheCoil_WithoutThePlayerDoingAnything()
        {
            AddCleat(CleatSide.Shore, new Vector2(0.5f, 0f), 5.35f, "wharf.bollard_0");
            AddCleat(CleatSide.Boat, new Vector2(12f, 0f), 0.6f, "boat.dory.bow_1");
            ThrowFlick();
            Assert.AreEqual(MooringPhase.MadeFast, _rope.Phase);

            // The tide out-ran the scope while the player was elsewhere (the physics half does this; here
            // it is simulated by the boat's own cast-off) — the controller must notice, not stay stuck.
            _mooring.CastOff();
            _rope.Tick(0.02f, false, Vector2.zero, true, false, false);

            Assert.AreEqual(MooringPhase.Idle, _rope.Phase,
                            "the line can end without the player; the rope goes back in the coil");
        }
    }
}
