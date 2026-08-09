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
    ///
    /// <para><b>And the rule generalises past the clock.</b> Some presentations are not paced by TIME at
    /// all: the deck haul's heave is keyed to the LINE hauled, so the hands stop when the rope stops and
    /// a brisk take runs them fast — a timer would drift off the rope within one pot.
    /// <see cref="FrameAt"/> is the same rule for those: the caller supplies a position along the clip
    /// in FRAMES and the clip is scaled to that quantity exactly as <see cref="FrameFor"/> scales it to
    /// a duration.</para>
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
        /// Which frame is showing at a position of <paramref name="framePosition"/> FRAMES along the
        /// clip — the answer for a presentation whose pace is a quantity rather than a clock.
        ///
        /// <para>The unit is deliberately the clip's own frames, not a 0..1 progress: it makes the
        /// caller state how far the clip has run in the clip's terms, so a rig that re-bakes the same
        /// motion at a different frame count cannot silently change the pace. 12.4 on an 8-frame
        /// looping clip is one-and-a-half cycles in, showing frame 4.</para>
        ///
        /// <para><paramref name="loops"/> wraps; a one-shot clamps to the last frame. Negative-safe and
        /// total: NaN, infinity, negatives and a position far past the end all answer with a frame in
        /// range. The arithmetic is done in <c>double</c> because a caller keyed to an unbounded
        /// quantity can hand over a position that would overflow <c>int</c> on the way through.</para>
        /// </summary>
        public static int FrameAt(float framePosition, int frameCount, bool loops)
        {
            if (frameCount <= 0) return 0;
            if (float.IsNaN(framePosition) || framePosition <= 0f) return 0;
            if (float.IsInfinity(framePosition)) return loops ? 0 : frameCount - 1;

            // Position is > 0 from here, so the cast truncates toward zero == floors. Taking the
            // modulo BEFORE the cast is what keeps a huge position off int's range, and for positive
            // values it agrees exactly with flooring first: floor(9.7) % 8 == (int)(9.7 % 8) == 1.
            double pos = framePosition;
            if (loops)
            {
                int wrapped = (int)(pos % frameCount);
                return wrapped >= frameCount || wrapped < 0 ? 0 : wrapped;   // fp belt-and-braces
            }
            return pos >= frameCount - 1 ? frameCount - 1 : (int)pos;
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
