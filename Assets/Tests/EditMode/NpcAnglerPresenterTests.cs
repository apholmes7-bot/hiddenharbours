using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>No flourish on a stale edge</b> (ADR 0053 §11): a villager angler's splash fires only on a phase
    /// edge between two consecutive drawn frames on a clock that ran forward by about that step. Never on
    /// the first sample after attaching, never across a frame gap, never across a clock jump. And the
    /// angler service attaches one presenter per host, however often it is asked.
    /// </summary>
    public sealed class NpcAnglerPresenterTests
    {
        private const double FrameSeconds = 1d / 60d;
        private const float SplashFps = 12f;

        private readonly List<Object> _made = new List<Object>();
        private IGameClock _previousClock;
        private ScriptedClock _clock;

        private sealed class FakeAnglerStand : IAnglerStand
        {
            public bool Fishing = true;
            public FishingState State;
            public float Seconds;
            public AnglerLook LookValue;

            public bool TryGetAnglerState(out FishingState state, out float secondsInPhase)
            {
                state = State;
                secondsInPhase = Seconds;
                return Fishing;
            }

            public AnglerLook Look => LookValue;
        }

        private sealed class ScriptedClock : IGameClock
        {
            public double TotalSeconds { get; private set; }
            public GameTime Now => new GameTime(TotalSeconds);
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
            public int DayIndex => 0;
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float DayFraction => 0f;
            public float HourOfDay => 12f;
            public void SeekTo(double totalSeconds) => TotalSeconds = totalSeconds;
        }

        [SetUp]
        public void SetUp()
        {
            _previousClock = GameServices.Clock;
            _clock = new ScriptedClock();
            _clock.SeekTo(500d);
            GameServices.Clock = _clock;
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Clock = _previousClock;
            for (int i = _made.Count - 1; i >= 0; i--)
                if (_made[i] != null) Object.DestroyImmediate(_made[i]);
            _made.Clear();
        }

        [Test]
        public void TheFirstSampleAfterAttaching_ThrowsNoSplash_EvenRightAfterTouchdown()
        {
            FakeAnglerStand stand = Stand();
            NpcAnglerPresenter p = Presenter(stand);
            stand.State = Phase(FishingPhase.Waiting);
            stand.Seconds = 0.001f;
            p.Present(10);
            Assert.IsFalse(p.SplashRenderer.enabled, "a splash on the first sample after attaching");
        }

        [Test]
        public void AConsecutiveTouchdown_ThrowsTheSplash()
        {
            FakeAnglerStand stand = Stand();
            NpcAnglerPresenter p = Presenter(stand);
            stand.State = Phase(FishingPhase.Cast);
            stand.Seconds = 1f;
            p.Present(10);

            _clock.SeekTo(_clock.TotalSeconds + FrameSeconds);
            stand.State = Phase(FishingPhase.Waiting);
            stand.Seconds = (float)(FrameSeconds * 0.5d);
            p.Present(11);
            Assert.IsTrue(p.SplashRenderer.enabled, "a consecutive Cast to Waiting edge threw no splash");
        }

        [Test]
        public void ATouchdownAfterAFrameGap_ThrowsNoSplash()
        {
            FakeAnglerStand stand = Stand();
            NpcAnglerPresenter p = Presenter(stand);
            stand.State = Phase(FishingPhase.Cast);
            stand.Seconds = 1f;
            p.Present(10);

            _clock.SeekTo(_clock.TotalSeconds + FrameSeconds);
            stand.State = Phase(FishingPhase.Waiting);
            stand.Seconds = (float)(FrameSeconds * 0.5d);
            p.Present(12);   // a frame was skipped (he was hidden)
            Assert.IsFalse(p.SplashRenderer.enabled, "a splash across a frame gap");
        }

        [Test]
        public void ATouchdownAcrossAClockJump_ThrowsNoSplash()
        {
            FakeAnglerStand stand = Stand();
            NpcAnglerPresenter p = Presenter(stand);
            stand.State = Phase(FishingPhase.Cast);
            stand.Seconds = 1f;
            p.Present(10);

            // The next frame, but the clock leapt (a sleep skip): the edge is far older than a frame.
            _clock.SeekTo(_clock.TotalSeconds + FrameSeconds);
            stand.State = Phase(FishingPhase.Waiting);
            stand.Seconds = 30f;
            p.Present(11);
            Assert.IsFalse(p.SplashRenderer.enabled, "a splash for an edge older than the frame step");

            // And a clock that ran backwards (a seek) is never consecutive either.
            stand.State = Phase(FishingPhase.Cast);
            stand.Seconds = 1f;
            p.Present(12);
            _clock.SeekTo(_clock.TotalSeconds - 10d);
            stand.State = Phase(FishingPhase.Waiting);
            stand.Seconds = 0.001f;
            p.Present(13);
            Assert.IsFalse(p.SplashRenderer.enabled, "a splash across a backward seek");
        }

        [Test]
        public void WhenHeStopsFishing_TheLineAndFishAreHidden()
        {
            FakeAnglerStand stand = Stand();
            NpcAnglerPresenter p = Presenter(stand);
            stand.State = Phase(FishingPhase.Waiting);
            stand.Seconds = 2f;
            p.Present(10);
            stand.Fishing = false;
            _clock.SeekTo(_clock.TotalSeconds + FrameSeconds);
            p.Present(11);
            Assert.IsFalse(p.DrawsLine);
            Assert.IsFalse(p.FishRenderer.enabled);
        }

        [Test]
        public void TheService_AttachesOnePresenterPerHost_AndRebindsIt()
        {
            GameObject host = Track(new GameObject("NpcAnglerHost"));
            host.AddComponent<SpriteRenderer>();
            var service = new AnglerPresentationService();
            FakeAnglerStand a = Stand(), b = Stand();
            IAnglerFigure first = service.Attach(host, a);
            IAnglerFigure second = service.Attach(host, b);
            Assert.IsNotNull(first);
            Assert.AreSame(first, second, "a second attach must return the presenter already there");
            Assert.AreEqual(1, host.GetComponents<NpcAnglerPresenter>().Length);
            Assert.IsNull(service.Attach(null, a));
            Assert.IsNull(service.Attach(host, null));
        }

        // ================================================================ harness

        private FakeAnglerStand Stand()
        {
            var tex = Track(new Texture2D(2, 2));
            var frames = new Sprite[4];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = Track(Sprite.Create(tex, new Rect(0, 0, 2, 2), Vector2.zero));
            return new FakeAnglerStand
            {
                LookValue = new AnglerLook
                {
                    LineWidthM = 0.02f, LineSegments = 8,
                    FishSecondsPerFrame = 0.1f, FishDartSpeedMps = 1.2f, FishSpeedSmoothing01 = 0.35f,
                    FallbackJumpPeriodSeconds = 4.5f, WaterElementOrder = 30,
                    SplashFrames = frames, SplashFps = SplashFps,
                },
            };
        }

        private NpcAnglerPresenter Presenter(FakeAnglerStand stand)
        {
            GameObject host = Track(new GameObject("NpcAngler"));
            host.AddComponent<SpriteRenderer>();
            var p = host.AddComponent<NpcAnglerPresenter>();
            p.Bind(stand);
            return p;
        }

        /// <summary>A cast-path state in <paramref name="phase"/>: his aim 6 m out, no fish yet.</summary>
        private static FishingState Phase(FishingPhase phase)
            => new FishingState(phase, 0f, 0f, null, null, default, 0f, 0f, false, 0f,
                                0f, 0f, 0f, 0f, 6f, 0f, 0f, 0f, 0f);

        private T Track<T>(T made) where T : Object
        {
            _made.Add(made);
            return made;
        }
    }
}
