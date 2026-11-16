namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The ONE place a character's stat factor is composed</b> (needs slice 1). Traits are bonuses,
    /// needs are gentle penalties, and a seam (the walk, the deck walk, the till) applies their product
    /// through <see cref="ApplyPlayer"/> without knowing either exists (rule 4).
    ///
    /// <para><b>The order.</b> <c>trait factor x needs factor</c>. The needs factor carries its own floor
    /// (<see cref="NeedsSettings.WalkFloor"/>), and traits only ever help (the 2026-11-14 ruling: no
    /// drawbacks), so the product can never fall below that floor: hunger slows, it never stops. A trait
    /// that eases a need does so inside the needs factor (<see cref="NeedRelief"/>), not by multiplying
    /// on top.</para>
    ///
    /// <para><b>Bit-for-bit.</b> Fed, rested and trait-less, both factors are the literal 1 and
    /// <see cref="TraitMath.Apply"/> returns the input untouched.</para>
    /// </summary>
    public static class CharacterFactors
    {
        /// <summary>
        /// The local player's combined factor for <paramref name="stat"/>: <c>traits x (needs x body)</c>.
        /// Body slice 1 adds the body's walk factor (height and mass) beside the needs; the worn pair is never
        /// below <see cref="NeedsMath.MinimumFloor"/> (hunger slows, it never stops). With the default body
        /// its factor is the literal 1, so this is bit for bit the needs slice's product.
        /// </summary>
        public static float Player(TraitStat stat)
        {
            float worn = GameServices.Needs.PlayerMultiplier(stat) * GameServices.Body.PlayerMultiplier(stat);
            if (worn < NeedsMath.MinimumFloor) worn = NeedsMath.MinimumFloor;
            return GameServices.Traits.PlayerMultiplier(stat) * worn;
        }

        /// <summary><paramref name="value"/> scaled by the local player's combined factor for
        /// <paramref name="stat"/>; untouched when that factor is exactly 1.</summary>
        public static float ApplyPlayer(float value, TraitStat stat) => TraitMath.Apply(value, Player(stat));
    }
}
