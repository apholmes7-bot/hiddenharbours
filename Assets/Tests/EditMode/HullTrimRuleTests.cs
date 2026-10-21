using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>HER TRIM FOLLOWS HER HULL AND HER MOTOR — the rule's promises</b> (the owner on #893,
    /// 2026-09-26: each boat's feel is set from her hull and her motor, and heavy boats rise gently and
    /// never plane by that same rule; design <c>boats-and-navigation.md</c> §2.7.3). Her FORM sets the
    /// curve and her BUILD (how heavy she is for her length) scales it. Her motor needs no number of its
    /// own: the law already reads the speed her drive can hold and the push her thrust gives her mass.
    ///
    /// <para>These tests guard what the rule PROMISES about the shipped hulls, never its formula. A
    /// guard that recomputed the table would only ask the data to agree with itself, and would pass a
    /// wrong rule. So any number can be retuned and stay green while the promises hold. The forms are
    /// named here hull by hull, so a new hull is placed in one on purpose.</para>
    /// </summary>
    public class HullTrimRuleTests
    {
        const string DataBoats = "Assets/_Project/Data/Boats";
        const string ConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";

        enum Form { Planing, SemiDisplacement, SmallOpen, Sail, Ship }

        // Every hull under Data/Boats, by id, in the form the rule gives her (§2.7.3).
        static readonly Dictionary<string, Form> FormOf = new()
        {
            // Planing: she rises to her hump, then settles onto the plane.
            ["boat.sport_skiff"] = Form.Planing,
            ["boat.sport_skiff_mk2"] = Form.Planing,
            ["boat.sport_skiff_twin"] = Form.Planing,
            ["boat.zodiac_frc"] = Form.Planing,
            ["boat.zodiac_hurricane"] = Form.Planing,
            // Semi-displacement: she rises and holds it.
            ["boat.console_skiff"] = Form.SemiDisplacement,
            ["boat.cape_islander"] = Form.SemiDisplacement,
            ["boat.lobster_boat"] = Form.SemiDisplacement,
            ["boat.lobster_inshore_hardtop_fundy"] = Form.SemiDisplacement,
            ["boat.lobster_inshore_hardtop_newfoundland"] = Form.SemiDisplacement,
            ["boat.lobster_inshore_hardtop_northumberland"] = Form.SemiDisplacement,
            ["boat.lobster_inshore_open_fundy"] = Form.SemiDisplacement,
            ["boat.lobster_inshore_open_newfoundland"] = Form.SemiDisplacement,
            ["boat.lobster_inshore_open_northumberland"] = Form.SemiDisplacement,
            ["boat.lobster_standard_hardtop_fundy"] = Form.SemiDisplacement,
            ["boat.lobster_standard_hardtop_newfoundland"] = Form.SemiDisplacement,
            ["boat.lobster_standard_hardtop_northumberland"] = Form.SemiDisplacement,
            ["boat.lobster_standard_open_fundy"] = Form.SemiDisplacement,
            ["boat.lobster_standard_open_newfoundland"] = Form.SemiDisplacement,
            ["boat.lobster_standard_open_northumberland"] = Form.SemiDisplacement,
            ["boat.lobster_offshore_hardtop_fundy"] = Form.SemiDisplacement,
            ["boat.lobster_offshore_hardtop_newfoundland"] = Form.SemiDisplacement,
            ["boat.lobster_offshore_hardtop_northumberland"] = Form.SemiDisplacement,
            ["boat.lobster_offshore_open_fundy"] = Form.SemiDisplacement,
            ["boat.lobster_offshore_open_newfoundland"] = Form.SemiDisplacement,
            ["boat.lobster_offshore_open_northumberland"] = Form.SemiDisplacement,
            ["boat.sport_fisher_convertible"] = Form.SemiDisplacement,
            ["boat.sport_fisher_skybridge"] = Form.SemiDisplacement,
            // Small open boats: a small rise she holds.
            ["boat.dory"] = Form.SmallOpen,
            ["boat.dory_outboard"] = Form.SmallOpen,
            ["boat.punt"] = Form.SmallOpen,
            ["boat.punt_upgraded"] = Form.SmallOpen,
            // Sail: a small rise she holds.
            ["boat.sloop_30"] = Form.Sail,
            ["boat.sloop_88"] = Form.Sail,
            // Ships: level.
            ["boat.side_dragger"] = Form.Ship,
            ["boat.stern_trawler"] = Form.Ship,
            ["boat.stern_trawler_mk2"] = Form.Ship,
            ["boat.coastal_packet"] = Form.Ship,
            ["boat.tanker"] = Form.Ship,
        };

        static List<BoatHullDef> Hulls()
        {
            var list = new List<BoatHullDef>();
            foreach (string guid in AssetDatabase.FindAssets("t:BoatHullDef", new[] { DataBoats }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>(path);
                Assert.IsNotNull(hull, $"{path} does not load as a hull");
                list.Add(hull);
            }
            Assert.Greater(list.Count, 0, $"harness: no hulls found under {DataBoats}");
            return list;
        }

        static Form FormOfHull(BoatHullDef hull)
        {
            Assert.IsTrue(FormOf.TryGetValue(hull.Id ?? "", out Form form),
                $"{hull.Id} has no form named here: place her in one on purpose (§2.7.3)");
            return form;
        }

        // Every two hulls of one form, each pair once.
        static IEnumerable<(BoatHullDef a, BoatHullDef b)> PairsWithinAForm()
        {
            var hulls = Hulls();
            for (int i = 0; i < hulls.Count; i++)
                for (int j = i + 1; j < hulls.Count; j++)
                    if (FormOfHull(hulls[i]) == FormOfHull(hulls[j]))
                        yield return (hulls[i], hulls[j]);
        }

        // How heavy she is for her length: her mass over the cube of her length, which a hull scaled
        // up whole keeps. In doubles, so two hulls built alike compare equal.
        static double WeightForHerLength(BoatHullDef hull)
        {
            double l = hull.LengthMeters;
            return hull.MassKg / (l * l * l);
        }

        [Test]
        public void EveryHullUnderDataBoats_HasItsFormNamedHere()
        {
            var seen = new HashSet<string>();
            foreach (var hull in Hulls())
            {
                Assert.IsFalse(string.IsNullOrEmpty(hull.Id), $"{AssetDatabase.GetAssetPath(hull)} has no id");
                Assert.IsTrue(seen.Add(hull.Id), $"{hull.Id} is shipped twice");
                FormOfHull(hull);
            }
            foreach (string id in FormOf.Keys)
                Assert.IsTrue(seen.Contains(id), $"{id} is named here, but no hull under {DataBoats} carries it");
        }

        [Test]
        public void WithinEachForm_AHullHeavierForHerLength_NeverHumpsLower()
        {
            int compared = 0;
            foreach (var (a, b) in PairsWithinAForm())
            {
                double wa = WeightForHerLength(a), wb = WeightForHerLength(b);
                if (wa == wb) continue;
                var (heavier, lighter) = wa > wb ? (a, b) : (b, a);
                compared++;
                Assert.GreaterOrEqual(heavier.TrimHumpDegrees, lighter.TrimHumpDegrees,
                    $"{FormOf[a.Id]}: {heavier.Id} ({WeightForHerLength(heavier):0.000} kg/m³) is heavier " +
                    $"for her length than {lighter.Id} ({WeightForHerLength(lighter):0.000}), so she rises no less " +
                    $"({heavier.TrimHumpDegrees}° against {lighter.TrimHumpDegrees}°)");
            }
            Assert.Greater(compared, 0, "harness: some hulls of one form differ in build");
        }

        [Test]
        public void NoNonPlaningHull_HasAPlaningDrop()
        {
            bool aPlaningHullDrops = false;
            foreach (var hull in Hulls())
            {
                Form form = FormOfHull(hull);
                if (form == Form.Planing)
                {
                    aPlaningHullDrops |= hull.TrimPlaningDropDegrees > 0f;
                    continue;
                }
                Assert.AreEqual(0f, hull.TrimPlaningDropDegrees,
                    $"{hull.Id} ({form}) never planes, so she gives back none of her rise");
            }
            Assert.IsTrue(aPlaningHullDrops, "harness: the planing hulls author a drop, so it is a live number");
        }

        [Test]
        public void TheShips_AreLevel()
        {
            int ships = 0;
            foreach (var hull in Hulls())
            {
                if (FormOfHull(hull) != Form.Ship) continue;
                ships++;
                Assert.AreEqual(0f, hull.TrimHumpDegrees, $"{hull.Id}: a ship does not rise");
                Assert.AreEqual(0f, hull.TrimPlaningDropDegrees, $"{hull.Id}: a ship does not plane");
                Assert.AreEqual(0f, hull.TrimAccelDegreesPerMps2, $"{hull.Id}: a ship does not squat");
                Assert.AreEqual(0f, hull.TrimDecelDegreesPerMps2, $"{hull.Id}: a ship does not dip");
            }
            int named = 0;
            foreach (Form form in FormOf.Values)
                if (form == Form.Ship) named++;
            Assert.AreEqual(named, ships, "harness: every ship named here was loaded and read");
        }

        [Test]
        public void WithinEachForm_ALongerHull_NeverHasAShorterLag()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAssetPath);
            Assert.IsNotNull(config, $"missing: {ConfigAssetPath}");
            HullTrimSettings policy = config.HullTrim;
            Assert.IsTrue(policy.Enabled, "harness: the shipped policy is on, so each hull resolves her own lag");

            int compared = 0;
            foreach (var (a, b) in PairsWithinAForm())
            {
                if (a.LengthMeters == b.LengthMeters) continue;
                var (longer, shorter) = a.LengthMeters > b.LengthMeters ? (a, b) : (b, a);
                compared++;
                // The lag she is drawn with: her own, or the policy's where she leaves hers at 0.
                float longerLag = HullTrimMath.Resolve(longer, policy).ResponseSeconds;
                float shorterLag = HullTrimMath.Resolve(shorter, policy).ResponseSeconds;
                Assert.GreaterOrEqual(longerLag, shorterLag,
                    $"{FormOf[a.Id]}: {longer.Id} ({longer.LengthMeters} m) is longer than {shorter.Id} " +
                    $"({shorter.LengthMeters} m), so she answers no faster ({longerLag} s against {shorterLag} s)");
            }
            Assert.Greater(compared, 0, "harness: some hulls of one form differ in length");
        }
    }
}
