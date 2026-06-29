using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The BOAT SPOTLIGHT (ADR 0016) — the first concrete additive light: a warm CONE beam thrown forward off
    /// the bow onto the dark water, so the night becomes a thing you navigate BY YOUR LIGHT (the owner's M2/M3
    /// night-lighting vision, P1 "The Sea Has Moods" / P5 "Cozy but with Teeth"). Drop it on a boat and it
    /// configures + carries a <see cref="SceneLight"/> cone, anchored at/near the bow and oriented along the
    /// boat's HEADING, that follows and rotates with the hull and auto-gates to night (the gate is in the
    /// shader — see ADR 0016 / <see cref="LightMath"/>).
    ///
    /// <para><b>Reads the boat through Transform only (rule 4).</b> This component lives in the Art lane, which
    /// must NOT reference the Boats module. It is attached to the boat GameObject, so the boat's HEADING is
    /// simply its own <c>transform.up</c> (the bow, the same convention <see cref="BoatController"/> and the
    /// wake use) and the bow ANCHOR is a local offset forward along it — no cross-module reference at all. The
    /// optional "dim/off when not making way" gate measures the carrier's OWN speed frame-to-frame (so it works
    /// on the player boat AND NPC boats), again with zero coupling to Boats. (When a Core boat-kinematics seam
    /// is wired into the active boat, this can opt into it; today the transform-speed read is sufficient and
    /// dependency-free.)</para>
    ///
    /// <para><b>Composition, not duplication.</b> All the drawing/pooling/night-gating lives in
    /// <see cref="SceneLight"/>; this just owns one and pushes the boat-spotlight feel (warm, soft, forward) +
    /// the bow anchor + the way-gate. Mirrors the drop-on pattern (<see cref="SpriteShadow"/>). Visual-only:
    /// drives no sim, saves nothing (rule 5). Pooled, no per-frame alloc (rule 7).</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoatSpotlight : MonoBehaviour
    {
        [Header("Bow anchor")]
        [Tooltip("How far FORWARD of the boat origin (along the bow / transform.up, metres) the spotlight sits — " +
                 "push it out to the bow so the beam starts at the front of the hull.")]
        [SerializeField] private float _bowOffset = 0.6f;

        [Tooltip("Sideways offset of the lamp from the centreline (metres, positive = starboard). Usually 0 for " +
                 "a single bow light.")]
        [SerializeField] private float _sideOffset = 0f;

        [Header("Beam feel (warm soft forward spotlight by default — all tunable, rule 6)")]
        [Tooltip("Beam colour. A warm low-amber reads as a ship's spotlight cutting the cold North-Atlantic dark.")]
        [ColorUsage(true, true)] [SerializeField] private Color _color = new Color(1f, 0.88f, 0.62f, 1f);

        [Tooltip("Beam brightness (master intensity, before the night-gate + flicker).")]
        [Min(0f)] [SerializeField] private float _intensity = 1.5f;

        [Tooltip("How far the beam throws ahead of the bow (metres).")]
        [Min(0.5f)] [SerializeField] private float _range = 9f;

        [Tooltip("Cone HALF-angle (degrees): a tight searchlight (~15) to a broad floodlight (~50).")]
        [Range(0f, 90f)] [SerializeField] private float _coneHalfAngle = 26f;

        [Tooltip("Angular edge softness of the beam (0 = hard-edged cone, 1 = very soft-edged).")]
        [Range(0f, 1f)] [SerializeField] private float _angularSoftness = 0.45f;

        [Tooltip("Radial edge softness (how gently the beam fades to its far/round edge).")]
        [Range(0f, 1f)] [SerializeField] private float _edgeSoftness = 0.6f;

        [Tooltip("Subtle deterministic flicker (a lamp's living wobble). 0 = rock-steady searchlight.")]
        [Range(0f, 1f)] [SerializeField] private float _flickerAmount = 0.06f;

        [Header("Behaviour")]
        [Tooltip("Dim the beam toward off when the boat is barely moving (moored / aground / drifting), so the " +
                 "spotlight reads as a working searchlight while under way. Off = always on (at night).")]
        [SerializeField] private bool _dimWhenStationary = true;

        [Tooltip("Speed (m/s) at/above which the beam is at FULL brightness. Below it the beam dims toward off " +
                 "(linearly to zero at standstill). Ignored when 'Dim When Stationary' is off.")]
        [Min(0.01f)] [SerializeField] private float _fullBrightnessSpeed = 1.2f;

        [Tooltip("The least the beam dims to when stationary (0 = fully off when stopped, 1 = no dimming). A " +
                 "small floor keeps a faint glow at the bow even moored.")]
        [Range(0f, 1f)] [SerializeField] private float _stationaryFloor = 0.15f;

        private SceneLight _light;
        private Vector3 _lastPos;
        private float _smoothedSpeed;

        // expose for the editor menu / tests to read the configured light
        public SceneLight Light => _light;

        private void Awake()
        {
            _light = GetComponent<SceneLight>();
            if (_light == null) _light = gameObject.AddComponent<SceneLight>();
            ConfigureLight();
            _lastPos = transform.position;
        }

        private void OnEnable()
        {
            if (_light == null) _light = GetComponent<SceneLight>();
            ConfigureLight();
            _lastPos = transform.position;
            _smoothedSpeed = 0f;
        }

        /// <summary>Stamp the boat-spotlight feel onto the carried <see cref="SceneLight"/> (a forward warm cone at the bow).</summary>
        private void ConfigureLight()
        {
            if (_light == null) return;
            _light.Shape = SceneLight.LightShape.Cone;
            _light.ConeHalfAngle = _coneHalfAngle;
            _light.Range = _range;
            _light.Color = _color;
            _light.Intensity = _intensity;
            _light.FlickerAmount = _flickerAmount;
            _light.AngularSoftness = _angularSoftness;
            _light.EdgeSoftness = _edgeSoftness;
            // The bow anchor: forward along the boat heading (transform.up) plus any side offset. SceneLight
            // throws the cone along this transform's up, so the beam already points along the bow.
            _light.OriginOffset = new Vector2(_sideOffset, _bowOffset);
        }

        private void Update()
        {
            if (_light == null) return;

            // Re-apply the live-tunable feel so inspector edits show immediately (cheap, no alloc).
            _light.ConeHalfAngle = _coneHalfAngle;
            _light.Range = _range;
            _light.Color = _color;
            _light.FlickerAmount = _flickerAmount;
            _light.OriginOffset = new Vector2(_sideOffset, _bowOffset);

            float wayFactor = 1f;
            if (_dimWhenStationary)
            {
                // Measure the carrier's own speed (transform delta) — deterministic, no Boats reference, works
                // on any boat. Smooth it lightly so the beam doesn't strobe with per-frame jitter.
                float dt = Mathf.Max(Time.deltaTime, 1e-4f);
                float speed = (transform.position - _lastPos).magnitude / dt;
                _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, speed, 1f - Mathf.Exp(-dt * 6f));
                wayFactor = WayBrightness(_smoothedSpeed, _fullBrightnessSpeed, _stationaryFloor);
            }
            _lastPos = transform.position;

            _light.Intensity = _intensity * wayFactor;
        }

        /// <summary>
        /// PURE (testable): the way-gate brightness multiplier in <c>[floor, 1]</c> from the boat's speed — full
        /// (1) at/above <paramref name="fullSpeed"/>, ramping linearly down to <paramref name="floor"/> at a
        /// standstill. So the searchlight reads as "working" under way and fades (never fully snaps off, if the
        /// floor &gt; 0) when moored/aground/drifting. Deterministic; no scene. Defaults keep a faint moored glow.
        /// </summary>
        public static float WayBrightness(float speed, float fullSpeed, float floor)
        {
            float f = Mathf.Clamp01(floor);
            float t = Mathf.Clamp01(speed / Mathf.Max(fullSpeed, 1e-4f));
            return Mathf.Lerp(f, 1f, t);
        }
    }
}
