using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>One recorded point of the cast gesture: where the pointer was (world metres) and when
    /// (seconds since the wind-back began, from the caller's injected dt — never <c>Time</c>). Plain data
    /// so the maths below stays a pure function of its inputs.</summary>
    public readonly struct FlickSample
    {
        public readonly Vector2 Position;
        public readonly float Time;
        public FlickSample(Vector2 position, float time) { Position = position; Time = time; }
    }

    /// <summary>
    /// What a released flick gesture resolved to. <see cref="IsCast"/> false = the gesture never became a
    /// cast (no wind-back / a twitch / degenerate input) — the cozy outcome is simply "nothing flew", the
    /// player just tries again. When true, the line flies along <see cref="Direction"/> and lands at
    /// <see cref="LandingPoint"/>, <see cref="DistanceMetres"/> from the anchor.
    /// </summary>
    public readonly struct FlickCastResult
    {
        /// <summary>True = a real cast: the line flies. False = the gesture didn't amount to a cast.</summary>
        public readonly bool IsCast;
        /// <summary>Unit vector of the flick — the direction the line flies (zero when !IsCast).</summary>
        public readonly Vector2 Direction;
        /// <summary>0..1 how much of the rod's power the sweep earned (speed + length vs the full-power refs).</summary>
        public readonly float Power01;
        /// <summary>0..1 release-timing quality: 1 = let go in the sweet part of the sweep, 0 = piled line.</summary>
        public readonly float Quality01;
        /// <summary>How far from the anchor the cast lands (m) — power scaled by quality, floored and capped.</summary>
        public readonly float DistanceMetres;
        /// <summary>Where the line lands: anchor + Direction · DistanceMetres.</summary>
        public readonly Vector2 LandingPoint;

        public FlickCastResult(bool isCast, Vector2 direction, float power01, float quality01,
                               float distanceMetres, Vector2 landingPoint)
        {
            IsCast = isCast;
            Direction = direction;
            Power01 = power01;
            Quality01 = quality01;
            DistanceMetres = distanceMetres;
            LandingPoint = landingPoint;
        }

        /// <summary>The "nothing flew" result (all fields neutral).</summary>
        public static FlickCastResult NoCast => default;
    }

    /// <summary>
    /// The PURE maths of the <b>flick-cast</b> (Rod Fishing v2, design/rod-fishing-v2-brainstorm.md §2.2) —
    /// the gesture cast that replaced the old press-to-cast: wind the mouse BACK behind the character, sweep
    /// it FORWARD past them, release to let the spool loose. The Fishing-side twin of
    /// <see cref="RodFightMath"/> / <see cref="TrapHaulMath"/>: no <c>Time</c>, no RNG, no scene, nothing
    /// saved — gesture samples in (positions + caller-accumulated timestamps), a <see cref="FlickCastResult"/>
    /// out, fully EditMode-testable and NaN-safe (rule 5). Every threshold is a passed
    /// <see cref="FlickCastSettings"/> field (rule 6).
    ///
    /// <para><b>How the gesture is read.</b>
    /// <list type="bullet">
    ///   <item><b>Release</b> = the last valid sample (the caller evaluates when the hold ends).</item>
    ///   <item><b>Wind-back apex</b> = the sample FARTHEST from the release point — the deepest the mouse
    ///   was drawn back before the forward sweep, robust to curved gestures.</item>
    ///   <item><b>Direction</b> = apex → release (owner's call: the flick vector aims the cast).</item>
    ///   <item><b>Power</b> = sweep length + peak sweep speed, each normalised against its full-power
    ///   reference and blended by <c>SpeedWeight01</c>; capped at 1.</item>
    ///   <item><b>Quality (the skill beat)</b> = WHERE along the sweep you released, measured as the signed
    ///   distance of the release point past the anchor along the flick: full inside the sweet band, fading
    ///   to 0 over the falloff. Too early (still behind you) or too late = a short, piled cast.</item>
    ///   <item><b>Cozy fail</b> = a bad cast is just a SHORT cast: quality scales distance down to the
    ///   piled fraction, never to zero, and every real cast lands at least <c>MinCastMetres</c> out.</item>
    /// </list></para>
    ///
    /// <para><b>The per-gear cap seam.</b> <paramref name="castCapMetres"/> is an explicit parameter (not
    /// read off the settings) so the caller decides whose cap applies — today
    /// <see cref="FlickCastSettings.MaxCastDistanceMetres"/> from <c>GameConfig</c>, later a rod/tackle
    /// GearDef's own cap (P4 upgrades extend the reach) with no change here.</para>
    /// </summary>
    public static class FlickCastMath
    {
        /// <summary>
        /// Resolve a finished gesture (the hold was released) into a cast. Pure and deterministic: the same
        /// samples, anchor, settings and cap always produce the identical result. Samples with any NaN
        /// component are skipped; fewer than two usable samples, a sweep shorter than
        /// <c>MinFlickLengthMetres</c>, or a wind-back shallower than <c>MinWindBackMetres</c> (including a
        /// purely backwards drag — its "sweep" points away from the water, so its apex sits at the start,
        /// not behind) resolve to <see cref="FlickCastResult.NoCast"/>.
        /// </summary>
        /// <param name="samples">The gesture in chronological order; only the first <paramref name="count"/>
        /// entries are read. Null tolerated (→ NoCast).</param>
        /// <param name="count">How many entries of <paramref name="samples"/> are the gesture.</param>
        /// <param name="anchor">The character's position (world m) — the point the cast is aimed from.</param>
        /// <param name="s">The owner's gesture tuning (<see cref="GameConfig.FlickCast"/>).</param>
        /// <param name="castCapMetres">The rod/tackle cast cap (m) — the per-gear seam (see class doc).</param>
        public static FlickCastResult Evaluate(FlickSample[] samples, int count, Vector2 anchor,
                                               in FlickCastSettings s, float castCapMetres)
        {
            if (samples == null) return FlickCastResult.NoCast;
            count = Mathf.Min(count, samples.Length);

            Vector2 a = new Vector2(Safe(anchor.x), Safe(anchor.y));

            // Release = the last usable sample (skip trailing NaN junk).
            int releaseIdx = -1;
            for (int i = count - 1; i >= 0; i--)
                if (IsUsable(samples[i])) { releaseIdx = i; break; }
            if (releaseIdx < 0) return FlickCastResult.NoCast;
            Vector2 release = samples[releaseIdx].Position;

            // Wind-back apex = the usable sample farthest from the release point.
            int apexIdx = -1;
            float apexSqr = -1f;
            for (int i = 0; i < releaseIdx; i++)
            {
                if (!IsUsable(samples[i])) continue;
                float d = (samples[i].Position - release).sqrMagnitude;
                if (d > apexSqr) { apexSqr = d; apexIdx = i; }
            }
            if (apexIdx < 0) return FlickCastResult.NoCast;            // a single point is no gesture
            Vector2 apex = samples[apexIdx].Position;

            // The forward sweep: apex → release. A twitch is not a flick.
            float sweepLen = Mathf.Sqrt(apexSqr);
            if (sweepLen < Mathf.Max(1e-4f, Safe(s.MinFlickLengthMetres))) return FlickCastResult.NoCast;
            Vector2 dir = (release - apex) / sweepLen;

            // The rod must have been WOUND BACK: the apex sits behind the character along the flick. A drag
            // that only went backwards fails here (its apex is the start point, at/ahead of the anchor).
            float windBackDepth = -Vector2.Dot(apex - a, dir);
            if (windBackDepth < Safe(s.MinWindBackMetres)) return FlickCastResult.NoCast;

            // ---- power: sweep length + peak sweep speed, blended -------------------------------------
            float lengthTerm = Mathf.Clamp01(sweepLen / SafePos(s.FullPowerFlickMetres));
            float peakSpeed = PeakSweepSpeed(samples, apexIdx, releaseIdx);
            float power01;
            if (peakSpeed >= 0f)
            {
                float speedTerm = Mathf.Clamp01(peakSpeed / SafePos(s.FullPowerFlickSpeed));
                power01 = Mathf.Clamp01(Mathf.Lerp(lengthTerm, speedTerm, Mathf.Clamp01(Safe(s.SpeedWeight01))));
            }
            else
            {
                power01 = lengthTerm;   // no measurable timing (degenerate timestamps) → length carries it
            }

            // ---- quality: WHERE along the sweep the spool was released (the skill beat) --------------
            // Signed distance of the release point past the anchor, along the flick. Sweet at
            // SweetReleaseMetres past the character; full inside the window, fading over the falloff.
            float sRelease = Vector2.Dot(release - a, dir);
            float offSweet = Mathf.Max(0f, Mathf.Abs(sRelease - Safe(s.SweetReleaseMetres)) - Safe(s.SweetWindowMetres));
            float quality01 = Mathf.Clamp01(1f - offSweet / SafePos(s.QualityFalloffMetres));

            // ---- distance: power scaled by timing, floored (cozy short cast) and capped (the gear) ---
            float cap = Mathf.Max(0f, Safe(castCapMetres));
            float distance01 = power01 * Mathf.Lerp(Mathf.Clamp01(Safe(s.PiledCastFraction01)), 1f, quality01);
            float floor = Mathf.Min(Mathf.Max(0f, Safe(s.MinCastMetres)), cap);
            float distance = Mathf.Clamp(distance01 * cap, floor, cap);

            return new FlickCastResult(true, dir, power01, quality01, distance, a + dir * distance);
        }

        /// <summary>Fastest segment speed (m/s) over the forward sweep (apex → release), or −1 when no
        /// consecutive usable pair has a positive time step (timing then can't be measured).</summary>
        private static float PeakSweepSpeed(FlickSample[] samples, int fromIdx, int toIdx)
        {
            float peak = -1f;
            int prev = -1;
            for (int i = fromIdx; i <= toIdx; i++)
            {
                if (!IsUsable(samples[i])) continue;
                if (prev >= 0)
                {
                    float dt = samples[i].Time - samples[prev].Time;
                    if (dt > 0f)
                    {
                        float v = (samples[i].Position - samples[prev].Position).magnitude / dt;
                        if (v > peak) peak = v;
                    }
                }
                prev = i;
            }
            return peak;
        }

        /// <summary>A sample with any NaN component is junk — skipped entirely (rule-5 NaN safety).</summary>
        private static bool IsUsable(in FlickSample p)
            => !float.IsNaN(p.Position.x) && !float.IsNaN(p.Position.y) && !float.IsNaN(p.Time);

        /// <summary>NaN → 0 (the neutral value) — mirrors <see cref="RodFightMath"/>'s guard.</summary>
        private static float Safe(float x) => float.IsNaN(x) ? 0f : x;

        /// <summary>NaN/zero/negative → a small positive epsilon, for divisors.</summary>
        private static float SafePos(float x) => Mathf.Max(1e-4f, Safe(x));
    }
}
