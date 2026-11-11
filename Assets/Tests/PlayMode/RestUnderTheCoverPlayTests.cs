using System.Collections;
using HiddenHarbours.Core;
using HiddenHarbours.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The real screen cover answers a rest.</b> The EditMode tests pin the ORDER a rest runs in
    /// against recording stand-ins. This is the other half: the game's own cover, the one that
    /// self-installs in play, is black in the very frame of the press and clears by itself afterwards,
    /// with nothing in the rest waiting on it.
    ///
    /// <para>It reads the cover's state (its image's alpha and whether it draws), not pixels, so it
    /// holds on a machine with no graphics device.</para>
    /// </summary>
    public class RestUnderTheCoverPlayTests
    {
        /// <summary>How long the test will watch for the cover to clear, in real seconds. A test bound
        /// only: many times the cover's own duration, so a slow machine does not fail a working fade.</summary>
        const float WatchSeconds = 10f;

        sealed class MovingClock : IGameClock
        {
            public int Moves;
            public double TotalSeconds { get; private set; } = 1650d;
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.HighSummer;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 22f;
            public float DayFraction => 22f / 24f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
            public bool IsHeld { get; set; }

            public bool FastForwardTo(double totalSeconds)
            {
                if (IsHeld || !(totalSeconds > TotalSeconds)) return false;
                TotalSeconds = totalSeconds;
                Moves++;
                return true;
            }
        }

        sealed class CountingSave : ISaveService
        {
            public int Saves;
            public SaveData Current { get; } = SaveMigration.NewGame();
            public bool LoadedExistingSave { get; private set; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { Saves++; LoadedExistingSave = true; }
        }

        IGameClock _previousClock;
        GameConfig _previousConfig;
        ISaveService _previousSave;
        string _previousRegion;
        bool _responderWasInstalled;
        GameConfig _config;

        [SetUp]
        public void SetUp()
        {
            _previousClock = GameServices.Clock;
            _previousConfig = GameServices.Config;
            _previousSave = GameServices.Save;
            _previousRegion = GameServices.CurrentRegionId;
            _responderWasInstalled = RestSaveResponder.Installed;
            _config = ScriptableObject.CreateInstance<GameConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            if (!_responderWasInstalled) RestSaveResponder.Uninstall();
            GameServices.Clock = _previousClock;
            GameServices.Config = _previousConfig;
            GameServices.Save = _previousSave;
            GameServices.CurrentRegionId = _previousRegion;
            if (_config != null) Object.Destroy(_config);
        }

        static Image TheCover()
        {
            RegionFadeOverlay.Install();
            GameObject overlay = GameObject.Find("RegionFadeOverlay");
            Assert.IsNotNull(overlay, "the cover installs itself in play");
            Transform black = overlay.transform.Find("RegionFade_Canvas/Black");
            Assert.IsNotNull(black, "the cover has its black image");
            return black.GetComponent<Image>();
        }

        static IEnumerator UntilClear(Image black)
        {
            float deadline = Time.realtimeSinceStartup + WatchSeconds;
            while (black.enabled && Time.realtimeSinceStartup < deadline) yield return null;
        }

        [UnityTest]
        public IEnumerator A_rest_is_black_in_the_frame_of_the_press_and_clears_by_itself()
        {
            Image black = TheCover();
            yield return UntilClear(black);   // a cover left fading by an earlier scene change is not ours
            Assert.IsFalse(black.enabled, "the cover is idle before the press");

            var clock = new MovingClock();
            var save = new CountingSave();
            GameServices.Clock = clock;
            GameServices.Config = _config;
            GameServices.Save = save;
            GameServices.CurrentRegionId = null;
            RestSaveResponder.Install();

            EventBus.Publish(new RestSaveRequested("your bed at Ginny's"));

            // Same frame, before anything is drawn: fully black, the clock already moved, the day kept.
            Assert.IsTrue(black.enabled, "the cover draws in the frame of the press");
            Assert.AreEqual(1f, black.color.a, 1e-6f, "and it is fully black");
            Assert.AreEqual(1, clock.Moves, "the morning came under it");
            Assert.AreEqual(1, save.Saves, "and the save was written once under it");

            yield return null;
            yield return null;
            Assert.Less(black.color.a, 1f, "the cover was let go: it is fading, not held");

            yield return UntilClear(black);
            Assert.IsFalse(black.enabled, "the cover clears by itself; nothing is left black");
            Assert.AreEqual(0f, black.color.a, 1e-6f);
            Assert.AreEqual(1, save.Saves, "no second write while it faded");
        }

        [UnityTest]
        public IEnumerator A_rest_on_a_held_hour_never_touches_the_cover()
        {
            Image black = TheCover();
            yield return UntilClear(black);

            var clock = new MovingClock { IsHeld = true };
            var save = new CountingSave();
            GameServices.Clock = clock;
            GameServices.Config = _config;
            GameServices.Save = save;
            GameServices.CurrentRegionId = null;
            RestSaveResponder.Install();

            EventBus.Publish(new RestSaveRequested("your bed at Ginny's"));

            Assert.IsFalse(black.enabled, "no light changes, so nothing is covered");
            Assert.AreEqual(0, clock.Moves, "the held hour did not move");
            Assert.AreEqual(1, save.Saves, "the rest is the save it always was");
        }
    }
}
