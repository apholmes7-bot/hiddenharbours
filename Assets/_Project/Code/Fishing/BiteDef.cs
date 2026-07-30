using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The plain-data mirror of a <see cref="BiteDef"/> — what the pure <see cref="BiteSequenceSim"/>
    /// consumes (captured at sequence start, so a mid-bite asset edit can't tear a live sequence;
    /// the <see cref="RodFightSim"/> capture discipline). Tests construct one directly, no assets.
    /// </summary>
    public readonly struct BiteProfile
    {
        public readonly int FalseNibblesMin;
        public readonly int FalseNibblesMax;
        public readonly float NibbleGapMinSeconds;
        public readonly float NibbleGapMaxSeconds;
        public readonly float NibbleDipSeconds;
        public readonly float TakeWindowSeconds;
        public readonly float ReturnChance01;
        public readonly float ReturnDelayMinSeconds;
        public readonly float ReturnDelayMaxSeconds;
        public readonly int MaxPasses;

        public BiteProfile(int falseNibblesMin, int falseNibblesMax,
                           float nibbleGapMinSeconds, float nibbleGapMaxSeconds,
                           float nibbleDipSeconds, float takeWindowSeconds,
                           float returnChance01, float returnDelayMinSeconds,
                           float returnDelayMaxSeconds, int maxPasses)
        {
            FalseNibblesMin = Mathf.Max(0, falseNibblesMin);
            FalseNibblesMax = Mathf.Max(FalseNibblesMin, falseNibblesMax);
            NibbleGapMinSeconds = Mathf.Max(0.05f, nibbleGapMinSeconds);
            NibbleGapMaxSeconds = Mathf.Max(NibbleGapMinSeconds, nibbleGapMaxSeconds);
            NibbleDipSeconds = Mathf.Max(0.05f, nibbleDipSeconds);
            TakeWindowSeconds = Mathf.Max(0.05f, takeWindowSeconds);
            ReturnChance01 = Mathf.Clamp01(returnChance01);
            ReturnDelayMinSeconds = Mathf.Max(0f, returnDelayMinSeconds);
            ReturnDelayMaxSeconds = Mathf.Max(ReturnDelayMinSeconds, returnDelayMaxSeconds);
            MaxPasses = Mathf.Max(1, maxPasses);
        }
    }

    /// <summary>
    /// A fish species' BITE personality, as data (ADR 0003) — the §10 owner drop made literal: how
    /// many teasing FALSE NIBBLES she throws before the true take, how wide the strike window is when
    /// she finally commits ("yes should be different by species" — a mackerel hits hard and holds on,
    /// a haddock mouths the bait), and how willing she is to COME BACK after a missed strike ("It
    /// keeps nibbling" — a miss is never instantly fatal). One asset = one bite profile. Create via
    /// Assets ▸ Create ▸ Hidden Harbours ▸ Bite, save in Data/Bites.
    ///
    /// <para><b>Opt-in, the RodFightDef shape.</b> A species references one from
    /// <see cref="FishSpeciesDef.Bite"/>; leave that EMPTY and the species keeps today's forgiving
    /// auto-hook bite exactly (press to hook early, else it hooks itself — no nibble game). Content
    /// authors tune the moment per species with no code change (rule 6). Shipped values are
    /// PLACEHOLDER personalities — the owner tunes in play.</para>
    ///
    /// <para><b>Consumed by</b> the pure <see cref="BiteSequenceSim"/> (this slice — maths only) and,
    /// in the next slices, the controller's bite phase + the diegetic tells. Nothing here is saved;
    /// the sequence is real-time interaction, seeded only so tests replay it (rule 5's world-sim
    /// contract is untouched).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Bite", fileName = "Bite")]
    public class BiteDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable, append-only id (e.g. \"bite.atlantic_cod\"). Never reuse or rename (rule 2).")]
        public string Id = "bite.template";
        public string DisplayName = "Bite (template)";

        [Header("The nibble game (owner §10.2 — false nibbles are in)")]
        [Min(0)]
        [Tooltip("Fewest teasing dips before the true take. 0 = she can hit on the first touch.")]
        public int FalseNibblesMin = 1;

        [Min(0)]
        [Tooltip("Most teasing dips before the true take. More = a longer read, more chances to be " +
                 "fooled into an early strike.")]
        public int FalseNibblesMax = 3;

        [Min(0.05f)]
        [Tooltip("Shortest gap (s) between events in a pass — nibble to nibble, last nibble to the take.")]
        public float NibbleGapMinSeconds = 0.8f;

        [Min(0.05f)]
        [Tooltip("Longest gap (s) between events in a pass. A wide min→max range makes her rhythm " +
                 "unreadable; a narrow one makes her mechanical.")]
        public float NibbleGapMaxSeconds = 2.2f;

        [Min(0.05f)]
        [Tooltip("How long each teasing dip SHOWS (s) — the window in which striking is a mistake. " +
                 "Presentation reads this for the bobber's nibble state.")]
        public float NibbleDipSeconds = 0.35f;

        [Header("The take (the strike window — the species personality dial)")]
        [Min(0.05f)]
        [Tooltip("How long the TRUE take stays strikeable (s). Wide = she hits hard and holds on " +
                 "(easy); tight = she mouths the bait and spits it (the fussy fish).")]
        public float TakeWindowSeconds = 0.8f;

        [Header("The miss (owner §10.2 — \"It keeps nibbling\": a miss is never instantly fatal)")]
        [Range(0f, 1f)]
        [Tooltip("Chance she comes back for ANOTHER PASS after a missed strike (early on a tease, or " +
                 "late on the take). At 1 she always returns until MaxPasses runs out; at 0 a miss " +
                 "sends her off despite the ruling — keep it well above 0.")]
        public float ReturnChance01 = 0.6f;

        [Min(0f)]
        [Tooltip("Shortest wait (s) before a returning fish starts her next pass.")]
        public float ReturnDelayMinSeconds = 1.5f;

        [Min(0f)]
        [Tooltip("Longest wait (s) before a returning fish starts her next pass.")]
        public float ReturnDelayMaxSeconds = 4f;

        [Min(1)]
        [Tooltip("Most passes she will ever make at one cast (first pass included). Bounds the " +
                 "keeps-nibbling rule so one cast can't nibble forever.")]
        public int MaxPasses = 3;

        /// <summary>This Def's values as the plain profile the pure sim consumes (validated/clamped).</summary>
        public BiteProfile ToProfile()
            => new BiteProfile(FalseNibblesMin, FalseNibblesMax, NibbleGapMinSeconds, NibbleGapMaxSeconds,
                               NibbleDipSeconds, TakeWindowSeconds, ReturnChance01,
                               ReturnDelayMinSeconds, ReturnDelayMaxSeconds, MaxPasses);
    }
}
