using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The PURE presentation maths of the <b>Rod Fishing v2 line &amp; bobber reads</b>
    /// (<c>docs/design/rod-fishing-v2-brainstorm.md</c> §2.2–§2.3, §3) — the four diegetic states the
    /// LINE ITSELF must carry, because the v2 rod keeps the trap-haul language: no HUD, the line is the
    /// instrument (<c>TrapHaulMath</c>'s "the rope IS the instrument", Build 6):
    ///
    /// <list type="number">
    ///   <item><b>BOBBER DIP</b> — the surface bite tell (cast fishing, §2.1/§2.2): the bobber ducks in
    ///   two sharp taps per bite loop — the same two-tap cadence the character rig bakes into its
    ///   <c>bite</c> state ("two sharp tip-taps per loop", <c>characterIsoRig.js</c>), so the rod tip,
    ///   the fisher and the bobber all speak one rhythm. <see cref="BobberDip01"/>.</item>
    ///   <item><b>SINKING LINE</b> — the "still working" cue while the weighted rig falls (§2.3):
    ///   surface ripples pulse at the line's entry point, phased by <b>metres fallen</b> — so a heavier,
    ///   faster-falling lure visibly works faster, and counting the pulses IS counting the fall. This is
    ///   the owner's fall-time depth read (decision #4: no depth gauge — fall time + the slack tell are
    ///   the whole read). <see cref="SinkRipplePhase"/> / <see cref="RingAge01"/>.</item>
    ///   <item><b>SLACK "HIT BOTTOM"</b> — the lure bottoms out and the line goes visibly slack (§2.3:
    ///   "sitting on the floor — you <i>feel</i> bottom, no number"): the sag arrives with a small damped
    ///   overshoot so the moment POPS instead of oozing in. <see cref="SlackOvershoot"/>.</item>
    ///   <item><b>TAUT-LINE STRAIN</b> — the fight read (§3): the line straightens as she loads, whitens
    ///   LATE (the "let go!" read is reserved for near-snap, so the everyday reel stays cozy — P5), and
    ///   shudders across its length like the strained haul rope. <see cref="Whiten01"/> /
    ///   <see cref="StrainShudder01"/> / <see cref="ShudderOffset"/>.</item>
    /// </list>
    ///
    /// <para><b>Pure, side-effect-free statics</b> (the <see cref="SeaweedMath"/> /
    /// <see cref="AmbientParticleMath"/> lane pattern): every read is EditMode-testable headless, no
    /// scene, no clock, no RNG. <b>NO gameplay lives here</b> — these functions are <i>driven</i>. A
    /// later gameplay wave's <c>RodFightMath</c> supplies the 0..1 line-taut/strain and the slack/taut
    /// state; sink progress and the bite phase come from the fight phase machine (<c>FishingState</c>).
    /// This class only turns those values into what the player SEES.</para>
    ///
    /// <para><b>Art sources (per the rig contract).</b> The line and bobber are <b>runtime FX, never
    /// baked</b> — <c>docs/art/rigs/rodIsoRig.js</c> says so explicitly and exposes <c>RodIso.LINE</c> /
    /// <c>RodIso.BOBBER</c> as their colours and <c>tip()/projectLocal()</c> as their anchors. The
    /// presenter that consumes this maths draws the line as a <c>LineRenderer</c> (the trap-haul rope
    /// precedent) and reuses <c>Art/Textures/Water/SurfaceRipple.png</c> for the sink rings and
    /// <c>Art/Fishing/SplashBurst.png</c> for the strike/land surface breaks.</para>
    ///
    /// <para><b>The catenary is a deliberate twin.</b> <see cref="SampleLine"/> is the Art-lane twin of
    /// <c>TrapHaulMath.SampleHaulRope</c>, which is itself the Fishing-lane twin of
    /// <c>BoatMooring.SampleRopeCurve</c> — rule 4 forbids the cross-feature reference, so each lane
    /// carries the same trivial parabola, and every copy documents the others (as those two do).</para>
    /// </summary>
    public static class RodLineMath
    {
        // ==== the line curve (all four states draw through this) =====================================

        /// <summary>
        /// Sample the fishing line from <paramref name="rodTip"/> to <paramref name="endPoint"/> (the
        /// bobber, the surface entry point, or the fish) into <paramref name="buffer"/> as a catenary
        /// whose belly sags by <c>(1 − taut01) · maxSag</c> at the midpoint and tapers to zero at both
        /// ends — taut ⇒ straight (the fight), slack ⇒ drooping (the bottom tell). Sag droops straight
        /// down (−y). Endpoints are always pinned exactly. Pure + allocation-free (writes a caller-owned
        /// buffer), so the taut/slack curve is EditMode-testable — the twin of
        /// <c>TrapHaulMath.SampleHaulRope</c> (see the class remarks).
        /// </summary>
        /// <param name="rodTip">The rod-tip end of the line (from <c>RodIso.tip()</c> at runtime).</param>
        /// <param name="endPoint">The far end — bobber, entry point, or fish.</param>
        /// <param name="taut01">0 = fully slack (max belly) .. 1 = bar-straight. Clamped.</param>
        /// <param name="maxSag">World-metre belly at full slack (a tunable, rule 6 — never hard-code).</param>
        /// <param name="buffer">Caller-owned sample buffer; length 0/1 are safe no-ops/endpoint.</param>
        public static void SampleLine(Vector2 rodTip, Vector2 endPoint, float taut01, float maxSag,
                                      Vector2[] buffer)
        {
            int n = buffer.Length;
            if (n == 0) return;
            if (n == 1) { buffer[0] = endPoint; return; }

            float slack = 1f - Mathf.Clamp01(taut01);
            float sag = slack * Mathf.Max(0f, maxSag);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                Vector2 p = Vector2.Lerp(rodTip, endPoint, t);
                p += Vector2.down * (sag * (4f * t * (1f - t)));   // parabola peaking at the midpoint
                buffer[i] = p;
            }
        }

        // ==== 1. the bobber dip (surface bite tell) ==================================================

        /// <summary>First tap starts at this phase of the bite loop. Art direction, not balance: the two
        /// taps sit where the character rig's baked <c>bite</c> state taps its rod tip, so the bobber,
        /// tip and fisher read as ONE event. Loop ends stay at zero so the state loops cleanly.</summary>
        public const float FirstTapPhase = 0.18f;

        /// <summary>Second tap starts here — after the first has fully released.</summary>
        public const float SecondTapPhase = 0.52f;

        /// <summary>Phase width of a tap's pull-down. Short = the dip SNAPS under (a bite, not a bob).</summary>
        public const float TapAttack = 0.05f;

        /// <summary>Phase width of a tap's release — the bobber pops back slower than it ducked.</summary>
        public const float TapRelease = 0.16f;

        /// <summary>
        /// The bite-tell dip 0..1 across one bite loop: two sharp taps (fast attack, slower release),
        /// zero at both loop ends. The caller scales by its dip depth (a Def tunable) and subtracts from
        /// the bobber's y. Phase outside [0,1] is wrapped, so a looping driver can feed raw time/period.
        /// Pure, deterministic.
        /// </summary>
        public static float BobberDip01(float bitePhase01)
        {
            float u = bitePhase01 - Mathf.Floor(bitePhase01);   // wrap into [0,1)
            return Mathf.Clamp01(Tap(u, FirstTapPhase) + Tap(u, SecondTapPhase));
        }

        static float Tap(float u, float start)
        {
            float t = u - start;
            if (t <= 0f) return 0f;
            if (t <= TapAttack) return t / TapAttack;                       // snap under
            t -= TapAttack;
            if (t <= TapRelease) return 1f - t / TapRelease;                // pop back
            return 0f;
        }

        // ==== 2. the sinking line ("still working" — and the fall-time depth read) ===================

        /// <summary>
        /// The ripple phase at the line's surface entry point while the weighted rig falls: one ring per
        /// <c>1 / ripplesPerMeter</c> metres fallen. Phased by DISTANCE, not time — so a heavier lure
        /// (faster fall) pulses faster on screen with no extra plumbing, and the pulse count is a
        /// literal depth count for the player who counts (decision #4). Whole numbers are ring spawns;
        /// use <see cref="RingAge01"/> for the current ring's age. Negative inputs clamp to 0. Pure.
        /// </summary>
        /// <param name="metersFallen">How far the rig has sunk (m), from the fight phase machine.</param>
        /// <param name="ripplesPerMeter">Rings per metre of fall (a Def tunable, rule 6).</param>
        public static float SinkRipplePhase(float metersFallen, float ripplesPerMeter)
            => Mathf.Max(0f, metersFallen) * Mathf.Max(0f, ripplesPerMeter);

        /// <summary>The current ring's age 0..1 within <paramref name="ripplePhase"/> (its expand-and-fade
        /// progress — drives <c>SurfaceRipple.png</c>'s scale/alpha). Pure.</summary>
        public static float RingAge01(float ripplePhase)
            => Mathf.Max(0f, ripplePhase) - Mathf.Floor(Mathf.Max(0f, ripplePhase));

        // ==== 3. the slack "hit bottom" tell =========================================================

        /// <summary>
        /// The sag multiplier that makes the bottom-out POP: when the line suddenly slackens (the rig
        /// hit the floor), a real line bellies PAST its rest sag and settles back. Returns a multiplier
        /// on the drawn sag — 1 at the moment of slack, a damped overshoot peaking at
        /// <c>1 + overshoot01</c>-ish inside the first <paramref name="settleSeconds"/>, then settling
        /// to exactly 1 (with one tiny natural undershoot on the way). Apply to
        /// <see cref="SampleLine"/>'s <c>maxSag</c> while the bottom tell plays.
        /// <c>m(u) = 1 + overshoot01 · sin(πu) · e^(−2u)</c> with <c>u = t / settleSeconds</c>.
        /// Negative time reads as 1 (not yet slack). Pure, NaN-safe.
        /// </summary>
        /// <param name="secondsSinceSlack">Seconds since the slack state began (from the phase machine).</param>
        /// <param name="overshoot01">How far past rest the belly kicks (0..1 of the rest sag). Tunable.</param>
        /// <param name="settleSeconds">Roughly how long the kick takes to settle (&gt; 0). Tunable.</param>
        public static float SlackOvershoot(float secondsSinceSlack, float overshoot01, float settleSeconds)
        {
            if (secondsSinceSlack <= 0f) return 1f;
            float tau = Mathf.Max(1e-4f, settleSeconds);
            float u = secondsSinceSlack / tau;
            float kick = Mathf.Clamp01(overshoot01) * Mathf.Sin(Mathf.PI * u) * Mathf.Exp(-2f * u);
            return 1f + kick;
        }

        // ==== 4. the taut-line strain (the fight read) ===============================================

        /// <summary>Along-line shudder wave number — the SAME spatial frequency the strained trap-haul
        /// rope shudders at (<c>TrapHaulController</c>'s <c>i · 1.7</c>), so a straining line and a
        /// straining rope are one visual language.</summary>
        public const float ShudderWaveNumber = 1.7f;

        /// <summary>
        /// How much of the strain range shows as SHUDDER, 0..1: zero until <paramref name="strain01"/>
        /// passes <paramref name="shudderStart01"/>, then a smoothstep to 1 at full strain. The start
        /// threshold keeps the everyday fight calm (cozy, §7) and reserves the vibration for the
        /// near-snap read. Pure, monotonic.
        /// </summary>
        public static float StrainShudder01(float strain01, float shudderStart01)
        {
            float start = Mathf.Clamp01(shudderStart01);
            if (start >= 1f) return 0f;
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Clamp01(strain01) - start) / (1f - start)));
        }

        /// <summary>
        /// The perpendicular shudder displacement of sample <paramref name="index"/> of
        /// <paramref name="count"/>: a travelling wave along the line
        /// (<c>sin(phase + index · ShudderWaveNumber)</c>), scaled by <paramref name="amplitude"/> and
        /// tapered by <c>sin(πt)</c> so both ENDS stay pinned (the tip and the fish don't vibrate — the
        /// line between them does). The presenter applies it along the line's perpendicular. Pure.
        /// </summary>
        public static float ShudderOffset(int index, int count, float phase, float amplitude)
        {
            if (count < 3 || index <= 0 || index >= count - 1) return 0f;
            float t = index / (float)(count - 1);
            return Mathf.Sin(phase + index * ShudderWaveNumber) * amplitude * Mathf.Sin(t * Mathf.PI);
        }

        /// <summary>
        /// How WHITE the strained line draws, 0..1 from <paramref name="strain01"/>, biased LATE:
        /// <c>strain^lateBias</c> with <paramref name="lateBias"/> ≥ 1, so the whitening — the "she's
        /// about to part!" read — arrives only near the top of the range and an ordinary reel stays the
        /// line's own colour (cozy daily loop, §7; teeth reserved). <c>lateBias = 1</c> is the trap
        /// rope's linear shade. The caller lerps line colour → strain colour by this. Pure, monotonic.
        /// </summary>
        public static float Whiten01(float strain01, float lateBias)
            => Mathf.Pow(Mathf.Clamp01(strain01), Mathf.Max(1f, lateBias));
    }
}
