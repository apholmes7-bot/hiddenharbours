using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Every tunable of the SEA-MIST / shore-spray effect, in one serializable struct so the math stays free
    /// of magic numbers (CLAUDE.md rule 6). <see cref="SeaMistEmitter"/> serializes an owner-editable instance.
    /// Defaults are a subtle, drifting ambient haze that thickens with fog and sea-state.
    /// </summary>
    [System.Serializable]
    public struct SeaMistConfig
    {
        [Header("Pool & area")]
        [Tooltip("Max live mist wisps (the pool is fixed and recycled — zero per-frame allocation).")]
        [Min(1)] public int MaxWisps;
        [Tooltip("Half-size (m) of the area, centred on the camera, mist drifts within (x = across, y = depth).")]
        public Vector2 AreaHalfSize;

        [Header("Density & spawn")]
        [Tooltip("Baseline mist intensity on a clear, glassy day (0..1) — the faint ambient shimmer that's always there.")]
        [Range(0f, 1f)] public float BaselineIntensity;
        [Tooltip("How much LOW visibility (fog) thickens the mist (added to baseline).")]
        [Range(0f, 2f)] public float FogWeight;
        [Tooltip("How much HIGHER sea-state (spray kicking up) thickens the mist (added to baseline).")]
        [Range(0f, 2f)] public float SeaStateWeight;

        [Header("Drift & look")]
        [Tooltip("Metres of downwind drift per unit of the shared 0..1 scene wind — how strongly mist rides the breeze (cohesion with grass/water).")]
        public float WindResponse;
        [Tooltip("A slow own-drift (m/s) added so the mist creeps even in dead calm.")]
        public Vector2 BaseDrift;
        [Tooltip("Seconds a wisp lives before it has fully dissolved.")]
        [Min(0.1f)] public float Lifetime;
        [Tooltip("Fraction of life spent fading IN (0..1).")]
        [Range(0f, 1f)] public float FadeIn;
        [Tooltip("Fraction of life spent fading OUT (0..1).")]
        [Range(0f, 1f)] public float FadeOut;
        [Tooltip("Wisp size at birth (m).")]
        [Min(0.01f)] public float Size;
        [Tooltip("± deterministic variation in per-wisp size (0..1 fraction).")]
        [Range(0f, 1f)] public float SizeJitter;
        [Tooltip("Mist tint. A soft cool white reads as haze over the water; alpha is driven by life + intensity + day/night.")]
        public Color Color;
        [Tooltip("Peak opacity at full intensity (0..1) before the life envelope + day/night scale it. Keep low — mist is subtle.")]
        [Range(0f, 1f)] public float MaxAlpha;

        [Header("Day / night")]
        [Tooltip("How strongly night dims the mist (0 = ignore time of day, 1 = tracks the light exactly).")]
        [Range(0f, 1f)] public float NightFade;
        [Tooltip("How faintly the mist catches MOONLIGHT at night so it never blacks out entirely (0..1 floor).")]
        [Range(0f, 1f)] public float MoonlightCatch;

        public static SeaMistConfig Default => new SeaMistConfig
        {
            MaxWisps          = 18,
            AreaHalfSize      = new Vector2(16f, 10f),
            BaselineIntensity = 0.18f,
            FogWeight         = 0.9f,
            SeaStateWeight    = 0.6f,
            WindResponse      = 1.6f,
            BaseDrift         = new Vector2(0.05f, 0.02f),
            Lifetime          = 7f,
            FadeIn            = 0.35f,
            FadeOut           = 0.45f,
            Size              = 3.2f,
            SizeJitter        = 0.4f,
            Color             = new Color(0.86f, 0.92f, 0.97f, 1f),
            MaxAlpha          = 0.22f,
            NightFade         = 0.55f,
            MoonlightCatch    = 0.12f,
        };
    }

    /// <summary>
    /// Every tunable of the CHIMNEY-SMOKE column (rule 6). <see cref="ChimneySmoke"/> serializes an
    /// owner-editable instance. Defaults are a thin, cosy hearth plume that bends downwind.
    /// </summary>
    [System.Serializable]
    public struct ChimneySmokeConfig
    {
        [Header("Pool & emission")]
        [Tooltip("Max live smoke puffs (fixed, recycled pool — zero per-frame allocation).")]
        [Min(1)] public int MaxPuffs;
        [Tooltip("Puffs emitted per second — a thin steady wisp wants only a few.")]
        [Min(0.1f)] public float EmitPerSecond;
        [Tooltip("Seconds a puff lives (how high/far the column reaches before dissolving).")]
        [Min(0.1f)] public float Lifetime;

        [Header("Rise & bend")]
        [Tooltip("How fast a puff rises (m/s up the screen).")]
        public float RiseSpeed;
        [Tooltip("Metres of downwind bend per unit of the shared 0..1 scene wind — how far the plume leans (cohesion with grass/water).")]
        public float WindResponse;
        [Tooltip("Gentle per-puff sideways sway amplitude (m) so the column breathes, not a rigid arc.")]
        public float SwayAmp;

        [Header("Look")]
        [Tooltip("Fraction of life spent fading IN (0..1) — puffs build at the flue.")]
        [Range(0f, 1f)] public float FadeIn;
        [Tooltip("Fraction of life spent fading OUT (0..1) — puffs thin out aloft.")]
        [Range(0f, 1f)] public float FadeOut;
        [Tooltip("Puff size at birth (m).")]
        [Min(0.01f)] public float StartSize;
        [Tooltip("How much a puff grows over its life (1 = none, 2 = doubles) — smoke spreads as it cools.")]
        [Min(1f)] public float Spread;
        [Tooltip("± deterministic variation in per-puff size (0..1 fraction).")]
        [Range(0f, 1f)] public float SizeJitter;
        [Tooltip("Smoke tint. A warm-grey reads as a hearth plume; alpha is driven by life + day/night.")]
        public Color Color;
        [Tooltip("Peak opacity at birth (0..1) before the life envelope + day/night scale it.")]
        [Range(0f, 1f)] public float MaxAlpha;

        [Header("Day / night")]
        [Tooltip("How strongly night dims the smoke (0 = ignore time of day). Smoke usually only dims a little — the hearth burns at night too.")]
        [Range(0f, 1f)] public float NightFade;

        public static ChimneySmokeConfig Default => new ChimneySmokeConfig
        {
            MaxPuffs      = 14,
            EmitPerSecond = 3.5f,
            Lifetime      = 3.5f,
            RiseSpeed     = 1.3f,
            WindResponse  = 1.1f,
            SwayAmp       = 0.12f,
            FadeIn        = 0.2f,
            FadeOut       = 0.5f,
            StartSize     = 0.55f,
            Spread        = 2.6f,
            SizeJitter    = 0.3f,
            Color         = new Color(0.72f, 0.71f, 0.69f, 1f),
            MaxAlpha      = 0.5f,
            NightFade     = 0.35f,
        };
    }

    /// <summary>
    /// Every tunable of the GULL flock (rule 6). <see cref="GullFlock"/> serializes an owner-editable
    /// instance. Defaults are a few birds wheeling occasionally over the harbour.
    /// </summary>
    [System.Serializable]
    public struct GullConfig
    {
        [Header("Flock")]
        [Tooltip("How many gulls wheel at once. A FEW reads as a living coast; a sky full reads as a swarm.")]
        [Min(0)] public int Count;
        [Tooltip("Half-size (m) of the region, centred on the camera, the gull loops fill (x across, y depth).")]
        public Vector2 AreaHalfSize;
        [Tooltip("Min/max loop radius (m) — bigger birds take wider, lazier wheels.")]
        public Vector2 RadiusRange;
        [Tooltip("Min/max loop period (s) — how long one full wheel takes (slower = lazier).")]
        public Vector2 PeriodRange;

        [Header("Cadence")]
        [Tooltip("Fraction of the time a given gull is actually ON-SCREEN/flying vs resting off-loop (0..1). Lower = occasional birds, not constant.")]
        [Range(0f, 1f)] public float ActiveFraction;

        [Header("Look")]
        [Tooltip("Gull sprite size (m).")]
        [Min(0.01f)] public float Size;
        [Tooltip("Metres the whole loop is skewed downwind per unit of the shared 0..1 wind (rides the breeze).")]
        public float WindDrift;
        [Tooltip("Gull tint. A pale grey-white reads against sky/sea; alpha driven by day/night + the appear/vanish fade.")]
        public Color Color;
        [Tooltip("Peak opacity (0..1) before day/night + fade scale it.")]
        [Range(0f, 1f)] public float MaxAlpha;

        [Header("Day / night")]
        [Tooltip("How strongly night dims the gulls (0 = ignore). Gulls roost at night, so a high fade reads true.")]
        [Range(0f, 1f)] public float NightFade;

        public static GullConfig Default => new GullConfig
        {
            Count          = 4,
            AreaHalfSize   = new Vector2(14f, 9f),
            RadiusRange    = new Vector2(3.5f, 7f),
            PeriodRange    = new Vector2(9f, 16f),
            ActiveFraction = 0.6f,
            Size           = 0.7f,
            WindDrift      = 1.0f,
            Color          = new Color(0.93f, 0.95f, 0.97f, 1f),
            MaxAlpha       = 0.9f,
            NightFade      = 0.8f,
        };
    }

    /// <summary>
    /// Every tunable of the DUST-MOTE / pollen effect (rule 6). <see cref="DustMotes"/> serializes an
    /// owner-editable instance. Defaults are tiny specks drifting by day that fade away after dark.
    /// </summary>
    [System.Serializable]
    public struct DustMoteConfig
    {
        [Header("Pool & area")]
        [Tooltip("Max live motes (fixed, recycled pool).")]
        [Min(1)] public int MaxMotes;
        [Tooltip("Half-size (m) of the area, centred on the camera, motes fill.")]
        public Vector2 AreaHalfSize;

        [Header("Drift & life")]
        [Tooltip("Metres of drift per unit of the shared 0..1 scene wind — motes carry on the breeze.")]
        public float WindResponse;
        [Tooltip("A slow own-drift (m/s).")]
        public Vector2 BaseDrift;
        [Tooltip("Bob (rise/fall) amplitude (m) so motes hang and shimmer rather than falling.")]
        public float BobAmp;
        [Tooltip("How fast a mote bobs (rad/s).")]
        public float BobSpeed;
        [Tooltip("Seconds a mote lives.")]
        [Min(0.1f)] public float Lifetime;
        [Tooltip("Fraction of life fading IN (0..1).")]
        [Range(0f, 1f)] public float FadeIn;
        [Tooltip("Fraction of life fading OUT (0..1).")]
        [Range(0f, 1f)] public float FadeOut;

        [Header("Look")]
        [Tooltip("Mote size (m) — keep tiny.")]
        [Min(0.005f)] public float Size;
        [Tooltip("± deterministic size variation (0..1 fraction).")]
        [Range(0f, 1f)] public float SizeJitter;
        [Tooltip("Mote tint. A warm pale fleck catches the sun.")]
        public Color Color;
        [Tooltip("Peak opacity (0..1) before life + day/night scale it. Keep low — motes are barely there.")]
        [Range(0f, 1f)] public float MaxAlpha;

        [Header("Day / night")]
        [Tooltip("How strongly night kills the motes (1 = gone in full dark — sunbeam motes only show by day).")]
        [Range(0f, 1f)] public float NightFade;

        public static DustMoteConfig Default => new DustMoteConfig
        {
            MaxMotes     = 24,
            AreaHalfSize = new Vector2(10f, 7f),
            WindResponse = 0.8f,
            BaseDrift    = new Vector2(0.03f, 0.0f),
            BobAmp       = 0.25f,
            BobSpeed     = 0.6f,
            Lifetime     = 9f,
            FadeIn       = 0.25f,
            FadeOut      = 0.3f,
            Size         = 0.09f,
            SizeJitter   = 0.5f,
            Color        = new Color(1f, 0.97f, 0.85f, 1f),
            MaxAlpha     = 0.35f,
            NightFade    = 1f,
        };
    }

    /// <summary>
    /// Every tunable of the FALLING-RAIN effect (rule 6) — short vertical streaks that fall over the sea when
    /// the deterministic weather drives up the chop AND drops the visibility (a squall). <see cref="RainEmitter"/>
    /// serializes an owner-editable instance. There is NO precipitation signal in the sim, so rain is DERIVED
    /// art-only from sea-state + visibility via <see cref="AmbientParticleMath.RainIntensity"/> (no sim/save
    /// change). Defaults ship the feature OFF (<see cref="BaselineIntensity"/> 0, and the rain only appears once
    /// chop + murk cross in) so it never rains on a calm clear day until the owner dials it in.
    /// </summary>
    [System.Serializable]
    public struct RainConfig
    {
        [Header("Pool & area")]
        [Tooltip("Max live rain drops (the pool is fixed and recycled — zero per-frame allocation). Rule 7 budget cap.")]
        [Min(1)] public int MaxDrops;
        [Tooltip("Half-size (m) of the area, centred on the camera, rain falls within (x = across, y = up/down).")]
        public Vector2 AreaHalfSize;

        [Header("Intensity (derived — no forecast in the sim)")]
        [Tooltip("Baseline rain on a clear, glassy day (0..1). DEFAULT 0 = feature OFF until you dial it in.")]
        [Range(0f, 1f)] public float BaselineIntensity;
        [Tooltip("How much HIGHER sea-state (the wind is up, chop building) drives the rain — the main knob.")]
        [Range(0f, 2f)] public float SeaStateWeight;
        [Tooltip("The VISIBILITY gate (0..1): how much the rain needs the light to go MURKY. 1 = must fog up " +
                 "before it rains (a clear blustery day stays dry); 0 = rain purely on chop, no gate.")]
        [Range(0f, 1f)] public float FogWeight;

        [Header("Fall & slant")]
        [Tooltip("How fast a drop falls (m/s DOWN the screen). Rain is fast — this is the streak's speed.")]
        [Min(0.1f)] public float FallSpeed;
        [Tooltip("Metres of sideways SLANT per m/s of the REAL sim wind — so a gale visibly slants the rain " +
                 "(reads the sim WindVector directly for m/s, not the normalised 0..1 shader wind).")]
        public float WindResponse;
        [Tooltip("Length (m) of a rain streak — the vertical extent of the drop sprite before slant stretches it.")]
        [Min(0.01f)] public float Length;

        [Header("Look")]
        [Tooltip("Rain streak width scale (m) — keep thin; the sprite is a short vertical line.")]
        [Min(0.01f)] public float Size;
        [Tooltip("Seconds a drop lives before it is recycled (roughly how long it takes to cross the field).")]
        [Min(0.1f)] public float Lifetime;
        [Tooltip("Fraction of life spent fading IN (0..1) — drops ease in near the top.")]
        [Range(0f, 1f)] public float FadeIn;
        [Tooltip("Fraction of life spent fading OUT (0..1) — drops fade near the bottom.")]
        [Range(0f, 1f)] public float FadeOut;
        [Tooltip("Rain tint. A cool pale streak reads as rain over the water; alpha is driven by life + intensity + day/night.")]
        public Color Color;
        [Tooltip("Peak opacity at full intensity (0..1) before the life envelope + day/night scale it.")]
        [Range(0f, 1f)] public float MaxAlpha;

        [Header("Day / night")]
        [Tooltip("How strongly night dims the rain (0 = ignore time of day, 1 = tracks the light exactly). " +
                 "Rain keeps falling at night, so a low fade reads true.")]
        [Range(0f, 1f)] public float NightFade;
        [Tooltip("How faintly the rain catches MOONLIGHT at night so a downpour never blacks out entirely (0..1 floor).")]
        [Range(0f, 1f)] public float MoonlightCatch;

        public static RainConfig Default => new RainConfig
        {
            MaxDrops          = 64,
            AreaHalfSize      = new Vector2(16f, 10f),
            BaselineIntensity = 0f,      // feature OFF by default — no rain until the sea builds AND murks up
            SeaStateWeight    = 1.0f,
            FogWeight         = 0.6f,
            FallSpeed         = 14f,
            WindResponse      = 0.35f,
            Length            = 0.9f,
            Size              = 0.06f,
            Lifetime          = 1.6f,
            FadeIn            = 0.15f,
            FadeOut           = 0.25f,
            Color             = new Color(0.78f, 0.85f, 0.92f, 1f),
            MaxAlpha          = 0.5f,
            NightFade         = 0.25f,
            MoonlightCatch    = 0.1f,
        };
    }
}
