using UnityEngine;
using UnityEngine.InputSystem;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The key that takes the tide table out and puts it away (VS-06). A deliberately tiny component so
    /// the page itself (<see cref="TidePanel"/>) stays a view with no opinion about how it was opened.
    ///
    /// <para><b>Why a key, for a diegetic read.</b> The page is the diegetic object — paper you consult
    /// and put down — and a key is how you reach for it, the same way the market screens are reached.
    /// The real destination is a world interaction (the table pinned by the cottage door, or an almanac
    /// in the boat), which needs only to call <see cref="TidePanel.Open"/>: that entry point is public
    /// and argument-free precisely so world-content can wire one without this class changing.</para>
    ///
    /// <para><b>The binding.</b> <c>Tab</c> — the table, tabbed out. It held <c>N</c> from VS-06
    /// until 2026-08-20, when the owner gave N to the notebook (<c>NotebookPresenter.OpenKey</c>) and
    /// ruled the tide table onto Tab; Tab was audited free of every binding in the project including
    /// the <c>.inputactions</c> asset (the sweep surface the C-key trap taught). Serialized so the
    /// owner can rebind it without touching code — the same courtesy <c>BoatSpotlight</c> extends for
    /// its lamp key. New Input System only: legacy <c>UnityEngine.Input</c> compiles in this project
    /// and then throws at runtime.</para>
    /// </summary>
    public sealed class TidePanelInput : MonoBehaviour
    {
        [Tooltip("The key that opens and closes the tide table. Tab since 2026-08-20 (N belongs to " +
                 "the notebook). Rebindable here without touching code.")]
        [SerializeField] private Key _toggleKey = Key.Tab;

        [Tooltip("Let the key work at all. Off = the page can still be opened from code or a world " +
                 "interaction (TidePanel.Open), just not by this keypress.")]
        [SerializeField] private bool _keyOpensTable = true;

        private void Update()
        {
            if (!_keyOpensTable) return;

            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb[_toggleKey].wasPressedThisFrame) TidePanel.Toggle();
        }
    }
}
