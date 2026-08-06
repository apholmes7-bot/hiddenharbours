using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Guards the flower shader's DRAWN POSE SELECT — the line that walks a flower through the art director's
    /// four hand-drawn sway columns.
    ///
    /// <para><b>The bug this fixture exists for (found 2026-08-06, shipped since #215).</b> The pose select used
    /// to read <c>frac(IN.uv.x + k / _Cols)</c>. That looks like a correct modulo, and it IS correct for a single
    /// point — but it runs in the VERTEX shader and the result is INTERPOLATED across the quad, so the wrap has
    /// to land the same way for every vertex or the interpolator sweeps between two unrelated columns. An
    /// authored column-0 flower spans uv.x exactly 0.00..0.25, so at k = 3 the left vertex got
    /// <c>frac(0.75) = 0.75</c> and the right vertex got <c>frac(1.00) = 0.00</c>: the quad then interpolated
    /// BACKWARDS across the entire texture and drew a squeezed, reversed strip of all four poses inside one
    /// flower. A single bud became a "multi-bloom" for a quarter of every cycle — ~1.4 s in every 5.7 s at idle —
    /// which is exactly the owner's long-standing "flowers swap sprites every few seconds". k = 0, 1, 2 never
    /// wrapped, so three poses looked right and only the fourth was garbage, which is why it read as an
    /// intermittent swap rather than a broken shader.</para>
    ///
    /// <para><b>Why these tests and not a rendered-pixel test.</b> CI has no graphics device, so nothing here can
    /// rasterise a quad. Instead the fixture holds the two things that actually make the bare add safe: the RULE
    /// is continuous across the quad (the maths, mirrored below and negative-controlled against the old wrapping
    /// rule so the fixture is proven able to catch the regression), and the SHIPPED SHADER still uses that rule
    /// (a source guard, so the mirror cannot drift from the HLSL it claims to model). The sheet-grid half of the
    /// precondition — cells exactly 1/_Cols wide, grid-sliced, no SpriteAtlas — is held by
    /// <see cref="FlowerCatalogTests"/> and deliberately not duplicated here.</para>
    /// </summary>
    public class FlowerPoseSelectTests
    {
        private const string FlowerShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursFlower.shader";

        /// <summary>
        /// The SHIPPED pose-select rule, mirrored from HiddenHarboursFlower.shader. Kept in lockstep with the HLSL
        /// by <see cref="FlowerShader_PoseSelect_DoesNotWrapTheInterpolatedU"/>.
        /// </summary>
        private static float PoseU(float u, int k, int cols) => u + (float)k / Mathf.Max(cols, 1);

        /// <summary>The rule as it SHIPPED BROKEN — here only as the negative control.</summary>
        private static float PoseU_Wrapping(float u, int k, int cols)
        {
            float shifted = u + (float)k / Mathf.Max(cols, 1);
            return shifted - Mathf.Floor(shifted);       // HLSL frac()
        }

        /// <summary>
        /// The heart of it: for every pose k, the quad's two u edges must stay in order and one cell apart. A
        /// backwards or over-wide span means the rasteriser is sweeping across columns inside one flower.
        /// </summary>
        [Test]
        public void PoseSelect_KeepsTheQuadsUEdgesInOrderAndExactlyOneCellApart([NUnit.Framework.Range(0, 3)] int k)
        {
            int cols = ShippedCols();
            float cellW = 1f / cols;
            // An authored flower is always column 0 (see LoadNeutral_IsColumnZero...), so its quad spans 0..1/cols.
            float left = PoseU(0f, k, cols);
            float right = PoseU(cellW, k, cols);

            Assert.Greater(
                right, left,
                $"Pose {k}: the quad's right u ({right}) is not beyond its left u ({left}). The rasteriser " +
                "interpolates between these, so a non-increasing pair sweeps BACKWARDS through the sheet and " +
                "draws several poses squeezed into one flower — the 'flowers swap sprites every few seconds' bug. " +
                "Do not wrap the pose select with frac(); see HiddenHarboursFlower.shader.");
            Assert.AreEqual(
                cellW, right - left, 1e-6f,
                $"Pose {k}: the quad spans {right - left} of the sheet but one cell is {cellW}. The pose select " +
                "must shift by WHOLE cells and change the span not at all.");
        }

        /// <summary>The shift must land on pose k of the SAME ROW, and never read off the end of the sheet.</summary>
        [Test]
        public void PoseSelect_LandsOnPoseK_AndStaysInsideTheSheet([NUnit.Framework.Range(0, 3)] int k)
        {
            int cols = ShippedCols();
            float cellW = 1f / cols;
            float left = PoseU(0f, k, cols);
            float right = PoseU(cellW, k, cols);

            Assert.AreEqual(
                k * cellW, left, 1e-6f, $"Pose {k} should start at column {k} (u = {k * cellW}), not {left}.");
            Assert.GreaterOrEqual(left, 0f, $"Pose {k} reads before the start of the sheet.");
            Assert.LessOrEqual(
                right, 1f + 1e-6f,
                $"Pose {k} reads past the end of the sheet (u = {right}). The bare add is only safe because " +
                "authoring places COLUMN 0; a non-zero authored column needs the sprite's own column as per-quad " +
                "data, not a frac() here.");
        }

        /// <summary>
        /// NEGATIVE CONTROL. Proves this fixture can actually catch the regression: the old wrapping rule really
        /// does invert the quad's u edges at the last pose. Without this, the tests above could be passing for
        /// some reason other than the fix.
        /// </summary>
        [Test]
        public void TheOldWrappingRule_InvertsTheQuad_AtTheLastPose()
        {
            int cols = ShippedCols();
            float cellW = 1f / cols;
            int lastK = cols - 1;

            float left = PoseU_Wrapping(0f, lastK, cols);
            float right = PoseU_Wrapping(cellW, lastK, cols);

            Assert.Less(
                right, left,
                "The old frac()-wrapped rule was expected to invert the quad at the last pose (that WAS the bug). " +
                "If it no longer does, this fixture's negative control has stopped controlling for anything — " +
                "work out why before trusting the tests above.");
        }

        /// <summary>
        /// SOURCE GUARD — keeps the mirror above honest. The shipped HLSL must still assign OUT.uv without
        /// wrapping the interpolated u.
        /// </summary>
        [Test]
        public void FlowerShader_PoseSelect_DoesNotWrapTheInterpolatedU()
        {
            string full = Path.Combine(
                Path.GetDirectoryName(Application.dataPath) ?? string.Empty, FlowerShaderPath);
            Assert.IsTrue(
                File.Exists(full),
                $"Could not read the flower shader at '{FlowerShaderPath}' — that leaves the pose select " +
                "UNGUARDED. Treat it as a failure, not a pass.");

            var offenders = new List<string>();
            bool sawAssignment = false;
            int lineNo = 0;
            foreach (string raw in File.ReadAllLines(full))
            {
                lineNo++;
                string line = raw.Trim();
                if (line.StartsWith("//")) continue;             // the ⚠ comment names frac() on purpose
                if (!line.Contains("OUT.uv")) continue;
                if (!line.Contains("=")) continue;
                sawAssignment = true;
                if (line.Contains("frac("))
                    offenders.Add($"{FlowerShaderPath}:{lineNo}  {line}");
            }

            Assert.IsTrue(
                sawAssignment,
                "Found no 'OUT.uv = ...' assignment in the flower shader. Either the pose select was renamed or " +
                "removed — this guard is now watching nothing, so fix the guard rather than deleting it.");
            Assert.IsEmpty(
                offenders,
                "The flower shader's pose select wraps the interpolated u with frac() again. That is the " +
                "'flowers swap sprites every few seconds' bug: frac() runs PER VERTEX, so at the last pose the " +
                "quad's right edge wraps to 0 while its left edge does not, and the flower renders as a squeezed " +
                "reversed strip of every pose. Shift by whole cells instead:\n  " + string.Join("\n  ", offenders));
        }

        /// <summary>
        /// The precondition the bare add rests on: "neutral" — what every authoring path places
        /// (DecorPrefabBuilder, StPetersWoodsPlanter and the Flower Paint Tool all call it) — is COLUMN 0.
        /// </summary>
        [Test]
        public void LoadNeutral_IsColumnZero_ForEveryTierThatShips()
        {
            var problems = new List<string>();

            foreach (string stem in FlowerCatalog.SheetStems())
            {
                if (!FlowerCatalog.TrySplit(stem, out _, out FlowerCatalog.Tier tier)) continue;
                var grid = FlowerCatalog.GridFor(tier);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(FlowerCatalog.SheetPath(stem));
                if (tex == null) continue;                       // not imported; FlowerCatalogTests owns that case

                for (int row = 0; row < grid.Rows; row++)
                {
                    var s = FlowerCatalog.LoadNeutral(stem, tier, row);
                    if (s == null) continue;
                    if (!Mathf.Approximately(s.rect.x, 0f))
                        problems.Add($"'{s.name}' (row {row}) sits at rect.x {s.rect.x}, not column 0.");
                }
            }

            Assert.IsEmpty(
                problems,
                "A flower's 'neutral' sprite is not column 0. The shader's pose select shifts FORWARD from the " +
                "authored column without wrapping, so a non-zero authored column would read off the end of the " +
                "sheet. Either keep authoring at column 0 or give the shader the sprite's own column as per-quad " +
                "data:\n  " + string.Join("\n  ", problems));
        }

        /// <summary>_Cols as the shipped materials actually set it — never the constant 4 written by hand.</summary>
        private static int ShippedCols()
        {
            var mat = FlowerCatalog.MaterialFor(FlowerCatalog.Tier.Single);
            Assert.IsNotNull(mat, "Flower_Single.mat is missing — cannot check the pose select against shipped data.");
            int cols = Mathf.RoundToInt(mat.GetFloat("_Cols"));
            Assert.AreEqual(
                FlowerCatalog.GridFor(FlowerCatalog.Tier.Single).Cols, cols,
                "Flower_Single.mat's _Cols disagrees with the catalog's tier grid (FlowerMaterialTests owns that " +
                "assertion in full) — the pose-select checks below would be measuring the wrong sheet.");
            return cols;
        }
    }
}
