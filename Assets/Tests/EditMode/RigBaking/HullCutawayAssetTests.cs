using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>Has the fleet actually been RE-BAKED?</b> — the committed-asset half of the cutaway,
    /// deliberately separate from <see cref="HullLevelTagBakeTests"/>.
    ///
    /// <para>That fixture proves the mechanism against a fresh extraction and would stay green over
    /// a project whose committed meshes had never been touched. This one asks the other question,
    /// and it is the one this repo keeps losing: <i>a builder-generated asset went stale and the
    /// boat got debugged in the code.</i> The failure message therefore names the bake, not the
    /// bug.</para>
    ///
    /// <para><b>Scoped to batch 1 on purpose.</b> The cutaway kit landed three rigs on 2026-08-26;
    /// the dragger, the trawler Mk II, the tanker and the eighteen lobster variants are batch 2 and
    /// their rigs publish no <c>geometry()</c> yet. Asserting over the whole fleet would go red for
    /// hulls nobody has been asked to change. The <i>selection</i> is what has to be defended, so
    /// the last test here asserts that every hull outside batch 1 whose RIG has gained a vocabulary
    /// gets re-baked too — which is what makes this fixture notice batch 2's arrival instead of
    /// quietly ignoring it.</para>
    /// </summary>
    public sealed class HullCutawayAssetTests
    {
        private static readonly string[] Batch1 = { "lobsterBoat", "sternTrawler", "coastalPacket" };

        private const string ReBake =
            "\n\nRe-bake: Hidden Harbours ▸ Dev ▸ 3D Hulls ▸ Bake the 3 cutaway batch-1 hull meshes " +
            "(headless: -executeMethod HiddenHarbours.Tools.RigBaking.RigMeshAssetBaker" +
            ".BakeCutawayBatch1Cli), and commit the result.";

        private static IEnumerable<FleetHull> Batch1Hulls =>
            HullMeshFleet.Hulls.Where(h => Batch1.Contains(h.Key));

        [Test]
        public void EveryBatch1Hull_CarriesHerLevelTable_AndAMeshTaggedToMatch()
        {
            var stale = new List<string>();

            foreach (FleetHull hull in Batch1Hulls)
            {
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(hull.MeshAssetPath);
                Assert.IsNotNull(def, $"{hull.Key}: no committed def at {hull.MeshAssetPath}.");
                Assert.IsNotNull(def.Mesh, $"{hull.Key}: the mesh sub-asset is missing.");

                if (def.LevelTags == null || def.LevelTags.Length == 0)
                    stale.Add($"{hull.Key}: LevelTags is empty — the def has no idea which of her " +
                              "levels is which, so a cabin cannot ask for a cut");
                else if (!def.Mesh.HasVertexAttribute(VertexAttribute.TexCoord1))
                    stale.Add($"{hull.Key}: the def carries {def.LevelTags.Length} level rows but her " +
                              "MESH has no TexCoord1 — a table with nothing to point at");
                else if (!def.CarriesLevelTags)
                    stale.Add($"{hull.Key}: CarriesLevelTags is false with both halves present, " +
                              "which should be unreachable");
            }

            CollectionAssert.IsEmpty(stale,
                "These hulls' rigs publish a cutaway vocabulary but their committed meshes do not " +
                "carry it:\n  " + string.Join("\n  ", stale) + ReBake);
        }

        /// <summary>
        /// <b>Every enclosed level in the committed table joins to a real interior level, and every
        /// open one refuses the cut.</b>
        ///
        /// <para>The join is the whole defence against this fleet's worst shipped defect — one
        /// vocabulary indexed by another's order drew the tanker's engine space every time the
        /// player walked into her wheelhouse. Here it is asserted on the ASSETS, because that is the
        /// pair the game actually loads.</para>
        /// </summary>
        [Test]
        public void EveryCommittedLevelRow_ResolvesTheWayTheGateWillAskIt()
        {
            var wrong = new List<string>();

            foreach (FleetHull hull in Batch1Hulls)
            {
                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(hull.MeshAssetPath);
                if (def == null || def.LevelTags == null || def.LevelTags.Length == 0) continue;

                BoatInteriorDef interior = InteriorDefFor(hull);
                var defIds = interior?.Levels == null
                    ? new HashSet<string>()
                    : new HashSet<string>(interior.Levels.Where(l => l != null).Select(l => l.Id));

                foreach (HullMeshDef.LevelTag row in def.LevelTags)
                {
                    int asked = def.CutawayTagForDeck(row.DeckId);

                    if (!row.Enclosed)
                    {
                        if (asked != 0)
                            wrong.Add($"{hull.Key}.{row.LevelId}: declared OPEN yet CutawayTagForDeck " +
                                      $"answers {asked}. Cutting an open level is cutting the sky.");
                        continue;
                    }

                    if (row.Tag == 0)
                        wrong.Add($"{hull.Key}.{row.LevelId}: an enclosed level tagged 0, which is " +
                                  "'hull' AND the gate's off value — she could never be cut.");
                    if (asked != row.Tag)
                        wrong.Add($"{hull.Key}.{row.LevelId}: CutawayTagForDeck('{row.DeckId}') " +
                                  $"answers {asked}, the row says {row.Tag}.");
                    if (defIds.Count > 0 && !string.IsNullOrEmpty(row.DeckId) && !defIds.Contains(row.DeckId))
                        wrong.Add($"{hull.Key}.{row.LevelId}: deck '{row.DeckId}' is not a level of " +
                                  $"interior def '{interior.Id}' [{string.Join(", ", defIds)}].");
                }

                Assert.AreEqual(0, def.CutawayTagForDeck("no_such_level"),
                    $"{hull.Key}: an unknown deck id must answer 0 — a guess here is a house that " +
                    "opens on the wrong boat.");
            }

            CollectionAssert.IsEmpty(wrong, string.Join("\n  ", wrong) + ReBake);
        }

        /// <summary>
        /// <b>The batch-1 selection is not allowed to go stale.</b> When batch 2's rigs land, this
        /// goes red for each hull whose rig has gained a vocabulary its committed mesh has not — so
        /// the arrival of new pass-3 rigs is a test failure with a bake instruction attached, rather
        /// than a feature that silently does not happen on the new hulls.
        /// </summary>
        [Test]
        public void NoHullOutsideBatch1_HasARigThatOutranHerBake()
        {
            var owed = new List<string>();

            foreach (FleetHull hull in HullMeshFleet.Hulls)
            {
                if (Batch1.Contains(hull.Key)) continue;

                var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(hull.MeshAssetPath);
                if (def == null) continue;
                bool meshTagged = def.Mesh != null
                                  && def.Mesh.HasVertexAttribute(VertexAttribute.TexCoord1);
                if (meshTagged && def.LevelTags != null && def.LevelTags.Length > 0) continue;

                using IRigScriptHost host = RigScriptHostFactory.Create();
                RigMeshData fresh = RigMeshExtractor.ExtractFrom(host, hull.ScriptPath,
                                                                 hull.GlobalName, hull: hull.Extraction);
                if (!fresh.CarriesLevelTags) continue;   // still pre-cutaway: nothing owed

                owed.Add($"{hull.Key}: her rig now publishes " +
                         $"[{string.Join(", ", fresh.LevelIds.OrderBy(kv => kv.Value).Select(kv => kv.Key))}] " +
                         "and her committed mesh carries none of it");
            }

            CollectionAssert.IsEmpty(owed,
                "Cutaway batch 2 has arrived upstream and these hulls have not been re-baked, so " +
                "their houses will never open:\n  " + string.Join("\n  ", owed) +
                "\n\nRe-bake them through the whole-fleet entry point (Bake ALL fleet hull meshes / " +
                "Bake the 18 lobster variant hull meshes), and retire the batch-1-only entry point " +
                "with them.");
        }

        private static BoatInteriorDef InteriorDefFor(FleetHull hull)
        {
            if (hull.VisualAssetPaths == null) return null;
            foreach (string path in hull.VisualAssetPaths)
            {
                var visual = AssetDatabase.LoadAssetAtPath<Boats.BoatVisualDef>(path);
                if (visual != null && visual.Interior != null) return visual.Interior;
            }
            return null;
        }
    }
}
