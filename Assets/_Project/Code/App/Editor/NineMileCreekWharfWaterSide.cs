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
    /// <summary>The wharf's one authored berth register; no parallel packing table.</summary>
    public static class NineMileCreekWharfWaterSide
    {
        public const string KeyScenePath = "Assets/_Project/Data/KeyScenes/KeyScene_nmc_wharf.asset";
        public const string TodayPath = "Assets/_Project/Art/Sprites/NineMileCreek/WaterSide/nmcWaterSide.today.json";
        public const string RootName = "NmcWharfWaterSide";
        public static readonly string[] HarbourKeys = { "west", "fleet", "east", "arm_w", "arm_e" };

        public static KeySceneDef Scene() => AssetDatabase.LoadAssetAtPath<KeySceneDef>(KeyScenePath)
            ?? throw new InvalidOperationException("The wharf key scene is unavailable.");
        [Serializable] sealed class OwnedRow { public string id, owner, waits; }
        [Serializable] sealed class Ownership { public OwnedRow[] rows; }
        public static KeyScenePiece[] PlacementRows()
        {
            var owned = JsonUtility.FromJson<Ownership>(File.ReadAllText(NineMileCreekWharfLandSide.RecordPath)).rows;
            var eligible = owned.Where(r => r.owner == "H5" && string.IsNullOrEmpty(r.waits)).ToArray();
            var ids = new HashSet<string>(eligible.Select(r => r.id));
            var rows = Scene().Pieces.Where(p => ids.Contains(p.Id)).ToArray();
            if (rows.Length != eligible.Length)
                throw new StPetersLayerRefresh.Refusal("The H5 placement subset names an absent row.");
            if (rows.Any(p => owned.Any(r => r.id == p.Id && (r.owner != "H5" || !string.IsNullOrEmpty(r.waits)))))
                throw new StPetersLayerRefresh.Refusal("The H5 placement subset names a waiting row.");
            return rows;
        }
        public static object Register()
        {
            var row = Scene().Pieces.Single(p => p.Id == "harbour.west");
            return DeckSidecarJson.Parse(row.Options.Single(o => o.Key == "nmc.scene.harbour").Value);
        }
        static object M(object o, string k) => DeckSidecarJson.Member(o, k);
        static float F(object o) => DeckSidecarJson.Float(o);
        static IReadOnlyList<object> A(object o) => DeckSidecarJson.AsArray(o);

        public static float[] WallCentres() => A(M(Register(), "berths")).Select(b =>
        {
            var slot = A(M(b, "slot")); return (F(slot[0]) + F(slot[1])) * 0.5f;
        }).ToArray();
        public static Vector2 WallSlot(int index)
        {
            var b = A(M(Register(), "berths"))[index]; var slot = A(M(b, "slot"));
            return new Vector2(F(slot[0]), F(slot[1]));
        }
        public static Rect WorkingFloat()
        {
            object f = M(Register(), "workingFloat"); var x = A(M(f, "x")); var y = A(M(f, "y"));
            return Rect.MinMaxRect(F(x[0]), F(y[0]), F(x[1]), F(y[1]));
        }
        /// <summary>Where the rig's bake drew the working float: its own walk polygon. The register moves her.</summary>
        public static Rect BakedWorkingFloat()
        {
            object part = A(M(ReadToday(), "parts")).Single(p => DeckSidecarJson.String(M(p, "key")) == "west_float");
            var poly = A(M(part, "walkPolygon")).Select(q => { var xy = A(q); return new Vector2(F(xy[0]), F(xy[1])); }).ToArray();
            return Rect.MinMaxRect(poly.Min(q => q.x), poly.Min(q => q.y), poly.Max(q => q.x), poly.Max(q => q.y));
        }
        public static KeyScenePiece[] FloatBoatRows() => Scene().Pieces.Where(p =>
            p.Kind == KeySceneDef.KindBoat && p.StandsOn == "water").OrderBy(p => p.At.x).ToArray();
        public static Vector2 FloatBerth(int index)
        {
            var rows = FloatBoatRows();
            if (index < 0 || index >= rows.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return rows[index].At;
        }
        public static KeyScenePiece FloatRowForOwner(string ownerId)
        {
            string suffix = ownerId.Substring(ownerId.LastIndexOf('_') + 1);
            return FloatBoatRows().Single(p => p.Id == "boat.nmc_" + suffix);
        }
        public static SetPieceDef Definition(string key) => AssetDatabase.LoadAssetAtPath<SetPieceDef>(
            "Assets/_Project/Data/SetPieces/SetPiece_nmc_" + key + ".asset")
            ?? throw new InvalidOperationException("The harbour Def is not baked: " + key);
        public static object ReadToday() => DeckSidecarJson.Parse(File.ReadAllText(TodayPath));
    }
}
#endif
