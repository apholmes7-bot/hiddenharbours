using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The pure maths behind the mesh-hull facet pipeline (ADR 0022 phase 3): the object transform
    /// that reproduces the art director's rig projection through the game's ordinary straight-down
    /// 2D orthographic camera, and the RINDEX-faithful "darkened colour" LUT the keyline pass uses.
    /// POCO-style statics so EditMode tests can police the conventions headless (no GPU needed).
    ///
    /// <para><b>The projection trick (ADR 0022).</b> The iso rotation is baked into the OBJECT
    /// transform — <c>Rx(elev−90)·Rz(heading)</c> in spirit, implemented as the rig's own
    /// screen-projection rows composed with its roll/pitch/heading rotation — so the ordinary 2D
    /// ortho camera reproduces the rig's exact projection AND its z-buffer depth. That collapses
    /// the rig's <c>shadeOf(n, se, ce)</c> to a plain <c>dot(worldNormal, LN)</c>: the key light
    /// stays fixed in SCREEN space, which is what makes the result read as pixel art rather than
    /// as lit 3D.</para>
    ///
    /// <para><b>⚠️ The frame is a REFLECTION, on purpose.</b> The rigs are right-handed z-up;
    /// Unity is left-handed and the 2D camera looks along +Z ("larger world z = further"), so the
    /// rig→world map has determinant −1. A Unity <see cref="Transform"/> cannot hold a reflection
    /// in its rotation, so the map is decomposed as <c>properRotation · diag(1,1,−1)</c>:
    /// <see cref="HullRotation"/> returns the proper rotation and <see cref="HullScale"/> the
    /// constant mirror scale. The same reflection is why the shader's light vector gets its z
    /// NEGATED (<see cref="ShaderLightVector"/>) — the spike measured this; do not "fix" the sign.</para>
    /// </summary>
    public static class IsoFacetMath
    {
        /// <summary>The constant mirror half of the rig→world decomposition. See the class doc.</summary>
        public static readonly Vector3 HullScale = new Vector3(1f, 1f, -1f);

        /// <summary>
        /// The rig→world linear map: the rig's <c>camBasis</c> rotation (roll about rig-Y, then
        /// pitch about rig-X, then heading about rig-Z) followed by its screen projection rows
        /// (screen-x, screen-y-up = y·se + z·ce, depth = y·ce − z·se). Determinant −1 — see the
        /// class doc. <paramref name="dirUnits"/> is in RIG units (1 unit = 45°, CCW, fractional
        /// values are genuine intermediate headings); mapping the game's compass heading onto this
        /// is deliberately NOT phase 3's business (the per-artwork mirror saga lives there).
        /// </summary>
        public static Matrix4x4 RigToWorld(double dirUnits, double elevationDeg,
                                           double rollDeg = 0, double pitchDeg = 0)
        {
            const double deg = Math.PI / 180.0;
            double th = dirUnits * Math.PI / 4.0;
            double e = elevationDeg * deg, r = rollDeg * deg, q = pitchDeg * deg;
            double ct = Math.Cos(th), st = Math.Sin(th);
            double se = Math.Sin(e), ce = Math.Cos(e);
            double cr = Math.Cos(r), sr = Math.Sin(r);
            double cq = Math.Cos(q), sq = Math.Sin(q);

            // Columns of B = Rz(th)·Rx(q)·Ry(r): the rig's own projVert rotation, applied to the
            // object-space basis vectors (transcribed from the rig; also RigMeshReferenceRasterizer.Rotate).
            // x-axis: roll then heading (pitch does not touch a pure-x vector's y... it does via z1):
            Span<double> bx = stackalloc double[3], by = stackalloc double[3], bz = stackalloc double[3];
            RotateBasis(1, 0, 0, ct, st, cr, sr, cq, sq, bx);
            RotateBasis(0, 1, 0, ct, st, cr, sr, cq, sq, by);
            RotateBasis(0, 0, 1, ct, st, cr, sr, cq, sq, bz);

            // Projection rows S: world.x = rX, world.y(up) = rY·se + rZ·ce, world.z(depth) = rY·ce − rZ·se.
            var m = Matrix4x4.identity;
            m.m00 = (float)bx[0]; m.m01 = (float)by[0]; m.m02 = (float)bz[0];
            m.m10 = (float)(bx[1] * se + bx[2] * ce);
            m.m11 = (float)(by[1] * se + by[2] * ce);
            m.m12 = (float)(bz[1] * se + bz[2] * ce);
            m.m20 = (float)(bx[1] * ce - bx[2] * se);
            m.m21 = (float)(by[1] * ce - by[2] * se);
            m.m22 = (float)(bz[1] * ce - bz[2] * se);
            return m;
        }

        static void RotateBasis(double x, double y, double z,
                                double ct, double st, double cr, double sr, double cq, double sq,
                                Span<double> outV)
        {
            // The rig's projVert rotation, verbatim: roll (x,z), pitch (y,z), heading (x,y).
            double x1 = x * cr + z * sr, z1 = -x * sr + z * cr;
            double y2 = y * cq - z1 * sq, z2 = y * sq + z1 * cq;
            outV[0] = x1 * ct - y2 * st;
            outV[1] = x1 * st + y2 * ct;
            outV[2] = z2;
        }

        /// <summary>
        /// The PROPER-rotation half of <see cref="RigToWorld"/> — assign to the hull child's
        /// <c>localRotation</c> with <see cref="HullScale"/> as its <c>localScale</c> and the
        /// composed local matrix equals the reflection map exactly
        /// (<c>R·diag(1,1,−1) = RigToWorld</c>).
        /// </summary>
        public static Quaternion HullRotation(double dirUnits, double elevationDeg,
                                              double rollDeg = 0, double pitchDeg = 0)
        {
            Matrix4x4 m = RigToWorld(dirUnits, elevationDeg, rollDeg, pitchDeg);
            // Undo the mirror on the right: M' = M·diag(1,1,−1) is a proper rotation.
            var fwd = new Vector3(-m.m02, -m.m12, -m.m22);   // third column, negated
            var up = new Vector3(m.m01, m.m11, m.m21);
            return Quaternion.LookRotation(fwd, up);
        }

        /// <summary>
        /// Heave: the rig subtracts it from SCREEN y in PIXELS; in world terms that is a straight
        /// upward translation of the hull by <c>heavePx / pxPerMetre</c> metres.
        /// </summary>
        public static Vector3 HeaveOffset(double heavePx, int pxPerMetre) =>
            new Vector3(0f, (float)(heavePx / pxPerMetre), 0f);

        /// <summary>
        /// The light vector the SHADER dots world normals with: the rig's own normalised LN with
        /// its z NEGATED, because the object matrix is a reflection of the rig's right-handed
        /// frame (see the class doc; the spike measured this sign).
        /// </summary>
        public static Vector4 ShaderLightVector(Vector3 rigLightN) =>
            new Vector4(rigLightN.x, rigLightN.y, -rigLightN.z, 0f);

        /// <summary>
        /// Builds the per-(material, rampIndex) "darkened colour" LUT the keyline pass darkens
        /// depth-discontinuity far sides with — <b>RINDEX-faithful, not merely "two steps down my
        /// own ramp"</b>.
        ///
        /// <para>The rig's <c>doEdge</c> resolves the pixel's COLOUR back to (ramp, index) through
        /// RINDEX — distinct ramps in first-appearance order, deduped by CONTENT (aliased
        /// materials share one ramp array), with LATER assignments winning — then darkens two
        /// steps down THAT ramp. When one colour value appears in two different ramps, the later
        /// ramp's neighbourhood wins, which is not necessarily the ramp the pixel was drawn with.
        /// Precomputing the resolution per (material, index) keeps the GPU pass exact for free.</para>
        ///
        /// <para>Entries that the rig would NOT darken (colour unresolvable, or resolved index
        /// ≤ 0) hold the ORIGINAL colour, so "darken" is always a plain LUT load with no branch.</para>
        /// </summary>
        public static Color32[][] BuildDarkenedRamps(IReadOnlyList<Color32[]> ramps)
        {
            if (ramps == null) throw new ArgumentNullException(nameof(ramps));

            // Distinct ramps by content, first-appearance order (the rig dedupes by array
            // IDENTITY; extraction flattens that, and content-dedupe reproduces it — see
            // RigMeshReferenceRasterizer.BuildRampTable, the test oracle this must agree with).
            var distinct = new List<Color32[]>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var ramp in ramps)
            {
                string key = ContentKey(ramp);
                if (seen.Add(key))
                    distinct.Add(ramp);
            }

            // colour -> (distinct ramp, index), later wins — exactly the rig's nested forEach.
            var resolve = new Dictionary<int, (int ramp, int index)>();
            for (int r = 0; r < distinct.Count; r++)
                for (int i = 0; i < distinct[r].Length; i++)
                    resolve[Pack(distinct[r][i])] = (r, i);

            var lut = new Color32[ramps.Count][];
            for (int m = 0; m < ramps.Count; m++)
            {
                var src = ramps[m];
                var dark = new Color32[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    dark[i] = src[i];   // default: the rig would not darken this pixel
                    if (resolve.TryGetValue(Pack(src[i]), out var e) && e.index > 0)
                        dark[i] = distinct[e.ramp][Math.Max(0, e.index - 2)];
                }
                lut[m] = dark;
            }
            return lut;
        }

        static string ContentKey(Color32[] ramp)
        {
            var sb = new System.Text.StringBuilder(ramp.Length * 7);
            foreach (var c in ramp)
                sb.Append(c.r).Append(',').Append(c.g).Append(',').Append(c.b).Append(';');
            return sb.ToString();
        }

        static int Pack(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;
    }
}
