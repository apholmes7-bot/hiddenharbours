using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE TRIPWIRE for the one face.</b>
    ///
    /// <para><b>⚠️ RED UNTIL THE BAKE RUNS, AND THAT IS DELIBERATE.</b> This PR lands the baker and
    /// the metrics but no atlas — the lane that wrote it cannot open Unity. Gating on the asset
    /// existing (<c>if (!File.Exists) Assert.Pass()</c>) would be exactly the vacuous green the
    /// cloud-lane protocol forbids: it would have passed for the whole time the face was missing, and
    /// would go on passing the day the bake silently stopped producing it. So EXISTENCE IS THE FIRST
    /// ASSERTION and the message names the menu item that fixes it.</para>
    ///
    /// <para>Expect red on the first CI run and green after the coordinator's bake:
    /// <c>Hidden Harbours ▸ Art ▸ Bake The Face</c>, or headless
    /// <c>-executeMethod HiddenHarbours.Tools.RigBaking.NotebookKitBakeMenu.BakeFaceHeadless</c>.</para>
    /// </summary>
    public class HarbourTypeBakeTests
    {
        const string HowToFix =
            "Run: Hidden Harbours ▸ Art ▸ Bake The Face — or headless, -executeMethod " +
            "HiddenHarbours.Tools.RigBaking.NotebookKitBakeMenu.BakeFaceHeadless (twice on a fresh " +
            "worktree: a first Unity run can import only and never call the method).";

        static IRigScriptHost Host()
        {
            var host = RigScriptHostFactory.Create();
            host.Execute(NotebookKitBaker.CanvasShimJs);
            RigCatalog.InstallModule(host, RigCatalog.Get(BubbleKitBaker.RigKey));
            return host;
        }

        /// <summary>Every glyph the rig actually ships, sorted exactly as the baker sorts them.</summary>
        static string RigGlyphs(IRigScriptHost host)
        {
            host.Execute("globalThis.__hhFaceChars = Object.keys(BubbleKit.FONT).sort();");
            return host.EvaluateString("__hhFaceChars.join('')");
        }

        // ---- the metrics, which need no bake at all ---------------------------------------------------

        [Test]
        public void TheCellCSharpBelievesIsTheCellTheRigDraws()
        {
            // The half of the face that CAN be proven before a bake — and the half that would send the
            // atlas grid and the font metrics wrong together if it ever drifted.
            using var host = Host();

            Assert.That((int)host.EvaluateNumber("BubbleKit.CELL.adv"), Is.EqualTo(HarbourType.Advance));
            Assert.That((int)host.EvaluateNumber("BubbleKit.CELL.w"), Is.EqualTo(HarbourType.GlyphWidth));
            Assert.That((int)host.EvaluateNumber("BubbleKit.CELL.box"), Is.EqualTo(HarbourType.GlyphHeight));
            Assert.That((int)host.EvaluateNumber("BubbleKit.CELL.line"), Is.EqualTo(HarbourType.LineHeight));
        }

        [Test]
        public void TheAscentIsTheBaselineRowPlusOne()
        {
            // ⭐ The off-by-one that would sit every line of the book one pixel into its own rule. The
            // rig calls row `baseline` the LAST row of a capital, not the first row under it, so the
            // distance from cap top to the underside of that row is index + 1.
            using var host = Host();

            int baselineRow = (int)host.EvaluateNumber("BubbleKit.CELL.baseline");
            Assert.That(HarbourType.Ascent, Is.EqualTo(baselineRow + 1));

            int cap = (int)host.EvaluateNumber("BubbleKit.CELL.cap");
            Assert.That(HarbourType.Ascent, Is.EqualTo(cap),
                "a capital sits exactly on the baseline, so ascent and cap height are the same number");

            int desc = (int)host.EvaluateNumber("BubbleKit.CELL.desc");
            Assert.That(HarbourType.Descent, Is.EqualTo(-desc), "descent is the rows below the baseline");
            Assert.That(HarbourType.Ascent - HarbourType.Descent, Is.EqualTo(HarbourType.GlyphHeight),
                "ascent plus descent must be the whole glyph box or a glyph is cropped");
        }

        [Test]
        public void NoGlyphDrawsOutsideTheBoxTheMetricsClaim()
        {
            // Every glyph's rows must fit within `box` rows of its top offset, or the atlas cell is too
            // short and the sheet shifts under itself.
            using var host = Host();

            host.Execute(
                "globalThis.__hhOver = (function(){var out=[];var F=BubbleKit.FONT;" +
                "for (var k in F){var G=F[k], rows=G.g||G, t=G.t||0;" +
                "  if (t + rows.length > BubbleKit.CELL.box) out.push(k+':'+(t+rows.length));" +
                "  for (var r=0;r<rows.length;r++) if (rows[r].length > BubbleKit.CELL.w) out.push(k+':w'+rows[r].length);" +
                "} return out;})();");

            int over = (int)host.EvaluateNumber("__hhOver.length");
            string names = host.EvaluateString("__hhOver.join(', ')");

            Assert.That(over, Is.Zero,
                $"these glyphs draw outside the {HarbourType.GlyphWidth}×{HarbourType.GlyphHeight} " +
                $"cell the atlas gives them: {names}");
        }

        // ---- the bake ------------------------------------------------------------------------------------

        [Test]
        public void TheAtlasIsOnDisk()
        {
            Assert.That(File.Exists(HarbourType.AtlasPath), Is.True,
                $"{HarbourType.AtlasPath} is not baked.\n{HowToFix}");
        }

        [Test]
        public void TheFontAssetResolvesAndCarriesTheKitsGlyphs()
        {
            Assert.That(File.Exists(HarbourType.FontPath), Is.True,
                $"{HarbourType.FontPath} is not built.\n{HowToFix}");

            var font = AssetDatabase.LoadAssetAtPath<Font>(HarbourType.FontPath);
            Assert.That(font, Is.Not.Null, $"{HarbourType.FontPath} is on disk but does not load as a Font.");

            Assert.That(font.characterInfo, Is.Not.Null.And.Not.Empty,
                "the font has no character table — the build set material but never characterInfo.");

            using var host = Host();
            string glyphs = RigGlyphs(host);

            var have = font.characterInfo.Select(c => (char)c.index).ToHashSet();
            var missing = glyphs.Where(c => !have.Contains(c)).ToList();

            Assert.That(missing, Is.Empty,
                "the font is missing glyphs the rig ships: " +
                string.Join(" ", missing.Select(c => $"U+{(int)c:X4}")));

            Assert.That(font.characterInfo.Length, Is.EqualTo(glyphs.Length),
                "the font carries a different number of glyphs than the rig declares — a stale bake.");
        }

        [Test]
        public void TheFontHasALineHeightAndAnAdvanceRatherThanZeroes()
        {
            // ⚠️ The silent failure this whole design guards: a script-made Font whose metrics were
            // never written reports zero, and every line of the book draws on top of the last. Unity
            // gives `lineHeight` and `ascent` no setter, so they can only arrive through the
            // serialized names — and if those ever move, this is what says so.
            var font = AssetDatabase.LoadAssetAtPath<Font>(HarbourType.FontPath);
            Assert.That(font, Is.Not.Null, $"the face is not built.\n{HowToFix}");

            Assert.That(font.lineHeight, Is.EqualTo((float)HarbourType.LineHeight).Within(0.01f),
                "line height did not survive the SerializedObject write.");
            Assert.That(font.ascent, Is.EqualTo(HarbourType.Ascent),
                "ascent did not survive the SerializedObject write.");

            foreach (CharacterInfo info in font.characterInfo)
                Assert.That(info.advance, Is.EqualTo(HarbourType.Advance),
                    $"U+{info.index:X4} advances {info.advance}, not the monospace cell. A single " +
                    "wrong advance breaks the promise that a caret owns a cell.");
        }

        [Test]
        public void TheAtlasIsExactlyTheGridTheMetricsDescribe()
        {
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(HarbourType.AtlasPath);
            Assert.That(atlas, Is.Not.Null, $"the atlas is not imported.\n{HowToFix}");

            using var host = Host();
            int count = RigGlyphs(host).Length;
            int rows = (count + HarbourType.AtlasColumns - 1) / HarbourType.AtlasColumns;

            Assert.That(atlas.width, Is.EqualTo(HarbourType.AtlasColumns * HarbourType.GlyphWidth));
            Assert.That(atlas.height, Is.EqualTo(rows * HarbourType.GlyphHeight));
        }

        [Test]
        public void TheAtlasImportedAtPointWithNoMips()
        {
            // The face is under Assets/_Project/Art/, so ArtImportPipeline's lock reaches it. This
            // WITNESSES that lock rather than restating it — a bilinear atlas would blur every glyph
            // and read as "the pixel font looks wrong" rather than as an import setting.
            var importer = AssetImporter.GetAtPath(HarbourType.AtlasPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null, $"the atlas is not imported.\n{HowToFix}");

            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), "a blurred face is not a face");
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        }

        [Test]
        public void TheFaceLivesOutsideTheNotebooksPieceFolder()
        {
            // The reverse arm of NotebookArtTests asserts that folder holds EXACTLY the kit's pieces.
            // The face is the BUBBLE kit's and belongs to neither consumer, so it must not land there
            // — this is what stops a well-meaning tidy-up from breaking that test.
            Assert.That(HarbourType.ArtFolder, Is.Not.EqualTo(NotebookKit.ArtFolder));
            Assert.That(HarbourType.AtlasPath, Does.StartWith("Assets/_Project/Art/"),
                "still under Art/, so ArtImportPipeline's lock reaches it");
        }
    }
}
