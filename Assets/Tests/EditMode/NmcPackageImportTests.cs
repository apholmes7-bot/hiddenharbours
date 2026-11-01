using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using HiddenHarbours.App.Editor;
using Node = HiddenHarbours.App.Editor.KeySceneImport.YamlNode;

namespace HiddenHarbours.Tests.EditMode
{
    public class NmcPackageImportTests
    {
        const string Guid = "4c5d4ebc05de4cf6957872faaf3cb719";
        const string Wharf = "keyscene.nmc_wharf", Falls = "keyscene.nmc_the_falls";
        const string Asset = "KeyScene_nmc_wharf.asset";
        const string Piece = "{`id`:`prop.nmc_one`,`kit`:`nmcSetPieces`,`piece`:`bench`,`defType`:`SetPieceDef`,`at`:[1,2],`z`:3,`dir`:4}";
        static string J(string s) => s.Replace('`', '"');
        static byte[] B(string s) => Encoding.UTF8.GetBytes(J(s));
        static string Sha(byte[] b) { using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(b).Select(x => x.ToString("x2"))); }
        static Node Field(IEnumerable<KeyValuePair<string, Node>> fields, string key) => fields.Single(f => f.Key == key).Value;
        static List<KeyValuePair<string, Node>> Read(KeySceneImport.Result r) => KeySceneImport.ReadAsset(r.Assets[Asset], Asset);
        static List<Node> Rows(KeySceneImport.Result r) => Field(Read(r), "Pieces").Items;
        static string Option(Node row, string key) => row.Get("Options").Items.Single(o => o.Get("Key").Scalar == key).Get("Value").Scalar;

        sealed class Fixture
        {
            public readonly Dictionary<string, string> Files = new Dictionary<string, string>();
            public string Map = "{`schema`:`hidden-harbours/key-scene-id-map@1`,`renames`:[]}";
            public string Hosts = "{`schema`:`hidden-harbours/key-scene-hosts@1`,`hosts`:[]}";
            public string Retired = "{`schema`:`hidden-harbours/nmc-retired-ids@1`,`retired`:[]}";
            public Fixture(params string[] pieces)
            {
                if (pieces.Length == 0) pieces = new[] { Piece };
                Files["region.json"] = J("{`schema`:`hidden-harbours/nmc-one-scene@1`,`id`:`region.nmc`," +
                    "`ground`:{`package`:`g/`,`plan`:`plan.json`},`scenes`:[{`id`:`" + Wharf + "`,`file`:`wharf.json`}]," +
                    "`owners`:{" + string.Join(",", pieces.Select(p => KeySceneImport.Json.Parse(J(p), "fixture").Get("id").Text).Distinct()
                        .Select(id => "`" + id + "`:`" + Wharf + "`")) + "},`refs`:[]}");
                Files["g/plan.json"] = "{}";
                Files["wharf.json"] = J("{`schema`:`hidden-harbours/key-scene@1`,`id`:`" + Wharf + "`,`name`:`wharf`,`pieces`:[" + string.Join(",", pieces) + "]}");
            }
            public Func<string, byte[]> Reader()
            {
                var bytes = Files.ToDictionary(f => f.Key, f => B(f.Value));
                bytes["SHA256SUMS.txt"] = Encoding.UTF8.GetBytes(string.Join("\n", bytes.Select(f => Sha(f.Value) + "  " + f.Key)) + "\n");
                return p => bytes.TryGetValue(p, out var data) ? data : null;
            }
            public KeySceneImport.Result Import(IReadOnlyDictionary<string, string> existing = null) =>
                KeySceneImport.ImportNmc("synthetic", Reader(), existing, Guid, B(Map), B(Hosts), B(Retired));
        }

        [Test] public void BothSceneSchemas_AndTheRegionAlias_AreExplicit()
        {
            var f = new Fixture();
            Assert.AreEqual(KeySceneImport.NmcRegionId, Field(Read(f.Import()), "RegionId").Scalar);
            f.Files["wharf.json"] = f.Files["wharf.json"].Replace("key-scene@1", "keyscene@2");
            Assert.AreEqual("0", Field(Read(f.Import()), "Placed").Scalar);
            f.Files["wharf.json"] = f.Files["wharf.json"].Replace("keyscene@2", "keyscene@99");
            Assert.Throws<KeySceneImport.Refusal>(() => f.Import());
            f = new Fixture(); f.Files["region.json"] = f.Files["region.json"].Replace("nmc-one-scene@1", "nmc-one-scene@2");
            Assert.Throws<KeySceneImport.Refusal>(() => f.Import());
        }

        [Test] public void Refs_RequireTheirOwner_AndSpawnZero()
        {
            var f = new Fixture();
            f.Files["region.json"] = f.Files["region.json"].Replace(J("`file`:`wharf.json`}"), J("`file`:`wharf.json`},{`id`:`" + Falls + "`,`file`:`falls.json`}"))
                .Replace(J("`refs`:[]"), J("`refs`:[{`id`:`prop.nmc_one`,`owner`:`" + Wharf + "`,`in`:`" + Falls + "`}]"))
                .Replace(J("`owners`:{"), J("`owners`:{`prop.nmc_two`:`" + Falls + "`,"));
            f.Files["falls.json"] = J("{`schema`:`hidden-harbours/key-scene@1`,`id`:`" + Falls + "`,`name`:`falls`,`pieces`:[" +
                Piece.Replace("prop.nmc_one", "prop.nmc_two") + "," + Piece.Replace("`dir`:4", "`dir`:4,`ref`:`" + Wharf + " (board only)`") + "]}");
            var r = f.Import();
            var rows = Field(KeySceneImport.ReadAsset(r.Assets["KeyScene_nmc_the_falls.asset"], "falls"), "Pieces").Items;
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("prop.nmc_two", rows[0].Get("Id").Scalar);
            StringAssert.Contains("prop.nmc_one", Option(rows[0], "nmc.scene.refs"));
            f.Files["falls.json"] = f.Files["falls.json"].Replace(Wharf + " (board only)", "keyscene.nmc_missing");
            Assert.Throws<KeySceneImport.Refusal>(() => f.Import());
        }

        [Test] public void CranePoses_AreOneEntity_WithDisjointVariants()
        {
            string day = Piece.Replace("prop.nmc_one", "structure.nmc_wharf_crane").Replace("`dir`:4", "`dir`:4,`variants`:[`today`],`opts`:{`state`:`working`}");
            string night = day.Replace("today", "night").Replace("working", "stowed");
            var r = new Fixture(day, night).Import();
            Assert.AreEqual(1, Rows(r).Count);
            StringAssert.Contains("stowed", Option(Rows(r)[0], "nmc.variantPoses"));
            Assert.AreEqual(2, r.Tallies[Wharf].Owned);
            Assert.Throws<KeySceneImport.Refusal>(() => new Fixture(day, day).Import());
            Assert.Throws<KeySceneImport.Refusal>(() => new Fixture(day, night.Replace("nmcSetPieces", "NmcStores")).Import());
        }

        [Test] public void TheIdMap_RewritesNestedReferences_AndReservesOldIds()
        {
            var f = new Fixture(Piece.Replace("prop.nmc_one", "prop.nmc_oldBed").Replace("`dir`:4", "`dir`:4,`stations`:[{`host`:`prop.nmc_oldBed`}],`opts`:{`host`:`prop.nmc_oldBed`,`links`:[`prop.nmc_oldBed`]}"));
            f.Map = "{`schema`:`hidden-harbours/key-scene-id-map@1`,`renames`:[{`old`:`prop.nmc_oldBed`,`new`:`prop.nmc_bed_w`}] }";
            var r = f.Import();
            Assert.AreEqual("prop.nmc_bed_w", Rows(r)[0].Get("Id").Scalar);
            StringAssert.Contains("prop.nmc_bed_w", Option(Rows(r)[0], "nmc.stations"));
            Assert.AreEqual("prop.nmc_bed_w", Option(Rows(r)[0], "host"));
            Assert.AreEqual(J("[`prop.nmc_bed_w`]"), Option(Rows(r)[0], "links"));
            Assert.Contains("prop.nmc_oldBed", Field(Read(r), "RetiredIds").Items.Select(i => i.Scalar).ToArray());
        }

        [Test] public void RemovedAndReservedIds_AreNeverReused()
        {
            var first = new Fixture(Piece, Piece.Replace("prop.nmc_one", "prop.nmc_two")).Import();
            var second = new Fixture().Import(first.Assets);
            Assert.Throws<KeySceneImport.Refusal>(() => new Fixture(Piece, Piece.Replace("prop.nmc_one", "prop.nmc_two")).Import(second.Assets));
            var f = new Fixture(); f.Retired = "{`schema`:`hidden-harbours/nmc-retired-ids@1`,`retired`:[{`id`:`prop.nmc_one`,`reason`:`superseded`}]}";
            Assert.Throws<KeySceneImport.Refusal>(() => f.Import());
        }

        [Test] public void MissingHosts_AndChangedMountWords_Refuse()
        {
            var f = new Fixture(Piece.Replace("`dir`:4", "`dir`:4,`mount`:`on the roof`"));
            Assert.Throws<KeySceneImport.Refusal>(() => f.Import());
            f.Hosts = "{`schema`:`hidden-harbours/key-scene-hosts@1`,`hosts`:[{`piece`:`prop.nmc_one`,`mount`:`on the roof`,`standsOn`:`mount`,`host`:`structure.nmc_absent`,`anchor`:`roof`,`answer`:`fixture`}]}";
            Assert.Throws<KeySceneImport.Refusal>(() => f.Import());
            f.Files["g/plan.json"] = J("{`id`:`structure.nmc_absent`}");
            Assert.DoesNotThrow(() => f.Import());
            f.Files["wharf.json"] = f.Files["wharf.json"].Replace("on the roof", "on a roof");
            Assert.Throws<KeySceneImport.Refusal>(() => f.Import());
        }

        [Test] public void SupersededSources_AreNeverParsed_OrSelected()
        {
            var f = new Fixture();
            string[] paths = { "old/main_street.plaza/scene.json", "hh-nmc-wharf-return/scenes/keyscene.nmc_fuel_stop/scene.json",
                "old/record-2b/scene.json", "old/record-2c/scene.json" };
            foreach (string path in paths) f.Files[path] = "deliberately not JSON";
            Assert.DoesNotThrow(() => f.Import());
            foreach (string path in paths)
            {
                var selected = new Fixture(); selected.Files[path] = "deliberately not JSON";
                selected.Files["region.json"] = selected.Files["region.json"].Replace("wharf.json", path);
                StringAssert.Contains("superseded", Assert.Throws<KeySceneImport.Refusal>(() => selected.Import()).Message);
            }
        }

        [Test] public void ASecondImport_IsIdentical_AndKeepsItsGuid()
        {
            var f = new Fixture(); var first = f.Import(); var again = f.Import(first.Assets);
            CollectionAssert.IsEmpty(again.Changed); CollectionAssert.IsEmpty(again.Metas);
            Assert.AreEqual(first.Assets[Asset], again.Assets[Asset]);
            StringAssert.Contains(KeySceneImport.GuidFor(Wharf), first.Metas[Asset + ".meta"]);
        }

        [Test] public void Regions_AreIsolated_BeforeForeignAssetsAreParsed()
        {
            var f = new Fixture(); var a = f.Import();
            var existing = new Dictionary<string, string>(a.Assets) { ["KeyScene_stp_landing.asset"] = "foreign invalid YAML" };
            CollectionAssert.IsEmpty(f.Import(existing).Changed);
            var files = new Dictionary<string, byte[]> {
                ["island.json"] = B("{`schema`:`hidden-harbours/island@1`,`scenes`:[{`id`:`keyscene.stp_test`,`file`:`scenes/test/scene.json`}]}"),
                ["scenes/test/scene.json"] = B("{`id`:`keyscene.stp_test`,`name`:`test`,`pieces`:[]}") };
            files["SHA256SUMS"] = Encoding.UTF8.GetBytes(string.Join("\n", files.Select(x => Sha(x.Value) + "  " + x.Key)));
            KeySceneImport.Result Stp(IReadOnlyDictionary<string,string> old) => KeySceneImport.Import("stp", p => files.TryGetValue(p, out var b) ? b : null,
                old, "region.st_peters", Guid, B(f.Map), B(f.Hosts));
            var baseline = Stp(null);
            var mixed = new Dictionary<string,string>(baseline.Assets) { [Asset] = "foreign invalid YAML" };
            CollectionAssert.IsEmpty(Stp(mixed).Changed);
            CollectionAssert.AreEqual(baseline.Assets, Stp(mixed).Assets);
        }

        [Test] public void IncompatibleKindsAndKits_AndChangedIdentity_Refuse()
        {
            Assert.Throws<KeySceneImport.Refusal>(() => new Fixture(Piece.Replace("SetPieceDef", "MysteryDef")).Import());
            Assert.Throws<KeySceneImport.Refusal>(() => new Fixture(Piece.Replace("nmcSetPieces", "DoryIso")).Import());
            var old = new Fixture().Import();
            Assert.Throws<KeySceneImport.Refusal>(() => new Fixture(Piece.Replace("nmcSetPieces", "NmcStores")).Import(old.Assets));
            Assert.Throws<KeySceneImport.Refusal>(() => KeySceneImport.NmcPlanKindOf("MysteryPlan"));
        }

        [Test] public void HouseholdsStationsFootprintsAndConditions_StayData()
        {
            string row = Piece.Replace("`dir`:4", "`dir`:4,`def`:{`household`:`a family from Toronto every August`},`stations`:[{`id`:`door`,`at`:[1,2,3]}],`footprint`:[-2,4,-3,5],`lights`:[{`at`:[0,0,2],`when`:`dusk`,`dot`:true}]");
            var r = new Fixture(row).Import(); var p = Rows(r)[0];
            Assert.AreEqual("a family from Toronto every August", Option(p, "nmc.household"));
            Assert.AreEqual("High Summer; exact window H13", Option(p, "nmc.visitMapping"));
            Assert.AreEqual("[-2,4,-3,5]", Option(p, "nmc.footprint"));
            Assert.AreEqual("{x: 1, y: 2}", p.Get("At").Scalar);
            CollectionAssert.IsEmpty(p.Get("Words").Items);
            Assert.AreEqual(1, Field(Read(r), "Lights").Items.Count);
        }

        [Test] public void CommittedScenes_HaveTheRuledCounts_AndPassTheDataContract()
        {
            string[] suffixes = { "wharf", "fuel_stop", "town_bridge", "the_falls", "landing", "campground", "north_creek", "weather_face", "main_street", "south_shore", "plaza", "shore_lane" };
            int[] source = {134,30,55,21,25,178,53,11,131,66,122,40}, owned = {134,30,53,11,25,178,53,11,91,66,122,40};
            var ids = new HashSet<string>();
            for (int i=0;i<suffixes.Length;i++)
            {
                string file = "Assets/_Project/Data/KeyScenes/KeyScene_nmc_" + suffixes[i] + ".asset";
                Assert.IsTrue(File.Exists(file), file);
                var data = KeySceneImport.ReadAsset(File.ReadAllText(file), file);
                Assert.AreEqual("keyscene.nmc_" + suffixes[i], Field(data,"Id").Scalar);
                Assert.AreEqual("region.nine_mile_creek",Field(data,"RegionId").Scalar);
                Assert.AreEqual(suffixes[i] == "wharf" ? "1" : "0", Field(data,"Placed").Scalar);
                StringAssert.Contains("HH-nmc-key-scenes-one-scene-reissue-3-2026-10-07", Field(data,"Source").Scalar);
                var pieces = Field(data,"Pieces").Items;
                Assert.AreEqual(source[i].ToString(),Option(pieces[0],"nmc.scene.sourceRows"));
                Assert.AreEqual(owned[i].ToString(),Option(pieces[0],"nmc.scene.ownedRows"));
                Assert.AreEqual(owned[i]-(i==0?1:0),pieces.Count);
                foreach(var p in pieces)
                {
                    string id=p.Get("Id").Scalar;
                    Assert.IsTrue(ids.Add(id),id);
                    StringAssert.IsMatch(@"^[a-z][a-z0-9_]*\.[a-z][a-z0-9_]*$",id);
                    Assert.IsNotEmpty(p.Get("Owner").Scalar,id);
                    Assert.IsNotEmpty(p.Get("Kit").Scalar,id);
                    Assert.IsNotEmpty(p.Get("Variants").Items,id);
                    CollectionAssert.IsEmpty(p.Get("Words").Items,id);
                    CollectionAssert.DoesNotContain(Field(data,"RetiredIds").Items.Select(r=>r.Scalar).ToArray(),id);
                }
                foreach (var light in Field(data,"Lights").Items)
                    Assert.IsTrue(pieces.Any(p=>p.Get("Id").Scalar==light.Get("PieceId").Scalar));
            }
            Assert.AreEqual(813,ids.Count);
            var region = File.ReadAllText("Assets/_Project/Data/Regions/NineMileCreek.asset");
            StringAssert.Contains("Id: region.nine_mile_creek",region);
        }

        [Test] public void TheManifest_MustHold_AndMissingOwnersRefuse()
        {
            var f = new Fixture(); var read = f.Reader();
            Assert.Throws<KeySceneImport.Refusal>(() => KeySceneImport.ImportNmc("synthetic",
                p => p == "wharf.json" ? B("tampered") : read(p), null, Guid, B(f.Map), B(f.Hosts), B(f.Retired)));
            f.Files["region.json"] = f.Files["region.json"].Replace(J("`prop.nmc_one`:`" + Wharf + "`"), "");
            StringAssert.Contains("missing owner", Assert.Throws<KeySceneImport.Refusal>(() => f.Import()).Message);
        }

        [Test] public void PlanKinds_ReuseTerrainDefs_AndKeepMeasuredGeometry()
        {
            var f = new Fixture();
            f.Files["g/plan.json"] = J("{`roads`:[{`id`:`road.nmc_lane`,`width`:4.2,`pts`:[[1,2],[3,4]]}]," +
                "`bridges`:[{`id`:`bridge.nmc_brook`,`span`:8,`deck`:5.2}],`lots`:{`town`:[{`id`:`lot.nmc_house`,`at`:[1,2],`footprint`:[-2,4,-3,5]}]}," +
                "`areas`:[{`id`:`area.nmc_test`,`box`:[-2,4,-3,5]}],`grading`:[{`id`:`grade.nmc_test`}]}");
            var row = Rows(f.Import())[0];
            StringAssert.Contains("4.2", Option(row,"nmc.plan.roads"));
            StringAssert.Contains("5.2", Option(row,"nmc.plan.bridges"));
            Assert.AreEqual(J("[{`id`:`area.nmc_test`,`box`:[-2,4,-3,5]}]"), Option(row,"nmc.plan.areas"));
            Assert.IsFalse(row.Get("Options").Items.Any(o=>o.Get("Key").Scalar=="nmc.plan.grading"));
            StringAssert.Contains(Sha(B(f.Files["g/plan.json"])), Option(row,"nmc.ground.source"));
            foreach (string kind in new[] { "road", "lot", "bridge", "household", "PathDef", "StreamDef", "PondDef", "CoastSectionDef" })
                StringAssert.Contains(kind, Option(row,"nmc.plan.kinds"));
        }

        static Fixture TransferFixture(bool transferred)
        {
            var f = new Fixture(Piece, Piece.Replace("prop.nmc_one", "prop.nmc_three"));
            f.Files["region.json"] = f.Files["region.json"].Replace(J("`file`:`wharf.json`}"),
                J("`file`:`wharf.json`},{`id`:`" + Falls + "`,`file`:`falls.json`}"))
                .Replace(J("`owners`:{"), J("`owners`:{`prop.nmc_two`:`" + Falls + "`,"));
            f.Files["falls.json"] = J("{`schema`:`hidden-harbours/keyscene@2`,`id`:`" + Falls +
                "`,`name`:`falls`,`pieces`:[" + Piece.Replace("prop.nmc_one", "prop.nmc_two") +
                (transferred ? "," + Piece.Replace("[1,2]", "[8,9]") : "") + "]}");
            if (transferred)
                f.Files["region.json"] = f.Files["region.json"].Replace(J("`prop.nmc_one`:`" + Wharf + "`"), J("`prop.nmc_one`:`" + Falls + "`"))
                    .Replace(J("`refs`:[]"), J("`refs`:[],`takenOver`:[{`id`:`prop.nmc_one`,`from`:`" + Wharf + "`,`owner`:`" + Falls + "`,`why`:`moved by call 69`}]"));
            return f;
        }

        static Fixture HouseFixture(string kit, string id = "structure.nmc_south_shore_house_4", string scene = "keyscene.nmc_south_shore")
        {
            var f = new Fixture(Piece.Replace("prop.nmc_one", id).Replace("nmcSetPieces", kit).Replace("SetPieceDef", "BuildingDef"));
            f.Files["region.json"] = f.Files["region.json"].Replace(Wharf, scene);
            f.Files["wharf.json"] = f.Files["wharf.json"].Replace(Wharf, scene);
            return f;
        }

        [Test] public void TheTwoRuledEstateMigrations_KeepIdKindSceneAndGuid()
        {
            const string scene = "keyscene.nmc_south_shore", file = "KeyScene_nmc_south_shore.asset";
            foreach (string id in new[] { "structure.nmc_south_shore_house_4", "structure.nmc_south_shore_house_5" })
            {
                var before = HouseFixture("HouseIso (old/path.js)", id).Import();
                var f = HouseFixture("NmcShoreEstates.estateHouse", id);
                var after = f.Import(before.Assets);
                var data = KeySceneImport.ReadAsset(after.Assets[file], file);
                var row = Field(data,"Pieces").Items.Single();
                Assert.AreEqual(id,row.Get("Id").Scalar);
                Assert.AreEqual("building",row.Get("Kind").Scalar);
                Assert.AreEqual(scene,Field(data,"Id").Scalar);
                Assert.AreEqual(scene,Option(row,"nmc.ownerScene"));
                Assert.AreEqual("NmcShoreEstates.estateHouse",row.Get("Kit").Scalar);
                CollectionAssert.IsEmpty(Field(data,"RetiredIds").Items);
                CollectionAssert.IsEmpty(after.Metas, "the existing scene GUID is never rewritten");
                StringAssert.Contains(KeySceneImport.GuidFor(scene),before.Metas[file+".meta"]);
                Assert.IsTrue(after.Report.Any(l=>l.Contains("kit migration "+id) && l.Contains("third issue south-shore redraw, call 65")));
                CollectionAssert.IsEmpty(f.Import(after.Assets).Changed);
                Assert.Throws<KeySceneImport.Refusal>(()=>HouseFixture("HouseIso",id).Import(after.Assets),"not reversible");
                var wrongScene = HouseFixture("HouseIso",id,Wharf).Import();
                Assert.Throws<KeySceneImport.Refusal>(()=>HouseFixture("NmcShoreEstates.estateHouse",id,Wharf).Import(wrongScene.Assets));
                var wrongKind = HouseFixture("NmcShoreEstates.estateHouse",id);
                wrongKind.Files["wharf.json"] = wrongKind.Files["wharf.json"].Replace("BuildingDef","SetPieceDef");
                Assert.Throws<KeySceneImport.Refusal>(()=>wrongKind.Import(before.Assets));
            }
        }

        [Test] public void AnUnlistedNmcIdChangingFamily_StillRefuses()
        {
            const string id = "structure.nmc_south_shore_house_6";
            var before = HouseFixture("HouseIso",id).Import();
            StringAssert.Contains("An id that changes kit is a STOP for the owner, not a move.",
                Assert.Throws<KeySceneImport.Refusal>(()=>HouseFixture("NmcShoreEstates.estateHouse",id).Import(before.Assets)).Message);
        }

        [Test] public void SameFamilyAnnotationChanges_PassOnlyOnNmc()
        {
            var before = HouseFixture("HouseIso").Import();
            foreach (string annotation in new[] { "HouseIso 1.1", "HouseIso.preset", "HouseIso,new annotation", "HouseIso(new/path.js)" })
                Assert.DoesNotThrow(()=>HouseFixture(annotation).Import(before.Assets),annotation);
        }

        static KeySceneImport.Result StPetersKit(string kit, IReadOnlyDictionary<string,string> existing = null)
        {
            var files = new Dictionary<string,byte[]> {
                ["island.json"] = B("{`schema`:`hidden-harbours/island@1`,`scenes`:[{`id`:`keyscene.stp_test`,`file`:`scene.json`}]}"),
                ["scene.json"] = B("{`id`:`keyscene.stp_test`,`name`:`test`,`pieces`:[" +
                    Piece.Replace("prop.nmc_one","structure.stp_test_house").Replace("nmcSetPieces",kit).Replace("SetPieceDef","BuildingDef") + "]}") };
            files["SHA256SUMS"] = Encoding.UTF8.GetBytes(string.Join("\n",files.Select(x=>Sha(x.Value)+"  "+x.Key)));
            var f = new Fixture();
            return KeySceneImport.Import("synthetic",p=>files.TryGetValue(p,out var b)?b:null,existing,"region.st_peters",Guid,B(f.Map),B(f.Hosts));
        }

        [Test] public void StPeters_StillRefusesACrossFamilyChange()
        {
            var before = StPetersKit("HouseIso");
            StringAssert.Contains("its kit changes",Assert.Throws<KeySceneImport.Refusal>(()=>StPetersKit("NmcShoreEstates.estateHouse",before.Assets)).Message);
        }

        [Test] public void StPeters_StillRefusesASameFamilyAnnotationChange()
        {
            var before = StPetersKit("HouseIso");
            foreach (string annotation in new[] { "HouseIso 1.1", "HouseIso.preset", "HouseIso,new annotation", "HouseIso(new/path.js)" })
                StringAssert.Contains("its kit changes",Assert.Throws<KeySceneImport.Refusal>(()=>StPetersKit(annotation,before.Assets)).Message);
        }

        [Test] public void TakenOverIds_ChangeOwner_OnlyAsRegionJsonListsThem_AndNeverRetire()
        {
            var old = TransferFixture(false).Import();
            var f = TransferFixture(true);
            var result = f.Import(old.Assets);
            Assert.AreEqual(1, Rows(result).Count);
            Assert.AreEqual("2", Option(Rows(result)[0], "nmc.scene.sourceRows"));
            Assert.AreEqual("1", Option(Rows(result)[0], "nmc.scene.ownedRows"));
            StringAssert.Contains("[1,2]", Option(Rows(result)[0], "nmc.scene.takeoverRecords"));
            var target = Field(KeySceneImport.ReadAsset(result.Assets["KeyScene_nmc_the_falls.asset"], "falls"), "Pieces").Items;
            var moved = target.Single(p=>p.Get("Id").Scalar=="prop.nmc_one");
            Assert.AreEqual("{x: 8, y: 9}", moved.Get("At").Scalar);
            string record = Option(moved, "nmc.takeover");
            StringAssert.Contains(J("`from`:`" + Wharf + "`"), record);
            StringAssert.Contains(J("`to`:`" + Falls + "`"), record);
            StringAssert.Contains(J("`call`:69"), record);
            foreach (string asset in result.Assets.Values)
                CollectionAssert.DoesNotContain(Field(KeySceneImport.ReadAsset(asset,"asset"),"RetiredIds").Items.Select(p=>p.Scalar).ToArray(),"prop.nmc_one");
            CollectionAssert.IsEmpty(f.Import(result.Assets).Changed);
            CollectionAssert.IsEmpty(result.Metas);
            // The family may keep its identity when a version/path/preset annotation changes.
            f.Files["falls.json"] = f.Files["falls.json"].Replace("nmcSetPieces", "nmcSetPieces 1.1 (new/path.js)");
            Assert.DoesNotThrow(()=>f.Import(old.Assets));
            foreach (string mutation in new[] { "missing", "wrongFrom", "wrongTo", "duplicate", "missingRecord", "missingOwned", "changedKit", "changedKind" })
            {
                var bad = TransferFixture(true);
                if (mutation == "missing") bad.Files["region.json"] = bad.Files["region.json"].Replace("takenOver", "unused");
                if (mutation == "wrongFrom") bad.Files["region.json"] = bad.Files["region.json"].Replace(J("`from`:`" + Wharf + "`"), J("`from`:`keyscene.nmc_landing`"));
                if (mutation == "wrongTo") bad.Files["region.json"] = bad.Files["region.json"].Replace(J("`owner`:`" + Falls + "`"), J("`owner`:`" + Wharf + "`"));
                if (mutation == "duplicate") bad.Files["region.json"] = bad.Files["region.json"].Replace(J("`takenOver`:["), J("`takenOver`:[{`id`:`prop.nmc_one`,`from`:`" + Wharf + "`,`owner`:`" + Falls + "`,`why`:`call 69`},"));
                if (mutation == "missingRecord") bad.Files["wharf.json"] = bad.Files["wharf.json"].Replace(J(Piece + ","), "");
                if (mutation == "missingOwned") bad.Files["falls.json"] = bad.Files["falls.json"].Replace(J("," + Piece.Replace("[1,2]", "[8,9]")), "");
                if (mutation == "changedKit") bad.Files["falls.json"] = bad.Files["falls.json"].Replace("nmcSetPieces", "NmcStores");
                if (mutation == "changedKind") bad.Files["falls.json"] = bad.Files["falls.json"].Replace("nmcSetPieces", "NmcStores").Replace("SetPieceDef", "BuildingDef");
                Assert.Throws<KeySceneImport.Refusal>(()=>bad.Import(old.Assets),mutation);
            }
            // Matching source records cannot disguise a family change relative to the committed owner.
            var disguised = TransferFixture(true);
            disguised.Files["wharf.json"] = disguised.Files["wharf.json"].Replace("nmcSetPieces", "NmcStores");
            disguised.Files["falls.json"] = disguised.Files["falls.json"].Replace("nmcSetPieces", "NmcStores");
            Assert.Throws<KeySceneImport.Refusal>(()=>disguised.Import(old.Assets));
        }

        [Test] public void TheNewKitFamilies_Import_AndEveryOtherPairStillRefuses()
        {
            foreach (var pair in new[] { ("NmcShoreEstates","BuildingDef"), ("NmcShoreEstates","SetPieceDef"),
                ("NmcMainStreet","BuildingDef"), ("NmcMainStreet","SetPieceDef"), ("WalkupIso","BuildingDef"), ("RowhouseIso","BuildingDef") })
            {
                var f = new Fixture(Piece.Replace("nmcSetPieces",pair.Item1).Replace("SetPieceDef",pair.Item2));
                Assert.AreEqual(pair.Item2=="BuildingDef"?"building":"setPiece",Rows(f.Import())[0].Get("Kind").Scalar);
            }
            foreach (string kit in new[] { "NmcShoreEstates", "NmcMainStreet", "WalkupIso", "RowhouseIso" })
                foreach (string type in new[] { "VehicleDef", "AmbientVehicleDef", "AmbientBoatDef", "RockDef", "MysteryDef", "SetPieceDef" })
                {
                    if (type=="SetPieceDef" && (kit=="NmcShoreEstates"||kit=="NmcMainStreet")) continue;
                    Assert.Throws<KeySceneImport.Refusal>(()=>new Fixture(Piece.Replace("nmcSetPieces",kit).Replace("SetPieceDef",type)).Import(),kit+" / "+type);
                }
        }

        [Test] public void HostsNamedById_ResolveToAnOwnedRow_OrRefuse()
        {
            string hosted = Piece.Replace("`dir`:4", "`dir`:4,`host`:`prop.nmc_hostOld`");
            var f = new Fixture(hosted, Piece.Replace("prop.nmc_one","prop.nmc_hostOld"));
            f.Map = "{`schema`:`hidden-harbours/key-scene-id-map@1`,`renames`:[{`old`:`prop.nmc_hostOld`,`new`:`prop.nmc_host`}] }";
            var row = Rows(f.Import())[0];
            Assert.AreEqual("prop.nmc_host", row.Get("MountHost").Scalar);
            Assert.AreEqual("mount", row.Get("StandsOn").Scalar);
            Assert.AreEqual("", row.Get("MountAnchor").Scalar);
            Assert.AreEqual(J("`prop.nmc_host`"), Option(row,"nmc.host"));
            f.Files["wharf.json"] = f.Files["wharf.json"].Replace(J(","+Piece.Replace("prop.nmc_one","prop.nmc_hostOld")), "");
            f.Files["region.json"] = f.Files["region.json"].Replace(J(",`prop.nmc_hostOld`:`"+Wharf+"`"), "");
            f.Files["g/plan.json"] = J("{`id`:`prop.nmc_hostOld`}");
            Assert.Throws<KeySceneImport.Refusal>(()=>f.Import(),"a plan id is not an owned scene row");
            var otherScene = TransferFixture(false);
            otherScene.Files["wharf.json"] = otherScene.Files["wharf.json"].Replace(J("`id`:`prop.nmc_one`"),J("`id`:`prop.nmc_one`,`host`:`prop.nmc_two`"));
            Assert.Throws<KeySceneImport.Refusal>(()=>otherScene.Import(),"another scene's row is not a host");
            Assert.Throws<KeySceneImport.Refusal>(()=>new Fixture(Piece.Replace("`dir`:4","`dir`:4,`host`:`prop.nmc_one`")).Import(),"self host");
            var conflict = new Fixture(Piece.Replace("`dir`:4","`dir`:4,`host`:`prop.nmc_two`,`mount`:`on the roof`"),Piece.Replace("prop.nmc_one","prop.nmc_two"),Piece.Replace("prop.nmc_one","prop.nmc_three"));
            conflict.Hosts = "{`schema`:`hidden-harbours/key-scene-hosts@1`,`hosts`:[{`piece`:`prop.nmc_one`,`mount`:`on the roof`,`standsOn`:`mount`,`host`:`prop.nmc_three`,`anchor`:`roof`,`answer`:`fixture`}]}";
            Assert.Throws<KeySceneImport.Refusal>(()=>conflict.Import(),"host/mount disagreement");
        }

        [Test] public void TheThirdIssuesRecordFolders_AreNeverParsed()
        {
            foreach (string folder in new[] { "record-2d", "record-2e", "record-2f" })
            {
                string path = "old/"+folder+"/scene.json";
                var f = new Fixture(); f.Files[path] = "invalid record JSON";
                Assert.DoesNotThrow(()=>f.Import());
                f.Files["region.json"] = f.Files["region.json"].Replace("wharf.json",path);
                StringAssert.Contains("superseded",Assert.Throws<KeySceneImport.Refusal>(()=>f.Import()).Message);
            }
        }

        [Test] public void TheFourteenRedrawnIds_AreRetired_AndNeverReused()
        {
            var table = KeySceneImport.Json.Parse(File.ReadAllText(KeySceneImport.NmcRetiredFile),"retired").Get("retired").Items;
            Assert.AreEqual(206,table.Count);
            string[] expected = { "boat.nmc_shore_dory","prop.nmc_bank_bench_4","prop.nmc_bank_bench_5","prop.nmc_shore_fire",
                "structure.nmc_bank_steps_4","structure.nmc_bank_steps_5","prop.nmc_lot_pole_05","prop.nmc_lot_pole_04","prop.nmc_yard_fence_e",
                "prop.nmc_yard_rack_02","prop.nmc_yard_rack_01","structure.nmc_plaza_dock_grocery","structure.nmc_plaza_dock_lumber","structure.nmc_apartments_2" };
            CollectionAssert.AreEqual(expected,table.Skip(192).Select(r=>r.Get("id").Text).ToArray());
            for (int i=0;i<expected.Length;i++)
            {
                Assert.AreEqual("keyscene.nmc_"+(i<6?"south_shore":i<13?"plaza":"main_street"),table[192+i].Get("scene").Text);
                Assert.AreEqual("retired by the third issue's redraw",table[192+i].Get("reason").Text);
                var f = new Fixture(Piece.Replace("prop.nmc_one",expected[i]));
                f.Retired = File.ReadAllText(KeySceneImport.NmcRetiredFile);
                Assert.Throws<KeySceneImport.Refusal>(()=>f.Import(),expected[i]);
            }
            foreach (string old in new[] { "prop.nmc_lot_pole_198_81","prop.nmc_lot_pole_255_81","prop.nmc_yard_fence_e140","prop.nmc_yard_rack_267","prop.nmc_yard_rack_277" })
            {
                var f = new Fixture(Piece.Replace("prop.nmc_one",old));
                f.Map = File.ReadAllText(KeySceneImport.NmcIdMapFile); f.Retired = File.ReadAllText(KeySceneImport.NmcRetiredFile);
                Assert.Throws<KeySceneImport.Refusal>(()=>f.Import(),old);
            }
            foreach (string file in Directory.GetFiles("Assets/_Project/Data/KeyScenes","KeyScene_nmc_*.asset"))
            {
                var data = KeySceneImport.ReadAsset(File.ReadAllText(file),file);
                var retired = Field(data,"RetiredIds").Items.Select(r=>r.Scalar).ToArray();
                foreach (string id in expected) Assert.Contains(id,retired,file);
            }
        }

        [Test] public void CommittedThirdIssue_HoldsTakeoversHostsAreasAndSixHouseholdsAsData()
        {
            List<Node> Pieces(string suffix) => Field(KeySceneImport.ReadAsset(File.ReadAllText("Assets/_Project/Data/KeyScenes/KeyScene_nmc_"+suffix+".asset"),suffix),"Pieces").Items;
            var plaza = Pieces("plaza"); var hosts = plaza.Where(p=>p.Get("MountHost").Scalar.Length>0).ToArray();
            Assert.AreEqual(13,hosts.Length);
            foreach (var row in hosts) Assert.IsTrue(plaza.Any(p=>p.Get("Id").Scalar==row.Get("MountHost").Scalar));
            var main = Pieces("main_street"); var town = Pieces("town_bridge");
            foreach (string id in new[] { "structure.nmc_restaurant_bar","structure.nmc_chandlery" })
            {
                var row = main.Single(p=>p.Get("Id").Scalar==id);
                StringAssert.Contains("keyscene.nmc_town_bridge",Option(row,"nmc.takeover"));
                Assert.IsFalse(town.Any(p=>p.Get("Id").Scalar==id));
                StringAssert.Contains(id,Option(town[0],"nmc.scene.takeoverRecords"));
            }
            var wharf = Pieces("wharf")[0];
            Assert.AreEqual(7,KeySceneImport.Json.Parse(Option(wharf,"nmc.plan.areas"),"areas").Items.Count);
            StringAssert.Contains("06d0217c8c389c6b97582e2eb6b0cc44ff5bd042bde3af538ac47fd1fe89577a",Option(wharf,"nmc.ground.source"));
            var lots = KeySceneImport.Json.Parse(Option(wharf,"nmc.plan.lots"),"lots").Get("south_shore_houses").Items
                .Where(l=>l.Get("id").Text.StartsWith("lot.nmc_south_shore_estate_",StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(6,lots.Length);
            var homes = Pieces("south_shore").Where(p=>p.Get("Options").Items.Any(o=>o.Get("Key").Scalar=="nmc.household")).ToArray();
            Assert.AreEqual(6,homes.Length);
            foreach (var home in homes)
            {
                var def = KeySceneImport.Json.Parse(Option(home,"nmc.def"),"def");
                string brief = Option(home,"nmc.household");
                Assert.AreEqual(def.Get("household").Text,brief);
                Assert.AreEqual(brief,lots.Single(l=>l.Get("id").Text==def.Get("lot").Text).Get("household").Text);
                CollectionAssert.IsEmpty(home.Get("Words").Items);
            }
        }
    }
}
