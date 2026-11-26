using NUnit.Framework;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Sea mist is drawn over the sea, never over dry ground. Before this, a wisp was placed anywhere in
    /// the box round the camera, so on a misty day soft cyan discs lay on the grass by the cannery at
    /// St Peters (and on any field, path or roof near the shore). These pin the rule that keeps it off land.
    /// </summary>
    public class SeaMistOverWaterTests
    {
        const float Eps = 1e-5f;
        static readonly float Band = SeaMistConfig.Default.ShoreFadeMetres;

        [Test]
        public void TheDefaultShoreBandIsPositive()
        {
            Assert.Greater(Band, 0f, "a zero band would make the shoreline edge of the mist a hard cut.");
        }

        [Test]
        public void DryGround_DrawsNoMist()
        {
            // The cannery's grass: the ground about 6 m above a spring high of +2.2 m.
            Assert.AreEqual(0f, AmbientParticleMath.MistOverWater01(2.2f - 6f, Band), Eps);
            Assert.AreEqual(0f, AmbientParticleMath.MistOverWater01(0f, Band), Eps, "the waterline itself is dry");
        }

        [Test]
        public void OpenWater_DrawsTheWholeWisp()
        {
            Assert.AreEqual(1f, AmbientParticleMath.MistOverWater01(Band, Band), Eps);
            Assert.AreEqual(1f, AmbientParticleMath.MistOverWater01(12f, Band), Eps);
        }

        [Test]
        public void TheShallowsFadeTheWispIn_Monotonically()
        {
            float prev = 0f;
            for (int i = 1; i <= 10; i++)
            {
                float w = AmbientParticleMath.MistOverWater01(Band * i / 10f, Band);
                Assert.GreaterOrEqual(w, prev);
                prev = w;
            }
            Assert.AreEqual(0.5f, AmbientParticleMath.MistOverWater01(Band * 0.5f, Band), Eps);
        }

        [Test]
        public void NoGroundKnown_KeepsTheOldLook()
        {
            // A scene with no tidal terrain cannot say where the land is; the mist draws as it always did.
            Assert.AreEqual(1f, AmbientParticleMath.MistOverWater01(float.PositiveInfinity, Band), Eps);
            Assert.AreEqual(0f, AmbientParticleMath.MistOverWater01(float.NaN, Band), Eps, "a NaN ground reads as no water");
        }

        [Test]
        public void ANonPositiveBand_IsAHardCutAtTheWaterline()
        {
            Assert.AreEqual(1f, AmbientParticleMath.MistOverWater01(0.01f, 0f), Eps);
            Assert.AreEqual(0f, AmbientParticleMath.MistOverWater01(-0.01f, 0f), Eps);
        }
    }
}
