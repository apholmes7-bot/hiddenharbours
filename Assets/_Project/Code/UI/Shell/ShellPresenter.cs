using UnityEngine;
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
            if (phase == ShellPhase.Title) TitleScreen.Open();
            else TitleScreen.CloseIfOpen();
        }
    }
}
