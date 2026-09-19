using System;
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>Lands each placed route end on the floor it names — or refuses the route.</b> The reader
    /// places a companionway from its opening and a ladder leg from its base; this asks whether the spot
    /// each end came out at is somewhere she can actually stand: on the named interior level at its sole
    /// height, or on the named exterior deck area at that area's height there, within the def's landing
    /// tolerance, in plan AND in height.
    ///
    /// <para><b>A refusal is loud and per link, never per hull.</b> The route stays in the def,
    /// unplaced, with the end, the floor and the metres in its reason, and the builder prints it. Her
    /// rooms and her door are sound and the rest of her links still walk, so the hull is not refused.
    /// The 53's flybridge ladder is the case this exists for: its head names <c>bridge_sole</c> but sits
    /// over the aft deck (Phase A, A1), and a walk that trusted it would stand her in the air.</para>
    ///
    /// <para>Pure — no AssetDatabase, no scene; EditMode-testable from plain arrays.</para>
    /// </summary>
    public static class BoatInteriorRouteLanding
    {
        /// <summary>
        /// Land every placed route. A route whose end misses is set unplaced with the reason. Routes the
        /// reader already left unplaced are passed over — their reason is the reader's, and stands.
        /// </summary>
        /// <returns>One line per route refused here, naming the hull, the route and the metres.</returns>
        public static List<string> Land(string hull, BoatInteriorRoute[] routes, BoatInteriorLevel[] levels,
                                        DeckArea[] deckAreas, float toleranceMetres)
        {
            var refusals = new List<string>();
            if (routes == null) return refusals;

            for (int i = 0; i < routes.Length; i++)
            {
                BoatInteriorRoute r = routes[i];
                if (r == null || !r.Placed) continue;

                string from = WhyNotLanded("from", r.FromLevel, r.FromPoint, levels, deckAreas, toleranceMetres);
                string to = WhyNotLanded("to", r.ToLevel, r.ToPoint, levels, deckAreas, toleranceMetres);
                if (from == null && to == null) continue;

                r.Placed = false;
                r.NotPlacedBecause = from != null && to != null ? from + "; " + to : from ?? to;
                refusals.Add($"{hull}: route '{r.Id}' ({r.FromLevel} → {r.ToLevel}) refused — {r.NotPlacedBecause}");
            }
            return refusals;
        }

        /// <summary>
        /// Null when <paramref name="point"/> stands on the floor <paramref name="floorId"/> names —
        /// an interior level first, else any exterior deck area of that id — within
        /// <paramref name="tolerance"/> in plan and in height. Otherwise why not, in metres.
        /// </summary>
        public static string WhyNotLanded(string end, string floorId, Vector3 point, BoatInteriorLevel[] levels,
                                          DeckArea[] deckAreas, float tolerance)
        {
            var spot = new Vector2(point.x, point.y);
            string at = $"its {end} end ({point.x:0.00}, {point.y:0.00}, {point.z:0.00})";
            if (string.IsNullOrEmpty(floorId)) return $"{at} names no floor";

            BoatInteriorLevel level = LevelById(levels, floorId);
            if (level != null)
            {
                if (!level.IsUsable()) return $"{at} names '{floorId}', a level with no outline to stand on";
                float plan = PlanDistance(level.Outline, spot, out _);
                float dz = point.z - level.SoleZMeters;
                if (plan <= tolerance && Mathf.Abs(dz) <= tolerance) return null;
                return Miss(at, floorId, plan, dz, level.SoleZMeters, tolerance)
                     + Under(spot, point.z, floorId, levels, deckAreas, tolerance);
            }

            bool named = false;
            float bestPlan = float.PositiveInfinity, bestDz = 0f, bestFloor = 0f;
            if (deckAreas != null)
                for (int i = 0; i < deckAreas.Length; i++)
                {
                    DeckArea a = deckAreas[i];
                    if (a == null || a.Outline == null || a.Outline.Length < 3 ||
                        !string.Equals(a.Id, floorId, StringComparison.Ordinal)) continue;
                    named = true;

                    float plan = PlanDistance(a.Outline, spot, out Vector2 onIt);
                    float floor = DeckAreaMath.HeightAt(a.HeightPlane, onIt);
                    float dz = point.z - floor;
                    if (plan <= tolerance && Mathf.Abs(dz) <= tolerance) return null;
                    if (plan < bestPlan - 1e-4f || (plan <= bestPlan + 1e-4f && Mathf.Abs(dz) < Mathf.Abs(bestDz)))
                    {
                        bestPlan = plan;
                        bestDz = dz;
                        bestFloor = floor;
                    }
                }

            if (!named)
                return deckAreas == null
                    ? $"{at} names '{floorId}', which is not a level of this interior, and her hull has no " +
                      "deck def to find it on"
                    : $"{at} names '{floorId}', which is neither a level of this interior nor a deck area " +
                      "of her hull";
            return Miss(at, floorId, bestPlan, bestDz, bestFloor, tolerance)
                 + Under(spot, point.z, floorId, levels, deckAreas, tolerance);
        }

        static string Miss(string at, string floorId, float plan, float dz, float floor, float tolerance)
            => $"{at} misses '{floorId}' by {plan:0.00} m in plan and {dz:+0.00;-0.00;0.00} m in height " +
               $"(that floor is at {floor:0.00} m there; tolerance {tolerance:0.00} m)";

        /// <summary>What she would actually stand on at that spot, if anything — the line an art brief
        /// needs. Of every floor lying under the spot in plan, the nearest in height.</summary>
        static string Under(Vector2 spot, float z, string named, BoatInteriorLevel[] levels, DeckArea[] deckAreas,
                            float tolerance)
        {
            string bestId = null;
            float bestFloor = 0f, bestGap = float.PositiveInfinity;

            if (levels != null)
                for (int i = 0; i < levels.Length; i++)
                {
                    BoatInteriorLevel l = levels[i];
                    if (l == null || !l.IsUsable() || PlanDistance(l.Outline, spot, out _) > tolerance) continue;
                    float gap = Mathf.Abs(z - l.SoleZMeters);
                    if (gap < bestGap) { bestGap = gap; bestFloor = l.SoleZMeters; bestId = l.Id; }
                }
            if (deckAreas != null)
                for (int i = 0; i < deckAreas.Length; i++)
                {
                    DeckArea a = deckAreas[i];
                    if (a == null || a.Outline == null || a.Outline.Length < 3) continue;
                    if (PlanDistance(a.Outline, spot, out Vector2 onIt) > tolerance) continue;
                    float floor = DeckAreaMath.HeightAt(a.HeightPlane, onIt);
                    float gap = Mathf.Abs(z - floor);
                    if (gap < bestGap) { bestGap = gap; bestFloor = floor; bestId = a.Id; }
                }

            if (bestId == null) return " — no floor of hers lies under that spot";
            return string.Equals(bestId, named, StringComparison.Ordinal)
                ? ""
                : $" — the floor under that spot is '{bestId}' at {bestFloor:0.00} m";
        }

        /// <summary>0 inside the outline; else the plan distance to its nearest edge.
        /// <paramref name="onIt"/> is the spot itself inside, else that nearest edge point.</summary>
        static float PlanDistance(Vector2[] outline, Vector2 spot, out Vector2 onIt)
        {
            if (DeckAreaMath.Contains(outline, spot))
            {
                onIt = spot;
                return 0f;
            }
            onIt = DeckAreaMath.ClosestPointOnOutline(outline, spot, out float sqr);
            return Mathf.Sqrt(sqr);
        }

        static BoatInteriorLevel LevelById(BoatInteriorLevel[] levels, string id)
        {
            if (levels == null) return null;
            for (int i = 0; i < levels.Length; i++)
                if (levels[i] != null && string.Equals(levels[i].Id, id, StringComparison.Ordinal))
                    return levels[i];
            return null;
        }
    }
}
