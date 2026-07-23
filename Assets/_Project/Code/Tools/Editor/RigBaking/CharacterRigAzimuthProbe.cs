using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Measures a CHARACTER rig's azimuth convention from rendered pixels — the character
    /// counterpart of <see cref="RigAzimuthProbe"/>, which must NOT be reused here.
    ///
    /// Why the boat probe cannot serve: both of its steps read noise on a humanoid. (1) The PCA
    /// long axis of a standing figure is the VERTICAL body axis at every heading — there is no
    /// broadside where the silhouette elongates along the heading. (2) A person has no bow taper;
    /// head and feet are both "blunt ends", so the taper tie-break resolves arbitrarily. Feeding a
    /// character through <see cref="RigAzimuthProbe.MeasureFromQuarterTurn"/> yields a confident
    /// nonsense answer, which is worse than no answer.
    ///
    /// The measurement used instead is the one <c>CharacterIsoFacingTests</c> already proved on the
    /// shipped Fisher art: WHERE THE FACE SITS. In the head band of a cell, count the pixels of the
    /// rig's own skin ramp and compare their centroid with the body centroid. A character drawn
    /// looking screen-RIGHT carries its face right of its body; screen-LEFT, left of it. Rendered at
    /// the row LABELLED East (dir = DIRS/4), a genuinely clockwise rig therefore shows the face
    /// RIGHT of the body, and at the row labelled West (dir = 3·DIRS/4) it shows the face LEFT. A
    /// counter-clockwise (mislabelled) rig is exactly the mirror — the same left/right swap that
    /// shipped six of the owner's ten playtest defects on the boat kits.
    ///
    /// Nothing here is read from a README: the skin ramp comes from the rig's own SKINS table (via
    /// its default build), and the head band is centred on the rig's own projected head anchor.
    /// Like the boat probe, the caller cross-checks the measurement against the catalog declaration
    /// and REFUSES to bake on a mismatch rather than silently picking a side.
    /// </summary>
    public static class CharacterRigAzimuthProbe
    {
        /// <summary>Alpha threshold for "this pixel is body" — same rationale as the boat probe:
        /// the rigs lay a 1px keyline and use ordered dither, so a mid threshold avoids fringe.</summary>
        const byte AlphaThreshold = 128;

        /// <summary>Per-channel tolerance when matching a pixel against the skin ramp. Wide enough
        /// to survive the depth-edge darkening pass, narrow enough that hair/outfit ramps do not
        /// register (verified against the rig's palettes: the nearest non-skin shade is 13+ per
        /// channel away).</summary>
        const int SkinTolerance = 12;

        /// <summary>Head band, relative to the projected head anchor: the anchor sits just above
        /// the crown (headC z+0.17), so the face occupies the band below it. −5..+15 lands on the
        /// same 20-or-so-px head band CharacterIsoFacingTests measured (rows 40..60 of the idle
        /// cell) — MEASURED against the live rig: widening it to +20 dilutes the face offset with
        /// shoulder pixels and roughly halves the signal.</summary>
        const int BandAbovePx = 5, BandBelowPx = 15;

        /// <summary>Minimum AGGREGATE skin pixels (across all probed frames) in a profile row
        /// before the offset is trusted. Below this the face is effectively hidden and the sign is
        /// dither noise. Measured on the live rig: a profile row reads ~100 over the six idle
        /// frames, so 60 fails loudly on a palette drift without flaking on dither.</summary>
        const int MinSkinPixels = 60;

        /// <summary>Minimum |face offset| (px) before a sign is believed. The live rig measures
        /// ~±0.8 px on the profile rows (the head band holds hair on both sides, so the offset is
        /// small but consistent); 0.3 keeps a real margin under it, and the opposite-signs
        /// requirement below is the primary defence against noise.</summary>
        const double MinOffsetPx = 0.3;

        /// <summary>
        /// One measured cell (or several combined — the sums are carried so frames aggregate
        /// exactly rather than averaging averages).
        /// </summary>
        public readonly struct FaceRead
        {
            /// <summary>Skin-ramp pixels found in the head band.</summary>
            public readonly int SkinPixels;
            /// <summary>Opaque pixels found in the head band.</summary>
            public readonly int BodyPixels;
            readonly double _skinSumX, _bodySumX;

            /// <summary>Skin centroid x − body centroid x, in px. &lt; 0 = face screen-LEFT,
            /// &gt; 0 = screen-RIGHT.</summary>
            public double FaceOffset =>
                (SkinPixels > 0 && BodyPixels > 0)
                    ? _skinSumX / SkinPixels - _bodySumX / BodyPixels
                    : 0.0;

            public FaceRead(int skinPixels, double skinSumX, int bodyPixels, double bodySumX)
            {
                SkinPixels = skinPixels; _skinSumX = skinSumX;
                BodyPixels = bodyPixels; _bodySumX = bodySumX;
            }

            public static FaceRead operator +(FaceRead a, FaceRead b) =>
                new FaceRead(a.SkinPixels + b.SkinPixels, a._skinSumX + b._skinSumX,
                             a.BodyPixels + b.BodyPixels, a._bodySumX + b._bodySumX);
        }

        public readonly struct Result
        {
            public readonly AzimuthConvention Convention;
            public readonly FaceRead East, West;   // at the rows LABELLED East / West
            public readonly string Report;

            public Result(AzimuthConvention convention, FaceRead east, FaceRead west, string report)
            {
                Convention = convention; East = east; West = west; Report = report;
            }
        }

        /// <summary>
        /// The pure measurement: face offset within the head band of one rendered cell.
        /// Kept engine-free (bytes in, numbers out) so it is testable without a script host.
        /// </summary>
        public static FaceRead MeasureFaceOffset(byte[] rgba, int width, int height,
                                                 double headAnchorY, IReadOnlyList<Color32> skinRamp)
        {
            if (rgba == null) throw new ArgumentNullException(nameof(rgba));
            if (rgba.Length != width * height * 4)
                throw new ArgumentException(
                    $"Buffer is {rgba.Length} bytes, expected {width * height * 4} for {width}×{height} RGBA.");
            if (skinRamp == null || skinRamp.Count == 0)
                throw new ArgumentException("An empty skin ramp can only ever measure zero face.",
                                            nameof(skinRamp));

            int yLo = Math.Max(0, (int)Math.Floor(headAnchorY) - BandAbovePx);
            int yHi = Math.Min(height - 1, (int)Math.Ceiling(headAnchorY) + BandBelowPx);

            long skinN = 0, bodyN = 0;
            double skinSum = 0, bodySum = 0;
            for (int y = yLo; y <= yHi; y++)
            for (int x = 0; x < width; x++)
            {
                int i = (y * width + x) * 4;
                if (rgba[i + 3] < AlphaThreshold) continue;
                bodyN++; bodySum += x;
                if (!IsSkin(rgba[i], rgba[i + 1], rgba[i + 2], skinRamp)) continue;
                skinN++; skinSum += x;
            }

            return new FaceRead((int)skinN, skinSum, (int)bodyN, bodySum);
        }

        static bool IsSkin(byte r, byte g, byte b, IReadOnlyList<Color32> ramp)
        {
            for (int i = 0; i < ramp.Count; i++)
            {
                Color32 s = ramp[i];
                if (Math.Abs(r - s.r) <= SkinTolerance &&
                    Math.Abs(g - s.g) <= SkinTolerance &&
                    Math.Abs(b - s.b) <= SkinTolerance) return true;
            }
            return false;
        }

        /// <summary>
        /// Renders the rig's East- and West-LABELLED rows and decides the convention from where the
        /// face sits. Throws rather than guessing when the signal is weak or self-contradictory.
        /// </summary>
        public static Result Measure(IRigScriptHost host, string globalName, in RigGeometry geo)
        {
            string g = globalName;

            // The rig's own default-build skin ramp — never a hand-copied table. The mid shades are
            // used (darkest two and lightest one dropped): they are what the renderer actually lays
            // on a lit profile face, and dropping the extremes keeps the darkest skin shade clear of
            // the hair/boot palettes.
            var ramp = ReadDefaultSkinMidRamp(host, g);

            int e = geo.NativeDirs / 4;          // the row LABELLED East
            int w = 3 * geo.NativeDirs / 4;      // the row LABELLED West

            FaceRead east = ReadFaceAt(host, g, geo, e, ramp);
            FaceRead west = ReadFaceAt(host, g, geo, w, ramp);

            var sb = new StringBuilder();
            sb.AppendLine($"skin ramp (mid shades of the rig's default build): {ramp.Count} colours");
            sb.AppendLine($"row labelled E (dir {e}): {east.SkinPixels} skin px, face offset {east.FaceOffset:F2} px");
            sb.AppendLine($"row labelled W (dir {w}): {west.SkinPixels} skin px, face offset {west.FaceOffset:F2} px");

            if (east.SkinPixels < MinSkinPixels || west.SkinPixels < MinSkinPixels)
                throw new InvalidOperationException(
                    "CHARACTER AZIMUTH PROBE INCONCLUSIVE — too little face to measure.\n" + sb +
                    "Either the palette moved (re-derive the ramp from the rig) or the head band " +
                    "missed the head. Do not bake until the probe reads a real face.");

            bool eRight = east.FaceOffset > +MinOffsetPx;
            bool eLeft  = east.FaceOffset < -MinOffsetPx;
            bool wRight = west.FaceOffset > +MinOffsetPx;
            bool wLeft  = west.FaceOffset < -MinOffsetPx;

            AzimuthConvention convention;
            if (eRight && wLeft)      convention = AzimuthConvention.Clockwise;
            else if (eLeft && wRight) convention = AzimuthConvention.CounterClockwise;
            else
                throw new InvalidOperationException(
                    "CHARACTER AZIMUTH PROBE INCONCLUSIVE — the East and West rows do not look to " +
                    "opposite sides.\n" + sb +
                    "A valid read needs mirrored profiles; anything else means the art or the " +
                    "measurement changed shape. Do not bake until this is understood.");

            sb.Append($"=> MEASURED CONVENTION: {convention}");
            return new Result(convention, east, west, sb.ToString());
        }

        /// <summary>
        /// Reads the face across EVERY idle frame, summed — the same aggregation
        /// CharacterIsoFacingTests uses. One frame's ~25 skin px is a workable but thin signal;
        /// six frames' ~100 px is a solid one (measured on the live rig).
        /// </summary>
        static FaceRead ReadFaceAt(IRigScriptHost host, string g, in RigGeometry geo, int dir,
                                   IReadOnlyList<Color32> ramp)
        {
            string d = dir.ToString(CultureInfo.InvariantCulture);
            int idleFrames = Math.Max(1, (int)host.EvaluateNumber($"{g}.ANIMS.idle.frames"));

            var total = new FaceRead();
            for (int f = 0; f < idleFrames; f++)
            {
                double headY = host.EvaluateNumber($"{g}.anchors({d},{{frame:{f}}}).head.y");
                byte[] rgba = host.EvaluateBytes($"{g}.render({d},{{frame:{f}}})");
                if (rgba.Length != geo.Width * geo.Height * 4)
                    throw new InvalidOperationException(
                        $"Probe render at dir {dir} frame {f} came back {rgba.Length} bytes, " +
                        $"expected {geo.Width * geo.Height * 4} for {geo.Width}×{geo.Height} RGBA.");
                total += MeasureFaceOffset(rgba, geo.Width, geo.Height, headY, ramp);
            }
            return total;
        }

        /// <summary>Mid shades of the rig's default-build skin ramp, read from the rig itself.</summary>
        static IReadOnlyList<Color32> ReadDefaultSkinMidRamp(IRigScriptHost host, string g)
        {
            // Default build is what render() falls back to with no opts.build — resolve the same way.
            string json = host.EvaluateString(
                $"JSON.stringify({g}.SKINS[{g}.BUILDS.fisher.skin])");
            var hex = ParseJsonStringArray(json);
            if (hex.Count < 3)
                throw new InvalidOperationException(
                    $"The rig's default skin ramp has only {hex.Count} shades — nothing to measure with.");

            // Drop the darkest third and the single lightest shade; keep the lit mid-face shades.
            int lo = hex.Count / 3;
            int hi = hex.Count - 2;
            var ramp = new List<Color32>();
            for (int i = lo; i <= hi; i++) ramp.Add(HexColor(hex[i]));
            return ramp;
        }

        static List<string> ParseJsonStringArray(string json)
        {
            // The input is JSON.stringify of an array of '#rrggbb' strings — no escapes, no nesting.
            var outp = new List<string>();
            int i = 0;
            while (i < json.Length)
            {
                if (json[i] == '"')
                {
                    int end = json.IndexOf('"', i + 1);
                    if (end < 0) break;
                    outp.Add(json.Substring(i + 1, end - i - 1));
                    i = end + 1;
                }
                else i++;
            }
            return outp;
        }

        static Color32 HexColor(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex[0] != '#' || hex.Length < 7)
                throw new FormatException($"'{hex}' is not a #rrggbb colour.");
            byte r = byte.Parse(hex.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte g = byte.Parse(hex.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte b = byte.Parse(hex.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new Color32(r, g, b, 255);
        }
    }
}
