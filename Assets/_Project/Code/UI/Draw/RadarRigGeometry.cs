using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>The radar's box-parameterised layout — <c>radarRig.js:163-183</c>. Its own type, for the
    /// reason <see cref="FishRigLayout"/> is its own: the radar shares the finder's PORTRAIT footprint
    /// exactly (480×660, so a brow carries it at the sonar's height) but subdivides its glass
    /// differently — a status strip, a square SCOPE and a data panel, where the finder has a ruler and a
    /// sonar column.</summary>
    public readonly struct RadarRigLayout
    {
        public readonly int Pad, BrandH, ColW;

        /// <summary>The whole recessed glass.</summary>
        public readonly RigRect Lcd;

        /// <summary>The side pusher column.</summary>
        public readonly RigRect Col;

        /// <summary>The wordmark strip along the bottom of the case.</summary>
        public readonly RigRect Brand;

        /// <summary>Inside the glass: the top chrome strip (range · orientation · TX · gain/sea/rain).</summary>
        public readonly RigRect Status;

        /// <summary>Inside the glass: the data panel under the scope (HDG/EBL/VRM/GAIN/…). Zero-height
        /// when the box is too short to carry it — <c>radarRig.js:425</c> only draws it when the glass
        /// has the room AND the font scale is 2 or better.</summary>
        public readonly RigRect Data;

        /// <summary>The PPI scope's centre and radius — the one circle everything polar is placed in.</summary>
        public readonly int ScopeCx, ScopeCy, ScopeR;

        /// <summary>MODE — steps standby → head-up → north-up (<see cref="RadarMode"/>).</summary>
        public readonly RigRect Button0;

        /// <summary>RANGE ▲ — a WIDER range (a higher rung).</summary>
        public readonly RigRect Button1;

        /// <summary>RANGE ▼ — a CLOSER range (a lower rung).</summary>
        public readonly RigRect Button2;

        public RadarRigLayout(int pad, int brandH, int colW, RigRect lcd, RigRect col, RigRect brand,
                              RigRect status, RigRect data, int scopeCx, int scopeCy, int scopeR,
                              RigRect b0, RigRect b1, RigRect b2)
        {
            Pad = pad; BrandH = brandH; ColW = colW;
            Lcd = lcd; Col = col; Brand = brand;
            Status = status; Data = data;
            ScopeCx = scopeCx; ScopeCy = scopeCy; ScopeR = scopeR;
            Button0 = b0; Button1 = b1; Button2 = b2;
        }

        /// <summary>The three side pushers by index (0 = MODE, 1 = RANGE ▲, 2 = RANGE ▼).</summary>
        public RigRect Button(int i) => i == 0 ? Button0 : i == 1 ? Button1 : Button2;

        /// <summary>True when a canvas point is inside the scope's disc (plus a small pad, the rig's own
        /// <c>pickAt</c> tolerance — <c>radarRig.js:159</c>).</summary>
        public bool ScopeContains(double px, double py, double padPx = 3.0)
        {
            double dx = px - ScopeCx, dy = py - ScopeCy;
            double rr = ScopeR + padPx;
            return dx * dx + dy * dy <= rr * rr;
        }
    }

    /// <summary>
    /// The radar's PURE geometry and scope maths — the immutable rig source is
    /// <c>docs/art/rigs/ui/radar/Art/radarRig.js</c> (ADR 0025 S5). Split out of the painter so the
    /// layout (which is also the hit geometry) and the polar placement are EditMode-testable without a
    /// canvas, exactly as <see cref="DepthRigGeometry"/> and <see cref="FishRigGeometry"/> are.
    ///
    /// <para><b>⚠ ONE bearing convention.</b> The rig's <c>brgTrue</c> is ALREADY the game's compass
    /// convention: <c>dir(a) = {sin a, −cos a}</c> (radarRig.js:123) puts 0° at the top of a y-DOWN
    /// canvas and 90° to the right, which is 0 = N, 90 = E, clockwise — the same convention
    /// <see cref="BoatKinematics.BearingDegrees"/> and <see cref="NavMath"/> already carry. So a world
    /// bearing computed through <see cref="NavMath.BearingDegrees"/> feeds
    /// <see cref="ScreenAngleOf"/> unchanged, and nothing here re-derives one. The single
    /// <c>atan2</c> in this file is <see cref="PickAt"/>'s, which converts a CANVAS point back into a
    /// bearing (the exact inverse of <c>dir</c>) rather than measuring the world — a different question
    /// with a different input, and the reason it is allowed to exist.</para>
    ///
    /// <para><b>⚠ The rig's RANGE_STEPS are not the game's.</b> <c>radarRig.js:129</c> ships
    /// <c>[0.5,1,2,3,6,12,24]</c> NM for a fictional ocean; the shipped ladder is
    /// <see cref="RadarSettings"/>'s re-scaled doublings, for the reason that block spells out. The rig's
    /// array is kept here only as the ART reference the golden test pins against.</para>
    /// </summary>
    public static class RadarRigGeometry
    {
        /// <summary>Standalone sprite size — <c>radarRig.js:14</c>. PORTRAIT, and deliberately the
        /// finder's exact footprint ("matches the sonar footprint exactly") so a brow carries either at
        /// the same height.</summary>
        public const int W = 480, H = 660;

        /// <summary>The rig's OWN authored range ladder in nautical miles (<c>radarRig.js:129</c>) —
        /// the art reference, NOT what the instrument uses. See the class remarks.</summary>
        public static readonly float[] RigRangeSteps = { 0.5f, 1f, 2f, 3f, 6f, 12f, 24f };

        /// <summary>The eight compass points the data panel abbreviates with (<c>radarRig.js:131</c>).</summary>
        public static readonly string[] Compass8 = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        // The standalone screen inset (radarRig.js:444) — X 6% / Y 11% / W 88% / H 78% of the canvas,
        // character-identical to the finder's. Pre-resolved to the integers the rig computes so the port
        // and the hit test share ONE box.
        public static readonly int InsetX = DrawSurface.JsRound(W * 0.06);          // 29
        public static readonly int InsetY = DrawSurface.JsRound(H * 0.11);          // 73
        public static readonly int InsetW = W - 2 * DrawSurface.JsRound(W * 0.06);  // 422
        public static readonly int InsetH = DrawSurface.JsRound(H * 0.78);          // 515

        /// <summary>The whole instrument fitted to a box — <c>radarRig.js:163-183</c>, verbatim.</summary>
        public static RadarRigLayout Layout(int x, int y, int ww, int hh)
        {
            int pad = System.Math.Max(3, DrawSurface.JsRound(ww * 0.04));
            int brandH = System.Math.Max(7, DrawSurface.JsRound(hh * 0.085));
            int colW = System.Math.Max(14, DrawSurface.JsRound(ww * 0.12));
            int bodyH = hh - pad * 2 - brandH;

            var lcd = new RigRect(x + pad, y + pad, ww - pad * 3 - colW, bodyH);
            var col = new RigRect(x + ww - pad - colW, y + pad, colW, bodyH);

            int keyH = System.Math.Max(14, System.Math.Min(
                DrawSurface.JsRound(col.W * 0.72),
                (int)System.Math.Floor((col.H - DrawSurface.JsRound(col.H * 0.16)) / 3.0)));
            int span = col.H - keyH;
            var b0 = new RigRect(col.X, DrawSurface.JsRound(col.Y + span * 0.0), col.W, keyH);
            var b1 = new RigRect(col.X, DrawSurface.JsRound(col.Y + span * 0.5), col.W, keyH);
            var b2 = new RigRect(col.X, DrawSurface.JsRound(col.Y + span * 1.0), col.W, keyH);

            var brand = new RigRect(x + pad, y + hh - brandH - DrawSurface.JsRound(pad * 0.3),
                                    ww - pad * 2, brandH);

            // Inside the LCD glass.
            int stripH = System.Math.Max(9, DrawSurface.JsRound(lcd.W * 0.075));
            int scopeSide = System.Math.Min(lcd.W, lcd.H - stripH);
            int sr = (int)System.Math.Floor(scopeSide / 2.0) - 2;
            var status = new RigRect(lcd.X, lcd.Y, lcd.W, stripH);
            int scopeCx = DrawSurface.JsRound(lcd.X + lcd.W / 2.0);
            int scopeCy = DrawSurface.JsRound(lcd.Y + stripH + scopeSide / 2.0);
            int dataTop = lcd.Y + stripH + scopeSide;
            var data = new RigRect(lcd.X, dataTop, lcd.W, System.Math.Max(0, lcd.Y + lcd.H - dataTop));

            return new RadarRigLayout(pad, brandH, colW, lcd, col, brand, status, data,
                                      scopeCx, scopeCy, sr, b0, b1, b2);
        }

        /// <summary>The standalone instrument box inside the 480×660 sprite — <c>radarRig.js:444</c>.</summary>
        public static RadarRigLayout StandaloneLayout() => Layout(InsetX, InsetY, InsetW, InsetH);

        /// <summary>Font scale for a box height — <c>radarRig.js:403</c>. Below 112 px the glass drops to
        /// the 1-px font and the rig itself stops drawing the bearing numbers, the data panel and the
        /// clutter bars.</summary>
        public static int FontScale(int hh) => hh >= 200 ? 3 : hh >= 112 ? 2 : 1;

        /// <summary>
        /// A TRUE bearing turned into the angle it is DRAWN at — <c>radarRig.js:149</c>. North-up draws
        /// the bearing itself; head-up subtracts own heading so the bow is always at the top.
        /// </summary>
        public static float ScreenAngleOf(float bearingTrue, bool headUp, float headingDeg)
            => headUp ? bearingTrue - headingDeg : bearingTrue;

        /// <summary>
        /// Where a contact lands on the scope — <c>radarRig.js:150-154</c>. <paramref name="rangeFrac"/>
        /// is the contact's distance as a fraction of the scope's range; anything beyond 1 is off the
        /// glass and <paramref name="visible"/> comes back false.
        ///
        /// <para>The rig's own <c>1.001</c> tolerance is kept: a contact sitting exactly ON the selected
        /// range is drawn at the rim rather than blinking out, which is what stops a target flickering
        /// at the edge as the boat rolls.</para>
        /// </summary>
        public static void BlipXY(int scopeCx, int scopeCy, int scopeR,
                                  float bearingTrue, float rangeFrac, bool headUp, float headingDeg,
                                  out double px, out double py, out float screenAngle, out bool visible)
        {
            screenAngle = ScreenAngleOf(bearingTrue, headUp, headingDeg);
            RigDrawUtil.Dir(screenAngle * RigDrawUtil.DEG, out double dx, out double dy);
            px = scopeCx + dx * rangeFrac * scopeR;
            py = scopeCy + dy * rangeFrac * scopeR;
            visible = rangeFrac <= 1.001f;
        }

        /// <summary>The half-extent a blip is drawn at — <c>radarRig.js:153</c>. Floored at 3 px so the
        /// smallest return is still a mark rather than a single pixel on a big scope.</summary>
        public static double BlipHalf(int scopeR, float size)
            => System.Math.Max(3.0, scopeR * 0.05) * (size <= 0f ? 1f : size);

        /// <summary>
        /// A canvas point turned back into a bearing and a range fraction — <c>radarRig.js:155-160</c>,
        /// the EBL/VRM cursor's read.
        ///
        /// <para>This is the one <c>atan2</c> in the port and it is the inverse of <c>dir</c>, not a
        /// second world-bearing convention: it takes CANVAS pixels (y down) and returns the true bearing
        /// they represent on the scope, which is exactly what the drawing put there.</para>
        /// </summary>
        public static void PickAt(int scopeCx, int scopeCy, int scopeR, double px, double py,
                                  bool headUp, float headingDeg,
                                  out float bearingTrue, out float rangeFrac, out bool inside)
        {
            double dx = px - scopeCx, dy = py - scopeCy;
            double rr = System.Math.Sqrt(dx * dx + dy * dy);
            double frac = scopeR > 0 ? rr / scopeR : 0.0;
            if (frac < 0.0) frac = 0.0; else if (frac > 1.0) frac = 1.0;
            rangeFrac = (float)frac;

            float screenAngle = NavMath.Norm360((float)(System.Math.Atan2(dx, -dy) / RigDrawUtil.DEG));
            // Undo whatever ScreenAngleOf applied: head-up drew (true − heading), so add it back.
            bearingTrue = NavMath.Norm360(headUp ? screenAngle + headingDeg : screenAngle);
            inside = rr <= scopeR + 3;
        }

        /// <summary>Is a contact inside the guard zone? — <c>radarRig.js:362-363</c>. The sector is
        /// [<paramref name="fromDeg"/>, <paramref name="toDeg"/>] clockwise (wrapping through north is
        /// legal) and [<paramref name="near01"/>, <paramref name="far01"/>] of the scope's range.</summary>
        public static bool InGuard(float bearingTrue, float rangeFrac,
                                   float fromDeg, float toDeg, float near01, float far01)
        {
            if (rangeFrac < near01 || rangeFrac > far01) return false;
            float a = NavMath.Norm360(bearingTrue);
            float a0 = NavMath.Norm360(fromDeg), a1 = NavMath.Norm360(toDeg);
            if (a1 < a0) a1 += 360f;
            if (a < a0) a += 360f;
            return a >= a0 && a <= a1;
        }

        /// <summary>Which of the four echo-strength colours a size falls in — <c>radarRig.js:229</c>.</summary>
        public static int StrengthIndex(float size)
            => size < 0.8f ? 0 : size < 1.3f ? 1 : size < 1.9f ? 2 : 3;

        /// <summary>
        /// How bright a return is, given where the sweep has just been — <c>radarRig.js:230</c>. A
        /// contact the sweep has only just passed is at full brightness and everything behind it decays
        /// through the phosphor's persistence.
        ///
        /// <para>A set in STANDBY returns a flat 0.5: the aerial is not turning, so nothing is being
        /// refreshed and every echo sits at the same half-lit level. That is the rig's own answer and it
        /// is the honest one — a standby picture is the last one painted, not a live one.</para>
        /// </summary>
        public static float SweepBrightness(float screenAngle, float sweepDeg, bool transmitting)
        {
            if (!transmitting) return 0.5f;
            float d = NavMath.Norm360(sweepDeg - screenAngle);
            float v = 1f - (d / 300f) * 0.62f;
            return v < 0.38f ? 0.38f : (v > 1f ? 1f : v);
        }

        /// <summary>The rig's own deterministic hash for its stipple clouds — <c>radarRig.js:126</c>.
        /// Transcribed rather than replaced by <c>Random</c>: the speckle must be the SAME every repaint
        /// or the sea clutter would crawl, and it must be the same in a test as on screen.</summary>
        public static double HashRnd(double n)
        {
            double x = System.Math.Sin(n * 127.1 + 13.7) * 43758.5453;
            return x - System.Math.Floor(x);
        }

        /// <summary>
        /// Range in NM: whole miles at ten and above and one decimal from a mile up (<c>radarRig.js:132</c>,
        /// unchanged), but TWO DECIMALS below a mile — the chartplotter's own
        /// <see cref="NavRigGeometry.FmtNM"/> rule rather than the rig's.
        ///
        /// <para><b>⚠ The rig's EIGHTHS are not transcribable onto this ladder, and keeping them would
        /// make the scope lie.</b> <c>radarRig.js:132</c> prints sub-mile ranges as eighths ("3/8"),
        /// which is exact for its own authored ladder — <c>[0.5,1,2,3,6,12,24]</c>, every rung a whole
        /// number of eighths. The shipped ladder is re-scaled for a few-hundred-metre region
        /// (<see cref="RadarSettings"/>), and its rungs are not: 0.2 NM would print <b>"2/8"</b> (0.25 —
        /// wrong by a quarter) and the closest rung, 0.05 NM, would print <b>"0/8"</b>. The chartplotter
        /// hit exactly this when its ladder was re-scaled and answered it the same way, with the note
        /// "the sub-1 branch is what makes the re-scaled harbour ladder readable". Matching it also means
        /// the two instruments sitting side by side on one brow read the same, which is worth more than
        /// a formatting flourish for a ladder the game does not have.</para>
        /// </summary>
        public static string FormatNM(float nm)
        {
            nm = System.Math.Abs(nm);
            if (nm >= 10f) return DrawSurface.JsRound(nm).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (nm >= 1f) return nm.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            return nm.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>A bearing as three zero-padded degrees — <c>radarRig.js:135</c>.</summary>
        public static string FormatDeg(float deg)
        {
            int d = DrawSurface.JsRound(NavMath.Norm360(deg));
            if (d >= 360) d -= 360;                 // 359.7° rounds to 360; a compass has no 360
            return d.ToString("000", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>The nearest of eight compass points — <c>radarRig.js:134</c>.</summary>
        public static string Cardinal8(float deg)
            => Compass8[DrawSurface.JsRound(NavMath.Norm360(deg) / 45f) % 8];
    }
}
