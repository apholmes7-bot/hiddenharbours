using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE NEUTRALITY GUARD</b> for terrain pass 9 PR 4 (ADR 0046: still water above the tide, and one
    /// height precision, R16). Subjects: <see cref="PaintedHeightMap"/>'s decode, and
    /// <see cref="PaintedTidalTerrain"/>, the terrain every on-foot water read is taken over.
    ///
    /// <para>PR 4 changes HOW a painted height map is read (<see cref="PaintedHeightMap.ReadNormalizedR"/> now
    /// reads an R16 texture's codes through <c>GetPixelData</c>) and adds a still water that composes with the
    /// tide as <c>max(tide, still)</c>. Neither may move a committed region by a millimetre. For each committed
    /// map, two clauses:</para>
    /// <list type="number">
    /// <item><b>Every texel decodes to the metres the BASE code decoded it to.</b></item>
    /// <item><b>With no still map bound, the water level is the tide</b>, bit for bit, at every texel centre
    /// and every tide, and the on-foot depth there is the tide over the ground, bit for bit.</item>
    /// </list>
    ///
    /// <para><b>The bar is not asked of the code under test.</b> Clause 1's expected metres are the base
    /// code's decode (<c>c25d952d</c>: <c>Mathf.Lerp(min, max, Clamp01(k / 255f))</c> over
    /// <c>GetPixels()</c>), computed OUTSIDE Unity in IEEE single precision for every one of the 256 codes and
    /// stored here as one table per map:</para>
    /// <code>table[k] = f32(min) + (f32(max) - f32(min)) * (f32(k) / f32(255))</code>
    /// <para>(numpy: <c>a, b = float32(min), float32(max); a + (b - a) * (arange(256, dtype=float32) /
    /// float32(255))</c>). The table was cross-checked against the scene exporter's own decode of the same PNGs
    /// (<c>tools/scene-export/hhexport/heightmap.py</c>: <c>decode_r8</c>, then
    /// <c>round(low + k/255·span, 3)</c>): they agree to 0.49 mm, the exporter's millimetre rounding. Which
    /// code each texel holds is pinned by a SHA-256 of the file's codes in Unity's <c>GetPixels32</c> order
    /// (bottom row first), taken from the PNG by an independent decoder (PIL, which agreed with the exporter's
    /// <c>decode_r8</c> on every code). So the texture Unity loads is proven to be the file the table was built
    /// for before a single metre is compared.</para>
    ///
    /// <para><b>The tolerance, 1e-5 m, and what it catches.</b> The base's decode and the table differ only
    /// by float rounding: <c>k × (1/255f)</c> against <c>k / 255f</c> moves a decode by at most 9.5e-7 m, and a
    /// JIT that holds a float intermediate at double precision moves it by less. A real change is thousands
    /// of times the bar: reading an 8-bit map as <c>k / 256</c> moves a texel by up to 0.039 m (St Peters) or
    /// 0.047 m (Nine Mile Creek); one code off moves it 0.039 or 0.047 m; a range change, up to 1 m.</para>
    ///
    /// <para><b>This guard passes on the base code, by design.</b> It proves that PR 4 left the base where it
    /// was; it is not a test of a new behaviour, so it has no base failure to show. Its negative controls are
    /// the arithmetic above.</para>
    ///
    /// <para><b>When a committed map changes on purpose</b> (PR 5 commits a new St Peters map), this guard
    /// fails BY NAME on the SHA and says so. Re-fixture that map in the PR that changes it, from that PR's
    /// base: the same formula over the map's range, and the new codes' SHA. Never widen the bar to pass.</para>
    ///
    /// <para><b>St Peters since terrain PR 5 B</b> is an R16 map over −4 … +12 m (its ground file's import), so
    /// clause 1 reads its 16-bit codes through <c>GetPixelData</c>, pins them by the SHA-256 of their
    /// little-endian bytes, bottom row first (taken from the written PNG by an independent decoder, and the
    /// manifest's height values), and asks each texel for the R16 rule <c>min + (max − min) × k / 65535</c>,
    /// computed here in double: the file's definition, not the code under test. One code is 0.24 mm, 24 times
    /// the bar; the float decode is within 2e-6 m. The map binds a still water now (PR 5's), so clause 2 is
    /// asked of its heights on a copy with the still map taken off.</para>
    /// </summary>
    public class PaintedHeightMapNeutralityTests
    {
        // The worst float rounding between the base's decode and the table is 9.5e-7 m; see the class doc.
        private const float Tolerance = 1e-5f;

        // Spring low and high (±2.2 m), datum, the bar crest (+0.88 m), and two levels no tide reaches.
        private static readonly float[] Tides = { -2.2f, 0f, 0.88f, 2.2f, -1000f, 1000f };

        private GameObject _go;
        private PaintedHeightMap _copy;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _go = null;
            if (_copy != null) Object.DestroyImmediate(_copy);
            _copy = null;
            GameServices.Reset();
        }

        // ---------------------------------------------------------------------------------------------------
        // Clause 1: every texel decodes to the metres the base decoded it to.
        // ---------------------------------------------------------------------------------------------------

        [TestCase("StPeters")]
        [TestCase("NineMileCreek")]
        public void EveryTexel_DecodesToTheMetresTheBaseDecodedItTo(string key)
        {
            CommittedMap m = Fixture(key);
            PaintedHeightMap map = LoadMap(m);
            Texture2D tex = map.HeightTexture;

            // Which code each texel holds, as Unity loaded it: proven to be the committed file's codes.
            int[] codes = LoadedCodes(key, m, tex, out byte[] bytes);
            string sha = Sha256Hex(bytes);
            Assert.AreEqual(m.CodesSha256, sha,
                $"{key}: the codes Unity loads from '{m.PngPath}' (format {tex.format}) are not the codes this " +
                "guard's table was built for. Either the committed map changed, or Unity now loads it " +
                "differently. A map changed on purpose is re-fixtured in the PR that changes it (see the class " +
                "doc); a map that changed by accident is the defect. SHA-256 of the loaded codes, bottom row " +
                $"first: {sha}.");

            map.Rebuild();
            PaintedHeightField field = map.Field;
            Assert.IsNotNull(field, $"{key}: the map decoded no field (is the texture still readable?).");
            Assert.AreEqual(m.Width, field.Width, $"{key}: the decoded field's width.");
            Assert.AreEqual(m.Height, field.Height, $"{key}: the decoded field's height.");

            int bad = 0;
            double worst = 0;
            int worstX = 0, worstY = 0, firstBadX = -1, firstBadY = -1;
            for (int y = 0; y < m.Height; y++)
            {
                for (int x = 0; x < m.Width; x++)
                {
                    double want = m.Decode(codes[y * m.Width + x]);
                    double err = System.Math.Abs(field.ElevationAtTexel(x, y) - want);
                    if (!(err <= Tolerance))            // a NaN counts against it
                    {
                        if (bad == 0) { firstBadX = x; firstBadY = y; }
                        bad++;
                    }
                    if (double.IsNaN(err) || err > worst) { worst = err; worstX = x; worstY = y; }
                }
            }

            string report =
                $"{key}: {m.Width} × {m.Height} texels ({tex.format}, range {m.Min} … {m.Max} m). Worst " +
                $"|decode − base| = {worst:0.000e+00} m at texel ({worstX}, {worstY}); bar {Tolerance:0e+00} m.";
            Assert.AreEqual(0, bad,
                $"{bad} texel(s) no longer decode to the metres the base decoded them to — the first at " +
                $"({firstBadX}, {firstBadY}), code {(firstBadX < 0 ? -1 : codes[firstBadY * m.Width + firstBadX])}, " +
                $"base {(firstBadX < 0 ? double.NaN : m.Decode(codes[firstBadY * m.Width + firstBadX]))} m, " +
                $"now {(firstBadX < 0 ? float.NaN : field.ElevationAtTexel(firstBadX, firstBadY))} m. PR 4 must " +
                "not move a committed region (charter §0.2). " + report);
            Debug.Log("[PaintedHeightMapNeutrality] " + report);
        }

        // ---------------------------------------------------------------------------------------------------
        // Clause 2: with no still map bound, the water level is the tide everywhere.
        // ---------------------------------------------------------------------------------------------------

        [TestCase("StPeters")]
        [TestCase("NineMileCreek")]
        public void WithNoStillMapBound_TheWaterLevelIsTheTide_Everywhere(string key)
        {
            CommittedMap m = Fixture(key);
            PaintedHeightMap map = LoadMap(m);

            // The premise, read off the committed asset: it binds no still map, so its decode builds none. A map
            // that binds one since PR 5 (St Peters') is asked of its heights on a copy with the still map taken off.
            if (m.StillBound)
            {
                Assert.IsNotNull(map.StillLevelTexture, $"{key}: the committed map no longer binds its still-level texture.");
                map = WithoutItsStillMap(map);
            }
            Assert.IsNull(map.StillLevelTexture,
                $"{key}: the committed map now binds a still-level texture. PR 4 adds no water anywhere " +
                "(charter §0.2); a still map arrives with PR 5.");
            map.Rebuild();
            Assert.IsNull(map.StillWater, $"{key}: a map with no still texture decoded a still water.");

            // What a region with no still map registers: nothing, which the seam answers as the empty sea.
            GameServices.Reset();
            IStillWater seam = GameServices.StillWater;
            Assert.AreSame(EmptyStillWater.Instance, seam,
                "GameServices.StillWater with nothing registered must be the empty still water.");

            _go = new GameObject("NeutralityTerrain");
            var terrain = _go.AddComponent<PaintedTidalTerrain>();
            terrain.Map = map;

            var env = new FlatEnv();
            IStillWater[] sources = { map.StillWater, seam };
            string[] sourceNames = { "the map's own still water (none)", "GameServices.StillWater (empty)" };

            PaintedHeightField field = map.Field;
            Assert.IsNotNull(field, $"{key}: the map decoded no field.");

            long checks = 0, bad = 0;
            string first = null;
            for (int y = 0; y < field.Height; y++)
            {
                for (int x = 0; x < field.Width; x++)
                    Check(field.TexelToWorld(x, y));
            }

            // Off the map, where the height clamps to its edge and a pond must not reach.
            Vector2 half = m.WorldSize * 0.5f;
            Check(m.WorldCenter + new Vector2(-half.x - 50f, 0f));
            Check(m.WorldCenter + new Vector2(half.x + 50f, half.y + 50f));
            Check(m.WorldCenter + new Vector2(0f, -half.y - 1000f));

            Assert.Greater(checks, (long)m.Width * m.Height, "the sweep did not run over the map.");
            Assert.AreEqual(0L, bad,
                $"{key}: with no still map bound, {bad} of {checks} reads were not the tide. First: {first}");
            Debug.Log($"[PaintedHeightMapNeutrality] {key}: {checks} reads over {m.Width} × {m.Height} texel " +
                      $"centres and 3 off-map points, {Tides.Length} tides × {sources.Length} still sources: " +
                      "the water level is the tide and the depth is the tide over the ground, bit for bit.");

            void Check(Vector2 p)
            {
                float ground = terrain.ElevationAt(p);
                for (int t = 0; t < Tides.Length; t++)
                {
                    float tide = Tides[t];
                    env.Level = tide;
                    float wantDepth = tide - ground;
                    for (int s = 0; s < sources.Length; s++)
                    {
                        float water = StillWaterLevels.WaterLevelAt(env, sources[s], 0.0, p);
                        float depth = TidalWalkability.DepthAt(terrain, env, sources[s], null, 0.0, p);
                        checks++;
                        if (water == tide && depth == wantDepth) continue;
                        bad++;
                        if (first == null)
                            first = $"at ({p.x}, {p.y}), tide {tide} m, {sourceNames[s]}: water {water} m " +
                                    $"(want {tide}), depth {depth} m (want {wantDepth}).";
                    }
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------

        private static PaintedHeightMap LoadMap(CommittedMap m)
        {
            var map = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(m.AssetPath);
            Assert.IsNotNull(map, $"The committed map '{m.AssetPath}' did not load.");
            Assert.IsNotNull(map.HeightTexture, $"'{m.AssetPath}' binds no height texture.");
            Assert.AreEqual(m.PngPath, AssetDatabase.GetAssetPath(map.HeightTexture),
                $"'{m.AssetPath}' binds a different height texture from the one this guard was built for.");
            Assert.AreEqual(m.Min, map.MinElevation, $"'{m.AssetPath}': the range's minimum moved.");
            Assert.AreEqual(m.Max, map.MaxElevation, $"'{m.AssetPath}': the range's maximum moved.");
            Assert.AreEqual(m.WorldCenter, map.WorldCenter, $"'{m.AssetPath}': the world centre moved.");
            Assert.AreEqual(m.WorldSize, map.WorldSize, $"'{m.AssetPath}': the world size moved.");
            Assert.AreEqual(m.Width, map.HeightTexture.width, $"'{m.PngPath}': the width.");
            Assert.AreEqual(m.Height, map.HeightTexture.height, $"'{m.PngPath}': the height.");
            Assert.IsTrue(map.HeightTexture.isReadable, $"'{m.PngPath}' is not CPU-readable.");
            return map;
        }

        /// <summary>
        /// The code each texel holds as Unity loaded it, bottom row first, and the bytes its SHA is taken over: an 8-bit
        /// map's R bytes through <c>GetPixels32</c>, an R16 map's codes through <c>GetPixelData</c> as little-endian pairs.
        /// </summary>
        private static int[] LoadedCodes(string key, CommittedMap m, Texture2D tex, out byte[] bytes)
        {
            if (m.Bits == 16)
            {
                Assert.AreEqual(TextureFormat.R16, tex.format, $"{key}: '{m.PngPath}' does not load as R16.");
                var data = tex.GetPixelData<ushort>(0);
                Assert.AreEqual(m.Width * m.Height, data.Length, $"{key}: GetPixelData returned the wrong texel count.");
                var codes16 = new int[data.Length];
                bytes = new byte[data.Length * 2];
                for (int i = 0; i < data.Length; i++)
                {
                    ushort k = data[i];
                    codes16[i] = k;
                    bytes[2 * i] = (byte)k;
                    bytes[2 * i + 1] = (byte)(k >> 8);
                }
                return codes16;
            }
            Color32[] px = tex.GetPixels32();
            Assert.AreEqual(m.Width * m.Height, px.Length, $"{key}: GetPixels32 returned the wrong texel count.");
            var codes = new int[px.Length];
            bytes = new byte[px.Length];
            for (int i = 0; i < px.Length; i++) { bytes[i] = px[i].r; codes[i] = px[i].r; }
            return codes;
        }

        /// <summary>A copy of the committed map with its still-level texture taken off (the asset is not touched).</summary>
        private PaintedHeightMap WithoutItsStillMap(PaintedHeightMap map)
        {
            _copy = Object.Instantiate(map);
            var so = new SerializedObject(_copy);
            SerializedProperty still = so.FindProperty("_stillLevelTexture");
            Assert.IsNotNull(still, "PaintedHeightMap has no _stillLevelTexture to take off.");
            still.objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            return _copy;
        }

        private static string Sha256Hex(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private sealed class FlatEnv : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        // ---------------------------------------------------------------------------------------------------
        // The fixture: taken from the base (c25d952d) outside Unity, never computed by the code under test.
        // ---------------------------------------------------------------------------------------------------

        private sealed class CommittedMap
        {
            public string AssetPath, PngPath;
            public int Width, Height;
            public float Min, Max;
            public Vector2 WorldCenter, WorldSize;
            public int Bits = 8;          // 8: the R byte through GetPixels32; 16: R16 codes through GetPixelData
            public bool StillBound;       // the committed asset binds a still-level texture (clause 2 takes it off a copy)
            public string CodesSha256;    // the file's codes, bottom row first (GetPixels32's order; R16 little-endian)
            public float[] BaseDecode;    // 8-bit: metres for code k, as the base decoded it (the class doc's formula)

            /// <summary>The metres code <paramref name="k"/> stands for: the table, or the R16 rule in double.</summary>
            public double Decode(int k) => Bits == 16 ? Min + ((double)Max - Min) * k / 65535.0 : BaseDecode[k];
        }

        private static CommittedMap Fixture(string key)
        {
            switch (key)
            {
                case "StPeters":
                    return new CommittedMap
                    {
                        AssetPath = "Assets/_Project/Data/Terrain/StPetersSeabed.asset",
                        PngPath = "Assets/_Project/Data/Terrain/StPetersSeabed_HeightTex.png",
                        Width = 1520, Height = 1040, Min = -4f, Max = 12f,
                        WorldCenter = new Vector2(0f, 0f), WorldSize = new Vector2(760f, 520f),
                        // Terrain PR 5 B: ground.stp_island's import (StPetersPlan.manifest.json's height values).
                        Bits = 16, StillBound = true,
                        // island-ground@3: independent PNG decode, little-endian R16, bottom row first.
                        // Outside the issue boxes every code still equals main; the 1e-5 m decode bar stands.
                        CodesSha256 = "d2a2bab9c2963abfe671cc0af2b561b7f164430f46414c6f753879216681c41a",
                    };
                case "NineMileCreek":
                    return new CommittedMap
                    {
                        AssetPath = "Assets/_Project/Data/Terrain/NineMileCreekSeabed.asset",
                        PngPath = "Assets/_Project/Data/Terrain/NineMileCreekSeabed_HeightTex.png",
                        Width = 1520, Height = 1120, Min = -6f, Max = 6f,
                        WorldCenter = new Vector2(0f, 0f), WorldSize = new Vector2(760f, 560f),
                        // The PNG file itself: bd16d00743182dbc3706db010560ac0c2d77df538c218505ada92f9dfb8930da
                        CodesSha256 = "8875aa942f2b3806b34bfa8ff5120deb85456c7bf6e2d24e8a4279515e08f8d1",
                        BaseDecode = NineMileCreekBaseDecode,
                    };
                default:
                    Assert.Fail("No fixture for " + key);
                    return null;
            }
        }

        // Nine Mile Creek, -6 … +6 m: the base's decode of code k (0 … 255), eight to a row.
        private static readonly float[] NineMileCreekBaseDecode =
        {
            -6.0f, -5.952941f, -5.9058824f, -5.8588233f, -5.8117647f, -5.7647057f, -5.717647f, -5.670588f,
            -5.6235294f, -5.5764704f, -5.529412f, -5.4823527f, -5.435294f, -5.388235f, -5.3411765f, -5.2941175f,
            -5.247059f, -5.2f, -5.152941f, -5.105882f, -5.0588236f, -5.0117645f, -4.964706f, -4.917647f,
            -4.8705883f, -4.8235292f, -4.7764707f, -4.7294116f, -4.682353f, -4.635294f, -4.5882354f, -4.5411763f,
            -4.4941177f, -4.4470587f, -4.3999996f, -4.352941f, -4.3058825f, -4.2588234f, -4.2117643f, -4.1647058f,
            -4.117647f, -4.070588f, -4.023529f, -3.9764705f, -3.9294116f, -3.8823528f, -3.835294f, -3.7882352f,
            -3.7411764f, -3.6941175f, -3.6470587f, -3.6f, -3.552941f, -3.5058823f, -3.4588234f, -3.4117646f,
            -3.3647058f, -3.317647f, -3.2705882f, -3.2235293f, -3.1764705f, -3.1294117f, -3.0823529f, -3.035294f,
            -2.988235f, -2.9411764f, -2.8941174f, -2.8470588f, -2.7999997f, -2.7529411f, -2.705882f, -2.6588235f,
            -2.6117644f, -2.5647058f, -2.5176468f, -2.4705882f, -2.4235291f, -2.3764706f, -2.3294115f, -2.282353f,
            -2.2352939f, -2.1882353f, -2.1411762f, -2.0941176f, -2.0470586f, -2.0f, -1.952941f, -1.9058824f,
            -1.8588233f, -1.8117647f, -1.7647057f, -1.7176471f, -1.670588f, -1.6235294f, -1.5764704f, -1.5294118f,
            -1.4823527f, -1.4352942f, -1.3882351f, -1.3411765f, -1.2941175f, -1.2470589f, -1.1999998f, -1.1529412f,
            -1.1058822f, -1.0588236f, -1.0117645f, -0.96470594f, -0.9176469f, -0.8705883f, -0.82352924f, -0.77647066f,
            -0.7294116f, -0.682353f, -0.63529396f, -0.5882354f, -0.5411763f, -0.49411774f, -0.44705868f, -0.4000001f,
            -0.35294104f, -0.30588245f, -0.2588234f, -0.21176481f, -0.16470575f, -0.11764717f, -0.07058811f, -0.02352953f,
            0.023530006f, 0.07058859f, 0.11764717f, 0.16470623f, 0.21176529f, 0.25882387f, 0.30588245f, 0.3529415f,
            0.40000057f, 0.44705915f, 0.49411774f, 0.5411768f, 0.58823586f, 0.63529444f, 0.682353f, 0.7294121f,
            0.77647114f, 0.8235297f, 0.8705883f, 0.91764736f, 0.9647064f, 1.011765f, 1.0588236f, 1.1058826f,
            1.1529417f, 1.2000003f, 1.2470589f, 1.2941179f, 1.341177f, 1.3882356f, 1.4352942f, 1.4823532f,
            1.5294123f, 1.5764709f, 1.6235294f, 1.6705885f, 1.7176476f, 1.7647061f, 1.8117647f, 1.8588238f,
            1.9058828f, 1.9529414f, 2.0f, 2.047059f, 2.094118f, 2.1411762f, 2.1882353f, 2.2352943f,
            2.2823534f, 2.3294125f, 2.3764706f, 2.4235296f, 2.4705887f, 2.5176468f, 2.5647058f, 2.611765f,
            2.658824f, 2.705883f, 2.7529411f, 2.8000002f, 2.8470592f, 2.8941174f, 2.9411764f, 2.9882355f,
            3.0352945f, 3.0823536f, 3.1294117f, 3.1764708f, 3.2235298f, 3.270588f, 3.317647f, 3.364706f,
            3.411765f, 3.4588242f, 3.5058823f, 3.5529413f, 3.6000004f, 3.6470585f, 3.6941175f, 3.7411766f,
            3.7882357f, 3.8352947f, 3.8823528f, 3.929412f, 3.976471f, 4.023529f, 4.070588f, 4.117647f,
            4.164706f, 4.2117653f, 4.2588234f, 4.3058825f, 4.3529415f, 4.3999996f, 4.4470587f, 4.4941177f,
            4.541177f, 4.588236f, 4.635294f, 4.682353f, 4.729412f, 4.77647f, 4.8235292f, 4.8705883f,
            4.9176474f, 4.9647064f, 5.0117645f, 5.0588236f, 5.1058826f, 5.1529408f, 5.2f, 5.247059f,
            5.294118f, 5.341177f, 5.388235f, 5.435294f, 5.482353f, 5.5294113f, 5.5764704f, 5.6235294f,
            5.6705885f, 5.7176476f, 5.7647057f, 5.8117647f, 5.858824f, 5.905882f, 5.952941f, 6.0f,
        };
    }
}
