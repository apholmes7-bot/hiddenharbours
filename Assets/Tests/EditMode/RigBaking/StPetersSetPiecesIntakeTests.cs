using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The St Peters set-pieces kit (Claude Design's key-scenes return, 2026-09-27) as it landed: fourteen
    /// pinned files, eight sidecars the kit still writes, two lib copies held to their canonical files, the
    /// today file the bake reads, and no reader of <c>*.gameplay.json</c> in the repo that can see the kit's
    /// own gameplay folder. Nothing here needs the baked sheets; <see cref="SetPieceDefTests"/> holds those.
    ///
    /// <para>Every bar is written here, in the test: the kit's files and the drop's hashes, the readers'
    /// folders as the readers themselves declare them. None is asked of the code under test.</para>
    /// </summary>
    public class StPetersSetPiecesIntakeTests
    {
        internal const string Kit = "docs/art/rigs/st-peters-set-pieces-kit/";
        internal const string TodayPath = "Assets/_Project/Art/Sprites/StPeters/SetPieces/stPetersSetPieces.today.json";
        const string TodaySha256Lf = "86b1557a29b3ed105fe20c5550fcdcecee76542502468982d90c7c97d8ddbe95";

        /// <summary>The eight pieces, rig key to Def id, in the kit's KEYS order.</summary>
        internal static readonly (string Key, string Id)[] Pieces =
        {
            ("slipTideBoard", "prop.stp_slip_tide_board"),
            ("harbourVane", "prop.stp_harbour_vane"),
            ("boilerStack", "structure.stp_cannery_boiler_stack"),
            ("trolleyLine", "structure.stp_cannery_trolley_line"),
            ("trolley", "prop.stp_cannery_trolley"),
            ("conveyor", "prop.stp_cannery_conveyor"),
            ("fallenSign", "prop.stp_cannery_fallen_sign"),
            ("doorNotice", "prop.stp_cannery_door_notice"),
        };

        /// <summary>The kit's scripts in the order its checker loads them.</summary>
        internal static readonly string[] Scripts = { "lib/weatherSky.js", "lib/coastalPass.js", "stPetersSetPieces.js" };

        /// <summary>The delivered files and the drop's SHA-256 for each: a text file's LF bytes (the kit has no
        /// eol attribute, so a Windows checkout may hold it CRLF), the board's raw bytes.</summary>
        static readonly (string File, string Sha256)[] Delivered =
        {
            ("boards/set-pieces.png", "6b53b061aa70cf6a589d5021d375e938d7dee759d0bd5a49cd69ee311c26ec28"),
            ("checks/check-kit.cjs", "babfa8186ee62eed8c621f3283bc4d433ab14199c51ac3f0339e0c14b31a8d31"),
            ("gameplay/prop.stp_cannery_conveyor.gameplay.json", "be348bf034b13edff939001200e2b7b78f2d36150b400d13bc7ca1428d6bdd61"),
            ("gameplay/prop.stp_cannery_door_notice.gameplay.json", "cc9f0802368d1cb2767d956d371f499b870c38d02e41547514da648860d3edb0"),
            ("gameplay/prop.stp_cannery_fallen_sign.gameplay.json", "3e8f2fcb7520fa6500df59759a5948facd680da514395dbdfe0bd54c6a4f653c"),
            ("gameplay/prop.stp_cannery_trolley.gameplay.json", "04a07bdcc790aa96cf57c2964d91f128da2b69f6fae63fb09f0fcf50c939bf58"),
            ("gameplay/prop.stp_harbour_vane.gameplay.json", "fecf6a8b21c6d73e3a925bd6753cbb8b35cb9efbf455707e05cfb47d3504836a"),
            ("gameplay/prop.stp_slip_tide_board.gameplay.json", "9a5803dbb6c326dd6bb0b09de3f9aeeb1e6b09e4fd583e77d632b1f55470207b"),
            ("gameplay/structure.stp_cannery_boiler_stack.gameplay.json", "1bd56390c698049db955f20ad821effaf4add4f088d460e169d3d1d3cdd2bccb"),
            ("gameplay/structure.stp_cannery_trolley_line.gameplay.json", "6b152760198c643791c4cd8954bf3a33bcd40cf83cbfa7177416039d6ba79316"),
            ("harness.html", "a68769a55b2c4e6d8b372c44fc11257c391ae3184596a512ce28abcf9a240b90"),
            ("lib/coastalPass.js", "041b0bc32040c10a1bafc57da1d3f19e76047285c2b802861cff67caca5b166d"),
            ("lib/weatherSky.js", "38300697d5fe685b4800b2a5252aa3c9f0fbcb08bad40e80f6ce3aee1e9c7062"),
            ("stPetersSetPieces.js", "4b6df7c1033703c533e3ad570b548dfdfa779a63cf46f8b59fa2db46b12b1b9f"),
        };

        // ---- helpers the set-piece tests share ------------------------------------------------------------

        internal static string Abs(string repoRelative) => Path.Combine(RigCatalog.RepoRoot, repoRelative);
        internal static string Lf(string text) => text.Replace("\r\n", "\n");

        internal static string Sha(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        internal static string ShaLf(string repoRelative) =>
            Sha(Encoding.UTF8.GetBytes(Lf(File.ReadAllText(Abs(repoRelative)))));

        internal static string SidecarFile(string id) => $"{Kit}gameplay/{id}.gameplay.json";
        internal static object ReadJson(string repoRelative) => DeckSidecarJson.Parse(File.ReadAllText(Abs(repoRelative)));
        internal static object Member(object owner, string key) => DeckSidecarJson.Member(owner, key);
        internal static List<object> Items(object v) => DeckSidecarJson.AsArray(v) ?? new List<object>();
        internal static string Str(object owner, string key) => DeckSidecarJson.String(DeckSidecarJson.Member(owner, key));

        internal static double Num(object v)
        {
            Assert.That(DeckSidecarJson.TryDouble(v, out double d), Is.True, $"expected a number, found {v ?? "null"}");
            return d;
        }

        /// <summary>The today file's block for one piece: <c>{key, id, scene, sceneEntry, options, gameplay}</c>.</summary>
        internal static object TodayPiece(string key)
        {
            var hits = Items(Member(ReadJson(TodayPath), "pieces")).Where(p => Str(p, "key") == key).ToArray();
            Assert.That(hits.Length, Is.EqualTo(1), $"{TodayPath} lists {key} {hits.Length} times.");
            return hits[0];
        }

        /// <summary>Two parsed documents are the same data: members in any order, arrays in order.</summary>
        internal static bool JsonEquals(object a, object b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (a is Dictionary<string, object> oa && b is Dictionary<string, object> ob)
                return oa.Count == ob.Count && oa.All(kv => ob.TryGetValue(kv.Key, out object v) && JsonEquals(kv.Value, v));
            if (a is List<object> la && b is List<object> lb)
                return la.Count == lb.Count && la.Zip(lb, JsonEquals).All(same => same);
            return a.Equals(b);
        }

        /// <summary>A host with the kit loaded as its checker loads it: the three scripts, unmodified, in order.</summary>
        internal static IRigScriptHost KitHost()
        {
            IRigScriptHost host = RigScriptHostFactory.Create();
            try
            {
                foreach (string script in Scripts)
                    host.Execute(File.ReadAllText(Abs(Kit + script)));
                Assert.That(host.EvaluateString("StPetersSetPieces.VERSION"), Is.EqualTo("1.0.0"));
                return host;
            }
            catch
            {
                host.Dispose();
                throw;
            }
        }

        // ---- 1. the delivery ------------------------------------------------------------------------------

        [Test]
        public void TheKitIsTheFourteenDeliveredFilesAndNothingElse()
        {
            foreach (var (file, pinned) in Delivered)
            {
                string path = Abs(Kit + file);
                FileAssert.Exists(path, file);
                byte[] bytes = File.ReadAllBytes(path);
                string actual = file.EndsWith(".png", StringComparison.Ordinal)
                    ? Sha(bytes)
                    : Sha(Encoding.UTF8.GetBytes(Lf(Encoding.UTF8.GetString(bytes))));
                Assert.That(actual, Is.EqualTo(pinned), $"{Kit}{file} is not the delivered file.");
            }

            string kitRoot = Path.GetFullPath(Abs(Kit));
            string[] present = Directory.GetFiles(kitRoot, "*", SearchOption.AllDirectories)
                .Select(p => Path.GetFullPath(p).Substring(kitRoot.Length).Replace('\\', '/').TrimStart('/'))
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToArray();
            Assert.That(present, Is.EqualTo(Delivered.Select(d => d.File).OrderBy(p => p, StringComparer.Ordinal).ToArray()),
                "The kit folder holds only what was delivered. Generated data (the today file, the sheets, the " +
                "Defs) lives beside the sheets, never in the kit.");
        }

        [Test]
        public void EverySidecarParsesAndNamesOneOfTheEightPieces()
        {
            string[] files = Directory.GetFiles(Abs(Kit + "gameplay"), "*.gameplay.json", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();
            Assert.That(files, Is.EqualTo(Pieces.Select(p => p.Id + ".gameplay.json").OrderBy(n => n, StringComparer.Ordinal).ToArray()),
                "One sidecar per piece, named for its id.");

            foreach (var (key, id) in Pieces)
            {
                object g = ReadJson(SidecarFile(id));
                Assert.That(Str(g, "schema"), Is.EqualTo("hidden-harbours/st-peters-set-piece@1"), id);
                Assert.That(Str(g, "generatedBy"), Is.EqualTo("StPetersSetPieces 1.0.0"), id);
                Assert.That(Str(g, "id"), Is.EqualTo(id));
                Assert.That(Str(g, "key"), Is.EqualTo(key), id);
                Assert.That(Str(g, "defType"), Is.EqualTo("SetPieceDef"), id);
                Assert.That(Items(Member(g, "facings")).Count, Is.EqualTo(8), id);
                Assert.That(Items(Member(g, "cells")).Count, Is.EqualTo(8), id);
            }
        }

        [Test]
        public void TheBakersTablesAreTheKitsKeysAndTheCheckersOrder()
        {
            Assert.That(StPetersSetPieceKit.Pieces.Select(p => (p.Key, p.Value)).ToArray(), Is.EqualTo(Pieces));
            Assert.That(StPetersSetPieceKit.Scripts, Is.EqualTo(Scripts));
            Assert.That(File.ReadAllText(Abs(Kit + "checks/check-kit.cjs")),
                Does.Contain("['lib/weatherSky.js', 'lib/coastalPass.js', 'stPetersSetPieces.js']"),
                "The checker names its load order in one array; the baker installs the same three in that order.");

            using var host = KitHost();
            Assert.That(host.EvaluateString("StPetersSetPieces.KEYS.join(',')"),
                Is.EqualTo(string.Join(",", Pieces.Select(p => p.Key))));
            Assert.That(host.EvaluateString("StPetersSetPieces.KEYS.map(function (k) { return StPetersSetPieces.PIECES[k].id; }).join(',')"),
                Is.EqualTo(string.Join(",", Pieces.Select(p => p.Id))));
        }

        // ---- 2. the lib copies ----------------------------------------------------------------------------

        /// <summary>
        /// The kit carries its own copies of two shared files. Each must stay the canonical file, byte for
        /// byte after LF, or the kit draws with a light the rest of the coast does not.
        /// </summary>
        [TestCase("lib/coastalPass.js", "docs/art/rigs/village-return/houses-kit/Art/coastalPass.js")]
        [TestCase("lib/weatherSky.js", "docs/art/rigs/terrain/pass9/lib/weatherSky.js")]
        public void LibCopyIsItsCanonicalFileAfterLf(string copy, string canonical)
        {
            if (!File.Exists(Abs(canonical)))
                Assert.Fail($"{canonical} is missing. For coastalPass.js that means #898 (the houses kit's coastal " +
                            "pass) has not landed on this base yet: take main after it merges, and this guard then " +
                            "compares the copy.");
            Assert.That(ShaLf(Kit + copy), Is.EqualTo(ShaLf(canonical)),
                $"{Kit}{copy} is no longer {canonical} after LF. Re-take the canonical file into the kit (and re-run " +
                "the checker), or report the drift: never edit one copy alone.");
        }

        // ---- 3. no reader sees the kit's sidecars --------------------------------------------------------

        /// <summary>
        /// The seven readers of <c>*.gameplay.json</c> (intake charter §0) and the folder each reads, read from
        /// the reader itself: a private const by reflection, BoatInteriorDefShapeTests' inline path from its
        /// source. A reader that moves moves this guard's bar with it.
        /// </summary>
        static IEnumerable<(string Reader, string Folder)> ReaderFolders()
        {
            yield return ("DeckSidecarImporter", Const(typeof(DeckSidecarImporter), "SidecarFolder"));
            yield return ("BoatInteriorDefBuilder", Const(typeof(BoatInteriorDefBuilder), "GameplayFolder"));
            yield return ("BoatInteriorSheetBaker", Const(typeof(BoatInteriorSheetBaker), "GameplayFolder"));
            yield return ("DwellingRigFleet", Const(typeof(DwellingRigFleet), "SidecarFolder"));
            yield return ("VehicleRigFleet", Const(typeof(VehicleRigFleet), "SidecarFolder"));
            yield return ("DeckSidecarImportParityTests", Const(typeof(DeckSidecarImportParityTests), "SidecarFolder"));
            yield return ("BoatInteriorDefShapeTests", InlineFolder("Assets/Tests/EditMode/RigBaking/BoatInteriorDefShapeTests.cs"));
        }

        static string Const(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null,
                $"{type.Name}.{name} is gone. Find where that reader reads sidecars now, then update this guard.");
            object value = field.IsLiteral ? field.GetRawConstantValue() : field.GetValue(null);
            return ((string)value).Replace('\\', '/').TrimEnd('/');
        }

        static string InlineFolder(string source)
        {
            Match m = Regex.Match(File.ReadAllText(Abs(source)), @"string gameplay = Path\.Combine\(RepoRoot, ""([^""]+)""\)");
            Assert.That(m.Success, Is.True,
                $"{source} no longer names its sidecar folder inline. Find where it reads now, then update this guard.");
            return m.Groups[1].Value.TrimEnd('/');
        }

        [Test]
        public void NoSidecarReaderSeesTheKitsGameplayFolder()
        {
            string kitGameplay = Kit + "gameplay";
            var readers = ReaderFolders().ToArray();
            Assert.That(readers.Length, Is.EqualTo(7));

            foreach (var (reader, folder) in readers)
            {
                // Not the folder, and not under it: every reader enumerates its folder's top level, but a
                // kit nested in one would be a step from being read.
                Assert.That(kitGameplay == folder || kitGameplay.StartsWith(folder + "/", StringComparison.Ordinal),
                    Is.False, $"{reader} reads {folder}, which holds the kit's sidecars.");

                string dir = Abs(folder);
                if (!Directory.Exists(dir)) continue;
                string[] seen = Directory.GetFiles(dir, "*.gameplay.json").Select(Path.GetFileName).ToArray();
                Assert.That(seen.Intersect(Pieces.Select(p => p.Id + ".gameplay.json")), Is.Empty,
                    $"{reader}'s folder {folder} holds a set piece's sidecar.");
            }
        }

        /// <summary>
        /// The readers' folders keep the kit out only while every enumeration of sidecars stays at its
        /// folder's top level. A recursive one under <c>docs/art/rigs</c> would reach the kit's gameplay folder.
        /// </summary>
        [Test]
        public void NoCodeEnumeratesSidecarsRecursively()
        {
            string needle = "\"*.gameplay" + ".json\"";
            string self = nameof(StPetersSetPiecesIntakeTests) + ".cs";
            var recursive = new List<string>();
            foreach (string cs in Directory.GetFiles(Abs("Assets"), "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(cs) == self) continue;
                string text = File.ReadAllText(cs);
                if (!text.Contains(needle)) continue;

                string[] lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!lines[i].Contains(needle)) continue;
                    string statement = string.Join(" ", lines.Skip(i).Take(3));
                    if (statement.Contains("AllDirectories"))
                        recursive.Add($"{Path.GetFileName(cs)}:{i + 1}");
                }
            }
            Assert.That(recursive, Is.Empty,
                "A recursive sidecar enumeration would reach the St Peters kit's gameplay folder: " + string.Join(", ", recursive));
        }

        /// <summary>
        /// The deck reader resolves a sidecar's rig by NAME anywhere under <c>docs/art/rigs</c>, and throws on two
        /// files of one name. The kit adds five script and page names to that tree; no deck sidecar may resolve
        /// to one of them.
        /// </summary>
        [Test]
        public void NoDeckSidecarResolvesItsRigIntoTheKit()
        {
            string deckFolder = Const(typeof(DeckSidecarImporter), "SidecarFolder");
            string kitRoot = Path.GetFullPath(Abs(Kit)).Replace('\\', '/');
            int resolved = 0;
            foreach (string file in Directory.GetFiles(Abs(deckFolder), "*.gameplay.json"))
            {
                string rigFile = DeckSidecarReader.ResolveRigFileName(Path.GetFileName(file), File.ReadAllText(file));
                string rigPath = DeckSidecarReader.ResolveRigPath(RigCatalog.RepoRoot, rigFile);
                if (rigPath == null) continue;
                resolved++;
                Assert.That(Path.GetFullPath(rigPath).Replace('\\', '/'), Does.Not.StartWith(kitRoot),
                    $"{Path.GetFileName(file)} resolves its rig ({rigFile}) into the St Peters kit.");
            }
            Assert.That(resolved, Is.GreaterThan(0), $"no deck sidecar in {deckFolder} resolved a rig at all.");
        }

        // ---- 4. the kit, run --------------------------------------------------------------------------------

        [Test]
        public void TheKitStillWritesEverySidecarByteForByte()
        {
            using var host = KitHost();
            foreach (var (key, id) in Pieces)
            {
                string want = Lf(File.ReadAllText(Abs(SidecarFile(id))));
                string got = host.EvaluateString($"JSON.stringify(StPetersSetPieces.gameplay('{key}'), null, 1) + '\\n'");
                Assert.That(got, Is.EqualTo(want), $"{id}: the kit no longer writes the sidecar it was delivered with.");
            }
        }

        [Test]
        public void TheTodayGameplayIsTheKitAtTheTodayOptions()
        {
            string today = File.ReadAllText(Abs(TodayPath));
            using var host = KitHost();
            host.Execute("globalThis.__t = " + today + ";");
            Assert.That((int)host.EvaluateNumber("__t.pieces.length"), Is.EqualTo(Pieces.Length));
            for (int i = 0; i < Pieces.Length; i++)
            {
                Assert.That(host.EvaluateString($"__t.pieces[{i}].key"), Is.EqualTo(Pieces[i].Key));
                Assert.That(host.EvaluateString($"__t.pieces[{i}].id"), Is.EqualTo(Pieces[i].Id));
                Assert.That(host.EvaluateBool(
                        $"JSON.stringify(StPetersSetPieces.gameplay(__t.pieces[{i}].key, Object.assign({{}}, __t.pieces[{i}].options))) " +
                        $"=== JSON.stringify(__t.pieces[{i}].gameplay)"),
                    Is.True, $"{Pieces[i].Key}: the kit at the today options no longer writes the today file's gameplay block.");
            }
        }

        /// <summary>
        /// The key scenes move two pieces off the kit's defaults: the landing sets the tide board's foot at
        /// −1.97 (its sidecar's is −1.0) and the cannery lays the trolley line along its own path. Only the
        /// fields a placement can move may differ from the delivered sidecars, and only on those two.
        /// </summary>
        [Test]
        public void TodayDiffersFromTheSidecarsOnlyWhereTheScenesMovedAPiece()
        {
            string[] movable = { "bounds", "cells", "anchors", "options" };
            var differs = new List<string>();
            foreach (var (key, id) in Pieces)
            {
                var today = DeckSidecarJson.AsObject(Member(TodayPiece(key), "gameplay"));
                var file = DeckSidecarJson.AsObject(ReadJson(SidecarFile(id)));
                Assert.That(today.Keys.OrderBy(k => k, StringComparer.Ordinal),
                    Is.EqualTo(file.Keys.OrderBy(k => k, StringComparer.Ordinal)), $"{key}: the two blocks' members.");
                foreach (string member in file.Keys)
                {
                    if (JsonEquals(file[member], today[member])) continue;
                    Assert.That(movable, Does.Contain(member), $"{key}.{member} differs from the delivered sidecar.");
                    differs.Add($"{key}.{member}");
                }
            }
            Assert.That(differs, Is.EquivalentTo(new[]
            {
                "slipTideBoard.bounds", "slipTideBoard.cells", "slipTideBoard.anchors", "slipTideBoard.options",
                "trolleyLine.bounds", "trolleyLine.cells", "trolleyLine.anchors", "trolleyLine.options",
            }));
        }

        /// <summary>
        /// The today file is transcribed from the two key scenes. It is pinned, it names the kit bytes it was
        /// made against, it bakes in daylight with no lamp, and it leaves the restored cannery out: no
        /// restored cell, no smoke plume, no purchase hook lands here.
        /// </summary>
        [Test]
        public void TheTodayFileIsPinnedAndLeavesTheRestoredCanneryOut()
        {
            Assert.That(ShaLf(TodayPath), Is.EqualTo(TodaySha256Lf),
                "The today options are transcribed from the key scenes; change them deliberately, re-bake, then re-pin.");

            object today = ReadJson(TodayPath);
            Assert.That(Str(today, "schema"), Is.EqualTo("hidden-harbours/st-peters-set-pieces-today@1"));
            object kit = Member(today, "kit");
            Assert.That(Str(kit, "folder"), Is.EqualTo(Kit.TrimEnd('/')));
            Assert.That(Str(kit, "global"), Is.EqualTo("StPetersSetPieces"));
            Assert.That(Str(kit, "version"), Is.EqualTo("1.0.0"));
            var scripts = Items(Member(kit, "scripts"));
            Assert.That(scripts.Count, Is.EqualTo(Scripts.Length));
            for (int i = 0; i < Scripts.Length; i++)
            {
                Assert.That(Str(scripts[i], "path"), Is.EqualTo(Scripts[i]));
                Assert.That(Str(scripts[i], "sha256Lf"), Is.EqualTo(ShaLf(Kit + Scripts[i])),
                    $"the today file was made against another {Scripts[i]}.");
            }

            object bake = Member(today, "bake");
            Assert.That(Num(Member(Member(bake, "sky"), "time")), Is.EqualTo(14));
            Assert.That(Num(Member(Member(bake, "sky"), "cloud")), Is.EqualTo(0.1));
            Assert.That(Num(Member(Member(bake, "sky"), "wind")), Is.EqualTo(0.3));
            Assert.That(Num(Member(bake, "lit")), Is.EqualTo(0), "No piece emits in the bake.");
            Assert.That(Member(bake, "outline"), Is.EqualTo(false));
            Assert.That(Num(Member(bake, "facings")), Is.EqualTo(8));
            Assert.That(Num(Member(bake, "importSizeCap")), Is.EqualTo(2048));

            Assert.That(Items(Member(today, "leftOut")).Select(x => Str(x, "id")), Is.EquivalentTo(new[]
            {
                "structure.stp_cannery_boiler_stack_restored", "structure.stp_cannery_trolley_line_restored",
                "prop.stp_cannery_trolley_restored", "prop.stp_cannery_conveyor_restored",
            }));
            foreach (object left in Items(Member(today, "leftOut")))
                Assert.That(Str(Member(left, "options"), "decay"), Is.EqualTo("restored"), Str(left, "id"));

            var pieces = Items(Member(today, "pieces"));
            Assert.That(pieces.Select(p => Str(p, "id")), Is.EqualTo(Pieces.Select(p => p.Id)));
            foreach (object p in pieces)
            {
                string id = Str(p, "id");
                Assert.That(Str(Member(p, "options"), "decay"), Is.Not.EqualTo("restored"), id);
                Assert.That(Str(Member(Member(p, "gameplay"), "options"), "decay"), Is.Not.EqualTo("restored"), id);
                Assert.That(Str(p, "scene"), Is.EqualTo(Str(Member(p, "gameplay"), "scene")), id);
            }
            Assert.That(Items(Member(today, "scenes")).Select(s => Str(s, "id")),
                Is.EquivalentTo(pieces.Select(p => Str(p, "scene")).Distinct()));
        }

        [Test]
        public void FramesTurnClockwiseOverTheKitsCounterclockwiseDirs()
        {
            int[] rigDirOfFrame = { 0, 7, 6, 5, 4, 3, 2, 1 };
            var facings = Items(Member(ReadJson(SidecarFile(Pieces[0].Id)), "facings"));
            for (int k = 0; k < 8; k++)
            {
                int dir = rigDirOfFrame[k];
                Assert.That(SetPieceDef.RigDirForFrame(k), Is.EqualTo(dir), $"SetPieceDef, frame {k}");
                Assert.That(SetPieceDef.FrameForRigDir(dir), Is.EqualTo(k), $"SetPieceDef, dir {dir}");
                Assert.That(StPetersSetPieceKit.RigDirForFrame(k), Is.EqualTo(dir), $"the baker, frame {k}");
                Assert.That(Num(Member(facings[dir], "dir")), Is.EqualTo(dir));
                Assert.That(Num(Member(facings[dir], "bearingDeg")), Is.EqualTo(45.0 * k),
                    $"frame {k} shows rig dir {dir}: its show face must point {45 * k}°, one clockwise step a frame.");
            }
        }
    }
}
