using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// THE FRESHNESS READ ON THE HOLD (M1 §7.3) — the tell that stops a bucket of catch going from
    /// "fine" to "the fishmonger waved me off" with nothing in between. <see cref="Freshness"/> asks for
    /// this by name: <i>"Losing a bucket you watched rot is a lesson; losing one that looked fine until
    /// the buyer refused it is a bug."</i>
    ///
    /// <para><b>It rides the tray, it is not a HUD counter.</b> Owner canon, recorded on
    /// <see cref="ShipHold"/>: the deck container IS the hold's readout. <see cref="DeckContainerPresenter"/>
    /// already carries two of the three channels — how MUCH (fill state) and how far GONE (the
    /// <see cref="RotTint"/> green). The one thing a tint cannot say is WHEN, so that is all this adds: a
    /// small mark above the tray counting down the time you have left to land it.</para>
    ///
    /// <para><b>Quiet until it matters.</b> Nothing is drawn while the catch is fresh, while the hold is
    /// empty, or while the catch is arrested (frozen, alive, on ice — a chilled tote is not on a clock).
    /// The mark fades in only once the worst item crosses <see cref="_showFromSpoil01"/>, so a clean deck
    /// stays clean and the mark appearing is itself the warning.</para>
    ///
    /// <para><b>A pure read (rule 4/5).</b> Everything shown is folded by the Core
    /// <see cref="HoldFreshness"/> from <see cref="IHold.Items"/> and a <see cref="SpoilContext"/> — the
    /// same settle-on-read numbers the sell gate and the rot tint use, so the tray can never tell the
    /// player one thing and the till another. Nothing is stamped, banked or saved here.</para>
    ///
    /// <para>Runtime-spawned by <see cref="ShipHold"/> alongside the tray and the ice box, so every boat
    /// with a hold gets it without a builder re-run. Polls on a slow cadence (spoil accrues continuously,
    /// so it cannot be purely event-driven) and rebuilds its string only when the displayed value
    /// actually changes — no per-frame allocation (rule 7).</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ShipHold))]
    public sealed class DeckFreshnessTag : MonoBehaviour
    {
        [Tooltip("Spoil 0..1 at which the mark appears. Below this the deck stays clean — the mark " +
                 "showing up at all is the warning. 0.35 leaves well over half the catch's life as " +
                 "runway before the buyer's refusal at the policy's UnsellableSpoil.")]
        [Range(0f, 1f)] [SerializeField] private float _showFromSpoil01 = 0.35f;

        [Tooltip("How often (real seconds) the mark re-reads the hold. Spoil accrues continuously, so " +
                 "this cannot be event-driven; slow is fine because the number is in in-game hours.")]
        [Min(0.1f)] [SerializeField] private float _tickSeconds = 1f;

        [Tooltip("Height above the tray, in metres, that the mark floats.")]
        [SerializeField] private float _heightMeters = 0.55f;

        [Tooltip("World size of the mark's text. Small — this is a tag on a tote, not a banner.")]
        [Min(0.001f)] [SerializeField] private float _characterMeters = 0.11f;

        // Sits just above the tray so the mark is never hidden behind the catch it describes.
        private const int TagSortingOrder = DeckContainerPresenter.ContainerSortingOrder + 1;

        // The escalation. Colour is reinforcement only — the countdown NUMBER and the ⚠ glyph carry the
        // read on their own (accessibility §8).
        private static readonly Color Turning = new Color(0.96f, 0.80f, 0.35f);  // going off
        private static readonly Color Urgent  = new Color(0.95f, 0.52f, 0.25f);  // land it now
        private static readonly Color Rubbish = new Color(0.85f, 0.30f, 0.25f);  // already refused

        /// <summary>Below this many in-game hours to refusal, the mark reads as urgent.</summary>
        private const double UrgentHours = 2.0;

        private ShipHold _hold;
        private TextMesh _text;
        private MeshRenderer _textRenderer;
        private DeckContainerPresenter _tray;
        private float _tickTimer;
        private string _shown;

        private void Awake()
        {
            _hold = GetComponent<ShipHold>();
            _tray = GetComponent<DeckContainerPresenter>();
            BuildTag();
            SetShown(null);
        }

        private void LateUpdate()
        {
            // Ride the tray wherever the drawn facing puts it (the tray itself jumps with the pictured
            // deck each LateUpdate, so read its position AFTER it has moved).
            if (_text != null && _tray != null)
            {
                Vector3 trayPos = _tray.ContainerWorldPosition;
                _text.transform.position = new Vector3(trayPos.x, trayPos.y + _heightMeters, trayPos.z);
            }

            _tickTimer -= Time.deltaTime;
            if (_tickTimer > 0f) return;
            _tickTimer = _tickSeconds;

            Refresh();
        }

        private void Refresh()
        {
            HoldFreshnessSummary summary = HoldFreshness.Summarise(_hold, SpoilContext.Capture());

            // Nothing aboard, or nothing going off yet → the deck stays clean.
            if (summary.ItemCount == 0 || (!summary.HasSpoiled && summary.WorstSpoil < _showFromSpoil01))
            {
                SetShown(null);
                return;
            }

            // Already refused: no countdown left to run, say so plainly.
            if (summary.HasSpoiled)
            {
                SetShown(HoldReadStrings.Rubbish(summary.SpoiledCount), Rubbish);
                return;
            }

            // Arrested (frozen / alive / on ice) — it is turning, but it is not on a clock right now.
            if (!summary.HasCountdown)
            {
                SetShown(HoldReadStrings.Held(summary.WorstMode), Turning);
                return;
            }

            double hours = summary.SecondsToRefusal / SecondsPerHour();
            SetShown(HoldReadStrings.Countdown(hours), hours <= UrgentHours ? Urgent : Turning);
        }

        private static float SecondsPerHour()
        {
            GameConfig config = GameServices.Config;
            float perDay = config != null ? config.SecondsPerDay : GameConfig.DefaultSecondsPerDay;
            return perDay / 24f;
        }

        // Change-detected: an unchanged read repaints nothing and allocates nothing.
        private void SetShown(string value, Color color = default)
        {
            if (value == _shown)
            {
                if (value != null && _text != null) _text.color = color;
                return;
            }
            _shown = value;
            if (_text == null) return;

            bool show = !string.IsNullOrEmpty(value);
            if (_textRenderer != null) _textRenderer.enabled = show;
            if (!show) return;

            _text.text = value;
            _text.color = color;
        }

        private void BuildTag()
        {
            var go = new GameObject("FreshnessTag");
            go.transform.SetParent(transform, false);

            _text = go.AddComponent<TextMesh>();
            _text.font = DefaultFont();
            _text.anchor = TextAnchor.LowerCenter;
            _text.alignment = TextAlignment.Center;
            _text.fontSize = 64;                       // rendered small by characterSize; keeps it crisp
            _text.characterSize = _characterMeters;
            _text.color = Turning;

            _textRenderer = go.GetComponent<MeshRenderer>();
            if (_textRenderer != null)
            {
                // TextMesh renders with the font's own material; point it at the font so the glyphs show.
                if (_text.font != null) _textRenderer.sharedMaterial = _text.font.material;
                _textRenderer.sortingOrder = TagSortingOrder;
                _textRenderer.enabled = false;         // quiet until something is going off
            }
        }

        // Unity 6 removed Arial.ttf from Resources; LegacyRuntime.ttf is the built-in fallback.
        private static Font DefaultFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }
    }
}
