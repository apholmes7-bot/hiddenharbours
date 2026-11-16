using System;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Strength, carrying, cardio and the energy of effort</b> (body slice 2). Pure functions of the body,
    /// its fitness levels and the numbers (rule 6). The anchor everything is measured from is the reference
    /// body (slice 1's default frame at height scale 1, at its lean mass, untrained): for it every factor here
    /// is the literal 1, so today's game is unchanged until the player trains or picks another body.
    ///
    /// <list type="bullet">
    /// <item><b>Strength</b> of a muscle group = <c>(mass / reference lean) ^ massExponent x (1 + trainable(frame)
    /// x level)</c>. A bigger body lifts more untrained; a bigger frame can also be trained further.</item>
    /// <item><b>Capacity</b> = reference capacity x the mean of leg and back strength.</item>
    /// <item><b>Carry walk</b> = <c>1 - slowAtCapacity x load / capacity</c>, never below the carry floor.</item>
    /// <item><b>Cardio walk</b> = <c>1 + speedGainMax x cardio</c>; moving costs <c>1 - savingMax x cardio</c>.</item>
    /// <item><b>Energy</b> of an activity = kcal per unit x units x mass / reference lean (x the cardio saving
    /// for movement).</item>
    /// </list>
    /// </summary>
    public static class StrengthMath
    {
        /// <summary>No load may slow the walk below this, whatever the config says (carrying slows, never stops).</summary>
        public const float MinimumCarryFloor = 0.6f;

        /// <summary>The four kinds, in save order (allocation-free iteration).</summary>
        public static readonly FitnessKind[] Kinds =
            { FitnessKind.Grip, FitnessKind.Back, FitnessKind.Legs, FitnessKind.Cardio };

        /// <summary>The curve a kind trains on: cardio has its own, the three muscle groups share one.</summary>
        public static FitnessCurve CurveFor(FitnessKind kind, in StrengthSettings st) =>
            kind == FitnessKind.Cardio ? st.Cardio : st.Strength;

        /// <summary>At a save write: carry every fitness level to <paramref name="now"/>. Null-safe.</summary>
        public static void SettleFitness(PlayerBodyDto body, double now, in StrengthSettings st, double secondsPerGameDay)
        {
            if (body == null || now < 0d) return;
            for (int i = 0; i < Kinds.Length; i++)
            {
                FitnessStateDto state = body.Get(Kinds[i]);
                FitnessMath.Settle(ref state, CurveFor(Kinds[i], st), now, secondsPerGameDay);
                body.Set(Kinds[i], state);
            }
        }

        /// <summary>The reference body's lean mass, kg: the yardstick for strength and effort.</summary>
        public static double ReferenceLeanKg(in BodySettings b) => BodyMath.LeanMassKg(b.DefaultFrame, 1f, b);

        /// <summary>How much stronger than untrained this frame can train to: small .. large by frame.</summary>
        public static float TrainableGain(float frame, in StrengthSettings st)
        {
            float f = frame < 0f ? 0f : frame > 1f ? 1f : frame;
            return st.SmallFrameTrainableGain + (st.LargeFrameTrainableGain - st.SmallFrameTrainableGain) * f;
        }

        /// <summary>A muscle group's strength against the reference body untrained (exactly 1 for it).</summary>
        public static float StrengthFactor(double massKg, float frame, float level, in BodySettings b, in StrengthSettings st)
        {
            double reference = ReferenceLeanKg(b);
            double ratio = reference > 0d && massKg > 0d ? massKg / reference : 1d;
            double size = ratio == 1d ? 1d : Math.Pow(ratio, st.MassExponent);
            double trained = level > 0f ? 1d + TrainableGain(frame, st) * Math.Min(level, 1f) : 1d;
            double factor = size * trained;
            return factor == 1d ? 1f : (float)factor;
        }

        /// <summary>What the body carries comfortably at a walk, kg: reference capacity x mean(legs, back).</summary>
        public static float CarryCapacityKg(float legStrength, float pullStrength, in StrengthSettings st)
        {
            float mean = (legStrength + pullStrength) * 0.5f;
            return st.ReferenceCarryCapacityKg * (mean > 0f ? mean : 0f);
        }

        /// <summary>The walk under a load: exactly 1 with none, falling with load over capacity, floored.</summary>
        public static float CarryWalkFactor(float loadKg, float capacityKg, in StrengthSettings st)
        {
            if (!(loadKg > 0f)) return 1f;
            float floor = st.CarryWalkFloor < MinimumCarryFloor ? MinimumCarryFloor
                        : st.CarryWalkFloor > 1f ? 1f : st.CarryWalkFloor;
            if (!(capacityKg > 0f)) return floor;
            float slow = st.CarrySlowAtCapacity < 0f ? 0f : st.CarrySlowAtCapacity > 1f ? 1f : st.CarrySlowAtCapacity;
            float factor = 1f - slow * (loadKg / capacityKg);
            return factor > floor ? factor : floor;
        }

        /// <summary>The walk from cardio: exactly 1 untrained, up to 1 + the speed gain at full cardio.</summary>
        public static float CardioWalkFactor(float cardioLevel, in StrengthSettings st)
        {
            if (!(cardioLevel > 0f)) return 1f;
            float gain = st.CardioSpeedGainMax < 0f ? 0f : st.CardioSpeedGainMax;
            return 1f + gain * Math.Min(cardioLevel, 1f);
        }

        /// <summary>What moving costs against untrained: 1 untrained, down by the saving at full cardio.</summary>
        public static float MovementCostScale(float cardioLevel, in StrengthSettings st)
        {
            if (!(cardioLevel > 0f)) return 1f;
            float saving = st.CardioMovementSavingMax < 0f ? 0f : st.CardioMovementSavingMax > 0.9f ? 0.9f : st.CardioMovementSavingMax;
            return 1f - saving * Math.Min(cardioLevel, 1f);
        }

        /// <summary>The kcal above resting that <paramref name="units"/> of <paramref name="activity"/> cost this
        /// body: per-unit kcal x units x mass / reference lean, less the cardio saving if it is movement.</summary>
        public static double ActivityKcal(ActivityDef activity, float units, double massKg, float cardioLevel,
                                          in BodySettings b, in StrengthSettings st)
        {
            if (activity == null || !(units > 0f) || !(activity.KcalPerUnitAboveRest > 0f)) return 0d;
            double reference = ReferenceLeanKg(b);
            double size = reference > 0d && massKg > 0d ? massKg / reference : 1d;
            double kcal = (double)activity.KcalPerUnitAboveRest * units * size;
            if (activity.Kind == ActivityKind.Movement) kcal *= MovementCostScale(cardioLevel, st);
            return kcal;
        }
    }
}
