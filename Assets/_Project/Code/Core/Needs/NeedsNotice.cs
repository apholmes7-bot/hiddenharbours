using System;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// What a villager can see of the player's needs (cleanliness slice 1): one flag per need stage.
    /// Raw stages: a trait relief eases a need's PENALTY, never what shows (the needs ruling).
    /// </summary>
    [Flags]
    public enum NeedSigns
    {
        None = 0,
        Peckish = 1 << 0,
        Hungry = 1 << 1,
        Tired = 1 << 2,
        WornOut = 1 << 3,
        Grubby = 1 << 4,
        Stinky = 1 << 5,
    }

    /// <summary>
    /// How a villager's reply goes, given the player's needs (owner rulings 2026-11-14: hungry and tired
    /// make the player CRANKY, some friendly or bonus options do not appear; 2026-11-15: a stinky player is
    /// noticed and villagers react negatively). Append-only, explicit values (stored on
    /// <see cref="NeedReactionDef"/>).
    /// </summary>
    public enum ReplyTone
    {
        /// <summary>Nothing shows: the villager's ordinary reply.</summary>
        Warm = 0,

        /// <summary>The player is hungry or worn out: a cranky exchange.</summary>
        Cranky = 1,

        /// <summary>The player stinks: the villager is put off (a remark, a step back).</summary>
        PutOff = 2,
    }

    /// <summary>
    /// <b>THE REPLY SEAM</b> (cleanliness slice 1): the one place a villager's talk asks how the player's
    /// needs show. Main had the walk/price half of the needs seam (<see cref="CharacterFactors"/>) but no
    /// reply half ("later, a villager's reply"); this is it, for all three needs, so stinky joins hunger and
    /// tiredness exactly as they would. Pure over stages; <see cref="ToneNow"/> reads
    /// <see cref="GameServices.Needs"/>. The dialogue lane's join: pick the line pool by the tone (a
    /// <see cref="NeedReactionDef"/> for <see cref="ReplyTone.PutOff"/>) and hide bonus options unless Warm.
    /// </summary>
    public static class NeedsNotice
    {
        /// <summary>The signs three stages make.</summary>
        public static NeedSigns FromStages(NeedStage hunger, NeedStage tiredness, NeedStage dirtiness)
        {
            NeedSigns s = NeedSigns.None;
            if (hunger == NeedStage.Strong) s |= NeedSigns.Hungry;
            else if (hunger == NeedStage.Mild) s |= NeedSigns.Peckish;
            if (tiredness == NeedStage.Strong) s |= NeedSigns.WornOut;
            else if (tiredness == NeedStage.Mild) s |= NeedSigns.Tired;
            if (dirtiness == NeedStage.Strong) s |= NeedSigns.Stinky;
            else if (dirtiness == NeedStage.Mild) s |= NeedSigns.Grubby;
            return s;
        }

        /// <summary>The signs <paramref name="needs"/> shows now (none for null).</summary>
        public static NeedSigns Of(INeedsService needs)
            => needs == null
                ? NeedSigns.None
                : FromStages(needs.Stage(NeedKind.Hunger), needs.Stage(NeedKind.Tiredness),
                             needs.Stage(NeedKind.Dirtiness));

        /// <summary>The reply tone the signs make. Stink outranks crankiness: the villager reacts to what
        /// they smell before what they hear.</summary>
        public static ReplyTone ToneOf(NeedSigns signs)
        {
            if ((signs & NeedSigns.Stinky) != 0) return ReplyTone.PutOff;
            if ((signs & (NeedSigns.Hungry | NeedSigns.WornOut)) != 0) return ReplyTone.Cranky;
            return ReplyTone.Warm;
        }

        /// <summary>The local player's reply tone now.</summary>
        public static ReplyTone ToneNow() => ToneOf(Of(GameServices.Needs));
    }
}
