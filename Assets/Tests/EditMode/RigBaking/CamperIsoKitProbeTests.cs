using System.IO;
using NUnit.Framework;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE CAMPER ISO KIT DROP — is the rig what its README says, and what will bite the baker?</b>
    ///
    /// <para>A probe, not an acceptance suite. The 2026-08-16 drop is the first PARKED DWELLING in the
    /// repo and it ships <b>no harness and no reference sheets</b> — every other rig drop this project
    /// has taken arrived with both, and they are what a bake is normally checked against. With no
    /// committed image to compare to, the README's own measured numbers are the only control there is,
    /// so they are reproduced here in the repo's own V8 rather than taken on trust.</para>
    ///
    /// <para>What it settles:</para>
    /// <list type="bullet">
    ///   <item>All sixteen per-facing crop boxes in the README are the rig's ACTUAL output, to the
    ///   pixel — so the drop is internally faithful and the numbers can be used to pack sheets.</item>
    ///   <item><c>render(dir)</c> takes an INTEGER INDEX, not the compass string the README talks in.
    ///   Passing a name renders a fully transparent cell and says nothing. This is the trap most likely
    ///   to produce eight silently-empty sheets.</item>
    ///   <item>The Clipper's default awning occludes the door's own enter cue at exactly the facings
    ///   the door is supposed to read at — so the cue has to be baked with the awning off.</item>
    /// </list>
    /// </summary>
    public class CamperIsoKitProbeTests
    {
        const string RigPath = DwellingRigFleet.CamperRigPath;
        const string Global = "CamperIso";

        static string Full(string repoRelative) => Path.Combine(RigCatalog.RepoRoot, repoRelative);

        static IRigScriptHost Host()
        {
            IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(File.ReadAllText(Full(RigPath)));
            return host;
        }

        // =============================================================================================
        //  1. THE EXPORT SURFACE IS WHAT THE README CLAIMS
        // =============================================================================================

        [Test]
        public void TheRigInstallsItsGlobal_WithTheDocumentedCellAndPivot()
        {
            using IRigScriptHost host = Host();

            Assert.That(host.EvaluateBool($"typeof {Global} === 'object' && {Global} !== null"), Is.True,
                $"the rig ran but installed no globalThis.{Global}.");

            Assert.That(host.EvaluateNumber($"{Global}.W"), Is.EqualTo(384), "cell width");
            Assert.That(host.EvaluateNumber($"{Global}.H"), Is.EqualTo(320), "cell height");
            Assert.That(host.EvaluateNumber($"{Global}.PX"), Is.EqualTo(32),
                "32 px = 1 m is the project convention (ADR 0006) and every consumer assumes it");
            Assert.That(host.EvaluateNumber($"{Global}.DIRS"), Is.EqualTo(8));
            Assert.That(host.EvaluateNumber($"{Global}.defaultElev"), Is.EqualTo(40),
                "every iso rig in this repo bakes at 40 degrees of elevation");
            Assert.That(host.EvaluateNumber($"{Global}.pivot.x"), Is.EqualTo(192));
            Assert.That(host.EvaluateNumber($"{Global}.pivot.y"), Is.EqualTo(214));

            Assert.That(host.EvaluateString($"{Global}.order.join(' ')"),
                        Is.EqualTo("N NE E SE S SW W NW"));
            Assert.That(host.EvaluateString($"{Global}.list().join(' ')"),
                        Is.EqualTo("bantam clipper"),
                        "the kit is two lengths off one loft; a third variant is a new drop, not a knob");
        }

        // =============================================================================================
        //  2. ⚠ THE TRAP: dir IS AN INDEX, AND THE FAILURE IS SILENT
        // =============================================================================================

        /// <summary>
        /// 🔴 <b>The one that will cost the baker a cycle.</b> The README describes every facing by
        /// compass name (<c>"At S the tongue comes at you"</c>), but <c>camBasis</c> computes
        /// <c>dir * PI/4</c> — so <c>dir</c> must be an integer 0..7. Passing <c>'N'</c> makes the angle
        /// <c>NaN</c>, every projected vertex becomes <c>NaN</c>, and <c>render</c> returns a **fully
        /// transparent cell with no error thrown**.
        ///
        /// <para>A baker that loops <c>CamperIso.order</c> by name therefore writes eight empty sheets
        /// and reports success. Pinned here so the bake PR starts from a measurement instead of
        /// discovering it.</para>
        /// </summary>
        [Test]
        public void RenderTakesAnIntegerDirection_AndACompassStringSilentlyRendersNothing()
        {
            using IRigScriptHost host = Host();

            const string countOpaque =
                "(function(d){var p=" + Global + ".render(d,{variant:'bantam'});var n=0;" +
                "for(var i=3;i<p.length;i+=4){if(p[i]!==0)n++;}return n;})";

            double byIndex = host.EvaluateNumber(countOpaque + "(0)");
            double byName = host.EvaluateNumber(countOpaque + "('N')");

            Assert.That(byIndex, Is.GreaterThan(1000),
                "render(0) drew almost nothing — the rig itself is broken, not just the call form.");

            Assert.That(byName, Is.EqualTo(0),
                "render('N') drew pixels, so the compass-string form works after all and this trap is " +
                "stale — simplify the baker and delete this test. (It returned " + byName + ".)");
        }

        // =============================================================================================
        //  3. THE README'S CROP BOXES ARE THE RIG'S ACTUAL PIXELS
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The faithfulness control, and with no reference sheets in the kit it is the only one
        /// available.</b> The README publishes a per-facing bounding box for both variants and says to
        /// "crop and pack from a per-facing bbox, never from the cell". If those numbers are right, the
        /// drop is internally consistent and can be packed from them; if they are stale, every sheet is
        /// cropped wrong.
        ///
        /// <para>Measured off the alpha channel of a real render, at rest, in the repo's own V8.</para>
        /// </summary>
        [TestCase("bantam", 0, "150,119,226,253")]
        [TestCase("bantam", 1, "111,124,258,255")]
        [TestCase("bantam", 2, "82,135,266,233")]
        [TestCase("bantam", 3, "111,122,258,256")]
        [TestCase("bantam", 4, "157,117,233,275")]
        [TestCase("bantam", 5, "125,122,272,262")]
        [TestCase("bantam", 6, "117,141,301,233")]
        [TestCase("bantam", 7, "125,124,272,255")]
        [TestCase("clipper", 0, "145,87,282,282")]
        [TestCase("clipper", 1, "75,99,291,277")]
        [TestCase("clipper", 2, "31,109,311,235")]
        [TestCase("clipper", 3, "75,96,291,279")]
        [TestCase("clipper", 4, "101,78,238,308")]
        [TestCase("clipper", 5, "92,96,308,285")]
        [TestCase("clipper", 6, "72,133,352,271")]
        [TestCase("clipper", 7, "92,99,308,277")]
        public void EveryPerFacingCropBoxMatchesTheReadme(string variant, int dir, string expected)
        {
            using IRigScriptHost host = Host();

            string measured = host.EvaluateString(BboxExpression(variant, dir));

            Assert.That(measured, Is.EqualTo(expected),
                $"{variant} facing {dir} ({host.EvaluateString($"{Global}.order[{dir}]")}) measures " +
                $"{measured} but the kit's README publishes {expected}. Either the rig was reshaped " +
                "after the README was written or the table was transcribed by hand — and since this " +
                "kit ships no reference sheets, that table is the only thing a bake can be packed from.");
        }

        /// <summary>
        /// The drawbar sits on the ground BELOW the pivot row, and the README says how far: 61 px on
        /// the Bantam and 94 px on the Clipper when the nose swings toward the camera (S).
        ///
        /// <para>⚠ Worth pinning because it looks exactly like a bug. The pivot is the ground-centre of
        /// the BODY, tongue excluded, so ground-level pixels painting below the pivot row are the
        /// A-frame resting on the dirt — not the camper sinking into it. An importer that "corrects"
        /// the pivot to the sprite's bottom edge would float every camper by that many pixels.</para>
        /// </summary>
        [TestCase("bantam", 61)]
        [TestCase("clipper", 94)]
        public void TheDrawbarPaintsBelowThePivotRow_ByTheDocumentedAmount(string variant, int expected)
        {
            using IRigScriptHost host = Host();

            // Facing 4 is S — the tongue toward the camera, which is where the overhang is greatest.
            string[] box = host.EvaluateString(BboxExpression(variant, 4)).Split(',');
            int y1 = int.Parse(box[3]);
            int pivotY = (int)host.EvaluateNumber($"{Global}.pivot.y");

            Assert.That(y1 - pivotY, Is.EqualTo(expected),
                $"{variant} paints {y1 - pivotY} px below the pivot row at S; the README documents " +
                $"{expected}. That overhang is the drawbar on the ground — if it changed, the tongue " +
                "geometry moved and every placement that assumes the published pivot is off.");
        }

        // =============================================================================================
        //  4. THE AWNING HIDES THE CLIPPER'S OWN ENTER CUE
        // =============================================================================================

        /// <summary>
        /// 🔴 <b>A measured trap for whoever bakes the enter cue.</b> The README says <c>+x</c> is the
        /// curb side, "which is the door side, and it reads at <c>SW W NW</c>". That is true of the
        /// BODY — and the Bantam behaves exactly so. The Clipper does not, because it fits an
        /// <b>awning by default</b>, on the curb side, directly over the door.
        ///
        /// <para>Measured as the number of pixels the door cue changes between <c>swing:0</c> and
        /// <c>swing:1</c>. With the awning fitted, the Clipper's cue at W is a fraction of what it is
        /// without. Bake the cue with <c>awning:false</c>, or the enter animation is a few pixels
        /// twitching under a canopy.</para>
        /// </summary>
        [Test]
        public void TheClippersDefaultAwningOccludesItsOwnDoorCue()
        {
            using IRigScriptHost host = Host();

            const int W = 6;   // facing index for W, where the curb side faces the camera squarely

            double withAwning = host.EvaluateNumber(SwingDeltaExpression("clipper", W, awning: true));
            double withoutAwning = host.EvaluateNumber(SwingDeltaExpression("clipper", W, awning: false));

            Assert.That(withoutAwning, Is.GreaterThan(withAwning * 2.0),
                $"at W the Clipper's door cue changes {withAwning} px with its default awning and " +
                $"{withoutAwning} px without. If those are now close, the awning stopped occluding the " +
                "door and the bake no longer needs to turn it off — simplify the bake and delete this.");

            // …and the Bantam, which fits no awning by default, does not have the problem.
            double bantam = host.EvaluateNumber(SwingDeltaExpression("bantam", W, awning: false));
            Assert.That(bantam, Is.GreaterThan(withAwning),
                "the Bantam's unobstructed door cue should be larger than the Clipper's occluded one; " +
                "if it is not, this comparison is not measuring occlusion.");
        }

        // ---- expressions -----------------------------------------------------------------------

        /// <summary>The alpha bounding box of one render, as "x0,y0,x1,y1". Computed in JS so the whole
        /// pixel buffer never crosses the host boundary.</summary>
        static string BboxExpression(string variant, int dir) =>
            "(function(){var p=" + Global + ".render(" + dir + ",{variant:'" + variant + "'});" +
            "var W=" + Global + ".W,H=" + Global + ".H,x0=1e9,y0=1e9,x1=-1,y1=-1;" +
            "for(var y=0;y<H;y++)for(var x=0;x<W;x++){if(p[(y*W+x)*4+3]===0)continue;" +
            "if(x<x0)x0=x;if(y<y0)y0=y;if(x>x1)x1=x;if(y>y1)y1=y;}" +
            "return x0+','+y0+','+x1+','+y1;})()";

        /// <summary>How many pixels the enter cue repaints between closed and fully open.</summary>
        static string SwingDeltaExpression(string variant, int dir, bool awning) =>
            "(function(){var o={variant:'" + variant + "',awning:" + (awning ? "true" : "false") + "};" +
            "var a=" + Global + ".render(" + dir + ",Object.assign({},o,{swing:0}));" +
            "var b=" + Global + ".render(" + dir + ",Object.assign({},o,{swing:1}));" +
            "var n=0;for(var i=0;i<a.length;i+=4){" +
            "if(a[i]!==b[i]||a[i+1]!==b[i+1]||a[i+2]!==b[i+2]||a[i+3]!==b[i+3])n++;}return n;})()";
    }
}
