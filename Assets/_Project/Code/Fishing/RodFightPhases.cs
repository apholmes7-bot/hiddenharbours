using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// <b>THE one place</b> the Fishing-internal <see cref="RodFightPhase"/> (Deep/Surface — the fight
    /// maths' two-value domain) maps to the Core <see cref="FishingPhase"/> wire vocabulary
    /// (<see cref="FishingPhase.FightDeep"/>/<see cref="FishingPhase.FightSurface"/>) and back
    /// (Wave-3 carried thread). The internal enum is deliberately KEPT rather than eliminated:
    /// <see cref="RodFightMath"/>'s shipped Wave-1 signatures take it, and a two-value domain enum keeps
    /// the maths honest — it cannot be handed <c>Waiting</c> or <c>Landed</c> by mistake, which a Core
    /// <see cref="FishingPhase"/> parameter would allow. Everything that crosses the module boundary
    /// converts HERE and nowhere else, so the two vocabularies can never skew.
    /// </summary>
    public static class RodFightPhases
    {
        /// <summary>The Core wire phase for a fight phase — what the controller publishes.</summary>
        public static FishingPhase ToFishingPhase(RodFightPhase phase)
            => phase == RodFightPhase.Surface ? FishingPhase.FightSurface : FishingPhase.FightDeep;

        /// <summary>The fight-maths phase for a Core wire phase. Only the two v2 fight phases are a
        /// fight; anything else returns false with the safe, steer-ignoring <see cref="RodFightPhase.Deep"/>
        /// (the same posture <see cref="RodFightMath.PhaseFor"/> takes on bad input).</summary>
        public static bool TryFromFishingPhase(FishingPhase phase, out RodFightPhase fightPhase)
        {
            fightPhase = phase == FishingPhase.FightSurface ? RodFightPhase.Surface : RodFightPhase.Deep;
            return phase == FishingPhase.FightSurface || phase == FishingPhase.FightDeep;
        }
    }

    /// <summary>
    /// How a species' <see cref="RodFightDef.Strength"/> personality dial reaches the fight maths
    /// (Wave-3 carried thread — the Def's tooltip promises "RodFightMath scales the run pressure by
    /// this personality dial", and this is where that promise is kept). One pure mapping, used by the
    /// live fight AND by content validation, so the authored numbers are always judged at the values
    /// the fight will actually run.
    ///
    /// <para><b>The mapping:</b> the dial is a straight multiplier on the run's tension pressure,
    /// <c>×(2·Strength)</c> — <c>0.5</c> (the field default) runs the authored <c>runTensionPressure</c>
    /// exactly as written, <c>0</c> is a fish whose runs load nothing (a gentle schoolie), <c>1</c>
    /// doubles them (the barn door). Because the forgiving-cove invariant
    /// (<see cref="RodFightMath.MaintainOutbleedsTheRun"/>) must hold at the EFFECTIVE pressure, the
    /// content sweep asserts it against <see cref="EffectiveRunPressure"/>, not the raw field.</para>
    /// </summary>
    public static class RodFightStrength
    {
        /// <summary>The Strength dial's neutral point — at this value the authored rates run as
        /// written (the <see cref="RodFightDef.Strength"/> field default).</summary>
        public const float NeutralStrength01 = 0.5f;

        /// <summary>The run-pressure multiplier for a strength dial: <c>2·strength01</c>, so
        /// <see cref="NeutralStrength01"/> → ×1. Monotonically increasing — a stronger fish always
        /// loads the line harder (the tested direction). Pure, NaN-safe.</summary>
        public static float RunPressureScale(float strength01)
        {
            float s = float.IsNaN(strength01) ? NeutralStrength01 : Mathf.Clamp01(strength01);
            return s / NeutralStrength01;
        }

        /// <summary>The tension pressure her runs actually apply: the authored
        /// <c>runTensionPressure</c> scaled by the personality dial.</summary>
        public static float EffectiveRunPressure(float runTensionPressure, float strength01)
            => Mathf.Max(0f, float.IsNaN(runTensionPressure) ? 0f : runTensionPressure)
               * RunPressureScale(strength01);
    }
}
