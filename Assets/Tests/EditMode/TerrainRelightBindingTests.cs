using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Terrain pass 9's relight wiring, short of the maps themselves (they land with their arrays): the
    /// surface binds TerrainLight6's four inputs only when they fit the detail array, and the builder packs
    /// the palette ramp where the shader reads it.
    ///
    /// <para>The expectations are the shader include's (Include/TerrainLight6.hlsl), read from its source
    /// or written out by hand from its header: a tile TL6_TILE texels square, palette p's band b at
    /// x = p * 5 + b, and the slice's parameters at TL6_RAMP_PARAMS. None is read from the C# under
    /// test.</para>
    /// </summary>
    public class TerrainRelightBindingTests
    {
        const string IncludePath = "Assets/_Project/Art/Shaders/Include/TerrainLight6.hlsl";
        const int Tile = 256;      // TL6_TILE
        const int RampWidth = 81;  // sixteen palettes of five, then the parameters (TL6_RAMP_PARAMS = 80)

        // ============================ THE SHADER'S LAYOUT ============================

        [Test]
        public void TheIncludesTileAndRamp_AreTheOnesTheSurfaceChecksAndTheBuilderPacks()
        {
            string src = File.ReadAllText(Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                                                       IncludePath));
            int tile = Define(src, "TL6_TILE"), paramsAt = Define(src, "TL6_RAMP_PARAMS");
            Match pal = Regex.Match(src, @"LOAD_TEXTURE2D\(_RelightRamp, int2\(p \* (\d+) \+ b, s\)\)");
            Assert.IsTrue(pal.Success, "the include's TL6Pal no longer reads the ramp at x = p * bands + b");
            int bands = int.Parse(pal.Groups[1].Value);

            Assert.AreEqual(tile, TerrainSplatSurface.RelightTileTexels,
                "the surface accepts relight slices of a size the shader does not mask its loads to");
            Assert.AreEqual(paramsAt + 1, TerrainSplatSurface.RelightRampWidth,
                "the surface accepts a ramp whose parameters are not where the shader reads them");
            Assert.AreEqual(bands, TerrainTexArrayBuilder.RelightBands,
                "the builder packs palettes of a length the shader does not step by");
            Assert.AreEqual(paramsAt, TerrainTexArrayBuilder.RelightPalettes * TerrainTexArrayBuilder.RelightBands,
                "the builder's palettes do not end where the shader reads the parameters");
        }

        [Test]
        public void TheRampRow_HoldsEachPaletteBandAtItsColumn_AndTheParametersAtEighty()
        {
            // Two palettes of five, each colour its own index (r = i + 1, g = i + 101, b = i + 201), so a
            // colour read from the wrong column names the column it came from.
            var palettes = new string[10];
            for (int i = 0; i < palettes.Length; i++) palettes[i] = $"#{i + 1:x2}{i + 101:x2}{i + 201:x2}";
            var ramp = new Color[RampWidth * 2];
            for (int x = 0; x < RampWidth; x++) ramp[RampWidth + x] = Color.magenta;   // stale: must be overwritten

            Assert.IsNull(TerrainTexArrayBuilder.PackRampRow(palettes, -2.5f, 3.75f, 1.25f, ramp, RampWidth));

            Assert.AreEqual(new Color(1, 101, 201, 255), ramp[RampWidth + 0], "palette 0, band 0 (x = 0)");
            Assert.AreEqual(new Color(9, 109, 209, 255), ramp[RampWidth + 8], "palette 1, band 3 (x = 1 * 5 + 3)");
            Assert.AreEqual(new Color(10, 110, 210, 255), ramp[RampWidth + 9], "palette 1, band 4 (x = 9)");
            Assert.AreEqual(default(Color), ramp[RampWidth + 10], "palette 2 does not exist: x = 10 must be zero");
            Assert.AreEqual(default(Color), ramp[RampWidth + 79], "palette 15 does not exist: x = 79 must be zero");
            Assert.AreEqual(new Color(-2.5f, 3.75f, 1.25f, 1f), ramp[RampWidth + 80],
                "x = 80 is not (heightMin, heightRange, pondMax, 1)");
            Assert.AreEqual(default(Color), ramp[8], "the pack wrote outside its own row");
        }

        [Test]
        public void ARampRowTheShaderWouldMisread_IsRefused()
        {
            var seventeen = new string[17 * 5];
            for (int i = 0; i < seventeen.Length; i++) seventeen[i] = "#000000";
            Assert.IsNotNull(TerrainTexArrayBuilder.PackRampRow(seventeen, 0f, 1f, 1f, new Color[RampWidth], 0),
                "seventeen palettes were packed: the seventeenth's first band is where the shader reads the parameters");
            Assert.IsNotNull(TerrainTexArrayBuilder.PackRampRow(new[] { "#000000", "#000000" }, 0f, 1f, 1f,
                                                                new Color[RampWidth], 0),
                "two colours were packed as a palette of five");
            Assert.IsNotNull(TerrainTexArrayBuilder.PackRampRow(
                                 new[] { "#00000g", "#000000", "#000000", "#000000", "#000000" }, 0f, 1f, 1f,
                                 new Color[RampWidth], 0),
                "a colour that is not #rrggbb was packed");
        }

        // ============================ THE SURFACE'S BINDING ============================

        [Test]
        public void MapsThatFitTheDetailArray_TurnTheRelightOn_AndReachTheGround()
        {
            RequireTextureArrays();
            var detail = NewArray(Tile, 3);
            Texture2DArray normal = NewArray(Tile, 3), light = NewArray(Tile, 3), marks = NewArray(Tile, 3);
            var ramp = Ramp(RampWidth, 3);
            var go = new GameObject("RelightFits");
            try
            {
                go.SetActive(false);
                var splat = go.AddComponent<TerrainSplatSurface>();
                splat.ConfigureDetail(detail, null);
                splat.ConfigureRelight(normal, light, marks, ramp);
                go.SetActive(true);

                var mpb = Block(go);
                Assert.AreEqual(1f, mpb.GetFloat("_RelightLoaded"), "maps that fit did not turn the relight on");
                Assert.AreSame(normal, mpb.GetTexture("_RelightNormal"), "the normal map did not reach the ground");
                Assert.AreSame(light, mpb.GetTexture("_RelightLight"), "the light map did not reach the ground");
                Assert.AreSame(marks, mpb.GetTexture("_RelightDetail"), "the detail map did not reach the ground");
                Assert.AreSame(ramp, mpb.GetTexture("_RelightRamp"), "the ramp did not reach the ground");
            }
            finally { Destroy(go, detail, normal, light, marks, ramp); }
        }

        [Test]
        public void MapsTakenAway_TurnTheRelightBackOff()
        {
            RequireTextureArrays();
            var detail = NewArray(Tile, 3);
            Texture2DArray normal = NewArray(Tile, 3), light = NewArray(Tile, 3), marks = NewArray(Tile, 3);
            var ramp = Ramp(RampWidth, 3);
            var go = new GameObject("RelightTakenAway");
            try
            {
                go.SetActive(false);
                var splat = go.AddComponent<TerrainSplatSurface>();
                splat.ConfigureDetail(detail, null);
                splat.ConfigureRelight(normal, light, marks, ramp);
                go.SetActive(true);
                Assert.AreEqual(1f, Block(go).GetFloat("_RelightLoaded"), "premise: maps that fit turn the relight on");

                splat.ConfigureRelight(null, null, null, null);
                splat.enabled = false;
                splat.enabled = true;   // OnEnable pushes again, over the block it pushed before

                Assert.AreEqual(0f, Block(go).GetFloat("_RelightLoaded"),
                    "the relight stayed on after its maps were taken away: the shader would load unbound maps");
            }
            finally { Destroy(go, detail, normal, light, marks, ramp); }
        }

        [Test]
        public void MapsThatDoNotFit_AreRefused_AndSaidOnce()
        {
            RequireTextureArrays();
            var detail = NewArray(Tile, 3);
            Texture2DArray normal = NewArray(Tile, 3), light = NewArray(Tile, 3), marks = NewArray(Tile, 3);
            Texture2DArray shallow = NewArray(Tile, 2), narrow = NewArray(Tile / 2, 3);
            Texture2D ramp = Ramp(RampWidth, 3), shortRamp = Ramp(RampWidth, 2), thinRamp = Ramp(RampWidth - 1, 3);
            var go = new GameObject("RelightMisfits");
            int warnings = 0;
            void Count(string message, string stack, LogType type)
            {
                if (type == LogType.Warning && message.Contains("relight maps do not fit")) warnings++;
            }
            try
            {
                go.SetActive(false);
                var splat = go.AddComponent<TerrainSplatSurface>();
                splat.ConfigureDetail(detail, null);

                splat.ConfigureRelight(shallow, light, marks, ramp);
                Assert.IsFalse(splat.RelightLoaded, "a normal map one slice short of the detail array was accepted");
                splat.ConfigureRelight(normal, narrow, marks, ramp);
                Assert.IsFalse(splat.RelightLoaded, "a light map of 128-texel slices was accepted");
                splat.ConfigureRelight(normal, light, null, ramp);
                Assert.IsFalse(splat.RelightLoaded, "three maps of four were accepted");
                splat.ConfigureRelight(normal, light, marks, thinRamp);
                Assert.IsFalse(splat.RelightLoaded, "a ramp 80 wide, with no column for the parameters, was accepted");
                splat.ConfigureRelight(normal, light, marks, shortRamp);
                Assert.IsFalse(splat.RelightLoaded, "a ramp one row short of the detail array was accepted");

                Application.logMessageReceived += Count;
                go.SetActive(true);
                splat.enabled = false;
                splat.enabled = true;
                Application.logMessageReceived -= Count;

                Assert.AreEqual(0f, Block(go).GetFloat("_RelightLoaded"), "maps that do not fit turned the relight on");
                Assert.AreEqual(1, warnings, "a refused binding should be said once, not on every push, and not never");
            }
            finally
            {
                Application.logMessageReceived -= Count;
                Destroy(go, detail, normal, light, marks, shallow, narrow, ramp, shortRamp, thinRamp);
            }
        }

        // ============================ helpers ============================

        static int Define(string src, string name)
        {
            Match m = Regex.Match(src, @"#define\s+" + name + @"\s+(\d+)");
            Assert.IsTrue(m.Success, $"the include no longer defines {name}");
            return int.Parse(m.Groups[1].Value);
        }

        static void RequireTextureArrays()
        {
            if (!SystemInfo.supports2DArrayTextures)
                Assert.Ignore("this graphics device has no texture arrays, so the relight maps cannot be made here.");
        }

        static Texture2DArray NewArray(int size, int depth) =>
            new Texture2DArray(size, size, depth, TextureFormat.RGBA32, false, true) { name = "RelightBindingProbe" };

        static Texture2D Ramp(int width, int rows) =>
            new Texture2D(width, rows, TextureFormat.RGBA32, false, true) { name = "RelightBindingRamp" };

        static MaterialPropertyBlock Block(GameObject host)
        {
            var renderer = host.GetComponentInChildren<MeshRenderer>(true);
            Assert.IsNotNull(renderer, "the surface built no ground quad, so it pushed nothing to read");
            var mpb = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(mpb);
            return mpb;
        }

        static void Destroy(params Object[] objects)
        {
            foreach (Object o in objects)
                if (o != null) Object.DestroyImmediate(o);
        }
    }
}
