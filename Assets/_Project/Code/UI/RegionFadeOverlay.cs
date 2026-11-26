using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// A self-contained fullscreen fade that covers the VS-22 region-transition snap (the additive
    /// scene-cut + the persistent-rig reposition) so the Cove↔Nine Mile Creek crossing reads as a short voyage
    /// instead of a hard teleport. On a region change it FLASHES black and FADES IN — the new region
    /// reveals as the black clears — with a brief arrival card.
    ///
    /// <para><b>Self-contained &amp; additive.</b> It self-installs once at runtime
    /// (<see cref="RuntimeInitializeOnLoadMethod"/>) and is <c>DontDestroyOnLoad</c>, so it needs NO
    /// builder or scene wiring. It reads ONLY <see cref="SceneManager.activeSceneChanged"/> — the very
    /// signal the travel coordinator already uses — so there is no coupling into World/Boats/App and no
    /// reference to the builders, the reposition logic, or the HUD. The overlay never eats input
    /// (no raycast target), renders above the HUD only for the brief cover, and ALWAYS clears to
    /// transparent (no stuck-black); a re-trigger just restarts the one fade (no double-fade / no
    /// stacking).</para>
    /// </summary>
    public sealed class RegionFadeOverlay : MonoBehaviour
    {
        private static RegionFadeOverlay _instance;

        /// <summary>Self-install once when the game starts — no builder/scene wiring needed.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Install()
        {
            if (_instance != null) return;
            var go = new GameObject("RegionFadeOverlay");
            _instance = go.AddComponent<RegionFadeOverlay>();
        }

        [Tooltip("How long the cover takes to fade from black to clear (real seconds). A brief voyage flash.")]
        [SerializeField] private float _fadeSeconds = RegionFade.DefaultFadeSeconds;
        [Tooltip("Show a brief arrival card (the arrived scene's name) over the fade.")]
        [SerializeField] private bool _showArrivalCard = true;

        private Image _black;
        private Text _arrivalLabel;
        private float _elapsed;
        private bool _fading;
        private bool _subscribed;
        private bool _loading;
        private float _applied = -1f;   // the alpha last written, so an idle cover writes nothing per frame

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Build();
            Apply(0f); // start fully transparent (idle)
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void OnDestroy()
        {
            Unsubscribe();
            if (_instance == this) _instance = null;
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            EventBus.Subscribe<RegionTravelCover>(OnTravelCover);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            EventBus.Unsubscribe<RegionTravelCover>(OnTravelCover);
            _subscribed = false;
        }

        // Cover the snap: when the active region changes, flash black THIS frame (before it renders, so
        // the reposition done in the same event is never seen un-faded), then fade in over Update.
        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            if (!RegionFade.ShouldCover(previous.name, next.name)) return;
            // A SEAM crossing (ADR 0057) brings its own momentary cover, already black at this instant
            // (GameServices.SeamCoverAlpha, owner ruling on PR 1223): no flash, no slow clear and no
            // arrival card on top of it. Door and passage hops never set it, so they are unchanged.
            if (GameServices.SeamCoverAlpha > 0f) return;

            _elapsed = 0f;
            _fading = true; // restart the single fade — a re-trigger never stacks (no double-fade)

            if (_arrivalLabel != null)
            {
                bool show = _showArrivalCard;
                _arrivalLabel.enabled = show;
                if (show) _arrivalLabel.text = RegionFade.ArrivalTitle(next.name);
            }
            Apply(1f); // flash black immediately
        }

        private void Update()
        {
            float alpha = 0f;
            if (_loading) alpha = 1f;
            else if (_fading)
            {
                // Unscaled so the cover still clears if the world is paused (a menu/dialogue) during a hop.
                _elapsed += Time.unscaledDeltaTime;
                alpha = RegionFade.AlphaAfter(_elapsed, _fadeSeconds);
                if (alpha <= 0f)
                {
                    _fading = false; // fade always completes → no stuck-black
                    if (_arrivalLabel != null) _arrivalLabel.enabled = false;
                }
            }

            // The seam crossing's momentary fade rides this same cover (one fade system): whichever wants
            // more black wins, and the seam's own ramp always ends at 0, so it can never stick either.
            alpha = Mathf.Max(alpha, Mathf.Clamp01(GameServices.SeamCoverAlpha));
            if (alpha != _applied) Apply(alpha);
        }

        private void OnTravelCover(RegionTravelCover cover)
        {
            _loading = cover.IsLoading;
            _elapsed = 0f;
            _fading = true;
            Apply(1f);
        }

        private void Apply(float alpha)
        {
            _applied = alpha;
            if (_black != null)
            {
                var c = _black.color; c.a = alpha; _black.color = c;
                _black.enabled = alpha > 0f; // fully clear → render nothing
            }
            if (_arrivalLabel != null && _arrivalLabel.enabled)
            {
                var tc = _arrivalLabel.color; tc.a = alpha; _arrivalLabel.color = tc;
            }
        }

        // ---- construction (code-driven, no prefab) ------------------------------------------

        private void Build()
        {
            var canvasGo = new GameObject("RegionFade_Canvas", typeof(Canvas), typeof(PixelSnappedCanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // above the HUD (100) and the sell screen (200): a full cover

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            // Fullscreen black — NEVER a raycast target (must not eat gameplay/HUD input).
            var blackGo = new GameObject("Black", typeof(RectTransform), typeof(Image));
            blackGo.transform.SetParent(canvasGo.transform, false);
            var brt = (RectTransform)blackGo.transform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            _black = blackGo.GetComponent<Image>();
            _black.color = new Color(0f, 0f, 0f, 0f);
            _black.raycastTarget = false;
            _black.enabled = false;

            // Centred arrival card — fades with the black (most legible on black, gone as the world reveals).
            var labelGo = new GameObject("Arrival", typeof(RectTransform), typeof(Text), typeof(Outline));
            labelGo.transform.SetParent(canvasGo.transform, false);
            var lrt = (RectTransform)labelGo.transform;
            lrt.anchorMin = new Vector2(0.5f, 0.5f); lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.anchoredPosition = Vector2.zero;
            lrt.sizeDelta = new Vector2(900f, 140f);
            _arrivalLabel = labelGo.GetComponent<Text>();
            _arrivalLabel.font = DefaultFont();
            _arrivalLabel.fontSize = _arrivalLabel.font != null && _arrivalLabel.font.name == HarbourType.FontName
                ? HarbourType.SizeNearest(56) : 56;
            _arrivalLabel.alignment = TextAnchor.MiddleCenter;
            _arrivalLabel.color = new Color(1f, 1f, 1f, 0f);
            _arrivalLabel.raycastTarget = false;
            _arrivalLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _arrivalLabel.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = labelGo.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            _arrivalLabel.enabled = false;
        }

        // The pixel face, one face for everything the player reads. The built-in font is only the
        // fallback for a box where the face is not imported (Unity 6 removed Arial.ttf from Resources).
        private static Font DefaultFont()
        {
            var f = HarbourType.Load();
            if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }
    }
}
