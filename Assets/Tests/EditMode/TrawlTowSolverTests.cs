using System;
using NUnit.Framework;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class TrawlTowSolverTests
    {
        private static void Same(TrawlTowResult a, TrawlTowResult b)
        {
            Assert.AreEqual(a.Status, b.Status); Assert.AreEqual(a.LotCount, b.LotCount);
            Assert.AreEqual(a.Count, b.Count); Assert.AreEqual(a.BycatchCount, b.BycatchCount);
            Assert.AreEqual(a.WeightKg, b.WeightKg); Assert.AreEqual(a.EndIntegrity01, b.EndIntegrity01);
            Assert.AreEqual(a.SweptSquareMetres, b.SweptSquareMetres); Assert.AreEqual(a.Samples, b.Samples);
            for (int i = 0; i < a.LotCount; i++) Assert.AreEqual(a[i], b[i]);
        }

        [Test]
        public void ARealSweep_CollectsTheSampledSchoolOnceAndUsesExistingSizeBounds()
        {
            var r = TrawlTestData.Resolve();
            Assert.AreEqual(TrawlTowStatus.Complete, r.Status); Assert.AreEqual(12, r.Count);
            Assert.AreEqual(1, r.LotCount); Assert.AreEqual("fish.test_cod", r[0].SpeciesId);
            Assert.AreEqual(1u, r[0].SchoolKey); Assert.AreEqual(0, r.BycatchCount);
            Assert.That(r[0].UnitWeightKg, Is.InRange(1d, 2d));
            Assert.AreEqual(r[0].UnitWeightKg * 12d, r.WeightKg);
            Assert.AreEqual(80d, r.SweptSquareMetres); Assert.AreEqual(1d, r.EndIntegrity01);
            Assert.AreEqual(5, r.Samples);
        }

        [TestCase(0d, 0)]
        [TestCase(0.5d, 0)]
        [TestCase(1d, 12)]
        [TestCase(2d, 12)]
        [TestCase(3d, 12)]
        [TestCase(3.5d, 0)]
        public void TowSpeedWindow_IsInclusiveAndStationaryGearDoesNotFish(double speed, int expected)
        {
            var s = new TrawlTestData.Survey { Populations = new[] { TrawlTestData.Population(x: (float)(speed * 5d)) } };
            Assert.AreEqual(expected, TrawlTestData.Resolve(s, TrawlTestData.Track(speed: speed)).Count);
        }

        [TestCase(0d, 0)]
        [TestCase(39.99d, 0)]
        [TestCase(40d, 12)]
        [TestCase(100d, 12)]
        [TestCase(100.01d, 0)]
        public void WarpLength_IsCheckedAgainstActualGroundDepth(double warp, int expected)
            => Assert.AreEqual(expected, TrawlTestData.Resolve(track: TrawlTestData.Track(warp: warp)).Count);

        [TestCase(0d, 0)]
        [TestCase(3.99d, 0)]
        [TestCase(4d, 12)]
        [TestCase(8d, 12)]
        [TestCase(8.01d, 0)]
        public void DoorSpreadWindow_IsInclusive(double spread, int expected)
            => Assert.AreEqual(expected, TrawlTestData.Resolve(track: TrawlTestData.Track(spread: spread)).Count);

        [TestCase(9d, 0)]
        [TestCase(10d, 12)]
        [TestCase(100d, 12)]
        [TestCase(101d, 0)]
        public void GroundDepthWindow_DoesNotClampFishOntoAnInvalidGround(double depth, int expected)
        {
            var s = new TrawlTestData.Survey
            {
                Ground = new TrawlGroundSample("ground.test", depth, 0d, Season.EarlySpring),
                Populations = new[] { TrawlTestData.Population(depth: (float)(depth - 1d)) }
            };
            Assert.AreEqual(expected, TrawlTestData.Resolve(s, TrawlTestData.Track(warp: depth * 3d)).Count);
        }

        [TestCase(16.99f, 0)]
        [TestCase(17f, 12)]
        [TestCase(20f, 12)]
        [TestCase(20.01f, 0)]
        public void OnlyTheBottomMouthLayer_IsCaught(float depth, int expected)
        {
            var s = new TrawlTestData.Survey { Populations = new[] { TrawlTestData.Population(depth: depth) } };
            Assert.AreEqual(expected, TrawlTestData.Resolve(s).Count);
        }

        [Test]
        public void OffTrackSchool_AndAnEmptySea_DoNotCreateACatch()
        {
            var s = new TrawlTestData.Survey { Populations = new[] { TrawlTestData.Population(y: 4f) } };
            Assert.AreEqual(0, TrawlTestData.Resolve(s).Count);
            s.Populations = Array.Empty<TrawlPopulationSample>();
            Assert.AreEqual(0, TrawlTestData.Resolve(s).Count);
        }

        [Test]
        public void SeasonAndExplicitTrawlEligibility_AreIndependentGates()
        {
            var s = new TrawlTestData.Survey { Populations = new[] { TrawlTestData.Population(seasons: SeasonMask.HighSummer) } };
            Assert.AreEqual(0, TrawlTestData.Resolve(s).Count);
            s.Ground = new TrawlGroundSample("ground.test", 20d, 0d, Season.HighSummer);
            Assert.AreEqual(12, TrawlTestData.Resolve(s).Count);
            s.Populations = new[] { TrawlTestData.Population(eligible: false) };
            Assert.AreEqual(0, TrawlTestData.Resolve(s).Count);
        }

        [Test]
        public void TargetSelection_LabelsBycatchWithoutChangingWhatEntersTheNet()
        {
            var s = new TrawlTestData.Survey { Populations = new[]
            { TrawlTestData.Population(), TrawlTestData.Population("fish.test_haddock", 2, 5) } };
            var r = TrawlTestData.Resolve(s);
            Assert.AreEqual(17, r.Count); Assert.AreEqual(5, r.BycatchCount); Assert.AreEqual(2, r.LotCount);
            var allBycatch = TrawlTestData.Resolve(s, targets: Array.Empty<string>());
            Assert.AreEqual(17, allBycatch.Count); Assert.AreEqual(17, allBycatch.BycatchCount);
            Assert.AreEqual(r.WeightKg, allBycatch.WeightKg);
        }

        [Test]
        public void ProviderOrder_CannotSelectTheSpeciesThatFillsTheLastCodendSpace()
        {
            var g = TrawlTestData.Gear; g.CodendCapacityIndividuals = 14;
            var s = new TrawlTestData.Survey { Populations = new[]
            { TrawlTestData.Population(), TrawlTestData.Population("fish.test_haddock", 2, 5) } };
            var r = TrawlTestData.Resolve(s, gear: g);
            Assert.AreEqual(14, r.Count); Assert.AreEqual(12, r[0].Count); Assert.AreEqual(2, r[1].Count);
            s.Reverse = true; Same(r, TrawlTestData.Resolve(s, gear: g));
        }

        [Test]
        public void AFullCodend_PreservesEarlierCatchWhenALaterSchoolHasAnEarlierId()
        {
            var g = TrawlTestData.Gear; g.CodendCapacityIndividuals = 5;
            var s = new TrawlTestData.Survey { Populations = new[]
            { TrawlTestData.Population("fish.z_early", x: 2f), TrawlTestData.Population("fish.a_late", 2, x: 18f) } };
            var r = TrawlTestData.Resolve(s, gear: g);
            Assert.AreEqual(5, r.Count); Assert.AreEqual(1, r.LotCount); Assert.AreEqual("fish.z_early", r[0].SpeciesId);
        }

        [Test]
        public void RepeatedPasses_CannotExceedTheSchoolsIndividualsWithinOneTow()
        {
            var track = new TrawlTrack(new[]
            {
                new TrawlTrackKnot(0d, Vector2.zero, 60d, 4d),
                new TrawlTrackKnot(10d, new Vector2(20f, 0f), 60d, 4d),
                new TrawlTrackKnot(20d, Vector2.zero, 60d, 4d),
                new TrawlTrackKnot(30d, new Vector2(20f, 0f), 60d, 4d)
            });
            Assert.AreEqual(12, TrawlTestData.Resolve(track: track).Count);
        }

        [Test]
        public void CornersInsideOneBin_UseTheNetPolylineRatherThanItsDiagonalChord()
        {
            var g = TrawlTestData.Gear; g.SampleGameSeconds = 10d;
            var track = new TrawlTrack(new[]
            {
                new TrawlTrackKnot(0d, Vector2.zero, 60d, 4d),
                new TrawlTrackKnot(5d, new Vector2(10f, 0f), 60d, 4d),
                new TrawlTrackKnot(10d, new Vector2(10f, 10f), 60d, 4d)
            });
            var s = new TrawlTestData.Survey { Populations = new[] { TrawlTestData.Population(x: 10f, y: 5f, radius: 0f) } };
            Assert.AreEqual(20d, track.Distance(0d, 10d));
            var r = TrawlTestData.Resolve(s, track, g);
            Assert.AreEqual(12, r.Count); Assert.AreEqual(80d, r.SweptSquareMetres);
            s.Populations = new[] { TrawlTestData.Population(x: 5f, y: 5f, radius: 0f) };
            Assert.AreEqual(0, TrawlTestData.Resolve(s, track, g).Count);
        }

        [Test]
        public void CollinearTrackSubdivisions_DoNotChangeSamplingOrCatch()
        {
            var split = new TrawlTrack(new[]
            {
                new TrawlTrackKnot(0d, Vector2.zero, 60d, 4d),
                new TrawlTrackKnot(3d, new Vector2(6f, 0f), 60d, 4d),
                new TrawlTrackKnot(7d, new Vector2(14f, 0f), 60d, 4d),
                new TrawlTrackKnot(10d, new Vector2(20f, 0f), 60d, 4d)
            });
            Same(TrawlTestData.Resolve(), TrawlTestData.Resolve(track: split));
        }

        [Test]
        public void AFastDashThenAStop_CannotHideBehindAnInRangeAverageTowSpeed()
        {
            var g = TrawlTestData.Gear; g.SampleGameSeconds = 10d;
            var track = new TrawlTrack(new[]
            {
                new TrawlTrackKnot(0d, Vector2.zero, 60d, 4d),
                new TrawlTrackKnot(1d, new Vector2(20f, 0f), 60d, 4d),
                new TrawlTrackKnot(10d, new Vector2(20f, 0f), 60d, 4d)
            });
            Assert.AreEqual(20d, track.Distance(0d, 10d));
            Assert.AreEqual(0, TrawlTestData.Resolve(track: track, gear: g).Count);
        }

        [Test]
        public void AControlExcursionAtAKnot_CannotHideBehindAnInRangeMidpoint()
        {
            var g = TrawlTestData.Gear; g.SampleGameSeconds = 10d;
            var track = new TrawlTrack(new[]
            {
                new TrawlTrackKnot(0d, Vector2.zero, 60d, 4d),
                new TrawlTrackKnot(1d, new Vector2(2f, 0f), 120d, 4d),
                new TrawlTrackKnot(2d, new Vector2(4f, 0f), 60d, 4d),
                new TrawlTrackKnot(10d, new Vector2(20f, 0f), 60d, 4d)
            });
            Assert.AreEqual(60d, track.At(5d).WarpMetres);
            Assert.AreEqual(0, TrawlTestData.Resolve(track: track, gear: g).Count);
        }

        [Test]
        public void MoreElapsedTrack_CanReachANewSchoolButCannotInventFishOffThatTrack()
        {
            var s = new TrawlTestData.Survey { Populations = new[]
            { TrawlTestData.Population(x: 2f), TrawlTestData.Population("fish.test_later", 2, 5, x: 18f) } };
            Assert.AreEqual(12, TrawlTestData.Resolve(s, TrawlTestData.Track(duration: 4d)).Count);
            Assert.AreEqual(17, TrawlTestData.Resolve(s).Count);
        }

        [Test]
        public void HardBottom_BreaksAtItsActualPositionAndCannotCatchALaterSchool()
        {
            var g = TrawlTestData.Gear; g.SampleGameSeconds = 10d; g.DamagePerHardMetre = 0.2d;
            var s = new TrawlTestData.Survey
            {
                Ground = new TrawlGroundSample("ground.hard", 20d, 1d, Season.EarlySpring),
                Populations = new[] { TrawlTestData.Population(x: 2f, radius: 0f),
                    TrawlTestData.Population("fish.test_late", 2, x: 18f, radius: 0f) }
            };
            var r = TrawlTestData.Resolve(s, gear: g);
            Assert.AreEqual(0d, r.EndIntegrity01); Assert.AreEqual(40d, r.SweptSquareMetres);
            Assert.AreEqual(12, r.Count); Assert.AreEqual(1, r.LotCount);
            Assert.AreEqual("fish.test_cod", r[0].SpeciesId);
        }

        [Test]
        public void ExcessWarpOrSpeed_StillDamagesGearOnHardBottom_WhileShortWarpDoesNot()
        {
            var g = TrawlTestData.Gear; g.DamagePerHardMetre = 0.05d;
            var s = new TrawlTestData.Survey { Ground = new TrawlGroundSample("ground.hard", 20d, 1d, Season.EarlySpring) };
            var r = TrawlTestData.Resolve(s, TrawlTestData.Track(warp: 120d), g);
            Assert.AreEqual(0, r.Count); Assert.AreEqual(0.5d, r.EndIntegrity01, 1e-12d);
            r = TrawlTestData.Resolve(s, TrawlTestData.Track(speed: 4d), g);
            Assert.AreEqual(0, r.Count); Assert.AreEqual(0d, r.EndIntegrity01, 1e-12d);
            r = TrawlTestData.Resolve(s, TrawlTestData.Track(warp: 10d), g);
            Assert.AreEqual(0, r.Count); Assert.AreEqual(1d, r.EndIntegrity01);
        }

        [Test]
        public void ZeroIntegrityOrZeroCaptureEfficiency_NeverCreatesCatch()
        {
            Assert.AreEqual(0, TrawlTestData.Resolve(integrity: 0d).Count);
            var g = TrawlTestData.Gear; g.CaptureFraction01 = 0d;
            Assert.AreEqual(0, TrawlTestData.Resolve(gear: g).Count);
        }

        [Test]
        public void PartialFinalBin_UsesOnlyElapsedTrackDistance()
        {
            var s = new TrawlTestData.Survey { Populations = new[] { TrawlTestData.Population(x: 1f, radius: 0f) } };
            var r = TrawlTestData.Resolve(s, TrawlTestData.Track(duration: 1d));
            Assert.AreEqual(1, r.Samples); Assert.AreEqual(8d, r.SweptSquareMetres); Assert.AreEqual(12, r.Count);
        }

        [Test]
        public void UnknownOrTruncatedSurvey_FailsClosedWithNoPartialCatchOrCommittedWear()
        {
            var s = new TrawlTestData.Survey { UnknownAfter = 7d };
            var r = TrawlTestData.Resolve(s);
            Assert.AreEqual(TrawlTowStatus.UnknownGround, r.Status); Assert.AreEqual(0, r.Count); Assert.AreEqual(1d, r.EndIntegrity01);
            s.UnknownAfter = double.PositiveInfinity; s.ReturnCount = 9;
            r = TrawlTestData.Resolve(s);
            Assert.AreEqual(TrawlTowStatus.BudgetExceeded, r.Status); Assert.AreEqual(0, r.Count);
        }

        [Test]
        public void DuplicateOrInconsistentSchoolIdentity_IsRejectedRatherThanDoubled()
        {
            var s = new TrawlTestData.Survey { Populations = new[] { TrawlTestData.Population(), TrawlTestData.Population() } };
            Assert.AreEqual(TrawlTowStatus.InvalidSurvey, TrawlTestData.Resolve(s).Status);
            s.AtTime = t => new[] { TrawlTestData.Population(count: t < 5d ? 12 : 13) };
            Assert.AreEqual(TrawlTowStatus.InvalidSurvey, TrawlTestData.Resolve(s).Status);
        }

        [Test]
        public void InvalidSpeciesOrGroundFacts_CannotFallBackToATableRoll()
        {
            var s = new TrawlTestData.Survey { Populations = new[] { TrawlTestData.Population(minWeight: double.NaN) } };
            Assert.AreEqual(TrawlTowStatus.InvalidSurvey, TrawlTestData.Resolve(s).Status);
            s.Populations = new[] { TrawlTestData.Population() }; s.Ground = default;
            Assert.AreEqual(TrawlTowStatus.UnknownGround, TrawlTestData.Resolve(s).Status);
        }

        [Test]
        public void AllBudgets_FailExplicitlyAndNeverReturnAnHonestLookingPartialHaul()
        {
            var g = TrawlTestData.Gear; g.MaxSamples = 4;
            var s = new TrawlTestData.Survey();
            Assert.AreEqual(TrawlTowStatus.BudgetExceeded, TrawlTestData.Resolve(s, gear: g).Status);
            Assert.AreEqual(0, s.Calls);
            g = TrawlTestData.Gear; g.MaxTowGameSeconds = 8d;
            Assert.AreEqual(TrawlTowStatus.BudgetExceeded, TrawlTestData.Resolve(gear: g).Status);
            g = TrawlTestData.Gear; g.MaxEncounteredSchools = 1;
            s.Populations = new[] { TrawlTestData.Population(), TrawlTestData.Population("fish.test_haddock", 2) };
            var r = TrawlTestData.Resolve(s, gear: g);
            Assert.AreEqual(TrawlTowStatus.BudgetExceeded, r.Status); Assert.AreEqual(0, r.Count);
        }

        [Test]
        public void SeedTimeAndIdentityReplay_IsExactAcrossOneThousandSeedsAndLongGameTime()
        {
            var g = TrawlTestData.Gear; g.CaptureFraction01 = 0.2d;
            var track = TrawlTestData.Track(start: 1000000000000d);
            var expected = new TrawlTowResult[1000];
            for (int seed = 0; seed < expected.Length; seed++) expected[seed] = TrawlTestData.Resolve(track: track, gear: g, seed: seed);
            for (int seed = expected.Length - 1; seed >= 0; seed--)
            {
                TrawlTestData.Resolve(seed: seed + 1000);
                Same(expected[seed], TrawlTestData.Resolve(track: track, gear: g, seed: seed));
            }
            Assert.AreNotEqual(expected[0].WeightKg, expected[1].WeightKg);
        }

        [Test]
        public void Track_OwnsItsInputAndRejectsNonFiniteOrBackwardsKnots()
        {
            var knots = new[] { new TrawlTrackKnot(0d, Vector2.zero, 60d, 4d),
                new TrawlTrackKnot(10d, new Vector2(20f, 0f), 60d, 4d) };
            var track = new TrawlTrack(knots); knots[1] = knots[0];
            Assert.AreEqual(10d, track.End); Assert.AreEqual(20d, track.Distance(0d, 10d));
            Assert.Throws<ArgumentException>(() => new TrawlTrack(knots));
            Assert.Throws<ArgumentOutOfRangeException>(() => track.At(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => track.Distance(5d, 4d));
            knots[1] = new TrawlTrackKnot(10d, new Vector2(float.NaN, 0f), 60d, 4d);
            Assert.Throws<ArgumentException>(() => new TrawlTrack(knots));
        }
    }
}
