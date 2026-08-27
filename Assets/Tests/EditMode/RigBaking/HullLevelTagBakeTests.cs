using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>The per-face CUTAWAY tag, adjudicated at the bake</b> — the owner's 2026-08-26 ruling
    /// (exterior with the house cut away below decks) as a set of facts about extraction rather
    /// than about pixels.
    ///
    /// <para><b>These run against a FRESH extraction, not the committed assets.</b> That is
    /// deliberate and it is the same lever <c>HullMeshFleetTests.EveryCommittedHullMesh_MatchesA
    /// FreshExtractionFromItsRig</c> pulls: a change to the extractor can be proven against every
    /// rig in the kit without re-baking a single asset, so the mechanism lands and is tested before
    /// any bytes move. The committed side — "has the fleet actually been re-baked yet?" — is
    /// <see cref="HullCutawayAssetTests"/>, and it is a separate question with a separate
    /// answer.</para>
    ///
    /// <para><b>Nothing here transcribes the kit's id table.</b> The README says ships run
    /// <c>hull 0 · main_deck 1 · house 2 · bridge 3 · below 4 · rigging 5</c> and the lobster family
    /// <c>hull 0 · cockpit 1 · foredeck 2 · house 3 · cuddy 4 · rigging 5</c> — but asserting those
    /// literals would pin a document, not a mechanism, and would go red the day batch 2 adds a
    /// level. What is asserted instead is the handful of STRUCTURAL properties the gate actually
    /// depends on, each read back out of the rig itself.</para>
    /// </summary>
    public sealed class HullLevelTagBakeTests
    {
        /// <summary>The cutaway kit's batch 1 — the lobster (the render-verified reference) and the
        /// two ships that share a sole and therefore break the tie in data.</summary>
        private static readonly string[] Pass3Keys = { "lobsterBoat", "sternTrawler", "coastalPacket" };

        private static IEnumerable<FleetHull> Pass3Hulls =>
            HullMeshFleet.Hulls.Where(h => Pass3Keys.Contains(h.Key));

        private static IEnumerable<FleetHull> UntaggedHulls =>
            HullMeshFleet.Hulls.Where(h => !Pass3Keys.Contains(h.Key)
                                           && !h.ScriptPath.Contains("Fleet")).Take(3);

        private static RigMeshData Extract(FleetHull hull, out IRigScriptHost host)
        {
            host = RigScriptHostFactory.Create();
            return RigMeshExtractor.ExtractFrom(host, hull.ScriptPath, hull.GlobalName,
                                                hull: hull.Extraction);
        }

        [Test]
        public void Batch1_IsThreeHullsAndTheyAreAllInTheFleetTable()
        {
            // A key that stops matching (a rename, a re-key) would silently empty every fixture
            // below and turn this whole file green by testing nothing.
            CollectionAssert.AreEquivalent(Pass3Keys, Pass3Hulls.Select(h => h.Key).ToArray(),
                "The cutaway kit's batch-1 hulls are no longer in HullMeshFleet under these keys. " +
                "Every test in this file selects by them, so a rename does not fail here — it makes " +
                "them all vacuous.");
        }

        /// <summary>
        /// <b>Every face declares a level, and the level is one the rig itself published.</b> The
        /// extractor refuses a face that does not, so this fixture passing at all is half the claim;
        /// the other half is that the tags are DISTRIBUTED — a rig whose cursor never moved would
        /// tag everything <c>hull</c> and open nothing.
        /// </summary>
        [Test]
        public void EveryPass3Face_DeclaresALevelFromItsOwnPublishedVocabulary()
        {
            var report = new List<string>();

            foreach (FleetHull hull in Pass3Hulls)
            {
                RigMeshData data = Extract(hull, out IRigScriptHost host);
                using (host)
                {
                    Assert.IsTrue(data.CarriesLevelTags,
                        $"{hull.Key}: her rig publishes no geometry().ids, so she cannot be cut open. " +
                        "Batch 1 of the cutaway kit is pass 3 on all three of these rigs.");

                    var vocabulary = new HashSet<int>(data.LevelIds.Values);
                    var seen = new Dictionary<int, int>();
                    foreach (RigFace f in data.Faces)
                    {
                        Assert.Contains(f.Level, vocabulary.ToArray(),
                            $"{hull.Key}: a face carries level {f.Level}, which is not in her own " +
                            "geometry().ids.");
                        seen.TryGetValue(f.Level, out int n);
                        seen[f.Level] = n + 1;
                    }

                    Assert.Greater(seen.Count, 2,
                        $"{hull.Key}: her {data.Faces.Count} faces fall into only {seen.Count} " +
                        "level(s). The authoring cursor is supposed to move as the rig builds — one " +
                        "or two buckets means it did not, and a cutaway would either take nothing " +
                        "or take the boat.");

                    report.Add($"{hull.Key}: " + string.Join(", ",
                        seen.OrderBy(kv => kv.Key)
                            .Select(kv => $"{NameOf(data, kv.Key)}={kv.Value}")));
                }
            }

            Debug.Log("[cutaway] face tags per hull —\n  " + string.Join("\n  ", report));
        }

        /// <summary>
        /// <b>The two ids the GATE's correctness rests on, read back off each rig.</b>
        ///
        /// <para><c>hull</c> must be 0, because 0 is also the shader's "gate off" — that identity is
        /// what makes the exterior silhouette structurally un-cuttable rather than merely never
        /// asked for, and it is why the room always shows INSIDE her own outline. <c>rigging</c>
        /// must sit outside the band a walkable level can occupy, because that is what stops a cut
        /// taking a mast away with the room it stands over. Both are mechanisms, not conventions,
        /// and both would be wrong SILENTLY.</para>
        /// </summary>
        [Test]
        public void HullIsZeroAndRiggingIsOutOfTheWalkableBand_OnEveryPass3Rig()
        {
            foreach (FleetHull hull in Pass3Hulls)
            {
                RigMeshData data = Extract(hull, out IRigScriptHost host);
                using (host)
                {
                    Assert.IsTrue(data.LevelIds.TryGetValue("hull", out int hullId), $"{hull.Key}: no 'hull' level.");
                    Assert.AreEqual(0, hullId,
                        $"{hull.Key}: 'hull' is id {hullId}, not 0. 0 is the shader's 'show the " +
                        "exterior' value, so any other id makes the hull's own shell cullable — the " +
                        "cutaway would eat the boat it is supposed to be a cutaway OF.");

                    Assert.IsTrue(data.LevelIds.TryGetValue("rigging", out int riggingId),
                        $"{hull.Key}: no 'rigging' class. The kit makes it a DEDICATED class so a cut " +
                        "can never take a spar with a room.");

                    int[] walkable = data.Levels.Select(l => l.Tag).Distinct().ToArray();
                    CollectionAssert.DoesNotContain(walkable, riggingId,
                        $"{hull.Key}: 'rigging' ({riggingId}) is also a walkable level, so somebody " +
                        "standing in that room would have her masts culled from under the sky.");

                    // Unique ids, or two rooms are one room as far as one fragment compare is concerned.
                    CollectionAssert.AllItemsAreUnique(data.LevelIds.Values.ToArray(), hull.Key);
                }
            }
        }

        /// <summary>
        /// <b>The door leaf cuts WITH the room.</b> Pass 3 made every hull's aft door a posed leaf
        /// composed by the rig's own <c>render()</c> as <c>F.concat(doorFaces(opts))</c>, and #660
        /// widened extraction to take it — a bare <c>F</c> left 490 px of leaf undrawn on the
        /// lobster. The leaf is house ENCLOSURE, so a cutaway that took the room and left the door
        /// hanging in the air would be worse than no cutaway at all.
        ///
        /// <para>The leaf is identified structurally — the faces the widened list has that
        /// <c>faces()</c> (the static tagged mesh) does not — rather than by counting from the end.</para>
        /// </summary>
        [Test]
        public void TheDoorLeaf_IsTaggedWithTheRoomItCloses()
        {
            foreach (FleetHull hull in Pass3Hulls)
            {
                RigMeshData data = Extract(hull, out IRigScriptHost host);
                using (host)
                {
                    int staticCount = (int)host.EvaluateNumber($"{hull.GlobalName}.faces().length");
                    int leaf = data.Faces.Count - staticCount;
                    Assert.Greater(leaf, 0,
                        $"{hull.Key}: the extracted face list is no longer LONGER than her static " +
                        "faces(), so the posed door leaf is not being taken. That is the #660 " +
                        "regression: her picture draws geometry her mesh has not got.");

                    for (int i = staticCount; i < data.Faces.Count; i++)
                        Assert.AreEqual("house", data.Faces[i].LevelName,
                            $"{hull.Key}: door-leaf face {i - staticCount} of {leaf} is tagged " +
                            $"'{data.Faces[i].LevelName}', not 'house'. The leaf is house enclosure " +
                            "and must cut with the room; left on 'hull' it would hang in the air " +
                            "over an opened wheelhouse.");
                }
            }
        }

        /// <summary>
        /// <b>Every level is ceilinged or explicitly OPEN, and the join to the interior def holds.</b>
        ///
        /// <para>Two claims the cutaway cannot work without. The ceiling is what breaks the two
        /// shared-sole ties in this very batch (trawler and packet both put <c>main_deck</c> and
        /// <c>house_sole</c> at one z) — without it a cut cannot tell the two apart. The
        /// <c>deck</c> field is the join to <c>BoatInteriorDef.Levels[].Id</c>, and it is the ONLY
        /// thing standing between this feature and the defect that already shipped once on this
        /// fleet: indexing one vocabulary by another's order drew the tanker's engine space every
        /// time the player walked into her wheelhouse, and looked fine doing it.</para>
        /// </summary>
        [Test]
        public void EveryPublishedLevel_HasACeilingOrADeclaredOpenSky_AndNamesADefLevel()
        {
            var unjoined = new List<string>();

            foreach (FleetHull hull in Pass3Hulls)
            {
                RigMeshData data = Extract(hull, out IRigScriptHost host);
                using (host)
                {
                    Assert.IsNotEmpty(data.Levels.ToArray(), $"{hull.Key}: geometry() published no levels.");

                    foreach (RigLevelRecord lvl in data.Levels)
                    {
                        Assert.IsNotEmpty(lvl.CeilingKind,
                            $"{hull.Key}.{lvl.Id}: no ceiling.kind at all. An absent field and an " +
                            "open sky must never look the same — that ambiguity is what made the " +
                            "spike's two derived ceilings both wrong.");
                        if (lvl.Enclosed)
                            Assert.Greater(lvl.CeilingZ, lvl.SoleZ,
                                $"{hull.Key}.{lvl.Id}: ceiling {lvl.CeilingZ} is not above sole {lvl.SoleZ}.");
                    }

                    // The shared-sole ties this batch exists to break.
                    var byZ = data.Levels.GroupBy(l => System.Math.Round(l.SoleZ, 3))
                                         .Where(g => g.Count() > 1);
                    foreach (var tie in byZ)
                        Assert.AreEqual(1, tie.Count(l => !l.Enclosed),
                            $"{hull.Key}: levels [{string.Join(", ", tie.Select(l => l.Id))}] share " +
                            $"sole z {tie.Key} and the ceilings do not separate them. The tie is what " +
                            "the kit broke IN DATA (house publishes a ceiling, the working deck " +
                            "publishes OPEN); un-broken, the faces all go to whichever the def " +
                            "lists first.");

                    BoatInteriorDef interior = InteriorDefFor(hull);
                    if (interior == null || interior.Levels == null) continue;
                    var defIds = new HashSet<string>(interior.Levels.Where(l => l != null).Select(l => l.Id));
                    foreach (RigLevelRecord lvl in data.Levels)
                    {
                        if (!lvl.Enclosed || string.IsNullOrEmpty(lvl.DeckId)) continue;
                        if (!defIds.Contains(lvl.DeckId))
                            unjoined.Add($"{hull.Key}.{lvl.Id}: rig says deck '{lvl.DeckId}', " +
                                         $"def has [{string.Join(", ", defIds)}]");
                    }
                }
            }

            CollectionAssert.IsEmpty(unjoined,
                "A rig's enclosed level names a BoatInteriorDef level that does not exist, so the " +
                "cutaway will silently resolve to 0 and the house will never open:\n  " +
                string.Join("\n  ", unjoined));
        }

        /// <summary>
        /// <b>The tag reaches TexCoord1, flat across each face — and UV0.z still carries the rig's
        /// own <c>db</c>.</b>
        ///
        /// <para>The <c>db</c> half is not incidental. The interior-mesh spike measured that
        /// culling the house ALONE leaves a revealed room only 20.3% visible — the hull's own near
        /// topsides stand between the camera and a cabin sole in a ¾ view, and a sheet never met
        /// that because a sheet composites over the hull. UV0.z is the lever that reproduces the
        /// sprite's compositing inside the depth test (20.3% → 97.6%), it has been in the mesh since
        /// the first bake, and it is exactly the sort of field a re-bake drops without anybody
        /// noticing until a room half-appears.</para>
        /// </summary>
        [Test]
        public void TheBuiltMesh_CarriesTheTagInTexCoord1_AndKeepsDbInUV0z()
        {
            foreach (FleetHull hull in Pass3Hulls)
            {
                RigMeshData data = Extract(hull, out IRigScriptHost host);
                using (host)
                {
                    RigMeshBuild built = RigMeshBuilder.Build(data, $"{hull.GlobalName}TagCheck");
                    try
                    {
                        Assert.IsTrue(built.Mesh.HasVertexAttribute(VertexAttribute.TexCoord1),
                            $"{hull.Key}: the built mesh has no TexCoord1, so nothing can be cut.");
                        Assert.AreEqual(data.Faces.Count, built.TaggedFaces, hull.Key);

                        var tags = new List<Vector2>();
                        var attrs = new List<Vector4>();
                        built.Mesh.GetUVs(RigMeshBuilder.LevelUvChannel, tags);
                        built.Mesh.GetUVs(RigMeshBuilder.AttrUvChannel, attrs);

                        int v = 0, dbCarried = 0;
                        for (int f = 0; f < data.Faces.Count; f++)
                        {
                            RigFace face = data.Faces[f];
                            if (face.Db != 0.0) dbCarried++;
                            for (int k = 0; k < face.V.Length; k++, v++)
                            {
                                Assert.AreEqual(face.Level, (int)tags[v].x,
                                    $"{hull.Key}: vertex {v} (face {f}) carries tag {tags[v].x}, " +
                                    $"the face declares {face.Level}. Tags are FLAT across a face.");
                                Assert.AreEqual(0f, tags[v].y,
                                    $"{hull.Key}: vertex {v} is flagged as emitted INTERIOR geometry. " +
                                    "This bake writes hull faces only — the room is a later lane.");
                                Assert.AreEqual((float)face.Db, attrs[v].z, 1e-6f,
                                    $"{hull.Key}: vertex {v} lost the rig's db from UV0.z. That is " +
                                    "the lever that pulls a revealed room forward of the near " +
                                    "topsides (spike: 20.3% -> 97.6% without it).");
                            }
                        }

                        Assert.Greater(dbCarried, 0,
                            $"{hull.Key}: not one face carries a non-zero db, so the UV0.z assertion " +
                            "above compared 0 with 0 on every vertex and proved nothing.");
                    }
                    finally
                    {
                        Object.DestroyImmediate(built.Mesh);
                    }
                }
            }
        }

        /// <summary>
        /// <b>A hull whose rig publishes no <c>geometry()</c> gains NOTHING</b> — no vocabulary, no
        /// tags, and above all no TexCoord1 channel.
        ///
        /// <para>This is the byte-identity guard for the rest of the fleet, and it is why
        /// <see cref="RigLevelTags.Untagged"/> is −1 rather than 0. A channel of zeros would read as
        /// "every face is hull", which is a claim; an absent channel is the truth, and it is what
        /// <c>HullMeshDef.CarriesLevelTags</c> and the renderer both key on so that a half-re-baked
        /// project is never wrong in a NEW way.</para>
        /// </summary>
        [Test]
        public void AHullWithNoGeometryExport_GainsNoTagsAndNoChannel()
        {
            int checkedHulls = 0;

            foreach (FleetHull hull in UntaggedHulls)
            {
                RigMeshData data = Extract(hull, out IRigScriptHost host);
                using (host)
                {
                    if (data.CarriesLevelTags) continue;   // batch 2 landed; not this test's business
                    checkedHulls++;

                    CollectionAssert.IsEmpty(data.Levels.ToArray(), hull.Key);
                    foreach (RigFace f in data.Faces)
                        Assert.AreEqual(RigLevelTags.Untagged, f.Level,
                            $"{hull.Key}: a face on a rig with no vocabulary claims a level.");

                    RigMeshBuild built = RigMeshBuilder.Build(data, $"{hull.GlobalName}NoTagCheck");
                    try
                    {
                        Assert.IsFalse(built.Mesh.HasVertexAttribute(VertexAttribute.TexCoord1),
                            $"{hull.Key}: gained a TexCoord1 channel she has no tags for. Every mesh " +
                            "in the fleet would then move, and every golden master with it.");
                        Assert.AreEqual(0, built.TaggedFaces, hull.Key);
                    }
                    finally
                    {
                        Object.DestroyImmediate(built.Mesh);
                    }
                }
            }

            Assert.Greater(checkedHulls, 0,
                "No untagged hull was available to check, so this guard proved nothing. If the whole " +
                "fleet has gone pass 3, retire this test rather than leaving it vacuous.");
        }

        private static string NameOf(RigMeshData data, int tag) =>
            data.LevelIds.FirstOrDefault(kv => kv.Value == tag).Key ?? tag.ToString();

        /// <summary>Her interior def, via the visual the fleet table already points at — so the join
        /// is checked against the asset the game really loads, not against a path spelled here.</summary>
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
