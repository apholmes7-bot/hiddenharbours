using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Content validation for the drifting-seaweed data (ADR 0003 "content is data"): every
    /// <see cref="SeaweedDef"/> under <c>Data/</c> has a stable unique id and sane owner tunables —
    /// especially the ORDERINGS the behaviour maths assumes (refloat above strand = the hysteresis;
    /// rest inside snag; an ascending size ladder) which hand-authored YAML can break silently — and
    /// the Resources <see cref="SeaweedLibrary"/> resolves and lists every bed. Follows the per-lane
    /// pattern of <c>AmbientFleetContentTests</c> (each role validates its own Defs).
    /// </summary>
    public class SeaweedContentTests
    {
        private const string DataRoot = "Assets/_Project/Data";

        private static List<SeaweedDef> LoadAll()
        {
            var list = new List<SeaweedDef>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(SeaweedDef)}", new[] { DataRoot }))
            {
                var def = AssetDatabase.LoadAssetAtPath<SeaweedDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) list.Add(def);
            }
            return list;
        }

        [Test]
        public void SeaweedDefs_Exist_AndHaveUniqueStableIds()
        {
            var defs = LoadAll();
            Assert.IsNotEmpty(defs, "the working coast ships at least one SeaweedDef in Data/");

            var seen = new HashSet<string>();
            foreach (var def in defs)
            {
                string path = AssetDatabase.GetAssetPath(def);
                Assert.IsFalse(string.IsNullOrWhiteSpace(def.Id), $"{path}: blank id");
                StringAssert.StartsWith("decor.", def.Id, $"{path}: def ids are type.snake_case");
                Assert.IsTrue(seen.Add(def.Id), $"{path}: duplicate seaweed id '{def.Id}'");
            }
        }

        [Test]
        public void SeaweedDefs_HaveSaneOwnerTunables()
        {
            foreach (var def in LoadAll())
            {
                string path = AssetDatabase.GetAssetPath(def);

                Assert.That(def.PieceCount, Is.InRange(1, 64),
                            $"{path}: PieceCount bounds the sprite pool (rule 7)");
                Assert.Greater(def.BedSize.x * def.BedSize.y, 0f, $"{path}: degenerate bed rect");
                Assert.Greater(def.RespawnSeconds, 0f, $"{path}: a merged piece must eventually respawn");
                Assert.Greater(def.MergeRadiusMeters, 0f, $"{path}: a zero merge radius = no clumping");
                Assert.Greater(def.MaxDriftSpeedMetersPerSecond, 0f, $"{path}: the drift cap must be positive");

                // The hysteresis that stops waterline flicker: refloat must sit ABOVE strand.
                Assert.Greater(def.RefloatDepthMeters, def.StrandDepthMeters,
                               $"{path}: RefloatDepthMeters must sit ABOVE StrandDepthMeters — the gap " +
                               "is the hysteresis that stops a piece flickering on the waterline");

                // A snagged piece rests ON the rim it was caught at (or inside it) — never outside.
                Assert.LessOrEqual(def.BuoyRestRadiusMeters, def.BuoySnagRadiusMeters,
                                   $"{path}: BuoyRestRadiusMeters outside the snag radius would teleport " +
                                   "the weed AWAY from the buoy it just fouled on");

                // Spawns must land afloat: seeding at/below the strand gate would beach fresh weed instantly.
                Assert.Greater(def.MinSpawnDepthMeters, def.StrandDepthMeters,
                               $"{path}: MinSpawnDepthMeters must clear the strand gate or fresh weed " +
                               "spawns already beached");

                Assert.IsNotNull(def.TierSizesMeters, $"{path}: no size ladder");
                Assert.IsNotEmpty(def.TierSizesMeters, $"{path}: the size ladder needs at least one rung");
                for (int t = 0; t < def.TierSizesMeters.Length; t++)
                {
                    Assert.Greater(def.TierSizesMeters[t], 0f, $"{path}: TierSizesMeters[{t}] must be positive");
                    if (t > 0)
                        Assert.Greater(def.TierSizesMeters[t], def.TierSizesMeters[t - 1],
                                       $"{path}: the size ladder must ascend — merges GROW the clump");
                }
                Assert.AreEqual(def.TierSizesMeters.Length - 1, def.MaxTier,
                                $"{path}: MaxTier is the ladder's top rung");

                Assert.IsNotNull(def.Palette, $"{path}: no palette");
                Assert.IsNotEmpty(def.Palette, $"{path}: the weed needs at least one tone");
                Assert.That(def.MaxAlpha, Is.InRange(0f, 1f),
                            $"{path}: MaxAlpha is a 0..1 opacity (hand-authored YAML can dodge the Range clamp)");
            }
        }

        [Test]
        public void Library_Resolves_AndListsEveryBedDef()
        {
            var lib = Resources.Load<SeaweedLibrary>(SeaweedLibrary.ResourcesPath);
            Assert.IsNotNull(lib, "Resources/SeaweedLibrary.asset must exist — the presenter boots from it");
            Assert.IsNotNull(lib.Beds);

            var listed = new HashSet<SeaweedDef>();
            foreach (var def in lib.Beds)
            {
                Assert.IsNotNull(def, "the library holds a null bed entry (a broken guid reference?)");
                listed.Add(def);
            }
            foreach (var def in LoadAll())
                Assert.IsTrue(listed.Contains(def),
                              $"{AssetDatabase.GetAssetPath(def)} is not listed in the SeaweedLibrary — the bed would never spawn");
        }

        [Test]
        public void StPetersBed_IsAuthored_AndSitsInTheHarbourWater()
        {
            foreach (var def in LoadAll())
            {
                if (def.Id != "decor.seaweed_st_peters") continue;
                Assert.AreEqual("StPeters", def.RegionSceneName,
                                "the St Peters bed activates with the StPeters scene");
                return;
            }
            Assert.Fail("the St Peters seaweed bed (decor.seaweed_st_peters) is missing");
        }
    }
}
