using System.IO;
using System.IO.Compression;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>Does the cell we pick for a heading actually DEPICT that heading?</b> — asserted against the
    /// PIXELS of the shipped Fisher art, never against our own lookup table.
    ///
    /// <para>This test exists because the mirrored iso BOAT art shipped and caused 6 of the owner's 10
    /// playtest defects, and it shipped green: the tests of the day asserted the mapping against the
    /// mapping. So nothing here trusts a label, a constant, or the rig's README. The sheet is decoded and
    /// measured, and the measurement decides which row is which.</para>
    ///
    /// <para><b>The measurement.</b> In each direction row, count the character's FACE — the pixels of the
    /// skin ramp in the head band — and note where they sit across the body:
    /// <list type="bullet">
    /// <item><description>Facing AWAY from the camera (North) shows almost no face → the row with the
    /// FEWEST skin pixels is North.</description></item>
    /// <item><description>Facing the camera (South) shows the most → the row with the MOST is South.</description></item>
    /// <item><description>A character facing screen-LEFT carries its face left of its body's centre, and
    /// one facing screen-RIGHT carries it right — which separates the western half of the compass from the
    /// eastern half. This is the assertion that catches a mirrored bake, because a mirror is exactly a
    /// left/right swap and leaves the N/S rows untouched.</description></item>
    /// </list>
    /// ✅ The art now measures out as N · NE · E · SE · S · SW · W · NW (row i depicts +45°·i) — a CLOCKWISE
    /// bake, matching its labels. It did NOT used to: the rig baked counter-clockwise and
    /// <c>CharacterVisualDef.FacingsAreCounterClockwise</c> was the un-mirror. The art director fixed the rig
    /// at source and re-baked all twelve body sheets, so that flag is now <b>false</b> — and these tests are
    /// what proved it, going red on the new art while the flag still said true.</para>
    ///
    /// <para><b>The PNG is decoded here, in pure C#</b>, rather than read through <c>Texture2D.GetPixels</c>.
    /// Two reasons: the sheets import with <c>isReadable: 0</c> so the imported texture has no CPU pixels to
    /// read; and going nowhere near a graphics API keeps this test safe on CI, which runs Unity with a null
    /// graphics device. It also means we measure the ORIGINAL art, immune to any importer downscale.</para>
    /// </summary>
    public class CharacterIsoFacingTests
    {
        const string IdleSheetPath = "Assets/_Project/Art/Characters/Iso/Fisher_idle.png";
        const string FisherVisualPath = "Assets/_Project/Data/Characters/FisherIso.asset";

        // The sheet's cell, from the art README (the slice tests own guarding it).
        const int CellW = 64, CellH = 88, Directions = 8, IdleFrames = 6;

        // The head band within a cell, in pixels from the cell's TOP. The character occupies rows 40..87 of
        // the 88-tall cell (measured across all three Fisher sheets); the head sits at the top of that.
        const int HeadTop = 40, HeadBottom = 60;

        // The Fisher's SKIN RAMP, read off the art. Three shades: lit, mid and shadowed. A tolerance covers
        // any future re-palette that nudges them; the SkinRamp_IsStillPresentInTheArt test fails loudly if a
        // re-palette moves them out of reach, rather than letting every row silently measure zero.
        static readonly Color32[] SkinRamp =
        {
            new Color32(224, 169, 129, 255),
            new Color32(201, 141, 99, 255),
            new Color32(168, 114, 74, 255),
        };
        const int SkinTolerance = 12;   // per-channel

        /// <summary>What one direction row measures out as.</summary>
        struct RowFace
        {
            public int SkinPixels;      // how much face is visible — peaks facing the camera, ~0 facing away
            public float SkinCentreX;   // where the face sits across the cell
            public float BodyCentreX;   // where the whole body sits, so the face offset is relative
            public float FaceOffset => SkinCentreX - BodyCentreX;   // <0 = face screen-LEFT, >0 = screen-RIGHT
        }

        static RowFace[] _rows;

        static RowFace[] Rows()
        {
            if (_rows != null) return _rows;

            var img = Png.Decode(IdleSheetPath);
            Assert.AreEqual(CellW * IdleFrames, img.Width,
                            $"{IdleSheetPath}: expected {IdleFrames} frames of {CellW} px");
            Assert.AreEqual(CellH * Directions, img.Height,
                            $"{IdleSheetPath}: expected {Directions} rows of {CellH} px");

            _rows = new RowFace[Directions];
            for (int d = 0; d < Directions; d++)
            {
                long skinN = 0, skinSum = 0, bodyN = 0, bodySum = 0;
                for (int f = 0; f < IdleFrames; f++)
                    for (int y = d * CellH + HeadTop; y < d * CellH + HeadBottom; y++)
                        for (int x = 0; x < CellW; x++)
                        {
                            Color32 p = img.At(f * CellW + x, y);
                            if (p.a < 128) continue;
                            bodyN++; bodySum += x;
                            if (!IsSkin(p)) continue;
                            skinN++; skinSum += x;
                        }

                _rows[d] = new RowFace
                {
                    SkinPixels = (int)skinN,
                    SkinCentreX = skinN > 0 ? (float)skinSum / skinN : 0f,
                    BodyCentreX = bodyN > 0 ? (float)bodySum / bodyN : 0f,
                };
            }
            return _rows;
        }

        static bool IsSkin(Color32 p)
        {
            foreach (var s in SkinRamp)
                if (Mathf.Abs(p.r - s.r) <= SkinTolerance &&
                    Mathf.Abs(p.g - s.g) <= SkinTolerance &&
                    Mathf.Abs(p.b - s.b) <= SkinTolerance) return true;
            return false;
        }

        static CharacterVisualDef Visual()
        {
            var def = AssetDatabase.LoadAssetAtPath<CharacterVisualDef>(FisherVisualPath);
            Assert.IsNotNull(def, $"No CharacterVisualDef at '{FisherVisualPath}'. Run Hidden Harbours ▸ " +
                                  "Art ▸ Build Character Visual Defs and commit the asset.");
            return def;
        }

        // ---- the measurement is meaningful at all --------------------------------------------------

        [Test]
        public void SkinRamp_IsStillPresentInTheArt()
        {
            var rows = Rows();
            int total = 0;
            foreach (var r in rows) total += r.SkinPixels;
            Assert.Greater(total, 200,
                "Barely any skin-ramp pixels found in Fisher_idle.png's head band. The palette has moved " +
                "(or the cell has), so every facing assertion below is measuring noise. Re-derive SkinRamp " +
                "from the art before trusting anything else in this file.");
        }

        [Test]
        public void Art_ReadsAsAMirroredPairOfProfiles_SoTheFaceOffsetIsRealSignal()
        {
            var rows = Rows();
            // Rows 1..3 and 7..5 are the same three profiles, mirrored. If the art ever stops being
            // symmetric, the left/right test below stops being a valid read of which way a row looks.
            for (int i = 1; i <= 3; i++)
            {
                var a = rows[i];
                var b = rows[Directions - i];
                Assert.AreEqual(Mathf.Sign(a.FaceOffset), -Mathf.Sign(b.FaceOffset),
                                $"rows {i} and {Directions - i} should look to OPPOSITE sides " +
                                $"(offsets {a.FaceOffset:0.00} vs {b.FaceOffset:0.00})");
            }
        }

        // ---- the load-bearing facing assertions ----------------------------------------------------

        [Test]
        public void NorthHeading_PicksTheRowWithTheLEASTFaceVisible()
        {
            var rows = Rows();
            int quietest = 0;
            for (int d = 1; d < Directions; d++)
                if (rows[d].SkinPixels < rows[quietest].SkinPixels) quietest = d;

            Assert.AreEqual(quietest, Visual().FacingRowFor(0f),
                "Heading 0 (North) must draw the row where the fisher's face is hidden — i.e. the one " +
                "showing the BACK of the head.");
        }

        [Test]
        public void SouthHeading_PicksTheRowWithTheMOSTFaceVisible()
        {
            var rows = Rows();
            int loudest = 0;
            for (int d = 1; d < Directions; d++)
                if (rows[d].SkinPixels > rows[loudest].SkinPixels) loudest = d;

            Assert.AreEqual(loudest, Visual().FacingRowFor(180f),
                "Heading 180 (South) must draw the row facing the camera — the one showing the most face.");
        }

        /// <summary>
        /// ⚠️ THE SABOTAGE TEST. Every EASTERLY heading must land on a row whose face is drawn toward the
        /// screen's RIGHT, and every WESTERLY one on a row facing LEFT. Flipping
        /// <c>FacingsAreCounterClockwise</c> back to true turns all six of these red (and only these — N and
        /// S are their own mirrors, which is exactly why the boat bug hid for so long). That asymmetry is
        /// the whole reason this test is written against PIXELS: six red and two green is the signature of a
        /// mirrored bake, and nothing that asserts a constant against a constant can see it.
        /// </summary>
        [TestCase(45f, +1, "NE")]
        [TestCase(90f, +1, "E")]
        [TestCase(135f, +1, "SE")]
        [TestCase(225f, -1, "SW")]
        [TestCase(270f, -1, "W")]
        [TestCase(315f, -1, "NW")]
        public void EastWestHeadings_PickRowsWhoseFaceLooksTheRightWay(float heading, int expectedSign,
                                                                      string label)
        {
            var rows = Rows();
            int row = Visual().FacingRowFor(heading);
            float offset = rows[row].FaceOffset;

            Assert.AreEqual(expectedSign, (int)Mathf.Sign(offset),
                $"Heading {heading}° ({label}) resolved to row {row}, whose face is drawn toward the " +
                $"screen {(offset < 0f ? "LEFT" : "RIGHT")} (offset {offset:0.00} px). A {label} heading " +
                $"must show a fisher looking to the {(expectedSign > 0 ? "RIGHT" : "LEFT")}. If this is " +
                "red, the art's bake direction and the def's FacingsAreCounterClockwise flag disagree.");
        }

        [Test]
        public void TheFisherSkin_DeclaresTheClockwiseBake_MatchingTheReBakedArt()
        {
            var def = Visual();
            Assert.IsFalse(def.FacingsAreCounterClockwise,
                "The Fisher rig was FIXED at source (th = −dir·45°) and all twelve body sheets re-baked, so " +
                "the rows now run clockwise exactly as labelled (N · NE · E · SE · S · SW · W · NW) and no " +
                "un-mirror is wanted. Leaving this true on the corrected art double-flips it — a fresh 180° " +
                "error at E/W. ⚠️ The BOAT kits were NOT re-baked: BoatVisualDef keeps its flag true. This " +
                "is per-artwork data; the two lineages are allowed to disagree.");
        }

        /// <summary>
        /// The flag is not the evidence — the PIXELS are. This re-derives the bake direction from the art
        /// alone and asserts the def agrees, so the two can never drift apart again without a red test.
        /// </summary>
        [Test]
        public void TheDeclaredBakeDirection_MatchesWhatTheArtActuallyShows()
        {
            var rows = Rows();

            // Row 2 and row 6 are the two rows a mirror actually swaps and the two the compass labels call
            // E and W. If the art is CLOCKWISE-as-labelled, row 2 (E) looks screen-RIGHT and row 6 (W)
            // looks screen-LEFT. If it is counter-clockwise, they are the other way round.
            bool artIsClockwise = rows[2].FaceOffset > 0f && rows[6].FaceOffset < 0f;
            bool artIsCounterClockwise = rows[2].FaceOffset < 0f && rows[6].FaceOffset > 0f;

            Assert.IsTrue(artIsClockwise ^ artIsCounterClockwise,
                $"rows 2 and 6 must look to OPPOSITE sides for this to be a valid read " +
                $"(offsets {rows[2].FaceOffset:0.00} and {rows[6].FaceOffset:0.00})");

            Assert.AreEqual(artIsCounterClockwise, Visual().FacingsAreCounterClockwise,
                $"The art measures as {(artIsCounterClockwise ? "COUNTER-CLOCKWISE" : "CLOCKWISE")} " +
                $"(row 2 face offset {rows[2].FaceOffset:0.00}, row 6 {rows[6].FaceOffset:0.00}) but the " +
                "def declares the opposite. The sheets and the flag MUST change together in one commit — " +
                "either alone leaves every character mirrored.");
        }

        // ---- the def is complete + shaped like the art ---------------------------------------------

        [Test]
        public void FisherVisual_IsCompleteAndMatchesTheArtsOwnFrameCounts()
        {
            var def = Visual();
            Assert.AreEqual("visual.fisher_iso", def.Id);
            Assert.AreEqual(Directions, def.FacingCount);
            Assert.AreEqual(0f, def.ZeroHeadingDegrees, 1e-4f, "row 0 is the North-facing picture");

            // Frame counts DERIVED from the PNGs on disk, not asserted against the builder's constants.
            Assert.AreEqual(Png.Decode(IdleSheetPath).Width / CellW, def.IdleFrameCount);
            Assert.AreEqual(Png.Decode("Assets/_Project/Art/Characters/Iso/Fisher_walk.png").Width / CellW,
                            def.WalkFrameCount);
            Assert.AreEqual(Png.Decode("Assets/_Project/Art/Characters/Iso/Fisher_run.png").Width / CellW,
                            def.RunFrameCount);

            Assert.IsTrue(def.HasGait(CharacterGait.Idle), "idle sheet incomplete — re-run the builder");
            Assert.IsTrue(def.HasGait(CharacterGait.Walk), "walk sheet incomplete — re-run the builder");
            Assert.IsTrue(def.HasGait(CharacterGait.Run), "run sheet incomplete — re-run the builder");
        }

        [Test]
        public void GaitThresholds_LeaveTheOrdinaryWalkAWalk()
        {
            var def = Visual();
            Assert.Greater(def.WalkSpeedThreshold, 0f, "a dead-band is needed or jitter twitches the idle");
            Assert.Greater(def.RunSpeedThreshold, 3f,
                "the run threshold must sit ABOVE the on-foot walk speed (3 m/s) or the fisher sprints " +
                "everywhere; there is no sprint input yet, so the run sheet is wired but dormant by design.");
        }

        [Test]
        public void EveryDirectionRow_ResolvesFromSomeHeading_AndNoneTwice()
        {
            var def = Visual();
            var hit = new bool[Directions];
            for (int d = 0; d < Directions; d++)
                hit[def.FacingRowFor(d * 45f)] = true;
            for (int d = 0; d < Directions; d++)
                Assert.IsTrue(hit[d], $"row {d} is unreachable — the mapping has collapsed onto fewer cells");
        }

        // ==== a minimal, device-free PNG reader ==========================================================

        /// <summary>
        /// Just enough PNG to measure the art: 8-bit RGBA, non-interlaced — which is what these sheets are.
        /// Deliberately no Unity texture APIs (see the class remarks). Anything else throws rather than
        /// guessing, so an art re-export in another format fails loudly instead of measuring garbage.
        /// </summary>
        class Png
        {
            public int Width, Height;
            byte[] _rgba;

            public Color32 At(int x, int y)
            {
                int i = (y * Width + x) * 4;
                return new Color32(_rgba[i], _rgba[i + 1], _rgba[i + 2], _rgba[i + 3]);
            }

            public static Png Decode(string assetPath)
            {
                string full = Path.GetFullPath(assetPath);
                Assert.IsTrue(File.Exists(full), $"{assetPath}: not on disk");
                byte[] bytes = File.ReadAllBytes(full);

                int w = 0, h = 0;
                var idat = new MemoryStream();
                int pos = 8;   // skip the 8-byte signature
                while (pos + 8 <= bytes.Length)
                {
                    int len = BE(bytes, pos);
                    string type = System.Text.Encoding.ASCII.GetString(bytes, pos + 4, 4);
                    int data = pos + 8;

                    if (type == "IHDR")
                    {
                        w = BE(bytes, data);
                        h = BE(bytes, data + 4);
                        Assert.AreEqual(8, bytes[data + 8], $"{assetPath}: only 8-bit PNG is supported");
                        Assert.AreEqual(6, bytes[data + 9], $"{assetPath}: only RGBA (colour type 6) is supported");
                        Assert.AreEqual(0, bytes[data + 12], $"{assetPath}: interlaced PNG is not supported");
                    }
                    else if (type == "IDAT") idat.Write(bytes, data, len);
                    else if (type == "IEND") break;

                    pos = data + len + 4;   // + CRC
                }
                Assert.Greater(w * h, 0, $"{assetPath}: no IHDR found");

                // zlib stream: skip the 2-byte header, inflate the rest.
                byte[] z = idat.ToArray();
                var raw = new MemoryStream();
                using (var ms = new MemoryStream(z, 2, z.Length - 2))
                using (var inflate = new DeflateStream(ms, CompressionMode.Decompress))
                    inflate.CopyTo(raw);

                return Unfilter(raw.ToArray(), w, h);
            }

            /// <summary>Undo the per-scanline PNG filters (None / Sub / Up / Average / Paeth).</summary>
            static Png Unfilter(byte[] src, int w, int h)
            {
                const int bpp = 4;
                int stride = w * bpp;
                var outp = new byte[w * h * bpp];
                int s = 0;
                for (int y = 0; y < h; y++)
                {
                    int filter = src[s++];
                    int row = y * stride;
                    int prev = (y - 1) * stride;
                    for (int i = 0; i < stride; i++)
                    {
                        int x = src[s + i];
                        int a = i >= bpp ? outp[row + i - bpp] : 0;
                        int b = y > 0 ? outp[prev + i] : 0;
                        int c = (i >= bpp && y > 0) ? outp[prev + i - bpp] : 0;
                        int v = filter switch
                        {
                            0 => x,
                            1 => x + a,
                            2 => x + b,
                            3 => x + ((a + b) >> 1),
                            4 => x + Paeth(a, b, c),
                            _ => throw new IOException($"unknown PNG filter {filter} on row {y}"),
                        };
                        outp[row + i] = (byte)v;
                    }
                    s += stride;
                }
                return new Png { Width = w, Height = h, _rgba = outp };
            }

            static int Paeth(int a, int b, int c)
            {
                int p = a + b - c, pa = Mathf.Abs(p - a), pb = Mathf.Abs(p - b), pc = Mathf.Abs(p - c);
                return (pa <= pb && pa <= pc) ? a : (pb <= pc ? b : c);
            }

            static int BE(byte[] d, int i) => (d[i] << 24) | (d[i + 1] << 16) | (d[i + 2] << 8) | d[i + 3];
        }
    }
}
