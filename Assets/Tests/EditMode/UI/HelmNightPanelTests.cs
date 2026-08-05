using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The NIGHT PANEL's two halves (ADR 0025, the boat-UI arc): the DECISION —
    /// <see cref="HelmPanelLight"/> is the project's one dusk rule and not a second copy of it — and
    /// the FACES — every dash chrome in the fleet actually paints something different when the panel
    /// lights come on.
    ///
    /// <para><b>Why the "it changed" assertions are the load-bearing ones.</b> The bug this slice
    /// fixes was never a wrong colour: it was a <c>night</c> argument that existed, compiled, and was
    /// passed <c>false</c> — twice over. <see cref="SportDashRender"/> took the parameter and never
    /// read it, and the runtime handed every renderer a literal <c>false</c>. Both states pass any
    /// test that only checks the day face, so the guard here is per-hull and per-REGION: the dials
    /// must warm, and the panel around them must move only on the hulls whose rig authors a panel
    /// ramp. A renderer that quietly drops <c>night</c> again goes red on the hull that dropped it.</para>
    ///
    /// <para>Pure <see cref="DrawSurface"/> throughout — a <c>Color32[]</c> CPU buffer, no
    /// <c>Texture2D</c>, no canvas — so this runs on a headless runner with no graphics device. The
    /// controller's own clock wiring needs the upload path and lives in PlayMode
    /// (<c>HelmNightPanelPlayTests</c>).</para>
    /// </summary>
    public class HelmNightPanelTests
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

        // The proof state, matching DashProofEyeball's: running, half ahead, tank full.
        private const float Drive = 0.5f, Rpm = 0.555f, Fuel = 1f;

        private static DrawSurface Skiff() => new DrawSurface(HelmDashGeometry.W, HelmDashGeometry.H);
        private static DrawSurface Pilot() => new DrawSurface(HelmDashGeometry.PilotW, HelmDashGeometry.PilotH);

        private static void RenderSkiff(DrawSurface s, bool sport, bool night)
        {
            if (sport) SportDashRender.Render(s, true, Drive, Rpm, Fuel, night);
            else ConsoleDashRender.Render(s, true, Drive, Rpm, Fuel, night);
        }

        private static void RenderPilot(DrawSurface s, bool cape, bool night)
        {
            var fit = new HelmFit(cape ? ConsoleRigKind.Cape : ConsoleRigKind.Novi,
                                  SounderKind.Depth, CompassMount.None, false, false);
            if (cape) CapeDashRender.Render(s, in fit, true, Rpm, Fuel, night);
            else NoviDashRender.Render(s, in fit, true, Rpm, Fuel, night);
        }

        /// <summary>How many pixels differ between two same-sized surfaces.</summary>
        private static int DiffCount(DrawSurface a, DrawSurface b)
        {
            int n = 0;
            for (int i = 0; i < a.Pixels.Length; i++)
            {
                Color32 p = a.Pixels[i], q = b.Pixels[i];
                if (p.r != q.r || p.g != q.g || p.b != q.b || p.a != q.a) n++;
            }
            return n;
        }

        /// <summary>Mean warmth (red minus blue) over a disc — the measurable signature of an amber
        /// backlight, and one that survives whether the rig lights black glass additively or tints a
        /// white dial source-over.</summary>
        private static double Warmth(DrawSurface s, int cx, int cy, int rad)
        {
            double sum = 0.0;
            int n = 0;
            for (int y = cy - rad; y <= cy + rad; y++)
            {
                if (y < 0 || y >= s.Height) continue;
                for (int x = cx - rad; x <= cx + rad; x++)
                {
                    if (x < 0 || x >= s.Width) continue;
                    int dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy > rad * rad) continue;
                    Color32 p = s.Pixels[y * s.Width + x];
                    sum += p.r - p.b;
                    n++;
                }
            }
            return n == 0 ? 0.0 : sum / n;
        }

        // ---- the decision --------------------------------------------------------------------------

        [Test]
        public void PanelLight_IsTheProjectsOneDuskRule_NotASecondCopyOfIt()
        {
            // Every hour on the half hour must agree with WatchFaceState — the rule the diegetic
            // watch's backlight and the cottage windows already run on. If a literal ever creeps in
            // here, this diverges.
            var clock = new FakeClock();
            for (int half = 0; half < 48; half++)
            {
                float hour = half * 0.5f;
                clock.HourOfDay = hour;
                Assert.That(HelmPanelLight.IsNight(clock), Is.EqualTo(WatchFaceState.NightAt(hour)),
                            $"the panel light must be the shared rule at {hour:F1}h");
            }
        }

        [Test]
        public void PanelLight_LightsAtDusk_AndGoesOutAtDayStart()
        {
            var clock = new FakeClock();
            // The canon thresholds (WatchFaceState 06:00 / 19:00), pinned at both edges so a moved
            // boundary is a visible test change and never a silent one.
            clock.HourOfDay = 5.99f; Assert.That(HelmPanelLight.IsNight(clock), Is.True, "before dawn");
            clock.HourOfDay = 6f; Assert.That(HelmPanelLight.IsNight(clock), Is.False, "day starts AT 06:00");
            clock.HourOfDay = 12f; Assert.That(HelmPanelLight.IsNight(clock), Is.False, "noon");
            clock.HourOfDay = 18.99f; Assert.That(HelmPanelLight.IsNight(clock), Is.False, "before dusk");
            clock.HourOfDay = 19f; Assert.That(HelmPanelLight.IsNight(clock), Is.True, "night starts AT 19:00");
            clock.HourOfDay = 23.5f; Assert.That(HelmPanelLight.IsNight(clock), Is.True, "late");
        }

        [Test]
        public void PanelLight_WithNoClock_ReadsDay_RatherThanThrowing()
        {
            // The overlay hosts are self-installing singletons: they run on a boot frame before the
            // sim registers, and in test rigs that never register one at all.
            Assert.That(HelmPanelLight.IsNight(null), Is.False);
        }

        // ---- the faces -----------------------------------------------------------------------------

        [Test]
        public void EveryDashChrome_PaintsADifferentFace_WhenThePanelLights()
        {
            // The whole-fleet guard. Before this slice the two skiffs would have failed it: the
            // console's night face was partly ported and the sport's not at all.
            var day = Skiff();
            var night = Skiff();

            RenderSkiff(day, sport: false, night: false);
            RenderSkiff(night, sport: false, night: true);
            Assert.That(DiffCount(day, night), Is.GreaterThan(1000), "console dash has no night face");

            RenderSkiff(day, sport: true, night: false);
            RenderSkiff(night, sport: true, night: true);
            Assert.That(DiffCount(day, night), Is.GreaterThan(1000), "sport dash has no night face");

            var pday = Pilot();
            var pnight = Pilot();
            RenderPilot(pday, cape: false, night: false);
            RenderPilot(pnight, cape: false, night: true);
            Assert.That(DiffCount(pday, pnight), Is.GreaterThan(1000), "novi dash has no night face");

            RenderPilot(pday, cape: true, night: false);
            RenderPilot(pnight, cape: true, night: true);
            Assert.That(DiffCount(pday, pnight), Is.GreaterThan(1000), "cape dash has no night face");
        }

        [Test]
        public void TheGaugeDials_WarmUnderTheBacklight_OnEveryHull()
        {
            // The backlight is amber on all four (ice-blue on the Novi, which is COOLER, so that hull
            // is asserted in its own direction). Measured inside the dial, clear of the bezel.
            const int r = 40;
            int sx = HelmDashGeometry.RpmCx, sy = HelmDashGeometry.RpmCy + HelmDashGeometry.TOPPAD;
            int px = HelmDashGeometry.PilotRpmCx, py = HelmDashGeometry.PilotRpmCy + HelmDashGeometry.PilotTOPPAD;

            var day = Skiff();
            var night = Skiff();

            RenderSkiff(day, sport: false, night: false);
            RenderSkiff(night, sport: false, night: true);
            Assert.That(Warmth(night, sx, sy, r), Is.GreaterThan(Warmth(day, sx, sy, r) + 10.0),
                        "the console's RPM dial must take its amber backlight (consoleRig.js:407)");

            RenderSkiff(day, sport: true, night: false);
            RenderSkiff(night, sport: true, night: true);
            Assert.That(Warmth(night, sx, sy, r), Is.GreaterThan(Warmth(day, sx, sy, r) + 10.0),
                        "the sport's white dial must take its amber backlight (sportRig.js:360)");

            var pday = Pilot();
            var pnight = Pilot();
            RenderPilot(pday, cape: true, night: false);
            RenderPilot(pnight, cape: true, night: true);
            Assert.That(Warmth(pnight, px, py, r), Is.GreaterThan(Warmth(pday, px, py, r) + 10.0),
                        "the Cape's warm lamp (capeRig.js:461)");

            RenderPilot(pday, cape: false, night: false);
            RenderPilot(pnight, cape: false, night: true);
            Assert.That(Warmth(pnight, px, py, r), Is.LessThan(Warmth(pday, px, py, r) - 5.0),
                        "the Novi's backlight is ICE, not amber — it must go COOLER (noviRig.js:208)");
        }

        [Test]
        public void ThePanelRamp_IsAuthoredOnTheConsoleAndNotOnTheSport()
        {
            // The two skiffs share geometry and diverge only in skin, and their rigs disagree about
            // night on purpose: the console drops its WHOLE face a palette step (consoleRig.js:58,
            // D = -0.17) and washes it amber (js:382); the sport authors neither and lights only its
            // dials (js:360 is the entire night branch). Each half is the other's control — one
            // catches a dropped ramp, the other catches an invented one.
            //
            // A window of bare face, card space: between the switch panel (ends x 164) and the
            // steering column (starts x 291), below the dials (end y 252) and above the sole step
            // (starts y 438). Nothing but the face's own paint is in it on either hull.
            const int x0 = 180, y0 = 340, w = 100, h = 60;

            var day = Skiff();
            var night = Skiff();

            RenderSkiff(day, sport: false, night: false);
            RenderSkiff(night, sport: false, night: true);
            Assert.That(RegionDiff(day, night, x0, y0, w, h), Is.GreaterThan(w * h / 2),
                        "the console's face must drop a palette step at night");

            RenderSkiff(day, sport: true, night: false);
            RenderSkiff(night, sport: true, night: true);
            Assert.That(RegionDiff(day, night, x0, y0, w, h), Is.EqualTo(0),
                        "sportRig.js authors NO panel ramp — inventing one is not porting it");
        }

        private static int RegionDiff(DrawSurface a, DrawSurface b, int x0, int y0, int w, int h)
        {
            int n = 0;
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                {
                    int i = y * a.Width + x;
                    Color32 p = a.Pixels[i], q = b.Pixels[i];
                    if (p.r != q.r || p.g != q.g || p.b != q.b || p.a != q.a) n++;
                }
            return n;
        }

        // ---- NEGATIVE CONTROL ----------------------------------------------------------------------

        [Test]
        public void NegativeControl_TheWarmthProbeAndTheRegionProbe_CanBothFail()
        {
            // Both instruments above are differential, so a probe that cannot tell two pictures apart
            // would make every assertion above vacuously true. Prove each can register a NEGATIVE:
            // the same picture against itself must read zero difference and zero warmth gain.
            var a = Skiff();
            var b = Skiff();
            RenderSkiff(a, sport: false, night: true);
            RenderSkiff(b, sport: false, night: true);

            Assert.That(DiffCount(a, b), Is.EqualTo(0),
                        "the render must be deterministic, or every diff above is noise");
            int sx = HelmDashGeometry.RpmCx, sy = HelmDashGeometry.RpmCy + HelmDashGeometry.TOPPAD;
            Assert.That(Warmth(a, sx, sy, 40), Is.EqualTo(Warmth(b, sx, sy, 40)).Within(1e-9),
                        "the warmth probe must be stable across identical renders");

            // And that the warmth probe is measuring the DIAL, not the whole canvas: a window over
            // the untouched sport gelcoat must NOT move at night, or the +10 margins above could be
            // satisfied by any wash anywhere.
            var day = Skiff();
            var night = Skiff();
            RenderSkiff(day, sport: true, night: false);
            RenderSkiff(night, sport: true, night: true);
            Assert.That(Warmth(night, 230, 370, 18), Is.EqualTo(Warmth(day, 230, 370, 18)).Within(1e-9),
                        "the sport's gelcoat is not lit — a probe that moves here is measuring the " +
                        "wrong thing (same bare-face window as the region probe)");
        }
    }
}
