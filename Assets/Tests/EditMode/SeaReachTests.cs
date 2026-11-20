using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE SEA'S REACH (owner, 2026-11-19: "once they arent attached to the waves they should remain
    /// still pools of water"). The pure half: <see cref="SeaReachField"/> floods a synthetic seabed from
    /// the open sea, and <see cref="SeaReachMath.Factor01"/> turns (tide, sill) into the swell share.
    /// Pinned: open water is 1 at every tide; a basin behind a bar is 0 once the tide falls below the bar
    /// and 1 above it; the hand-over is continuous; the sills agree with an independent flood at many
    /// tides; the same inputs give the same field and the same factor for the same seed and time; no
    /// diagonal or one-cell leak; beach hollows stay open; reads allocate nothing; and the Core seam and
    /// the fetch sample carry the factor without touching the older look where it is 1.
    /// </summary>
    public class SeaReachTests
    {
        private const float Blend = 0.25f;

        // 20 x 12 cells of 1 m. Columns 0-3: the sea (-3, every row, so the left edge seeds). Columns 4-7:
        // shallow open water (-2). Column 8: a one-cell bar (0). Columns 9-14, rows 2-9: a basin (-1.5).
        // Everything else: land (+3), including the other three edges.
        private const int W = 20, H = 12;
        private static float[] BarAndBasin()
        {
            var e = new float[W * H];
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float v = 3f;
                if (x <= 3) v = -3f;
                else if (x <= 7) v = -2f;
                else if (x == 8) v = 0f;
                else if (x <= 14 && y >= 2 && y <= 9) v = -1.5f;
                e[y * W + x] = v;
            }
            return e;
        }

        private static SeaReachSettings Settings(float minPool = 0.05f)
            => new SeaReachSettings
            {
                Enabled = 1f, ReconnectBlendMeters = Blend, SeedMaxElevationMeters = -2.5f,
                MinPoolDepthMeters = minPool, QuantumMeters = 0.001f, WetBandMeters = 0.05f,
            };

        private static SeaReachField Build(float[] e, int w = W, int h = H, float minPool = 0.05f)
            => SeaReachField.Build(e, w, h, Vector2.zero, new Vector2(w, h), Settings(minPool));

        private static Vector2 Cell(int x, int y) => new Vector2(x + 0.5f, y + 0.5f);

        [Test]
        public void OpenSeaStaysOneAtBothTides()
        {
            var f = Build(BarAndBasin());
            Assert.NotNull(f);
            foreach (float level in new[] { -2.2f, -1f, 0f, 2.2f })
            {
                Assert.AreEqual(1f, f.FactorAt(Cell(1, 5), level, Blend), $"deep sea at {level}");
                Assert.AreEqual(1f, f.FactorAt(Cell(6, 5), level, Blend), $"shallow open water at {level}");
            }
            Assert.AreEqual(SeaReachMath.AlwaysOpen, f.SillAtCell(6, 5));
        }

        [Test]
        public void BasinBehindBarIsCutOffAtLowTideAndJoinedAtHighTide()
        {
            var f = Build(BarAndBasin());
            Assert.AreEqual(0f, f.SillAtCell(11, 5), 1e-6f, "the basin's sill is the bar's crest");
            Assert.AreEqual(0f, f.FactorAt(Cell(11, 5), -0.5f, Blend), "low tide: still");
            Assert.AreEqual(0f, f.FactorAt(Cell(11, 5), -1.2f, Blend), "lower still: still");
            Assert.AreEqual(1f, f.FactorAt(Cell(11, 5), 2.2f, Blend), "high tide: the sea's swell");
            Assert.AreEqual(1f, f.FactorAt(Cell(11, 5), Blend, Blend), "one blend above the bar: all of it");
        }

        [Test]
        public void ReconnectionAndCutOffAreContinuous()
        {
            const float sill = 0f;
            Assert.AreEqual(0f, SeaReachMath.Factor01(sill, sill, Blend), "at the sill");
            Assert.AreEqual(0.5f, SeaReachMath.Factor01(sill + Blend * 0.5f, sill, Blend), 1e-6f, "half the blend");
            Assert.AreEqual(1f, SeaReachMath.Factor01(sill + Blend, sill, Blend), "the top of the blend");
            // No step anywhere: sweep the level through the blend in tiny increments, both directions.
            float step = 1e-4f, maxJump = 0f, prev = SeaReachMath.Factor01(-0.1f, sill, Blend);
            for (float level = -0.1f; level <= 0.4f; level += step)
            {
                float v = SeaReachMath.Factor01(level, sill, Blend);
                Assert.GreaterOrEqual(v, prev - 1e-7f, "monotonic in the level");
                maxJump = Mathf.Max(maxJump, Mathf.Abs(v - prev));
                prev = v;
            }
            // The smoothstep's steepest slope is 1.5 / blend; a step of 1e-4 m moves it at most 6e-4.
            Assert.Less(maxJump, 1.5f / Blend * step * 1.01f, "no pop through the hand-over");
            Assert.AreEqual(SeaReachMath.Factor01(sill - 1e-6f, sill, Blend),
                            SeaReachMath.Factor01(sill + 1e-6f, sill, Blend), 1e-6f, "both sides of the sill meet");
        }

        [Test]
        public void SillsMatchAnIndependentFloodAtManyTides()
        {
            // A rough deterministic seabed in centimetre steps (exact on the 1 mm quantum), sea along the
            // bottom edge, minPool 0 so every cell's sill is its true minimax path height.
            const int w = 40, h = 30;
            var rng = new System.Random(1234);
            var e = new float[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float trend = -3f + 6f * y / (h - 1);
                e[y * w + x] = Mathf.Round((trend + (float)(rng.NextDouble() * 2.4 - 1.2)) * 100f) / 100f;
            }
            for (int x = 0; x < w; x++) e[x] = -3f;   // the open sea's edge
            var f = SeaReachField.Build(e, w, h, Vector2.zero, new Vector2(w, h), Settings(0f));
            Assert.NotNull(f);

            int checkedCells = 0;
            foreach (float level in new[] { -2.4995f, -1.7495f, -0.9995f, -0.2495f, 0.5005f, 1.2505f, 2.0005f })
            {
                bool[] joined = IndependentFlood(e, w, h, level, -2.5f);
                for (int i = 0; i < w * h; i++)
                {
                    float sill = f.SillAtIndex(i);
                    bool byField = sill == SeaReachMath.AlwaysOpen ? e[i] < level : sill < level;
                    if (sill == SeaReachMath.Unreachable) byField = false;
                    Assert.AreEqual(joined[i], byField, $"cell {i % w},{i / w} at level {level} (sill {sill}, bed {e[i]})");
                    checkedCells++;
                }
            }
            Assert.AreEqual(7 * w * h, checkedCells);
        }

        // Plain breadth-first flood through wet cells (bed below the level) from wet seed-edge cells.
        private static bool[] IndependentFlood(float[] e, int w, int h, float level, float seedMax)
        {
            var joined = new bool[w * h];
            var q = new Queue<int>();
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                int i = y * w + x;
                if (edge && e[i] <= seedMax && e[i] < level) { joined[i] = true; q.Enqueue(i); }
            }
            while (q.Count > 0)
            {
                int i = q.Dequeue();
                int x = i % w, y = i / w;
                void Try(int j) { if (!joined[j] && e[j] < level) { joined[j] = true; q.Enqueue(j); } }
                if (x > 0) Try(i - 1);
                if (x < w - 1) Try(i + 1);
                if (y > 0) Try(i - w);
                if (y < h - 1) Try(i + w);
            }
            return joined;
        }

        [Test]
        public void SameInputsGiveTheSameFieldAndTheSameFactorForTheSameSeedAndTime()
        {
            var a = Build(BarAndBasin());
            var b = Build(BarAndBasin());
            Assert.AreEqual(a.CellCount, b.CellCount);
            for (int i = 0; i < a.CellCount; i++)
                Assert.AreEqual(a.SillAtIndex(i), b.SillAtIndex(i), $"cell {i}");

            // The level is the deterministic tide: same time, same factor, in any query order.
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                var times = new[] { 0.0, 3600.0 * 3.1, 3600.0 * 7.7, 86400.0 * 2.25, 3600.0 * 3.1 };
                var first = new float[times.Length];
                for (int k = 0; k < times.Length; k++)
                    first[k] = a.FactorAt(Cell(11, 5), TideModel.Height(times[k], TideProfile.CoddleCove, cfg), Blend);
                for (int k = times.Length - 1; k >= 0; k--)
                    Assert.AreEqual(first[k],
                        b.FactorAt(Cell(11, 5), TideModel.Height(times[k], TideProfile.CoddleCove, cfg), Blend),
                        $"time {times[k]}");
                Assert.AreEqual(first[1], first[4], "the same instant asked twice");
            }
            finally { UnityEngine.Object.DestroyImmediate(cfg); }
        }

        [Test]
        public void ACroppedBasinIsNotOceanAndASealedOneJoinsOnlyOverTheLand()
        {
            // The basin runs off the map's right edge at -1.5 m: shallower than any seed, so the raster
            // edge it touches is NOT the ocean, and its sill is still the bar.
            var e = BarAndBasin();
            for (int y = 2; y <= 9; y++)
            for (int x = 9; x < W; x++) e[y * W + x] = -1.5f;
            var cropped = Build(e);
            Assert.AreEqual(0f, cropped.SillAtCell(W - 1, 5), 1e-6f, "the edge cell is behind the bar, not open sea");
            Assert.AreEqual(0f, cropped.FactorAt(Cell(W - 1, 5), -0.5f, Blend));

            // Seal the bar to land height: the basin joins the sea only over the land (3 m), so no tide does.
            e = BarAndBasin();
            for (int y = 0; y < H; y++) e[y * W + 8] = 3f;
            var f = Build(e);
            Assert.AreEqual(3f, f.SillAtCell(11, 5), 1e-6f);
            Assert.AreEqual(0f, f.FactorAt(Cell(11, 5), 2.2f, Blend), "no tide reaches a sealed basin");
            Assert.AreEqual(0f, SeaReachMath.Factor01(100f, SeaReachMath.Unreachable, Blend), "never reached = never");

            var dry = new float[W * H];
            for (int i = 0; i < dry.Length; i++) dry[i] = 1f;
            Assert.IsNull(Build(dry), "no open-sea seed: no field, the older look");
            Assert.IsNull(SeaReachField.Build(null, W, H, Vector2.zero, Vector2.one, Settings()));
            var off = Settings(); off.Enabled = 0f;
            Assert.IsNull(SeaReachField.Build(BarAndBasin(), W, H, Vector2.zero, Vector2.one, off), "disabled");
        }

        [Test]
        public void NarrowBarsAndDiagonalContactsDoNotLeak()
        {
            // 5 x 5: sea at (0..1, 0..1) seeded from the corner; a pool cell at (2,2) touching (1,1) only
            // diagonally; land everywhere else.
            const int w = 5, h = 5;
            var e = new float[w * h];
            for (int i = 0; i < e.Length; i++) e[i] = 3f;
            e[0] = e[1] = e[w] = e[w + 1] = -3f;
            e[2 * w + 2] = -1f;
            var f = SeaReachField.Build(e, w, h, Vector2.zero, new Vector2(w, h), Settings());
            Assert.AreEqual(3f, f.SillAtCell(2, 2), 1e-4f, "only over the land does the diagonal pool join");
            Assert.AreEqual(0f, f.FactorAt(Cell(2, 2), 2f, Blend));

            // The one-cell bar of the main fixture: a bar one cell wide still holds the basin.
            var g = Build(BarAndBasin());
            Assert.AreEqual(0f, g.FactorAt(Cell(9, 5), -0.01f, Blend), "the cell right behind the bar");
        }

        [Test]
        public void TheReadFeathersBetweenWetCellsAndDryGroundNeverVotes()
        {
            var f = Build(BarAndBasin());
            // Cut off (-0.5 m, the bar at 0 is dry): the pool's rim against the bar stays still, and the
            // sea's side of the same bar keeps all of its swell. Dry ground has no say either way.
            Assert.AreEqual(0f, f.FactorAt(new Vector2(9.0f, 5.5f), -0.5f, Blend), "the pool's rim at the dry bar");
            Assert.AreEqual(0f, f.FactorAt(new Vector2(9.2f, 2.1f), -0.5f, Blend), "the pool's corner against dry land");
            Assert.AreEqual(1f, f.FactorAt(new Vector2(8.0f, 5.5f), -0.5f, Blend), "the sea against the dry bar");

            // Reconnecting (half the blend over the bar, now wet and open): the pool's outline is a ramp
            // from the bar's 1 to the pool's 0.5 over one cell, never a step.
            float level = Blend * 0.5f;
            Assert.AreEqual(0.75f, f.FactorAt(new Vector2(9.0f, 5.5f), level, Blend), 1e-5f, "halfway between centres");
            float prev = f.FactorAt(new Vector2(8.5f, 5.5f), level, Blend), maxJump = 0f;
            Assert.AreEqual(1f, prev, "the bar's own centre");
            for (float x = 8.5f; x <= 9.5f; x += 1e-3f)
            {
                float v = f.FactorAt(new Vector2(x, 5.5f), level, Blend);
                Assert.LessOrEqual(v, prev + 1e-6f, "falls toward the pool");
                maxJump = Mathf.Max(maxJump, Mathf.Abs(v - prev));
                prev = v;
            }
            Assert.Less(maxJump, 2e-3f, "no cell step across the outline");
            Assert.AreEqual(0.5f, f.FactorAt(new Vector2(9.5f, 5.5f), level, Blend), 1e-5f, "the pool's own centre");
            Assert.AreEqual(1f, f.FactorAt(new Vector2(2.3f, 7.7f), level, Blend), "open water off the centres: exactly 1");
        }

        [Test]
        public void BeachHollowsShallowerThanTheMinimumStayOpen()
        {
            var e = BarAndBasin();
            // A 3 cm hollow in the open shallows (bed -2 around it, -2.03 in it).
            e[5 * W + 6] = -2.03f;
            var f = Build(e);
            Assert.AreEqual(SeaReachMath.AlwaysOpen, f.SillAtCell(6, 5), "3 cm < 5 cm: a ripple, not a pool");
            Assert.AreEqual(1f, f.FactorAt(Cell(6, 5), -2.01f, Blend));
            // The same hollow with the minimum at zero is a (tiny) pool: the rule is the setting's.
            Assert.AreEqual(-2f, Build(e, minPool: 0f).SillAtCell(6, 5), 1e-4f);
        }

        [Test]
        public void ReadsAllocateNothingAndOffTheMapIsOpen()
        {
            var f = Build(BarAndBasin());
            float sum = 0f;
            for (int i = 0; i < 100; i++) sum += f.FactorAt(Cell(i % W, i % H), -0.5f, Blend);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) sum += f.FactorAt(Cell(i % W, i % H), -0.5f + i * 1e-4f, Blend);
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0L, after - before, "sampling allocates nothing");
            Assert.Greater(sum, 0f);
            Assert.AreEqual(1f, f.FactorAt(new Vector2(-1f, 5f), -0.5f, Blend), "off the map: not ours to judge");
            Assert.AreEqual(1f, f.FactorAt(new Vector2(W + 0.1f, 5f), -0.5f, Blend));
        }

        [Test]
        public void TheCoreSeamPublishesAndClearsByOwnerAndIsOneWithNoField()
        {
            object owner = new object(), other = new object();
            var f = Build(BarAndBasin());
            try
            {
                Assert.AreEqual(1f, SeaReach.FactorAt(Cell(11, 5), -0.5f), "no field: 1");
                SeaReach.Publish(owner, f, Blend);
                Assert.IsTrue(SeaReach.IsActive);
                Assert.AreEqual(0f, SeaReach.FactorAt(Cell(11, 5), -0.5f));
                Assert.AreEqual(1f, SeaReach.FactorAt(Cell(1, 5), -0.5f));
                SeaReach.Clear(other);
                Assert.IsTrue(SeaReach.IsActive, "only the owner clears");
                SeaReach.SetBlend(owner, 1f);
                Assert.AreEqual(0.5f, SeaReach.FactorAt(Cell(11, 5), 0.5f), 1e-6f, "the blend is live");
                SeaReach.Clear(owner);
                Assert.IsFalse(SeaReach.IsActive);
                Assert.AreEqual(1f, SeaReach.FactorAt(Cell(11, 5), -0.5f));
            }
            finally { SeaReach.Clear(owner); }
        }

        [Test]
        public void TheFetchSampleCarriesTheReachAndIsUntouchedAtOne()
        {
            WaveFetchSample uniform = 0.8f;
            Assert.AreEqual(0.8f, uniform.WithSeaReach(1f).Uniform, "1 = unchanged");
            Assert.AreEqual(0.4f, uniform.WithSeaReach(0.5f).Uniform, 1e-6f);
            Assert.AreEqual(0f, uniform.WithSeaReach(0f).Uniform);
            Assert.AreEqual(0f, uniform.WithSeaReach(0f).ForWavelength(20f));

            var spectral = new WaveFetchSample(1f, WaveFetchSettings.Default).WithSwell(0.9f);
            float open = spectral.ForTrain(SwellMath.FirstSlot, 60f);
            Assert.AreEqual(open, spectral.WithSeaReach(1f).ForTrain(SwellMath.FirstSlot, 60f), "1 = unchanged");
            Assert.AreEqual(0f, spectral.WithSeaReach(0f).ForTrain(SwellMath.FirstSlot, 60f), "swell trains too");
            Assert.AreEqual(0f, spectral.WithSeaReach(0f).ForTrain(0, 8f), "wind trains too");
            Assert.AreEqual(open * 0.25f, spectral.WithSeaReach(0.5f).WithSeaReach(0.5f)
                                                  .ForTrain(SwellMath.FirstSlot, 60f), 1e-6f, "reaches compose");
            Assert.AreEqual(1f, default(WaveFetchSample).SeaReach01, "a default sample keeps all of it");
        }

        [Test]
        public void TheConfigCarriesTheDefaultsAndTheShaderTwinsTheRule()
        {
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                var s = cfg.SeaReach;
                Assert.IsTrue(s.IsUsable);
                Assert.AreEqual(SeaReachSettings.Default.ReconnectBlendMeters, s.ReconnectBlendMeters);
                Assert.AreEqual(SeaReachSettings.Default.SeedMaxElevationMeters, s.SeedMaxElevationMeters);
                Assert.AreEqual(SeaReachSettings.Default.MinPoolDepthMeters, s.MinPoolDepthMeters);
                Assert.AreEqual(SeaReachSettings.Default.WetBandMeters, s.WetBandMeters);
            }
            finally { UnityEngine.Object.DestroyImmediate(cfg); }

#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/Config/GameConfig.asset");
            Assert.NotNull(asset, "the shipped GameConfig");
            Assert.IsTrue(asset.SeaReach.IsUsable, "the shipped config turns the reach on with usable values");

            string hlsl = System.IO.File.ReadAllText("Assets/_Project/Art/Shaders/Include/SeaReach.hlsl");
            StringAssert.Contains("return u * u * (3.0 - 2.0 * u);", hlsl, "the smoothstep twin of Factor01");
            StringAssert.Contains(".Load(", hlsl, "point read, never filtered");
            string water = System.IO.File.ReadAllText("Assets/_Project/Art/Shaders/HiddenHarboursWater.shader");
            StringAssert.Contains("Include/SeaReach.hlsl", water);
            StringAssert.Contains("float seaReach = SeaReach01(worldXY, _WaterLevel);", water);
            StringAssert.Contains("float vSeaReach = SeaReach01(ground, _WaterLevel);", water);
            StringAssert.Contains("caustic = lerp(causticStill, caustic, seaReach);", water, "a pool's seabed light holds still");
            string advect = System.IO.File.ReadAllText("Assets/_Project/Art/Shaders/HiddenHarboursFoamBufferAdvect.shader");
            StringAssert.Contains("front *= SeaReach01(worldXY, _WaterLevel);", advect);
#endif
        }
    }
}
