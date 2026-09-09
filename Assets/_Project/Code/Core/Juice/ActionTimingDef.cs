using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Anticipation and follow-through as TIMING (juice charter §4.3). The rig frames already exist;
    /// the presenters played them at a flat cadence. This Def gives one action four numbers the
    /// owner tunes without code: a <b>pre-hold</b> on the anticipation frame, a fast <b>strike</b>
    /// across the middle frames, a <b>follow-through</b> hold on the last frame, and a <b>settle</b>
    /// before the walk skin comes back. No new frames, no new sheets — the same pictures, timed.
    ///
    /// <para>One asset per action under <c>Resources/ActionTiming/</c> (<c>CastTiming</c>,
    /// <c>HaulTiming</c>, <c>DigTiming</c>); presenters take a serialized reference first and fall
    /// back to <c>Resources.Load</c> so a scene that was never re-wired still reads the Def. A missing
    /// Def is the flat cadence the presenter shipped with, never a throw.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Juice/Action Timing", fileName = "ActionTiming")]
    public sealed class ActionTimingDef : ScriptableObject
    {
        [Tooltip("Stable id, `timing.<action>` (timing.cast, timing.haul, timing.dig).")]
        public string Id;

        [Tooltip("Seconds the FIRST frame (the anticipation — the rod drawn back, the shovel raised) is " +
                 "held before the strike begins. 0 = no anticipation hold.")]
        [Min(0f)] public float PreHoldSeconds;

        [Tooltip("Seconds the strike takes to sweep from the first frame to the last. Short = snappy. " +
                 "0 = a hard cut to the last frame.")]
        [Min(0f)] public float StrikeSeconds;

        [Tooltip("Seconds the LAST frame (the follow-through — the rod out, the shovel in the sand) holds " +
                 "after the strike lands, before the settle.")]
        [Min(0f)] public float FollowThroughSeconds;

        [Tooltip("Seconds of settle after the follow-through, still on the last frame, before the action " +
                 "is done and the walk skin may come back.")]
        [Min(0f)] public float SettleSeconds;

        /// <summary>The value copy the maths reads — a presenter can hold this and never touch the asset again.</summary>
        public ActionTiming Timing => new ActionTiming(PreHoldSeconds, StrikeSeconds, FollowThroughSeconds, SettleSeconds);
    }

    /// <summary>The four numbers of an <see cref="ActionTimingDef"/> as a value — what the pure maths reads.</summary>
    public readonly struct ActionTiming
    {
        public readonly float PreHoldSeconds;
        public readonly float StrikeSeconds;
        public readonly float FollowThroughSeconds;
        public readonly float SettleSeconds;

        public ActionTiming(float preHold, float strike, float followThrough, float settle)
        {
            PreHoldSeconds = Mathf.Max(0f, preHold);
            StrikeSeconds = Mathf.Max(0f, strike);
            FollowThroughSeconds = Mathf.Max(0f, followThrough);
            SettleSeconds = Mathf.Max(0f, settle);
        }

        /// <summary>The whole action, first frame to done.</summary>
        public float TotalSeconds => PreHoldSeconds + StrikeSeconds + FollowThroughSeconds + SettleSeconds;

        /// <summary>The flat cadence: no holds, the strike across the whole run. Used when no Def is wired.</summary>
        public static ActionTiming Flat(float strikeSeconds) => new ActionTiming(0f, strikeSeconds, 0f, 0f);
    }

    /// <summary>Which beat of an action the clock is in.</summary>
    public enum ActionBeat { PreHold, Strike, FollowThrough, Settle, Done }

    /// <summary>
    /// The PURE frame law behind an <see cref="ActionTimingDef"/>: no <c>Time</c>, no state — a
    /// function of the seconds elapsed, the four numbers and the frame count. EditMode-tested
    /// (charter §4 acceptance: "EditMode tests on every curve").
    /// </summary>
    public static class ActionTimingMath
    {
        /// <summary>The beat at <paramref name="elapsed"/> seconds into the action.</summary>
        public static ActionBeat BeatAt(float elapsed, in ActionTiming t)
        {
            float e = Mathf.Max(0f, elapsed);
            float edge = t.PreHoldSeconds;
            if (e < edge) return ActionBeat.PreHold;
            edge += t.StrikeSeconds;
            if (e < edge) return ActionBeat.Strike;
            edge += t.FollowThroughSeconds;
            if (e < edge) return ActionBeat.FollowThrough;
            edge += t.SettleSeconds;
            if (e < edge) return ActionBeat.Settle;
            return ActionBeat.Done;
        }

        /// <summary>
        /// The frame to draw at <paramref name="elapsed"/> seconds: frame 0 through the pre-hold, the
        /// frames 1..N−1 swept evenly across the strike (the LAST frame is reached exactly as the strike
        /// ends), then the last frame held through the follow-through and the settle and beyond. A zero
        /// strike is a hard cut. Count ≤ 0 returns 0; a one-frame sheet is always frame 0.
        /// </summary>
        public static int FrameFor(float elapsed, in ActionTiming t, int frameCount)
        {
            if (frameCount <= 1) return 0;
            float e = Mathf.Max(0f, elapsed);
            if (e < t.PreHoldSeconds) return 0;
            int last = frameCount - 1;
            float s = e - t.PreHoldSeconds;
            if (t.StrikeSeconds <= 0f || s >= t.StrikeSeconds) return last;
            // 1 + ⌊u·last⌋ sweeps 1..last across the strike; the clamp keeps a float rounding on the
            // last tick from asking for frame N.
            int idx = 1 + Mathf.FloorToInt(s / t.StrikeSeconds * last);
            return Mathf.Clamp(idx, 1, last);
        }

        /// <summary>0 at the start of the strike, 1 when the last frame lands, clamped either side (the
        /// splash/spark presenters key their burst off this crossing 1).</summary>
        public static float Strike01(float elapsed, in ActionTiming t)
        {
            float s = Mathf.Max(0f, elapsed) - t.PreHoldSeconds;
            if (s <= 0f) return 0f;
            if (t.StrikeSeconds <= 0f) return 1f;
            return Mathf.Clamp01(s / t.StrikeSeconds);
        }

        /// <summary>True once the action has run its whole length (pre-hold + strike + follow-through + settle).</summary>
        public static bool IsDone(float elapsed, in ActionTiming t) => Mathf.Max(0f, elapsed) >= t.TotalSeconds;
    }
}
