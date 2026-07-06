using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// A PRECONFIGURED, ATTACH-AND-FORGET light source (ADR 0016) — the reusable pattern behind the owner's
    /// ratified lighting principle (2026-07-05): <em>lighting is AUTOMATIC (the day/night multiply overlay
    /// darkens every unlit sprite for free); the EXCEPTION is a light SOURCE, and some objects come
    /// PRECONFIGURED with one</em> — a house glows from its windows, a lamp post pools light on the ground, a
    /// vehicle has headlights. Drop this on such an object's prefab, pick a <see cref="LightPresets.Kind"/>, and
    /// the object CARRIES ITS OWN night light: it self-installs a <see cref="SceneLight"/> glow on wake, and that
    /// glow NIGHT-GATES automatically (on at dusk, off by day, smooth fade) — with ZERO per-object owner wiring.
    ///
    /// <para><b>Composition, not duplication (mirrors <see cref="BoatSpotlight"/>).</b> All the drawing / pooling
    /// / night-gating already lives in <see cref="SceneLight"/> (the additive quad above the day/night overlay,
    /// the in-shader gate off the published <c>_DayNightTint</c> — no clock read, ADR 0016). This component just
    /// OWNS one <see cref="SceneLight"/> and stamps the chosen preset's feel onto it via
    /// <see cref="LightPresets.Apply"/>, so a window glow looks like a window glow on EVERY cottage. Because the
    /// gate is in the shader, this component never reads the day/night cycle — exactly the "self-installing +
    /// automatic" contract every ambient effect in the project follows.</para>
    ///
    /// <para><b>Determinism (rule 5) / performance (rule 7) / seam discipline (rule 4).</b> The only variation is
    /// the deterministic hash flicker <see cref="SceneLight"/> already applies (never <see cref="System.Random"/>);
    /// it drives no sim and saves nothing. It reuses <see cref="SceneLight"/>'s one shared mesh + shared material
    /// (MPB-batched, no per-frame alloc). It references only <see cref="SceneLight"/> in its own lane — no
    /// cross-module reference at all.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PreconfiguredLight : MonoBehaviour
    {
        [Tooltip("Which preconfigured light this object carries. Window Glow = a soft warm interior spill (a lit " +
                 "house window); Lightpost = a warm lamp pool on the ground beneath a post; Worklight = a " +
                 "brighter cool steady work lamp. Each is a fixed, tunable look from LightPresets (rule 6) — a " +
                 "window glow reads the same on every cottage. All are RADIAL and night-gated automatically.")]
        [SerializeField] private LightPresets.Kind _preset = LightPresets.Kind.WindowGlow;

        [Tooltip("Extra master-intensity multiplier on top of the preset (1 = exactly the preset). Lets the owner " +
                 "dim/brighten this one placement without editing the shared preset — e.g. a fainter glow on a " +
                 "small window, a stronger one on a big lamp. The night-gate + flicker still scale it.")]
        [Min(0f)] [SerializeField] private float _intensityScale = 1f;

        // The carried glow (added on wake if absent, like BoatSpotlight adds its SceneLight). Exposed so the
        // editor menu / tests can read the configured light.
        private SceneLight _light;

        /// <summary>The configured <see cref="SceneLight"/> this object carries (null before wake).</summary>
        public SceneLight Light => _light;

        /// <summary>The preset this object carries (its light TYPE). Settable so a builder/menu can pick it.</summary>
        public LightPresets.Kind Preset
        {
            get => _preset;
            set { _preset = value; ConfigureLight(); }
        }

        private void Awake()
        {
            _light = GetComponent<SceneLight>();
            if (_light == null) _light = gameObject.AddComponent<SceneLight>();
            ConfigureLight();
        }

        private void OnEnable()
        {
            if (_light == null) _light = GetComponent<SceneLight>();
            ConfigureLight();
        }

        /// <summary>Stamp the chosen preset (and this placement's intensity scale) onto the carried light.</summary>
        private void ConfigureLight()
        {
            if (_light == null) return;
            LightPresets.Apply(_light, _preset);
            // Layer this placement's own intensity scale over the preset's base intensity (the preset set it just
            // above; multiply so the owner can trim ONE placement without editing the shared preset).
            _light.Intensity = LightPresets.For(_preset).Intensity * Mathf.Max(0f, _intensityScale);
        }

#if UNITY_EDITOR
        // Live-tune in the editor: re-stamp when a field changes in the inspector so the owner sees the preset /
        // intensity edit immediately (edit-mode only; SceneLight shows the glow via its no-cycle fallback).
        private void OnValidate()
        {
            if (_light == null) _light = GetComponent<SceneLight>();
            ConfigureLight();
        }
#endif
    }
}
