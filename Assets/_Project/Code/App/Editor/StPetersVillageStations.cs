#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.World;
using UnityEngine;
using Json = HiddenHarbours.App.Editor.KeySceneImport.Json;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.App.Editor
{
    public static class StPetersVillageStations
    {
        public const string DataPath = "Assets/_Project/Data/Terrain/StPetersVillageStations.json";
        public sealed class Placement
        {
            public string Id, Lot;
            public Vector2 Candidate, Position;
            public bool Fallback, Street;
            public float DoorWalkMetres;
        }

        public sealed class Area
        {
            public string Lot;
            public GroundPolygon Polygon;
        }

        public sealed class Ground
        {
            public float X0, Y1, Mpp, Min, Max;
            public TerrainPlanPng.Image Height, Path;
            public int X(Vector2 p) => (int)Math.Floor((p.x - X0) / Mpp);
            public int Y(Vector2 p) => (int)Math.Floor((Y1 - p.y) / Mpp);
            public float Elevation(Vector2 p)
            {
                int x = X(p), y = Y(p);
                if (x < 0 || y < 0 || x >= Height.Width || y >= Height.Height) throw new Refusal("Station is outside the height map.");
                return Min + (Max - Min) * Height.Samples[y * Height.Width + x] / 65535f;
            }
            public bool OnOwnPaint(Vector2 p, Vector2[] walk, float width)
            {
                int x = X(p), y = Y(p);
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int xx = x + dx, yy = y + dy;
                    if (xx < 0 || yy < 0 || xx >= Path.Width || yy >= Path.Height) continue;
                    if (Path.Samples[(yy * Path.Width + xx) * Path.Channels] == 0) continue;
                    Vector2 centre = new Vector2(X0 + (xx + .5f) * Mpp, Y1 - (yy + .5f) * Mpp);
                    for (int i = 0; i + 1 < walk.Length; i++)
                        if (Distance(centre, Project(centre, walk[i], walk[i + 1])) <= width * .5f) return true;
                }
                return false;
            }
        }

        public static bool IsStreet(string id) => id == StPetersRoutines.StationStoreCounter ||
            id == StPetersRoutines.StationStoreCustomerA || id == StPetersRoutines.StationStoreCustomerB || id == StPetersRoutines.StationStoreCustomerC;

        public static Vector2 InDoorFrame(Vector2 point, Vector2 oldDoor, Vector2 oldNormal, Vector2 newDoor, Vector2 newNormal)
        {
            Vector2 delta = point - oldDoor;
            delta.y /= SpriteLightMath.GroundDepthScale;
            float outward = Vector2.Dot(delta, oldNormal), across = Vector2.Dot(delta, new Vector2(-oldNormal.y, oldNormal.x));
            Vector2 next = newNormal * outward + new Vector2(-newNormal.y, newNormal.x) * across;
            next.y *= SpriteLightMath.GroundDepthScale;
            return newDoor + next;
        }

        static Vector2 Flat(Vector2 p) => new Vector2(p.x, p.y / SpriteLightMath.GroundDepthScale);
        static float Distance(Vector2 a, Vector2 b) => (Flat(a) - Flat(b)).magnitude;
        static Vector2 Project(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 d = Flat(b - a);
            float t = d.sqrMagnitude == 0 ? 0 : Mathf.Clamp01(Vector2.Dot(Flat(p - a), d) / d.sqrMagnitude);
            return a + (b - a) * t;
        }
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        public static Placement Resolve(string id, string lot, Vector2 candidate, Vector2[] walk, float width,
            IReadOnlyList<Area> areas, IReadOnlyList<GroundPolygon> rigs, Ground ground)
        {
            bool street = IsStreet(id);
            bool Legal(Vector2 p) => ground.Elevation(p) > StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude &&
                !rigs.Any(r => r.Contains(p)) && !areas.Any(a => (street || a.Lot != lot) && a.Polygon.Contains(p)) &&
                (street || (areas.Any(a => a.Lot == lot && a.Polygon.Contains(p)) && ground.OnOwnPaint(p, walk, width)));
            var result = new Placement { Id = id, Lot = lot, Candidate = candidate, Position = candidate, Street = street };
            if (Legal(candidate)) return result;
            if (street) throw new Refusal("Street station fails ground, full-rig or lot checks: " + id);
            if (walk == null || walk.Length < 2) throw new Refusal("Station has no own door walk: " + id);
            var boundaries = areas.Select(a => a.Polygon).Concat(rigs).ToArray();
            var choices = new List<(Vector2 Point, float Along)>();
            var witnesses = new List<Vector2>();
            float along = 0;
            for (int i = walk.Length - 1; i > 0; i--)
            {
                Vector2 a = walk[i], b = walk[i - 1], d = b - a;
                var cuts = new List<float> { 0, 1 };
                void Add(float t) { if (t > 0 && t < 1) cuts.Add(t); }
                foreach (var polygon in boundaries) for (int j = 0; j < polygon.Count; j++)
                {
                    polygon.Edge(j, out Vector2 c, out Vector2 e);
                    float denominator = Cross(d, e - c);
                    if (Math.Abs(denominator) < 1e-12f) continue;
                    float t = Cross(c - a, e - c) / denominator, u = Cross(c - a, d) / denominator;
                    if (u >= 0 && u <= 1) Add(t);
                }
                if (d.x != 0) for (int x = Math.Min(ground.X(a), ground.X(b)); x <= Math.Max(ground.X(a), ground.X(b)) + 1; x++) Add((ground.X0 + x * ground.Mpp - a.x) / d.x);
                if (d.y != 0) for (int y = Math.Min(ground.Y(a), ground.Y(b)); y <= Math.Max(ground.Y(a), ground.Y(b)) + 1; y++) Add((ground.Y1 - y * ground.Mpp - a.y) / d.y);
                cuts = cuts.Distinct().OrderBy(t => t).ToList();
                float length = Distance(a, b);
                foreach (float t in cuts) choices.Add((a + d * t, along + length * t));
                for (int j = 0; j + 1 < cuts.Count; j++)
                {
                    Vector2 lo = a + d * cuts[j], hi = a + d * cuts[j + 1];
                    Vector2 projected = Project(candidate, lo, hi);
                    choices.Add((projected, along + Distance(a, projected)));
                    witnesses.Add((lo + hi) * .5f);
                }
                along += length;
            }
            var allowed = choices.Where(c => Legal(c.Point)).OrderBy(c => Distance(candidate, c.Point)).ThenBy(c => c.Along).ToArray();
            if (allowed.Length == 0) throw new Refusal("No passing point on own door walk: " + id);
            var best = allowed[0];
            if (witnesses.Any(p => Legal(p) && Distance(candidate, p) < Distance(candidate, best.Point)))
                throw new Refusal("Own-walk legal interval has no attained nearest endpoint: " + id);
            result.Position = best.Point; result.Fallback = true; result.DoorWalkMetres = best.Along;
            return result;
        }

        static Vector2 Pair(Json row) => new Vector2(row.Items[0].Float("x"), row.Items[1].Float("y"));
        static Vector2 V2(string yaml) => ParseVec3(yaml.Replace("}", ", z: 0}"));
        static Vector2[] Points(Doc doc, string field)
        {
            int at = Array.IndexOf(doc.Lines, "  " + field + ":");
            if (at < 0) throw new Refusal("Missing station geometry: " + field);
            return doc.Lines.Skip(at + 1).TakeWhile(l => l.StartsWith("  - {x:", StringComparison.Ordinal)).Select(l => V2(l.Substring(4))).ToArray();
        }

        public static Dictionary<string, Placement> Read()
        {
            var data = Json.Parse(File.ReadAllText(DataPath), DataPath);
            if (data.Get("format").Text != "hidden-harbours.village-station-frames/1") throw new Refusal("Unknown station frame format.");
            string folder = StPetersTerrainPlan.VillageFolder;
            var lots = Directory.GetFiles(folder, "lot.*.asset").Select(p => SceneYaml.Parse(File.ReadAllText(p)).Docs.Single())
                .ToDictionary(d => d.Field("Id").Trim('"').Split('.').Last(), StringComparer.Ordinal);
            var areas = new List<Area>();
            foreach (var pair in lots) areas.Add(new Area { Lot = pair.Key, Polygon = new GroundPolygon(Points(pair.Value, "Outline")) });
            foreach (string path in Directory.GetFiles(folder, "yard.*.asset"))
            {
                var doc = SceneYaml.Parse(File.ReadAllText(path)).Docs.Single();
                areas.Add(new Area { Lot = doc.Field("LotId").Trim('"').Split('.').Last(), Polygon = new GroundPolygon(Points(doc, "Outline")) });
            }
            foreach (var row in data.Get("heldYards").Items)
                areas.Add(new Area { Lot = row.Get("id").Text, Polygon = new GroundPolygon(row.Get("points").Items.Select(Pair).ToArray()) });
            var rigs = new List<GroundPolygon>();
            foreach (var row in data.Get("fullBounds").Items)
            {
                string key = row.Get("lot").Text;
                Vector2 p = key == "red_saltbox" ? (Vector2)StPetersBuilder.RedSaltboxPos : key == "white_farmhouse" ? (Vector2)StPetersBuilder.WhiteFarmhousePos :
                    key == "post_office" ? (Vector2)StPetersShops.PostOfficePos : V2(lots[key].Field("Position"));
                var b = row.Get("local").Items.Select(v => v.Float(key)).ToArray();
                rigs.Add(new GroundPolygon(new[] { p + new Vector2(b[0], b[1]), p + new Vector2(b[2], b[1]), p + new Vector2(b[2], b[3]), p + new Vector2(b[0], b[3]) }));
            }
            var manifest = Json.Parse(File.ReadAllText("Assets/_Project/Data/Terrain/StPetersPlan.manifest.json"), "terrain manifest");
            var grid = manifest.Get("grid");
            string Map(string role) => manifest.Get("maps").Items.Single(r => r.Get("role").Text == role).Get("path").Text;
            var ground = new Ground { X0 = grid.Get("x0").Float("x0"), Y1 = grid.Get("y1").Float("y1"), Mpp = grid.Get("mpp").Float("mpp"),
                Min = manifest.Get("heightRange").Items[0].Float("min"), Max = manifest.Get("heightRange").Items[1].Float("max"),
                Height = TerrainPlanPng.ReadFile(Map("height")), Path = TerrainPlanPng.ReadFile(Map("splatF")) };
            if (ground.Height.BitDepth != 16 || ground.Height.Channels != 1 || ground.Path.BitDepth != 8 || ground.Path.Channels != 4 ||
                ground.Height.Width != ground.Path.Width || ground.Height.Height != ground.Path.Height) throw new Refusal("Station maps have incompatible grids.");
            var output = new Dictionary<string, Placement>(StringComparer.Ordinal);
            foreach (var frame in data.Get("frames").Items)
            {
                string key = frame.Get("lot").Text;
                var lot = lots[key];
                if (lot.Field("DoorFaces").Trim('"') != "S") throw new Refusal("Active station frame needs its south-facing door: " + key);
                Vector2 door = V2(lot.Field("Position")) + V2(lot.Field("DoorOffset"));
                var walk = SceneYaml.Parse(File.ReadAllText(folder + "/" + lot.Field("WalkId").Trim('"') + ".asset")).Docs.Single();
                foreach (var station in frame.Get("stations").Items)
                {
                    string id = station.Get("id").Text;
                    Vector2 candidate = InDoorFrame(Pair(station.Get("position")), Pair(frame.Get("door")), Pair(frame.Get("normal")), door, Vector2.down);
                    output.Add(id, id == "GeneralStoreCounter" ? new Placement { Id = id, Lot = key, Candidate = candidate, Position = candidate } :
                        Resolve(id, key, candidate, Points(walk, "Points"), ParseFloat(walk.Field("WidthMetres")), areas, rigs, ground));
                }
            }
            return output;
        }

        static Dictionary<string, Placement> _current;
        public static IReadOnlyDictionary<string, Placement> Current => _current ?? (_current = Read());
        public static void Invalidate() => _current = null;
        public static bool TryDooryard(Vector3 site, out Vector2 position)
        {
            string id = site == StPetersBuilder.GeneralStorePos ? StPetersRoutines.StationStoreCounter :
                site == StPetersBuilder.SchoolPos ? StPetersRoutines.LaneSchool :
                site == StPetersBuilder.SageCottagePos ? StPetersRoutines.StationSageDooryard : null;
            position = id == null ? Vector2.zero : Current[id].Position;
            return id != null;
        }
    }
}
#endif
