using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// A DROP-ON chimney smoke column — a thin plume of pooled puffs that rises from this transform (placed at
    /// a cottage chimney) and BENDS + drifts DOWNWIND on the SAME shared wind the grass/water/mist read, so the
    /// whole coast leans together. Unlike the ambient effects this is the one POSITIONED thing: it is dropped at
    /// the chimney and wired into <c>StPetersBuilder</c> at the cottage so a "Build St Peters" re-run shows
    /// hearth smoke (Pillar 3: a lived-in, inhabited coast).
    ///
    /// <para><b>Shared signals, seam discipline (rule 4) &amp; determinism (rule 5).</b> The downwind bend reads
    /// the shared global wind <c>_WindWorld</c> (<see cref="AmbientGlobals.Wind"/>); the look dims with the
    /// global day/night tint <c>_DayNightTint</c> — both READ-ONLY. Per-puff variation is the deterministic
    /// <see cref="AmbientParticleMath.Hash01(int,int)"/>, not <see cref="System.Random"/>. It drives no sim and
    /// saves nothing. <b>Performance (rule 7):</b> a fixed recycled pool, one shared sprite/material (batched),
    /// throttled tick — a single thin column is a handful of sprites.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChimneySmoke : MonoBehaviour
    {
        [Tooltip("Every knob of the smoke — pool, emission, rise/bend, look, day/night fade. Tune to taste; " +
                 "defaults are a thin cosy hearth plume that bends downwind.")]
        [SerializeField] private ChimneySmokeConfig _config = ChimneySmokeConfig.Default;

        [Tooltip("Local offset (m) from this transform to the actual flue mouth the smoke leaves from.")]
        [SerializeField] private Vector2 _chimneyOffset = new Vector2(0f, 0.5f);

        [Tooltip("Sorting order for the plume RELATIVE to the building it rises from — the SpriteRenderer on " +
                 "this object, else the nearest one up the parent chain. +1 reads just above the roof. " +
                 "⚠ Relative, not absolute: the building Y-SORTS, so a fixed number would sink underneath " +
                 "it (ADR 0032 re-based the decor band from 2…40 to 2…2402). With no renderer anywhere " +
                 "above, it is used as an absolute order.")]
        [SerializeField] private int _sortingOrder = 1;

        [Tooltip("How often (Hz) the smoke ticks. A slow plume reads fine at a handful of Hz.")]
        [Min(2f)] [SerializeField] private float _tickHz = 20f;

        private struct Puff
        {
            public bool Alive;
            public Vector2 Origin;
            public float Age;
            public float Lifetime;
            public float SizeJit;
            public float Seed;
        }

        private Puff[] _pool;
        private SpriteRenderer[] _renderers;
        private Sprite _sprite;
        private SpriteRenderer _reference;   // the building the plume rises from (may be null)
        private int _appliedOrder = int.MinValue;
        private float _tickTimer;
        private float _emitCarry;
        private int _emitCursor;
        private int _emitCounter;

        private void Awake()
        {
            // The building this plume belongs to. Searched from THIS object upward, so the atmosphere
            // test's chimney block (which carries its own renderer) resolves to itself and the island
            // cottage's bare chimney pivot resolves to the cottage. The pooled puffs are CHILDREN, so
            // they can never be picked up as our own reference.
            _reference = GetComponentInParent<SpriteRenderer>();
            _sprite = AmbientGlobals.BuildSoftPuff("ChimneySmoke.Puff", 24, 32, softness: 0.7f);
            BuildPool();
        }

        /// <summary>
        /// Keep the plume sitting just above the building it rises from. Re-checked on the tick rather
        /// than resolved once, because the building's order is written by a <c>YSortSprite</c> in its own
        /// <c>OnEnable</c> — and nothing orders that against this component's <c>Awake</c>. One int compare
        /// per tick when nothing has moved, which at a handful of Hz is free (rule 7).
        /// </summary>
        private void SyncSortingOrder()
        {
            int want = (_reference != null ? _reference.sortingOrder : 0) + _sortingOrder;
            if (want == _appliedOrder) return;
            _appliedOrder = want;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].sortingOrder = want;
        }

        private void OnEnable() => _tickTimer = 0f;

        private void BuildPool()
        {
            int n = Mathf.Max(1, _config.MaxPuffs);
            _pool = new Puff[n];
            _renderers = new SpriteRenderer[n];
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("puff");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _sprite;
                go.SetActive(false);
                _renderers[i] = sr;
            }
            SyncSortingOrder();   // seed it now; the tick keeps it right if the building sorts later
        }

        private void Update()
        {
            _tickTimer -= Time.deltaTime;
            if (_tickTimer > 0f) return;
            float step = _tickHz > 0f ? 1f / _tickHz : 0.05f;
            _tickTimer = step;
            Tick(step);
        }

        private void Tick(float dt)
        {
            SyncSortingOrder();
            Vector2 flue = (Vector2)transform.position + _chimneyOffset;
            Vector2 wind = AmbientGlobals.Wind;
            Color tint = AmbientGlobals.DayNightTint;
            float brightness = AmbientParticleMath.DayNightBrightness(tint);
            float dayOpacity = AmbientParticleMath.DayNightOpacity(brightness, _config.NightFade);

            // --- emit (steady rate, integrated carry; recycle round-robin) ---
            _emitCarry += Mathf.Max(0f, _config.EmitPerSecond) * dt;
            int toEmit = Mathf.FloorToInt(_emitCarry);
            if (toEmit > 0) _emitCarry -= toEmit;
            for (int k = 0; k < toEmit; k++) Emit(flue);

            // --- age + place each puff on the bent plume + render ---
            for (int i = 0; i < _pool.Length; i++)
            {
                ref Puff p = ref _pool[i];
                var sr = _renderers[i];
                if (!p.Alive)
                {
                    if (sr.gameObject.activeSelf) sr.gameObject.SetActive(false);
                    continue;
                }

                p.Age += dt;
                if (p.Age >= p.Lifetime) { p.Alive = false; sr.gameObject.SetActive(false); continue; }

                Vector2 pos = AmbientParticleMath.SmokePosition(
                    p.Origin, p.Age, _config.RiseSpeed, wind, _config.WindResponse, _config.SwayAmp, p.Seed);

                float life = AmbientParticleMath.Life01(p.Age, p.Lifetime);
                float env01 = AmbientParticleMath.LifeEnvelope(life, _config.FadeIn, _config.FadeOut);
                float size = _config.StartSize * p.SizeJit * Mathf.Lerp(1f, Mathf.Max(1f, _config.Spread), life);
                float alpha = Mathf.Clamp01(_config.MaxAlpha * env01 * dayOpacity);

                var t = sr.transform;
                t.position = new Vector3(pos.x, pos.y, 0f);
                t.localScale = new Vector3(size, size, 1f);
                Color col = _config.Color * tint;
                col.a = alpha;
                sr.color = col;
                if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
            }
        }

        private void Emit(Vector2 flue)
        {
            int i = _emitCursor;
            _emitCursor = (_emitCursor + 1) % _pool.Length;

            int salt = _emitCounter++;
            float hj = AmbientParticleMath.Hash01(salt, 13);
            float hx = AmbientParticleMath.Hash01(salt, 41);
            float hseed = AmbientParticleMath.Hash01(salt, 67);

            // a touch of horizontal jitter at the flue so the base isn't a single pixel column
            Vector2 origin = flue + new Vector2((hx - 0.5f) * 0.15f, 0f);

            _pool[i] = new Puff
            {
                Alive = true,
                Origin = origin,
                Age = 0f,
                Lifetime = Mathf.Max(0.1f, _config.Lifetime),
                SizeJit = 1f + (hj - 0.5f) * 2f * _config.SizeJitter,
                Seed = hseed,
            };
        }
    }
}
