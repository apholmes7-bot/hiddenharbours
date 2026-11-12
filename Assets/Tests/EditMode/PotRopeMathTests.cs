using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>The rope from pot to buoy: a fixed length in water whose depth and stream change with the tide.</summary>
    public class PotRopeMathTests
    {
        private const float Rope = 10f;
        private const float FloodWest = 270f;
        private const float Lean = 0.35f;

        [Test]
        public void Scope_IsTheSpareRope_AndNoneWhenTheWaterIsAsDeepAsTheRope()
        {
            Assert.That(PotRopeMath.Scope(Rope, 6f), Is.EqualTo(8f).Within(1e-4f));
            Assert.That(PotRopeMath.Scope(Rope, 0f), Is.EqualTo(Rope).Within(1e-4f));
            Assert.That(PotRopeMath.Scope(Rope, Rope), Is.EqualTo(0f));
            Assert.That(PotRopeMath.Scope(Rope, 12f), Is.EqualTo(0f));
            Assert.That(PotRopeMath.IsDrowned(Rope, 12f), Is.True);
            Assert.That(PotRopeMath.IsDrowned(Rope, 6f), Is.False);
        }

        [Test]
        public void TheBuoy_StreamsOneWayOnTheFlood_TheOtherOnTheEbb_AndSitsOverThePotAtSlack()
        {
            Vector2 flood = PotRopeMath.FloatOffset(Rope, 6f, 1f, FloodWest, Lean);
            Vector2 ebb = PotRopeMath.FloatOffset(Rope, 6f, -1f, FloodWest, Lean);
            Vector2 slack = PotRopeMath.FloatOffset(Rope, 6f, 0f, FloodWest, Lean);

            Assert.That(flood.x, Is.EqualTo(-8f * Lean).Within(1e-3f), "the flood carries it west");
            Assert.That(flood.y, Is.EqualTo(0f).Within(1e-3f));
            Assert.That((ebb + flood).magnitude, Is.LessThan(1e-3f), "the ebb carries it the same distance east");
            Assert.That(slack, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void AsTheTideRises_TheBuoyClosesUpOverThePot()
        {
            float last = float.MaxValue;
            for (float depth = 2f; depth <= Rope; depth += 0.5f)
            {
                float reach = PotRopeMath.FloatOffset(Rope, depth, 1f, FloodWest, Lean).magnitude;
                Assert.That(reach, Is.LessThan(last), $"depth {depth}");
                Assert.That(reach, Is.LessThanOrEqualTo(PotRopeMath.Scope(Rope, depth) + 1e-4f), "never further than the rope allows");
                last = reach;
            }
            Assert.That(last, Is.EqualTo(0f).Within(1e-4f), "at the rope's length it is straight up and down");
        }

        [Test]
        public void TheStream_ComesFromTheTidesRateOfRise_InGameHours()
        {
            const double day = 1800.0;                 // one game hour = 75 clock seconds
            float rise = PotRopeMath.RiseMetersPerHour(0f, 0.5f, 37.5, day);
            Assert.That(rise, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(PotRopeMath.RiseMetersPerHour(0.5f, 0f, 37.5, day), Is.EqualTo(-1f).Within(1e-4f));
            Assert.That(PotRopeMath.RiseMetersPerHour(0f, 1f, 0.0, day), Is.EqualTo(0f));

            Assert.That(PotRopeMath.Stream(0.5f, 1f), Is.EqualTo(0.5f));
            Assert.That(PotRopeMath.Stream(3f, 1f), Is.EqualTo(1f), "clamped at full flood");
            Assert.That(PotRopeMath.Stream(-3f, 1f), Is.EqualTo(-1f), "clamped at full ebb");
            Assert.That(PotRopeMath.Stream(1f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void SomethingUnderWater_DrawsDownScreen_AndFadesWithDepth()
        {
            Vector2 plan = new Vector2(5f, 9f);
            Assert.That(PotRopeMath.DrawnAtDepth(plan, 4f, 0.25f), Is.EqualTo(new Vector2(5f, 8f)));
            Assert.That(PotRopeMath.DrawnAtDepth(plan, -1f, 0.25f), Is.EqualTo(plan), "nothing draws above the surface");

            Assert.That(PotRopeMath.SeenThroughWater01(0f, 2.5f), Is.EqualTo(1f));
            Assert.That(PotRopeMath.SeenThroughWater01(1.25f, 2.5f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(PotRopeMath.SeenThroughWater01(6f, 2.5f), Is.EqualTo(0f));
        }
    }
}
