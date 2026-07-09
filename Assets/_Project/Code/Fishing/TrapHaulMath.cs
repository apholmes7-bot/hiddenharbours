using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The PURE, engine-light maths of the <b>haul-with-the-swell</b> minigame (trap-fishing arc Build 4,
    /// redesigned Build 6 to the owner's "richer action, faster, diegetic" verdict). Split out (like
    /// <see cref="TrapSoak"/> / <c>BuoyWaveMath</c> / <c>BoatWaveMotionMath</c>) so the take-rate scoring, the
    /// lift/drop read and the diegetic rope-load read are fully EditMode-testable with no scene, no clock, no
    /// <c>Time</c> — a pure function of their inputs, no RNG, nothing saved (rule 5).
    ///
    /// <para><b>Haul WITH the swell, the way a real hauler does — the action is HOLD, not tap.</b> The sea does
    /// half the work. When the swell <b>LIFTS</b> the boat and pot, the rope eases and you HOLD to take line in
    /// cheaply. When the boat <b>DROPS</b> into the trough, the rope <b>loads up</b> — holding through the drop
    /// strains the line and <b>slips it back</b>. So the play is <b>hold on the lift, ease on the fall</b>:
    /// continuous engagement, physically true, and it reads straight off the shared wave field. The lift/drop
    /// is <see cref="LiftSignal"/> (the surface's vertical velocity under the buoy, signed and normalized); the
    /// take is <see cref="HoldLineRate"/>.</para>
    ///
    /// <para><b>Calm is forgiving and quick, a big sea is a fight (P5 teeth).</b> In a glassy calm there is no
    /// swell to work, so holding just winds line in steadily at <c>calmHaulRate</c> — a fast, timing-free haul.
    /// As the sea builds, the swell takes over (the <c>swellCoupling</c> knob): you gain FAST on the lift but
    /// the rope SLIPS on the drop, so a clean haul (hold the lifts, ease the falls) lands the pot far quicker
    /// than a sloppy one (hold through everything and the sea gives you nothing). That coupling is the bridge
    /// from the environment's sea state to the FEEL of the haul.</para>
    ///
    /// <para><b>Cozy — the rope fights back, but never bites (owner's M2 call).</b> Missing the phase slips
    /// line back (real tension, a genuine fail-feel) but costs only TIME — you never lose the catch, the pot,
    /// or take damage. The catch itself is already fixed by soak + bait + seed in Build 3; this is only the
    /// ACT of retrieving it.</para>
    /// </summary>
    public static class TrapHaulMath
    {
        /// <summary>Below this total amplitude (metres) the sea is dead glass and the lift read is exactly 0
        /// (guards the 0/0 of normalizing a height rate by the amplitude envelope). A guard, not a tunable —
        /// mirrors <c>Core.WaveMath.GlassAmplitudeMeters</c> so the Fishing lane stays engine-light.</summary>
        public const float GlassAmplitudeMeters = 1e-6f;

        // ---- the lift/drop read (the swell's vertical velocity under the buoy) ------------------

        /// <summary>
        /// The signed, normalized <b>lift signal</b> in [−1, +1] from the surface's vertical motion under the
        /// buoy: <b>+1 = lifting hard</b> (the rope eases — HOLD to take line), <b>−1 = dropping hard</b> (the
        /// rope loads — ease off or slip). Built from the finite difference of the sampled wave
        /// <paramref name="height"/> vs the <paramref name="previousHeight"/> a tick ago, normalized by the
        /// field's height envelope <paramref name="totalAmplitude"/> (so a bigger sea reads fuller) and by the
        /// <paramref name="referenceRate"/> (normalized-height-per-second that counts as a full ±1 heave), then
        /// clamped. A near-glass sea (envelope ≈ 0) has no meaningful swell → returns 0 (the forgiving calm the
        /// design wants — <see cref="HoldLineRate"/> then falls to the steady wind-in). Pure, NaN-safe.
        /// </summary>
        /// <param name="height">The wave surface height (m about tide level) under the buoy right now.</param>
        /// <param name="previousHeight">The same read one tick ago.</param>
        /// <param name="totalAmplitude">The field's height envelope (<c>WaveTrains.TotalAmplitude</c>, m).</param>
        /// <param name="deltaSeconds">Game-time step between the two reads (s). ≤ 0 → no signal (0).</param>
        /// <param name="referenceRate">Normalized-height rate (per second) that reads as a full ±1 lift. A
        /// shaping knob for the read, not the take amount: lower = the swell reads "fully lifting" sooner.</param>
        public static float LiftSignal(float height, float previousHeight, float totalAmplitude,
                                       float deltaSeconds, float referenceRate)
        {
            if (deltaSeconds <= 1e-5f) return 0f;
            if (totalAmplitude <= GlassAmplitudeMeters) return 0f;   // glass → no swell → no lift
            if (float.IsNaN(height) || float.IsNaN(previousHeight)) return 0f;
            float refRate = Mathf.Max(1e-5f, referenceRate);
            float normRate = ((height - previousHeight) / totalAmplitude) / deltaSeconds;
            return Mathf.Clamp(normRate / refRate, -1f, 1f);
        }

        // ---- the take rate while holding (the heart of the action) ------------------------------

        /// <summary>
        /// Line taken IN per second while the player is <b>holding</b>, given the current <paramref name="lift"/>
        /// signal and the <paramref name="seaState01"/>. Blends the two ways line comes aboard:
        /// <list type="bullet">
        ///   <item><b>Calm — the steady wind-in.</b> With no swell to work, holding takes line at
        ///   <paramref name="calmHaulRate"/> regardless of timing: a quick, forgiving haul.</item>
        ///   <item><b>Rough — the swell does the work.</b> Line comes at <c>swellTakeRate · lift</c>: fast on
        ///   the LIFT (<paramref name="lift"/> &gt; 0) and <b>NEGATIVE on the DROP</b> (<paramref name="lift"/>
        ///   &lt; 0 — the rope slips back, the fight).</item>
        /// </list>
        /// The blend is by <c>seaState01 · swellCoupling</c>, so <paramref name="swellCoupling"/> in [0,1] is
        /// the P5 knob for HOW MUCH rough seas take over (0 = always the forgiving wind-in, the sea never bites;
        /// 1 = a full gale is pure swell timing). Pure, deterministic. Return is a signed rate — the caller
        /// clamps the accumulated line to [0,1].
        /// </summary>
        /// <param name="lift">Signed lift signal −1..+1 (from <see cref="LiftSignal"/>).</param>
        /// <param name="seaState01">Continuous sea-state axis 0 (glass) .. 1 (storm) — <c>EnvironmentSample.SeaState01</c>.</param>
        /// <param name="swellCoupling">How strongly rough seas take over the take (0..1). The P5 knob.</param>
        /// <param name="calmHaulRate">Line/second the steady wind-in takes in a glassy calm (≥ 0).</param>
        /// <param name="swellTakeRate">Line/second at a full lift in a big sea; also the slip rate on a full drop (≥ 0).</param>
        public static float HoldLineRate(float lift, float seaState01, float swellCoupling,
                                         float calmHaulRate, float swellTakeRate)
        {
            float sea = Mathf.Clamp01(seaState01) * Mathf.Clamp01(swellCoupling);
            float l = Mathf.Clamp(lift, -1f, 1f);
            float calm = Mathf.Max(0f, calmHaulRate);
            float swell = Mathf.Max(0f, swellTakeRate) * l;   // signed: + on the lift, − on the drop
            return Mathf.Lerp(calm, swell, sea);
        }

        // ---- the diegetic rope-load read (no HUD — the rope is the instrument) -------------------

        /// <summary>
        /// How <b>loaded / taut</b> the rope reads from the swell phase alone, <b>before the player acts</b> —
        /// the primary diegetic instrument (owner: the rope must make the correct moment obvious first). It is
        /// <b>slack on the LIFT</b> (the sea's easing the rope — take now) and <b>taut on the DROP</b> (the sea's
        /// loading the rope — ease off), scaled by <paramref name="seaState01"/> so a calm always reads relaxed.
        /// 0 = slack (drooping), 1 = bar-taut. Pure. Shown whether or not the player is holding, so the drop can
        /// be read coming.
        /// </summary>
        public static float SwellRopeLoad01(float lift, float seaState01)
        {
            float drop = Mathf.Clamp01(-Mathf.Clamp(lift, -1f, 1f));   // > 0 only on the fall
            return Mathf.Clamp01(drop * Mathf.Clamp01(seaState01));
        }

        /// <summary>
        /// The active <b>fight-strain</b> target 0..1 — how hard the player is FIGHTING the rope right now: high
        /// only when <b>holding THROUGH a drop</b> (loading the line against the falling swell), zero when
        /// lifting, slack, or not holding. This is what shudders and whitens the rope (the "let go!" read) and
        /// what audio voices as a strain groan — distinct from the always-on <see cref="SwellRopeLoad01"/>
        /// (which just shows the phase). Pure.
        /// </summary>
        public static float FightStrain01(bool holding, float lift, float seaState01)
            => holding ? SwellRopeLoad01(lift, seaState01) : 0f;

        /// <summary>
        /// The ambient rope <b>strain</b> 0..1 — the "how heavy is she" baseline the rope shade sits at between
        /// fights: rises with sea state (a big sea holds the rope taut) and eases the more line you've already
        /// taken in (the pot's near the surface, less weight below). <c>seaState01 · (1 − line01 · relief)</c>,
        /// clamped. Pure — the displayed strain is the max of this and the active <see cref="FightStrain01"/>.
        /// </summary>
        /// <param name="seaState01">Sea state (0 glass .. 1 storm) — the swell's weight on the haul.</param>
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
        // The haul rope is drawn in the world as the diegetic read (taut when the swell loads it, drooping when
        // it eases). BoatMooring owns the boat-lane mooring-rope catenary, but Fishing can't reference Boats
        // (rule 4 — no cross-feature edge), so this is the Fishing-lane twin of the same trivial parabola,
        // kept pure + static so the taut/slack curve is unit-testable here too.

        /// <summary>
        /// How <b>taut</b> the haul rope is, 0 (fully slack, drooping) → 1 (bar-tight), as the higher of the
        /// swell's current load on it and how hard the player is fighting it. <c>clamp01(max(load, fight))</c>.
        /// Pure.
        /// </summary>
        public static float RopeTaut01(float swellLoad01, float fightStrain01)
            => Mathf.Clamp01(Mathf.Max(Mathf.Clamp01(swellLoad01), Mathf.Clamp01(fightStrain01)));

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
