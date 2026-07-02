using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The owner-tunable art-direction for the whole-game 24-hour lighting cycle (ADR 0013, CLAUDE.md
    /// rule 6 — "no magic numbers", the day is data the owner edits, not constants in code). One asset
    /// describes how the global day/night tint, the sun's arc, and the weather→light coupling behave; the
    /// self-installing <see cref="DayNightController"/> reads exactly one of these (from
    /// <c>Resources/DayNightProfile</c> if present, otherwise a built-in default — see
    /// <see cref="CreateDefault"/>) and pushes the result to the shaders.
    ///
    /// <para><b>How to tune (owner).</b> Create one via <c>Assets ▸ Create ▸ Hidden Harbours ▸ Lighting ▸
    /// Day-Night Profile</c>, save it at <c>Assets/_Project/Resources/DayNightProfile.asset</c> (the name
    /// matters — that is the path the controller loads), then edit the <see cref="SkyTint"/> gradient and
    /// <see cref="Intensity"/> curve to art-direct the day. Everything is live: scrub the clock and watch
    /// the look change. No code, no scene wiring.</para>
    ///
    /// <para><b>Determinism (rule 5).</b> This holds only authored constants/curves; the cycle is a pure
    /// function of <c>(clock hour, weather)</c> evaluated against it (see <see cref="DayNightMath"/>) and
    /// nothing here is saved or randomised.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Lighting/Day-Night Profile", fileName = "DayNightProfile")]
    public sealed class DayNightProfile : ScriptableObject
    {
        [Header("Day tint (the whole-screen multiply colour over 24h)")]
        [Tooltip("The global MULTIPLY colour across the day fraction (0 = midnight, 0.5 = noon, 1 = " +
                 "midnight). Warm + low at dawn, bright neutral/cool at noon, orange-red at dusk, dark " +
                 "BLUE + low at night. The whole scene (sprites, water, grass) is multiplied by this, so " +
                 "a dark night swatch genuinely darkens everything — boat lights will matter (P1).")]
        [SerializeField] private Gradient _skyTint = new Gradient();

        [Tooltip("Overall brightness across the day fraction (0..1), multiplied into the tint above. Lets " +
                 "you dim the night HARD (toward 0) without changing the tint hue. Leave flat at 1 to let " +
                 "the gradient carry all the brightness, or pull the night down here for a darker night.")]
        [SerializeField] private AnimationCurve _intensity = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [Header("Sun arc (drives specular now, shadows in PR 2)")]
        [Tooltip("Hour the sun crosses the horizon at dawn (0..24). Before this it is night.")]
        [Range(0f, 24f)] [SerializeField] private float _sunriseHour = 6f;
        [Tooltip("Hour the sun crosses the horizon at dusk (0..24). After this it is night. Keep " +
                 "sunrise < sunset; solar noon sits halfway between.")]
        [Range(0f, 24f)] [SerializeField] private float _sunsetHour = 20f;
        [Tooltip("How far north (up the screen) every shadow leans even at a low sun — the sun sits in " +
                 "the SOUTH, so shadows always have a slight northward tilt. 0 = shadows run dead E↔W at " +
                 "dawn/dusk.")]
        [Range(0f, 1f)] [SerializeField] private float _shadowSouthBias = 0.2f;
        [Tooltip("Extra northward push at noon, so the midday shadow points straight UP the screen and " +
                 "reads as 'short, sun overhead'. Added on top of the south-bias as the sun climbs.")]
        [Range(0f, 2f)] [SerializeField] private float _shadowNoonLift = 0.9f;

        [Header("Weather → light coupling (the hook future weather plugs into)")]
        [Tooltip("Visibility (1 = clear, 0 = thick fog; = EnvironmentSample.Visibility) at/below which fog " +
                 "dims the light to its full weather-dim. Higher = fog bites sooner.")]
        [Range(0f, 1f)] [SerializeField] private float _fogVisibilityForFullDim = 0.15f;
        [Tooltip("Sea-state fraction (Glass=0 .. Storm=1) at which storm gloom STARTS dimming the light; " +
                 "it ramps from here to a full storm. ~0.6 ≈ a Gale. Calm seas add no gloom.")]
        [Range(0f, 1f)] [SerializeField] private float _seaStateDimStart = 0.6f;
        [Tooltip("The MOST that weather alone may dim/cool the daylight (0..1). Caps it so even a full " +
                 "storm at noon never blacks the screen out — night is the dark, weather is the gloom on top.")]
        [Range(0f, 1f)] [SerializeField] private float _weatherDimMax = 0.6f;
        [Tooltip("The cool, desaturated overcast colour the light shifts TOWARD under cloud/storm (before " +
                 "the time-of-day brightness is applied). A flat North-Atlantic grey.")]
        [SerializeField] private Color _overcastTint = new Color(0.5f, 0.55f, 0.62f, 1f);
        [Tooltip("How much FULL overcast erases a cast shadow (0 = shadows survive any weather, 1 = heavy " +
                 "cloud removes the shadow entirely — no sun through the cloud, no shadow). PR-2 shadows.")]
        [Range(0f, 1f)] [SerializeField] private float _overcastFadesShadow = 0.85f;

        [Header("Moonlight (a lit moon softly lifts the night; a new moon stays pitch dark)")]
        [Tooltip("The cool silver-blue colour MOONLIGHT pulls the night tint TOWARD while the sun is down " +
                 "and the moon is up. Only the direction of the lift — how far it goes is the strength below " +
                 "times the moon's phase + height (from the same deterministic MoonMath the water's " +
                 "reflected moon uses).")]
        [SerializeField] private Color _moonlightTint = new Color(0.62f, 0.70f, 0.90f, 1f);

        [Tooltip("The MOST moonlight may lift the night tint toward the colour above (0 = feature OFF, " +
                 "nights are moon-blind). Reached only by a FULL moon at the PEAK of its arc under a CLEAR " +
                 "sky; a new moon or a set moon adds exactly nothing, and overcast suppresses it like the " +
                 "rest of the light. The default 0.05 makes a clear full-moon midnight read roughly TWICE " +
                 "as bright as a new-moon one — subtly lit, still night.")]
        [Range(0f, 1f)] [SerializeField] private float _moonlightLiftMax = 0.05f;

        // ---- read-only accessors (the controller + DayNightMath read these) ----
        public Gradient SkyTint => _skyTint;
        public AnimationCurve Intensity => _intensity;
        public float SunriseHour => _sunriseHour;
        public float SunsetHour => _sunsetHour;
        public float ShadowSouthBias => _shadowSouthBias;
        public float ShadowNoonLift => _shadowNoonLift;
        public float FogVisibilityForFullDim => _fogVisibilityForFullDim;
        public float SeaStateDimStart => _seaStateDimStart;
        public float WeatherDimMax => _weatherDimMax;
        public Color OvercastTint => _overcastTint;
        public float OvercastFadesShadow => _overcastFadesShadow;
        public Color MoonlightTint => _moonlightTint;
        public float MoonlightLiftMax => _moonlightLiftMax;

        /// <summary>
        /// Author the shipped defaults onto a fresh asset (called from <see cref="Reset"/> in the editor and
        /// by <see cref="CreateDefault"/> at runtime): a North-Atlantic day — warm low dawn, bright slightly
        /// cool noon, orange-red dusk, and a GENUINELY dark blue night, with the brightness pulled hard down
        /// overnight so the later boat-lights have darkness to cut. Tunable afterwards.
        /// </summary>
        public void ApplyDefaults()
        {
            // Day fraction keys: 0 = midnight, 0.25 = dawn, 0.5 = noon, 0.75 = dusk, 1 = midnight.
            _skyTint = new Gradient
            {
                // NOTE: Unity Gradient allows a MAXIMUM of 8 colour keys. Keep this at <= 8.
                colorKeys = new[]
                {
                    new GradientColorKey(new Color(0.12f, 0.16f, 0.34f), 0.00f), // deep night blue
                    new GradientColorKey(new Color(0.18f, 0.22f, 0.40f), 0.20f), // pre-dawn
                    new GradientColorKey(new Color(0.95f, 0.66f, 0.45f), 0.27f), // warm low sunrise
                    new GradientColorKey(new Color(1.00f, 0.98f, 0.95f), 0.45f), // bright neutral morning
                    new GradientColorKey(new Color(0.98f, 1.00f, 1.00f), 0.50f), // cool bright noon
                    new GradientColorKey(new Color(1.00f, 0.55f, 0.32f), 0.74f), // orange-red sunset
                    new GradientColorKey(new Color(0.30f, 0.24f, 0.40f), 0.80f), // dusk purple
                    new GradientColorKey(new Color(0.12f, 0.16f, 0.34f), 1.00f), // back to night blue
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f),
                },
            };

            // Brightness: ~0.18 through the dead of night (dark, navigable later by lights), ramping up
            // around dawn to 1 across the day and back down by dusk.
            _intensity = new AnimationCurve(
                new Keyframe(0.00f, 0.18f),
                new Keyframe(0.22f, 0.20f),
                new Keyframe(0.30f, 0.85f),
                new Keyframe(0.50f, 1.00f),
                new Keyframe(0.70f, 0.90f),
                new Keyframe(0.80f, 0.30f),
                new Keyframe(0.88f, 0.18f),
                new Keyframe(1.00f, 0.18f));
            for (int i = 0; i < _intensity.length; i++)
                _intensity.SmoothTangents(i, 0f);

            _sunriseHour = 6f;
            _sunsetHour = 20f;
            _shadowSouthBias = 0.2f;
            _shadowNoonLift = 0.9f;
            _fogVisibilityForFullDim = 0.15f;
            _seaStateDimStart = 0.6f;
            _weatherDimMax = 0.6f;
            _overcastTint = new Color(0.5f, 0.55f, 0.62f, 1f);
            _overcastFadesShadow = 0.85f;
            _moonlightTint = new Color(0.62f, 0.70f, 0.90f, 1f);
            _moonlightLiftMax = 0.05f;
        }

        /// <summary>
        /// Build an in-memory default profile (no asset on disk) so the controller works in EVERY scene with
        /// ZERO wiring — the self-installing requirement. The owner overrides it by shipping a tuned
        /// <c>Resources/DayNightProfile.asset</c>.
        /// </summary>
        public static DayNightProfile CreateDefault()
        {
            var p = CreateInstance<DayNightProfile>();
            p.name = "DayNightProfile (built-in default)";
            p.ApplyDefaults();
            return p;
        }

#if UNITY_EDITOR
        // Give a freshly-created asset the shipped curves instead of an empty white gradient.
        private void Reset() => ApplyDefaults();
#endif
    }
}
