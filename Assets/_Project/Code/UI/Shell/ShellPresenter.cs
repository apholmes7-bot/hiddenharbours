using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The one listener that turns a <see cref="ShellPhaseChanged"/> into a page on screen (M1 §7.8).
    ///
    /// <para><b>Why it exists at all.</b> The composition root (App) decides the phase; the pages live in
    /// UI; and App does not reference UI (nor should it — rule 4). So the phase travels as a Core signal
    /// and this is what is listening at the other end.</para>
    ///
    /// <para><b>Self-installing, like <c>SaveService</c> and <c>AudioDirector</c>, and for the same
    /// reason:</b> no scene wiring, so the shell exists in whatever scene the game boots from without a
    /// scene builder being re-run over the owner's hand-painted work (ADR 0011). It installs at
    /// <c>AfterSceneLoad</c> — after every scene <c>Awake</c> and BEFORE any <c>Start</c>, so it is
    /// subscribed in time for <c>GameRoot.Start</c>'s boot edge.</para>
    ///
    /// <para><b>It is inert unless a boot flow runs.</b> The phase defaults to
    /// <see cref="ShellPhase.Playing"/>, so a PlayMode test, an EditMode rig or the owner pressing Play
    /// straight into a region scene gets a presenter that subscribes, sees the world it is already in,
    /// and draws nothing.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShellPresenter : MonoBehaviour
    {
        private static ShellPresenter _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[Shell]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ShellPresenter>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            EventBus.Subscribe<ShellPhaseChanged>(OnPhaseChanged);

            // Catch up on the phase we were born into. Belt and braces against install order: if a boot
            // flow somehow got there first, the page still appears rather than the world silently sitting
            // stopped behind nothing.
            Apply(ShellFlow.Phase);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<ShellPhaseChanged>(OnPhaseChanged);
            if (_instance == this) _instance = null;
        }

        private void OnPhaseChanged(ShellPhaseChanged e) => Apply(e.Phase);

        private static void Apply(ShellPhase phase)
        {
            if (phase == ShellPhase.Title)
            {
                // Leaving the world (quit to title) must not leave a pause menu hanging over the new one.
                PauseMenu.CloseIfOpen();
                TitleScreen.Open();
            }
            else
            {
                TitleScreen.CloseIfOpen();
            }
        }

        /// <summary>
        /// The pause key. It lives here rather than on its own component because the shell already has
        /// exactly one persistent host, and a second one would only be another thing to keep alive.
        ///
        /// <para><b>Esc is shared</b>, so this deliberately declines rather than fighting for it: not at
        /// the title (nothing to pause), not while a shell page of its own is up (each closes itself with
        /// Esc), not under the tide table (whose close would otherwise hand the clock back out from under
        /// a pause menu), and not while a modal owns interaction (a dialogue mid-sentence).</para>
        /// </summary>
        private void Update()
        {
            if (!PauseKeyPressed()) return;
            if (!CanTogglePause()) return;
            PauseMenu.Toggle();
        }

        private static bool PauseKeyPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;

            // Start on a pad — the console/handheld convention, free here because the shell is built on
            // real Selectables. New Input System only; legacy UnityEngine.Input throws at runtime here.
            var pad = Gamepad.current;
            return pad != null && pad.startButton.wasPressedThisFrame;
        }

        private static bool CanTogglePause()
        {
            if (ShellFlow.AtTitle) return false;
            // The sheet is checked FIRST: opened from the pause menu it sits ON TOP of a menu that is
            // still open, and Esc there means Back, not "close the menu underneath as well".
            if (SettingsSheet.IsOpen) return false;
            if (PauseMenu.IsOpen) return true;          // Esc closes the menu it opened
            if (TidePanel.IsOpen) return false;         // its Esc puts the page away
            // The wardrobe's Esc leaves the wardrobe. Asked as "owns the key THIS frame" rather than
            // "is open", because the picker closes itself on that same press and the two Updates run in
            // an order nobody defines — see WardrobePicker.OwnsCancelKey.
            if (WardrobePicker.OwnsCancelKey) return false;
            // The notebook's Esc shuts the book. Asked through Core rather than of the book itself:
            // NotebookPresenter is in World and UI may not reference it (rule 4), so the claim is the
            // seam — and it is the same "owns the key THIS frame" question, for the same race.
            if (CancelKeyClaim.OwnedThisFrame) return false;
            if (InteractionGate.IsBlocked) return false; // a modal (dialogue) owns the key
            return true;
        }
    }
}
