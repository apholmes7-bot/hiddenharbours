using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Environment
{
    /// <summary>
    /// <b>The dark hole a shovel leaves in the flat</b> (owner ask 2026-11-22: "there should be a dark hole
    /// left for the remainder of that day, even by working npcs, or until tide washes over it").
    ///
    /// <para><b>What makes a hole.</b> The player's dig: the blade entering the ground
    /// (<see cref="JuiceMoment.DigImpact"/>, at the ground point under the blade tip), heard on the bus, so a
    /// dig interrupted before the blade lands leaves nothing. The villagers digging on the flats: asked of
    /// <see cref="DugHoleSources"/>, which derives their holes from their day (a function of the clock), so
    /// those need no saving and are there after a load exactly as if the game had run all day.</para>
    ///
    /// <para><b>How long.</b> <see cref="DugHoleBook"/> / <see cref="DugHoleMath.EndsAt"/>: the rest of the
    /// game day, or until the deterministic tide first covers the spot, whichever is first.</para>
    ///
    /// <para><b>Not saved.</b> The player's holes are session state: a load (or quitting) forgets them,
    /// and the save format is untouched.</para>
    ///
    /// <para><b>Cost.</b> One self-installed hidden host, a fixed pool of <see cref="SpriteRenderer"/>s
    /// sharing one generated sprite under one <see cref="SortingGroup"/> (above the footprints, below the sea
    /// plane and everyone standing), and a refresh every <see cref="DugHoleSettings.RefreshRealSeconds"/>.
    /// Between refreshes a frame costs one compare; nothing allocates after the pool is built.</para>
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class DugHolePresenter : MonoBehaviour
    {
        private static bool _installed;

        /// <summary>Orders per metre of screen height inside the holes' own sorting group (one per art pixel,
        /// 32 px a metre) — only holes compete here, so this just keeps a near hole over a far one.</summary>
        private const float YSortPerMetre = 32f;
        private const int MaxYSort = 32000;   // inside Unity's sortingOrder range

        private readonly DugHoleBook _book = new DugHoleBook();
        private IDugHoleGround _ground = new LiveDugHoleGround();
        private IDugHoleSource _sourceOverride;
        private DugHoleSettings? _settingsOverride;

        private SortingGroup _group;
        private SpriteRenderer[] _pool;
        private Sprite _sprite;
        private Texture2D _texture;
        private float _nextRefresh;
        private int _shown;

        /// <summary>The live host, or null before the first scene.</summary>
        public static DugHolePresenter Instance { get; private set; }

        /// <summary>The rules (tests and probes read <see cref="DugHoleBook.Open"/>).</summary>
        public DugHoleBook Book => _book;

        /// <summary>How many hole renderers are on.</summary>
        public int ShownCount => _shown;

        /// <summary>The renderer pool (tests and probes; built on first use).</summary>
        public SpriteRenderer[] Pool { get { EnsureBuilt(); return _pool; } }

        private DugHoleSettings Settings => _settingsOverride ?? DugHoleSettings.Current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("DugHolePresenter") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<DugHolePresenter>();
        }

        private void Awake()
        {
            Instance = this;
            EnsureBuilt();
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(Instance, this)) Instance = null;
            if (_sprite != null) { if (Application.isPlaying) Destroy(_sprite); else DestroyImmediate(_sprite); }
            if (_texture != null) { if (Application.isPlaying) Destroy(_texture); else DestroyImmediate(_texture); }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<JuiceMomentCue>(OnMoment);
            EventBus.Subscribe<GameLoaded>(OnGameLoaded);
            _nextRefresh = 0f;
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<JuiceMomentCue>(OnMoment);
            EventBus.Unsubscribe<GameLoaded>(OnGameLoaded);
        }

        // ---- test seams ------------------------------------------------------------------------------

        /// <summary>Ask this ground instead of the live tide and terrain (tests). Null = live.</summary>
        public void ConfigureGround(IDugHoleGround ground) => _ground = ground ?? new LiveDugHoleGround();

        /// <summary>Ask this source for villager holes instead of the registry (tests). Null = the registry.</summary>
        public void ConfigureSource(IDugHoleSource source) => _sourceOverride = source;

        /// <summary>Pin the settings (tests). Null = <see cref="DugHoleSettings.Current"/>.</summary>
        public void OverrideSettings(DugHoleSettings? settings) => _settingsOverride = settings;

        // ---- the bus ---------------------------------------------------------------------------------

        /// <summary>Public so tests drive the same path the bus does. Only the blade entering the ground digs.</summary>
        public void OnMoment(JuiceMomentCue e)
        {
            if (e.Kind != JuiceMoment.DigImpact) return;
            IGameClock clock = GameServices.Clock;
            if (clock == null) return;
            DigAt(e.At, clock.TotalSeconds, GameServices.CurrentRegionId);
        }

        /// <summary>A load replaces the world the holes were dug in; the player's are session state.</summary>
        public void OnGameLoaded(GameLoaded e)
        {
            _book.ClearPlayerHoles();
            _nextRefresh = 0f;
        }

        /// <summary>The player's blade went in at <paramref name="at"/> at game time <paramref name="now"/>.</summary>
        public bool DigAt(Vector2 at, double now, string regionId)
        {
            bool kept = _book.AddPlayerHole(at, now, regionId, _ground, Settings, GameServices.SecondsPerDay);
            if (kept) RefreshAt(now, regionId);   // show it on the frame the blade lands
            return kept;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;   // the idle cost: one compare
            DugHoleSettings s = Settings;
            _nextRefresh = Time.unscaledTime + Mathf.Max(0.05f, s.RefreshRealSeconds);
            IGameClock clock = GameServices.Clock;
            if (clock == null) { HideFrom(0); return; }
            RefreshAt(clock.TotalSeconds, GameServices.CurrentRegionId);
        }

        /// <summary>Work out and draw the holes open at <paramref name="now"/>. Public for tests and probes.</summary>
        public int RefreshAt(double now, string regionId)
        {
            EnsureBuilt();
            DugHoleSettings s = Settings;
            int n = _book.Refresh(now, regionId, _ground, s, GameServices.SecondsPerDay, _pool.Length, _sourceOverride);
            Vector3 scale = HoleScale(s);
            for (int i = 0; i < n; i++)
            {
                SpriteRenderer sr = _pool[i];
                Vector2 p = _book.Open[i].Position;
                sr.transform.localPosition = new Vector3(p.x, p.y, 0f);
                sr.transform.localScale = scale;
                // Nearer the camera (lower on screen) draws over a hole further up the flat.
                sr.sortingOrder = Mathf.Clamp(-Mathf.RoundToInt(p.y * YSortPerMetre), -MaxYSort, MaxYSort);
                if (!sr.enabled) sr.enabled = true;
            }
            HideFrom(n);
            _shown = n;
            return n;
        }

        /// <summary>The hole's world size: its ground width, and its depth across the ground squashed by the ¾ view.</summary>
        public static Vector3 HoleScale(in DugHoleSettings s)
            => new Vector3(s.WidthMetres, s.WidthMetres * s.DepthOverWidth * IsoGround.GroundDepthScale, 1f);

        private void HideFrom(int first)
        {
            if (_pool == null) return;
            for (int i = first; i < _pool.Length; i++)
                if (_pool[i].enabled) _pool[i].enabled = false;
            if (first == 0) _shown = 0;
        }

        // ---- the pool --------------------------------------------------------------------------------

        private void EnsureBuilt()
        {
            if (_pool != null) return;
            DugHoleSettings s = Settings;
            _texture = BuildTexture(s);
            int px = _texture.width;
            _sprite = Sprite.Create(_texture, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), px);
            _sprite.name = "DugHole";
            _sprite.hideFlags = HideFlags.DontSave;

            _group = gameObject.GetComponent<SortingGroup>();
            if (_group == null) _group = gameObject.AddComponent<SortingGroup>();
            _group.sortingOrder = s.SortingOrder;

            int n = Mathf.Max(1, s.PoolSize);
            _pool = new SpriteRenderer[n];
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("hole");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _sprite;
                sr.enabled = false;
                _pool[i] = sr;
            }
        }

        /// <summary>
        /// The stand-in hole: a near-black bottom set toward the viewer (the near wall hides the top of the
        /// pit in the ¾ view, so the far wall shows above the dark), a wall ring in the rim colour, and a
        /// faint wet halo churned round the lip. Point filtered, one texel per art pixel; colours are sRGB.
        /// </summary>
        public static Texture2D BuildTexture(in DugHoleSettings s)
        {
            int px = Mathf.Max(6, s.TexturePixels);
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false)
            {
                name = "DugHole",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[px * px];
            Color clear = new Color(s.RimColor.r, s.RimColor.g, s.RimColor.b, 0f);
            for (int y = 0; y < px; y++)
            {
                for (int x = 0; x < px; x++)
                {
                    float u = (x + 0.5f) / px * 2f - 1f;
                    float v = (y + 0.5f) / px * 2f - 1f;
                    float d = Mathf.Sqrt(u * u + v * v);
                    // The bottom: a smaller disc dropped toward the viewer.
                    float bu = u / 0.66f, bv = (v + 0.16f) / 0.6f;
                    float bottom = Mathf.Sqrt(bu * bu + bv * bv);
                    Color c;
                    if (d > 1f) c = clear;
                    else if (bottom <= 1f) c = s.InnerColor;
                    else if (d <= 0.8f) c = new Color(s.RimColor.r, s.RimColor.g, s.RimColor.b, Mathf.Max(s.RimColor.a, 0.9f));
                    else c = new Color(s.RimColor.r, s.RimColor.g, s.RimColor.b, s.RimColor.a * 0.5f);
                    pixels[y * px + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }
    }
}
