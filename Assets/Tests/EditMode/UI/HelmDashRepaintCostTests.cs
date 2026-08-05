using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The rule-7 REPAINT COST measurement for all four helm dashes, taken headlessly (ADR 0025; the
    /// merge-seat's route past "no Unity machine here", PR #410).
    ///
    /// <para><b>Why this can run in EditMode at all.</b> <see cref="DrawSurface"/> is a
    /// <c>Color32[]</c> CPU buffer; <c>Texture2D</c> appears only in <c>ToTexture</c>, the upload. On
    /// S3b the split was 4.507 ms raster / 0.282 ms upload — ~94% of the cost is pure CPU, and it is
    /// exactly the part that scales with what these two new dashes added (the Cape's 1250 cork motes
    /// and 40 grain streaks, the Novi's 4-px carbon twill). So this times the RASTER only.</para>
    ///
    /// <para><b>Why all four in one run.</b> A single figure from one machine says little. Measuring
    /// the shipped skiffs and the new wheelhouses in the same run, on the same runner, under the same
    /// conditions makes the RATIO trustworthy even though the absolute numbers move with the hardware
    /// — better evidence than comparing two numbers taken months and machines apart.</para>
    ///
    /// <para><b>What this asserts, and what it does not.</b> CI runners are noisy and shared, so the
    /// numbers are LOGGED, not gated on a tight threshold — a timing test that fails on a busy runner
    /// trains people to ignore red. The only assertion is an absurdly loose ceiling that catches a
    /// pathological regression (an accidental O(n²), an unbounded loop) and nothing else.</para>
    ///
    /// <para><b>Reading the number.</b> Unlike the fish finder's free-running scan, a dash repaints on
    /// CHANGE DETECTION — a detent, a gear, a fit change — never per frame. A raster dearer than the
    /// skiffs' is therefore paid per state change, not per frame, which is why a figure that would
    /// have been unacceptable for the sonar can be fine here.</para>
    /// </summary>
    public class HelmDashRepaintCostTests
    {
        private const int Iterations = 50;
        private const float Drive = 0.5f, Rpm = 0.555f, Fuel = 0.62f;

        /// <summary>An absurdly loose ceiling — a smoke guard, not a budget. A dash that takes a third
        /// of a second to raster is broken, not merely slow; anything under it is a number to read.</summary>
        private const double SanityCeilingMs = 300.0;

        private static double MsPer(System.Action a)
        {
            a();                                   // warm: static ramps, the baked key, JIT
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < Iterations; i++) a();
            sw.Stop();
            return sw.Elapsed.TotalMilliseconds / Iterations;
        }

        [Test]
        public void AllFourDashes_RasterCost_IsMeasuredAndLogged()
        {
            var skiff = new DrawSurface(HelmDashGeometry.W, HelmDashGeometry.H);
            var pilot = new DrawSurface(HelmDashGeometry.PilotW, HelmDashGeometry.PilotH);

            // The SHIPPED fit of both wheelhouse hulls: a depth sounder, no compass, no radar, no gps.
            var noviFit = new HelmFit(ConsoleRigKind.Novi, SounderKind.Depth, CompassMount.None, false, false);
            var capeFit = new HelmFit(ConsoleRigKind.Cape, SounderKind.Depth, CompassMount.None, false, false);

            double console = MsPer(() => ConsoleDashRender.Render(skiff, true, Drive, Rpm, Fuel));
            double sport = MsPer(() => SportDashRender.Render(skiff, true, Drive, Rpm, Fuel));
            double novi = MsPer(() => NoviDashRender.Render(pilot, in noviFit, true, Rpm, Fuel));
            double cape = MsPer(() => CapeDashRender.Render(pilot, in capeFit, true, Rpm, Fuel));

            // Night costs extra on the wheelhouses only (the gauge backlights + the ice ambient wash).
            double noviNight = MsPer(() => NoviDashRender.Render(pilot, in noviFit, true, Rpm, Fuel, true));
            double capeNight = MsPer(() => CapeDashRender.Render(pilot, in capeFit, true, Rpm, Fuel, true));

            double skiffAvg = (console + sport) / 2.0;
            double pilotAvg = (novi + cape) / 2.0;
            string report =
                $"[HelmDashRepaintCost] raster ms/repaint, avg over {Iterations}, EditMode CPU, no GPU:\n" +
                $"  console  600x510  {console:F2} ms\n" +
                $"  sport    600x510  {sport:F2} ms\n" +
                $"  novi     600x548  {novi:F2} ms   (night {noviNight:F2} ms)\n" +
                $"  cape     600x548  {cape:F2} ms   (night {capeNight:F2} ms)\n" +
                $"  pilothouse / skiff ratio = {pilotAvg / skiffAvg:F2}x  " +
                $"(skiff avg {skiffAvg:F2} ms, pilothouse avg {pilotAvg:F2} ms)\n" +
                "  NOTE: dashes repaint on change detection (detent / gear / fit), never per frame.";
            TestContext.WriteLine(report);
            Debug.Log(report);

            Assert.That(console, Is.LessThan(SanityCeilingMs), "console dash raster");
            Assert.That(sport, Is.LessThan(SanityCeilingMs), "sport dash raster");
            Assert.That(novi, Is.LessThan(SanityCeilingMs), "novi dash raster");
            Assert.That(cape, Is.LessThan(SanityCeilingMs), "cape dash raster");
        }

        /// <summary>
        /// The S4.5 flush faces, measured on the same runner in the same run — because the honest
        /// answer to "what does mounting the instruments on the dash cost?" is <b>nothing</b>, and a
        /// claim like that should be evidence, not a comment.
        ///
        /// <para><b>Why there is no third number to take.</b> Mounting an instrument moves its
        /// <c>RawImage</c> onto the dash's brow rect; it does not composite a second picture into the
        /// dash card. So the flush face costs the instrument's OWN raster (below) and nothing else:
        /// no downscale pass, no re-composite, and — the part that would actually have hurt — no dash
        /// repaint when the sonar's scan steps. A software composite would have put the finder's
        /// 4 Hz cadence in charge of the whole 600×548 card.</para>
        ///
        /// <para>Both instruments keep their shipped change keys, so these are paid at their own
        /// rates: the sounder on an LCD string change (still water ⇒ roughly never), the finder at
        /// <c>WaterfallHz</c>. Neither changed in S4.5.</para>
        /// </summary>
        [Test]
        public void TheFlushBrowInstruments_CostTheirOwnRasterAndNothingMore()
        {
            var depthSurf = new DrawSurface(DepthRigRender.W, DepthRigRender.H);
            var fishSurf = new DrawSurface(FishRigRender.W, FishRigRender.H);

            var depthState = new DepthRigState(6.4f, false, false, true, 3f, 12f, false);
            var fishState = new FishRigState(6.4f, 12f, 20f, 3f, false, false, true, true, false, false,
                                             1.0, FishRigAdjust.Range, 0.6f, true, 0.8f, 12.4f);
            var marks = new System.Collections.Generic.List<SonarMark>();

            double depth = MsPer(() => DepthRigRender.Render(depthSurf, in depthState));
            double fish = MsPer(() => FishRigRender.Render(fishSurf, in fishState, marks));

            // The mount rect itself: pure float maths per frame, and the only NEW work S4.5 adds on
            // the paint path. It is here so the claim "mounting is free" has a number under it too.
            var fit = new HelmFit(ConsoleRigKind.Novi, SounderKind.Fish, CompassMount.None, false, false);
            var card = new Rect(0f, 0f, HelmDashGeometry.PilotW, HelmDashGeometry.PilotH);
            double mount = MsPer(() =>
                HelmInstrumentMountLayout.TryBrowSounderRect(in fit, card, out _));

            string report =
                $"[HelmDashRepaintCost] brow instruments, avg over {Iterations}, EditMode CPU, no GPU:\n" +
                $"  depth sounder  {DepthRigRender.W}x{DepthRigRender.H}  {depth:F2} ms   " +
                "(repaints on an LCD string change)\n" +
                $"  fish finder    {FishRigRender.W}x{FishRigRender.H}  {fish:F2} ms   " +
                "(repaints at FishFinder.WaterfallHz)\n" +
                $"  flush mount maths                {mount:F4} ms   " +
                "(S4.5's whole added paint cost — the face is the same texture at another rect)\n" +
                "  NOTE: mounting adds NO raster and does NOT dirty the dash card.";
            TestContext.WriteLine(report);
            Debug.Log(report);

            Assert.That(depth, Is.LessThan(SanityCeilingMs), "depth sounder raster");
            Assert.That(fish, Is.LessThan(SanityCeilingMs), "fish finder raster");
            Assert.That(mount, Is.LessThan(1.0),
                        "the per-frame mount maths must stay far below a raster — if this ever " +
                        "approaches one, someone has started drawing in it");
        }
    }
}
