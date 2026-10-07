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
            int[] source = {134,30,55,21,25,178,53,11,10,8,51,32}, owned = {134,30,55,11,25,178,53,11,2,8,51,32};
            var ids = new HashSet<string>();
            for (int i=0;i<suffixes.Length;i++)
            {
                string file = "Assets/_Project/Data/KeyScenes/KeyScene_nmc_" + suffixes[i] + ".asset";
                Assert.IsTrue(File.Exists(file), file);
                var data = KeySceneImport.ReadAsset(File.ReadAllText(file), file);
                Assert.AreEqual("keyscene.nmc_" + suffixes[i], Field(data,"Id").Scalar);
                Assert.AreEqual("region.nine_mile_creek",Field(data,"RegionId").Scalar);
                Assert.AreEqual("0",Field(data,"Placed").Scalar);
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
                    CollectionAssert.IsEmpty(p.Get("Words").Items,id);
                }
            }
            Assert.AreEqual(589,ids.Count);
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
                "`bridges`:[{`id`:`bridge.nmc_brook`,`span`:8,`deck`:5.2}],`lots`:{`town`:[{`id`:`lot.nmc_house`,`at`:[1,2],`footprint`:[-2,4,-3,5]}]}}");
            var row = Rows(f.Import())[0];
            StringAssert.Contains("4.2", Option(row,"nmc.plan.roads"));
            StringAssert.Contains("5.2", Option(row,"nmc.plan.bridges"));
            foreach (string kind in new[] { "road", "lot", "bridge", "household", "PathDef", "StreamDef", "PondDef", "CoastSectionDef" })
                StringAssert.Contains(kind, Option(row,"nmc.plan.kinds"));
        }
    }
}
