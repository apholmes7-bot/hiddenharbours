using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The cleat registry and the make-fast contract (M2-38): what a rope may be tied to, how the toss
    /// finds it, and the rules about which end is which. Pure — the lookups take their list explicitly, so
    /// none of this needs a scene.
    /// </summary>
    public class MooringCleatsTests
    {
        /// <summary>A plain test cleat. Position and elevation are settable so a test can run the tide.</summary>
        private sealed class FakeCleat : IMooringCleat
        {
            public string Id { get; set; } = "cleat.test";
            public CleatSide Side { get; set; } = CleatSide.Shore;
            public Vector2 WorldPosition { get; set; }
            public float ElevationMeters { get; set; }
        }

        private static FakeCleat Shore(string id, float x, float y, float elevation = 5.35f)
            => new FakeCleat { Id = id, Side = CleatSide.Shore, WorldPosition = new Vector2(x, y), ElevationMeters = elevation };

        private static FakeCleat Boat(string id, float x, float y, float elevation = 0.6f)
            => new FakeCleat { Id = id, Side = CleatSide.Boat, WorldPosition = new Vector2(x, y), ElevationMeters = elevation };

        [SetUp]
        public void SetUp() => MooringCleats.Clear();

        [TearDown]
        public void TearDown() => MooringCleats.Clear();

        // ---- the registry -------------------------------------------------------------------

        [Test]
        public void Register_IsIdempotent_SoADoubleEnableCannotDoubleTheFitting()
        {
            var c = Shore("a", 0f, 0f);
            MooringCleats.Register(c);
            MooringCleats.Register(c);
            Assert.AreEqual(1, MooringCleats.Count,
                            "a component enabled twice must not appear twice — a toss could otherwise " +
                            "resolve onto the same fitting from two entries");
        }

        [Test]
        public void RegisterAndUnregister_TolerateNullAndUnknown()
        {
            MooringCleats.Register(null);
            MooringCleats.Unregister(null);
            MooringCleats.Unregister(Shore("never-registered", 0f, 0f));
            Assert.AreEqual(0, MooringCleats.Count, "a teardown must never have to check first");
        }

        [Test]
        public void AnEmptyRegistry_IsNothingToTieTo_NotAFault()
        {
            Assert.IsFalse(MooringCleats.TryFindNearestNow(Vector2.zero, CleatSide.Shore, 100f, out var found));
            Assert.IsNull(found);
        }

        // ---- the lookup ---------------------------------------------------------------------

        [Test]
        public void TryFindNearest_TakesTheNearestOnTheRequestedSide_WithinTheRadius()
        {
            var near = Shore("near", 1f, 0f);
            var far = Shore("far", 5f, 0f);
            var wrongSide = Boat("boat", 0.2f, 0f);
            var list = new IMooringCleat[] { far, wrongSide, near };

            Assert.IsTrue(MooringCleats.TryFindNearest(list, Vector2.zero, CleatSide.Shore, 10f, out var got));
            Assert.AreSame(near, got, "nearest wins, and the boat cleat sitting closer is not a shore cleat");
        }

        [Test]
        public void TryFindNearest_RespectsTheRadius_SoAMissedTossFindsNothing()
        {
            var list = new IMooringCleat[] { Shore("a", 4f, 0f) };
            Assert.IsFalse(MooringCleats.TryFindNearest(list, Vector2.zero, CleatSide.Shore, 1.5f, out _),
                           "the loop fell in the water — coil and try again");
            Assert.IsTrue(MooringCleats.TryFindNearest(list, Vector2.zero, CleatSide.Shore, 4.5f, out _));
        }

        [Test]
        public void TryFindNearest_MeasuresHorizontally_SoATallWharfIsStillReachable()
        {
            // The wharf cleat is 7 m above the boat's, but only 1 m away across the water. A throw is
            // aimed across the water; the vertical gap is the TIDE's business, not the throw's.
            var list = new IMooringCleat[] { Shore("bollard", 1f, 0f, elevation: 5.35f) };
            Assert.IsTrue(MooringCleats.TryFindNearest(list, Vector2.zero, CleatSide.Shore, 1.5f, out _),
                          "height must not make a nearby bollard unthrowable-to");
        }

        [Test]
        public void FindById_MatchesExactly_ForRestoringALineAfterALoad()
        {
            var a = Shore("wharf.st_peters.bollard_0", 0f, 0f);
            var list = new IMooringCleat[] { a, Shore("wharf.st_peters.bollard_1", 9f, 0f) };
            Assert.AreSame(a, MooringCleats.FindById(list, "wharf.st_peters.bollard_0"));
            Assert.IsNull(MooringCleats.FindById(list, "wharf.st_peters.bollard_9"));
            Assert.IsNull(MooringCleats.FindById(list, null));
        }

        // ---- what may be made fast to what --------------------------------------------------

        private BoatMooring NewMooring(out GameObject go)
        {
            go = new GameObject("Boat");
            go.AddComponent<Rigidbody2D>();
            return go.AddComponent<BoatMooring>();
        }

        [Test]
        public void MakeFast_RequiresOneEndOnEachSide()
        {
            var m = NewMooring(out GameObject go);
            try
            {
                Assert.IsFalse(m.MakeFast(Boat("b", 0f, 0f), Boat("b2", 1f, 0f), 9f),
                               "boat-to-boat is rafting, which is explicitly out of scope");
                Assert.IsFalse(m.MakeFast(Shore("s", 0f, 0f), Shore("s2", 1f, 0f), 9f),
                               "shore-to-shore is not a mooring");
                Assert.IsFalse(m.MakeFast(null, Shore("s", 0f, 0f), 9f), "a line needs two ends");
                Assert.IsFalse(m.MakeFast(Boat("b", 0f, 0f), null, 9f));
                Assert.AreEqual(MooringState.Stowed, m.State, "none of those tied anything");

                Assert.IsTrue(m.MakeFast(Boat("b", 0f, 0f), Shore("s", 1f, 0f), 9f));
                Assert.AreEqual(MooringState.MadeFastToCleat, m.State);
                Assert.IsTrue(m.IsMadeFast);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void MakeFast_ClampsScopeToTheConfiguredRange()
        {
            var m = NewMooring(out GameObject go);
            try
            {
                MooringLineSettings s = MooringLineSettings.Default;
                m.MakeFast(Boat("b", 0f, 0f), Shore("s", 1f, 0f), 9999f);
                Assert.AreEqual(s.MaxScopeMetres, m.ScopeMetres, 1e-4f, "no infinite line");
                m.SetScope(-5f);
                Assert.AreEqual(s.MinScopeMetres, m.ScopeMetres, 1e-4f, "no negative line");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void AdjustScope_TightensAndSlackensInSteps_AndStopsAtTheEnds()
        {
            var m = NewMooring(out GameObject go);
            try
            {
                MooringLineSettings s = MooringLineSettings.Default;
                m.MakeFast(Boat("b", 0f, 0f), Shore("s", 1f, 0f), s.DefaultScopeMetres);

                Assert.AreEqual(s.DefaultScopeMetres - s.ScopeStepMetres, m.AdjustScope(-1), 1e-4f, "tighten");
                Assert.AreEqual(s.DefaultScopeMetres, m.AdjustScope(+1), 1e-4f, "slacken back");

                m.SetScope(s.MaxScopeMetres);
                Assert.AreEqual(s.MaxScopeMetres, m.AdjustScope(+1), 1e-4f, "already at the top");
                m.SetScope(s.MinScopeMetres);
                Assert.AreEqual(s.MinScopeMetres, m.AdjustScope(-1), 1e-4f, "already at the bottom");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void CastOff_FreesHer_AndIsANoOpWhenNothingIsTied()
        {
            var m = NewMooring(out GameObject go);
            try
            {
                Assert.IsFalse(m.CastOff(), "a stray key press must not 'untie' a boat that was never tied");

                m.MakeFast(Boat("b", 0f, 0f), Shore("s", 1f, 0f), 9f);
                Assert.IsTrue(m.CastOff());
                Assert.AreEqual(MooringState.Stowed, m.State);
                Assert.IsNull(m.BoatCleat, "the line's ends are let go with it");
                Assert.IsNull(m.ShoreCleat);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void BoardingStowsThePainter_ButNeverUntiesAMadeFastLine()
        {
            var m = NewMooring(out GameObject go);
            try
            {
                m.MakeFast(Boat("b", 0f, 0f), Shore("s", 1f, 0f), 9f);

                m.Stow();   // what the control switcher calls on boarding
                Assert.AreEqual(MooringState.MadeFastToCleat, m.State,
                                "stepping aboard a properly moored boat must not silently untie her");

                var player = new GameObject("Player");
                try
                {
                    m.Hold(player.transform);
                    Assert.AreEqual(MooringState.MadeFastToCleat, m.State,
                                    "stepping ashore does not put the painter in your hand — she is tied");
                    m.Root(Vector2.zero);
                    Assert.AreEqual(MooringState.MadeFastToCleat, m.State, "…nor rooted to the ground");

                    // And the hold/root PROMPT stands down: the root key has nothing to offer a boat
                    // that is properly moored, so it must not be advertised (it would be a no-op press).
                    Assert.IsFalse(m.IsMoored, "the painter is not out — she is made fast");
                    Assert.IsTrue(m.HasLineOut, "…but there is very much a rope to draw");
                }
                finally { Object.DestroyImmediate(player); }
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ---- the tide law, read off a live line ----------------------------------------------

        [Test]
        public void TheLinesReach_ShrinksAsTheTideOpensTheGap()
        {
            var m = NewMooring(out GameObject go);
            try
            {
                var boat = Boat("b", 0f, 0f, elevation: 2.8f);      // high water: cleat at +2.8 m
                var shore = Shore("s", 1f, 0f, elevation: 5.35f);   // the pier deck, fixed
                m.MakeFast(boat, shore, 9f);

                float atHigh = m.HorizontalReachMetres;
                Assert.AreEqual(2.55f, m.VerticalDropMetres, 1e-3f);

                boat.ElevationMeters = -1.6f;                        // …and the tide goes out
                float atLow = m.HorizontalReachMetres;
                Assert.AreEqual(6.95f, m.VerticalDropMetres, 1e-3f);

                Assert.Less(atLow, atHigh,
                            "the falling tide spends the line vertically, leaving less to reach across");
                Assert.Greater(atLow, 0f, "the default scope still has room at dead low — she is not hung");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
