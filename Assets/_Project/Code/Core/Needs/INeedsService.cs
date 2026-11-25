namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Hunger and tiredness, as a seam</b> (needs slice 1, owner ruling 2026-11-14). The local
    /// player's two needs, worked out from the save and the game clock. A feature module reads its stat's
    /// combined factor through <see cref="CharacterFactors.Player"/> (traits x needs, in one place) and
    /// never learns which need or trait moved it (rule 4).
    ///
    /// <para><b>What a need may touch</b> (the ruling): the walk speed and the price of an in-person sale,
    /// later a villager's reply. Never a stop, a faint, damage or death; never a bar, meter, number or HUD
    /// element. A trait's <see cref="NeedRelief"/> can cancel or ease a need's penalty.</para>
    ///
    /// <para><b>Deterministic.</b> The level at any instant is a pure function of the saved
    /// (level, instant) pair and the clock (<see cref="NeedsMath.LevelAt"/>). No roll, no frame step.</para>
    /// </summary>
    public interface INeedsService
    {
        /// <summary>The local player's level of <paramref name="need"/> now, 0..1 (0 with no save).</summary>
        float Level(NeedKind need);

        /// <summary>The local player's stage of <paramref name="need"/> now.</summary>
        NeedStage Stage(NeedKind need);

        /// <summary>The needs' share of the local player's factor for <paramref name="stat"/>, with the
        /// player's trait reliefs applied and the config floor honoured. Exactly 1 when fed and rested, and
        /// for every stat other than walk speed and sell price. Allocation-free (the walk reads it every
        /// physics tick).</summary>
        float PlayerMultiplier(TraitStat stat);

        /// <summary>Eat: lower hunger by <paramref name="amount"/> (0..1 of a full meal), from its level
        /// now. Writes the save's live data (no disk write: eating is not a save point) and announces a
        /// stage change at once. A non-positive amount or no save does nothing.</summary>
        void Eat(float amount);

        /// <summary>Spend energy (body slice 2): raise hunger by <paramref name="amount"/> (meals' worth) from
        /// its level now, never past full hunger, and return the part that did not fit (the body burns that
        /// from mass). The one way work and exercise make the player hungrier; announces a stage change at
        /// once. A non-positive amount or no save does nothing and returns 0.</summary>
        float Spend(float amount);

        /// <summary>Add exertion tiredness at the clock now, clamped to one, in addition to passive rise.
        /// Non-finite/non-positive amounts do nothing. Older implementations have no exertion cost.</summary>
        void Exert(float amount) { }

        /// <summary>Bank a seated stretch (kitchens slice 1): tiredness from
        /// <paramref name="seatedSinceGameSeconds"/> to now follows <see cref="SeatedRest"/> instead of the
        /// clock's rise, so it drifts toward the sitting floor and stops there, always short of what a
        /// night's sleep gives. A stretch before the last sleep or bank counts only from it. No save does
        /// nothing. The default (an implementation that predates kitchens) banks nothing.</summary>
        void RestSeated(double seatedSinceGameSeconds) { }

        /// <summary>A night's sleep: tiredness back to rested from now. Hunger is untouched; the hours slept
        /// already counted against it on the clock. Announces a stage change at once. No save does nothing.</summary>
        void Sleep();

        /// <summary>
        /// A sleep that began at <paramref name="bedtimeSeconds"/> and ends NOW (owner ruling 2026-11-15:
        /// the player chooses how long, and can wake early). Tiredness falls by
        /// <see cref="NeedsSettings.SleepReliefPerGameHour"/> for each in-game hour actually slept, from
        /// its level at bedtime (it does not rise while asleep), and is stamped at the waking. Hunger is
        /// untouched, as for <see cref="Sleep"/>. A bedtime at or after now relieves nothing.
        /// The default (an implementation that predates the ruling) is the whole-night <see cref="Sleep"/>.
        /// </summary>
        void SleepSince(double bedtimeSeconds) => Sleep();

        /// <summary>The slow tick (driven by the game clock, never per frame): anchors a fresh game's needs
        /// to the clock the first time it runs, and publishes <see cref="NeedStageChanged"/> for each need
        /// whose stage moved since the last look. The first look after a start only records the stage.</summary>
        void Tick();
    }
}
