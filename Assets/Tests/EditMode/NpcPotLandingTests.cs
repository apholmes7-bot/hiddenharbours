using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Landing the day's catch on the real committed Def: every keeper leaves the boat in a crate in her
    /// hands, is walked the whole way to the buyer's counter, and is sold there; the same every time it is
    /// asked, and inside the day.
    /// </summary>
    public class NpcPotLandingTests
    {
        private const string DefPath = "Assets/_Project/Data/Traps/NmcDoironYvettePotDay.asset";
        private const string FishFolder = "Assets/_Project/Data/Fish";
        private const double Day = GameConfig.DefaultSecondsPerDay;
        private const double Step = 0.25;
        private static readonly int[] Seeds = { 0, 1, 4242, 987654321 };
        private const int DaysProbed = 6;

        /// <summary>The wharf wall's face at Nine Mile Creek (the wharf key scene's berths' "face").</summary>
        private const float WallFaceY = 86f;
        private const float CounterReachMeters = 2.5f;

        private NpcPotDayDef _def;

        private static float Tide(double t) => 1.1f * Mathf.Sin((float)(t / Day * 4.0 * Mathf.PI));

        [SetUp]
        public void SetUp()
        {
            FishSpeciesRegistry.Reset();
            _def = AssetDatabase.LoadAssetAtPath<NpcPotDayDef>(DefPath);
            Assert.IsNotNull(_def, DefPath);
            var allowed = new HashSet<string>(_def.Trap.AllowedCatchFishIds);
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(FishSpeciesDef)}", new[] { FishFolder }))
            {
                var fish = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (fish != null && allowed.Contains(fish.Id)) FishSpeciesRegistry.Register(fish);
            }
        }

        [TearDown]
        public void TearDown() => FishSpeciesRegistry.Reset();

        private NpcPotLanding Build(int seed, int day, out NpcPotDay today, out NpcPotDayCatch caught, NpcPotDayDef def = null)
        {
            def = def != null ? def : _def;
            today = NpcPotDay.Build(def, seed, day, Day);
            NpcPotDay yesterday = NpcPotDay.Build(def, seed, day - 1, Day);
            caught = NpcPotDayCatch.Build(def, seed, today, yesterday, Season.EarlySpring, Tide, Day);
            return NpcPotLanding.Build(def, today, caught);
        }

        [Test]
        public void LandingVelocity_IsTheRoutesDerivative_AndZeroWhileSheStands()
        {
            NpcPotLanding plan = Build(4242, 2, out _, out _);
            Assert.IsTrue(plan.HasLanding);
            double ladder = plan.StartSeconds + 2 * _def.DeckWalkSeconds + _def.CrateLiftSeconds;
            double climb = ladder + _def.LadderSeconds * 0.5;
            double walk = ladder + _def.LadderSeconds + 1.0;
            foreach (double t in new[] { plan.StartSeconds + _def.DeckWalkSeconds * 0.5, climb, walk })
            {
                const double sample = 0.125;
                Vector2 measured = (plan.MomentAt(t + sample).Position - plan.MomentAt(t - sample).Position) / (float)(2 * sample);
                NpcLandingMoment moment = plan.MomentAt(t);
                Assert.That((moment.Velocity - measured).magnitude, Is.LessThan(0.001f));
                Assert.AreEqual(moment.Velocity, plan.MomentAt(t).Velocity, "repeated seeks have no frame history");
            }
            Assert.That(plan.MomentAt(walk).Velocity.magnitude, Is.EqualTo(_def.LandingWalkSpeed).Within(0.001f));
            Assert.AreEqual(Vector2.zero, plan.MomentAt(plan.StartSeconds + _def.DeckWalkSeconds + _def.CrateLiftSeconds * 0.5).Velocity);
            Assert.AreEqual(Vector2.zero, plan.MomentAt(plan.SoldAtSeconds - _def.SaleSeconds * 0.5).Velocity);
            Assert.AreEqual(Vector2.zero, plan.MomentAt(plan.EndSeconds).Velocity);
        }

        [Test]
        public void TheRoute_StartsAtTheTopOfHerLadder_AndEndsAtTheBuyersCounter()
        {
            Assert.IsNull(NpcPotLanding.Problem(_def), NpcPotLanding.Problem(_def));
            Vector2 top = _def.LandingRoute[0];
            Vector2 rail = NpcPotLanding.ToWorld(_def, _def.StepAshoreLocalMeters);
            Assert.That(top.y, Is.GreaterThan(WallFaceY), "the first point is on the wharf, behind the wall's face");
            Assert.That(rail.y, Is.LessThan(WallFaceY), "she steps off from the boat's side of the wall");
            Assert.That((top - rail).magnitude, Is.LessThan(5f), "the ladder is at her rail, not a jump away");

            Vector2 counter = _def.LandingRoute[_def.LandingRoute.Length - 1];
            Vector2 buyer = NineMileCreekMainland.FishBuyerPos;
            Assert.That((counter - buyer).magnitude, Is.LessThanOrEqualTo(CounterReachMeters),
                        $"the route ends {(counter - buyer).magnitude:0.0} m from the buyer's counter at {buyer}");
        }

        [Test]
        public void BeforeSheIsHome_NothingIsLanded_AndEveryCrateIsAboard()
        {
            NpcPotLanding plan = Build(4242, 2, out NpcPotDay today, out NpcPotDayCatch caught);
            Assert.IsTrue(plan.HasLanding, "seed 4242 day 2 lands keepers");
            Assert.That(plan.StartSeconds, Is.GreaterThanOrEqualTo(today.ReturnSeconds));
            NpcLandingMoment before = plan.MomentAt(today.ReturnSeconds);
            Assert.That(before.Step, Is.EqualTo(NpcLandingStep.Aboard));
            Assert.IsFalse(before.Ashore);
            Assert.That(plan.AtCounterAt(today.ReturnSeconds), Is.EqualTo(0));
            int aboard = 0;
            for (int g = 0; g < plan.GradeCount; g++) aboard += plan.AboardAt(today.ReturnSeconds, g);
            Assert.That(aboard, Is.EqualTo(caught.KeptCount));
        }

        [Test]
        public void EveryKeeper_IsAboard_InHerHands_OrAtTheCounter_UntilItIsSold()
        {
            foreach (int seed in Seeds)
            {
                for (int day = 0; day < DaysProbed; day++)
                {
                    NpcPotLanding plan = Build(seed, day, out NpcPotDay today, out NpcPotDayCatch caught);
                    if (!plan.HasLanding) { Assert.That(caught.KeptCount, Is.EqualTo(0)); continue; }

                    for (double t = plan.StartSeconds; t < plan.SoldAtSeconds; t += Step)
                    {
                        NpcLandingMoment m = plan.MomentAt(t);
                        int total = plan.AtCounterAt(t);
                        for (int g = 0; g < plan.GradeCount; g++) total += plan.AboardAt(t, g);
                        bool holding = m.Crate >= 0 && plan.AboardAt(t, m.Crate) == 0 && plan.AtCounterAt(t, m.Crate) == 0;
                        if (holding) total += caught.StowedAt(today.ReturnSeconds, m.Crate);
                        Assert.That(total, Is.EqualTo(caught.KeptCount),
                                    $"seed {seed} day {day} t+{t - plan.StartSeconds:0.00}: a keeper is nowhere ({m.Step}, crate {m.Crate})");
                    }
                    Assert.That(plan.AtCounterAt(plan.SoldAtSeconds), Is.EqualTo(0), "the buyer has taken the lot");
                    Assert.IsTrue(plan.IsSoldAt(plan.SoldAtSeconds));
                    Assert.That(plan.SoldCount, Is.EqualTo(caught.KeptCount));
                    Assert.That(plan.SoldKg, Is.EqualTo(caught.KeptKg));
                }
            }
        }

        [Test]
        public void ACrate_GoesDownOnlyAtTheCounter_AndOnlyFromHerHands()
        {
            NpcPotLanding plan = Build(4242, 2, out _, out _);
            var down = new int[plan.GradeCount];
            NpcLandingMoment last = plan.MomentAt(plan.StartSeconds);
            for (double t = plan.StartSeconds; t <= plan.SoldAtSeconds; t += Step)
            {
                NpcLandingMoment m = plan.MomentAt(t);
                for (int g = 0; g < plan.GradeCount; g++)
                {
                    int now = plan.AtCounterAt(t, g);
                    if (now == down[g] || t >= plan.SoldAtSeconds) continue;
                    Assert.That(last.Step, Is.EqualTo(NpcLandingStep.SettingDown), $"crate {g} appeared at the counter with no hand on it");
                    Assert.That(last.Crate, Is.EqualTo(g));
                    Assert.That((last.Position - plan.CounterPosition).magnitude, Is.LessThan(0.01f));
                    down[g] = now;
                }
                last = m;
            }
        }

        [Test]
        public void SheWalksTheWholeWay_NoJumps()
        {
            NpcPotLanding plan = Build(4242, 2, out _, out _);
            Vector2 rail = NpcPotLanding.ToWorld(_def, _def.StepAshoreLocalMeters);
            float ladder = (_def.LandingRoute[0] - rail).magnitude / _def.LadderSeconds;
            float deck = 8f / Mathf.Max(0.1f, _def.DeckWalkSeconds);   // no two deck points are 8 m apart
            float fastest = Mathf.Max(_def.LandingWalkSpeed, Mathf.Max(ladder, deck));

            NpcLandingMoment last = plan.MomentAt(plan.StartSeconds);
            bool wentAshore = false;
            for (double t = plan.StartSeconds + Step; t < plan.EndSeconds; t += Step)
            {
                NpcLandingMoment m = plan.MomentAt(t);
                Assert.That((m.Position - last.Position).magnitude, Is.LessThanOrEqualTo(fastest * (float)Step + 0.01f),
                            $"t+{t - plan.StartSeconds:0.00}: {last.Step} at {last.Position} then {m.Step} at {m.Position}");
                wentAshore |= m.Ashore;
                last = m;
            }
            Assert.IsTrue(wentAshore);
            NpcLandingMoment home = plan.MomentAt(plan.EndSeconds);
            Assert.That(home.Step, Is.EqualTo(NpcLandingStep.Aboard));
            Assert.IsFalse(home.Ashore, "she ends the day aboard");
        }

        [Test]
        public void AskedAgain_MidLanding_ThePlanIsTheSame()
        {
            NpcPotLanding a = Build(4242, 2, out _, out _), b = Build(4242, 2, out _, out _);
            Assert.That(b.StartSeconds, Is.EqualTo(a.StartSeconds));
            Assert.That(b.SoldAtSeconds, Is.EqualTo(a.SoldAtSeconds));
            Assert.That(b.EndSeconds, Is.EqualTo(a.EndSeconds));
            for (double t = a.StartSeconds; t <= a.EndSeconds; t += 7.3)
            {
                NpcLandingMoment x = a.MomentAt(t), y = b.MomentAt(t);
                Assert.That(y.Step, Is.EqualTo(x.Step));
                Assert.That(y.Position, Is.EqualTo(x.Position));
                Assert.That(y.Crate, Is.EqualTo(x.Crate));
                Assert.That(y.Ashore, Is.EqualTo(x.Ashore));
            }
        }

        [Test]
        public void TheLanding_IsOverBeforeTheDayIs()
        {
            foreach (int seed in Seeds)
            {
                for (int day = 0; day < DaysProbed; day++)
                {
                    NpcPotLanding plan = Build(seed, day, out NpcPotDay today, out _);
                    Assert.That(plan.EndSeconds, Is.LessThan(today.DayStartSeconds + Day),
                                $"seed {seed} day {day}: back aboard {plan.EndSeconds - today.DayStartSeconds:0} s into a {Day} s day");
                    Assert.That(plan.TripCount, Is.LessThanOrEqualTo(plan.GradeCount));
                }
            }
        }

        [Test]
        public void WithNothingKept_SheStaysAboard()
        {
            NpcPotDayDef empty = Object.Instantiate(_def);
            try
            {
                empty.CatchRegionId = "region.nowhere";
                NpcPotLanding plan = Build(4242, 2, out NpcPotDay today, out NpcPotDayCatch caught, empty);
                Assert.That(caught.KeptCount, Is.EqualTo(0));
                Assert.IsFalse(plan.HasLanding);
                Assert.That(plan.MomentAt(today.ReturnSeconds + 60.0).Step, Is.EqualTo(NpcLandingStep.Aboard));
                Assert.IsFalse(plan.IsSoldAt(today.ReturnSeconds + 3600.0));
            }
            finally { Object.DestroyImmediate(empty); }
        }
    }
}
