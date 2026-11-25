using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using Is = NUnit.Framework.Is;
#if UNITY_EDITOR
using UnityEngine.TestTools.Constraints;
#endif

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Reads the real authored YAML, so the same fleet arithmetic runs without an editor.
    /// The separate import test below proves Unity references/factory wiring when CI imports assets.</summary>
    public class FuelBurnBySpeedTests
    {
        public sealed class HullCase
        {
            public string Id;
            public float Capacity, FullBurn, CruiseThrottle;
            public bool HasFuelEngine;
            public FuelSpeedCurve Curve;
            public override string ToString() => Id;
            public IFuelBurnBySpeed Model(float cruiseSpeed = 3.7f) =>
                FuelBurnBySpeed.FromLegacyReference(Curve, cruiseSpeed, FullBurn, CruiseThrottle,
                    HasFuelEngine, ReadLegacySettings());
        }

        private static string Root
        {
            get
            {
                var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets/_Project/Data/Boats")))
                    dir = dir.Parent;
                if (dir == null) throw new DirectoryNotFoundException("Could not locate authored boat data.");
                return dir.FullName;
            }
        }

        private static Dictionary<string, string> Fields(string text) =>
            Regex.Matches(text, @"(?m)^ {2,4}(\w+): *(.*)$").Cast<Match>()
                .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Trim());
        private static float Number(Dictionary<string, string> fields, string key) =>
            float.Parse(fields[key], CultureInfo.InvariantCulture);
        private static string Reference(string value) => Regex.Match(value, @"guid: ([a-f0-9]{32})").Groups[1].Value;
        private static string FileGuid(string path) =>
            Regex.Match(File.ReadAllText(path + ".meta"), @"(?m)^guid: ([a-f0-9]{32})").Groups[1].Value;

        private static FuelSettings ReadLegacySettings()
        {
            string yaml = File.ReadAllText(Path.Combine(Root, "Assets/_Project/Data/Config/GameConfig.asset"));
            var fields = Fields(Regex.Match(yaml, @"(?ms)^  Fuel:\r?\n(.*?)(?=^  \S|\z)").Groups[1].Value);
            return new FuelSettings
            {
                IdleFraction = Number(fields, "IdleFraction"),
                ThrottleExponent = Number(fields, "ThrottleExponent"),
                LoadSurcharge = Number(fields, "LoadSurcharge"),
                HoldLoadWeight = Number(fields, "HoldLoadWeight"),
                SlipLoadWeight = Number(fields, "SlipLoadWeight"),
                SeaSurcharge = Number(fields, "SeaSurcharge"),
                BurnScale = Number(fields, "BurnScale")
            };
        }

        private static HullCase[] LoadFleet()
        {
            string boats = Path.Combine(Root, "Assets/_Project/Data/Boats");
            string code = Path.Combine(Root, "Assets/_Project/Code/Boats");
            string hullScript = FileGuid(Path.Combine(code, "BoatHullDef.cs"));
            string curveScript = FileGuid(Path.Combine(code, "FuelSpeedCurveDef.cs"));
            string profileScript = FileGuid(Path.Combine(code, "FuelSpeedProfileDef.cs"));
            var hulls = new Dictionary<string, Dictionary<string, string>>();
            var curves = new Dictionary<string, Dictionary<string, string>>();
            var profiles = new List<Dictionary<string, string>>();
            var ids = new HashSet<string>();
            var guids = new HashSet<string>();
            foreach (string path in Directory.EnumerateFiles(boats, "*.asset", SearchOption.AllDirectories))
            {
                string yaml = File.ReadAllText(path);
                string script = Reference(Regex.Match(yaml, @"(?m)^  m_Script: (.*)$").Groups[1].Value);
                if (script != hullScript && script != curveScript && script != profileScript) continue;
                var f = Fields(yaml);
                if (!ids.Add(f["Id"])) throw new InvalidDataException("Duplicate content id: " + f["Id"]);
                string guid = FileGuid(path);
                if (guid.Length != 32 || !guids.Add(guid)) throw new InvalidDataException("Missing/duplicate asset GUID.");
                if (script == hullScript) hulls.Add(guid, f);
                else if (script == curveScript) curves.Add(guid, f);
                else profiles.Add(f);
            }
            Assert.AreEqual(2, curves.Count, "One Def for each candidate hydrodynamic class.");
            Assert.AreEqual(46, hulls.Count, "Pinned-main fleet changed: deliberately review content coverage.");
            Assert.AreEqual(hulls.Count, profiles.Count, "Every hull needs exactly one separate binding.");
            CollectionAssert.AreEquivalent(new[] { "fuelcurve.displacement", "fuelcurve.planing" },
                curves.Values.Select(c => c["Id"]));
            foreach (var c in curves.Values)
                Assert.AreEqual(c["Id"] == "fuelcurve.displacement" ? 0f : 1f, Number(c, "HullClass"));
            var seen = new HashSet<string>();
            var result = new List<HullCase>();
            foreach (var p in profiles)
            {
                string hullGuid = Reference(p["Hull"]);
                Assert.IsTrue(seen.Add(hullGuid), "Duplicate hull binding.");
                var h = hulls[hullGuid];
                var c = curves[Reference(p["Curve"])];
                Assert.AreEqual("fuelprofile." + h["Id"].Substring("boat.".Length), p["Id"]);
                float cap = Number(h, "FuelCapacityLitres");
                float burn = Number(h, "FullThrottleLitresPerHour");
                Assert.That(cap, Is.GreaterThanOrEqualTo(0f));
                if (cap > 0f) Assert.That(burn, Is.GreaterThan(0f));
                result.Add(new HullCase
                {
                    Id = h["Id"], Capacity = cap, FullBurn = burn,
                    HasFuelEngine = cap > 0f && Number(h, "Propulsion") != 0f,
                    CruiseThrottle = Number(c, "ReferenceCruiseThrottle"),
                    Curve = new FuelSpeedCurve((FuelSpeedHullClass)(int)Number(c, "HullClass"),
                        Number(c, "DisplacementExponent"), Number(c, "HumpSpeedOverCruise"),
                        Number(c, "PlaningExponent"))
                });
            }
            Assert.AreEqual(32, result.Count(h => h.Curve.HullClass == FuelSpeedHullClass.Displacement));
            Assert.AreEqual(14, result.Count(h => h.Curve.HullClass == FuelSpeedHullClass.Planing));
            return result.OrderBy(h => h.Id, StringComparer.Ordinal).ToArray();
        }

        public static IEnumerable<TestCaseData> EveryHull => LoadFleet().Select(h => new TestCaseData(h));

        [TestCaseSource(nameof(EveryHull))]
        public void Reproduction_MatchesAuthoredCruiseBurnForEveryHullDef(HullCase hull)
        {
            var settings = ReadLegacySettings();
            float expected = hull.HasFuelEngine ? FuelBurn.LitresPerHour(hull.FullBurn,
                hull.CruiseThrottle, 0f, 0f, 0f, settings) : 0f;
            // Multiple supplied reference speeds: calibration is not tied to a guessed fleet speed.
            foreach (float speed in new[] { 0.5f, 3.7f, 25f })
            {
                var model = hull.Model(speed);
                Assert.AreEqual(expected, model.LitresPerHour(speed, true));
                Assert.AreEqual(expected, model.LitresPerHour(-speed, true));
                Assert.AreEqual(0f, model.LitresPerHour(speed, false));
                float idle = hull.HasFuelEngine ? FuelBurn.LitresPerHour(hull.FullBurn, 0f,
                    0f, 0f, 0f, settings) : 0f;
                Assert.AreEqual(idle, model.LitresPerHour(0f, true));
            }
        }

        [TestCaseSource(nameof(EveryHull))]
        public void BurnIsMonotonicInSpeedForEveryHullDef(HullCase hull)
        {
            var model = hull.Model();
            float previous = model.LitresPerHour(0f, true);
            for (int i = 1; i <= 1000; i++)
            {
                float next = model.LitresPerHour(i * 3.7f / 200f, true);
                if (hull.HasFuelEngine) Assert.That(next, Is.GreaterThan(previous), "speed index " + i);
                else Assert.AreEqual(0f, next, "Rowed dory has no fuel engine.");
                previous = next;
            }
        }

        [TestCaseSource(nameof(EveryHull))]
        public void RangeAtHalfSpeedExceedsRangeAtFullSpeedForEveryHullDef(HullCase hull)
        {
            var model = hull.Model();
            float fullSpeed = 3.7f / hull.CruiseThrottle; // comparison scenario, not a claimed measured top
            float halfRate = model.LitresPerHour(fullSpeed * 0.5f, true);
            float fullRate = model.LitresPerHour(fullSpeed, true);
            if (!hull.HasFuelEngine)
            {
                Assert.AreEqual("boat.dory", hull.Id, "Only the rowed dory is fuel-free in the pinned fleet.");
                Assert.AreEqual(0f, hull.Capacity);
                Assert.AreEqual(0f, halfRate);
                Assert.AreEqual(0f, fullRate);
                // Infinity cannot strictly exceed itself. Pin the non-fuel-limited exception explicitly.
                return;
            }
            double halfMetres = hull.Capacity / halfRate * (fullSpeed * 0.5) * 3600d;
            double fullMetres = hull.Capacity / fullRate * fullSpeed * 3600d;
            Assert.That(halfMetres, Is.GreaterThan(fullMetres));
        }

        private static object _allocationSink;
        private static float _burnSink;

        [Test]
        public void ZeroAllocationsPerTick_AcrossEveryHullAndCurveBranch()
        {
            IFuelBurnBySpeed[] models = LoadFleet().Select(h => h.Model()).ToArray();
            Action run = () =>
            {
                float sum = 0f;
                for (int i = 0; i < 20000; i++)
                    sum += models[i % models.Length].LitresBurnedOverTick((i % 37 - 18) * 0.5f,
                        i % 7 != 0, 0.02f);
                _burnSink = sum;
            };
            run(); run(); // JIT, interface dispatch and both curve branches are warm.
#if UNITY_EDITOR
            Assert.That(() => { _allocationSink = new byte[1024]; },
                UnityEngine.TestTools.Constraints.Is.AllocatingGCMemory(), "Allocation recorder positive control.");
            Assert.That(() => run(), UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
#endif
            long probe = GC.GetAllocatedBytesForCurrentThread();
            _allocationSink = new byte[1024];
            long detected = GC.GetAllocatedBytesForCurrentThread() - probe;
#if UNITY_EDITOR
            // Some Unity runtimes expose an inert thread counter; the recorder above remains mandatory.
            if (detected == 0) return;
#else
            Assert.That(detected, Is.GreaterThan(0L), "The native byte counter must detect the positive control.");
#endif
            long before = GC.GetAllocatedBytesForCurrentThread();
            run();
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, bytes);
            Assert.That(_burnSink, Is.GreaterThan(0f), "Busy ticks must actually burn fuel.");
        }

        [Test]
        public void DisplacementCurve_IsCubicAboveThePreservedIdle()
        {
            var model = new FuelBurnBySpeed(new FuelSpeedCurve(FuelSpeedHullClass.Displacement, 3f, 0.6f, 1.5f),
                2f, 10f, 1f);
            Assert.AreEqual(2.125f, model.LitresPerHour(1f, true));
            Assert.AreEqual(73f, model.LitresPerHour(4f, true));
        }

        [Test]
        public void PlaningCurve_IsContinuousAtTheHumpAndHasAFlatterHighSpeedExponent()
        {
            var model = new FuelBurnBySpeed(new FuelSpeedCurve(FuelSpeedHullClass.Planing, 3f, 0.6f, 1.5f),
                1f, 10f, 1f);
            float at = model.LitresPerHour(0.6f, true);
            float left = model.LitresPerHour(0.59999f, true);
            float right = model.LitresPerHour(0.60001f, true);
            Assert.That(right - left, Is.LessThan(0.002f));
            Assert.That((at - left) / (right - at), Is.EqualTo(1f).Within(0.03f));
            double highRatio = (model.LitresPerHour(8f, true) - 1f) /
                               (model.LitresPerHour(4f, true) - 1f);
            Assert.That(highRatio, Is.InRange(2.7d, 3.2d)); // approaches 2^1.5, versus displacement's 8
        }

        [Test]
        public void TickIntegration_IsRepeatableAndIndependentOfStepPartitionAndGameClock()
        {
            var model = LoadFleet().Single(h => h.Id == "boat.lobster_boat").Model();
            float whole = model.LitresBurnedOverTick(3f, true, 3600f);
            Assert.AreEqual(model.LitresPerHour(3f, true), whole);
            Assert.AreEqual(whole, model.LitresBurnedOverTick(3f, true, 3600f));
            double partitioned = 0d;
            for (int i = 0; i < 1000; i++) partitioned += model.LitresBurnedOverTick(3f, true, 3.6f);
            Assert.That(partitioned, Is.EqualTo(whole).Within(0.0001d));
            Assert.AreEqual(0f, model.LitresBurnedOverTick(3f, false, 3600f));
            Assert.AreEqual(0f, model.LitresBurnedOverTick(3f, true, 0f));
        }

        [Test]
        public void InvalidCalibrationAndNonfiniteTickInputsAreRejected()
        {
            var curve = new FuelSpeedCurve(FuelSpeedHullClass.Displacement, 3f, 0.6f, 1.5f);
            Assert.Throws<ArgumentOutOfRangeException>(() => new FuelBurnBySpeed(curve, 0f, 10f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FuelBurnBySpeed(curve, 1f, 1f, 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FuelBurnBySpeed(curve, 1f, float.NaN, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => FuelBurnBySpeed.FromLegacyReference(
                curve, 1f, 0f, 0.7f, true, FuelSettings.Default));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FuelBurnBySpeed(
                new FuelSpeedCurve(FuelSpeedHullClass.Planing, 3f, 0f, 1.5f), 1f, 10f, 1f));
            var model = new FuelBurnBySpeed(curve, 1f, 10f, 1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => model.LitresPerHour(float.NaN, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.LitresBurnedOverTick(1f, true, float.PositiveInfinity));
            Assert.AreEqual(float.MaxValue, model.LitresPerHour(float.MaxValue, true));
        }

#if UNITY_EDITOR
        [Test]
        public void ImportedProfiles_ResolveEveryHullAndFactoryReproducesCruise()
        {
            var settings = ReadLegacySettings();
            var hulls = UnityEditor.AssetDatabase.FindAssets("t:BoatHullDef", new[] { "Assets/_Project/Data" })
                .Select(g => UnityEditor.AssetDatabase.LoadAssetAtPath<BoatHullDef>(UnityEditor.AssetDatabase.GUIDToAssetPath(g)))
                .ToDictionary(h => h.Id);
            var profiles = UnityEditor.AssetDatabase.FindAssets("t:FuelSpeedProfileDef", new[] { "Assets/_Project/Data" })
                .Select(g => UnityEditor.AssetDatabase.LoadAssetAtPath<FuelSpeedProfileDef>(UnityEditor.AssetDatabase.GUIDToAssetPath(g)))
                .ToArray();
            Assert.AreEqual(46, hulls.Count);
            Assert.AreEqual(hulls.Count, profiles.Length);
            var seen = new HashSet<string>();
            foreach (var p in profiles)
            {
                Assert.IsNotNull(p.Hull); Assert.IsNotNull(p.Curve);
                Assert.IsTrue(seen.Add(p.Hull.Id));
                Assert.AreSame(hulls[p.Hull.Id], p.Hull);
                Assert.AreEqual("fuelprofile." + p.Hull.Id.Substring(5), p.Id);
                float expected = p.Hull.FuelCapacityLitres > 0f && p.Hull.Propulsion != PropulsionType.Oars
                    ? FuelBurn.LitresPerHour(p.Hull.FullThrottleLitresPerHour, p.Curve.ReferenceCruiseThrottle,
                        0f, 0f, 0f, settings) : 0f;
                Assert.AreEqual(expected, p.CreateModel(3.7f, settings).LitresPerHour(3.7f, true));
            }
        }
#endif
    }
}
