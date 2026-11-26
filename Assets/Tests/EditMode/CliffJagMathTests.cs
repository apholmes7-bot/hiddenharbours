using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>The rough-edges jag: off by default, whole texels, deterministic, not the tile's period, and a collision
    /// line with no bite narrower than a walker can leave.</summary>
    public class CliffJagMathTests
    {
        const string StPetersMix = "Assets/_Project/Data/Resources/CliffFaceMixes/CliffFaceMix_StPeters.asset";
        static readonly CliffJag Rough = new CliffJag(0.35f, 0.45f, new Vector3(3f, 0.9f, 0.25f), new Vector3(0.6f, 0.3f, 0.15f), 1f, 6);

        [Test]
        public void Off_IsExactlyZero_SoOtherRegionsDoNotMove()
        {
            CliffJag off = default;
            Assert.IsFalse(off.On);
            for (float x = -20f; x < 40f; x += 0.37f)
                Assert.AreEqual(0f, CliffJagMath.At(in off, x, 0.5f));
            Assert.IsFalse(CliffJag.From(null).On);
        }

        [Test]
        public void TheLine_MovesInWholeTexels_AndRepeatsItself()
        {
            float texel = 1f / CliffWaterlineMath.TexelsPerMetre;
            for (float x = 0f; x < 60f; x += 0.11f)
                foreach (float t in new[] { 0f, 0.3f, 1f })
                {
                    float a = CliffJagMath.At(in Rough, x, t);
                    Assert.AreEqual(0f, Mathf.Abs(a / texel - Mathf.Round(a / texel)), 1e-4f, $"x {x} t {t}");
                    Assert.AreEqual(a, CliffJagMath.At(in Rough, x, t), "the same place draws the same line");
                    Assert.LessOrEqual(Mathf.Abs(a), 0.45f * 1.06f + texel);
                }
        }

        [Test]
        public void TheLine_IsNotTheTilesPeriod_AndBreaksAtMoreThanOneScale()
        {
            int same = 0, n = 0, steps = 0, big = 0;
            float prev = CliffJagMath.At(in Rough, 0f, 1f);
            for (float x = 0.125f; x < 120f; x += 0.25f, n++)
            {
                float a = CliffJagMath.At(in Rough, x, 1f);
                if (Mathf.Approximately(a, CliffJagMath.At(in Rough, x + 12f, 1f))) same++;
                if (!Mathf.Approximately(a, prev)) steps++;
                if (Mathf.Abs(a - prev) >= 0.12f) big++;
                prev = a;
            }
            Assert.Less(same, n / 4, "the foot must not repeat with the kit's 12 m tile");
            Assert.Greater(steps, n / 2, "chips: the line steps at nearly every column");
            Assert.Greater(big, 10, "buttresses and bites: some steps are deep");
        }

        [Test]
        public void TheClosing_FillsEveryBiteNarrowerThanTheWalker_AndKeepsEveryBump()
        {
            const float step = 0.25f;
            int count = 480;
            var along = new float[count];
            var raw = new float[count];
            for (int i = 0; i < count; i++) { along[i] = i * step; raw[i] = CliffJagMath.At(in Rough, along[i], 1f); }
            float[] closed = CliffJagMath.Close(along, raw, Rough.MinBiteMetres);
            for (int i = 0; i < count; i++)
                Assert.GreaterOrEqual(closed[i], raw[i] - 1e-6f, "closing only ever fills");
            // Every run of the closed line lower than both its sides is at least the bite wide.
            for (int i = 1; i < count - 1; i++)
            {
                if (!(closed[i] < closed[i - 1] - 1e-6f)) continue;
                int j = i;
                while (j + 1 < count && Mathf.Abs(closed[j + 1] - closed[i]) < 1e-6f) j++;
                if (j + 1 < count && closed[j + 1] > closed[i] + 1e-6f)
                    Assert.GreaterOrEqual((j - i + 1) * step + 1e-4f, Rough.MinBiteMetres - step,
                        $"a notch at {along[i]} m is narrower than a walker can leave");
            }
            // The highest point of the line is never moved: closing fills bites, it does not grow buttresses.
            float rawMax = float.MinValue, closedMax = float.MinValue;
            for (int i = 0; i < count; i++) { rawMax = Mathf.Max(rawMax, raw[i]); closedMax = Mathf.Max(closedMax, closed[i]); }
            Assert.AreEqual(rawMax, closedMax, 1e-6f);
        }

        [Test]
        public void StPetersMix_TurnsTheRoughEdgesOn()
        {
#if UNITY_EDITOR
            var mix = UnityEditor.AssetDatabase.LoadAssetAtPath<CliffFaceMixDef>(StPetersMix);
            Assert.IsNotNull(mix);
            CliffJag jag = CliffJag.From(mix);
            Assert.IsTrue(jag.On);
            Assert.GreaterOrEqual(jag.MinBiteMetres, 2f * HiddenHarbours.World.QuayEdgePlan.WalkerFootRadiusMetres,
                "a bite the collision leaves open must be wider than the walker's foot");
#endif
        }
    }
}
