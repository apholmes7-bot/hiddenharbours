using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// Pins down what a rig's <c>pivot</c> numbers MEAN, and therefore which of the repo's two
    /// pivot formulas is right for the boat and character bakes. The full write-up is ADR 0026;
    /// this file is the executable half of it.
    ///
    /// <para>The question these tests close: <c>RigGeometry.UnityNormalisedPivot</c> computes
    /// <c>(H − pivotY)/H</c>, while the tree bake computes <c>pad/cellH = (h − 1 − pivotY)/h</c>.
    /// Those differ by exactly one row, so at most one of them can be right FOR A GIVEN QUANTITY.
    /// The answer is that they convert different quantities and both are right — but only after
    /// establishing, from the rigs themselves, that a boat/character <c>pivot</c> is a CONTINUOUS
    /// cell-corner coordinate rather than a pixel index.</para>
    ///
    /// <para>Two independent lines of evidence, one arithmetic and one measured from pixels, are
    /// asserted below. They must agree; if a future change makes them disagree, something has moved
    /// in the rigs and the bake is no longer trustworthy.</para>
    /// </summary>
    public class RigPivotConventionTests
    {
        /// <summary>
        /// Every rig in the catalog that exposes standard <c>W/H/pivot</c> geometry, i.e. everything
        /// <c>RigCatalog.Install</c> can load. (The item and kit rigs — crustacean, shellfish,
        /// catchKit, bucket — expose other shapes and need <c>InstallModule</c>, so they have no
        /// <c>pivot</c> for this question to apply to.)
        /// </summary>
        static readonly string[] StandardGeometryRigs =
        {
            "punt", "lobsterBoat", "character", "fish", "rod", "bobber", "fishTote",
            "house", "wharfBuilding",
        };

        // ---- Evidence 1: arithmetic — every rig centres its pivot EXACTLY ----------------------

        /// <summary>
        /// <b>The rigs put <c>pivot.x</c> at exactly <c>W/2</c>, and every cell is an even number of
        /// pixels wide.</b> That single fact settles the convention, because the two readings put
        /// the cell's true centre in different places:
        ///
        /// <list type="bullet">
        ///   <item>As a CONTINUOUS coordinate measured from the cell's left EDGE, dead centre is
        ///   <c>W/2</c> — an integer. The rigs write exactly that.</item>
        ///   <item>As a 0-based column INDEX, dead centre is <c>(W − 1)/2</c> — a half-integer for
        ///   every even <c>W</c>, e.g. 91.5 for the punt's 184. No rig writes that, and none could
        ///   write it as the integer they do write.</item>
        /// </list>
        ///
        /// <para>Were <c>pivot</c> a pixel index, every rig in the repo would be laterally
        /// off-centre by half a pixel — including the character, whose pivot is documented as
        /// "ground contact UNDER the character origin", and every hull, whose pivot is documented as
        /// on the centreline. So the pivot is a cell-corner continuous coordinate, and the y term
        /// (which comes out of the very same <c>projVert</c> expression, in the same units:
        /// <c>sx = cx + xr·S</c> beside <c>sy = cy − (…)·S</c>) is one too.</para>
        ///
        /// <para><b>Therefore <c>(H − pivotY)/H</c> is exact and <c>(H − 1 − pivotY)/H</c> would be a
        /// genuine one-row error.</b></para>
        /// </summary>
        [Test]
        public void EveryStandardRig_PutsItsPivotAtExactlyHalfTheCellWidth()
        {
            var report = new StringBuilder();
            foreach (var key in StandardGeometryRigs)
            {
                using var host = RigScriptHostFactory.Create();
                var geo = RigCatalog.Install(host, RigCatalog.Get(key));

                report.AppendLine($"{key,-14} W={geo.Width,5}  pivotX={geo.PivotX,7}  " +
                                  $"W/2={geo.Width / 2.0,7}  (W−1)/2={(geo.Width - 1) / 2.0,7}");

                Assert.AreEqual(0, geo.Width % 2,
                    $"{key}: cell width {geo.Width} is ODD, so W/2 and (W−1)/2 are no longer an " +
                    "integer/half-integer pair and this argument does not apply. Re-derive the " +
                    "convention for this rig before trusting its pivot.");

                Assert.AreEqual(geo.Width / 2.0, geo.PivotX, 1e-9,
                    $"{key}: pivotX is {geo.PivotX}, not W/2 = {geo.Width / 2.0}. Every rig in the " +
                    "repo has centred its pivot exactly, which is what proves the pivot is a " +
                    "continuous cell-corner coordinate (ADR 0026). A rig that breaks that pattern " +
                    "invalidates the proof — do not just relax this assert.");

                // The pivot must land dead centre once converted, which only the corner reading does.
                Assert.AreEqual(0.5f, geo.UnityNormalisedPivot.x, 1e-6f,
                    $"{key}: normalised pivot.x is not 0.5 — the artwork would sit off-centre.");
            }

            TestContext.WriteLine(report.ToString());
        }

        // ---- Evidence 2: measured — mirror the rendered silhouette ----------------------------

        /// <summary>
        /// The same conclusion read off RENDERED PIXELS rather than inferred from the source, in the
        /// spirit of <c>RigAzimuthProbe</c>: at a bow-on or stern-on heading a hull's silhouette is
        /// symmetric about its centreline, so mirroring the alpha mask about the true axis
        /// reproduces it and mirroring about an axis half a pixel off does not.
        ///
        /// <para>Rigs that turn out to carry an asymmetric fitting ABSTAIN (the probe reports
        /// indecisive) rather than failing — but at least <see cref="MinDecisiveSamples"/> samples
        /// must be decisive, so the test cannot pass vacuously, and every decisive sample must agree.</para>
        /// </summary>
        const int MinDecisiveSamples = 2;

        static IEnumerable<(string rig, int dir)> SymmetricHeadingCandidates()
        {
            // dir 0 = bow-on, dir 4 = stern-on. At both the azimuth term contributes no screen-x
            // shear, so a hull that is symmetric port-to-starboard renders symmetric.
            yield return ("punt", 0);
            yield return ("punt", 4);
            yield return ("lobsterBoat", 0);
            yield return ("lobsterBoat", 4);
            yield return ("character", 0);
            yield return ("character", 4);

            // NOT the fish: measured at 35 opaque px, because dir 0/4 points a mackerel at the
            // camera and a fish nose-on is a sliver. Symmetric, but nothing to mirror.
        }

        [Test]
        public void MeasuredFromPixels_ThePivotIsACellCornerCoordinate_NotAPixelIndex()
        {
            var report = new StringBuilder();
            int decisive = 0, cornerVerdicts = 0, indexVerdicts = 0;

            foreach (var (key, dir) in SymmetricHeadingCandidates())
            {
                using var host = RigScriptHostFactory.Create();
                var entry = RigCatalog.Get(key);
                var geo = RigCatalog.Install(host, entry);

                string d = dir.ToString(CultureInfo.InvariantCulture);

                // A rig that cannot be rendered with default opts ABSTAINS rather than collapsing
                // the proof — the sample count below is what keeps that honest.
                RigPivotConventionProbe.Result r;
                try
                {
                    byte[] rgba = host.EvaluateBytes($"{entry.GlobalName}.render({d},{{}})");
                    r = RigPivotConventionProbe.MeasureFromSymmetricRender(
                        rgba, geo.Width, geo.Height, (int)geo.PivotX);
                }
                catch (System.Exception ex)
                {
                    report.AppendLine($"---- {key} dir {dir}: ABSTAINED — {ex.GetType().Name}: {ex.Message}");
                    report.AppendLine();
                    continue;
                }

                // Written out as we go, not just accumulated: an assertion below aborts the test,
                // and the measurement is the whole point of running it.
                string sample = $"---- {key} dir {dir} ({geo.Width}×{geo.Height}, pivotX={geo.PivotX}) ----\n"
                              + r.Report;
                TestContext.WriteLine(sample);
                report.AppendLine(sample);
                report.AppendLine();

                if (!r.IsDecisive) continue;
                decisive++;
                if (r.Origin == PivotOrigin.CellCornerContinuous) cornerVerdicts++; else indexVerdicts++;

                Assert.AreEqual(PivotOrigin.CellCornerContinuous, r.Origin,
                    $"{key} dir {dir}: the rendered silhouette mirrors about the centre of column " +
                    $"{geo.PivotX} rather than about continuous {geo.PivotX}.0, which would mean the " +
                    "pivot is a pixel INDEX and every shared-helper pivot in the repo is a row high. " +
                    $"Read the probe report before acting on this.\n{r.Report}");

                // The independent extreme check must agree with the mirror verdict.
                Assert.AreEqual(2 * (int)geo.PivotX - 1, r.ExtremeSum,
                    $"{key} dir {dir}: silhouette minX+maxX disagrees with the mirror verdict.\n{r.Report}");
            }

            TestContext.WriteLine(report.ToString());

            Assert.GreaterOrEqual(decisive, MinDecisiveSamples,
                $"Only {decisive} of the probe headings were decisive, so this test proved nothing. " +
                $"The rigs may have gained asymmetric fittings; pick new symmetric headings rather " +
                $"than lowering the bar.\n{report}");
            Assert.AreEqual(0, indexVerdicts, $"Mixed verdicts across rigs.\n{report}");
            Assert.Greater(cornerVerdicts, 0);
        }

        // ---- The helper the evidence ratifies -------------------------------------------------

        /// <summary>
        /// Ties the measurement to the shipping formula, and states what the rejected alternative
        /// would have cost. Both boat and character bakes route through this one property.
        /// </summary>
        [Test]
        public void UnityNormalisedPivot_UsesTheCornerFormula_AndTheOneRowLowerVariantWouldDrift()
        {
            using var host = RigScriptHostFactory.Create();
            var geo = RigCatalog.Install(host, RigCatalog.Get("punt"));

            // The shipping formula, restated independently of the property under test.
            float corner = (float)((geo.Height - geo.PivotY) / geo.Height);
            Assert.AreEqual(corner, geo.UnityNormalisedPivot.y, 1e-9f,
                "UnityNormalisedPivot.y is no longer (H − pivotY)/H. ADR 0026 is the argument for " +
                "that formula; if it changed, that ADR needs revisiting first.");

            // The punt's documented identity — the number the README and PuntSheetSliceTests both name.
            Assert.AreEqual(74.0f / 168.0f, geo.UnityNormalisedPivot.y, 1e-6f,
                "The punt's boat origin is (168 − 94)/168 = 0.440476…, the value the hull and the " +
                "wider motor cell must share for the outboard to land on the transom.");

            // The rejected alternative, and its size. Computed in DOUBLE: differencing two ~0.44
            // floats loses about 2.3e-8, which is larger than the quantity under test deserves.
            double cornerD = (geo.Height - geo.PivotY) / geo.Height;
            double oneRowLowerD = (geo.Height - 1 - geo.PivotY) / geo.Height;
            double deltaNorm = cornerD - oneRowLowerD;
            Assert.AreEqual(1.0 / geo.Height, deltaNorm, 1e-12,
                "The two candidate formulas must differ by exactly one row of the cell.");
            TestContext.WriteLine(
                $"corner (H−pivotY)/H = {cornerD:F8}   index-style (H−1−pivotY)/H = {oneRowLowerD:F8}\n" +
                $"difference = 1 row = {deltaNorm:F8} normalised = 1 px = {1.0 / 32.0:F5} m at PPU 32.");
        }

        /// <summary>
        /// <c>CharacterRigBakeTests</c> asserts <c>8f / CellH</c> for the character's normalised
        /// pivot.y. Restated here against the measured convention: the character's pivot (32,80) in
        /// an 88-tall cell converts to (88 − 80)/88 = 8/88 under the corner reading, so that
        /// existing assertion is CONSISTENT with the finding, not a second convention.
        ///
        /// <para>(It is worth being explicit that the existing assert alone could not settle the
        /// question — <c>8f/CellH</c> is just the helper's own formula written out, so it agrees
        /// with the helper by construction. The evidence above is what makes it right.)</para>
        /// </summary>
        [Test]
        public void CharacterPivot_IsTheSameEightRowsTheBakeTestsAlreadyAssert()
        {
            using var host = RigScriptHostFactory.Create();
            var geo = RigCatalog.Install(host, RigCatalog.Get("character"));

            Assert.AreEqual(64, geo.Width);
            Assert.AreEqual(88, geo.Height);
            Assert.AreEqual(80.0, geo.PivotY, 1e-9);
            Assert.AreEqual(8f / 88f, geo.UnityNormalisedPivot.y, 1e-6f,
                "The character's ground contact must stay 8 rows above the cell bottom — the value " +
                "CharacterRigBakeTests and CharacterSheetSlicer both bake in.");
        }

        // ---- The tree's deliberate divergence, as a tripwire -----------------------------------

        /// <summary>
        /// The tree bake uses <c>pad/cellH</c>, one row LOWER than the shared helper, and that is
        /// correct — for the tree. This test exists so a future "let's unify the two pivot
        /// formulas" change has to read ADR 0026 first, in EITHER direction.
        ///
        /// <para>They convert different quantities. A boat's pivot is a projected geometric POINT
        /// (the keel bottom), landing on a continuous coordinate. A tree's is a chosen ROW — the rig
        /// computes <c>pivotY = Math.ceil(MG − top)</c> and exports <c>pad = h − 1 − pivotY</c>
        /// itself — and <c>pad/h</c> puts the pivot on that row's BOTTOM edge, which is what the
        /// wind shader's <c>_TrunkAnchor</c> needs so the lowest row of near-root flare stays inside
        /// the planted band.</para>
        ///
        /// <para>Numbers are Red Spruce's, the worked example in the design doc: <c>uv.y = 20/166 =
        /// 0.120</c>.</para>
        /// </summary>
        [Test]
        public void TreePivot_IsDeliberatelyOneRowLowerThanTheSharedHelper()
        {
            const int cellH = 166, pad = 20;
            const int pivotY = cellH - 1 - pad;   // 145 — the rig's own relation, inverted

            var e = new TreeKitCatalog.Entry
            {
                cellW = 100, cellH = cellH, pivotX = 50, pivotY = pivotY, nearFlarePad = pad,
            };

            Assert.AreEqual(pad / (float)cellH, TreeKitCatalog.NormalizedPivot(e).y, 1e-6f,
                "The tree pivot must stay pad/cellH — it doubles as the wind shader's _TrunkAnchor.");
            Assert.AreEqual(20f / 166f, TreeKitCatalog.NormalizedPivot(e).y, 1e-6f,
                "Red Spruce's published anchor is 20/166 = 0.120.");

            float shared = (cellH - pivotY) / (float)cellH;
            Assert.AreEqual(1f / cellH, shared - TreeKitCatalog.NormalizedPivot(e).y, 1e-6f,
                "The two formulas must differ by exactly one row. If this changed, one of them was " +
                "'unified' into the other — read ADR 0026: they convert different quantities and " +
                "the difference is intentional.");
        }
    }
}
