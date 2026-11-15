using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>What a kitchen piece is. Every place a kitchen stands (a galley aboard, a home, a staffed
    /// shop, a player-built building) is described with these and nothing else.</summary>
    public enum KitchenPieceKind
    {
        /// <summary>A prep station: raw food is prepared here (a counter).</summary>
        Prep = 0,
        /// <summary>A cooktop: prepared food is cooked here.</summary>
        Cook = 1,
        /// <summary>A food store: pantry, cooler or fridge (<see cref="KitchenPiece.StorageDefId"/>).</summary>
        Storage = 2,
        /// <summary>A seat.</summary>
        Seat = 3,
        /// <summary>A table (never pressed; it makes a seat beside it a dining seat).</summary>
        Table = 4,
    }

    /// <summary>
    /// <b>One kitchen piece, location-agnostic</b> (kitchens slice 1): a kind, the floor it stands on, the
    /// point that matters for it and, for a store, which store. For a seat the point is where the sitter's
    /// hips rest; for a table, the centre of its top; for anything else, where the player stands to use it.
    /// Plan distances use x and y; z is height. A boat builds these from its interior anchors
    /// (<see cref="BoatInteriorKitchen"/>); a building lane builds them from its own data.
    /// </summary>
    public readonly struct KitchenPiece
    {
        public readonly string Id;
        public readonly KitchenPieceKind Kind;
        public readonly string Level;
        public readonly Vector3 Point;
        public readonly Vector3 ReachPoint;
        public readonly string StorageDefId;

        public KitchenPiece(string id, KitchenPieceKind kind, string level, Vector3 point, Vector3 reachPoint,
                            string storageDefId = "")
        {
            Id = id ?? "";
            Kind = kind;
            Level = level ?? "";
            Point = point;
            ReachPoint = reachPoint;
            StorageDefId = storageDefId ?? "";
        }

        /// <summary>Plan (x, y) distance from this piece's point to <paramref name="p"/>.</summary>
        public float PlanDistanceTo(Vector3 p)
        {
            float dx = Point.x - p.x, dy = Point.y - p.y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }

    /// <summary>The pure rules over a set of <see cref="KitchenPiece"/>s.</summary>
    public static class KitchenLayout
    {
        /// <summary>
        /// True when <paramref name="seat"/> is a DINING seat: a table top, or a prep counter, stands on the
        /// same floor within <paramref name="reachMetres"/> (plan) of where the sitter sits. A counter
        /// counts because a small boat's galley has no table: you eat at the counter you cooked on.
        /// </summary>
        public static bool IsDiningSeat(in KitchenPiece seat, IReadOnlyList<KitchenPiece> pieces, float reachMetres)
        {
            if (seat.Kind != KitchenPieceKind.Seat || pieces == null || !(reachMetres > 0f)) return false;
            for (int i = 0; i < pieces.Count; i++)
            {
                KitchenPiece p = pieces[i];
                if (p.Kind != KitchenPieceKind.Table && p.Kind != KitchenPieceKind.Prep) continue;
                if (!string.Equals(p.Level, seat.Level, System.StringComparison.Ordinal)) continue;
                if (p.PlanDistanceTo(seat.Point) <= reachMetres) return true;
            }
            return false;
        }

        /// <summary><see cref="IsDiningSeat(in KitchenPiece, IReadOnlyList{KitchenPiece}, float)"/> at the
        /// configured reach.</summary>
        public static bool IsDiningSeat(in KitchenPiece seat, IReadOnlyList<KitchenPiece> pieces, in KitchenSettings settings)
            => IsDiningSeat(seat, pieces, settings.DiningSeatReachInForce);

        /// <summary>How many pieces of <paramref name="kind"/> are in the set.</summary>
        public static int CountOf(IReadOnlyList<KitchenPiece> pieces, KitchenPieceKind kind)
        {
            if (pieces == null) return 0;
            int n = 0;
            for (int i = 0; i < pieces.Count; i++) if (pieces[i].Kind == kind) n++;
            return n;
        }
    }

    /// <summary>How the player was placed when they ate.</summary>
    public enum EatingPosture
    {
        /// <summary>On their feet: the food's own relief.</summary>
        Standing = 0,
        /// <summary>Sat on a seat with no table or counter by it.</summary>
        Seated = 1,
        /// <summary>Sat at a dining seat (by a table or counter): the greater food boost.</summary>
        AtTable = 2,
    }

    /// <summary>
    /// <b>The meal and the seat</b> (kitchens slice 1, owner ask 2026-11-15): "players can also seat and
    /// dine which gives them a greater food boost, sitting also recovers tiredness to maximum amount that
    /// does not match sleeping". Pure maths, every number from <see cref="KitchenSettings"/>.
    /// </summary>
    public static class DiningRules
    {
        /// <summary>The multiplier a posture gives a meal: 1 standing, the seated one, the dining one.</summary>
        public static float Multiplier(EatingPosture posture, in KitchenSettings settings)
        {
            switch (posture)
            {
                case EatingPosture.AtTable: return settings.DiningReliefInForce;
                case EatingPosture.Seated: return settings.SeatedReliefInForce;
                default: return 1f;
            }
        }

        /// <summary>The relief a meal of <paramref name="baseRelief"/> gives in a posture (0 for a non-positive meal).</summary>
        public static float Relief(float baseRelief, EatingPosture posture, in KitchenSettings settings)
            => baseRelief > 0f ? baseRelief * Multiplier(posture, settings) : 0f;

        /// <summary>The posture a seat gives.</summary>
        public static EatingPosture PostureAt(bool seated, bool diningSeat)
            => !seated ? EatingPosture.Standing : diningSeat ? EatingPosture.AtTable : EatingPosture.Seated;
    }

    /// <summary>
    /// <b>Tiredness while seated</b>: it drifts toward the sitting floor and stops there. Above the floor
    /// it falls at <see cref="KitchenSettings.SitRecoveryPerGameHour"/>; at or below it (rested already) it
    /// keeps rising at the need's own rate, but no further than the floor. So sitting can take the edge off
    /// but never bring tiredness under the floor, which is strictly above 0, where a night's sleep lands.
    ///
    /// <para><b>Stepping equals one shot.</b> Each branch is a clamped straight line toward the floor, so
    /// sitting for a then b hours lands where sitting a + b hours does (to float rounding).</para>
    /// </summary>
    public static class SeatedRest
    {
        /// <summary>The tiredness after <paramref name="hours"/> seated from <paramref name="level"/>.</summary>
        public static float LevelAfter(float level, double hours, float risePerGameHour, in KitchenSettings settings)
        {
            float l = level < 0f ? 0f : level > 1f ? 1f : level;
            if (!(hours > 0d)) return l;
            float floor = settings.SitRestFloorInForce;
            if (l > floor)
            {
                double down = l - settings.SitRecoveryInForce * hours;
                return down <= floor ? floor : (float)down;
            }
            if (!(risePerGameHour > 0f)) return l;
            double up = l + risePerGameHour * hours;
            return up >= floor ? floor : (float)up;
        }
    }
}
