using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// VS-19 — the masthead pennant's maths. The burgee is the boat's wind instrument, so it has to be
    /// right in the ways a sailor would notice: it streams DOWNWIND, it reads APPARENT wind (so motoring
    /// into a calm still streams it astern), it goes stiff with strength, and it hangs rather than spins
    /// when there is nothing to read.
    ///
    /// <para>Pure — no services, no clock, no Unity runtime beyond <see cref="Vector2"/>.</para>
    /// </summary>
    public class TelltaleMathTests
    {
        private static readonly TelltaleSettings S = TelltaleSettings.Default;

        // Bearings are clockwise from North: N = 0, E = 90, S = 180, W = 270.
        private static Vector2 Blowing(Vector2 towards, float speed) => towards.normalized * speed;

        // ---- apparent wind -----------------------------------------------------------------------

        [Test]
        public void ApparentWind_IsTrueWindLessTheBoatsOwnMotion()
        {
            Vector2 apparent = TelltaleMath.ApparentWind(new Vector2(0f, 5f), new Vector2(0f, 2f));
            Assert.AreEqual(0f, apparent.x, 1e-5f);
            Assert.AreEqual(3f, apparent.y, 1e-5f, "running with the wind takes the sting out of it");
        }

        [Test]
        public void MotoringIntoAFlatCalm_StillStreamsThePennantAstern()
        {
            // The detail that makes the instrument feel real: no true wind at all, but 4 m/s of boat
            // speed northward means 4 m/s of apparent wind blowing south — the burgee streams aft.
            TelltaleState state = TelltaleMath.Resolve(Vector2.zero, new Vector2(0f, 4f), S);

            Assert.IsFalse(state.IsLimp, "her own speed is wind enough");
            Assert.AreEqual(4f, state.ApparentSpeed, 1e-4f);
            Assert.AreEqual(180f, state.StreamBearingDegrees, 1e-3f, "streaming south, i.e. astern");
        }

        [Test]
        public void RunningWithTheWindAtWindSpeed_GoesLimp()
        {
            // Dead downwind at exactly wind speed: the boat feels nothing at all.
            TelltaleState state = TelltaleMath.Resolve(new Vector2(0f, 5f), new Vector2(0f, 5f), S);

            Assert.IsTrue(state.IsLimp);
            Assert.AreEqual(0f, state.ApparentSpeed, 1e-4f);
        }

        // ---- direction ---------------------------------------------------------------------------

        [Test]
        public void PennantStreamsDownwind_NotIntoTheWind()
        {
            // A wind vector blowing toward the East must stream the pennant to the East (090°).
            TelltaleState state = TelltaleMath.Resolve(Blowing(Vector2.right, 8f), Vector2.zero, S);
            Assert.AreEqual(90f, state.StreamBearingDegrees, 1e-3f);
        }

        [Test]
        public void StreamBearing_CoversEveryQuarter()
        {
            Assert.AreEqual(0f, TelltaleMath.Resolve(Blowing(Vector2.up, 8f), Vector2.zero, S)
                                            .StreamBearingDegrees, 1e-3f);
            Assert.AreEqual(90f, TelltaleMath.Resolve(Blowing(Vector2.right, 8f), Vector2.zero, S)
                                             .StreamBearingDegrees, 1e-3f);
            Assert.AreEqual(180f, TelltaleMath.Resolve(Blowing(Vector2.down, 8f), Vector2.zero, S)
                                              .StreamBearingDegrees, 1e-3f);
            Assert.AreEqual(270f, TelltaleMath.Resolve(Blowing(Vector2.left, 8f), Vector2.zero, S)
                                              .StreamBearingDegrees, 1e-3f);
        }

        [Test]
        public void StreamBearing_AgreesWithTheCoreBearingConvention()
        {
            // The pennant and the compass must share one idea of North (ADR 0007), or the boat will
            // point one way and its burgee another.
            var apparent = new Vector2(3f, 4f);
            Assert.AreEqual(BoatKinematics.BearingDegrees(apparent),
                            TelltaleMath.Resolve(apparent, Vector2.zero, S).StreamBearingDegrees, 1e-4f);
        }

        // ---- stiffness ---------------------------------------------------------------------------

        [Test]
        public void ACalm_HangsThePennantRatherThanSpinningIt()
        {
            TelltaleState state = TelltaleMath.Resolve(Blowing(Vector2.right, 0.2f), Vector2.zero, S);

            Assert.IsTrue(state.IsLimp, "under the dead zone there is no honest direction to point");
            Assert.AreEqual(0f, state.Stiffness01, 1e-6f);
        }

        [Test]
        public void ExactlyZeroWind_IsLimpAndDoesNotThrow()
        {
            TelltaleState state = TelltaleMath.Resolve(Vector2.zero, Vector2.zero, S);
            Assert.IsTrue(state.IsLimp);
            Assert.AreEqual(0f, state.ApparentSpeed, 1e-6f);
        }

        [Test]
        public void Stiffness_RisesWithApparentWind_AndSaturates()
        {
            float light = TelltaleMath.Resolve(Blowing(Vector2.right, 2f), Vector2.zero, S).Stiffness01;
            float fresh = TelltaleMath.Resolve(Blowing(Vector2.right, 6f), Vector2.zero, S).Stiffness01;
            float blow = TelltaleMath.Resolve(Blowing(Vector2.right, 10f), Vector2.zero, S).Stiffness01;
            float gale = TelltaleMath.Resolve(Blowing(Vector2.right, 25f), Vector2.zero, S).Stiffness01;

            Assert.Less(light, fresh);
            Assert.Less(fresh, blow);
            Assert.AreEqual(1f, blow, 1e-4f, "board-stiff at the settings' stiff point");
            Assert.AreEqual(1f, gale, 1e-4f, "and it cannot look any stiffer than that");
        }

        [Test]
        public void Stiffness_IsAlwaysInRange()
        {
            for (float speed = 0f; speed <= 40f; speed += 0.37f)
            {
                float s = TelltaleMath.Resolve(Blowing(Vector2.right, speed), Vector2.zero, S).Stiffness01;
                Assert.GreaterOrEqual(s, 0f);
                Assert.LessOrEqual(s, 1f);
            }
        }

        [Test]
        public void BoatSpeed_ChangesTheReadTheWayASailorExpects()
        {
            var wind = new Vector2(0f, 6f);                        // blowing north at 6

            float beatingIntoIt = TelltaleMath.Resolve(wind, new Vector2(0f, -3f), S).ApparentSpeed;
            float lyingStill = TelltaleMath.Resolve(wind, Vector2.zero, S).ApparentSpeed;
            float runningOff = TelltaleMath.Resolve(wind, new Vector2(0f, 3f), S).ApparentSpeed;

            Assert.AreEqual(9f, beatingIntoIt, 1e-4f, "punching into it adds your own speed");
            Assert.AreEqual(6f, lyingStill, 1e-4f);
            Assert.AreEqual(3f, runningOff, 1e-4f, "bearing away takes it out of the rig");
            Assert.Greater(beatingIntoIt, runningOff);
        }

        // ---- the tunables --------------------------------------------------------------------------

        [Test]
        public void DefaultSettings_AreSaneAndOrdered()
        {
            Assert.Greater(S.StiffAtMetresPerSecond, S.LimpBelowMetresPerSecond,
                "the stiff point must sit above the dead zone or the ramp inverts");
            Assert.Greater(S.LimpBelowMetresPerSecond, 0f, "a dead zone of zero would spin in a calm");
            Assert.Greater(S.ResponsePerSecond, 0f);
        }

        [Test]
        public void DegenerateSettings_SaturateRatherThanDivideByZero()
        {
            // A misconfigured pair (stiff point at or below the dead zone) must not produce NaN.
            var broken = new TelltaleSettings
            {
                LimpBelowMetresPerSecond = 5f,
                StiffAtMetresPerSecond = 5f,
                ResponsePerSecond = 6f,
            };
            TelltaleState state = TelltaleMath.Resolve(Blowing(Vector2.right, 9f), Vector2.zero, broken);

            Assert.IsFalse(float.IsNaN(state.Stiffness01));
            Assert.AreEqual(1f, state.Stiffness01, 1e-6f);
        }
    }
}
