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
    /// none below a small threshold and none when aground. Each foam puff of the two wings is placed
    /// DIRECTLY ON the Kelvin-V arm geometry (<see cref="ArmEmitPoint"/>): from the stern apex, astern and
    /// outward at the half-angle by a deterministic distance up to <c>ArmLength</c>, so the arms are crisp
    /// diverging lines that WIDEN with distance by construction (a true V, not a soft cone). A fraction of
    /// puffs fill the turbulent stern churn between the arms (<see cref="SternFillPoint"/>). The V trails the
    /// hull because the apex sits at the stern.</description></item>
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
            public float   BirthStrength; // 0..1 strength BAKED at emit (speed-onset at birth) — a deposited
                                          // trail must keep the brightness it was laid with even after the
                                          // boat stops, instead of dimming with the boat's LIVE speed.
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
        /// The world position of a foam puff placed DIRECTLY ON one of the two diverging Kelvin-V arms — this
        /// is what makes the wake read as a crisp V rather than a soft cone. From the stern apex
        /// (<see cref="SternEmitPoint"/>) it walks <b>astern and outward</b> at <paramref name="cfg"/>.VHalfAngleDeg
        /// off the stern-line, by a distance <c>t · ArmLength</c> along the arm (<paramref name="t01"/> in 0..1).
        /// Because the arm direction is a FIXED half-angle, the lateral spread grows linearly with that distance —
        /// the arms WIDEN with distance by construction. <paramref name="side"/> is −1 (port) or +1 (starboard).
        /// Result is in WORLD space. Pure + static (the geometry the tests pin).
        /// </summary>
        public static Vector2 ArmEmitPoint(Vector2 boatPos, Vector2 bowDir, float t01, int side, in WakeConfig cfg)
        {
            Vector2 fwd = SafeDir(bowDir);
            Vector2 apex = boatPos - fwd * cfg.SternOffset;        // the V apex sits at the stern
            Vector2 astern = -fwd;
            float ang = Mathf.Deg2Rad * cfg.VHalfAngleDeg * Mathf.Sign(side == 0 ? 1 : side);
            Vector2 armDir = Rotate(astern, ang);                   // unit direction down this wing
            float dist = Mathf.Clamp01(t01) * Mathf.Max(0f, cfg.ArmLength);
            return apex + armDir * dist;
        }

        /// <summary>
        /// The world position of a turbulent stern-fill puff — the churn BETWEEN the two arms (the prop/keel wash).
        /// It sits along the centre stern-line a short way astern, jittered laterally inside the V so the fill never
        /// punches past the crisp arm edges. <paramref name="t01"/> is the along-line fraction, <paramref name="lat"/>
        /// in −1..1 the lateral position scaled by the V width at that distance times <paramref name="cfg"/>.SternFillWidth.
        /// Pure + static.
        /// </summary>
        public static Vector2 SternFillPoint(Vector2 boatPos, Vector2 bowDir, float t01, float lat, in WakeConfig cfg)
        {
            Vector2 fwd = SafeDir(bowDir);
            Vector2 apex = boatPos - fwd * cfg.SternOffset;
            Vector2 astern = -fwd;
            Vector2 rightOf = new Vector2(astern.y, -astern.x);    // perpendicular (lateral) axis
            float halfAngle = Mathf.Deg2Rad * cfg.VHalfAngleDeg;
            // Walk astern by the SAME projection the arms reach at this t (armDist·cos), so the fill triangle
            // shares the arms' astern extent. The V half-width at that astern distance is astern·tan(halfAngle);
            // the fill is bounded by it (scaled by SternFillWidth ≤ 1) so it never punches past the crisp edges.
            float asternDist = Mathf.Clamp01(t01) * Mathf.Max(0f, cfg.ArmLength) * Mathf.Cos(halfAngle);
            float halfWidth = Mathf.Tan(halfAngle) * asternDist;
            float lateral = Mathf.Clamp(lat, -1f, 1f) * halfWidth * Mathf.Clamp01(cfg.SternFillWidth);
            return apex + astern * asternDist + rightOf * lateral;
        }

        /// <summary>
        /// Shed up to <paramref name="count"/> particles this tick. Each new puff is assigned a stream — the two V
        /// WINGS (placed on the crisp arm geometry via <see cref="ArmEmitPoint"/>) most of the time, and the
        /// turbulent STERN FILL (<see cref="SternFillPoint"/>) for a deterministic <paramref name="cfg"/>.SternFillFraction
        /// of puffs — so the wake reads as two diverging arms with churn between them. Per-puff along-arm distance,
        /// lateral jitter, lifetime, size and wobble-seed are all deterministic from the emit counter (no RNG). Each
        /// puff also gets a gentle along-stream velocity so it keeps flowing astern before the current takes over.
        /// Recycles the oldest slot when the pool is full — zero allocation.
        /// </summary>
        public void Emit(int count, Vector2 boatPos, Vector2 bowDir, float speed, in WakeConfig cfg)
        {
            for (int k = 0; k < count; k++)
            {
                // Deterministic per-puff dice from the monotonic counter (no RNG, rule 5).
                float diceStream = Hash01(_emitCounter * 0x9E3779B1u + 0x85u);   // stream selection
                float t01        = Hash01(_emitCounter * 0x27D4EB2Fu + 0x13u);   // along-arm fraction
                float seed       = Hash01(_emitCounter);
                float lifeJit    = 1f + (Hash01(_emitCounter * 2654435761u) - 0.5f) * 2f * cfg.LifetimeJitter;
                float sizeJit    = 1f + (Hash01(_emitCounter * 40503u + 7u) - 0.5f) * 2f * cfg.SizeJitter;

                Vector2 pos;
                Vector2 vel;
                bool fill = cfg.SternFillFraction > 0f && diceStream < Mathf.Clamp01(cfg.SternFillFraction);
                if (fill)
                {
                    float lat = Hash01(_emitCounter * 0x165667B1u + 0x9Du) * 2f - 1f;  // −1..1 lateral inside the V
                    pos = SternFillPoint(boatPos, bowDir, t01, lat, cfg);
                    vel = EmitVelocity(bowDir, speed, 0, cfg);          // straight astern wash
                }
                else
                {
                    // Alternate the wings deterministically so the two arms stay balanced.
                    int side = ((int)(_emitCounter & 1u)) == 0 ? -1 : +1;
                    pos = ArmEmitPoint(boatPos, bowDir, t01, side, cfg);
                    vel = EmitVelocity(bowDir, speed, side, cfg);       // gentle outward+astern flow along the arm
                }

                int i = _next;
                _next = (_next + 1) % _pool.Length;
                _pool[i] = new Particle
                {
                    Alive    = true,
                    Pos      = pos,
                    Vel      = vel,
                    Age      = 0f,
                    Lifetime = Mathf.Max(0.05f, cfg.Lifetime * lifeJit),
                    Seed     = seed,
                    BaseSize = Mathf.Max(0.01f, cfg.FoamSize * sizeJit),
                    BirthStrength = 1f,
                };
                _emitCounter++;
            }
        }

        /// <summary>
        /// Emit ONE particle at an explicit world position/velocity — the DEPOSITED-TRAIL emit
        /// (<see cref="WakeTrailMath"/> computes where and how fast; this only owns the pooled slot and the
        /// deterministic per-particle jitter, exactly as <see cref="Emit"/> does for the template streams).
        /// <paramref name="lifetimeScale"/>/<paramref name="sizeScale"/> layer the trail's grading on top of
        /// the config's base lifetime/size (both still jittered from the emit counter — no clone-stamp trail);
        /// <paramref name="birthStrength"/> is BAKED into the particle so a laid deposit keeps its birth
        /// brightness after the boat slows/stops (the trail persists — the owner's ask). Recycles the oldest
        /// slot when the pool is full: emission can never exceed the pool (rule 7). Deterministic (rule 5),
        /// zero allocation.
        /// </summary>
        public void EmitAt(Vector2 pos, Vector2 vel, in WakeConfig cfg,
                           float lifetimeScale, float sizeScale, float birthStrength)
        {
            float seed    = Hash01(_emitCounter);
            float lifeJit = 1f + (Hash01(_emitCounter * 2654435761u) - 0.5f) * 2f * cfg.LifetimeJitter;
            float sizeJit = 1f + (Hash01(_emitCounter * 40503u + 7u) - 0.5f) * 2f * cfg.SizeJitter;

            int i = _next;
            _next = (_next + 1) % _pool.Length;
            _pool[i] = new Particle
            {
                Alive    = true,
                Pos      = pos,
                Vel      = vel,
                Age      = 0f,
                Lifetime = Mathf.Max(0.05f, cfg.Lifetime * lifeJit * Mathf.Max(0.01f, lifetimeScale)),
                Seed     = seed,
                BaseSize = Mathf.Max(0.01f, cfg.FoamSize * sizeJit * Mathf.Max(0.01f, sizeScale)),
                BirthStrength = Mathf.Clamp01(birthStrength),
            };
            _emitCounter++;
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
        [Tooltip("Half-angle (deg) of the diverging Kelvin-V wings off the stern line (~19° is the real Kelvin wake angle). Smaller = a narrower, sharper V.")]
        public float VHalfAngleDeg;
        [Tooltip("Length (m) of each diverging V arm — how far astern the crisp foam edges reach before they hand off to dissipation. Longer = a bigger, more obvious V.")]
        public float ArmLength;
        [Tooltip("Fraction (0..1) of puffs that fill the turbulent stern churn BETWEEN the arms (the prop/keel wash). 0 = clean arms only; higher = a busier centre.")]
        public float SternFillFraction;
        [Tooltip("How wide the stern fill spreads, as a fraction (0..1) of the V's own width at each distance — kept inside the arms so the fill never blurs the crisp edges.")]
        public float SternFillWidth;
        [Tooltip("Scales boat speed into the puff's initial along-arm/astern flow speed (m/s per m/s). Bigger = a livelier, faster-flowing V; the current then takes over as it decays.")]
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
            ShedPerSpeed         = 10f,
            SpeedThreshold       = 0.4f,
            SternOffset          = 0.5f,
            VHalfAngleDeg        = 19f,
            ArmLength            = 3.0f,
            SternFillFraction    = 0.3f,
            SternFillWidth       = 0.7f,
            WashSpeedScale       = 0.2f,
            VelocityDecay        = 0.5f,
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
