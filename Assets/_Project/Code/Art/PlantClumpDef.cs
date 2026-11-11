using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>THE CLUMP LAW'S TUNABLES — one asset, every number the law reads (rule 6).</b>
    ///
    /// <para>A plant field's recipe scatter rolls every cell at one density, so a meadow comes out as
    /// white noise: the same spacing and the same two sizes from one headland to the other. Growth does
    /// not do that. It gathers, leaves ground bare, stands tall in the middle of a patch and small at its
    /// rim, and a dry patch is a paler green than a damp one. <see cref="PlantClumpLaw"/> makes the scatter
    /// do the same from a low-frequency field of world position and the field's seed; this asset says how
    /// hard.</para>
    ///
    /// <para><b><see cref="Strength"/> 0 is the scatter exactly as it shipped</b> — every site, stage and
    /// pixel. A field with no clump asset bound is the same thing. The law changes only what is DERIVED at
    /// load: no ground payload, authored site or scene byte moves, and authored sites are never touched.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Plant Clump Law")]
    public sealed class PlantClumpDef : ScriptableObject
    {
        /// <summary>The most tone steps a field can draw: the plant shader's table holds eight entries and
        /// the first is "no step".</summary>
        public const int MaxToneSteps = 7;

        public string Id;

        [Tooltip("The master strength. 0 = the scatter exactly as it shipped; 1 = the full law below.")]
        [Range(0, 1)] public float Strength = 0.8f;

        [Header("Where growth gathers")]
        [Tooltip("Size in metres of the patches the field breaks into: about one clump and one gap per two patches.")]
        [Min(0.5f)] public float PatchMetres = 6f;

        [Tooltip("Size in metres of the finer breakup laid over the patches, so a clump's edge is ragged and not a blob.")]
        [Min(0.25f)] public float DetailMetres = 2.2f;

        [Tooltip("How much the finer breakup counts against the patches. 0 = smooth blobs.")]
        [Range(0, 1)] public float DetailWeight = 0.5f;

        [Tooltip("Where the clump field is below this, nothing is planted: the bare ground. Raise it for more bare ground.")]
        [Range(0, 1)] public float BareBelow = 0.38f;

        [Tooltip("Where the clump field is above this, the clump is at full gain. The band between is the clump's rim.")]
        [Range(0, 1)] public float FullAbove = 0.56f;

        [Tooltip("Density inside a clump, as a multiple of the recipe's. Above 1 the clumps are denser than the " +
                 "shipped scatter, which pays back the plants the bare ground gave up. 1 = thin only, never add.")]
        [Min(1)] public float ClumpGain = 1.45f;

        [Header("Size step (the atlas's own growth stages, never a scaled sprite)")]
        [Tooltip("Chance that a plant at the very rim of a clump stands one growth stage smaller. Falls to 0 at the clump's core.")]
        [Range(0, 1)] public float RimStepChance = 0.8f;

        [Tooltip("Share of those rim plants that step two stages down (to a seedling).")]
        [Range(0, 1)] public float SecondStepShare = 0.35f;

        [Tooltip("Chance that a young plant in the core of a clump stands as a mature one instead.")]
        [Range(0, 1)] public float CoreGrowChance = 0.5f;

        [Header("Tone step (discrete tints, lowest = the dry rim, highest = the lush core)")]
        [Tooltip("Size in metres of the tone drift that is independent of the clumps.")]
        [Min(0.5f)] public float TonePatchMetres = 11f;

        [Tooltip("How far the tone follows the clump field: 0 = an independent drift, 1 = rims are always the first step and cores the last.")]
        [Range(0, 1)] public float ToneFollowsClump = 0.5f;

        [Tooltip("Chance that one plant takes the step beside its patch's, so a patch is not one flat tint.")]
        [Range(0, 1)] public float ToneStrayChance = 0.15f;

        [Tooltip("The tints, multiplied over the plant's own colours. Each plant takes exactly one: steps, not a gradient. " +
                 "White is 'as drawn'. At most seven.")]
        [ColorUsage(false, true)] // a dry step may lift a channel above 1; the plain picker would clamp it
        public Color[] ToneSteps =
        {
            new Color(1.09f, 1.05f, 0.88f, 1f),
            new Color(1.04f, 1.02f, 0.95f, 1f),
            Color.white,
            new Color(0.94f, 0.97f, 0.95f, 1f),
            new Color(0.86f, 0.92f, 0.92f, 1f),
        };

        void OnValidate()
        {
            if (FullAbove < BareBelow) FullAbove = BareBelow;
            if (ToneSteps != null && ToneSteps.Length > MaxToneSteps) Array.Resize(ref ToneSteps, MaxToneSteps);
        }
    }
}
