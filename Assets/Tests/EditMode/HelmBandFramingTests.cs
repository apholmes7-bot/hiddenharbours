using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE HELM DASH BAND, H1 — <b>the §9.8 framing under a full-width band</b>. The owner's §9.8 ruling
    /// frames every hull so the whole vessel shows; a band 144·k tall now covers the bottom of the
    /// screen, and the camera gives half of it back (ADR 0050). Does every hull's STERN still clear the
    /// band's top — at rest, and with the look-ahead at its most northward (heading north, the view leads
    /// her up the screen and her stern drops toward the band)?
    ///
    /// <para>Measured with the camera's own laws for every hull asset at the six screens: the §9.8 floor
    /// (<see cref="CameraZoomPolicy.HelmWorldHeightMeters"/>, margin 1.4 and the 40° iso as the scenes
    /// serialize them), the ladder step on the viewport's own height, and the fixed lens with shipped
    /// pull-back at zero. A config that restores pull-back multiplies that same rung. The ruled lead
    /// (0.6 s, capped at 0.35 of the rung's half view) and the band said
    /// through the seam (<see cref="HelmFootprintArea.FullWidthBand"/>, 144·k) with the camera's shift for
    /// it at the shipped share (<see cref="CameraFollow.BandShiftTargetPx"/> — 72·k px at the ruled half).
    /// Speeds are scanned from rest to past the lead's cap.</para>
    ///
    /// <para><b>The framing rule is not changed here</b> (the handoff: not without a ruling). At rest
    /// every hull clears. With a fixed lens, no shipped pull-back and full lead heading north, the
    /// named sterns below drop under the band. Each remains awaiting the owner's ruling; no framing
    /// rule changes here. They are pinned BY NAME, so a change that makes the set larger fails here
    /// and a change that fixes them is told to update this list.</para>
    /// </summary>
    public class HelmBandFramingTests
    {
        private const string BoatsFolder = "Assets/_Project/Data/Boats";
        private const string ConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";

        // The helm framing's two scene dials (every scene serializes CameraFollow's defaults).
        private const float HelmFitMargin = 1.4f;
        private const float IsoElevationDegrees = 40f;

        private const int Ppu = CameraFollow.AssetsPPU;
        private const int SpeedSamples = 4000;

        private static readonly Vector2Int[] Screens =
        {
            new Vector2Int(1280, 720), new Vector2Int(1280, 800), new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440), new Vector2Int(3440, 1440), new Vector2Int(3840, 2160),
        };

        /// <summary>Awaiting a ruling (the H1 report names them): the hulls whose stern does not clear
        /// the band with the look-ahead at its most northward.</summary>
        private static readonly string[] SternUnderTheBand_AwaitingARuling =
        {
            "boat.coastal_packet @3840x2160",
            "boat.expedition_yacht_120 @1280x800",
            "boat.sport_fisher_convertible @1280x720",
            "boat.sport_fisher_skybridge @1920x1080",
            "boat.sport_fisher_skybridge @3840x2160",
            "boat.stern_trawler @1280x800",
            "boat.stern_trawler_mk2 @1280x800",
            "boat.tanker @1280x800",
            "boat.tanker @3840x2160",
        };

        [Test]
        public void AtRest_EveryHullsStern_ClearsTheBand_AtEveryScreen()
        {
            List<BoatHullDef> hulls = Hulls();
            GameConfig cfg = Shipped();
            JuiceSettings juice = cfg.Juice;
            float share = RuledShare(cfg);
            var failures = new List<string>();
            foreach (BoatHullDef hull in hulls)
            foreach (Vector2Int s in Screens)
            {
                float clearance = SternClearancePx(hull, s, 0f, in juice, share);
                if (clearance < 0f)
                    failures.Add($"{hull.Id} @{s.x}x{s.y}: stern {-clearance:F1} px under the band's top");
            }
            CollectionAssert.IsEmpty(failures, $"{hulls.Count} hulls at rest:\n" + string.Join("\n", failures));
        }

        [Test]
        public void WithTheLookAheadAtItsMostNorthward_OnlyTheNamedSterns_DropUnderTheBand()
        {
            List<BoatHullDef> hulls = Hulls();
            GameConfig cfg = Shipped();
            JuiceSettings juice = cfg.Juice;
            float share = RuledShare(cfg);
            Assert.AreEqual(0.6f, juice.BoatLeadSeconds, "premise: the ruled lead");
            Assert.AreEqual(0.35f, juice.BoatLeadMaxViewFraction, "premise: the ruled cap");

            var under = new List<string>();
            var detail = new List<string>();
            foreach (BoatHullDef hull in hulls)
            foreach (Vector2Int s in Screens)
            {
                float cap = juice.BoatLeadMaxViewFraction * 0.5f * RungHeight(hull, s);
                float vMax = Mathf.Max(TopSpeed(hull), cap / juice.BoatLeadSeconds);
                float worst = float.MaxValue, worstV = 0f;
                // Keep the exact 1 m/s sample alongside the full speed scan.
                Consider(SternClearancePx(hull, s, 1f, in juice, share), 1f, ref worst, ref worstV);
                for (int i = 0; i <= SpeedSamples; i++)
                {
                    float v = vMax * i / SpeedSamples;
                    Consider(SternClearancePx(hull, s, v, in juice, share), v, ref worst, ref worstV);
                }
                if (worst < 0f)
                {
                    under.Add($"{hull.Id} @{s.x}x{s.y}");
                    detail.Add($"{hull.Id} @{s.x}x{s.y}: {-worst:F1} px under at {worstV:F2} m/s");
                }
            }
            under.Sort(System.StringComparer.Ordinal);
            // The 120's row lands with #1011; this Where leaves with the 120 plate.
            // Only that row may be absent before its hull lands; every other pinned row stays required.
            CollectionAssert.AreEqual(SternUnderTheBand_AwaitingARuling.Where(row =>
                    row != "boat.expedition_yacht_120 @1280x800" ||
                    hulls.Any(hull => hull.Id == "boat.expedition_yacht_120")).ToArray(), under,
                "the sterns under a full-width band with the lead at its most northward — the framing rule " +
                "is not changed without a ruling; if a ruling moved it, update this list to what it fixed:\n" +
                string.Join("\n", detail));
        }

        private static void Consider(float clearance, float v, ref float worst, ref float worstV)
        {
            if (clearance < worst) { worst = clearance; worstV = v; }
        }

        /// <summary>The shipped share of the band the camera gives back. The named sterns were measured at
        /// the ruled half: a tuned share fails here first, so the list is re-measured, not trusted.</summary>
        private static float RuledShare(GameConfig cfg)
        {
            float share = cfg.HelmFootprint.CameraBandShare;
            Assert.AreEqual(0.5f, share, "premise: the ruled share (half the band)");
            return share;
        }

        // ---- the camera's laws, for one hull on one screen -------------------------------------------

        /// <summary>The §9.8 request for this hull on this screen, metres.</summary>
        private static float Request(BoatHullDef hull, Vector2Int s)
            => CameraZoomPolicy.HelmWorldHeightMeters(hull.CameraWorldHeightMeters, hull.LengthMeters,
                                                      HelmFitMargin, IsoElevationDegrees, s.x / (float)s.y);

        /// <summary>The ladder step the request lands on (the grid and the lead's cap read it).</summary>
        private static float RungHeight(BoatHullDef hull, Vector2Int s)
            => CameraZoomPolicy.WorldHeightForStep(
                CameraZoomPolicy.StepForWorldHeight(Request(hull, s), Ppu, s.y), Ppu, s.y);

        /// <summary>
        /// How far (screen px) the stern stands above the band's top at <paramref name="speed"/>, heading
        /// north with the lead ruled for that speed, the camera shifted by <paramref name="bandShare"/> of the
        /// band. Negative: under the band.
        /// </summary>
        private static float SternClearancePx(BoatHullDef hull, Vector2Int s, float speed, in JuiceSettings juice,
                                              float bandShare)
        {
            int step = CameraZoomPolicy.StepForWorldHeight(Request(hull, s), Ppu, s.y);
            float rung = CameraZoomPolicy.WorldHeightForStep(step, Ppu, s.y);
            float pullBack = CameraFeel.PullBackTarget(speed, 0f, in juice);
            float metersPerPx = rung * (1f + pullBack) / s.y;

            float cap = juice.BoatLeadMaxViewFraction * 0.5f * rung;
            float lead = Mathf.Min(juice.BoatLeadSeconds * speed, cap);
            int k = Mathf.Max(1, s.y / 1080);
            HelmFootprintArea band = HelmFootprintArea.FullWidthBand(144f * k);
            float shiftPx = CameraFollow.BandShiftTargetPx(in band, bandShare);
            float halfHull = 0.5f * hull.LengthMeters * Mathf.Sin(IsoElevationDegrees * Mathf.Deg2Rad);

            float boatPx = s.y * 0.5f + shiftPx - lead / metersPerPx;
            float sternPx = boatPx - halfHull / metersPerPx;
            return sternPx - band.TopPx;
        }

        private static float TopSpeed(BoatHullDef hull)
        {
            float thrust = BoatController.EngineThrust(1f, hull.EnginePower, 1f) * BoatController.ForceFeelScale;
            float resistance = SailDrive.LinearResistance(hull.ForwardDrag, Mathf.Max(1f, hull.MassKg / 100f),
                                                          BoatController.HullLinearDamping,
                                                          BoatController.ForceFeelScale)
                               * BoatController.ForceFeelScale;
            return resistance > 0f ? thrust / resistance : 0f;
        }

        private static List<BoatHullDef> Hulls()
        {
            List<BoatHullDef> hulls = UnityEditor.AssetDatabase.FindAssets("t:BoatHullDef", new[] { BoatsFolder })
                .Select(guid => UnityEditor.AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => UnityEditor.AssetDatabase.LoadAssetAtPath<BoatHullDef>(path))
                .Where(h => h != null)
                .OrderBy(h => h.Id, System.StringComparer.Ordinal)
                .ToList();
            Assert.GreaterOrEqual(hulls.Count, 39, "premise: every hull asset in the boats folder (39 today)");
            return hulls;
        }

        private static GameConfig Shipped()
        {
            var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAssetPath);
            Assert.IsNotNull(cfg, ConfigAssetPath);
            return cfg;
        }
    }
}
