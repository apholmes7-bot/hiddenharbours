using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;
using NUnit.Framework;
using Node = HiddenHarbours.App.Editor.KeySceneImport.YamlNode;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The key scene importer (O3) on a package built here, never CD's own (the package is not committed): it
    /// writes an asset as Unity writes one, round-trips, changes nothing when run twice, keeps by id, and stops
    /// where the owner must rule (the sums, a scene the island does not list, a mount it cannot read, a height
    /// off its anchor, a frozen piece no code places, an id that changes kit or kind, a retired id used again).
    /// It reads every id through the id map and the hosts' answers from their table (amendment 1), each a
    /// fixture of its own here.
    /// </summary>
    public class KeySceneImportTests
    {
        /// <summary>KeySceneDef.cs.meta's guid, which the step's assets name.</summary>
        const string ScriptGuid = "4c5d4ebc05de4cf6957872faaf3cb719";

        const string Region = "region.st_peters";
        const string Package = "P";
        const string CovePath = "scenes/keyscene.t_cove/scene.json";
        const string CoveAsset = "KeyScene_t_cove.asset";
        const string VillagePath = "scenes/keyscene.stp_village/scene.json";

        // The cove's pieces, with ` for ": one of each thing the importer must carry.
        const string Board = "{`id`: `prop.stp_slip_tide_board`, `kit`: `stPetersSetPieces`, `piece`: `slipTideBoard`, " +
                             "`defType`: `SetPieceDef`, `at`: [200.9, -3.62], `z`: -1.75, `dir`: 4, `opts`: {`foot`: -1.75}, " +
                             "`layer`: `raised`, `why`: `it reads the tide`}";

        const string Find = "{`id`: `prop.t_find`, `kit`: `shoreFinds`, `piece`: `Driftwood`, `defType`: `PropDef`, " +
                            "`at`: [1, 2], `z`: 0.5, `dir`: 0, `opts`: {`state`: `bleached`, `variant`: 1}}";

        const string Cannery = "{`id`: `structure.stp_cannery`, `kit`: `wharfBuilding2`, `piece`: `cannery`, " +
                               "`defType`: `BuildingDef`, `at`: [170, 16], `z`: 13, `dir`: 5, `frozen`: true}";

        const string Notice = "{`id`: `prop.t_notice`, `kit`: `stPetersSetPieces`, `piece`: `doorNotice`, `defType`: `SetPieceDef`, " +
                              "`at`: [175.52, 10.48], `z`: 7.2, `dir`: 4, `sortY`: 10.5, " +
                              "`mount`: `the cannery's door (anchor door, +1.2 m floor), +7.20`, " +
                              "`words`: [{`id`: `words.t_notice`, `text`: `NOTICE`}]}";

        const string Restored = "{`id`: `prop.t_stack_restored`, `kit`: `stPetersSetPieces`, `piece`: `boilerStack`, " +
                                "`defType`: `SetPieceDef`, `at`: [168, 18], `z`: 13, `dir`: 5, `variants`: [`restored`], " +
                                "`whenTide`: {`below`: -1.6}}";

        const string Lantern = "{`id`: `prop.t_lantern`, `kit`: `wharfDecor`, `piece`: `lanternPost`, `defType`: `SetPieceDef`, " +
                               "`at`: [184.5, 0.5], `z`: 5.35, `dir`: 4, `mount`: `context.wharf deck (WharfRig2 pier, frozen), +5.35`, " +
                               "`light`: {`preset`: `lanternPost`, `pool`: true, `reachM`: 3.6, `at`: [0, 0, 2.2], `colour`: `#ffc774`, " +
                               "`intensity`: 0.95, `when`: `dusk`, `whoLights`: `the keeper`, `budget`: 1}}";

        static readonly string[] CovePieces = { Board, Find, Cannery, Notice, Restored, Lantern };

        // A boulder whose id holds a position, and a gull the hosts' table stands on it by that id.
        const string Stone = "{`id`: `rock.t_stone@1,2`, `kit`: `pxRockIso3`, `piece`: `erratic`, `defType`: `RockFormDef`, " +
                             "`at`: [1, 2], `z`: 0.02, `dir`: 0}";

        const string Perch = "perched 1 m over the ground (a boulder, a ledge)";

        const string Perched = "{`id`: `gull.t_perched`, `kit`: `seagull`, `piece`: `gull`, `defType`: `seagull`, `at`: [1, 2], " +
                               "`z`: 1.02, `dir`: 0, `mount`: `" + Perch + "`}";

        const string PerchedAnswer = "{`piece`: `gull.t_perched`, `mount`: `" + Perch + "`, `standsOn`: `mount`, " +
                                     "`host`: `rock.t_stone@1,2`, `anchor`: `perch`, `answer`: `the desk's, on the boulder`}";

        const string StoneRenamed = "rock.t_stone_01";

        static string J(string s) => s.Replace('`', '"');

        /// <summary>An id map of the given old and new ids, in pairs.</summary>
        static string Map(params string[] oldNew) =>
            J("{`schema`: `hidden-harbours/key-scene-id-map@1`, `renames`: [" + string.Join(", ",
                Enumerable.Range(0, oldNew.Length / 2).Select(i => "{`old`: `" + oldNew[2 * i] + "`, `new`: `" + oldNew[2 * i + 1] + "`}")) + "]}");

        /// <summary>A hosts' table of the given answers.</summary>
        static string Hosts(params string[] answers) =>
            J("{`schema`: `hidden-harbours/key-scene-hosts@1`, `about`: `a fixture`, `hosts`: [" + string.Join(", ", answers) + "]}");

        static string Sha(byte[] b)
        {
            using (SHA256 sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(b).Select(x => x.ToString("x2")));
        }

        /// <summary>A package in memory, path to text. The sums are made from the files unless a test breaks them.</summary>
        sealed class Fixture
        {
            public readonly Dictionary<string, string> Files = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly HashSet<string> Unsummed = new HashSet<string>(StringComparer.Ordinal);
            public readonly Dictionary<string, string> Tampered = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly List<string> ExtraSumLines = new List<string>();

            /// <summary>The id map and the hosts' table beside the package (null: not there).</summary>
            public string IdMap = Map(), HostsTable = Hosts();

            public Func<string, byte[]> Reader()
            {
                var bytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                var sums = new StringBuilder();
                foreach (KeyValuePair<string, string> f in Files.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    byte[] b = Encoding.UTF8.GetBytes(f.Value);
                    if (!Unsummed.Contains(f.Key)) sums.Append(Sha(b)).Append("  ").Append(f.Key).Append('\n');
                    bytes[f.Key] = Tampered.TryGetValue(f.Key, out string t) ? Encoding.UTF8.GetBytes(t) : b;
                }
                foreach (string line in ExtraSumLines) sums.Append(line).Append('\n');
                bytes[KeySceneImport.SumsFile] = Encoding.UTF8.GetBytes(sums.ToString());
                return p => bytes.TryGetValue(p, out byte[] b) ? b : null;
            }
        }

        /// <summary>The cove (the given pieces, else all six), the village's file (never JSON: never read), a look
        /// pass with no file, and a ground file holding a pond.</summary>
        static Fixture Cove(params string[] pieces)
        {
            if (pieces.Length == 0) pieces = CovePieces;
            var f = new Fixture();
            f.Files[KeySceneImport.IslandFile] = J(
                "{`schema`: `hidden-harbours/island@1`, `id`: `island.t`, `ground`: `ground/g.json (with its stillWater)`, `scenes`: [" +
                "{`id`: `keyscene.t_cove`, `file`: `" + CovePath + "`}, " +
                "{`id`: `keyscene.stp_village`, `file`: `" + VillagePath + "`}, " +
                "{`id`: `lookpass.t_end`, `box`: [0, 0, 1, 1]}]}");
            f.Files["ground/g.json"] = J("{`id`: `ground.t_island`, `stillWater`: [{`id`: `pond.t_pool`, `z`: 2.05}]}");
            f.Files[CovePath] = J("{`schema`: `hidden-harbours/keyscene@1`, `id`: `keyscene.t_cove`, `name`: `The cove`, " +
                                  "`context`: {`wharf`: {`deck`: 5.35}}, `pieces`: [" + string.Join(", ", pieces) + "]}");
            f.Files[VillagePath] = "the village's 121 pieces: not JSON, because the importer never reads it";
            return f;
        }

        static KeySceneImport.Result Import(Fixture f, IReadOnlyDictionary<string, string> existing = null) =>
            KeySceneImport.Import(Package, f.Reader(), existing ?? new Dictionary<string, string>(), Region, ScriptGuid,
                                  f.IdMap == null ? null : Encoding.UTF8.GetBytes(f.IdMap),
                                  f.HostsTable == null ? null : Encoding.UTF8.GetBytes(f.HostsTable));

        static Node Field(List<KeyValuePair<string, Node>> map, string key) => map.FirstOrDefault(k => k.Key == key).Value;

        static List<Node> Rows(KeySceneImport.Result r) =>
            Field(KeySceneImport.ReadAsset(r.Assets[CoveAsset], CoveAsset), "Pieces").Items;

        static Node Row(KeySceneImport.Result r, string id) => Rows(r).Single(n => n.Get("Id").Scalar == id);

        static string[] Fields(Type t) =>
            t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
             .OrderBy(f => f.MetadataToken).Select(f => f.Name).ToArray();

        static void Refuses(Fixture f, string why, IReadOnlyDictionary<string, string> existing = null)
        {
            var refusal = Assert.Throws<KeySceneImport.Refusal>(() => Import(f, existing));
            StringAssert.Contains(why, refusal.Message);
        }

        [Test]
        public void AnAsset_IsWrittenAsUnityWritesIt()
        {
            Fixture f = Cove(Board);
            KeySceneImport.Result r = Import(f);
            byte[] scene = Encoding.UTF8.GetBytes(f.Files[CovePath]);

            // The header and the row are #913's own lines (KeyScene_stp_landing.asset), with O3's fields between.
            string expected = string.Join("\n", new[]
            {
                "%YAML 1.1",
                "%TAG !u! tag:unity3d.com,2011:",
                "--- !u!114 &11400000",
                "MonoBehaviour:",
                "  m_ObjectHideFlags: 0",
                "  m_CorrespondingSourceObject: {fileID: 0}",
                "  m_PrefabInstance: {fileID: 0}",
                "  m_PrefabAsset: {fileID: 0}",
                "  m_GameObject: {fileID: 0}",
                "  m_Enabled: 1",
                "  m_EditorHideFlags: 0",
                "  m_Script: {fileID: 11500000, guid: 4c5d4ebc05de4cf6957872faaf3cb719, type: 3}",
                "  m_Name: KeyScene_t_cove",
                "  m_EditorClassIdentifier: HiddenHarbours.World::HiddenHarbours.World.KeySceneDef",
                "  Id: keyscene.t_cove",
                "  DisplayName: The cove",
                "  RegionId: region.st_peters",
                $"  Source: P, {CovePath}, {scene.Length} bytes, sha256 {Sha(scene)}",
                "  Placed: 0",
                "  Pieces:",
                "  - Id: prop.stp_slip_tide_board",
                "    Kind: setPiece",
                "    Owner: ",
                "    Kit: stPetersSetPieces",
                "    Piece: slipTideBoard",
                "    State: ",
                "    At: {x: 200.9, y: -3.62}",
                "    Z: -1.75",
                "    StandsOn: ground",
                "    MountHost: ",
                "    MountAnchor: ",
                "    HasFoot: 0",
                "    Foot: {x: 0, y: 0}",
                "    Dir: 4",
                "    HasSortY: 0",
                "    SortY: 0",
                "    Layer: raised",
                "    Walk: ",
                "    Collider: ",
                "    Variants:",
                "    - today",
                "    WhenTide: ",
                "    Words: []",
                "    Options:",
                "    - Key: foot",
                "      Value: -1.75",
                "  Lights: []",
                "  RetiredIds: []",
                "  PlacementIds: []",
                "",
            });
            Assert.AreEqual(expected, r.Assets[CoveAsset]);
            Assert.AreEqual(KeySceneImport.MetaText(KeySceneImport.GuidFor("keyscene.t_cove")), r.Metas[CoveAsset + ".meta"]);
            StringAssert.StartsWith("fileFormatVersion: 2\nguid: ", r.Metas[CoveAsset + ".meta"]);
            Assert.AreEqual(KeySceneImport.GuidFor("keyscene.t_cove"), KeySceneImport.GuidFor("keyscene.t_cove"), "the same scene, the same guid");
        }

        [Test]
        public void TheWriter_FoldsAndQuotes_AsUnityDoes()
        {
            // Each line as Unity saved it in a committed asset (named), the value it holds, and the key's indent.
            var cases = new[]
            {
                // Data/Boats/Interiors/CapeIslanderIso.asset:204, a plain scalar folded past column 80.
                new[] { "4", "NotPlacedBecause", "its opening names a bulkhead line (at_y), not a hole with a run to walk",
                        "    NotPlacedBecause: its opening names a bulkhead line (at_y), not a hole with a\n      run to walk\n" },
                // Data/Boats/Interiors/CoastalPacketIso.asset:310, single-quoted for its ': ', folded.
                new[] { "4", "NotPlacedBecause", "it does not name the two floors it joins (connects: [foot, head])",
                        "    NotPlacedBecause: 'it does not name the two floors it joins (connects: [foot,\n      head])'\n" },
                // Data/Boats/PaintSchemes/CapeIslanderIso_BanksWhite.asset:17, double-quoted for its dash, folded.
                new[] { "2", "Note", "white topsides over her own green boot \u2014 half a Cape Island wharf in one scheme",
                        "  Note: \"white topsides over her own green boot \\u2014 half a Cape Island wharf in\n    one scheme\"\n" },
                // Data/StationPieces/dispenser_sDock.asset:74, a Latin-1 character escaped short.
                new[] { "4", "Label", "HOSE A1 \u00B7 GASOLINE", "    Label: \"HOSE A1 \\xB7 GASOLINE\"\n" },
                // #913's own: an empty string keeps its space, a number-like string stays plain, a list single-quoted.
                new[] { "4", "State", "", "    State: \n" },
                new[] { "6", "Value", "-1.75", "      Value: -1.75\n" },
                new[] { "6", "Value", "[[0,0],[4.01,-4.17],[6.41,-7.17],[8.01,-9.27]]", "      Value: '[[0,0],[4.01,-4.17],[6.41,-7.17],[8.01,-9.27]]'\n" },
                new[] { "6", "Value", "true", "      Value: true\n" },
                new[] { "4", "Colour", "#ffc774", "    Colour: '#ffc774'\n" },
                new[] { "4", "Note", "it's the keeper\u2019s", "    Note: \"it's the keeper\\u2019s\"\n" },
                // A no-break space: no committed asset holds one, so libyaml's own escape (Unity's writer is libyaml's,
                // as the folds and the short escapes above show).
                new[] { "4", "Note", "a 6\u00a0m dory", "    Note: \"a 6\\_m dory\"\n" },
            };
            foreach (string[] c in cases)
            {
                Assert.AreEqual(c[3], KeySceneImport.ScalarText(c[1], int.Parse(c[0]), c[2]), c[1] + ": " + c[2]);
                string back = Field(KeySceneImport.ReadAsset("MonoBehaviour:\n" + KeySceneImport.ScalarText(c[1], 2, c[2]), "case"), c[1]).Scalar;
                Assert.AreEqual(c[2], back, "read back: " + c[2]);
            }
        }

        [Test]
        public void Import_OfAFixture_RoundTrips_AndASecondImportChangesNothing()
        {
            KeySceneImport.Result first = Import(Cove());
            CollectionAssert.AreEqual(new[] { CoveAsset }, first.Assets.Keys.ToArray(), "the cove only: the village is never read, the look pass has no file");
            CollectionAssert.AreEqual(new[] { CoveAsset }, first.Changed.ToArray());
            CollectionAssert.AreEqual(new[] { CoveAsset + ".meta" }, first.Metas.Keys.ToArray());

            Node find = Row(first, "prop.t_find");
            Assert.AreEqual("bleached", find.Get("State").Scalar);
            Assert.AreEqual("[{Key=state, Value=bleached} | {Key=variant, Value=1}]", find.Get("Options").Canon());
            Assert.AreEqual("{x: 1, y: 2}", find.Get("At").Scalar);
            Assert.AreEqual("{\"below\":-1.6}", Row(first, "prop.t_stack_restored").Get("WhenTide").Scalar);
            CollectionAssert.AreEqual(new[] { "prop.stp_slip_tide_board", "prop.t_find", "structure.stp_cannery", "prop.t_notice",
                                              "prop.t_stack_restored", "prop.t_lantern" },
                                      Rows(first).Select(n => n.Get("Id").Scalar).ToArray(), "CD's order");

            KeySceneImport.Result second = Import(Cove(), first.Assets);
            CollectionAssert.IsEmpty(second.Changed, "run twice, it changes nothing");
            CollectionAssert.IsEmpty(second.Metas, "and makes no second meta");
            Assert.AreEqual(first.Assets[CoveAsset], second.Assets[CoveAsset]);
        }

        [Test]
        public void APlacementSelection_SurvivesImport_AndRefusesAMissingSelectedId()
        {
            var first = Import(Cove());
            first.Assets[CoveAsset] = first.Assets[CoveAsset].Replace("  PlacementIds: []", "  PlacementIds:\n  - prop.t_find");
            var second = Import(Cove(), first.Assets);
            CollectionAssert.IsEmpty(second.Changed);
            StringAssert.Contains("  PlacementIds:\n  - prop.t_find", second.Assets[CoveAsset]);
            first.Assets[CoveAsset] = first.Assets[CoveAsset].Replace("  - prop.t_find", "  - prop.t_missing");
            Refuses(Cove(), "selected placement id", first.Assets);
        }

        [Test]
        public void EastEndImport_RefusesAnyOtherSourceBeforeItReadsGround()
        {
            bool readGround = false;
            Assert.Throws<KeySceneImport.Refusal>(() => KeySceneImport.ImportEastEnd(
                Encoding.UTF8.GetBytes("{}"), at => { readGround = true; return 0f; }, null, ScriptGuid));
            Assert.IsFalse(readGround);
        }

        [Test]
        public void AFrozenPiece_ALight_AndAPieceWithoutToday_AreRecorded_AsData()
        {
            KeySceneImport.Result r = Import(Cove());
            List<KeyValuePair<string, Node>> asset = KeySceneImport.ReadAsset(r.Assets[CoveAsset], CoveAsset);
            Assert.AreEqual("0", Field(asset, "Placed").Scalar, "a new scene comes in unplaced: its wave turns it on");

            Node cannery = Row(r, "structure.stp_cannery");
            Assert.AreEqual("StPetersCannery", cannery.Get("Owner").Scalar, "a frozen piece names the code that places it");
            Assert.AreEqual(KeySceneDef.KindBuilding, cannery.Get("Kind").Scalar);
            Assert.AreEqual("{x: 170, y: 16}", cannery.Get("At").Scalar, "a round number as Unity writes it, never 1.7E+02");
            Assert.AreEqual("13", cannery.Get("Z").Scalar);
            Assert.AreEqual("[restored]", Row(r, "prop.t_stack_restored").Get("Variants").Canon(), "a later board's piece keeps no today");

            Node notice = Row(r, "prop.t_notice");
            Assert.AreEqual(KeySceneDef.StandsOnMount, notice.Get("StandsOn").Scalar);
            Assert.AreEqual("structure.stp_cannery", notice.Get("MountHost").Scalar);
            Assert.AreEqual("door", notice.Get("MountAnchor").Scalar);
            Assert.AreEqual("[words.t_notice]", notice.Get("Words").Canon(), "words by id; the text is the words table's");
            Assert.AreEqual("1", notice.Get("HasSortY").Scalar);
            Assert.AreEqual("10.5", notice.Get("SortY").Scalar);
            Assert.AreEqual(KeySceneDef.StandsOnDeck, Row(r, "prop.t_lantern").Get("StandsOn").Scalar);

            List<Node> lights = Field(asset, "Lights").Items;
            Assert.AreEqual(1, lights.Count);
            Assert.AreEqual("{PieceId=prop.t_lantern, Preset=lanternPost, Pool=1, ReachMetres=3.6, At={x: 0, y: 0, z: 2.2}, " +
                            "Colour=#ffc774, Intensity=0.95, When=dusk, WhoLights=the keeper, Budget=1, Note=}", lights[0].Canon());

            KeySceneImport.Tally t = r.Tallies["keyscene.t_cove"];
            Assert.AreEqual(new[] { 6, 5, 1, 1, 1, 2, 1, 1 }, new[] { t.Pieces, t.Today, t.Data, t.Frozen, t.Owned, t.Mounted, t.OnHost, t.Lights });
            Assert.IsTrue(r.Report.Any(l => l.StartsWith("refused: keyscene.stp_village", StringComparison.Ordinal)), string.Join("\n", r.Report));
        }

        [Test]
        public void ARemovedId_IsRetired_AndNeverReused()
        {
            KeySceneImport.Result first = Import(Cove());
            KeySceneImport.Result second = Import(Cove(Board, Cannery, Notice, Restored, Lantern), first.Assets);
            CollectionAssert.AreEqual(new[] { CoveAsset }, second.Changed.ToArray());
            Assert.AreEqual("[prop.t_find]", Field(KeySceneImport.ReadAsset(second.Assets[CoveAsset], CoveAsset), "RetiredIds").Canon());
            Assert.IsFalse(Rows(second).Any(n => n.Get("Id").Scalar == "prop.t_find"));
            Assert.Contains("  retired: prop.t_find", second.Report);

            Refuses(Cove(), "never reused", second.Assets);
        }

        [Test]
        public void AnIdThatChangesKit_Refuses()
        {
            KeySceneImport.Result first = Import(Cove());
            Refuses(Cove(Board, Find.Replace("`shoreFinds`", "`wharfDecor`"), Cannery, Notice, Restored, Lantern), "changes kit", first.Assets);
        }

        [Test]
        public void AnIdThatChangesKind_Refuses()
        {
            KeySceneImport.Result first = Import(Cove());
            Refuses(Cove(Board, Find.Replace("`PropDef`", "`RockFormDef`"), Cannery, Notice, Restored, Lantern), "changes kind", first.Assets);
        }

        [Test]
        public void TheVillagesFile_IsNeverRead_AndAFileTheIslandDoesNotList_Refuses()
        {
            KeySceneImport.Result r = Import(Cove());
            Assert.IsFalse(r.Assets.Keys.Any(k => k.Contains("village")), "no asset for the village");

            Fixture stray = Cove();
            stray.Files["scenes/keyscene.t_stray/scene.json"] = J("{`id`: `keyscene.t_stray`, `name`: `Stray`, `pieces`: []}");
            Refuses(stray, "island.json does not list it");
        }

        [Test]
        public void TheSums_MustHold()
        {
            Fixture tampered = Cove();
            tampered.Tampered[CovePath] = tampered.Files[CovePath].Replace("-3.62", "-3.63");
            Refuses(tampered, "sha256");

            Fixture missing = Cove();
            missing.ExtraSumLines.Add(new string('0', 64) + "  scenes/keyscene.t_gone/scene.json");
            Refuses(missing, "not in the package");

            Fixture unsummed = Cove();
            unsummed.Unsummed.Add(CovePath);
            Refuses(unsummed, "not in SHA256SUMS");
        }

        [Test]
        public void AMountTheTableCannotRead_Refuses()
        {
            Refuses(Cove(Board, Find, Cannery, Notice.Replace("(anchor door,", "(the door,"), Restored, Lantern), "mount table cannot read");
        }

        [Test]
        public void AMountsHeight_OffItsAnchor_Refuses()
        {
            Refuses(Cove(Board, Find, Cannery, Notice.Replace("`z`: 7.2,", "`z`: 7.26,"), Restored, Lantern), "more than 5 cm");
            Assert.DoesNotThrow(() => Import(Cove(Board, Find, Cannery, Notice.Replace("`z`: 7.2,", "`z`: 7.25,"), Restored, Lantern)),
                                "5 cm exactly is on the anchor");
        }

        [Test]
        public void ARowThatDiffersInMoreThanHeight_IsListed()
        {
            KeySceneImport.Result first = Import(Cove());
            KeySceneImport.Result height = Import(Cove(Board.Replace("`z`: -1.75", "`z`: -1.7"), Find, Cannery, Notice, Restored, Lantern), first.Assets);
            Assert.Contains("  height: prop.stp_slip_tide_board: Z -1.75 -> -1.7", height.Report);
            Assert.IsFalse(height.Report.Any(l => l.Contains("MORE THAN HEIGHT")), string.Join("\n", height.Report));

            KeySceneImport.Result turned = Import(Cove(Board, Find.Replace("`dir`: 0", "`dir`: 3"), Cannery, Notice, Restored, Lantern), first.Assets);
            Assert.Contains("  MORE THAN HEIGHT: prop.t_find: Dir 0 -> 3", turned.Report);
        }

        [Test]
        public void AnAssetFromBeforeTheNewFields_KeepsItsPlacing_AndNamesTheAdditions()
        {
            KeySceneImport.Result first = Import(Cove(Board));
            string[] o3 = { "Placed", "Kind", "Owner", "MountHost", "MountAnchor", "HasFoot", "Foot", "Layer", "Walk", "Collider",
                            "WhenTide", "Lights", "RetiredIds" };
            string before = string.Join("\n", first.Assets[CoveAsset].Split('\n')
                .Where(l => !o3.Any(f => l.TrimStart(' ', '-').StartsWith(f + ":", StringComparison.Ordinal))));
            var existing = new Dictionary<string, string> { { CoveAsset, before } };

            KeySceneImport.Result r = Import(Cove(Board), existing);
            Assert.AreEqual("1", Field(KeySceneImport.ReadAsset(r.Assets[CoveAsset], CoveAsset), "Placed").Scalar,
                            "an asset with no Placed line is placed, as the game reads it");
            Assert.IsTrue(r.Report.Any(l => l.StartsWith("  fields new to the asset: ", StringComparison.Ordinal)
                                          && l.Contains("Kind") && l.Contains("RetiredIds")), string.Join("\n", r.Report));
            Assert.IsFalse(r.Report.Any(l => l.Contains("MORE THAN HEIGHT") || l.Contains("height:")), string.Join("\n", r.Report));
        }

        [Test]
        public void TheTypeMap_AndTheOwnerTable_Refuse_WhatTheyDoNotName()
        {
            Refuses(Cove(Board, Find.Replace("`PropDef`", "`MysteryDef`")), "not in the type map");
            Refuses(Cove(Board, Find.Replace("`shoreFinds`", "`seagull`")), "not in the type map");
            Refuses(Cove(Board, Find.Replace("`dir`: 0", "`dir`: 0, `frozen`: true")), "names no code that places it");
            Refuses(Cove(Board, Cannery.Replace(", `frozen`: true", "")), "does not freeze it");
            Refuses(Cove(Board, Find.Replace("`dir`: 0", "`dir`: 0, `taken`: false")), "it is not taken");
            Refuses(Cove(Board, Find.Replace("`dir`: 0", "`dir`: 0, `sparkle`: 1")), "new to the importer");
        }

        [Test]
        public void AColliderShape_IsKept_AsCompactJson_AndALayerMustBeAString()
        {
            // The gap bridge's collider is the shape its kit builds from; a layer is only ever a name.
            string shaped = Find.Replace("`dir`: 0", "`dir`: 0, `collider`: {`deck`: [[-1.5, -0.575], [1.5, 0.575]], `walk`: `on`, `turn`: -11}");
            Assert.AreEqual("{\"deck\":[[-1.5,-0.575],[1.5,0.575]],\"walk\":\"on\",\"turn\":-11}",
                            Row(Import(Cove(Board, shaped)), "prop.t_find").Get("Collider").Scalar);
            Assert.AreEqual("polygon 0.40 x 0.42 m",
                            Row(Import(Cove(Board.Replace("`layer`", "`collider`: `polygon 0.40 x 0.42 m`, `layer`"))), "prop.stp_slip_tide_board").Get("Collider").Scalar);
            Refuses(Cove(Board, Find.Replace("`dir`: 0", "`dir`: 0, `layer`: 2")), "layer is not a string");
        }

        [Test]
        public void AContextHost_IsItsOwnScenes_NeverAnothers()
        {
            const string BayPath = "scenes/keyscene.t_bay/scene.json";
            const string Bollard = "context.wharf, the pier head\u2019s bollard on the deck, +5.72";
            const string Gull = "{`id`: `prop.t_bay_gull`, `kit`: `seagull`, `piece`: `gull`, `defType`: `seagull`, `at`: [190, 2], " +
                                "`z`: 5.72, `dir`: 2, `mount`: `" + Bollard + "`}";
            Fixture f = Cove(Board);
            f.HostsTable = Hosts("{`piece`: `prop.t_bay_gull`, `mount`: `" + Bollard + "`, `standsOn`: `mount`, `host`: `context.wharf`, " +
                                 "`anchor`: `northPilehead`, `answer`: `the desk's: the pilehead`}");
            f.Files[KeySceneImport.IslandFile] = f.Files[KeySceneImport.IslandFile].Replace(
                J("{`id`: `keyscene.stp_village`"), J("{`id`: `keyscene.t_bay`, `file`: `" + BayPath + "`}, {`id`: `keyscene.stp_village`"));

            f.Files[BayPath] = J("{`id`: `keyscene.t_bay`, `name`: `The bay`, `pieces`: [" + Gull + "]}");
            KeySceneImport.Result r = Import(f);
            CollectionAssert.AreEqual(new[] { "prop.t_bay_gull" }, r.UnheldHosts["context.wharf"], "the cove's wharf is not the bay's");

            f.Files[BayPath] = J("{`id`: `keyscene.t_bay`, `name`: `The bay`, `context`: {`wharf`: {`deck`: 5.35}}, `pieces`: [" + Gull + "]}");
            r = Import(f);
            CollectionAssert.IsEmpty(r.UnheldHosts);
            Assert.Contains("mount: keyscene.t_bay prop.t_bay_gull on context.wharf at northPilehead, z 5.72; host held: keyscene.t_bay's context; " +
                            "the hosts' table", r.Report);
        }

        [Test]
        public void EveryId_GoesThroughTheMap_ARowsOwnAndAHosts()
        {
            Fixture f = Cove(Board, Stone, Perched);
            f.IdMap = Map("rock.t_stone@1,2", StoneRenamed, "rock.t_gone@5,5", "rock.t_gone_01");
            f.HostsTable = Hosts(PerchedAnswer);
            KeySceneImport.Result r = Import(f);

            CollectionAssert.AreEqual(new[] { "prop.stp_slip_tide_board", StoneRenamed, "gull.t_perched" },
                                      Rows(r).Select(n => n.Get("Id").Scalar).ToArray(), "a row keeps its place under its new id");
            Node gull = Row(r, "gull.t_perched");
            Assert.AreEqual(KeySceneDef.StandsOnMount, gull.Get("StandsOn").Scalar);
            Assert.AreEqual(StoneRenamed, gull.Get("MountHost").Scalar, "a host's id goes through the map as a row's does");
            Assert.AreEqual("perch", gull.Get("MountAnchor").Scalar);
            CollectionAssert.IsEmpty(r.UnheldHosts, "the host is found by its new id");
            Assert.Contains("renamed: keyscene.t_cove rock.t_stone@1,2 -> " + StoneRenamed, r.Report);
            Assert.Contains("mount: keyscene.t_cove gull.t_perched on " + StoneRenamed + " at perch, z 1.02; host held: a piece of keyscene.t_cove; " +
                            "the hosts' table", r.Report);
            Assert.Contains("the id map renames rock.t_gone@5,5, and the package holds no such id", r.Report);
            StringAssert.DoesNotContain(KeySceneImport.PositionMark.ToString(), r.Assets[CoveAsset], "no id that holds a position reaches an asset");

            KeySceneImport.Result again = Import(f, r.Assets);
            CollectionAssert.IsEmpty(again.Changed, "imported again through the same map, nothing changes");
        }

        [Test]
        public void AnIdThatHoldsAPosition_TheMapDoesNotRename_IsAStopForTheArtDesk()
        {
            const string Stop = "holds a position, and the id map does not rename it. A STOP for the Art desk";
            Refuses(Cove(Board, Stone), Stop);

            Fixture host = Cove(Board, Perched);
            host.HostsTable = Hosts(PerchedAnswer);
            Refuses(host, Stop);

            Refuses(Cove(Board, Cannery, Notice.Replace("words.t_notice", "words.t_notice@1,1")), Stop);

            Fixture ground = Cove(Board);
            ground.Files["ground/g.json"] = J("{`id`: `ground.t_island`, `stillWater`: [{`id`: `pond.t_pool@3,4`, `z`: 2.05}]}");
            Refuses(ground, Stop);
        }

        [Test]
        public void TheHostsTable_AnswersAPiece_ByItsIdAndItsWords_AndANewWordingRefuses()
        {
            Fixture Answered(string gull, params string[] answers)
            {
                Fixture f = Cove(gull == null ? new[] { Board, Stone } : new[] { Board, Stone, gull });
                f.IdMap = Map("rock.t_stone@1,2", StoneRenamed);
                f.HostsTable = Hosts(answers);
                return f;
            }

            Assert.DoesNotThrow(() => Import(Answered(Perched, PerchedAnswer)));
            Refuses(Answered(Perched), "mount table cannot read");
            Refuses(Answered(Perched.Replace("perched 1 m", "perched 1.0 m"), PerchedAnswer), "A new wording is a new answer");
            Refuses(Answered(null, PerchedAnswer), "A stale answer");
            Refuses(Answered(Perched.Replace(", `mount`: `" + Perch + "`", ""), PerchedAnswer), "the package gives it none");
            Refuses(Answered(Perched, PerchedAnswer, PerchedAnswer), "answers gull.t_perched twice");
            Refuses(Answered(Perched, PerchedAnswer.Replace("`standsOn`: `mount`", "`standsOn`: `deck`")), "on a host or on its own ground");
            Refuses(Answered(Perched, PerchedAnswer.Replace("`anchor`: `perch`", "`anchor`: ``")), "names its host by id and the host's anchor");
            Refuses(Answered(Perched, PerchedAnswer.Replace("`standsOn`: `mount`", "`standsOn`: `ground`")), "on its own ground, it names no host");
            Refuses(Answered(Perched, PerchedAnswer.Replace("`answer`: `the desk's, on the boulder`", "`answer`: ``")), "whose answer it is");

            // On its own ground, as the reef's gull: no host, and the report says whose answer it is.
            string own = PerchedAnswer.Replace("`standsOn`: `mount`, `host`: `rock.t_stone@1,2`, `anchor`: `perch`",
                                               "`standsOn`: `ground`, `host`: ``, `anchor`: ``");
            KeySceneImport.Result r = Import(Answered(Perched, own));
            Node gull = Row(r, "gull.t_perched");
            Assert.AreEqual(KeySceneDef.StandsOnGround, gull.Get("StandsOn").Scalar);
            Assert.AreEqual("", gull.Get("MountHost").Scalar);
            Assert.Contains("mount: keyscene.t_cove gull.t_perched stands on its own ground, z 1.02, on no host; the hosts' table", r.Report);

            Fixture none = Answered(Perched, PerchedAnswer);
            none.HostsTable = null;
            Refuses(none, "no hosts' table");
        }

        [Test]
        public void TheIdMap_RenamesEachIdOnce_InOneStep_ToAnIdThatHoldsNoPosition()
        {
            Fixture With(string map)
            {
                Fixture f = Cove(Board);
                f.IdMap = map;
                return f;
            }

            Assert.DoesNotThrow(() => Import(With(Map("a.b@1,1", "a.b_01"))));
            Refuses(With(Map().Replace("key-scene-id-map@1", "village-id-map@1")), "not hidden-harbours/key-scene-id-map@1");
            Refuses(With(Map("a.b@1,1", "a.b_01", "a.b@1,1", "a.b_02")), "renames a.b@1,1 twice");
            Refuses(With(Map("a.b@1,1", "a.b_01", "a.b@2,2", "a.b_01")), "gives a.b_01 to two ids");
            Refuses(With(Map("a.b@1,1", "a.b@2,2")), "a new id never does");
            Refuses(With(Map("a.b@1,1", "a.b_01", "a.b_01", "a.b_02")), "A rename is one step");
            Refuses(With(Map("a.b@1,1", "a.b@1,1")), "is not a rename");
            Refuses(With(Map("a.b@1,1", "a.b_01").Replace("`a.b_01`}".Replace('`', '"'), J("`a.b_01`, `why`: `x`}"))), "new to the importer");
            Refuses(With(null), "no id map");
        }

        [Test]
        public void TheImportersFields_AreTheDefsOwn_InOrder()
        {
            CollectionAssert.AreEqual(KeySceneImport.SceneFields, Fields(typeof(KeySceneDef)));
            CollectionAssert.AreEqual(KeySceneImport.PieceFields, Fields(typeof(KeyScenePiece)));
            CollectionAssert.AreEqual(KeySceneImport.OptionFields, Fields(typeof(KeySceneOption)));
            CollectionAssert.AreEqual(KeySceneImport.LightFields, Fields(typeof(KeySceneLight)));

            KeySceneImport.Result r = Import(Cove());
            List<KeyValuePair<string, Node>> asset = KeySceneImport.ReadAsset(r.Assets[CoveAsset], CoveAsset);
            CollectionAssert.AreEqual(KeySceneImport.SceneFields, asset.Select(k => k.Key).Where(k => !k.StartsWith("m_", StringComparison.Ordinal)).ToArray());
            foreach (Node row in Rows(r))
                CollectionAssert.AreEqual(KeySceneImport.PieceFields, row.Map.Select(k => k.Key).ToArray(), row.Get("Id").Scalar);
            foreach (Node light in Field(asset, "Lights").Items)
                CollectionAssert.AreEqual(KeySceneImport.LightFields, light.Map.Select(k => k.Key).ToArray());
        }
    }
}
