using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// BUBBLES — the pure, engine-light simulation behind the owner's 2026-08-27 ask: <i>"i want to see
    /// bubbles form and drift but they arent entirely noticeable, everything looks very organized and
    /// shader-like and not particle like."</i>
    ///
    /// <para><b>What was actually wrong.</b> The wake already had bubbles — painted INTO the foam sprite by
    /// <c>WakeFoamTexture</c>'s aeration pass (#443). That is precisely the complaint: a bubble that is part
    /// of a texture is a PATTERN, and a pattern applied to every puff of a churn reads as a shader, because
    /// it is one. Bubbles only read as bubbles when they are individually addressable things with their own
    /// clocks. So this stream exists: discrete, pooled, each with its own birth, size, shade, drift and
    /// death.</para>
    ///
    /// <para><b>The four properties that buy "particle-like", and none of them is more pattern:</b>
    /// <list type="number">
    /// <item><description><b>BURSTY arrival</b> (<see cref="BurstCount"/>) — churn does not meter bubbles out
    /// at a steady rate; it throws clusters and then nothing. A constant per-tick rate is a metronome, and
    /// the eye reads a metronome as machinery. Arrivals here are a deterministic Bernoulli per slot, so the
    /// long-run rate is the configured one while any given moment is uneven.</description></item>
    /// <item><description><b>HEAVY-TAILED size</b> (<see cref="SizeAt"/>) — most bubbles are small and a few
    /// are large. A uniform size distribution gives a uniform field; a biased one gives a handful of
    /// individually READABLE bubbles riding a haze of small ones, which is what the owner is asking to be
    /// able to see.</description></item>
    /// <item><description><b>POP, not fade</b> (<see cref="AlphaAt"/> / <see cref="SizeAt(float,float,in
    /// WakeBubbleConfig)"/>) — a bubble holds its opacity for most of its life, then in its last moments
    /// SWELLS and goes. Foam fades; bubbles burst. Giving the two streams different death signatures is what
    /// stops the bubbles reading as more foam.</description></item>
    /// <item><description><b>OWN clocks</b> — lifetime, size, ramp position and drift are all per-particle,
    /// keyed off the birth seed, so no two bubbles in a cluster do the same thing at the same
    /// time.</description></item>
    /// </list></para>
    ///
    /// <para><b>Determinism &amp; purity (rule 5).</b> Every function is pure and deterministic in its
    /// arguments; variation comes from a stable integer avalanche over the monotonic emit counter, never
    /// <see cref="System.Random"/>. The stream reads the sim (speed, current, wind) and drives none of it,
    /// publishes nothing and saves nothing — presentation only.</para>
    ///
    /// <para><b>Budget (rule 7).</b> A fixed pool, recycled in place round-robin; a full pool recycles its
    /// oldest slot rather than growing. Emission can never exceed <see cref="Capacity"/> in a tick, and the
    /// per-tick cap is pinned by test. Zero allocation after construction.</para>
    /// </summary>
    public sealed class WakeBubbleSystem
    {
        /// <summary>One bubble. Flat struct in a flat array — recycled, never re-allocated.</summary>
        public struct Bubble
        {
            public bool    Alive;
            public Vector2 Pos;       // world position (m)
            public Vector2 Vel;       // own momentum (m/s) at birth, decaying
            public float   Age;       // seconds since it formed
            public float   Lifetime;  // seconds it lives before it pops
            public float   Seed;      // per-bubble 0..1 — drives its ramp offset, its shade jitter, its wobble
            public float   BaseSize;  // diameter at birth (m), before the pop swell
            public float   Strength;  // 0..1 opacity scale BAKED at birth (the churn's vigour where it formed)
        }

        private readonly Bubble[] _pool;
        private int _next;          // round-robin recycle cursor
        private uint _emitCounter;  // monotonic emit index -> the deterministic per-bubble seed

        public WakeBubbleSystem(int poolSize)
        {
            _pool = new Bubble[Mathf.Max(1, poolSize)];
        }

        /// <summary>The backing pool (read-only iteration for the renderer; do not resize).</summary>
        public Bubble[] Pool => _pool;
        public int Capacity => _pool.Length;

        /// <summary>Count of currently-live bubbles (tests / budget assertions).</summary>
        public int AliveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _pool.Length; i++) if (_pool[i].Alive) n++;
                return n;
            }
        }

        // ==== ARRIVAL (property 1: bursty, not metered) ====================================================

        /// <summary>
        /// How many bubbles form this tick — a deterministic BERNOULLI draw, not a rate times dt.
        ///
        /// <para>The expected arrivals in this tick are <c>lambda = rate · vigour · dt</c>. Rather than
        /// emitting <c>floor(lambda)</c> plus a carried fraction (which produces a perfectly even train — a
        /// metronome), each of <see cref="WakeBubbleConfig.BurstSlots"/> independent slots either fires or
        /// does not, with probability <c>lambda / slots</c>, decided by a stable hash of
        /// <paramref name="drawIndex"/> and the slot. Long-run rate: identical. Moment-to-moment: uneven,
        /// occasionally a cluster of several, occasionally nothing — which is what churn does and what the
        /// eye reads as things rather than machinery.</para>
        ///
        /// <para>Returns 0 below <see cref="WakeBubbleConfig.SpeedThreshold"/> or when aground: bubbles form
        /// where the hull is WORKING water, not wherever the boat happens to be. Bounded above by
        /// <see cref="WakeBubbleConfig.BurstSlots"/> by construction, which is the per-tick pool guard.</para>
        ///
        /// <para>Pure + static: <paramref name="drawIndex"/> is the caller's monotonic tick counter, so the
        /// sequence is reproducible and testable without a pool.</para>
        /// </summary>
        public static int BurstCount(float vigour01, bool aground, in WakeBubbleConfig cfg, float dt,
                                     uint drawIndex)
        {
            if (aground || dt <= 0f) return 0;
            float vigour = Mathf.Clamp01(vigour01);
            if (vigour <= 0f) return 0;

            int slots = Mathf.Max(1, cfg.BurstSlots);
            float lambda = Mathf.Max(0f, cfg.FormPerSecond) * vigour * dt;
            float p = Mathf.Clamp01(lambda / slots);
            if (p <= 0f) return 0;

            int n = 0;
            for (int s = 0; s < slots; s++)
            {
                // One decorrelated draw per (tick, slot). The slot salt is multiplied by a large odd
                // constant so consecutive slots do not walk adjacent hash buckets.
                if (Hash01(drawIndex * 2654435761u + (uint)s * 0x9E3779B9u) < p) n++;
            }
            return n;
        }

        /// <summary>
        /// The speed-driven VIGOUR of the churn, 0..1 — how hard the hull is working the water, which is what
        /// sets both the bubble rate and each bubble's birth opacity. Zero at or below
        /// <see cref="WakeBubbleConfig.SpeedThreshold"/>, saturating at
        /// <see cref="WakeBubbleConfig.FullVigourSpeed"/>. <paramref name="grade01"/> is the hull's
        /// <c>WakeGrading</c> magnitude (size x weight x speed) so a dragger boils where a dory fizzes — the
        /// bow half of the owner's original ask, graded by the same law the plume already uses.
        /// Pure + static.
        /// </summary>
        public static float Vigour01(float speed, float grade01, in WakeBubbleConfig cfg)
        {
            if (speed <= cfg.SpeedThreshold) return 0f;
            float span = Mathf.Max(0.01f, cfg.FullVigourSpeed - cfg.SpeedThreshold);
            float bySpeed = Mathf.Clamp01((speed - cfg.SpeedThreshold) / span);
            // The grade LIFTS rather than gates: a small boat at speed still bubbles, a big one bubbles more.
            float byGrade = Mathf.Lerp(1f - Mathf.Clamp01(cfg.GradeInfluence), 1f, Mathf.Clamp01(grade01));
            return Mathf.Clamp01(bySpeed * byGrade);
        }

        // ==== SIZE (property 2: heavy-tailed, so a few are individually readable) ==========================

        /// <summary>
        /// A bubble's birth diameter (m) from a uniform draw <paramref name="u01"/>, biased SMALL.
        ///
        /// <para><c>size = min + (max − min) · u^bias</c>. With <see cref="WakeBubbleConfig.SizeBias"/> &gt; 1
        /// the mass of the distribution sits near <c>min</c> and the large sizes are rare — a haze of small
        /// bubbles with a scatter of big readable ones, instead of a uniform field of identical dots. Exactly
        /// spans <c>[min, max]</c> at u = 0 and u = 1, and is monotone in u, so the owner's two size knobs
        /// still mean what they say.</para>
        /// </summary>
        public static float SizeAt(float u01, in WakeBubbleConfig cfg)
        {
            float u = Mathf.Clamp01(u01);
            float min = Mathf.Max(0.001f, cfg.MinSize);
            float max = Mathf.Max(min, cfg.MaxSize);
            return min + (max - min) * Mathf.Pow(u, Mathf.Max(0.01f, cfg.SizeBias));
        }

        // ==== DEATH (property 3: it POPS — a different signature from the foam's fade) =====================

        /// <summary>
        /// Opacity over life: a bubble HOLDS, then goes.
        ///
        /// <para>Through the first <c>1 − PopFraction</c> of its life it stays at full opacity (a bubble on
        /// the water does not gradually become transparent — it is there, and then it is not). Across the
        /// final <see cref="WakeBubbleConfig.PopFraction"/> it drops to 0. Non-increasing in life, exactly
        /// 0 at life 1.</para>
        ///
        /// <para>This is deliberately UNLIKE <c>WakeParticleSystem.LifeFade</c>, whose whole curve is a
        /// fade. Two streams that die the same way read as one stream; the bubbles have to burst for the eye
        /// to separate them from the foam they sit in.</para>
        /// </summary>
        public static float AlphaAt(float life01, in WakeBubbleConfig cfg)
        {
            float t = Mathf.Clamp01(life01);
            float pop = Mathf.Clamp(cfg.PopFraction, 0.01f, 1f);
            float holdUntil = 1f - pop;
            if (t <= holdUntil) return 1f;
            return Mathf.Clamp01((1f - t) / pop);
        }

        /// <summary>
        /// Diameter over life: steady, then a SWELL into the pop.
        ///
        /// <para>A bubble keeps its birth size through the hold, then grows to
        /// <see cref="WakeBubbleConfig.PopSwell"/> x that size as its film thins and lets go. Combined with
        /// <see cref="AlphaAt"/> the last moments read as a burst rather than a dissolve. Monotone
        /// non-decreasing, so a bubble never shrinks.</para>
        /// </summary>
        public static float SizeOverLife(float baseSize, float life01, in WakeBubbleConfig cfg)
        {
            float t = Mathf.Clamp01(life01);
            float pop = Mathf.Clamp(cfg.PopFraction, 0.01f, 1f);
            float holdUntil = 1f - pop;
            if (t <= holdUntil) return Mathf.Max(0f, baseSize);
            float k = (t - holdUntil) / pop;
            return Mathf.Max(0f, baseSize) * Mathf.Lerp(1f, Mathf.Max(1f, cfg.PopSwell), k);
        }

        // ==== EMISSION ====================================================================================

        /// <summary>
        /// Form one bubble at a world point. <paramref name="jitterRadius"/> scatters it off that point so a
        /// cluster is a cluster and not a stack; <paramref name="strength01"/> is baked at birth for the same
        /// reason the foam bakes its own — a bubble left behind keeps the vigour it formed in after the boat
        /// has gone by, instead of dimming with her live speed. Recycles the oldest slot when full (rule 7);
        /// deterministic, zero allocation.
        /// </summary>
        public void Form(Vector2 pos, Vector2 vel, float jitterRadius, float strength01,
                         in WakeBubbleConfig cfg)
        {
            float seed  = Hash01(_emitCounter);
            float uSize = Hash01(_emitCounter * 40503u + 7u);
            float uLife = Hash01(_emitCounter * 2654435761u + 13u);
            float uAngle = Hash01(_emitCounter * 22695477u + 29u);
            float uRad  = Hash01(_emitCounter * 1103515245u + 31u);

            float ang = uAngle * Mathf.PI * 2f;
            // sqrt on the radius draw keeps the scatter AREA-uniform; a raw uniform radius piles bubbles
            // at the centre of every cluster, which is the stacked look this scatter exists to avoid.
            float rad = Mathf.Max(0f, jitterRadius) * Mathf.Sqrt(uRad);
            Vector2 offset = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;

            float lifeJit = 1f + (uLife - 0.5f) * 2f * Mathf.Clamp01(cfg.LifetimeJitter);

            int i = _next;
            _next = (_next + 1) % _pool.Length;
            _pool[i] = new Bubble
            {
                Alive    = true,
                Pos      = pos + offset,
                Vel      = vel,
                Age      = 0f,
                Lifetime = Mathf.Max(0.05f, cfg.Lifetime * lifeJit),
                Seed     = seed,
                BaseSize = SizeAt(uSize, in cfg),
                Strength = Mathf.Clamp01(strength01),
            };
            _emitCounter++;
        }

        // ==== INTEGRATION =================================================================================

        /// <summary>
        /// Advance one bubble one tick: drift on its own momentum plus the water it sits in
        /// (<c>pos += (vel + drift) · dt</c>), decay its own push, and age it. Sets
        /// <see cref="Bubble.Alive"/> false once it outlives its lifetime. Pure + static so the drift law is
        /// unit-tested directly; <paramref name="velocityDecay"/> is a per-second retention raised to
        /// <paramref name="dt"/> for frame-rate independence.
        /// </summary>
        public static Bubble Advect(Bubble b, Vector2 drift, float velocityDecay, float dt)
        {
            if (!b.Alive) return b;
            b.Pos += (b.Vel + drift) * dt;
            b.Vel *= Mathf.Pow(Mathf.Clamp01(velocityDecay), Mathf.Max(0f, dt));
            b.Age += dt;
            if (b.Age >= b.Lifetime) b.Alive = false;
            return b;
        }

        /// <summary>Step the whole pool one tick. The per-bubble maths is the pure static above.</summary>
        public void Step(Vector2 drift, float velocityDecay, float dt)
        {
            for (int i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].Alive) continue;
                _pool[i] = Advect(_pool[i], drift, velocityDecay, dt);
            }
        }

        /// <summary>
        /// The drift a bubble rides: the tidal current it floats on plus a share of the WIND. Bubbles take a
        /// LARGER share than foam does — they stand proud of the surface where the air actually reaches them,
        /// where a foam raft lies in it. That difference in coupling is why a bubble visibly leaves the trail
        /// it was born in, which is half of "form and drift". Pure + static.
        /// </summary>
        public static Vector2 DriftVelocity(Vector2 current, Vector2 wind, float windFraction)
            => current + wind * Mathf.Clamp01(windFraction);

        /// <summary>Normalized life 0..1 (0 = just formed, 1 = popped). Pure + static.</summary>
        public static float Life01(float age, float lifetime)
            => lifetime <= 0f ? 1f : Mathf.Clamp01(age / lifetime);

        /// <summary>A stable 0..1 hash of a uint — the SAME avalanche
        /// <c>WakeParticleSystem.Hash01</c> uses, so the two streams' determinism is one story.</summary>
        public static float Hash01(uint x)
        {
            unchecked
            {
                x ^= x >> 16; x *= 0x7feb352du;
                x ^= x >> 15; x *= 0x846ca68bu;
                x ^= x >> 16;
                return (x & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }

    /// <summary>
    /// Every tunable of the bubble stream, in one struct so the maths carries no magic numbers (rule 6).
    /// <c>BoatWakeEmitter</c> serializes an owner-editable instance; <see cref="Enabled"/> false is the
    /// revert (no pool ticked, no renderers shown, nothing drawn).
    /// </summary>
    [System.Serializable]
    public struct WakeBubbleConfig
    {
        [Tooltip("Master switch for the bubble stream. Off = exactly the wake as it shipped before bubbles.")]
        public bool Enabled;

        [Header("Where they form")]
        [Tooltip("Boat speed (m/s) below which no bubbles form - a drifting hull is not working the water.")]
        public float SpeedThreshold;
        [Tooltip("Boat speed (m/s) at which the churn is at full vigour.")]
        public float FullVigourSpeed;
        [Tooltip("How much the hull's WakeGrading magnitude (size x weight x speed) lifts the rate. 0 = every hull bubbles alike; 1 = a dory fizzes where a dragger boils.")]
        [Range(0f, 1f)] public float GradeInfluence;
        [Tooltip("Fraction of bubbles that form at the BOW (the water working against the stem) rather than in the stern churn.")]
        [Range(0f, 1f)] public float BowFraction;
        [Tooltip("Radius the stern cluster scatters over, as a FRACTION OF HULL LENGTH - so it grades with the boat for free rather than needing a second constant per hull.")]
        public float SternScatterFraction;
        [Tooltip("Radius the bow cluster scatters over, as a fraction of hull length.")]
        public float BowScatterFraction;

        [Header("How they arrive (bursty, not metered)")]
        [Tooltip("Long-run bubbles formed per second at full vigour.")]
        public float FormPerSecond;
        [Tooltip("Independent arrival slots per tick. More slots = a smoother train; fewer = burstier clusters. Also the hard per-tick cap.")]
        [Min(1)] public int BurstSlots;

        [Header("How big they are")]
        [Tooltip("Smallest bubble diameter (m).")]
        public float MinSize;
        [Tooltip("Largest bubble diameter (m) - the few that are individually readable.")]
        public float MaxSize;
        [Tooltip("Size distribution bias. 1 = uniform; higher = mostly small with a rare big one (what reads as bubbles rather than a field of dots).")]
        [Min(0.01f)] public float SizeBias;

        [Header("How long they last, and how they go")]
        [Tooltip("Seconds a bubble lives before it pops.")]
        public float Lifetime;
        [Tooltip("+/- deterministic variation in per-bubble lifetime (0..1 fraction) - their own clocks.")]
        [Range(0f, 1f)] public float LifetimeJitter;
        [Tooltip("Fraction of life spent popping. Small = it holds, then bursts; 1 = it fades the whole way like foam.")]
        [Range(0.01f, 1f)] public float PopFraction;
        [Tooltip("How much a bubble swells as it bursts (1 = no swell).")]
        [Min(1f)] public float PopSwell;

        [Header("How they drift")]
        [Tooltip("Per-second retention of a bubble's own momentum (0..1); below 1 its push fades and only the water's drift remains.")]
        [Range(0f, 1f)] public float VelocityDecay;
        [Tooltip("Share of the WIND a bubble takes. Higher than the foam's, deliberately: a bubble stands proud of the surface where the air reaches it.")]
        [Range(0f, 1f)] public float WindDriftFraction;
        [Tooltip("Scales boat speed into a bubble's initial push away from the churn (m/s per m/s).")]
        public float BirthSpeedScale;

        [Header("Opacity")]
        [Tooltip("Opacity of a bubble at full vigour, before its own pop curve.")]
        [Range(0f, 1f)] public float Opacity;

        /// <summary>
        /// The shipped feel: a fizzing, uneven cluster at the stern and a lighter one at the stem. 26/s at
        /// full vigour over 6 slots means most ticks throw 0-2 bubbles and the occasional tick throws four -
        /// visibly uneven at 30 Hz. Sizes 0.05-0.30 m at bias 2.6 put the median near 0.09 m (a fleck) while
        /// still throwing the occasional 0.25 m bubble the eye can actually follow. A 1.1 s life with a 0.22
        /// pop means each one is READ, then bursts.
        /// </summary>
        public static WakeBubbleConfig Default => new WakeBubbleConfig
        {
            Enabled            = true,
            SpeedThreshold     = 0.35f,
            FullVigourSpeed    = 3.5f,
            GradeInfluence     = 0.6f,
            BowFraction        = 0.35f,
            SternScatterFraction = 0.16f,
            BowScatterFraction   = 0.09f,
            FormPerSecond      = 26f,
            BurstSlots         = 6,
            MinSize            = 0.05f,
            MaxSize            = 0.30f,
            SizeBias           = 2.6f,
            Lifetime           = 1.1f,
            LifetimeJitter     = 0.55f,
            PopFraction        = 0.22f,
            PopSwell           = 1.6f,
            VelocityDecay      = 0.35f,
            WindDriftFraction  = 0.35f,
            BirthSpeedScale    = 0.18f,
            Opacity            = 0.85f,
        };
    }
}
