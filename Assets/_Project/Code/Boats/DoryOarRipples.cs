using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>A small fixed pool of water marks that can wait their turn</b>: the spreading rings where an
    /// oar blade breaks the surface, and the drips that run off the blade on the recovery.
    ///
    /// <para>Each mark has a <see cref="Mark.Delay"/> before it shows, so a drip's ring can be laid on
    /// the frame the drip leaves the blade and still appear only when the drip lands. A mark moves on
    /// its own velocity (a falling drip) plus the tide's drift, grows from its start size to its end
    /// size with an ease-out (a ripple runs fast, then slows), and fades. Visual only: nothing here
    /// writes to the sim or the save (rule 5); the pool is built once and recycled, oldest first, so
    /// nothing allocates after construction (rule 7).</para>
    /// </summary>
    public sealed class OarWaterMarkPool
    {
        /// <summary>One mark. Plain fields in a flat array, recycled in place.</summary>
        public struct Mark
        {
            public bool Alive;
            /// <summary>World position (m).</summary>
            public Vector2 Pos;
            /// <summary>Own velocity (m/s): a drip's fall. Zero for a ring.</summary>
            public Vector2 Vel;
            /// <summary>Seconds still to wait before the mark shows and starts to age.</summary>
            public float Delay;
            /// <summary>Seconds since it showed.</summary>
            public float Age;
            /// <summary>Seconds it lives once shown.</summary>
            public float Lifetime;
            /// <summary>Radius (m) when it shows, and when it dies.</summary>
            public float StartRadius, EndRadius;
            /// <summary>Opacity at birth, 0..1 (the stroke's strength is already in it).</summary>
            public float Alpha;
            /// <summary>True while the mark is in the air (a falling drip): it does not drift on the tide.</summary>
            public bool Airborne;
        }

        private readonly Mark[] _pool;
        private int _next;

        public OarWaterMarkPool(int capacity)
        {
            _pool = new Mark[Mathf.Max(1, capacity)];
        }

        /// <summary>The backing pool, for the renderer. Do not resize.</summary>
        public Mark[] Pool => _pool;
        public int Capacity => _pool.Length;

        /// <summary>Marks alive, waiting or shown.</summary>
        public int AliveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _pool.Length; i++) if (_pool[i].Alive) n++;
                return n;
            }
        }

        /// <summary>Marks alive and past their delay: what is on screen.</summary>
        public int ShownCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _pool.Length; i++) if (IsShown(in _pool[i])) n++;
                return n;
            }
        }

        /// <summary>Lay one mark, recycling the oldest slot when the pool is full.</summary>
        public void Add(Vector2 pos, Vector2 vel, float delay, float lifetime, float startRadius,
                        float endRadius, float alpha, bool airborne)
        {
            int i = _next;
            _next = (_next + 1) % _pool.Length;
            _pool[i] = new Mark
            {
                Alive = true,
                Pos = pos,
                Vel = vel,
                Delay = Mathf.Max(0f, delay),
                Age = 0f,
                // Only a floor against a zero divide: a drip's life IS its fall, and its ring is timed
                // to that same fall, so a larger floor would carry the drip past the water.
                Lifetime = Mathf.Max(1e-3f, lifetime),
                StartRadius = Mathf.Max(0f, startRadius),
                EndRadius = Mathf.Max(0f, endRadius),
                Alpha = Mathf.Clamp01(alpha),
                Airborne = airborne,
            };
        }

        /// <summary>Step every mark by <paramref name="dt"/> under the tide's <paramref name="current"/>.</summary>
        public void Step(Vector2 current, float dt)
        {
            if (dt <= 0f) return;
            for (int i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].Alive) continue;
                _pool[i] = Advance(_pool[i], current, dt);
            }
        }

        /// <summary>
        /// One mark, one step. The delay is spent first and only the time left over ages the mark, so a
        /// ring laid with a 0.2 s wait shows on the frame it is due, whatever the frame rate. Pure.
        /// </summary>
        public static Mark Advance(Mark m, Vector2 current, float dt)
        {
            if (!m.Alive || dt <= 0f) return m;
            float t = dt;
            if (m.Delay > 0f)
            {
                if (m.Delay >= t) { m.Delay -= t; return m; }
                t -= m.Delay;
                m.Delay = 0f;
            }
            m.Pos += (m.Airborne ? m.Vel : m.Vel + current) * t;
            m.Age += t;
            if (m.Age >= m.Lifetime) m.Alive = false;
            return m;
        }

        /// <summary>True when the mark is alive and its delay is spent.</summary>
        public static bool IsShown(in Mark m) => m.Alive && m.Delay <= 0f;

        /// <summary>Normalised life, 0 at showing to 1 at death.</summary>
        public static float Life01(in Mark m) => m.Lifetime <= 0f ? 1f : Mathf.Clamp01(m.Age / m.Lifetime);

        /// <summary>
        /// Radius now: from start to end along <c>1 − (1 − life)^ease</c>, so with an ease above 1 the
        /// ring runs out quickly and slows as it spreads, as a real ripple does. Never shrinks.
        /// </summary>
        public static float Radius(in Mark m, float spreadEase)
        {
            float life = Life01(in m);
            float k = 1f - Mathf.Pow(1f - life, Mathf.Max(0.01f, spreadEase));
            return Mathf.Lerp(m.StartRadius, Mathf.Max(m.StartRadius, m.EndRadius), k);
        }

        /// <summary>Opacity now: the birth opacity times <c>1 − life^fadePower</c>. Never brightens.</summary>
        public static float Opacity(in Mark m, float fadePower)
        {
            float life = Life01(in m);
            return m.Alpha * Mathf.Clamp01(1f - Mathf.Pow(life, Mathf.Max(0.01f, fadePower)));
        }
    }

    /// <summary>
    /// <b>The ripple ring's picture</b>, as numbers: a soft annulus in a square, white with the ring in
    /// the alpha. Pure so its shape is tested headless; the emitter turns it into one shared sprite.
    /// </summary>
    public static class OarRippleTexture
    {
        /// <summary>
        /// Alpha (0..1) for a <paramref name="size"/>² ring whose band peaks at
        /// <paramref name="bandCentre01"/> of the radius and feathers to nothing
        /// <paramref name="bandHalfWidth01"/> either side. Row-major, y up.
        /// </summary>
        public static float[] RingCoverage(int size, float bandCentre01, float bandHalfWidth01)
        {
            int n = Mathf.Max(2, size);
            var a = new float[n * n];
            float c = (n - 1) * 0.5f;
            float r = n * 0.5f;
            float centre = Mathf.Clamp01(bandCentre01);
            float half = Mathf.Max(1e-3f, bandHalfWidth01);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x - c) / r, dy = (y - c) / r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float v = Mathf.Clamp01(1f - Mathf.Abs(d - centre) / half);
                a[y * n + x] = v * v * (3f - 2f * v);
            }
            return a;
        }
    }
}
