using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// <b>THE one place</b> the Fishing-internal <see cref="BitePhase"/> (the sequence sim's domain)
    /// maps to the Core <see cref="FishingPhase"/> wire vocabulary — the <see cref="RodFightPhases"/>
    /// discipline, so the two vocabularies can never skew. The mapping IS the diegetic design:
    /// <list type="bullet">
    ///   <item><see cref="BitePhase.Approach"/> / <see cref="BitePhase.Away"/> →
    ///   <see cref="FishingPhase.Waiting"/> — nothing shows; the water is quiet.</item>
    ///   <item><see cref="BitePhase.NibbleDip"/> → <see cref="FishingPhase.BiteNibble"/> — the teasing
    ///   dip (bobber nibble state, rod-tip knock, line twitch).</item>
    ///   <item><see cref="BitePhase.TakeOpen"/> → <see cref="FishingPhase.Bite"/> — the TRUE take
    ///   (bobber pulled under). Presentation renders it and the strike is judged against it off the
    ///   SAME sim state — one quantity, one computation (the flick-cast lesson).</item>
    /// </list>
    /// Terminal sim phases don't map — <see cref="BitePhase.Hooked"/> hands off to the fight and
    /// <see cref="BitePhase.Gone"/> to the NoBite result; the controller owns those transitions.
    /// </summary>
    public static class BitePhases
    {
        /// <summary>The Core wire phase for a live (non-terminal) sequence phase.</summary>
        public static FishingPhase ToFishingPhase(BitePhase phase)
        {
            switch (phase)
            {
                case BitePhase.NibbleDip: return FishingPhase.BiteNibble;
                case BitePhase.TakeOpen:  return FishingPhase.Bite;
                default:                  return FishingPhase.Waiting;   // Approach / Away — quiet water
            }
        }
    }
}
