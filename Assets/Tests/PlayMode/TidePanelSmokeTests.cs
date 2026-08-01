using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// VS-06 — a PlayMode smoke test for the player's tide table. It proves the three promises the page
    /// makes: it <b>freezes time</b> through the project's one pause path and gives it back exactly as it
    /// found it, it <b>prints the turns</b> for today and tomorrow, and it <b>computes</b> them from the
    /// Core environment seam rather than anything stored.
    ///
    /// <para>Reads only Core — tiny in-file fakes stand in for the clock and the environment service, so
    /// the test never touches the Environment module.</para>
    /// </summary>
    public class TidePanelSmokeTests
    {
        // A TEST-LOCAL clock scale (the rig sets it on its OWN GameConfig at line ~76), not the shipped
        // dial — the panel is proved against a clock this test fully controls. The game ships 1800.
        private const double SecondsPerDay = 1200.0;
        private const double SecondsPerHour = SecondsPerDay / 24.0;
        private const float Amplitude = 1.6f;

        private sealed class FakeClock : IGameClock
        {
            public double TotalSeconds { get; set; }
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => (int)(TotalSeconds / SecondsPerDay);
            public int DayOfSeason => DayIndex + 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => (float)((TotalSeconds / SecondsPerDay % 1.0) * 24.0);
            public float DayFraction => (float)(TotalSeconds / SecondsPerDay % 1.0);
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        /// <summary>A clean semidiurnal sine, so the page's turns are analytically predictable.</summary>
        private sealed class FakeEnv : IEnvironmentService
        {
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; } = TideProfile.CoddleCove;
            public int HeightQueries;

            public EnvironmentSample Sample()
                => new EnvironmentSample(new Vector2(0f, 3f), Vector2.zero, 1.2f, SeaState.Calm, 1f);

            public float TideHeightAt(double totalSeconds)
            {
                HeightQueries++;
                double period = 12.4206 * SecondsPerHour;
                return (float)(Amplitude * System.Math.Sin(2.0 * System.Math.PI * totalSeconds / period));
            }
        }

        private GameConfig _config;
        private FakeClock _clock;
        private FakeEnv _env;

        [SetUp]
        public void SetUp()
        {
            // A page left over from the previous test would still answer IsOpen until its Destroy
            // resolves. Close it here as well as in TearDown so each test starts from a clean slate
            // regardless of when the frame boundary falls between them.
            TidePanel.CloseIfOpen();
            GameServices.Reset();
            _clock = new FakeClock { TotalSeconds = 0.25 * SecondsPerDay };  // 06:00 on day 0
            _env = new FakeEnv();
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _config.SecondsPerDay = (float)SecondsPerDay;

            GameServices.Clock = _clock;
            GameServices.Environment = _env;
            GameServices.Config = _config;
            GameServices.CurrentRegionId = "region.coddle_cove";
        }

        [TearDown]
        public void TearDown()
        {
            TidePanel.CloseIfOpen();
            GameServices.Reset();
            if (_config != null) Object.DestroyImmediate(_config);
        }

        /// <summary>
        /// Every string the open page is currently showing. Sorted, because
        /// <see cref="FindObjectsSortMode.None"/> means exactly that — the traversal order is not
        /// guaranteed to repeat, so an order-sensitive comparison would flake on its own.
        /// </summary>
        private static string[] PageText()
            => Object.FindObjectsByType<Text>(FindObjectsSortMode.None)
                     .Select(t => t.text)
                     .OrderBy(s => s, System.StringComparer.Ordinal)
                     .ToArray();

        private static bool PageShows(string fragment)
            => PageText().Any(s => !string.IsNullOrEmpty(s) && s.Contains(fragment));

        [UnityTest]
        public IEnumerator Opening_FreezesTheClock_AndClosingGivesItBack()
        {
            Assert.IsFalse(_clock.IsPaused, "the world is running before the page comes out");

            TidePanel.Open();
            Assert.IsTrue(TidePanel.IsOpen);
            Assert.IsTrue(_clock.IsPaused, "time is frozen while the table is read");

            TidePanel.CloseIfOpen();
            yield return null;                       // Destroy resolves at end of frame → OnDestroy runs

            Assert.IsFalse(TidePanel.IsOpen);
            Assert.IsFalse(_clock.IsPaused, "closing the page starts the world again");
        }

        [UnityTest]
        public IEnumerator ClosingDoesNotResumeAWorldThatWasAlreadyPaused()
        {
            // A page opened from an already-paused moment (a pause menu, a cutscene) must not be the
            // thing that starts the sea moving again.
            _clock.IsPaused = true;

            TidePanel.Open();
            Assert.IsTrue(_clock.IsPaused);

            TidePanel.CloseIfOpen();
            yield return null;

            Assert.IsTrue(_clock.IsPaused, "the page restores what it found, it does not assume");
        }

        [UnityTest]
        public IEnumerator Page_IsNotBlank()
        {
            // The bluntest possible guard, and it earned its place: the first build of this panel wired
            // every label's position, size, font and colour but never assigned its TEXT, so the page
            // rendered as a blank sheet of paper while every structural assertion still passed. A label
            // that exists but says nothing is worse than no label — it looks like the feature works.
            TidePanel.Open();
            yield return null;

            string[] labels = PageText();
            Assert.IsNotEmpty(labels, "the page built some labels");
            Assert.IsTrue(labels.Any(s => !string.IsNullOrWhiteSpace(s)),
                "at least one of them has words on it — a page of empty labels is a blank sheet");
        }

        [UnityTest]
        public IEnumerator Page_PrintsTodayAndTomorrowWithHighAndLowWaters()
        {
            TidePanel.Open();
            yield return null;

            Assert.IsTrue(PageShows(HudStrings.TideTableTitle), "the page names itself");
            Assert.IsTrue(PageShows("Today"), "today's column is printed");
            Assert.IsTrue(PageShows("Tomorrow"), "tomorrow's column is printed — this is the crossing you plan");
            Assert.IsTrue(PageShows("High water"), "highs are listed");
            Assert.IsTrue(PageShows("Low water"), "lows are listed");
        }

        [UnityTest]
        public IEnumerator Page_MarksWhereNowFalls()
        {
            TidePanel.Open();
            yield return null;

            Assert.IsTrue(PageShows(HudStrings.TideTableNowMarker),
                "the player must see which turns have gone and which are coming");
            Assert.IsTrue(PageShows("06:00"), "the now-mark carries the current clock time");
        }

        [UnityTest]
        public IEnumerator Page_ComputesFromTheEnvironmentSeam()
        {
            _env.HeightQueries = 0;

            TidePanel.Open();
            yield return null;

            Assert.Greater(_env.HeightQueries, 0,
                "heights are recomputed from IEnvironmentService, never read from a store (rule 5)");
        }

        [UnityTest]
        public IEnumerator Page_IsBuiltOnce_AndDoesNotRepaintWhileItIsRead()
        {
            // The page is built on open and never rebuilt: a frozen world cannot change under it, so
            // there is nothing to repaint (rule 7). Assert that on the PAGE's own content rather than
            // on a count of environment samples — the sample count is a property of the whole scene
            // (anything else alive, an always-on HUD included, samples at its own cadence), so it
            // would be measuring the test run rather than this panel.
            TidePanel.Open();
            yield return null;

            string[] afterBuild = PageText();
            Assert.IsTrue(afterBuild.Any(s => !string.IsNullOrWhiteSpace(s)),
                "the page drew something with words on it to compare");

            yield return null;
            yield return null;

            CollectionAssert.AreEqual(afterBuild, PageText(),
                "nothing on the page moves while it is being read");
        }

        [UnityTest]
        public IEnumerator ClosingReleasesTheSlotImmediately_SoReopeningFreezesTheClockAgain()
        {
            // Regression: Destroy() only resolves at end of frame. If closing left the outgoing page in
            // the static slot, a reopen in the same frame would hand back the page on its way out — and
            // silently skip freezing the clock, because that only happens in Awake.
            TidePanel first = TidePanel.Open();
            Assert.IsTrue(_clock.IsPaused);

            TidePanel.CloseIfOpen();
            Assert.IsFalse(TidePanel.IsOpen, "a closed page is closed now, not at end of frame");
            Assert.IsFalse(_clock.IsPaused, "and time is given back now, too");

            TidePanel second = TidePanel.Open();          // same frame, no yield
            Assert.AreNotSame(first, second, "reopening deals a fresh page");
            Assert.IsTrue(_clock.IsPaused, "which freezes the clock again");

            TidePanel.CloseIfOpen();
            yield return null;
            Assert.IsFalse(_clock.IsPaused);
        }

        [UnityTest]
        public IEnumerator Toggle_OpensThenCloses()
        {
            TidePanel.Toggle();
            Assert.IsTrue(TidePanel.IsOpen);

            TidePanel.Toggle();
            yield return null;
            Assert.IsFalse(TidePanel.IsOpen);
        }

        [UnityTest]
        public IEnumerator Open_IsIdempotent_AndNeverStacksPages()
        {
            TidePanel first = TidePanel.Open();
            TidePanel second = TidePanel.Open();

            Assert.AreSame(first, second, "a second open reuses the page rather than dealing another");
            yield return null;

            TidePanel.CloseIfOpen();
            yield return null;
            Assert.IsFalse(_clock.IsPaused, "one open, one close — the pause does not leak");
        }

        [UnityTest]
        public IEnumerator MissingServices_ShowAnHonestBlankRatherThanWrongNumbers()
        {
            GameServices.Environment = null;

            TidePanel.Open();
            yield return null;

            Assert.IsTrue(PageShows(HudStrings.TideTableUnavailable),
                "with no tide to read, say so — never print a plausible wrong table (P1 truth)");
        }
    }
}
