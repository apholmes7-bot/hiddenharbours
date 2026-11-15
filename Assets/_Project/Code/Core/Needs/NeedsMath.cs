namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The pure maths of needs</b> (needs slice 1). No Unity state, no service, no save: a saved
    /// (level, instant), the clock and the owner's <see cref="NeedCurve"/> in; a level, a stage and a
    /// factor out. <see cref="NeedsService"/> only gathers the inputs.
    ///
    /// <para><b>Deterministic.</b> A level is a straight line from its saved instant, clamped to 0..1:
    /// the same save at the same game time gives the same level on every machine (rule 5). Nothing is
    /// integrated frame by frame, so frame rate, pauses and the slow tick's cadence cannot change it.</para>
    ///
    /// <para><b>Slows, never stops.</b> <see cref="Factor"/> never returns below its floor, and the
    /// floor itself is clamped above <see cref="MinimumFloor"/>, so even a mis-set config cannot stop the
    /// character. With no penalty the factor is the literal <see cref="TraitMath.Identity"/>, so a fed,
    /// rested character plays bit-for-bit as before.</para>
    /// </summary>
    public static class NeedsMath
    {
        /// <summary>The lowest floor the maths will honour, whatever the config says: the owner's "slow
        /// the character down but never stop them" as a guard rail, not a tunable.</summary>
        public const float MinimumFloor = 0.5f;

        /// <summary>The level of <paramref name="state"/> at <paramref name="nowGameSeconds"/>. An
        /// unstamped state, a clock behind the stamp, or no sensible hour length holds the saved level.</summary>
        public static float LevelAt(in NeedStateDto state, in NeedCurve curve, double nowGameSeconds,
                                    double secondsPerGameHour)
        {
            float level = Clamp01(state.Level);
            if (!state.IsStamped || !(secondsPerGameHour > 0d) || !(curve.RisePerGameHour > 0f)) return level;
            double elapsed = nowGameSeconds - state.SinceGameSeconds;
            if (!(elapsed > 0d)) return level;
            double risen = level + curve.RisePerGameHour * (elapsed / secondsPerGameHour);
            return risen >= 1d ? 1f : (float)risen;
        }

        /// <summary>The stage <paramref name="level"/> sits in. A StrongAt below MildAt is read as MildAt.</summary>
        public static NeedStage StageOf(float level, in NeedCurve curve)
        {
            float mild = Clamp01(curve.MildAt);
            float strong = curve.StrongAt > mild ? Clamp01(curve.StrongAt) : mild;
            if (level >= strong && strong > 0f) return NeedStage.Strong;
            if (level >= mild && mild > 0f) return NeedStage.Mild;
            return NeedStage.None;
        }

        /// <summary>The walk penalty of <paramref name="stage"/> (0 for none).</summary>
        public static float WalkPenalty(NeedStage stage, in NeedCurve curve)
            => stage == NeedStage.Strong ? Clamp01(curve.StrongWalkPenalty)
             : stage == NeedStage.Mild ? Clamp01(curve.MildWalkPenalty) : 0f;

        /// <summary>The in-person sell penalty of <paramref name="stage"/> (0 for none).</summary>
        public static float SellPenalty(NeedStage stage, in NeedCurve curve)
            => stage == NeedStage.Strong ? Clamp01(curve.StrongSellPenalty)
             : stage == NeedStage.Mild ? Clamp01(curve.MildSellPenalty) : 0f;

        /// <summary>The penalty one stat takes from one need, given its stage. Only
        /// <see cref="TraitStat.WalkSpeed"/> and <see cref="TraitStat.SellPrice"/> are ever touched (the
        /// owner's ruling: movement speed and in-person prices); every other stat is 0.</summary>
        public static float PenaltyFor(TraitStat stat, NeedStage stage, in NeedCurve curve)
        {
            switch (stat)
            {
                case TraitStat.WalkSpeed: return WalkPenalty(stage, curve);
                case TraitStat.SellPrice: return SellPenalty(stage, curve);
                default: return 0f;
            }
        }

        /// <summary>A relief scale made safe: 0..1, so a trait can only ever ease a penalty.</summary>
        public static float ReliefScale(float scale) => Clamp01(scale);

        /// <summary>
        /// The factor for a stat given the total penalty on it: <c>1 - penalty</c>, never below
        /// <paramref name="floor"/> (itself clamped to <see cref="MinimumFloor"/>..1). Exactly
        /// <see cref="TraitMath.Identity"/> with no penalty.
        /// </summary>
        public static float Factor(float totalPenalty, float floor)
        {
            if (!(totalPenalty > 0f)) return TraitMath.Identity;
            float safeFloor = floor < MinimumFloor ? MinimumFloor : floor > 1f ? 1f : floor;
            float factor = TraitMath.Identity - totalPenalty;
            return factor > safeFloor ? factor : safeFloor;
        }

        /// <summary>The floor that applies to <paramref name="stat"/> (1 for a stat needs never touch).</summary>
        public static float FloorFor(TraitStat stat, in NeedsSettings settings)
        {
            switch (stat)
            {
                case TraitStat.WalkSpeed: return settings.WalkFloor;
                case TraitStat.SellPrice: return settings.SellFloor;
                default: return TraitMath.Identity;
            }
        }

        /// <summary>
        /// <b>At a save write</b>: re-anchor every stamped need at its level NOW, so the saved instant is
        /// the save's own <see cref="SaveData.GameTimeSeconds"/>. The level is unchanged (same line, new
        /// start point); it only makes a reload independent of when the last meal was. Null-safe.
        /// </summary>
        public static void Settle(PlayerNeedsDto needs, double nowGameSeconds, in NeedsSettings settings,
                                  double secondsPerGameHour)
        {
            if (needs == null) return;
            if (needs.Hunger.IsStamped)
                needs.Hunger = new NeedStateDto(
                    LevelAt(needs.Hunger, settings.Hunger, nowGameSeconds, secondsPerGameHour), nowGameSeconds);
            if (needs.Tiredness.IsStamped)
                needs.Tiredness = new NeedStateDto(
                    LevelAt(needs.Tiredness, settings.Tiredness, nowGameSeconds, secondsPerGameHour), nowGameSeconds);
        }

        /// <summary>
        /// <b>At a clock seek</b> (<see cref="IGameClock.SeekTo"/>: a restore, never time passing): keep
        /// each stamped need's SAVED level and anchor it at <paramref name="nowGameSeconds"/>, so jumping the
        /// clock neither starves nor feeds anyone. On a real load the saved level was settled at the save's
        /// own instant (<see cref="Settle"/>) and the seek lands on that instant, so this changes nothing.
        /// Null-safe; an unstamped need stays unstamped.
        /// </summary>
        public static void Rebase(PlayerNeedsDto needs, double nowGameSeconds)
        {
            if (needs == null || nowGameSeconds < 0d) return;
            if (needs.Hunger.IsStamped) needs.Hunger = new NeedStateDto(needs.Hunger.Level, nowGameSeconds);
            if (needs.Tiredness.IsStamped) needs.Tiredness = new NeedStateDto(needs.Tiredness.Level, nowGameSeconds);
        }

        /// <summary>The curve for <paramref name="need"/>.</summary>
        public static NeedCurve CurveOf(NeedKind need, in NeedsSettings settings)
            => need == NeedKind.Tiredness ? settings.Tiredness : settings.Hunger;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
