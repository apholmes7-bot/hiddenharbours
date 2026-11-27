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

        /// <summary>Owner 11 26, no mixels: a wall that faces across the screen takes less of the roughness, or none.
        /// A south wall's push runs down its own drawn column and it keeps all of it; a steep east wall's push runs
        /// across a face that is a sliver on screen, and it takes none; with no bounds set nothing changes.</summary>
        [Test]
        public void ASteepWallAcrossTheScreen_TakesNoRoughness_AndASouthWallAllOfIt()
        {
            var bounds = new Vector2(0.35f, 0.8f);
            var faced = new CliffJag(0.35f, 0.45f, new Vector3(3f, 0.9f, 0.25f), new Vector3(0.6f, 0.3f, 0.15f), 1f, 6, bounds);
            float texel = 1f / CliffWaterlineMath.TexelsPerMetre;

            Vector2 south = Vector2.down, east = Vector2.right;
            Vector2 southColumn = Vector2.down;                               // brow to drawn toe, straight down the screen
            Vector2 steepEastColumn = new Vector2(2f, -6.1f).normalized;      // an 8 m bank at 76 degrees, facing east
            Vector2 easyEastColumn = new Vector2(7.2f, -6.1f).normalized;     // the same bank at 48 degrees

            Assert.AreEqual(1f, CliffJagMath.FacingGain(in faced, south, southColumn), 1e-6f);
            Assert.AreEqual(0f, CliffJagMath.FacingGain(in faced, east, steepEastColumn), 1e-6f);
            float between = CliffJagMath.FacingGain(in faced, east, easyEastColumn);
            Assert.That(between, Is.GreaterThan(0.5f).And.LessThan(1f), "a gentle east bank keeps most of it");
            Assert.AreEqual(1f, CliffJagMath.FacingGain(in Rough, east, steepEastColumn), "no bounds: every wall takes all of it");
            Assert.AreEqual(1f, CliffJagMath.FacingGain(default, east, steepEastColumn));

            for (float x = 0f; x < 40f; x += 0.23f)
            {
                const float profile = 0.31f, warp = 1.4f;
                // None: the kit's own profile at its own depth, and no jag.
                Assert.AreEqual(profile, CliffJagMath.Outward(in faced, profile, warp, 0f, x, 0f), 1e-6f, $"x {x}");
                // All: the profile at the warp's gain, and the jag's share still a whole number of texels.
                float all = CliffJagMath.Outward(in faced, profile, warp, 1f, x, 0f) - profile * warp;
                Assert.AreEqual(0f, Mathf.Abs(all / texel - Mathf.Round(all / texel)), 1e-3f, $"x {x}");
                // Part: never more than all of it, to the texel.
                float part = CliffJagMath.Outward(in faced, profile, warp, 0.5f, x, 0f) - profile * Mathf.Lerp(1f, warp, 0.5f);
                Assert.LessOrEqual(Mathf.Abs(part), Mathf.Abs(all) + texel, $"x {x}");
            }
        }

        /// <summary>St Peters sets the bounds, and in order: without them its steep east wall breaks into shards.</summary>
        [Test]
        public void StPeters_HoldsTheRoughnessBackOnWallsAcrossTheScreen()
        {
            var mix = UnityEditor.AssetDatabase.LoadAssetAtPath<CliffFaceMixDef>(StPetersMix);
            Assert.IsNotNull(mix, StPetersMix);
            Assert.Greater(mix.RoughnessByFacing.y, mix.RoughnessByFacing.x, "the bounds are not in order");
            Assert.Greater(mix.RoughnessByFacing.x, 0f, "St Peters' steep east wall takes the whole jag again");
            Assert.LessOrEqual(mix.RoughnessByFacing.y, 1f);
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
