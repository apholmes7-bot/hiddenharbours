using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE DECK WHOSE HEIGHT IS THE TIDE'S</b> — the float, the brow you reach it by, and the cleat
    /// you tie to, as arithmetic.
    ///
    /// <para><b>What this file is FOR.</b> <see cref="StandablePlatform"/> takes one fixed elevation, which
    /// is right for a quay and wrong for a pontoon: at Nine Mile Creek a float swings through 4.4 m, so a
    /// fixed registration would put the fisher two metres over the water at low tide or two metres under
    /// it at high — <b>a wrong number that nothing would fail on</b>, which is why the wharf builder
    /// refused to build the walk onto the float at all until this landed. These tests are the things that
    /// now fail instead.</para>
    ///
    /// <para><b>And what it proves about the SEAM.</b> <see cref="IStandableSurface"/> always claimed a
    /// moving deck was an implementation of its contract rather than a change to it (its own doc says so,
    /// and <c>StandableSurfacesTests</c> has a fake round moving deck asserting the claim compiles). The
    /// float is the first REAL one, and the tests below drive it through
    /// <see cref="StandableSurfaces"/> — the same composition the quay uses — rather than through its own
    /// API, so "Core did not have to move" is a measurement and not a hope.</para>
    ///
    /// <para>Pure logic over fake terrain / environment doubles and the REAL components. No scene, no tide
    /// service; the water level is handed in, exactly as every other tidal test in this project does it.</para>
    /// </summary>
    public class FloatingPlatformTests
    {
        /// <summary>A flat seabed at one elevation — the shape a dredged lane has under a float.</summary>
        private sealed class FlatBed : ITidalTerrain
        {
            public float Elevation;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        private sealed class FlatEnv : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        // The float this file argues about: the wharf rig's committed timber float, quoted here as plain
        // numbers because a UNIT test's job is the RULE. That these are the numbers Nine Mile Creek
        // actually builds with is NineMileCreekFloatTests' job, and it pins them to the rig's own source.
        private const float Freeboard = 0.40f;
        private const float Draught = 0.31f;
        private const float HullDepth = Freeboard + Draught;   // 0.71 m, deck to underside

        private readonly List<Object> _spawned = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            StandableSurfaces.Clear();
            MooringCleats.Clear();
            GameServices.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            StandableSurfaces.Clear();
            MooringCleats.Clear();
            GameServices.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        /// <summary>A real <see cref="FloatingPlatform"/>, registered explicitly — <c>OnEnable</c> does not
        /// run in edit mode, and registering a deck is a runtime act (the reason
        /// <c>StandableSurfacesTests</c> gives for doing the same).</summary>
        private FloatingPlatform Float(Rect footprint, float bed, bool register = true)
        {
            var go = new GameObject("Float_Test");
            _spawned.Add(go);
            var f = go.AddComponent<FloatingPlatform>();
            f.Configure("float.test", footprint, bed, Draught, Freeboard);
            if (register) StandableSurfaces.Register(f);
            return f;
        }

        private GangwayPlatform Brow(Vector2 shore, Vector2 landing, float abutment, FloatingPlatform onto,
                                     float halfWidth = 0.475f, bool register = true)
        {
            var go = new GameObject("Gangway_Test");
            _spawned.Add(go);
            var g = go.AddComponent<GangwayPlatform>();
            g.Configure("gangway.test", shore, landing, halfWidth, abutment, onto);
            if (register) StandableSurfaces.Register(g);
            return g;
        }

        private FloatCleat Cleat(Vector2 at, FloatingPlatform onto)
        {
            var go = new GameObject("FloatCleat_Test");
            _spawned.Add(go);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var c = go.AddComponent<FloatCleat>();
            c.Configure("cleat.test", onto);
            MooringCleats.Register(c);
            return c;
        }

        // =============================================================================================
        //  1. THE ONE LINE — deck = max(water − draught, bed) + hull depth
        // =============================================================================================

        /// <summary>⭐ Afloat, the deck is the water plus her freeboard, and NOTHING else enters into it.
        /// Walked across a whole 4.4 m cycle so the claim is about the mechanism rather than about one
        /// convenient hour.</summary>
        [Test]
        public void AfloatTheDeckIsTheWaterPlusHerFreeboard_AtEveryHourOfTheCycle()
        {
            const float Bed = -3.5f;                 // deep enough that she never touches
            for (float water = -2.2f; water <= 2.2f + 1e-4f; water += 0.2f)
            {
                Assert.That(FloatingPlatform.DeckElevation(water, Bed, Draught, Freeboard),
                            Is.EqualTo(water + Freeboard).Within(1e-4f),
                            $"at water {water:0.00} m she must ride her own freeboard above it");
                Assert.That(FloatingPlatform.IsAground(water, Bed, Draught), Is.False,
                            $"at water {water:0.00} m over a {Bed:0.00} m bed she has " +
                            $"{water - Bed:0.00} m under her against a {Draught:0.00} m draught");
            }
        }

        /// <summary>
        /// ⭐ And the other half of the SAME expression: when the ebb takes the water down to where her
        /// underside meets the ground, the deck <b>stops falling</b> and the water keeps going. Grounding
        /// is not a second rule that could disagree with the first — it is the <c>max</c>.
        /// </summary>
        [Test]
        public void OnTheEbbSheTakesTheGround_AndTheDeckStopsFallingWhileTheWaterDoesNot()
        {
            const float Bed = -1.0f;
            float touches = Bed + Draught;                     // the water level at which she first grounds

            Assert.That(FloatingPlatform.DeckElevation(touches + 0.5f, Bed, Draught, Freeboard),
                        Is.EqualTo(touches + 0.5f + Freeboard).Within(1e-4f), "half a metre up: afloat");

            // At the moment of touching, the two readings AGREE — the expression is continuous, so there
            // is no step in the deck as she settles.
            Assert.That(FloatingPlatform.DeckElevation(touches, Bed, Draught, Freeboard),
                        Is.EqualTo(Bed + HullDepth).Within(1e-4f),
                        "afloat and aground must give the same deck at the instant she touches, or the " +
                        "player is teleported the moment the tide crosses it");

            // Below it the deck is pinned to the ground and the water keeps ebbing away beneath her.
            foreach (float below in new[] { 0.1f, 0.5f, 1.2f })
            {
                float water = touches - below;
                Assert.That(FloatingPlatform.DeckElevation(water, Bed, Draught, Freeboard),
                            Is.EqualTo(Bed + HullDepth).Within(1e-4f),
                            $"{below:0.0} m below touching she is ON the bottom — the deck cannot follow");
                Assert.That(FloatingPlatform.IsAground(water, Bed, Draught), Is.True);
            }
        }

        /// <summary>Aground means the same thing for a float as for a hull: less water than draught. Asked
        /// against the very rule <c>BoatCrossing.CanFloat</c> holds every boat to, spelled out here rather
        /// than referenced so the two cannot quietly diverge.</summary>
        [Test]
        public void AgroundIsTheSameDepthRuleEveryHullIsHeldTo()
        {
            const float Bed = -0.8f;
            for (float water = -2.2f; water <= 2.2f + 1e-4f; water += 0.1f)
            {
                float depth = water - Bed;
                // ⚠ Skip the knife edge itself. The two forms subtract in a different order
                // (depth ≥ draught vs water − draught < bed), so within an ULP of the exact touching
                // point they may round apart — which is a property of floats, not a disagreement about
                // the rule. A millimetre either side is what a player could ever notice.
                if (Mathf.Abs(depth - Draught) < 1e-3f) continue;

                bool canFloat = depth >= Draught;              // BoatCrossing.CanFloat, verbatim
                Assert.That(FloatingPlatform.IsAground(water, Bed, Draught), Is.EqualTo(!canFloat),
                            $"at water {water:0.00} m the depth is {depth:0.00} m against a " +
                            $"{Draught:0.00} m draught — a float and a boat must not disagree about this");
            }
        }

        /// <summary>An unmeasured bed (−∞) is "no bed known", so she cannot claim to ground. The gate-off
        /// reading, and the reason the builder MEASURES rather than leaving the default.</summary>
        [Test]
        public void WithNoBedAuthored_SheCannotTakeTheGround()
        {
            Assert.That(FloatingPlatform.DeckElevation(-2.2f, float.NegativeInfinity, Draught, Freeboard),
                        Is.EqualTo(-2.2f + Freeboard).Within(1e-4f));
            Assert.That(FloatingPlatform.IsAground(-99f, float.NegativeInfinity, Draught), Is.False);
        }

        /// <summary>
        /// ⚠ A NaN bed degrades to AFLOAT, not to a NaN deck. <c>Mathf.Max(x, NaN)</c> returns NaN, and a
        /// NaN standing elevation would make every on-foot depth read NaN with nothing to point at — so
        /// the maths is written as an explicit comparison. This is the test that would fail if someone
        /// "tidied" it back into a <c>Mathf.Max</c>.
        /// </summary>
        [Test]
        public void ANaNBedDegradesToAfloat_RatherThanPoisoningTheDeck()
        {
            float deck = FloatingPlatform.DeckElevation(1.0f, float.NaN, Draught, Freeboard);
            Assert.That(float.IsNaN(deck), Is.False, "a NaN bed must never reach the standing elevation");
            Assert.That(deck, Is.EqualTo(1.0f + Freeboard).Within(1e-4f));
        }

        /// <summary>Negative draught/freeboard are floored at zero — a float that draws less than nothing
        /// is data we cannot honour, and the honest degenerate reading is a raft of no thickness.</summary>
        [Test]
        public void NonsenseDimensionsFloorAtZero_RatherThanInvertingHer()
        {
            Assert.That(FloatingPlatform.HullDepth(-5f, -5f), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(FloatingPlatform.DeckElevation(0.5f, -10f, -1f, -1f), Is.EqualTo(0.5f).Within(1e-4f));
        }

        // =============================================================================================
        //  2. THROUGH THE SEAM — the float composes exactly as the quay does
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>Core did not have to move, and this is the measurement.</b> The float is read through
        /// <see cref="StandableSurfaces.OnFootDepth"/> — the one composition every on-foot consumer uses —
        /// and it answers at every tide. The number that comes back is the same at every hour, which is
        /// the physical truth about standing on a float: <b>you are always exactly one freeboard above
        /// the water</b>, so the wade bands, the walk gate and the body's waterline can never disagree
        /// about a deck that is moving under them.
        /// </summary>
        [Test]
        public void StandingOnAFloat_TheWaterIsAlwaysExactlyOneFreeboardBelowYourFeet()
        {
            var terrain = new FlatBed { Elevation = -3.5f };
            var onFloat = new Vector2(5f, 0f);
            var surfaces = new IStandableSurface[] { Float(new Rect(0f, -1.2f, 48f, 2.4f), -3.5f) };

            for (float water = -2.2f; water <= 2.2f + 1e-4f; water += 0.4f)
            {
                GameServices.Environment = new FlatEnv { Level = water };
                float depth = StandableSurfaces.OnFootDepth(terrain, GameServices.Environment, surfaces,
                                                            0.0, onFloat);
                Assert.That(depth, Is.EqualTo(-Freeboard).Within(1e-4f),
                            $"at water {water:0.00} m the planks must still be {Freeboard:0.00} m clear — " +
                            "a float that flooded at high water or stood on stilts at low would show here");
            }
        }

        /// <summary>…and one metre off her edge the answer is back to the seabed, unchanged. A float that
        /// claimed water it does not cover is how a player walks out to sea.</summary>
        [Test]
        public void OneMetreOffHerEdge_TheSeabedAnswersAgain()
        {
            var terrain = new FlatBed { Elevation = -3.5f };
            FloatingPlatform dock = Float(new Rect(0f, -1.2f, 48f, 2.4f), -3.5f);
            var surfaces = new IStandableSurface[] { dock };
            GameServices.Environment = new FlatEnv { Level = 0f };

            var offside = new Vector2(5f, 2.2f);               // a metre north of the 1.2 m half-width
            Assert.That(StandableSurfaces.OnFootDepth(terrain, GameServices.Environment, surfaces, 0.0, offside),
                        Is.EqualTo(3.5f).Within(1e-4f), "off the float you are in the dredged lane");

            // …and the lip itself IS planks (the inclusive-edge rule the fixed platform states). Asked at
            // the footprint's OWN yMax rather than at a re-typed 1.2f: Rect computes yMax as
            // yMin + height, and a literal that merely looks the same can miss it by an ULP — which
            // would make this test about float arithmetic instead of about the edge rule.
            float lip = dock.Footprint.yMax;
            Assert.That(StandableSurfaces.OnFootDepth(terrain, GameServices.Environment, surfaces, 0.0,
                                                      new Vector2(5f, lip)),
                        Is.EqualTo(-Freeboard).Within(1e-4f), "a point exactly on the lip is on the planks");
        }

        /// <summary>With no environment service wired the water reads 0 and she sits at her own freeboard
        /// above datum — the established gate-off shape (<c>BoatCleats.ElevationOf</c>), not a special
        /// case, and not a throw.</summary>
        [Test]
        public void WithNoEnvironmentService_TheWaterReadsZeroAndSheSitsAtHerOwnFreeboard()
        {
            var dock = Float(new Rect(0f, -1.2f, 48f, 2.4f), -3.5f);
            Assert.That(FloatingPlatform.WaterLevelNow(), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(dock.DeckElevationNow(), Is.EqualTo(Freeboard).Within(1e-4f));
            Assert.That(dock.IsAgroundNow(), Is.False);
        }

        // =============================================================================================
        //  3. THE BROW — a ramp, not a height
        // =============================================================================================

        /// <summary>The deck along the brow is the straight line from the fixed abutment to wherever the
        /// float happens to be — checked at the ends and at the middle, so "it interpolates" is asserted
        /// rather than assumed from the ends agreeing.</summary>
        [Test]
        public void TheBrowRunsStraightFromTheAbutmentToWhereverTheFloatIs()
        {
            var dock = Float(new Rect(12f, -1.2f, 48f, 2.4f), -3.5f);
            var brow = Brow(new Vector2(0f, 0f), new Vector2(12f, 0f), 3.0f, dock);
            GameServices.Environment = new FlatEnv { Level = -2.2f };   // spring low

            float landing = dock.DeckElevationNow();                    // −1.80 m
            Assert.That(landing, Is.EqualTo(-2.2f + Freeboard).Within(1e-4f));

            Assert.That(brow.TryGetDeckElevation(new Vector2(0f, 0f), out float atHinge), Is.True);
            Assert.That(atHinge, Is.EqualTo(3.0f).Within(1e-4f), "the hinge is bolted to the abutment");

            Assert.That(brow.TryGetDeckElevation(new Vector2(12f, 0f), out float atLanding), Is.True);
            Assert.That(atLanding, Is.EqualTo(landing).Within(1e-4f),
                        "the landing end IS the float's deck — read off it, never a second copy");

            Assert.That(brow.TryGetDeckElevation(new Vector2(6f, 0f), out float halfway), Is.True);
            Assert.That(halfway, Is.EqualTo((3.0f + landing) * 0.5f).Within(1e-4f), "halfway is halfway");
        }

        /// <summary>⭐ And it re-solves its own slope every time the tide moves: steep at low water, nearly
        /// flat at high. Nothing animates — the brow simply asks the float where it is.</summary>
        [Test]
        public void TheBrowReSolvesItsSlopeFromTheTide_SteepOnTheEbbAndFlatOnTheFlood()
        {
            var dock = Float(new Rect(12f, -1.2f, 48f, 2.4f), -3.5f);
            var brow = Brow(new Vector2(0f, 0f), new Vector2(12f, 0f), 3.0f, dock);

            Assert.That(brow.RunMetres, Is.EqualTo(12f).Within(1e-4f));

            float atLow = brow.SlopeAt(-2.2f);      // drop 3.0 − (−1.80) = 4.80 m over 12 m
            float atHigh = brow.SlopeAt(2.2f);      // drop 3.0 −   2.60  = 0.40 m over 12 m
            Assert.That(brow.DropAt(-2.2f), Is.EqualTo(4.8f).Within(1e-4f));
            Assert.That(brow.DropAt(2.2f), Is.EqualTo(0.4f).Within(1e-4f));
            Assert.That(atLow, Is.EqualTo(0.4f).Within(1e-4f), "1:2.5 at dead low spring");
            Assert.That(atHigh, Is.LessThan(atLow * 0.15f), "…and all but level at the top of the tide");
        }

        /// <summary>No float, no brow. The surface reports <c>false</c> and the sim falls back to the
        /// seabed — it must NOT invent a flat walkway at the abutment height out of a missing reference,
        /// which would be a bridge over open water drawn from nothing.</summary>
        [Test]
        public void WithNothingToLandOn_ThereIsNoBrowAtAll()
        {
            var brow = Brow(new Vector2(0f, 0f), new Vector2(12f, 0f), 3.0f, null);
            Assert.That(brow.TryGetDeckElevation(new Vector2(6f, 0f), out _), Is.False);
            Assert.That(brow.DropAt(0f), Is.EqualTo(0f).Within(1e-6f));
        }

        /// <summary>The brow's footprint is a segment with a half-width: off the sides and past the ends
        /// there is nothing, and both the ends and the edges are INCLUSIVE for the reason the fixed
        /// platform gives — a point exactly on the lip is on the planks.</summary>
        [Test]
        public void TheBrowsFootprintIsTheSegment_EndsAndEdgesInclusive()
        {
            var from = new Vector2(0f, 0f);
            var to = new Vector2(12f, 0f);

            Assert.That(GangwayPlatform.TryProject(from, to, new Vector2(6f, 0f), 0.475f, out float mid), Is.True);
            Assert.That(mid, Is.EqualTo(0.5f).Within(1e-4f));

            // ⚠ The side edge is BRACKETED rather than stood on: the width test goes through
            // Vector2.Distance, whose sqrt can land a hair either side of an exact half-width, and a
            // test that turns on that is a test about arithmetic. The ENDS are asserted exactly below,
            // where the along-fraction is a ratio of exact values and lands on 0 and 1 cleanly.
            Assert.That(GangwayPlatform.TryProject(from, to, new Vector2(6f, 0.47f), 0.475f, out _), Is.True,
                        "a few millimetres inside the edge is planks");
            Assert.That(GangwayPlatform.TryProject(from, to, new Vector2(6f, 0.6f), 0.475f, out _), Is.False,
                        "a hand's breadth outside it is water");
            Assert.That(GangwayPlatform.TryProject(from, to, from, 0.475f, out float atHinge), Is.True,
                        "the hinge end is on the brow");
            Assert.That(atHinge, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(GangwayPlatform.TryProject(from, to, to, 0.475f, out float atLanding), Is.True,
                        "…and so is the landing end");
            Assert.That(atLanding, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(GangwayPlatform.TryProject(from, to, new Vector2(-0.5f, 0f), 0.475f, out _), Is.False,
                        "past the hinge you are on the quay, not the brow");
            Assert.That(GangwayPlatform.TryProject(from, to, new Vector2(12.5f, 0f), 0.475f, out _), Is.False,
                        "past the landing you are on the float, not the brow");
            Assert.That(GangwayPlatform.TryProject(from, from, new Vector2(0f, 0f), 0.475f, out _), Is.False,
                        "a brow with no run is not a surface");
        }

        /// <summary>The brow works at an ANGLE too — the reason its footprint is a segment and not a
        /// <see cref="Rect"/>. An axis-aligned box around a diagonal brow would claim water either side
        /// of it.</summary>
        [Test]
        public void ADiagonalBrowClaimsOnlyItsOwnPlanks_WhichARectangleCouldNot()
        {
            var from = new Vector2(0f, 0f);
            var to = new Vector2(10f, 10f);
            Assert.That(GangwayPlatform.TryProject(from, to, new Vector2(5f, 5f), 0.475f, out _), Is.True,
                        "on the centre line");
            Assert.That(GangwayPlatform.TryProject(from, to, new Vector2(9f, 1f), 0.475f, out _), Is.False,
                        "inside the bounding box, and a long way off the planks");
        }

        /// <summary>Two decks meeting must not report sea between them. Walked across the whole join at
        /// spring low, where the step is biggest, through the LIVE registry — the case a half-open
        /// footprint rule would fail on.</summary>
        [Test]
        public void WhereTheQuayTheBrowAndTheFloatMeet_ThereIsNoSeamOfWater()
        {
            var terrain = new FlatBed { Elevation = -3.5f };
            GameServices.Environment = new FlatEnv { Level = -2.2f };

            var quayGo = new GameObject("Quay_Test");
            _spawned.Add(quayGo);
            var quay = quayGo.AddComponent<StandablePlatform>();
            quay.Configure("quay.test", new Rect(-10f, -5f, 10f, 10f), 3.0f);   // ends at x = 0
            StandableSurfaces.Register(quay);

            var dock = Float(new Rect(12f, -1.2f, 48f, 2.4f), -3.5f);
            Brow(new Vector2(0f, 0f), new Vector2(12f, 0f), 3.0f, dock);

            // ⚠ The PURE overload, driven with the live registry. OnFootDepthNow would read
            // GameServices.TidalTerrain, which nothing sets here, and its absent-service contract is
            // NegativeInfinity ("as dry as can be") — under which this walk would pass through a hole
            // in the middle of it. The bed is handed in instead, so a gap reports the 1.3 m of water
            // actually standing in the lane at spring low and the assert has something to catch.
            for (float x = -1f; x <= 14f; x += 0.25f)
            {
                var at = new Vector2(x, 0f);
                float depth = StandableSurfaces.OnFootDepth(terrain, GameServices.Environment,
                                                            StandableSurfaces.Active, 0.0, at);
                Assert.That(depth, Is.LessThanOrEqualTo(0f),
                            $"at x = {x:0.00} the walk from quay to float reports {depth:0.00} m of water " +
                            "— somewhere along here there is a gap you would fall through");
            }
        }

        // =============================================================================================
        //  4. THE TIE-OFF — and why a float is the kind berth
        // =============================================================================================

        /// <summary>The cleat IS the float's deck: one number, so a rope can never be made fast two metres
        /// above the planks beside it.</summary>
        [Test]
        public void TheCleatRidesThePlanksItIsBoltedTo()
        {
            var dock = Float(new Rect(0f, -1.2f, 48f, 2.4f), -3.5f);
            var cleat = Cleat(new Vector2(10f, 0f), dock);

            foreach (float water in new[] { -2.2f, -1f, 0f, 1.4f, 2.2f })
            {
                GameServices.Environment = new FlatEnv { Level = water };
                Assert.That(cleat.ElevationMeters, Is.EqualTo(dock.DeckElevationNow()).Within(1e-5f));
                Assert.That(cleat.ElevationMeters, Is.EqualTo(water + Freeboard).Within(1e-4f));
            }
        }

        /// <summary>
        /// ⭐ <b>AND THIS IS WHY A FLOAT IS THE KIND BERTH.</b> A boat's cleat sits at
        /// <c>water + her freeboard</c> and a float's at <c>water + the float's</c>, so the drop between
        /// them is the difference of two freeboards — <b>constant, whatever the tide does</b>. A line made
        /// fast here never comes tight on the ebb, which the wall berths cannot say (there the drop grows
        /// by the whole 4.4 m range and a short scope slips). Nothing implements it; it falls out.
        /// </summary>
        [Test]
        public void AtAFloatTheDropToABoatsOwnCleatNeverChanges_WhichAWallBerthCannotSay()
        {
            const float BoatCleatAboveWaterline = 0.85f;     // a lobster boat's rail, near enough
            const float QuayDeck = 3.0f;                     // the wall berth, for the contrast

            var dock = Float(new Rect(0f, -1.2f, 48f, 2.4f), -3.5f);
            var cleat = Cleat(new Vector2(10f, 0f), dock);

            float firstFloatDrop = float.NaN;
            for (float water = -2.2f; water <= 2.2f + 1e-4f; water += 0.2f)
            {
                GameServices.Environment = new FlatEnv { Level = water };
                float boat = MooringLineMath.BoatCleatElevation(water, BoatCleatAboveWaterline);

                float floatDrop = boat - cleat.ElevationMeters;
                if (float.IsNaN(firstFloatDrop)) firstFloatDrop = floatDrop;
                Assert.That(floatDrop, Is.EqualTo(firstFloatDrop).Within(1e-4f),
                            $"at water {water:0.00} m the drop at the float moved — it must not");
                Assert.That(floatDrop, Is.EqualTo(BoatCleatAboveWaterline - Freeboard).Within(1e-4f),
                            "…and it is exactly the difference of the two freeboards");
            }

            // …against the wall berth, at the two ends of the same tide, stated exactly rather than
            // swept up from the loop's last accumulated step.
            float quayLow = QuayDeck - MooringLineMath.BoatCleatElevation(-2.2f, BoatCleatAboveWaterline);
            float quayHigh = QuayDeck - MooringLineMath.BoatCleatElevation(2.2f, BoatCleatAboveWaterline);
            Assert.That(quayLow - quayHigh, Is.EqualTo(4.4f).Within(1e-3f),
                        "the wall berth's drop must swing by the WHOLE range — that contrast is the point");
        }

        /// <summary>A float cleat is still SHORE-side: it is the landward end of a rope, and a line is only
        /// ever made fast between opposing sides. What moved is the elevation, not the side.</summary>
        [Test]
        public void AFloatCleatIsStillTheLandwardEndOfTheRope()
        {
            var dock = Float(new Rect(0f, -1.2f, 48f, 2.4f), -3.5f);
            Assert.That(Cleat(new Vector2(10f, 0f), dock).Side, Is.EqualTo(CleatSide.Shore));
        }

        /// <summary>Bolted to nothing it reads datum rather than claiming a height it has no basis for.</summary>
        [Test]
        public void ACleatOnNoFloatReadsDatum()
        {
            Assert.That(Cleat(new Vector2(10f, 0f), null).ElevationMeters, Is.EqualTo(0f).Within(1e-6f));
        }
    }
}
