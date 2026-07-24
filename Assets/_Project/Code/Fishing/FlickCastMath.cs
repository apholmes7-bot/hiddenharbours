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
        /// <summary>0..1 the RANGE you wound back for: how deep the wind-back went against the full-range
        /// reference. This is the distance you were AIMING at — and exactly what the live wind-back preview
        /// showed you while you were drawing back (<see cref="FlickCastMath.WindBackCharge01"/>).</summary>
        public readonly float Range01;
        /// <summary>0..1 the SNAP of the forward sweep: how much of the range you actually delivered. A hard
        /// flick delivers all of it; a limp one falls short of what you aimed at.</summary>
        public readonly float Snap01;
        /// <summary>How far from the anchor the cast lands (m) — the aimed range, delivered by the snap,
        /// floored and capped.</summary>
        public readonly float DistanceMetres;
        /// <summary>Where the line lands: anchor + Direction · DistanceMetres.</summary>
        public readonly Vector2 LandingPoint;

        public FlickCastResult(bool isCast, Vector2 direction, float range01, float snap01,
                               float distanceMetres, Vector2 landingPoint)
        {
            IsCast = isCast;
            Direction = direction;
            Range01 = range01;
            Snap01 = snap01;
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
    /// <para><b>How the gesture is read</b> (owner's ruling 2026-07-23: "wind-back sets the range you're
    /// aiming at, and the speed of the sweep adds or costs the last stretch").
    /// <list type="bullet">
    ///   <item><b>Release</b> = the last valid sample (the caller evaluates when the hold ends).</item>
    ///   <item><b>Wind-back apex</b> = the sample FARTHEST from the release point — the deepest the mouse
    ///   was drawn back before the forward sweep, robust to curved gestures.</item>
    ///   <item><b>Direction</b> = apex → release (owner's call: the flick vector aims the cast).</item>
    ///   <item><b>Range</b> = how deep the wind-back went, against <c>FullRangeWindBackMetres</c>. Draw
    ///   back further, aim further. This is the ONE quantity that sets how far you're throwing, and it is
    ///   the same quantity <see cref="WindBackCharge01"/> is previewing live while you draw back — so what
    ///   the player watches creep out over the water is what the release resolves to.</item>
    ///   <item><b>Snap</b> = peak speed of the forward sweep against <c>FullSnapFlickSpeed</c>: it decides
    ///   how much of that aimed range you actually deliver, from <c>LimpFlickFraction01</c> (a dribbled
    ///   sweep falls well short) to all of it (a proper snap of the wrist).</item>
    ///   <item><b>Cozy fail</b> = a bad cast is just a SHORT cast — never zero, and every real cast lands
    ///   at least <c>MinCastMetres</c> out, in the water rather than on your boots.</item>
    /// </list></para>
    ///
    /// <para><b>What this replaced, and why</b> (owner playtest 2026-07-23: "casts should have distances
    /// based on the cast"). The first model set distance from sweep LENGTH+SPEED and then scaled it by
    /// WHERE past the character you let go — full only within ~1 m of the angler, dead by ~3.8 m. On a
    /// screen roughly 16 m wide every natural flick releases far outside that, so nearly every cast
    /// collapsed onto the <c>MinCastMetres</c> floor no matter how it was thrown, while the wind-back
    /// preview promised the full cap. Distance now comes from the wind-back the player can see and aim,
    /// and release POSITION carries no hidden penalty at all.</para>
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

            // ---- range: how far you WOUND BACK is how far you're aiming ------------------------------
            // The same quantity WindBackCharge01 previews live, so the release keeps the preview's promise.
            float range01 = Mathf.Clamp01(windBackDepth / SafePos(s.FullRangeWindBackMetres));

            // ---- snap: how much of that range the forward sweep actually delivers ---------------------
            float peakSpeed = PeakSweepSpeed(samples, apexIdx, releaseIdx);
            float snap01 = peakSpeed >= 0f
                ? Mathf.Clamp01(peakSpeed / SafePos(s.FullSnapFlickSpeed))
                : 1f;   // no measurable timing (degenerate timestamps) → deliver the aim, never punish it

            // ---- distance: the aimed range, delivered by the snap, floored (cozy) and capped (the gear)
            float cap = Mathf.Max(0f, Safe(castCapMetres));
            float delivered = Mathf.Lerp(Mathf.Clamp01(Safe(s.LimpFlickFraction01)), 1f, snap01);
            float floor = Mathf.Min(Mathf.Max(0f, Safe(s.MinCastMetres)), cap);
            float distance = Mathf.Clamp(range01 * delivered * cap, floor, cap);

            return new FlickCastResult(true, dir, range01, snap01, distance, a + dir * distance);
        }

        // ---- live wind-back reads (presentation only — the cast still resolves at release) ----------

        /// <summary>
        /// The LIVE wind-back charge 0..1 while the gesture is held: how far the pointer is drawn from the
        /// character, normalized by the SAME full-range wind-back the resolved cast measures
        /// (<see cref="FlickCastResult.Range01"/>) — so the castBack sheets scrub in the units the release
        /// resolves in, and the aim the player watches is the aim they get (short only by however much they
        /// under-snap the sweep). Presentation data (published as <c>FishingState.CastCharge01</c>);
        /// <see cref="Evaluate"/> alone decides the cast. Pure, NaN-safe (junk reads as 0 — the rod simply
        /// hasn't loaded).
        /// </summary>
        public static float WindBackCharge01(Vector2 pointerWorld, Vector2 anchor, float fullRangeWindBackMetres)
        {
            float dx = pointerWorld.x - anchor.x, dy = pointerWorld.y - anchor.y;
            if (float.IsNaN(dx) || float.IsNaN(dy)) return 0f;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(len / SafePos(fullRangeWindBackMetres));
        }

        /// <summary>
        /// The live AIM offset (world m, relative to the angler) of where the wound-back rod is pointing
        /// the cast: opposite the drag (the pointer is pulled BEHIND the character, the line will fly the
        /// other way), reaching <paramref name="castCapMetres"/> at full charge. This is the spot the
        /// player is aiming AT — a full-snap release lands there; an under-snapped one falls short of it
        /// (see <see cref="FlickCastResult.Snap01"/>). Published as <c>FishingState.CastAimX/Y</c>; the
        /// resolved cast still comes from the whole gesture at release. Zero when the pointer sits on the
        /// anchor (nothing is wound back) or any input is NaN. Pure.
        /// </summary>
        public static Vector2 WindBackAimOffset(Vector2 pointerWorld, Vector2 anchor,
                                                float fullRangeWindBackMetres, float castCapMetres)
        {
            float dx = anchor.x - pointerWorld.x, dy = anchor.y - pointerWorld.y;
            if (float.IsNaN(dx) || float.IsNaN(dy)) return Vector2.zero;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 1e-4f) return Vector2.zero;
            float charge = Mathf.Clamp01(len / SafePos(fullRangeWindBackMetres));
            float reach = charge * Mathf.Max(0f, Safe(castCapMetres));
            return new Vector2(dx / len * reach, dy / len * reach);
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
