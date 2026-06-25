using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Pure-logic tests for the art-rendering components (VS-24). These cover the deterministic mapping
    /// functions only — the MonoBehaviours read the live sim through GameServices at runtime.
    /// </summary>
    public class ArtRenderingTests
    {
        [Test]
        public void Waterline_RecedesAtLow_FloodsAtHigh()
        {
            var dir = Vector2.up; // beach faces north: rising tide pushes the waterline +Y onto land
            var low  = TideShoreline.WaterlineOffset(-1.6f, 3f, dir);
            var mid  = TideShoreline.WaterlineOffset(0f,    3f, dir);
            var high = TideShoreline.WaterlineOffset(1.6f,  3f, dir);

            Assert.AreEqual(0f, mid.magnitude, 1e-4f, "mean tide sits at the anchor");
            Assert.Greater(high.y, 0f, "high tide floods along the flood direction");
            Assert.Less(low.y, 0f, "low tide recedes opposite the flood direction");
            Assert.Greater(high.y, low.y, "higher tide => waterline further onto land");
            Assert.AreEqual(1.6f * 3f, high.y, 1e-3f, "offset == tideHeight * slope along the flood axis");
        }

        [Test]
        public void Waterline_FollowsFloodDirection()
        {
            var off = TideShoreline.WaterlineOffset(2f, 1f, new Vector2(1f, 0f)); // east-facing beach
            Assert.AreEqual(2f, off.x, 1e-4f);
            Assert.AreEqual(0f, off.y, 1e-4f);
        }

        [TestCase(0f, true)]
        [TestCase(5.9f, true)]
        [TestCase(6f, false)]
        [TestCase(12f, false)]
        [TestCase(18.99f, false)]
        [TestCase(19f, true)]
        [TestCase(23.5f, true)]
        public void IsNight_WrapsMidnight(float hour, bool expectedNight)
        {
            Assert.AreEqual(expectedNight, CottageDayNight.IsNight(hour, dawnHour: 6f, duskHour: 19f));
        }

        // ===== WaterSurface: the SIM → shader-uniform mappings (the immersion key) =======================
        // These prove the shader's surface reads the deterministic environment the same way the physics does:
        // current → flow, wind → roughness, sea-state → chop, level passes through unchanged.

        [Test]
        public void Flow_RisesWithCurrent_AboveTheBaseFloor()
        {
            const float baseFlow = 0.06f, scale = 0.12f, full = 1.2f;
            float slack = WaterSurface.FlowSpeed(Vector2.zero, baseFlow, scale, full);
            float mid   = WaterSurface.FlowSpeed(new Vector2(0.6f, 0f), baseFlow, scale, full); // half of full
            float fast  = WaterSurface.FlowSpeed(new Vector2(1.2f, 0f), baseFlow, scale, full); // at full
            float over  = WaterSurface.FlowSpeed(new Vector2(5f, 0f), baseFlow, scale, full);   // saturates

            Assert.AreEqual(baseFlow, slack, 1e-5f, "slack water drifts at the material's base flow (never frozen)");
            Assert.Greater(mid, slack, "more current => faster scroll");
            Assert.Greater(fast, mid, "monotonic in current speed");
            Assert.AreEqual(baseFlow + scale, fast, 1e-5f, "at full current the live add reaches the scale");
            Assert.AreEqual(fast, over, 1e-5f, "current beyond full saturates (clamped 0..1)");
        }

        [Test]
        public void FlowDirection_FollowsTheCurrentSet_AndIsNormalized()
        {
            var dir = WaterSurface.FlowDirection(new Vector2(3f, 4f));   // 3-4-5 → unit (0.6, 0.8)
            Assert.AreEqual(0.6f, dir.x, 1e-4f);
            Assert.AreEqual(0.8f, dir.y, 1e-4f);
            Assert.AreEqual(1f, dir.magnitude, 1e-4f, "scroll dir is a unit vector");

            var slack = WaterSurface.FlowDirection(Vector2.zero);       // no NaN on slack water
            Assert.AreEqual(Vector2.right, slack, "near-zero current falls back to +x, never NaN");
        }

        [Test]
        public void Roughness_RisesWithWind_AndSaturates()
        {
            const float full = 12f;
            Assert.AreEqual(0f, WaterSurface.Roughness(Vector2.zero, full), 1e-5f, "calm => glassy (0 roughness)");
            Assert.AreEqual(0.5f, WaterSurface.Roughness(new Vector2(6f, 0f), full), 1e-4f, "half wind => half roughness");
            Assert.AreEqual(1f, WaterSurface.Roughness(new Vector2(12f, 0f), full), 1e-5f, "full-wind => full whitecaps");
            Assert.AreEqual(1f, WaterSurface.Roughness(new Vector2(40f, 0f), full), 1e-5f, "a gale saturates at 1");
        }

        [Test]
        public void Choppiness_SpansGlassToStorm_Monotonic()
        {
            Assert.AreEqual(0f, WaterSurface.Choppiness(SeaState.Glass), 1e-5f, "glass is flat");
            Assert.AreEqual(1f, WaterSurface.Choppiness(SeaState.Storm), 1e-5f, "a storm is fully choppy");
            float calm = WaterSurface.Choppiness(SeaState.Calm);
            float rough = WaterSurface.Choppiness(SeaState.Rough);
            Assert.Greater(rough, calm, "rougher seas => more chop");
            Assert.That(WaterSurface.Choppiness(SeaState.Moderate), Is.InRange(0f, 1f), "stays in the 0..1 uniform range");
        }
    }
}
