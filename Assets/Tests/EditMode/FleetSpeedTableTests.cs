using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE FLEET SPEED TABLE, held against every hull Def on disk.
    ///
    /// <para><b>The ruling.</b> The owner, 2026-11-12, on the four speed decisions in
    /// <c>docs/design/fleet-speed-and-fuel-audit.md</c>: planing hulls run at 0.45 x their real-world top
    /// speed; the jetski stays 5.5 m/s; the go-fast is 10 m/s; fuel burn rises with speed (PR 1134's
    /// model, untouched here). Every other hull keeps the speed the audit measured her at.</para>
    ///
    /// <para><b>The cap.</b> The owner, 2026-11-13: "keep every planing hull just under the go-fast, so
    /// she stays the fastest boat" - yes. A planing hull whose 0.45 x real speed would pass the go-fast
    /// runs instead at the go-fast's own Def speed minus
    /// <see cref="GameConfig.PlaningTopSpeedMarginUnderGoFastMps"/>. The margin is read off the
    /// GameConfig asset and the go-fast's speed off her own Def, so neither is a number in this file.
    /// <see cref="TheGoFast_IsStrictlyTheFastestHullInTheFleet"/> holds the outcome by name.</para>
    ///
    /// <para><b>Where a hull's speed lives.</b> A <see cref="BoatHullDef"/> has no top-speed field. Her top
    /// speed is what <see cref="BoatController"/>'s force model settles at: full thrust against the hull's
    /// own drag plus the body's damping on her mass. So the table is applied as DATA by solving
    /// <see cref="BoatHullDef.EnginePower"/> on each asset for the ruled speed, with mass and drag left
    /// alone, and this test reads the speed back off the same three numbers.</para>
    ///
    /// <para><b>Which hulls are "planing".</b> Not a list invented here: the hulls PR 1134 bound to the
    /// planing fuel curve (<c>fuelcurve.planing</c>). <see cref="ThePlaningRows_AreTheHullsTheFuelModelCallsPlaning"/>
    /// holds the two lists together, so a hull reclassified in the fuel model must take a speed row too.</para>
    ///
    /// <para>A new hull fails <see cref="EveryHullDef_RunsAtHerRowInTheSpeedTable"/> until she has a row.
    /// That is deliberate: a speed nobody ruled on does not join the fleet by accident.</para>
    /// </summary>
    public class FleetSpeedTableTests
    {
        const string DataBoats = "Assets/_Project/Data/Boats";
        const string FuelSpeedFolder = "Assets/_Project/Data/Boats/FuelSpeed";
        const string ConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";
        const string GoFastId = "boat.go_fast";

        /// <summary>The ruled share of real-world top speed a planing hull runs at.</summary>
        const float PlaningShareOfRealSpeed = 0.45f;
        const float MetersPerSecondPerKnot = 1852f / 3600f;

        /// <summary>Solved EnginePower is a whole number, so a small hull lands within a few mm/s.</summary>
        const float ToleranceMps = 0.01f;

        enum Rule
        {
            /// <summary>0.45 x the audit's real-world top speed (knots), held under the go-fast cap.</summary>
            PlaningShareOfReal,
            /// <summary>A speed the owner ruled outright, in m/s.</summary>
            RuledMetersPerSecond,
            /// <summary>Not moved by the ruling: the audit's still-water top speed for her (knots).</summary>
            AuditUnchangedKnots,
        }

        /// <summary>One row per hull id. Real-world figures are the audit's "Real C/T kn" top column.</summary>
        static readonly (string Id, Rule Rule, float Figure)[] Table =
        {
            // ---- planing hulls: 0.45 x real-world top speed (knots) ----
            ("boat.bowrider",                 Rule.PlaningShareOfReal, 42.1f),
            ("boat.console_skiff",            Rule.PlaningShareOfReal, 36.9f),
            ("boat.sport_skiff",              Rule.PlaningShareOfReal, 36.9f),
            ("boat.sport_skiff_mk2",          Rule.PlaningShareOfReal, 36.9f),
            ("boat.sport_skiff_twin",         Rule.PlaningShareOfReal, 36.9f),
            ("boat.express_cruiser",          Rule.PlaningShareOfReal, 45f),
            ("boat.motor_yacht_84",           Rule.PlaningShareOfReal, 27f),
            ("boat.sport_fisher_convertible", Rule.PlaningShareOfReal, 40f),
            ("boat.sport_fisher_skybridge",   Rule.PlaningShareOfReal, 38f),
            ("boat.yacht_tender",             Rule.PlaningShareOfReal, 43.8f),
            ("boat.zodiac_frc",               Rule.PlaningShareOfReal, 43.8f),
            ("boat.zodiac_hurricane",         Rule.PlaningShareOfReal, 43.8f),

            // ---- the two the owner ruled outright (m/s). Neither leaves the water. ----
            ("boat.jet_ski",                  Rule.RuledMetersPerSecond, 5.5f),
            ("boat.go_fast",                  Rule.RuledMetersPerSecond, 10f),

            // ---- every other hull: unchanged, at the audit's still-water top speed (knots) ----
            ("boat.dory",                     Rule.AuditUnchangedKnots, 3.95f),   // rowed: both oars
            ("boat.dory_outboard",            Rule.AuditUnchangedKnots, 4.22f),
            ("boat.punt",                     Rule.AuditUnchangedKnots, 4.51f),
            ("boat.punt_upgraded",            Rule.AuditUnchangedKnots, 5.63f),
            ("boat.cape_islander",            Rule.AuditUnchangedKnots, 8.16f),
            ("boat.lobster_boat",             Rule.AuditUnchangedKnots, 8.45f),
            ("boat.lobster_inshore_hardtop_fundy",            Rule.AuditUnchangedKnots, 7.57f),
            ("boat.lobster_inshore_hardtop_newfoundland",     Rule.AuditUnchangedKnots, 7.57f),
            ("boat.lobster_inshore_hardtop_northumberland",   Rule.AuditUnchangedKnots, 7.57f),
            ("boat.lobster_inshore_open_fundy",               Rule.AuditUnchangedKnots, 7.57f),
            ("boat.lobster_inshore_open_newfoundland",        Rule.AuditUnchangedKnots, 7.57f),
            ("boat.lobster_inshore_open_northumberland",      Rule.AuditUnchangedKnots, 7.57f),
            ("boat.lobster_standard_hardtop_fundy",           Rule.AuditUnchangedKnots, 8.45f),
            ("boat.lobster_standard_hardtop_newfoundland",    Rule.AuditUnchangedKnots, 8.44f),
            ("boat.lobster_standard_hardtop_northumberland",  Rule.AuditUnchangedKnots, 8.45f),
            ("boat.lobster_standard_open_fundy",              Rule.AuditUnchangedKnots, 8.45f),
            ("boat.lobster_standard_open_newfoundland",       Rule.AuditUnchangedKnots, 8.44f),
            ("boat.lobster_standard_open_northumberland",     Rule.AuditUnchangedKnots, 8.45f),
            ("boat.lobster_offshore_hardtop_fundy",           Rule.AuditUnchangedKnots, 8.94f),
            ("boat.lobster_offshore_hardtop_newfoundland",    Rule.AuditUnchangedKnots, 8.94f),
            ("boat.lobster_offshore_hardtop_northumberland",  Rule.AuditUnchangedKnots, 8.94f),
            ("boat.lobster_offshore_open_fundy",              Rule.AuditUnchangedKnots, 8.94f),
            ("boat.lobster_offshore_open_newfoundland",       Rule.AuditUnchangedKnots, 8.94f),
            ("boat.lobster_offshore_open_northumberland",     Rule.AuditUnchangedKnots, 8.94f),
            ("boat.side_dragger",             Rule.AuditUnchangedKnots, 6.77f),
            ("boat.stern_trawler",            Rule.AuditUnchangedKnots, 6.70f),
            ("boat.stern_trawler_mk2",        Rule.AuditUnchangedKnots, 6.73f),
            ("boat.coastal_packet",           Rule.AuditUnchangedKnots, 6.18f),
            ("boat.tanker",                   Rule.AuditUnchangedKnots, 5.06f),
            ("boat.expedition_yacht_120",     Rule.AuditUnchangedKnots, 6.70f),
            ("boat.sloop_30",                 Rule.AuditUnchangedKnots, 5.50f),   // auxiliary motoring
            ("boat.sloop_88",                 Rule.AuditUnchangedKnots, 9.99f),   // auxiliary motoring
        };

        /// <summary>The share-of-real speed before the cap: what strict 0.45 x real would give her.</summary>
        static float UncappedPlaningSpeedMps(float realTopKnots)
            => PlaningShareOfRealSpeed * realTopKnots * MetersPerSecondPerKnot;

        static float RuledSpeedMps((string Id, Rule Rule, float Figure) row, float planingCapMps)
        {
            switch (row.Rule)
            {
                case Rule.PlaningShareOfReal: return Mathf.Min(UncappedPlaningSpeedMps(row.Figure), planingCapMps);
                case Rule.RuledMetersPerSecond: return row.Figure;
                default: return row.Figure * MetersPerSecondPerKnot;
            }
        }

        static GameConfig Config()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAssetPath);
            Assert.IsNotNull(config, "premise: the GameConfig asset loads from " + ConfigAssetPath);
            return config;
        }

        /// <summary>
        /// The cap every planing hull but the go-fast is held to: the go-fast's own still-water top
        /// speed, off her Def, less the GameConfig margin. Both halves are data.
        /// </summary>
        static float PlaningCapMps(IEnumerable<BoatHullDef> hulls)
        {
            BoatHullDef goFast = hulls.FirstOrDefault(h => h.Id == GoFastId);
            Assert.IsNotNull(goFast, "premise: the go-fast's Def is on disk; the cap hangs off her speed");
            float margin = Config().PlaningTopSpeedMarginUnderGoFastMps;
            Assert.Greater(margin, 0f,
                "GameConfig.PlaningTopSpeedMarginUnderGoFastMps must be above zero, or a capped hull ties " +
                "the go-fast instead of running just under her");
            return StillWaterTopSpeedMps(goFast) - margin;
        }

        /// <summary>
        /// The speed <see cref="BoatController"/>'s force model settles at in still water, off the hull's
        /// own numbers and the controller's own two constants: full drive against hull drag plus the
        /// body's damping on a mass of <c>MassKg / 100</c>. A rowed hull's full drive is both oars.
        /// </summary>
        static float StillWaterTopSpeedMps(BoatHullDef h)
        {
            float drive = h.Propulsion == PropulsionType.Oars ? 2f * h.OarPower : h.EnginePower;
            float resistance = h.ForwardDrag * BoatController.ForceFeelScale
                             + (h.MassKg / 100f) * BoatController.HullLinearDamping;
            return resistance > 0f ? drive * BoatController.ForceFeelScale / resistance : 0f;
        }

        static List<BoatHullDef> EveryHull()
        {
            List<BoatHullDef> hulls = AssetDatabase.FindAssets("t:BoatHullDef", new[] { DataBoats })
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<BoatHullDef>(path))
                .Where(h => h != null)
                .OrderBy(h => h.Id, System.StringComparer.Ordinal)
                .ToList();
            Assert.GreaterOrEqual(hulls.Count, Table.Length,
                "premise: every hull the table names is on disk under " + DataBoats);
            return hulls;
        }

        [Test]
        public void TheTable_NamesEachHullOnce()
        {
            string[] twice = Table.GroupBy(r => r.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
            CollectionAssert.IsEmpty(twice, "a hull with two rows has two ruled speeds");
        }

        [Test]
        public void EveryHullDef_RunsAtHerRowInTheSpeedTable()
        {
            Dictionary<string, (string Id, Rule Rule, float Figure)> rows = Table.ToDictionary(r => r.Id);
            var failures = new List<string>();
            var seen = new HashSet<string>();

            List<BoatHullDef> hulls = EveryHull();
            float cap = PlaningCapMps(hulls);

            foreach (BoatHullDef hull in hulls)
            {
                seen.Add(hull.Id);
                if (!rows.TryGetValue(hull.Id, out var row))
                {
                    failures.Add($"{hull.Id}: no row in the fleet speed table. A new hull takes a ruled speed " +
                                 "before she joins the fleet (docs/design/fleet-speed-and-fuel-audit.md).");
                    continue;
                }

                float ruled = RuledSpeedMps(row, cap);
                float actual = StillWaterTopSpeedMps(hull);
                if (Mathf.Abs(ruled - actual) > ToleranceMps)
                    failures.Add($"{hull.Id}: the table says {ruled:0.000} m/s ({ruled / MetersPerSecondPerKnot:0.00} kn, " +
                                 $"{row.Rule} {row.Figure}) but her Def settles at {actual:0.000} m/s. Her " +
                                 "EnginePower, ForwardDrag or MassKg moved without re-solving EnginePower for " +
                                 "the ruled speed.");
            }

            foreach (var row in Table)
                if (!seen.Contains(row.Id))
                    failures.Add($"{row.Id}: in the table but no hull Def with that id is on disk.");

            CollectionAssert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void TheGoFast_IsStrictlyTheFastestHullInTheFleet()
        {
            // The owner, 2026-11-13: she stays the fastest boat. Every hull Def on disk, planing or not,
            // settles strictly below the go-fast's own still-water top speed.
            List<BoatHullDef> hulls = EveryHull();
            BoatHullDef goFast = hulls.FirstOrDefault(h => h.Id == GoFastId);
            Assert.IsNotNull(goFast, "premise: the go-fast's Def is on disk");
            float hers = StillWaterTopSpeedMps(goFast);

            string[] asFastOrFaster = hulls
                .Where(h => h.Id != GoFastId && StillWaterTopSpeedMps(h) >= hers)
                .Select(h => $"{h.Id} settles at {StillWaterTopSpeedMps(h):0.000} m/s")
                .ToArray();
            CollectionAssert.IsEmpty(asFastOrFaster,
                $"the go-fast ({hers:0.000} m/s) is the fastest hull in the fleet, but:\n" +
                string.Join("\n", asFastOrFaster));
        }

        [Test]
        public void NoPlaningHull_ButTheGoFast_ExceedsTheCapJustUnderHer()
        {
            // The cap itself, by name: the go-fast's Def speed less the GameConfig margin. Only the hulls
            // strict 0.45 x real would have put past the cap sit ON it; a hull under it keeps her 0.45 row.
            List<BoatHullDef> hulls = EveryHull();
            float cap = PlaningCapMps(hulls);
            Dictionary<string, (string Id, Rule Rule, float Figure)> rows = Table.ToDictionary(r => r.Id);
            var failures = new List<string>();

            foreach (BoatHullDef hull in hulls)
            {
                if (hull.Id == GoFastId || !rows.TryGetValue(hull.Id, out var row)) continue;
                if (row.Rule == Rule.AuditUnchangedKnots) continue;

                float actual = StillWaterTopSpeedMps(hull);
                if (actual > cap + ToleranceMps)
                    failures.Add($"{hull.Id}: settles at {actual:0.000} m/s, past the {cap:0.000} m/s cap");

                bool strictWouldPassTheCap = row.Rule == Rule.PlaningShareOfReal
                                             && UncappedPlaningSpeedMps(row.Figure) > cap;
                if (strictWouldPassTheCap && Mathf.Abs(actual - cap) > ToleranceMps)
                    failures.Add($"{hull.Id}: 0.45 x real ({UncappedPlaningSpeedMps(row.Figure):0.000} m/s) passes " +
                                 $"the cap, so she runs AT the cap ({cap:0.000} m/s), not {actual:0.000}");
            }

            CollectionAssert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void ThePlaningRows_AreTheHullsTheFuelModelCallsPlaning()
        {
            // PR 1134's profiles are the one place a hull's hydrodynamic class is authored. The speed
            // ruling is "planing hulls run at 0.45 x real", so the hulls on the planing fuel curve are
            // exactly the hulls with a planing row or an outright ruled speed (the jetski and go-fast are
            // planing hulls the owner gave their own number).
            string[] planingByFuelModel = AssetDatabase.FindAssets("t:FuelSpeedProfileDef", new[] { FuelSpeedFolder })
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<FuelSpeedProfileDef>(path))
                .Where(p => p != null && p.Hull != null && p.Curve != null
                            && p.Curve.HullClass == FuelSpeedHullClass.Planing)
                .Select(p => p.Hull.Id)
                .OrderBy(id => id, System.StringComparer.Ordinal)
                .ToArray();
            Assert.IsNotEmpty(planingByFuelModel, "premise: the planing fuel profiles load");

            string[] planingBySpeedTable = Table
                .Where(r => r.Rule != Rule.AuditUnchangedKnots)
                .Select(r => r.Id)
                .OrderBy(id => id, System.StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(planingByFuelModel, planingBySpeedTable,
                "the hulls on the planing fuel curve and the hulls on a planing speed row must be one list");
        }

        [Test]
        public void TheRuling_MovedNoHullsMassOrDrag_OnlyHerThrust()
        {
            // The ruled speeds were applied by solving EnginePower alone. If a planing hull's speed is
            // right but her thrust is not what her own mass and drag ask for at that speed, somebody
            // reached the number by moving mass or drag instead, which also moves her pickup, her
            // momentum and how the sea handles her.
            Dictionary<string, (string Id, Rule Rule, float Figure)> rows = Table.ToDictionary(r => r.Id);
            List<BoatHullDef> hulls = EveryHull();
            float cap = PlaningCapMps(hulls);
            foreach (BoatHullDef hull in hulls)
            {
                if (!rows.TryGetValue(hull.Id, out var row) || row.Rule == Rule.AuditUnchangedKnots) continue;
                float resistance = hull.ForwardDrag + hull.MassKg * BoatController.HullLinearDamping;
                float solved = Mathf.Round(RuledSpeedMps(row, cap) * resistance);
                Assert.AreEqual(solved, hull.EnginePower, 1f,
                    $"{hull.Id}: EnginePower is solved as ruled speed x (ForwardDrag + {BoatController.HullLinearDamping} x MassKg), " +
                    "rounded to a whole unit");
            }
        }
    }
}
