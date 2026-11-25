using System;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The shore-wave field the water shader turns its wave trains along: built once from a region's
    /// seabed, deterministic, facing the shore on a plain slope, and finite everywhere (flat, dry, wet).
    /// </summary>
    public class ShoreWaveFieldTests
    {
        const int W = 64, H = 48;
        const float Cell = 2f, MaxDistance = 96f;

        // A plane beach climbing toward +x: the shoreline (elevation 0) sits at x = 80 m.
        static float[] Slope(float riseX = 0.05f, float riseY = 0f, float shoreX = 80f)
        {
            var e = new float[W * H];
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float wx = (x + 0.5f) * Cell, wy = (y + 0.5f) * Cell;
                e[y * W + x] = riseX * (wx - shoreX) + riseY * (wy - H * Cell * 0.5f);
            }
            return e;
        }

        static float[] Build(float[] e, int smoothing = 3) =>
            ShoreWaveField.Build(e, W, H, Cell, Cell, 0f, MaxDistance, smoothing);

        [Test]
        public void ShoreWaveField_SameSeabed_BuildsBitIdentical()
        {
            float[] a = Build(Slope(0.05f, 0.02f));
            float[] b = Build(Slope(0.05f, 0.02f));
            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++)
                Assert.AreEqual(BitConverter.SingleToInt32Bits(a[i]), BitConverter.SingleToInt32Bits(b[i]), "cell value " + i);
        }

        [Test]
        public void ShoreWaveField_PlainSlope_TurnsWavesToFaceTheShore()
        {
            float[] f = Build(Slope());
            // Offshore of the line (x < 80 m), inside the clamp: the turned wave travels straight up the slope.
            for (int y = 4; y < H - 4; y += 6)
            for (int x = 4; x < 36; x += 4)
            {
                Vector2 shoreward = ShoreWaveField.ShorewardAt(f, W, x, y);
                Assert.That(Vector2.Dot(shoreward, Vector2.right), Is.GreaterThan(0.99f), $"cell {x},{y} faces {shoreward}");
                float expected = 80f - (x + 0.5f) * Cell;
                Assert.That(f[(y * W + x) * 4], Is.EqualTo(expected).Within(Cell), $"metres offshore at cell {x},{y}");
            }
        }

        [Test]
        public void ShoreWaveField_ObliqueSlope_FacesDownTheSeabedGradient()
        {
            // The shoreline runs at an angle: the turn must follow the beach, not the grid axes.
            float[] f = Build(Slope(0.05f, 0.05f, 60f));
            Vector2 upSlope = new Vector2(1f, 1f).normalized;
            int checkedCells = 0;
            for (int y = 8; y < H - 8; y += 3)
            for (int x = 6; x < W - 6; x += 3)
            {
                float metres = f[(y * W + x) * 4];
                if (metres < 6f || metres > 40f) continue;      // in the band, off the grid edges' influence
                Vector2 shoreward = ShoreWaveField.ShorewardAt(f, W, x, y);
                Assert.That(Vector2.Dot(shoreward, upSlope), Is.GreaterThan(0.97f), $"cell {x},{y} faces {shoreward}");
                checkedCells++;
            }
            Assert.That(checkedCells, Is.GreaterThan(10));
        }

        [Test]
        public void ShoreWaveField_SignedAcrossTheLine_NegativeInland()
        {
            float[] f = Build(Slope(), 0);
            int y = H / 2;
            float inland = f[(y * W + 50) * 4];   // x = 101 m, 21 m up the beach
            float seaward = f[(y * W + 20) * 4];  // x = 41 m, 39 m out
            Assert.That(inland, Is.LessThan(0f));
            Assert.That(seaward, Is.GreaterThan(0f));
            // The phase runs on over the flooded flats: the gradient keeps pointing offshore inland too.
            Assert.That(Vector2.Dot(ShoreWaveField.ShorewardAt(f, W, 50, y), Vector2.right), Is.GreaterThan(0.99f));
        }

        [Test]
        public void ShoreWaveField_NoLand_ReadsOpenSeaEverywhereWithoutNaN()
        {
            var deep = new float[W * H];
            for (int i = 0; i < deep.Length; i++) deep[i] = -12f;
            float[] f = Build(deep);
            for (int i = 0; i < W * H; i++)
            {
                AssertFinite(f, i);
                Assert.AreEqual(MaxDistance, f[i * 4], "open sea reads the clamp");
                Assert.AreEqual(0f, f[i * 4 + 1]);
                Assert.AreEqual(0f, f[i * 4 + 2]);
            }
            Assert.AreEqual(Vector2.zero, ShoreWaveField.ShorewardAt(f, W, W / 2, H / 2), "no shore, no turn");
        }

        [Test]
        public void ShoreWaveField_AllDry_ReadsInlandEverywhereWithoutNaN()
        {
            var dry = new float[W * H];
            for (int i = 0; i < dry.Length; i++) dry[i] = 3f;
            float[] f = Build(dry);
            for (int i = 0; i < W * H; i++)
            {
                AssertFinite(f, i);
                Assert.AreEqual(-MaxDistance, f[i * 4]);
                Assert.AreEqual(0f, f[i * 4 + 1]);
                Assert.AreEqual(0f, f[i * 4 + 2]);
            }
        }

        [Test]
        public void ShoreWaveField_RaggedCoastWithNaNSeabed_StaysFiniteAndUnitBounded()
        {
            var e = Slope(0.05f, 0.03f);
            var rng = new System.Random(7);   // a fixed seed: the same ragged coast every run
            for (int i = 0; i < e.Length; i++) e[i] += (float)(rng.NextDouble() - 0.5) * 4f;
            e[5] = float.NaN;                 // a bad cell reads as land, never poisons the field
            e[W * H / 2] = float.PositiveInfinity;
            foreach (int smoothing in new[] { 0, 3 })
            {
                float[] f = Build(e, smoothing);
                for (int i = 0; i < W * H; i++)
                {
                    AssertFinite(f, i);
                    float g = new Vector2(f[i * 4 + 1], f[i * 4 + 2]).magnitude;
                    Assert.That(g, Is.LessThanOrEqualTo(1.0001f), "gradient length at " + i);
                    Assert.That(Mathf.Abs(f[i * 4]), Is.LessThanOrEqualTo(MaxDistance));
                }
            }
        }

        [Test]
        public void ShoreWaveField_NonSquareCells_MeasureWorldMetres()
        {
            // 2 m by 4 m cells: the distance up a +y slope must count 4 m per row, not 1 per cell.
            var e = new float[W * H];
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                e[y * W + x] = 0.05f * ((y + 0.5f) * 4f - 120f);
            float[] f = ShoreWaveField.Build(e, W, H, 2f, 4f, 0f, MaxDistance, 0);
            float at = f[(10 * W + W / 2) * 4];     // y = 42 m, 78 m short of the line
            Assert.That(at, Is.EqualTo(78f).Within(4f));
            Assert.That(Vector2.Dot(ShoreWaveField.ShorewardAt(f, W, W / 2, 10), Vector2.up), Is.GreaterThan(0.99f));
        }

        static void AssertFinite(float[] f, int cell)
        {
            for (int c = 0; c < 4; c++)
            {
                float v = f[cell * 4 + c];
                Assert.That(float.IsNaN(v) || float.IsInfinity(v), Is.False, $"cell {cell} channel {c} = {v}");
            }
        }
    }
}
