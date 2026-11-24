using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// How still water above the tide (ADR 0046) is registered and drawn on a beach (owner, 11-22: "fix
    /// the creek strips look first"). Two causes, two fixes:
    ///
    /// <para><b>The pale sheet.</b> A brook's cells drain to the sea, so the sea's reach was 1 over it and
    /// its few centimetres sat inside the shore foam band. <see cref="SeaReachMath.UnderStill01"/> takes the
    /// sea's share out wherever still water stands above the tide.</para>
    ///
    /// <para><b>The stepped edge.</b> A brook's level is laid texel by texel as bed plus depth, so it jumps
    /// from row to row and its waterline jogs at each jump. <see cref="PaintedStillWater.SmoothLevels"/>
    /// smooths the surface inside the water only, and <see cref="PaintedStillWater.SealRims"/> lets a rim that
    /// still stood above its ground reach the ground, in a runtime copy both the sim and the render read.</para>
    /// </summary>
    public class StillWaterRimAndReachTests
    {
        const float MapMin = -4f, MapMax = 7f;
        const float Pond = 0.05f;

        static float Code(float level) => StillWaterLevels.EncodeCode(level, MapMin, MapMax) / 65535f;

        static Texture2D R16(float[] codes, int w, int h)
        {
            var raw = new ushort[codes.Length];
            for (int i = 0; i < raw.Length; i++) raw[i] = (ushort)Mathf.RoundToInt(codes[i] * 65535f);
            var t = new Texture2D(w, h, TextureFormat.R16, false, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            t.SetPixelData(raw, 0);
            t.Apply(false, false);
            return t;
        }

        // A brook 3 texels wide (x 2-4) running down 8 rows, its surface stepping down 4 cm a row.
        static float[] SteppedBrook()
        {
            var c = new float[8 * 8];
            for (int y = 0; y < 8; y++) for (int x = 2; x <= 4; x++) c[y * 8 + x] = Code(0.30f - 0.04f * y);
            return c;
        }

        [Test]
        public void AFlatPondStaysExactlyFlat_AndNoWaterIsAdded()
        {
            var codes = new float[64];
            for (int y = 3; y <= 5; y++) for (int x = 2; x <= 5; x++) codes[y * 8 + x] = Code(Pond);
            float[] before = (float[])codes.Clone();
            int changed = PaintedStillWater.SmoothLevels(codes, 8, 8, 2);
            Assert.AreEqual(0, changed, "a flat pond has nothing to smooth");
            for (int i = 0; i < 64; i++) Assert.AreEqual(before[i], codes[i], $"texel {i}");
        }

        [Test]
        public void NoneStaysNone_AndTheRadiusZeroIsOff()
        {
            float[] codes = SteppedBrook();
            float[] before = (float[])codes.Clone();
            Assert.AreEqual(0, PaintedStillWater.SmoothLevels(codes, 8, 8, 0), "radius 0 reads the map as it is");
            PaintedStillWater.SmoothLevels(codes, 8, 8, 2);
            for (int i = 0; i < 64; i++)
            {
                if (before[i] > 0f) Assert.Greater(codes[i], 0f, $"texel {i}: water stays water");
                else Assert.AreEqual(0f, codes[i], $"texel {i}: no water is added, none never votes");
            }
        }

        [Test]
        public void ASteppedBrookBecomesARamp_InsideItsOwnRange()
        {
            float[] codes = SteppedBrook();
            PaintedStillWater.SmoothLevels(codes, 8, 8, 2);
            float Level(int x, int y) => StillWaterLevels.DecodeCode01(codes[y * 8 + x], MapMin, MapMax);
            for (int y = 0; y < 8; y++)
            {
                Assert.AreEqual(Level(2, y), Level(3, y), 1e-5f, $"row {y}: level across the brook");
                Assert.AreEqual(Level(4, y), Level(3, y), 1e-5f, $"row {y}: level across the brook");
                float l = Level(3, y);
                Assert.LessOrEqual(l, 0.30f + 1e-5f, "never above the highest step");
                Assert.GreaterOrEqual(l, 0.30f - 0.04f * 7 - 1e-5f, "never below the lowest step");
                if (y > 0) Assert.Less(l, Level(3, y - 1), $"row {y}: still running downhill");
            }
            // In the middle rows a 5-row window is centred, so the ramp keeps the brook's own 4 cm a row.
            Assert.AreEqual(0.30f - 0.04f * 4, Level(3, 4), 5e-4f, "the mean of a straight run is its middle");
        }

        [Test]
        public void TheSmoothedMapIsWhatIsDrawn_AndSimEqualsRender()
        {
            Texture2D tex = R16(SteppedBrook(), 8, 8);
            var still = new PaintedStillWater(tex, Vector2.zero, new Vector2(8f, 8f), MapMin, MapMax, 2);
            var plain = new PaintedStillWater(tex, Vector2.zero, new Vector2(8f, 8f), MapMin, MapMax);
            try
            {
                Assert.AreSame(tex, plain.Map.Texture, "no smoothing: the asset itself is drawn");
                Assert.AreNotSame(tex, still.Map.Texture, "the asset is never written; a runtime copy is drawn");
                var drawn = (Texture2D)still.Map.Texture;
                Assert.AreEqual(TextureFormat.R16, drawn.format);
                Assert.AreEqual(tex.filterMode, drawn.filterMode);
                Assert.AreEqual(tex.wrapMode, drawn.wrapMode);
                var raw = drawn.GetPixelData<ushort>(0);
                for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    var centre = new Vector2(-4f + x + 0.5f, -4f + y + 0.5f);
                    float expect = StillWaterLevels.DecodeCode01(raw[y * 8 + x] / 65535f, MapMin, MapMax);
                    float got = still.StillLevelAt(centre);
                    if (float.IsNegativeInfinity(expect)) Assert.IsTrue(float.IsNegativeInfinity(got), $"texel {x},{y}");
                    else Assert.AreEqual(expect, got, 1e-5f, $"texel {x},{y}");
                }
            }
            finally { still.Dispose(); Object.DestroyImmediate(tex); }
        }

        [Test]
        public void ARimTakesTheLowerOfTheWaterAndItsOwnGround_AndNeverGainsDepth()
        {
            // 8 x 1 texels: water at x 2-3 at 0.05 m; west of it the ground stands above (a bank), east of
            // it the ground is still below the surface (the water reached the map's texel edge).
            var codes = new float[8];
            codes[2] = codes[3] = Code(Pond);
            float Ground(int x, int y) => x <= 1 ? 0.20f : x == 4 ? -0.10f : x >= 5 ? -0.30f : -0.20f;
            int sealed_ = PaintedStillWater.SealRims(codes, 8, 1, Ground, MapMin, MapMax);
            Assert.AreEqual(2, sealed_, "one texel each side, never further");
            Assert.AreEqual(Code(Pond), codes[1], 1e-6f, "a bank above the water: the usual dry support at the water's level");
            float east = StillWaterLevels.DecodeCode01(codes[4], MapMin, MapMax);
            Assert.LessOrEqual(east, -0.10f + 1e-5f, "ground below the water: its own ground, rounded down, so it gains no depth");
            Assert.AreEqual(-0.10f, east, 0.001f, "and no lower than the ground's own code");
            Assert.AreEqual(0f, codes[0], "two texels out stays none");
            Assert.AreEqual(0f, codes[5], "two texels out stays none");
        }

        [Test]
        public void ASealedRim_PutsTheWaterlineOnTheGround_NotOnTheTexelEdge()
        {
            // 1 m texels over an 8 x 8 m map at the origin. The pond (x 2-4) stands at 0.05 m; the ground under
            // it is -0.10 and rises steadily east: 0.00 at x 5, 0.15 at x 6. Unsealed, the level falls to none
            // across texel 5 while the ground there is still below the water.
            var codes = new float[64];
            var elev = new float[64];
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                if (x >= 2 && x <= 4) codes[y * 8 + x] = Code(Pond);
                elev[y * 8 + x] = x <= 4 ? -0.10f : (x - 5) * 0.15f;
            }
            Texture2D tex = R16(codes, 8, 8);
            var ground = new PaintedHeightField(elev, 8, 8, Vector2.zero, new Vector2(8f, 8f));
            var plain = new PaintedStillWater(tex, Vector2.zero, new Vector2(8f, 8f), MapMin, MapMax);
            var still = new PaintedStillWater(tex, Vector2.zero, new Vector2(8f, 8f), MapMin, MapMax, 0, ground);
            try
            {
                // Texel 4's centre is x 0.5, texel 5's is x 1.5. A third of the way across, the ground is still under the pond.
                var p = new Vector2(0.85f, 0f);
                Assert.Less(plain.StillLevelAt(p), ground.ElevationAt(p), "premise: unsealed, the water is gone before the ground rises through it");
                Assert.Greater(still.StillLevelAt(p), ground.ElevationAt(p), "sealed, the water reaches on toward the bank");
                var q = new Vector2(1.5f, 0f);
                Assert.LessOrEqual(still.StillLevelAt(q), ground.ElevationAt(q) + 1e-5f, "and is dry at the rim texel's own centre");
                Assert.AreNotSame(tex, still.Map.Texture, "the sealed map is a runtime copy");
            }
            finally { still.Dispose(); Object.DestroyImmediate(tex); }
        }

        [Test]
        public void AMapWithNothingToSmooth_IsDrawnFromTheAssetItself()
        {
            Texture2D tex = R16(new float[64], 8, 8);
            var still = new PaintedStillWater(tex, Vector2.zero, new Vector2(8f, 8f), MapMin, MapMax, 2);
            try { Assert.AreSame(tex, still.Map.Texture, "no pond, no copy"); }
            finally { still.Dispose(); Object.DestroyImmediate(tex); }
        }

        [Test]
        public void TheSeasShare_FadesOutOverStillWaterAboveTheTide()
        {
            const float band = 0.05f;
            Assert.AreEqual(1f, SeaReachMath.UnderStill01(0f, StillWaterLevels.None, band), "no still water: the sea's");
            Assert.AreEqual(1f, SeaReachMath.UnderStill01(0f, float.NaN, band), "no information: the older look");
            Assert.AreEqual(1f, SeaReachMath.UnderStill01(0.2f, 0.1f, band), "the tide on top: the sea's");
            Assert.AreEqual(1f, SeaReachMath.UnderStill01(0.1f, 0.1f, band), "level with it: the sea's");
            Assert.AreEqual(0.5f, SeaReachMath.UnderStill01(0f, 0.025f, band), 1e-6f, "halfway through the band");
            Assert.AreEqual(0f, SeaReachMath.UnderStill01(0f, 0.05f, band), "one band above the tide: still water");
            Assert.AreEqual(0f, SeaReachMath.UnderStill01(-2f, 0.05f, band), "low tide: still water");
            float prev = 1f;
            for (float tide = 0.2f; tide >= -0.2f; tide -= 0.001f)
            {
                float v = SeaReachMath.UnderStill01(tide, 0f, band);
                Assert.LessOrEqual(Mathf.Abs(v - prev), 0.001f / band + 1e-4f, "continuous in the tide: no pop");
                prev = v;
            }
        }

        sealed class FlatStill : IStillWater
        {
            public float Level;
            public float StillLevelAt(Vector2 worldPos) => worldPos.x > 10f ? Level : StillWaterLevels.None;
            public StillWaterMap Map => default;
        }

        [Test]
        public void TheCoreSeamTakesTheStillWatersShareOut_AndIsUnchangedWithoutIt()
        {
            // 20 x 4 open beach of 1 m cells, all below the seed height, so every cell is always open.
            var e = new float[80];
            for (int i = 0; i < e.Length; i++) e[i] = -3f;
            var settings = new SeaReachSettings
            {
                Enabled = 1f, ReconnectBlendMeters = 0.25f, SeedMaxElevationMeters = -2.5f,
                MinPoolDepthMeters = 0.05f, QuantumMeters = 0.001f, WetBandMeters = 0.05f,
            };
            var field = SeaReachField.Build(e, 20, 4, Vector2.zero, new Vector2(20f, 4f), settings);
            Assert.NotNull(field);
            object owner = new object();
            var still = new FlatStill { Level = 0.05f };
            try
            {
                SeaReach.Publish(owner, field, 0.25f);
                Assert.AreEqual(1f, SeaReach.FactorAt(new Vector2(15f, 2f), -1f), "no still water registered: the field's 1");
                GameServices.StillWater = still;
                Assert.AreEqual(1f, SeaReach.FactorAt(new Vector2(5f, 2f), -1f), "where there is none: unchanged");
                Assert.AreEqual(0f, SeaReach.FactorAt(new Vector2(15f, 2f), -1f), "a brook above the low tide is still water");
                Assert.AreEqual(1f, SeaReach.FactorAt(new Vector2(15f, 2f), 0.5f), "the high tide drowns it: the sea's again");
                Assert.AreEqual(0.5f, SeaReach.FactorAt(new Vector2(15f, 2f), 0.025f), 1e-5f, "halfway through the wet band");
            }
            finally
            {
                if (ReferenceEquals(GameServices.StillWater, still)) GameServices.StillWater = null;
                SeaReach.Clear(owner);
            }
        }

        [Test]
        public void TheShaderTwinTakesTheSameShareOut_InTheSharedInclude()
        {
            string hlsl = File.ReadAllText("Assets/_Project/Art/Shaders/Include/SeaReach.hlsl").Replace("\r\n", "\n");
            StringAssert.Contains("#include \"Assets/_Project/Art/Shaders/Include/StillWater.hlsl\"", hlsl,
                "the reach reads the still water itself, so the water and the foam deposit agree");
            StringAssert.Contains("return 1.0 - saturate((still - level) / max(_HHSeaReachWet.x, 1e-4));", hlsl,
                "the linear twin of SeaReachMath.UnderStill01 over the published wet band");
            StringAssert.Contains("return SeaReachOfSills01(worldXY, level) * SeaReachUnderStill01(worldXY, level);", hlsl,
                "every SeaReach01 reader gets the still water's share taken out");
        }

        [Test]
        public void StPetersBrooks_AreSmoothedAndSealed_AndNoTexelGainsDepth()
        {
            var map = StPetersTerrainPlan.LoadSeabed();
            Texture2D tex = map.StillLevelTexture;
            Assert.NotNull(tex, "the committed still map");
            float[] codes = PaintedHeightMap.ReadNormalizedR(tex);
            float[] before = (float[])codes.Clone();
            int w = tex.width, h = tex.height;
            int changed = PaintedStillWater.SmoothLevels(codes, w, h, 2);
            Assert.Greater(changed, 0, "the brooks' stepped surfaces are smoothed");
            float maxBefore = 0f;
            foreach (float c in before) if (c > maxBefore) maxBefore = c;
            for (int i = 0; i < codes.Length; i++)
            {
                if (before[i] > 0f) Assert.Greater(codes[i], 0f, $"texel {i}: water stays water");
                else Assert.AreEqual(0f, codes[i], $"texel {i}: smoothing adds no water");
                Assert.LessOrEqual(codes[i], maxBefore, $"texel {i}: never above the highest water on the map");
            }

            PaintedHeightField ground = map.Field;
            Vector2 min = map.WorldCenter - map.WorldSize * 0.5f, cell = new Vector2(map.WorldSize.x / w, map.WorldSize.y / h);
            float GroundAt(int x, int y) => ground.ElevationAt(new Vector2(min.x + (x + 0.5f) * cell.x, min.y + (y + 0.5f) * cell.y));
            float[] smoothed = (float[])codes.Clone();
            int sealedCount = PaintedStillWater.SealRims(codes, w, h, GroundAt, map.MinElevation, map.MaxElevation);
            Assert.Greater(sealedCount, 0, "some brook rims stood above their ground at the texel edge");
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (smoothed[i] > 0f) { Assert.AreEqual(smoothed[i], codes[i], "a coded texel is not touched by the seal"); continue; }
                if (!(codes[i] > 0f)) continue;
                float level = StillWaterLevels.DecodeCode01(codes[i], map.MinElevation, map.MaxElevation);
                Assert.LessOrEqual(level, GroundAt(x, y) + 1e-4f, $"texel {x},{y}: a sealed rim gains no depth at its centre");
            }
        }
    }
}