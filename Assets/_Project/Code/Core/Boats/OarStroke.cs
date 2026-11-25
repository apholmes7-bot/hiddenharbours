using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The four parts of one oar stroke, plus <see cref="Rest"/> for oars that are not rowing
    /// (trailing, shipped, or stowed under power).
    /// </summary>
    public enum OarStrokePart
    {
        Rest = 0,
        /// <summary>The blade is forward and going in.</summary>
        Catch,
        /// <summary>The blade is in the water, sweeping aft.</summary>
        Drive,
        /// <summary>The blade is aft and coming out.</summary>
        Release,
        /// <summary>The blade is out of the water, travelling forward again.</summary>
        Recovery,
    }

    /// <summary>
    /// <b>Where a boat's oars are in their stroke</b>, published by whoever poses the oars (the dory's
    /// mesh oars today) and read by whoever draws the rower. A Core seam so that the rower's body can
    /// follow the oars without the Player or Art modules reaching into the Boats module's classes
    /// (rule 4).
    ///
    /// <para><b>The phase is the oars' own, not a second clock.</b> 0 is the catch (blade forward,
    /// going in), the drive runs to 0.5, 0.5 is the release (blade aft, coming out), and the recovery
    /// runs back round to 1. A back-watering oar runs the same phase backwards, so a body that follows
    /// it rows backwards too. Presentation only: nothing in the simulation reads it (rule 5).</para>
    /// </summary>
    public interface IOarStroke
    {
        /// <summary>True while at least one oar is drawn ROWING. False when they trail, ship, or are
        /// stowed under power, and false when nobody is at the helm.</summary>
        bool IsStroking { get; }

        /// <summary>The stroke phase in [0, 1): 0 = catch, 0.5 = release. Meaningful only while
        /// <see cref="IsStroking"/>; 0 otherwise.</summary>
        float StrokePhase { get; }

        /// <summary>Which part of the stroke <see cref="StrokePhase"/> is in, or
        /// <see cref="OarStrokePart.Rest"/> when not stroking.</summary>
        OarStrokePart StrokePart { get; }
    }

    /// <summary>
    /// The pure arithmetic behind <see cref="IOarStroke"/>: wrapping, the oar-rig-to-stroke mapping and
    /// the part a phase is in. Static, allocation-free, deterministic.
    /// </summary>
    public static class OarStrokeMath
    {
        /// <summary>The stroke phase of the release: the drive is the first half of the stroke and the
        /// recovery the second. This is the oar rig's own shape (its row pose is one ellipse per turn,
        /// blade down for one half and up for the other), not a tunable.</summary>
        public const float ReleasePhase = 0.5f;

        /// <summary>The width of each named part. The stroke is split into four equal parts, CENTRED on
        /// its two turning points: the catch is the quarter around phase 0, the release the quarter
        /// around 0.5, and the drive and recovery the quarters between them. A structural definition,
        /// not a feel knob.</summary>
        public const float PartSpan = 0.25f;

        /// <summary>Wrap any value into [0, 1). NaN and infinities come back as 0 rather than
        /// propagating.</summary>
        public static float Wrap01(float x)
        {
            if (float.IsNaN(x) || float.IsInfinity(x)) return 0f;
            x -= Mathf.Floor(x);
            return x >= 1f ? 0f : x;
        }

        /// <summary>
        /// The stroke phase for an oar posed at <paramref name="oarTurns"/> of its rig's row cycle, when
        /// that rig's catch is at <paramref name="catchTurns"/>. Both are in turns; any whole number of
        /// turns either way gives the same answer.
        /// </summary>
        public static float PhaseFromOarTurns(float oarTurns, float catchTurns) =>
            Wrap01(oarTurns - catchTurns);

        /// <summary>The part of the stroke <paramref name="phase"/> is in; <see cref="OarStrokePart.Rest"/>
        /// when <paramref name="stroking"/> is false.</summary>
        public static OarStrokePart PartOf(bool stroking, float phase)
        {
            if (!stroking) return OarStrokePart.Rest;
            float shifted = Wrap01(phase + PartSpan * 0.5f);
            int quarter = Mathf.Min(3, Mathf.FloorToInt(shifted / PartSpan));
            switch (quarter)
            {
                case 0: return OarStrokePart.Catch;
                case 1: return OarStrokePart.Drive;
                case 2: return OarStrokePart.Release;
                default: return OarStrokePart.Recovery;
            }
        }
    }
}
