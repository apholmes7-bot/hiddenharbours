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

        /// <summary>Owner 11 27: a bank with no wall lost its height. A lip with a rock riser paints an upright face
        /// under its soil, nearly every column of it, lit at the top and darker at the foot; a lip with none (a wall's)
        /// paints no rock there.</summary>
        [Test]
        public void ARiser_PaintsAnUprightFace_LitAtTheTop_DarkAtTheFoot()
        {
            int risers = 0;
            foreach (CliffTurfLipDef def in Lips())
            {
                if (def.RockRiser.y <= 0) continue;
                risers++;
                using (IRigScriptHost host = CliffTurfLipBaker.CreateHost(def))
                {
                    byte[] rgba = CliffTurfLipBaker.Render(host, def, out int w, out int h);
                    // Column by column: the riser is the run of the def's own rock colours (the soil and the grass
                    // never use them). Nearly every column has one nearly as tall as the shortest riser, lit in its
                    // upper third and darker in its lower third.
                    var rock = new System.Collections.Generic.HashSet<int>();
                    foreach (string hex in def.Rock)
                        if (ColorUtility.TryParseHtmlString(hex, out Color c))
                        {
                            Color32 c32 = c;
                            rock.Add(c32.r << 16 | c32.g << 8 | c32.b);
                        }
                    int tall = 0, litOverDark = 0;
                    for (int x = 0; x < w; x++)
                    {
                        int top = -1, foot = -1;
                        for (int y = 0; y < h; y++)
                        {
                            int i = (y * w + x) * 4;
                            if (rgba[i + 3] != 255 || !rock.Contains(rgba[i] << 16 | rgba[i + 1] << 8 | rgba[i + 2])) continue;
                            if (top < 0) top = y;
                            foot = y;
                        }
                        if (top < 0 || foot - top + 1 < def.RockRiser.x - 8) continue;
                        tall++;
                        double Luma(int y0, int y1)
                        {
                            double sum = 0; int n = 0;
                            for (int y = y0; y <= y1; y++)
                            {
                                int i = (y * w + x) * 4;
                                if (rgba[i + 3] != 255 || !rock.Contains(rgba[i] << 16 | rgba[i + 1] << 8 | rgba[i + 2])) continue;
                                sum += 0.2126 * rgba[i] + 0.7152 * rgba[i + 1] + 0.0722 * rgba[i + 2]; n++;
                            }
                            return n > 0 ? sum / n : 0;
                        }
                        int third = (foot - top + 1) / 3;
                        if (Luma(top, top + third - 1) > Luma(foot - third + 1, foot) + 8) litOverDark++;
                    }
                    Assert.Greater(tall, w * 0.9f, $"{def.Id}: the riser has holes or is short where the ledge shows through");
                    Assert.Greater(litOverDark, tall * 0.9f, $"{def.Id}: the riser is not lit at its top and dark at its foot");
                }
            }
            Assert.Greater(risers, 0, "no lip paints a riser: a bank with no wall reads flat");
        }

        /// <summary>Owner 11 27: the stepped walls lost the flat grass ledge between their tiers. A lip with a terrace
        /// paints a band of turf under its riser, whole across the strip.</summary>
        [Test]
        public void ATerrace_PaintsAWholeBandOfTurf_UnderTheRiser()
        {
            int terraces = 0;
            foreach (CliffTurfLipDef def in Lips())
            {
                if (def.TerraceRows.y <= 0) continue;
                terraces++;
                using (IRigScriptHost host = CliffTurfLipBaker.CreateHost(def))
                {
                    byte[] rgba = CliffTurfLipBaker.Render(host, def, out int w, out int h);
                    // Under every column's riser foot, above every column's terrace edge.
                    int y = def.LineRow + def.Apron.y - 5 + def.RockRiser.y + 5;
                    Assert.Less(y, def.LineRow + def.Apron.x - 5 + def.RockRiser.x - 1 + def.TerraceRows.x - 2,
                                $"{def.Id}: the terrace is too short to test");
                    int green = 0;
                    for (int x = 0; x < w; x++)
                    {
                        int i = (y * w + x) * 4;
                        if (rgba[i + 3] == 255 && rgba[i + 1] > rgba[i]) green++;
                    }
                    Assert.Greater(green, w * 0.85f, $"{def.Id}: the terrace has holes where the face shows through");
                }
            }
            Assert.Greater(terraces, 0, "no lip paints a terrace: the stepped walls read as one ragged mass");
        }
    }
}
