using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The <b>shape of the burn curve</b>, shared by the whole fleet and owner-tunable (rule 6) —
    /// <c>docs/design/fuel-and-refuelling.md</c> §9.6.1.
    ///
    /// <para><b>One hull's THIRST is one number; the SHAPE is everyone's.</b> How much a particular boat
    /// drinks flat out is <c>BoatHullDef.FullThrottleLitresPerHour</c>, per hull. Everything in this
    /// struct is a dimensionless multiplier over that — how steeply burn climbs with throttle, what a
    /// full hold adds, what a storm adds. So re-tuning one boat is one Def field, and re-pricing fuel for
    /// the entire game is <see cref="BurnScale"/>: one number, here.</para>
    ///
    /// <para><b>Why it lives in Core (the <see cref="AnchorSettings"/> split, verbatim).</b> This is
    /// world-wide policy carried on the Core <c>GameConfig</c> the owner tunes. Core cannot reference the
    /// Boats module (rule 4), so the tunable struct lives here beside the maths that consumes it
    /// (<see cref="FuelBurn"/>); the component that integrates it against a real throttle is Boats-side.</para>
    ///
    /// <para><b>⚠ The GameConfig YAML trap (#420).</b> This is a STRUCT that <c>GameConfig.asset</c>
    /// serializes field by field, and a field the YAML omits deserializes to its <b>C# default</b> — 0
    /// for a float — <i>not</i> to <see cref="Default"/>. A zero <see cref="BurnScale"/> would mean
    /// <b>fuel is free and nothing ever burns</b>: the whole feature ships silently dead, with no error
    /// anywhere. The asset must carry every key below, and <c>GameConfigAssetCoverageTests</c> is the
    /// guard that notices when it doesn't.</para>
    /// </summary>
    [Serializable]
    public struct FuelSettings
    {
        [Header("The throttle curve")]
        [Tooltip("What she burns at ZERO throttle with the engine still turning, as a fraction of her " +
                 "full-throttle rate. Ticking over alongside while you hand-line is not free — but it is " +
                 "nearly free, which is the point: leaving her running is a small decision, not a tax.\n\n" +
                 "Raise it if idling out a tide feels like cheating; lower it if a long drift-fish feels " +
                 "punished.")]
        [Range(0f, 1f)] public float IdleFraction;

        [Tooltip("How steeply burn climbs with throttle. 1 = linear (half throttle, half the fuel); 2 = " +
                 "square, which is roughly how a real hull's resistance behaves, and is what ships.\n\n" +
                 "⭐ THIS NUMBER IS THE WHOLE LESSON. At 2, half throttle is half the speed for 31% of " +
                 "the fuel — so easing off buys about 60% more range. Nobody has to be told; a player who " +
                 "runs low once works it out. Raise it and throttling back saves even more; drop it to 1 " +
                 "and the choice stops mattering.")]
        [Min(0.1f)] public float ThrottleExponent;

        [Header("What she is carrying, and how hard she is having to push")]
        [Tooltip("The most a fully-loaded, fully-bogged engine adds, as a fraction. 0.35 = a boat working " +
                 "her hardest burns 35% more than the same boat light and free-running at the same " +
                 "throttle. This is what carrying the catch home costs.")]
        [Min(0f)] public float LoadSurcharge;

        [Tooltip("How much of 'engine load' is simply the CATCH ABOARD (hold used / hold capacity). With " +
                 "the slip weight below, these two split the load term between weight and resistance.")]
        [Range(0f, 1f)] public float HoldLoadWeight;

        [Tooltip("How much of 'engine load' is her FAILING TO MAKE THE SPEED HER THROTTLE ASKED FOR — a " +
                 "head sea, a foul tide, a fouled bottom, a towed load, a dragging anchor. One term covers " +
                 "every cause, because they all produce the same physical signature.\n\n" +
                 "⚠ SHIPS AT 0, DELIBERATELY. Slip needs a MEASURED per-hull top speed that does not " +
                 "exist yet, and deriving one from the hull stats is the exact mistake BowSprayGrading's " +
                 "remarks warn about. At 0 the model reduces to the simple version — load is hold weight " +
                 "only, and weather is priced entirely by the sea surcharge — and it becomes the full " +
                 "model later by moving this one number off zero. One formula, two phases, no branch. " +
                 "Proposed value once the measured field exists: 0.5.")]
        [Range(0f, 1f)] public float SlipLoadWeight;

        [Header("The sea")]
        [Tooltip("The most a storm adds over glass, as a fraction: 0.60 = a full gale costs 60% more fuel " +
                 "for the same throttle and the same load. Scales on the CONTINUOUS sea-state axis " +
                 "(WeatherModel.SeaState01), never the stepped enum — fuel is a sim quantity, and a cost " +
                 "that lurched at a band edge would be unreadable.\n\n" +
                 "⚠ THE SEA IS COUNTED TWICE, A LITTLE, AND THAT IS WHY THIS IS MODEST. A head sea slows " +
                 "her, which raises slip, which raises the load term — and then this charges for the same " +
                 "weather again. 0.60 rather than the ~0.9 it would want if it carried the head-sea cost " +
                 "alone. Raise it ONLY while SlipLoadWeight is 0, and never 'fix' the overlap by raising " +
                 "both.")]
        [Min(0f)] public float SeaSurcharge;

        [Header("The one dial that re-prices fuel for the whole game")]
        [Tooltip("⭐ Multiplies EVERY hull's burn. 1 = the authored rates as they stand. If fuel feels too " +
                 "thirsty or too free ACROSS THE BOARD, this is the number to move — not 38 Def fields.\n\n" +
                 "⚠ 0 means nothing ever burns and fuel is free. That is also what this field reads as if " +
                 "GameConfig.asset's YAML omits it, which is why the coverage test exists.")]
        [Min(0f)] public float BurnScale;

        [Header("Running dry")]
        [Tooltip("Where the low-fuel telegraph arms, as a fraction of the tank. 0.15 = she warns with " +
                 "about a sixth left — far enough out that the player still has the choice to turn back, " +
                 "which is the whole point of warning at all (P5: teeth, but the escape is in your hands).")]
        [Range(0f, 1f)] public float LowFuelFraction;

        [Tooltip("Does a boat you have just bought arrive FUELLED? True, because that is what a sale does, " +
                 "and a shipwright who hands you a dry boat you cannot move off his wharf is a bug shaped " +
                 "like realism. It is also the reading a save from before fuel existed is given.")]
        public bool NewBoatArrivesFull;

        /// <summary>
        /// The shipped tuning (§9.6.1). A square throttle curve over a near-free idle, a modest surcharge
        /// for a full hold, a storm that costs 60% more than glass, and slip switched OFF until a measured
        /// top speed exists to compute it from.
        ///
        /// <para>Sized so the dory's 10 L tank is about three working days and costs about one cod to
        /// fill, and a lobster boat's 85 L is three days run sensibly or one run greedily — the numbers
        /// the owner reads in §9.6.2's Days/tank column.</para>
        /// </summary>
        public static FuelSettings Default => new FuelSettings
        {
            IdleFraction = 0.08f,
            ThrottleExponent = 2f,
            LoadSurcharge = 0.35f,
            HoldLoadWeight = 0.5f,
            SlipLoadWeight = 0f,
            SeaSurcharge = 0.6f,
            BurnScale = 1f,
            LowFuelFraction = 0.15f,
            NewBoatArrivesFull = true,
        };
    }
}
