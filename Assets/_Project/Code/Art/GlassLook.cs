using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>Glowing glass</b>: the one look shared by every lit lamp and window, as data.
    ///
    /// <para>A lamp at night is three things, and each is drawn as a cheap layer over what is already
    /// there (ADR 0016's additive quad, the house emitter sheet, the boat pane mesh):</para>
    /// <list type="number">
    /// <item><b>The glass itself</b> (<see cref="Lens"/>): a white-hot centre, a body of the lamp's own
    /// colour round it, a soft rim, then a glow that falls off the way light does (an inverse-square tail windowed to
    /// the bloom) instead of a flat disc. Pixel-snapped and banded so it stays pixel art.</item>
    /// <item><b>The light on the water</b> (<see cref="Streak"/>): a short vertical streak in the lamp's
    /// colour under a lamp that stands over the sea, broken into rows that drift with the waves. It only
    /// draws on water (the published seabed below the published sea level) and never over a hull.</item>
    /// <item><b>The lit window</b> (<see cref="PaneLook"/>): a pane that is brighter low and in the middle
    /// (the lamp is on a table inside, not on the ceiling) and dimmer at its sides, a little uneven, with
    /// a dark frame and mullions, a warm colour that differs a little from window to window, and a slow
    /// oil-lamp breath on a boat's cabin. House windows take the same gradient off their baked emitter
    /// sheet.</item>
    /// </list>
    ///
    /// <para><b>Only how a lamp LOOKS.</b> Nothing here decides WHEN a lamp shows: the rule of the road
    /// (<see cref="BoatLamps.ShowsWhen"/>), the night gate and the 09-03 confinement ruling are untouched.
    /// The cabin's light still leaves only through its windows.</para>
    ///
    /// <para><b>Off is exactly the look before.</b> <see cref="Enabled"/> false stamps nothing new and
    /// publishes the house gradient as off, so every lamp draws as it did; that is also the rollback.</para>
    ///
    /// <para>A Resources profile for the reason <see cref="BoatLampLook"/> is one: Art's own tunables,
    /// zero scene wiring, built-in defaults when the asset is missing. Visual only (rule 5), no save.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Lighting/Glass Look", fileName = "GlassLook")]
    public sealed class GlassLook : ScriptableObject
    {
        /// <summary>Where the shipped asset lives, under any Resources folder.</summary>
        public const string ResourcePath = "GlassLook";

        /// <summary>The glass of one lamp family: core, rim and glow tail.</summary>
        [System.Serializable]
        public struct Lens
        {
            [Tooltip("The hot core's radius as a fraction of the light's bloom radius. 0 = no glass (the old look).")]
            [Range(0f, 1f)] public float CoreFraction;
            [Tooltip("Width of the coloured rim round the core, in art pixels.")]
            [Min(0f)] public float RimPixels;
            [Tooltip("How far the core is pulled to white on a white or warm lamp.")]
            [Range(0f, 1f)] public float Whiten;
            [Tooltip("The same pull on a COLOURED signal lamp (port, starboard). Kept low: a white core is not a red lamp.")]
            [Range(0f, 1f)] public float ColouredWhiten;
            [Tooltip("Brightness of the coloured rim, 0..1 of the core.")]
            [Range(0f, 1f)] public float RimLevel;
            [Tooltip("Brightness of the glow just outside the glass, 0..1 of the core.")]
            [Range(0f, 1f)] public float Tail;
            [Tooltip("How fast the glow falls off: the distance (fraction of the bloom radius) where it has halved.")]
            [Range(0.01f, 1f)] public float TailScale;
            [Tooltip("Flat bands the glow is cut into (0 = smooth).")]
            [Range(0, 8)] public int Bands;
            [Tooltip("Share of the core that is white hot, 0..1. The rest of the core is the coloured body. 1 = all of it hot.")]
            [Range(0f, 1f)] public float HotFraction;
            [Tooltip("Brightness of the coloured body round the hot centre, 0..1 of the centre.")]
            [Range(0f, 1f)] public float BodyLevel;
            [Tooltip("Share of the core's pull to white that the body keeps, 0..1. Low = the body shows the lamp's colour.")]
            [Range(0f, 1f)] public float BodyWhiten;
            [Tooltip("How far the rim fades across its width, 0..1. 0 = a flat ring, 1 = fades to nothing at its outer edge.")]
            [Range(0f, 1f)] public float RimFade;

            public bool IsOn => CoreFraction > 0f;
        }

        /// <summary>The broken reflection under one lamp family.</summary>
        [System.Serializable]
        public struct Streak
        {
            [Tooltip("Brightness of the streak as a share of its lamp. 0 = no streak (no quad, no draw).")]
            [Range(0f, 2f)] public float Strength;
            [Tooltip("How far down the screen the streak runs from the lamp, metres. The part over a hull is never drawn.")]
            [Min(0f)] public float LengthMetres;
            [Tooltip("Its width at the widest, metres.")]
            [Min(0f)] public float WidthMetres;
            [Tooltip("How far below its start (see HeightShare) the streak begins, metres.")]
            [Min(0f)] public float DropMetres;
            [Tooltip("Height of one wave row, art pixels.")]
            [Min(1f)] public float RowPixels;
            [Tooltip("Share of rows broken out by the waves at any moment, 0..1.")]
            [Range(0f, 1f)] public float Breakup;
            [Tooltip("How far a row sways sideways, as a share of the half width.")]
            [Range(0f, 1f)] public float Wobble;
            [Tooltip("How fast the rows re-break and sway (per second of game time).")]
            [Min(0f)] public float Speed;
            [Tooltip("Share of the lamp's height above the water the streak drops before it starts, 0..1. " +
                     "1 = at the drawn waterline under a lamp whose height is measured from the water.")]
            [Range(0f, 1f)] public float HeightShare;

            public bool IsOn => Strength > 0f && LengthMetres > 0f && WidthMetres > 0f;
        }

        /// <summary>A lit window pane: gradient, warm centre, frame and mullions.</summary>
        [System.Serializable]
        public struct PaneLook
        {
            [Tooltip("Brightness at the head of the pane, 0..1.")]
            [Range(0f, 1f)] public float HeadLevel;
            [Tooltip("Brightness at the sill, 0..1. Above the head: the lamp inside stands low.")]
            [Range(0f, 1.5f)] public float SillLevel;
            [Tooltip("Extra warmth in the middle of the pane, where the lamp is seen through the glass.")]
            [Range(0f, 1f)] public float CentreLift;
            [Tooltip("Flat bands the pane is cut into (0 = smooth).")]
            [Range(0, 8)] public int Bands;
            [Tooltip("Dark frame inside the pane's edge, art pixels (boat panes only; house windows carry their own).")]
            [Min(0f)] public float FramePixels;
            [Tooltip("Mullion bar width, art pixels (boat panes only). 0 = none, and boats ship 0: a cross bar " +
                     "is a house window's, not marine glass. Each boat pane is already its own light between " +
                     "the rig's posts; house windows carry their crosses in their own art.")]
            [Min(0f)] public float MullionPixels;
            [Tooltip("A pane gets a mullion across a side only when that side is at least this many art pixels.")]
            [Min(0f)] public float MullionMinPixels;
            [Tooltip("How far the cabin's oil lamp breathes, 0..1 of its brightness. Deterministic.")]
            [Range(0f, 0.5f)] public float Breath;
            [Tooltip("How fast it breathes.")]
            [Min(0f)] public float BreathSpeed;
            [Tooltip("House windows: how many texels the gradient looks along to find a pane's edges.")]
            [Range(1, 8)] public int ProbeTexels;
            [Tooltip("How much dimmer a pane is at its sides than in its middle, 0..1.")]
            [Range(0f, 1f)] public float AcrossFalloff;
            [Tooltip("Pixel-to-pixel unevenness of the glass, 0..0.5 of its brightness. Stable, not a flicker.")]
            [Range(0f, 0.5f)] public float Unevenness;
            [Tooltip("How far each window's warm colour drifts toward the second colour, 0..1. Each window gets its own fixed share.")]
            [Range(0f, 1f)] public float TintVariation;
            [Tooltip("The second warm interior colour a window may drift toward.")]
            public Color TintAlt;
        }

        [Tooltip("Master switch. Off = every lamp and window draws exactly as before this look existed.")]
        [SerializeField] private bool _enabled = true;
        [SerializeField] private float _pixelsPerMetre = 32f;

        [Header("Boat navigation lamps (sidelights, stern, masthead, anchor)")]
        [SerializeField] private Lens _navLens = new Lens
        {
            CoreFraction = 0.17f, RimPixels = 1f, Whiten = 0.85f, ColouredWhiten = 0.3f,
            RimLevel = 0.7f, Tail = 0.3f, TailScale = 0.17f, Bands = 3,
            HotFraction = 0.5f, BodyLevel = 0.92f, BodyWhiten = 0.2f, RimFade = 0f,
        };
        [SerializeField] private Streak _navStreak = new Streak
        {
            Strength = 0.42f, LengthMetres = 3.6f, WidthMetres = 0.34f, DropMetres = 0.15f,
            RowPixels = 2f, Breakup = 0.4f, Wobble = 0.35f, Speed = 0f, HeightShare = 0.45f,
        };

        [Header("Wharf and street lamps, lanterns")]
        [SerializeField] private Lens _shoreLens = new Lens
        {
            CoreFraction = 0.22f, RimPixels = 1f, Whiten = 0.8f, ColouredWhiten = 0.3f,
            RimLevel = 0.65f, Tail = 0.28f, TailScale = 0.18f, Bands = 3,
            HotFraction = 0.5f, BodyLevel = 0.9f, BodyWhiten = 0.2f, RimFade = 0f,
        };
        [SerializeField] private Streak _shoreStreak = new Streak
        {
            Strength = 0f, LengthMetres = 3.2f, WidthMetres = 0.4f, DropMetres = 0.6f,
            RowPixels = 2f, Breakup = 0.42f, Wobble = 0.35f, Speed = 0f, HeightShare = 1f,
        };

        [Header("Lit windows (boat cabins and houses)")]
        [SerializeField] private PaneLook _panes = new PaneLook
        {
            HeadLevel = 0.62f, SillLevel = 1f, CentreLift = 0.22f, Bands = 3,
            FramePixels = 1f, MullionPixels = 0f, MullionMinPixels = 7f,
            Breath = 0f, BreathSpeed = 0.7f, ProbeTexels = 6,
            AcrossFalloff = 0.25f, Unevenness = 0.06f, TintVariation = 0.5f,
            TintAlt = new Color(1f, 0.7f, 0.4f, 1f),
        };
        [Tooltip("A house window glow stops painting a disc over the wall; its light lands on the ground as a pool.")]
        [SerializeField] private bool _windowSpillIsPool = true;

        [Header("Occlusion")]
        [Tooltip("Lamps, their streaks on the water, a cabin's wash on deck and its lit panes sort AMONG the " +
                 "world: a hull, shed, wall, piling or figure nearer the camera draws over them as it would " +
                 "over the lamp itself. Off = drawn over everything, as before.")]
        [SerializeField] private bool _sortInWorld = true;

        public bool Enabled => _enabled;
        /// <summary>Do lamps sort among the world (see <see cref="SceneLight.SetWorldSort"/>)?</summary>
        public bool SortsInWorld => _enabled && _sortInWorld;
        public float PixelsPerMetre => _pixelsPerMetre;
        public Lens NavLens => _navLens;
        public Streak NavStreak => _navStreak;
        public Lens ShoreLens => _shoreLens;
        public Streak ShoreStreak => _shoreStreak;
        public PaneLook Panes => _panes;
        public bool WindowSpillIsPool => _windowSpillIsPool;

        // -------------------------------------------------------------------------------------------
        //  the profile in force
        // -------------------------------------------------------------------------------------------

        private static GlassLook s_Current;

        /// <summary>The look in force: the shipped <c>Resources/GlassLook.asset</c>, else the built-in
        /// defaults. Settable so a test or a plate can try another look; setting it republishes the
        /// house-window globals at once.</summary>
        public static GlassLook Current
        {
            get
            {
                if (s_Current != null) return s_Current;
                s_Current = Resources.Load<GlassLook>(ResourcePath);
                if (s_Current == null) s_Current = CreateDefault();
                return s_Current;
            }
            set
            {
                s_Current = value;
                Publish();
            }
        }

        /// <summary>A fresh look holding the built-in defaults (what the shipped asset holds).</summary>
        public static GlassLook CreateDefault()
        {
            var look = CreateInstance<GlassLook>();
            look.name = "GlassLook (defaults)";
            look.hideFlags = HideFlags.DontSave;
            return look;
        }

        // -------------------------------------------------------------------------------------------
        //  stamping
        // -------------------------------------------------------------------------------------------

        /// <summary>
        /// Glass and streak for one boat navigation lamp, written AFTER its preset and lamp look (a
        /// stamp clears both). Signal lamps keep their colour: their core whitens by
        /// <see cref="Lens.ColouredWhiten"/> only.
        /// </summary>
        public void ApplyNav(SceneLight light, HullLampKind kind)
        {
            if (light == null || !_enabled) return;
            bool coloured = kind == HullLampKind.PortSidelight || kind == HullLampKind.StarboardSidelight;
            light.SetGlass(_navLens, coloured, _pixelsPerMetre);
            light.SetStreak(_navStreak, _pixelsPerMetre);
            light.SetWorldSort(_sortInWorld);
        }

        /// <summary>
        /// Glass and streak for a placed shore light, written after its preset. A window glow is not a
        /// lamp: it gets no glass, and (with <see cref="WindowSpillIsPool"/>) stops painting its disc so
        /// only its pool on the ground remains. The worn headlamp is not part of this look.
        /// </summary>
        public void ApplyShore(SceneLight light, LightPresets.Kind kind)
        {
            if (light == null || !_enabled) return;
            switch (kind)
            {
                case LightPresets.Kind.Headlamp:
                    return;
                case LightPresets.Kind.WindowGlow:
                    if (_windowSpillIsPool) light.DrawsBloom = false;
                    return;
                default:
                    light.SetGlass(_shoreLens, coloured: false, _pixelsPerMetre);
                    light.SetStreak(_shoreStreak, _pixelsPerMetre);
                    light.SetWorldSort(_sortInWorld);
                    return;
            }
        }

        /// <summary>The cabin's oil-lamp breath now, as a brightness multiplier (1 = steady). A pure
        /// function of seed and game time (rule 5), so a plate shot twice is the same plate.</summary>
        public float CabinBreath(int seed, double seconds) =>
            _enabled ? LightMath.Flicker(seed, (float)seconds, _panes.Breath, _panes.BreathSpeed) : 1f;

        // -------------------------------------------------------------------------------------------
        //  the house windows' globals
        // -------------------------------------------------------------------------------------------

        static readonly int IdWindowGlassA = Shader.PropertyToID("_HHWindowGlassA");
        static readonly int IdWindowGlassB = Shader.PropertyToID("_HHWindowGlassB");
        static readonly int IdWindowGlassC = Shader.PropertyToID("_HHWindowGlassC");
        static readonly int IdWindowGlassTint = Shader.PropertyToID("_HHWindowGlassTint");

        /// <summary>
        /// Publish the house-window gradient to the lit-sprite shader: <c>_HHWindowGlassA</c> = (on,
        /// head level, sill level, centre lift), <c>_HHWindowGlassB</c> = (probe texels, bands),
        /// <c>_HHWindowGlassC</c> = (across falloff, unevenness, tint variation) and
        /// <c>_HHWindowGlassTint</c> = the second warm colour. Off (x 0) leaves every house window exactly
        /// as its bake draws it.
        /// </summary>
        public static void Publish()
        {
            GlassLook look = Current;
            PaneLook p = look._panes;
            Shader.SetGlobalVector(IdWindowGlassA, look._enabled
                ? new Vector4(1f, p.HeadLevel, p.SillLevel, p.CentreLift)
                : Vector4.zero);
            Shader.SetGlobalVector(IdWindowGlassB, new Vector4(Mathf.Max(1, p.ProbeTexels), Mathf.Max(0, p.Bands), 0f, 0f));
            Shader.SetGlobalVector(IdWindowGlassC, new Vector4(p.AcrossFalloff, p.Unevenness, p.TintVariation, 0f));
            Shader.SetGlobalColor(IdWindowGlassTint, p.TintAlt);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PublishAtStart() => Publish();

        private void OnValidate()
        {
            if (ReferenceEquals(this, s_Current)) Publish();
        }
    }
}
