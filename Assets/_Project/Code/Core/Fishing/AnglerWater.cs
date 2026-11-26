using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Where a villager's cast may land at the tide</b>, decided once per session slot (P1: the sea has
    /// moods, and the tide is one of them). Whether he casts at all, how far, the moment in the slot the
    /// falling tide leaves his bobber dry (he reels in then), and the waterline his fish is held off so she is
    /// never drawn across bared mud. Pure value, no allocation.
    ///
    /// <para>An unset plan (<see cref="IsSet"/> false, the default: no water truth to read) changes nothing,
    /// so a scene without tidal ground plays exactly as before.</para>
    ///
    /// FLAG lead-architect: new Core value (the angler water seam).
    /// </summary>
    public struct AnglerWater
    {
        /// <summary>False: no water truth was read; the script's own reach, no checks.</summary>
        public bool IsSet;

        /// <summary>False: no point in reach is water at the slot's start, so he does not cast this slot.</summary>
        public bool Casts;

        /// <summary>How far he casts this slot (m): the script's reach, or longer when that lands dry.</summary>
        public float ReachM;

        /// <summary>The moment into the slot (s) the tide first leaves the bobber dry, on the script's re-check
        /// cadence; positive infinity when it stays wet for the whole session.</summary>
        public double DryAtSeconds;

        /// <summary>The waterline between him and the bobber at the slot's start, measured from his stand (the
        /// frame the offsets are in); unset when the water reaches in to him.</summary>
        public ShoreEdge Waterline;

        /// <summary>True when this slot's cast is longer than the script's usual reach.</summary>
        public bool IsLongCast(in AnglerScript script) => IsSet && Casts && ReachM > script.CastReachM;
    }

    /// <summary>
    /// <b>The pure decision behind <see cref="AnglerWater"/></b>, over the same depth truth the player's cast
    /// reads (water level at the time less the ground there, <see cref="TidalExposure.WaterDepth"/>), which
    /// arrives as an injected probe so the decision is EditMode-testable (rule 5). Equal inputs give the equal
    /// plan: no clock, no randomness, nothing saved. The tide is a pure function of time, so the re-checks
    /// through the session are read up front, at fixed game-time steps from the slot's start: the same answer
    /// at any frame rate, worked out once per slot and never per frame.
    /// </summary>
    public static class AnglerWaterMath
    {
        /// <summary>The water depth (m) at a world point at a game time (s); at or below zero is dry.</summary>
        public delegate float DepthAt(Vector2 worldPoint, double totalSeconds);

        /// <summary>How many reaches are tried between the usual and the longest cast, and how many points the
        /// line in to him is sampled at for the waterline: a resolution bound, not a feel dial.</summary>
        public const int ReachSamples = 16;

        /// <summary>The most re-checks read for one session: a work bound, not a feel dial (a script whose
        /// cadence would need more is re-checked over its first this-many steps).</summary>
        public const int MaxRechecks = 64;

        /// <summary>
        /// The plan for one slot. <paramref name="origin"/> is his position in the world, <paramref name="direction"/>
        /// the slot's cast direction (unit), <paramref name="edge"/> the deck edge measured from him, and
        /// <paramref name="slotStartTotal"/> the slot's start on the tide's clock. A null probe or an invalid
        /// script gives the unset plan.
        /// </summary>
        public static AnglerWater Plan(in AnglerScript script, Vector2 origin, Vector2 direction, in ShoreEdge edge,
                                       double slotStartTotal, DepthAt depthAt)
        {
            var plan = new AnglerWater { DryAtSeconds = double.PositiveInfinity };
            if (depthAt == null || !script.IsValid(out _)) return plan;
            plan.IsSet = true;

            // The reach: his usual one if it lands in water deep enough, else the shortest longer one that does.
            float reach = float.NaN;
            if (DeepEnough(in script, origin, direction, in edge, script.CastReachM, slotStartTotal, depthAt))
                reach = script.CastReachM;
            else if (script.LongCastReachM > script.CastReachM)
            {
                for (int i = 1; i <= ReachSamples; i++)
                {
                    float r = Mathf.Lerp(script.CastReachM, script.LongCastReachM, i / (float)ReachSamples);
                    if (!DeepEnough(in script, origin, direction, in edge, r, slotStartTotal, depthAt)) continue;
                    reach = r;
                    break;
                }
            }
            if (float.IsNaN(reach)) return plan;   // dry everywhere in reach: he stands down this slot
            plan.Casts = true;
            plan.ReachM = reach;

            // The falling tide: the first re-check that finds the bobber dry.
            Vector2 bobber = origin + edge.Clamp(direction * reach);
            if (script.WaterRecheckSeconds > 0f)
            {
                for (int k = 1; k <= MaxRechecks; k++)
                {
                    double at = k * (double)script.WaterRecheckSeconds;
                    if (at >= script.LongestSessionSeconds) break;
                    if (depthAt(bobber, slotStartTotal + at) > 0f) continue;
                    plan.DryAtSeconds = at;
                    break;
                }
            }

            // The waterline: walking in from the bobber, the last point still wet before the ground is bared.
            float wetFrom = reach;
            for (int i = ReachSamples - 1; i >= 0; i--)
            {
                float r = reach * i / ReachSamples;
                if (depthAt(origin + direction * r, slotStartTotal) <= 0f) break;
                wetFrom = r;
            }
            if (wetFrom > 0f)
                plan.Waterline = new ShoreEdge(direction * wetFrom, direction, edge.IsSet ? edge.MarginM : 0f);
            return plan;
        }

        private static bool DeepEnough(in AnglerScript script, Vector2 origin, Vector2 direction, in ShoreEdge edge,
                                       float reach, double total, DepthAt depthAt)
            => depthAt(origin + edge.Clamp(direction * reach), total) > script.MinBobberDepthM;
    }
}
