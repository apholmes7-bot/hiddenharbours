using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>What the Loran set's lamp says. Values may be persisted by a later save field:
    /// append only, never renumber.</summary>
    public enum LoranSignal
    {
        NotFitted = 0,   // no Loran in this helm: the instrument draws nothing
        NoSignal  = 1,   // fitted and on, but no chain gives a fix here (out of range, lines parallel)
        Blink     = 2,   // numbers shown but BLINKING: fringe of range or poor crossing - do not trust
        Fix       = 3,   // a good fix
    }

    /// <summary>
    /// <b>The Loran read model</b>: everything the instrument view draws and everything a later
    /// autopilot consumes, as one value. Two TD numbers, the lamp, which chain and stations, and the
    /// position those numbers resolve to (the receiver's idea of where the boat is, carrying the
    /// chain's error, not the boat's true position).
    /// </summary>
    public readonly struct LoranReading
    {
        public readonly LoranSignal Signal;
        /// <summary>The chain read (<see cref="LoranChainDef.Id"/>); null without TDs.</summary>
        public readonly string ChainId;
        /// <summary>The chain's GRI, the four-digit number shown beside the TDs.</summary>
        public readonly int GroupRepetitionInterval;
        /// <summary>Indexes of the two secondaries in the chain, and their letters.</summary>
        public readonly int SecondaryA, SecondaryB;
        public readonly string LetterA, LetterB;
        /// <summary>The two time differences as SHOWN (quantized to the readout step), microseconds.</summary>
        public readonly double TdA, TdB;
        /// <summary>True when <see cref="FixPosition"/> is a resolved position.</summary>
        public readonly bool HasFix;
        /// <summary>Where the shown TDs put the boat, in world metres. Carries the chain's error.</summary>
        public readonly Vector2 FixPosition;
        /// <summary>Metres of position error per microsecond of TD error at this spot (the geometry
        /// the lamp judged). Hidden from the player; for tests, tuning and a later "quality" needle.</summary>
        public readonly float GeometryMetresPerMicrosecond;

        public LoranReading(LoranSignal signal, string chainId, int gri, int secondaryA, int secondaryB,
                            string letterA, string letterB, double tdA, double tdB, bool hasFix,
                            Vector2 fixPosition, float geometry)
        {
            Signal = signal; ChainId = chainId; GroupRepetitionInterval = gri;
            SecondaryA = secondaryA; SecondaryB = secondaryB; LetterA = letterA; LetterB = letterB;
            TdA = tdA; TdB = tdB; HasFix = hasFix; FixPosition = fixPosition;
            GeometryMetresPerMicrosecond = geometry;
        }

        public static LoranReading Off(LoranSignal signal)
            => new LoranReading(signal, null, 0, -1, -1, null, null, 0.0, 0.0, false, Vector2.zero, float.PositiveInfinity);

        /// <summary>True when the set shows numbers (lit or blinking) that can be written down.</summary>
        public bool HasNumbers => Signal == LoranSignal.Fix || Signal == LoranSignal.Blink;
    }

    /// <summary>Steering to a stored set of numbers: bearing and distance from the current fix.</summary>
    public readonly struct LoranSteer
    {
        public readonly bool HasSteer;
        public readonly int Number;
        /// <summary>Compass bearing from the current fix to the waypoint (0 = N, clockwise).</summary>
        public readonly float BearingDegrees;
        public readonly float DistanceMetres;
        /// <summary>Where the waypoint's numbers resolve, world metres (what an autopilot steers at).</summary>
        public readonly Vector2 WaypointPosition;

        public LoranSteer(int number, float bearingDegrees, float distanceMetres, Vector2 waypointPosition)
        {
            HasSteer = true; Number = number; BearingDegrees = bearingDegrees;
            DistanceMetres = distanceMetres; WaypointPosition = waypointPosition;
        }

        public static LoranSteer None => default;
    }

    /// <summary>
    /// <b>Reads the Loran set at a position.</b> Pure: a function of (receiver data, fitted, world
    /// position, worldSeed, gameTime) and nothing else, so the same boat at the same spot and minute
    /// in the same world always reads the same numbers (rule 5). Allocation-free: safe to call on the
    /// instrument tick.
    ///
    /// <para><b>Where the position comes from.</b> The caller passes a WORLD position. Today the
    /// helm seam (<see cref="IHelmInstruments.TryReadPosition"/>) reports the hull in its region's
    /// scene frame; mapping that into the shared world frame is the world-frame work in flight, and
    /// the instrument host will apply it before calling here. The Loran never reaches for a
    /// region itself.</para>
    /// </summary>
    public static class LoranReceiver
    {
        /// <summary>
        /// The reading at <paramref name="worldPos"/>. The set tries every chain and every secondary
        /// pair it can hear and keeps the one with the smallest expected error (geometry x range
        /// gain); ties keep the first in data order. It then adds the chain's deterministic error,
        /// rounds to the readout step, and resolves those numbers back to a position.
        /// </summary>
        public static LoranReading Read(LoranReceiverDef receiver, bool fitted, Vector2 worldPos,
                                        int worldSeed, double gameTimeSeconds)
        {
            if (!fitted) return LoranReading.Off(LoranSignal.NotFitted);
            if (receiver == null || receiver.Chains == null || !NavMath.IsFinite(worldPos)
                || double.IsNaN(gameTimeSeconds) || double.IsInfinity(gameTimeSeconds))
                return LoranReading.Off(LoranSignal.NoSignal);

            double c = receiver.PropagationMetresPerMicrosecond;
            LoranChainDef best = null;
            int bestA = -1, bestB = -1;
            double bestScore = double.PositiveInfinity, bestGeometry = 0.0, bestRange = 0.0;
            for (int ci = 0; ci < receiver.Chains.Length; ci++)
            {
                LoranChainDef chain = receiver.Chains[ci];
                if (chain == null || chain.Problem() != null) continue;
                double range = LoranMath.RangeFraction(chain, worldPos);
                if (!(range <= 1.0)) continue;
                double gain = LoranMath.RangeGain(chain, range);
                for (int a = 0; a < chain.Secondaries.Length; a++)
                for (int b = a + 1; b < chain.Secondaries.Length; b++)
                {
                    double g = LoranMath.GeometryMetresPerMicrosecond(chain, a, b, worldPos, c);
                    if (!(g < receiver.NoFixGeometryMetresPerMicrosecond)) continue;
                    double score = g * gain;
                    if (score < bestScore)
                    {
                        bestScore = score; best = chain; bestA = a; bestB = b;
                        bestGeometry = g; bestRange = range;
                    }
                }
            }
            if (best == null) return LoranReading.Off(LoranSignal.NoSignal);

            double res = receiver.DisplayResolutionMicroseconds;
            double tdA = LoranMath.Quantize(
                LoranMath.TimeDifference(best, bestA, worldPos, c)
                + LoranMath.ErrorMicroseconds(best, bestA, worldSeed, worldPos, gameTimeSeconds, bestRange), res);
            double tdB = LoranMath.Quantize(
                LoranMath.TimeDifference(best, bestB, worldPos, c)
                + LoranMath.ErrorMicroseconds(best, bestB, worldSeed, worldPos, gameTimeSeconds, bestRange), res);

            // The receiver's own fix: the shown numbers resolved, seeded at the true position so the
            // solver lands on the crossing the boat is actually near.
            if (!LoranMath.TryPositionFromTds(best, bestA, bestB, tdA, tdB, worldPos, receiver, out Vector2 fix))
                return LoranReading.Off(LoranSignal.NoSignal);

            bool blink = bestRange > receiver.FringeRangeFraction
                         || bestGeometry > receiver.BlinkGeometryMetresPerMicrosecond;
            return new LoranReading(blink ? LoranSignal.Blink : LoranSignal.Fix, best.Id,
                                    best.GroupRepetitionInterval, bestA, bestB,
                                    best.Secondaries[bestA].Letter, best.Secondaries[bestB].Letter,
                                    tdA, tdB, true, fix, (float)bestGeometry);
        }

        /// <summary>
        /// Where a stored set of numbers lies, in world metres, solved near <paramref name="guess"/>
        /// (pass the current fix). False when the waypoint's chain is not one this set tracks or the
        /// numbers do not resolve. This is the call an autopilot makes.
        /// </summary>
        public static bool TryResolve(LoranReceiverDef receiver, in LoranWaypoint waypoint, Vector2 guess,
                                      out Vector2 position)
        {
            position = default;
            if (receiver == null || !waypoint.IsValid) return false;
            LoranChainDef chain = receiver.ChainById(waypoint.ChainId);
            if (chain == null || chain.Problem() != null
                || !LoranMath.ValidPair(chain, waypoint.SecondaryA, waypoint.SecondaryB)) return false;
            return LoranMath.TryPositionFromTds(chain, waypoint.SecondaryA, waypoint.SecondaryB,
                                                waypoint.TdA, waypoint.TdB, guess, receiver, out position);
        }

        /// <summary>Bearing and distance from the current fix to a stored waypoint, or
        /// <see cref="LoranSteer.None"/> when the set has no fix or the numbers do not resolve.</summary>
        public static LoranSteer Steer(LoranReceiverDef receiver, in LoranReading current, in LoranWaypoint waypoint)
        {
            if (!current.HasFix) return LoranSteer.None;
            if (!TryResolve(receiver, waypoint, current.FixPosition, out Vector2 target)) return LoranSteer.None;
            return new LoranSteer(waypoint.Number, NavMath.BearingDegrees(current.FixPosition, target),
                                  NavMath.DistanceMetres(current.FixPosition, target), target);
        }
    }
}
