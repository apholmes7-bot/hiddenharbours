using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// THE BOW AS AN IMPACT — the pure, headless-testable law behind the owner's 2026-08-27 eyeball
    /// verdict: <i>"the bow splash reads identical to the rear wake … not physics based or dynamic."</i>
    ///
    /// <para><b>What was wrong, and it is one sentence.</b> Bow and stern were the same machinery. Both
    /// drew an authored graded tier sprite pinned to the hull, both shed particles at a METERED rate
    /// keyed to nothing but speed, and both faded out the same way — so of course they read as one
    /// effect twice. A bow wave is not a stern wake at the other end of the boat: a wake is water the
    /// hull has ALREADY disturbed streaming away behind her, continuous and long-lived, while a bow
    /// splash is a COLLISION — it happens when the stem meets a face of water, it is over in a moment,
    /// and it is violent in proportion to how hard the two met.</para>
    ///
    /// <para><b>The four properties that make it an impact, and none of them is more pattern</b> (the
    /// bubble lane's doctrine, applied to the second stream it was written for):
    /// <list type="number">
    /// <item><description><b>Driven by ENCOUNTER, not speed</b> (<see cref="EncounterRate"/> /
    /// <see cref="Impact01"/>) — the signal is the rate at which the sea at the cutwater is rising
    /// RELATIVE to the hull, read through the shared displaced-sea seam. A boat driving into a steep
    /// head sea throws water; the same boat at the same speed on glass does not. That is the difference
    /// between a splash and a decal, and nothing but the sea state can supply it.</description></item>
    /// <item><description><b>BURSTY arrival</b> (<see cref="BurstCount"/>) — a Bernoulli over a handful
    /// of slots, so a wave met head-on throws a cluster and the next moment throws nothing. A metered
    /// rate is a metronome, and a metronome is machinery.</description></item>
    /// <item><description><b>HEAVY-TAILED size</b> (<see cref="SizeAt"/>) — mostly fine spray with a
    /// few big readable gouts, rather than a uniform field of identical flecks.</description></item>
    /// <item><description><b>It FALLS BACK rather than fading</b> (<see cref="AlphaAt"/> +
    /// <see cref="SizeOverLife"/>) — a droplet holds, then shrinks away fast. Deliberately unlike the
    /// foam's long fade AND unlike the bubbles' swell-and-pop: three streams that die alike read as one
    /// stream, and telling them apart is most of what "dynamic" means to an eye.</description></item>
    /// </list></para>
    ///
    /// <para><b>Determinism &amp; purity (rule 5).</b> Every function is pure and deterministic in its
    /// arguments; variation comes from a stable integer avalanche over the caller's monotonic counter,
    /// never <see cref="System.Random"/>. It reads the sim (speed, the displaced sea) and drives none of
    /// it, publishes nothing and saves nothing. <see cref="BowImpactConfig.Enabled"/> = false restores
    /// the shipped metered stream exactly (the A/B).</para>
    ///
    /// <para><b>Rule 4.</b> The sea arrives as two already-sampled heights — the caller reads them
    /// through <c>BoatWakeEmitter.SeaLift</c>, which is the Core <c>DisplacedSea</c> seam. This file
    /// knows nothing about the wave field, the animator or the shore fade, and must not learn.</para>
    /// </summary>
    public static class BowImpactMath
    {
        /// <summary>
        /// The rate (m/s) at which the sea is rising AT THE CUTWATER relative to the hull — the bow
        /// burying into a face.
        ///
        /// <para>Both heights come from the same displaced-sea read, one at the stem and one at the
        /// boat's own centre, so what is measured is the gap between them opening: the hull rising on a
        /// swell lifts both and cancels, while a face arriving at the stem does not. Negative rates
        /// (the bow coming OUT of a trough) return 0 — water is thrown when the stem goes in, not when
        /// it comes up, and a symmetric signal would splash twice per wave.</para>
        ///
        /// <para>Zero when there is no displaced sea at all (both heights 0), which is the flat-plane
        /// limit and a genuine off switch rather than an approximation of one. Pure + static.</para>
        /// </summary>
        public static float EncounterRate(float stemHeight, float hullHeight,
                                          float previousStemHeight, float previousHullHeight, float dt)
        {
            if (dt <= 0f) return 0f;
            float now = stemHeight - hullHeight;
            float before = previousStemHeight - previousHullHeight;
            return Mathf.Max(0f, (now - before) / dt);
        }

        /// <summary>
        /// How hard the bow is hitting, 0..1 — <b>speed × sea state</b>, which is the owner's ask made
        /// numeric.
        ///
        /// <para>Speed is the CARRIER: below <see cref="BowImpactConfig.SpeedThreshold"/> the stem is
        /// parting water rather than hitting it and nothing is thrown however rough the sea, because a
        /// moored boat in a chop slaps (that is the foam buffer's bob channel) but does not throw spray
        /// forward. Above it, the encounter rate LIFTS the impact toward 1 —
        /// <see cref="BowImpactConfig.SeaGain"/> sets how much of the splash the sea is allowed to own,
        /// and 0 restores a pure speed ramp for anyone who wants the calm-water look everywhere.</para>
        ///
        /// <para>The two combine as a product-of-carrier-and-lift rather than a sum, so a fast boat on
        /// glass still throws a modest bow wave while the same boat in a head sea throws several times
        /// as much — which is the ratio the eye is being asked to read. Pure + static.</para>
        /// </summary>
        public static float Impact01(float speed, float encounterRate, in BowImpactConfig cfg)
        {
            if (speed <= cfg.SpeedThreshold) return 0f;
            float span = Mathf.Max(0.01f, cfg.FullSpeed - cfg.SpeedThreshold);
            float bySpeed = Mathf.Clamp01((speed - cfg.SpeedThreshold) / span);

            float seaKnee = Mathf.Max(0.01f, cfg.SeaRateKnee);
            float bySea = Mathf.Clamp01(Mathf.Max(0f, encounterRate) / seaKnee);
            // The sea LIFTS rather than gates: calm water still throws the base splash, and a head sea
            // multiplies it. Gain 0 = the pure speed ramp, bit-exact.
            float lift = 1f + Mathf.Max(0f, cfg.SeaGain) * bySea;

            return Mathf.Clamp01(bySpeed * lift);
        }

        /// <summary>
        /// How many droplets are thrown this tick — a deterministic BERNOULLI draw, not a rate times dt.
        ///
        /// <para>Expected arrivals are <c>lambda = rate · impact · dt</c>; each of
        /// <see cref="BowImpactConfig.BurstSlots"/> independent slots fires with probability
        /// <c>lambda / slots</c>, decided by a stable hash of <paramref name="drawIndex"/> and the slot.
        /// Long-run rate: exactly the configured one. Moment to moment: uneven, occasionally a whole
        /// cluster, occasionally nothing — which is what a stem hitting a wave does, and what the
        /// shipped <c>WakeTrailMath.DropletCount</c> carry-and-emit could not do, because a carried
        /// remainder is precisely a device for making the output EVEN.</para>
        ///
        /// <para>Bounded above by <see cref="BowImpactConfig.BurstSlots"/> by construction — that bound
        /// IS the per-tick pool guard (rule 7). Returns 0 aground or at zero impact. Pure + static.</para>
        /// </summary>
        public static int BurstCount(float impact01, bool aground, in BowImpactConfig cfg, float dt,
                                     uint drawIndex)
        {
            if (aground || dt <= 0f) return 0;
            float impact = Mathf.Clamp01(impact01);
            if (impact <= 0f) return 0;

            int slots = Mathf.Max(1, cfg.BurstSlots);
            float lambda = Mathf.Max(0f, cfg.ThrowPerSecond) * impact * dt;
            float p = Mathf.Clamp01(lambda / slots);
            if (p <= 0f) return 0;

            int n = 0;
            for (int s = 0; s < slots; s++)
            {
                // One decorrelated draw per (tick, slot); the slot salt is multiplied by a large odd
                // constant so consecutive slots do not walk adjacent hash buckets. The salt differs from
                // WakeBubbleSystem.BurstCount's so the two streams never burst in lockstep — two
                // synchronised bursts read as one event, which is the defect being fixed.
                if (WakeParticleSystem.Hash01(drawIndex * 2246822519u + (uint)s * 0x85EBCA6Bu) < p) n++;
            }
            return n;
        }

        /// <summary>
        /// A droplet's birth diameter (m) from a uniform draw, biased SMALL:
        /// <c>size = min + (max − min) · u^bias</c>. With <see cref="BowImpactConfig.SizeBias"/> &gt; 1
        /// most of the throw is fine spray and the big readable gouts are rare — a splash, rather than a
        /// uniform field of identical dots. Exactly spans <c>[min, max]</c> and is monotone in u, so the
        /// owner's two size knobs still mean what they say. Pure + static.
        /// </summary>
        public static float SizeAt(float u01, in BowImpactConfig cfg)
        {
            float u = Mathf.Clamp01(u01);
            float min = Mathf.Max(0.001f, cfg.MinSize);
            float max = Mathf.Max(min, cfg.MaxSize);
            return min + (max - min) * Mathf.Pow(u, Mathf.Max(0.01f, cfg.SizeBias));
        }

        /// <summary>
        /// Opacity over life: a droplet is THERE, then it is water again.
        ///
        /// <para>Full through the first <c>1 − FallFraction</c> of its life, then down to 0 across the
        /// rest. Non-increasing, exactly 0 at life 1.</para>
        ///
        /// <para>Deliberately unlike BOTH neighbours: the foam's <c>LifeFade</c> is a fade for its whole
        /// life, and a bubble HOLDS then swells as it bursts. Three streams that die alike read as one
        /// stream — giving each its own death signature is what lets the eye separate the splash at the
        /// bow from the churn at the stern, which is the owner's complaint stated as a mechanism.</para>
        /// </summary>
        public static float AlphaAt(float life01, in BowImpactConfig cfg)
        {
            float t = Mathf.Clamp01(life01);
            float fall = Mathf.Clamp(cfg.FallFraction, 0.01f, 1f);
            float holdUntil = 1f - fall;
            if (t <= holdUntil) return 1f;
            return Mathf.Clamp01((1f - t) / fall);
        }

        /// <summary>
        /// Diameter over life: steady, then it SHRINKS back into the sea.
        ///
        /// <para>A thrown droplet keeps its birth size through the hold, then falls to
        /// <see cref="BowImpactConfig.FallShrink"/> × that size as it drops back. The opposite sign from
        /// <c>WakeBubbleSystem.SizeOverLife</c>'s pop swell, on purpose — water thrown up comes back
        /// down and gets smaller doing it, and the contrast between the two motions is what stops the
        /// bow's spray reading as the stern's bubbles. Monotone non-increasing, and floored above zero
        /// so a droplet never renders through a degenerate quad.</para>
        /// </summary>
        public static float SizeOverLife(float baseSize, float life01, in BowImpactConfig cfg)
        {
            float t = Mathf.Clamp01(life01);
            float fall = Mathf.Clamp(cfg.FallFraction, 0.01f, 1f);
            float holdUntil = 1f - fall;
            float size = Mathf.Max(0f, baseSize);
            if (t <= holdUntil) return size;
            float k = (t - holdUntil) / fall;
            return Mathf.Max(0.001f, size * Mathf.Lerp(1f, Mathf.Clamp01(cfg.FallShrink), k));
        }

        /// <summary>
        /// The velocity one droplet is thrown at: forward along the bow, fanned by
        /// <paramref name="fan"/> (−1..1 across <see cref="BowImpactConfig.FanHalfAngleDeg"/>), at a
        /// fraction of the boat's speed scaled by how hard she is hitting.
        ///
        /// <para>Scaling by the IMPACT rather than by speed alone is the whole point: the same boat at
        /// the same speed throws water further when she is burying her stem in a face than when she is
        /// slicing glass. The boat then drives PAST the droplets she threw, which is the crash read.</para>
        /// </summary>
        public static Vector2 LaunchVelocity(Vector2 bow, float speed, float fan, float impact01,
                                             in BowImpactConfig cfg)
        {
            Vector2 dir = bow.sqrMagnitude > 1e-8f ? bow.normalized : Vector2.up;
            float half = Mathf.Max(0f, cfg.FanHalfAngleDeg);
            Vector2 rayed = Quaternion.Euler(0f, 0f, Mathf.Clamp(fan, -1f, 1f) * half) * dir;
            float throwSpeed = Mathf.Max(0f, speed) * Mathf.Max(0f, cfg.SpeedScale)
                               * (0.5f + 0.5f * Mathf.Clamp01(impact01));
            return rayed * throwSpeed;
        }
    }

    /// <summary>
    /// Every tunable of the impact-driven bow splash, in one struct so the maths carries no magic
    /// numbers (rule 6). The owner edits an instance on <c>BoatWakeEmitter</c>;
    /// <see cref="Enabled"/> = false restores the shipped metered droplet stream and the authored spray
    /// sheet's companion behaviour exactly (the A/B).
    /// </summary>
    [System.Serializable]
    public struct BowImpactConfig
    {
        [Tooltip("Drive the bow from IMPACT (speed x sea state) with bursty per-droplet throws. " +
                 "Off = the shipped metered rate keyed to speed alone — the A/B.")]
        public bool Enabled;

        [Header("The impact signal")]
        [Tooltip("Speed (m/s) below which the stem parts water rather than hitting it. Nothing is " +
                 "thrown below this however rough the sea — a moored boat in a chop SLAPS (that is the " +
                 "foam buffer's bob channel) but throws no spray forward.")]
        public float SpeedThreshold;
        [Tooltip("Speed (m/s) at which the speed carrier saturates.")]
        public float FullSpeed;
        [Tooltip("Rate (m/s) at which the sea is rising at the cutwater relative to the hull for the " +
                 "sea term to saturate. Small: this is a difference of two swell heights a few metres " +
                 "apart, not a wave height.")]
        public float SeaRateKnee;
        [Tooltip("How much the sea is allowed to MULTIPLY the splash. 0 = a pure speed ramp (calm-water " +
                 "look everywhere, bit-exact); 2 = a head sea trebles what glass throws.")]
        public float SeaGain;

        [Header("Arrival — bursty, never metered")]
        [Tooltip("Droplets per second at FULL impact, in the long run. Any given tick is uneven by " +
                 "construction; this is the mean, not a metronome setting.")]
        public float ThrowPerSecond;
        [Tooltip("Independent Bernoulli slots per tick. Also the HARD per-tick cap (rule 7): more slots " +
                 "= burstier clusters and a higher ceiling.")]
        public int BurstSlots;
        [Tooltip("Half-angle (deg) of the fan the droplets are thrown into, around the bow direction.")]
        public float FanHalfAngleDeg;
        [Tooltip("Launch speed as a fraction of boat speed at full impact — thrown forward, then the " +
                 "boat drives past them.")]
        public float SpeedScale;
        [Tooltip("Scatter radius (m) about the cutwater that droplets are born in, so a burst is a " +
                 "cluster and not a stack.")]
        public float ScatterMeters;

        [Header("Each droplet's own life")]
        [Tooltip("Seconds a droplet lives before it is water again. Short — a splash is a moment.")]
        public float Lifetime;
        [Tooltip("+/- per-droplet lifetime variation, so a burst does not vanish all at once.")]
        public float LifetimeJitter;
        [Tooltip("Smallest droplet diameter (m). Most of a throw sits near this.")]
        public float MinSize;
        [Tooltip("Largest droplet diameter (m) — the rare readable gout.")]
        public float MaxSize;
        [Tooltip("Size distribution bias. ABOVE 1 pushes the mass toward MinSize, which is what gives a " +
                 "few individually readable droplets riding a haze of fine spray instead of a uniform " +
                 "field of identical dots.")]
        public float SizeBias;
        [Tooltip("Fraction of life spent falling back: the droplet holds full for the rest, then goes. " +
                 "Its death signature must differ from the foam's fade AND the bubbles' pop, or the " +
                 "three streams read as one.")]
        public float FallFraction;
        [Tooltip("Size multiplier at the end of the fall. BELOW 1 on purpose — thrown water comes back " +
                 "down and shrinks doing it, the opposite of a bubble's burst swell.")]
        public float FallShrink;
        [Tooltip("Per-second retention of a droplet's own momentum (0..1). Low: spray loses its throw " +
                 "almost at once and the sea keeps it.")]
        public float VelocityDecay;

        /// <summary>
        /// The shipped feel. <see cref="ThrowPerSecond"/> 34 over 6 slots at a 0.45 s life is a
        /// steady-state population near 15 against the 48-droplet pool, so the burstiest cluster fits
        /// several times over and the pool never grows (rule 7). <see cref="SeaGain"/> 1.6 is the number
        /// the owner will reach for first: it is how much rougher the bow looks in a head sea than on
        /// glass, and it is the whole of "dynamic, distinct from the stern".
        /// </summary>
        public static BowImpactConfig Default => new BowImpactConfig
        {
            Enabled         = true,

            SpeedThreshold  = 1.2f,    // under the dory's ~2.0 m/s rowed terminal: she throws a little
            FullSpeed       = 5.5f,
            SeaRateKnee     = 0.35f,
            SeaGain         = 1.6f,

            ThrowPerSecond  = 34f,
            BurstSlots      = 6,
            FanHalfAngleDeg = 52f,
            SpeedScale      = 0.6f,
            ScatterMeters   = 0.22f,

            Lifetime        = 0.45f,
            LifetimeJitter  = 0.35f,
            MinSize         = 0.05f,
            MaxSize         = 0.26f,
            SizeBias        = 2.4f,    // heavy-tailed: mostly fine spray, a few readable gouts
            FallFraction    = 0.35f,
            FallShrink      = 0.35f,
            VelocityDecay   = 0.10f,
        };

        /// <summary>The OFF side of the A/B: the shipped metered stream, keyed to speed alone.</summary>
        public static BowImpactConfig Off
        {
            get
            {
                BowImpactConfig c = Default;
                c.Enabled = false;
                return c;
            }
        }
    }
}
