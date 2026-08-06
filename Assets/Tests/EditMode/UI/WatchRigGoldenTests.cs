using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The C# watch-face port's DRIFT GUARD (ADR 0025 Option A, the same honest form
    /// <see cref="DepthRigGoldenTests"/> takes: no editor Canvas2D shim exists yet, so this pins the
    /// numeric geometry, the canvas dims, the hit boxes and the LCD formatters rather than a pixel
    /// golden-master).
    ///
    /// <para><b>The goldens were transcribed by RUNNING the rig source</b>
    /// (<c>docs/art/rigs/ui/watch-face/Art/watchRig.js</c>) under a recording Canvas2D in node, and
    /// reading the cell rectangles out of the seg-bar paths it actually emitted — never by running the
    /// C# under test, and never by re-deriving its formulas by hand. So a bug in the port cannot leak
    /// into its own expected values.</para>
    ///
    /// <para><b>The season tag is an OWNER RULING, pinned on purpose.</b> The face shows the rig's own
    /// table, so <see cref="Season.TheTurn"/> reads <c>FAL</c> — ruled 2026-08-06 on PR #447 after it was
    /// raised as a <c>_confirm</c> against the canon name. Two tests hold it: one pins the ruled display,
    /// the other pins that the ruling stopped at the glass and renamed nothing. Neither is a cleanup
    /// target; changing either needs the owner.</para>
    /// </summary>
    public class WatchRigGoldenTests
    {
        private static void AssertRect(int x, int y, int w, int h, RigRect r, string what)
        {
            Assert.AreEqual(x, r.X, what + ".X");
            Assert.AreEqual(y, r.Y, what + ".Y");
            Assert.AreEqual(w, r.W, what + ".W");
            Assert.AreEqual(h, r.H, what + ".H");
        }

        // ---- canvas + chrome geometry (watchRig.js:13, 228, 245) --------------------------------

        [Test]
        public void CanvasDimensions_MatchTheRigSource()
        {
            Assert.AreEqual(340, WatchRigGeometry.W);
            Assert.AreEqual(356, WatchRigGeometry.H);
            Assert.AreEqual(WatchRigGeometry.W, WatchRigRender.W);
            Assert.AreEqual(WatchRigGeometry.H, WatchRigRender.H);
        }

        [Test]
        public void Glass_IsTheRigsRecessedLcdWindow()
            => AssertRect(58, 116, 224, 148, WatchRigGeometry.Glass(), "Glass");

        /// <summary>The pusher legend boxes MOVED in the 2026-08-06 drop (they were 96×26 at y 260,
        /// overlapping the bezel screws). Nothing clicks them yet — the HUD is read-only — but they are
        /// pinned so a future wiring cannot drift from where the legends are drawn.</summary>
        [Test]
        public void HitBoxes_MatchTheDropsRelocatedLegends()
        {
            AssertRect(218, 270, 66, 20, WatchRigGeometry.HitLight(), "HitLight");
            AssertRect(62, 270, 56, 20, WatchRigGeometry.HitMode(), "HitMode");
        }

        // ---- LCD cell layout (watchRig.js:145-186) ----------------------------------------------

        [Test]
        public void LcdCells_MatchTheRigsOwnSegBarPositions()
        {
            WatchLcdLayout L = WatchRigGeometry.StandaloneLayout();

            // main HH:MM — dw 32, dh 60, gaps 10/13, colon 8 wide, group centred in the 224-wide glass
            AssertRect(79, 156, 32, 60, L.Hour0, "Hour0");
            AssertRect(121, 156, 32, 60, L.Hour1, "Hour1");
            AssertRect(187, 156, 32, 60, L.Minute0, "Minute0");
            AssertRect(229, 156, 32, 60, L.Minute1, "Minute1");

            // date — 13×15 cells, right-aligned 8px off the glass edge, gap 4
            AssertRect(244, 123, 13, 15, L.Date0, "Date0");
            AssertRect(261, 123, 13, 15, L.Date1, "Date1");

            // seconds — 17×22 cells, right-aligned, gap 5
            AssertRect(235, 234, 17, 22, L.Second0, "Second0");
            AssertRect(257, 234, 17, 22, L.Second1, "Second1");

            // colon pips. The rig places the LOWER one at a fractional y (156 + 60·0.62 = 193.2), which
            // canvas anti-aliases; the port lands it on the pixel grid at 193 (the documented no-AA
            // deviation). The upper one is exact at 174.
            AssertRect(167, 174, 6, 6, L.ColonTop, "ColonTop");
            AssertRect(167, 193, 6, 6, L.ColonBottom, "ColonBottom");

            Assert.AreEqual(124, L.TopRowY, "TopRowY");
            Assert.AreEqual(234, L.BottomRowY, "BottomRowY");
        }

        /// <summary>The main group is CENTRED in the glass — the invariant behind the cell numbers, so a
        /// drifting digit width or gap fails here even if someone updated the cells to match it.</summary>
        [Test]
        public void MainGroup_IsCentredInTheGlass()
        {
            RigRect g = WatchRigGeometry.Glass();
            WatchLcdLayout L = WatchRigGeometry.StandaloneLayout();
            int leftMargin = L.Hour0.X - g.X;
            int rightMargin = (g.X + g.W) - (L.Minute1.X + L.Minute1.W);
            Assert.AreEqual(21, leftMargin, "left margin");
            Assert.AreEqual(leftMargin, rightMargin, "the HH:MM group must sit centred in the glass");
        }

        /// <summary>Negative control: the layout is genuinely parameterised by the glass box, so moving
        /// the box moves every cell with it. A hard-coded table would pass the goldens above and fail
        /// this.</summary>
        [Test]
        public void Layout_FollowsItsGlassBox()
        {
            WatchLcdLayout a = WatchRigGeometry.StandaloneLayout();
            WatchLcdLayout b = WatchRigGeometry.Layout(WatchRigGeometry.LcdX + 10, WatchRigGeometry.LcdY + 7,
                                                       WatchRigGeometry.LcdW, WatchRigGeometry.LcdH);
            Assert.AreEqual(a.Hour0.X + 10, b.Hour0.X);
            Assert.AreEqual(a.Hour0.Y + 7, b.Hour0.Y);
            Assert.AreEqual(a.Second1.X + 10, b.Second1.X);
            Assert.AreEqual(a.BottomRowY + 7, b.BottomRowY);
        }

        // ---- LCD string formatting (watchRig.js:141-150) -----------------------------------------

        // hour → 24-hour cell · 12-hour cell · AM/PM
        private static readonly object[][] Hours =
        {
            new object[] {  0, "00", "12", "AM" },
            new object[] {  1, "01", " 1", "AM" },   // leading SPACE, not a zero — the blanked cell
            new object[] {  6, "06", " 6", "AM" },
            new object[] {  9, "09", " 9", "AM" },
            new object[] { 10, "10", "10", "AM" },
            new object[] { 11, "11", "11", "AM" },
            new object[] { 12, "12", "12", "PM" },   // noon is 12 PM, not 0 PM
            new object[] { 13, "13", " 1", "PM" },
            new object[] { 18, "18", " 6", "PM" },
            new object[] { 23, "23", "11", "PM" },
        };

        [Test]
        public void HourCell_MatchesTheRigOverTheWholeDay()
        {
            foreach (object[] row in Hours)
            {
                int h = (int)row[0];
                Assert.AreEqual((string)row[1], WatchRigGeometry.HourText(h, use24: true), "24h at " + h);
                Assert.AreEqual((string)row[2], WatchRigGeometry.HourText(h, use24: false), "12h at " + h);
                Assert.AreEqual((string)row[3], WatchRigGeometry.AmPm(h), "AM/PM at " + h);
            }
        }

        [Test]
        public void HourCell_IsAlwaysTwoCharacters()
        {
            for (int h = 0; h < 24; h++)
            {
                Assert.AreEqual(2, WatchRigGeometry.HourText(h, true).Length, "24h at " + h);
                Assert.AreEqual(2, WatchRigGeometry.HourText(h, false).Length, "12h at " + h);
            }
        }

        [Test]
        public void Pad2_MatchesTheRig()
        {
            Assert.AreEqual("00", WatchRigGeometry.Pad2(0));
            Assert.AreEqual("01", WatchRigGeometry.Pad2(1));
            Assert.AreEqual("09", WatchRigGeometry.Pad2(9));
            Assert.AreEqual("10", WatchRigGeometry.Pad2(10));
            Assert.AreEqual("28", WatchRigGeometry.Pad2(28));   // the last day of a season
            Assert.AreEqual("59", WatchRigGeometry.Pad2(59));
        }

        /// <summary>
        /// THE RULED DISPLAY. The watch shows the rig's own <c>SEASON_ABBR</c> table — so
        /// <see cref="Season.TheTurn"/> reads <b>FAL</b> on the glass, by the owner's 2026-08-06 ruling
        /// ("fall instead of turn please", relayed on PR #447). It was raised as a <c>_confirm</c> because
        /// the canon calls that season "The Turn"; the owner ruled for the rig's word, and this is where
        /// that is written down. Changing it back is an owner call, not a cleanup.
        /// </summary>
        [Test]
        public void SeasonTag_ShowsTheRigsTable_PerTheOwnersRuling()
        {
            Assert.AreEqual("SPR", WatchRigGeometry.SeasonAbbr(Season.EarlySpring));
            Assert.AreEqual("SUM", WatchRigGeometry.SeasonAbbr(Season.HighSummer));
            Assert.AreEqual("FAL", WatchRigGeometry.SeasonAbbr(Season.TheTurn));
            Assert.AreEqual("WIN", WatchRigGeometry.SeasonAbbr(Season.HardWinter));
        }

        /// <summary>
        /// The ruling was scoped to the WATCH, and this pins that scope: the enum's own name is untouched
        /// and the HUD's prose season word still reads "The Turn". If someone later renames the season
        /// across the codebase, this is the test that should make them come back and get that ruling.
        /// </summary>
        [Test]
        public void TheRuling_IsTheWatchDisplayOnly_NotARenameOfTheSeason()
        {
            Assert.AreEqual("TheTurn", Season.TheTurn.ToString(),
                            "the enum keeps its canon name — the ruling was about the watch's glass");
            Assert.AreEqual("The Turn", HudStrings.Season(Season.TheTurn),
                            "the HUD's prose season word is unchanged by the watch ruling");
        }

        /// <summary>The rig's <c>WEEKDAYS</c> table is Mon-first, which is exactly the
        /// <see cref="Weekday"/> enum's order — so index and enum agree without a lookup table.</summary>
        [Test]
        public void WeekdayTag_MatchesTheRigsMonFirstTable()
        {
            string[] rig = { "MO", "TU", "WE", "TH", "FR", "SA", "SU" };
            for (int i = 0; i < rig.Length; i++)
                Assert.AreEqual(rig[i], WatchRigGeometry.WeekdayAbbr((Weekday)i), "weekday " + i);
        }

        [Test]
        public void YearAndMarketTags_MatchTheRig()
        {
            Assert.AreEqual("Y1", WatchRigGeometry.YearText(1));
            Assert.AreEqual("Y12", WatchRigGeometry.YearText(12));
            Assert.AreEqual("MKT", WatchRigGeometry.MarketTag(true));
            Assert.AreEqual("AL", WatchRigGeometry.MarketTag(false));
        }

        // ---- the renderer itself -----------------------------------------------------------------

        private static readonly Color32 DayLitSegment = new Color32(0x1c, 0x21, 0x19, 255);  // watchRig.js:21

        /// <summary>A face at a given hour. Night is derived with the SAME rule the live mapper uses
        /// (<see cref="WatchFaceState.NightAt"/>), so an hour past dusk really does produce a night
        /// face here rather than a day one with a night label.</summary>
        private static WatchRigState State(int hour, bool use24, bool seconds = false, bool light = false)
            => new WatchRigState(
                new WatchFaceState(hour, 47, 29, Weekday.Thursday, 6, Season.TheTurn, 1,
                                   isMarketDay: false, isNight: WatchFaceState.NightAt(hour)),
                use24, light, seconds);

        private static DrawSurface RenderFace(in WatchRigState s)
        {
            var surface = new DrawSurface(WatchRigRender.W, WatchRigRender.H);
            WatchRigRender.Render(surface, in s);
            return surface;
        }

        /// <summary>Count pixels in a box that are exactly the LIT day segment colour. The off-ghosts
        /// are a 7% wash over a pale panel, so they can never collide with this.</summary>
        private static int LitPixels(DrawSurface s, int x0, int y0, int x1, int y1)
        {
            int n = 0;
            for (int y = y0; y < y1; y++)
            {
                if (y < 0 || y >= s.Height) continue;
                for (int x = x0; x < x1; x++)
                {
                    if (x < 0 || x >= s.Width) continue;
                    Color32 p = s.Pixels[y * s.Width + x];
                    if (p.r == DayLitSegment.r && p.g == DayLitSegment.g && p.b == DayLitSegment.b) n++;
                }
            }
            return n;
        }

        /// <summary>A cell's box, widened by a few pixels because the 7-segment bars overhang their
        /// cell by half a stroke.</summary>
        private static int LitPixels(DrawSurface s, in RigRect cell)
            => LitPixels(s, cell.X - 4, cell.Y - 4, cell.X + cell.W + 4, cell.Y + cell.H + 4);

        /// <summary>
        /// The leading hour cell, probed from x+10 rightward. The 12-hour face prints its AM/PM tag in
        /// the LIT segment colour at the glass's left gutter (x 64–84), which reaches INTO this cell's
        /// widened box — so a naive box would count the "M" of "AM" as digit ink and the blank-cell
        /// test below would be measuring the wrong thing. Everything from x+10 on is the digit alone.
        /// </summary>
        private static int LitPixelsClearOfTheAmPmGutter(DrawSurface s, in RigRect cell)
            => LitPixels(s, cell.X + 10, cell.Y - 4, cell.X + cell.W + 4, cell.Y + cell.H + 4);

        /// <summary>
        /// THE SUBTLEST LINE IN THE PORT. In 12-hour mode an hour of 1–9 puts a SPACE in the leading
        /// cell, and the rig passes a <c>null</c> colour for it — canvas ignores a null <c>fillStyle</c>,
        /// so that cell draws nothing at all, not even the off-ghosts every other cell shows. Porting
        /// that as "draw a blank digit" would ghost seven faint segments where the rig shows bare glass.
        /// </summary>
        [Test]
        public void TwelveHourFace_LeavesTheLeadingCellCompletelyUndrawn()
        {
            WatchLcdLayout L = WatchRigGeometry.StandaloneLayout();

            DrawSurface twelve = RenderFace(State(6, use24: false));   // " 6" — leading cell blank
            DrawSurface twentyFour = RenderFace(State(6, use24: true)); // "06" — leading cell lights a 0

            Assert.AreEqual(0, LitPixelsClearOfTheAmPmGutter(twelve, L.Hour0),
                            "a blanked leading hour cell must draw NOTHING");
            Assert.Greater(LitPixelsClearOfTheAmPmGutter(twentyFour, L.Hour0), 0,
                           "the 24-hour face's leading cell shows a lit '0'");

            // …and the cell beside it is unaffected in both — the blank is one cell, not the pair.
            Assert.Greater(LitPixels(twelve, L.Hour1), 0);
            Assert.Greater(LitPixels(twentyFour, L.Hour1), 0);
        }

        /// <summary>Every cell the face claims to show actually gets ink where the layout says it is —
        /// the join between <see cref="WatchRigGeometry"/> and <see cref="WatchRigRender"/>.</summary>
        [Test]
        public void Face_DrawsLitSegmentsInEveryCellItClaims()
        {
            WatchLcdLayout L = WatchRigGeometry.StandaloneLayout();
            DrawSurface s = RenderFace(State(13, use24: true, seconds: true));

            Assert.Greater(LitPixels(s, L.Hour0), 0, "Hour0");
            Assert.Greater(LitPixels(s, L.Hour1), 0, "Hour1");
            Assert.Greater(LitPixels(s, L.Minute0), 0, "Minute0");
            Assert.Greater(LitPixels(s, L.Minute1), 0, "Minute1");
            Assert.Greater(LitPixels(s, L.Date0), 0, "Date0");
            Assert.Greater(LitPixels(s, L.Date1), 0, "Date1");
            Assert.Greater(LitPixels(s, L.Second0), 0, "Second0");
            Assert.Greater(LitPixels(s, L.Second1), 0, "Second1");
        }

        /// <summary>With the seconds field off, its cells carry no LIT segment — the dark-field state
        /// the HUD ships in (a live seconds cell would repaint the face ~48×/second, rule 7).</summary>
        [Test]
        public void SecondsOff_LeavesTheSecondsCellsUnlit()
        {
            WatchLcdLayout L = WatchRigGeometry.StandaloneLayout();
            DrawSurface s = RenderFace(State(13, use24: true, seconds: false));

            Assert.AreEqual(0, LitPixels(s, L.Second0), "Second0 must be dark when seconds are off");
            Assert.AreEqual(0, LitPixels(s, L.Second1), "Second1 must be dark when seconds are off");
            Assert.Greater(LitPixels(s, L.Minute1), 0, "…while the minutes still read");
        }

        /// <summary>The night face is the GREEN backlight, so none of its segments can be the day
        /// palette's ink. Also covers <see cref="WatchRigState.Backlit"/>'s <c>night || light</c>.</summary>
        [Test]
        public void NightFace_UsesTheBacklightPalette_NotTheDayInk()
        {
            WatchLcdLayout L = WatchRigGeometry.StandaloneLayout();
            DrawSurface night = RenderFace(State(21, use24: true));

            Assert.AreEqual(0, LitPixels(night, L.Hour1),
                            "the night face must not draw day-palette segments");

            Assert.IsTrue(State(21, true).Backlit, "21:00 is night");
            Assert.IsFalse(State(13, true).Backlit, "13:00 is day");
            Assert.IsTrue(new WatchRigState(new WatchFaceState(13, 0, 0, Weekday.Monday, 1,
                                                               Season.EarlySpring, 1, false, false),
                                            true, light: true, showSeconds: false).Backlit,
                          "the LIGHT pusher backlights a day face");
        }

        /// <summary>
        /// No label on the face may hit the 3×5 font's unknown-glyph path. That path draws a solid tofu
        /// block AND raises a <c>Debug.LogError</c>, which the framework escalates to a failure — so
        /// rendering every label a real face can show is the assertion (<see cref="RigFontUnknownGlyphTests"/>
        /// is where that guard is pinned). This sweeps all four seasons, all seven weekdays, both
        /// market states and both 12/24-hour faces, which between them touch every authored label.
        /// </summary>
        [Test]
        public void EveryLabelTheFaceCanShow_HasRealGlyphs()
        {
            RigDrawUtil.ResetUnknownGlyphReports();
            var surface = new DrawSurface(WatchRigRender.W, WatchRigRender.H);

            for (int season = 0; season < 4; season++)
                for (int day = 0; day < 7; day++)
                {
                    var face = new WatchFaceState(day * 3, 47, 29, (Weekday)day, 28 - day,
                                                  (Season)season, season + 1,
                                                  isMarketDay: day % 2 == 0, isNight: day > 3);
                    WatchRigRender.Render(surface,
                        new WatchRigState(face, use24: day % 2 == 0, light: false, showSeconds: true));
                }
        }
    }
}
