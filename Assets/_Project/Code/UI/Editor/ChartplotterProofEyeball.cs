using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI.Editor
{
    /// <summary>
    /// Bakes the OWNER EYEBALL PROOFS for the S6 chartplotter's console face — day and night, at the
    /// brow-mount size and at native — plus the rule-7 profile. Written to <c>docs/art/proofs/</c>
    /// (outside Assets/ on purpose: a proof for the PR and the owner, not game content).
    ///
    /// <para><b>⚠ The terrain here is a HARNESS, not St Peters.</b> The acceptance criteria ask for a
    /// proof against a recognisable St Peters shoreline, and that is genuinely blocked: the region has
    /// no baked <c>PaintedHeightMap</c> asset yet (the seabed re-bake the owner already owes), and the
    /// runtime terrain services are scene MonoBehaviours, so there is no seabed an editor script can
    /// load without standing up the whole region — which is itself a known false-green. So this bakes
    /// against an ANALYTIC coast: a shelving shore, a drying flat and a shoal, with depths in the
    /// game's own metres. What it proves is the shading ladder, the symbols, the transform, the strips
    /// and both palettes; what it cannot prove is that the survey matches the painted seabed, and the
    /// St Peters sheet is owed the moment that asset exists.</para>
    /// </summary>
    public static class ChartplotterProofEyeball
    {
        private const string OutDir = "docs/art/proofs";

        /// <summary>A coast with everything the shading ladder has to distinguish: land above datum, a
        /// drying flat, a shoal that shallows in the middle of the channel, and deep water offshore.
        /// Deterministic and analytic — no asset, no scene.</summary>
        private sealed class HarnessCoast : ITidalTerrain
        {
            public float ElevationAt(Vector2 p)
            {
                // A shore running along the region's west side: high inland, shelving away east.
                float shore = Mathf.Lerp(6f, -22f, Mathf.InverseLerp(60f, 620f, p.x));
                // A bay mouth cut into it, so the coastline is not a straight edge.
                float bay = -7f * Mathf.Exp(-Mathf.Pow((p.y - 300f) / 90f, 2f))
                                * Mathf.Exp(-Mathf.Pow((p.x - 150f) / 120f, 2f));
                // A drying flat inside the bay (elevation hovering just under datum).
                float flat = 5.5f * Mathf.Exp(-Mathf.Pow((p.x - 250f) / 70f, 2f)
                                              - Mathf.Pow((p.y - 180f) / 55f, 2f));
                // A shoal offshore — the thing a chart exists to warn about.
                float shoal = 9f * Mathf.Exp(-Mathf.Pow((p.x - 470f) / 60f, 2f)
                                             - Mathf.Pow((p.y - 380f) / 45f, 2f));
                return shore + bay + flat + shoal;
            }
        }

        [MenuItem("Hidden Harbours/Dev/Bake Chartplotter Proofs (console + brow + expanded, day + night) + profile")]
        public static void Bake()
        {
            var terrain = new HarnessCoast();
            var bounds = new Rect(0f, 0f, 760f, 520f);      // St Peters' real WorldSizeMeters

            var boat = new Vector2(360f, 300f);
            var waypoints = new List<NavWaypoint>
            {
                new(Region, "FUEL", new Vector2(190f, 250f), NavWaypointKind.Fuel),
                new(Region, "SHOAL", new Vector2(470f, 380f), NavWaypointKind.Hazard),
                new(Region, "BERTH", new Vector2(150f, 320f), NavWaypointKind.Anchor),
            };
            var route = new List<Vector2> { boat, new(430f, 340f), new(520f, 250f), new(600f, 210f) };
            var track = new List<Vector2>();
            for (int i = 0; i < 40; i++)
                track.Add(new Vector2(160f + i * 5.2f, 320f - i * 0.6f + Mathf.Sin(i * 0.35f) * 9f));

            foreach (bool night in new[] { false, true })
            {
                var chart = new NavChartSource();
                chart.EnsureBaked(bounds, night, terrain);

                var st = new NavRigState(boat, hasBoat: true, headingDeg: 62f, speedKnots: 6.4f,
                                         rangeNM: GameServices.Chartplotter.RangeNMAt(2),
                                         orient: NavRigGeometry.Orient.North, night: night,
                                         showTrack: true, tideMetres: 2.1f, tideRising: true);

                string slug = night ? "night" : "day";
                BakeOne($"chartplotter-console-{slug}-csharp.png",
                        NavRigGeometry.ConsoleW, NavRigGeometry.ConsoleH, in st, chart,
                        waypoints, route, track);

                // …and at the size it actually mounts in the Novi/Cape brow, which is where legibility
                // is decided. The slot is 150x104 on a 600x548 card; at a 1.5x focused dash that is
                // roughly this.
                BakeOne($"chartplotter-brow-{slug}-csharp.png", 225, 156, in st, chart,
                        waypoints, route, track);

                // ---- S6 PR 3: the two sheets that show what the HOST actually puts on screen ----
                //
                // ⚠ The sheet above RE-RENDERS the rig at 225x156. That is not what the player sees.
                // The flush-mount mechanism rasters the rig ONCE at native size and hands that one
                // texture to a RawImage that the mount rect scales (HelmInstrumentMountLayout: "one
                // raster, two presentations", and DrawSurface uploads FilterMode.Point). Re-rendering
                // small is a different picture — the rig's own typography breaks below ~83% of native,
                // which is precisely why the mechanism scales rather than re-renders. So these bake the
                // native raster and POINT-SAMPLE it to each destination, which is the real pipeline.
                var native = new DrawSurface(NavRigGeometry.ConsoleW, NavRigGeometry.ConsoleH);
                NavRigRender.Render(native, 0, 0, NavRigGeometry.ConsoleW, NavRigGeometry.ConsoleH,
                                    in st, chart, waypoints, route, track);
                BakeBlit($"chartplotter-brow-{slug}-mounted-csharp.png", native, 225, 156);
                BakeBlit($"chartplotter-expanded-{slug}-csharp.png", native,
                         NavRigGeometry.ConsoleW * 2, NavRigGeometry.ConsoleH * 2);
            }

            // A head-up sheet, because a rotated chart is the thing most likely to be silently wrong.
            {
                var chart = new NavChartSource();
                chart.EnsureBaked(bounds, false, terrain);
                var st = new NavRigState(boat, true, 62f, 6.4f, GameServices.Chartplotter.RangeNMAt(2),
                                         NavRigGeometry.Orient.Head, false, true, 2.1f, true);
                BakeOne("chartplotter-console-headup-csharp.png",
                        NavRigGeometry.ConsoleW, NavRigGeometry.ConsoleH, in st, chart,
                        waypoints, route, track);
            }

            Profile(terrain, bounds, boat, waypoints, route, track);
        }

        private const string Region = "region.st_peters";

        private static void BakeOne(string file, int w, int h, in NavRigState st, NavChartSource chart,
                                    List<NavWaypoint> waypoints, List<Vector2> route, List<Vector2> track)
        {
            var s = new DrawSurface(w, h);
            NavRigRender.Render(s, 0, 0, w, h, in st, chart, waypoints, route, track);

            Texture2D tex = null;
            s.ToTexture(ref tex);
            if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);
            string path = Path.Combine(OutDir, file);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[ChartplotterProof] Wrote {path} ({w}x{h}).");
        }

        /// <summary>
        /// Write ONE already-rastered surface at a destination size, point-sampled — the honest model of
        /// what the hosts do, where a single native raster is blitted into whatever rect it is mounted
        /// in. Nearest-neighbour on purpose: <see cref="DrawSurface.ToTexture"/> uploads
        /// <see cref="FilterMode.Point"/>, so anything smoother here would flatter the proof.
        /// </summary>
        private static void BakeBlit(string file, DrawSurface src, int destW, int destH)
        {
            if (destW <= 0 || destH <= 0) return;
            var dst = new DrawSurface(destW, destH);
            for (int y = 0; y < destH; y++)
            {
                int sy = Mathf.Clamp((int)((y + 0.5f) * src.Height / destH), 0, src.Height - 1);
                for (int x = 0; x < destW; x++)
                {
                    int sx = Mathf.Clamp((int)((x + 0.5f) * src.Width / destW), 0, src.Width - 1);
                    dst.Pixels[y * destW + x] = src.Pixels[sy * src.Width + sx];
                }
            }

            Texture2D tex = null;
            dst.ToTexture(ref tex);
            if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);
            string path = Path.Combine(OutDir, file);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[ChartplotterProof] Wrote {path} ({destW}x{destH}, blitted from " +
                      $"{src.Width}x{src.Height} — the real mount pipeline).");
        }

        private static void Profile(ITidalTerrain terrain, Rect bounds, Vector2 boat,
                                    List<NavWaypoint> waypoints, List<Vector2> route, List<Vector2> track)
        {
            const int n = 30;
            var sw = new System.Diagnostics.Stopwatch();

            // The BAKE: paid once per (region x palette), never while sailing.
            var chart = new NavChartSource();
            chart.EnsureBaked(bounds, false, terrain);          // warm
            sw.Restart();
            for (int i = 0; i < n; i++)
            {
                var c = new NavChartSource();
                c.EnsureBaked(bounds, false, terrain);
            }
            sw.Stop();
            double bakeMs = sw.Elapsed.TotalMilliseconds / n;

            // The REPAINT at both sizes.
            var st = new NavRigState(boat, true, 62f, 6.4f, GameServices.Chartplotter.RangeNMAt(2),
                                     NavRigGeometry.Orient.North, false, true, 2.1f, true);
            double MsPer(int w, int h)
            {
                var s = new DrawSurface(w, h);
                NavRigRender.Render(s, 0, 0, w, h, in st, chart, waypoints, route, track);   // warm
                sw.Restart();
                for (int i = 0; i < n; i++) NavRigRender.Render(s, 0, 0, w, h, in st, chart, waypoints, route, track);
                sw.Stop();
                return sw.Elapsed.TotalMilliseconds / n;
            }
            double brow = MsPer(225, 156);
            double native = MsPer(NavRigGeometry.ConsoleW, NavRigGeometry.ConsoleH);

            // And the claim that matters: a steady sail must not re-bake the survey.
            int rebakes = 0;
            for (int i = 0; i < 200; i++)
                if (chart.EnsureBaked(bounds, false, terrain)) rebakes++;

            Debug.Log($"[ChartplotterProof] profile (ms avg over {n}, editor CPU, no GPU):\n" +
                      $"  chart base bake  {bounds.width}x{bounds.height} m  {bakeMs:F2} ms   " +
                      "(once per region x palette — NOT per repaint)\n" +
                      $"  repaint brow     225x156     {brow:F2} ms\n" +
                      $"  repaint native   {NavRigGeometry.ConsoleW}x{NavRigGeometry.ConsoleH}     {native:F2} ms\n" +
                      $"  re-bakes over 200 steady frames: {rebakes} (must be 0)");
        }
    }
}
