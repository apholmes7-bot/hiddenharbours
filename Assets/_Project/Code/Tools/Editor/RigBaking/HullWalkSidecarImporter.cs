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
                // Save copies EVERY serialized field of the fresh deck over the saved asset, so the
                // authored boarding block (no sidecar carries it) is carried across first or it is wiped.
                var priorDeck = AssetDatabase.LoadAssetAtPath<BoatDeckDef>(deckPath);
                if (priorDeck != null) deck.CopyAuthoredBoardingRuleFrom(priorDeck);
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
            foreach (object raw in walk.Fields.ContainsKey("washboards")
                ? HullLayoutJson.Array(walk.Fields, "washboards") : new List<object>())
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
            interior.FootprintOutline = HouseEnvelope(walk.Hull, levels.Select(l => l.Outline),
                walk.Areas.Where(a => a.IsClimb &&
                    ((a.Cut != null && cuts.TryGetValue(a.Cut, out var hi) && hi.Level > 0) ||
                     (a.CutLo != null && cuts.TryGetValue(a.CutLo, out var lo) && lo.Level > 0)))
                    .Select(a => a.Polygon),
                walk.Areas.Where(a => a.IsClimb).Select(a => (a.Id,
                    cuts.TryGetValue(a.CutHi ?? a.Cut ?? "", out var hi) ? hi.Level : -1,
                    cuts.TryGetValue(a.CutLo ?? "", out var lo) ? lo.Level : -1, a.Polygon)),
                indoor.Select(a => (a.Id, a.Polygon)));
            interior.Door = walk.House == null ? new BoatInteriorDoor() : Door(HullLayoutJson.Child(walk.House, "door"), walk, result);
            interior.AdditionalDoors = walk.House != null && walk.House.TryGetValue("doors", out object doors)
                ? HullLayoutJson.List(doors, "house.doors").Select(d => HullLayoutJson.Object(d, "house.doors"))
                    .Where(d => HullLayoutJson.Text(d, "id") != interior.Door?.Id).Select(d => Door(d, walk, result)).Where(d => d != null).ToArray()
                : Array.Empty<BoatInteriorDoor>();
            interior.InternalPassages = Passages(walk, rig, interior);
            interior.Anchors = HullLayoutJson.Array(walk.Fields, "interact").Select(raw =>
            {
                var i = HullLayoutJson.Object(raw, "interact");
                return new BoatInteriorAnchor { Id = HullLayoutJson.Text(i, "id"), Action = HullLayoutJson.Text(i, "action"),
                    ReachPoint = HullLayoutJson.Point(i["reach"], "interact.reach") };
            }).ToArray();
            interior.Routes = walk.Areas.Where(a => a.IsClimb).Select(a => Route(a, walk, cuts, levelForArea, deck, interior)).ToArray();
            EmitEntryLandings(walk, interior);
            return result;
        }

        public static Vector2[] HouseEnvelope(string hull, IEnumerable<Vector2[]> soles,
                                              IEnumerable<Vector2[]> indoorClimbs,
                                              IEnumerable<(string Id, int HighLevel, int LowLevel, Vector2[] Polygon)> climbs = null,
                                              IEnumerable<(string Id, Vector2[] Polygon)> areas = null,
                                              Action<string> log = null)
        {
            var outlines = soles.ToArray();
            if (outlines.Length == 0) return Array.Empty<Vector2>();
            if (HullWalkPolygonUnion.TryUnion(outlines, out var rings, out int floors)) return rings[0];
            if (HullWalkPolygonUnion.TryUnion(outlines.Concat(indoorClimbs), out rings, out floors)) return rings[0];
            var islands = rings.Skip(1).Where(r => HouseRingArea(r) > 0).ToArray();
            var entries = (climbs ?? Array.Empty<(string Id, int HighLevel, int LowLevel, Vector2[] Polygon)>())
                .Where(c => c.HighLevel == 0 && c.LowLevel > 0).ToArray();
            if (islands.Length == floors - 1 && islands.All(r => entries.Any(c => HousePolygonsMeet(r, c.Polygon))))
            {
                var sources = (areas ?? Array.Empty<(string Id, Vector2[] Polygon)>()).ToArray();
                foreach (var island in islands)
                {
                    var ids = sources.Where(a => HousePolygonsMeet(island, a.Polygon)).Select(a => a.Id)
                        .Concat(entries.Where(c => HousePolygonsMeet(island, c.Polygon)).Select(c => c.Id))
                        .Distinct().OrderBy(id => id, StringComparer.Ordinal);
                    string message = hull + ": separate deck-entered compartment [" + string.Join(", ", ids) + "] " +
                        HouseRingArea(island).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " m^2";
                    if (log != null) log(message); else Debug.Log(message);
                }
                return rings[0];
            }
            throw new FormatException(hull + ": the soles and the indoor climbs do not join into one house envelope (" + floors + " floors)");
        }

        static double HouseRingArea(Vector2[] ring)
        {
            double area = 0;
            for (int i = 0; i < ring.Length; i++)
            {
                Vector2 a = ring[i], b = ring[(i + 1) % ring.Length];
                area += (double)a.x * b.y - (double)a.y * b.x;
            }
            return area * .5;
        }

        static bool HousePolygonsMeet(Vector2[] a, Vector2[] b)
        {
            foreach (var point in a)
            {
                if (DeckAreaMath.Contains(b, point)) return true;
                DeckAreaMath.ClosestPointOnOutline(b, point, out float distance);
                if (distance <= 1e-10f) return true;
            }
            foreach (var point in b)
                if (DeckAreaMath.Contains(a, point)) return true;
            for (int i = 0; i < a.Length; i++)
                for (int j = 0; j < b.Length; j++)
                {
                    Vector2 ab = a[(i + 1) % a.Length] - a[i], cd = b[(j + 1) % b.Length] - b[j];
                    Vector2 offset = b[j] - a[i];
                    float cross = ab.x * cd.y - ab.y * cd.x;
                    if (Mathf.Abs(cross) < 1e-10f) continue;
                    float t = (offset.x * cd.y - offset.y * cd.x) / cross;
                    float u = (offset.x * ab.y - offset.y * ab.x) / cross;
                    if (t >= 0 && t <= 1 && u >= 0 && u <= 1) return true;
                }
            return false;
        }

        static void EmitEntryLandings(HullWalkRead walk, BoatInteriorDef interior)
        {
            // Held metadata is not an entry. No legacy room/door output is rewritten here.
            if (!interior.HasInterior() || walk.House == null) return;
            var sourceDoors = new[] { HullLayoutJson.Child(walk.House, "door") }.ToList();
            if (walk.House.TryGetValue("doors", out object rawDoors))
                sourceDoors.AddRange(HullLayoutJson.List(rawDoors, "house.doors")
                    .Select(d => HullLayoutJson.Object(d, "house.doors")));
            var doors = new[] { interior.Door }.Concat(interior.AdditionalDoors ?? Array.Empty<BoatInteriorDoor>());
            foreach (BoatInteriorDoor door in doors)
            {
                if (door == null || !BoatCabinThreshold.HasBand(door)) continue;
                if (interior.Levels.Any(l => l != null && l.IsUsable()
                    && Mathf.Abs(l.SoleZMeters - door.ThresholdPoint.z) <= interior.FloorTolerance)) continue;
                var source = sourceDoors.First(d => HullLayoutJson.Text(d, "id") == door.Id);
                float y = HullLayoutJson.Number(source, "y");
                var candidates = new List<BoatInteriorEntryLanding>();
                foreach (HullWalkArea area in walk.Areas.Where(a => a.Kind == "stair" && a.Ramp != null))
                {
                    HullWalkRamp ramp = area.Ramp;
                    float high = Mathf.Max(ramp.Z0, ramp.Z1);
                    if (ramp.Z0 == ramp.Z1 || Mathf.Abs(high - door.ThresholdPoint.z) > interior.FloorTolerance) continue;
                    float at = ramp.Z0 > ramp.Z1 ? ramp.A0 : ramp.A1;
                    foreach (Vector2[] region in Split(area.Polygon, ramp.Axis, at))
                    {
                        if (region.Any(p => Mathf.Abs(area.HeightAt(p) - high) > 1e-6f)) continue;
                        Vector2 sill = BoatCabinThreshold.PointOf(door);
                        DeckAreaMath.ClosestPointOnOutline(region, sill, out float sqr);
                        if (!DeckAreaMath.Contains(region, sill) && sqr > 1e-12f) continue;
                        var landing = new BoatInteriorEntryLanding
                        {
                            OpeningStart = new Vector2(HullLayoutJson.Number(source, "x0"), y),
                            OpeningEnd = new Vector2(HullLayoutJson.Number(source, "x1"), y),
                            UpperPlateau = region,
                            HeightMetres = high,
                            RouteId = area.Id,
                        };
                        door.EntryLanding = landing;
                        if (!BoatCabinThreshold.TryEntryLine(interior, door, out BoatCabinEntryKind kind,
                                out _, out _, out _) || kind != BoatCabinEntryKind.Landing)
                            throw new FormatException(walk.Hull + ": house.doors." + door.Id +
                                ".entryLanding: plateau has no valid route to a room");
                        candidates.Add(landing);
                    }
                }
                if (candidates.Count != 1)
                    throw new FormatException(walk.Hull + ": house.doors." + door.Id +
                        ".entryLanding: expected one authored upper plateau, found " + candidates.Count);
                door.EntryLanding = candidates[0];
            }
        }

        static BoatInteriorDoor Door(Dictionary<string, object> d, HullWalkRead walk, HullWalkImportResult result)
        {
            string id = HullLayoutJson.Text(d, "id");
            bool alwaysOpen = d.TryGetValue("leaf", out object leaf) && leaf == null;
            string field = walk.Hull + ": house.doors." + id;
            if (alwaysOpen && (!d.TryGetValue("open", out object opened) || !(opened is bool b) || !b))
                throw new FormatException(field + ".open: leafless doorway must explicitly be open");
            float radius = walk.Radius;
            float x0 = HullLayoutJson.Number(d, "x0"), x1 = HullLayoutJson.Number(d, "x1"), y = HullLayoutJson.Number(d, "y");
            bool hinge = !alwaysOpen && HullLayoutJson.Text(d, "kind") == "hinge";
            var clear = alwaysOpen && !d.ContainsKey("clear") ? null : HullLayoutJson.Child(d, "clear");
            return new BoatInteriorDoor { Id = id, Side = HullLayoutJson.Text(d, "face"),
                OpeningMode = alwaysOpen ? BoatInteriorDoorOpeningMode.AlwaysOpen : BoatInteriorDoorOpeningMode.Leafed,
                OpeningReason = alwaysOpen ? HullLayoutJson.Text(d, "reason", field) : "",
                ClearWidthMeters = clear != null ? HullLayoutJson.Number(clear, "width") : x1 - x0,
                ClearHeightMeters = clear != null ? HullLayoutJson.Number(clear, "height") : HullLayoutJson.Number(d, "z1") - HullLayoutJson.Number(d, "z0"),
                ThresholdPoint = new Vector3((x0 + x1) / 2, y, HullLayoutJson.Number(d, "sillZ")),
                Mechanism = hinge ? BoatInteriorDoorMechanism.Hinged : BoatInteriorDoorMechanism.Sliding,
                KeepClear = hinge ? HullLayoutJson.Polygon(d["keepClear"], "house.door.keepClear", false) :
                    new[] { new Vector2(x0, y - 2 * radius), new Vector2(x1, y - 2 * radius), new Vector2(x1, y + 2 * radius), new Vector2(x0, y + 2 * radius) } };
        }

        static BoatInteriorPassage[] Passages(HullWalkRead walk, RigMeshData rig, BoatInteriorDef interior)
        {
            if (walk.House == null || !walk.House.TryGetValue("doorways", out object raw))
                return Array.Empty<BoatInteriorPassage>();
            var passages = new List<BoatInteriorPassage>();
            foreach (object item in HullLayoutJson.List(raw, "house.doorways"))
            {
                var d = HullLayoutJson.Object(item, "house.doorways");
                string id = HullLayoutJson.Text(d, "id"), field = walk.Hull + ": house.doorways." + id;
                if (!d.ContainsKey("leaf") || d["leaf"] != null || !d.TryGetValue("open", out object opened) || !(opened is bool b) || !b)
                    throw new FormatException(field + ": expected leaf null and open true");
                if (passages.Any(p => p.Id == id)) throw new FormatException(field + ".id: duplicate");
                string lv = HullLayoutJson.Text(d, "lv", field);
                var declared = rig.Levels.Where(l => l.Id == lv && l.Enclosed).ToArray();
                if (declared.Length != 1) throw new FormatException(field + ".lv: expected one enclosed published level");
                var levels = interior.Levels.Where(l => l.Id == declared[0].DeckId &&
                    Math.Abs(l.SoleZMeters - declared[0].SoleZ) < 0.0001).ToArray();
                if (levels.Length != 1) throw new FormatException(field + ".lv: missing or ambiguous imported sole");
                string wall = HullLayoutJson.Text(d, "wall", field);
                var u = HullLayoutJson.Array(d, "u", field);
                if ((wall != "x" && wall != "y") || u.Count != 2)
                    throw new FormatException(field + ".wall / u: expected axis and two aperture endpoints");
                var aperture = new Vector2(HullLayoutJson.Numeric(u[0], field + ".u[0]"), HullLayoutJson.Numeric(u[1], field + ".u[1]"));
                var clear = HullLayoutJson.Child(d, "clear", field);
                float width = HullLayoutJson.Number(clear, "width", field + ".clear");
                float height = HullLayoutJson.Number(clear, "height", field + ".clear");
                if (aperture.y <= aperture.x || width <= 0 || height <= 0 || width > aperture.y - aperture.x + 0.001f ||
                    height > declared[0].CeilingZ - declared[0].SoleZ + 0.001f)
                    throw new FormatException(field + ".clear: outside declared aperture or level");
                passages.Add(new BoatInteriorPassage { Id = id, RigLevelId = lv, LevelId = levels[0].Id,
                    SoleZMeters = levels[0].SoleZMeters, WallAxis = wall, WallAtMeters = HullLayoutJson.Number(d, "at", field),
                    ApertureMeters = aperture, ClearWidthMeters = width, ClearHeightMeters = height,
                    Reason = HullLayoutJson.Text(d, "reason", field) });
            }
            return passages.ToArray();
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
