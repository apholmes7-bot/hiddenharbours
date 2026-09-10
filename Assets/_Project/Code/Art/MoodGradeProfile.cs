using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The owner's five LOOKS for the post-processing grade (juice charter PR 1; art bible §4.2 — "the
    /// master ramp is graded per condition, never swapped wholesale: identity persists, mood changes").
    /// One asset, five <see cref="MoodGrade"/> keys — <b>Day</b>, <b>Golden hour</b>, <b>Night</b>,
    /// <b>Fog</b>, <b>Storm</b> — every field of each an inspector number (rule 6). The self-installing
    /// <see cref="MoodGradeDirector"/> reads exactly one of these (from <c>Resources/MoodGradeProfile</c>
    /// if present, else <see cref="CreateDefault"/>) and blends the keys by the weights
    /// <see cref="MoodGradeMath"/> derives from the clock and the weather.
    ///
    /// <para><b>How to tune (owner).</b> The asset SHIPS at
    /// <c>Assets/_Project/Resources/MoodGradeProfile.asset</c>. Open it, edit a key, and scrub the clock
    /// in Play: the frame warms at the sun's edges, cools and blooms at night, flattens and closes in
    /// under fog. The blend TIMING (how wide the golden hour is, how fast night fades in, where fog and
    /// storm start) is <c>GameConfig ▸ Juice</c>, not here.</para>
    ///
    /// <para>⚠️ <b>The asset is the authority; the code default is the fallback for a scene that loads
    /// without it.</b> <c>MoodGradeProfileAssetTests</c> pins the two on the FEATURE facts (night is
    /// colder than day, golden hour is warmer, fog crushes saturation and closes the vignette, grain and
    /// chromatic aberration ship OFF) and leaves the owner's numbers alone.</para>
    ///
    /// <para><b>Budget (rule 7).</b> The four shipped keys use Bloom + Lift/Gamma/Gain + Color
    /// Adjustments + Vignette = the charter's four. Film grain and chromatic aberration are authored at 0
    /// and are not enabled on the volume at all until they are non-zero; turning one on spends a slot,
    /// and the director caps the stack at four regardless (<see cref="MoodGradeStack"/>).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Lighting/Mood Grade Profile", fileName = "MoodGradeProfile")]
    public sealed class MoodGradeProfile : ScriptableObject
    {
        [Header("Day — full daylight: neutral, fuller saturation, a light vignette")]
        [SerializeField] private MoodGrade _day;

        [Header("Golden hour — the sun at the horizon: warm lift, warm gain, amber filter")]
        [SerializeField] private MoodGrade _goldenHour;

        [Header("Night — deep blues, low value: cold shadows, lamps bloom, saturation pulled")]
        [SerializeField] private MoodGrade _night;

        [Header("Fog — lifted black point, saturation crushed to grey, the vignette closes in")]
        [SerializeField] private MoodGrade _fog;

        [Header("Storm — low value, high contrast, cold, tight vignette")]
        [SerializeField] private MoodGrade _storm;

        public MoodGrade Day        => _day;
        public MoodGrade GoldenHour => _goldenHour;
        public MoodGrade Night      => _night;
        public MoodGrade Fog        => _fog;
        public MoodGrade Storm      => _storm;

        /// <summary>
        /// The shipped look (what the asset was created with, and what <see cref="CreateDefault"/> hands a
        /// scene that loads without it): the art bible's §4.2 intent in numbers — warm lift at golden hour,
        /// cold shadows at night, the vignette closing in fog.
        /// </summary>
        public void ApplyDefaults()
        {
            _day        = DefaultDay();
            _goldenHour = DefaultGoldenHour();
            _night      = DefaultNight();
            _fog        = DefaultFog();
            _storm      = DefaultStorm();
        }

        public static MoodGrade DefaultDay()
        {
            var g = MoodGrade.Neutral;
            g.BloomIntensity = 0.3f;  g.BloomThreshold = 0.95f; g.BloomScatter = 0.7f;
            g.Contrast = 5f;          g.Saturation = 8f;
            g.VignetteIntensity = 0.15f; g.VignetteSmoothness = 0.4f; g.VignetteColor = Color.black;
            return g;
        }

        /// <summary>
        /// ⚠ <b>THE MULTIPLY OWNS THE COLOUR; THIS OWNS THE TONE.</b> <c>DayNightProfile._skyTint</c>
        /// already multiplies the WHOLE SCREEN by the dusk orange <c>(1, 0.55, 0.32)</c> at this hour.
        /// A warm colour filter and a warm lift on top of that is the second hue the owner saw on
        /// 2026-09-09 — "a very noticeable yellow filter before and after". So the hue here is a
        /// RESIDUE of 0.01: enough that the art bible §4.2 direction is still true of these numbers,
        /// far too little to tint a frame. What is left is what only a post pass can do — bloom,
        /// contrast, saturation, a vignette.
        /// </summary>
        public static MoodGrade DefaultGoldenHour()
        {
            var g = MoodGrade.Neutral;
            g.BloomIntensity = 0.5f;  g.BloomThreshold = 0.85f; g.BloomScatter = 0.75f;
            g.Lift = new Vector4(1.01f, 1.00f, 0.99f, 0.02f);   // a breath of warmth, not a filter
            g.Gain = new Vector4(1.01f, 1.00f, 0.99f, 0.00f);   // the black-point lift (.w) is TONE, kept
            g.Contrast = 10f;         g.Saturation = 12f;
            g.ColorFilter = Color.white;                        // the sky tint is the golden hour's colour
            g.VignetteIntensity = 0.12f; g.VignetteSmoothness = 0.45f; g.VignetteColor = Color.black;
            return g;
        }

        /// <summary>
        /// ⚠ Same law as <see cref="DefaultGoldenHour"/>. The midnight blue is
        /// <c>DayNightProfile._skyTint</c>'s <c>(0.12, 0.16, 0.34)</c> at 18% intensity — the whole
        /// screen is already night-coloured before this runs. Laying a second blue over it while −15
        /// saturation pulled the world's own colour out is exactly what the owner reported: it
        /// "completely washes everything out on the screen except the colour". The blue is a residue
        /// now and the saturation cut is a nudge; the lamps still bloom, which is the point of a night
        /// grade at all.
        /// </summary>
        public static MoodGrade DefaultNight()
        {
            var g = MoodGrade.Neutral;
            g.BloomIntensity = 0.8f;  g.BloomThreshold = 0.75f; g.BloomScatter = 0.8f;
            g.Lift  = new Vector4(0.99f, 0.995f, 1.01f, -0.02f); // shadows still read cold, barely
            g.Contrast = 10f;         g.Saturation = -6f;
            g.ColorFilter = Color.white;
            g.VignetteIntensity = 0.18f; g.VignetteSmoothness = 0.5f; g.VignetteColor = Color.black;
            return g;
        }

        public static MoodGrade DefaultFog()
        {
            var g = MoodGrade.Neutral;
            g.BloomIntensity = 0.2f;  g.BloomThreshold = 0.9f;  g.BloomScatter = 0.9f;
            g.Lift = new Vector4(1.02f, 1.02f, 1.02f, 0.04f);   // lifted black point
            g.Gain = new Vector4(0.95f, 0.95f, 0.95f, 0f);
            g.Contrast = -20f;        g.Saturation = -40f;
            g.ColorFilter = new Color(0.94f, 0.96f, 0.98f, 1f);
            g.VignetteIntensity = 0.4f; g.VignetteSmoothness = 0.6f; g.VignetteColor = new Color(0.60f, 0.64f, 0.68f, 1f);
            return g;
        }

        public static MoodGrade DefaultStorm()
        {
            var g = MoodGrade.Neutral;
            g.BloomIntensity = 0.25f; g.BloomThreshold = 0.9f;  g.BloomScatter = 0.7f;
            g.Lift = new Vector4(0.95f, 0.97f, 1.00f, -0.03f);
            g.Gain = new Vector4(0.90f, 0.93f, 0.98f, 0f);
            g.Contrast = 20f;         g.Saturation = -25f;
            g.ColorFilter = new Color(0.85f, 0.90f, 0.98f, 1f);
            g.VignetteIntensity = 0.35f; g.VignetteSmoothness = 0.4f; g.VignetteColor = new Color(0.05f, 0.06f, 0.08f, 1f);
            return g;
        }

        /// <summary>A runtime instance at the shipped defaults, for a scene that loads without the asset.</summary>
        public static MoodGradeProfile CreateDefault()
        {
            var p = CreateInstance<MoodGradeProfile>();
            p.name = "MoodGradeProfile (built-in default)";
            p.ApplyDefaults();
            return p;
        }

#if UNITY_EDITOR
        // Give a freshly-created asset the shipped keys instead of five all-zero grades.
        private void Reset() => ApplyDefaults();
#endif
    }
}
