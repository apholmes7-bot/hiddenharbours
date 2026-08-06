using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>The LCD's cell layout — <c>watchRig.js:145-186</c> (<c>lcd</c>), parameterised by the
    /// glass box exactly as the source is, so the painter and any future hit test share ONE set of
    /// boxes. All cells are integer rig pixels; the 7-segment bars overhang each cell by half a
    /// stroke, which is the painter's business, not the layout's.</summary>
    public readonly struct WatchLcdLayout
    {
        /// <summary>The four HH:MM cells, left to right (watchRig.js:171-179).</summary>
        public readonly RigRect Hour0, Hour1, Minute0, Minute1;
        /// <summary>The two date cells, left to right (watchRig.js:160).</summary>
        public readonly RigRect Date0, Date1;
        /// <summary>The two seconds cells, left to right (watchRig.js:185).</summary>
        public readonly RigRect Second0, Second1;
        /// <summary>The colon's two 6×6 pips (watchRig.js:174-175).</summary>
        public readonly RigRect ColonTop, ColonBottom;
        /// <summary>The indicator strip's row (watchRig.js:153) and the season/seconds row
        /// (watchRig.js:182) — carried here so the painter and the cells share ONE copy of each.</summary>
        public readonly int TopRowY, BottomRowY;

        public WatchLcdLayout(RigRect h0, RigRect h1, RigRect m0, RigRect m1,
                              RigRect d0, RigRect d1, RigRect s0, RigRect s1,
                              RigRect colonTop, RigRect colonBottom, int topRowY, int bottomRowY)
        {
            Hour0 = h0; Hour1 = h1; Minute0 = m0; Minute1 = m1;
            Date0 = d0; Date1 = d1; Second0 = s0; Second1 = s1;
            ColonTop = colonTop; ColonBottom = colonBottom;
            TopRowY = topRowY; BottomRowY = bottomRowY;
        }

        /// <summary>The four main HH:MM cells by index (0-1 = hours, 2-3 = minutes).</summary>
        public RigRect Main(int i) => i == 0 ? Hour0 : i == 1 ? Hour1 : i == 2 ? Minute0 : Minute1;
    }

    /// <summary>
    /// The watch face's PURE geometry + LCD string formatting — the immutable rig source is
    /// <c>docs/art/rigs/ui/watch-face/Art/watchRig.js</c> (ADR 0025, Option A: a live C# renderer, NOT
    /// a bake — the face's state space is the whole calendar, so there is nothing finite to bake).
    /// Split out of <see cref="WatchRigRender"/> so the cell layout and the formatters are
    /// EditMode-testable without a canvas, exactly as <see cref="DepthRigGeometry"/> is for the sounder.
    ///
    /// <para><b>The clock is NOT read here.</b> <see cref="WatchFaceState.FromClock"/> in Core is the one
    /// mapper from <see cref="IGameClock"/> to the rig's parameter bag, and it already carries the canon
    /// (4×28-day seasons, Mon-first week, market day, the 06:00/19:00 night rule). This class only
    /// formats what that state already decided — there is deliberately no second time read.</para>
    ///
    /// <para><b>The season tag reads FAL, by owner ruling</b> (2026-08-06, relayed by the coordinator on
    /// PR #447: "fall instead of turn please"). The 2026-08-06 drop's <c>SEASON_ABBR</c> table is
    /// <c>SPR/SUM/FAL/WIN</c>, and the port follows it: <see cref="Season.TheTurn"/> prints <b>FAL</b> on
    /// the glass. This was raised as a <c>_confirm</c> item — the enum and the canon call that season "The
    /// Turn" (<c>docs/vision-and-pillars.md</c> §5.8) — and the owner ruled for the rig's word.</para>
    ///
    /// <para><b>The ruling is scoped to this display.</b> It renames nothing: <see cref="Season.TheTurn"/>
    /// keeps its name everywhere else, and <c>HudStrings.Season</c> still says "The Turn" in prose. This
    /// method is the ONLY place the watch's four-season vocabulary is decided, so if the broader naming
    /// ever follows, it is a separate owner call and does not start here.</para>
    /// </summary>
    public static class WatchRigGeometry
    {
        /// <summary>Standalone sprite size — watchRig.js:13.</summary>
        public const int W = 340, H = 356;

        // ---- the LCD glass, in canvas pixels (watchRig.js:228) -------------------------------------
        public const int LcdX = 58, LcdY = 116, LcdW = 224, LcdH = 148;

        /// <summary>The recessed glass the LCD content is drawn into.</summary>
        public static RigRect Glass() => new RigRect(LcdX, LcdY, LcdW, LcdH);

        // ---- pusher hit boxes (watchRig.js:245) ----------------------------------------------------
        // PRESENT BUT UNWIRED: the HUD is read-only (every HUD graphic sets raycastTarget = false), so
        // nothing clicks these today. They are transcribed and pinned so that if the owner later wants
        // the LIGHT pusher live, the boxes are already the rig's own and cannot drift from the legends.

        /// <summary>The LIGHT pusher's legend box — momentary backlight (watchRig.js:245).</summary>
        public static RigRect HitLight() => new RigRect(218, 270, 66, 20);

        /// <summary>The MODE pusher's legend box (watchRig.js:245).</summary>
        public static RigRect HitMode() => new RigRect(62, 270, 56, 20);

        // ---- LCD cell layout (watchRig.js:145-186) -------------------------------------------------

        /// <summary>Main HH:MM cell size and spacing — watchRig.js:164-169.</summary>
        private const int MainY = 40, MainH = 60, MainW = 32, MainGap = 10, ColonPad = 13, ColonW = 8;
        /// <summary>The colon pip: a 6×6 square at 30% and 62% of the digit height
        /// (watchRig.js:174-175).</summary>
        private const int ColonPip = 6;
        /// <summary>Date cells — watchRig.js:160,
        /// <c>seg7Str(date, dRight, topY-1, 13, 15, 4, …)</c>.</summary>
        private const int DateW = 13, DateH = 15, DateGap = 4;
        /// <summary>Seconds cells — watchRig.js:185,
        /// <c>seg7Str(ss, X+WW-8, botY, 17, 22, 5, …)</c>.</summary>
        private const int SecW = 17, SecH = 22, SecGap = 5;
        /// <summary>The LCD's own inner margins — watchRig.js:153/159/182.</summary>
        private const int TopY = 8, RightMargin = 8, BottomUp = 30;

        /// <summary>
        /// Lay the LCD out inside a glass box — the same box-parameterised form <c>lcd()</c> has, so the
        /// standalone face and any future flush mount agree about where a digit is.
        /// </summary>
        public static WatchLcdLayout Layout(int x, int y, int ww, int hh)
        {
            // --- date, right-aligned off the glass's right margin (watchRig.js:159-160). seg7Str walks
            // RIGHT to LEFT from dRight, so cell 1 is placed before cell 0.
            int topY = y + TopY;
            int dRight = x + ww - RightMargin;
            int date1X = dRight - DateW;
            int date0X = date1X - DateGap - DateW;
            int dateY = topY - 1;
            var d0 = new RigRect(date0X, dateY, DateW, DateH);
            var d1 = new RigRect(date1X, dateY, DateW, DateH);

            // --- main HH:MM, centred as one group (watchRig.js:169-179).
            int mainY = y + MainY;
            int groupW = MainW * 4 + MainGap * 2 + ColonPad * 2 + ColonW;
            int gx = x + DrawSurface.JsRound((ww - groupW) / 2.0);

            var h0 = new RigRect(gx, mainY, MainW, MainH);
            gx += MainW + MainGap;
            var h1 = new RigRect(gx, mainY, MainW, MainH);
            gx += MainW + ColonPad;

            // The colon's pips. The source passes FRACTIONAL y here (dh*0.62 = 37.2 on a 60-tall digit),
            // which canvas would anti-alias; under the rigs' own no-AA rule the port rounds to the pixel
            // grid instead (DrawSurface's documented deviation, same as the sounder's gradient).
            int colonX = gx + DrawSurface.JsRound((ColonW - ColonPip) / 2.0);
            var cTop = new RigRect(colonX, DrawSurface.JsRound(mainY + MainH * 0.30), ColonPip, ColonPip);
            var cBot = new RigRect(colonX, DrawSurface.JsRound(mainY + MainH * 0.62), ColonPip, ColonPip);
            gx += ColonW + ColonPad;

            var m0 = new RigRect(gx, mainY, MainW, MainH);
            gx += MainW + MainGap;
            var m1 = new RigRect(gx, mainY, MainW, MainH);

            // --- seconds, right-aligned on the bottom row (watchRig.js:182/185). Right-to-left again.
            int botY = y + hh - BottomUp;
            int sec1X = x + ww - RightMargin - SecW;
            int sec0X = sec1X - SecGap - SecW;
            var s0 = new RigRect(sec0X, botY, SecW, SecH);
            var s1 = new RigRect(sec1X, botY, SecW, SecH);

            return new WatchLcdLayout(h0, h1, m0, m1, d0, d1, s0, s1, cTop, cBot, topY, botY);
        }

        /// <summary>The standalone face's LCD layout — <see cref="Layout"/> over <see cref="Glass"/>.</summary>
        public static WatchLcdLayout StandaloneLayout()
            => Layout(LcdX, LcdY, LcdW, LcdH);

        // ---- LCD string formatting (watchRig.js:141-150) --------------------------------------------

        /// <summary>
        /// watchRig.js:142 — <c>pad2</c>. Two digits, zero-padded. The source's own negative branch
        /// (<c>'0' + -5</c> → "0-5") is deliberately NOT ported: every caller here is an hour, minute,
        /// second or day-of-season, none of which can be negative, and reproducing it would only hide a
        /// bad value behind a plausible-looking cell.
        /// </summary>
        public static string Pad2(int n)
            => n < 10 && n >= 0 ? "0" + Digit(n) : n.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static string Digit(int n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// The hour cell's two characters — watchRig.js:147-149. In 24-hour mode this is
        /// <c>pad2(h)</c>; in 12-hour mode the hour is 1..12 and a single digit is padded with a
        /// SPACE, not a zero (the rig blanks that cell entirely rather than lighting a leading 0 —
        /// <c>seg7</c>'s null-colour branch, watchRig.js:171).
        /// </summary>
        public static string HourText(int hour24, bool use24)
        {
            if (use24) return Pad2(hour24);
            int h12 = hour24 % 12;
            if (h12 == 0) h12 = 12;
            return h12 < 10 ? " " + Digit(h12) : Digit(h12);
        }

        /// <summary>watchRig.js:149 — the AM/PM tag shown only in 12-hour mode.</summary>
        public static string AmPm(int hour24) => hour24 < 12 ? "AM" : "PM";

        /// <summary>
        /// The season tag on the glass — the rig's own <c>SEASON_ABBR</c> table (watchRig.js:139), which
        /// the owner ruled for on 2026-08-06: <see cref="Season.TheTurn"/> shows <b>FAL</b> here. See the
        /// class remarks — the ruling is about this readout only and renames nothing.
        /// </summary>
        public static string SeasonAbbr(Season season) => season switch
        {
            Season.EarlySpring => "SPR",
            Season.HighSummer  => "SUM",
            Season.TheTurn     => "FAL",
            Season.HardWinter  => "WIN",
            _                  => "---",
        };

        /// <summary>The weekday tag — watchRig.js:138 <c>WEEKDAYS</c>, Mon-first, which is exactly the
        /// <see cref="Weekday"/> enum's own order.</summary>
        public static string WeekdayAbbr(Weekday weekday) => weekday switch
        {
            Weekday.Monday    => "MO",
            Weekday.Tuesday   => "TU",
            Weekday.Wednesday => "WE",
            Weekday.Thursday  => "TH",
            Weekday.Friday    => "FR",
            Weekday.Saturday  => "SA",
            Weekday.Sunday    => "SU",
            _                 => "--",
        };

        /// <summary>The year tag — watchRig.js:184 prints <c>'Y' + year</c>.</summary>
        public static string YearText(int year)
            => "Y" + year.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>The top-left indicator — watchRig.js:155 flags a market day, else the alarm tag.</summary>
        public static string MarketTag(bool isMarketDay) => isMarketDay ? "MKT" : "AL";
    }
}
