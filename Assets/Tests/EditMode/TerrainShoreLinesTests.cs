using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.App.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class TerrainShoreLinesTests
    {
        TerrainShoreLinesDef _def;
        [SetUp] public void SetUp() => _def = ScriptableObject.CreateInstance<TerrainShoreLinesDef>();
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(_def);

        static ShoreLineDerivation.Grid Fixture(Func<Vector2, float> elevation,
            Func<Vector2, ShoreLineDerivation.Substrate> substrate = null)
        {
            const int size = 64;
            var values = new float[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) values[y * size + x] = elevation(new Vector2((x + .5f) * .5f - 16, (y + .5f) * .5f - 16));
            return new ShoreLineDerivation.Grid(values, size, size, new Vector2(-16, -16), new Vector2(32, 32), substrate);
        }

        static string Hash(ShoreLineDerivation.Result result)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            foreach (var slice in result.Addresses)
            foreach (Color c in slice) { writer.Write(c.r); writer.Write(c.g); writer.Write(c.b); writer.Write(c.a); }
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream.ToArray()));
        }

        [Test] public void SamePaintedHeight_ProducesIdenticalAddressBytes()
        {
            var grid = Fixture(p => 6 - Mathf.Max(0, p.magnitude - 5) * 2);
            var a = ShoreLineDerivation.Build(grid, _def);
            var b = ShoreLineDerivation.Build(grid, _def);
            Assert.AreEqual(Hash(a), Hash(b));
            Assert.Greater(a.Lines[0].Length, 0, "A steep shore must yield a turf brow.");
            var changed = Fixture(p => 7 - Mathf.Max(0, p.magnitude - 5) * 2);
            Assert.AreNotEqual(Hash(a), Hash(ShoreLineDerivation.Build(changed, _def)), "Height changes must invalidate the derivation.");
        }

        [Test] public void BrowFollowsSlopeBreak_AboveSpringHigh()
        {
            var grid = Fixture(p => 6 - Mathf.Max(0, p.magnitude - 5) * 2);
            var result = ShoreLineDerivation.Build(grid, _def);
            Assert.Greater(result.Lines[0].Length, 0);
            foreach (var segment in result.Lines[0])
            {
                Assert.Greater(grid.At(segment.A), _def.SpringHigh);
                Assert.That(segment.A.magnitude, Is.InRange(3.8f, 6.2f), "Brow must follow the plateau edge, not the tide contour.");
            }
            Assert.AreEqual(0, result.Lines[1].Length, "A steep cliff is a turf brow, not a second scarp.");
            var gentle = Fixture(p => 6 - Mathf.Max(0, p.magnitude - 5) * .5f);
            Assert.Greater(ShoreLineDerivation.Build(gentle, _def).Lines[1].Length, 0, "A beach bank must yield a scarp.");
        }

        [Test] public void OpenContour_HasMetricDistanceAndNoBandOutsideFourMetres()
        {
            var result = ShoreLineDerivation.Build(Fixture(p => -p.x), _def);
            int visible = 0;
            // A straight spring-high line at x=-2.2; seaward is +x, independently known.
            int y = result.Height / 2;
            for (int x = 0; x < result.Width; x++)
            {
                Color a = result.Sample(2, new Vector2(-16 + x + .5f, -16 + y + .5f));
                float expectedDistance = -16 + x + .5f + 2.2f;
                if (Mathf.Abs(expectedDistance) < 5) Assert.AreEqual(expectedDistance, a.r, 1e-4f);
                bool inBand = ShoreLineDerivation.InBand(2, a);
                Assert.AreEqual(expectedDistance >= -1.84f && expectedDistance <= 2.16f, inBand);
                if (inBand) visible++;
            }
            Assert.AreEqual(4, visible, "A four-metre band spans four one-metre sample centres.");
            for (int kind = 0; kind < 5; kind++)
            {
                float land = -ShoreLineDerivation.Anchors[kind] * 4;
                float sea = land + 4;
                Assert.IsFalse(ShoreLineDerivation.InBand(kind, land - .001f));
                Assert.IsFalse(ShoreLineDerivation.InBand(kind, sea + .001f));
            }
        }

        [Test] public void ClosedContour_RepeatsMeetAtTheSeam()
        {
            var result = ShoreLineDerivation.Build(Fixture(p => 6 - p.magnitude), _def);
            var line = result.Lines[2];
            Assert.Greater(line.Length, 0);
            var first = line[0]; var last = line[line.Length - 1];
            Assert.Less(Vector2.Distance(first.A, last.B), 1e-4f);
            float repeats = (last.Arc + Vector2.Distance(last.A, last.B)) * last.RepeatScale / 8;
            Assert.AreEqual(Mathf.Round(repeats), repeats, 1e-4f);
        }

        [Test] public void BendDistance_IsInMetresInEveryQuadrant()
        {
            var result = ShoreLineDerivation.Build(Fixture(p => 6 - p.magnitude), _def);
            int checkedPoints = 0;
            for (int y = 0; y < result.Height; y++)
            for (int x = 0; x < result.Width; x++)
            {
                float radius = new Vector2(-16 + x + .5f, -16 + y + .5f).magnitude;
                if (Mathf.Abs(radius - 3.8f) > 2) continue;
                Color address = result.Sample(2, new Vector2(-16 + x + .5f, -16 + y + .5f));
                Assert.AreEqual(Mathf.Abs(address.r), address.a, 1e-4f);
                Assert.AreEqual(radius - 3.8f, address.r, .08f, "Distance must follow the bend, not a world axis or height delta.");
                checkedPoints++;
            }
            Assert.Greater(checkedPoints, 50);
        }

        [Test] public void InlandBump_DoesNotBecomeAShoreBrowOrStrip()
        {
            var grid = Fixture(p => 2.2f + p.y * (p.y < 0 ? .5f : .02f)
                + 6 * Mathf.Max(0, 1 - (p - new Vector2(0, 8)).sqrMagnitude / 9));
            var result = ShoreLineDerivation.Build(grid, _def);
            Assert.AreEqual(0, result.Lines[0].Length, "An inland hill beyond the first shallow break is not a turf brow.");
            Assert.AreEqual(0, result.Lines[1].Length, "A flat shore must not search onward to an inland hill.");
            for (int y = 0; y < result.Height; y++)
            for (int x = 0; x < result.Width; x++)
            {
                var p = new Vector2(-16 + x + .5f, -16 + y + .5f);
                if (Vector2.Distance(p, new Vector2(0, 8)) > 4) continue;
                for (int kind = 0; kind < 5; kind++) Assert.IsFalse(ShoreLineDerivation.InBand(kind,
                    result.Sample(kind, p)), "No strip belongs at the inland bump.");
            }
        }

        [Test] public void FilteringOppositeSigns_CannotInventAnInlandBand()
        {
            // Both samples are five metres from a curve. Filtering their signs must not make a new curve.
            Color filtered = Color.Lerp(new Color(-5, 0, 1, 5), new Color(5, 0, 1, 5), .5f);
            Assert.AreEqual(0, filtered.r);
            for (int kind = 0; kind < 5; kind++) Assert.IsFalse(ShoreLineDerivation.InBand(kind, filtered));
        }

        [Test] public void SandBermRejectsCliffStrips_WhileTurfBermAtPointTwoSlopeHasBoth()
        {
            float Berm(Vector2 p) => 2.2f + Mathf.Min(p.y, 1.5f) * .2f;
            var sand = ShoreLineDerivation.Build(Fixture(Berm, p => new ShoreLineDerivation.Substrate(0, sand: 1)), _def);
            Assert.AreEqual(0, sand.Lines[0].Length); Assert.AreEqual(0, sand.Lines[1].Length);
            Assert.IsTrue(sand.Addresses.Take(2).SelectMany(a => a).All(c => c == Color.clear));
            var turf = ShoreLineDerivation.Build(Fixture(Berm, p => new ShoreLineDerivation.Substrate(1)), _def);
            Assert.Greater(turf.Lines[0].Length, 0); Assert.Greater(turf.Lines[1].Length, 0);
        }

        [Test] public void TalusAboveHalfWritesNoStripCell_AndPointFourWithTurfKeepsLip()
        {
            float Berm(Vector2 p) => 2.2f + Mathf.Min(p.y, 1.5f) * .2f;
            ShoreLineDerivation.Substrate Cover(Vector2 p, float talus) => p.y >= 1 && p.y <= 3
                ? new ShoreLineDerivation.Substrate(1 - talus, talus: talus) : new ShoreLineDerivation.Substrate(1);
            var blocked = ShoreLineDerivation.Build(Fixture(Berm, p => Cover(p, .6f)), _def);
            for (int y = 17; y <= 18; y++)
            for (int x = 0; x < blocked.Width; x++)
            for (int kind = 0; kind < 5; kind++) Assert.AreEqual(Color.clear, blocked.Addresses[kind][y * blocked.Width + x]);
            var allowed = ShoreLineDerivation.Build(Fixture(Berm, p => Cover(p, .4f)), _def);
            Assert.Greater(allowed.Lines[0].Length, 0);
            Assert.IsTrue(ShoreLineDerivation.InBand(0, allowed.Sample(0, new Vector2(0, 2))));
            Assert.IsTrue(new ShoreLineDerivation.Substrate(1, ledge: .6f).ProtectedFoot);
            Assert.IsTrue(new ShoreLineDerivation.Substrate(1, shelf: .6f).ProtectedFoot);
        }

        [Test] public void FragmentProjectionFollowsCurvedShoreWithinQuarterMetre()
        {
            var result = ShoreLineDerivation.Build(Fixture(p => 6 - p.magnitude), _def);
            int checkedPoints = 0;
            for (float y = -6; y <= 6; y += .125f)
            for (float x = -6; x <= 6; x += .125f)
            {
                Vector2 point = new Vector2(x, y); float expected = point.magnitude - 3.8f;
                if (Mathf.Abs(expected) > 2) continue;
                Assert.LessOrEqual(Mathf.Abs(result.Sample(2, point).r - expected), .25f);
                checkedPoints++;
            }
            Assert.Greater(checkedPoints, 1000);
        }

        [Test] public void ShaderPatterns_AreSparseSeededAndHaveNoTwentyMetreRepeat()
        {
            const string code = @"Shader ""Hidden/ShorePatternPin"" { SubShader { Pass { ZWrite Off ZTest Always Cull Off
HLSLPROGRAM
#pragma target 3.5
#pragma vertex vert
#pragma fragment frag
#include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
float _ShoreLoaded,_ShoreStep4,_ShoreBlendMetres,_ShoreSourceBlendMetres;
float4 _ShoreSteps0123,_ShoreGrid,_HeightWorldMin,_HeightWorldSize;
float4 _ShoreRange01,_ShoreRange23,_ShoreRange4;
int _ShoreSeed;float4 _ShoreWrack,_ShorePattern,_ShoreLip,_ShoreLipShadow;
float _ShoreWallsLoaded;float4 _ShoreWallGrid;
#include ""Assets/_Project/Art/Shaders/Include/TerrainShoreLines.hlsl""
struct V {float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
V vert(uint id:SV_VertexID){V o;o.uv=float2((id<<1)&2,id&2);o.position=float4(o.uv*2-1,0,1);return o;}
float4 frag(V i):SV_Target {float d=i.uv.y*4-2;float4 a=float4(d,i.uv.x*64,1,abs(d));float2 uv;
return float4(SL_Wrack(a,uv),SL_Lip(a),SL_Variant((int)floor(a.g/6))/3.0,1);}
ENDHLSL
} } }";
            Shader shader = ShaderUtil.CreateShaderAsset(code);
            var material = new Material(shader); var rt = new RenderTexture(2048, 128, 0, RenderTextureFormat.ARGBFloat);
            var pixels = new Texture2D(2048, 128, TextureFormat.RGBAFloat, false, true);
            RenderTexture previous = RenderTexture.active;
            try
            {
                material.SetInt("_ShoreSeed", 17041); material.SetFloat("_ShoreBlendMetres", .25f);
                material.SetVector("_ShoreWrack", new Vector4(1, 3, 6, .5f));
                material.SetVector("_ShorePattern", new Vector4(2.5f, .6f, 0, 0));
                material.SetVector("_ShoreLip", new Vector4(.5f, 1, 1, 2));
                ShaderUtil.CompilePass(material, 0, true);
                Assert.IsFalse(ShaderUtil.GetShaderMessages(shader).Any(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error));
                Graphics.SetRenderTarget(rt); GL.Clear(false, true, Color.clear); material.SetPass(0);
                Graphics.DrawProceduralNow(MeshTopology.Triangles, 3);
                pixels.ReadPixels(new Rect(0, 0, 2048, 128), 0, 0); pixels.Apply();
                Color[] data = pixels.GetPixels();
                float coverage = data.Count(c => c.r > 0) / (float)data.Length;
                Assert.That(coverage, Is.GreaterThan(.01f).And.LessThanOrEqualTo(.4f));
                Assert.IsTrue(data.Any(c => c.g > 0));
                Assert.IsTrue(Enumerable.Range(0, 640).Any(x => Math.Abs(data[64 * 2048 + x].r - data[64 * 2048 + x + 640].r) > .01f));
                // seat ruling s11, 11-07: the readback row's orientation is not assumed; the lip is landward only
                int lipRow = 48, mirror = 127 - 48;
                float lipMax = Enumerable.Range(0, 2048).Max(x => data[lipRow * 2048 + x].g);
                float mirrorMax = Enumerable.Range(0, 2048).Max(x => data[mirror * 2048 + x].g);
                bool lipPresent = lipMax > .01f, mirrorPresent = mirrorMax > .01f;
                string rowDump = FormattableString.Invariant($"seat ruling s11: row {lipRow} max g={lipMax:R}, presence={lipPresent}; row {mirror} max g={mirrorMax:R}, presence={mirrorPresent}");
                TestContext.Progress.WriteLine(rowDump);
                Assert.IsTrue(lipPresent ^ mirrorPresent, rowDump);
                int measuredLipRow = lipPresent ? lipRow : mirror;
                Assert.IsTrue(Enumerable.Range(0, 640).Any(x => Math.Abs(data[measuredLipRow * 2048 + x].g - data[measuredLipRow * 2048 + x + 640].g) > .01f), rowDump);
                int[] variants = Enumerable.Range(0, 10).Select(i => Mathf.RoundToInt(data[(i * 192 + 96)].b * 3)).ToArray();
                Assert.GreaterOrEqual(variants.Distinct().Count(), 3);
                for (int i = 1; i < variants.Length; i++) Assert.AreNotEqual(variants[i - 1], variants[i]);
            }
            finally
            {
                RenderTexture.active = previous; rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(material); UnityEngine.Object.DestroyImmediate(shader);
            }
        }

        [Test] public void ArrayPins_AllFifteenSlicesAndTheirBytes()
        {
            var array = AssetDatabase.LoadAssetAtPath<Texture2DArray>(TerrainEdgeArrayImporter.RecipePath);
            Assert.IsNotNull(array);
            Assert.AreEqual(256, array.width); Assert.AreEqual(128, array.height); Assert.AreEqual(15, array.depth);
            Assert.AreEqual(TextureFormat.RGBA32, array.format); Assert.AreEqual(1, array.mipmapCount);
            Assert.IsTrue(array.isDataSRGB); Assert.AreEqual(FilterMode.Point, array.filterMode);
            Assert.AreEqual(TextureWrapMode.Repeat, array.wrapModeU); Assert.AreEqual(TextureWrapMode.Clamp, array.wrapModeV);
            string[] kinds = { "Turf", "Scarp", "Wrack", "Weedline", "Reefedge" };
            string[] steps = { "_Lo", "", "_Hi" };
            for (int kind = 0; kind < kinds.Length; kind++)
            for (int step = 0; step < steps.Length; step++)
            {
                string path = "Assets/_Project/Art/Terrain/Edges/" + kinds[kind] + steps[step] + ".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.AreEqual(FilterMode.Point, importer.filterMode, path);
                Assert.AreEqual(TextureWrapMode.Repeat, importer.wrapModeU, path);
                Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapModeV, path);
                Assert.IsFalse(importer.mipmapEnabled, path);
                Assert.IsTrue(importer.alphaIsTransparency, path);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, path);
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.IsTrue(ImageConversion.LoadImage(source, File.ReadAllBytes(path)));
                    CollectionAssert.AreEqual(source.GetPixels32(), array.GetPixels32(kind * 3 + step), path);
                }
                finally { UnityEngine.Object.DestroyImmediate(source); }
            }
        }

        [Test] public void ShaderSamplesOnlyInsideBand_SeawardFirst()
        {
            string shader = File.ReadAllText("Assets/_Project/Art/Shaders/Include/TerrainShoreLines.hlsl");
            StringAssert.Contains("t >= 0.0 && t <= 1.0", shader);
            StringAssert.Contains("if (SL_InBand(a, kind))", shader);
            StringAssert.Contains("for (int order = 0; order < 5; order++)", shader);
            StringAssert.Contains("int kind = 4 - order;", shader);
            Assert.AreEqual(1, Regex.Matches(shader, @"SAMPLE_TEXTURE2D_ARRAY_LOD\(_ShoreStrips").Count);
            StringAssert.Contains("_ShoreLoaded < 0.5", shader);
            StringAssert.Contains("_ShoreFields.Load", shader);
            StringAssert.DoesNotContain("SAMPLE_TEXTURE2D_ARRAY_LOD(_ShoreFields", shader);
        }

        [Test] public void NoShoreCpuWorkInFrameCallbacks_AndSplatLayoutUnchanged()
        {
            string source = File.ReadAllText("Assets/_Project/Code/Art/TerrainSplatSurface.cs");
            var update = Regex.Match(source, @"private void Update\(\)\s*\{(?<body>.*?)\n        \}", RegexOptions.Singleline);
            Assert.IsTrue(update.Success);
            StringAssert.DoesNotContain("Shore", update.Groups["body"].Value);
            StringAssert.Contains("PushWaterLevel();", update.Groups["body"].Value);
            string derive = File.ReadAllText("Assets/_Project/Code/Art/ShoreLineDerivation.cs");
            StringAssert.DoesNotContain("MonoBehaviour", derive);
            StringAssert.DoesNotContain("Time.", derive);
            Assert.AreEqual(6, TerrainSplatBrush.TextureCount); Assert.AreEqual(21, TerrainSplatBrush.MaterialCount);
            string surface = File.ReadAllText("Assets/_Project/Code/Art/TerrainSplatSurface.cs");
            Assert.AreEqual(1, Regex.Matches(surface, @"TerrainShoreLineBinding.Create\(").Count);
        }
    }
}
