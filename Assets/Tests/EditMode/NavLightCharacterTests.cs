using System;
using System.Collections.Generic;
using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE LIGHT CHARACTERS — does a mark flash what the chart says she flashes?</b>
    ///
    /// <para>Everything here is pure arithmetic on <see cref="NavLightCharacter"/>: no scene, no
    /// clock, no GPU. That is the point of putting the rhythm in Core — the question "is the south
    /// cardinal lit 8.4 seconds into her period" has an answer that can be checked without running
    /// a frame, and it is the same answer on every machine forever.</para>
    ///
    /// <para><b>The load-bearing test is <see cref="EveryCharacterPrefersItsOwnPeriod"/>.</b> Pinning
    /// on-fractions proves a character is lit for the right TOTAL; it does not prove the light has a
    /// period at all — a light that came on for a random eighth of every second would pass an
    /// on-fraction check. So each character is folded at its published period AND at a decoy period
    /// (T·√2, incommensurate with anything), and it must agree with itself exactly at the first and
    /// visibly disagree at the second. A rhythm has a favourite period; a wander does not.</para>
    /// </summary>
    public class NavLightCharacterTests
    {
        // The six distinct characters the ten shipped marks wear, as the defs write them.
        private const string North    = "Q";                 // CardinalN
        private const string East     = "Q(3) 10s";          // CardinalE
        private const string South    = "Q(6) + LFl 15s";    // CardinalS
        private const string West     = "Q(9) 15s";          // CardinalW
        private const string Isolated = "Fl(2) W 5s";        // Isolated danger
        private const string PortHand = "Fl G 4s";           // PortCan / PortLit
        private const string StbdHand = "Fl R 4s";           // StbdNun / StbdLit

        private static NavLightCharacter Parsed(string text)
        {
            Assert.That(NavLightCharacter.TryParse(text, out NavLightCharacter c, out string error),
                        Is.True, $"'{text}' did not parse: {error}");
            return c;
        }

        // =============================================================================================
        //  1. THE SHAPE OF EACH CHARACTER — period, group, colour, and how long the lamp is lit
        // =============================================================================================

        /// <summary>
        /// Every one of the seven strings the kit ships, pinned field by field. A change to any of
        /// these numbers is a change to what a mark MEANS, so it has to be deliberate.
        /// </summary>
        [Test]
        public void EveryShippedCharacterParsesToItsPublishedShape()
        {
            // text, period, group, flashes in a period, seconds lit, colour
            var expected = new (string Text, float Period, int Group, int Flashes, float On, NavLightColour Colour)[]
            {
                (North,    1f,  1, 1, 0.5f, NavLightColour.White),
                (East,    10f,  3, 3, 1.5f, NavLightColour.White),
                (South,   15f,  6, 7, 5.0f, NavLightColour.White),   // six quicks (3.0 s) + one long (2.0 s)
                (West,    15f,  9, 9, 4.5f, NavLightColour.White),
                (Isolated, 5f,  2, 2, 2.0f, NavLightColour.White),
                (PortHand, 4f,  1, 1, 1.0f, NavLightColour.Green),
                (StbdHand, 4f,  1, 1, 1.0f, NavLightColour.Red),
            };

            foreach (var e in expected)
            {
                NavLightCharacter c = Parsed(e.Text);
                Assert.That(c.IsLit, Is.True, $"'{e.Text}' parsed but is not lit");
                Assert.That(c.PeriodSeconds, Is.EqualTo(e.Period).Within(1e-4f), $"'{e.Text}' period");
                Assert.That(c.GroupCount, Is.EqualTo(e.Group), $"'{e.Text}' group count");
                Assert.That(c.FlashCount, Is.EqualTo(e.Flashes), $"'{e.Text}' flashes in a period");
                Assert.That(c.OnSeconds, Is.EqualTo(e.On).Within(1e-4f), $"'{e.Text}' seconds lit");
                Assert.That(c.Colour, Is.EqualTo(e.Colour), $"'{e.Text}' colour");
            }
        }

        /// <summary>
        /// ⭐ The south cardinal is the one composite in the kit and the only mark whose identity
        /// depends on TWO rhythms in sequence — six quick flashes and then one long one, which is
        /// how a skipper tells her from the west cardinal's nine. Pinned flash by flash, because
        /// "six quicks then a long" collapsing into "seven quicks" is a defect that keeps the right
        /// count and the right period and still points a boat at the wrong side of a shoal.
        /// </summary>
        [Test]
        public void TheSouthCardinalIsSixQuickFlashesAndThenALongOne()
        {
            NavLightCharacter c = Parsed(South);

            for (int i = 0; i < 6; i++)
            {
                Assert.That(c.OnsetOf(i), Is.EqualTo(i * 1.0f).Within(1e-4f), $"quick {i} onset");
                Assert.That(c.DurationOf(i), Is.EqualTo(NavLightCharacter.QuickFlashSeconds).Within(1e-4f),
                            $"quick {i} is not a QUICK flash");
            }

            Assert.That(c.OnsetOf(6), Is.EqualTo(6f).Within(1e-4f),
                        "the long flash does not follow the six quicks");
            Assert.That(c.DurationOf(6), Is.EqualTo(NavLightCharacter.LongFlashSeconds).Within(1e-4f),
                        "the seventh flash is not LONG — a south cardinal that shows seven quicks " +
                        "reads as a west cardinal's nine cut short, which is the opposite side of the danger.");
        }

        /// <summary>
        /// The on/off picture over one full period, sampled at 10 ms, against the schedule stated
        /// independently here. This is the test that would catch an off-by-one in the fold.
        /// </summary>
        [Test]
        public void TheSequenceOverOnePeriodMatchesTheCharter()
        {
            AssertWindows(North,    1f,  new[] { (0f, 0.5f) });
            AssertWindows(East,    10f,  new[] { (0f, 0.5f), (1f, 1.5f), (2f, 2.5f) });
            AssertWindows(West,    15f,  new[] { (0f, 0.5f), (1f, 1.5f), (2f, 2.5f), (3f, 3.5f), (4f, 4.5f),
                                                 (5f, 5.5f), (6f, 6.5f), (7f, 7.5f), (8f, 8.5f) });
            AssertWindows(South,   15f,  new[] { (0f, 0.5f), (1f, 1.5f), (2f, 2.5f), (3f, 3.5f), (4f, 4.5f),
                                                 (5f, 5.5f), (6f, 8f) });
            AssertWindows(Isolated, 5f,  new[] { (0f, 1f), (2f, 3f) });
            AssertWindows(PortHand, 4f,  new[] { (0f, 1f) });
        }

        private static void AssertWindows(string text, float period, (float From, float To)[] lit)
        {
            NavLightCharacter c = Parsed(text);
            // ⚠️ Sample the MIDDLE of each 10 ms cell, never its edge. Every flash boundary in the
            // kit lands on a round hundredth, and a sample sitting exactly on one is decided by the
            // last bit of a fold that adds and subtracts numbers in the tens of millions — the same
            // instant can read lit on one side of an identity and dark on the other. 5 ms of margin
            // is a hundred million times the error and costs nothing.
            const double step = 0.01d;
            for (double t = step * 0.5d; t < period; t += step)
            {
                bool want = false;
                foreach (var w in lit) if (t >= w.From && t < w.To) { want = true; break; }
                Assert.That(c.IsOn(t, 0d), Is.EqualTo(want),
                            $"'{text}' at t={t:0.00}s: expected {(want ? "LIT" : "dark")}");
            }
        }

        // =============================================================================================
        //  2. IT IS A CLOCK — periodic, pure, and it prefers its OWN period
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>The control that makes the rest of this file mean something.</b>
        ///
        /// <para>An on-fraction says how MUCH a light burns, never how it is arranged: a lamp that
        /// came on for a random eighth of every second would pass every pin above. So fold the same
        /// samples twice — at the published period, where a real character must agree with itself
        /// EXACTLY, and at a decoy period of T·√2, which is incommensurate with the period and every
        /// harmonic of it. A rhythm prefers its own period. A wander has no favourite.</para>
        ///
        /// <para>The decoy arm is the half that can fail for the right reason: it is what proves the
        /// agreement at T is a property of the light and not of a fold that agrees with everything.</para>
        /// </summary>
        [Test]
        public void EveryCharacterPrefersItsOwnPeriod()
        {
            foreach (string text in new[] { North, East, South, West, Isolated, PortHand })
            {
                NavLightCharacter c = Parsed(text);
                double period = c.PeriodSeconds;
                double decoy = period * Math.Sqrt(2d);

                int atPeriod = 0, atDecoy = 0, samples = 0;
                for (double t = 0.005d; t < period * 4d; t += 0.01d)   // off the boundaries — see AssertWindows
                {
                    bool now = c.IsOn(t, 0d);
                    if (now != c.IsOn(t + period, 0d)) atPeriod++;
                    if (now != c.IsOn(t + decoy, 0d)) atDecoy++;
                    samples++;
                }

                Assert.That(atPeriod, Is.Zero,
                            $"'{text}' disagreed with itself one period later {atPeriod} times — " +
                            "it is not periodic on the period it publishes.");
                Assert.That(atDecoy, Is.GreaterThan(samples / 20),
                            $"'{text}' agreed with itself at the DECOY period (T*sqrt2) almost " +
                            $"everywhere ({atDecoy}/{samples} disagreements). Either the light is on " +
                            "nearly all the time or the fold agrees with everything — in both cases " +
                            "the agreement at T above proves nothing.");
            }
        }

        /// <summary>
        /// Same seconds in, same answer out — a thousand times, in a shuffled order, with the phase
        /// in play. Rule 5: nothing here accumulates and nothing is random.
        /// </summary>
        [Test]
        public void IsOnIsPureInTheClock()
        {
            NavLightCharacter c = Parsed(South);
            var rng = new Random(20260903);   // the ORDER of the probes is random; the answers are not
            var seen = new Dictionary<double, bool>();

            for (int i = 0; i < 1000; i++)
            {
                double t = Math.Round(rng.NextDouble() * 60d, 3);
                bool on = c.IsOn(t, 3.25d);
                if (seen.TryGetValue(t, out bool before))
                    Assert.That(on, Is.EqualTo(before), $"the light changed its mind about t={t}");
                else
                    seen[t] = on;
            }

            // And a second, independently parsed copy agrees with the first everywhere.
            NavLightCharacter twin = Parsed(South);
            foreach (var kv in seen)
                Assert.That(twin.IsOn(kv.Key, 3.25d), Is.EqualTo(kv.Value),
                            $"two parses of the same text disagree at t={kv.Key}");
        }

        /// <summary>
        /// ⭐ A season of game time is tens of millions of seconds, and a <c>float</c> there has a
        /// resolution of a couple of seconds — a half-second flash simply stops existing. The fold
        /// is done in <c>double</c> for exactly this reason, so the mark must still be flashing
        /// after a year.
        /// </summary>
        [Test]
        public void AMarkStillFlashesAfterAYearOfGameTime()
        {
            NavLightCharacter c = Parsed(PortHand);
            const double aYear = 365d * 24d * 3600d;   // ~31.5 million in-game seconds

            int lit = 0, samples = 0;
            for (double t = aYear; t < aYear + c.PeriodSeconds * 3d; t += 0.05d)
            {
                if (c.IsOn(t, 0d)) lit++;
                samples++;
            }

            double fraction = lit / (double)samples;
            Assert.That(fraction, Is.EqualTo(0.25d).Within(0.03d),
                        $"after a year the port hand is lit {fraction:P1} of the time instead of 25% " +
                        "(1 s in 4). A float fold would have quantised the flash away entirely.");
        }

        /// <summary>A time before the start of the game folds like any other — <c>%</c> keeps the sign
        /// of the dividend and would have read every negative instant as dark.</summary>
        [Test]
        public void ANegativeTimeFoldsIntoThePeriod()
        {
            NavLightCharacter c = Parsed(PortHand);
            for (double t = 0.025d; t < c.PeriodSeconds; t += 0.05d)   // off the boundaries
                Assert.That(c.IsOn(t - 100d * c.PeriodSeconds, 0d), Is.EqualTo(c.IsOn(t, 0d)),
                            $"the fold does not handle negative time at t={t}");
        }

        // =============================================================================================
        //  3. PHASE — a mark's own, and stable forever
        // =============================================================================================

        /// <summary>
        /// ⭐ The seed is FNV-1a and not <c>string.GetHashCode()</c>, and these two published test
        /// vectors are what proves it. <c>GetHashCode</c> is documented as unstable across runtimes
        /// and is randomised per process on some of them: a phase built on it would re-shuffle every
        /// mark in the harbour on an engine upgrade, and a test that pinned one would pass here and
        /// fail on the next machine.
        /// </summary>
        [Test]
        public void TheSeedIsRealFnv1aAndSoIsStableForever()
        {
            Assert.That(NavLightCharacter.SeedFromId("a"), Is.EqualTo(-468965076),
                        "FNV-1a(\"a\") should be 0xE40C292C");
            Assert.That(NavLightCharacter.SeedFromId("foobar"), Is.EqualTo(-1080231576),
                        "FNV-1a(\"foobar\") should be 0xBF9CF968");
            Assert.That(NavLightCharacter.SeedFromId(""), Is.Zero, "an unnamed mark seeds at zero");
        }

        /// <summary>A phase lands inside the period, and the same id always lands on the same one.</summary>
        [Test]
        public void APhaseIsInsideThePeriodAndRepeatable()
        {
            NavLightCharacter c = Parsed(East);
            foreach (string id in new[] { "channel.nmc_entrance.p0", "channel.nmc_entrance.s2",
                                          "mark.nmc_breakwater_head", "channel.sp_approach.p1" })
            {
                float phase = c.PhaseFromSeed(NavLightCharacter.SeedFromId(id));
                Assert.That(phase, Is.GreaterThanOrEqualTo(0f).And.LessThan(c.PeriodSeconds),
                            $"'{id}' phased outside its own period");
                Assert.That(c.PhaseFromSeed(NavLightCharacter.SeedFromId(id)), Is.EqualTo(phase),
                            $"'{id}' phased differently the second time it was asked");
            }
        }

        // =============================================================================================
        //  4. WHAT IT REFUSES — loudly, because a light that never lights looks like a broken one
        // =============================================================================================

        /// <summary>An unlit mark is not an error; it is a mark with no lamp, and it says nothing.</summary>
        [Test]
        public void AnUnlitMarkParsesToNothingAndNeverLights()
        {
            foreach (string text in new[] { null, "", "   " })
            {
                Assert.That(NavLightCharacter.TryParse(text, out NavLightCharacter c, out string error),
                            Is.False, $"'{text ?? "null"}' should not yield a character");
                Assert.That(error, Is.Empty, "an unlit mark is not a malformed one");
                Assert.That(c.IsLit, Is.False);
                Assert.That(c.IsOn(0d, 0d), Is.False, "an unlit mark lit up");
                Assert.That(c.IsOn(12345.678d, 9d), Is.False, "an unlit mark lit up later on");
            }
        }

        /// <summary>
        /// Anything it does not fully understand is REFUSED with a reason. A parser that shrugged and
        /// returned a dark light would put an unlit mark in a channel and look exactly like a mark
        /// whose lamp had failed.
        /// </summary>
        [Test]
        public void GarbageIsRefusedWithAReason()
        {
            foreach (string text in new[] { "Mo(A) 6s", "Alt WR 4s", "Fl(0) 4s", "Q(9) 5s", "Fl 4s 6s" })
            {
                Assert.That(NavLightCharacter.TryParse(text, out _, out string error), Is.False,
                            $"'{text}' should have been refused");
                Assert.That(error, Is.Not.Empty, $"'{text}' was refused without saying why");
            }
        }

        /// <summary>
        /// ⭐ The refusal that is a DATA check, not a syntax one: nine quick flashes need nine
        /// seconds and cannot be shown on a five-second period. Caught here rather than drawn as a
        /// light that is on more than it is off.
        /// </summary>
        [Test]
        public void AGroupThatDoesNotFitItsPeriodIsRefused()
        {
            Assert.That(NavLightCharacter.TryParse("Q(9) 5s", out _, out string error), Is.False);
            Assert.That(error, Does.Contain("period"),
                        "the refusal should name the period it could not fit the flashes into");

            // …and the same group on a period that DOES hold it is fine.
            Assert.That(NavLightCharacter.TryParse("Q(9) 15s", out _, out _), Is.True);
        }

        /// <summary>
        /// <c>LFl</c> is read before <c>Fl</c> and before <c>F</c>. Shortest-token-first would turn a
        /// long flash into a fixed light followed by a flash, and the south cardinal would lose the
        /// one feature that identifies her.
        /// </summary>
        [Test]
        public void LongFlashIsNotReadAsAFixedLightAndAFlash()
        {
            NavLightCharacter c = Parsed("LFl 10s");
            Assert.That(c.Rhythm, Is.EqualTo(NavLightRhythm.LongFlash));
            Assert.That(c.FlashCount, Is.EqualTo(1), "'LFl' was split into two flashes");
            Assert.That(c.OnSeconds, Is.EqualTo(NavLightCharacter.LongFlashSeconds).Within(1e-4f));
        }

        /// <summary>A fixed light has no dark part — the one rhythm with no cycle to lay out.</summary>
        [Test]
        public void AFixedLightNeverGoesOut()
        {
            NavLightCharacter c = Parsed("F R");
            Assert.That(c.Colour, Is.EqualTo(NavLightColour.Red));
            for (double t = 0d; t < 20d; t += 0.37d)
                Assert.That(c.IsOn(t, 0d), Is.True, $"a fixed light went out at t={t}");
        }

        // =============================================================================================
        //  5. THE PHASE PLAN — sharing the period out, so no two of one character wink together
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>The property the whole plan exists for: the answer does not depend on the order the
        /// marks arrive in.</b>
        ///
        /// <para>This is the trap the lamp lane already paid for once — a light seeded off
        /// <c>transform.GetSiblingIndex()</c>, so that adding one child re-seeded every neighbour.
        /// The slots here are handed out in sorted-id order precisely so that shuffling the input
        /// cannot move a single mark, and the arm that proves it is the shuffle.</para>
        /// </summary>
        [Test]
        public void TheSpreadDoesNotCareWhatOrderTheMarksArriveIn()
        {
            var marks = new List<(string, string)>
            {
                ("channel.nmc_entrance.p0", PortHand),
                ("channel.nmc_entrance.s1", StbdHand),
                ("channel.nmc_entrance.p2", PortHand),
                ("channel.nmc_bar_gut.p1",  PortHand),
                ("mark.nmc_shoal_northeast", North),
                ("channel.nmc_bar_gut.s0",  StbdHand),
            };

            Dictionary<string, float> straight = NavLightPhasePlan.Spread(marks);

            var rng = new Random(4242);
            for (int trial = 0; trial < 8; trial++)
            {
                var shuffled = new List<(string, string)>(marks);
                for (int i = shuffled.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
                }

                Dictionary<string, float> got = NavLightPhasePlan.Spread(shuffled);
                Assert.That(got.Count, Is.EqualTo(straight.Count));
                foreach (KeyValuePair<string, float> kv in straight)
                    Assert.That(got[kv.Key], Is.EqualTo(kv.Value),
                                $"'{kv.Key}' moved when the marks were handed over in another order — " +
                                "the spread has picked up a dependence on arrival order.");
            }
        }

        /// <summary>
        /// The gap the plan promises is the gap it delivers, at every group size that could stand in
        /// a harbour. Swept rather than spot-checked, because the jitter is what could break it and
        /// the jitter differs per id.
        /// </summary>
        [Test]
        public void TheSpreadDeliversItsPromisedGapAtEveryGroupSize()
        {
            for (int count = 2; count <= 16; count++)
            {
                var marks = new List<(string, string)>();
                for (int i = 0; i < count; i++) marks.Add(($"channel.test.p{i}", PortHand));

                Dictionary<string, float> spread = NavLightPhasePlan.Spread(marks);
                Assert.That(spread.Count, Is.EqualTo(count));

                var f = new List<float>(spread.Values);
                float worst = SmallestGap(f);
                float promised = NavLightPhasePlan.GuaranteedGapFraction(count);

                Assert.That(worst, Is.GreaterThanOrEqualTo(promised - 1e-5f),
                            $"{count} marks of one character: closest pair {worst:0.####} of the " +
                            $"period, but the plan guarantees {promised:0.####}.");
                Assert.That(worst, Is.LessThan(1f / count + 1e-5f),
                            $"{count} marks cannot all be further apart than an even share");
            }
        }

        /// <summary>
        /// ⭐ The measurement that sent the hash away. Six port-hand cans phased by an INDEPENDENT
        /// hash of each id land where chance puts them, and chance puts two of them close — that is
        /// how the real Nine Mile Creek pair ended up 0.021 s apart on a four-second period. This
        /// arm shows the plan beats the hash on those very ids, which is a comparison that stays
        /// meaningful, rather than an absolute number that would rot the first time an id changed.
        /// </summary>
        [Test]
        public void TheSpreadBeatsAnIndependentHashOnTheMarksThatCaughtIt()
        {
            string[] ids =
            {
                "channel.nmc_entrance.p0", "channel.nmc_entrance.p1", "channel.nmc_entrance.p2",
                "channel.nmc_bar_gut.p0",  "channel.nmc_bar_gut.p1",  "channel.nmc_bar_gut.p2",
            };
            NavLightCharacter c = Parsed(PortHand);

            var hashed = new List<float>();
            var marks = new List<(string, string)>();
            foreach (string id in ids)
            {
                hashed.Add(c.PhaseFromSeed(NavLightCharacter.SeedFromId(id)) / c.PeriodSeconds);
                marks.Add((id, PortHand));
            }

            var planned = new List<float>(NavLightPhasePlan.Spread(marks).Values);

            float hashGap = SmallestGap(hashed);
            float planGap = SmallestGap(planned);

            Assert.That(planGap, Is.GreaterThan(hashGap),
                        $"the plan ({planGap:0.####} of the period) is no better than an independent " +
                        $"hash ({hashGap:0.####}) on the very marks that exposed the problem.");
            Assert.That(planGap, Is.GreaterThan(0.1f),
                        "the plan's own gap is too small to read as two separate lights");
        }

        /// <summary>The smallest gap between neighbours on the circle, as a fraction of the period.</summary>
        private static float SmallestGap(List<float> fractions)
        {
            var f = new List<float>(fractions);
            f.Sort();
            float worst = 1f;
            for (int i = 0; i < f.Count; i++)
            {
                float next = i + 1 < f.Count ? f[i + 1] : f[0] + 1f;   // wrap
                worst = Math.Min(worst, next - f[i]);
            }
            return worst;
        }

        /// <summary>An unlit mark is given no slot at all — she has no period to sit in.</summary>
        [Test]
        public void AnUnlitMarkGetsNoSlot()
        {
            Dictionary<string, float> spread = NavLightPhasePlan.Spread(new[]
            {
                ("buoy.mooring_a", ""),
                ("buoy.mooring_b", (string)null),
                ("channel.test.p0", PortHand),
            });

            Assert.That(spread.ContainsKey("buoy.mooring_a"), Is.False);
            Assert.That(spread.ContainsKey("buoy.mooring_b"), Is.False);
            Assert.That(spread.ContainsKey("channel.test.p0"), Is.True);
            Assert.That(NavLightPhasePlan.Spread(null), Is.Empty);
        }
    }
}
