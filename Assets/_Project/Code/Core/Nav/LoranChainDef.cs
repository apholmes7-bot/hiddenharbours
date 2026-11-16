using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>One SECONDARY station of a Loran chain: where it stands and the delay it waits after
    /// the master's pulse before it transmits its own (the coding delay).</summary>
    [Serializable]
    public struct LoranSecondary
    {
        [Tooltip("The station's letter on the receiver (W, X, Y, Z), as a skipper writes it beside the " +
                 "number. Flavour only; the index in the chain is what the maths uses.")]
        public string Letter;

        [Tooltip("Where the transmitter stands, in WORLD metres (+X east, +Y north) - the shared frame " +
                 "every region maps into, not one region's scene frame.")]
        public Vector2 Position;

        [Min(0f)]
        [Tooltip("How long (microseconds) this secondary waits after the master's pulse reaches it " +
                 "before it transmits. Only shifts the reading; it is what makes a real Loran number " +
                 "five digits long and different for every station.")]
        public float CodingDelayMicroseconds;
    }

    /// <summary>
    /// <b>One Loran-C chain</b> (owner ruling: "Loran before GPS: YES"): a master transmitter and
    /// two or more secondaries, as data (ADR 0003). The receiver measures the time difference (TD)
    /// between the master's pulse and each secondary's; each TD puts the boat on one hyperbola, and
    /// two of them cross at a fix. <see cref="LoranMath"/> does the arithmetic,
    /// <see cref="LoranReceiver"/> reads it at a position.
    ///
    /// <para><b>The error numbers live here, per chain</b>, because signal quality is a fact of the
    /// chain (how far the stations are, how hard they transmit), not of the set in the wheelhouse.
    /// They are a pure function of (worldSeed, position cell, game time bucket): no hidden randomness
    /// (rule 5), and the repeatable part is what made real Loran numbers worth writing down.</para>
    ///
    /// Create via Assets &gt; Create &gt; Hidden Harbours &gt; Nav &gt; Loran Chain, save in
    /// Data/Boats/Loran, and list it on the <see cref="LoranReceiverDef"/> that can track it.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Nav/Loran Chain", fileName = "LoranChain")]
    public sealed class LoranChainDef : ScriptableObject
    {
        public const string IdPrefix = "loran_chain.";

        [Header("Identity")]
        [Tooltip("Stable, append-only id (loran_chain.snake_case). A stored waypoint names its chain " +
                 "by this id, so it must never be renamed or reused.")]
        public string Id = IdPrefix;

        [Tooltip("Player-facing chain name. Flavour only.")]
        public string DisplayName = "";

        [Min(0)]
        [Tooltip("Group repetition interval, in tens of microseconds - the four-digit number a chain " +
                 "is known by on the set and in the almanac. Flavour shown on the receiver.")]
        public int GroupRepetitionInterval;

        [Header("Stations (world metres)")]
        [Tooltip("Where the master transmitter stands.")]
        public Vector2 Master;

        [Tooltip("The secondaries. Two or more; the receiver picks the pair whose lines cross best " +
                 "where the boat is.")]
        public LoranSecondary[] Secondaries = Array.Empty<LoranSecondary>();

        [Min(0f)]
        [Tooltip("Ground-wave range in metres. A boat farther than this from ANY station of the chain " +
                 "cannot use the chain at all.")]
        public float RangeMetres;

        [Header("Deterministic error")]
        [Min(0f)]
        [Tooltip("The REPEATABLE part of each TD's error (microseconds, before the range gain): the " +
                 "same at the same spot every visit for a given world seed. Return to your numbers and " +
                 "you return to your spot, even though the spot is not quite where the chart says.")]
        public float RepeatableErrorMicroseconds;

        [Min(0f)]
        [Tooltip("The DRIFTING part of each TD's error (microseconds, before the range gain): wanders " +
                 "slowly with game time, bucket to bucket.")]
        public float DriftErrorMicroseconds;

        [Min(1f)]
        [Tooltip("Size (metres) of the cell the error varies over. Smaller = a lumpier error field.")]
        public float ErrorCellMetres = 1f;

        [Min(0.01f)]
        [Tooltip("Game hours between drift values. The drift blends smoothly from one bucket's value " +
                 "to the next.")]
        public float DriftBucketHours = 1f;

        [Min(0f)]
        [Tooltip("How much worse the error gets toward the edge of range: the TD error is multiplied " +
                 "by 1 + gain x (range fraction)^2, so 3 = four times the error at the very edge.")]
        public float FringeErrorGain;

        /// <summary>What is wrong with this chain as authored, or null when it is usable. The one
        /// check the content test and the receiver both read.</summary>
        public string Problem()
        {
            if (string.IsNullOrWhiteSpace(Id) || Id == IdPrefix || !Id.StartsWith(IdPrefix, StringComparison.Ordinal))
                return "invalid chain id";
            if (Secondaries == null || Secondaries.Length < 2) return "a chain needs at least two secondaries";
            if (!NavMath.IsFinite(Master)) return "master position is not finite";
            for (int i = 0; i < Secondaries.Length; i++)
            {
                if (!NavMath.IsFinite(Secondaries[i].Position)) return "secondary position is not finite";
                if ((Secondaries[i].Position - Master).sqrMagnitude < 1f) return "secondary stands on the master";
                if (!IsUsable(Secondaries[i].CodingDelayMicroseconds)) return "invalid coding delay";
            }
            if (!(RangeMetres > 0f) || float.IsInfinity(RangeMetres)) return "invalid range";
            if (!IsUsable(RepeatableErrorMicroseconds) || !IsUsable(DriftErrorMicroseconds)
                || !IsUsable(FringeErrorGain)) return "invalid error model";
            if (!(ErrorCellMetres >= 1f) || float.IsInfinity(ErrorCellMetres)) return "invalid error cell";
            if (!(DriftBucketHours > 0f) || float.IsInfinity(DriftBucketHours)) return "invalid drift bucket";
            return null;
        }

        private static bool IsUsable(float v) => !float.IsNaN(v) && !float.IsInfinity(v) && v >= 0f;
    }
}
