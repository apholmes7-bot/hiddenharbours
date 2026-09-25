using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>The pass-4 tree shader's wind, flutter, gather and snow, on the CPU.</b> A line-for-line twin
    /// of <c>Art/Shaders/Include/TreeWindMaps.hlsl</c>, which is itself a port of the drop's reference
    /// shader, <c>TreeMaps4.shade()</c> in <c>docs/art/rigs/treeMaps4.js</c>: every screen pixel asks
    /// which REST texel the wind has carried onto it this frame, and draws that texel's colour (or the
    /// snow or the dark gap the contract's palette names for it).
    ///
    /// <para><b>⚠ WHY A TWIN AT ALL.</b> The reference shader is JavaScript and the game's is HLSL, and
    /// neither runs in a headless EditMode test. This one does, and the tests hold it to both ends: to
    /// the JS through the repo's own V8 (<c>TreeWindMathRigParityTests</c>: the source texel of every
    /// pixel of a cell, frame for frame, against <c>shade().src</c>), and to the HLSL through its SOURCE
    /// (<c>TreeWindMathTests</c>: every constant and load-bearing expression here must still be in the
    /// include, so the twin cannot quietly describe a shader that has moved on). Same idiom as
    /// <see cref="GrassWindMath"/> and <c>SpriteLightMath</c>.</para>
    ///
    /// <para><b>What the port changes, on purpose — three things, each named where it happens.</b>
    /// (1) The rig's wind is a sign and a strength (<c>{ w, dir: ±1 }</c>); ours is the 2-D
    /// <c>_WindWorld</c> vector. The sign is its x; the STEADY lean (the trunk's <c>w²</c> term and the
    /// limbs' <c>+0.5</c>) is weighted by <see cref="Frame.Side"/>, the share of the wind that runs
    /// along x, so a north wind shakes a tree without leaning it east. With the wind along x, Side is 1
    /// and the rig's arithmetic is untouched. (2) The rig is handed its frame; the shader picks one per
    /// tree from the clock, a travelling wave along the wind and a per-tree jitter
    /// (<see cref="LoopPos"/>). (3) The rig's turn-over flag (<c>tRate</c>, the aspen's pale underside)
    /// only recolours in the rig's own relight, which the game does not run: declined, and the report
    /// says what it costs.</para>
    ///
    /// <para><b>The maps.</b> Rig order: x right, y DOWN from the cell's top, 4 bytes a texel, exactly
    /// as <c>HHTreePass4.cell()</c> returns them. <c>wind</c>: R lean · G sway · B|A the top of the
    /// packed word. <c>phase</c>: R wave · G play · B depth · A the word's low byte. The word is
    /// <c>class 2 · gapSnow 3 · gap 3 · snow 3 · stamp id + 1 13</c> (<c>TreePass4Glue</c>). The
    /// shader reads the same bytes through <see cref="TexelOfRig"/>, because Unity's texel rows run
    /// bottom-up.</para>
    /// </summary>
    public static class TreeWindMath
    {
        // ---- the constants: each is a #define TREE_WIND_* in TreeWindMaps.hlsl -------------------------

        /// <summary>Frames in the rig's wind loop (<c>TreeRig4.LOOP</c>).</summary>
        public const int Loop = 16;

        /// <summary><c>phase.R</c> decodes to <c>(r − ½) · XSpan</c>: where a texel sits along the
        /// wave.</summary>
        public const float XSpan = 4f;

        /// <summary><c>phase.G</c> decodes to <c>(g − ½) · JSpan</c>: a limb's own play.</summary>
        public const float JSpan = 0.5f;

        /// <summary><c>wind.G</c> decodes to <c>g · RMax</c>: how far out on its limb a texel
        /// is.</summary>
        public const float RMax = 1.3f;

        /// <summary>Depth bytes within this of each other are a tie, and the leaf rule decides.</summary>
        public const int Margin = 3;

        /// <summary>The gather searches this many texels either side of where it lands: 5 × 5.</summary>
        public const int Reach = 2;

        /// <summary>The fixed-point steps the gather takes back along the field.</summary>
        public const int Iterations = 2;

        /// <summary>Flutter slots a leaf has in one loop.</summary>
        public const int FlutterSlots = 2;

        /// <summary>A leaf gone from over the dark between leaves shows the gap, and the gap snows no
        /// earlier than this byte (cover 0.6: the rig's <c>between || snow &gt; 0.6</c>).</summary>
        public const int GapSnowMin = 153;

        /// <summary>A texel is snow when its snow byte ≤ <c>round(cover · SnowScale)</c>.</summary>
        public const float SnowScale = 254f;

        /// <summary>At or below this cover nothing is snow (the rig's <c>snow &gt; 0.02</c>).</summary>
        public const float SnowOn = 0.02f;

        public const int ClassOutside = 0;
        public const int ClassWood = 1;
        public const int ClassBetween = 2;
        public const int ClassLeaf = 3;

        /// <summary>A leaf's sub-pixel dither: <c>±0.4</c> px, which rounds to nothing at rest.</summary>
        public const float Dither = 0.8f;

        /// <summary>The rig's flutter length in frames: the chance per slot is scaled by
        /// <c>Loop / FlutterFrames</c>.</summary>
        public const float FlutterFrames = 2.9f;

        public const float Tau = 6.28318530718f;

        // ---- the rig's arithmetic, exactly ---------------------------------------------------------------

        /// <summary>JavaScript's <c>Math.round</c>: a half rounds UP (<c>−2.5 → −2</c>), where
        /// <see cref="Mathf.Round"/> rounds a half to even. Every offset in the rig goes through it.</summary>
        public static int JsRound(float x)
        {
            float f = Mathf.Floor(x);
            return (int)f + (x - f >= 0.5f ? 1 : 0);
        }

        /// <summary>The rig's integer hash, bit for bit (<c>hsh</c> in treeMaps4.js, which copies the
        /// rig's own): the dither and the flutter slots key on it. <see cref="HashF"/> is the rig's
        /// <c>[0, 1)</c> reading of it.</summary>
        public static uint Hash(int n, int k)
        {
            unchecked
            {
                uint h = ((uint)n ^ ((uint)(k + 1) * 0x9e3779b9u)) * 0x85ebca6bu;
                h ^= h >> 13;
                h *= 0xc2b2ae35u;
                h ^= h >> 16;
                return h;
            }
        }

        public static float HashF(int n, int k) => Hash(n, k) * (1f / 4294967296f);

        public static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>A texture's UNORM value back to its byte, as the shader reads every map:
        /// <c>(uint)(u · 255 + 0.5)</c>. Exact for all 256 bytes.</summary>
        public static int ByteOf(float unorm) => (int)(unorm * 255f + 0.5f);

        // ---- the per-renderer rows and the per-frame state -------------------------------------------------

        /// <summary>The material rows the wind reads. Each names its shader row; a species' numbers come
        /// from <c>Trees.json</c> (the rig's <c>constants()</c>, written by the baker), never typed.</summary>
        public struct Params
        {
            public float BendPx;       // _TreeWind0.x — trunk bend at a full gale (px)
            public float LimbPx;       // _TreeWind0.y — limb reach at a full gale (px)
            public float Bob;          // _TreeWind0.z — a limb's vertical bob, as a share of its reach
            public float Flutter;      // _TreeWind0.w — the species' flutter rate
            public float ShimmerCalm;  // _TreeWind1.x — shimmer strength × calm share: still-air flutter
            public bool Conifer;       // _TreeWind1.y — a needle tuft flutters sideways, never up
            public float Gust;         // _TreeGust — the gust envelope's depth (the rig's default 0.4)
            public float Response;     // _TreeWindResponse — _WindWorld's strength → the rig's w
        }

        /// <summary>Everything one frame of one tree shares: computed per fragment from uniforms only,
        /// so it costs arithmetic, never a texture read.</summary>
        public struct Frame
        {
            public float Ww;         // the rig's w: 0 calm … 1 a full gale
            public float Dir;        // ±1: which way along x the wind blows
            public float Side;       // the share of the wind along x: the steady lean's weight
            public int F16;          // the loop frame, 0 … Loop − 1
            public float Ph;         // F16 / Loop
            public float Env;        // the gust envelope
            public float Trunk;      // the trunk's sway at full lean (px)
            public float La;         // the limbs' amplitude (px)
            public float LimbBias;   // the limbs' steady lean: the rig's +0.5, weighted by Side
            public float DRate;      // the chance a leaf's flutter slot fires
            public float Gust;
            public float Bob;
            public bool Conifer;
            public bool Still;       // calm and no shimmer: every texel draws itself
        }

        /// <summary>The rig's frame constants (<c>shade()</c>'s head), from the wind vector.</summary>
        public static Frame FrameOf(Vector2 windWorld, int f16, in Params p)
        {
            if ((uint)f16 >= Loop)
                throw new ArgumentOutOfRangeException(nameof(f16), f16, $"a loop frame is 0..{Loop - 1}");

            Frame fr;
            float len = windWorld.magnitude;
            fr.Ww = Mathf.Clamp01(len * p.Response);
            fr.Dir = windWorld.x < 0f ? -1f : 1f;
            // (1): the steady lean follows the wind's x share. Side = 1 with the wind along x.
            fr.Side = Mathf.Abs(windWorld.x) / Mathf.Max(len, 1e-4f);
            fr.Gust = Mathf.Clamp01(p.Gust);
            fr.F16 = f16;
            fr.Ph = f16 / (float)Loop;
            fr.Env = 1f + fr.Gust * 0.9f * Mathf.Sin(Tau * fr.Ph + 0.7f);
            fr.Trunk = fr.Ww * fr.Ww * p.BendPx * (0.8f + 0.2f * fr.Env) * fr.Side
                     + fr.Ww * p.BendPx * 0.42f * Mathf.Sin(Tau * fr.Ph + 0.3f) * (0.55f + 0.45f * fr.Env);
            fr.La = p.LimbPx * fr.Ww * (0.6f + 0.4f * fr.Env);
            fr.LimbBias = 0.5f * fr.Side;
            float resp = Smooth(0f, 0.85f, fr.Ww);
            fr.DRate = Mathf.Min(1f, (p.Flutter * resp * 0.35f + p.ShimmerCalm * 0.03f) * Loop / FlutterFrames);
            fr.Bob = p.Bob;
            fr.Conifer = p.Conifer;
            fr.Still = fr.Ww <= 0f && fr.DRate <= 0f;
            return fr;
        }

        /// <summary>(2): where in its loop a tree is. The clock, minus a wave travelling along the wind
        /// (a gust crosses a stand instead of every tree swaying as one), plus the tree's own jitter.
        /// <paramref name="noise"/> is the shader's <c>ValueNoise(root · _PhaseScale)</c>, in 0 … 1.</summary>
        public static float LoopPos(float time, Vector2 root, Vector2 windWorld, float loopSeconds,
                                    float waveScale, float noise, float jitter)
        {
            float len = windWorld.magnitude;
            Vector2 wdir = len > 1e-4f ? windWorld / len : new Vector2(1f, 0f);
            return time / Mathf.Max(loopSeconds, 1e-3f) - Vector2.Dot(root, wdir) * waveScale + noise * jitter;
        }

        public static int F16(float loopPos)
        {
            float frac = loopPos - Mathf.Floor(loopPos);
            return Mathf.Clamp((int)Mathf.Floor(frac * Loop), 0, Loop - 1);
        }

        // ---- one texel of the maps -------------------------------------------------------------------------

        /// <summary>One texel of the wind and phase maps, decoded as the rig's <c>decoded()</c> does. The
        /// shader reads the four floats straight from the UNORM texture, whose value IS <c>byte / 255</c>,
        /// and rebuilds the bytes of the word and the depth with <see cref="ByteOf"/>.</summary>
        public struct Texel
        {
            public float Lean;   // wind.R / 255: how much of the trunk's sway it takes
            public float Sway;   // wind.G / 255 · RMax: how far out on its limb it is
            public float X;      // (phase.R / 255 − ½) · XSpan: where it sits along the wave
            public float J;      // (phase.G / 255 − ½) · JSpan: its limb's own play
            public int Word;     // wind.B << 16 | wind.A << 8 | phase.A
            public int Dep;      // phase.B: its depth, 255 the nearest
        }

        public static Texel TexelOf(int windR, int windG, int windB, int windA,
                                    int phaseR, int phaseG, int phaseB, int phaseA)
        {
            Texel t;
            t.Lean = windR / 255f;
            t.Sway = windG / 255f * RMax;
            t.X = (phaseR / 255f - 0.5f) * XSpan;
            t.J = (phaseG / 255f - 0.5f) * JSpan;
            t.Word = WordOf(windB, windA, phaseA);
            t.Dep = phaseB;
            return t;
        }

        public static int WordOf(int windB, int windA, int phaseA) => (windB << 16) | (windA << 8) | phaseA;
        public static int ClassOf(int word) => word >> 22;
        public static int GapSnowIndexOf(int word) => (word >> 19) & 7;
        public static int GapIndexOf(int word) => (word >> 16) & 7;
        public static int SnowIndexOf(int word) => (word >> 13) & 7;
        public static int IdOf(int word) => word & 0x1FFF;

        /// <summary>A leaf's stamp: the id the hashes key on. The word carries it + 1.</summary>
        public static int StampOf(int word) => IdOf(word) - 1;

        // ---- the field, the offsets, the depth rule ------------------------------------------------------

        /// <summary>The displacement field at a texel (<c>shade()</c>'s <c>FX</c>, <c>FY</c>), in px,
        /// y down. Zero in calm. The field is defined OUTSIDE the silhouette too (the rig fills it
        /// outward), so a gather that starts off the tree still finds its way back.</summary>
        public static Vector2 Field(in Frame fr, in Texel t)
        {
            if (fr.Ww <= 0f) return Vector2.zero;
            float p = 2f * Tau * fr.Ph - fr.Dir * 0.9f * t.X + t.J;
            float reach = t.Sway > 0f ? Mathf.Pow(t.Sway, 1.2f) : 0f;
            float fx = fr.Dir * (fr.Trunk * t.Lean + fr.La * reach * (Mathf.Sin(p) + fr.LimbBias));
            float fy = -fr.La * fr.Bob * 0.5f * t.Sway * Mathf.Sin(p + 1.1f);
            return new Vector2(fx, fy);
        }

        /// <summary>Where a texel moves this frame, in whole px: wood and the dark between leaves
        /// follow the field; a leaf moves as one stamp, with a dither and, on its slots, a flutter.
        /// The maps are flat across a stamp, so every texel of a leaf computes the same offset.</summary>
        public static Vector2Int Offset(in Frame fr, in Texel t)
        {
            Vector2 f = Field(fr, t);
            if (ClassOf(t.Word) != ClassLeaf) return new Vector2Int(JsRound(f.x), JsRound(f.y));

            int gs = StampOf(t.Word);
            int sx = JsRound(f.x + (HashF(gs, 8) - 0.5f) * Dither);
            int sy = JsRound(f.y + (HashF(gs, 9) - 0.5f) * Dither);
            if (fr.DRate > 0f)
            {
                int dir = fr.Dir < 0f ? -1 : 1;
                // The rig breaks out of this loop on the first slot that fires; a `moved` flag is the same
                // rule in a form the shader can unroll.
                bool moved = false;
                for (int e = 0; e < FlutterSlots; e++)
                {
                    int s0 = (int)(Hash(gs, 10 + e) >> 28);   // == (hsh · 16) | 0, exactly
                    int age = (fr.F16 - s0 + Loop) % Loop;
                    int span = HashF(gs, 12 + e) < 0.55f ? 1 : 2;
                    float gate = fr.DRate * (1f + fr.Gust * 0.9f * Mathf.Sin(Tau * s0 / Loop + 0.7f - fr.Dir * t.X * 1.3f));
                    if (!moved && age < span && HashF(gs, 14 + e) < gate)
                    {
                        float r = HashF(gs, 16 + e);
                        if (fr.Conifer) sx += r < 0.8f ? dir : -dir;
                        else if (r < 0.55f) sx += dir;
                        else if (r < 0.75f) sy -= 1;
                        else if (r < 0.9f) { sx += dir; sy -= 1; }
                        else sx -= dir;
                        moved = true;
                    }
                }
            }
            return new Vector2Int(sx, sy);
        }

        /// <summary>Whether <paramref name="j"/> draws in front of <paramref name="b"/> when both land
        /// on one pixel: the nearer beyond <see cref="Margin"/>, then a leaf over the rest, then the
        /// later stamp.</summary>
        public static bool Front(in Texel j, in Texel b)
        {
            if (j.Dep > b.Dep + Margin) return true;
            if (b.Dep > j.Dep + Margin) return false;
            bool lj = ClassOf(j.Word) == ClassLeaf, lb = ClassOf(b.Word) == ClassLeaf;
            if (lj != lb) return lj;
            return lj ? IdOf(j.Word) > IdOf(b.Word) : j.Dep > b.Dep;
        }

        // ---- the gather ------------------------------------------------------------------------------------

        /// <summary>One cell's wind and phase maps, in rig order (see the class summary).</summary>
        public sealed class Maps
        {
            public readonly int Width;
            public readonly int Height;
            readonly byte[] _wind;
            readonly byte[] _phase;

            public Maps(int width, int height, byte[] wind, byte[] phase)
            {
                if (width <= 0 || height <= 0)
                    throw new ArgumentOutOfRangeException(nameof(width), $"a cell is at least 1 × 1, not {width} × {height}");
                if (wind == null || wind.Length != width * height * 4)
                    throw new ArgumentException($"the wind map is {width} × {height} × 4 bytes, not {wind?.Length ?? 0}", nameof(wind));
                if (phase == null || phase.Length != width * height * 4)
                    throw new ArgumentException($"the phase map is {width} × {height} × 4 bytes, not {phase?.Length ?? 0}", nameof(phase));
                Width = width;
                Height = height;
                _wind = wind;
                _phase = phase;
            }

            public Texel At(int x, int y)
            {
                int o = (y * Width + x) * 4;
                return TexelOf(_wind[o], _wind[o + 1], _wind[o + 2], _wind[o + 3],
                               _phase[o], _phase[o + 1], _phase[o + 2], _phase[o + 3]);
            }
        }

        /// <summary>
        /// Which rest texel lands on rig pixel (<paramref name="x"/>, <paramref name="y"/>) this frame.
        /// Step twice back along the field; of the 25 texels within <see cref="Reach"/> of where that
        /// lands, keep those whose own offset brings them here, and the frontmost wins. None? The texel
        /// under the landing point shows through, and if it is foliage it shows as the gap a leaf left
        /// (<paramref name="fallback"/>). False: nothing is drawn here.
        /// </summary>
        public static bool Gather(in Frame fr, Maps maps, int x, int y,
                                  out Vector2Int source, out Texel sourceTexel, out bool fallback)
        {
            int w = maps.Width, h = maps.Height;
            source = new Vector2Int(-1, -1);
            sourceTexel = default;
            fallback = false;

            if (fr.Still)
            {
                // Calm and no shimmer: every field is 0 and the dither rounds to 0, so the only texel
                // that lands here is this one. Exact, and 2 loads instead of 54.
                Texel self = maps.At(x, y);
                if (ClassOf(self.Word) == ClassOutside) return false;
                source = new Vector2Int(x, y);
                sourceTexel = self;
                return true;
            }

            int sx = x, sy = y;
            for (int it = 0; it < Iterations; it++)
            {
                Vector2 f = Field(fr, maps.At(Mathf.Clamp(sx, 0, w - 1), Mathf.Clamp(sy, 0, h - 1)));
                sx = x - JsRound(f.x);
                sy = y - JsRound(f.y);
            }

            bool found = false;
            for (int dy = -Reach; dy <= Reach; dy++)
            {
                for (int dx = -Reach; dx <= Reach; dx++)
                {
                    int xx = sx + dx, yy = sy + dy;
                    if (xx < 0 || xx >= w || yy < 0 || yy >= h) continue;
                    Texel c = maps.At(xx, yy);
                    if (ClassOf(c.Word) == ClassOutside) continue;
                    Vector2Int o = Offset(fr, c);
                    if (xx + o.x != x || yy + o.y != y) continue;
                    if (!found || Front(c, sourceTexel))
                    {
                        source = new Vector2Int(xx, yy);
                        sourceTexel = c;
                        found = true;
                    }
                }
            }

            if (!found && sx >= 0 && sy >= 0 && sx < w && sy < h)
            {
                Texel c = maps.At(sx, sy);
                int cls = ClassOf(c.Word);
                if (cls != ClassOutside)
                {
                    source = new Vector2Int(sx, sy);
                    sourceTexel = c;
                    fallback = cls >= ClassBetween;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>The whole cell's gather in rig order, as <c>shade().src</c> reports it: the rig
        /// index <c>y · w + x</c> of each pixel's source, or −1. For the tests.</summary>
        public static int[] SourceMap(in Frame fr, Maps maps, bool[] fallbackOut = null)
        {
            int n = maps.Width * maps.Height;
            if (fallbackOut != null && fallbackOut.Length != n)
                throw new ArgumentException($"the fallback map is {n} long", nameof(fallbackOut));
            var src = new int[n];
            for (int y = 0; y < maps.Height; y++)
            {
                for (int x = 0; x < maps.Width; x++)
                {
                    int i = y * maps.Width + x;
                    bool hit = Gather(fr, maps, x, y, out Vector2Int s, out _, out bool fb);
                    src[i] = hit ? s.y * maps.Width + s.x : -1;
                    if (fallbackOut != null) fallbackOut[i] = hit && fb;
                }
            }
            return src;
        }

        // ---- the colour ---------------------------------------------------------------------------------------

        public enum Source { Albedo, SnowRow, GapRow }

        /// <summary>What a gathered texel draws: its own albedo, or a column of the palette's snow row
        /// (row 0) or of the species' gap row (<c>_TreeWind1.z</c>).</summary>
        public readonly struct Pick
        {
            public readonly Source Source;
            public readonly int Index;

            public Pick(Source source, int index)
            {
                Source = source;
                Index = index;
            }

            public override string ToString() => Source == Source.Albedo ? "albedo" : $"{Source}[{Index}]";
        }

        /// <summary>
        /// The colour rule, as the drop's own check decodes it: a leaf that shows through where it was
        /// carried away (<paramref name="fallback"/>) is the dark gap, which snows from 0.6 cover; every
        /// other texel is its albedo until the cover reaches its snow byte. <paramref name="cover"/> is
        /// <c>_FoliageSnow</c>.
        /// </summary>
        public static Pick Decode(int word, int snowByte, bool fallback, float cover)
        {
            cover = Mathf.Clamp01(cover);
            int cls = ClassOf(word);
            int kk = JsRound(cover * SnowScale);
            bool snowing = cover > SnowOn;
            if (fallback && cls == ClassLeaf)
            {
                bool on = snowing && Mathf.Max(snowByte, GapSnowMin) <= kk;
                return on ? new Pick(Source.SnowRow, GapSnowIndexOf(word)) : new Pick(Source.GapRow, GapIndexOf(word));
            }
            bool lit = snowing && snowByte <= kk;
            return lit ? new Pick(Source.SnowRow, SnowIndexOf(word)) : new Pick(Source.Albedo, 0);
        }

        // ---- sheet texels and rig pixels -------------------------------------------------------------------

        /// <summary>A sheet texel as the rig pixel it is, and the left edge of its cell. The baked sheet
        /// is ONE row of cells, <c>cellW</c> wide each; the rig's rows run down from the cell's top and
        /// Unity's texel rows run up.</summary>
        public static Vector2Int RigOfTexel(Vector2Int texel, int cellW, int cellH, out int cellX0)
        {
            cellX0 = texel.x / cellW * cellW;
            int x = Mathf.Clamp(texel.x - cellX0, 0, cellW - 1);
            int y = cellH - 1 - Mathf.Clamp(texel.y, 0, cellH - 1);
            return new Vector2Int(x, y);
        }

        public static Vector2Int TexelOfRig(int cellX0, int cellH, int x, int y) =>
            new Vector2Int(cellX0 + x, cellH - 1 - y);

        /// <summary>The sheet texel under a uv, kept on the sheet: a uv of exactly 1 would floor one
        /// texel past its edge.</summary>
        public static Vector2Int TexelOfUv(Vector2 uv, float sheetW, float sheetH) =>
            new Vector2Int(Mathf.Clamp((int)Mathf.Floor(uv.x * sheetW), 0, (int)sheetW - 1),
                           Mathf.Clamp((int)Mathf.Floor(uv.y * sheetH), 0, (int)sheetH - 1));

        /// <summary>The centre of a texel as a uv: where the light sheets are read for a carried
        /// texel, so its light moves with it.</summary>
        public static Vector2 SourceUv(Vector2Int texel, float sheetW, float sheetH) =>
            new Vector2((texel.x + 0.5f) / sheetW, (texel.y + 0.5f) / sheetH);
    }
}
