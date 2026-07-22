using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>The rig's own <c>render(dir, opts)</c> arguments.</summary>
    public readonly struct RigViewOptions
    {
        /// <summary>Rig <c>dir</c> units. One unit is 45° (<c>th = dir*PI/4</c>); fractional values
        /// are genuine intermediate views, which is what makes 32 facings — and continuous mesh
        /// heading — possible at all.</summary>
        public readonly double Dir;
        public readonly double ElevationDegrees, RollDegrees, PitchDegrees;
        /// <summary>Heave, in PIXELS, subtracted from screen y — the rig's own units.</summary>
        public readonly double HeavePixels;

        public RigViewOptions(double dir, double elevationDegrees,
                              double rollDegrees = 0, double pitchDegrees = 0, double heavePixels = 0)
        {
            Dir = dir; ElevationDegrees = elevationDegrees;
            RollDegrees = rollDegrees; PitchDegrees = pitchDegrees; HeavePixels = heavePixels;
        }

        /// <summary>
        /// The argument list for the equivalent call into the rig itself — i.e. the text between
        /// the parentheses of <c>Global.render(…)</c>.
        ///
        /// ⚠️ TWO arguments, not one. <c>render(dir, opts)</c> treats a first argument that is an
        /// object as… still the <c>dir</c>, and then <c>Object.assign({}, opts, {dir})</c> puts that
        /// object into the basis where a number belongs. It does not throw; it silently renders
        /// <c>NaN</c> trigonometry and returns a fully transparent cell. Passing one object here
        /// would have made every golden master compare "empty vs empty".
        /// </summary>
        /// <remarks>
        /// ⚠️ Built by concatenation, NOT <c>string.Format</c>, and that is not a style preference.
        /// In <c>"…heave:{4:R}}}"</c> the format parser reads the specifier as <c>"R}"</c> — it
        /// treats the <c>}}</c> as an escaped brace INSIDE the specifier — and a custom numeric
        /// format with no digit placeholders emits its characters literally. The result is
        /// <c>heave:R}</c>, which V8 rejects with "ReferenceError: R is not defined" while pointing
        /// at nothing that looks like C#. Round-tripping each number on its own has no such edge.
        /// </remarks>
        public string ToJsArgs()
        {
            var c = System.Globalization.CultureInfo.InvariantCulture;
            return Dir.ToString("R", c) +
                   ",{elev:" + ElevationDegrees.ToString("R", c) +
                   ",roll:" + RollDegrees.ToString("R", c) +
                   ",pitch:" + PitchDegrees.ToString("R", c) +
                   ",heave:" + HeavePixels.ToString("R", c) + "}";
        }
    }

    /// <summary>
    /// The eight trig scalars the rig's <c>camBasis</c> derives from a view, plus heave.
    ///
    /// <para><b>Why this is a first-class thing and not just <c>Math.Cos</c> inline.</b> V8 and .NET
    /// do not compute <c>cos</c> and <c>sin</c> identically — both are IEEE doubles, but the last
    /// ULP differs, because V8 ships its own fdlibm port and .NET calls the platform CRT. Neither is
    /// wrong. On a rasteriser whose fill rule and ordered dither both compare against hard
    /// thresholds, a 1-ULP difference in the basis flips a handful of pixels that sit exactly on a
    /// facet or dither boundary — MEASURED at 1–3 px out of 42,370 (lobster), 115,206 (dragger) and
    /// 5,536 (punt).</para>
    ///
    /// <para>That is a confound, not a finding. Sourcing the basis from the SAME engine the rig runs
    /// in removes it, and what is left over is purely a statement about the extracted data — which
    /// is the only thing phase 2 is entitled to claim.</para>
    /// </summary>
    public readonly struct RigTrigBasis
    {
        public readonly double Ct, St, Se, Ce, Cr, Sr, Cq, Sq, Heave;
        /// <summary>True when the scalars came from the script engine rather than from .NET.</summary>
        public readonly bool FromScriptEngine;

        public RigTrigBasis(double ct, double st, double se, double ce,
                            double cr, double sr, double cq, double sq, double heave,
                            bool fromScriptEngine)
        {
            Ct = ct; St = st; Se = se; Ce = ce; Cr = cr; Sr = sr; Cq = cq; Sq = sq;
            Heave = heave; FromScriptEngine = fromScriptEngine;
        }

        /// <summary>The rig's <c>camBasis(opts)</c>, recomputed with .NET trig.</summary>
        public static RigTrigBasis FromDotNet(in RigViewOptions v)
        {
            const double deg = Math.PI / 180.0;
            double th = v.Dir * Math.PI / 4.0;
            double e = v.ElevationDegrees * deg;
            double roll = v.RollDegrees * deg, pitch = v.PitchDegrees * deg;
            return new RigTrigBasis(
                Math.Cos(th), Math.Sin(th), Math.Sin(e), Math.Cos(e),
                Math.Cos(roll), Math.Sin(roll), Math.Cos(pitch), Math.Sin(pitch),
                v.HeavePixels, fromScriptEngine: false);
        }

        /// <summary>
        /// The same scalars, evaluated by the engine that runs the rig. <c>camBasis</c> itself is
        /// closure-private like everything else, so this recomputes it from the rig's own formula —
        /// the point is only WHICH <c>Math.cos</c> executes.
        /// </summary>
        public static RigTrigBasis FromScriptHost(IRigScriptHost host, in RigViewOptions v)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            var c = System.Globalization.CultureInfo.InvariantCulture;
            // Handed over as RAW f64 BYTES, not as text. Decimal round-tripping a double through
            // JS's shortest-representation printer and back through double.Parse is very probably
            // exact — but "very probably exact" is the whole thing this method exists to eliminate.
            host.Execute(
                "globalThis.__hhRigBasis=(function(){var DEG=Math.PI/180," +
                "th=" + v.Dir.ToString("R", c) + "*Math.PI/4," +
                "e=" + v.ElevationDegrees.ToString("R", c) + "*DEG," +
                "r=" + v.RollDegrees.ToString("R", c) + "*DEG," +
                "q=" + v.PitchDegrees.ToString("R", c) + "*DEG;" +
                "var a=[Math.cos(th),Math.sin(th),Math.sin(e),Math.cos(e)," +
                "Math.cos(r),Math.sin(r),Math.cos(q),Math.sin(q)];" +
                "var buf=new ArrayBuffer(64),dv=new DataView(buf);" +
                "for(var i=0;i<8;i++)dv.setFloat64(i*8,a[i],true);" +
                "return new Uint8ClampedArray(buf);})();");
            byte[] b = host.EvaluateBytes("globalThis.__hhRigBasis");
            if (b.Length != 64)
                throw new InvalidOperationException($"Expected 64 basis bytes, got {b.Length}.");

            double D(int i) => BitConverter.ToDouble(b, i * 8);
            return new RigTrigBasis(D(0), D(1), D(2), D(3), D(4), D(5), D(6), D(7),
                                    v.HeavePixels, fromScriptEngine: true);
        }
    }

    /// <summary>
    /// How two RGBA cells differ. "Inked" = opaque in either image.
    ///
    /// <para><b><see cref="LargestDifferingCluster"/> is the load-bearing number, not the
    /// percentage.</b> That was learned the hard way: reversing the winding of one small hull facet
    /// changes 0.039% of the cell, and moving one of its vertices by a pixel and a half changes
    /// 0.026% — BOTH BELOW the ~0.05% that last-ULP arithmetic noise already produces. A
    /// whole-cell percentage cannot see a localised defect on a large hull; it dilutes it. Any
    /// tolerance expressed that way is a check that passes when it should fail.</para>
    ///
    /// <para>The two are separable by SHAPE rather than by size. Arithmetic noise is scattered
    /// single pixels sitting on facet and dither boundaries — measured largest cluster: 1. A
    /// geometry defect is a connected patch, because it is a face that moved. Clustering
    /// discriminates them by an order of magnitude where percentage overlaps.</para>
    ///
    /// <para>⚠️ This applies to ADR 0022's 1.3–4.4% shader figure too: it is a whole-cell average
    /// and is not, on its own, evidence that no localised defect exists.</para>
    /// </summary>
    public readonly struct RigPixelDiff
    {
        public readonly int InkedPixels, DifferingPixels, CoverageOnlyDifferences;
        /// <summary>Size of the largest 4-connected run of differing pixels.</summary>
        public readonly int LargestDifferingCluster;

        public RigPixelDiff(int inked, int differing, int coverageOnly, int largestCluster)
        {
            InkedPixels = inked; DifferingPixels = differing;
            CoverageOnlyDifferences = coverageOnly; LargestDifferingCluster = largestCluster;
        }

        /// <summary>Percentage of inked pixels that differ — the figure ADR 0022 quotes. Reported
        /// for comparability; see the type doc for why it must not be the acceptance criterion.</summary>
        public double PercentDiffering => InkedPixels == 0 ? 0 : 100.0 * DifferingPixels / InkedPixels;

        public override string ToString() =>
            $"{DifferingPixels}/{InkedPixels} inked px differ ({PercentDiffering:F4}%), " +
            $"largest connected cluster {LargestDifferingCluster}, " +
            $"{CoverageOnlyDifferences} silhouette (opaque-vs-transparent)";
    }

    /// <summary>
    /// A CPU re-implementation of the rigs' shared rasteriser (<c>_paint</c>) driven ENTIRELY by an
    /// extracted <see cref="RigMeshData"/> / <see cref="Mesh"/> — never by the rig's closure.
    ///
    /// <para><b>This is a test oracle, not a renderer.</b> Nothing in the game calls it. Its only
    /// job is to answer the phase-2 question — <i>is the extracted mesh a complete and faithful
    /// description of what the rig draws?</i> — and to answer it in a way CI can actually run.</para>
    ///
    /// <para><b>Why not render on the GPU, as the spike did.</b> Two reasons, and the second is the
    /// decisive one:</para>
    /// <list type="number">
    /// <item>CI has no graphics device ("Null Device"). A GPU golden master does not fail there, it
    /// CRASHES the editor — exit 1 with no results XML. A check CI cannot run is a check that
    /// silently stops running.</item>
    /// <item>A GPU comparison measures the SHADER, which is phase 3 and art-pipeline's lane. Its
    /// residual (ADR 0022: 1.3–4.4%) is float precision, rasterisation fill rules and dither
    /// boundaries — it would MASK an extraction defect of the same size. Phase 2 deserves a tighter
    /// instrument, and on the same arithmetic as the rig the honest target is not "within 4.4%" but
    /// "identical".</item>
    /// </list>
    ///
    /// <para>⚠️ It is a second implementation of one renderer, which ADR 0022 explicitly rejects for
    /// the PIPELINE ("two implementations of one renderer will drift silently"). That objection does
    /// not apply here and the distinction matters: this is ONE generic rasteriser driven by data
    /// pulled from the rig at run time, not per-rig geometry code. If the art director edits a rig,
    /// this reads the edit. If he changes the rasteriser itself, the golden master goes red — which
    /// is the alarm working, not drift.</para>
    /// </summary>
    public static class RigMeshReferenceRasterizer
    {
        const int Empty = -1;

        /// <summary>Rasterises the extracted face list directly, in double precision — this
        /// isolates EXTRACTION error from the float32 quantisation the Mesh imposes.</summary>
        public static byte[] RenderFromFaces(RigMeshData data, RigViewOptions view,
                                             RigTrigBasis? basis = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var tris = new List<Tri>(data.TriangleCount);
            foreach (var f in data.Faces)
            {
                // The rig takes ONE normal per face, off v[0]/v[1]/v[2], and reuses it for every
                // triangle of the fan. So each fan triangle must carry that triple — for every
                // triangle except the first, it is NOT the triangle's own three corners.
                for (int t = 1; t + 1 < f.V.Length; t++)
                    tris.Add(Tri.FromFace(f.V[0], f.V[t], f.V[t + 1],
                                          f.V[0], f.V[1], f.V[2],
                                          f.Mat, f.B, f.Db));
            }
            return Paint(data, tris, view, basis);
        }

        /// <summary>Rasterises the built <see cref="Mesh"/> — the exact buffer the facet shader will
        /// consume, float32 and all.</summary>
        public static byte[] RenderFromMesh(RigMeshData data, Mesh mesh, RigViewOptions view,
                                           RigTrigBasis? basis = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));

            var verts = mesh.vertices;
            var norms = mesh.normals;
            var attrs = new List<Vector4>();
            mesh.GetUVs(RigMeshBuilder.AttrUvChannel, attrs);
            int[] idx = mesh.triangles;

            if (norms.Length != verts.Length || attrs.Count != verts.Length)
                throw new InvalidOperationException(
                    $"Mesh channels disagree: {verts.Length} verts, {norms.Length} normals, " +
                    $"{attrs.Count} attrs. The facet shader reads all three per vertex.");

            var tris = new List<Tri>(idx.Length / 3);
            for (int i = 0; i < idx.Length; i += 3)
            {
                int a = idx[i], b = idx[i + 1], c = idx[i + 2];
                Vector4 at = attrs[a];
                tris.Add(Tri.FromMesh(ToD(verts[a]), ToD(verts[b]), ToD(verts[c]),
                                      ToD(norms[a]), Mathf.RoundToInt(at.x), at.y, at.z));
            }
            return Paint(data, tris, view, basis);
        }

        static Vector3d ToD(Vector3 v) => new Vector3d(v.x, v.y, v.z);

        readonly struct Tri
        {
            /// <summary>The triangle actually rasterised.</summary>
            public readonly Vector3d A, B, C;
            /// <summary>The face's FIRST THREE vertices — the triple the rig derives the whole
            /// polygon's normal from, which for every triangle of a fan except the first is NOT the
            /// triangle's own corners.</summary>
            public readonly Vector3d N0, N1, N2;
            /// <summary>Precomputed object-space normal, used when <see cref="NormalFromRotatedVerts"/>
            /// is false.</summary>
            public readonly Vector3d N;
            public readonly int Mat;
            public readonly double FaceBias, DepthBias;

            /// <summary>
            /// When true the normal is derived at paint time from the ROTATED
            /// <see cref="N0"/>/<see cref="N1"/>/<see cref="N2"/> — literally what the rig does
            /// (<c>normal(rv[0],rv[1],rv[2])</c> on <c>projVert</c> output). When false the stored
            /// object-space <see cref="N"/> is rotated instead — what the GPU will do with the
            /// mesh's baked-in normal.
            ///
            /// <para>⚠️ The two are the same vector in exact arithmetic and NOT the same in floating
            /// point: <c>(Ru)×(Rv)</c> and <c>R(u×v)</c> differ in the last ULP, and after
            /// normalising, scaling by GAIN and comparing against an ordered-dither threshold that is
            /// occasionally enough to flip one pixel. It is floating-point associativity, not missing
            /// geometry — but the only way to KNOW that is to remove it and watch the number reach
            /// zero.</para>
            /// </summary>
            public readonly bool NormalFromRotatedVerts;

            Tri(in Vector3d a, in Vector3d b, in Vector3d c,
                in Vector3d n0, in Vector3d n1, in Vector3d n2, in Vector3d n,
                int mat, double faceBias, double depthBias, bool normalFromRotatedVerts)
            {
                A = a; B = b; C = c;
                N0 = n0; N1 = n1; N2 = n2; N = n;
                Mat = mat; FaceBias = faceBias; DepthBias = depthBias;
                NormalFromRotatedVerts = normalFromRotatedVerts;
            }

            /// <summary>A fan triangle that shades from its FACE's normal, the rig's way.</summary>
            public static Tri FromFace(in Vector3d a, in Vector3d b, in Vector3d c,
                                       in Vector3d n0, in Vector3d n1, in Vector3d n2,
                                       int mat, double faceBias, double depthBias) =>
                new Tri(a, b, c, n0, n1, n2, default, mat, faceBias, depthBias, true);

            /// <summary>A mesh triangle that shades from its stored flat normal, the GPU's way.</summary>
            public static Tri FromMesh(in Vector3d a, in Vector3d b, in Vector3d c, in Vector3d n,
                                       int mat, double faceBias, double depthBias) =>
                new Tri(a, b, c, default, default, default, n, mat, faceBias, depthBias, false);
        }

        /// <summary>The rotation half of the rig's <c>projVert</c>, which is what a normal gets too
        /// — no translation, no scale, so it is valid for both.</summary>
        static Vector3d Rotate(in Vector3d p, in RigTrigBasis b)
        {
            double x1 = p.X * b.Cr + p.Z * b.Sr, z1 = -p.X * b.Sr + p.Z * b.Cr;
            double y2 = p.Y * b.Cq - z1 * b.Sq, z2 = p.Y * b.Sq + z1 * b.Cq;
            return new Vector3d(x1 * b.Ct - y2 * b.St, x1 * b.St + y2 * b.Ct, z2);
        }

        readonly struct Projected
        {
            public readonly double Sx, Sy, D;
            public Projected(double sx, double sy, double d) { Sx = sx; Sy = sy; D = d; }
        }

        static Projected Project(in Vector3d p, in RigTrigBasis b, RigMeshData data)
        {
            Vector3d r = Rotate(p, b);
            double s = data.PxPerMetre;
            return new Projected(
                data.PivotX + r.X * s,
                data.PivotY - (r.Y * b.Se + r.Z * b.Ce) * s - b.Heave,
                r.Y * b.Ce - r.Z * b.Se);
        }

        /// <summary>The rig's <c>shadeOf(n, se, ce)</c>. Note the elevation rotation folded into the
        /// dot: this is exactly the "key light fixed in SCREEN space" that ADR 0022 says makes the
        /// result read as pixel art rather than as lit 3D.</summary>
        static double ShadeOf(in Vector3d n, in RigTrigBasis b, in Vector3d ln) =>
            n.X * ln.X + (n.Y * b.Se + n.Z * b.Ce) * ln.Y + (-n.Y * b.Ce + n.Z * b.Se) * ln.Z;

        static byte[] Paint(RigMeshData data, List<Tri> tris, RigViewOptions view, RigTrigBasis? basisOverride)
        {
            int pw = data.W, ph = data.H;
            RigTrigBasis basis = basisOverride ?? RigTrigBasis.FromDotNet(view);

            var zbuf = new double[pw * ph];
            for (int i = 0; i < zbuf.Length; i++) zbuf[i] = double.PositiveInfinity;
            var col = new int[pw * ph];
            for (int i = 0; i < col.Length; i++) col[i] = Empty;
            var dep = new double[pw * ph];

            var rampTable = BuildRampTable(data);

            foreach (var tri in tris)
            {
                Vector3d n = tri.NormalFromRotatedVerts
                    ? RigMeshBuilder.ObjectNormal(Rotate(tri.N0, basis),
                                                  Rotate(tri.N1, basis),
                                                  Rotate(tri.N2, basis))
                    : Rotate(tri.N, basis);
                double sh = ShadeOf(n, basis, data.LightN);
                // The rig's interior/backface rescue: faces that opt in with b <= -1.
                if (sh < 0 && tri.FaceBias <= -1)
                    sh = ShadeOf(new Vector3d(-n.X, -n.Y, -n.Z), basis, data.LightN) * 0.9;

                double fidx = sh * data.Gain + data.Bias + tri.FaceBias;

                var mat = data.Materials[Mathf.Clamp(tri.Mat, 0, data.Materials.Count - 1)];
                var ramp = mat.Ramp;

                Projected a = Project(tri.A, basis, data);
                Projected b2 = Project(tri.B, basis, data);
                Projected c = Project(tri.C, basis, data);

                int minX = Math.Max(0, (int)Math.Floor(Math.Min(a.Sx, Math.Min(b2.Sx, c.Sx))));
                int maxX = Math.Min(pw - 1, (int)Math.Ceiling(Math.Max(a.Sx, Math.Max(b2.Sx, c.Sx))));
                int minY = Math.Max(0, (int)Math.Floor(Math.Min(a.Sy, Math.Min(b2.Sy, c.Sy))));
                int maxY = Math.Min(ph - 1, (int)Math.Ceiling(Math.Max(a.Sy, Math.Max(b2.Sy, c.Sy))));

                double area = (b2.Sx - a.Sx) * (c.Sy - a.Sy) - (c.Sx - a.Sx) * (b2.Sy - a.Sy);
                if (Math.Abs(area) < 1e-6) continue;

                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    double px = x + 0.5, py = y + 0.5;
                    double w0 = ((b2.Sx - px) * (c.Sy - py) - (c.Sx - px) * (b2.Sy - py)) / area;
                    double w1 = ((c.Sx - px) * (a.Sy - py) - (a.Sx - px) * (c.Sy - py)) / area;
                    double w2 = 1 - w0 - w1;
                    // The rig's own slack fill rule — NOT a top-left rule. It double-covers shared
                    // edges by design, which the z-test then resolves.
                    if (w0 < -0.001 || w1 < -0.001 || w2 < -0.001) continue;

                    double d = w0 * a.D + w1 * b2.D + w2 * c.D;
                    double deff = d - tri.DepthBias;
                    int i = y * pw + x;
                    if (deff >= zbuf[i]) continue;

                    zbuf[i] = deff;
                    dep[i] = d;
                    double base_ = Math.Floor(fidx);
                    int idx = (int)base_ + ((fidx - base_) > data.Bayer[x & 3, y & 3] ? 1 : 0) + mat.Off;
                    idx = Mathf.Clamp(idx, 0, ramp.Length - 1);
                    col[i] = Pack(ramp[idx]);
                }
            }

            var outCol = new int[pw * ph];
            Array.Copy(col, outCol, col.Length);

            // ---- the rig's 1px depth-discontinuity darkening (doEdge) -----------------------
            for (int y = 0; y < ph; y++)
            for (int x = 0; x < pw; x++)
            {
                int i = y * pw + x;
                if (col[i] == Empty) continue;
                for (int e = 0; e < 2; e++)
                {
                    int nx = x + (e == 0 ? 1 : 0), ny = y + (e == 0 ? 0 : 1);
                    if (nx >= pw || ny >= ph) continue;
                    int j = ny * pw + nx;
                    if (col[j] == Empty) continue;
                    if (Math.Abs(dep[i] - dep[j]) <= 0.30) continue;

                    int far = dep[i] > dep[j] ? i : j;
                    // RINDEX resolves the pixel's colour back to (ramp, index). Resolving by
                    // COLOUR — not by the material that drew it — is what the rig does, and it
                    // matters: aliased materials (blk, dark) share a ramp.
                    if (!rampTable.TryResolve(col[far], out int rIdx, out int cIdx)) continue;
                    if (cIdx <= 0) continue;
                    outCol[far] = Pack(rampTable.Ramps[rIdx][Math.Max(0, cIdx - 2)]);
                }
            }

            // ---- 1px keyline outline --------------------------------------------------------
            int key = Pack(data.Keyline);
            for (int y = 0; y < ph; y++)
            for (int x = 0; x < pw; x++)
            {
                int i = y * pw + x;
                if (outCol[i] != Empty) continue;
                bool touch =
                    (x + 1 < pw && col[i + 1] != Empty) ||
                    (x - 1 >= 0 && col[i - 1] != Empty) ||
                    (y + 1 < ph && col[i + pw] != Empty) ||
                    (y - 1 >= 0 && col[i - pw] != Empty);
                if (touch) outCol[i] = key;
            }

            var rgba = new byte[pw * ph * 4];
            for (int i = 0; i < pw * ph; i++)
            {
                int c = outCol[i];
                if (c == Empty) { rgba[i * 4 + 3] = 0; continue; }
                rgba[i * 4] = (byte)((c >> 16) & 0xFF);
                rgba[i * 4 + 1] = (byte)((c >> 8) & 0xFF);
                rgba[i * 4 + 2] = (byte)(c & 0xFF);
                rgba[i * 4 + 3] = 255;
            }
            return rgba;
        }

        static int Pack(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;

        /// <summary>
        /// Reconstructs the rig's <c>RINDEX</c>: distinct ramps in first-appearance order, mapping
        /// colour → (ramp, index) with LATER assignments winning, exactly as the rig's nested
        /// <c>forEach</c> does. Ramps are deduped by CONTENT because extraction flattens MATS and
        /// loses JS object identity — <c>blk</c>, <c>dark</c> and <c>boot</c> all point at the same
        /// array in the rig and must collapse back to one entry here.
        /// </summary>
        static RampTable BuildRampTable(RigMeshData data)
        {
            var ramps = new List<Color32[]>();
            var byKey = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int m = 0; m < data.Materials.Count; m++)
            {
                string k = string.Join(",", data.Materials[m].RampHex).ToLowerInvariant();
                if (byKey.ContainsKey(k)) continue;
                byKey[k] = ramps.Count;
                ramps.Add(data.Materials[m].Ramp);
            }

            var resolve = new Dictionary<int, (int ramp, int index)>();
            for (int r = 0; r < ramps.Count; r++)
                for (int i = 0; i < ramps[r].Length; i++)
                    resolve[Pack(ramps[r][i])] = (r, i);   // later wins, as in JS

            return new RampTable(ramps, resolve);
        }

        sealed class RampTable
        {
            public readonly List<Color32[]> Ramps;
            readonly Dictionary<int, (int ramp, int index)> _resolve;
            public RampTable(List<Color32[]> ramps, Dictionary<int, (int, int)> resolve)
            { Ramps = ramps; _resolve = resolve; }

            public bool TryResolve(int packed, out int ramp, out int index)
            {
                if (_resolve.TryGetValue(packed, out var e)) { ramp = e.ramp; index = e.index; return true; }
                ramp = index = -1; return false;
            }
        }

        /// <summary>Compares two RGBA cells the way ADR 0022 quotes fidelity: over INKED pixels
        /// (opaque in either image).</summary>
        /// <summary>
        /// Compares two RGBA cells. Requires the cell dimensions so differing pixels can be
        /// clustered — see <see cref="RigPixelDiff"/> for why the cluster size, not the percentage,
        /// is what a caller should assert on.
        /// </summary>
        public static RigPixelDiff Compare(byte[] a, byte[] b, int width, int height)
        {
            if (a == null || b == null) throw new ArgumentNullException();
            if (a.Length != b.Length)
                throw new ArgumentException($"Cell sizes differ: {a.Length} vs {b.Length} bytes.");
            if (a.Length != width * height * 4)
                throw new ArgumentException(
                    $"{width}×{height} does not describe a {a.Length}-byte RGBA cell.");

            int inked = 0, differing = 0, coverageOnly = 0;
            var mask = new bool[width * height];
            for (int i = 0, px = 0; i < a.Length; i += 4, px++)
            {
                bool oa = a[i + 3] > 0, ob = b[i + 3] > 0;
                if (!oa && !ob) continue;
                inked++;
                if (oa != ob) { differing++; coverageOnly++; mask[px] = true; continue; }
                if (a[i] != b[i] || a[i + 1] != b[i + 1] || a[i + 2] != b[i + 2])
                { differing++; mask[px] = true; }
            }

            return new RigPixelDiff(inked, differing, coverageOnly,
                                    LargestCluster(mask, width, height));
        }

        /// <summary>Largest 4-connected component in the difference mask, by iterative flood fill
        /// (iterative because a whole-hull difference would blow a recursive stack).</summary>
        static int LargestCluster(bool[] mask, int width, int height)
        {
            var seen = new bool[mask.Length];
            var stack = new Stack<int>();
            int best = 0;

            for (int start = 0; start < mask.Length; start++)
            {
                if (!mask[start] || seen[start]) continue;
                int size = 0;
                stack.Push(start);
                seen[start] = true;
                while (stack.Count > 0)
                {
                    int p = stack.Pop();
                    size++;
                    int x = p % width, y = p / width;
                    if (x > 0) Push(p - 1);
                    if (x < width - 1) Push(p + 1);
                    if (y > 0) Push(p - width);
                    if (y < height - 1) Push(p + width);
                }
                if (size > best) best = size;
            }
            return best;

            void Push(int q)
            {
                if (mask[q] && !seen[q]) { seen[q] = true; stack.Push(q); }
            }
        }
    }
}
