using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI.Editor
{
    /// <summary>
    /// Bakes the OWNER EYEBALL PROOFS for the S5 radar — the scope day and night, standing by and
    /// transmitting, at the brow-mount size and expanded, plus a head-up/north-up pair and a
    /// sea-clutter pair. Written to <c>docs/art/proofs/</c> (outside <c>Assets/</c> on purpose: a proof
    /// for the PR and the owner, not game content). The <see cref="ChartplotterProofEyeball"/> pattern
    /// throughout, including its two disciplines:
    ///
    /// <para><b>⚠ The terrain is a HARNESS, not St Peters</b> — the same blocker the chartplotter's
    /// sheet records: the region has no baked <c>PaintedHeightMap</c> asset yet, and the runtime terrain
    /// services are scene MonoBehaviours, so there is no seabed an editor script can load without
    /// standing the whole region up (itself a known false-green). So this bakes against an ANALYTIC
    /// coast in the game's own metres. What it proves is the PPI, the echo shapes, the sweep, both
    /// palettes and the tide's effect on the waterline; what it cannot prove is that the coastline
    /// matches the painted seabed.</para>
    ///
    /// <para><b>⚠ The mounted sheets are BLITTED, never re-rendered small.</b> The flush mount rasters
    /// the rig ONCE at native size and hands that texture to a <c>RawImage</c> the mount rect scales
    /// (<see cref="HelmInstrumentMountLayout"/> — "one raster, two presentations"). Re-rendering at the
    /// mount's size is a different picture, because the rig's 3×5 typography breaks below ~83% of
    /// native. So the mounted sheets point-sample the native raster, which is the real pipeline.</para>
    /// </summary>
    public static class RadarProofEyeball
    {
        private const string OutDir = "docs/art/proofs";

        /// <summary>The world the sheets are taken in — St Peters' real <c>WorldSizeMeters</c>.</summary>
        private static readonly Rect Bounds = new Rect(0f, 0f, 760f, 520f);
        private static readonly Vector2 Boat = new Vector2(360f, 300f);
        private const float Heading = 62f;

        /// <summary>A coast with everything the scope has to distinguish: a shore to the west with a bay
        /// cut into it, a drying flat that bares at low water, and an offshore shoal. Deterministic and
        /// analytic — no asset, no scene. Deliberately the chartplotter harness's shape, so the two
        /// instruments' sheets can be read against each other.</summary>
        private sealed class HarnessCoast : ITidalTerrain
        {
            public float ElevationAt(Vector2 p)
            {
                float shore = Mathf.Lerp(6f, -22f, Mathf.InverseLerp(60f, 620f, p.x));
                float bay = -7f * Mathf.Exp(-Mathf.Pow((p.y - 300f) / 90f, 2f))
                                * Mathf.Exp(-Mathf.Pow((p.x - 150f) / 120f, 2f));
                float flat = 5.5f * Mathf.Exp(-Mathf.Pow((p.x - 250f) / 70f, 2f)
                                              - Mathf.Pow((p.y - 180f) / 55f, 2f));
                float shoal = 9f * Mathf.Exp(-Mathf.Pow((p.x - 470f) / 60f, 2f)
                                             - Mathf.Pow((p.y - 380f) / 45f, 2f));
                return shore + bay + flat + shoal;
            }
        }

        /// <summary>A working scene on the water: two punts under way, three buoys, and the coast.
        /// Placed in WORLD metres and measured from own ship exactly as the host does, so what these
        /// sheets show is the real conversion and not a hand-placed picture.</summary>
        private static readonly List<RadarContact> Fleet = new()
        {
            new RadarContact(new Vector2(455f, 375f), RadarEchoKind.Vessel, 1.05f, 232f, 3.9f),
            new RadarContact(new Vector2(300f, 205f), RadarEchoKind.Vessel, 1.25f, 300f, 5.4f),
            RadarContact.Still(new Vector2(430f, 300f), RadarEchoKind.Buoy, 0.7f),
            RadarContact.Still(new Vector2(392f, 355f), RadarEchoKind.Buoy, 0.7f),
            RadarContact.Still(new Vector2(330f, 250f), RadarEchoKind.Buoy, 0.7f),
        };

        [MenuItem("Hidden Harbours/Dev/Bake Radar Proofs (brow + expanded, day + night, standby + TX)")]
        public static void Bake()
        {
            var terrain = new HarnessCoast();
            RadarSettings cfg = GameServices.Radar;
            float rangeNM = cfg.RangeNMAt(cfg.SafeDefaultRangeStep);

            // The brow slot is 150x150 on a 600x548 card; at a 1.5x focused dash that is ~225 wide, and
            // the portrait instrument letterboxes to 164x225 inside it.
            const int MountW = 164, MountH = 225;

            foreach (bool night in new[] { false, true })
            {
                string slug = night ? "night" : "day";

                DrawSurface tx = Native(terrain, rangeNM, in cfg, night, RadarMode.HeadUp, sea: 0.25f,
                                        sweepDeg: 118f);
                Write($"radar-scope-{slug}-csharp.png", tx);
                BakeBlit($"radar-brow-{slug}-mounted-csharp.png", tx, MountW, MountH);
                BakeBlit($"radar-expanded-{slug}-csharp.png", tx,
                         RadarRigRender.W * 2, RadarRigRender.H * 2);

                // STANDBY — the state the dash used to draw for it. No sweep, echoes flat at half lit.
                Write($"radar-standby-{slug}-csharp.png",
                      Native(terrain, rangeNM, in cfg, night, RadarMode.Standby, 0.25f, 118f));
            }

            // North-up against head-up: a rotated picture is the thing most likely to be silently wrong.
            Write("radar-northup-day-csharp.png",
                  Native(terrain, rangeNM, in cfg, false, RadarMode.NorthUp, 0.25f, 118f));

            // Flat calm against a full gale — the sea state really does reach the glass (P1).
            Write("radar-calm-day-csharp.png",
                  Native(terrain, rangeNM, in cfg, false, RadarMode.HeadUp, 0f, 118f));
            Write("radar-gale-day-csharp.png",
                  Native(terrain, rangeNM, in cfg, false, RadarMode.HeadUp,
                         Mathf.Clamp01(cfg.SeaClutterAtFullSeaState), 118f));

            // ⚠ The tide ruling, as a picture: the SAME coast at low water and at high. The drying flat
            // inside the bay is a solid return on one sheet and gone on the other, because a radar reads
            // the water of the moment — the deliberate opposite of the chart's datum survey.
            Write("radar-tide-low-day-csharp.png",
                  Native(terrain, rangeNM, in cfg, false, RadarMode.NorthUp, 0.15f, 118f, water: 0f));
            Write("radar-tide-high-day-csharp.png",
                  Native(terrain, rangeNM, in cfg, false, RadarMode.NorthUp, 0.15f, 118f, water: 3f));

            AssetDatabase.Refresh();
        }

        /// <summary>One native-size raster, with the blips measured from own ship through the ONE
        /// bearing convention — the host's own <c>ReadContacts</c>, so a sheet cannot be right here and
        /// wrong on the glass.</summary>
        private static DrawSurface Native(ITidalTerrain terrain, float rangeNM, in RadarSettings cfg,
                                          bool night, RadarMode mode, float sea, float sweepDeg,
                                          float water = 1f)
        {
            var prefs = new RadarPrefs(night, mode, cfg.SafeDefaultRangeStep);
            var st = new RadarRigState(rangeNM, cfg.SafeRings, prefs.HeadUp, Heading,
                                       Mathf.Clamp01(cfg.Gain), sea, rain: 0f,
                                       transmitting: prefs.Transmitting, trails: true, night: night,
                                       sweepDeg: sweepDeg, blink: false);

            var land = new RadarLandEcho();
            land.EnsureScanned(Boat, NavMath.NMToMetres(rangeNM), water, terrain, in cfg);

            var blips = new List<RadarBlip>();
            void Add(RadarContact c)
            {
                float bearing = NavMath.BearingDegrees(Boat, c.WorldPos);
                float rng = NavMath.MetresToNM(NavMath.DistanceMetres(Boat, c.WorldPos));
                float trail = c.IsMakingWay
                    ? Mathf.Clamp(c.SpeedMps / RadarRigRender.TrailReferenceSpeedMps, 0f, 2f) : 0f;
                blips.Add(new RadarBlip(bearing, rng, c.Size, c.Kind,
                                        c.IsMakingWay ? c.CourseDegrees : float.NaN, trail));
            }
            foreach (RadarContact c in land.Echoes) Add(c);
            foreach (RadarContact c in Fleet) Add(c);

            var s = new DrawSurface(RadarRigRender.W, RadarRigRender.H);
            RadarRigRender.RenderStandalone(s, in st, blips);
            return s;
        }

        private static void Write(string file, DrawSurface s)
        {
            Texture2D tex = null;
            s.ToTexture(ref tex);
            if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);
            string path = Path.Combine(OutDir, file);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[RadarProof] Wrote {path} ({s.Width}x{s.Height}).");
        }

        /// <summary>Point-sample the native raster to a destination size — the REAL mount pipeline
        /// (<see cref="DrawSurface.ToTexture"/> uploads <c>FilterMode.Point</c>), never a re-render.</summary>
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
            Debug.Log($"[RadarProof] Wrote {path} ({destW}x{destH}, blitted from " +
                      $"{src.Width}x{src.Height} — the real mount pipeline).");
        }
    }
}
