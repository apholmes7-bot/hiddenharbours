using System.Globalization;

namespace HiddenHarbours.UI
{
    /// <summary>One rectangle of the sounder's layout, in rig pixels (top-left origin, y down).</summary>
    public readonly struct RigRect
    {
        public readonly int X, Y, W, H;
        public RigRect(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }

        /// <summary>Point-in-rect, half-open on the far edges (the canvas hit-box convention).</summary>
        public bool Contains(double px, double py) => px >= X && px < X + W && py >= Y && py < Y + H;
    }

    /// <summary>The sounder's box-parameterised layout — <c>depthRig.js:124-136</c>.</summary>
    public readonly struct DepthRigLayout
    {
        public readonly int Pad, BrandH, ColW;
        public readonly RigRect Lcd, Col, Brand;
        public readonly RigRect Button0, Button1, Button2;   // SET (units) · ALARM up · ALARM down

        public DepthRigLayout(int pad, int brandH, int colW, RigRect lcd, RigRect col, RigRect brand,
                              RigRect b0, RigRect b1, RigRect b2)
        {
            Pad = pad; BrandH = brandH; ColW = colW;
            Lcd = lcd; Col = col; Brand = brand;
            Button0 = b0; Button1 = b1; Button2 = b2;
        }

        /// <summary>The three side pushers by index (0 = units/SET, 1 = alarm +step, 2 = alarm −step).</summary>
        public RigRect Button(int i) => i == 0 ? Button0 : i == 1 ? Button1 : Button2;
    }

    /// <summary>
    /// The depth sounder's PURE geometry + number formatting — the immutable rig source is
    /// <c>docs/art/rigs/ui/depth-finder/Art/depthRig.js</c> (ADR 0025, Option A). Split out of the
    /// painter so the layout (which is also the hit geometry) and the two LCD formatters are
    /// EditMode-testable without a canvas, exactly as <c>LeverRigGeometry</c> is for the lever.
    ///
    /// <para><b>Number formatting is where a port drifts silently</b>, so <see cref="FmtDepth"/> and
    /// <see cref="FmtSet"/> reproduce ECMAScript <c>Number.prototype.toFixed(1)</c> deliberately rather
    /// than trusting <c>ToString("F1")</c>: JS rounds HALF-UP on the exact binary value of the double,
    /// while .NET's "F" format and <c>(decimal)someDouble</c> both round the 15-significant-digit
    /// shortening — which disagrees at values like 0.15 (exactly 0.1499999999999999944…, so JS prints
    /// "0.1" and a naive port prints "0.2"). See <see cref="FixedOne"/>.</para>
    /// </summary>
    public static class DepthRigGeometry
    {
        // standalone sprite size — depthRig.js:11
        public const int W = 480, H = 330;

        /// <summary>Metres → feet, the rig's own constant (depthRig.js:119).</summary>
        public const double M2FT = 3.28084;

        // The standalone screen inset (depthRig.js:262) — X 6% / Y 11% / W 88% / H 78% of the canvas.
        // Pre-resolved to the integers the rig computes so the port and the hit test share ONE box.
        public static readonly int InsetX = DrawSurface.JsRound(W * 0.06);        // 29
        public static readonly int InsetY = DrawSurface.JsRound(H * 0.11);        // 36
        public static readonly int InsetW = W - 2 * DrawSurface.JsRound(W * 0.06); // 422
        public static readonly int InsetH = DrawSurface.JsRound(H * 0.78);        // 257

        /// <summary>The unit's mount rect on the standalone sprite (the box <c>paint</c> draws into).</summary>
        public static RigRect StandaloneMount() => new RigRect(InsetX, InsetY, InsetW, InsetH);

        /// <summary>The layout of the whole instrument inside a mount rect — depthRig.js:124-136. Both
        /// the painter and the pointer hit-test read this, so the pushers can never be drawn somewhere
        /// other than where they are clicked.</summary>
        public static DepthRigLayout Layout(int x, int y, int ww, int hh)
        {
            int pad = Max(3, DrawSurface.JsRound(ww * 0.045));
            int brandH = Max(7, DrawSurface.JsRound(hh * 0.13));
            int colW = Max(16, DrawSurface.JsRound(ww * 0.215));
            int bodyH = hh - pad * 2 - brandH;

            var lcd = new RigRect(x + pad, y + pad, ww - pad * 3 - colW, bodyH);
            var col = new RigRect(x + ww - pad - colW, y + pad, colW, bodyH);

            int bgp = Max(2, DrawSurface.JsRound(col.H * 0.05));
            int bh = (int)System.Math.Floor((col.H - bgp * 2) / 3.0);
            var b0 = new RigRect(col.X, col.Y + 0 * (bh + bgp), col.W, bh);
            var b1 = new RigRect(col.X, col.Y + 1 * (bh + bgp), col.W, bh);
            var b2 = new RigRect(col.X, col.Y + 2 * (bh + bgp), col.W, bh);

            var brand = new RigRect(x + pad, y + hh - brandH - DrawSurface.JsRound(pad * 0.3),
                                    ww - pad * 2, brandH);

            return new DepthRigLayout(pad, brandH, colW, lcd, col, brand, b0, b1, b2);
        }

        // ---- the LCD number formatters (depthRig.js:120-121) --------------------------------------

        /// <summary>
        /// The big 7-seg depth readout's string: metres or feet, one decimal — but a value at or past
        /// 100 loses the decimal and prints as a rounded integer (there is only room for four cells).
        /// depthRig.js:120.
        /// </summary>
        public static string FmtDepth(double metres, bool feet)
        {
            double v = feet ? metres * M2FT : metres;
            if (v >= 100.0) return JsRoundToString(v);
            return FixedOne(v);
        }

        /// <summary>The small "AL n.n" set-point string — always one decimal, in the display units.
        /// depthRig.js:121.</summary>
        public static string FmtSet(double metres, bool feet)
            => FixedOne(feet ? metres * M2FT : metres);

        /// <summary>
        /// ECMAScript <c>Number.prototype.toFixed(1)</c>, exactly: take the sign off, round the
        /// <b>exact</b> binary value of the double half-UP to one decimal, print with one decimal, put
        /// the sign back (so <c>(-0.04).toFixed(1)</c> is <c>"-0.0"</c>, as in JS).
        ///
        /// <para><b>Why the round-trip through a 17-digit string.</b> A direct
        /// <c>(decimal)someDouble</c> cast rounds to 15 significant digits FIRST, which turns
        /// 0.1499999999999999944… into a clean 0.150000000000000 and then rounds it UP — the port would
        /// print "0.2" where the rig prints "0.1". "G17" round-trips the double, so the tie cases (which
        /// at one decimal are exactly the quarter values 0.25, 0.75, 1.25, … — the only ones a binary
        /// double can land on) survive intact and round half-up as JS does.</para>
        /// </summary>
        public static string FixedOne(double v)
        {
            if (double.IsNaN(v)) return "NAN";
            if (double.IsInfinity(v)) return v > 0 ? "INFINITY" : "-INFINITY";
            bool negative = v < 0.0;
            double magnitude = negative ? -v : v;
            // Defensive floor, far outside a sounder's domain: decimal cannot hold ~1e28, and a double
            // that large has no fractional part left to round anyway.
            if (magnitude >= 1e15) return (negative ? "-" : "") + JsRoundToString(magnitude) + ".0";
            decimal exact = decimal.Parse(magnitude.ToString("G17", CultureInfo.InvariantCulture),
                                          NumberStyles.Float, CultureInfo.InvariantCulture);
            decimal rounded = System.Math.Round(exact, 1, System.MidpointRounding.AwayFromZero);
            return (negative ? "-" : "") + rounded.ToString("0.0", CultureInfo.InvariantCulture);
        }

        /// <summary>JS <c>String(Math.round(v))</c> — half toward +∞, then a plain integer string.</summary>
        public static string JsRoundToString(double v)
            => ((long)System.Math.Floor(v + 0.5)).ToString(CultureInfo.InvariantCulture);

        private static int Max(int a, int b) => a > b ? a : b;
    }
}
