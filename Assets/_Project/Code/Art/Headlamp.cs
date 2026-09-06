using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>The walker's headlamp</b> — the directed half of world-lighting PR 3: a narrow cone worn on her
    /// brow, switched, aimed wherever she is looking.
    ///
    /// <para><b>Why a headlamp and not a torch in her hand.</b> The owner asked for <i>"a spotlight"</i>.
    /// A hand torch would cost a hand — and the TOOL CONTINUITY LAW (owner playtest, 2026-08-23) says what
    /// she is holding does not silently change underneath her, so a torch would have to be taken out and
    /// put away, and would fight the rod and the catch for the same slot. A headlamp costs no slot, needs
    /// no rig prop, and is what people who work on boats at night actually wear.</para>
    ///
    /// <para><b>It lights the world, not just a patch of ground</b> — which is the whole reason it is a
    /// <see cref="SceneLight"/> and not a sprite:
    /// <list type="bullet">
    ///   <item>the GROUND, through <see cref="LampPoolSystem"/> off <see cref="SceneLight.ReachMetres"/>
    ///   (#736's pool — the thing the bloom used to stand in for);</item>
    ///   <item>the WATER, through <see cref="IWaterLightEmitter"/> — from a wharf edge her beam plays on
    ///   the sea by the same relief model the searchlight uses (#691), taking one of the bridge's four
    ///   nearest-lamp slots;</item>
    ///   <item>the TREES, shrubs and shore plants, through the lit-decor path — which reads one global
    ///   lamp, so who writes it is arbitrated: see <see cref="WalkerLights.OwnsDecorLight"/>;</item>
    ///   <item>and it throws CAST SHADOWS off whatever stands in it (<see cref="LampShadowSystem"/>) —
    ///   except her own, which the carrier rule declines.</item>
    /// </list></para>
    ///
    /// <para><b>Determinism (rule 5):</b> the beam's state is a transient the player toggles; nothing is
    /// saved, nothing is random, and the lamp is steady (a battery lamp does not flicker).</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SceneLight))]
    public sealed class Headlamp : MonoBehaviour, IWaterLightEmitter
    {
        [Tooltip("Is the beam switched ON? A transient — never saved, and off is the state she starts in.")]
        [SerializeField] private bool _on;

        [Tooltip("How hard the beam reads on WATER relative to land, so the owner can balance the two " +
                 "independently — the same dial BoatSpotlight's searchlight carries, and for the same reason.")]
        [Range(0f, 2f)] [SerializeField] private float _waterStrength = 0.8f;

        private SceneLight _light;
        private WaterLightState _waterLight;

        /// <summary>Is the beam lit?</summary>
        public bool IsOn => _on;

        /// <summary>The lamp this drives.</summary>
        public SceneLight Light => _light != null ? _light : _light = GetComponent<SceneLight>();

        /// <summary>Set the height it is worn at. The beam's SHAPE is not a parameter: it comes from
        /// <see cref="LightPresets.ConeHalfDegrees"/>, so the angle is written in exactly one place and
        /// the preset library's own guard can assert it. Called once by the carrier.</summary>
        public void Configure(float browHeightMetres)
        {
            SceneLight l = Light;
            LightPresets.Apply(l, LightPresets.Kind.Headlamp);
            l.Shape = SceneLight.LightShape.Cone;
            l.ConeHalfAngle = LightPresets.ConeHalfDegrees(LightPresets.Kind.Headlamp);
            l.AngularSoftness = 0.35f;                 // a feathered edge; a hard-edged cone reads as a cutout
            l.ReachMetres = LightPresets.ReachMetres(LightPresets.Kind.Headlamp);
            l.BloomLiftMetres = browHeightMetres;      // the lit fitting rides on her brow (#733)
            l.LampHeightMeters = browHeightMetres;     // and its cast shadows are thrown from there
            l.CastsShadows = true;
            ApplyBeamState();
        }

        /// <summary>Throw the switch.</summary>
        public void Toggle() => SetOn(!_on);

        /// <summary>Set the beam on or off.</summary>
        public void SetOn(bool on)
        {
            _on = on;
            ApplyBeamState();
        }

        /// <summary>
        /// Is this lamp hers to burn at all right now? Off her feet the whole component stands down —
        /// the renderer, the water slot and the decor singleton alike — so nothing of hers is lit while
        /// she is at a wheel.
        ///
        /// <para>⚠️ It does NOT clear <see cref="_on"/>. Stepping aboard and back off again should give her
        /// the beam she had, not a lamp she has to switch on twice; the state is hers, the LIVENESS is the
        /// mode's.</para>
        /// </summary>
        public void SetLive(bool live)
        {
            if (Light != null) Light.enabled = live && _on;
        }

        private void Awake() => _light = GetComponent<SceneLight>();

        // ⚠️ The bridge's own contract is register-on-enable / unregister-on-disable (WaterLightBridge's
        // doc says so), NOT Start/OnDestroy: a lamp that is switched off by disabling the component must
        // leave the four nearest-lamp slots, or it holds one against a light that is actually burning.
        private void OnEnable()
        {
            ApplyBeamState();
            WaterLightBridge.Register(this);
        }

        private void OnDisable() => WaterLightBridge.Unregister(this);

        /// <summary>
        /// <b>⭐ SHE WRITES THE ONE DECOR LAMP — the other half of "one publisher".</b>
        ///
        /// <para><see cref="WalkerLights.OwnsDecorLight"/> makes every <see cref="BoatSpotlight"/> in the
        /// scene STAND OFF the <c>_BoatLight*</c> singleton while her beam is lit. That half on its own is
        /// not a handover, it is a HOLE: with the boats standing off and nothing put in their place, the
        /// five globals simply KEEP whatever a boat wrote last — a beam frozen at a moored dory's bow, or
        /// zeros in a region with no boat in it — and the trees in front of her are lit by a ghost, or not
        /// at all. Standing the boats off is only correct because this line takes their place.
        /// </para>
        ///
        /// <para><b>Off the WATER state, deliberately.</b> The numbers are the ones
        /// <see cref="TryGetWaterLight"/> has already composed, published through
        /// <see cref="DecorLampGlobals"/>'s <c>WaterLightState</c> overload — so her beam cannot light a
        /// spruce unlike it lights the sea she is pointing it over, and it carries the same water-strength
        /// scaling a boat's searchlight carries into this same path (<c>BoatSpotlight</c> publishes its
        /// <c>waterIntensity</c> here, not its land intensity). One packing, one meaning, both lamps.</para>
        ///
        /// <para><b>⚠️ LateUpdate, not Update, and that is the ordering guarantee.</b> Every
        /// <see cref="BoatSpotlight"/> publishes in <c>Update</c>. Writing here makes her the LAST word in
        /// the frame whatever order the boats ticked in — so on the frame her beam is switched ON she
        /// cannot be overwritten by a boat that happened to run after her, and on the frame it is switched
        /// OFF the boats resume with no blank frame in between. The stand-off guard and this ordering are
        /// belt and braces on purpose: either one alone leaves a single-frame flicker in the woods, which
        /// is exactly the class of defect nothing would ever report.</para>
        /// </summary>
        private void LateUpdate() => PublishDecorLampIfOwned();

        /// <summary>
        /// Publish this beam as the lit-decor path's one lamp, IF she is the one who owns it this frame.
        /// Returns whether it published.
        ///
        /// <para>Split out of <c>LateUpdate</c> for one reason: EditMode runs no Unity messages, so a
        /// fixture that could not call this could only assert the globals by reaching through reflection
        /// at a private frame callback — and the defect this closes (the boats standing off with nothing
        /// put in their place) is invisible to every other kind of assertion. The rule is worth a seam.</para>
        /// </summary>
        public bool PublishDecorLampIfOwned()
        {
            if (!WalkerLights.OwnsDecorLight) return false;
            if (!TryGetWaterLight(out WaterLightState state)) return false;
            DecorLampGlobals.Publish(in state);
            return true;
        }

        private void ApplyBeamState()
        {
            if (Light != null) Light.enabled = _on;
        }

        /// <summary>
        /// The water's view of this beam: the same numbers the land quad burns at, so the sea and the
        /// ground can never disagree about where she is pointing or how hard the lamp is going.
        /// </summary>
        public bool TryGetWaterLight(out WaterLightState state)
        {
            SceneLight l = Light;
            if (l == null || !_on || !l.enabled || !l.isActiveAndEnabled)
            {
                state = default;
                return false;
            }

            float half = Mathf.Clamp(l.ConeHalfAngle, 0f, 89.5f);
            float inner = half * (1f - Mathf.Clamp01(l.AngularSoftness));
            Vector2 dir = transform.up;

            _waterLight = new WaterLightState
            {
                LampWorld = l.WorldOrigin,
                LampHeightMeters = l.LampHeightMeters,
                BeamDir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.up,
                Color = l.Color,
                Intensity = Mathf.Max(0f, l.Intensity) * Mathf.Max(0f, _waterStrength),
                Range = Mathf.Max(0.01f, l.ReachMetres),
                CosHalfAngle = LightMath.CosFromHalfAngleDeg(half),
                CosInnerAngle = LightMath.CosFromHalfAngleDeg(inner),
                EdgeSoftness = Mathf.Clamp01(l.EdgeSoftness),
                GateThreshold = l.GateThreshold,
                GateSoftness = l.GateSoftness,
                GateFallback = l.GateFallback,
            };
            state = _waterLight;
            return true;
        }

    }
}
