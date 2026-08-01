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
    /// M1 §7.8 — the shell's boot contract. The item exists because a tester with one auto-loaded slot and
    /// no reset "cannot get a second first-impression", so these tests guard the three things that claim
    /// fixes it:
    /// <list type="bullet">
    ///   <item><b>Boot stops at the title</b> with the world frozen and the save NOT applied — the page is
    ///   up, the clock is where it was, nothing has been restored into the live services.</item>
    ///   <item><b>Continue</b> resumes the saved instant and starts the world.</item>
    ///   <item><b>New game over an existing save</b> asks first, and only then mints a fresh blob — and the
    ///   fresh game does not inherit the old one's time.</item>
    /// </list>
    ///
    /// <para>Reads only Core + UI: an in-file fake save service stands in for the real one, so no test ever
    /// goes near the player's save file on disk.</para>
    /// </summary>
    public class ShellBootPlayTests
    {
        private const double SecondsPerDay = 1200.0;
        private const double SavedInstant = 5 * SecondsPerDay + 400.0;   // mid-morning on day 5
        private const int SavedMoney = 340;

        /// <summary>A clock that records whether anything seeked it — "the save was applied" is exactly
        /// "the clock was moved to the saved instant" (SaveRestore's determinism anchor).</summary>
        private sealed class FakeClock : IGameClock
        {
            public double TotalSeconds { get; private set; }
            public int SeekCount;

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

            public void SeekTo(double totalSeconds)
            {
                SeekCount++;
                TotalSeconds = System.Math.Max(0.0, totalSeconds);
            }
        }

        /// <summary>The save service, in memory. <see cref="BeginNewGame"/> mirrors the real one's
        /// contract (fresh blob, no longer a resumed game) without touching disk.</summary>
        private sealed class FakeSave : ISaveService
        {
            public SaveData Current { get; private set; }
            public bool LoadedExistingSave { get; private set; }
            public int SaveCount;
            public int NewGameCount;

            public FakeSave(SaveData data, bool existing)
            {
                Current = data;
                LoadedExistingSave = existing;
            }

            // Flags live in the blob, as they do in the real service — so a grant guard reads the flags
            // of whichever game is currently loaded, which is the whole point of these tests.
            public bool GetFlag(string key)
            {
                var flags = Current?.OnboardingFlags;
                if (flags == null) return false;
                for (int i = 0; i < flags.Count; i++)
                    if (flags[i].Key == key) return flags[i].Value;
                return false;
            }

            public void SetFlag(string key, bool value)
            {
                if (Current == null) return;
                Current.OnboardingFlags ??= new System.Collections.Generic.List<SaveFlag>();
                for (int i = 0; i < Current.OnboardingFlags.Count; i++)
                {
                    if (Current.OnboardingFlags[i].Key != key) continue;
                    Current.OnboardingFlags[i] = new SaveFlag(key, value);
                    return;
                }
                Current.OnboardingFlags.Add(new SaveFlag(key, value));
            }

            public void Save() => SaveCount++;

            public void BeginNewGame()
            {
                NewGameCount++;
                Current = SaveMigration.NewGame();
                LoadedExistingSave = false;
            }
        }

        private FakeClock _clock;
        private FakeSave _save;
        private int _gameLoaded;
        private readonly System.Collections.Generic.List<GameObject> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            TitleScreen.CloseIfOpen();
            GameServices.Reset();
            ShellFlow.Reset();
            EventBus.Clear<GameLoaded>();
            // NOT ShellPhaseChanged: the self-installing ShellPresenter subscribes once for the whole
            // play session, so clearing that channel would silently unsubscribe the very thing under
            // test (and every test after this one).

            _gameLoaded = 0;
            _clock = new FakeClock();
            GameServices.Clock = _clock;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.Destroy(_spawned[i]);
            _spawned.Clear();

            TitleScreen.CloseIfOpen();
            EventBus.Clear<GameLoaded>();
            GameServices.Reset();
            ShellFlow.Reset();
        }

        /// <summary>Install a save service holding a game in progress (or none at all).</summary>
        private void GivenSave(bool existing)
        {
            var data = SaveMigration.NewGame();
            if (existing)
            {
                data.GameTimeSeconds = SavedInstant;
                data.DayIndex = (int)(SavedInstant / SecondsPerDay);
                data.Money = SavedMoney;
            }
            _save = new FakeSave(data, existing);
            GameServices.Save = _save;
        }

        private void CountGameLoaded()
        {
            EventBus.Subscribe<GameLoaded>(OnGameLoaded);
        }

        private void OnGameLoaded(GameLoaded e) => _gameLoaded++;

        private static string[] ScreenText()
            => Object.FindObjectsByType<Text>(FindObjectsSortMode.None)
                     .Where(t => t.gameObject.activeInHierarchy)
                     .Select(t => t.text)
                     .ToArray();

        private static bool ScreenShows(string fragment)
            => ScreenText().Any(s => !string.IsNullOrEmpty(s) && s.Contains(fragment));

        private static Button FindButton(string name)
            => Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                     .FirstOrDefault(b => b.gameObject.activeInHierarchy && b.name == name);

        // ---- boot ---------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Boot_ParksAtTheTitle_WithTheWorldStoppedAndTheSaveUnapplied()
        {
            GivenSave(existing: true);
            CountGameLoaded();

            ShellFlow.EnterTitle();     // exactly what GameRoot.Start does at boot
            yield return null;          // the self-installed presenter puts the page up on the edge

            Assert.IsTrue(ShellFlow.AtTitle, "boot ends at the title, not in the world");
            Assert.IsTrue(_clock.IsPaused, "the world is stopped behind the page");
            Assert.AreEqual(0, _clock.SeekCount, "the save is NOT applied until the player chooses");
            Assert.AreEqual(0, _gameLoaded, "and nothing is told the game has loaded");
            Assert.IsTrue(TitleScreen.IsOpen, "the title page is on screen");
            Assert.IsTrue(ScreenShows(ShellStrings.GameTitle), "with the game's name on it");
        }

        [UnityTest]
        public IEnumerator Title_SaysWhichBuildThisIs()
        {
            // §7.8's exit criterion ends "...and can tell you which build they were on". A closed playtest
            // takes reports from several builds at once, and one that cannot be pinned to a build is worth
            // very little — so the page carries it, and carries it where a first screenshot will catch it.
            GivenSave(existing: false);

            ShellFlow.EnterTitle();
            yield return null;

            Assert.IsTrue(ScreenShows(BuildInfo.StampLine),
                "the title page shows the build stamp — in an editor run that reads 'editor', which is " +
                "the honest answer for a session that was never built");
        }

        [UnityTest]
        public IEnumerator Continue_ResumesTheSavedInstant_AndStartsTheWorld()
        {
            GivenSave(existing: true);
            CountGameLoaded();

            ShellFlow.EnterTitle();
            yield return null;

            FindButton("Continue").onClick.Invoke();
            yield return null;

            Assert.IsFalse(ShellFlow.AtTitle, "picking Continue enters the world");
            Assert.IsFalse(_clock.IsPaused, "and starts the clock");
            Assert.AreEqual(SavedInstant, _clock.TotalSeconds, 0.001,
                "resumed at the instant the game was saved — the determinism anchor (rule 5)");
            Assert.AreEqual(1, _gameLoaded, "GameLoaded fires once, so the fleet re-grants its hull");
            Assert.IsFalse(TitleScreen.IsOpen, "the page comes down behind you");
        }

        [UnityTest]
        public IEnumerator FirstLaunch_OffersNoContinue_AndNewGameNeedsNoConfirm()
        {
            GivenSave(existing: false);

            ShellFlow.EnterTitle();
            yield return null;

            Assert.IsNull(FindButton("Continue"),
                "nothing to go back to — a Continue that loads nothing is worse than no Continue");
            Assert.IsTrue(ScreenShows(ShellStrings.NoGameYet), "and the page says so");

            FindButton("NewGame").onClick.Invoke();
            yield return null;

            Assert.IsFalse(ShellFlow.AtTitle, "with nothing to lose, New game just starts");
            Assert.IsFalse(_save.LoadedExistingSave, "on a blob that is nobody's game in progress");
            Assert.AreEqual(0, _clock.SeekCount, "and keeps the authored start hour");
        }

        // ---- the item that blocks the playtest: New Game over an existing save ---------------

        [UnityTest]
        public IEnumerator NewGame_OverAnExistingSave_AsksFirst_AndTheSafeAnswerIsUnderTheCursor()
        {
            GivenSave(existing: true);

            ShellFlow.EnterTitle();
            yield return null;

            FindButton("NewGame").onClick.Invoke();
            yield return null;

            Assert.IsTrue(ScreenShows(ShellStrings.ConfirmNewGameTitle), "it asks before it destroys");
            Assert.IsTrue(ScreenShows(ShellStrings.ConfirmNewGameBody), "and says what is lost");
            Assert.AreEqual(0, _save.NewGameCount, "nothing has happened to the save yet");
            Assert.IsTrue(ShellFlow.AtTitle, "and the world has not been entered");

            var es = UnityEngine.EventSystems.EventSystem.current;
            Assert.IsNotNull(es, "the confirm is keyboard/gamepad drivable");
            Assert.AreEqual("KeepGame", es.currentSelectedGameObject.name,
                "a player who mashes Enter keeps their harbour");
        }

        [UnityTest]
        public IEnumerator NewGame_BackingOutOfTheConfirm_LeavesTheGameAlone()
        {
            GivenSave(existing: true);

            ShellFlow.EnterTitle();
            yield return null;
            FindButton("NewGame").onClick.Invoke();
            yield return null;

            FindButton("KeepGame").onClick.Invoke();
            yield return null;

            Assert.AreEqual(0, _save.NewGameCount, "the save is untouched");
            Assert.IsTrue(_save.LoadedExistingSave, "it is still the game that was loaded");
            Assert.IsTrue(ShellFlow.AtTitle, "and we are back at the title");
            Assert.IsTrue(ScreenShows(ShellStrings.NewGame), "looking at the menu again");
        }

        [UnityTest]
        public IEnumerator NewGame_Confirmed_MintsAFreshBlob_AndDoesNotInheritTheOldGamesTime()
        {
            GivenSave(existing: true);
            CountGameLoaded();

            ShellFlow.EnterTitle();
            yield return null;
            FindButton("NewGame").onClick.Invoke();
            yield return null;

            FindButton("Overwrite").onClick.Invoke();
            yield return null;

            Assert.AreEqual(1, _save.NewGameCount, "the slot was reset, exactly once");
            Assert.AreEqual(SaveMigration.CurrentVersion, _save.Current.SchemaVersion,
                "a fresh blob at the current schema");
            Assert.AreEqual(0, _save.Current.Money, "with none of the old game's money");
            Assert.IsFalse(_save.LoadedExistingSave, "and it is no longer a resumed game");

            Assert.AreEqual(0, _clock.SeekCount,
                "a new game keeps its authored start hour — seeking to a fresh blob's gameTime of 0 " +
                "would drag it back to midnight");
            Assert.IsFalse(_clock.IsPaused, "the world runs");
            Assert.AreEqual(1, _gameLoaded, "and the one GameLoaded edge fires for a new game too");
            Assert.IsFalse(TitleScreen.IsOpen);
        }

        // ---- the autosave that must NOT fire at the title ------------------------------------

        [Test]
        public void AutosaveIsRefusedAtTheTitle_SoQuittingFromItCannotEraseAGame()
        {
            // The live services hold boot defaults while the loaded blob holds the real game; an
            // OnApplicationQuit autosave from the title would snapshot the former over the latter.
            GivenSave(existing: true);

            ShellFlow.EnterTitle();
            Assert.IsFalse(SaveService.WritesAllowed, "no writing from the title");

            ShellFlow.ContinueGame();
            Assert.IsTrue(SaveService.WritesAllowed, "in the world, autosave works as it always did");
        }

        [Test]
        public void HasGameToContinue_IsFalseWithNoSaveServiceAtAll()
        {
            GameServices.Save = null;
            Assert.IsFalse(ShellFlow.HasGameToContinue);
        }

        // ---- what a new game must NOT inherit -----------------------------------------------
        // A title state in front of play moves the moment the save becomes authoritative. Anything that
        // seeded itself at boot now seeds from the game the player is about to abandon — and a "second
        // first-impression" that starts with the last game's licence and no starting gear is not one.

        [UnityTest]
        public IEnumerator NewGame_DoesNotInheritTheAbandonedGamesLicences()
        {
            GivenSave(existing: true);
            _save.Current.OwnedLicenses = new System.Collections.Generic.List<string> { "license.cod" };

            var go = new GameObject("LicenseService");
            _spawned.Add(go);
            var licenses = go.AddComponent<HiddenHarbours.Economy.LicenseService>();
            yield return null;                       // OnEnable → seeds from the loaded save

            Assert.IsTrue(licenses.IsLicensed("license.cod"), "the game in progress holds its licence");

            ShellFlow.EnterTitle();
            yield return null;
            FindButton("NewGame").onClick.Invoke();
            yield return null;
            FindButton("Overwrite").onClick.Invoke();
            yield return null;

            Assert.IsFalse(licenses.IsLicensed("license.cod"),
                "a fresh harbour has no licences — the opening's licence beat is played again");
        }

        [UnityTest]
        public IEnumerator NewGame_StillGrantsTheStartingKit_EvenThoughTheOldSaveSaidItWasGranted()
        {
            GivenSave(existing: true);
            // The abandoned game had long since been given its shovel and bucket.
            _save.Current.OnboardingFlags = new System.Collections.Generic.List<SaveFlag>
            {
                new SaveFlag("st_peters_starting_gear_granted", true),
            };

            ShellFlow.EnterTitle();
            yield return null;

            var go = new GameObject("StartingGear");
            _spawned.Add(go);
            go.AddComponent<HiddenHarbours.Player.StartingGear>();
            yield return null;                       // Start → defers, because we are at the title

            FindButton("NewGame").onClick.Invoke();
            yield return null;
            FindButton("Overwrite").onClick.Invoke();
            yield return null;

            CollectionAssert.Contains(_save.Current.OwnedGear, "gear.shovel",
                "the new game is granted its starting kit — the abandoned game's flag says nothing " +
                "about this one");
        }
    }
}
