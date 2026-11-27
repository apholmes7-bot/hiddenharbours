#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using HiddenHarbours.Core;
using HiddenHarbours.UI;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>New Game on the title page begins the island week.</b> The real title page is put up and its
    /// own New Game button pressed (and, over a game in progress, its own confirm). Afterwards the
    /// week's key is in the save, the gate at the end of the week is live and has a Tasks note to
    /// give, and the island's weather stands where he is. Continue, from the same page, begins
    /// nothing. A flag another part of a new game sets first does not stop the week.
    ///
    /// <para>The save is a fixture in memory. No save file is read or written.</para>
    /// </summary>
    public class IslandWeekBeginsPlayTests
    {
        const string Region = "region.st_peters";
        const string DoryOnSlip = "st_peters.dory_on_slip";

        sealed class MemorySave : ISaveService
        {
            public SaveData Current { get; private set; }
            public bool LoadedExistingSave { get; private set; }
            public string SetOnNewGame;
            public readonly Dictionary<string, int> Writes = new Dictionary<string, int>();

            public MemorySave(SaveData data, bool existing)
            {
                Current = data;
                LoadedExistingSave = existing;
            }

            public bool GetFlag(string key)
            {
                List<SaveFlag> flags = Current?.OnboardingFlags;
                if (flags == null) return false;
                for (int i = 0; i < flags.Count; i++)
                    if (flags[i].Key == key) return flags[i].Value;
                return false;
            }

            public void SetFlag(string key, bool value)
            {
                Current.OnboardingFlags ??= new List<SaveFlag>();
                Writes.TryGetValue(key, out int n);
                Writes[key] = n + 1;
                for (int i = 0; i < Current.OnboardingFlags.Count; i++)
                {
                    if (Current.OnboardingFlags[i].Key != key) continue;
                    Current.OnboardingFlags[i] = new SaveFlag(key, value);
                    return;
                }
                Current.OnboardingFlags.Add(new SaveFlag(key, value));
            }

            public void Save() { }

            public void BeginNewGame()
            {
                Current = SaveMigration.NewGame();
                LoadedExistingSave = false;
                if (SetOnNewGame != null) SetFlag(SetOnNewGame, true);
            }
        }

        sealed class DayClock : IGameClock
        {
            public double TotalSeconds => GameConfig.DefaultSecondsPerDay * 0.4;
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 9.6f;
            public float DayFraction => 0.4f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        readonly List<Object> _spawned = new List<Object>();
        MemorySave _save;
        CampaignBarrierDirector _weather;
        Transform _player;

        [SetUp]
        public void SetUp()
        {
            TitleScreen.CloseIfOpen();
            GameServices.Reset();
            ShellFlow.Reset();
            ShellPause.Reset();
            LocalWeather.Clear();
            IslandWeekStart.IsHost = null;
            StoryGates.Load(null);      // the shipped gates, as the game loads them

            GameServices.Clock = new DayClock();
            GameServices.CurrentRegionId = Region;

            // He stands inside the east wall's weather.
            CampaignBarrierDef east = Walls().Single(w => w.Id == "campaign_barrier.st_peters_storm_east");
            var player = new GameObject("Player");
            _spawned.Add(player);
            _player = player.transform;
            _player.position = east.EdgePoint + east.InwardNormal
                               * (0.5f * (east.FullDistanceMetres + east.LimitDistanceMetres));
            GameServices.PlayerTransform = _player;

            _weather = CampaignBarrierDirector.Instance;
            Assert.IsNotNull(_weather, "the weather walls install themselves");
            _weather.LoadWalls(Walls());
        }

        [TearDown]
        public void TearDown()
        {
            TitleScreen.CloseIfOpen();
            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
            if (_weather != null) _weather.LoadWalls(Walls());
            LocalWeather.Clear();
            IslandWeekStart.IsHost = null;
            GameServices.Reset();
            ShellFlow.Reset();
            ShellPause.Reset();
        }

        static CampaignBarrierDef[] Walls() =>
            Resources.LoadAll<CampaignBarrierDef>(CampaignBarrierDirector.ResourcesFolder);

        static Button FindButton(string name)
            => Object.FindObjectsByType<Button>()
                     .FirstOrDefault(b => b.gameObject.activeInHierarchy && b.name == name);

        void GivenSave(bool existing)
        {
            SaveData data = SaveMigration.NewGame();
            if (existing) data.Money = 340;
            _save = new MemorySave(data, existing);
            GameServices.Save = _save;
        }

        void AssertTheWeekHasBegun()
        {
            Assert.IsFalse(ShellFlow.AtTitle, "he is in the world");
            Assert.IsTrue(_save.GetFlag(IslandWeekStart.FirstWeekKey), "the week's key is in the save");
            Assert.AreEqual(1, _save.Writes[IslandWeekStart.FirstWeekKey], "written once");

            // The gate at the end of the week is live, and has a Tasks note for the first thing to do.
            var flags = new SaveFlagStore();
            StoryGateDef gate = StoryGates.FirstLive(flags);
            Assert.IsNotNull(gate, "the gate is live");
            Assert.AreEqual(IslandWeekStart.FirstWeekKey, gate.LiveWhileFlag);
            string note = StoryGates.TaskNote(flags);
            Assert.IsFalse(string.IsNullOrWhiteSpace(note), "and it has a Tasks note to give");
            CollectionAssert.Contains(gate.Notes.Select(n => n.TaskNote).ToArray(), note);

            // The island's weather stands, and is over him where he is.
            foreach (CampaignBarrierDef wall in Walls().Where(w => w.RegionId == Region))
                Assert.IsTrue(CampaignBarrierDirector.Stands(wall), wall.Id);
            _weather.Step();
            Assert.IsNotNull(_weather.Standing, "a wall stands where he is");
            Assert.IsTrue(LocalWeather.Active, "and its weather is laid over the look");
        }

        void AssertNoWeek()
        {
            Assert.IsFalse(_save.GetFlag(IslandWeekStart.FirstWeekKey));
            Assert.IsNull(StoryGates.FirstLive(new SaveFlagStore()), "no gate is live");
            foreach (CampaignBarrierDef wall in Walls().Where(w => w.RegionId == Region))
                Assert.IsFalse(CampaignBarrierDirector.Stands(wall), wall.Id);
            _weather.Step();
            Assert.IsNull(_weather.Standing, "no weather stands");
        }

        [UnityTest]
        public IEnumerator NewGameOnAFirstLaunch_BeginsTheIslandWeek()
        {
            GivenSave(existing: false);
            ShellFlow.EnterTitle();
            yield return null;
            Assert.IsTrue(TitleScreen.IsOpen, "the title page is up");
            AssertNoWeek();

            FindButton("NewGame").onClick.Invoke();
            yield return null;
            yield return null;

            AssertTheWeekHasBegun();
        }

        [UnityTest]
        public IEnumerator NewGameOverAGameInProgress_OnceConfirmed_BeginsTheIslandWeek()
        {
            GivenSave(existing: true);
            ShellFlow.EnterTitle();
            yield return null;

            FindButton("NewGame").onClick.Invoke();
            yield return null;
            Assert.IsTrue(ShellFlow.AtTitle, "it asks first");
            Assert.IsFalse(_save.GetFlag(IslandWeekStart.FirstWeekKey), "and nothing begins until he answers");

            FindButton("Overwrite").onClick.Invoke();
            yield return null;
            yield return null;

            AssertTheWeekHasBegun();
        }

        [UnityTest]
        public IEnumerator WithAnotherFlagSetFirstByTheNewGame_TheWeekStillBegins()
        {
            GivenSave(existing: false);
            _save.SetOnNewGame = DoryOnSlip;
            ShellFlow.EnterTitle();
            yield return null;

            FindButton("NewGame").onClick.Invoke();
            yield return null;
            yield return null;

            Assert.IsTrue(_save.GetFlag(DoryOnSlip), "the other flag was set first");
            AssertTheWeekHasBegun();
        }

        [UnityTest]
        public IEnumerator Continue_BeginsNothing()
        {
            GivenSave(existing: true);
            ShellFlow.EnterTitle();
            yield return null;

            FindButton("Continue").onClick.Invoke();
            yield return null;
            yield return null;

            Assert.IsFalse(ShellFlow.AtTitle, "he is in the world");
            Assert.IsEmpty(_save.Writes, "a game carried on is not written to");
            AssertNoWeek();
        }
    }
}
#endif
