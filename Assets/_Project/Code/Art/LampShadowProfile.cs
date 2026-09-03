using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The owner-tunable look of LAMP-CAST SHADOWS (ADR 0016, lights PR B; CLAUDE.md rule 6 — no
    /// magic numbers). One asset says how dark a lamp's shadow is, how many the pool may draw at
    /// once, how the rake lengthens as a caster walks away from the lamp, and the pixel grid it
    /// snaps to. The self-installing <see cref="LampShadowSystem"/> reads exactly one of these
    /// (from <c>Resources/LampShadowProfile</c> if present, otherwise the built-in default — the
    /// same convention as <see cref="DayNightProfile"/>).
    ///
    /// <para><b>How to tune (owner).</b> <c>Assets ▸ Create ▸ Hidden Harbours ▸ Lighting ▸ Lamp
    /// Shadow Profile</c>, saved at <c>Assets/_Project/Resources/LampShadowProfile.asset</c> (the name
    /// matters — that is the path the system loads). <see cref="Strength"/> is THE dial: 0 is today's
    /// frame exactly, byte for byte (the passthrough the render fixture proves).</para>
    ///
    /// <para><b>Determinism (rule 5).</b> Authored constants only; a shadow is a pure function of
    /// (the published day/night tint, the lamps, the casters) evaluated against these, and nothing
    /// here is saved or randomised.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Lighting/Lamp Shadow Profile", fileName = "LampShadowProfile")]
    public sealed class LampShadowProfile : ScriptableObject
    {
        [Header("Darkness")]
        [Tooltip("THE dial. The fraction of the light at a pixel a lamp's shadow removes, at the caster's " +
                 "feet, at the cone's core: 1 = a fully shadowed pixel keeps none of the lamp's light, 0 = " +
                 "no lamp shadows at all (today's frame, byte-identical). The cone's own feathering, the " +
                 "night-gate and a dimmed searchlight all scale it down from here.")]
        [Range(0f, 1f)] [SerializeField] private float _strength = 0.8f;

        [Tooltip("The colour a fully shadowed pixel is pulled TOWARD (rgb). Near-black with a hint of the " +
                 "cool sky reads best on the North-Atlantic palette — the same swatch the sun shadow uses.")]
        [SerializeField] private Color _shadowColor = new Color(0.04f, 0.05f, 0.10f, 1f);

        [Header("Budget (rule 7)")]
        [Tooltip("The POOL: how many lamp shadows may be drawn at once, across every lamp and every " +
                 "caster in range. Past this the NEAREST lamp-to-caster pairs win. Each is one quad, one " +
                 "shared material, no per-frame allocation.")]
        [Range(1, 64)] [SerializeField] private int _maxShadows = 24;

        [Tooltip("How often (Hz) the pairing of lamps to casters is re-decided. The pose of the chosen " +
                 "shadows follows every frame regardless (a boat sails, a beam sweeps); only the CHOICE " +
                 "of which pairs to draw is throttled.")]
        [Min(1f)] [SerializeField] private float _refreshHz = 10f;

        [Header("Length (× the caster's height) — the sun's own curve, driven by the LAMP's elevation")]
        [Tooltip("Shadow length when the lamp is straight overhead the caster — a short stub under the feet.")]
        [Min(0f)] [SerializeField] private float _lengthAtNoon = 0.35f;

        [Tooltip("Shadow length when the lamp sits on the caster's horizon (far away, or very low) — the " +
                 "long rake a searchlight throws behind a distant piling.")]
        [Min(0f)] [SerializeField] private float _lengthAtHorizon = 5f;

        [Tooltip("Hard CAP on the length (× height), so a low lamp cannot shoot a silhouette across the " +
                 "whole harbour.")]
        [Min(0f)] [SerializeField] private float _maxLength = 7f;

        [Tooltip("The lowest a lamp is ever treated as sitting above the ground its casters stand on " +
                 "(metres). A lamp that declares no height, or one declared at ground level, is floored " +
                 "here so its rake stays bounded.")]
        [Min(0.05f)] [SerializeField] private float _minLampHeightMeters = 0.5f;

        [Tooltip("How far a shadow thrown DOWN the screen (toward the camera) may compress before the " +
                 "shear is capped: the silhouette's vertical scale never falls below this. 0.2 = a shadow " +
                 "at the camera is at most five times foreshortened. Keeps the per-pixel un-shear invertible.")]
        [Range(0.05f, 1f)] [SerializeField] private float _minShearDenominator = 0.2f;

        [Header("Pixel art")]
        [Tooltip("Snap each shadow's anchor to the pixel grid so it stays crisp as the lamp sweeps (no " +
                 "shimmer). Off = smooth sub-pixel motion.")]
        [SerializeField] private bool _pixelSnap = true;

        [Tooltip("Pixels-per-unit the snap uses (match the project's sprite PPU).")]
        [Min(1f)] [SerializeField] private float _pixelsPerUnit = 32f;

        public float Strength { get => _strength; set => _strength = Mathf.Clamp01(value); }
        public Color ShadowColor { get => _shadowColor; set => _shadowColor = value; }
        public int MaxShadows { get => _maxShadows; set => _maxShadows = Mathf.Clamp(value, 1, 64); }
        public float RefreshHz { get => _refreshHz; set => _refreshHz = Mathf.Max(1f, value); }
        public float LengthAtNoon { get => _lengthAtNoon; set => _lengthAtNoon = Mathf.Max(0f, value); }
        public float LengthAtHorizon { get => _lengthAtHorizon; set => _lengthAtHorizon = Mathf.Max(0f, value); }
        public float MaxLength { get => _maxLength; set => _maxLength = Mathf.Max(0f, value); }
        public float MinLampHeightMeters { get => _minLampHeightMeters; set => _minLampHeightMeters = Mathf.Max(0.05f, value); }
        public float MinShearDenominator { get => _minShearDenominator; set => _minShearDenominator = Mathf.Clamp(value, 0.05f, 1f); }
        public bool PixelSnap { get => _pixelSnap; set => _pixelSnap = value; }
        public float PixelsPerUnit { get => _pixelsPerUnit; set => _pixelsPerUnit = Mathf.Max(1f, value); }

        /// <summary>
        /// The shipped defaults as an in-memory profile (no asset on disk), so the system works in
        /// EVERY scene with zero wiring — the self-installing requirement. The owner overrides it by
        /// shipping a tuned <c>Resources/LampShadowProfile.asset</c>.
        /// </summary>
        public static LampShadowProfile CreateDefault()
        {
            var p = CreateInstance<LampShadowProfile>();
            p.name = "LampShadowProfile (built-in default)";
            return p;
        }
    }
}
