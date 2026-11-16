using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>All the body numbers in one block</b> on <see cref="GameConfig"/> (body slice 1, owner ask
    /// 2026-11-15: frame, weight, metabolism, overeating). Read through <see cref="GameServices.BodySettings"/>
    /// so an unwired config falls back to <see cref="Default"/>. Every value is the owner's to tune (rule 6).
    ///
    /// <para><b>The shape of the body.</b> FRAME (bone structure, 0 = small .. 1 = large, chosen in the
    /// creator and fixed afterwards) and HEIGHT (a scale on <see cref="ReferenceHeightMetres"/>, the rig's
    /// own 0.92..1.08 convention) give three masses by body-mass index (mass = BMI x height squared): a
    /// FLOOR the body never wastes below, a LEAN mass it is built for, and the MAX its frame can carry.</para>
    ///
    /// <para><b>The reference body</b> (<see cref="DefaultFrame"/>, height scale 1, at lean mass) is today's
    /// player: its hunger rate and walk speed are exactly the ones the needs slice shipped.</para>
    /// </summary>
    [Serializable]
    public struct BodySettings
    {
        [Header("Shape")]
        [Tooltip("Height in metres of a height scale of 1 (the creator's middle). The creator's height " +
                 "slider is a scale on this, like the character rig's own height field.")]
        [Min(0.5f)] public float ReferenceHeightMetres;

        [Tooltip("The shortest height scale the body accepts (the rig draws 0.92).")]
        [Range(0.5f, 1f)] public float MinHeightScale;

        [Tooltip("The tallest height scale the body accepts (the rig draws 1.08).")]
        [Range(1f, 1.5f)] public float MaxHeightScale;

        [Tooltip("The frame an existing save and an untouched creator get (0 small .. 1 large). The body " +
                 "with this frame, height scale 1 and its lean mass is today's player, exactly.")]
        [Range(0f, 1f)] public float DefaultFrame;

        [Header("Masses by body-mass index (kg per square metre of height)")]
        [Tooltip("Lean BMI of the SMALLEST frame: the mass a small-boned body is built for.")]
        [Min(10f)] public float SmallFrameLeanBmi;
        [Tooltip("Lean BMI of the LARGEST frame.")]
        [Min(10f)] public float LargeFrameLeanBmi;
        [Tooltip("The most a SMALL frame can carry (BMI). Eating past it adds no mass.")]
        [Min(10f)] public float SmallFrameMaxBmi;
        [Tooltip("The most a LARGE frame can carry (BMI).")]
        [Min(10f)] public float LargeFrameMaxBmi;
        [Tooltip("The least a SMALL frame wastes to when starving (BMI). Hunger slows, it never stops: the " +
                 "body stops losing here.")]
        [Min(10f)] public float SmallFrameFloorBmi;
        [Tooltip("The least a LARGE frame wastes to (BMI).")]
        [Min(10f)] public float LargeFrameFloorBmi;

        [Header("Metabolism (Mifflin-St Jeor: 10 x kg + 6.25 x cm - 5 x age + constant, x activity)")]
        [Tooltip("Age in years used by the formula. The game has no ageing; one age for everyone.")]
        [Min(1f)] public float MetabolicAgeYears;
        [Tooltip("The formula's sex constant in kcal/day: +5 for men, -161 for women. Shipped at the " +
                 "midpoint, -78, so the body slider, not the sex, sets the appetite.")]
        public float MetabolicSexConstantKcal;
        [Tooltip("Physical activity level: daily need = resting need x this. 1.4 sedentary, 1.7 a working " +
                 "fisher, 2.0 very hard labour. No per-player activity signal exists yet, so one level.")]
        [Range(1f, 2.5f)] public float ActivityFactor;

        [Header("Weight change")]
        [Tooltip("Food energy that turns into one kg of body mass. Real fat is ~7700 kcal/kg; shipped at a " +
                 "fifth (1540) so a season of feasting shows. Higher = bodies change more slowly.")]
        [Min(100f)] public float KcalPerKgOfMass;
        [Tooltip("Fraction of surplus food energy the body stores (the rest is burnt digesting it).")]
        [Range(0f, 1f)] public float SurplusStoredFraction;
        [Tooltip("The most one meal can add, in kg, however much it overfills.")]
        [Min(0f)] public float MaxGainKgPerMeal;
        [Tooltip("Mass lost per IN-GAME day spent fully hungry (hunger at its top). A constant rate, so a " +
                 "slept-through night gives the same answer as one played minute by minute.")]
        [Min(0f)] public float StarvingLossKgPerGameDay;

        [Header("Walk")]
        [Tooltip("Walk speed scales as heightScale ^ this. 1 = stride in step with height (8 % taller walks " +
                 "8 % faster); 0.5 = the pendulum-leg law; 0 = height never changes the walk.")]
        [Range(0f, 2f)] public float HeightStrideExponent;
        [Tooltip("Walk speed taken off per whole lean mass carried above lean (0.35: a body 50 % over its " +
                 "lean mass walks 17.5 % slower). Below lean there is no bonus.")]
        [Range(0f, 1f)] public float SlowPerExtraLeanMass;
        [Tooltip("The slowest the BODY alone can make the walk, as a fraction of normal. The needs have " +
                 "their own floor; the two together never go below half speed.")]
        [Range(0.6f, 1f)] public float WalkFloor;

        [Header("Stomach and overeating")]
        [Tooltip("How full the stomach can get, in MEALS (a full meal is 1, what lowers hunger by 1). " +
                 "Eating past this makes the player sick: the meal comes back up and a mess is left.")]
        [Min(0.1f)] public float StomachCapacityMeals;
        [Tooltip("How many meals the stomach empties per IN-GAME hour.")]
        [Min(0.01f)] public float StomachEmptiesPerGameHour;

        /// <summary>The shipped starting numbers (guesses for the owner to tune by feel).</summary>
        public static BodySettings Default => new BodySettings
        {
            ReferenceHeightMetres = 1.75f,
            MinHeightScale = 0.92f,
            MaxHeightScale = 1.08f,
            DefaultFrame = 0.5f,

            SmallFrameLeanBmi = 20f,
            LargeFrameLeanBmi = 24f,
            SmallFrameMaxBmi = 30f,
            LargeFrameMaxBmi = 40f,
            SmallFrameFloorBmi = 17f,
            LargeFrameFloorBmi = 20f,

            MetabolicAgeYears = 30f,
            MetabolicSexConstantKcal = -78f,
            ActivityFactor = 1.7f,

            KcalPerKgOfMass = 1540f,
            SurplusStoredFraction = 0.75f,
            MaxGainKgPerMeal = 1f,
            StarvingLossKgPerGameDay = 1.5f,

            HeightStrideExponent = 1f,
            SlowPerExtraLeanMass = 0.35f,
            WalkFloor = 0.75f,

            StomachCapacityMeals = 1.5f,
            StomachEmptiesPerGameHour = 0.25f,
        };
    }
}
