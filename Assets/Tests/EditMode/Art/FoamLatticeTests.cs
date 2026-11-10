using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Guards for the open-sea repeat: the same cluster of whitecaps came back every 1.7 m across the
    /// wind, several times inside one screen.
    ///
    /// <para><b>The cause.</b> The sea's shared <c>Hash21</c> scales a lattice cell by (123.34, 345.45)
    /// and keeps the fraction. On whole cells that fraction steps by 17/50 in x and 9/20 in y, so the
    /// hash repeats every 50 cells in x and every 20 cells in y. The whitecap field compresses its
    /// cross-wind axis by the streak stretch, which brought the 20-cell repeat down to a couple of
    /// metres of sea.</para>
    ///
    /// <para><b>The fix these tests pin.</b> The evolving foam field reads its own lattice
    /// (<c>FoamHash21</c> through <c>FoamValueNoise</c>). The shared <c>Hash21</c> is left alone: its
    /// arithmetic is pinned to the hull caustic pattern by another suite. Everything here is source
    /// and arithmetic and runs without a graphics device. Whether the sea LOOKS right is a plate and
    /// an eyeball; no test asserts that.</para>
    /// </summary>
    public class FoamLatticeTests
    {
        private const string WaterShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";

        private static string Function(string source, string signature)
        {
            int at = source.IndexOf(signature, System.StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThan(0), signature + " not found");
            int begin = source.IndexOf('{', at), depth = 1, end = begin + 1;
            while (depth > 0) { if (source[end] == '{') depth++; if (source[end] == '}') depth--; end++; }
            return source.Substring(at, end - at);
        }

        private static string Code(string text) =>
            Regex.Replace(Regex.Replace(text, @"//[^\n]*", ""), @"\s+", "");

        private static float Frac(float v) => v - Mathf.Floor(v);

        /// <summary>Twin of the shader's FoamHash21. The source test below keeps the two tied.</summary>
        private static float FoamHash(float x, float y)
        {
            float ax = Frac(x * 0.1031f), ay = Frac(y * 0.1031f), az = ax;
            float d = ax * (ay + 33.33f) + ay * (az + 33.33f) + az * (ax + 33.33f);
            ax += d; ay += d; az += d;
            return Frac((ax + ay) * az);
        }

        [Test]
        public void FoamHash21_SourceIsTheArithmeticTheTwinMirrors()
        {
            string body = Code(Function(File.ReadAllText(WaterShaderPath), "float FoamHash21(float2 p)"));
            Assert.That(body, Is.EqualTo(
                "floatFoamHash21(float2p){" +
                "float3p3=frac(float3(p.x,p.y,p.x)*0.1031);" +
                "p3+=dot(p3,p3.yzx+33.33);" +
                "returnfrac((p3.x+p3.y)*p3.z);}"),
                "the foam lattice hash moved; move the C# twin in this file with it");
        }

        [Test]
        public void FoamValueNoise_ReadsTheFoamLatticeAtTheFourCellCorners()
        {
            string body = Function(File.ReadAllText(WaterShaderPath), "float FoamValueNoise(float2 p)");
            Assert.That(Regex.Matches(body, @"FoamHash21\(").Count, Is.EqualTo(4));
            Assert.That(Regex.Matches(body, @"(?<!Foam)Hash21\(").Count, Is.EqualTo(0),
                "the foam noise must not read the shared 50 x 20 cell lattice");
            StringAssert.Contains("f * f * (3.0 - 2.0 * f)", body,
                "same smoothstep weights as ValueNoise: the look of one blob does not change");
        }

        [Test]
        public void EvolvingField_ReadsTheFoamLattice_NotTheSharedOne()
        {
            // EvolvingField feeds the whitecaps, the clump rafts, the shore and surf churn and the wake
            // lace. One switch here moves all of them off the repeating lattice together.
            string body = Function(File.ReadAllText(WaterShaderPath), "float EvolvingField(float2 worldXY");
            Assert.That(Regex.Matches(body, @"FoamValueNoise\(").Count, Is.EqualTo(4));
            Assert.That(Regex.Matches(Regex.Replace(body, @"//[^\n]*", ""), @"(?<!Foam)ValueNoise\(").Count,
                Is.EqualTo(0), "a tap left on the shared lattice brings the 1.7 m repeat back");
            StringAssert.Contains("Pixelize(", body, "the field stays on the hard pixel grid");
        }

        [Test]
        public void TheFoamLattice_HasNoShortPeriod_NearAndFarFromTheOrigin()
        {
            // Two independent uniform values differ by 1/3 on average. A lattice that repeats at a
            // shift gives about 0 there (the shared hash gives 0 at x + 50 and at y + 20). The far
            // origins are where a float hash usually collapses: the foam coordinate grows with
            // drift x time, so a long session reads cells far from zero.
            const int patch = 48;
            foreach (int origin in new[] { 0, -760, 5000, 40000 })
                for (int shift = 1; shift <= 256; shift++)
                {
                    float alongX = 0f, alongY = 0f;
                    for (int y = 0; y < patch; y++)
                        for (int x = 0; x < patch; x++)
                        {
                            float here = FoamHash(origin + x, origin + y);
                            alongX += Mathf.Abs(FoamHash(origin + x + shift, origin + y) - here);
                            alongY += Mathf.Abs(FoamHash(origin + x, origin + y + shift) - here);
                        }
                    alongX /= patch * patch; alongY /= patch * patch;
                    Assert.That(alongX, Is.GreaterThan(0.2f), "repeat along x at shift " + shift + ", origin " + origin);
                    Assert.That(alongY, Is.GreaterThan(0.2f), "repeat along y at shift " + shift + ", origin " + origin);
                }
        }

        [Test]
        public void TheFoamLattice_IsEvenlySpread()
        {
            // The whitecap threshold reads this field's distribution; a lopsided hash would move how
            // much of the sea is white. Ten equal buckets, each within a third of its fair share.
            var buckets = new int[10];
            const int side = 200;
            double sum = 0;
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                {
                    float v = FoamHash(x - 100, y - 100);
                    Assert.That(v, Is.InRange(0f, 1f));
                    sum += v;
                    buckets[Mathf.Min(9, (int)(v * 10f))]++;
                }
            Assert.That(sum / (side * side), Is.InRange(0.47, 0.53));
            foreach (int count in buckets)
                Assert.That(count / (float)(side * side), Is.InRange(0.067f, 0.133f));
        }
    }
}
