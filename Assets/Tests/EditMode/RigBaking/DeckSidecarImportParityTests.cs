using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>The committed deck Defs still say what the sidecars say.</b> Every HULL sidecar in
    /// <c>docs/art/rigs/gameplay/</c> is re-read here with the production reader and compared,
    /// vertex by vertex, against the <see cref="BoatDeckDef"/> asset in the repo. Edit a sidecar
    /// without re-running the importer — or hand-edit a Def — and this goes red.
    ///
    /// <para><b>HULL sidecar, because the folder is no longer all boats.</b> The seagull kit put a
    /// <c>hidden-harbours/creature-gameplay@1</c> file in here, and every assertion below asks a
    /// question that is only meaningful about a boat. Discovery therefore runs through the
    /// production classifier — <see cref="DeckSidecarReader.IsHullSidecar"/>, the same one the
    /// importer uses, so the test and the menu item cannot disagree about what a hull is — and
    /// <see cref="TheHullGlobKeepsEveryHullAndExcludesOnlyDeclaredNonHullSidecars"/> guards the
    /// partition in both directions.</para>
    ///
    /// <para>This is the guard that makes "rig-data → Def, no hand transcription" a CHECKED claim
    /// rather than a promise. The design ruling behind M2-37 is explicit about why it is needed: the
    /// baked anchors JSON went dead because a hand-copied constant beside it drifted, and nothing
    /// noticed.</para>
    ///
    /// <para>It also runs the staleness rule in CI: every sidecar must still hash to the rig it names
    /// (exactly, or once line endings are normalised), so a hull reshaped without re-deriving its
    /// sidecar fails here rather than shipping the old boat's deck.</para>
    /// </summary>
    public class DeckSidecarImportParityTests
    {
        const string SidecarFolder = "docs/art/rigs/gameplay";
        const string RigFolder = "docs/art/rigs";
        const string DeckFolder = "Assets/_Project/Data/Boats/Decks";
        const float Tol = 1e-3f;                                   // 1 mm — well under the 31 mm pixel

        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        /// <summary>The non-hull schemas this fixture knows about, spelled out HERE rather than
        /// read back from production. Widening the production list therefore reddens this fixture
        /// and a human decides — a test that asked the code for its own answer would agree with any
        /// answer the code gave.</summary>
        static readonly string[] KnownNonHullSchemas = { "hidden-harbours/creature-gameplay@1" };

        /// <summary>Everything in the folder, hulls and otherwise.</summary>
        static IEnumerable<string> AllSidecarFiles =>
            Directory.GetFiles(Path.Combine(RepoRoot, SidecarFolder), "*.gameplay.json")
                     .OrderBy(f => f);

        /// <summary>The measured HULLS — what every test below is about. Classified by the
        /// production reader, never by name: the next creature must not need this file edited.</summary>
        static IEnumerable<string> SidecarFiles =>
            AllSidecarFiles.Where(f => DeckSidecarReader.IsHullSidecar(File.ReadAllText(f)));

        /// <summary>The other half of the partition, so the exclusion can be checked rather than
        /// trusted.</summary>
        static IEnumerable<string> NonHullSidecarFiles =>
            AllSidecarFiles.Where(f => !DeckSidecarReader.IsHullSidecar(File.ReadAllText(f)));

        /// <summary>
        /// Read one sidecar against the rig IT names — the same resolution the importer uses, so the
        /// two cannot disagree about which file the staleness rule is checking.
        ///
        /// <para>This used to derive the rig from the sidecar's file name. That is only expressible
        /// while every rig makes one boat; the lobster generator makes eighteen, whose files are
        /// named for the hull. It is also the safer form for the eleven — see
        /// <see cref="DeckSidecarReader.ResolveRigFileName"/>.</para>
        /// </summary>
        static SidecarRead ReadOne(string file)
        {
            string who = Path.GetFileName(file);
            string json = File.ReadAllText(file);
            string rigFile = DeckSidecarReader.ResolveRigFileName(who, json);
            // The SAME resolver the importer uses — flat first, then anywhere under the rig tree
            // (the sail rig kit's two hulls live in sail-rig-kit/<hull>/). Sharing it is the point:
            // a test that resolved rigs its own way could pass while the import refused.
            string rigPath = DeckSidecarReader.ResolveRigPath(RepoRoot, rigFile);
            Assert.IsNotNull(rigPath, $"{who}: the rig it names ({rigFile}) is nowhere under {RigFolder}");
            return DeckSidecarReader.Read(json, $"{SidecarFolder}/{who}", File.ReadAllBytes(rigPath));
        }

        static BoatDeckDef DefFor(string file)
        {
            // The sidecar's file STEM names the hull; the rig it was cut from is inside the file.
            string stem = DeckSidecarImporter.StripRigSuffix(
                Path.GetFileName(file).Replace(".gameplay.json", ""));
            string name = char.ToUpperInvariant(stem[0]) + stem.Substring(1);
            return AssetDatabase.LoadAssetAtPath<BoatDeckDef>($"{DeckFolder}/{name}.asset");
        }

        /// <summary>
        /// <b>The partition itself, checked in BOTH directions.</b> Every other test here iterates
        /// <c>SidecarFiles</c>, so a filter that excluded too much would turn the whole fixture green
        /// having checked nothing — and a filter that excluded too little would put a creature back
        /// in front of assertions written about boats.
        /// </summary>
        [Test]
        public void TheHullGlobKeepsEveryHullAndExcludesOnlyDeclaredNonHullSidecars()
        {
            string[] all = AllSidecarFiles.Select(Path.GetFileName).ToArray();
            string[] hulls = SidecarFiles.Select(Path.GetFileName).ToArray();
            string[] excluded = NonHullSidecarFiles.Select(Path.GetFileName).ToArray();

            Assert.AreEqual(all.Length, hulls.Length + excluded.Length,
                "the two halves of the partition do not add up to the folder");

            // ⚠️ ANTI-VACUITY. Nothing below this line would fail on an empty hull list.
            Assert.GreaterOrEqual(hulls.Length, 30,
                $"only {hulls.Length} hull sidecar(s) survived discovery out of {all.Length} files. " +
                "The fleet is thirty-odd measured hulls; a number this low means the exclusion ate " +
                "the corpus and every parity assertion below is passing on an empty list.");

            // Two named anchors, one for each shape a hull comes in. doryIsoRig DECLARES
            // hidden-harbours/boat-gameplay-geometry@1; capeIslanderIsoRig declares NO schema at all,
            // as eight of the committed hulls do. A positive "does it say boat?" filter keeps the
            // first and drops the second without a word — which is why the filter is negative.
            CollectionAssert.Contains(hulls, "doryIsoRig.gameplay.json",
                "the schema-DECLARING anchor hull fell out of discovery");
            CollectionAssert.Contains(hulls, "capeIslanderIsoRig.gameplay.json",
                "the schema-LESS anchor hull fell out of discovery — a positive schema filter does " +
                "exactly this, to eight hulls, in silence");

            // The one file we know is not a boat is actually out. A ledger anchor: a second creature
            // adds a line here, it does not change the discovery.
            CollectionAssert.Contains(excluded, "seagullIsoRig.gameplay.json",
                "the creature sidecar is back in the hull suite");

            foreach (string file in NonHullSidecarFiles)
            {
                string who = Path.GetFileName(file);
                string json = File.ReadAllText(file);
                string schema = DeckSidecarReader.DeclaredSchema(json);

                CollectionAssert.Contains(KnownNonHullSchemas, schema,
                    $"{who} is excluded from the hull suite under '{schema}', which this fixture has " +
                    "never heard of. Production widened its non-hull list; add the schema here once " +
                    "someone has confirmed the file really is not a boat.");

                // The direction that catches a hull excluded by accident: a boat carries a DECK
                // section, and this is the one thing an animal file provably does not have.
                Assert.IsFalse(json.Contains("\"DECK\""),
                    $"{who} declares the non-hull schema '{schema}' but carries a DECK section. That " +
                    "is a measured hull being quietly dropped from the parity check — the exact " +
                    "false green this partition exists to prevent.");
            }
        }

        [Test]
        public void EverySidecarStillDescribesTheRigItNames()
        {
            var stale = new List<string>();
            foreach (string file in SidecarFiles)
            {
                SidecarRead read = ReadOne(file);
                if (read.HashMatch == RigHashMatch.None)
                    stale.Add($"{Path.GetFileName(file)}: {string.Join(" | ", read.Errors)}");
            }
            Assert.IsEmpty(stale,
                "a stale sidecar means the hull was reshaped and its walkable area was not re-derived:\n" +
                string.Join("\n", stale));
        }

        [Test]
        public void EverySidecarHasACommittedDeckDef()
        {
            var missing = SidecarFiles.Where(f => DefFor(f) == null)
                                      .Select(Path.GetFileName).ToArray();
            Assert.IsEmpty(missing,
                "run Hidden Harbours ▸ Dev ▸ Import Deck Sidecars: " + string.Join(", ", missing));
        }

        [Test]
        public void EveryCommittedDeckDefMatchesItsSidecarVertexForVertex()
        {
            foreach (string file in SidecarFiles)
            {
                string who = Path.GetFileName(file);
                SidecarRead read = ReadOne(file);
                Assert.IsTrue(read.Ok, $"{who}: {string.Join(" | ", read.Errors)}");

                BoatDeckDef def = DefFor(file);
                Assert.IsNotNull(def, $"{who}: no committed def");
                Assert.AreEqual(read.Areas.Count, def.Areas.Length, $"{who}: area count");
                Assert.AreEqual(read.ExpectedRigSha, def.DerivedFromRigSha256, $"{who}: recorded rig SHA");

                for (int i = 0; i < read.Areas.Count; i++)
                {
                    SidecarArea src = read.Areas[i];
                    DeckArea baked = def.Areas[i];
                    Assert.AreEqual(src.Id, baked.Id, $"{who}: area {i} id");
                    Assert.AreEqual(src.IsWashboard ? DeckAreaKind.Washboard : DeckAreaKind.Deck,
                                    baked.Kind, $"{who}/{src.Id}: kind");
                    Assert.AreEqual(src.Vertices.Length, baked.Outline.Length, $"{who}/{src.Id}: vertex count");

                    for (int v = 0; v < src.Vertices.Length; v++)
                    {
                        Assert.AreEqual(src.Vertices[v].x, baked.Outline[v].x, Tol, $"{who}/{src.Id}[{v}].x");
                        Assert.AreEqual(src.Vertices[v].y, baked.Outline[v].y, Tol, $"{who}/{src.Id}[{v}].y");
                        // Height goes through the fitted plane, so it is checked against the FIT's own
                        // stated error bar rather than pretended to be exact.
                        float fitted = DeckAreaMath.HeightAt(baked.HeightPlane,
                                                             new Vector2(src.Vertices[v].x, src.Vertices[v].y));
                        Assert.LessOrEqual(Mathf.Abs(src.Vertices[v].z - fitted),
                                           baked.HeightResidualMax + Tol, $"{who}/{src.Id}[{v}].z");
                    }
                }

                Assert.AreEqual(read.Cleats.Count, def.Cleats.Length, $"{who}: cleat count");
                for (int c = 0; c < read.Cleats.Count; c++)
                {
                    Assert.AreEqual(read.Cleats[c].Id, def.Cleats[c].Id, $"{who}: cleat {c} id");
                    Assert.AreEqual(read.Cleats[c].Position.x, def.Cleats[c].PositionMeters.x, Tol);
                    Assert.AreEqual(read.Cleats[c].Position.y, def.Cleats[c].PositionMeters.y, Tol);
                    Assert.AreEqual(read.Cleats[c].Position.z, def.Cleats[c].PositionMeters.z, Tol);
                }
            }
        }

        [Test]
        public void TheWalkBoxIsTheBoxAroundTheDeckAreasOnly()
        {
            foreach (string file in SidecarFiles)
            {
                BoatDeckDef def = DefFor(file);
                Assert.IsNotNull(def, Path.GetFileName(file));

                float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
                float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
                foreach (DeckArea a in def.Areas)
                {
                    if (a.Kind != DeckAreaKind.Deck) continue;      // washboards are NOT free-walk area
                    minX = Mathf.Min(minX, a.Bounds.x); minY = Mathf.Min(minY, a.Bounds.y);
                    maxX = Mathf.Max(maxX, a.Bounds.z); maxY = Mathf.Max(maxY, a.Bounds.w);
                }
                Assert.AreEqual((minX + maxX) * 0.5f, def.WalkCenter.x, Tol, $"{def.Id}: centre x");
                Assert.AreEqual((minY + maxY) * 0.5f, def.WalkCenter.y, Tol, $"{def.Id}: centre y");
                Assert.AreEqual((maxX - minX) * 0.5f, def.WalkHalfExtents.x, Tol, $"{def.Id}: half-beam");
                Assert.AreEqual((maxY - minY) * 0.5f, def.WalkHalfExtents.y, Tol, $"{def.Id}: half-length");
            }
        }

        [Test]
        public void EveryBakedAreaIsUsableAndItsBoundsAgreeWithItsOutline()
        {
            foreach (string file in SidecarFiles)
            {
                BoatDeckDef def = DefFor(file);
                foreach (DeckArea a in def.Areas)
                {
                    Assert.IsTrue(a.IsUsable(), $"{def.Id}/{a.Id}: fewer than 3 vertices");
                    Vector4 recomputed = DeckAreaMath.BoundsOf(a.Outline);
                    Assert.AreEqual(recomputed.x, a.Bounds.x, Tol, $"{def.Id}/{a.Id}: minX");
                    Assert.AreEqual(recomputed.y, a.Bounds.y, Tol, $"{def.Id}/{a.Id}: minY");
                    Assert.AreEqual(recomputed.z, a.Bounds.z, Tol, $"{def.Id}/{a.Id}: maxX");
                    Assert.AreEqual(recomputed.w, a.Bounds.w, Tol, $"{def.Id}/{a.Id}: maxY");
                }
                Assert.IsTrue(def.HasWalkableDeck(), $"{def.Id}: no walkable DECK area survived the import");
            }
        }

        [Test]
        public void EveryMeasuredHullsVisualPointsAtHerOwnDeck()
        {
            // The wiring half: a def nobody references is a def nobody walks. Checked from the visuals'
            // side so a renamed or re-pointed visual asset is caught too.
            var wired = AssetDatabase.FindAssets("t:BoatVisualDef")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<BoatVisualDef>)
                .Where(v => v != null && v.Deck != null)
                .ToArray();

            Assert.IsNotEmpty(wired, "no hull wears an imported deck — the import never ran");

            foreach (BoatVisualDef v in wired)
                Assert.IsTrue(v.Deck.HasWalkableDeck(),
                              $"{v.Id} points at {v.Deck.Id}, which has no walkable area");

            // Every committed def is worn by at least one hull.
            var referenced = new HashSet<BoatDeckDef>(wired.Select(v => v.Deck));
            var orphans = SidecarFiles.Select(DefFor)
                                      .Where(d => d != null && !referenced.Contains(d))
                                      .Select(d => d.Id).ToArray();
            Assert.IsEmpty(orphans, "imported but worn by nobody: " + string.Join(", ", orphans));
        }
    }
}
