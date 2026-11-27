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

        static readonly CliffHeight Mass = new CliffHeight(0, 14, 0f, 0f, false, 0.2f, 0.35f, 0.06f,
                                                           0.15f, 112, new Vector2(0.3f, 0.55f), 18, 3, true);

        /// <summary>Coordinator 11 27: height from MASS, not ruled lines. A buttress's shade is a broad upright mass,
        /// metres wide, whose edges jog at every bed, so no column of the face is one straight line brow to toe.</summary>
        [Test]
        public void Buttresses_AreBroadMasses_WhoseEdgesNeverRunStraight()
        {
            const int width = 112 * 30, height = 360;
            int shaded = 0;
            var straight = new bool[width];
            for (int x = 0; x < width; x++) straight[x] = true;
            for (int x = 0; x < width; x++)
            {
                int first = CliffHeightMath.ButtressShade(in Mass, x, 0), firstLeft = CliffHeightMath.ButtressShade(in Mass, x - 1, 0);
                for (int y = 0; y < height; y++)
                {
                    int s = CliffHeightMath.ButtressShade(in Mass, x, y);
                    Assert.That(s, Is.InRange(0, 1));
                    shaded += s;
                    if (s != first || CliffHeightMath.ButtressShade(in Mass, x - 1, y) != firstLeft) straight[x] = false;
                }
            }
            float share = shaded / (float)(width * height);
            Assert.That(share, Is.InRange(0.3f, 0.55f), "the shaded share is outside its range");
            // An edge column: where shade meets light on row 0. None of them may stay an edge all the way down.
            for (int x = 1; x < width; x++)
            {
                bool edge = CliffHeightMath.ButtressShade(in Mass, x, 0) != CliffHeightMath.ButtressShade(in Mass, x - 1, 0);
                if (edge) Assert.IsFalse(straight[x], $"the buttress edge at column {x} runs straight down the face");
            }
            // The shade comes in runs metres wide, never thin lines.
            int runs = 0, inRun = 0, shortest = int.MaxValue;
            for (int x = 0; x < width; x++)
            {
                if (CliffHeightMath.ButtressShade(in Mass, x, 100) == 1) { inRun++; continue; }
                if (inRun > 0) { runs++; shortest = Mathf.Min(shortest, inRun); inRun = 0; }
            }
            Assert.Greater(runs, 20);
            Assert.GreaterOrEqual(shortest, 24, "a buttress's shade is a thin line, not a mass");
        }

        [Test]
        public void TheDeepFoot_IsOneTier_OnlyAtTheFoot()
        {
            for (int x = 0; x < 256; x++)
            {
                Assert.AreEqual(0, CliffHeightMath.RampDeep(in Mass, 0.1f, x));
                Assert.AreEqual(0, CliffHeightMath.RampDeep(in Mass, 0.6f, x));
                Assert.AreEqual(1, CliffHeightMath.RampDeep(in Mass, 0.97f, x));
            }
            Assert.AreEqual(0, CliffHeightMath.RampDeep(in Height, 0.99f, 4), "a mix with no deep share has none");
            Assert.AreEqual(0, CliffHeightMath.ButtressShade(in Height, 40, 40), "a mix with no buttresses has none");
        }

        [Test]
        public void TheMass_IsPerMapping_AndNeverOnADecal()
        {
            Assert.IsTrue(Mass.MassOn(true, false));
            Assert.IsTrue(Mass.MassOn(false, false), "a mix that asks for sheared walls gets them");
            Assert.IsFalse(Mass.MassOn(true, true), "a decal takes no height");
            Assert.IsFalse(Height.MassOn(false, false), "a mix that does not ask leaves sheared walls alone");
            Assert.AreEqual(0f, Mass.ShaderButtress(false).w);
            Assert.AreEqual(1f, Mass.ShaderButtress(true).w);
            Assert.AreEqual(0f, Height.ShaderButtress(true).w, "a mix with no buttresses switches the shader's off");
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
            Assert.IsTrue(h.RampOn, "St Peters' walls carry no ramp");
            Assert.Greater(h.RampFootShare, 0f);
            Assert.Greater(h.RampDeepShare, 0f, "the foot is only a band darker: it barely shows");
            Assert.IsTrue(h.ButtressOn, "St Peters' walls are not grouped into upright masses");
            Assert.IsFalse(h.JointLitEdge, "lit-edge pairs read as scaffolding (coordinator 11 27)");
            Assert.IsFalse(h.JointsOn, "ruled upright joints on the big walls were rejected (owner 11 27)");
            Assert.IsTrue(mix.StepShearedWalls, "the curved walls are still laid down the column: their bends are not pixel art");
            Assert.IsTrue(h.MassOn(false, false), "the sheared, stepped walls get no height pass");
            Assert.IsNotNull(mix.ShearedTurfLip, "the stepped walls hang no terrace");
            Assert.Greater(mix.ShearedTurfLip.TerraceRows.y, 0, "the stepped walls' lip paints no grass terrace");
            Assert.AreEqual(Vector2Int.zero, mix.TurfLip.TerraceRows, "the screen-grid walls' lip grew a terrace");
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
            foreach (string name in new[] { "_JointA", "_JointB", "_FaceRamp", "_Buttress", "_ButtressB", "float4 uv4", "float4 band",
                                            "tier -= shift.y", "band += shift.x" })
                StringAssert.Contains(name, face);
            foreach (string salt in new[] { "41.0", "42.0", "43.0", "44.0", "45.0", "46.0", "47.0", "48.0", "49.0" })
                StringAssert.Contains(salt, face, "a hash salt the twin uses is missing from the shader");
            string ground = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Art/Shaders/HiddenHarboursTerrainSplat.shader"));
            StringAssert.Contains("float4 _ShoreLedgeCalm", ground);
            StringAssert.Contains("TerrainLedgeCalm(col, w[5] + w[11] + w[12], _ShoreLedgeCalm)", ground);
        }
    }

    /// <summary>Owner 11 27: the bends of the curved walls did not read as pixel art. A stepped wall is laid on the
    /// screen grid with its bedding counted down from its own brow (<see cref="CliffWallGeometry.SteppedUv"/>).</summary>
    public sealed class CliffSteppedStrataTests
    {
        const float S = 4f, T = 4f, Cell = 1f / CliffWaterlineMath.TexelsPerMetre;

        [Test]
        public void ACell_IsOneTexel_SquareAndUnrotated()
        {
            var p = new Vector2(12.3f, -40.7f);
            Vector2 a = CliffWallGeometry.SteppedUv(p, -38f, S, T);
            Vector2 right = CliffWallGeometry.SteppedUv(p + new Vector2(Cell, 0f), -38f, S, T);
            Vector2 down = CliffWallGeometry.SteppedUv(p + new Vector2(0f, -Cell), -38f, S, T);
            Assert.AreEqual(Cell / S, right.x - a.x, 1e-6f, "a cell across is not one texel along u");
            Assert.AreEqual(0f, right.y - a.y, 1e-6f, "a cell across moves v: the texel is sheared");
            Assert.AreEqual(Cell / T, down.y - a.y, 1e-6f, "a cell down is not one texel down v");
            Assert.AreEqual(0f, down.x - a.x, 1e-6f, "a cell down moves u: the texel is rotated");
            Assert.AreEqual(CliffWallGeometry.ScreenPlanarUv(p, S, T).x, a.x, 1e-6f, "u is not the screen grid's");
        }

        [Test]
        public void TheBedding_FollowsTheBrow_InWholeTexelSteps()
        {
            // Down from the brow is the same v wherever the brow stands: bedding parallel to the brow.
            Vector2 high = CliffWallGeometry.SteppedUv(new Vector2(0f, -10f), -8f, S, T);
            Vector2 low = CliffWallGeometry.SteppedUv(new Vector2(0f, -13f), -11f, S, T);
            Assert.AreEqual(high.y, low.y, 1e-5f);
            // The texel row a cell centre draws, across a brow that falls a third of a texel per column: the rows
            // step by whole texels, never a fraction (point sampled at the cell's centre).
            int texelsPerTile = Mathf.RoundToInt(T * CliffWaterlineMath.TexelsPerMetre);
            int steps = 0, last = int.MinValue;
            for (int col = 0; col < 30; col++)
            {
                float brow = -8f - col * Cell / 3f;
                float y = (Mathf.Floor(-12f / Cell) + 0.5f) * Cell;           // one row of cells
                int row = Mathf.FloorToInt(CliffWallGeometry.SteppedUv(new Vector2(col * Cell, y), brow, S, T).y * texelsPerTile);
                if (last != int.MinValue)
                {
                    Assert.That(last - row, Is.InRange(0, 1), "the bedding jumped more than a texel between two columns");
                    steps += last - row;
                }
                last = row;
            }
            Assert.That(steps, Is.InRange(8, 11), "the bedding does not follow the brow's fall");
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
