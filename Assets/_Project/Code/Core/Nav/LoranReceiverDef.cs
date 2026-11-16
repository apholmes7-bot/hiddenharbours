using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The Loran set in the wheelhouse</b>, as data: which chains it can track, how finely it
    /// shows a time difference, when it blinks, how many sets of numbers it remembers, and the
    /// propagation speed its arithmetic assumes. Pairs with the purchasable
    /// <c>instrument.loran</c> offer (Economy) and the helm slot (<c>HelmConsoleDef.SupportsLoran</c>);
    /// this Def is the BEHAVIOUR, those are the price and the place.
    ///
    /// <para>No number in <see cref="LoranMath"/> or <see cref="LoranReceiver"/> is a literal: every
    /// tunable is here or on a <see cref="LoranChainDef"/> (rule 6).</para>
    ///
    /// Create via Assets &gt; Create &gt; Hidden Harbours &gt; Nav &gt; Loran Receiver, save in
    /// Data/Boats/Loran.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Nav/Loran Receiver", fileName = "LoranReceiver")]
    public sealed class LoranReceiverDef : ScriptableObject
    {
        public const string IdPrefix = "loran_receiver.";

        [Header("Identity")]
        [Tooltip("Stable, append-only id (loran_receiver.snake_case).")]
        public string Id = IdPrefix;

        [Header("Chains")]
        [Tooltip("Every chain this set can track. It reads the one that gives the best fix where the " +
                 "boat is; a waypoint saved on one chain still steers while the set reads another.")]
        public LoranChainDef[] Chains = Array.Empty<LoranChainDef>();

        [Header("Signal")]
        [Min(1f)]
        [Tooltip("Assumed ground-wave speed, metres per microsecond. A touch under light in vacuum " +
                 "(299.79), as the real chains assumed over salt water.")]
        public float PropagationMetresPerMicrosecond = 299.69f;

        [Min(0.001f)]
        [Tooltip("The smallest step the TD readout shows, microseconds. 0.1 = the classic one-decimal " +
                 "readout; a stored waypoint keeps exactly what was shown.")]
        public float DisplayResolutionMicroseconds = 0.1f;

        [Range(0f, 1f)]
        [Tooltip("Past this fraction of a chain's range the set still reads, but BLINKS: the numbers " +
                 "are there and not to be trusted.")]
        public float FringeRangeFraction = 0.85f;

        [Min(1f)]
        [Tooltip("Geometry, in metres of position error per microsecond of TD error. Worse than this " +
                 "(lines crossing at a shallow angle, near a baseline extension) and the set BLINKS. " +
                 "The best possible geometry is about 212.")]
        public float BlinkGeometryMetresPerMicrosecond = 1200f;

        [Min(1f)]
        [Tooltip("Worse than this geometry and the pair gives no fix at all (the two lines are near " +
                 "parallel). Must be at least the blink value.")]
        public float NoFixGeometryMetresPerMicrosecond = 5000f;

        [Header("Memory")]
        [Min(1)]
        [Tooltip("How many sets of numbers the set remembers. When full it refuses another rather than " +
                 "dropping an old one - the chartplotter's rule.")]
        public int MaxStoredWaypoints = 10;

        [Header("Solver")]
        [Min(1)]
        [Tooltip("Most Newton steps when turning two TDs back into a position.")]
        public int SolverIterations = 24;

        [Min(0.001f)]
        [Tooltip("The solver stops once a step moves the position less than this many metres.")]
        public float SolverToleranceMetres = 0.25f;

        [Min(1f)]
        [Tooltip("Largest single solver step, metres - keeps a far guess from leaping to the wrong " +
                 "branch of a hyperbola.")]
        public float SolverMaxStepMetres = 5000f;

        /// <summary>What is wrong with this receiver as authored, or null when it is usable.</summary>
        public string Problem()
        {
            if (string.IsNullOrWhiteSpace(Id) || Id == IdPrefix || !Id.StartsWith(IdPrefix, StringComparison.Ordinal))
                return "invalid receiver id";
            if (Chains == null || Chains.Length == 0) return "a receiver needs at least one chain";
            for (int i = 0; i < Chains.Length; i++)
            {
                if (Chains[i] == null) return "missing chain";
                string chain = Chains[i].Problem();
                if (chain != null) return Chains[i].Id + ": " + chain;
                for (int j = 0; j < i; j++)
                    if (Chains[j] != null && Chains[j].Id == Chains[i].Id) return "duplicate chain id";
            }
            if (!Finite(PropagationMetresPerMicrosecond) || PropagationMetresPerMicrosecond < 1f) return "invalid propagation speed";
            if (!Finite(DisplayResolutionMicroseconds) || DisplayResolutionMicroseconds <= 0f) return "invalid display resolution";
            if (!Finite(FringeRangeFraction) || FringeRangeFraction < 0f || FringeRangeFraction > 1f) return "invalid fringe fraction";
            if (!Finite(BlinkGeometryMetresPerMicrosecond) || !Finite(NoFixGeometryMetresPerMicrosecond)
                || BlinkGeometryMetresPerMicrosecond < 1f
                || NoFixGeometryMetresPerMicrosecond < BlinkGeometryMetresPerMicrosecond) return "invalid geometry limits";
            if (MaxStoredWaypoints < 1) return "invalid waypoint capacity";
            if (SolverIterations < 1 || !Finite(SolverToleranceMetres) || SolverToleranceMetres <= 0f
                || !Finite(SolverMaxStepMetres) || SolverMaxStepMetres < 1f) return "invalid solver settings";
            return null;
        }

        /// <summary>The chain with this id, or null. A linear scan of a handful of entries; no
        /// allocation.</summary>
        public LoranChainDef ChainById(string chainId)
        {
            if (Chains == null || string.IsNullOrEmpty(chainId)) return null;
            for (int i = 0; i < Chains.Length; i++)
                if (Chains[i] != null && string.Equals(Chains[i].Id, chainId, StringComparison.Ordinal))
                    return Chains[i];
            return null;
        }

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
