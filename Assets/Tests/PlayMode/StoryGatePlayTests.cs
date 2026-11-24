#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>Day seven on St Peters, played.</b> A save in its island week with the dory mended, the cod
    /// licence held, a fish kept from the dory and shellfish sold; the clock on the seventh day; the
    /// gate's own islander (the shipped asset) within reach, inside the weather on the island's east
    /// edge. He talks to her: her bubble carries the word, and when he has heard it out the flag is in
    /// the save and the very next step of the weather has no wall standing and nothing laid over the
    /// look. Walking away from the word leaves it to be heard.
    ///
    /// <para>The save is a fixture in memory. No save file is read or written.</para>
    /// </summary>
    public class StoryGatePlayTests
    {
        const string GatePath = "Assets/_Project/Data/Resources/StoryGates/StPetersWeather.asset";
        const string RulesPath = "Assets/_Project/Data/Resources/IslandWeek.asset";
        const string DataRoot = "Assets/_Project/Data";
        const string Region = "region.st_peters";
        const float Reach = 1.8f;

        sealed class FlagSave : ISaveService
        {
            public readonly Dictionary<string, int> Writes = new Dictionary<string, int>();
            public SaveData Current { get; } = new SaveData();

            public bool GetFlag(string key)
            {
                foreach (SaveFlag flag in Current.OnboardingFlags)
                    if (flag.Key == key) return flag.Value;
                return false;
            }

            public void SetFlag(string key, bool value)
            {
                Writes.TryGetValue(key, out int n);
                Writes[key] = n + 1;
                for (int i = 0; i < Current.OnboardingFlags.Count; i++)
                    if (Current.OnboardingFlags[i].Key == key)
                    {
                        Current.OnboardingFlags[i] = new SaveFlag(key, value);
                        return;
                    }
                Current.OnboardingFlags.Add(new SaveFlag(key, value));
            }

            public void Save() { }
        }

        sealed class DayClock : IGameClock
        {
            public int Day;
            public double TotalSeconds => Day * (double)GameConfig.DefaultSecondsPerDay + GameConfig.DefaultSecondsPerDay * 0.5;
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => Day;
            public int DayOfSeason => Day + 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 12f;
            public float DayFraction => 0.5f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        readonly List<Object> _spawned = new List<Object>();
        StoryGateDef _gate;
        IslandWeekRulesDef _rules;
        NpcDef _islander;
        FlagSave _save;
        DayClock _clock;
        Transform _player;
        DialoguePresenter _presenter;
        WorldInteractor _interactor;
        CampaignBarrierDirector _weather;

        [SetUp]
        public void SetUp()
        {
            Spawn("AudioListener").AddComponent<AudioListener>();

            GameServices.Reset();
            InteractionGate.Reset();
            InteractActorProbe.Reset();
            InteractOffer.Reset();
            LocalWeather.Clear();

            _gate = AssetDatabase.LoadAssetAtPath<StoryGateDef>(GatePath);
            _rules = AssetDatabase.LoadAssetAtPath<IslandWeekRulesDef>(RulesPath);
            _islander = AssetDatabase.FindAssets("t:NpcDef", new[] { DataRoot })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<NpcDef>)
                .Single(n => n != null && n.Id == _gate.NpcId);
            StoryGates.Load(null);      // the shipped gates, as the game loads them

            // The day-7 save: in its island week, has met her, and holds the four saved facts.
            _save = new FlagSave();
            _save.Current.OnboardingFlags.Add(new SaveFlag(_gate.LiveWhileFlag, true));
            _save.Current.OnboardingFlags.Add(new SaveFlag(_islander.CompletionFlag, true));
            _save.Current.OnboardingFlags.Add(new SaveFlag(IslandWeekFactRecorder.FishFromDoryKey, true));
            _save.Current.OnboardingFlags.Add(new SaveFlag(IslandWeekFactRecorder.ShellfishSoldKey, true));
            _save.Current.RepairedBoats.Add(_rules.DoryId);
            _save.Current.OwnedLicenses.Add(_rules.LicenceId);
            GameServices.Save = _save;
            _clock = new DayClock { Day = _rules.EarliestDay - 1 };
            GameServices.Clock = _clock;
            GameServices.CurrentRegionId = Region;

            // He stands inside the east wall's weather, and she is beside him.
            CampaignBarrierDef east = Walls().Single(w => w.Id == "campaign_barrier.st_peters_storm_east");
            Vector2 inTheWeather = east.EdgePoint + east.InwardNormal
                                   * (0.5f * (east.FullDistanceMetres + east.LimitDistanceMetres));
            _player = Spawn("Player").transform;
            _player.position = inTheWeather;
            GameServices.PlayerTransform = _player;

            var her = Spawn("Islander");
            her.transform.position = inTheWeather + new Vector2(0.8f, 0f);
            var talkPoint = her.AddComponent<Interactable>();
            talkPoint.Configure(_islander);

            _presenter = Spawn("DialoguePresenter").AddComponent<DialoguePresenter>();
            _interactor = Spawn("WorldInteractor").AddComponent<WorldInteractor>();
            _interactor.Configure(_player, _presenter, new[] { talkPoint }, Reach);

            _weather = CampaignBarrierDirector.Instance;
            Assert.IsNotNull(_weather, "the weather walls install themselves");
            _weather.LoadWalls(Walls());
        }

        [TearDown]
        public void TearDown()
        {
            if (_presenter != null && _presenter.IsShowing) _presenter.Close();
            foreach (Object o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
            if (_weather != null)
                _weather.LoadWalls(Resources.LoadAll<CampaignBarrierDef>(CampaignBarrierDirector.ResourcesFolder));
            LocalWeather.Clear();
            GameServices.Reset();
            InteractionGate.Reset();
        }

        GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        static CampaignBarrierDef[] Walls() =>
            Resources.LoadAll<CampaignBarrierDef>(CampaignBarrierDirector.ResourcesFolder);

        static string Flat(string text) => Regex.Replace(text ?? "", @"\s+", " ").Trim();

        /// <summary>Hear the open conversation to its end, as presses of Interact; returns what was shown.</summary>
        IEnumerator HearItOut(List<string> heard)
        {
            for (int guard = 0; guard < 64 && _presenter.IsShowing; guard++)
            {
                if (_presenter.IsFilling) _presenter.Advance();      // the press that fills the line
                heard.Add(Flat(_presenter.VisibleText));
                _presenter.Advance();                                // the press that moves on
                yield return null;
            }
            Assert.IsFalse(_presenter.IsShowing, "the conversation ended");
        }

        void AssertTheWallsStand()
        {
            _weather.Step();
            Assert.IsNotNull(_weather.Standing, "a wall stands where he is");
            Assert.Greater(_weather.Look01, 0f, "and its weather is over him");
            Assert.IsTrue(LocalWeather.Active, "laid over the look");
            foreach (CampaignBarrierDef wall in Walls().Where(w => w.RegionId == Region))
                Assert.IsTrue(CampaignBarrierDirector.Stands(wall), wall.Id);
        }

        [UnityTest]
        public IEnumerator OnDaySeven_HerWordSetsTheFlag_AndTheWallsAreGoneTheSameStep()
        {
            Assert.AreSame(_save, GameServices.Save, "the fixture save, never a file");
            AssertTheWallsStand();

            Assert.IsTrue(_interactor.BeginInteract(), "she is in reach and has something to say");
            yield return null;
            Assert.IsTrue(_presenter.IsShowing, "her ordinary speech bubble");
            Assert.IsFalse(_save.GetFlag(_gate.SetsFlag), "not while she is still speaking");

            var heard = new List<string>();
            yield return HearItOut(heard);
            string all = string.Join(" ", heard);
            foreach (string line in _gate.Word.FirstLines)
                StringAssert.Contains(Flat(line), all, "the word is the gate's own lines");

            Assert.IsTrue(_save.GetFlag(_gate.SetsFlag), "heard out, the flag is in the save");
            Assert.AreEqual(1, _save.Writes[_gate.SetsFlag]);

            // The very next step of the weather: nothing stands, nothing is laid over the look.
            _weather.Step();
            Assert.IsNull(_weather.Standing);
            Assert.AreEqual(0f, _weather.Look01);
            Assert.AreEqual(0f, _weather.Storm01);
            Assert.AreEqual(0f, _weather.PushAcceleration);
            Assert.IsFalse(LocalWeather.Active, "the storm look is cleared");
            Assert.IsFalse(LocalWeather.SquallActive, "and the front with it");
            foreach (CampaignBarrierDef wall in Walls().Where(w => w.RegionId == Region))
                Assert.IsFalse(CampaignBarrierDirector.Stands(wall), wall.Id + " is down");

            // Talking to her again is her ordinary talk, and writes the flag no second time.
            yield return null;
            Assert.IsTrue(_interactor.BeginInteract());
            yield return null;
            heard.Clear();
            yield return HearItOut(heard);
            all = string.Join(" ", heard);
            StringAssert.DoesNotContain(Flat(_gate.Word.FirstLines[0]), all, "the word is said once");
            StringAssert.Contains(Flat(_islander.Dialogue.Lines(true)[0]), all, "her ordinary talk");
            Assert.AreEqual(1, _save.Writes[_gate.SetsFlag], "set once");
        }

        [UnityTest]
        public IEnumerator WalkingAwayFromTheWord_LeavesItToBeHeard()
        {
            Assert.IsTrue(_interactor.BeginInteract());
            yield return null;
            Assert.IsTrue(_presenter.IsShowing);

            _presenter.Close();                 // he walked off mid-word
            yield return null;
            Assert.IsFalse(_save.GetFlag(_gate.SetsFlag), "a word he did not hear out opens nothing");
            AssertTheWallsStand();

            Assert.IsTrue(_interactor.BeginInteract(), "and she will say it again");
            yield return null;
            var heard = new List<string>();
            yield return HearItOut(heard);
            StringAssert.Contains(Flat(_gate.Word.FirstLines[0]), string.Join(" ", heard));
            Assert.IsTrue(_save.GetFlag(_gate.SetsFlag));
        }

        [UnityTest]
        public IEnumerator ADayEarly_SheSaysWhatIsStillToDo_AndTheWallsStand()
        {
            _clock.Day = _rules.EarliestDay - 2;

            Assert.IsTrue(_interactor.BeginInteract());
            yield return null;
            var heard = new List<string>();
            yield return HearItOut(heard);
            string all = string.Join(" ", heard);

            Assert.IsTrue(_gate.TryNote(_rules.DayReachedTextKey, out StoryGateNote note));
            StringAssert.Contains(Flat(note.SpokenHint), all, "one line about the first thing missing");
            StringAssert.Contains(Flat(_islander.Dialogue.Lines(true)[0]), all, "after her ordinary talk");
            Assert.IsFalse(_save.GetFlag(_gate.SetsFlag));
            Assert.IsFalse(_save.Writes.ContainsKey(_gate.SetsFlag));
            AssertTheWallsStand();
        }

        [UnityTest]
        public IEnumerator AnOlderSave_HearsOnlyHerOrdinaryTalk_AndMeetsNoWeather()
        {
            // The same progressed save without the first-week key: the save a player had before.
            _save.Current.OnboardingFlags.RemoveAll(f => f.Key == _gate.LiveWhileFlag);

            _weather.Step();
            Assert.IsNull(_weather.Standing, "no wall for a save that never started the week");
            Assert.IsFalse(LocalWeather.Active);

            Assert.IsTrue(_interactor.BeginInteract());
            yield return null;
            var heard = new List<string>();
            yield return HearItOut(heard);
            string all = string.Join(" ", heard);
            StringAssert.DoesNotContain(Flat(_gate.Word.FirstLines[0]), all);
            foreach (StoryGateNote note in _gate.Notes)
                StringAssert.DoesNotContain(Flat(note.SpokenHint), all);
            Assert.IsFalse(_save.Writes.ContainsKey(_gate.SetsFlag), "and nothing is written for the gate");
        }
    }
}
#endif
