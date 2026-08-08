using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The tide law</b> (M2-38) — the piece of the mooring feature that had to be got right rather than
    /// merely built, pinned as pure maths with no scene and no physics loop.
    ///
    /// <list type="bullet">
    ///   <item><b>SCOPE vs DROP</b> — a line's horizontal reach is what the tide leaves of its length. The
    ///   Pythagorean trade, its zero, and the fact that it is monotonic in the drop.</item>
    ///   <item><b>BOTH HAZARDS FROM ONE FUNCTION</b> — a boat hanging below her cleat and a boat lifted
    ///   above it are the same geometry. The owner named both; the symmetry means neither needed its own
    ///   rule.</item>
    ///   <item><b>THE SEAMANSHIP GRADIENT</b> — with St Peters' real numbers, a generous scope rides an
    ///   ordinary tide out and a snugged-up one is lost by it. If this test ever goes quiet, "mind the
    ///   tide" has become scenery.</item>
    ///   <item><b>SLIP</b> — the working-load threshold, and that it can only be crossed by the drop.</item>
    ///   <item><b>DETERMINISM + NaN</b> — identical inputs give bit-identical answers; junk reads as the
    ///   neutral value rather than propagating (rule 5).</item>
    /// </list>
    /// </summary>
    public class MooringLineMathTests
    {
        // St Peters as the region actually stands since the 2026-08-01 pacing ruling: the pier deck is
        // measured at +5.35 m above datum and the tide swings ±2.2 m about mean. A small hull's cleat sits
        // ~0.6 m above her waterline. These are the numbers the gradient test below reasons with.
        private const float DeckElevation = 5.35f;
        private const float TideAmplitude = 2.2f;
        private const float CleatFreeboard = 0.6f;

        private static float BoatCleatAt(float tideOffset)
            => MooringLineMath.BoatCleatElevation(tideOffset, CleatFreeboard);

        private static float DropAt(float tideOffset)
            => MooringLineMath.VerticalDrop(BoatCleatAt(tideOffset), DeckElevation);

        // ---- the trade -------------------------------------------------------------------------

        [Test]
        public void HorizontalReach_IsTheScopeLessWhatTheDropSpends()
        {
            // 5 m of line with 3 m of drop leaves the 3-4-5 triangle's 4 m across the water.
            Assert.AreEqual(4f, MooringLineMath.HorizontalReach(5f, 3f), 1e-4f);
            // No drop at all → the whole line reaches across.
            Assert.AreEqual(5f, MooringLineMath.HorizontalReach(5f, 0f), 1e-4f);
        }

        [Test]
        public void HorizontalReach_IsZeroOnceTheDropEatsTheWholeLine()
        {
            Assert.AreEqual(0f, MooringLineMath.HorizontalReach(5f, 5f), 1e-4f, "exactly hanging");
            Assert.AreEqual(0f, MooringLineMath.HorizontalReach(5f, 9f), 1e-4f,
                            "past hanging is still zero, never a NaN from a negative root");
        }

        [Test]
        public void HorizontalReach_ShrinksMonotonicallyAsTheWaterLeaves()
        {
            float previous = float.PositiveInfinity;
            for (float drop = 0f; drop <= 6f; drop += 0.5f)
            {
                float reach = MooringLineMath.HorizontalReach(6f, drop);
                Assert.Less(reach, previous, $"reach must keep shrinking as the drop grows (drop={drop})");
                previous = reach;
            }
        }

        // ---- one function, both hazards --------------------------------------------------------

        [Test]
        public void VerticalDrop_IsSymmetric_SoLiftedAndHungAreTheSameGeometry()
        {
            // The owner named two hazards: a falling tide dropping her below a wharf cleat, and a rising
            // one lifting her above a low one. They are the same absolute gap, which is why a single
            // function covers both and neither needed a rule of its own.
            float hungBelow = MooringLineMath.VerticalDrop(1f, 4f);   // boat 3 m below her cleat
            float liftedAbove = MooringLineMath.VerticalDrop(7f, 4f); // boat 3 m above it
            Assert.AreEqual(3f, hungBelow, 1e-4f);
            Assert.AreEqual(hungBelow, liftedAbove, 1e-4f, "below and above are the same strain");
        }

        [Test]
        public void RisingTideOnALowCleat_TightensTheLine()
        {
            // Tied bar-taut at low water to something at the waterline (a float, a ring): as she rises
            // PAST it the gap opens again and the line pins her down. The mirrored hazard.
            const float lowRing = 0f;
            float atLowWater = MooringLineMath.VerticalDrop(BoatCleatAt(-TideAmplitude), lowRing);
            float atHighWater = MooringLineMath.VerticalDrop(BoatCleatAt(+TideAmplitude), lowRing);
            Assert.Less(atLowWater, atHighWater,
                        "a cleat at the waterline is strained by the RISING tide, not the falling one");
        }

        [Test]
        public void FallingTideOnAHighWharf_TightensTheLine()
        {
            Assert.Less(DropAt(+TideAmplitude), DropAt(-TideAmplitude),
                        "a wharf cleat high above her is strained by the FALLING tide — the classic case");
        }

        // ---- the seamanship gradient (the reason the feature exists) ---------------------------

        [Test]
        public void AtStPeters_TheDefaultScopeRidesTheTideOut_AndASnuggedOneIsLost()
        {
            MooringLineSettings s = MooringLineSettings.Default;
            float dropHigh = DropAt(+TideAmplitude);
            float dropLow = DropAt(-TideAmplitude);

            // Sanity on the region itself: the tide opens the gap by its full range, on top of a pier that
            // already stands well clear of high water. This is the number the defaults are sized against —
            // it is the DROP that matters, not the tidal range alone.
            Assert.AreEqual(2f * TideAmplitude, dropLow - dropHigh, 1e-3f);
            Assert.AreEqual(2.55f, dropHigh, 1e-3f, "≈2.6 m from bollard to cleat at high water");
            Assert.AreEqual(6.95f, dropLow, 1e-3f, "≈7.0 m at low — a line has to cover this first");

            // The DEFAULT scope rides the whole ebb: never hung, never lost.
            float start = s.DefaultScopeMetres;
            Assert.Greater(MooringLineMath.HorizontalReach(start, dropLow), 0f,
                           "the starting scope must not leave her HANGING at dead low water");
            Assert.IsFalse(MooringLineMath.Slips(SpanAtRest(start, dropLow), start, s.WorkingLoadFactor),
                           "…nor lose her");
            Assert.Greater(MooringLineMath.HorizontalReach(start, dropHigh),
                           MooringLineMath.HorizontalReach(start, dropLow),
                           "…while still visibly drawing her in as the water goes — that is the tell");

            // Snug her up at high water and the ebb collects on it. THIS is the lesson, and the player's
            // own choice sets it up: at high water 4 m looks perfectly seamanlike.
            const float snug = 4f;
            Assert.Greater(MooringLineMath.HorizontalReach(snug, dropHigh), 0f,
                           "4 m is fine at high water — the mistake is invisible when it is made");
            Assert.IsFalse(MooringLineMath.Slips(SpanAtRest(snug, dropHigh), snug, s.WorkingLoadFactor));
            Assert.IsTrue(MooringLineMath.Slips(SpanAtRest(snug, dropLow), snug, s.WorkingLoadFactor),
                          "…and the falling tide is what collects on it");
        }

        /// <summary>The span of a line whose boat sits as far in as the constraint allows: with reach left
        /// she rides at scope, and once she is hanging the span is the drop itself.</summary>
        private static float SpanAtRest(float scope, float drop)
            => drop >= scope ? drop : scope;

        // ---- slip ------------------------------------------------------------------------------

        [Test]
        public void Slips_OnlyPastTheWorkingLoad_SoTouchingTheWaterIsNotAPenalty()
        {
            Assert.IsFalse(MooringLineMath.Slips(6f, 6f, 1.25f), "bar-taut is not a failure");
            Assert.IsFalse(MooringLineMath.Slips(7.4f, 6f, 1.25f), "inside the working load she holds");
            Assert.IsTrue(MooringLineMath.Slips(7.6f, 6f, 1.25f), "past it the loop surrenders");
        }

        [Test]
        public void Slips_TreatsAFactorBelowOneAsOne_SoALineCannotSurrenderWhileSlack()
        {
            Assert.IsFalse(MooringLineMath.Slips(5.9f, 6f, 0f),
                           "a mistuned factor must not make a slack line let go");
        }

        [Test]
        public void Load01_ReadsSlackTautAndOverloaded()
        {
            Assert.AreEqual(0f, MooringLineMath.Load01(0f, 6f), 1e-4f, "sitting on the cleat = all slack");
            Assert.AreEqual(0.5f, MooringLineMath.Load01(3f, 6f), 1e-4f);
            Assert.AreEqual(1f, MooringLineMath.Load01(6f, 6f), 1e-4f, "bar-taut");
            Assert.Greater(MooringLineMath.Load01(9f, 6f), 1f, "overloaded");
            Assert.AreEqual(1f, MooringLineMath.Load01(1f, 0f), 1e-4f, "a line with no length is always taut");
        }

        // ---- span, scope stepping, and the advice ----------------------------------------------

        [Test]
        public void Span_IsTheThreeDimensionalGap_NotTheMapDistance()
        {
            // 3 m apart on the map, 4 m of drop → 5 m of rope needed. Using the map distance alone would
            // under-state what the line has to cover by 2 m, which is the whole bug this guards.
            float span = MooringLineMath.Span(new Vector2(0f, 0f), 0f, new Vector2(3f, 0f), 4f);
            Assert.AreEqual(5f, span, 1e-4f);
        }

        [Test]
        public void CleatHeightAboveWaterline_TakesTheDraughtOff_AndNeverGoesNegative()
        {
            Assert.AreEqual(0.7f, MooringLineMath.CleatHeightAboveWaterline(1f, 0.3f), 1e-4f);
            Assert.AreEqual(0f, MooringLineMath.CleatHeightAboveWaterline(0.2f, 0.9f), 1e-4f,
                            "a fitting modelled below the waterline reads as AT it, never below");
        }

        [Test]
        public void BoatCleatElevation_RidesTheWater()
        {
            Assert.AreEqual(2.8f, MooringLineMath.BoatCleatElevation(2.2f, 0.6f), 1e-4f);
            Assert.AreEqual(-1.6f, MooringLineMath.BoatCleatElevation(-2.2f, 0.6f), 1e-4f);
        }

        [Test]
        public void StepScope_StepsAndClamps()
        {
            Assert.AreEqual(7f, MooringLineMath.StepScope(6f, +1, 1f, 1.5f, 12f), 1e-4f, "slacken");
            Assert.AreEqual(5f, MooringLineMath.StepScope(6f, -1, 1f, 1.5f, 12f), 1e-4f, "tighten");
            Assert.AreEqual(12f, MooringLineMath.StepScope(11.5f, +5, 1f, 1.5f, 12f), 1e-4f, "clamped at max");
            Assert.AreEqual(1.5f, MooringLineMath.StepScope(2f, -9, 1f, 1.5f, 12f), 1e-4f, "clamped at min");
        }

        [Test]
        public void ScopeForFall_IsTheInverseOfHorizontalReach_SoAdviceAndPhysicsAgree()
        {
            // Ask for a line that still has 4 m of room after another 3 m of ebb from a 0 m drop…
            float scope = MooringLineMath.ScopeForFall(0f, 3f, 4f);
            Assert.AreEqual(5f, scope, 1e-4f);
            // …and the reach at that fall is exactly the room asked for. Same algebra, both directions.
            Assert.AreEqual(4f, MooringLineMath.HorizontalReach(scope, 3f), 1e-4f);
        }

        // ---- determinism + NaN safety (rule 5) -------------------------------------------------

        [Test]
        public void TheWholeLaw_IsBitStableForIdenticalInputs()
        {
            for (int i = 0; i < 64; i++)
            {
                float drop = i * 0.1f;
                Assert.AreEqual(MooringLineMath.HorizontalReach(6f, drop),
                                MooringLineMath.HorizontalReach(6f, drop),
                                "no hidden state, no RNG — the same drop always leaves the same reach");
            }

            Vector2 a = new Vector2(1.25f, -3.5f), b = new Vector2(-4f, 2.75f);
            Assert.AreEqual(MooringLineMath.Span(a, 1.1f, b, 5.35f), MooringLineMath.Span(a, 1.1f, b, 5.35f));
        }

        [Test]
        public void NaNIn_ReadsAsTheNeutralValue_NeverPropagates()
        {
            float nan = float.NaN;
            Assert.IsFalse(float.IsNaN(MooringLineMath.HorizontalReach(nan, 2f)));
            Assert.IsFalse(float.IsNaN(MooringLineMath.HorizontalReach(6f, nan)));
            Assert.IsFalse(float.IsNaN(MooringLineMath.VerticalDrop(nan, nan)));
            Assert.IsFalse(float.IsNaN(MooringLineMath.Load01(nan, 6f)));
            Assert.IsFalse(float.IsNaN(MooringLineMath.StepScope(nan, 1, 1f, 1.5f, 12f)));
            Assert.IsFalse(float.IsNaN(MooringLineMath.Span(new Vector2(nan, 0f), nan, Vector2.zero, 0f)));
            Assert.IsFalse(float.IsNaN(MooringLineMath.CleatHeightAboveWaterline(nan, nan)));
        }

        // ---- the settings the law is tuned by --------------------------------------------------

        [Test]
        public void DefaultSettings_LeaveScopeAsARealDecision()
        {
            MooringLineSettings s = MooringLineSettings.Default;

            Assert.Greater(s.MaxScopeMetres, s.MinScopeMetres, "there must be a range to choose within");
            Assert.GreaterOrEqual(s.DefaultScopeMetres, s.MinScopeMetres);
            Assert.LessOrEqual(s.DefaultScopeMetres, s.MaxScopeMetres);
            Assert.Greater(s.WorkingLoadFactor, 1f,
                           "a loop that surrenders the instant the line comes taut makes every moor a coin-toss");
            Assert.Greater(s.SlipGraceSeconds, 0f, "one snatching wave must not cost the boat");

            // THE dial, graded against the region's REAL drop rather than its tidal range: an ordinary
            // ebb must be able to exhaust a short line, and the longest line must be able to ride it out.
            // If both of these ever stop being true the scope choice has stopped mattering.
            float dropLow = DropAt(-TideAmplitude);
            Assert.Less(s.MinScopeMetres * s.WorkingLoadFactor, dropLow,
                        "the shortest line must be losable to an ordinary tide");
            Assert.Greater(s.MaxScopeMetres, dropLow,
                           "…and the longest must still have reach across the water at dead low");
        }
    }
}
