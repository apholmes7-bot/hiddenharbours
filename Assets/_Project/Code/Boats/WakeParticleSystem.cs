using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The PURE, engine-light simulation behind the boat wake (the owner's brief: "the wake animation
    /// [should] follow the boat, it needs to travel with the current as the waves distort it, once it
    /// loses force a distance from the boat it dissipates"). A fixed POOL of foam-puff particles shed at
    /// the stern, advected by their own momentum + the live tidal current, wobbled by the waves, and faded
    /// + spread over a lifetime. ALL FOUR brief points live here as deterministic, side-effect-free math so
    /// they can be EditMode-tested headless without opening Unity:
    /// <list type="number">
    /// <item><description><b>Follow the boat</b> — particles are EMITTED at the stern
    /// (<see cref="SternEmitPoint"/>) at a rate proportional to boat SPEED (<see cref="EmissionCount"/>),
    /// none below a small threshold and none when aground. Two diverging streams at the Kelvin-V half-angle
    /// give the classic V; the boat's own motion seeds each puff's start velocity, so the freshest foam sits
    /// right under the stern and the V trails the hull.</description></item>
    /// <item><description><b>Travel with the current</b> — every live particle integrates
    /// <c>pos += (particleVel + current) * dt</c> (<see cref="Advect"/>) so it DRIFTS on the live tidal set
    /// on top of its own momentum, and <c>particleVel *= velocityDecay^dt</c> so the wake's own push fades
    /// ("loses force"); far from the boat only the current's drift remains.</description></item>
    /// <item><description><b>Waves distort it</b> — a deterministic value-noise displacement keyed by
    /// (worldPos, time) and scaled by sea-state (<see cref="WaveDistort"/>) wobbles each puff; glassy water
    /// is undistorted, a rough sea breaks the wake up. No RNG — a stable hash, scoped to the VFX.</description></item>
    /// <item><description><b>Dissipate</b> — each particle has a LIFETIME; over its life its opacity fades to
    /// 0 (<see cref="LifeFade"/>) and its size SPREADS (<see cref="LifeSpread"/>), so a distance/time astern
    /// reads as faded + spread = dissolved.</description></item>
    /// </list>
    ///
    /// <para><b>Determinism &amp; purity (CLAUDE.md rule 5).</b> The particle jitter is a deterministic hash
    /// of (cell, time), NOT <see cref="System.Random"/>; the whole system is a pure function of its inputs
    /// and the seeded emission counter, so identical inputs reproduce identical foam. It reads the sim but
    /// drives no sim and saves nothing — visual only. No per-tick allocation: the pool is fixed and dead
    /// particles are recycled in place (rule 7).</para>
    ///
    /// <para><b>No magic numbers (rule 6).</b> Every tunable arrives via <see cref="WakeConfig"/>, which the
    /// owner-facing <see cref="BoatWakeEmitter"/> serializes; nothing here hard-codes feel.</para>
    /// </summary>
    public sealed class WakeParticleSystem
    {
        /// <summary>One foam puff. Struct-of-fields in a flat array — recycled, never re-allocated.</summary>
        public struct Particle
        {
            public bool   Alive;
            public Vector2 Pos;        // world position (m)
            public Vector2 Vel;        // own momentum (m/s), decays over life
            public float   Age;        // seconds since emit
            public float   Lifetime;   // seconds it lives
            public float   Seed;       // per-particle phase for the wave wobble (deterministic, set at emit)
            public float   BaseSize;   // size at birth (m), before spread
        }

        private readonly Particle[] _pool;
        private int _next;             // round-robin recycle cursor
        private uint _emitCounter;      // monotonic emit index → deterministic per-particle seed

        public WakeParticleSystem(int poolSize)
        {
            _pool = new Particle[Mathf.Max(1, poolSize)];
        }

        /// <summary>The backing pool (read-only iteration for the renderer; do not resize).</summary>
        public Particle[] Pool => _pool;
        public int Capacity => _pool.Length;

        /// <summary>Count of currently-live particles (for tests / debug).</summary>
        public int AliveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _pool.Length; i++) if (_pool[i].Alive) n++;
                return n;
            }
        }

        // ==== EMISSION (brief point 1: follow the boat, rate ∝ speed, gated) ===============================

        /// <summary>
        /// How many foam puffs to shed this tick: <c>rate = sheddPerSpeed · max(0, speed − threshold)</c>
        /// integrated over <paramref name="dt"/>, with the carried fraction so slow boats still emit a puff
        /// every few ticks instead of never. Returns <b>0</b> below the speed threshold or when aground — the
        /// wake only forms when the hull is actually pushing water (the brief's "follow the boat"). Pure +
        /// static so the speed-gate is unit-tested without the pool. The fractional carry is the only stateful
        /// part and is handled by <see cref="Emit"/>; this returns the *target* whole-count for a given carry.
        /// </summary>
        public static int EmissionCount(float speed, bool aground, in WakeConfig cfg, float dt, ref float carry)
        {
            if (aground || speed <= cfg.SpeedThreshold || dt <= 0f)
            {
                // Don't let carry build up while stopped, or the wake would "burp" on restart.
                carry = 0f;
                return 0;
            }
            float over = speed - cfg.SpeedThreshold;
            carry += cfg.ShedPerSpeed * over * dt;
            int whole = Mathf.FloorToInt(carry);
            if (whole > 0) carry -= whole;
            return whole;
        }

        /// <summary>
        /// The stern emit point: <c>boatPos − bowDir · sternOffset</c>. Foam is shed from BEHIND the hull
        /// (the brief's "shed at the stern"), so the V trails the boat. Pure + static.
        /// </summary>
        public static Vector2 SternEmitPoint(Vector2 boatPos, Vector2 bowDir, float sternOffset)
            => boatPos - SafeDir(bowDir) * sternOffset;

        /// <summary>
        /// The initial velocity of a newly-shed puff for one of the two diverging V streams. The Kelvin-V is
        /// built by pushing the puff <b>astern and outward</b> at <paramref name="cfg"/>.VHalfAngleDeg off the
        /// boat's stern-line, scaled by the boat's speed, so faster boats throw a wider, livelier wake. The
        /// <paramref name="side"/> is +1 (starboard quarter) or −1 (port quarter); a central turbulent stream
        /// uses side 0. The result is in WORLD space. Pure + static.
        /// </summary>
        public static Vector2 EmitVelocity(Vector2 bowDir, float speed, int side, in WakeConfig cfg)
        {
            Vector2 fwd = SafeDir(bowDir);
            Vector2 astern = -fwd;
            // Rotate the astern direction outward by ±half-angle for the two wings (0 = straight astern wash).
            float ang = Mathf.Deg2Rad * cfg.VHalfAngleDeg * (side == 0 ? 0f : Mathf.Sign(side));
            Vector2 dir = Rotate(astern, ang);
            return dir * (speed * cfg.WashSpeedScale);
        }

        /// <summary>
        /// Shed up to <paramref name="count"/> particles this tick from the stern. Each new puff alternates
        /// the V wings (and seeds a central wash if <paramref name="cfg"/>.CentralStream) so the streams
        /// stay balanced; its lifetime, base size and wobble-seed are set deterministically from the emit
        /// counter (no RNG). Recycles the oldest slot when the pool is full — zero allocation.
        /// </summary>
        public void Emit(int count, Vector2 boatPos, Vector2 bowDir, float speed, in WakeConfig cfg)
        {
            Vector2 stern = SternEmitPoint(boatPos, bowDir, cfg.SternOffset);
            for (int k = 0; k < count; k++)
            {
                // Cycle the wings: port, starboard, (optional) centre — deterministic from the counter.
                int phase = cfg.CentralStream ? (int)(_emitCounter % 3) : (int)(_emitCounter % 2);
                int side = phase == 0 ? -1 : phase == 1 ? +1 : 0;

                int i = _next;
                _next = (_next + 1) % _pool.Length;

                float seed = Hash01(_emitCounter);
                // Jitter the lifetime / size a touch per puff (deterministic) so the wake isn't a clone-stamp.
                float lifeJit = 1f + (Hash01(_emitCounter * 2654435761u) - 0.5f) * 2f * cfg.LifetimeJitter;
                float sizeJit = 1f + (Hash01(_emitCounter * 40503u + 7u) - 0.5f) * 2f * cfg.SizeJitter;

                _pool[i] = new Particle
                {
                    Alive    = true,
                    Pos      = stern,
                    Vel      = EmitVelocity(bowDir, speed, side, cfg),
                    Age      = 0f,
                    Lifetime = Mathf.Max(0.05f, cfg.Lifetime * lifeJit),
                    Seed     = seed,
                    BaseSize = Mathf.Max(0.01f, cfg.FoamSize * sizeJit),
                };
                _emitCounter++;
            }
        }

        // ==== INTEGRATION (brief 2: travel with the current; brief 4: dissipate) ===========================

        /// <summary>
        /// Advance one particle one tick: drift on its own momentum PLUS the live tidal current
        /// (<c>pos += (vel + current) · dt</c>), decay its own push (<c>vel *= velocityDecay^dt</c>), and age
        /// it. Returns the updated particle; sets <see cref="Particle.Alive"/> false once it outlives its
        /// lifetime. Pure + static: the advection/decay is the brief's "travel with the current … loses force"
        /// and is unit-tested directly. <paramref name="velocityDecay"/> is the per-second retention (0..1);
        /// raised to <paramref name="dt"/> for frame-rate independence.
        /// </summary>
        public static Particle Advect(Particle p, Vector2 current, float velocityDecay, float dt)
        {
            if (!p.Alive) return p;
            p.Pos += (p.Vel + current) * dt;
            // Frame-rate-independent exponential decay of the wake's OWN push (the current term is untouched,
            // so far astern the puff keeps only the current's drift — "once it loses force … only drift remains").
            float retain = Mathf.Pow(Mathf.Clamp01(velocityDecay), Mathf.Max(0f, dt));
            p.Vel *= retain;
            p.Age += dt;
            if (p.Age >= p.Lifetime) p.Alive = false;
            return p;
        }

        /// <summary>
        /// Step the whole pool one tick under the given current. Convenience over <see cref="Advect"/> for the
        /// runtime driver; the per-particle math is the pure static above (what the tests exercise).
        /// </summary>
        public void Step(Vector2 current, float velocityDecay, float dt)
        {
            for (int i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].Alive) continue;
                _pool[i] = Advect(_pool[i], current, velocityDecay, dt);
            }
        }

        // ==== LIFE CURVES (brief 4: fade + spread = dissolve) ==============================================

        /// <summary>
        /// Normalized life 0..1 (0 = just shed, 1 = at end of lifetime). Pure + static.
        /// </summary>
        public static float Life01(float age, float lifetime)
            => lifetime <= 0f ? 1f : Mathf.Clamp01(age / lifetime);

        /// <summary>
        /// Opacity over life: starts at <paramref name="cfg"/>.StartAlpha and FADES to 0 by end of life,
        /// shaped by <paramref name="cfg"/>.FadePower (1 = linear, &gt;1 = lingers bright then drops, &lt;1 =
        /// fades fast then trails). Monotonic non-increasing in life — a particle never brightens, so a puff a
        /// distance astern is always fainter than a fresh one. Pure + static.
        /// </summary>
        public static float LifeFade(float life01, in WakeConfig cfg)
        {
            float t = Mathf.Clamp01(life01);
            float remain = 1f - Mathf.Pow(t, Mathf.Max(0.01f, cfg.FadePower));
            return Mathf.Clamp01(cfg.StartAlpha) * Mathf.Clamp01(remain);
        }

        /// <summary>
        /// Size over life: GROWS from the puff's base size to <c>base · SpreadFactor</c> by end of life (the
        /// foam softens/spreads as it dissolves). Monotonic non-decreasing in life — a puff never shrinks, so
        /// "spread" reads correctly astern. Pure + static.
        /// </summary>
        public static float LifeSpread(float baseSize, float life01, in WakeConfig cfg)
        {
            float t = Mathf.Clamp01(life01);
            float grow = Mathf.Lerp(1f, Mathf.Max(1f, cfg.SpreadFactor), t);
            return Mathf.Max(0f, baseSize) * grow;
        }

        // ==== WAVE DISTORTION (brief 3: the waves distort it) =============================================

        /// <summary>
        /// A deterministic per-particle wave displacement (m), scaled by sea-state roughness so glassy water
        /// (roughness 0) leaves the wake undisturbed and a rough sea (roughness→1) wobbles/breaks it up. Built
        /// from a stable value-noise of (worldPos, time, perParticleSeed) — NO <see cref="System.Random"/> — so
        /// the same inputs reproduce the same wobble (rule 5), scoped to the VFX only. The amplitude is
        /// <paramref name="cfg"/>.WaveDistortAmount · roughness. Pure + static.
        /// </summary>
        public static Vector2 WaveDistort(Vector2 worldPos, float time, float seed, float roughness, in WakeConfig cfg)
        {
            float amp = cfg.WaveDistortAmount * Mathf.Clamp01(roughness);
            if (amp <= 0f) return Vector2.zero;
            float f = Mathf.Max(0.001f, cfg.WaveDistortFrequency);
            // Two decorrelated value-noise samples → an (x,y) wobble that swirls as time advances.
            float nx = ValueNoise(worldPos.x * f + seed * 13.17f, worldPos.y * f - time * cfg.WaveDistortSpeed);
            float ny = ValueNoise(worldPos.y * f - seed * 7.91f, worldPos.x * f + time * cfg.WaveDistortSpeed + 19.3f);
            // Map 0..1 noise to −1..1 so the wobble is zero-mean (doesn't drag the wake one way).
            return new Vector2(nx * 2f - 1f, ny * 2f - 1f) * amp;
        }

        /// <summary>
        /// The final RENDER position of a particle: its integrated position plus the wave wobble. The wobble is
        /// a display-only offset (it does NOT accumulate into <see cref="Particle.Pos"/>, so it can't drift the
        /// wake), keeping the integration clean and the distortion purely visual. Pure + static.
        /// </summary>
        public static Vector2 RenderPosition(in Particle p, float time, float roughness, in WakeConfig cfg)
            => p.Pos + WaveDistort(p.Pos, time, p.Seed, roughness, cfg);

        // ==== deterministic noise helpers (hash-based; no System.Random — rule 5) ==========================

        /// <summary>A stable 0..1 hash of a uint (the per-emit jitter source). Deterministic, no RNG.</summary>
        public static float Hash01(uint x)
        {
            // A small integer-hash avalanche (xorshift-mult), then map to 0..1.
            x ^= x >> 16; x *= 0x7feb352du;
            x ^= x >> 15; x *= 0x846ca68bu;
            x ^= x >> 16;
            return (x & 0xFFFFFF) / (float)0x1000000;   // 24-bit mantissa → [0,1)
        }

        /// <summary>
        /// Smooth, deterministic 2-D value noise in 0..1 (lattice hash + smoothstep bilerp). Cheap and stable —
        /// the same (x,y) always returns the same value, so the wave wobble is reproducible (rule 5). Not Unity's
        /// Perlin (which we avoid for portability/determinism guarantees), but the same character.
        /// </summary>
        public static float ValueNoise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float xf = x - xi;
            float yf = y - yi;
            float u = xf * xf * (3f - 2f * xf);   // smoothstep
            float v = yf * yf * (3f - 2f * yf);
            float a = LatticeHash(xi, yi);
            float b = LatticeHash(xi + 1, yi);
            float c = LatticeHash(xi, yi + 1);
            float d = LatticeHash(xi + 1, yi + 1);
            float ab = Mathf.Lerp(a, b, u);
            float cd = Mathf.Lerp(c, d, u);
            return Mathf.Lerp(ab, cd, v);
        }

        private static float LatticeHash(int xi, int yi)
        {
            unchecked
            {
                uint h = (uint)(xi * 374761393 + yi * 668265263);
                return Hash01(h);
            }
        }

        private static Vector2 SafeDir(Vector2 d)
            => d.sqrMagnitude > 1e-8f ? d.normalized : Vector2.up;

        private static Vector2 Rotate(Vector2 v, float radians)
        {
            float cs = Mathf.Cos(radians), sn = Mathf.Sin(radians);
            return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
        }
    }

    /// <summary>
    /// Every tunable of the wake, in one struct so the math stays free of magic numbers (CLAUDE.md rule 6).
    /// <see cref="BoatWakeEmitter"/> serializes an owner-editable instance and passes it in. Defaults are a
    /// sensible greybox feel; the owner tunes on the component.
    /// </summary>
    [System.Serializable]
    public struct WakeConfig
    {
        [Header("Emission (follow the boat)")]
        [Tooltip("Foam puffs shed per second, per (m/s) of boat speed above the threshold. Higher = a denser wake.")]
        public float ShedPerSpeed;
        [Tooltip("Boat speed (m/s) below which NO wake forms — a drifting/idling boat leaves no foam.")]
        public float SpeedThreshold;
        [Tooltip("How far astern of the boat origin the foam is shed (m) — the stern, behind the hull.")]
        public float SternOffset;
        [Tooltip("Half-angle (deg) of the diverging Kelvin-V wings off the stern line (~19° is the real wake angle).")]
        public float VHalfAngleDeg;
        [Tooltip("Adds a third, central turbulent stern stream (the prop wash) between the two V wings.")]
        public bool CentralStream;
        [Tooltip("Scales boat speed into the puff's initial outward/astern wash speed (m/s per m/s). Bigger = a livelier spreading V.")]
        public float WashSpeedScale;

        [Header("Travel with the current / lose force")]
        [Tooltip("Per-second retention of a puff's OWN momentum (0..1). <1 means the wake's push fades, leaving only the current's drift.")]
        public float VelocityDecay;

        [Header("Dissipate (lifetime + fade + spread)")]
        [Tooltip("Seconds a foam puff lives before it has fully dissolved.")]
        public float Lifetime;
        [Tooltip("Opacity at birth (0..1). Fades to 0 over the lifetime.")]
        public float StartAlpha;
        [Tooltip("Fade shaping: 1 = linear fade, >1 = stays bright then drops late, <1 = fades early then trails.")]
        public float FadePower;
        [Tooltip("How much a puff grows over its life (1 = no spread, 2 = doubles) — the foam softening/spreading as it dies.")]
        public float SpreadFactor;
        [Tooltip("Foam puff size at birth (m).")]
        public float FoamSize;
        [Tooltip("± random-ish (deterministic) variation in per-puff lifetime (0..1 fraction) so the wake isn't a clone-stamp.")]
        public float LifetimeJitter;
        [Tooltip("± deterministic variation in per-puff birth size (0..1 fraction).")]
        public float SizeJitter;

        [Header("Waves distort it")]
        [Tooltip("Max wave-wobble displacement (m) at full sea-state roughness. 0 = the waves never distort the wake.")]
        public float WaveDistortAmount;
        [Tooltip("Spatial frequency of the wave wobble (1/m) — higher = a finer, busier break-up.")]
        public float WaveDistortFrequency;
        [Tooltip("How fast the wave wobble swirls over time.")]
        public float WaveDistortSpeed;

        /// <summary>The greybox default feel — a lively-but-subtle inshore wake. The owner tunes from here.</summary>
        public static WakeConfig Default => new WakeConfig
        {
            ShedPerSpeed         = 6f,
            SpeedThreshold       = 0.4f,
            SternOffset          = 0.5f,
            VHalfAngleDeg        = 19f,
            CentralStream        = true,
            WashSpeedScale       = 0.35f,
            VelocityDecay        = 0.35f,
            Lifetime             = 2.2f,
            StartAlpha           = 0.7f,
            FadePower            = 1.4f,
            SpreadFactor         = 2.2f,
            FoamSize             = 0.35f,
            LifetimeJitter       = 0.25f,
            SizeJitter           = 0.3f,
            WaveDistortAmount    = 0.18f,
            WaveDistortFrequency = 0.6f,
            WaveDistortSpeed     = 0.5f,
        };
    }
}
