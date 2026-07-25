using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// THE PURE MATHS OF WORKING A LURE (owner's ask 2026-07-25) — how well the player's actual hand
    /// motion matches what the tackle wants, and what that's worth.
    ///
    /// <para><b>The owner's two rules.</b> Each tackle wants its OWN action (a jig wants slow heaves, a
    /// spinner wants fast and continuous), and a lure you aren't working is <b>nearly dead — but bait
    /// still fishes</b>. So the two ways to fish became genuinely different to play: bait is patient and
    /// fishes itself while you watch the water; a lure demands your hands and rewards them.</para>
    ///
    /// <para><b>Why "nearly" and not "completely".</b> Everything else in this fishing is a weight and
    /// never a wall, and the owner kept that here too: a motionless lure keeps
    /// <see cref="JiggingSettings.DeadLureFraction01"/> of its chance, so a distracted player still
    /// occasionally gets a fish rather than staring at a system that has silently switched off. It is
    /// low enough that working the lure is obviously the point.</para>
    ///
    /// <para>Pure, deterministic, NaN-safe, engine-light (rule 5) — the measured work goes in
    /// (<see cref="JigWork"/>), a quality and a multiplier come out. Nothing here touches the world.</para>
    /// </summary>
    public static class JigMath
    {
        /// <summary>
        /// How well this working matches what the tackle wants, 0 (nothing like it / not working at all)
        /// → 1 (right tempo, right stroke).
        ///
        /// <para>Both axes must be right, so the score is the PRODUCT of the two matches rather than
        /// their average: fast little twitches are not a slow heave, and averaging would call them
        /// half-right. Each axis is scored on RATIO rather than difference, because "twice as fast as it
        /// wants" means the same thing whether the target is one stroke a second or four.</para>
        /// </summary>
        /// <param name="style">What the tackle wants. <see cref="JigStyle.None"/> — bait, and anything
        /// that fishes itself — always scores 1: there is nothing to get wrong.</param>
        public static float Quality01(JigStyle style, float strokesPerSec, float strokeMetres,
                                      in JiggingSettings s)
        {
            if (style == JigStyle.None) return 1f;

            GetTarget(style, in s, out float wantTempo, out float wantStroke);
            float tempoMatch = RatioMatch01(Safe(strokesPerSec), wantTempo, s.Tolerance01);
            float strokeMatch = RatioMatch01(Safe(strokeMetres), wantStroke, s.Tolerance01);
            return Mathf.Clamp01(tempoMatch * strokeMatch);
        }

        /// <summary>
        /// How much longer the wait for a bite becomes, given how well the lure is being worked. 1 at a
        /// perfect working; up to <c>1 / DeadLureFraction01</c> when the rod is dead — i.e. a motionless
        /// lure waits many times longer, which is "nearly dead" expressed as time rather than as a
        /// hidden dice roll the player can't see.
        ///
        /// <para>Applied to the bite delay rather than to the catch odds on purpose: the player can SEE
        /// a wait. A worked lure getting bites while a dead one doesn't is legible without a word of UI —
        /// which is the standard the whole fight is already held to.</para>
        /// </summary>
        public static float BiteDelayScale(JigStyle style, float quality01, in JiggingSettings s)
        {
            if (style == JigStyle.None) return 1f;                     // bait fishes itself; no penalty
            float dead = Mathf.Clamp(Safe(s.DeadLureFraction01), 0.001f, 1f);
            float q = Mathf.Clamp01(Safe(quality01));
            // Lerp the EFFECTIVENESS from dead→1, then invert it into a wait multiplier.
            return 1f / Mathf.Lerp(dead, 1f, q);
        }

        /// <summary>The tempo (strokes/sec) and stroke size (m) a style is asking for — the owner's five
        /// hand-feels, tuned in one place rather than restated on every asset.</summary>
        public static void GetTarget(JigStyle style, in JiggingSettings s,
                                     out float strokesPerSec, out float strokeMetres)
        {
            switch (style)
            {
                case JigStyle.LiftAndDrop: strokesPerSec = s.LiftAndDropTempo; strokeMetres = s.LiftAndDropStroke; return;
                case JigStyle.QuickJerks:  strokesPerSec = s.QuickJerksTempo;  strokeMetres = s.QuickJerksStroke;  return;
                case JigStyle.SteadySweep: strokesPerSec = s.SteadySweepTempo; strokeMetres = s.SteadySweepStroke; return;
                case JigStyle.SlowCrawl:   strokesPerSec = s.SlowCrawlTempo;   strokeMetres = s.SlowCrawlStroke;   return;
                case JigStyle.FastSteady:  strokesPerSec = s.FastSteadyTempo;  strokeMetres = s.FastSteadyStroke;  return;
                default:                   strokesPerSec = 0f;                 strokeMetres = 0f;                  return;
            }
        }

        /// <summary>
        /// Score one axis by how close <paramref name="actual"/> is to <paramref name="want"/> as a
        /// RATIO. 1 when they match, falling away either side, 0 at nothing-at-all.
        /// <paramref name="tolerance01"/> widens the band — at 0.5 you score well anywhere from about
        /// two-thirds to one-and-a-half times the target, which is forgiving enough to find by feel and
        /// tight enough that a jig worked like a spinner is obviously wrong.
        /// </summary>
        public static float RatioMatch01(float actual, float want, float tolerance01)
        {
            float a = Mathf.Max(0f, Safe(actual));
            float w = Mathf.Max(1e-4f, Safe(want));
            if (a <= 0f) return 0f;                       // a dead rod matches nothing

            // Symmetric in log-space: half the target and twice the target score the same.
            float ratio = a / w;
            float off = Mathf.Abs(Mathf.Log(ratio));      // 0 when perfect
            float tol = Mathf.Max(0.05f, Safe(tolerance01));
            return Mathf.Clamp01(1f - off / (tol * 2.2f));
        }

        /// <summary>NaN → 0 (the neutral value) — the module's standard input guard (rule 5).</summary>
        private static float Safe(float x) => float.IsNaN(x) ? 0f : x;
    }
}
