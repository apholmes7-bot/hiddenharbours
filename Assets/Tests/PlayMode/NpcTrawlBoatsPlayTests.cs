using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The visible step of an NPC trawler's day, passage only, on the real Def and the real hull: she is drawn
    /// in her harbour's scene while the day has her there, in the open water's scene while it has her outside,
    /// and in neither when the region she is in is not the one loaded. Two empty scenes stand in for the two
    /// regions, so no region scene is loaded or touched.
    /// </summary>
    public class NpcTrawlBoatsPlayTests
    {
        const string DefPath = "Assets/_Project/Data/Trawlers/RicePointFaceTrawlDay.asset";
        const string HullPath = "Assets/_Project/Data/Boats/SternTrawler.asset";
        const string HarbourScene = "NpcTrawlTestHarbour";
        const string GroundScene = "NpcTrawlTestGround";
        const double Day = GameConfig.DefaultSecondsPerDay;
        const int WorkedDay = 2;

        sealed class Sea : IEnvironmentService
        {
            public int WorldSeed => 4242;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => 0f;
            public float WaterLevelAt(double totalSeconds) => 0f;
        }

        sealed class SetClock : IGameClock
        {
            public double T;
            public double TotalSeconds => T;
            public GameTime Now => new GameTime(T);
            public bool IsPaused { get; set; } = true;
            public float TimeScale { get; set; }
            public int DayIndex => (int)System.Math.Floor(T / Day);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float DayFraction => (float)((T / Day) % 1.0);
            public float HourOfDay => DayFraction * 24f;
            public void SeekTo(double totalSeconds) => T = totalSeconds;
        }

        readonly List<Object> _spawned = new();
        SetClock _clock;
        NpcTrawlDayDef _def;
        BoatHullDef _hull;
        NpcTrawlBoats _boats;
        NpcTrawlDay _today;
        bool _autoBindWas;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _clock = new SetClock { T = WorkedDay * Day + 5.0 };
            GameServices.Environment = new Sea();
            GameServices.Clock = _clock;

#if UNITY_EDITOR
            var committed = AssetDatabase.LoadAssetAtPath<NpcTrawlDayDef>(DefPath);
            Assert.IsNotNull(committed, DefPath);
            _hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>(HullPath);
            Assert.IsNotNull(_hull, HullPath);
            // The committed day, with the two stand-in scenes in place of the two regions.
            _def = Object.Instantiate(committed);
            _spawned.Add(_def);
            _def.Id = "trawlday.test_rpt_face";
            _def.HarbourSceneName = HarbourScene;
            _def.GroundSceneName = GroundScene;
#else
            Assert.Ignore("Needs the AssetDatabase: this drives the real committed Def and hull.");
#endif

            _boats = NpcTrawlBoats.Instance;
            if (_boats == null)
            {
                var host = new GameObject("[NpcTrawlBoats] (test)");
                _spawned.Add(host);
                _boats = host.AddComponent<NpcTrawlBoats>();
            }
            _autoBindWas = _boats.AutoBind;
            _boats.AutoBind = false;
            _today = NpcTrawlDay.Build(_def, 4242, WorkedDay, Day);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_boats != null)
            {
                if (_def != null) _boats.Unbind(_def.Id);
                _boats.AutoBind = _autoBindWas;
            }
            foreach (string name in new[] { HarbourScene, GroundScene })
            {
                Scene scene = SceneManager.GetSceneByName(name);
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
            GameServices.Reset();
        }

        /// <summary>Moves the clock and waits past the presenter's slow tick, so the loaded scenes are re-read.</summary>
        IEnumerator At(double clockSeconds)
        {
            _clock.T = clockSeconds;
            float until = Time.unscaledTime + 0.3f;
            while (Time.unscaledTime < until) yield return null;
            yield return null;
            yield return null;
        }

        static void Load(string scene) => SceneManager.CreateScene(scene);

        static IEnumerator Unload(string scene)
        {
            yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByName(scene));
        }

        Transform Hull => _boats.HullOf(_def.Id);
        Vector2 HullAt => Hull.position;
        bool Shown => _boats.IsShown(_def.Id) && Hull.gameObject.activeInHierarchy;

        [UnityTest]
        public IEnumerator BeforeSheLetsGo_SheLiesAtHerBerth_InTheHarbour_WithAHullThatDraws()
        {
            Load(HarbourScene);
            Assert.IsTrue(_boats.TryBind(_def, _hull, null));
            Assert.IsFalse(_boats.TryBind(_def, _hull, null), "one hull per day");
            yield return At(_today.DayStartSeconds + 5.0);

            Assert.IsTrue(Shown, "she is drawn in her harbour");
            Assert.That((HullAt - _def.BerthPosition).magnitude, Is.LessThan(0.01f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(-Hull.eulerAngles.z, _def.BerthHeadingDegrees)), Is.LessThan(0.1f),
                        "bow where her berth's bow is");
            var renderers = Hull.GetComponentsInChildren<Renderer>(false);
            Assert.That(renderers.Length, Is.GreaterThan(0), "the stern trawler's hull is drawn, not an empty object");
        }

        [UnityTest]
        public IEnumerator SheGoesOutThroughTheChannelMouth_AndIsGoneFromTheHarbour()
        {
            Load(HarbourScene);
            Assert.IsTrue(_boats.TryBind(_def, _hull, null));

            double under = _today.DepartSeconds + _def.BoardSeconds + _def.LetGoSeconds;
            yield return At(under + 10.0);
            Assert.IsTrue(Shown);
            Assert.That((HullAt - _def.BerthPosition).magnitude, Is.GreaterThan(5f), "she has left her berth");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(-Hull.eulerAngles.z, _def.BerthHeadingDegrees)), Is.LessThan(0.5f),
                        "going astern, on the heading she lay at");

            yield return At(_today.OutsideFromSeconds - 0.5);
            Assert.IsTrue(Shown);
            Assert.That((HullAt - _def.HarbourOut[_def.HarbourOut.Length - 1]).magnitude, Is.LessThan(2f),
                        "the last the harbour sees of her is at the channel mouth");

            yield return At(_today.OutsideFromSeconds + 1.0);
            Assert.IsFalse(Shown, "she is on the open water, which is not loaded: nothing of her is drawn here");

            yield return At(_today.OnGroundFromSeconds + 30.0);
            Assert.IsFalse(Shown, "nor while she works her ground");
        }

        [UnityTest]
        public IEnumerator OnTheOpenWater_SheComesInAtTheSeam_WorksHerTrack_AndLeavesByTheSeam()
        {
            Load(GroundScene);
            Assert.IsTrue(_boats.TryBind(_def, _hull, null));

            yield return At(_today.DayStartSeconds + 5.0);
            Assert.IsFalse(Shown, "at dawn she is at her berth, in the other region");

            yield return At(_today.OutsideFromSeconds + 0.5);
            Assert.IsTrue(Shown, "the second she leaves the harbour she is here");
            Assert.That((HullAt - _def.PassageOut[0]).magnitude, Is.LessThan(5f), "at the seam she arrives by");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(-Hull.eulerAngles.z, 180f)), Is.LessThan(2f), "heading south, into the water");

            double middle = (_today.OnGroundFromSeconds + _today.OnGroundUntilSeconds) * 0.5;
            yield return At(middle);
            Assert.IsTrue(Shown);
            Vector2 a = _def.WorkTrack[0], b = _def.WorkTrack[_def.WorkTrack.Length - 1];
            Assert.That(HullAt.y, Is.EqualTo(a.y).Within(0.1f), "on her track");
            Assert.That(HullAt.x, Is.InRange(Mathf.Min(a.x, b.x) - 0.1f, Mathf.Max(a.x, b.x) + 0.1f));
            Vector2 was = HullAt;
            yield return At(middle + 10.0);
            Assert.That((HullAt - was).magnitude, Is.EqualTo(_def.WorkSpeed * 10f).Within(0.5f), "making her working speed");

            yield return At(_today.InsideFromSeconds - 0.5);
            Assert.IsTrue(Shown);
            Assert.That((HullAt - _def.PassageHome[_def.PassageHome.Length - 1]).magnitude, Is.LessThan(5f));

            yield return At(_today.InsideFromSeconds + 1.0);
            Assert.IsFalse(Shown, "home through the channel mouth: gone from the open water");
        }

        [UnityTest]
        public IEnumerator SheComesHomeBowFirst_AndIsFastAtHerBerthAgain()
        {
            Load(HarbourScene);
            Assert.IsTrue(_boats.TryBind(_def, _hull, null));

            yield return At(_today.InsideFromSeconds + 5.0);
            Assert.IsTrue(Shown, "back in the harbour");
            float course = NpcPotDay.Bearing(_def.HarbourHome[0], _def.HarbourHome[1], 0f);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(-Hull.eulerAngles.z, course)), Is.LessThan(1f), "bow on her course");

            yield return At(_today.ReturnSeconds + 5.0);
            Assert.IsTrue(Shown);
            Assert.That((HullAt - _def.BerthPosition).magnitude, Is.LessThan(0.01f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(-Hull.eulerAngles.z, _def.BerthHeadingDegrees)), Is.LessThan(0.1f));
        }

        [UnityTest]
        public IEnumerator LoadedAtAnyHour_SheIsWhereTheClockSays_InWhicheverRegionIsLoaded()
        {
            Load(HarbourScene);
            Assert.IsTrue(_boats.TryBind(_def, _hull, null));
            double onGround = _today.OnGroundFromSeconds + 60.0;
            yield return At(onGround);
            Assert.IsFalse(Shown);

            // The player sails out: the harbour unloads and the open water loads.
            yield return Unload(HarbourScene);
            Load(GroundScene);
            yield return At(onGround + 1.0);
            Assert.IsTrue(Shown, "the same boat, now seen from the water she is on");
            NpcTrawlBoatPose pose = _today.PoseAt(onGround + 1.0);
            Assert.That((HullAt - pose.Position).magnitude, Is.LessThan(0.01f));

            // The next day she does it again.
            NpcTrawlDay tomorrow = NpcTrawlDay.Build(_def, 4242, WorkedDay + 1, Day);
            yield return At(tomorrow.OnGroundFromSeconds + 60.0);
            Assert.IsTrue(Shown);
            Assert.That(_boats.DayOf(_def.Id).DayIndex, Is.EqualTo(WorkedDay + 1));
            Assert.That((HullAt - tomorrow.PoseAt(tomorrow.OnGroundFromSeconds + 60.0).Position).magnitude, Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator AFrameOfTheFleet_AllocatesNothing()
        {
            Load(GroundScene);
            Assert.IsTrue(_boats.TryBind(_def, _hull, null));
            yield return At(_today.OnGroundFromSeconds + 60.0);
            Assert.IsTrue(Shown);
            _boats.Tick();
            Assert.That(() => { _clock.T += 0.02; _boats.Tick(); }, Is.Not.AllocatingGCMemory());
        }
    }
}
