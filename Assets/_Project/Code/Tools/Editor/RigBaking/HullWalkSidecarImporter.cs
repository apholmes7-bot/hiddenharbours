using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>Builds the existing deck, room and helm data from a validated walk and its rig.</summary>
    public sealed class HullWalkImportResult
    {
        public readonly List<string> Refusals = new List<string>();
        public bool Ok => Refusals.Count == 0;
    }

    public static class HullWalkSidecarImporter
    {
        public const string SidecarFolder = "docs/art/rigs/gameplay";

        public static string[] Files(string root) => Directory.Exists(Path.Combine(root, SidecarFolder))
            ? Directory.GetFiles(Path.Combine(root, SidecarFolder), "*.walk.json").OrderBy(f => f, StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();

        public static HullWalkImportResult Import(RigMeshData rig, string hullStem, HullMeshDef mesh,
                                                   IEnumerable<BoatVisualDef> visuals)
        {
            if (rig.Walk == null) throw new ArgumentException("walk: required", nameof(rig));
            var deck = ScriptableObject.CreateInstance<BoatDeckDef>();
            var room = ScriptableObject.CreateInstance<BoatInteriorDef>();
            try
            {
                string deckPath = DeckSidecarImporter.DeckAssetPath(hullStem);
                string stem = DeckSidecarImporter.StripRigSuffix(hullStem);
                string roomPath = "Assets/_Project/Data/Boats/Interiors/" + char.ToUpperInvariant(stem[0]) + stem.Substring(1) + ".asset";
                var priorRoom = AssetDatabase.LoadAssetAtPath<BoatInteriorDef>(roomPath);
                if (priorRoom != null) EditorUtility.CopySerialized(priorRoom, room);
                HullWalkImportResult result = Populate(rig.Walk, rig, hullStem, deck, room);
                if (!result.Ok) return result;
                var savedDeck = Save(deck, deckPath);
                var savedRoom = rig.Walk.House != null ? Save(room, roomPath) : null;
                mesh.LayoutSchema = rig.Walk.Schema;
                mesh.LiftSets = rig.LiftSets;
                EditorUtility.SetDirty(mesh);
                foreach (BoatVisualDef visual in visuals)
                {
                    // Preserve measured room metadata even when a held room has no enterable level.
                    if (savedRoom != null)
                    {
                        if (visual.Interior != null && visual.Interior != savedRoom)
                        {
                            result.Refusals.Add(hullStem + ": visual '" + visual.name +
                                "': Interior references a different interior asset ('" + visual.Interior.Id + "')");
                            continue;
                        }
                        visual.Interior = savedRoom;
                    }
                    visual.Deck = savedDeck;
                    EditorUtility.SetDirty(visual);
                }
                AssetDatabase.SaveAssets();
                return result;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(deck);
                UnityEngine.Object.DestroyImmediate(room);
            }
        }

        static T Save<T>(T source, string path) where T : ScriptableObject
        {
            var target = AssetDatabase.LoadAssetAtPath<T>(path);
            if (target == null && File.Exists(path)) throw new IOException(path + ": existing asset did not load");
            if (target == null)
            {
                string directory = Path.GetDirectoryName(path).Replace('\\', '/');
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
                target = UnityEngine.Object.Instantiate(source);
                AssetDatabase.CreateAsset(target, path);
            }
            else EditorUtility.CopySerialized(source, target);
            EditorUtility.SetDirty(target);
            return target;
        }

        public static HullWalkImportResult Populate(HullWalkRead walk, RigMeshData rig, string hullStem,
                                    BoatDeckDef deck, BoatInteriorDef interior)
        {
            var result = new HullWalkImportResult();
            if (!walk.Ok) throw new FormatException(string.Join("; ", walk.Errors));
            if (rig.Walk == null || rig.Walk.Hull != walk.Hull) throw new FormatException(walk.Hull + ": geometry belongs to another hull");
            var cuts = rig.LiftSets.GroupBy(c => c.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            foreach (HullWalkArea a in walk.Areas)
                foreach (string key in new[] { a.Cut, a.CutHi, a.CutLo }.Where(k => k != null))
                    if (!cuts.ContainsKey(key)) throw new FormatException(walk.Hull + ": walk.areas." + a.Id + ".cut: unknown key " + key);
            string suffix = DeckSidecarImporter.SnakeCase(DeckSidecarImporter.StripRigSuffix(hullStem));
            deck.Id = "deck." + suffix;
            deck.SourceSidecar = walk.SidecarPath;
            deck.SourceRig = walk.RigSourcePath ?? "docs/art/rigs/" + walk.Rig;
            deck.DerivedFromRigSha256 = walk.RigSha256;
            deck.LoaMeters = walk.LoaMeters;
            deck.HasHelmStation = walk.Anchors.ContainsKey("helm");
            deck.HelmStationLocalMeters = deck.HasHelmStation ? HullLayoutJson.Point(walk.Anchors["helm"], "anchors.helm") : Vector3.zero;
            deck.HelmStationSource = deck.HasHelmStation ? "anchors.helm" : "";
            var deckAreas = new List<DeckArea>();
            foreach (HullWalkArea area in walk.Areas)
                if (!area.IsClimb && cuts[area.Cut].Level == 0) deckAreas.AddRange(DeckAreas(area));
            foreach (object raw in HullLayoutJson.Array(walk.Fields, "washboards"))
            {
                var w = HullLayoutJson.Object(raw, "washboards");
                string side = HullLayoutJson.Text(w, "side");
                float width = HullLayoutJson.Number(w, "width_m"), direction = side == "port" ? 1 : -1;
                Vector3[] edge = HullLayoutJson.Array(w, "outer_edge").Select(v => HullLayoutJson.Triple(v, "washboards.outer_edge")).ToArray();
                var polygon = edge.Concat(edge.Reverse().Select(v => v + new Vector3(direction * width, 0, 0))).ToArray();
                deckAreas.Add(DeckArea.From("washboard_" + side, DeckAreaKind.Washboard, polygon));
            }
            deck.Areas = deckAreas.ToArray();
            deck.Cleats = HullLayoutJson.Array(walk.Fields, "cleats").Select(raw =>
            {
                var c = HullLayoutJson.Object(raw, "cleats");
                return new DeckCleat { Id = HullLayoutJson.Text(c, "id"), Type = HullLayoutJson.Text(c, "type"),
                    PositionMeters = HullLayoutJson.Triple(c["pos"], "cleats.pos") };
            }).ToArray();
            var deckPoints = deck.Areas.Where(a => a.Kind == DeckAreaKind.Deck).SelectMany(a => a.Outline).ToArray();
            Vector4 bounds = DeckAreaMath.BoundsOf(deckPoints);
            deck.WalkCenter = new Vector2((bounds.x + bounds.z) / 2, (bounds.y + bounds.w) / 2);
            deck.WalkHalfExtents = new Vector2((bounds.z - bounds.x) / 2, (bounds.w - bounds.y) / 2);

            interior.Id = "interior." + suffix;
            interior.SourceSidecar = walk.SidecarPath;
            interior.SourceHullRig = interior.SourceInteriorRig = deck.SourceRig;
            interior.HullRigSha256 = interior.InteriorRigSha256 = walk.RigSha256;
            interior.FitsHulls = new[] { hullStem };
            interior.RidesHullRock = true;
            var cell = HullLayoutJson.Child(walk.Fields, "cell");
            var pivot = HullLayoutJson.Child(cell, "pivot");
            interior.PixelsPerMetre = (int)HullLayoutJson.Number(cell, "pxPerM");
            interior.CellPixels = new Vector2Int((int)HullLayoutJson.Number(cell, "W"), (int)HullLayoutJson.Number(cell, "H"));
            interior.PivotPixels = new Vector2Int((int)HullLayoutJson.Number(pivot, "x"), (int)HullLayoutJson.Number(pivot, "y"));
            var levels = new List<BoatInteriorLevel>();
            var levelForArea = new Dictionary<string, string>(StringComparer.Ordinal);
            var indoor = walk.Areas.Where(a => !a.IsClimb && cuts[a.Cut].Level > 0).ToArray();
            foreach (var group in indoor.GroupBy(a => (a.Cut, a.Z)))
            {
                if (group.Any(a => a.Ramp != null)) throw new FormatException(walk.Hull + ": indoor ramp requires a climb kind");
                var set = cuts[group.Key.Cut];
                RigLevelRecord declared = rig.Levels.First(l => l.Tag == set.Level);
                string id = Math.Abs(group.Key.Z - declared.SoleZ) < 0.0001 ? declared.DeckId : group.First().Id;
                Vector2[][] rings = HullWalkPolygonUnion.Union(group);
                var holes = rings.Skip(1).ToArray();
                levels.Add(new BoatInteriorLevel { Id = id, SoleZMeters = group.Key.Z, Outline = rings[0],
                    Obstructions = holes.Select((h, i) => new BoatInteriorObstruction { Id = id + "_hole_" + i,
                        Footprint = h, Treatment = "wall" }).ToArray() });
                foreach (HullWalkArea a in group) levelForArea.Add(a.Id, id);
                if (id != declared.DeckId && !rig.LiftSets.Any(c => c.DeckId == id))
                {
                    var copy = set; copy.DeckId = id;
                    rig.LiftSets = rig.LiftSets.Concat(new[] { copy }).ToArray();
                }
            }
            interior.Levels = levels.ToArray();
            interior.FootprintOutline = levels.Count == 0 ? Array.Empty<Vector2>() : HullWalkPolygonUnion.Union(levels.Select(l => l.Outline))[0];
            interior.Door = walk.House == null ? new BoatInteriorDoor() : Door(HullLayoutJson.Child(walk.House, "door"), walk, result);
            interior.AdditionalDoors = walk.House != null && walk.House.TryGetValue("doors", out object doors)
                ? HullLayoutJson.List(doors, "house.doors").Select(d => HullLayoutJson.Object(d, "house.doors"))
                    .Where(d => HullLayoutJson.Text(d, "id") != interior.Door?.Id).Select(d => Door(d, walk, result)).Where(d => d != null).ToArray()
                : Array.Empty<BoatInteriorDoor>();
            interior.Anchors = HullLayoutJson.Array(walk.Fields, "interact").Select(raw =>
            {
                var i = HullLayoutJson.Object(raw, "interact");
                return new BoatInteriorAnchor { Id = HullLayoutJson.Text(i, "id"), Action = HullLayoutJson.Text(i, "action"),
                    ReachPoint = HullLayoutJson.Point(i["reach"], "interact.reach") };
            }).ToArray();
            interior.Routes = walk.Areas.Where(a => a.IsClimb).Select(a => Route(a, walk, cuts, levelForArea, deck, interior)).ToArray();
            return result;
        }

        static BoatInteriorDoor Door(Dictionary<string, object> d, HullWalkRead walk, HullWalkImportResult result)
        {
            string id = HullLayoutJson.Text(d, "id");
            if (d.TryGetValue("leaf", out object leaf) && leaf == null)
            {
                result.Refusals.Add(walk.Hull + ": house.doors." + id +
                    ": leafless doorway: not imported until batch 1 (needs #916's door runtime)");
                return null;
            }
            float radius = walk.Radius;
            float x0 = HullLayoutJson.Number(d, "x0"), x1 = HullLayoutJson.Number(d, "x1"), y = HullLayoutJson.Number(d, "y");
            bool hinge = HullLayoutJson.Text(d, "kind") == "hinge";
            return new BoatInteriorDoor { Id = id, Side = HullLayoutJson.Text(d, "face"),
                ClearWidthMeters = HullLayoutJson.Number(HullLayoutJson.Child(d, "clear"), "width"),
                ClearHeightMeters = HullLayoutJson.Number(HullLayoutJson.Child(d, "clear"), "height"),
                ThresholdPoint = new Vector3((x0 + x1) / 2, y, HullLayoutJson.Number(d, "sillZ")),
                Mechanism = hinge ? BoatInteriorDoorMechanism.Hinged : BoatInteriorDoorMechanism.Sliding,
                KeepClear = hinge ? HullLayoutJson.Polygon(d["keepClear"], "house.door.keepClear", false) :
                    new[] { new Vector2(x0, y - 2 * radius), new Vector2(x1, y - 2 * radius), new Vector2(x1, y + 2 * radius), new Vector2(x0, y + 2 * radius) } };
        }

        static BoatInteriorRoute Route(HullWalkArea area, HullWalkRead walk, Dictionary<string, HullMeshDef.LiftSet> cuts,
                                       Dictionary<string, string> levelForArea, BoatDeckDef deck, BoatInteriorDef interior)
        {
            Vector3 from = RampEnd(area, false), to = RampEnd(area, true);
            string fromCut = (from.z > area.ZMid ? area.CutHi : area.CutLo) ?? area.Cut;
            string toCut = (to.z > area.ZMid ? area.CutHi : area.CutLo) ?? area.Cut;
            var route = new BoatInteriorRoute { Id = area.Id, FromPoint = from, ToPoint = to,
                TotalRiseMeters = to.z - from.z, Mechanism = area.Kind == "stair" ? "InteriorStair" : "vertical_ladder",
                Exterior = cuts[fromCut].Level == 0 && cuts[toCut].Level == 0 };
            route.FromLevel = Landing(from, fromCut, walk, levelForArea, deck, interior.FloorTolerance);
            route.ToLevel = Landing(to, toCut, walk, levelForArea, deck, interior.FloorTolerance);
            route.Placed = route.FromLevel != null && route.ToLevel != null;
            route.NotPlacedBecause = route.Placed ? "" : area.Id + ": ramp end has no matching floor";
            return route;
        }

        static string Landing(Vector3 point, string cut, HullWalkRead walk, Dictionary<string, string> levels,
                              BoatDeckDef deck, float tolerance)
        {
            foreach (HullWalkArea area in walk.Areas)
            {
                if (area.IsClimb || area.Cut != cut || Math.Abs(area.HeightAt(point) - point.z) > tolerance) continue;
                Vector2 p = point;
                DeckAreaMath.ClosestPointOnOutline(area.Polygon, p, out float distance);
                if (area.Holes.Any(h => DeckAreaMath.Contains(h, p))) continue;
                if (!DeckAreaMath.Contains(area.Polygon, p) && distance > tolerance * tolerance) continue;
                if (levels.TryGetValue(area.Id, out string id)) return id;
                DeckArea baked = deck.Areas.FirstOrDefault(a => a.Id == area.Id || a.Id.StartsWith(area.Id + ".", StringComparison.Ordinal));
                if (baked != null) return baked.Id;
            }
            return null;
        }

        static Vector3 RampEnd(HullWalkArea area, bool last)
        {
            HullWalkRamp r = area.Ramp;
            // The walk clamps z beyond a0/a1: a flat landing can extend to the climb's boundary.
            // Join floors at that authored boundary, not at the interior interpolation endpoint.
            float[] extent = area.Polygon.Select(p => r.Axis == "x" ? p.x : p.y).ToArray();
            bool increasing = r.A1 >= r.A0;
            float at = last == increasing ? extent.Max() : extent.Min();
            float z = last ? r.Z1 : r.Z0;
            var crossings = new List<float>();
            for (int i = 0; i < area.Polygon.Length; i++)
            {
                Vector2 a = area.Polygon[i], b = area.Polygon[(i + 1) % area.Polygon.Length];
                float av = r.Axis == "x" ? a.x : a.y, bv = r.Axis == "x" ? b.x : b.y;
                if (at < Mathf.Min(av, bv) || at > Mathf.Max(av, bv)) continue;
                if (av == bv) { crossings.Add(r.Axis == "x" ? a.y : a.x); crossings.Add(r.Axis == "x" ? b.y : b.x); }
                else { Vector2 p = Vector2.Lerp(a, b, (at - av) / (bv - av)); crossings.Add(r.Axis == "x" ? p.y : p.x); }
            }
            if (crossings.Count == 0) throw new FormatException("walk.areas." + area.Id + ".ramp: endpoint outside polygon");
            float mid = (crossings.Min() + crossings.Max()) / 2;
            return r.Axis == "x" ? new Vector3(at, mid, z) : new Vector3(mid, at, z);
        }

        public static IEnumerable<DeckArea> DeckAreas(HullWalkArea area)
        {
            var pieces = new List<Vector2[]> { area.Polygon };
            if (area.Ramp != null)
                foreach (float at in new[] { area.Ramp.A0, area.Ramp.A1 }.OrderBy(v => v))
                    pieces = pieces.SelectMany(p => Split(p, area.Ramp.Axis, at)).ToList();
            for (int i = 0; i < pieces.Count; i++)
            {
                var result = DeckArea.From(pieces.Count == 1 ? area.Id : area.Id + "." + i, DeckAreaKind.Deck,
                    pieces[i].Select(p => new Vector3(p.x, p.y, area.HeightAt(p))).ToArray());
                result.Holes = area.Holes.Select(h => new DeckAreaHole { Outline = h.ToArray() }).ToArray();
                yield return result;
            }
        }

        static IEnumerable<Vector2[]> Split(Vector2[] polygon, string axis, float at)
        {
            float[] coords = polygon.Select(p => axis == "x" ? p.x : p.y).ToArray();
            if (coords.Min() >= at || coords.Max() <= at) { yield return polygon; yield break; }
            foreach (bool lower in new[] { true, false })
            {
                var output = new List<Vector2>();
                for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
                {
                    bool before = lower ? coords[j] <= at : coords[j] >= at, after = lower ? coords[i] <= at : coords[i] >= at;
                    if (before != after) output.Add(Vector2.Lerp(polygon[j], polygon[i], (at - coords[j]) / (coords[i] - coords[j])));
                    if (after) output.Add(polygon[i]);
                }
                if (output.Count >= 3) yield return output.ToArray();
            }
        }
    }
}
