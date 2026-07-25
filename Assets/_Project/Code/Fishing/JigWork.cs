using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// How a piece of tackle wants to be WORKED (owner's ask 2026-07-25). Append-only, the
    /// <see cref="LureTag"/> discipline: never renumber a member, assets serialize the integer.
    ///
    /// <para>Each style is a different thing to do with your hand, which is the whole point — it is what
    /// makes owning five pieces of tackle worth something beyond five sets of numbers. The actual tempo
    /// and stroke each one wants are owner-tuned in <c>GameConfig.Jigging</c>, not baked here.</para>
    /// </summary>
    public enum JigStyle
    {
        /// <summary>Doesn't need working — bait, and anything that fishes itself. A still rod is fine.</summary>
        None = 0,
        /// <summary>Big slow heaves, then let it flutter back down. The cod jig: she takes it on the drop.</summary>
        LiftAndDrop = 1,
        /// <summary>Short, fast twitches. Mackerel feathers — you're imitating a panicking shoal of fry.</summary>
        QuickJerks = 2,
        /// <summary>A long, even sweep. The spoon wobbling steadily through the water.</summary>
        SteadySweep = 3,
        /// <summary>Slow, small movements with long pauses. Soft bait crawling along like something dying.</summary>
        SlowCrawl = 4,
        /// <summary>Quick and continuous. A spinner has to keep its blade turning or it's just a weight.</summary>
        FastSteady = 5,
    }

    /// <summary>
    /// THE WORKING OF THE ROD — measures what the player's hand is actually doing, so the fishing can
    /// tell a lure being fished from a lure hanging dead in the water (owner's ask 2026-07-25).
    ///
    /// <para><b>Why this exists.</b> Bait and tackle differed only in their numbers. Now they differ in
    /// how you PLAY them: bait fishes itself while you watch the water, and a lure only fishes if you
    /// work it. That also fills the one genuinely dead stretch of the loop — the wait for a bite, which
    /// until now was a timer counting down behind a motionless bobber.</para>
    ///
    /// <para><b>What it measures.</b> Two axes, read off the pointer's travel: <see cref="StrokesPerSec"/>
    /// (how fast you're working) and <see cref="StrokeMetres"/> (how big each stroke is). A stroke is
    /// counted at each REVERSAL of travel — the top of a lift, the bottom of a drop — which is exactly
    /// how a rod is worked, and means the read doesn't care which direction the player chooses to work
    /// in. Both decay toward zero when the hand stops, so a lure left hanging goes quiet on its own.</para>
    ///
    /// <para>Engine-light and dt-injected (the <see cref="RodFightRhythm"/> lane): no <c>Time</c>, no
    /// scene, no RNG — so a jig sequence replays exactly in an EditMode test.</para>
    /// </summary>
    public sealed class JigWork
    {
        /// <summary>Travel (m) below which a wobble isn't a stroke — stops hand-jitter and pixel noise
        /// from reading as furious jigging.</summary>
        public const float MinStrokeMetres = 0.12f;

        /// <summary>How fast the measured work fades when the hand stops (per second). Brisk: a lure
        /// that stops moving should go quiet quickly, not coast on its reputation.</summary>
        public const float DecayPerSec = 1.6f;

        /// <summary>
        /// Longest gap (s) between reversals still counted as continuous work. Past this the player has
        /// simply stopped, and the read decays honestly instead of coasting on the last stroke.
        ///
        /// <para><b>Must exceed the slowest legitimate action.</b> The first cut of this was 1.2 s —
        /// shorter than a lift-and-drop stroke (1.25 s) and far shorter than a slow crawl (2.0 s), so the
        /// two slowest styles were read as "stopped" and could never be performed at all. Anything slower
        /// than this constant is unworkable by construction, so it has to stay comfortably above the
        /// slowest tempo in <c>GameConfig.Jigging</c> — which is what
        /// <c>JiggingTests.EachTackle_IsBestServedByItsOwnAction</c> guards.</para>
        /// </summary>
        public const float StallSeconds = 3f;

        private bool _hasLast;
        private Vector2 _lastPos;
        private Vector2 _strokeStart;
        private Vector2 _travelDir;
        private float _sinceReversal;
        private float _distanceThisStroke;

        /// <summary>Strokes per second, smoothed — how FAST the rod is being worked. 0 when still.</summary>
        public float StrokesPerSec { get; private set; }

        /// <summary>Metres per stroke, smoothed — how BIG each movement is. 0 when still.</summary>
        public float StrokeMetres { get; private set; }

        /// <summary>Reset to a dead rod (a new cast, a change of tackle).</summary>
        public void Reset()
        {
            _hasLast = false;
            _sinceReversal = 0f;
            _distanceThisStroke = 0f;
            _travelDir = Vector2.zero;
            StrokesPerSec = 0f;
            StrokeMetres = 0f;
        }

        /// <summary>
        /// Feed one tick of pointer motion. <paramref name="pointer"/> is in world metres (the caller
        /// passes the pointer, or the pointer relative to the angler — either works, the read is on
        /// CHANGE). An invalid pointer decays the read rather than freezing it, so losing the mouse
        /// mid-cast reads as "stopped working", which is the honest interpretation.
        /// </summary>
        public void Tick(float dt, Vector2 pointer, bool pointerValid)
        {
            if (float.IsNaN(dt) || dt <= 0f) return;
            if (!pointerValid || float.IsNaN(pointer.x) || float.IsNaN(pointer.y)) { Decay(dt); return; }

            if (!_hasLast)
            {
                _hasLast = true;
                _lastPos = pointer;
                _strokeStart = pointer;
                return;
            }

            Vector2 step = pointer - _lastPos;
            _lastPos = pointer;
            _sinceReversal += dt;

            float stepLen = step.magnitude;
            if (stepLen > 1e-5f)
            {
                Vector2 dir = step / stepLen;
                // A REVERSAL: the hand turned back on itself. That's the top of a lift or the bottom of
                // a drop — one stroke completed.
                if (_travelDir != Vector2.zero && Vector2.Dot(dir, _travelDir) < 0f)
                {
                    float strokeLen = (pointer - _strokeStart).magnitude;
                    if (strokeLen >= MinStrokeMetres && _sinceReversal > 1e-3f
                        && _sinceReversal <= StallSeconds)
                    {
                        float rate = 1f / _sinceReversal;
                        // THE FIRST COMPLETE STROKE COUNTS IN FULL; later ones ease, so one odd stroke
                        // can't swing the read.
                        //
                        // Easing in from zero instead (the obvious way, and what this did first) means
                        // a SLOW action needs several strokes to register — a slow crawl at 2 s a stroke
                        // took about ten seconds to be recognised, while the entire wait for a bite is
                        // 1.5–4 s. The game would never once have noticed the player doing it correctly.
                        // One finished stroke is real evidence; treat it as such.
                        bool cold = StrokesPerSec < 1e-3f;
                        StrokesPerSec = cold ? rate : Mathf.Lerp(StrokesPerSec, rate, 0.5f);
                        StrokeMetres = cold ? strokeLen : Mathf.Lerp(StrokeMetres, strokeLen, 0.5f);
                    }
                    _strokeStart = pointer;
                    _sinceReversal = 0f;
                    _distanceThisStroke = 0f;
                }
                _travelDir = dir;
                _distanceThisStroke += stepLen;
            }

            // Stalled mid-stroke — the hand is resting, so the read has to fall even though no reversal
            // has happened. Without this, parking the mouse mid-lift would read as working forever.
            if (_sinceReversal > StallSeconds) Decay(dt);
        }

        private void Decay(float dt)
        {
            float k = Mathf.Exp(-DecayPerSec * dt);
            StrokesPerSec *= k;
            StrokeMetres *= k;
        }
    }
}
