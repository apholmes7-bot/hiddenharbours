using System;
using System.Collections.Generic;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The C# twin of <c>TerrainLight6.relight</c>, the terrain pass 9 kit's floor light
    /// (<c>docs/art/rigs/terrain/pass9/terrainLight6.js</c>), line for line.
    ///
    /// <para><b>What it is for.</b> The splat shader relights the ground from the kit's baked maps
    /// (<c>Art/Shaders/Include/TerrainLight6.hlsl</c>), and a shader cannot be tested on a CI runner with
    /// no GPU. This is the same light in engine-light C#, so it can be: <c>TerrainLight6TwinTests</c>
    /// relights the kit's own G-buffers through it and compares the bytes with the kit's, which Node wrote
    /// into <c>docs/art/rigs/terrain/pass9/fixtures/terrainLight6.twin.json</c>. The twin never computes
    /// its own expectations.</para>
    ///
    /// <para><b>How it is written.</b> Doubles throughout, as JavaScript computes, and each statement in
    /// the order the rig writes it, so a diff against the rig reads line by line. <see cref="JsRound"/>
    /// is JavaScript's <c>Math.round</c>, <see cref="Hash2"/> is <c>PxLang.hash2</c>'s 32-bit integer
    /// arithmetic, and the rig's <c>Float32Array</c> fields stay <c>float[]</c> here and widen on read,
    /// as they do in JavaScript. The rig's numbers are its own look: they are ported, not tuned, and a
    /// change belongs in the rig and a re-bake.</para>
    ///
    /// <para><b>What the shader keeps.</b> All of <see cref="Relight"/> is twinned: land, the tile's own
    /// water, puddles, the tide and swash, and snow. The splat shader relights the land, the tile's water
    /// and the puddles; it leaves out snow (decision 4 of the pass 9 plan) and the tide block, whose sea
    /// is the water plane's (ADR 0012) and whose shore wetness is the shader's existing wet band.</para>
    /// </summary>
    public static class TerrainLight6
    {
        // ---- surface classes (CL, CLASSES) ----------------------------------------------------------------
        public const int ClVeg = 0, ClSoil = 1, ClSand = 2, ClRock = 3, ClMud = 4, ClWater = 5, ClWeed = 6, ClShell = 7, ClLitter = 8;

        /// <summary>One entry of the rig's <c>CLASSES</c>: porosity, gloss, how well it holds snow, and
        /// how readily it pools a puddle.</summary>
        public readonly struct SurfaceClass
        {
            public readonly string Key;
            public readonly double Por, Gloss, Hold, Puddle;
            public SurfaceClass(string key, double por, double gloss, double hold, double puddle)
            {
                Key = key; Por = por; Gloss = gloss; Hold = hold; Puddle = puddle;
            }
        }

        public static readonly SurfaceClass[] Classes =
        {
            new SurfaceClass("veg", 0.10, 0.10, 1.00, 0.5),
            new SurfaceClass("soil", 0.30, 0.10, 1.00, 1.0),
            new SurfaceClass("sand", 0.36, 0.22, 1.00, 0.7),
            new SurfaceClass("rock", 0.20, 0.60, 0.90, 1.0),
            new SurfaceClass("mud", 0.12, 0.75, 0.85, 1.0),
            new SurfaceClass("water", 0, 1, 0, 0),
            new SurfaceClass("weed", 0.14, 0.85, 0.60, 0.3),
            new SurfaceClass("shell", 0.16, 0.70, 1.00, 0.2),
            new SurfaceClass("litter", 0.22, 0.00, 1.00, 0.3),
        };

        const double Elev = 40;
        static readonly double CE = Math.Cos(Elev * Math.PI / 180), SE = Math.Sin(Elev * Math.PI / 180);
        const string Cold = "#1d3b4a";
        /// <summary>Toward the camera, floor space (east, south, up).</summary>
        static readonly double[] View = { 0, CE, SE };
        const double Tau = Math.PI * 2;
        public const int Loop = 16;

        static readonly int[][] SnowR = Ramp("#5c7180", "#7d93a0", "#a8bcc4", "#cfdde1", "#eef4f4");
        static readonly int[][] FoamR = Ramp("#6d8a90", "#9ab4b6", "#c6d7d6", "#e2ebe9", "#f6faf8");
        const string Deep = "#234b58", Shoal = "#3a6a6c";

        static readonly int[] SStep = { 1, 2, 3, 4, 6, 8, 11, 15, 20, 27, 36 };

        // ---- the G-buffer, the sky and the options: the rig's objects, as the relight reads them ----------

        /// <summary>A tile as <c>gbuf()</c> leaves it: <c>t</c>'s palette, mark and alpha, and the fields
        /// the relight reads. Build one from the kit's own <c>gbuf</c> output; the twin does not derive it.</summary>
        public sealed class GBuffer
        {
            public int N, M;
            public bool Wrap = true;
            public float[] H, Nx, Ny, Nz, Ao, Proud, Hollow, Pond;
            public double HMax;
            public byte[] Cls;
            public int[] Band0;
            /// <summary><c>t.pal</c>, <c>t.mark</c>, <c>t.a</c> (null = opaque) and <c>t.pals</c>, as
            /// [palette][band] = {r, g, b}.</summary>
            public int[] Pal, Mark;
            public byte[] A;
            public int[][][] Pals;
            /// <summary>Scene-only fields: null on a lone tile, as in the rig.</summary>
            public float[] Elev, Slope, Fetch;
            public Func<int, double> Far;

            public int Ix(int x, int y)
            {
                if (Wrap) return ((y % M) + M) % M * N + ((x % N) + N) % N;
                return (y < 0 ? 0 : y >= M ? M - 1 : y) * N + (x < 0 ? 0 : x >= N ? N - 1 : x);
            }
        }

        /// <summary>The fields of a WeatherSky sky that the relight reads. Unset values take the rig's
        /// defaults, as <c>||</c> and <c>== null</c> give them there.</summary>
        public sealed class Sky
        {
            public double[] SunF;
            public double SunI;
            public double? SkyI;
            public double Expo;
            public double Snow, Wet, Rain, Fog;
            /// <summary><c>sky.wind.w</c>; null when the sky has no wind (the rig then reads 0.2).</summary>
            public double? Wind;
            public bool Grade = true;
            public string Kc, Ac, Wash, FogC, SkyC;
            public double Aa, Amb, Ka, Wa;
        }

        /// <summary>A tip: the texel of a mark furthest toward the sun, with its projection.</summary>
        public readonly struct Tip
        {
            public readonly double V;
            public readonly int I;
            public Tip(double v, int i) { V = v; I = i; }
        }

        /// <summary><c>relight</c>'s third argument.</summary>
        public sealed class Options
        {
            public int X0, Y0, W, H, Frame;
            public byte[] Occ;
            public float[] Skyv;
            /// <summary><c>o.seaDir</c>: 0 reads as unset (1), as <c>o.seaDir || 1</c> does.</summary>
            public int SeaDir;
            public int[] Lv, Only;
            public double? Tide, TideHigh;
            /// <summary><c>o._tips</c>: read when set, and always written back.</summary>
            public Dictionary<int, Tip> Tips;
            public byte[] Out;
        }

        // ---- helpers ---------------------------------------------------------------------------------------

        /// <summary>JavaScript's <c>Math.round</c>: halves round up, toward +infinity.</summary>
        public static double JsRound(double v)
        {
            double f = Math.Floor(v);
            return v - f >= 0.5 ? f + 1 : f;
        }

        static double Clamp(double v, double a, double b) => v < a ? a : v > b ? b : v;
        static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;

        public static int[] H2R(string h) => new[]
        {
            Convert.ToInt32(h.Substring(1, 2), 16), Convert.ToInt32(h.Substring(3, 2), 16), Convert.ToInt32(h.Substring(5, 2), 16),
        };

        static int[] R2B(double[] r) => new[]
        {
            (int)Clamp(JsRound(r[0]), 0, 255), (int)Clamp(JsRound(r[1]), 0, 255), (int)Clamp(JsRound(r[2]), 0, 255),
        };

        /// <summary><c>mix</c>: a blend of two hex colours, rounded to bytes as <c>r2h</c> rounds.</summary>
        static int[] Mix(int[] a, int[] b, double t) => R2B(new[]
        {
            a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t,
        });

        static int[][] Ramp(params string[] hex)
        {
            var r = new int[hex.Length][];
            for (int i = 0; i < hex.Length; i++) r[i] = H2R(hex[i]);
            return r;
        }

        static int[][] WaterRamp(string body, Sky sky)
        {
            int[] b = H2R(body), black = H2R("#000000"), white = H2R("#ffffff"), cold = H2R(Cold);
            int[] skyC = H2R(sky.SkyC ?? "#b0c9d8"), kc = H2R(sky.Kc ?? "#fff0cf");
            return new[]
            {
                Mix(Mix(b, black, 0.42), cold, 0.30), Mix(Mix(b, black, 0.20), cold, 0.14), b,
                Mix(b, skyC, 0.42), Mix(Mix(skyC, white, 0.30), kc, 0.20),
            };
        }

        /// <summary><c>PxLang.hash2</c>: a 32-bit integer hash to [0, 1).</summary>
        public static double Hash2(int x, int y, int s)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + s * 1442695041;
                h ^= (int)((uint)h >> 13);
                h = h * 1274126177;
                h ^= (int)((uint)h >> 16);
                return (uint)h / 4294967296.0;
            }
        }

        /// <summary><c>vn</c>: value noise on the integer lattice, smoothstep between the corners.</summary>
        public static double Vn(double x, double y, int s)
        {
            double xi = Math.Floor(x), yi = Math.Floor(y), fx = x - xi, fy = y - yi, u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
            int ix = (int)xi, iy = (int)yi;
            double a = Hash2(ix, iy, s), b = Hash2(ix + 1, iy, s), c = Hash2(ix, iy + 1, s), d = Hash2(ix + 1, iy + 1, s);
            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }

        /// <summary><c>sunVis</c>: 1 when the height field does not block the sun at (x, y), else 0.</summary>
        public static double SunVis(GBuffer g, int x, int y, double[] l)
        {
            double lh = Hypot(l[0], l[1]); if (l[2] <= 0.02) return 0; if (lh < 1e-3) return 1;
            double ux = l[0] / lh, uy = l[1] / lh, tn = l[2] / lh; int i0 = g.Ix(x, y); double h0 = g.H[i0] + 0.25, room = g.HMax - h0;
            foreach (int s in SStep)
            {
                if (tn * s > room) break;
                if (g.H[g.Ix((int)JsRound(x + ux * s), (int)JsRound(y + uy * s))] > h0 + tn * s) return 0;
            }
            return 1;
        }

        /// <summary><c>swashAt</c>: how far above the still level the last wave's sheet reaches, at this frame.</summary>
        public static void SwashAt(int x, int fi, Sky sky, out double s, out double a, out bool back)
        {
            double w = sky.Wind ?? 0.2, ph = Tau * fi / Loop + 2.4 * Vn(x / 95.0, 0.5, 71) + x * 0.006;
            a = 0.05 + 0.15 * w;
            s = a * (0.5 + 0.5 * Math.Sin(ph));
            back = Math.Cos(ph) < 0;
        }

        /// <summary><c>snowE</c>: snow exposure, low = snows first.</summary>
        static double SnowE(GBuffer g, int x, int y, int i, int lvl)
        {
            double f = 0.60 * Vn(x / 9.0, y / 7.0, 17) + 0.34 * Vn(x / 3.0, y / 3.0, 29) + 0.06 * Hash2(x, y, 31);
            return f + Math.Min(0.5, g.Proud[i] * 0.35) + (1 - g.Nz[i]) * 1.4 - Math.Min(0.3, g.Hollow[i] * 0.3) + (lvl >= 1 ? 0.22 : 0) + (1 - Classes[g.Cls[i]].Hold) * 0.5;
        }

        /// <summary><c>isPud</c>: a hollow broad enough to hold water, by class.</summary>
        static bool IsPud(GBuffer g, int x, int y, int i, int c, double wetT)
        {
            double cp = Classes[c].Puddle; if (cp == 0) return false;
            double h = g.Pond[i], fill = (wetT - 0.25) / 0.75;
            if (h * cp <= (1 - fill) * 1.1 + 0.18) return false;
            double lo = h * 0.55;
            return g.Pond[g.Ix(x + 1, y)] > lo && g.Pond[g.Ix(x - 1, y)] > lo && g.Pond[g.Ix(x, y + 1)] > lo && g.Pond[g.Ix(x, y - 1)] > lo;
        }

        /// <summary><c>bandOff</c>: the band step for an exposure.</summary>
        public static int BandOff(double e, bool grade)
        {
            if (!grade) return 0;
            double r = e / 0.995;
            return r >= 1.25 ? 1 : r >= 0.30 ? 0 : r >= 0.14 ? -1 : r >= 0.06 ? -2 : -3;
        }

        /// <summary>
        /// JavaScript's <c>Math.hypot</c> for the two- and three-argument calls the rig makes, as V8 computes
        /// it: each value over the largest, the squares summed with Kahan's compensation, the root times the
        /// largest. <c>Math.Sqrt(a * a + b * b)</c> is a different rounding and misses V8's result in the last
        /// bit for some arguments, which a threshold downstream can see.
        /// </summary>
        public static double Hypot(double a, double b) => Hypot(2, a, b, 0);
        public static double Hypot(double a, double b, double c) => Hypot(3, a, b, c);

        static double Hypot(int count, double a, double b, double c)
        {
            bool nan = false; double max = 0;
            for (int k = 0; k < count; k++)
            {
                double v = k == 0 ? a : k == 1 ? b : c;
                if (double.IsNaN(v)) nan = true;
                else if (Math.Abs(v) > max) max = Math.Abs(v);
            }
            if (double.IsPositiveInfinity(max)) return double.PositiveInfinity;
            if (nan) return double.NaN;
            if (max == 0) return 0;
            double sum = 0, compensation = 0;
            for (int k = 0; k < count; k++)
            {
                double n = Math.Abs(k == 0 ? a : k == 1 ? b : c) / max, summand = n * n - compensation, preliminary = sum + summand;
                compensation = (preliminary - sum) - summand;
                sum = preliminary;
            }
            return Math.Sqrt(sum) * max;
        }

        // ---- relight -----------------------------------------------------------------------------------------

        /// <summary>
        /// <c>relight(G, sky, o)</c>: the region <c>(o.X0, o.Y0, o.W, o.H)</c> of <paramref name="g"/>
        /// relit under <paramref name="sky"/>, as RGBA bytes, row by row (or only the region-local indices
        /// in <c>o.Only</c>). The statements follow the rig's, in its order.
        /// </summary>
        public static byte[] Relight(GBuffer g, Sky sky, Options o)
        {
            o = o ?? new Options();
            int x0 = o.X0, y0 = o.Y0, w = o.W != 0 ? o.W : g.N, hh = o.H != 0 ? o.H : g.M;
            byte[] outB = o.Out ?? new byte[w * hh * 4];
            bool grade = sky.Grade; double[] l = sky.SunF ?? new double[] { 0, 0, 1 }; double sunI = sky.SunI, skyI = sky.SkyI ?? 0.6, expo = sky.Expo != 0 ? sky.Expo : 1;
            double snow = sky.Snow, wet0 = sky.Wet, rain = sky.Rain, fog = sky.Fog; int fi = ((o.Frame % Loop) + Loop) % Loop; double wind = sky.Wind ?? 0.2;
            byte[] occ = o.Occ; float[] skyv = o.Skyv; int sdir = o.SeaDir != 0 ? o.SeaDir : 1; int[] lv = o.Lv; double? tide = o.Tide;
            double? tideHigh = o.TideHigh ?? (tide == null ? (double?)null : tide.Value + 0.35); float[] elev = g.Elev;
            const double kS = 0.36, kD = 1.0; int p0 = g.Pals.Length; bool sunOn = sunI > 0.01 && l[2] > 0.02;
            int[][][] ex = { SnowR, FoamR, WaterRamp(Deep, sky), WaterRamp(Shoal, sky) };
            int ps = p0, pf = p0 + 1, pw = p0 + 2, pws = p0 + 3;
            int[] kc = H2R(sky.Kc ?? "#ffffff"), ac = H2R(sky.Ac ?? "#000000"), wc = H2R(sky.Wash ?? "#ffffff"), fc = H2R(sky.FogC ?? "#c3cdce"), sc = H2R(sky.SkyC ?? "#b0c9d8");

            int[] Colour(int pid, int bi, int lit, int fq, int spc, int wk)
            {
                int[][] ramp = pid < p0 ? g.Pals[pid] : ex[pid - p0];
                double[] r = { ramp[bi][0], ramp[bi][1], ramp[bi][2] };
                if (spc == 1) for (int k = 0; k < 3; k++) r[k] = ramp[4][k] + (kc[k] - ramp[4][k]) * 0.55;
                else if (spc == 2) for (int k = 0; k < 3; k++) r[k] = ramp[4][k] + (255 - ramp[4][k]) * 0.6;
                else if (spc == 3) for (int k = 0; k < 3; k++) r[k] += (sc[k] - r[k]) * 0.30;
                if (grade)
                {
                    double u = bi / 4.0, ta = sky.Aa * (1 - u) * (1 - sky.Amb * 0.35), tk = sky.Ka * u * (lit != 0 ? 1 : 0.3), wd = wk == 2 ? 0.30 : wk == 1 ? 0.12 : 0;
                    for (int k = 0; k < 3; k++)
                    {
                        r[k] += (ac[k] - r[k]) * ta; r[k] += (kc[k] - r[k]) * tk; r[k] *= 1 - wd;
                        if (sky.Wa != 0) r[k] += (wc[k] - r[k]) * sky.Wa;
                        if (fq != 0) r[k] += (fc[k] - r[k]) * fq * 0.17;
                    }
                }
                return R2B(r);
            }

            int[] MixC(int[] a, int[] b, double f) => new[]
            {
                (int)JsRound(a[0] + (b[0] - a[0]) * f), (int)JsRound(a[1] + (b[1] - a[1]) * f), (int)JsRound(a[2] + (b[2] - a[2]) * f),
            };

            // the sun's direction on the floor, for tips and seams
            double lx = l[0], ly = l[1]; double ll = Hypot(lx, ly);
            if (ll < 0.18) { lx = -0.6; ly = -0.8; } else { lx /= ll; ly /= ll; }
            int ax = lx > 0.38 ? -1 : lx < -0.38 ? 1 : 0, ay = ly > 0.38 ? -1 : ly < -0.38 ? 1 : 0;
            int[] only = o.Only; int ni = only != null ? only.Length : w * hh;
            // tips: per mark, the texel furthest toward the sun (inside the region)
            var tipV = new Dictionary<int, Tip>();
            if (only == null)
            {
                for (int y = 0; y < hh; y++) for (int x = 0; x < w; x++)
                {
                    int i = g.Ix(x0 + x, y0 + y), k = g.Mark[i]; if (k == 0) continue;
                    double v = (x0 + x) * lx + (y0 + y) * ly;
                    if (!tipV.TryGetValue(k, out Tip q) || v > q.V) tipV[k] = new Tip(v, i);
                }
            }
            Dictionary<int, Tip> tipOf = o.Tips ?? tipV; o.Tips = tipOf;
            double[] rv = new double[3];
            for (int q = 0; q < ni; q++)
            {
                int li = only != null ? only[q] : q, x = li % w, y = li / w, X = x0 + x, Y = y0 + y, i = g.Ix(X, Y);
                int c = g.Cls[i]; double nX = g.Nx[i], nY = g.Ny[i], nZ = g.Nz[i], ao = g.Ao[i]; int lvl = lv != null ? lv[li] : 0;
                double far = g.Far != null ? g.Far(Y) : 0; int fq = fog > 0.01 ? (int)JsRound(Clamp(fog * (0.85 + 0.3 * far), 0, 1) * 4) : 0;
                // light (A, B, D)
                double nl = nX * l[0] + nY * l[1] + nZ * l[2];
                double vis = sunOn && nl > 0 && lvl < 3 && !(occ != null && occ[i] != 0) ? SunVis(g, X, Y, l) * (lvl == 2 ? 0.45 : 1) : 0;
                double sun = kD * sunI * (nl > 0 ? Math.Pow(nl, 1.25) : 0) * vis;
                double e0 = (kS * skyI * (0.30 + 0.70 * ao) * (skyv != null ? skyv[i] : 1) * (0.45 + 0.55 * nZ) * (lvl >= 1 ? 0.78 : 1) + sun) * expo;
                int db = grade ? BandOff(e0, true) : (ao < 0.7 ? -1 : 0);
                if (grade && lvl == 1 && sunI < 0.3) db -= 1;
                int lit = sun > 0.22 ? 1 : 0;
                // water: the sea below the tide + swash, permanent water, puddles (G)
                double wetT = wet0; bool isW = c == ClWater; double depth = elev != null ? 1 : 0.2; int foam = 0, sheen = 0; double shore = 0;
                if (elev != null && tide != null)
                {
                    double e = elev[i]; SwashAt(X, fi, sky, out double swS, out double swA, out bool swBack); double front = tide.Value + swS;
                    if (e < front)
                    {
                        isW = true; depth = front - e;
                        double sl = g.Slope != null ? Math.Min(1, g.Slope[i] * 6) : 0, fe = g.Fetch != null ? g.Fetch[i] : 1;
                        double fw = 0.012 + (0.02 + (g.Slope != null ? g.Slope[i] : 0)) * (1.1 + 2.4 * wind) * (0.4 + 0.6 * fe), ph2 = Tau * fi / Loop;
                        if (depth < fw) { double lace = Vn(X / 3.1 + 1.6 * Math.Cos(ph2), Y / 1.6 + 1.6 * Math.Sin(ph2), swBack ? 68 : 61); if (lace > 0.28 + 0.5 * depth / fw) foam = 1; }
                        else if (depth < 0.1 + 0.12 * wind && sl < 0.5 && fe > 0.3) { double st = Vn(X / 5.5 + 2 * Math.Cos(ph2), Y / 1.3 + 2 * Math.Sin(ph2), 67); if (st > 0.87 - 0.1 * wind) foam = 2; }
                    }
                    else
                    {
                        if (e < tide.Value + swA * 1.1) { shore = 0.95; if (swBack || e < front + 0.035) sheen = 1; }
                        else if (e < tideHigh.Value) shore = 0.55 + 0.4 * Clamp((tideHigh.Value - e) / 0.4, 0, 1);
                        wetT = Math.Max(wetT, shore);
                    }
                }
                if (!isW && wetT > 0.25 && g.Pond[i] > 0.18 && IsPud(g, X, Y, i, c, wetT)) { isW = true; depth = 0.05; }
                int pid, bi, spc = 0, wk = 0; int[] rgb;
                if (isW)
                {
                    // waves: a swell running to the shore on the loop, a chop on top, both with the wind
                    double fetch = g.Fetch != null ? g.Fetch[i] : 1;
                    double amp = (0.16 + 0.84 * wind) * Clamp(depth / 0.8, 0.12, 1) * (0.2 + 0.8 * fetch), ph = Tau * fi / Loop;
                    double q1 = Y * 0.62 + 1.6 * Math.Sin(X * 0.043 + Y * 0.017) + 3.1 * Vn(X / 58.0, Y / 21.0, 13) - ph * sdir;
                    double q2 = X * 0.37 + Y * 0.81 + 2.2 * Vn(X / 17.0, Y / 9.0, 31) + 2 * ph;
                    double cw = Math.Sin(q1), dcy = Math.Cos(q1) * 0.62, dcx = Math.Cos(q1) * 0.07 + 0.35 * wind * Math.Cos(q2) * 0.37;
                    double wx = -amp * dcx, wy = -amp * (dcy + 0.35 * wind * Math.Cos(q2) * 0.81), wz = 1; double wl = Hypot(wx, wy, wz); wx /= wl; wy /= wl; wz /= wl;
                    double nv = wx * View[0] + wy * View[1] + wz * View[2]; rv[0] = 2 * nv * wx - View[0]; rv[1] = 2 * nv * wy - View[1]; rv[2] = 2 * nv * wz - View[2];
                    // crests broken into dashes by a mask that circles through noise once per loop, so it tiles in time
                    double mk = Vn(X / 7.0 + 2.5 * Math.Cos(ph), Y / 2.5 + 2.5 * Math.Sin(ph), 47), ak = Clamp(amp / 0.45, 0.15, 1.4);
                    double crest = (cw + 0.45 * wind * Math.Sin(q2)) * ak;
                    bi = crest > 0.8 && mk > 0.5 ? 3 : crest < -0.85 && mk < 0.42 ? 1 : 2;
                    bi = Clamp(bi + (db <= -2 ? -1 : 0), 0, 4);
                    pid = depth < 0.6 ? pws : pw;
                    if (sunOn)
                    {
                        double sp = rv[0] * l[0] + rv[1] * l[1] + rv[2] * l[2];
                        if (sunI > 0.08 && sp > 0.955 - 0.05 * wind && Hash2(X, Y, 11 + fi) < 0.55) { bi = 4; spc = 2; }
                        else if (sunI > 0.08 && sp > 0.86 && crest > 0.5 && mk > 0.5 && Hash2(X, Y, 17 + fi) < 0.35) bi = 4;
                    }
                    if (wind > 0.55 && fetch > 0.5 && crest > 0.95 && mk > 0.62 && Hash2(X >> 1, Y, 23 + fi) < (wind - 0.55) * 1.1) { pid = pf; bi = 3; spc = 0; }
                    if (rain > 0.04)
                    {
                        // rain rings, one drop per 7×7 cell per loop
                        int cx = (int)Math.Floor(X / 7.0), cy = (int)Math.Floor(Y / 7.0); double h = Hash2(cx, cy, 41);
                        if (h < rain * 0.9)
                        {
                            double ox = cx * 7 + 1 + Hash2(cx, cy, 42) * 5, oy = cy * 7 + 1 + Hash2(cx, cy, 43) * 5; int age = (fi + (int)Math.Floor(Hash2(cx, cy, 44) * Loop)) % Loop; double r = age * 0.45;
                            if (r < 3.2 && Math.Abs(Hypot((X - ox) * 0.8, Y - oy) - r) < 0.55) { bi = Math.Min(4, bi + 1); spc = 0; }
                        }
                    }
                    if (foam != 0) { pid = pf; bi = foam == 2 ? 2 + (Hash2(X, Y, 31 + fi) < 0.3 ? 1 : 0) : Hash2(X, Y, 29 + fi) < 0.6 ? 3 : 4; bi = Clamp(bi + (db <= -2 ? -1 : 0), 0, 4); spc = 0; }
                    rgb = Colour(pid, bi, lit, fq, spc, 0);
                    if (depth < 0.16 && foam == 0)
                    {
                        // the shallows: the bottom through three steps of water
                        int pal0 = g.Pal[i], b0 = Clamp(g.Band0[i] + db, 0, 4); int[] gnd = Colour(pal0, b0, lit, fq, 0, 2);
                        rgb = MixC(gnd, rgb, depth < 0.04 ? 0.3 : depth < 0.09 ? 0.5 : 0.7);
                    }
                }
                else
                {
                    pid = g.Pal[i]; bi = g.Band0[i] + db;
                    // live tip and seam (C)
                    int k = g.Mark[i];
                    if (k != 0)
                    {
                        bool seam = false;
                        if (ax != 0) { int j = g.Ix(X + ax, Y); if (g.Mark[j] != k && g.H[i] - g.H[j] > 0.2) seam = true; }
                        if (!seam && ay != 0) { int j = g.Ix(X, Y + ay); if (g.Mark[j] != k && g.H[i] - g.H[j] > 0.2) seam = true; }
                        if (seam && bi >= 1) bi -= 1;
                        else if (lit != 0 && g.Proud[i] > 0.12) { if (tipOf.TryGetValue(k, out Tip tp) && tp.I == i) bi += 1; }
                    }
                    // wet (E)
                    SurfaceClass cc = Classes[c]; double wp = wetT * cc.Por;
                    wk = wp > 0.21 ? 2 : wp > 0.06 ? 1 : 0;
                    if (wetT > 0.3 && cc.Gloss > 0.3)
                    {
                        if (lit != 0 && bi >= 3 && Hash2(k != 0 ? k : i, 3, 17) < wetT * cc.Gloss * 0.8) spc = 2;
                        else if (nZ > 0.9 && Vn(X / 7.0, Y / 5.0, 19) > 1 - wetT * cc.Gloss * 0.42) spc = 3;
                    }
                    if (sheen != 0 && spc == 0) spc = 3;
                    // snow (F)
                    if (snow > 0.02 && cc.Hold > 0 && shore < 0.5)
                    {
                        double e = SnowE(g, X, Y, i, lvl), thr = snow * 1.3 - 0.12;
                        if (e < thr)
                        {
                            pid = ps; bi = Clamp(3 + db + (bi - g.Band0[i] > 0 ? 1 : 0), 0, 4); wk = 0; spc = 0;
                            if (lit != 0 && nZ > 0.96 && Hash2(X, Y, 37) < 0.006) { bi = 4; spc = 2; }
                        }
                        else if (e < thr + 0.05) wk = Math.Max(wk, 1);
                    }
                    rgb = Colour(pid, Clamp(bi, 0, 4), lit, fq, spc, wk);
                }
                int o4 = li * 4; outB[o4] = (byte)rgb[0]; outB[o4 + 1] = (byte)rgb[1]; outB[o4 + 2] = (byte)rgb[2]; outB[o4 + 3] = g.A != null ? g.A[i] : (byte)255;
            }
            return outB;
        }
    }
}
