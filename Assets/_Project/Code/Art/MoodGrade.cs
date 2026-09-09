using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// One authored LOOK of the post-processing grade — every number the URP volume stack will be told,
    /// as plain owner-editable fields (rule 6). A <see cref="MoodGradeProfile"/> holds five of these (day,
    /// golden hour, night, fog, storm) and <see cref="MoodGradeMath"/> blends them by weight into the one
    /// grade the <see cref="MoodGradeDirector"/> writes to the screen.
    ///
    /// <para><b>Field ↔ effect.</b> Each group maps 1:1 onto a URP volume override so the owner can read the
    /// URP docs for what a number does: Bloom · Lift/Gamma/Gain (rgb = the trackball colour, w = its offset,
    /// <c>(1,1,1,0)</c> is neutral) · Color Adjustments · Vignette · Film Grain · Chromatic Aberration.
    /// An effect whose fields sit at their identity is NOT enabled on the volume at all
    /// (<see cref="MoodGradeStack"/>), which is how the "≤ 4 effects active" budget is kept: grain and
    /// chromatic aberration ship at 0 and cost nothing until the owner asks for them.</para>
    ///
    /// <para>Plain data. Nothing here is saved or randomised; the blend is a pure function of the world's
    /// published facts (rule 5).</para>
    /// </summary>
    [System.Serializable]
    public struct MoodGrade
    {
        [Header("Bloom (lamps, glints and lit windows halo; 0 = no bloom pass at all)")]
        [Tooltip("Strength of the bloom. 0 disables the effect entirely (no pass, no cost).")]
        [Min(0f)] public float BloomIntensity;
        [Tooltip("Brightness above which a pixel blooms. Sprites are mostly below 1, so ~0.9 blooms only " +
                 "the additive lights and water glints; lower it and the whole frame starts to glow.")]
        [Min(0f)] public float BloomThreshold;
        [Tooltip("How far the bloom spreads (0 tight .. 1 wide).")]
        [Range(0f, 1f)] public float BloomScatter;

        [Header("Lift / Gamma / Gain (rgb = trackball colour, w = offset; (1,1,1,0) is neutral)")]
        [Tooltip("Shadows. Push w slightly positive to lift the black point (fog); tint rgb cool for cold " +
                 "night shadows, warm for a low sun.")]
        public Vector4 Lift;
        [Tooltip("Midtones.")]
        public Vector4 Gamma;
        [Tooltip("Highlights. Warm rgb here is the golden-hour glow.")]
        public Vector4 Gain;

        [Header("Colour adjustments")]
        [Tooltip("Exposure in EV. Keep near 0 — the day/night MULTIPLY overlay already carries the darkness.")]
        public float PostExposure;
        [Tooltip("Contrast, -100..100. Negative flattens (fog), positive punches (storm).")]
        [Range(-100f, 100f)] public float Contrast;
        [Tooltip("Saturation, -100..100. Negative crushes toward grey (fog, night); positive is a bright day.")]
        [Range(-100f, 100f)] public float Saturation;
        [Tooltip("A multiply tint over the whole frame. White = none.")]
        public Color ColorFilter;

        [Header("Vignette (closes in as the weather does)")]
        [Tooltip("How far the vignette reaches in from the edges. 0 disables the effect.")]
        [Range(0f, 1f)] public float VignetteIntensity;
        [Tooltip("How soft the vignette's edge is.")]
        [Range(0.01f, 1f)] public float VignetteSmoothness;
        [Tooltip("The vignette's colour — black for night, fog-grey for fog.")]
        public Color VignetteColor;

        [Header("Film grain (ships at 0 — turning it on spends one of the four effect slots)")]
        [Range(0f, 1f)] public float GrainIntensity;
        [Range(0f, 1f)] public float GrainResponse;

        [Header("Chromatic aberration (OFF by default — charter: ships 0)")]
        [Range(0f, 1f)] public float ChromaticAberration;

        /// <summary>Neutral LGG trackball: no colour shift, no offset.</summary>
        public static readonly Vector4 NeutralTrackball = new Vector4(1f, 1f, 1f, 0f);

        /// <summary>
        /// The identity grade: every effect at the value that leaves the frame untouched. Blending toward
        /// this is "no grade"; writing it activates NOTHING on the volume.
        /// </summary>
        public static MoodGrade Neutral => new MoodGrade
        {
            BloomIntensity = 0f, BloomThreshold = 0.9f, BloomScatter = 0.7f,
            Lift = NeutralTrackball, Gamma = NeutralTrackball, Gain = NeutralTrackball,
            PostExposure = 0f, Contrast = 0f, Saturation = 0f, ColorFilter = Color.white,
            VignetteIntensity = 0f, VignetteSmoothness = 0.2f, VignetteColor = Color.black,
            GrainIntensity = 0f, GrainResponse = 0.8f,
            ChromaticAberration = 0f,
        };

        // ---- identity tests per effect (what MoodGradeStack uses to decide `active`) ----------------

        private const float IdentityEps = 1e-4f;

        public bool BloomIsIdentity => BloomIntensity <= IdentityEps;

        public bool LiftGammaGainIsIdentity =>
            IsNeutral(Lift) && IsNeutral(Gamma) && IsNeutral(Gain);

        public bool ColorAdjustmentsIsIdentity =>
            Mathf.Abs(PostExposure) <= IdentityEps && Mathf.Abs(Contrast) <= IdentityEps &&
            Mathf.Abs(Saturation) <= IdentityEps && IsWhite(ColorFilter);

        public bool VignetteIsIdentity => VignetteIntensity <= IdentityEps;

        public bool GrainIsIdentity => GrainIntensity <= IdentityEps;

        public bool ChromaticAberrationIsIdentity => ChromaticAberration <= IdentityEps;

        private static bool IsNeutral(Vector4 v) =>
            Mathf.Abs(v.x - 1f) <= IdentityEps && Mathf.Abs(v.y - 1f) <= IdentityEps &&
            Mathf.Abs(v.z - 1f) <= IdentityEps && Mathf.Abs(v.w) <= IdentityEps;

        private static bool IsWhite(Color c) =>
            Mathf.Abs(c.r - 1f) <= IdentityEps && Mathf.Abs(c.g - 1f) <= IdentityEps &&
            Mathf.Abs(c.b - 1f) <= IdentityEps;

        // ---- blending --------------------------------------------------------------------------------

        /// <summary>Linear blend of every field, <paramref name="t"/> clamped to 0..1.</summary>
        public static MoodGrade Lerp(in MoodGrade a, in MoodGrade b, float t)
        {
            t = Mathf.Clamp01(t);
            return new MoodGrade
            {
                BloomIntensity      = Mathf.Lerp(a.BloomIntensity, b.BloomIntensity, t),
                BloomThreshold      = Mathf.Lerp(a.BloomThreshold, b.BloomThreshold, t),
                BloomScatter        = Mathf.Lerp(a.BloomScatter, b.BloomScatter, t),
                Lift                = Vector4.Lerp(a.Lift, b.Lift, t),
                Gamma               = Vector4.Lerp(a.Gamma, b.Gamma, t),
                Gain                = Vector4.Lerp(a.Gain, b.Gain, t),
                PostExposure        = Mathf.Lerp(a.PostExposure, b.PostExposure, t),
                Contrast            = Mathf.Lerp(a.Contrast, b.Contrast, t),
                Saturation          = Mathf.Lerp(a.Saturation, b.Saturation, t),
                ColorFilter         = Color.Lerp(a.ColorFilter, b.ColorFilter, t),
                VignetteIntensity   = Mathf.Lerp(a.VignetteIntensity, b.VignetteIntensity, t),
                VignetteSmoothness  = Mathf.Lerp(a.VignetteSmoothness, b.VignetteSmoothness, t),
                VignetteColor       = Color.Lerp(a.VignetteColor, b.VignetteColor, t),
                GrainIntensity      = Mathf.Lerp(a.GrainIntensity, b.GrainIntensity, t),
                GrainResponse       = Mathf.Lerp(a.GrainResponse, b.GrainResponse, t),
                ChromaticAberration = Mathf.Lerp(a.ChromaticAberration, b.ChromaticAberration, t),
            };
        }

        /// <summary>
        /// Weighted sum of three grades. The weights are expected to be a partition (sum 1, each ≥ 0 —
        /// <see cref="MoodGradeMath.TimeOfDay"/> guarantees it); they are normalised defensively so a
        /// caller that hands in a near-partition cannot brighten or darken the frame by rounding.
        /// </summary>
        public static MoodGrade Mix3(in MoodGrade a, float wa, in MoodGrade b, float wb, in MoodGrade c, float wc)
        {
            wa = Mathf.Max(0f, wa); wb = Mathf.Max(0f, wb); wc = Mathf.Max(0f, wc);
            float sum = wa + wb + wc;
            if (sum <= 1e-6f) return a;
            wa /= sum; wb /= sum; wc /= sum;
            return new MoodGrade
            {
                BloomIntensity      = a.BloomIntensity * wa + b.BloomIntensity * wb + c.BloomIntensity * wc,
                BloomThreshold      = a.BloomThreshold * wa + b.BloomThreshold * wb + c.BloomThreshold * wc,
                BloomScatter        = a.BloomScatter * wa + b.BloomScatter * wb + c.BloomScatter * wc,
                Lift                = a.Lift * wa + b.Lift * wb + c.Lift * wc,
                Gamma               = a.Gamma * wa + b.Gamma * wb + c.Gamma * wc,
                Gain                = a.Gain * wa + b.Gain * wb + c.Gain * wc,
                PostExposure        = a.PostExposure * wa + b.PostExposure * wb + c.PostExposure * wc,
                Contrast            = a.Contrast * wa + b.Contrast * wb + c.Contrast * wc,
                Saturation          = a.Saturation * wa + b.Saturation * wb + c.Saturation * wc,
                ColorFilter         = a.ColorFilter * wa + b.ColorFilter * wb + c.ColorFilter * wc,
                VignetteIntensity   = a.VignetteIntensity * wa + b.VignetteIntensity * wb + c.VignetteIntensity * wc,
                VignetteSmoothness  = a.VignetteSmoothness * wa + b.VignetteSmoothness * wb + c.VignetteSmoothness * wc,
                VignetteColor       = a.VignetteColor * wa + b.VignetteColor * wb + c.VignetteColor * wc,
                GrainIntensity      = a.GrainIntensity * wa + b.GrainIntensity * wb + c.GrainIntensity * wc,
                GrainResponse       = a.GrainResponse * wa + b.GrainResponse * wb + c.GrainResponse * wc,
                ChromaticAberration = a.ChromaticAberration * wa + b.ChromaticAberration * wb + c.ChromaticAberration * wc,
            };
        }

        /// <summary>Every field clamped to the range URP accepts for it, so an offset can never push a
        /// parameter out of its slider.</summary>
        public MoodGrade Clamped()
        {
            var g = this;
            g.BloomIntensity      = Mathf.Max(0f, g.BloomIntensity);
            g.BloomThreshold      = Mathf.Max(0f, g.BloomThreshold);
            g.BloomScatter        = Mathf.Clamp01(g.BloomScatter);
            g.Contrast            = Mathf.Clamp(g.Contrast, -100f, 100f);
            g.Saturation          = Mathf.Clamp(g.Saturation, -100f, 100f);
            g.ColorFilter         = ClampColor(g.ColorFilter);
            g.VignetteIntensity   = Mathf.Clamp01(g.VignetteIntensity);
            g.VignetteSmoothness  = Mathf.Clamp(g.VignetteSmoothness, 0.01f, 1f);
            g.VignetteColor       = ClampColor(g.VignetteColor);
            g.GrainIntensity      = Mathf.Clamp01(g.GrainIntensity);
            g.GrainResponse       = Mathf.Clamp01(g.GrainResponse);
            g.ChromaticAberration = Mathf.Clamp01(g.ChromaticAberration);
            return g;
        }

        private static Color ClampColor(Color c) =>
            new Color(Mathf.Max(0f, c.r), Mathf.Max(0f, c.g), Mathf.Max(0f, c.b), 1f);
    }
}
