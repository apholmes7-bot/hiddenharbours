#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    public static class StPetersVillageDressing
    {
        public const string CommonsRoot = "StPetersVillageCommons";
        public const string DressingGroup = "Dressing";

        public sealed class Row
        {
            public string Group;
            public VillagePiece Piece;
            public string Kit;
            public string Key;
            public string Status;
            public Vector2 Position;
        }

        static readonly HashSet<string> BakedYardPieces = new HashSet<string>(StringComparer.Ordinal)
        {
            "buoyPost", "trapBench", "anchorSet", "doryPlanter", "oilTank", "sawhorses",
            "clothesline", "vegRows", "potatoDrills", "barrelPlanter", "borderBed", "swingSet",
            "firePit", "farmStand", "flagpole",
        };

        static readonly HashSet<string> OmittedYardPieces = new HashSet<string>(StringComparer.Ordinal)
        {
            "woodpile", "rainBarrel", "birdbath", "compostBin", "doghouse", "lilac", "mailbox",
            "raspberryRow", "rhubarb", "roseBush", "sandbox",
        };

        public static bool HoldsYard(string id) => id == "yard.st_peters.white_farmhouse" ||
            id == "yard.st_peters.red_saltbox" || id == "yard.st_peters.post_office" ||
            id == "yard.st_peters.cream_saltbox";

        public static bool HoldsCommonsStation(string id) => id == "station.st_peters.flake_yard";

        public static bool HoldsTree(string id) => id == "tree.stp_vl_square_maple_01" ||
            id == "tree.stp_vl_square_maple_02";

        public static Row Resolve(string group, VillagePiece piece, bool held)
        {
            if (piece == null) throw new ArgumentNullException(nameof(piece));
            var row = new Row { Group = group, Piece = piece, Kit = piece.Kit, Key = piece.Piece,
                Position = piece.Position, Status = "place" };
            if (held || HoldsTree(piece.Id)) { row.Status = "held"; return row; }
            if (piece.Pending) throw new InvalidOperationException("Dressing piece awaits its kit: " + piece.Id);
            if (piece.Kit == "yardIso")
            {
                if (OmittedYardPieces.Contains(piece.Piece)) row.Status = "omit";
                else if (piece.Piece == "bench")
                {
                    row.Kit = NineMileCreekDressing.DecorFamily;
                    row.Status = "stand-in";
                }
                else if (!BakedYardPieces.Contains(piece.Piece))
                    throw new InvalidOperationException("Unruled yard dressing: " + piece.Id + " / " + piece.Piece);
            }
            else if (piece.Kit != NineMileCreekDressing.DecorFamily && piece.Kit != NineMileCreekDressing.FindsFamily)
                throw new InvalidOperationException("Unknown dressing kit: " + piece.Id + " / " + piece.Kit);
            return row;
        }

        public static List<Row> Plan(IEnumerable<VillagePiece> pieces, IEnumerable<YardDef> yards,
            IEnumerable<CommonsDef> commons, IEnumerable<VillagePlacement> roadside)
        {
            var byId = pieces.ToDictionary(p => p.Id, StringComparer.Ordinal);
            var result = new List<Row>();
            var used = new HashSet<string>(StringComparer.Ordinal);
            void Add(string group, IEnumerable<VillagePlacement> placements, bool held)
            {
                foreach (var placement in placements)
                {
                    if (!used.Add(placement.PieceId))
                        throw new InvalidOperationException("Dressing is placed twice: " + placement.PieceId);
                    if (!byId.TryGetValue(placement.PieceId, out var piece))
                        throw new InvalidOperationException("Missing dressing piece: " + placement.PieceId);
                    var row = Resolve(group, piece, held);
                    row.Position = placement.Position;
                    result.Add(row);
                }
            }
            foreach (var yard in yards) Add(yard.Id, yard.Dressing, HoldsYard(yard.Id));
            foreach (var common in commons) Add(common.Id, common.Furniture, false);
            Add("roadside", roadside, false);
            return result;
        }

        public static List<Row> ReadPlan()
        {
            var plan = StPetersVillageYards.Read<VillagePlanDef>("village_plan.st_peters");
            return Plan(plan.Pieces,
                plan.YardIds.Select(StPetersVillageYards.Read<YardDef>),
                plan.CommonsIds.Select(StPetersVillageYards.Read<CommonsDef>), plan.Roadside);
        }

        public static bool Places(Row row) => row.Status == "place" || row.Status == "stand-in";

        public static int YardCell(YardKit.Contract contract, int dir)
        {
            if (contract == null || !YardCatalog.HasMeasuredBearings(contract))
                throw new InvalidOperationException("Yard dressing needs the registered facing table.");
            return YardCatalog.FacingFor(contract, StPetersLayerRefresh.KitDirHeading(dir));
        }

        public static Sprite SpriteFor(Row row)
        {
            if (!Places(row)) throw new InvalidOperationException("Dressing is not active: " + row.Piece.Id);
            switch (row.Kit)
            {
                case "yardIso":
                    var contract = YardKit.Load();
                    return YardCatalog.LoadFacing(YardCatalog.Find(contract, row.Key), YardCell(contract, row.Piece.Dir));
                case NineMileCreekDressing.DecorFamily:
                    return IsoPackSprites.Facing(row.Kit, row.Key,
                        IsoPackSprites.FacingForHeading(row.Kit, StPetersLayerRefresh.KitDirHeading(row.Piece.Dir)));
                case NineMileCreekDressing.FindsFamily:
                    return IsoPackSprites.Find(row.Key, "bleached", row.Piece.Dir, 0);
                default:
                    throw new InvalidOperationException("No dressing art for " + row.Piece.Id);
            }
        }

        public static GameObject Place(Transform yardsRoot)
        {
            if (yardsRoot == null) throw new ArgumentNullException(nameof(yardsRoot));
            var rows = ReadPlan().Where(Places).ToArray();
            var sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var sprite = SpriteFor(row);
                if (sprite == null) throw new InvalidOperationException("Missing dressing sheet: " + row.Piece.Id);
                sprites.Add(row.Piece.Id, sprite);
            }
            var commons = new GameObject(CommonsRoot);
            var groups = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                if (!groups.TryGetValue(row.Group, out var group))
                {
                    var groupGo = new GameObject(row.Group);
                    groupGo.transform.SetParent(row.Group.StartsWith("yard.", StringComparison.Ordinal)
                        ? yardsRoot : commons.transform, false);
                    group = groupGo.transform;
                    groups.Add(row.Group, group);
                }
                var go = new GameObject(row.Piece.Id);
                go.transform.SetParent(group, false);
                go.transform.position = new Vector3(row.Position.x, row.Position.y, 0f);
                if (row.Kit == "yardIso")
                    YardCatalog.Configure(go, YardCatalog.Find(row.Key), sprites[row.Piece.Id]);
                else
                {
                    go.AddComponent<SpriteRenderer>().sprite = sprites[row.Piece.Id];
                    go.AddComponent<YSortSprite>();
                }
            }
            return commons;
        }
    }
}
#endif
