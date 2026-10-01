using System;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>Rig 9's paint, ported line for line</b> (characterIsoRig9.js <c>paint</c>, and the head snap of
    /// <c>paintSolved</c>) over faces in the rig's frame. It has two uses, and the first is what makes
    /// the second worth anything:
    /// <list type="number">
    /// <item>On the faces rig 9 itself posed, with the rig's own numbers, it returns rig 9's render
    /// byte for byte. That is the port's proof, and a guard holds it.</item>
    /// <item>On the faces a def poses through the engine's own path, with the def's thresholds, ink
    /// and snap, whatever differs from rig 9's render is what the def's data and the engine's posing
    /// cost. That is character PR 2a's ink comparison (<see cref="CharacterSkinInk9"/>).</item>
    /// </list>
    ///
    /// <para><b>Engine-free on purpose</b> (System only), so the port can be proved against V8 without
    /// the editor. The arithmetic follows the rig's operation order, JavaScript's <c>Math.round</c>
    /// (<see cref="JsRound"/>) and V8's <c>Math.hypot</c> (<see cref="HypotV8"/>), and the rig's
    /// depth and z buffers are <c>Float32Array</c>s, so they are floats here too: a port that kept
    /// them in doubles would break ties the rig breaks the other way.</para>
    /// </summary>
    public static class RigPaint9
    {
        /// <summary>One face: its corners in the rig's frame (metres, +x right, +y forward, +z up) as
        /// <c>x0, y0, z0, x1, …</c>; its material; the rig's shade bias <c>b</c> and depth bias
        /// <c>db</c>; the threshold it culls at (the rig's <c>minT</c>, or a def's role threshold);
        /// and whether the head snap moves it.</summary>
        public sealed class Face
        {
            public double[] V;
            public int Mat;
            public double B, Db, MinT;
            public bool Head;

            public int Corners => V.Length / 3;
        }

        /// <summary>One material as paint reads it: its ramp as <c>0xRRGGBB</c>, its gain and bias, its
        /// tone window (null reads as the rig's 0 and 99), its offset, and whether it is one fixed
        /// colour (step 0, never dropped).</summary>
        public sealed class Material
        {
            public int[] Ramp;
            public double Gain = 1, Bias;
            public int? Lo, Hi;
            public int Off;
            public bool Fixed;
        }

        /// <summary>The rig's <c>camOf(o)</c>, field for field. Read it off the rig's own camOf, so the
        /// sines and cosines are V8's and not this runtime's.</summary>
        public struct Camera
        {
            public double Ct, St, Se, Ce, Cr, Sr, Cq, Sq, S, Heave;
            public int W, H, Cx, Cy;
        }

        /// <summary>The numbers paint reads besides the faces: <c>SHADING.key</c>, <c>form</c>,
        /// <c>formMid</c>, <c>edge</c>, <c>keyline</c> (as <c>0xRRGGBB</c>), <c>keylineMix</c>, and the
        /// floor of the face cull (the <c>1e-4</c> of <c>max(1e-4, minT)</c>).</summary>
        public struct Ink
        {
            public double KeyX, KeyY, KeyZ;
            public double Form, FormMid;
            public double Edge;
            public int Keyline;
            public double KeylineMix;
            public double CullFloor;

            /// <summary>Mix the keyline in SINGLE precision, as the resolve does:
            /// <c>floor(a + (b − a)·(float)mix + 0.5)</c> in floats, rather than the rig's doubles and
            /// <c>Math.round</c>. Off for the rig's own numbers (the port's proof); on for a def's, whose
            /// mix is a float (<see cref="CharacterSkinInk9.KeylineMixAgrees"/> shows the two agree on
            /// every byte pair before a caller relies on it).</summary>
            public bool SinglePrecisionMix;
        }

        /// <summary>One projected corner: the rig's <c>proj</c> output.</summary>
        public struct Projected
        {
            public double Sx, Sy, D, Xr, Yr, Zr;
        }

        /// <summary>What <see cref="Paint"/> leaves behind besides the colours, for a caller that wants
        /// to say WHICH pixels differ and why.</summary>
        public sealed class Result
        {
            public byte[] Rgba;
            public int W, H;
            /// <summary>Per pixel: the material painted, or −1.</summary>
            public short[] Mat;
            /// <summary>Per pixel: the step after the edge drop.</summary>
            public sbyte[] Step;
            /// <summary>Per pixel: the face painted, or −1.</summary>
            public int[] FaceIndex;
        }

        /// <summary>The rig's <c>proj(p, C)</c>, verbatim.</summary>
        public static Projected Project(double x, double y, double z, in Camera c)
        {
            double x1 = x * c.Cr + z * c.Sr, z1 = -x * c.Sr + z * c.Cr;
            double y2 = y * c.Cq - z1 * c.Sq, z2 = y * c.Sq + z1 * c.Cq;
            double xr = x1 * c.Ct - y2 * c.St, yr = x1 * c.St + y2 * c.Ct;
            return new Projected
            {
                Xr = xr,
                Yr = yr,
                Zr = z2,
                Sx = c.Cx + xr * c.S,
                Sy = c.Cy - (yr * c.Se + z2 * c.Ce) * c.S - c.Heave,
                D = yr * c.Ce - z2 * c.Se,
            };
        }

        /// <summary>paintSolved's head snap for a head point <c>fkp(W[head], headMid)</c>: the screen
        /// offset that puts it on a pixel centre, <c>[round(sx − 0.5) + 0.5 − sx, round(sy − 0.5) + 0.5 − sy]</c>.</summary>
        public static double[] Snap(double x, double y, double z, in Camera c)
        {
            Projected q = Project(x, y, z, c);
            return new[] { JsRound(q.Sx - 0.5) + 0.5 - q.Sx, JsRound(q.Sy - 0.5) + 0.5 - q.Sy };
        }

        /// <summary>
        /// The rig's <c>paint(faces, C, MATS, {snap, keyline, edges})</c>. <paramref name="snap"/> is
        /// null for no snap. Materials are indexed by <see cref="Face.Mat"/>.
        /// </summary>
        public static Result Paint(Face[] faces, in Camera c, Material[] mats, in Ink ink, double[] snap,
                                   bool keyline, bool edges)
        {
            if (faces == null) throw new ArgumentNullException(nameof(faces));
            if (mats == null) throw new ArgumentNullException(nameof(mats));
            int wd = c.W, hd = c.H, n = wd * hd;
            var zb = new float[n];
            var dep = new float[n];
            var mat = new short[n];
            var stp = new sbyte[n];
            var fid = new int[n];
            for (int i = 0; i < n; i++) { zb[i] = float.PositiveInfinity; mat[i] = -1; fid[i] = -1; }

            for (int fi = 0; fi < faces.Length; fi++)
            {
                Face f = faces[fi];
                int nv = f.Corners;
                var P = new Projected[nv];
                for (int k = 0; k < nv; k++) P[k] = Project(f.V[3 * k], f.V[3 * k + 1], f.V[3 * k + 2], c);
                if (snap != null && f.Head)
                    for (int k = 0; k < nv; k++) { P[k].Sx += snap[0]; P[k].Sy += snap[1]; }

                double nx = 0, ny = 0, nz = 0;
                for (int i = 0; i < nv; i++)
                {
                    Projected a = P[i], cc = P[(i + 1) % nv];
                    nx += (a.Yr - cc.Yr) * (a.Zr + cc.Zr);
                    ny += (a.Zr - cc.Zr) * (a.Xr + cc.Xr);
                    nz += (a.Xr - cc.Xr) * (a.Yr + cc.Yr);
                }
                double nl = HypotV8(nx, ny, nz);
                if (nl == 0 || double.IsNaN(nl)) nl = 1;   // Math.hypot(...) || 1
                nx /= nl; ny /= nl; nz /= nl;
                double toward = -ny * c.Ce + nz * c.Se;
                if (toward <= Math.Max(ink.CullFloor, f.MinT)) continue;

                double up = ny * c.Se + nz * c.Ce;
                Material m = mats[f.Mat];
                int step = 0;
                if (!m.Fixed)
                {
                    double s = JsRound((nx * ink.KeyX + up * ink.KeyY + toward * ink.KeyZ +
                                        ink.Form * (toward - ink.FormMid)) * 1e9) / 1e9;
                    double tone = Clamp(JsRound(s * m.Gain + m.Bias + f.B), m.Lo ?? 0, m.Hi ?? 99) + m.Off;
                    step = (int)Math.Max(0, Math.Min(m.Ramp.Length - 1, tone));
                }

                double db = f.Db;
                for (int t = 1; t + 1 < nv; t++)
                {
                    Projected a = P[0], bq = P[t], cc = P[t + 1];
                    double area = (bq.Sx - a.Sx) * (cc.Sy - a.Sy) - (cc.Sx - a.Sx) * (bq.Sy - a.Sy);
                    if (Math.Abs(area) < 1e-9) continue;
                    int x0 = (int)Math.Max(0, Math.Floor(Math.Min(a.Sx, Math.Min(bq.Sx, cc.Sx))));
                    int x1 = (int)Math.Min(wd - 1, Math.Ceiling(Math.Max(a.Sx, Math.Max(bq.Sx, cc.Sx))));
                    int y0 = (int)Math.Max(0, Math.Floor(Math.Min(a.Sy, Math.Min(bq.Sy, cc.Sy))));
                    int y1 = (int)Math.Min(hd - 1, Math.Ceiling(Math.Max(a.Sy, Math.Max(bq.Sy, cc.Sy))));
                    for (int y = y0; y <= y1; y++)
                        for (int x = x0; x <= x1; x++)
                        {
                            double px = x + 0.5, py = y + 0.5;
                            double w0 = ((bq.Sx - px) * (cc.Sy - py) - (cc.Sx - px) * (bq.Sy - py)) / area;
                            double w1 = ((cc.Sx - px) * (a.Sy - py) - (a.Sx - px) * (cc.Sy - py)) / area;
                            double w2 = 1 - w0 - w1;
                            if (w0 < -1e-6 || w1 < -1e-6 || w2 < -1e-6) continue;
                            double d = w0 * a.D + w1 * bq.D + w2 * cc.D;
                            int i = y * wd + x;
                            double dq = JsRound((d - db) * 1e7) / 1e7;
                            if (dq < zb[i])
                            {
                                zb[i] = (float)dq;
                                dep[i] = (float)d;
                                mat[i] = (short)f.Mat;
                                stp[i] = (sbyte)step;
                                fid[i] = fi;
                            }
                        }
                }
            }

            // Inner contour: across a depth break the FARTHER pixel drops one step.
            if (edges)
            {
                var drop = new bool[n];
                for (int y = 0; y < hd; y++)
                    for (int x = 0; x < wd; x++)
                    {
                        int i = y * wd + x;
                        if (mat[i] < 0) continue;
                        for (int e = 0; e < 2; e++)
                        {
                            int X = x + (e == 0 ? 1 : 0), Y = y + (e == 0 ? 0 : 1);
                            if (X >= wd || Y >= hd) continue;
                            int j = Y * wd + X;
                            if (mat[j] < 0) continue;
                            if (Math.Abs((double)dep[i] - dep[j]) > ink.Edge) drop[dep[i] > dep[j] ? i : j] = true;
                        }
                    }
                for (int i = 0; i < n; i++)
                    if (drop[i] && !mats[mat[i]].Fixed && stp[i] > 0) stp[i]--;
            }

            var col = new int[n];
            for (int i = 0; i < n; i++)
            {
                if (mat[i] < 0) { col[i] = -1; continue; }
                int[] ramp = mats[mat[i]].Ramp;
                col[i] = ramp[Math.Min(ramp.Length - 1, (int)stp[i])];
            }
            var outc = (int[])col.Clone();

            // Keyline: an empty pixel beside the figure takes the tint of its NEAREST filled neighbour
            // (ties to the darker), scanned up, right, left, down.
            if (keyline)
                for (int y = 0; y < hd; y++)
                    for (int x = 0; x < wd; x++)
                    {
                        int i = y * wd + x;
                        if (col[i] >= 0) continue;
                        int src = -1;
                        double sd = double.PositiveInfinity, sl = double.PositiveInfinity;
                        for (int k = 0; k < 4; k++)
                        {
                            int X = x + KeylineDx[k], Y = y + KeylineDy[k];
                            if (X < 0 || X >= wd || Y < 0 || Y >= hd) continue;
                            int j = Y * wd + X, cj = col[j];
                            if (cj < 0) continue;
                            double lum = ((cj >> 16) & 255) + ((cj >> 8) & 255) + (cj & 255);
                            if (dep[j] < sd - 1e-9 || (Math.Abs(dep[j] - sd) <= 1e-9 && lum < sl))
                            {
                                src = cj; sd = dep[j]; sl = lum;
                            }
                        }
                        if (src >= 0)
                            outc[i] = ink.SinglePrecisionMix
                                ? MixHexSingle(ink.Keyline, src, (float)ink.KeylineMix)
                                : MixHex(ink.Keyline, src, ink.KeylineMix);
                    }

            var rgba = new byte[n * 4];
            for (int i = 0; i < n; i++)
            {
                int cc = outc[i];
                if (cc < 0) continue;
                rgba[i * 4] = (byte)((cc >> 16) & 255);
                rgba[i * 4 + 1] = (byte)((cc >> 8) & 255);
                rgba[i * 4 + 2] = (byte)(cc & 255);
                rgba[i * 4 + 3] = 255;
            }
            return new Result { Rgba = rgba, W = wd, H = hd, Mat = mat, Step = stp, FaceIndex = fid };
        }

        /// <summary>The keyline's scan order: [0,−1], [1,0], [−1,0], [0,1].</summary>
        static readonly int[] KeylineDx = { 0, 1, -1, 0 };
        static readonly int[] KeylineDy = { -1, 0, 0, 1 };

        /// <summary>The rig's <c>mixHex(a, b, t)</c>: per byte, <c>round(A + (B − A)·t)</c>.</summary>
        public static int MixHex(int a, int b, double t)
        {
            int r = 0;
            for (int shift = 16; shift >= 0; shift -= 8)
            {
                int A = (a >> shift) & 255, B = (b >> shift) & 255;
                r |= ((int)JsRound(A + (B - A) * t) & 255) << shift;
            }
            return r;
        }

        /// <summary>The resolve's keyline mix: per byte, <c>floor(A + (B − A)·t + 0.5)</c> with every
        /// operation rounded to single precision (the casts make C# round where a GPU does).</summary>
        public static int MixHexSingle(int a, int b, float t)
        {
            int r = 0;
            for (int shift = 16; shift >= 0; shift -= 8)
            {
                int A = (a >> shift) & 255, B = (b >> shift) & 255;
                r |= (MixByteSingle(A, B, t) & 255) << shift;
            }
            return r;
        }

        /// <summary>One byte of <see cref="MixHexSingle"/>.</summary>
        public static int MixByteSingle(int a, int b, float t)
        {
            float product = (float)((float)(b - a) * t);
            float sum = (float)(a + product);
            return (int)Math.Floor((float)(sum + 0.5f));
        }

        /// <summary>The rig's <c>clamp(v, a, b)</c>: <c>v &lt; a ? a : v &gt; b ? b : v</c>.</summary>
        public static double Clamp(double v, double a, double b) => v < a ? a : v > b ? b : v;

        /// <summary>
        /// JavaScript's <c>Math.round</c> as V8 computes it: the ceiling, less one when the ceiling is
        /// more than half above. Not <c>Math.Floor(x + 0.5)</c>, which rounds 0.49999999999999994 up.
        /// </summary>
        public static double JsRound(double x)
        {
            double r = Math.Ceiling(x);
            return r - 0.5 > x ? r - 1.0 : r;
        }

        /// <summary>
        /// V8's <c>Math.hypot</c> for three arguments: the largest magnitude divided out, the squares
        /// summed with Kahan's compensation, <c>sqrt(sum) * max</c>. Infinity wins over NaN, as there.
        /// </summary>
        public static double HypotV8(double x, double y, double z)
        {
            double ax = Math.Abs(x), ay = Math.Abs(y), az = Math.Abs(z);
            bool nan = double.IsNaN(ax) || double.IsNaN(ay) || double.IsNaN(az);
            double max = 0;
            if (!double.IsNaN(ax) && ax > max) max = ax;
            if (!double.IsNaN(ay) && ay > max) max = ay;
            if (!double.IsNaN(az) && az > max) max = az;
            if (double.IsPositiveInfinity(max)) return double.PositiveInfinity;
            if (nan) return double.NaN;
            if (max == 0) return 0;
            double sum = 0, compensation = 0;
            Add(ax); Add(ay); Add(az);
            return Math.Sqrt(sum) * max;

            void Add(double v)
            {
                double q = v / max;
                double summand = q * q - compensation;
                double preliminary = sum + summand;
                compensation = preliminary - sum - summand;
                sum = preliminary;
            }
        }

        /// <summary><c>#rrggbb</c> as <c>0xRRGGBB</c>.</summary>
        public static int ParseHex(string hex)
        {
            if (hex == null || hex.Length != 7 || hex[0] != '#')
                throw new FormatException($"'{hex}' is not a #rrggbb colour.");
            return Convert.ToInt32(hex.Substring(1), 16);
        }
    }
}
