using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The pure maths of traits</b> (traits slice 1). No Unity state, no service, no save: a list of
    /// <see cref="TraitDef"/>s in, one factor per stat out. <see cref="TraitService"/> resolves ids and
    /// orders the list; this class only combines.
    ///
    /// <para><b>The rule.</b> For one stat: <c>(1 + sum of Additive values) x product of Multiplier
    /// values</c>, floored at zero. Defs are combined in the order given, which the service makes
    /// ordinal-by-id so the float product is the same on every machine and in every pick order (rule 5).</para>
    ///
    /// <para><b>No traits is exactly 1.</b> With nothing touching a stat the result is the literal
    /// <c>1f</c>, and <see cref="Apply"/> returns its input untouched for a factor of exactly 1, so a
    /// character without traits plays bit-for-bit as before.</para>
    /// </summary>
    public static class TraitMath
    {
        /// <summary>The factor that changes nothing.</summary>
        public const float Identity = 1f;

        /// <summary>The combined factor for <paramref name="stat"/> over <paramref name="defs"/>, in the
        /// order given. Null defs and null modifier arrays are skipped. Exactly <see cref="Identity"/>
        /// when no modifier names the stat.</summary>
        public static float Combine(TraitStat stat, IReadOnlyList<TraitDef> defs)
        {
            if (defs == null || defs.Count == 0) return Identity;
            float additive = 0f;
            float product = Identity;
            bool touched = false;
            for (int d = 0; d < defs.Count; d++)
            {
                TraitModifier[] mods = defs[d] != null ? defs[d].Modifiers : null;
                if (mods == null) continue;
                for (int m = 0; m < mods.Length; m++)
                {
                    if (mods[m].Stat != stat) continue;
                    touched = true;
                    if (mods[m].Kind == TraitModifierKind.Additive) additive += mods[m].Value;
                    else product *= mods[m].Value;
                }
            }
            if (!touched) return Identity;
            float factor = (Identity + additive) * product;
            return factor > 0f ? factor : 0f;
        }

        /// <summary>The combined factor for <paramref name="stat"/> from ONE trait alone.</summary>
        public static float FactorOf(TraitDef def, TraitStat stat)
            => def == null ? Identity : Combine(stat, new[] { def });

        /// <summary><paramref name="value"/> scaled by <paramref name="factor"/>, returned untouched when the
        /// factor is exactly <see cref="Identity"/>: the seams call this so "no traits" is the old number,
        /// not a re-multiplied one.</summary>
        public static float Apply(float value, float factor)
            => factor == Identity ? value : value * factor;

        /// <summary>Whether a factor on <paramref name="stat"/> helps the player: above 1 for a stat where
        /// more is better, below 1 for one where less is better (the bite wait). The content validation test
        /// holds every shipped trait to this (the 2026-11-14 ruling: no drawbacks).</summary>
        public static bool IsBenefit(TraitStat stat, float factor)
            => HigherIsBetter(stat) ? factor > Identity : factor < Identity && factor > 0f;

        /// <summary>The direction of a stat. A new <see cref="TraitStat"/> states its own here.</summary>
        public static bool HigherIsBetter(TraitStat stat)
        {
            switch (stat)
            {
                case TraitStat.BiteWait: return false;
                default: return true;   // WalkSpeed, SellPrice
            }
        }
    }
}
