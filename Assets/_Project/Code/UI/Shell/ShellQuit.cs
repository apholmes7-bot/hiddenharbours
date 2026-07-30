using UnityEngine;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// Leaving the game (M1 §7.8). One line of real work, in one place, because the title page and the
    /// pause menu must mean exactly the same thing by "Quit" — and because <c>Application.Quit</c> does
    /// nothing at all in the editor, which is how a Quit button ships looking broken to the one person
    /// who tested it in Play mode.
    /// </summary>
    public static class ShellQuit
    {
        /// <summary>
        /// Close the game. The save is NOT written here on purpose: <c>SaveService</c> already autosaves
        /// on <c>OnApplicationQuit</c> (and declines to when the world was never entered), so a second
        /// write from the UI would be a second opinion about the player's data.
        /// </summary>
        public static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
