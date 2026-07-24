using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// The wharf SELL SCREEN (VS-18). A self-building, code-driven overlay (no prefab, no
    /// GreyboxBuilder — same pattern as HudController) that the wharf opens instead of
    /// instant-selling: pick a species from the hold, drag a quantity slider and watch the MARGINAL
    /// price + running total fall LIVE as the market self-glutts, then Confirm — or "Sell all of type"
    /// / "Sell all". The displayed total is exactly the coin paid (both come from <see cref="SellPricing"/>).
    ///
    /// <para>Lives in the Economy module (not HiddenHarbours.UI) because it reads the live
    /// <see cref="Market"/> + <see cref="MarketMath"/>; the UI assembly is deliberately Core-only.
    /// Co-locating the view with the data it reads mirrors HudController (which lives in UI).
    /// Cross-lane into economy-sim's folder — flagged in the PR. The economics are in the testable
    /// <see cref="SellService"/>; this class is just the screen.</para>
    /// </summary>
    public sealed class SellScreen : MonoBehaviour
    {
        private static SellScreen _instance;

        private IHold _hold;
        private IWallet _wallet;
        private Market _market;

        private const float PanelW = 920f, PanelH = 540f;

        // ---- built UI refs ------------------------------------------------------------------
        private RectTransform _speciesList;
        private Text _moneyLabel;
        private Image _detailIcon;
        private Text _detailName;
        private Text _marginalLabel;
        private Text _totalLabel;
        private Text _qtyLabel;
        private Slider _qtySlider;
        private Text _sellTypeLabel;

        private readonly List<GameObject> _speciesButtons = new();
        private string _selectedSpecies;

        private static readonly Color Backdrop   = new Color(0f, 0f, 0f, 0.72f);
        private static readonly Color PanelColor = new Color(0.10f, 0.12f, 0.15f, 0.98f);
        private static readonly Color Accent     = new Color(0.55f, 0.95f, 0.55f);
        private static readonly Color ButtonBg   = new Color(0.20f, 0.24f, 0.30f, 1f);
        private static readonly Color SelectedBg = new Color(0.30f, 0.42f, 0.30f, 1f);

        /// <summary>
        /// Open the sell screen for a hold + wallet (the wharf calls this on the sell interaction).
        /// Locates the scene's Market itself. Reuses the open screen if there already is one.
        /// </summary>
        public static SellScreen Open(IHold hold, IWallet wallet)
        {
            if (hold == null || wallet == null) return null;
            if (_instance == null)
            {
                var go = new GameObject("SellScreen");
                _instance = go.AddComponent<SellScreen>(); // Awake builds the canvas
            }
            _instance.Bind(hold, wallet);
            return _instance;
        }

        private void Awake()
        {
            EnsureEventSystem();
            Build();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Bind(IHold hold, IWallet wallet)
        {
            _hold = hold;
            _wallet = wallet;
            _market = FindAnyObjectByType<Market>();   // one Market in the scene (on the Wharf)
            Refresh();
        }

        // ---- data → view --------------------------------------------------------------------

        private void Refresh()
        {
            if (_hold == null || _hold.UsedUnits == 0) { Close(); return; }   // nothing left → done

            RebuildSpeciesList();

            // Keep the selection if it still exists, else select the first species.
            if (_selectedSpecies == null || SellService.CountOf(_hold, _selectedSpecies) == 0)
                _selectedSpecies = _speciesButtons.Count > 0 ? _speciesButtons[0].name : null;

            SelectSpecies(_selectedSpecies);
            UpdateMoney();
        }

        private void RebuildSpeciesList()
        {
            foreach (var b in _speciesButtons) if (b != null) Destroy(b);
            _speciesButtons.Clear();

            var seen = new HashSet<string>();
            var items = _hold.Items;
            float y = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                CatchItem it = items[i];
                if (!seen.Add(it.SpeciesId)) continue;

                int count = SellService.CountOf(_hold, it.SpeciesId);
                string id = it.SpeciesId;
                string label = it.DisplayName + "   ×" + count;

                // Show the species icon at the row's left (resolved by id via the Core IconRegistry — the
                // sell screen never references the Fishing def). When an icon resolves, indent the label
                // past it; with no icon the row falls back to text-only (icon is reinforcement, not the
                // only channel). The name+count text always carries the row.
                Sprite icon = IconRegistry.Get(id);
                float labelPad = icon != null ? RowIconBox + 8f : 0f;

                GameObject btn = MakeButton(_speciesList, id, label, 0f, y, 380f, 52f,
                    () => SelectSpecies(id), accent: false, align: TextAnchor.MiddleLeft, out _,
                    leftPad: labelPad);
                if (icon != null)
                    MakeRowIcon((RectTransform)btn.transform, icon);
                _speciesButtons.Add(btn);
                y -= 58f;
            }
        }

        private void SelectSpecies(string speciesId)
        {
            _selectedSpecies = speciesId;
            int count = SellService.CountOf(_hold, speciesId);

            foreach (var b in _speciesButtons)
            {
                if (b == null) continue;
                var img = b.GetComponent<Image>();
                if (img != null) img.color = (b.name == speciesId) ? SelectedBg : ButtonBg;
            }

            if (count <= 0 || speciesId == null)
            {
                _detailName.text = "—";
                if (_detailIcon != null) _detailIcon.enabled = false;
                _qtySlider.gameObject.SetActive(false);
                _marginalLabel.text = "";
                _totalLabel.text = "";
                _qtyLabel.text = "";
                return;
            }

            CatchItem sample = SampleOf(speciesId);
            _detailName.text = sample.DisplayName;

            // The selected species' icon beside the name (resolved by id via the Core IconRegistry).
            if (_detailIcon != null)
            {
                Sprite icon = IconRegistry.Get(speciesId);
                _detailIcon.sprite = icon;
                _detailIcon.enabled = icon != null;
            }
            if (_sellTypeLabel != null) _sellTypeLabel.text = "Sell all " + sample.DisplayName;

            _qtySlider.gameObject.SetActive(true);
            _qtySlider.minValue = 1;
            _qtySlider.maxValue = count;
            _qtySlider.wholeNumbers = true;
            _qtySlider.SetValueWithoutNotify(count);   // default to the lot; drag down to glutt-check
            UpdatePriceLabels();
        }

        private void OnSliderChanged(float _) => UpdatePriceLabels();

        private void UpdatePriceLabels()
        {
            if (_selectedSpecies == null) return;
            int have = SellService.CountOf(_hold, _selectedSpecies);
            int q = Mathf.Clamp(Mathf.RoundToInt(_qtySlider.value), 0, have);
            CatchItem sample = SampleOf(_selectedSpecies);
            float supply = _market != null ? _market.SupplyOf(sample.Category) : 0f;
            float demand = _market != null ? _market.DemandFor(sample.Category) : 1f; // per-category demand

            int marginal = q > 0
                ? SellPricing.MarginalPrice(sample.BaseValue, sample.SupplyElasticity, supply, q - 1, demand)
                : 0;
            int total = SellService.Quote(_hold, _market, _selectedSpecies, q);

            _qtyLabel.text = "Qty " + q + " / " + have;
            _marginalLabel.text = "This unit: " + Money(marginal);
            _totalLabel.text = "Total: " + Money(total);
        }

        private void UpdateMoney()
        {
            if (_moneyLabel != null && _wallet != null) _moneyLabel.text = Money(_wallet.Money);
        }

        // ---- actions ------------------------------------------------------------------------

        private void OnConfirm()
        {
            if (_selectedSpecies == null) return;
            SellService.SellSpecies(_hold, _wallet, _market, _selectedSpecies, Mathf.RoundToInt(_qtySlider.value));
            Refresh();
        }

        private void OnSellAllOfType()
        {
            if (_selectedSpecies == null) return;
            SellService.SellSpecies(_hold, _wallet, _market, _selectedSpecies,
                SellService.CountOf(_hold, _selectedSpecies));
            Refresh();
        }

        private void OnSellEverything()
        {
            SellService.SellAll(_hold, _wallet, _market);
            Refresh();   // empties the hold → Close()
        }

        private void Close() => Destroy(gameObject);

        // ---- helpers ------------------------------------------------------------------------

        private CatchItem SampleOf(string speciesId)
        {
            var items = _hold.Items;
            for (int i = 0; i < items.Count; i++)
                if (items[i].SpeciesId == speciesId) return items[i];
            return default;
        }

        private static string Money(int amount)
            => "₲" + amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            // A UI screen needs an EventSystem to receive clicks/drags; the always-on HUD never did,
            // so the greybox has none. Use the new-Input-System module (the project's input backend)
            // and assign its built-in default actions so pointer clicks/drags are processed.
            var es = new GameObject("EventSystem", typeof(EventSystem));
            var module = es.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
            DontDestroyOnLoad(es);
        }

        // ---- construction (code-driven, no prefab) ------------------------------------------

        private void Build()
        {
            var canvasGo = new GameObject("SellScreen_Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200; // above the HUD (100) and the rod gauge (90)

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            var canvasRt = (RectTransform)canvasGo.transform;

            // Full-screen backdrop — dims the world and eats clicks behind the screen.
            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(canvasRt, false);
            var bdRt = backdrop.GetComponent<RectTransform>();
            bdRt.anchorMin = Vector2.zero; bdRt.anchorMax = Vector2.one;
            bdRt.offsetMin = Vector2.zero; bdRt.offsetMax = Vector2.zero;
            var bdImg = backdrop.GetComponent<Image>();
            bdImg.color = Backdrop; bdImg.raycastTarget = true;

            // Centred fixed-size panel. All children are positioned from its TOP-LEFT (y grows down).
            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(canvasRt, false);
            var panel = panelGo.GetComponent<RectTransform>();
            panel.anchorMin = new Vector2(0.5f, 0.5f); panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(PanelW, PanelH);
            var panelImg = panelGo.GetComponent<Image>();
            panelImg.color = PanelColor; panelImg.raycastTarget = true;

            MakeText(panel, "Sell at the Wharf", 40, TextAnchor.UpperLeft, 24f, -16f, 520f, 52f);
            _moneyLabel = MakeText(panel, "₲0", 36, TextAnchor.UpperRight, 560f, -16f, 336f, 52f);
            _moneyLabel.color = Accent;

            MakeText(panel, "Your hold", 26, TextAnchor.UpperLeft, 24f, -84f, 360f, 34f);
            var listHost = new GameObject("SpeciesList", typeof(RectTransform));
            listHost.transform.SetParent(panel, false);
            _speciesList = listHost.GetComponent<RectTransform>();
            _speciesList.anchorMin = new Vector2(0f, 1f); _speciesList.anchorMax = new Vector2(0f, 1f);
            _speciesList.pivot = new Vector2(0f, 1f);
            _speciesList.anchoredPosition = new Vector2(24f, -124f);
            _speciesList.sizeDelta = new Vector2(380f, 360f);

            // Detail panel: the selected species' icon (left) + its name (indented past the icon).
            _detailIcon   = MakeDetailIcon(panel, 430f, -78f, 56f);
            _detailName   = MakeText(panel, "—", 32, TextAnchor.UpperLeft, 430f + 66f, -84f, 400f, 44f);
            _qtyLabel     = MakeText(panel, "", 26, TextAnchor.UpperLeft, 430f, -140f, 466f, 34f);
            _qtySlider    = MakeSlider(panel, 430f, -184f, 466f, 36f);
            _qtySlider.onValueChanged.AddListener(OnSliderChanged);
            _marginalLabel = MakeText(panel, "", 28, TextAnchor.UpperLeft, 430f, -232f, 466f, 36f);
            _totalLabel    = MakeText(panel, "", 34, TextAnchor.UpperLeft, 430f, -274f, 466f, 44f);
            _totalLabel.color = Accent;

            MakeButton(panel, "Confirm", "Confirm sale", 430f, -336f, 466f, 56f,
                OnConfirm, accent: true, align: TextAnchor.MiddleCenter, out _);
            MakeButton(panel, "SellType", "Sell all of type", 430f, -400f, 466f, 50f,
                OnSellAllOfType, accent: false, align: TextAnchor.MiddleCenter, out _sellTypeLabel);

            MakeButton(panel, "SellAll", "Sell ALL", 24f, -470f, 280f, 56f,
                OnSellEverything, accent: false, align: TextAnchor.MiddleCenter, out _);
            MakeButton(panel, "Close", "Close", 616f, -470f, 280f, 56f,
                Close, accent: false, align: TextAnchor.MiddleCenter, out _);
        }

        // ---- UI builders (all anchor to parent TOP-LEFT; x right, y down) -------------------

        private static Text MakeText(RectTransform parent, string text, int fontSize, TextAnchor align,
            float x, float y, float w, float h)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            var t = go.GetComponent<Text>();
            t.font = DefaultFont();
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var o = go.GetComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        private GameObject MakeButton(RectTransform parent, string name, string label,
            float x, float y, float w, float h, UnityAction onClick, bool accent, TextAnchor align,
            out Text labelText, float leftPad = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);

            var img = go.GetComponent<Image>();
            img.color = accent ? SelectedBg : ButtonBg;
            img.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(onClick);

            // leftPad reserves room for a row icon; for left-aligned labels add it to the base padding.
            float pad = (align == TextAnchor.MiddleLeft ? 14f : 0f) + leftPad;
            labelText = MakeText(rt, label, 26, align, pad, 0f, w - pad - (align == TextAnchor.MiddleLeft ? 14f : 0f), h);
            if (accent) labelText.color = Color.black;
            return go;
        }

        // ---- icon helpers (resolve by id through the Core IconRegistry) ---------------------
        // A square box the row icon fits inside (the fish art is 48×32 — preserveAspect keeps it crisp).
        private const float RowIconBox = 40f;

        // The icon at the left of a species row, vertically centred inside the 52px row, padded 8px in.
        private void MakeRowIcon(RectTransform row, Sprite icon)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(row, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(8f, 0f);
            rt.sizeDelta = new Vector2(RowIconBox, RowIconBox);
            var img = go.GetComponent<Image>();
            img.sprite = icon;
            img.preserveAspect = true;   // fish icons are 48×32 — never stretch them
            img.raycastTarget = false;   // clicks pass through to the row button beneath
        }

        // The larger icon beside the detail-panel species name (built once, sprite swapped per selection).
        private Image MakeDetailIcon(RectTransform parent, float x, float y, float size)
        {
            var go = new GameObject("DetailIcon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.GetComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.enabled = false;   // shown only when a selection resolves an icon
            return img;
        }

        // A minimal but functional uGUI slider (background + fill + draggable handle).
        private static Slider MakeSlider(RectTransform parent, float x, float y, float w, float h)
        {
            var go = new GameObject("QtySlider", typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);

            const float handleHalf = 16f;

            Stretch(MakeRect(rt, "Background", new Color(0.06f, 0.07f, 0.09f, 1f), true),
                new Vector2(0f, 0.25f), new Vector2(1f, 0.75f), Vector2.zero, Vector2.zero);

            var fillArea = MakeRect(rt, "Fill Area", Color.clear, false);
            Stretch(fillArea, new Vector2(0f, 0.25f), new Vector2(1f, 0.75f),
                new Vector2(handleHalf, 0f), new Vector2(-handleHalf, 0f));
            var fill = MakeRect(fillArea, "Fill", Accent, true);
            Stretch(fill, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            var handleArea = MakeRect(rt, "Handle Slide Area", Color.clear, false);
            Stretch(handleArea, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(handleHalf, 0f), new Vector2(-handleHalf, 0f));
            var handle = MakeRect(handleArea, "Handle", Color.white, true);
            handle.anchorMin = new Vector2(0f, 0f); handle.anchorMax = new Vector2(0f, 1f);
            handle.pivot = new Vector2(0.5f, 0.5f);
            handle.sizeDelta = new Vector2(2f * handleHalf, 0f);

            var slider = go.GetComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0; slider.maxValue = 1; slider.wholeNumbers = true;
            return slider;
        }

        private static RectTransform MakeRect(RectTransform parent, string name, Color color, bool image)
        {
            var go = image
                ? new GameObject(name, typeof(RectTransform), typeof(Image))
                : new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            if (image)
            {
                var img = go.GetComponent<Image>();
                img.color = color;
                img.raycastTarget = true;
            }
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
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
