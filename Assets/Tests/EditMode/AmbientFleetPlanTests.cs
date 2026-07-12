using System;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The ambient fisher fleet's PLANNER (canon M2-33, P3): determinism (rule 5 — same
    /// <c>(worldSeed, fleetId, dayIndex)</c> ⇒ the same grounds, every run) and the spring-low depth
    /// gate (no planned spot or travel leg can EVER be shallower than the margin, at any tide phase —
    /// the gate compares against the tide's all-time floor). Pure functions over a synthetic seabed,
    /// headless.
    /// </summary>
    public class AmbientFleetPlanTests
    {
        // A synthetic St-Peters-like seabed: deep floor −4 m, a shoal ridge across the middle rising
        // to +1.6 m (the "sandbar"), and an island disc at (−30,−30). Spring low −3.5 m ⇒ only the
        // deep floor (depth 0.5 m at the floor) clears a 0.4 m margin.
        private const float MinWaterLevel = -3.5f;   // mean 0, amplitude 3.5 — the tide's hard floor
        private const float Margin = 0.4f;

        private static float Elevation(Vector2 p)
        {
            float e = -4f;
            if (Mathf.Abs(p.y) < 5f) e = Mathf.Max(e, 1.6f - Mathf.Abs(p.y) * 0.5f);      // the bar
            float d = Vector2.Distance(p, new Vector2(-30f, -30f));
            if (d < 12f) e = Mathf.Max(e, Mathf.Lerp(6f, -4f, d / 12f));                   // the island
            return e;
        }

        private static readonly Rect Grounds = new Rect(-40f, -45f, 80f, 50f);   // straddles bar + island

        private static Vector2[][] Plan(int seed, int day = 3, int boats = 4, int spots = 2)
            => AmbientFleetPlan.PlanFleet(seed, "fleet.test", day, boats, spots, Grounds,
                                          Elevation, MinWaterLevel, Margin,
                                          spotSpacingMeters: 8f, legSampleStepMeters: 2f, maxTries: 64);

        // ---- determinism (rule 5) --------------------------------------------------------------

        [Test]
        public void SameSeedAndDay_PlansIdenticalFleet()
        {
            var a = Plan(1234);
            var b = Plan(1234);
            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].Length, b[i].Length, $"boat {i} spot count");
                for (int j = 0; j < a[i].Length; j++)
                    Assert.AreEqual(a[i][j], b[i][j], $"boat {i} spot {j} must be bit-identical");
            }
        }

        [Test]
        public void DifferentSeed_PlansDifferentGrounds()
        {
            Assert.IsTrue(AnySpotDiffers(Plan(1234), Plan(99)), "a different world seed must move the fleet");
        }

        [Test]
        public void DifferentDay_PlansDifferentGrounds()
        {
            Assert.IsTrue(AnySpotDiffers(Plan(1234, day: 3), Plan(1234, day: 4)),
                          "fishers shift grounds day to day (spots fold the dayIndex)");
        }

        private static bool AnySpotDiffers(Vector2[][] a, Vector2[][] b)
        {
            for (int i = 0; i < Mathf.Min(a.Length, b.Length); i++)
            {
                if (a[i].Length != b[i].Length) return true;
                for (int j = 0; j < a[i].Length; j++)
                    if ((a[i][j] - b[i][j]).sqrMagnitude > 1e-6f) return true;
            }
            return false;
        }

        // ---- the depth gate ----------------------------------------------------------------------

        [Test]
        public void EveryBoat_GetsItsFullSpotCount_OnTheDeepFloor()
        {
            var fleet = Plan(1234);
            Assert.AreEqual(4, fleet.Length);
            foreach (var spots in fleet)
                Assert.AreEqual(2, spots.Length, "the synthetic grounds have plenty of deep water — no boat should fall short");
        }

        [Test]
        public void NoSpot_IsEverShallowerThanMargin_AtAnyTidePhase()
        {
            var fleet = Plan(1234);
            foreach (var spots in fleet)
                foreach (var p in spots)
                {
                    // The gate's own promise: margin depth at the tide's all-time floor…
                    Assert.GreaterOrEqual(MinWaterLevel - Elevation(p), Margin, $"spot {p} too shallow at spring low");
                    // …which by construction holds at EVERY phase of the tide (level ≥ floor, always).
                    for (int k = 0; k <= 100; k++)
                    {
                        float level = 0f + 3.5f * Mathf.Sin(k * 0.02f * Mathf.PI * 2f);   // any semidiurnal phase
                        Assert.GreaterOrEqual(level - Elevation(p), Margin,
                                              $"spot {p} shallower than the margin at tide level {level}");
                    }
                }
        }

        [Test]
        public void EveryTravelLeg_StaysInPlannableWater_IncludingTheClosingLeg()
        {
            var fleet = Plan(1234);
            foreach (var spots in fleet)
            {
                if (spots.Length < 2) continue;
                for (int j = 0; j < spots.Length; j++)
                {
                    Vector2 from = spots[j];
                    Vector2 to = spots[(j + 1) % spots.Length];   // the work cycle wraps
                    Assert.IsTrue(AmbientFleetPlan.IsLegClear(from, to, Elevation, MinWaterLevel, Margin, 1f),
                                  $"leg {from} → {to} crosses ground that can bare");
                }
            }
        }

        [Test]
        public void SpotsKeepTheFleetWideSpacing()
        {
            var fleet = Plan(1234);
            var all = new System.Collections.Generic.List<Vector2>();
            foreach (var spots in fleet) all.AddRange(spots);
            for (int i = 0; i < all.Count; i++)
                for (int j = i + 1; j < all.Count; j++)
                    Assert.GreaterOrEqual(Vector2.Distance(all[i], all[j]), 8f - 1e-3f,
                                          "two buoy spots planned closer than the fleet spacing");
        }

        [Test]
        public void HardShore_RelaxesTheMarginDeterministically_RatherThanIdlingTheFleet()
        {
            // A seabed where the best water is only 0.2 m at spring low: the 0.4 margin fails
            // everywhere, so the planner must relax (deterministically) and still find work.
            static float Shallow(Vector2 p) => -3.7f;
            var a = AmbientFleetPlan.PlanFleet(7, "fleet.test", 0, 2, 2, Grounds, Shallow, MinWaterLevel,
                                               Margin, 8f, 2f, 64);
            var b = AmbientFleetPlan.PlanFleet(7, "fleet.test", 0, 2, 2, Grounds, Shallow, MinWaterLevel,
                                               Margin, 8f, 2f, 64);
            foreach (var spots in a)
            {
                Assert.AreEqual(2, spots.Length, "the relaxed margin must still yield spots");
                foreach (var p in spots)
                    Assert.Greater(MinWaterLevel - Shallow(p), 0f, "even relaxed, a spot must hold water");
            }
            for (int i = 0; i < a.Length; i++)
                for (int j = 0; j < a[i].Length; j++)
                    Assert.AreEqual(a[i][j], b[i][j], "the fallback must be as deterministic as the happy path");
        }

        // ---- the hash primitive -------------------------------------------------------------------

        [Test]
        public void Hash01_IsStable_InRange_AndSpreads()
        {
            uint seed = AmbientFleetPlan.BoatSeed(42, "fleet.test", 1);
            float a = AmbientFleetPlan.Hash01(seed, 10, 5);
            Assert.AreEqual(a, AmbientFleetPlan.Hash01(seed, 10, 5), "same inputs ⇒ same value, always");
            Assert.That(a, Is.InRange(0f, 0.9999999f));
            Assert.AreNotEqual(a, AmbientFleetPlan.Hash01(seed, 10, 6), "neighbouring indices must diverge");
            Assert.AreNotEqual(a, AmbientFleetPlan.Hash01(seed, 11, 5), "streams must diverge");
        }
    }
}
