using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The per-hull polygon clamp (M2-37, data half).</b> Every hull used to walk the SAME
    /// 1.4 × 3.2 m rectangle tuned to the dory; now each clamps to her own imported areas. These pin
    /// the clamp's contract on defs built through the very same <see cref="DeckArea.From"/> bake the
    /// sidecar importer uses — a dory-shaped centreline strip, a lobster-boat-shaped cockpit + raised
    /// foredeck, and a hull with washboards.
    ///
    /// <para>Also pinned: the search HINT (rule 7 — the per-tick path must not walk every area), and
    /// that washboards stay OUT of the free-walk area until the Space climb exists.</para>
    /// </summary>
    public class DeckPolygonWalkTests
    {
        const float Iso = 40f;
        const float Tol = 1e-3f;

        // The dory's real bilge floor, rounded: a 0.45 m centreline strip, 3.6 m long. The rectangle she
        // used to walk was 1.4 m wide — three times her actual sole.
        static BoatDeckDef DoryLike()
        {
            var def = ScriptableObject.CreateInstance<BoatDeckDef>();
            def.Id = "deck.test_dory";
            def.Areas = new[]
            {
                DeckArea.From("floor", DeckAreaKind.Deck, new[]
                {
                    new Vector3(-0.225f, 1.8f, 0.2f), new Vector3(-0.225f, -1.8f, 0.06f),
                    new Vector3(0.225f, -1.8f, 0.06f), new Vector3(0.225f, 1.8f, 0.2f),
                }),
            };
            def.WalkCenter = Vector2.zero;
            def.WalkHalfExtents = new Vector2(0.225f, 1.8f);
            return def;
        }

        // Two disjoint areas at different heights, the lobster boat's shape: a low cockpit aft, a raised
        // foredeck forward, and the wheelhouse gap between them that only the washboards cross.
        static BoatDeckDef TwoLevelHull()
        {
            var def = ScriptableObject.CreateInstance<BoatDeckDef>();
            def.Id = "deck.test_two_level";
            def.Areas = new[]
            {
                DeckArea.From("cockpit", DeckAreaKind.Deck, new[]
                {
                    new Vector3(-1.7f, -6f, 0.5f), new Vector3(1.7f, -6f, 0.5f),
                    new Vector3(1.7f, 0.55f, 0.5f), new Vector3(-1.7f, 0.55f, 0.5f),
                }),
                DeckArea.From("foredeck", DeckAreaKind.Deck, new[]
                {
                    new Vector3(-1.2f, 3.85f, 2.1f), new Vector3(1.2f, 3.85f, 2.1f),
                    new Vector3(1.2f, 6.3f, 2.7f), new Vector3(-1.2f, 6.3f, 2.7f),
                }),
                DeckArea.From("washboard_port", DeckAreaKind.Washboard, new[]
                {
                    new Vector3(-2.1f, -6f, 1.2f), new Vector3(-1.7f, -6f, 1.2f),
                    new Vector3(-1.7f, 3.6f, 2.1f), new Vector3(-2.1f, 3.6f, 2.1f),
                }),
            };
            def.WalkCenter = new Vector2(0f, 0.15f);
            def.WalkHalfExtents = new Vector2(1.7f, 6.15f);
            return def;
        }

        // ---- the clamp ------------------------------------------------------------------------------

        [Test]
        public void APointAlreadyOnTheDeckIsLeftExactlyWhereItIs()
        {
            BoatDeckDef def = DoryLike();
            int hint = -1;
            Vector2 p = new Vector2(0.1f, -0.5f);
            Vector2 clamped = def.ClampToWalkable(p, ref hint, out float h);
            Assert.AreEqual(p.x, clamped.x, Tol);
            Assert.AreEqual(p.y, clamped.y, Tol);
            Assert.AreEqual(0, hint, "the containing area becomes the next tick's hint");
            Assert.Greater(h, 0f, "the floor stands above the keel");
        }

        [Test]
        public void SteppingOffTheBeamIsHeldOnTheStripNotOnTheOldRectangle()
        {
            // 1.0 m abeam is comfortably inside the greybox rectangle's 0.7 m half-beam… once. On the
            // dory's real sole it is 0.775 m over the side.
            BoatDeckDef def = DoryLike();
            int hint = -1;
            Vector2 clamped = def.ClampToWalkable(new Vector2(1.0f, 0f), ref hint, out _);
            Assert.AreEqual(0.225f, clamped.x, Tol, "held at the sole's own edge");
            Assert.AreEqual(0f, clamped.y, Tol, "and not shoved along the keel");
        }

        [Test]
        public void SteppingPastTheBowIsHeldAtTheForwardEdge()
        {
            BoatDeckDef def = DoryLike();
            int hint = -1;
            Vector2 clamped = def.ClampToWalkable(new Vector2(0f, 9f), ref hint, out _);
            Assert.AreEqual(1.8f, clamped.y, Tol);
            Assert.AreEqual(0f, clamped.x, Tol);
        }

        [Test]
        public void TheHintIsHonouredButNeverTraps()
        {
            // Standing on the foredeck, then teleported into the cockpit: the stale hint must not win.
            BoatDeckDef def = TwoLevelHull();
            int hint = 1;                                              // "I was on the foredeck"
            Vector2 clamped = def.ClampToWalkable(new Vector2(0f, -3f), ref hint, out float h);
            Assert.AreEqual(-3f, clamped.y, Tol);
            Assert.AreEqual(0, hint, "the hint follows the player into the cockpit");
            Assert.AreEqual(0.5f, h, Tol, "and reports the cockpit's height, not the foredeck's");
        }

        [Test]
        public void HeightComesFromTheAreaYouAreStandingOn()
        {
            BoatDeckDef def = TwoLevelHull();
            int hint = -1;
            def.ClampToWalkable(new Vector2(0f, -2f), ref hint, out float sole);
            def.ClampToWalkable(new Vector2(0f, 5f), ref hint, out float fore);
            Assert.AreEqual(0.5f, sole, Tol);
            Assert.Greater(fore, 2f, "the raised foredeck stands well above the sole");
        }

        [Test]
        public void TheGapBetweenTWOAreasHoldsYouOnTheNEARERONE()
        {
            // The wheelhouse gap: pressing forward out of the cockpit must keep you in the cockpit, not
            // teleport you to the foredeck on the far side of the house.
            BoatDeckDef def = TwoLevelHull();
            int hint = 0;
            Vector2 clamped = def.ClampToWalkable(new Vector2(0f, 1.2f), ref hint, out _);
            Assert.AreEqual(0.55f, clamped.y, Tol, "held at the cockpit's forward edge");
            Assert.AreEqual(0, hint);
        }

        // ---- washboards -----------------------------------------------------------------------------

        [Test]
        public void WashboardsAreNotWalkableUntilTheClimbExists()
        {
            BoatDeckDef def = TwoLevelHull();
            Assert.IsTrue(def.HasWashboards(), "the data is loaded…");

            int hint = -1;
            Vector2 onTheWashboard = new Vector2(-1.9f, 0f);
            Vector2 clamped = def.ClampToWalkable(onTheWashboard, ref hint, out _);
            Assert.AreEqual(-1.7f, clamped.x, Tol, "…but the free walk stops at the cockpit's rail");

            hint = -1;
            Vector2 allowed = def.ClampToWalkable(onTheWashboard, ref hint, out _, includeWashboards: true);
            Assert.AreEqual(onTheWashboard.x, allowed.x, Tol, "the climb can opt in with no re-derivation");
        }

        [Test]
        public void AnOpenBoatSaysSoRatherThanFailing()
        {
            BoatDeckDef def = DoryLike();
            Assert.IsFalse(def.HasWashboards(), "no WASHBOARD section = an open boat, not an error");
            Assert.IsTrue(def.HasWalkableDeck());
        }

        // ---- the step -------------------------------------------------------------------------------

        [Test]
        public void AStepIsMetresOfDECKPerSecondAtEveryHeading()
        {
            // The point of stepping in the hull frame: the walk covers the same amount of DECK whichever
            // way she happens to be pointing. (What that draws as on screen then foreshortens, correctly.)
            BoatDeckDef def = TwoLevelHull();
            foreach (float heading in new[] { 0f, 45f, 90f, 210f })
            {
                int hint = -1;
                Vector2 start = new Vector2(0f, -3f);
                Vector2 moved = DeckWalkController.StepOnDeckPolygon(
                    start, Vector2.up, speed: 2f, dt: 0.5f, heading, Iso, def, ref hint, out _);
                Assert.AreEqual(1f, Vector2.Distance(start, moved), 1e-2f,
                                $"2 m/s for 0.5 s at {heading}° must be 1 m of deck");
            }
        }

        [Test]
        public void NoInputStillClampsSoATurningHullKeepsYouAboard()
        {
            BoatDeckDef def = DoryLike();
            int hint = -1;
            Vector2 offTheSide = new Vector2(3f, 0f);
            Vector2 held = DeckWalkController.StepOnDeckPolygon(
                offTheSide, Vector2.zero, speed: 2.5f, dt: 0.016f, 0f, Iso, def, ref hint, out _);
            Assert.AreEqual(0.225f, held.x, Tol);
        }

        [Test]
        public void ManyStepsIntoTheRailNeverLeaveTheDeck()
        {
            // The property that actually matters: hammer the input and the player stays on the polygon.
            BoatDeckDef def = TwoLevelHull();
            int hint = -1;
            Vector2 p = Vector2.zero;
            p = def.ClampToWalkable(p, ref hint, out _);

            var rng = new System.Random(4242);
            for (int i = 0; i < 500; i++)
            {
                float heading = (float)(rng.NextDouble() * 360.0);
                Vector2 push = new Vector2((float)(rng.NextDouble() * 2 - 1), (float)(rng.NextDouble() * 2 - 1));
                p = DeckWalkController.StepOnDeckPolygon(p, push, 2.5f, 0.02f, heading, Iso, def,
                                                         ref hint, out _);

                bool onSomeDeck = false;
                foreach (DeckArea a in def.Areas)
                {
                    if (a.Kind != DeckAreaKind.Deck) continue;
                    // A clamp lands ON the outline, where an even-odd test may read either way — so allow
                    // a hair of slack rather than pretending the boundary is inside.
                    DeckAreaMath.ClosestPointOnOutline(a.Outline, p, out float sqr);
                    if (DeckAreaMath.Contains(a.Outline, a.Bounds, p) || sqr < 1e-6f) { onSomeDeck = true; break; }
                }
                Assert.IsTrue(onSomeDeck, $"step {i} left the deck at {p}");
            }
        }

        [Test]
        public void AHullWithNoAreasLeavesThePointAlone()
        {
            // Absence is data: an empty def must not silently pull the player to the origin.
            var empty = ScriptableObject.CreateInstance<BoatDeckDef>();
            Assert.IsFalse(empty.HasWalkableDeck());
            int hint = -1;
            Vector2 p = new Vector2(3f, -2f);
            Assert.AreEqual(p, empty.ClampToWalkable(p, ref hint, out float h));
            Assert.AreEqual(0f, h);
        }
    }
}
