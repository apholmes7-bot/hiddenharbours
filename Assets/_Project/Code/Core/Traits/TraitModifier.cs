using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The numbers a trait may touch</b> (traits slice 1, owner ruling 2026-11-14). Each value names
    /// ONE existing seam a feature module already owns; the module asks
    /// <see cref="ITraitService.PlayerMultiplier"/> for its own stat and never learns which trait moved it.
    ///
    /// <para><b>Append-only, explicit values.</b> Unity serializes an enum as its integer, so a
    /// <see cref="TraitDef"/> asset stores the number, not the name. A shipped value is never renumbered
    /// or reused; a retired stat keeps its number with no seam reading it.</para>
    /// </summary>
    public enum TraitStat
    {
        /// <summary>Walk and sprint speed on foot (<c>PlayerWalkController</c>). Higher is better.</summary>
        WalkSpeed = 0,

        /// <summary>The price the player's own sales fetch (<c>SellService</c>, through the outlet's
        /// price level). Higher is better.</summary>
        SellPrice = 1,

        /// <summary>The wait for a bite after a cast or drop (<c>FishingController</c>). Lower is better.</summary>
        BiteWait = 2,
    }

    /// <summary>How a <see cref="TraitModifier"/>'s <see cref="TraitModifier.Value"/> is read.</summary>
    public enum TraitModifierKind
    {
        /// <summary><c>Value</c> is a factor: 1.08 means "8 % more", 0.9 means "10 % less".</summary>
        Multiplier = 0,

        /// <summary><c>Value</c> is added to the factor before the multipliers apply: 0.08 means "+8 %",
        /// -0.1 means "-10 %". Additive modifiers on one stat SUM, so two "+5 %" make "+10 %", never
        /// "+10.25 %".</summary>
        Additive = 1,
    }

    /// <summary>
    /// One (stat, value) pair on a <see cref="TraitDef"/>. A plain serializable struct, so the owner
    /// tunes it in the inspector and a test builds one in a line.
    /// </summary>
    [Serializable]
    public struct TraitModifier
    {
        [Tooltip("The seam this modifier moves.")]
        public TraitStat Stat;

        [Tooltip("Multiplier: Value is a factor (1.08 = +8 %). Additive: Value is added to the factor " +
                 "(0.08 = +8 %); additive values on one stat sum before any multiplier applies.")]
        public TraitModifierKind Kind;

        [Tooltip("The number. Owner-tunable; the content validation test refuses a value that would be " +
                 "a drawback (the 2026-11-14 ruling: no drawbacks) or no change at all.")]
        public float Value;

        public TraitModifier(TraitStat stat, TraitModifierKind kind, float value)
        {
            Stat = stat;
            Kind = kind;
            Value = value;
        }
    }
}
