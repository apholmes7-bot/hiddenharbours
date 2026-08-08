using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The PURE maths behind clip playback — elapsed time → frame, and "is a one-shot done yet". Split
    /// out (the <see cref="IsoCharacterMath"/> / <c>PlayerHaulAnimMath</c> pattern) so the whole
    /// time→cell mapping is EditMode-testable headless. No engine state, no <c>Time</c>, no allocation.
    ///
    /// <para><b>The scaled-playback rule, in one place.</b> A clip has a natural duration (its frame
    /// count at its baked rate) and the MOVE that plays it has its own, owner-tuned duration. When the
    /// two disagree the clip is stretched or compressed to fit the move —
    /// <b>never the other way round</b>. The boarding vault is 0.55 s of owner-approved feel and the
    /// rig's <c>board</c> is 10 f × 90 ms = 0.9 s; making the vault 0.9 s to suit the art would retune a
    /// move the owner already signed off. So <paramref name="durationSeconds"/> wins whenever it is
    /// given, and the baked rate is the fallback for a caller that just wants the clip at its own
    /// speed.</para>
    /// </summary>
    public static class CharacterClipMath
    {
        /// <summary>Below this a duration is treated as "not given" — a caller asking for a zero-length
        /// clip means "play it at its own rate", not "divide by zero".</summary>
        public const float MinScaledDuration = 1e-5f;

        /// <summary>
        /// The clip's natural length in seconds: <paramref name="frameCount"/> frames at
        /// <paramref name="framesPerSecond"/>. Zero when either is zero — which
        /// <see cref="FrameFor"/> reads as "freeze on frame 0" rather than as an error.
        /// </summary>
        public static float NaturalDurationSeconds(int frameCount, float framesPerSecond)
        {
            if (frameCount <= 0 || framesPerSecond <= 0f) return 0f;
            return frameCount / framesPerSecond;
        }

        /// <summary>
        /// Which frame is showing <paramref name="elapsedSeconds"/> into a clip.
        ///
        /// <para>With a <paramref name="durationSeconds"/> above <see cref="MinScaledDuration"/> the
        /// WHOLE clip is scaled to span exactly that long, so frame 0 lands at t=0 and the last frame
        /// ends at t=duration however many frames the rig baked. Without one, the clip runs at its
        /// baked <paramref name="framesPerSecond"/>.</para>
        ///
        /// <para><paramref name="loops"/> wraps (haul, ladderDown); a one-shot CLAMPS to the last frame
        /// and holds it (board, boardDown), because a boarding clip that wrapped would put the fisher
        /// back on the wharf for a frame at the top of her own vault.</para>
        ///
        /// <para>Negative-safe and total: any input answers with a frame in range.</para>
        /// </summary>
        public static int FrameFor(float elapsedSeconds, float durationSeconds, int frameCount,
                                   float framesPerSecond, bool loops)
        {
            if (frameCount <= 0) return 0;
            if (float.IsNaN(elapsedSeconds) || elapsedSeconds <= 0f) return 0;

            int step;
            if (durationSeconds > MinScaledDuration)
            {
                // SCALED to the move: the clip spans durationSeconds exactly.
                step = Mathf.FloorToInt(elapsedSeconds / durationSeconds * frameCount);
            }
            else
            {
                if (framesPerSecond <= 0f) return 0;
                step = Mathf.FloorToInt(elapsedSeconds * framesPerSecond);
            }

            if (loops) return ((step % frameCount) + frameCount) % frameCount;
            return Mathf.Clamp(step, 0, frameCount - 1);
        }

        /// <summary>
        /// True when a ONE-SHOT clip has run out — its last frame has been shown for its full share of
        /// the playback. A looping clip is never finished, so this is always false for one.
        ///
        /// <para>The caller decides what "finished" costs: the boarding move ignores it (the ARC owns
        /// the timing and stops the clip when it lands), while a fire-and-forget one-shot uses it to
        /// hand the renderer back on its own.</para>
        /// </summary>
        public static bool IsFinished(float elapsedSeconds, float durationSeconds, int frameCount,
                                      float framesPerSecond, bool loops)
        {
            if (loops || frameCount <= 0) return false;
            float total = durationSeconds > MinScaledDuration
                ? durationSeconds
                : NaturalDurationSeconds(frameCount, framesPerSecond);
            if (total <= 0f) return true;              // nothing to play — done on arrival
            return elapsedSeconds >= total;
        }
    }
}
