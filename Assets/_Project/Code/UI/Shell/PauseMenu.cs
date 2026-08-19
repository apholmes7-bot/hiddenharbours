using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The pause menu (M1 §7.8) — mid-session, over a stopped world: carry on, change the volume, go back
    /// to the title, or stop playing.
    ///
    /// <para><b>It stops the world the way the tide table does</b>, through
    /// <see cref="ShellPause"/> → <see cref="IGameClock.IsPaused"/>: the project's ONE pause path, no
    /// second clock, and on resume it restores what it found rather than assuming the world was running
    /// (open the pause menu while the tide table has already stopped time and closing it does not start
    /// the sea again).</para>
    ///
    /// <para><b>And it takes the controls with it.</b> Raising <see cref="InteractionGate"/> stops the
    /// shared Interact key reaching the world underneath — the convention the dialogue panel set — while
    /// <see cref="ShellFlow.WorldInputBlocked"/> (true while paused) is what the player rig honours so the
    /// helm cannot be steered from behind the menu. A pause you can sail through is not one.</para>
    ///
    /// <para><b>Both ways out save first.</b> Quit to title and quit to desktop each write the game before
    /// they go, and the page says so under them — a tester should never be deciding whether their last
    /// hour counts.</para>
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        private static PauseMenu _instance;

        /// <summary>True while the menu is up.</summary>
        public static bool IsOpen => _instance != null;

        private const float MarginX  = 110f;
        private const float TitleY   = -132f;
        private const float RuleY    = -196f;
        private const float FirstY   = -252f;
        private const float ItemW    = 420f;
        private const float ItemH    = 54f;
        private const float ItemStep = 64f;
        private const float HintY    = -664f;

        private RectTransform _host;

        /// <summary>
        /// Open the menu and stop the world. Refuses at the title — there is no session to pause, and a
        /// pause menu over the title page would be a door to nowhere. Reuses the open menu if there is one.
        /// </summary>
        public static PauseMenu Open()
        {
            if (ShellFlow.AtTitle) return null;

            if (_instance == null)
            {
                var go = new GameObject("PauseMenu");
                _instance = go.AddComponent<PauseMenu>();   // Awake stops the world and builds the page
            }
            return _instance;
        }

        /// <summary>Close the menu and give the world back, if it is up. Safe to call when it is not.</summary>
        public static void CloseIfOpen()
        {
            if (_instance != null) _instance.Close();
        }

        /// <summary>Open the menu, or close it if it is already up — what the Esc key does.</summary>
        public static void Toggle()
        {
            if (_instance != null) _instance.Close();
            else Open();
        }

        private bool _gateWasBlocked;
        private bool _released;

        private void Awake()
        {
            PaperUi.EnsureEventSystem();
            ShellPause.Pause();
            _gateWasBlocked = InteractionGate.IsBlocked;
            InteractionGate.IsBlocked = true;
            Build();
        }

        private void OnDestroy()
        {
            // Runs however the menu died — including a scene teardown taking it with the world. Nothing
            // may leave the clock or the interact gate wedged.
            Release();
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// Give back the world and the interact key, restoring what was found rather than assuming.
        /// <b>Once per menu</b>: <see cref="Object.Destroy"/> only resolves at end of frame, so an
        /// already-closed menu's <c>OnDestroy</c> would otherwise arrive AFTER a replacement had stopped
        /// the world again — and resume it out from under the page that is up.
        /// </summary>
        private void Release()
        {
            if (_released) return;
            _released = true;

            ShellPause.Resume();
            InteractionGate.IsBlocked = _gateWasBlocked;
        }

        /// <summary>Put the menu away and hand the world back NOW, not at end of frame — see
        /// <c>TidePanel</c>'s regression: a Destroy that has not resolved still answers
        /// <see cref="IsOpen"/>, and a same-frame reopen would be handed the page on its way out and
        /// silently skip stopping the world.</summary>
        private void Close()
        {
            Release();
            if (_instance == this) _instance = null;
            Destroy(gameObject);
        }

        // ---- the page ---------------------------------------------------------------------------

        private void Build()
        {
            _host = PaperUi.MakeScreen(transform, "PauseMenu_Canvas", PaperUi.PauseSortingOrder);
            PaperUi.MakeScrim(_host, PaperUi.Scrim);

            PaperUi.MakeText(_host, ShellStrings.PauseTitle, 52, TextAnchor.UpperLeft,
                             MarginX, TitleY, 800f, 66f, PaperUi.Chalk);
            PaperUi.MakeRule(_host, MarginX, RuleY, 520f);

            float y = FirstY;
            var items = new Selectable[5];

            items[0] = PaperUi.MakeMenuItem(_host, "Resume", ShellStrings.Resume,
                                            MarginX, y, ItemW, ItemH, OnResume);
            y -= ItemStep;
            items[1] = PaperUi.MakeMenuItem(_host, "Notebook", ShellStrings.Notebook,
                                            MarginX, y, ItemW, ItemH, OnNotebook);
            y -= ItemStep;
            items[2] = PaperUi.MakeMenuItem(_host, "Settings", ShellStrings.Settings,
                                            MarginX, y, ItemW, ItemH, OnSettings);
            y -= ItemStep + 12f;
            items[3] = PaperUi.MakeMenuItem(_host, "QuitToTitle", ShellStrings.QuitToTitle,
                                            MarginX, y, ItemW, ItemH, OnQuitToTitle);
            y -= ItemStep;
            items[4] = PaperUi.MakeMenuItem(_host, "QuitToDesktop", ShellStrings.QuitToDesktop,
                                            MarginX, y, ItemW, ItemH, OnQuitToDesktop);
            y -= ItemStep;

            // Said once, under both of them, rather than twice beside them.
            PaperUi.MakeText(_host, ShellStrings.QuitSavesFirst, 22, TextAnchor.UpperLeft,
                             MarginX + 36f, y + 8f, ItemW, 30f, PaperUi.ChalkFaint);

            PaperUi.WireVerticalNavigation(items);

            PaperUi.MakeText(_host, ShellStrings.PauseHint, 22, TextAnchor.UpperLeft,
                             MarginX, HintY, 720f, 32f, PaperUi.ChalkFaint);
        }

        // ---- choices ----------------------------------------------------------------------------

        private void OnResume() => Close();

        /// <summary>
        /// Take the notebook out.
        ///
        /// <para><b>This row is the whole of the book's binding, and that is the point.</b> The dev-key
        /// ledger is spent A-Z, so the notebook adds NO key: it rides the one control the player
        /// already has for "stop and deal with something", and anything else that ever wants to open
        /// the book publishes the same signal (a desk, a bedside table) without touching this file.</para>
        ///
        /// <para><b>The menu goes away first, and the world starts again.</b> The book is a thing she
        /// holds while standing in the world, not another page of the menu — so the pause is released
        /// on the way out and the book takes the interact verb instead (via
        /// <c>InteractionGate</c>), which is how every modal in this repo says the key is its own.
        /// ⚠️ Whether the world should instead STOP behind the open book is a taste call the owner
        /// owes; it is one line here and one in the presenter either way.</para>
        ///
        /// <para>Published rather than called: <c>NotebookPresenter</c> lives in World and this is UI,
        /// and neither module may reach into the other (rule 4).</para>
        /// </summary>
        private void OnNotebook()
        {
            Close();
            EventBus.Publish(new NotebookRequested("menu.pause"));
        }

        /// <summary>Open the settings over the menu and hide it, so Back lands here rather than in the
        /// world. The world stays stopped throughout — the pause is not released until Resume.</summary>
        private void OnSettings()
        {
            PaperUi.SetPageVisible(_host, false);
            SettingsSheet.Open(ReturnFromSettings);
        }

        private void ReturnFromSettings()
        {
            PaperUi.SetPageVisible(_host, true);

            var resume = _host != null ? _host.Find("Resume") : null;
            var es = EventSystem.current;
            if (es != null && resume != null) es.SetSelectedGameObject(resume.gameObject);
        }

        /// <summary>Save, then leave the world. The menu takes itself down first: the core it is sitting
        /// on is about to be rebuilt, and the released clock must be handed back before it goes.</summary>
        private void OnQuitToTitle()
        {
            Release();
            if (_instance == this) _instance = null;
            Destroy(gameObject);

            ShellFlow.QuitToTitle();
        }

        /// <summary>Save, then close the game. <c>SaveService</c> also autosaves on quit, but the write is
        /// made here explicitly rather than trusting a platform to deliver that callback.</summary>
        private void OnQuitToDesktop()
        {
            var save = GameServices.Save;
            if (save != null) save.Save();

            ShellQuit.QuitApplication();
        }
    }
}
