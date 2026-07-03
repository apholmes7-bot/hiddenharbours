using System.Reflection;
using HiddenHarbours.Boats;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Pins the owner's B2 feel pass (2026-07-03): the visual rock's default motion DOUBLED
    /// ("pretty subtle and could likely be doubled in overall movement length") and a short
    /// fps-independent output smoothing added ("should be a smooth rock"). The serialized defaults
    /// are what every builder-spawned boat gets, so a silent regression here is the owner's tuning
    /// silently undone — reflection-pinned by name (the names are the Inspector-override contract).
    /// </summary>
    public class BoatWaveMotionDefaultsTests
    {
        private static float ReadFloatDefault(string fieldName)
        {
            var go = new GameObject("BoatWaveMotionDefaultsProbe");
            try
            {
                var motion = go.AddComponent<BoatWaveMotion>();
                FieldInfo field = typeof(BoatWaveMotion).GetField(
                    fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(field,
                    $"serialized tunable '{fieldName}' is gone — renaming it silently discards the " +
                    "owner's Inspector overrides (the feel-pass contract keeps the names stable)");
                return (float)field.GetValue(motion);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [TestCase("_rollDegreesPerSlope", 16f, TestName = "Doubled defaults: roll °/slope 8 → 16")]
        [TestCase("_maxRollDegrees", 9f, TestName = "Doubled defaults: max roll 4.5 → 9°")]
        [TestCase("_pitchOffsetPerSlope", 0.1f, TestName = "Doubled defaults: pitch offset/slope 0.05 → 0.1")]
        [TestCase("_maxPitchOffset", 0.16f, TestName = "Doubled defaults: max pitch offset 0.08 → 0.16")]
        [TestCase("_pitchSquashPerSlope", 0.1f, TestName = "Doubled defaults: pitch squash/slope 0.05 → 0.1")]
        [TestCase("_maxPitchSquash", 0.12f, TestName = "Doubled defaults: max pitch squash 0.06 → 0.12")]
        [TestCase("_bobPerHeightMeter", 0.12f, TestName = "Doubled defaults: bob/metre 0.06 → 0.12")]
        [TestCase("_maxBob", 0.2f, TestName = "Doubled defaults: max bob 0.1 → 0.2")]
        [TestCase("_motionSmoothingSeconds", 0.2f, TestName = "Smoothing default: 0.2 s output damping")]
        public void SerializedDefault_IsPinned(string fieldName, float expected)
        {
            Assert.AreEqual(expected, ReadFloatDefault(fieldName), 1e-6f,
                $"'{fieldName}' default drifted from the owner's 2026-07-03 feel pass");
        }
    }
}
