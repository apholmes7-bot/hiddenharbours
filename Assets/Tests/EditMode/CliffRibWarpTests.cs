using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The cliff rib warp (<see cref="CliffRibWarpDef"/>): the kit profile's 4 m rib no longer lies down a run of coast at
    /// one fixed pitch, nothing tears at a chunk cut, and with the warp off every face is laid exactly as before.
    /// </summary>
    public class CliffRibWarpTests
    {
        /// <summary>The kit profile's dominant rib: its third harmonic in the 12 m tile.</summary>
        const float RibMetres = 4f;

        private List<StPetersCliffWalls.Chunk> _chunks;
        private CliffRibWarpDef _def;
        private CliffRibWarp _drawn;

        [OneTimeSetUp]
        public void ReadTheShippedWarpAndTheScenesWalls()
        {
            _def = Resources.Load<CliffRibWarpDef>(CliffRibWarpDef.ResourcePath);
            Assert.IsNotNull(_def, $"no CliffRibWarpDef at Resources/{CliffRibWarpDef.ResourcePath}: every cliff falls back " +
                                   "to the kit's fixed 4 m rib");
            _drawn = _def.Drawn();
            _drawn.StepMetres = CliffCatalog.FaceMetresS / CliffCatalog.FaceWidth;   // as every wall steps it
            var walls = StPetersCliffWalls.WallsInScene(File.ReadAllText(StPetersLayerRefreshTests.ScenePath));
            _chunks = new List<StPetersCliffWalls.Chunk>(walls.Count);
            foreach (StPetersCliffWalls.SceneWall w in walls) _chunks.Add(w.AsChunk());
        }

        [Test]
        public void Off_IsExactlyTheOldLaying()
        {
            CliffRibWarp off = default;
            Assert.IsTrue(off.IsOff);
            var probe = new[] { new Vector2(0f, 0f), new Vector2(183.4f, -31.7f), new Vector2(-310.2f, -164.9f) };
            foreach (Vector2 p in probe)
            {
                Assert.AreEqual(0f, CliffRibWarpMath.ShiftMetres(in off, p), "off must slide nothing, to the bit");
                Assert.AreEqual(1f, CliffRibWarpMath.DepthGain(in off, p), "off must keep the kit's own depth, to the bit");
            }

            var disabled = ScriptableObject.CreateInstance<CliffRibWarpDef>();
            try
            {
                disabled.ShiftWaves = new[] { new CliffRibWarpWave(29f, 0.5f, 0f, 0f) };
                disabled.Enabled = false;
                Assert.IsTrue(disabled.Drawn().IsOff, "a disabled warp must draw as off");
            }
            finally { Object.DestroyImmediate(disabled); }
        }

        [Test]
        public void TheShippedWarp_IsOn_AndNeverFoldsTheFace()
        {
            Assert.IsTrue(_def.Enabled, "the shipped warp is off: the St Peters scallop repeat is back");
            Assert.IsNotNull(_drawn.Shift, "the shipped warp slides nothing");
            float slope = CliffRibWarpMath.MaxSlope(_drawn.Shift);
            Assert.LessOrEqual(slope, Mathf.Min(_def.MaxSlopeAllowed, CliffRibWarpDef.SlopeCeiling) + 1e-5f,
                "the drawn shift is steeper than its cap: a texel is stretched past what the Def allows");
            Assert.Less(slope, 1f, "a shift slope of 1 folds the face back on itself");
            Assert.GreaterOrEqual(_drawn.DepthMin, 0f);
            Assert.GreaterOrEqual(_drawn.DepthMax, _drawn.DepthMin);
        }

        [Test]
        public void TooSteepAWarp_IsScaledDownToItsCap()
        {
            var steep = ScriptableObject.CreateInstance<CliffRibWarpDef>();
            try
            {
                steep.MaxSlopeAllowed = 0.3f;
                steep.ShiftWaves = new[] { new CliffRibWarpWave(5f, 2f, 0f, 0f), new CliffRibWarpWave(7f, 2f, 60f, 0.3f) };
                float drawn = CliffRibWarpMath.MaxSlope(steep.Drawn().Shift);
                Assert.AreEqual(0.3f, drawn, 1e-5f);
                Assert.AreEqual(2f, steep.ShiftWaves[0].Amplitude, "Drawn() must not write back into the Def");
            }
            finally { Object.DestroyImmediate(steep); }
        }

        [Test]
        public void EveryWavelength_StaysOffTheTilesFractions()
        {
            float tile = CliffCatalog.FaceMetresS;
            var all = new List<CliffRibWarpWave>();
            if (_def.ShiftWaves != null) all.AddRange(_def.ShiftWaves);
            if (_def.DepthWaves != null) all.AddRange(_def.DepthWaves);
            Assert.Greater(all.Count, 0);
            foreach (CliffRibWarpWave w in all)
                for (int p = 1; p <= 4; p++)
                for (int q = 1; q <= 4; q++)
                {
                    float lock_ = tile * p / q;
                    Assert.Greater(Mathf.Abs(w.WavelengthMetres - lock_) / lock_, 0.04f,
                        $"a {w.WavelengthMetres} m wave sits within 4% of {lock_:0.##} m ({p}/{q} of the {tile} m tile): it " +
                        "comes round with the ribs it is meant to break");
                }
        }

        /// <summary>The shipped warp at both copies of every station two chunks share: the same tile coordinate and the
        /// same depth gain from either side, so the cut stays closed with the warp on.</summary>
        [Test]
        public void EveryChunkBoundary_StillClosesWithTheWarpOn()
        {
            float texel = CliffCatalog.FaceMetresS / CliffCatalog.FaceWidth;
            int cuts = 0;
            float worst = 0f, worstGain = 0f;
            for (int i = 0; i + 1 < _chunks.Count; i++)
            {
                StPetersCliffWalls.Chunk a = _chunks[i], b = _chunks[i + 1];
                if (a.Samples[a.Samples.Count - 1].BrowPlan != b.Samples[0].BrowPlan) continue;
                cuts++;
                WarpedAt(a, a.Samples.Count - 1, out float ua, out float ga);
                WarpedAt(b, 0, out float ub, out float gb);
                worst = Mathf.Max(worst, Mathf.Abs(ua - ub));
                worstGain = Mathf.Max(worstGain, Mathf.Abs(ga - gb));
                Assert.Less(Mathf.Abs(ua - ub), texel * 0.1f,
                    $"a shared station at {b.Samples[0].BrowPlan} is warped to {ua:F4} m from one chunk and {ub:F4} m " +
                    "from the next: the wall tears open at the cut");
                Assert.Less(Mathf.Abs(ga - gb), 1e-3f,
                    $"a shared station at {b.Samples[0].BrowPlan} stands at depth gain {ga:F4} from one chunk and {gb:F4} " +
                    "from the next: the foot steps at the cut");
            }
            Assert.Greater(cuts, 0, "no chunk boundaries at all: this pin is asserting nothing");
            Debug.Log($"[cliff-rib-warp] {cuts} cuts close with the warp on; worst {worst * 1000f:F3} mm, worst gain step {worstGain:F5}.");
        }

        /// <summary>
        /// The repeat itself, on St Peters' real coast: the rib (the profile's 4 m harmonic, weighted by the depth gain)
        /// laid down every unbroken run of wall, and its autocorrelation at every lag from 2 m to 14 m. Off, it comes
        /// back whole every 4 m; on, no lag finds it again.
        /// </summary>
        [Test]
        public void TheFourMetreRib_NeverComesRoundAgain_OnStPetersCoast()
        {
            float off = WorstRepeat(default, out float offLag);
            float on = WorstRepeat(_drawn, out float onLag);
            Debug.Log($"[cliff-rib-warp] the rib's strongest repeat, 2-14 m: off {off:F3} at {offLag:F2} m, on {on:F3} at {onLag:F2} m.");
            Assert.Greater(off, 0.9f, "the unwarped rib should repeat almost exactly: the measure is not measuring the rib");
            // The bound is physical: a shift whose slope is held to s moves two points L apart by at most s x L relative
            // to each other, so at the shipped 0.45 a 4 m lag decorrelates only partly (0.839 at the first ship, 1.000 off).
            // The pin holds the repeat clearly broken and catches a warp gone dead.
            Assert.Less(on, 0.9f, $"with the warp on the rib still comes round at {onLag:F2} m ({on:F3}): the scallops read as a row");
        }

        // -----------------------------------------------------------------------------------------------------------

        /// <summary>Station <paramref name="i"/>'s warped tile coordinate (metres along the run) and depth gain, as the
        /// mesh lays them: the column values either side of a cut station lerped by its fraction.</summary>
        void WarpedAt(StPetersCliffWalls.Chunk c, int i, out float u, out float gain)
        {
            Columns(c, out CliffWallColumns columns, out float[] along);
            int g = columns.Gap[i];
            float t = columns.At[i];
            u = Val(g);
            gain = CliffRibWarpMath.DepthGain(in _drawn, columns.AlongBrow[g]);
            if (t > 0f)
            {
                u = Mathf.Lerp(u, Val(g + 1), t);
                gain = Mathf.Lerp(gain, CliffRibWarpMath.DepthGain(in _drawn, columns.AlongBrow[g + 1]), t);
            }

            float Val(int k) => c.AlongOffsetMetres + along[k] + CliffRibWarpMath.ShiftMetres(in _drawn, columns.AlongBrow[k]);
        }

        static void Columns(StPetersCliffWalls.Chunk c, out CliffWallColumns columns, out float[] along)
        {
            Assert.IsTrue(CliffWallSurface.TryColumns(c.Samples.ToArray(), c.TextureBrow?.ToArray(), c.SliceAt?.ToArray(),
                                                      c.SliceBefore, c.SliceAfter, out columns, out string problem),
                          $"a wall's slice data does not lay: {problem}");
            along = new float[columns.AlongBrow.Length];
            for (int k = 1; k < along.Length; k++) along[k] = along[k - 1] + Vector2.Distance(columns.AlongBrow[k - 1], columns.AlongBrow[k]);
        }

        /// <summary>The rib's strongest autocorrelation over lags 2..14 m, pooled across St Peters' runs.</summary>
        float WorstRepeat(CliffRibWarp warp, out float atLag)
        {
            const float step = 0.05f;
            var runs = new List<List<float>>();
            int run = int.MinValue;
            List<float> sig = null;
            float lastS = float.NegativeInfinity;
            foreach (StPetersCliffWalls.Chunk c in _chunks)
            {
                if (c.RunIndex != run) { run = c.RunIndex; sig = new List<float>(); runs.Add(sig); lastS = float.NegativeInfinity; }
                Columns(c, out CliffWallColumns cols, out float[] along);
                for (int k = 0; k + 1 < along.Length; k++)
                {
                    float s0 = c.AlongOffsetMetres + along[k], s1 = c.AlongOffsetMetres + along[k + 1];
                    if (s1 - s0 < 1e-4f) continue;
                    for (float s = Mathf.Max(s0, lastS + step); s < s1; s += step)
                    {
                        float f = (s - s0) / (s1 - s0);
                        Vector2 p = Vector2.Lerp(cols.AlongBrow[k], cols.AlongBrow[k + 1], f);
                        float u = s + CliffRibWarpMath.ShiftMetres(in warp, p);
                        sig.Add(CliffRibWarpMath.DepthGain(in warp, p) * Mathf.Cos(2f * Mathf.PI * u / RibMetres));
                        lastS = s;
                    }
                }
            }

            float best = float.NegativeInfinity;
            atLag = 0f;
            for (int lag = Mathf.RoundToInt(2f / step); lag <= Mathf.RoundToInt(14f / step); lag++)
            {
                double num = 0, den0 = 0, den1 = 0;
                foreach (List<float> r in runs)
                {
                    if (r.Count <= lag) continue;
                    double m = 0;
                    for (int i = 0; i < r.Count; i++) m += r[i];
                    m /= r.Count;
                    for (int i = 0; i + lag < r.Count; i++)
                    {
                        double x = r[i] - m, y = r[i + lag] - m;
                        num += x * y; den0 += x * x; den1 += y * y;
                    }
                }
                if (den0 <= 0 || den1 <= 0) continue;
                float ac = (float)(num / System.Math.Sqrt(den0 * den1));
                if (ac > best) { best = ac; atLag = lag * step; }
            }
            return best;
        }
    }
}
