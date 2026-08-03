namespace HiddenHarbours.UI
{
    /// <summary>The fish finder's box-parameterised layout — <c>fishRig.js:137-155</c>. Its own type and
    /// its own constants: the finder is NOT the sounder at a different size (see
    /// <see cref="FishRigGeometry"/>), and it subdivides its glass into three regions the sounder has no
    /// equivalent of.</summary>
    public readonly struct FishRigLayout
    {
        public readonly int Pad, BrandH, ColW;

        /// <summary>The whole recessed glass.</summary>
        public readonly RigRect Lcd;

        /// <summary>The side pusher column.</summary>
        public readonly RigRect Col;

        /// <summary>The wordmark strip along the bottom of the case.</summary>
        public readonly RigRect Brand;

        /// <summary>Inside the glass: the top chrome strip (signal / link / battery / volts). A tap here
        /// toggles the night backlight (Ruling A).</summary>
        public readonly RigRect Status;

        /// <summary>Inside the glass: the right-edge depth scale. A tap here toggles feet/metres
        /// (Ruling A).</summary>
        public readonly RigRect Ruler;

        /// <summary>Inside the glass: the colour sonar column, left of the ruler and below the status
        /// strip. A tap here toggles fish-ID (Ruling A). Fish marks are placed in THIS box's normalised
        /// space.</summary>
        public readonly RigRect Sonar;

        /// <summary>MODE — cycles what the ▲/▼ pushers adjust (Ruling A).</summary>
        public readonly RigRect Button0;

        /// <summary>▲ — steps the active mode up.</summary>
        public readonly RigRect Button1;

        /// <summary>▼ — steps the active mode down.</summary>
        public readonly RigRect Button2;

        public FishRigLayout(int pad, int brandH, int colW, RigRect lcd, RigRect col, RigRect brand,
                             RigRect status, RigRect ruler, RigRect sonar,
                             RigRect b0, RigRect b1, RigRect b2)
        {
            Pad = pad; BrandH = brandH; ColW = colW;
            Lcd = lcd; Col = col; Brand = brand;
            Status = status; Ruler = ruler; Sonar = sonar;
            Button0 = b0; Button1 = b1; Button2 = b2;
        }

        /// <summary>The three side pushers by index (0 = MODE, 1 = ▲, 2 = ▼).</summary>
        public RigRect Button(int i) => i == 0 ? Button0 : i == 1 ? Button1 : Button2;
    }

    /// <summary>
    /// The fish finder's PURE geometry — the immutable rig source is
    /// <c>docs/art/rigs/ui/fish-finder/Art/fishRig.js</c> (ADR 0025, Option A; the ADR names this rig as
    /// the one that MUST be a live renderer). Split out of the painter so the layout — which is also the
    /// hit geometry — and the sonar's procedural contour are EditMode-testable without a canvas, exactly
    /// as <see cref="DepthRigGeometry"/> is for the sounder.
    ///
    /// <para><b>⚠ This is NOT <see cref="DepthRigGeometry"/> with different numbers plugged in — it is a
    /// different layout.</b> Every constant differs (<c>pad</c> 4% vs 4.5%, <c>brandH</c> 11.5% vs 13%,
    /// <c>colW</c> 12% vs 21.5%), the pushers are SPREAD across the column by
    /// <c>span·(i/2)</c> rather than stacked with a gap, and the glass subdivides into
    /// <see cref="FishRigLayout.Status"/> / <see cref="FishRigLayout.Ruler"/> /
    /// <see cref="FishRigLayout.Sonar"/> where the sounder's is one pane. Reusing the sounder's layout
    /// would draw the pushers somewhere other than where they are clicked.</para>
    ///
    /// <para><b>The rig is PORTRAIT</b> — 480×660 against the sounder's 480×330 (<c>fishRig.js:11</c>).
    /// Nothing that sizes a card from the sounder's aspect transfers.</para>
    ///
    /// <para><b>Number formatting is NOT re-ported here.</b> <c>fishRig.js:128-130</c> is
    /// character-identical to <c>depthRig.js:119-121</c>; the one copy lives in
    /// <see cref="RigNumberFormat"/> (ADR 0025 S3, Ruling B).</para>
    /// </summary>
    public static class FishRigGeometry
    {
        /// <summary>Standalone sprite size — <c>fishRig.js:11</c>. PORTRAIT.</summary>
        public const int W = 480, H = 660;

        /// <summary>The rig's own vertical scale choices, metres — <c>fishRig.js:131</c>
        /// <c>RANGE_STEPS = [10,20,40,60]</c>. These are rig ART, not balance: the ruler draws five
        /// divisions and these are the values that label cleanly on it, so they live beside the layout
        /// rather than in <c>GameConfig</c>. What IS an owner tunable is which step a fresh finder starts
        /// on (<c>FishFinderSettings.DefaultRangeMetres</c>).</summary>
        public static readonly float[] RangeSteps = { 10f, 20f, 40f, 60f };

        // The standalone screen inset (fishRig.js:369) — X 6% / Y 11% / W 88% / H 78% of the canvas.
        // Pre-resolved to the integers the rig computes so the port and the hit test share ONE box.
        public static readonly int InsetX = DrawSurface.JsRound(W * 0.06);          // 29
        public static readonly int InsetY = DrawSurface.JsRound(H * 0.11);          // 73
        public static readonly int InsetW = W - 2 * DrawSurface.JsRound(W * 0.06);  // 422
        public static readonly int InsetH = DrawSurface.JsRound(H * 0.78);          // 515

        /// <summary>The unit's mount rect on the standalone sprite (the box <c>paint</c> draws into) —
        /// <c>fishRig.js:369</c>. Parameterised by canvas size because the card state renders the
        /// instrument at a SMALLER rig resolution rather than downscaling a 480×660 texture (point-filtered
        /// pixel art does not survive a fractional downscale, and a smaller raster is also cheaper —
        /// rule 7).</summary>
        public static RigRect StandaloneMount(int canvasW, int canvasH)
        {
            int x = DrawSurface.JsRound(canvasW * 0.06);
            int y = DrawSurface.JsRound(canvasH * 0.11);
            return new RigRect(x, y, canvasW - 2 * x, DrawSurface.JsRound(canvasH * 0.78));
        }

        /// <summary>The mount rect at the rig's native canvas size.</summary>
        public static RigRect StandaloneMount() => new RigRect(InsetX, InsetY, InsetW, InsetH);

        /// <summary>The layout of the whole instrument inside a mount rect — <c>fishRig.js:137-155</c>.
        /// Both the painter and the pointer hit-test read this, so a pusher can never be drawn somewhere
        /// other than where it is clicked.</summary>
        public static FishRigLayout Layout(int x, int y, int ww, int hh)
        {
            int pad = Max(3, DrawSurface.JsRound(ww * 0.04));
            int brandH = Max(7, DrawSurface.JsRound(hh * 0.115));
            int colW = Max(14, DrawSurface.JsRound(ww * 0.12));
            int bodyH = hh - pad * 2 - brandH;

            var lcd = new RigRect(x + pad, y + pad, ww - pad * 3 - colW, bodyH);
            var col = new RigRect(x + ww - pad - colW, y + pad, colW, bodyH);

            // The pushers are SPREAD, not stacked: three keys pinned to the top, middle and bottom of a
            // tall column (fishRig.js:144-146). Nothing like the sounder's bh+gap stack.
            int keyH = Max(14, Min(DrawSurface.JsRound(col.W * 0.72),
                                   (int)System.Math.Floor((col.H - DrawSurface.JsRound(col.H * 0.16)) / 3.0)));
            int span = col.H - keyH;
            var b0 = new RigRect(col.X, DrawSurface.JsRound(col.Y + span * 0.0), col.W, keyH);
            var b1 = new RigRect(col.X, DrawSurface.JsRound(col.Y + span * 0.5), col.W, keyH);
            var b2 = new RigRect(col.X, DrawSurface.JsRound(col.Y + span * 1.0), col.W, keyH);

            var brand = new RigRect(x + pad, y + hh - brandH - DrawSurface.JsRound(pad * 0.3),
                                    ww - pad * 2, brandH);

            // regions inside the LCD glass (fishRig.js:149-153) — the strip height is tied to the glass's
            // WIDTH, not its tall column, so a portrait glass does not get a giant chrome band.
            int stripH = Max(9, DrawSurface.JsRound(lcd.W * 0.075));
            int scaleW = Max(14, DrawSurface.JsRound(lcd.W * 0.115));
            var status = new RigRect(lcd.X, lcd.Y, lcd.W, stripH);
            var ruler = new RigRect(lcd.X + lcd.W - scaleW, lcd.Y + stripH, scaleW, lcd.H - stripH);
            var sonar = new RigRect(lcd.X, lcd.Y + stripH, lcd.W - scaleW, lcd.H - stripH);

            return new FishRigLayout(pad, brandH, colW, lcd, col, brand, status, ruler, sonar, b0, b1, b2);
        }

        // ---- the sonar's own maths (fishRig.js:222-243) ---------------------------------------------

        /// <summary>The rig's clamp — written in its own ternary form so a NaN propagates exactly as it
        /// does in the source (<c>fishRig.js:222</c>), rather than being silently healed here.</summary>
        public static double Clamp(double v, double a, double b) => v < a ? a : (v > b ? b : v);

        /// <summary>
        /// Where on the glass the measured depth sits, 0 (surface) … 1 (bottom of the scale) —
        /// <c>fishRig.js:239</c>, clamped to 0.05…0.985 so the contour never leaves the picture.
        ///
        /// <para><b>⚠ <paramref name="range"/> must be positive.</b> This is the finder's one
        /// divide, and at zero it is Inf/NaN — a garbage contour, not a small one. The save migration heals
        /// a stored non-positive range (schema v9) and <see cref="SafeRange"/> is the belt-and-braces at the
        /// point of use; this stays a faithful port so the goldens mean what they say.</para>
        /// </summary>
        public static double DepthFrac(double depth, double range) => Clamp(depth / range, 0.05, 0.985);

        /// <summary>A range that cannot produce a garbage picture: any non-positive or non-finite value
        /// falls back to <paramref name="fallbackMetres"/> (the owner's
        /// <c>FishFinderSettings.DefaultRangeMetres</c>). Belt-and-braces beside the save migration's heal
        /// — a range reaches the glass from a save file, and the divide is unforgiving.</summary>
        public static float SafeRange(float rangeMetres, float fallbackMetres)
        {
            if (rangeMetres > 0f && !float.IsInfinity(rangeMetres)) return rangeMetres;
            return fallbackMetres > 0f ? fallbackMetres : 20f;
        }

        /// <summary>
        /// The bottom contour at horizontal position <paramref name="u"/> (0…1 across the sonar box),
        /// as a fraction of the glass's height — <c>fishRig.js:240-243</c>.
        ///
        /// <para><b>⚠ There is no waterfall history and no ring buffer.</b> The contour is three sine
        /// trains around the CURRENT depth, evaluated fresh every repaint; nothing is retained between
        /// frames, and the "newest ping at right" comment in the source is decoration. A port that invents
        /// a scroll buffer is inventing a feature the rig does not have (and would then have to decide what
        /// it means, which is a gameplay question nobody asked). The fish MARKS are the opposite — those
        /// are real, and come from the Core school seam.</para>
        /// </summary>
        public static double BottomAt(double u, double phase, double depthFrac)
        {
            double w = 0.055 * System.Math.Sin(u * 6.3 + phase * 0.7)
                     + 0.03 * System.Math.Sin(u * 13.0 - phase * 1.05)
                     + 0.017 * System.Math.Sin(u * 22.0 + phase * 1.9);
            return Clamp(depthFrac + w, 0.06, 0.995);
        }

        /// <summary>The rig's cheap hash noise — <c>fishRig.js:223</c>. Drives only DECORATIVE speckle and
        /// fuzz through <c>&gt; 0.86</c> / <c>&gt; 0.8</c> booleans, so a last-ulp difference in
        /// <c>Math.Sin</c> can at most flip one pixel; the goldens pin the function, never the placement of
        /// speckle.</summary>
        public static double HashRnd(double n)
        {
            double x = System.Math.Sin(n * 127.1 + 13.7) * 43758.5453;
            return x - System.Math.Floor(x);
        }

        /// <summary>Absolute pixel geometry of one fish mark inside a sonar box — <c>fishRig.js:157-160</c>.
        /// <paramref name="x01"/>/<paramref name="y01"/> are normalised in the SONAR box (not the glass, not
        /// the case). Note the floor of 4 px applies BEFORE the size multiplier, exactly as the source has
        /// it.</summary>
        public static void FishGeom(in RigRect sonar, double x01, double y01, double size,
                                    out double cx, out double cy, out double half)
        {
            cx = sonar.X + x01 * sonar.W;
            cy = sonar.Y + y01 * sonar.H;
            half = System.Math.Max(4.0, System.Math.Min(sonar.W, sonar.H) * 0.06) * size;
        }

        // ---- the RANGE pushers (Ruling A) -----------------------------------------------------------

        /// <summary>
        /// Step the vertical scale by <paramref name="steps"/> positions along
        /// <see cref="RangeSteps"/>, clamped at both ends. A stored range that is not one of the steps
        /// (an old save, a healed zero, an owner default off the ladder) snaps to the NEAREST step first,
        /// so the pushers always do something sensible rather than jumping to an end.
        /// </summary>
        public static float StepRange(float currentMetres, int steps)
        {
            int nearest = 0;
            double best = double.MaxValue;
            for (int i = 0; i < RangeSteps.Length; i++)
            {
                double d = System.Math.Abs(RangeSteps[i] - currentMetres);
                if (d < best) { best = d; nearest = i; }
            }
            int next = nearest + steps;
            if (next < 0) next = 0;
            if (next > RangeSteps.Length - 1) next = RangeSteps.Length - 1;
            return RangeSteps[next];
        }

        private static int Max(int a, int b) => a > b ? a : b;
        private static int Min(int a, int b) => a < b ? a : b;
    }
}
