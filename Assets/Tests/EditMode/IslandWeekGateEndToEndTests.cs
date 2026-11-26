using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The island week, from New Game to the open sea, in the full game.</b> One chain over the
    /// shipped content: a new game begins its week and the four weather walls round St Peters stand;
    /// her word alone lifts nothing; the next kept sleep that reaches morning sets
    /// <c>st_peters.weather_turned</c> and all four walls lift. A save made before the week existed is
    /// never shut in, and a save written mid-week loads with the walls exactly as they were.
    ///
    /// <para>The clock is the real <see cref="GameClock"/>, the sleep is the real
    /// <see cref="RestSaveResponder"/>, the gate and walls are the shipped assets. Only the save is a
    /// stand-in, which keeps flags in the same <see cref="SaveData.OnboardingFlags"/> list the real
    /// service does and round-trips through <see cref="SaveSerialization"/> on every write, so a
    /// "reload" here is the same JSON parse and migration a launch runs.</para>
    /// </summary>
    public class IslandWeekGateEndToEndTests
    {
        const string DataRoot = "Assets/_Project/Data";
        const string WeatherTurned = "st_peters.weather_turned";

        /// <summary>A save that writes its JSON on every change, as the real service does.</summary>
        sealed class JsonSave : ISaveService
        {
            public string Json;
            public SaveData Current { get; private set; }
            public bool LoadedExistingSave { get; private set; }

            public JsonSave(SaveData data) { Current = data; }

            public static JsonSave Load(string json)
            {
                var save = new JsonSave(SaveSerialization.FromJson(json)) { Json = json };
                save.LoadedExistingSave = true;
                return save;
            }

            public bool GetFlag(string key)
            {
                if (string.IsNullOrEmpty(key) || Current?.OnboardingFlags == null) return false;
                foreach (SaveFlag f in Current.OnboardingFlags) if (f.Key == key) return f.Value;
                return false;
            }

            public void SetFlag(string key, bool value)
            {
                if (string.IsNullOrEmpty(key) || Current == null) return;
                Current.OnboardingFlags ??= new List<SaveFlag>();
                for (int i = 0; i < Current.OnboardingFlags.Count; i++)
                {
                    if (Current.OnboardingFlags[i].Key != key) continue;
                    if (Current.OnboardingFlags[i].Value == value) return;
                    Current.OnboardingFlags[i] = new SaveFlag(key, value);
                    Save();
                    return;
                }
                Current.OnboardingFlags.Add(new SaveFlag(key, value));
                Save();
            }

            public void Save()
            {
                IGameClock clock = GameServices.Clock;
                if (clock != null)
                {
                    Current.GameTimeSeconds = clock.TotalSeconds;
                    Current.DayIndex = clock.DayIndex;
                }
                Json = SaveSerialization.ToJson(Current);
            }
        }

        readonly List<Object> _spawned = new List<Object>();
        GameConfig _config;
        GameClock _clock;
        StoryGateDef _gate;
        List<CampaignBarrierDef> _walls;

        IGameClock _previousClock;
        GameConfig _previousConfig;
        ISaveService _previousSave;
        bool? _previousDemo;
        System.Func<bool> _previousDemoHost;
        System.Func<bool> _previousWeekHost;
        bool _responderWasInstalled;

        [SetUp]
        public void SetUp()
        {
            _previousClock = GameServices.Clock;
            _previousConfig = GameServices.Config;
            _previousSave = GameServices.Save;
            _previousDemo = DemoBuild.Override;
            _previousDemoHost = DemoWeek.IsHost;
            _previousWeekHost = IslandWeekStart.IsHost;
            _responderWasInstalled = RestSaveResponder.Installed;
            SleepFlow.Reset();

            DemoBuild.Override = false;
            DemoWeek.IsHost = null;
            IslandWeekStart.IsHost = null;

            _config = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(_config);
            var go = new GameObject("IslandWeekGateClock");
            _spawned.Add(go);
            _clock = go.AddComponent<GameClock>();
            FieldInfo field = typeof(GameClock).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "GameClock keeps its config in _config");
            field.SetValue(_clock, _config);
            _clock.SeekTo(_clock.StartSeconds);
            GameServices.Clock = _clock;
            GameServices.Config = _config;

            _gate = Resources.LoadAll<StoryGateDef>(StoryGates.ResourcesFolder)
                .Single(g => g.MorningSetsFlag == WeatherTurned);
            StoryGates.Load(_gate);
            StoryGates.ListenForMorning();

            _walls = AssetDatabase.FindAssets("t:CampaignBarrierDef", new[] { DataRoot })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<CampaignBarrierDef>)
                .Where(w => w != null && w.LiftedByFlag == WeatherTurned).ToList();
        }

        [TearDown]
        public void TearDown()
        {
            SleepFlow.Reset();
            StoryGates.Load(null);
            if (!_responderWasInstalled) RestSaveResponder.Uninstall();
            GameServices.Clock = _previousClock;
            GameServices.Config = _previousConfig;
            GameServices.Save = _previousSave;
            DemoBuild.Override = _previousDemo;
            DemoWeek.IsHost = _previousDemoHost;
            IslandWeekStart.IsHost = _previousWeekHost;
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        double At(int day, double hour) =>
            day * (double)_config.SecondsPerDay + _config.SecondsPerDay * (hour / 24d);

        JsonSave NewGame()
        {
            var save = new JsonSave(SaveMigration.NewGame());
            GameServices.Save = save;
            Assert.IsTrue(IslandWeekStart.BeginForNewGame(save), "a new game begins its island week");
            return save;
        }

        void HearHerWord(ISaveService save) => save.SetFlag(_gate.SetsFlag, true);

        GameSaved KeptNightSleep()
        {
            _clock.SeekTo(At(_clock.DayIndex, 22));
            GameSaved kept = RestSaveResponder.Fulfil(new RestSaveRequested("your bed at Ginny's"));
            Assert.IsTrue(kept.Ok, "the night was kept");
            return kept;
        }

        int Standing() => _walls.Count(CampaignBarrierDirector.Stands);

        [Test]
        public void TheShippedGateAndWallsAreTheOnesThisChainNames()
        {
            Assert.AreEqual(4, _walls.Count, "four weather walls round St Peters");
            foreach (var wall in _walls)
                Assert.AreEqual(IslandWeekStart.FirstWeekKey, wall.StandsWhileFlag, wall.Id);
            Assert.AreEqual(IslandWeekStart.FirstWeekKey, _gate.LiveWhileFlag);
            Assert.AreEqual(WeatherTurned, _gate.PendingUntilFlag);
        }

        [Test]
        public void ANewGame_StandsTheWalls_AndTheKeptMorningAfterHerWordLiftsAllFour()
        {
            JsonSave save = NewGame();
            Assert.IsTrue(save.GetFlag(IslandWeekStart.FirstWeekKey));
            Assert.AreEqual(4, Standing(), "the week has begun: every wall stands");

            KeptNightSleep();
            Assert.IsFalse(save.GetFlag(WeatherTurned), "a night before her word turns nothing");
            Assert.AreEqual(4, Standing());

            HearHerWord(save);
            Assert.AreEqual(4, Standing(), "her word alone lifts nothing");

            KeptNightSleep();
            Assert.IsTrue(save.GetFlag(WeatherTurned), "the kept morning after her word turns the weather");
            Assert.AreEqual(0, Standing(), "all four walls lift");
            StringAssert.Contains(WeatherTurned, save.Json, "the turned weather is in the written file");

            KeptNightSleep();
            Assert.AreEqual(0, Standing(), "and stay lifted");
        }

        [Test]
        public void ANapBeforeMorning_DoesNotTurnTheWeather()
        {
            JsonSave save = NewGame();
            HearHerWord(save);
            _clock.SeekTo(At(_clock.DayIndex, 12));
            Assert.IsTrue(RestSaveResponder.Fulfil(new RestSaveRequested("your bed at Ginny's")).Ok);
            Assert.IsFalse(save.GetFlag(WeatherTurned));
            Assert.AreEqual(4, Standing());
        }

        [Test]
        public void AnOldSave_WithoutTheWeek_IsNeverShutIn()
        {
            // A save from before the island week existed: under way, flags of its own, no week key.
            var old = SaveMigration.NewGame();
            old.SchemaVersion = SaveMigration.CurrentVersion - 1;
            old.OnboardingFlags = new List<SaveFlag>
            {
                new SaveFlag(OnboardingFlags.OnboardedKey, true),
                new SaveFlag("arrived_st_peters", true),
            };
            JsonSave save = JsonSave.Load(SaveSerialization.ToJson(old));
            GameServices.Save = save;
            Assert.IsNotNull(save.Current, "the old save loads");
            Assert.IsFalse(save.GetFlag(IslandWeekStart.FirstWeekKey));
            Assert.AreEqual(0, Standing(), "an old save meets no weather wall");

            KeptNightSleep();
            Assert.AreEqual(0, Standing(), "and a night does not raise one");
            Assert.IsFalse(save.GetFlag(IslandWeekStart.FirstWeekKey), "nothing but New Game begins the week");
        }

        [Test]
        public void ASaveWrittenMidWeek_LoadsWithTheWallsUp_AndOneAfterTheMorningWithThemDown()
        {
            JsonSave save = NewGame();
            KeptNightSleep();
            HearHerWord(save);
            string midWeek = save.Json; // written by the word itself, before the next night

            GameServices.Save = JsonSave.Load(midWeek);
            Assert.IsTrue(GameServices.Save.GetFlag(IslandWeekStart.FirstWeekKey));
            Assert.IsTrue(GameServices.Save.GetFlag(_gate.SetsFlag), "her word was kept");
            Assert.AreEqual(4, Standing(), "mid-week, reloaded: the walls stand");

            KeptNightSleep();
            string afterMorning = ((JsonSave)GameServices.Save).Json;
            GameServices.Save = JsonSave.Load(afterMorning);
            Assert.IsTrue(GameServices.Save.GetFlag(WeatherTurned));
            Assert.AreEqual(0, Standing(), "the morning after, reloaded: the walls are down");
        }

        [Test]
        public void TheDemo_KeepsEveryWallUp_AndItsMorningTurnsNothing()
        {
            DemoBuild.Override = true;
            JsonSave save = NewGame();
            HearHerWord(save);
            KeptNightSleep();
            Assert.IsFalse(save.GetFlag(WeatherTurned), "the demo ends at her word");
            Assert.AreEqual(4, Standing(), "the demo's exits stay closed");
        }
    }
}
