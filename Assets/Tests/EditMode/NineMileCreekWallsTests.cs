using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE QUAY'S OWN WALLS, held to the region's own numbers.</b> The owner's 2026-08-19 walk stepped
    /// off Nine Mile Creek's wharf and kept going: the quay had no player collision at all. This fixture
    /// is what stops that shipping again, and it asks its questions at the places a walker actually goes
    /// — the mooring face, the apron's three water sides, the way onto the quay, the brow, the ladder and
    /// the boat ramp.
    ///
    /// <para><b>Every number here is READ, never restated.</b> The edges come off
    /// <see cref="NineMileCreekWharf"/>'s own footprints and the ground off the region's own terrain, so
    /// re-siting a wall re-aims these tests instead of quietly making them lies.</para>
    ///
    /// <para><b>⚠ Half of this file is the NEGATIVE half, and it is the half that matters.</b> A wall
    /// everywhere is as broken as a wall nowhere: it fences the fisher off her own quay, off the brow to
    /// the float, and off the boat ramp. So each "is walled" claim below has a matching "is NOT walled"
    /// claim beside it, and several of the assertions exist purely so that a plan which walls the whole
    /// world cannot pass.</para>
    /// </summary>
    public class NineMileCreekWallsTests
    {
        readonly List<Object> _spawned = new List<Object>();

        MainlandTidalTerrain _terrain;
        List<Vector2[]> _runs;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("TidalTerrain");
            _spawned.Add(go);
            _terrain = go.AddComponent<MainlandTidalTerrain>();
            NineMileCreekBuilder.ConfigureNineMileCreekTerrain(_terrain);
            _runs = NineMileCreekWalls.Runs(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        /// <summary>How far a world point is from the nearest metre of planned wall.</summary>
        float WallDistance(Vector2 p)
        {
            float best = float.PositiveInfinity;
            for (int r = 0; r < _runs.Count; r++)
            {
                Vector2[] pts = _runs[r];
                for (int i = 0; i + 1 < pts.Length; i++)
                    best = Mathf.Min(best, GroundPolygon.DistanceToSegment(p, pts[i], pts[i + 1]));
            }
            return best;
        }

        void AssertWalled(Vector2 p, string what) =>
            Assert.Less(WallDistance(p), 0.2f,
                $"{what} at ({p.x:0.#}, {p.y:0.#}) has NO wall — the nearest planned metre is " +
                $"{WallDistance(p):0.00} m away. This is the 2026-08-19 defect: the fisher walks off the " +
                "quay here and falls 4.6 m into the basin.");

        void AssertOpen(Vector2 p, string what) =>
            Assert.Greater(WallDistance(p), 0.5f,
                $"{what} at ({p.x:0.#}, {p.y:0.#}) is WALLED — the nearest planned metre is " +
                $"{WallDistance(p):0.00} m away. A wall here does not stop a fall; it fences the fisher " +
                "out of somewhere she is meant to go.");

        // =========================================================================================
        //  the quay is walled at all
        // =========================================================================================

        [Test]
        public void TheQuay_IsWalled()
        {
            float metres = QuayEdgePlan.TotalLength(_runs);
            Debug.Log($"[NineMileCreekWallsTests] the quay's wall bill: {metres:0.#} m in {_runs.Count} " +
                      "run(s). Deck " + NineMileCreekWharf.DeckFootprint() + ", apron " +
                      NineMileCreekWharf.ApronFootprint() + ".");

            Assert.Greater(_runs.Count, 0, "the plan produced no runs at all — the quay is unwalled.");

            // The faces that are a fall: the 78 m of mooring edge east of the apron, the apron's 43 m
            // east face, its 10 m head and its 48 m west face. Comfortably over 150 m; a plan that came
            // back with 20 has found one edge and missed the rest.
            Assert.Greater(metres, 150f,
                $"only {metres:0.#} m of wall for a quay whose water faces run to about 180 m. Something " +
                "large is unwalled — read the run list in the log above and find which face is missing.");
        }

        // =========================================================================================
        //  the faces that are a fall
        // =========================================================================================

        [Test]
        public void TheMooringFace_IsWalled_AlongEveryBerth()
        {
            float lip = NineMileCreekWharf.MooringEdgeY;
            for (int i = 0; i < NineMileCreekWharf.BerthCount; i++)
            {
                Vector2 abreast = new Vector2(NineMileCreekWharf.BerthPos(i).x, lip);
                AssertWalled(abreast, $"the mooring face abreast of berth {i}");
            }
        }

        [Test]
        public void TheApronsThreeWaterFaces_AreWalled()
        {
            Rect apron = NineMileCreekWharf.ApronFootprint();

            // The EAST face, below where the north wall's deck takes over — clear of the brow's gate.
            AssertWalled(new Vector2(apron.xMax, 60f), "the apron's east face");
            AssertWalled(new Vector2(apron.xMax, 80f), "the apron's east face");
            // Its HEAD, the seaward end.
            AssertWalled(new Vector2(apron.center.x, apron.yMin), "the apron's head");
            // Its WEST face, which stands over the deep water the wharf grew here for.
            AssertWalled(new Vector2(apron.xMin, 55f), "the apron's west face");
            AssertWalled(new Vector2(apron.xMin, 85f), "the apron's west face");
        }

        [Test]
        public void TheWharfHead_IsWalled_WhereItStandsOverTheBasin()
        {
            Rect deck = NineMileCreekWharf.DeckFootprint();
            AssertWalled(new Vector2(deck.xMax, deck.yMin + 1f), "the wharf head's east face");
        }

        [Test]
        public void TheTroughBetweenTheApronAndTheSpit_IsWalled()
        {
            // ⭐ THE EDGE A TRACED LINE MISSES, AND THE ONE A SINGLE PROBE MISSES TOO. The apron's north
            // edge runs west to x = 82 but the north wall's deck only starts at x = 86, so a four-metre
            // bight of the apron's north side faces open ground with the spit four metres further north.
            // It is invisible on a plan drawing, and what is actually there is a TROUGH: two fills' skirts
            // pass each other, so the ground dips before it climbs to the yard.
            Rect apron = NineMileCreekWharf.ApronFootprint();
            Rect deck = NineMileCreekWharf.DeckFootprint();
            Assert.Less(apron.xMin, deck.xMin,
                "the apron no longer sticks out west of the deck, so the trough this test guards is gone — " +
                "delete the test rather than leaving it passing on geometry that stopped existing.");

            Vector2 lip = new Vector2((apron.xMin + deck.xMin) * 0.5f, apron.yMax);
            AssertWalled(lip, "the apron/spit trough");
        }

        [Test]
        public void OneReadingAtTheProbeDistance_WouldHaveMissedTheTrough()
        {
            // ⚠ THE MEASUREMENT THAT SHAPED THE RULE, pinned so it cannot be quietly undone. Walking north
            // off the apron's north-west bight the ground does not fall away — it dips into a trough and
            // climbs back out to the spit. So the reading two metres out is the HIGHEST of the four, and a
            // rule that took only that reading would leave this stretch unwalled with the wall built
            // either side of it, which is the most convincing way for this to be wrong.
            Rect apron = NineMileCreekWharf.ApronFootprint();
            Rect deck = NineMileCreekWharf.DeckFootprint();
            Vector2 lip = new Vector2((apron.xMin + deck.xMin) * 0.5f, apron.yMax);
            float deckElevation = NineMileCreekWharf.ApronElevationFrom(_terrain);

            float atProbe = _terrain.ElevationAt(lip + new Vector2(0f, QuayEdgePlan.DefaultProbeMetres));
            float lowest = float.PositiveInfinity;
            for (float d = 0.5f; d <= QuayEdgePlan.DefaultProbeMetres + 1e-4f; d += 0.5f)
                lowest = Mathf.Min(lowest, _terrain.ElevationAt(lip + new Vector2(0f, d)));

            Debug.Log($"[NineMileCreekWallsTests] north off the apron's bight at x={lip.x:0.#}: deck " +
                      $"+{deckElevation:0.00} m, lowest within {QuayEdgePlan.DefaultProbeMetres:0.#} m " +
                      $"{lowest:0.00} m, at exactly {QuayEdgePlan.DefaultProbeMetres:0.#} m {atProbe:0.00} m.");

            Assert.Less(deckElevation - atProbe, QuayEdgePlan.DefaultFallMetres,
                $"the single reading {QuayEdgePlan.DefaultProbeMetres:0.#} m out is now {atProbe:0.00} m, " +
                $"a {deckElevation - atProbe:0.00} m drop — the trough has been filled or the terrain " +
                "re-cut, so this straddle no longer exists. Re-measure before simplifying IsFallEdge back " +
                "to one reading; other edges may still hide one.");
            Assert.GreaterOrEqual(deckElevation - lowest, QuayEdgePlan.DefaultFallMetres,
                $"the LOWEST reading within {QuayEdgePlan.DefaultProbeMetres:0.#} m is {lowest:0.00} m, " +
                $"only {deckElevation - lowest:0.00} m below the deck — so there is no trough here and " +
                "the test above is measuring flat ground.");
        }

        // =========================================================================================
        //  …and the places a wall must NOT be
        // =========================================================================================

        [Test]
        public void TheWayOntoTheQuay_IsOpen()
        {
            // The spit yard stands 0.6 m ABOVE the deck and runs the whole length of it: this is how you
            // get on the wharf at all, and the only landward join the quay has.
            Rect deck = NineMileCreekWharf.DeckFootprint();
            for (float x = deck.xMin + 4f; x <= deck.xMax - 4f; x += 12f)
                AssertOpen(new Vector2(x, deck.yMax), "the way down onto the quay from the yard");
        }

        [Test]
        public void TheSeamBetweenTheTwoWalls_IsOpen()
        {
            // Where the apron runs under the north wall's deck there is no edge, only quay. A wall here
            // splits the wharf in two.
            Rect apron = NineMileCreekWharf.ApronFootprint();
            Rect deck = NineMileCreekWharf.DeckFootprint();
            float midX = (deck.xMin + apron.xMax) * 0.5f;

            AssertOpen(new Vector2(midX, deck.yMin), "the north wall's south edge over the apron");
            AssertOpen(new Vector2(midX, apron.yMax), "the apron's north edge under the north wall");
            AssertOpen(new Vector2(deck.xMin, (deck.yMin + apron.yMax) * 0.5f),
                       "the north wall's west end inside the apron");
        }

        [Test]
        public void TheBrow_HasAGateWideEnoughToWalkThrough()
        {
            Vector2 hinge = NineMileCreekWharf.GangwayShoreEnd;
            float clear = NineMileCreekWalls.BrowGateClearMetres;

            Assert.Greater(clear, QuayEdgePlan.WalkerFootRadiusMetres * 2f,
                "the brow's gate is no wider than the fisher, so she cannot enter it");

            // Daylight down the middle of the opening…
            AssertOpen(hinge, "the brow's hinge");
            Assert.Greater(WallDistance(hinge), clear * 0.5f - 0.15f,
                $"the brow's gate is narrower than the {clear:0.00} m it was authored at — the nearest " +
                $"wall is {WallDistance(hinge):0.00} m from the hinge, and the fisher is " +
                $"{QuayEdgePlan.WalkerFootRadiusMetres * 2f:0.00} m across.");

            // …and wall either side of it, or this is not a gate but a missing face.
            AssertWalled(new Vector2(hinge.x, hinge.y + clear), "the apron's east face north of the brow");
            AssertWalled(new Vector2(hinge.x, hinge.y - clear), "the apron's east face south of the brow");
        }

        [Test]
        public void TheBoatRamp_HasNoWallAnywhereNearIt()
        {
            // ⭐ THE ONE LEGITIMATE GRADE INTO THIS WATER — a real slope in the terrain out on the natural
            // shore, west of the whole wharf. No plan may come near it, and the margin here is generous on
            // purpose: "near" is what breaks a ramp, not "on".
            Vector2 head = NineMileCreekMainland.BoatRampHeadPos;
            Vector2 toe = NineMileCreekMainland.BoatRampToePos;

            for (float t = 0f; t <= 1f; t += 0.1f)
            {
                Vector2 on = Vector2.Lerp(head, toe, t);
                Assert.Greater(WallDistance(on), 3f,
                    $"a planned wall comes within {WallDistance(on):0.0} m of the boat ramp at " +
                    $"({on.x:0.#}, {on.y:0.#}). The ramp is how you get into this water on foot and with " +
                    "the amphibian, and the quay's walls have no business within reach of it.");
            }
        }

        // =========================================================================================
        //  boarding still works — the acceptance the gates exist for
        // =========================================================================================

        [Test]
        public void TheLadder_IsWalled_AndTheClimbStillReachesItFromTheDeck()
        {
            // ⚠ THE LADDER GETS NO GATE, ON PURPOSE. Going down it is a scripted move that begins from
            // wherever the fisher is STANDING — ControlSwitcher looks for a ladder within
            // GameConfig.LadderBoarding.LadderReachMetres of her and then drives her transform — so a wall
            // at the lip is not in its way. A HOLE there would be a 4.6 m fall cut into the exact wall
            // this slice exists to build.
            Vector2 ladder = Vector2.zero;
            bool found = false;
            foreach (var f in NineMileCreekWharf.Fittings())
                if (f.Name == "ladder") { ladder = f.Position; found = true; break; }

            Assert.IsTrue(found, "the quay has no ladder fitting, so this test is measuring an empty wall");
            AssertWalled(ladder, "the lip at the ladder");

            // She stands on the concrete a half-metre inboard of the wall — where the bollards stand —
            // and the ladder is still well inside the reach the boarding move uses (4 m by default).
            float standoff = PropertyBoundary.DefaultThicknessMetres * 0.5f
                           + QuayEdgePlan.WalkerFootRadiusMetres + 0.1f;
            Vector2 standing = new Vector2(ladder.x, ladder.y + standoff);
            Assert.Less(Vector2.Distance(standing, ladder), 4f,
                $"standing clear of the wall puts the fisher {Vector2.Distance(standing, ladder):0.00} m " +
                "from the ladder, outside the boarding move's reach — the wall HAS closed the boarding " +
                "point and needs a gate after all.");
        }

        [Test]
        public void TheDisembarkPoint_LandsHerOnTheDeck_InsideTheWall()
        {
            // Coming ashore teleports her to DisembarkPos. That point must be on the quay side of the
            // wall, or every arrival at Nine Mile Creek starts with the physics shoving her somewhere.
            Vector2 ashore = NineMileCreekMainland.DisembarkPos;
            Rect deck = NineMileCreekWharf.DeckFootprint();

            Assert.IsTrue(StandablePlatform.Contains(deck, ashore),
                $"the disembark point {ashore} is not on the north wall's deck {deck} at all.");
            Assert.Greater(ashore.y, deck.yMin + PropertyBoundary.DefaultThicknessMetres * 0.5f,
                $"the disembark point sits within half a wall thickness of the lip at y={deck.yMin:0.#}: " +
                "she arrives inside the wall and gets pushed. Move the point inboard or drop the wall's " +
                "thickness — do not delete the wall.");
        }

        // =========================================================================================
        //  the slipway: a finding, pinned so it cannot be quietly forgotten
        // =========================================================================================

        [Test]
        public void TheSlipway_IsWalled_BecauseItIsNotInTheTerrain()
        {
            // ⚠ FINDING (this slice). The boatyard's ramp is DRAWN running east off the apron at y = 52,
            // but it appears in no fill: the ground a couple of metres east of the apron there is the
            // −1.6 m basin, exactly as it is either side. So a gate at the slipway would open 8 m of quay
            // onto a drop with no slope under it. It is walled, and the honest fix if the owner wants it
            // walkable is a terrain fill — the grade must exist before the wall may open for it.
            Vector2 head = NineMileCreekMainland.SlipwayHeadPos;
            float deck = NineMileCreekWharf.ApronElevationFrom(_terrain);
            float justOff = _terrain.ElevationAt(head + new Vector2(QuayEdgePlan.DefaultProbeMetres, 0f));

            Assert.Greater(deck - justOff, QuayEdgePlan.DefaultFallMetres,
                $"the ground {QuayEdgePlan.DefaultProbeMetres:0.#} m down the slipway is now " +
                $"{justOff:0.00} m against a deck of {deck:0.00} m — the ramp HAS a grade in the terrain " +
                "now. Give it a gate (NineMileCreekWalls.Gates) and delete this test.");
            AssertWalled(head, "the slipway head");
        }
    }
}
