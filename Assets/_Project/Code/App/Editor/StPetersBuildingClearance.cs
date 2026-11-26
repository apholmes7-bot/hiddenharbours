#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HiddenHarbours.Art;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>No plant grows on a house.</b> The plant field draws every site by its root's y, and a building
    /// draws by the y of its middle, so a flower rooted on the porch or against the front wall paints over
    /// the wall, and one rooted inside the walls paints over the room when the player is in it.
    ///
    /// <para>The ground kept clear round each building is its floor grown by
    /// <see cref="ClearanceMetres"/> on every side, and behind it one storey's drawn rise (the rule
    /// Ginny's cottage was held to first). The field's ground grid is closed (<c>255</c>) under that
    /// zone, so the meadow recipe never seeds there; nothing authored is moved by this.</para>
    /// </summary>
    public static class StPetersBuildingClearance
    {
        /// <summary>How far past the walls the ground is kept clear (m). The furthest plant the audit found
        /// drawing over a wall stood 1.46 m out from it, on a porch.</summary>
        public const float ClearanceMetres = 1.5f;

        /// <summary>
        /// Buildings this rule does not hold, each with the reason. Ginny's cottage keeps its own
        /// room-only test: her lot is a let-go property and its planting belongs to the plot work.
        /// Her sheds and the derelict cannery are overgrown ON PURPOSE (docs/art/intentional-overgrowth.md)
        /// and carry no interior, so they never reach this rule; they are named so nobody adds them.
        /// </summary>
        public static readonly (string Path, string Reason)[] Exceptions =
        {
            ("GinnyPlot/sageCottage", "Ginny's cottage: held to its room by its own test; her lot's planting is the plot's"),
            ("GinnyPlot/leanTo", "intentional overgrowth: a ruined lean-to"),
            ("GinnyPlot/woodshed", "intentional overgrowth: a let-go woodshed"),
            ("GinnyPlot/netStore", "intentional overgrowth: a let-go net store"),
            ("GinnyPlot/CamperLot", "Ginny's lot: its planting belongs to the plot work"),
            ("StPetersKeyScenes/keyscene.stp_cannery", "intentional overgrowth: the derelict cannery"),
        };

        public static bool IsException(string path) =>
            Exceptions.Any(e => path == e.Path || path.StartsWith(e.Path + "/", StringComparison.Ordinal));

        public sealed class Zone
        {
            public string Path;
            public Vector2[] Corners;
            public float Wall, Rise;

            /// <summary>Is <paramref name="p"/> on ground this building keeps clear? <paramref name="pad"/>
            /// grows the zone, so a whole grid cell can be tested by its centre.</summary>
            public bool Contains(Vector2 p, float pad = 0f)
            {
                if (DistanceTo(p) <= ClearanceMetres + pad) return true;
                float behind = Mathf.Max(Wall, Rise);
                for (float d = 0f; d <= behind + 1e-4f; d += 0.25f)
                    if (DistanceTo(new Vector2(p.x, p.y - d)) <= Wall + pad) return true;
                return false;
            }

            public float DistanceTo(Vector2 p)
            {
                if (Inside(p)) return 0f;
                float best = float.MaxValue;
                for (int i = 0; i < Corners.Length; i++)
                    best = Mathf.Min(best, SegmentDistance(p, Corners[i], Corners[(i + 1) % Corners.Length]));
                return best;
            }

            bool Inside(Vector2 p)
            {
                bool c = false;
                for (int i = 0, j = Corners.Length - 1; i < Corners.Length; j = i++)
                    if ((Corners[i].y > p.y) != (Corners[j].y > p.y) &&
                        p.x < (Corners[j].x - Corners[i].x) * (p.y - Corners[i].y) / (Corners[j].y - Corners[i].y) + Corners[i].x)
                        c = !c;
                return c;
            }

            static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
            {
                Vector2 ab = b - a;
                float t = ab.sqrMagnitude < 1e-12f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                return Vector2.Distance(p, a + ab * t);
            }
        }

        /// <summary>Every building in the scene with an interior, read back the way the player walks it.</summary>
        public static List<Zone> ReadZones(StPetersLayerRefresh.SceneYaml scene)
        {
            var zones = new List<Zone>();
            foreach (var interior in scene.Docs.Where(d => d.IsScript("HiddenHarbours.World.BuildingInterior")))
            {
                var go = scene.Get(interior.FieldRef("m_GameObject"));
                var tr = scene.TransformOf(go);
                var floor = new InteriorFootprint(
                    InteriorFixturePlacement.WorldOf(scene, tr),
                    StPetersLayerRefresh.ParseFloat(interior.Field("_widthMetres")),
                    StPetersLayerRefresh.ParseFloat(interior.Field("_lengthMetres")),
                    StPetersLayerRefresh.ParseInt(interior.Field("_facing")),
                    StPetersLayerRefresh.ParseInt(interior.Field("_facings")),
                    StPetersLayerRefresh.ParseFloat(interior.Field("_groundDepthScale")));
                zones.Add(new Zone
                {
                    Path = PathOf(scene, tr),
                    Corners = floor.Corners(),
                    Wall = StPetersLayerRefresh.ParseFloat(interior.Field("_wallThicknessMetres")),
                    Rise = StPetersLayerRefresh.ParseFloat(interior.Field("_upperLevelY")),
                });
            }
            // A building drawn whole carries no room; its plan comes from the step that stands it.
            zones.AddRange(StPetersLayerRefresh.KeeperCottageZones(scene));
            return zones;
        }

        static string PathOf(StPetersLayerRefresh.SceneYaml scene, StPetersLayerRefresh.Doc tr)
        {
            var names = new List<string>();
            for (var t = tr; t != null; t = scene.Get(t.FieldRef("m_Father")))
                names.Add(scene.Get(t.FieldRef("m_GameObject")).Field("m_Name"));
            names.Reverse();
            return string.Join("/", names);
        }

        public const string GroundPath = "Assets/_Project/Data/Plants5/StPetersPlantGround.asset";
        public const string DefinitionPath = "Assets/_Project/Data/Plants5/StPetersPlantField.asset";

        /// <summary>
        /// Close the ground grid under every held building's zone. Idempotent: a second run changes
        /// nothing. Batch: <c>-executeMethod HiddenHarbours.App.Editor.StPetersBuildingClearance.ApplyToGround</c>.
        /// </summary>
        public static void ApplyToGround()
        {
            var scene = StPetersLayerRefresh.SceneYaml.Parse(File.ReadAllText(StPetersLayerRefresh.ScenePath));
            var zones = ReadZones(scene).Where(z => !IsException(z.Path)).ToList();
            if (zones.Count == 0) throw new InvalidOperationException("No St Peters building interiors were read.");
            var ground = AssetDatabase.LoadAssetAtPath<PlantFieldGroundData>(GroundPath);
            var definition = AssetDatabase.LoadAssetAtPath<PlantFieldDefinition>(DefinitionPath);
            float cell = definition.CellSize;
            float halfDiagonal = cell * 0.70710678f;
            byte[] map = ground.Decode();
            var log = new StringBuilder();
            int changed = 0;
            foreach (var zone in zones)
            {
                float minX = zone.Corners.Min(c => c.x) - ClearanceMetres - cell, maxX = zone.Corners.Max(c => c.x) + ClearanceMetres + cell;
                float minY = zone.Corners.Min(c => c.y) - ClearanceMetres - cell;
                float maxY = zone.Corners.Max(c => c.y) + Mathf.Max(ClearanceMetres, Mathf.Max(zone.Wall, zone.Rise) + zone.Wall) + cell;
                int x0 = Mathf.Max(0, Mathf.FloorToInt((minX - definition.Origin.x) / cell));
                int x1 = Mathf.Min(ground.Width - 1, Mathf.FloorToInt((maxX - definition.Origin.x) / cell));
                int y0 = Mathf.Max(0, Mathf.FloorToInt((minY - definition.Origin.y) / cell));
                int y1 = Mathf.Min(ground.Height - 1, Mathf.FloorToInt((maxY - definition.Origin.y) / cell));
                int here = 0;
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var centre = new Vector2(definition.Origin.x + (x + 0.5f) * cell, definition.Origin.y + (y + 0.5f) * cell);
                    if (!zone.Contains(centre, halfDiagonal)) continue;
                    int at = y * ground.Width + x;
                    if (map[at] == 255) continue;
                    map[at] = 255; changed++; here++;
                }
                log.AppendLine($"{zone.Path}: {here} cell(s) closed");
            }
            if (changed > 0)
            {
                ground.Payload = Encode(map);
                EditorUtility.SetDirty(ground);
                AssetDatabase.SaveAssetIfDirty(ground);
            }
            // A plant set down by hand ignores the grid, so one inside a zone is taken off the list itself.
            var uprooted = definition.Authored.Where(a => zones.Any(z => z.Contains(a.At))).ToArray();
            if (uprooted.Length > 0)
            {
                var gone = new HashSet<string>(uprooted.Select(a => a.Id));
                definition.Authored = definition.Authored.Where(a => !gone.Contains(a.Id)).ToArray();
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssetIfDirty(definition);
                foreach (var zone in zones)
                {
                    var here = uprooted.Where(a => zone.Contains(a.At)).Select(a => a.Id).ToArray();
                    if (here.Length > 0) log.AppendLine($"{zone.Path}: {here.Length} authored plant(s) removed: {string.Join(", ", here)}");
                }
            }
            log.AppendLine($"total {changed} cell(s) closed, {uprooted.Length} authored plant(s) removed, {definition.Authored.Length} kept");
            Debug.Log("[StPetersBuildingClearance]\n" + log);
        }

        /// <summary>The ground grid's run-length payload: little-endian uint16 run, then the byte.</summary>
        public static string Encode(byte[] map)
        {
            using (var stream = new MemoryStream())
            {
                for (int start = 0; start < map.Length;)
                {
                    int end = start + 1;
                    while (end < map.Length && end - start < ushort.MaxValue && map[end] == map[start]) end++;
                    int length = end - start;
                    stream.WriteByte((byte)length); stream.WriteByte((byte)(length >> 8)); stream.WriteByte(map[start]);
                    start = end;
                }
                return Convert.ToBase64String(stream.ToArray());
            }
        }
    }
}
#endif
