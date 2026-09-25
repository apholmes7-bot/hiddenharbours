using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>TERRAIN PASS 9, PR 2: the C# twin of TerrainLight6 lights the ground as the kit does.</b>
    ///
    /// <para><see cref="TerrainLight6"/> is the ground's relight in C#, line for line with the kit's
    /// <c>terrainLight6.js</c>. The splat shader's port of the same light cannot run on a CI runner with
    /// no GPU, and the twin can, so the light is proven here. Every expected byte is the kit's:
    /// <c>docs/art/rigs/terrain/pass9/twinFixture.js</c> ran <c>TerrainLight6.relight</c> on Node over
    /// G-buffers that the kit's own <c>gbuf</c> made from PxKit8 tiles, and wrote what it returned into
    /// <see cref="FixturePath"/>. Nothing here asks the twin for an expectation.</para>
    ///
    /// <para>Only the texels the fixture marks stable are compared. A stable texel kept its bytes while
    /// the kit re-ran the case with each sky input moved by 1e-7. A texel that moves under that sits on
    /// a threshold, where one last bit between V8's <c>pow</c> or <c>sin</c> and .NET's could flip it,
    /// and so could JsonUtility's reading of a decimal. In the fixture as committed, 39,388 of the
    /// 39,392 texels are stable.</para>
    /// </summary>
    public class TerrainLight6TwinTests
    {
        const string FixturePath = "docs/art/rigs/terrain/pass9/fixtures/terrainLight6.twin.json";
        const string RigPath = "docs/art/rigs/terrain/pass9/terrainLight6.js";

        /// <summary>The inputs the fixture's coverage counts switch off, each measured on the kit.</summary>
        static readonly string[] Inputs = { "tips", "marks", "puddles", "water", "snow", "rain", "fog", "tide", "occ", "lv" };

        [Serializable] public sealed class GBufferJson
        {
            public string name; public int n, m; public bool wrap;
            public string hmax, H, nx, ny, nz, ao, proud, hollow, pond, cls, band0, pal, mark, alpha, elev, slope, fetch, far;
            public string[] pals;
        }
        [Serializable] public sealed class SkyJson
        {
            public string name; public double[] sunF; public double sunI, expo, snow, wet, rain, fog, aa, amb, ka, wa, skyI, wind;
            public bool grade, hasSkyI, hasWind; public string kc, ac, wash, fogC, skyC;
        }
        [Serializable] public sealed class OptionsJson
        {
            public int x0, y0, w, h, frame, seaDir; public string occ, skyv, lv, only;
            public bool hasTide, hasTideHigh; public double tide, tideHigh;
        }
        [Serializable] public sealed class CoverageJson { public int tips, marks, puddles, water, snow, rain, fog, tide, occ, lv; }
        [Serializable] public sealed class CaseJson
        {
            public string name; public int gbuffer; public SkyJson sky; public OptionsJson options;
            public string rgba, stable; public int stableCount; public CoverageJson coverage;
        }
        [Serializable] public sealed class HypotJson { public string args2, want2, args3, want3; }
        [Serializable] public sealed class Fixture { public string rigSha256; public GBufferJson[] gbuffers; public CaseJson[] cases; public HypotJson hypot; }

        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static Fixture Load()
        {
            string path = Path.Combine(Root, FixturePath);
            Assert.IsTrue(File.Exists(path), $"{FixturePath} is missing; node docs/art/rigs/terrain/pass9/twinFixture.js writes it");
            var f = JsonUtility.FromJson<Fixture>(File.ReadAllText(path));
            Assert.IsNotNull(f?.cases, $"{FixturePath} did not parse");
            return f;
        }

        // ---- the fixture's encodings: base64 of each typed array's little-endian bytes ------------------------
        static byte[] B(string s) => Convert.FromBase64String(s);
        static byte[] BOrNull(string s) => string.IsNullOrEmpty(s) ? null : B(s);
        static float[] F32(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            byte[] b = B(s); var a = new float[b.Length / 4];
            for (int i = 0; i < a.Length; i++) a[i] = BitConverter.ToSingle(LittleEndian(b, i * 4, 4), 0);
            return a;
        }
        static double[] F64(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            byte[] b = B(s); var a = new double[b.Length / 8];
            for (int i = 0; i < a.Length; i++) a[i] = BitConverter.ToDouble(LittleEndian(b, i * 8, 8), 0);
            return a;
        }
        static int[] I32(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            byte[] b = B(s); var a = new int[b.Length / 4];
            for (int i = 0; i < a.Length; i++) a[i] = b[4 * i] | b[4 * i + 1] << 8 | b[4 * i + 2] << 16 | b[4 * i + 3] << 24;
            return a;
        }
        static byte[] LittleEndian(byte[] b, int at, int n)
        {
            var r = new byte[n]; Array.Copy(b, at, r, 0, n);
            if (!BitConverter.IsLittleEndian) Array.Reverse(r);
            return r;
        }
        static string OrNull(string s) => string.IsNullOrEmpty(s) ? null : s;

        static TerrainLight6.GBuffer ToGBuffer(GBufferJson e)
        {
            byte[] band0 = B(e.band0), pal = B(e.pal), mark = B(e.mark);
            var pals = new int[e.pals.Length / 5][][];
            for (int p = 0; p < pals.Length; p++)
            {
                pals[p] = new int[5][];
                for (int b = 0; b < 5; b++) pals[p][b] = TerrainLight6.H2R(e.pals[p * 5 + b]);
            }
            double[] far = F64(e.far);
            var g = new TerrainLight6.GBuffer
            {
                N = e.n, M = e.m, Wrap = e.wrap, HMax = F64(e.hmax)[0],
                H = F32(e.H), Nx = F32(e.nx), Ny = F32(e.ny), Nz = F32(e.nz), Ao = F32(e.ao),
                Proud = F32(e.proud), Hollow = F32(e.hollow), Pond = F32(e.pond),
                Cls = B(e.cls), Band0 = new int[band0.Length], Pal = new int[pal.Length], Mark = new int[mark.Length / 2],
                A = BOrNull(e.alpha), Pals = pals,
                Elev = F32(e.elev), Slope = F32(e.slope), Fetch = F32(e.fetch),
                Far = far == null ? null : (Func<int, double>)(y => far[y]),
            };
            for (int i = 0; i < band0.Length; i++) g.Band0[i] = (sbyte)band0[i];
            for (int i = 0; i < pal.Length; i++) g.Pal[i] = pal[i];
            for (int i = 0; i < g.Mark.Length; i++) g.Mark[i] = mark[2 * i] | mark[2 * i + 1] << 8;
            return g;
        }

        static TerrainLight6.Sky ToSky(SkyJson s) => new TerrainLight6.Sky
        {
            SunF = s.sunF, SunI = s.sunI, Expo = s.expo, SkyI = s.hasSkyI ? s.skyI : (double?)null, Wind = s.hasWind ? s.wind : (double?)null,
            Snow = s.snow, Wet = s.wet, Rain = s.rain, Fog = s.fog, Grade = s.grade,
            Kc = OrNull(s.kc), Ac = OrNull(s.ac), Wash = OrNull(s.wash), FogC = OrNull(s.fogC), SkyC = OrNull(s.skyC),
            Aa = s.aa, Amb = s.amb, Ka = s.ka, Wa = s.wa,
        };

        static TerrainLight6.Options ToOptions(OptionsJson o) => new TerrainLight6.Options
        {
            X0 = o.x0, Y0 = o.y0, W = o.w, H = o.h, Frame = o.frame, SeaDir = o.seaDir,
            Occ = BOrNull(o.occ), Skyv = F32(o.skyv), Lv = I32(o.lv), Only = I32(o.only),
            Tide = o.hasTide ? o.tide : (double?)null, TideHigh = o.hasTideHigh ? o.tideHigh : (double?)null,
        };

        static int[] Coverage(CoverageJson c) => new[] { c.tips, c.marks, c.puddles, c.water, c.snow, c.rain, c.fog, c.tide, c.occ, c.lv };

        // ---- the tests ------------------------------------------------------------------------------------------

        /// <summary>The fixture was written by the rig this repo commits: its stamp is the sha256 of
        /// <see cref="RigPath"/>'s bytes (LF, pinned by <c>.gitattributes</c>). A new drop of the rig
        /// without a re-run of the generator reads red here.</summary>
        [Test]
        public void Fixture_WasWrittenByTheCommittedRig()
        {
            string rig = Path.Combine(Root, RigPath);
            Assert.IsTrue(File.Exists(rig), $"{RigPath} is missing");
            string hash;
            using (var sha = SHA256.Create())
            {
                var sb = new StringBuilder();
                foreach (byte b in sha.ComputeHash(File.ReadAllBytes(rig))) sb.Append(b.ToString("x2"));
                hash = sb.ToString();
            }
            Assert.AreEqual(Load().rigSha256, hash,
                $"{FixturePath} was written by another {RigPath}; re-run node docs/art/rigs/terrain/pass9/twinFixture.js");
        }

        /// <summary>Every case, relit through the twin, gives the kit's bytes at every stable texel.</summary>
        [Test]
        public void Twin_RelightsEveryCase_AsTheKitDid()
        {
            var f = Load();
            var g = new TerrainLight6.GBuffer[f.gbuffers.Length];
            for (int k = 0; k < g.Length; k++) g[k] = ToGBuffer(f.gbuffers[k]);
            var faults = new List<string>();
            int compared = 0;
            foreach (var c in f.cases)
            {
                byte[] want = B(c.rgba), stable = B(c.stable);
                byte[] got = TerrainLight6.Relight(g[c.gbuffer], ToSky(c.sky), ToOptions(c.options));
                if (got.Length != want.Length) { faults.Add($"{c.name}: the twin wrote {got.Length} bytes, the kit {want.Length}"); continue; }
                int w = c.options.w != 0 ? c.options.w : g[c.gbuffer].N, miss = 0, first = -1;
                for (int i = 0; i < stable.Length; i++)
                {
                    if (stable[i] == 0) continue;
                    compared++;
                    int q = i * 4;
                    if (got[q] != want[q] || got[q + 1] != want[q + 1] || got[q + 2] != want[q + 2] || got[q + 3] != want[q + 3])
                        if (miss++ == 0) first = i;
                }
                if (miss == 0) continue;
                int t = first * 4;
                faults.Add($"{c.name}: {miss} texel(s) differ; the first, ({first % w}, {first / w}): " +
                           $"twin {got[t]},{got[t + 1]},{got[t + 2]},{got[t + 3]}, kit {want[t]},{want[t + 1]},{want[t + 2]},{want[t + 3]}");
            }
            Assert.IsEmpty(faults, "The twin is not the kit's light:\n  " + string.Join("\n  ", faults));
            Assert.Greater(compared, 0, "no texel was compared");
        }

        /// <summary>The twin's <c>Hypot</c> is V8's <c>Math.hypot</c>, bit for bit, at the fixture's
        /// samples: each is one where the plain square root of the sum of squares rounds differently, so
        /// the relit bytes alone would not tell the two apart.</summary>
        [Test]
        public void Twin_Hypot_IsV8s_BitForBit()
        {
            var h = Load().hypot;
            var faults = new List<string>();
            double[] a2 = F64(h.args2), w2 = F64(h.want2), a3 = F64(h.args3), w3 = F64(h.want3);
            Assert.IsNotEmpty(w2, "no two-argument samples"); Assert.IsNotEmpty(w3, "no three-argument samples");
            for (int j = 0; j < w2.Length; j++)
            {
                double got = TerrainLight6.Hypot(a2[2 * j], a2[2 * j + 1]);
                if (BitConverter.DoubleToInt64Bits(got) != BitConverter.DoubleToInt64Bits(w2[j]))
                    faults.Add($"hypot({a2[2 * j]:R}, {a2[2 * j + 1]:R}) = {got:R}, V8 {w2[j]:R}");
            }
            for (int j = 0; j < w3.Length; j++)
            {
                double got = TerrainLight6.Hypot(a3[3 * j], a3[3 * j + 1], a3[3 * j + 2]);
                if (BitConverter.DoubleToInt64Bits(got) != BitConverter.DoubleToInt64Bits(w3[j]))
                    faults.Add($"hypot({a3[3 * j]:R}, {a3[3 * j + 1]:R}, {a3[3 * j + 2]:R}) = {got:R}, V8 {w3[j]:R}");
            }
            Assert.IsEmpty(faults, "Not V8's Math.hypot:\n  " + string.Join("\n  ", faults));
        }

        /// <summary>The fixture tests what it claims to. Every input the relight reads decides some texels
        /// in some case (the counts are the kit's, measured by switching the input off), each case's
        /// stable mask agrees with its count, and at least 90% of each case's texels are compared.</summary>
        [Test]
        public void Fixture_ExercisesEveryInput_AndComparesMostTexels()
        {
            var f = Load();
            var faults = new List<string>();
            var sum = new int[Inputs.Length];
            foreach (var c in f.cases)
            {
                int[] v = Coverage(c.coverage);
                for (int k = 0; k < v.Length; k++) sum[k] += v[k];
                byte[] stable = B(c.stable);
                int n = 0;
                foreach (byte s in stable) n += s;
                if (n != c.stableCount) faults.Add($"{c.name}: the mask holds {n} stable texels, the count says {c.stableCount}");
                if (n * 10 < stable.Length * 9) faults.Add($"{c.name}: only {n} of {stable.Length} texels are stable");
            }
            for (int k = 0; k < Inputs.Length; k++)
                if (sum[k] == 0) faults.Add($"no case exercises {Inputs[k]}");
            Assert.IsEmpty(faults, "The fixture does not test what it claims:\n  " + string.Join("\n  ", faults));
        }
    }
}
