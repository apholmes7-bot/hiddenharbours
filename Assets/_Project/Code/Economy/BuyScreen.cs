using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// The stall BUY SCREEN (VS-16 ui-ux side) — buying stops being a blind dev keypress. A
    /// self-building, code-driven overlay (no prefab — same pattern as <see cref="SellScreen"/>/
    /// RodGaugeView/HudController) that a stall's buy interaction opens instead of insta-buying:
    /// it lists every offer the stall carries (whatever <see cref="Shipwright"/>/<see cref="GearShop"/>/
    /// <see cref="LicenseVendor"/> components sit on the stall GameObject, each wired to its Def asset —
    /// content is data, ADR 0003), shows name/price/description, disables what you can't afford or
    /// already own (redundant-coded: greyed AND a status line, never colour alone), and Confirm routes
    /// through the vendors' EXISTING no-arg seams (<c>TryBuy()</c>/<c>TryRepair()</c>) — the screen is a
    /// skin over the purchase flow, never a second implementation (money, save writes, and the Core
    /// events all stay in the vendors).
    ///
    /// <para>Lives in the Economy module (not HiddenHarbours.UI) because it reads the vendor components
    /// + offer Defs; the UI assembly is deliberately Core-only. Same precedent as <see cref="SellScreen"/>
    /// (cross-lane into economy-sim's folder — flagged in the PR). Pure row logic is in the testable
    /// <see cref="BuyLogic"/>/<see cref="BuyCatalog"/>; this class is just the screen.</para>
    ///
    /// <para><b>Input.</b> Pointer clicks via the InputSystemUIInputModule; keyboard/gamepad navigate
    /// the uGUI buttons (automatic navigation + an initial selection is set every refresh), Submit
    /// confirms, and Esc / gamepad East closes. New Input System only — never legacy
    /// <c>UnityEngine.Input</c>. No per-frame allocations: rows rebuild only on open and after a
    /// purchase.</para>
    /// </summary>
    public sealed class BuyScreen : MonoBehaviour
    {
        private static BuyScreen _instance;

        /// <summary>True while a buy screen is open (drivers can skip re-opening).</summary>
        public static bool IsOpen => _instance != null;

        private GameObject _stall;
        private readonly List<BuyRow> _rows = new();
        private readonly List<GameObject> _rowButtons = new();
        private string _selectedId;

        private const float PanelW = 920f, PanelH = 540f;

        // ---- built UI refs ------------------------------------------------------------------
        private RectTransform _rowList;
        private Text _moneyLabel;
        private Image _detailIcon;
        private Text _detailName;
        private Text _detailFlavor;
        private Text _detailNote;
        private Text _priceLabel;
        private Text _statusLabel;
        private Button _buyButton;
        private Text _buyLabel;

        // Palette mirrors the SellScreen so the two market screens read as one family.
        private static readonly Color Backdrop   = new Color(0f, 0f, 0f, 0.72f);
        private static readonly Color PanelColor = new Color(0.10f, 0.12f, 0.15f, 0.98f);
        private static readonly Color Accent     = new Color(0.95f, 0.85f, 0.45f); // buy = coin gold (sell is green)
        private static readonly Color ButtonBg   = new Color(0.20f, 0.24f, 0.30f, 1f);
        private static readonly Color SelectedBg = new Color(0.42f, 0.38f, 0.24f, 1f);

        // Loc-seam literals (HudStrings convention: centralised consts now, loc tables route here later).
        private const string TitleText       = "For Sale";
        private const string OffersHeader    = "On offer";
        private const string BuyVerb         = "Buy";
        private const string RepairVerb      = "Repair";
        private const string CloseText       = "Close";
        private const string OwnedSuffix     = "   - owned";
        private const string StatusOwned     = "Already yours.";
        private const string StatusTooDear   = "You can't afford this yet.";
        private const string StatusEmpty     = "Nothing for sale here.";

        /// <summary>
        /// Open the buy screen for a stall GameObject (the stall's buy interaction calls this). Every
        /// vendor component on the stall contributes its offer. Reuses the open screen if there is one.
        /// </summary>
        public static BuyScreen Open(GameObject stall)
        {
            if (stall == null) return null;
            if (_instance == null)
            {
                var go = new GameObject("BuyScreen");
                _instance = go.AddComponent<BuyScreen>(); // Awake builds the canvas
            }
            _instance.BindStall(stall);
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

        private void Update()
        {
            // Close on Esc / gamepad East (the shared Cancel convention). New Input System only.
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) ||
                (pad != null && pad.buttonEast.wasPressedThisFrame))
                Close();
        }

        private void BindStall(GameObject stall)
        {
            _stall = stall;
            Refresh();
        }

        // ---- data → view --------------------------------------------------------------------

        private void Refresh()
        {
            int money = GameServices.Wallet?.Money ?? 0;
            BuyCatalog.Build(_stall, money, GameServices.Save?.Current, GameServices.Licenses, _rows);

            RebuildRowButtons();

            // Keep the selection if that offer still exists (a bought damaged boat keeps its id as its
            // row turns into the repair row), else select the first row.
            int idx = IndexOf(_selectedId);
            if (idx < 0 && _rows.Count > 0) idx = 0;
            Select(idx);
            UpdateMoney();
        }

        private int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i].Id == id) return i;
            return -1;
        }

        private void RebuildRowButtons()
        {
            foreach (var b in _rowButtons) if (b != null) Destroy(b);
            _rowButtons.Clear();

            float y = 0f;
            for (int i = 0; i < _rows.Count; i++)
            {
                BuyRow row = _rows[i];
                string label = RowLabel(row);

                // Optional icon at the row's left, resolved by stable id via the Core IconRegistry
                // (never a Def reference) — reinforcement only, the text always carries the row.
                Sprite icon = IconRegistry.Get(row.Id);
                float labelPad = icon != null ? RowIconBox + 8f : 0f;

                int index = i;
                GameObject btn = MakeButton(_rowList, row.Id ?? ("row" + i), label, 0f, y, 380f, 52f,
                    () => Select(index), accent: false, align: TextAnchor.MiddleLeft, out _,
                    leftPad: labelPad);
                if (icon != null)
                    MakeRowIcon((RectTransform)btn.transform, icon);
                _rowButtons.Add(btn);
                y -= 58f;
            }
        }

        // Row label: "Name — ₲price" (+ owned marker). Built on refresh only, never per frame.
        private static string RowLabel(in BuyRow row)
        {
            string verb = row.Quote.Kind == BuyRowKind.BoatRepair ? "  (" + RepairVerb.ToLowerInvariant() + ")" : "";
            string owned = row.Quote.Owned ? OwnedSuffix : "   " + Money(row.Quote.Price);
            return row.DisplayName + verb + owned;
        }

        private void Select(int index)
        {
            bool valid = index >= 0 && index < _rows.Count;
            _selectedId = valid ? _rows[index].Id : null;

            for (int i = 0; i < _rowButtons.Count; i++)
            {
                var b = _rowButtons[i];
                if (b == null) continue;
                var img = b.GetComponent<Image>();
                if (img != null) img.color = (i == index) ? SelectedBg : ButtonBg;
            }

            if (!valid)
            {
                _detailName.text = "-";
                _detailFlavor.text = "";
                _detailNote.text = "";
                _priceLabel.text = "";
                _statusLabel.text = _rows.Count == 0 ? StatusEmpty : "";
                if (_detailIcon != null) _detailIcon.enabled = false;
                _buyButton.interactable = false;
                _buyLabel.text = BuyVerb;
                FocusFirstSelectable();
                return;
            }

            BuyRow row = _rows[index];
            _detailName.text = row.DisplayName;
            _detailFlavor.text = row.Flavor;
            _detailNote.text = row.Note;

            if (_detailIcon != null)
            {
                Sprite icon = IconRegistry.Get(row.Id);
                _detailIcon.sprite = icon;
                _detailIcon.enabled = icon != null;
            }

            string verb = row.Quote.Kind == BuyRowKind.BoatRepair ? RepairVerb : BuyVerb;
            _priceLabel.text = row.Quote.Owned ? "" : verb + " for " + Money(row.Quote.Price);
            _statusLabel.text = row.Quote.Owned ? StatusOwned
                              : row.Quote.CanBuy ? ""
                              : StatusTooDear;

            // Disabled is redundant-coded: the button greys (uGUI disabled tint) AND the status line
            // says why — never colour alone (charter DoD).
            _buyButton.interactable = row.Quote.CanBuy;
            _buyLabel.text = row.Quote.Owned ? "Owned" : verb + "  " + Money(row.Quote.Price);

            FocusFirstSelectable();
        }

        private void UpdateMoney()
        {
            if (_moneyLabel != null && GameServices.Wallet != null)
                _moneyLabel.text = Money(GameServices.Wallet.Money);
        }

        // Keyboard/gamepad entry point: keep something sensible selected so navigate/submit work
        // without a pointer. Prefer the selected row's button, else the Buy button, else Close.
        private void FocusFirstSelectable()
        {
            var es = EventSystem.current;
            if (es == null) return;
            int idx = IndexOf(_selectedId);
            GameObject target = (idx >= 0 && idx < _rowButtons.Count && _rowButtons[idx] != null)
                ? _rowButtons[idx]
                : _buyButton != null ? _buyButton.gameObject : null;
            if (target != null) es.SetSelectedGameObject(target);
        }

        // ---- actions ------------------------------------------------------------------------

        private void OnConfirm()
        {
            int idx = IndexOf(_selectedId);
            if (idx < 0) return;
            BuyRow row = _rows[idx];
            if (!row.Quote.CanBuy || row.Vendor == null) return;

            // Route through the vendors' EXISTING purchase seams — the same path the dev keypress used.
            // They spend the wallet, write the save, and raise the Core events; we only re-render.
            switch (row.Quote.Kind)
            {
                case BuyRowKind.Boat:       ((Shipwright)row.Vendor).TryBuy();      break;
                case BuyRowKind.BoatRepair: ((Shipwright)row.Vendor).TryRepair();   break;
                case BuyRowKind.Gear:       ((GearShop)row.Vendor).TryBuy();        break;
                case BuyRowKind.License:    ((LicenseVendor)row.Vendor).TryBuy();   break;
                case BuyRowKind.Pot:        ((PotShop)row.Vendor).TryBuy();         break;
            }
            Refresh();   // ownership/affordability changed → rows, states, money all re-read
        }

        private void Close() => Destroy(gameObject);

        // ---- helpers ------------------------------------------------------------------------

        private static string Money(int amount)
            => "₲" + amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            // A UI screen needs an EventSystem for clicks/navigation; the always-on HUD never did.
            // Use the new-Input-System module (the project's input backend) with its default actions
            // so pointer, keyboard-navigate, submit, and cancel are all processed (same as SellScreen).
            var es = new GameObject("EventSystem", typeof(EventSystem));
            var module = es.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
            DontDestroyOnLoad(es);
        }

        // ---- construction (code-driven, no prefab — mirrors SellScreen) ---------------------

        private void Build()
        {
            var canvasGo = new GameObject("BuyScreen_Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 205; // above the HUD (100) and the sell screen (200) if both are up

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

            MakeText(panel, TitleText, 40, TextAnchor.UpperLeft, 24f, -16f, 520f, 52f);
            _moneyLabel = MakeText(panel, "₲0", 36, TextAnchor.UpperRight, 560f, -16f, 336f, 52f);
            _moneyLabel.color = Accent;

            MakeText(panel, OffersHeader, 26, TextAnchor.UpperLeft, 24f, -84f, 360f, 34f);
            var listHost = new GameObject("OfferList", typeof(RectTransform));
            listHost.transform.SetParent(panel, false);
            _rowList = listHost.GetComponent<RectTransform>();
            _rowList.anchorMin = new Vector2(0f, 1f); _rowList.anchorMax = new Vector2(0f, 1f);
            _rowList.pivot = new Vector2(0f, 1f);
            _rowList.anchoredPosition = new Vector2(24f, -124f);
            _rowList.sizeDelta = new Vector2(380f, 360f);

            // Detail panel: icon (left) + name, then flavor, condition note, price, and status.
            _detailIcon   = MakeDetailIcon(panel, 430f, -78f, 56f);
            _detailName   = MakeText(panel, "-", 32, TextAnchor.UpperLeft, 430f + 66f, -84f, 400f, 44f);
            _detailFlavor = MakeText(panel, "", 24, TextAnchor.UpperLeft, 430f, -144f, 466f, 84f);
            _detailFlavor.horizontalOverflow = HorizontalWrapMode.Wrap;
            _detailFlavor.color = new Color(0.85f, 0.88f, 0.92f);
            _detailNote   = MakeText(panel, "", 24, TextAnchor.UpperLeft, 430f, -232f, 466f, 62f);
            _detailNote.horizontalOverflow = HorizontalWrapMode.Wrap;
            _detailNote.color = new Color(0.95f, 0.75f, 0.55f); // a warm "mind this" tone (+ the words themselves)
            _priceLabel   = MakeText(panel, "", 34, TextAnchor.UpperLeft, 430f, -298f, 466f, 44f);
            _priceLabel.color = Accent;
            _statusLabel  = MakeText(panel, "", 26, TextAnchor.UpperLeft, 430f, -344f, 466f, 36f);

            GameObject buyGo = MakeButton(panel, "Buy", BuyVerb, 430f, -400f, 466f, 56f,
                OnConfirm, accent: true, align: TextAnchor.MiddleCenter, out _buyLabel);
            _buyButton = buyGo.GetComponent<Button>();

            MakeButton(panel, "Close", CloseText, 24f, -470f, 280f, 56f,
                Close, accent: false, align: TextAnchor.MiddleCenter, out _);
        }

        // ---- UI builders (all anchor to parent TOP-LEFT; x right, y down — SellScreen's) ----

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
        private const float RowIconBox = 40f;

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
            img.preserveAspect = true;
            img.raycastTarget = false;   // clicks pass through to the row button beneath
        }

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

        // Unity 6 removed Arial.ttf from Resources; LegacyRuntime.ttf is the built-in fallback.
        private static Font DefaultFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }
    }
}
