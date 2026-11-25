using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>The pure half of the soap bubbles: where each one is and how strongly it shows.</summary>
    public static class FrothMath
    {
        /// <summary>A bubble's age in its own cycle, staggered so they never rise in step.</summary>
        public static float Age(float time, int index, int count, in WashVisualsSettings s)
        {
            float life = Mathf.Max(0.1f, s.BubbleLifetimeSeconds);
            float stagger = life * index / Mathf.Max(1, count);
            return Mathf.Repeat(time + stagger, life);
        }

        /// <summary>Which cycle a bubble is on (each cycle starts it somewhere new).</summary>
        public static int Cycle(float time, int index, int count, in WashVisualsSettings s)
        {
            float life = Mathf.Max(0.1f, s.BubbleLifetimeSeconds);
            float stagger = life * index / Mathf.Max(1, count);
            return Mathf.FloorToInt((time + stagger) / life);
        }

        /// <summary>A bubble's offset from his feet: it starts somewhere either side of his shoulders and rises.</summary>
        public static Vector2 Offset(float age, int index, int cycle, in WashVisualsSettings s)
        {
            float side = Hash01(index, cycle) * 2f - 1f;
            float wobble = Mathf.Sin((age * 6f) + index) * 0.25f;
            float x = s.BubbleOffsetMetres.x + (side + wobble * 0.2f) * s.BubbleSpreadMetres;
            float y = s.BubbleOffsetMetres.y + age * s.BubbleRiseMetresPerSecond;
            return new Vector2(x, y);
        }

        /// <summary>How strongly a bubble shows at an age: it swells in quickly and fades as it rises.</summary>
        public static float Alpha(float age, in WashVisualsSettings s)
        {
            float life = Mathf.Max(0.1f, s.BubbleLifetimeSeconds);
            float t = Mathf.Clamp01(age / life);
            float rise = Mathf.Clamp01(t * 6f);
            return rise * (1f - t);
        }

        /// <summary>A stable 0..1 from two ints (no shared random state).</summary>
        public static float Hash01(int a, int b)
        {
            unchecked
            {
                uint h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }

    /// <summary>
    /// <b>Soap bubbles rising off the player while a soaped wash runs</b> (owner ask 2026-11-17: "soapy frothing
    /// animations"). The fixture froths in its own art; this is the half on him, so a wash at a basin or under
    /// a shower reads as soapy too.
    ///
    /// <para><b>Driven by the wash's soap flag</b> (<see cref="WashSoapSignal"/>), read once at each edge of the
    /// wash: no soap, no suds, unless <see cref="WashVisualsSettings.FrothWithoutSoap"/>. The pool is made once
    /// the first time it is needed and the glyph is generated once; nothing allocates per frame.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerFrothPresenter : MonoBehaviour
    {
        // The placeholder glyph's pixel shape: a 4 px ring with a 1 px highlight, in a 4 x 4 cell.
        private const int GlyphPx = 4;

        private static Sprite _glyph;

        private SpriteRenderer[] _pool;
        private SpriteRenderer _body;
        private bool _washing;
        private bool _soaped;
        private int _shown;

        public int ShownCount => _shown;
        public bool Frothing { get; private set; }

        private void OnEnable() => EventBus.Subscribe<PlayerWashChanged>(OnWashChanged);

        private void OnDisable()
        {
            EventBus.Unsubscribe<PlayerWashChanged>(OnWashChanged);
            HideAll();
        }

        private void OnWashChanged(PlayerWashChanged e)
        {
            _washing = e.Washing;
            _soaped = WashSoapSignal.IsSoaped(e);
        }

        private void LateUpdate()
        {
            WashVisualsSettings s = WashVisualsSettings.Current;
            Frothing = _washing && WashVisualsSettings.Froths(_soaped, s);
            Apply(Frothing, Time.time, in s);
        }

        /// <summary>Draw the bubbles for one frame (public for tests).</summary>
        public int Apply(bool frothing, float time, in WashVisualsSettings s)
        {
            int want = frothing ? Mathf.Max(0, s.BubbleCount) : 0;
            if (want > 0 && _pool == null) BuildPool(want);
            if (_pool == null) return _shown = 0;
            want = Mathf.Min(want, _pool.Length);
            if (_body == null) _body = GetComponent<SpriteRenderer>();

            float size = Mathf.Max(0f, s.BubbleSizeMetres);
            for (int i = 0; i < _pool.Length; i++)
            {
                SpriteRenderer b = _pool[i];
                if (i >= want)
                {
                    if (b.enabled) b.enabled = false;
                    continue;
                }

                if (_body != null)
                {
                    b.sortingLayerID = _body.sortingLayerID;
                    b.sortingOrder = _body.sortingOrder + s.BubbleOrdersAboveFigure;
                }

                float age = FrothMath.Age(time, i, want, in s);
                int cycle = FrothMath.Cycle(time, i, want, in s);
                Vector2 offset = FrothMath.Offset(age, i, cycle, in s);
                Transform t = b.transform;
                t.localPosition = new Vector3(offset.x, offset.y, 0f);
                t.rotation = Quaternion.identity;
                float k = 0.6f + 0.4f * FrothMath.Hash01(i, cycle + 7);
                t.localScale = new Vector3(size * k, size * k, 1f);

                Color c = s.BubbleColor;
                c.a *= FrothMath.Alpha(age, in s);
                b.color = c;
                if (!b.enabled) b.enabled = true;
            }
            return _shown = want;
        }

        private void HideAll()
        {
            _shown = 0;
            if (_pool == null) return;
            for (int i = 0; i < _pool.Length; i++)
                if (_pool[i] != null) _pool[i].enabled = false;
        }

        private void BuildPool(int size)
        {
            int n = Mathf.Max(1, size);
            _pool = new SpriteRenderer[n];
            Sprite glyph = Glyph();
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("SoapBubble");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = glyph;
                sr.enabled = false;
                _pool[i] = sr;
            }
        }

        private static Sprite Glyph()
        {
            if (_glyph != null) return _glyph;
            var tex = new Texture2D(GlyphPx, GlyphPx, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                name = "SoapBubble",
            };
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 rim = new Color32(200, 215, 230, 255);
            Color32 shine = new Color32(255, 255, 255, 255);
            Color32 fill = new Color32(235, 245, 255, 90);
            // Row 0 is the bottom. A round 4 px bubble: rim all round, a faint fill, a highlight top-left.
            var px = new[]
            {
                clear, rim,  rim,  clear,
                rim,   fill, fill, rim,
                rim,   shine, fill, rim,
                clear, rim,  rim,  clear,
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _glyph = Sprite.Create(tex, new Rect(0, 0, GlyphPx, GlyphPx), new Vector2(0.5f, 0.5f), GlyphPx);
            _glyph.hideFlags = HideFlags.HideAndDontSave;
            return _glyph;
        }
    }
}
