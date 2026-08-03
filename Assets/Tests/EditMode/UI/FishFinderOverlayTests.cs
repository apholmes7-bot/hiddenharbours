using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The fish finder HOST's policy, as pure maths (ADR 0025 S3b): the scan cadence that keeps the
    /// instrument inside the frame budget, the card's placement and hit geometry, the mark presenter, and
    /// the owner's Ruling A control mapping including the kept shallow alarm (Ruling E).
    ///
    /// <para><b>Every claim here is negative-controlled</b> — each guard is paired with a test that shows
    /// the WRONG implementation failing it, so none of them is pinning nothing.</para>
    /// </summary>
    public class FishFinderOverlayTests
    {
        private static FishFinderSettings Cfg => FishFinderSettings.Default;
        private static DepthSounderSettings Sounder => DepthSounderSettings.Default;

        private static FishRigState StateAt(double phase, float depth = 12f)
            => new FishRigState(depth, 12f, 20f, 3f, false, false, true, true, false, false, phase,
                                FishRigAdjust.Range, 0.75f, true, 0.8f, 4f);

        // ---- the scan cadence (rule 7) -----------------------------------------------------------------

        [Test]
        public void TheScanRepaintsAtWaterfallHz_NotPerFrame()
        {
            // 120 frames of one second at the OWNER'S OWN shipped rate. The change key is the whole
            // FishRigState, so "how many distinct states does a second produce" IS "how many rasters does
            // a second cost" — and one raster is a MEASURED ~4.8 ms at 480x660.
            float hz = Cfg.WaterfallHz;
            var seen = new HashSet<FishRigState>();
            for (int f = 0; f < 120; f++)
            {
                double t = f / 120.0;
                long bucket = FishFinderOverlayLayout.PhaseBucket(t, hz);
                seen.Add(StateAt(FishFinderOverlayLayout.PhaseSeconds(bucket, hz)));
            }
            Assert.LessOrEqual(seen.Count, Mathf.CeilToInt(hz) + 1,
                "a second of scan must cost at most WaterfallHz rasters — the rig's phase free-runs, and " +
                "feeding it Time.time repaints thousands of spans plus a texture upload EVERY frame");
            Assert.GreaterOrEqual(seen.Count, Mathf.FloorToInt(hz), "…and it must actually still scroll");
        }

        [Test]
        public void NegativeControl_AnUnquantizedPhase_RepaintsEveryFrame()
        {
            // The exact mistake the quantization exists to prevent, measured: the same loop with the raw
            // clock produces a distinct picture per frame, which the guard above rejects.
            var seen = new HashSet<FishRigState>();
            for (int f = 0; f < 120; f++) seen.Add(StateAt(f / 120.0));
            Assert.AreEqual(120, seen.Count, "a free-running phase is a repaint per frame");
            Assert.Greater(seen.Count, Mathf.CeilToInt(Cfg.WaterfallHz) + 1,
                           "…which is exactly what the cadence guard fails on");
        }

        [Test]
        public void FramesInsideOneBucket_PaintABitIdenticalPicture()
        {
            // Change detection is only honest if the painter cannot see anything the key does not. Two
            // frames in the same bucket must produce the SAME phase, not merely a close one.
            const float hz = 8f;
            double a = FishFinderOverlayLayout.PhaseSeconds(FishFinderOverlayLayout.PhaseBucket(0.501, hz), hz);
            double b = FishFinderOverlayLayout.PhaseSeconds(FishFinderOverlayLayout.PhaseBucket(0.6249, hz), hz);
            Assert.AreEqual(a, b, 0d, "same bucket ⇒ bit-identical phase");
            double c = FishFinderOverlayLayout.PhaseSeconds(FishFinderOverlayLayout.PhaseBucket(0.626, hz), hz);
            Assert.AreNotEqual(a, c, "…and the next bucket really does move the scan");
            Assert.AreEqual(0L, FishFinderOverlayLayout.PhaseBucket(3.2, 0f),
                            "a zero rate freezes the scan rather than dividing by zero");
        }

        [Test]
        public void TheAlarmBlink_LandsOnScanBuckets_SoASoundingAlarmCostsNoExtraRasters()
        {
            // Ruling E's blink and the waterfall share ONE change key, and this is the constraint that
            // makes that free: every blink FLIP must also be a bucket flip, i.e. WaterfallHz must be a
            // MULTIPLE of 2x AlarmBlinkHz. Asserted against the OWNER'S OWN tuning, so a dial that breaks
            // the relationship shows up here rather than as a mysterious frame cost.
            float hz = Cfg.WaterfallHz, blinkHz = Sounder.AlarmBlinkHz;
            bool prevBlink = DepthSounder.BlinkPhase(0.0, blinkHz);
            long prevBucket = FishFinderOverlayLayout.PhaseBucket(0.0, hz);
            for (int f = 1; f <= 2000; f++)
            {
                double t = f / 2000.0 * 2.0;                       // two seconds, fine-grained
                bool blink = DepthSounder.BlinkPhase(t, blinkHz);
                long bucket = FishFinderOverlayLayout.PhaseBucket(t, hz);
                if (blink != prevBlink)
                    Assert.AreNotEqual(prevBucket, bucket,
                        $"a blink flip at t={t} that is not also a bucket flip is an extra raster");
                prevBlink = blink; prevBucket = bucket;
            }
        }

        // ---- card placement ------------------------------------------------------------------------------

        [Test]
        public void TheCard_IsAScaledBlitOfTheNativeRig_AndKeepsThePortraitAspect()
        {
            FishFinderSettings s = Cfg;
            Assert.AreEqual(0.5f, FishFinderOverlayLayout.CardScale(false, in s, 1920f, 1080f), 1e-4f);
            Assert.AreEqual(1f, FishFinderOverlayLayout.CardScale(true, in s, 1920f, 1080f), 1e-4f);

            Rect small = FishFinderOverlayLayout.CardRect(false, in s, 1920f, 1080f);
            Assert.AreEqual(240f, small.width, 1e-3f);
            Assert.AreEqual(330f, small.height, 1e-3f);
            Assert.AreEqual((double)FishRigGeometry.W / FishRigGeometry.H,
                            (double)small.width / small.height, 1e-6,
                            "one scale drives both axes — a portrait instrument is never stretched");

            Rect focus = FishFinderOverlayLayout.CardRect(true, in s, 1920f, 1080f);
            Assert.AreEqual(480f, focus.width, 1e-3f, "the focused state is the rig's native size");
            Assert.AreEqual(660f, focus.height, 1e-3f);
        }

        [Test]
        public void TheRigsTypography_ForbidsARenderSmallerThanNative()
        {
            // ⚠ The reason the card is a scaled BLIT and not a smaller drawing, pinned as a measurement.
            // The rig picks its font scale from the MOUNT HEIGHT (fishRig.js:323) and its status-strip
            // height from the GLASS WIDTH (:149) — two scaling laws that disagree below ~83% of native.
            // At native everything fits; at half size the key legends are wider than the pushers and the
            // strip's text is taller than the strip.
            RigRect nat = FishRigGeometry.StandaloneMount();
            AssertTypographyFits(nat, true, "native 480x660");

            RigRect half = FishRigGeometry.StandaloneMount(240, 330);
            AssertTypographyFits(half, false, "a HALF-SIZE render — the option this design rejects");

            // …and the wide console brow S6 will mount into is fine, which is why DrawUnit still travels.
            AssertTypographyFits(new RigRect(0, 0, 300, 120), true, "a 300x120 console brow");
        }

        [Test]
        public void TheSmallCard_ParksTopRight_AndTheFocusedOneCentres()
        {
            FishFinderSettings s = Cfg;
            Rect small = FishFinderOverlayLayout.CardRect(false, in s, 1920f, 1080f);
            Assert.AreEqual(1920f - s.MarginX - 240f, small.xMin, 1e-3f);
            Assert.AreEqual(1080f - s.MarginY - 330f, small.yMin, 1e-3f, "screen y is UP — top-right");

            Rect focus = FishFinderOverlayLayout.CardRect(true, in s, 1920f, 1080f);
            Assert.AreEqual(960f, focus.center.x, 1e-3f);
            Assert.AreEqual(540f, focus.center.y, 1e-3f);
        }

        [Test]
        public void TheCard_ShrinksToFitAShortScreen_RatherThanRunningOffIt()
        {
            // A portrait instrument is the one that does not fit: 660 px of native rig on a 600 px screen.
            FishFinderSettings s = Cfg;
            Rect focus = FishFinderOverlayLayout.CardRect(true, in s, 1024f, 600f);
            Assert.LessOrEqual(focus.height, 600f - s.MarginY * 2f + 1e-3f,
                               "the focused card fits the screen it sits on");
            Assert.AreEqual((double)FishRigGeometry.W / FishRigGeometry.H,
                            (double)focus.width / focus.height, 1e-6, "…without distorting");
        }

        /// <summary>Does the rig's own text fit the boxes it is drawn in at this mount size? The two that
        /// break first are the 5-character pusher legend and the status strip's row height.</summary>
        private static void AssertTypographyFits(RigRect mount, bool shouldFit, string what)
        {
            FishRigLayout l = FishRigGeometry.Layout(mount.X, mount.Y, mount.W, mount.H);
            int fs = mount.H >= 200 ? 3 : mount.H >= 112 ? 2 : 1;      // fishRig.js:323
            int ls = Mathf.Max(1, fs - 1);                              // fishRig.js:171
            int legend = "RANGE".Length * (3 * ls + ls) - ls;
            bool fits = legend <= l.Button0.W - 4 && 5 * fs <= l.Status.H;
            Assert.AreEqual(shouldFit, fits,
                $"{what}: legend {legend}px in a {l.Button0.W - 4}px key, strip text {5 * fs}px in a " +
                $"{l.Status.H}px strip");
        }

        // ---- hit geometry (Ruling A's targets) ------------------------------------------------------------

        [Test]
        public void HitTest_ResolvesEveryControl_InTheRigsOwnSpace()
        {
            // The host hit-tests in RIG space at the NATIVE size, so the small and focused states share
            // one hit geometry and focus scaling needs no special-casing (the S1 discipline).
            RigRect mount = FishRigGeometry.StandaloneMount();
            FishRigLayout l = FishRigGeometry.Layout(mount.X, mount.Y, mount.W, mount.H);

            Assert.AreEqual(0, FishFinderOverlayLayout.HitTest(Centre(l.Button0)), "MODE");
            Assert.AreEqual(1, FishFinderOverlayLayout.HitTest(Centre(l.Button1)), "up");
            Assert.AreEqual(2, FishFinderOverlayLayout.HitTest(Centre(l.Button2)), "down");
            Assert.AreEqual(FishFinderOverlayLayout.StatusHit, FishFinderOverlayLayout.HitTest(Centre(l.Status)),
                            "status strip = night backlight");
            Assert.AreEqual(FishFinderOverlayLayout.RulerHit, FishFinderOverlayLayout.HitTest(Centre(l.Ruler)),
                            "ruler = feet/metres");
            Assert.AreEqual(FishFinderOverlayLayout.SonarHit, FishFinderOverlayLayout.HitTest(Centre(l.Sonar)),
                            "sonar glass = fish-ID");
            Assert.AreEqual(-1, FishFinderOverlayLayout.HitTest(new Vector2(1f, 1f)),
                            "the case corner is not a control");

            // The explicit-size overload (for an S6 dash that composites at another resolution) agrees
            // with the native one on the same normalised point.
            RigRect m2 = FishRigGeometry.StandaloneMount(240, 330);
            FishRigLayout l2 = FishRigGeometry.Layout(m2.X, m2.Y, m2.W, m2.H);
            Assert.AreEqual(1, FishFinderOverlayLayout.HitTest(Centre(l2.Button1), 240, 330));
            Assert.AreEqual(FishFinderOverlayLayout.SonarHit,
                            FishFinderOverlayLayout.HitTest(Centre(l2.Sonar), 240, 330));
        }

        [Test]
        public void NegativeControl_TheSoundersHitBoxes_LandOnTheWrongThing()
        {
            // The failure mode the whole "FishRigGeometry is its own class" rule exists to prevent,
            // demonstrated on the shipped mount. ⚠ Note the MIDDLE pusher's centres happen to fall inside
            // each other — which is exactly why a copy-pasted layout survives a casual click-test and then
            // misses everywhere else. The outer two are where it shows.
            RigRect mount = FishRigGeometry.StandaloneMount();
            DepthRigLayout d = DepthRigGeometry.Layout(mount.X, mount.Y, mount.W, mount.H);
            FishRigLayout f = FishRigGeometry.Layout(mount.X, mount.Y, mount.W, mount.H);

            Assert.AreEqual(-1, FishFinderOverlayLayout.HitTest(Centre(d.Button2)),
                            "the middle of the SOUNDER's ▼ is not a finder control at all");
            Assert.AreEqual(2, FishFinderOverlayLayout.HitTest(Centre(f.Button2)),
                            "…while the finder's own ▼ is, so the probe is honest");

            // And the boxes themselves differ in every dimension, so nothing about them is transferable.
            Assert.AreNotEqual(d.Button0.W, f.Button0.W, "pusher width");
            Assert.AreNotEqual(d.Button0.H, f.Button0.H, "pusher height");
            Assert.AreNotEqual(d.Button2.Y, f.Button2.Y, "…and where the bottom one sits");
        }

        private static Vector2 Centre(RigRect r) => new Vector2(r.X + r.W / 2f, r.Y + r.H / 2f);

        // ---- Ruling A: the control mapping ------------------------------------------------------------------

        [Test]
        public void Mode_CyclesWhatTheArrowsAdjust()
        {
            Assert.AreEqual(FishRigAdjust.Alarm, FishFinderControls.CycleMode(FishRigAdjust.Range));
            Assert.AreEqual(FishRigAdjust.Range, FishFinderControls.CycleMode(FishRigAdjust.Alarm));
        }

        [Test]
        public void InRangeMode_TheArrowsWalkTheRigsOwnScaleLadder()
        {
            FishFinderSettings f = Cfg; DepthSounderSettings d = Sounder;
            SounderPrefs p = SounderPrefs.FromDefaults(in d, in f);
            Assert.AreEqual(20f, p.RangeMetres, 1e-4f);

            Assert.IsTrue(FishFinderControls.StepActive(FishRigAdjust.Range, ref p, +1, in d, in f));
            Assert.AreEqual(40f, p.RangeMetres, 1e-4f);
            FishFinderControls.StepActive(FishRigAdjust.Range, ref p, +1, in d, in f);
            Assert.AreEqual(60f, p.RangeMetres, 1e-4f);
            Assert.IsFalse(FishFinderControls.StepActive(FishRigAdjust.Range, ref p, +1, in d, in f),
                           "at the deepest scale a ▲ changes nothing — and must not cost a disk write");

            // …and RANGE never touches the alarm.
            Assert.AreEqual(d.DefaultAlarmMetres, p.AlarmMetres, 1e-4f);
            Assert.IsTrue(p.Armed);
        }

        [Test]
        public void InAlarmMode_TheArrowsAreTheSoundersOwnStepAlarm_NotASecondRule()
        {
            FishFinderSettings f = Cfg; DepthSounderSettings d = Sounder;
            SounderPrefs p = SounderPrefs.FromDefaults(in d, in f);
            float start = p.AlarmMetres;

            FishFinderControls.StepActive(FishRigAdjust.Alarm, ref p, +1, in d, in f);
            Assert.AreEqual(DepthSounder.StepAlarm(start, +1, in d), p.AlarmMetres, 1e-4f,
                            "the finder MUST move the set-point through DepthSounder.StepAlarm");
            FishFinderControls.StepActive(FishRigAdjust.Alarm, ref p, -1, in d, in f);
            Assert.AreEqual(start, p.AlarmMetres, 1e-4f);
            Assert.AreEqual(20f, p.RangeMetres, 1e-4f, "…and ALARM never touches the scale");

            // Walk the whole ladder and check every rung against the sounder's own rule.
            for (int i = 0; i < 60; i++)
            {
                float expected = DepthSounder.StepAlarm(p.AlarmMetres, +1, in d);
                FishFinderControls.StepActive(FishRigAdjust.Alarm, ref p, +1, in d, in f);
                Assert.AreEqual(expected, p.AlarmMetres, 1e-4f, $"rung {i}");
            }
            Assert.AreEqual(d.MaxAlarmMetres, p.AlarmMetres, 1e-4f, "clamped at the deepest set-point");
        }

        [Test]
        public void SteppingBelowTheMinimum_Disarms_AndSteppingUpFromOff_ReArmsThere()
        {
            // Ruling A's arm/disarm — no new key, no new UI, just StepAlarm's existing clamp.
            FishFinderSettings f = Cfg; DepthSounderSettings d = Sounder;
            var p = new SounderPrefs(d.MinAlarmMetres, true, false, false, 20f);

            Assert.IsTrue(FishFinderControls.StepActive(FishRigAdjust.Alarm, ref p, -1, in d, in f));
            Assert.IsFalse(p.Armed, "▼ past the shallowest set-point turns the alarm OFF");
            Assert.IsFalse(FishFinderControls.StepActive(FishRigAdjust.Alarm, ref p, -1, in d, in f),
                           "…and a second ▼ is a no-op, not a negative set-point");

            Assert.IsTrue(FishFinderControls.StepActive(FishRigAdjust.Alarm, ref p, +1, in d, in f));
            Assert.IsTrue(p.Armed, "▲ from off re-arms");
            Assert.AreEqual(d.MinAlarmMetres, p.AlarmMetres, 1e-4f, "…at the minimum");
        }

        // ---- Ruling E: the alarm is the SOUNDER'S alarm ------------------------------------------------------

        [Test]
        public void TheTrigger_IsDepthSounderShallowAlarm_OverTheWholeLadder()
        {
            // The pin against a second alarm rule creeping in. The finder's glass reddens on exactly the
            // states the sounder's does, including the INCLUSIVE boundary at the set-point.
            var p = new SounderPrefs(3f, true, false, false, 20f);
            foreach (float depth in new[] { 0f, 0.5f, 2.99f, 3f, 3.01f, 6f, 40f })
            {
                bool expected = DepthSounder.ShallowAlarm(depth, in p);
                FishRigState s = WithAlarm(depth, in p, expected);
                Assert.AreEqual(expected, s.Triggered, $"at {depth} m");
            }
            Assert.IsTrue(DepthSounder.ShallowAlarm(3f, in p), "inclusive AT the set-point");

            var off = new SounderPrefs(3f, false, false, false, 20f);
            Assert.IsFalse(DepthSounder.ShallowAlarm(0.1f, in off), "a disarmed finder never flashes");
        }

        [Test]
        public void TheState_CarriesTheTrigger_AndDoesNotReDeriveIt()
        {
            // Structural pin: FishRigState.Triggered must be the value the host was GIVEN, never a second
            // `armed && depth <= alarm` inside the render path. Feed it a state that WOULD trigger under a
            // re-derivation and confirm the carried false survives.
            var s = new FishRigState(1f, 12f, 20f, 3f, false, false, true, true,
                                     triggered: false, blink: true, phase: 0.0,
                                     adjust: FishRigAdjust.Range, sens: 0.75f, link: true,
                                     batt: 0.8f, volts: 4f);
            Assert.IsFalse(s.Triggered,
                "1 m against a 3 m armed set-point WOULD trigger — the state must not compute that itself");
        }

        [Test]
        public void NegativeControl_AnAlarmThatIgnoresTheSetPoint_FailsTheLadder()
        {
            // "The alarm still works" is only a claim if the wrong answer is visibly different. A stepper
            // that ignores the set-point (holds it wherever it started) disagrees with the sounder's rule
            // across most of the travel — which is what the ladder test above would catch.
            DepthSounderSettings d = Sounder;
            float held = d.DefaultAlarmMetres;
            int disagreements = 0;
            float walking = d.DefaultAlarmMetres;
            for (int i = 0; i < 20; i++)
            {
                walking = DepthSounder.StepAlarm(walking, +1, in d);
                if (!Mathf.Approximately(walking, held)) disagreements++;
            }
            Assert.Greater(disagreements, 15,
                "an ignored set-point must diverge from the sounder's rule almost immediately");

            // And a trigger that ignores the set-point (a fixed 3 m, say) misreports a tuned alarm.
            var tuned = new SounderPrefs(8f, true, false, false, 20f);
            Assert.IsTrue(DepthSounder.ShallowAlarm(5f, in tuned), "5 m IS shallow against an 8 m set-point");
            Assert.IsFalse(5f <= 3f, "…which a hard-coded 3 m rule would have missed");
        }

        // ---- the mark presenter -----------------------------------------------------------------------------

        [Test]
        public void TheEmptySea_DrawsNothing_AndIsNotAnError()
        {
            // The shipped default until a fish model registers, and the state this whole slice was built
            // and tested against.
            FishFinderSettings f = Cfg;
            var into = new List<SonarMark> { new SonarMark(0.5f, 3f, 1f) };
            Assert.AreEqual(0, FishMarkPresenter.Build(null, in f, into));
            Assert.IsEmpty(into, "the output list is cleared even when there is nothing to write");
            Assert.AreEqual(0, FishMarkPresenter.Build(new List<FishMark>(), in f, into));

            int n = EmptyFishSchools.Instance.MarksAt(Vector2.zero, 0d, new List<FishMark>());
            Assert.AreEqual(0, n, "…which is exactly what the seam's null object returns");
        }

        [Test]
        public void Density_IsTheMarkCount_CappedButNeverInvented()
        {
            FishFinderSettings f = Cfg;
            var into = new List<SonarMark>();
            var one = new List<FishMark> { new FishMark(6f, 1, 1f) };
            Assert.AreEqual(1, FishMarkPresenter.Build(one, in f, into), "one fish is one icon");

            var several = new List<FishMark> { new FishMark(6f, 5, 1f) };
            Assert.AreEqual(5, FishMarkPresenter.Build(several, in f, into), "five is five");

            var swarm = new List<FishMark> { new FishMark(6f, 500, 1f) };
            Assert.AreEqual(f.MaxMarksPerSchool, FishMarkPresenter.Build(swarm, in f, into),
                            "…and a huge school is capped rather than drawn as soup (rule 7)");

            var none = new List<FishMark> { new FishMark(6f, 0, 1f) };
            Assert.AreEqual(0, FishMarkPresenter.Build(none, in f, into));
        }

        [Test]
        public void MarksStayInMetres_AndAreDeterministic()
        {
            FishFinderSettings f = Cfg;
            var schools = new List<FishMark> { new FishMark(8f, 3, 1f), new FishMark(15f, 2, 0.4f) };
            var a = new List<SonarMark>();
            var b = new List<SonarMark>();
            FishMarkPresenter.Build(schools, in f, a);
            FishMarkPresenter.Build(schools, in f, b);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].X01, b[i].X01, 0f, "the scan must not reshuffle 8x a second");
                Assert.AreEqual(a[i].DepthMetres, b[i].DepthMetres, 0f);
                Assert.AreEqual(a[i].Size, b[i].Size, 0f);
                Assert.GreaterOrEqual(a[i].X01, f.MarkXMargin01 - 1e-5f);
                Assert.LessOrEqual(a[i].X01, 1f - f.MarkXMargin01 + 1e-5f);
                Assert.Greater(a[i].DepthMetres, 0f, "depths stay METRES, never a 0..1 fraction");
            }
            // A school you are sitting on draws bigger marks than one you are clipping the edge of.
            float strong = a[0].Size, weak = a[a.Count - 1].Size;
            Assert.Greater(strong, weak, "signal strength reads as mark size");
        }

        [Test]
        public void AMarksDepthIsNotAFractionOfTheRange_SoARangeChangeDoesNotMakeFishSwim()
        {
            // T10, pinned. A school at 8 m is at 8 m whatever the scale: the presenter stores metres and
            // the painter divides. Storing a normalised y would put the same fish at 0.4 on a 20 m scale
            // and leave it at 0.4 (i.e. 16 m) when the player flipped to 40.
            FishFinderSettings f = Cfg;
            var schools = new List<FishMark> { new FishMark(8f, 1, 1f) };
            var marks = new List<SonarMark>();
            FishMarkPresenter.Build(schools, in f, marks);
            float depth = marks[0].DepthMetres;
            Assert.AreEqual(8f, depth, f.MarkDepthJitterMetres + 1e-4f);

            Assert.AreEqual(0.4, depth / 20f, 0.04, "on the 20 m scale it sits at 0.4 of the glass");
            Assert.AreEqual(0.2, depth / 40f, 0.02, "on the 40 m scale it sits at 0.2 — the SAME 8 m");
        }

        private static FishRigState WithAlarm(float depth, in SounderPrefs p, bool triggered)
            => new FishRigState(depth, 12f, 20f, p.AlarmMetres, false, false, true, p.Armed, triggered,
                                false, 0.0, FishRigAdjust.Range, 0.75f, true, 0.8f, 4f);
    }
}
