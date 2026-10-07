#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using HiddenHarbours.World;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    public static partial class KeySceneImport
    {
        public const string EastEndSha256 = "e7cd4023be9c6819a7c1d66143ba76dfa47ad5f13ba4f8e12c90c0080c160f9a";

        /// <summary>Pass 2's 21 owner/building rows, seated on the game's ground. The other takes remain the Art desk's.</summary>
        public static Result ImportEastEnd(byte[] bytes, Func<Vector2, float> ground,
            IReadOnlyDictionary<string, string> existing, string scriptGuid)
        {
            if (Sha256(bytes) != EastEndSha256) throw new Refusal("The east end is not the ruled pass 2 file. STOP for the Art desk.");
            Json source = Json.Parse(Text(bytes, "eastEndPieces.json"), "eastEndPieces.json");
            var scene = new Scene
            {
                Id = "keyscene.stp_east_end", Name = "St Peters east end",
                Source = "HH-st-peters-phase-a-art-2026-10-02/east-end/eastEndPieces.json; sha256 " + EastEndSha256,
            };
            var result = new Result();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (Json p in Items(Need(source, "pieces", scene.Id), "east end pieces"))
            {
                string take = Need(p, "take", scene.Id).Str("take");
                if (take != "owner" && take != "with its building") continue;
                string id = Need(p, "id", scene.Id).Str("id");
                if (id.IndexOf('@') >= 0 || !ids.Add(id)) throw new Refusal("Invalid east-end id: " + id);
                var row = new Row
                {
                    Id = id, Kind = take == "owner" ? KeySceneDef.KindBuilding : KeySceneDef.KindSetPiece,
                    Kit = Need(p, "kit", id).Str("kit"), Piece = Need(p, "piece", id).Str("piece"),
                    Dir = Need(p, "dir", id).Int("dir"),
                };
                (row.X, row.Y) = Pair(Need(p, "at", id), id + " at");
                row.Z = ground(new Vector2(row.X, row.Y));
                if (float.IsNaN(row.Z) || float.IsInfinity(row.Z)) throw new Refusal("No ground at " + id);
                row.Variants.Add(KeySceneDef.Today);
                Json options = p.Get("opts");
                if (options != null)
                    foreach (var option in options.Fields)
                        row.Options.Add(new KeyValuePair<string, string>(option.Key,
                            option.Value.Kind == Json.Of.String ? option.Value.Text : option.Value.Compact()));
                scene.Rows.Add(row);
                scene.Tally.Pieces++; scene.Tally.Today++; scene.Tally.Kits.Add(row.Kit);
                result.Report.Add($"{id}: pass2 {Need(p, "z", id).Float("z")}; ground {Num(row.Z)}");
            }
            if (scene.Rows.Count != 21) throw new Refusal("The ruled east end must hold 21 rows.");
            Write(scene, ReadExisting(existing, scriptGuid), existing, "region.st_peters", scriptGuid, result);
            return result;
        }
    }
}
#endif
