using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Law 5 of the three moments (lead-architect ruling 2026-09-09, rule 5): the sim clock never reads
    /// <c>Time.timeScale</c>. The landing hit-stop dips it to 0.05 for 0.09 s; the world must age 0.09 s of
    /// wall time under that dip, not 4.5 ms. Before this fix <c>GameClock.Update</c> integrated scaled
    /// <c>Time.deltaTime</c> and every landing took ~85 ms from the tide.
    /// </summary>
    public class GameClockReadsWallTimeTests
    {
        private float _savedTimeScale;
        private GameObject _go;

        [SetUp] public void SetUp() { _savedTimeScale = Time.timeScale; }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = _savedTimeScale;
            if (_go != null) Object.DestroyImmediate(_go);
        }

        private GameClock BuildClock()
        {
            _go = new GameObject("clock-under-test");
            var clock = _go.AddComponent<GameClock>();           // EditMode: no Awake, so wire the config by hand
            var config = ScriptableObject.CreateInstance<GameConfig>();
            typeof(GameClock).GetField("_config", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(clock, config);
            clock.SeekTo(0d);
            return clock;
        }

        [Test]
        public void A_hit_stop_at_0_05_for_90ms_ages_the_world_90ms_of_wall_time_not_4_5ms()
        {
            var clock = BuildClock();
            Time.timeScale = 0.05f;                       // what HitStopHost writes for the dip
            clock.Advance(0.09f);                         // 90 ms of WALL time, as Update hands it
            Assert.That(clock.TotalSeconds, Is.EqualTo(0.09d).Within(1e-6), "the clock read the dip");
            Assert.That(clock.TotalSeconds, Is.Not.EqualTo(0.09d * 0.05d).Within(1e-6), "4.5 ms is the scaled world");
        }

        [Test]
        public void The_clocks_own_TimeScale_is_the_one_rate_and_IsPaused_the_one_pause()
        {
            var clock = BuildClock();
            Time.timeScale = 0.05f;
            clock.TimeScale = 2f;
            clock.Advance(0.5f);
            Assert.That(clock.TotalSeconds, Is.EqualTo(1.0d).Within(1e-6), "TimeScale is the one rate");
            clock.IsPaused = true;
            clock.Advance(0.5f);
            Assert.That(clock.TotalSeconds, Is.EqualTo(1.0d).Within(1e-6), "IsPaused is the one pause");
        }

        [Test]
        public void The_clock_source_hands_Update_the_UNSCALED_delta_and_never_reads_the_scaled_one()
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Code", "Environment", "GameClock.cs");
            string src = File.ReadAllText(path);
            StringAssert.Contains("Advance(Time.unscaledDeltaTime)", src, "Update integrates wall time");
            StringAssert.DoesNotContain("Time.deltaTime", src, "the sim clock never reads the scaled delta");
            StringAssert.DoesNotContain("Time.timeScale =", src, "the clock never writes the feel channel");
        }
    }
}
