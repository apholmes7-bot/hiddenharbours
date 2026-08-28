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
    /// depends on, each read back out of the rig itself. <b>Batch 2 duly added one</b> (2026-08-27):
    /// the tanker publishes <c>poop_deck 6</c>, a second exterior deck, and not one fixture in this
    /// file had to be told about it.</para>
    ///
    /// <para><b>The LID, ruled 2026-08-27.</b> A cut takes its declared ceiling: when
    /// level L engages the gate, L's own faces come off AND the faces of the level L's
    /// ceiling record names as its lid. One hop, declaration-driven, never inferred. The
    /// three fixtures at the end of this file are that ruling — it closed the question the
    /// first round of this lane pinned OPEN, and they assert it as law rather than
    /// reporting it as a finding.</para>
    /// </summary>
    public sealed class HullLevelTagBakeTests
    {
        /// <summary>
        /// Every hull whose rig publishes a level vocabulary — batch 1 AND batch 2 of the cutaway kit.
        ///
        /// <para>Batch 1: the lobster (the render-verified reference) and the two ships that share a
        /// sole and therefore break the tie in data. Batch 2, landed 2026-08-27: the dragger, the
        /// trawler Mk II, the tanker — who brings <c>poop_deck</c>, a seventh level id and the fleet's
        /// first SECOND exterior deck — and the eighteen lobster variants. <b>Pass 4, 2026-08-28:
        /// the CAPE ISLANDER</b>, and she is the one that matters most, because the game opens
        /// below her decks — every fixture in this file was vacuous on the flagship until her rig
        /// published a vocabulary, and the join rule below in particular had nothing to join.</para>
        ///
        /// <para>The eighteen come off <see cref="LobsterVariantFleet"/> rather than being spelled
        /// out, for the reason <c>HullMeshFleet</c> gives where it does the same: eighteen hand-typed
        /// rows are exactly the number where one mismatch survives review. The six one-hull rigs stay
        /// literal, because there a rename SHOULD fail
        /// <see cref="TheCutawayKitsHulls_AreAllInTheFleetTable"/> loudly rather than quietly
        /// shrinking the set.</para>
        /// </summary>
        private static readonly string[] Pass3Keys =
            new[] { "lobsterBoat", "sternTrawler", "coastalPacket",
                    "sideDragger", "sternTrawlerMk2", "tanker",
                    "capeIslander" }
                .Concat(LobsterVariantFleet.All.Select(v => v.Key))
                .ToArray();

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
        public void TheCutawayKitsHulls_AreAllInTheFleetTable()
        {
            // A key that stops matching (a rename, a re-key) would silently empty every fixture
            // below and turn this whole file green by testing nothing.
            CollectionAssert.AreEquivalent(Pass3Keys, Pass3Hulls.Select(h => h.Key).ToArray(),
                "The cutaway kit's hulls are no longer in HullMeshFleet under these keys. " +
                "Every test in this file selects by them, so a rename does not fail here — it makes " +
                "them all vacuous.");

            Assert.AreEqual(25, Pass3Keys.Length,
                "Batch 1 is three hulls, batch 2 is twenty-one (three ships + eighteen variants), and " +
                "pass 4 added the cape islander. A different total means a rig was added to the kit " +
                "without this file being told, or LobsterVariantFleet stopped enumerating eighteen.");
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
                        "Every rig selected here is pass 3 or later — batch 1 and batch 2 of the kit.");

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
                    // ⚠️ ASK THE RIG FOR THE SAME BOAT THE EXTRACTOR BUILT. A generator's `faces()`
                    // with no argument builds its DEFAULT variant, so on the eighteen lobsters this
                    // compared one hull's extraction against another hull's static list and reported
                    // a NEGATIVE leaf — the fixture reading as "the door is missing" when the real
                    // fault was that it had asked about a different boat.
                    string opts = hull.Extraction != null && hull.Extraction.IsVariant
                                  && !string.IsNullOrEmpty(hull.Extraction.ViewOptions)
                        ? hull.Extraction.ViewOptions
                        : "";
                    int staticCount = (int)host.EvaluateNumber($"{hull.GlobalName}.faces({opts}).length");
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
        /// <b>The table the gate reads.</b> Everything above proves the rig published a vocabulary;
        /// this proves the row the bake writes from it is one <see cref="HullMeshDef.CutawayForDeck"/>
        /// can answer — an enclosed level joined to a DECK id, carrying its own tag and its declared
        /// lid's.
        ///
        /// <para>Built by <see cref="RigMeshAssetBaker.LevelTableFor"/>, the bake's OWN mapping,
        /// called rather than transcribed — a field dropped in the baker fails here instead of
        /// passing against a copy of itself. Nothing is baked and no asset is touched;
        /// <see cref="HullCutawayAssetTests"/> stays the separate question of whether the committed
        /// bytes have caught up, and the end-to-end call through a real def is the fixture below.</para>
        ///
        /// <para><b>Why both arms.</b> "Every enclosed level has a cut" alone would pass on a table
        /// that opened EVERYTHING, and cutting into an open deck is the failure the ruling is about —
        /// you cannot take the roof off the sky. So an open level must carry <c>Enclosed=false</c> in
        /// the same sweep, which is the field <c>CutawayForDeck</c> refuses on.</para>
        /// </summary>
        [Test]
        public void TheBakesLevelTable_AnswersForEveryEnclosedLevelAndRefusesTheOpenOnes()
        {
            var unanswerable = new List<string>();
            var wouldOverOpen = new List<string>();
            int enclosedChecked = 0;

            foreach (FleetHull hull in Pass3Hulls)
            {
                RigMeshData data = Extract(hull, out IRigScriptHost host);
                using (host)
                {
                    HullMeshDef.LevelTag[] table = RigMeshAssetBaker.LevelTableFor(data);
                    Assert.AreEqual(data.Levels.Count, table.Length,
                        $"{hull.Key}: the bake's own mapping dropped a level on the way to the def.");

                    for (int i = 0; i < table.Length; i++)
                    {
                        HullMeshDef.LevelTag row = table[i];
                        RigLevelRecord lvl = data.Levels[i];

                        Assert.AreEqual(lvl.Id, row.LevelId, $"{hull.Key}: row {i} lost its level id.");
                        Assert.AreEqual(lvl.Tag, row.Tag,
                            $"{hull.Key}.{lvl.Id}: the row carries tag {row.Tag}, the rig tagged her " +
                            $"faces {lvl.Tag}. A row that names the wrong tag opens someone else's room.");
                        Assert.AreEqual(lvl.LidTag, row.LidTag,
                            $"{hull.Key}.{lvl.Id}: the row took lid {row.LidTag}, the rig declared " +
                            $"'{lvl.LidLevelId}' ({lvl.LidTag}).");
                        Assert.AreEqual(lvl.Enclosed, row.Enclosed, $"{hull.Key}.{lvl.Id}");

                        if (!row.Enclosed)
                        {
                            // CutawayForDeck refuses on exactly this field. An open deck reaching the
                            // table as enclosed is how the sky gets its roof taken off.
                            if (row.Tag > 0 && row.Enclosed)
                                wouldOverOpen.Add($"{hull.Key}.{lvl.Id}");
                            continue;
                        }

                        enclosedChecked++;
                        if (string.IsNullOrEmpty(row.DeckId))
                            unanswerable.Add($"{hull.Key}.{lvl.Id}: enclosed, and joins to no deck id — " +
                                             "CutawayForDeck can never be handed anything that matches it");
                        else if (row.Tag <= 0)
                            unanswerable.Add($"{hull.Key}.{lvl.Id}: deck '{row.DeckId}' carries tag " +
                                             $"{row.Tag}, and 0 IS the gate's 'no cut'");
                    }
                }
            }

            CollectionAssert.IsEmpty(unanswerable,
                "An enclosed level would engage the gate and get nothing back, so its ceiling stays on " +
                "and the occupant goes below to look at a whole boat:\n  " +
                string.Join("\n  ", unanswerable));
            CollectionAssert.IsEmpty(wouldOverOpen,
                "The table would open a level the rig published as OPEN:\n  " +
                string.Join("\n  ", wouldOverOpen));
            Assert.Greater(enclosedChecked, 0,
                "Not one enclosed level was examined, so both assertions above compared empty lists.");
        }

        /// <summary>
        /// <b>The cape, by name, through the real gate.</b> The sweep above reads the table; this one
        /// asks <see cref="HullMeshDef.CutawayForDeck"/> the question the game asks, on the boat the
        /// intro opens inside — so that losing her from the fleet table fails here rather than
        /// shrinking a loop in silence.
        ///
        /// <para>Her rig's vocabulary is its own (<c>house</c>, <c>cuddy</c>) and the join to the
        /// interior def is by DECK id (<c>house_sole</c>, <c>cuddy_sole</c>) — two namespaces on
        /// purpose, and conflating them is how the gate silently resolves to 0.</para>
        ///
        /// <para>⚠️ Needs a live <c>ScriptableObject</c>, so it SKIPS in the no-editor harness the way
        /// seven fixtures in this folder already do. It is a real check in the editor and in CI; the
        /// data it depends on is covered headless by the sweep above.</para>
        /// </summary>
        [Test]
        public void TheCapeIslandersHouseOpens_AndHerCuddyTakesTheForedeckWithIt()
        {
            FleetHull cape = HullMeshFleet.Hulls.Single(h => h.Key == "capeIslander");
            RigMeshData data = Extract(cape, out IRigScriptHost host);
            using (host)
            {
                var def = ScriptableObject.CreateInstance<HullMeshDef>();
                try
                {
                    def.LevelTags = RigMeshAssetBaker.LevelTableFor(data);

                    HullMeshDef.Cut house = def.CutawayForDeck("house_sole");
                    Assert.IsTrue(house.Opens,
                        "CutawayForDeck(house_sole) returned no cut. This is the call the intro makes " +
                        "in the first minute of the game; unanswered, the room draws over her closed " +
                        "wheelhouse — the #645 overdraw #673 accepted only because she had no pass yet.");
                    Assert.AreEqual(0, house.Lid,
                        "her house declares ceiling.lid null — a HARD eave deckhead, an explicit veto. " +
                        "A lid here would take the roof slab off with the room.");

                    HullMeshDef.Cut cuddy = def.CutawayForDeck("cuddy_sole");
                    Assert.IsTrue(cuddy.Opens, "CutawayForDeck(cuddy_sole) returned no cut.");
                    Assert.AreEqual(data.LevelIds["foredeck"], cuddy.Lid,
                        "the cuddy has no faces of her own — she is a void under the whaleback, and her " +
                        "ceiling is the foredeck's raked underside. Without that declared lid her cut " +
                        "removes NOTHING and the foredeck stays over the berth, which is the exact " +
                        "failure ceiling.lid was ruled for.");

                    foreach (string open in new[] { "cockpit", "foredeck" })
                        Assert.IsFalse(def.CutawayForDeck(open).Opens,
                            $"'{open}' is an open deck on this hull — no hardtop, no whaleback over it.");
                }
                finally
                {
                    Object.DestroyImmediate(def);
                }
            }
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

        /// <summary>
        /// <b>A cut takes its declared ceiling</b> — the coordinator's ruling of 2026-08-27, and the
        /// answer to the question the first round of this lane pinned as OPEN.
        ///
        /// <para><b>What the question was.</b> Three enclosed levels have no hull faces of their own:
        /// both ships' <c>below</c> and the lobster's <c>cuddy</c>. A cut of a level culls the faces
        /// tagged to it, so all three engaged the gate and removed nothing — the player went below and
        /// looked at a whole boat. The ruling closes it: when level L engages, L's faces come off AND
        /// the faces of the level L's ceiling record names as its LID. One hop, declaration-driven.</para>
        ///
        /// <para>So this asserts the thing that was missing: <b>every enclosed level either has faces
        /// of its own or takes a lid that has some</b>. A level that has neither is a cut that opens
        /// nothing, which is exactly the state this round exists to end — and the message says so
        /// rather than reporting an inequality.</para>
        /// </summary>
        [Test]
        public void EveryEnclosedLevel_RemovesSomething_ItsOwnFacesOrItsDeclaredLids()
        {
            var opensNothing = new List<string>();
            var viaLid = new List<string>();

            foreach (FleetHull hull in Pass3Hulls)
            {
                RigMeshData data = Extract(hull, out IRigScriptHost host);
                using (host)
                {
                    var faceCount = new Dictionary<int, int>();
                    foreach (RigFace f in data.Faces)
                    {
                        faceCount.TryGetValue(f.Level, out int n);
                        faceCount[f.Level] = n + 1;
                    }

                    foreach (RigLevelRecord lvl in data.Levels)
                    {
                        if (!lvl.Enclosed) continue;   // an open level is never cut into

                        faceCount.TryGetValue(lvl.Tag, out int own);
                        int lid = 0;
                        if (lvl.LidTag > 0) faceCount.TryGetValue(lvl.LidTag, out lid);

                        if (own + lid == 0)
                        {
                            opensNothing.Add($"{hull.Key}.{lvl.Id} (lid '{lvl.LidLevelId}')");
                            continue;
                        }
                        if (own == 0)
                            viaLid.Add($"{hull.Key}.{lvl.Id} -> {lvl.LidLevelId} ({lid} faces)");
                    }
                }
            }

            CollectionAssert.IsEmpty(opensNothing,
                "These enclosed levels remove NOTHING when they engage the gate — the occupant goes " +
                "below and looks at a whole boat. Either the rig tags no faces to them and declares " +
                "no lid, or the lid it declares has no faces either:\n  " +
                string.Join("\n  ", opensNothing));

            // Not merely "nothing is broken": the levels that own no faces must be the ones the
            // ruling was made about, and they must be opening via their lid. A hull that quietly
            // stopped needing its lid would pass the assertion above while the ruling went unused.
            //
            // ⚠️ BATCH 2 TURNED THIS FROM A COINCIDENCE INTO A PATTERN, measured across 24 hulls:
            // the set of levels that own NO faces is EXACTLY the set that declares a lid, both 24.
            // Every enclosed room the rigs build has walls of its own EXCEPT the ones under a deck,
            // and those are voids the deck roofs — three ships' engine spaces (dragger 119, both
            // trawlers 142, packet 92 lid faces), the tanker's accommodation under her poop (56),
            // and nineteen cuddies under their foredeck (15 each). That is why the ruling was needed
            // at all: with no lid these levels engage the gate and remove nothing, and the occupant
            // goes below to look at a whole boat.
            //
            // The eighteen variants come off LobsterVariantFleet rather than being spelled out —
            // they are one generator rig, which declares the cuddy's lid once for all eighteen,
            // and so they are one line here.
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "capeIslander.cuddy -> foredeck (15 faces)",
                    "lobsterBoat.cuddy -> foredeck (15 faces)",
                    "sternTrawler.below -> main_deck (142 faces)",
                    "coastalPacket.below -> main_deck (92 faces)",
                    "sideDragger.below -> main_deck (119 faces)",
                    "sternTrawlerMk2.below -> main_deck (142 faces)",
                    "tanker.below -> poop_deck (56 faces)",
                }
                .Concat(LobsterVariantFleet.All.Select(v => $"{v.Key}.cuddy -> foredeck (15 faces)"))
                .ToArray(),
                viaLid,
                "The set of levels that open ONLY through their lid has changed. These are the " +
                "ruling's own examples and the reason it exists; a level joining or leaving this " +
                "list is an upstream tagging change and wants a look. Now:\n  " +
                string.Join("\n  ", viaLid));
        }

        /// <summary>
        /// <b>Every declared lid is a legal one</b> — the shapes <see cref="RigMeshExtractor"/> refuses
        /// at the bake, asserted here as properties of the shipped rigs rather than only as guards.
        ///
        /// <para>The extractor throws on each of these, so a green run here says the rigs are clean
        /// AND that the guards were not silently satisfied by a rig that declares no lids at all —
        /// which is why the count is asserted first.</para>
        /// </summary>
        [Test]
        public void EveryDeclaredLid_IsALeafLevelThatIsNeitherHullNorRigging()
        {
            int lids = 0;

            foreach (FleetHull hull in Pass3Hulls)
            {
                RigMeshData data = Extract(hull, out IRigScriptHost host);
                using (host)
                {
                    var byId = new Dictionary<string, RigLevelRecord>();
                    foreach (RigLevelRecord l in data.Levels) byId[l.Id] = l;

                    foreach (RigLevelRecord lvl in data.Levels)
                    {
                        if (string.IsNullOrEmpty(lvl.LidLevelId))
                        {
                            Assert.AreEqual(0, lvl.LidTag,
                                $"{hull.Key}.{lvl.Id}: no lid id but a non-zero lid tag.");
                            continue;
                        }
                        lids++;

                        Assert.AreNotEqual(lvl.Id, lvl.LidLevelId, $"{hull.Key}.{lvl.Id}: its own lid.");
                        Assert.AreNotEqual(RigLevelTags.HullLevelId, lvl.LidLevelId,
                            $"{hull.Key}.{lvl.Id}: takes the hull's own shell. That would eat the boat " +
                            "the cutaway is a cutaway OF.");
                        Assert.AreNotEqual(RigLevelTags.RiggingLevelId, lvl.LidLevelId,
                            $"{hull.Key}.{lvl.Id}: takes the rigging class, which exists precisely so " +
                            "a cut can never take a spar.");
                        Assert.IsTrue(data.LevelIds.TryGetValue(lvl.LidLevelId, out int expected),
                            $"{hull.Key}.{lvl.Id}: lid '{lvl.LidLevelId}' is not in geometry().ids.");
                        Assert.AreEqual(expected, lvl.LidTag, $"{hull.Key}.{lvl.Id}: lid tag mismatch.");

                        // ONE HOP. The shader has one lid uniform so it cannot express a chain; this
                        // is the same law asserted where a chain could be WRITTEN.
                        if (byId.TryGetValue(lvl.LidLevelId, out RigLevelRecord lidRecord))
                            Assert.IsEmpty(lidRecord.LidLevelId ?? "",
                                $"{hull.Key}.{lvl.Id} takes '{lvl.LidLevelId}', which itself takes " +
                                $"'{lidRecord.LidLevelId}'. ONE HOP ONLY — a chain would peel a ship " +
                                "open one deck at a time from wherever the occupant was standing.");
                    }
                }
            }

            Assert.AreEqual(25, lids,
                "Batch 1 declares three lids (lobster cuddy, both ships' below), batch 2 declares " +
                "twenty-one (three ships' below — the TANKER's onto poop_deck, not main_deck — and " +
                "the eighteen variants' cuddy), and pass 4 adds the CAPE's cuddy onto her " +
                "whaleback foredeck. A different count means every property above was checked " +
                "against a different set of rigs than the one this fixture was written for.");
        }

        /// <summary>
        /// <b>The stand-in table is retired, and this is what holds it retired.</b>
        ///
        /// <para>There was a table in this repo — <c>RigLevelLids</c>, keyed by rig file — because
        /// the cutaway rigs' ceiling records did not name their lid in a form a machine could read:
        /// they carried <c>of:</c>, which is prose (<c>'main-deck underside (DECK-0.12)'</c>, where
        /// the level id is <c>main_deck</c>). Upstream's ask-2 drop added <c>ceiling.lid</c> to all
        /// seven rigs, so the rig answers the question itself and the table is gone.</para>
        ///
        /// <para><b>The ledger inverts.</b> It used to name what was still owed so the debt could not
        /// be forgotten; it now asserts that nothing is owed and that no second answer has grown
        /// back. Two halves, and the first is the one that matters: a table absent today can be
        /// re-added tomorrow by anyone who meets a rig without a lid and reaches for the old shape.
        /// It is asserted by NAME through reflection rather than by reference, because a test cannot
        /// reference a type that must not exist.</para>
        ///
        /// <para>The second half is the source ledger: every level on every cutaway hull must have
        /// got its lid from the RIG — <see cref="RigLevelRecord.LidFromRig"/> for the seven that
        /// name one, <see cref="RigLevelRecord.LidFromVeto"/> for the rest, which publish
        /// <c>lid: null</c>. Never <c>none</c>: an unresolved record would mean a level whose lid
        /// nobody decided, and the extractor refuses that outright.</para>
        /// </summary>
        [Test]
        public void TheStandInLidTable_StaysRetired_AndEveryLidComesFromTheRig()
        {
            // ---- half one: the table has not grown back ------------------------------------------
            System.Type table = typeof(RigLevelRecord).Assembly
                .GetType("HiddenHarbours.Tools.RigBaking.RigLevelLids", throwOnError: false);
            Assert.IsNull(table,
                "RigLevelLids is back. It existed only because the cutaway rigs predated " +
                "ceiling.lid; they publish it now, so a table beside them is a SECOND answer to a " +
                "question the rig already answers, and the stale one is the dangerous one — a wrong " +
                "lid does not look wrong, it opens a plausible hole in the wrong deck. If a NEW rig " +
                "arrives without the field, the extractor refuses its bake and the fix is upstream: " +
                "add ceiling.lid to that rig.");

            // ---- half two: every lid on every cutaway hull came from the rig ----------------------
            var notFromTheRig = new List<string>();
            var named = new List<string>();

            foreach (FleetHull hull in Pass3Hulls)
            {
                RigMeshData data = Extract(hull, out IRigScriptHost host);
                using (host)
                {
                    foreach (RigLevelRecord l in data.Levels)
                    {
                        if (l.LidSource != RigLevelRecord.LidFromRig &&
                            l.LidSource != RigLevelRecord.LidFromVeto)
                            notFromTheRig.Add($"{hull.Key}.{l.Id} [{l.LidSource}]");

                        if (l.LidSource == RigLevelRecord.LidFromRig)
                            named.Add($"{hull.Key}.{l.Id} -> {l.LidLevelId}");
                    }
                }
            }

            CollectionAssert.IsEmpty(notFromTheRig,
                "These levels did not get their lid from the rig. Every walkable level must publish " +
                "ceiling.lid — a level id, or null for the per-level veto — and nothing else may " +
                "answer for it:\n  " + string.Join("\n  ", notFromTheRig));

            // The same twenty-four relationships the fixtures above check, asserted here as
            // PROVENANCE: they are now the rigs' own words rather than this repo's. The eighteen
            // variant rows come off LobsterVariantFleet because one generator rig makes them all.
            string[] expected = new[]
                {
                    "capeIslander.cuddy -> foredeck",
                    "lobsterBoat.cuddy -> foredeck",
                    "sternTrawler.below -> main_deck",
                    "coastalPacket.below -> main_deck",
                    "sideDragger.below -> main_deck",
                    "sternTrawlerMk2.below -> main_deck",
                    "tanker.below -> poop_deck",
                }
                .Concat(LobsterVariantFleet.All.Select(v => $"{v.Key}.cuddy -> foredeck"))
                .ToArray();

            CollectionAssert.AreEquivalent(expected, named,
                "The set of lids the RIGS declare has changed. This is upstream art moving, not a " +
                "local table drifting — read the rig's ceiling records before touching this list. " +
                "Now:\n  " + string.Join("\n  ", named));
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
