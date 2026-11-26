using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The clifftop's turf lip is a recipe, not hand-drawn pixels: the committed strip is exactly what its Def bakes
    /// to, it is imported so one of its texels is one texel on screen, and it leaves the meadow behind the brow
    /// line alone (owner 11 26: no mixels, and grass that hangs over the ledge).
    /// </summary>
    public class CliffTurfLipBakeTests
    {
        static CliffTurfLipDef[] Lips() =>
            AssetDatabase.FindAssets("t:" + nameof(CliffTurfLipDef))
                .Select(g => AssetDatabase.LoadAssetAtPath<CliffTurfLipDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null).ToArray();

        [Test]
        public void TheCommittedStrip_IsWhatItsDefBakesTo()
        {
            CliffTurfLipDef[] lips = Lips();
            Assert.IsNotEmpty(lips, "no CliffTurfLipDef asset");
            foreach (CliffTurfLipDef def in lips)
                using (IRigScriptHost host = CliffTurfLipBaker.CreateHost(def))
                {
                    byte[] rgba = CliffTurfLipBaker.Render(host, def, out int w, out int h);
                    Assert.IsTrue(CliffTurfLipBaker.MatchesCommitted(def, rgba, w, h),
                        $"{def.OutputPath} is not what {def.Id} bakes to: re-run Hidden Harbours/Dev/Bake Cliff Turf Lips and commit");
                }
        }

        [Test]
        public void TheStrip_IsImportedTexelForTexel()
        {
            foreach (CliffTurfLipDef def in Lips())
            {
                StringAssert.StartsWith(CliffTurfLipDef.IdPrefix, def.Id);
                Assert.IsTrue(def.IsDrawable, $"{def.Id} has no strip");
                Assert.AreEqual(def.StripTexels.x, def.Strip.width, $"{def.Id}: the strip was resampled on import");
                Assert.AreEqual(def.StripTexels.y, def.Strip.height, $"{def.Id}: the strip was resampled on import");
                Assert.AreEqual(FilterMode.Point, def.Strip.filterMode, $"{def.Id}: a filtered strip smears its texels");
                Assert.AreEqual(1, def.Strip.mipmapCount, $"{def.Id}: a mip would draw a second texel size");
                Assert.AreEqual(TextureWrapMode.Repeat, def.Strip.wrapModeU);
                Assert.AreEqual(TextureWrapMode.Clamp, def.Strip.wrapModeV);
            }
        }

        [Test]
        public void TheStrip_LeavesTheMeadowAlone_AndHangsBelowTheLine()
        {
            foreach (CliffTurfLipDef def in Lips())
                using (IRigScriptHost host = CliffTurfLipBaker.CreateHost(def))
                {
                    byte[] rgba = CliffTurfLipBaker.Render(host, def, out int w, out int h);
                    int line = def.LineRow, above = 0, aboveFringe = 0, lineRow = 0, hang = 0;
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            if (rgba[(y * w + x) * 4 + 3] == 0) continue;
                            if (y < line - def.Fringe.y) aboveFringe++;
                            else if (y < line) above++;
                            else if (y == line) lineRow++;
                        }
                    // Columns whose grass reaches well below the mat: the hanging clumps.
                    int reach = line + def.Sod.y + def.Short.y + 2;
                    for (int x = 0; x < w; x++)
                    {
                        int i = (reach * w + x) * 4;
                        if (rgba[i + 3] == 255 && rgba[i + 1] > rgba[i]) hang++;
                    }
                    Assert.AreEqual(0, aboveFringe, $"{def.Id}: the strip paints over the meadow above its fringe");
                    Assert.Less(above, (line * w) / 2, $"{def.Id}: the fringe is a solid band, not blades over the edge");
                    Assert.Greater(lineRow, w * (1f - def.TornShare * 2f - 0.1f), $"{def.Id}: the mat is mostly missing at the edge");
                    Assert.Greater(hang, w / 20, $"{def.Id}: almost no grass hangs below the mat");
                }
        }
    }
}
