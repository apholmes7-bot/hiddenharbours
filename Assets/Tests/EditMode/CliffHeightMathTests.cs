using System.IO;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Owner 11 27: the cliffs lost their height in places, and the flat ledge where they meet the floor is hard to
    /// tell from the face. Height comes back as upright joints and a top-light, foot-dark ramp on the walls laid on
    /// the screen grid (<see cref="CliffHeightMath"/>), and the ledge is calmed (<see cref="TerrainLedgeCalmMath"/>).
    /// CI has no graphics device: these pin the arithmetic the shaders twin, and the region's data.
    /// </summary>
    public sealed class CliffHeightMathTests
    {
        static readonly CliffHeight Height = new CliffHeight(16, 28, 0.75f, 0.3f, true, 0.2f, 0.28f, 0.06f);

        [Test]
        public void NoNumbers_DrawNoJointAndNoRamp()
        {
            CliffHeight off = default;
            for (int x = -40; x < 40; x++)
                for (int y = -40; y < 40; y++)
                    Assert.AreEqual(CliffHeightMath.None, CliffHeightMath.JointAt(in off, x, y));
            Assert.AreEqual(0, CliffHeightMath.RampShift(in off, 0f, 3));
            Assert.AreEqual(0, CliffHeightMath.RampShift(in off, 1f, 3));
            Assert.AreEqual(0f, off.ShaderJointB(true).y, "a mix with no joints switches the shader's off");
            Assert.AreEqual(0f, off.ShaderRamp(true).w, "a mix with no ramp switches the shader's off");
            Assert.AreEqual(Vector4.zero, CliffHeight.From(null).ShaderRamp(true));
        }

        [Test]
        public void OnlyAWallOnTheScreenGrid_TakesThem()
        {
            Assert.AreEqual(1f, Height.ShaderJointB(true).y);
            Assert.AreEqual(0f, Height.ShaderJointB(false).y, "a sheared wall keeps its own look");
            Assert.AreEqual(1f, Height.ShaderRamp(true).w);
            Assert.AreEqual(0f, Height.ShaderRamp(false).w);
        }

        /// <summary>A joint is upright: long unbroken runs down the face, one crack per stretch per row, never more
        /// than a texel off its line, with its lit edge right beside it.</summary>
        [Test]
        public void Joints_RunDownTheFace_InLongUprightRuns()
        {
            const int width = 16 * 40, height = 600;
            int cracks = 0, longest = 0;
            var run = new int[width];
            for (int y = 0; y < height; y++)
            {
                var perZone = new int[width / 16 + 2];
                for (int x = 0; x < width; x++)
                {
                    int j = CliffHeightMath.JointAt(in Height, x, y);
                    if (j == CliffHeightMath.LitEdge)
                        Assert.AreEqual(CliffHeightMath.Crack, CliffHeightMath.JointAt(in Height, x - 1, y),
                            $"a lit edge at ({x},{y}) with no crack left of it");
                    if (j != CliffHeightMath.Crack) { run[x] = 0; continue; }
                    cracks++;
                    perZone[x / 16]++;
                    run[x]++;
                    longest = Mathf.Max(longest, run[x]);
                }
                foreach (int k in perZone) Assert.LessOrEqual(k, 1, $"row {y}: two cracks in one stretch");
            }
            float share = cracks / (float)(height * width / 16);
            Assert.AreEqual(0.75f, share, 0.12f, "about the run share of rows carry a crack in each stretch");
            Assert.GreaterOrEqual(longest, 28, "no joint ran a whole run down the face");
        }

        [Test]
        public void Joints_StayInTheirStretch()
        {
            for (int zone = -30; zone < 30; zone++)
                for (int r = -20; r < 20; r++)
                {
                    int jx = CliffHeightMath.JointColumn(in Height, zone, r);
                    if (jx == int.MinValue) continue;
                    Assert.GreaterOrEqual(jx, zone * 16);
                    Assert.Less(jx, zone * 16 + 16);
                }
        }

        [Test]
        public void TheRamp_LightsTheTop_AndShadesTheFoot_WithARaggedEdge()
        {
            int lifted = 0, dropped = 0, cols = 512;
            var topEdge = new System.Collections.Generic.HashSet<int>();
            for (int x = 0; x < cols; x++)
            {
                Assert.AreEqual(1, CliffHeightMath.RampShift(in Height, 0.05f, x), "the brow is not lit");
                Assert.AreEqual(-1, CliffHeightMath.RampShift(in Height, 0.95f, x), "the foot is not in shade");
                Assert.AreEqual(0, CliffHeightMath.RampShift(in Height, 0.5f, x), "the middle of the face is stepped");
                if (CliffHeightMath.RampShift(in Height, 0.2f, x) == 1) lifted++;
                if (CliffHeightMath.RampShift(in Height, 0.72f, x) == -1) dropped++;
                int edge = 0;
                while (edge < 64 && CliffHeightMath.RampShift(in Height, edge / 64f, x) == 1) edge++;
                topEdge.Add(edge);
            }
            Assert.That(lifted, Is.InRange(cols / 4, cols * 3 / 4), "the top edge does not wander about its share");
            Assert.That(dropped, Is.InRange(cols / 4, cols * 3 / 4), "the foot edge does not wander about its share");
            Assert.GreaterOrEqual(topEdge.Count, 3, "the ramp's edge is a level line");
        }

        [Test]
        public void TheRamp_IsOnePaletteBand_NeverMore()
        {
            for (int i = 0; i <= 100; i++)
                for (int x = 0; x < 64; x++)
                    Assert.That(CliffHeightMath.RampShift(in Height, i / 100f, x), Is.InRange(-1, 1));
        }

        /// <summary>The St Peters mix gives its screen-grid walls height, hangs a bank's own strip, calms the ledge,
        /// and no longer chops its bedding into short slid tiers (slide 160 on 14-texel tiers read as brickwork).</summary>
        [Test]
        public void StPeters_GivesItsWallsHeight_AndCalmsItsLedge()
        {
            CliffFaceMixDef.ClearCache();
            CliffFaceMixDef mix = CliffFaceMixDef.ForScene("StPeters");
            Assert.IsNotNull(mix);
            CliffHeight h = CliffHeight.From(mix);
            Assert.IsTrue(h.JointsOn, "St Peters' screen-grid walls carry no upright joints");
            Assert.IsTrue(h.RampOn, "St Peters' screen-grid walls carry no ramp");
            Assert.Greater(h.RampFootShare, 0f);
            Assert.Greater(mix.FaultBlockTexels.y, mix.FaultBlockTexels.x, "the fault's blocks are wider than tall: they read as courses, not height");
            Assert.LessOrEqual(mix.FaultSlideTexels, mix.FaultBlockTexels.y, "a slide longer than a block chops every upright feature");
            Assert.IsNotNull(mix.ShoreBrowTurfLip, "the banks hang the wall's lip, with no riser");
            Assert.Greater(mix.ShoreBrowTurfLip.RockRiser.y, 0, "the bank's own lip paints no riser");
            Assert.AreEqual(Vector2Int.zero, mix.TurfLip.RockRiser, "a wall's lip paints a riser over its own face");
            Vector4 calm = TerrainLedgeCalmMath.Shader(mix);
            Assert.Greater(calm.x, 0f, "the ledge is not calmed");
            Assert.Less(calm.y, 1f);
            Assert.Less(calm.z, 1f);
        }

        /// <summary>The shaders read what the C# pushes, under the same names (a typo reads as zero: off).</summary>
        [Test]
        public void TheShaders_DeclareWhatIsPushed()
        {
            string face = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Art/Shaders/HiddenHarboursCliffFace.shader"));
            foreach (string name in new[] { "_JointA", "_JointB", "_FaceRamp", "float4 uv4", "float4 band" })
                StringAssert.Contains(name, face);
            foreach (string salt in new[] { "41.0", "42.0", "43.0", "44.0", "45.0", "46.0" })
                StringAssert.Contains(salt, face, "a hash salt the twin uses is missing from the shader");
            string ground = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Art/Shaders/HiddenHarboursTerrainSplat.shader"));
            StringAssert.Contains("float4 _ShoreLedgeCalm", ground);
            StringAssert.Contains("TerrainLedgeCalm(col, w[5] + w[11] + w[12], _ShoreLedgeCalm)", ground);
        }
    }

    /// <summary>The ledge calm (<see cref="TerrainLedgeCalmMath"/>): a function of the texel's colour alone.</summary>
    public sealed class CliffLedgeCalmTests
    {
        static readonly Vector4 Calm = new Vector4(1f, 0.6f, 0.65f, 0.08f);
        static readonly Vector3 Red = new Vector3(0.20f, 0.07f, 0.04f), Crack = new Vector3(0.03f, 0.012f, 0.01f);

        static float Sat(Vector3 c)
        {
            float max = Mathf.Max(c.x, Mathf.Max(c.y, c.z)), min = Mathf.Min(c.x, Mathf.Min(c.y, c.z));
            return max > 0f ? (max - min) / max : 0f;
        }

        static float Luma(Vector3 c) => Vector3.Dot(c, TerrainLedgeCalmMath.Luma);

        [Test]
        public void StrengthZero_OrNoRock_LeavesTheGroundAsItWas()
        {
            Assert.AreEqual(Red, TerrainLedgeCalmMath.Calm(Red, 1f, new Vector4(0f, 0.6f, 0.65f, 0.08f)));
            Assert.AreEqual(Red, TerrainLedgeCalmMath.Calm(Red, 0f, Calm));
            Assert.AreEqual(Vector4.zero, TerrainLedgeCalmMath.Shader(null));
        }

        [Test]
        public void TheCalmedRock_IsLessSaturated_AndItsSpreadIsDrawnIn()
        {
            Vector3 red = TerrainLedgeCalmMath.Calm(Red, 1f, Calm), crack = TerrainLedgeCalmMath.Calm(Crack, 1f, Calm);
            Assert.Less(Sat(red), Sat(Red));
            Assert.Less(Mathf.Abs(Luma(red) - Luma(crack)), Mathf.Abs(Luma(Red) - Luma(Crack)), "the cracks keep their contrast");
            Assert.AreEqual(0.08f + (Luma(Red) - 0.08f) * 0.65f, Luma(red), 1e-4f, "the light is not drawn toward the pivot");
            Assert.Greater(red.x, red.z, "the rock lost its own hue");
        }

        [Test]
        public void HalfARock_TakesHalfTheCalm()
        {
            Vector3 full = TerrainLedgeCalmMath.Calm(Red, 1f, Calm), half = TerrainLedgeCalmMath.Calm(Red, 0.5f, Calm);
            Assert.AreEqual(((Red + full) * 0.5f).x, half.x, 1e-5f);
            Assert.AreEqual(((Red + full) * 0.5f).y, half.y, 1e-5f);
        }

        [Test]
        public void AGreyAtThePivot_IsAFixedPoint()
        {
            var grey = new Vector3(0.08f, 0.08f, 0.08f);
            Vector3 c = TerrainLedgeCalmMath.Calm(grey, 1f, Calm);
            Assert.AreEqual(grey.x, c.x, 1e-5f); Assert.AreEqual(grey.y, c.y, 1e-5f); Assert.AreEqual(grey.z, c.z, 1e-5f);
        }
    }
}
