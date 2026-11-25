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
    /// <item><b>The glass itself</b> (<see cref="Lens"/>): a near-white hot core, a ring of the lamp's own
    /// colour round it, then a glow that falls off the way light does (an inverse-square tail windowed to
    /// the bloom) instead of a flat disc. Pixel-snapped and banded so it stays pixel art.</item>
    /// <item><b>The light on the water</b> (<see cref="Streak"/>): a short vertical streak in the lamp's
    /// colour under a lamp that stands over the sea, broken into rows that drift with the waves. It only
    /// draws on water (the published seabed below the published sea level) and never over a hull.</item>
    /// <item><b>The lit window</b> (<see cref="PaneLook"/>): a pane that is brighter low and in the middle
    /// (the lamp is on a table inside, not on the ceiling), with a dark frame and mullions, and a slow
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
            [Tooltip("Mullion bar width, art pixels (boat panes only). 0 = none.")]
            [Min(0f)] public float MullionPixels;
            [Tooltip("A pane gets a mullion across a side only when that side is at least this many art pixels.")]
            [Min(0f)] public float MullionMinPixels;
            [Tooltip("How far the cabin's oil lamp breathes, 0..1 of its brightness. Deterministic.")]
            [Range(0f, 0.5f)] public float Breath;
            [Tooltip("How fast it breathes.")]
            [Min(0f)] public float BreathSpeed;
            [Tooltip("House windows: how many texels the gradient looks along to find a pane's edges.")]
            [Range(1, 8)] public int ProbeTexels;
        }

        [Tooltip("Master switch. Off = every lamp and window draws exactly as before this look existed.")]
        [SerializeField] private bool _enabled = true;
        [SerializeField] private float _pixelsPerMetre = 32f;

        [Header("Boat navigation lamps (sidelights, stern, masthead, anchor)")]
        [SerializeField] private Lens _navLens = new Lens
        {
            CoreFraction = 0.17f, RimPixels = 1f, Whiten = 0.85f, ColouredWhiten = 0.3f,
            RimLevel = 1f, Tail = 0.32f, TailScale = 0.22f, Bands = 3,
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
            RimLevel = 0.95f, Tail = 0.3f, TailScale = 0.24f, Bands = 3,
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
            FramePixels = 0f, MullionPixels = 1f, MullionMinPixels = 7f,
            Breath = 0f, BreathSpeed = 0.7f, ProbeTexels = 6,
        };
        [Tooltip("A house window glow stops painting a disc over the wall; its light lands on the ground as a pool.")]
        [SerializeField] private bool _windowSpillIsPool = true;

        public bool Enabled => _enabled;
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

        /// <summary>
        /// Publish the house-window gradient to the lit-sprite shader: <c>_HHWindowGlassA</c> = (on,
        /// head level, sill level, centre lift), <c>_HHWindowGlassB</c> = (probe texels, bands). Off (x 0)
        /// leaves every house window exactly as its bake draws it.
        /// </summary>
        public static void Publish()
        {
            GlassLook look = Current;
            PaneLook p = look._panes;
            Shader.SetGlobalVector(IdWindowGlassA, look._enabled
                ? new Vector4(1f, p.HeadLevel, p.SillLevel, p.CentreLift)
                : Vector4.zero);
            Shader.SetGlobalVector(IdWindowGlassB, new Vector4(Mathf.Max(1, p.ProbeTexels), Mathf.Max(0, p.Bands), 0f, 0f));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PublishAtStart() => Publish();

        private void OnValidate()
        {
            if (ReferenceEquals(this, s_Current)) Publish();
        }
    }
}
