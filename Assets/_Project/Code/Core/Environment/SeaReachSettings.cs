using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The owner's dials for <b>which water the sea's swell reaches</b> (the owner, 2026-11-19: "pools of
    /// water which were disconnected from the sea still followed the same pulsing wave swells despite being
    /// still water, once they arent attached to the waves they should remain still pools of water").
    ///
    /// <para>The region's seabed is flooded once from the open sea (<see cref="SeaReachField"/>): every
    /// cell learns the lowest water level at which it joins the sea. A pool the falling tide has cut off
    /// stands below that level and draws still: no swell lift, no breathing edge, no surf or shore foam.
    /// As the rising tide tops the sill again the swell fades back in over <see cref="ReconnectBlendMeters"/>
    /// of tide, never popping.</para>
    ///
    /// <para>Presentation and hull ride only: nothing saved, recomputed from the terrain and the
    /// deterministic tide (rule 5).</para>
    /// </summary>
    [Serializable]
    public struct SeaReachSettings
    {
        [Tooltip("1 = water the falling tide has cut off from the open sea draws still (no swell, no " +
                 "pulsing edge, no surf foam). 0 = every water body moves with the sea, the older look.")]
        [Range(0f, 1f)] public float Enabled;

        [Tooltip("How much tide (m) it takes for a reconnected pool to take the full swell again once the " +
                 "rising water tops its sill. The swell eases in over this rise and eases out over the same " +
                 "fall, so a pool never pops. Must be above 0.")]
        [Range(0.01f, 2f)] public float ReconnectBlendMeters;

        [Tooltip("The open sea: map-edge seabed at or below this height (m above chart datum) is the ocean " +
                 "the swell comes from. Keep it below the lowest tide, so a pond or a lake that reaches the " +
                 "map edge is never mistaken for the sea.")]
        [Range(-20f, 0f)] public float SeedMaxElevationMeters;

        [Tooltip("A hollow shallower than this (m, from its floor up to the lip it spills over) still moves " +
                 "with the sea: ripples in the sand on a beach must not freeze the wash running over them.")]
        [Range(0f, 0.5f)] public float MinPoolDepthMeters;

        [Tooltip("The height step (m) the flood sorts the seabed by. Smaller is more exact and costs more " +
                 "memory at load; a millimetre is finer than any painted height map's own step.")]
        [Range(0.0001f, 0.05f)] public float QuantumMeters;

        [Tooltip("How deep (m) a seabed cell must stand under the water to count fully when the reach is " +
                 "smoothed between neighbouring cells. Dry ground never counts, so a pool's rim stays still " +
                 "and the sea beside a dry bar keeps its swell; a reconnecting pool's outline is feathered " +
                 "instead of drawn in cell steps. Must be above 0.")]
        [Range(0.001f, 0.5f)] public float WetBandMeters;

        /// <summary>The shipped values (<c>SeaReachTests</c> pins the config asset against these).</summary>
        public static SeaReachSettings Default => new SeaReachSettings
        {
            Enabled = 1f,
            ReconnectBlendMeters = 0.25f,
            SeedMaxElevationMeters = -2.5f,
            MinPoolDepthMeters = 0.05f,
            QuantumMeters = 0.001f,
            WetBandMeters = 0.05f,
        };

        /// <summary>True when the dials describe a usable flood: on, a strictly positive blend and quantum,
        /// and finite numbers throughout. Anything else draws the older look (every water moves).</summary>
        public bool IsUsable =>
            Enabled > 0.5f &&
            ReconnectBlendMeters > 0f && !float.IsInfinity(ReconnectBlendMeters) &&
            QuantumMeters > 0f && !float.IsInfinity(QuantumMeters) &&
            !float.IsNaN(SeedMaxElevationMeters) && !float.IsInfinity(SeedMaxElevationMeters) &&
            MinPoolDepthMeters >= 0f && !float.IsInfinity(MinPoolDepthMeters) &&
            WetBandMeters > 0f && !float.IsInfinity(WetBandMeters);
    }
}
