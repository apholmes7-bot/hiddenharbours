using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The diegetic watch mapper (<see cref="WatchFaceState.FromClock"/>) — binds the game clock to the
    /// art director's <c>WatchRig</c> params. Verifies the time canon anchors against a fake clock (new
    /// game = High Summer · Day 1 · 07:30), that hour/minute TRUNCATE (a watch shows elapsed time, not
    /// rounded), and that the night backlight flips at the documented 05:30 / 21:30 thresholds. No Unity
    /// runtime — a pure mapper over the <see cref="IGameClock"/> contract, tested with the fake-clock
    /// pattern the EditMode suite already uses.
    /// </summary>
    public class WatchFaceStateTests
    {
        /// <summary>Minimal settable IGameClock (the PotStockGatingTests fake shape).</summary>
        private sealed class FakeClock : IGameClock
        {
            public double Seconds;
            public double TotalSeconds => Seconds;
            public GameTime Now => default;
            public Season Season { get; set; } = Season.HighSummer;
            public int Year { get; set; } = 1;
            public int DayIndex { get; set; }
            public int DayOfSeason { get; set; } = 1;
            public Weekday Weekday { get; set; } = Weekday.Monday;
            public bool IsMarketDay { get; set; }
            public float HourOfDay { get; set; } = 7.5f;
            public float DayFraction => HourOfDay / 24f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
            public void SeekTo(double totalSeconds) => Seconds = totalSeconds;
        }

        [Test]
        public void NewGameAnchor_EarlySpringDay1_0600()
        {
            var w = WatchFaceState.FromClock(new FakeClock());   // defaults = the new-game start
            Assert.AreEqual(7, w.Hour);
            Assert.AreEqual(30, w.Minute);
            Assert.AreEqual(0, w.Second);
            Assert.AreEqual(Weekday.Monday, w.Weekday);
            Assert.AreEqual(1, w.DayOfSeason);
            Assert.AreEqual(Season.HighSummer, w.Season);
            Assert.AreEqual(1, w.Year);
            Assert.IsFalse(w.IsNight, "07:30 is morning");
        }

        [Test]
        public void HourMinute_Truncate_NeverRoundUp()
        {
            // 13:45:30 — a rounding mapper would show 13:46; a watch shows 13:45.
            var clock = new FakeClock { HourOfDay = 13f + 45.5f / 60f };
            var w = WatchFaceState.FromClock(clock);
            Assert.AreEqual(13, w.Hour);
            Assert.AreEqual(45, w.Minute, "minute truncates, does not round to 46");
            Assert.AreEqual(30, w.Second, 1, "seconds ~30 (cosmetic; ±1 float tolerance)");
        }

        [Test]
        public void Night_FlipsAt_DuskAndDawn()
        {
            Assert.IsTrue (WatchFaceState.NightAt(5.49f), "before 05:30 is night");
            Assert.IsFalse(WatchFaceState.NightAt(5.50f), "05:30 is day");
            Assert.IsFalse(WatchFaceState.NightAt(21.49f), "just before 21:30 is day");
            Assert.IsTrue (WatchFaceState.NightAt(21.50f), "21:30 is night");
            Assert.IsTrue (WatchFaceState.NightAt(23.5f), "late is night");
        }

        [Test]
        public void Night_ThresholdsAreTunable()
        {
            // An explicit fixture can move dusk independently of the shipping profile.
            Assert.IsTrue(WatchFaceState.NightAt(20f, nightStartHour: 19f));
            Assert.IsFalse(WatchFaceState.NightAt(20f), "the shipped sun is still up at 20:00");
        }

        [Test]
        public void PassesThrough_MarketDay_Season_Year_Weekday()
        {
            var clock = new FakeClock
            {
                Season = Season.TheTurn, Year = 3, DayOfSeason = 12,
                Weekday = Weekday.Friday, IsMarketDay = true, HourOfDay = 9.25f,
            };
            var w = WatchFaceState.FromClock(clock);
            Assert.AreEqual(Season.TheTurn, w.Season);
            Assert.AreEqual(3, w.Year);
            Assert.AreEqual(12, w.DayOfSeason);
            Assert.AreEqual(Weekday.Friday, w.Weekday);
            Assert.IsTrue(w.IsMarketDay);
            Assert.AreEqual(9, w.Hour);
            Assert.AreEqual(15, w.Minute, "9.25h = 09:15");
        }

        [Test]
        public void Midnight_Wraps()
        {
            var w = WatchFaceState.FromClock(new FakeClock { HourOfDay = 0f });
            Assert.AreEqual(0, w.Hour);
            Assert.AreEqual(0, w.Minute);
            Assert.IsTrue(w.IsNight, "00:00 is night");
        }
    }
}
