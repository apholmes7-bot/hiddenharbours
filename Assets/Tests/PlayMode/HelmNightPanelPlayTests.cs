using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The night panel's WIRING, on the real paint path (ADR 0025, the boat-UI arc): does the shipped
    /// compositor actually read the clock, and does it pay for dusk exactly once?
    ///
    /// <para><b>Why this test exists at all.</b> Every dash renderer in the fleet has carried a
    /// working <c>night</c> parameter since S4, and the game still had no night helm — because
    /// <see cref="HelmDashController"/> called them all with <c>false</c>. That is a defect no
    /// renderer test can see: both sides of it draw a perfectly correct day face. So this drives
    /// <see cref="HelmDashController.UpdateAndPaint"/> itself, through its own texture upload, and
    /// asserts on what the card SHOWS.</para>
    ///
    /// <para><b>Why PlayMode.</b> <c>UpdateAndPaint</c> ends in <c>DrawSurface.ToTexture</c> — a real
    /// <c>Texture2D.Apply</c>. The pure raster half of the feature is asserted headlessly in
    /// <c>HelmNightPanelTests</c>; only the parts that genuinely need the upload path are here.
    /// Nothing below counts frames as time: the clock is set outright and the calls are ordered, so
    /// there is no duration bet anywhere in this file.</para>
    /// </summary>
    public class HelmNightPanelPlayTests
    {
        private sealed class FakeClock : IGameClock
        {
            public double TotalSeconds => 0;
            public GameTime Now => default;
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay { get; set; } = 12f;
            public float DayFraction => HourOfDay / 24f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
            public void SeekTo(double totalSeconds) { }
        }

        /// <summary>A helm parked at a steady state: nothing but the clock moves in these tests, so
        /// any repaint they observe can only have come from the panel lights.</summary>
        private sealed class SteadyHelm : IHelmControl
        {
            public bool HasHelm => true;

            /// <summary>These tests DRAW this helm, and the panel only ever draws the one the player
            /// is at — so the presentation answer is true here for the same reason the engine one is.
            /// (The two are separate questions on the real relay: HasHelm is "an engine hull is being
            /// driven", IsPlayerHelm is "by her".)</summary>
            public bool IsPlayerHelm => true;
            public HelmControlStyle Style => HelmControlStyle.Lever;

            // Steady, like everything else on this helm: no hook, nothing to light, so a repaint these
            // tests observe still can only have come from the panel lights.
            public bool HasAnchor => false;
            public AnchorState AnchorState => AnchorState.Stowed;
            public void ToggleAnchor() { }
            public HelmLeverFinish LeverFinish => HelmLeverFinish.Graphite;
            public HelmWheelRim WheelRim => HelmWheelRim.Rubber;
            public HelmFit Fit { get; set; }
            public float Drive => 0.5f;
            public float Steer => 0f;
            public void StepAhead() { }
            public void StepAstern() { }
            public void SetNeutral() { }
            public void DragDrive(float sig) { }
            public void EndDrag() { }
            public bool SteerDragActive => false;
            public void DragSteer(float steer) { }
            public void EndSteerDrag() { }
        }

        private GameObject _go;
        private Texture2D _texture;

        [SetUp]
        public void SetUp() => GameServices.Reset();

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            if (_go != null) Object.DestroyImmediate(_go);
            if (_texture != null) Object.DestroyImmediate(_texture);
            _go = null;
            _texture = null;
        }

        private RawImage NewImage()
        {
            _go = new GameObject("NightPanelTestImage");
            return _go.AddComponent<RawImage>();
        }

        [Test]
        public void TheDash_TakesItsPanelLightFromTheClock_AndPaysForDuskExactlyOnce()
        {
            var clock = new FakeClock { HourOfDay = 12f };
            GameServices.Clock = clock;

            var helm = new SteadyHelm
            {
                Fit = new HelmFit(ConsoleRigKind.Console, SounderKind.Depth, CompassMount.Dome,
                                  false, false),
            };
            var ctl = new HelmDashController();
            RawImage image = NewImage();

            // Noon: the first paint always repaints (nothing is shown yet) and shows the DAY face.
            Assert.That(ctl.UpdateAndPaint(helm, helm.Fit, 235f, 0.016f, false, ref _texture, image),
                        Is.True, "the first paint must draw");
            Assert.That(ctl.NightShown, Is.False, "noon is not night");

            // A second call with nothing changed must cost nothing — the baseline the dusk assertion
            // below is measured against.
            Assert.That(ctl.UpdateAndPaint(helm, helm.Fit, 235f, 0.016f, false, ref _texture, image),
                        Is.False, "a steady frame must not repaint");

            // Dusk. The ONLY thing that moved is the hour.
            clock.HourOfDay = 22f;
            Assert.That(ctl.UpdateAndPaint(helm, helm.Fit, 235f, 0.016f, false, ref _texture, image),
                        Is.True, "crossing dusk must repaint the dash");
            Assert.That(ctl.NightShown, Is.True,
                        "the compositor must light the panel from the clock — the S4 defect was a " +
                        "hard-coded false here, and it looked exactly like a correct day face");

            // Deeper into the night: the hour keeps moving, the BOOLEAN does not. This is the
            // "keyed on the night boolean, never on continuous time" criterion, and it is the
            // assertion that fails if anyone keys the repaint on the hour itself.
            clock.HourOfDay = 22.5f;
            Assert.That(ctl.UpdateAndPaint(helm, helm.Fit, 235f, 0.016f, false, ref _texture, image),
                        Is.False, "time passing at night must not repaint");
            clock.HourOfDay = 3f;
            Assert.That(ctl.UpdateAndPaint(helm, helm.Fit, 235f, 0.016f, false, ref _texture, image),
                        Is.False, "still night after midnight — still no repaint");

            // Dawn: one more flip, one more repaint, back to the day face.
            clock.HourOfDay = 7f;
            Assert.That(ctl.UpdateAndPaint(helm, helm.Fit, 235f, 0.016f, false, ref _texture, image),
                        Is.True, "dawn must repaint");
            Assert.That(ctl.NightShown, Is.False, "and put the day face back");
        }

        [Test]
        public void EveryHullInTheFleet_LightsItsPanel_NotJustTheOnesPortedFirst()
        {
            // S2a ported the skiffs, S4 the wheelhouses, and only the wheelhouses ever got a night
            // face wired. Walk all four rigs through the same dusk so a hull can never be quietly
            // left on the day path again.
            var clock = new FakeClock();
            GameServices.Clock = clock;
            RawImage image = NewImage();

            foreach (ConsoleRigKind rig in new[]
                     {
                         ConsoleRigKind.Console, ConsoleRigKind.Sport,
                         ConsoleRigKind.Novi, ConsoleRigKind.Cape,
                     })
            {
                var helm = new SteadyHelm
                {
                    Fit = new HelmFit(rig, SounderKind.Depth, CompassMount.None, false, false),
                };
                var ctl = new HelmDashController();

                clock.HourOfDay = 12f;
                ctl.UpdateAndPaint(helm, helm.Fit, 235f, 0.016f, false, ref _texture, image);
                Assert.That(ctl.NightShown, Is.False, $"{rig} at noon");

                clock.HourOfDay = 21f;
                Assert.That(ctl.UpdateAndPaint(helm, helm.Fit, 235f, 0.016f, false, ref _texture, image),
                            Is.True, $"{rig} must repaint at dusk");
                Assert.That(ctl.NightShown, Is.True, $"{rig} must light its panel");
            }
        }

        // ---- NEGATIVE CONTROL ------------------------------------------------------------------------

        [Test]
        public void NegativeControl_WithNoClockRegistered_TheHelmStaysOnItsDayFace()
        {
            // The assertions above would all pass against a controller that simply returned true and
            // reported night unconditionally. This is the other end: no clock at all — the boot frame
            // and every test rig that never registers one — must leave the panel dark, and must not
            // throw doing it.
            GameServices.Clock = null;

            var helm = new SteadyHelm
            {
                Fit = new HelmFit(ConsoleRigKind.Console, SounderKind.Depth, CompassMount.None,
                                  false, false),
            };
            var ctl = new HelmDashController();
            RawImage image = NewImage();

            Assert.That(ctl.UpdateAndPaint(helm, helm.Fit, 0f, 0.016f, false, ref _texture, image),
                        Is.True, "first paint");
            Assert.That(ctl.NightShown, Is.False, "no clock is not night");
            Assert.That(ctl.UpdateAndPaint(helm, helm.Fit, 0f, 0.016f, false, ref _texture, image),
                        Is.False, "and it must settle, not flap between faces");
        }
    }
}
