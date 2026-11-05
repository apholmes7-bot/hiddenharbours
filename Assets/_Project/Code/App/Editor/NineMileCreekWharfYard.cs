#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>The delivered harbour.yard's part frames and the trades that read its anchors.</summary>
    public static class NineMileCreekWharfYard
    {
        public const string RootName = "NmcWharfBoatYard";
        public const string CatalogueName = "NmcWharfYardCatalogue";
        public const string DoryTradeName = "NmcWharfYardDoryTrade";
        public const string OutboardTradeName = "NmcWharfYardOutboardTrade";
        public const string RowId = "harbour.yard", DamagedDoryRowId = "boat.nmc_damaged_dory";
        [Serializable] public sealed class Bounds { public float[] x, y, z; }
        [Serializable] public sealed class Placement { public string row; public float[] at; public float elevation; public int dir; }
        [Serializable] public sealed class Anchor { public float[] door; }
        [Serializable] public sealed class Gameplay { public string id; public Bounds bounds; public Placement placement; public Anchor anchors; }
        [Serializable] public sealed class Piece { public string key, id; public Gameplay gameplay; }
        [Serializable] public sealed class Today { public Piece[] pieces; }
        // The general JSON reader retains polygon arrays, which Unity JsonUtility does not deserialize.
        public static Today Read() => JsonUtility.FromJson<Today>(File.ReadAllText(NmcShipyardKit.TodayPath));
        public static KeyScenePiece Row(string id) => AssetDatabase.LoadAssetAtPath<KeySceneDef>(NineMileCreekWharfLandSide.KeyScenePath)
            .Pieces.Single(p => p.Id == id);
        public static Vector3 DamagedDory => Row(DamagedDoryRowId).At;
        public static Vector3 Door
        {
            get
            {
                Piece p = Read().pieces.Single(p => p.key == "shed");
                float[] a = p.gameplay.anchors.door, at = p.gameplay.placement.at;
                if (a == null || a.Length != 3) throw new InvalidOperationException("The delivered yard has no shed door anchor.");
                return new Vector3(at[0] + a[0], at[1] + a[1], a[2]);
            }
        }
        public static Rect Quay
        {
            get
            {
                object today = DeckSidecarJson.Parse(File.ReadAllText(NmcShipyardKit.TodayPath));
                object part = DeckSidecarJson.AsArray(DeckSidecarJson.Member(today, "pieces")).Single(p =>
                    DeckSidecarJson.String(DeckSidecarJson.Member(p, "key")) == "yardFront");
                object game = DeckSidecarJson.Member(part, "gameplay");
                var poly = DeckSidecarJson.AsArray(DeckSidecarJson.Member(DeckSidecarJson.AsArray(DeckSidecarJson.Member(game, "walkSurfaces"))[0], "poly"));
                var points = poly.Select(p => DeckSidecarJson.AsArray(p)).Select(p => new Vector2(DeckSidecarJson.Float(p[0]), DeckSidecarJson.Float(p[1]))).ToArray();
                float[] at = Read().pieces.Single(p => p.key == "yardFront").gameplay.placement.at;
                return Rect.MinMaxRect(at[0] + points.Min(p => p.x), at[1] + points.Min(p => p.y), at[0] + points.Max(p => p.x), at[1] + points.Max(p => p.y));
            }
        }
        public static Vector2 QuayPost => Quay.center;
        public static Vector2 QuayFace => new Vector2(Quay.center.x, Quay.yMin);
        public sealed class FootprintPart
        {
            public string Key, Id;
            public readonly List<(string label, Vector2[] points)> Shapes = new List<(string, Vector2[])>();
        }
        static IReadOnlyList<FootprintPart> _footprints;
        static Rect? _footprint;
        /// <summary>The same delivered gameplay polygons that PlaceYard turns into walks and solids.</summary>
        public static IReadOnlyList<FootprintPart> Footprints()
        {
            if (_footprints != null) return _footprints;
            var parts = new List<FootprintPart>();
            object today = DeckSidecarJson.Parse(File.ReadAllText(NmcShipyardKit.TodayPath));
            foreach (object raw in DeckSidecarJson.AsArray(DeckSidecarJson.Member(today, "pieces")))
            {
                object game = DeckSidecarJson.Member(raw, "gameplay");
                var placement = DeckSidecarJson.AsArray(DeckSidecarJson.Member(DeckSidecarJson.Member(game, "placement"), "at"));
                var at = new Vector2(DeckSidecarJson.Float(placement[0]), DeckSidecarJson.Float(placement[1]));
                var part = new FootprintPart { Key = DeckSidecarJson.String(DeckSidecarJson.Member(raw, "key")),
                    Id = DeckSidecarJson.String(DeckSidecarJson.Member(game, "id")) };
                foreach (var group in new[] { (field: "walkSurfaces", points: "poly", label: "walk"),
                                              (field: "colliders", points: "pts", label: "solid") })
                {
                    int index = 0;
                    foreach (object shape in DeckSidecarJson.AsArray(DeckSidecarJson.Member(game, group.field)))
                    {
                        var points = DeckSidecarJson.AsArray(DeckSidecarJson.Member(shape, group.points))
                            .Select(p => DeckSidecarJson.AsArray(p))
                            .Select(p => new Vector2(DeckSidecarJson.Float(p[0]), DeckSidecarJson.Float(p[1])) + at).ToArray();
                        part.Shapes.Add((group.label + index++, points));
                    }
                }
                if (part.Shapes.Count == 0) throw new InvalidOperationException(part.Id + " has no delivered footprint.");
                parts.Add(part);
            }
            return _footprints = parts;
        }
        public static Rect Footprint
        {
            get
            {
                if (_footprint.HasValue) return _footprint.Value;
                var points = Footprints().SelectMany(p => p.Shapes).SelectMany(s => s.points).ToArray();
                var rect = Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
                _footprint = rect;
                return rect;
            }
        }
        // Rulings 24–26: beside the unchanged road, with both parked headings clear of the player walks.
        public const float OutboardBayEastOfRoadEndMetres = 5f, OutboardBaySouthOfRoadMetres = 1.5f;
        public static Vector2 OutboardBay => new Vector2(
            NineMileCreekGroundPlan.YardRoadEnd.x + OutboardBayEastOfRoadEndMetres,
            NineMileCreekGroundPlan.YardRoadY - OutboardBaySouthOfRoadMetres);
        public static Vector2[] RoadApproach(Vector2[] wharfRoad) => wharfRoad.Concat(new[]
            { NineMileCreekGroundPlan.YardRoadStart, NineMileCreekGroundPlan.YardRoadEnd }).ToArray();
        public static SetPieceDef Definition(string key) => AssetDatabase.LoadAssetAtPath<SetPieceDef>(
            "Assets/_Project/Data/SetPieces/SetPiece_" + key + ".asset") ?? throw new InvalidOperationException("The yard Def is not baked: " + key);
    }
}
#endif
