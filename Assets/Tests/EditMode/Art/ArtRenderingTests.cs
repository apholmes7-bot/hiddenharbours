using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

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
    }
}
