using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>The hull reflection fades with depth, in streaks.</b>
    ///
    /// <para>The mirrored hull read as a uniform dark slab broken into a soft block. Two rules fix
    /// it: the reflection pass turns fragments into blockers as the mirrored point goes deeper
    /// (<see cref="HullMeshReflection.FadeKeep"/>, twin of <c>HHMeshReflectFadeKeep</c>), and it does so in
    /// horizontal streaks along each pixel row (<see cref="HullMeshReflection.FadeThreshold"/>, twin of
    /// <c>HHMeshReflectFadeThreshold</c>), the way a reflection breaks on moving water. These tests pin both.</para>
    /// </summary>
    public class HullReflectionFadeTests
    {
        [Test]
        public void PackFade_ZeroLength_IsOff()
        {
            Assert.AreEqual(Vector4.zero, HullMeshReflection.PackFade(0.1f, 0f));
            Assert.AreEqual(Vector4.zero, HullMeshReflection.PackFade(0.1f, -1f));
        }

        [Test]
        public void PackFade_StoresStartAndInverseLength()
        {
            Vector4 f = HullMeshReflection.PackFade(0.1f, 0.5f);
            Assert.AreEqual(0.1f, f.x, 1e-6f);
            Assert.AreEqual(2f, f.y, 1e-6f);
            Assert.AreEqual(0f, HullMeshReflection.PackFade(-3f, 0.5f).x, 0f, "a start is never below the waterline");
        }

        [Test]
        public void FadeKeep_IsWholeToTheStart_ThenFallsToNothing()
        {
            Vector4 f = HullMeshReflection.PackFade(0.1f, 0.6f);
            Assert.AreEqual(1f, HullMeshReflection.FadeKeep(0f, f), 1e-6f);
            Assert.AreEqual(1f, HullMeshReflection.FadeKeep(0.1f, f), 1e-6f);
            Assert.AreEqual(0.5f, HullMeshReflection.FadeKeep(0.4f, f), 1e-5f);
            Assert.AreEqual(0f, HullMeshReflection.FadeKeep(0.7f, f), 1e-6f);
            Assert.AreEqual(0f, HullMeshReflection.FadeKeep(3f, f), 1e-6f);

            float prev = 1f;
            for (float h = 0f; h <= 1f; h += 0.01f)
            {
                float k = HullMeshReflection.FadeKeep(h, f);
                Assert.LessOrEqual(k, prev + 1e-6f, "the kept share never grows with depth");
                prev = k;
            }
        }

        [Test]
        public void FadeBlocks_Off_NeverBlocks()
        {
            for (float h = 0f; h < 5f; h += 0.25f)
                Assert.IsFalse(HullMeshReflection.FadeBlocks(h, Vector4.zero, 0.99f));
        }

        [Test]
        public void FadeBlocks_KeepsTheLineWhole_AndDithersOutTheDeep()
        {
            Vector4 f = HullMeshReflection.PackFade(0.1f, 0.6f);
            // Over a 4x4 ordered-dither tile, the share of cells kept equals FadeKeep.
            foreach (float h in new[] { 0f, 0.1f, 0.25f, 0.4f, 0.55f, 0.7f, 1.2f })
            {
                int kept = 0;
                for (int i = 0; i < 16; i++)
                    if (!HullMeshReflection.FadeBlocks(h, f, (i + 0.5f) / 16f)) kept++;
                Assert.AreEqual(HullMeshReflection.FadeKeep(h, f), kept / 16f, 1f / 16f + 1e-6f,
                    $"height {h}: kept {kept}/16");
            }
        }

        [Test]
        public void PackFade_StreaksUnderOnePixel_AreTheDither()
        {
            Assert.AreEqual(0f, HullMeshReflection.PackFade(0.1f, 0.6f, 0.5f).z, 0f);
            Assert.AreEqual(6f, HullMeshReflection.PackFade(0.1f, 0.6f, 6f).z, 0f);
            Assert.AreEqual(Vector4.zero, HullMeshReflection.PackFade(0.1f, 0f, 6f), "no fade, no streaks");
        }

        [Test]
        public void FadeThreshold_StreaksOff_IsTheDitherValue()
        {
            Vector4 f = HullMeshReflection.PackFade(0.1f, 0.6f, 0f);
            Assert.AreEqual(0.40625f, HullMeshReflection.FadeThreshold(13, 7, f, 0.40625f), 0f);
        }

        [Test]
        public void FadeThreshold_Streaks_AreRunsAlongARow()
        {
            const int Len = 6;
            Vector4 f = HullMeshReflection.PackFade(0.1f, 0.6f, Len);
            for (int row = -20; row < 20; row++)
            {
                int changes = 0;
                float prev = HullMeshReflection.FadeThreshold(-60, row, f, 0f);
                for (int x = -59; x < 60; x++)
                {
                    float v = HullMeshReflection.FadeThreshold(x, row, f, 0f);
                    Assert.That(v, Is.InRange(0f, 1f));
                    if (v != prev) changes++;
                    prev = v;
                }
                // 120 cells in runs of 6: at most one change per run boundary.
                Assert.LessOrEqual(changes, 120 / Len + 1, $"row {row}: a streak is {Len} pixels, not a dither");
                Assert.GreaterOrEqual(changes, 120 / Len - 2, $"row {row}: the runs differ from one another");
            }
        }

        [Test]
        public void FadeThreshold_Streaks_SpreadEvenly_SoTheKeptShareFollowsTheFade()
        {
            Vector4 f = HullMeshReflection.PackFade(0.1f, 0.6f, 6f);
            foreach (float h in new[] { 0.1f, 0.25f, 0.4f, 0.55f, 0.7f })
            {
                int kept = 0, n = 0;
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 96; x++, n++)
                        if (!HullMeshReflection.FadeBlocks(h, f, HullMeshReflection.FadeThreshold(x, y, f, 0f))) kept++;
                Assert.AreEqual(HullMeshReflection.FadeKeep(h, f), kept / (float)n, 0.08f, $"height {h}");
            }
        }

        [Test]
        public void TheShippedConfig_FadesAndStreaks()
        {
            Assert.Greater(HiddenHarbours.Core.GameConfig.DefaultHullReflectionFadeMeters, 0f);
            Assert.GreaterOrEqual(HiddenHarbours.Core.GameConfig.DefaultHullReflectionStreakPixels, 1f);
        }
    }
}