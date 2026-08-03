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
    /// <para><b>Number formatting is where a port drifts silently</b> — JS <c>toFixed(1)</c> rounds
    /// HALF-UP on the exact binary value of the double, while .NET's "F" format and
    /// <c>(decimal)someDouble</c> both round the 15-significant-digit shortening first, and the two
    /// disagree at values like 0.15. That difficulty is worth solving once, so the formatters now live in
    /// <see cref="RigNumberFormat"/> (ADR 0025 S3, Ruling B: <c>fishRig.js:128-130</c> is
    /// character-identical to this rig's, and a second port would drift silently). The five members below
    /// are thin FORWARDERS kept so every S2 call site and its goldens stay exactly as shipped.</para>
    /// </summary>
    public static class DepthRigGeometry
    {
        // standalone sprite size — depthRig.js:11
        public const int W = 480, H = 330;

        /// <summary>Metres → feet, the rig's own constant (depthRig.js:119). Forwards to
        /// <see cref="RigNumberFormat.M2FT"/> — the one copy.</summary>
        public const double M2FT = RigNumberFormat.M2FT;

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

        // ---- the LCD number formatters — FORWARDERS to RigNumberFormat (Ruling B) --------------------
        // Kept here so S2's call sites and its goldens are untouched, and so the depthRig.js line numbers
        // stay documented where a reader of the sounder looks for them. Do NOT re-implement any of these:
        // the shared copy is the point, and a second implementation drifts silently (0.15 is where).

        /// <summary>
        /// The big 7-seg depth readout's string: metres or feet, one decimal — but a value at or past
        /// 100 loses the decimal and prints as a rounded integer (there is only room for four cells).
        /// depthRig.js:120. → <see cref="RigNumberFormat.FmtDepth"/>.
        /// </summary>
        public static string FmtDepth(double metres, bool feet) => RigNumberFormat.FmtDepth(metres, feet);

        /// <summary>The small "AL n.n" set-point string — always one decimal, in the display units.
        /// depthRig.js:121. → <see cref="RigNumberFormat.FmtSet"/>.</summary>
        public static string FmtSet(double metres, bool feet) => RigNumberFormat.FmtSet(metres, feet);

        /// <summary>ECMAScript <c>Number.prototype.toFixed(1)</c>, exactly. →
        /// <see cref="RigNumberFormat.FixedOne"/>, where the half-up-on-the-exact-binary-value reasoning
        /// is written out.</summary>
        public static string FixedOne(double v) => RigNumberFormat.FixedOne(v);

        /// <summary>JS <c>String(Math.round(v))</c> — half toward +∞, then a plain integer string. →
        /// <see cref="RigNumberFormat.JsRoundToString"/>.</summary>
        public static string JsRoundToString(double v) => RigNumberFormat.JsRoundToString(v);

        private static int Max(int a, int b) => a > b ? a : b;
    }
}
