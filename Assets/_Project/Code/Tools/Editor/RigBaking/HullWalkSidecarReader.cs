using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    public sealed class HullWalkArea
    {
        public string Id, Cut, Kind, CutHi, CutLo;
        public Vector2[] Polygon;
        public Vector2[][] Holes;
        public float Z, ZMid;
        public HullWalkRamp Ramp;
        public Dictionary<string, object> Fields;
        public bool IsClimb => Kind == "stair" || Kind == "ladder";
        public float HeightAt(Vector2 p) => Ramp == null ? Z : Ramp.HeightAt(p);
    }

    public sealed class HullWalkRamp
    {
        public string Axis;
        public float A0, A1, Z0, Z1;
        public float HeightAt(Vector2 p) => Mathf.Lerp(Z0, Z1,
            Mathf.Clamp01(((Axis == "x" ? p.x : p.y) - A0) / (A1 - A0)));
    }

    public sealed class HullWalkRead
    {
        public string SidecarPath, RigSourcePath, Schema, Rig, ExportSymbol, Hull, Kind, RigSha256;
        public float LoaMeters, Dwl, CockpitZ, Radius, StepTolerance;
        public RigHashMatch HashMatch;
        public Dictionary<string, object> Fields, House, Anchors;
        public readonly List<HullWalkArea> Areas = new List<HullWalkArea>();
        public readonly List<string> Errors = new List<string>();
        public bool Ok => Errors.Count == 0;
    }

    /// <summary>Reads the delivered hull-walk contract. Fields without a consumer stay in Fields.</summary>
    public static class HullWalkSidecarReader
    {
        public const string Schema = "hidden-harbours/hull-walk@1";

        public static HullWalkRead Read(string json, string sidecarPath, byte[] rigBytes)
        {
            var result = new HullWalkRead { SidecarPath = sidecarPath };
            try
            {
                var root = HullLayoutJson.Object(DeckSidecarJson.Parse(json), "root");
                result.Fields = root;
                result.Hull = HullLayoutJson.Text(root, "hull");
                result.Schema = HullLayoutJson.Text(root, "schema");
                if (result.Schema != Schema) throw new FormatException("schema: unknown id '" + result.Schema + "'");
                result.Rig = HullLayoutJson.Text(root, "rig");
                result.ExportSymbol = HullLayoutJson.Text(root, "exportSymbol");
                result.Kind = HullLayoutJson.Text(root, "kind");
                if (!new[] { "open", "cabin", "ship" }.Contains(result.Kind))
                    throw new FormatException("kind: expected open, cabin or ship");
                HullLayoutJson.Text(root, "label");
                HullLayoutJson.Text(root, "authoring");
                HullLayoutJson.Text(root, "frame");
                if (HullLayoutJson.Text(root, "units") != "metres") throw new FormatException("units: expected metres");
                result.RigSha256 = HullLayoutJson.Text(root, "derivedFromRigSha256");
                if (!Regex.IsMatch(result.RigSha256, @"\A[0-9a-fA-F]{64}\z"))
                    throw new FormatException("derivedFromRigSha256: expected 64 hex characters");
                result.HashMatch = DeckSidecarReader.MatchRigHash(rigBytes, result.RigSha256, out _);
                if (result.HashMatch == RigHashMatch.None)
                    throw new FormatException("derivedFromRigSha256: rig bytes do not match");
                result.LoaMeters = HullLayoutJson.Number(root, "loa_m");
                result.Dwl = HullLayoutJson.Number(root, "DWL");
                result.CockpitZ = HullLayoutJson.Number(root, "cockpitZ");
                ReadCell(HullLayoutJson.Child(root, "cell"));
                var tags = HullLayoutJson.Child(root, "tags");
                HullLayoutJson.TagIds(HullLayoutJson.Child(tags, "lv", "tags"), "tags.lv");
                var roles = HullLayoutJson.TagIds(HullLayoutJson.Child(tags, "ro", "tags"), "tags.ro");
                foreach (object role in HullLayoutJson.Array(tags, "ghostRoles", "tags"))
                    if (!(role is string r) || !roles.ContainsKey(r)) throw new FormatException("tags.ghostRoles: unknown role");
                var walk = HullLayoutJson.Child(root, "walk");
                result.Radius = HullLayoutJson.Number(walk, "radius", "walk");
                result.StepTolerance = HullLayoutJson.Number(walk, "stepTol", "walk");
                if (result.Radius <= 0 || result.StepTolerance <= 0) throw new FormatException("walk.radius / walk.stepTol: must be positive");
                HullLayoutJson.Text(walk, "rule", "walk");
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (object item in HullLayoutJson.Array(walk, "areas", "walk"))
                {
                    var a = ReadArea(HullLayoutJson.Object(item, "walk.areas"));
                    if (!ids.Add(a.Id)) throw new FormatException("walk.areas." + a.Id + ": duplicate id");
                    result.Areas.Add(a);
                }
                if (result.Areas.Count == 0) throw new FormatException("walk.areas: must not be empty");
                if (HullLayoutJson.Required(walk, "spawn", "walk") is object spawn)
                {
                    var s = HullLayoutJson.Object(spawn, "walk.spawn");
                    HullLayoutJson.Number(s, "x", "walk.spawn");
                    HullLayoutJson.Number(s, "y", "walk.spawn");
                    if (!ids.Contains(HullLayoutJson.Text(s, "area", "walk.spawn")))
                        throw new FormatException("walk.spawn.area: unknown area");
                }
                object house = result.Kind == "open" && !root.ContainsKey("house")
                    ? null : HullLayoutJson.Required(root, "house");
                if (result.Kind == "open" && house != null) throw new FormatException("house: expected null on open hull");
                if (result.Kind != "open")
                {
                    result.House = HullLayoutJson.Child(root, "house");
                    ReadDoor(HullLayoutJson.Child(result.House, "door", "house"), "house.door");
                    HullLayoutJson.Array(result.House, "levels", "house");
                    var decks = HullLayoutJson.Child(result.House, "decks", "house");
                    foreach (var pair in decks)
                    {
                        string p = "house.decks." + pair.Key;
                        var d = HullLayoutJson.Object(pair.Value, p);
                        HullLayoutJson.Text(d, "deck", p);
                        string kind = HullLayoutJson.Text(d, "kind", p);
                        HullLayoutJson.Number(d, "soleZ", p);
                        object ceil = HullLayoutJson.Required(d, "ceilZ", p);
                        if (kind == "hard") HullLayoutJson.Numeric(ceil, p + ".ceilZ");
                        else if (kind != "open" || ceil != null) throw new FormatException(p + ".kind / ceilZ: expected open with null ceiling");
                    }
                    if (result.House.TryGetValue("doors", out object doors))
                        foreach (object door in HullLayoutJson.List(doors, "house.doors"))
                            ReadDoor(HullLayoutJson.Object(door, "house.doors"), "house.doors");
                }
                result.Anchors = HullLayoutJson.Child(root, "anchors");
                if (result.Kind != "open" || result.Anchors.ContainsKey("helm"))
                    HullLayoutJson.Point(HullLayoutJson.Required(result.Anchors, "helm", "anchors"), "anchors.helm");
                foreach (object item in HullLayoutJson.Array(root, "interact"))
                {
                    var i = HullLayoutJson.Object(item, "interact");
                    string p = "interact." + HullLayoutJson.Text(i, "id", "interact");
                    HullLayoutJson.Text(i, "action", p);
                    HullLayoutJson.Text(i, "label", p);
                    HullLayoutJson.Text(i, "prompt", p);
                    HullLayoutJson.Point(HullLayoutJson.Required(i, "at", p), p + ".at");
                    HullLayoutJson.Point(HullLayoutJson.Required(i, "reach", p), p + ".reach");
                    object area = HullLayoutJson.Required(i, "area", p);
                    if (area != null && (!(area is string name) || !ids.Contains(name)))
                        throw new FormatException(p + ".area: unknown area");
                    if (area == null) HullLayoutJson.Text(i, "offWalk", p);
                }
                foreach (object item in HullLayoutJson.Array(root, "cleats"))
                {
                    var c = HullLayoutJson.Object(item, "cleats");
                    string p = "cleats." + HullLayoutJson.Text(c, "id", "cleats");
                    HullLayoutJson.Text(c, "type", p);
                    HullLayoutJson.Triple(HullLayoutJson.Required(c, "pos", p), p + ".pos");
                }
                var excluded = HullLayoutJson.Array(root, "_excluded");
                foreach (object item in excluded)
                {
                    var e = HullLayoutJson.Object(item, "_excluded");
                    HullLayoutJson.Text(e, "id", "_excluded");
                    HullLayoutJson.Text(e, "of", "_excluded");
                    HullLayoutJson.Text(e, "reason", "_excluded");
                }
                var washboards = HullLayoutJson.Array(root, "washboards");
                if (washboards.Count == 0 && !excluded.Any(e => (string)DeckSidecarJson.Member(e, "of") == "washboards"))
                    throw new FormatException("washboards: empty without _excluded entry");
                foreach (object item in washboards)
                {
                    var w = HullLayoutJson.Object(item, "washboards");
                    string side = HullLayoutJson.Text(w, "side", "washboards");
                    if (side != "port" && side != "starboard") throw new FormatException("washboards.side: unknown side");
                    if (HullLayoutJson.Number(w, "width_m", "washboards") <= 0) throw new FormatException("washboards.width_m: must be positive");
                    var edge = HullLayoutJson.Array(w, "outer_edge", "washboards");
                    if (edge.Count < 2) throw new FormatException("washboards.outer_edge: requires two points");
                    foreach (object point in edge) HullLayoutJson.Triple(point, "washboards.outer_edge");
                }
                var rock = HullLayoutJson.Child(root, "rock");
                foreach (string key in new[] { "frames", "rollA", "pitchA", "heaveA", "period" })
                    HullLayoutJson.Number(rock, key, "rock");
                var qa = HullLayoutJson.Child(root, "qa");
                HullLayoutJson.Child(qa, "fitCheck", "qa");
                HullLayoutJson.Child(qa, "character", "qa");
            }
            catch (FormatException e)
            {
                result.Errors.Add((result.Hull ?? sidecarPath) + ": " + e.Message);
            }
            return result;
        }

        static void ReadCell(Dictionary<string, object> cell)
        {
            foreach (string key in new[] { "W", "H", "pxPerM", "defaultElev" }) HullLayoutJson.Number(cell, key, "cell");
            var pivot = HullLayoutJson.Child(cell, "pivot", "cell");
            HullLayoutJson.Number(pivot, "x", "cell.pivot");
            HullLayoutJson.Number(pivot, "y", "cell.pivot");
            HullLayoutJson.Array(cell, "order", "cell");
        }

        static HullWalkArea ReadArea(Dictionary<string, object> fields)
        {
            string id = HullLayoutJson.Text(fields, "id", "walk.areas"), p = "walk.areas." + id;
            var a = new HullWalkArea
            {
                Id = id, Fields = fields, Cut = HullLayoutJson.Text(fields, "cut", p),
                Polygon = HullLayoutJson.Polygon(HullLayoutJson.Required(fields, "poly", p), p + ".poly"),
                Holes = HullLayoutJson.Array(fields, "holes", p).Select(h => HullLayoutJson.Polygon(h, p + ".holes")).ToArray(),
                Kind = DeckSidecarJson.String(DeckSidecarJson.Member(fields, "kind")),
                CutHi = DeckSidecarJson.String(DeckSidecarJson.Member(fields, "cutHi")),
                CutLo = DeckSidecarJson.String(DeckSidecarJson.Member(fields, "cutLo")),
            };
            bool flat = fields.ContainsKey("z"), ramp = fields.ContainsKey("ramp");
            if (flat == ramp) throw new FormatException(p + ".z / ramp: requires exactly one");
            if (flat) a.Z = HullLayoutJson.Number(fields, "z", p);
            else
            {
                var r = HullLayoutJson.Child(fields, "ramp", p);
                string rp = p + ".ramp";
                a.Ramp = new HullWalkRamp { Axis = HullLayoutJson.Text(r, "axis", rp),
                    A0 = HullLayoutJson.Number(r, "a0", rp), A1 = HullLayoutJson.Number(r, "a1", rp),
                    Z0 = HullLayoutJson.Number(r, "z0", rp), Z1 = HullLayoutJson.Number(r, "z1", rp) };
                if (a.Ramp.Axis != "x" && a.Ramp.Axis != "y") throw new FormatException(rp + ".axis: expected x or y");
                if (a.Ramp.A0 == a.Ramp.A1) throw new FormatException(rp + ".a1: equals a0");
            }
            if (a.IsClimb || fields.ContainsKey("zMid") || fields.ContainsKey("cutHi") || fields.ContainsKey("cutLo"))
            {
                if (!a.IsClimb) throw new FormatException(p + ".kind: expected stair or ladder");
                if (a.Ramp == null) throw new FormatException(p + ".ramp: required for climb");
                a.ZMid = HullLayoutJson.Number(fields, "zMid", p);
                if (a.ZMid <= Mathf.Min(a.Ramp.Z0, a.Ramp.Z1) || a.ZMid >= Mathf.Max(a.Ramp.Z0, a.Ramp.Z1))
                    throw new FormatException(p + ".zMid: must lie between ramp heights");
                if (a.CutHi == null && a.CutLo == null) throw new FormatException(p + ".cutHi / cutLo: requires a cut key");
            }
            return a;
        }

        static void ReadDoor(Dictionary<string, object> door, string p)
        {
            HullLayoutJson.Text(door, "id", p);
            if (door.TryGetValue("open", out object open) && open is bool b && b &&
                door.TryGetValue("leaf", out object leaf) && leaf == null)
            { HullLayoutJson.Text(door, "reason", p); return; }
            HullLayoutJson.Text(door, "leaf", p);
            HullLayoutJson.Text(door, "lv", p);
            HullLayoutJson.Text(door, "ro", p);
            HullLayoutJson.Text(door, "face", p);
            HullLayoutJson.Text(door, "opensTo", p);
            var clear = HullLayoutJson.Child(door, "clear", p);
            HullLayoutJson.Number(clear, "width", p + ".clear");
            HullLayoutJson.Number(clear, "height", p + ".clear");
            foreach (string k in new[] { "y", "x0", "x1", "z0", "z1", "sillZ", "clearAt" }) HullLayoutJson.Number(door, k, p);
            string kind = HullLayoutJson.Text(door, "kind", p);
            if (kind == "slide") HullLayoutJson.Number(door, "travel", p);
            else if (kind == "hinge")
            {
                var hinge = HullLayoutJson.Child(door, "hinge", p);
                foreach (string k in new[] { "x", "y", "z0", "z1" }) HullLayoutJson.Number(hinge, k, p + ".hinge");
                HullLayoutJson.Number(door, "maxDeg", p);
                HullLayoutJson.Text(door, "swings", p);
                HullLayoutJson.Polygon(HullLayoutJson.Required(door, "keepClear", p), p + ".keepClear", false);
            }
            else throw new FormatException(p + ".kind: expected slide or hinge");
        }
    }

    /// <summary>Strict JSON field checks shared by the walk and geometry readers.</summary>
    public static class HullLayoutJson
    {
        public static object Required(Dictionary<string, object> map, string key, string path = "") =>
            map.TryGetValue(key, out object value) ? value : throw new FormatException(Path(path, key) + ": required field missing");
        static string Path(string path, string key) => path.Length == 0 ? key : path + "." + key;
        public static Dictionary<string, object> Object(object value, string path) =>
            value as Dictionary<string, object> ?? throw new FormatException(path + ": expected object");
        public static Dictionary<string, object> Child(Dictionary<string, object> map, string key, string path = "") =>
            Object(Required(map, key, path), Path(path, key));
        public static List<object> List(object value, string path) =>
            value as List<object> ?? throw new FormatException(path + ": expected array");
        public static List<object> Array(Dictionary<string, object> map, string key, string path = "") =>
            List(Required(map, key, path), Path(path, key));
        public static string Text(Dictionary<string, object> map, string key, string path = "") =>
            Required(map, key, path) is string s && s.Length > 0 ? s : throw new FormatException(Path(path, key) + ": expected nonempty string");
        public static float Numeric(object value, string path) => value is double n && !double.IsNaN(n) && !double.IsInfinity(n) && Math.Abs(n) <= float.MaxValue
            ? (float)n : throw new FormatException(path + ": expected finite number");
        public static int Integer(Dictionary<string, object> map, string key, string path = "")
        {
            float n = Number(map, key, path);
            if (n != (int)n) throw new FormatException(Path(path, key) + ": expected integer");
            return (int)n;
        }
        public static float Number(Dictionary<string, object> map, string key, string path = "") => Numeric(Required(map, key, path), Path(path, key));
        public static Vector3 Point(object value, string path)
        {
            var p = Object(value, path);
            return new Vector3(Number(p, "x", path), Number(p, "y", path), Number(p, "z", path));
        }
        public static Vector3 Triple(object value, string path)
        {
            var p = List(value, path);
            if (p.Count != 3) throw new FormatException(path + ": expected three coordinates");
            return new Vector3(Numeric(p[0], path), Numeric(p[1], path), Numeric(p[2], path));
        }
        public static Vector2[] Polygon(object value, string path, bool ccw = true)
        {
            var list = List(value, path);
            if (list.Count < 3) throw new FormatException(path + ": requires three points");
            var points = new Vector2[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                var p = List(list[i], path);
                if (p.Count != 2) throw new FormatException(path + ": expected two coordinates");
                points[i] = new Vector2(Numeric(p[0], path), Numeric(p[1], path));
            }
            double area = 0;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                area += points[j].x * (double)points[i].y - points[i].x * (double)points[j].y;
            if (ccw && area <= 0) throw new FormatException(path + ": expected counter-clockwise polygon");
            return points;
        }
        public static Dictionary<string, int> TagIds(Dictionary<string, object> map, string path)
        {
            var ids = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var kv in map)
            {
                float n = Numeric(kv.Value, path + "." + kv.Key);
                if (n < 0 || n > 30 || n != (int)n || ids.ContainsValue((int)n))
                    throw new FormatException(path + "." + kv.Key + ": expected distinct integer in 0..30");
                ids.Add(kv.Key, (int)n);
            }
            if (ids.Count == 0) throw new FormatException(path + ": empty tag table");
            return ids;
        }
    }
}
