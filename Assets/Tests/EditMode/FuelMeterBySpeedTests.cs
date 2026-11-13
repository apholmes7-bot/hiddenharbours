using System;
using System.Collections.Generic;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = NUnit.Framework.Is;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE LIVE METER BURNS BY SPEED: the owner's 11-12 ruling, wired
    /// (<c>docs/design/fuel-burn-by-speed-logic.md</c>, "Live wiring").
    ///
    /// <para>Everything here drives the real <see cref="BoatFuelTank"/> over the SHIPPED hull, curve
    /// and profile Defs: <see cref="BoatFuelTank.LitresPerHourAt"/> and
    /// <see cref="BoatFuelTank.BurnStep"/> are the whole of what the fixed tick runs. The arithmetic of
    /// the curve itself is <c>FuelBurnBySpeedTests</c>' job; this suite holds the WIRING: which speed
    /// goes in, which calibration it is read against, and what that does to a tank.</para>
    ///
    /// <para><b>"Before"</b> throughout is the throttle curve this replaces: flat out she burned her
    /// Def's <c>FullThrottleLitresPerHour</c>, whatever speed that throttle produced.</para>
    /// </summary>
    public class FuelMeterBySpeedTests
    {
        private const string DataRoot = "Assets/_Project/Data";

        /// <summary>The 11-12 speed table's ruled top speeds (m/s) for the thirteen planing hulls it
        /// moves (PR 1168). Held here as the scenario the ruling was made for, so the proof does not
        /// wait on that PR's assets and does not change when they land.</summary>
        private static readonly (string Id, float TopSpeedMps)[] RuledTopSpeeds =
        {
            ("boat.bowrider", 9.75f), ("boat.console_skiff", 8.54f), ("boat.sport_skiff", 8.54f),
            ("boat.sport_skiff_mk2", 8.54f), ("boat.sport_skiff_twin", 8.54f),
            ("boat.express_cruiser", 10.42f), ("boat.motor_yacht_84", 6.25f),
            ("boat.sport_fisher_convertible", 9.26f), ("boat.sport_fisher_skybridge", 8.80f),
            ("boat.yacht_tender", 10.14f), ("boat.zodiac_frc", 10.14f),
            ("boat.zodiac_hurricane", 10.14f), ("boat.go_fast", 10.00f),
        };

        /// <summary>The approved planing curve prices full speed, at the speed a hull was calibrated
        /// for, at 0.946 of her old flat-out rate (the design doc's own table). So with her speed
        /// unchanged a planing hull's flat-out range may be up to 1/0.946 of before, and no more.</summary>
        private const float PlaningFlatOutAllowance = 1.06f;

        /// <summary>From this much faster than her calibration, a planing hull flat out covers no more
        /// ground per tank than she used to. Every hull the speed table moves is past it.</summary>
        private const float SpeedUpPastWhichRangeShrinks = 1.11f;

        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            GameServices.ActiveBoatFuel = null;
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
        }

        // ---- the rig ------------------------------------------------------------------------------

        private static List<BoatHullDef> Hulls()
        {
            var list = new List<BoatHullDef>();
            foreach (string guid in AssetDatabase.FindAssets("t:BoatHullDef", new[] { DataRoot }))
            {
                var h = AssetDatabase.LoadAssetAtPath<BoatHullDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (h != null && !string.IsNullOrEmpty(h.Id)) list.Add(h);
            }
            Assert.That(list.Count, Is.GreaterThanOrEqualTo(46), "the shipped fleet did not load");
            return list;
        }

        private static BoatHullDef ById(string id)
        {
            foreach (var h in Hulls())
                if (h.Id == id) return h;
            Assert.Fail($"{id} is not in Data/: this suite names shipped hulls by their stable ids");
            return null;
        }

        /// <summary>The hulls the live meter actually drains: an engine and a tank.</summary>
        private static List<BoatHullDef> MeteredHulls()
        {
            var list = Hulls();
            list.RemoveAll(h => h.Propulsion != PropulsionType.Engine || !(h.FuelCapacityLitres > 0f));
            return list;
        }

        private static FuelSpeedProfileDef ProfileOf(BoatHullDef hull)
        {
            FuelSpeedProfileLibrary library = FuelSpeedProfileLibrary.Shared;
            Assert.IsNotNull(library, "Resources/FuelSpeedProfileLibrary did not load");
            FuelSpeedProfileDef profile = library.Find(hull.Id);
            Assert.IsNotNull(profile, $"{hull.Id} has no entry in the fuel speed profile library");
            return profile;
        }

        private static bool IsPlaning(BoatHullDef hull) =>
            ProfileOf(hull).Curve.HullClass == FuelSpeedHullClass.Planing;

        private static float TopSpeed(BoatHullDef h) =>
            FuelSpeedMeter.StillWaterTopSpeedMps(h.EnginePower, h.ForwardDrag, h.MassKg);

        /// <summary>The still-water top speed her fuel numbers were calibrated at.</summary>
        private static float CalibratedTopSpeed(BoatHullDef h)
        {
            FuelSpeedProfileDef p = ProfileOf(h);
            return p.ReferenceCruiseSpeedMps / p.Curve.ReferenceCruiseThrottle;
        }

        /// <summary>A runtime copy of a shipped hull, re-engined to settle at the given top speed.
        /// Thrust alone is moved, which is how the speed table applies a ruled speed.</summary>
        private BoatHullDef ReEngined(BoatHullDef shipped, float topSpeedMps)
        {
            BoatHullDef copy = Object.Instantiate(shipped);
            _spawned.Add(copy);
            copy.EnginePower = shipped.EnginePower * (topSpeedMps / TopSpeed(shipped));
            Assert.AreEqual(topSpeedMps, TopSpeed(copy), 1e-3f);
            return copy;
        }

        /// <summary>A bare, brim-full tank pointed at a hull: the BoatFuelTankTests rig.</summary>
        private BoatFuelTank Tank(BoatHullDef hull)
        {
            var go = new GameObject("boat");
            _spawned.Add(go);
            var tank = go.AddComponent<BoatFuelTank>();
            tank.Configure(hull);
            tank.SetLitres(hull.FuelCapacityLitres);
            return tank;
        }

        /// <summary>Metres a full tank carries her at a steady throttle, calm and light.</summary>
        private float RangeMetres(BoatHullDef hull, float throttle)
        {
            float rate = Tank(hull).LitresPerHourAt(throttle, 0f, 0f);
            Assert.That(rate, Is.GreaterThan(0f), hull.Id);
            return hull.FuelCapacityLitres / rate * FuelBurn.SecondsPerHour *
                   FuelSpeedMeter.DrivenSpeedMps(throttle, TopSpeed(hull));
        }

        /// <summary>The curve's own shape, written out again from the design doc so the wiring is
        /// checked against the doc and not against itself.</summary>
        private static double Shape(FuelSpeedCurveDef c, double x)
        {
            double n = c.DisplacementExponent, h = c.HumpSpeedOverCruise, p = c.PlaningExponent;
            if (c.HullClass == FuelSpeedHullClass.Displacement || x <= h) return Math.Pow(x, n);
            return Math.Pow(h, n) + n / p * Math.Pow(h, n - p) * (Math.Pow(x, p) - Math.Pow(h, p));
        }

        private static float ExpectedCalmRate(BoatHullDef hull, float throttle)
        {
            FuelSpeedProfileDef p = ProfileOf(hull);
            FuelSettings cfg = GameServices.Fuel;
            float r = hull.FullThrottleLitresPerHour;
            double idle = FuelBurn.LitresPerHour(r, 0f, 0f, 0f, 0f, cfg);
            double cruise = FuelBurn.LitresPerHour(r, p.Curve.ReferenceCruiseThrottle, 0f, 0f, 0f, cfg);
            double x = Math.Abs(throttle) * TopSpeed(hull) / p.ReferenceCruiseSpeedMps;
            return (float)(idle + (cruise - idle) * Shape(p.Curve, x) / Shape(p.Curve, 1d));
        }

        // ---- the binding --------------------------------------------------------------------------

        [Test]
        public void TheLibrary_BindsEveryHull_AndEveryFuelledEngineIsCalibrated()
        {
            FuelSpeedProfileLibrary library = FuelSpeedProfileLibrary.Shared;
            Assert.IsNotNull(library, "Resources/FuelSpeedProfileLibrary did not load");

            int profileAssets = AssetDatabase.FindAssets("t:FuelSpeedProfileDef", new[] { DataRoot }).Length;
            Assert.AreEqual(profileAssets, library.Profiles.Length,
                "every profile asset must be listed in the library, or its hull silently keeps the throttle curve");
            foreach (FuelSpeedProfileDef p in library.Profiles)
                Assert.IsNotNull(p, "the library has a null entry");

            foreach (BoatHullDef h in Hulls())
            {
                FuelSpeedProfileDef p = ProfileOf(h);
                Assert.AreSame(h, p.Hull, h.Id);
                bool fuelled = h.FuelCapacityLitres > 0f && h.Propulsion != PropulsionType.Oars;
                if (fuelled)
                    Assert.IsTrue(p.IsCalibrated, $"{h.Id} carries fuel but has no reference cruise speed");
                else
                    Assert.AreEqual(0f, p.ReferenceCruiseSpeedMps, $"{h.Id} has no fuel engine to calibrate");
            }
        }

        // ---- speed costs fuel ---------------------------------------------------------------------

        [Test]
        public void AtFullSpeed_SheBurnsMorePerHourThanAtHalf_PerHerCurve()
        {
            foreach (BoatHullDef h in MeteredHulls())
            {
                BoatFuelTank tank = Tank(h);
                float idle = tank.LitresPerHourAt(0f, 0f, 0f);
                float half = tank.LitresPerHourAt(0.5f, 0f, 0f);
                float full = tank.LitresPerHourAt(1f, 0f, 0f);

                Assert.That(half, Is.GreaterThan(idle), $"{h.Id}: half speed must cost more than idling");
                Assert.That(full, Is.GreaterThan(half), $"{h.Id}: full speed must cost more than half");
                Assert.AreEqual(ExpectedCalmRate(h, 0.5f), half, half * 1e-3f, $"{h.Id} at half speed");
                Assert.AreEqual(ExpectedCalmRate(h, 1f), full, full * 1e-3f, $"{h.Id} at full speed");
                Assert.AreEqual(full, tank.LitresPerHourAt(-1f, 0f, 0f), $"{h.Id}: astern is charged as ahead");
            }
        }

        [Test]
        public void EasingOff_BuysDistance_OnEveryHull()
        {
            foreach (BoatHullDef h in MeteredHulls())
                Assert.That(RangeMetres(h, 0.5f), Is.GreaterThan(RangeMetres(h, 1f)),
                    $"{h.Id}: half speed must carry her further on a tank than flat out");
        }

        [Test]
        public void Idle_AndTheCalibratedCruiseSpeed_CostWhatTheyAlwaysDid()
        {
            FuelSettings cfg = GameServices.Fuel;
            foreach (BoatHullDef h in MeteredHulls())
            {
                FuelSpeedProfileDef p = ProfileOf(h);
                BoatFuelTank tank = Tank(h);
                float r = h.FullThrottleLitresPerHour;

                float oldIdle = FuelBurn.LitresPerHour(r, 0f, 0f, 0f, 0f, cfg);
                Assert.AreEqual(oldIdle, tank.LitresPerHourAt(0f, 0f, 0f), oldIdle * 1e-5f,
                    $"{h.Id}: ticking over costs what it did; no idle rule was invented");

                // The throttle that drives her at exactly the speed she was calibrated for.
                float throttle = p.ReferenceCruiseSpeedMps / TopSpeed(h);
                Assert.That(throttle, Is.LessThanOrEqualTo(1f), $"{h.Id} cannot reach her calibrated cruise speed");
                float oldCruise = FuelBurn.LitresPerHour(r, p.Curve.ReferenceCruiseThrottle, 0f, 0f, 0f, cfg);
                Assert.AreEqual(oldCruise, tank.LitresPerHourAt(throttle, 0f, 0f), oldCruise * 1e-3f,
                    $"{h.Id}: at her calibrated cruise speed she burns her old cruise rate");
            }
        }

        [Test]
        public void AFullHoldAndARoughSea_StillCostTheSameSurcharge()
        {
            FuelSettings cfg = GameServices.Fuel;
            float oldSurcharge = FuelBurn.LitresPerHour(100f, 0.6f, 1f, 0f, 1f, cfg) /
                                 FuelBurn.LitresPerHour(100f, 0.6f, 0f, 0f, 0f, cfg);
            Assert.AreEqual(oldSurcharge, FuelBurn.DutySurcharge(1f, 0f, 1f, cfg), 1e-5f,
                "the split-out surcharge must be the throttle curve's own load and sea terms");
            Assert.AreEqual(1f, FuelBurn.DutySurcharge(0f, 0f, 0f, cfg), "light and calm is no surcharge");
            Assert.That(oldSurcharge, Is.GreaterThan(1f));

            BoatFuelTank tank = Tank(ById("boat.lobster_boat"));
            float calm = tank.LitresPerHourAt(0.6f, 0f, 0f);
            Assert.AreEqual(calm * oldSurcharge, tank.LitresPerHourAt(0.6f, 1f, 1f), calm * 1e-4f,
                "load and sea must not quietly disappear from the speed meter");
        }

        // ---- range --------------------------------------------------------------------------------

        [Test]
        public void ADisplacementHull_FlatOut_GoesNoFurtherOnATankThanBefore()
        {
            foreach (BoatHullDef h in MeteredHulls())
            {
                if (IsPlaning(h)) continue;
                float before = h.FuelCapacityLitres / h.FullThrottleLitresPerHour *
                               FuelBurn.SecondsPerHour * TopSpeed(h);
                Assert.That(RangeMetres(h, 1f), Is.LessThanOrEqualTo(before),
                    $"{h.Id}: pushing a displacement hull flat out must not be cheaper than it was");
            }
        }

        [Test]
        public void APlaningHull_FlatOut_NeverOutrangesTheThrottleMeter_BeyondTheCurvesOwnMargin()
        {
            foreach (BoatHullDef h in MeteredHulls())
            {
                if (!IsPlaning(h)) continue;
                float hoursBefore = h.FuelCapacityLitres / h.FullThrottleLitresPerHour;
                float after = RangeMetres(h, 1f);

                // Against her old self: the throttle meter, at the speed she was calibrated for.
                float before = hoursBefore * FuelBurn.SecondsPerHour * CalibratedTopSpeed(h);
                Assert.That(after, Is.LessThanOrEqualTo(before * PlaningFlatOutAllowance),
                    $"{h.Id}: flat-out range grew past what the approved planing curve allows");
                if (TopSpeed(h) >= CalibratedTopSpeed(h) * SpeedUpPastWhichRangeShrinks)
                    Assert.That(after, Is.LessThanOrEqualTo(before),
                        $"{h.Id} is faster than she was and must not also go further on a tank");

                // Against the throttle meter at the speed she makes TODAY: the thing this replaces.
                float throttleMeter = hoursBefore * FuelBurn.SecondsPerHour * TopSpeed(h);
                Assert.That(after, Is.LessThanOrEqualTo(throttleMeter * PlaningFlatOutAllowance), h.Id);
            }
        }

        [Test]
        public void AtTheSpeedTablesRuledSpeeds_NoPlaningHullGoesFurtherFlatOutThanSheUsedTo()
        {
            foreach ((string id, float ruled) in RuledTopSpeeds)
            {
                BoatHullDef shipped = ById(id);
                Assert.IsTrue(IsPlaning(shipped), $"{id} is in the speed table but not on the planing curve");
                BoatHullDef fast = ReEngined(shipped, ruled);

                float hours = shipped.FuelCapacityLitres / shipped.FullThrottleLitresPerHour;
                float before = hours * FuelBurn.SecondsPerHour * CalibratedTopSpeed(shipped);
                float throttleMeterAtRuledSpeed = hours * FuelBurn.SecondsPerHour * ruled;
                float after = RangeMetres(fast, 1f);

                Assert.That(after, Is.LessThanOrEqualTo(before),
                    $"{id} at {ruled} m/s: flat out she must not out-range her old self on the same tank");
                Assert.That(after, Is.LessThan(throttleMeterAtRuledSpeed),
                    $"{id} at {ruled} m/s: the speed meter must cost her more than the throttle meter would");
                Assert.That(Tank(fast).LitresPerHourAt(1f, 0f, 0f),
                    Is.GreaterThan(shipped.FullThrottleLitresPerHour),
                    $"{id} at {ruled} m/s: flat out she must burn more per hour than her old flat-out rate");
            }
        }

        [Test]
        public void ABiggerEngine_BurnsMoreAtTheSameThrottle_AndIdlesTheSame()
        {
            BoatHullDef shipped = ById("boat.go_fast");
            BoatHullDef fast = ReEngined(shipped, TopSpeed(shipped) * 1.5f);
            BoatFuelTank slowTank = Tank(shipped), fastTank = Tank(fast);

            Assert.That(fastTank.LitresPerHourAt(1f, 0f, 0f), Is.GreaterThan(slowTank.LitresPerHourAt(1f, 0f, 0f)));
            Assert.That(fastTank.LitresPerHourAt(0.5f, 0f, 0f), Is.GreaterThan(slowTank.LitresPerHourAt(0.5f, 0f, 0f)));
            Assert.AreEqual(slowTank.LitresPerHourAt(0f, 0f, 0f), fastTank.LitresPerHourAt(0f, 0f, 0f));
        }

        // ---- determinism --------------------------------------------------------------------------

        /// <summary>A fixed, clockless sequence of duties: throttle sweeps both ways through zero, the
        /// hold fills, the sea gets up. No randomness, so the run is its own seed.</summary>
        private static float RunSequence(BoatFuelTank tank, int steps)
        {
            float used = 0f;
            for (int i = 0; i < steps; i++)
            {
                float throttle = (i * 37 % 201 - 100) / 100f;
                float hold = i % 50 / 49f;
                float sea = i % 7 / 6f;
                used += tank.BurnStep(throttle, hold, sea, 0.02f);
            }
            return used;
        }

        [Test]
        public void TheDrain_IsDeterministic_ForAFixedSequenceOfDuties()
        {
            foreach (string id in new[] { "boat.go_fast", "boat.lobster_boat", "boat.dory_outboard" })
            {
                BoatHullDef h = ById(id);
                BoatFuelTank a = Tank(h), b = Tank(h);
                float usedA = RunSequence(a, 3000);
                float usedB = RunSequence(b, 3000);

                Assert.That(usedA, Is.GreaterThan(0f), $"{id}: the sequence must actually burn fuel");
                Assert.AreEqual(usedA, usedB, $"{id}: the same duties in the same order must burn the same litres, to the bit");
                Assert.AreEqual(a.Litres, b.Litres, $"{id}: and leave the same level in the tank");
                Assert.That(a.Litres, Is.LessThan(h.FuelCapacityLitres));
                Assert.AreEqual(h.FuelCapacityLitres - usedA, a.Litres, 1e-2f, $"{id}: every litre burned left the tank");

                // And again on the SAME tank, refilled: the meter keeps no hidden state between runs.
                a.SetLitres(h.FuelCapacityLitres);
                Assert.AreEqual(usedA, RunSequence(a, 3000), $"{id}: a second run on the same tank must match the first");
            }
        }

        [Test]
        public void ADryTank_BurnsNothing_AndATankNeverGoesBelowEmpty()
        {
            BoatHullDef h = ById("boat.dory_outboard");
            BoatFuelTank tank = Tank(h);
            tank.SetLitres(0.001f);
            float used = tank.BurnStep(1f, 0f, 0f, 3600f);
            Assert.AreEqual(0.001f, used, 1e-6f, "she can only burn what is aboard");
            Assert.AreEqual(0f, tank.Litres);
            Assert.AreEqual(0f, tank.BurnStep(1f, 0f, 0f, 3600f), "a dry tank burns nothing");
        }

        // ---- what does NOT change -------------------------------------------------------------------

        [Test]
        public void AHullWithNoProfile_KeepsTheThrottleCurve_Exactly()
        {
            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            _spawned.Add(h);
            h.Id = "boat.test_unprofiled";
            h.Propulsion = PropulsionType.Engine;
            h.FuelCapacityLitres = 100f;
            h.FuelGrade = FuelGrades.Gas;
            h.FullThrottleLitresPerHour = 60f;

            BoatFuelTank tank = Tank(h);
            FuelSettings cfg = GameServices.Fuel;
            foreach (float throttle in new[] { 0f, 0.3f, 0.7f, 1f, -0.5f })
                Assert.AreEqual(FuelBurn.LitresPerHour(60f, throttle, 0.4f, 0f, 0.5f, cfg),
                    tank.LitresPerHourAt(throttle, 0.4f, 0.5f),
                    $"an un-profiled hull at throttle {throttle} must meter exactly as it always did");
        }

        [Test]
        public void TheRowedDory_StillBurnsNothing()
        {
            BoatHullDef dory = ById("boat.dory");
            var go = new GameObject("dory");
            _spawned.Add(go);
            var tank = go.AddComponent<BoatFuelTank>();
            tank.Configure(dory);

            Assert.AreEqual(0f, tank.LitresPerHourAt(1f, 1f, 1f));
            Assert.AreEqual(0f, tank.BurnStep(1f, 1f, 1f, 3600f));
        }

        // ---- the fixed tick allocates nothing -------------------------------------------------------

        private static float _sink;

        [Test]
        public void TheWarmedBurnStep_AllocatesNothing()
        {
            BoatFuelTank planing = Tank(ById("boat.go_fast"));
            BoatFuelTank displacement = Tank(ById("boat.tanker"));
            Action run = () =>
            {
                float sum = 0f;
                for (int i = 0; i < 2000; i++)
                {
                    float throttle = (i * 37 % 201 - 100) / 100f;
                    sum += planing.BurnStep(throttle, 0.5f, 0.25f, 0.02f);
                    sum += displacement.BurnStep(throttle, 0.5f, 0.25f, 0.02f);
                }
                _sink = sum;
            };
            run(); run();   // the model is built, the profile found and both curve branches are warm

            Assert.That(() => run(), UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory(),
                "the live burn runs on the fixed tick and must not allocate once warm");
            Assert.That(_sink, Is.GreaterThan(0f), "busy ticks must actually burn fuel");
        }
    }
}
