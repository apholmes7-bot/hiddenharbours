using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE LADDER — <c>docs/design/fuel-and-refuelling.md</c> §9.6.2, re-derived from the authored Defs
    /// rather than restated from the doc.
    ///
    /// <para><b>⭐ Why this suite exists and the content validator is not enough.</b> Validation asks
    /// whether each hull is <i>coherent</i>; this asks whether the fleet still <i>says what it was
    /// authored to say</i>. Every one of these numbers is a proposal the owner is invited to move, and
    /// moving one is fine — what must not happen is a re-tune quietly flattening the thing the ladder is
    /// FOR. Reach growing up the ladder is P2 made physical in the currency the player feels every trip
    /// (2.6 km in a dory → 15 km offshore), and nothing else in the game would notice if it stopped
    /// being true.</para>
    ///
    /// <para><b>These are BANDS, not equalities</b>, and deliberately so: an equality test over 38 hulls
    /// is a re-tune that reds thirty-eight assertions and teaches nobody anything. A band goes red only
    /// when a change breaks the SHAPE — which is the thing worth defending.</para>
    /// </summary>
    public class FuelLadderTests
    {
        private const string DataRoot = "Assets/_Project/Data";

        /// <summary>The reference duty every derived column in §9.6.2 is computed at: throttle 0.7, half
        /// hold, Moderate sea, no slip. Multiplier 0.726.</summary>
        private static float CruiseLitresPerHour(float r) =>
            FuelBurn.LitresPerHour(r, 0.7f, 0.5f, 0f, (int)SeaState.Moderate / 7f, FuelSettings.Default);

        /// <summary>A reference working day: 12 minutes of engine running — about 40% of the 30-minute
        /// game day under power, the rest fishing, hauling or ashore.</summary>
        private const float WorkingDayMinutesUnderPower = 12f;

        private static List<BoatHullDef> Hulls()
        {
            var list = new List<BoatHullDef>();
            foreach (string guid in AssetDatabase.FindAssets("t:BoatHullDef", new[] { DataRoot }))
            {
                var h = AssetDatabase.LoadAssetAtPath<BoatHullDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (h != null && !string.IsNullOrEmpty(h.Id)) list.Add(h);
            }
            return list;
        }

        private static BoatHullDef ById(string id)
        {
            foreach (var h in Hulls())
                if (h.Id == id) return h;
            Assert.Fail($"{id} is not in Data/ — this suite names shipped hulls by their stable ids");
            return null;
        }

        // ---- the fleet is authored at all ---------------------------------------------------------

        [Test]
        public void EveryEngineHull_CarriesATank()
        {
            // The authoring pass is done, so "un-authored" is no longer an acceptable state for a boat
            // with an engine — it would mean infinite range, silently, for that hull alone.
            var bare = new List<string>();
            foreach (var h in Hulls())
                if (h.Propulsion == PropulsionType.Engine && h.FuelCapacityLitres <= 0f)
                    bare.Add(h.Id);

            Assert.IsEmpty(bare,
                "these hulls are engine-driven but carry no tank, so they have infinite range: " +
                string.Join(", ", bare));
        }

        [Test]
        public void TheRowedDory_IsTheOnlyHullWithoutATank()
        {
            var tankless = new List<string>();
            foreach (var h in Hulls())
                if (h.FuelCapacityLitres <= 0f) tankless.Add(h.Id);

            CollectionAssert.AreEqual(new[] { "boat.dory" }, tankless,
                "§9.6.2 lists exactly one hull as '(none — she rows)'. Every other boat drinks something.");
        }

        [Test]
        public void EveryTank_IsOneOfTheTwoENGINEGrades()
        {
            // §9.10: gas or diesel. Nothing burns oil or stove oil — those heat houses and lubricate
            // gearcases, and a hull authored to one would be refused by every pump in the game.
            foreach (var h in Hulls())
            {
                if (h.FuelCapacityLitres <= 0f) continue;
                Assert.That(h.FuelGrade, Is.EqualTo(FuelGrades.Gas).Or.EqualTo(FuelGrades.Diesel),
                    $"{h.Id}: '{h.FuelGrade}' is not an engine fuel");
            }
        }

        // ---- the geography gate --------------------------------------------------------------------

        [Test]
        public void TheGasFleetIsTheOutboardFleet_AndTheDieselFleetHasEngineRooms()
        {
            // ⭐ THE WHOLE OF §3 IN ONE ASSERTION. St Peters sells gas in cans over a counter and has no
            // pump; Nine Mile Creek has diesel. So the day you own a diesel boat, home can no longer
            // fuel you — and WHICH boats those are is what decides when that day arrives.
            string[] gas =
            {
                "boat.dory_outboard", "boat.fishing_skiff", "boat.punt", "boat.punt_upgraded",
                "boat.console_skiff", "boat.sport_skiff", "boat.sport_skiff_mk2", "boat.sport_skiff_twin",
                "boat.zodiac_frc", "boat.zodiac_hurricane",
            };
            foreach (string id in gas)
                Assert.AreEqual(FuelGrades.Gas, ById(id).FuelGrade, $"{id} is outboard-driven");

            Assert.AreEqual(FuelGrades.Diesel, ById("boat.lobster_boat").FuelGrade);
            Assert.AreEqual(FuelGrades.Diesel, ById("boat.cape_islander").FuelGrade);
            Assert.AreEqual(FuelGrades.Diesel, ById("boat.tanker").FuelGrade);
        }

        [Test]
        public void TheGateClosesAtTheFirstLobsterBoat()
        {
            // ⏳ OWNER, §9.15 call 1 — the six inshore variants are authored DIESEL as recommended, so
            // the "home can no longer fuel you" beat lands at the first lobster boat rather than at the
            // 12 m standard boats. Pinned so flipping it is a deliberate act with a visible diff, not a
            // drift. One field on six assets either way.
            foreach (var h in Hulls())
            {
                if (!h.Id.StartsWith("boat.lobster_inshore_")) continue;
                Assert.AreEqual(FuelGrades.Diesel, h.FuelGrade,
                    $"{h.Id}: §9.10's recommendation is diesel — flip all six together, or none");
            }
        }

        // ---- the shape of the ladder ----------------------------------------------------------------

        [Test]
        public void TanksGrowMonotonicallyUpTheLadder()
        {
            // Not every hull, but the spine of the progression — the boats a player actually steps
            // through. A rung that shrank would be an upgrade that cost you range.
            string[] spine =
            {
                "boat.dory_outboard", "boat.punt", "boat.console_skiff", "boat.sport_skiff",
                "boat.cape_islander", "boat.lobster_offshore_open_fundy",
                "boat.sport_fisher_convertible", "boat.stern_trawler", "boat.coastal_packet", "boat.tanker",
            };

            float previous = 0f;
            foreach (string id in spine)
            {
                float tank = ById(id).FuelCapacityLitres;
                Assert.Greater(tank, previous, $"{id} carries no more fuel than the rung below her");
                previous = tank;
            }
        }

        [Test]
        public void CanonsOwnTankFigures_AreUnchanged()
        {
            // ⭐ §9.1's identity in force: boats-and-navigation.md §1.1's FUEL-UNIT column is re-read as
            // litres with NO re-tuning. These five are canon's own numbers; if one moves, the identity
            // has quietly stopped being an identity and §9.1's whole argument needs re-running.
            Assert.AreEqual(10f, ById("boat.dory_outboard").FuelCapacityLitres, 1e-3f, "dory");
            Assert.AreEqual(25f, ById("boat.punt").FuelCapacityLitres, 1e-3f, "punt");
            Assert.AreEqual(90f, ById("boat.cape_islander").FuelCapacityLitres, 1e-3f, "cape islander");
            Assert.AreEqual(700f, ById("boat.stern_trawler").FuelCapacityLitres, 1e-3f, "stern trawler");
            Assert.AreEqual(9000f, ById("boat.tanker").FuelCapacityLitres, 1e-3f, "tanker");
        }

        [Test]
        public void EveryBoat_GetsAtLeastAWorkingDayPerTank()
        {
            // ⭐ THE FLOOR THAT KEEPS FUEL A SOFT GATE (§1). A boat that cannot finish one ordinary day
            // on a full tank turns every trip into a refuelling errand, which is the opposite of what
            // this system is for: fuel should be a line in the day's arithmetic, never a wall.
            foreach (var h in Hulls())
            {
                if (h.FuelCapacityLitres <= 0f) continue;
                float hoursPerTank = h.FuelCapacityLitres / CruiseLitresPerHour(h.FullThrottleLitresPerHour);
                float daysPerTank = hoursPerTank * 60f / WorkingDayMinutesUnderPower;

                Assert.Greater(daysPerTank, 2.5f,
                    $"{h.Id} gets only {daysPerTank:0.0} working days on a tank — under ~3 the fill " +
                    "stops being a decision and becomes a chore");
                Assert.Less(daysPerTank, 25f,
                    $"{h.Id} gets {daysPerTank:0.0} working days on a tank, which is far enough that " +
                    "she has effectively stopped having a fuel economy at all");
            }
        }

        [Test]
        public void EnduranceGrowsUpTheLadder_WhichIsWhatTheLadderIsFOR()
        {
            // §9.6.2's Days/tank column: 3.0 in the dory → 14 in a tanker. The doc is explicit that
            // RANGE flattens above ~15 km because the big hulls are slow, and that this is fine — the
            // far regions are gated by seaworthiness and draught, not by fuel. ENDURANCE is the column
            // that must keep climbing, and it is the one a player feels as "she can stay out".
            float dory = DaysPerTank(ById("boat.dory_outboard"));
            float cape = DaysPerTank(ById("boat.cape_islander"));
            float trawler = DaysPerTank(ById("boat.stern_trawler"));

            Assert.Greater(cape, dory, "a Cape Islander must stay out longer than a dory");
            Assert.Greater(trawler, cape, "and a stern trawler longer than a Cape Islander");
            Assert.AreEqual(3.0f, dory, 0.4f, "the dory's ~3 days per tank is the opening's pacing");
        }

        [Test]
        public void ATankOfGasForTheDory_CostsAboutOneCod()
        {
            // ⭐⭐ THE SENTENCE §9.1's WHOLE ARGUMENT RESTS ON. The FU-is-a-litre identity was chosen
            // because the MONEY lands here — heavier than a load of ice, lighter than the clam licence,
            // impossible to ignore for a player whose whole day is four fish. If a re-tune ever breaks
            // this, the unit question is reopened, not merely re-balanced.
            var station = LoadStation("station.route_91");
            Assert.IsNotNull(station, "Route 91 must exist — §8.2's cheaper of the two sites");

            FuelGradeOffer offer = station.Offer(FuelGrades.Gas);
            Assert.IsTrue(offer.Available, "Route 91 stocks gas");

            var dory = ById("boat.dory_outboard");
            FuelQuote fill = FuelPricing.Quote(offer, dory.FuelGrade, dory.FuelCapacityLitres,
                                               litresInVessel: 0f, money: 100000);

            Assert.AreEqual(16, fill.Price, 1,
                "a brim fill of the dory at the road station should land at about 16 ₲ — roughly one " +
                "cod (14 ₲), and about three loads of ice");
        }

        // ---- the worked examples, against the AUTHORED hulls ----------------------------------------

        [Test]
        public void TheAuthoredDory_ReproducesTheWorkedExample()
        {
            // §9.7 was computed at R = 23. FuelBurnTests pins the arithmetic; this pins that the ASSET
            // still carries the R the doc did its arithmetic with.
            var dory = ById("boat.dory_outboard");
            Assert.AreEqual(23f, dory.FullThrottleLitresPerHour, 1e-3f);
            Assert.AreEqual(16.7f, CruiseLitresPerHour(dory.FullThrottleLitresPerHour), 0.1f,
                "§9.7 row B — cruise, half hold, moderate");
        }

        [Test]
        public void TheAuthoredLobsterBoat_ReproducesTheWorkedExample()
        {
            var lobster = ById("boat.lobster_boat");
            Assert.AreEqual(105f, lobster.FullThrottleLitresPerHour, 1e-3f);
            Assert.AreEqual(76.2f, CruiseLitresPerHour(lobster.FullThrottleLitresPerHour), 0.1f,
                "§9.8 row B — cruise, half hold, moderate");
            Assert.AreEqual(85f, lobster.FuelCapacityLitres, 1e-3f,
                "and the 85 L tank behind §9.8's 'three days or one' comparison");
        }

        [Test]
        public void TheGreedyDay_CostsAboutThreeTimesTheSensibleOne()
        {
            // ⭐ §9.8's whole point, asserted: the tank is three days or one, and the BOAT has nothing to
            // do with which. If a re-tune ever flattens this, easing off has stopped being a decision.
            var lobster = ById("boat.lobster_boat");
            float r = lobster.FullThrottleLitresPerHour;
            float light = (int)SeaState.Light / 7f, moderate = (int)SeaState.Moderate / 7f;
            float lively = (int)SeaState.Lively / 7f, gale = (int)SeaState.Gale / 7f;
            var cfg = FuelSettings.Default;

            float sensible =
                Burn(r, 0.85f, 0f,   light,    6f, cfg) +
                Burn(r, 0.3f,  0.6f, moderate, 12f, cfg) +
                Burn(r, 0.85f, 1f,   lively,   6f, cfg);

            float greedy =
                Burn(r, 1f, 0f,   light, 10f, cfg) +
                Burn(r, 0.35f, 0.6f, lively, 20f, cfg) +
                Burn(r, 1f, 1f,   gale,  12f, cfg);

            Assert.Greater(greedy / sensible, 2f,
                $"the greedy day cost {greedy:0.0} L against the sensible day's {sensible:0.0} L — " +
                "under 2× and 'pin the throttle' stops being a choice with a consequence");
            Assert.Less(greedy, lobster.FuelCapacityLitres,
                "…and it must still GET HOME. The greedy day is not punished — she arrives with a " +
                "bigger catch and a nearly empty tank, and tomorrow's first decision is the fuel run. " +
                "Cozy, but with teeth (P5).");
        }

        // ---- helpers ---------------------------------------------------------------------------------

        private static float Burn(float r, float throttle, float hold, float sea, float minutes,
                                  in FuelSettings cfg) =>
            FuelBurn.LitresBurnedOverTick(r, throttle, hold, 0f, sea, minutes * 60f, cfg);

        private static float DaysPerTank(BoatHullDef h) =>
            h.FuelCapacityLitres / CruiseLitresPerHour(h.FullThrottleLitresPerHour)
            * 60f / WorkingDayMinutesUnderPower;

        private static FuelStationDef LoadStation(string id)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:FuelStationDef", new[] { DataRoot }))
            {
                var s = AssetDatabase.LoadAssetAtPath<FuelStationDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (s != null && s.Id == id) return s;
            }
            return null;
        }
    }
}
