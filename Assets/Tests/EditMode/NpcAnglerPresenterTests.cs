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

        private sealed class FakeApproachStand : IAnglerStand, IAnglerApproach
        {
            public FishingState State;
            public float Seconds;
            public AnglerLook LookValue;
            public string FishId;
            public float SecondsToBite = float.NaN;

            public bool TryGetAnglerState(out FishingState state, out float secondsInPhase)
            {
                state = State;
                secondsInPhase = Seconds;
                return true;
            }

            public AnglerLook Look => LookValue;

            public bool TryGetApproach(out string fishId, out float weightKg, out float secondsToBite)
            {
                fishId = FishId;
                weightKg = 2f;
                secondsToBite = SecondsToBite;
                return FishId != null;
            }
        }

        private sealed class FakeSwimArt : ISwimFishArt
        {
            public Sprite Cell;
            public SwimFishPose LastPose;
            public bool TrySwimCell(string speciesId, SwimFishPose pose, float headingRadians, double seconds, out Sprite cell)
            {
                cell = speciesId == "fish.atlantic_cod" ? Cell : null;
                if (pose != SwimFishPose.Shadow) LastPose = pose;
                return cell != null;
            }
            public float PoseSeconds(SwimFishPose pose) => pose == SwimFishPose.Roll ? 0.52f : 0.72f;
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

        [Test]
        public void TheFishComingIn_IsDrawnOnlyOverTheLastOfHisWait_AndStaysWithHerThroughTheDeepFight()
        {
            ISwimFishArt previousArt = SwimFishArt.Service;
            var tex = Track(new Texture2D(2, 2));
            SwimFishArt.Service = new FakeSwimArt { Cell = Track(Sprite.Create(tex, new Rect(0, 0, 2, 2), Vector2.zero)) };
            try
            {
                AnglerLook look = Stand().LookValue;
                look.FishDrawScale = 2;
                look.Approach = new AnglerApproachLook
                {
                    Seconds = 3f, FadeInSeconds = 0.5f, FromM = 4f, OffLineDegrees = 30f, SwingM = 0.5f,
                    CircleM = 0.9f, CircleDegrees = 270f, CircleSeconds = 1f, PauseSeconds = 0.5f, TakeSeconds = 0.3f,
                    PauseBeatRate = 0.5f, TakeBeatRate = 2f, HookBlendSeconds = 0.6f, SortingOrder = -3, Alpha01 = 0.8f,
                    BodyTint = new Color(0.45f, 0.5f, 0.55f, 1f), DepthFadeM = 4f, DeepAlpha01 = 0.55f,
                    BedShadowColor = new Color(0f, 0f, 0f, 0.45f), BedShadowOffsetPerM = new Vector2(0.1f, -0.2f),
                    BedShadowMaxDepthM = 2.5f,
                };
                var stand = new FakeApproachStand { LookValue = look, State = Phase(FishingPhase.Waiting), Seconds = 1f };
                NpcAnglerPresenter p = Presenter(stand);

                p.Present(10);
                Assert.IsFalse(p.ApproachRenderer.enabled, "drawn with no fish coming");

                stand.FishId = "fish.atlantic_cod";
                stand.SecondsToBite = 5f;
                p.Present(11);
                Assert.IsFalse(p.ApproachRenderer.enabled, "drawn before her approach began");

                stand.SecondsToBite = 1f;
                p.Present(12);
                Assert.IsTrue(p.ApproachRenderer.enabled, "not drawn as she comes in");
                Assert.AreEqual(-3, p.ApproachRenderer.sortingOrder);
                Vector3 drawn = p.ApproachRenderer.transform.lossyScale;
                Assert.AreEqual(2f, drawn.x, 1e-5f, "not drawn at the look's whole-number scale");
                Assert.AreEqual(2f, drawn.y, 1e-5f);
                Assert.IsTrue(p.BedShadowRenderer.enabled, "no shadow on the bed in thin water");
                Assert.AreEqual(-4, p.BedShadowRenderer.sortingOrder, "her shadow must sit under her");

                // Her flank flashes as she turns into the circle (swim-in = 3 - 1 - 0.5 - 0.3 = 1.2 s).
                stand.SecondsToBite = 3f - 1.2f - 0.1f;
                p.Present(121);
                Assert.AreEqual(SwimFishPose.Roll, ((FakeSwimArt)SwimFishArt.Service).LastPose, "no flank flash at her turn");
                Assert.AreEqual(1f, p.ApproachRenderer.color.r, 1e-5f, "the flash is drawn as baked, untinted");

                stand.FishId = "fish.soft_shell_clam";
                p.Present(13);
                Assert.IsFalse(p.ApproachRenderer.enabled, "a species with no swim art was drawn");

                stand.FishId = null;
                stand.State = new FishingState(FishingPhase.FightDeep, 0.5f, 0.2f, "fish.atlantic_cod", "fish.atlantic_cod",
                                               default, 2f, 0.6f, false, 0.5f, 0.5f, 3f, 0f, 0f, 6f, 0f, 1f, 0f, 0.5f);
                p.Present(14);
                Assert.IsTrue(p.ApproachRenderer.enabled, "not drawn under the water in the deep fight");
                Vector3 at = p.ApproachRenderer.transform.position - p.transform.position;
                Assert.AreEqual(0.5f, at.x, 1e-4f);
                Assert.AreEqual(3f, at.y, 1e-4f, "not drawn where the fight has her");
                Assert.AreEqual(SwimFishPose.Thrash, ((FakeSwimArt)SwimFishArt.Service).LastPose, "hooked, she must thrash");

                // At the strike she is still on the bait: the hook-set never jumps her out to the fight.
                stand.Seconds = 0f;
                p.Present(141);
                Vector3 struck = p.ApproachRenderer.transform.position - p.transform.position;
                Assert.AreEqual(stand.State.CastAimX, struck.x, 0.03f);
                Assert.AreEqual(stand.State.CastAimY, struck.y, 0.03f, "she jumped off the bait at the strike");

                stand.State = Phase(FishingPhase.FightSurface);
                p.Present(15);
                Assert.IsFalse(p.ApproachRenderer.enabled, "still drawn after she surfaced");
            }
            finally { SwimFishArt.Service = previousArt; }
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

        private NpcAnglerPresenter Presenter(IAnglerStand stand)
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
