using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The small splash a rod fight throws up</b> where the surface breaks: the touchdown, the float
    /// pulled under, the fish breaking, a jumper going back in, the result. One once-through flipbook on
    /// a renderer its owner made, ordered over the fisher.
    ///
    /// <para><b>Reads no clock and keeps none.</b> The owner starts it (<see cref="Begin"/>), keeps the
    /// seconds since it began, and hands them to <see cref="Render"/>; which edges throw a splash is the
    /// pure <see cref="RodPresenterMath.SplashFor"/>. The player passes its own frame-step clock, a
    /// villager passes seconds on its game-time timeline.</para>
    /// </summary>
    public sealed class RodSplashFlourish
    {
        private readonly SpriteRenderer _sr;
        private bool _active;
        private Vector2 _pos;

        public RodSplashFlourish(SpriteRenderer renderer) { _sr = renderer; }

        /// <summary>Is a splash playing (its owner's clock should run).</summary>
        public bool Active => _active;

        /// <summary>
        /// Start a splash at <paramref name="pos"/>. False (and nothing starts) with no frames to play;
        /// on true the owner restarts its splash clock at 0.
        /// </summary>
        public bool Begin(Vector2 pos, Sprite[] frames)
        {
            if (frames == null || frames.Length == 0) return false;
            _active = true;
            _pos = pos;
            return true;
        }

        /// <summary>
        /// Draw the frame <paramref name="splashSeconds"/> into the splash, or end it once the flipbook
        /// has played through. The order is written only when <paramref name="sorted"/>.
        /// </summary>
        public void Render(float splashSeconds, Sprite[] frames, float fps, bool sorted, int sortingOrder)
        {
            if (!_active) { _sr.enabled = false; return; }
            int frame = RodPresenterMath.OnceFlipFrame(splashSeconds, 1f / Mathf.Max(0.1f, fps), frames.Length);
            if (frame >= frames.Length || frames[frame] == null)
            {
                _active = false;
                _sr.enabled = false;
                return;
            }
            _sr.transform.position = new Vector3(_pos.x, _pos.y, 0f);
            // ADR 0059 step 2: the splash rides the fisher's own order, one rung in front, not a parked 51.
            if (sorted && _sr.sortingOrder != sortingOrder) _sr.sortingOrder = sortingOrder;
            _sr.sprite = frames[frame];
            _sr.enabled = true;
        }

        /// <summary>End the splash without drawing (a disable: it must not resume on the next enable).</summary>
        public void Stop() => _active = false;

        /// <summary>Take the splash out of the picture.</summary>
        public void Hide()
        {
            if (_sr != null) _sr.enabled = false;
        }
    }
}
