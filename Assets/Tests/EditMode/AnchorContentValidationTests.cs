using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>Every shipped hull has anchor data</b> — the owner's ruling ("an anchor on every hull") held
    /// against the ACTUAL <c>Data/Boats</c> assets, the <c>ContentValidationTests</c> pattern.
    ///
    /// <para><b>Why this test reads the YAML and not just the loaded object.</b>
    /// <see cref="BoatHullDef.HasAnchor"/> is declared <c>= true</c>, but that initializer is C#: Unity
    /// never runs it on an asset it loads, so a hull whose file omits the key deserializes as
    /// <b>false</b> and quietly loses her ground tackle. That is the GameConfig YAML trap (#420) in its
    /// hull-asset form, and it is invisible to an <c>AssetDatabase.LoadAssetAtPath</c> assertion the day
    /// somebody re-serializes an asset with an older schema. So the file is checked for the KEY, and the
    /// loaded object for the VALUE — the two halves catch different failures.</para>
    ///
    /// <para>The fleet asset (<c>fleet.st_peters_ambient</c>) is not a hull and is not in scope; every
    /// <see cref="BoatHullDef"/> under <c>Data/</c> is.</para>
    /// </summary>
    public class AnchorContentValidationTests
    {
        private const string DataRoot = "Assets/_Project/Data";

        private static List<BoatHullDef> AllHulls()
        {
            var list = new List<BoatHullDef>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(BoatHullDef)}", new[] { DataRoot }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<BoatHullDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        private static string ReadAsset(Object asset)
        {
            string repoRelative = AssetDatabase.GetAssetPath(asset);
            string path = Path.Combine(Application.dataPath, "..", repoRelative);
            Assert.IsTrue(File.Exists(path), $"missing on disk: {repoRelative}");
            return File.ReadAllText(path);
        }

        [Test]
        public void EveryShippedHull_StatesHerGroundTackle_InTheFileNotJustInCode()
        {
            var hulls = AllHulls();
            Assert.IsNotEmpty(hulls, "the fleet ships hull assets");

            string[] keys = { nameof(BoatHullDef.HasAnchor), nameof(BoatHullDef.AnchorMassKg),
                              nameof(BoatHullDef.RodeMeters) };

            foreach (BoatHullDef hull in hulls)
            {
                string path = AssetDatabase.GetAssetPath(hull);
                string yaml = ReadAsset(hull);
                foreach (string key in keys)
                    Assert.IsTrue(Regex.IsMatch(yaml, $@"^\s*{key}:\s*\S+\s*$", RegexOptions.Multiline),
                        $"{path} ({hull.Id}) does not serialize {key}. Unity deserializes an absent key " +
                        $"to the C# default — false / 0 — NOT to the field initializer, so this hull " +
                        $"would quietly lose her anchor. Add the key to the asset.");
            }
        }

        [Test]
        public void EveryShippedHull_CarriesAHook_AndARodeToGoWithIt()
        {
            var settings = AnchorSettings.Default;

            foreach (BoatHullDef hull in AllHulls())
            {
                string path = AssetDatabase.GetAssetPath(hull);

                Assert.IsTrue(hull.HasAnchor,
                    $"{path} ({hull.Id}) carries no anchor. The owner's ruling is an anchor on EVERY " +
                    $"hull; a boat that genuinely has none is a deliberate exception and needs a note " +
                    $"here saying so.");
                Assert.IsTrue(AnchorMath.CarriesAnchor(hull), $"{path}: the resolver disagrees with the data");

                Assert.Greater(hull.AnchorMassKg, 0f,
                    $"{path} ({hull.Id}) states no hook weight. 0 silently falls back to the shared " +
                    $"dinghy grapnel, which is right for an un-authored hull and wrong for a shipped one.");
                Assert.Greater(hull.RodeMeters, 0f,
                    $"{path} ({hull.Id}) states no rode. 0 falls back to the dinghy-class default.");

                // The resolvers are what the sim, the key and the switch actually read — so assert
                // through them, not around them.
                Assert.AreEqual(hull.AnchorMassKg, AnchorMath.AnchorMassFor(hull, in settings), 1e-3f);
                Assert.AreEqual(hull.RodeMeters, AnchorMath.RodeFor(hull, in settings), 1e-3f);
                Assert.Greater(AnchorMath.DragBrakeStrengthFor(hull.AnchorMassKg, in settings), 0f,
                    $"{path}: her dragging tackle checks her with nothing at all");
            }
        }

        [Test]
        public void TheTackleLadder_GrowsWithTheHull_AcrossTheThreeClassesTheGamePlays()
        {
            BoatHullDef dory = Hull("Dory");                    // rowed, no helm, no dash
            BoatHullDef skiff = Hull("ConsoleSkiff");           // engine + skiff console dash
            BoatHullDef lobster = Hull("LobsterBoat");          // engine + pilothouse dash

            // P2 in one column: a bigger boat swings more iron on more line, and so anchors deeper.
            Assert.Less(dory.AnchorMassKg, skiff.AnchorMassKg);
            Assert.Less(skiff.AnchorMassKg, lobster.AnchorMassKg);
            Assert.Less(dory.RodeMeters, skiff.RodeMeters);
            Assert.Less(skiff.RodeMeters, lobster.RodeMeters);

            // …and that the three really are the three classes the anchor has to work on: one hull with
            // no helm at all, one skiff console, one pilothouse. If this ever stops being true the
            // PlayMode coverage is testing one shape three times.
            Assert.AreEqual(PropulsionType.Oars, dory.Propulsion, "the dory is rowed — no helm to switch");
            // Unity's own == (never Assert.IsNull): a destroyed or absent asset reference is a
            // fake-null that a reference comparison sails straight through.
            Assert.IsTrue(dory.Helm == null, "…and no console either");
            Assert.IsNotNull(skiff.Helm, "the console skiff has a dash");
            Assert.AreEqual(ConsoleRigKind.Console, skiff.Helm.Rig);
            Assert.IsNotNull(lobster.Helm, "the lobster boat has a wheelhouse dash");
            Assert.IsTrue(HiddenHarbours.UI.HelmDashGeometry.IsPilothouse(lobster.Helm.Rig),
                "…of the OTHER helm family — the two draw their anchor switch differently");
        }

        private static BoatHullDef Hull(string name)
        {
            var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>($"{DataRoot}/Boats/{name}.asset");
            Assert.IsNotNull(hull, $"missing hull asset: {name}");
            return hull;
        }
    }
}
