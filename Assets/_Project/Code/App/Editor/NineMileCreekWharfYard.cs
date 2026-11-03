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
        public static SetPieceDef Definition(string key) => AssetDatabase.LoadAssetAtPath<SetPieceDef>(
            "Assets/_Project/Data/SetPieces/SetPiece_" + key + ".asset") ?? throw new InvalidOperationException("The yard Def is not baked: " + key);
    }
}
#endif
