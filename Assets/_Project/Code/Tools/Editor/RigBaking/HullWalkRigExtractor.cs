using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>The hull-layout bake reads only published members of the selected hull.</summary>
    public static class HullWalkRigExtractor
    {
        public const string GeometrySchema = "hidden-harbours/hull-geometry@1";

        public static string FindWalk(string scriptPath, IRigScriptHost host, string scope)
        {
            string folder = Path.Combine(RigCatalog.RepoRoot, HullWalkSidecarImporter.SidecarFolder);
            if (!Directory.Exists(folder)) return null;
            string stem = Path.GetFileNameWithoutExtension(scriptPath);
            string[] files = Directory.GetFiles(folder, stem + ".*.walk.json");
            if (files.Length == 0) return null;
            string id = host.EvaluateString($"String({scope}.id || '')");
            string file = Path.Combine(folder, stem + "." + id + ".walk.json");
            return File.Exists(file) ? file : null;
        }

        public static RigMeshData Extract(IRigScriptHost host, string scriptPath, string global,
                                          string scope, string walkPath)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(RigCatalog.RepoRoot, scriptPath));
            string relativeWalk = walkPath.Replace('\\', '/');
            string prefix = RigCatalog.RepoRoot.Replace('\\', '/').TrimEnd('/') + "/";
            if (relativeWalk.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) relativeWalk = relativeWalk.Substring(prefix.Length);
            HullWalkRead walk = HullWalkSidecarReader.Read(File.ReadAllText(walkPath), relativeWalk, bytes);
            walk.RigSourcePath = scriptPath.Replace('\\', '/');
            if (!walk.Ok) throw new FormatException(string.Join("; ", walk.Errors));
            if (walk.ExportSymbol != global || walk.Rig != Path.GetFileName(scriptPath) ||
                host.EvaluateString($"String({scope}.id)") != walk.Hull)
                throw new FormatException(walk.Hull + ": rig / exportSymbol / hull disagree with selected global");
            foreach (string method in new[] { "faceList", "interiorFaces", "doorFaces", "geometry", "matsFor" })
                if (!host.EvaluateBool($"typeof {scope}.{method} === 'function'"))
                    throw new FormatException(walk.Hull + ": missing global member " + method);
            var geometry = Json(host, scope + ".geometry()");
            var data = new RigMeshData { RigKey = global, GlobalName = global, Walk = walk, WalkGeometry = geometry };
            ReadGeometry(data, geometry);
            foreach (string field in new[] { "rig", "exportSymbol", "hull", "frame" })
                if (HullLayoutJson.Text(geometry, field, "geometry") != HullLayoutJson.Text(walk.Fields, field))
                    throw new FormatException(walk.Hull + ": geometry." + field + ": differs from walk");
            string units = HullLayoutJson.Text(geometry, "units", "geometry");
            if (units != "metres" && units != "m")
                throw new FormatException(walk.Hull + ": geometry.units: unsupported unit \"" + units + "\"");
            if (HullLayoutJson.Number(geometry, "dwlZ", "geometry") != walk.Dwl)
                throw new FormatException(walk.Hull + ": geometry.dwlZ: differs from walk.DWL");
            var tags = HullLayoutJson.Child(walk.Fields, "tags");
            foreach (var pair in new[] { ("lv", "ids"), ("ro", "roles") })
            {
                var a = HullLayoutJson.TagIds(HullLayoutJson.Child(tags, pair.Item1), "tags." + pair.Item1);
                var b = HullLayoutJson.TagIds(HullLayoutJson.Child(geometry, pair.Item2), "geometry." + pair.Item2);
                if (a.Count != b.Count || a.Any(k => !b.TryGetValue(k.Key, out int tag) || tag != k.Value))
                    throw new FormatException(walk.Hull + ": tags." + pair.Item1 + ": differs from geometry");
            }
            data.SourceFaceExpression = scope + ".faceList({})";
            data.W = (int)Number(host, scope, "W"); data.H = (int)Number(host, scope, "H");
            data.PxPerMetre = (int)Number(host, scope, "PX");
            data.PivotX = Number(host, scope, "pivot.x"); data.PivotY = Number(host, scope, "pivot.y");
            data.DefaultElev = Number(host, scope, "defaultElev");
            data.Gain = Number(host, scope, "GAIN"); data.Bias = Number(host, scope, "BIAS");
            data.LightN = new Vector3d(Number(host, scope, "LN[0]"), Number(host, scope, "LN[1]"), Number(host, scope, "LN[2]"));
            data.Keyline = host.EvaluateBool($"typeof {scope}.KEY === 'string'") ? Color(host.EvaluateString(scope + ".KEY")) : new Color32(0, 0, 0, 255);
            data.BayerWasExported = host.EvaluateBool($"Array.isArray({scope}.BAYER)");
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    data.Bayer[x, y] = data.BayerWasExported ? Number(host, scope, $"BAYER[{x}][{y}]") : (RigMeshSymbols.CanonicalBayer[x, y] + 0.5) / 16;
            var paints = Json(host, scope + ".matsFor()");
            var mats = HullLayoutJson.Child(paints, "MATS", "matsFor()");
            var exterior = FacesJson(host, scope + ".faceList({})");
            var leaf = FacesJson(host, scope + ".doorFaces(0)");
            var room = FacesJson(host, scope + ".interiorFaces()");
            exterior.AddRange(leaf);
            ReadFaces(exterior, mats, data, false, data.Faces, data.Materials,
                SchemeStableGroups(host, scope, mats, exterior));
            data.WalkInteriorFaces = new List<RigFace>();
            ReadFaces(room, mats, data, true, data.WalkInteriorFaces, data.InteriorMaterials);
            if (data.Materials.Count > HullMeshDef.HullRampSlots || data.InteriorMaterials.Count > HullMeshDef.InteriorRampSlots)
                throw new FormatException(walk.Hull + ": paints exceed hull / room ramp slots");
            // The room receives the same depth shift as the kit path, using the hull's bounds.
            if (data.Faces.Count > 0)
            {
                var points = data.Faces.SelectMany(f => f.V).ToArray();
                double dx = points.Max(p => p.X) - points.Min(p => p.X);
                double dy = points.Max(p => p.Y) - points.Min(p => p.Y);
                double dz = points.Max(p => p.Z) - points.Min(p => p.Z);
                double shift = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                foreach (RigFace face in data.WalkInteriorFaces) face.Db += shift;
            }
            return data;
        }

        static double Number(IRigScriptHost host, string scope, string member)
        {
            if (!host.EvaluateBool($"typeof {scope}.{member} === 'number' && isFinite({scope}.{member})"))
                throw new FormatException(scope + "." + member + ": expected finite number");
            return host.EvaluateNumber(scope + "." + member);
        }

        static Dictionary<string, object> Json(IRigScriptHost host, string expression) =>
            HullLayoutJson.Object(DeckSidecarJson.Parse(host.EvaluateString("JSON.stringify(" + expression + ")")), expression);
        static List<object> FacesJson(IRigScriptHost host, string expression) =>
            HullLayoutJson.List(DeckSidecarJson.Parse(host.EvaluateString("JSON.stringify(" + expression + ")")), expression);

        public static RigMeshData Door(IRigScriptHost host, string scope, RigMeshData hull, double pose)
        {
            var leaf = hull.WithFaces(new List<RigFace>());
            var mats = HullLayoutJson.Child(Json(host, scope + ".matsFor()"), "MATS");
            ReadFaces(FacesJson(host, scope + ".doorFaces(" + pose.ToString("R", CultureInfo.InvariantCulture) + ")"),
                      mats, leaf, false, leaf.Faces, leaf.Materials);
            return leaf;
        }

        static Dictionary<string, string> SchemeStableGroups(IRigScriptHost host, string scope,
                            Dictionary<string, object> mats, List<object> exterior)
        {
            // A single-scheme rig keeps its original palette construction exactly.
            if (!host.EvaluateBool($"Array.isArray({scope}.schemeIds) && {scope}.schemeIds.length > 1"))
                return null;
            var tables = HullLayoutJson.List(DeckSidecarJson.Parse(host.EvaluateString(
                $"JSON.stringify({scope}.schemeIds.map(function(id){{return {scope}.matsFor(id).MATS;}}))")), "schemes")
                .Select(t => HullLayoutJson.Object(t, "scheme.MATS")).ToArray();
            var used = new HashSet<string>(exterior.Select(f => HullLayoutJson.Text(HullLayoutJson.Object(f, "face"), "mat")));
            var groups = new Dictionary<string, string>(StringComparer.Ordinal);
            // Keep the rig's key order so each group's representative is its first used name.
            foreach (string name in mats.Keys.Where(used.Contains))
                groups.Add(name, string.Join(";", tables.Select(table =>
                {
                    var paint = HullLayoutJson.Child(table, name, "scheme.MATS");
                    return HullLayoutJson.Integer(paint, "off", name).ToString(CultureInfo.InvariantCulture) + "|" +
                        string.Join(",", HullLayoutJson.Array(paint, "ramp", name).Select(c => c as string));
                })));
            return groups;
        }

        static void ReadFaces(List<object> faces, Dictionary<string, object> mats, RigMeshData data,
                              bool interior, List<RigFace> output, List<RigMaterial> palette,
                              Dictionary<string, string> paintGroups = null)
        {
            var roles = HullLayoutJson.TagIds(HullLayoutJson.Child(data.WalkGeometry, "roles"), "geometry.roles");
            foreach (object raw in faces)
            {
                var f = HullLayoutJson.Object(raw, "face");
                string name = HullLayoutJson.Text(f, "mat", "face");
                var paint = HullLayoutJson.Object(HullLayoutJson.Required(mats, name, "MATS"), "MATS." + name);
                string[] hex = HullLayoutJson.Array(paint, "ramp", "MATS." + name).Select(c => c as string).ToArray();
                if (hex.Length == 0) throw new FormatException("MATS." + name + ".ramp: empty ramp");
                int offset = HullLayoutJson.Integer(paint, "off", "MATS." + name);
                int mat = palette.FindIndex(m => paintGroups == null
                    ? m.Off == offset && m.RampHex.SequenceEqual(hex)
                    : paintGroups[m.Name] == paintGroups[name]);
                if (mat < 0)
                {
                    mat = palette.Count;
                    string representative = paintGroups == null ? name : mats.Keys.First(n =>
                        paintGroups.TryGetValue(n, out string group) && group == paintGroups[name]);
                    palette.Add(new RigMaterial { Name = representative, Off = offset, RampHex = hex, Ramp = hex.Select(Color).ToArray() });
                }
                int lv = HullLayoutJson.Integer(f, "lv", "face"), ro = HullLayoutJson.Integer(f, "ro", "face");
                HullLayoutJson.Number(f, "b", "face"); HullLayoutJson.Number(f, "db", "face");
                if (!data.LevelIds.Values.Contains(lv)) throw new FormatException("face.lv: unknown tag");
                if (!roles.Values.Contains(ro)) throw new FormatException("face.ro: unknown tag");
                if (!(HullLayoutJson.Required(f, "it", "face") is bool it) || it != interior) throw new FormatException("face.it: wrong list");
                var vertices = HullLayoutJson.Array(f, "v", "face");
                if (vertices.Count < 3) throw new FormatException("face.v: requires three vertices");
                var face = new RigFace { V = vertices.Select(v =>
                    {
                        HullLayoutJson.Triple(v, "face.v"); var a = (List<object>)v;
                        return new Vector3d((double)a[0], (double)a[1], (double)a[2]);
                    }).ToArray(), Mat = mat, Level = lv, Role = ro, Interior = it,
                    LevelName = data.LevelIds.First(kv => kv.Value == lv).Key,
                    B = (double)HullLayoutJson.Required(f, "b", "face"), Db = (double)HullLayoutJson.Required(f, "db", "face") };
                if (f.TryGetValue("sg", out object sg))
                {
                    face.TexKind = sg as string == "plank" ? 1 : sg as string == "board" ? 2 : sg as string == "quilt" ? 3 :
                        throw new FormatException("face.sg: unknown generator");
                    face.TexPeriod = HullLayoutJson.Number(f, "period", "face");
                    face.Uv = HullLayoutJson.Array(f, "uv", "face").Select(v =>
                    { var a = HullLayoutJson.List(v, "face.uv"); if (a.Count != 2) throw new FormatException("face.uv: expected two coordinates"); return new Vector2(HullLayoutJson.Numeric(a[0], "face.uv"), HullLayoutJson.Numeric(a[1], "face.uv")); }).ToArray();
                    if (face.Uv.Length != face.V.Length) throw new FormatException("face.uv: must match vertices");
                }
                output.Add(face);
            }
        }

        public static void ReadGeometry(RigMeshData data, Dictionary<string, object> geometry)
        {
            if (HullLayoutJson.Text(geometry, "schema") != GeometrySchema) throw new FormatException("geometry.schema: unknown id");
            var ids = HullLayoutJson.TagIds(HullLayoutJson.Child(geometry, "ids"), "geometry.ids");
            var roles = HullLayoutJson.TagIds(HullLayoutJson.Child(geometry, "roles"), "geometry.roles");
            data.LevelIds = ids;
            var levels = new List<RigLevelRecord>();
            foreach (object item in HullLayoutJson.Array(geometry, "levels"))
            {
                var l = HullLayoutJson.Object(item, "geometry.levels");
                string id = HullLayoutJson.Text(l, "id"), p = "geometry.levels." + id;
                string kind = HullLayoutJson.Text(l, "kind", p);
                object ceiling = HullLayoutJson.Required(l, "ceil", p);
                if (!ids.ContainsKey(id)) throw new FormatException(p + ".id: unknown tag");
                if ((kind != "hard" && kind != "open") || (kind == "open") != (ceiling == null)) throw new FormatException(p + ".kind / ceil: inconsistent ceiling");
                levels.Add(new RigLevelRecord { Id = id, Tag = ids[id], DeckId = HullLayoutJson.Text(l, "deck", p),
                    SoleZ = HullLayoutJson.Number(l, "sole", p), Enclosed = kind == "hard", CeilingKind = kind,
                    CeilingZ = ceiling == null ? 0 : HullLayoutJson.Numeric(ceiling, p + ".ceil") });
            }
            data.Levels = levels;
            var cutaway = HullLayoutJson.Child(geometry, "cutaway", "geometry");
            var gate = HullLayoutJson.Child(cutaway, "gate", "geometry.cutaway");
            var ghost = HullLayoutJson.Child(cutaway, "ghost", "geometry.cutaway");
            var shows = HullLayoutJson.Child(cutaway, "shows", "geometry.cutaway");
            if (!gate.Keys.OrderBy(k => k).SequenceEqual(ghost.Keys.OrderBy(k => k)) ||
                !gate.Keys.OrderBy(k => k).SequenceEqual(shows.Keys.OrderBy(k => k))) throw new FormatException("geometry.cutaway: key sets differ");
            var sets = new List<HullMeshDef.LiftSet>();
            foreach (var pair in gate)
            {
                if (shows[pair.Key] != null && !(shows[pair.Key] is string))
                    throw new FormatException("geometry.cutaway.shows." + pair.Key + ": expected level or null");
                string shown = shows[pair.Key] as string;
                if (shown != null && !levels.Any(l => l.Id == shown && l.Enclosed)) throw new FormatException("geometry.cutaway.shows." + pair.Key + ": expected hard level");
                var entries = HullLayoutJson.List(pair.Value, "geometry.cutaway.gate." + pair.Key).Select(e => e as string).ToArray();
                var ghostEntries = HullLayoutJson.List(ghost[pair.Key], "geometry.cutaway.ghost." + pair.Key).Select(e => e as string).ToArray();
                if (ghostEntries.Any(e => e == null || (!e.Contains(":") && !entries.Contains(e))))
                    throw new FormatException("geometry.cutaway.gate." + pair.Key + ": lifts fewer whole levels than ghost");
                int mask = 0, roleMask = 0;
                var roleMasks = new int[31];
                foreach (string entry in entries)
                {
                    string[] parts = entry?.Split(':');
                    if (parts == null || parts.Length > 2 || !ids.ContainsKey(parts[0])) throw new FormatException("geometry.cutaway.gate." + pair.Key + ": unknown level");
                    if (parts.Length == 1)
                    {
                        if (parts[0] == shown) roleMask = int.MaxValue;
                        else mask |= 1 << ids[parts[0]];
                    }
                    else
                    {
                        if (!roles.ContainsKey(parts[1])) throw new FormatException("geometry.cutaway.gate." + pair.Key + ": unknown role");
                        if (parts[0] == shown) roleMask |= 1 << roles[parts[1]];
                        else roleMasks[ids[parts[0]]] |= 1 << roles[parts[1]];
                    }
                }
                sets.Add(new HullMeshDef.LiftSet { Key = pair.Key,
                    DeckId = levels.FirstOrDefault(l => l.Id == shown)?.DeckId ?? pair.Key,
                    Level = shown == null ? 0 : ids[shown], LiftLevels = mask, ShownRoles = roleMask, RoleMasks = roleMasks });
            }
            data.LiftSets = sets.ToArray();
        }

        static Color32 Color(string hex)
        {
            if (hex == null || hex.Length != 7 || hex[0] != '#') throw new FormatException("MATS.ramp: expected #rrggbb");
            return new Color32(Convert.ToByte(hex.Substring(1, 2), 16), Convert.ToByte(hex.Substring(3, 2), 16), Convert.ToByte(hex.Substring(5, 2), 16), 255);
        }
    }
}
