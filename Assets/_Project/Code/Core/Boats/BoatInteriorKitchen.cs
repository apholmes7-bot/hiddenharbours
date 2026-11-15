using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A hull's galley as kitchen pieces</b> (kitchens slice 1): the only boat-specific step between a
    /// <see cref="BoatInteriorDef"/>'s anchors and the location-agnostic rules (<see cref="KitchenLayout"/>,
    /// <see cref="KitchenStores"/>). A building lane writes its own twin of this over its own data.
    /// </summary>
    public static class BoatInteriorKitchen
    {
        /// <summary>The prefix every fixture aboard carries; the store and seat keys share the bunk's shape.</summary>
        public const string FixtureKeyPrefix = "fixture.boat.";

        /// <summary>The storage KEY (and fixture id) of a food store, seat or bunk anchor aboard this
        /// interior: <c>fixture.boat.&lt;interior id&gt;.&lt;anchor id&gt;</c>, the bunk's id shape. Two boats of
        /// the same hull share it today (one boat per hull is all main can own).</summary>
        public static string FixtureKey(BoatInteriorDef def, BoatInteriorAnchor anchor)
            => def == null || anchor == null ? "" : FixtureKeyPrefix + def.Id + "." + anchor.Id;

        /// <summary>The kind of piece an anchor is, or false for an anchor that is not part of a kitchen.</summary>
        public static bool TryKind(BoatInteriorAnchor anchor, out KitchenPieceKind kind)
        {
            kind = default;
            if (anchor == null) return false;
            switch (anchor.Action)
            {
                case BoatInteriorAnchor.ActionPrep: kind = KitchenPieceKind.Prep; return true;
                case BoatInteriorAnchor.ActionCook: kind = KitchenPieceKind.Cook; return true;
                case BoatInteriorAnchor.ActionFoodStorage: kind = KitchenPieceKind.Storage; return true;
                case BoatInteriorAnchor.ActionSit: kind = KitchenPieceKind.Seat; return true;
                case BoatInteriorAnchor.ActionTable: kind = KitchenPieceKind.Table; return true;
                default: return false;
            }
        }

        /// <summary>
        /// Every kitchen piece aboard. A seat's or table's point is its furniture point on its furniture
        /// level; any other piece stands at its reach point on <paramref name="levelOfReach"/>'s answer (the
        /// level a reach point stands on: the caller resolves it from the def's levels).
        /// </summary>
        public static List<KitchenPiece> Pieces(BoatInteriorDef def, Func<BoatInteriorAnchor, string> levelOfReach)
        {
            var pieces = new List<KitchenPiece>();
            if (def?.Anchors == null) return pieces;
            for (int i = 0; i < def.Anchors.Length; i++)
            {
                BoatInteriorAnchor a = def.Anchors[i];
                if (!TryKind(a, out KitchenPieceKind kind)) continue;
                bool furniture = (kind == KitchenPieceKind.Seat || kind == KitchenPieceKind.Table) && a.HasFurnitureGeometry;
                string level = furniture ? a.FurnitureLevel : levelOfReach?.Invoke(a) ?? "";
                pieces.Add(new KitchenPiece(FixtureKey(def, a), kind, level,
                                            furniture ? a.FurniturePoint : a.ReachPoint, a.ReachPoint,
                                            a.KitchenStorageId));
            }
            return pieces;
        }

        /// <summary>
        /// The level a reach point stands on: the def level whose sole height is nearest the point's z within
        /// <see cref="BoatInteriorDef.FloorTolerance"/>, or empty.
        /// </summary>
        public static string LevelOfReach(BoatInteriorDef def, BoatInteriorAnchor anchor)
        {
            if (def?.Levels == null || anchor == null) return "";
            string best = "";
            float bestGap = float.MaxValue;
            for (int i = 0; i < def.Levels.Length; i++)
            {
                BoatInteriorLevel level = def.Levels[i];
                if (level == null) continue;
                float gap = Math.Abs(level.SoleZMeters - anchor.ReachPoint.z);
                if (gap > def.FloorTolerance || gap >= bestGap) continue;
                // Two levels can share a height and a plan (a packet's main deck runs under its house sole,
                // both z 5): the floor it stands on holds the point in its outline and not inside one of
                // its own obstructions (on the main deck the galley is inside the deckhouse box).
                var p = new Vector2(anchor.ReachPoint.x, anchor.ReachPoint.y);
                if (!Inside(level.Outline, p) || InAnyObstruction(level, p)) continue;
                best = level.Id;
                bestGap = gap;
            }
            return best;
        }

        static bool InAnyObstruction(BoatInteriorLevel level, Vector2 p)
        {
            if (level.Obstructions == null) return false;
            for (int i = 0; i < level.Obstructions.Length; i++)
                if (level.Obstructions[i] != null && Inside(level.Obstructions[i].Footprint, p)) return true;
            return false;
        }

        /// <summary>Even-odd point-in-polygon (plan).</summary>
        public static bool Inside(Vector2[] polygon, Vector2 p)
        {
            if (polygon == null || polygon.Length < 3) return false;
            bool c = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i], b = polygon[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) c = !c;
            }
            return c;
        }

        /// <summary><see cref="Pieces(BoatInteriorDef, Func{BoatInteriorAnchor, string})"/> with each reach point's
        /// level found by <see cref="LevelOfReach"/>.</summary>
        public static List<KitchenPiece> Pieces(BoatInteriorDef def) => Pieces(def, a => LevelOfReach(def, a));
    }
}
