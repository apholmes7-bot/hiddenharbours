using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Apparent wind, the sum the sail drive resolves against: true wind less the boat's own motion.
    /// Pure: no services, no clock, no Unity runtime beyond <see cref="Vector2"/>.
    /// </summary>
    public class TelltaleMathTests
    {
        [Test]
        public void ApparentWind_IsTrueWindLessTheBoatsOwnMotion()
        {
            Vector2 apparent = TelltaleMath.ApparentWind(new Vector2(0f, 5f), new Vector2(0f, 2f));
            Assert.AreEqual(0f, apparent.x, 1e-5f);
            Assert.AreEqual(3f, apparent.y, 1e-5f, "running with the wind takes the sting out of it");
        }

        [Test]
        public void MotoringIntoAFlatCalm_StillMakesAWindAcrossTheDeck()
        {
            Vector2 apparent = TelltaleMath.ApparentWind(Vector2.zero, new Vector2(0f, 4f));
            Assert.AreEqual(4f, apparent.magnitude, 1e-4f, "her own speed is wind enough");
            Assert.AreEqual(180f, BoatKinematics.BearingDegrees(apparent), 1e-3f, "blowing aft");
        }

        [Test]
        public void BoatSpeed_ChangesTheReadTheWayASailorExpects()
        {
            var wind = new Vector2(0f, 6f);                        // blowing north at 6

            float beatingIntoIt = TelltaleMath.ApparentWind(wind, new Vector2(0f, -3f)).magnitude;
            float lyingStill = TelltaleMath.ApparentWind(wind, Vector2.zero).magnitude;
            float runningOff = TelltaleMath.ApparentWind(wind, new Vector2(0f, 3f)).magnitude;

            Assert.AreEqual(9f, beatingIntoIt, 1e-4f, "punching into it adds your own speed");
            Assert.AreEqual(6f, lyingStill, 1e-4f);
            Assert.AreEqual(3f, runningOff, 1e-4f, "bearing away takes it out of the rig");
        }

        [Test]
        public void ApparentWind_IsDeterministic()
        {
            var a = TelltaleMath.ApparentWind(new Vector2(3.1f, -2.7f), new Vector2(-1.3f, 0.4f));
            var b = TelltaleMath.ApparentWind(new Vector2(3.1f, -2.7f), new Vector2(-1.3f, 0.4f));
            Assert.AreEqual(a, b);
        }
    }
}
