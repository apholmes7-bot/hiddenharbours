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
    /// M1 §7.8 — the pause menu and the settings sheet. The two promises worth guarding are that
    /// <b>pause freezes and unfreezes cleanly</b> (through the project's one pause path, restoring what it
    /// found rather than assuming), and that the <b>faders actually move the mix</b> — a settings screen
    /// whose sliders do nothing is worse than no settings screen.
    ///
    /// <para>Reads only Core + UI. The audio mix is a tiny in-file fake, so no test needs the Audio module
    /// or a running director; the stored-settings tests snapshot and restore the real PlayerPrefs keys so a
    /// test run never changes the machine's actual settings.</para>
    /// </summary>
    public class ShellPausePlayTests
    {
        private sealed class FakeClock : IGameClock
        {
            public double TotalSeconds { get; private set; }
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 6f;
            public float DayFraction => 0.25f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
            public void SeekTo(double totalSeconds) => TotalSeconds = totalSeconds;
        }

        private sealed class FakeMix : IAudioMix
        {
            public float MasterVolume { get; set; } = 1f;
            public float AmbienceVolume { get; set; } = 0.8f;
            public float SfxVolume { get; set; } = 1f;
            public float MusicVolume { get; set; } = 0.6f;
        }

        private FakeClock _clock;
        private FakeMix _mix;

        // The settings keys are the MACHINE's real preferences, and putting the sheet away writes them.
        // Snapshot every one of them around each test so a test run can never change the owner's own
        // volume or window mode.
        private static readonly string[] PrefKeys =
        {
            GameSettings.MasterKey, GameSettings.AmbienceKey, GameSettings.SfxKey,
            GameSettings.MusicKey, GameSettings.FullscreenKey,
        };
        private readonly bool[] _hadPref = new bool[PrefKeys.Length];
        private readonly float[] _prefValue = new float[PrefKeys.Length];

        private void SnapshotPrefs()
        {
            for (int i = 0; i < PrefKeys.Length; i++)
            {
                _hadPref[i] = PlayerPrefs.HasKey(PrefKeys[i]);
                _prefValue[i] = _hadPref[i] ? PlayerPrefs.GetFloat(PrefKeys[i], 0f) : 0f;
            }
        }

        private void RestorePrefs()
        {
            for (int i = 0; i < PrefKeys.Length; i++)
            {
                if (_hadPref[i]) PlayerPrefs.SetFloat(PrefKeys[i], _prefValue[i]);
                else PlayerPrefs.DeleteKey(PrefKeys[i]);
            }
            PlayerPrefs.Save();
        }

        [SetUp]
        public void SetUp()
        {
            SnapshotPrefs();
            SettingsSheet.CloseIfOpen();
            PauseMenu.CloseIfOpen();
            TitleScreen.CloseIfOpen();
            GameServices.Reset();
            ShellFlow.Reset();
            InteractionGate.Reset();

            _clock = new FakeClock();
            _mix = new FakeMix();
            GameServices.Clock = _clock;
            GameServices.AudioMix = _mix;
        }

        [TearDown]
        public void TearDown()
        {
            SettingsSheet.CloseIfOpen();
            PauseMenu.CloseIfOpen();
            TitleScreen.CloseIfOpen();
            GameServices.Reset();
            ShellFlow.Reset();
            InteractionGate.Reset();
            RestorePrefs();
        }

        private static Button FindButton(string name)
            => Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                     .FirstOrDefault(b => b.gameObject.activeInHierarchy && b.name == name);

        private static Slider FindSlider(string name)
            => Object.FindObjectsByType<Slider>(FindObjectsSortMode.None)
                     .FirstOrDefault(s => s.gameObject.activeInHierarchy && s.name == name);

        private static bool ScreenShows(string fragment)
            => Object.FindObjectsByType<Text>(FindObjectsSortMode.None)
                     .Where(t => t.gameObject.activeInHierarchy)
                     .Any(t => !string.IsNullOrEmpty(t.text) && t.text.Contains(fragment));

        // ---- pause freezes and unfreezes cleanly --------------------------------------------

        [UnityTest]
        public IEnumerator Pause_StopsTheWorld_AndResumingStartsItAgain()
        {
            Assert.IsFalse(_clock.IsPaused, "the world is running before the menu");

            PauseMenu.Open();
            Assert.IsTrue(PauseMenu.IsOpen);
            Assert.IsTrue(_clock.IsPaused, "time is stopped while the menu is up");
            Assert.IsTrue(ShellFlow.WorldInputBlocked, "and the controls are the shell's, not the sea's");
            Assert.IsTrue(InteractionGate.IsBlocked, "the shared Interact key does not reach the world");

            FindButton("Resume").onClick.Invoke();
            yield return null;

            Assert.IsFalse(PauseMenu.IsOpen);
            Assert.IsFalse(_clock.IsPaused, "resuming starts the world again");
            Assert.IsFalse(ShellFlow.WorldInputBlocked, "and gives the controls back");
            Assert.IsFalse(InteractionGate.IsBlocked);
        }

        [UnityTest]
        public IEnumerator Pause_OverAnAlreadyStoppedWorld_DoesNotStartItOnClose()
        {
            // The tide table (or any other page) already has time stopped. Closing the pause menu must
            // restore what it FOUND — it is not the authority on whether the world should be running.
            _clock.IsPaused = true;

            PauseMenu.Open();
            Assert.IsTrue(_clock.IsPaused);

            PauseMenu.CloseIfOpen();
            yield return null;

            Assert.IsTrue(_clock.IsPaused, "the page restores what it found, it does not assume");
        }

        [UnityTest]
        public IEnumerator ClosingReleasesTheSlotImmediately_SoReopeningStopsTheWorldAgain()
        {
            PauseMenu first = PauseMenu.Open();
            PauseMenu.CloseIfOpen();
            Assert.IsFalse(PauseMenu.IsOpen, "a closed menu is closed now, not at end of frame");
            Assert.IsFalse(_clock.IsPaused, "and time is given back now, too");

            PauseMenu second = PauseMenu.Open();          // same frame, no yield
            Assert.AreNotSame(first, second, "reopening deals a fresh page");
            Assert.IsTrue(_clock.IsPaused, "which stops the world again");

            PauseMenu.CloseIfOpen();
            yield return null;
            Assert.IsFalse(_clock.IsPaused, "one open, one close — the pause does not leak");
        }

        [Test]
        public void Pause_RefusesToOpenAtTheTitle()
        {
            ShellFlow.EnterTitle();

            Assert.IsNull(PauseMenu.Open(), "there is no session to pause at the title");
            Assert.IsFalse(PauseMenu.IsOpen);
        }

        [UnityTest]
        public IEnumerator Pause_OffersBothWaysOut_AndSaysBothSave()
        {
            PauseMenu.Open();
            yield return null;

            Assert.IsNotNull(FindButton("QuitToTitle"));
            Assert.IsNotNull(FindButton("QuitToDesktop"));
            Assert.IsTrue(ScreenShows(ShellStrings.QuitSavesFirst),
                "a tester must know their last hour counts before they pick either");
        }

        // ---- the faders move the mix ---------------------------------------------------------

        [UnityTest]
        public IEnumerator Settings_SlidersDriveTheLiveMix()
        {
            SettingsSheet.Open(null);
            yield return null;

            Slider music = FindSlider("Music");
            Assert.IsNotNull(music, "there is a music fader");
            Assert.AreEqual(_mix.MusicVolume, music.value, 0.001f, "starting where the mix actually is");

            music.value = 0f;
            Assert.AreEqual(0f, _mix.MusicVolume, 0.001f,
                "the sound moves under your hand — that is the only way a volume can be judged");

            FindSlider("Master").value = 0.25f;
            Assert.AreEqual(0.25f, _mix.MasterVolume, 0.001f);
        }

        [UnityTest]
        public IEnumerator Settings_WithNoAudioRunning_SaysSo_RatherThanShowingDeadSliders()
        {
            GameServices.AudioMix = null;

            SettingsSheet.Open(null);
            yield return null;

            Assert.IsNull(FindSlider("Master"), "no faders where there is nothing to fade");
            Assert.IsTrue(ScreenShows(ShellStrings.AudioUnavailable), "and the sheet says why (P1 truth)");
        }

        [UnityTest]
        public IEnumerator Settings_BackReturnsToWhoeverOpenedIt()
        {
            int returned = 0;

            SettingsSheet.Open(() => returned++);
            yield return null;

            FindButton("Back").onClick.Invoke();
            yield return null;

            Assert.IsFalse(SettingsSheet.IsOpen, "the sheet is put away");
            Assert.AreEqual(1, returned, "and the page that opened it comes back — exactly once");
        }

        [UnityTest]
        public IEnumerator Settings_OpenedFromPause_LeavesTheWorldStoppedThroughout()
        {
            PauseMenu.Open();
            FindButton("Settings").onClick.Invoke();
            yield return null;

            Assert.IsTrue(SettingsSheet.IsOpen);
            Assert.IsTrue(_clock.IsPaused, "the world stays stopped while the settings are read");

            FindButton("Back").onClick.Invoke();
            yield return null;

            Assert.IsTrue(PauseMenu.IsOpen, "Back lands on the menu it was opened from");
            Assert.IsTrue(_clock.IsPaused, "and the world is still stopped — only Resume starts it");

            PauseMenu.CloseIfOpen();
            yield return null;
            Assert.IsFalse(_clock.IsPaused);
        }

        // ---- the mix is remembered, and the save is not the place it is remembered ------------

        [Test]
        public void StoredMix_RoundTrips_WithoutTouchingTheSave()
        {
            // TearDown puts the machine's real preferences back — see SnapshotPrefs.
            _mix.MasterVolume = 0.42f;
            _mix.AmbienceVolume = 0.13f;
            GameSettings.StoreFrom(_mix);

            var reloaded = new FakeMix();
            GameSettings.LoadInto(reloaded);

            Assert.AreEqual(0.42f, reloaded.MasterVolume, 0.001f);
            Assert.AreEqual(0.13f, reloaded.AmbienceVolume, 0.001f);
        }

        [Test]
        public void UnstoredMix_LeavesTheAuthoredDefaultsAlone()
        {
            PlayerPrefs.DeleteKey(GameSettings.MasterKey);

            var fresh = new FakeMix { MasterVolume = 0.77f };
            GameSettings.LoadInto(fresh);

            Assert.AreEqual(0.77f, fresh.MasterVolume, 0.001f,
                "a first run is not silently overridden by a file of zeros");
        }
    }
}
