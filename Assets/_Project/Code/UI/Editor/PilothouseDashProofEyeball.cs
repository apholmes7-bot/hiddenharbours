using System.IO;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI.Editor
{
    /// <summary>
    /// Bakes the OWNER EYEBALL PROOFS for the S4 composed PILOTHOUSE dashes (Novi + Cape Islander),
    /// composited exactly as the runtime compositor mounts them: chrome → compass → grabbable wheel →
    /// binnacle lever. Written to <c>docs/art/proofs/</c> (outside Assets/ on purpose — a proof for the
    /// PR and the owner, not game content). Compare against the rigs' own previews at the same signals.
    ///
    /// <para>Sheets baked, per hull:</para>
    /// <list type="bullet">
    /// <item><c>*-dash-csharp.png</c> — the SHIPPED fit (depth sounder, no compass, no radar, no gps):
    /// the state the player actually gets, and the one that shows the two empty mounts.</item>
    /// <item><c>*-dash-night-csharp.png</c> — the same, backlit.</item>
    /// <item><c>*-dash-dome-csharp.png</c> / <c>*-dash-flush-csharp.png</c> — both compass mounts. The
    /// dome takes the crown and displaces the centre mount; the FLUSH unit renders here for the first
    /// time in the project (these two hulls are the first that can carry one).</item>
    /// <item><c>*-dash-sounder-csharp.png</c> / <c>*-dash-finder-csharp.png</c> — the brow with the
    /// depth sounder and with the colour finder in it, the S3b handover as it lands on these hulls.
    /// NOTE: at runtime the sounder still hosts on its OWN card (SounderOverlayHost); mounting it into
    /// the dash brow is S6. These two sheets composite it into the brow box so the fit can be judged
    /// before that slice — they show the destination, not today's screen.</item>
    /// </list>
    ///
    /// <para>Also the rule-7 PROFILE harness (ADR 0025's measurement gate): it stopwatches each dash's
    /// chrome repaint, the compose and the upload over a steady-state loop and logs the numbers the PR
    /// body carries. These are the LARGEST dashes in the fleet — 600×548 against the skiffs' 600×510 —
    /// so the comparison against S2a's console/sport figures is the point of the run.</para>
    /// </summary>
    public static class PilothouseDashProofEyeball
    {
        private const string OutDir = "docs/art/proofs";

        // The proof state: every instrument off its rest pose (the S2a convention, same signals).
        private const float Drive = 0.5f;
        private const float Steer = -0.35f;
        private const float Heading = 235f;
        private const float Fuel = 0.62f;      // off the stop, so the needle and the arc both read

        [MenuItem("Hidden Harbours/Dev/Bake Pilothouse Dash Proofs (novi + cape) + profile")]
        public static void Bake()
        {
            foreach (ConsoleRigKind rig in new[] { ConsoleRigKind.Novi, ConsoleRigKind.Cape })
            {
                string slug = rig == ConsoleRigKind.Novi ? "novi" : "cape";
                HelmLeverFinish finish = HelmLeverFinish.Chrome;   // both rigs spec the chrome lever
                HelmWheelRim rim = rig == ConsoleRigKind.Novi ? HelmWheelRim.Steel : HelmWheelRim.Rubber;

                HelmFit shipped = new HelmFit(rig, SounderKind.Depth, CompassMount.None, false, false);
                BakeOne(rig, shipped, $"{slug}-dash-csharp.png", finish, rim, night: false, brow: Brow.None);
                BakeOne(rig, shipped, $"{slug}-dash-night-csharp.png", finish, rim, night: true, brow: Brow.None);

                BakeOne(rig, new HelmFit(rig, SounderKind.Depth, CompassMount.Dome, false, false),
                        $"{slug}-dash-dome-csharp.png", finish, rim, night: false, brow: Brow.None);
                BakeOne(rig, new HelmFit(rig, SounderKind.Depth, CompassMount.Flush, false, false),
                        $"{slug}-dash-flush-csharp.png", finish, rim, night: false, brow: Brow.None);

                BakeOne(rig, shipped, $"{slug}-dash-sounder-csharp.png", finish, rim, night: false,
                        brow: Brow.Depth);
                BakeOne(rig, new HelmFit(rig, SounderKind.Fish, CompassMount.None, false, false),
                        $"{slug}-dash-finder-csharp.png", finish, rim, night: false, brow: Brow.Fish);
            }
            Profile();
        }

        private enum Brow { None, Depth, Fish }

        private static void BakeOne(ConsoleRigKind rig, HelmFit fit, string file, HelmLeverFinish finish,
                                    HelmWheelRim rim, bool night, Brow brow)
        {
            int w = HelmDashGeometry.CanvasW(rig), h = HelmDashGeometry.CanvasH(rig);
            var card = new DrawSurface(w, h);
            var wheel = new DrawSurface(WheelRigRender.W, WheelRigRender.H);
            var lever = new DrawSurface(LeverRigRender.W, LeverRigRender.H);

            float rpm = Mathf.Clamp01(0.11f + 0.89f * Mathf.Abs(Drive));
            if (rig == ConsoleRigKind.Novi)
                NoviDashRender.Render(card, in fit, running: true, rpm, Fuel, night);
            else
                CapeDashRender.Render(card, in fit, running: true, rpm, Fuel, night);

            // The brow instrument, where this proof asks for one (S6 mounts it here for real).
            if (brow != Brow.None)
            {
                HelmDashGeometry.SlotBoxOnCard(HelmDashGeometry.PilotSounderSlot, brow == Brow.Fish,
                                               out int bx, out int by, out int bw, out int bh);
                if (brow == Brow.Depth)
                {
                    DepthRigRender.DrawUnit(card, bx, by, bw, bh,
                        new DepthRigState(18.4f, false, night, true, 3f, 13.8f, false));
                }
                else
                {
                    FishRigRender.DrawUnit(card, bx, by, bw, bh,
                        new FishRigState(18.4f, 13.8f, 20f, 3f, false, night, true, true, false, false,
                                         0.0, FishRigAdjust.Range, 0.8f, true, 0.85f, 12.6f), null);
                }
            }

            // compass → wheel → lever, exactly the compositor's order (HelmDashController).
            if (fit.Compass != CompassMount.None)
            {
                HelmDashGeometry.CompassBoxOnCard(rig, fit.Compass, out int cbx, out int cby,
                                                  out int cbw, out int cbh);
                var compass = new DrawSurface(cbw, cbh);
                CompassRigRender.PaintInto(compass, 0, 0, cbw, cbh, fit.Compass, Heading, night);
                RigDrawUtil.CompositeAt(card, compass, cbx, cby);
            }
            WheelRigRender.Render(wheel, WheelRigGeometry.DegFromSteer(Steer, WheelRigGeometry.DefaultTurns), rim);
            HelmDashGeometry.WheelCellOrigin(rig, out int wx, out int wy);
            RigDrawUtil.CompositeAt(card, wheel, wx, wy);
            LeverRigRender.Render(lever, Drive, finish);
            HelmDashGeometry.LeverCellOrigin(rig, out int lx, out int ly);
            RigDrawUtil.CompositeAt(card, lever, lx, ly);

            Texture2D tex = null;
            card.ToTexture(ref tex);
            if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);
            string path = Path.Combine(OutDir, file);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[PilothouseDashProofEyeball] Wrote {path} ({w}x{h}).");
        }

        private static void Profile()
        {
            const int n = 100;
            var sw = new System.Diagnostics.Stopwatch();
            int w = HelmDashGeometry.PilotW, h = HelmDashGeometry.PilotH;
            var chrome = new DrawSurface(w, h);
            var card = new DrawSurface(w, h);
            var wheel = new DrawSurface(WheelRigRender.W, WheelRigRender.H);
            var lever = new DrawSurface(LeverRigRender.W, LeverRigRender.H);
            HelmDashGeometry.DomeBoxOnCard(ConsoleRigKind.Novi, out _, out _, out int cbw, out int cbh);
            var compass = new DrawSurface(cbw, cbh);
            Texture2D tex = null;

            var novi = new HelmFit(ConsoleRigKind.Novi, SounderKind.Depth, CompassMount.None, false, false);
            var cape = new HelmFit(ConsoleRigKind.Cape, SounderKind.Depth, CompassMount.None, false, false);

            // Warm every path once (static ramps, baked keys, texture alloc).
            NoviDashRender.Render(chrome, in novi, true, 0.5f, Fuel);
            CapeDashRender.Render(chrome, in cape, true, 0.5f, Fuel);
            WheelRigRender.Render(wheel, 0, HelmWheelRim.Steel);
            CompassRigRender.PaintDome(compass, 0, 0, cbw, cbh, 0, false);
            LeverRigRender.Render(lever, Drive, HelmLeverFinish.Chrome);
            card.ToTexture(ref tex);

            double MsPer(System.Action a)
            {
                sw.Restart();
                for (int i = 0; i < n; i++) a();
                sw.Stop();
                return sw.Elapsed.TotalMilliseconds / n;
            }

            int spin = 0;
            double noviMs = MsPer(() => NoviDashRender.Render(chrome, in novi, true, 0.5f, Fuel));
            double capeMs = MsPer(() => CapeDashRender.Render(chrome, in cape, true, 0.5f, Fuel));
            double noviNightMs = MsPer(() => NoviDashRender.Render(chrome, in novi, true, 0.5f, Fuel, true));
            double wheelMs = MsPer(() => WheelRigRender.Render(wheel, (spin++ * 3) % 540, HelmWheelRim.Steel));
            double compassMs = MsPer(() => CompassRigRender.PaintDome(compass, 0, 0, cbw, cbh, (spin++ * 7) % 360, false));
            double flushMs = MsPer(() => CompassRigRender.PaintFlush(compass, 0, 0,
                HelmDashGeometry.PilotFlushBoxW, HelmDashGeometry.PilotFlushBoxH, (spin++ * 7) % 360, false));
            double leverMs = MsPer(() => LeverRigRender.Render(lever, Drive, HelmLeverFinish.Chrome));
            double composeMs = MsPer(() =>
            {
                System.Array.Copy(chrome.Pixels, card.Pixels, chrome.Pixels.Length);
                HelmDashGeometry.DomeBoxOnCard(ConsoleRigKind.Novi, out int dbx, out int dby, out _, out _);
                RigDrawUtil.CompositeAt(card, compass, dbx, dby);
                HelmDashGeometry.WheelCellOrigin(ConsoleRigKind.Novi, out int wx, out int wy);
                RigDrawUtil.CompositeAt(card, wheel, wx, wy);
                HelmDashGeometry.LeverCellOrigin(ConsoleRigKind.Novi, out int lx, out int ly);
                RigDrawUtil.CompositeAt(card, lever, lx, ly);
            });
            double uploadMs = MsPer(() => card.ToTexture(ref tex));

            Object.DestroyImmediate(tex);
            Debug.Log("[PilothouseDashProofEyeball] repaint profile (ms avg over " + n + ", editor CPU, " +
                      $"{w}x{h}): novi chrome {noviMs:F2} · cape chrome {capeMs:F2} · " +
                      $"novi chrome NIGHT {noviNightMs:F2} · wheel cell {wheelMs:F2} · dome cell {compassMs:F2} · " +
                      $"flush cell {flushMs:F2} · lever cell {leverMs:F2} · compose {composeMs:F2} · " +
                      $"texture upload {uploadMs:F2}. Worst live frame = wheel + compose + upload " +
                      $"≈ {wheelMs + composeMs + uploadMs:F2} ms (chrome repaints only on a detent/gear/fit " +
                      "change, never per frame). Compare against S2a's skiff numbers from " +
                      "'Bake Skiff Dash Proofs' on the same machine — that is the honest comparison.");
        }
    }
}
