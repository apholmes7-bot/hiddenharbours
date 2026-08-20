using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE BURN MODEL — <c>docs/design/fuel-and-refuelling.md</c> §9.5, and its acceptance sheet §9.12.
    ///
    /// <para>Four kinds of guard, and each exists because of a specific way this could go wrong quietly:</para>
    /// <list type="number">
    /// <item><description><b>The curve behaves.</b> Burn rises with throttle, with the catch aboard and
    /// with the sea, and never falls. A sign slip anywhere in the formula would make easing off cost MORE,
    /// which no playtest would name correctly.</description></item>
    /// <item><description><b>The arithmetic is pinned to numbers a human has read.</b> §9.7 and §9.8's
    /// worked tables are reproduced here to the digit, so a re-tune shows up as a diff in the doc's own
    /// figures rather than as silent drift in a boat's range.</description></item>
    /// <item><description><b>Determinism (rule 5), three ways.</b> Bit-identical on a repeat, independent
    /// of the fixed-step size, and — the one that has teeth — independent of the CLOCK.</description></item>
    /// <item><description><b>The shipped asset actually carries the tuning.</b>
    /// <c>GameConfigAssetCoverageTests</c> proves the KEYS are present; it cannot tell 1 from 0. A
    /// <c>BurnScale</c> of 0 in the YAML is a green coverage test and a game where fuel is free.</description></item>
    /// </list>
    /// </summary>
    public class FuelBurnTests
    {
        /// <summary>The shipped curve, as authored. Every test that is not about tuning uses this.</summary>
        private static FuelSettings Cfg => FuelSettings.Default;

        /// <summary>
        /// The shipped curve with slip switched ON at its proposed weight. §9.7/§9.8's worked examples
        /// carry a slip column, and the fleet ships with <see cref="FuelSettings.SlipLoadWeight"/> at 0
        /// until a MEASURED top speed exists to compute slip from (§9.5.2). So the doc's tables are the
        /// FORMULA's numbers, pinned here at the weight they were computed for; the shipped game reads
        /// the same rows with the slip column at 0.
        /// </summary>
        private static FuelSettings CfgWithSlip
        {
            get { var c = FuelSettings.Default; c.SlipLoadWeight = 0.5f; return c; }
        }

        /// <summary>The continuous sea axis at a named band. Equals <c>(int)state / 7</c> exactly at every
        /// band edge — <c>WeatherModel.SeaState01</c> says so and is built to keep the two agreeing.</summary>
        private static float Sea(SeaState s) => (int)s / 7f;

        private const float DoryR = 23f;         // boat.dory_outboard, §9.6.2
        private const float LobsterR = 105f;     // boat.lobster_boat, §9.6.2

        // ---- 1. the curve behaves -------------------------------------------------------------

        [Test]
        public void Burn_IsNonDecreasing_InThrottle()
        {
            float previous = -1f;
            for (int i = 0; i <= 40; i++)
            {
                float throttle = i / 40f;
                float rate = FuelBurn.LitresPerHour(DoryR, throttle, 0.5f, 0f, Sea(SeaState.Moderate), Cfg);
                Assert.GreaterOrEqual(rate, previous,
                    $"burn fell as throttle rose (at {throttle:0.###}) — opening the throttle must never " +
                    "cost less fuel per hour than easing it back.");
                previous = rate;
            }
        }

        [Test]
        public void Burn_IsNonDecreasing_InHoldFraction()
        {
            float previous = -1f;
            for (int i = 0; i <= 20; i++)
            {
                float hold = i / 20f;
                float rate = FuelBurn.LitresPerHour(DoryR, 0.7f, hold, 0f, Sea(SeaState.Moderate), Cfg);
                Assert.GreaterOrEqual(rate, previous, $"burn fell as the hold filled (at {hold:0.##})");
                previous = rate;
            }
        }

        [Test]
        public void Burn_IsNonDecreasing_InSeaState()
        {
            float previous = -1f;
            for (int i = 0; i <= 28; i++)
            {
                float sea = i / 28f;
                float rate = FuelBurn.LitresPerHour(DoryR, 0.7f, 0.5f, 0f, sea, Cfg);
                Assert.GreaterOrEqual(rate, previous, $"burn fell as the sea got up (at SeaState01 {sea:0.###})");
                previous = rate;
            }
        }

        [Test]
        public void Burn_IsNonDecreasing_InSlip_WhenSlipIsWeighted()
        {
            // With the shipped weight of 0 this axis is deliberately FLAT; the monotonicity that matters
            // is the one the model will have when the measured top speed lands, so test at that weight.
            float previous = -1f;
            for (int i = 0; i <= 20; i++)
            {
                float slip = i / 20f;
                float rate = FuelBurn.LitresPerHour(DoryR, 0.7f, 0.5f, slip, Sea(SeaState.Moderate), CfgWithSlip);
                Assert.GreaterOrEqual(rate, previous, $"burn fell as she slipped worse (at {slip:0.##})");
                previous = rate;
            }
        }

        [Test]
        public void AsternBurnsTheSameAsAhead_AtTheSameNotch()
        {
            // |throttle|: backing down burns fuel too, and astern is weaker thrust for the same gulp.
            float ahead = FuelBurn.LitresPerHour(DoryR, 0.6f, 0.25f, 0f, Sea(SeaState.Calm), Cfg);
            float astern = FuelBurn.LitresPerHour(DoryR, -0.6f, 0.25f, 0f, Sea(SeaState.Calm), Cfg);
            Assert.AreEqual(ahead, astern, "the model reads |throttle|, so astern costs what ahead costs");
        }

        [Test]
        public void AHullWithNoTank_BurnsNothing_AtAnyDuty()
        {
            // ⭐ The standing rule: the playable build must run with ZERO hulls authored. R = 0 is what
            // every asset written before the field existed deserializes to, and it must be inert.
            Assert.AreEqual(0f, FuelBurn.LitresPerHour(0f, 1f, 1f, 1f, 1f, Cfg));
            Assert.AreEqual(0f, FuelBurn.LitresBurnedOverTick(0f, 1f, 1f, 1f, 1f, 60f, Cfg));
        }

        [Test]
        public void IdleWithTheEngineRunning_CostsTheIdleFraction_AndNotZero()
        {
            float idle = FuelBurn.LitresPerHour(DoryR, 0f, 0f, 0f, 0f, Cfg);
            Assert.AreEqual(DoryR * Cfg.IdleFraction, idle, 1e-4f,
                "ticking over is not free — that is the whole reason IdleFraction exists");
            Assert.Greater(idle, 0f);
        }

        [Test]
        public void TheThrottleCurve_ReproducesTheOwnersTable()
        {
            // §9.6.1's "fraction of full-throttle burn" row, which is what the owner actually reads when
            // deciding whether easing off should buy more or less range.
            var expected = new (float throttle, float fraction)[]
            {
                (0f, 0.08f), (0.25f, 0.14f), (0.5f, 0.31f), (0.7f, 0.53f), (0.85f, 0.75f), (1f, 1.00f),
            };
            float flatOut = FuelBurn.LitresPerHour(DoryR, 1f, 0f, 0f, 0f, Cfg);
            foreach (var (throttle, fraction) in expected)
            {
                float got = FuelBurn.LitresPerHour(DoryR, throttle, 0f, 0f, 0f, Cfg) / flatOut;
                Assert.AreEqual(fraction, got, 0.005f, $"throttle {throttle:0.##} is off the owner's table");
            }
        }

        [Test]
        public void TheSeaCurve_ReproducesTheOwnersTable()
        {
            var expected = new (SeaState sea, float term)[]
            {
                (SeaState.Glass, 1.00f), (SeaState.Calm, 1.09f), (SeaState.Light, 1.17f),
                (SeaState.Moderate, 1.26f), (SeaState.Lively, 1.34f), (SeaState.Rough, 1.43f),
                (SeaState.Gale, 1.51f), (SeaState.Storm, 1.60f),
            };
            float glass = FuelBurn.LitresPerHour(DoryR, 0.7f, 0f, 0f, 0f, Cfg);
            foreach (var (sea, term) in expected)
            {
                float got = FuelBurn.LitresPerHour(DoryR, 0.7f, 0f, 0f, Sea(sea), Cfg) / glass;
                Assert.AreEqual(term, got, 0.005f, $"{sea} is off the owner's sea table");
            }
        }

        [Test]
        public void EasingOffToHalfThrottle_BuysAboutSixtyPercentMoreRange()
        {
            // ⭐ THE LESSON THE FUEL GAUGE TEACHES, asserted rather than hoped for: half throttle is half
            // the speed for 31% of the fuel, so litres-per-kilometre falls by ~38% and range rises ~60%.
            // If a re-tune of ThrottleExponent ever flattens this, the model has stopped saying anything
            // and a player who eases off is just slower.
            float full = FuelBurn.LitresPerHour(DoryR, 1f, 0f, 0f, 0f, Cfg);
            float half = FuelBurn.LitresPerHour(DoryR, 0.5f, 0f, 0f, 0f, Cfg);

            // Litres per unit distance scales as rate / speed, and speed is taken as proportional to
            // throttle (the §9.6.1 table's second row is exactly that assumption).
            float rangeGain = (full / 1f) / (half / 0.5f);
            Assert.Greater(rangeGain, 1.5f, "easing off must buy meaningfully more range");
            Assert.AreEqual(1.61f, rangeGain, 0.02f, "the ~60% figure the doc teaches has moved");
        }

        // ---- 2. the worked examples, to the digit ----------------------------------------------

        /// <summary>
        /// §9.7 — the dory outboard, the four duties in the doc's own table. Pinned so a re-tune is a diff
        /// in numbers a human has read, not a silent change to how far the first boat in the game can go.
        /// </summary>
        [Test]
        public void WorkedExample_TheDoryOutboard_MatchesTheDoc()
        {
            AssertRate("A ambling out, empty, calm", 7.7f,
                DoryR, 0.5f, 0f, 0f, SeaState.Calm);
            AssertRate("B cruise, half hold, moderate", 16.7f,
                DoryR, 0.7f, 0.5f, 0f, SeaState.Moderate);
            AssertRate("C flat out, laden, punching home", 38.5f,
                DoryR, 1.0f, 1.0f, 0.40f, SeaState.Lively);
            AssertRate("D ticking over while you hand-line", 2.5f,
                DoryR, 0f, 0.5f, 0f, SeaState.Moderate);
        }

        /// <summary>§9.8 — a lobster boat, the four duties in the doc's own table.</summary>
        [Test]
        public void WorkedExample_TheLobsterBoat_MatchesTheDoc()
        {
            AssertRate("A steaming out light", 91.6f,
                LobsterR, 0.85f, 0f, 0f, SeaState.Light);
            AssertRate("B cruise, half hold, moderate", 76.2f,
                LobsterR, 0.7f, 0.5f, 0f, SeaState.Moderate);
            AssertRate("C working along the string", 24.5f,
                LobsterR, 0.3f, 0.6f, 0.20f, SeaState.Moderate);
            AssertRate("D flat out, full, in a gale", 199.3f,
                LobsterR, 1.0f, 1.0f, 0.45f, SeaState.Gale);
        }

        [Test]
        public void TheReferenceCruiseDuty_IsTheDocsPoint726Multiplier()
        {
            // Every "Cruise (L/h)" figure in §9.6.2's 38-hull table is R × this. If the multiplier moves,
            // the entire ladder of endurance and range in that table moves with it.
            float multiplier = FuelBurn.LitresPerHour(1f, 0.7f, 0.5f, 0f, Sea(SeaState.Moderate), Cfg);
            Assert.AreEqual(0.726f, multiplier, 0.001f,
                "the reference cruise duty (throttle 0.7, half hold, Moderate sea, no slip) has moved — " +
                "§9.6.2's Cruise/Endurance/Range/Days-per-tank columns are all derived from it");
        }

        /// <summary>
        /// §9.7's reference fishing day — out to the shoal, hand-line, home laden — which the doc totals
        /// at <b>2.14 L of a 10 L tank</b>, and from which its "≈4.7 fishing days on a tank" follows.
        ///
        /// <para><b>Pinned as a BAND, not a digit, and the reason is honest:</b> unlike the A–D duty table
        /// above, the day table gives no HOLD column, and the hold is filling as the day goes on (its
        /// 2.1 L/h hand-lining leg implies roughly a third of a hold aboard by then, not the empty boat
        /// its 'out to the shoal' leg starts with). Reverse-engineering hold values until the total hits
        /// 2.14 would pin an invented number and call it the doc's. The band below is what the day costs
        /// across every reasonable reading of that missing column — wide enough to be true, narrow enough
        /// that a re-tune which doubled a dory's daily burn would break it.</para>
        /// </summary>
        [Test]
        public void ADayInTheDory_CostsAboutATenthOfHerTank()
        {
            float used =
                FuelBurn.LitresBurnedOverTick(DoryR, 0.6f, 0f, 0f, Sea(SeaState.Calm), 4f * 60f, Cfg) +
                FuelBurn.LitresBurnedOverTick(DoryR, 0f, 0.3f, 0f, Sea(SeaState.Calm), 10f * 60f, Cfg) +
                FuelBurn.LitresBurnedOverTick(DoryR, 0.6f, 1f, 0f, Sea(SeaState.Light), 5f * 60f, Cfg);

            Assert.AreEqual(2.14f, used, 0.25f,
                "§9.7's reference fishing day has drifted. That figure is why a dory tank is about four " +
                "or five fishing days, and why a fill is a line in the day's arithmetic rather than a wall.");
            Assert.Less(used, 10f, "a single ordinary day must never empty the opening boat's tank");
        }

        // ---- 3. determinism, three ways --------------------------------------------------------

        [Test]
        public void TheSameTrip_IntegratedTwice_IsBitIdentical()
        {
            // Rule 5, at its strictest: not "within tolerance" — the SAME BITS. Any hidden state, any
            // clock read, any RNG in the rate function shows up here and nowhere else.
            float first = IntegrateReferenceTrip(steps: 3000, stepSeconds: 1f / 50f);
            float second = IntegrateReferenceTrip(steps: 3000, stepSeconds: 1f / 50f);
            Assert.AreEqual(BitConverter.SingleToInt32Bits(first), BitConverter.SingleToInt32Bits(second),
                $"the same trip integrated twice gave {first:R} then {second:R} — the burn model is not pure");
        }

        [Test]
        public void TheSameTrip_AtTwoFixedStepSizes_CostsTheSameFuel()
        {
            // ⚠ Frame-rate independence. A spike must not change what a crossing costs. Integration reads
            // the FIXED step, so halving it and doubling the count is the same 60 s of engine running.
            float at50Hz = IntegrateReferenceTrip(steps: 3000, stepSeconds: 1f / 50f);
            float at200Hz = IntegrateReferenceTrip(steps: 12000, stepSeconds: 1f / 200f);

            Assert.AreEqual(at50Hz, at200Hz, at50Hz * 1e-3f,
                $"60 s of the same duty cost {at50Hz:0.####} L at a 50 Hz step and {at200Hz:0.####} L at " +
                "200 Hz. Fuel must be a function of ENGINE-RUNNING SECONDS, not of how often we looked.");
        }

        [Test]
        public void DoublingTheDayLength_ChangesFuelUsedByExactlyZero()
        {
            // ⭐⭐ THE REGRESSION §9.5.3 EXISTS TO PREVENT, stated as an executable fact.
            //
            // GameConfig.SecondsPerDay is an owner tunable for TIDE PACING. If fuel were billed per GAME
            // hour, doubling the day would silently HALVE the fuel cost of every crossing in the game —
            // and no owner moving a pacing dial would think to re-check fuel. The guard is structural:
            // FuelBurn takes real seconds and cannot see the clock at all, so this test's job is to
            // notice if anyone ever wires it to one.
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                config.SecondsPerDay = GameConfig.DefaultSecondsPerDay;
                float shortDay = IntegrateReferenceTrip(3000, 1f / 50f);

                config.SecondsPerDay = GameConfig.DefaultSecondsPerDay * 2f;
                float longDay = IntegrateReferenceTrip(3000, 1f / 50f);

                Assert.AreEqual(BitConverter.SingleToInt32Bits(shortDay),
                                BitConverter.SingleToInt32Bits(longDay),
                    "the clock moved and the fuel bill moved with it. Fuel is metered against real " +
                    "engine-running seconds; nothing in the burn path may read SecondsPerDay.");
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        /// <summary>
        /// ⭐⭐ THE CLOCK TEST WITH TEETH. Its sibling above integrates a trip and asserts the total does
        /// not move when <c>SecondsPerDay</c> does — which is the acceptance criterion verbatim, and which
        /// becomes load-bearing the moment a boat-side integrator exists to drive it. But TODAY nothing
        /// consumes the model, so that test cannot fail however wrong the code is, and a guard that cannot
        /// fail is not a guard.
        ///
        /// <para>This one can. It reads <c>FuelBurn.cs</c> itself and demands the purity §9.5.3 asks for:
        /// no clock, no randomness, no Unity. The day someone threads a <c>GameConfig</c> into the burn
        /// path to "get the hour", or reaches for <c>Time.deltaTime</c> instead of the fixed step passed
        /// in, this goes red at the exact line — which is a great deal cheaper than noticing that tuning
        /// tide pacing quietly re-priced every crossing in the game.</para>
        /// </summary>
        [Test]
        public void TheBurnModel_CannotSeeTheClock_TheRng_OrUnity()
        {
            const string sourcePath = "Assets/_Project/Code/Core/Economy/FuelBurn.cs";
            Assert.IsTrue(System.IO.File.Exists(sourcePath), $"{sourcePath} has moved — repoint this guard");

            string[] lines = System.IO.File.ReadAllLines(sourcePath);
            var offences = new System.Collections.Generic.List<string>();

            // Each banned token, and the sentence it would break. Doc comments are exempt: this file
            // NAMES SecondsPerDay and Mathf in its own remarks precisely to warn against them.
            var banned = new (string token, string why)[]
            {
                ("SecondsPerDay",   "the GAME clock — billing per game hour makes a longer day cheaper to cross"),
                ("Time.",           "a clock read — the fixed step arrives as an argument, always"),
                ("UnityEngine",     "a Unity dependency — the rate function is pure arithmetic"),
                ("Mathf",           "Unity's math — use System.Math; this file has no reason to know Unity exists"),
                ("Random",          "randomness — rule 5: the same duty costs the same fuel, forever"),
                ("GameConfig",      "the whole config — take FuelSettings, which carries no clock"),
            };

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("///") || trimmed.StartsWith("//")) continue;   // remarks may warn
                foreach (var (token, why) in banned)
                    if (lines[i].Contains(token))
                        offences.Add($"{sourcePath}:{i + 1} uses '{token}' — {why}\n      {trimmed}");
            }

            Assert.IsEmpty(offences,
                "FuelBurn must stay pure (§9.5.3): no clock, no RNG, no Unity types. Found:\n  " +
                string.Join("\n  ", offences));
        }

        [Test]
        public void AnHourAtAKnownRate_BurnsExactlyThatManyLitres()
        {
            // The unit that ties the rate to the integral. If SecondsPerHour were ever "helpfully"
            // replaced by GameConfig.SecondsPerHour, this is the arithmetic that would stop being true.
            Assert.AreEqual(23f, FuelBurn.LitresBurned(23f, FuelBurn.SecondsPerHour), 1e-3f);
            Assert.AreEqual(11.5f, FuelBurn.LitresBurned(23f, FuelBurn.SecondsPerHour / 2f), 1e-3f);
            Assert.AreEqual(0f, FuelBurn.LitresBurned(23f, 0f), "no time, no fuel");
            Assert.AreEqual(0f, FuelBurn.LitresBurned(23f, -1f), "time does not run backwards");
        }

        // ---- 4. slip, and the seam it is waiting on --------------------------------------------

        [Test]
        public void Slip_IsZero_WhenTheQuestionIsMeaningless()
        {
            // The forgiving direction: an un-measured hull (top speed 0) is never charged a surcharge it
            // cannot justify, and neither is a boat at rest with her throttle shut.
            Assert.AreEqual(0f, FuelBurn.SlipFraction(0f, 0.5f, 0f), "no measured top speed = no slip");
            Assert.AreEqual(0f, FuelBurn.SlipFraction(0f, 0f, 4.24f), "throttle shut = no slip");
        }

        [Test]
        public void Slip_ReadsHowBadlySheIsMissingTheSpeedSheAskedFor()
        {
            const float top = 4.24f;                       // boat.lobster_boat, MEASURED (§9.8)
            Assert.AreEqual(0f, FuelBurn.SlipFraction(top, 1f, top), 1e-5f, "making her speed = no slip");
            Assert.AreEqual(0.5f, FuelBurn.SlipFraction(top * 0.5f, 1f, top), 1e-5f, "half her speed = 0.5");
            Assert.AreEqual(1f, FuelBurn.SlipFraction(0f, 1f, top), 1e-5f, "stopped dead = fully bogged");
            Assert.AreEqual(0f, FuelBurn.SlipFraction(top * 1.5f, 1f, top), 1e-5f,
                "running faster than asked (a fair tide under her) clamps at 0, never negative");
        }

        [Test]
        public void SlipCostsNothingToday_BecauseTheShippedWeightIsZero()
        {
            // Pins the first-cut decision itself: until MeasuredTopSpeedMps exists, the slip column must
            // make no difference to any bill. This is what lets §9.7/§9.8's slip figures sit in the doc
            // without the shipped game charging for them.
            float noSlip = FuelBurn.LitresPerHour(LobsterR, 0.7f, 0.5f, 0f, Sea(SeaState.Moderate), Cfg);
            float fullySlipped = FuelBurn.LitresPerHour(LobsterR, 0.7f, 0.5f, 1f, Sea(SeaState.Moderate), Cfg);
            Assert.AreEqual(noSlip, fullySlipped,
                "SlipLoadWeight ships at 0, so slip must be exactly free until the measured field lands");
        }

        // ---- 5. the shipped asset actually carries the tuning ----------------------------------

        /// <summary>
        /// ⭐ THE VALUE CHECK THE COVERAGE TEST CANNOT DO. <c>GameConfigAssetCoverageTests</c> proves the
        /// nine keys are present in the YAML; it cannot tell 1 from 0. A shipped <c>BurnScale: 0</c> is a
        /// green suite and a game where fuel is free and no boat ever runs dry — which is exactly the
        /// silent-zero shape that has already cost this repo twice.
        /// </summary>
        [Test]
        public void TheShippedGameConfig_CarriesTheAuthoredFuelCurve()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/Config/GameConfig.asset");
            Assert.IsNotNull(asset, "GameConfig.asset is missing from Data/Config");

            FuelSettings shipped = asset.Fuel, intended = FuelSettings.Default;
            Assert.AreEqual(intended.IdleFraction, shipped.IdleFraction, 1e-6f, "Fuel.IdleFraction");
            Assert.AreEqual(intended.ThrottleExponent, shipped.ThrottleExponent, 1e-6f, "Fuel.ThrottleExponent");
            Assert.AreEqual(intended.LoadSurcharge, shipped.LoadSurcharge, 1e-6f, "Fuel.LoadSurcharge");
            Assert.AreEqual(intended.HoldLoadWeight, shipped.HoldLoadWeight, 1e-6f, "Fuel.HoldLoadWeight");
            Assert.AreEqual(intended.SlipLoadWeight, shipped.SlipLoadWeight, 1e-6f, "Fuel.SlipLoadWeight");
            Assert.AreEqual(intended.SeaSurcharge, shipped.SeaSurcharge, 1e-6f, "Fuel.SeaSurcharge");
            Assert.AreEqual(intended.LowFuelFraction, shipped.LowFuelFraction, 1e-6f, "Fuel.LowFuelFraction");
            Assert.AreEqual(intended.NewBoatArrivesFull, shipped.NewBoatArrivesFull, "Fuel.NewBoatArrivesFull");

            Assert.Greater(shipped.BurnScale, 0f,
                "⚠ GameConfig.asset ships BurnScale = 0, which means NOTHING EVER BURNS and fuel is free. " +
                "That is what an omitted YAML key deserializes to — hand-add the value, do not let Unity " +
                "re-serialize the whole file.");
            Assert.AreEqual(intended.BurnScale, shipped.BurnScale, 1e-6f, "Fuel.BurnScale");
        }

        // ---- helpers ---------------------------------------------------------------------------

        private static void AssertRate(string label, float expected, float r, float throttle,
                                       float hold, float slip, SeaState sea)
        {
            float got = FuelBurn.LitresPerHour(r, throttle, hold, slip, Sea(sea), CfgWithSlip);
            Assert.AreEqual(expected, got, 0.05f,
                $"{label}: the doc says {expected:0.0} L/h and the model says {got:0.000}");
        }

        /// <summary>
        /// A fixed 60 seconds of engine running at one steady duty, integrated tick by tick. The duty is
        /// arbitrary but deliberately non-trivial (part throttle, part hold, a real sea) so a term dropping
        /// out of the formula changes the answer.
        /// </summary>
        private static float IntegrateReferenceTrip(int steps, float stepSeconds)
        {
            float litres = 0f;
            for (int i = 0; i < steps; i++)
                litres += FuelBurn.LitresBurnedOverTick(
                    LobsterR, 0.7f, 0.5f, 0f, Sea(SeaState.Moderate), stepSeconds, Cfg);
            return litres;
        }
    }
}
