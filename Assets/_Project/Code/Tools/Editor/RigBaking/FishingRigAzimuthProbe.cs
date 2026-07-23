using System;
using System.Globalization;
using System.Text;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Measures the azimuth convention of the fishing kit's two DIRECTIONAL rigs — the parametric
    /// fish (<c>FishIso</c>) and the rod (<c>RodIso</c>) — from rendered pixels. (The bobber has no
    /// azimuth term at all and is never probed.)
    ///
    /// Neither existing probe fits these rigs, for measured reasons rather than taste:
    ///
    /// <list type="bullet">
    /// <item><see cref="RigAzimuthProbe"/> (boats: PCA + bow-taper) assumes the TAPERED end of the
    /// silhouette points at the heading. A fish is elongated like a hull, but its girth curve peaks
    /// forward (max at station u=0.28) and tapers hardest at the TAIL — the tapered end points
    /// <i>away</i> from the heading, so the boat tie-break would return a confidently reversed
    /// answer. The rod at rest is a near-1-px line; PCA of a line "works" but the taper test reads
    /// dither.</item>
    /// <item><see cref="CharacterRigAzimuthProbe"/> (face-skin centroid) needs a SKINS table and a
    /// projected head anchor. A fish has neither.</item>
    /// </list>
    ///
    /// WHAT IS MEASURED INSTEAD — one principle, two shapes:
    ///
    /// <para><b>Fish: the head side carries the mass.</b> Rendered DRY (<c>waterZ:null</c>, so no
    /// translucent underwater alpha) with every pose override zeroed (no sweep/curve/roll — a bent
    /// tail would smear the signal), the fish is a horizontal profile at the rows labelled E and W.
    /// Two facts of its own loft put the opaque-pixel centroid on the HEAD side of the silhouette's
    /// horizontal midpoint: girth peaks at u=0.28 (mass forward — body centroid lands at u≈0.44),
    /// and the thin tail fan extends the BBOX tailward without adding meaningful mass. At the row
    /// labelled East a genuinely clockwise rig faces the head screen-RIGHT, so centroid−midpoint is
    /// positive; at West, negative. A counter-clockwise (mislabelled) rig is the exact mirror. The
    /// probe renders at <c>scale 2</c> (cod) purely to grow the signal while still fitting the
    /// 64 px cell. MEASURED against a faithful port of the rig's own loft+rasteriser: the offset
    /// is exactly mirrored at ±1.95 px (418 opaque px per cell; ±0.95 px at scale 1) — small,
    /// because the head-mass and tail-bbox effects partly cancel, but perfectly consistent, so the
    /// floor sits at 0.75 px and the mirrored-signs requirement carries the defence.</para>
    ///
    /// <para><b>Rod: the blank extends toward the heading.</b> At <c>rest:'ground'</c> the rod lies
    /// flat along the character-forward axis: grip at the pivot, the 0.95–1.55 m blank entirely on
    /// the heading side. The opaque centroid relative to the PIVOT COLUMN is therefore a huge signal
    /// (~+20 px for the deepwater tier) — screen-right at the row labelled East iff the rig is
    /// clockwise.</para>
    ///
    /// <para><b>Anchor cross-check, because the anchors are the deliverable.</b> The fight pins the
    /// line to <c>FishIso.mouth()</c> and draws it from <c>RodIso.tip()</c>; a mouth/tip that
    /// disagrees with the rendered pixels would poison every exported anchor even if the sheets came
    /// out right. So after deciding the convention from pixels, the probe asserts the rig's own
    /// anchor lands on the measured head/tip side at both rows — and throws rather than bakes when
    /// it does not.</para>
    ///
    /// Like both siblings: the caller cross-checks the measurement against the catalog declaration
    /// and REFUSES to bake on a mismatch rather than silently picking a side.
    /// </summary>
    public static class FishingRigAzimuthProbe
    {
        /// <summary>Alpha threshold for "this pixel is artwork" — the rigs lay a 1 px keyline and
        /// ordered dither, and the DRY renders used here are fully opaque, so mid avoids fringe.</summary>
        const byte AlphaThreshold = 128;

        /// <summary>Minimum opaque pixels before a silhouette is trusted at all.</summary>
        const int MinOpaquePixels = 40;

        /// <summary>Fish floor: measured ±1.95 px at probe scale (see the class remarks) — 0.75
        /// keeps a real margin under it without dipping into dither noise, and the
        /// opposite-signs-at-E-and-W requirement is the primary defence.</summary>
        const double FishMinOffsetPx = 0.75;

        /// <summary>Rod floor: the ground-rest blank puts nearly the whole silhouette on the
        /// heading side of the grip (~+20 px for the deepwater tier), so the floor can be an
        /// order of magnitude stiffer.</summary>
        const double RodMinOffsetPx = 5.0;

        /// <summary>One measured cell: opaque mass and horizontal extent.</summary>
        public readonly struct SilhouetteRead
        {
            public readonly int OpaquePixels;
            public readonly double CentroidX;
            public readonly int MinX, MaxX;

            /// <summary>Centroid x − horizontal bbox midpoint. The fish's "which side is the
            /// head" number.</summary>
            public double MassOffset => OpaquePixels > 0 ? CentroidX - (MinX + MaxX) / 2.0 : 0.0;

            public SilhouetteRead(int opaquePixels, double centroidX, int minX, int maxX)
            {
                OpaquePixels = opaquePixels; CentroidX = centroidX; MinX = minX; MaxX = maxX;
            }
        }

        public readonly struct Result
        {
            public readonly AzimuthConvention Convention;
            public readonly SilhouetteRead East, West;   // at the rows LABELLED East / West
            public readonly string Report;

            public Result(AzimuthConvention convention, SilhouetteRead east, SilhouetteRead west,
                          string report)
            {
                Convention = convention; East = east; West = west; Report = report;
            }
        }

        /// <summary>
        /// The pure measurement: opaque centroid + horizontal extent of one rendered cell.
        /// Engine-free (bytes in, numbers out) so it is testable without a script host.
        /// </summary>
        public static SilhouetteRead ReadSilhouette(byte[] rgba, int width, int height)
        {
            if (rgba == null) throw new ArgumentNullException(nameof(rgba));
            if (rgba.Length != width * height * 4)
                throw new ArgumentException(
                    $"Buffer is {rgba.Length} bytes, expected {width * height * 4} for {width}×{height} RGBA.");

            long n = 0; double sumX = 0; int minX = int.MaxValue, maxX = int.MinValue;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (rgba[(y * width + x) * 4 + 3] < AlphaThreshold) continue;
                n++; sumX += x;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
            }
            return n > 0
                ? new SilhouetteRead((int)n, sumX / n, minX, maxX)
                : new SilhouetteRead(0, 0, 0, 0);
        }

        // ---- the fish -----------------------------------------------------------------------

        /// <summary>Species used for the probe: the longest body = the strongest mass signal. The
        /// convention is a property of the rig's camera basis, identical for every species.</summary>
        public const string FishProbeSpecies = "cod";

        /// <summary>Probe-only render scale. Grows the signal (~2× the pixels) while the scaled cod
        /// still fits the 64 px cell (tail extreme ≈ 28.5 px from the pivot column at 32).</summary>
        public const double FishProbeScale = 2.0;

        /// <summary>
        /// Decides the fish rig's convention from where the head-side mass sits at the rows
        /// labelled E and W, then cross-checks <c>mouth()</c> against the measured head side.
        /// <paramref name="dirs"/> is the bake recipe's facing count (the rig declares no DIRS).
        /// </summary>
        public static Result MeasureFish(IRigScriptHost host, string globalName, in RigGeometry geo,
                                         int dirs)
        {
            string g = globalName;
            int e = dirs / 4, w = 3 * dirs / 4;

            // Dry, pose-zeroed profile render. waterZ:null switches the underwater tint (and its
            // sub-threshold alpha) off; the explicit zero overrides kill the swim-frame tail sweep.
            string opts = "{species:'" + FishProbeSpecies + "',scale:" + Num(FishProbeScale) +
                          ",waterZ:null,sweep:0,curve:0,roll:0,pitch:0,z:0}";

            SilhouetteRead east = RenderAndRead(host, g, geo, e, opts);
            SilhouetteRead west = RenderAndRead(host, g, geo, w, opts);

            var sb = new StringBuilder();
            sb.AppendLine($"fish probe: species {FishProbeSpecies}, scale {FishProbeScale}, dry pose-zeroed profile");
            sb.AppendLine($"row labelled E (dir {e}): {east.OpaquePixels} px, head-mass offset {east.MassOffset:F2} px");
            sb.AppendLine($"row labelled W (dir {w}): {west.OpaquePixels} px, head-mass offset {west.MassOffset:F2} px");

            var convention = Decide(east.MassOffset, west.MassOffset, FishMinOffsetPx, east, west, sb,
                "the head-mass offsets at the E and W rows do not mirror — either the loft changed " +
                "shape (girth peak / tail fan moved) or the measurement no longer fits this art");

            // mouth() must land on the measured head side — it is the fight's line-attach anchor.
            double mouthEx = host.EvaluateNumber($"{g}.mouth({e},{opts}).dx");
            double mouthWx = host.EvaluateNumber($"{g}.mouth({w},{opts}).dx");
            sb.AppendLine($"mouth().dx at E/W rows: {mouthEx:F1} / {mouthWx:F1} px");
            bool headIsRightAtE = east.MassOffset > 0;
            if ((mouthEx > 0) != headIsRightAtE || (mouthWx > 0) == headIsRightAtE)
                throw new InvalidOperationException(
                    "FISH PROBE: mouth() contradicts the rendered pixels.\n" + sb +
                    "The pixels put the head on one side and the rig's own mouth anchor on the " +
                    "other. Baking would ship sheets whose exported line-attach points are on the " +
                    "wrong end of the fish. This is a rig defect — flag it to the art director's " +
                    "workspace; do not paper over it host-side.");

            sb.Append($"=> MEASURED CONVENTION: {convention}");
            return new Result(convention, east, west, sb.ToString());
        }

        // ---- the rod ------------------------------------------------------------------------

        /// <summary>Tier used for the probe: the longest blank = the strongest signal.</summary>
        public const string RodProbeTier = "deep";

        /// <summary>
        /// Decides the rod rig's convention from which side of the grip column the blank extends
        /// at <c>rest:'ground'</c>, then cross-checks <c>tip()</c> against the measured side.
        /// </summary>
        public static Result MeasureRod(IRigScriptHost host, string globalName, in RigGeometry geo,
                                        int dirs)
        {
            string g = globalName;
            int e = dirs / 4, w = 3 * dirs / 4;
            string opts = "{tier:'" + RodProbeTier + "',rest:'ground'}";

            SilhouetteRead east = RenderAndRead(host, g, geo, e, opts);
            SilhouetteRead west = RenderAndRead(host, g, geo, w, opts);

            // For the rod the reference is the PIVOT column (the grip), not the bbox midpoint:
            // the blank is nearly all of the extent AND nearly all of the mass, both on the
            // heading side of the grip.
            double eOff = east.CentroidX - geo.PivotX;
            double wOff = west.CentroidX - geo.PivotX;

            var sb = new StringBuilder();
            sb.AppendLine($"rod probe: tier {RodProbeTier}, rest:'ground' (flat along the heading)");
            sb.AppendLine($"row labelled E (dir {e}): {east.OpaquePixels} px, blank offset from grip {eOff:F2} px");
            sb.AppendLine($"row labelled W (dir {w}): {west.OpaquePixels} px, blank offset from grip {wOff:F2} px");

            var convention = Decide(eOff, wOff, RodMinOffsetPx, east, west, sb,
                "the blank does not extend to mirrored sides of the grip at the E and W rows — " +
                "either rest:'ground' stopped lying flat along the heading or the grip moved off " +
                "the pivot");

            double tipEx = host.EvaluateNumber($"{g}.tip({e},{opts}).x") - geo.PivotX;
            double tipWx = host.EvaluateNumber($"{g}.tip({w},{opts}).x") - geo.PivotX;
            sb.AppendLine($"tip().x − grip at E/W rows: {tipEx:F1} / {tipWx:F1} px");
            if ((tipEx > 0) != (eOff > 0) || (tipWx > 0) != (wOff > 0))
                throw new InvalidOperationException(
                    "ROD PROBE: tip() contradicts the rendered pixels.\n" + sb +
                    "The pixels put the blank on one side of the grip and the rig's own tip anchor " +
                    "on the other. The line draws from tip() — baking would ship line FX anchored " +
                    "off the wrong end. This is a rig defect — flag it upstream, do not shim it.");

            sb.Append($"=> MEASURED CONVENTION: {convention}");
            return new Result(convention, east, west, sb.ToString());
        }

        // ---- shared -------------------------------------------------------------------------

        static SilhouetteRead RenderAndRead(IRigScriptHost host, string g, in RigGeometry geo,
                                            int dir, string optsJs)
        {
            byte[] rgba = host.EvaluateBytes(
                $"{g}.render({dir.ToString(CultureInfo.InvariantCulture)},{optsJs})");
            if (rgba.Length != geo.Width * geo.Height * 4)
                throw new InvalidOperationException(
                    $"Probe render at dir {dir} came back {rgba.Length} bytes, expected " +
                    $"{geo.Width * geo.Height * 4} for {geo.Width}×{geo.Height} RGBA.");
            return ReadSilhouette(rgba, geo.Width, geo.Height);
        }

        static AzimuthConvention Decide(double eOff, double wOff, double minOffsetPx,
                                        in SilhouetteRead east, in SilhouetteRead west,
                                        StringBuilder sb, string mirrorFailureHint)
        {
            if (east.OpaquePixels < MinOpaquePixels || west.OpaquePixels < MinOpaquePixels)
                throw new InvalidOperationException(
                    "FISHING PROBE INCONCLUSIVE — too little silhouette to measure.\n" + sb +
                    "Do not bake until the probe reads real artwork.");

            bool eRight = eOff > +minOffsetPx, eLeft = eOff < -minOffsetPx;
            bool wRight = wOff > +minOffsetPx, wLeft = wOff < -minOffsetPx;

            if (eRight && wLeft) return AzimuthConvention.Clockwise;
            if (eLeft && wRight) return AzimuthConvention.CounterClockwise;

            throw new InvalidOperationException(
                "FISHING PROBE INCONCLUSIVE — the East and West rows do not read to opposite " +
                "sides.\n" + sb + mirrorFailureHint +
                ". Do not bake until this is understood.");
        }

        static string Num(double d) => d.ToString("R", CultureInfo.InvariantCulture);
    }
}
