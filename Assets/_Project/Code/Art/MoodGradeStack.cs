using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The six URP volume overrides the grade can drive, on ONE runtime <see cref="VolumeProfile"/>, and
    /// the one method that writes a <see cref="MoodGrade"/> into them. Headless-testable: building the
    /// stack and writing a grade touch only <c>ScriptableObject</c>s — no camera, no GPU — so the EditMode
    /// tests can assert the effect count and the values without a render.
    ///
    /// <para><b>The budget lives here (rule 7, charter: "≤ 4 effects active").</b> An effect whose fields
    /// are at identity is set <c>active = false</c> — URP then skips it entirely. If a grade would activate
    /// more than <see cref="MaxActiveEffects"/>, the lowest-priority effects are dropped in a fixed order
    /// (chromatic aberration first, then film grain, then bloom) and <see cref="DroppedCount"/> says how
    /// many, so the owner's slot report can name what was cut rather than the frame silently costing more.
    /// The three tone effects — Color Adjustments, Lift/Gamma/Gain, Vignette — are never dropped: URP
    /// folds them into the single uber pass, so they are one cost, not three.</para>
    /// </summary>
    public sealed class MoodGradeStack
    {
        /// <summary>The charter's cap on simultaneously active volume overrides.</summary>
        public const int MaxActiveEffects = 4;

        public Bloom               Bloom               { get; private set; }
        public ColorAdjustments    ColorAdjustments    { get; private set; }
        public LiftGammaGain       LiftGammaGain       { get; private set; }
        public Vignette            Vignette            { get; private set; }
        public FilmGrain           FilmGrain           { get; private set; }
        public ChromaticAberration ChromaticAberration { get; private set; }

        /// <summary>How many overrides the last <see cref="Write"/> left active.</summary>
        public int ActiveCount { get; private set; }

        /// <summary>How many the last <see cref="Write"/> had to drop to stay under the cap.</summary>
        public int DroppedCount { get; private set; }

        /// <summary>
        /// Add the six overrides to <paramref name="profile"/> (every parameter's override flag ON, so the
        /// volume's values win over the global default profile) and return the handle that writes them.
        /// </summary>
        public static MoodGradeStack Build(VolumeProfile profile)
        {
            var s = new MoodGradeStack
            {
                Bloom               = GetOrAdd<Bloom>(profile),
                ColorAdjustments    = GetOrAdd<ColorAdjustments>(profile),
                LiftGammaGain       = GetOrAdd<LiftGammaGain>(profile),
                Vignette            = GetOrAdd<Vignette>(profile),
                FilmGrain           = GetOrAdd<FilmGrain>(profile),
                ChromaticAberration = GetOrAdd<ChromaticAberration>(profile),
            };
            // Nothing is on until a grade says so.
            s.Bloom.active = false; s.ColorAdjustments.active = false; s.LiftGammaGain.active = false;
            s.Vignette.active = false; s.FilmGrain.active = false; s.ChromaticAberration.active = false;
            return s;
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var existing))
            {
                existing.SetAllOverridesTo(true);
                return existing;
            }
            return profile.Add<T>(overrides: true);
        }

        /// <summary>
        /// Push every field of <paramref name="g"/> into its override and set each effect's <c>active</c>
        /// from whether the grade actually asks for it. Returns the active count after the cap.
        /// No allocation.
        /// </summary>
        public int Write(in MoodGrade g)
        {
            Bloom.intensity.value = g.BloomIntensity;
            Bloom.threshold.value = g.BloomThreshold;
            Bloom.scatter.value   = g.BloomScatter;

            LiftGammaGain.lift.value  = g.Lift;
            LiftGammaGain.gamma.value = g.Gamma;
            LiftGammaGain.gain.value  = g.Gain;

            ColorAdjustments.postExposure.value = g.PostExposure;
            ColorAdjustments.contrast.value     = g.Contrast;
            ColorAdjustments.saturation.value   = g.Saturation;
            ColorAdjustments.colorFilter.value  = g.ColorFilter;

            Vignette.intensity.value  = g.VignetteIntensity;
            Vignette.smoothness.value = g.VignetteSmoothness;
            Vignette.color.value      = g.VignetteColor;

            FilmGrain.intensity.value = g.GrainIntensity;
            FilmGrain.response.value  = g.GrainResponse;

            ChromaticAberration.intensity.value = g.ChromaticAberration;

            // Wanted, in KEEP priority (first kept, last dropped).
            bool wantTone     = !g.ColorAdjustmentsIsIdentity;
            bool wantLgg      = !g.LiftGammaGainIsIdentity;
            bool wantVignette = !g.VignetteIsIdentity;
            bool wantBloom    = !g.BloomIsIdentity;
            bool wantGrain    = !g.GrainIsIdentity;
            bool wantChroma   = !g.ChromaticAberrationIsIdentity;

            int active = 0, dropped = 0;
            ColorAdjustments.active    = Take(wantTone,     ref active, ref dropped);
            LiftGammaGain.active       = Take(wantLgg,      ref active, ref dropped);
            Vignette.active            = Take(wantVignette, ref active, ref dropped);
            Bloom.active               = Take(wantBloom,    ref active, ref dropped);
            FilmGrain.active           = Take(wantGrain,    ref active, ref dropped);
            ChromaticAberration.active = Take(wantChroma,   ref active, ref dropped);

            ActiveCount = active;
            DroppedCount = dropped;
            return active;
        }

        private static bool Take(bool wanted, ref int active, ref int dropped)
        {
            if (!wanted) return false;
            if (active >= MaxActiveEffects) { dropped++; return false; }
            active++;
            return true;
        }
    }
}
