using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The PULL-BACK strike's pure maths (owner §10.2: "pull back and press maybe?" — this is the
    /// pull-back half; the press half is the plain action edge). A yank is pointer motion AWAY from
    /// the line's far end — the mirror of the flick-cast's forward sweep: you cast by sweeping toward
    /// the water, you strike by hauling back from it. Pure + static (the FlickCastMath discipline);
    /// the stateful accumulator is <see cref="StrikeYank"/>.
    /// </summary>
    public static class StrikeGestureMath
    {
        /// <summary>
        /// The BACKWARD speed (m/s) of a pointer step: the component of its velocity along the
        /// far-end→angler axis. Positive = hauling back (away from the bobber, toward/behind the
        /// angler); ≤ 0 = drifting toward the water — not a strike. Degenerate spans (dt ≤ 0, the
        /// angler ON the far end) read 0, never NaN.
        /// </summary>
        public static float BackwardSpeed(Vector2 prevPointer, Vector2 pointer, float dt,
                                          Vector2 angler, Vector2 farEnd)
        {
            if (dt <= 0f) return 0f;
            Vector2 axis = angler - farEnd;
            float len = axis.magnitude;
            if (len < 1e-4f) return 0f;
            return Vector2.Dot((pointer - prevPointer) / dt, axis / len);
        }
    }

    /// <summary>
    /// One live pull-back accumulator — engine-light, allocation-free, owned by the controller for
    /// the duration of a bite sequence. Feed it the pointer each tick; it reports the yank EDGE the
    /// tick the gesture completes (enough backward travel, fast enough, without stalling). The edge
    /// is then judged by the ONE <see cref="BiteSequenceSim"/> exactly like a press — the gesture
    /// never re-computes what phase the bite is in (the flick-cast lesson).
    /// </summary>
    public sealed class StrikeYank
    {
        private Vector2 _prevPointer;
        private bool _hasPrev;
        private float _travelM;   // backward metres banked at ≥ the qualifying speed

        /// <summary>Forget everything (a new sequence, a completed yank, a broken gesture).</summary>
        public void Reset()
        {
            _hasPrev = false;
            _travelM = 0f;
        }

        /// <summary>
        /// Advance one tick. True EXACTLY on the tick the yank completes (then self-resets, so a
        /// held drag can't fire twice). A stall — backward speed under the settings' stall fraction
        /// of the qualifying speed — breaks the gesture and the travel is forfeit: a yank is one
        /// committed haul, not a slow wander.
        /// </summary>
        public bool Tick(float dt, Vector2 pointerWorld, bool pointerValid,
                         Vector2 angler, Vector2 farEnd, in StrikeSettings s)
        {
            if (!pointerValid) { Reset(); return false; }
            if (!_hasPrev) { _prevPointer = pointerWorld; _hasPrev = true; return false; }

            float backSpeed = StrikeGestureMath.BackwardSpeed(_prevPointer, pointerWorld, dt, angler, farEnd);
            _prevPointer = pointerWorld;

            float minSpeed = Mathf.Max(0.01f, s.YankMinSpeedMps);
            if (backSpeed >= minSpeed)
            {
                _travelM += backSpeed * dt;
                if (_travelM >= Mathf.Max(0.01f, s.YankMinMetres))
                {
                    _travelM = 0f;
                    return true;
                }
            }
            else if (backSpeed < minSpeed * Mathf.Clamp01(s.YankStallFraction01))
            {
                _travelM = 0f;   // the haul stalled (or reversed) — the gesture is broken
            }
            return false;
        }
    }
}
