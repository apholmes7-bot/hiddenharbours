using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The weather→shadow hook is LIVE (ADR 0013 PR 2 fix): <see cref="DayNightController"/> must publish a
    /// <c>_ShadowStrength</c> global that folds the LIVE weather, so the projected <see cref="SpriteShadow"/>
    /// actually softens under overcast/storm in-game (it was inert — the live path overwrote strength with
    /// <c>clamp01(elevation)</c> and never read weather). The pure fold is unit-tested in
    /// <c>DayNightMathTests</c>; this guards the missing piece — that the controller FEEDS it real weather
    /// and publishes the result, so <c>OvercastFadesShadow</c> is no longer dead.
    ///
    /// <para>Deterministic (rule 5): a pure function of the injected fake clock + weather + profile, nothing
    /// saved. Core-only sim reads (rule 4): the controller reads <see cref="GameServices.Clock"/> /
    /// <see cref="GameServices.Environment"/>. We drive a controller's tick synchronously (so the
    /// auto-installed singleton can't race our read) and assert the published global.</para>
    /// </summary>
    public class ShadowStrengthGlobalPlayTests
    {
        private static readonly int IdShadowStrength = Shader.PropertyToID("_ShadowStrength");

        // ---- minimal Core-contract fakes (only the bits the controller reads) -----------------

        /// <summary>A clock fixed at a single hour — the controller only reads <see cref="HourOfDay"/>.</summary>
        private sealed class FixedClock : IGameClock
        {
            private readonly float _hour;
            public FixedClock(float hour) { _hour = hour; }
            public double TotalSeconds => _hour * 3600.0;
            public GameTime Now => default;
            public Season Season => Season.HighSummer;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => default;
            public bool IsMarketDay => false;
            public float HourOfDay => _hour;
            public float DayFraction => Mathf.Repeat(_hour, 24f) / 24f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        /// <summary>An environment that reports a fixed weather mood — the controller reads
        /// <c>Sample().Visibility</c> + <c>Sample().SeaState</c>.</summary>
        private sealed class FixedEnvironment : IEnvironmentService
        {
            private readonly float _visibility;
            private readonly SeaState _seaState;
            public FixedEnvironment(float visibility, SeaState seaState)
            {
                _visibility = visibility;
                _seaState = seaState;
            }
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample()
                => new EnvironmentSample(Vector2.zero, Vector2.zero, 0f, _seaState, _visibility);
            public float TideHeightAt(double totalSeconds) => 0f;
        }

        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
            GameServices.Reset();
        }

        /// <summary>Spawn a controller and synchronously run ONE tick against the current GameServices,
        /// returning the <c>_ShadowStrength</c> it published. Driving the private Tick directly makes the
        /// read deterministic — the auto-installed singleton's own Update can't overwrite it between the
        /// tick and the read.</summary>
        private float PublishStrengthForWeather(float hour, float visibility, SeaState seaState)
        {
            GameServices.Clock = new FixedClock(hour);
            GameServices.Environment = new FixedEnvironment(visibility, seaState);

            var go = new GameObject("DayNightController (test)");
            _spawned.Add(go);
            var controller = go.AddComponent<DayNightController>();   // Awake loads the default profile

            // Run a tick synchronously so our publish is the last write before we read the global.
            MethodInfo tick = typeof(DayNightController)
                .GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(tick, "DayNightController.Tick() not found (private API moved?)");
            tick.Invoke(controller, null);

            return Shader.GetGlobalFloat(IdShadowStrength);
        }

        [Test]
        public void Controller_PublishesShadowStrength_ThatFadesUnderStormVsClear()
        {
            const float noon = 13f;   // high clear noon → firm shadow (default sunrise 6 / sunset 20)

            float clear = PublishStrengthForWeather(noon, visibility: 1f, seaState: SeaState.Glass);
            float storm = PublishStrengthForWeather(noon, visibility: 0f, seaState: SeaState.Storm);

            Assert.Greater(clear, 0.9f, "a high clear noon publishes a firm shadow strength");
            Assert.Less(storm, clear, "a storm/fog at noon publishes a WEAKER shadow strength (the weather hook is live)");
            Assert.Greater(storm, 0f, "weather alone never fully erases the shadow at the default OvercastFadesShadow");
        }

        [Test]
        public void Controller_PublishesZeroShadowStrength_AtNight()
        {
            float night = PublishStrengthForWeather(2f, visibility: 1f, seaState: SeaState.Glass);
            Assert.AreEqual(0f, night, 1e-3f, "no sun at 2am → no cast shadow, whatever the weather");
        }
    }
}
