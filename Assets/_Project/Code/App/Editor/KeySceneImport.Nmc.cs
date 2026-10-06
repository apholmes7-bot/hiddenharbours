#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    public static partial class KeySceneImport
    {
        public const string NmcRegionId = "region.nine_mile_creek";
        public const string NmcPackageRegionId = "region.nmc";
        public const string NmcRegionSchema = "hidden-harbours/nmc-one-scene@1";
        public const string NmcIdMapFile = "docs/design/nmc-key-scenes/key-scene-id-map.json";
        public const string NmcHostsFile = "docs/design/nmc-key-scenes/key-scene-hosts.json";
        public const string NmcRetiredFile = "docs/design/nmc-key-scenes/key-scene-retired-ids.json";

        // Filename isolation happens BEFORE opening an asset. Pure callers get the same boundary.
        public static bool IsNmcAsset(string file) => file.StartsWith("KeyScene_nmc_", StringComparison.Ordinal);
        static IReadOnlyDictionary<string, string> WithoutNmc(IReadOnlyDictionary<string, string> existing) =>
            existing?.Where(e => !IsNmcAsset(e.Key)).ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);

        static readonly Regex NmcStableId = new Regex(@"^[a-z][a-z0-9_]*\.[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant);
        static readonly string[] NmcSceneSuffixes = { "wharf", "fuel_stop", "town_bridge", "the_falls", "landing",
            "campground", "north_creek", "weather_face", "main_street", "south_shore", "plaza", "shore_lane" };

        /// <summary>Pure NMC profile. Reads only manifest-checked package bytes, not Unity or the filesystem.
        /// The package's region alias maps here exactly once. Options carry the additional data contract;
        /// there is one placement per owned id, and refs never become placements.</summary>
        public static Result ImportNmc(string packageName, Func<string, byte[]> read,
            IReadOnlyDictionary<string, string> existing, string scriptGuid, byte[] idMap, byte[] hosts, byte[] retiredIds)
        {
            if (string.IsNullOrEmpty(packageName) || scriptGuid == null || !GuidRx.IsMatch(scriptGuid))
                throw new Refusal("NMC needs a package name and KeySceneDef's script guid.");
            var result = new Result();
            result.Report.Add("profile: NMC H1; " + NmcPackageRegionId + " -> " + NmcRegionId);
            Beside beside = ReadBeside(idMap, hosts, result, NmcIdMapFile, NmcHostsFile);
            Json retirement = Json.Parse(Text(retiredIds ?? throw new Refusal("no NMC retirement table"), NmcRetiredFile), NmcRetiredFile);
            if (Need(retirement, "schema", NmcRetiredFile).Str("retirement schema") != "hidden-harbours/nmc-retired-ids@1")
                throw new Refusal("unknown NMC retirement schema");
            var reservations = Items(Need(retirement, "retired", NmcRetiredFile), "retired ids")
                .Select(r => Need(r, "id", "retired id").Str("retired id")).ToList();
            if (reservations.Distinct().Count() != reservations.Count) throw new Refusal("duplicate retired id");
            var files = CheckSums(path => read(path == SumsFile ? "SHA256SUMS.txt" : path), result);
            Json region = ParseFile(files, "region.json");
            if (Need(region, "schema", "region").Str("region schema") != NmcRegionSchema ||
                Need(region, "id", "region").Str("region id") != NmcPackageRegionId)
                throw new Refusal("unknown NMC region schema or id.");
            var chosen = new Dictionary<string, Json>(StringComparer.Ordinal);
            var scenes = new Dictionary<string, Scene>(StringComparer.Ordinal);
            foreach (Json entry in Items(Need(region, "scenes", "region"), "region scenes"))
            {
                string id = Need(entry, "id", "scene entry").Str("scene id");
                if (!NmcSceneSuffixes.Any(s => id == "keyscene.nmc_" + s) || chosen.ContainsKey(id))
                    throw new Refusal("unknown or duplicate NMC scene: " + id);
                string file = Need(entry, "file", id).Str("scene file");
                if (NmcSupersededFile(file)) throw new Refusal("superseded NMC scene selected: " + file);
                Json json = ParseFile(files, file);
                string schema = Need(json, "schema", file).Str("scene schema");
                if (schema != "hidden-harbours/key-scene@1" && schema != "hidden-harbours/keyscene@2")
                    throw new Refusal("unknown NMC scene schema: " + schema);
                if (Need(json, "id", file).Str("scene id") != id) throw new Refusal("scene index/id mismatch: " + file);
                chosen.Add(id, json);
                scenes.Add(id, new Scene { Id = id, Name = Need(json, "name", id).Str("scene name"),
                    Source = $"{packageName}, {file}, {files[file].Length} bytes, sha256 {Sha256(files[file])}" });
            }
            Json ground = Need(region, "ground", "region");
            string groundFile = Need(ground, "package", "ground").Str("ground package") + Need(ground, "plan", "ground").Str("ground plan");
            Json plan = ParseFile(files, groundFile);
            var held = new Dictionary<string, string>(StringComparer.Ordinal);
            CollectIds(plan, "the NMC terrain plan (H2/H4)", held, beside);
            var owners = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var o in Need(region, "owners", "region").Fields)
            {
                string id = NmcId(o.Key, beside);
                if (owners.ContainsKey(id)) throw new Refusal("two owner keys map to " + id);
                owners.Add(id, o.Value.Str("owner"));
            }
            var actual = new Dictionary<string, string>(StringComparer.Ordinal);
            var poses = new Dictionary<string, List<Json>>(StringComparer.Ordinal);
            var refs = new List<(string Scene, string Id, string Owner)>();
            int count = 0, owned = 0;
            foreach (var s in chosen)
                foreach (Json p in Items(Need(s.Value, "pieces", s.Key), "pieces"))
                {
                    count++;
                    string sourceId = Need(p, "id", s.Key).Str("piece id"), id = NmcId(sourceId, beside);
                    if (!owners.TryGetValue(id, out string owner)) throw new Refusal("missing owner for " + id);
                    if (p.Has("ref"))
                    {
                        string target = p.Get("ref").Str("ref").Split(' ')[0];
                        if (target != owner || target == s.Key) throw new Refusal("ref/owner mismatch: " + id);
                        refs.Add((s.Key, id, owner));
                        continue;
                    }
                    owned++;
                    if (owner != s.Key) throw new Refusal("wrong owner for " + id);
                    if (actual.TryGetValue(id, out string previous) && previous != s.Key)
                        throw new Refusal("two scenes own " + id);
                    actual[id] = s.Key;
                    held[id] = s.Key;
                    if (!poses.TryGetValue(id, out var family)) poses[id] = family = new List<Json>();
                    family.Add(p);
                    scenes[s.Key].Tally.Pieces++;
                    scenes[s.Key].Tally.Owned++;
                    if (id != sourceId && !scenes[s.Key].ReservedIds.Contains(sourceId)) scenes[s.Key].ReservedIds.Add(sourceId);
                }
            foreach (var o in owners)
                if (!actual.TryGetValue(o.Key, out string scene) || scene != o.Value) throw new Refusal("owner has no owned row: " + o.Key);
            var indexRefs = Items(Need(region, "refs", "region"), "refs").Select(r =>
                (Scene: Need(r, "in", "ref").Str("ref scene"), Id: NmcId(Need(r, "id", "ref").Str("ref id"), beside),
                 Owner: Need(r, "owner", "ref").Str("ref owner"))).ToList();
            if (indexRefs.Count != refs.Count || indexRefs.Distinct().Count() != refs.Count || refs.Any(r => !indexRefs.Contains(r)))
                throw new Refusal("region refs do not match the scene refs.");
            foreach (var r in refs)
                if (!actual.TryGetValue(r.Id, out string owner) || owner != r.Owner) throw new Refusal("missing ref host: " + r.Id);
            foreach (var family in poses)
            {
                var s = scenes[actual[family.Key]];
                Row row = NmcRow(s, family.Key, family.Value[0], held, beside);
                if (family.Value.Count > 1)
                {
                    var variants = new HashSet<string>(StringComparer.Ordinal);
                    foreach (Json p in family.Value)
                    {
                        Row pose = NmcRow(s, family.Key, p, held, beside);
                        if (pose.Kind != row.Kind || pose.Kit != row.Kit || pose.Piece != row.Piece)
                            throw new Refusal("incompatible kind/kit in variant family " + row.Id);
                        foreach (string v in pose.Variants)
                            if (!variants.Add(v)) throw new Refusal("overlapping variants of " + row.Id);
                    }
                    row.Variants.Clear(); row.Variants.AddRange(variants.OrderBy(v => v, StringComparer.Ordinal));
                    // Pose changes, including position, are retained, never flattened into a second entity.
                    AddNmc(row, "variantPoses", "[" + string.Join(",", family.Value.Select(p => NmcMapped(p, beside))) + "]");
                }
                s.Rows.Add(row); s.Tally.Data++; s.Tally.Kits.Add(row.Kit);
                if (family.Value[0].Get("lights") is Json lights)
                    foreach (Json light in Items(lights, "lights")) s.Lights.Add(NmcLight(row.Id, light));
                Json one = family.Value[0].Get("light");
                if (one != null) s.Lights.Add(NmcLight(row.Id, one));
            }
            foreach (string id in beside.Hosts.Keys)
                if (!beside.Answered.Contains(id)) throw new Refusal("stale NMC host answer: " + id);

            // Additional contracts use the existing Options extension, scoped to the owning scene's first
            // owned piece. No duplicate terrain/NPC identity or raw package file is made into a game entity.
            foreach (var item in scenes)
            {
                Scene s = item.Value;
                if (s.Rows.Count == 0) throw new Refusal("NMC scene needs an owned piece for its contract: " + s.Id);
                Row carrier = s.Rows[0];
                Json source = chosen[s.Id];
                foreach (string key in new[] { "layout", "flora", "floraRules", "harbour", "crane", "roads", "lots", "lights", "sound", "game", "budgets", "ground", "context", "names" })
                    if (source.Has(key)) AddNmc(carrier, "scene." + key, NmcMapped(source.Get(key), beside));
                AddNmc(carrier, "scene.refs", "[" + string.Join(",", indexRefs.Where(r => r.Scene == s.Id)
                    .Select(r => "{\"id\":\"" + r.Id + "\",\"owner\":\"" + r.Owner + "\"}")) + "]");
                AddNmc(carrier, "scene.sourceRows", (s.Tally.Pieces + refs.Count(r => r.Scene == s.Id)).ToString());
                AddNmc(carrier, "scene.ownedRows", s.Tally.Owned.ToString());
                AddNmc(carrier, "deferred", NmcDeferred(s.Id));
                NmcHouseholds(s, source, beside);
                s.Tally.Lights = s.Lights.Count;
            }
            if (scenes.TryGetValue("keyscene.nmc_wharf", out Scene wharf))
            {
                Row carrier = wharf.Rows[0];
                AddNmc(carrier, "ground.source", groundFile + ", sha256 " + Sha256(files[groundFile]));
                foreach (string key in new[] { "coast", "water", "roads", "bridges", "boardwalks", "paths", "lots", "frozen" })
                    if (plan.Has(key)) AddNmc(carrier, "plan." + key, NmcMapped(plan.Get(key), beside));
                AddNmc(carrier, "plan.kinds", "{\"roads\":\"" + NmcPlanKindOf("RoadPlan") + "\",\"lots\":\"" +
                    NmcPlanKindOf("LotPlan") + "\",\"bridges\":\"" + NmcPlanKindOf("BridgePlan") + "\",\"households\":\"" +
                    NmcPlanKindOf("HouseholdPlan") + "\",\"paths\":\"" + NmcPlanKindOf("PathDef") + "\",\"streams\":\"" +
                    NmcPlanKindOf("StreamDef") + "\",\"ponds\":\"" + NmcPlanKindOf("PondDef") + "\",\"coast\":\"" + NmcPlanKindOf("CoastSectionDef") + "\"}");
                AddNmc(carrier, "plan.contract", "terrain: RegionTerrainPlanDef/GroundFileDef; coast: CoastSectionDef; water: StreamDef/PondDef; paths: PathDef; road/lot/bridge/household: NmcPlanKindOf; H2/H4/H13 apply, never grade in H1");
            }
            var ours = (existing ?? new Dictionary<string, string>()).Where(e => IsNmcAsset(e.Key))
                .ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
            var old = ReadExisting(ours, scriptGuid);
            var reserved = new HashSet<string>(old.Values.SelectMany(e => e.Retired), StringComparer.Ordinal);
            foreach (var e in old)
            {
                if (!scenes.ContainsKey(e.Key)) throw new Refusal("an existing NMC scene is absent from the package: " + e.Key);
                if (Get(e.Value.Top, "RegionId")?.Scalar != NmcRegionId) throw new Refusal("NMC asset has another region: " + e.Value.File);
                if (e.Value.Placed) throw new Refusal("H1 cannot rewrite a placed scene: " + e.Key);
                foreach (var row in e.Value.Rows)
                    if (actual.TryGetValue(row.Get("Id").Scalar, out string owner) && owner != e.Key)
                        throw new Refusal("an existing id changes scene owner: " + row.Get("Id").Scalar);
            }
            foreach (string id in actual.Keys)
                if (reserved.Contains(id) || reservations.Contains(id)) throw new Refusal("a retired id is never reused: " + id);
            // Package-wide reservations live on every scene, including ids retired before the first import.
            foreach (Scene scene in scenes.Values)
                foreach (string id in reservations)
                    if (!scene.ReservedIds.Contains(id)) scene.ReservedIds.Add(id);
            foreach (Scene scene in scenes.Values) Write(scene, old, ours, NmcRegionId, scriptGuid, result);
            result.Report.Add($"NMC source rows {count}; refs {refs.Count} (spawn zero); owned rows {owned}; unique owned ids {actual.Count}");
            result.Report.Add($"changed: {result.Changed.Count} of {result.Assets.Count} assets; new metas {result.Metas.Count}");
            return result;
        }

        static bool NmcSupersededFile(string file) => file.Contains("/record-2b/") || file.Contains("/record-2c/") ||
            file == "hh-nmc-wharf-return/scenes/keyscene.nmc_fuel_stop/scene.json" || file.Contains("main_street.plaza");

        static string NmcId(string id, Beside beside)
        {
            string mapped = beside.Id(id, "NMC");
            if (!NmcStableId.IsMatch(mapped)) throw new Refusal("NMC id needs an explicit snake_case rename: " + id);
            return mapped;
        }

        static string NmcMapped(Json value, Beside beside)
        {
            // JSON parsing/quoting preserves household text and rewrites both reference values and object keys.
            string Map(string text)
            {
                foreach (var r in beside.Renames.OrderByDescending(r => r.Key.Length))
                    text = Regex.Replace(text, @"(?<![\w.])" + Regex.Escape(r.Key) + @"(?![\w.])", _ => r.Value);
                return text;
            }
            string Quote(string s) => Json.Parse("{\"v\":" + SystemQuote(s) + "}", "quote").Get("v").Compact();
            if (value.Kind == Json.Of.String) return Quote(Map(value.Text));
            if (value.Kind == Json.Of.Array) return "[" + string.Join(",", value.Items.Select(v => NmcMapped(v, beside))) + "]";
            if (value.Kind == Json.Of.Object) return "{" + string.Join(",", value.Fields.Select(f => Quote(Map(f.Key)) + ":" + NmcMapped(f.Value, beside))) + "}";
            return value.Compact();
        }

        static string SystemQuote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
        static void AddNmc(Row row, string key, string value) => row.Options.Add(new KeyValuePair<string, string>("nmc." + key, value));

        static string NmcDeferred(string scene) => "placement=" + NmcPlacementOwner(scene) +
            "; names/households/calendar/visits/fire schedules=H13 after the owner names slate; tide/float=H5/H7/H8/H11; " +
            "lights/audio/flora=scene placement lane with art-pipeline/audio; river fishery=M3; " +
            "bookings/fees/ring/bell/pool/court/interiors=separate scope; roads/access=H4 (R2), then R4; office identity=N3; crane gameplay=N9";

        static string NmcPlacementOwner(string id)
        {
            switch (id)
            {
                case "keyscene.nmc_wharf": return "H5/H6 (N8 fleet; N9 crane; N3 office)";
                case "keyscene.nmc_fuel_stop": return "H9";
                case "keyscene.nmc_town_bridge": case "keyscene.nmc_the_falls": case "keyscene.nmc_main_street": return "H7";
                case "keyscene.nmc_campground": return "H12";
                case "keyscene.nmc_north_creek": return "H11";
                case "keyscene.nmc_plaza": return "H10";
                default: return "H8";
            }
        }
    }
}
#endif
