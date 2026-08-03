using System.IO;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI.Editor
{
    /// <summary>
    /// Bakes the OWNER EYEBALL PROOFS for the S2a composed skiff dashes: the console and sport
    /// dash chrome with the grabbable wheel, dome compass and binnacle lever composited exactly as
    /// the runtime compositor mounts them (day state, running, half ahead, a third of port lock,
    /// heading 235° — a state where every instrument visibly reads). Written to
    /// <c>docs/art/proofs/</c> (outside Assets/ on purpose — a proof for the PR/owner, not game
    /// content). Compare against the rigs' own previews at the same signals.
    ///
    /// <para>Also the rule-7 PROFILE harness: it stopwatches each layer's repaint and the composite
    /// + upload over a steady-state loop and logs the numbers the ADR demands before a helm ships
    /// (editor CPU raster timings; the PR body carries them).</para>
    /// </summary>
    public static class DashProofEyeball
    {
        private const string ConsoleOut = "docs/art/proofs/console-dash-csharp.png";
        private const string SportOut = "docs/art/proofs/sport-dash-csharp.png";

        // The proof state: every instrument off its rest pose.
        private const float Drive = 0.5f;
        private const float Steer = -0.35f;
        private const float Heading = 235f;
        private const float Fuel = 1f;

        [MenuItem("Hidden Harbours/Dev/Bake Skiff Dash Proofs (console + sport, day) + profile")]
        public static void Bake()
        {
            BakeOne(sport: false, ConsoleOut, HelmLeverFinish.Graphite, HelmWheelRim.Rubber);
            BakeOne(sport: true, SportOut, HelmLeverFinish.Chrome, HelmWheelRim.Steel);
            Profile();
        }

        private static void BakeOne(bool sport, string outPath, HelmLeverFinish finish, HelmWheelRim rim)
        {
            var chrome = new DrawSurface(HelmDashGeometry.W, HelmDashGeometry.H);
            var card = new DrawSurface(HelmDashGeometry.W, HelmDashGeometry.H);
            var wheel = new DrawSurface(WheelRigRender.W, WheelRigRender.H);
            var lever = new DrawSurface(LeverRigRender.W, LeverRigRender.H);
            var compass = new DrawSurface(HelmDashGeometry.DomeBoxW, HelmDashGeometry.DomeBoxH);

            float rpm = Mathf.Clamp01(0.11f + 0.89f * Mathf.Abs(Drive));
            if (sport) SportDashRender.Render(chrome, running: true, Drive, rpm, Fuel);
            else ConsoleDashRender.Render(chrome, running: true, Drive, rpm, Fuel);
            WheelRigRender.Render(wheel, WheelRigGeometry.DegFromSteer(Steer, WheelRigGeometry.DefaultTurns), rim);
            CompassRigRender.PaintDome(compass, 0, 0, HelmDashGeometry.DomeBoxW, HelmDashGeometry.DomeBoxH,
                                       Heading, night: false);
            LeverRigRender.Render(lever, Drive, finish);

            System.Array.Copy(chrome.Pixels, card.Pixels, chrome.Pixels.Length);
            HelmDashGeometry.DomeBoxOnCard(out int dbx, out int dby, out _, out _);
            RigDrawUtil.CompositeAt(card, compass, dbx, dby);
            HelmDashGeometry.WheelCellOrigin(out int wx, out int wy);
            RigDrawUtil.CompositeAt(card, wheel, wx, wy);
            if (!sport) ConsoleDashRender.PaintWheelCap(card);
            HelmDashGeometry.LeverCellOrigin(out int lx, out int ly);
            RigDrawUtil.CompositeAt(card, lever, lx, ly);

            Texture2D tex = null;
            card.ToTexture(ref tex);
            string dir = Path.GetDirectoryName(outPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[DashProofEyeball] Wrote {outPath} ({HelmDashGeometry.W}x{HelmDashGeometry.H}).");
        }

        private static void Profile()
        {
            var chrome = new DrawSurface(HelmDashGeometry.W, HelmDashGeometry.H);
            var card = new DrawSurface(HelmDashGeometry.W, HelmDashGeometry.H);
            var wheel = new DrawSurface(WheelRigRender.W, WheelRigRender.H);
            var lever = new DrawSurface(LeverRigRender.W, LeverRigRender.H);
            var compass = new DrawSurface(HelmDashGeometry.DomeBoxW, HelmDashGeometry.DomeBoxH);
            Texture2D tex = null;
            var sw = new System.Diagnostics.Stopwatch();
            const int n = 100;

            // Warm every path once (static ramps, key bake, card bake).
            ConsoleDashRender.Render(chrome, true, Drive, 0.5f, Fuel);
            SportDashRender.Render(chrome, true, Drive, 0.5f, Fuel);
            WheelRigRender.Render(wheel, 0, HelmWheelRim.Rubber);
            CompassRigRender.PaintDome(compass, 0, 0, HelmDashGeometry.DomeBoxW, HelmDashGeometry.DomeBoxH, 0, false);
            LeverRigRender.Render(lever, Drive, HelmLeverFinish.Graphite);
            card.ToTexture(ref tex);

            double MsPer(System.Action a)
            {
                sw.Restart();
                for (int i = 0; i < n; i++) a();
                sw.Stop();
                return sw.Elapsed.TotalMilliseconds / n;
            }

            int spin = 0;
            double chromeMs = MsPer(() => ConsoleDashRender.Render(chrome, true, Drive, 0.5f, Fuel));
            double sportMs = MsPer(() => SportDashRender.Render(chrome, true, Drive, 0.5f, Fuel));
            double wheelMs = MsPer(() => WheelRigRender.Render(wheel, (spin++ * 3) % 540, HelmWheelRim.Rubber));
            double compassMs = MsPer(() => CompassRigRender.PaintDome(compass, 0, 0,
                HelmDashGeometry.DomeBoxW, HelmDashGeometry.DomeBoxH, (spin++ * 7) % 360, false));
            double leverMs = MsPer(() => LeverRigRender.Render(lever, Drive, HelmLeverFinish.Graphite));
            double composeMs = MsPer(() =>
            {
                System.Array.Copy(chrome.Pixels, card.Pixels, chrome.Pixels.Length);
                HelmDashGeometry.DomeBoxOnCard(out int dbx, out int dby, out _, out _);
                RigDrawUtil.CompositeAt(card, compass, dbx, dby);
                HelmDashGeometry.WheelCellOrigin(out int wx, out int wy);
                RigDrawUtil.CompositeAt(card, wheel, wx, wy);
                ConsoleDashRender.PaintWheelCap(card);
                HelmDashGeometry.LeverCellOrigin(out int lx, out int ly);
                RigDrawUtil.CompositeAt(card, lever, lx, ly);
            });
            double uploadMs = MsPer(() => card.ToTexture(ref tex));

            Object.DestroyImmediate(tex);
            Debug.Log("[DashProofEyeball] repaint profile (ms avg over " + n + ", editor CPU): " +
                      $"console chrome {chromeMs:F2} · sport chrome {sportMs:F2} · wheel cell {wheelMs:F2} · " +
                      $"compass cell {compassMs:F2} · lever cell {leverMs:F2} · compose {composeMs:F2} · " +
                      $"texture upload {uploadMs:F2}. Worst live frame = wheel + compose + upload " +
                      $"≈ {wheelMs + composeMs + uploadMs:F2} ms (chrome repaints only on a detent/gear change).");
        }
    }
}
