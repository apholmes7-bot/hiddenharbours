using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The dialogue panel view (VS-21). Self-contained &amp; code-driven like <c>HudController</c>: it
    /// builds its own ScreenSpaceOverlay Canvas — a panel with a portrait, a nameplate, the line
    /// text, and an "E ▸" continue hint — in <see cref="Awake"/>, so it needs no prefab and works
    /// headless. The imported <c>DialoguePanel</c>/<c>NamePlate</c> art is used when wired; if a
    /// sprite is missing it falls back to a tinted rect so the greybox still runs.
    ///
    /// It is a pure view: the <see cref="WorldInteractor"/> drives it (Play / Advance from the
    /// Interact key) and the sequencing lives in the testable <see cref="DialogueRunner"/>. While a
    /// conversation is up it raises <see cref="InteractionGate"/> so the shared Interact key doesn't
    /// also board the dory underneath (the cross-module seam — see InteractionGate). Reads/owns no
    /// other module's types; talks to the rest of the game only through Core.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class DialoguePresenter : MonoBehaviour
    {
        [Header("Panel art (optional — falls back to tinted rects)")]
        [SerializeField] private Sprite _panelSprite;
        [SerializeField] private Sprite _nameplateSprite;

        private GameObject _root;
        private Image _portrait;
        private Text _nameText;
        private Text _bodyText;

        private DialogueRunner _runner;
        private Action _onComplete;

        public bool IsShowing { get; private set; }

        private void Awake()
        {
            BuildCanvas();
            HideRoot();
        }

        private void OnDisable()
        {
            // A panel destroyed/disabled mid-line must never leave interaction wedged off.
            if (IsShowing) InteractionGate.Reset();
        }

        // ---- public API (driven by WorldInteractor) -----------------------------------------

        /// <summary>Begin showing a conversation. Empty/null lines complete immediately (no-op view).</summary>
        public void Play(IReadOnlyList<DialogueLine> lines, Action onComplete = null)
        {
            _runner = new DialogueRunner(lines);
            _runner.Open();
            _onComplete = onComplete;

            if (!_runner.IsOpen) { Finish(); return; }   // nothing to show

            IsShowing = true;
            InteractionGate.IsBlocked = true;
            ShowRoot();
            Render(_runner.Current);
        }

        /// <summary>Advance to the next line, or close after the last. No-op if nothing is showing.</summary>
        public void Advance()
        {
            if (!IsShowing) return;
            if (_runner.Advance())
                Render(_runner.Current);
            else
                Finish();
        }

        /// <summary>Close the panel now (cancel). Fires the completion callback like a normal close.</summary>
        public void Close()
        {
            if (!IsShowing) return;
            _runner.Close();
            Finish();
        }

        private void Finish()
        {
            IsShowing = false;
            InteractionGate.IsBlocked = false;
            HideRoot();
            var cb = _onComplete;
            _onComplete = null;
            cb?.Invoke();
        }

        // ---- rendering ----------------------------------------------------------------------

        private void Render(in DialogueLine line)
        {
            if (_nameText != null) _nameText.text = line.Speaker ?? "";
            if (_bodyText != null) _bodyText.text = line.Text ?? "";
            if (_portrait != null)
            {
                bool has = line.Portrait != null;
                _portrait.enabled = has;
                if (has) _portrait.sprite = line.Portrait;
            }
        }

        private void ShowRoot() { if (_root != null) _root.SetActive(true); }
        private void HideRoot() { if (_root != null) _root.SetActive(false); }

        // ---- canvas construction (code-driven, no prefab) -----------------------------------

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("Dialogue_Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110; // above the HUD (100)
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            // Root holder (toggled on/off) anchored along the bottom.
            _root = new GameObject("DialogueRoot", typeof(RectTransform));
            _root.transform.SetParent(canvasGo.transform, false);
            var rootRt = _root.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0.5f, 0f);
            rootRt.anchorMax = new Vector2(0.5f, 0f);
            rootRt.pivot = new Vector2(0.5f, 0f);
            rootRt.anchoredPosition = new Vector2(0f, 24f);
            rootRt.sizeDelta = new Vector2(900f, 220f);

            // Panel background (imported art, or a dark slate fallback).
            var panel = MakeImage(rootRt, "Panel", _panelSprite, new Color(0.10f, 0.13f, 0.16f, 0.96f));
            Stretch(panel.rectTransform);

            // Portrait box on the left.
            _portrait = MakeImage(rootRt, "Portrait", null, new Color(1f, 1f, 1f, 1f));
            _portrait.preserveAspect = true;
            var pr = _portrait.rectTransform;
            pr.anchorMin = new Vector2(0f, 0f); pr.anchorMax = new Vector2(0f, 1f);
            pr.pivot = new Vector2(0f, 0.5f);
            pr.anchoredPosition = new Vector2(20f, 0f);
            pr.sizeDelta = new Vector2(180f, -36f); // square-ish, inset top/bottom
            _portrait.enabled = false;

            // Nameplate (imported art, or a warm plank fallback) with the speaker name on it.
            var nameplate = MakeImage(rootRt, "Nameplate", _nameplateSprite, new Color(0.20f, 0.16f, 0.10f, 0.98f));
            var npr = nameplate.rectTransform;
            npr.anchorMin = new Vector2(0f, 1f); npr.anchorMax = new Vector2(0f, 1f);
            npr.pivot = new Vector2(0f, 1f);
            npr.anchoredPosition = new Vector2(210f, -10f);
            npr.sizeDelta = new Vector2(300f, 44f);
            _nameText = MakeText(nameplate.rectTransform, "Name", TextAnchor.MiddleCenter, 26);
            Stretch(_nameText.rectTransform);

            // Body text fills the area right of the portrait, below the nameplate.
            _bodyText = MakeText(rootRt, "Body", TextAnchor.UpperLeft, 28);
            var br = _bodyText.rectTransform;
            br.anchorMin = new Vector2(0f, 0f); br.anchorMax = new Vector2(1f, 1f);
            br.pivot = new Vector2(0.5f, 0.5f);
            br.offsetMin = new Vector2(216f, 18f);
            br.offsetMax = new Vector2(-24f, -60f);
            _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _bodyText.verticalOverflow = VerticalWrapMode.Truncate;

            // "E ▸" continue hint, bottom-right.
            var hint = MakeText(rootRt, "ContinueHint", TextAnchor.LowerRight, 22);
            hint.text = WorldStrings.ContinueHint;
            hint.color = new Color(1f, 1f, 1f, 0.7f);
            var hr = hint.rectTransform;
            hr.anchorMin = new Vector2(1f, 0f); hr.anchorMax = new Vector2(1f, 0f);
            hr.pivot = new Vector2(1f, 0f);
            hr.anchoredPosition = new Vector2(-20f, 12f);
            hr.sizeDelta = new Vector2(120f, 30f);
        }

        private static Image MakeImage(RectTransform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            if (sprite != null) { img.sprite = sprite; img.color = Color.white; }
            else img.color = color;     // fallback tint when the art isn't imported
            img.raycastTarget = false;
            return img;
        }

        private static Text MakeText(RectTransform parent, string name, TextAnchor align, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = DefaultFont();
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = Color.white;
            text.raycastTarget = false;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Font DefaultFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }
    }
}
