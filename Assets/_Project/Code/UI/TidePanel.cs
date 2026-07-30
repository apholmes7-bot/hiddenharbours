using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// VS-06 — the <b>tide table the player reads</b>: today's and tomorrow's high and low waters, a
    /// marker for where "now" falls among them, and the standing read (height, making or ebbing, how
    /// long to the turn). This is how a crossing gets planned, which is the whole point of a tide that
    /// is a real force (P1).
    ///
    /// <para><b>It is paper, not a gauge.</b> The owner's standing direction is that information is
    /// diegetic — so this is drawn as the almanac page it would be: warm stock, ruled columns, ink.
    /// Nothing is bolted onto the HUD; the page is something you take out, read, and put away.</para>
    ///
    /// <para><b>Time is frozen while it is open.</b> Opening sets <c>IGameClock.IsPaused</c> — the
    /// project's one pause path — and closing restores whatever the clock was doing before, so a page
    /// opened during an already-paused moment does not resume the world on close. There is deliberately
    /// no second clock. Because the world is stopped, every value on the page is fixed for as long as it
    /// is up: the page is built ONCE on open and does no per-frame work at all (rule 7).</para>
    ///
    /// <para><b>Every value is computed, none stored.</b> Heights come from
    /// <c>IEnvironmentService.TideHeightAt</c> for the active region, and the turns are found by
    /// <see cref="TideAlmanac"/>, which walks the same <see cref="TideReadout.Derive"/> the HUD's gauge
    /// uses. Table, gauge and editor tool cannot drift apart, and nothing tide-shaped enters the save
    /// (rule 5).</para>
    ///
    /// <para>Self-building and code-driven (no prefab), matching <c>HudController</c> and the market
    /// screens. Reads state only through Core — the UI assembly references nothing but
    /// <c>HiddenHarbours.Core</c>, which structurally prevents reaching into Environment.</para>
    /// </summary>
    public sealed class TidePanel : MonoBehaviour
    {
        private static TidePanel _instance;

        /// <summary>True while the page is open (an opener can toggle rather than stack pages).</summary>
        public static bool IsOpen => _instance != null;

        // Sized for the worst honest page: a semidiurnal day's four turns plus the "now" mark, with
        // headroom for a shorter tidal period than canon. Rows must clear the footer rule at -(H-92).
        private const float PanelW = 860f, PanelH = 600f;
        private const float ColumnW = 380f;
        private const float RowH = 42f;
        private const float ColumnTop = -214f;      // the day heading
        private const float FirstRowY = -258f;      // the first turn under the heading's rule

        /// <summary>How many day-columns the sheet is ruled for. Today and tomorrow is the span a
        /// crossing is planned over; a wider page is a layout change, not a tuning one.</summary>
        private const int MaxColumns = 2;

        // Where the clock was before this page froze it, so closing restores rather than assumes.
        private bool _clockWasPaused;
        private bool _frozeClock;

        // Reused across pages so a redraw allocates no list.
        private readonly List<TideTurn> _turns = new();

        // The almanac's palette: aged stock and iron-gall ink. High/low waters carry a glyph AND the
        // words "High water"/"Low water", so colour is only ever reinforcement (accessibility §8).
        private static readonly Color Backdrop   = new Color(0f, 0f, 0f, 0.62f);
        private static readonly Color Paper      = new Color(0.898f, 0.855f, 0.761f, 0.99f);
        private static readonly Color PaperEdge  = new Color(0.792f, 0.737f, 0.627f, 1f);
        private static readonly Color Ink        = new Color(0.157f, 0.133f, 0.106f, 1f);
        private static readonly Color InkFaint   = new Color(0.157f, 0.133f, 0.106f, 0.55f);
        private static readonly Color Rule       = new Color(0.157f, 0.133f, 0.106f, 0.16f);
        private static readonly Color HighInk    = new Color(0.145f, 0.325f, 0.239f, 1f); // sea green
        private static readonly Color LowInk     = new Color(0.478f, 0.231f, 0.153f, 1f); // dried rust
        private static readonly Color NowInk     = new Color(0.604f, 0.294f, 0.106f, 1f); // the pencil mark
        private static readonly Color ButtonBg   = new Color(0.792f, 0.737f, 0.627f, 1f);

        /// <summary>
        /// Open the tide table (reusing the open page if there is one). Public and argument-free so any
        /// opener can call it — the keypress in <see cref="TidePanelInput"/> today, and a diegetic
        /// world interaction (the table pinned by the cottage door) whenever world-content wires one,
        /// without this class having to know which.
        /// </summary>
        public static TidePanel Open()
        {
            if (_instance == null)
            {
                var go = new GameObject("TidePanel");
                _instance = go.AddComponent<TidePanel>();   // Awake freezes time and builds the page
            }
            return _instance;
        }

        /// <summary>Close the open page, if any. Safe to call when nothing is open.</summary>
        public static void CloseIfOpen()
        {
            if (_instance != null) _instance.Close();
        }

        /// <summary>Open the page, or close it if it is already up.</summary>
        public static void Toggle()
        {
            if (_instance != null) _instance.Close();
            else Open();
        }

        private void Awake()
        {
            EnsureEventSystem();
            FreezeClock();
            Build();
        }

        private void OnDestroy()
        {
            ThawClock();
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            // Close on Esc / gamepad East — the project's shared Cancel convention (BuyScreen/SellScreen).
            // New Input System only; legacy UnityEngine.Input compiles here and then throws at runtime.
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) ||
                (pad != null && pad.buttonEast.wasPressedThisFrame))
                Close();
        }

        private void Close() => Destroy(gameObject);

        // ---- the frozen clock ------------------------------------------------------------------

        // Freeze through the clock's own pause flag — the one pause path (plan-to-m1 §7.8 builds the
        // pause menu on this same pattern). Remember the prior state: a page opened while the world was
        // already stopped must not start it again on close.
        private void FreezeClock()
        {
            var clock = GameServices.Clock;
            if (clock == null) return;
            _clockWasPaused = clock.IsPaused;
            clock.IsPaused = true;
            _frozeClock = true;
        }

        private void ThawClock()
        {
            if (!_frozeClock) return;
            var clock = GameServices.Clock;
            if (clock != null) clock.IsPaused = _clockWasPaused;
            _frozeClock = false;
        }

        // ---- the page ---------------------------------------------------------------------------

        private void Build()
        {
            var canvasGo = new GameObject("TidePanel_Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 210; // above the HUD (100) and the market screens (200/205)

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            var canvasRt = (RectTransform)canvasGo.transform;

            // Backdrop — dims the world behind the page and eats clicks meant for it.
            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(canvasRt, false);
            var bdRt = backdrop.GetComponent<RectTransform>();
            bdRt.anchorMin = Vector2.zero; bdRt.anchorMax = Vector2.one;
            bdRt.offsetMin = Vector2.zero; bdRt.offsetMax = Vector2.zero;
            var bdImg = backdrop.GetComponent<Image>();
            bdImg.color = Backdrop; bdImg.raycastTarget = true;

            // The sheet. A slightly larger, darker plate offset behind it reads as the paper's edge and
            // the shadow it throws. Built first so the page proper draws over it.
            MakePlate(canvasRt, "PaperEdge", PanelW + 10f, PanelH + 10f, PaperEdge, new Vector2(4f, -4f));
            RectTransform page = MakePlate(canvasRt, "Page", PanelW, PanelH, Paper);

            BuildHeader(page);

            // Resolve the page's inputs once. Missing services (boot, or an EditMode harness) fall back
            // to an honest "can't read it" page rather than plausible-looking wrong numbers (P1 truth).
            var clock = GameServices.Clock;
            var env = GameServices.Environment;
            GameConfig config = GameServices.Config;

            if (clock == null || env == null || config == null || config.SecondsPerDay <= 0f)
            {
                MakeText(page, HudStrings.TideTableUnavailable, 26, TextAnchor.UpperLeft,
                         40f, -150f, PanelW - 80f, 40f, InkFaint);
                BuildFooterControls(page);
                return;
            }

            TideTableSettings settings = GameServices.TideTable;
            double secondsPerDay = config.SecondsPerDay;
            float secondsPerHour = config.SecondsPerHour;
            double now = clock.TotalSeconds;

            double slopeDt = secondsPerHour * settings.SlopeStepHours;
            double scanStep = secondsPerHour * settings.ScanStepHours;
            double horizon = secondsPerHour * config.TidalPeriodHours;

            // The height function for the ACTIVE region, straight off the Core seam — the same source
            // the HUD gauge samples. Bound once here, so the scan reuses one delegate.
            Func<double, float> heightAt = env.TideHeightAt;

            // The sheet is ruled for two columns, so the page looks exactly as far ahead as it can print
            // — never further. (The owner's editor window honours the full WindowDays; it can scroll.)
            int columns = Mathf.Clamp(settings.WindowDays, 1, MaxColumns);
            double pageStart = TideAlmanac.DayStartSeconds(now, secondsPerDay);
            double pageEnd = pageStart + columns * secondsPerDay;

            TideAlmanac.FindTurns(heightAt, pageStart, pageEnd, slopeDt, scanStep, horizon,
                                  Mathf.Max(1, settings.MaxTurns), _turns);

            BuildStandingRead(page, heightAt, now, slopeDt, scanStep, horizon, secondsPerHour, secondsPerDay);

            // One ruled column per day: today, then tomorrow — the span a crossing is planned over.
            int today = TideAlmanac.DayIndexOf(now, secondsPerDay);
            for (int i = 0; i < columns; i++)
            {
                float x = 40f + i * (ColumnW + 20f);
                BuildDayColumn(page, x, ColumnTop, today + i, i == 0, now, secondsPerDay);
            }

            BuildFooterControls(page);
        }

        private void BuildHeader(RectTransform page)
        {
            MakeText(page, HudStrings.TideTableTitle, 44, TextAnchor.UpperLeft, 40f, -28f, 520f, 56f, Ink);

            // The region the page is FOR. A tide table is local — saying so is both flavour and truth,
            // since the heights are the active region's profile and no one else's.
            string region = GameServices.CurrentRegionId;
            if (!string.IsNullOrEmpty(region))
                MakeText(page, HudStrings.TideTableForRegion(region), 24, TextAnchor.UpperRight,
                         PanelW - 380f - 40f, -40f, 380f, 34f, InkFaint);

            MakeRule(page, 40f, -92f, PanelW - 80f);
        }

        // The standing read: what the water is doing this minute, in the same words the HUD uses.
        private void BuildStandingRead(RectTransform page, Func<double, float> heightAt, double now,
                                       double slopeDt, double scanStep, double horizon,
                                       float secondsPerHour, double secondsPerDay)
        {
            TideState st = TideReadout.Derive(heightAt, now, slopeDt, scanStep, horizon);

            string line = HudStrings.TideTableNowLabel + "  "
                        + HudFormat.ClockHHMM(TideAlmanac.HourOfDayAt(now, secondsPerDay)) + "   ·   "
                        + HudFormat.HeightMeters(st.HeightMeters) + "   ·   "
                        + HudStrings.TideArrow(st.Rising) + " " + HudStrings.TideDirection(st.Rising);
            MakeText(page, line, 28, TextAnchor.UpperLeft, 40f, -110f, PanelW - 80f, 38f, Ink);

            string turn;
            if (st.HasTurn && secondsPerHour > 0f)
            {
                float turnClock = TideAlmanac.HourOfDayAt(now + st.SecondsToTurn, secondsPerDay);
                turn = HudStrings.TideTableNextTurn(st.Rising)
                     + " " + HudFormat.DurationHMM(st.SecondsToTurn, secondsPerHour)
                     + "  (" + HudFormat.ClockHHMM(turnClock) + ")";
            }
            else
            {
                turn = HudStrings.TideTableNoTurn;
            }
            MakeText(page, turn, 26, TextAnchor.UpperLeft, 40f, -148f, PanelW - 80f, 36f, InkFaint);

            MakeRule(page, 40f, -190f, PanelW - 80f);
        }

        // One day's column of turns, with the "now" pencil mark slotted into its true position.
        private void BuildDayColumn(RectTransform page, float x, float y, int dayIndex, bool isToday,
                                    double now, double secondsPerDay)
        {
            MakeText(page, HudStrings.TideTableDayHeading(isToday, dayIndex), 28, TextAnchor.UpperLeft,
                     x, y, ColumnW, 36f, Ink);
            MakeRule(page, x, y - 38f, ColumnW);

            float rowY = FirstRowY;
            bool any = false, nowMarked = false;

            for (int i = 0; i < _turns.Count; i++)
            {
                TideTurn turn = _turns[i];
                if (TideAlmanac.DayIndexOf(turn.Seconds, secondsPerDay) != dayIndex) continue;
                any = true;

                // The mark goes in before the first turn still ahead of us — so the player reads the
                // page as "these have gone, these are coming".
                if (isToday && !nowMarked && now <= turn.Seconds)
                {
                    MakeNowRow(page, x, rowY, now, secondsPerDay);
                    rowY -= RowH;
                    nowMarked = true;
                }

                MakeTurnRow(page, x, rowY, turn, secondsPerDay);
                rowY -= RowH;
            }

            // Now falls after this day's last turn (late evening) — the mark still belongs on the page.
            if (isToday && !nowMarked)
            {
                MakeNowRow(page, x, rowY, now, secondsPerDay);
                rowY -= RowH;
            }

            if (!any)
                MakeText(page, HudStrings.TideTableNoTurnsToday, 24, TextAnchor.UpperLeft,
                         x + 8f, rowY, ColumnW - 8f, 34f, InkFaint);
        }

        private void MakeTurnRow(RectTransform page, float x, float y, in TideTurn turn, double secondsPerDay)
        {
            // Glyph + WORD + time + height. The colour is the fourth channel, never the only one (§8).
            Color tint = turn.IsHigh ? HighInk : LowInk;
            MakeText(page, HudStrings.TideTurnLabel(turn.IsHigh), 26, TextAnchor.UpperLeft,
                     x + 8f, y, 190f, 34f, tint);
            MakeText(page, HudFormat.ClockHHMM(TideAlmanac.HourOfDayAt(turn.Seconds, secondsPerDay)),
                     26, TextAnchor.UpperLeft, x + 202f, y, 90f, 34f, Ink);
            MakeText(page, HudFormat.HeightMeters(turn.HeightMeters), 26, TextAnchor.UpperRight,
                     x + 292f, y, 84f, 34f, Ink);
        }

        private void MakeNowRow(RectTransform page, float x, float y, double now, double secondsPerDay)
        {
            MakeText(page, HudStrings.TideTableNowMarker, 26, TextAnchor.UpperLeft,
                     x + 8f, y, 190f, 34f, NowInk);
            MakeText(page, HudFormat.ClockHHMM(TideAlmanac.HourOfDayAt(now, secondsPerDay)),
                     26, TextAnchor.UpperLeft, x + 202f, y, 90f, 34f, NowInk);
        }

        // The one control on the page, plus the key that does the same thing. Keyboard and gamepad
        // reach it because it is a real uGUI Button and it takes the initial selection (PC-first).
        private void BuildFooterControls(RectTransform page)
        {
            MakeRule(page, 40f, -(PanelH - 92f), PanelW - 80f);

            MakeText(page, HudStrings.TideTableCloseHint, 22, TextAnchor.UpperLeft,
                     40f, -(PanelH - 78f), PanelW - 300f, 32f, InkFaint);

            GameObject close = MakeButton(page, "Close", HudStrings.TideTableClose,
                                          PanelW - 220f, -(PanelH - 72f), 180f, 48f, Close);

            var es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(close);
        }

        // ---- builders (all anchored to the page's TOP-LEFT; x grows right, y grows down) ---------

        /// <summary>
        /// A centred sheet of the given size, returning a TOP-LEFT-pivoted content host that fills it —
        /// so every caller above lays out in one frame (x grows right, y grows down from the sheet's
        /// top-left corner). <paramref name="offset"/> nudges the sheet itself, which is how the shadowed
        /// paper edge sits behind and below the page.
        /// </summary>
        private static RectTransform MakePlate(RectTransform parent, string name, float w, float h,
                                               Color color, Vector2 offset = default)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(w, h);
            var img = go.GetComponent<Image>();
            img.color = color; img.raycastTarget = true;

            // Anchored to the sheet's top-left corner with a matching pivot, so a zero anchoredPosition
            // puts the host's corner exactly on the sheet's.
            var host = new GameObject("Content", typeof(RectTransform));
            host.transform.SetParent(rt, false);
            var hostRt = host.GetComponent<RectTransform>();
            hostRt.anchorMin = new Vector2(0f, 1f); hostRt.anchorMax = new Vector2(0f, 1f);
            hostRt.pivot = new Vector2(0f, 1f);
            hostRt.anchoredPosition = Vector2.zero;
            hostRt.sizeDelta = new Vector2(w, h);
            return hostRt;
        }

        private static Text MakeText(RectTransform parent, string text, int fontSize, TextAnchor align,
                                     float x, float y, float w, float h, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
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
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            // Deliberately no Outline here: the HUD outlines text to survive busy water, but ink on
            // paper wants no halo — the page supplies its own contrast.
            return t;
        }

        private static void MakeRule(RectTransform parent, float x, float y, float w)
        {
            var go = new GameObject("Rule", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, 2f);
            var img = go.GetComponent<Image>();
            img.color = Rule; img.raycastTarget = false;
        }

        private static GameObject MakeButton(RectTransform parent, string name, string label,
                                             float x, float y, float w, float h, UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);

            var img = go.GetComponent<Image>();
            img.color = ButtonBg; img.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(onClick);

            MakeText(rt, label, 26, TextAnchor.MiddleCenter, 0f, 0f, w, h, Ink);
            return go;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            // Needed for the Close button's clicks and for keyboard/gamepad selection. Same module and
            // default actions the market screens install (new Input System — the project's backend).
            var es = new GameObject("EventSystem", typeof(EventSystem));
            var module = es.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
            DontDestroyOnLoad(es);
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
