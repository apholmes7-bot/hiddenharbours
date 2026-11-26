using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>How long a wharf ladder must be, and how much of it shows</b>, as pure functions of the deck
    /// height and the region's tide profile. Recomputed, never saved (rule 5); the margins come from
    /// <see cref="WharfLadderDef"/> (rule 6).
    ///
    /// <para>The tide is <c>MeanLevel + Amplitude x envelope x carrier</c> with the envelope at most 1
    /// (<c>TideModel.Height</c>), so <c>MeanLevel - Amplitude</c> is the lowest the water ever stands and
    /// <c>MeanLevel + Amplitude</c> the highest.</para>
    /// </summary>
    public static class WharfLadderReach
    {
        /// <summary>The lowest water the profile can reach (spring low), metres above datum.</summary>
        public static float LowestWater(TideProfile tide) => tide.MeanLevel - tide.Amplitude;

        /// <summary>The highest water the profile can reach (spring high), metres above datum.</summary>
        public static float HighestWater(TideProfile tide) => tide.MeanLevel + tide.Amplitude;

        /// <summary>The deepest a ladder's foot may stand and still be the kind's ladder: the region's
        /// lowest water less the kind's margin. A foot at or below this is usable at every tide.</summary>
        public static float RequiredFootElevation(TideProfile tide, WharfLadderDef kind)
            => LowestWater(tide) - (kind != null ? kind.BelowLowestWaterMetres : 0f);

        /// <summary>The length from the deck to the required foot, in metres.</summary>
        public static float RequiredLengthMetres(float topElevation, TideProfile tide, WharfLadderDef kind)
            => topElevation - RequiredFootElevation(tide, kind);

        /// <summary>True when a ladder from <paramref name="topElevation"/> of
        /// <paramref name="lengthMetres"/> reaches the kind's required foot.</summary>
        public static bool ReachesLowestWater(float topElevation, float lengthMetres, TideProfile tide,
                                              WharfLadderDef kind)
            => topElevation - lengthMetres <= RequiredFootElevation(tide, kind);

        /// <summary>How much of the ladder stands dry above <paramref name="water"/>, in metres: the deck
        /// down to the water, or the whole ladder if the water is below its foot, or none if above its
        /// top.</summary>
        public static float ExposedMetres(float topElevation, float lengthMetres, float water)
        {
            float foot = topElevation - lengthMetres;
            float wetFrom = water > foot ? water : foot;
            float exposed = topElevation - wetFrom;
            return exposed > 0f ? exposed : 0f;
        }

        /// <summary>True when <paramref name="rungMetres"/> lies in the kind's comfortable step range.</summary>
        public static bool RungInReach(float rungMetres, WharfLadderDef kind)
            => kind != null && rungMetres >= kind.MinRungPitchMetres && rungMetres <= kind.MaxRungPitchMetres;
    }
}
