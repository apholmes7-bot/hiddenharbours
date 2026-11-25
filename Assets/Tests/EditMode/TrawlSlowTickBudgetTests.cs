using System.Diagnostics;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class TrawlSlowTickBudgetTests
    {
        private const int WarmupSolves = 4;
        private const int MeasuredSolves = 32;
        private const double MaxMeanMilliseconds = 10d;

        [Test]
        public void AuthoredCaps_CompleteTheStressWorkload_WithinTheBatchBudget()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TrawlGearAssetDef>(
                TrawlContentValidationTests.Root + "BottomOtterPrototype.asset");
            Assert.IsNotNull(asset);
            double mean = AssertStressBudget(asset.Spec);
            TestContext.WriteLine($"TRAWL_BUDGET solves={MeasuredSolves} bins=32 schoolsPerBin=16 "
                + $"encounters=32 knots=64 meanMs={mean:F4} limitMs={MaxMeanMilliseconds}");
        }

        // Shared with the no-editor measurement harness. It passes the exact text-authored Spec;
        // this helper passing cannot prove native asset import or count as the NUnit case above.
        public static double AssertStressBudget(TrawlGearDef gear)
        {
            Assert.IsTrue(gear.IsValid);
            Assert.AreEqual(32, gear.MaxSamples);
            Assert.AreEqual(16, gear.MaxSchoolsPerSample);
            Assert.AreEqual(32, gear.MaxEncounteredSchools);
            Assert.AreEqual(64, gear.MaxTrackKnots);
            // Stress capture and full-duration work, regardless of the inactive content's zero yield.
            gear.CaptureFraction01 = 1d;
            gear.DamagePerHardMetre = 0d;
            double speed = (gear.MinTowMetresPerGameSecond + gear.MaxTowMetresPerGameSecond) / 2d;
            var knots = new TrawlTrackKnot[gear.MaxTrackKnots];
            for (int i = 0; i < knots.Length; i++)
            {
                double time = gear.MaxTowGameSeconds * i / (knots.Length - 1d);
                knots[i] = new TrawlTrackKnot(time, new Vector2((float)(speed * time), 0f),
                    20d * gear.MinWarpDepthRatio, gear.MaxDoorSpreadMetres);
            }
            var track = new TrawlTrack(knots);
            var survey = new DenseSurvey(gear.SampleGameSeconds, gear.MaxSchoolsPerSample);
            string[] targets = { "fish.budget_fixture" };
            for (int i = 0; i < WarmupSolves; i++)
                TrawlTowSolver.Resolve(gear, 42, "tow.budget_fixture", track, survey, targets, 1d);
            int count = 0, samples = 0;
            TrawlTowResult last = null;
            var timer = Stopwatch.StartNew();
            for (int i = 0; i < MeasuredSolves; i++)
            {
                last = TrawlTowSolver.Resolve(gear, 42, "tow.budget_fixture", track, survey, targets, 1d);
                count += last.Count;
                samples += last.Samples;
            }
            timer.Stop();
            double mean = timer.Elapsed.TotalMilliseconds / MeasuredSolves;
            Assert.AreEqual(TrawlTowStatus.Complete, last.Status);
            Assert.AreEqual(MeasuredSolves * 32, count);
            Assert.AreEqual(MeasuredSolves * gear.MaxSamples, samples);
            Assert.AreEqual(32, last.LotCount);
            Assert.AreEqual(0, last.BycatchCount);
            Assert.Less(mean, MaxMeanMilliseconds,
                "Unwired batch solve regressed; this excludes survey acquisition and live integration.");
            return mean;
        }

        private sealed class DenseSurvey : ITrawlSurvey
        {
            private readonly double _interval;
            private readonly int _count;
            internal DenseSurvey(double interval, int count) { _interval = interval; _count = count; }
            public int Fill(TrawlTrack track, double from, double to, double halfWidth, double time,
                TrawlPopulationSample[] into, out TrawlGroundSample ground)
            {
                ground = new TrawlGroundSample("ground.budget_fixture", 20d, 0d, Season.EarlySpring);
                int offset = ((int)(from / _interval) % 2) * _count;
                var centre = track.At(time).NetCentre;
                for (int i = 0; i < _count; i++)
                {
                    var school = new SchoolFieldSample("fish.budget_fixture", (uint)(offset + i + 1),
                        centre, 19f, 0f, 1f, 1, 0d);
                    var facts = new TrawlSpeciesFacts("fish.budget_fixture", SeasonMask.AllYear, 1d, 1d, true);
                    into[i] = new TrawlPopulationSample(school, facts);
                }
                return _count;
            }
        }
    }
}
