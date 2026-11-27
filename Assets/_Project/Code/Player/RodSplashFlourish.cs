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
        private int _first;

        public RodSplashFlourish(SpriteRenderer renderer) { _sr = renderer; }

        /// <summary>Is a splash playing (its owner's clock should run).</summary>
        public bool Active => _active;

        /// <summary>Where the playing splash is (world m): its owner sorts it by which side of the fisher it is.</summary>
        public Vector2 Position => _pos;

        /// <summary>
        /// Start a splash at <paramref name="pos"/>. False (and nothing starts) with no frames to play;
        /// on true the owner restarts its splash clock at 0. <paramref name="firstFrame"/> starts the flipbook
        /// part-way (a thrashing fish's churn plays only the open rings, never the column and crown of a break),
        /// kept inside the flipbook.
        /// </summary>
        public bool Begin(Vector2 pos, Sprite[] frames, int firstFrame = 0)
        {
            if (frames == null || frames.Length == 0) return false;
            _active = true;
            _pos = pos;
            _first = Mathf.Clamp(firstFrame, 0, frames.Length - 1);
            return true;
        }

        /// <summary>
        /// Draw the frame <paramref name="splashSeconds"/> into the splash, or end it once the flipbook
        /// has played through. The order is written only when <paramref name="sorted"/>.
        /// </summary>
        public void Render(float splashSeconds, Sprite[] frames, float fps, bool sorted, int sortingOrder)
        {
            if (!_active) { _sr.enabled = false; return; }
            int first = Mathf.Clamp(_first, 0, frames.Length - 1);
            int frame = first + RodPresenterMath.OnceFlipFrame(splashSeconds, 1f / Mathf.Max(0.1f, fps), frames.Length - first);
            if (frame >= frames.Length || frames[frame] == null)
            {
                _active = false;
                _sr.enabled = false;
                return;
            }
            _sr.transform.position = new Vector3(_pos.x, _pos.y, 0f);
            // The order is the owner's (EffectSorting.RodSplashOrder: by the side of the fisher it lands on).
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
