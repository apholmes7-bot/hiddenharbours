using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// The pure shapes behind the figure's answer to a big one (owner's ask 2026-11-13: "the player
    /// sprite should have an exclamation for a big fish too, i want the player sprite to react to
    /// knowing something large is on the end"). Every number is a <see cref="FishFeelSettings"/> dial
    /// (<c>GameConfig.FishFeel</c>); nothing here is read by the fight's rules.
    /// </summary>
    public static class BigOneReactionMath
    {
        /// <summary>The mark's size as a multiple of its settled size: 0 → the overshoot over the pop
        /// time, back down to 1 over the same again, then 1.</summary>
        public static float MarkScale(float elapsed, in FishFeelSettings s)
        {
            float pop = s.BigOneMarkPopSeconds;
            float e = Mathf.Max(0f, elapsed);
            if (pop <= 0f || e >= pop * 2f) return 1f;
            float peak = Mathf.Max(1f, s.BigOneMarkPopScale);
            return e < pop ? Mathf.Lerp(0f, peak, e / pop) : Mathf.Lerp(peak, 1f, (e - pop) / pop);
        }

        /// <summary>The mark's opacity: full, then fading to nothing over the last fade-seconds of its time.</summary>
        public static float MarkAlpha(float elapsed, in FishFeelSettings s)
        {
            float remaining = s.BigOneMarkSeconds - Mathf.Max(0f, elapsed);
            if (remaining <= 0f) return 0f;
            return s.BigOneMarkFadeSeconds <= 0f ? 1f : Mathf.Clamp01(remaining / s.BigOneMarkFadeSeconds);
        }

        /// <summary>Extra seconds the hook-set pose holds for a big one; 0 with the reaction switched off.</summary>
        public static float StrikeHoldSeconds(in FishFeelSettings s)
            => s.BigOneReactionEnabled ? Mathf.Max(0f, s.BigOneStrikeHoldSeconds) : 0f;
    }

    /// <summary>
    /// How long the exclamation has been up. Starts on the big-one moment, runs for
    /// <see cref="FishFeelSettings.BigOneMarkSeconds"/>, then clears itself. A second start while it is
    /// already up is refused, so one hook can never show two marks.
    /// </summary>
    public struct BigOneReactionClock
    {
        /// <summary>The mark is up.</summary>
        public bool Active { get; private set; }
        /// <summary>Seconds since it went up (0 when it is down).</summary>
        public float Elapsed { get; private set; }

        /// <summary>Start the mark. False (and nothing changes) when the reaction is switched off, has
        /// no time to show, or is already up.</summary>
        public bool Trigger(in FishFeelSettings s)
        {
            if (Active || !s.BigOneReactionEnabled || s.BigOneMarkSeconds <= 0f) return false;
            Active = true;
            Elapsed = 0f;
            return true;
        }

        /// <summary>Advance by <paramref name="dt"/> seconds; clears at the end of its time, or at once
        /// if the owner switches the reaction off while it is up.</summary>
        public void Tick(float dt, in FishFeelSettings s)
        {
            if (!Active) return;
            Elapsed += Mathf.Max(0f, dt);
            if (!s.BigOneReactionEnabled || Elapsed >= s.BigOneMarkSeconds) Clear();
        }

        public void Clear()
        {
            Active = false;
            Elapsed = 0f;
        }
    }

    /// <summary>
    /// THE EXCLAMATION OVER THE FISHER. Listens for the Core moment "a big one is hooked"
    /// (<see cref="InnerVoiceCue"/> carrying <see cref="InnerVoiceCues.BigOneHooked"/> — the same signal,
    /// raised once per such hook by the rod loop, that her "Woah" line answers) and pops a "!" above the
    /// figure for <c>GameConfig.FishFeel.BigOneMarkSeconds</c>. Rule 4: it knows the cue id and nothing
    /// of Fishing or World.
    ///
    /// <para><b>The mark is procedural placeholder art</b> — a few pixels built once in code, because no
    /// exclamation / emote sprite exists in the project yet. A baked marker can replace
    /// <see cref="Glyph"/> without touching the timing.</para>
    ///
    /// <para>Added to the fisher by <see cref="RodFightPresenter"/> (no scene edit). Idle cost: one bool
    /// test per LateUpdate; the child renderer is made on the first big one, and nothing allocates per frame.</para>
    /// </summary>
    public sealed class BigOneReactionPresenter : MonoBehaviour
    {
        // The placeholder glyph's pixel SHAPE (not feel): a 3 px wide "!" — a 2 px dot, a 1 px gap and
        // a 7 px bar — inside a 1 px dark outline.
        private const int GlyphWidthPx = 5;
        private const int GlyphHeightPx = 12;
        private const int GlyphDotTopRow = 2;      // rows 1..2 are the dot
        private const int GlyphBarBottomRow = 4;   // rows 4..10 are the bar
        // Drawn over the figure and over the rod in its hands (the rod overlay sits at figure + 1).
        private const int OrderAboveFigure = 2;
        private static readonly Color OutlineColor = new Color(0.1f, 0.08f, 0.12f, 1f);

        private static Sprite _glyph;

        private BigOneReactionClock _clock;
        private SpriteRenderer _mark;
        private SpriteRenderer _figure;

        /// <summary>The mark is up right now.</summary>
        public bool Showing => _clock.Active;

        /// <summary>The mark's renderer, or null before the first big one. For tests/tooling.</summary>
        public SpriteRenderer Mark => _mark;

        private static FishFeelSettings Feel
            => GameServices.Config != null ? GameServices.Config.FishFeel : FishFeelSettings.Default;

        private void OnEnable() => EventBus.Subscribe<InnerVoiceCue>(OnInnerVoiceCue);

        private void OnDisable()
        {
            EventBus.Unsubscribe<InnerVoiceCue>(OnInnerVoiceCue);
            Hide();
        }

        /// <summary>Public so tests drive the same path the bus does.</summary>
        public void OnInnerVoiceCue(InnerVoiceCue e)
        {
            if (e.CueId != InnerVoiceCues.BigOneHooked) return;
            FishFeelSettings feel = Feel;
            if (!_clock.Trigger(in feel)) return;
            Pose(in feel);
        }

        private void LateUpdate()
        {
            if (_clock.Active) Advance(Time.deltaTime);
        }

        /// <summary>Advance the mark by <paramref name="dt"/> seconds (LateUpdate; tests call it directly).</summary>
        public void Advance(float dt)
        {
            if (!_clock.Active) return;
            FishFeelSettings feel = Feel;
            _clock.Tick(dt, in feel);
            if (_clock.Active) Pose(in feel);
            else Hide();
        }

        private void Pose(in FishFeelSettings feel)
        {
            if (_mark == null) _mark = MakeMark();
            if (_figure == null) _figure = GetComponent<SpriteRenderer>();
            if (_figure != null)
            {
                _mark.sortingLayerID = _figure.sortingLayerID;
                _mark.sortingOrder = _figure.sortingOrder + OrderAboveFigure;
            }

            Transform t = _mark.transform;
            Vector2 offset = feel.BigOneMarkOffsetMetres;
            t.localPosition = new Vector3(offset.x, offset.y, 0f);
            t.rotation = Quaternion.identity;   // upright on screen whatever the figure's transform does
            float size = Mathf.Max(0f, feel.BigOneMarkHeightMetres) * BigOneReactionMath.MarkScale(_clock.Elapsed, in feel);
            t.localScale = new Vector3(size, size, 1f);

            Color c = feel.BigOneMarkColor;
            c.a *= BigOneReactionMath.MarkAlpha(_clock.Elapsed, in feel);
            _mark.color = c;
            _mark.enabled = true;
        }

        private void Hide()
        {
            _clock.Clear();
            if (_mark != null) _mark.enabled = false;
        }

        private SpriteRenderer MakeMark()
        {
            var go = new GameObject("BigOneMark");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Glyph();
            sr.sortingOrder = OrderAboveFigure;
            sr.enabled = false;
            return sr;
        }

        /// <summary>The shared placeholder "!" — one unit tall, pivot at its foot, built once.</summary>
        private static Sprite Glyph()
        {
            if (_glyph != null) return _glyph;
            var tex = new Texture2D(GlyphWidthPx, GlyphHeightPx, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                name = "BigOneMark",
            };
            var px = new Color32[GlyphWidthPx * GlyphHeightPx];
            Color32 outline = OutlineColor, fill = Color.white;
            for (int y = 0; y < GlyphHeightPx; y++)
            {
                bool inkRow = (y >= 1 && y <= GlyphDotTopRow) || (y >= GlyphBarBottomRow && y <= GlyphHeightPx - 2);
                for (int x = 0; x < GlyphWidthPx; x++)
                    px[y * GlyphWidthPx + x] = inkRow && x >= 1 && x <= GlyphWidthPx - 2 ? fill : outline;
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _glyph = Sprite.Create(tex, new Rect(0, 0, GlyphWidthPx, GlyphHeightPx), new Vector2(0.5f, 0f), GlyphHeightPx);
            _glyph.hideFlags = HideFlags.HideAndDontSave;
            _glyph.name = "BigOneMark";
            return _glyph;
        }
    }
}
