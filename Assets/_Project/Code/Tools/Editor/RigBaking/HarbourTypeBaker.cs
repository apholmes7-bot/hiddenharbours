using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>What one face bake produced.</summary>
    public sealed class HarbourTypeBakeReport
    {
        public string EngineName;
        public int GlyphCount;
        public int AtlasWidth, AtlasHeight;
        public int PngBytes;
        public bool AtlasRewritten;
        public bool FontRewritten;

        /// <summary>The glyphs baked, in atlas order — what a test diffs against the rig.</summary>
        public string Glyphs = "";
    }

    /// <summary>
    /// <b>THE FACE, BAKED — the kit's glyph table turned into a Unity font asset.</b>
    ///
    /// <para><b>⭐ Drawn by the rig, not re-drawn from it.</b> Every glyph on the atlas is produced by
    /// calling the bubble kit's own exported <c>text()</c> with the character, into the same canvas
    /// shim every other piece bakes through. Nothing here reads a glyph's rows and re-rasterises
    /// them: a second transcription of the same shapes is exactly the class of bug that cannot be
    /// diffed, and the kit's face would drift from the bubbles' one silent pixel at a time. What C#
    /// contributes is the GRID and the METRICS — where each glyph sits and what a Font asset must be
    /// told about it — never the ink.</para>
    ///
    /// <para><b>Why a bitmap Font and not a size knob.</b> Unity's own words, from the engine binary,
    /// are "font size and style overrides are only supported for dynamic fonts". A custom font sets at
    /// exactly its baked pixel size and ignores <c>Text.fontSize</c> entirely — which is precisely
    /// what a pixel face wants. The book gets its size from an INTEGER scale on the whole spread, so a
    /// glyph is never resampled and the hand never lands on a half cell.</para>
    ///
    /// <para><b>⚠️ The metrics a Font needs are not all settable from script.</b> Cecil on
    /// <c>UnityEngine.TextRenderingModule</c> says <c>material</c> and <c>characterInfo</c> have
    /// setters and <c>fontSize</c>, <c>ascent</c> and <c>lineHeight</c> do NOT. The first two are set
    /// through the public API; the rest go through <see cref="SerializedObject"/> by their serialized
    /// names, which are read out of Unity's own type tree
    /// (<c>m_LineSpacing</c> · <c>m_Ascent</c> · <c>m_FontSize</c>). <see cref="SetMetric"/> REFUSES
    /// loudly and prints every property it can actually see if a name ever moves — a font that
    /// silently kept a zero line height would draw every line of the book on top of the last.</para>
    /// </summary>
    public static class HarbourTypeBaker
    {
        /// <summary>The JS global the atlas canvas is parked on.</summary>
        const string Slot = "globalThis.__hhFaceAtlas";

        /// <summary>The glyph keys, sorted, parked so C# and JS agree on the order.</summary>
        const string Chars = "globalThis.__hhFaceChars";

        public static HarbourTypeBakeReport Bake()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(NotebookKitBaker.CanvasShimJs);
            RigCatalog.InstallModule(host, RigCatalog.Get(BubbleKitBaker.RigKey));

            AssertCellAgrees(host);

            // Sorted by code unit so the atlas is byte-stable across bakes — an unsorted Object.keys
            // would re-lay the whole sheet the day the rig's literal is reordered.
            host.Execute($"{Chars} = Object.keys(BubbleKit.FONT).sort();");

            int count = (int)host.EvaluateNumber($"{Chars}.length");
            if (count <= 0)
                throw new InvalidOperationException(
                    "Refusing to bake the face: BubbleKit.FONT is empty. The rig resolved but has no " +
                    "glyph table — check that the bubble rig installed rather than a stub.");

            string glyphs = host.EvaluateString($"{Chars}.join('')");
            if (glyphs.Length != count)
                throw new InvalidOperationException(
                    $"BubbleKit.FONT has {count} keys but they join to {glyphs.Length} characters — a " +
                    "key is not a single character. The atlas indexes one glyph per key and cannot " +
                    "represent a multi-character key; refusing rather than baking a shifted sheet.");

            int cw = HarbourType.GlyphWidth, ch = HarbourType.GlyphHeight;
            int cols = HarbourType.AtlasColumns;
            int rows = (count + cols - 1) / cols;
            int atlasW = cols * cw, atlasH = rows * ch;

            host.Execute(
                $"{Slot} = (function(){{" +
                $"var cs={Chars}, n=cs.length, cv=new OffscreenCanvas({atlasW},{atlasH});" +
                "var c=cv.getContext('2d');" +
                $"for(var i=0;i<n;i++){{var gx=(i%{cols})*{cw}, gy=Math.floor(i/{cols})*{ch};" +
                "BubbleKit.text(c, cs[i], gx, gy, '#ffffff', 1);}" +
                "return cv;})();");

            int w = (int)host.EvaluateNumber($"{Slot}.width");
            int h = (int)host.EvaluateNumber($"{Slot}.height");
            if (w != atlasW || h != atlasH)
                throw new InvalidOperationException(
                    $"The face atlas came back {w}×{h}, not the {atlasW}×{atlasH} it was asked for.");

            byte[] rgba = host.EvaluateBytes($"{Slot}.data");
            if (rgba.Length != w * h * 4)
                throw new InvalidOperationException(
                    $"The face atlas returned {rgba.Length} bytes for {w}×{h} (expected {w * h * 4}).");

            // An all-transparent atlas means text() drew nothing — the failure that would otherwise
            // ship a font of blanks and read as "the book has no words".
            bool anyInk = false;
            for (int i = 3; i < rgba.Length; i += 4)
                if (rgba[i] != 0) { anyInk = true; break; }

            if (!anyInk)
                throw new InvalidOperationException(
                    "The face atlas baked completely transparent — BubbleKit.text() drew nothing. " +
                    "Refusing rather than shipping a font of blanks.");

            var report = new HarbourTypeBakeReport
            {
                EngineName = host.EngineName,
                GlyphCount = count,
                AtlasWidth = w,
                AtlasHeight = h,
                Glyphs = glyphs,
            };

            string folderAbs = Path.Combine(RigCatalog.RepoRoot, HarbourType.ArtFolder);
            Directory.CreateDirectory(folderAbs);

            WriteAtlasPng(rgba, w, h, report);
            return report;
        }

        /// <summary>
        /// Import the atlas and build the Font over it. Separate from <see cref="Bake"/> because a
        /// bake writes files and an import needs Unity to have seen them — the same two-step every
        /// sibling baker uses, and the reason the headless entry point calls both with a refresh in
        /// between.
        /// </summary>
        public static void BuildFontAsset(HarbourTypeBakeReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));

            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(HarbourType.AtlasPath);
            if (atlas == null)
                throw new InvalidOperationException(
                    $"{HarbourType.AtlasPath} is not imported yet. Refresh between the bake and the " +
                    "font build.");

            var font = AssetDatabase.LoadAssetAtPath<Font>(HarbourType.FontPath);
            bool created = font == null;
            if (created)
            {
                font = new Font(HarbourType.FontName);
                AssetDatabase.CreateAsset(font, HarbourType.FontPath);
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(HarbourType.FontPath);
            if (material == null)
            {
                // The material lives INSIDE the font asset. A loose .mat beside it is one more file to
                // keep in step, and Unity resolves a font's material by reference either way.
                Shader shader = Shader.Find("UI/Default") ?? Shader.Find("GUI/Text Shader");
                if (shader == null)
                    throw new InvalidOperationException(
                        "Neither UI/Default nor GUI/Text Shader resolved — cannot build the face's " +
                        "material.");

                material = new Material(shader) { name = HarbourType.FontName + "_Material" };
                AssetDatabase.AddObjectToAsset(material, font);
            }

            material.mainTexture = atlas;
            font.material = material;
            font.characterInfo = BuildCharacterInfo(report, atlas.width, atlas.height);

            var so = new SerializedObject(font);
            SetMetric(so, "m_LineSpacing", HarbourType.LineHeight);
            SetMetric(so, "m_Ascent", HarbourType.Ascent);
            SetMetric(so, "m_FontSize", HarbourType.GlyphHeight);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            report.FontRewritten = true;
        }

        /// <summary>
        /// One <see cref="CharacterInfo"/> per glyph.
        ///
        /// <para>The vertical frame is the part worth stating out loud. The rig draws a glyph's rows
        /// downwards from the CAP TOP, and calls the last row of a capital the baseline — so the
        /// ascent is that index PLUS ONE (the underside of that row), the descent is whatever the
        /// 8-row box has left, and a glyph's quad runs from <c>+Ascent</c> down to
        /// <c>Ascent - GlyphHeight</c>. Off by one anywhere here sits every line of the book one pixel
        /// into its own rule.</para>
        /// </summary>
        static CharacterInfo[] BuildCharacterInfo(HarbourTypeBakeReport report, int atlasW, int atlasH)
        {
            int cw = HarbourType.GlyphWidth, ch = HarbourType.GlyphHeight;
            int cols = HarbourType.AtlasColumns;

            var infos = new CharacterInfo[report.GlyphCount];
            for (int i = 0; i < report.GlyphCount; i++)
            {
                int gx = (i % cols) * cw;
                int gy = (i / cols) * ch;          // in DRAWING space: top-left origin

                float u0 = gx / (float)atlasW;
                float u1 = (gx + cw) / (float)atlasW;

                // The PNG is written flipped (rigs draw top-down, Unity textures are bottom-up), so a
                // drawing row maps to 1 - v.
                float vTop = 1f - gy / (float)atlasH;
                float vBottom = 1f - (gy + ch) / (float)atlasH;

                infos[i] = new CharacterInfo
                {
                    index = report.Glyphs[i],
                    advance = HarbourType.Advance,
                    glyphWidth = cw,
                    glyphHeight = ch,
                    minX = 0,
                    maxX = cw,
                    maxY = HarbourType.Ascent,
                    minY = HarbourType.Ascent - ch,
                    uvTopLeft = new Vector2(u0, vTop),
                    uvTopRight = new Vector2(u1, vTop),
                    uvBottomLeft = new Vector2(u0, vBottom),
                    uvBottomRight = new Vector2(u1, vBottom),
                };
            }

            return infos;
        }

        /// <summary>
        /// Set one serialized metric, or refuse and say what IS there.
        ///
        /// <para>These names come from Unity's own type tree rather than from documentation, but a
        /// name that moves in a future editor would otherwise fail SILENTLY — the font would ship with
        /// a zero line height and every line of the book would draw on top of the last. Printing the
        /// visible property list turns that into one legible failure with the answer in it.</para>
        /// </summary>
        static void SetMetric(SerializedObject so, string property, int value)
        {
            SerializedProperty prop = so.FindProperty(property);
            if (prop == null)
                throw new InvalidOperationException(
                    $"Font has no serialized property '{property}' in this editor, so the face cannot " +
                    "be given its metrics. Refusing rather than shipping a font that draws every " +
                    "line on top of the last.\nProperties actually present:\n  " +
                    string.Join("\n  ", VisibleProperties(so)));

            if (prop.propertyType == SerializedPropertyType.Float) prop.floatValue = value;
            else prop.intValue = value;
        }

        static List<string> VisibleProperties(SerializedObject so)
        {
            var names = new List<string>();
            SerializedProperty it = so.GetIterator();
            while (it.NextVisible(enterChildren: true)) names.Add($"{it.propertyPath} ({it.propertyType})");
            return names;
        }

        /// <summary>
        /// The cell C# believes must be the cell the rig draws, checked before a pixel is written.
        ///
        /// <para><see cref="HarbourType"/> aliases the bubble kit's constants rather than restating
        /// them, so this can only fail if the RIG moved — which is exactly when the atlas grid and the
        /// font metrics would otherwise go quietly wrong together.</para>
        /// </summary>
        static void AssertCellAgrees(IRigScriptHost host)
        {
            var drift = new List<string>();

            void Check(string expr, int mine, string what)
            {
                int theirs = (int)host.EvaluateNumber(expr);
                if (theirs != mine) drift.Add($"{what}: rig says {theirs}, C# says {mine} ({expr})");
            }

            Check("BubbleKit.CELL.adv", HarbourType.Advance, "advance");
            Check("BubbleKit.CELL.w", HarbourType.GlyphWidth, "glyph width");
            Check("BubbleKit.CELL.box", HarbourType.GlyphHeight, "glyph box");
            Check("BubbleKit.CELL.line", HarbourType.LineHeight, "line height");
            Check("BubbleKit.CELL.baseline", HarbourType.Ascent - 1, "baseline row");

            if (drift.Count > 0)
                throw new InvalidOperationException(
                    "Refusing to bake the face: the rig's cell and HarbourType disagree on " +
                    $"{drift.Count} value(s).\n  " + string.Join("\n  ", drift) +
                    "\nThe rig is the oracle — fix the C# constants (and the bubble kit's, which " +
                    "HarbourType aliases), never the rig.");
        }

        static void WriteAtlasPng(byte[] rgba, int w, int h, HarbourTypeBakeReport report)
        {
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                int dstRow = (h - 1 - y) * w;      // top-left origin in, bottom-up texture out
                int srcRow = y * w * 4;
                for (int x = 0; x < w; x++)
                {
                    int i = srcRow + x * 4;
                    pixels[dstRow + x] = new Color32(rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]);
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false);
            byte[] png;
            try
            {
                tex.SetPixels32(pixels);
                tex.Apply(false, false);
                png = tex.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }

            report.PngBytes = png.Length;

            string abs = Path.Combine(RigCatalog.RepoRoot, HarbourType.AtlasPath);
            if (File.Exists(abs) && BytesEqual(File.ReadAllBytes(abs), png))
            {
                report.AtlasRewritten = false;
                return;
            }

            File.WriteAllBytes(abs, png);
            report.AtlasRewritten = true;
        }

        static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>A one-line summary for the bake log.</summary>
        public static string Summarise(HarbourTypeBakeReport r)
        {
            var sb = new StringBuilder();
            sb.Append($"face: {r.GlyphCount} glyphs on a {r.AtlasWidth}×{r.AtlasHeight} atlas ");
            sb.Append($"({r.PngBytes} B, {(r.AtlasRewritten ? "rewritten" : "unchanged")}) ");
            sb.Append($"via {r.EngineName}");
            return sb.ToString();
        }
    }
}
