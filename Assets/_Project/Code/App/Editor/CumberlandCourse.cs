using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.World;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>Course geometry from the same document and JSON parser as the mown ground.</summary>
    public sealed class CumberlandCourse
    {
        public const string SourcePath = CumberlandBuilder.GroundFolder + "/plan.json";
        public const string Prefix = "station.cumberland.";
        public const string Clubhouse = Prefix + "clubhouse_door";
        public const string EdgeOut = Prefix + "east_edge_out";
        public const string EdgeIn = Prefix + "east_edge_in";
        public const float TeeBackMetres = 2.2f;
        public const float SlotGapMetres = 1.4f;
        public const float GreenStandRadiusMetres = 9f;
        public const float GreenSlotAngleDegrees = 18f;
        public const float GreenApproachMetres = 4f;
        public const float HazardClearanceMetres = 2f;
        public const float BridgeLandingMetres = 6f;
        public const float DoorClearanceMetres = 2f;

        public sealed class Hole
        {
            public int Number, Par;
            public Vector2? Tee, Green;
            public Vector2[] Line;
        }

        public sealed class Pond
        {
            public string Id;
            public Vector2 Centre, Radii;
            public float Rotation;
        }

        public readonly Hole[] Holes;
        public readonly Pond[] Ponds;
        public readonly Vector3[] Bunkers;
        public readonly Vector2[] Brook;
        public readonly float[] BrookWidths;
        public readonly float GreenRadius, TeeRadius, EastEdge;
        public readonly Vector2 LotCentre, LotSize, Bridge;
        public readonly RoutineStationEntry[] Stations;
        public readonly Vector2[] Nodes, Via;
        public readonly string[] NodeNames;
        public readonly int[] Parents, ViaStart, ViaCount;

        public static CumberlandCourse Read() => new CumberlandCourse(
            File.ReadAllText(SourcePath),
            File.ReadAllText(CumberlandBuilder.GroundFolder + "/surface-rules.json"));

        public CumberlandCourse(string json, string paintRules)
        {
            var doc = (Dictionary<string, object>)TerrainPlanSourcesJson.ParseDocument(json);
            if ((string)doc["schema"] != GroundFileIntake.CumberlandSchema)
                throw new ArgumentException("Unexpected course document.");
            var course = Obj(doc, "course");
            Holes = Rows(course, "holes").Select(h => new Hole {
                Number = (int)Num(h, "n"), Par = (int)Num(h, "par"),
                Tee = h["tee"] == null ? (Vector2?)null : Point(h["tee"]),
                Green = h["green"] == null ? (Vector2?)null : Point(h["green"]),
                Line = Points(h["line"])
            }).ToArray();
            if (Holes.Length != 6 || Holes.Select(h => h.Number).Distinct().Count() != 6 ||
                Holes.Any(h => h.Line.Length < 2)) throw new ArgumentException("Incomplete course holes.");
            GreenRadius = Num(Obj(course, "greens"), "r");
            TeeRadius = Num(Obj((Dictionary<string, object>)TerrainPlanSourcesJson.ParseDocument(paintRules), "course"), "teeRadius");
            var region = Obj(doc, "region");
            EastEdge = Point(region["centre"]).x + Point(region["size"]).x / 2f;
            var lot = Rows(Obj(doc, "lots"), "club").Single(l => (string)l["id"] == (string)course["clubhouse"]);
            if ((string)lot["faces"] != "S") throw new ArgumentException("The course door must face south.");
            LotCentre = Point(lot["at"]); LotSize = Point(lot["size"]);
            Bunkers = ((List<object>)course["bunkers"]).Select(b => {
                var a = (List<object>)b;
                return new Vector3((float)(double)a[0], (float)(double)a[1], (float)(double)a[2]);
            }).ToArray();
            var water = Obj(doc, "water");
            var pondIds = ((List<object>)course["ponds"]).Cast<string>().ToArray();
            Ponds = Rows(water, "ponds").Where(p => pondIds.Contains((string)p["id"]))
                .Select(p => new Pond { Id = (string)p["id"], Centre = Point(p["c"]),
                    Radii = Point(p["r"]), Rotation = Num(p, "rot") }).ToArray();
            var brook = Rows(water, "streams").Single(s => (string)s["id"] == "stream.cmb_brook");
            Brook = Points(brook["pts"]);
            BrookWidths = ((List<object>)brook["w"]).Select(n => (float)(double)n).ToArray();
            var cart = Rows(doc, "paths").Single(p => (string)p["id"] == (string)course["cart_path"]);
            Bridge = Crossing(Points(cart["pts"]), Brook);

            var positions = new List<Vector2>(); var names = new List<string>();
            var parents = new List<int>(); var vias = new List<Vector2>();
            var starts = new List<int>(); var counts = new List<int>();
            var stations = new List<RoutineStationEntry>();
            int AddNode(string name, Vector2 at, int parent, params Vector2[] bends)
            {
                int index = positions.Count;
                positions.Add(at); names.Add(name); parents.Add(parent);
                starts.Add(vias.Count); counts.Add(bends.Length);
                // RoutineLanes stores bends from child to parent.
                vias.AddRange(bends.Reverse());
                return index;
            }
            void AddStation(string id, Vector2 at, Vector2 facing, int node, bool hidden = false)
            {
                stations.Add(new RoutineStationEntry { Id = id, Position = at,
                    StandHeadingDegrees = Heading(facing), LaneNodeName = names[node],
                    OutOfSight = hidden, Why = "Derived from the course document." });
            }
            int Slots(Hole hole, bool tee, int node)
            {
                for (int slot = 3; slot >= 0; slot--)
                {
                    Vector2 at = Slot(hole, tee, slot);
                    node = AddNode((tee ? "tee_" : "green_") + hole.Number + "_" + (char)('a' + slot), at, node);
                    Vector2 facing = tee ? Direction(hole) : hole.Green.Value - at;
                    AddStation(SlotId(tee, hole.Number, slot), at, facing, node);
                }
                return node;
            }

            Vector2 door = LotCentre + Vector2.down * LotSize.y / 2f;
            int root = AddNode("clubhouse", door, -1);
            AddStation(Clubhouse, door, Vector2.down, root, true);
            int previous = root;
            for (int n = 1; n <= 5; n++)
            {
                Hole hole = GetHole(n);
                Vector2 tee = Slot(hole, true, 3);
                Vector2[] connector = n == 1
                    ? new[] { door + new Vector2(-LotSize.x / 2f - DoorClearanceMetres, -DoorClearanceMetres) }
                    : n == 2 ? new[] { GetHole(1).Green.Value } : Array.Empty<Vector2>();
                if (n == 4)
                {
                    Vector3 bunker = Bunkers.OrderBy(b => Vector2.Distance(hole.Tee.Value, new Vector2(b.x, b.y))).First();
                    connector = new[] { new Vector2(bunker.x, bunker.y - bunker.z - HazardClearanceMetres) };
                }
                int teeNode = AddNode("tee_" + n, tee, previous, connector);
                previous = Slots(hole, true, teeNode);
                if (hole.Green.HasValue)
                {
                    Vector2 entry = Slot(hole, false, 3);
                    var bends = hole.Line.Skip(1).Take(hole.Line.Length - 2).ToList();
                    if (n == 1)
                    {
                        bends.InsertRange(1, new[] { Bridge + Vector2.right * BridgeLandingMetres,
                            Bridge + Vector2.left * BridgeLandingMetres });
                    }
                    if (n == 2)
                    {
                        Pond west = Ponds.Single(p => p.Id == "pond.cmb_course_west");
                        float westX = west.Centre.x - west.Radii.x - HazardClearanceMetres;
                        bends.Add(new Vector2(westX, west.Centre.y - west.Radii.y - HazardClearanceMetres));
                        bends.Add(new Vector2(westX, west.Centre.y + west.Radii.y + HazardClearanceMetres));
                    }
                    bends.Add(hole.Green.Value - LastDirection(hole) * (GreenStandRadiusMetres + GreenApproachMetres));
                    bends.Add(entry + (entry - hole.Green.Value).normalized * GreenApproachMetres);
                    previous = AddNode("green_" + n, entry, previous, bends.ToArray());
                    previous = Slots(hole, false, previous);
                }
            }
            Vector2 edgeOut = AtEastEdge(GetHole(5));
            int outNode = AddNode("east_edge_out", edgeOut, previous);
            AddStation(EdgeOut, edgeOut, Direction(GetHole(5)), outNode, true);

            Hole last = GetHole(18);
            Vector2 lastEntry = Slot(last, false, 3);
            Vector2 southEast = door + new Vector2(LotSize.x / 2f + DoorClearanceMetres, -DoorClearanceMetres);
            int lastNode = AddNode("green_18", lastEntry, root, southEast,
                new Vector2(southEast.x, last.Green.Value.y));
            lastNode = Slots(last, false, lastNode);
            Vector2 edgeIn = AtEastEdge(last);
            int inNode = AddNode("east_edge_in", edgeIn, lastNode);
            AddStation(EdgeIn, edgeIn, LastDirection(last), inNode, true);

            Stations = stations.ToArray(); Nodes = positions.ToArray(); NodeNames = names.ToArray();
            Parents = parents.ToArray(); Via = vias.ToArray();
            ViaStart = starts.ToArray(); ViaCount = counts.ToArray();
        }

        public Hole GetHole(int number) => Holes.Single(h => h.Number == number);
        public RoutineStationEntry Station(string id) => Stations.Single(s => s.Id == id);
        public static string SlotId(bool tee, int hole, int slot) =>
            Prefix + (tee ? "tee_" : "green_") + hole + "_" + (char)('a' + slot);

        public Vector2 Slot(Hole hole, bool tee, int slot)
        {
            if (slot < 0 || slot > 3) throw new ArgumentOutOfRangeException(nameof(slot));
            Vector2 direction = tee ? Direction(hole) : LastDirection(hole);
            if (tee)
                return hole.Tee.Value - direction * (TeeBackMetres + (slot - 1.5f) * SlotGapMetres);
            float radians = (slot - 1.5f) * GreenSlotAngleDegrees * Mathf.Deg2Rad;
            Vector2 radial = -direction * GreenStandRadiusMetres;
            return hole.Green.Value + new Vector2(radial.x * Mathf.Cos(radians) - radial.y * Mathf.Sin(radians),
                radial.x * Mathf.Sin(radians) + radial.y * Mathf.Cos(radians));
        }

        public Vector2 AtEastEdge(Hole hole)
        {
            for (int i = 1; i < hole.Line.Length; i++)
            {
                Vector2 a = hole.Line[i - 1], b = hole.Line[i];
                if (Mathf.Min(a.x, b.x) <= EastEdge && Mathf.Max(a.x, b.x) >= EastEdge && a.x != b.x)
                    return Vector2.Lerp(a, b, (EastEdge - a.x) / (b.x - a.x));
            }
            throw new ArgumentException("Hole does not reach the east edge.");
        }

        public Vector2[] Route(string fromId, string toId)
        {
            RoutineStationEntry from = Station(fromId), to = Station(toId);
            if (from.OutOfSight && to.OutOfSight) return new[] { to.Position };
            int a = Array.IndexOf(NodeNames, from.LaneNodeName), b = Array.IndexOf(NodeNames, to.LaneNodeName);
            var nodePath = new int[Nodes.Length * 2 + 2];
            int count = RoutineLaneTree.WriteNodePath(Parents, a, b, nodePath);
            var line = new Vector2[Nodes.Length + Via.Length + 8];
            int points = RoutineLaneTree.WritePolyline(Parents, Nodes, ViaStart, ViaCount, Via, nodePath, count, line);
            return new[] { from.Position }.Concat(line.Take(points)).Concat(new[] { to.Position }).ToArray();
        }

        public float Distance(string from, string to)
        {
            Vector2[] line = Route(from, to);
            return RoutineLaneTree.PolylineLength(line, 0, line.Length);
        }

        public static Vector2 Direction(Hole hole) => (hole.Line[1] - hole.Line[0]).normalized;
        public static Vector2 LastDirection(Hole hole) => (hole.Line[hole.Line.Length - 1] - hole.Line[hole.Line.Length - 2]).normalized;
        public static float Heading(Vector2 direction) => IsoGround.BearingDegrees(direction);
        static Dictionary<string, object> Obj(Dictionary<string, object> d, string key) => (Dictionary<string, object>)d[key];
        static IEnumerable<Dictionary<string, object>> Rows(Dictionary<string, object> d, string key) => ((List<object>)d[key]).Cast<Dictionary<string, object>>();
        static float Num(Dictionary<string, object> d, string key)
        {
            float n = (float)(double)d[key];
            if (float.IsNaN(n) || float.IsInfinity(n)) throw new ArgumentException("Nonfinite course number.");
            return n;
        }
        static Vector2 Point(object value)
        {
            var a = (List<object>)value;
            if (a.Count != 2) throw new ArgumentException("Expected a course point.");
            return new Vector2((float)(double)a[0], (float)(double)a[1]);
        }
        static Vector2[] Points(object value) => ((List<object>)value).Select(Point).ToArray();
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        static Vector2 Crossing(Vector2[] path, Vector2[] brook)
        {
            for (int i = 1; i < path.Length; i++)
                for (int j = 1; j < brook.Length; j++)
                {
                    Vector2 a = path[i - 1], u = path[i] - a, b = brook[j - 1], v = brook[j] - b;
                    float denominator = Cross(u, v);
                    if (Mathf.Abs(denominator) < 1e-6f) continue;
                    float t = Cross(b - a, v) / denominator, s = Cross(b - a, u) / denominator;
                    if (t >= 0f && t <= 1f && s >= 0f && s <= 1f) return a + u * t;
                }
            throw new ArgumentException("No cart bridge crossing in the plan.");
        }
    }
}
