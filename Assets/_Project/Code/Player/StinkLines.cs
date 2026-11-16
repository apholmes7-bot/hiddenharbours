using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// The pure shapes behind the stink lines (cleanliness slice 2). Every number is a
    /// <see cref="StinkLinesSettings"/> dial (<c>GameConfig.StinkLines</c>); nothing here changes the game,
    /// only the picture.
    /// </summary>
    public static class StinkLinesMath
    {
        /// <summary>How many lines show at <paramref name="dirt"/> (0 clean .. 1 filthy): none below
        /// <see cref="StinkLinesSettings.StartAtDirt"/>, then <see cref="StinkLinesSettings.MinLines"/>,
        /// stepping up evenly to <see cref="StinkLinesSettings.MaxLines"/> at
        /// <see cref="StinkLinesSettings.FullAtDirt"/>. Never decreases as dirt rises.</summary>
        public static int LineCount(float dirt, in StinkLinesSettings s)
        {
            if (!s.Enabled || s.MaxLines <= 0 || dirt < s.StartAtDirt) return 0;
            int max = s.MaxLines;
            int min = Mathf.Clamp(s.MinLines, 1, max);
            float span = s.FullAtDirt - s.StartAtDirt;
            float t = span <= 0f ? 1f : Mathf.Clamp01((dirt - s.StartAtDirt) / span);
            int steps = max - min + 1;
            return min + Mathf.Min(steps - 1, Mathf.FloorToInt(t * steps));
        }

        /// <summary>Is the player's figure on screen to carry lines? Below decks they hide unless the
        /// owner asks otherwise; otherwise the lines show wherever his picture is drawn: by the deck rider
        /// aboard, or by his own body ashore (false at a helm or in a cab, where the body is put away).</summary>
        public static bool FigureShown(bool riderDrawing, bool belowDecks, bool bodyDrawn, bool showBelowDecks)
        {
            if (belowDecks && !showBelowDecks) return false;
            return riderDrawing || bodyDrawn;
        }

        /// <summary>The side offset of line <paramref name="index"/>'s column: the middle first, then right,
        /// then left, outwards. Stable when the count changes, so a line never jumps sideways.</summary>
        public static float ColumnX(int index, in StinkLinesSettings s)
        {
            if (index <= 0) return 0f;
            int ring = (index + 1) / 2;
            float side = (index & 1) == 1 ? 1f : -1f;
            return ring * s.SpreadMetres * side;
        }

        /// <summary>How far through its life line <paramref name="index"/> of a pool of
        /// <paramref name="poolSize"/> is at <paramref name="time"/>, in seconds (0 .. lifetime). Lines are
        /// staggered by an even share of the life so they rise one after another, not all at once.</summary>
        public static float Age(float time, int index, int poolSize, in StinkLinesSettings s)
        {
            float life = Mathf.Max(0.1f, s.LifetimeSeconds);
            float share = poolSize > 0 ? (float)index / poolSize : 0f;
            float phase = Mathf.Repeat(time / life + share, 1f);
            return phase * life;
        }

        /// <summary>Line <paramref name="index"/>'s offset from the figure's feet at <paramref name="age"/>
        /// seconds: its column, rising from the head, wavering sideways.</summary>
        public static Vector2 Offset(float age, int index, in StinkLinesSettings s)
        {
            float a = Mathf.Max(0f, age);
            // Neighbouring lines waver in opposite directions (half a cycle apart).
            float sway = Mathf.Sin(2f * Mathf.PI * a * s.SwayCyclesPerSecond + Mathf.PI * index) * s.SwayMetres;
            return new Vector2(s.HeadOffsetMetres.x + ColumnX(index, in s) + sway,
                               s.HeadOffsetMetres.y + a * s.RiseMetresPerSecond);
        }

        /// <summary>Line opacity at <paramref name="age"/> (0..1 of the tint's own alpha): fades in over
        /// the first fade-seconds of its life and out over the last.</summary>
        public static float Alpha(float age, in StinkLinesSettings s)
        {
            float life = Mathf.Max(0.1f, s.LifetimeSeconds);
            float a = Mathf.Clamp(age, 0f, life);
            if (s.FadeSeconds <= 0f) return 1f;
            return Mathf.Clamp01(Mathf.Min(a, life - a) / s.FadeSeconds);
        }
    }

    /// <summary>
    /// THE STINK LINES OVER THE PLAYER (cleanliness slice 2, owner ask 2026-11-15). While his dirt is
    /// past <see cref="StinkLinesSettings.StartAtDirt"/>, a few small wavy lines rise from his head, more
    /// as he gets dirtier, and they fade away as he washes. A gentle joke and nothing more: no number, no
    /// penalty, no sound (owner ruling family: needs slow, never stop; never shown as a bar).
    ///
    /// <para><b>Rule 4.</b> The dirt is read through Core (<see cref="GameServices.Cleanliness"/>), and
    /// read again at once on the needs' <see cref="NeedStageChanged"/> and on
    /// <see cref="PlayerWashChanged"/>; otherwise once per <see cref="StinkLinesSettings.ResampleSeconds"/>.
    /// Where his picture is drawn comes from this module's own <see cref="DeckRiderVisual"/>.</para>
    ///
    /// <para><b>The line is procedural placeholder art</b>: an 11 px squiggle with a soft dark edge built
    /// once in code, because no stink-line sprite exists yet. A baked sprite can replace
    /// <see cref="Glyph"/> without touching the timing.</para>
    ///
    /// <para><b>Cost.</b> The pool is made on first need (none for a clean player) at
    /// <see cref="StinkLinesSettings.MaxLines"/> renderers and never grows. Nothing allocates per frame.
    /// Added to the player by <see cref="PlayerWalkController"/> (no scene edit).</para>
    /// </summary>
    [DefaultExecutionOrder(103)]   // after the rider (100) has stated where the body is drawn this frame
    public sealed class StinkLinesPresenter : MonoBehaviour
    {
        // The placeholder glyph's pixel SHAPE (not feel): a 1 px wavy line, 9 px tall, in a 5 x 11 cell
        // with a 1 px soft dark edge. Column of the ink per row, bottom to top.
        private const int GlyphWidthPx = 5;
        private const int GlyphHeightPx = 11;
        private static readonly int[] GlyphInkColumn = { 2, 1, 1, 2, 3, 3, 2, 1, 1 };
        private static readonly Color32 GlyphEdge = new Color32(90, 90, 90, 150);   // tinted to a dark rim

        private static Sprite _glyph;

        private SpriteRenderer[] _pool;
        private int _shown;
        private float _dirt;
        private float _resampleIn;
        private bool _resampleNow = true;

        private bool _resolved;
        private SpriteRenderer _body;
        private DeckRiderVisual _rider;

        /// <summary>The renderers made so far (0 until the first time he is dirty enough). For tests.</summary>
        public int PoolSize => _pool != null ? _pool.Length : 0;

        /// <summary>How many lines are up right now. For tests/tooling.</summary>
        public int ShownCount => _shown;

        /// <summary>Line <paramref name="i"/>'s renderer, or null. For tests/tooling.</summary>
        public SpriteRenderer Line(int i) => _pool != null && i >= 0 && i < _pool.Length ? _pool[i] : null;

        private void OnEnable()
        {
            EventBus.Subscribe<NeedStageChanged>(OnNeedStageChanged);
            EventBus.Subscribe<PlayerWashChanged>(OnWashChanged);
            _resampleNow = true;
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<NeedStageChanged>(OnNeedStageChanged);
            EventBus.Unsubscribe<PlayerWashChanged>(OnWashChanged);
            HideAll();
        }

        private void OnNeedStageChanged(NeedStageChanged e)
        {
            if (e.Need == NeedKind.Dirtiness) _resampleNow = true;
        }

        private void OnWashChanged(PlayerWashChanged e) => _resampleNow = true;

        private void LateUpdate()
        {
            StinkLinesSettings s = StinkLinesSettings.Current;
            _resampleIn -= Time.deltaTime;
            if (_resampleNow || _resampleIn <= 0f)
            {
                _dirt = GameServices.Cleanliness.Dirt;
                _resampleIn = s.ResampleSeconds;
                _resampleNow = false;
            }
            Apply(_dirt, FigureShownNow(in s), Time.time, in s);
        }

        /// <summary>Is his picture on screen right now (the rider aboard, the body ashore), and not below
        /// decks unless the settings allow it.</summary>
        private bool FigureShownNow(in StinkLinesSettings s)
        {
            Resolve();
            bool riderDrawing = _rider != null && _rider.IsDrawing;
            bool belowDecks = _rider != null && _rider.BelowDecks;
            bool bodyDrawn = _body != null && _body.enabled && (_rider == null || _rider.BodyShownOnRoot);
            return StinkLinesMath.FigureShown(riderDrawing, belowDecks, bodyDrawn, s.ShowBelowDecks);
        }

        /// <summary>Draw the lines for <paramref name="dirt"/> at <paramref name="time"/> (real seconds).
        /// LateUpdate calls it; tests call it directly. Returns how many lines are up.</summary>
        public int Apply(float dirt, bool figureShown, float time, in StinkLinesSettings s)
        {
            int want = figureShown ? StinkLinesMath.LineCount(dirt, in s) : 0;
            if (want > 0 && _pool == null) BuildPool(s.MaxLines);
            if (_pool == null) return _shown = 0;
            want = Mathf.Min(want, _pool.Length);

            Resolve();
            // Aboard, the picture is not at this root: it rides with the hull (the rider's stated offset,
            // the same read CarryHands makes). Zero ashore.
            Vector2 drawn = _rider != null ? (Vector2)transform.InverseTransformVector(_rider.DrawnBodyOffset) : Vector2.zero;
            float size = Mathf.Max(0f, s.LineHeightMetres);

            for (int i = 0; i < _pool.Length; i++)
            {
                SpriteRenderer line = _pool[i];
                if (i >= want)
                {
                    if (line.enabled) line.enabled = false;
                    continue;
                }

                // Inside the body's own live Y-sorted band, a few orders ahead: whatever covers him
                // (a wall in front, a building) covers his lines too.
                if (_body != null)
                {
                    line.sortingLayerID = _body.sortingLayerID;
                    line.sortingOrder = _body.sortingOrder + s.OrdersAboveFigure;
                }

                float age = StinkLinesMath.Age(time, i, _pool.Length, in s);
                Vector2 offset = StinkLinesMath.Offset(age, i, in s) + drawn;
                Transform t = line.transform;
                t.localPosition = new Vector3(offset.x, offset.y, 0f);
                t.rotation = Quaternion.identity;   // upright on screen whatever the root does
                t.localScale = new Vector3(size, size, 1f);

                Color c = s.Color;
                c.a *= StinkLinesMath.Alpha(age, in s);
                line.color = c;
                if (!line.enabled) line.enabled = true;
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

        private void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            _body = GetComponent<SpriteRenderer>();
            _rider = GetComponent<DeckRiderVisual>();
        }

        private void BuildPool(int size)
        {
            int n = Mathf.Max(1, size);
            _pool = new SpriteRenderer[n];
            Sprite glyph = Glyph();
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("StinkLine");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = glyph;
                sr.flipX = (i & 1) == 1;   // neighbours mirror, so three lines are not three copies
                sr.enabled = false;
                _pool[i] = sr;
            }
        }

        /// <summary>The shared placeholder squiggle: one unit tall, pivot at its foot, built once.</summary>
        private static Sprite Glyph()
        {
            if (_glyph != null) return _glyph;
            var tex = new Texture2D(GlyphWidthPx, GlyphHeightPx, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                name = "StinkLine",
            };
            var px = new Color32[GlyphWidthPx * GlyphHeightPx];
            Color32 clear = new Color32(0, 0, 0, 0), ink = new Color32(255, 255, 255, 255);
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            // Ink first, one pixel per row from row 1, then a soft rim on its empty 4-neighbours.
            for (int r = 0; r < GlyphInkColumn.Length; r++)
                px[(r + 1) * GlyphWidthPx + GlyphInkColumn[r]] = ink;
            for (int y = 0; y < GlyphHeightPx; y++)
            for (int x = 0; x < GlyphWidthPx; x++)
            {
                if (px[y * GlyphWidthPx + x].a != 0) continue;
                if (IsInk(px, x - 1, y) || IsInk(px, x + 1, y) || IsInk(px, x, y - 1) || IsInk(px, x, y + 1))
                    px[y * GlyphWidthPx + x] = GlyphEdge;
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _glyph = Sprite.Create(tex, new Rect(0, 0, GlyphWidthPx, GlyphHeightPx), new Vector2(0.5f, 0f), GlyphHeightPx);
            _glyph.hideFlags = HideFlags.HideAndDontSave;
            _glyph.name = "StinkLine";
            return _glyph;
        }

        private static bool IsInk(Color32[] px, int x, int y)
            => x >= 0 && x < GlyphWidthPx && y >= 0 && y < GlyphHeightPx && px[y * GlyphWidthPx + x].a == 255;
    }
}
