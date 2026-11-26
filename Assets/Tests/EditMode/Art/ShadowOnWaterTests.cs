using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>One rule for any shadow on open water</b> (owner, Nine Mile Creek at low water, 2026-11-25: the masts
    /// and piles threw long diagonal bars across the basin). Headless arithmetic of <see cref="ShadowOnWater"/>:
    /// on water a shadow is capped short, faded and broken; on land nothing changes; the answer is a pure
    /// function of its inputs (rule 5). The look itself is the PR's plates.
    /// </summary>
    public class ShadowOnWaterTests
    {
        [Test]
        public void OnLand_TheShadowIsUntouched()
        {
            Assert.AreEqual(4.2f, ShadowOnWater.LengthMultiplier(4.2f, false, 0.6f), 1e-6f);
            Assert.AreEqual(1f, ShadowOnWater.AlphaScale(0.05f, false, 0.25f, 0.6f, 0.7f), 1e-6f);
            Assert.AreEqual(0f, ShadowOnWater.Breakup(false, 0.6f), 1e-6f);
        }

        [Test]
        public void OnWater_TheRakeIsCappedShort_ButAShortShadowIsNotLengthened()
        {
            Assert.AreEqual(0.6f, ShadowOnWater.LengthMultiplier(4.2f, true, 0.6f), 1e-6f, "a low-sun rake is capped");
            Assert.AreEqual(0.35f, ShadowOnWater.LengthMultiplier(0.35f, true, 0.6f), 1e-6f, "a noon shadow is kept");
            Assert.AreEqual(0f, ShadowOnWater.LengthMultiplier(4.2f, true, -1f), 1e-6f, "a negative cap is no shadow, not a flip");
        }

        [Test]
        public void OnWater_TheShadeFades_AndFadesMoreWithALowSun()
        {
            float high = ShadowOnWater.AlphaScale(1f, true, 0.25f, 0.6f, 0.7f);
            float low = ShadowOnWater.AlphaScale(0.05f, true, 0.25f, 0.6f, 0.7f);
            Assert.AreEqual(0.7f, high, 1e-5f, "a full sun keeps the flat water alpha");
            Assert.Less(low, high, "a low sun fades it further");
            Assert.Greater(low, 0f, "never to nothing: the shade still reads");
        }

        [Test]
        public void OnWater_TheBreakupIsClamped()
        {
            Assert.AreEqual(0.6f, ShadowOnWater.Breakup(true, 0.6f), 1e-6f);
            Assert.AreEqual(1f, ShadowOnWater.Breakup(true, 3f), 1e-6f);
            Assert.AreEqual(0f, ShadowOnWater.Breakup(true, -1f), 1e-6f);
        }

        [Test]
        public void TheGrid_IsNeverZero_AndFollowsTheProfile()
        {
            var p = ScriptableObject.CreateInstance<SpriteShadowProfile>();
            try
            {
                p.WaterBreakupGrid = new Vector3(32f, 4f, 9f);
                Assert.AreEqual(new Vector4(32f, 4f, 9f, 0f), ShadowOnWater.Grid(p));
                p.WaterBreakupGrid = Vector3.zero;
                Vector4 g = ShadowOnWater.Grid(p);
                Assert.That(g.x >= 1f && g.y >= 1f && g.z >= 1f, "a zero grid would divide by zero in the shader");
                Vector4 none = ShadowOnWater.Grid(null);
                Assert.That(none.x >= 1f && none.y >= 1f && none.z >= 1f);
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void TheRule_IsAPureFunction_SameInputsSameAnswer()
        {
            for (int i = 0; i < 64; i++)
            {
                float e = i / 63f;
                Assert.AreEqual(ShadowOnWater.AlphaScale(e, true, 0.25f, 0.6f, 0.7f),
                                ShadowOnWater.AlphaScale(e, true, 0.25f, 0.6f, 0.7f));
                Assert.AreEqual(ShadowOnWater.LengthMultiplier(e * 6f, true, 0.6f),
                                ShadowOnWater.LengthMultiplier(e * 6f, true, 0.6f));
            }
        }

        [Test]
        public void WithNoTerrainService_EverywhereReadsAsLand()
        {
            ITidalTerrain terrain = GameServices.TidalTerrain;
            try
            {
                GameServices.TidalTerrain = null;
                Assert.IsFalse(ShadowOnWater.IsOverWater(new Vector2(110f, 80f)));
                Assert.IsFalse(ShadowOnWater.IsOverWater(new Vector2(110f, 80f), new Bounds(new Vector3(110f, 75f), new Vector3(3f, 10f))));
            }
            finally { GameServices.TidalTerrain = terrain; }
        }

        [Test]
        public void ACasterSpanningTheEdge_IsJudgedByTwoOfThree()
        {
            // a gangway: foot on the deck, its bottom and middle over the basin -> water
            Assert.IsTrue(ShadowOnWater.Majority(false, true, true));
            // a crane on the wharf: foot and middle on land, only its jib's bottom over water -> land
            Assert.IsFalse(ShadowOnWater.Majority(false, true, false));
            Assert.IsTrue(ShadowOnWater.Majority(true, true, true));
            Assert.IsFalse(ShadowOnWater.Majority(false, false, false));
            Assert.IsTrue(ShadowOnWater.Majority(true, false, true));
        }

        [Test]
        public void ALandCaster_HasItsShadeOnWaterCutAtTheWaterCap()
        {
            // no rule: off
            Assert.AreEqual(Vector4.zero, ShadowOnWater.LandCasterEdge(false, 3f, 0.6f, 0.5f, 0.6f));
            // a long morning rake of 3 heights, capped at 0.6 heights on water: cut a fifth of the way along
            Vector4 e = ShadowOnWater.LandCasterEdge(true, 3f, 0.6f, 0.5f, 0.6f);
            Assert.AreEqual(0.2f, e.x, 1e-5f);
            Assert.AreEqual(0.5f, e.y, 1e-6f);
            Assert.AreEqual(0.6f, e.z, 1e-6f);
            Assert.AreEqual(1f, e.w);
            // a noon shade already shorter than the cap is never cut
            Assert.Greater(ShadowOnWater.LandCasterEdge(true, 0.4f, 0.6f, 1f, 0.6f).x, 1e6f);
            // clamps
            Vector4 c = ShadowOnWater.LandCasterEdge(true, 3f, 0.6f, 2f, -1f);
            Assert.AreEqual(1f, c.y); Assert.AreEqual(0f, c.z);
        }

        [Test]
        public void APieceLyingOnTheWater_CastsNoSunShadow()
        {
            // A float lies on the surface: it has no height to rake a shade from, and its tall plan sprite
            // read as a tower made the boxed shade beside the fleet gangway (NMC, 2026-11-25).
            var rules = Resources.Load<SunCasterRules>(nameof(SunCasterRules));
            Assert.IsNotNull(rules);
            foreach (string piece in new[] { "fingerA", "fingerB_front", "float", "float_back", "recFloat", "recFloat_front" })
                Assert.IsTrue(System.Array.Exists(rules.ExcludedNames, p => SunCasterRules.Matches(piece, p)), piece);
            // ...while a net float on the drying rack and the gangways still cast.
            foreach (string piece in new[] { "NetFloat_dry_36", "gangway0_s0" })
                Assert.IsFalse(System.Array.Exists(rules.ExcludedNames, p => SunCasterRules.Matches(piece, p)), piece);
        }

        [Test]
        public void TheBuiltInDefault_AsksForNoRule()
        {
            var p = SpriteShadowProfile.CreateDefault();
            try { Assert.IsFalse(ShadowOnWater.AnyRule(p)); }
            finally { Object.DestroyImmediate(p); }
        }
    }
}
