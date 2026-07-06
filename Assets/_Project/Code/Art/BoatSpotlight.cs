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

        [Tooltip("Radial edge softness (how gently the beam fades to its far/round edge). Higher keeps the lit " +
                 "core from clipping to a hard disc — a searchlight pool that feathers out, not a stamped circle.")]
        [Range(0f, 1f)] [SerializeField] private float _edgeSoftness = 0.7f;

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

        [Header("Light the WATER from within the water shader (ADR 0016)")]
        [Tooltip("Also light the WATER (not just land). The additive QUAD lights land, but the URP 2D renderer " +
                 "draws the custom-shader water OVER the quad regardless of sorting order — so the water gets lit " +
                 "FROM WITHIN its own shader instead: this publishes the beam as GLOBAL shader uniforms " +
                 "(_BoatLight*) and the water fragment adds the cone to its own colour (no sorting dependency, " +
                 "composes with reflections/foam/palette). Off = the water term is disabled (intensity 0).")]
        [SerializeField] private bool _lightWater = true;

        [Tooltip("Water-side strength multiplier on the published beam intensity, so the owner can balance how " +
                 "strongly the beam reads on WATER vs LAND independently (land = the quad; water = this term). " +
                 "1 = the same intensity as land; higher = a stronger raking pool of light on the dark sea. " +
                 "Default 0.8 = a SEARCHLIGHT that reveals the waves, not a floodlamp that washes them flat — the " +
                 "water shader now MULTIPLY-brightens the sea's own colour by the cone weight (owner tunes the " +
                 "reveal strength/warmth via _BoatLightBrighten/_BoatLightTintAmount on Water.mat).")]
        [Min(0f)] [SerializeField] private float _waterStrength = 0.8f;

        [Tooltip("Frame DARKNESS (0 = bright noon .. 1 = pitch black) at/below which the WATER term is fully OFF " +
                 "(so the beam can't wash daylight water out). Mirrors SceneLight's night-gate; the water reads " +
                 "the same published day/night tint as the land quad does.")]
        [Range(0f, 1f)] [SerializeField] private float _gateThreshold = 0.12f;

        [Tooltip("Width of the WATER term's night-gate fade-in band above the threshold (small = a hard switch " +
                 "at dusk; wide = a slow ramp through twilight).")]
        [Range(0f, 1f)] [SerializeField] private float _gateSoftness = 0.35f;

        [Tooltip("What the WATER term shows when the day/night cycle ISN'T running (EditMode / a bare art scene / " +
                 "the demo before Play): 1 = fully show (so the beam reads for tuning), 0 = hidden. Mirrors how " +
                 "the water shader's other layers treat an unset day/night tint.")]
        [Range(0f, 1f)] [SerializeField] private float _gateFallback = 1f;

        [Header("Bounce with the hull (the lamp rocks on the rolling deck; ADR 0018 B2 read)")]
        [Tooltip("Let the beam BOB and SWAY with the boat's wave rock, like a lamp mounted on a rolling deck. " +
                 "The spotlight sits on the steady physics ROOT (correct for AIMING — the cone follows the bow), " +
                 "so on its own it does NOT share the visual rock. On, it reads the hull's current bob + roll off " +
                 "the boat's VISUAL child and nudges the published beam. Off = the beam sits rock-steady on the " +
                 "root (exactly today's behaviour). Glass calm ⇒ zero rock ⇒ zero sway either way.")]
        [SerializeField] private bool _bounceWithHull = true;

        [Tooltip("How much of the hull's visual BOB (its screen-vertical wave lift, world units) the lamp shares " +
                 "— 1 = the lamp bobs exactly with the deck, less = a stiffer mount. 0 disables the bob share.")]
        [Range(0f, 2f)] [SerializeField] private float _bounceBobScale = 1f;

        [Tooltip("How far the cone SWAYS per degree of the hull's visual ROLL (degrees of beam sway per degree " +
                 "of deck roll). Keep SMALL — a mounted lamp leans a LITTLE with the deck, it doesn't slew. 0 " +
                 "disables the sway. E.g. 0.5 = the cone leans half a degree for each degree the deck rolls.")]
        [Range(0f, 2f)] [SerializeField] private float _bounceSwayDegreesPerRoll = 0.5f;

        [Tooltip("The boat's VISUAL child transform the wave rock is applied to (e.g. FishingBoatVisual). The " +
                 "bounce reads its live bob + roll. Auto-found by name at runtime if left null; leaving it null on " +
                 "a boat with no such child simply means no bounce (the beam sits steady on the root).")]
        [SerializeField] private Transform _hullVisual;

        [Tooltip("Name of the visual child to auto-find when Hull Visual is null (the builder names it " +
                 "'FishingBoatVisual'). Reads the rock WITHOUT referencing the Boats module (rule 4 — Art stays " +
                 "decoupled): the bob is the child's world-Y deviation from its rest pose, the roll its renderer's " +
                 "world z-tilt.")]
        [SerializeField] private string _hullVisualName = "FishingBoatVisual";

        // ---- the published GLOBAL shader uniforms (the water shader reads these; ADR 0016) ----------------
        // ONE boat "water light" is published as a handful of globals (like _SunDir / _DayNightTint already are),
        // and the water fragment adds the cone illumination to its own col.rgb. ONE global light is enough for
        // now: the boat spotlight is THE night-nav light. The clean extension to MANY lights later is to publish
        // ARRAYS (_BoatLightPos[], ... with a _BoatLightCount) and loop in the shader; the single-light path is a
        // count-1 special case of that, so nothing here needs rethinking when a second light arrives.
        private static readonly int IdBoatLightPos    = Shader.PropertyToID("_BoatLightPos");     // xy = world lamp pos
        private static readonly int IdBoatLightDir    = Shader.PropertyToID("_BoatLightDir");     // xy = world beam axis (unit)
        private static readonly int IdBoatLightColor  = Shader.PropertyToID("_BoatLightColor");   // rgb = colour
        // x = effective intensity (master × way-gate × water-strength × flicker), y = range (m),
        // z = cos(halfAngle) (outer cone edge), w = cos(innerAngle) (fully-lit inner cone).
        private static readonly int IdBoatLightParams = Shader.PropertyToID("_BoatLightParams");
        // x = radial edge softness, y = night-gate threshold, z = night-gate softness, w = cycle-off fallback.
        private static readonly int IdBoatLightParams2 = Shader.PropertyToID("_BoatLightParams2");

        // Whether ANY BoatSpotlight has published a live water-light this frame, so a disabled/destroyed light
        // zeroes the global out (no stuck beam over the water). Set when a spotlight publishes; the publisher
        // that wrote last "owns" the global (single-light model). On disable we publish zero intensity.
        private SceneLight _light;
        private Vector3 _lastPos;
        private float _smoothedSpeed;
        private float _publishTimer;

        // ---- bounce-with-the-hull read state (decoupled from Boats — Transform/SpriteRenderer only, rule 4) ----
        // The hull visual (auto-found by name), its child whose local z-rotation carries the wave ROLL, and the
        // REST local pose we measure the live BOB against. BoatWaveMotion lifts the visual by (bob+pitch) in WORLD
        // Y off a FIXED base local pose (the builder parents FishingBoatVisual at local (0,0,0)); so the current
        // bob is the visual's world-Y deviation from where its rest local pose maps under the (possibly yawed +
        // moving) root — computed against the root's live transform, so it stays correct as the boat sails/turns.
        // We snapshot the rest local pose the first time we see the visual; any one-frame rock baked in is a tiny
        // constant bias, dwarfed by _bounceBobScale (and the builder's base is exactly zero anyway).
        private Transform _hullVisualCached;
        private Transform _rollSource;                 // the child whose local z-rotation carries the wave roll
        private bool _restCaptured;
        private Vector3 _restLocalPos;                 // the visual's rest LOCAL position (bob measured vs this)

        // How often (Hz) the water-light globals are re-published. The beam is slow; the sim throttles to a few
        // Hz elsewhere too (rule 7). The POSE the LAND quad follows every frame in SceneLight; the water globals
        // need only track the boat at a few Hz — cheap, no per-frame alloc.
        private const float PublishHz = 20f;

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
            _publishTimer = 0f;
            // Re-resolve the hull visual + its rest pose on wake (the boat may have been re-skinned / re-parented
            // while disabled). A fresh find on the next bounce read; the rest is re-snapshotted then.
            _hullVisualCached = null;
            _rollSource = null;
            _restCaptured = false;
        }

        private void OnDisable()
        {
            // The single-light model: a disabled/destroyed spotlight must turn the WATER term OFF, or the beam
            // would stick on the water with no boat. Publish zero intensity (the shader treats <= 0 as no light).
            PublishWaterLight(0f, Vector3.zero, Vector2.up);
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

            float effectiveIntensity = _intensity * wayFactor;
            _light.Intensity = effectiveIntensity;   // the LAND quad

            // ---- publish the WATER light globals (throttled) ------------------------------------------------
            // The water shader lights ITSELF from these (ADR 0016) — sorting-independent, unlike the land quad.
            _publishTimer -= Time.deltaTime;
            if (_publishTimer <= 0f)
            {
                _publishTimer = PublishHz > 0f ? 1f / PublishHz : 0.05f;
                // The lamp WORLD position (the bow anchor) and WORLD beam axis (the boat heading = transform.up),
                // the SAME geometry SceneLight throws the land cone along — so land + water read one beam.
                Vector3 lampWorld = transform.TransformPoint(new Vector3(_sideOffset, _bowOffset, 0f));
                Vector2 beamDir = transform.up;

                // ---- BOUNCE WITH THE HULL (ADR 0018 B2 read): a lamp on a rolling deck bobs + sways ----------
                // The spotlight sits on the steady physics ROOT (correct for AIMING — the cone follows the bow),
                // so on its own it does NOT share the wave rock BoatWaveMotion gives the hull's VISUAL child. Read
                // that visual's live BOB (its world-Y wave lift) + ROLL (its child renderer's z-tilt) WITHOUT a
                // Boats reference (Transform/SpriteRenderer only, rule 4), and nudge the published beam: offset the
                // lamp up/down by the bob, sway the cone a touch by the roll. Glass calm ⇒ zero rock ⇒ zero nudge.
                if (_bounceWithHull)
                {
                    ReadHullRock(out float bob, out float rollDegrees);
                    lampWorld.y += LightMath.BounceLampYOffset(bob, _bounceBobScale);
                    beamDir = LightMath.BounceSwayDir(beamDir, rollDegrees, _bounceSwayDegreesPerRoll);
                }

                // The water term reuses the SAME deterministic flicker SceneLight applies to the land quad, so the
                // two surfaces wobble together (LightMath.Flicker is a pure hash of (seed, time) — rule 5). Use a
                // stable per-boat seed so it is reproducible. The water-side strength lets the owner balance the
                // beam's read on water vs land independently.
                int seed = (name.GetHashCode() * 397) ^ transform.GetSiblingIndex();
                float flicker = LightMath.Flicker(seed, Time.time, _flickerAmount, 1f);
                float waterIntensity = _lightWater
                    ? Mathf.Max(0f, effectiveIntensity) * Mathf.Max(0f, _waterStrength) * flicker
                    : 0f;
                PublishWaterLight(waterIntensity, lampWorld, beamDir);
            }
        }

        /// <summary>
        /// Push the ONE boat "water light" to the GLOBAL shader uniforms the water fragment reads (ADR 0016).
        /// <paramref name="intensity"/> &lt;= 0 (light off / not lighting water / disabled) publishes a zero
        /// intensity, which the shader treats as "no beam" (the water term is skipped). The cone HALF-angle is
        /// published as a COSINE (the shader tests the cone with a single dot, no per-pixel trig), with the
        /// inner (fully-lit) cone derived from the angular softness so the water cone's feathering matches the
        /// land cone's <see cref="LightMath.ConeFalloff"/>. Visual-only: it sets render globals, reads no sim,
        /// saves nothing (rule 5); the values come from the boat transform (rule 4 — no Boats reference).
        /// </summary>
        private void PublishWaterLight(float intensity, Vector3 lampWorld, Vector2 beamDir)
        {
            float half = Mathf.Clamp(_coneHalfAngle, 0f, 89.5f);          // a cone (never a full radial here)
            float cosHalf = LightMath.CosFromHalfAngleDeg(half);
            float innerAngle = half * (1f - Mathf.Clamp01(_angularSoftness));   // fully-lit out to here
            float cosInner = LightMath.CosFromHalfAngleDeg(innerAngle);

            Shader.SetGlobalVector(IdBoatLightPos, new Vector4(lampWorld.x, lampWorld.y, 0f, 0f));
            Shader.SetGlobalVector(IdBoatLightDir, new Vector4(beamDir.x, beamDir.y, 0f, 0f));
            Shader.SetGlobalColor(IdBoatLightColor, _color);
            Shader.SetGlobalVector(IdBoatLightParams,
                new Vector4(Mathf.Max(0f, intensity), Mathf.Max(0.01f, _range), cosHalf, cosInner));
            Shader.SetGlobalVector(IdBoatLightParams2,
                new Vector4(Mathf.Clamp01(_edgeSoftness), Mathf.Clamp01(_gateThreshold),
                            Mathf.Clamp01(_gateSoftness), Mathf.Clamp01(_gateFallback)));
        }

        /// <summary>
        /// Read the hull's CURRENT wave rock off the boat's VISUAL child — the BOB (its screen-vertical wave
        /// lift, world units) and the ROLL (the renderer's WORLD z-tilt, degrees) — WITHOUT referencing the Boats
        /// module (Transform + SpriteRenderer only, rule 4 — the Art lane stays decoupled). BoatWaveMotion
        /// (ADR 0018 B2) lifts the visual by (bob+pitch) in WORLD Y off a fixed rest local pose and routes the
        /// roll through the renderer's WORLD z-rotation (screen-aligned + leant); so the bob is the visual's
        /// world-Y deviation from where its rest local pose maps under the live (yawed + moving) root, and the
        /// roll is the renderer's world euler z (heading-independent). Auto-finds + caches the visual (and its
        /// roll host) by name; if none is found the boat simply has no visual rock, so both are 0 (the beam sits
        /// steady — no bounce). Visual-only.
        /// </summary>
        private void ReadHullRock(out float bob, out float rollDegrees)
        {
            bob = 0f;
            rollDegrees = 0f;

            // Resolve (once, then cached) the visual child to read the rock from. Prefer the wired ref; else find
            // the named child (the builder names it "FishingBoatVisual"). No alloc on the cached path.
            if (_hullVisualCached == null)
            {
                _hullVisualCached = _hullVisual != null ? _hullVisual : FindChildByName(transform, _hullVisualName);
                _rollSource = null;
                _restCaptured = false;
                if (_hullVisualCached == null) return;    // no visual child on this boat -> no bounce
            }
            Transform visual = _hullVisualCached;

            // Snapshot the REST local pose once, so BOB is measured as a deviation from it (the un-rocked pose).
            if (!_restCaptured)
            {
                _restLocalPos = visual.localPosition;
                _restCaptured = true;
            }

            // BOB: the visual's world-Y minus where its REST local pose maps under the root's live transform.
            // Using world positions keeps it correct as the boat sails and yaws (a local-Y read would swing with
            // the hull). The builder's rest local is (0,0,0), so this is just the visual's world-Y lift.
            Vector3 restWorld = transform.TransformPoint(_restLocalPos);
            bob = visual.position.y - restWorld.y;

            // ROLL: the wave roll is composed onto the visual's renderer as a WORLD z-rotation each LateUpdate —
            // DirectionalBoatSprite.ApplySnap sets the renderer's world rotation to Euler(0,0,VisualTiltDegrees)
            // (it counter-rotates the root's heading yaw AWAY so the picture stays screen-aligned, then leans it
            // by the tilt) — so the renderer's WORLD z-euler IS the wave roll, independent of the boat's heading.
            // Read the visual's own SpriteRenderer transform (the tilt host); the world read stays correct as the
            // boat turns (a LOCAL read would fold the heading yaw back in). Cached after the first find.
            if (_rollSource == null)
            {
                var childSr = visual.GetComponentInChildren<SpriteRenderer>();
                _rollSource = childSr != null ? childSr.transform : visual;
            }
            rollDegrees = NormalizeSignedDegrees(_rollSource.eulerAngles.z);
        }

        /// <summary>Depth-first find a descendant Transform by exact name (no alloc beyond the recursion; runs
        /// once then the result is cached). Null if none matches — the boat then simply has no bounce source.</summary>
        private static Transform FindChildByName(Transform root, string childName)
        {
            if (string.IsNullOrEmpty(childName)) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform c = root.GetChild(i);
                if (c.name == childName) return c;
                Transform found = FindChildByName(c, childName);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Map a 0..360 Euler angle to a signed [-180, 180] so a small negative roll reads as a small
        /// negative sway (a wrapped 359° reads as -1°, not +359°). Pure.</summary>
        private static float NormalizeSignedDegrees(float deg)
        {
            deg %= 360f;
            if (deg > 180f) deg -= 360f;
            else if (deg < -180f) deg += 360f;
            return deg;
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
