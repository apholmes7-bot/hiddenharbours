using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    public class NmcSetPiecesIntakeTests
    {
        const string Kit = "docs/art/rigs/nmc-set-pieces-kit/";
        const string Today = "Assets/_Project/Art/Sprites/NineMileCreek/SetPieces/nmcSetPieces.today.json";
        const string Sheets = "Assets/_Project/Art/Sprites/NineMileCreek/SetPieces";
        static readonly (string Key, string Id)[] Pieces =
        {
            ("officeTrailer", "structure.nmc_wharf_office"), ("wharfCrane", "structure.nmc_wharf_crane"),
            ("harbourLight", "structure.nmc_harbour_light"), ("restaurant", "structure.nmc_restaurant"),
            ("fishMarket", "structure.nmc_fish_market"), ("landingPad", "prop.nmc_crane_pad"),
        };
        static readonly string[] Scripts = { "lib/weatherSky.js", "lib/coastalPass.js", "nmcSetPieces.js" };
        static readonly (string File, string Hash)[] Delivered =
        {
            ("gameplay/barStake.gameplay.json", "54293bfa03b57d28208cf5ac34eb8b4c148e990745a8725ab4e2bc0c138d2947"),
            ("gameplay/bench.gameplay.json", "9cea35d46b7e72f8c55ba9ecf0817d7c77baf5739141aa602267c7867844eb00"),
            ("gameplay/boardwalk.gameplay.json", "3102ea61022de3aa0d06be32b3649bded63b04c7cf038b5d0b0b3b1d636f4616"),
            ("gameplay/boardwalkLamp.gameplay.json", "b293092b135cd6d16bd305fd9bea5659a8cd16054eb0053493406179a2c5c717"),
            ("gameplay/campTent.gameplay.json", "0cd739cf8d443093681e2b5be54746505ee5abee2b0f9a316914193364c5256d"),
            ("gameplay/cascade.gameplay.json", "003d49193c7cf3f8bd664c6952abf6e36ef0e42a205e8c36a6b141ac7956731c"),
            ("gameplay/chandlery.gameplay.json", "aecfa25158130f3c76113a9ead83a5d842d2e388cf3816bfa7a18dd613457889"),
            ("gameplay/comfortStation.gameplay.json", "c05353807647d93d1877ee0fa5c0a9e2e9b970a785ca33666c4cc1b1bded197b"),
            ("gameplay/creekDock.gameplay.json", "f215a81646c56cb3306ecf775dceb31bb7e0f371858f16b447ace6a139eb8cef"),
            ("gameplay/crossingBoard.gameplay.json", "e8cd568bdd8ea1e489e769cf9bf15a2a835c3f3cbcac6d720a1ec1de9dec0505"),
            ("gameplay/fallsDeck.gameplay.json", "917b8446261495db5397aeebf584a94d0b969df746b1a79a5efb1e89ad60f747"),
            ("gameplay/fireRing.gameplay.json", "996c245da3188989fbb0dd278dbc4bedf7fdeb9f9e865c7dc70855318cdf095b"),
            ("gameplay/guardRail.gameplay.json", "735ed09e5662e4c2c3db2c1271852bc4f12a49c7b5ccd013057bc640b9f7efa6"),
            ("gameplay/gullySteps.gameplay.json", "18e88d96a4d2f81788db58131637c2d5018e4fcb6659464ab2e08345cbb65856"),
            ("gameplay/lifeRingPost.gameplay.json", "761f0d72f9ab64a63bbd00aed7b359a45e3b4655fe716ac40ca89b49a76db562"),
            ("gameplay/picnicTable.gameplay.json", "4931956b2c988e78856ca721d8b0708cbacc3881c6ca8e650b50409e94e8fbac"),
            ("gameplay/riverDeck.gameplay.json", "c2bffba15e1bdedc72aadf02cbbf1b686bb11aeed1f21b3bd3d8a85bc0c76ae9"),
            ("gameplay/riverWall.gameplay.json", "dbd3a1e0842dde7516865ae40204807439129854b5db719337e3adf04507e3ac"),
            ("gameplay/routeBridge.gameplay.json", "c8682435e645d5bf287266e3079a4a513d2f8f84e9d83e13e14638cbf59a97bc"),
            ("gameplay/signBoard.gameplay.json", "5770bb419e7703fa44b135102ffebd0df36087cc84dee3f0f6dbda4c5c8db1fc"),
            ("gameplay/siteLights.gameplay.json", "29afcb1a5fc5c72ae591b7fba6457334813e9967b3417dbac3ffb6a776e81dcd"),
            ("gameplay/sitePost.gameplay.json", "54c5dc635ab345cddec3c03f2c3be877c00021bffab0139f7046c9d32bfe8a41"),
            ("gameplay/wallStairs.gameplay.json", "b787b19bb5a05a6f60a9895ea8c164ed21675bc50176b6e74ad3245b02ec3bbe"),
            ("nmcSetPieces.js", "9f64906237501c7bdf0bcf0aa402f9443d2e2299fd8c97da0b72f68f62f7d966"),
        };
        // Historical stamps are pinned independently of the kit output. No other bytes may differ.
        static readonly Dictionary<string, string> Historical = new Dictionary<string, string>
        {
            { "barStake", "NmcSetPieces 1.3.0" },
            { "bench", "NmcSetPieces 1.3.0" },
            { "boardwalk", "NmcSetPieces 1.2.0" },
            { "boardwalkLamp", "NmcSetPieces 1.2.0" },
            { "campTent", "NmcSetPieces 1.3.0" },
            { "cascade", "NmcSetPieces 1.3.0" },
            { "chandlery", "NmcSetPieces 1.2.0" },
            { "comfortStation", "NmcSetPieces 1.3.0" },
            { "crossingBoard", "NmcSetPieces 1.3.0" },
            { "fallsDeck", "NmcSetPieces 1.3.0" },
            { "fireRing", "NmcSetPieces 1.3.0" },
            { "guardRail", "NmcSetPieces 1.3.0" },
            { "lifeRingPost", "NmcSetPieces 1.3.0" },
            { "picnicTable", "NmcSetPieces 1.3.0" },
            { "riverDeck", "NmcSetPieces 1.2.0" },
            { "riverWall", "NmcSetPieces 1.2.0" },
            { "routeBridge", "NmcSetPieces 1.2.0" },
            { "signBoard", "NmcSetPieces 1.3.0" },
            { "siteLights", "NmcSetPieces 1.3.0" },
            { "sitePost", "NmcSetPieces 1.3.0" },
            { "wallStairs", "NmcSetPieces 1.2.0" },
        };
        static readonly string[] LfOnly = { "creekDock", "gullySteps" };
        static string Abs(string p) => Path.Combine(RigCatalog.RepoRoot, p);
        static object Read(string p) => DeckSidecarJson.Parse(File.ReadAllText(Abs(p)));
        static object M(object o, string k) => DeckSidecarJson.Member(o, k);
        static string S(object o, string k) => DeckSidecarJson.String(M(o, k));
        static List<object> A(object v) => DeckSidecarJson.AsArray(v) ?? new List<object>();
        static string Lf(string s) => s.Replace("\r\n", "\n");
        static string FinalLf(string s) => s.EndsWith("\n", StringComparison.Ordinal) ? s.Substring(0, s.Length - 1) : s;
        static string HashLf(string p) => StPetersSetPieceKit.Sha256Hex(Encoding.UTF8.GetBytes(Lf(File.ReadAllText(Abs(p)))));
        static object Piece(string key) => A(M(Read(Today), "pieces")).Single(p => S(p, "key") == key);
        static SetPieceDef Def(string key) => AssetDatabase.LoadAssetAtPath<SetPieceDef>($"Assets/_Project/Data/SetPieces/SetPiece_{key}.asset");
        static IRigScriptHost Host()
        {
            var host = RigScriptHostFactory.Create();
            NmcSetPieceKit.Install(host);
            host.Execute("globalThis.__t = " + File.ReadAllText(Abs(Today)) + ";");
            return host;
        }
        static Vector3 V3(object value)
        {
            var a = A(value);
            Assert.That(a.Count, Is.EqualTo(3));
            return new Vector3((float)(double)a[0], (float)(double)a[1], (float)(double)a[2]);
        }

        [Test]
        public void TheKitIsTheDeliveredFilesAndOnlyItsTwoLibrariesAndTwoChecks()
        {
            foreach (var pin in Delivered) Assert.That(HashLf(Kit + pin.File), Is.EqualTo(pin.Hash), pin.File);
            var extras = new[] { "lib/weatherSky.js", "lib/coastalPass.js", "checks/check-kit.cjs", "checks/write-today.cjs" };
            string prefix = Path.GetFullPath(Abs(Kit));
            var files = Directory.GetFiles(prefix, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetFullPath(f).Substring(prefix.Length).TrimStart('\\', '/').Replace('\\', '/'));
            Assert.That(files, Is.EquivalentTo(Delivered.Select(p => p.File).Concat(extras)));
        }

        [Test]
        public void EverySidecarParsesAndNamesOneOfTheTwentyThreeLaterPieces()
        {
            var files = Directory.GetFiles(Abs(Kit + "gameplay"), "*.gameplay.json", SearchOption.TopDirectoryOnly);
            Assert.That(files.Length, Is.EqualTo(23));
            Assert.That(Historical.Keys.Concat(LfOnly), Is.EquivalentTo(files.Select(f => S(DeckSidecarJson.Parse(File.ReadAllText(f)), "key"))));
            foreach (string file in files)
            {
                var g = DeckSidecarJson.Parse(File.ReadAllText(file));
                string key = S(g, "key");
                Assert.That(Path.GetFileName(file), Is.EqualTo(key + ".gameplay.json"));
                Assert.That(S(g, "schema"), Is.EqualTo("hidden-harbours/nmc-set-piece@1"));
                Assert.That(S(g, "defType"), Is.EqualTo("SetPieceDef"));
                Assert.That(S(g, "id"), Is.Not.Empty);
                Assert.That(Pieces.Select(p => p.Key), Does.Not.Contain(key));
                Assert.That(A(M(g, "facings")).Count, Is.EqualTo(8));
                Assert.That(A(M(g, "cells")).Count, Is.EqualTo(8));
            }
        }

        [Test]
        public void TheBakersTablesAreTheSixKitKeysInTheCheckersOrder()
        {
            Assert.That(NmcSetPieceKit.Pieces.Select(p => (p.Key, p.Value)), Is.EqualTo(Pieces));
            Assert.That(NmcSetPieceKit.Scripts, Is.EqualTo(Scripts));
            Assert.That(File.ReadAllText(Abs(Kit + "checks/check-kit.cjs")), Does.Contain("['lib/weatherSky.js', 'lib/coastalPass.js', 'nmcSetPieces.js']"));
            using var host = Host();
            Assert.That(host.EvaluateNumber("NmcSetPieces.KEYS.length"), Is.EqualTo(29));
            Assert.That(host.EvaluateString("NmcSetPieces.KEYS.filter(function(k){return __t.pieces.some(function(p){return p.key===k;});}).join(',')"), Is.EqualTo(string.Join(",", Pieces.Select(p => p.Key))));
            Assert.That(NmcSetPieceKit.Pieces.Select(p => p.Key).Intersect(StPetersSetPieceKit.Pieces.Select(p => p.Key)), Is.Empty);
        }

        [TestCase("weatherSky.js", "docs/art/rigs/terrain/pass9/lib/weatherSky.js")]
        [TestCase("coastalPass.js", "docs/art/rigs/village-return/houses-kit/Art/coastalPass.js")]
        public void LibCopyIsItsCanonicalFileAfterLf(string file, string canonical)
            => Assert.That(HashLf(Kit + "lib/" + file), Is.EqualTo(HashLf(canonical)));

        [Test]
        public void NoSidecarReaderSeesTheKitsGameplayFolder()
        {
            var readers = new[] { (typeof(DeckSidecarImporter), "SidecarFolder"), (typeof(BoatInteriorDefBuilder), "GameplayFolder"),
                (typeof(BoatInteriorSheetBaker), "GameplayFolder"), (typeof(DwellingRigFleet), "SidecarFolder"), (typeof(VehicleRigFleet), "SidecarFolder") };
            foreach (var reader in readers)
            {
                var f = reader.Item1.GetField(reader.Item2, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                string folder = ((string)(f.IsLiteral ? f.GetRawConstantValue() : f.GetValue(null))).Replace('\\', '/').TrimEnd('/');
                Assert.That((Kit + "gameplay").StartsWith(folder + "/", StringComparison.Ordinal) || Kit + "gameplay" == folder, Is.False, reader.Item1.Name);
                if (Directory.Exists(Abs(folder))) Assert.That(Directory.GetFiles(Abs(folder), "*.gameplay.json").Select(Path.GetFileName).Intersect(Delivered.Select(p => Path.GetFileName(p.File))), Is.Empty);
            }
            foreach (string source in new[] { "DeckSidecarImportParityTests", "BoatInteriorDefShapeTests" })
            {
                string file = Abs("Assets/Tests/EditMode/RigBaking/" + source + ".cs");
                Assert.That(File.ReadAllText(file), Does.Not.Contain(Kit.TrimEnd('/')));
            }
        }

        [Test]
        public void NoCodeEnumeratesSidecarsRecursively()
        {
            string needle = "\"*.gameplay" + ".json\"";
            foreach (string file in Directory.GetFiles(Abs("Assets"), "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) == nameof(NmcSetPiecesIntakeTests) + ".cs") continue;
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++) if (lines[i].Contains(needle))
                    Assert.That(string.Join(" ", lines.Skip(i).Take(3)), Does.Not.Contain("AllDirectories"), file);
            }
        }

        [Test]
        public void NoDeckSidecarResolvesItsRigIntoTheKit()
        {
            var f = typeof(DeckSidecarImporter).GetField("SidecarFolder", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            string folder = (string)f.GetRawConstantValue();
            int resolved = 0;
            foreach (string file in Directory.GetFiles(Abs(folder), "*.gameplay.json"))
            {
                string rig = DeckSidecarReader.ResolveRigFileName(Path.GetFileName(file), File.ReadAllText(file));
                string found = DeckSidecarReader.ResolveRigPath(RigCatalog.RepoRoot, rig);
                if (found == null) continue;
                resolved++;
                Assert.That(Path.GetFullPath(found).Replace('\\', '/'), Does.Not.StartWith(Path.GetFullPath(Abs(Kit)).Replace('\\', '/')));
            }
            Assert.That(resolved, Is.GreaterThan(0));
        }

        [Test]
        public void SidecarsEqualKitOutputWithOnlyThePinnedStampsAndFinalLfExceptions()
        {
            Assert.That(Historical.Values.Count(v => v == "NmcSetPieces 1.2.0"), Is.EqualTo(7));
            Assert.That(Historical.Values.Count(v => v == "NmcSetPieces 1.3.0"), Is.EqualTo(14));
            using var host = Host();
            foreach (string file in Directory.GetFiles(Abs(Kit + "gameplay"), "*.gameplay.json"))
            {
                string delivered = FinalLf(Lf(File.ReadAllText(file))), key = S(DeckSidecarJson.Parse(delivered), "key");
                string actual = host.EvaluateString($"JSON.stringify(NmcSetPieces.gameplay('{key}'), null, 1)");
                string stamp = Historical.TryGetValue(key, out string old) ? old : "NmcSetPieces 1.4.0";
                Assert.That(S(DeckSidecarJson.Parse(delivered), "generatedBy"), Is.EqualTo(stamp), key);
                if (Historical.ContainsKey(key))
                {
                    Assert.That(delivered, Is.Not.EqualTo(actual), key + " must retain the pinned historical difference");
                    delivered = delivered.Replace(" \"generatedBy\": \"" + stamp + "\",", " \"generatedBy\": \"NmcSetPieces 1.4.0\",");
                }
                else Assert.That(LfOnly, Does.Contain(key));
                Assert.That(delivered, Is.EqualTo(actual), key + ": no other byte may differ");
            }
        }

        [Test]
        public void TodayAndEveryVariantAreTheKitAtTheSceneOptions()
        {
            object today = Read(Today);
            Assert.That(S(today, "schema"), Is.EqualTo("hidden-harbours/nmc-set-pieces-today@1"));
            NmcSetPieceKit.CheckToday(today);
            var scene = A(M(today, "scenes")).Single();
            Assert.That(S(scene, "sha256"), Is.EqualTo("81307f997035dbbec420c65326337fb439dc82afe9049c0b96de69f856fec26d"));
            Assert.That((double)M(scene, "bytes"), Is.EqualTo(78926));
            Assert.That(A(M(today, "leftOut")), Is.Empty);
            using var host = Host();
            Assert.That(host.EvaluateBool("__t.pieces.every(function(p){return JSON.stringify(NmcSetPieces.gameplay(p.key,p.options))===JSON.stringify(p.gameplay)&&p.variants.every(function(v){return JSON.stringify(NmcSetPieces.gameplay(p.key,v.options))===JSON.stringify(v.gameplay);});})"), Is.True);
            Assert.That(host.EvaluateString("__t.pieces.map(function(p){return p.sceneEntry;}).join(',')"), Is.EqualTo("9,6,12,11,10,8"));
            Assert.That(host.EvaluateString("JSON.stringify(__t.bake)"), Is.EqualTo("{\"sky\":{\"time\":14,\"cloud\":0.1,\"wind\":0.3},\"lit\":0,\"outline\":false,\"facings\":8,\"importSizeCap\":4096}"));
        }

        [TestCase("officeTrailer", "counter,door,interact")]
        [TestCase("fishMarket", "counter,door,iceChest")]
        [TestCase("restaurant", "door,takeout,deckDoor")]
        [TestCase("landingPad", "seat")]
        [TestCase("wharfCrane", "hook,tip,loadBase,operator,column")]
        public void RequiredPointAnchorsAreTheKitsPoints(string key, string names)
        {
            var def = Def(key);
            Assert.That(def, Is.Not.Null);
            object anchors = M(M(Piece(key), "gameplay"), "anchors");
            foreach (string name in names.Split(','))
            {
                Assert.That(def.TryGetAnchor(name, out Vector3 at), Is.True, key + "." + name);
                Assert.That((at - V3(M(anchors, name))).magnitude, Is.LessThan(1e-5f));
            }
        }

        [Test]
        public void MarketBoardBindsTheThreeKitLinesToTheLiveMarket()
        {
            var word = Def("fishMarket").Words.Single(w => w.Id == "words.nmc_market_board");
            Assert.That(word.Bind, Is.EqualTo("market.today"));
            Assert.That(word.Format, Is.EqualTo(new[] { "TODAY", "{species1} {price1}", "{species2} {price2}" }));
        }

        [Test]
        public void OfficeRetainsTheHarbourmasterVendorAndRole()
        {
            var office = Def("officeTrailer");
            Assert.That(office.Id, Is.EqualTo("structure.nmc_wharf_office"));
            Assert.That(office.HasVendor, Is.True);
            Assert.That(office.Vendor.Seller, Is.EqualTo("seller.nmc_harbourmaster"));
            Assert.That(office.Vendor.At, Is.EqualTo(new Vector3(2.8f, 0.3f, 1.5f)));
            Assert.That(office.State, Is.Empty);
            Assert.That(S(M(DeckSidecarJson.Parse(office.GameplayJson), "anchors"), "role"), Is.EqualTo("harbourmaster"));
        }

        [Test]
        public void CraneKeepsWorkingAndStowedInOneSheet()
        {
            var crane = Def("wharfCrane");
            Assert.That(crane.State, Is.EqualTo("working"));
            Assert.That(crane.Variants.Length, Is.EqualTo(1));
            var stowed = crane.Variants[0];
            Assert.That(stowed.Name, Is.EqualTo("stowed"));
            Assert.That(S(DeckSidecarJson.Parse(stowed.BakedOptionsJson), "state"), Is.EqualTo("stowed"));
            Assert.That(stowed.Anchors.Select(a => a.Name), Is.SupersetOf(new[] { "hook", "tip", "operator", "column" }));
            Assert.That(stowed.Anchors.Select(a => a.Name), Does.Not.Contain("loadBase"));
            Assert.That(stowed.Frames.Select(f => f.RigDir), Is.EqualTo(new[] {0,7,6,5,4,3,2,1}));
        }

        [Test]
        public void HarbourLightKeepsGreenWestAndRedEastInOneSheet()
        {
            var light = Def("harbourLight");
            Assert.That(light.State, Is.Empty);
            Assert.That(S(DeckSidecarJson.Parse(light.BakedOptionsJson), "colour"), Is.EqualTo("green"));
            Assert.That(light.Variants.Single().Name, Is.EqualTo("colour=red"));
            Assert.That(S(DeckSidecarJson.Parse(light.Variants[0].BakedOptionsJson), "colour"), Is.EqualTo("red"));
        }

        [Test]
        public void EveryDefAndVariantRetainsTheWholeCanonicalKitBlock()
        {
            using var host = Host();
            for (int i = 0; i < Pieces.Length; i++)
            {
                var def = Def(Pieces[i].Key);
                Assert.That(def, Is.Not.Null);
                Assert.That(def.Id, Is.EqualTo(Pieces[i].Id));
                Assert.That(def.GameplayJson, Is.EqualTo(host.EvaluateString($"JSON.stringify(__t.pieces[{i}].gameplay)")));
                Assert.That(def.Frames.Length, Is.EqualTo(8));
                Assert.That(def.Frames.All(f => f.Sprite != null), Is.True);
                for (int j = 0; j < def.Variants.Length; j++)
                    Assert.That(def.Variants[j].GameplayJson, Is.EqualTo(host.EvaluateString($"JSON.stringify(__t.pieces[{i}].variants[{j}].gameplay)")));
            }
        }

        [Test]
        public void StPetersReadsTheSameDefaultsAfterTheAppends()
        {
            foreach (string key in new[] { "slipTideBoard", "harbourVane", "boilerStack", "trolleyLine", "trolley", "conveyor", "fallenSign", "doorNotice" })
            {
                var def = Def(key);
                Assert.That(def.State, Is.Empty, key);
                Assert.That(def.Variants, Is.Empty, key);
                Assert.That(def.HasVendor, Is.False, key);
                foreach (var w in def.Words) { Assert.That(w.Bind, Is.Empty); Assert.That(w.Format, Is.Empty); }
            }
            Assert.That(StPetersSetPieceKit.ExtraGameplayMembers, Is.Empty);
            Assert.That(StPetersSetPieceKit.ExtraAnchorMembers, Is.Empty);
        }

        [Test]
        public void StPetersStillRefusesAnUnknownGameplayMember()
        {
            var block = DeckSidecarJson.AsObject(M(A(M(Read("Assets/_Project/Art/Sprites/StPeters/SetPieces/stPetersSetPieces.today.json"), "pieces"))[0], "gameplay"));
            block.Add("crane", new Dictionary<string, object>());
            Assert.That(SetPieceDefBuilder.ValidateGameplayMembers(block, StPetersSetPieceKit.Definition).Single(), Does.Contain("carries 'crane'"));
        }

        [Test]
        public void NoWharfPieceIsPlacedInAnySceneOrKeyScene()
        {
            foreach (string scene in Directory.GetFiles(Abs("Assets/_Project/Scenes"), "*.unity", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(scene);
                foreach (var piece in Pieces)
                {
                    Assert.That(text, Does.Not.Contain(piece.Id), scene);
                    Assert.That(text, Does.Not.Contain(AssetDatabase.AssetPathToGUID($"Assets/_Project/Data/SetPieces/SetPiece_{piece.Key}.asset")), scene);
                }
            }
            foreach (string record in Directory.GetFiles(Abs("Assets/_Project/Data/KeyScenes"), "*.asset"))
            {
                string text = File.ReadAllText(record);
                if (Pieces.Any(p => text.Contains(p.Id))) Assert.That(text, Does.Contain("  Placed: 0"), record);
            }
        }

        [Test]
        public void TheKitTodayAndContractShareOneCapAndSheetsImportAtTheirBakedSize()
        {
            Assert.That(NmcSetPieceKit.ImportSizeCap, Is.EqualTo(4096));
            Assert.That(NmcSetPieceKit.Facings, Is.EqualTo(8));
            Assert.That(NmcSetPieceKit.Definition.ImportSizeCap, Is.EqualTo(NmcSetPieceKit.ImportSizeCap));
            Assert.That(NmcSetPieceKit.Definition.Facings, Is.EqualTo(NmcSetPieceKit.Facings));
            Assert.That((double)M(M(Read(Today), "bake"), "importSizeCap"), Is.EqualTo(NmcSetPieceKit.ImportSizeCap));
            var contract = JsonUtility.FromJson<SetPieceSheetSlicer.Contract>(File.ReadAllText(Abs(Sheets + "/nmcSetPieces.contract.json")));
            Assert.That(contract.importSizeCap, Is.EqualTo(NmcSetPieceKit.ImportSizeCap));
            Assert.That(contract.facings, Is.EqualTo(NmcSetPieceKit.Facings));
            foreach (var sheet in contract.pieces)
            {
                Assert.That(NavBuoyKit.PlanSheet(sheet.cellW, sheet.cellH, sheet.cells, contract.importSizeCap,
                    out int cols, out int rows, out int w, out int h), Is.True, sheet.key + " has no grid");
                Assert.That((sheet.cols, sheet.rows, sheet.sheetW, sheet.sheetH), Is.EqualTo((cols, rows, w, h)), sheet.key);
                string path = Sheets + "/SetPiece_" + sheet.key + ".png";
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(texture, Is.Not.Null, path);
                Assert.That((texture.width, texture.height), Is.EqualTo((sheet.sheetW, sheet.sheetH)), path);
                Assert.That(Math.Max(texture.width, texture.height), Is.LessThanOrEqualTo(contract.importSizeCap), path);
                Assert.That(importer.maxTextureSize, Is.GreaterThanOrEqualTo(Mathf.NextPowerOfTwo(Math.Max(sheet.sheetW, sheet.sheetH))), path);
                TestContext.WriteLine($"{sheet.key}: baked {sheet.sheetW}x{sheet.sheetH}; imported {texture.width}x{texture.height}; maxTextureSize {importer.maxTextureSize}; kit cap {contract.importSizeCap}");
            }
            Assert.That(contract.pieces.Single(p => p.key == "restaurant").cells, Is.EqualTo(8));
            Assert.That(StPetersSetPieceKit.ImportSizeCap, Is.EqualTo(2048));
            Assert.That(StPetersSetPieceKit.Definition.ImportSizeCap, Is.EqualTo(2048));
        }

        [Test]
        public void AllFacingAndVariantPixelsAreTheKitAtTheirOwnOptions()
        {
            var contract = JsonUtility.FromJson<SetPieceSheetSlicer.Contract>(File.ReadAllText(Abs(Sheets + "/nmcSetPieces.contract.json")));
            using var host = Host();
            host.Execute("globalThis.__sky=WeatherSky.at(__t.bake.sky);");
            for (int i = 0; i < Pieces.Length; i++)
            {
                var sheet = contract.pieces[i];
                var tex = new Texture2D(2,2,TextureFormat.RGBA32,false);
                try
                {
                    Assert.That(tex.LoadImage(File.ReadAllBytes(Abs(Sheets + "/SetPiece_" + sheet.key + ".png"))), Is.True);
                    Assert.That(Math.Max(tex.width,tex.height), Is.LessThanOrEqualTo(NmcSetPieceKit.ImportSizeCap));
                    var pixels=tex.GetPixels32();
                    foreach (var frame in sheet.frames)
                    {
                        int v=frame.cell/8-1;
                        host.Execute($"globalThis.__r=(function(){{var p=__t.pieces[{i}],v={v},o=v<0?p.options:p.variants[v].options;return NmcSetPieces.render(p.key,{frame.rigDir},Object.assign({{sky:__sky,lit:0,outline:false}},o));}})();");
                        int w=(int)host.EvaluateNumber("__r.w"),h=(int)host.EvaluateNumber("__r.h");
                        byte[] rgba=host.EvaluateBytes("__r.rgba");
                        int wrong=0;
                        for(int y=0;y<sheet.cellH;y++)for(int x=0;x<sheet.cellW;x++)
                        {
                            int sx=frame.cropX+x,sy=frame.cropY+y,s=(sy*w+sx)*4;
                            bool inside=sx>=0&&sx<w&&sy>=0&&sy<h;
                            byte a=inside?rgba[s+3]:(byte)0;
                            var got=pixels[(tex.height-1-(frame.cell/sheet.cols*sheet.cellH+y))*tex.width+frame.cell%sheet.cols*sheet.cellW+x];
                            if(a==0?got.a!=0:got.a!=a||got.r!=rgba[s]||got.g!=rgba[s+1]||got.b!=rgba[s+2])wrong++;
                        }
                        Assert.That(wrong, Is.Zero, sheet.key+" cell "+frame.cell);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(tex); }
            }
        }
    }
}
