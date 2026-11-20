using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The needs</b> (needs slice 1, owner ruling 2026-11-14: "traits, hunger and add sleep"; Dirtiness
    /// added by cleanliness slice 1).
    /// Each is a 0..1 value that rises with GAME time: 0 is fed / rested, 1 is as hungry / as worn out as
    /// a character gets. A need may only slow the walk and shave the price of an in-person sale (later,
    /// colour a villager's reply). It never stops, faints, hurts or kills, and it is never drawn as a bar,
    /// meter or number: the owner's "No bars", shown later in the character's look and the world's answer.
    ///
    /// <para><b>Append-only, explicit values</b> (the <see cref="TraitStat"/> rule): a
    /// <see cref="NeedRelief"/> on a <see cref="TraitDef"/> asset stores the integer.</para>
    /// </summary>
    public enum NeedKind
    {
        /// <summary>Hunger. Relieved by eating (<see cref="INeedsService.Eat"/>).</summary>
        Hunger = 0,

        /// <summary>Tiredness. Relieved by a night's sleep (<see cref="INeedsService.Sleep"/>).</summary>
        Tiredness = 1,

        /// <summary>Dirtiness, the third need (cleanliness slice 1, owner ask 2026-11-15): 0 clean, 1 filthy.
        /// Its level is <see cref="ICleanlinessService.Dirt"/> (work, the sea and washing move it, not just
        /// the clock); its curve is <see cref="CleanlinessSettings.Curve"/>. Mild reads grubby, Strong reads
        /// STINKY (villagers notice, <see cref="NeedsNotice"/>). Relieved by washing, never by eating.</summary>
        Dirtiness = 2,
    }

    /// <summary>
    /// How strongly a need is felt, by threshold (<see cref="NeedCurve.MildAt"/>,
    /// <see cref="NeedCurve.StrongAt"/>). Hunger reads fine / peckish / hungry; tiredness reads rested /
    /// tired / worn out. The words belong to the bubble slice; the numbers are the owner's.
    /// </summary>
    public enum NeedStage
    {
        /// <summary>Fed, or rested. No effect at all.</summary>
        None = 0,

        /// <summary>Peckish, or tired. A small slow-down and a small price shave.</summary>
        Mild = 1,

        /// <summary>Hungry, or worn out. A bigger slow-down, never below the floor.</summary>
        Strong = 2,
    }

    /// <summary>
    /// One need's stage changed for the local player (needs slice 1). Published from the slow tick
    /// (<see cref="INeedsService.Tick"/>) and straight away by <see cref="INeedsService.Eat"/> /
    /// <see cref="INeedsService.Sleep"/>, once per crossing. The next slice hangs the speech bubble and
    /// the character's look on this; nothing in this slice listens.
    /// </summary>
    public readonly struct NeedStageChanged
    {
        public readonly NeedKind Need;
        public readonly NeedStage From;
        public readonly NeedStage To;

        public NeedStageChanged(NeedKind need, NeedStage from, NeedStage to)
        {
            Need = need;
            From = from;
            To = to;
        }
    }

    /// <summary>
    /// A trait's hold over one need (needs slice 1, owner ruling 2026-11-14: "Maybe personality traits can
    /// overcome these"). Scales that need's PENALTY, never the need itself: the character still gets
    /// hungry (the look and the bubble still show it), it just does not slow them or cost them at the till.
    /// 0 cancels the penalty, 0.5 halves it, 1 changes nothing. Values outside 0..1 are clamped at run
    /// time, so a relief can never become a drawback; the content validation test refuses them anyway.
    /// </summary>
    [Serializable]
    public struct NeedRelief
    {
        [Tooltip("The need whose penalty this trait eases.")]
        public NeedKind Need;

        [Tooltip("What is left of that need's penalty: 0 cancels it, 0.5 halves it, 1 changes nothing.")]
        [Range(0f, 1f)] public float PenaltyScale;

        public NeedRelief(NeedKind need, float penaltyScale)
        {
            Need = need;
            PenaltyScale = penaltyScale;
        }
    }
}
