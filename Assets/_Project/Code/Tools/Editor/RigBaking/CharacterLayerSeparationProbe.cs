using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// One structural axis measured at one pose: what changing it does to the rendered pixels.
    /// </summary>
    public sealed class LayerSeparationRead
    {
        /// <summary>Build key under test — <c>garment</c>, <c>hat</c>, <c>hairStyle</c>, <c>beard</c>.</summary>
        public string Axis;
        /// <summary>The two values compared (the bare value first).</summary>
        public string From, To;
        /// <summary>The pose: direction row, anim, frame.</summary>
        public int Dir, Frame;
        public string Anim;

        /// <summary>Pixels whose RGBA differs between the two renders.</summary>
        public int DeltaPixels;
        /// <summary>Of those, how many were TRANSPARENT before and opaque after — pure addition, the
        /// only kind an overlay can express.</summary>
        public int AddedPixels;
        /// <summary>Of those, how many were already opaque and got a DIFFERENT colour — the garment
        /// covering the body, or the body's own shading moving underneath it. Indistinguishable from
        /// outside, which is the whole difficulty.</summary>
        public int RepaintedPixels;
        /// <summary>Of those, how many were opaque before and TRANSPARENT after — the change ate
        /// silhouette. An overlay cannot subtract.</summary>
        public int ErasedPixels;

        /// <summary>Delta bounding box in cell px, top-left origin.</summary>
        public int MinX, MinY, MaxX, MaxY;

        /// <summary>Delta pixels landing in the HEAD band (the face). A garment reaching up here means
        /// the renderer is not compositing parts — it is re-solving the whole figure.</summary>
        public int DeltaInHeadBand;

        public bool Any => DeltaPixels > 0;

        public override string ToString() =>
            $"{Axis} {From}→{To} @dir{Dir} {Anim}f{Frame}: {DeltaPixels} delta " +
            $"({AddedPixels} added, {RepaintedPixels} repainted, {ErasedPixels} erased), " +
            $"bbox x{MinX}..{MaxX} y{MinY}..{MaxY}, {DeltaInHeadBand} in the head band";
    }

    /// <summary>
    /// One axis's INDEPENDENCE result: is the change this axis makes the same change regardless of
    /// the rest of the build?
    /// </summary>
    public sealed class LayerIndependenceRead
    {
        public string Axis, From, To;
        /// <summary>The other axis varied underneath, and its two values.</summary>
        public string Under, UnderA, UnderB;

        /// <summary>Pixels the axis changes over base A, and over base B.</summary>
        public int DeltaOverA, DeltaOverB;
        /// <summary>Pixels the axis changes over BOTH bases, with the SAME resulting colour. If the
        /// change were a separable layer this would equal both counts above.</summary>
        public int IdenticalPixels;

        /// <summary>0..1 — how much of the change is reusable across a different body underneath.</summary>
        public double Reuse =>
            Math.Max(DeltaOverA, DeltaOverB) == 0
                ? 1.0
                : IdenticalPixels / (double)Math.Max(DeltaOverA, DeltaOverB);

        public override string ToString() =>
            $"{Axis} {From}→{To} under {Under} {UnderA} vs {UnderB}: " +
            $"{DeltaOverA} / {DeltaOverB} delta px, {IdenticalPixels} identical " +
            $"({Reuse * 100:F1}% reusable)";
    }

    public sealed class LayerSeparationResult
    {
        public readonly List<LayerSeparationRead> Reads = new List<LayerSeparationRead>();
        public readonly List<LayerIndependenceRead> Independence = new List<LayerIndependenceRead>();

        /// <summary>OUTCOME A: the structural axes render as separable layers, so a layered bake can
        /// give the creator free combination. OUTCOME B: integrated only.</summary>
        public bool SeparableLayers;
        public string Verdict;
        public string Report;
    }

    /// <summary>
    /// <b>The layer-separation spike (ADR 0029).</b> Answers ONE question with pixels rather than
    /// argument: can a structural axis of the pass-6 character be baked as an OVERLAY over a shared
    /// base, or does it only exist as part of a whole-figure render?
    ///
    /// <para>The answer decides how much freedom the character creator can offer. A separable rig
    /// means <c>bases + overlays</c> sheets and free combination (7 garments × 7 hats × 8 hair × 8
    /// beard = 3136 combinations from ~30 sheets). An integrated rig means every combination is its
    /// own bake, so the creator's structural axes ship preset-quantized until the art director lands
    /// a layer-isolating export.</para>
    ///
    /// <para><b>Two measurements, and the second is the one that decides.</b></para>
    ///
    /// <list type="number">
    ///   <item><b>Containment.</b> Change one axis; classify every differing pixel as ADDED (was
    ///   transparent), REPAINTED (was opaque, different colour now) or ERASED (was opaque, gone now).
    ///   An overlay composites — it can only ADD. Erased pixels are already fatal: a layer cannot
    ///   remove silhouette from the layer beneath it. Delta reaching the HEAD BAND while a GARMENT is
    ///   the axis under test is the same verdict from the other end: a torso layer that repaints the
    ///   face is not a torso layer.</item>
    ///
    ///   <item><b>Independence.</b> Make the SAME axis change over two DIFFERENT bodies underneath
    ///   and compare the two deltas pixel for pixel. If a garment were a separable layer, putting it
    ///   on would paint the same pixels the same colours whatever the beard is doing. The share that
    ///   matches is the share a baked overlay could actually reuse — and it is a share, not a yes/no,
    ///   which is why this reports a percentage and the verdict quotes it.</item>
    /// </list>
    ///
    /// <para>Probed across several facings and two poses, because a single front-on idle frame is
    /// exactly where an integrated renderer looks separable: the profile facings are where the
    /// z-buffer re-solves and the dither reflows.</para>
    ///
    /// <para>Nothing here writes art. It renders through the rig's own <c>render()</c> — the same
    /// entry the baker uses — and returns numbers.</para>
    /// </summary>
    public static class CharacterLayerSeparationProbe
    {
        /// <summary>Head band around the projected head anchor, as in
        /// <see cref="CharacterRigAzimuthProbe"/>: the anchor sits just above the crown.</summary>
        const int BandAbovePx = 5, BandBelowPx = 15;

        /// <summary>
        /// The share of a change's pixels that must be reusable over a different body before the axis
        /// counts as a separable layer. Set at 0.98 rather than 1.0 so a stray dither pixel at a band
        /// edge cannot decide an architecture — but high enough that "mostly the same shape" does not
        /// pass, because a 90%-reusable overlay is a visible seam on 10% of the garment.
        /// </summary>
        const double ReuseThreshold = 0.98;

        /// <summary>
        /// Poses to probe. Deliberately spans facings AND states: the mid-stride walk and the dig's
        /// f8 (the toss, the most extreme torso twist the rig has) are where an integrated renderer
        /// stops resembling a stack of layers.
        /// </summary>
        static readonly (int dir, string anim, int frame)[] Poses =
        {
            (0, "idle", 0),   // N — away
            (2, "walk", 3),   // E — profile, mid-stride
            (4, "idle", 0),   // S — toward camera, the flattering one
            (5, "dig", 8),    // SW — the toss
            (6, "walk", 3),   // W — the mirrored profile
        };

        /// <summary>The structural axes the creator would want free, each as bare → dressed.</summary>
        static readonly (string axis, string from, string to)[] Axes =
        {
            ("garment", "workshirt", "oilskins"),
            ("hat", "none", "souwester"),
            ("hairStyle", "crop", "long"),
            ("beard", "none", "full"),
        };

        /// <summary>The bodies to vary UNDERNEATH each axis for the independence measurement.</summary>
        static readonly (string under, string a, string b)[] Bases =
        {
            ("beard", "none", "full"),      // does a beard move what a garment paints?
            ("age", "adult", "elder"),      // head-to-body ratio, and the elder's stoop
        };

        public static LayerSeparationResult Measure(IRigScriptHost host, string g, in RigGeometry geo)
        {
            var result = new LayerSeparationResult();

            // ---- 1. containment ----------------------------------------------------------------
            foreach (var (axis, from, to) in Axes)
            foreach (var (dir, anim, frame) in Poses)
                result.Reads.Add(Contain(host, g, geo, axis, from, to, dir, anim, frame));

            // ---- 2. independence ---------------------------------------------------------------
            foreach (var (axis, from, to) in Axes)
            foreach (var (under, ua, ub) in Bases)
            {
                // The profile facing, mid-stride: the hardest honest case, and the one a walking
                // NPC in a shop-bought coat would actually be seen in.
                result.Independence.Add(
                    Independence(host, g, geo, axis, from, to, under, ua, ub, dir: 2,
                                 anim: "walk", frame: 3));
            }

            // ---- the verdict --------------------------------------------------------------------
            int erased = 0, headBandFromGarment = 0;
            foreach (var r in result.Reads)
            {
                erased += r.ErasedPixels;
                if (r.Axis == "garment") headBandFromGarment += r.DeltaInHeadBand;
            }

            double worstReuse = 1.0;
            foreach (var i in result.Independence) worstReuse = Math.Min(worstReuse, i.Reuse);

            result.SeparableLayers = erased == 0
                                     && headBandFromGarment == 0
                                     && worstReuse >= ReuseThreshold;

            var sb = new StringBuilder();
            sb.AppendLine("CHARACTER LAYER-SEPARATION SPIKE (ADR 0029)");
            sb.AppendLine($"rig {g}  cell {geo.Width}×{geo.Height}  pivot ({geo.PivotX},{geo.PivotY})");
            sb.AppendLine();
            sb.AppendLine("-- 1. containment: what changing one axis does to the pixels ------------");
            foreach (var r in result.Reads) sb.AppendLine("   " + r);
            sb.AppendLine();
            sb.AppendLine("-- 2. independence: is the change the same over a different body? -------");
            foreach (var i in result.Independence) sb.AppendLine("   " + i);
            sb.AppendLine();
            sb.AppendLine($"erased pixels (an overlay cannot subtract) : {erased}");
            sb.AppendLine($"garment delta reaching the head band       : {headBandFromGarment}");
            sb.AppendLine($"worst reuse across a changed body          : {worstReuse * 100:F1}% " +
                          $"(threshold {ReuseThreshold * 100:F0}%)");
            sb.AppendLine();

            result.Verdict = result.SeparableLayers
                ? "OUTCOME A — the structural axes render as SEPARABLE LAYERS. A layered bake " +
                  "(shared bases + per-value overlays) reproduces the integrated render, so the " +
                  "creator's structural axes can combine freely."
                : "OUTCOME B — the structural axes are INTEGRATED. Changing one re-solves pixels " +
                  "the others own, so an overlay baked over one body does not composite correctly " +
                  "over another. The creator's structural axes ship preset-quantized until the art " +
                  "director lands a layer-isolating export; COLOUR is unaffected and stays free " +
                  "(it is a ramp swap, not a render).";
            sb.AppendLine(result.Verdict);

            result.Report = sb.ToString();
            return result;
        }

        // ---- the two measurements ---------------------------------------------------------------

        static LayerSeparationRead Contain(IRigScriptHost host, string g, in RigGeometry geo,
                                           string axis, string from, string to,
                                           int dir, string anim, int frame)
        {
            byte[] a = Render(host, g, geo, dir, anim, frame, Build((axis, from)));
            byte[] b = Render(host, g, geo, dir, anim, frame, Build((axis, to)));

            double headY = host.EvaluateNumber(
                $"{g}.anchors({dir},{{anim:'{anim}',frame:{frame}}}).head.y");
            int yLo = Math.Max(0, (int)Math.Floor(headY) - BandAbovePx);
            int yHi = Math.Min(geo.Height - 1, (int)Math.Ceiling(headY) + BandBelowPx);

            var read = new LayerSeparationRead
            {
                Axis = axis, From = from, To = to, Dir = dir, Anim = anim, Frame = frame,
                MinX = int.MaxValue, MinY = int.MaxValue, MaxX = -1, MaxY = -1,
            };

            for (int y = 0; y < geo.Height; y++)
            for (int x = 0; x < geo.Width; x++)
            {
                int i = (y * geo.Width + x) * 4;
                if (a[i] == b[i] && a[i + 1] == b[i + 1] && a[i + 2] == b[i + 2] && a[i + 3] == b[i + 3])
                    continue;

                read.DeltaPixels++;
                bool wasOpaque = a[i + 3] >= 128, isOpaque = b[i + 3] >= 128;
                if (!wasOpaque && isOpaque) read.AddedPixels++;
                else if (wasOpaque && !isOpaque) read.ErasedPixels++;
                else if (wasOpaque) read.RepaintedPixels++;

                if (x < read.MinX) read.MinX = x;
                if (x > read.MaxX) read.MaxX = x;
                if (y < read.MinY) read.MinY = y;
                if (y > read.MaxY) read.MaxY = y;
                if (y >= yLo && y <= yHi) read.DeltaInHeadBand++;
            }

            if (read.MaxX < 0) { read.MinX = read.MinY = 0; }
            return read;
        }

        static LayerIndependenceRead Independence(IRigScriptHost host, string g, in RigGeometry geo,
                                                  string axis, string from, string to,
                                                  string under, string underA, string underB,
                                                  int dir, string anim, int frame)
        {
            byte[] a0 = Render(host, g, geo, dir, anim, frame, Build((under, underA), (axis, from)));
            byte[] a1 = Render(host, g, geo, dir, anim, frame, Build((under, underA), (axis, to)));
            byte[] b0 = Render(host, g, geo, dir, anim, frame, Build((under, underB), (axis, from)));
            byte[] b1 = Render(host, g, geo, dir, anim, frame, Build((under, underB), (axis, to)));

            var read = new LayerIndependenceRead
            {
                Axis = axis, From = from, To = to, Under = under, UnderA = underA, UnderB = underB,
            };

            for (int p = 0; p < geo.Width * geo.Height; p++)
            {
                int i = p * 4;
                bool changedOverA = Differs(a0, a1, i);
                bool changedOverB = Differs(b0, b1, i);
                if (changedOverA) read.DeltaOverA++;
                if (changedOverB) read.DeltaOverB++;

                // Reusable = the axis paints this pixel on both bodies, and paints it the SAME.
                // A pre-baked overlay is exactly "these pixels, these colours" — so anything else is
                // a pixel the overlay would get wrong on the other body.
                if (changedOverA && changedOverB && !Differs(a1, b1, i)) read.IdenticalPixels++;
            }

            return read;
        }

        static bool Differs(byte[] p, byte[] q, int i) =>
            p[i] != q[i] || p[i + 1] != q[i + 1] || p[i + 2] != q[i + 2] || p[i + 3] != q[i + 3];

        // ---- rendering --------------------------------------------------------------------------

        /// <summary>A build literal over the rig's DEFAULT_BUILD: only the named axes are overridden,
        /// everything else falls back exactly as the rig documents.</summary>
        static string Build(params (string key, string value)[] overrides)
        {
            var sb = new StringBuilder("{");
            for (int i = 0; i < overrides.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(overrides[i].key).Append(":'").Append(overrides[i].value).Append('\'');
            }
            return sb.Append('}').ToString();
        }

        static byte[] Render(IRigScriptHost host, string g, in RigGeometry geo,
                             int dir, string anim, int frame, string buildJs)
        {
            byte[] rgba = host.EvaluateBytes(
                $"{g}.render({dir.ToString(CultureInfo.InvariantCulture)}," +
                $"{{anim:'{anim}',frame:{frame.ToString(CultureInfo.InvariantCulture)}," +
                $"build:{buildJs}}})");

            if (rgba.Length != geo.Width * geo.Height * 4)
                throw new InvalidOperationException(
                    $"Layer probe render came back {rgba.Length} bytes, expected " +
                    $"{geo.Width * geo.Height * 4} for {geo.Width}×{geo.Height} RGBA.");
            return rgba;
        }
    }
}
