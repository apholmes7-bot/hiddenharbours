using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The screen the HUD lays itself out on</b>, in SCREEN PIXELS: the player's own
    /// (<see cref="Screen.width"/>, <see cref="Screen.height"/>, <see cref="Screen.safeArea"/>), unless a
    /// plate has handed it another.
    ///
    /// <para><b>A test seam</b>, as <c>CameraFollow.ScreenPixelsOverride</c> is the camera's. A batch
    /// editor has one screen, and the helm band's plates draw the HUD at 720p to 4K, so every overlay
    /// that places itself by the screen — the helm host, the instrument hosts, the HUD, the toast, the
    /// popup and the quest note — reads it here. Unset, it IS the player's screen, read fresh each
    /// time: nothing in the game sets it.</para>
    /// </summary>
    public static class HudScreen
    {
        private static Vector2Int _override;

        /// <summary>True while a plate has handed the HUD a screen.</summary>
        public static bool IsOverridden => _override.x > 0 && _override.y > 0;

        /// <summary>The screen's width, px.</summary>
        public static int Width => IsOverridden ? _override.x : Screen.width;

        /// <summary>The screen's height, px.</summary>
        public static int Height => IsOverridden ? _override.y : Screen.height;

        /// <summary>The safe area, px: the whole of a plate's screen while one is set.</summary>
        public static Rect SafeArea => IsOverridden ? new Rect(0f, 0f, _override.x, _override.y) : Screen.safeArea;

        /// <summary>Lay the HUD out on <paramref name="size"/> px. A size without both sides above 0
        /// hands back the player's screen.</summary>
        public static void Override(Vector2Int size)
            => _override = size.x > 0 && size.y > 0 ? size : Vector2Int.zero;

        /// <summary>The player's own screen again.</summary>
        public static void Reset() => _override = Vector2Int.zero;
    }
}
