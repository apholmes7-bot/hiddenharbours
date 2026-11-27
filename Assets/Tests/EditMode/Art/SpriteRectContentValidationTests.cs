using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>Checks source bytes before an importer can clamp or discard an invalid sprite rect.</summary>
    public class SpriteRectContentValidationTests
    {
        const string ArtRoot = "Assets/_Project/Art";

        // Baseline c626cea: no out-of-bounds rects. If an existing exception ever needs to be
        // recorded, pin the entire diagnostic (path, rect and texture size), not a folder or
        // file wildcard. Remove the entry when repaired; it must not excuse another bad slice.
        static readonly string[] KnownOutOfBoundsRectsAtBaseline = { };

        [Test]
        public void EveryArtPng_HasAllStoredSpriteRectsInsideItsSourcePixels()
        {
            string[] paths = Directory.GetFiles(ArtRoot, "*", SearchOption.AllDirectories)
                .Where(p => string.Equals(Path.GetExtension(p), ".png", StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            Assert.That(paths, Is.Not.Empty, "Art PNG scan found no files.");
            var faults = new List<string>();
            var outside = new List<string>();
            int rectCount = 0, implicitSingles = 0, sourceOnly = 0;
            foreach (string path in paths)
            {
                try
                {
                    (uint width, uint height) size;
                    using (var stream = File.OpenRead(path)) size = ReadPngSize(stream);
                    if (!File.Exists(path + ".meta"))
                    {
                        // Unity ignores source folders ending in '~'. Still read their PNG headers,
                        // but there is no imported sprite and no .meta rect to validate there.
                        if (path.Split('/').Any(part => part.EndsWith("~", StringComparison.Ordinal)))
                        { sourceOnly++; continue; }
                        throw new FormatException("missing .meta");
                    }
                    List<SourceRect> rects = ReadRects(File.ReadAllText(path + ".meta"), size.width, size.height);
                    rectCount += rects.Count;
                    implicitSingles += rects.Count(r => r.Label == "implicit single sprite");
                    outside.AddRange(rects.Where(r => !r.IsInside(size.width, size.height))
                        .Select(r => DescribeOutside(path, r, size.width, size.height)));
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is FormatException)
                { faults.Add(path + ": " + ex.Message); }
            }
            faults.AddRange(outside.Except(KnownOutOfBoundsRectsAtBaseline, StringComparer.Ordinal));
            faults.AddRange(KnownOutOfBoundsRectsAtBaseline.Except(outside, StringComparer.Ordinal)
                .Select(entry => "Remove repaired exception from KnownOutOfBoundsRectsAtBaseline: " + entry));
            TestContext.WriteLine($"Read {paths.Length} PNG headers; {rectCount} rects " +
                $"({implicitSingles} implicit single sprites); {sourceOnly} source-only PNGs; {outside.Count} outside.");
            Assert.That(faults, Is.Empty, string.Join("\n", faults));
        }

        static (uint width, uint height) ReadPngSize(Stream stream)
        {
            var header = new byte[24];
            int offset = 0;
            while (offset < header.Length)
            {
                int read = stream.Read(header, offset, header.Length - offset);
                if (read == 0) throw new FormatException("truncated PNG header");
                offset += read;
            }
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            if (!header.Take(8).SequenceEqual(signature) || BigEndian(header, 8) != 13 ||
                header[12] != 'I' || header[13] != 'H' || header[14] != 'D' || header[15] != 'R')
                throw new FormatException("expected PNG signature and IHDR (resolve any LFS pointer first)");
            uint width = BigEndian(header, 16), height = BigEndian(header, 20);
            if (width == 0 || height == 0 || width > int.MaxValue || height > int.MaxValue)
                throw new FormatException("invalid PNG pixel dimensions");
            return (width, height);
        }

        static uint BigEndian(byte[] bytes, int offset) =>
            (uint)bytes[offset] << 24 | (uint)bytes[offset + 1] << 16 |
            (uint)bytes[offset + 2] << 8 | bytes[offset + 3];

        sealed class SourceRect
        {
            public string Label;
            public double X, Y, Width, Height;
            public bool IsInside(uint width, uint height) =>
                X >= 0 && Y >= 0 && Width >= 0 && Height >= 0 &&
                X + Width <= width && Y + Height <= height;
        }

        static string DescribeOutside(string path, SourceRect r, uint width, uint height) =>
            string.Format(CultureInfo.InvariantCulture,
                "{0}: {1} rect ({2}, {3}, {4}, {5}) outside {6} x {7} PNG",
                path, r.Label, r.X, r.Y, r.Width, r.Height, width, height);

        // Unity's text importer metadata uses block rects; accept flow rects too. Read every
        // stored rect even in Single/None mode (old slice rows can survive a mode change).
        // Single mode without a stored rect means the full source texture, at origin zero.
        static List<SourceRect> ReadRects(string meta, uint width, uint height)
        {
            string[] lines = meta.Replace("\r", "").Split('\n');
            var mode = Regex.Match(meta, @"(?m)^  spriteMode: ([0-3])[ \t]*\r?$");
            if (!mode.Success) throw new FormatException("missing or unsupported spriteMode");
            var rects = new List<SourceRect>();
            string spriteName = "unnamed sprite";
            for (int i = 0; i < lines.Length; i++)
            {
                string text = lines[i].Trim();
                if (text.StartsWith("- ", StringComparison.Ordinal)) text = text.Substring(2);
                if (text.StartsWith("name:", StringComparison.Ordinal)) spriteName = text.Substring(5).Trim();
                if (!text.StartsWith("rect:", StringComparison.Ordinal) &&
                    !text.StartsWith("spriteRect:", StringComparison.Ordinal)) continue;
                int indent = lines[i].Length - lines[i].TrimStart().Length;
                string fields = text.Substring(text.IndexOf(':') + 1);
                for (int j = i + 1; j < lines.Length; j++)
                {
                    if (string.IsNullOrWhiteSpace(lines[j])) continue;
                    if (lines[j].Length - lines[j].TrimStart().Length <= indent) break;
                    fields += "\n" + lines[j];
                }
                var values = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (Match field in Regex.Matches(fields, @"\b(x|y|width|height):\s*([^\s,}]+)"))
                {
                    if (values.ContainsKey(field.Groups[1].Value) ||
                        !double.TryParse(field.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
                        double.IsNaN(value) || double.IsInfinity(value))
                        throw new FormatException($"invalid rect field at line {i + 1}: {field.Value}");
                    values.Add(field.Groups[1].Value, value);
                }
                if (values.Count != 4) throw new FormatException($"incomplete rect at line {i + 1}");
                rects.Add(new SourceRect { Label = spriteName + " at line " + (i + 1),
                    X = values["x"], Y = values["y"], Width = values["width"], Height = values["height"] });
            }
            if (mode.Groups[1].Value == "1" && rects.Count == 0)
                rects.Add(new SourceRect { Label = "implicit single sprite", Width = width, Height = height });
            return rects;
        }

        [TestCase(0, 0, 32, 32, true)]
        [TestCase(3, 0, 32, 32, false)]
        [TestCase(0, 3, 32, 32, false)]
        [TestCase(-1, 0, 32, 32, false)]
        [TestCase(0, -1, 32, 32, false)]
        [TestCase(0, 0, -1, 32, false)]
        [TestCase(0.5, 0.5, 31.5, 31.5, true)]
        [TestCase(0.5, 0, 32, 32, false)]
        public void RectEdges_AreMeasuredAgainstSourcePixels(double x, double y, double w, double h, bool inside)
        {
            var rect = new SourceRect { X = x, Y = y, Width = w, Height = h };
            Assert.That(rect.IsInside(32, 32), Is.EqualTo(inside));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void StoredRects_InSingleAndMultipleModes_IncludeEverySlice(int mode)
        {
            string meta = "TextureImporter:\n  spriteMode: " + mode + "\n  spriteSheet:\n    sprites:\n" +
                "    - name: good\n      rect:\n        serializedVersion: 2\n        x: 0\n        y: 0\n        width: 32\n        height: 32\n" +
                "    - name: bad\n      rect:\n        serializedVersion: 2\n        x: 3\n        y: 0\n        width: 32\n        height: 32\n";
            var rects = ReadRects(meta.Replace("\n", "\r\n"), 32, 32);
            Assert.That(rects.Count, Is.EqualTo(2));
            Assert.That(rects.Select(r => r.IsInside(32, 32)), Is.EqualTo(new[] { true, false }));
            StringAssert.Contains("sample.png: bad", DescribeOutside("sample.png", rects[1], 32, 32));
            StringAssert.Contains("(3, 0, 32, 32) outside 32 x 32", DescribeOutside("sample.png", rects[1], 32, 32));
        }

        [Test]
        public void SingleSprite_WithoutStoredRect_UsesTheWholePng()
        {
            var rects = ReadRects("TextureImporter:\n  spriteMode: 1\n  spriteSheet:\n    sprites: []\n", 17, 29);
            Assert.That(rects.Count, Is.EqualTo(1));
            Assert.That(new[] { rects[0].X, rects[0].Y, rects[0].Width, rects[0].Height }, Is.EqualTo(new[] { 0d, 0d, 17d, 29d }));
        }

        [TestCase("{x: 3, y: 0, width: 32, height: 32}", false)]
        [TestCase("{x: 0, y: 0, width: 32, height: 32}", true)]
        public void FlowSpriteRect_IsAlsoChecked(string rect, bool inside)
        {
            var rects = ReadRects("TextureImporter:\n  spriteMode: 1\n  spriteRect: " + rect + "\n", 32, 32);
            Assert.That(rects.Count, Is.EqualTo(1));
            Assert.That(rects[0].IsInside(32, 32), Is.EqualTo(inside));
        }

        [TestCase("{x: 0, y: 0, width: 32}")]
        [TestCase("{x: NaN, y: 0, width: 32, height: 32}")]
        [TestCase("{x: Infinity, y: 0, width: 32, height: 32}")]
        [TestCase("{x: 0, x: 1, y: 0, width: 32, height: 32}")]
        public void MalformedRects_FailInsteadOfDisappearing(string rect)
        {
            Assert.Throws<FormatException>(() => ReadRects("TextureImporter:\n  spriteMode: 2\n  rect: " + rect, 32, 32));
        }

        [Test]
        public void PngHeader_ReadsBigEndianDimensionsWithoutPixelDecode()
        {
            byte[] header = Header();
            header[18] = 1; header[19] = 2; header[22] = 3; header[23] = 4;
            using (var stream = new MemoryStream(header))
                Assert.That(ReadPngSize(stream), Is.EqualTo((258u, 772u)));
        }

        [TestCase(0)] // signature
        [TestCase(11)] // IHDR length
        [TestCase(12)] // IHDR type
        [TestCase(19)] // zero width
        [TestCase(23)] // zero height
        public void InvalidPngHeader_Fails(int byteToClear)
        {
            byte[] header = Header(); header[byteToClear] = 0;
            using (var stream = new MemoryStream(header))
                Assert.Throws<FormatException>(() => ReadPngSize(stream));
        }

        [Test]
        public void TruncatedPngHeader_Fails()
        {
            using (var stream = new MemoryStream(Header().Take(23).ToArray()))
                Assert.Throws<FormatException>(() => ReadPngSize(stream));
        }

        static byte[] Header() => new byte[]
        { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 32, 0, 0, 0, 32 };
    }
}
