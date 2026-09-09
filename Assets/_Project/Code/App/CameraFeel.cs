using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.App
{
    /// <summary>
    /// <b>THE CAMERA SPEAKS</b> (juice PR 2, owner ruling 2026-09-09 — <c>HANDOFF-2026-09-09-juice-lane.md</c>
    /// §3). The feel state of the ONE follower, kept pure: a <b>push-in</b> on a landed catch, a
    /// <b>shake</b> on a grounding, a <b>pull-back at speed</b> on open water. Three timers, two outputs
    /// (<see cref="ZoomMultiplier"/> and <see cref="ShakeOffset"/>), no engine objects, no allocation,
    /// and every amplitude, duration and threshold read from <see cref="JuiceSettings"/> (rule 6).
    ///
    /// <para><see cref="CameraFollow"/> owns the one instance, feeds it the events and
    /// <b>unscaled</b> time, and applies the outputs after its own follow and framing — see
    /// <c>CameraFollow.TickFeel</c> for the wiring and the order. This class is what the EditMode
    /// tests drive with synthetic events: the curves are static so a test can assert them at a point,
    /// and the driven state is what asserts "back to rest".</para>
    /// </summary>
    public sealed class CameraFeel
    {
        // Push-in: seconds since the catch (below zero = none live) and this push's weight-class scale.
        private float _pushElapsed = -1f;
        private float _pushScale;

        // Shake: seconds since the impact (below zero = none live) and its amplitude in metres.
        private float _shakeElapsed = -1f;
        private float _shakeAmplitude;

        // Pull-back: the fraction actually on screen, which follows its target under the slew limit.
        private float _pullBack;

        /// <summary>The live push-in as a fraction of the framing height (0 = none).</summary>
        public float PushIn { get; private set; }

        /// <summary>The live pull-back as a fraction of the framing height (0 = none).</summary>
        public float PullBack => _pullBack;

        /// <summary>The live shake, metres, to ADD to the follow position this frame.</summary>
        public Vector2 ShakeOffset { get; private set; }

        /// <summary>What the framing's orthographic size is multiplied by this frame. Exactly 1 at rest.</summary>
        public float ZoomMultiplier => (1f - PushIn) * (1f + _pullBack);

        /// <summary>True when nothing is live and both outputs are their identity — the byte-for-byte path.</summary>
        public bool AtRest
            => _pushElapsed < 0f && _shakeElapsed < 0f && PushIn == 0f && _pullBack == 0f
               && ShakeOffset.x == 0f && ShakeOffset.y == 0f;

        /// <summary>A catch has landed: start (or restart) the push-in, scaled by the fish's weight class.</summary>
        public void OnCatch(float weightKg, in JuiceSettings s)
        {
            if (s.CatchPushInFraction <= 0f) return;
            _pushScale = WeightScale(weightKg, s.CatchPushInHeavyKg, s.CatchPushInLightScale);
            _pushElapsed = 0f;
        }

        /// <summary>The hull has hit something: start a shake of <c>ImpactShakeMeters × severity</c>. A
        /// second hit during a live shake takes the LARGER amplitude and restarts the clock — two bumps
        /// never add up to more than the harder one.</summary>
        public void OnImpact(float severity01, in JuiceSettings s)
        {
            float amp = Mathf.Max(0f, s.ImpactShakeMeters) * Mathf.Clamp01(severity01);
            if (amp <= 0f || s.ImpactShakeSeconds <= 0f) return;
            _shakeAmplitude = _shakeElapsed >= 0f ? Mathf.Max(_shakeAmplitude, amp) : amp;
            _shakeElapsed = 0f;
        }

        /// <summary>Drop every live effect and both outputs to rest, immediately.</summary>
        public void Reset()
        {
            _pushElapsed = -1f; _shakeElapsed = -1f;
            PushIn = 0f; _pullBack = 0f; ShakeOffset = Vector2.zero;
        }

        /// <summary>
        /// One frame of feel. <paramref name="unscaledDt"/> is UNSCALED time (charter §3: PR 3's
        /// hit-stop must not freeze a shake mid-frame). <paramref name="openWater"/> is whether the
        /// pull-back may act at all (the helm, never the deck or the shore); off it, the pull-back slews
        /// back to nothing at the same limited rate it grew.
        /// </summary>
        public void Tick(float unscaledDt, float speedMps, float seaState01, bool openWater, in JuiceSettings s)
        {
            if (unscaledDt < 0f) unscaledDt = 0f;

            if (_pushElapsed >= 0f)
            {
                _pushElapsed += unscaledDt;
                float total = Mathf.Max(0f, s.CatchPushInSeconds) + Mathf.Max(0f, s.CatchPushOutSeconds);
                if (_pushElapsed >= total) { _pushElapsed = -1f; PushIn = 0f; }
                else PushIn = Mathf.Max(0f, s.CatchPushInFraction) * _pushScale
                              * PushInEnvelope(_pushElapsed, s.CatchPushInSeconds, s.CatchPushOutSeconds);
            }

            if (_shakeElapsed >= 0f)
            {
                _shakeElapsed += unscaledDt;
                if (_shakeElapsed >= s.ImpactShakeSeconds) { _shakeElapsed = -1f; ShakeOffset = Vector2.zero; }
                else ShakeOffset = ShakeAt(_shakeElapsed, _shakeAmplitude, s.ImpactShakeSeconds, s.ImpactShakeHz);
            }

            float target = openWater ? PullBackTarget(speedMps, seaState01, s) : 0f;
            float range = Mathf.Max(0f, s.SpeedPullBackFraction) + Mathf.Max(0f, s.SpeedPullBackSeaStateFraction);
            _pullBack = Slew(_pullBack, target, range, s.SpeedPullBackSlewSeconds, unscaledDt);
        }

        // ================= the curves — pure, and what the tests pin =================================

        /// <summary>
        /// The push-in envelope, 0..1: an ease-OUT rise to the peak over <paramref name="inSeconds"/>
        /// (fast off the mark, arriving gently — the fish is IN), then a smooth-step release over
        /// <paramref name="outSeconds"/> back to exactly 0. Zero at t ≤ 0 and at t ≥ in + out; exactly
        /// 1 at t = in. An in-time of 0 is an instant peak; an out-time of 0 is an instant release.
        /// </summary>
        public static float PushInEnvelope(float t, float inSeconds, float outSeconds)
        {
            if (t <= 0f) return 0f;
            if (inSeconds > 0f && t < inSeconds)
            {
                float u = t / inSeconds;
                return 1f - (1f - u) * (1f - u);
            }
            if (outSeconds <= 0f) return 0f;
            float v = (t - Mathf.Max(0f, inSeconds)) / outSeconds;
            if (v >= 1f) return 0f;
            return 1f - Mathf.SmoothStep(0f, 1f, v);
        }

        /// <summary>The shake's decay, 0..1: a squared fall from 1 at the hit to exactly 0 at
        /// <paramref name="seconds"/> — most of the energy is gone in the first third, the way a hull
        /// that has stopped stops shaking.</summary>
        public static float ShakeEnvelope(float t, float seconds)
        {
            if (seconds <= 0f || t >= seconds) return 0f;
            if (t <= 0f) return 1f;
            float u = 1f - t / seconds;
            return u * u;
        }

        /// <summary>The shake offset at time <paramref name="t"/> after a hit of
        /// <paramref name="amplitude"/> metres: two sines at <paramref name="hz"/> (and a non-integer
        /// relative of it, so the path never closes into a visible figure) under
        /// <see cref="ShakeEnvelope"/>. Deterministic — no random, nothing to seed, and a test can ask
        /// for the exact value at a point. Magnitude never exceeds <c>amplitude × √2 × envelope</c>.</summary>
        public static Vector2 ShakeAt(float t, float amplitude, float seconds, float hz)
        {
            float env = ShakeEnvelope(t, seconds);
            if (env <= 0f || amplitude <= 0f) return Vector2.zero;
            float w = 2f * Mathf.PI * Mathf.Max(0f, hz) * t;
            return new Vector2(Mathf.Sin(w), Mathf.Cos(w * 0.731f)) * (amplitude * env);
        }

        /// <summary>The weight-class scale of a push-in, <paramref name="lightScale"/>..1: linear in
        /// kg up to <paramref name="heavyKg"/> (a cod at the heavy mark gets the full push, a smelt the
        /// light floor). A heavy mark of 0 means every catch is a heavy one.</summary>
        public static float WeightScale(float kg, float heavyKg, float lightScale)
        {
            lightScale = Mathf.Clamp01(lightScale);
            if (heavyKg <= 0f) return 1f;
            return Mathf.Clamp(Mathf.Max(0f, kg) / heavyKg, lightScale, 1f);
        }

        /// <summary>The pull-back the boat's speed and the sea ASK for, before the slew: a smooth-step
        /// of speed between the two thresholds times the base fraction, plus the sea-state fraction
        /// scaled by the same speed term — a boat lying still in a swell is not pulled back, only a
        /// boat driving through one.</summary>
        public static float PullBackTarget(float speedMps, float seaState01, in JuiceSettings s)
        {
            float speedT = SmoothStep01(s.SpeedPullBackStartMps, s.SpeedPullBackFullMps, speedMps);
            float sea = Mathf.Clamp01(seaState01) * Mathf.Max(0f, s.SpeedPullBackSeaStateFraction);
            return speedT * (Mathf.Max(0f, s.SpeedPullBackFraction) + sea);
        }

        /// <summary>Smooth-step of <paramref name="x"/> between two thresholds, 0 at or below
        /// <paramref name="start"/>, 1 at or above <paramref name="full"/>. Thresholds authored equal
        /// or backwards are a hard step at <paramref name="full"/> — never a divide by zero.</summary>
        public static float SmoothStep01(float start, float full, float x)
        {
            if (full <= start) return x >= full ? 1f : 0f;
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x - start) / (full - start)));
        }

        /// <summary>Move <paramref name="current"/> toward <paramref name="target"/> by at most
        /// <c>fullRange / slewSeconds × dt</c> — the whole range takes <paramref name="slewSeconds"/>
        /// to cross, so a swell that changes the target every frame cannot make the view breathe. A
        /// slew of 0 (or an empty range) is no limit.</summary>
        public static float Slew(float current, float target, float fullRange, float slewSeconds, float dt)
        {
            if (slewSeconds <= 0f || fullRange <= 0f) return target;
            return Mathf.MoveTowards(current, target, fullRange / slewSeconds * Mathf.Max(0f, dt));
        }
    }
}
