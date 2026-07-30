using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The title page (M1 §7.8) — the first thing a tester sees, and the only door into the game.
    /// Continue, New game, Quit; and the confirm that stands between an impatient click and somebody's
    /// harbour.
    ///
    /// <para><b>It is a view, not the state.</b> <see cref="ShellFlow"/> owns the phase and stopped the
    /// clock before this page existed; the page renders that phase and reports the player's choice back.
    /// It never freezes time itself, so there is exactly one thing in the project that can stop the world
    /// (rule 5, and the tide table's precedent).</para>
    ///
    /// <para><b>The picture is the game.</b> No painted key art — the world is already rendering behind
    /// this page, so the title is a dusk scrim over the harbour with the wordmark on it. Hand-painted
    /// title art is not something to spend before the GO/POLISH/PIVOT verdict is in (§7.8).</para>
    ///
    /// <para>Self-building and code-driven (no prefab), like the HUD, the tide table and the market
    /// screens; reads state only through Core, which the UI assembly's references structurally enforce.
    /// Built ONCE on open, no per-frame work beyond the cancel key (rule 7).</para>
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        private static TitleScreen _instance;

        /// <summary>True while the page is up.</summary>
        public static bool IsOpen => _instance != null;

        // Layout, in the 1280x720 reference frame (x right, y down from the top-left corner).
        private const float MarginX      = 110f;
        private const float WordmarkY    = -152f;
        private const float RuleY        = -252f;
        private const float MenuTopY     = -324f;
        private const float MenuItemW    = 380f;
        private const float MenuItemH    = 54f;
        private const float MenuStep     = 64f;
        private const float DetailX      = MarginX + MenuItemW + 28f;
        private const float ConfirmBodyW = 660f;
        private const float HintY        = -664f;

        private RectTransform _host;
        private GameObject _menu;       // the three lines
        private GameObject _confirm;    // the New Game confirm, built lazily and toggled

        /// <summary>Open the page (reusing the one that is up, if any). Argument-free so any opener can
        /// call it — today the shell presenter, on the phase edge.</summary>
        public static TitleScreen Open()
        {
            if (_instance == null)
            {
                var go = new GameObject("TitleScreen");
                _instance = go.AddComponent<TitleScreen>();   // Awake builds the page
            }
            return _instance;
        }

        /// <summary>Take the page down, if it is up. Safe to call when it is not.</summary>
        public static void CloseIfOpen()
        {
            if (_instance != null) _instance.Close();
        }

        private void Awake()
        {
            PaperUi.EnsureEventSystem();
            Build();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>Release the slot NOW rather than at end of frame — <see cref="Object.Destroy"/> only
        /// resolves when the frame does, and until then a closed page would still answer
        /// <see cref="IsOpen"/> and a same-frame reopen would hand back the page on its way out (the
        /// tide table's regression, avoided here by construction).</summary>
        private void Close()
        {
            if (_instance == this) _instance = null;
            Destroy(gameObject);
        }

        // Cancel backs out of the confirm — the destructive question is never a trap you have to answer.
        // At the top level there is nothing to cancel to: Quit is a line on the menu, not a keypress.
        private void Update()
        {
            if (_confirm == null || !_confirm.activeSelf) return;

            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) ||
                (pad != null && pad.buttonEast.wasPressedThisFrame))
                ShowMenu();
        }

        // ---- the page ---------------------------------------------------------------------------

        private void Build()
        {
            _host = PaperUi.MakeScreen(transform, "TitleScreen_Canvas", PaperUi.TitleSortingOrder);
            PaperUi.MakeScrim(_host, PaperUi.Scrim);

            PaperUi.MakeText(_host, ShellStrings.GameTitle, 82, TextAnchor.UpperLeft,
                             MarginX, WordmarkY, 900f, 108f, PaperUi.Chalk);
            PaperUi.MakeRule(_host, MarginX, RuleY, 520f);

            BuildMenu();

            PaperUi.MakeText(_host, ShellStrings.MenuHint, 22, TextAnchor.UpperLeft,
                             MarginX, HintY, 620f, 32f, PaperUi.ChalkFaint);
        }

        private void BuildMenu()
        {
            _menu = MakeColumn("Menu");
            var column = (RectTransform)_menu.transform;

            bool canContinue = ShellFlow.HasGameToContinue;
            float y = MenuTopY;

            // Up to four lines, built in the order the player should walk them. Continue is offered only
            // when there is something behind it — a first launch must not dangle a button that loads
            // nothing (and would silently mean "new game" if it were pressed).
            var items = new Selectable[canContinue ? 4 : 3];
            int n = 0;

            if (canContinue)
            {
                items[n++] = PaperUi.MakeMenuItem(column, "Continue", ShellStrings.Continue,
                                                  MarginX, y, MenuItemW, MenuItemH, OnContinue);
                PaperUi.MakeText(column, DescribeSavedGame(), 24, TextAnchor.MiddleLeft,
                                 DetailX, y, 420f, MenuItemH, PaperUi.ChalkFaint);
                y -= MenuStep;
            }

            items[n++] = PaperUi.MakeMenuItem(column, "NewGame", ShellStrings.NewGame,
                                              MarginX, y, MenuItemW, MenuItemH, OnNewGame);
            if (!canContinue)
                PaperUi.MakeText(column, ShellStrings.NoGameYet, 24, TextAnchor.MiddleLeft,
                                 DetailX, y, 420f, MenuItemH, PaperUi.ChalkFaint);
            y -= MenuStep;

            items[n++] = PaperUi.MakeMenuItem(column, "Settings", ShellStrings.Settings,
                                              MarginX, y, MenuItemW, MenuItemH, OnSettings);
            y -= MenuStep;

            items[n] = PaperUi.MakeMenuItem(column, "Quit", ShellStrings.Quit,
                                            MarginX, y, MenuItemW, MenuItemH, OnQuit);

            PaperUi.WireVerticalNavigation(items);
        }

        /// <summary>The line beside Continue: which game is waiting. A missing or not-yet-loaded save
        /// service degrades to no line at all rather than a plausible wrong one (P1 truth).</summary>
        private static string DescribeSavedGame()
        {
            var save = GameServices.Save;
            SaveData data = save != null ? save.Current : null;
            return data == null ? string.Empty : ShellStrings.SavedGameSummary(data.DayIndex, data.Money);
        }

        // ---- the confirm ------------------------------------------------------------------------

        /// <summary>
        /// The guard on the destructive line. Built lazily (a first launch never sees it) and kept
        /// alongside the menu rather than replacing it, so backing out is instant and cannot half-build.
        /// The SAFE answer is first and takes the selection — a player who mashes Enter keeps their game.
        /// </summary>
        private void ShowConfirm()
        {
            if (_confirm == null) BuildConfirm();
            _menu.SetActive(false);
            _confirm.SetActive(true);

            var keep = _confirm.transform.Find("KeepGame");
            var es = EventSystem.current;
            if (es != null && keep != null) es.SetSelectedGameObject(keep.gameObject);
        }

        private void ShowMenu()
        {
            if (_confirm != null) _confirm.SetActive(false);
            _menu.SetActive(true);

            var newGame = _menu.transform.Find("NewGame");
            var es = EventSystem.current;
            if (es != null && newGame != null) es.SetSelectedGameObject(newGame.gameObject);
        }

        private void BuildConfirm()
        {
            _confirm = MakeColumn("Confirm");
            var column = (RectTransform)_confirm.transform;

            PaperUi.MakeText(column, ShellStrings.ConfirmNewGameTitle, 38, TextAnchor.UpperLeft,
                             MarginX, MenuTopY, ConfirmBodyW, 50f, PaperUi.Chalk);
            PaperUi.MakeText(column, ShellStrings.ConfirmNewGameBody, 24, TextAnchor.UpperLeft,
                             MarginX, MenuTopY - 54f, ConfirmBodyW, 90f, PaperUi.ChalkFaint);

            // What is actually at stake, in the same words the menu used to describe it.
            PaperUi.MakeText(column, DescribeSavedGame(), 24, TextAnchor.UpperLeft,
                             MarginX, MenuTopY - 150f, ConfirmBodyW, 34f, PaperUi.ChalkFaint);

            float y = MenuTopY - 200f;
            var keep = PaperUi.MakeMenuItem(column, "KeepGame", ShellStrings.ConfirmNewGameNo,
                                            MarginX, y, MenuItemW, MenuItemH, ShowMenu);
            var overwrite = PaperUi.MakeMenuItem(column, "Overwrite", ShellStrings.ConfirmNewGameYes,
                                                 MarginX, y - MenuStep, MenuItemW, MenuItemH,
                                                 OnConfirmNewGame, PaperUi.WarnInk);

            PaperUi.WireVerticalNavigation(new[] { keep, overwrite });
        }

        // ---- choices ----------------------------------------------------------------------------

        private void OnContinue() => ShellFlow.ContinueGame();

        private void OnNewGame()
        {
            // Nothing to lose on a first launch — the confirm would be a question with one answer.
            if (ShellFlow.HasGameToContinue) ShowConfirm();
            else ShellFlow.StartNewGame();
        }

        private void OnConfirmNewGame() => ShellFlow.StartNewGame();

        /// <summary>The settings sheet opens over the page and hides it, so Back lands back on the title
        /// rather than dumping the player somewhere. A tester must be able to fix the volume BEFORE
        /// starting, not only after being startled by it.</summary>
        private void OnSettings()
        {
            PaperUi.SetPageVisible(_host, false);
            SettingsSheet.Open(ReturnFromSettings);
        }

        private void ReturnFromSettings()
        {
            if (_host == null) return;
            PaperUi.SetPageVisible(_host, true);

            var settings = _host.Find("Menu/Settings");
            var es = EventSystem.current;
            if (es != null && settings != null) es.SetSelectedGameObject(settings.gameObject);
        }

        private void OnQuit() => ShellQuit.QuitApplication();

        // ---- helpers ----------------------------------------------------------------------------

        /// <summary>A full-size, top-left-pinned group inside the page, so a whole column of content can
        /// be shown or hidden as one thing.</summary>
        private GameObject MakeColumn(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_host, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = PaperUi.ReferenceResolution;
            return go;
        }
    }
}
