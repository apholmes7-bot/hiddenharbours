using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// NMC H9, one gas bar: the fuel stop's 30 rows, the eleven wharf rows that wait for its kit, the fuel ids the
    /// region has issued, and the guards that bite once the new forecourt is placed. Text reads only (the key scene
    /// assets, the owners record, the scene file and the kit's rig), so every case runs without the editor's
    /// asset database. A guard whose subject had not landed ended in Assert.Ignore with the reason until H9 Phase B landed it; it is never
    /// deleted, and it starts to bite on the commit that lands its subject.
    /// </summary>
    public class NmcFuelStopRowTests
    {
        const string FuelStopPath = "Assets/_Project/Data/KeyScenes/KeyScene_nmc_fuel_stop.asset";
        const string WharfPath = "Assets/_Project/Data/KeyScenes/KeyScene_nmc_wharf.asset";
        const string RecordPath = "Assets/_Project/Data/KeyScenes/KeyScene_nmc_wharf.owners.json";
        const string ScenePath = "Assets/_Project/Scenes/NineMileCreek.unity";
        const string RigPath = "docs/art/rigs/fuel-stop/fuelStopRig.js";
        const string DefFolder = "Assets/_Project/Data/SetPieces";
        const string StationFolder = "Assets/_Project/Data/FuelStations";
        const string FuelPumpMeta = "Assets/_Project/Code/Economy/FuelPump.cs.meta";
        const string LoopTestPath = "Assets/Tests/PlayMode/NineMileCreekLoopPlayTests.cs";
        const string KitPrefix = "fuelStopRig";
        const string Forecourt = "CreekFuel";
        const string OldForecourt = "Route91Station";
        const string RoadGasPump = "fixture.route_91.pump1a_gas";

        /// <summary>The fuel stop's rows as the key scene has them: 28 set pieces on the kit and two gull perches.</summary>
        static readonly (string Id, string Piece)[] Thirty =
        {
            ("station.nmc_store", "store"), ("station.nmc_canopy", "canopy"), ("station.nmc_semi_lane", "truck"),
            ("station.nmc_pylon", "pylon"), ("station.nmc_propane", "propane"), ("station.nmc_ice", "iceBox"),
            ("station.nmc_bench", "bench"), ("station.nmc_bin", "bin"), ("station.nmc_firewood", "firewood"),
            ("station.nmc_air", "air"), ("station.nmc_vacuum", "vacuum"), ("station.nmc_dumpster", "dumpster"),
            ("station.nmc_picnic", "picnic"), ("station.nmc_stop", "stopSign"), ("station.nmc_stop_91", "stopSign"),
            ("station.nmc_light_w", "lampPole"), ("station.nmc_light_mid", "lampPole"), ("station.nmc_light_e", "lampPole"),
            ("station.nmc_picnic_stream", "picnic"), ("station.nmc_bin_stream", "bin"),
            ("station.nmc_wheel_stop_1", "wheelStop"), ("station.nmc_wheel_stop_2", "wheelStop"),
            ("station.nmc_wheel_stop_3", "wheelStop"), ("station.nmc_wheel_stop_4", "wheelStop"),
            ("station.nmc_wheel_stop_5", "wheelStop"), ("station.nmc_wheel_stop_6", "wheelStop"),
            ("station.nmc_wheel_stop_7", "wheelStop"), ("station.nmc_wheel_stop_8", "wheelStop"),
            ("gull.fs_canopy", "perch"), ("gull.fs_parapet", "perch"),
        };

        /// <summary>
        /// The eleven wharf rows that wait for the kit (the H9 charter's Decision 5): owner as the owners record has
        /// it, piece, place and height as the wharf's key scene has them. H9 places them; it moves none of this.
        /// </summary>
        static readonly (string Id, string Owner, string Piece, double X, double Y, double Z)[] Eleven =
        {
            ("prop.nmc_dumpster", "H6", "dumpster", 92.2, 133.8, 3.456),
            ("prop.nmc_picnic", "H6", "picnic", 93.9, 112.4, 3.638),
            ("prop.nmc_ice", "H6", "iceBox", 79.4, 111.8, 3.65),
            ("prop.nmc_bin", "H6", "bin", 92.8, 109.2, 3.642),
            ("prop.nmc_picnic_r", "H6", "picnic", 157.2, 142.8, 3.404),
            ("prop.nmc_light_court", "H6", "lampPole", 44.6, 108.6, 3.643),
            ("prop.nmc_light_lot", "H6", "lampPole", 124, 107.6, 3.646),
            ("prop.nmc_light_quay_w", "H6", "lampPole", 78.4, 95.8, 3.531),
            ("prop.nmc_light_quay_e", "H6", "lampPole", 147.8, 95.6, 3.523),
            ("prop.nmc_light_ramp", "H5", "lampPole", 48.6, 88.6, 3.454),
            ("prop.nmc_light_lot_e", "H6", "lampPole", 164, 107.4, 3.653),
        };

        /// <summary>
        /// Every fuel id the region has issued, with the grade it sells and the station it sells for. Ids are
        /// append-only: each stays on exactly one pump, and none is lost or reused when the forecourt is rebuilt.
        /// </summary>
        static readonly (string Id, string Grade, string Station)[] FuelIds =
        {
            ("fixture.route_91.pump1a_gas", "gas", "station.route_91"),
            ("fixture.route_91.pump1b_diesel", "diesel", "station.route_91"),
            ("fixture.route_91.pump2a_mixed", "mixed", "station.route_91"),
            ("fixture.route_91.pump2b_gas", "gas", "station.route_91"),
            ("fixture.nmc.wharf_pump_gas", "gas", "station.nmc_wharf_pumps"),
            ("fixture.nmc.wharf_pump_diesel", "diesel", "station.nmc_wharf_pumps"),
        };

        // ---------------------------------------------------------------- premises (green at the base and after H9)

        [Test]
        public void TheFuelStopsThirtyRows_AreH9s_TwentyEightOnTheKitAndTwoPerches()
        {
            var rows = Rows(FuelStopPath);
            Assert.That(rows.Select(r => (r.Id, r.Piece)), Is.EquivalentTo(Thirty), "the fuel stop's rows");
            Assert.That(rows.Select(r => r.Id).Distinct().Count(), Is.EqualTo(rows.Count), "an id twice in the fuel stop");
            foreach (var row in rows)
            {
                Assert.That(row.Owner, Is.EqualTo("H9"), row.Id);
                if (row.Id.StartsWith("gull.", StringComparison.Ordinal))
                    Assert.That((row.Kind, row.Kit), Is.EqualTo(("gull", "seagull")), row.Id);
                else
                {
                    Assert.That(row.Kind, Is.EqualTo("setPiece"), row.Id);
                    Assert.That(row.Kit, Does.StartWith(KitPrefix), row.Id);
                }
            }
        }

        [Test]
        public void TheElevenWaitingWharfRows_KeepTheirOwnersPiecesAndPlaces()
        {
            var rows = Rows(WharfPath).ToDictionary(r => r.Id);
            string record = File.ReadAllText(RecordPath);
            Assert.That(rows.Values.Count(r => r.Kit.StartsWith(KitPrefix, StringComparison.Ordinal)), Is.EqualTo(Eleven.Length),
                "the wharf's rows on the fuel stop kit");
            foreach (var pin in Eleven)
            {
                Assert.That(rows.TryGetValue(pin.Id, out var row), Is.True, pin.Id);
                Assert.That(row.Kind, Is.EqualTo("setPiece"), pin.Id);
                Assert.That(row.Kit, Does.StartWith(KitPrefix), pin.Id);
                Assert.That(row.Piece, Is.EqualTo(pin.Piece), pin.Id);
                Assert.That(row.X, Is.EqualTo(pin.X).Within(1e-6), pin.Id + " x");
                Assert.That(row.Y, Is.EqualTo(pin.Y).Within(1e-6), pin.Id + " y");
                Assert.That(row.Z, Is.EqualTo(pin.Z).Within(1e-6), pin.Id + " z");
                Assert.That(row.Dir, Is.EqualTo(4), pin.Id + " dir");
                Assert.That(RecordField(record, pin.Id, "owner"), Is.EqualTo(pin.Owner), pin.Id + " owner in the record");
            }
        }

        [Test]
        public void EveryRowOnTheKit_NamesAKindTheRigDraws()
        {
            if (!File.Exists(RigPath)) Assert.Ignore($"{RigPath} is not in the repo yet: the kit's copy waits on the owner's word.");
            var kinds = RigKinds(File.ReadAllText(RigPath));
            Assert.That(kinds, Has.Count.EqualTo(17), "the rig's KINDS");
            var asking = Rows(FuelStopPath).Concat(Rows(WharfPath)).Where(r => r.Kit.StartsWith(KitPrefix, StringComparison.Ordinal)).ToList();
            Assert.That(asking, Has.Count.EqualTo(28 + Eleven.Length), "rows on the kit");
            foreach (var row in asking) Assert.That(kinds, Has.Member(row.Piece), row.Id + " asks the kit for a piece it does not draw");
        }

        [Test]
        public void TheFuelStopsIds_AreNewInTheRegion_AndNoneIsARetiredId()
        {
            var fuel = Rows(FuelStopPath).Select(r => r.Id).ToList();
            var wharf = Rows(WharfPath).Select(r => r.Id).ToList();
            var retired = RetiredIds(FuelStopPath).Concat(RetiredIds(WharfPath)).ToList();
            Assert.That(fuel.Intersect(wharf), Is.Empty, "an id in both key scenes");
            Assert.That(fuel.Concat(wharf).Intersect(retired), Is.Empty, "a retired id is in use again");
        }

        [Test]
        public void TheFuelIds_AreEachOnOnePump_WithTheirGradeAndStation_AndNoneIsReused()
        {
            var scene = SceneText.Load();
            var pumps = scene.FuelPumps();
            var stations = StationIdsByGuid();
            Assert.That(pumps.Where(p => string.IsNullOrEmpty(p.Id)), Is.Empty, "a pump with no id");
            var reused = pumps.GroupBy(p => p.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.That(reused, Is.Empty, "a fuel id on two pumps");
            foreach (var pin in FuelIds)
            {
                var on = pumps.Where(p => p.Id == pin.Id).ToList();
                Assert.That(on, Has.Count.EqualTo(1), pin.Id + " is lost (on no pump)");
                Assert.That(on[0].Grade, Is.EqualTo(pin.Grade), pin.Id + " grade");
                Assert.That(stations.TryGetValue(on[0].StationGuid ?? "", out string station) ? station : "(no station)",
                    Is.EqualTo(pin.Station), pin.Id + " station");
            }
            foreach (var pump in pumps.Where(p => p.Id.StartsWith("fixture.route_91.", StringComparison.Ordinal)))
                Assert.That(pump.Id, Does.EndWith("_" + pump.Grade), pump.Id + " names a grade it does not sell");
        }

        // ---------------------------------------------------------------- guards that bite when H9's subject lands

        [Test]
        public void TheLoopsFuelLeg_FillsAtPump1aGas_OnTheNewForecourtUnderCreekFuel()
        {
            Assert.That(File.ReadAllText(LoopTestPath), Does.Contain("\"" + RoadGasPump + "\""), "the loop's leg asks for another pump");
            var scene = SceneText.Load();
            var pump = scene.FuelPumps().Where(p => p.Id == RoadGasPump).ToList();
            Assert.That(pump, Has.Count.EqualTo(1), RoadGasPump);
            var chain = scene.Ancestry(pump[0].GameObject);
            Assert.That(chain, Has.Member(Forecourt), RoadGasPump + " is not under " + Forecourt);
            Assert.That(scene.HasGameObjectNamed(OldForecourt), Is.False, OldForecourt + " is retired by name in H9 Phase B");
            Assert.That(chain, Has.No.Member(OldForecourt));
        }

        [Test]
        public void TheFuelStopsThirtyRows_ArePlacedOnceEach_UnderCreekFuel()
        {
            var scene = SceneText.Load();
            var placed = scene.PlacedIds();
            foreach (var row in Thirty)
            {
                Assert.That(placed.TryGetValue(row.Id, out var objects) ? objects.Count : 0, Is.EqualTo(1), row.Id + " placed once");
                Assert.That(scene.Ancestry(objects.Single()), Has.Member(Forecourt), row.Id + " is not under " + Forecourt);
            }
            Assert.That(TopField(FuelStopPath, "Placed"), Is.EqualTo("1"), "the fuel stop's key scene is not marked placed");
            Assert.That(ListField(FuelStopPath, "PlacementIds"), Is.SupersetOf(Thirty.Select(r => r.Id)));
        }

        [Test]
        public void TheElevenWaitingWharfRows_ArePlacedOnceEach_AndWaitNoLonger()
        {
            var scene = SceneText.Load();
            var placed = scene.PlacedIds();
            string record = File.ReadAllText(RecordPath);
            foreach (var row in Eleven)
            {
                Assert.That(placed.TryGetValue(row.Id, out var objects) ? objects.Count : 0, Is.EqualTo(1), row.Id + " placed once");
                Assert.That(RecordField(record, row.Id, "waits"), Is.Null.Or.Empty, row.Id + " still waits in the owners record");
                Assert.That(scene.Ancestry(objects.Single()), Has.Member("NmcWharfLandSide"), row.Id + " must share the wharf dressing parent");
            }
            Assert.That(ListField(WharfPath, "PlacementIds"), Is.SupersetOf(Eleven.Select(r => r.Id)));
        }

        [Test]
        public void EveryRowOnTheKit_HasItsSetPieceDef_OnTheRuledKindId()
        {
            // Only this kit's own Defs answer its rows. Other kits' set pieces share the folder and may share a generic
            // RigKey (the Falls' bench is also "bench"), so a Def is read here only when it names the fuel stop's key scene.
            string sceneId = TopField(FuelStopPath, "Id");
            Assert.That(sceneId, Is.Not.Null.And.Not.Empty, FuelStopPath + " has no Id");
            var defs = Directory.GetFiles(DefFolder, "*.asset")
                .Where(f => TopField(f, "SceneId") == sceneId)
                .Select(f => (Id: TopField(f, "Id"), RigKey: TopField(f, "RigKey"), File: f.Replace('\\', '/'))).ToList();
            Assert.That(defs.GroupBy(d => d.RigKey).Where(g => g.Count() > 1).Select(g => g.Key), Is.Empty,
                "two of the fuel stop's Defs share a RigKey");
            var asking = Rows(FuelStopPath).Concat(Rows(WharfPath)).Where(r => r.Kit.StartsWith(KitPrefix, StringComparison.Ordinal)).ToList();
            foreach (var row in asking)
            {
                var shared = new Dictionary<string, string> {
                    { "wheelStop", "prop.nmc_wheel_stop" }, { "stopSign", "prop.nmc_stop_sign" },
                    { "lampPole", "prop.nmc_lamp_pole" }, { "picnic", "prop.nmc_picnic" },
                    { "bin", "prop.nmc_bin" }, { "iceBox", "prop.nmc_ice_box" }, { "dumpster", "prop.nmc_dumpster" } };
                string id = shared.TryGetValue(row.Piece, out var kindId) ? kindId : row.Id;
                var mine = defs.Where(d => d.Id == id).ToList();
                Assert.That(mine, Has.Count.EqualTo(1), row.Id + " has no SetPieceDef, or two");
                Assert.That(mine[0].RigKey, Is.EqualTo(row.Piece), row.Id + " " + mine[0].File);
            }
        }

        // ---------------------------------------------------------------- text readers

        [Test]
        public void H6SkipsFuelStopRigRows_WhileKeepingItsMissingRowGuard()
        {
            string source = File.ReadAllText("Assets/_Project/Code/App/Editor/NineMileCreekWharfLandSide.Placement.cs");
            StringAssert.Contains("rows = rows.Where(p => !p.Kit.StartsWith(\"fuelStopRig\", StringComparison.Ordinal)).ToArray();", source);
            Assert.That(source.IndexOf("if (rows.Length != ids.Count)", StringComparison.Ordinal),
                Is.LessThan(source.IndexOf("rows = rows.Where", StringComparison.Ordinal)));
        }

        sealed class Row
        {
            public string Id, Kind = "", Owner = "", Kit = "", Piece = "";
            public double X, Y, Z;
            public int Dir;
        }

        static readonly Regex RowStart = new Regex(@"^  - Id: (.+?)\s*$");
        static readonly Regex RowField = new Regex(@"^    (Kind|Owner|Kit|Piece|At|Z|Dir):(.*)$");
        static readonly Regex TopLevel = new Regex(@"^  [A-Za-z]");
        static readonly Regex Xy = new Regex(@"x: (-?[0-9.eE+-]+), y: (-?[0-9.eE+-]+)");

        static double Num(string s) => double.Parse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);
        static string Unquote(string s) => s.Trim().Trim('"', '\'');

        static List<Row> Rows(string path)
        {
            var rows = new List<Row>();
            Row row = null;
            bool pieces = false;
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.TrimEnd('\r');
                if (line == "  Pieces:") { pieces = true; continue; }
                if (!pieces) continue;
                if (TopLevel.IsMatch(line)) break;
                var start = RowStart.Match(line);
                if (start.Success) { rows.Add(row = new Row { Id = start.Groups[1].Value }); continue; }
                var field = row == null ? Match.Empty : RowField.Match(line);
                if (!field.Success) continue;
                string value = field.Groups[2].Value.Trim();
                switch (field.Groups[1].Value)
                {
                    case "Kind": row.Kind = value; break;
                    case "Owner": row.Owner = value; break;
                    case "Kit": row.Kit = Unquote(value); break;
                    case "Piece": row.Piece = value; break;
                    case "Z": row.Z = Num(value); break;
                    case "Dir": row.Dir = (int)Num(value); break;
                    case "At":
                        var xy = Xy.Match(value);
                        Assert.That(xy.Success, Is.True, $"{path}: {row.Id} At '{value}'");
                        row.X = Num(xy.Groups[1].Value);
                        row.Y = Num(xy.Groups[2].Value);
                        break;
                }
            }
            Assert.That(rows, Is.Not.Empty, path + " has no rows");
            return rows;
        }

        static string TopField(string path, string name)
        {
            var m = Regex.Match(File.ReadAllText(path), "^  " + Regex.Escape(name) + ":(.*?)\r?$", RegexOptions.Multiline);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        static List<string> ListField(string path, string name)
        {
            var items = new List<string>();
            bool inside = false;
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.TrimEnd('\r');
                if (line == "  " + name + ":") { inside = true; continue; }
                if (!inside) continue;
                if (!line.StartsWith("  - ", StringComparison.Ordinal)) break;
                items.Add(line.Substring(4).Trim());
            }
            return items;
        }

        static List<string> RetiredIds(string path) => ListField(path, "RetiredIds");

        static string RecordField(string record, string id, string field)
        {
            var row = Regex.Match(record, "\\{[^{}]*\"id\"\\s*:\\s*\"" + Regex.Escape(id) + "\"[^{}]*\\}");
            Assert.That(row.Success, Is.True, RecordPath + " has no row " + id);
            var value = Regex.Match(row.Value, "\"" + Regex.Escape(field) + "\"\\s*:\\s*\"([^\"]*)\"");
            return value.Success ? value.Groups[1].Value : null;
        }

        static List<string> RigKinds(string rig)
        {
            var block = Regex.Match(rig, @"const KINDS = \{(.*?)\n  \};", RegexOptions.Singleline);
            Assert.That(block.Success, Is.True, RigPath + " has no KINDS block");
            return Regex.Matches(block.Groups[1].Value, @"(\w+): \{ label:").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        }

        static Dictionary<string, string> StationIdsByGuid()
        {
            var map = new Dictionary<string, string>();
            foreach (string asset in Directory.GetFiles(StationFolder, "*.asset"))
            {
                var guid = Regex.Match(File.ReadAllText(asset + ".meta"), @"guid: ([0-9a-f]{32})");
                if (guid.Success) map[guid.Groups[1].Value] = TopField(asset, "Id");
            }
            return map;
        }

        /// <summary>The scene file split into its YAML documents, read once per run.</summary>
        sealed class SceneText
        {
            sealed class Doc { public int Class; public bool Stripped; public string Body; }
            public sealed class Pump { public string Id, Grade, StationGuid, GameObject; }

            static SceneText _loaded;
            readonly Dictionary<string, Doc> _docs = new Dictionary<string, Doc>();
            readonly Dictionary<string, string> _transformOf = new Dictionary<string, string>();
            static readonly Regex Header = new Regex(@"^--- !u!(\d+) &(-?\d+)( stripped)?\r?$", RegexOptions.Multiline);
            static readonly Dictionary<string, Regex> Fields = new Dictionary<string, Regex>();

            public static SceneText Load() => _loaded ?? (_loaded = new SceneText(File.ReadAllText(ScenePath)));

            SceneText(string text)
            {
                var headers = Header.Matches(text);
                Assert.That(headers.Count, Is.GreaterThan(0), ScenePath);
                for (int i = 0; i < headers.Count; i++)
                {
                    int start = headers[i].Index + headers[i].Length;
                    int end = i + 1 < headers.Count ? headers[i + 1].Index : text.Length;
                    _docs[headers[i].Groups[2].Value] = new Doc
                    {
                        Class = int.Parse(headers[i].Groups[1].Value, CultureInfo.InvariantCulture),
                        Stripped = headers[i].Groups[3].Success,
                        Body = text.Substring(start, end - start),
                    };
                }
                foreach (var kv in _docs)
                    if ((kv.Value.Class == 4 || kv.Value.Class == 224) && !kv.Value.Stripped)
                        _transformOf[Ref(kv.Value.Body, "m_GameObject")] = kv.Key;
            }

            static string Field(string body, string name)
            {
                if (!Fields.TryGetValue(name, out var re))
                    Fields[name] = re = new Regex("^[ \t]*" + Regex.Escape(name) + ":(.*?)\r?$", RegexOptions.Multiline);
                var m = re.Match(body);
                return m.Success ? m.Groups[1].Value.Trim() : null;
            }

            static string Ref(string body, string name)
            {
                var m = Regex.Match(Field(body, name) ?? "", @"fileID: (-?\d+)");
                return m.Success ? m.Groups[1].Value : "0";
            }

            static string Guid(string body, string name)
            {
                var m = Regex.Match(Field(body, name) ?? "", @"guid: ([0-9a-f]{32})");
                return m.Success ? m.Groups[1].Value : null;
            }

            string InstanceName(string prefabInstance)
            {
                if (!_docs.TryGetValue(prefabInstance, out var pi)) return "(prefab instance " + prefabInstance + ")";
                var m = Regex.Match(pi.Body, @"propertyPath: m_Name\s*\n\s*value: (.*?)\r?\n");
                return m.Success ? m.Groups[1].Value.Trim() : "(unnamed prefab instance)";
            }

            public bool HasGameObjectNamed(string name) =>
                _docs.Any(kv => kv.Value.Class == 1 && !kv.Value.Stripped && Field(kv.Value.Body, "m_Name") == name) ||
                _docs.Any(kv => kv.Value.Class == 1001 && InstanceName(kv.Key) == name);

            /// <summary>The names from a GameObject up to its scene root, through prefab instances by their names.</summary>
            public List<string> Ancestry(string gameObject)
            {
                var names = new List<string>();
                string transform;
                var go = _docs[gameObject];
                if (go.Stripped)
                {
                    string pi = Ref(go.Body, "m_PrefabInstance");
                    names.Add(InstanceName(pi));
                    transform = _docs.TryGetValue(pi, out var piDoc) ? Ref(piDoc.Body, "m_TransformParent") : "0";
                }
                else if (!_transformOf.TryGetValue(gameObject, out transform)) return names;
                for (int guard = 0; transform != "0" && guard < 128; guard++)
                {
                    if (!_docs.TryGetValue(transform, out var tf)) break;
                    if (tf.Stripped)
                    {
                        string pi = Ref(tf.Body, "m_PrefabInstance");
                        names.Add(InstanceName(pi));
                        transform = _docs.TryGetValue(pi, out var piDoc) ? Ref(piDoc.Body, "m_TransformParent") : "0";
                    }
                    else
                    {
                        string owner = Ref(tf.Body, "m_GameObject");
                        names.Add(_docs.TryGetValue(owner, out var od) ? Field(od.Body, "m_Name") : "?");
                        transform = Ref(tf.Body, "m_Father");
                    }
                }
                return names;
            }

            public List<Pump> FuelPumps()
            {
                var guid = Regex.Match(File.ReadAllText(FuelPumpMeta), @"guid: ([0-9a-f]{32})").Groups[1].Value;
                return _docs.Values.Where(d => d.Class == 114 && Guid(d.Body, "m_Script") == guid)
                    .Select(d => new Pump
                    {
                        Id = Field(d.Body, "_id") ?? "", Grade = Field(d.Body, "_grade") ?? "",
                        StationGuid = Guid(d.Body, "_station"), GameObject = Ref(d.Body, "m_GameObject"),
                    }).ToList();
            }

            /// <summary>Row ids placed in the scene: a set piece's _placementId, or a perch's or fixture's _id.</summary>
            public Dictionary<string, HashSet<string>> PlacedIds()
            {
                var placed = new Dictionary<string, HashSet<string>>();
                foreach (var d in _docs.Values.Where(d => d.Class == 114))
                    foreach (string key in new[] { "_placementId", "_id" })
                    {
                        string id = Field(d.Body, key);
                        if (string.IsNullOrEmpty(id)) continue;
                        if (!placed.TryGetValue(id, out var set)) placed[id] = set = new HashSet<string>();
                        set.Add(Ref(d.Body, "m_GameObject"));
                    }
                return placed;
            }
        }
    }
}
