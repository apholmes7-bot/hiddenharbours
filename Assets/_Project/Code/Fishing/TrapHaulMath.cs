using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The PURE, engine-light maths of the <b>haul-by-hand</b> minigame (trap-fishing arc Build 4) — the
    /// owner's pick: <b>pull the rope in rhythm with the passing swell</b>. Split out (like
    /// <see cref="TrapSoak"/> / <c>BuoyWaveMath</c> / <c>BoatWaveMotionMath</c>) so the rhythm scoring, the
    /// swell-coupled timing window and the diegetic rope-strain read are fully EditMode-testable with no
    /// scene, no clock, no <c>Time</c> — a pure function of their inputs, no RNG, nothing saved (rule 5).
    ///
    /// <para><b>The cadence comes from the sea, not a metronome.</b> The "beat" is the passing crest of the
    /// shared deterministic wave field (<see cref="Core.WaveMath"/>) under the buoy — the SAME height read
    /// the buoy bobs on and the boat rocks to. A pull lands cleanly when it falls near the crest; the
    /// window is the swell's own phase. This class takes the <b>normalized wave phase</b> (0..1 over one
    /// swell cycle, crest at 0) so it stays engine-free; the driver converts the live
    /// <c>WaveMath.Sample(...).Height</c> under the buoy into that phase.</para>
    ///
    /// <para><b>Calm is forgiving, a big sea bites (P5 teeth).</b> The on-beat window <em>tightens</em> as
    /// the sea gets rougher: in a glassy calm the cadence is broad and easy, in a gale the crests hurry and
    /// the window is a sliver, so the rope strains and slips if you rush it. That coupling is
    /// <see cref="OnBeatWindow"/> (window shrinks with <c>seaState01</c>), and it is the direct bridge from
    /// the environment's sea state to the FEEL of the haul.</para>
    ///
    /// <para><b>No penalty (owner's M2 call).</b> A mistimed pull simply gains no line — you never lose the
    /// catch or the pot. So the score is monotonic: good pulls add line toward the surface, bad pulls do
    /// nothing. The minigame is about FEEL, not gating the reward (the catch itself is already fixed by
    /// soak + bait + seed in Build 3; this is only the ACT of retrieving it).</para>
    /// </summary>
    public static class TrapHaulMath
    {
        // ---- wave phase → beat ------------------------------------------------------------------

        /// <summary>
        /// The normalized swell phase in [0,1) from the live wave <paramref name="height"/> (m, about the
        /// tide level) and the field's height envelope <paramref name="totalAmplitude"/> (the sum of the
        /// live trains' amplitudes, <c>WaveTrains.TotalAmplitude</c>) — a crude but stable "where in the
        /// swing are we" read that peaks (phase → 0) at the crest and is farthest (→ 0.5) in the trough.
        /// Uses <c>acos(height/amp)/2π</c> folded to [0,0.5]; we do NOT need the rising/falling half to be
        /// distinguished because the beat is "hit it near the crest", symmetric about the peak. A near-glass
        /// sea (<paramref name="totalAmplitude"/> ≈ 0) has no meaningful phase → returns 0 (always "on the
        /// beat"), which is exactly the forgiving calm the design wants. Pure, deterministic, NaN-safe.
        /// </summary>
        public static float PhaseFromHeight(float height, float totalAmplitude)
        {
            if (totalAmplitude <= 1e-4f || float.IsNaN(height)) return 0f;
            float norm = Mathf.Clamp(height / totalAmplitude, -1f, 1f);
            // acos maps +amp (crest) → 0 rad and −amp (trough) → π rad; /π folds it to [0,1] where 0 = crest.
            // Halved so a full crest→trough is 0..0.5 (a single-sided distance-from-crest phase).
            return Mathf.Acos(norm) / (2f * Mathf.PI);
        }

        // ---- the swell-coupled timing window ----------------------------------------------------

        /// <summary>
        /// The half-width of the on-beat window (in phase units, 0..0.5) at a given sea state — <b>broad and
        /// forgiving in a calm, a tight sliver in a gale</b> (P5). Lerps from <paramref name="calmWindow"/>
        /// (at <c>seaState01</c> = 0) down toward <c>calmWindow · (1 − coupling)</c> (at
        /// <c>seaState01</c> = 1), where <paramref name="swellCoupling"/> in [0,1] is HOW MUCH rough seas
        /// tighten it (0 = sea state doesn't matter, the window is always <paramref name="calmWindow"/>;
        /// 1 = a full gale closes the window entirely). A pull whose phase-distance from the crest is within
        /// this window gains line. Pure; clamped so it never goes negative or exceeds the calm width.
        /// </summary>
        /// <param name="calmWindow">On-beat half-window at glassy calm (phase units, 0..0.5). The forgiving width.</param>
        /// <param name="seaState01">The continuous sea-state axis (0 glass .. 1 storm) — <c>EnvironmentSample.SeaState01</c>.</param>
        /// <param name="swellCoupling">How strongly rough seas tighten the window (0..1). The P5 knob.</param>
        public static float OnBeatWindow(float calmWindow, float seaState01, float swellCoupling)
        {
            float calm = Mathf.Clamp(calmWindow, 0f, 0.5f);
            float sea = Mathf.Clamp01(seaState01);
            float coupling = Mathf.Clamp01(swellCoupling);
            float tighten = 1f - coupling * sea;          // 1 at calm, (1−coupling) at full storm
            return Mathf.Clamp(calm * tighten, 0f, calm);
        }

        /// <summary>
        /// Is a pull at <paramref name="pullPhase"/> (0..0.5, distance-from-crest phase from
        /// <see cref="PhaseFromHeight"/>) <b>on the beat</b>? True iff it is within
        /// <paramref name="onBeatWindow"/> of the crest (phase 0). The crest is the beat; the closer to it,
        /// the cleaner the pull. Pure.
        /// </summary>
        public static bool IsOnBeat(float pullPhase, float onBeatWindow)
            => Mathf.Abs(pullPhase) <= Mathf.Max(0f, onBeatWindow);

        /// <summary>
        /// The line gained by a single pull, in [0..<paramref name="maxGainPerPull"/>] fractions of the haul.
        /// An off-beat pull gains <b>nothing</b> (owner: no penalty, just no progress). An on-beat pull gains
        /// the most dead on the crest and tapers to a floor at the window edge — so timing rewards precision
        /// without being all-or-nothing. Pure, deterministic.
        /// </summary>
        /// <param name="pullPhase">Distance-from-crest phase of the pull (0..0.5).</param>
        /// <param name="onBeatWindow">The current on-beat half-window (from <see cref="OnBeatWindow"/>).</param>
        /// <param name="maxGainPerPull">Line fraction a PERFECT (dead-on-crest) pull gains (0..1). Tunable.</param>
        /// <param name="edgeGainFraction">Fraction of <paramref name="maxGainPerPull"/> a just-in-window pull
        /// gains (0..1) — the taper floor, so a barely-on-beat pull still counts for something.</param>
        public static float LineGain(float pullPhase, float onBeatWindow, float maxGainPerPull,
                                     float edgeGainFraction)
        {
            float window = Mathf.Max(0f, onBeatWindow);
            float dist = Mathf.Abs(pullPhase);
            if (dist > window) return 0f;                 // off the beat → no line (no penalty)

            float max = Mathf.Max(0f, maxGainPerPull);
            if (window <= 1e-6f) return max;              // degenerate window → dead-on counts full

            float edge = Mathf.Clamp01(edgeGainFraction);
            float t = 1f - dist / window;                 // 1 dead-on-crest, 0 at the window edge
            return max * Mathf.Lerp(edge, 1f, t);         // taper from full (crest) to the edge floor
        }

        // ---- the diegetic rope-strain read (no HUD) ---------------------------------------------

        /// <summary>
        /// The rope <b>strain</b> 0..1 — the diegetic read the world shows in place of a HUD bar: a taut,
        /// straining rope in a big sea, a slack easy one in a calm. Strain rises with sea state (the crest
        /// hauls hard against you) and eases the more line you've already taken in (the pot's near the
        /// surface, less weight below). <c>seaState01 · (1 − line01 · relief)</c>, clamped. Pure — art keys a
        /// strain SHADE / a creak audio cue off this; it never gates the reward.
        /// </summary>
        /// <param name="seaState01">Sea state (0 glass .. 1 storm) — the swell fighting the haul.</param>
        /// <param name="line01">Haul progress 0..1 (0 on the bottom, 1 at the surface).</param>
        /// <param name="lineRelief">How much a nearly-surfaced pot relieves strain (0..1). 0 = strain is pure
        /// sea state; 1 = a fully surfaced pot has no strain regardless of sea.</param>
        public static float RopeStrain01(float seaState01, float line01, float lineRelief)
        {
            float sea = Mathf.Clamp01(seaState01);
            float line = Mathf.Clamp01(line01);
            float relief = Mathf.Clamp01(lineRelief);
            return Mathf.Clamp01(sea * (1f - line * relief));
        }

        // ---- a pure catenary for the greybox haul rope (mirrors BoatMooring.SampleRopeCurve) ----
        //
        // The haul rope is drawn in the world as the diegetic read (taut when hauling hard, drooping when
        // slack). BoatMooring owns the boat-lane mooring-rope catenary, but Fishing can't reference Boats
        // (rule 4 — no cross-feature edge), so this is the Fishing-lane twin of the same trivial parabola,
        // kept pure + static so the taut/slack curve is unit-testable here too.

        /// <summary>
        /// How <b>taut</b> the haul rope is, 0 (fully slack, drooping) → 1 (bar-tight), from the current rope
        /// strain and how hard the last pull was pulling. The haul rope goes taut on a pull and eases between
        /// pulls; higher strain holds it tauter. <c>clamp01(max(strain, pullTautness))</c>. Pure.
        /// </summary>
        public static float RopeTaut01(float strain01, float pullTautness01)
            => Mathf.Clamp01(Mathf.Max(Mathf.Clamp01(strain01), Mathf.Clamp01(pullTautness01)));

        /// <summary>
        /// Sample the haul rope from <paramref name="railPoint"/> (the boat/rail) to <paramref name="potPoint"/>
        /// (the buoy) into <paramref name="buffer"/> as a catenary whose belly sags by
        /// <c>(1 − taut01) · maxSag</c> at the midpoint and tapers to zero at both ends — taut ⇒ straight,
        /// slack ⇒ drooping (the twin of <c>BoatMooring.SampleRopeCurve</c>, inverted on taut vs slack). Sag
        /// droops straight down (−y). Pure + static (writes a caller-owned buffer, no allocation), so the
        /// greybox rope curve is EditMode-testable.
        /// </summary>
        public static void SampleHaulRope(Vector2 railPoint, Vector2 potPoint, float taut01, float maxSag,
                                          Vector2[] buffer)
        {
            int n = buffer.Length;
            if (n == 0) return;
            if (n == 1) { buffer[0] = potPoint; return; }

            float slack = 1f - Mathf.Clamp01(taut01);
            float sag = slack * Mathf.Max(0f, maxSag);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                Vector2 p = Vector2.Lerp(railPoint, potPoint, t);
                float belly = sag * (4f * t * (1f - t));   // parabola peaking at the midpoint
                p += Vector2.down * belly;
                buffer[i] = p;
            }
        }
    }
}
