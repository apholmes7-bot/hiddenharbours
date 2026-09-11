using System;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// <b>WHAT THE FISH HEARD</b> — the fishing side of <see cref="GullSplashed"/>, and the owner's
    /// <i>"fish react to it landing"</i> turned into something a school can be asked about.
    ///
    /// <para><b>Why a log and not a handler.</b> A signal is heard only by a listener that already
    /// exists, and it is heard once. The school a splash landed on is not a thing that can be told
    /// anything: schools are recomputed from <c>(worldSeed, gameTime)</c> on every query and hold no
    /// state between frames (rule 5). So the splash is what is remembered — a handful of them, briefly —
    /// and the draw asks "is anything scattering me?" of a set that can answer. That also means a school
    /// that streams into view a tenth of a second after the bird hit still reacts, which a
    /// fire-and-forget handler could not do.</para>
    ///
    /// <para><b>It stamps the clock itself.</b> <see cref="EventBus"/> publishes synchronously, so the
    /// moment of delivery IS the moment of the splash; taking <see cref="GameServices.Clock"/> here puts
    /// the splash and the fish on one clock — the same <c>TotalSeconds</c> the presenter draws on —
    /// rather than trusting a publisher's stamp to agree with a consumer's read. The clock may be absent
    /// (a fixture, a boot frame); zero is then the honest reading, and every splash in that world shares
    /// it.</para>
    ///
    /// <para><b>Bounded by construction</b> (rule 7): a fixed ring, no allocation after the constructor,
    /// nothing to prune. A splash older than the <c>dart</c> is simply never chosen — see
    /// <see cref="ShoalEventMath.ScatterAt"/> — so the oldest entries expire without being swept.</para>
    ///
    /// <para>⚠️ <b>The picture only.</b> Nothing here is read by the bite, the catch roll or the finder.
    /// A gull moves the fish you can SEE; what is biting is untouched, by the owner's ruling of
    /// 2026-09-09.</para>
    /// </summary>
    public sealed class GullSplashLog
    {
        /// <summary>Splashes remembered at once. A flock is 3-9 birds and a splash matters for a quarter
        /// of a second, so this is generous rather than tight.</summary>
        public const int DefaultCapacity = 8;

        private struct Entry
        {
            public Vector2 Where;
            public GullSplashKind Kind;
            public double AtSeconds;
        }

        private readonly Entry[] _ring;
        private readonly Action<GullSplashed> _handler;
        private int _next;
        private int _held;

        public GullSplashLog(int capacity = DefaultCapacity)
        {
            _ring = new Entry[Mathf.Max(1, capacity)];
            _handler = Record;
        }

        /// <summary>True between <see cref="Subscribe"/> and <see cref="Unsubscribe"/>. The listener's
        /// own account of its lifetime, so a test can state it rather than infer it.</summary>
        public bool Listening { get; private set; }

        /// <summary>How many splashes the ring is holding, capped at its capacity.</summary>
        public int Held => _held;

        /// <summary>Start hearing splashes. Idempotent: subscribing twice would double every delivery,
        /// which is exactly the bug a component with both an <c>OnEnable</c> and an explicit install
        /// would otherwise ship.</summary>
        public void Subscribe()
        {
            if (Listening) return;
            EventBus.Subscribe(_handler);
            Listening = true;
        }

        /// <summary>Stop hearing splashes. Idempotent, and safe to call after
        /// <c>EventBus.Clear&lt;GullSplashed&gt;()</c> has already dropped the handler.</summary>
        public void Unsubscribe()
        {
            if (!Listening) return;
            EventBus.Unsubscribe(_handler);
            Listening = false;
        }

        /// <summary>Forget every splash. The subscription is untouched.</summary>
        public void Clear()
        {
            _next = 0;
            _held = 0;
        }

        /// <summary>
        /// Record a splash at a time you state rather than one read off the clock — the seam a
        /// determinism test drives, and the reason the scatter can be proven twice from the same inputs
        /// without a clock in the room.
        /// </summary>
        public void RecordAt(in GullSplashed splash, double atSeconds)
        {
            _ring[_next] = new Entry
            {
                Where = splash.WorldPosition,
                Kind = splash.Kind,
                AtSeconds = atSeconds,
            };
            _next = (_next + 1) % _ring.Length;
            if (_held < _ring.Length) _held++;
        }

        private void Record(GullSplashed splash)
            => RecordAt(splash, GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0.0);

        /// <summary>
        /// Is this school bolting, and from what? The strongest splash still inside its <c>dart</c>
        /// wins — a second bird landing beside the first makes the fish run from the nearer of the two,
        /// not from the average of them.
        /// </summary>
        /// <param name="school">The school being drawn.</param>
        /// <param name="nowSeconds">The presenter's <c>GameServices.Clock.TotalSeconds</c>.</param>
        /// <param name="reachMetres">
        /// <c>GameConfig.FishSchools.GullSplashScatterRadiusMetres</c>.</param>
        /// <param name="maxStrength01">
        /// <c>GameConfig.FishSchools.GullSplashScatterStrength01</c>.</param>
        public bool TryScatter(in FishSchool school, double nowSeconds,
                               float reachMetres, float maxStrength01,
                               out float strength01, out double startSeconds, out Vector2 splashAt)
        {
            strength01 = 0f;
            startSeconds = 0.0;
            splashAt = Vector2.zero;

            for (int i = 0; i < _held; i++)
            {
                Entry e = _ring[i];

                float w = ShoalEventMath.ScatterStrength01(e.Where.x, e.Where.y,
                                                           school.Centre.x, school.Centre.y,
                                                           school.RadiusMetres, reachMetres,
                                                           maxStrength01);
                if (!ShoalEventMath.ScatterAt(w, nowSeconds - e.AtSeconds)) continue;
                if (w <= strength01) continue;

                strength01 = w;
                startSeconds = e.AtSeconds;
                splashAt = e.Where;
            }

            return strength01 > 0f;
        }
    }
}
