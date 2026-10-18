using HiddenHarbours.Core;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    public class SloopYawMathTests
    {
        [TestCase(true, 20f, 0f, 0f, 800f)]
        [TestCase(true, -20f, 0f, 0f, -800f)]
        [TestCase(true, 0f, 0f, 0f, -800f)]
        [TestCase(true, 20f, 0.25f, 0f, -800f)]
        [TestCase(true, -20f, -0.25f, 0f, 800f)]
        [TestCase(true, 20f, 0f, 1f, 400f)]
        [TestCase(true, 20f, 0f, -1f, 400f)]
        [TestCase(true, 20f, 0f, 2f, 0f)]
        [TestCase(true, 20f, 0f, -3f, 0f)]
        [TestCase(false, 20f, 0f, 0f, 0f)]
        public void PayOff_Table(bool irons, float angle, float helm, float way, float expected)
            => Assert.That(SailDrive.IronsPayOffTorque(irons, angle, helm, way, 800f, 2f, 10f, 10f),
                Is.EqualTo(expected).Within(0.001f));

        [Test]
        public void PayOff_ZeroTuningAndCalmAreInert()
        {
            Assert.That(SailDrive.IronsPayOffTorque(true, 20f, 1f, 0f, 0f, 2f, 10f, 10f), Is.Zero);
            Assert.That(SailDrive.IronsPayOffTorque(true, 20f, 1f, 0f, 800f, 2f, 0f, 10f), Is.Zero);
        }

    }
}
