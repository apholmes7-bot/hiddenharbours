using System;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// How ONE navigation lamp is drawn: a small hard hot core, and a faint banded halo round it.
    /// Plain data — <see cref="BoatLampLook"/> holds one per lamp kind.
    /// </summary>
    [Serializable]
    public struct NavLampLook
    {
        [Tooltip("The lamp's colour. Rule of the road: red to port, green to starboard, white astern " +
                 "and at the masthead. Keep it saturated: this is added over a dark frame.")]
        public Color Color;

        [Tooltip("How bright the core burns (before the hull's own trim and the night gate).")]
        [Min(0f)] public float Intensity;

        [Tooltip("Outer radius of the faint halo, metres. This is the whole size of the lamp on screen.")]
        [Min(0.01f)] public float HaloRadiusMetres;

        [Tooltip("Radius of the hard hot core, metres. A few art pixels. Must be smaller than the halo.")]
        [Min(0f)] public float CoreRadiusMetres;

        [Tooltip("How bright the halo is beside the core (core = 1). Small = a lamp, large = a ball.")]
        [Range(0f, 1f)] public float HaloStrength;

        [Tooltip("How many flat bands the halo is cut into. 0 = a smooth gradient (the old orb).")]
        [Range(0, SceneLight.MaxBandSteps)] public int BandSteps;

        [Tooltip("How far the core is pulled toward white (0 = the lamp's own colour, 1 = white). Keep " +
                 "it low on the sidelights or red and green stop reading.")]
        [Range(0f, 1f)] public float CoreWhiten;

        [Tooltip("Halo softness when Band Steps is 0 (0 hard disc .. 1 soft). Unused while banded.")]
        [Range(0f, 1f)] public float EdgeSoftness;

        public NavLampLook(Color color, float intensity, float haloRadiusMetres, float coreRadiusMetres,
                           float haloStrength, int bandSteps, float coreWhiten, float edgeSoftness)
        {
            Color = color;
            Intensity = intensity;
            HaloRadiusMetres = haloRadiusMetres;
            CoreRadiusMetres = coreRadiusMetres;
            HaloStrength = haloStrength;
            BandSteps = bandSteps;
            CoreWhiten = coreWhiten;
            EdgeSoftness = edgeSoftness;
        }

        /// <summary>The core radius actually drawn: never bigger than the halo it sits in.</summary>
        public float SafeCoreRadiusMetres => Mathf.Clamp(CoreRadiusMetres, 0f, Mathf.Max(HaloRadiusMetres, 0.01f));
    }

    /// <summary>
    /// <b>The look of the lamps a boat carries, as DATA</b> (rule 6) — the numbers that used to be C#
    /// constants in <see cref="BoatLampPresets"/>. The owner tunes
    /// <c>Resources/BoatLampLook.asset</c>; no code changes.
    ///
    /// <para><b>Why a Resources profile and not GameConfig or a Def.</b> It is the Art module's own
    /// pattern for lighting tunables: <see cref="LampShadowProfile"/> is exactly this — one
    /// ScriptableObject in Resources, loaded with zero scene wiring, with the shipped defaults built
    /// in so a scene with no asset still draws the shipped look. It is one fleet-wide look, not a
    /// catalogue of content entities, so it carries no Def id; per-hull data (where each lamp sits,
    /// its trim) stays in <c>HullLamp</c>.</para>
    ///
    /// <para><b>The field defaults ARE the shipped look</b>, and a test pins the asset against them,
    /// so the two cannot drift. Visual only: drives no sim, saves nothing (rule 5).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Lighting/Boat Lamp Look", fileName = "BoatLampLook")]
    public sealed class BoatLampLook : ScriptableObject
    {
        /// <summary>Where the shipped asset lives, under any Resources folder.</summary>
        public const string ResourcePath = "BoatLampLook";

        // The rule-of-the-road colours. Saturated on purpose: an additive glow over a crushed-dark
        // frame adds up, and a washed-out pink adds up to white and stops being a signal.
        static readonly Color PortRed      = new Color(1f, 0.10f, 0.09f, 1f);
        static readonly Color StarboardGrn = new Color(0.10f, 1f, 0.34f, 1f);
        static readonly Color LampWhite    = new Color(1f, 0.96f, 0.88f, 1f);

        [Header("Navigation lamps: a small hot core and a faint banded halo")]
        [Tooltip("Port sidelight (red). The halo must stay under half the gap between the pair on the " +
                 "tightest hull (the cape, 0.60 m apart) or red and green mix to yellow.")]
        [SerializeField] private NavLampLook _portSidelight =
            new NavLampLook(PortRed, 1.4f, 0.1875f, 0.0625f, 0.22f, 2, 0.2f, 0.4f);

        [Tooltip("Starboard sidelight (green). Same size as the port one.")]
        [SerializeField] private NavLampLook _starboardSidelight =
            new NavLampLook(StarboardGrn, 1.4f, 0.1875f, 0.0625f, 0.22f, 2, 0.2f, 0.4f);

        [Tooltip("Stern light (white).")]
        [SerializeField] private NavLampLook _sternLight =
            new NavLampLook(LampWhite, 1.35f, 0.25f, 0.0625f, 0.2f, 2, 0.6f, 0.45f);

        [Tooltip("Masthead (white), and the range light, which is the same lamp. The biggest and " +
                 "brightest of the round lamps.")]
        [SerializeField] private NavLampLook _masthead =
            new NavLampLook(LampWhite, 1.6f, 0.3125f, 0.09375f, 0.22f, 3, 0.6f, 0.45f);

        [Tooltip("Anchor light (white). Dimmer and smaller than the masthead: 'something is here'.")]
        [SerializeField] private NavLampLook _anchorLight =
            new NavLampLook(LampWhite, 1.0f, 0.21875f, 0.0625f, 0.18f, 2, 0.6f, 0.45f);

        [Tooltip("Snap the lamps to the art pixel grid, so the core and bands are whole pixels.")]
        [SerializeField] private bool _snapToPixelGrid = true;

        [Tooltip("Art pixels per metre the snap uses (match the project's sprite PPU).")]
        [Min(1f)] [SerializeField] private float _pixelsPerMetre = 32f;

        [Header("Cabin light: what leaves a glazed wall")]
        [Tooltip("ON = the cabin's light LANDS ON THE DECK: each glazed wall lights a pool under it " +
                 "(it brightens what is already there). OFF = no pool.")]
        [SerializeField] private bool _spillPoolsTheDeck = true;

        [Tooltip("ON = also paint the old orange wedge over the frame. OFF (shipped) = no wedge. This " +
                 "is the way back to the look before 2026-11-13.")]
        [SerializeField] private bool _spillPaintsWedge;

        [Tooltip("How strongly a glazed wall lights the deck (before the occupancy boost).")]
        [Min(0f)] [SerializeField] private float _spillIntensity = 0.5f;

        [Tooltip("Shortest reach a glazed wall gets, metres (one small porthole).")]
        [Min(0.01f)] [SerializeField] private float _minWallSpillMetres = 0.5f;

        [Tooltip("Longest reach a glazed wall gets, metres (a backstop).")]
        [Min(0.01f)] [SerializeField] private float _maxWallSpillMetres = 1.6f;

        [Tooltip("Reach as a multiple of ONE window's width. Off a window, never off the whole wall.")]
        [Min(0f)] [SerializeField] private float _wallSpillWindowMultiple = 1.4f;

        [Tooltip("Half-angle of the wedge the wall lights, degrees.")]
        [Range(1f, 89f)] [SerializeField] private float _wallSpillHalfAngleDeg = 45f;

        [Tooltip("How soft the wedge's sides are, as a fraction of the half-angle (painted wedge only).")]
        [Range(0f, 1f)] [SerializeField] private float _wallSpillAngularSoftness = 0.85f;

        [Tooltip("How soft the wedge's far edge is (painted wedge only).")]
        [Range(0f, 1f)] [SerializeField] private float _wallSpillEdgeSoftness = 0.9f;

        [Tooltip("How far BELOW the window sill the pool starts, in window heights. The pool is laid " +
                 "at the foot of the wall, not up at the glass.")]
        [Min(0f)] [SerializeField] private float _spillFootBelowSillPaneHeights = 1f;

        /// <summary>The look of one navigation lamp kind. Range light = masthead; anything that is not
        /// a navigation lamp answers the stern light, the same safe landing the presets always had.</summary>
        public NavLampLook For(HullLampKind kind)
        {
            switch (kind)
            {
                case HullLampKind.PortSidelight:      return _portSidelight;
                case HullLampKind.StarboardSidelight: return _starboardSidelight;
                case HullLampKind.Masthead:
                case HullLampKind.RangeLight:         return _masthead;
                case HullLampKind.AnchorLight:        return _anchorLight;
                default:                              return _sternLight;
            }
        }

        /// <summary>Art pixels per metre the lamps snap to, or 0 when the snap is off.</summary>
        public float SnapPixelsPerMetre => _snapToPixelGrid ? Mathf.Max(1f, _pixelsPerMetre) : 0f;

        public bool SpillPoolsTheDeck => _spillPoolsTheDeck;
        public bool SpillPaintsWedge => _spillPaintsWedge;
        public float SpillIntensity => Mathf.Max(0f, _spillIntensity);
        public float MinWallSpillMetres => Mathf.Max(0.01f, _minWallSpillMetres);
        public float MaxWallSpillMetres => Mathf.Max(MinWallSpillMetres, _maxWallSpillMetres);
        public float WallSpillWindowMultiple => Mathf.Max(0f, _wallSpillWindowMultiple);
        public float WallSpillHalfAngleDeg => Mathf.Clamp(_wallSpillHalfAngleDeg, 1f, 89f);
        public float WallSpillAngularSoftness => Mathf.Clamp01(_wallSpillAngularSoftness);
        public float WallSpillEdgeSoftness => Mathf.Clamp01(_wallSpillEdgeSoftness);
        public float SpillFootBelowSillPaneHeights => Mathf.Max(0f, _spillFootBelowSillPaneHeights);

        // ---- the one in force ------------------------------------------------------------------

        private static BoatLampLook s_Current;

        /// <summary>
        /// The look in force: the shipped <c>Resources/BoatLampLook.asset</c>, else the built-in
        /// defaults. Loaded once and cached, so reading it every frame costs a null check. Set it to
        /// override (a test, a fixture); set it to null to go back to the shipped asset.
        /// </summary>
        public static BoatLampLook Current
        {
            get
            {
                if (s_Current != null) return s_Current;
                s_Current = Resources.Load<BoatLampLook>(ResourcePath);
                if (s_Current == null) s_Current = CreateDefault();
                return s_Current;
            }
            set => s_Current = value;
        }

        /// <summary>The shipped defaults as an in-memory look (no asset on disk).</summary>
        public static BoatLampLook CreateDefault()
        {
            var look = CreateInstance<BoatLampLook>();
            look.name = "BoatLampLook (built-in default)";
            look.hideFlags = HideFlags.HideAndDontSave;
            return look;
        }
    }
}
