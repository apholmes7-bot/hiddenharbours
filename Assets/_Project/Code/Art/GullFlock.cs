using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// SELF-INSTALLING gulls — a FEW gull sprites that wheel across the sky on looping, varied paths,
    /// occasionally (not constantly), riding the SAME shared breeze the rest of the coast reads. The cosy
    /// "living working coast" touch (Pillar 3): a couple of birds turning lazy circles over the harbour make
    /// St Peters feel alive without a flock of clutter.
    ///
    /// <para><b>Self-installing (mirrors <see cref="GrassWindBridge"/>).</b> A
    /// <see cref="RuntimeInitializeOnLoadMethod"/> spawns ONE hidden persistent host, so the gulls appear in
    /// every scene with no wiring. Each gull's loop is centred on the active camera so a few birds always wheel
    /// over what the player sees.</para>
    ///
    /// <para><b>Shared signals (cohesion), seam discipline (rule 4) &amp; determinism (rule 5).</b> The loops
    /// skew downwind on the shared global wind <c>_WindWorld</c> (<see cref="AmbientGlobals.Wind"/>); the look
    /// dims with the global day/night tint <c>_DayNightTint</c> (gulls roost at night) — both READ-ONLY. Each
    /// gull's radius/period/phase/active-window is a deterministic <see cref="AmbientParticleMath.Hash01(int,int)"/>
    /// per bird, never <see cref="System.Random"/>. The flight position is the pure
    /// <see cref="AmbientParticleMath.GullPosition"/>, evaluated from <c>Time</c> (cosmetic motion is allowed to
    /// read the clock). Drives no sim, saves nothing. <b>Performance (rule 7):</b> a handful of fixed sprites,
    /// one shared sprite/material (batched), throttled.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GullFlock : MonoBehaviour
    {
        [Tooltip("Every knob of the gulls — count, loop area/radius/period, how OFTEN they're on-screen, look, " +
                 "day/night fade, downwind drift. Tune to taste; defaults are a few birds wheeling occasionally.")]
        [SerializeField] private GullConfig _config = GullConfig.Default;

        [Tooltip("Sorting order — gulls fly ABOVE everything (they're high in the sky).")]
        [SerializeField] private int _sortingOrder = 20;

        [Tooltip("How often (Hz) the flock updates. Gulls glide; a handful of Hz reads fine and stays cheap.")]
        [Min(5f)] [SerializeField] private float _tickHz = 30f;

        private struct Gull
        {
            public float Radius;
            public float Period;     // seconds per loop
            public float PhaseOffset;
            public float Variant;    // reshapes the loop
            public float ActiveStart; // 0..1 window within the loop where the bird is visible
            public float ActiveSpan;
            public float SizeJit;
        }

        private Gull[] _gulls;
        private SpriteRenderer[] _renderers;
        private Sprite _sprite;
        private float _tickTimer;

        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("GullFlock") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<GullFlock>();
        }

        private void Awake()
        {
            _sprite = AmbientGlobals.BuildGull("GullFlock.Gull", 32);
            BuildFlock();
        }

        private void OnEnable() => _tickTimer = 0f;

        private void BuildFlock()
        {
            int n = Mathf.Max(0, _config.Count);
            _gulls = new Gull[n];
            _renderers = new SpriteRenderer[n];
            for (int i = 0; i < n; i++)
            {
                // Deterministic per-gull parameters (no RNG).
                float hr = AmbientParticleMath.Hash01(i, 3);
                float hp = AmbientParticleMath.Hash01(i, 17);
                float hph = AmbientParticleMath.Hash01(i, 31);
                float hv = AmbientParticleMath.Hash01(i, 47);
                float ha = AmbientParticleMath.Hash01(i, 59);
                float hsz = AmbientParticleMath.Hash01(i, 73);

                float span = Mathf.Clamp01(_config.ActiveFraction);
                _gulls[i] = new Gull
                {
                    Radius = Mathf.Lerp(_config.RadiusRange.x, _config.RadiusRange.y, hr),
                    Period = Mathf.Max(0.5f, Mathf.Lerp(_config.PeriodRange.x, _config.PeriodRange.y, hp)),
                    PhaseOffset = hph,
                    Variant = hv,
                    ActiveStart = ha * (1f - span),  // place the visible window somewhere in the loop
                    ActiveSpan = span,
                    SizeJit = 0.85f + hsz * 0.3f,
                };

                var go = new GameObject("gull");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _sprite;
                sr.sortingOrder = _sortingOrder;
                go.SetActive(false);
                _renderers[i] = sr;
            }
        }

        private void Update()
        {
            _tickTimer -= Time.deltaTime;
            if (_tickTimer > 0f) return;
            _tickTimer = _tickHz > 0f ? 1f / _tickHz : 0.033f;
            Tick();
        }

        private void Tick()
        {
            if (_gulls == null || _gulls.Length == 0) return;

            Camera cam = AmbientGlobals.ResolveCamera();
            if (cam == null) { HideAll(); return; }
            Vector2 center = cam.transform.position;

            Vector2 wind = AmbientGlobals.Wind;
            Color tint = AmbientGlobals.DayNightTint;
            float brightness = AmbientParticleMath.DayNightBrightness(tint);
            float dayOpacity = AmbientParticleMath.DayNightOpacity(brightness, _config.NightFade);
            float time = Time.time;

            for (int i = 0; i < _gulls.Length; i++)
            {
                ref Gull g = ref _gulls[i];
                var sr = _renderers[i];

                float loopPhase = Mathf.Repeat(time / g.Period + g.PhaseOffset, 1f);

                // Visibility window: the bird is only on-screen for ActiveSpan of each loop, fading at the edges.
                float winAlpha = WindowAlpha(loopPhase, g.ActiveStart, g.ActiveSpan);
                if (winAlpha <= 1e-3f || dayOpacity <= 1e-3f)
                {
                    if (sr.gameObject.activeSelf) sr.gameObject.SetActive(false);
                    continue;
                }

                float radiusY = g.Radius * (_config.AreaHalfSize.y / Mathf.Max(0.01f, _config.AreaHalfSize.x));
                Vector2 pos = AmbientParticleMath.GullPosition(
                    center, g.Radius, radiusY, loopPhase, g.Variant, wind, _config.WindDrift);
                Vector2 heading = AmbientParticleMath.GullHeading(
                    center, g.Radius, radiusY, loopPhase, g.Variant, wind, _config.WindDrift);

                var t = sr.transform;
                t.position = new Vector3(pos.x, pos.y, 0f);
                float size = _config.Size * g.SizeJit;
                // face travel: flip horizontally when heading west so the glyph isn't mirrored awkwardly
                float sx = heading.x >= 0f ? size : -size;
                t.localScale = new Vector3(sx, size, 1f);

                float alpha = Mathf.Clamp01(_config.MaxAlpha * winAlpha * dayOpacity);
                Color col = _config.Color * tint;
                col.a = alpha;
                sr.color = col;
                if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// The visibility envelope of a gull within its loop: 1 across the active window
        /// [<paramref name="start"/>, start+span], smoothly fading in/out at the window edges, 0 outside — so a
        /// bird sweeps in, wheels, and drifts off rather than blinking on. Wraps around the loop. Pure-ish
        /// (kept local; no sim). Handles the full-loop case (span≈1) as always-on.
        /// </summary>
        private static float WindowAlpha(float phase, float start, float span)
        {
            if (span >= 0.999f) return 1f;
            if (span <= 1e-4f) return 0f;
            // distance into the window, wrapped
            float d = Mathf.Repeat(phase - start, 1f);
            if (d > span) return 0f;
            float edge = Mathf.Min(0.2f, span * 0.4f);   // fade band at each end
            float rin = edge <= 1e-4f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d / edge));
            float rout = edge <= 1e-4f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((span - d) / edge));
            return Mathf.Clamp01(rin * rout);
        }

        private void HideAll()
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null && _renderers[i].gameObject.activeSelf)
                    _renderers[i].gameObject.SetActive(false);
        }
    }
}
