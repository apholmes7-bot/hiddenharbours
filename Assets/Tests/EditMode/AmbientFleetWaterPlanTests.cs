using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.App.Editor;   // StPetersBuilder — the authored tide MinWaterLevel derives from

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The ambient fleet's planner on its ACCESSIBLE water (#886): <see cref="AmbientFleetWater"/> and
    /// the <c>AmbientFleetPlan.PlanFleet</c> overload that takes it, over synthetic seabeds, headless. Water
    /// the arrival cannot reach is never ground, spots and legs keep clear of the keep-clear areas, the plan
    /// is a pure function of <c>(worldSeed, fleetId, dayIndex)</c>, and the margin relaxes only where no
    /// water holds it. St Peters itself is <see cref="AmbientFleetGroundsTests"/>' subject.
    /// </summary>
    public class AmbientFleetWaterPlanTests
    {
        private static readonly float MinWaterLevel =
            StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude;   // spring low water
        private const float Margin = 0.4f;
        private const float Deep = -4f;                                  // 1.8 m at spring low

        private static readonly Rect Bounds = new Rect(-100f, -100f, 200f, 200f);
        private static readonly Vector2 Seed = new Vector2(-80f, -80f);
        private const float Cell = 4f;
        private const float Clearance = 14f;
        private const float Spacing = 8f;
        private const float LegStep = 4f;
        private const int Tries = 64;
        private const float MaxLeg = 180f;
        private const float ProbeReach = 6f;
        private const float ProbeSide = 40f;

        private static readonly int[] Seeds = { 12345, 0, 1, 7, 42, 2026, -1, 99991 };
        private const int Days = 8;

        private static float Open(Vector2 p) => Deep;

        // A deep pool inside a ring of dry rock: as deep as the open water, and cut off from it at every tide.
        private static readonly Vector2 PoolCentre = new Vector2(45f, 45f);
        private const float PoolRadius = 22f, WallOuterRadius = 30f;

        private static float Pool(Vector2 p)
        {
            float d = Vector2.Distance(p, PoolCentre);
            return d >= PoolRadius && d <= WallOuterRadius ? 3f : Deep;
        }

        private static AmbientFleetWater Water(Func<Vector2, float> floor, FleetKeepClearArea[] keepClear = null) =>
            AmbientFleetWater.Build(Bounds, Cell, Seed, floor, MinWaterLevel, keepClear, Clearance);

        private static Vector2[][] Plan(AmbientFleetWater water, int seed, int day, int boats = 4, int spots = 2,
                                        float maxLeg = MaxLeg, float[] margins = null) =>
            AmbientFleetPlan.PlanFleet(seed, "fleet.test", day, boats, spots, water, Margin, Spacing, LegStep,
                                       Tries, maxLeg, ProbeReach, ProbeSide, margins);

        private static IEnumerable<(Vector2 From, Vector2 To)> Legs(Vector2[] spots)
        {
            int n = spots.Length;
            if (n < 2) yield break;
            for (int k = 0; k < (n == 2 ? 1 : n); k++) yield return (spots[k], spots[(k + 1) % n]);
        }

        // ---- reach ---------------------------------------------------------------------------------

        [Test]
        public void EnclosedDeepPool_IsNeverReached_NorPlanned()
        {
            Assert.GreaterOrEqual(MinWaterLevel - Pool(PoolCentre), Margin,
                "the pool must be deep enough to fish, or this proves nothing about reach");
            AmbientFleetWater water = Water(Pool);
            Assert.IsFalse(water.IsReachable(PoolCentre, Margin), "the walled pool must not join the seed");
            Assert.IsTrue(water.IsReachable(new Vector2(-40f, 40f), Margin), "the open water must join the seed");

            int spots = 0;
            foreach (int seed in Seeds)
            for (int day = 0; day < Days; day++)
            foreach (Vector2[] boat in Plan(water, seed, day))
            {
                Assert.AreEqual(2, boat.Length, $"seed {seed} day {day}: the open water fills every boat");
                foreach (Vector2 s in boat)
                {
                    spots++;
                    Assert.Greater(Vector2.Distance(s, PoolCentre), WallOuterRadius,
                                   $"seed {seed} day {day}: spot {s} is in the walled pool or on its wall");
                }
            }
            Assert.Greater(spots, 0);
        }

        [Test]
        public void ASeedOnDryGround_ReachesNothing_SoNoBoatIsPlanned()
        {
            // A seed on the rock wall floods nothing, whatever the margin, so no boat gets a spot.
            AmbientFleetWater water = AmbientFleetWater.Build(Bounds, Cell, PoolCentre + Vector2.right * 26f, Pool,
                                                              MinWaterLevel, null, Clearance);
            Assert.AreEqual(0, water.ReachableCount(Margin));
            foreach (Vector2[] boat in Plan(water, 12345, 0))
                Assert.AreEqual(0, boat.Length, "no water joins the seed, so there is nowhere to fish");
        }

        // ---- keeping clear -------------------------------------------------------------------------

        private static readonly FleetKeepClearArea[] Areas =
        {
            new FleetKeepClearArea
            {
                Name = "fairway", Shape = FleetKeepClearShape.Capsule,
                A = new Vector2(-100f, 10f), B = new Vector2(30f, 10f), Radius = 6f,
            },
            new FleetKeepClearArea
            {
                Name = "frame", Shape = FleetKeepClearShape.Box,
                A = new Vector2(20f, -60f), B = new Vector2(70f, -20f),
            },
            new FleetKeepClearArea
            {
                Name = "mark", Shape = FleetKeepClearShape.Capsule,
                A = new Vector2(-50f, -50f), B = new Vector2(-50f, -50f),
            },
        };

        // The test's own distances, never the planner's: a point's by projection, a leg's by dense sampling.
        private static float Gap(FleetKeepClearArea area, Vector2 p)
        {
            Vector2 nearest;
            if (area.Shape == FleetKeepClearShape.Box)
            {
                nearest = Vector2.Max(Vector2.Min(area.A, area.B), Vector2.Min(Vector2.Max(area.A, area.B), p));
            }
            else
            {
                Vector2 ab = area.B - area.A;
                float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - area.A, ab) / ab.sqrMagnitude) : 0f;
                nearest = area.A + ab * t;
            }
            return Vector2.Distance(p, nearest) - area.Radius;
        }

        private static float Gap(FleetKeepClearArea area, Vector2 from, Vector2 to)
        {
            float best = float.MaxValue;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / 0.1f));
            for (int i = 0; i <= steps; i++) best = Mathf.Min(best, Gap(area, Vector2.Lerp(from, to, i / (float)steps)));
            return best;
        }

        [Test]
        public void SpotsAndLegs_StandTheClearanceOffEveryArea_AndInsideTheBounds([Values(2, 3)] int spotsPerBoat)
        {
            AmbientFleetWater water = Water(Open, Areas);
            Rect inner = Rect.MinMaxRect(Bounds.xMin + Clearance, Bounds.yMin + Clearance,
                                         Bounds.xMax - Clearance, Bounds.yMax - Clearance);
            // Sampling finds a leg's nearest approach to within a few centimetres.
            const float slack = 0.06f;

            foreach (int seed in Seeds)
            for (int day = 0; day < Days; day++)
            foreach (Vector2[] boat in Plan(water, seed, day, spots: spotsPerBoat))
            {
                Assert.AreEqual(spotsPerBoat, boat.Length, $"seed {seed} day {day}: the open water fills every boat");
                foreach (Vector2 s in boat)
                {
                    Assert.IsTrue(inner.Contains(s), $"seed {seed} day {day}: spot {s} is inside the clearance band");
                    foreach (FleetKeepClearArea area in Areas)
                        Assert.GreaterOrEqual(Gap(area, s), Clearance - 1e-3f,
                                              $"seed {seed} day {day}: spot {s} is inside {area.Name}'s clearance");
                }
                foreach ((Vector2 from, Vector2 to) in Legs(boat))
                foreach (FleetKeepClearArea area in Areas)
                    Assert.GreaterOrEqual(Gap(area, from, to), Clearance - slack,
                                          $"seed {seed} day {day}: leg {from}→{to} crosses {area.Name}'s clearance");
            }
        }

        [Test]
        public void KeepClearDistances_AreMeasuredToTheShapeNotItsCentre()
        {
            var capsule = new FleetKeepClearArea
            {
                Shape = FleetKeepClearShape.Capsule, A = new Vector2(0f, 0f), B = new Vector2(10f, 0f), Radius = 2f,
            };
            Assert.AreEqual(3f, capsule.DistanceTo(new Vector2(5f, 5f)), 1e-4f, "beside the spine");
            Assert.AreEqual(3f, capsule.DistanceTo(new Vector2(15f, 0f)), 1e-4f, "past the end cap");
            Assert.AreEqual(0f, capsule.DistanceTo(new Vector2(5f, 1f)), 1e-4f, "inside is zero, never negative");
            Assert.AreEqual(0f, capsule.DistanceTo(new Vector2(5f, -9f), new Vector2(5f, 9f)), 1e-4f,
                            "a leg crossing the spine touches it");
            Assert.AreEqual(8f, capsule.DistanceTo(new Vector2(20f, -5f), new Vector2(20f, 5f)), 1e-4f,
                            "a leg beside the end cap");

            var box = new FleetKeepClearArea
            {
                Shape = FleetKeepClearShape.Box, A = new Vector2(10f, 10f), B = new Vector2(0f, 0f),
            };
            Assert.AreEqual(5f, box.DistanceTo(new Vector2(15f, 5f)), 1e-4f, "corners in either order");
            Assert.AreEqual(5f, box.DistanceTo(new Vector2(13f, 14f)), 1e-4f, "off a corner");
            Assert.AreEqual(0f, box.DistanceTo(new Vector2(-5f, 5f), new Vector2(15f, 5f)), 1e-4f,
                            "a leg straight through the box, both ends outside");
            Assert.AreEqual(2f, box.DistanceTo(new Vector2(-5f, 12f), new Vector2(15f, 12f)), 1e-4f,
                            "a leg passing above it");
        }

        // ---- determinism (rule 5) ------------------------------------------------------------------

        [Test]
        public void SameSeedFleetAndDay_PlansTheSameSpots_FromWaterMeasuredAgain()
        {
            Vector2[][] a = Plan(Water(Pool, Areas), 1234, 3);
            Vector2[][] b = Plan(Water(Pool, Areas), 1234, 3);
            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].Length, b[i].Length, $"boat {i} spot count");
                for (int j = 0; j < a[i].Length; j++)
                    Assert.AreEqual(a[i][j], b[i][j], $"boat {i} spot {j} must be bit-identical");
            }

            Vector2[][] otherDay = Plan(Water(Pool, Areas), 1234, 4);
            Vector2[][] otherSeed = Plan(Water(Pool, Areas), 1235, 3);
            Assert.AreNotEqual(a[0][0], otherDay[0][0], "a new day must plan new grounds");
            Assert.AreNotEqual(a[0][0], otherSeed[0][0], "another world must plan other grounds");
        }

        // ---- the margin ----------------------------------------------------------------------------

        [Test]
        public void WhereTheWaterHoldsTheMargin_NoBoatIsRelaxed_AndEveryBoatIsFull()
        {
            AmbientFleetWater water = Water(Pool, Areas);
            foreach (int seed in Seeds)
            for (int day = 0; day < Days; day++)
            {
                var margins = new float[4];
                Vector2[][] plan = Plan(water, seed, day, margins: margins);
                for (int b = 0; b < plan.Length; b++)
                {
                    Assert.AreEqual(2, plan[b].Length, $"seed {seed} day {day} boat {b}: two spots");
                    Assert.AreEqual(Margin, margins[b], $"seed {seed} day {day} boat {b}: planned at the full margin");
                    foreach (Vector2 s in plan[b])
                        Assert.GreaterOrEqual(MinWaterLevel - Pool(s), Margin, $"seed {seed} day {day}: spot {s}");
                }
            }
        }

        [Test]
        public void HardShore_RelaxesTheMarginDeterministically_OnTheAccessibleWater()
        {
            // Water only 0.25 m deep at spring low everywhere: the 0.4 m margin holds nowhere, so the planner
            // halves it once and still fills every boat, the same way every run.
            float shallowBed = MinWaterLevel - 0.25f;
            float Shallow(Vector2 p) => shallowBed;
            var marginsA = new float[2];
            var marginsB = new float[2];
            Vector2[][] a = Plan(Water(Shallow), 7, 0, boats: 2, margins: marginsA);
            Vector2[][] b = Plan(Water(Shallow), 7, 0, boats: 2, margins: marginsB);
            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(2, a[i].Length, "the relaxed margin must still yield spots");
                Assert.AreEqual(Margin * 0.5f, marginsA[i], 1e-6f, "one halving reaches water that holds 0.25 m");
                foreach (Vector2 p in a[i])
                    Assert.GreaterOrEqual(MinWaterLevel - Shallow(p), marginsA[i], "a spot holds its relaxed margin");
                for (int j = 0; j < a[i].Length; j++)
                    Assert.AreEqual(a[i][j], b[i][j], "the fallback must be as deterministic as the happy path");
            }
        }

        // ---- legs and the probe ------------------------------------------------------------------

        [Test]
        public void EveryLeg_IsWithinTheCap_TheClosingLegIncluded([Values(2, 3, 4)] int spotsPerBoat)
        {
            const float cap = 30f;
            AmbientFleetWater water = Water(Open);
            foreach (int seed in Seeds)
            foreach (Vector2[] boat in Plan(water, seed, 0, spots: spotsPerBoat, maxLeg: cap))
            {
                Assert.AreEqual(spotsPerBoat, boat.Length, $"seed {seed}: the open water fills every boat");
                foreach ((Vector2 from, Vector2 to) in Legs(boat))
                    Assert.LessOrEqual(Vector2.Distance(from, to), cap + 1e-3f, $"seed {seed}: leg {from}→{to}");
            }
        }

        [Test]
        public void NoSpot_LiesWithinTheProbeReachOfAStraightShoal()
        {
            // Deep water on one side of a straight edge, 0.2 m at spring low on the other. The edge faces
            // midway between two of the ring's 16 bearings, its worst case: the ring still keeps a spot
            // cos(180° / 16) of the probe's reach off it.
            Vector2 shoalward = new Vector2(Mathf.Cos(Mathf.PI / 16f), Mathf.Sin(Mathf.PI / 16f));
            float HalfShoal(Vector2 p) => Vector2.Dot(p, shoalward) < 0f ? Deep : MinWaterLevel - 0.2f;
            AmbientFleetWater water = Water(HalfShoal);
            float nearest = ProbeReach * Mathf.Cos(Mathf.PI / 16f);
            int near = 0;
            foreach (int seed in Seeds)
            for (int day = 0; day < Days; day++)
            foreach (Vector2[] boat in Plan(water, seed, day))
            foreach (Vector2 s in boat)
            {
                float offEdge = -Vector2.Dot(s, shoalward);
                Assert.GreaterOrEqual(offEdge, nearest - 1e-3f, $"seed {seed} day {day}: spot {s} is within the probe of the shoal");
                if (offEdge < 3f * ProbeReach) near++;
            }
            Assert.Greater(near, 0, "some spots must lie near the shoal, or this proves nothing about the ring");
        }
    }
}
