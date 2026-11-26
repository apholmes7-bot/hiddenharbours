using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The boards' coursed cliff faces are the px kit's own <c>face()</c> with the style's painter, not hand-drawn
    /// pixels: a committed style face is exactly what its Def bakes to, the rock's own painter is put back after
    /// every style render (so the kit faces bake as shipped), and the style really draws a different pattern from
    /// the kit face it stands in for while packing the same palette rows.
    /// </summary>
    public class CliffFaceStyleBakeTests
    {
        const string StylesFolder = "Assets/_Project/Data/Terrain/CliffStyles";
        const string PG = CliffCatalog.PxRigGlobalName;

        static CliffFaceStyleDef Board() =>
            AssetDatabase.FindAssets("t:" + nameof(CliffFaceStyleDef), new[] { StylesFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<CliffFaceStyleDef>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(s => s != null && s.Id == "cliffstyle.board_coursed");

        [TestCase("S", 0)]
        [TestCase("E", 2)]
        public void ACommittedStyleFace_IsWhatItsDefBakesTo(string aspect, int batter)
        {
            CliffFaceStyleDef style = Board();
            Assert.IsNotNull(style, "no cliffstyle.board_coursed asset");
            using (IRigScriptHost host = CliffFaceStyleBaker.CreateHost(style))
            {
                CliffFaceStyleBaker.Render(host, style, aspect, CliffCatalog.Batters[batter], out int w, out int h);
                foreach (string channel in CliffCatalog.PxLiveChannels)
                {
                    string path = CliffFaceStyleBaker.FacePath(style, aspect, batter, channel);
                    Assert.IsTrue(CliffFaceStyleBaker.MatchesCommitted(path, CliffFaceStyleBaker.Channel(host, channel), w, h),
                        $"{path} is not what {style.Id} bakes to: re-run Hidden Harbours/Dev/Bake Cliff Face Styles and commit");
                }
            }
        }

        [Test]
        public void AStyleRender_PutsTheRocksOwnPainterBack()
        {
            CliffFaceStyleDef style = Board();
            Assert.IsNotNull(style);
            using (IRigScriptHost host = CliffFaceStyleBaker.CreateHost(style))
            {
                host.Execute($"globalThis.__hhKeep = {PG}.DEF['{style.Rock}'].paint;");
                CliffFaceStyleBaker.Render(host, style, "S", CliffCatalog.Batters[0], out _, out _);
                Assert.IsTrue(host.EvaluateBool($"{PG}.DEF['{style.Rock}'].paint === globalThis.__hhKeep"),
                    "the style's painter was left installed: every later kit face would bake in the style");
            }
        }

        [Test]
        public void TheStyleFace_DrawsADifferentPattern_InTheSamePaletteRows()
        {
            CliffFaceStyleDef style = Board();
            Assert.IsNotNull(style);
            CliffStyleFace face = style.Faces.First(f => f.Base != null && f.Base.name == "Sandstone_S_index");
            Color32[] kit = ReadPixels(face.Base), mine = ReadPixels(face.Index);
            Assert.AreEqual(kit.Length, mine.Length);
            int differ = 0, n = kit.Length;
            var kitRows = new System.Collections.Generic.HashSet<byte>();
            var myRows = new System.Collections.Generic.HashSet<byte>();
            for (int i = 0; i < n; i++)
            {
                // R names the palette row, G the band: a different pattern changes either.
                if (kit[i].r != mine[i].r || kit[i].g != mine[i].g) differ++;
                kitRows.Add(kit[i].r);
                myRows.Add(mine[i].r);
            }
            Assert.Greater(differ / (float)n, 0.25f, "the style face is nearly the kit face: the painter swap did not take");
            Assert.IsTrue(myRows.IsSubsetOf(kitRows.Concat(new byte[] { 0, 1, 2, 3, 4, 5, 18, 19, 20 })),
                "the style face names palette rows the sandstone kit face does not: it would draw another rock's colours");
        }

        /// <summary>The PNG's pixels by channel name (LoadImage may hand back ARGB32, so never read raw bytes).</summary>
        static Color32[] ReadPixels(Texture2D t)
        {
            var copy = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                Assert.IsTrue(copy.LoadImage(File.ReadAllBytes(Path.Combine(RigCatalog.RepoRoot, AssetDatabase.GetAssetPath(t)))));
                return copy.GetPixels32();
            }
            finally { Object.DestroyImmediate(copy); }
        }
    }
}
